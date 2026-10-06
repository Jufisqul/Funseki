using UnityEngine;

namespace Funseki.Dialogue
{
    // Plays a speaker's "mumble" syllables (GDD 5.3). Falls back to a synthesized beep when the speaker has no clips.
    public class MumblePlayer
    {
        readonly AudioSource source;
        readonly DialogueSettings settings;
        static AudioClip beep;

        public MumblePlayer(GameObject host, DialogueSettings settings)
        {
            this.settings = settings;
            source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }

        public void Play(Speaker speaker)
        {
            AudioClip clip = null;
            if (speaker != null && speaker.mumbleClips != null && speaker.mumbleClips.Length > 0)
                clip = speaker.mumbleClips[Random.Range(0, speaker.mumbleClips.Length)];
            if (clip == null) clip = Beep();

            float pitch = speaker != null ? speaker.mumblePitch : 1f;
            float jitter = settings.mumblePitchJitter;
            source.pitch = pitch * (1f + Random.Range(-jitter, jitter));
            source.PlayOneShot(clip, settings.mumbleVolume * (speaker != null ? speaker.mumbleVolume : 1f));
        }

        // A short soft square blip, so placeholder speakers are audible without sound assets.
        static AudioClip Beep()
        {
            if (beep != null) return beep;
            const int rate = 44100;
            const float length = 0.05f, freq = 480f;
            int n = (int)(rate * length);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                float env = Mathf.Min(1f, i / 200f) * (1f - (float)i / n);
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * freq * 3f * t);
                data[i] = 0.35f * env * wave;
            }
            beep = AudioClip.Create("MumbleBeep", n, 1, rate, false);
            beep.SetData(data, 0);
            return beep;
        }
    }
}
