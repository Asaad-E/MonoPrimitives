# RingBuffer&lt;T&gt; — Guide

`RingBuffer<T>` (namespace `MonoPrimitives`, file [`src/Core/RingBuffer.cs`](../src/Core/RingBuffer.cs)) is a fixed-capacity generic ring buffer — the same "never allocate once warmed up, oldest entry silently overwritten" building block [`Trail2D`](Trail2D_Guide.md)/[`Trail3D`](Trail2D_Guide.md#3d-trail3d) and [`FpsCounter`](FpsCounter_Guide.md) each already build privately for their own use, exposed here as a reusable type instead of hand-rolled again for your own history/log/sample-window need.

## Quick start

```csharp
using MonoPrimitives;

private readonly RingBuffer<float> _recentDamage = new(capacity: 10);

void OnHit(float amount)
{
    _recentDamage.Add(amount);
    float total = 0f;
    foreach (float d in _recentDamage) total += d; // oldest to newest, allocation-free
}
```

## API

| Member | What it does |
|---|---|
| `new RingBuffer<T>(capacity)` | Creates an empty buffer holding up to `capacity` elements. Throws if `capacity <= 0`. |
| `Add(item)` | Appends `item`, evicting the oldest element once `Capacity` is reached. |
| `Clear()` | Drops every recorded element and clears the backing slots (so a reference-type `T` doesn't keep the GC from collecting what it pointed to). |
| `Capacity` | Maximum number of elements this buffer holds. |
| `Count` | How many elements are actually recorded so far (grows to `Capacity`, then stays there). |
| `this[indexFromOldest]` | Element at that index — `0` is the oldest recorded element, `Count - 1` is the newest. Throws outside `[0, Count)`. |
| `Newest` / `Oldest` | Shorthand for `this[Count - 1]` / `this[0]`. Throws on an empty buffer. |
| `foreach` (`IEnumerable<T>`) | Enumerates oldest-first to newest-last, matching the indexer's own order — allocation-free when used as `foreach (var x in ringBuffer)` against the concrete type (a custom struct enumerator, the same trick `List<T>` itself uses, not a boxed `yield return` iterator). |

## Why this exists as its own type

`Trail2D`, `Trail3D`, and `FpsCounter` all privately implement the exact same "fixed array, wrap the write index, evict the oldest" logic for their own internal history — `RingBuffer<T>` is that logic, generalized and made public, so a fourth (your own) use of the same pattern doesn't need to be written from scratch again. It has no drawing, no frame-time semantics, no opinion about what `T` is — just the buffer.

## See also

- [`Trail2D_Guide.md`](Trail2D_Guide.md) — a `Vector2`/`Vector3`-specific ring buffer with drawing and fade built on top, if that's closer to what you need.
- [`FpsCounter_Guide.md`](FpsCounter_Guide.md) — a `float`-specific ring buffer with rolling-average math built on top.
- [`ObjectPool_Guide.md`](ObjectPool_Guide.md) — a different kind of reuse: a grab-bag of interchangeable instances instead of an ordered history.
