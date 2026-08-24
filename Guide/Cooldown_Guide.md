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
| `new Cooldown(duration)` | A cooldown of `duration` seconds, starting already `IsReady` — the usual expectation for something usable right away the first time. |
| `Duration` | The full duration `Reset()`/a successful `TryUse()` restarts the countdown to. Settable. |
| `Update(deltaSeconds)` / `Update(GameTime)` | Counts the cooldown down. Call once per frame. |
| `IsReady` | True once the countdown has reached zero. |
| `Remaining` | Seconds left before `IsReady` — never negative. |
| `Progress` | `0` right after use, `1` once `IsReady` — a cooldown bar's fill amount. |
| `TryUse()` | If `IsReady`, restarts the countdown and returns `true`; otherwise does nothing and returns `false` — "can I use this, and if so start the cooldown" in one call. |
| `Reset()` | Restarts the countdown at `Duration` — forces *not* ready. |
| `ResetReady()` | Forces `IsReady` immediately, skipping whatever was left — e.g. to undo a `Reset()`. |

## A struct, not a class — and the one thing to know about that

`Cooldown` is a plain `struct`, not a class, on purpose: it's meant to be a field on potentially hundreds of entities in a simulation (an enemy's attack cooldown, a spawner's timer), and as a struct it costs nothing beyond the entity's own memory — no separate heap allocation per instance the way a class field would need.

The one thing this asks of you in return is the standard mutable-struct rule: **store it as a field, not a local you reassign each frame.** `cooldown.Update(dt)` correctly mutates a `Cooldown` stored as a field on `this`. It does *not* correctly mutate a copy pulled out into a local variable, an array element read by value into a local, or a `foreach` loop variable — those all operate on a copy, and the mutation is lost the moment the copy goes out of scope. This is the same caveat that applies to any mutable struct in C# (not specific to `Cooldown`), just worth naming explicitly since a `Cooldown` ticking down "for no reason" is exactly the symptom this mistake produces.

## See also

- [`Guide/ObjectPool_Guide.md`](ObjectPool_Guide.md) — pairs naturally with a `Cooldown` for a spawner (`if (cooldown.TryUse()) pool.Get();`).
- [`Guide/FrameLimiter_Guide.md`](FrameLimiter_Guide.md) — a different timing concern: pacing the whole game loop, not counting down one specific thing.
