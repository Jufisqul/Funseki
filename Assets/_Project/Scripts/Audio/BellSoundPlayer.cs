using System.Collections;
using Funseki.Core;
using UnityEngine;

namespace Funseki.Audio
{
    // Plays the school bell on GameEvents.OnBell. 2D sound: the bell is heard everywhere in the school.
    // Lives under [Bootstrap], so every day scene has it.
    [RequireComponent(typeof(AudioSource))]
    public class BellSoundPlayer : MonoBehaviour
    {
        [SerializeField] BellSettings settings;

        AudioSource source;
        Coroutine stopRoutine;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }

        void OnEnable() => GameEvents.OnBell += OnBell;
        void OnDisable() => GameEvents.OnBell -= OnBell;

        void OnBell(BellType type)
        {
            if (settings == null) return;
            var sound = settings.For(type);
            if (sound.clip == null) return;

            if (stopRoutine != null) StopCoroutine(stopRoutine);
            source.Stop();
            source.clip = sound.clip;
            source.volume = sound.volume;
            source.pitch = sound.pitch;
            source.Play();
            if (sound.maxLength > 0f) stopRoutine = StartCoroutine(StopAfter(sound.maxLength));
        }

        IEnumerator StopAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            source.Stop();
            stopRoutine = null;
        }
    }
}
