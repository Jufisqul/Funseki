using System;
using System.Collections.Generic;
using UnityEngine;

namespace Funseki.Core
{
    // World memory: string flags (prank_whistle_done) and int counters (pranks_done).
    // Ids are snake_case English. Serializes to JSON via JsonUtility for the save system.
    [Serializable]
    public class WorldFlags : ISerializationCallbackReceiver
    {
        [Serializable]
        struct FlagEntry { public string id; public bool value; }

        [Serializable]
        struct CounterEntry { public string id; public int value; }

        [SerializeField] List<FlagEntry> flagList = new();
        [SerializeField] List<CounterEntry> counterList = new();

        readonly Dictionary<string, bool> flags = new();
        readonly Dictionary<string, int> counters = new();

        // ---- flags
        public bool GetFlag(string id) => flags.TryGetValue(id, out var v) && v;

        public void SetFlag(string id, bool value = true)
        {
            if (flags.TryGetValue(id, out var old) && old == value) return;
            flags[id] = value;
            GameEvents.RaiseWorldFlagChanged(id, value);
        }

        // ---- counters
        public int GetCounter(string id) => counters.TryGetValue(id, out var v) ? v : 0;

        public void SetCounter(string id, int value)
        {
            if (counters.TryGetValue(id, out var old) && old == value) return;
            counters[id] = value;
            GameEvents.RaiseWorldCounterChanged(id, value);
        }

        public int AddCounter(string id, int delta = 1)
        {
            int v = GetCounter(id) + delta;
            SetCounter(id, v);
            return v;
        }

        public IReadOnlyDictionary<string, bool> Flags => flags;
        public IReadOnlyDictionary<string, int> Counters => counters;

        public void Clear()
        {
            flags.Clear();
            counters.Clear();
        }

        // ---- save / load
        public string ToJson() => JsonUtility.ToJson(this);

        public void LoadJson(string json)
        {
            Clear();
            JsonUtility.FromJsonOverwrite(json, this);
        }

        public void OnBeforeSerialize()
        {
            flagList.Clear();
            foreach (var kv in flags) flagList.Add(new FlagEntry { id = kv.Key, value = kv.Value });
            counterList.Clear();
            foreach (var kv in counters) counterList.Add(new CounterEntry { id = kv.Key, value = kv.Value });
        }

        public void OnAfterDeserialize()
        {
            flags.Clear();
            foreach (var e in flagList) flags[e.id] = e.value;
            counters.Clear();
            foreach (var e in counterList) counters[e.id] = e.value;
        }
    }
}
