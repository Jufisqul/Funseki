using System;
using System.Collections.Generic;
using Funseki.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Funseki.UI
{
    /// <summary>When a first-time control hint comes up.</summary>
    public enum HudHintTrigger
    {
        /// <summary>The HUD has been visible in a break for 'delay' seconds (counted only while it is visible).</summary>
        BreakTime,
        /// <summary>The hero aimed at something he can use for the first time (E).</summary>
        InteractionTarget,
        /// <summary>The first item came into the inventory (wheel).</summary>
        ItemAdded,
        /// <summary>The first hero switch happened.</summary>
        HeroSwitched,
    }

    // Behavior and texts of the HUD (GDD 8.2): when it is shown, timings of animations, texts and the list of
    // first-time control hints. The look is in HudTheme. Assets/_Project/Data/UI/HudSettings.asset.
    [CreateAssetMenu(fileName = "HudSettings", menuName = "Funseki/UI/HUD Settings")]
    public class HudSettings : ScriptableObject
    {
        [Serializable]
        public class Hint
        {
            [Tooltip("snake_case; the hint is remembered in WorldFlags as hud_hint_<id> and never shown again")]
            public string id;
            public HudHintTrigger trigger;
            [Tooltip("Seconds after the trigger (BreakTime: of visible break time)")]
            [Min(0f)] public float delay;
            [Tooltip("Action in the Gameplay map whose keys are shown: Move, Interact, HeroMenu, HeroAbility, CycleItem, FirstPerson")]
            public string action;
            [Tooltip("What the key does: «Ходить»")]
            public string text;
            [Tooltip("Key cap text on the keyboard; empty = the name of the bound key")]
            public string keyboardLabel;
            [Tooltip("Button text on a gamepad; empty = the name of the bound button")]
            public string gamepadLabel;
            [Tooltip("Icons instead of the caps; empty = HudTheme.keyIcons by the bound control, then a cap")]
            public Sprite keyboardIcon;
            public Sprite gamepadIcon;
            [Tooltip("If the player already used this action before the hint came up, it is not shown")]
            public bool skipIfAlreadyUsed = true;
        }

        [Header("Visibility")]
        [Tooltip("The HUD is shown only in these states (hidden in cutscenes, dialogues, lessons, «Поймали»)")]
        public GameState[] visibleStates = { GameState.Break };
        [Tooltip("States that keep the HUD as it was (the hero switch window pauses the game)")]
        public GameState[] keepStates = { GameState.Paused };
        [Tooltip("s")]
        public float fadeTime = 0.25f;

        [Header("Day")]
        [Tooltip("{0} = day number")]
        public string dayFormat = "День {0}";

        [Header("Goal")]
        [Tooltip("Old goal fades out and the new one slides in, s")]
        public float objectiveChangeTime = 0.45f;
        [Tooltip("How far the new goal slides in from, px")]
        public float objectiveSlide = 24f;
        [Tooltip("The goal line flashes in the accent color this long after a change, s")]
        public float objectiveFlashTime = 1.2f;

        [Header("«Шум»")]
        [Tooltip("At «Тихо» and 0 the megaphone hides after this many seconds")]
        public float noiseHideDelay = 2f;
        [Tooltip("Pulses per second at «Подозрительно»")]
        public float noisePulseRate = 2.2f;
        [Tooltip("Size change of a pulse")]
        public float noisePulseScale = 0.15f;
        [Tooltip("A jolt when the noise grows, size change")]
        public float noiseBumpScale = 0.25f;

        [Header("Journal notification")]
        public string journalToastTitle = "Новая запись в журнале";
        [Tooltip("s")]
        public float journalToastTime = 3.5f;

        [Header("First-time control hints")]
        [Tooltip("Actions of the hints (GameInput.inputactions)")]
        public InputActionAsset actions;
        public string actionMap = "Gameplay";
        [Tooltip("How long a hint stays, s")]
        public float hintTime = 3f;
        [Tooltip("Pause between two hints in a row, s")]
        public float hintGap = 0.6f;
        public List<Hint> hints = new();

        public bool IsVisibleIn(GameState state) => Array.IndexOf(visibleStates, state) >= 0;
        public bool KeepsIn(GameState state) => Array.IndexOf(keepStates, state) >= 0;
    }
}
