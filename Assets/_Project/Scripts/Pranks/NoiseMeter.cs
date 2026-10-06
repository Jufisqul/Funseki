using Funseki.Core;
using UnityEngine;

namespace Funseki.Pranks
{
    // The «Шум» meter (GDD 5.4), 0–100: «Тихо» (0–39) / «Подозрительно» (40–99) / «Поймали» (100).
    // Grows only through GameEvents.OnNoiseMade (sources send it when a teacher saw the mischief: a prank, a kick,
    // the marker on the poster; NoiseSettings can override the amount per reason). Falls while no teacher sees the
    // controlled hero, faster in a HideZone. At the top it raises GameEvents.OnCaught with the last witness.
    // Registered as INoiseMeter; one per day scene.
    public class NoiseMeter : MonoBehaviour, INoiseMeter
    {
        [SerializeField] NoiseSettings settings;

        float value;
        float lastNoiseAt = -1000f;
        float nextCheck;
        int lastSentValue = -1;
        NoiseStage lastSentStage;
        GameObject lastWitness;
        bool caughtRaised;
        bool registered;

        public float Value => value;
        public float Max => settings.max;
        public NoiseStage Stage => settings.StageOf(value);
        public bool HeroSeen { get; private set; }
        public bool HeroHidden { get; private set; }

        void Awake()
        {
            if (ServiceLocator.IsRegistered<INoiseMeter>())
            {
                Debug.LogWarning("[Noise] Another noise meter is already registered; this one is off.", this);
                enabled = false;
                return;
            }
            ServiceLocator.Register<INoiseMeter>(this);
            registered = true;
        }

        void OnDestroy()
        {
            if (registered && ServiceLocator.TryGet<INoiseMeter>(out var m) && ReferenceEquals(m, this))
                ServiceLocator.Unregister<INoiseMeter>();
        }

        void OnEnable()
        {
            GameEvents.OnNoiseMade += OnNoiseMade;
            GameEvents.OnCaughtEnded += OnCaughtEnded;
        }

        void OnDisable()
        {
            GameEvents.OnNoiseMade -= OnNoiseMade;
            GameEvents.OnCaughtEnded -= OnCaughtEnded;
        }

        void Start() => Send(true);

        void OnNoiseMade(GameObject hero, GameObject witness, string reason, int amount)
        {
            if (!enabled || !IsActive()) return;
            int add = settings.AmountFor(reason, amount);
            if (add <= 0) return;
            lastWitness = witness;
            lastNoiseAt = Time.time;
            Debug.Log($"[Noise] +{add} ({reason}, seen by {(witness != null ? witness.name : "?")}).");
            SetValue(value + add);
        }

        void OnCaughtEnded(GameObject hero) => ResetNoise();

        public void ResetNoise()
        {
            caughtRaised = false;
            lastWitness = null;
            SetValue(0f);
        }

        void Update()
        {
            if (!IsActive()) return;

            if (Time.time >= nextCheck)
            {
                nextCheck = Time.time + settings.visibilityCheckInterval;
                CheckHero();
            }

            if (value > 0f && !HeroSeen && Time.time - lastNoiseAt >= settings.decayDelay)
            {
                float rate = settings.decayPerSecond * (HeroHidden ? settings.hideZoneMultiplier : 1f);
                SetValue(value - rate * Time.deltaTime);
            }
        }

        void CheckHero()
        {
            var hero = HeroService.CurrentObject;
            if (hero == null) { HeroSeen = HeroHidden = false; return; }
            Vector3 feet = hero.transform.position;
            Vector3 chest = feet + Vector3.up * settings.heroChestHeight;
            var watcher = Watchers.FindWatching(feet, settings.watcherSearchRadius, chest);
            HeroSeen = watcher != null;
            if (HeroSeen) lastWitness = watcher;
            HeroHidden = HideZone.Find(feet + Vector3.up * 0.5f) != null;
        }

        void SetValue(float v)
        {
            value = Mathf.Clamp(v, 0f, settings.max);
            Send(false);
            if (value >= settings.max && !caughtRaised)
            {
                caughtRaised = true;
                var hero = HeroService.CurrentObject;
                Debug.Log($"[Noise] Full: caught by {(lastWitness != null ? lastWitness.name : "?")}.");
                GameEvents.RaiseCaught(hero, lastWitness, settings.defaultCaughtScene);
            }
        }

        void Send(bool force)
        {
            int rounded = Mathf.RoundToInt(value);
            var stage = Stage;
            if (!force && rounded == lastSentValue && stage == lastSentStage) return;
            if (stage != lastSentStage && !force) Debug.Log($"[Noise] Stage: {stage} ({rounded}).");
            lastSentValue = rounded;
            lastSentStage = stage;
            GameEvents.RaiseNoiseChanged(value, stage);
        }

        bool IsActive() =>
            !ServiceLocator.TryGet<GameStateMachine>(out var fsm) || settings.IsActive(fsm.Current);
    }
}
