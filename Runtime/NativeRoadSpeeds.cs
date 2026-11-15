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
        private Dictionary<object, (int Original, int Effective)> overrides =
            new Dictionary<object, (int Original, int Effective)>(References.Instance);
        private Dictionary<object, (int Original, int Effective)> spare =
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
        private static readonly IReadOnlyDictionary<object, (int Original, int Effective)> NoOverrides =
            new Dictionary<object, (int Original, int Effective)>(References.Instance);
        internal static Func<IReadOnlyDictionary<object, (int Original, int Effective)>> Reusing<T>(Action<Dictionary<object, T>> fill,
            Func<Dictionary<object, T>> buffer, Func<T, (int, int)> limits)
        {
            var read = buffer();
            var mapped = new Dictionary<object, (int Original, int Effective)>(References.Instance);
            return () =>
            {
                fill(read);
                mapped.Clear();
                foreach (var pair in read) mapped.Add(pair.Key, limits(pair.Value));
                return mapped;
            };
        }
        internal static IReadOnlyDictionary<object, (int Original, int Effective)> Copy<T>(IReadOnlyDictionary<object, T> source, Func<T, (int, int)> limits)
        {
            if (source.Count == 0) return NoOverrides;
            var copy = new Dictionary<object, (int Original, int Effective)>(source.Count, References.Instance);
            foreach (var pair in source) copy.Add(pair.Key, limits(pair.Value));
            return copy;
        }

        public bool WaitsAfterRefresh() { Refresh(); return Waiting != null; }
        private int refreshedFrame = int.MinValue;
        public long Captures { get; private set; }
        public void RefreshForFrame(int frame)
        {
            if (frame == refreshedFrame && Waiting == null && Verified) return;
            Refresh();
            refreshedFrame = Waiting == null && Verified ? frame : int.MinValue;
        }
        public void Refresh()
        {
            if (capture == null) return;
            Captures++;
            IReadOnlyDictionary<object, (int Original, int Effective)> next;
            try { next = capture() ?? throw new InvalidOperationException("RLO road speed snapshot is absent."); }
            catch (Exception error) when (transient?.Invoke(error) is string waiting) { Waiting = waiting; Verified = false; return; }
            catch (Exception error) { Waiting = null; Verified = false; LastRefusal = error.Message; throw; }
            Waiting = null;
            var detached = spare;
            detached.Clear();
            if (next is Dictionary<object, (int Original, int Effective)> concrete)
                foreach (var pair in concrete) Accept(detached, pair.Key, pair.Value);
            else
                foreach (var pair in next) Accept(detached, pair.Key, pair.Value);
            spare = overrides; overrides = detached; Verified = true; LastRefusal = null;
        }

        private void Accept(Dictionary<object, (int Original, int Effective)> detached, object key, (int Original, int Effective) value)
        {
            if (key is not Waypoint lane || value.Original <= 0 || value.Effective <= 0
                || value.Effective > value.Original || lane.maxSpeed != value.Effective
                || canonical.TryGetValue(lane, out int old) && old != value.Original)
            {
                Verified = false; LastRefusal = "RLO road speed override disagrees with its canonical native lane.";
                throw new InvalidOperationException(LastRefusal);
            }
            detached.Add(lane, value);
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
