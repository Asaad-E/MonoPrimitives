using System;
using Microsoft.Xna.Framework;

namespace MonoPrimitives
{
    /// <summary>
    /// A simple countdown: <see cref="Update(float)"/> it down every frame, then check
    /// <see cref="IsReady"/> — or call <see cref="TryUse"/> to check-and-restart in one step — for
    /// an attack cooldown, a spawn timer, a debounce on repeated input. A plain <c>struct</c>, not a
    /// class: cheap enough to be a field on hundreds of entities in a simulation without each one
    /// costing a separate heap allocation. Store it as a field, not a local re-assigned each frame —
    /// the usual caveat for any mutable struct (mutating a copy pulled into a local, an array
    /// element accessed by value, or a <c>foreach</c> loop variable never writes back to the
    /// original).
    /// </summary>
    public struct Cooldown
    {
        private float _remaining;

        /// <summary>The full duration (seconds) <see cref="Reset"/> or a successful <see cref="TryUse"/> restarts the countdown to.</summary>
        public float Duration { get; set; }

        /// <summary>True once <see cref="Remaining"/> has counted down to (or past) zero.</summary>
        public readonly bool IsReady => _remaining <= 0f;

        /// <summary>Seconds left before <see cref="IsReady"/> — never negative, even if the last <see cref="Update(float)"/> overshot past zero.</summary>
        public readonly float Remaining => MathF.Max(_remaining, 0f);

        /// <summary>How far through the countdown this is: <c>0</c> just after a <see cref="Reset"/>/<see cref="TryUse"/>, <c>1</c> once <see cref="IsReady"/> — a cooldown bar's fill amount. <c>1</c> if <see cref="Duration"/> is zero or negative (nothing to wait through).</summary>
        public readonly float Progress => Duration > 0f ? 1f - Remaining / Duration : 1f;

        /// <summary>Creates a cooldown of <paramref name="duration"/> seconds, starting already <see cref="IsReady"/> — the usual expectation for something you should be able to use right away the first time.</summary>
        public Cooldown(float duration)
        {
            Duration = duration;
            _remaining = 0f;
        }

        /// <summary>Counts the cooldown down by <paramref name="deltaSeconds"/>. Call once per frame.</summary>
        public void Update(float deltaSeconds) => _remaining = MathF.Max(0f, _remaining - deltaSeconds);

        /// <summary>Same as <see cref="Update(float)"/>, from a <see cref="GameTime"/> directly.</summary>
        public void Update(GameTime gameTime) => Update((float)gameTime.ElapsedGameTime.TotalSeconds);

        /// <summary>If <see cref="IsReady"/>, restarts the countdown at <see cref="Duration"/> and returns <c>true</c>; otherwise does nothing and returns <c>false</c> — the whole "can I fire, and if so start the cooldown" check in one call, e.g. <c>if (cooldown.TryUse()) Fire();</c>.</summary>
        public bool TryUse()
        {
            if (!IsReady) return false;
            _remaining = Duration;
            return true;
        }

        /// <summary>Restarts the countdown at <see cref="Duration"/> — not <see cref="IsReady"/> again until it fully counts down.</summary>
        public void Reset() => _remaining = Duration;

        /// <summary>Forces <see cref="IsReady"/> immediately, skipping whatever time was left — e.g. to undo a <see cref="Reset"/>, or let something fire right away the first time despite a nonzero <see cref="Duration"/> already set at construction.</summary>
        public void ResetReady() => _remaining = 0f;
    }
}
