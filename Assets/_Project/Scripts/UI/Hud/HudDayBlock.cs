using Funseki.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Funseki.UI
{
    // Top-left corner: the time-of-day icon over «День N», and under them the goal of the break in one line.
    // The goal changes softly: the old line fades out, the new one slides in and flashes in the accent color.
    public class HudDayBlock
    {
        const float DateHeight = 48f;
        const float GoalHeight = 54f;
        const float GoalMaxWidth = 720f;

        readonly HudTheme theme;
        readonly HudSettings settings;
        readonly Image timeIcon;
        readonly TextMeshProUGUI date;
        readonly RectTransform goalRoot;
        readonly CanvasGroup goalGroup;
        readonly Image goalMarker;
        readonly TextMeshProUGUI goal;

        string shownGoal = "", pendingGoal;
        float goalAnim = -1f;      // 0..1 while the goal changes, -1 when idle
        float flashLeft;
        float iconBump;
        TimeOfDayIcon? shownIcon;

        public HudDayBlock(Transform parent, HudTheme theme, HudSettings settings)
        {
            this.theme = theme;
            this.settings = settings;
            var root = HudBuild.Place(HudBuild.Rect("DayAndGoal", parent), new Vector2(0f, 1f),
                new Vector2(theme.margin.x, -theme.margin.y), new Vector2(GoalMaxWidth, theme.timeIconSize + DateHeight + GoalHeight + 16f));

            timeIcon = HudBuild.Image("TimeOfDay", root, null, Color.white);
            HudBuild.Place(timeIcon.rectTransform, new Vector2(0f, 1f), Vector2.zero, Vector2.one * theme.timeIconSize);

            date = HudBuild.Text("Day", root, theme, theme.titleFont, theme.dayFontSize);
            HudBuild.Place(date.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -theme.timeIconSize), new Vector2(400f, DateHeight));

            goalRoot = HudBuild.Place(HudBuild.Rect("Goal", root), new Vector2(0f, 1f),
                new Vector2(0f, -theme.timeIconSize - DateHeight - 12f), new Vector2(GoalMaxWidth, GoalHeight));
            goalGroup = goalRoot.gameObject.AddComponent<CanvasGroup>();
            HudBuild.Stretch(HudBuild.Panel("Back", goalRoot, theme).rectTransform);
            goalMarker = HudBuild.Image("Marker", goalRoot, HudArt.Circle, theme.accentColor);
            HudBuild.Place(goalMarker.rectTransform, new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(16f, 16f));
            goal = HudBuild.Text("Text", goalRoot, theme, theme.bodyFont, theme.objectiveFontSize);
            var gr = HudBuild.Stretch(goal.rectTransform);
            gr.offsetMin = new Vector2(46f, 0f);
            gr.offsetMax = new Vector2(-18f, 0f);
            goalGroup.alpha = 0f;

            SetTime(TimeOfDayIcon.Sun);
            SetDay(1);
        }

        public void SetDay(int day) => date.text = string.Format(settings.dayFormat, day);

        public void SetTime(TimeOfDayIcon icon)
        {
            if (shownIcon == icon) return;
            if (shownIcon.HasValue) iconBump = 1f;
            shownIcon = icon;
            var sprite = theme.TimeIcon(icon);
            timeIcon.sprite = sprite != null ? sprite : HudArt.TimeOfDay(icon);
            timeIcon.color = sprite != null ? Color.white : theme.TimeColor(icon);
            timeIcon.preserveAspect = true;
        }

        public void SetGoal(string text, bool instant)
        {
            text ??= "";
            if (instant)
            {
                shownGoal = text;
                pendingGoal = null;
                goalAnim = -1f;
                ApplyGoal(text);
                goalGroup.alpha = text.Length > 0 ? 1f : 0f;
                goalRoot.anchoredPosition = new Vector2(0f, goalRoot.anchoredPosition.y);
                return;
            }
            if (text == (pendingGoal ?? shownGoal)) return;
            pendingGoal = text;
            if (goalAnim < 0f) goalAnim = 0f;
        }

        public void Tick(float dt)
        {
            // Time icon: a short bump when the time of day changes.
            if (iconBump > 0f)
            {
                iconBump = Mathf.Max(0f, iconBump - dt * 2.5f);
                timeIcon.rectTransform.localScale = Vector3.one * (1f + 0.3f * Mathf.Sin(iconBump * Mathf.PI));
            }

            if (goalAnim >= 0f)
            {
                float half = Mathf.Max(0.01f, settings.objectiveChangeTime * 0.5f);
                bool hadOld = shownGoal.Length > 0;
                goalAnim += dt / half;
                float y = goalRoot.anchoredPosition.y;
                if (goalAnim < 1f && hadOld)
                {
                    goalGroup.alpha = 1f - HudBuild.Ease(goalAnim);                 // fade the old line out
                }
                else
                {
                    if (pendingGoal != null)
                    {
                        shownGoal = pendingGoal;
                        pendingGoal = null;
                        ApplyGoal(shownGoal);
                        if (!hadOld) goalAnim = Mathf.Max(goalAnim, 1f);
                        if (shownGoal.Length > 0) flashLeft = settings.objectiveFlashTime;
                    }
                    float t = HudBuild.Ease(goalAnim - 1f);
                    goalGroup.alpha = shownGoal.Length > 0 ? t : 0f;
                    goalRoot.anchoredPosition = new Vector2(-settings.objectiveSlide * (1f - t), y);  // slide in from the left
                    if (goalAnim >= 2f) goalAnim = -1f;
                }
            }

            if (flashLeft > 0f)
            {
                flashLeft = Mathf.Max(0f, flashLeft - dt);
                float k = flashLeft / Mathf.Max(0.01f, settings.objectiveFlashTime);
                goal.color = Color.Lerp(theme.textColor, theme.accentColor, k * (0.5f + 0.5f * Mathf.Cos(k * Mathf.PI * 4f)));
                goalMarker.rectTransform.localScale = Vector3.one * (1f + 0.6f * k);
            }
        }

        void ApplyGoal(string text)
        {
            goal.text = text;
            goal.color = theme.textColor;
            // The panel hugs the text, up to the max width; long goals end in "…".
            float w = Mathf.Min(GoalMaxWidth, goal.GetPreferredValues(text).x + 46f + 22f);
            goalRoot.sizeDelta = new Vector2(w, GoalHeight);
        }
    }
}
