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
            results.Check("FrameLimiter: constructor rejects a null Game or a non-positive target FPS", () =>
            {
                try { _ = new FrameLimiter(null, 60f); return "expected ArgumentNullException for a null Game"; }
                catch (ArgumentNullException) { }

                try { _ = new FrameLimiter(game, 0f); return "expected ArgumentOutOfRangeException for targetFps <= 0"; }
                catch (ArgumentOutOfRangeException) { }
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
        }
    }
}
