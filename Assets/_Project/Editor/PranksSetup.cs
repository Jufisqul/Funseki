using System.Collections.Generic;
using System.IO;
using Funseki.Core;
using Funseki.Dialogue;
using Funseki.NPC;
using Funseki.Pranks;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using PlayerSettings = Funseki.Player.PlayerSettings;

namespace Funseki.EditorTools
{
    // Tools > Funseki > Pranks > Setup pranks in Slice_Day1 (GDD 5.4):
    // - data: NoiseSettings, the two «Поймали» scenes, Prank_Whistle, WhistleRoutine, the reaction Timeline,
    //   Item_Whistle and the courtyard gossip BarkSet — created once and kept (designer edits survive);
    // - [Bootstrap]: WeekJournal; PlayerSettings: hero input blocked in the Caught state;
    // - Slice_Day1: the root "Pranks" (rebuilt every time) with NoiseMeter, CaughtDirector + the bucket spot,
    //   hiding zones, the PE teacher with his post, the bench and the whistle, the kick box on the courtyard
    //   vending machine, three students and the PlayableDirector of the reaction. Other roots are not touched.
    // Positions are School_Greybox plan meters: world x = plan x, world z = -plan y.
    public static class PranksSetup
    {
        const string DataDir = "Assets/_Project/Data/Pranks";
        const string WhistleDir = "Assets/_Project/Data/Pranks/Whistle";
        const string ArtDir = "Assets/_Project/Art/Placeholders/Pranks";
        const string ItemPath = "Assets/_Project/Data/Inventory/Items/Item_Whistle.asset";
        const string GossipPath = "Assets/_Project/Data/Dialogue/BarkSet_WhistleGossip.asset";
        const string NpcSettingsPath = "Assets/_Project/Data/NPC/NpcSettings.asset";
        const string PlayerSettingsPath = "Assets/_Project/Data/PlayerSettings.asset";
        const string FontPath = "Assets/MainMenu/Fonts/GolosText-SemiBold SDF.asset";
        const string RootName = "Pranks";

        // Courtyard (plan x 6..46, y 12..30); the vending machine stands at (16, -29.55) against the south wall, front +Z.
        static readonly Vector3 TeacherPost = new(26f, 0f, -19.5f);     // north edge of the PE area, looking south over it
        static readonly Vector3 FightSpot = new(16f, 0f, -28.55f);      // in front of the machine, facing it
        static readonly Vector3 MachinePos = new(16f, 0f, -29.55f);
        static readonly Vector3 BenchPos = new(14.2f, 0f, -29.1f);      // west of the machine, against the wall
        static readonly Vector3 BucketSpot = new(24f, 0f, -37.6f);      // shoe-locker corridor at the entrance, back to the north wall
        static readonly Vector3[] Students = { new(24f, 0f, -25f), new(27.5f, 0f, -26f), new(29f, 0f, -23f) };
        static readonly float[] StudentYaw = { 30f, -40f, 200f };

        [MenuItem("Tools/Funseki/Pranks/Setup pranks in Slice_Day1")]
        public static void Build()
        {
            foreach (var dir in new[] { DataDir, WhistleDir, ArtDir }) Directory.CreateDirectory(dir);
            var a = EnsureAssets();
            AddJournalToBootstrap();
            BlockInputWhenCaught();

            var active = SceneManager.GetActiveScene();
            var slice = SceneManager.GetSceneByPath(CoreScenesSetup.SlicePath);
            bool opened = !slice.isLoaded;
            if (opened) slice = EditorSceneManager.OpenScene(CoreScenesSetup.SlicePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(slice);
            var keep = LayoutKeeper.Capture(slice);

            foreach (var go in slice.GetRootGameObjects())
                if (go.name == RootName) Object.DestroyImmediate(go);
            var root = new GameObject(RootName).transform;

            BuildNoiseAndCaught(root, a);
            BuildHideZones(root);
            BuildWhistle(root, a);
            NpcModelsSetup.DressSlice(slice);

            keep.Restore();
            EditorSceneManager.MarkSceneDirty(slice);
            EditorSceneManager.SaveScene(slice);
            if (opened) EditorSceneManager.CloseScene(slice, true);
            else if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            AssetDatabase.SaveAssets();
            Debug.Log("[PranksSetup] Slice_Day1: noise meter, «Поймали», hiding zones and the whistle prank placed under 'Pranks'.");
        }

        // ---------------------------------------------------------------- data

        class Assets
        {
            public NoiseSettings noise;
            public CaughtSceneData buckets, laps;
            public PrankData whistle;
            public WhistleRoutineData routine;
            public TimelineAsset reaction;
            public ItemData item;
            public BarkSet gossip;
            public NpcSettings npc;
            public TMP_FontAsset font;
            public Material teacher, student, whistleMat, bench;
        }

        static Assets EnsureAssets()
        {
            var a = new Assets
            {
                npc = AssetDatabase.LoadAssetAtPath<NpcSettings>(NpcSettingsPath),
                font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath),
                teacher = Mat("M_PE_Teacher", new Color(0.65f, 0.12f, 0.12f)),
                student = Mat("M_Student", new Color(0.45f, 0.75f, 1f)),
                whistleMat = Mat("M_Whistle", new Color(0.95f, 0.8f, 0.15f), 0.6f),
                bench = Mat("M_Bench", new Color(0.55f, 0.4f, 0.25f)),
            };

            a.item = Asset<ItemData>(ItemPath, d =>
            {
                d.id = "whistle";
                d.displayName = "Свисток";
                d.description = "Свисток физрука. Ещё тёплый.";
            });

            a.buckets = Asset<CaughtSceneData>($"{DataDir}/CaughtScene_Buckets.asset", d =>
            {
                d.kind = CaughtSceneKind.Buckets;
                d.witnessLines = new[] { "А ну стоять! Вёдра в руки — и в коридор!", "Попался! В коридор, с вёдрами!" };
                d.heroLines = new[] { "Тяжёлые…", "Это непедагогично.", "Я просто мимо шёл." };
            });
            a.laps = Asset<CaughtSceneData>($"{DataDir}/CaughtScene_WhistleLaps.asset", d =>
            {
                d.kind = CaughtSceneKind.Laps;
                d.witnessLines = new[] { "Ах ты ж! А ну отдай! Круги! Бегом!" };
                d.heroLines = new[] { "Ладно, ладно, бегу!" };
                d.lapWitnessLines = new[] { "Фьють!", "Фьють-фьють!", "Выше колени! Фьють!" };
                d.punishmentTime = 6f;
            });
            a.noise = Asset<NoiseSettings>($"{DataDir}/NoiseSettings.asset", d => d.defaultCaughtScene = a.buckets);

            a.reaction = AssetDatabase.LoadAssetAtPath<TimelineAsset>($"{WhistleDir}/Timeline_WhistleReaction.playable")
                         ?? CreateReactionTimeline($"{WhistleDir}/Timeline_WhistleReaction.playable");

            a.whistle = Asset<PrankData>($"{WhistleDir}/Prank_Whistle.asset", d =>
            {
                d.id = "whistle";
                d.title = "Свистать всех наверх";
                d.journalCaption = "Стащили свисток физрука, пока он воевал с автоматом. Без свистка он беспомощен.";
                d.prompt = "Взять свисток";
                d.phaseIds = new[] { "break_1" };
                d.watchRadius = 20f;
                d.noiseWhenSeen = 0;
                d.seenMeansCaught = true;
                d.caughtScene = a.laps;
                d.doneFlag = "prank_whistle_done";
                d.consequenceFlags = new string[0];
                d.flagsAfterReaction = new[] { "day1_break1_goal" };
                d.counters = new[] { "pranks_done" };
                d.rewardItem = a.item;
                d.successLines = new[] { "Свисток мой.", "Тихо… тихо…" };
                d.reactionTimeline = a.reaction;
                d.reactionRadius = 25f;
            });
            a.routine = Asset<WhistleRoutineData>($"{WhistleDir}/WhistleRoutine.asset", d => d.prank = a.whistle);

            a.gossip = Asset<BarkSet>(GossipPath, d =>
            {
                d.triggerRadius = 4f;
                d.cooldown = 10f;
                var after = new[]
                {
                    "Ты видел? Физрук свистел в пальцы!",
                    "Говорят, у него свисток украли.",
                    "Без свистка он как без голоса.",
                    "Он нас что, в пальцы строить будет?",
                };
                foreach (var t in after)
                    d.lines.Add(new BarkLine
                    {
                        text = t,
                        conditions = new List<DialogueCondition> { new() { kind = ConditionKind.FlagIsSet, id = "prank_whistle_done" } },
                    });
                d.lines.Add(new BarkLine
                {
                    text = "Физрук опять с автоматом дерётся.",
                    conditions = new List<DialogueCondition> { new() { kind = ConditionKind.FlagIsNotSet, id = "prank_whistle_done" } },
                });
            });

            EditorUtility.SetDirty(a.noise);
            return a;
        }

        // 10 s: the PE teacher looks for the whistle, panics and whistles through his fingers; the students don't get it.
        static TimelineAsset CreateReactionTimeline(string path)
        {
            var tl = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(tl, path);

            var teacherBarks = tl.CreateTrack<BarkTrack>(null, "Teacher Barks");
            Bark(teacherBarks, 0f, "Мой свисток?!");
            Bark(teacherBarks, 2.2f, "*свистит в пальцы* Фьюююю!");
            Bark(teacherBarks, 4.6f, "Фью! Фью-фью-фью!");
            Bark(teacherBarks, 7.2f, "Построились! Все! ФЬЮ!");

            var teacherBody = tl.CreateTrack<WiggleTrack>(null, "Teacher Body");
            Wiggle(teacherBody, 0f, 2.2f, WiggleMode.LookAround, 70f, 0.8f);
            Wiggle(teacherBody, 2.2f, 2.4f, WiggleMode.Shake, 0.04f, 9f);
            Wiggle(teacherBody, 4.6f, 2.6f, WiggleMode.Hop, 0.25f, 3f);
            Wiggle(teacherBody, 7.2f, 2.8f, WiggleMode.Shake, 0.05f, 10f);

            Bark(tl.CreateTrack<BarkTrack>(null, "Student 1 Barks"), 3f, "Чего это он?");
            Bark(tl.CreateTrack<BarkTrack>(null, "Student 2 Barks"), 5.4f, "Сенсей, вы птиц зовёте?");
            Bark(tl.CreateTrack<BarkTrack>(null, "Student 3 Barks"), 8f, "Я ничего не понял.");

            EditorUtility.SetDirty(tl);
            AssetDatabase.SaveAssets();
            return tl;
        }

        static void Bark(TrackAsset track, float start, string text)
        {
            var clip = track.CreateClip<BarkClip>();
            clip.start = start;
            clip.duration = 2f;
            clip.displayName = text;
            ((BarkClip)clip.asset).text = text;
            EditorUtility.SetDirty(clip.asset);
        }

        static void Wiggle(TrackAsset track, float start, float duration, WiggleMode mode, float amplitude, float frequency)
        {
            var clip = track.CreateClip<WiggleClip>();
            clip.start = start;
            clip.duration = duration;
            clip.displayName = mode.ToString();
            var w = (WiggleClip)clip.asset;
            w.mode = mode;
            w.amplitude = amplitude;
            w.frequency = frequency;
            EditorUtility.SetDirty(w);
        }

        static void BlockInputWhenCaught()
        {
            var ps = AssetDatabase.LoadAssetAtPath<PlayerSettings>(PlayerSettingsPath);
            if (ps == null || ps.IsInputBlocked(GameState.Caught)) return;
            var list = new List<GameState>(ps.inputBlockedStates) { GameState.Caught };
            ps.inputBlockedStates = list.ToArray();
            EditorUtility.SetDirty(ps);
            Debug.Log("[PranksSetup] PlayerSettings: hero input is blocked in the Caught state.");
        }

        static void AddJournalToBootstrap()
        {
            var scene = SceneManager.GetSceneByPath(CoreScenesSetup.BootstrapPath);
            bool wasOpen = scene.isLoaded;
            if (!wasOpen) scene = EditorSceneManager.OpenScene(CoreScenesSetup.BootstrapPath, OpenSceneMode.Additive);
            GameObject boot = null;
            foreach (var go in scene.GetRootGameObjects())
                if (go.GetComponent<Bootstrap>() != null) boot = go;
            if (boot == null) Debug.LogError("[PranksSetup] No Bootstrap object in the Bootstrap scene.");
            else if (boot.GetComponentInChildren<WeekJournal>(true) == null)
            {
                var j = new GameObject("WeekJournal");
                j.transform.SetParent(boot.transform, false);
                j.AddComponent<WeekJournal>();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[PranksSetup] [Bootstrap]: WeekJournal added.");
            }
            if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
        }

        // ---------------------------------------------------------------- scene

        static void BuildNoiseAndCaught(Transform root, Assets a)
        {
            var noise = new GameObject("NoiseMeter");
            noise.transform.SetParent(root, false);
            PlayerSliceSetup.Set(noise.AddComponent<NoiseMeter>(), "settings", a.noise);

            var caught = new GameObject("CaughtDirector");
            caught.transform.SetParent(root, false);
            var spot = new GameObject("BucketSpot").transform;
            spot.SetParent(caught.transform, false);
            spot.SetPositionAndRotation(BucketSpot, Quaternion.Euler(0f, 180f, 0f));
            var director = caught.AddComponent<CaughtDirector>();
            PlayerSliceSetup.Set(director, "defaultScene", a.buckets);
            PlayerSliceSetup.Set(director, "bucketSpot", spot);
            PlayerSliceSetup.Set(director, "font", a.font);
        }

        static void BuildHideZones(Transform root)
        {
            var parent = new GameObject("HideZones").transform;
            parent.SetParent(root, false);
            HideBox(parent, "Hide_WC_M", "Туалет М", new Vector3(55f, 1.5f, -34f), new Vector3(5.6f, 3f, 7.6f));
            HideBox(parent, "Hide_WC_F", "Туалет Ж", new Vector3(55f, 1.5f, -26f), new Vector3(5.6f, 3f, 7.6f));
            // In front of the shoe lockers on the north wall under rooms 14-17.
            HideBox(parent, "Hide_ShoeLockers", "За шкафчиками", new Vector3(16f, 1.5f, -38.1f), new Vector3(4f, 3f, 1.6f));
        }

        static void HideBox(Transform parent, string name, string label, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            var zone = go.AddComponent<HideZone>();
            var so = new SerializedObject(zone);
            so.FindProperty("displayName").stringValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildWhistle(Transform root, Assets a)
        {
            var parent = new GameObject("Prank_Whistle").transform;
            parent.SetParent(root, false);

            var post = Point(parent, "TeacherPost", TeacherPost, 180f);
            var fight = Point(parent, "FightSpot", FightSpot, 180f);

            // The bench and the whistle on it.
            var bench = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bench.name = "WhistleBench";
            bench.transform.SetParent(parent, false);
            bench.transform.position = BenchPos + Vector3.up * 0.22f;
            bench.transform.localScale = new Vector3(1.3f, 0.44f, 0.45f);
            bench.GetComponent<Renderer>().sharedMaterial = a.bench;

            var whistleGo = new GameObject("Whistle");
            whistleGo.transform.SetParent(parent, false);
            whistleGo.transform.SetPositionAndRotation(BenchPos + new Vector3(0.1f, 0.46f, 0.05f), Quaternion.Euler(0f, 35f, 0f));
            var hit = whistleGo.AddComponent<BoxCollider>();
            hit.isTrigger = true;
            hit.center = new Vector3(0f, 0.1f, 0f);
            hit.size = new Vector3(0.5f, 0.3f, 0.5f);
            var visual = WhistleModel(whistleGo.transform, a.whistleMat);
            var trigger = whistleGo.AddComponent<PrankTrigger>();
            PlayerSliceSetup.Set(trigger, "data", a.whistle);
            var tso = new SerializedObject(trigger);
            tso.FindProperty("startAvailable").boolValue = false;
            var visuals = tso.FindProperty("visuals");
            visuals.arraySize = 1;
            visuals.GetArrayElementAtIndex(0).objectReferenceValue = visual;
            tso.ApplyModifiedPropertiesWithoutUndo();

            // A trigger box around the courtyard vending machine: Кайто's kick hits it.
            var kickGo = new GameObject("MachineKick");
            kickGo.transform.SetParent(parent, false);
            kickGo.transform.position = MachinePos + Vector3.up * 0.95f;
            var kickBox = kickGo.AddComponent<BoxCollider>();
            kickBox.isTrigger = true;
            kickBox.size = new Vector3(1.2f, 1.9f, 1.1f);
            var kick = kickGo.AddComponent<KickTarget>();
            var kso = new SerializedObject(kick);
            var lines = kso.FindProperty("kickLines");
            lines.arraySize = 2;
            lines.GetArrayElementAtIndex(0).stringValue = "Н-на, железяка!";
            lines.GetArrayElementAtIndex(1).stringValue = "БДЫЩ.";
            kso.ApplyModifiedPropertiesWithoutUndo();

            // The PE teacher.
            var teacher = Npc(parent, "NPC_PE_Teacher", TeacherPost, 180f, a.teacher, true, a.npc, out var teacherBody);
            var neck = Box(teacher.transform, "Whistle_Neck", new Vector3(0f, 1.2f, 0.27f), new Vector3(0.06f, 0.05f, 0.08f), a.whistleMat);
            var wt = teacher.AddComponent<WhistleTeacher>();
            PlayerSliceSetup.Set(wt, "data", a.routine);
            PlayerSliceSetup.Set(wt, "post", post);
            PlayerSliceSetup.Set(wt, "fightSpot", fight);
            PlayerSliceSetup.Set(wt, "benchWhistle", trigger);
            PlayerSliceSetup.Set(wt, "handWhistle", neck);
            PlayerSliceSetup.Set(wt, "machine", kick);

            // Students in the PE area: they gossip about the whistle until the end of the day.
            var students = new GameObject[Students.Length];
            for (int i = 0; i < Students.Length; i++)
            {
                students[i] = Npc(parent, $"NPC_Student_{i + 1}", Students[i], StudentYaw[i], a.student, false, a.npc, out _);
                PlayerSliceSetup.Set(students[i].AddComponent<BarkTrigger>(), "barks", a.gossip);
            }

            // The world reaction.
            var reactGo = new GameObject("WhistleReaction");
            reactGo.transform.SetParent(parent, false);
            var pd = reactGo.AddComponent<PlayableDirector>();
            pd.playOnAwake = false;
            pd.extrapolationMode = DirectorWrapMode.None;
            pd.playableAsset = a.reaction;
            foreach (var track in a.reaction.GetOutputTracks())
            {
                Object binding = track.name switch
                {
                    "Teacher Barks" => teacher,
                    "Teacher Body" => teacherBody,
                    "Student 1 Barks" => students[0],
                    "Student 2 Barks" => students[1],
                    "Student 3 Barks" => students[2],
                    _ => null,
                };
                if (binding != null) pd.SetGenericBinding(track, binding);
            }
            PlayerSliceSetup.Set(trigger, "reactionDirector", pd);
        }

        static GameObject WhistleModel(Transform parent, Material mat)
        {
            var model = new GameObject("Model");
            model.transform.SetParent(parent, false);
            Box(model.transform, "Body", new Vector3(0f, 0.03f, 0f), new Vector3(0.07f, 0.05f, 0.1f), mat);
            Box(model.transform, "Mouthpiece", new Vector3(0f, 0.035f, 0.075f), new Vector3(0.03f, 0.025f, 0.06f), mat);
            Box(model.transform, "Cord", new Vector3(0.12f, 0.005f, -0.05f), new Vector3(0.2f, 0.01f, 0.01f), mat);
            return model;
        }

        // Root on the ground with NpcActor, a capsule Body (the Timeline wiggles it) and a "nose" showing where it faces.
        static GameObject Npc(Transform parent, string name, Vector3 pos, float yaw, Material mat, bool teacher, NpcSettings settings,
            out Transform body)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));

            var pivot = new GameObject("Body").transform;
            pivot.SetParent(root.transform, false);
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Capsule";
            capsule.transform.SetParent(pivot, false);
            capsule.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            capsule.transform.localScale = new Vector3(0.5f, 0.85f, 0.5f);
            capsule.GetComponent<Renderer>().sharedMaterial = mat;
            Box(pivot, "Nose", new Vector3(0f, 1.45f, 0.25f), new Vector3(0.12f, 0.12f, 0.15f), mat);
            body = pivot;

            var actor = root.AddComponent<NpcActor>();
            PlayerSliceSetup.Set(actor, "settings", settings);
            var so = new SerializedObject(actor);
            so.FindProperty("isTeacher").boolValue = teacher;
            so.FindProperty("reactsToPranks").boolValue = !teacher;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        static Transform Point(Transform parent, string name, Vector3 pos, float yaw)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            return t;
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

        // ---------------------------------------------------------------- assets

        static T Asset<T>(string path, System.Action<T> init) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            init(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static Material Mat(string name, Color color, float metallic = 0f)
        {
            string path = $"{ArtDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Metallic", metallic);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
