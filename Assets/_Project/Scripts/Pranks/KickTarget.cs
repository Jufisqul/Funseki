using Funseki.Core;
using UnityEngine;

namespace Funseki.Pranks
{
    // Makes an object a target of Кайто's kick (IKickable): the kick raises GameEvents.OnKicked(this object, hero) and
    // the hero says one of kickLines. Put it next to a trigger collider around the object (the vending machine), so the
    // object's own colliders stay untouched.
    public class KickTarget : MonoBehaviour, IKickable
    {
        [TextArea(1, 2)]
        [SerializeField] string[] kickLines = { "Н-на!" };

        void OnEnable() => GameEvents.OnKicked += OnKicked;
        void OnDisable() => GameEvents.OnKicked -= OnKicked;

        void OnKicked(GameObject target, GameObject hero)
        {
            if (target != gameObject || hero == null) return;
            Debug.Log($"[Prank] {hero.name} kicked {name}.", this);
            string line = PrankData.Pick(kickLines);
            if (string.IsNullOrEmpty(line)) return;
            if (ServiceLocator.TryGet<IBarkService>(out var barks)) barks.Say(hero, line);
            else GameEvents.RaiseHeroBark(hero, line);
        }
    }
}
