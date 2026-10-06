using Funseki.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Funseki.UI
{
    // The save icon (GDD 5.7): a spinning ring with «Сохранение…» in the bottom-right corner for a moment after
    // every save (GameEvents.OnGameSaved). Child of [Bootstrap], so it shows in every scene. Runs on unscaled time.
    public class SaveIndicator : MonoBehaviour
    {
        [SerializeField] HudTheme theme;
        [SerializeField] MenuSettings settings;

        CanvasGroup group;
        RectTransform ring;
        float shownUntil = -1f;

        void Awake()
        {
            if (theme == null || settings == null)
            {
                Debug.LogError("[SaveIndicator] HudTheme or MenuSettings is not assigned.", this);
                enabled = false;
                return;
            }
            Build();
        }

        void OnEnable() => GameEvents.OnGameSaved += OnGameSaved;
        void OnDisable() => GameEvents.OnGameSaved -= OnGameSaved;

        void OnGameSaved(string path) => shownUntil = Time.unscaledTime + settings.saveIconTime;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bool show = Time.unscaledTime < shownUntil;
            group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, dt * 4f);
            if (group.alpha > 0f) ring.Rotate(0f, 0f, -240f * dt);
        }

        void Build()
        {
            MenuBuild.Canvas("SaveIconCanvas", transform, theme, settings.sortingOrder + 5, out group);
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            var root = group.transform;

            float size = settings.saveIconSize;
            var box = HudBuild.Place(HudBuild.Rect("SaveIcon", root), new Vector2(1f, 0f), new Vector2(-theme.margin.x, theme.margin.y),
                new Vector2(size, size));
            var ringImg = HudBuild.Image("Ring", box, HudArt.Ring, theme.accentColor);
            HudBuild.Stretch(ringImg.rectTransform);
            ring = ringImg.rectTransform;
            // A gap in the ring makes the spin visible.
            var gap = HudBuild.Image("Gap", ring, HudArt.Circle, theme.panelColor);
            HudBuild.Place(gap.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -size * 0.02f), new Vector2(size * 0.28f, size * 0.28f));

            var text = HudBuild.Text("Text", root, theme, theme.bodyFont, settings.textFontSize * 0.8f, TextAlignmentOptions.Right);
            HudBuild.Place(text.rectTransform, new Vector2(1f, 0f), new Vector2(-theme.margin.x - size - 14f, theme.margin.y),
                new Vector2(420f, size));
            text.text = settings.savingText;
        }
    }
}
