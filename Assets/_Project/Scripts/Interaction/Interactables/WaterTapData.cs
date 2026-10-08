using UnityEngine;

namespace Funseki.Interaction
{
    // Кранчик с водой. The first-time lines of InteractableData are said at the brown water («Дерьмо»);
    // the repeat lines on ordinary openings (empty = silent).
    [CreateAssetMenu(fileName = "Interactable_WaterTap", menuName = "Funseki/Interaction/Water Tap")]
    public class WaterTapData : InteractableData
    {
        [Header("Tap")]
        public string openPrompt = "Открыть кран";
        public string closePrompt = "Закрыть кран";

        [Header("Water")]
        [Tooltip("Seconds of brown water at the very first opening")]
        public float dirtyDuration = 2f;
        [Tooltip("Seconds the brown fades to clear")]
        public float clearingTime = 0.4f;
        public Color dirtyColor = new(0.45f, 0.3f, 0.12f, 0.9f);
        public Color cleanColor = new(0.65f, 0.85f, 1f, 0.55f);

        [Header("Every N-th opening")]
        [Tooltip("Every N-th opening says a milestone line instead; 0 = never")]
        public int milestoneEvery = 10;
        [TextArea(1, 3)] public string[] milestoneLines = { "Да, всё ещё вода." };

        [Header("Teacher nearby")]
        [Tooltip("Shouted by the teacher who sees the tap opened (over the teacher, not the hero); empty = nothing")]
        [TextArea(1, 3)] public string[] teacherShoutLines = { "Не трать воду!" };
    }
}
