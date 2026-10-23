using System;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class TravelTimingCollapsedNativeTailTests
{
    private const string Native = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static Position P(double x) => new(x, 0, 0);
    private static string Tag(int from, int to) => $"native:road:v1:{Native}:{from}:{to}";
    private sealed class Planner : IRoutePlanner, ITrafficRoutePlanner
    {
        public Planner(double rate = 1, bool sectionBoundary = false, double rise = 0)
        {
            TrafficNetwork = new TrafficNetwork(Native, Enumerable.Range(0, 3).Select(i => new TrafficNetworkNode
            { Id = i, Position = new Position(220 + i * 10, i * rise, 0), AllowsCar = true, KilometresPerHour = 36, Boundary = sectionBoundary && i == 1,
                Successors = i == 2 ? Array.Empty<int>() : new[] { i + 1 } }).ToArray(), rate);
        }
        public TrafficNetwork TrafficNetwork { get; }
        public Route? Plan(Place a, Place b, double minute, TravelPreferences preferences) => null;
        public void SetTrafficForecast(Func<int, double, TrafficLinkForecast>? forecast) { }
        public bool IsTrafficLinkClosed(int linkId) => false;
    }
    private static (CityState State, CitizenState Citizen, double Raw) Captured(bool terminal = false, double gap = 0, bool sectionBoundary = false, bool sloped = false, int ulps = 1)
    {
        Position Point(int i) => new Position(220 + i * 10, sloped ? i * .5 : 0, 0);
        double minutes = Position.Distance(Point(0), Point(1)) / 10;
        var route = new Route { Legs = new() { new RouteLeg { From = Point(0), To = Point(1), Minutes = minutes,
            Mode = TravelMode.Car, ServiceId = Tag(0, 1) } } };
        if (!terminal) route.Legs.Add(new RouteLeg { From = Point(1), To = Point(2), Minutes = minutes,
            Mode = TravelMode.Car, ServiceId = Tag(1, 2) });
        var state = Population.Create(new[] { new CitizenIdentity { Id = "car", Name = "Car", Age = 30, Persona = Persona.Commuter } }, new[]
        { new Place { Id = "a", Name = "Home", Kind = PlaceKind.Home, Capacity = 1, Position = route.Legs[0].From },
          new Place { Id = "b", Name = "Work", Kind = PlaceKind.Work, Capacity = 1, Position = route.Legs[^1].To } }, "tail-retime", "0.40.4", 7, 0);
        var citizen = state.Citizens[0]; citizen.PlaceId = citizen.HomeId = "a"; citizen.DestinationId = citizen.DayPlaceId = "b";
        citizen.Phase = CitizenPhase.Travelling; citizen.DestinationActivity = Activity.Work; citizen.ActivityUntilMinute = 500;
        citizen.Journey = route; citizen.DepartureMinute = 0; citizen.NextEventMinute = route.Minutes;
        citizen.Preferences.OwnsCar = true; citizen.Car = new CarState { Id = "car:car", ParkedPosition = route.Legs[0].From };
        state.TravelTimingVersion = 1; state.TravelClockRate = 1;
        var simulation = new CitySimulator(state, new Planner(sectionBoundary: sectionBoundary, rise: sloped ? .5 : 0)); simulation.AdvanceTo(0);
        Assert.True(simulation.TryAcquirePhysicalJourney("car", out var lease));
        double raw = minutes - gap;
        if (gap == 0) for (int i = 0; i < ulps; i++) raw = Math.BitDecrement(raw);
        simulation.AdvanceTo(raw); Assert.True(simulation.ObservePhysicalJourney(lease!, raw));
        Assert.True(simulation.ReleasePhysicalJourney(lease!)); state.PrepareForSave();
        Assert.Equal(raw, state.Traffic!.Vehicles.Single().NominalMinutes);
        return (state, citizen, raw);
    }

    private sealed class ShortContinuationPlanner : IRoutePlanner, ITrafficRoutePlanner
    {
        public ShortContinuationPlanner(double continuation, double rate = 1)
        {
            double[] x = { 1000, 1060, 1060 + continuation };
            TrafficNetwork = new TrafficNetwork(Native, Enumerable.Range(0, 3).Select(i => new TrafficNetworkNode
            { Id = i, Position = P(x[i]), AllowsCar = true, KilometresPerHour = 36,
                Successors = i == 2 ? Array.Empty<int>() : new[] { i + 1 } }).ToArray(), rate);
        }
        public TrafficNetwork TrafficNetwork { get; }
        public Route? Plan(Place a, Place b, double minute, TravelPreferences preferences) => null;
        public void SetTrafficForecast(Func<int, double, TrafficLinkForecast>? forecast) { }
        public bool IsTrafficLinkClosed(int linkId) => false;
    }
    private static (CityState State, CitizenState Citizen, double Raw) CapturedShortContinuation(double continuation)
    {
        var (initial, _, _) = Captured();
        var state = SaveCodec.Read(SaveCodec.Write(initial)).State!; state.Traffic = null; state.TrafficTopologyId = ""; state.Minute = 0;
        double[] x = { 1000, 1060, 1060 + continuation };
        var route = new Route { Legs = Enumerable.Range(0, 2).Select(i => new RouteLeg
        { From = P(x[i]), To = P(x[i + 1]), Mode = TravelMode.Car, Minutes = Math.Abs(x[i + 1] - x[i]) / 10,
            ServiceId = Tag(i, i + 1) }).ToList() };
        var citizen = state.Citizens[0]; citizen.Journey = route; citizen.DepartureMinute = 0; citizen.NextEventMinute = route.Minutes;
        citizen.Car!.ParkedPosition = route.Legs[0].From;
        state.Places.Single(place => place.Id == "a").Position = route.Legs[0].From;
        state.Places.Single(place => place.Id == "b").Position = route.Legs[^1].To;
        var planner = new ShortContinuationPlanner(continuation); var simulation = new CitySimulator(state, planner); simulation.AdvanceTo(0);
        Assert.True(simulation.TryAcquirePhysicalJourney("car", out var lease));
        double raw = 6 - 4 * 2.2204460492503131e-16 * 6;
        simulation.AdvanceTo(raw); Assert.True(simulation.ObservePhysicalJourney(lease!, raw));
        Assert.Equal(raw, lease!.ObservedMinutes); Assert.True(simulation.ReleasePhysicalJourney(lease)); state.PrepareForSave();
        planner.TrafficNetwork.ValidateRemainingMapping(route, state.Traffic!.Vehicles.Single().Route, raw);
        Assert.Equal(P(1060), route.PositionAt(raw));
        return (state, citizen, raw);
    }

    [Fact]
    public void CollapsedInteriorNativeTailPreservesObservedPrefixAndRebindsAfterRealClockRetime()
    {
        var (state, citizen, raw) = Captured();
        Position before = citizen.Journey!.PositionAt(raw); Assert.Equal(P(230), before);
        var original = state.Traffic!.Vehicles.Single(); long order = original.Order;
        var queued = state.Traffic.Links.Single().Vehicles.ToArray();
        Assert.True(TravelTiming.Apply(state, 2, 2));
        Assert.Equal(2, citizen.Journey!.Legs.Count);
        Assert.Equal(raw, citizen.Journey.Legs[0].Minutes);
        Assert.Equal(P(220), citizen.Journey.Legs[0].From); Assert.Equal(before, citizen.Journey.Legs[0].To);
        Assert.All(citizen.Journey.Legs, leg => Assert.True(Position.Distance(leg.From, leg.To) > 0));
        Assert.Equal(before, citizen.Journey.PositionAt(raw));
        var saved = state.Traffic!.Vehicles.Single();
        Assert.Equal(raw, saved.NominalMinutes); Assert.Equal(order, saved.Order);
        Assert.Equal(queued, state.Traffic.Links.Single().Vehicles); Assert.False(saved.Completed);
        var planner = new Planner(2);
        Assert.True(planner.TrafficNetwork.TryMapRemaining(citizen.Journey, raw, out _));
        planner.TrafficNetwork.ValidateRemainingMapping(citizen.Journey, saved.Route, raw);
        var restored = new CitySimulator(state, planner);
        Assert.Equal(raw, restored.Traffic!.GetPermission("car").NominalMinutes);
        Assert.Equal(1, restored.Traffic.VehicleCount); Assert.Equal(0, citizen.CompletedJourneys);
    }

    [Fact]
    public void CollapsedInteriorTailSurvivesSaveCodecRoundtripWithTheSamePoseAndOwnership()
    {
        var (state, _, raw) = Captured(); Assert.True(TravelTiming.Apply(state, 2, 2));
        var decoded = SaveCodec.Read(SaveCodec.Write(state)); Assert.Equal(SaveReadStatus.Loaded, decoded.Status);
        var copied = decoded.State!; var simulation = new CitySimulator(copied, new Planner(2));
        Assert.Equal(raw, simulation.Traffic!.GetPermission("car").NominalMinutes);
        Assert.Equal(P(230), copied.Citizens[0].Journey!.PositionAt(raw));
        Assert.Equal(2, copied.Citizens[0].Journey!.Legs.Count);
        Assert.Equal(1, simulation.Traffic.VehicleCount); Assert.Equal(0, copied.Citizens[0].CompletedJourneys);
    }

    [Fact]
    public void MateriallyTinyRealTailKeepsItsGeometryDurationAndNativeProvenance()
    {
        var (state, citizen, raw) = Captured(gap: 1e-8);
        Position before = citizen.Journey!.PositionAt(raw); Assert.True(before.X < 230);
        Assert.True(TravelTiming.Apply(state, 2, 2));
        Assert.Equal(3, citizen.Journey!.Legs.Count);
        var tail = citizen.Journey.Legs[1]; Assert.Equal(before, tail.From); Assert.Equal(P(230), tail.To);
        Assert.True(tail.Minutes > 1e-8); Assert.StartsWith("native:road:fragment1:", tail.ServiceId);
        Assert.True(new Planner(2).TrafficNetwork.TryMapRemaining(citizen.Journey, raw, out _));
        Assert.Equal(raw, state.Traffic!.Vehicles.Single().NominalMinutes);
        _ = new CitySimulator(state, new Planner(2));
    }

    [Fact]
    public void UnreportedTerminalResidualRefusesAtomicallyAndOriginalNetworkStillBinds()
    {
        var (state, citizen, raw) = Captured(terminal: true);
        string before = SaveCodec.Write(state); Route route = citizen.Journey!; var traffic = state.Traffic;
        Assert.Throws<ArgumentException>(() => TravelTiming.Apply(state, 2, 2));
        Assert.Same(route, citizen.Journey); Assert.Same(traffic, state.Traffic);
        Assert.Equal(before, SaveCodec.Write(state));
        Assert.Equal(1, state.TravelClockRate); Assert.Equal(raw, state.Minute);
        var rebound = new CitySimulator(state, new Planner());
        Assert.Equal(raw, rebound.Traffic!.GetPermission("car").NominalMinutes);
        Assert.False(rebound.Traffic.GetPermission("car").IsCompleted);
    }
    [Fact]
    public void UnreportedInternalSectionResidualRefusesAtomicallyAndOriginalNetworkStillBinds()
    {
        var (state, citizen, raw) = Captured(sectionBoundary: true);
        string before = SaveCodec.Write(state); Route route = citizen.Journey!; var traffic = state.Traffic;
        Assert.Equal(2, traffic!.Definitions.Count); Assert.Equal(0, traffic.Vehicles.Single().RouteIndex);
        Assert.Throws<ArgumentException>(() => TravelTiming.Apply(state, 2, 2));
        Assert.Same(route, citizen.Journey); Assert.Same(traffic, state.Traffic);
        Assert.Equal(before, SaveCodec.Write(state));
        Assert.Equal(1, state.TravelClockRate); Assert.Equal(raw, state.Minute);
        var rebound = new CitySimulator(state, new Planner(sectionBoundary: true));
        var permission = rebound.Traffic!.GetPermission("car");
        Assert.Equal(raw, permission.NominalMinutes); Assert.Equal(0, permission.RouteIndex);
        Assert.False(permission.IsCompleted); Assert.Equal(1, rebound.Traffic.VehicleCount);
        Assert.Equal(new[] { "car" }, rebound.Traffic.Capture().Links.Single(link => link.LinkId == 0).Vehicles);
        Assert.Empty(rebound.Traffic.Capture().Links.Single(link => link.LinkId == 1).Vehicles);
    }

    [Theory]
    [InlineData(TravelMode.Walk)] [InlineData(TravelMode.Car)]
    public void FixedPolicyWaitTailIsRetainedEvenThoughItsGeometryIsStationary(TravelMode mode)
    {
        var (original, _, raw) = Captured();
        var state = SaveCodec.Read(SaveCodec.Write(original)).State!; state.Traffic = null; state.TrafficTopologyId = "";
        var citizen = state.Citizens[0]; citizen.Journey = new Route { Legs = new()
        { new RouteLeg { From = P(220), To = P(220), Minutes = 1, Mode = mode },
          new RouteLeg { From = P(220), To = P(240), Minutes = 1, Mode = mode } } };
        citizen.NextEventMinute = 2;
        Assert.True(TravelTiming.Apply(state, 2, 2));
        Assert.Equal(2, citizen.Journey!.Legs.Count);
        Assert.Equal(1, citizen.Journey.Legs[0].Minutes); // The whole fixed wait, including its unconsumed time, remains unchanged.
        Assert.Equal(P(220), citizen.Journey.Legs[0].From); Assert.Equal(P(220), citizen.Journey.Legs[0].To);
        Assert.Equal(2, citizen.Journey.Legs[1].Minutes);
    }

    [Fact]
    public void NonNativeWalkIsNotMistakenForAnInvalidNativeTail()
    {
        var (original, _, raw) = Captured();
        var state = SaveCodec.Read(SaveCodec.Write(original)).State!; state.Traffic = null; state.TrafficTopologyId = "";
        var citizen = state.Citizens[0];
        foreach (var leg in citizen.Journey!.Legs) { leg.Mode = TravelMode.Walk; leg.ServiceId = ""; }
        Assert.True(TravelTiming.Apply(state, 2, 2));
        Assert.Equal(2, citizen.Journey.Legs.Count);
        Assert.Equal(raw, citizen.Journey.Legs[0].Minutes); Assert.Equal(2, citizen.Journey.Legs[1].Minutes);
        Assert.Equal(P(230), citizen.Journey.Legs[0].To); Assert.Equal(P(230), citizen.Journey.Legs[1].From);
    }

    [Fact]
    public void MaterialUnconsumedNativeTimeCannotDisappearInsideAnUnrepresentableFragment()
    {
        var (original, _, _) = Captured();
        var state = SaveCodec.Read(SaveCodec.Write(original)).State!; state.Traffic = null; state.TrafficTopologyId = "";
        var citizen = state.Citizens[0]; state.Minute = .99; citizen.DepartureMinute = 0; citizen.NextEventMinute = 2;
        citizen.Journey = new Route { Legs = new()
        { new RouteLeg { From = P(Math.BitDecrement(230)), To = P(230), Minutes = 1, Mode = TravelMode.Car,
              ServiceId = $"native:road:fragment1:{Native}:0:1" },
          new RouteLeg { From = P(230), To = P(240), Minutes = 1, Mode = TravelMode.Car, ServiceId = Tag(1, 2) } } };
        citizen.Car!.ParkedPosition = citizen.Journey.Legs[0].From;
        state.Places.Single(place => place.Id == "a").Position = citizen.Journey.Legs[0].From;
        state.Places.Single(place => place.Id == "b").Position = P(240);
        Assert.Equal(citizen.Journey.Legs[0].To, Position.Lerp(citizen.Journey.Legs[0].From, citizen.Journey.Legs[0].To, .99));
        string before = SaveCodec.Write(state);
        Assert.Throws<ArgumentException>(() => TravelTiming.Apply(state, 2, 2));
        Assert.Equal(before, SaveCodec.Write(state));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SlopedNearEndSamplesPreserveTheCornerOrRefuseAnUnreportedSectionEndpoint(bool sectionBoundary)
    {
        for (int ulps = 1; ulps <= 32; ulps++)
        {
            var (state, citizen, raw) = Captured(sectionBoundary: sectionBoundary, sloped: true, ulps: ulps);
            Position pose = citizen.Journey!.PositionAt(raw); Assert.False(pose.Equals(citizen.Journey.Legs[0].To));
            if (sectionBoundary)
            {
                string before = SaveCodec.Write(state); var route = citizen.Journey; var traffic = state.Traffic;
                Assert.Throws<ArgumentException>(() => TravelTiming.Apply(state, 2, 2));
                Assert.Same(route, citizen.Journey); Assert.Same(traffic, state.Traffic); Assert.Equal(before, SaveCodec.Write(state));
                var rebound = new CitySimulator(state, new Planner(sectionBoundary: true, rise: .5));
                Assert.Equal(raw, rebound.Traffic!.GetPermission("car").NominalMinutes);
                Assert.Equal(pose, citizen.Journey!.PositionAt(raw));
            }
            else AssertRetimedCapture(state, raw, 2, new Planner(2, rise: .5), rawPositionUnchanged: false);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RepeatedRetimeUsesTheSameSampledNativeCornerWithoutGrantingASectionEndpoint(bool sectionBoundary)
    {
        var (state, citizen, _) = Captured(gap: .5, sectionBoundary: sectionBoundary, sloped: true);
        Assert.True(TravelTiming.Apply(state, 2, 2));
        var planner = new Planner(2, sectionBoundary, .5);
        var simulation = new CitySimulator(state, planner);
        Assert.True(simulation.TryAcquirePhysicalJourney("car", out var lease));
        double endpoint = citizen.Journey!.Legs[0].Minutes + citizen.Journey.Legs[1].Minutes;
        double raw = Math.BitDecrement(endpoint);
        simulation.AdvanceTo(raw); Assert.True(simulation.ObservePhysicalJourney(lease!, raw));
        Assert.True(simulation.ReleasePhysicalJourney(lease!)); state.PrepareForSave();
        Assert.StartsWith("native:road:fragment1:", citizen.Journey.Legs[1].ServiceId);
        planner.TrafficNetwork.ValidateRemainingMapping(citizen.Journey, state.Traffic!.Vehicles[0].Route, raw);
        Position pose = citizen.Journey.PositionAt(raw); Assert.False(pose.Equals(citizen.Journey.Legs[1].To));
        if (sectionBoundary)
        {
            string before = SaveCodec.Write(state); var route = citizen.Journey; var traffic = state.Traffic;
            Assert.Throws<ArgumentException>(() => TravelTiming.Apply(state, 2, 4));
            Assert.Same(route, citizen.Journey); Assert.Same(traffic, state.Traffic); Assert.Equal(before, SaveCodec.Write(state));
            var rebound = new CitySimulator(state, new Planner(2, true, .5));
            Assert.Equal(raw, rebound.Traffic!.GetPermission("car").NominalMinutes);
            Assert.Equal(pose, citizen.Journey.PositionAt(raw));
        }
        else AssertRetimedCapture(state, raw, 4, new Planner(4, rise: .5), rawPositionUnchanged: false);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SlopedRepresentableOneHundredNanometreTailStillRetimesAndBinds(bool sectionBoundary)
    {
        var (state, citizen, raw) = Captured(gap: 1e-8, sectionBoundary: sectionBoundary, sloped: true);
        Position pose = citizen.Journey!.PositionAt(raw);
        Assert.True(TravelTiming.Apply(state, 2, 2));
        Assert.Equal(3, citizen.Journey!.Legs.Count); Assert.Equal(pose, citizen.Journey.Legs[1].From);
        var rebound = new CitySimulator(state, new Planner(2, sectionBoundary, .5));
        Assert.Equal(raw, rebound.Traffic!.GetPermission("car").NominalMinutes);
        Assert.Equal(pose, citizen.Journey.PositionAt(raw));
    }

    [Theory]
    [InlineData(4, .000002)] [InlineData(3, .0000011)]
    public void CollapsedTailThatViolatesDetachedSpanTimingRefusesBeforePublishingAnything(double rate, double continuation)
    {
        var (state, citizen, raw) = CapturedShortContinuation(continuation);
        string before = SaveCodec.Write(state); var route = citizen.Journey; var traffic = state.Traffic;
        Assert.Throws<ArgumentException>(() => TravelTiming.Apply(state, 2, rate));
        Assert.Same(route, citizen.Journey); Assert.Same(traffic, state.Traffic); Assert.Equal(before, SaveCodec.Write(state));
        var rebound = new CitySimulator(state, new ShortContinuationPlanner(continuation));
        Assert.Equal(raw, rebound.Traffic!.GetPermission("car").NominalMinutes);
        Assert.Equal(P(1060), citizen.Journey!.PositionAt(raw)); Assert.Equal(0, citizen.CompletedJourneys);
    }

    [Theory]
    [InlineData(2, .000002)] [InlineData(4, .0001)] [InlineData(3, .0001)]
    public void CollapsedTailInsideTheExistingDetachedTimingAllowanceStillRetimesAndRebinds(double rate, double continuation)
    {
        var (state, citizen, raw) = CapturedShortContinuation(continuation);
        Assert.True(TravelTiming.Apply(state, 2, rate));
        Assert.Equal(2, citizen.Journey!.Legs.Count); Assert.Equal(raw, citizen.Journey.Legs[0].Minutes);
        var rebound = new CitySimulator(state, new ShortContinuationPlanner(continuation, rate));
        Assert.Equal(raw, rebound.Traffic!.GetPermission("car").NominalMinutes);
        Assert.Equal(P(1060), citizen.Journey.PositionAt(raw)); Assert.Equal(1, rebound.Traffic.VehicleCount);
    }

    [Fact]
    public void DetachedSpanTimingGateExemptsCompletedHistoricalSections()
    {
        var (original, _, _) = Captured(sectionBoundary: true);
        var state = SaveCodec.Read(SaveCodec.Write(original)).State!; var citizen = state.Citizens[0];
        var simulation = new CitySimulator(state, new Planner(sectionBoundary: true));
        Assert.True(simulation.TryAcquirePhysicalJourney("car", out var lease));
        simulation.AdvanceTo(1); Assert.True(simulation.ObservePhysicalJourney(lease!, 1)); simulation.AdvanceTo(1);
        simulation.AdvanceTo(1.5); Assert.True(simulation.ObservePhysicalJourney(lease!, 1.5));
        Assert.True(simulation.ReleasePhysicalJourney(lease!)); state.PrepareForSave();
        Assert.Equal(1, state.Traffic!.Vehicles.Single().RouteIndex);
        Assert.True(TravelTiming.Apply(state, 2, 2));
        var historical = state.Traffic!.Vehicles.Single().Route[0];
        Assert.Equal(2, historical.FreeFlowMinutes); Assert.Equal(1, historical.EndNominalMinutes - historical.StartNominalMinutes);
        var rebound = new CitySimulator(state, new Planner(2, sectionBoundary: true));
        Assert.Equal(1.5, rebound.Traffic!.GetPermission("car").NominalMinutes);
        Assert.Equal(1, rebound.Traffic.GetPermission("car").RouteIndex);
        Assert.Equal(P(235), citizen.Journey!.PositionAt(1.5));
    }

    [Fact]
    public void WaitingSourceFutureSpansCannotBypassDetachedTimingValidation()
    {
        var (captured, _, _) = CapturedShortContinuation(.0001);
        var state = SaveCodec.Read(SaveCodec.Write(captured)).State!; var citizen = state.Citizens[0];
        var network = new ShortContinuationPlanner(.0001).TrafficNetwork;
        Assert.True(network.TryMap(citizen.Journey!, out var mapped));
        mapped[0].FreeFlowMinutes += .01; // Structurally valid kernel data with a forged future traversal duration.
        var kernel = new TrafficKernel(network.Links, state.Minute); kernel.SetLinkClosed(0, true);
        kernel.Enqueue("car", mapped); state.Traffic = kernel.Capture();
        citizen.DepartureMinute = state.Minute; citizen.NextEventMinute = state.Minute + citizen.Journey!.Minutes;
        Assert.Equal(-1, state.Traffic.Vehicles.Single().RouteIndex);
        string before = SaveCodec.Write(state);
        Assert.Throws<ArgumentException>(() => TravelTiming.Apply(state, 2, 2));
        Assert.Equal(before, SaveCodec.Write(state));
        Assert.Equal(new[] { "car" }, state.Traffic!.Links.Single().WaitingSources);
    }

    private static (CityState State, double Nominal, double Elapsed) CapturedAtLargeClock(double nominal) =>
        CapturedAtClock(nominal, 100000010);

    private static (CityState State, double Nominal, double Elapsed) CapturedAtClock(double nominal, double endClock)
    {
        var (original, _, _) = CapturedShortContinuation(10);
        var state = SaveCodec.Read(SaveCodec.Write(original)).State!; var citizen = state.Citizens[0];
        double clock = endClock - 10;
        state.Traffic = null; state.TrafficTopologyId = ""; state.Minute = clock;
        citizen.DepartureMinute = clock; citizen.NextEventMinute = clock + 7;
        var simulation = new CitySimulator(state, new ShortContinuationPlanner(10)); simulation.AdvanceTo(clock);
        Assert.True(simulation.TryAcquirePhysicalJourney("car", out var lease));
        simulation.AdvanceTo(clock + 10); Assert.True(simulation.ObservePhysicalJourney(lease!, nominal));
        Assert.Equal(nominal, lease!.ObservedMinutes); Assert.True(simulation.ReleasePhysicalJourney(lease)); state.PrepareForSave();
        double elapsed = state.Minute - citizen.DepartureMinute;
        string json = SaveCodec.Write(state); var decoded = SaveCodec.Read(json); Assert.Equal(SaveReadStatus.Loaded, decoded.Status);
        _ = new CitySimulator(SaveCodec.Read(json).State!, new ShortContinuationPlanner(10));
        return (decoded.State!, nominal, elapsed);
    }

    [Theory]
    [InlineData(3.00000001, 2)] [InlineData(3.00000001, .5)] [InlineData(3.000000007, 100)]
    public void ActualLargeClockCaptureRetimesAtMeasuredTrafficWithoutAmplifyingClockRoundoff(double nominal, double rate)
    {
        var (state, observed, elapsed) = CapturedAtLargeClock(nominal); Assert.NotEqual(observed, elapsed);
        if (rate == 100) Assert.True(observed > elapsed); else Assert.True(observed < elapsed);
        AssertRetimedCapture(state, observed, rate, new ShortContinuationPlanner(10, rate));
    }

    [Theory]
    [InlineData(3.000000007, 2)] [InlineData(3, 100)] [InlineData(3, 2)]
    public void EqualOrBoundedlyLeadingActualTrafficStillRetimesAndRoundtrips(double nominal, double rate)
    {
        var (state, observed, elapsed) = CapturedAtLargeClock(nominal); Assert.True(observed >= elapsed);
        Assert.True(TravelTiming.Apply(state, 2, rate));
        string json = SaveCodec.Write(state); var decoded = SaveCodec.Read(json); Assert.Equal(SaveReadStatus.Loaded, decoded.Status);
        var rebound = new CitySimulator(decoded.State!, new ShortContinuationPlanner(10, rate));
        double actual = rebound.Traffic!.GetPermission("car").NominalMinutes;
        Assert.InRange(Math.Abs(actual - (state.Minute - state.Citizens[0].DepartureMinute)), 0, 1e-7);
    }

    [Fact]
    public void LegalClockLagAtACollapsedClockCornerRetainsTheActuallyObservedNativeTail()
    {
        var (original, _, raw) = CapturedShortContinuation(10);
        var state = SaveCodec.Read(SaveCodec.Write(original)).State!;
        // Exercise the accepted legacy clock/traffic agreement at this exact collapsed
        // endpoint. The real large-clock captures above establish the naturally arising case.
        var vehicle = state.Traffic!.Vehicles.Single();
        double nominal = vehicle.NominalMinutes - 1e-8;
        vehicle.NominalMinutes = nominal; vehicle.MotionStartFraction = nominal / 7;
        vehicle.MotionStartedAt = state.Minute; vehicle.ReadyAt = state.Minute + (1 - vehicle.MotionStartFraction) * 7;
        foreach (var item in state.Traffic.Events) item.At = vehicle.ReadyAt;
        state.Citizens[0].DepartureMinute = 0; state.Citizens[0].NextEventMinute = 7;
        TrafficKernel.ValidateState(state.Traffic);
        string before = SaveCodec.Write(state); Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(before).Status);
        _ = new CitySimulator(SaveCodec.Read(before).State!, new ShortContinuationPlanner(10));
        Assert.Equal(P(1060), state.Citizens[0].Journey!.PositionAt(raw));
        Assert.True(state.Citizens[0].Journey!.PositionAt(nominal).X < 1060);
        AssertRetimedCapture(state, nominal, 2, new ShortContinuationPlanner(10, 2));
        Assert.True(state.Citizens[0].Journey!.Legs[1].From.X < 1060);
    }

    [Fact]
    public void NativeOwnedLegacyUntaggedPrefixRetimesAtTheMeasuredCoordinate()
    {
        var (state, observed, elapsed) = CapturedAtLargeClock(3.00000001); Assert.True(observed < elapsed);
        state.Citizens[0].Journey!.Legs[0].ServiceId = ""; // Supported historical native geometry without fragment tags.
        string before = SaveCodec.Write(state); Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(before).Status);
        _ = new CitySimulator(SaveCodec.Read(before).State!, new ShortContinuationPlanner(10));
        AssertRetimedCapture(state, observed, 2, new ShortContinuationPlanner(10, 2));
    }

    private static void AssertRetimedCapture(CityState state, double observed, double rate, ITrafficRoutePlanner planner, bool rawPositionUnchanged = true)
    {
        var citizen = state.Citizens[0]; Position pose = citizen.Journey!.PositionAt(observed);
        var sampledPose = new CitySimulator(state, new UnmappedPlanner()).SampleJourney(citizen, observed);
        double departure = citizen.DepartureMinute; double clock = state.Minute;
        var original = state.Traffic!.Vehicles.Single(); long order = original.Order;
        var occupants = state.Traffic.Links.Select(link => link.Vehicles.ToArray()).ToArray();
        Assert.True(TravelTiming.Apply(state, 2, rate));
        Assert.Equal(clock, state.Minute); Assert.Equal(departure, citizen.DepartureMinute);
        var changed = state.Traffic!.Vehicles.Single(); Assert.Equal(order, changed.Order);
        for (int i = 0; i < occupants.Length; i++) Assert.Equal(occupants[i], state.Traffic.Links[i].Vehicles);
        if (rawPositionUnchanged) Assert.Equal(pose, citizen.Journey!.PositionAt(changed.NominalMinutes));
        Assert.Equal(original.Completed, changed.Completed); Assert.Equal(original.CompletedAt, changed.CompletedAt);
        string json = SaveCodec.Write(state); var decoded = SaveCodec.Read(json); Assert.Equal(SaveReadStatus.Loaded, decoded.Status);
        var rebound = new CitySimulator(decoded.State!, (IRoutePlanner)planner);
        double actual = rebound.Traffic!.GetPermission("car").NominalMinutes;
        Assert.Equal(changed.NominalMinutes, actual);
        if (rawPositionUnchanged) Assert.Equal(pose, decoded.State!.Citizens[0].Journey!.PositionAt(actual));
        var restoredSample = rebound.SampleJourney(decoded.State!.Citizens[0], actual);
        Assert.Equal(sampledPose.Position, restoredSample.Position); Assert.Equal(sampledPose.CarPosition, restoredSample.CarPosition);
        Assert.Equal(sampledPose.Mode, restoredSample.Mode);
        Assert.InRange(Math.Abs(actual - (clock - departure)), 0, 1e-7);
    }

    [Theory]
    [InlineData(false, .5)] [InlineData(false, 2)] [InlineData(false, 100)]
    [InlineData(true, .5)] [InlineData(true, 2)] [InlineData(true, 100)]
    public void OrdinaryClockCaptureRetimesBothSidesOfDepartureSubtractionRoundoff(bool leading, double rate)
    {
        double nominal = leading ? Math.BitIncrement(3) : Math.BitDecrement(3);
        var (state, observed, elapsed) = CapturedAtClock(nominal, 2194.8916206359863);
        Assert.Equal(3, elapsed); Assert.NotEqual(observed, elapsed);
        AssertRetimedCapture(state, observed, rate, new ShortContinuationPlanner(10, rate));
    }

    [Fact]
    public void CompletedTrafficRetainsItsTimestampAndOnlyRetimesItsFutureWalkingEgress()
    {
        var (original, _, _) = Captured(); var state = SaveCodec.Read(SaveCodec.Write(original)).State!;
        var citizen = state.Citizens[0]; citizen.Journey!.Legs[1].Mode = TravelMode.Walk; citizen.Journey.Legs[1].ServiceId = "";
        var planner = new Planner(); Assert.True(planner.TrafficNetwork.TryMap(citizen.Journey, out var mapped));
        var kernel = new TrafficKernel(planner.TrafficNetwork.Links, 0); kernel.Enqueue("car", mapped);
        kernel.AdvanceTo(0, 100); kernel.SetPhysicalControl("car", true); kernel.AdvanceTo(1, 100);
        Assert.Equal(1, kernel.ReportPhysicalProgress("car", 1)); kernel.AdvanceTo(1, 100);
        kernel.SetPhysicalControl("car", false);
        Assert.True(kernel.GetPermission("car").IsCompleted);
        state.Minute = 1; state.Traffic = kernel.Capture(); citizen.DepartureMinute = 0; citizen.NextEventMinute = 2;
        var completed = state.Traffic.Vehicles.Single(); Assert.Equal(1, completed.CompletedAt);
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(SaveCodec.Write(state)).Status);
        Assert.True(TravelTiming.Apply(state, 2, 2));
        Assert.Equal(1, citizen.Journey.Legs[0].Minutes); Assert.Equal(2, citizen.Journey.Legs[1].Minutes);
        Assert.Equal(3, citizen.NextEventMinute); Assert.Equal(0, citizen.DepartureMinute);
        Assert.True(state.Traffic!.Vehicles.Single().Completed); Assert.Equal(1, state.Traffic.Vehicles.Single().CompletedAt);
        var decoded = SaveCodec.Read(SaveCodec.Write(state)); Assert.Equal(SaveReadStatus.Loaded, decoded.Status);
        var rebound = new CitySimulator(decoded.State!, new Planner(2));
        Assert.Equal(0, rebound.Traffic!.VehicleCount); Assert.Equal(3, decoded.State!.Citizens[0].NextEventMinute);
        Assert.Equal(P(230), rebound.SampleJourney(decoded.State.Citizens[0]).Position);
    }

    private sealed class PolylinePlanner : IRoutePlanner, ITrafficRoutePlanner
    {
        private readonly bool closedDownstream;
        public PolylinePlanner(double[] x, double rate = 1, int boundary = -1, bool closedDownstream = false)
        {
            this.closedDownstream = closedDownstream;
            TrafficNetwork = new TrafficNetwork(Native,
                x.Select((value, i) => new TrafficNetworkNode { Id = i, Position = P(value), AllowsCar = true, Boundary = i == boundary,
                    KilometresPerHour = 36, Successors = i + 1 == x.Length ? Array.Empty<int>() : new[] { i + 1 } }).ToArray(), rate);
        }
        public TrafficNetwork TrafficNetwork { get; }
        public Route? Plan(Place a, Place b, double minute, TravelPreferences preferences) => null;
        public void SetTrafficForecast(Func<int, double, TrafficLinkForecast>? forecast) { }
        public bool IsTrafficLinkClosed(int linkId) => closedDownstream && linkId == 1;
    }

    [Theory]
    [InlineData(false, .9, .5)] [InlineData(false, .9, 2)] [InlineData(false, .9, 100)]
    [InlineData(true, 1.5, .5)] [InlineData(true, 1.5, 2)] [InlineData(true, 1.5, 100)]
    public void NonbinaryPrefixArithmeticMapsMeasuredTrafficToTheActualSplitBoundary(bool manyLegs, double nominal, double rate)
    {
        double[] x = manyLegs ? new[] { 220d, 223, 225, 229, 239, 249 } : new[] { 220d, 223, 233, 243 };
        var state = CapturedPolyline(x, nominal);
        AssertRetimedCapture(state, nominal, rate, new PolylinePlanner(x, rate));
        if (!manyLegs)
        {
            double prefixEnd = state.Citizens[0].Journey!.Legs.Take(2).Sum(leg => leg.Minutes);
            Assert.Equal(.9000000000000001, prefixEnd);
            Assert.Equal(prefixEnd, state.Traffic!.Vehicles.Single().NominalMinutes);
        }
    }

    private static CityState CapturedPolyline(double[] x, double nominal, int boundary = -1, bool closedDownstream = false)
    {
        var (source, _, _) = Captured(); var state = SaveCodec.Read(SaveCodec.Write(source)).State!;
        state.Traffic = null; state.TrafficTopologyId = ""; state.Minute = 2184.8916206359863;
        var citizen = state.Citizens[0]; citizen.Journey = new Route { Legs = Enumerable.Range(0, x.Length - 1)
            .Select(i => new RouteLeg { From = P(x[i]), To = P(x[i + 1]), Minutes = Math.Abs(x[i + 1] - x[i]) / 10,
                Mode = TravelMode.Car, ServiceId = Tag(i, i + 1) }).ToList() };
        citizen.DepartureMinute = state.Minute; citizen.NextEventMinute = state.Minute + citizen.Journey.Minutes;
        citizen.Car!.ParkedPosition = P(x[0]); state.Places.Single(place => place.Id == "a").Position = P(x[0]);
        state.Places.Single(place => place.Id == "b").Position = P(x[^1]);
        var simulation = new CitySimulator(state, new PolylinePlanner(x, boundary: boundary, closedDownstream: closedDownstream)); simulation.AdvanceTo(state.Minute);
        Assert.True(simulation.TryAcquirePhysicalJourney("car", out var lease)); simulation.AdvanceTo(state.Minute + 10);
        Assert.True(simulation.ObservePhysicalJourney(lease!, nominal)); Assert.True(simulation.ReleasePhysicalJourney(lease!));
        state.PrepareForSave();
        string json = SaveCodec.Write(state); Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(json).Status);
        _ = new CitySimulator(SaveCodec.Read(json).State!, new PolylinePlanner(x, boundary: boundary, closedDownstream: closedDownstream));
        return state;
    }

    [Theory]
    [InlineData(.5)] [InlineData(2)] [InlineData(100)]
    public void ExactCumulativeNonbinaryBoundaryDoesNotCreateAPhantomPartialNativeLeg(double rate)
    {
        double[] x = { 220, 223, 225, 229, 239, 249 };
        var state = CapturedPolyline(x, .9);
        Assert.Equal(.9, state.Citizens[0].Journey!.Legs.Take(3).Sum(leg => leg.Minutes));
        AssertRetimedCapture(state, .9, rate, new PolylinePlanner(x, rate));
        Assert.Equal(.9, state.Traffic!.Vehicles.Single().NominalMinutes);
        Assert.Equal(5, state.Citizens[0].Journey!.Legs.Count);
        Assert.Equal(new[] { .3, .2, .4, rate, rate }, state.Citizens[0].Journey!.Legs.Select(leg => leg.Minutes));
        Assert.All(state.Citizens[0].Journey!.Legs, leg => Assert.StartsWith("native:road:v1:", leg.ServiceId));
    }

    [Theory]
    [InlineData(.5)] [InlineData(2)] [InlineData(100)]
    public void QueuedCarAtAnExactNonbinarySectionEndRetimesWithoutGrantingBoundaryCrossing(double rate)
    {
        double[] x = { 220, 223, 225, 229, 239, 249 };
        var state = CapturedPolyline(x, .9, boundary: 3, closedDownstream: true);
        Assert.Equal(0, state.Traffic!.Vehicles.Single().RouteIndex);
        Assert.Equal(.9, TrafficKernel.Restore(state.Traffic, false).GetPermission("car").NominalMinutes);
        AssertRetimedCapture(state, .9, rate, new PolylinePlanner(x, rate, boundary: 3, closedDownstream: true));
        Assert.Equal(0, state.Traffic!.Vehicles.Single().RouteIndex);
        Assert.Equal(new[] { "car" }, state.Traffic.Links.Single(link => link.LinkId == 0).Vehicles);
        Assert.Empty(state.Traffic.Links.Single(link => link.LinkId == 1).Vehicles);
        Assert.False(state.Traffic.Vehicles.Single().Completed);
    }

    [Theory]
    [InlineData(.5)] [InlineData(2)]
    public void NormalizedNativeBoundaryKeepsItsSampledPoseWhenRawSubtractionCrossesTheCorner(double rate)
    {
        // A small canonical 36 km/h route reproduces the native capture's arithmetic:
        // cumulative 1.6 + 3.8 is 5.4, but repeated subtraction enters the next edge.
        double[] x = { 0, -16, 22, 23 }; var state = CapturedPolyline(x, 5.4);
        var citizen = state.Citizens[0]; var oldSimulation = new CitySimulator(state, new PolylinePlanner(x));
        var before = oldSimulation.SampleJourney(citizen, 5.4);
        Assert.Equal(P(22), before.Position); Assert.Equal(2, before.LegIndex); Assert.Equal(0, before.LegFraction);
        Assert.False(before.Position.Equals(citizen.Journey!.PositionAt(5.4)));
        long order = state.Traffic!.Vehicles.Single().Order; double departure = citizen.DepartureMinute;
        Assert.True(TravelTiming.Apply(state, 2, rate));
        Assert.Equal(departure, citizen.DepartureMinute); Assert.Equal(order, state.Traffic!.Vehicles.Single().Order);
        Assert.Equal(5.4, state.Traffic.Vehicles.Single().NominalMinutes);
        Assert.Equal(new[] { 1.6, 3.8, .1 * rate }, citizen.Journey!.Legs.Select(leg => leg.Minutes));
        Assert.All(citizen.Journey.Legs, leg => Assert.StartsWith("native:road:v1:", leg.ServiceId));
        var decoded = SaveCodec.Read(SaveCodec.Write(state)); Assert.Equal(SaveReadStatus.Loaded, decoded.Status);
        var rebound = new CitySimulator(decoded.State!, new PolylinePlanner(x, rate));
        var after = rebound.SampleJourney(decoded.State!.Citizens[0], rebound.Traffic!.GetPermission("car").NominalMinutes);
        Assert.Equal(before.Position, after.Position); Assert.Equal(before.CarPosition, after.CarPosition);
        Assert.Equal(before.Mode, after.Mode); Assert.Equal(before.LegIndex, after.LegIndex); Assert.Equal(0, after.LegFraction);
    }

    private sealed class UnmappedPlanner : IRoutePlanner
    {
        public Route? Plan(Place a, Place b, double minute, TravelPreferences preferences) => null;
    }

    [Fact]
    public void SharedPureSamplerRetainsPhysicalBoundaryCarParkingAndPublicValidationContracts()
    {
        var (original, _, _) = Captured(); var state = SaveCodec.Read(SaveCodec.Write(original)).State!;
        state.Traffic = null; state.TrafficTopologyId = ""; state.Minute = 0;
        var citizen = state.Citizens[0]; citizen.Journey = new Route { Legs = new()
        {
            new RouteLeg { From = P(220), To = P(221), Mode = TravelMode.Walk, Minutes = .1 },
            new RouteLeg { From = P(221), To = P(221), Mode = TravelMode.Car, Minutes = .2 },
            new RouteLeg { From = P(221), To = P(231), Mode = TravelMode.Car, Minutes = .9 },
            new RouteLeg { From = P(231), To = P(240), Mode = TravelMode.Walk, Minutes = .3 }
        } };
        citizen.DepartureMinute = 0; citizen.NextEventMinute = citizen.Journey.Minutes; citizen.Car!.ParkedPosition = P(221);
        var simulation = new CitySimulator(state, new UnmappedPlanner());
        var pure = typeof(CitySimulator).GetMethod("SampleRoute", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        foreach (double elapsed in new[] { 0, .1, .3, .75, 1.2, citizen.Journey.Minutes })
        {
            var expected = (PhysicalJourneySample)pure.Invoke(null, new object?[] { citizen.Journey, elapsed, citizen.Car.ParkedPosition })!;
            Assert.Equal(expected, simulation.SampleJourney(citizen, elapsed));
        }
        Assert.Equal(P(221), simulation.SampleJourney(citizen, 0).CarPosition);
        Assert.Equal(1, simulation.SampleJourney(citizen, .1).LegIndex);
        Assert.Equal(2, simulation.SampleJourney(citizen, .3).LegIndex);
        Assert.Equal(226, simulation.SampleJourney(citizen, .75).Position.X, 12);
        Assert.Equal(simulation.SampleJourney(citizen, .75).Position, simulation.SampleJourney(citizen, .75).CarPosition);
        var egress = simulation.SampleJourney(citizen, 1.2); Assert.Equal(TravelMode.Walk, egress.Mode);
        Assert.Equal(P(231), egress.Position); Assert.Equal(P(231), egress.CarPosition);
        var completed = simulation.SampleJourney(citizen, citizen.Journey.Minutes);
        Assert.True(completed.AtEnd); Assert.Equal(P(240), completed.Position); Assert.Equal(P(231), completed.CarPosition);
        Assert.Throws<ArgumentNullException>(() => simulation.SampleJourney(null!, 0));
        foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, -1, citizen.Journey.Minutes + 1 })
            Assert.Throws<ArgumentOutOfRangeException>(() => simulation.SampleJourney(citizen, invalid));
        citizen.Phase = CitizenPhase.AtPlace;
        Assert.Throws<InvalidOperationException>(() => simulation.SampleJourney(citizen, 0));
    }

    [Theory]
    [InlineData(false, .5)] [InlineData(false, 2)] [InlineData(true, .5)] [InlineData(true, 2)]
    public void SamplerNormalizedLagAtACoalescedCornerRetimesWithoutMovingTheBodyBack(bool sloped, double rate)
    {
        var (state, citizen, raw) = Captured(gap: 5e-13, sloped: sloped);
        var measured = new CitySimulator(state, new UnmappedPlanner()).SampleJourney(citizen, raw);
        Assert.Equal(1, measured.LegIndex); Assert.Equal(0, measured.LegFraction);
        Assert.Equal(citizen.Journey!.Legs[0].To, measured.Position);
        Assert.False(citizen.Journey.PositionAt(raw).Equals(measured.Position));
        AssertRetimedCapture(state, raw, rate, new Planner(rate, rise: sloped ? .5 : 0), rawPositionUnchanged: false);
        Assert.Equal(raw, state.Traffic!.Vehicles.Single().NominalMinutes);
        Assert.Equal(2, citizen.Journey.Legs.Count); Assert.Equal(raw, citizen.Journey.Legs[0].Minutes);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public void SamplerNormalizationCannotCreditAnUnreportedTerminalOrInternalSectionEndpoint(bool sloped, bool terminal)
    {
        var (state, citizen, raw) = Captured(terminal: terminal, gap: 5e-13, sectionBoundary: true, sloped: sloped);
        string before = SaveCodec.Write(state); var route = citizen.Journey; var traffic = state.Traffic;
        var error = Assert.Throws<ArgumentException>(() => TravelTiming.Apply(state, 2, 2));
        Assert.Contains("unreported occupied traffic section endpoint", error.Message);
        Assert.Same(route, citizen.Journey); Assert.Same(traffic, state.Traffic); Assert.Equal(before, SaveCodec.Write(state));
        var rebound = new CitySimulator(SaveCodec.Read(before).State!, new Planner(sectionBoundary: true, rise: sloped ? .5 : 0));
        Assert.Equal(raw, rebound.Traffic!.GetPermission("car").NominalMinutes); Assert.False(rebound.Traffic.GetPermission("car").IsCompleted);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void TailJustOutsideTheExistingSamplerBoundaryWindowKeepsItsRealGeometry(bool sloped)
    {
        var (state, citizen, raw) = Captured(gap: 2e-12, sloped: sloped);
        var before = new CitySimulator(state, new UnmappedPlanner()).SampleJourney(citizen, raw);
        Assert.Equal(0, before.LegIndex); Assert.True(before.LegFraction < 1);
        Assert.False(before.Position.Equals(citizen.Journey!.Legs[0].To));
        AssertRetimedCapture(state, raw, 2, new Planner(2, rise: sloped ? .5 : 0));
        Assert.Equal(3, citizen.Journey.Legs.Count); Assert.Equal(before.Position, citizen.Journey.Legs[1].From);
        Assert.True(Position.Distance(citizen.Journey.Legs[1].From, citizen.Journey.Legs[1].To) > 0);
    }

    [Fact]
    public void AmbiguousGeometryOutsideSamplerNormalizationStillRefusesWithoutMutation()
    {
        double[] x = { 1000000, 1000010, 1000020 }; double raw = 1 - 1e-10;
        var state = CapturedPolyline(x, raw); var citizen = state.Citizens[0];
        var before = new CitySimulator(state, new UnmappedPlanner()).SampleJourney(citizen, raw);
        Assert.Equal(0, before.LegIndex); Assert.True(before.LegFraction < 1);
        Assert.False(before.Position.Equals(citizen.Journey!.Legs[0].To));
        string json = SaveCodec.Write(state); var route = citizen.Journey; var traffic = state.Traffic;
        var error = Assert.Throws<ArgumentException>(() => TravelTiming.Apply(state, 2, 2));
        Assert.Contains("ambiguous double-precision projection", error.Message);
        Assert.Same(route, citizen.Journey); Assert.Same(traffic, state.Traffic); Assert.Equal(json, SaveCodec.Write(state));
        _ = new CitySimulator(SaveCodec.Read(json).State!, new PolylinePlanner(x));
    }

    [Theory]
    [InlineData(TravelMode.Walk, false, false)] [InlineData(TravelMode.Subway, false, false)]
    [InlineData(TravelMode.Subway, true, false)] [InlineData(TravelMode.Subway, true, true)]
    public void UnownedWalkingAndTransitHandoffsReuseTheSamplerWithoutDroppingFixedWaitMinutes(TravelMode nextMode, bool stationary, bool tinyGeometry)
    {
        var (source, _, raw) = Captured(gap: 5e-13); var state = SaveCodec.Read(SaveCodec.Write(source)).State!;
        state.Traffic = null; state.TrafficTopologyId = ""; var citizen = state.Citizens[0];
        citizen.Journey!.Legs[0].Mode = TravelMode.Walk; citizen.Journey.Legs[0].ServiceId = stationary ? "station-wait" : "";
        citizen.Journey.Legs[1].Mode = nextMode; citizen.Journey.Legs[1].ServiceId = nextMode == TravelMode.Subway ? "subway:line-1" : "";
        if (stationary)
        {
            citizen.Journey.Legs[0].To = tinyGeometry ? P(220 + 5e-7) : citizen.Journey.Legs[0].From;
            citizen.Journey.Legs[1].From = citizen.Journey.Legs[0].To;
            if (tinyGeometry) Assert.InRange(Position.Distance(citizen.Journey.Legs[0].From, citizen.Journey.Legs[0].To), double.Epsilon, 1e-6);
        }
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(SaveCodec.Write(state)).Status);
        var before = new CitySimulator(state, new UnmappedPlanner()).SampleJourney(citizen, raw);
        double departure = citizen.DepartureMinute; Assert.Equal(1, before.LegIndex); Assert.Equal(0, before.LegFraction);
        Assert.True(TravelTiming.Apply(state, 2, 2));
        Assert.Equal(departure, citizen.DepartureMinute); Assert.Null(state.Traffic); Assert.Equal(2, citizen.Journey.Legs.Count);
        Assert.Equal(stationary ? 1 : raw, citizen.Journey.Legs[0].Minutes);
        Assert.Equal(2, citizen.Journey.Legs[1].Minutes);
        Assert.Equal(departure + citizen.Journey.Minutes, citizen.NextEventMinute);
        var decoded = SaveCodec.Read(SaveCodec.Write(state)); Assert.Equal(SaveReadStatus.Loaded, decoded.Status);
        var rebound = new CitySimulator(decoded.State!, new UnmappedPlanner());
        var after = rebound.SampleJourney(decoded.State!.Citizens[0], raw);
        Assert.Equal(before.Position, after.Position); Assert.Equal(before.CarPosition, after.CarPosition);
        Assert.Equal(before.Mode, after.Mode); Assert.Equal(before.LegFraction, after.LegFraction);
    }

    [Fact]
    public void SamplerProvenCornerResidualStillMustPassTheDetachedSpanTimingGate()
    {
        double[] x = { 1000, 1060, 1060.000005 }; double raw = 6 - 5e-13;
        var state = CapturedPolyline(x, raw); var citizen = state.Citizens[0];
        var measured = new CitySimulator(state, new UnmappedPlanner()).SampleJourney(citizen, raw);
        Assert.Equal(1, measured.LegIndex); Assert.Equal(0, measured.LegFraction);
        string before = SaveCodec.Write(state); var route = citizen.Journey; var traffic = state.Traffic;
        var error = Assert.Throws<ArgumentException>(() => TravelTiming.Apply(state, 2, 2));
        Assert.Contains("duration", error.Message);
        Assert.Same(route, citizen.Journey); Assert.Same(traffic, state.Traffic); Assert.Equal(before, SaveCodec.Write(state));
        _ = new CitySimulator(SaveCodec.Read(before).State!, new PolylinePlanner(x));
    }

    private static CityState UnownedCanonicalWalkingRoute(double[] x, double elapsed)
    {
        var state = Population.Create(new[] { new CitizenIdentity { Id = "walker", Name = "Walker", Age = 30, Persona = Persona.Commuter } },
            new[] { new Place { Id = "a", Name = "Home", Kind = PlaceKind.Home, Capacity = 1, Position = P(x[0]) },
                new Place { Id = "b", Name = "Work", Kind = PlaceKind.Work, Capacity = 1, Position = P(x[^1]) } },
            "walking-boundary-retime", "0.40.4", 7, 0);
        state.Minute = elapsed; state.TravelTimingVersion = 1; state.TravelClockRate = 2;
        var citizen = state.Citizens[0]; citizen.PlaceId = citizen.HomeId = "a"; citizen.DestinationId = citizen.DayPlaceId = "b";
        citizen.Phase = CitizenPhase.Travelling; citizen.DestinationActivity = Activity.Work; citizen.ActivityUntilMinute = 500;
        citizen.Journey = new Route { Legs = Enumerable.Range(0, x.Length - 1).Select(i => new RouteLeg
            { From = P(x[i]), To = P(x[i + 1]), Minutes = Math.Abs(x[i + 1] - x[i]), Mode = TravelMode.Walk }).ToList() };
        citizen.DepartureMinute = 0; citizen.NextEventMinute = citizen.Journey.Minutes;
        return state;
    }

    [Fact]
    public void SampledStartWithZeroIterativeResidueRetimesBothDirectionsAndEventuallyArrives()
    {
        double[] x = { 0, 2.6, 7.800000000000001, 9.3 };
        double elapsed = Math.BitIncrement(x[2]); var state = UnownedCanonicalWalkingRoute(x, elapsed); var citizen = state.Citizens[0];
        Assert.Equal(7.800000000000001, citizen.Journey!.Legs.Take(2).Sum(leg => leg.Minutes));
        double residue = elapsed; foreach (var leg in citizen.Journey.Legs.Take(2)) residue -= leg.Minutes;
        Assert.Equal(0, residue); Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(SaveCodec.Write(state)).Status);
        var before = new CitySimulator(state, new UnmappedPlanner()).SampleJourney(citizen);
        Assert.Equal(2, before.LegIndex); Assert.Equal(0, before.LegFraction); Assert.Equal(P(x[2]), before.Position);
        foreach (double rate in new[] { 1d, 2, 1, 2 })
        {
            Assert.True(TravelTiming.Apply(state, 2, rate));
            Assert.Equal(3, citizen.Journey!.Legs.Count); Assert.All(citizen.Journey.Legs, leg => Assert.True(leg.Minutes > 0));
            Assert.Equal(2.6, citizen.Journey.Legs[0].Minutes); Assert.Equal(5.200000000000001, citizen.Journey.Legs[1].Minutes);
            Assert.Equal(1.5 * rate / 2, citizen.Journey.Legs[2].Minutes);
            Assert.Equal(0, citizen.DepartureMinute); Assert.Equal(elapsed, state.Minute);
            Assert.Equal(citizen.Journey.Minutes, citizen.NextEventMinute);
            var decoded = SaveCodec.Read(SaveCodec.Write(state)); Assert.Equal(SaveReadStatus.Loaded, decoded.Status);
            state = decoded.State!; citizen = state.Citizens[0];
            var sample = new CitySimulator(state, new UnmappedPlanner()).SampleJourney(citizen);
            Assert.Equal(before.Position, sample.Position); Assert.Equal(before.CarPosition, sample.CarPosition); Assert.Equal(before.Mode, sample.Mode);
        }
        var resumed = new CitySimulator(state, new UnmappedPlanner()); double arrival = citizen.NextEventMinute;
        resumed.AdvanceTo(arrival);
        Assert.Equal(CitizenPhase.AtPlace, citizen.Phase); Assert.Equal(1, citizen.CompletedJourneys);
        Assert.Equal("b", citizen.PlaceId); Assert.Equal(P(x[^1]), resumed.PositionOf(citizen));
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(SaveCodec.Write(state)).Status);
    }

    [Fact]
    public void SampledEndpointHandlesProvenIterativeOverrunWithoutAddingATail()
    {
        double[] x = { 0, 4.9, 9.3, 11.700000000000001, 21.5, 30.8, 33.1 };
        double elapsed = Math.BitDecrement(x[5]); var state = UnownedCanonicalWalkingRoute(x, elapsed); var citizen = state.Citizens[0];
        double residue = elapsed; foreach (var leg in citizen.Journey!.Legs.Take(4)) residue -= leg.Minutes;
        Assert.True(residue > citizen.Journey.Legs[4].Minutes);
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(SaveCodec.Write(state)).Status);
        var before = new CitySimulator(state, new UnmappedPlanner()).SampleJourney(citizen);
        Assert.Equal(5, before.LegIndex); Assert.Equal(0, before.LegFraction); Assert.Equal(P(x[5]), before.Position);
        Assert.True(TravelTiming.Apply(state, 2, 1));
        Assert.Equal(6, citizen.Journey.Legs.Count); Assert.All(citizen.Journey.Legs, leg => Assert.True(leg.Minutes > 0));
        var decoded = SaveCodec.Read(SaveCodec.Write(state)); Assert.Equal(SaveReadStatus.Loaded, decoded.Status);
        var resumed = new CitySimulator(decoded.State!, new UnmappedPlanner()); var restored = decoded.State!.Citizens[0];
        var after = resumed.SampleJourney(restored); Assert.Equal(before.Position, after.Position); Assert.Equal(before.CarPosition, after.CarPosition);
        Assert.Equal(before.Mode, after.Mode); Assert.Equal(0, after.LegFraction);
        resumed.AdvanceTo(restored.NextEventMinute);
        Assert.Equal(1, restored.CompletedJourneys); Assert.Equal("b", restored.PlaceId);
    }

    [Fact]
    public void SampledZeroResidueStartRetainsTheEntireFuturePolicyWait()
    {
        double[] x = { 0, 2.6, 7.800000000000001, 9.3 }; double elapsed = Math.BitIncrement(x[2]);
        var state = UnownedCanonicalWalkingRoute(x, elapsed); var citizen = state.Citizens[0];
        citizen.Journey!.Legs[2].To = citizen.Journey.Legs[2].From; citizen.Journey.Legs[2].ServiceId = "station-wait";
        state.Places.Single(place => place.Id == "b").Position = P(x[2]);
        var before = new CitySimulator(state, new UnmappedPlanner()).SampleJourney(citizen);
        Assert.Equal(2, before.LegIndex); Assert.Equal(0, before.LegFraction);
        Assert.True(TravelTiming.Apply(state, 2, 1));
        Assert.Equal(3, citizen.Journey.Legs.Count); Assert.Equal(1.5, citizen.Journey.Legs[2].Minutes);
        Assert.Equal(citizen.Journey.Minutes, citizen.NextEventMinute);
        var decoded = SaveCodec.Read(SaveCodec.Write(state)); Assert.Equal(SaveReadStatus.Loaded, decoded.Status);
        var restored = decoded.State!.Citizens[0]; var resumed = new CitySimulator(decoded.State, new UnmappedPlanner());
        var after = resumed.SampleJourney(restored); Assert.Equal(before.Position, after.Position); Assert.Equal(before.Mode, after.Mode);
        resumed.AdvanceTo(restored.NextEventMinute); Assert.Equal(1, restored.CompletedJourneys); Assert.Equal("b", restored.PlaceId);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CanonicalWalkingBoundaryUlpsSurviveRepeatedClockRoundtripsAndArrival(bool longerRoute)
    {
        double[] x = longerRoute ? new[] { 0, 4.9, 9.3, 11.700000000000001, 21.5, 30.8, 33.1 }
            : new[] { 0, 2.6, 7.800000000000001, 9.3 };
        for (int boundary = 1; boundary < x.Length - 1; boundary++)
        for (int ulps = -4; ulps <= 4; ulps++)
        {
            double elapsed = x[boundary];
            for (int i = 0; i < Math.Abs(ulps); i++) elapsed = ulps < 0 ? Math.BitDecrement(elapsed) : Math.BitIncrement(elapsed);
            var state = UnownedCanonicalWalkingRoute(x, elapsed); var citizen = state.Citizens[0];
            var measured = new CitySimulator(state, new UnmappedPlanner()).SampleJourney(citizen);
            foreach (double rate in new[] { 1d, 2, 1, 2 })
            {
                Assert.True(TravelTiming.Apply(state, 2, rate));
                Assert.Equal(x.Length - 1, citizen.Journey!.Legs.Count);
                Assert.All(citizen.Journey.Legs, leg => Assert.True(leg.Minutes > 0));
                Assert.Equal(0, citizen.DepartureMinute); Assert.Equal(elapsed, state.Minute);
                Assert.Equal(citizen.Journey.Minutes, citizen.NextEventMinute);
                var decoded = SaveCodec.Read(SaveCodec.Write(state)); Assert.Equal(SaveReadStatus.Loaded, decoded.Status);
                state = decoded.State!; citizen = state.Citizens[0];
                var after = new CitySimulator(state, new UnmappedPlanner()).SampleJourney(citizen);
                Assert.Equal(measured.Position, after.Position); Assert.Equal(measured.CarPosition, after.CarPosition);
                Assert.Equal(measured.Mode, after.Mode);
            }
            var resumed = new CitySimulator(state, new UnmappedPlanner());
            resumed.AdvanceTo(citizen.NextEventMinute);
            Assert.Equal(1, citizen.CompletedJourneys); Assert.Equal("b", citizen.PlaceId);
            Assert.Equal(P(x[^1]), resumed.PositionOf(citizen));
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void UnownedPartialPrefixUsesTheResumedClockRatherThanTheTrafficMapCoordinate(bool nearZero)
    {
        double[] x = nearZero ? new[] { -49.1, -8.3, 0, 3.0 }
            : new[] { 0, .2, 8.2, 17.299999999999997 };
        double elapsed = nearZero ? 49.100000099999995 : 8.200000999999999;
        var state = UnownedCanonicalWalkingRoute(x, elapsed); var citizen = state.Citizens[0]; var route = citizen.Journey!;
        var simulation = new CitySimulator(state, new UnmappedPlanner()); var before = simulation.SampleJourney(citizen);
        // Prove the former check's coordinate differs from the unchanged departure-clock
        // coordinate, even though the actual resumed physical sample preserves the body.
        var retime = typeof(TravelTiming).GetMethod("Retime", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        object?[] arguments = { route, elapsed, false, 2d, 1d, .5d, null };
        var replacement = (Route)retime.Invoke(null, arguments)!; var map = arguments[^1]!;
        double mapped = (double)map.GetType().GetMethod("Map", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(map, new object[] { elapsed })!;
        Assert.NotEqual(elapsed, mapped); citizen.Journey = replacement;
        var actual = simulation.SampleJourney(citizen); var mappedSample = simulation.SampleJourney(citizen, mapped);
        Assert.Equal(before.Position, actual.Position); Assert.Equal(before.CarPosition, actual.CarPosition); Assert.Equal(before.Mode, actual.Mode);
        Assert.False(before.Position.Equals(mappedSample.Position)); citizen.Journey = route;
        foreach (double rate in new[] { 1d, 2, 1, 2 })
        {
            Assert.True(TravelTiming.Apply(state, 2, rate)); Assert.Equal(0, citizen.DepartureMinute); Assert.Equal(elapsed, state.Minute);
            Assert.All(citizen.Journey!.Legs, leg => Assert.True(leg.Minutes > 0));
            var decoded = SaveCodec.Read(SaveCodec.Write(state)); Assert.Equal(SaveReadStatus.Loaded, decoded.Status);
            state = decoded.State!; citizen = state.Citizens[0];
            var resumedSample = new CitySimulator(state, new UnmappedPlanner()).SampleJourney(citizen);
            Assert.Equal(before.Position, resumedSample.Position); Assert.Equal(before.CarPosition, resumedSample.CarPosition); Assert.Equal(before.Mode, resumedSample.Mode);
        }
        var resumed = new CitySimulator(state, new UnmappedPlanner()); resumed.AdvanceTo(citizen.NextEventMinute);
        Assert.Equal(1, citizen.CompletedJourneys); Assert.Equal("b", citizen.PlaceId); Assert.Equal(P(x[^1]), resumed.PositionOf(citizen));
    }

}
