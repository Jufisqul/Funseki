using Funseki.Core;
using UnityEngine;

namespace Funseki.Dialogue
{
    // A character that says a line from its BarkSet when the hero comes close (GDD 5.3).
    // The hero is the object tagged "Player" (or, failing that, one with a CharacterController) within triggerRadius.
    public class BarkTrigger : MonoBehaviour
    {
        [SerializeField] BarkSet barks;
        [Tooltip("How often the distance to the hero is checked, s")]
        [SerializeField] float checkInterval = 0.2f;

        readonly Collider[] hits = new Collider[16];
        bool heroInside;
        float nextCheck, lastBark = -1000f;

        void Update()
        {
            if (barks == null || Time.time < nextCheck) return;
            nextCheck = Time.time + checkInterval;

            bool inside = HeroNearby();
            if (inside && !heroInside) TryBark();
            heroInside = inside;
        }

        bool HeroNearby()
        {
            int n = Physics.OverlapSphereNonAlloc(transform.position, barks.triggerRadius, hits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var root = hits[i].transform.root;
                if (root == transform.root) continue;
                if (root.CompareTag("Player") || hits[i].GetComponent<CharacterController>() != null) return true;
            }
            return false;
        }

        void TryBark()
        {
            if (Time.time - lastBark < barks.cooldown) return;
            if (ServiceLocator.TryGet<GameStateMachine>(out var fsm) && !barks.IsAllowed(fsm.Current)) return;
            if (!ServiceLocator.TryGet<IBarkService>(out var service)) return;

            var hero = ServiceLocator.TryGet<DialogueRunner>(out var runner) ? runner.CurrentHero : HeroId.Kaito;
            string line = barks.Pick(hero);
            if (line == null) return;
            service.Say(gameObject, line);
            lastBark = Time.time;
        }

        void OnDrawGizmosSelected()
        {
            if (barks == null) return;
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, barks.triggerRadius);
        }
    }
}
