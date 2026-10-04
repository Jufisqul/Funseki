using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Editor-only: in Play Mode, feeds fake keys to the hero and logs what happened.
// Quick regression check for the controller and cameras without touching the keyboard.
public static class PlayerSmokeTest
{
    static double start;
    static int stage;
    static Vector3 startPos;
    static InputSettings.EditorInputBehaviorInPlayMode oldEditorBehavior;
    static InputSettings.BackgroundBehavior oldBackground;

    [MenuItem("Tools/One Funseki/Debug/Player Smoke Test")]
    static void Run()
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning("[Smoke] Enter Play Mode first."); return; }
        oldEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        oldBackground = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        startPos = Player().position;
        start = EditorApplication.timeSinceStartup;
        stage = 0;
        Press(Key.W, Key.LeftShift);
        EditorApplication.update += Tick;
    }

    static Transform Player() => GameObject.Find("Player").transform;

    static void Press(params Key[] keys) => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));

    static void Tick()
    {
        double t = EditorApplication.timeSinceStartup - start;
        var pc = Player().GetComponent<PlayerController>();
        var rig = Object.FindAnyObjectByType<PlayerCameraRig>();

        if (stage == 0 && t > 1.0)
        {
            Debug.Log($"[Smoke] Sprint 1s: moved {Vector3.Distance(startPos, Player().position):F2} m, speed01 {pc.Speed01:F2}, grounded {pc.IsGrounded}");
            Press(Key.Space);
            stage = 1;
        }
        else if (stage == 1 && t > 1.25)
        {
            Debug.Log($"[Smoke] Jump: height {Player().position.y:F2} m, vy {pc.Velocity.y:F2}");
            Press(Key.F);
            stage = 2;
        }
        else if (stage == 2 && t > 1.8)
        {
            var cam = Camera.main.transform;
            Debug.Log($"[Smoke] F held: firstPerson {rig.IsFirstPerson}, camera-eye distance {Vector3.Distance(cam.position, Player().position + Vector3.up * 1.55f):F2} m");
            Press();
            stage = 3;
        }
        else if (stage == 3 && t > 2.5)
        {
            var cam = Camera.main.transform;
            Debug.Log($"[Smoke] F released: firstPerson {rig.IsFirstPerson}, camera distance {Vector3.Distance(cam.position, Player().position):F2} m, grounded {pc.IsGrounded}");
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorBehavior;
            InputSystem.settings.backgroundBehavior = oldBackground;
            EditorApplication.update -= Tick;
        }
    }
}
