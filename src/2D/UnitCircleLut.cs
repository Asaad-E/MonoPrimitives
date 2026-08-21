#nullable enable

using System;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;

namespace MonoPrimitives.Primitives2D
{
    /// <summary>
    /// Precomputed unit-circle lookup table — the 2D counterpart to the 3D library's own
    /// <c>TrigLut</c>, exposed publicly for the same reason: fast, allocation-free sin/cos for
    /// your own curved geometry (a custom particle ring, a procedural shape) without redoing
    /// this table yourself. <see cref="PrimitiveBatch"/> uses <see cref="Sample"/> internally
    /// for every curved shape it draws.
    /// </summary>
    public static class UnitCircleLut
    {
        /// <summary>Number of samples covering a full turn.</summary>
        public const int Resolution = 512;

        /// <summary>1 / (2*PI) — multiply a radian angle by this to get turns, instead of dividing by 2*PI on every call.</summary>
        public const float TurnsPerRadian = 1f / (MathF.PI * 2f);

        /// <summary>1 / 360 — multiply a degree angle by this to get turns, instead of dividing by 360 on every call.</summary>
        public const float TurnsPerDegree = 1f / 360f;

        private static readonly Vector2[] Table = Build();

        private static Vector2[] Build()
        {
            var table = new Vector2[Resolution];
            const double step = System.Math.PI * 2.0 / Resolution;
            for (int i = 0; i < Resolution; i++)
                table[i] = new Vector2((float)System.Math.Cos(i * step), (float)System.Math.Sin(i * step));
            return table;
        }

        /// <summary>
        /// Samples the unit circle at a normalized angle <paramref name="t01"/> in [0, 1)
        /// (1 = a full turn) with linear interpolation between the two nearest table entries —
        /// trig-free, accurate to well under a pixel at any sane radius.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 Sample(float t01)
        {
            float f = t01 * Resolution;
            // Floor, not a raw (int) cast -- (int)f truncates toward zero, which is wrong for
            // negative f (e.g. (int)(-51.2f) is -51, not -52) and left frac negative, extrapolating
            // from the wrong side of the wrapped index instead of interpolating within it. Harmless
            // in practice (the lerp is linear, so it still lands within a curvature-sized error of
            // the correct point), but exact is free here -- same fix as Noise.cs's lattice wrap.
            int i0 = (int)MathF.Floor(f);
            float frac = f - i0;

            i0 &= Resolution - 1; // Resolution is a power of two
            int i1 = (i0 + 1) & (Resolution - 1);

            Vector2 a = Table[i0];
            Vector2 b = Table[i1];
            return new Vector2(a.X + (b.X - a.X) * frac,
                               a.Y + (b.Y - a.Y) * frac);
        }

        /// <summary>
        /// <see cref="Sample"/>, taking the angle in radians instead of turns — a multiply by
        /// <see cref="TurnsPerRadian"/> (not a divide by <c>2*PI</c>) before the lookup.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 SampleRadians(float radians) => Sample(radians * TurnsPerRadian);

        /// <summary>
        /// <see cref="Sample"/>, taking the angle in degrees instead of turns — a multiply by
        /// <see cref="TurnsPerDegree"/> (not a divide by 360) before the lookup.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 SampleDegrees(float degrees) => Sample(degrees * TurnsPerDegree);
    }
}
