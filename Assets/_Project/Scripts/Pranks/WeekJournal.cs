using System;
using System.Collections.Generic;
using Funseki.Core;
using UnityEngine;

namespace Funseki.Pranks
{
    // «Журнал недели» (GDD 5.4): the pranks done and secrets found this week, in order. Registered as IWeekJournal
    // and saved under "journal". Child of [Bootstrap], so it lives through scene loads. The UI comes in task 12.
    [DefaultExecutionOrder(-900)]
    public class WeekJournal : MonoBehaviour, IWeekJournal, ISaveable
    {
        [Serializable]
        class Data { public List<JournalEntry> entries = new(); }

        Data data = new();
        bool owner;

        public IReadOnlyList<JournalEntry> Entries => data.entries;
        public string SaveKey => "journal";

        void Awake()
        {
            if (ServiceLocator.IsRegistered<IWeekJournal>()) { Destroy(gameObject); return; }
            owner = true;
            ServiceLocator.Register<IWeekJournal>(this);
            SaveRegistry.Register(this);
        }

        void OnDestroy()
        {
            if (!owner) return;
            SaveRegistry.Unregister(this);
            if (ServiceLocator.TryGet<IWeekJournal>(out var j) && ReferenceEquals(j, this)) ServiceLocator.Unregister<IWeekJournal>();
        }

        public bool Has(string id) => data.entries.Exists(e => e.id == id);

        public bool Add(JournalEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.id) || Has(entry.id)) return false;
            data.entries.Add(entry);
            Debug.Log($"[Journal] + {entry.kind} «{entry.title}» (day {entry.day}).");
            GameEvents.RaiseJournalEntryAdded(entry);
            return true;
        }

        public string ToJson() => JsonUtility.ToJson(data);

        public void LoadJson(string json)
        {
            data = JsonUtility.FromJson<Data>(json) ?? new Data();
            data.entries ??= new List<JournalEntry>();
            Debug.Log($"[Journal] Loaded {data.entries.Count} entr(ies).");
        }
    }
}
