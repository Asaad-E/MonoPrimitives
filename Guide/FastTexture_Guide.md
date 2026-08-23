# FastTexture — Guide

`FastTexture` (namespace `MonoPrimitives`, file [`src/Core/FastTexture.cs`](../src/Core/FastTexture.cs)) wraps a `Texture2D`/`RenderTarget2D` and uploads pixel data via a direct `glTexSubImage2D` call instead of `Texture2D.SetData` — measured ~2.5-2.7x faster for a full-texture update once per real frame (2500x2500, DesktopGL). Desktop GL only (Windows/Linux/macOS DesktopGL backend); falls back to plain `SetData` automatically everywhere else, so it's always safe to use.

## Quick start

```csharp
using MonoPrimitives;

private FastTexture _texture;
private Color[] _pixels;

protected override void LoadContent()
{
    _texture = new FastTexture(GraphicsDevice, width: 512, height: 512);
    _pixels = new Color[512 * 512];
    Console.WriteLine(_texture.Diagnostics); // which path is active, and why, if it fell back
}

protected override void Draw(GameTime gameTime)
{
    // ... fill _pixels ...
    _texture.Update(_pixels);

    _spriteBatch.Begin();
    _spriteBatch.Draw(_texture.Texture, position, Color.White);
    _spriteBatch.End();
}
```

## API

| Member | What it does |
|---|---|
| `new FastTexture(device, width, height, format = SurfaceFormat.Color)` | Creates a new non-mipmapped texture and wraps it. |
| `new FastTexture(device, texture, ownsTexture = false)` | Wraps an existing `Texture2D` (including a `RenderTarget2D`). `ownsTexture: true` disposes the wrapped texture too when this wrapper is disposed. |
| `Texture` | The underlying `Texture2D` — draw with this as normal. |
| `Width` / `Height` | Shorthand for `Texture.Width`/`Height`. |
| `IsRawUploadAvailable` | `true` if the fast path was established. `false` means every `Update` transparently uses `SetData` instead — correct, just not faster. |
| `Diagnostics` | Human-readable explanation of which path is active, and exactly why if the fast path was unavailable. Log this once at startup. |
| `Update(data)` / `Update(ReadOnlySpan<T> data)` | Uploads a full-texture pixel buffer. Must be exactly `Width * Height` elements. |
| `Update(rect, data)` | Uploads a sub-rectangle. `data` must be tightly packed for that rectangle (row-major, no padding). |
| `Update(IntPtr data, sizeInBytes)` | Uploads from an unmanaged pointer — for data you already hold natively, no managed copy in between. |
| `AutoInvalidateDeviceCache` (default `true`) | See "The texture-slot cache" below. |

## Only mip level 0, and RenderTarget2D caveats

Only the base mip level is written — create textures for this with `mipMap: false`, or expect stale lower mips on one that has them (`Diagnostics` calls this out). Wrapping a `RenderTarget2D` is supported, but never call `Update` while it's the active render target — call `GraphicsDevice.SetRenderTarget(null)` first.

## Threading

Call `Update` only from the thread that owns the GL context — `Update`/`Draw`, never a background task.

## The texture-slot cache

`GraphicsDevice` keeps a per-slot cache of which texture it believes is bound, skipping a redundant GL bind when it thinks nothing changed. The raw upload here bypasses that cache entirely, so `GraphicsDevice` has no way to know the binding moved underneath it. `AutoInvalidateDeviceCache` (on by default) clears the cache after every raw upload so the next draw re-binds for real; it's cheap, and safe to leave on. Turn it off only if you've measured your own draw path always re-binds anyway.

## See also

- [`Design/DECISIONS.md`](../Design/DECISIONS.md) — the benchmark-methodology story: an early synthetic benchmark found the raw path *slower*, which turned out to be an artifact of measuring it with no real per-frame boundary between calls, not a real result.
