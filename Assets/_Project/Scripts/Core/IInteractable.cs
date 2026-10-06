using UnityEngine;

namespace Funseki.Core
{
    // Anything the hero can use with E (GDD 5.1). Implement it on a MonoBehaviour next to a collider;
    // the hero's interactor picks the closest visible one in front of the character.
    public interface IInteractable
    {
        // Prompt text without the key, e.g. "Взять «Маркер»". Shown as "E — <Prompt>".
        string Prompt { get; }
        bool CanInteract(GameObject hero);
        void Interact(GameObject hero);
    }

    // Something the selected item can be used on with LMB (primary) / RMB (alternative).
    // Return false when this item does nothing here: the hero then says the "won't work" line.
    public interface IItemTarget
    {
        bool UseItem(ItemData item, bool primary);
    }
}
