using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Funseki.School;

namespace Funseki.School.EditorTools
{
    public class SchoolBuilderWindow : EditorWindow
    {
        SchoolLayout _layout;
        KitSettings _settings;

        [MenuItem("Tools/Funseki/Build School")]
        public static void Open()
        {
            var w = GetWindow<SchoolBuilderWindow>("Build School");
            w.minSize = new Vector2(340, 220);
        }

        void OnEnable()
        {
            _layout = AssetDatabase.LoadAssetAtPath<SchoolLayout>(SchoolLayoutDefaults.LayoutPath);
            _settings = AssetDatabase.LoadAssetAtPath<KitSettings>(KitGenerator.SettingsPath);
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Greybox school", EditorStyles.boldLabel);
            _layout = (SchoolLayout)EditorGUILayout.ObjectField("School Layout", _layout, typeof(SchoolLayout), false);
            _settings = (KitSettings)EditorGUILayout.ObjectField("Kit Settings", _settings, typeof(KitSettings), false);

            if (_layout == null && GUILayout.Button("Create layout from the floor plans"))
                _layout = SchoolLayoutDefaults.LoadOrCreate();
            if (_settings == null && GUILayout.Button("Create Kit Settings"))
                _settings = KitGenerator.LoadOrCreateSettings();

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_layout == null || _settings == null))
            {
                if (GUILayout.Button("Build School", GUILayout.Height(32)))
                    SchoolBuilder.Build(_layout, _settings, true);
            }
            if (GUILayout.Button("Capture top views (Docs/School_TopView_F*.png)"))
                SchoolTopView.Capture();

            EditorGUILayout.HelpBox(
                "Rebuilds Assets/_Project/School/Scenes/School_Greybox.unity from scratch. " +
                "Regenerate the kit first (Tools > Funseki > Generate Kit) after changing Kit Settings.",
                MessageType.Info);
        }
    }

    // Renders each floor from above into a PNG: everything above the floor's ceiling and all
    // ceiling panels are hidden, so the wall layout reads like a plan.
    public static class SchoolTopView
    {
        [MenuItem("Tools/Funseki/Capture School Top Views")]
        public static void Capture()
        {
            var settings = KitGenerator.LoadOrCreateSettings();
            GameObject source = null;
            for (int i = 0; i < SceneManager.sceneCount && source == null; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.path == SchoolBuilder.ScenePath && s.isLoaded)
                    source = s.GetRootGameObjects().FirstOrDefault(g => g.name == "School");
            }
            if (source == null)
            {
                Debug.LogError("[Funseki] Open School_Greybox.unity and build the school before capturing.");
                return;
            }

            for (int floor = 1; floor <= 2; floor++)
            {
                var preview = EditorSceneManager.NewPreviewScene();
                try
                {
                    var clone = Object.Instantiate(source);
                    SceneManager.MoveGameObjectToScene(clone, preview);

                    float cut = floor * settings.floorHeight - settings.slabThickness - 0.01f;
                    foreach (var t in clone.GetComponentsInChildren<Transform>(true))
                        if (t.name == "Ceiling") t.gameObject.SetActive(false);
                    var bounds = new Bounds();
                    bool first = true;
                    foreach (var r in clone.GetComponentsInChildren<Renderer>())
                    {
                        if (r.bounds.min.y >= cut) { r.enabled = false; continue; }
                        if (first) { bounds = r.bounds; first = false; }
                        else bounds.Encapsulate(r.bounds);
                    }

                    var light = new GameObject("Sun").AddComponent<Light>();
                    light.type = LightType.Directional;
                    light.intensity = 1.2f;
                    light.transform.rotation = Quaternion.Euler(60f, 30f, 0f);
                    SceneManager.MoveGameObjectToScene(light.gameObject, preview);

                    var cam = new GameObject("TopCamera").AddComponent<Camera>();
                    SceneManager.MoveGameObjectToScene(cam.gameObject, preview);
                    cam.scene = preview;
                    cam.orthographic = true;
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.55f, 0.65f, 0.75f);
                    const int width = 2400, height = 1300;
                    float aspect = (float)width / height;
                    cam.orthographicSize = Mathf.Max(bounds.extents.z, bounds.extents.x / aspect) + 2f;
                    cam.transform.SetPositionAndRotation(
                        new Vector3(bounds.center.x, cut + 50f, bounds.center.z), Quaternion.Euler(90f, 0f, 0f));
                    cam.nearClipPlane = 0.1f;
                    cam.farClipPlane = 200f;

                    KitPreview.SaveCameraPng(cam, $"{KitGenerator.Root}Docs/School_TopView_F{floor}.png", width, height);
                }
                finally
                {
                    EditorSceneManager.ClosePreviewScene(preview);
                }
            }
            Debug.Log("[Funseki] Top views saved to " + KitGenerator.Root + "Docs/");
        }
    }
}
