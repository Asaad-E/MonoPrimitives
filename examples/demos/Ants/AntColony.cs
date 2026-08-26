using System;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using MonoPrimitives;

namespace Ants;

/// <summary>
/// Stigmergy: ants coordinate not by sensing each other (no spatial hash, no neighbor queries --
/// the one simulation here that genuinely doesn't need one) but by reading and depositing onto two
/// shared pheromone grids. A <see cref="AntState.Searching"/> ant wanders, steering toward
/// whatever <see cref="FoodTrail"/> it senses ahead, and lays down <see cref="HomeTrail"/> as it
/// goes; once it reaches food it flips to <see cref="AntState.Returning"/> and does the same thing
/// in reverse. Neither trail needs the other ants to be doing anything in particular -- the
/// shortest path emerges purely from trails decaying while a longer route accumulates fewer
/// reinforcing trips per second than a shorter one.
/// </summary>
internal sealed class AntColony
{
    public enum AntState { Searching, Returning }

    public struct Ant
    {
        public Vector2 Position;
        public float Heading; // radians, matching Vector2Extensions.Angle's convention
        public AntState State;
    }

    public readonly struct FoodSource
    {
        public readonly Vector2 Position;
        public readonly float Radius;
        public FoodSource(Vector2 position, float radius) { Position = position; Radius = radius; }
    }

    private const float MoveSpeed = 140f;
    private const float TurnSpeed = MathF.PI * 3f;      // max radians/sec the heading can swing
    private const float WanderJitter = 0.6f;             // radians/sec of random heading noise
    private const float SensorDistance = 28f;
    private const float SensorSpreadRadians = MathF.PI / 4f;
    private const float DepositAmount = 1f;
    private const float TrailDecayPerSecond = 0.35f;
    private const float PickupRadius = 14f;
    private const float NestRadius = 30f;

    public readonly float WorldWidth;
    public readonly float WorldHeight;
    public readonly Vector2 NestPosition;
    public readonly FoodSource[] FoodSources;

    private Ant[] _ants;
    public int Count => _ants.Length;

    private readonly int _gridCols;
    private readonly int _gridRows;
    private readonly float _cellSize;

    /// <summary>Deposited by searching ants -- lets returning-with-food ants retrace a known-good outbound path.</summary>
    public float[] HomeTrail { get; }

    /// <summary>Deposited by returning-with-food ants -- lets searching ants follow a known route straight to food.</summary>
    public float[] FoodTrail { get; }

    public int GridCols => _gridCols;
    public int GridRows => _gridRows;
    public float CellSize => _cellSize;

    private readonly Random _rng;

    public AntColony(float worldWidth, float worldHeight, int antCount, float cellSize, FoodSource[] foodSources, int seed = 12345)
    {
        WorldWidth = worldWidth;
        WorldHeight = worldHeight;
        NestPosition = new Vector2(worldWidth * 0.5f, worldHeight * 0.5f);
        FoodSources = foodSources;
        _cellSize = cellSize;
        _gridCols = Math.Max(1, (int)MathF.Ceiling(worldWidth / cellSize));
        _gridRows = Math.Max(1, (int)MathF.Ceiling(worldHeight / cellSize));
        HomeTrail = new float[_gridCols * _gridRows];
        FoodTrail = new float[_gridCols * _gridRows];

        _rng = new Random(seed);
        _ants = new Ant[antCount];
        for (int i = 0; i < antCount; i++) _ants[i] = SpawnAtNest();
    }

    public void SetCount(int newCount)
    {
        newCount = Math.Max(1, newCount);
        if (newCount == Count) return;

        var newAnts = new Ant[newCount];
        int keep = Math.Min(newCount, Count);
        Array.Copy(_ants, newAnts, keep);
        for (int i = keep; i < newCount; i++) newAnts[i] = SpawnAtNest();
        _ants = newAnts;
    }

    private Ant SpawnAtNest() => new()
    {
        Position = NestPosition,
        Heading = (float)(_rng.NextDouble() * MathHelper.TwoPi),
        State = AntState.Searching,
    };

    public ReadOnlySpan<Ant> Ants => _ants;

    public void Update(float dt)
    {
        Ant[] ants = _ants;
        Parallel.For(0, ants.Length, i => SenseAndMove(i, ants, dt));

        // Sequential: many ants can land on the same cell in the same frame, and a plain += from
        // multiple threads would drop updates -- this pass is O(ants), cheap next to the sensing
        // pass above, so it isn't worth the complexity of an atomic float add for it.
        for (int i = 0; i < ants.Length; i++)
            Deposit(ants[i]);

        DecayTrails(dt);
    }

    private void SenseAndMove(int index, Ant[] ants, float dt)
    {
        Ant ant = ants[index];
        float[] trail = ant.State == AntState.Searching ? FoodTrail : HomeTrail;

        float forward = SampleTrail(ant.Position, ant.Heading, trail);
        float left = SampleTrail(ant.Position, ant.Heading - SensorSpreadRadians, trail);
        float right = SampleTrail(ant.Position, ant.Heading + SensorSpreadRadians, trail);

        float turn = 0f;
        if (left > forward || right > forward)
            turn = left > right ? -TurnSpeed : TurnSpeed;
        turn += ((float)_rng.NextDouble() * 2f - 1f) * WanderJitter;

        // Once close enough to actually see the goal, steer straight for it instead of relying on
        // trail strength alone -- otherwise an ant can sit right next to the nest/food and never
        // quite center on it if the trail gradient there happens to be shallow.
        Vector2 goal = ant.State == AntState.Searching ? NearestFood(ant.Position) : NestPosition;
        float distToGoal = Vector2.Distance(ant.Position, goal);
        float homingRadius = ant.State == AntState.Searching ? SensorDistance * 3f : SensorDistance * 4f;
        if (distToGoal < homingRadius)
        {
            float desiredHeading = (goal - ant.Position).Angle();
            turn = MathHelper.WrapAngle(desiredHeading - ant.Heading) * 4f;
        }

        ant.Heading += Math.Clamp(turn, -TurnSpeed, TurnSpeed) * dt;
        Vector2 direction = new(MathF.Cos(ant.Heading), MathF.Sin(ant.Heading));
        ant.Position = WrapToWorld(ant.Position + direction * MoveSpeed * dt);

        if (ant.State == AntState.Searching && DistanceToNearestFood(ant.Position) < PickupRadius)
        {
            ant.State = AntState.Returning;
            ant.Heading += MathF.PI; // turn around immediately, toward the nest
        }
        else if (ant.State == AntState.Returning && Vector2.Distance(ant.Position, NestPosition) < NestRadius)
        {
            ant.State = AntState.Searching;
            ant.Heading += MathF.PI;
        }

        ants[index] = ant;
    }

    private void Deposit(in Ant ant)
    {
        float[] trail = ant.State == AntState.Searching ? HomeTrail : FoodTrail;
        trail[CellIndex(ant.Position)] += DepositAmount;
    }

    private void DecayTrails(float dt)
    {
        float retain = MathF.Max(0f, 1f - TrailDecayPerSecond * dt);
        Parallel.For(0, HomeTrail.Length, i =>
        {
            HomeTrail[i] *= retain;
            FoodTrail[i] *= retain;
        });
    }

    private float SampleTrail(Vector2 position, float heading, float[] trail)
    {
        Vector2 sensorPos = position + new Vector2(MathF.Cos(heading), MathF.Sin(heading)) * SensorDistance;
        return trail[CellIndex(sensorPos)];
    }

    private int CellIndex(Vector2 pos)
    {
        int cx = (int)(WrapCoord(pos.X, WorldWidth) / _cellSize);
        int cy = (int)(WrapCoord(pos.Y, WorldHeight) / _cellSize);
        if (cx >= _gridCols) cx = _gridCols - 1; else if (cx < 0) cx = 0;
        if (cy >= _gridRows) cy = _gridRows - 1; else if (cy < 0) cy = 0;
        return cy * _gridCols + cx;
    }

    private Vector2 NearestFood(Vector2 pos)
    {
        Vector2 best = FoodSources[0].Position;
        float bestDistSq = Vector2.DistanceSquared(pos, best);
        for (int i = 1; i < FoodSources.Length; i++)
        {
            float distSq = Vector2.DistanceSquared(pos, FoodSources[i].Position);
            if (distSq < bestDistSq) { bestDistSq = distSq; best = FoodSources[i].Position; }
        }
        return best;
    }

    private float DistanceToNearestFood(Vector2 pos)
    {
        float best = float.MaxValue;
        foreach (FoodSource food in FoodSources)
            best = MathF.Min(best, Vector2.Distance(pos, food.Position) - food.Radius);
        return best;
    }

    private static float WrapCoord(float v, float span) => ((v % span) + span) % span;

    private Vector2 WrapToWorld(Vector2 p) => new(WrapCoord(p.X, WorldWidth), WrapCoord(p.Y, WorldHeight));
}
