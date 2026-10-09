using System;
using System.Collections.Generic;
using System.Reflection;
using Funseki.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Funseki.DayCycle
{
    // Locks and unlocks school doors by the time of day (RoomAccessSettings).
    // When a phase starts with a new DayPhase.timeOfDay, every door that touches a room closed at that time gets locked;
    // doors of rooms open again get their own lock back (or none). Applied again when the school scene loads.
    // Door, LockedDoor and Zone live in the school's own assembly, so they are reached by name and reflection,
    // like SchoolSceneLoader and LessonWalk do. A closed door is a LockedDoor with an unlockDay no day reaches.
    public class RoomAccessDirector : MonoBehaviour
    {
        const int ClosedDay = 99;

        [SerializeField] RoomAccessSettings settings;
        [SerializeField] string schoolScene = "School_Greybox";

        class Gate
        {
            public Component door;        // Door, or null for a locked passage
            public Component lockComp;    // the school's own LockedDoor, or the one added here
            public bool lockAdded;
            public int ownUnlockDay = 1;
            public string ownLockLine, ownDoorLine;
            public Vector3 position;
            public readonly List<string> zones = new();
            public GameObject Go => door != null ? door.gameObject : lockComp != null ? lockComp.gameObject : null;
        }

        readonly List<Gate> gates = new();
        Type lockType;
        bool scanned;
        TimeOfDayIcon? applied, wanted;

        void OnEnable()
        {
            GameEvents.OnPhaseStarted += OnPhaseStarted;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            GameEvents.OnPhaseStarted -= OnPhaseStarted;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void Start()
        {
            if (settings == null) { Debug.LogWarning("[RoomAccess] No RoomAccessSettings assigned.", this); enabled = false; return; }
            if (wanted == null && ServiceLocator.TryGet<IDayCycle>(out var day) && day.CurrentPhase != null)
                wanted = day.CurrentPhase.timeOfDay;
            var school = SceneManager.GetSceneByName(schoolScene);
            if (school.isLoaded) Scan(school);
            Apply();
        }

        void OnPhaseStarted(DayPhase phase)
        {
            wanted = phase.timeOfDay;
            Apply();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != schoolScene) return;
            Scan(scene);
            applied = null;
            Apply();
        }

        // Only a change of the time of day touches the doors: inside one time of day the story
        // (LessonWalk opening the gym, scripted doors) keeps what it did.
        void Apply()
        {
            if (!enabled || settings == null || !scanned || wanted == null || applied == wanted) return;
            var time = wanted.Value;
            applied = time;

            int closed = 0;
            foreach (var g in gates)
            {
                if (g.Go == null) continue;
                bool open = true;
                foreach (var z in g.zones)
                    if (!settings.IsOpen(z, time)) { open = false; break; }
                if (open) Open(g);
                else { Close(g); closed++; }
            }
            Debug.Log($"[RoomAccess] {Label(time)}: {closed} of {gates.Count} door(s) closed.");
        }

        void Close(Gate g)
        {
            if (g.door != null && IsDoorOpen(g.door) && !HeroNear(g.position))
                g.door.SendMessage("Toggle", g.position + g.door.transform.forward, SendMessageOptions.DontRequireReceiver);

            if (g.lockComp == null)
            {
                if (lockType == null) return;
                g.lockComp = g.Go.AddComponent(lockType);
                g.lockAdded = true;
                if (g.zones.Count > 0) Set(g.lockComp, "zoneId", g.zones[0]);
            }
            Set(g.lockComp, "unlockDay", ClosedDay);
            Set(g.lockComp, "lockedLine", settings.closedLine);
            if (g.door != null) Set(g.door, "lockedLine", settings.closedLine);
        }

        void Open(Gate g)
        {
            if (g.door != null && g.ownDoorLine != null) Set(g.door, "lockedLine", g.ownDoorLine);
            if (g.lockComp == null) return;
            if (g.lockAdded)
            {
                Destroy(g.lockComp);
                g.lockComp = null;
                g.lockAdded = false;
                return;
            }
            Set(g.lockComp, "unlockDay", g.ownUnlockDay);
            if (g.ownLockLine != null) Set(g.lockComp, "lockedLine", g.ownLockLine);
        }

        // ---------------------------------------------------------------- scan

        void Scan(Scene scene)
        {
            gates.Clear();
            var zones = new List<(string id, Bounds bounds)>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    var zone = t.GetComponent("Zone");
                    if (zone == null) continue;
                    var id = Get<string>(zone, "zoneId");
                    var box = t.GetComponent<BoxCollider>();
                    if (string.IsNullOrEmpty(id) || box == null) continue;
                    var b = new Bounds(t.TransformPoint(box.center), Vector3.Scale(box.size, t.lossyScale));
                    b.Expand(new Vector3(settings.doorTouchDistance * 2f, 0f, settings.doorTouchDistance * 2f));
                    zones.Add((id, b));
                }

            foreach (var root in scene.GetRootGameObjects())
            {
                if (!root.activeSelf) continue;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    var door = t.GetComponent("Door");
                    var lockComp = t.GetComponent("LockedDoor");
                    if (door == null && lockComp == null) continue;
                    if (lockComp != null) lockType = lockComp.GetType();

                    var g = new Gate { door = door, lockComp = lockComp, position = Center(t) };
                    if (door != null) g.ownDoorLine = Get<string>(door, "lockedLine");
                    if (lockComp != null)
                    {
                        g.ownUnlockDay = Get<int>(lockComp, "unlockDay");
                        g.ownLockLine = Get<string>(lockComp, "lockedLine");
                        var own = Get<string>(lockComp, "zoneId");
                        if (!string.IsNullOrEmpty(own)) g.zones.Add(own);
                    }
                    foreach (var (id, b) in zones)
                        if (b.Contains(g.position) && !g.zones.Contains(id)) g.zones.Add(id);
                    gates.Add(g);
                }
            }

            // A school with no LockedDoor at all still has the type in its assembly.
            if (lockType == null && gates.Count > 0 && gates[0].door != null)
                lockType = gates[0].door.GetType().Assembly.GetType("Funseki.School.LockedDoor");
            scanned = true;

            if (lockType == null) Debug.LogWarning("[RoomAccess] Funseki.School.LockedDoor not found: doors cannot be closed.");
            foreach (var r in settings.rooms)
            {
                bool found = false;
                foreach (var g in gates) if (g.zones.Contains(r.zoneId)) { found = true; break; }
                if (!found && (!r.morning || !r.day || !r.evening))
                    Debug.LogWarning($"[RoomAccess] '{r.name}' ({r.zoneId}) has no door: it cannot be closed (open passages stay open).");
            }
        }

        static Vector3 Center(Transform t)
        {
            var col = t.GetComponentInChildren<Collider>(true);
            return col != null && !col.isTrigger ? col.bounds.center : t.position;
        }

        static bool HeroNear(Vector3 p, float radius)
        {
            foreach (HeroId id in Enum.GetValues(typeof(HeroId)))
            {
                var hero = HeroService.GetHero(id);
                if (hero != null && Vector3.Distance(hero.transform.position, p) < radius) return true;
            }
            var current = HeroService.CurrentObject;
            return current != null && Vector3.Distance(current.transform.position, p) < radius;
        }

        bool HeroNear(Vector3 p) => HeroNear(p, settings.keepOpenNearHero);

        static bool IsDoorOpen(Component door)
        {
            var prop = door.GetType().GetProperty("IsOpen");
            return prop != null && (bool)prop.GetValue(door);
        }

        static T Get<T>(Component c, string field)
        {
            var f = c.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance);
            return f != null && f.GetValue(c) is T v ? v : default;
        }

        static void Set(Component c, string field, object value)
        {
            var f = c.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance);
            if (f != null && f.FieldType.IsInstanceOfType(value)) f.SetValue(c, value);
        }

        static string Label(TimeOfDayIcon t) => t switch
        {
            TimeOfDayIcon.Dawn => "утро",
            TimeOfDayIcon.Sunset => "вечер",
            _ => "день",
        };
    }
}
