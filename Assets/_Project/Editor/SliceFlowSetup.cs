using System.Collections.Generic;
using System.IO;
using Funseki.Core;
using Funseki.DayCycle;
using Funseki.Dialogue;
using Funseki.Heroes;
using Funseki.Save;
using Funseki.UI;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using UnityEngine.UI;

namespace Funseki.EditorTools
{
    // Tools > Funseki > Slice > Setup slice flow (menus, autosave, intro cutscene):
    // - data, created once and then kept (designer edits survive): Data/UI/MenuSettings, Data/Dialogue/Cutscene_Day1_Intro
    //   (DialogueGraph with [TODO] lines), Data/Cutscenes/Cutscene_Day1_Intro (CutsceneData) + its Timeline;
    // - DaySchedule_Day1_Slice: the intro phase ends only on its goal flag (no 0.5 s time limit);
    // - Bootstrap: "Menus" under [Bootstrap] with PauseMenu, SaveIndicator and SliceEndView (rebuilt);
    // - Slice_Day1: the root "Cutscene_Intro" (rebuilt): StoryCutscene + PlayableDirector, the camera rig, the puppet
    //   heroes at the gates and on the porch, the «День 1» title, a placeholder gate with a road to it.
    //   The Timeline tracks are bound by name, so a Timeline edited by hand keeps working.
    public static class SliceFlowSetup
    {
        const string MenuSettingsPath = "Assets/_Project/Data/UI/MenuSettings.asset";
        const string ThemePath = "Assets/_Project/Data/UI/HudTheme.asset";
        const string InputPath = "Assets/_Project/Data/Input/GameInput.inputactions";
        const string SchedulePath = "Assets/_Project/Data/DayCycle/DaySchedule_Day1_Slice.asset";
        const string GraphPath = "Assets/_Project/Data/Dialogue/Cutscene_Day1_Intro.asset";
        const string CutsceneDir = "Assets/_Project/Data/Cutscenes";
        const string CutscenePath = CutsceneDir + "/Cutscene_Day1_Intro.asset";
        const string TimelinePath = CutsceneDir + "/Cutscene_Day1_Intro.playable";
        const string SpeakersDir = "Assets/_Project/Data/Dialogue/Speakers";
        const string HeroesDir = "Assets/_Project/Data/Heroes";
        const string TitleFontPath = "Assets/MainMenu/Fonts/Unbounded-ExtraBold SDF.asset";
        const string BodyFontPath = "Assets/MainMenu/Fonts/GolosText-SemiBold SDF.asset";
        const string FloorMat = "Assets/_Project/School/Kit/Materials/M_Kit_Floor.mat";
        const string WallMat = "Assets/_Project/School/Kit/Materials/M_Kit_Wall.mat";
        const string RootName = "Cutscene_Intro";

        // Track names: the scene objects are bound to them.
        const string CameraTrack = "Camera";
        const string CameraOnTrack = "Camera On";
        const string GateTrack = "Heroes at the gates";
        const string PorchTrack = "Heroes on the porch";
        const string TitleTrack = "Title «День 1»";

        const float Duration = 48f;
        const float CutToFlyover = 6.5f;
        const float CutToPorch = 34.5f;
        const float TitleFrom = 26.5f, TitleTo = 33.5f;
        const float DialogueAt = 37f;

        static readonly Vector3[] GateSpots = { new(25.2f, 0f, -58f), new(24f, 0f, -58.4f), new(26.4f, 0f, -58.4f) };
        static readonly float[] GateYaw = { 0f, 10f, -10f };
        static readonly Vector3[] PorchSpots = { new(25.2f, 0f, -45.4f), new(24.1f, 0f, -45.9f), new(26.3f, 0f, -45.9f) };
        static readonly float[] PorchYaw = { 180f, 130f, 230f };

        // (time, camera position, look-at point). Pairs at a cut make a hard cut.
        static readonly (float t, Vector3 pos, Vector3 look)[] CameraKeys =
        {
            (0f, new(25.2f, 1.5f, -54.6f), new(25.2f, 1.2f, -58f)),
            (CutToFlyover - 0.01f, new(25.2f, 1.55f, -55.5f), new(25.2f, 1.3f, -58f)),
            (CutToFlyover, new(25.2f, 1.8f, -62.5f), new(25.2f, 4f, -42f)),
            (12f, new(25.2f, 8f, -53f), new(25.2f, 4f, -36f)),
            (18f, new(25.2f, 15f, -41f), new(25.2f, 0f, -24f)),
            (24f, new(33f, 14f, -27f), new(24f, 0f, -20f)),
            (29f, new(19f, 14f, -15f), new(26f, 0f, -22f)),
            (CutToPorch - 0.01f, new(14f, 16f, -22f), new(26f, 0f, -22f)),
            (CutToPorch, new(25.2f, 1.6f, -49.4f), new(25.2f, 1.3f, -45.4f)),
            (Duration, new(25.2f, 1.7f, -48.2f), new(25.2f, 1.45f, -44f)),
        };

        [MenuItem("Tools/Funseki/Slice/Setup slice flow (menus, autosave, intro cutscene)")]
        public static void Build()
        {
            var theme = AssetDatabase.LoadAssetAtPath<HudTheme>(ThemePath);
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            if (theme == null || input == null)
            {
                Debug.LogError("[SliceFlowSetup] Need Data/UI/HudTheme (Tools > Funseki > UI > Setup HUD) and GameInput.inputactions.");
                return;
            }
            Directory.CreateDirectory(CutsceneDir);
            var menu = EnsureMenuSettings(input);
            FixSchedule();
            var graph = EnsureIntroDialogue();
            var timeline = EnsureTimeline();
            var data = EnsureCutsceneData(graph, timeline);
            AssetDatabase.SaveAssets();

            SetupBootstrap(theme, menu);
            SetupSlice(data, timeline, input);
            AssetDatabase.SaveAssets();
            Debug.Log("[SliceFlowSetup] Done: menus under [Bootstrap], Cutscene_Intro in Slice_Day1, data in Data/UI, Data/Cutscenes, Data/Dialogue.");
        }

        // ---------------------------------------------------------------- data

        static MenuSettings EnsureMenuSettings(InputActionAsset input)
        {
            var s = AssetDatabase.LoadAssetAtPath<MenuSettings>(MenuSettingsPath);
            if (s != null) return s;
            s = ScriptableObject.CreateInstance<MenuSettings>();
            s.actions = input;
            AssetDatabase.CreateAsset(s, MenuSettingsPath);
            return s;
        }

        static void FixSchedule()
        {
            var schedule = AssetDatabase.LoadAssetAtPath<DaySchedule>(SchedulePath);
            if (schedule == null) { Debug.LogWarning($"[SliceFlowSetup] {SchedulePath} not found."); return; }
            var intro = schedule.phases.Find(p => p.id == "intro");
            if (intro == null || intro.end.maxDuration <= 0f || intro.end.maxDuration > 5f) return;
            intro.end.maxDuration = 0f;
            EditorUtility.SetDirty(schedule);
            Debug.Log("[SliceFlowSetup] DaySchedule_Day1_Slice: the intro now ends on its goal flag (the cutscene), not after 0.5 s.");
        }

        static DialogueGraph EnsureIntroDialogue()
        {
            var g = AssetDatabase.LoadAssetAtPath<DialogueGraph>(GraphPath);
            if (g != null) return g;
            var ryuta = AssetDatabase.LoadAssetAtPath<Speaker>($"{SpeakersDir}/Speaker_Ryuta.asset");
            var rei = AssetDatabase.LoadAssetAtPath<Speaker>($"{SpeakersDir}/Speaker_Rei.asset");
            var kaito = AssetDatabase.LoadAssetAtPath<Speaker>($"{SpeakersDir}/Speaker_Kaito.asset");
            g = ScriptableObject.CreateInstance<DialogueGraph>();
            g.nodes = new List<DialogueNode>
            {
                Node("arrive", ryuta, "[TODO] Рюта: первая реплика у входа в Фунсэки"),
                Node("rei", rei, "[TODO] Рэй: ответ"),
                Node("kaito", kaito, "[TODO] Кайто: ответ"),
                Node("go_in", ryuta, "[TODO] Рюта: «пошли внутрь»", end: true),
            };
            AssetDatabase.CreateAsset(g, GraphPath);
            return g;
        }

        static DialogueNode Node(string id, Speaker speaker, string text, bool end = false) =>
            new() { id = id, speaker = speaker, text = text, end = end };

        static CutsceneData EnsureCutsceneData(DialogueGraph graph, TimelineAsset timeline)
        {
            var d = AssetDatabase.LoadAssetAtPath<CutsceneData>(CutscenePath);
            if (d != null)
            {
                if (d.timeline == null) { d.timeline = timeline; EditorUtility.SetDirty(d); }
                return d;
            }
            d = ScriptableObject.CreateInstance<CutsceneData>();
            d.phaseId = "intro";
            d.timeline = timeline;
            d.dialogue = graph;
            d.dialogueAt = DialogueAt;
            d.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);
            AssetDatabase.CreateAsset(d, CutscenePath);
            return d;
        }

        // The Timeline is generated once; later runs keep it (and bind the scene objects to its tracks by name).
        static TimelineAsset EnsureTimeline()
        {
            var tl = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
            if (tl != null) return tl;

            tl = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(tl, TimelinePath);
            tl.durationMode = TimelineAsset.DurationMode.FixedLength;
            tl.fixedDuration = Duration;

            var clip = CameraClip();
            AssetDatabase.AddObjectToAsset(clip, tl);
            var cam = tl.CreateTrack<AnimationTrack>(null, CameraTrack);
            cam.trackOffset = TrackOffset.ApplyTransformOffsets;
            var tc = cam.CreateClip(clip);
            tc.start = 0;
            tc.duration = Duration;
            tc.displayName = "Gates → yard → porch";

            Activation(tl, CameraOnTrack, 0f, Duration);
            Activation(tl, GateTrack, 0f, CutToPorch);
            Activation(tl, PorchTrack, CutToPorch, Duration);
            Activation(tl, TitleTrack, TitleFrom, TitleTo);
            EditorUtility.SetDirty(tl);
            return tl;
        }

        static void Activation(TimelineAsset tl, string name, float from, float to)
        {
            var track = tl.CreateTrack<ActivationTrack>(null, name);
            track.postPlaybackState = ActivationTrack.PostPlaybackState.Inactive;
            var c = track.CreateDefaultClip();
            c.start = from;
            c.duration = to - from;
            c.displayName = "Active";
        }

        static AnimationClip CameraClip()
        {
            var clip = new AnimationClip { name = "IntroCamera", frameRate = 60f };
            var curves = new AnimationCurve[7];
            for (int i = 0; i < curves.Length; i++) curves[i] = new AnimationCurve();
            Quaternion prev = Quaternion.identity;
            for (int k = 0; k < CameraKeys.Length; k++)
            {
                var (t, pos, look) = CameraKeys[k];
                var rot = Quaternion.LookRotation(look - pos, Vector3.up);
                if (k > 0 && Quaternion.Dot(prev, rot) < 0f) rot = new Quaternion(-rot.x, -rot.y, -rot.z, -rot.w);
                prev = rot;
                float[] v = { pos.x, pos.y, pos.z, rot.x, rot.y, rot.z, rot.w };
                for (int i = 0; i < 7; i++) curves[i].AddKey(new Keyframe(t, v[i]));
            }
            foreach (var c in curves)
                for (int k = 0; k < c.length; k++)
                {
                    // Smooth moves; straight lines across the 0.01 s cut gaps, so the cuts stay hard.
                    bool cut = (k + 1 < c.length && c.keys[k + 1].time - c.keys[k].time < 0.05f)
                               || (k > 0 && c.keys[k].time - c.keys[k - 1].time < 0.05f);
                    var mode = cut ? AnimationUtility.TangentMode.Linear : AnimationUtility.TangentMode.ClampedAuto;
                    AnimationUtility.SetKeyLeftTangentMode(c, k, mode);
                    AnimationUtility.SetKeyRightTangentMode(c, k, mode);
                }
            string[] props = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z",
                "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
            for (int i = 0; i < 7; i++)
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), props[i]), curves[i]);
            return clip;
        }

        // ---------------------------------------------------------------- Bootstrap

        static void SetupBootstrap(HudTheme theme, MenuSettings menu)
        {
            var scene = SceneManager.GetSceneByPath(CoreScenesSetup.BootstrapPath);
            bool wasOpen = scene.isLoaded;
            if (!wasOpen) scene = EditorSceneManager.OpenScene(CoreScenesSetup.BootstrapPath, OpenSceneMode.Additive);

            GameObject boot = null;
            foreach (var go in scene.GetRootGameObjects())
                if (go.GetComponent<Bootstrap>() != null) boot = go;
            if (boot == null)
            {
                Debug.LogError("[SliceFlowSetup] No Bootstrap object in the Bootstrap scene.");
                if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
                return;
            }
            if (boot.GetComponentInChildren<SaveService>(true) == null)
                Debug.LogWarning("[SliceFlowSetup] No SaveService under [Bootstrap]: run Tools > Funseki > Save > Add inventory and save services to Bootstrap.");

            var old = boot.transform.Find("Menus");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var menus = new GameObject("Menus");
            menus.transform.SetParent(boot.transform, false);
            Assign(menus.AddComponent<PauseMenu>(), theme, menu);
            Assign(menus.AddComponent<SaveIndicator>(), theme, menu);
            Assign(menus.AddComponent<SliceEndView>(), theme, menu);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
        }

        static void Assign(Component c, HudTheme theme, MenuSettings menu)
        {
            var so = new SerializedObject(c);
            so.FindProperty("theme").objectReferenceValue = theme;
            so.FindProperty("settings").objectReferenceValue = menu;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- Slice_Day1

        static void SetupSlice(CutsceneData data, TimelineAsset timeline, InputActionAsset input)
        {
            var active = SceneManager.GetActiveScene();
            var slice = SceneManager.GetSceneByPath(CoreScenesSetup.SlicePath);
            bool opened = !slice.isLoaded;
            if (opened) slice = EditorSceneManager.OpenScene(CoreScenesSetup.SlicePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(slice);
            var keep = LayoutKeeper.Capture(slice);

            foreach (var go in slice.GetRootGameObjects())
                if (go.name == RootName) Object.DestroyImmediate(go);
            var root = new GameObject(RootName);

            var director = root.AddComponent<PlayableDirector>();
            director.playableAsset = timeline;
            director.playOnAwake = false;
            director.extrapolationMode = DirectorWrapMode.Hold;

            BuildGate(root.transform);

            // Camera rig: the Animation track moves it, the Activation track switches its CinemachineCamera on.
            var rig = new GameObject("IntroCamera");
            rig.transform.SetParent(root.transform, false);
            rig.AddComponent<Animator>();
            var cam = rig.AddComponent<CinemachineCamera>();
            cam.Priority = 40; // below the dialogue camera (DialogueSettings.cameraPriority 50), above gameplay (10)
            var lens = cam.Lens;
            lens.FieldOfView = 50f;
            lens.NearClipPlane = 0.1f;
            cam.Lens = lens;
            rig.transform.SetPositionAndRotation(CameraKeys[0].pos, Quaternion.LookRotation(CameraKeys[0].look - CameraKeys[0].pos));
            rig.SetActive(false);

            var gate = Puppets(root.transform, "Heroes_Gates", GateSpots, GateYaw);
            var porch = Puppets(root.transform, "Heroes_Porch", PorchSpots, PorchYaw);
            var title = BuildTitle(root.transform, out var titleText, out var subtitleText);

            foreach (var track in timeline.GetOutputTracks())
            {
                Object target = track.name switch
                {
                    CameraTrack => rig.GetComponent<Animator>(),
                    CameraOnTrack => rig,
                    GateTrack => gate,
                    PorchTrack => porch,
                    TitleTrack => title,
                    _ => null,
                };
                if (target != null) director.SetGenericBinding(track, target);
                else Debug.LogWarning($"[SliceFlowSetup] Timeline track '{track.name}' has nothing to bind to; bind it by hand.");
            }

            var cutscene = root.AddComponent<StoryCutscene>();
            var so = new SerializedObject(cutscene);
            so.FindProperty("data").objectReferenceValue = data;
            so.FindProperty("director").objectReferenceValue = director;
            so.FindProperty("actions").objectReferenceValue = input;
            so.FindProperty("dialogueHero").objectReferenceValue = porch.transform.GetChild(0).gameObject;
            so.FindProperty("dialogueListener").objectReferenceValue = porch.transform.GetChild(2).gameObject;
            so.FindProperty("titleText").objectReferenceValue = titleText;
            so.FindProperty("subtitleText").objectReferenceValue = subtitleText;
            so.ApplyModifiedPropertiesWithoutUndo();

            keep.Restore();
            EditorSceneManager.MarkSceneDirty(slice);
            EditorSceneManager.SaveScene(slice);
            if (opened) EditorSceneManager.CloseScene(slice, true);
            else if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
        }

        // Placeholder school gates south of the porch, with a road from them to the main entrance.
        static void BuildGate(Transform root)
        {
            var g = new GameObject("Gate_Placeholder");
            g.transform.SetParent(root, false);
            var floor = AssetDatabase.LoadAssetAtPath<Material>(FloorMat);
            var wall = AssetDatabase.LoadAssetAtPath<Material>(WallMat);
            Block(g.transform, "Road", new Vector3(25.2f, -0.05f, -55.6f), new Vector3(16f, 0.1f, 15f), floor);
            Block(g.transform, "Pillar_W", new Vector3(21.4f, 1.4f, -60.5f), new Vector3(0.9f, 2.8f, 0.9f), wall);
            Block(g.transform, "Pillar_E", new Vector3(29f, 1.4f, -60.5f), new Vector3(0.9f, 2.8f, 0.9f), wall);
            Block(g.transform, "Fence_W", new Vector3(19f, 0.6f, -60.5f), new Vector3(4f, 1.2f, 0.3f), wall);
            Block(g.transform, "Fence_E", new Vector3(31.4f, 0.6f, -60.5f), new Vector3(4f, 1.2f, 0.3f), wall);
            Block(g.transform, "Plate", new Vector3(21.4f, 2f, -60.04f), new Vector3(0.6f, 0.9f, 0.04f), floor);
        }

        static void Block(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = name;
            b.transform.SetParent(parent, false);
            b.transform.position = pos;
            b.transform.localScale = size;
            int layer = LayerMask.NameToLayer("Environment");
            if (layer >= 0) b.layer = layer;
            if (mat != null) b.GetComponent<Renderer>().sharedMaterial = mat;
            GameObjectUtility.SetStaticEditorFlags(b, StaticEditorFlags.BatchingStatic);
        }

        // The three hero models (no gameplay components), Ryuta / Rei / Kaito in the spot order.
        static GameObject Puppets(Transform root, string name, Vector3[] spots, float[] yaw)
        {
            var group = new GameObject(name);
            group.transform.SetParent(root, false);
            string[] ids = { "Ryuta", "Rei", "Kaito" };
            for (int i = 0; i < ids.Length; i++)
            {
                var hero = AssetDatabase.LoadAssetAtPath<HeroData>($"{HeroesDir}/Hero_{ids[i]}.asset");
                GameObject puppet = hero != null && hero.prefab != null
                    ? (GameObject)PrefabUtility.InstantiatePrefab(hero.prefab)
                    : GameObject.CreatePrimitive(PrimitiveType.Capsule);
                if (hero == null || hero.prefab == null) Debug.LogWarning($"[SliceFlowSetup] Hero_{ids[i]} has no model; a capsule stands in.");
                puppet.name = $"Puppet_{ids[i]}";
                puppet.transform.SetParent(group.transform, false);
                puppet.transform.SetPositionAndRotation(spots[i], Quaternion.Euler(0f, yaw[i], 0f));
                foreach (var col in puppet.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
            }
            group.SetActive(false);
            return group;
        }

        static GameObject BuildTitle(Transform root, out TMP_Text title, out TMP_Text subtitle)
        {
            var go = new GameObject("Title_Day1", typeof(RectTransform));
            go.transform.SetParent(root, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            title = Text(go.transform, "Title", AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontPath), 150f, new Vector2(0f, 40f));
            subtitle = Text(go.transform, "Subtitle", AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath), 44f, new Vector2(0f, -70f));
            title.text = "День 1";
            subtitle.text = "";
            go.SetActive(false);
            return go;
        }

        static TMP_Text Text(Transform parent, string name, TMP_FontAsset font, float size, Vector2 pos)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos;
            r.sizeDelta = new Vector2(1600f, size * 1.4f);
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center;
            t.color = new Color(1f, 0.98f, 0.93f, 1f);
            t.outlineColor = new Color(0.08f, 0.06f, 0.1f, 0.9f);
            t.outlineWidth = 0.2f;
            t.raycastTarget = false;
            return t;
        }
    }
}
