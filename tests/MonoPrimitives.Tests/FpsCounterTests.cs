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
                return null;
            });

            results.Check("FpsCounter: constant 1/60s frame times average to ~60 FPS", () =>
            {
                var fps = new FpsCounter(60);
                for (int i = 0; i < 60; i++) fps.Update(1f / 60f);
                if (System.MathF.Abs(fps.AverageFps - 60f) > 0.01f) return $"expected ~60, got {fps.AverageFps}";
                if (System.MathF.Abs(fps.CurrentFps - 60f) > 0.01f) return $"expected ~60, got {fps.CurrentFps}";
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
                return null;
            });
        }
    }
}
