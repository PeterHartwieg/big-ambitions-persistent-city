using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class TrafficNetworkTests
{
    private const string NativeId = "test-native";
    [Fact]
    public void UnbranchedSectionsHaveFiniteStorageAndPartialRouteKeepsActualDuration()
    {
        var network = Network(Chain(7, 20));
        Assert.Equal(2, network.Links.Count);
        Assert.Equal(13, network.Links[0].Capacity); // Declared 80 m / 6 m policy.
        Assert.Equal(1.8, network.Links[0].HeadwayMinutes);
        var route = new Route();
        route.Legs.Add(new RouteLeg { Mode = TravelMode.Walk, Minutes = 2 });
        route.Legs.Add(Leg(1, 2, 1)); route.Legs.Add(Leg(2, 3, 3)); route.Legs.Add(Leg(3, 4, 2));
        Assert.True(network.TryMap(route, out var mapped));
        var link = Assert.Single(mapped);
        Assert.Equal(0, link.LinkId); Assert.Equal(2, link.StartNominalMinutes);
        Assert.Equal(8, link.EndNominalMinutes); Assert.Equal(6, link.FreeFlowMinutes);
        Assert.Equal(.25, link.StartLinkFraction); Assert.Equal(1, link.EndLinkFraction);
    }

    [Fact]
    public void ForksMergesJunctionsAndAccessContractsRemainSectionBoundaries()
    {
        var nodes = Chain(7, 10);
        nodes[1].Successors = new[] { 2, 4 }; // Fork.
        nodes[3].Boundary = true; // Native junction/entry/exit or lane-change metadata.
        nodes[4].StructuralContract = "different-car-access";
        var network = Network(nodes);
        Assert.True(network.TryGetEdge(0, 1, out var beforeFork));
        Assert.True(network.TryGetEdge(1, 2, out var afterFork));
        Assert.NotEqual(beforeFork.LinkId, afterFork.LinkId);
        Assert.True(network.TryGetEdge(2, 3, out var junction));
        Assert.True(network.TryGetEdge(3, 4, out var junctionExit));
        Assert.NotEqual(junction.LinkId, junctionExit.LinkId);
        Assert.True(network.TryGetEdge(4, 5, out var access));
        Assert.True(network.TryGetEdge(5, 6, out var afterAccess));
        Assert.NotEqual(access.LinkId, afterAccess.LinkId);
    }

    [Fact]
    public void FilteredIncomingBranchDoesNotBecomeEvidenceOfUnbranchedTopology()
    {
        var nodes = Chain(5, 10);
        nodes[4].Successors = new[] { 2 }; nodes[4].AllowsCar = false;
        var network = Network(nodes);
        Assert.True(network.TryGetEdge(1, 2, out var a)); Assert.True(network.TryGetEdge(2, 3, out var b));
        Assert.NotEqual(a.LinkId, b.LinkId);
    }

    [Fact]
    public void CyclesBreakDeterministicallyAndEveryDirectedEdgeMapsExactlyOnce()
    {
        var nodes = Chain(4, 10); nodes[3].Successors = new[] { 0 };
        var first = Network(nodes); var second = Network(nodes.Reverse().ToArray());
        Assert.Equal(first.TopologyId, second.TopologyId);
        Assert.Equal(first.Links.Count, second.Links.Count);
        foreach (var node in nodes)
            foreach (int next in node.Successors)
            {
                Assert.True(first.TryGetEdge(node.Id, next, out var a)); Assert.True(second.TryGetEdge(node.Id, next, out var b));
                Assert.Equal(a.LinkId, b.LinkId);
            }
    }

    [Fact]
    public void ClockRetimingKeepsStructuralIdentityButSignalGroupChangesInvalidateIt()
    {
        var a = Chain(4, 10); var b = Chain(4, 10);
        a[2].Signal = new TrafficSignalSchedule { CycleMinutes = 60, GreenDurationMinutes = 20, GreenStartMinutes = 30 };
        b[2].Signal = new TrafficSignalSchedule { CycleMinutes = 180, GreenDurationMinutes = 60, GreenStartMinutes = 90 };
        var first = Network(a); var retimed = Network(b, 3);
        Assert.Equal(first.TopologyId, retimed.TopologyId);
        Assert.Equal(first.Links[0].FreeFlowMinutes * 3, retimed.Links[0].FreeFlowMinutes, 10);
        b[2].Signal!.GreenStartMinutes = 0;
        Assert.NotEqual(first.TopologyId, Network(b, 3).TopologyId);
    }

    [Fact]
    public void WrongRevisionMissingEdgesAndDisconnectedRoadTagsFailWithoutPartialMapping()
    {
        var network = Network(Chain(5, 10));
        foreach (var legs in new[] { new[] { Leg(0, 1), Leg(3, 4) }, new[] { Leg(0, 3) },
            new[] { new RouteLeg { Mode = TravelMode.Car, Minutes = 1, ServiceId = "native:road:v1:other:0:1" } } })
        {
            var route = new Route(); route.Legs.AddRange(legs);
            Assert.False(network.TryMap(route, out var result)); Assert.Empty(result);
        }
    }

    [Fact]
    public void NativeTagsDoNotAuthorizeUnrelatedGeometry()
    {
        var network = Network(Chain(3, 20));
        var route = new Route(); route.Legs.Add(Leg(0, 1));
        Assert.True(network.TryMap(route, out _));
        route.Legs[0].To = new Position(20, 0, 8);
        Assert.False(network.TryMap(route, out var result)); Assert.Empty(result);
    }

    private static TrafficNetwork Network(TrafficNetworkNode[] nodes, double rate = 1) => new TrafficNetwork(NativeId, nodes, rate);
    private static TrafficNetworkNode[] Chain(int count, double spacing) => Enumerable.Range(0, count).Select(i => new TrafficNetworkNode
    { Id = i, Position = new Position(i * spacing, 0, 0), KilometresPerHour = 30, AllowsCar = true,
        Successors = i + 1 < count ? new[] { i + 1 } : Array.Empty<int>() }).ToArray();
    private static RouteLeg Leg(int from, int to, double minutes = 1) => new RouteLeg
    { From = new Position(from * 20, 0, 0), To = new Position(to * 20, 0, 0), Mode = TravelMode.Car, Minutes = minutes,
        ServiceId = "native:road:v1:" + NativeId + ":" + from + ":" + to };
}
