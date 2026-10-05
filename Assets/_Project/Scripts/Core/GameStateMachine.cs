using UnityEngine;

namespace Funseki.Core
{
    // Owns the current GameState. Every change raises GameEvents.OnGameStateChanged.
    // Paused remembers the state it interrupted so Resume() returns to it.
    public class GameStateMachine
    {
        public GameState Current { get; private set; } = GameState.None;
        public GameState Previous { get; private set; } = GameState.None;

        GameState stateBeforePause = GameState.None;

        public bool IsPaused => Current == GameState.Paused;

        public void ChangeState(GameState next)
        {
            if (next == Current) return;
            if (next == GameState.Paused) stateBeforePause = Current;

            Previous = Current;
            Current = next;
            Debug.Log($"[GameState] {Previous} -> {Current}");
            GameEvents.RaiseGameStateChanged(Previous, Current);
        }

        public void Pause()
        {
            if (Current == GameState.Paused || Current == GameState.MainMenu || Current == GameState.None) return;
            ChangeState(GameState.Paused);
        }

        public void Resume()
        {
            if (Current != GameState.Paused) return;
            ChangeState(stateBeforePause);
        }
    }
}
