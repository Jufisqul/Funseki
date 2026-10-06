using System;
using System.Collections.Generic;
using UnityEngine;

namespace Funseki.Dialogue
{
    // A choice under a node's line. Up to 3 regular ones plus one "Голос" option that only Кайто sees.
    [Serializable]
    public class DialogueChoice
    {
        [TextArea(1, 3)] public string text;
        [Tooltip("The «Голос» option: shown only while Кайто is in control, marked with an icon")]
        public bool voice;
        [Tooltip("The option is hidden unless all of these pass")]
        public List<DialogueCondition> conditions = new();
        [Tooltip("Run when the player picks it")]
        public List<DialogueAction> actions = new();
        [Tooltip("Id of the node to go to; empty = the node right after this one in the list")]
        public string next;
        [Tooltip("Picking it ends the conversation")]
        public bool end;
    }

    // One step of a conversation: a line (speaker + text) and, optionally, choices shown after it.
    [Serializable]
    public class DialogueNode
    {
        [Tooltip("Unique within the graph; referenced by 'next'. Filled automatically when empty")]
        public string id;
        public Speaker speaker;
        [Tooltip("May be empty for a node that only shows choices")]
        [TextArea(2, 5)] public string text;
        [Tooltip("The node is skipped (to the next one in the list) unless all of these pass")]
        public List<DialogueCondition> conditions = new();
        [Tooltip("Run when the node is shown")]
        public List<DialogueAction> actions = new();
        [Tooltip("Shown after the line is typed out. Max 3 regular + 1 «Голос»")]
        public List<DialogueChoice> choices = new();
        [Tooltip("Id of the node after this one (when there are no choices); empty = next in the list")]
        public string next;
        [Tooltip("The conversation ends after this line")]
        public bool end;
    }

    // A whole conversation (GDD 5.3). Nodes play top to bottom unless 'next' or a choice jumps elsewhere;
    // the conversation ends after the last node or a node / choice marked 'end'.
    [CreateAssetMenu(fileName = "Dialogue_", menuName = "Funseki/Dialogue/Dialogue Graph")]
    public class DialogueGraph : ScriptableObject
    {
        public const int MaxRegularChoices = 3;

        public List<DialogueNode> nodes = new();

        public int IndexOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i].id == id) return i;
            return -1;
        }

        // Index of the node to show after 'from' given a 'next' id (empty = the following node). -1 = the end.
        public int Resolve(string next, int from)
        {
            if (string.IsNullOrEmpty(next)) return from + 1 < nodes.Count ? from + 1 : -1;
            int i = IndexOf(next);
            if (i < 0) Debug.LogWarning($"[Dialogue] {name}: no node with id '{next}', ending the conversation.", this);
            return i;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (n == null) continue;
                if (string.IsNullOrEmpty(n.id)) n.id = UniqueId(i);
                if (!seen.Add(n.id)) Debug.LogWarning($"[Dialogue] {name}: node id '{n.id}' is used twice.", this);

                int regular = 0, voice = 0;
                foreach (var c in n.choices)
                {
                    if (c.voice) voice++; else regular++;
                    if (!string.IsNullOrEmpty(c.next) && IndexOf(c.next) < 0)
                        Debug.LogWarning($"[Dialogue] {name}: node '{n.id}' choice points to missing node '{c.next}'.", this);
                }
                if (regular > MaxRegularChoices)
                    Debug.LogWarning($"[Dialogue] {name}: node '{n.id}' has {regular} regular choices, max {MaxRegularChoices}.", this);
                if (voice > 1)
                    Debug.LogWarning($"[Dialogue] {name}: node '{n.id}' has {voice} «Голос» choices, max 1.", this);
                if (!string.IsNullOrEmpty(n.next) && IndexOf(n.next) < 0)
                    Debug.LogWarning($"[Dialogue] {name}: node '{n.id}' points to missing node '{n.next}'.", this);
            }
        }

        string UniqueId(int index)
        {
            for (int k = index + 1; ; k++)
            {
                string id = $"n{k:00}";
                if (IndexOf(id) < 0) return id;
            }
        }
#endif
    }
}
