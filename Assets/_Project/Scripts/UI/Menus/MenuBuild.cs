using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Funseki.UI
{
    // uGUI pieces of the in-game menus (pause, «Конец демо»), built in code with the HUD theme, like the HUD itself.
    public static class MenuBuild
    {
        public static Canvas Canvas(string name, Transform parent, HudTheme theme, int sortingOrder, out CanvasGroup group)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = theme.referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = theme.matchWidthOrHeight;
            go.AddComponent<GraphicRaycaster>();
            group = go.AddComponent<CanvasGroup>();
            return canvas;
        }

        /// <summary>A vertical stack centered on the screen; children keep their own sizes.</summary>
        public static RectTransform Column(string name, Transform parent, float spacing, float width)
        {
            var r = HudBuild.Rect(name, parent);
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(width, 0f);
            var layout = r.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fit = r.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return r;
        }

        public static void Height(Component c, float height)
        {
            if (!c.TryGetComponent<LayoutElement>(out var le)) le = c.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
        }

        public static TextMeshProUGUI Label(string name, Transform parent, HudTheme theme, TMP_FontAsset font, float size,
            string text, TextAlignmentOptions align = TextAlignmentOptions.Center, bool wrap = false)
        {
            var t = HudBuild.Text(name, parent, theme, font, size, align);
            t.text = text;
            if (wrap)
            {
                t.textWrappingMode = TextWrappingModes.Normal;
                t.overflowMode = TextOverflowModes.Overflow;
            }
            return t;
        }

        public static Button Button(string name, Transform parent, HudTheme theme, MenuSettings s, string text, Action onClick)
        {
            var img = HudBuild.Panel(name, parent, theme);
            img.color = Color.white;
            img.raycastTarget = true;
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.normalColor = theme.panelColor;
            colors.highlightedColor = theme.accentColor;
            colors.selectedColor = Color.Lerp(theme.panelColor, theme.accentColor, 0.7f);
            colors.pressedColor = theme.accentColor * 0.85f;
            colors.disabledColor = theme.panelColor * new Color(1f, 1f, 1f, 0.4f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(() => onClick());

            var label = HudBuild.Text("Label", img.transform, theme, theme.bodyFont, s.buttonFontSize, TextAlignmentOptions.Center);
            HudBuild.Stretch(label.rectTransform);
            label.text = text;
            Height(img, s.buttonSize.y);
            return button;
        }

        public static Slider Slider(string name, Transform parent, HudTheme theme, float min, float max, float value, Action<float> onChange)
        {
            var root = HudBuild.Rect(name, parent);
            root.sizeDelta = new Vector2(0f, 36f);
            var back = HudBuild.Image("Background", root, HudArt.RoundedRect, theme.noiseBarBackColor);
            back.type = Image.Type.Sliced;
            HudBuild.Stretch(back.rectTransform);
            back.rectTransform.offsetMin = new Vector2(0f, 10f);
            back.rectTransform.offsetMax = new Vector2(0f, -10f);

            var fillArea = HudBuild.Rect("Fill Area", root);
            HudBuild.Stretch(fillArea);
            fillArea.offsetMin = new Vector2(0f, 10f);
            fillArea.offsetMax = new Vector2(0f, -10f);
            var fill = HudBuild.Image("Fill", fillArea, HudArt.RoundedRect, theme.accentColor);
            fill.type = Image.Type.Sliced;
            fill.rectTransform.sizeDelta = Vector2.zero;

            var handleArea = HudBuild.Rect("Handle Area", root);
            HudBuild.Stretch(handleArea);
            handleArea.offsetMin = new Vector2(14f, 0f);
            handleArea.offsetMax = new Vector2(-14f, 0f);
            var handle = HudBuild.Image("Handle", handleArea, HudArt.Circle, theme.textColor);
            handle.raycastTarget = true;
            handle.rectTransform.sizeDelta = new Vector2(32f, 0f);

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(value);
            var colors = slider.colors;
            colors.highlightedColor = theme.accentColor;
            colors.selectedColor = theme.accentColor;
            slider.colors = colors;
            if (onChange != null) slider.onValueChanged.AddListener(v => onChange(v));
            back.raycastTarget = true;
            Height(root, 36f);
            return slider;
        }

        /// <summary>A UI needs an EventSystem; day scenes have none. Returns the one it made, or null.</summary>
        public static GameObject EnsureEventSystem(Transform parent)
        {
            if (EventSystem.current != null) return null;
            var go = new GameObject("EventSystem (Menus)", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.transform.SetParent(parent, false);
            return go;
        }

        public static void Select(Selectable s)
        {
            if (s != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(s.gameObject);
        }
    }
}
