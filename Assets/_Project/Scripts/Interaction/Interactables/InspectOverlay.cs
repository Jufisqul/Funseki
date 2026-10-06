using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Funseki.Interaction
{
    // Screen layer of a 3D close-up (the locker): the hero's line at the bottom, the close hint under it and a
    // caption that follows the hovered detail. Built in code from InspectData; one per object, made on first use.
    public class InspectOverlay
    {
        readonly InspectData data;
        GameObject root;
        RectTransform canvasRect, captionRect;
        TextMeshProUGUI subtitle, hint, caption;

        public InspectOverlay(InspectData data) => this.data = data;

        public void Show(string line)
        {
            if (root == null) Build();
            subtitle.text = line ?? "";
            subtitle.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(line));
            hint.text = data.closeHint;
            SetCaption(null, Vector2.zero);
            root.SetActive(true);
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        public void Destroy()
        {
            if (root != null) Object.Destroy(root);
        }

        /// <summary>Caption above a screen point; null hides it.</summary>
        public void SetCaption(string text, Vector2 screenPoint)
        {
            if (root == null) return;
            bool show = !string.IsNullOrEmpty(text);
            captionRect.gameObject.SetActive(show);
            if (!show) return;
            caption.text = text;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out var local);
            captionRect.anchoredPosition = local + new Vector2(0f, 30f);
        }

        void Build()
        {
            root = new GameObject("InspectOverlay", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 70;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasRect = (RectTransform)root.transform;

            subtitle = Label(Panel("Subtitle", new Vector2(0.5f, 0f), new Vector2(0f, 110f)), 34f);
            hint = Label(Panel("Hint", new Vector2(0.5f, 0f), new Vector2(0f, 40f)), data.hintFontSize);
            var capPanel = Panel("Caption", new Vector2(0.5f, 0.5f), Vector2.zero);
            captionRect = (RectTransform)capPanel.transform;
            captionRect.pivot = new Vector2(0.5f, 0f);
            caption = Label(capPanel, 28f);
        }

        GameObject Panel(string name, Vector2 anchor, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(root.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            var bg = go.AddComponent<Image>();
            bg.color = data.panelColor;
            bg.raycastTarget = false;
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 8, 10);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return go;
        }

        TextMeshProUGUI Label(GameObject panel, float size)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(panel.transform, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (data.font != null) t.font = data.font;
            t.fontSize = size;
            t.color = data.textColor;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }
    }
}
