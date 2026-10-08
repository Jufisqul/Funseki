using UnityEngine;

namespace Funseki.Story
{
    // The start of day 1 after the prologue (CLAUDE.md «Полный День 1», steps 2–3): Рюта alone in the yard,
    // the principal at the entrance, the move to room 7, meeting Кайто and Рэй.
    // Assets/_Project/Data/Story/Day1Start.asset. Dialogues are DialogueGraph assets (Data/Dialogue/Day1).
    [CreateAssetMenu(fileName = "Day1Start", menuName = "Funseki/Story/Day 1 Start Data")]
    public class Day1StartData : ScriptableObject
    {
        [Header("Day phases")]
        [Tooltip("Phase in which Рюта arrives and meets the principal; the heroes are placed when it starts")]
        public string arrivalPhaseId = "arrival";
        [Tooltip("Phases of this chain. A day that starts after them (Quick Play) counts the chain as done")]
        public string[] chainPhaseIds = { "prologue", "arrival", "dorm" };

        [Header("Flags")]
        [Tooltip("Set once the heroes stood at their start spots (not placed again after a load)")]
        public string placedFlag = "day1_start_placed";
        [Tooltip("The principal's welcome at the entrance is over")]
        public string directorMetFlag = "day1_director_met";
        [Tooltip("The principal showed room 7 and left; the arrival phase's goal flag")]
        public string directorDoneFlag = "day1_director_done";
        [Tooltip("Кайто and Рэй join (HeroSettings.partyUnlockFlag); the main quest starts on it")]
        public string partyFlag = "heroes_unlocked";
        [Tooltip("Set in this order when the day starts after the chain (Quick Play into break_1): the whole start counts as done")]
        public string[] skipFlags =
        {
            "day1_start_placed", "day1_director_met", "day1_director_done", "day1_met_takeshi", "day1_met_yukki",
            "day1_met_masumi", "day1_met_hiro", "day1_met_tsubaki", "heroes_unlocked",
        };

        [Header("Principal")]
        [Tooltip("The welcome starts by itself when the hero comes this close, m")]
        public float directorTalkRadius = 3f;
        [Tooltip("DialogueGraph: the welcome at the entrance")]
        public ScriptableObject directorIntro;
        [Tooltip("DialogueGraph: at the door of room 7")]
        public ScriptableObject directorRoom;
        [Tooltip("Walking speed when the principal leaves, m/s")]
        public float directorWalkSpeed = 1.5f;

        [Header("Room 7")]
        [Tooltip("DialogueGraph: meeting Кайто and Рэй")]
        public ScriptableObject room7Meet;

        [Header("Fades")]
        public float fadeOutTime = 0.6f;
        [Tooltip("Black screen while the hero is moved, s")]
        public float blackTime = 0.5f;
        public float fadeInTime = 0.6f;

        [Header("Fence")]
        [Tooltip("Рюта's line at the fence: the yard is fine, beyond the fence is not")]
        public string fenceLine = "[TODO] Забор. Дальше мне нельзя.";
        [Tooltip("The line is said again no sooner than this, s")]
        public float fenceLineCooldown = 5f;
    }
}
