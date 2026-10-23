using System;
using System.Collections.Generic;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class EtaModeChoiceTests
{
    [Fact]
    public void ExactTieRetainsFirstFeasibleCandidateButAnyRealImprovementWins()
    {
        var walk = Route(TravelMode.Walk, 5);
        var subway = Route(TravelMode.Subway, 5);
        var fasterCar = Route(TravelMode.Car, 4.999999999);
        var prefs = new TravelPreferences { OwnsCar = true, WalkingToleranceMetres = 100 };
        Route? winner = null;
        double best = double.PositiveInfinity;
        foreach (var route in new[] { walk, subway, fasterCar })
        {
            if (!TransportComposition.Valid(route, new Position(), new Position(100, 0, 0), prefs, new Position())) continue;
            if (!TransportComposition.IsFaster(route.Minutes, best)) continue;
            if (ReferenceEquals(route, fasterCar)) Assert.Same(walk, winner);
            winner = route; best = route.Minutes;
        }
        Assert.Same(fasterCar, winner);
    }

    [Fact]
    public void UnavailableFastCarAndOverBudgetWalkCannotBeatSlowerFeasibleSubway()
    {
        var prefs = new TravelPreferences { OwnsCar = false, AllowTransit = true, WalkingToleranceMetres = 20 };
        var car = Route(TravelMode.Car, 1);
        var walk = Route(TravelMode.Walk, 2);
        var subway = Route(TravelMode.Subway, 10);
        Assert.False(TransportComposition.Valid(car, new Position(), new Position(100, 0, 0), prefs));
        Assert.False(TransportComposition.Valid(walk, new Position(), new Position(100, 0, 0), prefs));
        Assert.True(TransportComposition.Valid(subway, new Position(), new Position(100, 0, 0), prefs));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidEstimateCannotBecomeWinner(double minutes) =>
        Assert.False(TransportComposition.IsFaster(minutes, double.PositiveInfinity));

    [Fact]
    public void ModeBoundsIgnorePreferenceCostsEvenForNearlyEqualElapsedTimes()
    {
        var from = new Position(); var to = new Position(100, 0, 0);
        var prefs = new TravelPreferences { FareWeightMinutes = 1000000 };
        double bound = TransportComposition.ModeLowerBound(TravelMode.Subway, from, to, prefs, "a", 2,
            nonCarRetrievalPenalty: 60);
        prefs.FareWeightMinutes = 0;
        Assert.Equal(bound, TransportComposition.ModeLowerBound(TravelMode.Subway, from, to, prefs, "b", 2));
        var complete = Route(TravelMode.Subway, 3 + 100.0 / 10);
        Assert.True(bound <= complete.Minutes);
    }

    [Fact]
    public void CongestionChoosesDetourWithoutTurningPredictedQueuesIntoMovementTime()
    {
        var graph = RoadDiamond();
        double estimated = 0;
        var detour = graph.Plan(0, 3, _ => false, (_, _) => true,
            extraMinutes: (from, to) => from == 0 && to == 1 ? 100 : 2,
            estimateFound: value => estimated = value)!;
        Assert.Equal(20, detour.Legs[0].To.Z);
        Assert.Equal(detour.Minutes + 4, estimated, 10);
        Assert.Equal(estimated, detour.EstimatedMinutes, 10);
        Assert.True(estimated > detour.Minutes);
        // Fresh queries must make a previously congested off-path shortcut competitive immediately.
        var reopened = graph.Plan(0, 3, _ => false, (_, _) => true,
            extraMinutes: (_, _) => 0, estimateFound: value => estimated = value)!;
        Assert.Equal(0, reopened.Legs[0].To.Z);
        Assert.True(reopened.Minutes < detour.Minutes);
        Assert.Equal(reopened.Minutes, estimated, 10);
        Assert.Equal(reopened.Minutes, reopened.EstimatedMinutes, 10);
        Assert.Equal(reopened.Minutes, RoadDiamond().Plan(0, 3, _ => false, (_, _) => true)!.Minutes);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.MaxValue)]
    public void InvalidOrOverflowingSnapshotCostRejectsQuery(double extra)
    {
        bool reported = false;
        Assert.Null(RoadDiamond().Plan(0, 3, _ => false, (_, _) => true,
            extraMinutes: (_, _) => extra, estimateFound: _ => reported = true));
        Assert.False(reported);
    }

    [Fact]
    public void ChangedSelectedEdgeSnapshotRejectsQueryWithoutReportingMixedEstimate()
    {
        var seen = new HashSet<(int From, int To)>();
        bool reported = false;
        Assert.Null(RoadDiamond().Plan(0, 3, _ => false, (_, _) => true,
            extraMinutes: (from, to) => seen.Add((from, to)) ? 0 : 1,
            estimateFound: _ => reported = true));
        Assert.False(reported);
    }

    [Fact]
    public void CongestionSnapshotDoesNotRelaxDirectionClosureOrGateConstraints()
    {
        var graph = RoadDiamond();
        Assert.Null(graph.Plan(3, 0, _ => false, (_, _) => true, extraMinutes: (_, _) => 0));
        Assert.Null(graph.Plan(0, 3, i => i == 1 || i == 2, (_, _) => true, extraMinutes: (_, _) => 0));
        Assert.Null(graph.Plan(0, 3, _ => false, (from, _) => from.X != 0, extraMinutes: (_, _) => 0));
    }

    [Fact]
    public void JoinedDoorToDoorEstimateCountsRoadDelayOnceAndCanChangeModeWinner()
    {
        var access = Route(TravelMode.Walk, 1);
        var road = new Route { Legs = new List<RouteLeg> { new() { From = new Position(100, 0, 0),
            To = new Position(200, 0, 0), Mode = TravelMode.Car, Minutes = 5 } } }.WithEstimate(10);
        var egress = new Route { Legs = new List<RouteLeg> { new() { From = new Position(200, 0, 0),
            To = new Position(300, 0, 0), Mode = TravelMode.Walk, Minutes = 2 } } };
        var car = TransportComposition.Join(access, road, egress)!;
        Assert.Equal(8, car.Minutes);
        Assert.Equal(13, car.EstimatedMinutes);
        var subway = Route(TravelMode.Subway, 10);
        var prefs = new TravelPreferences { OwnsCar = true, FareWeightMinutes = 1000 };
        Assert.True(car.Minutes < subway.Minutes);
        Assert.True(TransportComposition.IsFaster(TransportComposition.Score(subway, 3, prefs, "person"),
            TransportComposition.Score(car, 0, prefs, "person")));
        Assert.Equal(100, car.PositionAt(1).X); // Queue estimate did not change nominal physical sampling.
        car.Legs[1].Minutes = 100;
        Assert.Null(TransportComposition.Join(car)); // A stale estimate below changed nominal timing is invalid.
    }

    [Theory]
    [InlineData(4)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void RouteEstimateCannotBeBelowNominalOrInvalid(double minutes) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Route(TravelMode.Car, 5).WithEstimate(minutes));

    [Fact]
    public void RealQueueDemandChangesNextTripModeAndDrainingRestoresCarChoice()
    {
        var kernel = new TrafficKernel(new[] { new TrafficLinkDefinition { Id=0, Capacity=4, FreeFlowMinutes=5, HeadwayMinutes=2 } }, 0);
        var preferences = new TravelPreferences { OwnsCar=true, AllowTransit=true, FareWeightMinutes=1000, WalkingToleranceMetres=1000 };
        var walk=Route(TravelMode.Walk,20); var subway=Route(TravelMode.Subway,12);
        Route Choose()
        {
            var car=Route(TravelMode.Car,11).WithEstimate(11+kernel.Forecast(0,kernel.Clock).DelayMinutes);
            Route best=walk;
            foreach(var candidate in new[]{subway,car})
                if(TransportComposition.Valid(candidate,new Position(),new Position(100,0,0),preferences,new Position())
                    && TransportComposition.IsFaster(TransportComposition.Score(candidate,0,preferences,"next"),best.EstimatedMinutes)) best=candidate;
            return best;
        }
        Assert.Equal(TravelMode.Car,Choose().Legs[0].Mode);
        for(int i=0;i<3;i++) kernel.Enqueue("commuter"+i,new[]{new TrafficRouteLeg {LinkId=0,StartNominalMinutes=0,EndNominalMinutes=5}});
        kernel.AdvanceTo(0,100);
        Assert.Same(subway,Choose());
        Assert.Equal(3,kernel.VehicleCount); // Estimates did not create, move or cancel demand.
        kernel.AdvanceTo(30,100);
        Assert.Equal(TravelMode.Car,Choose().Legs[0].Mode);
        Assert.Equal(3,kernel.VehicleCount); // Completed acknowledgements remain owned until removed.
    }

    private static TransportRoadGraph RoadDiamond() => new(new[] {
        new TransportRoadNode { Position = new Position(), AllowsCar = true, KilometresPerHour = 30, Successors = new[] { 1, 2 } },
        new TransportRoadNode { Position = new Position(20, 0, 0), AllowsCar = true, KilometresPerHour = 30, Successors = new[] { 3 } },
        new TransportRoadNode { Position = new Position(20, 0, 20), AllowsCar = true, KilometresPerHour = 30, Successors = new[] { 3 } },
        new TransportRoadNode { Position = new Position(40, 0, 0), AllowsCar = true, KilometresPerHour = 30 }
    });

    private static Route Route(TravelMode mode, double minutes) => new()
    {
        Legs = new List<RouteLeg> { new() { From = new Position(), To = new Position(100, 0, 0),
            Mode = mode, Minutes = minutes, ServiceId = mode == TravelMode.Subway ? "native:subway" : "" } }
    };
}
