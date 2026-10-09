using System.Collections.Generic;
using System.IO;
using Funseki.Core;
using Funseki.DayCycle;
using Funseki.Dialogue;
using Funseki.Heroes;
using Funseki.Inventory;
using Funseki.NPC;
using Funseki.Quests;
using Funseki.School;
using Funseki.Story;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Funseki.EditorTools
{
    // Tools > Funseki > Day1 > Setup Day 1 start (prologue → dorm): the start of the full day 1 (CLAUDE.md «Полный День 1»,
    // steps 1–4) on top of the existing systems.
    // - Data, created once and then kept (designer edits survive): speakers of the principal and the five classmates,
    //   dialogues Data/Dialogue/Day1/* ([TODO] lines), the quest Q_Day1_Main + QuestSettings, Data/Story/Prologue_Day1
    //   and Day1Start; the Prologue map in GameInput (Next, Skip).
    // - DaySchedule_Day1_Slice: the old "intro" phase becomes prologue → arrival → dorm (once; the rest of the day stays).
    // - Bootstrap: "Quests" (QuestService) under [Bootstrap] (rebuilt).
    // - School_Greybox (dorm only): dorm_5..dorm_1 are rooms 7..11 (display names), rooms 10 and 11 locked until day 3
    //   («Заперто. Тут нужна отмычка»), bunk beds switched off, 3 beds + nightstands per room in School_Props/Dorm_Day1.
    // - Slice_Day1: the root "Day1_Start" (rebuilt): the prologue, the fence, the principal and his spots, room 7
    //   (RoomMeeting, CollectionShelf, TrophyWall), the classmates of rooms 8 and 9.
    public static class Day1StartSetup
    {
        const string RootName = "Day1_Start";
        const string InputPath = "Assets/_Project/Data/Input/GameInput.inputactions";
        const string SchedulePath = "Assets/_Project/Data/DayCycle/DaySchedule_Day1_Slice.asset";
        const string HeroSettingsPath = "Assets/_Project/Data/Heroes/HeroSettings.asset";
        const string SpeakersDir = "Assets/_Project/Data/Dialogue/Speakers";
        const string DialogueDir = "Assets/_Project/Data/Dialogue/Day1";
        const string QuestsDir = "Assets/_Project/Data/Quests";
        const string QuestSettingsPath = QuestsDir + "/QuestSettings.asset";
        const string MainQuestPath = QuestsDir + "/Q_Day1_Main.asset";
        const string StoryDir = "Assets/_Project/Data/Story";
        const string ProloguePath = StoryDir + "/Prologue_Day1.asset";
        const string StartDataPath = StoryDir + "/Day1Start.asset";
        const string NpcSettingsPath = "Assets/_Project/Data/NPC/NpcSettings.asset";
        const string LayoutPath = "Assets/_Project/School/Data/SchoolLayout.asset";
        const string SchoolScenePath = "Assets/_Project/School/Scenes/School_Greybox.unity";
        const string TitleFontPath = "Assets/MainMenu/Fonts/Unbounded-ExtraBold SDF.asset";
        const string BodyFontPath = "Assets/MainMenu/Fonts/GolosText-SemiBold SDF.asset";
        const string MatDir = "Assets/_Project/Art/Placeholders/Day1";

        // Dorm: 5 rooms 8 x 8 m on floor 2 (y 4.2) along the north wall, doors to the corridor in the south wall.
        // Numbered from the north-east stairs westwards: dorm_5 = 7, dorm_4 = 8, dorm_3 = 9, dorm_2 = 10, dorm_1 = 11.
        const float Floor2 = 4.2f;
        static readonly (string zone, int number, float x)[] Rooms =
        {
            ("dorm_5", 7, 38f), ("dorm_4", 8, 30f), ("dorm_3", 9, 22f), ("dorm_2", 10, 14f), ("dorm_1", 11, 6f),
        };
        const int LockpickDay = 3;
        const string LockedLine = "Заперто. Тут нужна отмычка";

        // The front yard: the porch (Schoolyard) and the road to the gates (the old intro's placeholder gate at z -60.5).
        const float YardWest = 17.2f, YardEast = 33.2f, GateZ = -60.5f, FacadeZ = -42f;

        // ---------------------------------------------------------------- menu

        [MenuItem("Tools/Funseki/Day1/Setup Day 1 start (prologue → dorm)")]
        public static void Build()
        {
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            if (input == null) { Debug.LogError($"[Day1StartSetup] {InputPath} not found."); return; }
            Directory.CreateDirectory(DialogueDir);
            Directory.CreateDirectory(QuestsDir);
            Directory.CreateDirectory(StoryDir);
            Directory.CreateDirectory(MatDir);

            EnsurePrologueMap();
            var speakers = EnsureSpeakers();
            var dialogues = EnsureDialogues(speakers);
            var quests = EnsureQuests();
            var prologue = EnsurePrologueData();
            var start = EnsureStartData(dialogues);
            FixSchedule();
            FixHeroSettings();
            AssetDatabase.SaveAssets();

            SetupBootstrap(quests);
            SetupSchool();
            SetupSlice(prologue, start, dialogues, speakers, input);
            AssetDatabase.SaveAssets();

            Debug.Log("[Day1StartSetup] Done. Dialogue ids for the game designer: " + string.Join(", ", DialogueIds));
        }

        static readonly string[] DialogueIds =
        {
            "Day1_Director_Intro", "Day1_Director_Room", "Day1_Room7_Meet",
            "Day1_Meet_Takeshi", "Day1_Meet_Yukki", "Day1_Meet_Masumi", "Day1_Meet_Hiro", "Day1_Meet_Tsubaki",
        };

        // ---------------------------------------------------------------- data

        static void EnsurePrologueMap()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            if (asset.FindActionMap(PrologueDossier.MapName) != null) return;
            var map = asset.AddActionMap(PrologueDossier.MapName);
            var next = map.AddAction("Next", InputActionType.Button);
            next.AddBinding("<Mouse>/leftButton").WithGroup("Keyboard&Mouse");
            next.AddBinding("<Keyboard>/enter").WithGroup("Keyboard&Mouse");
            next.AddBinding("<Gamepad>/buttonSouth").WithGroup("Gamepad");
            var skip = map.AddAction("Skip", InputActionType.Button);
            skip.AddBinding("<Keyboard>/space").WithGroup("Keyboard&Mouse");
            skip.AddBinding("<Gamepad>/start").WithGroup("Gamepad");
            File.WriteAllText(InputPath, asset.ToJson());
            AssetDatabase.ImportAsset(InputPath);
            Debug.Log("[Day1StartSetup] GameInput: Prologue map added (Next: LMB / Enter / A, Skip: Space / Start held).");
        }

        // Speaker key -> asset. Heroes exist already; the principal and the classmates are created once.
        static Dictionary<string, Speaker> EnsureSpeakers()
        {
            var s = new Dictionary<string, Speaker>
            {
                ["Ryuta"] = AssetDatabase.LoadAssetAtPath<Speaker>($"{SpeakersDir}/Speaker_Ryuta.asset"),
                ["Rei"] = AssetDatabase.LoadAssetAtPath<Speaker>($"{SpeakersDir}/Speaker_Rei.asset"),
                ["Kaito"] = AssetDatabase.LoadAssetAtPath<Speaker>($"{SpeakersDir}/Speaker_Kaito.asset"),
            };
            AddSpeaker(s, "Director", "Директор", new Color(0.55f, 0.55f, 0.6f), 0.8f);
            AddSpeaker(s, "Takeshi", "Такеши", new Color(0.35f, 0.6f, 0.95f), 0.95f);
            AddSpeaker(s, "Yukki", "Юкки", new Color(1f, 0.55f, 0.7f), 1.35f);
            AddSpeaker(s, "Masumi", "Масуми", new Color(0.7f, 0.5f, 1f), 1.25f);
            AddSpeaker(s, "Hiro", "Хиро", new Color(1f, 0.6f, 0.2f), 1.1f);
            AddSpeaker(s, "Tsubaki", "Цубаки", new Color(0.4f, 0.85f, 0.6f), 1.3f);
            return s;
        }

        static void AddSpeaker(Dictionary<string, Speaker> s, string key, string displayName, Color color, float pitch)
        {
            string path = $"{SpeakersDir}/Speaker_{key}.asset";
            var sp = AssetDatabase.LoadAssetAtPath<Speaker>(path);
            if (sp == null)
            {
                sp = ScriptableObject.CreateInstance<Speaker>();
                sp.displayName = displayName;
                sp.nameColor = color;
                sp.mumblePitch = pitch;
                AssetDatabase.CreateAsset(sp, path);
            }
            s[key] = sp;
        }

        static Dictionary<string, DialogueGraph> EnsureDialogues(Dictionary<string, Speaker> sp)
        {
            var d = new Dictionary<string, DialogueGraph>();
            d["Day1_Director_Intro"] = Graph("Day1_Director_Intro",
                N(sp["Director"], "[TODO] Директор встречает Рюту у входа: «Тамура Рюта, я полагаю?»"),
                N(sp["Ryuta"], "[TODO] Ответ Рюты."),
                N(sp["Director"], "[TODO] Правила школы."),
                N(sp["Director"], "[TODO] Устав."),
                N(sp["Director"], "[TODO] Расписание."),
                N(sp["Director"], "[TODO] «Идём, покажу, где ты будешь жить».", end: true));
            d["Day1_Director_Room"] = Graph("Day1_Director_Room",
                N(sp["Director"], "[TODO] У двери комнаты 7: «Твоя комната. Соседи приехали вчера»."),
                N(sp["Ryuta"], "[TODO] Ответ Рюты."),
                N(sp["Director"], "[TODO] Директор прощается и уходит.", end: true));
            d["Day1_Room7_Meet"] = Graph("Day1_Room7_Meet",
                N(sp["Kaito"], "[TODO] Кайто знакомится с Рютой."),
                N(sp["Rei"], "[TODO] Рэй знакомится с Рютой."),
                N(sp["Ryuta"], "[TODO] Ответ Рюты."),
                N(sp["Kaito"], "[TODO] «Пойдём познакомимся с остальными: комнаты 8 и 9»."),
                N(sp["Rei"], "[TODO] Реплика Рэй.", end: true));
            Meet(d, sp, "Takeshi", "Такеши");
            Meet(d, sp, "Yukki", "Юкки");
            Meet(d, sp, "Masumi", "Масуми");
            Meet(d, sp, "Hiro", "Хиро");
            Meet(d, sp, "Tsubaki", "Цубаки");
            return d;
        }

        static void Meet(Dictionary<string, DialogueGraph> d, Dictionary<string, Speaker> sp, string key, string name)
        {
            string id = $"Day1_Meet_{key}";
            d[id] = Graph(id,
                N(sp[key], $"[TODO] {name} знакомится с Рютой."),
                N(sp["Ryuta"], "[TODO] Ответ Рюты."),
                N(sp[key], $"[TODO] Последняя реплика {name}.", end: true));
        }

        static DialogueNode N(Speaker speaker, string text, bool end = false) =>
            new() { speaker = speaker, text = text, end = end };

        static DialogueGraph Graph(string id, params DialogueNode[] nodes)
        {
            string path = $"{DialogueDir}/{id}.asset";
            var g = AssetDatabase.LoadAssetAtPath<DialogueGraph>(path);
            if (g != null) return g;
            g = ScriptableObject.CreateInstance<DialogueGraph>();
            for (int i = 0; i < nodes.Length; i++) nodes[i].id = $"n{i + 1:00}";
            g.nodes = new List<DialogueNode>(nodes);
            AssetDatabase.CreateAsset(g, path);
            return g;
        }

        static QuestSettings EnsureQuests()
        {
            var main = AssetDatabase.LoadAssetAtPath<QuestData>(MainQuestPath);
            if (main == null)
            {
                main = ScriptableObject.CreateInstance<QuestData>();
                main.id = "Q_Day1_Main";
                main.title = "Познакомься с группой";
                main.kind = QuestKind.Main;
                main.startFlag = "heroes_unlocked";
                main.steps = new List<QuestStep>
                {
                    new() { objective = "Познакомься с соседями из комнаты 8",
                            doneFlags = new List<string> { "day1_met_takeshi", "day1_met_yukki", "day1_met_masumi" } },
                    new() { objective = "Познакомься с соседями из комнаты 9",
                            doneFlags = new List<string> { "day1_met_hiro", "day1_met_tsubaki" } },
                };
                AssetDatabase.CreateAsset(main, MainQuestPath);
            }
            var settings = AssetDatabase.LoadAssetAtPath<QuestSettings>(QuestSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<QuestSettings>();
                AssetDatabase.CreateAsset(settings, QuestSettingsPath);
            }
            if (!settings.quests.Contains(main))
            {
                settings.quests.Add(main);
                EditorUtility.SetDirty(settings);
            }
            return settings;
        }

        static PrologueData EnsurePrologueData()
        {
            var p = AssetDatabase.LoadAssetAtPath<PrologueData>(ProloguePath);
            if (p != null) return p;
            p = ScriptableObject.CreateInstance<PrologueData>();
            p.titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontPath);
            p.bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);
            AssetDatabase.CreateAsset(p, ProloguePath);
            return p;
        }

        static Day1StartData EnsureStartData(Dictionary<string, DialogueGraph> d)
        {
            var s = AssetDatabase.LoadAssetAtPath<Day1StartData>(StartDataPath);
            if (s != null) return s;
            s = ScriptableObject.CreateInstance<Day1StartData>();
            s.directorIntro = d["Day1_Director_Intro"];
            s.directorRoom = d["Day1_Director_Room"];
            s.room7Meet = d["Day1_Room7_Meet"];
            AssetDatabase.CreateAsset(s, StartDataPath);
            return s;
        }

        // The old intro cutscene (the trio at the gates) gives way to prologue → arrival → dorm. Once: if the
        // schedule already has the prologue, it is left as the designer keeps it.
        static void FixSchedule()
        {
            var schedule = AssetDatabase.LoadAssetAtPath<DaySchedule>(SchedulePath);
            if (schedule == null) { Debug.LogWarning($"[Day1StartSetup] {SchedulePath} not found."); return; }
            if (schedule.phases.Exists(p => p.id == "prologue")) return;
            int at = schedule.phases.FindIndex(p => p.id == "intro");
            if (at >= 0) schedule.phases.RemoveAt(at);
            else at = 0;
            schedule.phases.InsertRange(at, new[]
            {
                new DayPhase
                {
                    id = "prologue", type = DayPhaseType.StoryScene, state = GameState.Cutscene, timeOfDay = TimeOfDayIcon.Dawn,
                    end = new PhaseEndCondition { goalFlag = "day1_prologue_done" },
                },
                new DayPhase
                {
                    id = "arrival", type = DayPhaseType.StoryScene, state = GameState.Break, timeOfDay = TimeOfDayIcon.Dawn,
                    objective = "Встреться с директором у входа",
                    end = new PhaseEndCondition { goalFlag = "day1_director_done" },
                },
                new DayPhase
                {
                    id = "dorm", type = DayPhaseType.StoryScene, state = GameState.Break, timeOfDay = TimeOfDayIcon.Dawn,
                    objective = "Зайди в комнату 7",
                    end = new PhaseEndCondition { goalFlag = "quest_q_day1_main_done" },
                },
            });
            EditorUtility.SetDirty(schedule);
            Debug.Log("[Day1StartSetup] DaySchedule_Day1_Slice: intro → prologue, arrival, dorm.");
        }

        static void FixHeroSettings()
        {
            // An asset saved before the field existed reads the default; write it so it shows in the file.
            var hs = AssetDatabase.LoadAssetAtPath<HeroSettings>(HeroSettingsPath);
            if (hs == null) return;
            if (string.IsNullOrEmpty(hs.partyUnlockFlag)) hs.partyUnlockFlag = "heroes_unlocked";
            // Day 1 starts as Рюта alone.
            hs.startHero = HeroId.Ryuta;
            EditorUtility.SetDirty(hs);
        }

        // ---------------------------------------------------------------- Bootstrap

        static void SetupBootstrap(QuestSettings quests)
        {
            var scene = SceneManager.GetSceneByPath(CoreScenesSetup.BootstrapPath);
            bool wasOpen = scene.isLoaded;
            if (!wasOpen) scene = EditorSceneManager.OpenScene(CoreScenesSetup.BootstrapPath, OpenSceneMode.Additive);

            GameObject boot = null;
            foreach (var go in scene.GetRootGameObjects())
                if (go.GetComponent<Bootstrap>() != null) boot = go;
            if (boot == null)
            {
                Debug.LogError("[Day1StartSetup] No Bootstrap object in the Bootstrap scene.");
                if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
                return;
            }
            var old = boot.transform.Find("Quests");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var go2 = new GameObject("Quests");
            go2.transform.SetParent(boot.transform, false);
            PlayerSliceSetup.Set(go2.AddComponent<QuestService>(), "settings", quests);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
        }

        // ---------------------------------------------------------------- School_Greybox (dorm only)

        static void SetupSchool()
        {
            var layout = AssetDatabase.LoadAssetAtPath<SchoolLayout>(LayoutPath);
            if (layout != null)
            {
                foreach (var r in Rooms)
                {
                    var z = layout.zones.Find(x => x.zoneId == r.zone);
                    if (z != null) z.displayName = $"Комната {r.number}";
                }
                EditorUtility.SetDirty(layout);
            }

            var active = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(SchoolScenePath);
            bool wasOpen = scene.isLoaded;
            if (!wasOpen) scene = EditorSceneManager.OpenScene(SchoolScenePath, OpenSceneMode.Additive);

            GameObject school = null, furniture = null, props = null;
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == "School") school = go;
                else if (go.name == "School_Furniture") furniture = go;
                else if (go.name == "School_Props") props = go;
            }
            if (school == null || furniture == null)
            {
                Debug.LogError("[Day1StartSetup] School_Greybox has no School / School_Furniture root.");
                if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
                return;
            }
            if (props == null)
            {
                props = new GameObject("School_Props");
                SceneManager.MoveGameObjectToScene(props, scene);
            }

            // Room names on the zone triggers.
            foreach (var zone in school.GetComponentsInChildren<Zone>(true))
                foreach (var r in Rooms)
                    if (zone.zoneId == r.zone) { zone.displayName = $"Комната {r.number}"; EditorUtility.SetDirty(zone); }

            // Bunk beds give way to three single beds.
            int bunks = 0;
            foreach (var t in furniture.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.Contains("BunkBed") || !t.gameObject.activeSelf || (t.parent != null && t.parent.name.Contains("BunkBed"))) continue;
                foreach (var r in Rooms)
                    if (UnderZone(t, r.zone)) { t.gameObject.SetActive(false); bunks++; }
            }

            // Rooms 10 and 11: locked until the lockpick (day 3).
            int locks = 0;
            var doors = school.GetComponentsInChildren<Door>(true);
            foreach (var r in Rooms)
            {
                if (r.number < 10) continue;
                var door = NearestDoor(doors, new Vector3(r.x + 4f, Floor2 + 1.1f, -8f));
                if (door == null) { Debug.LogWarning($"[Day1StartSetup] No door found for room {r.number} ({r.zone})."); continue; }
                door.lockedLine = LockedLine;
                var lk = door.GetComponent<LockedDoor>() ?? door.gameObject.AddComponent<LockedDoor>();
                lk.zoneId = r.zone;
                lk.unlockDay = LockpickDay;
                lk.lockedLine = LockedLine;
                EditorUtility.SetDirty(door);
                EditorUtility.SetDirty(lk);
                locks++;
            }

            // Beds and nightstands.
            var keepBeds = LayoutKeeper.Capture(props.transform);
            var oldBeds = props.transform.Find("Dorm_Day1");
            if (oldBeds != null) Object.DestroyImmediate(oldBeds.gameObject);
            var beds = new GameObject("Dorm_Day1").transform;
            beds.SetParent(props.transform, false);
            var frame = Mat("M_Day1_BedFrame", new Color(0.42f, 0.44f, 0.48f));
            var mattress = Mat("M_Day1_Mattress", new Color(0.72f, 0.82f, 0.92f));
            var pillow = Mat("M_Day1_Pillow", new Color(0.96f, 0.96f, 0.94f));
            var wood = Mat("M_Day1_Nightstand", new Color(0.62f, 0.45f, 0.3f));
            foreach (var r in Rooms)
            {
                var room = new GameObject($"Room_{r.number}").transform;
                room.SetParent(beds, false);
                for (int i = 0; i < 3; i++)
                {
                    float x = r.x + 1.2f + i * 2.8f;
                    Bed(room, $"Bed_{i + 1}", new Vector3(x, Floor2, -1.25f), frame, mattress, pillow);
                    float nx = i == 0 ? r.x + 0.48f : x - 1.4f + 0.05f;
                    Solid(room, $"Nightstand_{i + 1}", new Vector3(nx, Floor2 + 0.275f, -0.5f), new Vector3(0.45f, 0.55f, 0.45f), wood);
                }
            }

            keepBeds.Restore();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
            else if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            Debug.Log($"[Day1StartSetup] School_Greybox: rooms 7–11 named, {locks} door(s) locked until day {LockpickDay}, " +
                      $"{bunks} bunk bed(s) switched off, 15 beds + nightstands in School_Props/Dorm_Day1.");
        }

        static bool UnderZone(Transform t, string zoneId)
        {
            for (var p = t.parent; p != null; p = p.parent)
                if (p.name == $"Zone_{zoneId}") return true;
            return false;
        }

        static Door NearestDoor(Door[] doors, Vector3 at)
        {
            Door best = null;
            float bestD = 2.5f;
            foreach (var d in doors)
            {
                var rend = d.GetComponentInChildren<Renderer>();
                Vector3 c = rend != null ? rend.bounds.center : d.transform.position;
                if (Mathf.Abs(c.y - at.y) > 2f) continue;
                float dist = Vector2.Distance(new Vector2(c.x, c.z), new Vector2(at.x, at.z));
                if (dist < bestD) { bestD = dist; best = d; }
            }
            return best;
        }

        // A single bed along the north wall, head to the wall: frame, mattress, pillow (the frame has the collider).
        static void Bed(Transform parent, string name, Vector3 center, Material frame, Material mattress, Material pillow)
        {
            var bed = new GameObject(name).transform;
            bed.SetParent(parent, false);
            bed.position = center;
            Solid(bed, "Frame", center + new Vector3(0f, 0.2f, 0f), new Vector3(0.95f, 0.4f, 2.05f), frame);
            Visual(bed, "Mattress", center + new Vector3(0f, 0.47f, -0.02f), new Vector3(0.85f, 0.14f, 1.95f), mattress);
            Visual(bed, "Pillow", center + new Vector3(0f, 0.59f, 0.72f), new Vector3(0.6f, 0.1f, 0.35f), pillow);
        }

        // ---------------------------------------------------------------- Slice_Day1

        static void SetupSlice(PrologueData prologue, Day1StartData data, Dictionary<string, DialogueGraph> dialogues,
            Dictionary<string, Speaker> speakers, InputActionAsset input)
        {
            var active = SceneManager.GetActiveScene();
            var slice = SceneManager.GetSceneByPath(CoreScenesSetup.SlicePath);
            bool opened = !slice.isLoaded;
            if (opened) slice = EditorSceneManager.OpenScene(CoreScenesSetup.SlicePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(slice);
            var keep = LayoutKeeper.Capture(slice);

            HeroUnit kaito = null, rei = null;
            foreach (var go in slice.GetRootGameObjects())
            {
                if (go.name == RootName) { Object.DestroyImmediate(go); continue; }
                foreach (var u in go.GetComponentsInChildren<HeroUnit>(true))
                {
                    if (u.Data == null) continue;
                    if (u.Data.id == HeroId.Kaito) kaito = u;
                    else if (u.Data.id == HeroId.Rei) rei = u;
                }
            }
            if (kaito == null || rei == null)
                Debug.LogWarning("[Day1StartSetup] Кайто / Рэй not found in Slice_Day1: run Tools > Funseki > Slice > Build Slice_Day1 first.");

            var root = new GameObject(RootName).transform;
            var npcSettings = AssetDatabase.LoadAssetAtPath<NpcSettings>(NpcSettingsPath);
            var directorMat = Mat("M_Day1_Director", new Color(0.2f, 0.22f, 0.3f));

            // Prologue.
            var pro = new GameObject("Prologue");
            pro.transform.SetParent(root, false);
            var dossier = pro.AddComponent<PrologueDossier>();
            PlayerSliceSetup.Set(dossier, "data", prologue);
            PlayerSliceSetup.Set(dossier, "actions", input);

            // Fence.
            BuildFence(root, data);

            // Arrival: start spots, the principal, the room-7 spots and his way out to the north-east stairs.
            var arrival = new GameObject("Arrival").transform;
            arrival.SetParent(root, false);
            var ryutaStart = Point(arrival, "Start_Ryuta", new Vector3(25.2f, 0.05f, -57.5f), 0f);
            var kaitoStart = Point(arrival, "Start_Kaito", new Vector3(42f, Floor2 + 0.05f, -2.75f), 180f);
            var reiStart = Point(arrival, "Start_Rei", new Vector3(44.8f, Floor2 + 0.05f, -2.75f), 200f);
            var director = Npc(arrival, "NPC_Director", new Vector3(25.2f, 0f, -46.3f), 180f, directorMat, true, npcSettings, speakers["Director"]);
            var heroAtRoom = Point(arrival, "Room7_Hero", new Vector3(42f, Floor2 + 0.05f, -10.2f), 0f);
            var directorAtRoom = Point(arrival, "Room7_Director", new Vector3(43.4f, Floor2, -9.1f), -130f);
            var exit = new[]
            {
                Point(arrival, "Exit_1", new Vector3(49f, Floor2, -10f), 90f),
                Point(arrival, "Exit_2", new Vector3(49f, Floor2, -5.5f), 0f),
            };
            var arr = arrival.gameObject.AddComponent<Day1Arrival>();
            var so = new SerializedObject(arr);
            so.FindProperty("data").objectReferenceValue = data;
            so.FindProperty("ryutaStart").objectReferenceValue = ryutaStart;
            so.FindProperty("kaitoStart").objectReferenceValue = kaitoStart;
            so.FindProperty("reiStart").objectReferenceValue = reiStart;
            so.FindProperty("director").objectReferenceValue = director;
            so.FindProperty("heroAtRoom").objectReferenceValue = heroAtRoom;
            so.FindProperty("directorAtRoom").objectReferenceValue = directorAtRoom;
            var ex = so.FindProperty("directorExit");
            ex.arraySize = exit.Length;
            for (int i = 0; i < exit.Length; i++) ex.GetArrayElementAtIndex(i).objectReferenceValue = exit[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            // Room 7: the meeting, the shelf for figures and the wall for trophies.
            BuildRoom7(root, data, kaito, rei);

            // Rooms 8 and 9: the classmates.
            var classmates = new GameObject("Classmates").transform;
            classmates.SetParent(root, false);
            Classmate(classmates, "Takeshi", "NPC_Takeshi", new Vector3(32f, Floor2, -3f), 180f, NpcModelsSetup.Jock, dialogues, speakers, npcSettings);
            Classmate(classmates, "Yukki", "NPC_Yukki", new Vector3(34.2f, Floor2, -3.3f), 170f, NpcModelsSetup.GossipA, dialogues, speakers, npcSettings);
            Classmate(classmates, "Masumi", "NPC_Masumi", new Vector3(36.2f, Floor2, -3f), 200f, NpcModelsSetup.GossipB, dialogues, speakers, npcSettings);
            Classmate(classmates, "Hiro", "NPC_Hiro", new Vector3(24.8f, Floor2, -3f), 170f, NpcModelsSetup.Joker, dialogues, speakers, npcSettings);
            Classmate(classmates, "Tsubaki", "NPC_Tsubaki", new Vector3(27.6f, Floor2, -3.2f), 195f, NpcModelsSetup.GossipB, dialogues, speakers, npcSettings);

            keep.Restore();
            EditorSceneManager.MarkSceneDirty(slice);
            EditorSceneManager.SaveScene(slice);
            if (opened) EditorSceneManager.CloseScene(slice, true);
            else if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
        }

        // The yard is closed by the facade in the north and a fence on the other three sides; the gap between the
        // old placeholder gate's pillars gets closed gates. Visible fence 1.2 m, invisible walls 3 m, line zones inside.
        static void BuildFence(Transform root, Day1StartData data)
        {
            var fence = new GameObject("Fence").transform;
            fence.SetParent(root, false);
            float depth = FacadeZ - GateZ, midZ = (FacadeZ + GateZ) * 0.5f, width = YardEast - YardWest, midX = (YardEast + YardWest) * 0.5f;
            var metal = Mat("M_Day1_Fence", new Color(0.25f, 0.3f, 0.28f));

            Visual(fence, "Fence_W", new Vector3(YardWest - 0.15f, 0.6f, midZ), new Vector3(0.12f, 1.2f, depth), metal);
            Visual(fence, "Fence_E", new Vector3(YardEast + 0.15f, 0.6f, midZ), new Vector3(0.12f, 1.2f, depth), metal);
            Visual(fence, "Gate_RailTop", new Vector3(25.2f, 1.9f, GateZ - 0.06f), new Vector3(6.7f, 0.08f, 0.08f), metal);
            Visual(fence, "Gate_RailBottom", new Vector3(25.2f, 0.15f, GateZ - 0.06f), new Vector3(6.7f, 0.08f, 0.08f), metal);
            for (float x = 22.2f; x < 28.5f; x += 0.6f)
                Visual(fence, "Gate_Bar", new Vector3(x, 1.0f, GateZ - 0.06f), new Vector3(0.05f, 2f, 0.05f), metal);

            Wall(fence, "Wall_W", new Vector3(YardWest - 0.15f, 1.5f, midZ), new Vector3(0.3f, 3f, depth + 1f));
            Wall(fence, "Wall_E", new Vector3(YardEast + 0.15f, 1.5f, midZ), new Vector3(0.3f, 3f, depth + 1f));
            Wall(fence, "Wall_S", new Vector3(midX, 1.5f, GateZ - 0.2f), new Vector3(width + 1f, 3f, 0.3f));

            var zones = new[]
            {
                LineZone(fence, "Line_W", new Vector3(YardWest + 0.6f, 1f, midZ), new Vector3(1.2f, 2f, depth)),
                LineZone(fence, "Line_E", new Vector3(YardEast - 0.6f, 1f, midZ), new Vector3(1.2f, 2f, depth)),
                LineZone(fence, "Line_S", new Vector3(midX, 1f, GateZ + 0.6f), new Vector3(width, 2f, 1.2f)),
            };
            var b = fence.gameObject.AddComponent<StoryBoundary>();
            var so = new SerializedObject(b);
            so.FindProperty("data").objectReferenceValue = data;
            var arr = so.FindProperty("lineZones");
            arr.arraySize = zones.Length;
            for (int i = 0; i < zones.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = zones[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildRoom7(Transform root, Day1StartData data, HeroUnit kaito, HeroUnit rei)
        {
            const float x0 = 38f;
            var room = new GameObject("Room7").transform;
            room.SetParent(root, false);

            // The meeting: the room's inside, a little smaller than the walls.
            var meetGo = new GameObject("Meeting");
            meetGo.transform.SetParent(room, false);
            meetGo.transform.position = new Vector3(x0 + 4f, Floor2 + 1f, -4.2f);
            var box = meetGo.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(7.4f, 2f, 7f);
            var meeting = meetGo.AddComponent<RoomMeeting>();
            var so = new SerializedObject(meeting);
            so.FindProperty("data").objectReferenceValue = data;
            so.FindProperty("speaker").objectReferenceValue = kaito != null ? kaito.gameObject : null;
            var listeners = so.FindProperty("listeners");
            listeners.arraySize = 1;
            listeners.GetArrayElementAtIndex(0).objectReferenceValue = rei != null ? rei.gameObject : null;
            so.ApplyModifiedPropertiesWithoutUndo();

            var shelfMat = Mat("M_Day1_Shelf", new Color(0.55f, 0.38f, 0.24f));
            var corkMat = Mat("M_Day1_Cork", new Color(0.72f, 0.58f, 0.4f));

            // Shelf for figures on the east wall, between the desks and the beds.
            var shelfGo = new GameObject("CollectionShelf");
            shelfGo.transform.SetParent(room, false);
            shelfGo.transform.SetPositionAndRotation(new Vector3(x0 + 7.62f, Floor2 + 1.45f, -4.8f), Quaternion.Euler(0f, -90f, 0f));
            Solid(shelfGo.transform, "Board", shelfGo.transform.position, new Vector3(1.8f, 0.04f, 0.3f), shelfMat);
            var shelfSlots = new Transform[6];
            for (int i = 0; i < shelfSlots.Length; i++)
                shelfSlots[i] = Point(shelfGo.transform, $"Slot_{i + 1}",
                    shelfGo.transform.position + new Vector3(-0.02f, 0.02f, -0.75f + i * 0.3f), -90f);
            var shelf = shelfGo.AddComponent<CollectionShelf>();
            SetArray(shelf, "slots", shelfSlots);

            // Trophy wall on the west wall above the beds: 14 spots, 7 x 2.
            var wallGo = new GameObject("TrophyWall");
            wallGo.transform.SetParent(room, false);
            wallGo.transform.SetPositionAndRotation(new Vector3(x0 + 0.23f, Floor2 + 2.2f, -4.2f), Quaternion.Euler(0f, 90f, 0f));
            Visual(wallGo.transform, "Board", wallGo.transform.position, new Vector3(4.6f, 1.3f, 0.03f), corkMat);
            var wallSlots = new Transform[14];
            for (int row = 0; row < 2; row++)
            for (int col = 0; col < 7; col++)
                wallSlots[row * 7 + col] = Point(wallGo.transform, $"Slot_{row * 7 + col + 1}",
                    wallGo.transform.position + new Vector3(0.04f, 0.3f - row * 0.6f, -1.95f + col * 0.65f), 90f);
            var trophies = wallGo.AddComponent<TrophyWall>();
            SetArray(trophies, "slots", wallSlots);
        }

        static void Classmate(Transform parent, string key, string objectName, Vector3 pos, float yaw, string model,
            Dictionary<string, DialogueGraph> dialogues, Dictionary<string, Speaker> speakers, NpcSettings settings)
        {
            var mat = Mat($"M_Day1_{key}", speakers[key].nameColor);
            var npc = Npc(parent, objectName, pos, yaw, mat, false, settings, speakers[key]);
            NpcModelsSetup.Dress(npc.transform, model);
            var talk = npc.AddComponent<DialogueNpc>();
            var so = new SerializedObject(talk);
            so.FindProperty("dialogue").objectReferenceValue = dialogues[$"Day1_Meet_{key}"];
            so.FindProperty("prompt").stringValue = "Познакомиться";
            so.FindProperty("talkedFlag").stringValue = $"day1_met_{key.ToLowerInvariant()}";
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // An NPC like the pranks' ones: root on the ground (NpcActor, SpeakerTag, a capsule collider for E),
        // a "Body" with a capsule and a nose (a model goes there when dressed).
        static GameObject Npc(Transform parent, string name, Vector3 pos, float yaw, Material mat, bool teacher,
            NpcSettings settings, Speaker speaker)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.85f, 0f);
            col.height = 1.7f;
            col.radius = 0.3f;

            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Capsule";
            capsule.transform.SetParent(body, false);
            capsule.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            capsule.transform.localScale = new Vector3(0.5f, 0.85f, 0.5f);
            capsule.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(capsule.GetComponent<Collider>());
            Visual(body, "Nose", root.transform.TransformPoint(new Vector3(0f, 1.45f, 0.25f)), new Vector3(0.12f, 0.12f, 0.15f), mat)
                .transform.rotation = root.transform.rotation;

            var actor = root.AddComponent<NpcActor>();
            PlayerSliceSetup.Set(actor, "settings", settings);
            var so = new SerializedObject(actor);
            so.FindProperty("isTeacher").boolValue = teacher;
            so.ApplyModifiedPropertiesWithoutUndo();
            var tag = root.AddComponent<SpeakerTag>();
            tag.speaker = speaker;
            tag.headHeight = 1.8f;
            return root;
        }

        // ---------------------------------------------------------------- helpers

        static Transform Point(Transform parent, string name, Vector3 pos, float yaw)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            return t;
        }

        static GameObject Visual(Transform parent, string name, Vector3 worldPos, Vector3 size, Material mat)
        {
            var go = Solid(parent, name, worldPos, size, mat);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static GameObject Solid(Transform parent, string name, Vector3 worldPos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, true);
            go.transform.position = worldPos;
            go.transform.rotation = parent.rotation;
            go.transform.localScale = size;
            if (parent.lossyScale != Vector3.one) go.transform.localScale = Vector3.Scale(size, Inverse(parent.lossyScale));
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static void Wall(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.AddComponent<BoxCollider>().size = size;
        }

        static BoxCollider LineZone(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            return box;
        }

        static void SetArray(Object target, string field, Transform[] values)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Material Mat(string name, Color color)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", 0.2f);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Vector3 Inverse(Vector3 v) => new(1f / v.x, 1f / v.y, 1f / v.z);
    }
}
