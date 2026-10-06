using System.Collections;
using System.Collections.Generic;
using Funseki.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Funseki.Lessons
{
    // After the bell of a break, the teacher leads the students from where they stood to the lesson
    // (Физра: from the courtyard to the gym). The route is this object's children in order; points named "Door…"
    // mark doors of the school greybox that are unlocked and opened at the bell, so the class (and the heroes) can pass.
    // While walking, the walkers' own routine scripts (pauseWhileWalking, e.g. WhistleTeacher) are switched off.
    // When the lesson is over everybody walks out: the walkers go back along the route to the places they had at the
    // bell (their scripts come back on as each arrives), and the mini-game's extras (LessonMiniGame.Leavers) walk out
    // of the door and disappear in the corridor.
    public class LessonWalk : MonoBehaviour
    {
        [SerializeField] LessonWalkSettings settings;
        [Tooltip("The lesson this walk leads to: the LessonDirector hands its extras over when it ends")]
        [SerializeField] string lessonId = "fizra";
        [Tooltip("The teacher first, then the students")]
        [SerializeField] Transform[] walkers = new Transform[0];
        [Tooltip("Routine scripts of the walkers that would move them (the break-time PE teacher)")]
        [SerializeField] Behaviour[] pauseWhileWalking = new Behaviour[0];
        [Tooltip("Scene with the school doors")]
        [SerializeField] string schoolScene = "School_Greybox";

        // One moving character: a path of points, then a final rotation, or removal for the lesson extras.
        class Mover
        {
            public Transform who;
            public List<Vector3> path;
            public int next;
            public float startAt;
            public Quaternion? endRotation;
            public bool destroyAtEnd;
        }

        static readonly List<LessonWalk> all = new();
        readonly List<Vector3> route = new();
        readonly List<Vector3> doorPoints = new();
        readonly List<Mover> movers = new();
        Vector3[] homePos;
        Quaternion[] homeRot;
        bool walked;

        public static LessonWalk Find(string id)
        {
            foreach (var w in all)
                if (w.lessonId == id) return w;
            return null;
        }

        void OnEnable()
        {
            all.Add(this);
            GameEvents.OnPhaseEnded += OnPhaseEnded;
            GameEvents.OnPhaseStarted += OnPhaseStarted;
        }

        void OnDisable()
        {
            all.Remove(this);
            GameEvents.OnPhaseEnded -= OnPhaseEnded;
            GameEvents.OnPhaseStarted -= OnPhaseStarted;
        }

        void OnPhaseEnded(DayPhase phase)
        {
            if (phase == null || phase.id != settings.afterPhaseId || walked) return;
            StartWalk();
        }

        // The lesson is over and the next break began: everybody walks back to their break places.
        void OnPhaseStarted(DayPhase phase)
        {
            if (phase == null || phase.type != DayPhaseType.Break || !walked) return;
            WalkBack();
        }

        void ReadRoute()
        {
            route.Clear();
            doorPoints.Clear();
            foreach (Transform p in transform)
            {
                route.Add(p.position);
                if (p.name.StartsWith("Door")) doorPoints.Add(p.position);
            }
        }

        void StartWalk()
        {
            ReadRoute();
            if (route.Count == 0) { Debug.LogWarning("[LessonWalk] No route points.", this); return; }

            int n = walkers.Length;
            homePos = new Vector3[n];
            homeRot = new Quaternion[n];
            movers.Clear();
            for (int i = 0; i < n; i++)
            {
                if (walkers[i] == null) continue;
                homePos[i] = walkers[i].position;
                homeRot[i] = walkers[i].rotation;
                movers.Add(new Mover
                {
                    who = walkers[i],
                    path = new List<Vector3>(route) { StandSpot(i) },
                    startAt = Time.time + settings.startDelay + i * settings.followDelay,
                    endRotation = Quaternion.LookRotation(LastDirection(), Vector3.up),
                });
            }
            foreach (var b in pauseWhileWalking) if (b != null) b.enabled = false;
            walked = true;
            StartCoroutine(OpenDoors());

            if (n > 0 && walkers[0] != null) Say(walkers[0], Pick(settings.teacherStartLines));
            for (int i = 1; i < n; i++)
                if (walkers[i] != null && Random.value < settings.studentLineChance) Say(walkers[i], Pick(settings.studentLines));
            Debug.Log($"[LessonWalk] {name}: {n} walker(s) set off, {route.Count} points, {doorPoints.Count} door(s).");
        }

        void WalkBack()
        {
            walked = false;
            if (homePos == null) return;
            var back = new List<Vector3>(route);
            back.Reverse();
            // The students leave first (they stand nearer the door), the teacher last.
            for (int i = 0; i < walkers.Length; i++)
            {
                var w = walkers[i];
                if (w == null) continue;
                movers.RemoveAll(m => m.who == w);
                movers.Add(new Mover
                {
                    who = w,
                    path = new List<Vector3>(back) { homePos[i] },
                    endRotation = homeRot[i],
                    startAt = Time.time + settings.startDelay + (walkers.Length - 1 - i) * settings.followDelay,
                });
            }
            Debug.Log($"[LessonWalk] {name}: the class walks back to the break places.");
        }

        /// <summary>The mini-game's extras (students of the lesson) walk out of the door into the corridor and vanish.</summary>
        public void TakeLeavers(IEnumerable<Transform> leavers)
        {
            if (leavers == null) return;
            if (route.Count == 0) ReadRoute();
            // Back along the route through the first door on the lesson side, plus one point beyond it.
            var exit = new List<Vector3>();
            for (int k = route.Count - 1; k >= 0; k--)
            {
                exit.Add(route[k]);
                if (k < route.Count - 1 && doorPoints.Contains(route[k]))
                {
                    if (k > 0) exit.Add(route[k - 1]);
                    break;
                }
            }
            if (exit.Count == 0) return;

            int i = 0;
            foreach (var t in leavers)
            {
                if (t == null) continue;
                t.SetParent(transform.parent, true);
                movers.Add(new Mover
                {
                    who = t,
                    path = new List<Vector3>(exit),
                    destroyAtEnd = true,
                    startAt = Time.time + settings.startDelay + i++ * settings.followDelay * 0.5f,
                });
            }
            Debug.Log($"[LessonWalk] {name}: {i} lesson extra(s) walk out.");
        }

        void Update()
        {
            if (movers.Count == 0) return;
            if (ServiceLocator.TryGet<GameStateMachine>(out var fsm) && fsm.Current != GameState.Break) return;

            float dt = Time.deltaTime;
            for (int m = movers.Count - 1; m >= 0; m--)
            {
                var mv = movers[m];
                if (mv.who == null) { movers.RemoveAt(m); continue; }
                if (Time.time < mv.startAt) continue;

                Vector3 target = mv.path[mv.next];
                Vector3 pos = mv.who.position;
                Vector3 to = target - pos;
                to.y = 0f;
                float step = settings.speed * dt;
                if (to.magnitude > step)
                {
                    mv.who.position = pos + to.normalized * step;
                    Face(mv.who, to, settings.turnSpeed * dt);
                    continue;
                }
                mv.who.position = new Vector3(target.x, pos.y, target.z);
                if (++mv.next < mv.path.Count) continue;
                movers.RemoveAt(m);
                Arrive(mv);
            }
        }

        void Arrive(Mover mv)
        {
            if (mv.destroyAtEnd) { Destroy(mv.who.gameObject); return; }
            if (mv.endRotation.HasValue) mv.who.rotation = mv.endRotation.Value;
            if (walked) return; // arrived at the lesson
            foreach (var b in pauseWhileWalking)
                if (b != null && b.transform.IsChildOf(mv.who)) b.enabled = true;
        }

        // Side by side at the last point, across the direction of the last leg.
        Vector3 StandSpot(int i)
        {
            Vector3 dir = LastDirection();
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            float offset = (i - (walkers.Length - 1) * 0.5f) * settings.standSpacing;
            return route[route.Count - 1] + side * offset;
        }

        Vector3 LastDirection()
        {
            if (route.Count < 2) return transform.forward;
            Vector3 d = route[route.Count - 1] - route[route.Count - 2];
            d.y = 0f;
            return d.sqrMagnitude > 0.001f ? d.normalized : transform.forward;
        }

        // ---------------------------------------------------------------- doors

        // Door and LockedDoor live in the school's own assembly, so they are reached by name, like SchoolSceneLoader does.
        // A LockedDoor is removed (the zone opens for the rest of the day); then, a frame later, the door is opened.
        IEnumerator OpenDoors()
        {
            var scene = SceneManager.GetSceneByName(schoolScene);
            if (!scene.isLoaded || doorPoints.Count == 0) yield break;

            var doors = new List<Component>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>())
                {
                    var door = t.GetComponent("Door");
                    if (door == null || !NearDoorPoint(t.position)) continue;
                    var lockComp = t.GetComponent("LockedDoor");
                    if (lockComp != null) Destroy(lockComp);
                    doors.Add(door);
                }
            yield return null;

            foreach (var door in doors)
            {
                var isOpen = door.GetType().GetProperty("IsOpen");
                if (isOpen != null && (bool)isOpen.GetValue(door)) continue;
                Vector3 from = route.Count > 0 ? route[0] : transform.position;
                door.SendMessage("Toggle", from, SendMessageOptions.DontRequireReceiver);
            }
            Debug.Log($"[LessonWalk] {doors.Count} door(s) on the way opened.");
        }

        bool NearDoorPoint(Vector3 p)
        {
            foreach (var d in doorPoints)
            {
                Vector3 delta = p - d;
                delta.y = 0f;
                if (delta.magnitude <= settings.doorRadius) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- helpers

        static void Face(Transform w, Vector3 dir, float maxDegrees)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            w.rotation = Quaternion.RotateTowards(w.rotation, Quaternion.LookRotation(dir.normalized, Vector3.up), maxDegrees);
        }

        static void Say(Transform who, string text)
        {
            if (who != null && !string.IsNullOrEmpty(text) && ServiceLocator.TryGet<IBarkService>(out var barks)) barks.Say(who.gameObject, text);
        }

        static string Pick(string[] lines) => lines != null && lines.Length > 0 ? lines[Random.Range(0, lines.Length)] : null;

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.9f);
            Vector3? prev = null;
            foreach (Transform p in transform)
            {
                Gizmos.DrawWireSphere(p.position + Vector3.up * 0.1f, p.name.StartsWith("Door") ? 0.5f : 0.25f);
                if (prev.HasValue) Gizmos.DrawLine(prev.Value + Vector3.up * 0.1f, p.position + Vector3.up * 0.1f);
                prev = p.position;
            }
        }
    }
}
