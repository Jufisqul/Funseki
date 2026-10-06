using Funseki.Core;
using UnityEngine;

namespace Funseki.Interaction
{
    // Плакат или картина. E (no marker) = inspect with the first-time / repeat lines; LMB with drawItem = deface it.
    [CreateAssetMenu(fileName = "Interactable_Poster", menuName = "Funseki/Interaction/Poster")]
    public class PosterData : InteractableData
    {
        [Header("Picture")]
        public Texture2D cleanTexture;
        [Tooltip("Defaced versions; each new drawing picks one the poster doesn't show yet")]
        public Texture2D[] drawnTextures;

        [Header("Drawing")]
        [Tooltip("The item that draws (Маркер)")]
        public ItemData drawItem;
        [TextArea(1, 3)] public string[] drawLines =
        {
            "Усы — это классика.",
            "Теперь это искусство.",
            "Так-то лучше.",
            "Директор оценит.",
        };
        [Tooltip("Said instead when a teacher saw the drawing; empty = the usual draw line")]
        [TextArea(1, 3)] public string[] drawSeenLines = { "Ой." };
        [Tooltip("Noise when a teacher sees the drawing")]
        [Min(0)] public int drawNoiseAmount = 1;
        public string drawNoiseReason = "poster_drawn";
        [Tooltip("World flag set after the first drawing (for NPC talk), snake_case")]
        public string drawnFlag = "poster_drawn";
    }
}
