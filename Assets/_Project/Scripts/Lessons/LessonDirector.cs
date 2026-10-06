using System.Collections;
using System.Collections.Generic;
using Funseki.Core;
using TMPro;
using UnityEngine;

namespace Funseki.Lessons
{
    // Runs lessons in a day scene (object "Lessons" in Slice_Day1). When a Lesson phase starts (the hero walked into
    // the LessonEntrance after the bell, the game is already in the Lesson state):
    // autosave → the mini-game prefab spawns at its LessonStage, the heroes take their spots → intro lines (the late
    // gag when lesson_late is set) → the mini-game → outcome scene → flags <lessonId>_day<N>_result(_<outcome>)
    // → the phase goal flag, so the DayCycle rings the end-of-lesson bell and the next break starts. There is no fail.
    // If the phase is skipped (F9) mid-lesson, the lesson is torn down without a result.
    public class LessonDirector : MonoBehaviour
    {
        [SerializeField] List<LessonData> lessons = new();
        [Tooltip("Font of the lesson captions")]
        [SerializeField] TMP_FontAsset captionFont;

        DayPhase runningPhase;
        LessonMiniGame game;
        LessonStage stage;
        LessonCaption caption;
        Coroutine routine;

        void OnEnable()
        {
            GameEvents.OnPhaseStarted += OnPhaseStarted;
            GameEvents.OnPhaseEnded += OnPhaseEnded;
        }

        void OnDisable()
        {
            GameEvents.OnPhaseStarted -= OnPhaseStarted;
            GameEvents.OnPhaseEnded -= OnPhaseEnded;
            TearDown();
        }

        void OnDestroy() => caption?.Destroy();

        void Update() => caption?.Tick();

        void OnPhaseStarted(DayPhase phase)
        {
            if (phase == null || phase.type != DayPhaseType.Lesson) return;
            var data = lessons.Find(l => l != null && l.id == phase.lessonId);
            if (data == null)
            {
                Debug.LogWarning($"[Lesson] No LessonData '{phase.lessonId}' in the LessonDirector: the phase waits for F9/F10.", this);
                return;
            }
            if (data.miniGamePrefab == null)
            {
                Debug.LogWarning($"[Lesson] {data.name} has no mini-game prefab.", data);
                return;
            }
            if (runningPhase != null) TearDown();
            runningPhase = phase;
            routine = StartCoroutine(Run(phase, data));
        }

        void OnPhaseEnded(DayPhase phase)
        {
            // Our own completion clears runningPhase first; anything else (F9) aborts the lesson.
            if (runningPhase == null || phase != runningPhase) return;
            Debug.Log($"[Lesson] '{phase.lessonId}' skipped: the phase ended before the lesson did.");
            TearDown();
        }

        IEnumerator Run(DayPhase phase, LessonData data)
        {
            ServiceLocator.TryGet<WorldFlags>(out var flags);
            int day = ServiceLocator.TryGet<IDayCycle>(out var dayCycle) ? dayCycle.Day : 1;
            bool late = flags != null && !string.IsNullOrEmpty(data.lateFlag) && flags.GetFlag(data.lateFlag);

            GameEvents.RaiseAutosaveRequested($"lesson_{data.id}");

            stage = LessonStage.Find(data.id);
            if (stage == null) Debug.LogWarning($"[Lesson] No LessonStage '{data.id}' in the scene: the mini-game spawns at the LessonDirector.", this);
            var parent = stage != null ? stage.transform : transform;

            var leader = HeroService.CurrentObject;
            var performer = HeroService.GetHero(data.leadHero);
            if (performer == null) performer = leader;
            var others = new GameObject[3];
            for (int i = 0; i < 3; i++) others[i] = HeroService.GetHero((HeroId)i);
            if (stage != null) stage.Begin(leader, others);

            caption ??= new LessonCaption(transform, captionFont);
            game = Instantiate(data.miniGamePrefab, parent);
            game.name = data.miniGamePrefab.name;
            game.Setup(new LessonContext
            {
                Data = data, Stage = stage, Day = day, Late = late, Performer = performer, Flags = flags, Caption = caption,
            });
            Debug.Log($"[Lesson] '{data.id}' day {day}{(late ? " (late)" : "")}, performer {(performer != null ? performer.name : "none")}.");

            caption.Show(data.title, 2f);
            yield return Lines(late && data.lateIntro.Count > 0 ? data.lateIntro : data.intro);

            GameEvents.RaiseLessonStarted(data.id);
            game.Play();
            while (!game.IsFinished) yield return null;

            var outcome = data.OutcomeFor(game.Score);
            var scene = data.SceneFor(outcome, flags);
            Debug.Log($"[Lesson] '{data.id}': {game.Score:P0} -> {outcome}, scene '{scene?.name ?? "-"}'.");

            if (scene != null)
            {
                caption.Show(string.IsNullOrEmpty(scene.caption) ? data.OutcomeName(outcome) : scene.caption, 3f);
                game.PlayGag(scene.gag, outcome);
                if (stage != null) stage.PlayTimeline(scene.timeline);
                float started = 0f;
                yield return Lines(scene.lines, t => started += t);
                if (started < scene.minDuration) yield return Wait(scene.minDuration - started);
            }
            else caption.Show(data.OutcomeName(outcome), 3f);

            if (flags != null)
            {
                string result = data.ResultFlag(day);
                flags.SetFlag(result);
                flags.SetFlag($"{result}_{LessonData.OutcomeId(outcome)}");
                flags.SetCounter(result, LessonData.OutcomeCounter(outcome));
                if (scene != null) foreach (var f in scene.setFlags) if (!string.IsNullOrEmpty(f)) flags.SetFlag(f);
                Debug.Log($"[Lesson] Flags: {result}, {result}_{LessonData.OutcomeId(outcome)}, counter {result} = {LessonData.OutcomeCounter(outcome)}.");
            }
            GameEvents.RaiseLessonFinished(data.id, LessonData.OutcomeId(outcome));

            yield return Wait(data.bellDelay);

            routine = null;
            runningPhase = null;
            // The extras walk out of the lesson with the class instead of vanishing.
            game.Cleanup();
            var walk = LessonWalk.Find(data.id);
            if (walk != null) walk.TakeLeavers(game.Leavers);
            TearDown(cleaned: true);

            // The DayCycle ends the phase on its goal flag (or now, if it has none): end-of-lesson bell, next break.
            if (flags != null && !string.IsNullOrEmpty(phase.end.goalFlag)) flags.SetFlag(phase.end.goalFlag);
            else dayCycle?.CompletePhase();
        }

        IEnumerator Lines(List<LessonLine> lines, System.Action<float> spent = null)
        {
            if (lines == null) yield break;
            foreach (var line in lines)
            {
                if (line == null) continue;
                var who = game != null ? game.GetActor(line.speaker) : null;
                if (who != null && ServiceLocator.TryGet<IBarkService>(out var barks)) barks.Say(who, line.text);
                else Debug.Log($"[Lesson] {line.speaker}: {line.text}");
                yield return Wait(line.duration);
                spent?.Invoke(line.duration);
            }
        }

        // Game time that stands still during the pause or a conversation.
        static IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                if (!ServiceLocator.TryGet<GameStateMachine>(out var fsm) || fsm.Current == GameState.Lesson) t += Time.deltaTime;
                yield return null;
            }
        }

        void TearDown(bool cleaned = false)
        {
            if (routine != null) { StopCoroutine(routine); routine = null; }
            runningPhase = null;
            if (game != null)
            {
                if (!cleaned) game.Cleanup();
                Destroy(game.gameObject);
                game = null;
            }
            if (stage != null) { stage.End(); stage = null; }
            caption?.Hide();
        }
    }
}
