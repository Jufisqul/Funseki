using System;
using System.Collections.Generic;
using Funseki.Core;
using UnityEngine;
using UnityEngine.Playables;

namespace Funseki.Lessons
{
    /// <summary>The three lesson results. There is no fail: a bad result is just another funny scene.</summary>
    public enum LessonOutcome
    {
        /// <summary>«Блестяще»</summary>
        Excellent,
        /// <summary>«Нормально»</summary>
        Normal,
        /// <summary>«Позорно»</summary>
        Shame,
    }

    /// <summary>Who says a lesson line. The mini-game maps the role to an object in its scene.</summary>
    public enum LessonRole
    {
        Teacher,
        /// <summary>The classmate who explains things (Шутник on Физра)</summary>
        Helper,
        /// <summary>The hero who leads this lesson (LessonData.leadHero)</summary>
        LeadHero,
        /// <summary>A random student of the class</summary>
        Student,
    }

    [Serializable]
    public class LessonLine
    {
        public LessonRole speaker;
        [TextArea(1, 3)] public string text;
        [Tooltip("Seconds before the next line")]
        [Min(0f)] public float duration = 2.5f;
    }

    // One outcome scene. Several scenes may share an outcome: the first one whose flag conditions hold is played
    // (for example «Блестяще» in the «без свистка» mode is its own scene above the ordinary one).
    [Serializable]
    public class LessonOutcomeScene
    {
        [Tooltip("Name for the designer and the logs")]
        public string name;
        public LessonOutcome outcome;
        [Tooltip("Played only if all these WorldFlags are set")]
        public string[] requireFlags = new string[0];
        [Tooltip("Played only if none of these WorldFlags is set")]
        public string[] forbidFlags = new string[0];
        [Tooltip("Gag id the mini-game plays during the scene (FizraMiniGame: anthem, nod, give_whistle, finger_anthem…). Empty = none")]
        public string gag;
        [Tooltip("Big caption over the scene: «Блестяще!»")]
        public string caption;
        public List<LessonLine> lines = new();
        [Tooltip("Optional Timeline played in the lesson stage during the scene")]
        public PlayableAsset timeline;
        [Tooltip("Minimum length of the scene, s (the lines may make it longer)")]
        [Min(0f)] public float minDuration = 4f;
        [Tooltip("WorldFlags set after this scene (consequences for the rest of the day)")]
        public string[] setFlags = new string[0];

        public bool Matches(LessonOutcome o, WorldFlags flags)
        {
            if (o != outcome) return false;
            foreach (var f in requireFlags) if (!string.IsNullOrEmpty(f) && (flags == null || !flags.GetFlag(f))) return false;
            foreach (var f in forbidFlags) if (!string.IsNullOrEmpty(f) && flags != null && flags.GetFlag(f)) return false;
            return true;
        }
    }

    // One lesson of the week (8 in the game). Assets/_Project/Data/Lessons/Lesson_<id>.asset.
    // LessonDirector plays it when a Lesson phase with this id starts: autosave → intro → mini-game → outcome scene →
    // result flags → the end-of-lesson bell.
    [CreateAssetMenu(fileName = "Lesson", menuName = "Funseki/Lessons/Lesson")]
    public class LessonData : ScriptableObject
    {
        [Tooltip("Same as DayPhase.lessonId and LessonEntrance: fizra")]
        public string id = "fizra";
        [Tooltip("«Физра»")]
        public string title = "Физра";
        [Tooltip("The hero who performs in the mini-game")]
        public HeroId leadHero = HeroId.Ryuta;
        [Tooltip("Mini-game prefab, spawned at the LessonStage with the same id")]
        public LessonMiniGame miniGamePrefab;

        [Header("Intro")]
        public List<LessonLine> intro = new();
        [Tooltip("Instead of the intro when the heroes are late (WorldFlag below): the teacher's gag")]
        public List<LessonLine> lateIntro = new();
        public string lateFlag = "lesson_late";

        [Header("Outcomes (share of commands done right, 0..1)")]
        [Range(0f, 1f)] public float excellentFrom = 0.9f;
        [Range(0f, 1f)] public float normalFrom = 0.5f;
        [Tooltip("First matching scene per outcome wins")]
        public List<LessonOutcomeScene> outcomes = new();
        [Tooltip("Captions of the outcomes: Блестяще, Нормально, Позорно")]
        public string[] outcomeNames = { "Блестяще", "Нормально", "Позорно" };

        [Header("Result")]
        [Tooltip("{0} = lesson id, {1} = day. Set as a flag (lesson done), as flag + _<outcome id> and as a counter " +
                 "(1 shame, 2 normal, 3 excellent)")]
        public string resultFlagFormat = "{0}_day{1}_result";
        [Tooltip("Seconds after the outcome scene before the bell")]
        [Min(0f)] public float bellDelay = 1f;

        public LessonOutcome OutcomeFor(float score) =>
            score >= excellentFrom ? LessonOutcome.Excellent : score >= normalFrom ? LessonOutcome.Normal : LessonOutcome.Shame;

        public LessonOutcomeScene SceneFor(LessonOutcome outcome, WorldFlags flags) =>
            outcomes.Find(s => s.Matches(outcome, flags));

        public string OutcomeName(LessonOutcome o) =>
            outcomeNames != null && (int)o < outcomeNames.Length ? outcomeNames[(int)o] : o.ToString();

        public static string OutcomeId(LessonOutcome o) => o switch
        {
            LessonOutcome.Excellent => "excellent",
            LessonOutcome.Normal => "normal",
            _ => "shame",
        };

        public static int OutcomeCounter(LessonOutcome o) => o switch
        {
            LessonOutcome.Excellent => 3,
            LessonOutcome.Normal => 2,
            _ => 1,
        };

        public string ResultFlag(int day) => string.Format(resultFlagFormat, id, day);
    }
}
