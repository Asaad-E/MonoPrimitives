# ObjectPool&lt;T&gt; — Guide

`ObjectPool<T>` (namespace `MonoPrimitives`, file [`src/Core/ObjectPool.cs`](../src/Core/ObjectPool.cs)) reuses instances of `T` instead of letting them fall to the GC — for anything spawned and discarded often enough to matter: bullets, particles, agents in a large simulation. It doesn't know or care what `T` is or does; it only hands out and takes back instances between `Get`/`Return` calls you make yourself.

## Quick start

```csharp
using MonoPrimitives;

private readonly ObjectPool<Bullet> _bullets = new(
    factory: () => new Bullet(),
    onGet: b => b.Active = true,
    onReturn: b => b.Active = false);

void Fire(Vector2 position, Vector2 velocity)
{
    Bullet b = _bullets.Get();
    b.Position = position;
    b.Velocity = velocity;
}

void OnBulletExpired(Bullet b) => _bullets.Return(b);
```

## API

| Member | What it does |
|---|---|
| `new ObjectPool<T>(factory, onGet = null, onReturn = null, initialCapacity = 0, maxSize = int.MaxValue)` | `factory` builds a brand-new `T` — called only when the pool is empty and `Get()` needs one. `onGet`/`onReturn` are optional hooks (see below). `initialCapacity` pre-builds that many instances up front. `maxSize` caps how many `Return()` actually keeps. |
| `Get()` | Hands back a pooled instance if one's available, or builds a new one via `factory` otherwise. Runs `onGet` on it first if one was given. |
| `Return(item)` | Gives `item` back to the pool for a future `Get()` to reuse. Runs `onReturn` on it first if one was given. Throws on `null`. |
| `Clear()` | Drops every pooled (inactive) instance. Outstanding ones already handed out are unaffected. |
| `CountActive` | Instances currently outstanding — handed out but not yet returned. |
| `CountInactive` | Instances sitting in the pool right now, ready for the next `Get()`. |
| `CountAll` | `CountActive` + `CountInactive`. |

## `onGet`/`onReturn`: reset the instance, not the pool

A reused instance still has whatever state it had the last time it was returned — `ObjectPool<T>` doesn't know how to "clean" a `T` for you. `onGet` is where you put it back into a usable state (position, health, whatever makes it look fresh to its next caller); `onReturn` is where you release anything it shouldn't keep holding onto while sitting idle (e.g. clearing a reference to something else it pointed at, so that object isn't kept alive by a pooled instance nobody's using). Both are optional — a pool of something genuinely stateless (or that resets itself on next use anyway) doesn't need either.

## What this doesn't do

- **Doesn't validate double-`Return()` or returning something never `Get()`'d.** Both are caller misuse, not guarded against — the same trust-the-caller boundary this library draws everywhere else (adding a tracking check would cost real overhead on every `Get`/`Return` to catch a bug you control by just not doing it).
- **Doesn't know what `T` is.** No update loop, no rendering, no ownership of your game logic — it's a building block, not a system. You still own deciding when something is spawned/expired; this only handles not reallocating it every time.
- **`maxSize`** exists so a caller that forgets to ever call `Get()` again after a burst of `Return()`s doesn't leave the pool holding an unbounded number of idle instances forever — past the cap, a `Return()` just lets that instance fall to the GC instead.

## See also

- [`Guide/RingBuffer_Guide.md`](RingBuffer_Guide.md) — a different kind of reuse: a fixed-size history instead of a grab-bag of interchangeable instances.
- [`Guide/Cooldown_Guide.md`](Cooldown_Guide.md) — pairs naturally with a pool for a spawner (`if (cooldown.TryUse()) Fire();`).
