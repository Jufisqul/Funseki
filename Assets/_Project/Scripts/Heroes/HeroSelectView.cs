using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Funseki.Heroes
{
    // The switch window (GDD 2.1): three portrait cards side by side with the key to press, the hero's name and
    // the name of the Q action. Built in code on its own canvas; HeroParty shows and hides it.
    public class HeroSelectView
    {
        public event Action<int> CardClicked;

        readonly HeroSettings settings;
        readonly GameObject root;
        readonly List<(Image frame, TextMeshProUGUI current)> cards = new();

        public HeroSelectView(HeroSettings settings, IReadOnlyList<HeroData> heroes, Transform parent)
        {
            this.settings = settings;

            root = new GameObject("HeroSelectCanvas");
            root.transform.SetParent(parent, false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            var dim = Rect("Dim", root.transform);
            Stretch(dim);
            dim.gameObject.AddComponent<Image>().color = settings.backgroundColor;

            var title = Text("Title", root.transform, settings.title, 56f, FontStyles.Bold);
            Anchor(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, settings.cardSize.y * 0.5f + 90f), new Vector2(1200f, 80f));

            var hint = Text("Hint", root.transform, settings.hint, 28f, FontStyles.Normal);
            hint.color = new Color(settings.textColor.r, settings.textColor.g, settings.textColor.b, 0.7f);
            Anchor(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -settings.cardSize.y * 0.5f - 70f), new Vector2(1200f, 50f));

            float gap = 40f;
            float total = heroes.Count * settings.cardSize.x + (heroes.Count - 1) * gap;
            for (int i = 0; i < heroes.Count; i++)
            {
                float x = -total * 0.5f + settings.cardSize.x * 0.5f + i * (settings.cardSize.x + gap);
                cards.Add(BuildCard(i, heroes[i], new Vector2(x, 0f)));
            }

            root.SetActive(false);
        }

        public bool IsOpen => root.activeSelf;

        public void Show(int currentIndex)
        {
            for (int i = 0; i < cards.Count; i++)
                cards[i].current.gameObject.SetActive(i == currentIndex);
            root.SetActive(true);
        }

        public void Hide() => root.SetActive(false);

        public void Destroy()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
        }

        (Image, TextMeshProUGUI) BuildCard(int index, HeroData hero, Vector2 position)
        {
            Color accent = hero != null ? hero.color : Color.gray;
            var card = Rect($"Card_{index + 1}", root.transform);
            Anchor(card, new Vector2(0.5f, 0.5f), position, settings.cardSize);

            // Frame in the hero's color, card body inset by 6 px.
            var frame = card.gameObject.AddComponent<Image>();
            frame.color = accent;
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;
            button.onClick.AddListener(() => CardClicked?.Invoke(index));

            var body = Rect("Body", card);
            Stretch(body, 6f);
            body.gameObject.AddComponent<Image>().color = settings.cardColor;
            body.GetComponent<Image>().raycastTarget = false;

            float w = settings.cardSize.x, h = settings.cardSize.y;
            var portraitRect = Rect("Portrait", body);
            Anchor(portraitRect, new Vector2(0.5f, 1f), new Vector2(0f, -20f - (w - 40f) * 0.5f), new Vector2(w - 40f, w - 40f));
            var portrait = portraitRect.gameObject.AddComponent<Image>();
            portrait.raycastTarget = false;
            if (hero != null && hero.portrait != null)
            {
                portrait.sprite = hero.portrait;
                portrait.preserveAspect = true;
            }
            else
            {
                portrait.color = Color.Lerp(accent, settings.cardColor, 0.35f);
                string letter = hero != null && !string.IsNullOrEmpty(hero.displayName) ? hero.displayName.Substring(0, 1) : "?";
                var initial = Text("Initial", portraitRect, letter, 140f, FontStyles.Bold);
                Stretch(initial.rectTransform);
            }

            var name = Text("Name", body, hero != null ? hero.displayName : "—", 34f, FontStyles.Bold);
            Anchor(name.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 95f), new Vector2(w - 20f, 44f));

            string abilityName = hero != null && hero.ability != null ? "Q — " + hero.ability.displayName : "";
            var ability = Text("Ability", body, abilityName, 24f, FontStyles.Normal);
            ability.color = accent;
            Anchor(ability.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 55f), new Vector2(w - 20f, 34f));

            var key = Text("Key", card, (index + 1).ToString(), 40f, FontStyles.Bold);
            key.color = settings.cardColor;
            Anchor(key.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -32f), new Vector2(64f, 64f));
            var keyBack = Rect("KeyBack", card);
            keyBack.SetSiblingIndex(key.transform.GetSiblingIndex());
            Anchor(keyBack, new Vector2(0.5f, 0f), new Vector2(0f, -32f), new Vector2(56f, 56f));
            var keyImg = keyBack.gameObject.AddComponent<Image>();
            keyImg.color = accent;
            keyImg.raycastTarget = false;

            var current = Text("Current", body, settings.currentLabel, 22f, FontStyles.Italic);
            current.color = accent;
            Anchor(current.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(w - 20f, 30f));
            return (frame, current);
        }

        TextMeshProUGUI Text(string name, Transform parent, string text, float size, FontStyles style)
        {
            var t = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (settings.font != null) t.font = settings.font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = settings.textColor;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            return t;
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform r, float inset = 0f)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(inset, inset);
            r.offsetMax = new Vector2(-inset, -inset);
        }

        static void Anchor(RectTransform r, Vector2 anchor, Vector2 position, Vector2 size)
        {
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = position;
            r.sizeDelta = size;
        }
    }
}
