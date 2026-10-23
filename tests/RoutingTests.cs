using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class RoutingTests
{
    private static GraphNode Node(string id, double x, double y = 0) => new(id, new Position(x, y, 0));
    private static Place PlaceAt(GraphNode node) => new() { Id = node.Id, Position = node.Position };
    private static Route? Plan(GraphNode[] nodes, GraphEdge[] edges, TravelPreferences? preferences = null, int from = 0, int to = 1)
        => new GraphRoutePlanner(nodes, edges).Plan(PlaceAt(nodes[from]), PlaceAt(nodes[to]), 600, preferences ?? new TravelPreferences());

    [Fact]
    public void DisconnectedAndUnknownNodesHaveNoStraightLineFallback()
    {
        var a = Node("a", 0); var b = Node("b", 1); var c = Node("c", 2);
        var planner = new GraphRoutePlanner(new[] { a, b, c }, new[] { new GraphEdge("ab", "a", "b", TravelMode.Walk, 1) });
        Assert.Null(planner.Plan(PlaceAt(a), PlaceAt(c), 0, new TravelPreferences()));
        Assert.Null(planner.Plan(PlaceAt(a), new Place { Id = "unknown", Position = b.Position }, 0, new TravelPreferences()));
        Assert.Null(planner.Plan(PlaceAt(a), PlaceAt(a), 0, new TravelPreferences()));
    }

    [Fact]
    public void RoadsAreDirectedAndRequireCarOwnership()
    {
        var nodes = new[] { Node("a", 0), Node("b", 100) };
        var edges = new[] { new GraphEdge("road", "a", "b", TravelMode.Car, 2) };
        Assert.Null(Plan(nodes, edges));
        var ownsCar = new TravelPreferences { OwnsCar = true };
        Assert.Equal(TravelMode.Car, Assert.Single(Plan(nodes, edges, ownsCar)!.Legs).Mode);
        Assert.Null(Plan(nodes, edges, ownsCar, 1, 0));
    }

    [Fact]
    public void CarCannotBeCollectedAfterWalkingOrAbandonedBeforeDestination()
    {
        var nodes = new[] { Node("a", 0), Node("b", 300), Node("c", 100), Node("d", 200) };
        var edges = new[] {
            new GraphEdge("walk-in", "a", "c", TravelMode.Walk, 1),
            new GraphEdge("car-middle", "c", "d", TravelMode.Car, 1),
            new GraphEdge("walk-out", "d", "b", TravelMode.Walk, 1)
        };
        var preferences = new TravelPreferences { OwnsCar = true };
        Assert.Null(Plan(nodes, edges, preferences));
        Assert.Null(Plan(nodes, new[] {
            new GraphEdge("car-first", "a", "d", TravelMode.Car, 1), edges[2]
        }, preferences));
        Assert.Null(Plan(nodes, new[] {
            edges[0], new GraphEdge("car-last", "c", "b", TravelMode.Car, 1)
        }, preferences));
    }

    [Fact]
    public void CarAndTransitCompeteWithWalkingUsingTravelTime()
    {
        var nodes = new[] { Node("a", 0), Node("b", 100) };
        var edges = new[] {
            new GraphEdge("walk", "a", "b", TravelMode.Walk, 10),
            new GraphEdge("bus", "a", "b", TravelMode.Bus, 3, headwayMinutes: 4, serviceId: "B"),
            new GraphEdge("car", "a", "b", TravelMode.Car, 4)
        };
        Assert.Equal(TravelMode.Bus, Plan(nodes, edges)!.Legs.Last().Mode);
        Assert.Equal(TravelMode.Car, Plan(nodes, edges, new TravelPreferences { OwnsCar = true })!.Legs.Last().Mode);
        Assert.Equal(TravelMode.Walk, Plan(nodes, edges, new TravelPreferences { AllowTransit = false })!.Legs.Last().Mode);
        Assert.Equal(TravelMode.Walk, Plan(nodes, new[] { edges[0], new GraphEdge("slow-car", "a", "b", TravelMode.Car, 11) }, new TravelPreferences { OwnsCar = true })!.Legs.Last().Mode);
    }

    [Fact]
    public void BoardingWaitCanMakeFastBusWorseThanWalking()
    {
        var nodes = new[] { Node("a", 0), Node("b", 100) };
        var route = Plan(nodes, new[] {
            new GraphEdge("walk", "a", "b", TravelMode.Walk, 8),
            new GraphEdge("bus", "a", "b", TravelMode.Bus, 2, headwayMinutes: 20, serviceId: "B")
        });
        Assert.Equal(TravelMode.Walk, Assert.Single(route!.Legs).Mode);
    }

    [Fact]
    public void FareWeightChangesChoiceWithoutChangingElapsedTime()
    {
        var nodes = new[] { Node("a", 0), Node("b", 100) };
        var edges = new[] {
            new GraphEdge("walk", "a", "b", TravelMode.Walk, 9),
            new GraphEdge("subway", "a", "b", TravelMode.Subway, 3, fare: 4, headwayMinutes: 2, serviceId: "S")
        };
        var inexpensive = Plan(nodes, edges, new TravelPreferences { FareWeightMinutes = 0.5 })!;
        Assert.Equal(4, inexpensive.Minutes);
        Assert.Equal(TravelMode.Subway, inexpensive.Legs.Last().Mode);
        Assert.Equal(TravelMode.Walk, Plan(nodes, edges, new TravelPreferences { FareWeightMinutes = 2 })!.Legs.Last().Mode);
    }

    [Fact]
    public void SameServiceContinuesWithoutAnotherWaitOrFare()
    {
        var nodes = new[] { Node("a", 0), Node("b", 200), Node("stop", 100) };
        var route = Plan(nodes, new[] {
            new GraphEdge("first", "a", "stop", TravelMode.Bus, 2, fare: 2, headwayMinutes: 6, serviceId: "B"),
            new GraphEdge("second", "stop", "b", TravelMode.Bus, 2, fare: 100, headwayMinutes: 100, serviceId: "B"),
            new GraphEdge("walk", "a", "b", TravelMode.Walk, 12)
        }, new TravelPreferences { FareWeightMinutes = 2 })!;
        Assert.Equal(7, route.Minutes);
        Assert.Equal(3, route.Legs.Count);
        Assert.All(route.Legs, leg => Assert.Equal(TravelMode.Bus, leg.Mode));
    }

    [Fact]
    public void ChangingServiceOrModeBoardsAgain()
    {
        var nodes = new[] { Node("a", 0), Node("b", 200), Node("stop", 100) };
        foreach (var mode in new[] { TravelMode.Bus, TravelMode.Subway })
        {
            var route = Plan(nodes, new[] {
                new GraphEdge("first", "a", "stop", TravelMode.Bus, 2, fare: 2, headwayMinutes: 6, serviceId: "B"),
                new GraphEdge("second", "stop", "b", mode, 2, fare: 3, headwayMinutes: 8, serviceId: mode == TravelMode.Bus ? "C" : "B")
            })!;
            Assert.Equal(11, route.Minutes);
            Assert.Equal(4, route.Legs.Count);
        }
    }

    [Fact]
    public void TransferFareCanMakeWalkingCheaper()
    {
        var nodes = new[] { Node("a", 0), Node("b", 200), Node("stop", 100) };
        var route = Plan(nodes, new[] {
            new GraphEdge("first", "a", "stop", TravelMode.Bus, 1, fare: 1, serviceId: "B"),
            new GraphEdge("second", "stop", "b", TravelMode.Bus, 1, fare: 10, serviceId: "C"),
            new GraphEdge("walk", "a", "b", TravelMode.Walk, 10)
        })!;
        Assert.Equal(TravelMode.Walk, Assert.Single(route.Legs).Mode);
    }

    [Fact]
    public void MoreExpensivePrefixOnSameServiceAvoidsAnExpensiveTransfer()
    {
        var nodes = new[] { Node("a", 0), Node("b", 200), Node("stop", 100) };
        var route = Plan(nodes, new[] {
            new GraphEdge("cheap-prefix", "a", "stop", TravelMode.Bus, 1, serviceId: "B"),
            new GraphEdge("through-prefix", "a", "stop", TravelMode.Bus, 3, serviceId: "C"),
            new GraphEdge("through-finish", "stop", "b", TravelMode.Bus, 1, fare: 10, headwayMinutes: 10, serviceId: "C")
        })!;
        Assert.Equal(4, route.Minutes);
        Assert.Equal(2, route.Legs.Count);
        Assert.All(route.Legs, leg => Assert.Equal("C", leg.ServiceId));
    }

    [Fact]
    public void WalkingBetweenSameServiceLegsRequiresReboarding()
    {
        var nodes = new[] { Node("a", 0), Node("b", 300), Node("s1", 100), Node("s2", 200) };
        var route = Plan(nodes, new[] {
            new GraphEdge("first", "a", "s1", TravelMode.Bus, 2, headwayMinutes: 6, serviceId: "B"),
            new GraphEdge("transfer", "s1", "s2", TravelMode.Walk, 1),
            new GraphEdge("second", "s2", "b", TravelMode.Bus, 2, headwayMinutes: 8, serviceId: "B")
        })!;
        Assert.Equal(12, route.Minutes);
        Assert.Equal(new[] { TravelMode.Bus, TravelMode.Bus, TravelMode.Walk, TravelMode.Bus, TravelMode.Bus }, route.Legs.Select(l => l.Mode));
    }

    [Fact]
    public void WalkingBudgetIsCumulativeAndPreservesMoreExpensiveFeasibleLabels()
    {
        var nodes = new[] { Node("a", 0), Node("b", 150), Node("join", 100), Node("detour", 80, 60) };
        var edges = new[] {
            new GraphEdge("cheap-detour", "a", "detour", TravelMode.Walk, 1),
            new GraphEdge("cheap-join", "detour", "join", TravelMode.Walk, 1),
            new GraphEdge("direct", "a", "join", TravelMode.Walk, 5),
            new GraphEdge("finish", "join", "b", TravelMode.Walk, 1)
        };
        var route = Plan(nodes, edges, new TravelPreferences { WalkingToleranceMetres = 150 })!;
        Assert.Equal(6, route.Minutes);
        Assert.Equal(2, route.Legs.Count);
        Assert.Null(Plan(nodes, edges, new TravelPreferences { WalkingToleranceMetres = 149 }));
    }

    [Fact]
    public void WalkAccessTransitAndWalkEgressRemainConnectedAndInterpolateWaiting()
    {
        var nodes = new[] { Node("a", 0), Node("b", 300), Node("stop1", 50), Node("stop2", 250) };
        var route = Plan(nodes, new[] {
            new GraphEdge("access", "a", "stop1", TravelMode.Walk, 2),
            new GraphEdge("bus", "stop1", "stop2", TravelMode.Bus, 4, headwayMinutes: 6, serviceId: "B"),
            new GraphEdge("egress", "stop2", "b", TravelMode.Walk, 2)
        })!;
        Assert.Equal(11, route.Minutes);
        Assert.All(route.Legs, l => Assert.True(l.Minutes > 0));
        for (int i = 1; i < route.Legs.Count; i++) AssertPosition(route.Legs[i - 1].To, route.Legs[i].From);
        AssertPosition(nodes[0].Position, route.PositionAt(-100));
        AssertPosition(new Position(25, 0, 0), route.PositionAt(1));
        AssertPosition(nodes[2].Position, route.PositionAt(2));
        AssertPosition(nodes[2].Position, route.PositionAt(4));
        AssertPosition(new Position(150, 0, 0), route.PositionAt(7));
        AssertPosition(nodes[1].Position, route.PositionAt(100));
    }

    [Fact]
    public void EqualCostRoutesUseStableEdgeIdsAcrossInputOrder()
    {
        var nodes = new[] { Node("a", 0), Node("b", 100), Node("left", 40, 10), Node("right", 40, -10) };
        var edges = new[] {
            new GraphEdge("z-first", "a", "right", TravelMode.Walk, 1),
            new GraphEdge("z-last", "right", "b", TravelMode.Walk, 1),
            new GraphEdge("a-first", "a", "left", TravelMode.Walk, 1),
            new GraphEdge("a-last", "left", "b", TravelMode.Walk, 1)
        };
        var first = Plan(nodes, edges)!;
        var reversed = new GraphRoutePlanner(nodes.Reverse(), edges.Reverse()).Plan(PlaceAt(nodes[0]), PlaceAt(nodes[1]), 600, new TravelPreferences())!;
        AssertPosition(nodes[2].Position, first.Legs[0].To);
        AssertPosition(first.Legs[0].To, reversed.Legs[0].To);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-1)]
    public void InvalidDepartureAndPreferenceScalarsAreRejected(double value)
    {
        var nodes = new[] { Node("a", 0), Node("b", 100) };
        var planner = new GraphRoutePlanner(nodes, Array.Empty<GraphEdge>());
        Assert.Throws<ArgumentException>(() => planner.Plan(PlaceAt(nodes[0]), PlaceAt(nodes[1]), value, new TravelPreferences()));
        Assert.Throws<ArgumentException>(() => planner.Plan(PlaceAt(nodes[0]), PlaceAt(nodes[1]), 0, new TravelPreferences { WalkingToleranceMetres = value }));
        Assert.Throws<ArgumentException>(() => planner.Plan(PlaceAt(nodes[0]), PlaceAt(nodes[1]), 0, new TravelPreferences { FareWeightMinutes = value }));
    }

    [Fact]
    public void GraphValidationRejectsInvalidNumbersIdentityAndServiceMetadata()
    {
        var nodes = new[] { Node("a", 0), Node("b", 100) };
        var invalid = new[] {
            new GraphEdge("e", "a", "b", TravelMode.Walk, 0),
            new GraphEdge("e", "a", "b", TravelMode.Walk, -1),
            new GraphEdge("e", "a", "b", TravelMode.Walk, double.NaN),
            new GraphEdge("e", "a", "b", TravelMode.Car, 1, fare: -1),
            new GraphEdge("e", "a", "b", TravelMode.Car, 1, fare: double.PositiveInfinity),
            new GraphEdge("e", "a", "b", TravelMode.Bus, 1, headwayMinutes: -1, serviceId: "B"),
            new GraphEdge("e", "a", "b", TravelMode.Bus, 1, headwayMinutes: double.NaN, serviceId: "B"),
            new GraphEdge("e", "a", "b", TravelMode.Bus, 1),
            new GraphEdge("e", "a", "missing", TravelMode.Walk, 1),
            new GraphEdge("e", "a", "b", (TravelMode)100, 1),
            new GraphEdge("e", "a", "b", TravelMode.Walk, 1, headwayMinutes: 1),
            new GraphEdge("e", "a", "b", TravelMode.Walk, 1, serviceId: "B")
        };
        foreach (var edge in invalid) Assert.Throws<ArgumentException>(() => new GraphRoutePlanner(nodes, new[] { edge }));
        Assert.Throws<ArgumentException>(() => new GraphRoutePlanner(new[] { nodes[0], nodes[0] }, Array.Empty<GraphEdge>()));
        Assert.Throws<ArgumentException>(() => new GraphRoutePlanner(new[] { Node("bad", double.NaN) }, Array.Empty<GraphEdge>()));
        Assert.Throws<ArgumentException>(() => new GraphRoutePlanner(nodes, new[] { new GraphEdge("e", "a", "b", TravelMode.Walk, 1), new GraphEdge("e", "b", "a", TravelMode.Walk, 1) }));
    }

    [Fact]
    public void PlacePositionMismatchAndNumericOverflowAreRejected()
    {
        var nodes = new[] { Node("a", 0), Node("b", 100) };
        var planner = new GraphRoutePlanner(nodes, new[] { new GraphEdge("bus", "a", "b", TravelMode.Bus, 1, fare: double.MaxValue, serviceId: "B") });
        Assert.Throws<ArgumentException>(() => planner.Plan(new Place { Id = "a", Position = new Position(1, 0, 0) }, PlaceAt(nodes[1]), 0, new TravelPreferences()));
        Assert.Throws<ArgumentException>(() => planner.Plan(PlaceAt(nodes[0]), PlaceAt(nodes[1]), 0, new TravelPreferences { FareWeightMinutes = 2 }));
    }

    private static void AssertPosition(Position expected, Position actual)
    {
        Assert.Equal(expected.X, actual.X, 8);
        Assert.Equal(expected.Y, actual.Y, 8);
        Assert.Equal(expected.Z, actual.Z, 8);
    }
}
