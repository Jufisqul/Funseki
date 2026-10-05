using Funseki.Core;
using UnityEngine;

namespace Funseki.Player
{
    // Every tunable number of hero movement and camera (GDD 4). Edited by the designer in
    // Assets/_Project/Data/PlayerSettings.asset; most values are read every frame, so they can be tuned in Play.
    [CreateAssetMenu(fileName = "PlayerSettings", menuName = "Funseki/Player/Player Settings")]
    public class PlayerSettings : ScriptableObject
    {
        [Header("Movement, m/s")]
        public float walkSpeed = 1.8f;
        public float sprintSpeed = 4.5f;
        [Tooltip("How fast the hero reaches the target speed, m/s²")]
        public float acceleration = 14f;
        [Tooltip("How fast the hero stops when the stick is released, m/s²")]
        public float deceleration = 18f;
        [Tooltip("Share of acceleration available in the air (0..1)")]
        [Range(0f, 1f)] public float airControl = 0.4f;
        [Tooltip("Time to turn toward the movement direction, s")]
        public float turnSmoothTime = 0.1f;

        [Header("Jump and gravity")]
        public float jumpHeight = 0.6f;
        [Tooltip("m/s², negative")]
        public float gravity = -20f;
        [Tooltip("Jump still works this long after walking off an edge, s")]
        public float coyoteTime = 0.12f;
        [Tooltip("A jump pressed this long before landing still fires, s")]
        public float jumpBuffer = 0.1f;

        [Header("Steps and stairs")]
        [Tooltip("Highest step the hero walks up without jumping, m")]
        public float stepOffset = 0.35f;
        [Tooltip("Steepest walkable slope, degrees")]
        public float slopeLimit = 50f;
        [Tooltip("When walking down stairs the hero is pulled to the ground if it is closer than this, m")]
        public float groundSnapDistance = 0.45f;

        [Header("Look sensitivity")]
        [Tooltip("Degrees per pixel of mouse movement")]
        public float mouseSensitivity = 0.12f;
        [Tooltip("Degrees per second at full right-stick tilt")]
        public float gamepadSensitivity = 180f;
        public bool invertY;

        [Header("Third-person camera")]
        [Tooltip("Distance from the camera to the hero's shoulders, m")]
        public float cameraDistance = 3.5f;
        [Tooltip("Height of the camera pivot as a share of the hero's height")]
        [Range(0.5f, 1.1f)] public float pivotHeight = 0.9f;
        public float thirdPersonFov = 55f;
        [Tooltip("Lowest and highest camera pitch, degrees")]
        public Vector2 pitchRange = new(-20f, 65f);
        [Tooltip("Pitch the camera starts at, degrees")]
        public float defaultPitch = 12f;
        [Tooltip("Camera follow lag, s")]
        public float followDamping = 0.1f;

        [Header("Camera collision")]
        [Tooltip("Layers the camera can't pass through (school walls are on Environment)")]
        public LayerMask cameraCollisionLayers = 1;
        [Tooltip("The camera keeps this far from walls, m")]
        public float cameraRadius = 0.25f;
        [Tooltip("Closest the camera may come to the hero when pushed by a wall, m")]
        public float minCameraDistance = 0.4f;

        [Header("First-person view (hold F)")]
        [Tooltip("Eye height as a share of the hero's height")]
        [Range(0.5f, 1f)] public float eyeHeight = 0.93f;
        public float firstPersonFov = 65f;
        [Tooltip("Lowest and highest look pitch, degrees")]
        public Vector2 firstPersonPitchRange = new(-70f, 70f);
        [Tooltip("Blend between third- and first-person cameras, s")]
        public float firstPersonBlendTime = 0.3f;

        [Header("Animation")]
        [Tooltip("Smoothing of the Speed parameter, s")]
        public float animatorSpeedDamping = 0.1f;
        [Tooltip("Airtime shorter than this doesn't count as leaving the ground (steps, slopes), s")]
        public float airborneDelay = 0.15f;

        [Header("Input")]
        [Tooltip("Game states where the hero ignores movement, look and jump")]
        public GameState[] inputBlockedStates = { GameState.Cutscene, GameState.Dialogue, GameState.Paused, GameState.Lesson };

        public bool IsInputBlocked(GameState state) => System.Array.IndexOf(inputBlockedStates, state) >= 0;
    }
}
