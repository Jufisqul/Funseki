using UnityEngine;

namespace Funseki.Core
{
    // Static shortcut to the IHeroRoster service so NPCs, items and dialogue can ask "who is playing now":
    // HeroService.Current (HeroId) and HeroService.CurrentObject (the leader's GameObject).
    // Without a roster (test scenes with a single hero) every hero counts as controlled.
    public static class HeroService
    {
        public static bool HasRoster => ServiceLocator.IsRegistered<IHeroRoster>();

        /// <summary>The controlled hero; Ryuta when there is no roster (use HasRoster to tell).</summary>
        public static HeroId Current => ServiceLocator.TryGet<IHeroRoster>(out var r) ? r.Current : HeroId.Ryuta;

        /// <summary>The leader object; null without a roster.</summary>
        public static GameObject CurrentObject => ServiceLocator.TryGet<IHeroRoster>(out var r) ? r.CurrentObject : null;

        public static GameObject GetHero(HeroId id) => ServiceLocator.TryGet<IHeroRoster>(out var r) ? r.GetHero(id) : null;

        /// <summary>Is this hero the leader (true for any hero when there is no roster)?</summary>
        public static bool IsLeader(GameObject hero) =>
            !ServiceLocator.TryGet<IHeroRoster>(out var r) || r.CurrentObject == hero;

        /// <summary>Should this hero react to the player's input now: the leader, and not during the switch hand-over.</summary>
        public static bool IsInControl(GameObject hero) =>
            !ServiceLocator.TryGet<IHeroRoster>(out var r) || (r.CurrentObject == hero && !r.IsSwitching);

        /// <summary>Which hero this object is (searches the parents); false for non-heroes or without a roster.</summary>
        public static bool TryGetId(GameObject obj, out HeroId id)
        {
            id = default;
            if (obj == null || !ServiceLocator.TryGet<IHeroRoster>(out var r)) return false;
            for (int i = 0; i <= (int)HeroId.Kaito; i++)
            {
                var hero = r.GetHero((HeroId)i);
                if (hero != null && obj.transform.IsChildOf(hero.transform)) { id = (HeroId)i; return true; }
            }
            return false;
        }
    }
}
