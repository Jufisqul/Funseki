using Funseki.Core;
using UnityEngine;

namespace Funseki.Inventory
{
    // An item lying in the world. E puts it into the shared inventory and hides this object.
    // Optional world flag remembers it was taken, so a loaded save doesn't show it again.
    public class PickupItem : MonoBehaviour, IInteractable, IPickup
    {
        [SerializeField] ItemData item;
        [Tooltip("snake_case flag set when taken; empty for none")]
        [SerializeField] string takenFlag;
        [Tooltip("What Рюта says about this spot when his «Голоса» find the item («Там, за автоматом…»); empty = a generic line")]
        [SerializeField] string voiceHint;

        public ItemData Item => item;
        public string VoiceHint => voiceHint;

        public string Prompt =>
            ServiceLocator.TryGet<Inventory>(out var inv) ? string.Format(inv.Settings.pickupPromptFormat, item.displayName) : item.displayName;

        void Start()
        {
            if (!string.IsNullOrEmpty(takenFlag) && ServiceLocator.TryGet<WorldFlags>(out var flags) && flags.GetFlag(takenFlag))
                gameObject.SetActive(false);
        }

        public bool CanInteract(GameObject hero) => item != null && ServiceLocator.IsRegistered<Inventory>();

        public void Interact(GameObject hero)
        {
            var inv = ServiceLocator.Get<Inventory>();
            if (!inv.TryAdd(item))
            {
                GameEvents.RaiseHeroBark(hero, inv.Settings.fullLine);
                return;
            }
            if (!string.IsNullOrEmpty(takenFlag) && ServiceLocator.TryGet<WorldFlags>(out var flags))
                flags.SetFlag(takenFlag);
            gameObject.SetActive(false);
        }
    }
}
