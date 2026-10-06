using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Funseki.School;

namespace Funseki.School.EditorTools
{
    // Editor-only, Play Mode: walks the player from the spawn to the courtyard's south double door,
    // presses E, then walks through. Logs whether the door opened and where the player ended up.
    public static class SchoolDoorSmokeTest
    {
        static Keyboard _fake;
        static double _t0;
        static int _stage;
        static InputSettings.EditorInputBehaviorInPlayMode _oldEditor;
        static InputSettings.BackgroundBehavior _oldBackground;
        static bool _oldRunInBackground;

        [MenuItem("Tools/Funseki/Debug/Door Smoke Test")]
        static void Run()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("[DoorSmoke] Enter Play Mode first."); return; }
            _oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            _oldBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            _oldRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            _fake = InputSystem.AddDevice<Keyboard>("DoorSmokeKeyboard");

            // Point the orbit camera along the player's facing so W walks straight ahead.
            var rig = Object.FindAnyObjectByType<PlayerCameraRig>();
            var orbit = rig.thirdPersonCamera.GetComponent<Unity.Cinemachine.CinemachineOrbitalFollow>();
            orbit.HorizontalAxis.Value = Mathf.DeltaAngle(0f, Player().eulerAngles.y);

            _t0 = EditorApplication.timeSinceStartup;
            _stage = 0;
            Debug.Log($"[DoorSmoke] Start at {Player().position}");
            Press(Key.W);
            EditorApplication.update += Tick;
        }

        static Transform Player() => GameObject.Find("Player").transform;

        static void Press(params Key[] keys) => InputSystem.QueueStateEvent(_fake, new KeyboardState(keys));

        static Door NearestDoor()
        {
            Door best = null;
            float bestD = float.MaxValue;
            foreach (var d in Object.FindObjectsByType<Door>(FindObjectsSortMode.None))
            {
                float dist = Vector3.Distance(d.transform.position, Player().position);
                if (dist < bestD) { bestD = dist; best = d; }
            }
            return best;
        }

        static void Tick()
        {
            double t = EditorApplication.timeSinceStartup - _t0;
            if (_stage == 0 && t > 2.6)
            {
                Press();
                var door = NearestDoor();
                Debug.Log($"[DoorSmoke] Stopped at {Player().position}; nearest door {door?.name} ({door?.kind}) open {door?.IsOpen}");
                _stage = 1;
            }
            else if (_stage == 1 && t > 2.9)
            {
                Press(Key.E);
                _stage = 2;
            }
            else if (_stage == 2 && t > 3.05)
            {
                Press();
                _stage = 3;
            }
            else if (_stage == 3 && t > 3.7)
            {
                var door = NearestDoor();
                Debug.Log($"[DoorSmoke] After E: door {door?.name} open {door?.IsOpen}, locked {door?.IsLocked}");
                Press(Key.W);
                _stage = 4;
            }
            else if (_stage == 4 && t > 6.3)
            {
                Press();
                var p = Player().position;
                Debug.Log($"[DoorSmoke] Walked on to {p}; inside courtyard (z > -30): {p.z > -30f}");
                InputSystem.RemoveDevice(_fake);
                InputSystem.settings.editorInputBehaviorInPlayMode = _oldEditor;
                InputSystem.settings.backgroundBehavior = _oldBackground;
                Application.runInBackground = _oldRunInBackground;
                EditorApplication.update -= Tick;
            }
        }
    }
}
