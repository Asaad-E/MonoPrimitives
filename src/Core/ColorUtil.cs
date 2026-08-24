using System;
using System.Globalization;
using Microsoft.Xna.Framework;

namespace MonoPrimitives
{
    /// <summary>
    /// Color conversions and adjustments that are awkward directly in RGB — hex strings, HSV,
    /// lightening/darkening/saturating, and a hue-aware lerp for vivid color-wheel transitions.
    /// </summary>
    /// <remarks>
    /// Hue is a normalized turn in [0,1) (0 = red, 1/3 = green, 2/3 = blue), matching this
    /// project's own convention elsewhere for angle-like values (see <c>FillCircleSector</c>),
    /// not degrees or radians. A hue-aware lerp exists because a straight RGB lerp between two
    /// saturated hues muddies through gray on the way — see <see cref="LerpHSV"/>.
    /// </remarks>
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

        /// <summary>Inverts each RGB channel (255 minus the value) — a photographic-negative effect, e.g. a hit-flash. Alpha is unchanged.</summary>
        public static Color Invert(Color color) => new((byte)(255 - color.R), (byte)(255 - color.G), (byte)(255 - color.B), color.A);

        /// <summary>
        /// Adjusts contrast around each RGB channel's midpoint (127.5). <paramref name="amount"/>
        /// in [-1,1]: 0 leaves the color unchanged, 1 doubles every channel's distance from the
        /// midpoint (clamped), -1 flattens every channel to exactly mid-gray.
        /// </summary>
        /// <remarks>
        /// Unlike <see cref="Saturate"/>/<see cref="Desaturate"/>, this operates directly on RGB,
        /// not hue/value — a contrast pull can shift a color's apparent hue slightly, which is expected.
        /// </remarks>
        public static Color Contrast(Color color, float amount)
        {
            float factor = 1f + Math.Clamp(amount, -1f, 1f);
            return new Color(ContrastChannel(color.R, factor), ContrastChannel(color.G, factor), ContrastChannel(color.B, factor), color.A);
        }

        private static byte ContrastChannel(byte channel, float factor)
            => (byte)Math.Clamp((channel / 255f - 0.5f) * factor * 255f + 127.5f, 0f, 255f);

        /// <summary>Straight per-channel RGB lerp — a thin, discoverable pass-through to <see cref="Color.Lerp(Color,Color,float)"/>. For vivid hue-to-hue transitions instead of muddying through gray, use <see cref="LerpHSV"/>.</summary>
        public static Color Lerp(Color a, Color b, float t) => Color.Lerp(a, b, t);

        /// <summary>
        /// Interpolates through HSV space instead of RGB, staying vivid the whole way. Hue takes
        /// the SHORT way around the wheel (e.g. red→violet goes backward through magenta, not
        /// forward through the whole spectrum) unless <paramref name="longWay"/> is set.
        /// </summary>
        /// <remarks>
        /// A straight RGB lerp from a saturated red to a saturated blue passes through a muddy
        /// gray/purple at t=0.5; this sweeps the hue wheel instead to avoid that.
        /// </remarks>
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

        // ---------------------------------------------------------------------
        // Blend modes
        // ---------------------------------------------------------------------
        // Pure Color x Color -> Color functions, computing a blended color VALUE to draw
        // normally afterward -- not a GPU blend-state operation (Primitive2DBatch already uses one
        // NonPremultiplied blend state throughout; these are for tinting/layering colors in code,
        // procedural palette mixing, that kind of thing). Alpha is always taken from `a`
        // unchanged -- these blend color, not transparency. Byte math throughout (not the
        // float-based ToHSV/FromHSV round trip the adjustments above use), with a +127 rounding
        // term on every /255 division rather than truncating, same rounding this library already
        // does when going float->byte elsewhere (see Primitives2D.cs's BarycentricColor).

        /// <summary>Multiply blend: darkens -- the result is never lighter than either input. Like stacking two semi-transparent color filters.</summary>
        public static Color Multiply(Color a, Color b)
            => new(
                (byte)((a.R * b.R + 127) / 255),
                (byte)((a.G * b.G + 127) / 255),
                (byte)((a.B * b.B + 127) / 255),
                a.A);

        /// <summary>Screen blend: lightens -- the inverse of <see cref="Multiply"/>, the result is never darker than either input.</summary>
        public static Color Screen(Color a, Color b)
            => new(
                (byte)(255 - ((255 - a.R) * (255 - b.R) + 127) / 255),
                (byte)(255 - ((255 - a.G) * (255 - b.G) + 127) / 255),
                (byte)(255 - ((255 - a.B) * (255 - b.B) + 127) / 255),
                a.A);

        /// <summary>
        /// Overlay blend: <see cref="Multiply"/> where <paramref name="a"/> is dark, <see cref="Screen"/>
        /// where it's light — boosts contrast instead of uniformly darkening or lightening.
        /// <paramref name="a"/> is the base color; <paramref name="b"/> is the color overlaid on it.
        /// </summary>
        /// <remarks>The two arguments aren't interchangeable, unlike <see cref="Multiply"/>/<see cref="Screen"/>.</remarks>
        public static Color Overlay(Color a, Color b)
            => new(OverlayChannel(a.R, b.R), OverlayChannel(a.G, b.G), OverlayChannel(a.B, b.B), a.A);

        private static byte OverlayChannel(byte baseChannel, byte blendChannel)
        {
            int result = baseChannel < 128
                ? (2 * baseChannel * blendChannel + 127) / 255
                : 255 - (2 * (255 - baseChannel) * (255 - blendChannel) + 127) / 255;
            return (byte)Math.Clamp(result, 0, 255);
        }

        /// <summary>Additive (linear dodge) blend: straight per-channel sum, clamped at 255 — brightens aggressively, the standard "glow"/particle-additive look.</summary>
        public static Color Additive(Color a, Color b)
            => new((byte)Math.Min(255, a.R + b.R), (byte)Math.Min(255, a.G + b.G), (byte)Math.Min(255, a.B + b.B), a.A);
    }
}
