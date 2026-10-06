using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Funseki.Lessons.Fizra
{
    // Everything tunable about «Физра: Свисток» except the outcome thresholds and scenes (those are in LessonData).
    // Assets/_Project/Data/Lessons/Fizra/FizraSettings.asset.
    [CreateAssetMenu(fileName = "FizraSettings", menuName = "Funseki/Lessons/Fizra/Settings")]
    public class FizraSettings : ScriptableObject
    {
        [Header("Rounds")]
        public List<FizraRound> rounds = new();
        [Tooltip("Pause between rounds, s")]
        [Min(0f)] public float roundPause = 2.5f;
        [Tooltip("How long the new whistle of a round is demonstrated, s")]
        [Min(0f)] public float newCommandDemo = 2.5f;

        [Header("«Без свистка» (the whistle was stolen)")]
        [Tooltip("WorldFlag that turns the mode on")]
        public string noWhistleFlag = "prank_whistle_done";
        [Tooltip("Chance that the helper translates a command wrong")]
        [Range(0f, 1f)] public float helperWrongChance = 0.2f;
        [Tooltip("Pictogram over the teacher while he whistles through his fingers")]
        public string noWhistleGlyph = "?!";
        [Tooltip("Lines before the first round in this mode")]
        public List<LessonLine> noWhistleIntro = new();
        [Tooltip("Helper lines when he is not sure; {0} = his translation")]
        public string[] helperTranslateFormats = { "Он говорит: «{0}»", "Кажется… «{0}»", "«{0}»! Наверное." };

        [Header("Helper (Шутник) and students")]
        [Tooltip("Chance that the helper teases the hero after a mistake")]
        [Range(0f, 1f)] public float helperTeaseChance = 0.5f;
        public string[] helperTeaseLines = { "Это был не тот свисток!", "Сок мой, сок мой!", "Красиво. Неправильно, но красиво." };
        [Tooltip("Chance that a student in the line gets a command wrong")]
        [Range(0f, 1f)] public float studentErrorChance = 0.15f;
        [Tooltip("Lines of the students when they obey a false command (the kettle)")]
        public string[] studentFalseLines = { "А?!", "Это был свисток?", "Я прыгнул на всякий случай." };
        [Tooltip("The teacher about the kettle")]
        public string[] teacherFalseLines = { "…", "*сердито смотрит на окно учительской*" };

        [Header("Feedback")]
        public string rightText = "Есть!";
        public string wrongText = "Мимо!";
        public string missedText = "Проспал!";
        [Tooltip("Show «right / all» in the corner (spec: open question)")]
        public bool showScore = true;
        public string scoreFormat = "{0} / {1}";
        [Tooltip("Hero lines after a mistake, sometimes")]
        [Range(0f, 1f)] public float heroMistakeLineChance = 0.3f;
        public string[] heroMistakeLines = { "Я так и хотел.", "Лень было.", "Руки в карманах — минус к ловкости." };

        [Header("Lazy body (placeholder animation)")]
        [Tooltip("Ryuta barely leaves the ground")]
        public float heroJumpHeight = 0.12f;
        public float studentJumpHeight = 0.45f;
        public float stepDistance = 0.4f;
        [Tooltip("Body height while sitting, part of normal")]
        [Range(0.3f, 1f)] public float sitScale = 0.6f;
        [Tooltip("Length of a move, s")]
        [Min(0.1f)] public float moveTime = 0.45f;
        [Tooltip("Tilt of the mistake gag, degrees")]
        public float mistakeTilt = 35f;
        [Tooltip("Ball flight time for the catch command, part of the window")]
        [Range(0.2f, 1f)] public float ballFlightPart = 0.8f;

        [Header("Sound")]
        [Range(0f, 1f)] public float whistleVolume = 0.6f;
        [Tooltip("Base pitch of the synthesized whistle, Hz")]
        public float whistleHz = 2600f;
        [Tooltip("Base pitch of the finger whistle, Hz (lower and wobblier)")]
        public float fingerHz = 1500f;
        [Tooltip("Anthem the teacher whistles on «Блестяще»: notes as Hz:seconds")]
        public string anthemNotes = "1568:0.4 1760:0.4 1976:0.4 2093:0.8 1976:0.4 1760:0.4 1568:0.9";
        [Tooltip("Short approving whistle (Нормально)")]
        public string approvePattern = "s";
        [Tooltip("Angry whistles (Позорно)")]
        public string angryPattern = "x x x _ x x x";

        [Header("Camera")]
        [Tooltip("Priority of the lesson camera over the gameplay cameras")]
        public int cameraPriority = 55;

        [Header("Debug (Editor only)")]
        public Key winRoundKey = Key.F7;
        public Key loseRoundKey = Key.F8;
    }
}
