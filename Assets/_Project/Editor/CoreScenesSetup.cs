using System.Collections.Generic;
using System.IO;
using Funseki.Core;
using Funseki.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Funseki.EditorTools
{
    // Tools > Funseki > Core: creates Bootstrap / Slice_Day1 scenes, the GameFlowConfig asset
    // and the Build Settings order. Safe to re-run: existing files are kept.
    // Scenes are created additively, so whatever scene is open in the Editor stays open.
    public static class CoreScenesSetup
    {
        public const string FlowPath = "Assets/_Project/Data/GameFlowConfig.asset";
        public const string BootstrapPath = "Assets/_Project/Scenes/Bootstrap.unity";
        public const string SlicePath = "Assets/_Project/Scenes/Slice_Day1.unity";
        public const string MainMenuPath = "Assets/_Project/Scenes/MainMenu.unity";

        [MenuItem("Tools/Funseki/Core/Create Core Scenes")]
        public static void CreateAll()
        {
            var flow = AssetDatabase.LoadAssetAtPath<GameFlowConfig>(FlowPath);
            if (flow == null)
            {
                flow = ScriptableObject.CreateInstance<GameFlowConfig>();
                AssetDatabase.CreateAsset(flow, FlowPath);
            }

            var active = SceneManager.GetActiveScene();
            if (!File.Exists(BootstrapPath)) CreateBootstrap(flow);
            if (!File.Exists(SlicePath)) CreateSlice();
            if (active.IsValid()) SceneManager.SetActiveScene(active);

            SetupBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[Funseki] Core scenes ready: Bootstrap, Slice_Day1, GameFlowConfig, Build Settings.");
        }

        static void CreateBootstrap(GameFlowConfig flow)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var go = new GameObject("[Bootstrap]");
            SceneManager.MoveGameObjectToScene(go, scene);
            var boot = go.AddComponent<Bootstrap>();
            var so = new SerializedObject(boot);
            so.FindProperty("flow").objectReferenceValue = flow;
            so.ApplyModifiedPropertiesWithoutUndo();
            go.AddComponent<GameStateDebugLabel>();
            EditorSceneManager.SaveScene(scene, BootstrapPath);
            EditorSceneManager.CloseScene(scene, true);
        }

        static void CreateSlice()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            var cam = new GameObject("Main Camera") { tag = "MainCamera" };
            cam.AddComponent<Camera>();
            cam.AddComponent<AudioListener>();
            cam.transform.SetPositionAndRotation(new Vector3(0, 6, -10), Quaternion.Euler(25, 0, 0));

            var sun = new GameObject("Directional Light");
            sun.AddComponent<Light>().type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(50, -30, 0);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground_Placeholder";
            ground.transform.localScale = new Vector3(4, 1, 4);

            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "Slice_Day1_Marker";
            marker.transform.position = new Vector3(0, 0.5f, 0);

            foreach (var g in new[] { cam, sun, ground, marker }) SceneManager.MoveGameObjectToScene(g, scene);
            EditorSceneManager.SaveScene(scene, SlicePath);
            EditorSceneManager.CloseScene(scene, true);
        }

        // Bootstrap first (the build starts there), existing entries kept in order, Slice_Day1 added.
        static void SetupBuildSettings()
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            list.RemoveAll(s => s.path == BootstrapPath || s.path == SlicePath);
            list.Insert(0, new EditorBuildSettingsScene(BootstrapPath, true));
            if (!list.Exists(s => s.path == MainMenuPath)) list.Add(new EditorBuildSettingsScene(MainMenuPath, true));
            list.Add(new EditorBuildSettingsScene(SlicePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        // Play from Bootstrap without leaving the scene you're editing.
        [MenuItem("Tools/Funseki/Core/Play From Bootstrap")]
        public static void PlayFromBootstrap()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapPath);
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        static void ClearStartSceneAfterPlay()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                if (s == PlayModeStateChange.EnteredEditMode) EditorSceneManager.playModeStartScene = null;
            };
        }
    }
}
