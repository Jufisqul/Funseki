using System;
using System.Collections.Generic;
using Funseki.Core;
using UnityEngine;

namespace Funseki.UI
{
    // Placeholder shapes for the HUD, drawn once in code as white sprites and tinted by the theme colors.
    // Used only where HudTheme leaves a sprite empty.
    public static class HudArt
    {
        const int Size = 128;
        static readonly Dictionary<string, Sprite> cache = new();

        public static Sprite Circle => Get("circle", (x, y) => Disc(x, y, 0.5f, 0.5f, 0.48f));

        public static Sprite Ring => Get("ring", (x, y) => Mathf.Min(Disc(x, y, 0.5f, 0.5f, 0.48f), 1f - Disc(x, y, 0.5f, 0.5f, 0.36f)));

        public static Sprite RoundedRect => GetSliced("rounded", 0.22f);

        public static Sprite TimeOfDay(TimeOfDayIcon icon) => icon switch
        {
            // Sun on the horizon, low or high, with rays for the full sun.
            TimeOfDayIcon.Dawn => Get("dawn", (x, y) => Mathf.Max(Horizon(x, y, 0.3f), y > 0.3f ? Disc(x, y, 0.5f, 0.38f, 0.26f) : 0f)),
            TimeOfDayIcon.Sunset => Get("sunset", (x, y) => Mathf.Max(Horizon(x, y, 0.3f), y > 0.3f ? Disc(x, y, 0.5f, 0.24f, 0.3f) : 0f)),
            _ => Get("sun", (x, y) => Mathf.Max(Disc(x, y, 0.5f, 0.5f, 0.24f), Rays(x, y))),
        };

        // A side view of a megaphone: grip, small back, wide cone to the right, two sound arcs.
        public static Sprite Megaphone => Get("megaphone", (x, y) =>
        {
            float a = 0f;
            if (x >= 0.12f && x <= 0.26f && Mathf.Abs(y - 0.55f) <= 0.09f) a = 1f;                       // back
            float t = Mathf.InverseLerp(0.26f, 0.7f, x);
            if (x >= 0.26f && x <= 0.7f && Mathf.Abs(y - 0.55f) <= Mathf.Lerp(0.09f, 0.27f, t)) a = 1f;  // cone
            if (x >= 0.3f && x <= 0.38f && y >= 0.22f && y <= 0.5f) a = 1f;                              // grip
            float r = Mathf.Sqrt((x - 0.62f) * (x - 0.62f) + (y - 0.55f) * (y - 0.55f));
            float ang = Mathf.Abs(Mathf.Atan2(y - 0.55f, x - 0.62f));
            if (ang < 0.7f && (Mathf.Abs(r - 0.22f) < 0.025f || Mathf.Abs(r - 0.32f) < 0.025f)) a = 1f;  // sound
            return a;
        });

        // An open book for the journal notification.
        public static Sprite Book => Get("book", (x, y) =>
        {
            if (y < 0.2f || y > 0.8f || x < 0.12f || x > 0.88f) return 0f;
            if (Mathf.Abs(x - 0.5f) < 0.025f) return 0f;                                                  // spine gap
            float page = x < 0.5f ? (x - 0.12f) / 0.38f : (0.88f - x) / 0.38f;
            bool line = page > 0.2f && page < 0.85f && Mathf.Repeat(y * 10f, 1f) < 0.18f && y > 0.3f && y < 0.7f;
            return line ? 0.35f : 1f;
        });

        static float Disc(float x, float y, float cx, float cy, float r)
        {
            float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            return Mathf.Clamp01((r - d) * Size);
        }

        static float Horizon(float x, float y, float at) => Mathf.Abs(y - at) < 0.03f && x > 0.06f && x < 0.94f ? 1f : 0f;

        static float Rays(float x, float y)
        {
            float dx = x - 0.5f, dy = y - 0.5f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d < 0.32f || d > 0.47f) return 0f;
            float ang = Mathf.Atan2(dy, dx) / (Mathf.PI * 2f) * 8f;
            return Mathf.Abs(ang - Mathf.Round(ang)) < 0.12f ? 1f : 0f;
        }

        static Sprite Get(string key, Func<float, float, float> alpha)
        {
            if (cache.TryGetValue(key, out var s) && s != null) return s;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "Hud_" + key, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[Size * Size];
            for (int j = 0; j < Size; j++)
            for (int i = 0; i < Size; i++)
                px[j * Size + i] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha((i + 0.5f) / Size, (j + 0.5f) / Size)) * 255));
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            s.name = tex.name;
            cache[key] = s;
            return s;
        }

        static Sprite GetSliced(string key, float radius)
        {
            if (cache.TryGetValue(key, out var s) && s != null) return s;
            const int n = 64;
            int r = Mathf.RoundToInt(n * radius);
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Hud_" + key, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float cx = Mathf.Clamp(i + 0.5f, r, n - r), cy = Mathf.Clamp(j + 0.5f, r, n - r);
                float d = Mathf.Sqrt((i + 0.5f - cx) * (i + 0.5f - cx) + (j + 0.5f - cy) * (j + 0.5f - cy));
                px[j * n + i] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d + 0.5f) * 255));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            s.name = tex.name;
            cache[key] = s;
            return s;
        }
    }
}
