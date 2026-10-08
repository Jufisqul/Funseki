using UnityEngine;

namespace Funseki.NPC
{
    // Shared tunables of NPCs. Assets/_Project/Data/NPC/NpcSettings.asset.
    [CreateAssetMenu(fileName = "NpcSettings", menuName = "Funseki/NPC/NPC Settings")]
    public class NpcSettings : ScriptableObject
    {
        [Header("Sight (teachers notice mischief)")]
        [Tooltip("m")]
        public float viewDistance = 15f;
        [Tooltip("Width of the view cone, degrees")]
        [Range(10f, 360f)] public float viewAngle = 120f;
        [Tooltip("Eye height above the NPC's origin, m")]
        public float eyeHeight = 1.6f;
        [Tooltip("Layers that block the view (school walls are on Environment)")]
        public LayerMask occluderLayers = ~0;

        [Header("Reactions")]
        [Tooltip("How long an NPC keeps looking at a prank done nearby, s")]
        public float prankLookTime = 10f;
        [Tooltip("deg/s")]
        public float turnSpeed = 240f;

        [Header("Animation (NpcLocomotion, NpcArmPointer)")]
        [Tooltip("Smoothing of the measured walking speed fed to the animator, s")]
        public float speedDamping = 0.15f;
        [Tooltip("Below this speed the NPC plays idle, m/s")]
        public float idleBelowSpeed = 0.1f;
        [Tooltip("How fast the arm swings to a pointing gesture and back, 1/s")]
        public float armPointSpeed = 10f;
    }
}
