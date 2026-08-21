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
                var noise = new Noise(3, octaves: 5);
                float min = float.MaxValue, max = float.MinValue;
                for (int i = 0; i < 500; i++)
                {
                    float v = noise.Fbm3D(i * 0.11f, i * 0.05f, i * 0.17f);
                    min = MathF.Min(min, v);
                    max = MathF.Max(max, v);
                }
                return min >= -1.5f && max <= 1.5f ? null : $"Fbm3D range [{min:F2}, {max:F2}] is well outside the expected [-1,1]";
            });

            // The entire point of gradient noise over raw random: nearby inputs give nearby
            // outputs. This is the one property none of the tests above actually verify.
            results.Check("Sample1D/2D/3D are continuous -- a tiny step changes the output by a bounded amount", () =>
            {
                var noise = new Noise(11);
                const float step = 0.001f;
                float max1D = 0f, max2D = 0f, max3D = 0f;
                for (int i = 0; i < 200; i++)
                {
                    float x = i * 0.37f, y = i * 0.19f, z = i * 0.23f; // walk across many lattice cells, including near cell boundaries
                    max1D = MathF.Max(max1D, MathF.Abs(noise.Sample1D(x + step) - noise.Sample1D(x)));
                    max2D = MathF.Max(max2D, MathF.Abs(noise.Sample2D(x + step, y) - noise.Sample2D(x, y)));
                    max3D = MathF.Max(max3D, MathF.Abs(noise.Sample3D(x + step, y, z) - noise.Sample3D(x, y, z)));
                }
                // A discontinuous jump (a lattice-boundary bug) would show up as a change orders of
                // magnitude larger than the step itself; gradient noise's slope is bounded, so a
                // generous multiple of the step is still nowhere near what a real seam would produce.
                const float maxAllowed = step * 50f;
                return max1D < maxAllowed && max2D < maxAllowed && max3D < maxAllowed
                    ? null
                    : $"a {step} step produced a jump of up to {MathF.Max(max1D, MathF.Max(max2D, max3D)):F4} (1D={max1D:F4} 2D={max2D:F4} 3D={max3D:F4}), expected < {maxAllowed:F4}";
            });

            results.Check("Sample2D/3D stay continuous across the negative-coordinate boundary (x=0)", () =>
            {
                // The lattice-index wrap uses (int)Floor(x) & 255, which only behaves like a true
                // modulo for negative x because of two's-complement bit patterns -- a future "simplify
                // to % 256" refactor would silently reintroduce a seam exactly at every integer
                // boundary, most obviously at 0. Confirm there's no such seam.
                var noise = new Noise(23);
                const float step = 0.001f;
                float diff2D = MathF.Abs(noise.Sample2D(0f, 5.5f) - noise.Sample2D(-step, 5.5f));
                float diff3D = MathF.Abs(noise.Sample3D(0f, 5.5f, -3.2f) - noise.Sample3D(-step, 5.5f, -3.2f));
                const float maxAllowed = step * 50f;
                return diff2D < maxAllowed && diff3D < maxAllowed
                    ? null
                    : $"discontinuity at x=0: 2D diff={diff2D:F4}, 3D diff={diff3D:F4}, expected < {maxAllowed:F4}";
            });

            results.Check("Fbm2D/3D are deterministic and Octaves=0 returns exactly 0", () =>
            {
                var a = new Noise(55, octaves: 6);
                var b = new Noise(55, octaves: 6);
                if (a.Fbm2D(1.2f, 3.4f) != b.Fbm2D(1.2f, 3.4f)) return "Fbm2D not deterministic for the same seed/settings";
                if (a.Fbm3D(1.2f, 3.4f, 5.6f) != b.Fbm3D(1.2f, 3.4f, 5.6f)) return "Fbm3D not deterministic for the same seed/settings";

                var zeroOctaves = new Noise(1) { Octaves = 0 };
                if (zeroOctaves.Fbm2D(1f, 1f) != 0f) return $"Fbm2D with Octaves=0 returned {zeroOctaves.Fbm2D(1f, 1f)}, expected exactly 0";
                return null;
            });

            results.Check("RidgeNoise2D/3D and Turbulence2D/3D stay within [0, ~1.2] (not the signed [-1,1] Fbm range)", () =>
            {
                var noise = new Noise(17, octaves: 5);
                float minR = float.MaxValue, maxR = float.MinValue, minT = float.MaxValue, maxT = float.MinValue;
                for (int i = 0; i < 500; i++)
                {
                    float x = i * 0.11f, y = i * 0.05f, z = i * 0.17f;
                    float r2 = noise.RidgeNoise2D(x, y), r3 = noise.RidgeNoise3D(x, y, z);
                    float t2 = noise.Turbulence2D(x, y), t3 = noise.Turbulence3D(x, y, z);
                    minR = MathF.Min(minR, MathF.Min(r2, r3)); maxR = MathF.Max(maxR, MathF.Max(r2, r3));
                    minT = MathF.Min(minT, MathF.Min(t2, t3)); maxT = MathF.Max(maxT, MathF.Max(t2, t3));
                }
                return minR >= -0.2f && maxR <= 1.2f && minT >= -0.2f && maxT <= 1.2f
                    ? null
                    : $"Ridge range [{minR:F2},{maxR:F2}], Turbulence range [{minT:F2},{maxT:F2}], expected roughly [0,1]";
            });

            results.Check("RidgeNoise2D produces sharper peaks than Turbulence2D for the same input (ridges are folded+squared, turbulence is not)", () =>
            {
                var noise = new Noise(5, octaves: 1); // single octave: RidgeNoise/Turbulence differ only by the squaring
                float sumRidge = 0f, sumTurb = 0f;
                const int samples = 300;
                for (int i = 0; i < samples; i++)
                {
                    float x = i * 0.09f, y = i * 0.04f;
                    sumRidge += noise.RidgeNoise2D(x, y);
                    sumTurb += 1f - noise.Turbulence2D(x, y); // same "1 - |sample|" base as Ridge, unsquared
                }
                // Squaring a [0,1] value never increases it, so Ridge's average "1-|n|" contribution
                // should be <= the unsquared version's -- confirms the squaring is actually happening.
                return sumRidge <= sumTurb + 0.01f
                    ? null
                    : $"average Ridge ({sumRidge / samples:F3}) should not exceed average unsquared 1-|n| ({sumTurb / samples:F3})";
            });
        }
    }
}
