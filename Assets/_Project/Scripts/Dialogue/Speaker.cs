using UnityEngine;

namespace Funseki.Dialogue
{
    // Who is talking: name, portrait, name color and the "mumble" played under each typed letter (GDD 5.3).
    [CreateAssetMenu(fileName = "Speaker_", menuName = "Funseki/Dialogue/Speaker")]
    public class Speaker : ScriptableObject
    {
        public string displayName;
        [Tooltip("Shown in the lower left corner of the dialogue window; empty = a colored square with the first letter")]
        public Sprite portrait;
        public Color nameColor = Color.white;

        [Header("Mumble")]
        [Tooltip("Short syllable sounds, one is picked at random per letter; empty = a synthesized beep")]
        public AudioClip[] mumbleClips;
        [Tooltip("1 = normal, higher = squeakier voice")]
        [Range(0.3f, 3f)] public float mumblePitch = 1f;
        [Range(0f, 1f)] public float mumbleVolume = 1f;
    }
}
