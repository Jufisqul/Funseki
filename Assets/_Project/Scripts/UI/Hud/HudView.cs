using Funseki.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Funseki.UI
{
    // The in-game HUD of a day scene (GDD 8.2), built in code on its own canvas from HudTheme (look) and HudSettings
    // (behavior). Only the corners and edges are used, the center of the screen stays free:
    //   top-left     time-of-day icon over «День N», the break goal under it        (OnPhaseStarted, OnObjectiveChanged)
    //   top-right    «Шум» megaphone; under it «Новая запись в журнале»            (OnNoiseChanged, OnPrankDone)
    //   bottom-left  «Партия»: three portraits with the Q cooldown                 (OnHeroSwitched, IHeroStatus)
    //   bottom-mid   first-time control hints, above the inventory row             (HudSettings.hints)
    // Shown only in HudSettings.visibleStates (Break): hidden in cutscenes, dialogues, lessons, «Поймали».
    // No health or mana bars by design.
    public class HudView : MonoBehaviour
    {
        [SerializeField] HudTheme theme;
        [SerializeField] HudSettings settings;

        /// <summary>The HUD of the loaded day scene, if any (GameStateDebugLabel hides itself while it exists).</summary>
        public static HudView Active { get; private set; }

        CanvasGroup group;
        HudDayBlock day;
        HudPartyBlock party;
        HudNoiseBlock noise;
        HudJournalToast toast;
        HudHints hints;
        bool visible;
        GameState state = GameState.None;
        bool padShown;

        void Awake()
        {
            if (theme == null || settings == null)
            {
                Debug.LogError("[HUD] HudTheme or HudSettings is not assigned.", this);
                enabled = false;
                return;
            }
            Build();
            Active = this;
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        void OnEnable()
        {
            GameEvents.OnGameStateChanged += OnStateChanged;
            GameEvents.OnPhaseStarted += OnPhaseStarted;
            GameEvents.OnObjectiveChanged += OnObjectiveChanged;
            GameEvents.OnHeroSwitched += OnHeroSwitched;
            GameEvents.OnNoiseChanged += OnNoiseChanged;
            GameEvents.OnPrankDone += OnPrankDone;
            GameEvents.OnInteractionTargetChanged += OnInteractionTargetChanged;
            GameEvents.OnItemAdded += OnItemAdded;
        }

        void OnDisable()
        {
            GameEvents.OnGameStateChanged -= OnStateChanged;
            GameEvents.OnPhaseStarted -= OnPhaseStarted;
            GameEvents.OnObjectiveChanged -= OnObjectiveChanged;
            GameEvents.OnHeroSwitched -= OnHeroSwitched;
            GameEvents.OnNoiseChanged -= OnNoiseChanged;
            GameEvents.OnPrankDone -= OnPrankDone;
            GameEvents.OnInteractionTargetChanged -= OnInteractionTargetChanged;
            GameEvents.OnItemAdded -= OnItemAdded;
        }

        void Start()
        {
            // Catch up with what happened before this object woke up (the scene's start state, a loaded game).
            if (ServiceLocator.TryGet<GameStateMachine>(out var fsm)) state = fsm.Current;
            if (ServiceLocator.TryGet<IDayCycle>(out var cycle))
            {
                day.SetDay(cycle.Day);
                if (cycle.CurrentPhase != null) day.SetTime(cycle.CurrentPhase.timeOfDay);
            }
            if (ServiceLocator.TryGet<INoiseMeter>(out var meter)) noise.Init(meter.Value, meter.Max, meter.Stage);
            if (HeroService.HasRoster) party.SetCurrent(HeroService.Current);
            UpdateKeyLabels();
            visible = settings.IsVisibleIn(state);
            group.alpha = visible ? 1f : 0f;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (HudKeys.UpdateLastDevice())
            {
                // The player picked up the gamepad or went back to the keyboard: relabel the keys.
                hints.RefreshDevice();
                UpdateKeyLabels();
            }
            group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, dt / Mathf.Max(0.01f, settings.fadeTime));
            day.Tick(dt);
            party.Tick(dt);
            noise.Tick(dt);
            if (visible) toast.Tick(dt);
            // Hints count game time: they wait while the hero switch window pauses the game.
            hints.Tick(Time.deltaTime, visible && group.alpha > 0.5f, state == GameState.Break);
        }

        // ---------------------------------------------------------------- events

        void OnStateChanged(GameState previous, GameState current)
        {
            state = current;
            if (settings.KeepsIn(current)) return;
            visible = settings.IsVisibleIn(current);
        }

        void OnPhaseStarted(DayPhase phase)
        {
            if (ServiceLocator.TryGet<IDayCycle>(out var cycle)) day.SetDay(cycle.Day);
            day.SetTime(phase.timeOfDay);
        }

        void OnObjectiveChanged(string text) => day.SetGoal(text, false);

        void OnHeroSwitched(GameObject previous, GameObject current, float time)
        {
            if (HeroService.TryGetId(current, out var id)) party.SetCurrent(id);
            if (previous != null) hints.OnHeroSwitched();
        }

        void OnNoiseChanged(float value, NoiseStage stage) => noise.SetNoise(value, stage);

        void OnPrankDone(IPrank prank, Vector3 position) => toast.Show(prank != null ? prank.Title : "");

        void OnInteractionTargetChanged(GameObject hero, GameObject target)
        {
            if (target != null) hints.OnInteractionTarget();
        }

        void OnItemAdded(ItemData item) => hints.OnItemAdded();

        void UpdateKeyLabels()
        {
            padShown = HudKeys.UseGamepad;
            var map = settings.actions != null ? settings.actions.FindActionMap(settings.actionMap) : null;
            var ability = map?.FindAction("HeroAbility");
            string label = null;
            foreach (var h in settings.hints)
                if (h != null && h.action == "HeroAbility") label = padShown ? h.gamepadLabel : h.keyboardLabel;
            if (string.IsNullOrEmpty(label)) label = HudKeys.DisplayName(ability, padShown);
            party.SetAbilityKey(label);
        }

        // ---------------------------------------------------------------- build

        void Build()
        {
            var canvasGo = new GameObject("HudCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = theme.sortingOrder;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = theme.referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = theme.matchWidthOrHeight;
            group = canvasGo.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            group.alpha = 0f;

            var root = canvasGo.transform;
            day = new HudDayBlock(root, theme, settings);
            party = new HudPartyBlock(root, theme);
            noise = new HudNoiseBlock(root, theme, settings);
            toast = new HudJournalToast(root, theme, settings, theme.margin.y + theme.noiseIconSize + theme.noiseBarSize.y + 30f);
            hints = new HudHints(root, theme, settings);
        }
    }
}
