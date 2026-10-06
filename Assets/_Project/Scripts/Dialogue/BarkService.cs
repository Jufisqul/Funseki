using System.Collections.Generic;
using Funseki.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Funseki.Dialogue
{
    // Short speech bubbles over characters' heads for 2-3 s; the game goes on (GDD 5.3).
    // Registered as IBarkService: ServiceLocator.Get<IBarkService>().Say(npc, "..."). Also shows the hero's own lines
    // (GameEvents.OnHeroBark, e.g. "Не сработает") over the hero, replacing Funseki.UI.BarkView's subtitle.
    // One bubble per speaker: a new line replaces the old one.
    [DefaultExecutionOrder(-900)]
    public class BarkService : MonoBehaviour, IBarkService
    {
        [SerializeField] DialogueSettings settings;

        class Bubble
        {
            public GameObject speaker;
            public float headHeight;
            public RectTransform rect;
            public CanvasGroup group;
            public TextMeshProUGUI label;
            public LayoutElement textLayout;
            public float shownAt, duration;
        }

        readonly List<Bubble> active = new();
        readonly Stack<Bubble> pool = new();
        RectTransform canvasRect;
        MumblePlayer mumble;
        bool owner;

        void Awake()
        {
            if (ServiceLocator.IsRegistered<IBarkService>()) { Destroy(gameObject); return; }
            owner = true;
            ServiceLocator.Register<IBarkService>(this);
            BuildCanvas();
            mumble = new MumblePlayer(gameObject, settings);
        }

        void OnEnable() => GameEvents.OnHeroBark += Say;
        void OnDisable() => GameEvents.OnHeroBark -= Say;

        void OnDestroy()
        {
            if (owner && ServiceLocator.TryGet<IBarkService>(out var s) && ReferenceEquals(s, this))
                ServiceLocator.Unregister<IBarkService>();
        }

        public void Say(GameObject speaker, string text)
        {
            if (!owner || speaker == null || string.IsNullOrEmpty(text)) return;

            var b = active.Find(x => x.speaker == speaker);
            if (b == null)
            {
                b = pool.Count > 0 ? pool.Pop() : CreateBubble();
                b.speaker = speaker;
                active.Add(b);
            }
            var tag = speaker.GetComponentInParent<SpeakerTag>();
            b.headHeight = tag != null && tag.headHeight > 0f ? tag.headHeight : HeadHeight(speaker);
            b.label.text = text;
            // Short lines stay on one line, long ones wrap at barkMaxWidth.
            float natural = b.label.GetPreferredValues(text, float.PositiveInfinity, float.PositiveInfinity).x;
            b.textLayout.preferredWidth = Mathf.Min(natural, settings.barkMaxWidth);
            b.shownAt = Time.unscaledTime;
            b.duration = Mathf.Clamp(settings.barkMinDuration + text.Length * settings.barkSecondsPerChar,
                settings.barkMinDuration, settings.barkMaxDuration);
            b.group.alpha = 1f;
            b.rect.gameObject.SetActive(true);
            Place(b);

            mumble.Play(tag != null ? tag.speaker : null);
        }

        void LateUpdate()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var b = active[i];
                float t = Time.unscaledTime - b.shownAt;
                if (b.speaker == null || t >= b.duration)
                {
                    b.rect.gameObject.SetActive(false);
                    b.speaker = null;
                    active.RemoveAt(i);
                    pool.Push(b);
                    continue;
                }
                float fadeStart = b.duration - settings.barkFadeTime;
                b.group.alpha = t < fadeStart ? 1f : Mathf.Clamp01((b.duration - t) / Mathf.Max(0.01f, settings.barkFadeTime));
                Place(b);
            }
        }

        // Follows the head on screen; hidden when behind the camera or too far.
        void Place(Bubble b)
        {
            var cam = Camera.main;
            if (cam == null) { b.rect.gameObject.SetActive(false); return; }
            Vector3 world = b.speaker.transform.position + Vector3.up * (b.headHeight + settings.barkHeadOffset);
            Vector3 screen = cam.WorldToScreenPoint(world);
            bool visible = screen.z > 0f && screen.z < settings.barkMaxDistance;
            if (b.rect.gameObject.activeSelf != visible) b.rect.gameObject.SetActive(visible);
            if (!visible) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var local);
            b.rect.anchoredPosition = local;
        }

        static float HeadHeight(GameObject speaker)
        {
            var cc = speaker.GetComponent<CharacterController>();
            if (cc != null) return cc.center.y + cc.height * 0.5f;
            var renderers = speaker.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return 1.8f;
            float top = float.MinValue;
            foreach (var r in renderers) top = Mathf.Max(top, r.bounds.max.y);
            return top - speaker.transform.position.y;
        }

        void BuildCanvas()
        {
            var go = new GameObject("BarkBubbles", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasRect = (RectTransform)go.transform;
        }

        Bubble CreateBubble()
        {
            var go = new GameObject("Bubble", typeof(RectTransform));
            go.transform.SetParent(canvasRect, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0f);

            var bg = go.AddComponent<Image>();
            bg.sprite = settings.panelSprite;
            bg.type = settings.panelSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            bg.color = settings.barkBubbleColor;
            bg.raycastTarget = false;
            var group = go.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 10, 12);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Tail under the bubble pointing at the speaker.
            var tailGo = new GameObject("Tail", typeof(RectTransform));
            tailGo.transform.SetParent(go.transform, false);
            tailGo.AddComponent<LayoutElement>().ignoreLayout = true;
            var tail = tailGo.AddComponent<Image>();
            tail.color = settings.barkBubbleColor;
            tail.raycastTarget = false;
            var trt = tail.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0f);
            trt.pivot = new Vector2(0.5f, 0.5f);
            trt.anchoredPosition = Vector2.zero;
            trt.sizeDelta = new Vector2(18f, 18f);
            trt.localRotation = Quaternion.Euler(0f, 0f, 45f);
            tailGo.transform.SetAsFirstSibling();

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var label = textGo.AddComponent<TextMeshProUGUI>();
            if (settings.font != null) label.font = settings.font;
            label.fontSize = settings.barkFontSize;
            label.color = settings.barkTextColor;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            var textLayout = textGo.AddComponent<LayoutElement>();

            go.SetActive(false);
            return new Bubble { rect = rect, group = group, label = label, textLayout = textLayout };
        }
    }
}
