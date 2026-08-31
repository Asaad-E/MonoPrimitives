# RectangleF — Guide

`RectangleF` (namespace `MonoPrimitives`, file [`src/Core/RectangleF.cs`](../src/Core/RectangleF.cs)) is a float-precision counterpart to MonoGame's own `Rectangle` (integer-only) — for anything that needs sub-pixel positions/sizes without truncating them: a zoomed camera's visible bounds, a smoothly scaling UI panel, a hitbox that shouldn't snap to whole pixels.

## Quick start

```csharp
using MonoPrimitives;

var bounds = new RectangleF(10.5f, 20.25f, 100f, 50f);
var hitbox = RectangleF.FromCenter(player.Position, new Vector2(32f, 32f));

if (hitbox.Intersects(bounds)) { /* ... */ }
```

## API

Mirrors `Rectangle`'s own member shape, just with `float` fields — anything you already know from `Rectangle` works the same way here.

| Member | What it does |
|---|---|
| `X`/`Y`/`Width`/`Height` | Public fields, same as `Rectangle`. `Width`/`Height` can be negative — nothing here guards against it. |
| `new RectangleF(x, y, width, height)` | Direct construction. |
| `FromCenter(center, size)` | Builds a rectangle of `size` centered on `center`. |
| `Left`/`Right`/`Top`/`Bottom` | Computed from `X`/`Y`/`Width`/`Height`. |
| `Position`/`Size` | `Vector2` views of `X,Y` / `Width,Height` — settable. |
| `Center` | Midpoint — stays correct even for a negative `Width`/`Height`. |
| `IsEmpty` | True when `Width <= 0` or `Height <= 0`. |
| `Contains(x, y)` / `Contains(Vector2)` / `Contains(RectangleF)` | Point-in-rect and rect-fully-inside-rect checks. |
| `Intersects(other)` | True if the two rectangles overlap — edge-touching alone does **not** count, matching `Rectangle.Intersects`. |
| `Inflate(horizontalAmount, verticalAmount)` | Returns a grown (or shrunk, for a negative amount) copy, same center — matches `Rectangle.Inflate`'s convention (each side moves by the given amount, so total size changes by double), but returns a new value instead of mutating in place. |
| `RectangleF.Intersect(a, b)` | The overlapping region, or `Empty` if they don't intersect. |
| `RectangleF.Union(a, b)` | The smallest rectangle containing both. |
| `RectangleF.Lerp(a, b, t)` | Linearly interpolates `X`/`Y`/`Width`/`Height` independently. `t` isn't clamped — values outside `[0,1]` extrapolate. Pairs with `Camera2D.FitBounds`/`Camera3D.FitBounds` for an eased "zoom to fit" instead of their instant cut. |
| `ToRectangle()` | Rounds to the nearest integer `Rectangle` (`MathF.Round`, banker's rounding at exact `.5` values). |
| `(RectangleF)rectangle` / implicit from `Rectangle` | A `Rectangle`'s integer values widen to `RectangleF` for free — exact, no rounding. |
| `Empty` | A static zero-sized rectangle at the origin. |

## See also

- [`VectorExtensions`](Vector2Extensions_Guide.md) — the equivalent small-utility treatment for `Vector2`.
