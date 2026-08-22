# Code style & conventions

Follow these rather than re-deriving a convention or introducing an inconsistent one.

## API naming

- `Fill<Shape>` / `Border<Shape>` / `Draw<Shape>` for every closed shape. `Fill` = solid, no outline. `Border` = outline only, grows **inward**, clamps gracefully instead of overflowing a small shape (2D) / defaults to `DefaultLineWidth` when no explicit thickness is given (3D). `Draw` = both (single-color overload + `fillColor, borderColor` overload).
- No `Ex`/`V`/etc. suffix for a parameter variant — it's an overload of the same name instead (e.g. a two-endpoint cylinder or a vector-size cube is `FillCylinder`/`FillCube` with a different signature, not `FillCylinderEx`/`FillCubeV`). A genuinely different shape family (`*Rounded`, `*Gradient`, `*Chamfer`) still gets its own suffix — see below.
- Rounded-corner variants: `*Rounded` suffix (`FillTriangleRounded`), not a separate verb.
- Multi-point primitives with no "inside" (`DrawTriangleFan`, `DrawTriangleStrip`) keep a single `Draw` name.
- Points (`DrawPixel`, `DrawPoint`) are untouched by Fill/Border/Draw — nothing to border.

## Units and parameters

- Rotation is **radians**, pivots on the shape's own center unless an explicit origin is given.
- Exception: `startAngle`/`endAngle` on `DrawCircleSector`/`DrawRing` are normalized **turns** `[0,1]`, documented at each call site.
- `thickness` defaults to `1f` on `Border*`/`Draw*`. `LineJoin.Miter` is the default join (cheapest); `Round`/`Bevel` cost more triangles.

## Structure

- `2D`/`3D` namespaces are independent of each other; `Core/` holds only code that's byte-identical between them (see DECISIONS.md). Don't duplicate something that belongs in Core, and don't force genuinely different code into a shared abstraction just to avoid duplication.
- No per-frame heap allocation on hot paths — `stackalloc` with a heap fallback (`MaxStackAllocElements = 4096`) for caller-controlled sizes.

## Comments and docs

- Never name "raylib" or other design inspirations in doc comments or `Guide/*.md` files. Internal implementation comments may reference an algorithm's origin if useful. **Exception, by explicit request:** the root `README.md`'s "Inspiration" section names Apos.Shapes/raylib/raylib-cs/MonoGame.Extended/Godot/Processing directly and explains what each influenced — that section is deliberate and should stay, don't scrub it to match this rule.
- Be concise. Describe behavior, not lineage.
- Comment the WHY (a non-obvious clamp, a hidden invariant, a workaround), not the WHAT.
- Don't reference "the fix" or a specific past conversation in code comments — that belongs in a changelog, not in code that outlives the context that produced it.

## Verification discipline

The most important rule — this codebase is built mostly by an AI with no live `GraphicsDevice` available by default:

- **Render genuinely new geometry before trusting it.** Reproduce the vertex/triangle math standalone (`System.Drawing` → PNG for 2D, a scratch MonoGame console project for 3D/real types) and look at the output. A live `GraphicsDevice` *is* available in this environment when needed (confirmed via a real Stopwatch benchmark) — use it for anything reasoning alone can't settle.
- Reasoning alone has missed real bugs here (tangent gaps, self-intersections, inverted normals invisible under flat shading, corner-overlap bugs). Don't skip verification for rotation, winding, per-corner radii, gradient offsets, or new trig.
- `dotnet build` the real project (not just the scratch one) before calling anything done.

## Scope discipline

- No physics resolution, ever — see PROJECT.md.
- Don't add a feature/abstraction beyond what was asked. Check ROADMAP.md before building something that "seems like it should exist" — it may already be deliberately deferred.
