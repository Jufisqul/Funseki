using Funseki.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Funseki.EditorTools
{
    // Fast test loops for the slice (see Funseki.Core.QuickPlay):
    // - Quick Play: Bootstrap -> Slice_Day1 straight into the first break (no main menu, no intro),
    //   autosave off so the real save stays intact.
    // - Quick Play Here: the same, with the leading hero dropped where the Scene View is looking.
    // - Edit Slice + School: opens Slice_Day1 with School_Greybox next to it, to edit objects and the building together.
    // The open scenes are saved first (with a prompt) because Play runs what is on disk.
    public static class QuickPlayMenu
    {
        const string StartPhase = "break_1";
        const string SchoolPath = "Assets/_Project/School/Scenes/School_Greybox.unity";

        [MenuItem("Tools/Funseki/Quick Play/Play Break (skip menu and intro) %&p", priority = 0)]
        static void PlayBreak() => Start(null);

        [MenuItem("Tools/Funseki/Quick Play/Play Break Here (from Scene View) %&#p", priority = 1)]
        static void PlayBreakHere()
        {
            var view = SceneView.lastActiveSceneView;
            if (view == null) { Debug.LogWarning("[QuickPlay] Open a Scene View first."); return; }
            // The point in the middle of the Scene View: cast from its camera, step back off walls,
            // then stand on whatever floor is below. Needs the school open in the editor to hit anything.
            // Ceiling panels are ignored, so a view from above lands in the room, not on the roof.
            var cam = view.camera.transform;
            if (!CastSkippingCeilings(cam.position, cam.forward, 500f, out var hit))
            {
                Debug.LogWarning("[QuickPlay] The Scene View does not look at any geometry. Open the school " +
                                 "(Tools > Funseki > Quick Play > Edit Slice + School Together) and point the view at a floor.");
                return;
            }
            Vector3 p = hit.point - cam.forward * 0.5f;
            if (CastSkippingCeilings(p + Vector3.up * 0.5f, Vector3.down, 50f, out var floor))
                p = floor.point;
            Debug.Log($"[QuickPlay] Hero will start at {p} (Scene View center).");
            Start(p + Vector3.up * 0.05f);
        }

        static bool CastSkippingCeilings(Vector3 origin, Vector3 dir, float distance, out RaycastHit best)
        {
            best = default;
            float bestDist = float.MaxValue;
            foreach (var h in Physics.RaycastAll(origin, dir, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.name.Contains("Ceiling") || h.distance >= bestDist) continue;
                best = h;
                bestDist = h.distance;
            }
            return bestDist < float.MaxValue;
        }

        [MenuItem("Tools/Funseki/Quick Play/Edit Slice + School Together", priority = 20)]
        static void EditTogether()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(CoreScenesSetup.SlicePath, OpenSceneMode.Single);
            EditorSceneManager.OpenScene(SchoolPath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(SceneManager.GetSceneByPath(CoreScenesSetup.SlicePath));
            Debug.Log("[QuickPlay] Slice_Day1 and School_Greybox are open together. Save each scene after editing.");
        }

        static void Start(Vector3? spawn)
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SessionState.SetString(QuickPlay.SceneKey, "Slice_Day1");
            SessionState.SetString(QuickPlay.PhaseKey, StartPhase);
            SessionState.SetString(QuickPlay.SpawnKey, spawn.HasValue
                ? string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0};{1};{2}", spawn.Value.x, spawn.Value.y, spawn.Value.z)
                : "");
            CoreScenesSetup.PlayFromBootstrap();
        }

        [InitializeOnLoadMethod]
        static void ClearAfterPlay()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                if (s != PlayModeStateChange.EnteredEditMode) return;
                SessionState.EraseString(QuickPlay.SceneKey);
                SessionState.EraseString(QuickPlay.PhaseKey);
                SessionState.EraseString(QuickPlay.SpawnKey);
            };
        }
    }
}
