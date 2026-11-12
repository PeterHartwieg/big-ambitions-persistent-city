using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using GleyTrafficSystem;

namespace PersistentCity.Runtime
{
    internal sealed class RoadSpeedsWaitingException : InvalidOperationException
    {
        internal RoadSpeedsWaitingException(string message) : base(message) { }
    }

    internal sealed class NativeRoadSpeeds
    {
        private readonly Func<IReadOnlyDictionary<object, (int Original, int Effective)>>? capture;
        private readonly Dictionary<object, int> canonical = new Dictionary<object, int>(References.Instance);
        private IReadOnlyDictionary<object, (int Original, int Effective)> overrides =
            new Dictionary<object, (int Original, int Effective)>(References.Instance);

        private readonly Func<Exception, string?>? transient;

        public NativeRoadSpeeds(Func<IReadOnlyDictionary<object, (int Original, int Effective)>>? capture = null, Func<Exception, string?>? transient = null)
        { this.capture = capture; this.transient = transient; }

        public bool HasOverrides => overrides.Count != 0;
        public bool FromRlo => capture != null;
        public string? Waiting { get; private set; }
        public bool Verified { get; private set; }
        public string? LastRefusal { get; private set; }
        internal static IEqualityComparer<object> ReferenceComparer => References.Instance;

        public bool WaitsAfterRefresh() { Refresh(); return Waiting != null; }
        public void Refresh()
        {
            if (capture == null) return;
            IReadOnlyDictionary<object, (int Original, int Effective)> next;
            try { next = capture() ?? throw new InvalidOperationException("RLO road speed snapshot is absent."); }
            catch (Exception error) when (transient?.Invoke(error) is string waiting) { Waiting = waiting; Verified = false; return; }
            catch (Exception error) { Waiting = null; Verified = false; LastRefusal = error.Message; throw; }
            Waiting = null;
            var detached = new Dictionary<object, (int Original, int Effective)>(References.Instance);
            foreach (var pair in next)
            {
                if (pair.Key is not Waypoint lane || pair.Value.Original <= 0 || pair.Value.Effective <= 0
                    || pair.Value.Effective > pair.Value.Original || lane.maxSpeed != pair.Value.Effective
                    || canonical.TryGetValue(lane, out int old) && old != pair.Value.Original)
                {
                    Verified = false; LastRefusal = "RLO road speed override disagrees with its canonical native lane.";
                    throw new InvalidOperationException(LastRefusal);
                }
                detached.Add(lane, pair.Value);
            }
            overrides = detached; Verified = true; LastRefusal = null;
        }

        public int Canonical(Waypoint lane) => Resolve(lane).Original;
        public int Effective(Waypoint lane) => Resolve(lane).Effective;

        private (int Original, int Effective) Resolve(Waypoint lane)
        {
            if (Waiting != null) throw new RoadSpeedsWaitingException(Waiting);
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
                if (attempt == 0 && capture != null) { Refresh(); if (Waiting != null) throw new RoadSpeedsWaitingException(Waiting); }
            }
            Verified = false; LastRefusal = "Native road speed changed without a verified RLO slowdown/restoration.";
            throw new InvalidOperationException(LastRefusal);
        }

        private sealed class References : IEqualityComparer<object>
        {
            internal static readonly References Instance = new References();
            public new bool Equals(object? a, object? b) => ReferenceEquals(a, b);
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }
    }
}
