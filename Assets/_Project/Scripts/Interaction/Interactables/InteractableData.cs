using System;
using System.Collections.Generic;
using Funseki.Core;
using UnityEngine;

namespace Funseki.Interaction
{
    public enum InteractRepeat
    {
        /// <summary>Once per game: after the first use the object stops offering E.</summary>
        Once,
        /// <summary>Once per school day (IDayCycle.Day).</summary>
        OncePerDay,
        Always,
    }

    // One hero's own lines; an empty list falls back to the common lines.
    [Serializable]
    public class HeroLines
    {
        public HeroId hero;
        [Tooltip("Instead of the common first-time lines")]
        [TextArea(1, 3)] public string[] first;
        [Tooltip("Instead of the common repeat lines")]
        [TextArea(1, 3)] public string[] repeat;
    }

    // Shared settings of a world object the heroes use with E (GDD 5.9): prompt, lines, repeat rule, flags, noise.
    // One asset can serve several objects (two vending machines); each object keeps its own state by objectId.
    // Specific objects extend it (WaterTapData, VendingMachineData...). Assets: Data/Interaction/Interactables.
    [CreateAssetMenu(fileName = "Interactable_", menuName = "Funseki/Interaction/Interactable")]
    public class InteractableData : ScriptableObject
    {
        [Header("E")]
        [Tooltip("Prompt text without the key: «Открыть кран» -> «E — Открыть кран»")]
        public string prompt = "Осмотреть";
        public InteractRepeat repeat = InteractRepeat.Always;

        [Header("Who and when")]
        [Tooltip("Heroes that get the E prompt; empty = any hero")]
        public HeroId[] allowedHeroes;
        [Tooltip("School days the object works on; empty = every day")]
        public int[] onlyOnDays;

        [Header("Lines (a random one from the list)")]
        [Tooltip("The first use in the game")]
        [TextArea(1, 3)] public string[] firstLines;
        [Tooltip("Every later use; empty = silent")]
        [TextArea(1, 3)] public string[] repeatLines;
        [Tooltip("Per-hero lines; a hero without an entry (or with an empty list) says the common ones")]
        public List<HeroLines> heroLines = new();

        [Header("Teacher nearby")]
        [Tooltip("Said instead of the usual line when a teacher can see the hero; empty = the usual line")]
        [TextArea(1, 3)] public string[] teacherNearbyLines;
        [Tooltip("How far a teacher can be to count as «nearby», m")]
        public float teacherRadius = 12f;

        [Header("Noise («Шум»)")]
        [Tooltip("Noise added when a teacher sees the use; 0 = this object never makes noise")]
        [Min(0)] public int noiseAmount;
        [Tooltip("Reason id for GameEvents.OnNoiseMade, snake_case")]
        public string noiseReason;

        [Header("World flags")]
        [Tooltip("The object offers E only while this flag is set; empty = always")]
        public string requiredFlag;
        [Tooltip("The object stops offering E once this flag is set; empty = never")]
        public string blockedByFlag;
        [Tooltip("Set on the first use (snake_case)")]
        public string[] flagsOnFirstUse;
        [Tooltip("Counters increased on every use (snake_case)")]
        public string[] countersOnUse;

        public string[] FirstLines(HeroId hero)
        {
            var own = heroLines.Find(h => h.hero == hero);
            return own != null && own.first != null && own.first.Length > 0 ? own.first : firstLines;
        }

        public string[] RepeatLines(HeroId hero)
        {
            var own = heroLines.Find(h => h.hero == hero);
            return own != null && own.repeat != null && own.repeat.Length > 0 ? own.repeat : repeatLines;
        }

        public static string Pick(string[] lines) =>
            lines == null || lines.Length == 0 ? null : lines[UnityEngine.Random.Range(0, lines.Length)];
    }
}
