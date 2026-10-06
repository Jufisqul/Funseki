using System.Collections;
using Funseki.Core;
using UnityEngine;
using UnityEngine.Playables;

namespace Funseki.Pranks
{
    // The bait of a prank (GDD 5.4): E on it (IInteractable) or the required item on it (IItemTarget) does the prank
    // from PrankData. A teacher who sees it adds «Шум» or, with seenMeansCaught, catches the hero (GameEvents.OnCaught).
    // Otherwise: done/consequence flags and counters, a journal entry, the reward item, the hero's line,
    // GameEvents.OnPrankDone(prank, position) for the NPCs around, and the world reaction Timeline on reactionDirector.
    // The bait can be hidden and shown by its scene routine (SetAvailable: the whistle is on the bench only while the
    // PE teacher fights the machine). Done is remembered by the prank's done flag, so a loaded save hides the bait.
    public class PrankTrigger : MonoBehaviour, IInteractable, IItemTarget
    {
        [SerializeField] PrankData data;
        [Tooltip("Plays data.reactionTimeline with this scene's bindings; empty = no reaction Timeline")]
        [SerializeField] PlayableDirector reactionDirector;
        [Tooltip("The bait's visuals; hidden while unavailable and after the prank")]
        [SerializeField] GameObject[] visuals;
        [SerializeField] bool startAvailable = true;

        static WorldFlags standaloneFlags;

        bool available;
        bool done;

        public PrankData Data => data;
        public bool Available => available && !done;
        public bool Done => done;

        static WorldFlags Flags => ServiceLocator.TryGet<WorldFlags>(out var f) ? f : standaloneFlags ??= new WorldFlags();

        void Start()
        {
            done = data != null && Flags.GetFlag(data.DoneFlag);
            SetAvailable(startAvailable);
        }

        /// <summary>Shows or hides the bait; a done prank stays hidden.</summary>
        public void SetAvailable(bool value)
        {
            available = value;
            bool show = available && !done;
            if (visuals != null)
                foreach (var v in visuals) if (v != null) v.SetActive(show);
            foreach (var c in GetComponents<Collider>()) c.enabled = show;
        }

        // ---------------------------------------------------------------- IInteractable

        public string Prompt => data != null ? data.prompt : "";

        public bool CanInteract(GameObject hero) =>
            Available && data.requiredItem == null && data.ConditionsMet(HeroOf(hero), Flags, out _);

        public void Interact(GameObject hero) => Perform(hero);

        // ---------------------------------------------------------------- IItemTarget

        public bool UseItem(ItemData item, bool primary)
        {
            if (!Available || data.requiredItem == null || item != data.requiredItem || !primary) return false;
            var hero = HeroService.CurrentObject;
            if (!data.ConditionsMet(HeroOf(hero), Flags, out _)) return false;
            Perform(hero);
            return true;
        }

        // ---------------------------------------------------------------- the prank

        void Perform(GameObject hero)
        {
            if (hero == null) hero = HeroService.CurrentObject;
            Vector3 feet = hero != null ? hero.transform.position : transform.position;
            var witness = Watchers.FindWatching(feet, data.watchRadius, feet + Vector3.up * 1.2f);

            if (witness != null)
            {
                if (data.seenMeansCaught)
                {
                    Debug.Log($"[Prank] '{data.id}': {witness.name} saw it, caught.", this);
                    GameEvents.RaiseCaught(hero, witness, data.caughtScene);
                    return;
                }
                if (data.noiseWhenSeen > 0) GameEvents.RaiseNoiseMade(hero, witness, "prank", data.noiseWhenSeen);
            }

            done = true;
            SetAvailable(false);
            var flags = Flags;
            flags.SetFlag(data.DoneFlag);
            SetAll(flags, data.consequenceFlags);
            if (data.counters != null)
                foreach (var c in data.counters) if (!string.IsNullOrEmpty(c)) flags.AddCounter(c);

            if (ServiceLocator.TryGet<IWeekJournal>(out var journal))
                journal.Add(new JournalEntry
                {
                    id = data.id,
                    kind = JournalEntryKind.Prank,
                    title = data.title,
                    caption = data.journalCaption,
                    day = ServiceLocator.TryGet<IDayCycle>(out var day) ? day.Day : 1,
                    hero = (int)HeroOf(hero),
                });
            if (data.rewardItem != null) GameEvents.RaiseItemGiven(data.rewardItem);

            Say(hero, PrankData.Pick(data.successLines));
            Debug.Log($"[Prank] '{data.id}' done by {(hero != null ? hero.name : "?")}{(witness != null ? $", seen by {witness.name}" : "")}.", this);
            GameEvents.RaisePrankDone(data, transform.position);
            StartCoroutine(React());
        }

        IEnumerator React()
        {
            float duration = 0f;
            if (reactionDirector != null && data.reactionTimeline != null)
            {
                if (reactionDirector.playableAsset != data.reactionTimeline) reactionDirector.playableAsset = data.reactionTimeline;
                reactionDirector.time = 0;
                reactionDirector.Play();
                duration = (float)data.reactionTimeline.duration;
            }
            if (duration > 0f) yield return new WaitForSeconds(duration);
            SetAll(Flags, data.flagsAfterReaction);
        }

        static void SetAll(WorldFlags flags, string[] ids)
        {
            if (ids == null) return;
            foreach (var f in ids) if (!string.IsNullOrEmpty(f)) flags.SetFlag(f);
        }

        static void Say(GameObject hero, string text)
        {
            if (string.IsNullOrEmpty(text) || hero == null) return;
            if (ServiceLocator.TryGet<IBarkService>(out var barks)) barks.Say(hero, text);
            else GameEvents.RaiseHeroBark(hero, text);
        }

        static HeroId HeroOf(GameObject hero) =>
            hero != null && HeroService.TryGetId(hero, out var id) ? id : HeroService.Current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => standaloneFlags = null;
    }
}
