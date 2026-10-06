using Funseki.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace Funseki.DayCycle
{
    // Plays the cutscene of a StoryScene phase (the day intro «Приезд в Фунсэки») when that phase starts:
    // fade in → the Timeline (its Activation tracks show the puppet heroes, the cutscene camera and the day title)
    // → at CutsceneData.dialogueAt the Timeline waits for the dialogue (IDialogueService) → the rest of the Timeline
    // → fade to black → the phase goal flag (or IDayCycle.CompletePhase) → the break starts and the screen fades back
    // in on the real heroes. Holding Cutscene/Skip (Space / A) for skipHoldTime skips it, except during the dialogue,
    // which has its own «Пропуск». Game time: the pause menu stops it.
    public class StoryCutscene : MonoBehaviour
    {
        public const string MapName = "Cutscene";

        [SerializeField] CutsceneData data;
        [SerializeField] PlayableDirector director;
        [SerializeField] InputActionAsset actions;
        [Tooltip("Puppets the dialogue camera frames: the speaking hero and the one listening")]
        [SerializeField] GameObject dialogueHero;
        [SerializeField] GameObject dialogueListener;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text subtitleText;

        enum Step { Idle, Playing, Dialogue, Ending, FadingBack }

        Step step = Step.Idle;
        DayPhase phase;
        InputAction skip;
        bool dialogueDone;
        float hold, fade, fadeTarget, fadeSpeed;
        bool hintShown;

        Canvas overlay;
        Image black, bar;
        TextMeshProUGUI hint;
        GameObject hintRoot;

        void Awake()
        {
            if (data == null || director == null)
            {
                Debug.LogError("[Cutscene] CutsceneData or PlayableDirector is not assigned.", this);
                enabled = false;
                return;
            }
            director.playOnAwake = false;
            director.timeUpdateMode = DirectorUpdateMode.GameTime;
            director.extrapolationMode = DirectorWrapMode.Hold;
            if (data.timeline != null) director.playableAsset = data.timeline;
            if (titleText != null) titleText.text = data.title;
            if (subtitleText != null) subtitleText.text = data.subtitle;
            skip = actions != null ? actions.FindActionMap(MapName, true).FindAction("Skip", true) : null;
            BuildOverlay();
        }

        void OnEnable()
        {
            GameEvents.OnPhaseStarted += OnPhaseStarted;
            GameEvents.OnPhaseEnded += OnPhaseEnded;
            GameEvents.OnDialogueEnded += OnDialogueEnded;
        }

        void OnDisable()
        {
            GameEvents.OnPhaseStarted -= OnPhaseStarted;
            GameEvents.OnPhaseEnded -= OnPhaseEnded;
            GameEvents.OnDialogueEnded -= OnDialogueEnded;
            skip?.Disable();
        }

        void OnDestroy()
        {
            if (overlay != null) Destroy(overlay.gameObject);
        }

        // ---------------------------------------------------------------- phase

        void OnPhaseStarted(DayPhase p)
        {
            if (p == null || p.id != data.phaseId || step != Step.Idle) return;
            phase = p;
            Play();
        }

        // The phase ended without us (F9): stop and hand over.
        void OnPhaseEnded(DayPhase p)
        {
            if (p == null || p != phase || step == Step.Idle || step == Step.FadingBack) return;
            Debug.Log("[Cutscene] The phase ended first; the cutscene stops.");
            StopTimeline();
            phase = null;
            BeginFadeBack();
        }

        void Play()
        {
            Debug.Log($"[Cutscene] {data.name}: playing ({(director.playableAsset != null ? director.duration : 0):0} s + dialogue).");
            step = Step.Playing;
            dialogueDone = data.dialogue == null;
            hold = 0f;
            hintShown = !data.hintOnlyAfterPress;
            skip?.Enable();
            overlay.gameObject.SetActive(true);
            SetFade(1f);
            FadeTo(0f, data.fadeIn);
            director.time = 0;
            if (director.playableAsset != null) director.Play();
        }

        // ---------------------------------------------------------------- per frame

        void Update()
        {
            if (step == Step.Idle) return;
            float dt = Time.deltaTime;
            TickFade(dt);

            switch (step)
            {
                case Step.Playing:
                    TickSkip(dt);
                    if (step != Step.Playing) break;
                    if (!dialogueDone && director.time >= data.dialogueAt) StartDialogue();
                    else if (director.playableAsset == null || director.time >= director.duration - data.fadeOut - 0.01) BeginEnding();
                    break;
                case Step.Dialogue:
                    if (dialogueDone) { step = Step.Playing; director.Resume(); }
                    break;
                case Step.Ending:
                    TickSkip(dt);
                    if (fade >= 0.999f) Finish();
                    break;
                case Step.FadingBack:
                    if (fade <= 0.001f) { overlay.gameObject.SetActive(false); step = Step.Idle; }
                    break;
            }
            hintRoot.SetActive((step == Step.Playing || step == Step.Ending) && hintShown);
        }

        void TickSkip(float dt)
        {
            if (skip == null) return;
            if (skip.WasPressedThisFrame())
            {
                hintShown = true;
                hint.text = skip.activeControl?.device is Gamepad ? data.skipHintGamepad : data.skipHintKeyboard;
            }
            hold = skip.IsPressed() ? hold + dt : Mathf.MoveTowards(hold, 0f, dt * 3f);
            bar.fillAmount = Mathf.Clamp01(hold / data.skipHoldTime);
            if (hold < data.skipHoldTime) return;
            Debug.Log("[Cutscene] Skipped.");
            hold = 0f;
            dialogueDone = true;
            if (step == Step.Playing) BeginEnding(fast: true);
        }

        void StartDialogue()
        {
            if (!ServiceLocator.TryGet<IDialogueService>(out var dialogues)
                || !dialogues.StartDialogue(data.dialogue, dialogueListener, dialogueHero))
            {
                Debug.LogWarning("[Cutscene] The dialogue could not start (no DialogueRunner under [Bootstrap]?); going on without it.");
                dialogueDone = true;
                return;
            }
            director.Pause();
            step = Step.Dialogue;
        }

        void OnDialogueEnded(GameObject npc, GameObject hero)
        {
            if (step == Step.Dialogue) dialogueDone = true;
        }

        void BeginEnding(bool fast = false)
        {
            step = Step.Ending;
            FadeTo(1f, fast ? Mathf.Min(0.4f, data.fadeOut) : data.fadeOut);
        }

        // Under the black screen: the Timeline stops (its puppets and camera switch off), the phase ends,
        // the break starts on the real heroes in the school hall.
        void Finish()
        {
            StopTimeline();
            var p = phase;
            phase = null;
            if (p != null && ServiceLocator.TryGet<WorldFlags>(out var flags) && !string.IsNullOrEmpty(p.end.goalFlag))
                flags.SetFlag(p.end.goalFlag);
            else if (ServiceLocator.TryGet<IDayCycle>(out var day)) day.CompletePhase();
            BeginFadeBack();
        }

        void BeginFadeBack()
        {
            skip?.Disable();
            step = Step.FadingBack;
            FadeTo(0f, data.fadeBackIn);
        }

        void StopTimeline()
        {
            if (director.state == PlayState.Playing || director.time > 0) director.Stop();
        }

        // ---------------------------------------------------------------- overlay

        void FadeTo(float target, float seconds)
        {
            fadeTarget = target;
            fadeSpeed = seconds <= 0f ? 1000f : 1f / seconds;
        }

        void SetFade(float v)
        {
            fade = v;
            black.color = new Color(0f, 0f, 0f, v);
        }

        void TickFade(float dt) => SetFade(Mathf.MoveTowards(fade, fadeTarget, fadeSpeed * dt));

        void BuildOverlay()
        {
            var go = new GameObject("CutsceneOverlay", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            overlay = go.AddComponent<Canvas>();
            overlay.renderMode = RenderMode.ScreenSpaceOverlay;
            overlay.sortingOrder = 95;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            black = NewRect("Fade", go.transform).gameObject.AddComponent<Image>();
            Stretch(black.rectTransform);
            black.raycastTarget = false;

            var hr = NewRect("SkipHint", go.transform);
            hr.anchorMin = hr.anchorMax = hr.pivot = new Vector2(1f, 0f);
            hr.anchoredPosition = new Vector2(-48f, 40f);
            hr.sizeDelta = new Vector2(620f, 56f);
            hintRoot = hr.gameObject;
            hint = NewRect("Text", hr).gameObject.AddComponent<TextMeshProUGUI>();
            Stretch(hint.rectTransform);
            hint.rectTransform.offsetMin = new Vector2(0f, 12f);
            if (data.font != null) hint.font = data.font;
            hint.fontSize = data.hintFontSize;
            hint.color = data.hintColor;
            hint.alignment = TextAlignmentOptions.BottomRight;
            hint.raycastTarget = false;
            hint.text = data.skipHintKeyboard;

            var barBack = NewRect("Bar", hr).gameObject.AddComponent<Image>();
            barBack.color = new Color(1f, 1f, 1f, 0.2f);
            var bb = barBack.rectTransform;
            bb.anchorMin = new Vector2(0.35f, 0f);
            bb.anchorMax = new Vector2(1f, 0f);
            bb.pivot = new Vector2(1f, 0f);
            bb.sizeDelta = new Vector2(0f, 5f);
            bar = NewRect("Fill", bb).gameObject.AddComponent<Image>();
            Stretch(bar.rectTransform);
            bar.color = data.barColor;
            bar.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            bar.type = Image.Type.Filled;
            bar.fillMethod = Image.FillMethod.Horizontal;
            bar.fillAmount = 0f;

            hintRoot.SetActive(false);
            go.SetActive(false);
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }
    }
}
