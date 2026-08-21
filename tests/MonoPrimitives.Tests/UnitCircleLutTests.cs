using System;
using Microsoft.Xna.Framework;
using MonoPrimitives.Primitives2D;

namespace MonoPrimitives.Tests
{
    /// <summary>Pure-math checks for <see cref="UnitCircleLut"/> — no GraphicsDevice needed.</summary>
    internal static class UnitCircleLutTests
    {
        public static void Run(TestResults results)
        {
            results.Check("Sample hits the four cardinal points at 0/0.25/0.5/0.75 turns", () =>
            {
                bool Close(Vector2 v, float x, float y) => MathF.Abs(v.X - x) < 0.01f && MathF.Abs(v.Y - y) < 0.01f;
                Vector2 at0 = UnitCircleLut.Sample(0f), at25 = UnitCircleLut.Sample(0.25f);
                Vector2 at50 = UnitCircleLut.Sample(0.5f), at75 = UnitCircleLut.Sample(0.75f);
                if (!Close(at0, 1f, 0f)) return $"Sample(0)={at0}, expected ~(1,0)";
                if (!Close(at25, 0f, 1f)) return $"Sample(0.25)={at25}, expected ~(0,1)";
                if (!Close(at50, -1f, 0f)) return $"Sample(0.5)={at50}, expected ~(-1,0)";
                if (!Close(at75, 0f, -1f)) return $"Sample(0.75)={at75}, expected ~(0,-1)";
                return null;
            });

            results.Check("Sample stays on the unit circle (length ~1) across many angles", () =>
            {
                float maxDeviation = 0f;
                for (int i = 0; i < 1000; i++)
                {
                    float t = i / 1000f;
                    float len = UnitCircleLut.Sample(t).Length();
                    maxDeviation = MathF.Max(maxDeviation, MathF.Abs(len - 1f));
                }
                // Linear interpolation between two points on a circle is always slightly inside
                // it (a chord is shorter than the arc) -- bounded by the lattice spacing, not zero.
                return maxDeviation < 0.001f ? null : $"max |length-1| = {maxDeviation:F5}, expected < 0.001";
            });

            results.Check("Sample is continuous across the t01=0/1 wrap boundary", () =>
            {
                const float step = 0.0001f;
                float diff = Vector2.Distance(UnitCircleLut.Sample(1f - step), UnitCircleLut.Sample(0f + step));
                return diff < step * 20f ? null : $"seam at the wrap boundary: diff={diff:F5} for a step of {step}";
            });

            // Regression test: (int)f truncates toward zero for negative f, which used to leave a
            // negative interpolation fraction after the index was wrapped -- harmless to within
            // curvature-sized error (the lerp is linear, so it coincidentally lands close to right),
            // but not exact. Fixed to floor before casting, same pattern as Noise's lattice wrap.
            results.Check("Sample(t) matches Sample(t mod 1) exactly for negative and >1 inputs", () =>
            {
                var rand = new Random(1);
                float maxDiff = 0f;
                for (int i = 0; i < 2000; i++)
                {
                    float t = (float)(rand.NextDouble() * 20.0 - 10.0); // [-10, 10)
                    float wrapped = t - MathF.Floor(t);
                    maxDiff = MathF.Max(maxDiff, Vector2.Distance(UnitCircleLut.Sample(t), UnitCircleLut.Sample(wrapped)));
                }
                return maxDiff < 1e-4f ? null : $"max diff between Sample(t) and Sample(wrap(t)) = {maxDiff:F6}, expected ~0";
            });

            results.Check("SampleRadians/SampleDegrees agree with Sample(t01) and with real trig, including negative angles", () =>
            {
                var rand = new Random(2);
                float maxErrVsTurns = 0f, maxErrVsMath = 0f;
                for (int i = 0; i < 2000; i++)
                {
                    float radians = (float)(rand.NextDouble() * 40.0 - 20.0); // includes negative and >2*PI
                    float degrees = radians * (180f / MathF.PI);

                    Vector2 viaTurns = UnitCircleLut.Sample(radians / (MathF.PI * 2f));
                    Vector2 viaRadians = UnitCircleLut.SampleRadians(radians);
                    Vector2 viaDegrees = UnitCircleLut.SampleDegrees(degrees);
                    maxErrVsTurns = MathF.Max(maxErrVsTurns, Vector2.Distance(viaTurns, viaRadians));
                    maxErrVsTurns = MathF.Max(maxErrVsTurns, Vector2.Distance(viaTurns, viaDegrees));

                    double trueCos = Math.Cos(radians), trueSin = Math.Sin(radians);
                    maxErrVsMath = MathF.Max(maxErrVsMath, (float)Math.Max(Math.Abs(viaRadians.X - trueCos), Math.Abs(viaRadians.Y - trueSin)));
                }
                if (maxErrVsTurns > 1e-5f) return $"SampleRadians/SampleDegrees disagree with equivalent Sample(t01) by {maxErrVsTurns:F7}";
                // Same lattice-spacing error bound as the plain Sample table check above (~1/Resolution^2 scale).
                return maxErrVsMath < 1e-4f ? null : $"SampleRadians max abs error vs Math.Sin/Cos = {maxErrVsMath:F6}, expected < 1e-4";
            });
        }
    }
}
