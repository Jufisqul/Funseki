using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Funseki.EditorTools
{
    // Keeps hand-placed positions when a Tools > Funseki setup menu rebuilds its root.
    // A setup captures the scene before it deletes its root and restores right before saving:
    // every object whose hierarchy path still exists gets its old local position, rotation and scale back,
    // new objects stay where the builder put them. Same-name siblings are told apart by their order ("Bed#2").
    // To get the builder's default place back: delete the object (or the whole root) and run the setup again,
    // or untick Tools > Funseki > Keep Hand-Placed Positions on Rebuild.
    public sealed class LayoutKeeper
    {
        const string PrefKey = "Funseki.KeepHandPlacedPositions";
        const string MenuPath = "Tools/Funseki/Keep Hand-Placed Positions on Rebuild";

        struct Pose { public Vector3 position; public Quaternion rotation; public Vector3 scale; }

        readonly Scene scene;
        readonly Transform root;
        readonly Dictionary<string, Pose> poses = new();

        static bool Enabled => EditorPrefs.GetBool(PrefKey, true);

        [MenuItem(MenuPath, priority = 900)]
        static void Toggle() => EditorPrefs.SetBool(PrefKey, !Enabled);

        [MenuItem(MenuPath, true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        LayoutKeeper(Scene scene, Transform root)
        {
            this.scene = scene;
            this.root = root;
        }

        /// <summary>Remembers every object of the scene.</summary>
        public static LayoutKeeper Capture(Scene scene)
        {
            var keeper = new LayoutKeeper(scene, null);
            if (Enabled && scene.IsValid() && scene.isLoaded)
                foreach (var go in keeper.Roots()) keeper.Read(go.transform, Key(go.transform, null));
            return keeper;
        }

        /// <summary>Remembers the objects under one parent (its own pose is not touched).</summary>
        public static LayoutKeeper Capture(Transform parent)
        {
            var keeper = new LayoutKeeper(default, parent);
            if (Enabled && parent != null)
                foreach (Transform child in parent) keeper.Read(child, Key(child, null));
            return keeper;
        }

        /// <summary>Puts the remembered objects back. Call after the rebuild, before the scene is saved.</summary>
        public void Restore()
        {
            if (poses.Count == 0) return;
            int restored = 0;
            if (root != null)
                foreach (Transform child in root) restored += Write(child, Key(child, null));
            else if (scene.IsValid() && scene.isLoaded)
                foreach (var go in Roots()) restored += Write(go.transform, Key(go.transform, null));
            if (restored > 0)
                Debug.Log($"[LayoutKeeper] {restored} hand-placed object(s) kept in {(root != null ? root.name : scene.name)}.");
        }

        IEnumerable<GameObject> Roots() => scene.GetRootGameObjects();

        void Read(Transform t, string key)
        {
            poses[key] = new Pose { position = t.localPosition, rotation = t.localRotation, scale = t.localScale };
            foreach (Transform child in t) Read(child, Key(child, key));
        }

        int Write(Transform t, string key)
        {
            int n = 0;
            if (poses.TryGetValue(key, out var p)
                && (t.localPosition != p.position || t.localRotation != p.rotation || t.localScale != p.scale))
            {
                // A CharacterController would snap the hero back otherwise.
                var cc = t.GetComponent<CharacterController>();
                bool on = cc != null && cc.enabled;
                if (on) cc.enabled = false;
                t.localPosition = p.position;
                t.localRotation = p.rotation;
                t.localScale = p.scale;
                if (on) cc.enabled = true;
                n = 1;
            }
            foreach (Transform child in t) n += Write(child, Key(child, key));
            return n;
        }

        // "Parent/Child", with "#n" for the n-th sibling of the same name (n from 2).
        static string Key(Transform t, string parentKey)
        {
            int same = 0;
            var parent = t.parent;
            if (parent != null)
            {
                for (int i = 0; i < t.GetSiblingIndex(); i++)
                    if (parent.GetChild(i).name == t.name) same++;
            }
            else
            {
                foreach (var go in t.gameObject.scene.GetRootGameObjects())
                {
                    if (go.transform == t) break;
                    if (go.name == t.name) same++;
                }
            }
            string name = same == 0 ? t.name : $"{t.name}#{same + 1}";
            return parentKey == null ? name : parentKey + "/" + name;
        }
    }
}
