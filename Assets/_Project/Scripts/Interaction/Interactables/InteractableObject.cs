using System.Collections.Generic;
using Funseki.Core;
using UnityEngine;

namespace Funseki.Interaction
{
    // Base of the slice's world objects (GDD 5.9): E with the prompt from InteractableData, the repeat rule,
    // first-time / repeat / teacher-nearby lines per hero through IBarkService, world flags and «Шум».
    // On its own it is an «inspect» object that only talks; WaterTap, VendingMachine, RyutaLocker, DrawablePoster
    // and BookletRack add their behaviour by overriding OnUsed / RestoreState.
    //
    // State lives in WorldFlags under obj_<objectId>_<name> (uses, day and whatever the object adds), so it goes into
    // the save with the world and comes back when the scene loads (RestoreState in Start).
    public class InteractableObject : MonoBehaviour, IInteractable
    {
        [Tooltip("Unique snake_case id of this object in the game; the save keys hang on it")]
        [SerializeField] protected string objectId;
        [SerializeField] protected InteractableData data;

        static WorldFlags standaloneFlags;
        static readonly Collider[] nearby = new Collider[64];
        static readonly HashSet<INpc> seen = new();

        public string ObjectId => objectId;
        public InteractableData Data => data;

        /// <summary>How many times E was used on this object (whole game).</summary>
        public int Uses => Flags.GetCounter(Key("uses"));

        protected static WorldFlags Flags =>
            ServiceLocator.TryGet<WorldFlags>(out var f) ? f : standaloneFlags ??= new WorldFlags();

        protected static int CurrentDay => ServiceLocator.TryGet<IDayCycle>(out var day) ? day.Day : 1;

        protected string Key(string name) => $"obj_{objectId}_{name}";

        // ---- IInteractable
        public virtual string Prompt => data.prompt;

        public bool CanInteract(GameObject hero)
        {
            if (data == null) return false;
            if (!string.IsNullOrEmpty(data.requiredFlag) && !Flags.GetFlag(data.requiredFlag)) return false;
            if (!string.IsNullOrEmpty(data.blockedByFlag) && Flags.GetFlag(data.blockedByFlag)) return false;
            switch (data.repeat)
            {
                case InteractRepeat.Once when Uses > 0: return false;
                case InteractRepeat.OncePerDay when Uses > 0 && Flags.GetCounter(Key("day")) == CurrentDay: return false;
            }
            return CanUse(hero);
        }

        public void Interact(GameObject hero)
        {
            bool first = Uses == 0;
            Flags.AddCounter(Key("uses"));
            Flags.SetCounter(Key("day"), CurrentDay);
            if (first && data.flagsOnFirstUse != null)
                foreach (var f in data.flagsOnFirstUse) if (!string.IsNullOrEmpty(f)) Flags.SetFlag(f);
            if (data.countersOnUse != null)
                foreach (var c in data.countersOnUse) if (!string.IsNullOrEmpty(c)) Flags.AddCounter(c);

            var witness = FindWatchingTeacher(hero);
            if (witness != null && data.noiseAmount > 0)
                GameEvents.RaiseNoiseMade(hero, witness, data.noiseReason, data.noiseAmount);

            OnUsed(hero, first, witness);
        }

        // ---- for subclasses
        protected virtual void Start() => RestoreState();

        /// <summary>Extra conditions on top of the repeat rule and flags (a jammed machine, an open view).</summary>
        protected virtual bool CanUse(GameObject hero) => true;

        /// <summary>What E does. Default: says the first-time / repeat / teacher line.</summary>
        protected virtual void OnUsed(GameObject hero, bool first, GameObject witness) => SayUsualLine(hero, first, witness);

        /// <summary>Puts the visuals in line with the saved state (called in Start, after a load the scene starts anew).</summary>
        protected virtual void RestoreState() { }

        /// <summary>The teacher line if a teacher watches and the data has one, otherwise the hero's first / repeat line.</summary>
        protected void SayUsualLine(GameObject hero, bool first, GameObject witness)
        {
            if (witness != null && data.teacherNearbyLines != null && data.teacherNearbyLines.Length > 0)
            {
                Say(hero, InteractableData.Pick(data.teacherNearbyLines));
                return;
            }
            var id = HeroOf(hero);
            Say(hero, InteractableData.Pick(first ? data.FirstLines(id) : data.RepeatLines(id)));
        }

        /// <summary>A bark over the hero (BarkService); hero null = the hero the player controls.</summary>
        protected static void Say(GameObject hero, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (hero == null) hero = HeroService.CurrentObject;
            if (hero != null && ServiceLocator.TryGet<IBarkService>(out var barks)) barks.Say(hero, text);
            else GameEvents.RaiseHeroBark(hero, text);
        }

        protected static HeroId HeroOf(GameObject hero) =>
            hero != null && HeroService.TryGetId(hero, out var id) ? id : HeroService.Current;

        protected void Report(GameObject hero, string action) => GameEvents.RaiseObjectUsed(hero, gameObject, objectId, action);

        /// <summary>A teacher within data.teacherRadius who can see the hero right now, or null.</summary>
        protected GameObject FindWatchingTeacher(GameObject hero)
        {
            if (hero == null) hero = HeroService.CurrentObject;
            Vector3 center = hero != null ? hero.transform.position : transform.position;
            Vector3 chest = center + Vector3.up * 1.2f;
            int n = Physics.OverlapSphereNonAlloc(center, data.teacherRadius, nearby, ~0, QueryTriggerInteraction.Collide);
            seen.Clear();
            for (int i = 0; i < n; i++)
            {
                var npc = nearby[i].GetComponentInParent<INpc>();
                if (npc == null || !npc.IsTeacher || !seen.Add(npc)) continue;
                if (npc.CanSee(chest)) return ((Component)npc).gameObject;
            }
            return null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => standaloneFlags = null;

#if UNITY_EDITOR
        void OnValidate()
        {
            if (string.IsNullOrEmpty(objectId)) objectId = name.ToLowerInvariant().Replace(' ', '_');
        }
#endif
    }
}
