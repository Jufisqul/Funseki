using Funseki.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Funseki.UI
{
    // Top-right corner: the «Шум» megaphone with a thin bar under it. Hidden at «Тихо»; appears the first time the
    // noise grows (with a jolt), pulses at «Подозрительно», turns red at «Поймали», hides again a moment after the
    // noise is back to zero.
    public class HudNoiseBlock
    {
        readonly HudTheme theme;
        readonly HudSettings settings;
        readonly RectTransform root;
        readonly CanvasGroup group;
        readonly RectTransform iconRect;
        readonly Image icon, barFill;

        float value, max = 100f;
        NoiseStage stage;
        bool shown;
        float hideTimer = -1f;
        float bump, pulseTime;

        public HudNoiseBlock(Transform parent, HudTheme theme, HudSettings settings)
        {
            this.theme = theme;
            this.settings = settings;
            float w = Mathf.Max(theme.noiseIconSize, theme.noiseBarSize.x);
            root = HudBuild.Place(HudBuild.Rect("Noise", parent), new Vector2(1f, 1f), new Vector2(-theme.margin.x, -theme.margin.y),
                new Vector2(w, theme.noiseIconSize + theme.noiseBarSize.y + 10f));
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            icon = HudBuild.Image("Megaphone", root, theme.noiseIcon != null ? theme.noiseIcon : HudArt.Megaphone, theme.noiseQuietColor);
            iconRect = HudBuild.Place(icon.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, Vector2.one * theme.noiseIconSize);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(0f, -theme.noiseIconSize * 0.5f);

            var back = HudBuild.Image("Bar", root, HudArt.RoundedRect, theme.noiseBarBackColor);
            back.type = Image.Type.Sliced;
            HudBuild.Place(back.rectTransform, new Vector2(0.5f, 0f), Vector2.zero, theme.noiseBarSize);
            barFill = HudBuild.Image("Fill", back.transform, HudArt.RoundedRect, theme.noiseQuietColor);
            barFill.type = Image.Type.Filled;
            barFill.fillMethod = Image.FillMethod.Horizontal;
            barFill.fillAmount = 0f;
            HudBuild.Stretch(barFill.rectTransform, 2f);
        }

        /// <summary>Current meter state without animation (scene start, after a load).</summary>
        public void Init(float v, float maxValue, NoiseStage s)
        {
            max = Mathf.Max(1f, maxValue);
            value = v;
            stage = s;
            shown = v > 0f;
            group.alpha = shown ? 1f : 0f;
        }

        public void SetNoise(float v, NoiseStage s)
        {
            if (ServiceLocator.TryGet<INoiseMeter>(out var meter)) max = Mathf.Max(1f, meter.Max);
            bool rose = v > value + 0.01f;
            if (rose)
            {
                shown = true;
                bump = 1f;
                hideTimer = -1f;
            }
            if (v <= 0.01f && s == NoiseStage.Quiet && shown && hideTimer < 0f) hideTimer = settings.noiseHideDelay;
            value = v;
            stage = s;
        }

        public void Tick(float dt)
        {
            if (hideTimer >= 0f)
            {
                hideTimer -= dt;
                if (value > 0.01f) hideTimer = -1f;
                else if (hideTimer < 0f) shown = false;
            }
            group.alpha = Mathf.MoveTowards(group.alpha, shown ? 1f : 0f, dt / Mathf.Max(0.01f, settings.fadeTime));

            Color color = stage switch
            {
                NoiseStage.Caught => theme.noiseCaughtColor,
                NoiseStage.Suspicious => theme.noiseSuspiciousColor,
                _ => theme.noiseQuietColor,
            };
            float scale = 1f;
            if (stage == NoiseStage.Suspicious)
            {
                pulseTime += dt;
                float p = 0.5f + 0.5f * Mathf.Sin(pulseTime * settings.noisePulseRate * Mathf.PI * 2f);
                scale += settings.noisePulseScale * p;
                color = Color.Lerp(color, theme.noiseCaughtColor, p * 0.35f);
            }
            else pulseTime = 0f;

            if (bump > 0f)
            {
                bump = Mathf.Max(0f, bump - dt * 4f);
                scale += settings.noiseBumpScale * Mathf.Sin(bump * Mathf.PI);
            }

            iconRect.localScale = Vector3.one * scale;
            iconRect.localRotation = Quaternion.Euler(0f, 0f, bump > 0f ? Mathf.Sin(bump * Mathf.PI * 3f) * 10f : 0f);
            icon.color = theme.noiseIcon != null ? Color.Lerp(Color.white, color, 0.5f) : color;
            barFill.color = color;
            barFill.fillAmount = Mathf.MoveTowards(barFill.fillAmount, value / max, dt * 2f);
        }
    }
}
