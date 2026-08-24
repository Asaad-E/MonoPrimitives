#nullable enable

using Microsoft.Xna.Framework;
using MonoPrimitives;

namespace MonoPrimitives.Primitives3D
{
    public sealed partial class Primitive3DBatch
    {
        /// <summary>
        /// Draws billboarded (always facing the camera) 3D debug text at <paramref name="position"/> —
        /// labeling an agent, a debug readout floating over a point in the scene. Same 5x7 bitmap
        /// glyph data as the 2D library's own <c>DebugFont5x7</c> (<see cref="FontGlyphs5x7"/>),
        /// rendered as small camera-facing quads instead of flat screen-space rectangles.
        /// <paramref name="pixelSize"/> is a WORLD-space size (a glyph is <c>5*pixelSize</c> wide,
        /// <c>7*pixelSize</c> tall in world units), not screen pixels — scale it to the scene, or
        /// re-scale per frame based on camera distance if you want constant screen size. Text is
        /// never affected by <see cref="LightingEnabled"/> — it should read the same regardless of
        /// scene lighting. Billboarding is "cylindrical" (stays upright relative to world +Y,
        /// rotating only to face the camera around that axis), the usual choice for labels — falls
        /// back to a full camera-facing basis only when looking almost straight up or down, where
        /// that axis is undefined. <paramref name="maxWidth"/> greater than 0 (in the same
        /// world-space unit as <paramref name="pixelSize"/>) word-wraps <paramref name="text"/>
        /// first (see <see cref="FontGlyphs5x7.WrapText"/>) — 0 (the default) draws exactly as
        /// given, no wrapping.
        /// </summary>
        public void DrawString3D(string text, Vector3 position, float pixelSize, Color color, float glyphSpacing = 1f, float lineSpacing = 2f, float maxWidth = 0f)
        {
            GetBillboardAxes(position, out Vector3 right, out Vector3 up);
            DrawString3D(text, position, right, up, pixelSize, color, glyphSpacing, lineSpacing, maxWidth);
        }

        /// <summary>
        /// Draws 3D debug text with a caller-supplied, fixed <paramref name="right"/>/
        /// <paramref name="up"/> basis instead of always facing the camera — the opt-out for
        /// when billboarding isn't wanted (a label painted onto a specific surface, text that
        /// should visibly rotate away as the camera orbits past it, matching a fixed HUD-in-world
        /// panel). Same glyph data and world-space <paramref name="pixelSize"/> as the billboarded
        /// overload; <paramref name="right"/>/<paramref name="up"/> need not be unit length or
        /// exactly orthogonal — each glyph pixel is simply offset by <c>col * pixelSize</c> along
        /// <paramref name="right"/> and <c>row * pixelSize</c> along <paramref name="up"/>, so a
        /// scaled or sheared basis skews the text the same way it would skew any other quad.
        /// <paramref name="maxWidth"/> greater than 0 word-wraps <paramref name="text"/> first, same
        /// as the billboarded overload.
        /// </summary>
        public void DrawString3D(string text, Vector3 position, Vector3 right, Vector3 up, float pixelSize, Color color, float glyphSpacing = 1f, float lineSpacing = 2f, float maxWidth = 0f)
        {
            ThrowIfNotBegun();
            if (string.IsNullOrEmpty(text) || pixelSize <= 0f) return;
            if (maxWidth > 0f) text = FontGlyphs5x7.WrapText(text, maxWidth, pixelSize, glyphSpacing);

            Vector3 lineStart = position;
            Vector3 cursor = position;
            float advanceY = (FontGlyphs5x7.GlyphHeight + lineSpacing) * pixelSize;

            foreach (char c in text)
            {
                if (c == '\n')
                {
                    lineStart -= up * advanceY;
                    cursor = lineStart;
                    continue;
                }

                if (c != ' ')
                {
                    byte[] glyph = FontGlyphs5x7.GetGlyph(c);
                    for (int row = 0; row < FontGlyphs5x7.GlyphHeight; row++)
                    {
                        byte bits = glyph[row];
                        for (int col = 0; col < FontGlyphs5x7.GlyphWidth; col++)
                        {
                            if ((bits & (1 << (FontGlyphs5x7.GlyphWidth - 1 - col))) == 0) continue;

                            Vector3 pixelTopLeft = cursor + right * (col * pixelSize) - up * (row * pixelSize);
                            Vector3 a = pixelTopLeft;
                            Vector3 b = pixelTopLeft + right * pixelSize;
                            Vector3 c2 = pixelTopLeft + right * pixelSize - up * pixelSize;
                            Vector3 d = pixelTopLeft - up * pixelSize;
                            PushQuad(a, b, c2, d, color);
                        }
                    }
                }

                cursor += right * FontGlyphs5x7.AdvanceFor(c, pixelSize, glyphSpacing);
            }
        }

        /// <summary>
        /// Total (width, height) in world units — same unit as <paramref name="pixelSize"/> — that
        /// <see cref="DrawString3D"/> would occupy along its own billboard axes, for centering
        /// before drawing (e.g. offset by <c>-size.X/2</c> along <c>right</c> to center a label).
        /// </summary>
        public static Vector2 MeasureText3D(string text, float pixelSize, float glyphSpacing = 1f, float lineSpacing = 2f)
        {
            (float width, float height) = FontGlyphs5x7.MeasureText(text, pixelSize, glyphSpacing, lineSpacing);
            return new Vector2(width, height);
        }

        /// <summary>
        /// Right/up axes for a cylindrical billboard at <paramref name="position"/> facing the
        /// active camera — shared by <see cref="DrawString3D"/> and available for your own
        /// billboarded quads (particles, sprites) that want the same "always upright, rotates to
        /// face camera" behavior instead of full spherical billboarding.
        /// </summary>
        public void GetBillboardAxes(Vector3 position, out Vector3 right, out Vector3 up)
        {
            Vector3 toCamera = _cameraPosition - position;
            if (toCamera.LengthSquared() < 1e-10f)
            {
                right = Vector3.UnitX;
                up = Vector3.UnitY;
                return;
            }

            Vector3 forward = Vector3.Normalize(toCamera);
            Vector3 crossUp = Vector3.Cross(Vector3.Up, forward);
            if (crossUp.LengthSquared() < 1e-6f)
            {
                // Looking almost straight up or down world +Y: cylindrical billboarding has no
                // well-defined "upright" axis here, fall back to a full camera-facing basis.
                BuildBasis(forward, out right, out up);
                return;
            }

            right = Vector3.Normalize(crossUp);
            up = Vector3.Cross(forward, right); // unit length already: forward and right are orthonormal unit vectors
        }
    }
}
