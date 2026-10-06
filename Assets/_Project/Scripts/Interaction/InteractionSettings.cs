using Funseki.Core;
using TMPro;
using UnityEngine;

namespace Funseki.Interaction
{
    // Tunables of target selection, the prompt and the highlight (GDD 5.1).
    // Assets/_Project/Data/Interaction/InteractionSettings.asset.
    [CreateAssetMenu(fileName = "InteractionSettings", menuName = "Funseki/Interaction/Interaction Settings")]
    public class InteractionSettings : ScriptableObject
    {
        [Header("Target selection")]
        [Tooltip("Farthest target from the hero, m")]
        public float radius = 2f;
        [Tooltip("Width of the cone in front of the hero, degrees")]
        [Range(10f, 360f)] public float coneAngle = 90f;
        [Tooltip("Height on the hero the distance and cone are measured from, m")]
        public float chestHeight = 1f;
        [Tooltip("Height of the hero's eyes for the line-of-sight check, m")]
        public float eyeHeight = 1.45f;
        [Tooltip("Layers that hold interactable colliders")]
        public LayerMask targetLayers = ~0;
        [Tooltip("Layers that block the line of sight to a target")]
        public LayerMask occluderLayers = ~0;

        [Header("Prompt")]
        [Tooltip("{0} is the target's Prompt")]
        public string interactFormat = "E — {0}";
        [Tooltip("Shown over an item target while an item is in hand; {0} is the item name")]
        public string useItemFormat = "ЛКМ — {0}";
        public TMP_FontAsset font;
        [Tooltip("Text size; 100 = 1 m")]
        public float fontSize = 16f;
        public Color textColor = Color.white;
        public Color backgroundColor = new(0f, 0f, 0f, 0.6f);
        [Tooltip("Gap between the top of the target and the prompt, m")]
        public float promptOffset = 0.25f;
        [Tooltip("The prompt is moved this far toward the camera, so walls around a door don't hide it, m")]
        public float promptPullToCamera = 0.6f;
        [Tooltip("Draw the prompt over all geometry")]
        public bool promptOnTop = true;

        [Header("Highlight")]
        [Tooltip("Extra material drawn over the target (transparent, additive)")]
        public Material highlightMaterial;

        [Header("States")]
        [Tooltip("Game states where the hero can interact. Without Bootstrap (no state machine) interaction is always on.")]
        public GameState[] activeStates = { GameState.Break };

        public bool IsActive(GameState state) => System.Array.IndexOf(activeStates, state) >= 0;
    }
}
