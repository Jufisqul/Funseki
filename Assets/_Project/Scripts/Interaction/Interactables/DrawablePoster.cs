using Funseki.Core;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Funseki.Interaction
{
    // Плакат или картина (GDD 5.9, spec «Плакаты и картины»). Рюта or Кайто with the marker in the inventory press E
    // (or LMB with the marker in hand) and draw on the picture itself: the close-up camera looks at it, LMB / A paints
    // under the mouse or the gamepad cursor, Esc / E finishes and the hero says a line. Once per picture; afterwards
    // nobody can use it any more. A teacher who sees the start or the drawing catches the hero at once
    // (PosterData.seenMeansCaught).
    //
    // The hand drawing goes into the save as a PNG under drawing_<objectId> (ISaveable, registered in Awake, so a
    // «Continue» hands it back before Start). obj_<id>_variant keeps a preset defaced picture as a fallback.
    public class DrawablePoster : InteractableObject, IItemTarget, ISaveable
    {
        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        static readonly int MainTex = Shader.PropertyToID("_MainTex");

        [Tooltip("The picture surface: a Quad (its local x / y map straight to the texture UV)")]
        [SerializeField] Renderer picture;
        [SerializeField] InputActionAsset actions;
        [SerializeField] CinemachineCamera closeUpCamera;

        PosterData D => (PosterData)data;
        MaterialPropertyBlock block;
        InspectSession session;
        InspectOverlay overlay;

        Texture2D canvas;
        Color32[] pixels;
        int painted;
        bool dirty;
        bool handDrawn;
        Vector2? lastUv;
        Vector2 padCursor;
        bool usingPad;
        GameObject artist;
        float nextWatch;
        readonly System.Collections.Generic.List<Renderer> hiddenHeroParts = new();

        int Variant => Flags.GetCounter(Key("variant"));
        bool Drawn => Flags.GetFlag(Key("drawn")) || Variant > 0;

        public override string Prompt => CanDraw(HeroService.CurrentObject) ? D.drawPrompt : data.prompt;

        void Awake()
        {
            if (actions != null) session = new InspectSession(actions);
            if (closeUpCamera != null) closeUpCamera.gameObject.SetActive(false);
            SaveRegistry.Register(this);
        }

        protected override void RestoreState()
        {
            if (handDrawn) SetTexture(canvas);
            else ShowVariant(Variant);
        }

        // Once drawn on, the picture is done: no E prompt for anyone.
        protected override bool CanUse(GameObject hero) => !Drawn && (session == null || session.CanOpen);

        bool CanDraw(GameObject hero)
        {
            if (Drawn || session == null || D.drawItem == null) return false;
            if (!IsAllowed(D.drawHeroes, HeroOf(hero))) return false;
            var bag = Bag;
            return bag != null && bag.Contains(D.drawItem);
        }

        protected override void OnUsed(GameObject hero, bool first, GameObject witness)
        {
            if (CanDraw(hero)) { BeginDrawing(hero, witness); return; }
            SayUsualLine(hero, first, witness);
        }

        // LMB with the marker in hand: the same as E for a hero who can draw.
        public bool UseItem(ItemData item, bool primary)
        {
            if (item == null || item != D.drawItem || !primary) return false;
            var hero = HeroService.CurrentObject;
            if (!CanDraw(hero) || !session.CanOpen) return false;
            BeginDrawing(hero, FindWatchingTeacher(hero));
            return true;
        }

        // ---------------------------------------------------------------- the mini-game

        void BeginDrawing(GameObject hero, GameObject witness)
        {
            artist = hero;
            if (witness != null && D.seenMeansCaught)
            {
                Caught(hero, witness);
                return;
            }

            session.Begin();
            PrepareCanvas();
            overlay ??= new InspectOverlay(D);
            overlay.Show(null);
            overlay.SetHint(D.drawHint);
            ShowCloseUp(true);
            padCursor = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            usingPad = false;
            lastUv = null;
            painted = 0;
            nextWatch = Time.unscaledTime + D.watchCheckInterval;
            Report(hero, "poster_drawing_started");
        }

        void Update()
        {
            if (session == null || !session.Active) return;
            if (session.ClosePressed) { Finish(); return; }

            if (Time.unscaledTime >= nextWatch)
            {
                nextWatch = Time.unscaledTime + D.watchCheckInterval;
                var witness = FindWatchingTeacher(artist);
                if (witness != null) { Finish(witness); return; }
            }

            Vector2 screen = CursorPosition();
            overlay.SetCaption(usingPad ? "+" : null, screen);
            if (session.PaintHeld && TryGetUv(screen, out var uv))
            {
                PaintStroke(lastUv ?? uv, uv);
                lastUv = uv;
            }
            else lastUv = null;

            if (dirty)
            {
                canvas.SetPixels32(pixels);
                canvas.Apply(false);
                dirty = false;
            }
        }

        Vector2 CursorPosition()
        {
            Vector2 stick = session.CursorMove;
            if (stick.sqrMagnitude > 0.04f)
            {
                usingPad = true;
                padCursor += stick * (D.cursorSpeed * Screen.height * Time.unscaledDeltaTime);
                padCursor.x = Mathf.Clamp(padCursor.x, 0f, Screen.width);
                padCursor.y = Mathf.Clamp(padCursor.y, 0f, Screen.height);
            }
            else if (session.PointerMoved) usingPad = false;
            return usingPad ? padCursor : session.Pointer;
        }

        bool TryGetUv(Vector2 screen, out Vector2 uv)
        {
            uv = default;
            var cam = Camera.main;
            if (cam == null || picture == null) return false;
            var t = picture.transform;
            var ray = cam.ScreenPointToRay(screen);
            if (!new Plane(t.forward, t.position).Raycast(ray, out float dist)) return false;
            Vector3 local = t.InverseTransformPoint(ray.GetPoint(dist));
            uv = new Vector2(local.x + 0.5f, local.y + 0.5f);
            return uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
        }

        void PaintStroke(Vector2 from, Vector2 to)
        {
            Vector2 a = new(from.x * canvas.width, from.y * canvas.height);
            Vector2 b = new(to.x * canvas.width, to.y * canvas.height);
            float step = Mathf.Max(1f, D.brushRadius * 0.5f);
            int stamps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / step));
            for (int i = 0; i <= stamps; i++) Stamp(Vector2.Lerp(a, b, i / (float)stamps));
        }

        void Stamp(Vector2 center)
        {
            int r = D.brushRadius, w = canvas.width, h = canvas.height;
            int cx = Mathf.RoundToInt(center.x), cy = Mathf.RoundToInt(center.y);
            Color32 ink = D.inkColor;
            for (int y = Mathf.Max(0, cy - r); y <= Mathf.Min(h - 1, cy + r); y++)
            for (int x = Mathf.Max(0, cx - r); x <= Mathf.Min(w - 1, cx + r); x++)
            {
                if ((x - cx) * (x - cx) + (y - cy) * (y - cy) > r * r) continue;
                int i = y * w + x;
                if (pixels[i].r == ink.r && pixels[i].g == ink.g && pixels[i].b == ink.b) continue;
                pixels[i] = ink;
                painted++;
                dirty = true;
            }
        }

        // The close-up camera looks only at the picture: the three heroes are hidden while it is on,
        // so the leader standing in front of the poster doesn't block the drawing.
        void ShowCloseUp(bool on)
        {
            if (closeUpCamera != null)
            {
                if (on) closeUpCamera.Priority = D.cameraPriority;
                closeUpCamera.gameObject.SetActive(on);
            }
            if (on)
            {
                hiddenHeroParts.Clear();
                foreach (HeroId id in System.Enum.GetValues(typeof(HeroId)))
                {
                    var hero = HeroService.GetHero(id);
                    if (hero == null) continue;
                    foreach (var r in hero.GetComponentsInChildren<Renderer>())
                        if (r.enabled) { r.enabled = false; hiddenHeroParts.Add(r); }
                }
                if (artist != null && hiddenHeroParts.Count == 0)
                    foreach (var r in artist.GetComponentsInChildren<Renderer>())
                        if (r.enabled) { r.enabled = false; hiddenHeroParts.Add(r); }
            }
            else
            {
                foreach (var r in hiddenHeroParts) if (r != null) r.enabled = true;
                hiddenHeroParts.Clear();
            }
        }

        // A readable copy of what the picture shows now; the source textures don't need Read/Write.
        void PrepareCanvas()
        {
            Texture source = TextureFor(Variant);
            int w = source != null ? source.width : 256, h = source != null ? source.height : 384;
            if (canvas == null || canvas.width != w || canvas.height != h)
            {
                if (canvas != null) Destroy(canvas);
                canvas = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = $"Drawing_{objectId}", wrapMode = TextureWrapMode.Clamp };
            }
            if (source != null)
            {
                var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(source, rt);
                var previous = RenderTexture.active;
                RenderTexture.active = rt;
                canvas.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
            else
            {
                var fill = new Color32[w * h];
                for (int i = 0; i < fill.Length; i++) fill[i] = new Color32(240, 232, 205, 255);
                canvas.SetPixels32(fill);
            }
            canvas.Apply(false);
            pixels = canvas.GetPixels32();
            SetTexture(canvas);
        }

        void Finish(GameObject witness = null)
        {
            session.End();
            overlay.Hide();
            ShowCloseUp(false);
            var hero = artist;

            if (witness != null && D.seenMeansCaught)
            {
                // Caught red-handed: the drawing stays (the evidence), the punishment follows.
                if (painted >= D.minPaintedPixels) MarkDrawn(hero);
                else ShowVariant(Variant);
                Caught(hero, witness);
                return;
            }
            if (painted < D.minPaintedPixels)
            {
                ShowVariant(Variant);
                return;
            }

            MarkDrawn(hero);
            witness = FindWatchingTeacher(hero);
            if (witness != null && D.drawNoiseAmount > 0)
                GameEvents.RaiseNoiseMade(hero, witness, D.drawNoiseReason, D.drawNoiseAmount);
            Say(hero, InteractableData.Pick(witness != null && D.drawSeenLines is { Length: > 0 } ? D.drawSeenLines : D.DrawLines(HeroOf(hero))));
        }

        void MarkDrawn(GameObject hero)
        {
            Flags.SetFlag(Key("drawn"));
            handDrawn = true;
            Flags.SetCounter(Key("variant"), PickVariant());
            Flags.AddCounter(Key("drawings"));
            if (!string.IsNullOrEmpty(D.drawnFlag)) Flags.SetFlag(D.drawnFlag);
            Report(hero, "poster_drawn");
        }

        void Caught(GameObject hero, GameObject witness)
        {
            Say(hero, InteractableData.Pick(D.drawSeenLines));
            Report(hero, "poster_caught");
            GameEvents.RaiseCaught(hero, witness, D.caughtScene);
        }

        // ---------------------------------------------------------------- the picture

        // 1-based index into drawnTextures for the reload; 0 when there are none (the picture then stays clean after a reload).
        int PickVariant() => D.drawnTextures is { Length: > 0 } ? Random.Range(1, D.drawnTextures.Length + 1) : 0;

        Texture2D TextureFor(int variant) =>
            variant > 0 && D.drawnTextures != null && variant <= D.drawnTextures.Length
                ? D.drawnTextures[variant - 1]
                : D.cleanTexture;

        void ShowVariant(int variant)
        {
            var tex = TextureFor(variant);
            if (tex != null) SetTexture(tex);
        }

        void SetTexture(Texture tex)
        {
            if (picture == null) return;
            block ??= new MaterialPropertyBlock();
            picture.GetPropertyBlock(block);
            block.SetTexture(BaseMap, tex);
            block.SetTexture(MainTex, tex);
            picture.SetPropertyBlock(block);
        }

        void OnDisable()
        {
            if (session == null || !session.Active) return;
            session.End();
            overlay?.Hide();
            ShowCloseUp(false);
        }

        // ---------------------------------------------------------------- save

        [System.Serializable]
        struct DrawingSave
        {
            public int width, height;
            public string png;
        }

        public string SaveKey => $"drawing_{objectId}";

        public string ToJson()
        {
            if (!handDrawn || canvas == null) return "";
            return JsonUtility.ToJson(new DrawingSave
            {
                width = canvas.width, height = canvas.height,
                png = System.Convert.ToBase64String(canvas.EncodeToPNG()),
            });
        }

        public void LoadJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            var save = JsonUtility.FromJson<DrawingSave>(json);
            if (string.IsNullOrEmpty(save.png)) return;
            if (canvas == null) canvas = new Texture2D(save.width, save.height, TextureFormat.RGBA32, false) { name = $"Drawing_{objectId}", wrapMode = TextureWrapMode.Clamp };
            if (!canvas.LoadImage(System.Convert.FromBase64String(save.png))) return;
            canvas.wrapMode = TextureWrapMode.Clamp;
            handDrawn = true;
            SetTexture(canvas);
        }

        void OnDestroy()
        {
            SaveRegistry.Unregister(this);
            overlay?.Destroy();
            if (canvas != null) Destroy(canvas);
        }
    }
}
