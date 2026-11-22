using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

public sealed class Gr5RoadSearchFinalizeTests
{
    private readonly ITestOutputHelper output;
    public Gr5RoadSearchFinalizeTests(ITestOutputHelper output) => this.output = output;

    private static TransportRoadNode[] Line(int count)
    {
        var nodes = new TransportRoadNode[count];
        for (int i = 0; i < count; i++)
            nodes[i] = new TransportRoadNode { Position = new Position(i * 10, 0, (i % 3) * 0.5), AllowsCar = true, KilometresPerHour = 20 + i % 30,
                Successors = i + 1 < count ? new[] { i + 1 } : Array.Empty<int>() };
        return nodes;
    }

    private static double Slow(int from, int to, double baseline) => baseline * (1 + (from + to) % 7 / 10.0);
    private static double Extra(int from, int to) => (from * 3 + to) % 5 / 20.0;

    private static string Describe(Route? route, int[]? path)
        => route == null ? "null" : string.Join(",", route.Legs.Select(l => l.Minutes.ToString("R"))) + "|" + route.EstimatedMinutes.ToString("R")
            + "|" + (path == null ? "" : string.Join(",", path));

    [Fact]
    public void ALongSuccessfulRouteFinishesAcrossSlicesWithTheSameResultAndChecksAsOneCall()
    {
        var graph = new TransportRoadGraph(Line(460));
        var wholeCalls = new List<string>(); int[]? wholePath = null;
        var whole = graph.PlanWithTraversal(0, 459, _ => false, (a, b) => { wholeCalls.Add(a.X + ":" + b.X); return true; }, Slow,
            pathFound: p => wholePath = p, extraMinutes: Extra);
        Assert.NotNull(whole);
        Assert.Equal(459, whole!.Legs.Count);

        var slicedCalls = new List<string>(); int[]? slicedPath = null;
        using var search = graph.BeginSearch(0, 459, _ => false, (a, b) => { slicedCalls.Add(a.X + ":" + b.X); return true; }, Slow,
            pathFound: p => slicedPath = p, extraMinutes: Extra);
        int finalizingCalls = 0, worstFinalChecks = 0;
        while (true)
        {
            bool finalizing = search.Finalizing;
            int before = slicedCalls.Count;
            bool done = search.Advance(16);
            if (finalizing) { finalizingCalls++; worstFinalChecks = Math.Max(worstFinalChecks, slicedCalls.Count - before); }
            if (done) break;
        }
        Assert.Equal(Describe(whole, wholePath), Describe(search.Result, slicedPath));
        Assert.Equal(wholeCalls, slicedCalls);
        Assert.True(finalizingCalls >= 459 * 2 / (16 * TransportRoadGraph.RoadSearch.FinalNodesPerExpansion));
        Assert.True(worstFinalChecks <= 16 * TransportRoadGraph.RoadSearch.FinalNodesPerExpansion, "worst finalizing call checked " + worstFinalChecks + " legs");
        output.WriteLine("459-leg route: finalization took " + finalizingCalls + " calls of Advance(16), at most " + worstFinalChecks
            + " leg checks per call; before this change the goal expansion reconstructed and checked all 459 legs in one call");
    }

    [Fact]
    public void ANodeClosedWhileTheRouteIsBeingFinalizedFailsTheRouteLikeAOneCallSearchWithThatClosure()
    {
        var graph = new TransportRoadGraph(Line(300));
        var closed = new HashSet<int>();
        using var search = graph.BeginSearch(0, 299, n => closed.Contains(n), (_, _) => true, Slow, extraMinutes: Extra);
        while (!search.Finalizing) Assert.False(search.Advance(16));
        Assert.False(search.Advance(16));
        closed.Add(250);
        while (!search.Advance(16)) { }
        Assert.Null(search.Result);
        Assert.Null(graph.PlanWithTraversal(0, 299, n => n == 250, (_, _) => true, Slow, extraMinutes: Extra));
    }

    [Fact]
    public void AnAbandonedFinalizationReleasesTheWorkspaceForTheNextSearch()
    {
        var graph = new TransportRoadGraph(Line(200));
        var reference = graph.PlanWithTraversal(0, 199, _ => false, (_, _) => true, Slow, extraMinutes: Extra)!;
        var abandoned = graph.BeginSearch(0, 199, _ => false, (_, _) => true, Slow, extraMinutes: Extra);
        while (!abandoned.Finalizing) abandoned.Advance(16);
        abandoned.Dispose();
        Assert.True(abandoned.Finished); Assert.Null(abandoned.Result);
        using var next = graph.BeginSearch(0, 199, _ => false, (_, _) => true, Slow, extraMinutes: Extra);
        while (!next.Advance(16)) { }
        Assert.Equal(reference.Legs.Select(l => l.Minutes), next.Result!.Legs.Select(l => l.Minutes));
        Assert.Equal(reference.EstimatedMinutes, next.Result.EstimatedMinutes);
    }
}
