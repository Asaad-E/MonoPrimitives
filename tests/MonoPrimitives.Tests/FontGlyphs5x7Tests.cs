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

            results.Check("WrapText: no-ops for text that already fits, or a non-positive maxWidth/pixelSize", () =>
            {
                string text = "SHORT";
                if (FontGlyphs5x7.WrapText(text, maxWidth: 1000f, pixelSize: 2f, glyphSpacing: 1f) != text) return "expected unchanged text when it already fits";
                if (FontGlyphs5x7.WrapText(text, maxWidth: 0f, pixelSize: 2f) != text) return "expected unchanged text for maxWidth <= 0";
                if (FontGlyphs5x7.WrapText(text, maxWidth: 100f, pixelSize: 0f) != text) return "expected unchanged text for pixelSize <= 0";
                if (FontGlyphs5x7.WrapText("", maxWidth: 100f, pixelSize: 2f) != "") return "expected empty text unchanged";
                return null;
            });

            results.Check("WrapText: every resulting line fits within maxWidth (the actual invariant that matters)", () =>
            {
                string text = "the quick brown fox jumps over the lazy dog and then keeps running";
                const float pixelSize = 2f, glyphSpacing = 1f, maxWidth = 60f;
                string wrapped = FontGlyphs5x7.WrapText(text, maxWidth, pixelSize, glyphSpacing);

                foreach (string line in wrapped.Split('\n'))
                {
                    (float width, _) = FontGlyphs5x7.MeasureText(line, pixelSize, glyphSpacing, lineSpacing: 2f);
                    if (width > maxWidth + 1e-3f) return $"line \"{line}\" measures {width}, exceeding maxWidth {maxWidth}";
                }
                if (wrapped.Split('\n').Length < 2) return "expected the text to actually wrap into multiple lines given this maxWidth";
                return null;
            });

            results.Check("WrapText: breaks at a word boundary, not mid-word, when a word-boundary break exists", () =>
            {
                // "AAAA BBBB" at pixelSize=2, glyphSpacing=1: each 4-letter word is 4*(5+1)*2=48px,
                // plus the space. A maxWidth that fits exactly one word plus the space, but not both
                // words, should break right after "AAAA", not partway through either word.
                string wrapped = FontGlyphs5x7.WrapText("AAAA BBBB", maxWidth: 50f, pixelSize: 2f, glyphSpacing: 1f);
                string[] lines = wrapped.Split('\n');
                if (lines.Length != 2) return $"expected exactly 2 lines, got {lines.Length}: \"{wrapped.Replace("\n", "\\n")}\"";
                if (lines[0] != "AAAA" || lines[1] != "BBBB") return $"expected [\"AAAA\",\"BBBB\"], got [\"{lines[0]}\",\"{lines[1]}\"]";
                return null;
            });

            results.Check("WrapText: a single word wider than maxWidth is hard-broken mid-word instead of overflowing", () =>
            {
                string longWord = new string('A', 30); // guaranteed wider than any small maxWidth
                string wrapped = FontGlyphs5x7.WrapText(longWord, maxWidth: 40f, pixelSize: 2f, glyphSpacing: 1f);
                if (!wrapped.Contains('\n')) return $"expected the long word to be hard-broken across multiple lines, got \"{wrapped}\"";

                foreach (string line in wrapped.Split('\n'))
                {
                    (float width, _) = FontGlyphs5x7.MeasureText(line, pixelSize: 2f, glyphSpacing: 1f, lineSpacing: 2f);
                    if (width > 40f + 1e-3f) return $"hard-broken line \"{line}\" still exceeds maxWidth: {width}";
                }
                // Nothing lost: removing the inserted newlines should reconstruct the original word exactly.
                if (wrapped.Replace("\n", "") != longWord) return $"expected no characters lost/added by the hard break, got \"{wrapped.Replace("\n", "")}\"";
                return null;
            });

            results.Check("WrapText: existing '\\n' are preserved as forced breaks, each paragraph wrapped independently", () =>
            {
                string text = "AAAA BBBB\nCCCC DDDD";
                string wrapped = FontGlyphs5x7.WrapText(text, maxWidth: 50f, pixelSize: 2f, glyphSpacing: 1f);
                string[] lines = wrapped.Split('\n');
                // Each original paragraph (AAAA BBBB / CCCC DDDD) wraps into 2 lines on its own (same
                // case as the word-boundary test above), so 2 paragraphs -> 4 lines total.
                if (lines.Length != 4) return $"expected 4 lines (2 paragraphs x 2 wrapped lines each), got {lines.Length}: \"{wrapped.Replace("\n", "\\n")}\"";
                if (lines[0] != "AAAA" || lines[1] != "BBBB" || lines[2] != "CCCC" || lines[3] != "DDDD")
                    return $"unexpected line contents: \"{wrapped.Replace("\n", "\\n")}\"";
                return null;
            });
        }
    }
}
