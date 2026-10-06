using Funseki.Core;
using UnityEngine;

namespace Funseki.School
{
    // A door (or an open passage) into a zone that opens on a later day.
    // On a passage without a Door, E makes the hero say it's locked; a Door handles that itself.
    public class LockedDoor : MonoBehaviour, IInteractable
    {
        public string zoneId;
        [Range(1, 7)] public int unlockDay = 1;

        [Header("Texts")]
        public string prompt = "Пройти";
        public string lockedLine = "Заперто";

        public bool IsLocked => SchoolDay.Current < unlockDay;

        // ---- IInteractable
        public string Prompt => prompt;
        public bool CanInteract(GameObject hero) => IsLocked && GetComponent<Door>() == null;
        public void Interact(GameObject hero) => GameEvents.RaiseHeroBark(hero, lockedLine);
    }
}
