using Funseki.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Funseki.UI
{
    // Bottom-left corner: «Партия», the portraits of the three heroes. The hero in control is bigger and bright,
    // the other two are tinted. Every portrait has its Q state: a dark sector with seconds while the action recharges,
    // a key badge that lights up when it is ready. Reads IHeroStatus on the hero objects through HeroService.
    public class HudPartyBlock
    {
        class Card
        {
            public HeroId id;
            public IHeroStatus status;
            public RectTransform root;
            public Image frame, portrait, cooldown, badge;
            public TextMeshProUGUI initial, seconds, badgeKey, name;
            public float scale = 1f;
            public float readyFlash;
            public bool wasCooling;
        }

        readonly HudTheme theme;
        readonly RectTransform root;
        readonly Card[] cards = new Card[3];
        HeroId current;
        bool hasCurrent;
        string abilityKey = "Q";

        public HudPartyBlock(Transform parent, HudTheme theme)
        {
            this.theme = theme;
            float big = theme.portraitSize * theme.currentPortraitScale;
            float width = big + 2f * (theme.portraitSize + theme.portraitSpacing) + theme.portraitSpacing;
            root = HudBuild.Place(HudBuild.Rect("Party", parent), Vector2.zero, new Vector2(theme.margin.x, theme.margin.y),
                new Vector2(width, big + 28f));
            for (int i = 0; i < cards.Length; i++) cards[i] = BuildCard((HeroId)i);
            Layout(true);
        }

        public void SetAbilityKey(string label) => abilityKey = string.IsNullOrEmpty(label) ? "Q" : label;

        public void SetCurrent(HeroId id)
        {
            current = id;
            hasCurrent = true;
        }

        public void Tick(float dt)
        {
            bool any = false;
            foreach (var c in cards)
            {
                if (c.status == null || (c.status is Object o && o == null)) Bind(c);
                // Heroes not in the party yet (day 1 before room 7) get no card.
                bool shown = c.status != null && HeroService.IsInParty(c.id);
                c.root.gameObject.SetActive(shown);
                if (!shown) continue;
                any = true;

                bool isCurrent = hasCurrent ? c.id == current : HeroService.Current == c.id;
                float target = isCurrent ? theme.currentPortraitScale : 1f;
                c.scale = Mathf.MoveTowards(c.scale, target, dt * 3f);

                float total = c.status.AbilityCooldown;
                float left = c.status.AbilityCooldownLeft;
                int uses = c.status.AbilityUsesLeft;
                bool cooling = left > 0.01f && total > 0f;
                bool spent = uses == 0;

                c.cooldown.fillAmount = spent ? 1f : cooling ? Mathf.Clamp01(left / total) : 0f;
                c.seconds.text = cooling && !spent ? Mathf.CeilToInt(left).ToString() : "";
                if (c.wasCooling && !cooling && !spent) c.readyFlash = 1f;     // the action came back
                c.wasCooling = cooling;
                c.readyFlash = Mathf.Max(0f, c.readyFlash - dt * 2f);

                Color tint = isCurrent ? Color.white : theme.inactivePortraitTint;
                c.portrait.color = c.portrait.sprite != null ? tint : Color.Lerp(c.status.Color, theme.panelColor, 0.35f) * tint;
                c.frame.color = isCurrent ? c.status.Color : Color.Lerp(c.status.Color, theme.panelColor, 0.55f);
                c.badge.color = spent || cooling ? theme.abilitySpentColor : theme.abilityReadyColor;
                c.badge.rectTransform.localScale = Vector3.one * (1f + 0.45f * Mathf.Sin(c.readyFlash * Mathf.PI));
                c.badgeKey.text = abilityKey;
                c.name.color = isCurrent ? theme.textColor : theme.textDimColor;
            }
            root.gameObject.SetActive(any);
            if (any) Layout(false);
        }

        void Bind(Card c)
        {
            c.status = null;
            var hero = HeroService.GetHero(c.id);
            var status = hero != null ? hero.GetComponent<IHeroStatus>() : null;
            if (status == null) return;
            c.status = status;
            c.portrait.sprite = status.Portrait;
            c.portrait.preserveAspect = true;
            string n = string.IsNullOrEmpty(status.ShortName) ? c.id.ToString() : status.ShortName;
            c.initial.text = status.Portrait == null ? n.Substring(0, 1) : "";
            c.name.text = n;
        }

        // Cards stand in a row from the left, each at its current size, bottoms aligned.
        void Layout(bool instant)
        {
            float x = 0f;
            foreach (var c in cards)
            {
                if (instant) c.scale = 1f;
                if (!c.root.gameObject.activeSelf && !instant) continue;
                float size = theme.portraitSize * c.scale;
                c.root.anchoredPosition = new Vector2(x, 24f);
                c.root.sizeDelta = new Vector2(size, size);
                x += size + theme.portraitSpacing;
            }
        }

        Card BuildCard(HeroId id)
        {
            var c = new Card { id = id };
            c.root = HudBuild.Place(HudBuild.Rect($"Hero_{id}", root), Vector2.zero, Vector2.zero, Vector2.one * theme.portraitSize);

            c.frame = HudBuild.Image("Frame", c.root, HudArt.RoundedRect, Color.white);
            c.frame.type = Image.Type.Sliced;
            HudBuild.Stretch(c.frame.rectTransform);

            var inner = HudBuild.Image("Back", c.root, HudArt.RoundedRect, theme.panelColor);
            inner.type = Image.Type.Sliced;
            HudBuild.Stretch(inner.rectTransform, theme.portraitFrame);

            c.portrait = HudBuild.Image("Portrait", c.root, null, Color.white);
            HudBuild.Stretch(c.portrait.rectTransform, theme.portraitFrame);
            c.initial = HudBuild.Text("Initial", c.portrait.transform, theme, theme.titleFont, theme.portraitSize * 0.5f, TextAlignmentOptions.Center);
            HudBuild.Stretch(c.initial.rectTransform);
            c.initial.enableAutoSizing = true;
            c.initial.fontSizeMax = theme.portraitSize * 0.6f;

            // Q recharge: a clockwise dark sector shrinking to nothing, and the seconds left.
            c.cooldown = HudBuild.Image("Cooldown", c.root, HudArt.RoundedRect, theme.cooldownColor);
            c.cooldown.type = Image.Type.Filled;
            c.cooldown.fillMethod = Image.FillMethod.Radial360;
            c.cooldown.fillOrigin = (int)Image.Origin360.Top;
            c.cooldown.fillClockwise = false;
            c.cooldown.fillAmount = 0f;
            HudBuild.Stretch(c.cooldown.rectTransform, theme.portraitFrame);
            c.seconds = HudBuild.Text("Seconds", c.root, theme, theme.titleFont, theme.cooldownFontSize, TextAlignmentOptions.Center);
            HudBuild.Stretch(c.seconds.rectTransform);

            c.badge = HudBuild.Image("AbilityKey", c.root, HudArt.Circle, theme.abilityReadyColor);
            HudBuild.Place(c.badge.rectTransform, new Vector2(1f, 1f), new Vector2(-4f, -4f), new Vector2(30f, 30f));
            c.badge.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            c.badgeKey = HudBuild.Text("Key", c.badge.transform, theme, theme.titleFont, 18f, TextAlignmentOptions.Center);
            c.badgeKey.color = theme.keyTextColor;
            c.badgeKey.outlineWidth = 0f;
            c.badgeKey.enableAutoSizing = true;
            c.badgeKey.fontSizeMin = 8f;
            c.badgeKey.fontSizeMax = 18f;
            HudBuild.Stretch(c.badgeKey.rectTransform, 3f);

            c.name = HudBuild.Text("Name", c.root, theme, theme.bodyFont, theme.heroNameFontSize, TextAlignmentOptions.Center);
            var nr = c.name.rectTransform;
            nr.anchorMin = new Vector2(0f, 0f);
            nr.anchorMax = new Vector2(1f, 0f);
            nr.pivot = new Vector2(0.5f, 1f);
            nr.anchoredPosition = new Vector2(0f, -2f);
            nr.sizeDelta = new Vector2(30f, 24f);
            return c;
        }
    }
}
