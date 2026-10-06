using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Funseki.UI
{
    // Top-right, under the «Шум» megaphone: «Новая запись в журнале» with the prank title. Slides in from the right
    // edge, stays, slides out. Several pranks in a row queue up.
    public class HudJournalToast
    {
        const float SlideTime = 0.35f;

        readonly HudTheme theme;
        readonly HudSettings settings;
        readonly RectTransform root;
        readonly CanvasGroup group;
        readonly TextMeshProUGUI title, text;
        readonly Image iconImage;
        readonly Queue<string> queue = new();
        readonly float restX;
        float t = -1f;   // time since the current toast appeared, -1 when none

        public HudJournalToast(Transform parent, HudTheme theme, HudSettings settings, float top)
        {
            this.theme = theme;
            this.settings = settings;
            restX = -theme.margin.x;
            root = HudBuild.Place(HudBuild.Rect("JournalToast", parent), new Vector2(1f, 1f), new Vector2(restX, -top), theme.toastSize);
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            HudBuild.Stretch(HudBuild.Panel("Back", root, theme).rectTransform);
            var stripe = HudBuild.Image("Stripe", root, HudArt.RoundedRect, theme.journalAccentColor);
            stripe.type = Image.Type.Sliced;
            HudBuild.Place(stripe.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(10f, theme.toastSize.y));
            stripe.rectTransform.pivot = new Vector2(0f, 0.5f);
            stripe.rectTransform.anchoredPosition = Vector2.zero;

            float iconSize = theme.toastSize.y - 28f;
            iconImage = HudBuild.Image("Icon", root, theme.journalIcon != null ? theme.journalIcon : HudArt.Book,
                theme.journalIcon != null ? Color.white : theme.journalAccentColor);
            HudBuild.Place(iconImage.rectTransform, new Vector2(0f, 0.5f), new Vector2(24f, 0f), Vector2.one * iconSize);
            iconImage.rectTransform.pivot = new Vector2(0f, 0.5f);
            iconImage.rectTransform.anchoredPosition = new Vector2(24f, 0f);

            float left = 24f + iconSize + 16f;
            title = HudBuild.Text("Title", root, theme, theme.bodyFont, theme.toastTitleFontSize);
            title.color = theme.journalAccentColor;
            var tr = title.rectTransform;
            tr.anchorMin = new Vector2(0f, 0.5f);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.offsetMin = new Vector2(left, 0f);
            tr.offsetMax = new Vector2(-16f, -8f);

            text = HudBuild.Text("Text", root, theme, theme.bodyFont, theme.toastTextFontSize);
            var xr = text.rectTransform;
            xr.anchorMin = new Vector2(0f, 0f);
            xr.anchorMax = new Vector2(1f, 0.5f);
            xr.offsetMin = new Vector2(left, 8f);
            xr.offsetMax = new Vector2(-16f, 0f);
            root.gameObject.SetActive(false);
        }

        public void Show(string prankTitle) => queue.Enqueue(prankTitle ?? "");

        public void Tick(float dt)
        {
            if (t < 0f)
            {
                if (queue.Count == 0) return;
                title.text = settings.journalToastTitle;
                text.text = queue.Dequeue();
                t = 0f;
                root.gameObject.SetActive(true);
            }

            t += dt;
            float stay = Mathf.Max(settings.journalToastTime, SlideTime * 2f);
            float k = t < SlideTime ? HudBuild.Ease(t / SlideTime)
                : t > stay - SlideTime ? 1f - HudBuild.Ease((t - (stay - SlideTime)) / SlideTime) : 1f;
            group.alpha = k;
            root.anchoredPosition = new Vector2(restX + (1f - k) * (theme.toastSize.x * 0.6f), root.anchoredPosition.y);
            iconImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, t < 0.6f ? Mathf.Sin(t * 30f) * (0.6f - t) * 20f : 0f);

            if (t >= stay)
            {
                t = -1f;
                group.alpha = 0f;
                root.gameObject.SetActive(false);
            }
        }
    }
}
