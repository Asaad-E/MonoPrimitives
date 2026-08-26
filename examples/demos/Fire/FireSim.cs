using System;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using MonoPrimitives;

namespace Fire;

/// <summary>
/// A particle-based fire: unlike every other simulation in examples/demos/, fire particles never
/// interact with each other -- no neighbor queries, no spatial hash, no double buffer -- so this
/// is a much lighter kind of "many small things" than boids/N-bodies/fluid. Each particle just
/// ages, rises, drifts along the same kind of Perlin-noise flow field <c>BoidsSwarm</c> uses, and
/// cools from white-hot through orange to smoke as it dies (<see cref="ColorUtil.FromTemperature"/>).
/// Dead particles are compacted out with a swap-remove instead of tracked with a separate free
/// list, keeping the live particles a single dense, cache-friendly array.
/// </summary>
internal sealed class FireSim
{
    public struct Particle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Age;
        public float MaxLife;
    }

    private const float RiseSpeed = 220f;
    private const float RiseSpeedJitter = 60f;
    private const float SpawnSpread = 90f;         // horizontal spawn jitter around the emitter x
    private const float MinLifeSeconds = 1.4f;
    private const float MaxLifeSeconds = 2.6f;
    private const float TurbulenceStrength = 140f;
    public const float BaseParticleRadius = 16f;

    private const int FlowCols = 48;
    private const int FlowRows = 32;
    private const float FlowNoiseScale = 0.004f;
    private const float FlowTimeScale = 0.35f;
    private const float FlowAngleScale = MathF.PI * 3f;
    private readonly Vector2[] _flowField;
    private readonly Noise _noise;
    private float _flowTime;

    public readonly float WorldWidth;
    public readonly float WorldHeight;
    public float EmitterX { get; set; }
    public float SpawnRatePerSecond { get; set; } = 400f;

    private Particle[] _particles;
    public int LiveCount { get; private set; }

    private readonly Random _rng;
    private float _spawnAccumulator;

    public FireSim(float worldWidth, float worldHeight, int maxParticles, int seed = 12345)
    {
        WorldWidth = worldWidth;
        WorldHeight = worldHeight;
        EmitterX = worldWidth * 0.5f;
        _rng = new Random(seed);
        _particles = new Particle[maxParticles];
        _noise = new Noise(seed);
        _flowField = new Vector2[FlowCols * FlowRows];
    }

    public int Capacity => _particles.Length;

    public void SetCapacity(int newCapacity)
    {
        newCapacity = Math.Max(1, newCapacity);
        if (newCapacity == Capacity) return;

        var newParticles = new Particle[newCapacity];
        int keep = Math.Min(LiveCount, newCapacity);
        Array.Copy(_particles, newParticles, keep);
        _particles = newParticles;
        LiveCount = keep;
    }

    public ReadOnlySpan<Particle> Particles => _particles.AsSpan(0, LiveCount);

    public void Update(float dt)
    {
        BuildFlowField(dt);

        Particle[] particles = _particles;
        int liveCount = LiveCount;
        Parallel.For(0, liveCount, i => AdvanceParticle(i, particles, dt));

        CompactDeadParticles();
        SpawnNewParticles(dt);
    }

    // ---- Flow field: identical technique to BoidsSwarm's, animated faster and rougher (higher
    // FlowAngleScale/FlowTimeScale) for a flickery look instead of a slow, smooth drift. ----

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

    // ---- Per-particle step: runs on the thread pool, one call per live particle -- no particle
    // ever reads another's slot, so this needs no locking, and there's no double buffer to swap
    // since nothing here depends on any other particle's *current* state.

    private void AdvanceParticle(int index, Particle[] particles, float dt)
    {
        Particle p = particles[index];
        p.Age += dt;

        Vector2 turbulence = SampleFlowField(p.Position) * TurbulenceStrength;
        p.Velocity = new Vector2(turbulence.X, -RiseSpeed + turbulence.Y * 0.4f);
        p.Position += p.Velocity * dt;

        particles[index] = p;
    }

    // Sequential on purpose: swapping a dead particle with the last live one to keep the array
    // dense isn't safe to do from multiple threads at once (two removals could race on the same
    // "last live" slot), and this pass is O(liveCount) -- trivial next to the parallel step above.
    private void CompactDeadParticles()
    {
        int i = 0;
        while (i < LiveCount)
        {
            if (_particles[i].Age >= _particles[i].MaxLife)
            {
                LiveCount--;
                _particles[i] = _particles[LiveCount];
            }
            else
            {
                i++;
            }
        }
    }

    private void SpawnNewParticles(float dt)
    {
        _spawnAccumulator += SpawnRatePerSecond * dt;
        int toSpawn = (int)_spawnAccumulator;
        _spawnAccumulator -= toSpawn;

        int room = Capacity - LiveCount;
        toSpawn = Math.Min(toSpawn, room);

        for (int n = 0; n < toSpawn; n++)
            _particles[LiveCount++] = SpawnParticle();
    }

    private Particle SpawnParticle()
    {
        float spawnX = EmitterX + ((float)_rng.NextDouble() * 2f - 1f) * SpawnSpread;
        float upSpeed = RiseSpeed + ((float)_rng.NextDouble() * 2f - 1f) * RiseSpeedJitter;
        return new Particle
        {
            Position = new Vector2(spawnX, WorldHeight),
            Velocity = new Vector2(0f, -upSpeed),
            Age = 0f,
            MaxLife = MinLifeSeconds + (float)_rng.NextDouble() * (MaxLifeSeconds - MinLifeSeconds),
        };
    }

    /// <summary>0 at birth (hottest) to 1 at death (coldest/about to vanish) -- drives both color and size in <see cref="Game1"/>.</summary>
    public static float AgeFraction(in Particle p) => MathHelper.Clamp(p.Age / p.MaxLife, 0f, 1f);
}
