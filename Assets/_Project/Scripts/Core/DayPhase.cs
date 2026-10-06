using System;
using UnityEngine;

namespace Funseki.Core
{
    // One step of a school day (GDD 3.2). Lives in a DaySchedule asset (Funseki.DayCycle);
    // the type is in Core because GameEvents.OnPhaseStarted / OnPhaseEnded carry it to every module.
    public enum DayPhaseType { Morning, Break, Lesson, AfterSchool, StoryScene, Evening }

    /// <summary>Time-of-day icon shown by the HUD.</summary>
    public enum TimeOfDayIcon { Dawn, Sun, Sunset }

    public enum BellType
    {
        /// <summary>The break is over, go to the lesson.</summary>
        LessonStart,
        /// <summary>Repeated bell: the hero is still not in class.</summary>
        Second,
        /// <summary>The lesson is over.</summary>
        LessonEnd,
    }

    /// <summary>When the phase ends (for a break: when the bell rings). Whichever comes first.</summary>
    [Serializable]
    public class PhaseEndCondition
    {
        [Tooltip("WorldFlag that means the phase goal is done (the break goal, lesson finished, cutscene over). Empty = none")]
        public string goalFlag;
        [Tooltip("Seconds of play before the phase ends anyway. 0 = no limit (only the goal or code ends it)")]
        [Min(0f)] public float maxDuration;
    }

    [Serializable]
    public class DayPhase
    {
        [Tooltip("Unique within the day, snake_case: break_1, lesson_fizra. Used by saves and logs")]
        public string id;
        public DayPhaseType type;
        [Tooltip("GameState the game switches to when the phase starts")]
        public GameState state = GameState.Break;
        public TimeOfDayIcon timeOfDay = TimeOfDayIcon.Sun;
        [Tooltip("Lesson only: id of the lesson mini-game and of its LessonEntrance (fizra)")]
        public string lessonId;
        [Tooltip("Phase goal for the HUD")]
        [TextArea(1, 3)] public string objective;
        [Tooltip("Lesson only: HUD goal after the bell, while the hero walks to this lesson")]
        [TextArea(1, 3)] public string goToLessonObjective;
        [Tooltip("Bell / end condition")]
        public PhaseEndCondition end = new();

        public override string ToString() => $"{type} '{id}'";
    }

    // The day cycle service (DayCycleDirector in Slice scenes), via ServiceLocator.
    // Lessons, cutscenes and the save system talk to it through this interface.
    public interface IDayCycle
    {
        int Day { get; }
        /// <summary>The current phase; after a break bell it is still that break until the lesson starts.</summary>
        DayPhase CurrentPhase { get; }
        /// <summary>The break bell has rung and the day waits for the hero at the LessonEntrance.</summary>
        bool WaitingForLesson { get; }
        /// <summary>Ends the current phase now (a lesson mini-game or a cutscene finished).</summary>
        void CompletePhase();
        /// <summary>Break only, before its bell: the bell rings in this many seconds («Поймали» leaves 60 s).</summary>
        void SetTimeToBell(float seconds);
        string ToJson();
        void LoadJson(string json);
    }
}
