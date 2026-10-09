using System.Collections.Generic;
using System.IO;
using System.Linq;
using Funseki.DayCycle;
using Funseki.School;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Funseki.EditorTools
{
    // Tools > Funseki > DayCycle > Setup room access: open / closed rooms by time of day.
    // - Data/DayCycle/RoomAccess.asset is created if missing; its room list gets every Zone of School_Greybox.
    //   Rooms already in the list keep their morning / day / evening ticks, new ones start open all day.
    // - Slice_Day1: RoomAccessDirector is added to the DayCycle object (nothing else in the scene changes).
    // School_Greybox is only read, never saved.
    public static class RoomAccessSetup
    {
        public const string SettingsPath = "Assets/_Project/Data/DayCycle/RoomAccess.asset";
        const string SchoolScenePath = "Assets/_Project/School/Scenes/School_Greybox.unity";

        [MenuItem("Tools/Funseki/DayCycle/Setup room access (fill rooms from the school)")]
        public static void Setup()
        {
            var settings = EnsureSettings();
            int added = FillRooms(settings);

            var active = SceneManager.GetActiveScene();
            var slice = SceneManager.GetSceneByPath(CoreScenesSetup.SlicePath);
            bool opened = !slice.isLoaded;
            if (opened) slice = EditorSceneManager.OpenScene(CoreScenesSetup.SlicePath, OpenSceneMode.Additive);

            var dayCycle = slice.GetRootGameObjects().FirstOrDefault(g => g.name == "DayCycle");
            if (dayCycle == null)
                Debug.LogWarning("[RoomAccessSetup] Slice_Day1 has no DayCycle object: run Tools > Funseki > DayCycle > Setup Day 1 schedule first.");
            else
            {
                AddDirector(dayCycle);
                EditorSceneManager.MarkSceneDirty(slice);
                EditorSceneManager.SaveScene(slice);
            }
            if (opened) EditorSceneManager.CloseScene(slice, true);
            else if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);

            Selection.activeObject = settings;
            Debug.Log($"[RoomAccessSetup] {settings.rooms.Count} room(s) in RoomAccess.asset ({added} new). Tick morning / day / evening there.");
        }

        // DayCycleSetup.AddToActiveScene calls this too, so Build Slice_Day1 keeps the director.
        internal static void AddDirector(GameObject dayCycle)
        {
            var director = dayCycle.GetComponent<RoomAccessDirector>();
            if (director == null) director = dayCycle.AddComponent<RoomAccessDirector>();
            var so = new SerializedObject(director);
            so.FindProperty("settings").objectReferenceValue = EnsureSettings();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static RoomAccessSettings EnsureSettings()
        {
            var s = AssetDatabase.LoadAssetAtPath<RoomAccessSettings>(SettingsPath);
            if (s != null) return s;
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            s = ScriptableObject.CreateInstance<RoomAccessSettings>();
            AssetDatabase.CreateAsset(s, SettingsPath);
            AssetDatabase.SaveAssets();
            return s;
        }

        // Reads the zones of School_Greybox (opened additively and closed without saving if it was not open).
        static int FillRooms(RoomAccessSettings settings)
        {
            var school = SceneManager.GetSceneByPath(SchoolScenePath);
            bool opened = !school.isLoaded;
            if (opened) school = EditorSceneManager.OpenScene(SchoolScenePath, OpenSceneMode.Additive);

            var zones = new List<Zone>();
            foreach (var root in school.GetRootGameObjects())
                zones.AddRange(root.GetComponentsInChildren<Zone>(true));
            if (opened) EditorSceneManager.CloseScene(school, true);

            var old = settings.rooms.Where(r => !string.IsNullOrEmpty(r.zoneId))
                .GroupBy(r => r.zoneId).ToDictionary(g => g.Key, g => g.First());
            var rooms = new List<RoomAccessSettings.Room>();
            int added = 0;
            foreach (var z in zones.Where(z => !string.IsNullOrEmpty(z.zoneId))
                         .GroupBy(z => z.zoneId).Select(g => g.First())
                         .OrderBy(z => z.floor).ThenBy(z => z.displayName))
            {
                if (!old.TryGetValue(z.zoneId, out var room)) { room = new RoomAccessSettings.Room { zoneId = z.zoneId }; added++; }
                room.name = $"Этаж {z.floor} · {(string.IsNullOrEmpty(z.displayName) ? z.zoneId : z.displayName)} ({z.zoneId})";
                rooms.Add(room);
                old.Remove(z.zoneId);
            }
            // Rooms no longer in the school stay at the end, so hand-made entries are not lost.
            rooms.AddRange(old.Values);

            Undo.RecordObject(settings, "Fill room access");
            settings.rooms = rooms;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return added;
        }
    }
}
