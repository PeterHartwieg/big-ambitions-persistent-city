using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public sealed class GraphNode
    {
        public GraphNode(string id, Position position) { Id = id; Position = position; }
        public string Id { get; }
        public Position Position { get; }
    }

    public sealed class GraphEdge
    {
        public GraphEdge(string id, string fromNodeId, string toNodeId, TravelMode mode,
            double minutes, double fare = 0, double headwayMinutes = 0, string serviceId = "")
        {
            Id = id; FromNodeId = fromNodeId; ToNodeId = toNodeId; Mode = mode;
            Minutes = minutes; Fare = fare; HeadwayMinutes = headwayMinutes; ServiceId = serviceId;
        }
        public string Id { get; }
        public string FromNodeId { get; }
        public string ToNodeId { get; }
        public TravelMode Mode { get; }
        public double Minutes { get; }
        public double Fare { get; }
        public double HeadwayMinutes { get; }
        public string ServiceId { get; }
    }

    /// <summary>
    /// Offline directed graph prototype, not a game road or transit adapter. Places must use graph
    /// node IDs and positions. Walking tolerance limits total walked metres. Transit uses expected
    /// headway / 2 boarding waits (no timetable), and fares apply once per continuous service.
    /// A car must be used for the entire journey. Unknown, disconnected and same-node trips return null.
    /// </summary>
    public sealed class GraphRoutePlanner : IRoutePlanner
    {
        private readonly Dictionary<string, GraphNode> nodes = new Dictionary<string, GraphNode>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<GraphEdge>> outgoing = new Dictionary<string, List<GraphEdge>>(StringComparer.Ordinal);

        public GraphRoutePlanner(IEnumerable<GraphNode> nodes, IEnumerable<GraphEdge> edges)
        {
            if (nodes == null) throw new ArgumentNullException(nameof(nodes));
            if (edges == null) throw new ArgumentNullException(nameof(edges));
            foreach (var node in nodes)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.Id) || !node.Position.IsFinite)
                    throw new ArgumentException("Nodes need unique nonempty IDs and finite positions.", nameof(nodes));
                if (this.nodes.ContainsKey(node.Id)) throw new ArgumentException("Duplicate node ID.", nameof(nodes));
                this.nodes.Add(node.Id, node);
                outgoing.Add(node.Id, new List<GraphEdge>());
            }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var edge in edges)
            {
                if (edge == null || string.IsNullOrWhiteSpace(edge.Id) || !ids.Add(edge.Id)
                    || edge.FromNodeId == null || !this.nodes.ContainsKey(edge.FromNodeId)
                    || edge.ToNodeId == null || !this.nodes.ContainsKey(edge.ToNodeId)
                    || !Enum.IsDefined(typeof(TravelMode), edge.Mode)
                    || !Position.Finite(edge.Minutes) || edge.Minutes <= 0
                    || !NonnegativeFinite(edge.Fare) || !NonnegativeFinite(edge.HeadwayMinutes)
                    || (Transit(edge.Mode) && string.IsNullOrWhiteSpace(edge.ServiceId))
                    || (!Transit(edge.Mode) && (edge.HeadwayMinutes != 0 || !string.IsNullOrEmpty(edge.ServiceId))))
                    throw new ArgumentException("Edges need unique IDs, existing endpoints, positive travel time and valid costs/service metadata.", nameof(edges));
                if (!Position.Finite(Position.Distance(this.nodes[edge.FromNodeId].Position, this.nodes[edge.ToNodeId].Position)))
                    throw new ArgumentException("Edge distance exceeds the finite coordinate range.", nameof(edges));
                outgoing[edge.FromNodeId].Add(edge);
            }
            foreach (var list in outgoing.Values) list.Sort((a, b) => StringComparer.Ordinal.Compare(a.Id, b.Id));
        }

        public Route? Plan(Place from, Place to, double departureMinute, TravelPreferences preferences)
        {
            if (from == null) throw new ArgumentNullException(nameof(from));
            if (to == null) throw new ArgumentNullException(nameof(to));
            if (preferences == null) throw new ArgumentNullException(nameof(preferences));
            if (!NonnegativeFinite(departureMinute) || !from.Position.IsFinite || !to.Position.IsFinite
                || !NonnegativeFinite(preferences.WalkingToleranceMetres) || !NonnegativeFinite(preferences.FareWeightMinutes))
                throw new ArgumentException("Departure, positions and preference costs must be finite and nonnegative.");
            if (from.Id == null || to.Id == null || !nodes.TryGetValue(from.Id, out var origin) || !nodes.TryGetValue(to.Id, out var destination)) return null;
            if (!SamePosition(from.Position, origin.Position) || !SamePosition(to.Position, destination.Position))
                throw new ArgumentException("Place positions must match their graph nodes; implicit connector legs are unsupported.");
            if (origin.Id == destination.Id) return null;

            // Labels retain nondominated (generalized time, walked metres) alternatives. A single
            // shortest label per node would incorrectly discard feasible routes under a walk budget.
            var frontier = new List<Label>();
            var labels = new Dictionary<State, List<Label>>();
            var initial = new Label(new State(origin.Id, 0, null, ""), 0, 0, null, null, 0, new List<string>());
            frontier.Add(initial);
            labels.Add(initial.State, new List<Label> { initial });
            while (frontier.Count != 0)
            {
                frontier.Sort(CompareLabels);
                var current = frontier[0];
                frontier.RemoveAt(0);
                if (current.Discarded) continue;
                if (current.State.NodeId == destination.Id) return BuildRoute(current);
                foreach (var edge in outgoing[current.State.NodeId])
                {
                    bool car = edge.Mode == TravelMode.Car;
                    if (car && (!preferences.OwnsCar || current.State.JourneyKind == 2)) continue;
                    if (!car && current.State.JourneyKind == 1) continue;
                    if (Transit(edge.Mode) && !preferences.AllowTransit) continue;
                    bool boarding = Transit(edge.Mode) && (current.State.TransitMode != edge.Mode || current.State.ServiceId != edge.ServiceId);
                    double wait = boarding ? edge.HeadwayMinutes / 2 : 0;
                    double fare = !Transit(edge.Mode) || boarding ? edge.Fare : 0;
                    double cost = current.Cost + edge.Minutes + wait + fare * preferences.FareWeightMinutes;
                    if (!Position.Finite(cost)) throw new ArgumentException("Route costs exceed the finite numeric range.");
                    double walking = current.Walking + (edge.Mode == TravelMode.Walk ? Position.Distance(nodes[edge.FromNodeId].Position, nodes[edge.ToNodeId].Position) : 0);
                    if (walking > preferences.WalkingToleranceMetres || !Position.Finite(walking)) continue;
                    var state = new State(edge.ToNodeId, car ? 1 : 2, Transit(edge.Mode) ? edge.Mode : (TravelMode?)null, Transit(edge.Mode) ? edge.ServiceId : "");
                    var path = new List<string>(current.Path) { edge.Id };
                    var next = new Label(state, cost, walking, current, edge, wait, path);
                    if (!labels.TryGetValue(state, out var peers)) { peers = new List<Label>(); labels.Add(state, peers); }
                    bool dominated = false;
                    foreach (var peer in peers) if (!peer.Discarded && Dominates(peer, next)) { dominated = true; break; }
                    if (dominated) continue;
                    foreach (var peer in peers) if (!peer.Discarded && Dominates(next, peer)) peer.Discarded = true;
                    peers.Add(next);
                    frontier.Add(next);
                }
            }
            return null;
        }

        private Route BuildRoute(Label final)
        {
            var steps = new List<Label>();
            for (var step = final; step.Edge != null; step = step.Previous!) steps.Add(step);
            steps.Reverse();
            var route = new Route();
            foreach (var step in steps)
            {
                var edge = step.Edge!;
                var from = nodes[edge.FromNodeId].Position;
                if (step.Wait > 0) route.Legs.Add(new RouteLeg { From = from, To = from, Mode = edge.Mode, Minutes = step.Wait, ServiceId = edge.ServiceId });
                route.Legs.Add(new RouteLeg { From = from, To = nodes[edge.ToNodeId].Position, Mode = edge.Mode, Minutes = edge.Minutes, ServiceId = edge.ServiceId });
            }
            return route;
        }

        private static bool NonnegativeFinite(double value) => Position.Finite(value) && value >= 0;
        private static bool Transit(TravelMode mode) => mode == TravelMode.Bus || mode == TravelMode.Subway;
        private static bool SamePosition(Position a, Position b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;
        private static bool Dominates(Label a, Label b) => a.Walking <= b.Walking && (a.Cost < b.Cost || (a.Cost == b.Cost && ComparePaths(a.Path, b.Path) <= 0));
        private static int CompareLabels(Label a, Label b)
        {
            int cost = a.Cost.CompareTo(b.Cost);
            return cost != 0 ? cost : ComparePaths(a.Path, b.Path);
        }
        private static int ComparePaths(List<string> a, List<string> b)
        {
            for (int i = 0; i < Math.Min(a.Count, b.Count); i++)
            {
                int comparison = StringComparer.Ordinal.Compare(a[i], b[i]);
                if (comparison != 0) return comparison;
            }
            return a.Count.CompareTo(b.Count);
        }

        private readonly struct State : IEquatable<State>
        {
            public State(string nodeId, int journeyKind, TravelMode? transitMode, string serviceId)
            { NodeId = nodeId; JourneyKind = journeyKind; TransitMode = transitMode; ServiceId = serviceId; }
            public string NodeId { get; }
            public int JourneyKind { get; } // 0 = start, 1 = car, 2 = walking/transit
            public TravelMode? TransitMode { get; }
            public string ServiceId { get; }
            public bool Equals(State other) => NodeId == other.NodeId && JourneyKind == other.JourneyKind && TransitMode == other.TransitMode && ServiceId == other.ServiceId;
            public override bool Equals(object? obj) => obj is State state && Equals(state);
            public override int GetHashCode() => HashCode.Combine(NodeId, JourneyKind, TransitMode, ServiceId);
        }
        private sealed class Label
        {
            public Label(State state, double cost, double walking, Label? previous, GraphEdge? edge, double wait, List<string> path)
            { State = state; Cost = cost; Walking = walking; Previous = previous; Edge = edge; Wait = wait; Path = path; }
            public State State { get; }
            public double Cost { get; }
            public double Walking { get; }
            public Label? Previous { get; }
            public GraphEdge? Edge { get; }
            public double Wait { get; }
            public List<string> Path { get; }
            public bool Discarded { get; set; }
        }
    }
}
