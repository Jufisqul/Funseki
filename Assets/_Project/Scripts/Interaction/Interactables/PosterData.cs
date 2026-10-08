using Funseki.Core;
using UnityEngine;

namespace Funseki.Interaction
{
    // Плакат или картина. Heroes from drawHeroes with drawItem in the inventory get E = drawing on it (close-up,
    // LMB / A paints, Esc / E finishes); other heroes get E = inspect with the usual lines. Once drawn on, nobody gets E.
    // One asset per kind of picture (posters, paintings); each object keeps its own state by objectId.
    [CreateAssetMenu(fileName = "Interactable_Poster", menuName = "Funseki/Interaction/Poster")]
    public class PosterData : InspectData
    {
        [Header("Picture")]
        public Texture2D cleanTexture;
        [Tooltip("Defaced versions; shown only when a drawn-on picture has no hand drawing in the save")]
        public Texture2D[] drawnTextures;

        [Header("Drawing: who and with what")]
        [Tooltip("The item that draws (Маркер); it only has to be in the inventory, not in the hand")]
        public ItemData drawItem;
        [Tooltip("Heroes that can draw (spec: Рюта and Кайто)")]
        public HeroId[] drawHeroes = { HeroId.Ryuta, HeroId.Kaito };
        public string drawPrompt = "Разрисовать";

        [Header("Drawing: the mini-game")]
        [Tooltip("Hint at the bottom of the close-up")]
        public string drawHint = "ЛКМ / A — рисовать · Esc / E — готово";
        public Color inkColor = new(0.8f, 0.05f, 0.08f);
        [Tooltip("Brush radius in pixels of the picture texture")]
        [Min(1)] public int brushRadius = 4;
        [Tooltip("Gamepad cursor speed, share of the screen height per second")]
        public float cursorSpeed = 0.6f;
        [Tooltip("Priority of the close-up camera over the gameplay cameras")]
        public int cameraPriority = 60;
        [Tooltip("Painted pixels needed for the drawing to count (less = nothing happened)")]
        [Min(1)] public int minPaintedPixels = 40;
        [Tooltip("How often a teacher is looked for while drawing, s")]
        public float watchCheckInterval = 0.5f;

        [Header("Drawing: lines")]
        [Tooltip("Said when the drawing is done (a random one)")]
        [TextArea(1, 3)] public string[] drawLines =
        {
            "Усы — это классика.",
            "Теперь это искусство.",
            "Так-то лучше.",
            "Директор оценит.",
        };
        [Tooltip("Per-hero lines for a finished drawing (the first list of each entry); empty = drawLines")]
        public System.Collections.Generic.List<HeroLines> heroDrawLines = new();
        [Tooltip("Said when a teacher saw the drawing")]
        [TextArea(1, 3)] public string[] drawSeenLines = { "Ой." };

        [Header("Drawing: caught")]
        [Tooltip("A teacher who sees the drawing catches the hero at once (spec); off = noise instead")]
        public bool seenMeansCaught = true;
        [Tooltip("CaughtSceneData of the punishment; empty = the CaughtDirector default")]
        public ScriptableObject caughtScene;
        [Tooltip("Noise when a teacher sees the drawing and seenMeansCaught is off")]
        [Min(0)] public int drawNoiseAmount = 1;
        public string drawNoiseReason = "poster_drawn";
        [Tooltip("World flag set after the first drawing (for NPC talk), snake_case")]
        public string drawnFlag = "poster_drawn";

        public string[] DrawLines(HeroId hero)
        {
            var own = heroDrawLines.Find(h => h.hero == hero);
            return own != null && own.first is { Length: > 0 } ? own.first : drawLines;
        }
    }
}
