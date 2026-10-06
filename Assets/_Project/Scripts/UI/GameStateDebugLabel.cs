using Funseki.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Funseki.UI
{
    // Dev-only overlay in the corner: current GameState, day phase, objective, the last bell and the «Шум» meter.
    // Lives on the Bootstrap object. Hidden while a HudView is in the scene (it shows the same things for real);
    // F1 in the editor or a dev build shows it anyway.
    public class GameStateDebugLabel : MonoBehaviour
    {
        [SerializeField] bool show = true;

        GameState state = GameState.None;
        string phase = "-";
        string objective = "";
        string bell = "-";
        float noise;
        NoiseStage noiseStage;
        GUIStyle style;
        bool forceShow;

        void OnEnable()
        {
            GameEvents.OnGameStateChanged += Handle;
            GameEvents.OnPhaseStarted += HandlePhase;
            GameEvents.OnObjectiveChanged += HandleObjective;
            GameEvents.OnBell += HandleBell;
            GameEvents.OnNoiseChanged += HandleNoise;
        }

        void OnDisable()
        {
            GameEvents.OnGameStateChanged -= Handle;
            GameEvents.OnPhaseStarted -= HandlePhase;
            GameEvents.OnObjectiveChanged -= HandleObjective;
            GameEvents.OnBell -= HandleBell;
            GameEvents.OnNoiseChanged -= HandleNoise;
        }

        void Handle(GameState previous, GameState current) => state = current;
        void HandlePhase(DayPhase p) => phase = $"{p.type} '{p.id}' ({p.timeOfDay})";
        void HandleObjective(string text) => objective = text;
        void HandleBell(BellType type) => bell = $"{type} at {Time.time:0} s";
        void HandleNoise(float value, NoiseStage stage) { noise = value; noiseStage = stage; }

        void Update()
        {
            if (Debug.isDebugBuild && Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame) forceShow = !forceShow;
        }

        void OnGUI()
        {
            if (!show || !Debug.isDebugBuild) return;
            if (HudView.Active != null && !forceShow) return;
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 14 };
            GUI.Label(new Rect(10, Screen.height - 118, 900, 24), NoiseLine(), style);
            GUI.Label(new Rect(10, Screen.height - 88, 900, 24), $"Phase: {phase}   Bell: {bell}", style);
            GUI.Label(new Rect(10, Screen.height - 58, 900, 24), $"Objective: {objective}", style);
            GUI.Label(new Rect(10, Screen.height - 28, 400, 24), $"GameState: {state}", style);
        }

        string NoiseLine()
        {
            string stage = noiseStage switch
            {
                NoiseStage.Suspicious => "Подозрительно",
                NoiseStage.Caught => "Поймали",
                _ => "Тихо",
            };
            string extra = "";
            if (ServiceLocator.TryGet<INoiseMeter>(out var meter))
            {
                noise = meter.Value;
                if (meter.HeroSeen) extra += "   [на виду]";
                if (meter.HeroHidden) extra += "   [укрытие]";
            }
            return $"Шум: {noise:0} / 100 ({stage}){extra}";
        }
    }
}
