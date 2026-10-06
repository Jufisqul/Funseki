using UnityEngine;

namespace Funseki.Pranks
{
    // The PE teacher's routine for the slice prank «Украсть свисток»: on the first break he walks to the vending machine
    // in the courtyard, puts the whistle on the bench and fights the machine — the window for the prank.
    // Data/Pranks/Whistle/WhistleRoutine.asset; played by WhistleTeacher.
    [CreateAssetMenu(fileName = "WhistleRoutine", menuName = "Funseki/Pranks/Whistle Routine")]
    public class WhistleRoutineData : ScriptableObject
    {
        [Tooltip("The prank of the whistle on the bench")]
        public PrankData prank;
        [Tooltip("Day phase in which the routine runs")]
        public string phaseId = "break_1";

        [Header("Schedule, s")]
        [Tooltip("From the start of the phase to the first walk to the machine")]
        public float firstTripDelay = 8f;
        [Tooltip("A new walk starts this long after the previous one, until the whistle is stolen")]
        public float repeatEvery = 90f;
        [Tooltip("How long he fights the machine (the whistle lies on the bench)")]
        public float fightDuration = 20f;

        [Header("Movement")]
        [Tooltip("m/s")]
        public float walkSpeed = 1.8f;
        [Tooltip("deg/s")]
        public float turnSpeed = 300f;

        [Header("Glances at the bench while fighting, s")]
        public float glanceEveryMin = 3f;
        public float glanceEveryMax = 5f;
        public float glanceDuration = 1.2f;

        [Header("Кайто kicks the machine")]
        [Tooltip("He turns away for this long, s")]
        public float distractDuration = 5f;
        [TextArea(1, 2)] public string[] distractLines = { "А? Кто там?!" };

        [Header("After the whistle is stolen")]
        [Tooltip("He stays at the machine while the world reaction plays, s")]
        public float panicTime = 10f;

        [Header("Lines (a random one)")]
        [TextArea(1, 2)] public string[] goLines = { "Ну, железяка, сегодня ты мне отдашь банку." };
        [TextArea(1, 2)] public string[] fightLines = { "Отдай банку!", "Я двадцать лет в спорте!", "Кхм. Ещё раз.", "Да что ж ты такой!" };
        public float fightLineEvery = 4.5f;
        [TextArea(1, 2)] public string[] giveUpLines = { "Ничья. В следующий раз." };
    }
}
