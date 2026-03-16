using System;
using SkiaSharp;

namespace ModernFormsNext
{
    internal static class ColorHelper
    {
        public static SKColor FromHsv (float h, float s, float v, byte a = 255)
        {
            h = NormalizeHue (h);
            s = Clamp01 (s);
            v = Clamp01 (v);

            if (s <= 0f) {
                byte gray = ToByte (v * 255f);
                return new SKColor (gray, gray, gray, a);
            }

            float hh = h / 60f;
            int sector = (int)MathF.Floor (hh);
            float fraction = hh - sector;

            float p = v * (1f - s);
            float q = v * (1f - s * fraction);
            float t = v * (1f - s * (1f - fraction));

            float r, g, b;

            switch (sector) {
                case 0:
                    r = v; g = t; b = p;
                    break;
                case 1:
                    r = q; g = v; b = p;
                    break;
                case 2:
                    r = p; g = v; b = t;
                    break;
                case 3:
                    r = p; g = q; b = v;
                    break;
                case 4:
                    r = t; g = p; b = v;
                    break;
                default:
                    r = v; g = p; b = q;
                    break;
            }

            return new SKColor (ToByte (r * 255f), ToByte (g * 255f), ToByte (b * 255f), a);
        }

        public static void ToHsv (SKColor color, out float h, out float s, out float v)
        {
            float r = color.Red / 255f;
            float g = color.Green / 255f;
            float b = color.Blue / 255f;

            float max = MathF.Max (r, MathF.Max (g, b));
            float min = MathF.Min (r, MathF.Min (g, b));
            float delta = max - min;

            v = max;
            s = max <= 0f ? 0f : delta / max;

            if (delta <= 0f) {
                h = 0f;
                return;
            }

            if (max == r)
                h = 60f * (((g - b) / delta) % 6f);
            else if (max == g)
                h = 60f * (((b - r) / delta) + 2f);
            else
                h = 60f * (((r - g) / delta) + 4f);

            if (h < 0f)
                h += 360f;
        }

        public static void ToHsl (SKColor color, out float h, out float s, out float l)
        {
            float r = color.Red / 255f;
            float g = color.Green / 255f;
            float b = color.Blue / 255f;

            float max = MathF.Max (r, MathF.Max (g, b));
            float min = MathF.Min (r, MathF.Min (g, b));
            float delta = max - min;

            l = (max + min) / 2f;

            if (delta <= 0f) {
                h = 0f;
                s = 0f;
                return;
            }

            s = l > 0.5f
                ? delta / (2f - max - min)
                : delta / (max + min);

            if (max == r)
                h = 60f * (((g - b) / delta) % 6f);
            else if (max == g)
                h = 60f * (((b - r) / delta) + 2f);
            else
                h = 60f * (((r - g) / delta) + 4f);

            if (h < 0f)
                h += 360f;
        }

        public static string ToHex (SKColor color, bool includeAlpha = true)
        {
            return includeAlpha
                ? $"#{color.Alpha:X2}{color.Red:X2}{color.Green:X2}{color.Blue:X2}"
                : $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}";
        }

        public static float NormalizeHue (float hue)
        {
            while (hue < 0f)
                hue += 360f;

            while (hue >= 360f)
                hue -= 360f;

            return hue;
        }

        public static float Clamp01 (float value)
            => MathF.Max (0f, MathF.Min (1f, value));

        public static byte ToByte (float value)
            => (byte)Math.Clamp ((int)MathF.Round (value), 0, 255);
    }
}
