using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Funseki.Lessons.Fizra
{
    // The PE lesson UI, built in code: the whistle pictogram over the teacher's head with its translation and the
    // reaction timer, a short «Есть! / Мимо!» after each answer, and the score in the corner.
    public class FizraView
    {
        readonly GameObject root;
        readonly RectTransform canvasRect, bubble;
        readonly Image iconImage, timerFill;
        readonly TextMeshProUGUI glyph, caption, feedback, score;
        Transform anchor;
        float feedbackUntil;

        public FizraView(Transform parent, TMP_FontAsset font)
        {
            root = new GameObject("FizraView");
            root.transform.SetParent(parent, false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 35;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasRect = (RectTransform)root.transform;

            // Bubble over the teacher.
            bubble = Rect("Bubble", root.transform, new Vector2(260f, 230f));
            var bg = bubble.gameObject.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.92f);
            iconImage = Rect("Icon", bubble, new Vector2(110f, 110f)).gameObject.AddComponent<Image>();
            iconImage.rectTransform.anchoredPosition = new Vector2(0f, 45f);
            iconImage.preserveAspect = true;
            glyph = Text("Glyph", bubble, font, 72f, new Vector2(240f, 110f), new Color(0.12f, 0.12f, 0.15f));
            glyph.rectTransform.anchoredPosition = new Vector2(0f, 45f);
            caption = Text("Caption", bubble, font, 36f, new Vector2(250f, 60f), new Color(0.75f, 0.15f, 0.1f));
            caption.rectTransform.anchoredPosition = new Vector2(0f, -40f);

            var timerBg = Rect("Timer", bubble, new Vector2(220f, 14f));
            timerBg.anchoredPosition = new Vector2(0f, -95f);
            timerBg.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.2f);
            timerFill = Rect("Fill", timerBg, new Vector2(220f, 14f)).gameObject.AddComponent<Image>();
            timerFill.color = new Color(0.95f, 0.6f, 0.1f);
            timerFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            timerFill.rectTransform.anchoredPosition = new Vector2(-110f, 0f);

            feedback = Text("Feedback", root.transform, font, 64f, new Vector2(600f, 100f), Color.white);
            feedback.rectTransform.anchorMin = feedback.rectTransform.anchorMax = new Vector2(0.5f, 0.3f);
            feedback.outlineWidth = 0.25f;
            feedback.outlineColor = new Color32(20, 20, 20, 255);

            score = Text("Score", root.transform, font, 44f, new Vector2(300f, 70f), Color.white);
            score.rectTransform.anchorMin = score.rectTransform.anchorMax = new Vector2(1f, 1f);
            score.rectTransform.pivot = new Vector2(1f, 1f);
            score.rectTransform.anchoredPosition = new Vector2(-40f, -40f);
            score.alignment = TextAlignmentOptions.Right;
            score.outlineWidth = 0.25f;
            score.outlineColor = new Color32(20, 20, 20, 255);

            bubble.gameObject.SetActive(false);
            feedback.gameObject.SetActive(false);
            score.gameObject.SetActive(false);
        }

        public void SetAnchor(Transform teacherHead) => anchor = teacherHead;

        public void ShowCommand(Sprite icon, string glyphText, string captionText)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
            glyph.gameObject.SetActive(icon == null);
            glyph.text = glyphText;
            caption.text = captionText ?? "";
            caption.gameObject.SetActive(!string.IsNullOrEmpty(captionText));
            timerFill.rectTransform.localScale = Vector3.one;
            bubble.gameObject.SetActive(true);
            Place();
        }

        public void SetTimer(float left01) => timerFill.rectTransform.localScale = new Vector3(Mathf.Clamp01(left01), 1f, 1f);

        public void HideCommand() => bubble.gameObject.SetActive(false);

        public void ShowFeedback(string text, Color color, float seconds = 0.9f)
        {
            feedback.text = text;
            feedback.color = color;
            feedback.gameObject.SetActive(true);
            feedbackUntil = Time.unscaledTime + seconds;
        }

        public void SetScore(string text)
        {
            score.text = text;
            score.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        public void Tick()
        {
            if (feedback.gameObject.activeSelf && Time.unscaledTime > feedbackUntil) feedback.gameObject.SetActive(false);
            if (bubble.gameObject.activeSelf) Place();
        }

        public void Destroy()
        {
            if (root != null) Object.Destroy(root);
        }

        void Place()
        {
            var cam = Camera.main;
            if (cam == null || anchor == null) { bubble.anchoredPosition = new Vector2(0f, 300f); return; }
            Vector3 screen = cam.WorldToScreenPoint(anchor.position);
            if (screen.z <= 0f) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var local);
            bubble.anchoredPosition = local + new Vector2(0f, 140f);
        }

        static RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.sizeDelta = size;
            return r;
        }

        static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, float size, Vector2 box, Color color)
        {
            var t = Rect(name, parent, box).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center;
            t.color = color;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }
    }
}
