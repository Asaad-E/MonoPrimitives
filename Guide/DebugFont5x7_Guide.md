# DebugFont5x7 — Guide

`DebugFont5x7` (namespace `MonoPrimitives.Primitives2D`, file [`src/2D/DebugFont5x7.cs`](../src/2D/DebugFont5x7.cs)) is a standalone 5×7 dot-matrix pixel font drawn entirely with `FillRectangle` — no textures, no `SpriteFont`. The actual glyph bitmap data lives separately in [`FontGlyphs5x7`](../src/Core/FontGlyphs5x7.cs) (namespace `MonoPrimitives`), shared with the 3D library's own billboard text renderer — only the drawing differs (flat rectangles in 2D screen space, camera-facing quads in 3D world space).

Covers full basic ASCII (32–126) plus Spanish characters (`ñ Ñ á é í ó ú Á É Í Ó Ú ü Ü ¿ ¡`). Intended for debug/test text — HUD counters, labels, on-screen values — not production typography.

## Quick start

```csharp
using MonoPrimitives.Primitives2D; // DrawString/MeasureText are extension methods on PrimitiveBatch

batch.Begin();
batch.DrawString("FPS: 60", new Vector2(10, 10), pixelSize: 4, Color.White);
batch.End();
```

## API

| Member | What it does |
|---|---|
| `DrawString(this PrimitiveBatch, text, position, pixelSize, color, glyphSpacing = 1f, lineSpacing = 2f)` | Draws text starting at `position` (top-left of the first character), one `FillRectangle` per "on" pixel. `'\n'` starts a new line. Named to match `SpriteBatch.DrawString`. |
| `MeasureText(text, pixelSize, glyphSpacing, lineSpacing)` | Total `(Width, Height)` the text would occupy — for centering/layout before drawing. |
| `SpaceWidthScale` (static, default `0.3f`) | How wide a space character is, as a fraction of a normal glyph's width. Set once to change spacing globally — shared with the 3D renderer. |
| `GlyphWidth` / `GlyphHeight` (constants, `5`/`7`) | The font's cell size in pixels, before `pixelSize` scaling. |

A character with no assigned glyph draws as a hollow box instead of silently vanishing — you'll always notice a missing character rather than lose it in blank space.

## Why it's not production typography

- **No true descenders.** `g`, `j`, `p`, `q`, `y` are compressed to fit the same 7-row cell as every other glyph, rather than hanging below the baseline the way real typography does.
- **A strict x-height convention every non-ascender lowercase letter follows exactly**: rows 0-1 are always blank, the glyph body occupies rows 2-6 (5 rows). Ascenders (`b d f h k l t`) use the full 0-6 range instead. `i`/`j` are a dot at row 0, a blank row 1, then a rows-2-6 body — the same baseline as everything else, just with a floating dot on top. This convention is why every letter reads as sitting on the same baseline; a letter that quietly breaks it (one row too tall, or starting one row too high) reads as visually "off" even though nothing else about its shape is wrong — which is exactly what happened to `a`/`e`/`g`/`u` (see below).

## Bugs found and fixed this session

Both were found by rendering the alphabet at real reading size and looking closely, not by reading the bitmap literals — a 6-row-tall letter or a one-column notch doesn't jump out of a `0b01110` the way it does on screen.

- **`a`/`e`/`g`/`u` (and accented `á`/`é`) were one row taller than every other x-height letter.** Each had a single literally-duplicated row in its own bitmap (e.g. `u` repeated its side-row four times instead of three) — almost certainly a copy-paste slip when those glyphs were first authored. Fixed by dropping the duplicate row, the same "compress by one row" technique already used correctly elsewhere in this font (the accented uppercase letters).
- **Lowercase `h`'s crossbar stopped one column short of its right leg** (`0b11110` instead of `0b11111`), so the leg only touched the arch diagonally at a corner instead of sharing an edge — read as a floating, disconnected leg. Fixed by widening the crossbar to full width. `n` (and `ñ`, which reuses `n`'s body) has the same underlying pattern and was deliberately left as-is — not reported, and changing it wasn't confirmed.

## Testing

[`tests/MonoPrimitives.Tests/FontGlyphs5x7Tests.cs`](../tests/MonoPrimitives.Tests/FontGlyphs5x7Tests.cs) locks in the row-span convention above as a permanent check per letter class (x-height, ascender, dotted, uppercase/digit), plus the `h` crossbar fix, the fallback hollow-box glyph, `AdvanceFor`'s scaling, and `MeasureText`'s multi-line behavior — this is exactly the check that would have caught the `a`/`e`/`g`/`u` bug immediately, instead of needing an actual rendered screenshot to notice. Run with:

```bash
dotnet run --project tests/MonoPrimitives.Tests/MonoPrimitives.Tests.csproj
```

## See also

- [`Design/DECISIONS.md`](../Design/DECISIONS.md) — both bug write-ups in full, including the visual renders that caught them.
- `examples/test/TextReadabilityTest` — every printable glyph plus a pangram at reading size, pannable/zoomable via `Camera2D`.
