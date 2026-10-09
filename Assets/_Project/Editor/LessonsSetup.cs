using System.Collections.Generic;
using System.IO;
using Funseki.Core;
using Funseki.DayCycle;
using Funseki.Lessons;
using Funseki.Lessons.Fizra;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Funseki.EditorTools
{
    // Tools > Funseki > Lessons > Setup Fizra lesson in Slice_Day1:
    // - data (created once, designer edits survive): Lesson_Fizra, FizraSettings, the six whistles (5 + the kettle),
    //   three rounds and the «Контрольная» round of 3 commands in 10 s;
    // - the mini-game prefab Prefabs/Lessons/Lesson_Fizra (rebuilt every time): PE teacher, helper on a bench,
    //   8 students in line, ball, kettle, lesson camera;
    // - DaySchedule_Day1_Slice: the 15 s placeholder limit of lesson_fizra removed (the lesson ends the phase);
    // - Slice_Day1: the root "Lessons" (rebuilt every time) with the LessonDirector, Stage_fizra in the gym and Walk_fizra:
    //   after the break_1 bell the break-time PE teacher leads the courtyard students to the gym (doors on the way are
    //   unlocked and opened); they are hidden during the lesson and go back to the courtyard for break_2.
    //   LessonEntrance_fizra (DayCycle setup) is moved into the gym; other roots are not touched.
    // Positions are School_Greybox plan meters: world x = plan x, world z = -plan y.
    public static class LessonsSetup
    {
        const string DataDir = "Assets/_Project/Data/Lessons";
        const string FizraDir = "Assets/_Project/Data/Lessons/Fizra";
        const string CommandsDir = "Assets/_Project/Data/Lessons/Fizra/Commands";
        const string RoundsDir = "Assets/_Project/Data/Lessons/Fizra/Rounds";
        const string PrefabDir = "Assets/_Project/Prefabs/Lessons";
        const string PrefabPath = "Assets/_Project/Prefabs/Lessons/Lesson_Fizra.prefab";
        const string ArtDir = "Assets/_Project/Art/Placeholders/Lessons";
        const string InputPath = "Assets/_Project/Data/Input/GameInput.inputactions";
        const string FontPath = "Assets/MainMenu/Fonts/GolosText-SemiBold SDF.asset";
        const string SchedulePath = "Assets/_Project/Data/DayCycle/DaySchedule_Day1_Slice.asset";
        const string RootName = "Lessons";

        // PE is in the gym (zone gym, plan x -22..0, y 16..32): the class stands in its middle looking west at the teacher.
        static readonly Vector3 StagePos = new(-11f, 0f, -24f);
        static readonly Quaternion StageRot = Quaternion.Euler(0f, -90f, 0f);
        // The walk from the courtyard: out of its west door, along the west corridor, into the gym east door.
        static readonly (string name, Vector3 pos)[] WalkRoute =
        {
            ("Point_Yard", new Vector3(9f, 0f, -21f)),
            ("Door_Courtyard_W", new Vector3(6f, 0f, -21f)),
            ("Point_Corridor_1", new Vector3(3f, 0f, -21f)),
            ("Point_Corridor_2", new Vector3(3f, 0f, -24f)),
            ("Door_Gym_E", new Vector3(0f, 0f, -24f)),
            ("Point_Gym_In", new Vector3(-3f, 0f, -24f)),
            ("Point_Gym_Stand", new Vector3(-8f, 0f, -24f)),
        };
        const string WalkPath = "Assets/_Project/Data/Lessons/LessonWalk_Fizra.asset";
        static readonly string[] HideNames = { "NPC_PE_Teacher", "NPC_Student_1", "NPC_Student_2", "NPC_Student_3" };

        [MenuItem("Tools/Funseki/Lessons/Setup Fizra lesson in Slice_Day1")]
        public static void Build()
        {
            foreach (var dir in new[] { DataDir, FizraDir, CommandsDir, RoundsDir, PrefabDir, ArtDir }) Directory.CreateDirectory(dir);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var settings = EnsureFizraData();
            var prefab = BuildPrefab(settings, font);
            NpcModelsSetup.DressLessonPrefab();
            var lesson = EnsureLesson(prefab);
            FixSchedule();

            var active = SceneManager.GetActiveScene();
            var slice = SceneManager.GetSceneByPath(CoreScenesSetup.SlicePath);
            bool opened = !slice.isLoaded;
            if (opened) slice = EditorSceneManager.OpenScene(CoreScenesSetup.SlicePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(slice);
            var keep = LayoutKeeper.Capture(slice);

            foreach (var go in slice.GetRootGameObjects())
                if (go.name == RootName) Object.DestroyImmediate(go);
            BuildScene(slice, lesson, font);

            keep.Restore();
            EditorSceneManager.MarkSceneDirty(slice);
            EditorSceneManager.SaveScene(slice);
            if (opened) EditorSceneManager.CloseScene(slice, true);
            else if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            AssetDatabase.SaveAssets();
            Debug.Log("[LessonsSetup] Slice_Day1: LessonDirector and Stage_fizra placed under 'Lessons'; prefab " + PrefabPath);
        }

        // ---------------------------------------------------------------- data

        static FizraSettings EnsureFizraData()
        {
            var jump = Command("Whistle_Jump", "jump", "Прыгай!", "•", "s", "Jump", false, FizraMove.Jump);
            var step = Command("Whistle_Step", "step", "Шаг {0}!", "• •", "s _ s", "Step", true, FizraMove.Step);
            var sit = Command("Whistle_Sit", "sit", "Сядь!", "———", "l", "Sit", false, FizraMove.Sit);
            var freeze = Command("Whistle_Freeze", "freeze", "Замри!", "~~~", "t", "", false, FizraMove.None);
            var ball = Command("Whistle_Catch", "catch", "Лови мяч!", "! !", "x _ x", "Catch", false, FizraMove.Catch);
            var kettle = Asset<WhistleCommand>($"{CommandsDir}/Whistle_Kettle.asset", c =>
            {
                c.id = "kettle";
                c.caption = "";
                c.glyph = "";
                c.synthPattern = "k";
                c.inputAction = "";
                c.move = FizraMove.None;
                c.isFalse = true;
                c.falseMoveOfStudents = FizraMove.Jump;
            });

            var r1 = Asset<FizraRound>($"{RoundsDir}/FizraRound_1.asset", r =>
            {
                r.title = "Раунд 1";
                r.commands = new List<WhistleCommand> { jump, step, sit, freeze };
                r.newCommand = jump;
                r.minCommands = 6; r.maxCommands = 8;
                r.reactionWindow = 1.5f; r.gap = 1.3f;
                r.showCaptions = true;
                r.startLine = "Фьють!";
            });
            var r2 = Asset<FizraRound>($"{RoundsDir}/FizraRound_2.asset", r =>
            {
                r.title = "Раунд 2";
                r.commands = new List<WhistleCommand> { jump, step, sit, freeze, ball };
                r.newCommand = ball;
                r.minCommands = 6; r.maxCommands = 8;
                r.reactionWindow = 1.5f; r.gap = 0.9f;
                r.showCaptions = false;
            });
            var r3 = Asset<FizraRound>($"{RoundsDir}/FizraRound_3.asset", r =>
            {
                r.title = "Раунд 3";
                r.commands = new List<WhistleCommand> { jump, step, sit, freeze, ball };
                r.minCommands = 6; r.maxCommands = 8;
                r.reactionWindow = 1.3f; r.gap = 0.6f;
                r.showCaptions = false;
                r.falseCommands = new List<WhistleCommand> { kettle };
                r.falseCount = 1;
                r.falseInMiddle = true;
                r.startPattern = "2093:0.2 2349:0.2 2637:0.2 2093:0.2 2637:0.4 2349:0.5";
                r.startLine = "*свистит мелодию*";
            });
            Asset<FizraRound>($"{RoundsDir}/FizraRound_Exam3in10.asset", r =>
            {
                r.title = "Контрольная: свисток";
                r.commands = new List<WhistleCommand> { jump, step, sit, freeze, ball };
                r.minCommands = 3; r.maxCommands = 3;
                r.reactionWindow = 1.5f;
                r.totalTime = 10f;
                r.showCaptions = false;
            });

            return Asset<FizraSettings>($"{FizraDir}/FizraSettings.asset", s =>
            {
                s.rounds = new List<FizraRound> { r1, r2, r3 };
                s.noWhistleIntro = new List<LessonLine>
                {
                    Line(LessonRole.Teacher, "*свистит в пальцы* Фьюю-фь!", 2.2f),
                    Line(LessonRole.Helper, "Свистка нет… Ладно. Я буду переводить. Доверьтесь мне.", 2.8f),
                    Line(LessonRole.LeadHero, "Это ужасная идея.", 2f),
                };
            });
        }

        static WhistleCommand Command(string file, string id, string caption, string glyph, string pattern, string action,
            bool directional, FizraMove move) =>
            Asset<WhistleCommand>($"{CommandsDir}/{file}.asset", c =>
            {
                c.id = id;
                c.caption = caption;
                c.glyph = glyph;
                c.synthPattern = pattern;
                c.inputAction = action;
                c.directional = directional;
                c.move = move;
            });

        static LessonData EnsureLesson(FizraMiniGame prefab)
        {
            var lesson = Asset<LessonData>($"{DataDir}/Lesson_Fizra.asset", d =>
            {
                d.id = "fizra";
                d.title = "Физра";
                d.leadHero = HeroId.Ryuta;
                d.intro = new List<LessonLine>
                {
                    Line(LessonRole.Teacher, "Фьють! *показывает: строиться*", 2.2f),
                    Line(LessonRole.Helper, "Он сказал «построились». Я перевожу, если что.", 2.6f),
                    Line(LessonRole.LeadHero, "Руки из карманов не вынимаю. Принцип.", 2.2f),
                };
                d.lateIntro = new List<LessonLine>
                {
                    Line(LessonRole.Teacher, "*долгий укоризненный свисток*", 2.4f),
                    Line(LessonRole.Teacher, "*показывает на часы, потом на небо*", 2.2f),
                    Line(LessonRole.Helper, "Он говорит: «Опоздавшие бегают до выпускного». Шучу. Наверное.", 3f),
                };
                d.outcomes = new List<LessonOutcomeScene>
                {
                    new()
                    {
                        name = "Блестяще без свистка", outcome = LessonOutcome.Excellent, requireFlags = new[] { "prank_whistle_done" },
                        gag = "voice_anthem", caption = "Блестяще!", minDuration = 6f, setFlags = new[] { "fizra_day1_voice_heard" },
                        lines = new List<LessonLine>
                        {
                            Line(LessonRole.Teacher, "Г-гимн школы… *поёт своим голосом*", 2.6f),
                            Line(LessonRole.Helper, "Он заговорил! Три года ни слова!", 2.4f),
                            Line(LessonRole.Student, "*падает в обморок*", 2f),
                        },
                    },
                    new()
                    {
                        name = "Блестяще", outcome = LessonOutcome.Excellent, gag = "anthem", caption = "Блестяще!", minDuration = 6f,
                        lines = new List<LessonLine>
                        {
                            Line(LessonRole.Teacher, "*пускает слезу и свистит гимн*", 3f),
                            Line(LessonRole.Helper, "Ладно, проиграл. Дынный сок с меня.", 2.5f),
                        },
                    },
                    new()
                    {
                        name = "Нормально", outcome = LessonOutcome.Normal, gag = "nod", caption = "Нормально", minDuration = 4f,
                        lines = new List<LessonLine>
                        {
                            Line(LessonRole.Teacher, "*короткий одобрительный свисток*", 2f),
                            Line(LessonRole.Helper, "Он говорит: «Сойдёт». Это высшая похвала после гимна.", 2.8f),
                        },
                    },
                    new()
                    {
                        name = "Позорно", outcome = LessonOutcome.Shame, gag = "point_exit", caption = "Позорно", minDuration = 5f,
                        setFlags = new[] { "fizra_teacher_angry" },
                        lines = new List<LessonLine>
                        {
                            Line(LessonRole.Teacher, "*гневно свистит и показывает на выход*", 2.6f),
                            Line(LessonRole.Helper, "Сок мой! Дынный!", 2f),
                            Line(LessonRole.LeadHero, "Я берёг силы.", 2f),
                        },
                    },
                };
            });
            lesson.miniGamePrefab = prefab;
            EditorUtility.SetDirty(lesson);
            return lesson;
        }

        static LessonLine Line(LessonRole who, string text, float duration) => new() { speaker = who, text = text, duration = duration };

        static void FixSchedule()
        {
            var schedule = AssetDatabase.LoadAssetAtPath<DaySchedule>(SchedulePath);
            if (schedule == null) { Debug.LogWarning("[LessonsSetup] No DaySchedule_Day1_Slice: run the DayCycle setup."); return; }
            var phase = schedule.phases.Find(p => p.lessonId == "fizra");
            if (phase == null) return;
            if (phase.end.maxDuration > 0f)
            {
                phase.end.maxDuration = 0f;
                Debug.Log("[LessonsSetup] DaySchedule_Day1_Slice: lesson_fizra no longer ends after a fixed time; the lesson ends it.");
            }
            if (phase.goToLessonObjective != null && phase.goToLessonObjective.Contains("двор"))
                phase.goToLessonObjective = "Звонок! Беги на физру в спортзал";
            EditorUtility.SetDirty(schedule);
        }

        // ---------------------------------------------------------------- prefab

        static FizraMiniGame BuildPrefab(FizraSettings settings, TMP_FontAsset font)
        {
            var teacherMat = Mat("M_Fizra_Teacher", new Color(0.65f, 0.12f, 0.12f));
            var studentMat = Mat("M_Fizra_Student", new Color(0.45f, 0.75f, 1f));
            var helperMat = Mat("M_Fizra_Helper", new Color(0.35f, 0.8f, 0.35f));
            var whistleMat = Mat("M_Fizra_Whistle", new Color(0.95f, 0.8f, 0.15f));
            var woodMat = Mat("M_Fizra_Bench", new Color(0.55f, 0.4f, 0.25f));
            var ballMat = Mat("M_Fizra_Ball", new Color(1f, 0.45f, 0.1f));
            var tearMat = Mat("M_Fizra_Tear", new Color(0.3f, 0.6f, 1f));

            var root = new GameObject("Lesson_Fizra");
            var game = root.AddComponent<FizraMiniGame>();

            // The teacher, facing the class (south).
            var teacher = new GameObject("Teacher").transform;
            teacher.SetParent(root.transform, false);
            teacher.SetLocalPositionAndRotation(new Vector3(0f, 0f, 5f), Quaternion.Euler(0f, 180f, 0f));
            var teacherBody = Capsule(teacher, "Body", teacherMat);
            var head = new GameObject("Head").transform;
            head.SetParent(teacher, false);
            head.localPosition = new Vector3(0f, 2.05f, 0f);
            var arm = new GameObject("Arm").transform;
            arm.SetParent(teacherBody, false);
            arm.localPosition = new Vector3(0.32f, 1.4f, 0f);
            Box(arm, "Sleeve", new Vector3(0f, 0f, 0.3f), new Vector3(0.11f, 0.11f, 0.6f), teacherMat);
            var whistle = Box(teacherBody, "Whistle", new Vector3(0f, 1.2f, 0.27f), new Vector3(0.06f, 0.05f, 0.08f), whistleMat);
            var tear = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tear.name = "Tear";
            tear.transform.SetParent(teacherBody, false);
            tear.transform.localPosition = new Vector3(0.1f, 1.55f, 0.26f);
            tear.transform.localScale = Vector3.one * 0.06f;
            tear.GetComponent<Renderer>().sharedMaterial = tearMat;
            Object.DestroyImmediate(tear.GetComponent<Collider>());
            var teacherAudio = teacher.gameObject.AddComponent<AudioSource>();
            teacherAudio.playOnAwake = false;
            teacherAudio.spatialBlend = 0f;

            // The helper (Шутник) on a bench to the east, looking at the class.
            var bench = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bench.name = "HelperBench";
            bench.transform.SetParent(root.transform, false);
            bench.transform.localPosition = new Vector3(7f, 0.22f, 2.5f);
            bench.transform.localScale = new Vector3(0.45f, 0.44f, 1.6f);
            bench.GetComponent<Renderer>().sharedMaterial = woodMat;
            var helper = new GameObject("Helper").transform;
            helper.SetParent(root.transform, false);
            helper.SetLocalPositionAndRotation(new Vector3(7f, 0.25f, 2.5f), Quaternion.Euler(0f, -90f, 0f));
            Capsule(helper, "Body", helperMat);

            // The line: the heroes stand at x 0 / ±2 (LessonStage spots), the students around them.
            var studentPos = new[]
            {
                new Vector3(-5.5f, 0f, 0f), new Vector3(-3.8f, 0f, 0f), new Vector3(3.8f, 0f, 0f), new Vector3(5.5f, 0f, 0f),
                new Vector3(-4.5f, 0f, -2f), new Vector3(-1.5f, 0f, -2f), new Vector3(1.5f, 0f, -2f), new Vector3(4.5f, 0f, -2f),
            };
            var students = new Transform[studentPos.Length];
            var line = new GameObject("Students").transform;
            line.SetParent(root.transform, false);
            for (int i = 0; i < studentPos.Length; i++)
            {
                students[i] = new GameObject($"Student_{i + 1}").transform;
                students[i].SetParent(line, false);
                students[i].localPosition = studentPos[i];
                Capsule(students[i], "Body", studentMat);
            }

            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Ball";
            ball.transform.SetParent(root.transform, false);
            ball.transform.localScale = Vector3.one * 0.25f;
            ball.GetComponent<Renderer>().sharedMaterial = ballMat;
            Object.DestroyImmediate(ball.GetComponent<Collider>());
            ball.SetActive(false);

            // The kettle whistles from the teachers' room window on the north side of the courtyard.
            var kettle = new GameObject("Kettle_TeachersRoom");
            kettle.transform.SetParent(root.transform, false);
            kettle.transform.localPosition = new Vector3(-8f, 3f, 13f);
            var kettleAudio = kettle.AddComponent<AudioSource>();
            kettleAudio.playOnAwake = false;
            kettleAudio.spatialBlend = 0.3f;

            var exit = new GameObject("ExitPoint").transform;
            exit.SetParent(root.transform, false);
            exit.localPosition = new Vector3(0f, 1f, -11f);   // the gym east door, behind the class

            // Behind the line, looking at the teacher: screen left = the step «влево».
            var camGo = new GameObject("CM Lesson");
            camGo.transform.SetParent(root.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 3.4f, -7.5f);
            camGo.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, 1.3f, 3f) - camGo.transform.localPosition, Vector3.up);
            var cam = camGo.AddComponent<CinemachineCamera>();
            var lens = cam.Lens;
            lens.FieldOfView = 50f;
            cam.Lens = lens;
            camGo.SetActive(false);

            var so = new SerializedObject(game);
            so.FindProperty("settings").objectReferenceValue = settings;
            so.FindProperty("actions").objectReferenceValue = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            so.FindProperty("font").objectReferenceValue = font;
            so.FindProperty("lessonCamera").objectReferenceValue = cam;
            so.FindProperty("teacher").objectReferenceValue = teacher;
            so.FindProperty("teacherBody").objectReferenceValue = teacherBody;
            so.FindProperty("teacherHead").objectReferenceValue = head;
            so.FindProperty("teacherArm").objectReferenceValue = arm;
            so.FindProperty("teacherWhistle").objectReferenceValue = whistle;
            so.FindProperty("teacherTear").objectReferenceValue = tear;
            so.FindProperty("helper").objectReferenceValue = helper;
            var arr = so.FindProperty("students");
            arr.arraySize = students.Length;
            for (int i = 0; i < students.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = students[i];
            so.FindProperty("ball").objectReferenceValue = ball.transform;
            so.FindProperty("teacherAudio").objectReferenceValue = teacherAudio;
            so.FindProperty("kettleAudio").objectReferenceValue = kettleAudio;
            so.FindProperty("exitPoint").objectReferenceValue = exit;
            so.FindProperty("stageAxes").objectReferenceValue = root.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return saved.GetComponent<FizraMiniGame>();
        }

        // ---------------------------------------------------------------- scene

        static void BuildScene(Scene slice, LessonData lesson, TMP_FontAsset font)
        {
            var root = new GameObject(RootName);
            var director = root.AddComponent<LessonDirector>();
            var dso = new SerializedObject(director);
            var list = dso.FindProperty("lessons");
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = lesson;
            dso.FindProperty("captionFont").objectReferenceValue = font;
            dso.ApplyModifiedPropertiesWithoutUndo();

            var stageGo = new GameObject("Stage_fizra");
            stageGo.transform.SetParent(root.transform, false);
            stageGo.transform.SetPositionAndRotation(StagePos, StageRot);
            var stage = stageGo.AddComponent<LessonStage>();

            var spots = new Transform[3];
            var spotPos = new[] { Vector3.zero, new Vector3(-2f, 0f, 0f), new Vector3(2f, 0f, 0f) };
            for (int i = 0; i < 3; i++)
            {
                spots[i] = new GameObject(i == 0 ? "HeroSpot_Leader" : $"HeroSpot_{i}").transform;
                spots[i].SetParent(stageGo.transform, false);
                spots[i].localPosition = spotPos[i];
            }

            var hide = new List<GameObject>();
            foreach (var go in slice.GetRootGameObjects())
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    if (System.Array.IndexOf(HideNames, t.name) >= 0) hide.Add(t.gameObject);
            if (hide.Count < HideNames.Length)
                Debug.LogWarning($"[LessonsSetup] Found {hide.Count} of {HideNames.Length} break-time PE objects to hide (run the Pranks setup first?).");

            var so = new SerializedObject(stage);
            so.FindProperty("lessonId").stringValue = lesson.id;
            var s = so.FindProperty("heroSpots");
            s.arraySize = spots.Length;
            for (int i = 0; i < spots.Length; i++) s.GetArrayElementAtIndex(i).objectReferenceValue = spots[i];
            var h = so.FindProperty("hideDuringLesson");
            h.arraySize = hide.Count;
            for (int i = 0; i < hide.Count; i++) h.GetArrayElementAtIndex(i).objectReferenceValue = hide[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            BuildWalk(root.transform, hide);
            MoveEntrance(slice);
        }

        // The break-time PE teacher leads the courtyard students to the gym after the break_1 bell.
        static void BuildWalk(Transform root, List<GameObject> cast)
        {
            var settings = Asset<LessonWalkSettings>(WalkPath, _ => { });
            cast.Sort((a, b) => System.Array.IndexOf(HideNames, a.name).CompareTo(System.Array.IndexOf(HideNames, b.name)));

            var walkGo = new GameObject("Walk_fizra");
            walkGo.transform.SetParent(root, false);
            foreach (var (pointName, pos) in WalkRoute)
            {
                var p = new GameObject(pointName).transform;
                p.SetParent(walkGo.transform, false);
                p.position = pos;
            }
            var walk = walkGo.AddComponent<LessonWalk>();
            var pause = new List<Behaviour>();
            foreach (var go in cast)
                if (go.GetComponent<Funseki.Pranks.WhistleTeacher>() is { } wt) pause.Add(wt);

            var so = new SerializedObject(walk);
            so.FindProperty("settings").objectReferenceValue = settings;
            var w = so.FindProperty("walkers");
            w.arraySize = cast.Count;
            for (int i = 0; i < cast.Count; i++) w.GetArrayElementAtIndex(i).objectReferenceValue = cast[i].transform;
            var p2 = so.FindProperty("pauseWhileWalking");
            p2.arraySize = pause.Count;
            for (int i = 0; i < pause.Count; i++) p2.GetArrayElementAtIndex(i).objectReferenceValue = pause[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // LessonEntrance_fizra belongs to the DayCycle setup; only its place changes (same values as DayCycleSetup now has).
        static void MoveEntrance(Scene slice)
        {
            foreach (var go in slice.GetRootGameObjects())
            {
                if (go.name != "LessonEntrance_fizra") continue;
                go.transform.position = DayCycleSetup.FizraEntrancePos;
                var box = go.GetComponent<BoxCollider>();
                if (box != null) box.size = DayCycleSetup.FizraEntranceSize;
                Debug.Log("[LessonsSetup] LessonEntrance_fizra moved into the gym.");
            }
        }

        // ---------------------------------------------------------------- helpers

        static Transform Capsule(Transform parent, string name, Material mat)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Capsule";
            capsule.transform.SetParent(pivot, false);
            capsule.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            capsule.transform.localScale = new Vector3(0.5f, 0.85f, 0.5f);
            capsule.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(capsule.GetComponent<Collider>());
            Box(pivot, "Nose", new Vector3(0f, 1.45f, 0.25f), new Vector3(0.12f, 0.12f, 0.15f), mat);
            return pivot;
        }

        static GameObject Box(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static T Asset<T>(string path, System.Action<T> init) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            init(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static Material Mat(string name, Color color)
        {
            string path = $"{ArtDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
