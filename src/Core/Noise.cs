using System;

namespace MonoPrimitives
{
    /// <summary>
    /// Seedable gradient (Perlin-style) noise — smooth, deterministic pseudo-randomness for
    /// terrain heightmaps, procedural texture-like effects, or any "organic" variation that
    /// needs to look continuous rather than static-y. Construct once per seed and reuse.
    /// </summary>
    /// <remarks>
    /// Output is roughly in [-1, 1] (not hard clamped — gradient noise can slightly overshoot
    /// that range at some inputs, same as any standard Perlin implementation). The seed only
    /// affects a one-time permutation-table shuffle, not the cost of each sample.
    /// </remarks>
    public sealed class Noise
    {
        private readonly int[] _perm = new int[512];

        /// <summary>Octave count for <see cref="Fbm1D"/>/<see cref="Fbm2D"/>/<see cref="Fbm3D"/> and the Ridge/Turbulence variants. Editable; defaults to the value passed at construction.</summary>
        public int Octaves { get; set; }

        /// <summary>Frequency multiplier applied each octave (how much finer detail each layer adds). Editable; defaults to the value passed at construction.</summary>
        public float Lacunarity { get; set; }

        /// <summary>Amplitude multiplier applied each octave (how much each finer layer contributes). Editable; defaults to the value passed at construction.</summary>
        public float Gain { get; set; }

        /// <summary>
        /// Creates a noise generator whose permutation table is shuffled from <paramref name="seed"/>
        /// — same seed always gives the same sequence of samples. <paramref name="octaves"/>/
        /// <paramref name="lacunarity"/>/<paramref name="gain"/> set the initial
        /// <see cref="Octaves"/>/<see cref="Lacunarity"/>/<see cref="Gain"/> (editable afterward).
        /// </summary>
        /// <remarks>
        /// One consistent fBm configuration per generator, the same shape <see cref="Primitives2D.Camera2D"/>'s
        /// <c>MoveSpeed</c> or <see cref="PrimitiveInput"/>'s <c>DoubleClickTime</c> use, rather than
        /// repeating the same three values at every <see cref="Fbm2D"/> call site. Construct a second
        /// <see cref="Noise"/> with the same seed (cheap — just a permutation shuffle) if you
        /// genuinely need two different fBm configurations sampling the same underlying gradient field.
        /// </remarks>
        public Noise(int seed = 0, int octaves = 4, float lacunarity = 2f, float gain = 0.5f)
        {
            Octaves = octaves;
            Lacunarity = lacunarity;
            Gain = gain;

            var p = new int[256];
            for (int i = 0; i < 256; i++) p[i] = i;

            var rng = new Random(seed);
            for (int i = 255; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (p[i], p[j]) = (p[j], p[i]);
            }
            for (int i = 0; i < 512; i++) _perm[i] = p[i & 255];
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static float Lerp(float t, float a, float b) => a + t * (b - a);

        // Standard "improved Perlin noise" gradient selection (Perlin 2002): hashes to one of 12
        // gradient directions using only cheap sign/select logic, no lookup table of vectors.
        private static float Grad(int hash, float x, float y, float z)
        {
            int h = hash & 15;
            float u = h < 8 ? x : y;
            float v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }

        /// <summary>3D Perlin noise at (x, y, z).</summary>
        public float Sample3D(float x, float y, float z)
        {
            int xi = (int)MathF.Floor(x) & 255;
            int yi = (int)MathF.Floor(y) & 255;
            int zi = (int)MathF.Floor(z) & 255;

            float xf = x - MathF.Floor(x);
            float yf = y - MathF.Floor(y);
            float zf = z - MathF.Floor(z);

            float u = Fade(xf), v = Fade(yf), w = Fade(zf);

            int a = _perm[xi] + yi, aa = _perm[a] + zi, ab = _perm[a + 1] + zi;
            int b = _perm[xi + 1] + yi, ba = _perm[b] + zi, bb = _perm[b + 1] + zi;

            return Lerp(w,
                Lerp(v,
                    Lerp(u, Grad(_perm[aa], xf, yf, zf), Grad(_perm[ba], xf - 1f, yf, zf)),
                    Lerp(u, Grad(_perm[ab], xf, yf - 1f, zf), Grad(_perm[bb], xf - 1f, yf - 1f, zf))),
                Lerp(v,
                    Lerp(u, Grad(_perm[aa + 1], xf, yf, zf - 1f), Grad(_perm[ba + 1], xf - 1f, yf, zf - 1f)),
                    Lerp(u, Grad(_perm[ab + 1], xf, yf - 1f, zf - 1f), Grad(_perm[bb + 1], xf - 1f, yf - 1f, zf - 1f))));
        }

        /// <summary>2D Perlin noise at (x, y) — a z=0 slice of <see cref="Sample3D"/>, the standard way to specialize Perlin noise down a dimension.</summary>
        public float Sample2D(float x, float y) => Sample3D(x, y, 0f);

        /// <summary>
        /// 1D Perlin noise at x. A smooth, deterministic "wander" over one variable: a steering
        /// angle drifting over time, wind gust strength, camera shake — anywhere you'd otherwise
        /// reach for a random walk but want it continuous instead of jittery.
        /// </summary>
        /// <remarks>
        /// Deliberately NOT a y=0,z=0 slice of <see cref="Sample3D"/> the way <see cref="Sample2D"/>
        /// is (a z=0 slice) — <see cref="Grad"/>'s 12-direction gradient table has several hash
        /// values whose x-facing component actually reads <c>y</c> or <c>z</c> instead of <c>x</c>;
        /// fixing BOTH y and z to 0 makes those cases evaluate to exactly zero, so a naive 3D
        /// slice was empirically ~23% near-zero output versus ~2% for a normal 2D/3D sample (11x
        /// more "flat" regions than real noise). This uses a dedicated 1D gradient (±1 per hash
        /// bit, the standard approach) instead, which doesn't have that degenerate case.
        /// </remarks>
        public float Sample1D(float x)
        {
            int xi = (int)MathF.Floor(x) & 255;
            float xf = x - MathF.Floor(x);
            float u = Fade(xf);

            float g0 = (_perm[xi] & 1) == 0 ? xf : -xf;
            float g1 = (_perm[xi + 1] & 1) == 0 ? xf - 1f : -(xf - 1f);
            return Lerp(u, g0, g1);
        }

        /// <summary>
        /// Fractal Brownian motion: sums <see cref="Octaves"/> layers of <see cref="Sample2D"/> at
        /// increasing frequency and decreasing amplitude, normalized back to roughly the same
        /// [-1,1] range as a single sample regardless of octave count.
        /// </summary>
        /// <remarks>
        /// Frequency scales by <see cref="Lacunarity"/> and amplitude by <see cref="Gain"/> each
        /// octave — more octaves means rougher, more natural-looking terrain than a single noise
        /// octave alone.
        /// </remarks>
        public float Fbm2D(float x, float y)
        {
            float sum = 0f, amplitude = 1f, frequency = 1f, maxAmplitude = 0f;
            for (int i = 0; i < Octaves; i++)
            {
                sum += Sample2D(x * frequency, y * frequency) * amplitude;
                maxAmplitude += amplitude;
                amplitude *= Gain;
                frequency *= Lacunarity;
            }
            return maxAmplitude > 1e-6f ? sum / maxAmplitude : 0f;
        }

        /// <summary>1D counterpart to <see cref="Fbm2D"/>.</summary>
        public float Fbm1D(float x)
        {
            float sum = 0f, amplitude = 1f, frequency = 1f, maxAmplitude = 0f;
            for (int i = 0; i < Octaves; i++)
            {
                sum += Sample1D(x * frequency) * amplitude;
                maxAmplitude += amplitude;
                amplitude *= Gain;
                frequency *= Lacunarity;
            }
            return maxAmplitude > 1e-6f ? sum / maxAmplitude : 0f;
        }

        /// <summary>3D counterpart to <see cref="Fbm2D"/>.</summary>
        public float Fbm3D(float x, float y, float z)
        {
            float sum = 0f, amplitude = 1f, frequency = 1f, maxAmplitude = 0f;
            for (int i = 0; i < Octaves; i++)
            {
                sum += Sample3D(x * frequency, y * frequency, z * frequency) * amplitude;
                maxAmplitude += amplitude;
                amplitude *= Gain;
                frequency *= Lacunarity;
            }
            return maxAmplitude > 1e-6f ? sum / maxAmplitude : 0f;
        }

        /// <summary>
        /// Ridged multifractal noise — the standard look for mountain-ridge terrain. Result is
        /// naturally in roughly <c>[0,1]</c>, not <c>[-1,1]</c> like <see cref="Fbm2D"/>. Uses
        /// <see cref="Octaves"/>/<see cref="Lacunarity"/>/<see cref="Gain"/>, same as <see cref="Fbm2D"/>.
        /// </summary>
        /// <remarks>
        /// Each octave folds <see cref="Sample2D"/> through <c>(1 - |sample|)²</c> before summing, so
        /// values near a lattice's zero-crossings (where <c>|sample|</c> is smallest) become sharp
        /// ridges instead of smooth rolling hills. Squaring a folded, already-positive value can
        /// never go negative, which is why the result stays non-negative unlike <see cref="Fbm2D"/>.
        /// </remarks>
        public float RidgeNoise2D(float x, float y)
        {
            float sum = 0f, amplitude = 1f, frequency = 1f, maxAmplitude = 0f;
            for (int i = 0; i < Octaves; i++)
            {
                float n = 1f - MathF.Abs(Sample2D(x * frequency, y * frequency));
                sum += n * n * amplitude;
                maxAmplitude += amplitude;
                amplitude *= Gain;
                frequency *= Lacunarity;
            }
            return maxAmplitude > 1e-6f ? sum / maxAmplitude : 0f;
        }

        /// <summary>3D counterpart to <see cref="RidgeNoise2D"/>.</summary>
        public float RidgeNoise3D(float x, float y, float z)
        {
            float sum = 0f, amplitude = 1f, frequency = 1f, maxAmplitude = 0f;
            for (int i = 0; i < Octaves; i++)
            {
                float n = 1f - MathF.Abs(Sample3D(x * frequency, y * frequency, z * frequency));
                sum += n * n * amplitude;
                maxAmplitude += amplitude;
                amplitude *= Gain;
                frequency *= Lacunarity;
            }
            return maxAmplitude > 1e-6f ? sum / maxAmplitude : 0f;
        }

        /// <summary>
        /// Turbulence: a rougher, "billowy" look than <see cref="Fbm2D"/>. Naturally in roughly
        /// <c>[0,1]</c>, like <see cref="RidgeNoise2D"/>, not <c>[-1,1]</c>. Uses
        /// <see cref="Octaves"/>/<see cref="Lacunarity"/>/<see cref="Gain"/>.
        /// </summary>
        /// <remarks>
        /// Sums <c>|<see cref="Sample2D"/>|</c> per octave instead of the signed value, which creases
        /// at every zero-crossing instead of leaving smooth troughs.
        /// </remarks>
        public float Turbulence2D(float x, float y)
        {
            float sum = 0f, amplitude = 1f, frequency = 1f, maxAmplitude = 0f;
            for (int i = 0; i < Octaves; i++)
            {
                sum += MathF.Abs(Sample2D(x * frequency, y * frequency)) * amplitude;
                maxAmplitude += amplitude;
                amplitude *= Gain;
                frequency *= Lacunarity;
            }
            return maxAmplitude > 1e-6f ? sum / maxAmplitude : 0f;
        }

        /// <summary>3D counterpart to <see cref="Turbulence2D"/>.</summary>
        public float Turbulence3D(float x, float y, float z)
        {
            float sum = 0f, amplitude = 1f, frequency = 1f, maxAmplitude = 0f;
            for (int i = 0; i < Octaves; i++)
            {
                sum += MathF.Abs(Sample3D(x * frequency, y * frequency, z * frequency)) * amplitude;
                maxAmplitude += amplitude;
                amplitude *= Gain;
                frequency *= Lacunarity;
            }
            return maxAmplitude > 1e-6f ? sum / maxAmplitude : 0f;
        }
    }
}
