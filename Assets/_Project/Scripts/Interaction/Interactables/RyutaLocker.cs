using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Funseki.Interaction
{
    // Шкафчик Рюты (GDD 5.9): E swings the door open and the close-up camera looks at what's inside;
    // each InspectDetail shows its caption under the mouse (gamepad: A / D or the d-pad step through them).
    // The hero says «Ну и бардак». Esc / E closes. While open the game is in the Dialogue state (see InspectSession).
    public class RyutaLocker : InteractableObject
    {
        [SerializeField] InputActionAsset actions;
        [Tooltip("Door pivot (on the hinge); rotates around Y")]
        [SerializeField] Transform door;
        [SerializeField] CinemachineCamera closeUpCamera;
        [Tooltip("Hover ray length from the camera, m")]
        [SerializeField] float hoverDistance = 4f;

        LockerData D => (LockerData)data;
        InspectSession session;
        InspectOverlay overlay;
        InspectDetail[] details;
        Quaternion doorClosed;
        Coroutine doorRoutine;
        int padIndex = -1;

        void Awake()
        {
            session = new InspectSession(actions);
            details = GetComponentsInChildren<InspectDetail>(true);
            if (door != null) doorClosed = door.localRotation;
            if (closeUpCamera != null) closeUpCamera.gameObject.SetActive(false);
        }

        protected override bool CanUse(GameObject hero) => session.CanOpen;

        protected override void OnUsed(GameObject hero, bool first, GameObject witness)
        {
            session.Begin();
            overlay ??= new InspectOverlay(D);
            padIndex = -1;
            MoveDoor(D.doorOpenAngle);
            if (closeUpCamera != null)
            {
                closeUpCamera.Priority = D.cameraPriority;
                closeUpCamera.gameObject.SetActive(true);
            }

            // The hero is behind the close-up camera, so the line is also shown as a subtitle.
            var id = HeroOf(hero);
            string line = witness != null && data.teacherNearbyLines is { Length: > 0 }
                ? InteractableData.Pick(data.teacherNearbyLines)
                : InteractableData.Pick(first ? data.FirstLines(id) : data.RepeatLines(id));
            Say(hero, line);
            overlay.Show(line);
            Report(hero, "locker_opened");
        }

        void Update()
        {
            if (!session.Active) return;
            if (session.ClosePressed) { Close(); return; }
            UpdateHover();
        }

        void UpdateHover()
        {
            int step = session.Step;
            if (step != 0 && details.Length > 0)
                padIndex = padIndex < 0 ? 0 : (padIndex + step + details.Length) % details.Length;
            else if (session.PointerMoved)
                padIndex = -1;

            var cam = Camera.main;
            if (cam == null) return;

            InspectDetail target = null;
            Vector2 at = session.Pointer;
            if (padIndex >= 0)
            {
                target = details[padIndex];
                at = cam.WorldToScreenPoint(target.transform.position);
            }
            else if (Physics.Raycast(cam.ScreenPointToRay(at), out var hit, hoverDistance, ~0, QueryTriggerInteraction.Collide)
                     && hit.transform.IsChildOf(transform))
                target = hit.collider.GetComponentInParent<InspectDetail>();

            string caption = target != null && target.index >= 0 && target.index < D.captions.Length ? D.captions[target.index] : null;
            overlay.SetCaption(caption, at);
        }

        void Close()
        {
            session.End();
            overlay.Hide();
            MoveDoor(0f);
            if (closeUpCamera != null) closeUpCamera.gameObject.SetActive(false);
        }

        void MoveDoor(float angle)
        {
            if (door == null) return;
            if (doorRoutine != null) StopCoroutine(doorRoutine);
            // Negative yaw swings a door hinged on its left edge out toward the viewer.
            doorRoutine = StartCoroutine(SwingDoor(doorClosed * Quaternion.Euler(0f, -angle, 0f)));
        }

        IEnumerator SwingDoor(Quaternion to)
        {
            Quaternion from = door.localRotation;
            for (float t = 0f; t < D.doorTime; t += Time.unscaledDeltaTime)
            {
                door.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t / D.doorTime));
                yield return null;
            }
            door.localRotation = to;
            doorRoutine = null;
        }

        void OnDisable()
        {
            if (session == null || !session.Active) return;
            session.End();
            overlay.Hide();
            if (closeUpCamera != null) closeUpCamera.gameObject.SetActive(false);
            if (door != null) door.localRotation = doorClosed;
        }

        void OnDestroy() => overlay?.Destroy();
    }
}
