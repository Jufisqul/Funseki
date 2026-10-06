using System.Collections;
using System.Collections.Generic;
using Funseki.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Funseki.Dialogue
{
    // Plays a DialogueGraph (GDD 5.3): puts the game into the Dialogue state, types lines with the speaker's mumble,
    // handles the "Dialogue" action map (advance, Ctrl fast-forward, Alt auto, skip, choice navigation)
    // and frames the pair with DialogueCamera. A service: child of [Bootstrap], or a copy in a test scene.
    [DefaultExecutionOrder(-900)]
    public class DialogueRunner : MonoBehaviour, IDialogueService
    {
        public const string MapName = "Dialogue";

        [SerializeField] DialogueSettings settings;
        [SerializeField] InputActionAsset actions;

        public bool IsRunning => graph != null;
        public HeroId CurrentHero => HeroService.HasRoster ? HeroService.Current : settings.fallbackHero;

        DialogueView view;
        DialogueCamera cam;
        MumblePlayer mumble;
        InputActionMap map;
        InputAction advance, fastForward, auto, skip, navigate;
        bool owner;

        DialogueGraph graph;
        GameObject npc, hero;
        int index = -1;
        DialogueNode node;
        readonly List<DialogueChoice> choices = new();
        bool typing, choicesOpen, autoMode;
        float typed, lineDoneAt;
        int total, mumbledUpTo, lettersSinceMumble, ignoreInputFrame;
        GameState stateBefore = GameState.Break;
        GameObject ownEventSystem;

        void Awake()
        {
            if (ServiceLocator.IsRegistered<DialogueRunner>()) { Destroy(gameObject); return; }
            owner = true;
            ServiceLocator.Register(this);
            ServiceLocator.Register<IDialogueService>(this);

            map = actions.FindActionMap(MapName, true);
            advance = map.FindAction("Advance", true);
            fastForward = map.FindAction("FastForward", true);
            auto = map.FindAction("Auto", true);
            skip = map.FindAction("Skip", true);
            navigate = map.FindAction("Navigate", true);

            view = new DialogueView(settings, transform);
            view.SkipClicked += Skip;
            view.AutoClicked += ToggleAuto;
            view.ChoiceClicked += Choose;
            view.ChoiceHovered += Select;
            cam = new DialogueCamera(settings, transform);
            mumble = new MumblePlayer(gameObject, settings);
        }

        void OnDestroy()
        {
            if (!owner) return;
            if (ServiceLocator.TryGet<DialogueRunner>(out var r) && r == this) ServiceLocator.Unregister<DialogueRunner>();
            if (ServiceLocator.TryGet<IDialogueService>(out var d) && ReferenceEquals(d, this)) ServiceLocator.Unregister<IDialogueService>();
            map?.Disable();
        }

        // ---------------------------------------------------------------- public

        public bool StartDialogue(DialogueGraph dialogue, GameObject npcObject, GameObject heroObject)
        {
            if (IsRunning || dialogue == null || dialogue.nodes.Count == 0) return false;
            graph = dialogue;
            npc = npcObject;
            hero = heroObject;

            if (ServiceLocator.TryGet<GameStateMachine>(out var fsm))
            {
                stateBefore = fsm.Current;
                fsm.ChangeState(GameState.Dialogue);
            }
            EnsureEventSystem();
            map.Enable();
            view.Show();
            view.SetAuto(autoMode);

            if (settings.npcFacesHero && npc != null && hero != null)
            {
                Vector3 to = hero.transform.position - npc.transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.0001f) npc.transform.rotation = Quaternion.LookRotation(to, Vector3.up);
            }
            cam.Begin(hero != null ? hero.transform : null, npc != null ? npc.transform : null);

            GameEvents.RaiseDialogueStarted(npc, hero);
            ignoreInputFrame = Time.frameCount;
            Enter(FindShown(0));
            return true;
        }

        bool IDialogueService.StartDialogue(ScriptableObject dialogue, GameObject npcObject, GameObject heroObject) =>
            dialogue is DialogueGraph g && StartDialogue(g, npcObject, heroObject);

        public void Stop() => End();

        // ---------------------------------------------------------------- flow

        // First node at or after i whose conditions pass; -1 = none (the end).
        int FindShown(int i)
        {
            if (i < 0) return -1;
            var h = CurrentHero;
            for (; i < graph.nodes.Count; i++)
                if (DialogueCondition.AllMet(graph.nodes[i].conditions, h)) return i;
            return -1;
        }

        void Enter(int i)
        {
            if (!IsRunning) return;
            if (i < 0) { End(); return; }

            SetNode(i);
            if (string.IsNullOrEmpty(node.text))
            {
                if (choices.Count > 0) { view.SetLine(node.speaker, ""); OpenChoices(); }
                else Continue();
                return;
            }
            total = view.SetLine(node.speaker, node.text);
            typed = 0f;
            mumbledUpTo = 0;
            lettersSinceMumble = 0;
            typing = true;
        }

        // Makes node i current: runs its actions and collects the choices this hero may see.
        void SetNode(int i)
        {
            index = i;
            node = graph.nodes[i];
            choicesOpen = false;
            view.HideChoices();
            DialogueAction.RunAll(node.actions);

            choices.Clear();
            var h = CurrentHero;
            foreach (var c in node.choices)
            {
                if (c == null) continue;
                if (c.voice && h != HeroId.Kaito) continue;
                if (DialogueCondition.AllMet(c.conditions, h)) choices.Add(c);
            }
            // Regular choices first, the «Голос» option last.
            choices.Sort((a, b) => a.voice.CompareTo(b.voice));
        }

        void FinishTyping()
        {
            typing = false;
            view.SetVisible(total);
            lineDoneAt = Time.unscaledTime;
            if (choices.Count > 0) OpenChoices();
            else view.SetContinueMark(true);
        }

        // After a typed-out line without choices.
        void Continue()
        {
            if (!IsRunning) return;
            if (choices.Count > 0) { OpenChoices(); return; }
            if (node.end) { End(); return; }
            Enter(FindShown(graph.Resolve(node.next, index)));
        }

        void OpenChoices()
        {
            choicesOpen = true;
            view.SetContinueMark(false);
            view.ShowChoices(choices);
            selected = 0;
        }

        int selected;

        void Select(int i)
        {
            if (!choicesOpen || i < 0 || i >= choices.Count) return;
            selected = i;
            view.SetSelected(i);
        }

        void Choose(int i)
        {
            if (!IsRunning || !choicesOpen || i < 0 || i >= choices.Count) return;
            var c = choices[i];
            choicesOpen = false;
            view.HideChoices();
            ignoreInputFrame = Time.frameCount;
            DialogueAction.RunAll(c.actions);
            if (c.end) End();
            else Enter(FindShown(graph.Resolve(c.next, index)));
        }

        // «Пропуск»: jumps over lines (still running their actions) to the next choice, or to the end.
        void Skip()
        {
            if (!IsRunning || choicesOpen) return;
            ignoreInputFrame = Time.frameCount;
            while (true)
            {
                if (choices.Count > 0)
                {
                    total = view.SetLine(node.speaker, node.text);
                    FinishTyping();
                    return;
                }
                if (node.end) { End(); return; }
                int next = FindShown(graph.Resolve(node.next, index));
                if (next < 0) { End(); return; }
                SetNode(next);
            }
        }

        void ToggleAuto()
        {
            autoMode = !autoMode;
            view.SetAuto(autoMode);
            if (autoMode && !typing) lineDoneAt = Time.unscaledTime;
        }

        void End()
        {
            if (!IsRunning) return;
            var n = npc;
            var h = hero;
            graph = null;
            node = null;
            npc = hero = null;
            typing = choicesOpen = false;
            choices.Clear();

            map.Disable();
            view.Hide();
            cam.End();
            if (ownEventSystem != null) { Destroy(ownEventSystem); ownEventSystem = null; }
            // A frame later, so the key that closed the dialogue (Space) doesn't also make the hero jump.
            StartCoroutine(RestoreStateNextFrame());
            GameEvents.RaiseDialogueEnded(n, h);
        }

        IEnumerator RestoreStateNextFrame()
        {
            yield return null;
            if (IsRunning) yield break;
            if (ServiceLocator.TryGet<GameStateMachine>(out var fsm) && fsm.Current == GameState.Dialogue)
            {
                var back = stateBefore == GameState.Dialogue || stateBefore == GameState.None ? GameState.Break : stateBefore;
                fsm.ChangeState(back);
            }
        }

        // ---------------------------------------------------------------- per frame

        void Update()
        {
            if (!IsRunning) return;
            view.Tick();
            if (Time.frameCount == ignoreInputFrame) return;

            if (auto.WasPressedThisFrame()) ToggleAuto();
            if (skip.WasPressedThisFrame()) { Skip(); return; }

            bool next = advance.WasPressedThisFrame() && !ClickedOnButton();
            bool fast = fastForward.IsPressed();

            if (choicesOpen)
            {
                if (navigate.WasPressedThisFrame())
                {
                    float v = navigate.ReadValue<float>();
                    if (v > 0.5f) Select((selected - 1 + choices.Count) % choices.Count);
                    else if (v < -0.5f) Select((selected + 1) % choices.Count);
                }
                if (next) Choose(selected);
                return;
            }

            if (typing)
            {
                if (next)
                {
                    FinishTyping();
                    return;
                }
                typed += settings.charsPerSecond * PlayerOptions.TextSpeed * (fast ? settings.fastMultiplier : 1f) * Time.unscaledDeltaTime;
                int shown = Mathf.Min(total, Mathf.FloorToInt(typed));
                Mumble(shown);
                view.SetVisible(shown);
                if (shown >= total) FinishTyping();
                return;
            }

            float waited = Time.unscaledTime - lineDoneAt;
            if (next || (autoMode && waited >= settings.autoDelay) || (fast && waited >= settings.fastLineDelay))
                Continue();
        }

        void LateUpdate()
        {
            if (IsRunning) cam.Tick();
        }

        // One mumble per few letters, at most one per frame.
        void Mumble(int shown)
        {
            bool play = false;
            for (; mumbledUpTo < shown; mumbledUpTo++)
            {
                if (!char.IsLetterOrDigit(view.CharAt(mumbledUpTo))) continue;
                if (lettersSinceMumble++ % settings.mumbleEveryChars == 0) play = true;
            }
            if (play) mumble.Play(node.speaker);
        }

        // A mouse click on «Пропуск», «Авто» or a choice is handled by the button, not as "next line".
        bool ClickedOnButton()
        {
            if (!(advance.activeControl?.device is Mouse)) return false;
            var es = EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }

        // Buttons need an EventSystem; scenes without UI (the slice) don't have one.
        void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            ownEventSystem = new GameObject("DialogueEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            ownEventSystem.transform.SetParent(transform, false);
        }
    }
}
