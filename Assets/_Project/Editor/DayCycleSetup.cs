using System.Collections.Generic;
using System.IO;
using Funseki.Core;
using Funseki.DayCycle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Funseki.EditorTools
{
    // Tools > Funseki > DayCycle: the Day 1 slice schedule and its scene objects.
    // - DaySchedule_Day1_Slice.asset is created if missing (an existing asset is never overwritten).
    // - In Slice_Day1: DayCycle (DayCycleDirector) and LessonEntrance_fizra replace the old CutscenePlaceholder.
    // Re-runnable: the objects this tool owns are recreated.
    public static class DayCycleSetup
    {
        public const string SchedulePath = "Assets/_Project/Data/DayCycle/DaySchedule_Day1_Slice.asset";

        // PE is in the gym (zone gym, plan x -22..0, y 16..32), just inside its east double door from the west
        // corridor. The gym is locked until day 2 in the SchoolLayout; on day 1 Funseki.Lessons.LessonWalk unlocks
        // and opens the way (courtyard west door, gym east door) at the bell.
        internal static readonly Vector3 FizraEntrancePos = new(-3.5f, 1.5f, -24f);
        internal static readonly Vector3 FizraEntranceSize = new(6f, 3f, 12f);

        internal static readonly string[] OwnedRoots = { "DayCycle", "LessonEntrance_fizra", "CutscenePlaceholder" };

        [MenuItem("Tools/Funseki/DayCycle/Setup Day 1 schedule in Slice_Day1")]
        public static void Setup()
        {
            var active = SceneManager.GetActiveScene();
            var slice = SceneManager.GetSceneByPath(CoreScenesSetup.SlicePath);
            bool opened = !slice.isLoaded;
            if (opened) slice = EditorSceneManager.OpenScene(CoreScenesSetup.SlicePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(slice);
            var keep = LayoutKeeper.Capture(slice);

            foreach (var go in slice.GetRootGameObjects())
                if (System.Array.IndexOf(OwnedRoots, go.name) >= 0) Object.DestroyImmediate(go);
            AddToActiveScene();

            keep.Restore();
            EditorSceneManager.MarkSceneDirty(slice);
            EditorSceneManager.SaveScene(slice);
            if (opened) EditorSceneManager.CloseScene(slice, true);
            else if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            Debug.Log("[DayCycleSetup] Slice_Day1: DayCycle + LessonEntrance_fizra (courtyard) added.");
        }

        // Adds the director and the lesson entrance to the active scene (Build Slice_Day1 calls this too).
        internal static void AddToActiveScene()
        {
            var director = new GameObject("DayCycle").AddComponent<DayCycleDirector>();
            var so = new SerializedObject(director);
            so.FindProperty("schedule").objectReferenceValue = EnsureSchedule();
            so.ApplyModifiedPropertiesWithoutUndo();

            var entrance = new GameObject("LessonEntrance_fizra");
            entrance.transform.position = FizraEntrancePos;
            var box = entrance.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = FizraEntranceSize;
            var rb = entrance.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            var le = entrance.AddComponent<LessonEntrance>();
            var leSo = new SerializedObject(le);
            leSo.FindProperty("lessonId").stringValue = "fizra";
            leSo.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static DaySchedule EnsureSchedule()
        {
            var s = AssetDatabase.LoadAssetAtPath<DaySchedule>(SchedulePath);
            if (s != null) return s;

            Directory.CreateDirectory(Path.GetDirectoryName(SchedulePath));
            s = ScriptableObject.CreateInstance<DaySchedule>();
            s.day = 1;
            s.endState = GameState.SliceEnd;
            s.phases = new List<DayPhase>
            {
                // Placeholder until the Timeline cutscene exists: it should set day1_intro_done (or call IDayCycle.CompletePhase).
                new()
                {
                    id = "intro", type = DayPhaseType.StoryScene, state = GameState.Cutscene, timeOfDay = TimeOfDayIcon.Dawn,
                    end = new PhaseEndCondition { goalFlag = "day1_intro_done", maxDuration = 0.5f },
                },
                new()
                {
                    id = "break_1", type = DayPhaseType.Break, state = GameState.Break, timeOfDay = TimeOfDayIcon.Sun,
                    objective = "Устрой шалость, пока не прозвенел звонок",
                    end = new PhaseEndCondition { goalFlag = "day1_break1_goal", maxDuration = 480f },
                },
                // Placeholder until the PE mini-game exists: it should set lesson_fizra_done.
                new()
                {
                    id = "lesson_fizra", type = DayPhaseType.Lesson, state = GameState.Lesson, timeOfDay = TimeOfDayIcon.Sun,
                    lessonId = "fizra",
                    objective = "Урок физкультуры",
                    goToLessonObjective = "Звонок! Беги на физру в спортзал",
                    end = new PhaseEndCondition { goalFlag = "lesson_fizra_done", maxDuration = 15f },
                },
                new()
                {
                    id = "break_2", type = DayPhaseType.Break, state = GameState.Break, timeOfDay = TimeOfDayIcon.Sun,
                    objective = "Поговори с одноклассником до звонка",
                    end = new PhaseEndCondition { goalFlag = "day1_break2_goal", maxDuration = 480f },
                },
            };
            AssetDatabase.CreateAsset(s, SchedulePath);
            AssetDatabase.SaveAssets();
            return s;
        }
    }
}
