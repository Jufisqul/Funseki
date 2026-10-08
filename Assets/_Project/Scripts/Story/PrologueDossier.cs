using System.Collections;
using Funseki.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Funseki.Story
{
    // Day 1 prologue (CLAUDE.md «Полный День 1», step 1): a top-down look at the desk with Тамура Рюта's file.
    // Runs during the "prologue" phase of the day in Slice_Day1, as a full-screen overlay over the loading school,
    // so the main menu, the save and Build Settings work as before. Prologue map: Next (LMB / A) turns the page,
    // on the last page it stamps «ПЕРЕВЕДЁН — ФУНСЭКИ» (drop, sound, shake); holding Skip (Space / Start) skips.
    // Then the screen goes black, PrologueData.doneFlag ends the phase and the arrival starts under the black.
    // Pages: 1) photos, name, birth date; 2) description; 3) transfer reason + stamp.
    public class PrologueDossier : MonoBehaviour
    {
        public const string MapName = "Prologue";

        [SerializeField] PrologueData data;
        [SerializeField] InputActionAsset actions;

        InputActionMap map;
        InputAction next, skip;
        Canvas canvas;
        RectTransform shakeRoot, stamp;
        RectTransform[] pages;
        TMP_Text hint;
        Image skipFill;
        StoryFader fader;
        AudioSource audioSource;
        int page;
        float skipHeld;
        bool running, busy, finished;

        void Awake()
        {
            map = actions.FindActionMap(MapName, true);
            next = map.FindAction("Next", true);
            skip = map.FindAction("Skip", true);
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            Build();
            fader = StoryFader.Create(transform, 410);
            // Cover the screen from the first frame while the prologue is still ahead (no flash of the school).
            bool pending = ServiceLocator.TryGet<WorldFlags>(out var flags) && !flags.GetFlag(data.doneFlag);
            canvas.gameObject.SetActive(pending);
        }

        void OnEnable() => GameEvents.OnPhaseStarted += OnPhaseStarted;

        void OnDisable()
        {
            GameEvents.OnPhaseStarted -= OnPhaseStarted;
            map?.Disable();
        }

        void Start()
        {
            // A day that starts elsewhere (Continue, Quick Play) never shows the file.
            if (ServiceLocator.TryGet<IDayCycle>(out var day) && day.CurrentPhase != null && day.CurrentPhase.id == data.phaseId) Begin();
            else if (!running) canvas.gameObject.SetActive(false);
        }

        void OnPhaseStarted(DayPhase phase)
        {
            if (phase != null && phase.id == data.phaseId) Begin();
        }

        void Begin()
        {
            if (running) return;
            running = true;
            finished = false;
            page = 0;
            skipHeld = 0f;
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i].gameObject.SetActive(true);
                pages[i].localRotation = Quaternion.identity;
                pages[i].SetSiblingIndex(pages.Length - 1 - i);   // page 0 on top
            }
            stamp.gameObject.SetActive(false);
            canvas.gameObject.SetActive(true);
            fader.Set(0f);
            map.Enable();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            UpdateHint();
            Debug.Log("[Prologue] The file is on the desk.");
        }

        void Update()
        {
            if (!running || finished) return;

            // Hold to skip.
            skipHeld = skip.IsPressed() ? skipHeld + Time.unscaledDeltaTime : 0f;
            float k = Mathf.Clamp01(skipHeld / Mathf.Max(0.01f, data.skipHoldTime));
            skipFill.fillAmount = k;
            if (k >= 1f) { Debug.Log("[Prologue] Skipped."); StartCoroutine(Finish()); return; }

            if (busy || !next.WasPressedThisFrame()) return;
            if (page < pages.Length - 1) StartCoroutine(Flip());
            else StartCoroutine(Stamp());
        }

        /// <summary>Ends the prologue at once (the editor smoke test).</summary>
        public void SkipNow()
        {
            if (running && !finished) StartCoroutine(Finish());
        }

        IEnumerator Flip()
        {
            busy = true;
            var current = pages[page];
            for (float t = 0f; t < data.flipTime; t += Time.unscaledDeltaTime)
            {
                // The page turns on its left edge until it is edge-on, then the next one lies open.
                current.localRotation = Quaternion.Euler(0f, Mathf.Lerp(0f, 90f, t / data.flipTime), 0f);
                yield return null;
            }
            current.gameObject.SetActive(false);
            page++;
            UpdateHint();
            busy = false;
        }

        IEnumerator Stamp()
        {
            busy = true;
            hint.text = "";
            stamp.gameObject.SetActive(true);
            var rest = Quaternion.Euler(0f, 0f, data.stampAngle);
            for (float t = 0f; t < data.stampDropTime; t += Time.unscaledDeltaTime)
            {
                float s = Mathf.Lerp(2.4f, 1f, t / data.stampDropTime);
                stamp.localScale = Vector3.one * s;
                stamp.localRotation = rest;
                yield return null;
            }
            stamp.localScale = Vector3.one;
            if (data.stampSound != null) audioSource.PlayOneShot(data.stampSound, data.stampVolume);
            else audioSource.PlayOneShot(Thud(), data.stampVolume);
            Debug.Log("[Prologue] Stamped.");

            for (float t = 0f; t < data.shakeTime; t += Time.unscaledDeltaTime)
            {
                float fall = 1f - t / data.shakeTime;
                shakeRoot.anchoredPosition = Random.insideUnitCircle * data.shakeStrength * fall;
                yield return null;
            }
            shakeRoot.anchoredPosition = Vector2.zero;
            yield return new WaitForSecondsRealtime(data.holdAfterStamp);
            yield return Finish();
        }

        IEnumerator Finish()
        {
            if (finished) yield break;
            finished = true;
            busy = true;
            map.Disable();
            yield return fader.FadeTo(1f, data.fadeOutTime);
            canvas.gameObject.SetActive(false);
            // The phase ends on this flag; the arrival starts and places the hero while the screen is black.
            if (ServiceLocator.TryGet<WorldFlags>(out var flags)) flags.SetFlag(data.doneFlag);
            else Debug.LogWarning("[Prologue] No WorldFlags: play from Bootstrap.");
            yield return null;
            yield return new WaitForSecondsRealtime(data.blackTime);
            yield return fader.FadeTo(0f, data.fadeInTime);
            running = false;
            busy = false;
        }

        void UpdateHint()
        {
            hint.text = page < pages.Length - 1 ? data.nextHint : data.stampHint;
            skipFill.fillAmount = 0f;
        }

        // ---------------------------------------------------------------- build

        void Build()
        {
            canvas = StoryUi.Canvas("PrologueScreen", transform, 300);
            var desk = StoryUi.Stretch("Desk", canvas.transform);
            StoryUi.Image(desk, data.deskColor).raycastTarget = true;
            shakeRoot = StoryUi.Stretch("Shake", canvas.transform);

            // A few things on the desk so it reads as a desk from above.
            var pen = StoryUi.Rect("Pen", shakeRoot, new Vector2(720f, -260f), new Vector2(260f, 16f));
            StoryUi.Image(pen, new Color(0.1f, 0.12f, 0.25f, 1f));
            pen.localRotation = Quaternion.Euler(0f, 0f, 28f);
            var mug = StoryUi.Rect("Mug", shakeRoot, new Vector2(-760f, 300f), new Vector2(150f, 150f));
            StoryUi.Image(mug, new Color(0.85f, 0.85f, 0.82f, 1f));
            var tea = StoryUi.Rect("Tea", mug, Vector2.zero, new Vector2(110f, 110f));
            StoryUi.Image(tea, new Color(0.45f, 0.28f, 0.12f, 1f));

            var folder = StoryUi.Rect("Folder", shakeRoot, new Vector2(0f, -10f), new Vector2(1180f, 820f));
            StoryUi.Image(folder, data.folderColor);
            folder.localRotation = Quaternion.Euler(0f, 0f, 1.5f);
            var tab = StoryUi.Rect("Tab", folder, new Vector2(-380f, 440f), new Vector2(380f, 70f));
            StoryUi.Image(tab, data.folderColor);
            StoryUi.Text("Label", tab, Vector2.zero, new Vector2(360f, 60f), data.titleFont, 30f, data.inkColor,
                TextAlignmentOptions.Center, data.folderLabel);

            pages = new RectTransform[3];
            pages[0] = Page(folder, "Page_1");
            Photo(pages[0], new Vector2(-330f, 150f), data.photoFront, data.photoFrontCaption);
            Photo(pages[0], new Vector2(-40f, 150f), data.photoProfile, data.photoProfileCaption);
            Field(pages[0], new Vector2(0f, -150f), data.fullNameLabel, data.fullName);
            Field(pages[0], new Vector2(0f, -260f), data.birthDateLabel, data.birthDate);

            pages[1] = Page(folder, "Page_2");
            Field(pages[1], new Vector2(0f, 250f), data.descriptionLabel, data.description, 420f);

            pages[2] = Page(folder, "Page_3");
            Field(pages[2], new Vector2(0f, 250f), data.reasonLabel, data.transferReason, 300f);

            stamp = StoryUi.Rect("Stamp", pages[2], new Vector2(120f, -230f), new Vector2(640f, 130f));
            var frame = StoryUi.Image(stamp, data.stampColor);
            var inner = StoryUi.Rect("Inner", stamp, Vector2.zero, new Vector2(620f, 110f));
            StoryUi.Image(inner, data.pageColor);
            StoryUi.Text("Text", stamp, Vector2.zero, new Vector2(610f, 110f), data.titleFont, 52f, data.stampColor,
                TextAlignmentOptions.Center, data.stampText);
            frame.raycastTarget = false;

            hint = StoryUi.Text("Hint", canvas.transform, new Vector2(0f, -500f), new Vector2(1200f, 50f), data.bodyFont, 30f,
                data.hintColor, TextAlignmentOptions.Center);
            var skipBar = StoryUi.Rect("Skip", canvas.transform, new Vector2(720f, -500f), new Vector2(420f, 50f));
            StoryUi.Text("Text", skipBar, Vector2.zero, new Vector2(420f, 40f), data.bodyFont, 22f, data.hintColor,
                TextAlignmentOptions.Center, data.skipHint);
            var fill = StoryUi.Rect("Fill", skipBar, new Vector2(0f, -26f), new Vector2(380f, 6f));
            skipFill = StoryUi.Image(fill, data.hintColor);
            skipFill.sprite = WhiteSprite();
            skipFill.type = Image.Type.Filled;
            skipFill.fillMethod = Image.FillMethod.Horizontal;
            skipFill.fillAmount = 0f;
        }

        RectTransform Page(RectTransform folder, string name)
        {
            // Pivot on the left edge: the page turns like a page of a book.
            var p = StoryUi.Rect(name, folder, new Vector2(-520f, 0f), new Vector2(1040f, 740f), new Vector2(0f, 0.5f));
            StoryUi.Image(p, data.pageColor);
            // Children are anchored to the page centre, so their positions do not depend on the pivot.
            return p;
        }

        void Photo(RectTransform page, Vector2 pos, Sprite sprite, string caption)
        {
            var frame = StoryUi.Rect("Photo", page, pos, new Vector2(240f, 300f));
            StoryUi.Image(frame, Color.white);
            var img = StoryUi.Rect("Image", frame, new Vector2(0f, 10f), new Vector2(220f, 250f));
            StoryUi.Image(img, sprite != null ? Color.white : data.photoPlaceholderColor, sprite);
            StoryUi.Text("Caption", frame, new Vector2(0f, -135f), new Vector2(220f, 30f), data.bodyFont, 20f, data.inkColor,
                TextAlignmentOptions.Center, caption);
            if (sprite == null)
                StoryUi.Text("Placeholder", img, Vector2.zero, new Vector2(200f, 60f), data.bodyFont, 22f, data.inkColor,
                    TextAlignmentOptions.Center, "ФОТО");
        }

        void Field(RectTransform page, Vector2 pos, string label, string value, float height = 60f)
        {
            StoryUi.Text("Label", page, pos + new Vector2(0f, 30f), new Vector2(900f, 36f), data.titleFont, 22f, data.inkColor,
                TextAlignmentOptions.TopLeft, label.ToUpperInvariant());
            StoryUi.Text("Value", page, pos - new Vector2(0f, height * 0.5f), new Vector2(900f, height), data.bodyFont, 30f,
                data.inkColor, TextAlignmentOptions.TopLeft, value);
        }

        static Sprite white;
        static Sprite WhiteSprite()
        {
            if (white != null) return white;
            var tex = Texture2D.whiteTexture;
            return white = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }

        // A short low thud until a real stamp sound is set in the data.
        static AudioClip thud;
        static AudioClip Thud()
        {
            if (thud != null) return thud;
            const int rate = 44100;
            var samples = new float[(int)(rate * 0.18f)];
            var rnd = new System.Random(7);
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Exp(-t * 28f);
                samples[i] = env * (0.7f * Mathf.Sin(2f * Mathf.PI * 70f * t) + 0.3f * ((float)rnd.NextDouble() * 2f - 1f));
            }
            thud = AudioClip.Create("StampThud", samples.Length, 1, rate, false);
            thud.SetData(samples, 0);
            return thud;
        }
    }
}
