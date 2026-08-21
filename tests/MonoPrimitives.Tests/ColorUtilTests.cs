using System;
using Microsoft.Xna.Framework;
using MonoPrimitives;

namespace MonoPrimitives.Tests
{
    /// <summary>Pure-math checks for <see cref="ColorUtil"/> — no GraphicsDevice needed.</summary>
    internal static class ColorUtilTests
    {
        public static void Run(TestResults results)
        {
            results.Check("Color -> HSV -> Color round-trips for every Palette color", () =>
            {
                foreach (Color original in Palette.All)
                {
                    ColorUtil.ToHSV(original, out float h, out float s, out float v);
                    Color back = ColorUtil.FromHSV(h, s, v, original.A);
                    if (Math.Abs(original.R - back.R) > 2 || Math.Abs(original.G - back.G) > 2 || Math.Abs(original.B - back.B) > 2)
                        return $"{original} -> HSV({h:F3},{s:F3},{v:F3}) -> {back} did not round-trip within tolerance";
                }
                return null;
            });

            results.Check("Hex -> Color -> Hex round-trips", () =>
            {
                string[] hexes = { "#FF0000", "#00FF00", "#0000FF", "#1A2B3C" };
                foreach (string hex in hexes)
                {
                    Color c = ColorUtil.FromHex(hex);
                    string back = ColorUtil.ToHex(c);
                    if (!string.Equals(hex, back, StringComparison.OrdinalIgnoreCase))
                        return $"{hex} -> {c} -> {back} did not round-trip";
                }
                return null;
            });

            results.Check("FromHex accepts no-# and lowercase input, and the RRGGBBAA alpha path", () =>
            {
                Color noHash = ColorUtil.FromHex("1A2B3C");
                Color lower = ColorUtil.FromHex("#1a2b3c");
                Color withAlpha = ColorUtil.FromHex("#1A2B3C80");
                if (noHash != lower) return $"no-# and lowercase disagreed: {noHash} vs {lower}";
                if (withAlpha.R != noHash.R || withAlpha.G != noHash.G || withAlpha.B != noHash.B)
                    return $"RRGGBBAA changed RGB unexpectedly: {withAlpha} vs {noHash}";
                if (withAlpha.A != 0x80)
                    return $"RRGGBBAA alpha parsed as {withAlpha.A:X2}, expected 80";
                if (noHash.A != 255)
                    return $"6-digit hex should default to fully opaque, got A={noHash.A}";
                return null;
            });

            results.Check("FromHex throws on null/empty/wrong-length/non-hex input", () =>
            {
                bool Throws(Action action)
                {
                    try { action(); return false; }
                    catch (ArgumentException) { return true; }
                    catch (FormatException) { return true; }
                }
                if (!Throws(() => ColorUtil.FromHex(null))) return "null did not throw";
                if (!Throws(() => ColorUtil.FromHex(""))) return "empty string did not throw";
                if (!Throws(() => ColorUtil.FromHex("#ABC"))) return "3-digit hex (wrong length) did not throw";
                if (!Throws(() => ColorUtil.FromHex("#GGGGGG"))) return "non-hex digits did not throw";
                return null;
            });

            results.Check("Lerp(a, b, 0) == a and Lerp(a, b, 1) == b", () =>
            {
                Color a = Palette.Alizarin, b = Palette.PeterRiver;
                Color at0 = ColorUtil.Lerp(a, b, 0f);
                Color at1 = ColorUtil.Lerp(a, b, 1f);
                return at0 == a && at1 == b ? null : $"Lerp endpoints wrong: t=0 -> {at0} (expected {a}), t=1 -> {at1} (expected {b})";
            });

            results.Check("LerpHSV(a, b, 0) == a and LerpHSV(a, b, 1) == b", () =>
            {
                Color a = Palette.Alizarin, b = Palette.PeterRiver;
                Color at0 = ColorUtil.LerpHSV(a, b, 0f);
                Color at1 = ColorUtil.LerpHSV(a, b, 1f);
                bool closeTo(Color x, Color y) => Math.Abs(x.R - y.R) <= 2 && Math.Abs(x.G - y.G) <= 2 && Math.Abs(x.B - y.B) <= 2;
                return closeTo(at0, a) && closeTo(at1, b) ? null : $"LerpHSV endpoints wrong: t=0 -> {at0} (expected ~{a}), t=1 -> {at1} (expected ~{b})";
            });

            results.Check("Lighten/Darken move value in the expected direction", () =>
            {
                Color mid = new(128, 128, 128);
                ColorUtil.ToHSV(mid, out _, out _, out float baseV);
                ColorUtil.ToHSV(ColorUtil.Lighten(mid, 0.2f), out _, out _, out float lighterV);
                ColorUtil.ToHSV(ColorUtil.Darken(mid, 0.2f), out _, out _, out float darkerV);
                return lighterV > baseV && darkerV < baseV ? null : $"expected lighter > {baseV:F2} > darker, got lighter={lighterV:F2} darker={darkerV:F2}";
            });

            results.Check("Complementary is 0.5 turns away in hue", () =>
            {
                Color c = Palette.Alizarin;
                ColorUtil.ToHSV(c, out float h, out _, out _);
                ColorUtil.ToHSV(ColorUtil.Complementary(c), out float hComp, out _, out _);
                float diff = MathF.Abs(h - hComp);
                diff = MathF.Min(diff, 1f - diff); // hue wraps at 1.0
                return MathF.Abs(diff - 0.5f) < 0.01f ? null : $"expected hue difference of 0.5 turns, got {diff:F3}";
            });

            results.Check("Saturate/Desaturate move saturation in the expected direction", () =>
            {
                Color muted = new(160, 140, 140); // low but nonzero saturation
                ColorUtil.ToHSV(muted, out _, out float baseS, out _);
                ColorUtil.ToHSV(ColorUtil.Saturate(muted, 0.5f), out _, out float moreS, out _);
                ColorUtil.ToHSV(ColorUtil.Desaturate(muted, 0.5f), out _, out float lessS, out _);
                return moreS > baseS && lessS < baseS ? null : $"expected more > {baseS:F2} > less, got more={moreS:F2} less={lessS:F2}";
            });

            results.Check("Invert is its own inverse and maps black<->white", () =>
            {
                Color c = Palette.Alizarin;
                Color back = ColorUtil.Invert(ColorUtil.Invert(c));
                if (back != c) return $"Invert(Invert({c})) = {back}, expected {c}";
                if (ColorUtil.Invert(Color.Black) != Color.White) return $"Invert(black) = {ColorUtil.Invert(Color.Black)}, expected white";
                if (ColorUtil.Invert(Color.White) != Color.Black) return $"Invert(white) = {ColorUtil.Invert(Color.White)}, expected black";
                Color withAlpha = new(10, 20, 30, 77);
                if (ColorUtil.Invert(withAlpha).A != 77) return "Invert changed alpha, expected it unchanged";
                return null;
            });

            results.Check("Contrast(x, 0) is unchanged; Contrast(x, -1) flattens to mid-gray", () =>
            {
                Color c = Palette.PeterRiver;
                Color unchanged = ColorUtil.Contrast(c, 0f);
                if (unchanged != c) return $"Contrast(c, 0) = {unchanged}, expected {c} unchanged";
                Color flat = ColorUtil.Contrast(c, -1f);
                if (flat.R != 127 || flat.G != 127 || flat.B != 127)
                    return $"Contrast(c, -1) = {flat}, expected every channel at 127 (mid-gray)";
                if (flat.A != c.A) return $"Contrast changed alpha: {flat.A} vs {c.A}";
                return null;
            });

            results.Check("Blend modes: Multiply(white,x)==x, Screen(black,x)==x, Additive clamps at 255, alpha always from 'a'", () =>
            {
                Color x = Palette.Sunflower;
                if (ColorUtil.Multiply(Color.White, x) != new Color(x, Color.White.A))
                    return $"Multiply(white, x) = {ColorUtil.Multiply(Color.White, x)}, expected {x} (with white's alpha)";
                if (ColorUtil.Screen(Color.Black, x) != new Color(x, Color.Black.A))
                    return $"Screen(black, x) = {ColorUtil.Screen(Color.Black, x)}, expected {x} (with black's alpha)";
                Color bright = new(200, 200, 200);
                Color additive = ColorUtil.Additive(bright, bright);
                if (additive.R != 255 || additive.G != 255 || additive.B != 255)
                    return $"Additive(200,200) = {additive}, expected clamped to 255 per channel";
                Color a = new(10, 20, 30, 111), b = new(40, 50, 60, 222);
                if (ColorUtil.Multiply(a, b).A != 111 || ColorUtil.Screen(a, b).A != 111 || ColorUtil.Overlay(a, b).A != 111 || ColorUtil.Additive(a, b).A != 111)
                    return "a blend mode did not preserve 'a' argument's own alpha unchanged";
                return null;
            });

            results.Check("Overlay(x, x) increases contrast toward black/white on either side of the midpoint", () =>
            {
                Color dark = new(60, 60, 60), light = new(200, 200, 200);
                Color darkResult = ColorUtil.Overlay(dark, dark);
                Color lightResult = ColorUtil.Overlay(light, light);
                if (darkResult.R >= dark.R) return $"Overlay(dark,dark) should darken further: {darkResult.R} vs {dark.R}";
                if (lightResult.R <= light.R) return $"Overlay(light,light) should lighten further: {lightResult.R} vs {light.R}";
                return null;
            });

            results.Check("Palette.Cycle wraps for indices beyond length and negative indices", () =>
            {
                int n = Palette.Primary.Length;
                if (Palette.Cycle(0) != Palette.Primary[0]) return "Cycle(0) did not match Primary[0]";
                if (Palette.Cycle(n) != Palette.Primary[0]) return $"Cycle({n}) should wrap to Primary[0]";
                if (Palette.Cycle(n + 2) != Palette.Primary[2]) return $"Cycle({n + 2}) should wrap to Primary[2]";
                if (Palette.Cycle(-1) != Palette.Primary[n - 1]) return $"Cycle(-1) should wrap to Primary[{n - 1}] (last), got index for {Palette.Cycle(-1)}";
                return null;
            });

            results.Check("Palette.All/Primary/GradientPairs are non-empty and fully opaque", () =>
            {
                if (Palette.All.Length == 0) return "Palette.All is empty";
                if (Palette.Primary.Length == 0) return "Palette.Primary is empty";
                if (Palette.GradientPairs.Length == 0) return "Palette.GradientPairs is empty";
                foreach (Color c in Palette.All)
                    if (c.A != 255) return $"{c} in Palette.All is not fully opaque";
                foreach (var (inner, outer) in Palette.GradientPairs)
                    if (inner.A != 255 || outer.A != 255) return "a GradientPairs entry is not fully opaque";
                return null;
            });
        }
    }
}
