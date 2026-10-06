using TMPro;
using UnityEngine;
using UnityEngine.Playables;

namespace Funseki.DayCycle
{
    // A story cutscene of a day phase (StoryScene): its Timeline, the dialogue in the middle, the skip rules and texts.
    // Assets/_Project/Data/Cutscenes/Cutscene_*.asset. Played by StoryCutscene in the day scene.
    [CreateAssetMenu(fileName = "Cutscene", menuName = "Funseki/DayCycle/Cutscene")]
    public class CutsceneData : ScriptableObject
    {
        [Tooltip("Day phase that plays this cutscene (DaySchedule): intro")]
        public string phaseId = "intro";
        [Tooltip("The Timeline: camera moves, who is visible, the day title")]
        public PlayableAsset timeline;

        [Header("Dialogue")]
        [Tooltip("DialogueGraph played in the middle; the Timeline waits for it")]
        public ScriptableObject dialogue;
        [Tooltip("Timeline second at which the Timeline pauses and the dialogue starts")]
        [Min(0f)] public float dialogueAt = 36f;

        [Header("Day title")]
        public string title = "День 1";
        public string subtitle = "Понедельник. Весна 2003-го";

        [Header("Skip")]
        [Tooltip("Hold the Cutscene/Skip button (Space / A) this long to skip, seconds")]
        [Min(0.1f)] public float skipHoldTime = 1f;
        public string skipHintKeyboard = "Удерживайте Пробел, чтобы пропустить";
        public string skipHintGamepad = "Удерживайте A, чтобы пропустить";
        [Tooltip("The hint appears after the first press")]
        public bool hintOnlyAfterPress = true;

        [Header("Fades")]
        [Min(0f)] public float fadeIn = 1.2f;
        [Tooltip("Black fade at the end; the heroes appear in the school hall behind it")]
        [Min(0f)] public float fadeOut = 1.2f;
        [Min(0f)] public float fadeBackIn = 0.8f;

        [Header("Look")]
        public TMP_FontAsset font;
        public float hintFontSize = 26f;
        public Color hintColor = new(1f, 0.98f, 0.93f, 0.85f);
        public Color barColor = new(1f, 0.78f, 0.25f, 1f);
    }
}
