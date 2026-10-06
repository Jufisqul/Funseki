using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Funseki.EditorTools
{
    // Opens every scene from Build Settings and Assets/_Project, then every prefab outside Assets/_Trash,
    // and lists Missing Script and Missing Reference problems in the Console as [ValidateReferences].
    public static class ReferenceValidator
    {
        [MenuItem("Tools/Funseki/Validate References")]
        public static void Validate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var problems = new List<string>();

            var scenes = EditorBuildSettings.scenes.Select(s => s.path)
                .Concat(AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Project" }).Select(AssetDatabase.GUIDToAssetPath))
                .Distinct().ToList();
            foreach (var path in scenes)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (var root in scene.GetRootGameObjects()) CheckTree(root, path, problems);
            }

            var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !p.StartsWith("Assets/_Trash/")).ToList();
            foreach (var path in prefabs)
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root != null) CheckTree(root, path, problems);
            }

            if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);

            var head = $"[ValidateReferences] {scenes.Count} scenes, {prefabs.Count} prefabs, {problems.Count} problems";
            if (problems.Count == 0) Debug.Log(head + ": PASS");
            else
            {
                Debug.LogWarning(head);
                foreach (var p in problems) Debug.LogWarning("[ValidateReferences] " + p);
            }
        }

        static void CheckTree(GameObject go, string owner, List<string> problems)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                var components = t.gameObject.GetComponents<Component>();
                foreach (var c in components)
                {
                    if (c == null)
                    {
                        problems.Add($"Missing Script: {owner} / {PathOf(t)}");
                        continue;
                    }
                    var so = new SerializedObject(c);
                    var p = so.GetIterator();
                    while (p.NextVisible(true))
                    {
                        if (p.propertyType != SerializedPropertyType.ObjectReference) continue;
                        if (p.objectReferenceValue != null) continue;
                        object id = p.objectReferenceEntityIdValue;
                        if (!Equals(id, System.Activator.CreateInstance(id.GetType())))
                            problems.Add($"Missing Reference: {owner} / {PathOf(t)} / {c.GetType().Name}.{p.propertyPath}");
                    }
                }
            }
        }

        static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    }
}
