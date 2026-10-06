using UnityEngine;

namespace Funseki.Core
{
    // The three playable heroes. Dialogue and bark conditions check which one is in control.
    public enum HeroId
    {
        Ryuta,  // Рюта
        Rei,    // Рэй
        Kaito   // Кайто: hears the "Голос" and gets its extra dialogue option
    }

    // Who the player controls right now. Registered in ServiceLocator by Funseki.Heroes.HeroParty (in the day scene);
    // read it through the static HeroService. Scenes without it (test scenes with one hero) fall back to defaults.
    public interface IHeroRoster
    {
        HeroId Current { get; }
        /// <summary>The hero object under the player's control (the leader).</summary>
        GameObject CurrentObject { get; }
        /// <summary>True for the short camera hand-over after a switch; nobody is controlled meanwhile.</summary>
        bool IsSwitching { get; }
        /// <summary>Null when that hero is not in the scene.</summary>
        GameObject GetHero(HeroId id);
    }
}
