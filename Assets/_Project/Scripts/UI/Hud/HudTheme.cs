using System;
using TMPro;
using UnityEngine;

namespace Funseki.UI
{
    // The look of the HUD in one place (GDD 8.2): fonts, colors, sizes and icons. The artist swaps placeholders here.
    // Every sprite may stay empty: the HUD then draws a flat placeholder shape tinted with the color next to it.
    // Assets/_Project/Data/UI/HudTheme.asset.
    [CreateAssetMenu(fileName = "HudTheme", menuName = "Funseki/UI/HUD Theme")]
    public class HudTheme : ScriptableObject
    {
        [Serializable]
        public class KeyIcon
        {
            [Tooltip("Control path as in the input actions: <Keyboard>/e, <Gamepad>/buttonWest, <Mouse>/scroll/y")]
            public string path;
            public Sprite sprite;
        }

        [Header("Screen")]
        [Tooltip("The HUD is laid out for this size and scaled; corners stay in the corners on 16:9 and 16:10")]
        public Vector2 referenceResolution = new(1920f, 1080f);
        [Tooltip("0 = scale by width, 1 = by height. 1 keeps the elements the same height on 16:9 and 16:10")]
        [Range(0f, 1f)] public float matchWidthOrHeight = 1f;
        [Tooltip("Distance of the HUD blocks from the screen edges, px of the reference size")]
        public Vector2 margin = new(40f, 32f);
        [Tooltip("Canvas sorting order; the hero switch window is 60, dialogue 80")]
        public int sortingOrder = 30;

        [Header("Fonts")]
        [Tooltip("«День 1», key caps")]
        public TMP_FontAsset titleFont;
        [Tooltip("Goal, hints, notifications, names")]
        public TMP_FontAsset bodyFont;
        public float dayFontSize = 40f;
        public float objectiveFontSize = 30f;
        public float hintFontSize = 30f;
        public float keyFontSize = 28f;
        public float toastTitleFontSize = 24f;
        public float toastTextFontSize = 30f;
        public float heroNameFontSize = 18f;
        public float cooldownFontSize = 30f;

        [Header("Colors")]
        public Color textColor = new(1f, 0.98f, 0.93f, 1f);
        public Color textDimColor = new(1f, 0.98f, 0.93f, 0.65f);
        [Tooltip("Text outline, so white text reads on a bright yard")]
        public Color textOutlineColor = new(0.08f, 0.06f, 0.1f, 0.9f);
        [Range(0f, 1f)] public float textOutlineWidth = 0.18f;
        [Tooltip("Background of the goal line, hint and notification")]
        public Color panelColor = new(0.1f, 0.08f, 0.14f, 0.72f);
        [Tooltip("Accent: the goal marker, the notification stripe")]
        public Color accentColor = new(1f, 0.78f, 0.25f, 1f);

        [Header("Day and time of day")]
        public Sprite dawnIcon;
        public Sprite sunIcon;
        public Sprite sunsetIcon;
        public Color dawnColor = new(1f, 0.62f, 0.55f, 1f);
        public Color sunColor = new(1f, 0.85f, 0.3f, 1f);
        public Color sunsetColor = new(1f, 0.45f, 0.2f, 1f);
        public float timeIconSize = 72f;

        [Header("Party")]
        public float portraitSize = 84f;
        [Tooltip("The hero in control is this much bigger")]
        public float currentPortraitScale = 1.25f;
        public float portraitSpacing = 14f;
        [Tooltip("Frame width; the frame takes the hero's color")]
        public float portraitFrame = 5f;
        [Tooltip("Portraits of the other two heroes are tinted with this")]
        public Color inactivePortraitTint = new(0.6f, 0.6f, 0.65f, 0.85f);
        [Tooltip("Dark sector over the portrait while Q recharges")]
        public Color cooldownColor = new(0.05f, 0.04f, 0.08f, 0.7f);
        [Tooltip("Q badge on the portrait when the action is ready")]
        public Color abilityReadyColor = new(0.45f, 0.95f, 0.55f, 1f);
        public Color abilitySpentColor = new(0.55f, 0.55f, 0.6f, 1f);

        [Header("«Шум» (megaphone)")]
        public Sprite noiseIcon;
        public float noiseIconSize = 84f;
        public Color noiseQuietColor = new(1f, 0.98f, 0.93f, 1f);
        public Color noiseSuspiciousColor = new(1f, 0.55f, 0.2f, 1f);
        public Color noiseCaughtColor = new(1f, 0.2f, 0.2f, 1f);
        public Color noiseBarBackColor = new(0.1f, 0.08f, 0.14f, 0.6f);
        public Vector2 noiseBarSize = new(110f, 12f);

        [Header("Hints and key icons")]
        [Tooltip("Key cap behind a key name when there is no icon")]
        public Color keyCapColor = new(1f, 0.98f, 0.93f, 1f);
        public Color keyTextColor = new(0.1f, 0.08f, 0.14f, 1f);
        [Tooltip("Gamepad buttons are drawn as round caps in this color")]
        public Color padCapColor = new(0.35f, 0.75f, 1f, 1f);
        public float keyCapHeight = 52f;
        [Tooltip("Icons for keys and buttons; a control without an icon is shown as a cap with its name")]
        public KeyIcon[] keyIcons = Array.Empty<KeyIcon>();

        [Header("Journal notification")]
        public Sprite journalIcon;
        public Color journalAccentColor = new(1f, 0.78f, 0.25f, 1f);
        public Vector2 toastSize = new(460f, 92f);

        [Header("Panels")]
        [Tooltip("9-slice background of the panels; empty = a rounded placeholder")]
        public Sprite panelSprite;

        public Sprite TimeIcon(Core.TimeOfDayIcon icon) => icon switch
        {
            Core.TimeOfDayIcon.Dawn => dawnIcon,
            Core.TimeOfDayIcon.Sunset => sunsetIcon,
            _ => sunIcon,
        };

        public Color TimeColor(Core.TimeOfDayIcon icon) => icon switch
        {
            Core.TimeOfDayIcon.Dawn => dawnColor,
            Core.TimeOfDayIcon.Sunset => sunsetColor,
            _ => sunColor,
        };

        public Sprite KeyIconFor(string path)
        {
            if (string.IsNullOrEmpty(path) || keyIcons == null) return null;
            foreach (var k in keyIcons)
                if (k != null && k.sprite != null && string.Equals(k.path, path, StringComparison.OrdinalIgnoreCase)) return k.sprite;
            return null;
        }
    }
}
