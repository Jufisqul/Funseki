using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Funseki.Interaction
{
    // Paper booklet on screen: title, picture, text, page counter and ◄ / ► / «Закрыть» buttons (mouse).
    // Keys come through InspectSession. Built in code from BookletData; one per rack, made on first use.
    public class BookletView
    {
        readonly BookletData data;
        GameObject root;
        TextMeshProUGUI title, body, counter;
        RawImage picture;
        AspectRatioFitter pictureAspect;
        Button prevButton, nextButton;
        int page;

        public event Action CloseClicked;

        public BookletView(BookletData data) => this.data = data;

        public void Open()
        {
            if (root == null) Build();
            EnsureEventSystem();
            page = 0;
            Refresh();
            root.SetActive(true);
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        public void Destroy()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
        }

        public void Flip(int step)
        {
            int count = data.pages?.Length ?? 0;
            if (count == 0) return;
            page = Mathf.Clamp(page + step, 0, count - 1);
            Refresh();
        }

        void Refresh()
        {
            int count = data.pages?.Length ?? 0;
            var p = count > 0 ? data.pages[page] : null;
            title.text = p?.title ?? "";
            body.text = p?.text ?? "";
            picture.texture = p?.image;
            picture.gameObject.SetActive(p?.image != null);
            if (p?.image != null) pictureAspect.aspectRatio = (float)p.image.width / Mathf.Max(1, p.image.height);
            counter.text = string.Format(data.pageFormat, page + 1, Mathf.Max(1, count));
            prevButton.interactable = page > 0;
            nextButton.interactable = page < count - 1;
        }

        // The buttons need an EventSystem; the slice has none of its own yet.
        static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        void Build()
        {
            root = new GameObject("BookletView", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            var dim = Rect("Dim", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            var paper = Rect("Paper", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, data.panelSize);
            paper.gameObject.AddComponent<Image>().color = data.paperColor;
            var col = paper.gameObject.AddComponent<VerticalLayoutGroup>();
            col.padding = new RectOffset(48, 48, 36, 28);
            col.spacing = 16f;
            col.childControlWidth = col.childControlHeight = true;
            col.childForceExpandWidth = true;
            col.childForceExpandHeight = false;

            title = Text(paper, "Title", data.titleFontSize, FontStyles.Bold, TextAlignmentOptions.Center);

            var picHolder = Rect("PictureHolder", paper, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var picLayout = picHolder.gameObject.AddComponent<LayoutElement>();
            picLayout.flexibleHeight = 1f;
            picLayout.minHeight = 200f;
            var pic = Rect("Picture", picHolder, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            picture = pic.gameObject.AddComponent<RawImage>();
            picture.raycastTarget = false;
            pictureAspect = pic.gameObject.AddComponent<AspectRatioFitter>();
            pictureAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

            body = Text(paper, "Body", data.bodyFontSize, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            body.textWrappingMode = TextWrappingModes.Normal;

            var bar = Rect("Buttons", paper, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var row = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 24f;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            bar.gameObject.AddComponent<LayoutElement>().minHeight = 56f;

            prevButton = MakeButton(bar, data.previousLabel, () => Flip(-1));
            counter = Text(bar, "Counter", data.bodyFontSize, FontStyles.Normal, TextAlignmentOptions.Center);
            nextButton = MakeButton(bar, data.nextLabel, () => Flip(1));
            MakeButton(bar, data.closeLabel, () => CloseClicked?.Invoke());

            var hint = Text(paper, "Hint", data.hintFontSize, FontStyles.Italic, TextAlignmentOptions.Center);
            hint.text = data.closeHint;
            hint.alpha = 0.6f;

            root.SetActive(false);
        }

        Button MakeButton(RectTransform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var rect = Rect("Button", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var img = rect.gameObject.AddComponent<Image>();
            img.color = data.inkColor;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            button.onClick.AddListener(onClick);
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(22, 22, 8, 8);
            layout.childControlWidth = layout.childControlHeight = true;
            var t = Text(rect, "Label", data.bodyFontSize, FontStyles.Bold, TextAlignmentOptions.Center);
            t.text = label;
            t.color = data.paperColor;
            return button;
        }

        TextMeshProUGUI Text(RectTransform parent, string name, float size, FontStyles style, TextAlignmentOptions align)
        {
            var rect = Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var t = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (data.font != null) t.font = data.font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = data.inkColor;
            t.alignment = align;
            t.raycastTarget = false;
            return t;
        }

        static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = min;
            r.anchorMax = max;
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            return r;
        }
    }
}
