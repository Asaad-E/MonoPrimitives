using System;
using Microsoft.Xna.Framework.Graphics;
using MonoPrimitives.Primitives2D;

namespace MonoPrimitives.Tests
{
    /// <summary>
    /// Regression coverage for a real gap: <c>Primitive2DBatch.Dispose()</c> used to be a bare
    /// <c>_effect?.Dispose()</c> with no <c>_disposed</c> flag at all, unlike <c>Primitive3DBatch</c>
    /// (which has always guarded <c>Begin</c> with <c>ThrowIfDisposed()</c>) — a gap this project's
    /// own DECISIONS.md already named directly ("worth a look if Primitive2DBatch ever grows
    /// unmanaged resources of its own") but never closed. Using a disposed <see cref="Primitive2DBatch"/>
    /// used to fail opaquely inside an already-released <see cref="BasicEffect"/> instead of with a
    /// clear <see cref="ObjectDisposedException"/> right at <c>Begin()</c>.
    /// </summary>
    internal static class Primitive2DBatchLifecycleTests
    {
        public static void Run(GraphicsDevice device, TestResults results)
        {
            results.Check("Primitive2DBatch: Begin() after Dispose() throws ObjectDisposedException", () =>
            {
                var batch = new Primitive2DBatch(device);
                batch.Dispose();

                try
                {
                    batch.Begin();
                    return "expected an ObjectDisposedException, Begin() did not throw at all";
                }
                catch (ObjectDisposedException)
                {
                    return null;
                }
            });

            results.Check("Primitive2DBatch: Dispose() is idempotent -- calling it twice doesn't throw", () =>
            {
                var batch = new Primitive2DBatch(device);
                batch.Dispose();
                batch.Dispose(); // must not throw
                return null;
            });

            results.Check("Primitive2DBatch.Effect exposes the same non-null BasicEffect instance every access", () =>
            {
                using var batch = new Primitive2DBatch(device);
                BasicEffect effect = batch.Effect;
                if (effect is null) return "Effect returned null";
                if (!ReferenceEquals(effect, batch.Effect)) return "Effect should return the same instance every access, not rebuild one";
                if (!effect.VertexColorEnabled) return "the batch's own required VertexColorEnabled=true invariant was already violated at construction";
                return null;
            });
        }
    }
}
