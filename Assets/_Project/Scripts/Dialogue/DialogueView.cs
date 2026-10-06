using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Funseki.Dialogue
{
    // The dialogue window of GDD 5.3: portrait in the lower left with the name under it, the text cloud to the right,
    // «Пропуск» and «Авто» under the window, choices stacked above the cloud. Built in code from DialogueSettings.
    public class DialogueView
    {
        public event Action SkipClicked;
        public event Action AutoClicked;
        public event Action<int> ChoiceClicked;
        public event Action<int> ChoiceHovered;

        const float ButtonRowY = 20f, ButtonHeight = 46f, NameHeight = 46f, Gap = 10f;

        class ChoiceButton
        {
            public GameObject go;
            public Image background;
            public Image icon;
            public TextMeshProUGUI label;
            public bool voice;
        }

        readonly DialogueSettings settings;
        GameObject root;
        Image portrait;
        TextMeshProUGUI portraitLetter, nameLabel, body;
        Image autoButton, continueMark;
        RectTransform choicesRoot;
        readonly List<ChoiceButton> choices = new();
        int choiceCount;
        bool showContinue;

        public DialogueView(DialogueSettings settings, Transform parent)
        {
            this.settings = settings;
            Build(parent);
            root.SetActive(false);
        }

        public void Show() => root.SetActive(true);
        public void Hide()
        {
            HideChoices();
            root.SetActive(false);
        }

        // Sets the speaker and the whole text, hidden; returns the number of characters to type.
        public int SetLine(Speaker speaker, string text)
        {
            bool hasSpeaker = speaker != null;
            portrait.transform.parent.gameObject.SetActive(hasSpeaker);
            nameLabel.gameObject.SetActive(hasSpeaker);
            if (hasSpeaker)
            {
                nameLabel.text = speaker.displayName;
                nameLabel.color = speaker.nameColor;
                portrait.sprite = speaker.portrait;
                portrait.color = speaker.portrait != null ? Color.white : Color.Lerp(speaker.nameColor, Color.black, 0.45f);
                portrait.preserveAspect = true;
                portraitLetter.gameObject.SetActive(speaker.portrait == null);
                portraitLetter.text = string.IsNullOrEmpty(speaker.displayName) ? "?" : speaker.displayName.Substring(0, 1);
            }

            body.text = text ?? "";
            body.maxVisibleCharacters = 0;
            body.ForceMeshUpdate();
            SetContinueMark(false);
            return body.textInfo.characterCount;
        }

        public void SetVisible(int count) => body.maxVisibleCharacters = count;

        public char CharAt(int i)
        {
            var info = body.textInfo;
            return i >= 0 && i < info.characterCount ? info.characterInfo[i].character : ' ';
        }

        public void SetContinueMark(bool on) => showContinue = on;

        public void SetAuto(bool on) => autoButton.color = on ? settings.buttonActiveColor : settings.buttonColor;

        public void ShowChoices(IReadOnlyList<DialogueChoice> list)
        {
            choiceCount = list.Count;
            for (int i = 0; i < list.Count; i++)
            {
                var b = i < choices.Count ? choices[i] : CreateChoice(i);
                var c = list[i];
                b.voice = c.voice;
                b.label.text = c.voice ? settings.voicePrefix + c.text : c.text;
                b.icon.gameObject.SetActive(c.voice);
                b.go.SetActive(true);
            }
            for (int i = list.Count; i < choices.Count; i++) choices[i].go.SetActive(false);
            choicesRoot.gameObject.SetActive(list.Count > 0);
            SetSelected(0);
        }

        public void HideChoices()
        {
            choiceCount = 0;
            if (choicesRoot != null) choicesRoot.gameObject.SetActive(false);
        }

        public void SetSelected(int index)
        {
            for (int i = 0; i < choiceCount; i++)
            {
                var b = choices[i];
                Color baseColor = b.voice ? settings.voiceColor : settings.choiceColor;
                b.background.color = i == index ? settings.choiceSelectedColor : baseColor;
            }
        }

        // Blinks the "next line" mark.
        public void Tick()
        {
            bool on = showContinue && Mathf.Repeat(Time.unscaledTime, 0.8f) < 0.55f;
            if (continueMark.enabled != on) continueMark.enabled = on;
        }

        // ---------------------------------------------------------------- build

        void Build(Transform parent)
        {
            root = new GameObject("DialogueUI", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            float m = settings.margin;
            float size = settings.portraitSize;
            float windowBottom = ButtonRowY + ButtonHeight + Gap;
            float portraitBottom = windowBottom + NameHeight + Gap;

            // Portrait with a frame, name under it.
            var frame = Panel("PortraitFrame", root.transform, settings.portraitFrameColor);
            Place(frame.rectTransform, new Vector2(0f, 0f), new Vector2(m, portraitBottom), new Vector2(size, size));
            portrait = Panel("Portrait", frame.transform, Color.white);
            portrait.sprite = null;
            Stretch(portrait.rectTransform, 8f);
            portraitLetter = Label("Letter", portrait.transform, size * 0.45f, Color.white, TextAlignmentOptions.Center);
            Stretch(portraitLetter.rectTransform, 0f);

            nameLabel = Label("Name", root.transform, settings.nameFontSize, Color.white, TextAlignmentOptions.Center);
            nameLabel.fontStyle = FontStyles.Bold;
            nameLabel.outlineColor = new Color(0f, 0f, 0f, 0.85f);
            nameLabel.outlineWidth = 0.2f;
            Place(nameLabel.rectTransform, Vector2.zero, new Vector2(m, windowBottom), new Vector2(size, NameHeight));

            // Text cloud to the right of the portrait, with a little tail pointing at it.
            float cloudLeft = m + size + 36f;
            var tail = Panel("CloudTail", root.transform, settings.cloudColor);
            tail.sprite = null;
            Place(tail.rectTransform, Vector2.zero, new Vector2(cloudLeft, portraitBottom + size * 0.5f), new Vector2(34f, 34f));
            tail.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            tail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            var cloud = Panel("Cloud", root.transform, settings.cloudColor);
            var crt = cloud.rectTransform;
            crt.anchorMin = new Vector2(0f, 0f);
            crt.anchorMax = new Vector2(1f, 0f);
            crt.pivot = new Vector2(0f, 0f);
            crt.offsetMin = new Vector2(cloudLeft, portraitBottom);
            crt.offsetMax = new Vector2(-m, portraitBottom + settings.cloudHeight);

            body = Label("Text", cloud.transform, settings.textFontSize, settings.textColor, TextAlignmentOptions.TopLeft);
            body.textWrappingMode = TextWrappingModes.Normal;
            Stretch(body.rectTransform, 0f);
            body.margin = new Vector4(40f, 30f, 40f, 30f);

            continueMark = Panel("Continue", cloud.transform, settings.textColor);
            continueMark.sprite = null;
            var cm = continueMark.rectTransform;
            cm.anchorMin = cm.anchorMax = new Vector2(1f, 0f);
            cm.pivot = new Vector2(0.5f, 0.5f);
            cm.anchoredPosition = new Vector2(-34f, 30f);
            cm.sizeDelta = new Vector2(16f, 16f);
            cm.localRotation = Quaternion.Euler(0f, 0f, 45f);
            continueMark.enabled = false;

            // «Пропуск» and «Авто» under the window, right side.
            autoButton = Button("Auto", settings.autoLabel, new Vector2(-m, ButtonRowY), () => AutoClicked?.Invoke());
            Button("Skip", settings.skipLabel, new Vector2(-m - 180f, ButtonRowY), () => SkipClicked?.Invoke());

            // Choices above the cloud, right-aligned.
            var choicesGo = new GameObject("Choices", typeof(RectTransform));
            choicesGo.transform.SetParent(root.transform, false);
            choicesRoot = (RectTransform)choicesGo.transform;
            choicesRoot.anchorMin = choicesRoot.anchorMax = new Vector2(1f, 0f);
            choicesRoot.pivot = new Vector2(1f, 0f);
            choicesRoot.anchoredPosition = new Vector2(-m, portraitBottom + settings.cloudHeight + 18f);
            choicesRoot.sizeDelta = new Vector2(820f, 0f);
            var layout = choicesGo.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.LowerRight;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            choicesGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            choicesGo.SetActive(false);
        }

        ChoiceButton CreateChoice(int index)
        {
            var bg = Panel($"Choice{index + 1}", choicesRoot, settings.choiceColor);
            bg.raycastTarget = true;
            var row = bg.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(24, 24, 12, 12);
            row.spacing = 14f;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            bg.gameObject.AddComponent<LayoutElement>().minHeight = 60f;

            var icon = Panel("VoiceIcon", bg.transform, Color.white);
            icon.sprite = settings.voiceIcon;
            icon.type = Image.Type.Simple;
            icon.preserveAspect = true;
            var iconLayout = icon.gameObject.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = iconLayout.preferredHeight = 36f;
            iconLayout.minWidth = iconLayout.minHeight = 36f;

            var label = Label("Text", bg.transform, settings.choiceFontSize, settings.choiceTextColor, TextAlignmentOptions.Left);
            label.textWrappingMode = TextWrappingModes.Normal;
            label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var button = bg.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => { Deselect(); ChoiceClicked?.Invoke(index); });

            var trigger = bg.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => ChoiceHovered?.Invoke(index));
            trigger.triggers.Add(enter);

            var b = new ChoiceButton { go = bg.gameObject, background = bg, icon = icon, label = label };
            choices.Add(b);
            return b;
        }

        Image Button(string name, string text, Vector2 bottomRight, Action onClick)
        {
            var bg = Panel(name, root.transform, settings.buttonColor);
            bg.raycastTarget = true;
            Place(bg.rectTransform, new Vector2(1f, 0f), bottomRight, new Vector2(160f, ButtonHeight));
            var label = Label("Text", bg.transform, settings.buttonFontSize, settings.buttonTextColor, TextAlignmentOptions.Center);
            Stretch(label.rectTransform, 0f);
            var button = bg.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => { Deselect(); onClick(); });
            label.text = text;
            return bg;
        }

        // A clicked button stays selected and would react to Submit (Space / pad A) along with the dialogue's own keys.
        static void Deselect()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        Image Panel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = settings.panelSprite;
            img.type = settings.panelSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        TextMeshProUGUI Label(string name, Transform parent, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (settings.font != null) t.font = settings.font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            return t;
        }

        // Anchored to a corner given by 'anchor' (0..1), pivot at the same corner.
        static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
