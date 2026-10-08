using Funseki.Core;
using UnityEngine;

namespace Funseki.Interaction
{
    // Душевая (spec «Душевая»). E turns the water on / off; Рюта or Кайто with paintItem in hand pour it into the
    // shower head instead (once per shower), and from then on the water runs in paintColor. The first-time lines of
    // InteractableData are said the first time the water is turned on, the repeat lines on later turns (empty = silent).
    [CreateAssetMenu(fileName = "Interactable_Shower", menuName = "Funseki/Interaction/Shower")]
    public class ShowerData : InteractableData
    {
        [Header("Water")]
        public string onPrompt = "Включить душ";
        public string offPrompt = "Выключить душ";
        public Color waterColor = new(0.65f, 0.85f, 1f, 0.45f);

        [Header("Paint")]
        [Tooltip("The item poured into the shower head (Краска); it has to be in the hero's hand")]
        public ItemData paintItem;
        [Tooltip("Heroes that can pour it (spec: Рюта and Кайто)")]
        public HeroId[] paintHeroes = { HeroId.Ryuta, HeroId.Kaito };
        public string paintPrompt = "Залить краску в лейку";
        [Tooltip("The paint is used up")]
        public bool consumeItem = true;
        public Color paintColor = new(0.95f, 0.2f, 0.6f, 0.9f);
        [Tooltip("Said after pouring (spec: a short chuckle)")]
        [TextArea(1, 3)] public string[] paintLines = { "Хе-хе." };
        [Tooltip("Said the first time the painted shower is turned on")]
        [TextArea(1, 3)] public string[] paintedWaterLines = { "Ха! Работает." };
        [Tooltip("Said when a teacher sees the pouring")]
        [TextArea(1, 3)] public string[] paintSeenLines = { "Это… для уборки!" };
        [Tooltip("Noise when a teacher sees the pouring; 0 = none")]
        [Min(0)] public int paintNoiseAmount = 25;
        public string paintNoiseReason = "shower_paint";
        [Tooltip("World flag set when any shower gets the paint (for a future prank), snake_case")]
        public string paintedFlag = "shower_paint_ready";
    }
}
