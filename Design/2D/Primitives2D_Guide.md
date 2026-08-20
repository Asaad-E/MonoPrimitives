# Primitives2D — Usage Guide

A 2D immediate-mode primitive renderer for MonoGame. This guide explains every public method, grouped by shape, so you can go from zero to drawing without reading the source. For implementation details, edge cases and design rationale, the XML doc comments on each method in `Primitives2D.cs` go deeper than this guide does.

## Getting started

Everything lives on one class, `PrimitiveBatch`, in namespace `Primitives2D`. Create one per `GraphicsDevice` (reuse it — don't allocate a new one per frame) and wrap your drawing in `Begin`/`End`, same as `SpriteBatch`:

```csharp
using Primitives2D;

var batch = new PrimitiveBatch(GraphicsDevice);

// each frame:
batch.Begin(); // or Begin(transformMatrix: camera.GetViewMatrix()) for a scrolling/zooming camera
batch.FillCircle(new Vector2(400, 300), 50, Color.Red);
batch.DrawRectangle(100, 100, 200, 80, Color.White, Color.Black, thickness: 4);
batch.End();
```

`Begin` has two overloads: a simple one and one taking `blendState`/`depthStencilState`/`rasterizerState`/`effect` overrides, matching `SpriteBatch`'s own pattern. The camera transform is the **first** parameter (`transformMatrix`) so it reads naturally: `Begin(camera.GetViewMatrix())`.

`Flush()` forces any buffered geometry to draw immediately, without ending the batch — useful if you need to interleave primitive drawing with something else that also touches the `GraphicsDevice`.

## Core conventions (read this once)

**Fill / Border / Draw.** Every closed shape follows the same three-verb pattern:
- `Fill<Shape>` — solid color, no outline.
- `Border<Shape>` — outline only, growing **inward** from the shape's own edge. A thick border on a small shape never pokes outside it — it clamps and degrades gracefully to a full fill instead.
- `Draw<Shape>` — both together. One overload takes a single `Color` (fill and border match); another takes `fillColor, borderColor` separately.

Raw multi-point mesh primitives that don't have a natural "inside" (`DrawTriangleFan`, `DrawTriangleStrip`) keep a single `Draw` name instead, since there's nothing to separately border.

**Rotation.** Shapes that have a meaningful orientation (Rectangle, Triangle, Ellipse, Poly) take an optional trailing `float rotation = 0f` in **radians**, pivoting on the shape's own center unless you pass an explicit `Vector2? origin` (an offset from the shape's own position, not an absolute world coordinate). Shapes that look identical at any rotation (a solid or radially-graded Circle) don't have a rotation parameter at all — it would be a no-op.

One different unit convention to know: `startAngle`/`endAngle` on `DrawCircleSector`/`DrawCircleSectorLines`/`DrawRing` are normalized **turns** in `[0, 1]` (`1` = a full circle), not radians and not degrees. This is unrelated to the `rotation` parameter's radians and is easy to trip over the first time.

**Thickness and joins.** `Border*`/`Draw*` methods that can have sharp corners take `LineJoin join = LineJoin.Miter` (sharp, cheapest) or `LineJoin.Round`/`LineJoin.Bevel` (with an optional `float? jointRadius` to control how rounded/beveled). Miter is fast and exact for most UI-style shapes; Round costs more triangles but looks right on large, soft shapes.

**Gradients.** `Fill<Shape>Gradient` (fill only) and `Draw<Shape>Gradient` (fill + border) exist for most shapes. In `Draw*Gradient`, the gradient fill always stops exactly where the border begins — for a circle of `radius` with `thickness`, the fill runs from the center out to `radius - thickness`. Most gradient methods also take `innerOffset`/`outerOffset`: solid-color margins before/after the actual fade. If the two would overlap (their sum exceeds the available space), both are scaled down proportionally rather than producing an inverted or negative fade — this happens consistently across every gradient method in the file.

**Overload families.** Most shapes accept their position/size either as separate `float` components, a `Vector2 position, Vector2 size` pair, or a `Rectangle` — pick whichever is most convenient at the call site; they all forward to the same implementation. `RectangleRounded`/`RectangleChamfer`'s `Vector2, Vector2` overloads truncate to `Rectangle`'s integer fields (no float-precision path exists for those two families yet).

**`RectCorners`.** Used for both `FillRectangleRounded`'s `radius` and `FillRectangleChamfer`'s `chamfer` — one value per corner (`TopLeft`/`TopRight`/`BottomRight`/`BottomLeft`), with an implicit conversion from a single `float` when you want the same value on all four corners: `batch.FillRectangleRounded(rect, 12f, Color.White)`.

---

## Points and pixels

The one family that's deliberately **not** part of the Fill/Border/Draw system — these are raw, single-vertex primitives for when you need the cheapest possible dot.

| Method | What it does |
|---|---|
| `DrawPixel(Vector2 \| float x, y, Color)` | A single 1×1 pixel. |
| `DrawPixelFast(Vector2, Color)` | Same, on the fastest internal path — use this for large numbers of pixels (particle-style effects). |
| `DrawPoint(Vector2 position, float size, Color)` | A filled square dot of the given size — a bigger, more visible pixel. |

## Lines

Stroke primitives — no Fill/Border split, since a line has no "inside." `thickness` comes **before** `color` here, unlike shape primitives where `color` comes right after the geometry.

| Method | What it does |
|---|---|
| `DrawLine(start, end, color)` | A 1px line. |
| `DrawLine(start, end, thickness, color)` | A thick line. |
| `DrawLine(start, end, thickness, color, LineCap cap)` | Thick line with an explicit cap style (`Butt`, `Square`, `Round`). |
| `DrawLineStrip(points, color)` / `DrawLineStrip(points, thickness, color, join, cap, jointRadius)` | A connected polyline through any number of points, with proper mitered/rounded joints at each corner instead of independent overlapping segments. |
| `DrawLineDashed(start, end, dashLength, gapLength, [thickness,] color)` | A dashed line. |

## Triangles

| Method | What it does |
|---|---|
| `FillTriangle(v1, v2, v3, color, rotation, origin)` | Solid triangle. |
| `FillTriangle(v1, c1, v2, c2, v3, c3, rotation, origin)` | Solid triangle with an independent color per vertex (a 3-point gradient). |
| `BorderTriangle(v1, v2, v3, color, thickness, rotation, origin, join, jointRadius)` | Outline only, growing inward. |
| `DrawTriangle(v1, v2, v3, [fillColor, borderColor,] thickness, rotation, origin, join, jointRadius)` | Fill + border together. |
| `FillTriangleGradient(v1, v2, v3, from, to, rotation, origin)` | `v1` → `from`, `v2` and `v3` → `to`. |
| `DrawTriangleGradient(v1, v2, v3, from, to, borderColor, thickness, rotation, origin, join, jointRadius)` | Gradient fill + solid border, fill inset so it stops at the border. |
| `DrawTriangleFan(points, color)` | Raw triangle fan from the first point — for arbitrary fan-shaped meshes, not a "triangle" in the geometric sense. |
| `DrawTriangleStrip(points, color)` | Raw triangle strip. |

Rotation on a triangle is a bit redundant (you already control its shape via `v1`/`v2`/`v3`) but is included for consistency with every other shape family and because it's occasionally convenient to spin an already-built triangle around a pivot without recomputing its three points by hand.

## Rectangles

Three families, all sharing the same Fill/Border/Draw/Gradient pattern: **plain** (sharp corners), **Rounded** (per-corner radius), **Chamfer** (per-corner diagonal cut).

### Plain rectangles

| Method | What it does |
|---|---|
| `FillRectangle(x, y, w, h \| position, size \| rect, color, rotation, origin)` | Solid rectangle. |
| `FillRectangle(rect, topLeft, topRight, bottomRight, bottomLeft)` | Solid rectangle with an independent color per corner. No rotation — it's a fixed 4-corner-color mapping, not a shape rotation. |
| `BorderRectangle(..., color, thickness, rotation, origin, join, jointRadius)` | Outline only, inward. |
| `DrawRectangle(..., [fillColor, borderColor,] thickness, rotation, origin, join, jointRadius)` | Fill + border. |
| `FillRectangleGradient(..., from, to, horizontal, rotation, origin, innerOffset, outerOffset)` | Linear gradient fill, `horizontal` picks the fade axis. |
| `DrawRectangleGradient(..., from, to, horizontal, borderColor, thickness, rotation, origin, innerOffset, outerOffset)` | Gradient fill (inset by `thickness`) + solid border, both rotating together about the same pivot. |

### Rounded rectangles

Same verbs, with an extra `RectCorners radius` parameter right after the geometry:

`FillRectangleRounded`, `BorderRectangleRounded`, `DrawRectangleRounded`, `FillRectangleRoundedGradient`, `DrawRectangleRoundedGradient` — same signatures as their plain counterparts, plus `radius`. Each corner's radius is independently clamped to half the shorter side.

### Chamfered rectangles

Same again, with `RectCorners chamfer` instead of `radius` — a straight diagonal cut instead of a rounded arc:

`FillRectangleChamfer`, `BorderRectangleChamfer`, `DrawRectangleChamfer`, `FillRectangleChamferGradient`, `DrawRectangleChamferGradient`.

## Circles

Plain circles have **no rotation parameter anywhere** — a solid or radially-graded circle looks identical at any angle, so it would be a no-op.

| Method | What it does |
|---|---|
| `FillCircle(center, radius, color)` / `FillCircle(x, y, radius, color)` | Solid circle. |
| `FillCircle(center, radius, segments, color)` | Solid circle with an explicit segment count (normally chosen automatically from the radius). |
| `BorderCircle(center, radius, color, thickness)` | Outline only. |
| `DrawCircle(center, radius, [fillColor, borderColor,] thickness)` | Fill + border. |
| `FillCircleGradient(center, radius, inner, outer, innerOffset, outerOffset)` | Radial gradient, center → rim. |
| `DrawCircleGradient(center, radius, innerFill, outerFill, borderColor, thickness, innerOffset, outerOffset)` | Radial gradient + border. |
| `FillCircleGradientLinear(center, radius, from, to, horizontal, rotation, innerOffset, outerOffset)` | A **straight** (non-radial) gradient across the circle — `horizontal` picks the default axis, `rotation` turns it, so a top-to-bottom fade is just `horizontal: false`. |
| `DrawCircleGradientLinear(center, radius, from, to, borderColor, horizontal, thickness, rotation, innerOffset, outerOffset)` | Linear gradient + border. |

## Ellipses

Ellipses **do** take a `rotation` parameter (radians) on every method — unlike a circle, tilting an ellipse's H/V axes is the only way to orient it at all.

| Method | What it does |
|---|---|
| `FillEllipse(center, radiusH, radiusV, color, rotation)` | Solid ellipse. |
| `FillEllipse(center, radiusH, radiusV, segments, inner, outer, rotation)` | Radial-gradient fan version with an explicit segment count. |
| `BorderEllipse(center, radiusH, radiusV, color, thickness, rotation)` | Outline only. |
| `DrawEllipse(center, radiusH, radiusV, [fillColor, borderColor,] thickness, rotation)` | Fill + border. |
| `FillEllipseGradient(center, radiusH, radiusV, inner, outer, rotation, innerOffset, outerOffset)` | Radial gradient. |
| `DrawEllipseGradient(center, radiusH, radiusV, innerFill, outerFill, borderColor, thickness, rotation, innerOffset, outerOffset)` | Radial gradient + border. |

## Regular polygons (Poly — N equal sides)

For a triangle/square/hexagon/etc. defined by a center, side count and radius. `rotation` is radians; there's no separate `origin` parameter (a regular polygon always pivots on its own center).

| Method | What it does |
|---|---|
| `FillPoly(center, sides, radius, color, rotation)` | Solid N-gon. |
| `BorderPoly(center, sides, radius, color, thickness, rotation, join, jointRadius)` | Outline only. |
| `DrawPoly(center, sides, radius, [fillColor, borderColor,] thickness, rotation, join, jointRadius)` | Fill + border. |
| `FillPolyGradient(center, sides, radius, inner, outer, rotation, innerOffset, outerOffset)` | Radial gradient. |
| `DrawPolyGradient(center, sides, radius, innerFill, outerFill, borderColor, thickness, rotation, innerOffset, outerOffset)` | Radial gradient + border. |

## Arbitrary polygons (Polygon — your own point list)

Takes a `ReadOnlySpan<Vector2>` of points instead of center/sides/radius — draw whatever shape you've computed yourself. **Assumes a convex or star-shaped-from-the-first-point input** (fan-triangulated from `points[0]`); a general non-convex polygon can render wrong. No general ear-clipping triangulation is implemented — this is a documented scope limit, not a silent bug.

| Method | What it does |
|---|---|
| `FillPolygon(points, color)` | Solid fill, fan-triangulated from `points[0]`. |
| `BorderPolygon(points, color, thickness, join, jointRadius)` | Outline only, inward (correct for convex input; a reflex corner on a non-convex input isn't guaranteed to resolve exactly right). |
| `DrawPolygon(points, [fillColor, borderColor,] thickness, join, jointRadius)` | Fill + border. |
| `FillPolygonGradient(points, from, to)` | `points[0]` → `from`, every other point → `to`. |
| `DrawPolygonGradient(points, from, to, borderColor, thickness, join, jointRadius)` | Gradient fill (inset by `thickness`) + border. |
| `FillPolygonGradientTopBottom(bottomPoints, topPoints, bottomColor, topColor)` | A gradient **ribbon** between two aligned point lists — `bottomPoints[i]` connects to `topPoints[i]`, fading from `bottomColor` to `topColor`. Useful for a fade that follows an arbitrary profile (e.g. a terrain silhouette) instead of a straight axis. If the two lists differ in length, the shorter one wins. |

## Sectors and rings

Special-purpose shapes that don't fit the Fill/Border/Draw mold cleanly, so they keep their own simpler names. Remember: `startAngle`/`endAngle` here are normalized **turns** `[0, 1]`, not radians.

| Method | What it does |
|---|---|
| `DrawCircleSector(center, radius, startAngle, endAngle, [segments,] color)` | A filled pie slice. |
| `DrawCircleSectorLines(center, radius, startAngle, endAngle, thickness, color)` | The pie slice's outline (arc + the two straight radii). |
| `DrawRing(center, innerRadius, outerRadius, [startAngle, endAngle, segments,] color)` | A filled annulus (donut), or a partial donut wedge if you pass an angle range. |

## Splines

Smooth curves through a set of control points, drawn as a single shared-vertex strip (proper joins, no seams between segments).

| Method | What it does |
|---|---|
| `DrawSplineLinear(points, thickness, color)` | Straight segments through every point (equivalent to `DrawLineStrip` but named for symmetry with the other splines). |
| `DrawSplineCatmullRom(points, thickness, color, segmentsPerPiece)` | A smooth curve that passes through every point. Needs at least 4 points. |
| `DrawSplineBezierCubic(points, thickness, color, segmentsPerPiece)` | A cubic Bézier spline: `[p1, c2, c3, p4, c5, c6, p7, ...]` — needs `3n + 1` points for `n` segments. |
| `GetSplinePointCatmullRom(p1, p2, p3, p4, t)` (static) | The raw math behind `DrawSplineCatmullRom`, if you need a point on the curve without drawing it. |
| `GetSplinePointBezierCubic(p1, c2, c3, p4, t)` (static) | Same, for the cubic Bézier formula. |
| `GetSplinePointBezierQuad(p1, c2, p3, t)` (static) | Same, for a quadratic (single control point) Bézier. |

## Text

A separate, standalone file (`DebugFont5x7.cs`) — a 5×7 dot-matrix pixel font drawn entirely with `FillRectangle` calls, no textures or `SpriteFont`. Covers full ASCII (32–126) plus Spanish characters (`ñ Ñ á é í ó ú Á É Í Ó Ú ü Ü ¿ ¡`). Intended for debug/test text (HUD counters, labels), not production typography — lowercase descenders are compressed to fit the same cell as everything else, and unknown characters draw as a hollow box instead of vanishing silently.

```csharp
using Primitives2D; // DrawString/MeasureText are extension methods on PrimitiveBatch

batch.DrawString("FPS: 60", new Vector2(10, 10), pixelSize: 4, Color.White);
```

| Member | What it does |
|---|---|
| `DrawString(this PrimitiveBatch, text, position, pixelSize, color, glyphSpacing = 1f, lineSpacing = 2f)` | Draws text starting at `position` (top-left of the first character). `'\n'` starts a new line. Named to match `SpriteBatch.DrawString`. |
| `MeasureText(text, pixelSize, glyphSpacing, lineSpacing)` | Total size in pixels the text would occupy — for centering/layout before drawing. |
| `DebugFont5x7.SpaceWidthScale` (static field, default `0.3f`) | How wide a space character is, as a fraction of a normal glyph's width. Change it once globally if you want tighter/looser spacing. |
| `DebugFont5x7.GlyphWidth` / `GlyphHeight` (constants, `5`/`7`) | The font's cell size in pixels, before `pixelSize` scaling. |

## A note on gradients in general

If you only remember one thing about the gradient methods: **`Draw*Gradient` always insets the gradient so it stops exactly where the border starts**, and `innerOffset`/`outerOffset` are measured from *that* inset boundary, not from the shape's outer edge. For example, `DrawCircleGradient(center, radius: 100, ..., thickness: 10, outerOffset: 20)` — the gradient's own effective radius is `90` (`100 - thickness`), and its outer solid-color margin starts at `70` (`90 - outerOffset`). This composition rule is identical across every shape's gradient — circle, ellipse, rectangle (all three corner styles), poly, polygon.

## Performance notes

- The batch buffers vertices/indices internally and only submits a draw call when the buffer fills up or `End()`/`Flush()` is called — draw as much as you want per frame without worrying about draw-call count yourself.
- Segment counts for circles/arcs are chosen automatically from the radius (more segments for bigger shapes, capped at `MaxCircleSegments`) — you don't need to hand-tune smoothness in the common case.
- `Border*`/`Draw*` with `LineJoin.Miter` (the default) is the cheapest join style. `Round`/`Bevel` cost more triangles per corner and are worth reaching for on large, soft-looking shapes, not small UI chrome.
- Prefer the segment-count overloads (e.g. `FillCircle(center, radius, segments, color)`) only when you've profiled and actually need to override the automatic choice — the default heuristic already balances smoothness against triangle count for you.
