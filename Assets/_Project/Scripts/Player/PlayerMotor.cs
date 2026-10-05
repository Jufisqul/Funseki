using System;
using UnityEngine;

namespace Funseki.Player
{
    // Third-person movement (GDD 4) on a CharacterController: WASD relative to the camera with diagonals,
    // Shift sprint, Space jump, gravity, steps and stairs. The hero smoothly turns toward where he moves.
    // While the first-person view is held he stands still and turns with the camera instead.
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : MonoBehaviour
    {
        [SerializeField] PlayerSettings settings;
        [SerializeField] PlayerInputReader input;
        [Tooltip("Camera that defines 'forward' for WASD; Main Camera if empty")]
        [SerializeField] Transform cameraTransform;

        public event Action Jumped;

        // Set by PlayerCameraController while F is held.
        public bool FirstPersonLock { get; set; }

        public Vector3 Velocity => planarVelocity + Vector3.up * verticalVelocity;
        public float PlanarSpeed => planarVelocity.magnitude;
        public bool IsGrounded { get; private set; }
        public bool IsSprinting { get; private set; }
        public float Height => (cc != null ? cc : cc = GetComponent<CharacterController>()).height;

        CharacterController cc;
        Vector3 planarVelocity;
        float verticalVelocity;
        float lastGroundedTime = -10f, lastJumpPressedTime = -10f;
        float yawVelocity;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            cc.stepOffset = settings.stepOffset;
            cc.slopeLimit = settings.slopeLimit;

            Vector2 stick = FirstPersonLock ? Vector2.zero : input.Move;
            IsSprinting = !FirstPersonLock && input.Sprint && stick.sqrMagnitude > 0.01f;
            float speed = IsSprinting ? settings.sprintSpeed : settings.walkSpeed * stick.magnitude;
            Vector3 wish = CameraRelative(stick) * speed;

            float accel = wish.sqrMagnitude > planarVelocity.sqrMagnitude ? settings.acceleration : settings.deceleration;
            if (!IsGrounded) accel *= settings.airControl;
            planarVelocity = Vector3.MoveTowards(planarVelocity, wish, accel * dt);

            if (IsGrounded)
            {
                lastGroundedTime = Time.time;
                if (verticalVelocity < 0f) verticalVelocity = -2f;
            }

            bool jumped = false;
            if (!FirstPersonLock && input.JumpPressed) lastJumpPressedTime = Time.time;
            if (Time.time - lastJumpPressedTime <= settings.jumpBuffer && Time.time - lastGroundedTime <= settings.coyoteTime)
            {
                verticalVelocity = Mathf.Sqrt(2f * settings.jumpHeight * -settings.gravity);
                lastGroundedTime = lastJumpPressedTime = -10f;
                jumped = true;
                Jumped?.Invoke();
            }
            verticalVelocity += settings.gravity * dt;

            Turn(dt, wish);
            bool wasGrounded = IsGrounded;
            cc.Move((planarVelocity + Vector3.up * verticalVelocity) * dt);
            IsGrounded = cc.isGrounded;

            // Walking down stairs the controller leaves the ground on every step; pull it back down.
            if (wasGrounded && !IsGrounded && !jumped && verticalVelocity <= 0f) SnapToGround();
        }

        void SnapToGround()
        {
            Vector3 origin = transform.position + Vector3.up * (cc.radius + 0.05f);
            float maxDistance = settings.groundSnapDistance + 0.05f;
            if (!Physics.SphereCast(origin, cc.radius * 0.9f, Vector3.down, out var hit, maxDistance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
            if (Vector3.Angle(hit.normal, Vector3.up) > settings.slopeLimit) return;
            cc.Move(Vector3.down * hit.distance);
            IsGrounded = cc.isGrounded;
            if (IsGrounded) verticalVelocity = -2f;
        }

        Vector3 CameraRelative(Vector2 stick)
        {
            if (cameraTransform == null) return new Vector3(stick.x, 0f, stick.y);
            Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 dir = forward * stick.y + right * stick.x;
            return dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.zero;
        }

        void Turn(float dt, Vector3 wish)
        {
            Vector3 look = FirstPersonLock && cameraTransform != null
                ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up)
                : wish;
            if (look.sqrMagnitude < 1e-4f) return;
            float target = Quaternion.LookRotation(look.normalized, Vector3.up).eulerAngles.y;
            float smooth = FirstPersonLock ? 0.03f : settings.turnSmoothTime;
            float yaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, target, ref yawVelocity, smooth, Mathf.Infinity, dt);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        // Teleport without the CharacterController snapping back (spawn points, cutscenes, day changes).
        public void Warp(Vector3 position, Quaternion rotation)
        {
            cc.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            planarVelocity = Vector3.zero;
            verticalVelocity = 0f;
            cc.enabled = true;
        }
    }
}
