using System.Collections.Generic;
using Funseki.Core;
using UnityEngine;

namespace Funseki.Heroes
{
    // Рюта «Пинок» (GDD 5.2): the nearest NPC (or IKickable object: the vending machine) in front of him within reach
    // gets GameEvents.OnKicked(target, hero); the target makes its own reaction. If a teacher sees it, GameEvents.OnNoiseMade goes to the «Шум» meter.
    [CreateAssetMenu(fileName = "Ability_Kick", menuName = "Funseki/Heroes/Abilities/Kick (Ryuta)")]
    public class KickAbility : HeroAbility
    {
        public const string AnimationTrigger = "Kick";
        static readonly int KickTrigger = Animator.StringToHash(AnimationTrigger);
        [Header("Kick")]
        [Tooltip("m")]
        public float reach = 1.5f;
        [Tooltip("Width of the cone in front of Рюта, degrees")]
        [Range(10f, 360f)] public float coneAngle = 120f;
        [Tooltip("Height of Рюта's chest for the check, m")]
        public float chestHeight = 1f;
        public LayerMask npcLayers = ~0;
        [Tooltip("Bark when nobody is in reach (the kick still happens); empty = silent")]
        public string missLine = "";

        [Header("Witnesses")]
        [Tooltip("Teachers farther than this don't notice anything, m")]
        public float witnessRange = 30f;
        [Tooltip("Reason id of the noise event")]
        public string noiseReason = "kick";
        public int noiseAmount = 1;

        static readonly Collider[] Hits = new Collider[64];
        static readonly HashSet<Component> Seen = new();

        public override bool TryUse(HeroAbilityContext ctx)
        {
            var animator = ctx.Hero.GetComponentInChildren<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
                foreach (var parameter in animator.parameters)
                    if (parameter.nameHash == KickTrigger && parameter.type == AnimatorControllerParameterType.Trigger)
                    {
                        animator.SetTrigger(KickTrigger);
                        break;
                    }
            var hero = ctx.Hero.transform;
            var npc = FindTarget(hero);
            if (npc == null)
            {
                ctx.Bark(missLine);
                return true;
            }

            GameEvents.RaiseKicked(npc.gameObject, ctx.Hero);
            ReportWitnesses(ctx.Hero);
            return true;
        }

        Component FindTarget(Transform hero)
        {
            Vector3 chest = hero.position + Vector3.up * chestHeight;
            Vector3 forward = Vector3.ProjectOnPlane(hero.forward, Vector3.up).normalized;
            // Triggers too: a kickable object may be marked by a trigger box around it.
            int n = Physics.OverlapSphereNonAlloc(chest, reach, Hits, npcLayers, QueryTriggerInteraction.Collide);
            Component best = null;
            float bestDist = float.MaxValue;
            Seen.Clear();
            for (int i = 0; i < n; i++)
            {
                var npc = Hits[i].GetComponentInParent<INpc>() as Component ?? Hits[i].GetComponentInParent<IKickable>() as Component;
                if (npc == null || !Seen.Add(npc) || npc.transform.IsChildOf(hero)) continue;
                Vector3 to = Vector3.ProjectOnPlane(Hits[i].bounds.ClosestPoint(chest) - hero.position, Vector3.up);
                if (to.sqrMagnitude > 0.0001f && Vector3.Angle(forward, to) > coneAngle * 0.5f) continue;
                float d = to.sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = npc; }
            }
            return best;
        }

        void ReportWitnesses(GameObject hero)
        {
            Vector3 seenPoint = hero.transform.position + Vector3.up * chestHeight;
            // Allocating query: within 30 m of a school there are far more colliders than any fixed buffer.
            var hits = Physics.OverlapSphere(hero.transform.position, witnessRange, ~0, QueryTriggerInteraction.Ignore);
            Seen.Clear();
            foreach (var hit in hits)
            {
                var npc = hit.GetComponentInParent<INpc>();
                if (npc is not Component c || !Seen.Add(c) || !npc.IsTeacher) continue;
                if (!npc.CanSee(seenPoint)) continue;
                GameEvents.RaiseNoiseMade(hero, c.gameObject, noiseReason, noiseAmount);
            }
        }
    }
}
