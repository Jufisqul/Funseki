using System;
using UnityEngine;

namespace Funseki.Core
{
    // The only bridge between modules: a module raises an event here, others subscribe.
    // Modules never reference each other directly. Add new events to this file, grouped by module.
    public static class GameEvents
    {
        // ---- Core
        /// <summary>(previous, current)</summary>
        public static event Action<GameState, GameState> OnGameStateChanged;
        /// <summary>(flag id, new value)</summary>
        public static event Action<string, bool> OnWorldFlagChanged;
        /// <summary>(counter id, new value)</summary>
        public static event Action<string, int> OnWorldCounterChanged;

        public static void RaiseGameStateChanged(GameState previous, GameState current) => OnGameStateChanged?.Invoke(previous, current);
        public static void RaiseWorldFlagChanged(string id, bool value) => OnWorldFlagChanged?.Invoke(id, value);
        public static void RaiseWorldCounterChanged(string id, int value) => OnWorldCounterChanged?.Invoke(id, value);

        // ---- Interaction
        /// <summary>(hero, target or null) — the object the hero is aimed at now (IInteractable and/or IItemTarget)</summary>
        public static event Action<GameObject, GameObject> OnInteractionTargetChanged;
        /// <summary>(hero, target) — after IInteractable.Interact on E</summary>
        public static event Action<GameObject, GameObject> OnInteracted;

        public static void RaiseInteractionTargetChanged(GameObject hero, GameObject target) => OnInteractionTargetChanged?.Invoke(hero, target);
        public static void RaiseInteracted(GameObject hero, GameObject target) => OnInteracted?.Invoke(hero, target);

        /// <summary>(hero, object, object id, action id) — a world object did something: tap_open, can_dropped, machine_jammed,
        /// machine_broken, locker_opened, poster_drawn, booklet_closed. For «мир помнит», telemetry and pranks.</summary>
        public static event Action<GameObject, GameObject, string, string> OnObjectUsed;

        public static void RaiseObjectUsed(GameObject hero, GameObject obj, string objectId, string action) =>
            OnObjectUsed?.Invoke(hero, obj, objectId, action);

        // ---- Inventory
        /// <summary>(item) — picked up into the shared inventory</summary>
        public static event Action<ItemData> OnItemAdded;
        /// <summary>(item)</summary>
        public static event Action<ItemData> OnItemRemoved;
        /// <summary>(item or null for empty hands) — the item in the hero's hand changed</summary>
        public static event Action<ItemData> OnSelectedItemChanged;
        /// <summary>(selected item) — the player scrolled the inventory, even if the selection stayed the same</summary>
        public static event Action<ItemData> OnItemCycled;
        /// <summary>(hero, item, target, primary, worked) — LMB/RMB with an item on a target</summary>
        public static event Action<GameObject, ItemData, GameObject, bool, bool> OnItemUsed;

        public static void RaiseItemAdded(ItemData item) => OnItemAdded?.Invoke(item);
        public static void RaiseItemRemoved(ItemData item) => OnItemRemoved?.Invoke(item);
        public static void RaiseSelectedItemChanged(ItemData item) => OnSelectedItemChanged?.Invoke(item);
        public static void RaiseItemCycled(ItemData selected) => OnItemCycled?.Invoke(selected);
        public static void RaiseItemUsed(GameObject hero, ItemData item, GameObject target, bool primary, bool worked) =>
            OnItemUsed?.Invoke(hero, item, target, primary, worked);

        // ---- Hero lines
        /// <summary>(hero, text) — a short line the hero says out loud ("Не сработает")</summary>
        public static event Action<GameObject, string> OnHeroBark;

        public static void RaiseHeroBark(GameObject hero, string text) => OnHeroBark?.Invoke(hero, text);

        // ---- Dialogue
        /// <summary>(npc, hero) — a conversation opened; the game is in the Dialogue state</summary>
        public static event Action<GameObject, GameObject> OnDialogueStarted;
        /// <summary>(npc, hero) — the conversation closed</summary>
        public static event Action<GameObject, GameObject> OnDialogueEnded;
        /// <summary>(item) — something (a dialogue action, a reward) hands the heroes an item; Inventory adds it</summary>
        public static event Action<ItemData> OnItemGiven;

        public static void RaiseDialogueStarted(GameObject npc, GameObject hero) => OnDialogueStarted?.Invoke(npc, hero);
        public static void RaiseDialogueEnded(GameObject npc, GameObject hero) => OnDialogueEnded?.Invoke(npc, hero);
        public static void RaiseItemGiven(ItemData item) => OnItemGiven?.Invoke(item);

        // ---- DayCycle
        /// <summary>(phase) — a day phase began; the game is already in phase.state</summary>
        public static event Action<DayPhase> OnPhaseStarted;
        /// <summary>(phase) — a phase finished (for a break: at its bell)</summary>
        public static event Action<DayPhase> OnPhaseEnded;
        /// <summary>(type) — the school bell rang; NPCs head to class on LessonStart</summary>
        public static event Action<BellType> OnBell;
        /// <summary>(text, may be empty) — the current goal for the HUD changed</summary>
        public static event Action<string> OnObjectiveChanged;
        /// <summary>(lesson id, entrance position) — show the hint arrow to the classroom (second bell)</summary>
        public static event Action<string, Vector3> OnLessonHint;
        /// <summary>(lesson id) — the hero stepped into a LessonEntrance</summary>
        public static event Action<string> OnLessonEntranceReached;
        /// <summary>(day) — the last phase of the schedule finished</summary>
        public static event Action<int> OnDayEnded;

        public static void RaisePhaseStarted(DayPhase phase) => OnPhaseStarted?.Invoke(phase);
        public static void RaisePhaseEnded(DayPhase phase) => OnPhaseEnded?.Invoke(phase);
        public static void RaiseBell(BellType type) => OnBell?.Invoke(type);
        public static void RaiseObjectiveChanged(string text) => OnObjectiveChanged?.Invoke(text);
        public static void RaiseLessonHint(string lessonId, Vector3 position) => OnLessonHint?.Invoke(lessonId, position);
        public static void RaiseLessonEntranceReached(string lessonId) => OnLessonEntranceReached?.Invoke(lessonId);
        public static void RaiseDayEnded(int day) => OnDayEnded?.Invoke(day);

        // ---- Heroes
        /// <summary>(previous leader or null, new leader, hand-over time in s) — control and camera move to another hero</summary>
        public static event Action<GameObject, GameObject, float> OnHeroSwitched;
        /// <summary>(hero, hero id) — the hero's unique action (Q) fired</summary>
        public static event Action<GameObject, HeroId> OnHeroAbilityUsed;
        /// <summary>(npc, hero) — Кайто kicked this NPC; the NPC makes its own reaction</summary>
        public static event Action<GameObject, GameObject> OnKicked;
        /// <summary>(hero, witness, reason id, amount) — a teacher saw mischief; feeds the «Шум» meter</summary>
        public static event Action<GameObject, GameObject, string, int> OnNoiseMade;

        public static void RaiseHeroSwitched(GameObject previous, GameObject current, float time) => OnHeroSwitched?.Invoke(previous, current, time);
        public static void RaiseHeroAbilityUsed(GameObject hero, HeroId id) => OnHeroAbilityUsed?.Invoke(hero, id);
        public static void RaiseKicked(GameObject npc, GameObject hero) => OnKicked?.Invoke(npc, hero);
        public static void RaiseNoiseMade(GameObject hero, GameObject witness, string reason, int amount) =>
            OnNoiseMade?.Invoke(hero, witness, reason, amount);

        // ---- Pranks
        /// <summary>(prank, position) — a prank succeeded; NPCs within prank.ReactionRadius react, flags are already set</summary>
        public static event Action<IPrank, Vector3> OnPrankDone;
        /// <summary>(value 0..max, stage) — the «Шум» meter changed</summary>
        public static event Action<float, NoiseStage> OnNoiseChanged;
        /// <summary>(hero, witness or null, caught scene asset or null for the default) — the hero is caught; the «Поймали» scene starts</summary>
        public static event Action<GameObject, GameObject, ScriptableObject> OnCaught;
        /// <summary>(hero) — the «Поймали» scene is over, the game is back in the break</summary>
        public static event Action<GameObject> OnCaughtEnded;
        /// <summary>(entry) — a new line in the «Журнал недели»</summary>
        public static event Action<JournalEntry> OnJournalEntryAdded;

        public static void RaisePrankDone(IPrank prank, Vector3 position) => OnPrankDone?.Invoke(prank, position);
        public static void RaiseNoiseChanged(float value, NoiseStage stage) => OnNoiseChanged?.Invoke(value, stage);
        public static void RaiseCaught(GameObject hero, GameObject witness, ScriptableObject scene) => OnCaught?.Invoke(hero, witness, scene);
        public static void RaiseCaughtEnded(GameObject hero) => OnCaughtEnded?.Invoke(hero);
        public static void RaiseJournalEntryAdded(JournalEntry entry) => OnJournalEntryAdded?.Invoke(entry);

        // ---- Lessons
        /// <summary>(lesson id) — a lesson mini-game began (after the autosave and the teacher's intro)</summary>
        public static event Action<string> OnLessonStarted;
        /// <summary>(lesson id, outcome id: excellent / normal / shame) — the lesson is over, its result flags are set</summary>
        public static event Action<string, string> OnLessonFinished;

        public static void RaiseLessonStarted(string lessonId) => OnLessonStarted?.Invoke(lessonId);
        public static void RaiseLessonFinished(string lessonId, string outcome) => OnLessonFinished?.Invoke(lessonId, outcome);

        // ---- Save
        /// <summary>(reason, e.g. lesson_fizra) — write the game now (autosave before a lesson, after an event)</summary>
        public static event Action<string> OnAutosaveRequested;
        /// <summary>(file path) — the game state was written</summary>
        public static event Action<string> OnGameSaved;
        /// <summary>(file path) — a save was read into the services; the scene reloads right after</summary>
        public static event Action<string> OnGameLoaded;

        public static void RaiseAutosaveRequested(string reason) => OnAutosaveRequested?.Invoke(reason);
        public static void RaiseGameSaved(string path) => OnGameSaved?.Invoke(path);
        public static void RaiseGameLoaded(string path) => OnGameLoaded?.Invoke(path);

        // Static events survive Play sessions when domain reload is off; drop stale subscribers.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAll()
        {
            OnGameStateChanged = null;
            OnWorldFlagChanged = null;
            OnWorldCounterChanged = null;
            OnInteractionTargetChanged = null;
            OnInteracted = null;
            OnObjectUsed = null;
            OnAutosaveRequested = null;
            OnGameSaved = null;
            OnGameLoaded = null;
            OnLessonStarted = null;
            OnLessonFinished = null;
            OnItemAdded = null;
            OnItemRemoved = null;
            OnSelectedItemChanged = null;
            OnItemCycled = null;
            OnItemUsed = null;
            OnHeroBark = null;
            OnDialogueStarted = null;
            OnDialogueEnded = null;
            OnItemGiven = null;
            OnPhaseStarted = null;
            OnPhaseEnded = null;
            OnBell = null;
            OnObjectiveChanged = null;
            OnLessonHint = null;
            OnLessonEntranceReached = null;
            OnDayEnded = null;
            OnHeroSwitched = null;
            OnHeroAbilityUsed = null;
            OnKicked = null;
            OnNoiseMade = null;
            OnPrankDone = null;
            OnNoiseChanged = null;
            OnCaught = null;
            OnCaughtEnded = null;
            OnJournalEntryAdded = null;
        }
    }
}
