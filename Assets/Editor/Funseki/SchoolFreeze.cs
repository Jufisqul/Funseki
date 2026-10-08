using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Funseki.School;

namespace Funseki.School.EditorTools
{
    // Freezing hands School_Greybox over to manual editing: a SchoolFrozen marker on the School root
    // makes Build School refuse to rebuild it. Kit prefabs stay linked, so Generate Kit still
    // updates module shapes and colors in place (positions and edits are kept).
    public static partial class SchoolBuilder
    {
        static bool IsFrozen(Scene scene)
        {
            var school = FindRoot(scene, RootName);
            return school != null && school.GetComponent<SchoolFrozen>() != null;
        }

        [MenuItem("Tools/Funseki/Freeze School (edit by hand)")]
        static void Freeze()
        {
            var scene = OpenSchoolScene(true);
            if (!scene.IsValid()) return;
            var school = FindRoot(scene, RootName);
            if (school == null)
            {
                Debug.LogError("[Funseki] School_Greybox has no School root yet: build it once first.");
                return;
            }
            if (school.GetComponent<SchoolFrozen>() == null) Undo.AddComponent<SchoolFrozen>(school);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Funseki] School_Greybox is frozen: edit it by hand, Build School will not rebuild it.");
        }

        [MenuItem("Tools/Funseki/Unfreeze School")]
        static void Unfreeze()
        {
            var scene = OpenSchoolScene(true);
            if (!scene.IsValid()) return;
            var school = FindRoot(scene, RootName);
            var marker = school != null ? school.GetComponent<SchoolFrozen>() : null;
            if (marker == null) { Debug.Log("[Funseki] School_Greybox is not frozen."); return; }
            if (!EditorUtility.DisplayDialog("Unfreeze School",
                    "Build School will be able to rebuild the School root again, and every hand edit inside School (walls, floors, doors) will be lost on the next build. School_Furniture and School_Props are kept.",
                    "Unfreeze", "Cancel")) return;
            Undo.DestroyObjectImmediate(marker);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Funseki] School_Greybox unfrozen: Build School can rebuild it.");
        }
    }
}
