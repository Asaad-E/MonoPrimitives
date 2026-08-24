using MonoPrimitives;

namespace MonoPrimitives.Tests
{
    /// <summary>Pure-math checks for <see cref="FpsCounter"/> — no GraphicsDevice needed.</summary>
    internal static class FpsCounterTests
    {
        public static void Run(TestResults results)
        {
            results.Check("FpsCounter: 0 before any Update", () =>
            {
                var fps = new FpsCounter(10);
                if (fps.AverageFps != 0f) return $"expected 0, got {fps.AverageFps}";
                if (fps.CurrentFps != 0f) return $"expected 0, got {fps.CurrentFps}";
                if (fps.AverageFrameTimeMs != 0f) return $"expected 0, got {fps.AverageFrameTimeMs}";
                if (fps.CurrentFrameTimeMs != 0f) return $"expected 0, got {fps.CurrentFrameTimeMs}";
                return null;
            });

            results.Check("FpsCounter: constant 1/60s frame times average to ~60 FPS (~16.67 ms)", () =>
            {
                var fps = new FpsCounter(60);
                for (int i = 0; i < 60; i++) fps.Update(1f / 60f);
                if (System.MathF.Abs(fps.AverageFps - 60f) > 0.01f) return $"expected ~60, got {fps.AverageFps}";
                if (System.MathF.Abs(fps.CurrentFps - 60f) > 0.01f) return $"expected ~60, got {fps.CurrentFps}";
                if (System.MathF.Abs(fps.AverageFrameTimeMs - 16.667f) > 0.01f) return $"expected ~16.67 ms, got {fps.AverageFrameTimeMs}";
                if (System.MathF.Abs(fps.CurrentFrameTimeMs - 16.667f) > 0.01f) return $"expected ~16.67 ms, got {fps.CurrentFrameTimeMs}";
                return null;
            });

            results.Check("FpsCounter: partially-filled window averages only the recorded samples", () =>
            {
                var fps = new FpsCounter(100);
                for (int i = 0; i < 10; i++) fps.Update(1f / 30f); // only 10 of 100 slots filled
                if (System.MathF.Abs(fps.AverageFps - 30f) > 0.01f) return $"expected ~30 (not diluted by empty slots), got {fps.AverageFps}";
                return null;
            });

            results.Check("FpsCounter: old samples are evicted once the window is full (rolling, not cumulative)", () =>
            {
                var fps = new FpsCounter(10);
                for (int i = 0; i < 10; i++) fps.Update(1f / 30f); // fill the window at 30 FPS
                for (int i = 0; i < 10; i++) fps.Update(1f / 60f); // then fully overwrite it at 60 FPS
                if (System.MathF.Abs(fps.AverageFps - 60f) > 0.01f) return $"expected the 30 FPS samples fully evicted (~60), got {fps.AverageFps}";
                return null;
            });

            results.Check("FpsCounter.CurrentFps reflects only the single most recent sample", () =>
            {
                var fps = new FpsCounter(10);
                for (int i = 0; i < 5; i++) fps.Update(1f / 30f);
                fps.Update(1f / 120f); // one fast frame
                if (System.MathF.Abs(fps.CurrentFps - 120f) > 0.01f) return $"expected CurrentFps ~120 from the last sample alone, got {fps.CurrentFps}";
                if (System.MathF.Abs(fps.AverageFps - 120f) < 0.01f) return "AverageFps should still be pulled down by the earlier slower samples, not equal CurrentFps";
                return null;
            });

            results.Check("FpsCounter: a zero-length frame doesn't produce Infinity/NaN", () =>
            {
                var fps = new FpsCounter(5);
                fps.Update(0f);
                if (float.IsNaN(fps.AverageFps) || float.IsInfinity(fps.AverageFps)) return $"AverageFps is {fps.AverageFps}";
                if (float.IsNaN(fps.CurrentFps) || float.IsInfinity(fps.CurrentFps)) return $"CurrentFps is {fps.CurrentFps}";
                if (fps.AverageFrameTimeMs != 0f) return $"AverageFrameTimeMs is {fps.AverageFrameTimeMs}";
                if (fps.CurrentFrameTimeMs != 0f) return $"CurrentFrameTimeMs is {fps.CurrentFrameTimeMs}";
                return null;
            });

            results.Check("FpsCounter.CurrentFrameTimeMs reflects only the single most recent sample", () =>
            {
                var fps = new FpsCounter(10);
                for (int i = 0; i < 5; i++) fps.Update(1f / 30f); // ~33.3 ms each
                fps.Update(1f / 120f); // one fast, ~8.3 ms frame
                if (System.MathF.Abs(fps.CurrentFrameTimeMs - 8.333f) > 0.01f) return $"expected CurrentFrameTimeMs ~8.33 from the last sample alone, got {fps.CurrentFrameTimeMs}";
                if (System.MathF.Abs(fps.AverageFrameTimeMs - 8.333f) < 0.01f) return "AverageFrameTimeMs should still be pulled up by the earlier slower samples, not equal CurrentFrameTimeMs";
                return null;
            });
        }
    }
}
