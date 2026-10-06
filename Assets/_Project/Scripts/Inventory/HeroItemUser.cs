using Funseki.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Funseki.Inventory
{
    // On the hero. Mouse wheel / d-pad scrolls the shared inventory (wraps both ways),
    // LMB / RMB (gamepad RT / LT) use the selected item on the current interaction target:
    // IItemTarget.UseItem(item, primary), or the "won't work" line when it doesn't take the item.
    public class HeroItemUser : MonoBehaviour
    {
        public const string MapName = "Gameplay";

        [SerializeField] InputActionAsset actions;

        InputActionMap map;
        InputAction cycle, use, altUse;
        GameObject target;
        bool active = true;
        bool stickHeld;

        void Awake()
        {
            map = actions.FindActionMap(MapName, true);
            cycle = map.FindAction("CycleItem", true);
            use = map.FindAction("UseItem", true);
            altUse = map.FindAction("AltUseItem", true);
        }

        void OnEnable()
        {
            map.Enable();
            GameEvents.OnGameStateChanged += OnGameStateChanged;
            GameEvents.OnInteractionTargetChanged += OnTargetChanged;
            if (ServiceLocator.TryGet<GameStateMachine>(out var fsm) && ServiceLocator.TryGet<Inventory>(out var inv))
                active = inv.Settings.IsActive(fsm.Current);
        }

        void OnDisable()
        {
            GameEvents.OnGameStateChanged -= OnGameStateChanged;
            GameEvents.OnInteractionTargetChanged -= OnTargetChanged;
        }

        void OnGameStateChanged(GameState previous, GameState current)
        {
            if (ServiceLocator.TryGet<Inventory>(out var inv)) active = inv.Settings.IsActive(current);
        }

        void OnTargetChanged(GameObject hero, GameObject newTarget)
        {
            if (hero == gameObject) target = newTarget;
        }

        void Update()
        {
            if (!active || !HeroService.IsInControl(gameObject) || !ServiceLocator.TryGet<Inventory>(out var inv)) return;

            int step = ReadCycleStep();
            if (step != 0) inv.Cycle(step);

            if (use.WasPressedThisFrame()) Use(inv, true);
            else if (altUse.WasPressedThisFrame()) Use(inv, false);
        }

        // Wheel: every scrolled frame is one step, down = next. D-pad / Tab: one step per press, right = next.
        int ReadCycleStep()
        {
            float v = cycle.ReadValue<float>();
            if (cycle.activeControl != null && cycle.activeControl.device is Mouse)
            {
                stickHeld = false;
                return Mathf.Abs(v) > 0.01f ? (v < 0f ? 1 : -1) : 0;
            }
            bool held = Mathf.Abs(v) > 0.5f;
            int step = held && !stickHeld ? (v > 0f ? 1 : -1) : 0;
            stickHeld = held;
            return step;
        }

        void Use(Inventory inv, bool primary)
        {
            var item = inv.Selected;
            if (item == null || target == null || !target.activeInHierarchy) return;

            var itemTarget = target.GetComponent<IItemTarget>();
            bool worked = itemTarget != null && itemTarget.UseItem(item, primary);
            if (!worked) GameEvents.RaiseHeroBark(gameObject, inv.Settings.wontWorkLine);
            GameEvents.RaiseItemUsed(gameObject, item, target, primary, worked);
        }
    }
}
