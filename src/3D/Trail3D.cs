#nullable enable

using System;
using Microsoft.Xna.Framework;

namespace MonoPrimitives.Primitives3D
{
    /// <summary>A fixed-capacity history of recent positions, drawn as a line that fades out toward the oldest point — a moving particle/agent's own trail.</summary>
    /// <remarks>Call <see cref="Add"/> once per frame with the thing's current position, then <see cref="Draw"/> it. <see cref="Add"/> never allocates once warmed up.</remarks>
    public sealed class Trail3D
    {
        private readonly Vector3[] _points;
        // Reused every Draw() call, sized once -- no per-frame allocation. _scratchPoints exists
        // because DrawLineStrip3D needs a CONTIGUOUS oldest-to-newest span, but _points is a ring
        // buffer (wraps around _newestIndex), so the logical order isn't contiguous in general.
        private readonly Vector3[] _scratchPoints;
        private readonly Color[] _scratchSegmentColors;
        private int _newestIndex = -1;

        /// <summary>Maximum number of points this trail holds — its "length" in frames of history, not world units.</summary>
        public int Capacity => _points.Length;

        /// <summary>How many points are actually recorded so far (grows to <see cref="Capacity"/>, then stays there).</summary>
        public int Count { get; private set; }

        /// <summary>Creates an empty trail holding up to <paramref name="capacity"/> points.</summary>
        public Trail3D(int capacity)
        {
            if (capacity < 2)
                throw new ArgumentOutOfRangeException(nameof(capacity), "A trail needs at least 2 points to draw a line.");
            _points = new Vector3[capacity];
            _scratchPoints = new Vector3[capacity];
            _scratchSegmentColors = new Color[capacity - 1];
        }

        /// <summary>Appends the current position, evicting the oldest point once <see cref="Capacity"/> is reached.</summary>
        public void Add(Vector3 position)
        {
            _newestIndex = (_newestIndex + 1) % _points.Length;
            _points[_newestIndex] = position;
            if (Count < _points.Length) Count++;
        }

        /// <summary>Drops every recorded point — call when the thing being tracked teleports, so the trail doesn't draw a line across the jump.</summary>
        public void Clear() => Count = 0;

        /// <summary>Position at <paramref name="indexFromOldest"/> (0 = oldest recorded point, <see cref="Count"/>-1 = newest).</summary>
        public Vector3 this[int indexFromOldest]
        {
            get
            {
                if (indexFromOldest < 0 || indexFromOldest >= Count)
                    throw new ArgumentOutOfRangeException(nameof(indexFromOldest));
                int oldestIndex = (_newestIndex - Count + 1 + _points.Length) % _points.Length;
                return _points[(oldestIndex + indexFromOldest) % _points.Length];
            }
        }

        /// <summary>Draws the trail as one joined camera-facing strip (<see cref="Primitive3DBatch.DrawLineStrip3D(ReadOnlySpan{Vector3},ReadOnlySpan{Color},float)"/>), fading from <paramref name="color"/> at the newest point down to <paramref name="color"/> scaled by <paramref name="fadeToAlpha"/> at the oldest — the default (0) fades all the way to invisible.</summary>
        /// <remarks>Shares one offset direction between adjacent segments at each interior point, so a sharp bend in the trail no longer shows the gap/overlap a naive per-segment draw would.</remarks>
        /// <param name="batch">Batch to draw into; must already be between <c>Begin</c>/<c>End</c>.</param>
        /// <param name="color">Color at the newest point, before any fade is applied.</param>
        /// <param name="thickness">Line thickness; &lt;= 0 (the default) uses <see cref="Primitive3DBatch.DefaultLineThickness"/> — the same sentinel convention as the rest of this library's <c>Border*</c>/<c>Draw*</c> methods.</param>
        /// <param name="fadeToAlpha">Alpha scale applied to <paramref name="color"/> at the oldest point, 0 (default) fading fully to invisible.</param>
        public void Draw(Primitive3DBatch batch, Color color, float thickness = -1f, float fadeToAlpha = 0f)
        {
            if (Count < 2) return;
            fadeToAlpha = Math.Clamp(fadeToAlpha, 0f, 1f);

            for (int i = 0; i < Count; i++) _scratchPoints[i] = this[i];

            float denom = Count - 1;
            for (int i = 1; i < Count; i++)
            {
                float t = (i - 0.5f) / denom; // segment midpoint's position along the trail, 0=oldest..1=newest
                float alphaFrac = fadeToAlpha + (1f - fadeToAlpha) * t;
                _scratchSegmentColors[i - 1] = new Color(color.R, color.G, color.B, (byte)(color.A * alphaFrac));
            }

            batch.DrawLineStrip3D(_scratchPoints.AsSpan(0, Count), _scratchSegmentColors.AsSpan(0, Count - 1), thickness);
        }
    }
}
