using UnityEngine;
using UnityEngine.InputSystem;

namespace Funseki.School
{
    // Lets the player open and close doors with the Interact action (E / gamepad X).
    // Picks the closest door in front of the character and shows a small prompt.
    public class PlayerInteractor : MonoBehaviour
    {
        public InputActionAsset actions;
        [Tooltip("Action map with the Interact action: Player in the old GameControls, Gameplay in GameInput")]
        public string actionMap = "Player";
        public float reach = 2.2f;
        public float lockedMessageTime = 1.5f;

        InputAction _interact;
        Door _door;
        LockedDoor _passage;
        float _lockedUntil;
        readonly Collider[] _hits = new Collider[32];

        void Awake()
        {
            _interact = actions.FindActionMap(actionMap, true).FindAction("Interact", true);
        }

        void Update()
        {
            FindTarget();
            if (!_interact.WasPressedThisFrame()) return;

            if (_door != null)
            {
                if (!_door.Toggle(transform.position)) _lockedUntil = Time.time + lockedMessageTime;
            }
            else if (_passage != null && _passage.IsLocked)
                _lockedUntil = Time.time + lockedMessageTime;
        }

        void FindTarget()
        {
            _door = null;
            _passage = null;
            Vector3 center = transform.position + Vector3.up * 1f + transform.forward * (reach * 0.5f);
            int n = Physics.OverlapSphereNonAlloc(center, reach * 0.6f, _hits, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue, bestPassage = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var col = _hits[i];
                float d = (col.ClosestPoint(transform.position + Vector3.up) - transform.position).sqrMagnitude;
                var door = col.GetComponentInParent<Door>();
                if (door != null)
                {
                    if (d < best) { best = d; _door = door; }
                    continue;
                }
                var passage = col.GetComponent<LockedDoor>();
                if (passage != null && d < bestPassage) { bestPassage = d; _passage = passage; }
            }
        }

        void OnGUI()
        {
            string text = null;
            if (Time.time < _lockedUntil) text = "Заперто";
            else if (_door != null) text = _door.IsOpen ? "[E] Закрыть" : "[E] Открыть";
            else if (_passage != null && _passage.IsLocked) text = "Заперто";
            if (text == null) return;

            var style = new GUIStyle(GUI.skin.box) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = Color.white;
            var size = style.CalcSize(new GUIContent(text)) + new Vector2(24f, 12f);
            GUI.Box(new Rect((Screen.width - size.x) * 0.5f, Screen.height * 0.72f, size.x, size.y), text, style);
        }
    }
}
