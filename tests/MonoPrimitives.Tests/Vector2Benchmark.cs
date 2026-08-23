using System;
using System.Diagnostics;
using XnaVec2 = Microsoft.Xna.Framework.Vector2;
using NumVec2 = System.Numerics.Vector2;

namespace MonoPrimitives.Tests
{
    /// <summary>
    /// Compares MonoGame's own <c>Microsoft.Xna.Framework.Vector2</c> against
    /// <c>System.Numerics.Vector2</c> (hardware-SIMD-backed on most platforms) for common 2D math.
    /// Informational only, like <c>FastTexture</c>'s original benchmark was before it became real
    /// API — never gates pass/fail, since results are machine-dependent. This library stays on
    /// MonoGame's own <c>Vector2</c> regardless of the outcome: every public API already takes and
    /// returns it, and switching would mean a conversion at every call site into/out of this library
    /// for no behavior change, not a drop-in swap.
    /// </summary>
    internal static class Vector2Benchmark
    {
        private const int Iterations = 2_000_000;
        private const int Warmup = 200_000;

        public static void Run(TestResults results)
        {
            Console.WriteLine();
            Console.WriteLine("-- Vector2 benchmark: XNA (Microsoft.Xna.Framework) vs System.Numerics (informational only) --");

            RunOp("Dot", DotXna, DotNum);
            RunOp("Add (sum)", AddXna, AddNum);
            RunOp("Scalar multiply", MulXna, MulNum);
            RunOp("Normalize", NormalizeXna, NormalizeNum);
            RunOp("Distance", DistanceXna, DistanceNum);
            RunMixedScenario();

            results.Check("Vector2Benchmark ran without throwing", () => null);
        }

        // ------------------------------------------------------------------
        // Individual-op battery
        // ------------------------------------------------------------------

        private static void RunOp(string name, Func<int, float> xna, Func<int, float> num)
        {
            xna(Warmup); num(Warmup); // JIT warmup, both paths

            var sw = Stopwatch.StartNew();
            float xnaSink = xna(Iterations);
            sw.Stop();
            double xnaMs = sw.Elapsed.TotalMilliseconds;

            sw.Restart();
            float numSink = num(Iterations);
            sw.Stop();
            double numMs = sw.Elapsed.TotalMilliseconds;

            Console.WriteLine($"  [{name,-16}] XNA: {xnaMs,7:F2}ms  System.Numerics: {numMs,7:F2}ms  ({xnaMs / numMs:F2}x)  (sink: {xnaSink:F3}/{numSink:F3})");
        }

        private static float DotXna(int n)
        {
            var a = new XnaVec2(1.3f, 2.7f);
            var b = new XnaVec2(-0.5f, 3.1f);
            float sum = 0f;
            for (int i = 0; i < n; i++) sum += XnaVec2.Dot(a, b);
            return sum;
        }

        private static float DotNum(int n)
        {
            var a = new NumVec2(1.3f, 2.7f);
            var b = new NumVec2(-0.5f, 3.1f);
            float sum = 0f;
            for (int i = 0; i < n; i++) sum += NumVec2.Dot(a, b);
            return sum;
        }

        private static float AddXna(int n)
        {
            var a = new XnaVec2(1.3f, 2.7f);
            var b = new XnaVec2(-0.5f, 3.1f);
            var acc = XnaVec2.Zero;
            for (int i = 0; i < n; i++) acc += a + b;
            return acc.X + acc.Y;
        }

        private static float AddNum(int n)
        {
            var a = new NumVec2(1.3f, 2.7f);
            var b = new NumVec2(-0.5f, 3.1f);
            var acc = NumVec2.Zero;
            for (int i = 0; i < n; i++) acc += a + b;
            return acc.X + acc.Y;
        }

        private static float MulXna(int n)
        {
            var a = new XnaVec2(1.3f, 2.7f);
            var acc = XnaVec2.Zero;
            for (int i = 0; i < n; i++) acc += a * 1.0001f;
            return acc.X + acc.Y;
        }

        private static float MulNum(int n)
        {
            var a = new NumVec2(1.3f, 2.7f);
            var acc = NumVec2.Zero;
            for (int i = 0; i < n; i++) acc += a * 1.0001f;
            return acc.X + acc.Y;
        }

        private static float NormalizeXna(int n)
        {
            var a = new XnaVec2(1.3f, 2.7f);
            float sum = 0f;
            for (int i = 0; i < n; i++)
            {
                var v = a * (1f + (i & 1));
                v.Normalize();
                sum += v.X;
            }
            return sum;
        }

        private static float NormalizeNum(int n)
        {
            var a = new NumVec2(1.3f, 2.7f);
            float sum = 0f;
            for (int i = 0; i < n; i++)
            {
                var v = NumVec2.Normalize(a * (1f + (i & 1)));
                sum += v.X;
            }
            return sum;
        }

        private static float DistanceXna(int n)
        {
            var a = new XnaVec2(1.3f, 2.7f);
            var b = new XnaVec2(-0.5f, 3.1f);
            float sum = 0f;
            for (int i = 0; i < n; i++) sum += XnaVec2.Distance(a, b);
            return sum;
        }

        private static float DistanceNum(int n)
        {
            var a = new NumVec2(1.3f, 2.7f);
            var b = new NumVec2(-0.5f, 3.1f);
            float sum = 0f;
            for (int i = 0; i < n; i++) sum += NumVec2.Distance(a, b);
            return sum;
        }

        // ------------------------------------------------------------------
        // Mixed realistic scenario: a simple particle-update loop (position += velocity*dt,
        // velocity steered toward a target, distance check) -- several ops per particle per frame,
        // the actual shape of a hot loop this library's callers would run every frame.
        // ------------------------------------------------------------------

        private const int ParticleCount = 20_000;
        private const int Frames = 300;

        private static void RunMixedScenario()
        {
            RunMixedXna(50); // warmup
            var sw = Stopwatch.StartNew();
            float xnaSink = RunMixedXna(Frames);
            sw.Stop();
            double xnaMs = sw.Elapsed.TotalMilliseconds;

            RunMixedNum(50);
            sw.Restart();
            float numSink = RunMixedNum(Frames);
            sw.Stop();
            double numMs = sw.Elapsed.TotalMilliseconds;

            Console.WriteLine($"  [Mixed scenario  ] XNA: {xnaMs,7:F2}ms  System.Numerics: {numMs,7:F2}ms  ({xnaMs / numMs:F2}x)  ({ParticleCount} particles x {Frames} frames, sink: {xnaSink:F3}/{numSink:F3})");
        }

        private static float RunMixedXna(int frames)
        {
            var positions = new XnaVec2[ParticleCount];
            var velocities = new XnaVec2[ParticleCount];
            var rand = new Random(1);
            for (int i = 0; i < ParticleCount; i++)
            {
                positions[i] = new XnaVec2((float)rand.NextDouble() * 1000f, (float)rand.NextDouble() * 1000f);
                velocities[i] = new XnaVec2((float)rand.NextDouble() - 0.5f, (float)rand.NextDouble() - 0.5f);
            }
            var target = new XnaVec2(500f, 500f);
            const float dt = 1f / 60f;
            float sink = 0f;

            for (int f = 0; f < frames; f++)
            {
                for (int i = 0; i < ParticleCount; i++)
                {
                    XnaVec2 toTarget = target - positions[i];
                    float dist = toTarget.Length();
                    if (dist > 0.001f)
                    {
                        XnaVec2 dir = toTarget / dist;
                        velocities[i] = XnaVec2.Lerp(velocities[i], dir * 100f, 0.05f);
                    }
                    positions[i] += velocities[i] * dt;
                    sink += dist;
                }
            }
            return sink;
        }

        private static float RunMixedNum(int frames)
        {
            var positions = new NumVec2[ParticleCount];
            var velocities = new NumVec2[ParticleCount];
            var rand = new Random(1);
            for (int i = 0; i < ParticleCount; i++)
            {
                positions[i] = new NumVec2((float)rand.NextDouble() * 1000f, (float)rand.NextDouble() * 1000f);
                velocities[i] = new NumVec2((float)rand.NextDouble() - 0.5f, (float)rand.NextDouble() - 0.5f);
            }
            var target = new NumVec2(500f, 500f);
            const float dt = 1f / 60f;
            float sink = 0f;

            for (int f = 0; f < frames; f++)
            {
                for (int i = 0; i < ParticleCount; i++)
                {
                    NumVec2 toTarget = target - positions[i];
                    float dist = toTarget.Length();
                    if (dist > 0.001f)
                    {
                        NumVec2 dir = toTarget / dist;
                        velocities[i] = NumVec2.Lerp(velocities[i], dir * 100f, 0.05f);
                    }
                    positions[i] += velocities[i] * dt;
                    sink += dist;
                }
            }
            return sink;
        }
    }
}
