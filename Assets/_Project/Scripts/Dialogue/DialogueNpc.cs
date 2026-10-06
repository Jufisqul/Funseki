using Funseki.Core;
using UnityEngine;

namespace Funseki.Dialogue
{
    // Talk to a character with E: starts its DialogueGraph through the DialogueRunner service.
    public class DialogueNpc : MonoBehaviour, IInteractable
    {
        const string DefaultPrompt = "Поговорить";

        [SerializeField] DialogueGraph dialogue;
        [Tooltip("Prompt after \"E — \"; empty = «Поговорить»")]
        [SerializeField] string prompt = DefaultPrompt;

        public string Prompt => string.IsNullOrEmpty(prompt) ? DefaultPrompt : prompt;

        public bool CanInteract(GameObject hero) =>
            dialogue != null && ServiceLocator.TryGet<DialogueRunner>(out var runner) && !runner.IsRunning;

        public void Interact(GameObject hero)
        {
            if (ServiceLocator.TryGet<DialogueRunner>(out var runner)) runner.StartDialogue(dialogue, gameObject, hero);
        }
    }
}
