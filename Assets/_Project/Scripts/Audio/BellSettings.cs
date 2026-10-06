using Funseki.Core;
using UnityEngine;

namespace Funseki.Audio
{
    // How the school bell sounds for each GameEvents.OnBell type. Edited by the designer.
    [CreateAssetMenu(fileName = "BellSettings", menuName = "Funseki/Audio/Bell Settings")]
    public class BellSettings : ScriptableObject
    {
        [System.Serializable]
        public class BellSound
        {
            [Tooltip("Empty = silent")]
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 0.8f;
            [Range(0.5f, 2f)] public float pitch = 1f;
            [Tooltip("Seconds of the clip to play, 0 = the whole clip")]
            [Min(0f)] public float maxLength;
        }

        [Tooltip("Break over, go to class")]
        public BellSound lessonStart = new();
        [Tooltip("Repeated bell: the hero is still not in class")]
        public BellSound second = new() { pitch = 1.05f };
        [Tooltip("Lesson over")]
        public BellSound lessonEnd = new();

        public BellSound For(BellType type) => type switch
        {
            BellType.Second => second,
            BellType.LessonEnd => lessonEnd,
            _ => lessonStart,
        };
    }
}
