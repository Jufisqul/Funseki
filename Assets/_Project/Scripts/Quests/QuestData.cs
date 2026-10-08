using System;
using System.Collections.Generic;
using Funseki.Core;
using UnityEngine;

namespace Funseki.Quests
{
    // One step of a quest: the goal shown in the HUD (main quest) and the Diary, done when all its flags are set.
    [Serializable]
    public class QuestStep
    {
        [Tooltip("Goal text: «Познакомься с соседями из комнаты 8»")]
        [TextArea(1, 3)] public string objective;
        [Tooltip("The step is done when ALL of these WorldFlags are set (day1_met_takeshi, day1_met_yukki...)")]
        public List<string> doneFlags = new();
        [Header("Reward when the step is done")]
        [Tooltip("WorldFlags set when the step is done")]
        public List<string> rewardFlags = new();
        [Tooltip("Items handed to the shared inventory")]
        public List<ItemData> rewardItems = new();
    }

    // A quest (task 17, CLAUDE.md «Полный День 1»): steps run top to bottom.
    // Starts when startFlag is set (or from code, IQuestLog.StartQuest). The active step of the main quest
    // is the HUD goal. When the last step is done, doneFlag is set.
    [CreateAssetMenu(fileName = "Q_", menuName = "Funseki/Quests/Quest")]
    public class QuestData : ScriptableObject
    {
        [Tooltip("Unique, used by saves and flags: Q_Day1_Main")]
        public string id;
        public string title;
        public QuestKind kind = QuestKind.Main;
        [Tooltip("WorldFlag that starts the quest (heroes_unlocked). Empty = only from code")]
        public string startFlag;
        [Tooltip("WorldFlag set when the whole quest is done. Empty = quest_<id>_done")]
        public string doneFlag;
        public List<QuestStep> steps = new();

        public string DoneFlag => string.IsNullOrEmpty(doneFlag) ? $"quest_{id.ToLowerInvariant()}_done" : doneFlag;
    }
}
