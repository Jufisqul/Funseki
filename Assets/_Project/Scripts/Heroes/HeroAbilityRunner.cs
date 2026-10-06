using Funseki.Core;
using UnityEngine;

namespace Funseki.Heroes
{
    // On every hero. Keeps the cooldown and per-break uses of the hero's ability and ticks its passive effect.
    // HeroParty calls TryUse when the leader presses Q.
    // Also the IHeroStatus the HUD reads for the hero card.
    [RequireComponent(typeof(HeroUnit))]
    public class HeroAbilityRunner : MonoBehaviour, IHeroStatus
    {
        HeroUnit unit;
        HeroAbilityContext ctx;
        float readyAt;
        int usedThisBreak;

        public IHeroAbility Ability => unit.Data != null ? unit.Data.ability : null;
        public float CooldownLeft => Mathf.Max(0f, readyAt - Time.time);
        public HeroAbilityContext Context => ctx;

        // IHeroStatus: the HUD party block reads these.
        public HeroId Id => unit.Data != null ? unit.Data.id : default;
        public string ShortName => unit.Data != null ? unit.Data.shortName : name;
        public Sprite Portrait => unit.Data != null ? unit.Data.portrait : null;
        public Color Color => unit.Data != null ? unit.Data.color : Color.white;
        public float AbilityCooldown => Ability != null ? Ability.Cooldown : 0f;
        public float AbilityCooldownLeft => CooldownLeft;
        public int AbilityUsesLeft => Ability == null || Ability.UsesPerBreak <= 0 ? -1 : Mathf.Max(0, Ability.UsesPerBreak - usedThisBreak);

        void Awake()
        {
            unit = GetComponent<HeroUnit>();
            ctx = new HeroAbilityContext { Hero = gameObject, Unit = unit, Runner = this };
        }

        void OnEnable() => GameEvents.OnPhaseStarted += OnPhaseStarted;
        void OnDisable() => GameEvents.OnPhaseStarted -= OnPhaseStarted;

        // A new break (or any day phase) gives back the per-break uses.
        void OnPhaseStarted(DayPhase phase) => usedThisBreak = 0;

        void Update() => Ability?.Tick(ctx, Time.deltaTime);

        public bool TryUse()
        {
            var ability = Ability;
            if (ability == null) return false;
            var data = unit.Data.ability;

            if (Time.time < readyAt)
            {
                ctx.Bark(data.cooldownLine);
                return false;
            }
            if (ability.UsesPerBreak > 0 && usedThisBreak >= ability.UsesPerBreak)
            {
                ctx.Bark(data.outOfUsesLine);
                return false;
            }
            if (!ability.TryUse(ctx)) return false;

            readyAt = Time.time + ability.Cooldown;
            usedThisBreak++;
            GameEvents.RaiseHeroAbilityUsed(gameObject, unit.Data.id);
            return true;
        }
    }
}
