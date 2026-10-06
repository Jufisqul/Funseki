using TMPro;
using UnityEngine;

namespace Funseki.Interaction
{
    // Look of a close-up screen (locker, booklet) on top of the usual InteractableData.
    public abstract class InspectData : InteractableData
    {
        [Header("Close-up screen")]
        public TMP_FontAsset font;
        [Tooltip("Hint at the bottom of the screen")]
        public string closeHint = "Esc / E — закрыть";
        public float hintFontSize = 24f;
        public Color textColor = Color.white;
        public Color panelColor = new(0.08f, 0.08f, 0.1f, 0.85f);
    }
}
