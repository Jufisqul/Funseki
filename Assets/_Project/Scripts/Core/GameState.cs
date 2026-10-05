namespace Funseki.Core
{
    // Top-level game modes. Only GameStateMachine changes them; everyone else listens to GameEvents.OnGameStateChanged.
    public enum GameState
    {
        None,
        MainMenu,
        Cutscene,
        Break,      // перемена: free roam hub
        Lesson,     // урок: mini-game
        Dialogue,
        Paused,
        Caught,     // a teacher caught the hero mid-prank
        SliceEnd
    }
}
