using System.Collections;
using System.Collections.Generic;
using Funseki.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Funseki.Pranks
{
    // «Поймали» (GDD 5.4). On GameEvents.OnCaught: state Caught, the witness says his line, then the punishment from
    // CaughtSceneData — the hero stands in the corridor with two buckets (bucketSpot), or runs laps around the
    // whistling witness — then black, the sign «Через 10 минут…», the «Шум» back to 0 (OnCaughtEnded),
    // timeToBell seconds left before the bell, and back to the break. No game over. One per day scene.
    public class CaughtDirector : MonoBehaviour
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        [Tooltip("Used when the catch names no scene")]
        [SerializeField] CaughtSceneData defaultScene;
        [Tooltip("Where the hero stands with the buckets; forward = where he faces")]
        [SerializeField] Transform bucketSpot;
        [SerializeField] TMP_FontAsset font;
        [SerializeField] Color fadeColor = Color.black;

        bool running;
        CanvasGroup overlay;
        TextMeshProUGUI sign;
        readonly List<GameObject> props = new();
        Material bucketMaterial;

        public bool IsRunning => running;

        void OnEnable() => GameEvents.OnCaught += OnCaught;
        void OnDisable() => GameEvents.OnCaught -= OnCaught;

        void OnCaught(GameObject hero, GameObject witness, ScriptableObject scene)
        {
            if (running) return;
            if (!ServiceLocator.TryGet<GameStateMachine>(out var fsm) || fsm.Current == GameState.Caught) return;
            if (hero == null) hero = HeroService.CurrentObject;
            if (hero == null) return;
            var data = scene as CaughtSceneData;
            if (data == null) data = defaultScene;
            if (data == null) { Debug.LogWarning("[Caught] No CaughtSceneData.", this); return; }
            StartCoroutine(Play(fsm, hero, witness, data));
        }

        IEnumerator Play(GameStateMachine fsm, GameObject hero, GameObject witness, CaughtSceneData data)
        {
            running = true;
            var stateBefore = fsm.Current;
            fsm.ChangeState(GameState.Caught);
            Debug.Log($"[Caught] {hero.name} caught by {(witness != null ? witness.name : "?")}: {data.name} ({data.kind}).", this);
            EnsureOverlay();

            Face(witness, hero.transform.position);
            Say(witness, PrankData.Pick(data.witnessLines));
            yield return new WaitForSeconds(data.beforePunishment);

            var cc = hero.GetComponent<CharacterController>();
            if (data.kind == CaughtSceneKind.Laps)
            {
                yield return Laps(hero, cc, witness, data);
                yield return Fade(1f, data.fadeTime);
            }
            else
            {
                yield return Fade(1f, data.fadeTime);
                if (bucketSpot != null) Warp(hero, cc, bucketSpot.position, bucketSpot.rotation);
                AddBuckets(hero, data);
                yield return new WaitForSeconds(0.4f); // the camera catches up behind the black
                yield return Fade(0f, data.fadeTime);
                Say(hero, PrankData.Pick(data.heroLines));
                yield return new WaitForSeconds(data.punishmentTime);
                yield return Fade(1f, data.fadeTime);
            }

            sign.text = data.signText;
            sign.gameObject.SetActive(true);
            yield return new WaitForSeconds(data.signTime);
            sign.gameObject.SetActive(false);
            ClearProps();

            if (ServiceLocator.TryGet<IDayCycle>(out var day)) day.SetTimeToBell(data.timeToBell);
            fsm.ChangeState(stateBefore == GameState.Caught || stateBefore == GameState.None ? GameState.Break : stateBefore);
            GameEvents.RaiseCaughtEnded(hero);
            yield return Fade(0f, data.fadeTime);
            running = false;
        }

        // The hero runs around the witness (or the spot in front of him) for punishmentTime.
        IEnumerator Laps(GameObject hero, CharacterController cc, GameObject witness, CaughtSceneData data)
        {
            var t = hero.transform;
            Vector3 center = witness != null ? witness.transform.position : t.position + t.forward * data.lapRadius;
            center.y = t.position.y;
            Vector3 from = t.position - center;
            from.y = 0f;
            float angle = from.sqrMagnitude > 0.01f ? Mathf.Atan2(from.z, from.x) : 0f;
            float radius = Mathf.Max(0.5f, data.lapRadius);
            float omega = data.lapSpeed / radius;

            bool ccWas = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false; // the animator then reads the speed from the movement

            Say(hero, PrankData.Pick(data.heroLines));
            float time = 0f, nextLine = 0f;
            while (time < data.punishmentTime)
            {
                float dt = Time.deltaTime;
                time += dt;
                angle -= omega * dt; // counter-clockwise seen from above
                Vector3 target = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                // The first moment: run onto the circle instead of jumping onto it.
                Vector3 next = Vector3.MoveTowards(t.position, target, data.lapSpeed * 1.5f * dt);
                Vector3 dir = next - t.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.000001f) t.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                t.position = next;

                Face(witness, t.position);
                if (time >= nextLine)
                {
                    nextLine = time + data.lapLineEvery;
                    Say(witness, PrankData.Pick(data.lapWitnessLines));
                }
                yield return null;
            }
            if (cc != null) cc.enabled = ccWas;
        }

        static void Warp(GameObject hero, CharacterController cc, Vector3 position, Quaternion rotation)
        {
            bool was = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;
            hero.transform.SetPositionAndRotation(position, rotation);
            if (cc != null) cc.enabled = was;
        }

        static void Face(GameObject who, Vector3 point)
        {
            if (who == null) return;
            Vector3 to = point - who.transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f) who.transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
        }

        static void Say(GameObject speaker, string text)
        {
            if (speaker == null || string.IsNullOrEmpty(text)) return;
            if (ServiceLocator.TryGet<IBarkService>(out var barks)) barks.Say(speaker, text);
            else GameEvents.RaiseHeroBark(speaker, text);
        }

        // ---------------------------------------------------------------- buckets

        void AddBuckets(GameObject hero, CaughtSceneData data)
        {
            var animator = hero.GetComponentInChildren<Animator>();
            Transform left = null, right = null;
            if (animator != null && animator.isHuman)
            {
                left = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                right = animator.GetBoneTransform(HumanBodyBones.RightHand);
            }
            var t = hero.transform;
            props.Add(Bucket(left, t, t.position - t.right * 0.35f + Vector3.up * 0.55f, data));
            props.Add(Bucket(right, t, t.position + t.right * 0.35f + Vector3.up * 0.55f, data));
        }

        GameObject Bucket(Transform hand, Transform hero, Vector3 fallback, CaughtSceneData data)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Bucket";
            Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            if (bucketMaterial == null)
            {
                bucketMaterial = new Material(r.sharedMaterial);
                bucketMaterial.SetColor(BaseColor, data.bucketColor);
                bucketMaterial.SetColor(ColorId, data.bucketColor);
            }
            r.sharedMaterial = bucketMaterial;
            go.transform.SetParent(hand != null ? hand : hero, true);
            go.transform.position = hand != null ? hand.position + Vector3.down * (data.bucketSize.y * 0.5f + 0.12f) : fallback;
            go.transform.rotation = Quaternion.identity;
            Vector3 parentScale = go.transform.parent.lossyScale;
            go.transform.localScale = new Vector3(
                data.bucketSize.x / Mathf.Max(0.0001f, parentScale.x),
                data.bucketSize.y * 0.5f / Mathf.Max(0.0001f, parentScale.y),
                data.bucketSize.z / Mathf.Max(0.0001f, parentScale.z));
            return go;
        }

        void ClearProps()
        {
            foreach (var p in props) if (p != null) Destroy(p);
            props.Clear();
        }

        // ---------------------------------------------------------------- overlay

        IEnumerator Fade(float to, float time)
        {
            float from = overlay.alpha;
            overlay.gameObject.SetActive(true);
            for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
            {
                overlay.alpha = Mathf.Lerp(from, to, t / time);
                yield return null;
            }
            overlay.alpha = to;
            if (to <= 0f) overlay.gameObject.SetActive(false);
        }

        void EnsureOverlay()
        {
            if (overlay != null) return;
            var canvasGo = new GameObject("CaughtOverlay", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            overlay = canvasGo.GetComponent<CanvasGroup>();
            overlay.alpha = 0f;
            overlay.blocksRaycasts = false;
            overlay.interactable = false;

            var black = new GameObject("Black", typeof(RectTransform), typeof(Image));
            black.transform.SetParent(canvasGo.transform, false);
            Stretch(black.GetComponent<RectTransform>());
            var img = black.GetComponent<Image>();
            img.color = fadeColor;
            img.raycastTarget = false;

            var textGo = new GameObject("Sign", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(canvasGo.transform, false);
            Stretch(textGo.GetComponent<RectTransform>());
            sign = textGo.GetComponent<TextMeshProUGUI>();
            if (font != null) sign.font = font;
            sign.fontSize = 64;
            sign.alignment = TextAlignmentOptions.Center;
            sign.color = Color.white;
            sign.raycastTarget = false;
            textGo.SetActive(false);
            canvasGo.SetActive(false);
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        void OnDestroy()
        {
            if (bucketMaterial != null) Destroy(bucketMaterial);
        }
    }
}
