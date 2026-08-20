using System;
using System.Runtime.CompilerServices;

namespace MonoPrimitives3D
{
    /// <summary>
    /// Precomputed unit-circle lookup tables to avoid per-frame trigonometry.
    /// The table stores <see cref="Resolution"/> samples over a full turn plus one
    /// wrap-around entry so that consumers can index <c>[i]</c> and <c>[i + 1]</c>
    /// without a modulo operation.
    /// </summary>
    public static class TrigLut
    {
        /// <summary>Number of samples covering a full revolution.</summary>
        public const int Resolution = 1024;

        /// <summary>Bit mask used for fast wrap-around indexing.</summary>
        public const int Mask = Resolution - 1;

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

        /// <summary>
        /// Returns sine/cosine for step <paramref name="step"/> out of
        /// <paramref name="steps"/> equal divisions of a full circle.
        /// Uses the LUT when the division maps exactly onto the table,
        /// otherwise falls back to real trigonometry (still allocation free).
        /// </summary>
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

            // Generic path: linear interpolation on the LUT keeps this cheap and
            // avoids Math.Sin/Cos calls while staying visually indistinguishable.
            float t = step * (float)Resolution / steps;
            int i0 = (int)t;
            float frac = t - i0;
            int a = i0 & Mask;
            int b = (i0 + 1) & Mask;
            sin = SinTable[a] + (SinTable[b] - SinTable[a]) * frac;
            cos = CosTable[a] + (CosTable[b] - CosTable[a]) * frac;
        }
    }
}