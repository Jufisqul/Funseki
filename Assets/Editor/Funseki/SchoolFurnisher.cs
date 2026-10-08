using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Funseki.School;

namespace Funseki.School.EditorTools
{
    // Placeholder furniture per zone preset (ZoneData.furnish). Positions are in plan meters.
    // Furniture lives in its own scene root (School_Furniture/<group>/Zone_<id>): Build School places it
    // only for zones that have none yet, so moved, deleted or added pieces survive rebuilds.
    // Tools > Funseki > Refurnish ... puts zones back to their presets on request.
    public static partial class SchoolBuilder
    {
        static Transform _furnitureRoot;

        [MenuItem("Tools/Funseki/Refurnish Selected Zones")]
        static void RefurnishSelected()
        {
            var ids = new HashSet<string>();
            foreach (var go in Selection.gameObjects)
                for (var t = go.transform; t != null; t = t.parent)
                    if (t.name.StartsWith("Zone_")) { ids.Add(t.name.Substring("Zone_".Length)); break; }
            if (ids.Count == 0)
            {
                Debug.LogWarning("[Funseki] Select an object inside a Zone_<id> (School or School_Furniture) first.");
                return;
            }
            if (!EditorUtility.DisplayDialog("Refurnish zones",
                    $"Put the preset furniture back in {string.Join(", ", ids)}? Hand edits to the furniture of these zones will be lost.",
                    "Refurnish", "Cancel")) return;
            Refurnish(ids);
        }

        [MenuItem("Tools/Funseki/Refurnish All Zones")]
        static void RefurnishAll()
        {
            if (!EditorUtility.DisplayDialog("Refurnish all zones",
                    "Replace all furniture in School_Furniture with the presets? Every hand edit to the furniture will be lost. School_Props is not touched.",
                    "Refurnish all", "Cancel")) return;
            Refurnish(null);
        }

        // Moves the furniture out of School and adds School_Props without rebuilding anything.
        [MenuItem("Tools/Funseki/Split Furniture And Props Roots (no rebuild)")]
        public static void SplitRoots()
        {
            var scene = OpenSchoolScene(true);
            if (!scene.IsValid()) return;
            bool migrated = false;
            if (FindRoot(scene, FurnitureRootName) == null)
                migrated = MigrateFurniture(scene, CreateRoot(scene, FurnitureRootName).transform);
            if (FindRoot(scene, PropsRootName) == null) CreateRoot(scene, PropsRootName);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Funseki] {FurnitureRootName} and {PropsRootName} are ready in {scene.path}" +
                      (migrated ? "; furniture moved out of School as it was." : "."));
        }

        static void Refurnish(HashSet<string> ids)
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[Funseki] Open School_Greybox.unity first.");
                return;
            }
            S = KitGenerator.LoadOrCreateSettings();
            _warnings = new List<string>();
            if (!LoadPrefabs()) return;
            var layout = SchoolLayoutDefaults.LoadOrCreate();
            _furnitureRoot = (FindRoot(scene, FurnitureRootName) ?? CreateRoot(scene, FurnitureRootName)).transform;

            int n = 0;
            foreach (var z in layout.zones.Where(z => z != null && !string.IsNullOrEmpty(z.zoneId)))
            {
                if (ids != null && !ids.Contains(z.zoneId)) continue;
                var old = _furnitureRoot.Find(z.group + "/Zone_" + z.zoneId);
                if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
                Furnish(z);
                var created = _furnitureRoot.Find(z.group + "/Zone_" + z.zoneId);
                if (created != null) Undo.RegisterCreatedObjectUndo(created.gameObject, "Refurnish zones");
                n++;
            }
            foreach (var w in _warnings) Debug.LogWarning("[Funseki] " + w);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Funseki] Refurnished {n} zone(s). Save the scene to keep it.");
        }

        static GameObject FindRoot(Scene scene, string name) =>
            scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);

        static GameObject CreateRoot(Scene scene, string name)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }

        static Transform FindOrCreateChild(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null) return t;
            t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static Transform FurnitureZone(ZoneData z) =>
            FindOrCreateChild(FindOrCreateChild(_furnitureRoot, z.group.ToString()), "Zone_" + z.zoneId);

        // Scenes built before the split kept furniture in School/<group>/Zone_<id>/Props.
        // Move it out as it is (with any hand edits) before School is rebuilt.
        static bool MigrateFurniture(Scene scene, Transform furniture)
        {
            var school = FindRoot(scene, RootName);
            if (school == null) return false;
            bool any = false;
            foreach (Transform group in school.transform)
            foreach (Transform zone in group)
            {
                var props = zone.Find("Props");
                if (props == null) continue;
                var target = FindOrCreateChild(FindOrCreateChild(furniture, group.name), zone.name);
                foreach (var child in props.Cast<Transform>().ToList()) child.SetParent(target, true);
                any = true;
            }
            return any;
        }
        // Local footprint of each prop: x along the prop, y = depth along its +Z.
        static readonly Dictionary<string, Vector2> Footprints = new Dictionary<string, Vector2>
        {
            { "Desk", new Vector2(0.6f, 0.45f) }, { "Chair", new Vector2(0.4f, 0.4f) },
            { "TeacherDesk", new Vector2(1.2f, 0.7f) }, { "Blackboard", new Vector2(3.6f, 0.08f) },
            { "CarpetBoard", new Vector2(3.1f, 0.05f) }, { "ShoeLocker", new Vector2(1f, 0.4f) },
            { "Bench", new Vector2(2f, 0.4f) }, { "BunkBed", new Vector2(0.9f, 2f) },
            { "Sink", new Vector2(0.6f, 0.45f) }, { "ToiletStall", new Vector2(1f, 1.5f) },
            { "Shower", new Vector2(1f, 1f) }, { "VendingMachine", new Vector2(1f, 0.8f) },
            { "WashingMachine", new Vector2(0.6f, 0.6f) }, { "Statue", new Vector2(1f, 1f) },
            { "ElevatorCabin", new Vector2(2f, 2f) }, { "TrashBin", new Vector2(0.6f, 0.6f) },
            { "StageBlock", new Vector2(2f, 2f) },
        };

        static void Furnish(ZoneData z)
        {
            var r = z.rect;
            float cx = r.center.x, cy = r.center.y;
            var door = DoorSide(z);

            switch (z.furnish)
            {
                case Funseki.School.Furnish.Classroom:
                    Classroom(z, "Blackboard");
                    break;

                case Funseki.School.Furnish.GeometryClass:
                    Classroom(z, "CarpetBoard");
                    break;

                case Funseki.School.Furnish.Anatomy:
                    Classroom(z, "Blackboard");
                    Prop(z, "Statue", r.xMax - 1f, r.yMin + 1f, Side.West);
                    break;

                case Funseki.School.Furnish.Workshop:
                    OnWall(z, "Blackboard", Side.West, cy);
                    for (float x = r.xMin + 2.5f; x <= r.xMax - 1f; x += 2.2f)
                    for (float y = r.yMin + 1.8f; y <= r.yMax - 1.5f; y += 2.2f)
                        Prop(z, "TeacherDesk", x, y, Side.North);
                    break;

                case Funseki.School.Furnish.Dorm:
                    Prop(z, "BunkBed", r.xMin + 0.75f, r.yMin + 1.25f, Side.South);
                    Prop(z, "BunkBed", r.xMax - 0.75f, r.yMin + 1.25f, Side.South);
                    for (int i = 0; i < 3; i++)
                    {
                        float x = r.xMin + 2.5f + i * 1.5f, y = r.yMin + 4f;
                        Prop(z, "Desk", x, y, Side.South);
                        Prop(z, "Chair", x, y + 0.45f, Side.South);
                    }
                    break;

                case Funseki.School.Furnish.Toilets:
                    AlongWall(z, "ToiletStall", Opposite(door), 1f, 4);
                    NearDoor(z, "Sink", door, 2, 0.8f);
                    break;

                case Funseki.School.Furnish.Showers:
                    AlongWall(z, "Shower", Opposite(door), 1.1f, 6);
                    Prop(z, "Bench", cx, cy, door == Side.North || door == Side.South ? Side.North : Side.East);
                    break;

                case Funseki.School.Furnish.Laundry:
                    AlongWall(z, "WashingMachine", Opposite(door), 0.8f, 8);
                    AlongWall(z, "WashingMachine", Side.North, 0.8f, 6);
                    Prop(z, "Bench", cx, cy + 1f, Side.North);
                    break;

                case Funseki.School.Furnish.Canteen:
                    for (float x = r.xMin + 4f; x <= r.xMax - 2f; x += 3.2f)
                    for (float y = r.yMin + 3f; y <= r.yMax - 2.5f; y += 2.6f)
                    {
                        Prop(z, "TeacherDesk", x, y, Side.North);
                        Prop(z, "Bench", x, y - 0.7f, Side.North);
                        Prop(z, "Bench", x, y + 0.7f, Side.North);
                    }
                    OnWall(z, "VendingMachine", Side.North, r.xMax - 3f);
                    OnWall(z, "VendingMachine", Side.North, r.xMax - 4.2f);
                    break;

                case Funseki.School.Furnish.Kitchen:
                    for (float y = r.yMin + 1.5f; y <= r.yMax - 1.5f; y += 1.4f)
                        OnWall(z, "TeacherDesk", Side.East, y);
                    AlongWall(z, "Sink", Side.West, 0.8f, 4);
                    break;

                case Funseki.School.Furnish.KitchenYard:
                    for (int i = 0; i < 3; i++) Prop(z, "TrashBin", r.xMin + 1f + i * 0.9f, r.yMin + 0.6f, Side.South);
                    break;

                case Funseki.School.Furnish.Gym:
                    for (float y = r.yMin + 2f; y <= r.yMax - 2f; y += 3f) OnWall(z, "Bench", Side.West, y);
                    break;

                case Funseki.School.Furnish.LockerRoom:
                    AlongWall(z, "ShoeLocker", Opposite(door), 1f, 6);
                    Prop(z, "Bench", cx, cy, Side.East);
                    break;

                case Funseki.School.Furnish.Weights:
                    for (int i = 0; i < 3; i++) Prop(z, "Bench", r.xMin + 2f + i * 3f, cy, Side.East);
                    break;

                case Funseki.School.Furnish.Storage:
                    AlongWall(z, "ShoeLocker", Opposite(door), 1f, 8);
                    break;

                case Funseki.School.Furnish.Office:
                {
                    Prop(z, "TeacherDesk", cx, cy, door);
                    var back = Dir(Opposite(door));
                    Prop(z, "Chair", cx + back.x * 0.75f, cy + back.y * 0.75f, Opposite(door));
                    AlongWall(z, "ShoeLocker", Opposite(door), 1f, 2);
                    break;
                }

                case Funseki.School.Furnish.StaffRoom:
                    for (float x = r.xMin + 2.5f; x <= r.xMax - 2f; x += 2.2f)
                    {
                        Prop(z, "TeacherDesk", x, cy - 0.45f, Side.North);
                        Prop(z, "TeacherDesk", x, cy + 0.45f, Side.South);
                        Prop(z, "Chair", x, cy - 1.2f, Side.North);
                        Prop(z, "Chair", x, cy + 1.2f, Side.South);
                    }
                    break;

                case Funseki.School.Furnish.Detention:
                    for (int i = 0; i < 4; i++)
                    {
                        float x = r.xMin + 2f + i * 1.3f;
                        Prop(z, "Desk", x, cy - 0.5f, Side.South);
                        Prop(z, "Chair", x, cy - 0.05f, Side.South);
                    }
                    break;

                case Funseki.School.Furnish.Assembly:
                    for (int x = r.xMin; x + 2 <= r.xMax; x += 2)
                    for (int y = r.yMax - 4; y + 2 <= r.yMax; y += 2)
                        Prop(z, "StageBlock", x + 1f, y + 1f, Side.North);
                    Prop(z, "Statue", cx, r.yMax - 1.5f, Side.North, S.stageHeight);
                    for (float x = r.xMin + 1.5f; x <= r.xMax - 1.5f; x += 0.8f)
                    for (float y = r.yMin + 4f; y <= r.yMax - 6f; y += 1.2f)
                        Prop(z, "Chair", x, y, Side.North);
                    break;

                case Funseki.School.Furnish.Elevator:
                    // Open front faces the door (east), so the cabin's +Z points west.
                    Prop(z, "ElevatorCabin", r.xMax - S.wallThickness - 1f, cy, Side.West);
                    break;

                case Funseki.School.Furnish.Courtyard:
                    Prop(z, "Bench", cx - 5f, cy - 3f, Side.South);
                    Prop(z, "Bench", cx + 5f, cy - 3f, Side.South);
                    Prop(z, "Bench", cx - 5f, cy + 3f, Side.North);
                    Prop(z, "Bench", cx + 5f, cy + 3f, Side.North);
                    break;

                case Funseki.School.Furnish.EntranceCorridor:
                    // Shoe lockers drawn on the plan: under rooms 14-17 and on both sides of the entrance.
                    foreach (var (a, b) in new[] { (6, 10), (14, 18), (34, 38), (42, 46) })
                        for (int x = a; x < b; x++) OnWall(z, "ShoeLocker", Side.North, x + 0.5f);
                    foreach (var (a, b) in new[] { (10, 22), (30, 42) })
                        for (int x = a; x < b; x++) OnWall(z, "ShoeLocker", Side.South, x + 0.5f);
                    break;

                case Funseki.School.Furnish.Hall:
                    OnWall(z, "VendingMachine", Side.South, r.xMin + 4f);
                    OnWall(z, "VendingMachine", Side.South, r.xMin + 5.2f);
                    break;
            }
        }

        static void Classroom(ZoneData z, string board)
        {
            var r = z.rect;
            OnWall(z, board, Side.West, r.center.y);
            Prop(z, "TeacherDesk", r.xMin + 1.6f, r.center.y, Side.East);
            for (float x = r.xMin + 3.2f; x <= r.xMax - 1f; x += 1.1f)
            for (float y = r.yMin + 1.2f; y <= r.yMax - 1f; y += 1.2f)
            {
                Prop(z, "Desk", x, y, Side.East);
                Prop(z, "Chair", x + 0.45f, y, Side.East);
            }
        }

        static Side DoorSide(ZoneData z)
        {
            foreach (var o in z.openings)
                if (o.type != OpeningType.Window && o.type != OpeningType.Open) return o.side;
            return Side.South;
        }

        static Side Opposite(Side s) =>
            s == Side.North ? Side.South : s == Side.South ? Side.North : s == Side.West ? Side.East : Side.West;

        // Plan direction of a side: North is -y.
        static Vector2 Dir(Side s) =>
            s == Side.North ? new Vector2(0, -1) : s == Side.South ? new Vector2(0, 1) : s == Side.West ? new Vector2(-1, 0) : new Vector2(1, 0);

        static float Yaw(Side facing) =>
            facing == Side.North ? 0f : facing == Side.East ? 90f : facing == Side.South ? 180f : 270f;

        // cx, cy: plan center of the footprint. facing: where the prop's +Z points.
        static GameObject Prop(ZoneData z, string name, float cx, float cy, Side facing, float yOffset = 0f)
        {
            if (!_prefabs.ContainsKey(name) || !Footprints.TryGetValue(name, out var fp))
            {
                _warnings.Add($"Prop {name} is missing from the kit (zone {z.zoneId}).");
                return null;
            }
            float yaw = Yaw(facing);
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var center = new Vector3(cx, Base(z.floor) + yOffset, -cy);
            var pivot = center - rot * new Vector3(fp.x * 0.5f, 0f, fp.y * 0.5f);
            return Spawn(name, FurnitureZone(z), pivot, yaw);
        }

        // Puts a prop with its back against a wall of the zone, centered at `along`.
        static void OnWall(ZoneData z, string name, Side wall, float along)
        {
            if (!Footprints.TryGetValue(name, out var fp)) { Prop(z, name, 0, 0, Side.North); return; }
            var r = z.rect;
            float inset = S.wallThickness + fp.y * 0.5f + 0.02f;
            switch (wall)
            {
                case Side.North: Prop(z, name, along, r.yMin + inset, Side.South); break;
                case Side.South: Prop(z, name, along, r.yMax - inset, Side.North); break;
                case Side.West: Prop(z, name, r.xMin + inset, along, Side.East); break;
                default: Prop(z, name, r.xMax - inset, along, Side.West); break;
            }
        }

        // A row of props along one wall, starting 0.7 m from its first corner.
        static void AlongWall(ZoneData z, string name, Side wall, float step, int max)
        {
            var r = z.rect;
            bool horizontal = wall == Side.North || wall == Side.South;
            float start = horizontal ? r.xMin : r.yMin;
            float len = horizontal ? r.width : r.height;
            int count = Mathf.Min(max, Mathf.FloorToInt((len - 1f) / step));
            for (int i = 0; i < count; i++) OnWall(z, name, wall, start + 0.7f + step * (i + 0.5f));
        }

        // A few props on the wall next to the door side, close to the door.
        static void NearDoor(ZoneData z, string name, Side door, int count, float step)
        {
            var r = z.rect;
            for (int i = 0; i < count; i++)
            {
                switch (door)
                {
                    case Side.South: OnWall(z, name, Side.West, r.yMax - 1f - i * step); break;
                    case Side.North: OnWall(z, name, Side.West, r.yMin + 1f + i * step); break;
                    case Side.West: OnWall(z, name, Side.North, r.xMin + 1f + i * step); break;
                    default: OnWall(z, name, Side.North, r.xMax - 1f - i * step); break;
                }
            }
        }
    }
}
