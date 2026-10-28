using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>The sliced road search must expand in the same order and answer the same as the one-shot search,
/// whatever the slice sizes, and must leave the shared scratch storage usable by other searches.</summary>
public sealed class RoadSearchSlicingTests
{
    private static TransportRoadNode Node(double x, double z, params int[] next) => new()
    { Position = new Position(x, 0, z), AllowsCar = true, KilometresPerHour = 36, Successors = next };

    private static TransportRoadNode[] RandomGrid(Random random)
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
        return nodes;
    }

    private static string Describe(Route? route, int[]? path, List<string> calls)
        => (route == null ? "null" : string.Join(",", route.Legs.Select(l => l.Minutes.ToString("R"))) + "|" + route.EstimatedMinutes.ToString("R"))
            + "|" + (path == null ? "" : string.Join(",", path)) + "|" + string.Join(";", calls);

    [Fact]
    public void SlicedSearchMatchesOneShotOnThirtyRandomGraphs()
    {
        var random = new Random(7319);
        var slices = new Random(41);
        int sliced = 0;
        for (int sample = 0; sample < 30; sample++)
        {
            var graph = new TransportRoadGraph(RandomGrid(random), new[] { .01, 1, 2, 1000 }[sample % 4]);
            for (int query = 0; query < 30; query++)
            {
                int start = random.Next(100), goal = random.Next(100);
                bool Disabled(int i) => (i + sample) % 19 == 0;
                double Slow(int a, int b, double nominal) => nominal * (1 + (a + b) % 3);
                double Extra(int a, int b) => (a * 3 + b) % 7;
                // Clear calls are recorded: the sliced search must issue them in the same order.
                var oneShotCalls = new List<string>(); int[]? oneShotPath = null;
                bool ClearOne(Position a, Position b) { oneShotCalls.Add(a.X + ":" + b.X); return !((a.X + b.X + sample) % 31 == 0); }
                var oneShot = graph.PlanWithTraversal(start, goal, Disabled, ClearOne, Slow, pathFound: p => oneShotPath = p, extraMinutes: Extra);
                double cost = oneShot?.EstimatedMinutes ?? 50;
                foreach (double ceiling in new[] { double.PositiveInfinity, cost * .5, cost, Math.BitIncrement(cost) })
                {
                    var boundedCalls = new List<string>(); int[]? boundedPath = null;
                    bool ClearBounded(Position a, Position b) { boundedCalls.Add(a.X + ":" + b.X); return !((a.X + b.X + sample) % 31 == 0); }
                    var bounded = graph.PlanWithTraversal(start, goal, Disabled, ClearBounded, Slow, pathFound: p => boundedPath = p,
                        extraMinutes: Extra, maximumEstimatedMinutes: ceiling);
                    var slicedCalls = new List<string>(); int[]? slicedPath = null;
                    bool ClearSliced(Position a, Position b) { slicedCalls.Add(a.X + ":" + b.X); return !((a.X + b.X + sample) % 31 == 0); }
                    using var search = graph.BeginSearch(start, goal, Disabled, ClearSliced, Slow, pathFound: p => slicedPath = p,
                        extraMinutes: Extra, maximumEstimatedMinutes: ceiling);
                    int steps = 0;
                    while (!search.Advance(slices.Next(1, 6))) { steps++; Assert.False(search.Finished); }
                    Assert.True(search.Finished);
                    if (steps > 0) sliced++;
                    Assert.Equal(Describe(bounded, boundedPath, boundedCalls), Describe(search.Result, slicedPath, slicedCalls));
                    Assert.True(search.Advance(3));
                    Assert.Equal(Describe(bounded, boundedPath, boundedCalls), Describe(search.Result, slicedPath, slicedCalls));
                }
            }
        }
        Assert.True(sliced > 1000, "too few searches were actually interrupted: " + sliced);
    }

    [Fact]
    public void OneShotQueryWhileASearchIsSuspendedDisturbsNeither()
    {
        var random = new Random(99);
        var graph = new TransportRoadGraph(RandomGrid(random));
        var reference = graph.Plan(0, 99, _ => false, (_, _) => true);
        var other = graph.Plan(5, 93, _ => false, (_, _) => true);
        Assert.NotNull(reference); Assert.NotNull(other);
        using var suspended = graph.BeginSearch(0, 99, _ => false, (_, _) => true);
        Assert.False(suspended.Advance(2));
        // The suspended search holds the borrowed storage; the one-shot query runs on its own and answers the same.
        var interleaved = graph.Plan(5, 93, _ => false, (_, _) => true);
        Assert.Equal(other!.Legs.Select(l => l.Minutes), interleaved!.Legs.Select(l => l.Minutes));
        using (var nested = graph.BeginSearch(5, 93, _ => false, (_, _) => true))
        {
            while (!nested.Advance(1)) { }
            Assert.Equal(other.Legs.Select(l => l.Minutes), nested.Result!.Legs.Select(l => l.Minutes));
        }
        while (!suspended.Advance(1)) { }
        Assert.Equal(reference!.Legs.Select(l => l.Minutes), suspended.Result!.Legs.Select(l => l.Minutes));
        Assert.Equal(reference.EstimatedMinutes, suspended.Result.EstimatedMinutes);
    }

    [Fact]
    public void AbandonedSearchReleasesStorageAndFinishedSearchStaysFinished()
    {
        var random = new Random(5);
        var graph = new TransportRoadGraph(RandomGrid(random));
        var reference = graph.Plan(0, 99, _ => false, (_, _) => true)!;
        var abandoned = graph.BeginSearch(0, 99, _ => false, (_, _) => true);
        Assert.False(abandoned.Advance(1));
        abandoned.Dispose();
        Assert.True(abandoned.Finished); Assert.Null(abandoned.Result);
        Assert.True(abandoned.Advance(1)); Assert.Null(abandoned.Result);
        abandoned.Dispose();
        // The next search borrows the released storage and answers the same as before.
        using var next = graph.BeginSearch(0, 99, _ => false, (_, _) => true);
        Assert.True(next.Advance(int.MaxValue));
        Assert.Equal(reference.Legs.Select(l => l.Minutes), next.Result!.Legs.Select(l => l.Minutes));
        Assert.True(next.Expanded > 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => next.Advance(0));
        // Unusable endpoints finish immediately without touching the storage.
        using var empty = graph.BeginSearch(0, 0, _ => false, (_, _) => true);
        Assert.True(empty.Finished); Assert.Null(empty.Result); Assert.Equal(0, empty.Expanded);
        Assert.Throws<ArgumentOutOfRangeException>(() => graph.BeginSearch(0, 1, _ => false, (_, _) => true, maximumEstimatedMinutes: -1));
    }

    [Fact]
    public void ExpansionLimitCountsAcrossSlices()
    {
        var nodes = Enumerable.Range(0, 400).Select(i => Node(i * 10, 0, i + 1)).ToArray();
        nodes[399] = Node(3990, 0);
        var graph = new TransportRoadGraph(nodes);
        Assert.Null(graph.Plan(0, 399, _ => false, (_, _) => true, expansionLimit: 100));
        using var limited = graph.BeginSearch(0, 399, _ => false, (_, _) => true, expansionLimit: 100);
        int steps = 0;
        while (!limited.Advance(7)) steps++;
        Assert.Null(limited.Result);
        Assert.Equal(graph.Plan(0, 399, _ => false, (_, _) => true, expansionLimit: 100) == null, limited.Result == null);
        Assert.InRange(steps, 10, 20);
        using var enough = graph.BeginSearch(0, 399, _ => false, (_, _) => true, expansionLimit: 1000);
        while (!enough.Advance(7)) { }
        Assert.Equal(399, enough.Result!.Legs.Count);
    }
}
