using Funseki.Core;
using UnityEngine;

namespace Funseki.NPC
{
    // On an NPC's root: makes it an INpc (Кайто can kick it, a teacher can witness mischief).
    // The kick reaction is a placeholder log for now. A prank within its reaction radius makes the NPC turn to look.
    public class NpcActor : MonoBehaviour, INpc
    {
        [SerializeField] NpcSettings settings;
        [SerializeField] bool isTeacher;
        [Tooltip("Turn to look at a prank done nearby (off for NPCs whose own routine drives their facing)")]
        [SerializeField] bool reactsToPranks = true;

        Vector3 lookAt;
        float lookUntil = -1f;

        public bool IsTeacher => isTeacher;

        void OnEnable()
        {
            GameEvents.OnKicked += OnKicked;
            GameEvents.OnPrankDone += OnPrankDone;
        }

        void OnDisable()
        {
            GameEvents.OnKicked -= OnKicked;
            GameEvents.OnPrankDone -= OnPrankDone;
        }

        void OnKicked(GameObject npc, GameObject hero)
        {
            if (npc != gameObject) return;
            Debug.Log($"[NPC] {name} получил пинок от {(hero != null ? hero.name : "?")} — реакция пока заглушка.", this);
        }

        void OnPrankDone(IPrank prank, Vector3 position)
        {
            if (!reactsToPranks || prank == null) return;
            if (Vector3.Distance(transform.position, position) > prank.ReactionRadius) return;
            lookAt = position;
            lookUntil = Time.time + settings.prankLookTime;
        }

        void Update()
        {
            if (Time.time > lookUntil) return;
            Vector3 to = Vector3.ProjectOnPlane(lookAt - transform.position, Vector3.up);
            if (to.sqrMagnitude < 0.01f) return;
            var target = Quaternion.LookRotation(to.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, settings.turnSpeed * Time.deltaTime);
        }

        public bool CanSee(Vector3 point)
        {
            Vector3 eyes = transform.position + Vector3.up * settings.eyeHeight;
            Vector3 to = point - eyes;
            if (to.magnitude > settings.viewDistance) return false;
            Vector3 flat = Vector3.ProjectOnPlane(to, Vector3.up);
            if (flat.sqrMagnitude > 0.0001f && Vector3.Angle(transform.forward, flat) > settings.viewAngle * 0.5f) return false;
            if (!Physics.Linecast(eyes, point, out var hit, settings.occluderLayers, QueryTriggerInteraction.Ignore)) return true;
            // Hitting a character (the hero's controller, another NPC) doesn't count as a wall.
            return hit.transform.IsChildOf(transform) || hit.collider is CharacterController
                   || hit.collider.GetComponentInParent<INpc>() != null;
        }
    }
}
