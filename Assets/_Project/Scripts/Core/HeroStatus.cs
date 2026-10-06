using UnityEngine;

namespace Funseki.Core
{
    // What the HUD shows on a hero's card: portrait, color and the state of the Q action.
    // Implemented by Funseki.Heroes.HeroAbilityRunner on every hero; found with
    // HeroService.GetHero(id).GetComponent<IHeroStatus>(), so UI never references the Heroes module.
    public interface IHeroStatus
    {
        HeroId Id { get; }
        string ShortName { get; }
        /// <summary>Null = no portrait (the HUD draws a colored card with the first letter).</summary>
        Sprite Portrait { get; }
        Color Color { get; }
        /// <summary>Full cooldown of the Q action, s; 0 when the hero has no action.</summary>
        float AbilityCooldown { get; }
        /// <summary>Seconds until Q works again; 0 = ready.</summary>
        float AbilityCooldownLeft { get; }
        /// <summary>Uses of Q left in this break; -1 = unlimited.</summary>
        int AbilityUsesLeft { get; }
    }
}
