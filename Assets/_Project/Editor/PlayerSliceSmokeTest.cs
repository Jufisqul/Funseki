using System.Collections.Generic;
using System.Text;
using Funseki.Core;
using Funseki.Player;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace Funseki.EditorTools
{
    // Tools > Funseki > Slice > Smoke Test Movement: Play from Bootstrap, New Game, wait for the Break,
    // then a virtual gamepad walks the hero from the main entrance into the school, up the NE stairs to floor 2
    // and back down, sprints, jumps and holds first person. Every frame it checks the camera isn't behind
    // or inside a wall. Result goes to the Console as [PlayerSliceSmokeTest] PASS / FAIL.
    [InitializeOnLoad]
    public static class PlayerSliceSmokeTest
    {
        const string Key = "Funseki.PlayerSliceSmokeTest.Step";
        const float WaypointTimeout = 40f;
        const float ArriveRadius = 0.6f;

        // Goals of the route (world space). Between goals the hero follows a NavMesh path baked at test start.
        static readonly (string name, Vector3 p, bool sprint)[] Route =
        {
            ("main entrance", new Vector3(25.2f, 0f, -43.2f), false),
            ("inside: shoe corridor", new Vector3(25.2f, 0f, -39.5f), true),
            ("NE stairs, floor 2", new Vector3(51f, 3.5f, -8.6f), true),
            ("back down: north corridor", new Vector3(44f, 0f, -10f), true),
        };

        static int step, waypoint;
        static double stepStart, phaseStart;
        static Gamepad pad;
        static float maxHeight;
        static int cameraBehindWall, cameraInsideWall, frames;
        static bool jumped, firstPersonOk, sawFirstPersonCamera;
        static readonly StringBuilder log = new();

        static PlayerSliceSmokeTest() => EditorApplication.update += Tick;

        [MenuItem("Tools/Funseki/Slice/Smoke Test Movement")]
        public static void Run()
        {
            SessionState.SetInt(Key, 1);
            CoreScenesSetup.PlayFromBootstrap();
        }

        static void Tick()
        {
            int s = SessionState.GetInt(Key, 0);
            if (s == 0) return;
            if (!EditorApplication.isPlaying) { if (step != 0) Reset(); return; }
            if (step != s) { step = s; stepStart = EditorApplication.timeSinceStartup; }
            double t = EditorApplication.timeSinceStartup - stepStart;
            if (t > 30 && step < 3) { Finish("FAIL: timed out at step " + step); return; }

            ServiceLocator.TryGet<GameStateMachine>(out var fsm);
            var state = fsm?.Current ?? GameState.None;
            string scene = SceneManager.GetActiveScene().name;

            if (step == 1 && scene == "MainMenu" && state == GameState.MainMenu)
            {
                Object.FindAnyObjectByType<MainMenuController>()?.NewGame();
                SessionState.SetInt(Key, 2);
            }
            else if (step == 2 && scene == "Slice_Day1" && state == GameState.Break)
            {
                BakeNavMesh();
                corners.Clear();
                pad = InputSystem.AddDevice<Gamepad>("SmokeTestPad");
                waypoint = 0;
                phaseStart = EditorApplication.timeSinceStartup;
                maxHeight = 0; cameraBehindWall = cameraInsideWall = frames = 0;
                jumped = firstPersonOk = sawFirstPersonCamera = false;
                log.Clear();
                SessionState.SetInt(Key, 3);
            }
            else if (step == 3) Drive();
        }

        static void Drive()
        {
            // With three heroes the one under control; otherwise the only hero.
            var player = HeroService.CurrentObject != null ? HeroService.CurrentObject : GameObject.FindWithTag("Player");
            var motor = player != null ? player.GetComponent<PlayerMotor>() : null;
            var cam = Camera.main;
            if (motor == null || cam == null) { Finish("FAIL: no Funseki.Player hero or Main Camera in Slice_Day1"); return; }
            var rig = cam.GetComponent<PlayerCameraController>();
            var brain = cam.GetComponent<CinemachineBrain>();

            Vector3 pos = player.transform.position;
            maxHeight = Mathf.Max(maxHeight, pos.y);
            CheckCamera(player, cam, rig, brain);
            double now = EditorApplication.timeSinceStartup;

            if (waypoint >= Route.Length) { FirstPersonPhase(motor, rig, brain, now); return; }

            var (name, goal, sprint) = Route[waypoint];
            if (Planar(goal - pos).magnitude < ArriveRadius && Mathf.Abs(goal.y - pos.y) < 1f)
            {
                log.AppendLine($"  reached {name} at {pos:F1} in {now - phaseStart:F1}s");
                waypoint++;
                phaseStart = now;
                corners.Clear();
                if (waypoint == 1 && !jumped) { jumped = true; Press(Vector2.zero, false, jump: true); }
                return;
            }
            if (now - phaseStart > WaypointTimeout)
            {
                Finish($"FAIL: stuck before '{name}' at {pos:F2}\n{log}");
                return;
            }

            if (corners.Count == 0 && !PlanPath(pos, goal)) { Finish($"FAIL: no NavMesh path to '{name}' from {pos:F2}\n{Reachability(pos)}{log}"); return; }
            while (corners.Count > 1 && Planar(corners.Peek() - pos).magnitude < 0.35f) corners.Dequeue();
            Vector3 to = Planar(corners.Peek() - pos);

            // Camera-relative stick toward the next path corner.
            Vector3 fwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 dir = to.normalized;
            Press(new Vector2(Vector3.Dot(dir, right), Vector3.Dot(dir, fwd)), sprint && corners.Count > 1);
        }

        static readonly Queue<Vector3> corners = new();

        static bool PlanPath(Vector3 from, Vector3 goal)
        {
            if (!NavMesh.SamplePosition(from, out var a, 2f, NavMesh.AllAreas)) return false;
            if (!NavMesh.SamplePosition(goal, out var b, 2f, NavMesh.AllAreas)) return false;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            foreach (var c in path.corners) corners.Enqueue(c);
            corners.Enqueue(goal);
            return true;
        }

        // Which parts of floor 1 the NavMesh connects to the hero: tells a closed door from a wall.
        static string Reachability(Vector3 from)
        {
            var probes = new (string, Vector3)[]
            {
                ("shoe corridor west", new(3f, 0f, -39.5f)), ("west corridor", new(3f, 0f, -20f)),
                ("north corridor", new(19.6f, 0f, -10f)), ("east corridor", new(49f, 0f, -25f)),
                ("courtyard", new(26f, 0f, -21f)), ("heroes' classroom", new(19.6f, 0f, -6.6f)),
            };
            var sb = new StringBuilder("  reachable: ");
            foreach (var (n, p) in probes)
            {
                corners.Clear();
                sb.Append(n).Append(PlanPath(from, p) ? " yes, " : " NO, ");
            }
            corners.Clear();
            return sb.AppendLine().ToString();
        }

        static Vector3 Planar(Vector3 v) => new(v.x, 0f, v.z);

        static void BakeNavMesh()
        {
            var go = new GameObject("SmokeTestNavMesh");
            var surface = go.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.1f;
            surface.layerMask = LayerMask.GetMask("Environment");   // the hero capsule is on Default
            surface.BuildNavMesh();
        }

        // Hold first person for 1.5 s while looking right: the hero must stand still and the FP camera be live.
        static void FirstPersonPhase(PlayerMotor motor, PlayerCameraController rig, CinemachineBrain brain, double now)
        {
            double t = now - phaseStart;
            if (t < 0.3) { Press(Vector2.zero, false); return; }
            if (t < 1.8)
            {
                Press(Vector2.up, false, firstPerson: true, look: new Vector2(0.5f, 0f));
                if (t > 1.0)
                {
                    sawFirstPersonCamera |= brain.ActiveVirtualCamera is CinemachineCamera c && c.name == "CM FirstPerson" && !brain.IsBlending;
                    firstPersonOk = rig.IsFirstPerson && motor.PlanarSpeed < 0.2f;
                }
                return;
            }
            if (t < 2.6) { Press(Vector2.zero, false); return; }

            bool backToThird = !rig.IsFirstPerson && brain.ActiveVirtualCamera is CinemachineCamera tp && tp.name == "CM ThirdPerson";
            var sb = new StringBuilder();
            sb.AppendLine($"route {Route.Length} waypoints, max height {maxHeight:F2} m (floor 2 = 3.5)");
            sb.AppendLine($"camera: {cameraBehindWall} frames behind a wall, {cameraInsideWall} frames inside geometry, of {frames}");
            sb.AppendLine($"first person: hero still and FP camera live = {firstPersonOk && sawFirstPersonCamera}, back to third person = {backToThird}");
            sb.Append(log);
            bool pass = maxHeight > 3.2f && cameraBehindWall == 0 && cameraInsideWall == 0 && firstPersonOk && sawFirstPersonCamera && backToThird;
            Finish((pass ? "PASS\n" : "FAIL\n") + sb);
        }

        static void CheckCamera(GameObject player, Camera cam, PlayerCameraController rig, CinemachineBrain brain)
        {
            if (rig.IsFirstPerson || brain.IsBlending) return;
            frames++;
            var pivot = player.transform.Find("CameraPivot");
            int mask = LayerMask.GetMask("Default", "Environment");
            Vector3 c = cam.transform.position;
            // Small margins: the Deoccluder keeps the camera radius away, so real contacts are deeper than this.
            Vector3 from = pivot.position, dir = c - from;
            if (dir.magnitude > 0.15f && Physics.Raycast(from, dir.normalized, dir.magnitude - 0.05f, mask, QueryTriggerInteraction.Ignore))
                cameraBehindWall++;
            if (Physics.CheckSphere(c, 0.04f, mask, QueryTriggerInteraction.Ignore)) cameraInsideWall++;
        }

        static void Press(Vector2 stick, bool sprint, bool jump = false, bool firstPerson = false, Vector2 look = default)
        {
            var state = new GamepadState { leftStick = stick, rightStick = look };
            if (sprint) state = state.WithButton(GamepadButton.LeftStick);
            if (jump) state = state.WithButton(GamepadButton.South);
            if (firstPerson) state = state.WithButton(GamepadButton.LeftShoulder);
            InputSystem.QueueStateEvent(pad, state);
        }

        static void Finish(string result)
        {
            SessionState.EraseInt(Key);
            if (result.StartsWith("PASS")) Debug.Log("[PlayerSliceSmokeTest] " + result);
            else Debug.LogError("[PlayerSliceSmokeTest] " + result);
            Reset();
            EditorApplication.isPlaying = false;
        }

        static void Reset()
        {
            if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
            pad = null;
            step = 0;
        }
    }
}
