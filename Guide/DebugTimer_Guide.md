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
| `new DebugTimer(label, separator = false)` | Starts timing. `label` is required — printed as-is on `Dispose()`. `separator: true` prints a divider line first, for marking the start of a new group of timers (e.g. once per frame). |
| `Dispose()` | Prints `[label] X.XX ms` for the time elapsed since construction — call via a `using` block/statement, not directly. |

## Why `label` is required, not `[CallerMemberName]`-optional

An earlier draft let `label` default to the calling member's name via `[CallerMemberName]`, so `new DebugTimer()` at the top of a method would auto-label itself. That's broken for a `struct`: `new DebugTimer()` — literally empty parens — never calls a user-defined constructor whose parameters are merely optional; C# specifically treats the zero-argument `new S()` form as "the all-fields-default value of `S`" for value types, regardless of what constructors exist, unless one is truly parameter-less (which can't carry `[CallerMemberName]`, since there'd be nothing to attribute). The result would have been a silently broken timer — `null` label, a zero timestamp read back as an hours-long "elapsed" time — for exactly the call style the feature was meant to make convenient, with no compiler error. See [`Design/DECISIONS.md`](../Design/DECISIONS.md) for the isolated repro that caught this.

## Console output and MonoGame

`Console.WriteLine` only reaches an attached console. A MonoGame Windows executable normally has none — run via a terminal (`dotnet run`, or a console attached to the built `.exe`) to actually see the output.

## See also

- [`Guide/FrameLimiter_Guide.md`](FrameLimiter_Guide.md) — a different timing concern: pacing the whole game loop and reading rolling-average FPS, not printing one block's duration.
