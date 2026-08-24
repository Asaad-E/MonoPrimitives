# ScreenshotUtil — Guide

`ScreenshotUtil` (namespace `MonoPrimitives`, file [`src/Core/ScreenshotUtil.cs`](../src/Core/ScreenshotUtil.cs)) saves the current back buffer to an image file. MonoGame's own `Texture2D.SaveAsPng`/`SaveAsJpeg` only save a texture you already own — getting the actual on-screen frame into one is left entirely to you; this is that missing step.

## Quick start

```csharp
using MonoPrimitives;

protected override void Update(GameTime gameTime)
{
    if (_input.IsKeyPressed(Keys.F12))
        _takeScreenshotNextFrame = true;

    base.Update(gameTime);
}

protected override void Draw(GameTime gameTime)
{
    // ... your normal drawing ...

    if (_takeScreenshotNextFrame)
    {
        ScreenshotUtil.Capture(GraphicsDevice, $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        _takeScreenshotNextFrame = false;
    }

    base.Draw(gameTime);
}
```

Call it after your scene is fully drawn (typically near the end of `Draw`, before `base.Draw`) — it captures whatever the back buffer currently holds, so anything drawn after the call won't be in the saved image.

## API

| Member | What it does |
|---|---|
| `Capture(device, filePath)` | Captures the current back buffer and saves it to `filePath`. Format is inferred from the extension — `.png`, or `.jpg`/`.jpeg` — anything else throws `ArgumentException` rather than silently guessing a format. Creates the destination directory if it doesn't exist yet. Throws on a null `device` or a null/empty `filePath`. |

That's the whole surface — one method, no state to manage.

## Notes

- PNG is lossless; JPEG is not — expect small color shifts on a `.jpg` capture, same as any JPEG.
- This reads `GraphicsDevice.PresentationParameters.BackBufferWidth`/`Height` and `GetBackBufferData`, so the saved image is exactly the window's current resolution — no separate size parameter.
