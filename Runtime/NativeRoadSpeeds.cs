using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using GleyTrafficSystem;

namespace PersistentCity.Runtime
{
    /// <summary>
    /// Keep structural speeds stable while a reviewed RLO fault changes the native driving limit.
    /// This object belongs to the planner, surviving its cache clears. Main thread only.
    /// </summary>
    internal sealed class NativeRoadSpeeds
    {
        private readonly Func<IReadOnlyDictionary<object, (int Original, int Effective)>>? capture;
        private readonly Dictionary<object, int> canonical = new Dictionary<object, int>(References.Instance);
        private IReadOnlyDictionary<object, (int Original, int Effective)> overrides =
            new Dictionary<object, (int Original, int Effective)>(References.Instance);

        public NativeRoadSpeeds(Func<IReadOnlyDictionary<object, (int Original, int Effective)>>? capture = null)
        { this.capture = capture; }

        public bool HasOverrides => overrides.Count != 0;
        internal static IEqualityComparer<object> ReferenceComparer => References.Instance;
        public void Refresh()
        {
            if (capture == null) return;
            var next = capture() ?? throw new InvalidOperationException("RLO road speed snapshot is absent.");
            var detached = new Dictionary<object, (int Original, int Effective)>(References.Instance);
            foreach (var pair in next)
            {
                if (pair.Key is not Waypoint lane || pair.Value.Original <= 0 || pair.Value.Effective <= 0
                    || pair.Value.Effective > pair.Value.Original || lane.maxSpeed != pair.Value.Effective
                    || canonical.TryGetValue(lane, out int old) && old != pair.Value.Original)
                    throw new InvalidOperationException("RLO road speed override disagrees with its canonical native lane.");
                detached.Add(lane, pair.Value);
            }
            overrides = detached;
        }

        public int Canonical(Waypoint lane) => Resolve(lane).Original;
        public int Effective(Waypoint lane) => Resolve(lane).Effective;

        private (int Original, int Effective) Resolve(Waypoint lane)
        {
            // Save/physical callbacks can run after RLO's Update and before this planner's next
            // Prepare. Refresh once on a mismatch rather than mistaking a verified transition
            // for changed geometry. No native/RLO mutation is invoked by the provider.
            for (int attempt = 0; attempt < 2; attempt++)
            {
                bool active = overrides.TryGetValue(lane, out var temporary);
                var value = active ? temporary : (Original: lane.maxSpeed, Effective: lane.maxSpeed);
                bool known = canonical.TryGetValue(lane, out int old);
                if ((!active || value.Original > 0 && value.Effective > 0) && lane.maxSpeed == value.Effective
                    && (!known || old == value.Original))
                {
                    if (!known) canonical.Add(lane, value.Original);
                    return value;
                }
                if (attempt == 0 && capture != null) Refresh();
            }
            throw new InvalidOperationException("Native road speed changed without a verified RLO slowdown/restoration.");
        }

        private sealed class References : IEqualityComparer<object>
        {
            internal static readonly References Instance = new References();
            public new bool Equals(object? a, object? b) => ReferenceEquals(a, b);
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }
    }
}
