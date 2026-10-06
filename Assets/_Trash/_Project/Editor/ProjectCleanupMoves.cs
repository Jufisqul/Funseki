using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Funseki.EditorTools
{
    // One-shot project cleanup (Docs/cleanup_report.md, tables A and B). Moves go through
    // AssetDatabase.MoveAsset so GUIDs and references survive. Safe to re-run: missing sources are skipped.
    public static class ProjectCleanupMoves
    {
        const string Trash = "Assets/_Trash/";

        static readonly (string from, string to)[] Moves =
        {
            // B: target structure
            ("Assets/Scenes/MainMenu.unity", "Assets/_Project/Scenes/MainMenu.unity"),
            ("Assets/_Project/Scenes/Dialogue_Test.unity", "Assets/_Project/Scenes/_Sandbox/Dialogue_Test.unity"),
            ("Assets/_Project/Scenes/Interaction_Test.unity", "Assets/_Project/Scenes/_Sandbox/Interaction_Test.unity"),
            ("Assets/Sound/BellRing.mp3", "Assets/_Project/Audio/SFX/BellRing.mp3"),
            ("Assets/Models/Animation", "Assets/ThirdParty/Models/Animation"),
            ("Assets/Models/Kaito", "Assets/ThirdParty/Models/Kaito"),
            ("Assets/Models/Rey", "Assets/ThirdParty/Models/Rey"),
            ("Assets/Models/Ryuto", "Assets/ThirdParty/Models/Ryuto"),
        };

        static readonly string[] ToTrash =
        {
            "Assets/Scenes/SampleScene.unity",
            "Assets/_Game/Scenes/PlayerTest.unity",
            "Assets/_Game/Scenes/Day1_Greybox.unity",
            "Assets/_Game/Editor",
            "Assets/_Game/Art/Greybox",
            "Assets/_Game/Art/Characters/Hero/Hero_Game.fbx",
            "Assets/_Game/Art/Characters/Hero/Hero.mat",
            "Assets/_Game/Art/Characters/Hero/Textures",
            "Assets/_Game/Art/Characters/Tomura",
            "Assets/_Game/Scripts/Util",
            "Assets/New Cubemap.png",
            "Assets/Screenshots",
            "Assets/_Project/Scripts/DayCycle/CutscenePlaceholder.cs",
            "Assets/_Project/Editor/PlayerSliceSmokeTest.cs",
            "Assets/MainMenu/Generated/Materials",
            "Assets/MainMenu/Generated/Textures/BrokenGlass.png",
            "Assets/MainMenu/Generated/Textures/VendingFront.png",
            "Assets/MainMenu/Materials/M_Glass_Greybox.mat",
            "Assets/MainMenu/Materials/M_Ground_Placeholder.mat",
            "Assets/MainMenu/Materials/M_Petal.mat",
            "Assets/MainMenu/Materials/M_School_Greybox.mat",
            "Assets/MainMenu/Sky",
            "Assets/MainMenu/MainMenu_VolumeProfile.asset",
            "Assets/MainMenu/Models/Characters/anime+character+3d+model.glb",
            "Assets/Models/Yukki",
            "Assets/ThirdParty/Quaternius_Nature",
            "Assets/ThirdParty/PolyHaven",
            "Assets/ThirdParty/Sky",
            "Assets/ThirdParty/Kenney_ModularBuildings",
            "Assets/ThirdParty/Kenney_CityCommercial",
            "Assets/ThirdParty/AmbientCG_Graffiti",
        };

        static readonly string[] RemoveFromBuild =
        {
            "SampleScene", "PlayerTest", "Day1_Greybox",
        };

        [MenuItem("Tools/Funseki/Core/Cleanup/Run Project Cleanup Moves")]
        public static void Run()
        {
            var log = new List<string>();
            int ok = 0, fail = 0;

            foreach (var (from, to) in Moves) Move(from, to, log, ref ok, ref fail);
            foreach (var from in ToTrash) Move(from, Trash + from.Substring("Assets/".Length), log, ref ok, ref fail);

            EditorBuildSettings.scenes = EditorBuildSettings.scenes
                .Where(s => !RemoveFromBuild.Contains(Path.GetFileNameWithoutExtension(s.path)))
                .ToArray();
            log.Add("Build Settings: " + string.Join(", ", EditorBuildSettings.scenes.Select(s => s.path)));

            int removed = RemoveEmptyFolders("Assets", log);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ProjectCleanup] moved {ok}, failed {fail}, empty folders removed {removed}\n" + string.Join("\n", log));
        }

        static void Move(string from, string to, List<string> log, ref int ok, ref int fail)
        {
            if (!AssetDatabase.IsValidFolder(from) && AssetDatabase.AssetPathToGUID(from) == "")
            {
                log.Add("SKIP (missing) " + from);
                return;
            }
            EnsureFolder(Path.GetDirectoryName(to).Replace('\\', '/'));
            var error = AssetDatabase.MoveAsset(from, to);
            if (string.IsNullOrEmpty(error)) { ok++; log.Add("OK   " + from + " -> " + to); }
            else { fail++; log.Add("FAIL " + from + " -> " + to + ": " + error); }
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        // Deletes folders that contain no files (only empty subfolders), bottom-up, with their .meta.
        static int RemoveEmptyFolders(string root, List<string> log)
        {
            int count = 0;
            foreach (var sub in AssetDatabase.GetSubFolders(root)) count += RemoveEmptyFolders(sub, log);
            if (root == "Assets" || root.StartsWith("Assets/_Trash")) return count;
            if (root == "Assets/_Project/Audio" || root == "Assets/_Project/Scripts/Telemetry") return count;
            var full = Path.GetFullPath(root);
            if (Directory.Exists(full) && !Directory.EnumerateFileSystemEntries(full).Any())
            {
                if (AssetDatabase.DeleteAsset(root)) { count++; log.Add("EMPTY removed " + root); }
            }
            return count;
        }
    }
}
