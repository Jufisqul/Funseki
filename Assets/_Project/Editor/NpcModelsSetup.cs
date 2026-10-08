using System.IO;
using System.Linq;
using Funseki.NPC;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Funseki.EditorTools
{
    // Tools > Funseki > NPC > Import NPC models and dress the slice NPCs:
    // - the low-poly NPC models (Art/Characters/NPC/*.fbx, built by Art/Characters/NPC/build_npcs.py in Blender):
    //   Humanoid import, every model material remapped to a shared URP Lit material in Art/Characters/Materials;
    // - Art/Animation/NPC.controller: Idle / Walk / Run blended by Speed (the project's Mixamo clips), created once;
    // - Prefabs/NPC/<model>.prefab: model + Animator + NpcLocomotion (rebuilt every time);
    // - the capsule NPCs get a model under their "Body" (the capsule keeps its collider, its renderer is switched off):
    //   Slice_Day1 (the Pranks courtyard cast and the HeroesTest porch) and the Fizra lesson prefab.
    // PranksSetup and LessonsSetup call DressSlice / DressLessonPrefab, so rebuilding them keeps the models.
    public static class NpcModelsSetup
    {
        const string ModelDir = "Assets/_Project/Art/Characters/NPC";
        const string MaterialDir = "Assets/_Project/Art/Characters/Materials";
        const string PrefabDir = "Assets/_Project/Prefabs/NPC";
        const string ControllerPath = "Assets/_Project/Art/Animation/NPC.controller";
        const string AnimDir = "Assets/ThirdParty/Models/Animation";
        const string NpcSettingsPath = "Assets/_Project/Data/NPC/NpcSettings.asset";
        const string LessonPrefabPath = "Assets/_Project/Prefabs/Lessons/Lesson_Fizra.prefab";
        const string ModelChild = "Model";

        public const string Toyoda = "NPC_Toyoda", Joker = "NPC_Joker", Nerd = "NPC_Nerd", Jock = "NPC_Jock",
            Yankee = "NPC_Yankee", GossipA = "NPC_Gossip_A", GossipB = "NPC_Gossip_B";
        static readonly string[] All = { Toyoda, Joker, Nerd, Jock, Yankee, GossipA, GossipB };
        static readonly string[] Crowd = { Jock, GossipA, Nerd, GossipB, Yankee };

        // Scene NPC (by object name) -> model.
        static readonly (string npc, string model)[] SliceCast =
        {
            ("NPC_PE_Teacher", Toyoda),
            ("NPC_Student_1", GossipA),
            ("NPC_Student_2", Yankee),
            ("NPC_Student_3", Jock),
            ("NPC_TestStudent", Nerd),
            ("NPC_TestTeacher", Toyoda),
        };

        // The whistle and the tear sit on the PE teacher's model: lips at 1.655 m, eyes at 1.72 m.
        static readonly Vector3 WhistleAtMouth = new(0f, 1.655f, 0.19f);
        static readonly Vector3 TearUnderEye = new(0.055f, 1.71f, 0.17f);

        [MenuItem("Tools/Funseki/NPC/Import NPC models and dress the slice NPCs")]
        public static void Build()
        {
            EnsurePrefabs();
            DressLessonPrefab();

            var active = SceneManager.GetActiveScene();
            var slice = SceneManager.GetSceneByPath(CoreScenesSetup.SlicePath);
            bool opened = !slice.isLoaded;
            if (opened) slice = EditorSceneManager.OpenScene(CoreScenesSetup.SlicePath, OpenSceneMode.Additive);
            DressSlice(slice);
            EditorSceneManager.MarkSceneDirty(slice);
            EditorSceneManager.SaveScene(slice);
            if (opened) EditorSceneManager.CloseScene(slice, true);
            else if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            AssetDatabase.SaveAssets();
            Debug.Log("[NpcModelsSetup] NPC models imported; Slice_Day1 NPCs and Lesson_Fizra dressed.");
        }

        // ---------------------------------------------------------------- imports and prefabs

        public static void EnsurePrefabs()
        {
            Directory.CreateDirectory(MaterialDir);
            Directory.CreateDirectory(PrefabDir);
            var controller = EnsureController();
            var settings = AssetDatabase.LoadAssetAtPath<NpcSettings>(NpcSettingsPath);
            foreach (var name in All)
            {
                string path = $"{ModelDir}/{name}.fbx";
                if (!SetupModel(path)) continue;
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
                go.name = name;
                var animator = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                var loco = go.AddComponent<NpcLocomotion>();
                PlayerSliceSetup.Set(loco, "settings", settings);
                PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{name}.prefab");
                Object.DestroyImmediate(go);
            }
        }

        static bool SetupModel(string path)
        {
            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi == null)
            {
                Debug.LogWarning($"[NpcModelsSetup] No model at {path} (run Art/Characters/NPC/build_npcs.py in Blender).");
                return false;
            }
            bool changed = mi.animationType != ModelImporterAnimationType.Human || mi.importAnimation
                           || mi.materialImportMode != ModelImporterMaterialImportMode.ImportStandard;
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            if (changed) mi.SaveAndReimport();

            // Materials are named after the Blender palette keys, so all NPCs share one URP material per colour.
            bool remapped = false;
            foreach (var embedded in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
            {
                var mat = SharedMaterial(embedded);
                mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), mat);
                remapped = true;
            }
            if (remapped) mi.SaveAndReimport();

            var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isHuman || !avatar.isValid)
                Debug.LogWarning($"[NpcModelsSetup] {path}: the Humanoid avatar is not valid, animations won't play.");
            return true;
        }

        static Material SharedMaterial(Material embedded)
        {
            string path = $"{MaterialDir}/M_NPC_{embedded.name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            Color color = embedded.HasProperty("_BaseColor") ? embedded.GetColor("_BaseColor") : embedded.color;
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", embedded.name == "Gold" ? 0.6f : 0.15f);
            mat.SetFloat("_Metallic", embedded.name == "Gold" ? 0.8f : 0f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static AnimatorController EnsureController()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (existing != null) return existing;
            var c = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            c.AddParameter("Speed", AnimatorControllerParameterType.Float);
            var state = c.CreateBlendTreeInController("Locomotion", out var tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(Clip("Idle"), 0f);
            tree.AddChild(Clip("Walk"), 1.4f);
            tree.AddChild(Clip("Run"), 4f);
            c.layers[0].stateMachine.defaultState = state;
            EditorUtility.SetDirty(c);
            return c;
        }

        static AnimationClip Clip(string file) =>
            AssetDatabase.LoadAllAssetsAtPath($"{AnimDir}/{file}.fbx").OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview"));

        // ---------------------------------------------------------------- dressing

        public static void DressSlice(Scene slice)
        {
            int n = 0;
            foreach (var root in slice.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var entry = SliceCast.FirstOrDefault(c => c.npc == t.name);
                if (entry.npc == null || t.GetComponent<NpcActor>() == null) continue;
                if (Dress(t, entry.model) == null) continue;
                n++;
                if (t.Find("Whistle_Neck") is { } whistle) whistle.localPosition = WhistleAtMouth;
            }
            Debug.Log($"[NpcModelsSetup] {slice.name}: {n} NPCs dressed.");
        }

        public static void DressLessonPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(LessonPrefabPath) == null) return;
            var root = PrefabUtility.LoadPrefabContents(LessonPrefabPath);
            try
            {
                var teacher = root.transform.Find("Teacher");
                if (teacher != null && Dress(teacher, Toyoda) is { } model)
                {
                    var body = teacher.Find("Body");
                    if (body.Find("Whistle") is { } whistle) whistle.localPosition = WhistleAtMouth;
                    if (body.Find("Tear") is { } tear) tear.localPosition = TearUnderEye;
                    if (body.Find("Arm") is { } arm)
                    {
                        arm.localPosition = new Vector3(0.3f, 1.42f, 0f);   // the model's right shoulder
                        Hide(arm.Find("Sleeve"));
                        var pointer = model.AddComponent<NpcArmPointer>();
                        pointer.SetSource(arm);
                        PlayerSliceSetup.Set(pointer, "settings", AssetDatabase.LoadAssetAtPath<NpcSettings>(NpcSettingsPath));
                    }
                }
                if (root.transform.Find("Helper") is { } helper) Dress(helper, Joker);
                if (root.transform.Find("Students") is { } line)
                    for (int i = 0; i < line.childCount; i++) Dress(line.GetChild(i), Crowd[i % Crowd.Length]);
                PrefabUtility.SaveAsPrefabAsset(root, LessonPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Puts the model under the NPC's "Body" (the pivot Timelines and puppets move) and hides the placeholder shapes.
        internal static GameObject Dress(Transform npc, string modelName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{modelName}.prefab");
            if (prefab == null)
            {
                Debug.LogWarning($"[NpcModelsSetup] No prefab {modelName}; run Tools > Funseki > NPC > Import NPC models first.");
                return null;
            }
            var body = npc.Find("Body") ?? npc;
            if (body.Find(ModelChild) is { } old) Object.DestroyImmediate(old.gameObject);
            Hide(body.Find("Capsule"));
            Hide(body.Find("Nose"));
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, body);
            model.name = ModelChild;
            model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            return model;
        }

        static void Hide(Transform t)
        {
            if (t != null && t.GetComponent<Renderer>() is { } r) r.enabled = false;
        }
    }
}
