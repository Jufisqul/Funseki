using Funseki.Core;
using UnityEngine;

namespace Funseki.Story
{
    // The school fence (CLAUDE.md «Полный День 1», step 2): the yard is open, beyond the fence is not.
    // Solid walls stop the hero; when the leader steps into one of the trigger boxes in front of them,
    // he says Day1StartData.fenceLine (no more often than fenceLineCooldown).
    public class StoryBoundary : MonoBehaviour
    {
        [SerializeField] Day1StartData data;
        [Tooltip("Trigger boxes along the fence, just inside it")]
        [SerializeField] BoxCollider[] lineZones;

        float nextLineAt;

        void Update()
        {
            if (Time.time < nextLineAt) return;
            var hero = HeroService.CurrentObject;
            if (hero == null) return;
            Vector3 p = hero.transform.position + Vector3.up * 0.5f;
            foreach (var zone in lineZones)
            {
                if (zone == null || !zone.bounds.Contains(p)) continue;
                GameEvents.RaiseHeroBark(hero, data.fenceLine);
                nextLineAt = Time.time + data.fenceLineCooldown;
                return;
            }
        }
    }
}
