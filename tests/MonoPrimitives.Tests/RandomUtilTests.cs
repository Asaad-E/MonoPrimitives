using System;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using MonoPrimitives;

namespace MonoPrimitives.Tests
{
    /// <summary>Pure-math checks for <see cref="RandomUtil"/> — no GraphicsDevice needed.</summary>
    internal static class RandomUtilTests
    {
        private const int LargeSampleCount = 200_000;

        public static void Run(TestResults results)
        {
            results.Check("Same seed produces the same sequence; different seeds diverge", () =>
            {
                var a = new RandomUtil(42);
                var b = new RandomUtil(42);
                var c = new RandomUtil(43);
                for (int i = 0; i < 20; i++)
                {
                    float av = a.NextUniform(), bv = b.NextUniform();
                    if (av != bv) return $"same-seed streams diverged at sample {i}: {av} != {bv}";
                }
                bool anyDifferent = false;
                for (int i = 0; i < 20; i++)
                    if (a.NextGaussian() != c.NextGaussian()) { anyDifferent = true; break; }
                return anyDifferent ? null : "different seeds produced an identical sequence (suspicious)";
            });

            results.Check("NextUniform stays within [min,max) over many samples", () =>
            {
                var rng = new RandomUtil(1);
                for (int i = 0; i < LargeSampleCount; i++)
                {
                    float v = rng.NextUniform(-3f, 5f);
                    if (v < -3f || v >= 5f) return $"NextUniform(-3,5) produced {v}, outside [-3,5)";
                }
                return null;
            });

            results.Check("NextInt stays within [min,max) and NextBool respects probability 0/1 edge cases", () =>
            {
                var rng = new RandomUtil(2);
                for (int i = 0; i < 10_000; i++)
                {
                    int v = rng.NextInt(-5, 5);
                    if (v < -5 || v >= 5) return $"NextInt(-5,5) produced {v}, outside [-5,5)";
                }
                for (int i = 0; i < 1000; i++)
                {
                    if (rng.NextBool(0f)) return "NextBool(0) returned true";
                    if (!rng.NextBool(1f)) return "NextBool(1) returned false";
                }
                return null;
            });

            results.Check("NextBool's true rate matches its probability over many samples", () =>
            {
                var rng = new RandomUtil(3);
                int trueCount = 0;
                for (int i = 0; i < LargeSampleCount; i++)
                    if (rng.NextBool(0.3f)) trueCount++;
                float rate = trueCount / (float)LargeSampleCount;
                return MathF.Abs(rate - 0.3f) < 0.01f ? null : $"NextBool(0.3) true rate was {rate:F3}, expected ~0.3";
            });

            results.Check("NextGaussian's sample mean/stddev match the requested parameters", () =>
            {
                var rng = new RandomUtil(4);
                double sum = 0, sumSq = 0;
                for (int i = 0; i < LargeSampleCount; i++)
                {
                    float v = rng.NextGaussian(mean: 10f, stdDev: 2f);
                    sum += v; sumSq += (double)v * v;
                }
                double mean = sum / LargeSampleCount;
                double variance = sumSq / LargeSampleCount - mean * mean;
                if (Math.Abs(mean - 10.0) > 0.05) return $"sample mean {mean:F3}, expected ~10";
                if (Math.Abs(Math.Sqrt(variance) - 2.0) > 0.05) return $"sample stddev {Math.Sqrt(variance):F3}, expected ~2";
                return null;
            });

            results.Check("NextExponential's sample mean matches 1/rate, and every sample is non-negative", () =>
            {
                var rng = new RandomUtil(5);
                double sum = 0;
                for (int i = 0; i < LargeSampleCount; i++)
                {
                    float v = rng.NextExponential(rate: 2f);
                    if (v < 0f) return $"NextExponential produced a negative value: {v}";
                    sum += v;
                }
                double mean = sum / LargeSampleCount;
                return Math.Abs(mean - 0.5) < 0.01 ? null : $"sample mean {mean:F3}, expected ~0.5 (1/rate)";
            });

            results.Check("NextLogNormal is always positive and its log matches the underlying Gaussian's mean", () =>
            {
                var rng = new RandomUtil(6);
                double sumLog = 0;
                for (int i = 0; i < LargeSampleCount; i++)
                {
                    float v = rng.NextLogNormal(mean: 1f, stdDev: 0.5f);
                    if (v <= 0f) return $"NextLogNormal produced a non-positive value: {v}";
                    sumLog += Math.Log(v);
                }
                double meanLog = sumLog / LargeSampleCount;
                return Math.Abs(meanLog - 1.0) < 0.02 ? null : $"mean(log(sample)) = {meanLog:F3}, expected ~1 (the underlying Gaussian's mean)";
            });

            results.Check("NextPoisson: mean/variance both regimes (direct simulation below the threshold, Gaussian approx above it)", () =>
            {
                var rng = new RandomUtil(7);
                bool CheckLambda(float lambda, double tolerance)
                {
                    double sum = 0, sumSq = 0;
                    for (int i = 0; i < LargeSampleCount; i++)
                    {
                        int v = rng.NextPoisson(lambda);
                        if (v < 0) return false;
                        sum += v; sumSq += (double)v * v;
                    }
                    double mean = sum / LargeSampleCount;
                    double variance = sumSq / LargeSampleCount - mean * mean;
                    // Poisson: mean == variance == lambda, by definition.
                    return Math.Abs(mean - lambda) < tolerance && Math.Abs(variance - lambda) < tolerance * 2;
                }
                if (!CheckLambda(5f, 0.1)) return "NextPoisson(5) (direct simulation regime) mean/variance did not match lambda=5";
                if (!CheckLambda(100f, 2.0)) return "NextPoisson(100) (Gaussian approximation regime, >30) mean/variance did not match lambda=100";
                return null;
            });

            results.Check("NextBinomial: mean/variance across all three regimes (direct, Poisson-approx, Gaussian-approx)", () =>
            {
                var rng = new RandomUtil(8);
                bool CheckTrialsProbability(int trials, float probability, double tolerance)
                {
                    double sum = 0, sumSq = 0;
                    const int samples = 50_000;
                    for (int i = 0; i < samples; i++)
                    {
                        int v = rng.NextBinomial(trials, probability);
                        if (v < 0 || v > trials) return false;
                        sum += v; sumSq += (double)v * v;
                    }
                    double mean = sum / samples;
                    double expectedMean = trials * probability;
                    double expectedVariance = expectedMean * (1 - probability);
                    double variance = sumSq / samples - mean * mean;
                    return Math.Abs(mean - expectedMean) < tolerance && Math.Abs(variance - expectedVariance) < tolerance * 3 + 0.5;
                }
                // Small trials/probability -- direct Bernoulli-trial simulation.
                if (!CheckTrialsProbability(20, 0.4f, 0.3)) return "NextBinomial(20, 0.4) (direct regime) mean/variance mismatch";
                // np(1-p) >= 9 -- Gaussian approximation regime.
                if (!CheckTrialsProbability(1000, 0.5f, 2.0)) return "NextBinomial(1000, 0.5) (Gaussian regime) mean/variance mismatch";
                // Large trials, tiny probability -- rare-event Poisson approximation regime.
                if (!CheckTrialsProbability(2_000_000, 0.00003f, 1.5)) return "NextBinomial(2_000_000, 0.00003) (Poisson regime) mean/variance mismatch";
                return null;
            });

            results.Check("NextOnUnitCircle is unit length; NextInsideUnitCircle stays within radius 1 and is area-uniform (not center-biased)", () =>
            {
                var rng = new RandomUtil(9);
                double sumRSq = 0;
                for (int i = 0; i < LargeSampleCount; i++)
                {
                    var onCircle = rng.NextOnUnitCircle();
                    if (MathF.Abs(onCircle.Length() - 1f) > 1e-4f) return $"NextOnUnitCircle length {onCircle.Length():F5}, expected 1";
                    var inside = rng.NextInsideUnitCircle();
                    if (inside.Length() > 1f + 1e-4f) return $"NextInsideUnitCircle length {inside.Length():F5}, expected <= 1";
                    sumRSq += inside.LengthSquared();
                }
                // A uniform-by-AREA disc sample has E[r^2] = 0.5; a naive uniform-by-RADIUS sample
                // (the bug this method's own doc comment says it avoids) would give E[r^2] = 1/3
                // instead -- distinct enough that this test would actually catch that regression.
                double meanRSq = sumRSq / LargeSampleCount;
                return Math.Abs(meanRSq - 0.5) < 0.01 ? null : $"E[r^2] = {meanRSq:F3}, expected ~0.5 (area-uniform); ~0.333 would mean a uniform-by-radius regression";
            });

            results.Check("NextOnUnitSphere is unit length and not pole-clustered; NextInsideUnitSphere stays within radius 1 and is volume-uniform", () =>
            {
                var rng = new RandomUtil(10);
                double sumZSq = 0, sumRCubed = 0;
                for (int i = 0; i < LargeSampleCount; i++)
                {
                    var onSphere = rng.NextOnUnitSphere();
                    if (MathF.Abs(onSphere.Length() - 1f) > 1e-4f) return $"NextOnUnitSphere length {onSphere.Length():F5}, expected 1";
                    sumZSq += (double)onSphere.Z * onSphere.Z;

                    var inside = rng.NextInsideUnitSphere();
                    if (inside.Length() > 1f + 1e-4f) return $"NextInsideUnitSphere length {inside.Length():F5}, expected <= 1";
                    sumRCubed += Math.Pow(inside.Length(), 3);
                }
                // z uniform in [-1,1] gives E[z^2] = 1/3; the naive "uniform theta/phi" parameterization
                // this method's doc comment says it avoids would cluster samples near the poles instead.
                double meanZSq = sumZSq / LargeSampleCount;
                if (Math.Abs(meanZSq - 1.0 / 3.0) > 0.01) return $"E[z^2] on the sphere = {meanZSq:F3}, expected ~0.333 (uniform, not pole-clustered)";
                // Radius is cbrt(uniform), so r^3 == the underlying uniform draw exactly -- E[r^3] = E[uniform] = 0.5.
                double meanRCubed = sumRCubed / LargeSampleCount;
                return Math.Abs(meanRCubed - 0.5) < 0.02 ? null : $"E[r^3] inside the sphere = {meanRCubed:F3}, expected ~0.5 (volume-uniform)";
            });

            results.Check("NextWeightedIndex: proportions match weights, degenerate single-weight case, and invalid input throws", () =>
            {
                var rng = new RandomUtil(11);
                float[] weights = { 1f, 3f, 6f }; // expect roughly 10%/30%/60%
                int[] counts = new int[3];
                const int samples = 100_000;
                for (int i = 0; i < samples; i++)
                {
                    int idx = rng.NextWeightedIndex(weights);
                    if (idx < 0 || idx > 2) return $"NextWeightedIndex returned out-of-range index {idx}";
                    counts[idx]++;
                }
                float[] expected = { 0.1f, 0.3f, 0.6f };
                for (int i = 0; i < 3; i++)
                {
                    float rate = counts[i] / (float)samples;
                    if (MathF.Abs(rate - expected[i]) > 0.01f) return $"index {i}: rate {rate:F3}, expected ~{expected[i]:F1}";
                }

                // A single nonzero weight among zeros must deterministically always pick that index.
                float[] onlyOne = { 0f, 5f, 0f };
                for (int i = 0; i < 100; i++)
                    if (rng.NextWeightedIndex(onlyOne) != 1) return "expected the only nonzero weight's index (1) every time";

                bool Throws(Action action) { try { action(); return false; } catch (ArgumentException) { return true; } }
                if (!Throws(() => rng.NextWeightedIndex(Array.Empty<float>()))) return "empty weights did not throw";
                if (!Throws(() => rng.NextWeightedIndex(new float[] { 0f, 0f }))) return "all-zero weights did not throw";
                if (!Throws(() => rng.NextWeightedIndex(new float[] { 1f, -1f }))) return "a negative weight did not throw";
                return null;
            });

            results.Check("NextItem always returns an element from the span, covers all of them, and throws on empty", () =>
            {
                var rng = new RandomUtil(12);
                string[] items = { "a", "b", "c" };
                var seen = new System.Collections.Generic.HashSet<string>();
                for (int i = 0; i < 1000; i++)
                {
                    string picked = rng.NextItem<string>(items);
                    if (Array.IndexOf(items, picked) < 0) return $"NextItem returned '{picked}', not one of the input items";
                    seen.Add(picked);
                }
                if (seen.Count != 3) return $"NextItem only ever returned {seen.Count}/3 distinct items over 1000 draws (suspicious)";

                bool threw = false;
                try { rng.NextItem<string>(Array.Empty<string>()); } catch (ArgumentException) { threw = true; }
                return threw ? null : "NextItem on an empty span did not throw";
            });

            results.Check("NextGaussianVector2/3: per-axis sample mean/stddev match the requested parameters", () =>
            {
                var rng = new RandomUtil(13);
                double sumX = 0, sumY = 0, sumZ = 0, sumSqX = 0, sumSqY = 0, sumSqZ = 0;
                for (int i = 0; i < LargeSampleCount; i++)
                {
                    var v2 = rng.NextGaussianVector2(new Vector2(5f, -5f), 2f);
                    sumX += v2.X; sumSqX += (double)v2.X * v2.X;
                    sumY += v2.Y; sumSqY += (double)v2.Y * v2.Y;

                    var v3 = rng.NextGaussianVector3(new Vector3(1f, 2f, 3f), 1f);
                    sumZ += v3.Z; sumSqZ += (double)v3.Z * v3.Z;
                }
                double meanX = sumX / LargeSampleCount, stdDevX = Math.Sqrt(sumSqX / LargeSampleCount - meanX * meanX);
                double meanY = sumY / LargeSampleCount, stdDevY = Math.Sqrt(sumSqY / LargeSampleCount - meanY * meanY);
                double meanZ = sumZ / LargeSampleCount, stdDevZ = Math.Sqrt(sumSqZ / LargeSampleCount - meanZ * meanZ);
                if (Math.Abs(meanX - 5.0) > 0.05 || Math.Abs(stdDevX - 2.0) > 0.05) return $"Vector2.X: mean {meanX:F3} stdDev {stdDevX:F3}, expected ~5/~2";
                if (Math.Abs(meanY - -5.0) > 0.05 || Math.Abs(stdDevY - 2.0) > 0.05) return $"Vector2.Y: mean {meanY:F3} stdDev {stdDevY:F3}, expected ~-5/~2";
                if (Math.Abs(meanZ - 3.0) > 0.05 || Math.Abs(stdDevZ - 1.0) > 0.05) return $"Vector3.Z: mean {meanZ:F3} stdDev {stdDevZ:F3}, expected ~3/~1";
                return null;
            });

            results.Check("UnderlyingRandom shares RandomUtil's own stream, not a separate one", () =>
            {
                // Two instances seeded identically: advance one purely through RandomUtil calls,
                // the other through a mix of RandomUtil calls and raw UnderlyingRandom calls that
                // consume the exact same number of underlying draws (NextSingle() each) -- if
                // UnderlyingRandom were a different Random instance, these would diverge.
                var a = new RandomUtil(77);
                var b = new RandomUtil(77);

                float a1 = a.NextUniform();
                float b1 = b.UnderlyingRandom.NextSingle(); // same underlying call NextUniform makes
                if (a1 != b1) return $"NextUniform vs UnderlyingRandom.NextSingle() diverged: {a1} != {b1}";

                int a2 = a.NextInt(0, 1000);
                int b2 = b.UnderlyingRandom.Next(0, 1000);
                if (a2 != b2) return $"NextInt vs UnderlyingRandom.Next() diverged: {a2} != {b2}";

                return null;
            });

            results.Check("RandomUtil.Shared works correctly across multiple threads concurrently", () =>
            {
                int total = 0;
                Parallel.For(0, 16, taskIndex =>
                {
                    for (int i = 0; i < 5000; i++)
                    {
                        _ = RandomUtil.Shared.NextGaussian();
                        _ = RandomUtil.Shared.NextInsideUnitCircle();
                        _ = RandomUtil.Shared.NextPoisson(10f);
                        System.Threading.Interlocked.Increment(ref total);
                    }
                });
                return total == 16 * 5000 ? null : $"expected 80000 completed iterations, got {total} (a thread likely threw)";
            });
        }
    }
}
