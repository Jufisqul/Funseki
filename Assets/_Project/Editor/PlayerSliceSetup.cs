using System.IO;
using System.Linq;
using Funseki.DayCycle;
using Funseki.Player;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using PlayerSettings = Funseki.Player.PlayerSettings;

namespace Funseki.EditorTools
{
    // Tools > Funseki > Slice: makes Slice_Day1 playable for the movement check.
    // - PlayerSettings.asset and Player.controller (Speed / Grounded / Jump) are created if missing.
    // - The school is not copied: a SchoolSceneLoader loads School_Greybox (built from KitSettings) additively at runtime.
    // - The Anime Girl hero with Funseki.Player components and the two Cinemachine cameras are added.
    // Re-runnable: everything this tool owns in Slice_Day1 is replaced. School_Greybox is never opened or changed.
    public static class PlayerSliceSetup
    {
        public const string SettingsPath = "Assets/_Project/Data/PlayerSettings.asset";
        public const string InputPath = "Assets/_Project/Data/Input/GameInput.inputactions";
        public const string ControllerPath = "Assets/_Project/Art/Animation/Player.controller";
        const string SlicePath = CoreScenesSetup.SlicePath;
        const string GirlFbx = "Assets/Anime Girl/Model/Anime_Girl.fbx";
        const string JumpFbx = "Assets/Animation/Jumping Up.fbx";
        const string WalkFbx = "Assets/Animation/Walking (1).fbx";
        const string RunFbx = "Assets/Animation/Run.fbx";
        const float FallbackHeight = 1.65f;

        // The game starts at the main entrance (double door at plan x 24..26.4, y 42), facing the school;
        // SchoolSceneLoader opens the doors as the scene starts.
        static readonly Vector3 EntranceSpawn = new(25.2f, 0.05f, -43.6f);

        static readonly string[] OwnedRoots =
        {
            "School", "SchoolLoader", "Schoolyard", "Sun", "Player", "Main Camera", "CM ThirdPerson", "CM FirstPerson", "CutscenePlaceholder",
            // placeholders from Create Core Scenes
            "Directional Light", "Ground_Placeholder", "Slice_Day1_Marker",
        };

        [MenuItem("Tools/Funseki/Slice/Build Slice_Day1 (player + school loader)")]
        public static void Build()
        {
            var settings = EnsureSettings();
            var controller = BuildAnimatorController(settings);
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            var girl = AssetDatabase.LoadAssetAtPath<GameObject>(GirlFbx);
            if (input == null || girl == null || controller == null)
            {
                Debug.LogError("[PlayerSliceSetup] Need GameInput.inputactions, the Anime Girl FBX and the animation clips.");
                return;
            }

            var active = SceneManager.GetActiveScene();
            var slice = OpenAdditive(SlicePath, out bool openedSlice);
            SceneManager.SetActiveScene(slice);
            foreach (var go in slice.GetRootGameObjects())
                if (OwnedRoots.Contains(go.name)) Object.DestroyImmediate(go);

            BuildLighting();
            new GameObject("SchoolLoader").AddComponent<SchoolSceneLoader>();
            BuildSchoolyard();

            var player = BuildPlayer(girl, controller, input, settings, out var pivot, out var eyes, out var body);
            BuildCameras(player, pivot, eyes, body, settings);
            new GameObject("CutscenePlaceholder").AddComponent<CutscenePlaceholder>();

            EditorSceneManager.MarkSceneDirty(slice);
            EditorSceneManager.SaveScene(slice);
            if (openedSlice) EditorSceneManager.CloseScene(slice, true);
            else if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);

            Debug.Log("[PlayerSliceSetup] Slice_Day1 built: player at the main entrance, School_Greybox loads additively at runtime.");
        }

        // ---------------------------------------------------------------- assets

        static PlayerSettings EnsureSettings()
        {
            var s = AssetDatabase.LoadAssetAtPath<PlayerSettings>(SettingsPath);
            if (s != null) return s;
            s = ScriptableObject.CreateInstance<PlayerSettings>();
            s.cameraCollisionLayers = LayerMask.GetMask("Default", "Environment");
            AssetDatabase.CreateAsset(s, SettingsPath);
            AssetDatabase.SaveAssets();
            return s;
        }

        // Locomotion blend tree (Idle / Walk / Run by Speed in m/s) and an Airborne state for jumps and falls.
        // Re-runnable: overwrites the controller so thresholds follow PlayerSettings speeds.
        [MenuItem("Tools/Funseki/Slice/Rebuild Player Animator")]
        static void RebuildAnimator() => BuildAnimatorController(EnsureSettings());

        static RuntimeAnimatorController BuildAnimatorController(PlayerSettings settings)
        {
            var idle = Clip(JumpFbx, "Idle");
            var jump = Clip(JumpFbx, "Jump");
            var walk = Clip(WalkFbx, "Walk");
            var run = Clip(RunFbx, "Run");
            if (idle == null || jump == null || walk == null || run == null)
            {
                Debug.LogError("[PlayerSliceSetup] Humanoid clips Idle/Jump/Walk/Run not found in Assets/Animation.");
                return null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ControllerPath));
            AssetDatabase.DeleteAsset(ControllerPath);
            var ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            ac.AddParameter(Player.PlayerAnimator.SpeedParam, AnimatorControllerParameterType.Float);
            ac.AddParameter(new AnimatorControllerParameter
            {
                name = Player.PlayerAnimator.GroundedParam, type = AnimatorControllerParameterType.Bool, defaultBool = true,
            });
            ac.AddParameter(Player.PlayerAnimator.JumpParam, AnimatorControllerParameterType.Trigger);

            var sm = ac.layers[0].stateMachine;
            var locomotion = ac.CreateBlendTreeInController("Locomotion", out var tree, 0);
            tree.blendParameter = Player.PlayerAnimator.SpeedParam;
            tree.useAutomaticThresholds = false;
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, settings.walkSpeed);
            tree.AddChild(run, settings.sprintSpeed);
            sm.defaultState = locomotion;

            var airborne = sm.AddState("Airborne");
            airborne.motion = jump;

            var jumpUp = locomotion.AddTransition(airborne);
            jumpUp.hasExitTime = false;
            jumpUp.duration = 0.05f;
            jumpUp.AddCondition(AnimatorConditionMode.If, 0, Player.PlayerAnimator.JumpParam);

            var fall = locomotion.AddTransition(airborne);
            fall.hasExitTime = false;
            fall.duration = 0.1f;
            fall.AddCondition(AnimatorConditionMode.IfNot, 0, Player.PlayerAnimator.GroundedParam);

            var land = airborne.AddTransition(locomotion);
            land.hasExitTime = false;
            land.duration = 0.15f;
            land.AddCondition(AnimatorConditionMode.If, 0, Player.PlayerAnimator.GroundedParam);

            AssetDatabase.SaveAssets();
            return ac;
        }

        static AnimationClip Clip(string fbx, string name) =>
            AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(c => c.name == name);

        // ---------------------------------------------------------------- scene

        static Scene OpenAdditive(string path, out bool opened)
        {
            var scene = SceneManager.GetSceneByPath(path);
            opened = !scene.isLoaded;
            return opened ? EditorSceneManager.OpenScene(path, OpenSceneMode.Additive) : scene;
        }

        static void BuildLighting()
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.75f, 0.83f, 0.9f);
            RenderSettings.ambientEquatorColor = new Color(0.79f, 0.76f, 0.72f);
            RenderSettings.ambientGroundColor = new Color(0.44f, 0.42f, 0.38f);
        }

        // The school greybox ends at its outer walls; give the entrance spawn a porch to stand on.
        static void BuildSchoolyard()
        {
            var yard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            yard.name = "Schoolyard";
            yard.transform.position = new Vector3(25.2f, -0.05f, -45.1f);
            yard.transform.localScale = new Vector3(16f, 0.1f, 6f);
            yard.layer = LayerMask.NameToLayer("Environment");
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/School/Kit/Materials/M_Kit_Floor.mat");
            if (mat != null) yard.GetComponent<Renderer>().sharedMaterial = mat;
            GameObjectUtility.SetStaticEditorFlags(yard, StaticEditorFlags.BatchingStatic);
        }

        static GameObject BuildPlayer(GameObject girl, RuntimeAnimatorController controller, InputActionAsset input,
            PlayerSettings settings, out Transform pivot, out Transform eyes, out Renderer[] body)
        {
            float height = MeasureHeight(girl);
            var go = new GameObject("Player") { tag = "Player" };
            go.transform.SetPositionAndRotation(EntranceSpawn, Quaternion.identity);

            var cc = go.AddComponent<CharacterController>();
            cc.height = height;
            cc.radius = 0.28f;
            cc.center = new Vector3(0f, height * 0.5f + cc.skinWidth, 0f);
            cc.stepOffset = settings.stepOffset;
            cc.slopeLimit = settings.slopeLimit;

            var model = (GameObject)PrefabUtility.InstantiatePrefab(girl, go.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            body = model.GetComponentsInChildren<Renderer>();

            pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(go.transform, false);
            pivot.localPosition = new Vector3(0f, height * settings.pivotHeight, 0f);
            eyes = new GameObject("Eyes").transform;
            eyes.SetParent(go.transform, false);
            eyes.localPosition = new Vector3(0f, height * settings.eyeHeight, 0.1f);

            var reader = go.AddComponent<PlayerInputReader>();
            Set(reader, "actions", input);
            Set(reader, "settings", settings);
            var motor = go.AddComponent<PlayerMotor>();
            Set(motor, "settings", settings);
            Set(motor, "input", reader);
            var anim = go.AddComponent<Player.PlayerAnimator>();
            Set(anim, "settings", settings);
            Set(anim, "motor", motor);
            Set(anim, "animator", animator);
            return go;
        }

        static void BuildCameras(GameObject player, Transform pivot, Transform eyes, Renderer[] body, PlayerSettings settings)
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.position = player.transform.position + new Vector3(0f, 2f, -3.5f);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            camGo.AddComponent<AudioListener>();
            var brain = camGo.AddComponent<CinemachineBrain>();

            var tp = new GameObject("CM ThirdPerson").AddComponent<CinemachineCamera>();
            tp.Follow = pivot;
            tp.Priority = 10;
            tp.Lens.NearClipPlane = 0.05f;
            var orbit = tp.gameObject.AddComponent<CinemachineOrbitalFollow>();
            orbit.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
            var tracker = orbit.TrackerSettings;
            tracker.BindingMode = BindingMode.WorldSpace;
            orbit.TrackerSettings = tracker;
            orbit.HorizontalAxis.Wrap = true;
            tp.gameObject.AddComponent<CinemachineRotationComposer>();
            var deoccluder = tp.gameObject.AddComponent<CinemachineDeoccluder>();
            deoccluder.IgnoreTag = "Player";
            var avoid = deoccluder.AvoidObstacles;
            avoid.Enabled = true;
            avoid.DistanceLimit = 0f;
            avoid.MinimumOcclusionTime = 0f;
            avoid.Strategy = CinemachineDeoccluder.ObstacleAvoidance.ResolutionStrategy.PullCameraForward;
            avoid.MaximumEffort = 4;
            avoid.SmoothingTime = 0f;
            avoid.Damping = 0.2f;
            avoid.DampingWhenOccluded = 0f;
            deoccluder.AvoidObstacles = avoid;

            var fp = new GameObject("CM FirstPerson").AddComponent<CinemachineCamera>();
            fp.Follow = eyes;
            fp.Priority = 20;
            fp.Lens.NearClipPlane = 0.05f;
            fp.gameObject.AddComponent<CinemachineHardLockToTarget>();
            var panTilt = fp.gameObject.AddComponent<CinemachinePanTilt>();
            panTilt.ReferenceFrame = CinemachinePanTilt.ReferenceFrames.World;
            panTilt.PanAxis.Wrap = true;

            var rig = player.AddComponent<PlayerCameraController>();
            Set(rig, "settings", settings);
            Set(rig, "input", player.GetComponent<PlayerInputReader>());
            Set(rig, "motor", player.GetComponent<PlayerMotor>());
            Set(rig, "brain", brain);
            Set(rig, "thirdPersonCamera", tp);
            Set(rig, "firstPersonCamera", fp);
            Set(rig, "pivot", pivot);
            Set(rig, "eyes", eyes);
            var so = new SerializedObject(rig);
            var arr = so.FindProperty("hideInFirstPerson");
            arr.arraySize = body.Length;
            for (int i = 0; i < body.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = body[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            Set(player.GetComponent<PlayerMotor>(), "cameraTransform", camGo.transform);
        }

        static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[PlayerSliceSetup] {target.GetType().Name}.{field} not found"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static float MeasureHeight(GameObject prefab)
        {
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                var renderers = go.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) return FallbackHeight;
                var b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                return b.size.y > 0.5f ? b.size.y : FallbackHeight;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }
    }
}
