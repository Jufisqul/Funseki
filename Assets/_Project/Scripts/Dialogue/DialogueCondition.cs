using System;
using System.Collections.Generic;
using Funseki.Core;
using UnityEngine;

namespace Funseki.Dialogue
{
    public enum ConditionKind
    {
        FlagIsSet,
        FlagIsNotSet,
        CounterAtLeast,
        CounterBelow,
        HeroIs,
        HeroIsNot
    }

    // One check against WorldFlags or the current hero. A node, choice or bark line is used only if all its checks pass.
    [Serializable]
    public class DialogueCondition
    {
        public ConditionKind kind;
        [Tooltip("Flag or counter id (snake_case), for the Flag / Counter checks")]
        public string id;
        [Tooltip("For CounterAtLeast / CounterBelow")]
        public int value;
        [Tooltip("For HeroIs / HeroIsNot")]
        public HeroId hero;

        public bool IsMet(HeroId currentHero)
        {
            ServiceLocator.TryGet<WorldFlags>(out var flags);
            switch (kind)
            {
                case ConditionKind.FlagIsSet: return flags != null && flags.GetFlag(id);
                case ConditionKind.FlagIsNotSet: return flags == null || !flags.GetFlag(id);
                case ConditionKind.CounterAtLeast: return (flags != null ? flags.GetCounter(id) : 0) >= value;
                case ConditionKind.CounterBelow: return (flags != null ? flags.GetCounter(id) : 0) < value;
                case ConditionKind.HeroIs: return currentHero == hero;
                case ConditionKind.HeroIsNot: return currentHero != hero;
                default: return true;
            }
        }

        public static bool AllMet(List<DialogueCondition> conditions, HeroId currentHero)
        {
            if (conditions == null) return true;
            foreach (var c in conditions)
                if (c != null && !c.IsMet(currentHero)) return false;
            return true;
        }
    }

    public enum ActionKind
    {
        SetFlag,
        ClearFlag,
        AddToCounter,
        GiveItem
    }

    // Something a node or a choice does to the world when it is shown / picked.
    [Serializable]
    public class DialogueAction
    {
        public ActionKind kind;
        [Tooltip("Flag or counter id (snake_case)")]
        public string id;
        [Tooltip("For AddToCounter")]
        public int amount = 1;
        [Tooltip("For GiveItem: goes to the shared inventory")]
        public ItemData item;

        public void Run()
        {
            ServiceLocator.TryGet<WorldFlags>(out var flags);
            switch (kind)
            {
                case ActionKind.SetFlag: flags?.SetFlag(id, true); break;
                case ActionKind.ClearFlag: flags?.SetFlag(id, false); break;
                case ActionKind.AddToCounter: flags?.AddCounter(id, amount); break;
                case ActionKind.GiveItem: if (item != null) GameEvents.RaiseItemGiven(item); break;
            }
            if (flags == null && kind != ActionKind.GiveItem)
                Debug.LogWarning($"[Dialogue] No WorldFlags service, action {kind} '{id}' skipped. Play from Bootstrap.");
        }

        public static void RunAll(List<DialogueAction> actions)
        {
            if (actions == null) return;
            foreach (var a in actions) a?.Run();
        }
    }
}
