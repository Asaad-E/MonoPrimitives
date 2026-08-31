# Trail2D / Trail3D — Guide

`Trail2D` (namespace `MonoPrimitives.Primitives2D`, file [`src/2D/Trail2D.cs`](../src/2D/Trail2D.cs)) is a fixed-capacity history of recent positions, drawn as a line that fades out toward the oldest point — a moving particle/agent's own trail. It's a fixed-size ring buffer under the hood: `Add` never allocates once warmed up, and old points are simply overwritten rather than shifted.

![Three Trail2D paths -- an arc, a wave, and a spiral -- each fading from a solid head to nothing](../img/trail_2d.png)

## Quick start

```csharp
using MonoPrimitives.Primitives2D;

private readonly Trail2D _trail = new(capacity: 30);

protected override void Update(GameTime gameTime)
{
    _trail.Add(particle.Position); // once per frame
}

protected override void Draw(GameTime gameTime)
{
    _batch.Begin();
    _trail.Draw(_batch, Color.Cyan, thickness: 3f, fadeToAlpha: 0f);
    _batch.End();
}
```

`capacity` is the trail's length in **frames of history**, not world units — a fast-moving particle's trail covers more distance for the same capacity than a slow one's. Pick it by how long a trail should visually persist, not by a target length in pixels.

## API

| Member | What it does |
|---|---|
| `new Trail2D(capacity)` | Empty trail holding up to `capacity` points. Throws if `capacity < 2` — a trail needs at least 2 points to draw a line. |
| `Add(position)` | Appends the current position, evicting the oldest point once `Capacity` is reached. Call once per frame. |
| `Clear()` | Drops every recorded point — call when the tracked thing teleports, so the trail doesn't draw a line across the jump. |
| `Capacity` | Maximum points held (fixed, set at construction). |
| `Count` | How many points are actually recorded so far (grows to `Capacity`, then stays there). |
| `this[indexFromOldest]` | Position at that index — `0` is the oldest recorded point, `Count - 1` is the newest. |
| `Draw(batch, color, thickness = 2f, fadeToAlpha = 0f)` | Draws `Count - 1` segments, fading from `color` at the newest end toward `color` scaled by `fadeToAlpha` at the oldest — `0` (default) fades all the way to invisible. No-ops if `Count < 2`. |

## Cost and fade, briefly

`Draw` costs one `DrawLine` call per segment (`Count - 1` total) — a single-color `DrawLineStrip` can't fade its own length, so each segment gets its own flat color instead. Keep `Capacity` no bigger than the trail actually needs to look right, especially with many trails on screen at once.

Each segment's color comes from its own *midpoint* position along the trail (not its endpoints), so the newest segment never quite reaches full `color.A` and the oldest never quite reaches exactly `fadeToAlpha` — an intentional, symmetric compromise given the one-flat-color-per-segment constraint above, not a bug. Not worth tuning unless you want the fade curve to hit those exact endpoints.

## 3D: `Trail3D`

`Trail3D` (namespace `MonoPrimitives.Primitives3D`, file [`src/3D/Trail3D.cs`](../src/3D/Trail3D.cs)) is the same ring-buffer trail, one dimension higher:

![A Trail3D following a helical path, fading from a solid head to nothing](../img/trail_3d.png)

```csharp
using MonoPrimitives.Primitives3D;

private readonly Trail3D _trail = new(capacity: 30);

protected override void Update(GameTime gameTime)
{
    _trail.Add(particle.Position); // Vector3, once per frame
}

protected override void Draw(GameTime gameTime)
{
    _batch.Begin(_camera);
    _trail.Draw(_batch, Color.Cyan, thickness: 0.1f, fadeToAlpha: 0f);
    _batch.End();
}
```

| Member | What it does |
|---|---|
| `new Trail3D(capacity)` | Empty trail holding up to `capacity` points (`Vector3`). Throws if `capacity < 2`. |
| `Add(position)` | Appends the current position, evicting the oldest point once `Capacity` is reached. |
| `Clear()` | Drops every recorded point. |
| `Capacity` / `Count` | Same as `Trail2D`. |
| `this[indexFromOldest]` | Same as `Trail2D`, returning a `Vector3`. |
| `Draw(batch, color, thickness = -1f, fadeToAlpha = 0f)` | Draws `Count - 1` `DrawLine3D` segments, same fade behavior as `Trail2D.Draw`. `thickness <= 0` (the default, `-1f`) falls back to `Primitive3DBatch.DefaultLineThickness` — the same sentinel convention as this library's other `Border*`/`Draw*` methods, unlike `Trail2D.Draw`'s fixed `2f` default. |

`DrawLineStrip3D`/`Trail3D.Draw` draw a single joined camera-facing strip — adjacent segments share one miter-joined offset at each interior vertex, so a sharp bend doesn't show a gap or overlap even at non-trivial `thickness`. See [`Design/DECISIONS.md`](../Design/DECISIONS.md) for how this was verified.

## See also

- [`Primitive2DBatch`](Primitive2DBatch_Guide.md) / [`Primitive3DBatch`](Primitive3DBatch_Guide.md) — `DrawLine`/`DrawLine3D`, which `Trail2D.Draw`/`Trail3D.Draw` are built on.
- `examples/test/ParticleTrailTest` — several `Trail2D`s with different capacity/thickness/fade styles, dragged behind particles that bounce off the window edges and each other.
