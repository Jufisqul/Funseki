using System.Collections.Generic;

namespace Funseki.Core
{
    public enum QuestKind { Main, Side }

    public enum QuestStatus { NotStarted, Active, Done }

    // What the quest log knows about one quest right now. The data itself (title, steps) is Funseki.Quests.QuestData.
    public readonly struct QuestInfo
    {
        public readonly string id;
        public readonly string title;
        public readonly QuestKind kind;
        public readonly QuestStatus status;
        /// <summary>Index of the current step; equals StepCount when the quest is done.</summary>
        public readonly int step;
        public readonly int stepCount;
        /// <summary>Goal text of the current step; empty when the quest is not active.</summary>
        public readonly string objective;

        public QuestInfo(string id, string title, QuestKind kind, QuestStatus status, int step, int stepCount, string objective)
        {
            this.id = id;
            this.title = title;
            this.kind = kind;
            this.status = status;
            this.step = step;
            this.stepCount = stepCount;
            this.objective = objective;
        }
    }

    // The quest log (Funseki.Quests.QuestService under [Bootstrap]), via ServiceLocator.
    // The Diary UI and story scripts read it; quests start by their start flag or by StartQuest.
    public interface IQuestLog
    {
        IReadOnlyList<QuestInfo> Quests { get; }
        bool TryGet(string questId, out QuestInfo quest);
        /// <summary>Starts a quest that has not started yet; false if unknown or already started.</summary>
        bool StartQuest(string questId);
    }
}
