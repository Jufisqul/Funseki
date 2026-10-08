using System;
using System.Collections;
using System.Collections.Generic;
using Funseki.Core;
using UnityEngine;

namespace Funseki.Quests
{
    // The quest log (task 17): starts quests by their start flag, closes steps when their flags are set, hands out
    // rewards and keeps the HUD goal on the active step of the main quest (GameEvents.OnObjectiveChanged).
    // Child of [Bootstrap], registered as IQuestLog and saved under "quests".
    // The day schedule also sets the HUD goal at each phase start; while a main quest is active its step wins
    // (re-sent one frame after the phase start). When the main quest is done the goal goes back to the phase's own.
    [DefaultExecutionOrder(-900)]
    public class QuestService : MonoBehaviour, IQuestLog, ISaveable
    {
        [SerializeField] QuestSettings settings;

        [Serializable]
        class Entry { public string id; public QuestStatus status; public int step; }

        [Serializable]
        class Data { public List<Entry> quests = new(); }

        Data data = new();
        readonly List<QuestInfo> infos = new();
        bool owner, evaluating, again;
        string shownObjective;

        public string SaveKey => "quests";

        public IReadOnlyList<QuestInfo> Quests
        {
            get
            {
                infos.Clear();
                foreach (var q in settings.quests)
                    if (q != null) infos.Add(Info(q));
                return infos;
            }
        }

        void Awake()
        {
            if (ServiceLocator.IsRegistered<IQuestLog>()) { Destroy(gameObject); return; }
            if (settings == null) { Debug.LogError("[Quests] No QuestSettings assigned.", this); enabled = false; return; }
            owner = true;
            ServiceLocator.Register<IQuestLog>(this);
            SaveRegistry.Register(this);
        }

        void OnDestroy()
        {
            if (!owner) return;
            SaveRegistry.Unregister(this);
            if (ServiceLocator.TryGet<IQuestLog>(out var q) && ReferenceEquals(q, this)) ServiceLocator.Unregister<IQuestLog>();
        }

        void OnEnable()
        {
            GameEvents.OnWorldFlagChanged += OnWorldFlagChanged;
            GameEvents.OnPhaseStarted += OnPhaseStarted;
        }

        void OnDisable()
        {
            GameEvents.OnWorldFlagChanged -= OnWorldFlagChanged;
            GameEvents.OnPhaseStarted -= OnPhaseStarted;
        }

        void Start() => Evaluate();

        // ---------------------------------------------------------------- IQuestLog

        public bool TryGet(string questId, out QuestInfo quest)
        {
            var q = Find(questId);
            quest = q != null ? Info(q) : default;
            return q != null;
        }

        public bool StartQuest(string questId)
        {
            var q = Find(questId);
            if (q == null) { Debug.LogWarning($"[Quests] No quest '{questId}' in QuestSettings."); return false; }
            if (State(q).status != QuestStatus.NotStarted) return false;
            Begin(q);
            Evaluate();
            return true;
        }

        // ---------------------------------------------------------------- save

        public string ToJson() => JsonUtility.ToJson(data);

        public void LoadJson(string json)
        {
            data = JsonUtility.FromJson<Data>(json) ?? new Data();
            data.quests ??= new List<Entry>();
            shownObjective = null;
            Debug.Log($"[Quests] Loaded {data.quests.Count} quest state(s).");
            // A loaded game may already have flags that move a step on; a fresh game (empty) changes nothing.
            if (owner && data.quests.Count > 0) Evaluate();
        }

        // ---------------------------------------------------------------- flow

        void OnWorldFlagChanged(string id, bool value)
        {
            if (value) Evaluate();
        }

        // The phase start has just set its own goal; the main quest's step goes back on top of it.
        void OnPhaseStarted(DayPhase phase)
        {
            shownObjective = null;
            if (isActiveAndEnabled) StartCoroutine(ShowObjectiveNextFrame());
        }

        IEnumerator ShowObjectiveNextFrame()
        {
            yield return null;
            UpdateObjective();
        }

        void Evaluate()
        {
            if (!ServiceLocator.TryGet<WorldFlags>(out var flags)) return;
            // Rewards set flags, which call back here; loop instead of recursing.
            if (evaluating) { again = true; return; }
            evaluating = true;
            try
            {
                do
                {
                    again = false;
                    foreach (var q in settings.quests)
                    {
                        if (q == null) continue;
                        var s = State(q);
                        if (s.status == QuestStatus.NotStarted && !string.IsNullOrEmpty(q.startFlag) && flags.GetFlag(q.startFlag))
                            Begin(q);
                        while (s.status == QuestStatus.Active && StepDone(q, s.step, flags))
                            CompleteStep(q, s, flags);
                    }
                } while (again);
            }
            finally
            {
                evaluating = false;
            }
            UpdateObjective();
        }

        void Begin(QuestData q)
        {
            var s = State(q);
            s.status = q.steps.Count > 0 ? QuestStatus.Active : QuestStatus.Done;
            s.step = 0;
            Debug.Log($"[Quests] Started {q.id} «{q.title}»: {Objective(q, 0)}");
            GameEvents.RaiseQuestStarted(q.id);
            Autosave(q.id);
        }

        void CompleteStep(QuestData q, Entry s, WorldFlags flags)
        {
            var step = q.steps[s.step];
            foreach (var f in step.rewardFlags)
                if (!string.IsNullOrEmpty(f)) flags.SetFlag(f);
            foreach (var item in step.rewardItems)
                if (item != null) GameEvents.RaiseItemGiven(item);

            s.step++;
            if (s.step >= q.steps.Count)
            {
                s.status = QuestStatus.Done;
                Debug.Log($"[Quests] Done {q.id} «{q.title}».");
                flags.SetFlag(q.DoneFlag);
                GameEvents.RaiseQuestCompleted(q.id);
            }
            else
            {
                Debug.Log($"[Quests] {q.id}: step {s.step + 1}/{q.steps.Count} — {Objective(q, s.step)}");
                GameEvents.RaiseQuestStepChanged(q.id, s.step);
            }
            Autosave(q.id);
        }

        static bool StepDone(QuestData q, int index, WorldFlags flags)
        {
            if (index < 0 || index >= q.steps.Count) return false;
            var step = q.steps[index];
            if (step.doneFlags.Count == 0) return false;
            foreach (var f in step.doneFlags)
                if (!string.IsNullOrEmpty(f) && !flags.GetFlag(f)) return false;
            return true;
        }

        // The HUD goal: the active main quest's step, or the current phase's own goal when no main quest runs.
        void UpdateObjective()
        {
            string text = null;
            foreach (var q in settings.quests)
            {
                if (q == null || q.kind != QuestKind.Main) continue;
                var s = State(q);
                if (s.status == QuestStatus.Active) { text = Objective(q, s.step); break; }
            }
            if (text == null)
            {
                // Nothing of ours to show: hand the goal back to the day only if we were the ones showing it.
                if (shownObjective == null) return;
                text = ServiceLocator.TryGet<IDayCycle>(out var day) && day.CurrentPhase != null ? day.CurrentPhase.objective ?? "" : "";
                shownObjective = null;
                GameEvents.RaiseObjectiveChanged(text);
                return;
            }
            if (text == shownObjective) return;
            shownObjective = text;
            GameEvents.RaiseObjectiveChanged(text);
        }

        void Autosave(string questId)
        {
            if (settings.autosaveOnProgress) GameEvents.RaiseAutosaveRequested($"quest_{questId}");
        }

        // ---------------------------------------------------------------- data

        QuestData Find(string id)
        {
            foreach (var q in settings.quests)
                if (q != null && q.id == id) return q;
            return null;
        }

        Entry State(QuestData q)
        {
            var e = data.quests.Find(x => x.id == q.id);
            if (e != null) return e;
            e = new Entry { id = q.id, status = QuestStatus.NotStarted };
            data.quests.Add(e);
            return e;
        }

        QuestInfo Info(QuestData q)
        {
            var s = State(q);
            string objective = s.status == QuestStatus.Active ? Objective(q, s.step) : "";
            return new QuestInfo(q.id, q.title, q.kind, s.status, s.step, q.steps.Count, objective);
        }

        static string Objective(QuestData q, int step) =>
            step >= 0 && step < q.steps.Count ? q.steps[step].objective ?? "" : "";
    }
}
