using Funseki.Core;
using UnityEngine;

namespace Funseki.Dialogue
{
    // Talk to a character with E: starts its DialogueGraph through the DialogueRunner service.
    // talkedFlag (optional) is set once a conversation with it has ended, so a quest step can wait for
    // "talked to everyone in the room" without relying on the actions inside the graph.
    public class DialogueNpc : MonoBehaviour, IInteractable
    {
        const string DefaultPrompt = "Поговорить";

        [SerializeField] DialogueGraph dialogue;
        [Tooltip("Prompt after \"E — \"; empty = «Поговорить»")]
        [SerializeField] string prompt = DefaultPrompt;
        [Tooltip("WorldFlag set after a conversation with this character ends (day1_met_takeshi). Empty = none")]
        [SerializeField] string talkedFlag;

        public string Prompt => string.IsNullOrEmpty(prompt) ? DefaultPrompt : prompt;

        void OnEnable() => GameEvents.OnDialogueEnded += OnDialogueEnded;
        void OnDisable() => GameEvents.OnDialogueEnded -= OnDialogueEnded;

        public bool CanInteract(GameObject hero) =>
            dialogue != null && ServiceLocator.TryGet<DialogueRunner>(out var runner) && !runner.IsRunning;

        public void Interact(GameObject hero)
        {
            if (ServiceLocator.TryGet<DialogueRunner>(out var runner)) runner.StartDialogue(dialogue, gameObject, hero);
        }

        void OnDialogueEnded(GameObject npc, GameObject hero)
        {
            if (npc != gameObject || string.IsNullOrEmpty(talkedFlag)) return;
            if (ServiceLocator.TryGet<WorldFlags>(out var flags)) flags.SetFlag(talkedFlag);
        }
    }
}
