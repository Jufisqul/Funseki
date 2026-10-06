using UnityEngine;

namespace Funseki.Interaction
{
    // Шкафчик Рюты. The first-time / repeat lines are said when the door opens («Ну и бардак»).
    [CreateAssetMenu(fileName = "Interactable_Locker", menuName = "Funseki/Interaction/Locker")]
    public class LockerData : InspectData
    {
        [Header("Door")]
        [Tooltip("How far the door (hinged on its left edge) swings out, degrees")]
        public float doorOpenAngle = 110f;
        [Tooltip("Seconds to open or close the door")]
        public float doorTime = 0.35f;

        [Header("Close-up")]
        [Tooltip("Priority of the close-up camera over the gameplay cameras")]
        public int cameraPriority = 60;
        [Tooltip("Captions of the details inside, by InspectDetail.index")]
        [TextArea(1, 3)] public string[] captions =
        {
            "Фото с какой-то девчонкой. Подписано «Не звони мне».",
            "Кроссовка. Одна. Вторую никто не видел с апреля.",
            "Учебник по математике. Ни разу не открыт, но весь в наклейках.",
            "Бенто трёхдневной давности. Оно смотрит в ответ.",
        };
    }
}
