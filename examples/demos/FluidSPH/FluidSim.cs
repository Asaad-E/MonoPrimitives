using System;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using MonoPrimitives;

namespace FluidSPH;

/// <summary>
/// A 2D Smoothed Particle Hydrodynamics (SPH) fluid: each particle's density comes from a
/// weighted sum of nearby particles (a smoothing kernel, not a hard cutoff), pressure comes from
/// how far that density sits above rest, and particles push apart along the pressure gradient plus
/// a viscosity term that damps relative velocity between neighbors -- gravity and container walls
/// do the rest. Kernel constants and the fixed internal timestep match the classic
/// Müller/Charypar/Gross real-time SPH formulation, not values guessed from scratch, since SPH is
/// notoriously easy to make numerically explode with an untested set of constants.
/// </summary>
internal sealed class FluidSim
{
    public struct Particle
    {
        public Vector2 Position;
        public Vector2 Velocity;
    }

    // ---- SPH constants (Müller et al. 2003, "Particle-Based Fluid Simulation for Interactive
    // Applications") -- a fixed, small internal timestep substepped as many times as needed to
    // cover a frame's real dt, since SPH's pressure term is stiff and blows up at a large step.
    private const float H = 16f;                 // smoothing radius
    private const float HSq = H * H;
    private const float ParticleMass = 2.5f;
    private const float RestDensity = 300f;
    private const float GasConstant = 2000f;
    private const float Viscosity = 200f;
    private const float SubstepDt = 0.0008f;
    private const int MaxSubstepsPerFrame = 40; // caps catch-up work if a frame stalls badly
    private static readonly Vector2 Gravity = new(0f, 12000f * 9.8f); // +Y is down in this world

    private const float BoundaryDamping = -0.5f;

    // Not `const`: they're derived from H via a runtime call (IntPow), which a const initializer
    // can't do -- computed once, here, instead of every kernel evaluation.
    private static readonly float Poly6 = 4f / (MathF.PI * IntPow(H, 8));
    private static readonly float SpikyGrad = -10f / (MathF.PI * IntPow(H, 5));
    private static readonly float ViscosityLaplacian = 40f / (MathF.PI * IntPow(H, 5));

    private static float IntPow(float value, int power)
    {
        float result = 1f;
        for (int i = 0; i < power; i++) result *= value;
        return result;
    }

    public readonly float ContainerWidth;
    public readonly float ContainerHeight;

    private Particle[] _current;
    private Particle[] _next;
    private float[] _density;
    private float[] _pressure;
    public int Count { get; private set; }

    // ---- Spatial hash: same counting-sort bucket grid as BoidsSwarm's, cell size = H so a 3x3
    // cell neighborhood always fully covers the kernel radius.
    private int _gridCols;
    private int _gridRows;
    private int[] _cellStarts;
    private int[] _cellCursor;
    private int[] _sortedIndices;
    private Vector2[] _sortedPositions; // gathered into cell order -- density sums read these a lot

    private readonly Random _rng;
    private float _leftoverDt;

    public FluidSim(float containerWidth, float containerHeight, int initialCount, int seed = 12345)
    {
        ContainerWidth = containerWidth;
        ContainerHeight = containerHeight;
        _rng = new Random(seed);

        _gridCols = Math.Max(1, (int)MathF.Ceiling(ContainerWidth / H));
        _gridRows = Math.Max(1, (int)MathF.Ceiling(ContainerHeight / H));
        _cellStarts = new int[_gridCols * _gridRows + 1];
        _cellCursor = new int[_gridCols * _gridRows];

        _current = Array.Empty<Particle>();
        _next = Array.Empty<Particle>();
        _density = Array.Empty<float>();
        _pressure = Array.Empty<float>();
        _sortedIndices = Array.Empty<int>();
        _sortedPositions = Array.Empty<Vector2>();

        SetCount(initialCount);
    }

    public void SetCount(int newCount)
    {
        newCount = Math.Max(1, newCount);
        if (newCount == Count) return;

        var newParticles = new Particle[newCount];
        int keep = Math.Min(newCount, Count);
        Array.Copy(_current, newParticles, keep);
        // Passes newCount explicitly rather than letting SpawnAt read the Count property -- Count
        // itself isn't updated until after this loop, so reading it here would use the *previous*
        // count for the column-count math below, silently laying every particle out in a single
        // column (an actual bug this caught: particles spawning far below the container).
        for (int i = keep; i < newCount; i++) newParticles[i] = SpawnAt(i, newCount);

        _current = newParticles;
        _next = new Particle[newCount];
        _density = new float[newCount];
        _pressure = new float[newCount];
        _sortedIndices = new int[newCount];
        _sortedPositions = new Vector2[newCount];
        Count = newCount;
    }

    public void Reseed()
    {
        for (int i = 0; i < Count; i++) _current[i] = SpawnAt(i, Count);
    }

    // Particles start packed into a block in the upper-left of the container (the classic SPH
    // "dam break" setup) and fall/spread under gravity -- a tiny position jitter keeps the initial
    // grid from being perfectly regular, which otherwise makes every particle in a row compute an
    // identical density and start moving in visually artificial lockstep.
    private Particle SpawnAt(int index, int total)
    {
        int columns = Math.Max(1, (int)MathF.Sqrt(total * (ContainerWidth * 0.5f) / (ContainerHeight * 0.7f)));
        int row = index / columns;
        int col = index % columns;

        // Spacing of exactly H, matching the reference this simulation's constants (REST_DENS,
        // GAS_CONST, ...) were tuned against -- packing particles any tighter starts them at a
        // much higher density than those constants expect, which spikes the initial pressure hard
        // enough to blow the simulation up within the first few substeps.
        float spacing = H;
        float jitterX = (float)_rng.NextDouble() * spacing * 0.1f;
        float jitterY = (float)_rng.NextDouble() * spacing * 0.1f;

        return new Particle
        {
            Position = new Vector2(
                H + col * spacing + jitterX,
                H + row * spacing + jitterY),
            Velocity = Vector2.Zero,
        };
    }

    /// <summary>Advances the fluid by <paramref name="dt"/> real seconds, internally split into fixed-size SPH substeps for numerical stability.</summary>
    public void Update(float dt)
    {
        _leftoverDt += dt;
        int substeps = Math.Min(MaxSubstepsPerFrame, (int)(_leftoverDt / SubstepDt));
        _leftoverDt -= substeps * SubstepDt;

        for (int s = 0; s < substeps; s++)
            Substep();
    }

    private void Substep()
    {
        BuildSpatialHash();

        Particle[] current = _current;
        float[] density = _density, pressure = _pressure;
        Parallel.For(0, Count, i => ComputeDensityPressure(i, current, density, pressure));

        Particle[] next = _next;
        Parallel.For(0, Count, i => ComputeForcesAndIntegrate(i, current, next, density, pressure));

        (_current, _next) = (_next, _current);
    }

    public ReadOnlySpan<Particle> Particles => _current;

    // ---- Spatial hash --------------------------------------------------------

    // Clamped, not just bounds-checked: a particle at least one full cell past the edge would
    // otherwise get inserted into the last valid cell here but have its own later 3x3 neighbor
    // query center on an out-of-range cell that never looks back at where it was actually placed --
    // missing its own self-contribution to density entirely, which divides by zero one line later
    // in ComputeForcesAndIntegrate. Both insertion (here) and every neighbor query below clamp the
    // same way, so a particle always finds itself regardless of how far out of bounds it strays.
    private void ClampedCell(Vector2 pos, out int cx, out int cy)
    {
        cx = (int)(pos.X / H);
        cy = (int)(pos.Y / H);
        if (cx >= _gridCols) cx = _gridCols - 1; else if (cx < 0) cx = 0;
        if (cy >= _gridRows) cy = _gridRows - 1; else if (cy < 0) cy = 0;
    }

    private int CellIndex(Vector2 pos)
    {
        ClampedCell(pos, out int cx, out int cy);
        return cy * _gridCols + cx;
    }

    private void BuildSpatialHash()
    {
        Array.Clear(_cellStarts, 0, _cellStarts.Length);
        for (int i = 0; i < Count; i++) _cellStarts[CellIndex(_current[i].Position) + 1]++;
        for (int c = 0; c < _gridCols * _gridRows; c++) _cellStarts[c + 1] += _cellStarts[c];

        Array.Copy(_cellStarts, _cellCursor, _cellCursor.Length);
        for (int i = 0; i < Count; i++)
        {
            int cell = CellIndex(_current[i].Position);
            _sortedIndices[_cellCursor[cell]++] = i;
        }

        for (int k = 0; k < Count; k++) _sortedPositions[k] = _current[_sortedIndices[k]].Position;
    }

    // Both passes below walk the same 3x3 cell neighborhood inline (not through a shared
    // Action-based helper) on purpose: this runs once per particle per pass per substep --
    // potentially millions of calls a second at scale -- and a delegate captured fresh each of
    // those calls would allocate at a frequency high enough to matter, unlike the single
    // per-Update() Parallel.For delegate (see DECISIONS.md's BoidsSwarm note: that one measurably
    // didn't matter, but this call site is a different frequency class entirely).

    // ---- Pass 1: density + pressure, one call per particle -----------------

    private void ComputeDensityPressure(int index, Particle[] current, float[] density, float[] pressure)
    {
        Vector2 pos = current[index].Position;
        float densitySum = 0f;

        ClampedCell(pos, out int cx, out int cy);
        for (int oy = -1; oy <= 1; oy++)
        {
            int ny = cy + oy;
            if (ny < 0 || ny >= _gridRows) continue;
            for (int ox = -1; ox <= 1; ox++)
            {
                int nx = cx + ox;
                if (nx < 0 || nx >= _gridCols) continue;
                int cell = ny * _gridCols + nx;
                int start = _cellStarts[cell], end = _cellStarts[cell + 1];
                for (int k = start; k < end; k++)
                {
                    Vector2 diff = pos - _sortedPositions[k];
                    float distSq = diff.LengthSquared();
                    if (distSq >= HSq) continue;
                    float diff2 = HSq - distSq;
                    densitySum += ParticleMass * Poly6 * diff2 * diff2 * diff2;
                }
            }
        }

        density[index] = densitySum;
        pressure[index] = MathF.Max(0f, GasConstant * (densitySum - RestDensity));
    }

    // ---- Pass 2: pressure + viscosity + gravity forces, integrate, bounce off walls ----

    private void ComputeForcesAndIntegrate(int index, Particle[] current, Particle[] next, float[] density, float[] pressure)
    {
        Particle self = current[index];
        float selfDensity = density[index];
        float selfPressure = pressure[index];

        Vector2 pressureForce = Vector2.Zero;
        Vector2 viscosityForce = Vector2.Zero;

        ClampedCell(self.Position, out int cx, out int cy);
        for (int oy = -1; oy <= 1; oy++)
        {
            int ny = cy + oy;
            if (ny < 0 || ny >= _gridRows) continue;
            for (int ox = -1; ox <= 1; ox++)
            {
                int nx = cx + ox;
                if (nx < 0 || nx >= _gridCols) continue;
                int cell = ny * _gridCols + nx;
                int start = _cellStarts[cell], end = _cellStarts[cell + 1];
                for (int k = start; k < end; k++)
                {
                    int j = _sortedIndices[k];
                    if (j == index) continue;

                    Vector2 diff = self.Position - _sortedPositions[k];
                    float distSq = diff.LengthSquared();
                    if (distSq >= HSq || distSq < 1e-9f) continue;

                    float dist = MathF.Sqrt(distSq);
                    float otherDensity = density[j];

                    // Pressure: symmetrized (self+other)/2 so the force pair is equal and
                    // opposite, pushing along -gradient of the spiky kernel (steep near r=0,
                    // unlike poly6 -- poly6's gradient vanishes at r=0, which lets particles
                    // clump instead of repel). diff/dist here is the unit vector pointing away
                    // from the neighbor (self - neighbor), which is exactly the direction a
                    // repulsive pressure force should push this particle.
                    float hMinusR = H - dist;
                    float pressureTerm = ParticleMass * (selfPressure + pressure[j]) / (2f * otherDensity);
                    pressureForce += (diff / dist) * (pressureTerm * SpikyGrad * hMinusR * hMinusR * hMinusR);

                    // Viscosity: pulls this particle's velocity toward its neighbors' average,
                    // weighted by the viscosity kernel's Laplacian -- damps relative motion so
                    // the fluid doesn't ring/oscillate forever.
                    viscosityForce += (current[j].Velocity - self.Velocity) * (ParticleMass / otherDensity * Viscosity * ViscosityLaplacian * (H - dist));
                }
            }
        }

        Vector2 gravityForce = Gravity * selfDensity;
        Vector2 acceleration = (pressureForce + viscosityForce + gravityForce) / selfDensity;

        Vector2 velocity = self.Velocity + acceleration * SubstepDt;
        Vector2 position = self.Position + velocity * SubstepDt;

        BounceOffWalls(ref position, ref velocity);

        next[index] = new Particle { Position = position, Velocity = velocity };
    }

    private void BounceOffWalls(ref Vector2 position, ref Vector2 velocity)
    {
        float margin = H * 0.5f;

        if (position.X < margin) { velocity.X *= BoundaryDamping; position.X = margin; }
        else if (position.X > ContainerWidth - margin) { velocity.X *= BoundaryDamping; position.X = ContainerWidth - margin; }

        if (position.Y < margin) { velocity.Y *= BoundaryDamping; position.Y = margin; }
        else if (position.Y > ContainerHeight - margin) { velocity.Y *= BoundaryDamping; position.Y = ContainerHeight - margin; }
    }
}
