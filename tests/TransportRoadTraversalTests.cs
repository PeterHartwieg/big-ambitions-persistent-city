using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class TransportRoadTraversalTests
{
    private delegate Route? SixArgumentPlanner(int start, int goal, Func<int, bool> disabled,
        Func<Position, Position, bool> clear, int limit, Action<int[]>? found);
    private delegate Route? EightArgumentPlanner(int start, int goal, Func<int, bool> disabled,
        Func<Position, Position, bool> clear, int limit, Action<int[]>? found,
        Func<int, int, double>? extra, Action<double>? estimate);
    private static TransportRoadNode Node(double x, double z, params int[] successors) => new()
    { Position = new Position(x, 0, z), AllowsCar = true, KilometresPerHour = 36, Successors = successors };
    private static TransportRoadNode[] Diamond() => new[]
    { Node(0, 0, 1, 2), Node(20, 0, 3), Node(20, 10, 3), Node(40, 0) };
    private static bool Open(int _) => false;
    private static bool Clear(Position _, Position __) => true;

    [Fact]
    public void SlowerMovingDurationsChooseAlternativeWithoutChangingStructuralRoadSpeeds()
    {
        var nodes = Diamond(); var graph = new TransportRoadGraph(nodes); int[]? baselinePath = null, slowPath = null;
        var original = graph.Plan(0, 3, Open, Clear, pathFound: path => baselinePath = path)!;
        var slowed = graph.PlanWithTraversal(0, 3, Open, Clear,
            (from, to, baseline) => from == 1 || to == 1 ? baseline * 2 : baseline,
            pathFound: path => slowPath = path)!;
        Assert.Equal(new[] { 0, 1, 3 }, baselinePath); Assert.Equal(new[] { 0, 2, 3 }, slowPath);
        Assert.Equal(4, original.Minutes); Assert.True(slowed.Minutes > original.Minutes);
        Assert.Equal(slowed.Minutes, slowed.EstimatedMinutes);
        Assert.All(nodes, node => Assert.Equal(36, node.KilometresPerHour));
        Assert.Equal(4, graph.Plan(0, 3, Open, Clear)!.Minutes); // Existing queries and returned routes stay unchanged.
        Assert.Equal(4, original.Minutes);
    }

    [Fact]
    public void TraversalIsCommittedMovementWhileCongestionRemainsEstimateOnly()
    {
        var graph = new TransportRoadGraph(new[] { Node(0, 0, 1), Node(10, 0, 2), Node(20, 0) });
        double estimate = 0;
        var route = graph.PlanWithTraversal(0, 2, Open, Clear, (_, _, baseline) => baseline * 3,
            extraMinutes: (_, _) => 5, estimateFound: found => estimate = found)!;
        Assert.Equal(new[] { 3.0, 3.0 }, route.Legs.Select(leg => leg.Minutes));
        Assert.Equal(6, route.Minutes); Assert.Equal(16, route.EstimatedMinutes); Assert.Equal(16, estimate);
        Assert.Equal(TravelMode.Car, route.Legs[0].Mode);
        Assert.Equal(2, graph.Plan(0, 2, Open, Clear)!.Minutes);
    }

    [Theory]
    [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(double.NegativeInfinity)]
    [InlineData(-1)] [InlineData(0)] [InlineData(.999999999999)]
    public void InvalidOrFasterTraversalRefusesTheWholeQuery(double effective)
    {
        var graph = new TransportRoadGraph(new[] { Node(0, 0, 1), Node(10, 0) }); bool found = false;
        Assert.Null(graph.PlanWithTraversal(0, 1, Open, Clear, (_, _, _) => effective,
            pathFound: _ => found = true));
        Assert.False(found); Assert.Equal(1, graph.Plan(0, 1, Open, Clear)!.Minutes);
    }

    [Theory]
    [InlineData(double.NaN)] [InlineData(1)] [InlineData(3)]
    public void ChangedSelectedTraversalSnapshotRefusesSuccess(double revised)
    {
        var graph = new TransportRoadGraph(new[] { Node(0, 0, 1), Node(10, 0) }); int calls = 0; bool found = false;
        Assert.Null(graph.PlanWithTraversal(0, 1, Open, Clear, (_, _, _) => ++calls == 1 ? 2 : revised,
            pathFound: _ => found = true));
        Assert.Equal(2, calls); Assert.False(found);
        Assert.Equal(2, graph.PlanWithTraversal(0, 1, Open, Clear, (_, _, baseline) => baseline * 2)!.Minutes);
    }

    [Theory]
    [InlineData(double.Epsilon)] [InlineData(double.MaxValue)]
    public void UnrepresentableBaselineNeverBecomesCallbackInventedMovement(double rate)
    {
        var graph = new TransportRoadGraph(new[] { Node(0, 0, 1), Node(20, 0) }, rate); int calls = 0;
        Assert.Null(graph.PlanWithTraversal(0, 1, Open, Clear, (_, _, _) => { calls++; return 1; }));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void TraversalAndDelayHaveIndependentSnapshotChecks()
    {
        var graph = new TransportRoadGraph(new[] { Node(0, 0, 1), Node(10, 0) }); int calls = 0;
        Assert.Null(graph.PlanWithTraversal(0, 1, Open, Clear, (_, _, baseline) => baseline * 2,
            extraMinutes: (_, _) => ++calls == 1 ? 1 : 2));
        Assert.Equal(2, calls);
    }

    [Fact]
    public void RevisitedEdgesReuseCapturedTraversalAndChosenEdgesAreCheckedAgain()
    {
        var graph = new TransportRoadGraph(new[] { Node(0, 0, 1, 1), Node(10, 0) });
        var baselines = new List<double>();
        var route = graph.PlanWithTraversal(0, 1, Open, Clear, (_, _, baseline) =>
        { baselines.Add(baseline); return baseline * 2; })!;
        Assert.Equal(2, baselines.Count); Assert.All(baselines, baseline => Assert.Equal(1, baseline));
        Assert.Equal(2, route.Minutes); // One exploration call despite duplicate edge, one acceptance call.
    }

    [Fact]
    public void IdentityTraversalPreservesExistingSixAndEightArgumentContracts()
    {
        var graph = new TransportRoadGraph(Diamond());
        SixArgumentPlanner six = graph.Plan; EightArgumentPlanner eight = graph.Plan;
        int[]? sixPath = null, eightPath = null, traversalPath = null;
        var a = six(0, 3, Open, Clear, 100, path => sixPath = path)!;
        var b = eight(0, 3, Open, Clear, 100, path => eightPath = path, (_, _) => .25, null)!;
        var c = graph.PlanWithTraversal(0, 3, Open, Clear, (_, _, baseline) => baseline,
            100, path => traversalPath = path, (_, _) => .25)!;
        Assert.Equal(sixPath, eightPath); Assert.Equal(eightPath, traversalPath);
        Assert.Equal(a.Minutes, b.Minutes); Assert.Equal(b.Minutes, c.Minutes);
        Assert.Equal(b.EstimatedMinutes, c.EstimatedMinutes);
        Assert.Equal(b.Legs.Select(leg => (leg.From, leg.To, leg.Minutes)), c.Legs.Select(leg => (leg.From, leg.To, leg.Minutes)));
        Assert.Throws<ArgumentNullException>(() => graph.PlanWithTraversal(0, 3, Open, Clear, null!));
    }

    [Fact]
    public void TraversalCallbackCanReenterAndAnExceptionReleasesSearchWorkspace()
    {
        var graph = new TransportRoadGraph(Diamond()); bool nested = false;
        var route = graph.PlanWithTraversal(0, 3, Open, Clear, (_, _, baseline) =>
        {
            if (!nested)
            {
                nested = true;
                Assert.Equal(4, graph.Plan(0, 3, Open, Clear)!.Minutes);
                Assert.Equal(8, graph.PlanWithTraversal(0, 3, Open, Clear, (_, _, minutes) => minutes * 2)!.Minutes);
            }
            return baseline * 2;
        })!;
        Assert.True(nested); Assert.Equal(8, route.Minutes);
        Assert.Throws<InvalidOperationException>(() => graph.PlanWithTraversal(0, 3, Open, Clear,
            (_, _, _) => throw new InvalidOperationException("snapshot failed")));
        Assert.Equal(4, graph.Plan(0, 3, Open, Clear)!.Minutes);
        Assert.Equal(8, graph.PlanWithTraversal(0, 3, Open, Clear, (_, _, baseline) => baseline * 2)!.Minutes);
    }
}
