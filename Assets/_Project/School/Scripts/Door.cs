using Funseki.Core;
using UnityEngine;

namespace Funseki.School
{
    // An interactive door of the greybox kit. Swing and double doors turn on their hinges away
    // from whoever opens them; sliding doors slide sideways along the wall.
    // A LockedDoor on the same object keeps it shut until its day.
    // The hero opens it with E through Funseki.Interaction (IInteractable); a locked door makes the hero say so.
    public class Door : MonoBehaviour, IInteractable
    {
        public enum Kind { Swing, Sliding, Double }

        public Kind kind;
        public float openAngle = 95f;
        public float slideDistance = 1.25f;
        public float duration = 0.35f;

        [Header("Texts")]
        public string openPrompt = "Открыть";
        public string closePrompt = "Закрыть";
        public string lockedLine = "Заперто";

        [Header("Double door leaves (found by name if empty)")]
        public Transform leftLeaf;
        public Transform rightLeaf;

        public bool IsOpen { get; private set; }
        public LockedDoor Lock => _lock != null ? _lock : (_lock = GetComponent<LockedDoor>());
        public bool IsLocked => Lock != null && Lock.IsLocked;

        LockedDoor _lock;
        Quaternion _closedRot, _closedRotL, _closedRotR;
        Vector3 _closedPos;
        float _t, _sign = 1f;
        bool _init;

        void Init()
        {
            if (_init) return;
            _init = true;
            _closedRot = transform.localRotation;
            _closedPos = transform.localPosition;
            if (kind == Kind.Double)
            {
                if (leftLeaf == null) leftLeaf = transform.Find("Leaf_L");
                if (rightLeaf == null) rightLeaf = transform.Find("Leaf_R");
                if (leftLeaf != null) _closedRotL = leftLeaf.localRotation;
                if (rightLeaf != null) _closedRotR = rightLeaf.localRotation;
            }
        }

        // ---- IInteractable
        public string Prompt => IsOpen ? closePrompt : openPrompt;
        public bool CanInteract(GameObject hero) => true;

        public void Interact(GameObject hero)
        {
            if (!Toggle(hero.transform.position)) GameEvents.RaiseHeroBark(hero, lockedLine);
        }

        // Returns false if the door is locked.
        public bool Toggle(Vector3 from)
        {
            Init();
            if (IsLocked) return false;
            if (!IsOpen)
            {
                // Open away from the player: the leaf swings to the side the player is not on.
                float side = transform.InverseTransformPoint(from).z;
                _sign = side >= 0f ? 1f : -1f;
            }
            IsOpen = !IsOpen;
            enabled = true;
            return true;
        }

        void Update()
        {
            if (!_init) { enabled = false; return; }
            float target = IsOpen ? 1f : 0f;
            _t = Mathf.MoveTowards(_t, target, Time.deltaTime / Mathf.Max(0.01f, duration));
            float k = Mathf.SmoothStep(0f, 1f, _t);

            switch (kind)
            {
                case Kind.Swing:
                    transform.localRotation = _closedRot * Quaternion.Euler(0f, _sign * openAngle * k, 0f);
                    break;
                case Kind.Sliding:
                    transform.localPosition = _closedPos + _closedRot * Vector3.left * (slideDistance * k);
                    break;
                case Kind.Double:
                    if (leftLeaf != null) leftLeaf.localRotation = _closedRotL * Quaternion.Euler(0f, _sign * openAngle * k, 0f);
                    // The right leaf is mirrored (turned 180°), so the same swing side needs the opposite angle.
                    if (rightLeaf != null) rightLeaf.localRotation = _closedRotR * Quaternion.Euler(0f, -_sign * openAngle * k, 0f);
                    break;
            }
            if (Mathf.Approximately(_t, target)) enabled = false;
        }
    }
}
