using Funseki.Core;
using UnityEngine;

namespace Funseki.Heroes
{
    // Рэй «Телефон» (GDD 5.2). Q, once per break: a call with a rumor (a short dialogue from the RumorSet whose flag
    // conditions pass). Passive: now and then a "MMS with a meme" comes in, only a sound and a bark.
    [CreateAssetMenu(fileName = "Ability_Phone", menuName = "Funseki/Heroes/Abilities/Phone (Rei)")]
    public class PhoneAbility : HeroAbility
    {
        [Header("Call")]
        public RumorSet rumors;
        [Tooltip("Said when no rumor is available; the use is not spent")]
        public string noRumorLine = "Никто не берёт трубку.";
        [Tooltip("Played when the call starts; empty = a synthesized beep")]
        public AudioClip dialClip;

        [Header("Incoming memes")]
        [Tooltip("Random pause between two memes, s")]
        public Vector2 memeInterval = new(45f, 110f);
        [Tooltip("Game states in which memes come in")]
        public GameState[] memeStates = { GameState.Break };
        [Tooltip("Notification sound; empty = a synthesized double beep")]
        public AudioClip memeClip;
        [Range(0f, 1f)] public float memeVolume = 0.8f;
        public string[] memeLines =
        {
            "Ха! Кот в ведре. Опять.",
            "Кто прислал мем про физрука?..",
            "ММС грузится уже минуту. Это точно мем.",
        };

        static AudioClip beep;

        public override bool TryUse(HeroAbilityContext ctx)
        {
            var rumor = rumors != null ? rumors.Pick() : null;
            if (rumor == null || !ServiceLocator.TryGet<IDialogueService>(out var dialogue) || dialogue.IsRunning)
            {
                ctx.Bark(noRumorLine);
                return false;
            }

            PlaySound(ctx, dialClip, memeVolume);
            if (!dialogue.StartDialogue(rumor.dialogue, null, ctx.Hero)) return false;
            if (!string.IsNullOrEmpty(rumor.heardFlag) && ServiceLocator.TryGet<WorldFlags>(out var flags))
                flags.SetFlag(rumor.heardFlag);
            return true;
        }

        public override void Tick(HeroAbilityContext ctx, float deltaTime)
        {
            if (ctx.PassiveTimer < 0f) ctx.PassiveTimer = Random.Range(memeInterval.x, memeInterval.y);
            if (!ServiceLocator.TryGet<GameStateMachine>(out var fsm) || System.Array.IndexOf(memeStates, fsm.Current) < 0) return;

            ctx.PassiveTimer -= deltaTime;
            if (ctx.PassiveTimer > 0f) return;
            ctx.PassiveTimer = Random.Range(memeInterval.x, memeInterval.y);
            PlaySound(ctx, memeClip, memeVolume);
            ctx.Bark(HeroAbilityContext.PickLine(memeLines));
        }

        static void PlaySound(HeroAbilityContext ctx, AudioClip clip, float volume)
        {
            var source = ctx.Runner.GetComponent<AudioSource>();
            if (source == null)
            {
                source = ctx.Runner.gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0.6f;
            }
            source.PlayOneShot(clip != null ? clip : Beep(), volume);
        }

        // Two short notification beeps, so the phone is audible without sound assets.
        static AudioClip Beep()
        {
            if (beep != null) return beep;
            const int rate = 44100;
            int n = (int)(rate * 0.32f);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                bool on = t < 0.12f || (t > 0.18f && t < 0.30f);
                float freq = t < 0.15f ? 1320f : 1760f;
                data[i] = on ? Mathf.Sin(2f * Mathf.PI * freq * t) * 0.25f : 0f;
            }
            beep = AudioClip.Create("PhoneBeep", n, 1, rate, false);
            beep.SetData(data, 0);
            return beep;
        }
    }
}
