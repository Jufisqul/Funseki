using Funseki.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Funseki.UI
{
    // Shows GameEvents.OnHeroBark lines as a subtitle in the lower part of the screen.
    // A new line replaces the current one. Built in code from BarkSettings.
    public class BarkView : MonoBehaviour
    {
        [SerializeField] BarkSettings settings;

        TextMeshProUGUI label;
        float shownAt = -100f;

        void Awake() => Build();

        void OnEnable() => GameEvents.OnHeroBark += Show;
        void OnDisable() => GameEvents.OnHeroBark -= Show;

        void Show(GameObject hero, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            // Funseki.Dialogue.BarkService shows hero lines as a bubble over the head; this subtitle is the fallback without it.
            if (ServiceLocator.IsRegistered<IBarkService>()) return;
            label.text = text;
            shownAt = Time.unscaledTime;
        }

        void Update()
        {
            float t = Time.unscaledTime - shownAt;
            float fadeStart = settings.duration - settings.fadeTime;
            float alpha = t < fadeStart ? 1f : settings.fadeTime > 0f ? 1f - (t - fadeStart) / settings.fadeTime : 0f;
            label.alpha = Mathf.Clamp01(alpha);
        }

        void Build()
        {
            var canvasGo = new GameObject("Barks", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var go = new GameObject("Line", typeof(RectTransform));
            go.transform.SetParent(canvasGo.transform, false);
            label = go.AddComponent<TextMeshProUGUI>();
            if (settings.font != null) label.font = settings.font;
            label.fontSize = settings.fontSize;
            label.color = settings.textColor;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.alpha = 0f;
            label.outlineColor = settings.outlineColor;
            label.outlineWidth = settings.outlineWidth;

            var rt = label.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, settings.screenHeight);
            rt.sizeDelta = new Vector2(1400f, settings.fontSize * 2.5f);
        }
    }
}
