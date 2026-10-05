using System;
using UnityEngine;

namespace Funseki.Core
{
    // Which scenes make up the game and which GameState each one starts in. Edited by the designer.
    [CreateAssetMenu(fileName = "GameFlowConfig", menuName = "Funseki/Core/Game Flow Config")]
    public class GameFlowConfig : ScriptableObject
    {
        [Serializable]
        public class SceneState
        {
            [Tooltip("Scene name as in Build Settings")]
            public string scene;
            public GameState startState;
        }

        [Tooltip("Scene Bootstrap opens after the services are up")]
        public string firstScene = "MainMenu";

        [Tooltip("GameState set when each scene finishes loading")]
        public SceneState[] sceneStates =
        {
            new() { scene = "MainMenu", startState = GameState.MainMenu },
            new() { scene = "Slice_Day1", startState = GameState.Cutscene },
        };

        public bool TryGetStartState(string sceneName, out GameState state)
        {
            foreach (var s in sceneStates)
                if (s.scene == sceneName) { state = s.startState; return true; }
            state = GameState.None;
            return false;
        }
    }
}
