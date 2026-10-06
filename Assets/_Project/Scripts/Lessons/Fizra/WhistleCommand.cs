using UnityEngine;

namespace Funseki.Lessons.Fizra
{
    /// <summary>What the hero's body does for a command (placeholder animation in FizraPuppet).</summary>
    public enum FizraMove { None, Jump, Step, Sit, Catch }

    /// <summary>Direction of a step command, relative to the line of students (the camera looks the same way).</summary>
    public enum StepDirection { Up, Down, Left, Right }

    // One whistle of the PE teacher and what it means (spec «Физра: Свисток» → Управление).
    // Assets/_Project/Data/Lessons/Fizra/Commands/Whistle_*.asset. A command with an empty inputAction means
    // «don't press anything» (freeze); a false command (the kettle) is never to be obeyed.
    [CreateAssetMenu(fileName = "Whistle", menuName = "Funseki/Lessons/Fizra/Whistle Command")]
    public class WhistleCommand : ScriptableObject
    {
        public string id = "jump";
        [Tooltip("Translation shown under the pictogram while captions are on, and what the helper says: «Прыгай!»")]
        public string caption = "Прыгай!";

        [Header("Pictogram")]
        public Sprite icon;
        [Tooltip("Text pictogram when there is no icon yet: the shape of the whistle (• •, ———, ~~~)")]
        public string glyph = "•";

        [Header("Sound")]
        [Tooltip("The real whistle; empty = synthesized from the pattern")]
        public AudioClip whistleClip;
        [Tooltip("Whistling through the fingers («без свистка»); empty = the pattern, distorted")]
        public AudioClip fingerClip;
        [Tooltip("Placeholder synth: s short, l long, t trill, x sharp, k kettle, _ pause. «s _ s» = two short")]
        public string synthPattern = "s";

        [Header("Input (action map «Lesson»)")]
        [Tooltip("Jump, Sit, Catch… Empty = freeze: don't press anything")]
        public string inputAction = "Jump";
        [Tooltip("The teacher shows a direction; the action becomes inputAction + Up/Down/Left/Right (StepLeft)")]
        public bool directional;
        [Tooltip("Reaction window, s. 0 = the round's window")]
        [Min(0f)] public float window;

        [Header("Body")]
        public FizraMove move = FizraMove.Jump;
        [Tooltip("Not a command at all (the kettle in the teachers' room): the right answer is to ignore it. " +
                 "The other students obey it anyway")]
        public bool isFalse;
        [Tooltip("False command: what the students do when they wrongly obey it")]
        public FizraMove falseMoveOfStudents = FizraMove.Jump;

        public bool IsFreeze => string.IsNullOrEmpty(inputAction);

        public string ActionFor(StepDirection dir) => directional ? inputAction + dir : inputAction;
    }
}
