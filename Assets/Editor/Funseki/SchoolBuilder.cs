using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Funseki.School;

namespace Funseki.School.EditorTools
{
    // Builds the greybox school from SchoolLayout + KitSettings into School_Greybox.unity.
    // Idempotent: the previous "School" root is deleted and everything is rebuilt.
    //
    // Walls: every floor is rasterized into 1 m cells (cell -> zone). A wall goes on each cell
    // edge between two different zones, once, on the side of the zone with the higher priority
    // (room > outdoor > corridor). Edges are merged into runs and filled with 2 m modules plus a
    // 1 m piece for odd lengths. Doors and windows from the layout cut the runs.
    public static partial class SchoolBuilder
    {
        public const string ScenePath = KitGenerator.Root + "Scenes/School_Greybox.unity";
        const string RootName = "School";
        // Furniture from the zone presets: placed once, then left alone so hand edits survive rebuilds.
        public const string FurnitureRootName = "School_Furniture";
        // The user's own assets: the builder only makes sure the root exists.
        public const string PropsRootName = "School_Props";

        static KitSettings S;
        static Dictionary<string, GameObject> _prefabs;
        static Dictionary<int, Dictionary<Vector2Int, ZoneData>> _raster;
        static Dictionary<ZoneData, Transform> _zoneRoots;
        static List<string> _warnings;

        class AbsOpening
        {
            public bool vertical;
            public int line, a, b;
            public OpeningType type;
            public ZoneData zone;
            public bool placed;
        }

        class Edge
        {
            public bool vertical;
            public int line, pos;
            public ZoneData owner, other;
            public bool ownerPositive; // owner is east (vertical) or south (horizontal) of the line
        }

        [MenuItem("Tools/Funseki/Rebuild School (no window)")]
        public static void BuildFromMenu() => Build(SchoolLayoutDefaults.LoadOrCreate(), KitGenerator.LoadOrCreateSettings(), false);

        public static bool Build(SchoolLayout layout, KitSettings settings, bool interactive)
        {
            if (layout == null || settings == null)
            {
                Debug.LogError("[Funseki] Build School needs a SchoolLayout and KitSettings.");
                return false;
            }
            S = settings;
            _warnings = new List<string>();
            if (!LoadPrefabs()) return false;

            var scene = OpenSchoolScene(interactive);
            if (!scene.IsValid()) return false;

            if (IsFrozen(scene))
            {
                const string msg = "School_Greybox is frozen for hand editing, so Build School will not touch it. " +
                                   "Use Tools > Funseki > Unfreeze School first if you really want to rebuild (hand edits in School will be lost).";
                Debug.LogWarning("[Funseki] " + msg);
                if (interactive) EditorUtility.DisplayDialog("Build School", msg, "OK");
                return false;
            }

            var furnitureRoot = FindRoot(scene, FurnitureRootName);
            bool migrated = false;
            if (furnitureRoot == null)
            {
                furnitureRoot = CreateRoot(scene, FurnitureRootName);
                migrated = MigrateFurniture(scene, furnitureRoot.transform);
            }
            if (FindRoot(scene, PropsRootName) == null) CreateRoot(scene, PropsRootName);
            _furnitureRoot = furnitureRoot.transform;

            foreach (var go in scene.GetRootGameObjects())
                if (go.name == RootName) Object.DestroyImmediate(go);

            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            var groups = new Dictionary<ZoneGroup, Transform>();
            foreach (ZoneGroup g in System.Enum.GetValues(typeof(ZoneGroup)))
            {
                var t = new GameObject(g.ToString()).transform;
                t.SetParent(root.transform, false);
                groups[g] = t;
            }

            var zones = layout.zones.Where(z => z != null && !string.IsNullOrEmpty(z.zoneId)).ToList();
            Rasterize(zones);

            _zoneRoots = new Dictionary<ZoneData, Transform>();
            foreach (var z in zones) _zoneRoots[z] = CreateZoneRoot(z, groups[z.group]);

            foreach (var floor in _raster.Keys.OrderBy(f => f)) BuildWalls(floor, zones);
            foreach (var z in zones)
            {
                BuildFloors(z);
                BuildCeilings(z);
                if (z.kind == ZoneKind.Stair || z.kind == ZoneKind.StairTop) BuildStairs(z);
                // Only zones that have no furniture yet: new zones, or the first build.
                if (_furnitureRoot.Find(z.group + "/Zone_" + z.zoneId) == null) Furnish(z);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            foreach (var w in _warnings) Debug.LogWarning("[Funseki] " + w);
            if (migrated) Debug.Log("[Funseki] Furniture moved from School to " + FurnitureRootName + " (kept as it was in the scene).");
            Debug.Log($"[Funseki] School built: {zones.Count} zones, {root.GetComponentsInChildren<Transform>().Length} objects -> {ScenePath}");
            return true;
        }

        // ---------- setup ----------

        static bool LoadPrefabs()
        {
            _prefabs = new Dictionary<string, GameObject>();
            foreach (var guid in AssetDatabase.FindAssets("PF_Kit_ t:Prefab", new[] { KitGenerator.PrefabDir.TrimEnd('/') }))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                _prefabs[go.name.Substring("PF_Kit_".Length)] = go;
            }
            foreach (var required in new[] { "Wall", "Wall_1m", "WallWindow", "WallDoorway", "WallDoorwayDouble", "WallSlidingDoor",
                         "Door_Swing", "Door_Double", "Floor", "Floor_1x1", "Ceiling", "Ceiling_1x1", "StairFlight", "Railing" })
            {
                if (_prefabs.ContainsKey(required)) continue;
                Debug.LogError($"[Funseki] Kit prefab PF_Kit_{required} is missing. Run Tools > Funseki > Generate Kit first.");
                return false;
            }
            return true;
        }

        internal static Scene OpenSchoolScene(bool interactive)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var loaded = SceneManager.GetSceneAt(i);
                if (loaded.path == ScenePath)
                {
                    SceneManager.SetActiveScene(loaded);
                    return loaded;
                }
            }

            bool anyDirty = false;
            for (int i = 0; i < SceneManager.sceneCount; i++) anyDirty |= SceneManager.GetSceneAt(i).isDirty;
            if (interactive && anyDirty)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return default;
                anyDirty = false;
            }

            // Never discard someone's unsaved scene: open the school next to it instead.
            bool additive = anyDirty;
            Scene scene;
            if (File.Exists(ScenePath))
                scene = EditorSceneManager.OpenScene(ScenePath, additive ? OpenSceneMode.Additive : OpenSceneMode.Single);
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, additive ? NewSceneMode.Additive : NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            SceneManager.SetActiveScene(scene);
            return scene;
        }

        static void Rasterize(List<ZoneData> zones)
        {
            _raster = new Dictionary<int, Dictionary<Vector2Int, ZoneData>>();
            foreach (var z in zones)
            for (int f = z.floor; f <= z.TopFloor; f++)
            {
                if (!_raster.TryGetValue(f, out var cells)) _raster[f] = cells = new Dictionary<Vector2Int, ZoneData>();
                for (int x = z.rect.xMin; x < z.rect.xMax; x++)
                for (int y = z.rect.yMin; y < z.rect.yMax; y++)
                {
                    var c = new Vector2Int(x, y);
                    if (cells.TryGetValue(c, out var prev) && prev != z)
                        _warnings.Add($"Zones {prev.zoneId} and {z.zoneId} overlap at ({x},{y}) on floor {f}.");
                    cells[c] = z;
                }
            }
        }

        static ZoneData Cell(int floor, int x, int y) =>
            _raster.TryGetValue(floor, out var cells) && cells.TryGetValue(new Vector2Int(x, y), out var z) ? z : null;

        static float Base(int floor) => (floor - 1) * S.floorHeight;

        static Transform CreateZoneRoot(ZoneData z, Transform group)
        {
            var go = new GameObject("Zone_" + z.zoneId);
            go.transform.SetParent(group, false);
            var zone = go.AddComponent<Zone>();
            zone.zoneId = z.zoneId;
            zone.displayName = z.displayName;
            zone.floor = z.floor;
            zone.unlockDay = z.unlockDay;

            float h = z.heightFloors * S.floorHeight;
            var trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(z.rect.center.x, Base(z.floor) + h * 0.5f, -z.rect.center.y);
            trigger.size = new Vector3(z.rect.width, h, z.rect.height);
            return go.transform;
        }

        static Transform Child(ZoneData z, string name)
        {
            var parent = _zoneRoots[z];
            var t = parent.Find(name);
            if (t != null) return t;
            t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static GameObject Spawn(string prefab, Transform parent, Vector3 position, float yaw)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(_prefabs[prefab], parent);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            return go;
        }

        // Places a module that runs along a grid line, +Z pointing into the owner side.
        // u is the plan coordinate where the module starts along the line.
        static GameObject PlaceOnLine(string prefab, Transform parent, bool vertical, int line, float u, float len,
            bool ownerPositive, float y)
        {
            LineFrame(vertical, line, u, len, ownerPositive, y, out var position, out var yaw);
            return Spawn(prefab, parent, position, yaw);
        }

        static void LineFrame(bool vertical, int line, float u, float len, bool ownerPositive, float y,
            out Vector3 position, out float yaw)
        {
            if (!vertical)
            {
                position = ownerPositive ? new Vector3(u + len, y, -line) : new Vector3(u, y, -line);
                yaw = ownerPositive ? 180f : 0f;
            }
            else
            {
                position = ownerPositive ? new Vector3(line, y, -u) : new Vector3(line, y, -(u + len));
                yaw = ownerPositive ? 90f : 270f;
            }
        }

        // ---------- walls, doors, windows ----------

        static int Priority(ZoneData z)
        {
            if (z == null) return -1;
            switch (z.kind)
            {
                case ZoneKind.Void: return 0;
                case ZoneKind.Corridor: return 1;
                case ZoneKind.Outdoor: return 2;
                default: return 3;
            }
        }

        static bool IsExterior(ZoneData z) => z == null || z.kind == ZoneKind.Outdoor || z.kind == ZoneKind.Void;

        static void BuildWalls(int floor, List<ZoneData> zones)
        {
            var cells = _raster[floor];
            int minX = cells.Keys.Min(c => c.x) - 1, maxX = cells.Keys.Max(c => c.x) + 1;
            int minY = cells.Keys.Min(c => c.y) - 1, maxY = cells.Keys.Max(c => c.y) + 1;

            var edges = new List<Edge>();
            for (int x = minX; x <= maxX; x++)
            for (int y = minY; y <= maxY; y++)
            {
                var a = Cell(floor, x, y);
                AddEdge(edges, true, x + 1, y, a, Cell(floor, x + 1, y));
                AddEdge(edges, false, y + 1, x, a, Cell(floor, x, y + 1));
            }

            var openings = CollectOpenings(floor, zones);

            var sorted = edges.OrderBy(e => e.vertical).ThenBy(e => e.line).ThenBy(e => e.pos).ToList();
            int i = 0;
            while (i < sorted.Count)
            {
                var first = sorted[i];
                int j = i + 1;
                while (j < sorted.Count && SameRun(sorted[j - 1], sorted[j])) j++;
                BuildRun(floor, first, first.pos, sorted[j - 1].pos + 1, openings);
                i = j;
            }

            foreach (var o in openings)
                if (!o.placed && o.type != OpeningType.Open)
                    _warnings.Add($"{o.type} of {o.zone.zoneId} on floor {floor} at line {o.line} ({o.a}..{o.b}) does not fit a single wall run and was skipped.");
        }

        static void AddEdge(List<Edge> edges, bool vertical, int line, int pos, ZoneData a, ZoneData b)
        {
            if (a == b) return;
            if (a != null && b != null && !string.IsNullOrEmpty(a.openGroup) && a.openGroup == b.openGroup) return;
            int pa = Priority(a), pb = Priority(b);
            if (Mathf.Max(pa, pb) <= 0) return;
            var owner = pa > pb ? a : b; // ties go to the east/south side
            var other = owner == a ? b : a;
            if (other == null && owner.kind == ZoneKind.Outdoor) return;
            edges.Add(new Edge { vertical = vertical, line = line, pos = pos, owner = owner, other = other, ownerPositive = owner == b });
        }

        static bool SameRun(Edge p, Edge n) =>
            p.vertical == n.vertical && p.line == n.line && n.pos == p.pos + 1 &&
            p.owner == n.owner && p.other == n.other && p.ownerPositive == n.ownerPositive;

        static List<AbsOpening> CollectOpenings(int floor, List<ZoneData> zones)
        {
            var list = new List<AbsOpening>();
            foreach (var z in zones)
            foreach (var o in z.openings)
            {
                if (z.floor + o.floorOffset != floor) continue;
                int len = o.type == OpeningType.DoorDouble ? Mathf.RoundToInt(S.doubleDoorModuleWidth)
                    : o.type == OpeningType.Open ? Mathf.Max(1, o.width)
                    : Mathf.RoundToInt(S.wallModuleWidth);
                var r = z.rect;
                var abs = new AbsOpening { type = o.type, zone = z };
                switch (o.side)
                {
                    case Side.North: abs.vertical = false; abs.line = r.yMin; abs.a = r.xMin + o.offset; break;
                    case Side.South: abs.vertical = false; abs.line = r.yMax; abs.a = r.xMin + o.offset; break;
                    case Side.West: abs.vertical = true; abs.line = r.xMin; abs.a = r.yMin + o.offset; break;
                    default: abs.vertical = true; abs.line = r.xMax; abs.a = r.yMin + o.offset; break;
                }
                abs.b = abs.a + len;
                list.Add(abs);
            }
            return list;
        }

        static void BuildRun(int floor, Edge e, int s, int end, List<AbsOpening> openings)
        {
            var walls = Child(e.owner, "Walls");
            float y = Base(floor);
            bool windows = (e.owner.autoWindows || (e.other != null && e.other.autoWindows)) &&
                           (IsExterior(e.other) || IsExterior(e.owner));

            var cuts = new List<(int a, int b, AbsOpening o)>();
            foreach (var o in openings)
            {
                if (o.vertical != e.vertical || o.line != e.line || o.b <= s || o.a >= end) continue;
                if (o.type == OpeningType.Open) cuts.Add((Mathf.Max(o.a, s), Mathf.Min(o.b, end), o));
                else if (o.a >= s && o.b <= end) cuts.Add((o.a, o.b, o));
            }
            cuts.Sort((p, q) => p.a.CompareTo(q.a));

            int cursor = s;
            foreach (var c in cuts)
            {
                if (c.a < cursor) continue;
                FillWall(walls, e, cursor, c.a, y, windows);
                PlaceOpening(walls, e, c.a, c.b, y, c.o);
                c.o.placed = true;
                cursor = c.b;
            }
            FillWall(walls, e, cursor, end, y, windows);

            // Tall zones: close the slab-thick gap between the wall rows.
            if (floor > e.owner.floor)
            {
                float band = y - S.slabThickness;
                for (int u = s; u < end; u += 2)
                {
                    int len = Mathf.Min(2, end - u);
                    var piece = PlaceOnLine(len == 2 ? "Wall" : "Wall_1m", walls, e.vertical, e.line, u, len, e.ownerPositive, band);
                    piece.transform.localScale = new Vector3(1f, S.slabThickness / S.ceilingHeight, 1f);
                }
            }
        }

        static void FillWall(Transform parent, Edge e, int a, int b, float y, bool windows)
        {
            int len = b - a;
            if (len <= 0) return;
            int modules = len / 2;
            for (int k = 0; k < modules; k++)
            {
                bool window = windows && (modules == 1 || k % 2 == 1);
                PlaceOnLine(window ? "WallWindow" : "Wall", parent, e.vertical, e.line, a + k * 2, 2, e.ownerPositive, y);
            }
            if (len % 2 == 1)
                PlaceOnLine("Wall_1m", parent, e.vertical, e.line, a + modules * 2, 1, e.ownerPositive, y);
        }

        static void PlaceOpening(Transform walls, Edge e, int a, int b, float y, AbsOpening o)
        {
            var zone = o.zone;
            bool locked = zone.unlockDay > 1;
            float t = S.wallThickness, dt = S.doorThickness;
            var doors = Child(e.owner, "Doors");
            GameObject leaf = null, wall = null;

            switch (o.type)
            {
                case OpeningType.Window:
                    PlaceOnLine("WallWindow", walls, e.vertical, e.line, a, b - a, e.ownerPositive, y);
                    return;

                case OpeningType.Doorway:
                    PlaceOnLine("WallDoorway", walls, e.vertical, e.line, a, b - a, e.ownerPositive, y);
                    break;

                case OpeningType.DoorSliding:
                    wall = PlaceOnLine("WallSlidingDoor", walls, e.vertical, e.line, a, b - a, e.ownerPositive, y);
                    leaf = wall.transform.Find("Door")?.gameObject;
                    break;

                case OpeningType.DoorSwing:
                {
                    wall = PlaceOnLine("WallDoorway", walls, e.vertical, e.line, a, b - a, e.ownerPositive, y);
                    float x0 = (S.wallModuleWidth - S.doorWidth) * 0.5f;
                    leaf = SpawnLocal("Door_Swing", doors, wall.transform, new Vector3(x0, 0f, (t - dt) * 0.5f));
                    break;
                }

                case OpeningType.DoorDouble:
                {
                    wall = PlaceOnLine("WallDoorwayDouble", walls, e.vertical, e.line, a, b - a, e.ownerPositive, y);
                    float x0 = (S.doubleDoorModuleWidth - S.doubleDoorWidth) * 0.5f;
                    leaf = SpawnLocal("Door_Double", doors, wall.transform, new Vector3(x0, 0f, (t - dt) * 0.5f));
                    break;
                }

                case OpeningType.Open:
                    if (locked)
                    {
                        // An open passage into a locked zone still needs something to block it.
                        LineFrame(e.vertical, e.line, a, b - a, e.ownerPositive, y, out var position, out var yaw);
                        leaf = new GameObject("LockedPassage");
                        leaf.transform.SetParent(doors, false);
                        leaf.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
                        var box = leaf.AddComponent<BoxCollider>();
                        box.center = new Vector3((b - a) * 0.5f, S.ceilingHeight * 0.5f, t * 0.5f);
                        box.size = new Vector3(b - a, S.ceilingHeight, t);
                    }
                    break;
            }

            if (zone.zoneId == "floor_3" && wall != null && _prefabs.ContainsKey("SignMeeting"))
            {
                // The joke door: the plate faces the stairs, the wall owner's side.
                float x0 = (S.wallModuleWidth - S.doorWidth) * 0.5f;
                SpawnLocal("SignMeeting", doors, wall.transform, new Vector3(x0 + S.doorWidth * 0.5f - 0.4f, 0f, t + 0.01f));
            }

            if (leaf != null && o.type != OpeningType.Open)
            {
                var door = leaf.AddComponent<Door>();
                door.kind = o.type == OpeningType.DoorSliding ? Door.Kind.Sliding
                    : o.type == OpeningType.DoorDouble ? Door.Kind.Double
                    : Door.Kind.Swing;
                door.slideDistance = S.doorWidth + 0.05f;
            }

            if (locked && leaf != null)
            {
                var lockComp = leaf.AddComponent<LockedDoor>();
                lockComp.zoneId = zone.zoneId;
                lockComp.unlockDay = zone.unlockDay;
            }
        }

        static GameObject SpawnLocal(string prefab, Transform parent, Transform frame, Vector3 local)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(_prefabs[prefab], parent);
            go.transform.SetPositionAndRotation(frame.TransformPoint(local), frame.rotation);
            return go;
        }

        // ---------- floors and ceilings ----------

        static void BuildFloors(ZoneData z)
        {
            if (z.kind == ZoneKind.Void) return;
            var parent = Child(z, "Floor");
            var r = z.rect;
            if (z.kind == ZoneKind.StairTop)
            {
                TileRect(parent, StripRect(z), Base(z.floor), "Floor", "Floor_1x1");
                return;
            }
            TileRect(parent, r, Base(z.floor), "Floor", "Floor_1x1");
        }

        static RectInt StripRect(ZoneData z)
        {
            var r = z.rect;
            return z.stairEntry == Side.North
                ? new RectInt(r.xMin, r.yMin, r.width, 1)
                : new RectInt(r.xMin, r.yMax - 1, r.width, 1);
        }

        static void BuildCeilings(ZoneData z)
        {
            if (z.kind == ZoneKind.Outdoor || z.kind == ZoneKind.Void) return;
            int above = z.TopFloor + 1;
            var allowed = new HashSet<Vector2Int>();
            for (int x = z.rect.xMin; x < z.rect.xMax; x++)
            for (int y = z.rect.yMin; y < z.rect.yMax; y++)
                if (IsExterior(Cell(above, x, y))) allowed.Add(new Vector2Int(x, y));
            if (allowed.Count == 0) return;
            TileRect(Child(z, "Ceiling"), z.rect, Base(z.TopFloor), "Ceiling", "Ceiling_1x1", allowed);
        }

        // Covers a plan rect with 2x2 tiles, filling what is left with 1x1 tiles.
        static void TileRect(Transform parent, RectInt r, float y, string big, string small, HashSet<Vector2Int> allowed = null)
        {
            var done = new HashSet<Vector2Int>();
            bool Free(int x, int yy) =>
                x < r.xMax && yy < r.yMax && !done.Contains(new Vector2Int(x, yy)) &&
                (allowed == null || allowed.Contains(new Vector2Int(x, yy)));

            for (int yy = r.yMin; yy < r.yMax; yy++)
            for (int x = r.xMin; x < r.xMax; x++)
            {
                if (!Free(x, yy)) continue;
                if (Free(x + 1, yy) && Free(x, yy + 1) && Free(x + 1, yy + 1))
                {
                    Spawn(big, parent, new Vector3(x, y, -(yy + 2)), 0f);
                    done.Add(new Vector2Int(x, yy)); done.Add(new Vector2Int(x + 1, yy));
                    done.Add(new Vector2Int(x, yy + 1)); done.Add(new Vector2Int(x + 1, yy + 1));
                }
                else
                {
                    Spawn(small, parent, new Vector3(x, y, -(yy + 1)), 0f);
                    done.Add(new Vector2Int(x, yy));
                }
            }
        }

        // ---------- stairs ----------

        // Two flights in a U: up along the west side to a landing, back along the east side.
        static void BuildStairs(ZoneData z)
        {
            var r = z.rect;
            float y = Base(z.floor), half = S.floorHeight * 0.5f, run = S.StairRun, sw = S.stairWidth;
            int x0 = r.xMin, x1 = r.xMax;
            var parent = Child(z, "Stairs");

            if (z.kind == ZoneKind.StairTop)
            {
                StripRailing(parent, z, y);
                return;
            }
            if (z.stairEntry != Side.North && z.stairEntry != Side.South)
            {
                _warnings.Add($"Stairs {z.zoneId}: only North or South entry is supported.");
                return;
            }
            if (r.height < run + 2)
            {
                _warnings.Add($"Stairs {z.zoneId}: zone is too short for a flight of {run} m.");
                return;
            }

            RectInt landing;
            int landingEdge;
            if (z.stairEntry == Side.South)
            {
                float yStart = r.yMax - 1, yTop = yStart - run;
                Spawn("StairFlight", parent, new Vector3(x0, y, -yStart), 0f);
                Spawn("StairFlight", parent, new Vector3(x1, y + half, -yTop), 180f);
                landingEdge = Mathf.RoundToInt(yTop);
                landing = new RectInt(x0, r.yMin, r.width, landingEdge - r.yMin);
            }
            else
            {
                float yStart = r.yMin + 1, yTop = yStart + run;
                Spawn("StairFlight", parent, new Vector3(x0 + sw, y, -yStart), 180f);
                Spawn("StairFlight", parent, new Vector3(x1 - sw, y + half, -yTop), 0f);
                landingEdge = Mathf.RoundToInt(yTop);
                landing = new RectInt(x0, landingEdge, r.width, r.yMax - landingEdge);
            }
            TileRect(parent, landing, y + half, "Floor", "Floor_1x1");

            // Railing along the landing edge over the well between the flights.
            bool landingSouthOfEdge = z.stairEntry == Side.North;
            for (float u = x0 + sw; u + 2 <= x1 - sw + 0.01f; u += 2)
                PlaceOnLine("Railing", parent, false, landingEdge, u, 2, landingSouthOfEdge, y + half);

            if (z.topLanding)
            {
                TileRect(parent, StripRect(z), y + S.floorHeight, "Floor", "Floor_1x1");
                StripRailing(parent, z, y + S.floorHeight);
            }
        }

        // The arrival strip on the upper floor: railing along its inner edge, except above the
        // second flight, which arrives at the east end.
        static void StripRailing(Transform parent, ZoneData z, float y)
        {
            var strip = StripRect(z);
            bool south = z.stairEntry == Side.South;
            int line = south ? strip.yMin : strip.yMax;
            for (int u = strip.xMin; u + 2 <= strip.xMax - S.stairWidth + 0.01f; u += 2)
                PlaceOnLine("Railing", parent, false, line, u, 2, south, y);
        }
    }
}
