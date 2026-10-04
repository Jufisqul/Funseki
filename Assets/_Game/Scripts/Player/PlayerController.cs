using UnityEngine;
using UnityEngine.InputSystem;

// Third-person movement (GDD 4): WASD relative to the camera with diagonals, Shift sprint, Space jump.
// The hero turns toward where he moves; in first-person view he turns with the camera instead.
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public InputActionAsset actions;
    public Transform cameraTransform;

    [Header("Movement")]
    public float walkSpeed = 3.2f;
    public float sprintSpeed = 6f;
    public float acceleration = 14f;
    public float turnSpeed = 720f;

    [Header("Jump")]
    public float jumpHeight = 1.1f;
    public float gravity = -22f;
    public float coyoteTime = 0.12f;

    // Set by dialogs, lessons and cutscenes: the hero stands still but keeps falling.
    public bool InputLocked { get; set; }
    // Set by PlayerCameraRig while F is held.
    public bool FaceCamera { get; set; }

    public Vector3 Velocity => planarVelocity + Vector3.up * verticalVelocity;
    public float Speed01 => planarVelocity.magnitude / sprintSpeed;
    public bool IsGrounded => cc.isGrounded;
    public bool IsSprinting { get; private set; }

    CharacterController cc;
    InputActionMap map;
    InputAction move, sprint, jump;
    Vector3 planarVelocity;
    float verticalVelocity;
    float lastGroundedTime = -1f;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        map = actions.FindActionMap("Player", true);
        move = map.FindAction("Move", true);
        sprint = map.FindAction("Sprint", true);
        jump = map.FindAction("Jump", true);
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
    }

    void OnEnable() => map.Enable();
    void OnDisable() => map.Disable();

    void Update()
    {
        float dt = Time.deltaTime;
        Vector2 input = InputLocked ? Vector2.zero : Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);

        IsSprinting = !InputLocked && sprint.IsPressed() && input.sqrMagnitude > 0.01f;
        Vector3 wish = CameraRelative(input) * (IsSprinting ? sprintSpeed : walkSpeed);
        planarVelocity = Vector3.MoveTowards(planarVelocity, wish, acceleration * dt);

        if (cc.isGrounded)
        {
            lastGroundedTime = Time.time;
            if (verticalVelocity < 0f) verticalVelocity = -2f;
        }
        if (!InputLocked && jump.WasPressedThisFrame() && Time.time - lastGroundedTime <= coyoteTime)
        {
            verticalVelocity = Mathf.Sqrt(2f * jumpHeight * -gravity);
            lastGroundedTime = -1f;
        }
        verticalVelocity += gravity * dt;

        Turn(dt, wish);
        cc.Move((planarVelocity + Vector3.up * verticalVelocity) * dt);
    }

    Vector3 CameraRelative(Vector2 input)
    {
        if (cameraTransform == null) return new Vector3(input.x, 0f, input.y);
        Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        return forward * input.y + right * input.x;
    }

    void Turn(float dt, Vector3 wish)
    {
        Vector3 look = FaceCamera && cameraTransform != null
            ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up)
            : wish;
        if (look.sqrMagnitude < 0.0001f) return;
        Quaternion target = Quaternion.LookRotation(look.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * dt);
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
