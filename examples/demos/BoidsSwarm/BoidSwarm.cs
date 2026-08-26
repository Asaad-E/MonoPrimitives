using System;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using MonoPrimitives;

namespace BoidsSwarm;

/// <summary>
/// The flocking simulation itself: double-buffered boid state (a full read-only snapshot the
/// parallel step reads from, and a write-only array it fills in -- swapped once the step
/// finishes, so no two threads ever touch the same element), a counting-sort spatial hash for
/// neighbor queries instead of an O(boids^2) scan, and a small Perlin-noise flow field the whole
/// flock drifts along. Every tunable behavior parameter is a plain mutable property so a UI
/// (ImGui, here) can retune the flock live without restarting.
/// </summary>
internal sealed class BoidSwarm
{
    public struct Boid
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public Color Color;
    }

    private static readonly Color[] FlockColors =
    {
        Palette.Turquoise, Palette.PeterRiver, Palette.Amethyst, Palette.Emerald, Palette.Sunflower,
    };

    // ---- Live-tunable behavior parameters ----------------------------------

    private float _perceptionRadius = 24f;

    /// <summary>How far a boid looks for neighbors. Changing this rebuilds the spatial hash's cell size (see <see cref="RebuildGrid"/>) -- cheap, since that's sized by cell count, not boid count.</summary>
    public float PerceptionRadius
    {
        get => _perceptionRadius;
        set
        {
            value = MathF.Max(1f, value);
            if (value == _perceptionRadius) return;
            _perceptionRadius = value;
            RebuildGrid();
        }
    }

    public float SeparationRadius { get; set; } = 10f;
    public float MaxSpeed { get; set; } = 140f;
    public float MaxForce { get; set; } = 260f;
    public float SeparationWeight { get; set; } = 1.7f;
    public float AlignmentWeight { get; set; } = 1.0f;
    public float CohesionWeight { get; set; } = 0.8f;
    public float FlowFieldWeight { get; set; } = 0.6f;

    /// <summary>
    /// When on, times <see cref="Update"/>'s three phases with <see cref="DebugTimer"/> and prints
    /// them to <see cref="Console"/> every frame. Off by default -- checked as a plain branch
    /// before ever touching <see cref="DebugTimer"/>/<see cref="Console"/>, so leaving it off costs
    /// nothing (no timer construction, no I/O) rather than just suppressing the printed output.
    /// </summary>
    public bool ProfilingEnabled { get; set; }

    public readonly float WorldWidth;
    public readonly float WorldHeight;

    private Boid[] _current;
    private Boid[] _next;
    public int Count { get; private set; }

    // ---- Spatial hash: a counting-sort bucket grid -------------------------
    // Rebuilt fresh every frame from _current (O(boids + cells), sequential -- cheap next to the
    // parallel step below). For any cell, _sortedIndices[_cellStarts[cell].._cellStarts[cell+1])
    // holds that cell's boid indices; no List<int>[]/Dictionary, so no per-cell allocation.
    private int _gridCols;
    private int _gridRows;
    private float _cellSize;
    private int[] _cellStarts = Array.Empty<int>();   // length gridCols*gridRows + 1
    private int[] _cellCursor = Array.Empty<int>();   // scratch, length gridCols*gridRows
    private int[] _sortedIndices = Array.Empty<int>(); // length == Count, cell order -> original index
    private Boid[] _sortedBoids = Array.Empty<Boid>(); // length == Count, boid DATA in that same cell order

    // ---- Flow field: a coarse grid of Perlin-noise directions, resampled every frame ----
    private const int FlowCols = 64;
    private const int FlowRows = 36;
    private const float FlowNoiseScale = 0.0025f;
    private const float FlowTimeScale = 0.06f;
    private const float FlowAngleScale = MathF.PI * 4f;
    private readonly Vector2[] _flowField = new Vector2[FlowCols * FlowRows];
    private readonly Noise _noise;
    private float _flowTime;

    public ReadOnlySpan<Vector2> FlowField => _flowField;
    public int FlowFieldCols => FlowCols;
    public int FlowFieldRows => FlowRows;

    private readonly Random _rng;

    public BoidSwarm(int worldWidth, int worldHeight, int initialCount, int seed = 12345)
    {
        WorldWidth = worldWidth;
        WorldHeight = worldHeight;
        _rng = new Random(seed);
        _noise = new Noise(seed);
        _current = Array.Empty<Boid>();
        _next = Array.Empty<Boid>();

        RebuildGrid();
        SetCount(initialCount);
    }

    /// <summary>Resizes the simulation to <paramref name="newCount"/> boids, keeping existing ones and spawning fresh ones at random for a growth, or truncating for a shrink.</summary>
    public void SetCount(int newCount)
    {
        newCount = Math.Max(1, newCount);
        if (newCount == Count) return;

        var newBoids = new Boid[newCount];
        int keep = Math.Min(newCount, Count);
        Array.Copy(_current, newBoids, keep);
        for (int i = keep; i < newCount; i++) newBoids[i] = SpawnRandom();

        _current = newBoids;
        _next = new Boid[newCount];
        _sortedIndices = new int[newCount];
        Count = newCount;
    }

    /// <summary>Respawns every boid at a random position/heading/color -- same as a fresh construction, without reallocating.</summary>
    public void Reseed()
    {
        for (int i = 0; i < Count; i++) _current[i] = SpawnRandom();
    }

    private Boid SpawnRandom() => new()
    {
        Position = new Vector2((float)_rng.NextDouble() * WorldWidth, (float)_rng.NextDouble() * WorldHeight),
        Velocity = RandomDirection() * (MaxSpeed * 0.5f),
        Color = FlockColors[_rng.Next(FlockColors.Length)],
    };

    private Vector2 RandomDirection()
    {
        float a = (float)(_rng.NextDouble() * MathHelper.TwoPi);
        return new Vector2(MathF.Cos(a), MathF.Sin(a));
    }

    /// <summary>Advances the whole flock by <paramref name="dt"/> seconds: rebuilds the flow field and spatial hash (sequential), steps every boid in parallel across all cores, then swaps the double buffer.</summary>
    public void Update(float dt)
    {
        if (ProfilingEnabled)
        {
            using (new DebugTimer("Flow field", separator: true)) BuildFlowField(dt);
            using (new DebugTimer("Spatial hash")) BuildSpatialHash();
        }
        else
        {
            BuildFlowField(dt);
            BuildSpatialHash();
        }

        Boid[] current = _current, next = _next;
        if (ProfilingEnabled)
        {
            using (new DebugTimer($"Parallel step ({Count:N0} boids)")) Parallel.For(0, Count, i => StepBoid(i, current, next, dt));
        }
        else
        {
            Parallel.For(0, Count, i => StepBoid(i, current, next, dt));
        }

        (_current, _next) = (_next, _current);
    }

    public ReadOnlySpan<Boid> Boids => _current;

    // ---- Flow field ---------------------------------------------------------

    private void BuildFlowField(float dt)
    {
        _flowTime += dt * FlowTimeScale;
        for (int y = 0; y < FlowRows; y++)
        {
            for (int x = 0; x < FlowCols; x++)
            {
                float wx = (x + 0.5f) / FlowCols * WorldWidth;
                float wy = (y + 0.5f) / FlowRows * WorldHeight;
                float n = _noise.Sample3D(wx * FlowNoiseScale, wy * FlowNoiseScale, _flowTime);
                float angle = n * FlowAngleScale;
                _flowField[y * FlowCols + x] = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            }
        }
    }

    private Vector2 SampleFlowField(Vector2 pos)
    {
        int cx = Math.Clamp((int)(pos.X / WorldWidth * FlowCols), 0, FlowCols - 1);
        int cy = Math.Clamp((int)(pos.Y / WorldHeight * FlowRows), 0, FlowRows - 1);
        return _flowField[cy * FlowCols + cx];
    }

    // ---- Spatial hash: counting sort ----------------------------------------

    private void RebuildGrid()
    {
        _cellSize = _perceptionRadius;
        _gridCols = Math.Max(1, (int)MathF.Ceiling(WorldWidth / _cellSize));
        _gridRows = Math.Max(1, (int)MathF.Ceiling(WorldHeight / _cellSize));
        _cellStarts = new int[_gridCols * _gridRows + 1];
        _cellCursor = new int[_gridCols * _gridRows];
    }

    private int CellIndex(Vector2 pos)
    {
        int cx = (int)(pos.X / _cellSize);
        int cy = (int)(pos.Y / _cellSize);
        // Positions are wrapped into [0, World*) at the end of every step, but clamp defensively
        // so a boundary float rounding error can't index outside the grid.
        if (cx >= _gridCols) cx = _gridCols - 1; else if (cx < 0) cx = 0;
        if (cy >= _gridRows) cy = _gridRows - 1; else if (cy < 0) cy = 0;
        return cy * _gridCols + cx;
    }

    private void BuildSpatialHash()
    {
        Array.Clear(_cellStarts, 0, _cellStarts.Length);

        // Pass 1: count boids per cell, offset by one slot (into cell+1) so the prefix sum below
        // turns this directly into start offsets without a separate counts array.
        for (int i = 0; i < Count; i++)
            _cellStarts[CellIndex(_current[i].Position) + 1]++;

        // Pass 2: prefix sum -- _cellStarts[c] becomes where cell c's boids begin in _sortedIndices.
        for (int c = 0; c < _gridCols * _gridRows; c++)
            _cellStarts[c + 1] += _cellStarts[c];

        // Pass 3: scatter each boid's index into its cell's slice, _cellCursor tracking each
        // cell's next free write position (starts at that cell's own _cellStarts).
        Array.Copy(_cellStarts, _cellCursor, _cellCursor.Length);
        for (int i = 0; i < Count; i++)
        {
            int cell = CellIndex(_current[i].Position);
            _sortedIndices[_cellCursor[cell]++] = i;
        }

        // Pass 4: gather the actual boid DATA into that same cell order, not just the indices.
        // _sortedIndices alone already lets a neighbor loop walk one cell's boids as a contiguous
        // *index* range, but current[_sortedIndices[k]] still jumps to a scattered, unrelated
        // Boid in memory for each one -- two boids in the same cell (the entire point of the
        // grid) end up nowhere near each other in current[]. _sortedBoids fixes that: boids in
        // the same cell are also contiguous in memory, so the neighbor loop's actual per-boid
        // reads (the far larger, more expensive kind next to a lone int) become sequential,
        // cache-friendly access instead of scattered lookups -- the standard "coherent grid"
        // technique. Measured with DebugTimer at 50k boids: cut the parallel step from ~33ms to
        // ~28ms -- a real win, though the neighbor loop's actual math (not memory access) turned
        // out to be the bigger remaining cost at this density (see DECISIONS.md).
        if (_sortedBoids.Length != Count) _sortedBoids = new Boid[Count];
        for (int k = 0; k < Count; k++)
            _sortedBoids[k] = _current[_sortedIndices[k]];
    }

    // ---- Per-boid step: runs on the thread pool, one call per boid ---------
    // Reads only current/next's own slot plus the read-only grid/flow-field state built above --
    // no two calls ever touch the same array element, so this needs no locking at all.
    //
    // Update() passes current/next/dt into a capturing lambda here, which does allocate a small
    // closure every call -- measured (DebugTimer, 5 runs x 10s each, both at 50k boids and at 200
    // boids/144Hz to make a fixed per-call cost as visible as possible) and found no detectable
    // difference against a cached, non-allocating Action<int> -- Parallel.For's own per-call
    // overhead (task scheduling across threads) already dwarfs one small allocation at any boid
    // count. Kept as the plain, explicit-parameter version since it reads clearly and the
    // "optimization" bought nothing (see DECISIONS.md).

    private void StepBoid(int index, Boid[] current, Boid[] next, float dt)
    {
        Boid self = current[index];
        Vector2 separation = Vector2.Zero, alignment = Vector2.Zero, cohesion = Vector2.Zero;
        int neighbors = 0;

        int cx = (int)(self.Position.X / _cellSize);
        int cy = (int)(self.Position.Y / _cellSize);
        float sepRadiusSq = SeparationRadius * SeparationRadius;
        float perceptionRadiusSq = _perceptionRadius * _perceptionRadius;

        for (int oy = -1; oy <= 1; oy++)
        {
            int ny = WrapIndex(cy + oy, _gridRows);
            for (int ox = -1; ox <= 1; ox++)
            {
                int nx = WrapIndex(cx + ox, _gridCols);
                int cell = ny * _gridCols + nx;
                int start = _cellStarts[cell], end = _cellStarts[cell + 1];
                for (int k = start; k < end; k++)
                {
                    if (_sortedIndices[k] == index) continue;
                    Boid other = _sortedBoids[k]; // contiguous per cell -- see BuildSpatialHash's pass 4

                    Vector2 toOther = ToroidalDelta(self.Position, other.Position, WorldWidth, WorldHeight);
                    float distSq = toOther.LengthSquared();
                    if (distSq > perceptionRadiusSq || distSq < 1e-6f) continue;

                    if (distSq < sepRadiusSq)
                    {
                        float dist = MathF.Sqrt(distSq);
                        float invDist = 1f / dist; // one division instead of two (Vector2/float divides each component)
                        separation -= toOther * invDist * (SeparationRadius - dist);
                    }

                    alignment += other.Velocity;
                    cohesion += toOther;
                    neighbors++;
                }
            }
        }

        Vector2 steer = separation * SeparationWeight;
        if (neighbors > 0)
        {
            alignment /= neighbors;
            steer += Limit(alignment - self.Velocity, MaxForce) * AlignmentWeight;

            cohesion /= neighbors;
            steer += Limit(cohesion, MaxForce) * CohesionWeight;
        }

        steer += SampleFlowField(self.Position) * (MaxForce * FlowFieldWeight);

        self.Velocity = Limit(self.Velocity + steer * dt, MaxSpeed);
        self.Position = Wrap(self.Position + self.Velocity * dt, WorldWidth, WorldHeight);
        next[index] = self;
    }

    private static int WrapIndex(int v, int max) => ((v % max) + max) % max;

    // Shortest vector from `from` to `to` across the toroidal wrap -- e.g. from x=1910 to x=5 is
    // +15 (through the seam), not -1905 (the long way around a plain subtraction would give).
    private static Vector2 ToroidalDelta(Vector2 from, Vector2 to, float width, float height)
        => new(WrapDelta(to.X - from.X, width), WrapDelta(to.Y - from.Y, height));

    private static float WrapDelta(float delta, float span)
    {
        if (delta > span * 0.5f) delta -= span;
        else if (delta < -span * 0.5f) delta += span;
        return delta;
    }

    private static Vector2 Limit(Vector2 v, float max)
    {
        float lenSq = v.LengthSquared();
        return lenSq > max * max ? v * (max / MathF.Sqrt(lenSq)) : v;
    }

    private static Vector2 Wrap(Vector2 p, float width, float height) => new(
        ((p.X % width) + width) % width,
        ((p.Y % height) + height) % height);
}
