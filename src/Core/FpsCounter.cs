using System;
using Microsoft.Xna.Framework;

namespace MonoPrimitives
{
    /// <summary>
    /// Tracks a rolling average frames-per-second over the last <see cref="SampleCount"/> frames.
    /// Call <see cref="Update(GameTime)"/> once per frame (typically the first line of <c>Draw</c>),
    /// read <see cref="AverageFps"/> whenever. Purely a measurement — pair it with your own
    /// <c>DrawString</c>/<c>DrawString3D</c> call to actually show it; this doesn't draw anything
    /// itself, and doesn't assume a screen position, format, or color.
    /// </summary>
    public sealed class FpsCounter
    {
        private readonly float[] _frameTimes;
        private int _index;
        private int _filled;

        /// <summary>How many recent frames the average is computed over.</summary>
        public int SampleCount => _frameTimes.Length;

        /// <param name="sampleCount">Window size for the rolling average — smaller reacts faster to real framerate changes, larger reads more stable. 60 (one second at 60 FPS) is a reasonable default for an on-screen counter.</param>
        public FpsCounter(int sampleCount = 60)
        {
            if (sampleCount <= 0) throw new ArgumentOutOfRangeException(nameof(sampleCount), "sampleCount must be positive.");
            _frameTimes = new float[sampleCount];
        }

        /// <summary>Records this frame's elapsed time from <paramref name="gameTime"/>.</summary>
        public void Update(GameTime gameTime) => Update((float)gameTime.ElapsedGameTime.TotalSeconds);

        /// <summary>Records this frame's elapsed time directly, for callers not driven by a <see cref="GameTime"/>.</summary>
        public void Update(float deltaSeconds)
        {
            _frameTimes[_index] = MathF.Max(deltaSeconds, 0f);
            _index = (_index + 1) % _frameTimes.Length;
            if (_filled < _frameTimes.Length) _filled++;
        }

        /// <summary>
        /// Average FPS over the window: total frames divided by total time, not a per-frame
        /// average of instantaneous FPS values — the latter over-weights a handful of unusually
        /// fast frames instead of reflecting how long the window actually took. 0 before the
        /// first <see cref="Update(GameTime)"/> call.
        /// </summary>
        public float AverageFps
        {
            get
            {
                if (_filled == 0) return 0f;
                float sum = 0f;
                for (int i = 0; i < _filled; i++) sum += _frameTimes[i];
                return sum > 0f ? _filled / sum : 0f;
            }
        }

        /// <summary>FPS implied by the single most recent frame alone — noisier than <see cref="AverageFps"/>, useful for spotting an isolated spike/stall.</summary>
        public float CurrentFps
        {
            get
            {
                if (_filled == 0) return 0f;
                int lastIndex = (_index - 1 + _frameTimes.Length) % _frameTimes.Length;
                float dt = _frameTimes[lastIndex];
                return dt > 0f ? 1f / dt : 0f;
            }
        }

        /// <summary>
        /// Average frame time in milliseconds over the window: total time divided by total
        /// frames — the same underlying average <see cref="AverageFps"/> is built on, just
        /// read before the reciprocal instead of after. Fps compresses the low end of the
        /// scale (60→59 fps is a 0.28 ms difference, 15→14 fps is a 4.8 ms one for the same
        /// "1 fps") and expands the high end, so a fixed budget — "this frame must fit in
        /// 16.6 ms" — is easier to read and compare directly against here than by eyeballing
        /// fps deltas. 0 before the first <see cref="Update(GameTime)"/> call.
        /// </summary>
        public float AverageFrameTimeMs
        {
            get
            {
                if (_filled == 0) return 0f;
                float sum = 0f;
                for (int i = 0; i < _filled; i++) sum += _frameTimes[i];
                return sum / _filled * 1000f;
            }
        }

        /// <summary>Frame time in milliseconds for the single most recent frame alone — noisier than <see cref="AverageFrameTimeMs"/>, useful for spotting an isolated spike/stall.</summary>
        public float CurrentFrameTimeMs
        {
            get
            {
                if (_filled == 0) return 0f;
                int lastIndex = (_index - 1 + _frameTimes.Length) % _frameTimes.Length;
                return _frameTimes[lastIndex] * 1000f;
            }
        }
    }
}
