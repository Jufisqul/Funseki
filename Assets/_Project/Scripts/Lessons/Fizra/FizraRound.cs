using System.Collections.Generic;
using UnityEngine;

namespace Funseki.Lessons.Fizra
{
    // One round of whistles. Physical education uses three (6–8 commands each); the future «Контрольная» takes one
    // round of 3 commands in 10 s (FizraRound_Exam3in10). Assets/_Project/Data/Lessons/Fizra/Rounds.
    [CreateAssetMenu(fileName = "FizraRound", menuName = "Funseki/Lessons/Fizra/Round")]
    public class FizraRound : ScriptableObject
    {
        [Tooltip("Caption at the start of the round: «Раунд 1»")]
        public string title = "Раунд 1";
        [Tooltip("Commands that can come up in this round")]
        public List<WhistleCommand> commands = new();
        [Tooltip("The whistle this round adds: shown once with its translation before the round, even without captions")]
        public WhistleCommand newCommand;

        [Header("Count and pace")]
        [Min(1)] public int minCommands = 6;
        [Min(1)] public int maxCommands = 8;
        [Tooltip("Seconds to react to a command")]
        [Min(0.2f)] public float reactionWindow = 1.5f;
        [Tooltip("Pause between commands, s (smaller = faster)")]
        [Min(0f)] public float gap = 1.2f;
        [Tooltip("If > 0: all the commands fit into this many seconds (the pause is computed). «Контрольная»: 3 in 10 s")]
        [Min(0f)] public float totalTime;

        [Header("Captions")]
        [Tooltip("Show the translation under the pictogram")]
        public bool showCaptions = true;

        [Header("False commands")]
        [Tooltip("Things that sound like a whistle but are not (the kettle)")]
        public List<WhistleCommand> falseCommands = new();
        [Min(0)] public int falseCount;
        [Tooltip("The false command comes in the middle of the round, otherwise anywhere")]
        public bool falseInMiddle = true;
        [Tooltip("A false command counts towards the score (ignoring it = a point)")]
        public bool falseCountsInScore = true;

        [Header("Start of the round")]
        [Tooltip("The teacher whistles this before the round (round 3: a melody). Empty = nothing")]
        public string startPattern;
        public AudioClip startClip;
        [Tooltip("Bark of the teacher or the helper at the start (role: Teacher)")]
        public string startLine;
    }
}
