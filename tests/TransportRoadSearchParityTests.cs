using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class TransportRoadSearchParityTests
{
    private delegate Route? Planner(int start, int goal, Func<int, bool> disabled,
        Func<Position, Position, bool> clear, int limit, Action<int[]>? found);
    private sealed record Result(Route? Route, int[]? Path,
        List<(int Kind, int Node, Position From, Position To)> Calls);

    [Theory]
    [InlineData(0.25)]
    [InlineData(1)]
    [InlineData(3.75)]
    [InlineData(1e300)]
    [InlineData(1e308)]
    [InlineData(double.Epsilon)]
    public void DirectedSearchMatchesOriginalAcrossVariedGraphsAndExpansionLimits(double clockRate)
    {
        for (int seed = 0; seed < 14; seed++)
        {
            var random = new Random(seed + 41019);
            var nodes = Enumerable.Range(0, 64).Select(i => Node(i % 8 * 10, i / 8 * 10,
                new[] { (i + 1) % 64, random.Next(64), random.Next(64), i, -1, 65 })).ToArray();
            for (int i = 1; i < nodes.Length - 1; i++)
            {
                nodes[i].KilometresPerHour = new[] { 5.0, 10, 20, 30, 40, 80, 0, double.NaN }[random.Next(8)];
                nodes[i].AllowsCar = random.Next(9) != 0;
            }
            var graph = new TransportRoadGraph(nodes, clockRate);
            var baseline = new BaselineRoadGraph(nodes, clockRate);
            bool Disabled(int index) => (index + seed) % 17 == 3;
            bool Clear(Position a, Position b) => !TransportAccessGeometry.IntersectsBounds(a, b,
                new Position(34, -1, 15 + seed), new Position(36, 1, 45 + seed));
            foreach (int limit in new[] { -1, 0, 1, 2, 3, 7, 16, 63, 64, 512 })
                foreach (var pair in new[] { (0, 63), (63, 0), (16, 45) })
                    AssertSame(Capture(baseline.Plan, pair.Item1, pair.Item2, Disabled, Clear, limit),
                        Capture(graph.Plan, pair.Item1, pair.Item2, Disabled, Clear, limit));
        }
    }

    [Fact]
    public void EqualPrioritiesChooseNodeIdAndKeepTheFirstEqualCostPredecessor()
    {
        var nodes = new[] { Node(0, 0, new[] { 2, 1 }), Node(10, 10, new[] { 3 }),
            Node(10, -10, new[] { 3 }), Node(20, 0) };
        var graph = new TransportRoadGraph(nodes);
        var baseline = new BaselineRoadGraph(nodes);
        var expected = Capture(baseline.Plan, 0, 3);
        Assert.Equal(new[] { 0, 1, 3 }, expected.Path);
        for (int limit = 0; limit <= 5; limit++)
            AssertSame(Capture(baseline.Plan, 0, 3, limit: limit), Capture(graph.Plan, 0, 3, limit: limit));
        AssertSame(expected, Capture(graph.Plan, 0, 3));
        nodes[0].Successors = new[] { 1, 2 };
        AssertSame(Capture(baseline.Plan, 0, 3), Capture(graph.Plan, 0, 3));
        Assert.Equal(expected.Path, Capture(graph.Plan, 0, 3).Path);
    }

    [Fact]
    public void EqualEstimatesCompareAccumulatedCostBeforeNodeId()
    {
        var nodes = new[] { Node(0, 0, new[] { 1, 2 }), Node(20, 0, new[] { 3 }),
            Node(10, 0, new[] { 3 }), Node(40, 0) };
        // This rate makes both heuristic and edge metres-per-minute exactly one,
        // so priorities are exactly (40,20,1) and (40,10,2).
        double rate = 40.0 / 3.6;
        var graph = new TransportRoadGraph(nodes, rate);
        var baseline = new BaselineRoadGraph(nodes, rate);
        for (int limit = 0; limit <= 5; limit++)
            AssertSame(Capture(baseline.Plan, 0, 3, limit: limit), Capture(graph.Plan, 0, 3, limit: limit));
        Assert.Equal(new[] { 0, 2, 3 }, Capture(graph.Plan, 0, 3).Path);
    }

    [Fact]
    public void ImprovedFrontierCostReplacesItsPriorityAndCyclesDoNotChangeItsPredecessor()
    {
        var nodes = CostUpdateNodes();
        var graph = new TransportRoadGraph(nodes);
        var baseline = new BaselineRoadGraph(nodes);
        for (int limit = 0; limit <= 7; limit++)
            AssertSame(Capture(baseline.Plan, 0, 4, limit: limit), Capture(graph.Plan, 0, 4, limit: limit));
        Assert.Equal(new[] { 0, 2, 3, 4 }, Capture(graph.Plan, 0, 4).Path);
    }

    [Fact]
    public void GoalConsumesAnExpansionAndTheOriginalPathLengthRefusalIsPreserved()
    {
        var nodes = Enumerable.Range(0, 514).Select(i => Node(i, 0, new[] { i + 1 })).ToArray();
        nodes[nodes.Length - 1].Successors = Array.Empty<int>();
        var graph = new TransportRoadGraph(nodes);
        var baseline = new BaselineRoadGraph(nodes);
        foreach (int goal in new[] { 1, 510, 511, 512, 513 })
            foreach (int limit in new[] { goal - 1, goal, goal + 1, 1000 })
                AssertSame(Capture(baseline.Plan, 0, goal, limit: limit), Capture(graph.Plan, 0, goal, limit: limit));
        Assert.Null(Capture(graph.Plan, 0, 1, limit: 1).Route);
        Assert.NotNull(Capture(graph.Plan, 0, 1, limit: 2).Route);
        Assert.Equal(511, Capture(graph.Plan, 0, 511).Route!.Legs.Count);
        Assert.Null(Capture(graph.Plan, 0, 512).Route);
    }

    [Fact]
    public void FreshQueriesObserveClosureReopeningGatePermissionsSpeedGeometryAndTopology()
    {
        var nodes = new List<TransportRoadNode> { Node(0, 0, new[] { 1, 2 }), Node(20, 0, new[] { 3 }),
            Node(20, 20, new[] { 3 }), Node(40, 0) };
        var graph = new TransportRoadGraph(nodes);
        var baseline = new BaselineRoadGraph(nodes);
        bool closed = true, gateClosed = false;
        bool Disabled(int node) => closed && node == 1;
        bool Clear(Position a, Position b) => !gateClosed || a.Z == 0 && b.Z == 0;
        Result Check()
        {
            var actual = Capture(graph.Plan, 0, 3, Disabled, Clear);
            AssertSame(Capture(baseline.Plan, 0, 3, Disabled, Clear), actual);
            return actual;
        }
        Assert.Equal(new[] { 0, 2, 3 }, Check().Path);
        closed = false;
        Assert.Equal(new[] { 0, 1, 3 }, Check().Path);
        nodes[1].AllowsCar = false;
        Assert.Equal(new[] { 0, 2, 3 }, Check().Path);
        gateClosed = true;
        Assert.Null(Check().Route);
        nodes[1].AllowsCar = true;
        Assert.Equal(new[] { 0, 1, 3 }, Check().Path);
        gateClosed = false;
        nodes[1].KilometresPerHour = 1;
        Assert.Equal(new[] { 0, 2, 3 }, Check().Path);
        nodes[2].Position = new Position(20, 0, 80);
        Assert.Equal(new[] { 0, 1, 3 }, Check().Path);
        nodes[1].Successors = Array.Empty<int>();
        Assert.Null(Check().Route);
        nodes.Add(Node(30, 0, new[] { 3 }));
        nodes[1].Successors = new[] { 4 };
        Assert.Equal(new[] { 0, 1, 4, 3 }, Check().Path);
        foreach (var pair in new[] { (-1, 3), (0, nodes.Count), (0, 0), (3, 0) })
            AssertSame(Capture(baseline.Plan, pair.Item1, pair.Item2), Capture(graph.Plan, pair.Item1, pair.Item2));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PermissionCallbacksAreInvokedAgainDuringFinalPathValidation(bool changeClear)
    {
        var nodes = new[] { Node(0, 0, new[] { 1 }), Node(10, 0, new[] { 2 }), Node(20, 0) };
        Result Run(Planner plan)
        {
            int nodeChecks = 0, edgeChecks = 0;
            return Capture(plan, 0, 2,
                node => !changeClear && node == 1 && ++nodeChecks == 3,
                (_, _) => !changeClear || ++edgeChecks != 3);
        }
        var expected = Run(new BaselineRoadGraph(nodes).Plan);
        Assert.Null(expected.Route);
        AssertSame(expected, Run(new TransportRoadGraph(nodes).Plan));
    }

    [Fact]
    public void CallbackPositionMutationPreservesStalePriorityAndExpansionBehavior()
    {
        Result Run(bool baseline, int goal, int limit)
        {
            // The disconnected goal lets the leftover old priority actually pop;
            // a reachable goal would return before that entry became observable.
            var nodes = CostUpdateNodes().Concat(new[] { Node(60, 0) }).ToArray();
            bool changed = false;
            bool Clear(Position from, Position to)
            {
                if (!changed && from.X == 0 && from.Z == 20 && to.X == 20)
                {
                    changed = true;
                    nodes[3].Position = new Position(20, 0, 1);
                }
                return true;
            }
            Planner plan = baseline ? new BaselineRoadGraph(nodes).Plan : new TransportRoadGraph(nodes).Plan;
            var result = Capture(plan, 0, goal, clear: Clear, limit: limit);
            if (limit >= 3) Assert.True(changed);
            return result;
        }
        foreach (int goal in new[] { 4, 5 })
            for (int limit = 0; limit <= 8; limit++) AssertSame(Run(true, goal, limit), Run(false, goal, limit));
    }

    [Fact]
    public void CallbackSpeedMutationKeepsDefaultTupleOrderingForNonfiniteCosts()
    {
        Result Run(bool baseline, int limit)
        {
            var nodes = new[] { Node(0, 0, new[] { 2, 1 }), Node(10, 0, new[] { 2, 3 }),
                Node(20, 0, new[] { 3 }), Node(30, 0) };
            bool Clear(Position from, Position to)
            {
                if (from.X == 0) nodes[0].KilometresPerHour = double.NaN;
                return true;
            }
            Planner plan = baseline ? new BaselineRoadGraph(nodes).Plan : new TransportRoadGraph(nodes).Plan;
            return Capture(plan, 0, 3, clear: Clear, limit: limit);
        }
        for (int limit = 0; limit <= 6; limit++) AssertSame(Run(true, limit), Run(false, limit));
    }

    [Fact]
    public void ReturnedRoutesAndPathArraysAreOwnedByTheCaller()
    {
        var nodes = CostUpdateNodes();
        var graph = new TransportRoadGraph(nodes);
        var first = Capture(graph.Plan, 0, 4);
        var second = Capture(graph.Plan, 0, 4);
        Assert.NotSame(first.Route, second.Route);
        Assert.NotSame(first.Route!.Legs, second.Route!.Legs);
        Assert.NotSame(first.Route.Legs[0], second.Route.Legs[0]);
        Assert.NotSame(first.Path, second.Path);
        first.Path![0] = 999;
        first.Route.Legs[0].Minutes = 999;
        first.Route.Legs.Clear();
        nodes[2].Position = new Position(0, 0, 21);
        Assert.Equal(new Position(0, 0, 20), second.Route.Legs[0].To);
        var third = Capture(graph.Plan, 0, 4);
        AssertSame(Capture(new BaselineRoadGraph(nodes).Plan, 0, 4), third);
        Assert.Equal(0, second.Path![0]);
        Assert.Equal(0, third.Path![0]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void NestedQueryFromAnyCallbackDoesNotOverwriteTheOuterQuery(int callbackKind)
    {
        var nodes = CostUpdateNodes();
        var graph = new TransportRoadGraph(nodes);
        var baseline = new BaselineRoadGraph(nodes);
        var nestedExpected = Capture(baseline.Plan, 2, 4);
        bool invoked = false;
        void Reenter()
        {
            if (invoked) return;
            invoked = true;
            AssertSame(nestedExpected, Capture(graph.Plan, 2, 4));
        }
        // Skip initial endpoint checks so the disabled callback enters during an active search.
        int disabledCalls = 0;
        var actual = Capture(graph.Plan, 0, 4,
            _ => { if (callbackKind == 0 && ++disabledCalls == 3) Reenter(); return false; },
            (_, _) => { if (callbackKind == 1) Reenter(); return true; },
            onFound: _ => { if (callbackKind == 2) Reenter(); });
        Assert.True(invoked);
        AssertSame(Capture(baseline.Plan, 0, 4), actual);
        AssertSame(Capture(baseline.Plan, 0, 4), Capture(graph.Plan, 0, 4));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void CallbackExceptionsReleaseTheReusableWorkspace(int callbackKind)
    {
        var nodes = CostUpdateNodes();
        var graph = new TransportRoadGraph(nodes);
        int disabledCalls = 0;
        Assert.Throws<InvalidOperationException>(() => graph.Plan(0, 4,
            _ => { if (callbackKind == 0 && ++disabledCalls == 3) throw new InvalidOperationException(); return false; },
            (_, _) => { if (callbackKind == 1) throw new InvalidOperationException(); return true; },
            pathFound: _ => { if (callbackKind == 2) throw new InvalidOperationException(); }));
        AssertSame(Capture(new BaselineRoadGraph(nodes).Plan, 0, 4), Capture(graph.Plan, 0, 4));
        var lease = typeof(TransportRoadGraph).GetField("workspaceInUse", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Equal(0, lease.GetValue(graph));
    }

    [Fact]
    public void GenerationWrapDiscardsPreviousDistancesAndPredecessors()
    {
        var nodes = CostUpdateNodes();
        var graph = new TransportRoadGraph(nodes);
        Assert.NotNull(Capture(graph.Plan, 0, 4).Route);
        var workspace = typeof(TransportRoadGraph).GetField("workspace", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(graph)!;
        workspace.GetType().GetField("generation", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(workspace, int.MaxValue);
        nodes[0].Successors = new[] { 1 };
        AssertSame(Capture(new BaselineRoadGraph(nodes).Plan, 0, 4), Capture(graph.Plan, 0, 4));
    }

    private static TransportRoadNode Node(double x, double z, int[]? successors = null) => new()
    {
        Position = new Position(x, 0, z), KilometresPerHour = 40, AllowsCar = true,
        Successors = successors ?? Array.Empty<int>()
    };

    private static TransportRoadNode[] CostUpdateNodes()
    {
        var nodes = new[] { Node(0, 0, new[] { 1, 2 }), Node(10, 0, new[] { 3, 0 }),
            Node(0, 20, new[] { 3, 3, 0 }), Node(20, 0, new[] { 4, 1 }), Node(40, 0) };
        nodes[1].KilometresPerHour = 1;
        return nodes;
    }

    private static Result Capture(Planner plan, int start, int goal, Func<int, bool>? disabled = null,
        Func<Position, Position, bool>? clear = null, int limit = 30000, Action<int[]>? onFound = null)
    {
        var calls = new List<(int Kind, int Node, Position From, Position To)>();
        int[]? path = null;
        var route = plan(start, goal,
            node => { calls.Add((0, node, default, default)); return disabled?.Invoke(node) ?? false; },
            (from, to) => { calls.Add((1, 0, from, to)); return clear?.Invoke(from, to) ?? true; }, limit,
            found => { calls.Add((2, 0, default, default)); path = found; onFound?.Invoke(found); });
        return new Result(route, path, calls);
    }

    private static void AssertSame(Result expected, Result actual)
    {
        Assert.Equal(expected.Calls, actual.Calls);
        Assert.Equal(expected.Path, actual.Path);
        if (expected.Route == null) { Assert.Null(actual.Route); return; }
        Assert.NotNull(actual.Route);
        Assert.Equal(expected.Route.Legs.Count, actual.Route.Legs.Count);
        for (int i = 0; i < expected.Route.Legs.Count; i++)
        {
            var a = expected.Route.Legs[i]; var b = actual.Route.Legs[i];
            Assert.Equal(a.From, b.From); Assert.Equal(a.To, b.To);
            Assert.Equal(a.Mode, b.Mode); Assert.Equal(a.ServiceId, b.ServiceId);
            Assert.Equal(a.Minutes, b.Minutes);
        }
    }

    // Independent retained oracle: original TransportRoadGraph from source-before,
    // including its callback order, exact tuple frontier and path-length limit.
    private sealed class BaselineRoadGraph
    {
        private readonly IReadOnlyList<TransportRoadNode> nodes;
        private readonly double clockRate;
        public BaselineRoadGraph(IReadOnlyList<TransportRoadNode> nodes, double gameMinutesPerScaledSecond = 1)
        {
            this.nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
            if (!Position.Finite(gameMinutesPerScaledSecond) || gameMinutesPerScaledSecond <= 0)
                throw new ArgumentOutOfRangeException(nameof(gameMinutesPerScaledSecond));
            clockRate = gameMinutesPerScaledSecond;
        }
        public Route? Plan(int start, int goal, Func<int, bool> disabled, Func<Position, Position, bool> clear, int expansionLimit = 30000,
            Action<int[]>? pathFound = null)
        {
            if (!Usable(start) || !Usable(goal) || start == goal || disabled(start) || disabled(goal)) return null;
            var distance = new Dictionary<int, double> { [start] = 0 };
            var predecessor = new Dictionary<int, int>();
            // 40 km/h is the maximum edge speed, so Euclidean remaining time is a consistent lower bound.
            double Heuristic(int node) => Position.Distance(nodes[node].Position, nodes[goal].Position) / (40.0 / 3.6 / clockRate);
            var frontier = new SortedSet<(double Estimate, double Cost, int Node)> { (Heuristic(start), 0, start) };
            int expanded = 0;
            while (frontier.Count != 0 && expanded++ < expansionLimit)
            {
                var current = frontier.Min; frontier.Remove(current);
                if (current.Node == goal)
                {
                    var path = new List<int> { goal };
                    int cursor = goal;
                    while (cursor != start)
                    {
                        if (!predecessor.TryGetValue(cursor, out cursor) || path.Count >= TransportComposition.MaximumLegs) return null;
                        path.Add(cursor);
                    }
                    path.Reverse();
                    var route = new Route();
                    for (int k = 1; k < path.Count; k++)
                    {
                        var a = nodes[path[k - 1]]; var b = nodes[path[k]];
                        if (disabled(path[k - 1]) || disabled(path[k]) || !clear(a.Position, b.Position)) return null;
                        route.Legs.Add(new RouteLeg { From = a.Position, To = b.Position, Mode = TravelMode.Car,
                            Minutes = Position.Distance(a.Position, b.Position) / (Math.Min(40, a.KilometresPerHour) / 3.6 / clockRate) });
                    }
                    pathFound?.Invoke(path.ToArray());
                    return route;
                }
                if (disabled(current.Node)) continue;
                var node = nodes[current.Node];
                foreach (int next in node.Successors)
                {
                    if (!Usable(next) || disabled(next)) continue;
                    var target = nodes[next];
                    double metres = Position.Distance(node.Position, target.Position);
                    // Reject corrupt/coarse long-distance links, rather than interpreting them as vehicle teleports.
                    if (!Position.Finite(metres) || metres <= 0.000001 || metres > 60 || !clear(node.Position, target.Position)) continue;
                    double cost = current.Cost + metres / (Math.Min(40, node.KilometresPerHour) / 3.6 / clockRate);
                    if (distance.TryGetValue(next, out double old))
                    {
                        if (old <= cost) continue;
                        frontier.Remove((old + Heuristic(next), old, next));
                    }
                    distance[next] = cost; predecessor[next] = current.Node; frontier.Add((cost + Heuristic(next), cost, next));
                }
            }
            return null;
        }
        private bool Usable(int index) => index >= 0 && index < nodes.Count && nodes[index] != null && nodes[index].AllowsCar
            && nodes[index].Position.IsFinite && Position.Finite(nodes[index].KilometresPerHour) && nodes[index].KilometresPerHour > 0;
    }
}
