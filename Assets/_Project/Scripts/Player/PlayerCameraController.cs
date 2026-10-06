using Funseki.Core;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;

namespace Funseki.Player
{
    // Drives the two Cinemachine cameras of GDD 4: a free orbit camera behind the hero that is pushed
    // forward by walls (Deoccluder), and an eye-level camera while F is held. The Brain blends between them
    // in PlayerSettings.firstPersonBlendTime. In first person the hero stands still and his body only casts shadows.
    // With three heroes (Funseki.Heroes) it lives on the camera and re-targets itself on GameEvents.OnHeroSwitched:
    // the orbit camera follows a point that glides from the old hero to the new one in the switch time.
    [DefaultExecutionOrder(-50)] // before the CinemachineBrain reads the follow point in LateUpdate
    public class PlayerCameraController : MonoBehaviour
    {
        [SerializeField] PlayerSettings settings;
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerMotor motor;
        [SerializeField] CinemachineBrain brain;
        [SerializeField] CinemachineCamera thirdPersonCamera;
        [SerializeField] CinemachineCamera firstPersonCamera;
        [Tooltip("Orbit pivot near the shoulders, child of the hero")]
        [SerializeField] Transform pivot;
        [Tooltip("Eye point, child of the hero")]
        [SerializeField] Transform eyes;
        [SerializeField] Renderer[] hideInFirstPerson;

        public const string PivotName = "CameraPivot";
        public const string EyesName = "Eyes";

        public bool IsFirstPerson { get; private set; }

        CinemachineOrbitalFollow orbit;
        CinemachineRotationComposer composer;
        CinemachineDeoccluder deoccluder;
        CinemachinePanTilt panTilt;
        Transform followPoint;
        Vector3 glideFrom;
        float glideStart, glideTime;

        void Awake()
        {
            // The third-person camera follows this point; it sits on the pivot except while gliding to a new hero.
            followPoint = new GameObject("CameraFollowPoint").transform;
            followPoint.position = pivot.position;
            thirdPersonCamera.Follow = followPoint;

            orbit = thirdPersonCamera.GetComponent<CinemachineOrbitalFollow>();
            composer = thirdPersonCamera.GetComponent<CinemachineRotationComposer>();
            deoccluder = thirdPersonCamera.GetComponent<CinemachineDeoccluder>();
            panTilt = firstPersonCamera.GetComponent<CinemachinePanTilt>();
            ApplySettings();
            orbit.HorizontalAxis.Value = motor.transform.eulerAngles.y;
            orbit.VerticalAxis.Value = settings.defaultPitch;
            firstPersonCamera.gameObject.SetActive(false);
        }

        void OnEnable() => GameEvents.OnHeroSwitched += OnHeroSwitched;

        void OnDisable()
        {
            GameEvents.OnHeroSwitched -= OnHeroSwitched;
            SetCursorLocked(false);
        }

        void LateUpdate()
        {
            ApplySettings();
            UpdateFollowPoint();
            SetCursorLocked(input.StateAllowsInput);

            if (input.FirstPersonHeld != IsFirstPerson) SetFirstPerson(input.FirstPersonHeld);

            Vector2 delta = input.LookDegrees;
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

        // Cheap enough to run every frame, so PlayerSettings can be tuned live in Play mode.
        void ApplySettings()
        {
            float h = motor.Height;
            pivot.localPosition = new Vector3(0f, h * settings.pivotHeight, 0f);
            eyes.localPosition = new Vector3(0f, h * settings.eyeHeight, 0.1f);

            orbit.Radius = settings.cameraDistance;
            orbit.VerticalAxis.Range = settings.pitchRange;
            orbit.VerticalAxis.Center = settings.defaultPitch;
            var tracker = orbit.TrackerSettings;
            tracker.PositionDamping = Vector3.one * settings.followDamping;
            orbit.TrackerSettings = tracker;
            if (composer != null) composer.Damping = Vector2.one * settings.followDamping;
            thirdPersonCamera.Lens.FieldOfView = settings.thirdPersonFov;

            deoccluder.CollideAgainst = settings.cameraCollisionLayers;
            deoccluder.MinimumDistanceFromTarget = settings.minCameraDistance;
            var avoid = deoccluder.AvoidObstacles;
            avoid.CameraRadius = settings.cameraRadius;
            deoccluder.AvoidObstacles = avoid;

            firstPersonCamera.Lens.FieldOfView = settings.firstPersonFov;
            panTilt.TiltAxis.Range = settings.firstPersonPitchRange;

            brain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.EaseInOut, settings.firstPersonBlendTime);
        }

        // Hand the view direction over so the switch feels like the same pair of eyes.
        void SetFirstPerson(bool on)
        {
            IsFirstPerson = on;
            Transform view = brain.transform;
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
                // Looking straight ahead in first person maps back to the default over-the-shoulder pitch.
                orbit.VerticalAxis.Value = orbit.VerticalAxis.ClampValue(pitch + settings.defaultPitch);
            }

            firstPersonCamera.gameObject.SetActive(on);
            motor.FirstPersonLock = on;
            foreach (var r in hideInFirstPerson)
                if (r != null) r.shadowCastingMode = on ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
        }

        void OnDestroy()
        {
            if (followPoint != null) Destroy(followPoint.gameObject);
        }

        void UpdateFollowPoint()
        {
            float t = glideTime > 0f ? Mathf.Clamp01((Time.time - glideStart) / glideTime) : 1f;
            followPoint.position = t >= 1f ? pivot.position : Vector3.Lerp(glideFrom, pivot.position, Mathf.SmoothStep(0f, 1f, t));
        }

        // Another hero took control: drive his motor and input, glide the camera over to him.
        void OnHeroSwitched(GameObject previous, GameObject next, float time)
        {
            if (next == null) return;
            var nextInput = next.GetComponent<PlayerInputReader>();
            var nextMotor = next.GetComponent<PlayerMotor>();
            var nextPivot = next.transform.Find(PivotName);
            var nextEyes = next.transform.Find(EyesName);
            if (nextInput == null || nextMotor == null || nextPivot == null || nextEyes == null)
            {
                Debug.LogWarning($"[PlayerCameraController] {next.name} has no PlayerInputReader / PlayerMotor / {PivotName} / {EyesName}.", next);
                return;
            }
            if (IsFirstPerson) SetFirstPerson(false);

            glideFrom = followPoint.position;
            glideStart = Time.time;
            glideTime = next == motor.gameObject ? 0f : time;
            input = nextInput;
            motor = nextMotor;
            pivot = nextPivot;
            eyes = nextEyes;
            firstPersonCamera.Follow = eyes;
            hideInFirstPerson = next.GetComponentsInChildren<Renderer>();
            if (glideTime <= 0f) UpdateFollowPoint();
        }

        static void SetCursorLocked(bool locked)
        {
            var mode = locked ? CursorLockMode.Locked : CursorLockMode.None;
            if (Cursor.lockState == mode) return;
            Cursor.lockState = mode;
            Cursor.visible = !locked;
        }
    }
}
