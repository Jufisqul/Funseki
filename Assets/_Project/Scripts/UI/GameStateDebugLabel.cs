using Funseki.Core;
using UnityEngine;

namespace Funseki.UI
{
    // Dev-only overlay in the corner: current GameState. Lives on the Bootstrap object.
    public class GameStateDebugLabel : MonoBehaviour
    {
        [SerializeField] bool show = true;

        GameState state = GameState.None;
        GUIStyle style;

        void OnEnable() => GameEvents.OnGameStateChanged += Handle;
        void OnDisable() => GameEvents.OnGameStateChanged -= Handle;

        void Handle(GameState previous, GameState current) => state = current;

        void OnGUI()
        {
            if (!show || !Debug.isDebugBuild) return;
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 14 };
            GUI.Label(new Rect(10, Screen.height - 28, 400, 24), $"GameState: {state}", style);
        }
    }
}
