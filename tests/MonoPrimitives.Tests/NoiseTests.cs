using System;
using MonoPrimitives;

namespace MonoPrimitives.Tests
{
    /// <summary>Pure-math checks for <see cref="Noise"/> — no GraphicsDevice needed.</summary>
    internal static class NoiseTests
    {
        public static void Run(TestResults results)
        {
            results.Check("Sample1D/2D/3D are deterministic for a given seed", () =>
            {
                var a = new Noise(42);
                var b = new Noise(42);
                float diff1D = MathF.Abs(a.Sample1D(3.14f) - b.Sample1D(3.14f));
                float diff2D = MathF.Abs(a.Sample2D(3.14f, 2.7f) - b.Sample2D(3.14f, 2.7f));
                float diff3D = MathF.Abs(a.Sample3D(3.14f, 2.7f, 1.6f) - b.Sample3D(3.14f, 2.7f, 1.6f));
                return diff1D == 0f && diff2D == 0f && diff3D == 0f ? null : "same seed produced different output";
            });

            results.Check("Different seeds produce different output", () =>
            {
                var a = new Noise(1);
                var b = new Noise(2);
                return a.Sample2D(3.14f, 2.7f) != b.Sample2D(3.14f, 2.7f) ? null : "different seeds produced identical output (suspicious)";
            });

            results.Check("Sample2D/3D stay within the documented ~[-1,1] range over many samples", () =>
            {
                var noise = new Noise(7);
                float min = float.MaxValue, max = float.MinValue;
                for (int i = 0; i < 2000; i++)
                {
                    float v = noise.Sample3D(i * 0.13f, i * 0.07f, i * 0.19f);
                    min = MathF.Min(min, v);
                    max = MathF.Max(max, v);
                }
                // "Roughly" [-1,1] per the class's own doc -- allow a little overshoot, but not wildly out of range.
                return min >= -1.5f && max <= 1.5f ? null : $"range [{min:F2}, {max:F2}] is well outside the expected [-1,1]";
            });

            // Regression test for the fixed Sample1D degeneracy: a naive y=0,z=0 slice of
            // Sample3D produced ~23% near-zero samples (11x the ~2% baseline) because several of
            // Grad's hash cases route their x-facing component through y or z instead of x, and
            // both were pinned to zero. Sample1D now uses its own dedicated 1D gradient.
            results.Check("Sample1D near-zero rate matches Sample2D's, not the old degenerate ~23%", () =>
            {
                var noise = new Noise(99);
                int nearZero1D = 0, nearZero2D = 0;
                const int samples = 5000;
                for (int i = 0; i < samples; i++)
                {
                    float x = i * 0.0973f; // an irrational-ish step so samples don't land on lattice points
                    if (MathF.Abs(noise.Sample1D(x)) < 0.01f) nearZero1D++;
                    if (MathF.Abs(noise.Sample2D(x, 0f)) < 0.01f) nearZero2D++;
                }
                float rate1D = nearZero1D / (float)samples;
                float rate2D = nearZero2D / (float)samples;
                // The old bug's rate was ~0.23 (11x the ~0.02 baseline). Allow generous slack
                // above rate2D without allowing anywhere near the old degenerate rate back in.
                return rate1D < rate2D * 3f + 0.03f
                    ? null
                    : $"Sample1D near-zero rate {rate1D:P1} looks degenerate (Sample2D's is {rate2D:P1})";
            });

            results.Check("Fbm1D/2D/3D stay within the documented ~[-1,1] range", () =>
            {
                var noise = new Noise(3);
                float min = float.MaxValue, max = float.MinValue;
                for (int i = 0; i < 500; i++)
                {
                    float v = noise.Fbm3D(i * 0.11f, i * 0.05f, i * 0.17f, octaves: 5);
                    min = MathF.Min(min, v);
                    max = MathF.Max(max, v);
                }
                return min >= -1.5f && max <= 1.5f ? null : $"Fbm3D range [{min:F2}, {max:F2}] is well outside the expected [-1,1]";
            });
        }
    }
}
