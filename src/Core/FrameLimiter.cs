using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Xna.Framework;

namespace MonoPrimitives
{
    /// <summary>
    /// Paces the game loop to a target framerate more precisely than <see cref="Game.IsFixedTimeStep"/>'s
    /// own timer, by sleeping out most of the remaining frame time and busy-spinning the last couple
    /// of milliseconds for precision. Call <see cref="BeginFrame"/> once at the top of a frame
    /// (typically the first line of <c>Update</c>) and <see cref="EndFrame"/> once at the very end
    /// of it (typically the last line of <c>Draw</c>).
    /// </summary>
    /// <remarks>
    /// Disables <see cref="Game.IsFixedTimeStep"/> and vsync at construction — both would otherwise
    /// pace the loop independently and fight this class. Construct this AFTER your
    /// <see cref="GraphicsDeviceManager"/> — it disables vsync by reading the manager already
    /// registered as a service, and does nothing if none is found yet.
    ///
    /// A rare (~1-5% of frames) OS-level scheduling jitter can make any single call to
    /// <see cref="Thread.Sleep(int)"/> run nearly a full extra frame long on Windows, regardless of
    /// how the remaining time is split between sleeping and spinning — measured directly, not fixed
    /// by a larger spin margin or an alternative tail strategy (<see cref="Thread.Yield"/>,
    /// <see cref="Thread.SpinWait"/>). If consistently smooth frame times matter more than idle CPU
    /// usage, that's an inherent tradeoff of any Sleep-based limiter, not a bug here.
    /// </remarks>
    public sealed class FrameLimiter
    {
        // How much of the wait is left to a precise busy-spin instead of Thread.Sleep. Empirically,
        // widening this reduces the worst-case overshoot when a Sleep call runs long, at the cost of
        // spinning (100% of one core) for longer every frame -- 2ms is a reasonable middle ground.
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
