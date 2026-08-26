#nullable enable

using System;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using MonoPrimitives;
using MonoPrimitives.Primitives3D;

namespace NBodies3D;

/// <summary>
/// The 3D counterpart to examples/demos/NBodies2D: gravitational attraction between every pair of
/// bodies, approximated at scale with a Barnes-Hut octree (8 children per node instead of a
/// quadtree's 4) instead of the O(bodies^2) pairwise sum a naive version would need. The tree is
/// rebuilt sequentially every frame (insertion order matters); the force-summing tree walk, one
/// per body, runs in parallel across every core.
/// </summary>
internal sealed class NBodySwarm
{
    public struct Body
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Mass;
        public Color Color;
    }

    private const float G = 4000f;
    private const float Theta = 0.7f;
    private const float SofteningSq = 14f * 14f;
    private const float MinBodyMass = 1f;
    private const float MaxBodyMass = 4f;
    private const float CentralMass = 80_000f;

    public readonly float WorldSize;

    private Body[] _current;
    private Body[] _next;
    public int Count { get; private set; }

    private readonly Random _rng;

    // ---- Barnes-Hut octree, pooled ------------------------------------------
    private OctNode[] _nodePool;
    private int _nodesInUse;
    private OctNode _root = null!;

    private sealed class OctNode
    {
        public Vector3 Center;
        public float HalfSize;
        public Vector3 ComPosition;
        public float TotalMass;
        public int BodyIndex = -1;
        public OctNode[]? Children;

        public void Reset(Vector3 center, float halfSize)
        {
            Center = center;
            HalfSize = halfSize;
            ComPosition = Vector3.Zero;
            TotalMass = 0f;
            BodyIndex = -1;
            Children = null;
        }

        public int OctantOf(Vector3 pos) =>
            (pos.X > Center.X ? 1 : 0) + (pos.Y > Center.Y ? 2 : 0) + (pos.Z > Center.Z ? 4 : 0);

        public Vector3 ChildCenter(int octant)
        {
            float o = HalfSize * 0.5f;
            return Center + new Vector3(
                (octant & 1) != 0 ? o : -o,
                (octant & 2) != 0 ? o : -o,
                (octant & 4) != 0 ? o : -o);
        }
    }

    public NBodySwarm(float worldSize, int initialCount, int seed = 12345)
    {
        WorldSize = worldSize;
        _rng = new Random(seed);
        _current = Array.Empty<Body>();
        _next = Array.Empty<Body>();
        _nodePool = new OctNode[256];
        for (int i = 0; i < _nodePool.Length; i++) _nodePool[i] = new OctNode();

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

    // Body 0 is the heavy central anchor; the rest spawn on a randomized spherical shell around
    // it with a tangential velocity (perpendicular to a random spin axis) sized for a circular
    // orbit at that radius, so the cluster holds a rotating structure instead of collapsing
    // straight into the center or scattering outward on the first frame.
    private Body SpawnOrbiting(bool isCentral)
    {
        if (isCentral)
            return new Body { Position = Vector3.Zero, Velocity = Vector3.Zero, Mass = CentralMass, Color = Color.White };

        Vector3 direction = RandomDirectionOnSphere();
        float radius = 60f + (float)_rng.NextDouble() * (WorldSize * 0.45f);
        Vector3 position = direction * radius;

        // Any axis not parallel to `direction` works to build a tangent; +Up almost always
        // qualifies, with +Right as a fallback for the rare near-parallel case.
        Vector3 spinAxis = Vector3.Up;
        if (MathF.Abs(Vector3.Dot(direction, spinAxis)) > 0.95f) spinAxis = Vector3.Right;
        Vector3 tangent = Vector3.Cross(spinAxis, direction).SafeNormalize();

        float orbitalSpeed = MathF.Sqrt(G * CentralMass / radius);
        Vector3 velocity = tangent * orbitalSpeed * (0.9f + (float)_rng.NextDouble() * 0.2f);

        float mass = MinBodyMass + (float)_rng.NextDouble() * (MaxBodyMass - MinBodyMass);
        return new Body
        {
            Position = position,
            Velocity = velocity,
            Mass = mass,
            Color = ColorUtil.FromTemperature(MathHelper.Lerp(1500f, 12000f, orbitalSpeed / 400f)),
        };
    }

    // Uniform random point on a unit sphere via Archimedes' equal-area projection: z picked
    // uniformly in [-1,1], then a uniformly random angle around it -- avoids the bias toward the
    // poles a naive independent-per-axis-then-normalize sample would have.
    private Vector3 RandomDirectionOnSphere()
    {
        float z = (float)(_rng.NextDouble() * 2.0 - 1.0);
        float azimuth = (float)(_rng.NextDouble() * MathHelper.TwoPi);
        float ringRadius = MathF.Sqrt(MathF.Max(0f, 1f - z * z));
        return new Vector3(ringRadius * MathF.Cos(azimuth), ringRadius * MathF.Sin(azimuth), z);
    }

    public void Update(float dt)
    {
        BuildTree();

        Body[] current = _current, next = _next;
        Parallel.For(0, Count, i => StepBody(i, current, next, dt));

        (_current, _next) = (_next, _current);
    }

    public ReadOnlySpan<Body> Bodies => _current;

    private void BuildTree()
    {
        _nodesInUse = 0;

        Vector3 min = _current[0].Position, max = _current[0].Position;
        for (int i = 1; i < Count; i++)
        {
            Vector3 p = _current[i].Position;
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        Vector3 center = (min + max) * 0.5f;
        Vector3 extent = max - min;
        float halfSize = MathF.Max(MathF.Max(extent.X, MathF.Max(extent.Y, extent.Z)) * 0.5f, 1f) + 1f;

        _root = RentNode(center, halfSize);
        for (int i = 0; i < Count; i++)
            Insert(_root, i);
    }

    private OctNode RentNode(Vector3 center, float halfSize)
    {
        if (_nodesInUse == _nodePool.Length)
            Array.Resize(ref _nodePool, _nodePool.Length * 2);
        OctNode node = _nodePool[_nodesInUse];
        if (node is null) _nodePool[_nodesInUse] = node = new OctNode();
        node.Reset(center, halfSize);
        _nodesInUse++;
        return node;
    }

    private void Insert(OctNode node, int bodyIndex)
    {
        Vector3 pos = _current[bodyIndex].Position;
        float mass = _current[bodyIndex].Mass;

        node.ComPosition = (node.ComPosition * node.TotalMass + pos * mass) / (node.TotalMass + mass);
        node.TotalMass += mass;

        if (node.Children is not null)
        {
            Insert(node.Children[node.OctantOf(pos)], bodyIndex);
            return;
        }

        if (node.BodyIndex < 0)
        {
            node.BodyIndex = bodyIndex;
            return;
        }

        int existingIndex = node.BodyIndex;
        node.BodyIndex = -1;
        node.Children = new OctNode[8];
        for (int o = 0; o < 8; o++)
            node.Children[o] = RentNode(node.ChildCenter(o), node.HalfSize * 0.5f);

        Vector3 existingPos = _current[existingIndex].Position;
        OctNode existingChild = node.Children[node.OctantOf(existingPos)];
        existingChild.BodyIndex = existingIndex;
        existingChild.ComPosition = existingPos;
        existingChild.TotalMass = _current[existingIndex].Mass;

        Insert(node.Children[node.OctantOf(pos)], bodyIndex);
    }

    private void StepBody(int index, Body[] current, Body[] next, float dt)
    {
        Body self = current[index];
        Vector3 force = AccumulateForce(_root, self.Position, index);
        Vector3 acceleration = force * (1f / self.Mass);

        self.Velocity += acceleration * dt;
        self.Position += self.Velocity * dt;
        next[index] = self;
    }

    private Vector3 AccumulateForce(OctNode node, Vector3 pos, int excludeIndex)
    {
        if (node.TotalMass <= 0f) return Vector3.Zero;

        if (node.Children is null)
        {
            if (node.BodyIndex == excludeIndex) return Vector3.Zero;
            return Attraction(node.ComPosition, node.TotalMass, pos);
        }

        Vector3 toCom = node.ComPosition - pos;
        float distance = toCom.Length();
        if (node.HalfSize * 2f / MathF.Max(distance, 1e-6f) < Theta)
            return Attraction(node.ComPosition, node.TotalMass, pos);

        Vector3 total = Vector3.Zero;
        foreach (OctNode child in node.Children!)
            total += AccumulateForce(child, pos, excludeIndex);
        return total;
    }

    private static Vector3 Attraction(Vector3 fromPos, float mass, Vector3 pos)
    {
        Vector3 toMass = fromPos - pos;
        float distSq = toMass.LengthSquared() + SofteningSq;
        float invDist = 1f / MathF.Sqrt(distSq);
        float invDistCubed = invDist * invDist * invDist;
        return toMass * (G * mass * invDistCubed);
    }
}
