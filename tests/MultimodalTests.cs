using System;
using System.Collections.Generic;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class MultimodalTests
{
    private static Position P(double x) => new(x, 0, 0);
    private static Route R(TravelMode mode, double from, double to, double minutes = 1) => new()
    {
        Legs = new List<RouteLeg> { new() { From = P(from), To = P(to), Mode = mode, Minutes = minutes, ServiceId = mode == TravelMode.Subway ? "native:subway" : "" } }
    };

    [Fact]
    public void WalkToParkedCarDriveAndWalkOutPreservesEveryCoordinate()
    {
        var joined = TransportComposition.Join(R(TravelMode.Walk, 0, 10), R(TravelMode.Car, 10, 100), R(TravelMode.Walk, 100, 120))!;
        var prefs = new TravelPreferences { OwnsCar = true, WalkingToleranceMetres = 30 };
        Assert.True(TransportComposition.Valid(joined, P(0), P(120), prefs, P(10)));
        Assert.False(TransportComposition.Valid(joined, P(0), P(120), prefs, P(11)));
        prefs.WalkingToleranceMetres = 29;
        Assert.False(TransportComposition.Valid(joined, P(0), P(120), prefs, P(10)));
        Assert.Equal(55, joined.PositionAt(1.5).X);
    }

    [Fact]
    public void CompositionNeverBridgesADiscontinuityOrReturnsCallerOwnedLegs()
    {
        Assert.Null(TransportComposition.Join(R(TravelMode.Walk, 0, 10), R(TravelMode.Car, 11, 100)));
        var source = R(TravelMode.Walk, 0, 10);
        var joined = TransportComposition.Join(source)!;
        joined.Legs[0].To = P(20);
        Assert.Equal(10, source.Legs[0].To.X);
    }

    [Fact]
    public void CarCannotReappearAfterWalkingOrCombineWithSubway()
    {
        var prefs = new TravelPreferences { OwnsCar = true };
        var split = TransportComposition.Join(R(TravelMode.Car, 0, 10), R(TravelMode.Walk, 10, 20), R(TravelMode.Car, 20, 30))!;
        Assert.False(TransportComposition.Valid(split, P(0), P(30), prefs, P(0)));
        var trainFirst = TransportComposition.Join(R(TravelMode.Subway, 0, 10), R(TravelMode.Car, 10, 30))!;
        Assert.False(TransportComposition.Valid(trainFirst, P(0), P(30), prefs, P(10)));
        var carFirst = TransportComposition.Join(R(TravelMode.Car, 0, 10), R(TravelMode.Subway, 10, 30))!;
        Assert.False(TransportComposition.Valid(carFirst, P(0), P(30), prefs, P(0)));
        Assert.False(TransportComposition.Valid(R(TravelMode.Car, 0, 10), P(0), P(10), prefs));
    }

    [Fact]
    public void FastestModeWinsRegardlessOfFareIdentityOrFutureRetrievalPreference()
    {
        var walk = R(TravelMode.Walk, 0, 100, 12);
        var train = R(TravelMode.Subway, 0, 100, 5);
        var prefs = new TravelPreferences { FareWeightMinutes = 3 };
        Assert.True(TransportComposition.Score(train, 3, prefs, "person", 60) < TransportComposition.Score(walk, 0, prefs, "person"));
        prefs.FareWeightMinutes = 0;
        Assert.True(TransportComposition.Score(train, 3, prefs, "person") < TransportComposition.Score(walk, 0, prefs, "person"));
        Assert.Equal(5, TransportComposition.Score(train, 3, prefs, "other-person"));
        // Even a sub-minute ETA advantage is decisive; no identity or habit penalty can erase it.
        var almostEqual = R(TravelMode.Car, 0, 100, 5.0001);
        Assert.True(TransportComposition.IsFaster(TransportComposition.Score(train, 1000, prefs, "train"),
            TransportComposition.Score(almostEqual, 0, prefs, "car")));
    }

    [Fact]
    public void NativeRoadTopologyStaysDirectedAndRespectsClosuresAndGates()
    {
        var nodes = new[] {
            new TransportRoadNode { Position = P(0), AllowsCar = true, KilometresPerHour = 30, Successors = new[] { 1 } },
            new TransportRoadNode { Position = P(30), AllowsCar = true, KilometresPerHour = 30, Successors = new[] { 2 } },
            new TransportRoadNode { Position = P(60), AllowsCar = true, KilometresPerHour = 30 }
        };
        var graph = new TransportRoadGraph(nodes);
        var path = graph.Plan(0, 2, _ => false, (_, _) => true)!;
        Assert.Equal(2, path.Legs.Count);
        Assert.Equal(7.2, path.Minutes, 8);
        Assert.Null(graph.Plan(2, 0, _ => false, (_, _) => true));
        Assert.Null(graph.Plan(0, 2, i => i == 1, (_, _) => true));
        Assert.Null(graph.Plan(0, 2, _ => false, (a, _) => a.X != 30));
        Assert.Null(graph.Plan(0, 2, _ => false, (_, _) => true, expansionLimit: 1));
        nodes[1].AllowsCar = false;
        Assert.Null(graph.Plan(0, 2, _ => false, (_, _) => true));
    }

    [Fact]
    public void DisconnectedAndCoarseRoadLinksHaveNoTeleportFallback()
    {
        var graph = new TransportRoadGraph(new[] {
            new TransportRoadNode { Position = P(0), AllowsCar = true, KilometresPerHour = 30, Successors = new[] { 1, 99 } },
            new TransportRoadNode { Position = P(1000), AllowsCar = true, KilometresPerHour = 30 }
        });
        Assert.Null(graph.Plan(0, 1, _ => false, (_, _) => true));
        var parts = new Route[513];
        for (int i = 0; i < parts.Length; i++) parts[i] = R(TravelMode.Walk, i, i + 1);
        Assert.Null(TransportComposition.Join(parts));
    }

    [Fact]
    public void NearbyParallelRoadsDoNotInventALaneChangeOrTurn()
    {
        var graph = new TransportRoadGraph(new[] {
            new TransportRoadNode { Position = P(0), AllowsCar = true, KilometresPerHour = 30, Successors = new[] { 1 } },
            new TransportRoadNode { Position = P(10), AllowsCar = true, KilometresPerHour = 30 },
            new TransportRoadNode { Position = new Position(10, 0, 2), AllowsCar = true, KilometresPerHour = 30, Successors = new[] { 3 } },
            new TransportRoadNode { Position = new Position(0, 0, 2), AllowsCar = true, KilometresPerHour = 30 }
        });
        Assert.Null(graph.Plan(0, 3, _ => false, (_, _) => true));
        Assert.NotNull(graph.Plan(0, 1, _ => false, (_, _) => true));
    }

    [Fact]
    public void ModeBoundsCannotExcludeAnActuallyCheaperComposedRoute()
    {
        var prefs = new TravelPreferences { OwnsCar = true, FareWeightMinutes = 2 };
        var car = TransportComposition.Join(R(TravelMode.Walk, 0, 30, 30.0 / 2),
            R(TravelMode.Car, 30, 30, TransportComposition.CarPreparationMinutes),
            R(TravelMode.Car, 30, 2000, 1970.0 / (40 / 3.6)),
            R(TravelMode.Car, 2000, 2000, TransportComposition.CarParkingMinutes),
            R(TravelMode.Walk, 2000, 2040, 40.0 / 2))!;
        double carBound = TransportComposition.ModeLowerBound(TravelMode.Car, P(0), P(2040), prefs, "resident", 2, P(30));
        Assert.True(carBound <= TransportComposition.Score(car, 0, prefs, "resident"));
        var train = TransportComposition.Join(R(TravelMode.Walk, 0, 100, 100.0 / 2),
            R(TravelMode.Subway, 100, 100, 3), R(TravelMode.Subway, 100, 2000, 1900.0 / 10),
            R(TravelMode.Walk, 2000, 2040, 40.0 / 2))!;
        Assert.True(TransportComposition.ModeLowerBound(TravelMode.Subway, P(0), P(2040), prefs, "resident", 2)
            <= TransportComposition.Score(train, 3, prefs, "resident"));
        // Walking a long detour to collect an already parked car is an unavoidable cost too.
        Assert.True(TransportComposition.ModeLowerBound(TravelMode.Car, P(0), P(2040), prefs, "resident", 2, P(-1000)) > carBound);
    }

    [Fact]
    public void ShortWalkWinsAgainstUnavoidableBoardingAndParkingCosts()
    {
        var prefs = new TravelPreferences { OwnsCar = true };
        var walk = R(TravelMode.Walk, 0, 60, 60.0 / 84);
        double walkScore = TransportComposition.Score(walk, 0, prefs, "short-trip");
        Assert.True(walkScore < TransportComposition.ModeLowerBound(TravelMode.Car, P(0), P(60), prefs, "short-trip", 84));
        Assert.True(walkScore < TransportComposition.ModeLowerBound(TravelMode.Subway, P(0), P(60), prefs, "short-trip", 84));
        // The bound remains conservative even under a custom pedestrian speed faster than the train.
        var fast = R(TravelMode.Walk, 0, 60, 0.06);
        Assert.True(TransportComposition.ModeLowerBound(TravelMode.Walk, P(0), P(60), prefs, "short-trip", 1000)
            <= TransportComposition.Score(fast, 0, prefs, "short-trip"));
    }

    [Fact]
    public void ShortFirstWalkLeavesOwnedCarAtHomeForLaterRetrieval()
    {
        var home = new Place { Id = "home", Position = P(0) };
        var person = new CitizenState { Identity = new CitizenIdentity { Id = "walker" }, HomeId = home.Id,
            PlaceId = home.Id, Phase = CitizenPhase.AtPlace, Preferences = new TravelPreferences { OwnsCar = true } };
        Assert.True(TransportComposition.TryAllocateHomeCar(person, home, home, P(20), R(TravelMode.Walk, 0, 20)));
        Assert.Equal("walker:car", person.Car!.Id);
        // Walking is cheaper for this first trip, but allocation already happened and is not mode-dependent.
        var walk = R(TravelMode.Walk, 0, 60, 60.0 / 84);
        Assert.True(TransportComposition.Score(walk, 0, person.Preferences, person.Identity.Id)
            < TransportComposition.ModeLowerBound(TravelMode.Car, P(0), P(60), person.Preferences, person.Identity.Id, 84, P(20)));
        person.PlaceId = "shop";
        var car = person.Car;
        Assert.False(TransportComposition.TryAllocateHomeCar(person, new Place { Id = "shop", Position = P(60) }, home,
            P(60), R(TravelMode.Walk, 0, 60)));
        Assert.Same(car, person.Car);
        Assert.Equal(20, person.Car.ParkedPosition.X);
        person.Car = null;
        Assert.False(TransportComposition.TryAllocateHomeCar(person, new Place { Id = "shop", Position = P(60) }, home,
            P(60), R(TravelMode.Walk, 0, 60)));
        Assert.Null(person.Car);
    }

    [Fact]
    public void ReopenedOffPathClosureWinsImmediatelyWithoutAStaleLegalDetour()
    {
        var nodes = new[] {
            new TransportRoadNode { Position = P(0), AllowsCar = true, KilometresPerHour = 30, Successors = new[] { 1, 2 } },
            new TransportRoadNode { Position = P(20), AllowsCar = true, KilometresPerHour = 30, Successors = new[] { 3 } },
            new TransportRoadNode { Position = new Position(20, 0, 20), AllowsCar = true, KilometresPerHour = 30, Successors = new[] { 3 } },
            new TransportRoadNode { Position = P(40), AllowsCar = true, KilometresPerHour = 30 }
        };
        var graph = new TransportRoadGraph(nodes);
        bool closed = true;
        bool Disabled(int index) => closed && index == 1;
        var detour = graph.Plan(0, 3, Disabled, (_, _) => true)!;
        Assert.Equal(20, detour.Legs[0].To.Z);
        closed = false;
        var reopened = graph.Plan(0, 3, Disabled, (_, _) => true)!;
        var reloaded = new TransportRoadGraph(nodes).Plan(0, 3, Disabled, (_, _) => true)!;
        Assert.Equal(0, reopened.Legs[0].To.Z);
        Assert.True(reopened.Minutes < detour.Minutes);
        Assert.Equal(reloaded.Minutes, reopened.Minutes);
    }

    [Fact]
    public void ReturningHomeChoosesFastestFeasibleTripWithoutFutureRetrievalPenalty()
    {
        var prefs = new TravelPreferences { OwnsCar = true, FareWeightMinutes = 0 };
        var walk = R(TravelMode.Walk, 600, 0, 600.0 / 84);
        var drive = TransportComposition.Join(R(TravelMode.Car, 600, 600, 2), R(TravelMode.Car, 600, 0, 6), R(TravelMode.Car, 0, 0, 2))!;
        double penalty = 60; // Historical preference costs no longer affect elapsed-time selection.
        double rawWalk = TransportComposition.Score(walk, 0, prefs, "homebound");
        double rawDrive = TransportComposition.Score(drive, 0, prefs, "homebound");
        Assert.True(rawWalk < rawDrive);
        Assert.True(TransportComposition.Score(walk, 0, prefs, "homebound", penalty)
            < TransportComposition.Score(drive, 0, prefs, "homebound", penalty));
        var subway = R(TravelMode.Subway, 600, 0, 600.0 / 10);
        Assert.Equal(0, TransportComposition.Score(subway, 0, prefs, "homebound", penalty)
            - TransportComposition.Score(subway, 0, prefs, "homebound"), 8);
        double subwayBound = TransportComposition.ModeLowerBound(TravelMode.Subway, P(600), P(0), prefs, "homebound", 84,
            nonCarRetrievalPenalty: penalty);
        Assert.True(subwayBound <= TransportComposition.Score(subway, 0, prefs, "homebound", penalty));
        // The fastest choice still requires the owned car at the physical pickup position.
        Assert.False(TransportComposition.Valid(drive, P(600), P(0), prefs, P(0)));
    }

    [Fact]
    public void UnknownGateFallbackBlocksItsLocalRegionWithoutClosingUnrelatedStreets()
    {
        var min = new Position(-20, -10, -20); var max = new Position(20, 10, 20);
        Assert.True(TransportAccessGeometry.IntersectsBounds(P(-30), P(30), min, max));
        Assert.True(TransportAccessGeometry.IntersectsBounds(P(0), P(0), min, max));
        Assert.False(TransportAccessGeometry.IntersectsBounds(P(100), P(200), min, max));
        Assert.False(TransportAccessGeometry.IntersectsBounds(new Position(-30, 0, 30), new Position(30, 0, 30), min, max));
        Assert.False(TransportAccessGeometry.IntersectsBounds(new Position(-30, 30, 0), new Position(30, 30, 0), min, max));
        // Overlapping segment/box envelopes alone are insufficient: this diagonal misses the actual box.
        Assert.False(TransportAccessGeometry.IntersectsBounds(new Position(10, 0, 40), new Position(40, 0, 10), min, max));
    }
    [Theory]
    [InlineData(1.0, 3.6)]
    [InlineData(0.5, 1.8)]
    [InlineData(2.0, 7.2)]
    public void RoadTravelUsesNativeMetresPerSecondAndGameClock(double clockRate, double expectedMinutes)
    {
        var graph = new TransportRoadGraph(new[] {
            new TransportRoadNode { Position = P(0), AllowsCar = true, KilometresPerHour = 40, Successors = new[] { 1 } },
            new TransportRoadNode { Position = P(40), AllowsCar = true, KilometresPerHour = 40 }
        }, clockRate);
        var route = graph.Plan(0, 1, _ => false, (_, _) => true)!;
        Assert.Equal(expectedMinutes, route.Minutes, 8);
        var preferences = new TravelPreferences { OwnsCar = true };
        var complete = TransportComposition.Join(R(TravelMode.Car, 0, 0, 2), route, R(TravelMode.Car, 40, 40, 2))!;
        double lower = TransportComposition.ModeLowerBound(TravelMode.Car, P(0), P(40), preferences, "clock", 2,
            gameMinutesPerScaledSecond: clockRate);
        Assert.True(lower <= TransportComposition.Score(complete, 0, preferences, "clock") + 0.0000001);
        Assert.Equal(4 + expectedMinutes, lower, 8);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public void BoundsRemainConservativeForClockAdjustedSubwayAndFasterWalking(double clockRate)
    {
        var prefs = new TravelPreferences();
        double trainMinutes = 3 + 120 / (10 / clockRate);
        var route = R(TravelMode.Subway, 0, 120, trainMinutes);
        foreach (double walk in new[] { 2.0, 1000.0 })
            Assert.True(TransportComposition.ModeLowerBound(TravelMode.Subway, P(0), P(120), prefs, "clock", walk,
                gameMinutesPerScaledSecond: clockRate) <= TransportComposition.Score(route, 3, prefs, "clock"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidClockRateCannotCreateAZeroOrNegativeDuration(double clockRate)
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(() => new TransportRoadGraph(System.Array.Empty<TransportRoadNode>(), clockRate));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => TransportComposition.ModeLowerBound(TravelMode.Car,
            P(0), P(40), new TravelPreferences(), "clock", 2, gameMinutesPerScaledSecond: clockRate));
    }

    [Fact]
    public void NativeRoadIdentitySurvivesCompositionAndSaveRoundTrip()
    {
        var state = Population.Create(new[] { new CitizenIdentity { Id = "road", Name = "Road Person", Age = 30, Persona = Persona.Commuter } },
            new[] { new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 1, Position = P(0) },
                new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = 1, Position = P(40) } },
            "road-test", "0.40.4", 7, 1440);
        var citizen = state.Citizens[0];
        citizen.Preferences.OwnsCar = true;
        citizen.Car = new CarState { Id = citizen.Identity.Id + ":car", ParkedPosition = P(0) };
        citizen.PlaceId = "home"; citizen.DayPlaceId = "work"; citizen.Phase = CitizenPhase.Travelling;
        citizen.Activity = Activity.Home; citizen.DestinationId = "work"; citizen.DestinationActivity = Activity.Work;
        citizen.DepartureMinute = state.Minute; citizen.NextEventMinute = state.Minute + 4; citizen.ActivityUntilMinute = 2880;
        string tag = "native:road:v1:" + new string('a', 64) + ":12:13";
        var part = R(TravelMode.Car, 0, 40, 4).WithEstimate(9); part.Legs[0].ServiceId = tag;
        citizen.Journey = TransportComposition.Join(part)!;
        Assert.Equal(tag, citizen.Journey.Legs[0].ServiceId);
        Assert.Equal(9, citizen.Journey.EstimatedMinutes);
        string wire = SaveCodec.Write(state);
        Assert.DoesNotContain("EstimatedMinutes", wire);
        var read = SaveCodec.Read(wire);
        Assert.Equal(SaveReadStatus.Loaded, read.Status);
        Assert.Equal(tag, read.State!.Citizens[0].Journey!.Legs[0].ServiceId);
        Assert.Equal(citizen.Journey.PositionAt(2).X, read.State.Citizens[0].Journey!.PositionAt(2).X);
        Assert.Equal(4, read.State.Citizens[0].Journey!.EstimatedMinutes); // Reload has nominal progress, no stale forecast.
    }

}
