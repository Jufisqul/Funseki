using Funseki.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Funseki.Interaction
{
    // A close-up the hero opens with E (Рюта's locker, the booklet): holds the game in the Dialogue state
    // (hero input off, cursor free, the bell waits), reads the "Inspect" action map (Esc / E / B — close,
    // A / D / arrows / d-pad / shoulders — previous / next, pointer position) and returns the previous state on End.
    public class InspectSession
    {
        public const string MapName = "Inspect";

        readonly InputActionMap map;
        readonly InputAction close, next, previous, point;
        GameState before = GameState.None;
        int openedFrame = -1, closedFrame = -1;

        public bool Active { get; private set; }

        public InspectSession(InputActionAsset actions)
        {
            map = actions.FindActionMap(MapName, true);
            close = map.FindAction("Close", true);
            next = map.FindAction("Next", true);
            previous = map.FindAction("Previous", true);
            point = map.FindAction("Point", true);
        }

        /// <summary>False in the frame the view closed, so the same E press doesn't open it again.</summary>
        public bool CanOpen => !Active && Time.frameCount != closedFrame;

        /// <summary>Esc / E / B, ignoring the E press that opened the view.</summary>
        public bool ClosePressed => Active && Time.frameCount > openedFrame && close.WasPressedThisFrame();

        /// <summary>+1 next, -1 previous, 0 nothing this frame.</summary>
        public int Step => !Active ? 0 : next.WasPressedThisFrame() ? 1 : previous.WasPressedThisFrame() ? -1 : 0;

        public Vector2 Pointer => point.ReadValue<Vector2>();

        /// <summary>The pointer moved this frame (switches the hover back from the gamepad to the mouse).</summary>
        public bool PointerMoved => Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 0.01f;

        public void Begin()
        {
            if (Active) return;
            Active = true;
            openedFrame = Time.frameCount;
            map.Enable();
            if (ServiceLocator.TryGet<GameStateMachine>(out var fsm))
            {
                before = fsm.Current;
                fsm.ChangeState(GameState.Dialogue);
            }
        }

        public void End()
        {
            if (!Active) return;
            Active = false;
            closedFrame = Time.frameCount;
            map.Disable();
            if (ServiceLocator.TryGet<GameStateMachine>(out var fsm) && fsm.Current == GameState.Dialogue)
                fsm.ChangeState(before == GameState.None || before == GameState.Dialogue ? GameState.Break : before);
        }
    }
}
