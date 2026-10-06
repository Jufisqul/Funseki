using System;
using Funseki.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Funseki.DayCycle
{
    // Runs a DaySchedule in a day scene (Slice_Day1): starts phases, rings the bell, tracks lateness.
    // Registered as IDayCycle. Bell rules (GDD 3.4):
    // - a break has no visible timer: the bell rings when its goal flag is set or after its max duration, whichever is first;
    // - after the bell the lesson starts when the hero enters the LessonEntrance of the next lesson;
    // - secondBellAfter seconds later: second bell + hint arrow; lateAfterBell seconds later: the late flag (no penalty).
    // Debug, Editor only: F9 jumps to the next phase, F10 sets the current phase goal flag.
    // Saved under "day" (ISaveable): on Continue the saved phase is re-entered instead of the first one.
    public class DayCycleDirector : MonoBehaviour, IDayCycle, ISaveable
    {
        [SerializeField] DaySchedule schedule;

        [Serializable]
        class DayState
        {
            public int day;
            public int phaseIndex = -1;
            public string phaseId;
            public float phaseTime;
            public bool bellRung;
            public float sinceBell;
            public bool secondBellRung;
            public bool late;
            public bool dayEnded;
            public string objective;
        }

        DayState s = new();
        GameStateMachine fsm;
        WorldFlags flags;
        bool advancePending;
        bool registered;
        bool restored;

        public string SaveKey => "day";
        public int Day => s.day;
        public DayPhase CurrentPhase => s.phaseIndex >= 0 && s.phaseIndex < schedule.phases.Count ? schedule.phases[s.phaseIndex] : null;
        public bool WaitingForLesson => s.bellRung && !s.dayEnded;

        DayPhase NextPhase => s.phaseIndex + 1 < schedule.phases.Count ? schedule.phases[s.phaseIndex + 1] : null;

        void Awake()
        {
            if (schedule == null) { Debug.LogError("[DayCycle] No DaySchedule assigned.", this); enabled = false; return; }
            ServiceLocator.Register<IDayCycle>(this);
            registered = true;
            // A pending saved day (Continue) arrives right here, before Start.
            SaveRegistry.Register(this);
        }

        void OnDestroy()
        {
            if (!registered) return;
            SaveRegistry.Unregister(this);
            if (ServiceLocator.TryGet<IDayCycle>(out var d) && ReferenceEquals(d, this))
                ServiceLocator.Unregister<IDayCycle>();
        }

        void OnEnable() => GameEvents.OnLessonEntranceReached += OnLessonEntranceReached;
        void OnDisable() => GameEvents.OnLessonEntranceReached -= OnLessonEntranceReached;

        void Start()
        {
            if (!ServiceLocator.TryGet(out fsm) || !ServiceLocator.TryGet(out flags))
            {
                Debug.LogWarning("[DayCycle] No GameStateMachine/WorldFlags: start Play from Bootstrap. The day does not run.");
                enabled = false;
                return;
            }
            if (restored) { Reenter(); return; }
            s.day = schedule.day;
            Debug.Log($"[DayCycle] Day {s.day} ({schedule.name}): {schedule.phases.Count} phases.");
            StartPhase(0);
        }

        void Update()
        {
            if (fsm == null || s.dayEnded) return;
            if (Application.isEditor) DebugKeys();

            if (advancePending)
            {
                if (IsBusy()) return;
                advancePending = false;
                Advance();
                return;
            }
            if (fsm.Current == GameState.Paused) return;

            var phase = CurrentPhase;
            if (phase == null) return;

            if (!s.bellRung)
            {
                s.phaseTime += Time.deltaTime;
                if (GoalDone(phase)) EndPhase("goal");
                else if (schedule.MaxDuration(phase) is var max && max > 0f && s.phaseTime >= max) EndPhase($"time {max:0.#} s");
                return;
            }

            // After the bell: the hero walks to class.
            s.sinceBell += Time.deltaTime;
            if (!s.secondBellRung && s.sinceBell >= schedule.secondBellAfter)
            {
                s.secondBellRung = true;
                Debug.Log($"[DayCycle] Second bell: the hero is still not at '{NextPhase?.lessonId}'.");
                GameEvents.RaiseBell(BellType.Second);
                RaiseHint();
            }
            if (!s.late && s.sinceBell > schedule.lateAfterBell)
            {
                s.late = true;
                Debug.Log($"[DayCycle] Late for '{NextPhase?.lessonId}': flag {schedule.lateFlag}.");
                if (!string.IsNullOrEmpty(schedule.lateFlag)) flags.SetFlag(schedule.lateFlag);
            }
        }

        // ---------------------------------------------------------------- IDayCycle

        public void CompletePhase()
        {
            if (fsm == null || s.dayEnded || s.bellRung) return;
            EndPhase("completed by code");
        }

        public void SetTimeToBell(float seconds)
        {
            var phase = CurrentPhase;
            if (fsm == null || s.dayEnded || s.bellRung || phase == null || phase.type != DayPhaseType.Break) return;
            float max = schedule.MaxDuration(phase);
            if (max <= 0f) return;
            s.phaseTime = max - Mathf.Max(0f, seconds);
            Debug.Log($"[DayCycle] Bell in {seconds:0} s ({phase}).");
        }

        public string ToJson()
        {
            s.phaseId = CurrentPhase?.id;
            return JsonUtility.ToJson(s);
        }

        // Restores the day and re-enters the saved phase (state, phase event, objective) without resetting its timers.
        public void LoadJson(string json)
        {
            var loaded = JsonUtility.FromJson<DayState>(json);
            if (loaded == null) return;
            int index = schedule.phases.FindIndex(p => p.id == loaded.phaseId);
            if (index >= 0) loaded.phaseIndex = index;
            s = loaded;
            advancePending = false;
            // Loaded before Start (Continue): Start re-enters the phase once the services are found.
            if (fsm == null) { restored = true; return; }
            Reenter();
        }

        void Reenter()
        {
            restored = false;
            if (s.dayEnded) { fsm.ChangeState(schedule.endState); SetObjective(s.objective); return; }
            var phase = CurrentPhase;
            if (phase == null) { StartPhase(0); return; }
            // Saved after the bell: the bell rings again right away, so the walk to class (doors, NPCs) replays.
            if (s.bellRung)
            {
                s.bellRung = false;
                s.secondBellRung = false;
                s.sinceBell = 0f;
                s.phaseTime = Mathf.Max(s.phaseTime, schedule.MaxDuration(phase));
            }
            if (phase.state != GameState.None) fsm.ChangeState(phase.state);
            Debug.Log($"[DayCycle] Loaded: {phase}, {s.phaseTime:0} s into it.");
            GameEvents.RaisePhaseStarted(phase);
            SetObjective(s.objective);
        }

        // ---------------------------------------------------------------- phases

        void StartPhase(int index)
        {
            s.phaseIndex = index;
            s.phaseTime = 0f;
            s.bellRung = false;
            s.sinceBell = 0f;
            s.secondBellRung = false;
            s.late = false;

            var phase = CurrentPhase;
            if (phase.state != GameState.None) fsm.ChangeState(phase.state);
            float max = schedule.MaxDuration(phase);
            Debug.Log($"[DayCycle] Phase started: {phase} ({phase.timeOfDay}), " +
                      $"ends on {(string.IsNullOrEmpty(phase.end.goalFlag) ? "-" : phase.end.goalFlag)} or {(max > 0f ? $"{max:0.#} s" : "no limit")}.");
            GameEvents.RaisePhaseStarted(phase);
            SetObjective(phase.objective);
        }

        void EndPhase(string reason)
        {
            var phase = CurrentPhase;
            Debug.Log($"[DayCycle] Phase ended: {phase} ({reason}).");
            GameEvents.RaisePhaseEnded(phase);

            switch (phase.type)
            {
                case DayPhaseType.Break:
                    RingLessonBell();
                    break;
                case DayPhaseType.Lesson:
                    Debug.Log("[DayCycle] Bell: lesson over.");
                    GameEvents.RaiseBell(BellType.LessonEnd);
                    RequestAdvance();
                    break;
                default:
                    RequestAdvance();
                    break;
            }
        }

        void RingLessonBell()
        {
            Debug.Log("[DayCycle] Bell: break over, everyone to class.");
            GameEvents.RaiseBell(BellType.LessonStart);

            var next = NextPhase;
            if (next == null || next.type != DayPhaseType.Lesson) { RequestAdvance(); return; }

            s.bellRung = true;
            s.sinceBell = 0f;
            if (!string.IsNullOrEmpty(schedule.lateFlag)) flags.SetFlag(schedule.lateFlag, false);
            SetObjective(string.IsNullOrEmpty(next.goToLessonObjective) ? next.objective : next.goToLessonObjective);
            if (LessonEntrance.Find(next.lessonId) == null)
                Debug.LogWarning($"[DayCycle] No LessonEntrance '{next.lessonId}' in the scene: press F9 to start the lesson.");

            // The hero may already be waiting at the door.
            var entrance = LessonEntrance.Find(next.lessonId);
            if (entrance != null && entrance.PlayerInside) OnLessonEntranceReached(next.lessonId);
        }

        void OnLessonEntranceReached(string lessonId)
        {
            if (!s.bellRung || s.dayEnded) return;
            var next = NextPhase;
            if (next == null || next.lessonId != lessonId) return;
            Debug.Log($"[DayCycle] The hero reached '{lessonId}' {s.sinceBell:0} s after the bell{(s.late ? " (late)" : "")}.");
            RequestAdvance();
        }

        void RequestAdvance()
        {
            if (IsBusy()) { advancePending = true; return; }
            Advance();
        }

        void Advance()
        {
            int next = s.phaseIndex + 1;
            if (next < schedule.phases.Count) { StartPhase(next); return; }

            s.dayEnded = true;
            s.bellRung = false;
            Debug.Log($"[DayCycle] Day {s.day} over -> {schedule.endState}.");
            SetObjective("");
            if (schedule.endState != GameState.None) fsm.ChangeState(schedule.endState);
            GameEvents.RaiseDayEnded(s.day);
        }

        // A conversation or the pause menu finishes first; the next phase starts right after.
        bool IsBusy() => fsm.Current == GameState.Dialogue || fsm.Current == GameState.Paused;

        bool GoalDone(DayPhase phase) => !string.IsNullOrEmpty(phase.end.goalFlag) && flags.GetFlag(phase.end.goalFlag);

        void SetObjective(string text)
        {
            text ??= "";
            s.objective = text;
            Debug.Log($"[DayCycle] Objective: {(text.Length > 0 ? text : "(none)")}");
            GameEvents.RaiseObjectiveChanged(text);
        }

        void RaiseHint()
        {
            var next = NextPhase;
            var entrance = next != null ? LessonEntrance.Find(next.lessonId) : null;
            if (entrance != null) GameEvents.RaiseLessonHint(next.lessonId, entrance.transform.position);
        }

        // ---------------------------------------------------------------- debug

        void DebugKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.f9Key.wasPressedThisFrame)
            {
                Debug.Log("[DayCycle] F9: next phase.");
                if (!s.bellRung) EndPhase("debug F9");
                if (s.bellRung) { s.bellRung = false; advancePending = false; Advance(); }
                else if (advancePending) { advancePending = false; Advance(); }
            }
            else if (kb.f10Key.wasPressedThisFrame)
            {
                var phase = CurrentPhase;
                if (s.bellRung || phase == null || string.IsNullOrEmpty(phase.end.goalFlag))
                    Debug.Log("[DayCycle] F10: the current phase has no goal flag to set.");
                else
                {
                    Debug.Log($"[DayCycle] F10: goal flag {phase.end.goalFlag} set.");
                    flags.SetFlag(phase.end.goalFlag);
                }
            }
        }
    }
}
