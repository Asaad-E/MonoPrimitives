using System;
using System.Runtime.CompilerServices;

namespace MonoPrimitives.Primitives3D
{
    /// <summary>Precomputed unit-circle lookup tables to avoid per-frame trigonometry.</summary>
    /// <remarks>The table stores <see cref="Resolution"/> samples over a full turn plus one wrap-around entry so that consumers can index <c>[i]</c> and <c>[i + 1]</c> without a modulo operation.</remarks>
    public static class TrigLut
    {
        /// <summary>Number of samples covering a full revolution.</summary>
        public const int Resolution = 1024;

        /// <summary>Bit mask used for fast wrap-around indexing.</summary>
        public const int Mask = Resolution - 1;

        /// <summary>1 / (2*PI) — multiply a radian angle by this to get turns, instead of dividing by 2*PI on every call.</summary>
        public const float TurnsPerRadian = 1f / (MathF.PI * 2f);

        /// <summary>1 / 360 — multiply a degree angle by this to get turns, instead of dividing by 360 on every call.</summary>
        public const float TurnsPerDegree = 1f / 360f;

        private static readonly float[] SinTable = new float[Resolution + 1];
        private static readonly float[] CosTable = new float[Resolution + 1];

        static TrigLut()
        {
            for (int i = 0; i <= Resolution; i++)
            {
                double a = i * (Math.PI * 2.0) / Resolution;
                SinTable[i] = (float)Math.Sin(a);
                CosTable[i] = (float)Math.Cos(a);
            }
        }

        /// <summary>Sine of <c>index * 2*PI / Resolution</c>. Index is wrapped.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SinIndex(int index) => SinTable[index & Mask];

        /// <summary>Cosine of <c>index * 2*PI / Resolution</c>. Index is wrapped.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CosIndex(int index) => CosTable[index & Mask];

        /// <summary>Returns sine/cosine for step <paramref name="step"/> out of <paramref name="steps"/> equal divisions of a full circle.</summary>
        /// <remarks>Uses the LUT when the division maps exactly onto the table, otherwise falls back to real trigonometry (still allocation free).</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SinCosStep(int step, int steps, out float sin, out float cos)
        {
            // Fast path: steps divides Resolution evenly -> exact LUT hit.
            if (steps > 0 && (Resolution % steps) == 0)
            {
                int idx = step * (Resolution / steps);
                sin = SinTable[idx & Mask];
                cos = CosTable[idx & Mask];
                return;
            }

            SampleInterpolated(step * (float)Resolution / steps, out sin, out cos);
        }

        /// <summary>Sine/cosine at a normalized angle <paramref name="t01"/> in [0, 1) (1 = a full turn), via linear interpolation between the two nearest table entries.</summary>
        /// <remarks>The continuous counterpart to <see cref="SinCosStep"/>, for an angle that isn't naturally a division of a circle (e.g. an animated phase held as its own float).</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Sample(float t01, out float sin, out float cos) => SampleInterpolated(t01 * Resolution, out sin, out cos);

        /// <summary>
        /// <see cref="Sample"/>, taking the angle in radians instead of turns — a multiply by
        /// <see cref="TurnsPerRadian"/> (not a divide by <c>2*PI</c>) before the lookup.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SampleRadians(float radians, out float sin, out float cos) => Sample(radians * TurnsPerRadian, out sin, out cos);

        /// <summary>
        /// <see cref="Sample"/>, taking the angle in degrees instead of turns — a multiply by
        /// <see cref="TurnsPerDegree"/> (not a divide by 360) before the lookup.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SampleDegrees(float degrees, out float sin, out float cos) => Sample(degrees * TurnsPerDegree, out sin, out cos);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SampleInterpolated(float t, out float sin, out float cos)
        {
            // Floor (not a raw truncating (int) cast) before splitting into index+fraction --
            // truncation rounds toward zero, so for negative t it picks the wrong pair of
            // adjacent table entries to interpolate between (same class of bug UnitCircleLut.Sample
            // had for negative input; confirmed numerically this is a real, if small, curvature
            // error at the wrap boundary, not just a theoretical one).
            int i0 = (int)MathF.Floor(t);
            float frac = t - i0;
            int a = i0 & Mask;
            int b = (i0 + 1) & Mask;
            sin = SinTable[a] + (SinTable[b] - SinTable[a]) * frac;
            cos = CosTable[a] + (CosTable[b] - CosTable[a]) * frac;
        }
    }
}