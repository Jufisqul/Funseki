using System;
using System.Collections.Generic;
using Funseki.Core;
using UnityEngine;

namespace Funseki.Heroes
{
    [Serializable]
    public class Rumor
    {
        [Tooltip("For the designer only")]
        public string note;
        [Tooltip("The phone conversation: a Funseki.Dialogue DialogueGraph asset")]
        public ScriptableObject dialogue;
        [Tooltip("All of these flags must be set")]
        public string[] requireFlags = Array.Empty<string>();
        [Tooltip("None of these flags may be set")]
        public string[] forbidFlags = Array.Empty<string>();
        [Tooltip("Set after the call, so the rumor is told once; empty = can repeat")]
        public string heardFlag;

        public bool IsAvailable(WorldFlags flags)
        {
            if (dialogue == null) return false;
            foreach (var f in requireFlags)
                if (!string.IsNullOrEmpty(f) && (flags == null || !flags.GetFlag(f))) return false;
            foreach (var f in forbidFlags)
                if (!string.IsNullOrEmpty(f) && flags != null && flags.GetFlag(f)) return false;
            return string.IsNullOrEmpty(heardFlag) || flags == null || !flags.GetFlag(heardFlag);
        }
    }

    // Rumors Рэй can learn by phone (GDD 5.2): hints about pranks and secrets. The first available one
    // from the top of the list is told, so put the most important rumors first.
    [CreateAssetMenu(fileName = "RumorSet_", menuName = "Funseki/Heroes/Rumor Set")]
    public class RumorSet : ScriptableObject
    {
        public List<Rumor> rumors = new();

        public Rumor Pick()
        {
            ServiceLocator.TryGet<WorldFlags>(out var flags);
            foreach (var r in rumors)
                if (r != null && r.IsAvailable(flags)) return r;
            return null;
        }
    }
}
