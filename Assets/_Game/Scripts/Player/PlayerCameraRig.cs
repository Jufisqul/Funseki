using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

// Feeds the Look action into two Cinemachine cameras (GDD 4):
// an orbit camera behind the hero, and an eye-level camera while F is held to inspect details.
// The Brain blends between them; the hero's body is hidden in first person.
public class PlayerCameraRig : MonoBehaviour
{
    public InputActionAsset actions;
    public PlayerController player;
    public CinemachineCamera thirdPersonCamera;
    public CinemachineCamera firstPersonCamera;
    public Renderer[] hideInFirstPerson;

    [Header("Sensitivity")]
    public float mouseDegreesPerPixel = 0.12f;
    public float gamepadDegreesPerSecond = 180f;
    public bool invertY;
    public bool lockCursor = true;

    public bool IsFirstPerson { get; private set; }

    CinemachineOrbitalFollow orbit;
    CinemachinePanTilt panTilt;
    InputAction look, firstPerson;

    void Awake()
    {
        var map = actions.FindActionMap("Player", true);
        look = map.FindAction("Look", true);
        firstPerson = map.FindAction("FirstPerson", true);
        orbit = thirdPersonCamera.GetComponent<CinemachineOrbitalFollow>();
        panTilt = firstPersonCamera.GetComponent<CinemachinePanTilt>();
        firstPersonCamera.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        if (lockCursor) Cursor.lockState = CursorLockMode.Locked;
    }

    void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
    }

    void Update()
    {
        bool wantFirstPerson = firstPerson.IsPressed() && !player.InputLocked;
        if (wantFirstPerson != IsFirstPerson) SetFirstPerson(wantFirstPerson);

        Vector2 delta = player.InputLocked ? Vector2.zero : ReadLook();
        if (IsFirstPerson)
        {
            panTilt.PanAxis.Value = panTilt.PanAxis.ClampValue(panTilt.PanAxis.Value + delta.x);
            panTilt.TiltAxis.Value = panTilt.TiltAxis.ClampValue(panTilt.TiltAxis.Value - delta.y);
        }
        else
        {
            orbit.HorizontalAxis.Value = orbit.HorizontalAxis.ClampValue(orbit.HorizontalAxis.Value + delta.x);
            orbit.VerticalAxis.Value = orbit.VerticalAxis.ClampValue(orbit.VerticalAxis.Value - delta.y);
        }
    }

    Vector2 ReadLook()
    {
        Vector2 raw = look.ReadValue<Vector2>();
        bool gamepad = look.activeControl != null && look.activeControl.device is Gamepad;
        Vector2 degrees = gamepad ? raw * gamepadDegreesPerSecond * Time.deltaTime : raw * mouseDegreesPerPixel;
        if (invertY) degrees.y = -degrees.y;
        return degrees;
    }

    // Hand the view direction over so the switch feels like the same pair of eyes.
    void SetFirstPerson(bool on)
    {
        IsFirstPerson = on;
        Transform view = player.cameraTransform;
        float yaw = view.eulerAngles.y;
        float pitch = Mathf.DeltaAngle(0f, view.eulerAngles.x);

        if (on)
        {
            panTilt.PanAxis.Value = panTilt.PanAxis.ClampValue(yaw);
            panTilt.TiltAxis.Value = panTilt.TiltAxis.ClampValue(pitch);
        }
        else
        {
            orbit.HorizontalAxis.Value = orbit.HorizontalAxis.ClampValue(yaw);
            // Looking straight ahead in first person maps back to the default over-the-shoulder height.
            orbit.VerticalAxis.Value = orbit.VerticalAxis.ClampValue(pitch + orbit.VerticalAxis.Center);
        }

        firstPersonCamera.gameObject.SetActive(on);
        player.FaceCamera = on;
        foreach (var r in hideInFirstPerson)
            if (r != null) r.shadowCastingMode = on
                ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly
                : UnityEngine.Rendering.ShadowCastingMode.On;
    }
}
