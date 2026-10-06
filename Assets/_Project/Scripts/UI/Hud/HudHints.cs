using System.Collections.Generic;
using Funseki.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Funseki.UI
{
    // Key names and icons for an action, for the keyboard or the gamepad (whichever the player has).
    public static class HudKeys
    {
        public const string KeyboardGroup = "Keyboard&Mouse";
        public const string GamepadGroup = "Gamepad";

        /// <summary>The player last used a gamepad: hints show its buttons. Keyboard and mouse by default; a merely
        /// plugged-in pad (or a virtual one from some driver) does not switch the hints, only pressing / moving it does.</summary>
        public static bool UseGamepad { get; private set; }

        /// <summary>Called every frame by HudView; true when the device kind changed.</summary>
        public static bool UpdateLastDevice()
        {
            bool was = UseGamepad;
            var pad = Gamepad.current;
            if (pad != null && (AnyButton(pad) || pad.leftStick.ReadValue().sqrMagnitude > 0.25f || pad.rightStick.ReadValue().sqrMagnitude > 0.25f))
                UseGamepad = true;
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if ((kb != null && kb.anyKey.wasPressedThisFrame) || (mouse != null && (mouse.leftButton.wasPressedThisFrame
                || mouse.rightButton.wasPressedThisFrame || mouse.delta.ReadValue().sqrMagnitude > 4f || mouse.scroll.ReadValue().y != 0f)))
                UseGamepad = false;
            return was != UseGamepad;
        }

        static bool AnyButton(Gamepad pad)
        {
            foreach (var c in pad.allControls)
                if (c is UnityEngine.InputSystem.Controls.ButtonControl b && !b.synthetic && b.wasPressedThisFrame) return true;
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetDevice() => UseGamepad = false;

        public static string DisplayName(InputAction action, bool gamepad)
        {
            if (action == null) return "";
            string s = action.GetBindingDisplayString(InputBinding.MaskByGroup(gamepad ? GamepadGroup : KeyboardGroup),
                InputBinding.DisplayStringOptions.DontIncludeInteractions);
            int bar = s.IndexOf(" | ");             // several bindings (WASD | arrows): the first one is enough
            return bar > 0 ? s.Substring(0, bar) : s;
        }

        /// <summary>Path of the first binding of the action for the device (a composite's first part).</summary>
        public static string FirstPath(InputAction action, bool gamepad)
        {
            if (action == null) return null;
            string group = gamepad ? GamepadGroup : KeyboardGroup;
            foreach (var b in action.bindings)
                if (!b.isComposite && !string.IsNullOrEmpty(b.groups) && b.groups.Contains(group)) return b.path;
            return null;
        }
    }

    // Bottom middle, above the inventory: first-time control hints (onboarding). Each hint from HudSettings comes up
    // once, on its trigger, for HudSettings.hintTime seconds: the key (or gamepad button) and what it does.
    // Seen hints are kept in WorldFlags (hud_hint_<id>), so they stay seen after a load.
    public class HudHints
    {
        class Pending
        {
            public HudSettings.Hint data;
            public InputAction action;
            public bool armed;
            public float wait;
        }

        const float BottomY = 200f;
        const float Height = 64f;
        const float FadeTime = 0.25f;

        readonly HudTheme theme;
        readonly HudSettings settings;
        readonly RectTransform root;
        readonly CanvasGroup group;
        readonly Image cap, capIcon;
        readonly TextMeshProUGUI capText, text;
        readonly List<Pending> pending = new();
        readonly InputActionMap map;

        Pending current;
        float shownFor, gapLeft;
        float breakTime;
        bool currentPad;
        bool loaded;

        public HudHints(Transform parent, HudTheme theme, HudSettings settings)
        {
            this.theme = theme;
            this.settings = settings;
            map = settings.actions != null ? settings.actions.FindActionMap(settings.actionMap) : null;

            root = HudBuild.Place(HudBuild.Rect("Hint", parent), new Vector2(0.5f, 0f), new Vector2(0f, BottomY), new Vector2(400f, Height));
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            HudBuild.Stretch(HudBuild.Panel("Back", root, theme).rectTransform);

            cap = HudBuild.Image("Key", root, HudArt.RoundedRect, theme.keyCapColor);
            cap.type = Image.Type.Sliced;
            capText = HudBuild.Text("Label", cap.transform, theme, theme.titleFont, theme.keyFontSize, TextAlignmentOptions.Center);
            capText.color = theme.keyTextColor;
            capText.outlineWidth = 0f;
            HudBuild.Stretch(capText.rectTransform);
            capIcon = HudBuild.Image("Icon", root, null, Color.white);

            text = HudBuild.Text("Text", root, theme, theme.bodyFont, theme.hintFontSize);
            root.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- triggers

        public void OnInteractionTarget() => Arm(HudHintTrigger.InteractionTarget);
        public void OnItemAdded() => Arm(HudHintTrigger.ItemAdded);
        public void OnHeroSwitched() => Arm(HudHintTrigger.HeroSwitched);

        void Arm(HudHintTrigger trigger)
        {
            Load();
            foreach (var p in pending)
                if (p.data.trigger == trigger && !p.armed) { p.armed = true; p.wait = p.data.delay; }
        }

        /// <summary>The device changed (a gamepad plugged in or out): relabel the hint on screen.</summary>
        public void RefreshDevice()
        {
            if (current != null && currentPad != HudKeys.UseGamepad) Fill(current);
        }

        // ---------------------------------------------------------------- update

        /// <param name="visible">The HUD is on screen (hints wait while it is hidden)</param>
        /// <param name="inBreak">The game is in a break (BreakTime counts only then)</param>
        public void Tick(float dt, bool visible, bool inBreak)
        {
            Load();
            if (visible && inBreak) breakTime += dt;

            // A hint that is no longer needed: the player found the key on his own.
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var p = pending[i];
                if (p.data.trigger == HudHintTrigger.BreakTime && !p.armed && visible && inBreak) { p.armed = true; p.wait = p.data.delay; }
                if (p.armed && visible) p.wait -= dt;
                if (visible && p.data.skipIfAlreadyUsed && p.action != null && p.action.WasPerformedThisFrame() && p != current
                    && p.action.ReadValueAsObject() is not Vector2 { sqrMagnitude: < 0.01f })
                {
                    MarkSeen(p);
                    pending.RemoveAt(i);
                }
            }

            if (current != null)
            {
                shownFor += dt;
                float total = Mathf.Max(settings.hintTime, FadeTime * 2f);
                float a = shownFor < FadeTime ? shownFor / FadeTime : shownFor > total - FadeTime ? (total - shownFor) / FadeTime : 1f;
                group.alpha = visible ? Mathf.Clamp01(a) : 0f;
                root.localScale = Vector3.one * (1f + 0.08f * Mathf.Clamp01(1f - shownFor / FadeTime));
                if (shownFor >= total)
                {
                    current = null;
                    root.gameObject.SetActive(false);
                    gapLeft = settings.hintGap;
                }
                return;
            }

            if (gapLeft > 0f) { gapLeft -= dt; return; }
            if (!visible) return;
            foreach (var p in pending)
            {
                if (!p.armed || p.wait > 0f) continue;
                Show(p);
                break;
            }
        }

        void Show(Pending p)
        {
            pending.Remove(p);
            MarkSeen(p);
            current = p;
            shownFor = 0f;
            Fill(p);
            root.gameObject.SetActive(true);
        }

        // Key cap (or icon) on the left, the text on the right; the panel hugs both.
        void Fill(Pending p)
        {
            currentPad = HudKeys.UseGamepad;
            var h = p.data;
            string label = currentPad ? h.gamepadLabel : h.keyboardLabel;
            if (string.IsNullOrEmpty(label)) label = HudKeys.DisplayName(p.action, currentPad);
            var icon = currentPad ? h.gamepadIcon : h.keyboardIcon;
            if (icon == null) icon = theme.KeyIconFor(HudKeys.FirstPath(p.action, currentPad));

            float capH = theme.keyCapHeight, pad = 14f;
            float capW;
            if (icon != null)
            {
                cap.gameObject.SetActive(false);
                capIcon.gameObject.SetActive(true);
                capIcon.sprite = icon;
                capIcon.preserveAspect = true;
                capW = capH * icon.rect.width / Mathf.Max(1f, icon.rect.height);
                HudBuild.Place(capIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(pad, 0f), new Vector2(capW, capH));
                capIcon.rectTransform.pivot = new Vector2(0f, 0.5f);
            }
            else
            {
                cap.gameObject.SetActive(true);
                capIcon.gameObject.SetActive(false);
                cap.sprite = currentPad ? HudArt.Circle : HudArt.RoundedRect;
                cap.type = currentPad ? Image.Type.Simple : Image.Type.Sliced;
                cap.color = currentPad ? theme.padCapColor : theme.keyCapColor;
                capText.text = label;
                capW = Mathf.Max(capH, capText.GetPreferredValues(label).x + 24f);
                if (currentPad && capW > capH) { cap.sprite = HudArt.RoundedRect; cap.type = Image.Type.Sliced; }
                HudBuild.Place(cap.rectTransform, new Vector2(0f, 0.5f), new Vector2(pad, 0f), new Vector2(capW, capH));
                cap.rectTransform.pivot = new Vector2(0f, 0.5f);
            }

            text.text = h.text;
            float textW = text.GetPreferredValues(h.text).x;
            HudBuild.Place(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(pad + capW + 16f, 0f), new Vector2(textW + 4f, Height));
            text.rectTransform.pivot = new Vector2(0f, 0.5f);
            root.sizeDelta = new Vector2(pad + capW + 16f + textW + pad + 6f, Height);
        }

        // ---------------------------------------------------------------- seen flags

        void Load()
        {
            if (loaded || !ServiceLocator.TryGet<WorldFlags>(out var flags)) return;
            loaded = true;
            foreach (var h in settings.hints)
            {
                if (h == null || string.IsNullOrEmpty(h.id) || flags.GetFlag(FlagOf(h))) continue;
                var action = map != null && !string.IsNullOrEmpty(h.action) ? map.FindAction(h.action) : null;
                if (action == null && !string.IsNullOrEmpty(h.action))
                    Debug.LogWarning($"[HUD] Hint '{h.id}': no action '{h.action}' in map '{settings.actionMap}'.");
                pending.Add(new Pending { data = h, action = action });
            }
        }

        static string FlagOf(HudSettings.Hint h) => "hud_hint_" + h.id;

        static void MarkSeen(Pending p)
        {
            if (ServiceLocator.TryGet<WorldFlags>(out var flags)) flags.SetFlag(FlagOf(p.data), true);
        }
    }
}
