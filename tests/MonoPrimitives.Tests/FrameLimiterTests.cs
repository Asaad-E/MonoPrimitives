using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using MonoPrimitives;

namespace MonoPrimitives.Tests
{
    /// <summary>
    /// Real-time checks for <see cref="FrameLimiter"/> — actually measures achieved frame times with
    /// a <see cref="Stopwatch"/> rather than reasoning about the sleep/spin math, since timing claims
    /// need a real clock to trust (see DECISIONS.md for how a synthetic, non-real-time benchmark
    /// misled this exact area of the codebase once already).
    /// </summary>
    internal static class FrameLimiterTests
    {
        public static void Run(Game game, TestResults results)
        {
            results.Check("FrameLimiter: constructor rejects a null Game, a non-positive target FPS, a negative maxFrameTime, or a non-positive fpsSampleCount", () =>
            {
                try { _ = new FrameLimiter(null, 60f); return "expected ArgumentNullException for a null Game"; }
                catch (ArgumentNullException) { }

                try { _ = new FrameLimiter(game, 0f); return "expected ArgumentOutOfRangeException for targetFps <= 0"; }
                catch (ArgumentOutOfRangeException) { }

                try { _ = new FrameLimiter(game, 60f, -0.01f); return "expected ArgumentOutOfRangeException for a negative maxFrameTime"; }
                catch (ArgumentOutOfRangeException) { }

                try
                {
                    _ = new FrameLimiter(game, 60f, fpsSampleCount: 0);
                    return "expected ArgumentOutOfRangeException for fpsSampleCount <= 0";
                }
                catch (ArgumentOutOfRangeException ex) when (ex.ParamName == "fpsSampleCount") { }
                return null;
            });

            results.Check("FrameLimiter: achieves close to the requested frame time on average, over real wall-clock time", () =>
            {
                var limiter = new FrameLimiter(game, 200f); // 5ms target -- fast enough to keep the test itself short
                const double targetMs = 1000.0 / 200f;
                const int frames = 60;

                var sw = Stopwatch.StartNew();
                for (int i = 0; i < frames; i++)
                {
                    limiter.BeginFrame();
                    limiter.EndFrame();
                }
                sw.Stop();

                double avgMs = sw.Elapsed.TotalMilliseconds / frames;
                // Generous tolerance: real OS scheduling jitter (measured separately, see DECISIONS.md)
                // occasionally makes one frame in ~20 run a full extra frame long. Over 60 frames that
                // moves the average by well under 1ms; this only catches the limiter being fundamentally
                // broken (e.g. not sleeping/spinning at all, or targeting the wrong duration).
                if (avgMs < targetMs - 1.0 || avgMs > targetMs + 3.0)
                    return $"average frame time {avgMs:F3}ms is too far from the {targetMs:F3}ms target";
                return null;
            });

            results.Check("FrameLimiter: TargetFps is live -- changing it takes effect on the very next EndFrame", () =>
            {
                var limiter = new FrameLimiter(game, 500f) { }; // 2ms
                limiter.BeginFrame();
                limiter.EndFrame(); // warm up, discard

                limiter.TargetFps = 100f; // 10ms
                var sw = Stopwatch.StartNew();
                limiter.BeginFrame();
                limiter.EndFrame();
                sw.Stop();

                double ms = sw.Elapsed.TotalMilliseconds;
                if (ms < 8.0) return $"expected roughly 10ms after switching TargetFps to 100, got {ms:F3}ms -- change didn't take effect";
                return null;
            });

            results.Check("FrameLimiter: EndFrame returns immediately if the frame's own work already overran the target", () =>
            {
                var limiter = new FrameLimiter(game, 1000f); // 1ms target, trivial to overrun
                limiter.BeginFrame();
                System.Threading.Thread.Sleep(20); // blow well past the 1ms target

                var sw = Stopwatch.StartNew();
                limiter.EndFrame();
                sw.Stop();

                if (sw.Elapsed.TotalMilliseconds > 2.0)
                    return $"EndFrame took {sw.Elapsed.TotalMilliseconds:F3}ms extra after the frame already overran -- should return immediately";
                return null;
            });

            results.Check("FrameLimiter.Elapsed reads mid-frame, before EndFrame, and tracks real wall-clock time", () =>
            {
                var limiter = new FrameLimiter(game, 60f);
                limiter.BeginFrame();
                System.Threading.Thread.Sleep(15);
                double elapsedMs = limiter.Elapsed.TotalMilliseconds;
                if (elapsedMs < 10.0) return $"expected Elapsed to reflect the real ~15ms Sleep mid-frame, got {elapsedMs:F3}ms -- Elapsed isn't reading a live Stopwatch";
                limiter.EndFrame(); // shouldn't throw or misbehave after reading Elapsed mid-frame
                return null;
            });

            results.Check("FrameLimiter.BeginFrame: reports 0 on the first call, then the real gap since the previous call, unclamped by default", () =>
            {
                var limiter = new FrameLimiter(game, 60f);
                float first = limiter.BeginFrame();
                if (first != 0f) return $"expected 0 on the first BeginFrame call, got {first:F4}";
                if (limiter.FrameTime != 0f) return $"expected FrameTime == 0 after the first call, got {limiter.FrameTime:F4}";

                System.Threading.Thread.Sleep(15);
                float second = limiter.BeginFrame();
                if (second < 0.010) return $"expected roughly 0.015s after a real 15ms sleep, got {second:F4}s";
                if (limiter.FrameTime != second) return "expected FrameTime to match BeginFrame's own return value";
                return null;
            });

            results.Check("FrameLimiter.BeginFrame: seeds the FPS counter with an expected (1/TargetFps) sample on the first call, not a phantom 0 that would skew AverageFps", () =>
            {
                var limiter = new FrameLimiter(game, 200f); // 5ms target
                limiter.BeginFrame(); // first call -- no real previous frame to measure

                // The seeded sample is exactly 1/TargetFps, so with nothing else in the window
                // AverageFps should read essentially exactly TargetFps, not 0 or an inflated value.
                if (MathF.Abs(limiter.AverageFps - 200f) > 0.5f)
                    return $"expected AverageFps ~200 right after the seeded first call, got {limiter.AverageFps:F3}";
                return null;
            });

            results.Check("FrameLimiter.MaxFrameTime: clamps a real slow frame's reported FrameTime instead of reporting its full duration", () =>
            {
                var limiter = new FrameLimiter(game, 60f, maxFrameTime: 0.05f); // 50ms cap
                limiter.BeginFrame();
                System.Threading.Thread.Sleep(150); // a real spike, well past the 50ms cap

                float frameTime = limiter.BeginFrame();
                if (frameTime > 0.06f) return $"expected FrameTime clamped to roughly 0.05s, got {frameTime:F4}s";
                if (limiter.FrameTime != frameTime) return "expected FrameTime property to match BeginFrame's own return value";
                return null;
            });

            results.Check("FrameLimiter.MaxFrameTime: 0 (default) disables clamping even on a real slow frame", () =>
            {
                var limiter = new FrameLimiter(game, 60f); // maxFrameTime defaults to 0
                limiter.BeginFrame();
                System.Threading.Thread.Sleep(100);

                float frameTime = limiter.BeginFrame();
                if (frameTime < 0.09f) return $"expected the real ~0.1s frame reported unclamped, got {frameTime:F4}s";
                return null;
            });

            results.Check("FrameLimiter: composes an internal FpsCounter -- AverageFps/CurrentFps/*FrameTimeMs are 0 before the first BeginFrame, and FpsSampleCount echoes the constructor arg", () =>
            {
                var limiter = new FrameLimiter(game, 60f, fpsSampleCount: 30);
                if (limiter.FpsSampleCount != 30) return $"expected FpsSampleCount == 30, got {limiter.FpsSampleCount}";
                if (limiter.AverageFps != 0f) return $"expected AverageFps == 0 before any BeginFrame, got {limiter.AverageFps:F3}";
                if (limiter.CurrentFps != 0f) return $"expected CurrentFps == 0 before any BeginFrame, got {limiter.CurrentFps:F3}";
                if (limiter.AverageFrameTimeMs != 0f) return $"expected AverageFrameTimeMs == 0 before any BeginFrame, got {limiter.AverageFrameTimeMs:F3}";
                if (limiter.CurrentFrameTimeMs != 0f) return $"expected CurrentFrameTimeMs == 0 before any BeginFrame, got {limiter.CurrentFrameTimeMs:F3}";
                return null;
            });

            results.Check("FrameLimiter: AverageFps/CurrentFps reflect real achieved framerate after running frames through BeginFrame/EndFrame", () =>
            {
                var limiter = new FrameLimiter(game, 200f); // 5ms target -- fast enough to keep the test short
                for (int i = 0; i < 30; i++)
                {
                    limiter.BeginFrame();
                    limiter.EndFrame();
                }
                // One more BeginFrame to feed the final EndFrame-paced gap into the counter.
                limiter.BeginFrame();

                if (limiter.AverageFps < 100f || limiter.AverageFps > 260f)
                    return $"expected AverageFps roughly near the 200 target, got {limiter.AverageFps:F1}";
                if (limiter.CurrentFps < 100f || limiter.CurrentFps > 260f)
                    return $"expected CurrentFps roughly near the 200 target, got {limiter.CurrentFps:F1}";
                if (limiter.AverageFrameTimeMs < 3.0f || limiter.AverageFrameTimeMs > 10.0f)
                    return $"expected AverageFrameTimeMs roughly near 5ms, got {limiter.AverageFrameTimeMs:F3}";
                return null;
            });

            results.Check("FrameLimiter: FPS/frame-time readouts use the raw unclamped frame time, not MaxFrameTime's clamp", () =>
            {
                var limiter = new FrameLimiter(game, 60f, maxFrameTime: 0.05f); // 50ms cap
                limiter.BeginFrame();
                System.Threading.Thread.Sleep(150); // a real ~150ms spike, well past the 50ms cap

                float frameTime = limiter.BeginFrame(); // clamped return value
                if (frameTime > 0.06f) return $"expected the return value clamped to roughly 0.05s, got {frameTime:F4}s";

                // CurrentFrameTimeMs should show the real ~150ms spike, not the 50ms the caller sees from FrameTime.
                if (limiter.CurrentFrameTimeMs < 100f)
                    return $"expected CurrentFrameTimeMs to reflect the real ~150ms spike unclamped, got {limiter.CurrentFrameTimeMs:F1}ms -- FPS counter should not be affected by MaxFrameTime";
                return null;
            });
        }
    }
}
