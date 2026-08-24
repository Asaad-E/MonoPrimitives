using MonoPrimitives;

namespace MonoPrimitives.Tests
{
    /// <summary>Pure-logic checks for <see cref="ObjectPool{T}"/> — no GraphicsDevice needed.</summary>
    internal static class ObjectPoolTests
    {
        private sealed class Widget
        {
            public int ResetCount;
            public int ReleaseCount;
        }

        public static void Run(TestResults results)
        {
            results.Check("ObjectPool: Get() with an empty pool calls the factory; counts track active/inactive/all", () =>
            {
                int created = 0;
                var pool = new ObjectPool<Widget>(() => { created++; return new Widget(); });

                if (pool.CountActive != 0 || pool.CountInactive != 0 || pool.CountAll != 0) return "counts should start at 0";

                Widget a = pool.Get();
                if (created != 1) return $"expected factory called once, got {created}";
                if (pool.CountActive != 1 || pool.CountAll != 1) return "CountActive/CountAll wrong after one Get()";

                pool.Return(a);
                if (pool.CountActive != 0 || pool.CountInactive != 1 || pool.CountAll != 1) return "counts wrong after Return()";
                return null;
            });

            results.Check("ObjectPool: Get() after Return() reuses the same instance instead of calling the factory again", () =>
            {
                int created = 0;
                var pool = new ObjectPool<Widget>(() => { created++; return new Widget(); });

                Widget a = pool.Get();
                pool.Return(a);
                Widget b = pool.Get();

                if (!ReferenceEquals(a, b)) return "expected the same instance back from the pool";
                if (created != 1) return $"expected factory called exactly once, got {created}";
                return null;
            });

            results.Check("ObjectPool: onGet/onReturn hooks run at the right time", () =>
            {
                var pool = new ObjectPool<Widget>(() => new Widget(),
                    onGet: w => w.ResetCount++,
                    onReturn: w => w.ReleaseCount++);

                Widget a = pool.Get();
                if (a.ResetCount != 1 || a.ReleaseCount != 0) return "onGet should have run once, onReturn not yet";
                pool.Return(a);
                if (a.ReleaseCount != 1) return "onReturn should have run once";
                Widget b = pool.Get(); // reuses 'a'
                if (!ReferenceEquals(a, b) || b.ResetCount != 2) return "onGet should run again on reuse";
                return null;
            });

            results.Check("ObjectPool: initialCapacity pre-fills the pool without any Get() calls", () =>
            {
                int created = 0;
                var pool = new ObjectPool<Widget>(() => { created++; return new Widget(); }, initialCapacity: 5);
                if (created != 5) return $"expected 5 pre-built instances, got {created}";
                if (pool.CountInactive != 5 || pool.CountAll != 5) return "CountInactive/CountAll should reflect the pre-fill";
                if (pool.CountActive != 0) return "pre-filled instances shouldn't count as active";
                return null;
            });

            results.Check("ObjectPool: maxSize caps how many Return()s the pool actually keeps", () =>
            {
                var pool = new ObjectPool<Widget>(() => new Widget(), maxSize: 2);
                var a = pool.Get();
                var b = pool.Get();
                var c = pool.Get();
                pool.Return(a);
                pool.Return(b);
                pool.Return(c); // pool is already at maxSize=2, this one should just be dropped
                if (pool.CountInactive != 2) return $"expected CountInactive capped at 2, got {pool.CountInactive}";
                return null;
            });

            results.Check("ObjectPool: Return(null) throws; Clear() empties only the inactive pool", () =>
            {
                var pool = new ObjectPool<Widget>(() => new Widget());
                try
                {
                    pool.Return(null!);
                    return "expected an ArgumentNullException";
                }
                catch (System.ArgumentNullException) { /* expected */ }

                var active = pool.Get();
                var toClear = pool.Get();
                pool.Return(toClear);
                if (pool.CountInactive != 1) return "setup wrong before Clear()";

                pool.Clear();
                if (pool.CountInactive != 0) return "Clear() should empty the inactive pool";
                if (pool.CountActive != 1) return "Clear() shouldn't touch outstanding (active) instances";
                pool.Return(active); // still returnable after Clear()
                return null;
            });

            results.Check("ObjectPool: constructor rejects a null factory or a non-positive maxSize", () =>
            {
                try { _ = new ObjectPool<Widget>(null!); return "expected ArgumentNullException for null factory"; }
                catch (System.ArgumentNullException) { }

                try { _ = new ObjectPool<Widget>(() => new Widget(), maxSize: 0); return "expected ArgumentOutOfRangeException for maxSize=0"; }
                catch (System.ArgumentOutOfRangeException) { }

                return null;
            });
        }
    }
}
