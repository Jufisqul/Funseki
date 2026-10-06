using System.Collections.Generic;
using Funseki.Core;
using UnityEngine;

namespace Funseki.Lessons
{
    // What a mini-game gets from the LessonDirector.
    public class LessonContext
    {
        public LessonData Data;
        public LessonStage Stage;
        public int Day;
        /// <summary>The heroes came after the late flag was set: the teacher opened with a gag.</summary>
        public bool Late;
        /// <summary>The hero who performs (LessonData.leadHero, or the party leader if that hero is not in the scene). May be null.</summary>
        public GameObject Performer;
        public WorldFlags Flags;
        public LessonCaption Caption;

        public bool GetFlag(string id) => Flags != null && !string.IsNullOrEmpty(id) && Flags.GetFlag(id);
    }

    // Base of every lesson mini-game prefab (FizraMiniGame…). Life cycle, driven by LessonDirector:
    // Setup (the lesson stage is ready, the intro is about to play) → Play → Finish(score) → PlayGag (outcome scene)
    // → Cleanup → destroyed. The mini-game must not change the GameState or the day phase itself.
    public abstract class LessonMiniGame : MonoBehaviour
    {
        public LessonContext Context { get; private set; }
        public bool IsFinished { get; private set; }
        /// <summary>Share of the lesson done right, 0..1; LessonData turns it into an outcome.</summary>
        public float Score { get; private set; }

        public void Setup(LessonContext context)
        {
            Context = context;
            IsFinished = false;
            OnSetup();
        }

        /// <summary>Cameras, cast and UI on; called before the intro lines.</summary>
        protected virtual void OnSetup() { }

        /// <summary>The intro is over: run the game and call Finish when it ends.</summary>
        public abstract void Play();

        /// <summary>The object that says lines for this role (a teacher capsule, the helper on the bench…).</summary>
        public virtual GameObject GetActor(LessonRole role) => role == LessonRole.LeadHero ? Context?.Performer : null;

        /// <summary>The outcome scene asks for a gag (LessonOutcomeScene.gag): an animation, a sound, a walk-off.</summary>
        public virtual void PlayGag(string gag, LessonOutcome outcome) { }

        /// <summary>Extras of the mini-game (the students in line) that walk out of the lesson instead of vanishing.
        /// Taken over by the lesson's LessonWalk before the prefab is destroyed.</summary>
        public virtual IEnumerable<Transform> Leavers => System.Array.Empty<Transform>();

        /// <summary>Last call before the prefab is destroyed: give back the heroes, switch the cameras off.</summary>
        public virtual void Cleanup() { }

        protected void Finish(float score)
        {
            if (IsFinished) return;
            Score = Mathf.Clamp01(score);
            IsFinished = true;
            Debug.Log($"[Lesson] {name}: finished with {Score:P0}.", this);
        }

        protected void Say(LessonRole role, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Say(GetActor(role), text);
        }

        protected static void Say(GameObject speaker, string text)
        {
            if (speaker == null || string.IsNullOrEmpty(text)) return;
            if (ServiceLocator.TryGet<IBarkService>(out var barks)) barks.Say(speaker, text);
            else Debug.Log($"[Lesson] {speaker.name}: {text}");
        }

        /// <summary>The lesson runs only in the Lesson state (a pause or a conversation freezes it).</summary>
        protected static bool Running =>
            !ServiceLocator.TryGet<GameStateMachine>(out var fsm) || fsm.Current == GameState.Lesson;
    }
}
