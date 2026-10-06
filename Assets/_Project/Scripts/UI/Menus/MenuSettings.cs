using System.Collections.Generic;
using Funseki.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Funseki.UI
{
    // Texts and behavior of the in-game menus: the pause menu (Esc), its «Настройки» page, the save icon
    // and the «Конец демо» screen. The look comes from HudTheme. Assets/_Project/Data/UI/MenuSettings.asset.
    [CreateAssetMenu(fileName = "MenuSettings", menuName = "Funseki/UI/Menu Settings")]
    public class MenuSettings : ScriptableObject
    {
        [Header("Input")]
        public InputActionAsset actions;
        [Tooltip("Opens and closes the pause menu; on a sub-page goes back")]
        public string pauseAction = "Gameplay/Pause";

        [Header("Pause")]
        [Tooltip("States in which Esc opens the pause menu")]
        public List<GameState> pausableStates = new() { GameState.Break, GameState.Lesson, GameState.Cutscene, GameState.Caught };
        [Tooltip("Scene of «В главное меню» (Build Settings name)")]
        public string mainMenuScene = "MainMenu";
        [Tooltip("Canvas sorting order: above the HUD (30), the hero window (60) and dialogue (80)")]
        public int sortingOrder = 100;
        [Tooltip("Darkening behind the menus")]
        public Color dimColor = new(0.05f, 0.04f, 0.08f, 0.75f);
        public Vector2 buttonSize = new(520f, 72f);
        public float buttonFontSize = 34f;
        public float titleFontSize = 64f;
        public float textFontSize = 30f;

        [Header("Pause texts")]
        public string pauseTitle = "Пауза";
        public string resumeText = "Продолжить";
        public string journalText = "Журнал недели";
        public string settingsText = "Настройки";
        public string mainMenuText = "В главное меню";
        public string quitText = "Выход";
        public string backText = "Назад";

        [Header("Journal page")]
        public string journalTitle = "Журнал недели";
        [TextArea(1, 3)] public string journalEmptyText = "Пока ни одной записи. Самое время что-нибудь устроить.";
        [Tooltip("{0} = day")]
        public string journalDayFormat = "День {0}";

        [Header("Settings page")]
        public string settingsTitle = "Настройки";
        public string volumeText = "Громкость";
        public string sensitivityText = "Чувствительность мыши";
        public string textSpeedText = "Скорость текста";
        public string windowModeText = "Режим окна";
        public string windowedText = "Оконный";
        public string fullscreenText = "Полноэкранный";

        [Header("Save icon")]
        public string savingText = "Сохранение…";
        [Tooltip("Seconds the icon stays after a save")]
        [Min(0.2f)] public float saveIconTime = 2f;
        public float saveIconSize = 56f;

        [Header("«Конец демо»")]
        public string endTitle = "Спасибо, что сыграли!";
        [TextArea(1, 3)] public string endSubtitle = "Это конец демо «One Funseki, Seven Days»: первый день в Фунсэки позади.";
        public string endFizraLabel = "Физра";
        [Tooltip("Lesson result flag prefix; _excellent / _normal / _shame are added (LessonData.ResultFlag)")]
        public string endFizraResultFlag = "fizra_day1_result";
        public string endFizraExcellent = "Блестяще";
        public string endFizraNormal = "Нормально";
        public string endFizraShame = "Позорно";
        public string endFizraNone = "Прогуляли";
        public string endJournalLabel = "Записи в журнале недели";
        [Tooltip("{0} = found, {1} = all in the slice")]
        public string endJournalFormat = "{0} из {1}";
        [Tooltip("How many journal entries (pranks and secrets) the slice has in total")]
        [Min(1)] public int endJournalTotal = 1;
        public string endSurveyText = "Анкета";
        [Tooltip("Link the «Анкета» button opens; empty hides the button")]
        public string surveyUrl = "";
        public string endMainMenuText = "В главное меню";
    }
}
