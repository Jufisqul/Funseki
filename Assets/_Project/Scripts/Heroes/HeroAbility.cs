using Funseki.Core;
using UnityEngine;

namespace Funseki.Heroes
{
    // A hero's unique action on Q (GDD 5.2). Cooldowns and limits live in the ability asset;
    // HeroAbilityRunner on the hero keeps the timers and calls TryUse / Tick.
    public interface IHeroAbility
    {
        string DisplayName { get; }
        /// <summary>Seconds after a successful use before Q works again.</summary>
        float Cooldown { get; }
        /// <summary>Uses allowed per day phase (one break); 0 = unlimited.</summary>
        int UsesPerBreak { get; }
        /// <summary>Do the action. False = nothing happened (no target...), the cooldown doesn't start.</summary>
        bool TryUse(HeroAbilityContext ctx);
        /// <summary>Every frame on every hero with this ability, leader or not (passive effects).</summary>
        void Tick(HeroAbilityContext ctx, float deltaTime);
    }

    // What an ability works with. One per hero, so it also keeps per-hero state of passive effects.
    public class HeroAbilityContext
    {
        public GameObject Hero;
        public HeroUnit Unit;
        public HeroAbilityRunner Runner;
        /// <summary>Free timer for an ability's passive effect (Рэй's incoming memes).</summary>
        public float PassiveTimer = -1f;

        public void Bark(string text)
        {
            if (!string.IsNullOrEmpty(text)) GameEvents.RaiseHeroBark(Hero, text);
        }

        public static string PickLine(string[] lines) =>
            lines == null || lines.Length == 0 ? null : lines[Random.Range(0, lines.Length)];
    }

    // Base of the ability assets (Assets/_Project/Data/Heroes/Abilities).
    public abstract class HeroAbility : ScriptableObject, IHeroAbility
    {
        [Tooltip("Shown on the hero's card in the switch window")]
        public string displayName;
        [TextArea(1, 3)] public string description;
        [Tooltip("s")]
        public float cooldown = 3f;
        [Tooltip("Uses per break (day phase); 0 = unlimited")]
        public int usesPerBreak;
        [Tooltip("Bark when Q is pressed during the cooldown; empty = silent")]
        public string cooldownLine;
        [Tooltip("Bark when the uses of this break are spent; empty = silent")]
        public string outOfUsesLine;

        public string DisplayName => displayName;
        public float Cooldown => cooldown;
        public int UsesPerBreak => usesPerBreak;

        public abstract bool TryUse(HeroAbilityContext ctx);
        public virtual void Tick(HeroAbilityContext ctx, float deltaTime) { }
    }
}
