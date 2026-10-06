using UnityEngine;

namespace Funseki.Core
{
    // For test scenes played directly (without Bootstrap): registers GameStateMachine and WorldFlags
    // and enters startState. Does nothing when Bootstrap already created them.
    [DefaultExecutionOrder(-950)]
    public class StandaloneServices : MonoBehaviour
    {
        [SerializeField] GameState startState = GameState.Break;

        GameStateMachine fsm;
        WorldFlags flags;

        void Awake()
        {
            if (ServiceLocator.IsRegistered<GameStateMachine>()) { enabled = false; return; }
            fsm = new GameStateMachine();
            flags = new WorldFlags();
            ServiceLocator.Register(fsm);
            ServiceLocator.Register(flags);
        }

        // In Start, so every OnEnable subscriber already listens to the first state change.
        void Start()
        {
            if (fsm != null) fsm.ChangeState(startState);
        }

        void OnDestroy()
        {
            if (fsm == null) return;
            if (ServiceLocator.TryGet<GameStateMachine>(out var f) && f == fsm) ServiceLocator.Unregister<GameStateMachine>();
            if (ServiceLocator.TryGet<WorldFlags>(out var w) && w == flags) ServiceLocator.Unregister<WorldFlags>();
        }
    }
}
