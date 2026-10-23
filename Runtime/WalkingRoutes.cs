using System;
using System.Collections.Generic;
using System.Globalization;
using Extensions;
using PersistentCity.Core;
using UnityEngine;
using UnityEngine.AI;

namespace PersistentCity.Runtime
{
    /// <summary>Complete NPC NavMesh paths only; no agents, actor movement or direct-line fallback. Main thread only.</summary>
    internal sealed class WalkingRoutes : IRoutePlanner
    {
        private const int CacheLimit = 1024;
        private const int MaxLegs = 512;
        private const float EndpointTolerance = 0.05f;
        private readonly double metresPerGameMinute;
        private readonly Func<Position, Position, bool>? publicSegment;
        private readonly Dictionary<string, CachedPath> cache = new Dictionary<string, CachedPath>(StringComparer.Ordinal);
        private readonly Queue<string> insertionOrder = new Queue<string>();

        public WalkingRoutes(double metresPerGameMinute = 84, Func<Position, Position, bool>? publicSegment = null)
        {
            if (!Position.Finite(metresPerGameMinute) || metresPerGameMinute <= 0) throw new ArgumentOutOfRangeException(nameof(metresPerGameMinute));
            this.metresPerGameMinute = metresPerGameMinute;
            this.publicSegment = publicSegment;
        }

        /// <summary>Invalidate on city reload or navigation changes; no cached route is persistent state.</summary>
        public void Clear() { cache.Clear(); insertionOrder.Clear(); }

        internal static NavMeshQueryFilter DryFilter
        {
            get
            {
                var filter = NavMeshHelper.NpcNavMeshFilter;
                // Native NPC defaults include every area; walking must not cross the
                // game's separately named Swimmable polygons.
                filter.areaMask &= ~NavMeshHelper.SwimmingAreaMask;
                return filter;
            }
        }

        public Route? Plan(Place from, Place to, double departureMinute, TravelPreferences preferences)
        {
            if (from == null || to == null || preferences == null || !from.Position.IsFinite || !to.Position.IsFinite
                || !Position.Finite(departureMinute) || departureMinute < 0 || !Position.Finite(preferences.WalkingToleranceMetres) || preferences.WalkingToleranceMetres < 0
                || !FitsVector(from.Position) || !FitsVector(to.Position)) return null;
            // Geometric lower bound avoids native path work for walks already outside the citizen's limit.
            if (Position.Distance(from.Position, to.Position) > preferences.WalkingToleranceMetres) return null;
            string key = PointKey(from.Position) + ">" + PointKey(to.Position);
            if (!cache.TryGetValue(key, out CachedPath? path))
            {
                path = Calculate(from.Position, to.Position);
                if (cache.Count >= CacheLimit) cache.Remove(insertionOrder.Dequeue());
                cache.Add(key, path); insertionOrder.Enqueue(key);
            }
            if (path.Route == null || path.Metres > preferences.WalkingToleranceMetres) return null;
            // Core owns returned routes; callers can change them without corrupting the geometry cache.
            var result = new Route();
            foreach (var leg in path.Route.Legs)
            {
                // Recheck current public access even for cached geometry; NavMesh alone need not encode a gate.
                if (publicSegment != null && !publicSegment(leg.From, leg.To)) return null;
                result.Legs.Add(new RouteLeg { From = leg.From, To = leg.To, Minutes = leg.Minutes, Mode = TravelMode.Walk });
            }
            return result;
        }

        private CachedPath Calculate(Position from, Position to)
        {
            var filter = DryFilter;
            if (!NavMesh.SamplePosition(Vector(from), out NavMeshHit start, EndpointTolerance, filter)
                || !NavMesh.SamplePosition(Vector(to), out NavMeshHit end, EndpointTolerance, filter)
                || !Point(start.position).IsFinite || !Point(end.position).IsFinite
                || Position.Distance(from, Point(start.position)) > EndpointTolerance || Position.Distance(to, Point(end.position)) > EndpointTolerance)
                return new CachedPath(null, 0);
            if (Position.Distance(from, to) < 0.000001)
            {
                var stationary = new Route();
                stationary.Legs.Add(new RouteLeg { From = from, To = to, Minutes = 0.01, Mode = TravelMode.Walk });
                return new CachedPath(stationary, 0);
            }
            var navigation = new NavMeshPath();
            if (!NavMesh.CalculatePath(start.position, end.position, filter, navigation) || navigation.status != NavMeshPathStatus.PathComplete)
                return new CachedPath(null, 0);
            Vector3[] corners = navigation.corners;
            if (corners == null || corners.Length < 2 || corners.Length > MaxLegs + 1
                || !Point(corners[0]).IsFinite || !Point(corners[corners.Length - 1]).IsFinite
                || Position.Distance(from, Point(corners[0])) > EndpointTolerance || Position.Distance(to, Point(corners[corners.Length - 1])) > EndpointTolerance)
                return new CachedPath(null, 0);
            var route = new Route();
            Position previous = from;
            double metres = 0;
            for (int i = 1; i < corners.Length; i++)
            {
                Position next = i == corners.Length - 1 ? to : Point(corners[i]);
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

        private static Position Point(Vector3 value) => new Position(value.x, value.y, value.z);
        private static Vector3 Vector(Position value) => new Vector3((float)value.X, (float)value.Y, (float)value.Z);
        private static bool FitsVector(Position value) => Math.Abs(value.X) <= float.MaxValue && Math.Abs(value.Y) <= float.MaxValue && Math.Abs(value.Z) <= float.MaxValue;
        private static string PointKey(Position value) => value.X.ToString("R", CultureInfo.InvariantCulture) + "," + value.Y.ToString("R", CultureInfo.InvariantCulture) + "," + value.Z.ToString("R", CultureInfo.InvariantCulture);
        private sealed class CachedPath
        {
            public CachedPath(Route? route, double metres) { Route = route; Metres = metres; }
            public Route? Route { get; }
            public double Metres { get; }
        }
    }
}
