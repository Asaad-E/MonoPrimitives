# Code style & conventions

Follow these rather than re-deriving a convention or introducing an inconsistent one.

## API naming

- `Fill<Shape>` / `Border<Shape>` / `Draw<Shape>` for every closed shape. `Fill` = solid, no outline. `Border` = outline only, grows **inward**, clamps gracefully instead of overflowing a small shape (2D) / defaults to `DefaultLineThickness` when no explicit thickness is given (3D). `Draw` = both, ONE overload: `Draw<Shape>(..., Color fillColor, Color? borderColor = null, ...)` — omit `borderColor` for the same color on both. Never a separate single-color overload alongside a `fillColor, borderColor` one; that duplication was audited out (see DECISIONS.md) in favor of this single signature, matching Apos.Shapes' own approach (a `Color` converts implicitly wherever it takes a richer fill/border type). Position/size still gets a real overload per encoding where more than one makes sense (`Rectangle`, `Vector2 position, Vector2 size`, raw floats) — that duplication is real (different types, not just a color default) and stays.
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
- `Nullable` is enabled project-wide. A field always assigned in `Begin()` (never the constructor) gets `= null!`, not a nullable type — reserve an actual `?` for a field that's genuinely sometimes null at runtime (see DECISIONS.md's Testing & tooling section).

## Comments and docs

**`///` XML doc comments are the API contract shown in IntelliSense/hover tooltips — nothing else belongs there.** `<summary>` is one sentence (rarely two): what it does / what it returns. Add `<remarks>` only for a genuine correctness gotcha — something that would break or surprise a caller if left unsaid (a hidden invariant, a thread-safety constraint, a non-obvious clamp). One or two sentences, not a paragraph.

**Splitting a bloated `<summary>` into `<summary>` + `<remarks>` is NOT a fix — most IDEs render both concatenated in the same tooltip, so total visible content is unchanged.** The actual fix is deleting content, not relocating it. Before writing any sentence into a `///` comment, ask: would omitting this cause someone to misuse the API or misunderstand a hard constraint? If no — it doesn't belong in `///` at all, in *either* tag.

**Nor is "the block is already short" a fix.** Apply that question to every clause independently, not to the block's overall length — a single compact sentence can still smuggle in a comparison to a sibling class/method, an exhaustive member list, or a usage-example snippet, and reads as "fine" if you only check line count. Caught doing exactly this in the same session the rule above was written: `RectangleF`'s remarks packed in a full list of mirrored members (`Left`/`Right`/`Top`/`Bottom`/`Contains`/`Intersects`/`Inflate`/`Union`/`Intersect`) as one "sentence"; `Easing`'s remarks carried both a comparison to `Camera2D`/`Camera3D`'s `SmoothDamp` and a code usage example; `ColorUtil.Contrast`'s remarks opened with "Unlike `Saturate`/`Desaturate`, this operates directly on RGB" before its one legitimate correctness note. All three were waved through as "short enough, looks fine" without dissecting each clause. Grade every sentence in a `///` block on content, never on its line count.

Rationale, benchmarks, comparisons to other classes/libraries, "why we built it this way," implementation mechanism (e.g. *how* a fast path works internally) — none of that belongs in a `///` comment. It goes in exactly one of these, never duplicated across more than one:
- A plain `//` comment near the relevant code, if it's genuinely useful to someone reading that specific implementation.
- `Design/DECISIONS.md`, if it's a design rationale worth recording.
- `Guide/*.md`, if it's usage guidance a caller would look up deliberately.

This was violated pervasively across the codebase (checked directly against IDE tooltip renders, not assumed) and is a standing priority to keep fixed, not a one-time cleanup — check any `///` comment you write or touch against this bar, every time.

- Never name "raylib" or other design inspirations in doc comments or `Guide/*.md` files. Internal implementation comments may reference an algorithm's origin if useful. **Exception, by explicit request:** the root `README.md`'s "Inspiration" section names Apos.Shapes/raylib/raylib-cs/MonoGame.Extended/Godot/Processing directly and explains what each influenced — that section is deliberate and should stay, don't scrub it to match this rule.
- Comment the WHY (a non-obvious clamp, a hidden invariant, a workaround), not the WHAT — applies to both `///` and `//` comments.
- Don't reference "the fix" or a specific past conversation in code comments — that belongs in a changelog, not in code that outlives the context that produced it.

## Verification discipline

The most important rule — this codebase is built mostly by an AI with no live `GraphicsDevice` available by default:

- **Render genuinely new geometry before trusting it.** Reproduce the vertex/triangle math standalone (`System.Drawing` → PNG for 2D, a scratch MonoGame console project for 3D/real types) and look at the output. A live `GraphicsDevice` *is* available in this environment when needed (confirmed via a real Stopwatch benchmark) — use it for anything reasoning alone can't settle.
- Reasoning alone has missed real bugs here (tangent gaps, self-intersections, inverted normals invisible under flat shading, corner-overlap bugs). Don't skip verification for rotation, winding, per-corner radii, gradient offsets, or new trig.
- `dotnet build` the real project (not just the scratch one) before calling anything done.

## Scope discipline

- No physics resolution, ever — see PROJECT.md.
- Don't add a feature/abstraction beyond what was asked. Check ROADMAP.md before building something that "seems like it should exist" — it may already be deliberately deferred.
