using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class RoadSearchCeilingTests
{
    private static TransportRoadNode Node(double x, double z, params int[] next) => new()
    { Position = new Position(x, 0, z), AllowsCar = true, KilometresPerHour = 36, Successors = next };

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-1)]
    public void InvalidCeilingIsRejected(double ceiling)
    {
        var graph = new TransportRoadGraph(new[] { Node(0, 0, 1), Node(10, 0) });
        Assert.Throws<ArgumentOutOfRangeException>(() => graph.Plan(0, 1, _ => false, (_, _) => true,
            maximumEstimatedMinutes: ceiling));
    }

    [Fact]
    public void UnreachableRegionStopsEarlyAndNextUnboundedQueryStillWorks()
    {
        var nodes = Enumerable.Range(0, 1000).Select(i => Node(i * 10, 0, i + 1)).ToArray();
        nodes[998].Successors = Array.Empty<int>(); nodes[999] = Node(0, 10);
        var graph = new TransportRoadGraph(nodes);
        int all = 0, bounded = 0; bool reported = false;
        Assert.Null(graph.Plan(0, 999, _ => false, (_, _) => { all++; return true; }));
        Assert.Null(graph.Plan(0, 999, _ => false, (_, _) => { bounded++; return true; },
            pathFound: _ => reported = true, estimateFound: _ => reported = true, maximumEstimatedMinutes: 10));
        Assert.False(reported); Assert.True(all > 990); Assert.InRange(bounded, 1, 10);
        var after = graph.Plan(0, 30, _ => false, (_, _) => true)!;
        Assert.Equal(30, after.Minutes);
    }

    [Fact]
    public void SlowingAndQueueingAreInsideCeilingWhileReopeningIsFresh()
    {
        var graph = new TransportRoadGraph(new[] { Node(0, 0, 1, 2), Node(20, 0, 3), Node(20, 10, 3), Node(40, 0) });
        int[]? path = null;
        var detour = graph.PlanWithTraversal(0, 3, _ => false, (_, _) => true,
            (a, b, nominal) => a == 1 || b == 1 ? nominal * 2 : nominal,
            pathFound: p => path = p, extraMinutes: (_, _) => 1, maximumEstimatedMinutes: 7)!;
        Assert.Equal(new[] { 0, 2, 3 }, path);
        Assert.Equal(detour.Minutes + 2, detour.EstimatedMinutes, 10);
        Assert.Null(graph.PlanWithTraversal(0, 3, _ => false, (_, _) => true,
            (_, _, nominal) => nominal * 2, extraMinutes: (_, _) => 1, maximumEstimatedMinutes: 7));
        Assert.Equal(4, graph.Plan(0, 3, _ => false, (_, _) => true, maximumEstimatedMinutes: 7)!.Minutes);
    }

    [Fact]
    public void ExactTieAndOneUlpWinAreLeftToStrictModeSelection()
    {
        var graph = new TransportRoadGraph(new[] { Node(0, 0, 1), Node(10, 0) });
        double exact = graph.Plan(0, 1, _ => false, (_, _) => true)!.EstimatedMinutes;
        foreach (double incumbent in new[] { Math.BitDecrement(exact), exact, Math.BitIncrement(exact) })
        {
            var route = graph.Plan(0, 1, _ => false, (_, _) => true, maximumEstimatedMinutes: incumbent)!;
            Assert.Equal(exact, route.EstimatedMinutes);
            Assert.Equal(exact < incumbent, TransportComposition.IsFaster(route.EstimatedMinutes, incumbent));
        }
        Assert.Null(graph.Plan(0, 1, _ => false, (_, _) => true, maximumEstimatedMinutes: .9));
    }

    [Fact]
    public void ConservativeDeductionsCoverNestedJoinsAndHugeAccessTimes()
    {
        foreach (double fixedMinutes in new[] { .01, 4, 1e6, 1e15 })
        {
            double road = 3.7;
            double incumbent = Math.BitIncrement(fixedMinutes + road);
            double ceiling = TransportComposition.RemainingSearchMinutes(incumbent, fixedMinutes);
            Assert.True(ceiling >= road);
            Assert.True(TransportComposition.RemainingSearchMinutes(ceiling, 0) >= ceiling);
        }
        Assert.Equal(double.PositiveInfinity, TransportComposition.RemainingSearchMinutes(double.PositiveInfinity, 10));
        Assert.Equal(double.PositiveInfinity, TransportComposition.RemainingSearchMinutes(10, double.NaN));
        Assert.True(TransportComposition.RemainingSearchMinutes(1, 2) >= 0);
    }

    [Fact]
    public void DirectedRandomGraphsPreserveEveryCompetitivePathAndEstimate()
    {
        var random = new Random(7319);
        for (int sample = 0; sample < 30; sample++)
        {
            var nodes = new TransportRoadNode[100];
            for (int i = 0; i < nodes.Length; i++)
            {
                int x = i % 10, z = i / 10;
                var next = new List<int>();
                foreach (int candidate in new[] { i - 10, i + 10, x > 0 ? i - 1 : -1, x < 9 ? i + 1 : -1 })
                    if (candidate >= 0 && candidate < nodes.Length && random.Next(5) != 0) next.Add(candidate);
                nodes[i] = Node(x * 10, z * 10, next.ToArray());
                nodes[i].KilometresPerHour = random.Next(5, 60);
            }
            var graph = new TransportRoadGraph(nodes, new[] { .01, 1, 2, 1000 }[sample % 4]);
            for (int query = 0; query < 30; query++)
            {
                int start = random.Next(nodes.Length), goal = random.Next(nodes.Length);
                int[]? originalPath = null;
                bool Disabled(int i) => (i + sample) % 19 == 0;
                bool Clear(Position a, Position b) => !((a.X + b.X + sample) % 31 == 0);
                double Slow(int a, int b, double nominal) => nominal * (1 + (a + b) % 3);
                double Extra(int a, int b) => (a * 3 + b) % 7;
                var original = graph.PlanWithTraversal(start, goal, Disabled, Clear, Slow,
                    pathFound: p => originalPath = p, extraMinutes: Extra);
                double cost = original?.EstimatedMinutes ?? 50;
                foreach (double ceiling in new[] { 0, cost * .5, Math.BitDecrement(cost), cost, Math.BitIncrement(cost), cost * 2, double.PositiveInfinity })
                {
                    int[]? path = null;
                    var actual = graph.PlanWithTraversal(start, goal, Disabled, Clear, Slow,
                        pathFound: p => path = p, extraMinutes: Extra, maximumEstimatedMinutes: ceiling);
                    if (original == null) { Assert.Null(actual); continue; }
                    if (cost <= ceiling) Assert.NotNull(actual);
                    if (actual == null) { Assert.True(cost > ceiling); Assert.Null(path); continue; }
                    Assert.Equal(originalPath, path);
                    Assert.Equal(original.Minutes, actual.Minutes);
                    Assert.Equal(original.EstimatedMinutes, actual.EstimatedMinutes);
                    Assert.Equal(original.Legs.Select(l => l.Minutes), actual.Legs.Select(l => l.Minutes));
                    Assert.Equal(TransportComposition.IsFaster(cost, ceiling), TransportComposition.IsFaster(actual.EstimatedMinutes, ceiling));
                }
            }
        }
    }
}
