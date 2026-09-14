# DebugTimer — Guide

`DebugTimer` (namespace `MonoPrimitives`, file [`src/Core/DebugTimer.cs`](../src/Core/DebugTimer.cs)) times a `using` block and prints the result to `Console` when it goes out of scope — a quick "why is this slow" check, not a profiler.

## Quick start

```csharp
using MonoPrimitives;

protected override void Update(GameTime gameTime)
{
    using (new DebugTimer("Update", separator: true))
    {
        // ... your update logic ...
    }
}

protected override void Draw(GameTime gameTime)
{
    using (new DebugTimer("Draw"))
    {
        // ... your draw logic ...
    }
}
```

```
------------------------------
[Update] 5.82 ms
[Draw] 0.32 ms
------------------------------
[Update] 7.38 ms
[Draw] 0.30 ms
```

## API

| Member | What it does |
|---|---|
| `new DebugTimer(label, separator = false, precision = 2)` | Starts timing. `label` is required — printed as-is on `Dispose()`. `separator: true` prints a divider line first, for marking the start of a new group of timers (e.g. once per frame). `precision` is the number of decimal places printed — raise it for a block short enough that 2 decimal places round to `0.00`. |
| `Dispose()` | Prints `[label] X.XX ms` for the time elapsed since construction — call via a `using` block/statement, not directly. |

## Why `label` is required, not `[CallerMemberName]`-optional

An earlier version let `label` default to the calling method's name via `[CallerMemberName]`, so `new DebugTimer()` would auto-label itself. Turns out that's broken for a struct: `new DebugTimer()` with empty parens never calls a constructor at all, even one with optional parameters — C# treats bare `new S()` on a value type as "just give me the default value," full stop. The result was a silently broken timer (null label, elapsed time read back as hours) for exactly the shorthand the feature was supposed to enable. See [`Design/DECISIONS.md`](../Design/DECISIONS.md) for the repro.

## Console output and MonoGame

`Console.WriteLine` only reaches an attached console. A MonoGame Windows executable normally has none — run via a terminal (`dotnet run`, or a console attached to the built `.exe`) to actually see the output.

## See also

- [`Guide/FrameLimiter_Guide.md`](FrameLimiter_Guide.md) — a different timing concern: pacing the whole game loop and reading rolling-average FPS, not printing one block's duration.
