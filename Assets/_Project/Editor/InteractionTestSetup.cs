using System.IO;
using Funseki.Core;
using Funseki.Interaction;
using Funseki.Inventory;
using Funseki.Player;
using Funseki.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Funseki.EditorTools
{
    // Tools > Funseki > Interaction: data assets of Funseki.Interaction / Funseki.Inventory and the Interaction_Test scene:
    // the hero, two pickups ("Маркер", "Отвёртка") on tables and a blackboard that reacts only to the marker.
    // Existing data assets are kept (designer edits survive); the scene is rebuilt from scratch.
    // Play the scene directly: it carries its own InventoryService, panel and bark view.
    public static class InteractionTestSetup
    {
        const string ScenePath = "Assets/_Project/Scenes/Interaction_Test.unity";
        const string InteractionData = "Assets/_Project/Data/Interaction";
        const string InventoryData = "Assets/_Project/Data/Inventory";
        const string ItemsData = "Assets/_Project/Data/Inventory/Items";
        const string UIData = "Assets/_Project/Data/UI";
        const string PlaceholderArt = "Assets/_Project/Art/Placeholders/Items";
        const string ItemPrefabs = "Assets/_Project/Prefabs/Items";
        const string FontPath = "Assets/MainMenu/Fonts/GolosText-SemiBold SDF.asset";

        [MenuItem("Tools/Funseki/Interaction/Build Interaction_Test scene")]
        public static void Build()
        {
            foreach (var dir in new[] { InteractionData, ItemsData, UIData, PlaceholderArt, ItemPrefabs })
                Directory.CreateDirectory(dir);

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var red = Mat("M_Item_Red", new Color(0.85f, 0.15f, 0.15f));
            var yellow = Mat("M_Item_Yellow", new Color(0.95f, 0.75f, 0.15f));
            var wood = Mat("M_Table", new Color(0.55f, 0.4f, 0.28f));
            var board = Mat("M_Blackboard", new Color(0.12f, 0.3f, 0.2f));
            var floor = Mat("M_TestFloor", new Color(0.6f, 0.6f, 0.62f));

            var marker = Item("Item_Marker", "marker", "Маркер", "Толстый перманентный маркер. Не отмывается.",
                HandPrefab("Hand_Marker", red, new Vector3(0.03f, 0.15f, 0.03f)));
            var screwdriver = Item("Item_Screwdriver", "screwdriver", "Отвёртка", "Крестовая. Чья-то из кабинета труда.",
                HandPrefab("Hand_Screwdriver", yellow, new Vector3(0.035f, 0.22f, 0.035f)));

            var reaction = Asset<ItemReactionData>($"{InteractionData}/Reaction_TestBoard.asset", r =>
            {
                r.acceptedItems = new[] { marker };
                r.successLine = "Готово. Теперь доска синяя. Навсегда.";
                r.alreadyDoneLine = "Хватит, и так красиво.";
                r.worldFlag = "test_board_marked";
            });
            var inventory = Asset<InventorySettings>($"{InventoryData}/InventorySettings.asset", s =>
            {
                s.font = font;
                s.allItems = new[] { marker, screwdriver };
            });
            var barks = EnsureBarkSettings();
            AssetDatabase.SaveAssets();

            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(PlayerSliceSetup.InputPath);
            var girl = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerSliceSetup.GirlFbx);
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PlayerSliceSetup.ControllerPath);
            if (input == null || girl == null || controller == null)
            {
                Debug.LogError("[InteractionTestSetup] Need GameInput.inputactions, the Anime Girl FBX and Player.controller " +
                               "(run Tools > Funseki > Slice > Rebuild Player Animator once).");
                return;
            }

            var active = SceneManager.GetActiveScene();
            // Rebuild in place if the scene is open in the Editor (it can.t be overwritten from a new one).
            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool wasOpen = scene.isLoaded;
            if (wasOpen)
                foreach (var go in scene.GetRootGameObjects()) Object.DestroyImmediate(go);
            else
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            BuildRoom(floor);

            var services = new GameObject("[Services]");
            Set(services.AddComponent<InventoryService>(), "settings", inventory);
            Set(services.AddComponent<InventoryPanel>(), "settings", inventory);
            Set(services.AddComponent<BarkView>(), "settings", barks);

            var playerSettings = PlayerSliceSetup.EnsureSettings();
            var player = PlayerSliceSetup.BuildPlayer(girl, controller, input, playerSettings, out var pivot, out var eyes, out var body);
            player.transform.SetPositionAndRotation(new Vector3(0f, 0.05f, -2.5f), Quaternion.identity);
            PlayerSliceSetup.BuildCameras(player, pivot, eyes, body, playerSettings);

            // BuildPlayer already added HeroInteractor with InteractionSettings.
            Set(player.AddComponent<HeroItemUser>(), "actions", input);
            Set(player.AddComponent<HeldItemView>(), "animator", player.GetComponentInChildren<Animator>());

            Table("Table_Marker", new Vector3(-1.5f, 0f, 0f), wood);
            Pickup("Pickup_Marker", marker, new Vector3(-1.5f, 0.8f, 0f), new Vector3(0.2f, 0.05f, 0.05f), red, "test_marker_taken");
            Table("Table_Screwdriver", new Vector3(1.5f, 0f, 0f), wood);
            Pickup("Pickup_Screwdriver", screwdriver, new Vector3(1.5f, 0.8f, 0f), new Vector3(0.26f, 0.05f, 0.05f), yellow, "test_screwdriver_taken");

            var boardGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            boardGo.name = "Blackboard";
            boardGo.transform.position = new Vector3(0f, 1.4f, 3.4f);
            boardGo.transform.localScale = new Vector3(2.4f, 1.2f, 0.08f);
            boardGo.GetComponent<Renderer>().sharedMaterial = board;
            Set(boardGo.AddComponent<ItemReactionTarget>(), "data", reaction);

            EditorSceneManager.SaveScene(scene, ScenePath);
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.SaveAssets();
            Debug.Log($"[InteractionTestSetup] {ScenePath} built. Open it and press Play (no Bootstrap needed).");
        }

        // ---------------------------------------------------------------- scene

        static void BuildRoom(Material floorMat)
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

            int env = LayerMask.NameToLayer("Environment");
            var room = new GameObject("Room");
            Box("Floor", room.transform, new Vector3(0f, -0.05f, 0f), new Vector3(10f, 0.1f, 8f), floorMat, env);
            Box("Wall_Back", room.transform, new Vector3(0f, 1.5f, 3.5f), new Vector3(10f, 3f, 0.1f), floorMat, env);
        }

        static void Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, int layer)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (layer >= 0) go.layer = layer;
        }

        static void Table(string name, Vector3 pos, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = pos + Vector3.up * 0.375f;
            go.transform.localScale = new Vector3(0.8f, 0.75f, 0.6f);
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        static void Pickup(string name, ItemData item, Vector3 pos, Vector3 size, Material mat, string flag)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, 25f, 0f);
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            var pickup = go.AddComponent<PickupItem>();
            Set(pickup, "item", item);
            var so = new SerializedObject(pickup);
            so.FindProperty("takenFlag").stringValue = flag;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- assets

        internal static InteractionSettings EnsureInteractionSettings()
        {
            Directory.CreateDirectory(InteractionData);
            return Asset<InteractionSettings>($"{InteractionData}/InteractionSettings.asset", s =>
            {
                s.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                s.highlightMaterial = HighlightMaterial();
            });
        }

        internal static BarkSettings EnsureBarkSettings()
        {
            Directory.CreateDirectory(UIData);
            return Asset<BarkSettings>($"{UIData}/BarkSettings.asset", s => s.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath));
        }

        static T Asset<T>(string path, System.Action<T> init) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<T>();
            init(a);
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        static ItemData Item(string file, string id, string displayName, string description, GameObject handPrefab) =>
            Asset<ItemData>($"{ItemsData}/{file}.asset", i =>
            {
                i.id = id;
                i.displayName = displayName;
                i.description = description;
                i.handPrefab = handPrefab;
                i.handPosition = new Vector3(0f, 0.08f, 0.03f);
            });

        static GameObject HandPrefab(string name, Material mat, Vector3 size)
        {
            string path = $"{ItemPrefabs}/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var root = new GameObject(name);
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Mesh";
            cube.transform.SetParent(root.transform, false);
            cube.transform.localScale = size;
            cube.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static Material Mat(string name, Color color)
        {
            string path = $"{PlaceholderArt}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // Unlit, transparent, additive: brightens whatever it is drawn over.
        static Material HighlightMaterial()
        {
            string path = $"{InteractionData}/M_InteractHighlight.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", new Color(0.28f, 0.24f, 0.12f, 1f));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f);
            m.SetFloat("_SrcBlend", (float)BlendMode.One);
            m.SetFloat("_DstBlend", (float)BlendMode.One);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[InteractionTestSetup] {target.GetType().Name}.{field} not found"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
