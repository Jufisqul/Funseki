using System.Collections.Generic;
using Funseki.Core;
using UnityEngine;

namespace Funseki.Pranks
{
    // Who watches the heroes: teachers (INpc.IsTeacher) that can see a point right now.
    public static class Watchers
    {
        static readonly HashSet<INpc> Seen = new();

        /// <summary>The closest teacher within radius that can see point, or null.</summary>
        public static GameObject FindWatching(Vector3 center, float radius, Vector3 point)
        {
            // Allocating query: around a school there are more colliders than a fixed buffer holds.
            var hits = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Collide);
            Seen.Clear();
            GameObject best = null;
            float bestDist = float.MaxValue;
            foreach (var hit in hits)
            {
                var npc = hit.GetComponentInParent<INpc>();
                if (npc == null || !npc.IsTeacher || !Seen.Add(npc) || !npc.CanSee(point)) continue;
                var go = ((Component)npc).gameObject;
                float d = (go.transform.position - center).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = go; }
            }
            return best;
        }
    }
}
