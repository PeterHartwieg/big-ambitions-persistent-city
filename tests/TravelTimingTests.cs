using System;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class TravelTimingTests
{
    private sealed class Planner : IRoutePlanner
    {
        public Route? Plan(Place from, Place to, double departureMinute, TravelPreferences preferences) => null;
    }

    private static CityState City(TravelMode mode = TravelMode.Walk, int count = 1, double now = 1443)
    {
        var state = Population.Create(Enumerable.Range(0, count).Select(i => new CitizenIdentity
        { Id = "timing" + i, Name = "Timing " + i, Age = 30, AppearanceSeed = i + 1 }), new[]
        {
            new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = count, Position = new Position(0,0,0) },
            new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = count, Position = new Position(120,0,0) }
        }, "timing-test", "0.40.4", 7, Math.Min(now, 1440));
        state.Minute = now; state.TravelTimingVersion = 0; state.TravelClockRate = 1;
        foreach (var c in state.Citizens)
        {
            c.PlaceId = "home"; c.DayPlaceId = "work"; c.Phase = CitizenPhase.Travelling;
            c.DestinationId = "work"; c.DestinationActivity = Activity.Work; c.DepartureMinute = Math.Min(now, 1440);
            c.NextEventMinute = c.DepartureMinute + 12; c.ActivityUntilMinute = 2400;
            c.Journey = new Route { Legs = new() { new RouteLeg { From = state.Places[0].Position, To = state.Places[1].Position, Minutes = 12, Mode = mode, ServiceId = "road:whole-edge" } } };
            if (mode == TravelMode.Car)
            {
                c.Preferences.OwnsCar = true;
                c.Car = new CarState { Id = c.Identity.Id + ":car", ParkedPosition = state.Places[0].Position };
            }
        }
        return state;
    }

    private static Position Position(CityState state) => new CitySimulator(state, new Planner()).PositionOf(state.Citizens[0]);
    private static Position? CarPosition(CityState state) => new CitySimulator(state, new Planner()).CarPositionOf(state.Citizens[0]);

    [Fact]
    public void InterruptedWalkPreservesPositionAndRetimesOnlyRemainingDistance()
    {
        var state = City(); var c = state.Citizens[0]; var original = c.Journey; var before = Position(state);
        Assert.True(TravelTiming.Apply(state, 2, 1));
        Assert.NotSame(original, c.Journey); Assert.Equal(1440, c.DepartureMinute);
        Assert.Equal(before.X, Position(state).X); Assert.Equal(30, Position(state).X);
        Assert.Equal(1443 + 90.0 / 2, c.NextEventMinute);
        Assert.Equal(2, c.Journey!.Legs.Count); Assert.All(c.Journey.Legs, l => Assert.Equal("road:whole-edge", l.ServiceId));
        Assert.Equal(1, state.TravelTimingVersion); Assert.Equal(1, state.TravelClockRate);
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(SaveCodec.Write(state)).Status);
    }

    [Fact]
    public void InterruptedCarRetainsCarAndCitizenPoseAndFutureWholeRoadTags()
    {
        var state = City(TravelMode.Car); var c = state.Citizens[0];
        c.Journey!.Legs = new()
        {
            new RouteLeg { From = new Position(0,0,0), To = new Position(60,0,0), Minutes = 6, Mode = TravelMode.Car, ServiceId = "native:road:v1:timing-native:0:1" },
            new RouteLeg { From = new Position(60,0,0), To = new Position(120,0,0), Minutes = 6, Mode = TravelMode.Car, ServiceId = "native:road:v1:timing-native:1:2" }
        };
        var before = Position(state); var parkedBefore = c.Car!.ParkedPosition; var carBefore = CarPosition(state);
        Assert.True(TravelTiming.Apply(state, 2, 0.5));
        Assert.Equal(before.X, Position(state).X); Assert.Equal(carBefore!.Value.X, CarPosition(state)!.Value.X);
        Assert.Equal(parkedBefore.X, c.Car.ParkedPosition.X);
        Assert.Equal(1443 + 9 * 60 * 0.5, c.NextEventMinute);
        Assert.Equal("native:road:fragment1:timing-native:0:1", c.Journey.Legs[0].ServiceId);
        Assert.Equal("native:road:fragment1:timing-native:0:1", c.Journey.Legs[1].ServiceId);
        Assert.Equal("native:road:v1:timing-native:1:2", c.Journey.Legs[2].ServiceId);
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(SaveCodec.Write(state)).Status);
    }

    [Theory]
    [InlineData(TravelMode.Subway)] [InlineData(TravelMode.Bus)]
    public void LegacyTransitMovingTimeUsesNativeSeconds(TravelMode mode)
    {
        var state = City(mode); TravelTiming.Apply(state, 2, 2);
        Assert.Equal(1443 + 9 * 60 * 2, state.Citizens[0].NextEventMinute);
        Assert.Equal(30, Position(state).X);
    }

    [Fact]
    public void InterruptedSubwayPreservesRequiredServiceAcrossSplitAndSaveReload()
    {
        var state = City(TravelMode.Subway); var c = state.Citizens[0]; c.Journey!.Legs[0].ServiceId = "subway:line-1";
        var before = Position(state); TravelTiming.Apply(state, 2, 1);
        Assert.Equal(before.X, Position(state).X); Assert.Equal(2, c.Journey.Legs.Count);
        Assert.All(c.Journey.Legs, leg => Assert.Equal("subway:line-1", leg.ServiceId));
        Assert.True(TransportComposition.Valid(c.Journey, state.Places[0].Position, state.Places[1].Position, c.Preferences));
        var read = SaveCodec.Read(SaveCodec.Write(state)); Assert.Equal(SaveReadStatus.Loaded, read.Status);
        Assert.Equal(before.X, Position(read.State!).X);
        Assert.All(read.State!.Citizens[0].Journey!.Legs, leg => Assert.Equal("subway:line-1", leg.ServiceId));
    }

    [Fact]
    public void CompletedLegsAndStationaryWaitsRemainContinuous()
    {
        var state = City(now: 1445); var c = state.Citizens[0];
        c.Journey!.Legs = new()
        {
            new RouteLeg { From = new Position(0,0,0), To = new Position(20,0,0), Minutes = 2, Mode = TravelMode.Walk, ServiceId = "done" },
            new RouteLeg { From = new Position(20,0,0), To = new Position(20,0,0), Minutes = 6, Mode = TravelMode.Walk, ServiceId = "wait" },
            new RouteLeg { From = new Position(20,0,0), To = new Position(120,0,0), Minutes = 4, Mode = TravelMode.Walk, ServiceId = "future" }
        };
        TravelTiming.Apply(state, 2, 2);
        Assert.Equal(20, Position(state).X); Assert.Equal(2, c.Journey.Legs[0].Minutes); Assert.Equal("done", c.Journey.Legs[0].ServiceId);
        Assert.Equal(1445 + 3 + 100.0 / 2 * 2, c.NextEventMinute);
        Assert.Equal("future", c.Journey.Legs[3].ServiceId);
    }

    [Fact]
    public void LowClockAnchorNeverBecomesNegativeWhenRemainingTravelExpands()
    {
        var state = City(now: 1); var c = state.Citizens[0]; state.Minute = 1.5;
        var before = Position(state); TravelTiming.Apply(state, 0.5, 1000);
        Assert.Equal(1, c.DepartureMinute); Assert.Equal(before.X, Position(state).X);
        Assert.True(c.NextEventMinute > 100000);
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(SaveCodec.Write(state)).Status);
    }

    [Fact]
    public void MaximumLegRouteKeepsCurrentLegTimingAndRetimesFutureWithoutTruncation()
    {
        var state = City(now: 1440.5); var c = state.Citizens[0]; c.DepartureMinute = 1440;
        c.Journey!.Legs = Enumerable.Range(0, 512).Select(i => new RouteLeg
        { From = new Position(i * 120.0 / 512,0,0), To = new Position((i + 1) * 120.0 / 512,0,0), Minutes = 1, Mode = TravelMode.Walk, ServiceId = "edge:" + i }).ToList();
        c.NextEventMinute = 1440 + 512;
        var before = Position(state); TravelTiming.Apply(state, 1, 1);
        Assert.Equal(512, c.Journey.Legs.Count); Assert.Equal(before.X, Position(state).X);
        Assert.Equal(1, c.Journey.Legs[0].Minutes); Assert.Equal("edge:0", c.Journey.Legs[0].ServiceId);
        Assert.Equal(120.0 / 512, c.Journey.Legs[1].Minutes);
        Assert.Equal(120, c.Journey.Legs[511].To.X);
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(SaveCodec.Write(state)).Status);
    }

    [Fact]
    public void VersionOneClockChangeScalesRemainingTimeAndSameRateReloadIsIdentical()
    {
        var state = City(); state.TravelTimingVersion = 1; state.TravelClockRate = 2;
        var before = Position(state); TravelTiming.Apply(state, 2, 0.5);
        Assert.Equal(before.X, Position(state).X); Assert.Equal(1443 + 9 * 0.25, state.Citizens[0].NextEventMinute);
        string saved = SaveCodec.Write(state); var read = SaveCodec.Read(saved); Assert.Equal(SaveReadStatus.Loaded, read.Status);
        Assert.False(TravelTiming.Apply(read.State!, 2, 0.5)); Assert.Equal(saved, SaveCodec.Write(read.State!));
    }

    [Fact]
    public void EveryActualTimingChangeReschedulesSafelyHomeCitizens()
    {
        var state = City(); var c = state.Citizens[0]; c.Phase = CitizenPhase.AtPlace; c.Activity = Activity.Home;
        c.DestinationId = ""; c.Journey = null; c.DepartureMinute = 0; c.NextEventMinute = 1900;
        TravelTiming.Apply(state, 2, 1); Assert.Equal(state.Minute, c.NextEventMinute);
        c.NextEventMinute = 1900;
        Assert.False(TravelTiming.Apply(state, 2, 1)); Assert.Equal(1900, c.NextEventMinute);
        Assert.True(TravelTiming.Apply(state, 2, 2)); Assert.Equal(state.Minute, c.NextEventMinute);
    }

    [Fact]
    public void AFailedLaterCitizenLeavesEarlierStagedChangesAndMetadataUntouched()
    {
        var state = City(count: 2); var firstRoute = state.Citizens[0].Journey; double firstEvent = state.Citizens[0].NextEventMinute;
        state.Citizens[1].Journey!.Legs[0].Minutes = 0;
        Assert.ThrowsAny<ArgumentException>(() => TravelTiming.Apply(state, 2, 1));
        Assert.Same(firstRoute, state.Citizens[0].Journey); Assert.Equal(firstEvent, state.Citizens[0].NextEventMinute);
        Assert.Equal(0, state.TravelTimingVersion); Assert.Equal(1, state.TravelClockRate);
    }

    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(0.009)] [InlineData(1001)]
    public void InvalidRatesAndWalkSpeedAreRejectedWithoutChanges(double value)
    {
        var state = City(); var route = state.Citizens[0].Journey;
        Assert.Throws<ArgumentOutOfRangeException>(() => TravelTiming.Apply(state, 2, value));
        Assert.Throws<ArgumentOutOfRangeException>(() => TravelTiming.Apply(state, value, 1));
        Assert.Same(route, state.Citizens[0].Journey); Assert.Equal(0, state.TravelTimingVersion);
    }

    [Fact]
    public void ExcessiveRetimedDurationAndFutureTimingVersionAreAtomicFailures()
    {
        var state = City(); var route = state.Citizens[0].Journey;
        Assert.Throws<ArgumentException>(() => TravelTiming.Apply(state, 0.01, 1000));
        Assert.Same(route, state.Citizens[0].Journey); Assert.Equal(0, state.TravelTimingVersion);
        state.TravelTimingVersion = 2;
        Assert.Throws<ArgumentException>(() => TravelTiming.Apply(state, 2, 1)); Assert.Same(route, state.Citizens[0].Journey);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void ClockChangePreservesRemainingStationWaitInGameMinutes(int timingVersion)
    {
        var state = City(now: 1445); var c = state.Citizens[0]; state.TravelTimingVersion = timingVersion;
        c.Journey!.Legs = new()
        {
            new RouteLeg { From = new Position(0,0,0), To = new Position(20,0,0), Minutes = 2, Mode = TravelMode.Walk },
            new RouteLeg { From = new Position(20,0,0), To = new Position(20,0,0), Minutes = 6, Mode = TravelMode.Walk, ServiceId = "station-wait" },
            new RouteLeg { From = new Position(20,0,0), To = new Position(120,0,0), Minutes = 4, Mode = TravelMode.Walk }
        };
        Assert.True(TravelTiming.Apply(state, 2, 2));
        Assert.Equal(3, c.Journey.Legs[1].Minutes); Assert.Equal(3, c.Journey.Legs[2].Minutes);
        Assert.Equal(1445 + 3 + (timingVersion == 0 ? 100 : 8), c.NextEventMinute);
        Assert.Equal(20, Position(state).X);
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(SaveCodec.Write(state)).Status);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void FuturePreparationWaitRemainsThreeGameMinutesAcrossClockChange(int timingVersion)
    {
        var state = City(now: 1441); var c = state.Citizens[0]; state.TravelTimingVersion = timingVersion;
        c.Journey!.Legs = new()
        {
            new RouteLeg { From = new Position(0,0,0), To = new Position(20,0,0), Minutes = 2, Mode = TravelMode.Walk },
            new RouteLeg { From = new Position(20,0,0), To = new Position(20,0,0), Minutes = 3, Mode = TravelMode.Walk, ServiceId = "preparation" },
            new RouteLeg { From = new Position(20,0,0), To = new Position(120,0,0), Minutes = 7, Mode = TravelMode.Walk }
        };
        TravelTiming.Apply(state, 2, 2);
        Assert.Equal(3, c.Journey.Legs.Single(leg => leg.ServiceId == "preparation").Minutes);
        Assert.Equal(1441 + (timingVersion == 0 ? 10 + 3 + 100 : 2 + 3 + 14), c.NextEventMinute);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void ExplicitCompletedArrivalAtHighClockRetimesDespiteSingleUlpReaddition(int timingVersion)
    {
        const double clock = 900000439.17;
        const double duration = 567634.374533236;
        var state = City(); var c = state.Citizens[0]; state.Minute = clock;
        c.DepartureMinute = clock; c.Journey!.Legs[0].Minutes = duration; c.NextEventMinute = clock + duration;
        var engine = new CitySimulator(state, new Planner());
        Assert.True(engine.TryAcquirePhysicalJourney(c.Identity.Id, out var lease));
        Assert.True(engine.ObservePhysicalJourney(lease!, duration));
        Assert.True(Math.Abs(c.NextEventMinute - (c.DepartureMinute + c.Journey.Minutes)) > 1e-7);
        state.TravelTimingVersion = timingVersion; state.TravelClockRate = 1;
        Assert.True(TravelTiming.Apply(state, 2, 2));
        Assert.Equal(clock, c.NextEventMinute); Assert.True(engine.SampleJourney(c).AtEnd);
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(SaveCodec.Write(state)).Status);
        // Merely near the clock is not an explicit completed arrival and cannot use this exception.
        c.NextEventMinute = clock + .00001;
        Assert.Throws<ArgumentException>(() => TravelTiming.Apply(state, 2, 3));
    }


    [Fact]
    public void RepeatedClockConversionsRetainTheSameOriginalNativeEdgeProvenance()
    {
        var state = City(TravelMode.Car); var citizen = state.Citizens[0];
        state.TravelTimingVersion = 1;
        citizen.Journey!.Legs = new()
        {
            new RouteLeg { From = new Position(0,0,0), To = new Position(60,0,0), Minutes = 6,
                Mode = TravelMode.Car, ServiceId = "native:road:v1:timing-native:0:1" },
            new RouteLeg { From = new Position(60,0,0), To = new Position(120,0,0), Minutes = 6,
                Mode = TravelMode.Car, ServiceId = "native:road:v1:timing-native:1:2" }
        };
        Assert.True(TravelTiming.Apply(state, 2, 2));
        state.Minute = 1444;
        double before = Position(state).X;
        Assert.True(TravelTiming.Apply(state, 2, 0.5));
        Assert.Equal(before, Position(state).X, 8);
        Assert.Equal(4, citizen.Journey.Legs.Count);
        Assert.All(citizen.Journey.Legs.Take(3), leg => Assert.Equal("native:road:fragment1:timing-native:0:1", leg.ServiceId));
        Assert.Equal("native:road:v1:timing-native:1:2", citizen.Journey.Legs[3].ServiceId);
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(SaveCodec.Write(state)).Status);
    }

    [Theory]
    [InlineData("native:road:v1:missing")]
    [InlineData("native:road:v1:timing-native:bad:1")]
    [InlineData("native:road:v1:timing-native:-1:1")]
    [InlineData("native:road:v2:timing-native:0:1")]
    public void MalformedNativeLookingTagsRetainLegacyClearingBehaviorWithoutBecomingProvenance(string service)
    {
        var state = City(TravelMode.Car); var citizen = state.Citizens[0];
        citizen.Journey!.Legs[0].ServiceId = service;
        Assert.True(TravelTiming.Apply(state, 2, 2));
        Assert.All(citizen.Journey.Legs, leg => Assert.Equal("", leg.ServiceId));
    }

    [Theory]
    [InlineData("v1")]
    [InlineData("fragment1")]
    public void UnownedMaximumSizeNativeCurrentEdgeCannotKeepOldPaceDuringClockConversion(string version)
    {
        var state = City(TravelMode.Car, now: 1440.5); var citizen = state.Citizens[0];
        state.TravelTimingVersion = 1; citizen.DepartureMinute = 1440;
        citizen.Journey!.Legs = Enumerable.Range(0, 512).Select(i => new RouteLeg
        {
            From = new Position(i * 120.0 / 512,0,0), To = new Position((i + 1) * 120.0 / 512,0,0),
            Minutes = 1, Mode = TravelMode.Car, ServiceId = "native:road:" + version + ":timing-native:" + i + ":" + (i + 1)
        }).ToList();
        citizen.NextEventMinute = 1440 + 512;
        Assert.Null(state.Traffic);
        string wire = SaveCodec.Write(state);
        Route original = citizen.Journey;
        Assert.Throws<ArgumentException>(() => TravelTiming.Apply(state, 2, 2));
        Assert.Same(original, citizen.Journey);
        Assert.Equal(wire, SaveCodec.Write(state));
        Assert.Equal(1, state.TravelClockRate);
    }


    [Fact]
    public void TinyNativeFragmentMovementIsRetimedInsteadOfBecomingAStationaryWait()
    {
        var state = City(TravelMode.Car, now: 1446 - 5e-9); var citizen = state.Citizens[0];
        state.TravelTimingVersion = 1; citizen.DepartureMinute = 1440;
        citizen.Journey!.Legs = new()
        {
            new RouteLeg { From = new Position(0,0,0), To = new Position(60 - 1e-7,0,0), Minutes = 6 - 1e-8,
                Mode = TravelMode.Car, ServiceId = "native:road:fragment1:timing-native:0:1" },
            new RouteLeg { From = new Position(60 - 1e-7,0,0), To = new Position(60,0,0), Minutes = 1e-8,
                Mode = TravelMode.Car, ServiceId = "native:road:fragment1:timing-native:0:1" },
            new RouteLeg { From = new Position(60,0,0), To = new Position(120,0,0), Minutes = 6,
                Mode = TravelMode.Car, ServiceId = "native:road:v1:timing-native:1:2" }
        };
        citizen.NextEventMinute = 1452;
        double elapsed = state.Minute - citizen.DepartureMinute;
        double remaining = 1e-8 - (elapsed - (6 - 1e-8));
        double before = Position(state).X;
        Assert.True(TravelTiming.Apply(state, 2, 2));
        Assert.Equal(before, Position(state).X, 8);
        Assert.Equal(4, citizen.Journey.Legs.Count);
        Assert.Equal(remaining * 2, citizen.Journey.Legs[2].Minutes, 12);
        Assert.Equal("native:road:fragment1:timing-native:0:1", citizen.Journey.Legs[2].ServiceId);
    }

}
