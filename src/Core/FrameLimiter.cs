using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Xna.Framework;

namespace MonoPrimitives
{
    /// <summary>Paces the game loop to a target framerate more precisely than <see cref="Game.IsFixedTimeStep"/>. Call <see cref="BeginFrame"/> at the top of a frame and <see cref="EndFrame"/> at the end.</summary>
    /// <remarks>Disables <see cref="Game.IsFixedTimeStep"/> and vsync at construction — construct this after your <see cref="GraphicsDeviceManager"/>, or there's no manager yet to disable vsync on.</remarks>
    public sealed class FrameLimiter
    {
        // Reserved for a precise busy-spin instead of Thread.Sleep, since a single Sleep call can run
        // nearly a full extra frame long on Windows due to OS scheduling jitter (~1-5% of frames,
        // measured directly) regardless of how the wait is split. 2ms is a reasonable middle ground
        // between that overshoot risk and spinning a full core for longer every frame.
        private const double SpinMarginMs = 2.0;

        private readonly Stopwatch _stopwatch = new Stopwatch();

        /// <summary>Target frames per second. Editable at any time — takes effect on the next <see cref="EndFrame"/>.</summary>
        public float TargetFps { get; set; }

        /// <summary>Time elapsed since the current frame's <see cref="BeginFrame"/> — read mid-frame (e.g. for a debug overlay) without waiting for <see cref="EndFrame"/>.</summary>
        /// <remarks>Read-only on purpose: the internal <see cref="Stopwatch"/> is otherwise unreachable, so nothing outside this class can <c>Stop()</c>/<c>Reset()</c> it and break <see cref="EndFrame"/>'s own pacing.</remarks>
        public TimeSpan Elapsed => _stopwatch.Elapsed;

        /// <summary>Disables <paramref name="game"/>'s <see cref="Game.IsFixedTimeStep"/> and vsync (if a <see cref="GraphicsDeviceManager"/> is already registered on it) and starts targeting <paramref name="targetFps"/>.</summary>
        public FrameLimiter(Game game, float targetFps = 60f)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            if (targetFps <= 0f) throw new ArgumentOutOfRangeException(nameof(targetFps), "targetFps must be positive.");

            TargetFps = targetFps;
            game.IsFixedTimeStep = false;

            if (game.Services.GetService(typeof(IGraphicsDeviceManager)) is GraphicsDeviceManager gdm)
            {
                gdm.SynchronizeWithVerticalRetrace = false;
                gdm.ApplyChanges();
            }
        }

        /// <summary>Marks the start of a frame. Call once, before doing any of the frame's own work.</summary>
        public void BeginFrame() => _stopwatch.Restart();

        /// <summary>
        /// Blocks until <see cref="TargetFps"/>'s worth of time has passed since the matching
        /// <see cref="BeginFrame"/>. Returns immediately (no sleep, no spin) if the frame's own work
        /// already took longer than the target frame time.
        /// </summary>
        public void EndFrame()
        {
            double targetMs = 1000.0 / TargetFps;
            double sleepUntilMs = targetMs - SpinMarginMs;

            if (sleepUntilMs > 0 && _stopwatch.Elapsed.TotalMilliseconds < sleepUntilMs)
            {
                int sleepMs = (int)(sleepUntilMs - _stopwatch.Elapsed.TotalMilliseconds);
                if (sleepMs > 0) Thread.Sleep(sleepMs);
            }

            while (_stopwatch.Elapsed.TotalMilliseconds < targetMs) { }
        }
    }
}
