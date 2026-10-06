using TMPro;
using UnityEngine;

namespace Funseki.UI
{
    // Look and timing of the hero's short spoken lines ("Не сработает"). Assets/_Project/Data/UI/BarkSettings.asset.
    [CreateAssetMenu(fileName = "BarkSettings", menuName = "Funseki/UI/Bark Settings")]
    public class BarkSettings : ScriptableObject
    {
        [Tooltip("How long a line stays on screen, s")]
        public float duration = 1.8f;
        [Tooltip("Fade out time at the end, s")]
        public float fadeTime = 0.3f;
        public TMP_FontAsset font;
        public float fontSize = 34f;
        public Color textColor = Color.white;
        public Color outlineColor = new(0f, 0f, 0f, 0.85f);
        [Range(0f, 1f)] public float outlineWidth = 0.2f;
        [Tooltip("Height of the line on screen, share of the screen height from the bottom")]
        [Range(0f, 1f)] public float screenHeight = 0.3f;
    }
}
