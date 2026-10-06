using System;
using System.Collections.Generic;
using Funseki.Core;
using UnityEngine;

namespace Funseki.Dialogue
{
    [Serializable]
    public class BarkLine
    {
        [TextArea(1, 2)] public string text;
        [Tooltip("The line can be picked only if all of these pass")]
        public List<DialogueCondition> conditions = new();
    }

    // Lines a character may blurt out (GDD 5.3). Picked at random among the ones whose conditions pass,
    // never the same line twice in a row.
    [CreateAssetMenu(fileName = "BarkSet_", menuName = "Funseki/Dialogue/Bark Set")]
    public class BarkSet : ScriptableObject
    {
        public List<BarkLine> lines = new();

        [Header("BarkTrigger")]
        [Tooltip("The character speaks when the hero comes this close, m")]
        public float triggerRadius = 3f;
        [Tooltip("Minimum time between two barks of one character, s")]
        public float cooldown = 8f;
        [Tooltip("Game states in which the character barks")]
        public GameState[] allowedStates = { GameState.Break };

        [NonSerialized] int lastIndex = -1;
        readonly List<int> candidates = new();

        public bool IsAllowed(GameState state) => Array.IndexOf(allowedStates, state) >= 0;

        // null when no line passes its conditions.
        public string Pick(HeroId hero)
        {
            candidates.Clear();
            for (int i = 0; i < lines.Count; i++)
                if (lines[i] != null && !string.IsNullOrEmpty(lines[i].text) && DialogueCondition.AllMet(lines[i].conditions, hero))
                    candidates.Add(i);
            if (candidates.Count == 0) return null;
            if (candidates.Count > 1) candidates.Remove(lastIndex);
            lastIndex = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            return lines[lastIndex].text;
        }
    }
}
