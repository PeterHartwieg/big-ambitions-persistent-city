using System;
using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;
using Extensions;
using PersistentCity.Core;
using UnityEngine;
using UnityEngine.AI;

namespace PersistentCity.Runtime
{
    internal sealed class WalkingRoutes : IRoutePlanner, IWalkingQuoteOperation
    {
        public NativeRouteMetrics Metrics { get; } = new NativeRouteMetrics();
        private static readonly double MillisecondsPerTick = 1000.0 / Stopwatch.Frequency;
        private const int CacheLimit = 1024;
        private const int MaxLegs = 512;
        private const float EndpointTolerance = 0.05f;
        private readonly double metresPerGameMinute;
        private readonly Func<Position, Position, bool>? publicSegment;
        private readonly Dictionary<PathKey, CachedPath> cache = new Dictionary<PathKey, CachedPath>();
        private readonly Queue<PathKey> insertionOrder = new Queue<PathKey>();
        private readonly NavMeshPath navigation = new NavMeshPath();
        private Vector3[] corners = new Vector3[64];
        private bool quoteOperation;
        private CachedPath? observed;
        private PathKey observedKey;
        private NavMeshQueryFilter observedFilter;

        public WalkingRoutes(double metresPerGameMinute = 84, Func<Position, Position, bool>? publicSegment = null)
        {
            if (!Position.Finite(metresPerGameMinute) || metresPerGameMinute <= 0) throw new ArgumentOutOfRangeException(nameof(metresPerGameMinute));
            this.metresPerGameMinute = metresPerGameMinute;
            this.publicSegment = publicSegment;
        }

        public void Clear() { DropGeometry(); observed = null; }
        private void DropGeometry() { cache.Clear(); insertionOrder.Clear(); Metrics.WalkingCacheClears++; }

        public void BeginWalkingQuote()
        {
            if (quoteOperation) throw new InvalidOperationException("Walking quote operation cannot be nested.");
            quoteOperation = true; observed = null;
        }
        public void EndWalkingQuote() { quoteOperation = false; observed = null; }

        internal static NavMeshQueryFilter DryFilter
        {
            get
            {
                var filter = NavMeshHelper.NpcNavMeshFilter;
                filter.areaMask &= ~NavMeshHelper.SwimmingAreaMask;
                return filter;
            }
        }

        public Route? Plan(Place from, Place to, double departureMinute, TravelPreferences preferences)
        {
            Metrics.WalkingRequests++;
            if (from == null || to == null || preferences == null || !from.Position.IsFinite || !to.Position.IsFinite
                || !Position.Finite(departureMinute) || departureMinute < 0 || !Position.Finite(preferences.WalkingToleranceMetres) || preferences.WalkingToleranceMetres < 0
                || !FitsVector(from.Position) || !FitsVector(to.Position)) { Metrics.WalkingInvalidInputs++; return null; }
            if (Position.Distance(from.Position, to.Position) > preferences.WalkingToleranceMetres) { Metrics.WalkingDistanceRejects++; return null; }
            var key = new PathKey(from.Position, to.Position);
            if (!cache.TryGetValue(key, out CachedPath? path))
            {
                Metrics.WalkingCacheMisses++; Metrics.WalkingCalculations++;
                long started = Stopwatch.GetTimestamp();
                try { path = Observe(from.Position, to.Position); }
                finally
                {
                    double elapsed = (Stopwatch.GetTimestamp() - started) * MillisecondsPerTick;
                    Metrics.WalkingCalculationMilliseconds += elapsed;
                    Metrics.MaximumWalkingCalculationMilliseconds = Math.Max(Metrics.MaximumWalkingCalculationMilliseconds, elapsed);
                }
                if (path.Route == null) Metrics.WalkingFailedCalculations++;
                if (cache.Count >= CacheLimit) { cache.Remove(insertionOrder.Dequeue()); Metrics.WalkingCacheEvictions++; }
                cache.Add(key, path); insertionOrder.Enqueue(key);
            }
            else
            {
                Metrics.WalkingCacheHits++;
                if (path.Route == null) Metrics.WalkingNegativeCacheHits++;
            }
            if (path.Route == null || path.Metres > preferences.WalkingToleranceMetres) return null;
            var result = new Route();
            foreach (var leg in path.Route.Legs)
            {
                if (publicSegment != null && !publicSegment(leg.From, leg.To)) return null;
                result.Legs.Add(new RouteLeg { From = leg.From, To = leg.To, Minutes = leg.Minutes, Mode = TravelMode.Walk });
            }
            return result;
        }

        private CachedPath Observe(Position from, Position to)
        {
            var filter = DryFilter;
            if (!quoteOperation) return Calculate(from, to, filter);
            var key = new PathKey(from, to);
            if (observed != null && observedKey.Equals(key)
                && observedFilter.agentTypeID == filter.agentTypeID && observedFilter.areaMask == filter.areaMask)
            { Metrics.NavMeshPathReuses++; return observed; }
            observed = null;
            var path = Calculate(from, to, filter);
            observed = path; observedKey = key; observedFilter = filter;
            return path;
        }

        private CachedPath Calculate(Position from, Position to, NavMeshQueryFilter filter)
        {
            if (!Sample(from, out NavMeshHit start, filter)
                || !Sample(to, out NavMeshHit end, filter)
                || !Point(start.position).IsFinite || !Point(end.position).IsFinite
                || Position.Distance(from, Point(start.position)) > EndpointTolerance || Position.Distance(to, Point(end.position)) > EndpointTolerance)
                return new CachedPath(null, 0);
            if (Position.Distance(from, to) < 0.000001)
            {
                var stationary = new Route();
                stationary.Legs.Add(new RouteLeg { From = from, To = to, Minutes = 0.01, Mode = TravelMode.Walk });
                return new CachedPath(stationary, 0);
            }
            Metrics.NavMeshPathQueries++;
            bool calculated;
            long pathStarted = Stopwatch.GetTimestamp();
            try { calculated = NavMesh.CalculatePath(start.position, end.position, filter, navigation); }
            finally
            {
                double elapsed = (Stopwatch.GetTimestamp() - pathStarted) * MillisecondsPerTick;
                Metrics.NavMeshPathMilliseconds += elapsed;
                Metrics.MaximumNavMeshPathMilliseconds = Math.Max(Metrics.MaximumNavMeshPathMilliseconds, elapsed);
            }
            if (!calculated || navigation.status != NavMeshPathStatus.PathComplete) return new CachedPath(null, 0);
            int count = navigation.GetCornersNonAlloc(corners);
            while (count == corners.Length && corners.Length <= MaxLegs + 1)
            {
                corners = new Vector3[corners.Length * 2];
                count = navigation.GetCornersNonAlloc(corners);
            }
            if (count < 2 || count > MaxLegs + 1
                || !Point(corners[0]).IsFinite || !Point(corners[count - 1]).IsFinite
                || Position.Distance(from, Point(corners[0])) > EndpointTolerance || Position.Distance(to, Point(corners[count - 1])) > EndpointTolerance)
                return new CachedPath(null, 0);
            var route = new Route();
            Position previous = from;
            double metres = 0;
            for (int i = 1; i < count; i++)
            {
                Position next = i == count - 1 ? to : Point(corners[i]);
                if (!next.IsFinite) return new CachedPath(null, 0);
                double length = Position.Distance(previous, next);
                if (!Position.Finite(length)) return new CachedPath(null, 0);
                if (length > 0.000001)
                {
                    double minutes = length / metresPerGameMinute;
                    if (!Position.Finite(minutes) || minutes <= 0 || minutes > 1000000) return new CachedPath(null, 0);
                    route.Legs.Add(new RouteLeg { From = previous, To = next, Minutes = minutes, Mode = TravelMode.Walk });
                    metres += length; previous = next;
                }
            }
            if (route.Legs.Count == 0 || route.Legs.Count > MaxLegs || !Position.Finite(metres) || !Position.Finite(route.Minutes)) return new CachedPath(null, 0);
            return new CachedPath(route, metres);
        }

        public bool IsWalkingQuoteValid(Place from, Place to, Route route, TravelPreferences preferences)
        {
            Metrics.WalkingQuoteChecks++;
            long started = Stopwatch.GetTimestamp(); bool valid = false;
            try { valid = ValidateQuote(from, to, route, preferences); return valid; }
            finally
            {
                if (!valid) Metrics.WalkingQuoteRejected++;
                double elapsed = (Stopwatch.GetTimestamp() - started) * MillisecondsPerTick;
                Metrics.WalkingQuoteCheckMilliseconds += elapsed;
                Metrics.MaximumWalkingQuoteCheckMilliseconds = Math.Max(Metrics.MaximumWalkingQuoteCheckMilliseconds, elapsed);
            }
        }

        private bool ValidateQuote(Place from, Place to, Route route, TravelPreferences preferences)
        {
            if (from == null || to == null || route?.Legs == null || preferences == null
                || !from.Position.IsFinite || !to.Position.IsFinite || !FitsVector(from.Position) || !FitsVector(to.Position)
                || !Position.Finite(preferences.WalkingToleranceMetres) || preferences.WalkingToleranceMetres < 0
                || route.Legs.Count == 0 || route.Legs.Count > MaxLegs) return false;
            Position previous = from.Position; double metres = 0;
            foreach (var leg in route.Legs)
            {
                if (leg == null || leg.Mode != TravelMode.Walk || !leg.From.IsFinite || !leg.To.IsFinite
                    || !FitsVector(leg.From) || !FitsVector(leg.To) || Position.Distance(previous, leg.From) > .001
                    || !Position.Finite(leg.Minutes) || leg.Minutes <= 0) return false;
                double length = Position.Distance(leg.From, leg.To);
                double expected = length < .000001 ? .01 : length / metresPerGameMinute;
                if (!Position.Finite(length) || Math.Abs(leg.Minutes - expected) > .000001) return false;
                metres += length;
                if (metres > preferences.WalkingToleranceMetres) return false;
                if (publicSegment != null && !publicSegment(leg.From, leg.To))
                { DropGeometry(); return false; }
                previous = leg.To;
            }
            if (Position.Distance(previous, to.Position) > .001) return false;
            var current = Observe(from.Position, to.Position).Route;
            if (current == null || current.Legs.Count != route.Legs.Count)
            { DropGeometry(); return false; }
            for (int i = 0; i < current.Legs.Count; i++)
            {
                var fresh = current.Legs[i]; var quoted = route.Legs[i];
                if (Position.Distance(fresh.From, quoted.From) > .001 || Position.Distance(fresh.To, quoted.To) > .001
                    || Math.Abs(fresh.Minutes - quoted.Minutes) > .000001
                    || (publicSegment != null && !publicSegment(fresh.From, fresh.To)))
                { DropGeometry(); return false; }
            }
            return true;
        }

        private bool Sample(Position position, out NavMeshHit hit, NavMeshQueryFilter filter)
        {
            Metrics.NavMeshSampleQueries++;
            return NavMesh.SamplePosition(Vector(position), out hit, EndpointTolerance, filter);
        }

        private static Position Point(Vector3 value) => new Position(value.x, value.y, value.z);
        private static Vector3 Vector(Position value) => new Vector3((float)value.X, (float)value.Y, (float)value.Z);
        private static bool FitsVector(Position value) => Math.Abs(value.X) <= float.MaxValue && Math.Abs(value.Y) <= float.MaxValue && Math.Abs(value.Z) <= float.MaxValue;
        private readonly struct PathKey : IEquatable<PathKey>
        {
            private readonly long ax, ay, az, bx, by, bz;
            public PathKey(Position a, Position b)
            {
                ax = BitConverter.DoubleToInt64Bits(a.X); ay = BitConverter.DoubleToInt64Bits(a.Y); az = BitConverter.DoubleToInt64Bits(a.Z);
                bx = BitConverter.DoubleToInt64Bits(b.X); by = BitConverter.DoubleToInt64Bits(b.Y); bz = BitConverter.DoubleToInt64Bits(b.Z);
            }
            public bool Equals(PathKey other) => ax == other.ax && ay == other.ay && az == other.az && bx == other.bx && by == other.by && bz == other.bz;
            public override bool Equals(object? obj) => obj is PathKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    long hash = ax;
                    hash = hash * 31 + ay; hash = hash * 31 + az; hash = hash * 31 + bx; hash = hash * 31 + by; hash = hash * 31 + bz;
                    return (int)hash ^ (int)(hash >> 32);
                }
            }
        }
        private sealed class CachedPath
        {
            public CachedPath(Route? route, double metres) { Route = route; Metres = metres; }
            public Route? Route { get; }
            public double Metres { get; }
        }
    }
}
