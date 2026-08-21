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
        }
    }
}
