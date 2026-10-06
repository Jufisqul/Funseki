using System.Collections.Generic;
using Funseki.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Funseki.UI
{
    // The pause menu (Esc / Start): «Продолжить», «Журнал недели» (entries with captions), «Настройки» (volume,
    // mouse sensitivity, text speed, windowed / fullscreen — PlayerOptions), «В главное меню» (no save: «Продолжить»
    // in the menu returns to the last autosave), «Выход». Child of [Bootstrap], works in any game scene whose state is
    // in MenuSettings.pausableStates. Stops game time (Time.timeScale 0) and the game audio while open.
    public class PauseMenu : MonoBehaviour
    {
        enum Page { Main, Journal, Settings }

        [SerializeField] HudTheme theme;
        [SerializeField] MenuSettings settings;

        InputAction toggle;
        CanvasGroup group;
        RectTransform mainPage, journalPage, settingsPage, journalList;
        Button resumeButton, journalBack;
        Slider volumeSlider;
        TextMeshProUGUI windowLabel;
        Page page;
        bool open;
        float timeScaleBefore = 1f;
        CursorLockMode lockBefore;
        bool cursorBefore;
        GameObject ownEventSystem;

        public static bool IsOpen { get; private set; }

        void Awake()
        {
            if (theme == null || settings == null)
            {
                Debug.LogError("[PauseMenu] HudTheme or MenuSettings is not assigned.", this);
                enabled = false;
                return;
            }
            PlayerOptions.Apply();
            // A copy of the Gameplay action, so it works whatever map the game has switched on or off.
            var source = settings.actions != null ? settings.actions.FindAction(settings.pauseAction) : null;
            if (source == null) Debug.LogError($"[PauseMenu] No action '{settings.pauseAction}'.", this);
            else toggle = source.Clone();
            Build();
            SetVisible(false);
        }

        void OnEnable()
        {
            toggle?.Enable();
            SceneManager.sceneLoaded += OnSceneLoaded;
            GameEvents.OnGameStateChanged += OnStateChanged;
        }

        void OnDisable()
        {
            toggle?.Disable();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            GameEvents.OnGameStateChanged -= OnStateChanged;
        }

        // The same Esc may have just closed something else (the hero window, the locker close-up): don't reopen on it.
        int stateChangedFrame = -1;
        void OnStateChanged(GameState previous, GameState current)
        {
            if (!open) stateChangedFrame = Time.frameCount;
        }

        void OnDestroy()
        {
            toggle?.Dispose();
            IsOpen = false;
        }

        void Update()
        {
            if (toggle == null || !toggle.WasPressedThisFrame()) return;
            if (open)
            {
                if (page != Page.Main) ShowPage(Page.Main);
                else Resume();
                return;
            }
            if (Time.frameCount == stateChangedFrame) return;
            if (ServiceLocator.TryGet<GameStateMachine>(out var fsm) && settings.pausableStates.Contains(fsm.Current)) Open(fsm);
        }

        // ---------------------------------------------------------------- open / close

        void Open(GameStateMachine fsm)
        {
            open = IsOpen = true;
            fsm.Pause();
            timeScaleBefore = Time.timeScale;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            lockBefore = Cursor.lockState;
            cursorBefore = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ownEventSystem = MenuBuild.EnsureEventSystem(transform);
            SetVisible(true);
            ShowPage(Page.Main);
        }

        void Resume()
        {
            Close(restoreCursor: true);
            if (ServiceLocator.TryGet<GameStateMachine>(out var fsm)) fsm.Resume();
        }

        void Close(bool restoreCursor)
        {
            if (!open) return;
            open = IsOpen = false;
            SetVisible(false);
            Time.timeScale = timeScaleBefore > 0f ? timeScaleBefore : 1f;
            AudioListener.pause = false;
            if (restoreCursor)
            {
                Cursor.lockState = lockBefore;
                Cursor.visible = cursorBefore;
            }
            if (ownEventSystem != null) { Destroy(ownEventSystem); ownEventSystem = null; }
            PlayerOptions.SavePrefs();
        }

        void ToMainMenu()
        {
            Close(restoreCursor: false);
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Debug.Log($"[PauseMenu] To the main menu ({settings.mainMenuScene}).");
            SceneManager.LoadScene(settings.mainMenuScene);
        }

        void Quit()
        {
            Close(restoreCursor: false);
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single && open) Close(restoreCursor: false);
        }

        void SetVisible(bool on)
        {
            group.alpha = on ? 1f : 0f;
            group.interactable = on;
            group.blocksRaycasts = on;
            group.gameObject.SetActive(on);
        }

        void ShowPage(Page p)
        {
            page = p;
            mainPage.gameObject.SetActive(p == Page.Main);
            journalPage.gameObject.SetActive(p == Page.Journal);
            settingsPage.gameObject.SetActive(p == Page.Settings);
            switch (p)
            {
                case Page.Main: MenuBuild.Select(resumeButton); break;
                case Page.Journal: FillJournal(); MenuBuild.Select(journalBack); break;
                case Page.Settings: RefreshSettings(); MenuBuild.Select(volumeSlider); break;
            }
        }

        // ---------------------------------------------------------------- pages

        void FillJournal()
        {
            for (int i = journalList.childCount - 1; i >= 0; i--) Destroy(journalList.GetChild(i).gameObject);
            IReadOnlyList<JournalEntry> entries = ServiceLocator.TryGet<IWeekJournal>(out var journal) ? journal.Entries : null;
            if (entries == null || entries.Count == 0)
            {
                MenuBuild.Label("Empty", journalList, theme, theme.bodyFont, settings.textFontSize, settings.journalEmptyText,
                    TextAlignmentOptions.Center, wrap: true);
                return;
            }
            foreach (var e in entries)
            {
                var title = MenuBuild.Label("Title", journalList, theme, theme.titleFont, settings.textFontSize,
                    $"{e.title}  <size=70%><color=#{ColorUtility.ToHtmlStringRGBA(theme.textDimColor)}>{string.Format(settings.journalDayFormat, e.day)}</color></size>",
                    TextAlignmentOptions.Left);
                title.color = theme.accentColor;
                if (!string.IsNullOrEmpty(e.caption))
                    MenuBuild.Label("Caption", journalList, theme, theme.bodyFont, settings.textFontSize * 0.85f, e.caption,
                        TextAlignmentOptions.Left, wrap: true);
            }
        }

        void RefreshSettings()
        {
            volumeSlider.SetValueWithoutNotify(PlayerOptions.Volume);
            windowLabel.text = WindowText();
        }

        string WindowText() => $"{settings.windowModeText}: {(PlayerOptions.Fullscreen ? settings.fullscreenText : settings.windowedText)}";

        // ---------------------------------------------------------------- build

        void Build()
        {
            MenuBuild.Canvas("PauseCanvas", transform, theme, settings.sortingOrder, out group);
            var root = group.transform;
            var dim = HudBuild.Image("Dim", root, null, settings.dimColor);
            dim.raycastTarget = true;
            HudBuild.Stretch(dim.rectTransform);

            float w = settings.buttonSize.x;
            mainPage = MenuBuild.Column("Main", root, 16f, w);
            Title(mainPage, settings.pauseTitle);
            resumeButton = MenuBuild.Button("Resume", mainPage, theme, settings, settings.resumeText, Resume);
            MenuBuild.Button("Journal", mainPage, theme, settings, settings.journalText, () => ShowPage(Page.Journal));
            MenuBuild.Button("Settings", mainPage, theme, settings, settings.settingsText, () => ShowPage(Page.Settings));
            MenuBuild.Button("MainMenu", mainPage, theme, settings, settings.mainMenuText, ToMainMenu);
            MenuBuild.Button("Quit", mainPage, theme, settings, settings.quitText, Quit);

            journalPage = MenuBuild.Column("Journal", root, 18f, 960f);
            Title(journalPage, settings.journalTitle);
            journalList = MenuBuild.Column("Entries", journalPage, 6f, 960f);
            journalBack = MenuBuild.Button("Back", journalPage, theme, settings, settings.backText, () => ShowPage(Page.Main));

            settingsPage = MenuBuild.Column("Settings", root, 12f, 760f);
            Title(settingsPage, settings.settingsTitle);
            Caption(settingsPage, settings.volumeText);
            volumeSlider = MenuBuild.Slider("Volume", settingsPage, theme, 0f, 1f, PlayerOptions.Volume, v => PlayerOptions.Volume = v);
            Caption(settingsPage, settings.sensitivityText);
            MenuBuild.Slider("Sensitivity", settingsPage, theme, PlayerOptions.MinSensitivity, PlayerOptions.MaxSensitivity,
                PlayerOptions.MouseSensitivity, v => PlayerOptions.MouseSensitivity = v);
            Caption(settingsPage, settings.textSpeedText);
            MenuBuild.Slider("TextSpeed", settingsPage, theme, PlayerOptions.MinTextSpeed, PlayerOptions.MaxTextSpeed,
                PlayerOptions.TextSpeed, v => PlayerOptions.TextSpeed = v);
            var window = MenuBuild.Button("WindowMode", settingsPage, theme, settings, WindowText(), () =>
            {
                PlayerOptions.Fullscreen = !PlayerOptions.Fullscreen;
                windowLabel.text = WindowText();
            });
            windowLabel = window.GetComponentInChildren<TextMeshProUGUI>();
            MenuBuild.Button("Back", settingsPage, theme, settings, settings.backText, () => ShowPage(Page.Main));
        }

        void Title(Transform parent, string text)
        {
            var t = MenuBuild.Label("Title", parent, theme, theme.titleFont, settings.titleFontSize, text);
            MenuBuild.Height(t, settings.titleFontSize * 1.6f);
        }

        void Caption(Transform parent, string text)
        {
            var t = MenuBuild.Label("Caption", parent, theme, theme.bodyFont, settings.textFontSize, text, TextAlignmentOptions.Left);
            t.color = theme.textDimColor;
            MenuBuild.Height(t, settings.textFontSize * 1.4f);
        }
    }
}
