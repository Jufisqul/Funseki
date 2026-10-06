using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Funseki.UI
{
    // Small helpers the HUD blocks use to build their uGUI objects in code with the theme applied.
    public static class HudBuild
    {
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Anchor and pivot at the same point (0..1), so the position is measured from that corner or edge.</summary>
        public static RectTransform Place(RectTransform r, Vector2 anchor, Vector2 position, Vector2 size)
        {
            r.anchorMin = r.anchorMax = r.pivot = anchor;
            r.anchoredPosition = position;
            r.sizeDelta = size;
            return r;
        }

        public static RectTransform Stretch(RectTransform r, float inset = 0f)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(inset, inset);
            r.offsetMax = new Vector2(-inset, -inset);
            return r;
        }

        public static Image Image(string name, Transform parent, Sprite sprite, Color color)
        {
            var img = Rect(name, parent).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            img.preserveAspect = sprite != null && sprite.border == Vector4.zero;
            if (sprite != null && sprite.border != Vector4.zero) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        public static Image Panel(string name, Transform parent, HudTheme theme)
        {
            var img = Image(name, parent, theme.panelSprite != null ? theme.panelSprite : HudArt.RoundedRect, theme.panelColor);
            img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, HudTheme theme, TMP_FontAsset font, float size,
            TextAlignmentOptions align = TextAlignmentOptions.Left)
        {
            var t = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.color = theme.textColor;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            // Outline through the material instance; keeps white text readable on a bright yard.
            t.outlineColor = theme.textOutlineColor;
            t.outlineWidth = theme.textOutlineWidth;
            return t;
        }

        /// <summary>0..1 → smooth 0..1.</summary>
        public static float Ease(float t) => t <= 0f ? 0f : t >= 1f ? 1f : t * t * (3f - 2f * t);
    }
}
