using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Funseki.Lessons.Fizra
{
    // Placeholder whistle sounds until the real clips are picked (spec content list: «Звуки свистков (5)»).
    // Pattern tokens: s short, l long, t trill, x sharp, k kettle, _ pause; notes: «1568:0.4» (Hz:seconds).
    // The finger version is lower, wobblier and breathier — the «без свистка» mode.
    public static class WhistleSynth
    {
        const int Rate = 44100;
        static readonly Dictionary<string, AudioClip> cache = new();

        public static AudioClip Make(string pattern, float baseHz, bool finger)
        {
            if (string.IsNullOrWhiteSpace(pattern)) return null;
            string key = $"{pattern}|{baseHz}|{finger}";
            if (cache.TryGetValue(key, out var clip) && clip != null) return clip;

            var samples = new List<float>();
            var noise = new System.Random(pattern.GetHashCode());
            foreach (var token in pattern.Split(' '))
            {
                if (token.Length == 0) continue;
                if (token.Contains(":"))
                {
                    var parts = token.Split(':');
                    if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float hz)
                        && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float sec))
                        Tone(samples, finger ? hz * 0.6f : hz, sec, finger ? 9f : 0f, finger ? 0.04f : 0.01f, finger, noise);
                    continue;
                }
                switch (token[0])
                {
                    case 's': Tone(samples, baseHz, 0.14f, finger ? 8f : 0f, 0.02f, finger, noise); break;
                    case 'l': Tone(samples, baseHz * 0.95f, 0.75f, finger ? 7f : 0f, 0.015f, finger, noise); break;
                    case 't': Tone(samples, baseHz, 0.7f, 22f, 0.08f, finger, noise); break;
                    case 'x': Tone(samples, baseHz * 1.25f, 0.08f, 0f, 0.01f, finger, noise); break;
                    case 'k': Kettle(samples, baseHz, finger, noise); break;
                    case '_': Silence(samples, 0.1f); break;
                }
                Silence(samples, 0.06f);
            }

            clip = AudioClip.Create($"Whistle_{pattern}{(finger ? "_finger" : "")}", samples.Count, 1, Rate, false);
            clip.SetData(samples.ToArray(), 0);
            cache[key] = clip;
            return clip;
        }

        // A pea whistle: a tone with a flutter (the pea), optional vibrato, soft attack and release.
        static void Tone(List<float> s, float hz, float seconds, float vibratoHz, float vibratoDepth, bool finger, System.Random noise)
        {
            int n = Mathf.RoundToInt(seconds * Rate);
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float vib = vibratoHz > 0f ? 1f + vibratoDepth * Mathf.Sin(2f * Mathf.PI * vibratoHz * t) : 1f;
                float flutter = finger ? 1f : 1f + 0.012f * Mathf.Sin(2f * Mathf.PI * 38f * t);
                phase += 2f * Mathf.PI * hz * vib * flutter / Rate;
                float env = Mathf.Min(1f, i / (0.012f * Rate)) * Mathf.Min(1f, (n - i) / (0.03f * Rate));
                float v = Mathf.Sin(phase);
                if (finger) v = v * 0.75f + ((float)noise.NextDouble() * 2f - 1f) * 0.25f;
                s.Add(v * env * 0.5f);
            }
        }

        // A kettle coming to the boil: a long rising whistle.
        static void Kettle(List<float> s, float hz, bool finger, System.Random noise)
        {
            int n = Mathf.RoundToInt(1.4f * Rate);
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                phase += 2f * Mathf.PI * Mathf.Lerp(hz * 0.7f, hz * 1.1f, k) / Rate;
                float env = Mathf.Clamp01(k * 3f) * Mathf.Min(1f, (n - i) / (0.05f * Rate));
                float v = Mathf.Sin(phase) * 0.8f + ((float)noise.NextDouble() * 2f - 1f) * 0.2f;
                s.Add(v * env * 0.45f);
            }
        }

        static void Silence(List<float> s, float seconds)
        {
            int n = Mathf.RoundToInt(seconds * Rate);
            for (int i = 0; i < n; i++) s.Add(0f);
        }
    }
}
