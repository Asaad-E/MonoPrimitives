using System;

namespace MonoPrimitives
{
    /// <summary>
    /// Seedable gradient (Perlin-style) noise — smooth, deterministic pseudo-randomness for
    /// terrain heightmaps, procedural texture-like effects, or any "organic" variation that
    /// needs to look continuous rather than static-y. Output is roughly in [-1, 1] (not hard
    /// clamped — gradient noise can slightly overshoot that range at some inputs, same as any
    /// standard Perlin implementation). Construct once per seed and reuse — the seed only
    /// affects a one-time permutation-table shuffle, not the cost of each sample.
    /// </summary>
    public sealed class Noise
    {
        private readonly int[] _perm = new int[512];

        public Noise(int seed = 0)
        {
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
        /// 1D Perlin noise at x — a y=0, z=0 slice of <see cref="Sample3D"/>, same technique as
        /// <see cref="Sample2D"/>. A smooth, deterministic "wander" over one variable: a steering
        /// angle drifting over time, wind gust strength, camera shake — anywhere you'd otherwise
        /// reach for a random walk but want it continuous instead of jittery.
        /// </summary>
        public float Sample1D(float x) => Sample3D(x, 0f, 0f);

        /// <summary>
        /// Fractal Brownian motion: sums <paramref name="octaves"/> layers of <see cref="Sample2D"/>
        /// at increasing frequency (<paramref name="lacunarity"/> per octave) and decreasing
        /// amplitude (<paramref name="gain"/> per octave) — rougher, more natural-looking terrain
        /// than a single noise octave. Result is normalized back to roughly the same [-1,1] range
        /// as a single sample regardless of octave count.
        /// </summary>
        public float Fbm2D(float x, float y, int octaves = 4, float lacunarity = 2f, float gain = 0.5f)
        {
            float sum = 0f, amplitude = 1f, frequency = 1f, maxAmplitude = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Sample2D(x * frequency, y * frequency) * amplitude;
                maxAmplitude += amplitude;
                amplitude *= gain;
                frequency *= lacunarity;
            }
            return maxAmplitude > 1e-6f ? sum / maxAmplitude : 0f;
        }

        /// <summary>1D counterpart to <see cref="Fbm2D"/>.</summary>
        public float Fbm1D(float x, int octaves = 4, float lacunarity = 2f, float gain = 0.5f)
        {
            float sum = 0f, amplitude = 1f, frequency = 1f, maxAmplitude = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Sample1D(x * frequency) * amplitude;
                maxAmplitude += amplitude;
                amplitude *= gain;
                frequency *= lacunarity;
            }
            return maxAmplitude > 1e-6f ? sum / maxAmplitude : 0f;
        }

        /// <summary>3D counterpart to <see cref="Fbm2D"/>.</summary>
        public float Fbm3D(float x, float y, float z, int octaves = 4, float lacunarity = 2f, float gain = 0.5f)
        {
            float sum = 0f, amplitude = 1f, frequency = 1f, maxAmplitude = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Sample3D(x * frequency, y * frequency, z * frequency) * amplitude;
                maxAmplitude += amplitude;
                amplitude *= gain;
                frequency *= lacunarity;
            }
            return maxAmplitude > 1e-6f ? sum / maxAmplitude : 0f;
        }
    }
}
