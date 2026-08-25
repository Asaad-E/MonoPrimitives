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
        private readonly FpsCounter _fpsCounter;
        private bool _hasBegun;
        private float _targetFps;
        private double _targetFrameTimeMs;

        /// <summary>Target frames per second. Editable at any time — takes effect on the next <see cref="EndFrame"/>.</summary>
        public float TargetFps
        {
            get => _targetFps;
            set
            {
                _targetFps = value;
                _targetFrameTimeMs = 1000.0 / value; // cached here so EndFrame doesn't redo this division every frame
            }
        }

        /// <summary>
        /// Upper bound <see cref="BeginFrame"/> clamps <see cref="FrameTime"/> to, in seconds.
        /// Editable at any time. <c>0</c> (default) disables clamping — a frame that actually took
        /// longer (e.g. after a breakpoint, a GC pause, or an asset load) would otherwise report
        /// that full duration, which can make a per-frame simulation take one huge step.
        /// </summary>
        public float MaxFrameTime { get; set; }

        /// <summary>
        /// The value <see cref="BeginFrame"/> most recently returned: real time since the previous
        /// <see cref="BeginFrame"/> call, in seconds, clamped by <see cref="MaxFrameTime"/>.
        /// <c>0</c> before the first call.
        /// </summary>
        public float FrameTime { get; private set; }

        /// <summary>Time elapsed since the current frame's <see cref="BeginFrame"/> — read mid-frame (e.g. for a debug overlay) without waiting for <see cref="EndFrame"/>.</summary>
        /// <remarks>Read-only on purpose: the internal <see cref="Stopwatch"/> is otherwise unreachable, so nothing outside this class can <c>Stop()</c>/<c>Reset()</c> it and break <see cref="EndFrame"/>'s own pacing.</remarks>
        public TimeSpan Elapsed => _stopwatch.Elapsed;

        /// <summary>Average FPS over the last <see cref="FpsSampleCount"/> real frame times (unaffected by <see cref="MaxFrameTime"/>'s clamp) — see <see cref="FpsCounter.AverageFps"/>. <c>0</c> before the first <see cref="BeginFrame"/> call.</summary>
        public float AverageFps => _fpsCounter.AverageFps;

        /// <summary>FPS implied by the single most recent real frame alone — see <see cref="FpsCounter.CurrentFps"/>.</summary>
        public float CurrentFps => _fpsCounter.CurrentFps;

        /// <summary>Average frame time in milliseconds over the last <see cref="FpsSampleCount"/> real frames (unaffected by <see cref="MaxFrameTime"/>'s clamp) — see <see cref="FpsCounter.AverageFrameTimeMs"/>.</summary>
        public float AverageFrameTimeMs => _fpsCounter.AverageFrameTimeMs;

        /// <summary>Frame time in milliseconds for the single most recent real frame alone — see <see cref="FpsCounter.CurrentFrameTimeMs"/>.</summary>
        public float CurrentFrameTimeMs => _fpsCounter.CurrentFrameTimeMs;

        /// <summary>Window size backing <see cref="AverageFps"/>/<see cref="AverageFrameTimeMs"/> — set at construction, fixed for this instance's lifetime.</summary>
        public int FpsSampleCount => _fpsCounter.SampleCount;

        /// <summary>Disables <paramref name="game"/>'s <see cref="Game.IsFixedTimeStep"/> and vsync (if a <see cref="GraphicsDeviceManager"/> is already registered on it) and starts targeting <paramref name="targetFps"/>.</summary>
        public FrameLimiter(Game game, float targetFps = 60f, float maxFrameTime = 0f, int fpsSampleCount = 60)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            if (targetFps <= 0f) throw new ArgumentOutOfRangeException(nameof(targetFps), "targetFps must be positive.");
            if (maxFrameTime < 0f) throw new ArgumentOutOfRangeException(nameof(maxFrameTime), "maxFrameTime must not be negative.");
            if (fpsSampleCount <= 0) throw new ArgumentOutOfRangeException(nameof(fpsSampleCount), "fpsSampleCount must be positive.");

            TargetFps = targetFps;
            MaxFrameTime = maxFrameTime;
            _fpsCounter = new FpsCounter(fpsSampleCount);
            game.IsFixedTimeStep = false;

            if (game.Services.GetService(typeof(IGraphicsDeviceManager)) is GraphicsDeviceManager gdm)
            {
                gdm.SynchronizeWithVerticalRetrace = false;
                gdm.ApplyChanges();
            }
        }

        /// <summary>Marks the start of a frame — call once, before doing any of the frame's own work. Returns the real time since the previous call, in seconds (<c>0</c> on the first call).</summary>
        /// <remarks>Clamped to <see cref="MaxFrameTime"/> if that's exceeded; <see cref="AverageFps"/>/<see cref="CurrentFps"/> are fed the raw, unclamped value instead, so they still reflect a real stall <see cref="MaxFrameTime"/> hides from <see cref="FrameTime"/>.</remarks>
        public float BeginFrame()
        {
            float rawFrameTime = (float)_stopwatch.Elapsed.TotalSeconds;

            // On the first call there's no real previous frame to measure -- feed the FPS counter
            // an expected-duration sample (1/TargetFps) instead of a phantom 0, which would
            // otherwise inflate AverageFps (= filled/sum) for the next FpsSampleCount frames.
            _fpsCounter.Update(_hasBegun ? rawFrameTime : 1f / TargetFps);
            _hasBegun = true;

            float frameTime = MaxFrameTime > 0f && rawFrameTime > MaxFrameTime ? MaxFrameTime : rawFrameTime;
            FrameTime = frameTime;

            _stopwatch.Restart();
            return frameTime;
        }

        /// <summary>
        /// Blocks until <see cref="TargetFps"/>'s worth of time has passed since the matching
        /// <see cref="BeginFrame"/>. Returns immediately (no sleep, no spin) if the frame's own work
        /// already took longer than the target frame time.
        /// </summary>
        public void EndFrame()
        {
            double targetMs = _targetFrameTimeMs;
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
