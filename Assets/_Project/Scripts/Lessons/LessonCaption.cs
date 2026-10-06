using TMPro;
using UnityEngine;

namespace Funseki.Lessons
{
    // Big caption over the lesson: the title card («Физра»), the outcome («Блестяще!»). Built in code on its own canvas.
    public class LessonCaption
    {
        readonly GameObject root;
        readonly TextMeshProUGUI label;
        readonly CanvasGroup group;
        float hideAt = -1f;

        public LessonCaption(Transform parent, TMP_FontAsset font)
        {
            root = new GameObject("LessonCaption");
            root.transform.SetParent(parent, false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = root.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            group = root.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var go = new GameObject("Text");
            go.transform.SetParent(root.transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.1f, 0.62f);
            rect.anchorMax = new Vector2(0.9f, 0.82f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            label = go.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 84f;
            label.enableAutoSizing = true;
            label.fontSizeMin = 36f;
            label.fontSizeMax = 96f;
            label.color = Color.white;
            label.outlineWidth = 0.2f;
            label.outlineColor = new Color32(30, 20, 10, 255);
            root.SetActive(false);
        }

        public void Show(string text, float seconds)
        {
            if (string.IsNullOrEmpty(text)) return;
            label.text = text;
            group.alpha = 1f;
            root.SetActive(true);
            hideAt = Time.unscaledTime + seconds;
        }

        public void Hide()
        {
            root.SetActive(false);
            hideAt = -1f;
        }

        public void Tick()
        {
            if (hideAt < 0f) return;
            float left = hideAt - Time.unscaledTime;
            if (left <= 0f) { Hide(); return; }
            group.alpha = Mathf.Clamp01(left / 0.4f);
        }

        public void Destroy()
        {
            if (root != null) Object.Destroy(root);
        }
    }
}
