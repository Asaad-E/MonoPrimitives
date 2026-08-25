#nullable enable

using System;
using Microsoft.Xna.Framework;
using MonoPrimitives;

namespace MonoPrimitives.Primitives2D
{
    /// <summary>Standalone 5x7 dot-matrix "pixel art" debug font, drawn entirely with <see cref="Primitive2DBatch.FillRectangle(float,float,float,float,Color,float,Vector2?)"/> — no textures, no SpriteFont.</summary>
    /// <remarks>
    /// Glyph data (<see cref="FontGlyphs5x7"/>) is shared with the 3D library's own billboard
    /// text renderer; only the actual drawing differs. Intended for test/debug text (HUD
    /// counters, labels), not production typography. External to Primitives2D.cs by design — a
    /// separate file, doesn't modify it.
    /// </remarks>
    public static class DebugFont5x7
    {
        /// <summary>Glyph width in pixels before <c>pixelSize</c> scaling — see <see cref="FontGlyphs5x7.GlyphWidth"/>.</summary>
        public const int GlyphWidth = FontGlyphs5x7.GlyphWidth;

        /// <summary>Glyph height in pixels before <c>pixelSize</c> scaling — see <see cref="FontGlyphs5x7.GlyphHeight"/>.</summary>
        public const int GlyphHeight = FontGlyphs5x7.GlyphHeight;

        /// <summary>Width of <c>' '</c>, as a fraction of a normal glyph's width. Set once to change it globally (shared with the 3D library's text renderer).</summary>
        public static float SpaceWidthScale
        {
            get => FontGlyphs5x7.SpaceWidthScale;
            set => FontGlyphs5x7.SpaceWidthScale = value;
        }

        /// <summary>
        /// Draws <paramref name="text"/> starting at <paramref name="position"/> (top-left of
        /// the first character), one <c>FillRectangle</c> per "on" pixel — named to match
        /// <c>SpriteBatch.DrawString</c>, the API this stands in for.
        /// </summary>
        /// <remarks>
        /// <paramref name="pixelSize"/> is the screen size of one font pixel (a glyph is
        /// therefore <c>5*pixelSize</c> wide, <c>7*pixelSize</c> tall). <c>'\n'</c> starts a new
        /// line. Characters with no glyph draw as a hollow box instead of silently vanishing.
        /// <paramref name="maxWidth"/> greater than 0 word-wraps <paramref name="text"/> first
        /// (see <see cref="FontGlyphs5x7.WrapText"/>) — 0 (the default) draws exactly as given,
        /// no wrapping.
        /// </remarks>
        public static void DrawString(this Primitive2DBatch batch, string text, Vector2 position, float pixelSize, Color color, float glyphSpacing = 1f, float lineSpacing = 2f, float maxWidth = 0f)
        {
            if (string.IsNullOrEmpty(text) || pixelSize <= 0f) return;
            if (maxWidth > 0f) text = FontGlyphs5x7.WrapText(text, maxWidth, pixelSize, glyphSpacing);

            float x = position.X;
            float y = position.Y;
            float advanceY = (GlyphHeight + lineSpacing) * pixelSize;

            foreach (char c in text)
            {
                if (c == '\n')
                {
                    x = position.X;
                    y += advanceY;
                    continue;
                }

                if (c != ' ')
                {
                    byte[] glyph = FontGlyphs5x7.GetGlyph(c);
                    for (int row = 0; row < GlyphHeight; row++)
                    {
                        byte bits = glyph[row];
                        for (int col = 0; col < GlyphWidth; col++)
                        {
                            if ((bits & (1 << (GlyphWidth - 1 - col))) == 0) continue;
                            batch.FillRectangle(x + col * pixelSize, y + row * pixelSize, pixelSize, pixelSize, color);
                        }
                    }
                }

                x += FontGlyphs5x7.AdvanceFor(c, pixelSize, glyphSpacing);
            }
        }

        /// <summary>
        /// Total size in pixels (already scaled by <paramref name="pixelSize"/>) that
        /// <see cref="DrawString"/> would occupy — for centering/layout before drawing.
        /// </summary>
        public static Vector2 MeasureText(string text, float pixelSize, float glyphSpacing = 1f, float lineSpacing = 2f)
        {
            (float width, float height) = FontGlyphs5x7.MeasureText(text, pixelSize, glyphSpacing, lineSpacing);
            return new Vector2(width, height);
        }
    }
}
