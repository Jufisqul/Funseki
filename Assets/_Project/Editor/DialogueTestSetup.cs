using System.Collections.Generic;
using System.IO;
using Funseki.Core;
using Funseki.Dialogue;
using Funseki.Inventory;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Funseki.EditorTools
{
    // Tools > Funseki > Dialogue: data assets of Funseki.Dialogue, the Dialogue_Test scene and the services in Bootstrap.
    // Dialogue_Test: the hero, a talking NPC cube (E — dialogue of 5 lines with one choice and a «Голос» option)
    // and a passer-by cube that barks when the hero comes close. Play it directly, no Bootstrap needed.
    // Existing data assets are kept (designer edits survive); the scene is rebuilt from scratch.
    public static class DialogueTestSetup
    {
        const string ScenePath = "Assets/_Project/Scenes/Dialogue_Test.unity";
        const string DataDir = "Assets/_Project/Data/Dialogue";
        const string SpeakersDir = "Assets/_Project/Data/Dialogue/Speakers";
        const string PlaceholderArt = "Assets/_Project/Art/Placeholders/Dialogue";
        const string FontPath = "Assets/MainMenu/Fonts/GolosText-SemiBold SDF.asset";
        const string InventorySettingsPath = "Assets/_Project/Data/Inventory/InventorySettings.asset";
        const string MarkerPath = "Assets/_Project/Data/Inventory/Items/Item_Marker.asset";

        [MenuItem("Tools/Funseki/Dialogue/Build Dialogue_Test scene")]
        public static void Build()
        {
            var settings = EnsureSettings();
            var kaito = SpeakerAsset("Speaker_Kaito", "Кайто", new Color(1f, 0.62f, 0.25f), 0.85f);
            var sato = SpeakerAsset("Speaker_TestSato", "Сато", new Color(0.45f, 0.75f, 1f), 1.3f);
            var passerby = SpeakerAsset("Speaker_TestPasserby", "Прохожий", new Color(0.7f, 0.9f, 0.5f), 1.1f);
            var marker = AssetDatabase.LoadAssetAtPath<ItemData>(MarkerPath);
            var dialogue = Asset<DialogueGraph>($"{DataDir}/Dialogue_Test.asset", g => FillTestDialogue(g, kaito, sato, marker));
            var barks = Asset<BarkSet>($"{DataDir}/BarkSet_Test.asset", FillTestBarks);
            AssetDatabase.SaveAssets();

            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(PlayerSliceSetup.InputPath);
            var girl = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerSliceSetup.GirlFbx) ?? FindModel("Anime_Girl");
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PlayerSliceSetup.ControllerPath);
            var inventory = AssetDatabase.LoadAssetAtPath<InventorySettings>(InventorySettingsPath);
            if (input == null || girl == null || controller == null)
            {
                Debug.LogError("[DialogueTestSetup] Need GameInput.inputactions, the Anime Girl FBX and Player.controller.");
                return;
            }

            var active = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool wasOpen = scene.isLoaded;
            if (wasOpen)
                foreach (var go in scene.GetRootGameObjects()) Object.DestroyImmediate(go);
            else
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            var grey = Mat("M_DialogueTest_Floor", new Color(0.6f, 0.6f, 0.62f));
            BuildRoom(grey);

            // Services, each on its own object: a copy steps aside (destroys its object) if Bootstrap already made one.
            new GameObject("[Core]").AddComponent<StandaloneServices>();
            if (inventory != null)
            {
                var inv = new GameObject("[Inventory]");
                Set(inv.AddComponent<InventoryService>(), "settings", inventory);
                Set(inv.AddComponent<InventoryPanel>(), "settings", inventory);
            }
            var runner = new GameObject("[Dialogue]").AddComponent<DialogueRunner>();
            Set(runner, "settings", settings);
            Set(runner, "actions", input);
            Set(new GameObject("[Barks]").AddComponent<BarkService>(), "settings", settings);

            var playerSettings = PlayerSliceSetup.EnsureSettings();
            var player = PlayerSliceSetup.BuildPlayer(girl, controller, input, playerSettings, out var pivot, out var eyes, out var body);
            player.transform.SetPositionAndRotation(new Vector3(0f, 0.05f, -3f), Quaternion.identity);
            PlayerSliceSetup.BuildCameras(player, pivot, eyes, body, playerSettings);
            Set(player.AddComponent<HeroItemUser>(), "actions", input);
            Set(player.AddComponent<HeldItemView>(), "animator", player.GetComponentInChildren<Animator>());

            var satoGo = NpcCube("NPC_Sato", new Vector3(-2f, 0f, 1f), 160f, Mat("M_DialogueTest_Sato", sato.nameColor), sato);
            Set(satoGo.AddComponent<DialogueNpc>(), "dialogue", dialogue);

            var passerGo = NpcCube("NPC_Passerby", new Vector3(2.5f, 0f, 1.5f), 200f, Mat("M_DialogueTest_Passerby", passerby.nameColor), passerby);
            Set(passerGo.AddComponent<BarkTrigger>(), "barks", barks);

            EditorSceneManager.SaveScene(scene, ScenePath);
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DialogueTestSetup] {ScenePath} built. Open it and press Play (no Bootstrap needed).");
        }

        // DialogueRunner and BarkService as children of [Bootstrap], so every scene played from Bootstrap has them.
        [MenuItem("Tools/Funseki/Dialogue/Add dialogue services to Bootstrap")]
        public static void AddToBootstrap()
        {
            var settings = EnsureSettings();
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(PlayerSliceSetup.InputPath);

            var scene = SceneManager.GetSceneByPath(CoreScenesSetup.BootstrapPath);
            bool wasOpen = scene.isLoaded;
            if (!wasOpen) scene = EditorSceneManager.OpenScene(CoreScenesSetup.BootstrapPath, OpenSceneMode.Additive);

            GameObject boot = null;
            foreach (var go in scene.GetRootGameObjects())
                if (go.GetComponent<Bootstrap>() != null) boot = go;
            if (boot == null)
            {
                Debug.LogError("[DialogueTestSetup] No Bootstrap object in the Bootstrap scene.");
                if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
                return;
            }

            if (boot.GetComponentInChildren<DialogueRunner>(true) == null)
            {
                var r = Child(boot, "DialogueRunner").AddComponent<DialogueRunner>();
                Set(r, "settings", settings);
                Set(r, "actions", input);
            }
            if (boot.GetComponentInChildren<BarkService>(true) == null)
                Set(Child(boot, "BarkService").AddComponent<BarkService>(), "settings", settings);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
            Debug.Log("[DialogueTestSetup] DialogueRunner and BarkService are under [Bootstrap].");
        }

        // The hero model has been moved around the project; find it by file name if PlayerSliceSetup's path is stale.
        static GameObject FindModel(string fileName)
        {
            foreach (var guid in AssetDatabase.FindAssets($"{fileName} t:Model"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == fileName) return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            return null;
        }

        static GameObject Child(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        // ---------------------------------------------------------------- test content

        static void FillTestDialogue(DialogueGraph g, Speaker kaito, Speaker sato, ItemData marker)
        {
            const string talked = "test_sato_talked";
            g.nodes = new List<DialogueNode>
            {
                Line("again", sato, "Опять ты? Ладно, слушай ещё раз.", c: Cond(ConditionKind.FlagIsSet, talked)),
                Line("hello", sato, "О, новенький! Ты ведь из 2-Б?"),
                Line("reply", kaito, "Вроде да. Если меня не перевели, пока я спал."),
                Line("ask", sato, "Слушай, у тебя случайно нет лишней отвёртки?", choices: new List<DialogueChoice>
                {
                    Choice("Нет, но могу поискать.", "find"),
                    Choice("А зачем тебе отвёртка?", "why"),
                    Choice("Нет.", "no"),
                    Choice("Скажи ему, что отвёртка — это ключ от учительской.", "voice", voice: true,
                        a: new DialogueAction { kind = ActionKind.SetFlag, id = "test_voice_used" }),
                }),
                Line("find", sato, "Отлично! Держи маркер, вдруг пригодится.", next: "bye",
                    a: marker != null ? new DialogueAction { kind = ActionKind.GiveItem, item = marker } : null),
                Line("why", sato, "Это секрет. Очень винтовой секрет.", next: "bye"),
                Line("no", sato, "Жаль. Придётся откручивать ногтями.", next: "bye"),
                Line("voice", kaito, "Вообще-то отвёртка — это ключ от учительской."),
                Line("bye", sato, "Ладно, скоро звонок. Увидимся на физре!", end: true,
                    a: new DialogueAction { kind = ActionKind.SetFlag, id = talked }),
            };
        }

        static void FillTestBarks(BarkSet b)
        {
            b.lines = new List<BarkLine>
            {
                new() { text = "Не толкайся, я тут стою." },
                new() { text = "Ты тоже опоздал?" },
                new() { text = "Говорят, в спортзале живёт привидение." },
                new() { text = "У меня завтрак убежал. Буквально." },
                new() { text = "Ключ от учительской, говоришь?..", conditions = new List<DialogueCondition> { Cond(ConditionKind.FlagIsSet, "test_voice_used") } },
            };
            b.triggerRadius = 3f;
            b.cooldown = 6f;
        }

        static DialogueNode Line(string id, Speaker speaker, string text, string next = null, bool end = false,
            DialogueCondition c = null, DialogueAction a = null, List<DialogueChoice> choices = null) => new()
        {
            id = id,
            speaker = speaker,
            text = text,
            next = next,
            end = end,
            conditions = c != null ? new List<DialogueCondition> { c } : new List<DialogueCondition>(),
            actions = a != null ? new List<DialogueAction> { a } : new List<DialogueAction>(),
            choices = choices ?? new List<DialogueChoice>(),
        };

        static DialogueChoice Choice(string text, string next, bool voice = false, DialogueAction a = null) => new()
        {
            text = text,
            next = next,
            voice = voice,
            actions = a != null ? new List<DialogueAction> { a } : new List<DialogueAction>(),
        };

        static DialogueCondition Cond(ConditionKind kind, string id) => new() { kind = kind, id = id };

        // ---------------------------------------------------------------- assets

        internal static DialogueSettings EnsureSettings()
        {
            Directory.CreateDirectory(DataDir);
            return Asset<DialogueSettings>($"{DataDir}/DialogueSettings.asset", s =>
            {
                s.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                s.panelSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                s.voiceIcon = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            });
        }

        static Speaker SpeakerAsset(string file, string displayName, Color color, float pitch)
        {
            Directory.CreateDirectory(SpeakersDir);
            return Asset<Speaker>($"{SpeakersDir}/{file}.asset", s =>
            {
                s.displayName = displayName;
                s.nameColor = color;
                s.mumblePitch = pitch;
            });
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

        static Material Mat(string name, Color color)
        {
            Directory.CreateDirectory(PlaceholderArt);
            string path = $"{PlaceholderArt}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // ---------------------------------------------------------------- scene

        static void BuildRoom(Material mat)
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
            Box("Floor", room.transform, new Vector3(0f, -0.05f, 0f), new Vector3(12f, 0.1f, 10f), mat, env);
            Box("Wall_Back", room.transform, new Vector3(0f, 1.5f, 4.5f), new Vector3(12f, 3f, 0.1f), mat, env);
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

        // Root on the ground (dialogue camera and bubbles measure from it), a body cube and a "nose" showing where it faces.
        static GameObject NpcCube(string name, Vector3 pos, float yaw, Material mat, Speaker speaker)
        {
            var root = new GameObject(name);
            root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));

            var bodyGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bodyGo.name = "Body";
            bodyGo.transform.SetParent(root.transform, false);
            bodyGo.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            bodyGo.transform.localScale = new Vector3(0.55f, 1.6f, 0.4f);
            bodyGo.GetComponent<Renderer>().sharedMaterial = mat;

            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Nose";
            nose.transform.SetParent(root.transform, false);
            nose.transform.localPosition = new Vector3(0f, 1.4f, 0.25f);
            nose.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);
            nose.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(nose.GetComponent<Collider>());

            var tag = root.AddComponent<SpeakerTag>();
            tag.speaker = speaker;
            tag.headHeight = 1.6f;
            return root;
        }

        static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[DialogueTestSetup] {target.GetType().Name}.{field} not found"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
