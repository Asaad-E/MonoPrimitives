# Cooldown — Guide

`Cooldown` (namespace `MonoPrimitives`, file [`src/Core/Cooldown.cs`](../src/Core/Cooldown.cs)) is a simple countdown: `Update` it down every frame, then check `IsReady` — or call `TryUse()` to check-and-restart in one step. An attack cooldown, a spawn timer, a debounce on repeated input.

## Quick start

```csharp
using MonoPrimitives;

private Cooldown _fireCooldown = new(duration: 0.25f);

protected override void Update(GameTime gameTime)
{
    _fireCooldown.Update(gameTime);
    if (_input.IsKeyDown(Keys.Space) && _fireCooldown.TryUse())
        Fire();
}
```

## API

| Member | What it does |
|---|---|
| `new Cooldown(duration)` | A cooldown of `duration` seconds, starting already `IsReady` — usable right away the first time. |
| `Duration` | The full duration `Reset()`/a successful `TryUse()` restarts the countdown to. Settable. |
| `Update(deltaSeconds)` / `Update(GameTime)` | Counts the cooldown down. Call once per frame. |
| `IsReady` | True once the countdown has reached zero. |
| `Remaining` | Seconds left before `IsReady` — never negative. |
| `Progress` | `0` right after use, `1` once `IsReady` — a cooldown bar's fill amount. |
| `TryUse()` | If `IsReady`, restarts the countdown and returns `true`; otherwise does nothing and returns `false` — "can I use this, and if so start the cooldown" in one call. |
| `Reset()` | Restarts the countdown at `Duration` — forces *not* ready. |
| `ResetReady()` | Forces `IsReady` immediately, skipping whatever was left — e.g. to undo a `Reset()`. |

## A struct, not a class — and the one gotcha that comes with it

`Cooldown` is a plain `struct`, not a class. It's meant to sit as a field on potentially hundreds of entities (an enemy's attack cooldown, a spawner's timer), and as a struct it costs nothing beyond the entity's own memory — no heap allocation per instance.

That means the usual mutable-struct rule applies: **store it as a field, not a local you reassign each frame.** `cooldown.Update(dt)` mutates it correctly when it's a field on `this`. It won't work on a copy pulled into a local variable, an array element read by value, or a `foreach` variable — those are all copies, and the update just gets thrown away. Nothing `Cooldown`-specific here, but it's worth calling out: a cooldown that seems to tick down "for no reason" is usually exactly this.
