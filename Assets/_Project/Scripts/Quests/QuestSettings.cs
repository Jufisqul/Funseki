using System.Collections.Generic;
using UnityEngine;

namespace Funseki.Quests
{
    // Every quest of the game and the quest log rules. Assets/_Project/Data/Quests/QuestSettings.asset.
    [CreateAssetMenu(fileName = "QuestSettings", menuName = "Funseki/Quests/Quest Settings")]
    public class QuestSettings : ScriptableObject
    {
        [Tooltip("All quests; a quest not in this list never starts")]
        public List<QuestData> quests = new();
        [Tooltip("Autosave when a quest starts or one of its steps is done")]
        public bool autosaveOnProgress = true;
    }
}
