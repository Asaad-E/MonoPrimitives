#nullable enable

using System;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;

namespace MonoPrimitives
{
    /// <summary>
    /// Seedable float-based random sampling across the distributions a simulation typically
    /// reaches for (uniform, Bernoulli, Gaussian, exponential, Poisson, binomial, log-normal,
    /// plus points on/inside a unit circle or sphere) — one class instead of hand-rolling each
    /// via <see cref="Random"/> per call site. Construct once per seed and reuse, same as
    /// <see cref="Noise"/>.
    ///
    /// The instance API is not thread-safe (wraps one <see cref="Random"/> stream) — same
    /// single-threaded assumption every other stateful class in this library makes
    /// (<see cref="Noise"/>, <c>PrimitiveInput</c>, the cameras). Parallelizing a large
    /// agent-based simulation should give each thread/task its own <see cref="RandomUtil"/>
    /// instance (e.g. seeded <c>baseSeed + taskIndex</c>) rather than share one across threads —
    /// cheaper than locking, and reproducibility across threads is impossible either way since
    /// interleaving order isn't deterministic. If a seeded/reproducible stream isn't needed,
    /// <see cref="Shared"/> is a static, genuinely thread-safe alternative built on
    /// <see cref="Random.Shared"/> instead.
    /// </summary>
    public sealed class RandomUtil
    {
        private readonly Random _rng;

        // Marsaglia polar's rejection loop always produces a USABLE PAIR of independent standard
        // normal values on acceptance; caching the second and returning it next call halves the
        // average cost of NextGaussian instead of throwing it away.
        private float? _spareGaussian;

        /// <summary>
        /// The underlying seeded <see cref="Random"/> stream every method above advances — drop
        /// down to it directly for anything not wrapped here (<see cref="Random.NextBytes(byte[])"/>,
        /// <see cref="Random.NextInt64()"/>, <see cref="Random.NextDouble()"/>,
        /// <see cref="Random.Shuffle{T}(T[])"/>, or any future addition to <see cref="Random"/>
        /// itself), while staying on the exact same deterministic sequence as every
        /// <see cref="RandomUtil"/> call around it. Constructing a separate <c>new Random(seed)</c>
        /// yourself would desync into its own independent stream instead of sharing this one.
        /// </summary>
        public Random UnderlyingRandom => _rng;

        /// <summary>Creates a generator whose stream is fully determined by <paramref name="seed"/> — same seed always gives the same sequence of samples.</summary>
        public RandomUtil(int seed) => _rng = new Random(seed);

        /// <summary>Creates a generator seeded from the system clock — a different sequence every run.</summary>
        public RandomUtil() => _rng = new Random();

        /// <summary>Uniform float in [<paramref name="min"/>, <paramref name="max"/>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float NextUniform(float min = 0f, float max = 1f) => SampleUniform(_rng, min, max);

        /// <summary>Uniform integer in [<paramref name="minInclusive"/>, <paramref name="maxExclusive"/>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int NextInt(int minInclusive, int maxExclusive) => SampleInt(_rng, minInclusive, maxExclusive);

        /// <summary>A single Bernoulli trial: <see langword="true"/> with probability <paramref name="probability"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool NextBool(float probability = 0.5f) => SampleBool(_rng, probability);

        /// <summary>
        /// Normally-distributed ("Gaussian") sample with the given <paramref name="mean"/>/<paramref name="stdDev"/>.
        /// See <see cref="SampleGaussian"/> for the algorithm (Marsaglia polar method).
        /// </summary>
        public float NextGaussian(float mean = 0f, float stdDev = 1f) => SampleGaussian(_rng, ref _spareGaussian, mean, stdDev);

        /// <summary>Log-normal sample: <c>Exp(NextGaussian(mean, stdDev))</c> — <paramref name="mean"/>/<paramref name="stdDev"/> describe the underlying normal, not the output's own mean/stdDev (standard log-normal convention).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float NextLogNormal(float mean = 0f, float stdDev = 1f) => MathF.Exp(NextGaussian(mean, stdDev));

        /// <summary>Exponentially-distributed sample (mean 1/<paramref name="rate"/>) via inverse-CDF (<c>-ln(1-U)/rate</c>). <paramref name="rate"/> must be &gt; 0.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float NextExponential(float rate) => SampleExponential(_rng, rate);

        /// <inheritdoc cref="SamplePoisson(Random, ref float?, float)"/>
        public int NextPoisson(float lambda) => SamplePoisson(_rng, ref _spareGaussian, lambda);

        /// <inheritdoc cref="SampleBinomial(Random, ref float?, int, float)"/>
        public int NextBinomial(int trials, float probability) => SampleBinomial(_rng, ref _spareGaussian, trials, probability);

        /// <summary>Uniformly-distributed point on the unit circle's edge (unit length).</summary>
        public Vector2 NextOnUnitCircle() => SampleOnUnitCircle(_rng);

        /// <inheritdoc cref="SampleInsideUnitCircle(Random)"/>
        public Vector2 NextInsideUnitCircle() => SampleInsideUnitCircle(_rng);

        /// <summary>Uniformly-distributed point on the unit sphere's surface (unit length).</summary>
        public Vector3 NextOnUnitSphere() => SampleOnUnitSphere(_rng);

        /// <inheritdoc cref="SampleInsideUnitSphere(Random)"/>
        public Vector3 NextInsideUnitSphere() => SampleInsideUnitSphere(_rng);

        /// <inheritdoc cref="SampleWeightedIndex(Random, ReadOnlySpan{float})"/>
        public int NextWeightedIndex(ReadOnlySpan<float> weights) => SampleWeightedIndex(_rng, weights);

        // ------------------------------------------------------------------
        // Thread-safe static counterpart
        // ------------------------------------------------------------------

        /// <summary>
        /// Static, thread-safe mirror of every instance method above, built on
        /// <see cref="Random.Shared"/> (itself thread-safe — .NET gives each thread its own
        /// internal stream) instead of a seeded per-instance stream. No seed constructor here:
        /// <see cref="Random.Shared"/> has no single reproducible sequence to seed in the first
        /// place once more than one thread touches it, so reproducibility was never on the table
        /// for this path — use the instance API instead when a deterministic sequence matters.
        /// <see cref="NextGaussian(float, float)"/>'s spare-value cache is <c>[ThreadStatic]</c>
        /// here rather than a single shared field, so each thread gets its own cache slot with no
        /// locking needed — the same strategy .NET's own <see cref="Random.Shared"/> uses
        /// internally for its underlying stream.
        /// </summary>
        public static class Shared
        {
            // Named distinctly from RandomUtil's own _spareGaussian instance field (not just
            // scoped differently -- same name in both places would read as one shared cache to
            // anyone skimming the file, when they're deliberately two independent caches).
            [ThreadStatic] private static float? _threadSpareGaussian;

            /// <inheritdoc cref="RandomUtil.NextUniform(float, float)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static float NextUniform(float min = 0f, float max = 1f) => SampleUniform(Random.Shared, min, max);

            /// <inheritdoc cref="RandomUtil.NextInt(int, int)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int NextInt(int minInclusive, int maxExclusive) => SampleInt(Random.Shared, minInclusive, maxExclusive);

            /// <inheritdoc cref="RandomUtil.NextBool(float)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool NextBool(float probability = 0.5f) => SampleBool(Random.Shared, probability);

            /// <inheritdoc cref="RandomUtil.NextGaussian(float, float)"/>
            public static float NextGaussian(float mean = 0f, float stdDev = 1f) => SampleGaussian(Random.Shared, ref _threadSpareGaussian, mean, stdDev);

            /// <inheritdoc cref="RandomUtil.NextLogNormal(float, float)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static float NextLogNormal(float mean = 0f, float stdDev = 1f) => MathF.Exp(NextGaussian(mean, stdDev));

            /// <inheritdoc cref="RandomUtil.NextExponential(float)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static float NextExponential(float rate) => SampleExponential(Random.Shared, rate);

            /// <inheritdoc cref="RandomUtil.NextPoisson(float)"/>
            public static int NextPoisson(float lambda) => SamplePoisson(Random.Shared, ref _threadSpareGaussian, lambda);

            /// <inheritdoc cref="RandomUtil.NextBinomial(int, float)"/>
            public static int NextBinomial(int trials, float probability) => SampleBinomial(Random.Shared, ref _threadSpareGaussian, trials, probability);

            /// <inheritdoc cref="RandomUtil.NextOnUnitCircle"/>
            public static Vector2 NextOnUnitCircle() => SampleOnUnitCircle(Random.Shared);

            /// <inheritdoc cref="RandomUtil.NextInsideUnitCircle"/>
            public static Vector2 NextInsideUnitCircle() => SampleInsideUnitCircle(Random.Shared);

            /// <inheritdoc cref="RandomUtil.NextOnUnitSphere"/>
            public static Vector3 NextOnUnitSphere() => SampleOnUnitSphere(Random.Shared);

            /// <inheritdoc cref="RandomUtil.NextInsideUnitSphere"/>
            public static Vector3 NextInsideUnitSphere() => SampleInsideUnitSphere(Random.Shared);

            /// <inheritdoc cref="RandomUtil.NextWeightedIndex(ReadOnlySpan{float})"/>
            public static int NextWeightedIndex(ReadOnlySpan<float> weights) => SampleWeightedIndex(Random.Shared, weights);
        }

        // ------------------------------------------------------------------
        // Shared algorithm cores -- parameterized on Random so the instance API (_rng) and
        // Shared (Random.Shared) run the exact same math with nothing duplicated between them.
        // ------------------------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SampleUniform(Random rng, float min, float max) => min + rng.NextSingle() * (max - min);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SampleInt(Random rng, int minInclusive, int maxExclusive) => rng.Next(minInclusive, maxExclusive);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool SampleBool(Random rng, float probability) => rng.NextSingle() < probability;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SampleExponential(Random rng, float rate) => -MathF.Log(1f - rng.NextSingle()) / rate;

        /// <summary>
        /// Normally-distributed sample with the given <paramref name="mean"/>/<paramref name="stdDev"/>.
        /// Uses the Marsaglia polar method (two uniforms rejection-sampled inside the unit disc,
        /// then <c>sqrt</c>/<c>log</c>) rather than Box-Muller specifically to avoid any
        /// <c>Sin</c>/<c>Cos</c> call — consistent with this library's existing "no trig on a hot
        /// path" convention elsewhere (<c>UnitCircleLut</c>/<c>TrigLut</c>). Acceptance rate is
        /// ~78.5% (unit disc area over the 2×2 square it's sampled from), and every acceptance
        /// yields two independent samples — the second is cached in <paramref name="spare"/> and
        /// returned on the next call.
        /// </summary>
        private static float SampleGaussian(Random rng, ref float? spare, float mean, float stdDev)
        {
            if (spare.HasValue)
            {
                float cached = spare.Value;
                spare = null;
                return mean + cached * stdDev;
            }

            float u, v, s;
            do
            {
                u = rng.NextSingle() * 2f - 1f;
                v = rng.NextSingle() * 2f - 1f;
                s = u * u + v * v;
            } while (s >= 1f || s <= 1e-12f); // s<=0 would take Log(0) below; excludes the (measure-zero) origin

            float scale = MathF.Sqrt(-2f * MathF.Log(s) / s);
            spare = v * scale;
            return mean + (u * scale) * stdDev;
        }

        /// <summary>
        /// Threshold above which <see cref="SamplePoisson"/>/<see cref="SampleBinomial"/> switch
        /// to a Gaussian approximation instead of direct simulation — both are otherwise
        /// O(lambda) / O(trials) per sample, an unbounded cost this library avoids elsewhere too
        /// (see <c>PrimitiveBatch.SegmentsForArc</c>'s capped segment count for the same
        /// reasoning). 30 is the standard textbook rule of thumb for Poisson→Normal.
        /// </summary>
        private const float GaussianApproxThreshold = 30f;

        /// <summary>np(1-p) threshold above which <see cref="SampleBinomial"/> uses the Gaussian approximation — the standard textbook rule of thumb for when Normal(np, sqrt(np(1-p))) stays a reasonable fit.</summary>
        private const float BinomialGaussianVarianceThreshold = 9f;

        /// <summary>
        /// Poisson-distributed non-negative integer count with rate <paramref name="lambda"/> (must be &gt;= 0).
        /// Uses Knuth's algorithm (repeated uniform multiplication) for <paramref name="lambda"/> up
        /// to <see cref="GaussianApproxThreshold"/> — exact, but O(lambda) per sample — then a
        /// rounded/clamped Normal(lambda, sqrt(lambda)) approximation above that, so a
        /// pandemic/population sim asking for a huge lambda never pays an unbounded cost.
        /// </summary>
        private static int SamplePoisson(Random rng, ref float? spare, float lambda)
        {
            if (lambda <= 0f) return 0;
            if (lambda > GaussianApproxThreshold)
                return Math.Max(0, (int)MathF.Round(SampleGaussian(rng, ref spare, lambda, MathF.Sqrt(lambda))));

            float limit = MathF.Exp(-lambda);
            int k = 0;
            float p = 1f;
            do
            {
                k++;
                p *= rng.NextSingle();
            } while (p > limit);
            return k - 1;
        }

        /// <summary>
        /// Binomial-distributed successes out of <paramref name="trials"/> at per-trial
        /// <paramref name="probability"/>. Three regimes, each picked to keep worst-case cost
        /// bounded regardless of how large <paramref name="trials"/> is: a Gaussian(np, sqrt(np(1-p)))
        /// approximation when variance is large enough for it to hold (standard np(1-p) &gt;= 9 rule);
        /// a Poisson(np) approximation when <paramref name="trials"/> is large but <paramref name="probability"/>
        /// is small enough that variance stays low (the rare-event-over-many-trials case an
        /// epidemic/population sim would actually hit — e.g. a small per-contact infection chance
        /// across a huge population); direct Bernoulli-trial simulation only when both are small.
        /// </summary>
        private static int SampleBinomial(Random rng, ref float? spare, int trials, float probability)
        {
            if (trials <= 0 || probability <= 0f) return 0;
            if (probability >= 1f) return trials;

            float mean = trials * probability;
            float variance = mean * (1f - probability);

            if (variance >= BinomialGaussianVarianceThreshold)
                return Math.Clamp((int)MathF.Round(SampleGaussian(rng, ref spare, mean, MathF.Sqrt(variance))), 0, trials);

            if (trials > GaussianApproxThreshold)
                return Math.Min(trials, SamplePoisson(rng, ref spare, mean));

            int successes = 0;
            for (int i = 0; i < trials; i++)
                if (SampleBool(rng, probability)) successes++;
            return successes;
        }

        // Deliberately plain MathF.Sin/Cos here, NOT UnitCircleLut/TrigLut: those live in the 2D
        // and 3D namespaces respectively, and RandomUtil is Core -- Core is the shared foundation
        // 2D/3D both depend ON, so it can't reach back into either without inverting that
        // dependency (see DECISIONS.md's Core-sharing rule). It's also a different cost profile
        // than the LUTs were built for: one Sin+Cos call per sample here, not per vertex of a
        // hundred-triangle shape, so raw trig is cheap enough not to matter.

        private static Vector2 SampleOnUnitCircle(Random rng)
        {
            float angle = rng.NextSingle() * MathHelper.TwoPi;
            return new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        }

        /// <summary>
        /// Uniformly-distributed point inside the unit disc (by area, not just by angle) — radius
        /// is <c>sqrt(uniform)</c>, not a plain uniform radius, since a plain uniform radius would
        /// bunch samples too densely near the center relative to the edge.
        /// </summary>
        private static Vector2 SampleInsideUnitCircle(Random rng) => SampleOnUnitCircle(rng) * MathF.Sqrt(rng.NextSingle());

        // z uniform in [-1,1], angle uniform, radius-at-that-z from the sphere equation -- NOT
        // "uniform theta, uniform phi" (the naive parameterization), which would visibly cluster
        // samples near the poles since latitude rings shrink there while getting equal sample
        // share. This z-uniform form is the standard fix: each latitude band's shrinking
        // circumference is exactly offset by z spending proportionally less range on it.
        private static Vector3 SampleOnUnitSphere(Random rng)
        {
            float z = rng.NextSingle() * 2f - 1f;
            float radiusXY = MathF.Sqrt(MathF.Max(0f, 1f - z * z));
            float angle = rng.NextSingle() * MathHelper.TwoPi;
            return new Vector3(radiusXY * MathF.Cos(angle), radiusXY * MathF.Sin(angle), z);
        }

        /// <summary>
        /// Uniformly-distributed point inside the unit ball (by volume) — radius is
        /// <c>cbrt(uniform)</c> (the 3D counterpart to <see cref="SampleInsideUnitCircle"/>'s
        /// <c>sqrt</c>), a closed-form radius rather than a rejection loop against a unit cube, so
        /// cost stays a fixed handful of ops per sample instead of a ~52%-acceptance retry loop.
        /// </summary>
        private static Vector3 SampleInsideUnitSphere(Random rng) => SampleOnUnitSphere(rng) * MathF.Cbrt(rng.NextSingle());

        /// <summary>
        /// Picks a random index into <paramref name="weights"/>, with probability proportional to
        /// each entry's own weight — a loot table, a weighted spawn/decision table. Weights must
        /// all be non-negative with at least one positive, or this throws. A single linear scan,
        /// O(n) per call with no state carried between calls — the right tool for a table that
        /// changes between picks, not a large static one sampled every frame (build your own
        /// cumulative-sum array once and binary-search it for that case instead).
        /// </summary>
        private static int SampleWeightedIndex(Random rng, ReadOnlySpan<float> weights)
        {
            if (weights.IsEmpty)
                throw new ArgumentException("weights must not be empty.", nameof(weights));

            float total = 0f;
            foreach (float w in weights)
            {
                if (w < 0f) throw new ArgumentException("weights must not contain negative values.", nameof(weights));
                total += w;
            }
            if (total <= 0f)
                throw new ArgumentException("weights must contain at least one positive value.", nameof(weights));

            float pick = rng.NextSingle() * total;
            float cumulative = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                cumulative += weights[i];
                if (pick < cumulative) return i;
            }
            return weights.Length - 1; // float-rounding safety net -- pick can land exactly at total
        }
    }
}
