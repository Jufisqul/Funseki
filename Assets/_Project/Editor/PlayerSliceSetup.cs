using System.IO;
using System.Linq;
using Funseki.DayCycle;
using Funseki.Interaction;
using Funseki.Player;
using Funseki.UI;
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
    // - PlayerSettings.asset is created if missing; Player.controller (Speed / Grounded / Jump / Talking / Hit) is rebuilt
    //   from Assets/Models/Animation.
    // - The school is not copied: a SchoolSceneLoader loads School_Greybox (built from KitSettings) additively at runtime.
    // - The three heroes (HeroesSetup: Рюта, Рэй, Кайто with Funseki.Player + Funseki.Heroes), the two Cinemachine
    //   cameras and test content for the Q actions (two NPCs and an item) are added.
    // Re-runnable: everything this tool owns in Slice_Day1 is replaced. School_Greybox is never opened or changed.
    public static class PlayerSliceSetup
    {
        public const string SettingsPath = "Assets/_Project/Data/PlayerSettings.asset";
        public const string InputPath = "Assets/_Project/Data/Input/GameInput.inputactions";
        public const string ControllerPath = "Assets/_Project/Art/Animation/Player.controller";
        const string SlicePath = CoreScenesSetup.SlicePath;
        internal const string GirlFbx = "Assets/Models/Rey/Model/Anime_Girl.fbx";
        const string AnimDir = "Assets/Models/Animation";
        const string JumpFbx = AnimDir + "/Jumping Up.fbx";
        const string IdleFbx = AnimDir + "/Idle.fbx";
        const string WalkFbx = AnimDir + "/Walk.fbx";
        const string OldWalkFbx = AnimDir + "/Walking (1).fbx";
        const string RunFbx = AnimDir + "/Run.fbx";
        const string TalkFbx = AnimDir + "/Talking.fbx";
        const string HitFbx = AnimDir + "/Getting Hit.fbx";
        const float FallbackHeight = 1.65f;

        // The game starts inside the school, in the entrance hall (corridor_s_1f) just past the main double door
        // (plan x 24..26.4, y 42), facing the passage to the courtyard. Followers stand 1.9 m behind, clear of
        // the door leaves that SchoolSceneLoader swings inward as the scene starts.
        static readonly Vector3 EntranceSpawn = new(25.2f, 0.05f, -38.2f);

        static readonly string[] OwnedRoots =
        {
            "School", "SchoolLoader", "Schoolyard", "Sun", "Player", "Main Camera", "CM ThirdPerson", "CM FirstPerson", "CutscenePlaceholder", "HeroBarks",
            // three heroes and their test content (HeroesSetup)
            "Heroes", "HeroesTest", "CameraFollowPoint",
            "DayCycle", "LessonEntrance_fizra",
            // placeholders from Create Core Scenes
            "Directional Light", "Ground_Placeholder", "Slice_Day1_Marker",
        };

        [MenuItem("Tools/Funseki/Slice/Build Slice_Day1 (player + school loader)")]
        public static void Build()
        {
            var settings = EnsureSettings();
            var controller = BuildAnimatorController(settings);
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            if (input == null || controller == null)
            {
                Debug.LogError("[PlayerSliceSetup] Need GameInput.inputactions and the animation clips in " + AnimDir);
                return;
            }
            // Hero data first: it may reimport models and write portraits, which must not happen with the scene half-built.
            var heroAssets = HeroesSetup.EnsureAssets();

            var active = SceneManager.GetActiveScene();
            var slice = OpenAdditive(SlicePath, out bool openedSlice);
            SceneManager.SetActiveScene(slice);
            foreach (var go in slice.GetRootGameObjects())
                if (OwnedRoots.Contains(go.name)) Object.DestroyImmediate(go);

            BuildLighting();
            new GameObject("SchoolLoader").AddComponent<SchoolSceneLoader>();
            BuildSchoolyard();

            // Рюта, Рэй and Кайто (Funseki.Heroes); the camera starts on the hero from HeroSettings.startHero.
            var leader = HeroesSetup.BuildHeroes(heroAssets, controller, input, settings, EntranceSpawn, out var heroes);
            var camTransform = BuildCameras(leader, leader.transform.Find("CameraPivot"), leader.transform.Find("Eyes"),
                leader.GetComponentsInChildren<Renderer>(), settings);
            foreach (var h in heroes) Set(h.GetComponent<PlayerMotor>(), "cameraTransform", camTransform);
            HeroesSetup.BuildTestContent(heroAssets);
            // Day phases and the bell (Funseki.DayCycle); its intro phase replaces the old CutscenePlaceholder.
            DayCycleSetup.AddToActiveScene();
            // The hero.s lines ("Заперто"). Moves under [Bootstrap] together with the inventory service later.
            Set(new GameObject("HeroBarks").AddComponent<BarkView>(), "settings", InteractionTestSetup.EnsureBarkSettings());

            EditorSceneManager.MarkSceneDirty(slice);
            EditorSceneManager.SaveScene(slice);
            if (openedSlice) EditorSceneManager.CloseScene(slice, true);
            else if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);

            Debug.Log("[PlayerSliceSetup] Slice_Day1 built: player at the main entrance, School_Greybox loads additively at runtime.");
        }

        // ---------------------------------------------------------------- assets

        internal static PlayerSettings EnsureSettings()
        {
            var s = AssetDatabase.LoadAssetAtPath<PlayerSettings>(SettingsPath);
            if (s != null) return s;
            s = ScriptableObject.CreateInstance<PlayerSettings>();
            s.cameraCollisionLayers = LayerMask.GetMask("Default", "Environment");
            AssetDatabase.CreateAsset(s, SettingsPath);
            AssetDatabase.SaveAssets();
            return s;
        }

        // Locomotion blend tree (Idle / Walk / Run by Speed in m/s), an Airborne state for jumps and falls,
        // Talking (bool, loops while the hero is in a dialogue) and Hit (trigger, plays once).
        // Clips come from Assets/Models/Animation; Mixamo files imported as Generic are switched to Humanoid first.
        // Re-runnable: overwrites the controller so thresholds follow PlayerSettings speeds.
        [MenuItem("Tools/Funseki/Slice/Rebuild Player Animator")]
        static void RebuildAnimator() => BuildAnimatorController(EnsureSettings());

        internal static RuntimeAnimatorController BuildAnimatorController(PlayerSettings settings)
        {
            EnsureHumanoidClip(IdleFbx, "Idle", true);
            EnsureHumanoidClip(WalkFbx, "Walk", true);
            EnsureHumanoidClip(TalkFbx, "Talking", true);
            EnsureHumanoidClip(HitFbx, "Hit", false);

            var idle = Clip(IdleFbx, "Idle") ?? Clip(JumpFbx, "Idle");
            var jump = Clip(JumpFbx, "Jump");
            var walk = Clip(WalkFbx, "Walk") ?? Clip(OldWalkFbx, "Walk");
            var run = Clip(RunFbx, "Run");
            var talk = Clip(TalkFbx, "Talking");
            var hit = Clip(HitFbx, "Hit");
            if (idle == null || jump == null || walk == null || run == null)
            {
                Debug.LogError("[PlayerSliceSetup] Humanoid clips Idle/Jump/Walk/Run not found in " + AnimDir);
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
            ac.AddParameter(Player.PlayerAnimator.TalkingParam, AnimatorControllerParameterType.Bool);
            ac.AddParameter(Player.PlayerAnimator.HitParam, AnimatorControllerParameterType.Trigger);

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

            if (talk != null)
            {
                var talking = sm.AddState("Talking");
                talking.motion = talk;
                var toTalk = locomotion.AddTransition(talking);
                toTalk.hasExitTime = false;
                toTalk.duration = 0.25f;
                toTalk.AddCondition(AnimatorConditionMode.If, 0, Player.PlayerAnimator.TalkingParam);
                var fromTalk = talking.AddTransition(locomotion);
                fromTalk.hasExitTime = false;
                fromTalk.duration = 0.25f;
                fromTalk.AddCondition(AnimatorConditionMode.IfNot, 0, Player.PlayerAnimator.TalkingParam);
            }

            if (hit != null)
            {
                var hitState = sm.AddState("Hit");
                hitState.motion = hit;
                var toHit = sm.AddAnyStateTransition(hitState);
                toHit.hasExitTime = false;
                toHit.duration = 0.05f;
                toHit.canTransitionToSelf = false;
                toHit.AddCondition(AnimatorConditionMode.If, 0, Player.PlayerAnimator.HitParam);
                var back = hitState.AddTransition(locomotion);
                back.hasExitTime = true;
                back.exitTime = 0.9f;
                back.duration = 0.2f;
            }

            AssetDatabase.SaveAssets();
            return ac;
        }

        // Mixamo FBX dropped in as Generic: make it Humanoid with its own avatar and name / loop its one clip.
        // The hero is moved by code (root motion is off), so the clip must play in place:
        // - horizontal motion and turning are NOT baked into the pose: they go to root motion and are dropped,
        //   otherwise a walk that moves forward slides back at every loop and a curving walk looks crooked;
        // - height IS baked, measured from the feet, so the feet stay on the ground.
        static void EnsureHumanoidClip(string fbx, string clipName, bool loop)
        {
            if (AssetImporter.GetAtPath(fbx) is not ModelImporter importer) return;
            var clips = importer.clipAnimations;
            if (importer.animationType == ModelImporterAnimationType.Human && clips.Length == 1 && IsInPlace(clips[0], clipName, loop)) return;

            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
                // The default takes are only known after the model is imported as Humanoid once.
                importer.SaveAndReimport();
                clips = importer.clipAnimations;
            }
            var source = clips.Length > 0 ? clips : importer.defaultClipAnimations;
            if (source.Length == 0) { Debug.LogWarning($"[PlayerSliceSetup] No animation in {fbx}"); return; }

            var clip = source[0];
            clip.name = clipName;
            clip.loopTime = loop;
            clip.lockRootRotation = false;
            clip.lockRootPositionXZ = false;
            clip.lockRootHeightY = true;
            clip.keepOriginalPositionY = false;
            clip.heightFromFeet = true;
            importer.clipAnimations = new[] { clip };
            importer.SaveAndReimport();
            Debug.Log($"[PlayerSliceSetup] {fbx}: Humanoid, clip '{clipName}' in place{(loop ? ", loop" : "")}.");
        }

        static bool IsInPlace(ModelImporterClipAnimation c, string name, bool loop) =>
            c.name == name && c.loopTime == loop && !c.lockRootRotation && !c.lockRootPositionXZ
            && c.lockRootHeightY && c.heightFromFeet && !c.keepOriginalPositionY;

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

        internal static GameObject BuildPlayer(GameObject girl, RuntimeAnimatorController controller, InputActionAsset input,
            PlayerSettings settings, out Transform pivot, out Transform eyes, out Renderer[] body)
        {
            var go = BuildHero("Player", girl, controller, input, settings, EntranceSpawn, Quaternion.identity);
            pivot = go.transform.Find(PlayerCameraController.PivotName);
            eyes = go.transform.Find(PlayerCameraController.EyesName);
            body = go.GetComponentsInChildren<Renderer>();
            return go;
        }

        // One controllable hero: CharacterController sized to the model, the model with Player.controller,
        // camera pivot and eyes, Funseki.Player movement and HeroInteractor (E).
        internal static GameObject BuildHero(string name, GameObject modelPrefab, RuntimeAnimatorController controller,
            InputActionAsset input, PlayerSettings settings, Vector3 position, Quaternion rotation)
        {
            float height = MeasureHeight(modelPrefab);
            var go = new GameObject(name) { tag = "Player" };
            go.transform.SetPositionAndRotation(position, rotation);

            var cc = go.AddComponent<CharacterController>();
            cc.height = height;
            cc.radius = 0.28f;
            cc.center = new Vector3(0f, height * 0.5f + cc.skinWidth, 0f);
            cc.stepOffset = settings.stepOffset;
            cc.slopeLimit = settings.slopeLimit;

            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, go.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            var animator = model.GetComponentInChildren<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            var pivot = new GameObject(PlayerCameraController.PivotName).transform;
            pivot.SetParent(go.transform, false);
            pivot.localPosition = new Vector3(0f, height * settings.pivotHeight, 0f);
            var eyes = new GameObject(PlayerCameraController.EyesName).transform;
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

            // E: doors and every other IInteractable (Funseki.Interaction).
            var interactor = go.AddComponent<HeroInteractor>();
            Set(interactor, "actions", input);
            Set(interactor, "settings", InteractionTestSetup.EnsureInteractionSettings());
            return go;
        }

        internal static Transform BuildCameras(GameObject player, Transform pivot, Transform eyes, Renderer[] body, PlayerSettings settings)
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

            // On the camera, not the hero: with three heroes it re-targets itself on every switch.
            var rig = camGo.AddComponent<PlayerCameraController>();
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
            return camGo.transform;
        }

        internal static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[PlayerSliceSetup] {target.GetType().Name}.{field} not found"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static float MeasureHeight(GameObject prefab)
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
