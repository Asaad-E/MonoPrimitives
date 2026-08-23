#nullable enable

using System;
using Microsoft.Xna.Framework;

namespace MonoPrimitives.Primitives2D
{
    /// <summary>
    /// A fixed-capacity history of recent positions, drawn as a line that fades out toward the
    /// oldest point — a moving particle/agent's own trail (the Pezzza's-Work-style look). Call
    /// <see cref="Add"/> once per frame with the thing's current position, then <see cref="Draw"/>
    /// it. A ring buffer under the hood: <see cref="Add"/> never allocates once warmed up, and old
    /// points are simply overwritten rather than shifted.
    /// </summary>
    public sealed class Trail2D
    {
        private readonly Vector2[] _points;
        private int _newestIndex = -1;

        /// <summary>Maximum number of points this trail holds — its "length" in frames of history, not world units.</summary>
        public int Capacity => _points.Length;

        /// <summary>How many points are actually recorded so far (grows to <see cref="Capacity"/>, then stays there).</summary>
        public int Count { get; private set; }

        /// <summary>Creates an empty trail holding up to <paramref name="capacity"/> points.</summary>
        public Trail2D(int capacity)
        {
            if (capacity < 2)
                throw new ArgumentOutOfRangeException(nameof(capacity), "A trail needs at least 2 points to draw a line.");
            _points = new Vector2[capacity];
        }

        /// <summary>Appends the current position, evicting the oldest point once <see cref="Capacity"/> is reached.</summary>
        public void Add(Vector2 position)
        {
            _newestIndex = (_newestIndex + 1) % _points.Length;
            _points[_newestIndex] = position;
            if (Count < _points.Length) Count++;
        }

        /// <summary>Drops every recorded point — call when the thing being tracked teleports, so the trail doesn't draw a line across the jump.</summary>
        public void Clear() => Count = 0;

        /// <summary>Position at <paramref name="indexFromOldest"/> (0 = oldest recorded point, <see cref="Count"/>-1 = newest).</summary>
        public Vector2 this[int indexFromOldest]
        {
            get
            {
                if (indexFromOldest < 0 || indexFromOldest >= Count)
                    throw new ArgumentOutOfRangeException(nameof(indexFromOldest));
                int oldestIndex = (_newestIndex - Count + 1 + _points.Length) % _points.Length;
                return _points[(oldestIndex + indexFromOldest) % _points.Length];
            }
        }

        /// <summary>
        /// Draws the trail as <see cref="Count"/>-1 segments, fading from <paramref name="color"/>
        /// at the newest point down to <paramref name="color"/> scaled by <paramref name="fadeToAlpha"/>
        /// at the oldest — the default (0) fades all the way to invisible. Cost is proportional to
        /// <see cref="Count"/> (one <c>DrawLine</c> call per segment, since a single-color
        /// <c>DrawLineStrip</c> can't fade along its own length) — keep <see cref="Capacity"/> no
        /// bigger than the trail actually needs to look right, especially with many trails on screen.
        /// </summary>
        public void Draw(Primitive2DBatch batch, Color color, float thickness = 2f, float fadeToAlpha = 0f)
        {
            if (Count < 2) return;
            fadeToAlpha = Math.Clamp(fadeToAlpha, 0f, 1f);
            float denom = Count - 1;
            for (int i = 1; i < Count; i++)
            {
                float t = (i - 0.5f) / denom; // segment midpoint's position along the trail, 0=oldest..1=newest
                float alphaFrac = fadeToAlpha + (1f - fadeToAlpha) * t;
                Color segColor = new(color.R, color.G, color.B, (byte)(color.A * alphaFrac));
                batch.DrawLine(this[i - 1], this[i], thickness, segColor);
            }
        }
    }
}
