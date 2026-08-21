using System;
using MonoPrimitives.Primitives3D;

namespace MonoPrimitives.Tests
{
    /// <summary>Pure-math checks for <see cref="TrigLut"/> — no GraphicsDevice needed.</summary>
    internal static class TrigLutTests
    {
        public static void Run(TestResults results)
        {
            results.Check("SinIndex/CosIndex hit the four cardinal points, and wrap correctly for negative/out-of-range indices", () =>
            {
                bool Close(float a, float b) => MathF.Abs(a - b) < 1e-5f;
                if (!Close(TrigLut.CosIndex(0), 1f) || !Close(TrigLut.SinIndex(0), 0f))
                    return $"index 0: ({TrigLut.CosIndex(0)},{TrigLut.SinIndex(0)}), expected (1,0)";
                int quarter = TrigLut.Resolution / 4;
                if (!Close(TrigLut.CosIndex(quarter), 0f) || !Close(TrigLut.SinIndex(quarter), 1f))
                    return $"index Resolution/4: ({TrigLut.CosIndex(quarter)},{TrigLut.SinIndex(quarter)}), expected (0,1)";

                // Negative/large indices must wrap to the same table entry as their modulo.
                if (!Close(TrigLut.SinIndex(-1), TrigLut.SinIndex(TrigLut.Resolution - 1)))
                    return $"SinIndex(-1)={TrigLut.SinIndex(-1)} != SinIndex(Resolution-1)={TrigLut.SinIndex(TrigLut.Resolution - 1)}";
                if (!Close(TrigLut.CosIndex(-TrigLut.Resolution - 5), TrigLut.CosIndex(TrigLut.Resolution - 5)))
                    return "CosIndex doesn't wrap consistently for indices below -Resolution";
                return null;
            });

            results.Check("SinCosStep's exact-division fast path matches real trig for several divisor counts", () =>
            {
                float maxErr = 0f;
                foreach (int steps in new[] { 1, 2, 4, 8, 16, 32, 64 }) // all divide Resolution=1024 evenly
                {
                    for (int step = -steps * 2; step <= steps * 2; step++)
                    {
                        TrigLut.SinCosStep(step, steps, out float sin, out float cos);
                        double angle = step * 2.0 * Math.PI / steps;
                        maxErr = MathF.Max(maxErr, (float)Math.Max(Math.Abs(sin - Math.Sin(angle)), Math.Abs(cos - Math.Cos(angle))));
                    }
                }
                return maxErr < 1e-4f ? null : $"fast-path max abs error = {maxErr:F6}, expected ~0 (exact LUT hits)";
            });

            results.Check("SinCosStep's generic (non-exact-division) path matches real trig, including negative step", () =>
            {
                float maxErr = 0f;
                const int steps = 7; // doesn't divide 1024 evenly -> forces the interpolated path
                for (int step = -30; step <= 30; step++)
                {
                    TrigLut.SinCosStep(step, steps, out float sin, out float cos);
                    double angle = step * 2.0 * Math.PI / steps;
                    maxErr = MathF.Max(maxErr, (float)Math.Max(Math.Abs(sin - Math.Sin(angle)), Math.Abs(cos - Math.Cos(angle))));
                }
                // Regression: a raw truncating (int) cast (instead of floor) on the interpolation
                // fraction used to leave ~3-4x this error for negative step -- see Design/DECISIONS.md.
                return maxErr < 5e-5f ? null : $"generic-path max abs error = {maxErr:F6}, expected < 5e-5 (floored, not truncated)";
            });

            results.Check("Sample(t01)/SampleRadians/SampleDegrees agree with each other and with real trig, including negative angles", () =>
            {
                var rand = new Random(3);
                float maxErrCross = 0f, maxErrMath = 0f;
                for (int i = 0; i < 5000; i++)
                {
                    float radians = (float)(rand.NextDouble() * 40.0 - 20.0);
                    float degrees = radians * (180f / MathF.PI);
                    float turns = radians / (MathF.PI * 2f);

                    TrigLut.Sample(turns, out float sinT, out float cosT);
                    TrigLut.SampleRadians(radians, out float sinR, out float cosR);
                    TrigLut.SampleDegrees(degrees, out float sinD, out float cosD);

                    maxErrCross = MathF.Max(maxErrCross, MathF.Max(MathF.Abs(sinT - sinR), MathF.Abs(cosT - cosR)));
                    maxErrCross = MathF.Max(maxErrCross, MathF.Max(MathF.Abs(sinT - sinD), MathF.Abs(cosT - cosD)));

                    double trueSin = Math.Sin(radians), trueCos = Math.Cos(radians);
                    maxErrMath = MathF.Max(maxErrMath, (float)Math.Max(Math.Abs(sinR - trueSin), Math.Abs(cosR - trueCos)));
                }
                if (maxErrCross > 1e-5f) return $"Sample/SampleRadians/SampleDegrees disagree by {maxErrCross:F7}";
                return maxErrMath < 1e-4f ? null : $"SampleRadians max abs error vs Math.Sin/Cos = {maxErrMath:F6}, expected < 1e-4";
            });

            results.Check("Sample is continuous across the t01=0/1 wrap boundary", () =>
            {
                const float step = 0.0001f;
                TrigLut.Sample(1f - step, out float sinA, out float cosA);
                TrigLut.Sample(0f + step, out float sinB, out float cosB);
                float diff = MathF.Max(MathF.Abs(sinA - sinB), MathF.Abs(cosA - cosB));
                return diff < step * 20f ? null : $"seam at the wrap boundary: diff={diff:F5} for a step of {step}";
            });
        }
    }
}
