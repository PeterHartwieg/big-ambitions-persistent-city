using System;
using System.Collections.Generic;
using System.Threading;

namespace PersistentCity.Core
{
    public static class TransportComposition
    {
        public const int MaximumLegs = 512;
        public const double CarPreparationMinutes = 2;
        public const double CarParkingMinutes = 2;
        public static Route? Join(params Route[] parts)
        {
            var result = new Route();
            double extraMinutes = 0;
            foreach (var part in parts)
            {
                if (part == null) return null;
                foreach (var leg in part.Legs)
                {
                    if (leg == null || !leg.From.IsFinite || !leg.To.IsFinite || !Position.Finite(leg.Minutes) || leg.Minutes <= 0
                        || !Enum.IsDefined(typeof(TravelMode), leg.Mode) || result.Legs.Count >= MaximumLegs) return null;
                    if (result.Legs.Count != 0 && Position.Distance(result.Legs[result.Legs.Count - 1].To, leg.From) > 0.001) return null;
                    result.Legs.Add(new RouteLeg { From = leg.From, To = leg.To, Minutes = leg.Minutes, Mode = leg.Mode, ServiceId = leg.ServiceId });
                }
                if (!Position.Finite(part.EstimatedMinutes) || part.EstimatedMinutes < part.Minutes) return null;
                extraMinutes += part.EstimatedMinutes - part.Minutes;
                if (!Position.Finite(extraMinutes)) return null;
            }
            if (result.Legs.Count == 0 || !Position.Finite(result.Minutes + extraMinutes)) return null;
            return result.WithEstimate(result.Minutes + extraMinutes);
        }

        public static bool Valid(Route route, Position from, Position to, TravelPreferences preferences, Position? parked = null)
        {
            if (route == null || route.Legs.Count == 0 || route.Legs.Count > MaximumLegs || !from.IsFinite || !to.IsFinite
                || preferences == null || !Position.Finite(preferences.WalkingToleranceMetres) || preferences.WalkingToleranceMetres < 0) return false;
            Position previous = from;
            double walking = 0, minutes = 0;
            int carState = 0; bool transit = false;
            foreach (var leg in route.Legs)
            {
                if (leg == null || !leg.From.IsFinite || !leg.To.IsFinite || !Position.Finite(leg.Minutes) || leg.Minutes <= 0
                    || Position.Distance(previous, leg.From) > 0.001 || leg.Mode == TravelMode.Bus || !Enum.IsDefined(typeof(TravelMode), leg.Mode)) return false;
                if (leg.Mode == TravelMode.Car)
                {
                    if (!preferences.OwnsCar || parked == null || carState == 2 || transit) return false;
                    if (carState == 0 && Position.Distance(parked.Value, leg.From) > 0.001) return false;
                    carState = 1;
                }
                else if (carState == 1) carState = 2;
                if (leg.Mode == TravelMode.Subway)
                {
                    if (!preferences.AllowTransit || string.IsNullOrWhiteSpace(leg.ServiceId) || carState != 0) return false;
                    transit = true;
                }
                if (leg.Mode == TravelMode.Walk) walking += Position.Distance(leg.From, leg.To);
                minutes += leg.Minutes; previous = leg.To;
            }
            return Position.Finite(minutes) && Position.Finite(route.EstimatedMinutes) && route.EstimatedMinutes >= minutes
                && Position.Finite(walking) && walking <= preferences.WalkingToleranceMetres
                && Position.Distance(previous, to) <= 0.001;
        }

        public static double Score(Route route, double fare, TravelPreferences preferences, string citizenId, double nonCarRetrievalPenalty = 0)
        {
            return route.EstimatedMinutes;
        }

        public static bool IsFaster(double estimatedMinutes, double incumbentMinutes) =>
            Position.Finite(estimatedMinutes) && estimatedMinutes > 0 && estimatedMinutes < incumbentMinutes;

        public static double RemainingSearchMinutes(double incumbentMinutes, double fixedMinutes)
        {
            if (!Position.Finite(incumbentMinutes) || incumbentMinutes < 0
                || !Position.Finite(fixedMinutes) || fixedMinutes < 0) return double.PositiveInfinity;
            return Math.Max(0, incumbentMinutes - fixedMinutes) + Math.Max(1, incumbentMinutes) * 1e-9;
        }

        public static bool TryAllocateHomeCar(CitizenState citizen, Place from, Place home, Position curb, Route access)
        {
            if (citizen.Car != null || !citizen.Preferences.OwnsCar || citizen.Phase != CitizenPhase.AtPlace
                || citizen.PlaceId != citizen.HomeId || from.Id != citizen.HomeId || home.Id != citizen.HomeId
                || !from.Position.IsFinite || !home.Position.IsFinite || Position.Distance(from.Position, home.Position) > 0.001
                || !curb.IsFinite || Position.Distance(home.Position, curb) > 98
                || !Valid(access, home.Position, curb, citizen.Preferences)) return false;
            foreach (var leg in access.Legs) if (leg.Mode != TravelMode.Walk) return false;
            citizen.Car = new CarState { Id = citizen.Identity.Id + ":car", ParkedPosition = curb };
            return true;
        }

        public static double ModeLowerBound(TravelMode mode, Position from, Position to, TravelPreferences preferences,
            string citizenId, double walkingSpeed, Position? parked = null, double nonCarRetrievalPenalty = 0, double gameMinutesPerScaledSecond = 1)
        {
            if (!Position.Finite(gameMinutesPerScaledSecond) || gameMinutesPerScaledSecond <= 0)
                throw new ArgumentOutOfRangeException(nameof(gameMinutesPerScaledSecond));
            if (mode == TravelMode.Subway)
                return 3 + Position.Distance(from, to) / Math.Max(10 / gameMinutesPerScaledSecond, walkingSpeed);
            if (mode == TravelMode.Car)
            {
                double maxSpeed = Math.Max(40.0 / 3.6 / gameMinutesPerScaledSecond, walkingSpeed);
                double travel = parked.HasValue ? Position.Distance(from, parked.Value) / walkingSpeed
                    + Position.Distance(parked.Value, to) / maxSpeed : Position.Distance(from, to) / maxSpeed;
                return CarPreparationMinutes + CarParkingMinutes + travel;
            }
            return Position.Distance(from, to) / walkingSpeed;
        }
        public const double SubwayFare = 3;
    }

    public static class TransportAccessGeometry
    {
        /// <summary>Intersection with one bounded conservative gate region; an unknown gate never denies
        /// unrelated city coordinates. Touching its boundary is conservatively treated as a crossing.</summary>
        public static bool IntersectsBounds(Position a, Position b, Position min, Position max)
        {
            if (Math.Max(a.X, b.X) < min.X || Math.Min(a.X, b.X) > max.X
                || Math.Max(a.Z, b.Z) < min.Z || Math.Min(a.Z, b.Z) > max.Z
                || Math.Max(a.Y, b.Y) < min.Y || Math.Min(a.Y, b.Y) > max.Y) return false;
            double enter = 0, exit = 1;
            return Slab(a.X, b.X - a.X, min.X, max.X, ref enter, ref exit)
                && Slab(a.Y, b.Y - a.Y, min.Y, max.Y, ref enter, ref exit)
                && Slab(a.Z, b.Z - a.Z, min.Z, max.Z, ref enter, ref exit);
        }
        private static bool Slab(double start, double delta, double minimum, double maximum, ref double enter, ref double exit)
        {
            if (Math.Abs(delta) < 0.000000001) return start >= minimum && start <= maximum;
            double a = (minimum - start) / delta, b = (maximum - start) / delta;
            enter = Math.Max(enter, Math.Min(a, b)); exit = Math.Min(exit, Math.Max(a, b));
            return enter <= exit;
        }
    }

    public sealed class TransportRoadNode
    {
        public Position Position { get; set; }
        public int[] Successors { get; set; } = Array.Empty<int>();
        public double KilometresPerHour { get; set; }
        public bool AllowsCar { get; set; }
    }

    public sealed class TransportRoadGraph
    {
        private readonly IReadOnlyList<TransportRoadNode> nodes;
        private readonly double clockRate;
        private readonly RoadSearchWorkspace workspace = new RoadSearchWorkspace();
        private int workspaceInUse;
        public TransportRoadGraph(IReadOnlyList<TransportRoadNode> nodes, double gameMinutesPerScaledSecond = 1)
        {
            this.nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
            if (!Position.Finite(gameMinutesPerScaledSecond) || gameMinutesPerScaledSecond <= 0)
                throw new ArgumentOutOfRangeException(nameof(gameMinutesPerScaledSecond));
            clockRate = gameMinutesPerScaledSecond;
        }
        public Route? Plan(int start, int goal, Func<int, bool> disabled, Func<Position, Position, bool> clear,
            int expansionLimit, Action<int[]>? pathFound) =>
            Plan(start, goal, disabled, clear, expansionLimit, pathFound, null, null);

        public Route? Plan(int start, int goal, Func<int, bool> disabled, Func<Position, Position, bool> clear,
            int expansionLimit, Action<int[]>? pathFound, Func<int, int, double>? extraMinutes, Action<double>? estimateFound) =>
            Plan(start, goal, disabled, clear, expansionLimit, pathFound, extraMinutes, estimateFound, double.PositiveInfinity);

        public Route? Plan(int start, int goal, Func<int, bool> disabled, Func<Position, Position, bool> clear, int expansionLimit = 30000,
            Action<int[]>? pathFound = null, Func<int, int, double>? extraMinutes = null, Action<double>? estimateFound = null,
            double maximumEstimatedMinutes = double.PositiveInfinity)
            => PlanInternal(start, goal, disabled, clear, null, expansionLimit, pathFound, extraMinutes, estimateFound, maximumEstimatedMinutes);

        public Route? PlanWithTraversal(int start, int goal, Func<int, bool> disabled, Func<Position, Position, bool> clear,
            Func<int, int, double, double> traversalMinutes, int expansionLimit,
            Action<int[]>? pathFound, Func<int, int, double>? extraMinutes, Action<double>? estimateFound) =>
            PlanWithTraversal(start, goal, disabled, clear, traversalMinutes, expansionLimit, pathFound, extraMinutes, estimateFound, double.PositiveInfinity);

        public Route? PlanWithTraversal(int start, int goal, Func<int, bool> disabled, Func<Position, Position, bool> clear,
            Func<int, int, double, double> traversalMinutes, int expansionLimit = 30000,
            Action<int[]>? pathFound = null, Func<int, int, double>? extraMinutes = null, Action<double>? estimateFound = null,
            double maximumEstimatedMinutes = double.PositiveInfinity)
        {
            if (traversalMinutes == null) throw new ArgumentNullException(nameof(traversalMinutes));
            return PlanInternal(start, goal, disabled, clear, traversalMinutes, expansionLimit, pathFound, extraMinutes, estimateFound, maximumEstimatedMinutes);
        }
        private Route? PlanInternal(int start, int goal, Func<int, bool> disabled, Func<Position, Position, bool> clear,
            Func<int, int, double, double>? traversalMinutes, int expansionLimit, Action<int[]>? pathFound,
            Func<int, int, double>? extraMinutes, Action<double>? estimateFound, double maximumEstimatedMinutes)
        {
            using var search = BeginSearch(start, goal, disabled, clear, traversalMinutes, expansionLimit, pathFound,
                extraMinutes, estimateFound, maximumEstimatedMinutes);
            search.Advance(int.MaxValue);
            return search.Result;
        }

        public RoadSearch BeginSearch(int start, int goal, Func<int, bool> disabled, Func<Position, Position, bool> clear,
            Func<int, int, double, double>? traversalMinutes = null, int expansionLimit = 30000, Action<int[]>? pathFound = null,
            Func<int, int, double>? extraMinutes = null, Action<double>? estimateFound = null,
            double maximumEstimatedMinutes = double.PositiveInfinity)
        {
            if (double.IsNaN(maximumEstimatedMinutes) || maximumEstimatedMinutes < 0)
                throw new ArgumentOutOfRangeException(nameof(maximumEstimatedMinutes));
            if (disabled == null) throw new ArgumentNullException(nameof(disabled));
            if (clear == null) throw new ArgumentNullException(nameof(clear));
            if (!Usable(start) || !Usable(goal) || start == goal || disabled(start) || disabled(goal)) return new RoadSearch(this);
            bool borrowed = Interlocked.CompareExchange(ref workspaceInUse, 1, 0) == 0;
            return new RoadSearch(this, start, goal, disabled, clear, traversalMinutes, expansionLimit, pathFound, extraMinutes, estimateFound,
                TransportComposition.RemainingSearchMinutes(maximumEstimatedMinutes, 0), borrowed ? workspace : new RoadSearchWorkspace(), borrowed);
        }
        private bool Usable(int index) => index >= 0 && index < nodes.Count && nodes[index] != null && nodes[index].AllowsCar
            && nodes[index].Position.IsFinite && Position.Finite(nodes[index].KilometresPerHour) && nodes[index].KilometresPerHour > 0;

        public sealed class RoadSearch : IDisposable
        {
            private readonly TransportRoadGraph graph;
            private readonly int start, goal, expansionLimit;
            private readonly Func<int, bool> disabled;
            private readonly Func<Position, Position, bool> clear;
            private readonly Func<int, int, double, double>? traversalMinutes;
            private readonly Action<int[]>? pathFound;
            private readonly Func<int, int, double>? extraMinutes;
            private readonly Action<double>? estimateFound;
            private readonly double maximumEstimatedMinutes;
            private readonly RoadSearchWorkspace? search;
            private readonly bool borrowed;
            private readonly Dictionary<(int From, int To), (double Baseline, double Nominal, double Extra)>? costs;
            private int expanded;
            private bool released;
            public bool Finished { get; private set; }
            public Route? Result { get; private set; }
            public int Expanded => expanded;

            internal RoadSearch(TransportRoadGraph graph)
            {
                this.graph = graph; disabled = _ => true; clear = (_, _) => false; Finished = true; released = true;
            }
            internal RoadSearch(TransportRoadGraph graph, int start, int goal, Func<int, bool> disabled, Func<Position, Position, bool> clear,
                Func<int, int, double, double>? traversalMinutes, int expansionLimit, Action<int[]>? pathFound, Func<int, int, double>? extraMinutes,
                Action<double>? estimateFound, double maximumEstimatedMinutes, RoadSearchWorkspace search, bool borrowed)
            {
                this.graph = graph; this.start = start; this.goal = goal; this.disabled = disabled; this.clear = clear;
                this.traversalMinutes = traversalMinutes; this.expansionLimit = expansionLimit; this.pathFound = pathFound;
                this.extraMinutes = extraMinutes; this.estimateFound = estimateFound; this.maximumEstimatedMinutes = maximumEstimatedMinutes;
                this.search = search; this.borrowed = borrowed;
                search.Begin(graph.nodes.Count);
                costs = extraMinutes == null && traversalMinutes == null ? null
                    : new Dictionary<(int From, int To), (double Baseline, double Nominal, double Extra)>();
                search.SetDistance(start, 0, -1);
                search.Add((Heuristic(start), 0, start));
            }

            private double Heuristic(int node) => Position.Distance(graph.nodes[node].Position, graph.nodes[goal].Position) / (40.0 / 3.6 / graph.clockRate);

            public bool Advance(int maximumExpansions)
            {
                if (maximumExpansions < 1) throw new ArgumentOutOfRangeException(nameof(maximumExpansions));
                if (Finished) return true;
                int remaining = maximumExpansions;
                while (search!.Count != 0 && expanded++ < expansionLimit)
                {
                    if (ExpandOne()) return true;
                    if (--remaining == 0) return false;
                }
                return Finish(null);
            }

            private bool Finish(Route? route)
            {
                Finished = true; Result = route;
                Release();
                return true;
            }

            private bool ExpandOne()
            {
                var nodes = graph.nodes; var search = this.search!;
                var current = search.Pop();
                if (current.Estimate > maximumEstimatedMinutes) return Finish(null);
                if (current.Node == goal)
                {
                    var path = new List<int> { goal };
                    int cursor = goal;
                    while (cursor != start)
                    {
                        if (!search.TryGetPredecessor(cursor, out cursor) || path.Count >= TransportComposition.MaximumLegs) return Finish(null);
                        path.Add(cursor);
                    }
                    path.Reverse();
                    var route = new Route();
                    for (int k = 1; k < path.Count; k++)
                    {
                        var a = nodes[path[k - 1]]; var b = nodes[path[k]];
                        if (disabled(path[k - 1]) || disabled(path[k]) || !clear(a.Position, b.Position)) return Finish(null);
                        double baseline = Position.Distance(a.Position, b.Position) / (Math.Min(40, a.KilometresPerHour) / 3.6 / graph.clockRate);
                        double nominal = baseline;
                        if (traversalMinutes != null && (!Position.Finite(baseline) || baseline <= 0)) return Finish(null);
                        if (costs != null)
                        {
                            if (!costs.TryGetValue((path[k - 1], path[k]), out var captured) || baseline != captured.Baseline) return Finish(null);
                            nominal = traversalMinutes == null ? baseline : traversalMinutes(path[k - 1], path[k], baseline);
                            double currentExtra = extraMinutes == null ? 0 : extraMinutes(path[k - 1], path[k]);
                            if ((traversalMinutes != null && (!Position.Finite(nominal) || nominal < baseline || nominal <= 0))
                                || nominal != captured.Nominal || !Position.Finite(currentExtra) || currentExtra < 0
                                || currentExtra != captured.Extra) return Finish(null);
                        }
                        route.Legs.Add(new RouteLeg { From = a.Position, To = b.Position, Mode = TravelMode.Car,
                            Minutes = nominal });
                    }
                    if (extraMinutes != null) route.WithEstimate(current.Cost);
                    pathFound?.Invoke(path.ToArray());
                    estimateFound?.Invoke(route.EstimatedMinutes);
                    return Finish(route);
                }
                if (disabled(current.Node)) return false;
                var node = nodes[current.Node];
                foreach (int next in node.Successors)
                {
                    if (!graph.Usable(next) || disabled(next)) continue;
                    var target = nodes[next];
                    double metres = Position.Distance(node.Position, target.Position);
                    if (!Position.Finite(metres) || metres <= 0.000001 || metres > 60 || !clear(node.Position, target.Position)) continue;
                    double baseline = metres / (Math.Min(40, node.KilometresPerHour) / 3.6 / graph.clockRate);
                    double nominal = baseline, extra = 0;
                    if (traversalMinutes != null && (!Position.Finite(baseline) || baseline <= 0)) return Finish(null);
                    if (costs != null)
                    {
                        if (costs.TryGetValue((current.Node, next), out var captured))
                        {
                            if (captured.Baseline != baseline) return Finish(null);
                            nominal = captured.Nominal; extra = captured.Extra;
                        }
                        else
                        {
                            nominal = traversalMinutes == null ? baseline : traversalMinutes(current.Node, next, baseline);
                            extra = extraMinutes == null ? 0 : extraMinutes(current.Node, next);
                            if ((traversalMinutes != null && (!Position.Finite(nominal) || nominal < baseline || nominal <= 0))
                                || !Position.Finite(extra) || extra < 0) return Finish(null);
                            costs.Add((current.Node, next), (baseline, nominal, extra));
                        }
                    }
                    double cost = current.Cost + nominal + extra;
                    if (costs != null && !Position.Finite(cost)) return Finish(null);
                    if (search.TryGetDistance(next, out double old))
                    {
                        if (old <= cost) continue;
                        search.Remove((old + Heuristic(next), old, next));
                    }
                    search.SetDistance(next, cost, current.Node); search.Add((cost + Heuristic(next), cost, next));
                }
                return false;
            }

            private void Release()
            {
                if (released) return;
                released = true;
                if (borrowed) Volatile.Write(ref graph.workspaceInUse, 0);
            }

            public void Dispose()
            {
                if (!Finished) { Finished = true; Result = null; }
                Release();
            }
        }
    }
}
