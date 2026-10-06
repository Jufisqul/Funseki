using Funseki.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Funseki.UI
{
    // «Конец демо»: shown when the game enters SliceEnd (the day schedule ends after the last bell).
    // Thanks, the results (the Физра outcome from the lesson result flags, journal entries found X of Y),
    // «Анкета» (Application.OpenURL with MenuSettings.surveyUrl) and «В главное меню». Child of [Bootstrap].
    public class SliceEndView : MonoBehaviour
    {
        [SerializeField] HudTheme theme;
        [SerializeField] MenuSettings settings;

        CanvasGroup group;
        TextMeshProUGUI fizraValue, journalValue;
        Button surveyButton, menuButton;
        GameObject ownEventSystem;
        bool shown;

        void Awake()
        {
            if (theme == null || settings == null)
            {
                Debug.LogError("[SliceEndView] HudTheme or MenuSettings is not assigned.", this);
                enabled = false;
                return;
            }
            Build();
            Hide();
        }

        void OnEnable()
        {
            GameEvents.OnGameStateChanged += OnStateChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            GameEvents.OnGameStateChanged -= OnStateChanged;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnStateChanged(GameState previous, GameState current)
        {
            if (current == GameState.SliceEnd) Show();
            else if (shown) Hide();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single && shown) Hide();
        }

        void Show()
        {
            shown = true;
            ServiceLocator.TryGet<WorldFlags>(out var flags);
            fizraValue.text = FizraOutcome(flags);
            int found = ServiceLocator.TryGet<IWeekJournal>(out var journal) ? journal.Entries.Count : 0;
            journalValue.text = string.Format(settings.endJournalFormat, found, Mathf.Max(found, settings.endJournalTotal));
            surveyButton.gameObject.SetActive(!string.IsNullOrEmpty(settings.surveyUrl));

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ownEventSystem = MenuBuild.EnsureEventSystem(transform);
            group.gameObject.SetActive(true);
            MenuBuild.Select(surveyButton.gameObject.activeSelf ? surveyButton : menuButton);
            Debug.Log($"[SliceEnd] Физра: {fizraValue.text}, journal {journalValue.text}.");
        }

        void Hide()
        {
            shown = false;
            group.gameObject.SetActive(false);
            if (ownEventSystem != null) { Destroy(ownEventSystem); ownEventSystem = null; }
        }

        string FizraOutcome(WorldFlags flags)
        {
            if (flags == null) return settings.endFizraNone;
            string f = settings.endFizraResultFlag;
            if (flags.GetFlag(f + "_excellent")) return settings.endFizraExcellent;
            if (flags.GetFlag(f + "_normal")) return settings.endFizraNormal;
            if (flags.GetFlag(f + "_shame")) return settings.endFizraShame;
            return settings.endFizraNone;
        }

        void OpenSurvey()
        {
            if (!string.IsNullOrEmpty(settings.surveyUrl)) Application.OpenURL(settings.surveyUrl);
        }

        void ToMainMenu()
        {
            Hide();
            Time.timeScale = 1f;
            SceneManager.LoadScene(settings.mainMenuScene);
        }

        void Build()
        {
            MenuBuild.Canvas("SliceEndCanvas", transform, theme, settings.sortingOrder - 1, out group);
            var root = group.transform;
            var bg = HudBuild.Image("Background", root, null, new Color(settings.dimColor.r, settings.dimColor.g, settings.dimColor.b, 0.94f));
            bg.raycastTarget = true;
            HudBuild.Stretch(bg.rectTransform);

            var col = MenuBuild.Column("Content", root, 20f, 1100f);
            var title = MenuBuild.Label("Title", col, theme, theme.titleFont, settings.titleFontSize, settings.endTitle);
            title.color = theme.accentColor;
            MenuBuild.Height(title, settings.titleFontSize * 1.6f);
            var sub = MenuBuild.Label("Subtitle", col, theme, theme.bodyFont, settings.textFontSize, settings.endSubtitle,
                TextAlignmentOptions.Center, wrap: true);
            MenuBuild.Height(sub, settings.textFontSize * 3f);

            fizraValue = Row(col, settings.endFizraLabel);
            journalValue = Row(col, settings.endJournalLabel);

            var buttons = MenuBuild.Column("Buttons", col, 14f, settings.buttonSize.x);
            // The column stretches to the content width; padding keeps the buttons at their own width.
            int side = Mathf.Max(0, Mathf.RoundToInt((1100f - settings.buttonSize.x) * 0.5f));
            buttons.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(side, side, 30, 0);
            surveyButton = MenuBuild.Button("Survey", buttons, theme, settings, settings.endSurveyText, OpenSurvey);
            menuButton = MenuBuild.Button("MainMenu", buttons, theme, settings, settings.endMainMenuText, ToMainMenu);
        }

        TextMeshProUGUI Row(Transform parent, string label)
        {
            var row = HudBuild.Rect("Row", parent);
            MenuBuild.Height(row, settings.textFontSize * 1.8f);
            var l = HudBuild.Text("Label", row, theme, theme.bodyFont, settings.textFontSize, TextAlignmentOptions.Right);
            l.rectTransform.anchorMin = new Vector2(0f, 0f);
            l.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            l.rectTransform.offsetMin = Vector2.zero;
            l.rectTransform.offsetMax = new Vector2(-16f, 0f);
            l.text = label;
            l.color = theme.textDimColor;
            var v = HudBuild.Text("Value", row, theme, theme.titleFont, settings.textFontSize, TextAlignmentOptions.Left);
            v.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            v.rectTransform.anchorMax = new Vector2(1f, 1f);
            v.rectTransform.offsetMin = new Vector2(16f, 0f);
            v.rectTransform.offsetMax = Vector2.zero;
            return v;
        }
    }
}
