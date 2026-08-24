# DebugFont5x7 — Guide

`DebugFont5x7` (namespace `MonoPrimitives.Primitives2D`, file [`src/2D/DebugFont5x7.cs`](../src/2D/DebugFont5x7.cs)) is a standalone 5×7 dot-matrix pixel font drawn entirely with `FillRectangle` — no textures, no `SpriteFont`. The actual glyph bitmap data lives separately in [`FontGlyphs5x7`](../src/Core/FontGlyphs5x7.cs) (namespace `MonoPrimitives`), shared with the 3D library's own billboard text renderer — only the drawing differs (flat rectangles in 2D screen space, camera-facing quads in 3D world space).

Covers full basic ASCII (32–126) plus Spanish characters (`ñ Ñ á é í ó ú Á É Í Ó Ú ü Ü ¿ ¡`). Intended for debug/test text — HUD counters, labels, on-screen values — not production typography.

## Quick start

```csharp
using MonoPrimitives.Primitives2D; // DrawString/MeasureText are extension methods on Primitive2DBatch

batch.Begin();
batch.DrawString("FPS: 60", new Vector2(10, 10), pixelSize: 4, Color.White);
batch.End();
```

## API

| Member | What it does |
|---|---|
| `DrawString(this Primitive2DBatch, text, position, pixelSize, color, glyphSpacing = 1f, lineSpacing = 2f, maxWidth = 0f)` | Draws text starting at `position` (top-left of the first character), one `FillRectangle` per "on" pixel. `'\n'` starts a new line. Named to match `SpriteBatch.DrawString`. `maxWidth` greater than 0 word-wraps first — see "Word-wrap" below. |
| `MeasureText(text, pixelSize, glyphSpacing, lineSpacing)` | Total `(Width, Height)` the text would occupy — for centering/layout before drawing. Measures `text` exactly as given; pass already-wrapped text (see below) to measure the wrapped size. |
| `SpaceWidthScale` (static, default `0.3f`) | How wide a space character is, as a fraction of a normal glyph's width. Set once to change spacing globally — shared with the 3D renderer. |
| `GlyphWidth` / `GlyphHeight` (constants, `5`/`7`) | The font's cell size in pixels, before `pixelSize` scaling. |
| `FontGlyphs5x7.GetGlyph(char)` | The raw 7-row bitmap (`byte[]`) for a character, or a hollow-box fallback glyph if it has none — for building your own custom renderer on top of the same glyph data instead of `DrawString`. |
| `FontGlyphs5x7.AdvanceFor(char, pixelSize, glyphSpacing)` | The horizontal advance (already scaled by `pixelSize`) one character takes up — what `DrawString`/`DrawString3D` use internally to move the cursor; useful for your own custom layout (e.g. computing where a specific character in a string will land). |
| `FontGlyphs5x7.WrapText(text, maxWidth, pixelSize, glyphSpacing = 1f)` | Inserts `'\n'` at word boundaries so no resulting line exceeds `maxWidth`. What `DrawString`/`DrawString3D`'s own `maxWidth` parameter calls internally — call it directly if you want the wrapped string itself (to measure it, log it, cache it, etc.) instead of just drawing it. |

A character with no assigned glyph draws as a hollow box instead of silently vanishing — you'll always notice a missing character rather than lose it in blank space.

## Word-wrap

```csharp
// Simplest: let DrawString wrap for you.
batch.DrawString(longDescription, new Vector2(20, 20), pixelSize: 2, Color.White, maxWidth: 300f);

// Or wrap once yourself (e.g. to measure the wrapped box before drawing anything):
string wrapped = FontGlyphs5x7.WrapText(longDescription, maxWidth: 300f, pixelSize: 2, glyphSpacing: 1f);
Vector2 size = DebugFont5x7.MeasureText(wrapped, pixelSize: 2, glyphSpacing: 1f, lineSpacing: 2f);
batch.DrawString(wrapped, panelCenter - size / 2f, pixelSize: 2, Color.White); // now centered as a block
```

**Use the exact same `pixelSize`/`glyphSpacing` for wrapping and drawing.** `WrapText` decides where to break based on those two values — passing different ones to `DrawString`'s own `maxWidth` than you'd pass to a manual `WrapText` call (or vice versa) measures against the wrong glyph size and the wrap will be off. This is why `DrawString(..., maxWidth:)` takes the same `pixelSize`/`glyphSpacing` it already needs anyway, rather than a separate pre-wrapped string — one call, nothing to keep in sync.

**A single word wider than `maxWidth` gets hard-broken mid-word**, not dropped or left overflowing — there's no earlier valid space to break at. Keep `maxWidth` comfortably wider than your longest expected single word (a stat name, a long identifier) if you want to guarantee word-wrap never falls back to a mid-word break.

**Existing `'\n'` in your text are forced breaks, wrapped independently per resulting paragraph** — a manual line break you already have stays exactly where you put it; `WrapText` only adds *more* breaks within each of your existing lines if they're too long, never removes or merges your own.

`maxWidth = 0` (the default) means "don't wrap" — `DrawString`/`DrawString3D` draw exactly the string you gave them, same as before this parameter existed. 3D's `maxWidth` is in the same world-space unit as `pixelSize`, not screen pixels.

## 3D: `DrawString3D` / `GetBillboardAxes`

3D's own `DebugFont5x7.cs` ([`src/3D/DebugFont5x7.cs`](../src/3D/DebugFont5x7.cs), methods on `Primitive3DBatch`) draws the exact same `FontGlyphs5x7` bitmap data as camera-facing quads in world space instead of flat screen-space rectangles:

```csharp
using MonoPrimitives.Primitives3D;

batch.Begin(camera);
batch.DrawString3D("HP: 100", agentPosition + Vector3.Up, pixelSize: 0.1f, Color.White); // billboarded
batch.End();
```

| Member | What it does |
|---|---|
| `DrawString3D(text, position, pixelSize, color, glyphSpacing = 1f, lineSpacing = 2f, maxWidth = 0f)` | Draws billboarded (camera-facing) text at `position`. `pixelSize` is a WORLD-space size (a glyph is `5*pixelSize` × `7*pixelSize` world units), not screen pixels — re-scale per frame by camera distance if you want constant screen size. Never affected by lighting. `maxWidth` (same world-space unit as `pixelSize`) word-wraps first, same as 2D's `DrawString`. |
| `DrawString3D(text, position, right, up, pixelSize, color, ..., maxWidth = 0f)` | Same, with a caller-supplied fixed `right`/`up` basis instead of billboarding — for text painted onto a surface or a fixed HUD-in-world panel. `right`/`up` need not be unit length or orthogonal; a scaled/sheared basis skews the text like any other quad would. |
| `MeasureText3D(text, pixelSize, glyphSpacing, lineSpacing)` | Same as 2D's `MeasureText`, in world units — for centering along the billboard's own axes. |
| `GetBillboardAxes(position, out right, out up)` | The `right`/`up` basis `DrawString3D` bills off of — exposed separately for your own billboarded quads (particles, sprites) that want the same behavior. |

**Billboarding is cylindrical** (stays upright relative to world `+Y`, rotating only to face the camera around that axis) — the usual choice for labels, matching Godot's `Label3D` default billboard mode and Unity's `TextMesh`. It falls back to a full camera-facing basis (`Primitive3DBatch.BuildBasis`, an orthonormal-basis-from-one-vector construction) only when looking almost straight up or down world `+Y`, where the cylindrical axis is undefined. The resulting `right`/`up` always match the camera's own on-screen right/up, so text never reads mirrored or upside-down regardless of which side the camera approaches from.

A full spherical billboard (tilting with camera pitch, not just yaw) was deliberately not added as an alternative mode — unusual for readable text specifically (most engines reserve that for particles/sprites, not labels), and not something any peer's text-billboard API defaults to either.

## Why it's not production typography

- **No true descenders.** `g`, `j`, `p`, `q`, `y` are compressed to fit the same 7-row cell as every other glyph, rather than hanging below the baseline the way real typography does.
- **A strict x-height convention every non-ascender lowercase letter follows exactly**: rows 0-1 are always blank, the glyph body occupies rows 2-6 (5 rows). Ascenders (`b d f h k l t`) use the full 0-6 range instead. `i`/`j` are a dot at row 0, a blank row 1, then a rows-2-6 body — the same baseline as everything else, just with a floating dot on top. This convention is why every letter reads as sitting on the same baseline; a letter that quietly breaks it (one row too tall, or starting one row too high) reads as visually "off" even though nothing else about its shape is wrong — which is exactly what happened to `a`/`e`/`g`/`u` (see below).

## Testing

[`tests/MonoPrimitives.Tests/FontGlyphs5x7Tests.cs`](../tests/MonoPrimitives.Tests/FontGlyphs5x7Tests.cs) locks in the row-span convention above as a permanent check per letter class (x-height, ascender, dotted, uppercase/digit), plus the fallback hollow-box glyph, `AdvanceFor`'s scaling, `MeasureText`'s multi-line behavior, and `WrapText` (every wrapped line actually fits within `maxWidth`, breaks land on word boundaries not mid-word, a too-long single word hard-breaks without losing/adding characters, and existing `'\n'` survive as independent forced breaks). [`ShapeTests2D.cs`](../tests/MonoPrimitives.Tests/ShapeTests2D.cs) renders `DrawString(..., maxWidth:)` to confirm no glyph pixel actually crosses the boundary, and that `maxWidth = 0` really does mean "off." [`tests/MonoPrimitives.Tests/DebugFont3DTests.cs`](../tests/MonoPrimitives.Tests/DebugFont3DTests.cs) covers 3D's own surface: `GetBillboardAxes`'s orthonormality and match against the camera's own view-space axes, the straight-up/down pole fallback, `DrawString3D` emitting geometry only for non-space glyphs, `MeasureText3D` agreeing with `FontGlyphs5x7.MeasureText`, and the not-begun/null/empty/zero-size no-op paths. Run with:

```bash
dotnet run --project tests/MonoPrimitives.Tests/MonoPrimitives.Tests.csproj
```

## See also

- [`Design/DECISIONS.md`](../Design/DECISIONS.md) — both 2D bug write-ups in full (including the visual renders that caught them), and the 3D billboard-axis verification.
- `examples/test/TextReadabilityTest` — every printable glyph plus a pangram at reading size, pannable/zoomable via `Camera2D`.
- `samples/MonoPrimitives.Sample/Gallery3D.cs` — `DrawString3D` used for every shape's caption in the 3D gallery.
- [`Guide/Camera3D_Guide.md`](Camera3D_Guide.md) — the camera `GetBillboardAxes` faces.
