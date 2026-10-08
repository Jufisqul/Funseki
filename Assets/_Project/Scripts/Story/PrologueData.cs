using TMPro;
using UnityEngine;

namespace Funseki.Story
{
    // The day 1 prologue: Тамура Рюта's file on the desk (CLAUDE.md «Полный День 1», step 1).
    // Assets/_Project/Data/Story/Prologue_Day1.asset. Texts are placeholders for the game designer.
    [CreateAssetMenu(fileName = "Prologue_", menuName = "Funseki/Story/Prologue Data")]
    public class PrologueData : ScriptableObject
    {
        [Header("Day flow")]
        [Tooltip("DaySchedule phase that shows the file")]
        public string phaseId = "prologue";
        [Tooltip("WorldFlag set when the file is stamped (or skipped); the phase's goal flag")]
        public string doneFlag = "day1_prologue_done";

        [Header("The file")]
        public string folderLabel = "ДЕЛО №[TODO]";
        [Tooltip("Front photo (анфас); empty = a grey placeholder with the caption")]
        public Sprite photoFront;
        [Tooltip("Side photo (профиль); empty = a grey placeholder with the caption")]
        public Sprite photoProfile;
        public string photoFrontCaption = "Анфас";
        public string photoProfileCaption = "Профиль";
        public string fullNameLabel = "Ф. И. О.";
        public string fullName = "[TODO] Тамура Рюта";
        public string birthDateLabel = "Дата рождения";
        public string birthDate = "[TODO]";
        public string descriptionLabel = "Характеристика";
        [TextArea(3, 10)] public string description = "[TODO] Описание ученика.";
        public string reasonLabel = "Причина перевода";
        [TextArea(3, 10)] public string transferReason = "[TODO] За что переводят в Фунсэки.";

        [Header("Stamp")]
        public string stampText = "ПЕРЕВЕДЁН — ФУНСЭКИ";
        public Color stampColor = new(0.78f, 0.1f, 0.1f, 0.92f);
        [Tooltip("Stamp tilt, degrees")]
        public float stampAngle = -12f;
        public AudioClip stampSound;
        [Range(0f, 1f)] public float stampVolume = 1f;
        [Tooltip("How long the stamp falls onto the page, s")]
        public float stampDropTime = 0.12f;
        [Tooltip("Screen shake after the stamp hits: strength in reference pixels and time, s")]
        public float shakeStrength = 14f;
        public float shakeTime = 0.3f;
        [Tooltip("Pause after the stamp before the screen goes dark, s")]
        public float holdAfterStamp = 1.3f;

        [Header("Pages and fades")]
        [Tooltip("Page turn time, s")]
        public float flipTime = 0.3f;
        public float fadeOutTime = 0.8f;
        [Tooltip("Black screen between the file and the school, s")]
        public float blackTime = 0.4f;
        public float fadeInTime = 0.8f;
        [Tooltip("Hold the skip button this long to skip the prologue, s")]
        public float skipHoldTime = 1f;

        [Header("Hints")]
        public string nextHint = "ЛКМ — перевернуть страницу";
        public string stampHint = "ЛКМ — поставить печать";
        public string skipHint = "Удерживай Пробел — пропустить";

        [Header("Look")]
        public TMP_FontAsset titleFont;
        public TMP_FontAsset bodyFont;
        public Color deskColor = new(0.33f, 0.22f, 0.15f, 1f);
        public Color folderColor = new(0.86f, 0.74f, 0.52f, 1f);
        public Color pageColor = new(0.96f, 0.94f, 0.88f, 1f);
        public Color inkColor = new(0.16f, 0.15f, 0.2f, 1f);
        public Color photoPlaceholderColor = new(0.62f, 0.62f, 0.6f, 1f);
        public Color hintColor = new(1f, 1f, 1f, 0.85f);
    }
}
