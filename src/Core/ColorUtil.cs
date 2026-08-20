using System;
using System.Globalization;
using Microsoft.Xna.Framework;

namespace MonoPrimitives
{
    /// <summary>
    /// Color conversions and adjustments that are awkward directly in RGB — hex strings, HSV,
    /// lightening/darkening/saturating, and a hue-aware lerp for vivid color-wheel transitions
    /// (a straight RGB lerp between two saturated hues muddies through gray on the way).
    /// Hue is a normalized turn in [0,1) (0 = red, 1/3 = green, 2/3 = blue), matching this
    /// project's own convention elsewhere for angle-like values (see <c>DrawCircleSector</c>),
    /// not degrees or radians.
    /// </summary>
    public static class ColorUtil
    {
        // ---------------------------------------------------------------------
        // Hex
        // ---------------------------------------------------------------------

        /// <summary>
        /// Parses a hex color string: <c>"#RRGGBB"</c>, <c>"RRGGBB"</c>, <c>"#RRGGBBAA"</c>, or
        /// <c>"RRGGBBAA"</c> (a leading <c>#</c> is optional either way; missing alpha defaults
        /// to fully opaque).
        /// </summary>
        public static Color FromHex(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                throw new ArgumentException("Hex color string is null or empty.", nameof(hex));

            ReadOnlySpan<char> s = hex.AsSpan().TrimStart('#');
            if (s.Length != 6 && s.Length != 8)
                throw new ArgumentException($"Hex color must be 6 (RRGGBB) or 8 (RRGGBBAA) digits, got {s.Length}: \"{hex}\".", nameof(hex));

            byte r = byte.Parse(s.Slice(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            byte g = byte.Parse(s.Slice(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            byte b = byte.Parse(s.Slice(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            byte a = s.Length == 8 ? byte.Parse(s.Slice(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) : (byte)255;
            return new Color(r, g, b, a);
        }

        /// <summary>Formats as <c>"#RRGGBB"</c>, or <c>"#RRGGBBAA"</c> if <paramref name="includeAlpha"/> is true.</summary>
        public static string ToHex(Color color, bool includeAlpha = false)
            => includeAlpha
                ? $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}"
                : $"#{color.R:X2}{color.G:X2}{color.B:X2}";

        // ---------------------------------------------------------------------
        // HSV
        // ---------------------------------------------------------------------

        /// <summary>Builds a color from hue (turns, [0,1)), saturation, and value (both [0,1]).</summary>
        public static Color FromHSV(float h, float s, float v, byte alpha = 255)
        {
            s = Math.Clamp(s, 0f, 1f);
            v = Math.Clamp(v, 0f, 1f);
            h -= MathF.Floor(h); // wrap into [0,1)

            float h6 = h * 6f;
            int i = (int)MathF.Floor(h6) % 6;
            if (i < 0) i += 6;
            float f = h6 - MathF.Floor(h6);

            float p = v * (1f - s);
            float q = v * (1f - f * s);
            float t = v * (1f - (1f - f) * s);

            (float r, float g, float b) = i switch
            {
                0 => (v, t, p),
                1 => (q, v, p),
                2 => (p, v, t),
                3 => (p, q, v),
                4 => (t, p, v),
                _ => (v, p, q),
            };

            return new Color(r, g, b, alpha / 255f);
        }

        /// <summary>Decomposes a color into hue (turns, [0,1)), saturation, and value (both [0,1]) — the inverse of <see cref="FromHSV"/>.</summary>
        public static void ToHSV(Color color, out float h, out float s, out float v)
        {
            float r = color.R / 255f, g = color.G / 255f, b = color.B / 255f;
            float max = MathF.Max(r, MathF.Max(g, b));
            float min = MathF.Min(r, MathF.Min(g, b));
            float delta = max - min;

            v = max;
            s = max <= 0f ? 0f : delta / max;

            if (delta <= 1e-6f)
            {
                h = 0f;
                return;
            }

            float hue;
            if (max == r) hue = ((g - b) / delta) % 6f;
            else if (max == g) hue = (b - r) / delta + 2f;
            else hue = (r - g) / delta + 4f;

            hue /= 6f;
            if (hue < 0f) hue += 1f;
            h = hue;
        }

        // ---------------------------------------------------------------------
        // Adjustments
        // ---------------------------------------------------------------------

        /// <summary>Moves value (brightness) toward 1 by <paramref name="amount"/> (0 = unchanged, 1 = white), preserving hue/saturation.</summary>
        public static Color Lighten(Color color, float amount)
        {
            ToHSV(color, out float h, out float s, out float v);
            return FromHSV(h, s, v + (1f - v) * Math.Clamp(amount, 0f, 1f), color.A);
        }

        /// <summary>Moves value (brightness) toward 0 by <paramref name="amount"/> (0 = unchanged, 1 = black), preserving hue/saturation.</summary>
        public static Color Darken(Color color, float amount)
        {
            ToHSV(color, out float h, out float s, out float v);
            return FromHSV(h, s, v * (1f - Math.Clamp(amount, 0f, 1f)), color.A);
        }

        /// <summary>Moves saturation toward 1 by <paramref name="amount"/> (0 = unchanged, 1 = fully saturated), preserving hue/value.</summary>
        public static Color Saturate(Color color, float amount)
        {
            ToHSV(color, out float h, out float s, out float v);
            return FromHSV(h, s + (1f - s) * Math.Clamp(amount, 0f, 1f), v, color.A);
        }

        /// <summary>Moves saturation toward 0 by <paramref name="amount"/> (0 = unchanged, 1 = grayscale), preserving hue/value.</summary>
        public static Color Desaturate(Color color, float amount)
        {
            ToHSV(color, out float h, out float s, out float v);
            return FromHSV(h, s * (1f - Math.Clamp(amount, 0f, 1f)), v, color.A);
        }

        /// <summary>The opposite hue (half a turn around the color wheel), same saturation/value — a color that reads as clearly distinct at a glance.</summary>
        public static Color Complementary(Color color)
        {
            ToHSV(color, out float h, out float s, out float v);
            return FromHSV(h + 0.5f, s, v, color.A);
        }

        /// <summary>Straight per-channel RGB lerp — a thin, discoverable pass-through to <see cref="Color.Lerp(Color,Color,float)"/>. For vivid hue-to-hue transitions instead of muddying through gray, use <see cref="LerpHSV"/>.</summary>
        public static Color Lerp(Color a, Color b, float t) => Color.Lerp(a, b, t);

        /// <summary>
        /// Interpolates through HSV space instead of RGB — a straight RGB lerp from a saturated
        /// red to a saturated blue passes through a muddy gray/purple at t=0.5; this instead
        /// sweeps the hue wheel, staying vivid the whole way. Hue takes the SHORT way around the
        /// wheel (e.g. red→violet goes backward through magenta, not forward through the whole
        /// spectrum) unless <paramref name="longWay"/> is set.
        /// </summary>
        public static Color LerpHSV(Color a, Color b, float t, bool longWay = false)
        {
            ToHSV(a, out float h1, out float s1, out float v1);
            ToHSV(b, out float h2, out float s2, out float v2);
            t = Math.Clamp(t, 0f, 1f);

            float delta = h2 - h1;
            if (!longWay)
            {
                if (delta > 0.5f) delta -= 1f;
                else if (delta < -0.5f) delta += 1f;
            }
            else
            {
                if (delta is > -0.5f and < 0.5f) delta += delta >= 0f ? -1f : 1f;
            }

            float h = h1 + delta * t;
            float s = s1 + (s2 - s1) * t;
            float v = v1 + (v2 - v1) * t;
            byte alpha = (byte)(a.A + (b.A - a.A) * t);
            return FromHSV(h, s, v, alpha);
        }
    }
}
