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
    static Keyboard fake;
    static bool oldRunInBackground;

    [MenuItem("Tools/One Funseki/Debug/Player Smoke Test")]
    static void Run()
    {
        if (!BeginFakeInput()) return;
        startPos = Player().position;
        start = EditorApplication.timeSinceStartup;
        stage = 0;
        Press(Key.W, Key.LeftShift);
        EditorApplication.update += Tick;
    }

    // Walks the hero straight ahead for a few seconds from wherever he stands (turn him first in the
    // Inspector). Logs where he ends up: handy for checking doors, stairs and slopes in grey boxes.
    [MenuItem("Tools/One Funseki/Debug/Walk Forward 6s")]
    static void WalkForward()
    {
        if (!BeginFakeInput()) return;
        var player = Player();
        var orbit = Object.FindAnyObjectByType<PlayerCameraRig>().thirdPersonCamera.GetComponent<Unity.Cinemachine.CinemachineOrbitalFollow>();
        orbit.HorizontalAxis.Value = Mathf.DeltaAngle(0f, player.eulerAngles.y);
        var from = player.position;
        double t0 = EditorApplication.timeSinceStartup;
        float maxY = from.y;
        Press(Key.W);
        void Walk()
        {
            maxY = Mathf.Max(maxY, Player().position.y);
            if (EditorApplication.timeSinceStartup - t0 < 6.0) return;
            Debug.Log($"[Smoke] Walk: from {from} to {Player().position}, travelled {Vector3.Distance(from, Player().position):F1} m, highest y {maxY:F2}");
            EndFakeInput();
            EditorApplication.update -= Walk;
        }
        EditorApplication.update += Walk;
    }

    static bool BeginFakeInput()
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning("[Smoke] Enter Play Mode first."); return false; }
        oldEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        oldBackground = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        // Keep the game ticking while the editor window is not focused (e.g. when driven by tools).
        oldRunInBackground = Application.runInBackground;
        Application.runInBackground = true;
        // A virtual keyboard: the real one gets reset whenever the editor loses focus.
        fake = InputSystem.AddDevice<Keyboard>("SmokeTestKeyboard");
        return true;
    }

    static void EndFakeInput()
    {
        Press();
        InputSystem.RemoveDevice(fake);
        InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorBehavior;
        InputSystem.settings.backgroundBehavior = oldBackground;
        Application.runInBackground = oldRunInBackground;
    }

    static Transform Player() => GameObject.Find("Player").transform;

    static void Press(params Key[] keys) => InputSystem.QueueStateEvent(fake, new KeyboardState(keys));

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
            var anim = Player().GetComponentInChildren<Animator>();
            Debug.Log($"[Smoke] Jump: height {Player().position.y:F2} m, vy {pc.Velocity.y:F2}, airborne anim {anim.GetCurrentAnimatorStateInfo(0).IsName("Airborne") || anim.GetNextAnimatorStateInfo(0).IsName("Airborne")}");
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
            EndFakeInput();
            EditorApplication.update -= Tick;
        }
    }
}
