using System.IO;
using Funseki.Audio;
using Funseki.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Funseki.EditorTools
{
    // Tools > Funseki > Audio: BellSettings.asset (created if missing, never overwritten)
    // and a BellSound child under [Bootstrap], so every scene played from Bootstrap hears the bell.
    public static class AudioSetup
    {
        public const string BellSettingsPath = "Assets/_Project/Data/Audio/BellSettings.asset";
        const string BellClipPath = "Assets/Sound/BellRing.mp3";

        [MenuItem("Tools/Funseki/Audio/Add bell sound to Bootstrap")]
        public static void AddBellToBootstrap()
        {
            var settings = EnsureBellSettings();

            var scene = SceneManager.GetSceneByPath(CoreScenesSetup.BootstrapPath);
            bool wasOpen = scene.isLoaded;
            if (!wasOpen) scene = EditorSceneManager.OpenScene(CoreScenesSetup.BootstrapPath, OpenSceneMode.Additive);

            GameObject boot = null;
            foreach (var go in scene.GetRootGameObjects())
                if (go.GetComponent<Bootstrap>() != null) boot = go;
            if (boot == null)
            {
                Debug.LogError("[AudioSetup] No Bootstrap object in the Bootstrap scene.");
                if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
                return;
            }

            var player = boot.GetComponentInChildren<BellSoundPlayer>(true);
            if (player == null)
            {
                var child = new GameObject("BellSound");
                child.transform.SetParent(boot.transform, false);
                child.AddComponent<AudioSource>();
                player = child.AddComponent<BellSoundPlayer>();
            }
            var so = new SerializedObject(player);
            so.FindProperty("settings").objectReferenceValue = settings;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
            Debug.Log("[AudioSetup] BellSound is under [Bootstrap].");
        }

        internal static BellSettings EnsureBellSettings()
        {
            var s = AssetDatabase.LoadAssetAtPath<BellSettings>(BellSettingsPath);
            if (s != null) return s;

            Directory.CreateDirectory(Path.GetDirectoryName(BellSettingsPath));
            s = ScriptableObject.CreateInstance<BellSettings>();
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(BellClipPath);
            if (clip == null) Debug.LogWarning($"[AudioSetup] {BellClipPath} not found; the bell slots stay empty.");
            s.lessonStart.clip = clip;
            s.second.clip = clip;
            s.lessonEnd.clip = clip;
            AssetDatabase.CreateAsset(s, BellSettingsPath);
            AssetDatabase.SaveAssets();
            return s;
        }
    }
}
