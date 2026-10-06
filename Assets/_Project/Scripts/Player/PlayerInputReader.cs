using Funseki.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Funseki.Player
{
    // Reads the "Gameplay" action map for the hero. While the game is in a state listed in
    // PlayerSettings.inputBlockedStates (cutscene, dialogue, pause, lesson), or this hero is not the one
    // under control (HeroService: a follower, or the camera hand-over after a switch), every value reads as idle.
    // The map itself stays enabled so other modules can still use Pause, Interact, etc.
    public class PlayerInputReader : MonoBehaviour
    {
        public const string MapName = "Gameplay";

        [SerializeField] InputActionAsset actions;
        [SerializeField] PlayerSettings settings;

        public bool InputEnabled => stateAllows && HeroService.IsInControl(gameObject);
        /// <summary>The game state lets the player act (ignores which hero is in control); the camera locks the cursor by it.</summary>
        public bool StateAllowsInput => stateAllows;

        bool stateAllows = true;

        public Vector2 Move => InputEnabled ? Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f) : Vector2.zero;
        public bool Sprint => InputEnabled && sprint.IsPressed();
        public bool JumpPressed => InputEnabled && jump.WasPressedThisFrame();
        public bool FirstPersonHeld => InputEnabled && firstPerson.IsPressed();

        // Look delta in degrees for this frame (yaw, pitch), sensitivity and invert applied.
        public Vector2 LookDegrees
        {
            get
            {
                if (!InputEnabled) return Vector2.zero;
                Vector2 raw = look.ReadValue<Vector2>();
                bool gamepad = look.activeControl != null && look.activeControl.device is Gamepad;
                Vector2 deg = gamepad ? raw * (settings.gamepadSensitivity * Time.deltaTime) : raw * (settings.mouseSensitivity * PlayerOptions.MouseSensitivity);
                if (settings.invertY) deg.y = -deg.y;
                return deg;
            }
        }

        InputActionMap map;
        InputAction move, look, sprint, jump, firstPerson;

        void Awake()
        {
            map = actions.FindActionMap(MapName, true);
            move = map.FindAction("Move", true);
            look = map.FindAction("Look", true);
            sprint = map.FindAction("Sprint", true);
            jump = map.FindAction("Jump", true);
            firstPerson = map.FindAction("FirstPerson", true);
        }

        void OnEnable()
        {
            map.Enable();
            GameEvents.OnGameStateChanged += OnGameStateChanged;
            // Without Bootstrap (Play straight from the scene) there is no state machine: input stays on.
            if (ServiceLocator.TryGet<GameStateMachine>(out var fsm)) Apply(fsm.Current);
        }

        void OnDisable()
        {
            GameEvents.OnGameStateChanged -= OnGameStateChanged;
            map.Disable();
        }

        void OnGameStateChanged(GameState previous, GameState current) => Apply(current);

        void Apply(GameState state) => stateAllows = !settings.IsInputBlocked(state);
    }
}
