using System;
using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

public sealed class TravelTimingMaximumRouteTests
{
    private const int CarLegs = 508;
    private const double CarMinutes = 10;
    private const double Departure = 1440;
    private readonly ITestOutputHelper output;

    public TravelTimingMaximumRouteTests(ITestOutputHelper output) => this.output = output;

    private static List<RouteLeg> MaximumRoute(bool native)
    {
        var legs = new List<RouteLeg>
        {
            Leg(0, 5, 1, TravelMode.Walk, ""), Leg(5, 10, 1, TravelMode.Walk, "")
        };
        for (int i = 0; i < CarLegs; i++)
            legs.Add(Leg(10 + 100.0 * i / CarLegs, 10 + 100.0 * (i + 1) / CarLegs, CarMinutes / CarLegs, TravelMode.Car,
                native ? "native:road:v1:timing-native:" + i + ":" + (i + 1) : ""));
        legs.Add(Leg(110, 115, 1, TravelMode.Walk, "")); legs.Add(Leg(115, 120, 1, TravelMode.Walk, ""));
        return legs;
    }

    private static RouteLeg Leg(double from, double to, double minutes, TravelMode mode, string service) => new()
    { From = new Position(from, 0, 0), To = new Position(to, 0, 0), Minutes = minutes, Mode = mode, ServiceId = service };

    private static CityState City(int count)
    {
        var state = Population.Create(Enumerable.Range(0, count).Select(i => new CitizenIdentity
        { Id = "max" + i, Name = "Maximum " + i, Age = 30, AppearanceSeed = i + 1 }), new[]
        {
            new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = count, Position = new Position(0, 0, 0) },
            new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = count, Position = new Position(120, 0, 0) }
        }, "maximum-route", "0.40.4", 7, Departure);
        state.TravelTimingVersion = 1; state.TravelClockRate = 20;
        foreach (var c in state.Citizens)
        {
            c.PlaceId = "home"; c.DayPlaceId = "work"; c.Phase = CitizenPhase.Travelling; c.Activity = Activity.Home;
            c.DestinationId = "work"; c.DestinationActivity = Activity.Work; c.ActivityUntilMinute = 4000;
            c.Preferences.OwnsCar = true;
            c.Car = new CarState { Id = c.Identity.Id + ":car", ParkedPosition = new Position(10, 0, 0) };
        }
        return state;
    }

    private static CityState Unowned(double elapsed, int version, double rate)
    {
        var state = City(1); var c = state.Citizens[0];
        state.Minute = Departure + elapsed; state.TravelTimingVersion = version; state.TravelClockRate = rate;
        c.DepartureMinute = Departure;
        c.Journey = new Route { Legs = MaximumRoute(true) };
        c.NextEventMinute = Departure + c.Journey.Minutes;
        return state;
    }

    private static double EdgeElapsed(int edge, double fraction) => 2 + (edge + fraction) * CarMinutes / CarLegs;

    private static (Position Person, Position? Car) Pose(CityState state, CitizenState citizen)
    {
        var city = new CitySimulator(state, new NoRoutes());
        return (city.PositionOf(citizen), city.CarPositionOf(citizen));
    }

    private static void AssertSamePose((Position Person, Position? Car) expected, (Position Person, Position? Car) actual)
    {
        Assert.InRange(Position.Distance(expected.Person, actual.Person), 0, 1e-9);
        Assert.Equal(expected.Car.HasValue, actual.Car.HasValue);
        if (expected.Car.HasValue) Assert.InRange(Position.Distance(expected.Car.Value, actual.Car!.Value), 0, 1e-9);
    }

    private static void AssertValid(CityState state)
    {
        foreach (var c in state.Citizens.Where(c => c.Phase == CitizenPhase.Travelling))
        {
            Assert.InRange(c.Journey!.Legs.Count, 1, 512);
            CitySimulator.ValidateRoute(c.Journey, state.Places[0].Position, state.Places[1].Position);
            CitySimulator.ValidateCarRoute(c.Journey, c);
            Assert.Equal(c.DepartureMinute + c.Journey.Minutes, c.NextEventMinute, 9);
        }
        if (state.Traffic != null)
        {
            TrafficKernel.ValidateState(state.Traffic);
            var citizens = state.Citizens.ToDictionary(c => c.Identity.Id);
            var kernel = TrafficKernel.Restore(state.Traffic);
            foreach (var vehicle in state.Traffic.Vehicles)
                Assert.InRange(Math.Abs(kernel.GetPermission(vehicle.VehicleId).NominalMinutes - (state.Minute - citizens[vehicle.VehicleId].DepartureMinute)), 0, 1e-7);
        }
        Assert.Equal(SaveReadStatus.Loaded, SaveWire.Read(SaveCodec.Write(state)).Status);
    }

    [Theory]
    [InlineData(1, 20, 1, 37)] [InlineData(1, 1, 20, 37)] [InlineData(0, 1, 20, 37)] [InlineData(0, 1, 1, 37)]
    [InlineData(1, 20, 1, 0)] [InlineData(1, 1, 20, 507)] [InlineData(1, 3, 1, 250)]
    public void PartialNativeEdgeOnAMaximumRouteRetimesWithoutAddingALeg(int version, double from, double to, int edge)
    {
        double elapsed = EdgeElapsed(edge, 0.37);
        var state = Unowned(elapsed, version, from); var c = state.Citizens[0];
        var before = Pose(state, c);
        double oldRemainingCar = (CarLegs - edge - 0.37) * CarMinutes / CarLegs;
        double expectedArrival = version == 0
            ? state.Minute + oldRemainingCar * 60 * to + 10 / 2.0 * to
            : state.Minute + (c.Journey!.Minutes - elapsed) * to / from;

        Assert.True(TravelTiming.Apply(state, 2, to, out int keptPace));

        Assert.Equal(0, keptPace);
        Assert.Equal(512, c.Journey!.Legs.Count);
        Assert.Equal(before, Pose(state, c));
        Assert.Equal(expectedArrival, c.NextEventMinute, 9);
        var tail = c.Journey.Legs.Single(l => l.From.Equals(before.Person) && l.Mode == TravelMode.Car && l.From.X < l.To.X);
        Assert.Equal("native:road:fragment1:timing-native:" + edge + ":" + (edge + 1), tail.ServiceId);
        Assert.Equal(0.63 * CarMinutes / CarLegs * (version == 0 ? 60 * to : to / from), tail.Minutes, 9);
        Assert.Equal(to, state.TravelClockRate);
        AssertValid(state);
    }

    [Fact]
    public void RepeatedClockChangesOnTheSameEdgeKeepTheRouteAtItsMaximumAndThePoseFixed()
    {
        var state = Unowned(EdgeElapsed(120, 0.5), 1, 20); var c = state.Citizens[0];
        foreach (double rate in new[] { 1.0, 3, 20, 1, 20, 3, 1 })
        {
            var before = Pose(state, c);
            Assert.True(TravelTiming.Apply(state, 2, rate, out int keptPace));
            Assert.Equal(0, keptPace);
            Assert.Equal(before, Pose(state, c));
            AssertValid(state);
            state.Minute += 0.001;
        }
    }

    [Fact]
    public void MaximumRouteWithNoMergeableCompletedLegsKeepsOnlyTheCurrentEdgeAtItsOldPace()
    {
        var state = Unowned(EdgeElapsed(0, 0.37), 1, 20); var c = state.Citizens[0];
        c.Journey!.Legs.RemoveAt(0); c.Journey.Legs[0] = Leg(0, 10, 2, TravelMode.Walk, "");
        c.Journey.Legs.Add(Leg(119, 120, 1, TravelMode.Walk, ""));
        c.Journey.Legs[c.Journey.Legs.Count - 2] = Leg(115, 119, 1, TravelMode.Walk, "");
        c.NextEventMinute = Departure + c.Journey.Minutes;
        var before = Pose(state, c); double oldEdge = c.Journey.Legs[1].Minutes;

        Assert.True(TravelTiming.Apply(state, 2, 1, out int keptPace));

        Assert.Equal(1, keptPace);
        Assert.Equal(512, c.Journey.Legs.Count);
        Assert.Equal(before, Pose(state, c));
        Assert.Equal(oldEdge, c.Journey.Legs[1].Minutes);
        Assert.Equal(CarMinutes / CarLegs / 20, c.Journey.Legs[2].Minutes, 12);
        AssertValid(state);
    }

    private static CityState Owned(double progress, bool firstEdge)
    {
        var state = City(2);
        var kernel = new TrafficKernel(new[] { new TrafficLinkDefinition { Id = 0, Capacity = 4, FreeFlowMinutes = CarMinutes, HeadwayMinutes = 0.001 } }, 1442);
        foreach (var c in state.Citizens)
        {
            c.Journey = new Route { Legs = MaximumRoute(true) };
            if (firstEdge)
            {
                c.Journey.Legs.RemoveAt(0); c.Journey.Legs[0] = Leg(0, 10, 2, TravelMode.Walk, "");
                c.Journey.Legs.Add(Leg(119, 120, 1, TravelMode.Walk, ""));
                c.Journey.Legs[c.Journey.Legs.Count - 2] = Leg(115, 119, 1, TravelMode.Walk, "");
            }
            kernel.Enqueue(c.Identity.Id, new[] { new TrafficRouteLeg { LinkId = 0, StartNominalMinutes = 2, EndNominalMinutes = 12, FreeFlowMinutes = CarMinutes } });
        }
        kernel.AdvanceTo(1442 + progress, 100);
        state.Minute = kernel.Clock;
        foreach (var c in state.Citizens)
        {
            c.DepartureMinute = state.Minute - kernel.GetPermission(c.Identity.Id).NominalMinutes;
            c.NextEventMinute = c.DepartureMinute + c.Journey!.Minutes;
        }
        state.Traffic = kernel.Capture(); state.TrafficTopologyId = new string('a', 64);
        return state;
    }

    [Theory]
    [InlineData(20, 1)] [InlineData(1, 20)] [InlineData(20, 3)]
    public void TrafficOwnedPartialEdgeOnAMaximumRouteKeepsTrafficAndJourneyInStep(double from, double to)
    {
        var state = Owned(5.0 + 0.37 * CarMinutes / CarLegs, false); state.TravelClockRate = from;
        var poses = state.Citizens.Select(c => Pose(state, c)).ToArray();
        var kernel = TrafficKernel.Restore(state.Traffic!);
        var remaining = state.Traffic!.Vehicles.ToDictionary(v => v.VehicleId, v => v.Route[0].EndNominalMinutes - kernel.GetPermission(v.VehicleId).NominalMinutes);

        Assert.True(TravelTiming.Apply(state, 2, to, out int keptPace));

        Assert.Equal(0, keptPace);
        Assert.Equal(2, state.Traffic!.Vehicles.Count);
        for (int i = 0; i < 2; i++)
        {
            Assert.Equal(512, state.Citizens[i].Journey!.Legs.Count);
            AssertSamePose(poses[i], Pose(state, state.Citizens[i]));
        }
        foreach (var vehicle in state.Traffic.Vehicles.Where(v => v.RouteIndex == 0))
            Assert.Equal(remaining[vehicle.VehicleId] * to / from, vehicle.Route[0].FreeFlowMinutes, 9);
        AssertValid(state);
    }

    [Fact]
    public void TrafficOwnedFirstEdgeWithNoMergeableLegsLeavesTrafficInsteadOfStoppingTheCity()
    {
        var state = Owned(0.37 * CarMinutes / CarLegs, true);
        var c = state.Citizens[0]; var before = Pose(state, c);
        Assert.Equal(0, state.Traffic!.Vehicles.Single(v => v.VehicleId == c.Identity.Id).RouteIndex);

        Assert.True(TravelTiming.Apply(state, 2, 1, out int keptPace));

        Assert.True(keptPace >= 1);
        Assert.DoesNotContain(state.Traffic!.Vehicles, v => v.VehicleId == c.Identity.Id);
        AssertSamePose(before, Pose(state, c));
        Assert.Equal(512, c.Journey!.Legs.Count);
        AssertValid(state);
    }

    [Fact]
    public void RigScaleMixedCityRetimesEveryCommittedJourneyThroughRepeatedSpeedChanges()
    {
        const int residents = 1000;
        var state = City(residents);
        var links = Enumerable.Range(0, 4).Select(i => new TrafficLinkDefinition
        { Id = i, Capacity = 1000, FreeFlowMinutes = CarMinutes / 4, HeadwayMinutes = 0.0001 }).ToArray();
        var kernel = new TrafficKernel(links, Departure);
        const double now = Departure + 9.5;
        var owned = new List<(CitizenState Citizen, double Enter)>();
        for (int i = 0; i < residents; i++)
        {
            var c = state.Citizens[i];
            double spread = (i * 7919 % residents) / (double)residents;
            switch (i % 4)
            {
                case 0:
                    c.Preferences.OwnsCar = false; c.Car = null;
                    c.Journey = new Route { Legs = new() { Leg(0, 120, 12, TravelMode.Walk, "") } };
                    c.DepartureMinute = now - 11.9 * spread - 0.05;
                    break;
                case 1: case 2:
                    c.Journey = new Route { Legs = MaximumRoute(i % 4 == 1) };
                    c.DepartureMinute = now - (12.9 * spread + 1.05);
                    break;
                default:
                    c.Journey = new Route { Legs = MaximumRoute(false) };
                    owned.Add((c, Departure + 9 * spread));
                    break;
            }
        }
        var route = Enumerable.Range(0, 4).Select(k => new TrafficRouteLeg
        { LinkId = k, StartNominalMinutes = 2 + k * CarMinutes / 4, EndNominalMinutes = 2 + (k + 1) * CarMinutes / 4, FreeFlowMinutes = CarMinutes / 4 }).ToArray();
        foreach (var (citizen, enter) in owned.OrderBy(o => o.Enter))
        {
            kernel.AdvanceTo(enter, 100000);
            kernel.Enqueue(citizen.Identity.Id, route.Select(r => new TrafficRouteLeg
            { LinkId = r.LinkId, StartNominalMinutes = r.StartNominalMinutes, EndNominalMinutes = r.EndNominalMinutes, FreeFlowMinutes = r.FreeFlowMinutes }).ToArray());
        }
        kernel.AdvanceTo(now, 1000000);
        state.Minute = now;
        foreach (var (citizen, _) in owned)
            citizen.DepartureMinute = now - kernel.GetPermission(citizen.Identity.Id).NominalMinutes;
        foreach (var c in state.Citizens) c.NextEventMinute = c.DepartureMinute + c.Journey!.Minutes;
        state.Traffic = kernel.Capture(); state.TrafficTopologyId = new string('a', 64);
        int ownedCount = state.Traffic.Vehicles.Count;
        Assert.Equal(residents / 4, ownedCount);

        var timings = new List<double>();
        foreach (double rate in new[] { 1.0, 3, 20, 1, 20, 3, 1, 20 })
        {
            var poses = state.Citizens.Select(c => c.Journey!.PositionAt(state.Minute - c.DepartureMinute)).ToArray();
            long started = Stopwatch.GetTimestamp();
            Assert.True(TravelTiming.Apply(state, 2, rate, out int keptPace));
            timings.Add((Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency);
            Assert.Equal(0, keptPace);
            Assert.Equal(ownedCount, state.Traffic!.Vehicles.Count);
            for (int i = 0; i < residents; i++)
            {
                var c = state.Citizens[i];
                var after = c.Journey!.PositionAt(state.Minute - c.DepartureMinute);
                Assert.InRange(Position.Distance(poses[i], after), 0, 1e-9);
            }
            AssertValid(state);
        }
        output.WriteLine("retime ms: " + string.Join(", ", timings.Select(t => t.ToString("0.00"))));
        PerfGate.Under(timings.Skip(1).Max(), 250, "rig-scale clock retime");
    }
    private sealed class LongRoad : IRoutePlanner, ITrafficRoutePlanner
    {
        public const string Topology = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
        public TrafficNetwork? TrafficNetwork { get; }
        public LongRoad(double rate, int edges = CarLegs) => TrafficNetwork = new TrafficNetwork(Topology, Enumerable.Range(0, edges + 1).Select(i => new TrafficNetworkNode
        {
            Id = i, Position = new Position(i, 0, 0), KilometresPerHour = 36, AllowsCar = true, Boundary = i % 127 == 0,
            Successors = i < edges ? new[] { i + 1 } : Array.Empty<int>()
        }).ToArray(), rate);
        public void SetTrafficForecast(Func<int, double, TrafficLinkForecast>? forecast) { }
        public bool IsTrafficLinkClosed(int id) => false;
        public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences) => null;
    }

    [Fact]
    public void TrafficOwnedMaximumNativeRoutesSurviveSpeedChangesAndRebindToTheRetimedNetwork()
    {
        const int residents = 6;
        var state = Population.Create(Enumerable.Range(0, residents).Select(i => new CitizenIdentity
        { Id = "road" + i, Name = "Road " + i, Age = 30, AppearanceSeed = i + 1, Persona = Persona.Commuter }), new[]
        {
            new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = residents, Position = new Position(-4, 0, 0) },
            new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = residents, Position = new Position(CarLegs + 4, 0, 0) }
        }, "long-road", "0.40.4", 7, Departure);
        for (int i = 0; i < residents; i++)
        {
            var c = state.Citizens[i];
            c.PlaceId = c.HomeId = "home"; c.DayPlaceId = "work"; c.Phase = CitizenPhase.Travelling;
            c.DestinationId = "work"; c.DestinationActivity = Activity.Work; c.ActivityUntilMinute = 4000;
            c.DepartureMinute = Departure - i * 0.7;
            c.Preferences.OwnsCar = true;
            c.Car = new CarState { Id = c.Identity.Id + ":car", ParkedPosition = new Position(0, 0, 0) };
            var legs = new List<RouteLeg> { Leg(-4, -2, 1, TravelMode.Walk, ""), Leg(-2, 0, 1, TravelMode.Walk, "") };
            for (int e = 0; e < CarLegs; e++) legs.Add(Leg(e, e + 1, 0.1, TravelMode.Car, "native:road:v1:" + LongRoad.Topology + ":" + e + ":" + (e + 1)));
            legs.Add(Leg(CarLegs, CarLegs + 2, 1, TravelMode.Walk, "")); legs.Add(Leg(CarLegs + 2, CarLegs + 4, 1, TravelMode.Walk, ""));
            c.Journey = new Route { Legs = legs }; c.NextEventMinute = c.DepartureMinute + c.Journey.Minutes;
        }
        double rate = state.TravelClockRate;
        var engine = new CitySimulator(state, new LongRoad(rate));
        engine.AdvanceTo(Departure + 2 + 20.037);
        Assert.True(engine.Traffic!.VehicleCount >= 2);
        foreach (double next in new[] { 20.0, 1, 3, 20, 1 })
        {
            engine.State.PrepareForSave();
            var poses = engine.State.Citizens.Select(c => engine.PositionOf(c)).ToArray();
            int owned = engine.State.Traffic?.Vehicles.Count ?? 0;
            Assert.True(TravelTiming.Apply(engine.State, 2, next, out int keptPace));
            Assert.Equal(0, keptPace);
            Assert.Equal(owned, engine.State.Traffic?.Vehicles.Count ?? 0);
            engine = new CitySimulator(engine.State, new LongRoad(next));
            for (int i = 0; i < residents; i++)
            {
                Assert.InRange(engine.State.Citizens[i].Journey?.Legs.Count ?? 0, 0, 512);
                Assert.InRange(Position.Distance(poses[i], engine.PositionOf(engine.State.Citizens[i])), 0, 1e-9);
            }
            engine.AdvanceTo(engine.State.Minute + 0.0137 * next);
        }
        engine.AdvanceTo(engine.State.Minute + 2000);
        Assert.All(engine.State.Citizens, c => Assert.Equal(1, c.CompletedJourneys));
        Assert.Equal(SaveReadStatus.Loaded, SaveWire.Read(SaveCodec.Write(engine.State)).Status);
    }
    [Theory]
    [InlineData(1, 20, 1, 510, 1, 0.37)] [InlineData(20, 1, 1, 510, 1, 0.37)]
    [InlineData(1, 20, 1, 509, 2, 127.37)] [InlineData(20, 1, 1, 509, 2, 127.37)]
    public void SimulatedCarWithNoMergeableLegsStaysOutOfTrafficUntilItsNextEdgeAndCompletes(double from, double to, int approach, int edges, int departure, double edge)
    {
        var state = Population.Create(new[] { new CitizenIdentity { Id = "fallback", Name = "Fallback", Age = 30, AppearanceSeed = 1, Persona = Persona.Commuter } }, new[]
        {
            new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 1, Position = new Position(-4, 0, 0) },
            new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = 1, Position = new Position(edges + 4, 0, 0) }
        }, "fallback-road", "0.40.4", 7, Departure);
        state.TravelClockRate = from;
        var c = state.Citizens[0];
        c.PlaceId = c.HomeId = "home"; c.DayPlaceId = "work"; c.Phase = CitizenPhase.Travelling;
        c.DestinationId = "work"; c.DestinationActivity = Activity.Work; c.ActivityUntilMinute = 9000;
        c.DepartureMinute = Departure; c.Preferences.OwnsCar = true;
        c.Car = new CarState { Id = c.Identity.Id + ":car", ParkedPosition = new Position(0, 0, 0) };
        var legs = new List<RouteLeg>();
        for (int i = 0; i < approach; i++) legs.Add(Leg(-4 + 4.0 * i / approach, -4 + 4.0 * (i + 1) / approach, 2.0 / approach, TravelMode.Walk, ""));
        for (int e = 0; e < edges; e++) legs.Add(Leg(e, e + 1, 0.1 * from, TravelMode.Car, "native:road:v1:" + LongRoad.Topology + ":" + e + ":" + (e + 1)));
        for (int i = 0; i < departure; i++) legs.Add(Leg(edges + 4.0 * i / departure, edges + 4.0 * (i + 1) / departure, 2.0 / departure, TravelMode.Walk, ""));
        Assert.Equal(512, legs.Count);
        c.Journey = new Route { Legs = legs }; c.NextEventMinute = c.DepartureMinute + c.Journey.Minutes;
        var engine = new CitySimulator(state, new LongRoad(from, edges));
        engine.AdvanceTo(Departure + 2 + edge * 0.1 * from);
        Assert.Equal(1, engine.Traffic!.VehicleCount);
        engine.State.PrepareForSave();
        var before = engine.SampleJourney(c);

        Assert.True(TravelTiming.Apply(engine.State, 2, to, out int degraded));

        Assert.Equal(1, degraded);
        Assert.Empty(engine.State.Traffic!.Vehicles);
        Assert.Equal(512, c.Journey!.Legs.Count);
        Assert.Equal(c.DepartureMinute + c.Journey.Minutes, c.NextEventMinute, 9);
        var reloaded = SaveWire.Read(SaveCodec.Write(engine.State));
        Assert.Equal(SaveReadStatus.Loaded, reloaded.Status);
        foreach (var city in new[] { engine.State, reloaded.State! })
        {
            var citizen = city.Citizens[0];
            var rebound = new CitySimulator(city, new LongRoad(to, edges));
            Assert.InRange(Position.Distance(before.Position, rebound.SampleJourney(citizen).Position), 0, 1e-9);
            rebound.AdvanceTo(city.Minute + 0.001);
            rebound.State.PrepareForSave();
            Assert.Empty(rebound.State.Traffic!.Vehicles);
            rebound = new CitySimulator(city, new LongRoad(to, edges));
            double nextEdge = city.Minute + (1 - (edge - Math.Floor(edge))) * 0.1 * from + 0.05 * to;
            rebound.AdvanceTo(nextEdge);
            rebound.State.PrepareForSave();
            Assert.Single(rebound.State.Traffic!.Vehicles);
            rebound = new CitySimulator(SaveWire.Read(SaveCodec.Write(city)).State!, new LongRoad(to, edges));
            rebound.AdvanceTo(nextEdge + 2000 * to);
            Assert.Equal(1, rebound.State.Citizens[0].CompletedJourneys);
        }
    }
}
