using Funseki.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Funseki.EditorTools
{
    // Tools > Funseki > Core > Smoke Test Flow: Play from Bootstrap, expect MainMenu,
    // press "New Game" on the menu, expect Slice_Day1 in GameState.Cutscene, then stop.
    // Result goes to the Console as [CoreFlowSmokeTest] PASS / FAIL.
    [InitializeOnLoad]
    public static class CoreFlowSmokeTest
    {
        const string Key = "Funseki.CoreFlowSmokeTest.Step";
        const float Timeout = 20f;
        static double stepStart;

        static CoreFlowSmokeTest() => EditorApplication.update += Tick;

        [MenuItem("Tools/Funseki/Core/Smoke Test Flow")]
        public static void Run()
        {
            SessionState.SetInt(Key, 1);
            CoreScenesSetup.PlayFromBootstrap();
        }

        static void Tick()
        {
            int step = SessionState.GetInt(Key, 0);
            if (step == 0 || !EditorApplication.isPlaying) return;
            if (stepStart == 0) stepStart = EditorApplication.timeSinceStartup;
            if (EditorApplication.timeSinceStartup - stepStart > Timeout) { Finish($"FAIL: timed out at step {step}"); return; }

            var scene = SceneManager.GetActiveScene().name;
            ServiceLocator.TryGet<GameStateMachine>(out var fsm);
            var state = fsm?.Current ?? GameState.None;

            if (step == 1 && scene == "MainMenu" && state == GameState.MainMenu)
            {
                var menu = GameObject.FindFirstObjectByType<MainMenuController>();
                if (menu == null) { Finish("FAIL: MainMenuController not found"); return; }
                menu.NewGame();
                Next(2);
            }
            else if (step == 2 && scene == "Slice_Day1" && state == GameState.Cutscene)
            {
                Finish("PASS: Bootstrap -> MainMenu -> Slice_Day1, state Cutscene");
            }
        }

        static void Next(int step)
        {
            SessionState.SetInt(Key, step);
            stepStart = EditorApplication.timeSinceStartup;
        }

        static void Finish(string result)
        {
            SessionState.EraseInt(Key);
            stepStart = 0;
            if (result.StartsWith("PASS")) Debug.Log("[CoreFlowSmokeTest] " + result);
            else Debug.LogError("[CoreFlowSmokeTest] " + result);
            EditorApplication.isPlaying = false;
        }
    }
}
