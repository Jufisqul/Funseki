using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Funseki.Story
{
    // Small builders for the story screens (the prologue file, the black fade). Built in code like the HUD.
    static class StoryUi
    {
        public static Canvas Canvas(string name, Transform parent, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            return canvas;
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(string name, Transform parent)
        {
            var rt = Rect(name, parent, Vector2.zero, Vector2.zero);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            return rt;
        }

        public static Image Image(RectTransform rt, Color color, Sprite sprite = null)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.sprite = sprite;
            img.preserveAspect = sprite != null;
            img.raycastTarget = false;
            return img;
        }

        public static TMP_Text Text(string name, Transform parent, Vector2 pos, Vector2 size, TMP_FontAsset font, float fontSize,
            Color color, TextAlignmentOptions align = TextAlignmentOptions.TopLeft, string text = "")
        {
            var rt = Rect(name, parent, pos, size);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            t.text = text;
            return t;
        }
    }

    // A full-screen black layer for story transitions (the prologue, the move to the dorm).
    public class StoryFader : MonoBehaviour
    {
        Image black;

        public float Alpha => black != null ? black.color.a : 0f;

        public static StoryFader Create(Transform parent, int sortingOrder = 400)
        {
            var canvas = StoryUi.Canvas("StoryFader", parent, sortingOrder);
            var fader = canvas.gameObject.AddComponent<StoryFader>();
            fader.black = StoryUi.Image(StoryUi.Stretch("Black", canvas.transform), new Color(0f, 0f, 0f, 0f));
            fader.black.raycastTarget = true;
            fader.black.enabled = false;
            return fader;
        }

        public void Set(float alpha)
        {
            black.color = new Color(0f, 0f, 0f, alpha);
            black.enabled = alpha > 0.001f;
        }

        // Unscaled time: a fade also runs while something pauses the game.
        public System.Collections.IEnumerator FadeTo(float alpha, float time)
        {
            float from = Alpha;
            if (time <= 0f) { Set(alpha); yield break; }
            for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
            {
                Set(Mathf.Lerp(from, alpha, t / time));
                yield return null;
            }
            Set(alpha);
        }
    }
}
