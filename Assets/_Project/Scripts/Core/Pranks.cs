using System;
using System.Collections.Generic;
using UnityEngine;

namespace Funseki.Core
{
    // Shared types of the prank system (GDD 5.4). Implemented by Funseki.Pranks; here because GameEvents carries them
    // and NPC, UI and lessons read them without referencing the Pranks module.

    /// <summary>A prank as other modules see it (Funseki.Pranks.PrankData).</summary>
    public interface IPrank
    {
        /// <summary>snake_case id: whistle</summary>
        string Id { get; }
        /// <summary>Title in the week journal: «Свистать всех наверх»</summary>
        string Title { get; }
        /// <summary>NPCs closer than this to the prank react to it, m</summary>
        float ReactionRadius { get; }
    }

    /// <summary>Stages of the «Шум» meter.</summary>
    public enum NoiseStage
    {
        /// <summary>«Тихо»</summary>
        Quiet,
        /// <summary>«Подозрительно»</summary>
        Suspicious,
        /// <summary>«Поймали»</summary>
        Caught,
    }

    // The «Шум» meter (Funseki.Pranks.NoiseMeter), via ServiceLocator. Noise comes in through GameEvents.OnNoiseMade.
    public interface INoiseMeter
    {
        /// <summary>0..Max</summary>
        float Value { get; }
        float Max { get; }
        NoiseStage Stage { get; }
        /// <summary>Is any watcher (a teacher) looking at the controlled hero right now?</summary>
        bool HeroSeen { get; }
        /// <summary>Is the controlled hero inside a hiding zone («Укрытие»)?</summary>
        bool HeroHidden { get; }
        void ResetNoise();
    }

    // Something Кайто's kick can hit besides an NPC (the vending machine). Found like INpc, through colliders
    // with GetComponentInParent; the kick raises GameEvents.OnKicked(object, hero) for it.
    public interface IKickable { }

    public enum JournalEntryKind { Prank, Secret }

    // One line of the «Журнал недели»: a prank done or a secret found. Title and caption are copied at the moment
    // the entry is made, so the journal reads the same even if the data asset changes later.
    [Serializable]
    public class JournalEntry
    {
        public string id;
        public JournalEntryKind kind;
        public string title;
        [TextArea(1, 3)] public string caption;
        public int day;
        /// <summary>Which hero did it (HeroId as int)</summary>
        public int hero;
    }

    // «Журнал недели» (Funseki.Pranks.WeekJournal), via ServiceLocator. Saved with the game; UI comes in task 12.
    public interface IWeekJournal
    {
        IReadOnlyList<JournalEntry> Entries { get; }
        bool Has(string id);
        /// <summary>False if an entry with this id is already there.</summary>
        bool Add(JournalEntry entry);
    }
}
