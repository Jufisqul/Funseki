using Funseki.Core;
using UnityEngine;
using UnityEngine.Timeline;

namespace Funseki.Pranks
{
    // One prank (GDD 5.4): what it is called in the «Журнал недели», when it can be done, what it changes in the world
    // and what happens when a teacher sees it. Assets: Data/Pranks/Prank_*. Used by PrankTrigger on the bait object.
    [CreateAssetMenu(fileName = "Prank_", menuName = "Funseki/Pranks/Prank")]
    public class PrankData : ScriptableObject, IPrank
    {
        [Tooltip("snake_case: whistle")]
        public string id;

        [Header("«Журнал недели»")]
        public string title;
        [TextArea(2, 4)] public string journalCaption;

        [Header("E / item")]
        [Tooltip("Prompt without the key: «Взять свисток»")]
        public string prompt = "Взять";
        [Tooltip("Done with this item in hand (LMB on the bait) instead of E; empty = E")]
        public ItemData requiredItem;

        [Header("Conditions")]
        [Tooltip("Who can do it; empty = any hero")]
        public HeroId[] heroes;
        [Tooltip("Day phase ids (break_1) when it can be done; empty = any phase")]
        public string[] phaseIds;
        [Tooltip("All of these flags must be set")]
        public string[] requiredFlags;
        [Tooltip("None of these flags may be set")]
        public string[] blockedByFlags;

        [Header("Watchers (teachers)")]
        [Tooltip("Teachers farther than this don't see the prank, m")]
        public float watchRadius = 20f;
        [Tooltip("«Шум» added when a teacher sees it")]
        [Min(0)] public int noiseWhenSeen = 30;
        [Tooltip("A teacher who sees it catches the hero at once («Поймали»), the prank does not happen")]
        public bool seenMeansCaught;
        [Tooltip("Which «Поймали» scene plays (CaughtScene_*); empty = the default from NoiseSettings")]
        public CaughtSceneData caughtScene;

        [Header("Success")]
        [Tooltip("Set when the prank is done; also blocks doing it again. prank_<id>_done when empty")]
        public string doneFlag;
        [Tooltip("Consequence flags set at once")]
        public string[] consequenceFlags;
        [Tooltip("Flags set when the world reaction is over (the break goal, so the bell doesn't cut the reaction)")]
        public string[] flagsAfterReaction;
        [Tooltip("Counters increased by 1")]
        public string[] counters = { "pranks_done" };
        [Tooltip("Goes into the shared inventory; empty = nothing")]
        public ItemData rewardItem;
        [Tooltip("What the hero says when it works; a random one")]
        [TextArea(1, 2)] public string[] successLines;

        [Header("World reaction")]
        [Tooltip("Timeline played by the PrankTrigger's PlayableDirector, 5–15 s")]
        public TimelineAsset reactionTimeline;
        [Tooltip("NPCs closer than this react (look at the spot), m")]
        public float reactionRadius = 20f;

        public string Id => id;
        public string Title => title;
        public float ReactionRadius => reactionRadius;
        public string DoneFlag => string.IsNullOrEmpty(doneFlag) ? $"prank_{id}_done" : doneFlag;

        /// <summary>Hero, phase and flag conditions (not the item and not the watchers).</summary>
        public bool ConditionsMet(HeroId hero, WorldFlags flags, out string why)
        {
            why = null;
            if (flags != null && flags.GetFlag(DoneFlag)) { why = "already done"; return false; }
            if (heroes != null && heroes.Length > 0 && System.Array.IndexOf(heroes, hero) < 0) { why = $"not for {hero}"; return false; }
            if (phaseIds != null && phaseIds.Length > 0)
            {
                string phase = ServiceLocator.TryGet<IDayCycle>(out var day) ? day.CurrentPhase?.id : null;
                if (phase != null && System.Array.IndexOf(phaseIds, phase) < 0) { why = $"wrong phase {phase}"; return false; }
            }
            if (requiredFlags != null)
                foreach (var f in requiredFlags)
                    if (!string.IsNullOrEmpty(f) && (flags == null || !flags.GetFlag(f))) { why = $"needs {f}"; return false; }
            if (blockedByFlags != null && flags != null)
                foreach (var f in blockedByFlags)
                    if (!string.IsNullOrEmpty(f) && flags.GetFlag(f)) { why = $"blocked by {f}"; return false; }
            return true;
        }

        public static string Pick(string[] lines) =>
            lines == null || lines.Length == 0 ? null : lines[Random.Range(0, lines.Length)];
    }
}
