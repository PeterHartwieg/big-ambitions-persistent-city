using System;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed partial class PhysicalJourneyTests
{
    private sealed class Planner : IRoutePlanner
    {
        public Route Plan(Place from, Place to, double minute, TravelPreferences preferences) => new Route
        { Legs = new() { new RouteLeg { From = from.Position, To = to.Position, Minutes = 12, Mode = TravelMode.Walk } } };
    }

    private static CitySimulator Engine(int count = 1)
    {
        var state = Population.Create(Enumerable.Range(0, count).Select(i => new CitizenIdentity
        { Id = "c" + i, Name = "Person " + i, Age = 30, AppearanceSeed = i + 1, Persona = Persona.Commuter }), new[]
        {
            new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = count, Position = new Position(0, 0, 0) },
            new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = count, Position = new Position(120, 0, 0) }
        }, "physical-test", "0.40.4", 7, 1440);
        foreach (var c in state.Citizens)
        {
            c.PlaceId = "home"; c.DayPlaceId = "work"; c.Phase = CitizenPhase.Travelling;
            c.DestinationId = "work"; c.DestinationActivity = Activity.Work;
            c.DepartureMinute = state.Minute; c.NextEventMinute = state.Minute + 12; c.ActivityUntilMinute = 2880;
            c.Journey = new Planner().Plan(state.Places[0], state.Places[1], state.Minute, c.Preferences);
        }
        return new CitySimulator(state, new Planner());
    }

    // F2d: nothing is leased. Traffic legs use a two-section road (TrafficJourneyTests' shape).
    private const string Native = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private sealed class Roads : IRoutePlanner, ITrafficRoutePlanner
    {
        public TrafficNetwork? TrafficNetwork { get; set; } = new TrafficNetwork(Native, new[]
        {
            new TrafficNetworkNode { Id = 0, Position = new Position(0,0,0), KilometresPerHour = 36, AllowsCar = true, Boundary = true, Successors = new[] { 1 } },
            new TrafficNetworkNode { Id = 1, Position = new Position(12,0,0), KilometresPerHour = 36, AllowsCar = true, Boundary = true, Successors = new[] { 2 } },
            new TrafficNetworkNode { Id = 2, Position = new Position(24,0,0), KilometresPerHour = 36, AllowsCar = true, Boundary = true }
        }, 1);
        public void SetTrafficForecast(Func<int, double, TrafficLinkForecast>? forecast) { }
        public bool IsTrafficLinkClosed(int id) => false;
        public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences) => null;
    }

    private static CitySimulator Driving(int count = 1)
    {
        var engine = Engine(count);
        foreach (var c in engine.State.Citizens)
        {
            c.Preferences.OwnsCar = true;
            c.Car = new CarState { Id = c.Identity.Id + ":car", ParkedPosition = new Position(0,0,0) };
            // Two road sections, then the walk from the parked car to work (12 minutes in all).
            c.Journey = new Route { Legs = new() {
                new RouteLeg { From = new Position(0,0,0), To = new Position(12,0,0), Minutes = 1.2, Mode = TravelMode.Car, ServiceId = "native:road:v1:" + Native + ":0:1" },
                new RouteLeg { From = new Position(12,0,0), To = new Position(24,0,0), Minutes = 1.2, Mode = TravelMode.Car, ServiceId = "native:road:v1:" + Native + ":1:2" },
                new RouteLeg { From = new Position(24,0,0), To = new Position(120,0,0), Minutes = 9.6, Mode = TravelMode.Walk } } };
        }
        engine = new CitySimulator(engine.State, new Roads());
        engine.AdvanceTo(1440.01);
        return engine;
    }

    [Fact]
    public void WalkLegsArriveOnTheLogicalClock()
    {
        var engine = Engine(); var c = engine.State.Citizens[0];
        engine.AdvanceTo(1446);
        Assert.Equal(60, engine.PositionOf(c).X);
        engine.AdvanceTo(2000);
        Assert.Equal(1, c.CompletedJourneys); Assert.Single(c.RecentVisits);
        Assert.Equal(1452, c.RecentVisits[0].AtMinute);
        var plain = Engine(); plain.AdvanceTo(2000);
        Assert.Equal(SaveCodec.Write(plain.State), SaveCodec.Write(engine.State));
    }

    [Fact]
    public void TrafficLegsRunOnTheKernelClockAndTheWalkAfterParkingIsNotFrozen()
    {
        var engine = Driving(); var c = engine.State.Citizens[0];
        var plain = Driving();
        double completed = double.NaN;
        engine.Traffic!.VehicleCompleted += (id, at) => completed = at;
        // Nobody reports progress: the kernel completes the vehicle at its own pace, never under physical control.
        for (double minute = 1441; minute < 1470 && double.IsNaN(completed); minute += 1)
        {
            engine.AdvanceTo(minute); plain.AdvanceTo(minute);
        }
        Assert.False(double.IsNaN(completed));
        Assert.Equal(CitizenPhase.Travelling, c.Phase);
        Assert.Equal(TravelMode.Walk, engine.SampleJourney(c).Mode);
        // The walk runs on the logical clock: arrival exactly its leg minutes after the traffic completion.
        Assert.Equal(completed + 9.6, c.NextEventMinute, 9);
        engine.AdvanceTo(c.NextEventMinute + 1); plain.AdvanceTo(engine.State.Minute);
        Assert.Equal(1, c.CompletedJourneys);
        Assert.Equal(SaveCodec.Write(plain.State), SaveCodec.Write(engine.State));
    }

    [Theory]
    [InlineData(TravelMode.Car)] [InlineData(TravelMode.Subway)]
    public void StationaryAndTravelBoundarySamplesAndReleasePreserveGeometry(TravelMode mode)
    {
        var engine = Engine(); var c = engine.State.Citizens[0];
        c.Preferences.OwnsCar = mode == TravelMode.Car;
        if (c.Preferences.OwnsCar) c.Car = new CarState { Id = c.Identity.Id + ":car", ParkedPosition = new Position(20, 0, 0) };
        c.Journey!.Legs = new()
        {
            new RouteLeg { From = new Position(0,0,0), To = new Position(20,0,0), Minutes = 2, Mode = TravelMode.Walk },
            new RouteLeg { From = new Position(20,0,0), To = new Position(20,0,0), Minutes = 2, Mode = TravelMode.Walk, ServiceId = "preparation" },
            new RouteLeg { From = new Position(20,0,0), To = new Position(100,0,0), Minutes = 6, Mode = mode },
            new RouteLeg { From = new Position(100,0,0), To = new Position(120,0,0), Minutes = 2, Mode = TravelMode.Walk }
        };
        var sample = engine.SampleJourney(c, 2); Assert.True(sample.Stationary); Assert.Equal(mode, sample.NextMode);
        Assert.Equal(2, sample.RemainingLegMinutes); Assert.Equal(0, sample.Direction.X);
        sample = engine.SampleJourney(c, 4); Assert.Equal(2, sample.LegIndex); Assert.Equal(mode, sample.Mode);
        Assert.Equal(0, sample.LegFraction); Assert.Equal(1, sample.Direction.X);
        engine.AdvanceTo(1447);
        Assert.Equal(60, engine.PositionOf(c).X);
        if (mode == TravelMode.Car) Assert.Equal(60, engine.CarPositionOf(c)!.Value.X);
        Assert.Equal(100, engine.SampleJourney(c, 10).CarPosition?.X ?? 100);
        Assert.True(engine.SampleJourney(c, 12).AtEnd);
    }
    [Fact]
    public void FractionalPrefixesCannotTrapACompletedPhysicalCornerOrFinalArrival()
    {
        var engine = Engine(); var c = engine.State.Citizens[0];
        double[] prefix = { 2.2355080526319506, 14.833594490199058, 4.9053881575314895,
            2.7993631912176262, 2.0588785716684344, 14.815948215906838 };
        c.Journey!.Legs.Clear();
        for (int i = 0; i < prefix.Length; i++)
            c.Journey.Legs.Add(new RouteLeg { From = new Position(i,0,0), To = new Position(i+1,0,0), Minutes = prefix[i], Mode = TravelMode.Walk });
        c.Journey.Legs.Add(new RouteLeg { From = new Position(6,0,0), To = new Position(7,0,0), Minutes = 10.91187700962951, Mode = TravelMode.Walk });
        c.Journey.Legs.Add(new RouteLeg { From = new Position(7,0,0), To = new Position(120,0,0), Minutes = .7, Mode = TravelMode.Walk });
        c.NextEventMinute = c.DepartureMinute + c.Journey.Minutes;
        // Reconstruct to rebuild the due queue after preparing the committed route fixture.
        engine = new CitySimulator(engine.State, new Planner());
        var sample = engine.SampleJourney(c, 48.09206235010644);
        double corner = sample.ElapsedMinutes + (1 - sample.LegFraction) * sample.LegMinutes;
        var next = engine.SampleJourney(c, corner);
        Assert.Equal(7, next.LegIndex); Assert.Equal(0, next.LegFraction); Assert.Equal(7, next.Position.X);
        double final = next.ElapsedMinutes + (1 - next.LegFraction) * next.LegMinutes;
        var end = engine.SampleJourney(c, final);
        Assert.True(end.AtEnd); Assert.Equal(c.Journey.Minutes, end.ElapsedMinutes); Assert.Equal(1, end.LegFraction);
        // A legitimately unfinished point remains on the old leg.
        Assert.Equal(6, engine.SampleJourney(c, corner - .000001).LegIndex);
    }

    [Theory]
    [InlineData(1)] [InlineData(3)] [InlineData(6)]
    public void SamplingJustAboveFractionalCornerNeverRegressesAcrossReload(int prefixCount)
    {
        var engine = Engine(); var c = engine.State.Citizens[0];
        double[] durations = { 2.2355080526319506, 14.833594490199058, 4.9053881575314895,
            2.7993631912176262, 2.0588785716684344, 14.815948215906838 };
        c.Journey!.Legs.Clear(); double boundary = 0;
        for (int i = 0; i < prefixCount; i++)
        {
            boundary += durations[i];
            c.Journey.Legs.Add(new RouteLeg { From = new Position(i,0,0), To = new Position(i+1,0,0), Minutes = durations[i], Mode = TravelMode.Walk });
        }
        c.Journey.Legs.Add(new RouteLeg { From = new Position(prefixCount,0,0), To = new Position(120,0,0), Minutes = 10.91187700962951, Mode = TravelMode.Walk });
        c.NextEventMinute = c.DepartureMinute + c.Journey.Minutes;
        engine = new CitySimulator(engine.State, new Planner());
        engine.AdvanceTo(1440 + Math.BitIncrement(boundary));
        for (int pass = 0; pass < 2; pass++)
        {
            var sample = engine.SampleJourney(c);
            double observed = sample.ElapsedMinutes;
            // A walker projecting itself onto the sampled pose never moves behind it.
            Assert.True(WalkerFollow.Advance(c.Journey!, 0, observed, sample.Position) <= observed);
            Assert.True(PhysicalProgress.TryProject(sample, sample.Position, false, out double projected));
            Assert.True(projected >= observed);
            var loaded = SaveWire.Read(SaveCodec.Write(engine.State)); Assert.Equal(SaveReadStatus.Loaded, loaded.Status);
            engine = new CitySimulator(loaded.State!, new Planner()); c = engine.State.Citizens[0];
            Assert.True(engine.SampleJourney(c).ElapsedMinutes >= observed, "A reload must never lower the logical progress.");
        }
        var end = engine.SampleJourney(c, c.Journey!.Minutes);
        Assert.True(PhysicalProgress.TryProject(end, end.To, false, out double completed));
        Assert.Equal(c.Journey.Minutes, completed);
    }

    [Fact]
    public void CompletedJourneyAtLargeFractionalClockCanSampleAndRetimeBeforeQueuedArrival()
    {
        var random = new Random(3417);
        for (int example = 0; example < 128; example++)
        {
            var engine = Engine(); var state = engine.State; var c = state.Citizens[0];
            state.Minute = 144000.37 + example * .1;
            c.Journey!.Legs.Clear(); int count = 3 + example % 8;
            for (int leg = 0; leg < count; leg++)
                c.Journey.Legs.Add(new RouteLeg { From = new Position(120.0 * leg / count,0,0),
                    To = new Position(120.0 * (leg+1) / count,0,0), Minutes = .01 + random.NextDouble() * 70, Mode = TravelMode.Walk });
            c.DepartureMinute = state.Minute; c.NextEventMinute = state.Minute + c.Journey.Minutes;
            engine = new CitySimulator(state, new Planner());
            // The completed state a traffic completion (or, before F2c, a walk lease) leaves: departure = clock - duration.
            double total = c.Journey.Minutes;
            c.DepartureMinute = state.Minute - total; c.NextEventMinute = state.Minute;
            var arrived = engine.SampleJourney(c); Assert.True(arrived.AtEnd); Assert.Equal(120, arrived.Position.X);
            state.TravelTimingVersion = 1; state.TravelClockRate = 1;
            Assert.True(TravelTiming.Apply(state, 2, 2));
            Assert.Equal(state.Minute, c.NextEventMinute);
            Assert.True(engine.SampleJourney(c).AtEnd);
            // A meaningful overshoot remains invalid rather than being hidden by the roundoff fix.
            c.DepartureMinute -= .000001;
            Assert.Throws<ArgumentOutOfRangeException>(() => engine.SampleJourney(c));
            Assert.Throws<ArgumentException>(() => TravelTiming.Apply(state, 2, 3));
        }
    }
}
