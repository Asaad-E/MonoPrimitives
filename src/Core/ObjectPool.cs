using System;
using System.Collections.Generic;

namespace MonoPrimitives
{
    /// <summary>
    /// A generic object pool: reuses instances instead of letting them fall to the GC, for anything
    /// spawned and discarded often enough to matter — bullets, particles, agents in a large
    /// simulation. Doesn't know or care what <typeparamref name="T"/> is or does; it only hands out
    /// and takes back instances between <see cref="Get"/>/<see cref="Return"/> calls you make
    /// yourself, the same "give you the building block, not the system" shape as this library's own
    /// zero-per-frame-allocation internals.
    /// </summary>
    /// <typeparam name="T">The pooled type. Constrained to a reference type — pooling exists to avoid heap allocation, which a value type doesn't need help avoiding.</typeparam>
    public sealed class ObjectPool<T> where T : class
    {
        private readonly Stack<T> _inactive = new();
        private readonly Func<T> _factory;
        private readonly Action<T>? _onGet;
        private readonly Action<T>? _onReturn;
        private readonly int _maxSize;

        /// <summary>Instances currently outstanding — handed out by <see cref="Get"/> but not yet given back to <see cref="Return"/>.</summary>
        public int CountActive { get; private set; }

        /// <summary>Instances currently sitting in the pool, ready to be handed out by the next <see cref="Get"/>.</summary>
        public int CountInactive => _inactive.Count;

        /// <summary>Every instance this pool is still tracking — <see cref="CountActive"/> + <see cref="CountInactive"/>.</summary>
        public int CountAll => CountActive + CountInactive;

        /// <param name="factory">Creates a brand-new <typeparamref name="T"/> — called only when the pool is empty and <see cref="Get"/> needs one.</param>
        /// <param name="onGet">Optional: runs on an instance right before <see cref="Get"/> hands it back — reset it to a usable state here (position, health, whatever makes a reused <typeparamref name="T"/> look "fresh" to its caller).</param>
        /// <param name="onReturn">Optional: runs on an instance right when <see cref="Return"/> receives it — release anything it shouldn't keep holding onto while sitting in the pool (e.g. clearing a reference to something else it pointed at).</param>
        /// <param name="initialCapacity">Pre-fills the pool with this many instances up front (each built via <paramref name="factory"/>), so the first burst of real <see cref="Get"/> calls doesn't have to construct anything.</param>
        /// <param name="maxSize">Caps how many instances <see cref="Return"/> actually keeps — a <see cref="Return"/> past this cap just lets that instance fall to the GC instead of growing the pool forever. <see cref="int.MaxValue"/> (the default) means unbounded.</param>
        public ObjectPool(Func<T> factory, Action<T>? onGet = null, Action<T>? onReturn = null, int initialCapacity = 0, int maxSize = int.MaxValue)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            if (maxSize <= 0) throw new ArgumentOutOfRangeException(nameof(maxSize), "maxSize must be positive.");
            _onGet = onGet;
            _onReturn = onReturn;
            _maxSize = maxSize;
            for (int i = 0; i < initialCapacity; i++) _inactive.Push(_factory());
        }

        /// <summary>Hands back a pooled instance if one's available, or builds a new one via the constructor's <c>factory</c> otherwise — either way, the constructor's <c>onGet</c> runs on it first if one was given.</summary>
        public T Get()
        {
            T item = _inactive.Count > 0 ? _inactive.Pop() : _factory();
            CountActive++;
            _onGet?.Invoke(item);
            return item;
        }

        /// <summary>
        /// Gives <paramref name="item"/> back to the pool for a future <see cref="Get"/> to reuse —
        /// runs the constructor's <c>onReturn</c> on it first if one was given. Don't touch
        /// <paramref name="item"/> again after this call; a later <see cref="Get"/> may hand the
        /// same instance to someone else at any time. Once <see cref="CountInactive"/> would exceed
        /// the constructor's <c>maxSize</c>, <paramref name="item"/> is simply dropped (left for the
        /// GC) instead of growing the pool further. Returning something never obtained from
        /// <see cref="Get"/>, or returning the same instance twice, is caller misuse — not guarded
        /// against, the same trust-the-caller boundary this library draws everywhere else.
        /// </summary>
        public void Return(T item)
        {
            if (item is null) throw new ArgumentNullException(nameof(item));
            CountActive--;
            _onReturn?.Invoke(item);
            if (_inactive.Count < _maxSize) _inactive.Push(item);
        }

        /// <summary>Drops every pooled (inactive) instance. Outstanding instances already handed out by <see cref="Get"/> are unaffected and can still be returned normally afterward.</summary>
        public void Clear() => _inactive.Clear();
    }
}
