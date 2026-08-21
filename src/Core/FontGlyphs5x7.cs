#nullable enable

using System.Collections.Generic;

namespace MonoPrimitives
{
    /// <summary>
    /// The raw bitmap data behind both libraries' 5x7 debug font — no rendering here (that's
    /// <c>DebugFont5x7</c> in each library, which draws it differently: flat rectangles in 2D
    /// screen space, camera-facing billboard quads in 3D world space). Covers full basic ASCII
    /// (32-126) plus the Spanish characters (ñ Ñ á é í ó ú Á É Í Ó Ú ü Ü ¿ ¡). Not production
    /// typography: lowercase letters have no true descenders (g, j, p, q, y are compressed to
    /// fit the same 7-row cell as everything else), and unknown characters fall back to a
    /// hollow box instead of silently vanishing.
    /// </summary>
    public static class FontGlyphs5x7
    {
        /// <summary>Every glyph is 5 columns wide, before any <c>pixelSize</c> scaling a caller applies.</summary>
        public const int GlyphWidth = 5;

        /// <summary>Every glyph is 7 rows tall, before any <c>pixelSize</c> scaling a caller applies.</summary>
        public const int GlyphHeight = 7;

        // Each glyph: 7 rows, top to bottom. Each row's lowest 5 bits are the columns,
        // left to right (bit 4 = leftmost column, bit 0 = rightmost) — so the binary
        // literal visually matches the row's "on" pixels when read left to right.
        private static readonly Dictionary<char, byte[]> Glyphs = new()
        {
            // ---- space & punctuation (0x20-0x2F) ----
            [' '] = new byte[] { 0b00000, 0b00000, 0b00000, 0b00000, 0b00000, 0b00000, 0b00000 },
            ['!'] = new byte[] { 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00000, 0b00100 },
            ['"'] = new byte[] { 0b01010, 0b01010, 0b01010, 0b00000, 0b00000, 0b00000, 0b00000 },
            ['#'] = new byte[] { 0b01010, 0b01010, 0b11111, 0b01010, 0b11111, 0b01010, 0b01010 },
            ['$'] = new byte[] { 0b00100, 0b01111, 0b10100, 0b01110, 0b00101, 0b11110, 0b00100 },
            ['%'] = new byte[] { 0b11001, 0b11010, 0b00010, 0b00100, 0b01000, 0b01011, 0b10011 },
            ['&'] = new byte[] { 0b01100, 0b10010, 0b10100, 0b01000, 0b10101, 0b10010, 0b01101 },
            ['\''] = new byte[] { 0b00100, 0b00100, 0b01000, 0b00000, 0b00000, 0b00000, 0b00000 },
            ['('] = new byte[] { 0b00010, 0b00100, 0b01000, 0b01000, 0b01000, 0b00100, 0b00010 },
            [')'] = new byte[] { 0b01000, 0b00100, 0b00010, 0b00010, 0b00010, 0b00100, 0b01000 },
            ['*'] = new byte[] { 0b00000, 0b00100, 0b10101, 0b01110, 0b10101, 0b00100, 0b00000 },
            ['+'] = new byte[] { 0b00000, 0b00100, 0b00100, 0b11111, 0b00100, 0b00100, 0b00000 },
            [','] = new byte[] { 0b00000, 0b00000, 0b00000, 0b00000, 0b00110, 0b00100, 0b01000 },
            ['-'] = new byte[] { 0b00000, 0b00000, 0b00000, 0b11111, 0b00000, 0b00000, 0b00000 },
            ['.'] = new byte[] { 0b00000, 0b00000, 0b00000, 0b00000, 0b00000, 0b01100, 0b01100 },
            ['/'] = new byte[] { 0b00001, 0b00010, 0b00100, 0b00100, 0b01000, 0b10000, 0b10000 },

            // ---- digits (0x30-0x39) ----
            ['0'] = new byte[] { 0b01110, 0b10001, 0b10011, 0b10101, 0b11001, 0b10001, 0b01110 },
            ['1'] = new byte[] { 0b00100, 0b01100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110 },
            ['2'] = new byte[] { 0b01110, 0b10001, 0b00001, 0b00010, 0b00100, 0b01000, 0b11111 },
            ['3'] = new byte[] { 0b11111, 0b00010, 0b00100, 0b00010, 0b00001, 0b10001, 0b01110 },
            ['4'] = new byte[] { 0b00010, 0b00110, 0b01010, 0b10010, 0b11111, 0b00010, 0b00010 },
            ['5'] = new byte[] { 0b11111, 0b10000, 0b11110, 0b00001, 0b00001, 0b10001, 0b01110 },
            ['6'] = new byte[] { 0b00110, 0b01000, 0b10000, 0b11110, 0b10001, 0b10001, 0b01110 },
            ['7'] = new byte[] { 0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b01000, 0b01000 },
            ['8'] = new byte[] { 0b01110, 0b10001, 0b10001, 0b01110, 0b10001, 0b10001, 0b01110 },
            ['9'] = new byte[] { 0b01110, 0b10001, 0b10001, 0b01111, 0b00001, 0b00010, 0b01100 },

            // ---- punctuation (0x3A-0x40) ----
            [':'] = new byte[] { 0b00000, 0b01100, 0b01100, 0b00000, 0b01100, 0b01100, 0b00000 },
            [';'] = new byte[] { 0b00000, 0b01100, 0b01100, 0b00000, 0b00110, 0b00100, 0b01000 },
            ['<'] = new byte[] { 0b00010, 0b00100, 0b01000, 0b10000, 0b01000, 0b00100, 0b00010 },
            ['='] = new byte[] { 0b00000, 0b00000, 0b11111, 0b00000, 0b11111, 0b00000, 0b00000 },
            ['>'] = new byte[] { 0b01000, 0b00100, 0b00010, 0b00001, 0b00010, 0b00100, 0b01000 },
            ['?'] = new byte[] { 0b01110, 0b10001, 0b00001, 0b00010, 0b00100, 0b00000, 0b00100 },
            ['@'] = new byte[] { 0b01110, 0b10001, 0b10111, 0b10101, 0b10111, 0b10000, 0b01111 },

            // ---- uppercase (0x41-0x5A) ----
            ['A'] = new byte[] { 0b01110, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001 },
            ['B'] = new byte[] { 0b11110, 0b10001, 0b10001, 0b11110, 0b10001, 0b10001, 0b11110 },
            ['C'] = new byte[] { 0b01111, 0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b01111 },
            ['D'] = new byte[] { 0b11100, 0b10010, 0b10001, 0b10001, 0b10001, 0b10010, 0b11100 },
            ['E'] = new byte[] { 0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b11111 },
            ['F'] = new byte[] { 0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b10000 },
            ['G'] = new byte[] { 0b01111, 0b10000, 0b10000, 0b10011, 0b10001, 0b10001, 0b01111 },
            ['H'] = new byte[] { 0b10001, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001 },
            ['I'] = new byte[] { 0b01110, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110 },
            ['J'] = new byte[] { 0b00111, 0b00010, 0b00010, 0b00010, 0b00010, 0b10010, 0b01100 },
            ['K'] = new byte[] { 0b10001, 0b10010, 0b10100, 0b11000, 0b10100, 0b10010, 0b10001 },
            ['L'] = new byte[] { 0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b11111 },
            ['M'] = new byte[] { 0b10001, 0b11011, 0b10101, 0b10101, 0b10001, 0b10001, 0b10001 },
            ['N'] = new byte[] { 0b10001, 0b11001, 0b10101, 0b10101, 0b10011, 0b10001, 0b10001 },
            ['O'] = new byte[] { 0b01110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110 },
            ['P'] = new byte[] { 0b11110, 0b10001, 0b10001, 0b11110, 0b10000, 0b10000, 0b10000 },
            ['Q'] = new byte[] { 0b01110, 0b10001, 0b10001, 0b10001, 0b10101, 0b10010, 0b01101 },
            ['R'] = new byte[] { 0b11110, 0b10001, 0b10001, 0b11110, 0b10100, 0b10010, 0b10001 },
            ['S'] = new byte[] { 0b01111, 0b10000, 0b10000, 0b01110, 0b00001, 0b00001, 0b11110 },
            ['T'] = new byte[] { 0b11111, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100 },
            ['U'] = new byte[] { 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110 },
            ['V'] = new byte[] { 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01010, 0b00100 },
            ['W'] = new byte[] { 0b10001, 0b10001, 0b10001, 0b10101, 0b10101, 0b10101, 0b01010 },
            ['X'] = new byte[] { 0b10001, 0b10001, 0b01010, 0b00100, 0b01010, 0b10001, 0b10001 },
            ['Y'] = new byte[] { 0b10001, 0b10001, 0b01010, 0b00100, 0b00100, 0b00100, 0b00100 },
            ['Z'] = new byte[] { 0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b10000, 0b11111 },

            // ---- punctuation (0x5B-0x60) ----
            ['['] = new byte[] { 0b01110, 0b01000, 0b01000, 0b01000, 0b01000, 0b01000, 0b01110 },
            ['\\'] = new byte[] { 0b10000, 0b01000, 0b00100, 0b00100, 0b00010, 0b00001, 0b00001 },
            [']'] = new byte[] { 0b01110, 0b00010, 0b00010, 0b00010, 0b00010, 0b00010, 0b01110 },
            ['^'] = new byte[] { 0b00100, 0b01010, 0b10001, 0b00000, 0b00000, 0b00000, 0b00000 },
            ['_'] = new byte[] { 0b00000, 0b00000, 0b00000, 0b00000, 0b00000, 0b00000, 0b11111 },
            ['`'] = new byte[] { 0b01000, 0b00100, 0b00000, 0b00000, 0b00000, 0b00000, 0b00000 },

            // ---- lowercase (0x61-0x7A) ----
            ['a'] = new byte[] { 0b00000, 0b00000, 0b01110, 0b00001, 0b01111, 0b10001, 0b01111 },
            ['b'] = new byte[] { 0b10000, 0b10000, 0b10000, 0b11110, 0b10001, 0b10001, 0b11110 },
            ['c'] = new byte[] { 0b00000, 0b00000, 0b01111, 0b10000, 0b10000, 0b10000, 0b01111 },
            ['d'] = new byte[] { 0b00001, 0b00001, 0b00001, 0b01111, 0b10001, 0b10001, 0b01111 },
            ['e'] = new byte[] { 0b00000, 0b00000, 0b01110, 0b10001, 0b11111, 0b10000, 0b01111 },
            ['f'] = new byte[] { 0b00110, 0b01001, 0b01000, 0b11100, 0b01000, 0b01000, 0b01000 },
            ['g'] = new byte[] { 0b00000, 0b00000, 0b01111, 0b10001, 0b01111, 0b00001, 0b01110 },
            ['h'] = new byte[] { 0b10000, 0b10000, 0b10000, 0b11110, 0b10001, 0b10001, 0b10001 },
            ['i'] = new byte[] { 0b00100, 0b00000, 0b01100, 0b00100, 0b00100, 0b00100, 0b01110 },
            ['j'] = new byte[] { 0b00010, 0b00000, 0b00110, 0b00010, 0b00010, 0b10010, 0b01100 },
            ['k'] = new byte[] { 0b10000, 0b10000, 0b10010, 0b10100, 0b11000, 0b10100, 0b10010 },
            ['l'] = new byte[] { 0b01100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110 },
            ['m'] = new byte[] { 0b00000, 0b00000, 0b11010, 0b10101, 0b10101, 0b10101, 0b10101 },
            ['n'] = new byte[] { 0b00000, 0b00000, 0b11110, 0b10001, 0b10001, 0b10001, 0b10001 },
            ['o'] = new byte[] { 0b00000, 0b00000, 0b01110, 0b10001, 0b10001, 0b10001, 0b01110 },
            ['p'] = new byte[] { 0b00000, 0b00000, 0b11110, 0b10001, 0b10001, 0b11110, 0b10000 },
            ['q'] = new byte[] { 0b00000, 0b00000, 0b01111, 0b10001, 0b10001, 0b01111, 0b00001 },
            ['r'] = new byte[] { 0b00000, 0b00000, 0b10110, 0b11001, 0b10000, 0b10000, 0b10000 },
            ['s'] = new byte[] { 0b00000, 0b00000, 0b01111, 0b10000, 0b01110, 0b00001, 0b11110 },
            ['t'] = new byte[] { 0b01000, 0b01000, 0b11100, 0b01000, 0b01000, 0b01001, 0b00110 },
            ['u'] = new byte[] { 0b00000, 0b00000, 0b10001, 0b10001, 0b10001, 0b10011, 0b01101 },
            ['v'] = new byte[] { 0b00000, 0b00000, 0b10001, 0b10001, 0b10001, 0b01010, 0b00100 },
            ['w'] = new byte[] { 0b00000, 0b00000, 0b10001, 0b10101, 0b10101, 0b10101, 0b01010 },
            ['x'] = new byte[] { 0b00000, 0b00000, 0b10001, 0b01010, 0b00100, 0b01010, 0b10001 },
            ['y'] = new byte[] { 0b00000, 0b00000, 0b10001, 0b10001, 0b01111, 0b00001, 0b01110 },
            ['z'] = new byte[] { 0b00000, 0b00000, 0b11111, 0b00010, 0b00100, 0b01000, 0b11111 },

            // ---- punctuation (0x7B-0x7E) ----
            ['{'] = new byte[] { 0b00110, 0b00100, 0b00100, 0b01000, 0b00100, 0b00100, 0b00110 },
            ['|'] = new byte[] { 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100 },
            ['}'] = new byte[] { 0b01100, 0b00100, 0b00100, 0b00010, 0b00100, 0b00100, 0b01100 },
            ['~'] = new byte[] { 0b00000, 0b00000, 0b01001, 0b10101, 0b10010, 0b00000, 0b00000 },

            // ---- Spanish ----
            ['ñ'] = new byte[] { 0b01010, 0b10100, 0b11110, 0b10001, 0b10001, 0b10001, 0b10001 },
            ['Ñ'] = new byte[] { 0b01010, 0b10001, 0b11001, 0b10101, 0b10011, 0b10001, 0b10001 },
            ['á'] = new byte[] { 0b00010, 0b00000, 0b01110, 0b00001, 0b01111, 0b10001, 0b01111 },
            ['é'] = new byte[] { 0b00010, 0b00000, 0b01110, 0b10001, 0b11111, 0b10000, 0b01111 },
            ['í'] = new byte[] { 0b00010, 0b00000, 0b01100, 0b00100, 0b00100, 0b00100, 0b01110 },
            ['ó'] = new byte[] { 0b00010, 0b00000, 0b01110, 0b10001, 0b10001, 0b10001, 0b01110 },
            ['ú'] = new byte[] { 0b00010, 0b00000, 0b10001, 0b10001, 0b10001, 0b10011, 0b01101 },
            ['ü'] = new byte[] { 0b01010, 0b00000, 0b10001, 0b10001, 0b10001, 0b10011, 0b01101 },
            ['Á'] = new byte[] { 0b00010, 0b01110, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001 },
            ['É'] = new byte[] { 0b00010, 0b11111, 0b10000, 0b11110, 0b10000, 0b10000, 0b11111 },
            ['Í'] = new byte[] { 0b00010, 0b01110, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110 },
            ['Ó'] = new byte[] { 0b00010, 0b01110, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110 },
            ['Ú'] = new byte[] { 0b00010, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110 },
            ['Ü'] = new byte[] { 0b01010, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110 },
            ['¿'] = new byte[] { 0b00100, 0b00000, 0b00100, 0b00010, 0b00001, 0b10001, 0b01110 },
            ['¡'] = new byte[] { 0b00100, 0b00000, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100 },
        };

        private static readonly byte[] Fallback = { 0b11111, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b11111 };

        /// <summary>Width of <c>' '</c>, as a fraction of a normal glyph's width. Set once to change it globally (shared by both libraries' renderers).</summary>
        public static float SpaceWidthScale { get; set; } = 0.3f;

        /// <summary>The 7-row bitmap for <paramref name="c"/>, or a hollow-box fallback if it has no glyph.</summary>
        public static byte[] GetGlyph(char c) => Glyphs.TryGetValue(c, out byte[]? found) ? found : Fallback;

        /// <summary>Advance width (already scaled by <paramref name="pixelSize"/>) for one character — <see cref="SpaceWidthScale"/> times a normal glyph's advance for <c>' '</c>, the full <see cref="GlyphWidth"/>-based advance for everything else.</summary>
        public static float AdvanceFor(char c, float pixelSize, float glyphSpacing)
            => c == ' ' ? (GlyphWidth * SpaceWidthScale + glyphSpacing) * pixelSize : (GlyphWidth + glyphSpacing) * pixelSize;

        /// <summary>
        /// Total (width, height) a renderer using this font would occupy for <paramref name="text"/>,
        /// already scaled by <paramref name="pixelSize"/> — pure layout math shared by both
        /// libraries' <c>MeasureText</c>. Width is in the same unit as height (screen pixels for
        /// the 2D renderer, world units along a billboard's own axes for the 3D one).
        /// </summary>
        public static (float Width, float Height) MeasureText(string text, float pixelSize, float glyphSpacing, float lineSpacing)
        {
            if (string.IsNullOrEmpty(text) || pixelSize <= 0f) return (0f, 0f);

            float advanceY = (GlyphHeight + lineSpacing) * pixelSize;
            float maxWidth = 0f, lineWidth = 0f;
            float height = GlyphHeight * pixelSize;
            char lastChar = '\0';

            foreach (char c in text)
            {
                if (c == '\n')
                {
                    if (lineWidth > 0f && lastChar != '\0') lineWidth -= glyphSpacing * pixelSize;
                    if (lineWidth > maxWidth) maxWidth = lineWidth;
                    lineWidth = 0f;
                    height += advanceY;
                    lastChar = '\0';
                    continue;
                }
                lineWidth += AdvanceFor(c, pixelSize, glyphSpacing);
                lastChar = c;
            }
            if (lineWidth > 0f && lastChar != '\0') lineWidth -= glyphSpacing * pixelSize;
            if (lineWidth > maxWidth) maxWidth = lineWidth;

            return (maxWidth, height);
        }
    }
}
