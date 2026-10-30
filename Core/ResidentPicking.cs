using System;

namespace PersistentCity.Core
{
    /// <summary>
    /// Decides when hover picking may run: only after the cursor has moved, and at most once per
    /// interval. A still cursor never triggers a pick. A move made while throttled is remembered, so
    /// the pick still happens once the interval elapses even if the cursor stopped in between.
    /// Allocation-free; the caller supplies an unscaled clock in seconds.
    /// </summary>
    public sealed class ResidentPickThrottle
    {
        public const double DefaultIntervalSeconds = 0.05;
        public const float DefaultMovePixels = 1f;
        private readonly double interval;
        private readonly float movePixels;
        private float lastX = float.NaN, lastY = float.NaN;
        private double nextAt = double.NegativeInfinity;
        private bool pending;

        public ResidentPickThrottle(double intervalSeconds = DefaultIntervalSeconds, float movePixels = DefaultMovePixels)
        {
            if (!(intervalSeconds >= 0) || double.IsInfinity(intervalSeconds)) throw new ArgumentOutOfRangeException(nameof(intervalSeconds));
            if (!(movePixels > 0) || float.IsInfinity(movePixels)) throw new ArgumentOutOfRangeException(nameof(movePixels));
            interval = intervalSeconds; this.movePixels = movePixels;
        }

        /// <summary>Picks since construction or the last <see cref="ResetPicks"/>; diagnostic only.</summary>
        public long Picks { get; private set; }

        /// <summary>Start a new diagnostic window. Scheduling state (last position, next time, pending move) is unchanged.</summary>
        public void ResetPicks() => Picks = 0;

        public bool ShouldPick(double now, float x, float y)
        {
            if (float.IsNaN(x) || float.IsNaN(y)) return false;
            if (float.IsNaN(lastX) || Math.Abs(x - lastX) >= movePixels || Math.Abs(y - lastY) >= movePixels) pending = true;
            if (!pending || now < nextAt) return false;
            pending = false; lastX = x; lastY = y; nextAt = now + interval; Picks++;
            return true;
        }

        /// <summary>Force a pick at the next allowed time, e.g. after the popup closed or the city changed.</summary>
        public void Invalidate() { lastX = lastY = float.NaN; }
    }

    /// <summary>
    /// Screen-space nearest-walker selection. Coordinates are GUI pixels with y growing downwards; the
    /// walker is the projected segment from feet to head. Lower score is a better hit; exact ties go
    /// to the nearer body, then the ordinal-lower stable ID, so the result never depends on iteration order.
    /// </summary>
    public struct ResidentPickAccumulator
    {
        public string? Id { get; private set; }
        public long Generation { get; private set; }
        public float Score { get; private set; }
        public float Depth { get; private set; }
        public bool Found => Id != null;

        public static bool Hit(float mouseX, float mouseY, float footX, float footY, float headX, float headY, float minimumHalfWidth, out float score)
        {
            score = float.PositiveInfinity;
            float height = footY - headY;
            if (!(height > 0) || float.IsInfinity(height) || float.IsNaN(footX) || float.IsNaN(headX)) return false;
            float pad = height * 0.1f;
            if (mouseY < headY - pad || mouseY > footY + pad) return false;
            float t = Math.Max(0, Math.Min(1, (mouseY - headY) / height));
            float axis = headX + (footX - headX) * t;
            float halfWidth = Math.Max(minimumHalfWidth, height * 0.3f);
            float dx = Math.Abs(mouseX - axis);
            if (dx > halfWidth) return false;
            score = dx / halfWidth + Math.Abs(mouseY - (headY + footY) * 0.5f) / height * 0.5f;
            return true;
        }

        public void Consider(string id, long generation, float score, float depth)
        {
            if (id == null || float.IsNaN(score) || float.IsNaN(depth)) return;
            if (Id != null)
            {
                if (score > Score) return;
                if (score == Score)
                {
                    if (depth > Depth) return;
                    if (depth == Depth && string.CompareOrdinal(id, Id) >= 0) return;
                }
            }
            Id = id; Generation = generation; Score = score; Depth = depth;
        }

        public void Clear() { Id = null; Generation = 0; Score = Depth = 0; }
    }

    public enum NativeMouseAction { Allow, Suppress, SuppressAndReleaseHolds }

    /// <summary>
    /// Who owns one mouse gesture (press, hold, release): the game's world mouse handling or the
    /// inspector. Only a press made while the cursor is on the inspector's popup or highlighted walker,
    /// with no native hold-walk in progress, becomes the inspector's; it then keeps the gesture until
    /// every button is up, wherever the cursor goes. Any other gesture stays native throughout, including
    /// its release over the popup or a walker, so the game always sees the release that ends a hold-walk.
    /// The caller passes the current input state and decides once per frame.
    /// </summary>
    public sealed class InspectorPointerGesture
    {
        public bool Owned { get; private set; }

        public NativeMouseAction Decide(bool pressed, bool released, bool anyButtonDown, bool nativeHoldActive, bool pointerOnInspector)
        {
            if (Owned)
            {
                if (released || !anyButtonDown) { Owned = false; return NativeMouseAction.SuppressAndReleaseHolds; }
                return NativeMouseAction.Suppress;
            }
            if (!pressed || nativeHoldActive || !pointerOnInspector) return NativeMouseAction.Allow;
            // A press and release within one frame ends the gesture at once.
            Owned = !released && anyButtonDown;
            return NativeMouseAction.Suppress;
        }

        public void Reset() => Owned = false;
    }
}
