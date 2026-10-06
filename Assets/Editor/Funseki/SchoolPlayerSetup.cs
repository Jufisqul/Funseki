using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Funseki.School;

namespace Funseki.School.EditorTools
{
    // Makes the Anime Girl the playable character of School_Greybox with the same setup as
    // Day1_Greybox: PlayerController, Hero.controller animations (Idle/Walk/Run/Jump, retargeted
    // through Humanoid), Cinemachine third-person + hold-F first-person cameras. Adds E to open doors.
    // Re-runnable: the previous Player, cameras and Sun in the scene are replaced.
    public static class SchoolPlayerSetup
    {
        public const string GirlFbx = "Assets/Anime Girl/Model/Anime_Girl.fbx";
        const string GirlTexture = "Assets/Anime Girl/Model/Texture_FULL.png";
        const string GirlMaterial = KitGenerator.Root + "Characters/M_AnimeGirl.mat";
        const string ControlsPath = "Assets/_Game/Input/GameControls.inputactions";
        const float MinHeight = 1.4f, MaxHeight = 1.9f, FallbackHeight = 1.65f;

        // Spawn inside the main entrance, facing into the school (plan meters: x east, y south).
        static readonly Vector2 SpawnPlan = new Vector2(26f, 39.5f);

        static readonly string[] OwnedRoots = { "Player", "Main Camera", "CM ThirdPerson", "CM FirstPerson", "CameraRig", "Sun" };

        [MenuItem("Tools/Funseki/Player/1. Setup Anime Girl Import")]
        public static void SetupImport()
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(GirlFbx);
            if (mi == null)
            {
                Debug.LogError("[Funseki] " + GirlFbx + " not found.");
                return;
            }
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.SaveAndReimport();

            // Keep her at a believable height next to the 1.8 m hero and 2.2 m doors.
            float h = MeasureHeight();
            if (h > 0.01f && (h < MinHeight || h > MaxHeight))
            {
                mi.globalScale *= FallbackHeight / h;
                mi.SaveAndReimport();
                Debug.Log($"[Funseki] Anime Girl was {h:F2} m tall, rescaled to {FallbackHeight} m.");
            }

            // The package ships Built-in RP toon shaders that render pink in URP: use one URP Lit material.
            var mat = BuildMaterial();
            foreach (var embedded in AssetDatabase.LoadAllAssetsAtPath(GirlFbx).OfType<Material>())
                mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), mat);
            mi.SaveAndReimport();

            var avatar = AssetDatabase.LoadAllAssetsAtPath(GirlFbx).OfType<Avatar>().FirstOrDefault();
            Debug.Log($"[Funseki] Anime Girl import: height {MeasureHeight():F2} m, avatar human {(avatar != null && avatar.isHuman)}, valid {(avatar != null && avatar.isValid)}");
        }

        [MenuItem("Tools/Funseki/Player/2. Add Player To School")]
        public static void AddPlayer()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GirlFbx);
            var controls = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ControlsPath);
            var animController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(HeroAnimatorBuilder.ControllerPath);
            if (prefab == null || controls == null || animController == null)
            {
                Debug.LogError("[Funseki] Need the Anime Girl FBX, GameControls.inputactions and Hero.controller.");
                return;
            }

            var scene = SchoolBuilder.OpenSchoolScene(false);
            if (!scene.IsValid()) return;
            SceneManager.SetActiveScene(scene);
            foreach (var go in scene.GetRootGameObjects())
                if (OwnedRoots.Contains(go.name)) Object.DestroyImmediate(go);

            PlayerTestSceneBuilder.BuildLighting();
            var player = BuildPlayer(prefab, controls, animController, out var cameraTarget, out var body);
            PlayerTestSceneBuilder.BuildCameras(controls, player, cameraTarget, body);

            // School walls are on the Environment layer: the camera must not pass through them.
            var tp = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "CM ThirdPerson");
            var deoccluder = tp != null ? tp.GetComponent<CinemachineDeoccluder>() : null;
            if (deoccluder != null)
                deoccluder.CollideAgainst = LayerMask.GetMask("Default", KitGenerator.EnvironmentLayer);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Funseki] Anime Girl player added to " + scene.path);
        }

        static GameObject BuildPlayer(GameObject prefab, InputActionAsset controls, RuntimeAnimatorController animController,
            out Transform cameraTarget, out Renderer[] body)
        {
            float height = MeasureHeight();
            if (height < 0.5f) height = FallbackHeight;

            var go = new GameObject("Player") { tag = "Player" };
            go.transform.SetPositionAndRotation(new Vector3(SpawnPlan.x, 0.05f, -SpawnPlan.y), Quaternion.identity);

            var cc = go.AddComponent<CharacterController>();
            cc.height = height;
            cc.radius = 0.28f;
            cc.center = new Vector3(0f, height * 0.5f, 0f);
            cc.stepOffset = 0.35f;
            cc.slopeLimit = 45f;

            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, go.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = animController;
            animator.applyRootMotion = false;
            body = model.GetComponentsInChildren<Renderer>();

            cameraTarget = new GameObject("CameraTarget").transform;
            cameraTarget.SetParent(go.transform, false);
            cameraTarget.localPosition = new Vector3(0f, height * 0.9f, 0f);

            var controller = go.AddComponent<PlayerController>();
            controller.actions = controls;
            var anim = go.AddComponent<PlayerAnimator>();
            anim.player = controller;
            anim.animator = animator;
            go.AddComponent<PlayerInteractor>().actions = controls;
            return go;
        }

        static float MeasureHeight()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GirlFbx);
            if (prefab == null) return 0f;
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                var renderers = go.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) return 0f;
                var b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                return b.size.y;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        static Material BuildMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(GirlMaterial);
            if (mat == null)
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(GirlMaterial));
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, GirlMaterial);
            }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(GirlTexture));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0.2f);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }
    }
}
