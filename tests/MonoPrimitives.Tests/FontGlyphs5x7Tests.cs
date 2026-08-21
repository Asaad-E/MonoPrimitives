using System;
using System.Collections.Generic;
using System.Linq;
using MonoPrimitives;

namespace MonoPrimitives.Tests
{
    /// <summary>Pure-math checks for <see cref="FontGlyphs5x7"/>'s glyph bitmap data — no GraphicsDevice needed.</summary>
    internal static class FontGlyphs5x7Tests
    {
        // "x-height" lowercase letters -- no ascender, should occupy exactly rows 2-6 (5 rows),
        // sitting on the same baseline as every other lowercase letter. Regression coverage for
        // the a/e/g/u bug (each was one row taller, from a duplicated bitmap row) -- see DECISIONS.md.
        private static readonly char[] XHeightLetters = { 'a', 'c', 'e', 'g', 'm', 'n', 'o', 'p', 'q', 'r', 's', 'u', 'v', 'w', 'x', 'y', 'z' };

        // Ascender letters -- should span the full 7-row cell (rows 0-6).
        private static readonly char[] AscenderLetters = { 'b', 'd', 'f', 'h', 'k', 'l', 't' };

        // "Dot + gap + body" letters -- row0 is a dot, row1 is blank, body occupies rows 2-6.
        private static readonly char[] DottedLetters = { 'i', 'j' };

        private static (int First, int Last) RowSpan(byte[] glyph)
        {
            int first = -1, last = -1;
            for (int row = 0; row < glyph.Length; row++)
            {
                if (glyph[row] == 0) continue;
                if (first < 0) first = row;
                last = row;
            }
            return (first, last);
        }

        public static void Run(TestResults results)
        {
            results.Check("Every glyph is exactly GlyphHeight (7) rows tall", () =>
            {
                foreach (char c in Enumerable.Range(32, 95).Select(i => (char)i).Concat("ñÑáéíóúüÁÉÍÓÚÜ¿¡"))
                {
                    byte[] glyph = FontGlyphs5x7.GetGlyph(c);
                    if (glyph.Length != FontGlyphs5x7.GlyphHeight)
                        return $"'{c}' has {glyph.Length} rows, expected {FontGlyphs5x7.GlyphHeight}";
                }
                return null;
            });

            results.Check("x-height lowercase letters occupy exactly rows 2-6 (regression: a/e/g/u used to be one row taller)", () =>
            {
                var failures = new List<string>();
                foreach (char c in XHeightLetters)
                {
                    (int first, int last) = RowSpan(FontGlyphs5x7.GetGlyph(c));
                    if (first != 2 || last != 6) failures.Add($"'{c}': rows {first}-{last}, expected 2-6");
                }
                return failures.Count == 0 ? null : string.Join(", ", failures);
            });

            results.Check("Ascender letters (b/d/f/h/k/l/t) span the full 0-6 row range", () =>
            {
                var failures = new List<string>();
                foreach (char c in AscenderLetters)
                {
                    (int first, int last) = RowSpan(FontGlyphs5x7.GetGlyph(c));
                    if (first != 0 || last != 6) failures.Add($"'{c}': rows {first}-{last}, expected 0-6");
                }
                return failures.Count == 0 ? null : string.Join(", ", failures);
            });

            results.Check("i/j are a dot (row 0) + gap (row 1) + rows 2-6 body, same baseline as the x-height letters", () =>
            {
                var failures = new List<string>();
                foreach (char c in DottedLetters)
                {
                    byte[] glyph = FontGlyphs5x7.GetGlyph(c);
                    if (glyph[0] == 0) failures.Add($"'{c}': row 0 (the dot) is blank");
                    if (glyph[1] != 0) failures.Add($"'{c}': row 1 (the gap) is not blank");
                    (int first, int last) = (2, 6);
                    for (int row = first; row <= last; row++)
                        if (glyph[row] == 0) { failures.Add($"'{c}': body row {row} is unexpectedly blank"); break; }
                }
                return failures.Count == 0 ? null : string.Join(", ", failures);
            });

            results.Check("Every uppercase letter and digit spans the full 0-6 row range", () =>
            {
                var failures = new List<string>();
                foreach (char c in "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789")
                {
                    (int first, int last) = RowSpan(FontGlyphs5x7.GetGlyph(c));
                    if (first != 0 || last != 6) failures.Add($"'{c}': rows {first}-{last}, expected 0-6");
                }
                return failures.Count == 0 ? null : string.Join(", ", failures);
            });

            // Regression test for the fixed 'h': its crossbar (row 3) used to stop one column short
            // of the right leg (0b11110), leaving the leg diagonally, not directly, connected to the
            // arch -- a visible "floating leg" notch. Fixed to a full-width crossbar (0b11111).
            results.Check("'h' has a full-width crossbar connecting both legs to the arch", () =>
            {
                byte[] h = FontGlyphs5x7.GetGlyph('h');
                return h[3] == 0b11111 ? null : $"'h' row 3 (the crossbar) is {Convert.ToString(h[3], 2).PadLeft(5, '0')}, expected 11111 (full width)";
            });

            results.Check("GetGlyph falls back to a hollow box for a character with no glyph", () =>
            {
                byte[] fallback = FontGlyphs5x7.GetGlyph('￿'); // guaranteed not to have an assigned glyph
                byte[] expected = { 0b11111, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b11111 };
                for (int i = 0; i < expected.Length; i++)
                    if (fallback[i] != expected[i]) return $"fallback row {i} = {Convert.ToString(fallback[i], 2)}, expected {Convert.ToString(expected[i], 2)}";
                return null;
            });

            results.Check("AdvanceFor scales with pixelSize and respects SpaceWidthScale", () =>
            {
                float originalScale = FontGlyphs5x7.SpaceWidthScale;
                try
                {
                    float normal = FontGlyphs5x7.AdvanceFor('A', pixelSize: 4f, glyphSpacing: 1f);
                    float doubled = FontGlyphs5x7.AdvanceFor('A', pixelSize: 8f, glyphSpacing: 1f); // same glyphSpacing -- only pixelSize doubles
                    if (MathF.Abs(doubled - normal * 2f) > 1e-4f) return $"advance did not scale linearly with pixelSize: {normal} -> {doubled}";

                    FontGlyphs5x7.SpaceWidthScale = 0.5f;
                    float space = FontGlyphs5x7.AdvanceFor(' ', pixelSize: 1f, glyphSpacing: 0f);
                    if (MathF.Abs(space - FontGlyphs5x7.GlyphWidth * 0.5f) > 1e-4f)
                        return $"space advance {space} did not reflect SpaceWidthScale=0.5 (expected {FontGlyphs5x7.GlyphWidth * 0.5f})";
                    return null;
                }
                finally { FontGlyphs5x7.SpaceWidthScale = originalScale; }
            });

            results.Check("MeasureText: multi-line height grows per line, width matches the longest line, empty text is (0,0)", () =>
            {
                (float w0, float h0) = FontGlyphs5x7.MeasureText("", pixelSize: 2f, glyphSpacing: 1f, lineSpacing: 2f);
                if (w0 != 0f || h0 != 0f) return $"empty text measured ({w0},{h0}), expected (0,0)";

                (float wOneLine, float hOneLine) = FontGlyphs5x7.MeasureText("AB", pixelSize: 2f, glyphSpacing: 1f, lineSpacing: 2f);
                (float wTwoLines, float hTwoLines) = FontGlyphs5x7.MeasureText("AB\nA", pixelSize: 2f, glyphSpacing: 1f, lineSpacing: 2f);

                if (hTwoLines <= hOneLine) return $"two-line height ({hTwoLines}) should exceed one-line height ({hOneLine})";
                if (MathF.Abs(wTwoLines - wOneLine) > 1e-4f) return $"two-line width ({wTwoLines}) should match the longer first line's width ({wOneLine})";
                return null;
            });
        }
    }
}
