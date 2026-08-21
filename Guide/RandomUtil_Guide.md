# RandomUtil — Guide

`RandomUtil` (namespace `MonoPrimitives`, file [`src/Core/RandomUtil.cs`](../src/Core/RandomUtil.cs)) is a seedable, float-based random sampler covering the probability distributions a simulation typically needs — uniform, Bernoulli, Gaussian, log-normal, exponential, Poisson, binomial, and uniform points on or inside a circle/sphere — instead of hand-rolling each one against `System.Random` at every call site.

This guide explains what each method does, when to reach for it, how the distributions are actually computed internally, and how to use `RandomUtil` safely across single-threaded and multi-threaded code.

## Quick start

```csharp
using MonoPrimitives;

private RandomUtil _rng;

protected override void Initialize()
{
    _rng = new RandomUtil(seed: 42); // fixed seed -> same simulation every run
    base.Initialize();
}

protected override void Update(GameTime gameTime)
{
    float speed = _rng.NextGaussian(mean: 120f, stdDev: 15f);
    bool didReproduce = _rng.NextBool(0.1f);
    // ...
}
```

Construct one instance, keep it, and call its `Next*` methods every frame — the same lifecycle as [`Noise`](../src/Core/Noise.cs). Use `new RandomUtil(seed)` for a reproducible sequence (debugging, sharing a specific run, deterministic tests), or the parameterless `new RandomUtil()` for a different sequence every run.

## Method reference

| Method | Returns | What it samples | Typical simulation use |
|---|---|---|---|
| `NextUniform(min = 0, max = 1)` | `float` | Flat/uniform value in `[min, max)` — every value in range equally likely | Random position within a bound, a flat-random delay, generic scalar variation with no natural "typical" value |
| `NextInt(minInclusive, maxExclusive)` | `int` | Uniform integer in `[minInclusive, maxExclusive)` | Picking an index or a discrete option among *N* choices, dice-roll-style mechanics |
| `NextBool(probability = 0.5)` | `bool` | A single Bernoulli trial — `true` with the given probability | A cell's alive/dead state in a cellular automaton, whether an individual agent survives/reproduces/dies this tick |
| `NextGaussian(mean = 0, stdDev = 1)` | `float` | Normal ("bell curve") distribution around `mean` | Natural variation around a typical value — particle speed/size/lifetime jitter, measurement noise, most agents near-average with a few outliers |
| `NextLogNormal(mean = 0, stdDev = 1)` | `float` | Log-normal — always positive, long right tail. `mean`/`stdDev` describe the *underlying normal*, not the output directly | Quantities that can never be negative and are often skewed: cluster/settlement sizes, resource yields, anything "small is common, huge is rare but possible" |
| `NextExponential(rate)` | `float` | Exponential, mean `1/rate` | Waiting time until the next independent random event — time until next arrival, next mutation, next decay/recovery in an epidemic model |
| `NextPoisson(lambda)` | `int` | Poisson — count of discrete events in a fixed interval, given an average rate `lambda` | Number of births/arrivals/infections this tick when you know the *average* rate but each event is independent |
| `NextBinomial(trials, probability)` | `int` | Binomial — number of successes out of `trials` independent attempts, each succeeding at `probability` | How many of *N* susceptible individuals get infected this tick, how many of *N* seeds germinate — the "count of successes out of a known population" case |
| `NextOnUnitCircle()` | `Vector2` | Uniform point on the unit circle's edge (length exactly 1) | Random initial 2D heading/direction for an agent, particle, or projectile |
| `NextInsideUnitCircle()` | `Vector2` | Uniform point inside the unit disc, by area (not just by angle) | Scattering spawn positions naturally around a point instead of a hard ring or a center-heavy clump |
| `NextOnUnitSphere()` | `Vector3` | Uniform point on the unit sphere's surface (length exactly 1) | Random initial 3D direction/orientation |
| `NextInsideUnitSphere()` | `Vector3` | Uniform point inside the unit ball, by volume | 3D volumetric jitter or scatter (particle puffs, spawn clouds) |

**Choosing between `NextUniform`, `NextGaussian`, and `NextExponential`/`NextPoisson`/`NextBinomial` for "randomness in a simulation":** ask what shape the real-world quantity has. No natural center (any value equally likely) → `NextUniform`. A typical value with symmetric variation around it → `NextGaussian`. A count of discrete events at a known rate → `NextPoisson`. A count of successes out of a known number of independent attempts → `NextBinomial`. A waiting time between independent events → `NextExponential`.

## How it works internally

### Gaussian: Marsaglia polar, not Box-Muller

The textbook way to turn uniform randomness into a normal distribution is the Box-Muller transform, which needs a `Sin` and a `Cos` call per sample. `RandomUtil` instead uses the **Marsaglia polar method**: it rejection-samples two uniform values inside the unit disc, then derives the result from `Sqrt`/`Log` alone — no trigonometry at all. This matches this library's existing stance elsewhere (`UnitCircleLut`/`TrigLut`) of avoiding `Sin`/`Cos` on a hot path wherever a cheaper equivalent exists.

The rejection loop accepts roughly 78.5% of the time (the unit disc's area relative to the 2×2 square it's sampled from), and every acceptance produces **two** independent normal samples, not one. The first is returned immediately; the second is cached internally and handed back on the *next* call to `NextGaussian`, so on average each call only does the full rejection-loop work half the time.

### Poisson and Binomial: bounded cost regardless of input size

Simulated directly, both Poisson and Binomial cost work proportional to their parameter (`lambda`, or `trials`) — fine for small values, but unbounded for large ones. A pandemic model asking `NextBinomial(2_000_000, 0.00003f)` every tick would be simulating two million individual coin flips per call if done naively.

`RandomUtil` instead switches to a Gaussian approximation once the distribution's variance is large enough for that approximation to hold (the standard textbook threshold), and — for the specific case of a huge trial count paired with a tiny probability, i.e. a rare event repeated across a huge population — falls back to a Poisson approximation instead, since that is the correct classical approximation for exactly that regime. The practical effect: cost stays a small, fixed number of operations no matter how large `lambda` or `trials` gets, and every regime was verified (200,000-sample runs) to match its theoretical mean and variance before being trusted.

You never need to know which internal path a given call took — the three regimes exist purely to keep worst-case cost bounded, not to change what you write at the call site.

### Circle and sphere sampling: why the radius isn't just `NextUniform(0, 1)`

For `NextInsideUnitCircle`, using a plain uniform value as the radius would bunch samples too densely near the center — a thin ring near radius 0.1 covers far less area than a ring near radius 0.9, so a uniform-by-radius approach over-represents the center. The fix is to take the square root of a uniform value as the radius instead, which correctly accounts for how area grows with radius in two dimensions. `NextInsideUnitSphere` uses the same idea with a cube root instead of a square root, since volume grows differently with radius in three dimensions.

`NextOnUnitSphere` has an analogous, differently-shaped subtlety: picking a uniformly random latitude and longitude (the naive approach) visibly clusters samples near the poles, because latitude rings shrink in circumference near the poles while still getting an equal share of samples. The fix used here is to pick the sphere's `z` coordinate uniformly in `[-1, 1]` and derive the latitude ring's radius from it — each ring's shrinking circumference is exactly offset by `z` spending proportionally less of its range there, giving a genuinely uniform distribution over the whole surface.

`RandomUtil` deliberately uses plain `MathF.Sin`/`Cos` for the angular part of circle/sphere sampling rather than this library's `UnitCircleLut`/`TrigLut` lookup tables — those tables live in the 2D and 3D namespaces respectively, while `RandomUtil` lives in the shared `Core` namespace that both depend on, so reaching back into either would invert that dependency. It's also a different cost profile than the tables exist for: one trigonometric call per random sample here, not one per vertex of a many-triangle shape.

## Thread safety: instance API vs. `RandomUtil.Shared`

### The instance API is single-threaded, by design

`RandomUtil` wraps one `System.Random` stream and is **not thread-safe** — the same assumption every other stateful class in this library makes (`Noise`, `PrimitiveInput`, `Camera2D`/`Camera3D`). Calling the same `RandomUtil` instance's methods from two threads at once is not supported and can corrupt its internal state.

This matters specifically when parallelizing a large agent-based simulation, e.g. with `Parallel.For` over thousands of agents. There are two supported ways to do that safely.

### Option A — one `RandomUtil` instance per thread (reproducible)

If the simulation should still be seed-driven, give each thread its own instance instead of sharing one. The cleanest way is `ThreadLocal<RandomUtil>`, which hands a *different* object to each thread that reads its `.Value`, built lazily the first time that thread asks:

```csharp
private const int BaseSeed = 42;

private readonly ThreadLocal<RandomUtil> _perThreadRng =
    new(() => new RandomUtil(BaseSeed + Environment.CurrentManagedThreadId));

Parallel.For(0, agentCount, i =>
{
    RandomUtil rng = _perThreadRng.Value; // this thread's own instance
    agents[i].Update(rng);
});
```

Walking through why this works:

- `new(() => new RandomUtil(...))` passes a *factory* to `ThreadLocal<T>`'s constructor: "the first time some thread asks for `.Value` and doesn't have one yet, run this to build it."
- `Environment.CurrentManagedThreadId` gives each thread a different seed offset, so threads don't coincidentally start from the same seed and produce correlated (not actually independent) streams.
- The first time thread #7 calls `.Value`, the factory runs once and thread #7 keeps that same instance for every later call — no thread ever touches another thread's `RandomUtil`.

Note that "reproducible" here means the *set* of values drawn is deterministic given the base seed — which thread happens to process which agent, and in what order, is still up to the thread pool's scheduling, so this does not guarantee byte-identical output across runs the way a single-threaded seeded run would.

### Option B — `RandomUtil.Shared` (thread-safe, not reproducible)

When reproducibility doesn't matter and simplicity does, skip per-thread instance management entirely and call the static `Shared` API directly:

```csharp
Parallel.For(0, agentCount, i =>
{
    agents[i].Velocity += RandomUtil.Shared.NextInsideUnitCircle() * jitterStrength;
});
```

`RandomUtil.Shared` mirrors every instance method as a static equivalent, built on .NET's own `Random.Shared` (thread-safe internally — each thread gets its own stream automatically) instead of a seeded per-instance stream. There is no seed constructor for `Shared`: once more than one thread can touch it, there is no single reproducible sequence to seed in the first place, so that guarantee was never on the table for this path regardless of how it's implemented.

`Shared.NextGaussian`'s internal spare-value cache (see the Marsaglia polar explanation above) uses a `[ThreadStatic]` field rather than a lock, so each thread keeps its own cache slot with no contention — the same strategy .NET's own `Random.Shared` uses internally. This was verified directly with a 16-thread stress test hammering every method concurrently: zero exceptions, and each thread's own running statistics matched the expected theoretical mean/variance.

### Which one to use

| | `RandomUtil` (instance) | `RandomUtil.Shared` |
|---|---|---|
| Thread safety | Single-threaded only | Thread-safe |
| Reproducible with a seed | Yes | No |
| Setup needed for parallel use | One instance per thread (`ThreadLocal<RandomUtil>`) | None — call directly |
| Best for | A simulation you want to reproduce, debug, or replay | Fire-and-forget randomness in parallel code where the exact sequence doesn't matter |

## See also

- [`Design/DECISIONS.md`](../Design/DECISIONS.md) — the condensed rationale behind these specific algorithm and threshold choices.
- [`Noise`](../src/Core/Noise.cs) — the other seedable source of randomness in this library, for smooth/coherent (not independent-sample) variation like terrain or organic motion.
