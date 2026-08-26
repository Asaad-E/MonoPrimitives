#nullable enable

using System;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using MonoPrimitives;

namespace NBodies2D;

/// <summary>
/// Gravitational N-body simulation: every body attracts every other body, with no perception
/// radius to bound the search the way flocking has -- a plain pairwise sum is O(bodies^2) and
/// caps out at a few thousand bodies. A Barnes-Hut quadtree instead groups distant bodies into
/// one approximate mass, giving O(bodies * log(bodies)) -- the standard technique for gravity at
/// scale. The tree is rebuilt sequentially every frame (insertion order matters, so it doesn't
/// parallelize cleanly); the actual force-summing tree walk, one per body, does.
/// </summary>
internal sealed class NBodySwarm
{
    public struct Body
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Mass;
        public Color Color;
    }

    private const float G = 4000f;                // tuned for a visually pleasing orbit speed, not real units
    private const float Theta = 0.6f;              // Barnes-Hut opening angle -- lower = more accurate, slower
    private const float SofteningSq = 12f * 12f;   // caps force at close range so bodies don't sling to infinity
    private const float MinBodyMass = 1f;
    private const float MaxBodyMass = 4f;
    private const float CentralMass = 60_000f;     // the one heavy body every other body orbits

    public readonly float WorldWidth;
    public readonly float WorldHeight;

    private Body[] _current;
    private Body[] _next;
    public int Count { get; private set; }

    private readonly Random _rng;

    // ---- Barnes-Hut quadtree, pooled ---------------------------------------
    // A tree node per body, worst case, rebuilt every frame -- pooling and reusing the same
    // QuadNode objects (reset, not reallocated) avoids paying that allocation cost every frame.
    private QuadNode[] _nodePool;
    private int _nodesInUse;
    private QuadNode _root = null!;

    private sealed class QuadNode
    {
        public Vector2 Center;
        public float HalfSize;
        public Vector2 ComPosition;
        public float TotalMass;
        public int BodyIndex = -1; // valid only when Children is null and TotalMass > 0 (a single-body leaf)
        public QuadNode[]? Children;

        public void Reset(Vector2 center, float halfSize)
        {
            Center = center;
            HalfSize = halfSize;
            ComPosition = Vector2.Zero;
            TotalMass = 0f;
            BodyIndex = -1;
            Children = null;
        }

        public int QuadrantOf(Vector2 pos) => (pos.X > Center.X ? 1 : 0) + (pos.Y > Center.Y ? 2 : 0);

        public Vector2 ChildCenter(int quadrant)
        {
            float offset = HalfSize * 0.5f;
            return Center + new Vector2((quadrant & 1) != 0 ? offset : -offset, (quadrant & 2) != 0 ? offset : -offset);
        }
    }

    public NBodySwarm(int worldWidth, int worldHeight, int initialCount, int seed = 12345)
    {
        WorldWidth = worldWidth;
        WorldHeight = worldHeight;
        _rng = new Random(seed);
        _current = Array.Empty<Body>();
        _next = Array.Empty<Body>();
        _nodePool = new QuadNode[256];
        for (int i = 0; i < _nodePool.Length; i++) _nodePool[i] = new QuadNode();

        SetCount(initialCount);
    }

    public void SetCount(int newCount)
    {
        newCount = Math.Max(2, newCount);
        if (newCount == Count) return;

        var newBodies = new Body[newCount];
        int keep = Math.Min(newCount, Count);
        Array.Copy(_current, newBodies, keep);
        for (int i = keep; i < newCount; i++) newBodies[i] = SpawnOrbiting(i == 0);

        _current = newBodies;
        _next = new Body[newCount];
        Count = newCount;
    }

    public void Reseed()
    {
        for (int i = 0; i < Count; i++) _current[i] = SpawnOrbiting(i == 0);
    }

    // Body 0 is the heavy central anchor everything else orbits; the rest spawn on a randomized
    // ring around it with the tangential speed a circular orbit at that radius needs (v =
    // sqrt(G*M/r)) so the galaxy holds its disk shape instead of immediately collapsing or flying
    // apart -- pure random velocities look like a scattering explosion, not a galaxy.
    private Body SpawnOrbiting(bool isCentral)
    {
        Vector2 center = new(WorldWidth * 0.5f, WorldHeight * 0.5f);
        if (isCentral)
        {
            return new Body { Position = center, Velocity = Vector2.Zero, Mass = CentralMass, Color = Color.White };
        }

        float radius = 40f + (float)_rng.NextDouble() * (WorldHeight * 0.45f);
        float angle = (float)(_rng.NextDouble() * MathHelper.TwoPi);
        Vector2 offset = new(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);

        float orbitalSpeed = MathF.Sqrt(G * CentralMass / radius);
        Vector2 tangent = offset.PerpendicularCounterClockwise().SafeNormalize();
        // A little velocity noise keeps the disk from being a perfect, static ring of circles.
        Vector2 velocity = tangent * orbitalSpeed * (0.9f + (float)_rng.NextDouble() * 0.2f);

        float mass = MinBodyMass + (float)_rng.NextDouble() * (MaxBodyMass - MinBodyMass);
        return new Body
        {
            Position = center + offset,
            Velocity = velocity,
            Mass = mass,
            Color = ColorUtil.FromTemperature(MathHelper.Lerp(1500f, 12000f, orbitalSpeed / 400f)),
        };
    }

    public void Update(float dt)
    {
        BuildTree();

        Body[] current = _current, next = _next;
        Parallel.For(0, Count, i => StepBody(i, current, next, dt));

        (_current, _next) = (_next, _current);
    }

    public ReadOnlySpan<Body> Bodies => _current;

    // ---- Tree construction (sequential -- insertion order matters) --------

    private void BuildTree()
    {
        _nodesInUse = 0;

        Vector2 min = _current[0].Position, max = _current[0].Position;
        for (int i = 1; i < Count; i++)
        {
            Vector2 p = _current[i].Position;
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }
        Vector2 center = (min + max) * 0.5f;
        float halfSize = MathF.Max(MathF.Max(max.X - min.X, max.Y - min.Y) * 0.5f, 1f) + 1f;

        _root = RentNode(center, halfSize);
        for (int i = 0; i < Count; i++)
            Insert(_root, i);
    }

    private QuadNode RentNode(Vector2 center, float halfSize)
    {
        if (_nodesInUse == _nodePool.Length)
            Array.Resize(ref _nodePool, _nodePool.Length * 2);
        QuadNode node = _nodePool[_nodesInUse];
        if (node is null) _nodePool[_nodesInUse] = node = new QuadNode();
        node.Reset(center, halfSize);
        _nodesInUse++;
        return node;
    }

    private void Insert(QuadNode node, int bodyIndex)
    {
        Vector2 pos = _current[bodyIndex].Position;
        float mass = _current[bodyIndex].Mass;

        // Every node accumulates total mass and center-of-mass on the way down/up regardless of
        // whether it ends up a leaf or internal -- that running total IS the approximation a
        // distant body reads later, so it has to include every body under this node.
        node.ComPosition = (node.ComPosition * node.TotalMass + pos * mass) / (node.TotalMass + mass);
        node.TotalMass += mass;

        if (node.Children is not null)
        {
            Insert(node.Children[node.QuadrantOf(pos)], bodyIndex);
            return;
        }

        if (node.BodyIndex < 0)
        {
            node.BodyIndex = bodyIndex; // empty leaf -- just place the body here
            return;
        }

        // Second body landing in this leaf: subdivide, then re-insert the one that was already
        // here alongside the new one.
        int existingIndex = node.BodyIndex;
        node.BodyIndex = -1;
        node.Children = new QuadNode[4];
        for (int q = 0; q < 4; q++)
            node.Children[q] = RentNode(node.ChildCenter(q), node.HalfSize * 0.5f);

        Vector2 existingPos = _current[existingIndex].Position;
        node.Children[node.QuadrantOf(existingPos)].BodyIndex = existingIndex;
        // The existing body's own mass/COM contribution was already folded into this node above
        // (from when it was first inserted) but its new leaf child still needs it recorded too.
        var child = node.Children[node.QuadrantOf(existingPos)];
        child.ComPosition = existingPos;
        child.TotalMass = _current[existingIndex].Mass;

        Insert(node.Children[node.QuadrantOf(pos)], bodyIndex);
    }

    // ---- Per-body step: runs on the thread pool, one call per body --------
    // Reads the tree built above (untouched for the rest of this Update() call) and only ever
    // writes its own next[index] slot -- safe to parallelize with no locking.

    private void StepBody(int index, Body[] current, Body[] next, float dt)
    {
        Body self = current[index];
        Vector2 force = AccumulateForce(_root, self.Position, index);
        Vector2 acceleration = force * (1f / self.Mass);

        self.Velocity += acceleration * dt;
        self.Position += self.Velocity * dt;
        next[index] = self;
    }

    private Vector2 AccumulateForce(QuadNode node, Vector2 pos, int excludeIndex)
    {
        if (node.TotalMass <= 0f) return Vector2.Zero;

        if (node.Children is null)
        {
            if (node.BodyIndex == excludeIndex) return Vector2.Zero;
            return Attraction(node.ComPosition, node.TotalMass, pos);
        }

        Vector2 toCom = node.ComPosition - pos;
        float distance = toCom.Length();
        // The opening-angle test: if this node's span looks small enough from here, treat its
        // whole subtree as one point mass at its center of mass instead of recursing further.
        if (node.HalfSize * 2f / MathF.Max(distance, 1e-6f) < Theta)
            return Attraction(node.ComPosition, node.TotalMass, pos);

        Vector2 total = Vector2.Zero;
        foreach (QuadNode child in node.Children!)
            total += AccumulateForce(child, pos, excludeIndex);
        return total;
    }

    private static Vector2 Attraction(Vector2 fromPos, float mass, Vector2 pos)
    {
        Vector2 toMass = fromPos - pos;
        float distSq = toMass.LengthSquared() + SofteningSq;
        float invDist = 1f / MathF.Sqrt(distSq);
        float invDistCubed = invDist * invDist * invDist;
        return toMass * (G * mass * invDistCubed);
    }
}
