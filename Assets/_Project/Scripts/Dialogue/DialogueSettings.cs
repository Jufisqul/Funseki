using Funseki.Core;
using TMPro;
using UnityEngine;

namespace Funseki.Dialogue
{
    // Every tunable number, color and text of dialogues and barks. Assets/_Project/Data/Dialogue/DialogueSettings.asset.
    [CreateAssetMenu(fileName = "DialogueSettings", menuName = "Funseki/Dialogue/Dialogue Settings")]
    public class DialogueSettings : ScriptableObject
    {
        [Header("Typing")]
        [Tooltip("Letters per second")]
        public float charsPerSecond = 50f;
        [Tooltip("While Ctrl is held: typing is this many times faster")]
        public float fastMultiplier = 5f;
        [Tooltip("While Ctrl is held: a typed-out line moves on after this long, s (stops at choices)")]
        public float fastLineDelay = 0.12f;
        [Tooltip("Auto mode (Alt / «Авто»): next line this long after the line is typed out, s")]
        public float autoDelay = 5f;

        [Header("Mumble")]
        [Tooltip("A mumble sound every N letters (spaces and punctuation are silent)")]
        [Min(1)] public int mumbleEveryChars = 2;
        [Range(0f, 1f)] public float mumbleVolume = 0.5f;
        [Tooltip("Random pitch spread around the speaker's pitch")]
        [Range(0f, 0.5f)] public float mumblePitchJitter = 0.08f;

        [Header("Hero")]
        [Tooltip("Hero used by conditions and the «Голос» option in scenes without the three heroes (no IHeroRoster service, e.g. test scenes)")]
        public HeroId fallbackHero = HeroId.Kaito;

        [Header("Camera")]
        [Tooltip("Blend into and out of the dialogue camera, s")]
        public float cameraBlendTime = 0.9f;
        public int cameraPriority = 50;
        [Tooltip("Camera distance to the side of the pair, m (grows with the distance between them)")]
        public float cameraSideDistance = 2.2f;
        [Tooltip("Camera height above the ground, m")]
        public float cameraHeight = 1.6f;
        [Tooltip("Shift of the camera toward the hero's back, m (over-the-shoulder look)")]
        public float cameraShoulderShift = 0.7f;
        [Tooltip("Height the camera aims at, m (around the faces)")]
        public float cameraLookHeight = 1.35f;
        [Tooltip("Share of the screen the pair takes")]
        [Range(0.2f, 1.5f)] public float cameraFramingSize = 0.8f;
        public Vector2 cameraFovRange = new(25f, 55f);
        [Tooltip("The NPC turns to face the hero when the conversation starts")]
        public bool npcFacesHero = true;

        [Header("Window")]
        public TMP_FontAsset font;
        [Tooltip("9-sliced sprite for the text cloud, buttons and bark bubbles")]
        public Sprite panelSprite;
        public float textFontSize = 38f;
        public float nameFontSize = 32f;
        public Color textColor = new(0.12f, 0.12f, 0.14f);
        public Color cloudColor = new(1f, 0.98f, 0.94f, 0.97f);
        public Color portraitFrameColor = new(0.12f, 0.12f, 0.14f, 0.9f);
        public float portraitSize = 260f;
        public float cloudHeight = 250f;
        [Tooltip("Distance from the screen edges, px at 1920x1080")]
        public float margin = 60f;

        [Header("Buttons under the window")]
        public string skipLabel = "Пропуск";
        public string autoLabel = "Авто";
        public float buttonFontSize = 26f;
        public Color buttonColor = new(0.12f, 0.12f, 0.14f, 0.8f);
        public Color buttonActiveColor = new(0.95f, 0.65f, 0.15f, 0.95f);
        public Color buttonTextColor = Color.white;

        [Header("Choices")]
        public float choiceFontSize = 32f;
        public Color choiceColor = new(0.12f, 0.12f, 0.14f, 0.85f);
        public Color choiceSelectedColor = new(0.95f, 0.65f, 0.15f, 0.95f);
        public Color choiceTextColor = Color.white;
        [Tooltip("Icon of the «Голос» option")]
        public Sprite voiceIcon;
        public Color voiceColor = new(0.55f, 0.3f, 0.85f, 0.95f);
        [Tooltip("Shown before the «Голос» option text")]
        public string voicePrefix = "Голос: ";

        [Header("Barks")]
        public float barkFontSize = 28f;
        [Tooltip("A bark stays at least / at most this long, s")]
        public float barkMinDuration = 2f;
        public float barkMaxDuration = 3f;
        [Tooltip("Extra time per letter between min and max, s")]
        public float barkSecondsPerChar = 0.04f;
        public float barkFadeTime = 0.3f;
        [Tooltip("Bubble height above the head, m")]
        public float barkHeadOffset = 0.3f;
        [Tooltip("Max bubble width, px at 1920x1080")]
        public float barkMaxWidth = 460f;
        public Color barkBubbleColor = new(1f, 0.98f, 0.94f, 0.95f);
        public Color barkTextColor = new(0.12f, 0.12f, 0.14f);
        [Tooltip("Barks farther than this from the active hero (the camera in scenes without heroes) are not shown, m")]
        public float barkMaxDistance = 12f;
        [Tooltip("Hide a bark when a wall is between the camera and the speaker's head")]
        public bool barkHideBehindWalls = true;
        [Tooltip("Layers that block barks (characters and triggers are always ignored)")]
        public LayerMask barkOcclusionMask = ~0;
    }
}
