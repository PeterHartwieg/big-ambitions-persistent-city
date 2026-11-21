using System;
using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;
using System.Linq;
using System.Reflection;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

public sealed class Gr5NetworkRequeueTests
{
    private readonly ITestOutputHelper output;
    public Gr5NetworkRequeueTests(ITestOutputHelper output) { this.output = output; }

    private const string Native = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    internal sealed class SwappablePlanner : IRoutePlanner, ITrafficRoutePlanner
    {
        public TrafficNetwork? TrafficNetwork { get; set; } = Network();
        public void SetTrafficForecast(Func<int, double, TrafficLinkForecast>? forecast) { }
        public bool IsTrafficLinkClosed(int linkId) => false;
        public Route? Plan(Place from, Place to, double departureMinute, TravelPreferences preferences) => null;
    }

    private static TrafficNetwork Network() => new TrafficNetwork(Native, new[]
    {
        new TrafficNetworkNode { Id = 0, Position = new Position(0, 0, 0), KilometresPerHour = 36, AllowsCar = true, Boundary = true, Successors = new[] { 1 } },
        new TrafficNetworkNode { Id = 1, Position = new Position(12, 0, 0), KilometresPerHour = 36, AllowsCar = true, Boundary = true, Successors = new[] { 2 } },
        new TrafficNetworkNode { Id = 2, Position = new Position(24, 0, 0), KilometresPerHour = 36, AllowsCar = true, Boundary = true }
    }, 1);

    private static CitySimulator City(int commuters, int resting, SwappablePlanner planner)
    {
        int count = commuters + resting;
        var state = Population.Create(Enumerable.Range(0, count).Select(i => new CitizenIdentity
        { Id = "requeue" + i.ToString("D4"), Name = "Resident " + i, Age = 30, Persona = Persona.Commuter }), new[]
        {
            new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = count, Position = new Position(-4, 0, 0) },
            new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = count, Position = new Position(30, 0, 0) }
        }, "network-requeue", "0.40.4", 7, 1440);
        for (int i = 0; i < count; i++)
        {
            var c = state.Citizens[i];
            c.PlaceId = c.HomeId = "home"; c.DayPlaceId = "work";
            if (i >= commuters)
            {
                c.Phase = CitizenPhase.AtPlace; c.Activity = Activity.Home;
                c.ActivityUntilMinute = 9000 + i; c.NextEventMinute = 9000 + i;
                continue;
            }
            c.Phase = CitizenPhase.Travelling; c.DestinationId = "work";
            c.DestinationActivity = Activity.Work; c.ActivityUntilMinute = 5000; c.DepartureMinute = 1440; c.Preferences.OwnsCar = true;
            c.Car = new CarState { Id = c.Identity.Id + ":car", ParkedPosition = new Position(0, 0, 0) };
            c.Journey = new Route();
            c.Journey.Legs.Add(new RouteLeg { From = state.Places[0].Position, To = new Position(0, 0, 0), Minutes = 2, Mode = TravelMode.Walk });
            c.Journey.Legs.Add(new RouteLeg { From = new Position(0, 0, 0), To = new Position(12, 0, 0), Minutes = 1.2, Mode = TravelMode.Car, ServiceId = "native:road:v1:" + Native + ":0:1" });
            c.Journey.Legs.Add(new RouteLeg { From = new Position(12, 0, 0), To = new Position(24, 0, 0), Minutes = 1.2, Mode = TravelMode.Car, ServiceId = "native:road:v1:" + Native + ":1:2" });
            c.Journey.Legs.Add(new RouteLeg { From = new Position(24, 0, 0), To = state.Places[1].Position, Minutes = 3, Mode = TravelMode.Walk });
            c.NextEventMinute = c.DepartureMinute + c.Journey.Minutes;
        }
        return new CitySimulator(state, planner);
    }

    [Fact]
    public void SwappedEqualNetworkKeepsTheSameQueueJourneysAndSaveBytes()
    {
        var steadyPlanner = new SwappablePlanner();
        var swappedPlanner = new SwappablePlanner();
        var steady = City(40, 60, steadyPlanner);
        var swapped = City(40, 60, swappedPlanner);
        foreach (double minute in new[] { 1440.5, 1441.7, 1443.1, 1444.9, 1447.3, 1452.0 })
        {
            steady.AdvanceTo(minute);
            swapped.AdvanceTo(minute);
            swappedPlanner.TrafficNetwork = Network();
            steady.RefreshTrafficClosures();
            swapped.RefreshTrafficClosures();
            Assert.Equal(steady.QueuedPersonEvents, swapped.QueuedPersonEvents);
            Assert.Equal(SaveCodec.Write(steady.State), SaveCodec.Write(swapped.State));
        }
        steady.AdvanceTo(3600);
        swapped.AdvanceTo(3600);
        Assert.Equal(steady.State.Citizens.Select(c => c.CompletedJourneys), swapped.State.Citizens.Select(c => c.CompletedJourneys));
        Assert.Equal(steady.State.Citizens.Select(c => c.NextEventMinute), swapped.State.Citizens.Select(c => c.NextEventMinute));
        Assert.Equal(SaveCodec.Write(steady.State), SaveCodec.Write(swapped.State));
    }

    [Fact]
    public void NetworkSwapDoesNotReallocateTheRestingQueue()
    {
        const int resting = 3000;
        var planner = new SwappablePlanner();
        var city = City(0, resting, planner);
        city.RefreshTrafficClosures();
        planner.TrafficNetwork = Network();
        city.RefreshTrafficClosures();
        planner.TrafficNetwork = Network();
        long before = GC.GetAllocatedBytesForCurrentThread();
        city.RefreshTrafficClosures();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(resting, city.QueuedPersonEvents);
        Assert.True(allocated < 4096, "network swap allocated " + allocated + " bytes for " + resting + " resting residents");
    }

    private const int RoadNodes = 4 * 14000 + 1;
    private const int DriveLegs = 40;

    internal static TrafficNetwork Road() => new TrafficNetwork(Native, RoadNodesForTest(), 1);

    internal static List<TrafficNetworkNode> RoadNodesForTest() => Enumerable.Range(0, RoadNodes).Select(i => new TrafficNetworkNode
    {
        Id = i, Position = new Position(i * 12, 0, 0), KilometresPerHour = 36, AllowsCar = true,
        Boundary = i % 4 == 0, Successors = i + 1 < RoadNodes ? new[] { i + 1 } : Array.Empty<int>()
    }).ToList();

    internal static CitySimulator RoadCity(int drivers, int resting, SwappablePlanner planner)
    {
        int count = drivers + resting;
        var state = Population.Create(Enumerable.Range(0, count).Select(i => new CitizenIdentity
        { Id = "swap" + i.ToString("D4"), Name = "Resident " + i, Age = 30, Persona = Persona.Commuter }), new[]
        {
            new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = count, Position = new Position(-4, 0, 0) },
            new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = count, Position = new Position(-8, 0, 0) }
        }, "network-swap-budget", "0.40.4", 7, 1440);
        for (int i = 0; i < count; i++)
        {
            var c = state.Citizens[i];
            c.PlaceId = c.HomeId = "home"; c.DayPlaceId = "work";
            if (i >= drivers)
            {
                c.Phase = CitizenPhase.AtPlace; c.Activity = Activity.Home;
                c.ActivityUntilMinute = 9000 + i; c.NextEventMinute = 9000 + i;
                continue;
            }
            int start = i * 37 % (RoadNodes - DriveLegs - 1);
            c.Phase = CitizenPhase.Travelling; c.DestinationId = "work";
            c.DestinationActivity = Activity.Work; c.ActivityUntilMinute = 5000; c.Preferences.OwnsCar = true;
            c.Car = new CarState { Id = c.Identity.Id + ":car", ParkedPosition = new Position(start * 12, 0, 0) };
            c.Journey = new Route();
            c.Journey.Legs.Add(new RouteLeg { From = state.Places[0].Position, To = new Position(start * 12, 0, 0), Minutes = 2, Mode = TravelMode.Walk });
            for (int k = start; k < start + DriveLegs; k++)
                c.Journey.Legs.Add(new RouteLeg { From = new Position(k * 12, 0, 0), To = new Position((k + 1) * 12, 0, 0), Minutes = 0.02, Mode = TravelMode.Car,
                    ServiceId = "native:road:v1:" + Native + ":" + k + ":" + (k + 1) });
            c.Journey.Legs.Add(new RouteLeg { From = new Position((start + DriveLegs) * 12, 0, 0), To = state.Places[1].Position, Minutes = 3, Mode = TravelMode.Walk });
            c.Journey = c.Journey.Commit();
            c.DepartureMinute = 1440 - 2 - 0.02 * (i % (DriveLegs - 4));
            c.NextEventMinute = c.DepartureMinute + c.Journey.Minutes;
        }
        return new CitySimulator(state, planner);
    }

    [Fact]
    public void ARigScaleNetworkSwapStaysUnderTheFrameBudgetAndAllocatesNothingAfterWarmUp()
    {
        var networks = new[] { Road(), Road() };
        var planner = new SwappablePlanner { TrafficNetwork = networks[0] };
        var city = RoadCity(750, 2250, planner);
        city.RefreshTrafficClosures();
        int queued = city.QueuedPersonEvents;
        var timings = new List<double>();
        long allocated = 0;
        for (int swap = 1; swap <= 12; swap++)
        {
            planner.TrafficNetwork = networks[swap % 2];
            long bytes = GC.GetAllocatedBytesForCurrentThread();
            long started = Stopwatch.GetTimestamp();
            city.RefreshTrafficClosures();
            timings.Add((Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency);
            if (swap > 2) allocated += GC.GetAllocatedBytesForCurrentThread() - bytes;
            Assert.Equal(queued, city.QueuedPersonEvents);
        }
        timings.Sort();
        output.WriteLine("network swap, 750 drivers x " + DriveLegs + " road legs, 2,250 resting, 14,000 links: median "
            + timings[timings.Count / 2].ToString("0.00") + " ms, max " + timings[^1].ToString("0.00") + " ms; " + allocated + " B over 10 warm swaps (.NET)");
        Assert.True(allocated < 4096 * 10, "warm network swaps allocated " + allocated + " bytes");
        PerfGate.Under(timings[timings.Count / 2], 25, "one network swap at rig scale (.NET)");
    }

    [Fact]
    public void APlayerHoldDuringASlicedNetworkSwapKeepsTheHeldRoadEntryLikeAWholeSwap()
    {
        var wholePlanner = new SwappablePlanner();
        var slicedPlanner = new SwappablePlanner();
        var whole = City(1, 0, wholePlanner);
        var sliced = City(1, 0, slicedPlanner);
        string id = whole.State.Citizens[0].Identity.Id;
        whole.AdvanceTo(1440.5); sliced.AdvanceTo(1440.5);
        whole.RefreshTrafficClosures(); sliced.RefreshTrafficClosures();
        wholePlanner.TrafficNetwork = Network(); slicedPlanner.TrafficNetwork = Network();
        Assert.False(sliced.TryRefreshTrafficClosures(0));
        Assert.True(sliced.HoldForPlayer(id, 0.25, PlayerHoldCause.PlayerFoot).Applied);
        Assert.True(whole.HoldForPlayer(id, 0.25, PlayerHoldCause.PlayerFoot).Applied);
        while (!sliced.TryRefreshTrafficClosures(0)) { }
        whole.RefreshTrafficClosures();
        Assert.Equal(SaveCodec.Write(whole.State), SaveCodec.Write(sliced.State));
        foreach (double minute in new[] { 1442.0, 1442.25, 1443.0, 1446.0, 1460.0 })
        {
            whole.AdvanceTo(minute); sliced.AdvanceTo(minute);
            whole.RefreshTrafficClosures(); sliced.RefreshTrafficClosures();
            Assert.Equal(whole.State.Citizens[0].Phase, sliced.State.Citizens[0].Phase);
            Assert.Equal(whole.State.Citizens[0].CompletedJourneys, sliced.State.Citizens[0].CompletedJourneys);
            Assert.Equal(SaveCodec.Write(whole.State), SaveCodec.Write(sliced.State));
        }
    }

    [Fact]
    public void ARigScaleNetworkSwapPreparesAcrossUpdatesWithinTheSliceAndMatchesAWholeSwap()
    {
        var networks = new[] { Road(), Road() };
        var wholePlanner = new SwappablePlanner { TrafficNetwork = networks[0] };
        var slicedPlanner = new SwappablePlanner { TrafficNetwork = networks[0] };
        var whole = RoadCity(750, 2250, wholePlanner);
        var sliced = RoadCity(750, 2250, slicedPlanner);
        whole.RefreshTrafficClosures(); sliced.RefreshTrafficClosures();
        var updates = new List<double>();
        for (int swap = 1; swap <= 4; swap++)
        {
            wholePlanner.TrafficNetwork = slicedPlanner.TrafficNetwork = networks[swap % 2];
            whole.RefreshTrafficClosures();
            while (true)
            {
                long started = Stopwatch.GetTimestamp();
                bool done = sliced.TryRefreshTrafficClosures(ClockRetimeStages.SliceMilliseconds);
                updates.Add((Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency);
                if (done) break;
            }
            Assert.Equal(whole.QueuedPersonEvents, sliced.QueuedPersonEvents);
            Assert.Equal(SaveCodec.Write(whole.State), SaveCodec.Write(sliced.State));
        }
        updates.Sort();
        output.WriteLine("sliced network swap, 750 drivers x " + DriveLegs + " road legs: " + updates.Count + " updates over 4 swaps, slowest "
            + updates[^1].ToString("0.00") + " ms (.NET Debug; whole swap median gate 25 ms)");
        Assert.True(sliced.NetworkPreparationUpdates > 4, "preparation updates " + sliced.NetworkPreparationUpdates);
        PerfGate.Under(updates[^1], 2 * ClockRetimeStages.SliceMilliseconds, "slowest network preparation update (.NET)");
    }

    [Fact]
    public void TheNetworkSwapRunsOnAnUpdateOfItsOwnAfterTheLastPreparationSlice()
    {
        var networks = new[] { Road(), Road() };
        var wholePlanner = new SwappablePlanner { TrafficNetwork = networks[0] };
        var slicedPlanner = new SwappablePlanner { TrafficNetwork = networks[0] };
        var whole = RoadCity(750, 2250, wholePlanner);
        var sliced = RoadCity(750, 2250, slicedPlanner);
        whole.RefreshTrafficClosures();
        while (!sliced.TryRefreshTrafficClosures(ClockRetimeStages.SliceMilliseconds)) { }
        long deferredBefore = sliced.NetworkSwapsDeferred;
        double slowestPreparation = 0, slowestSwap = 0;
        for (int swap = 1; swap <= 6; swap++)
        {
            wholePlanner.TrafficNetwork = slicedPlanner.TrafficNetwork = networks[swap % 2];
            whole.RefreshTrafficClosures();
            while (true)
            {
                int preparing = sliced.NetworkPreparationUpdates;
                long started = Stopwatch.GetTimestamp();
                bool done = sliced.TryRefreshTrafficClosures(ClockRetimeStages.SliceMilliseconds);
                double ms = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
                if (done) { Assert.Equal(preparing, sliced.NetworkPreparationUpdates); slowestSwap = Math.Max(slowestSwap, ms); break; }
                slowestPreparation = Math.Max(slowestPreparation, ms);
            }
            Assert.Equal(whole.QueuedPersonEvents, sliced.QueuedPersonEvents);
            Assert.Equal(SaveCodec.Write(whole.State), SaveCodec.Write(sliced.State));
        }
        Assert.Equal(6, sliced.NetworkSwapsDeferred - deferredBefore);
        output.WriteLine("network swap on its own update, 750 drivers x " + DriveLegs + " road legs, 2,250 resting, 14,000 links: slowest preparation update "
            + slowestPreparation.ToString("0.00") + " ms, slowest swap update " + slowestSwap.ToString("0.00") + " ms (.NET)");
        PerfGate.Under(slowestPreparation, 2 * ClockRetimeStages.SliceMilliseconds, "slowest network preparation update (.NET)");
        PerfGate.Under(slowestSwap, 2 * ClockRetimeStages.SliceMilliseconds, "network swap update without a preparation slice (.NET)");
    }

    [Fact]
    public void NetworkPreparationGetsOnlyWhatIsLeftOfTheUpdateTargetAndStillMatchesAWholeSwap()
    {
        var budget = new UpdateCityBudget();
        Assert.Equal(UpdateCityBudget.DefaultTargetMilliseconds - UpdateCityBudget.InitialReserveMilliseconds, budget.Slice(0, ClockRetimeStages.SliceMilliseconds), 9);
        Assert.Equal(UpdateCityBudget.MinimumSliceMilliseconds, budget.Slice(3.5, ClockRetimeStages.SliceMilliseconds), 9);
        Assert.Equal(UpdateCityBudget.MinimumSliceMilliseconds, budget.Slice(double.NaN, 1), 9);
        budget.ObservePhysical(2);
        Assert.Equal(UpdateCityBudget.MinimumSliceMilliseconds, budget.Slice(1, ClockRetimeStages.SliceMilliseconds), 9);
        Assert.Throws<ArgumentOutOfRangeException>(() => budget.Slice(0, 0.5));
        var networks = new[] { Road(), Road() };
        var wholePlanner = new SwappablePlanner { TrafficNetwork = networks[0] };
        var slicedPlanner = new SwappablePlanner { TrafficNetwork = networks[0] };
        var whole = RoadCity(750, 2250, wholePlanner);
        var sliced = RoadCity(750, 2250, slicedPlanner);
        whole.RefreshTrafficClosures(); sliced.RefreshTrafficClosures();
        var updates = new List<double>();
        for (int swap = 1; swap <= 2; swap++)
        {
            wholePlanner.TrafficNetwork = slicedPlanner.TrafficNetwork = networks[swap % 2];
            whole.RefreshTrafficClosures();
            while (true)
            {
                long started = Stopwatch.GetTimestamp();
                bool done = sliced.TryRefreshTrafficClosures(budget.Slice(3, ClockRetimeStages.SliceMilliseconds));
                updates.Add((Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency);
                if (done) break;
            }
            Assert.Equal(whole.QueuedPersonEvents, sliced.QueuedPersonEvents);
            Assert.Equal(SaveCodec.Write(whole.State), SaveCodec.Write(sliced.State));
        }
        updates.Sort();
        output.WriteLine("network swap on a 1 ms remainder: " + updates.Count + " updates over 2 swaps, slowest " + updates[^1].ToString("0.00") + " ms (.NET Debug)");
        PerfGate.Under(updates[^1], 2 * UpdateCityBudget.MinimumSliceMilliseconds + 1, "slowest network preparation update on a 1 ms remainder (.NET)");
    }

    [Fact]
    public void ValidatingLiveRigScaleDriversOnASwappedNetworkIsLinearAndAllocatesNothingAfterWarmUp()
    {
        const int drivers = 300, roadLegs = 480;
        var network = Road();
        double edgeMinutes = network.Links[0].FreeFlowMinutes / 4;
        var journeys = new List<(Route Journey, List<TrafficRouteLeg> Spans, double Progress)>();
        for (int i = 0; i < drivers; i++)
        {
            int start = i * 37 % (RoadNodes - roadLegs - 1) / 4 * 4;
            var route = new Route();
            route.Legs.Add(new RouteLeg { From = new Position(-4, 0, 0), To = new Position(start * 12, 0, 0), Minutes = 2, Mode = TravelMode.Walk });
            for (int k = start; k < start + roadLegs; k++)
                route.Legs.Add(new RouteLeg { From = new Position(k * 12, 0, 0), To = new Position((k + 1) * 12, 0, 0), Minutes = edgeMinutes, Mode = TravelMode.Car,
                    ServiceId = "native:road:v1:" + Native + ":" + k + ":" + (k + 1) });
            route.Legs.Add(new RouteLeg { From = new Position((start + roadLegs) * 12, 0, 0), To = new Position(-8, 0, 0), Minutes = 3, Mode = TravelMode.Walk });
            route = route.Commit();
            Assert.True(network.TryMap(route, out var spans));
            journeys.Add((route, spans, 2 + edgeMinutes * (i % (roadLegs - 4)) + edgeMinutes / 3));
        }
        foreach (var j in journeys) network.ValidateRemainingMapping(j.Journey, j.Spans, j.Progress);
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        long started = Stopwatch.GetTimestamp();
        foreach (var j in journeys) network.ValidateRemainingMapping(j.Journey, j.Spans, j.Progress);
        double elapsed = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
        bytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
        double slowest = 0;
        foreach (var j in journeys)
        {
            long one = Stopwatch.GetTimestamp();
            network.ValidateRemainingMapping(j.Journey, j.Spans, j.Progress);
            slowest = Math.Max(slowest, (Stopwatch.GetTimestamp() - one) * 1000.0 / Stopwatch.Frequency);
        }
        output.WriteLine("validate " + drivers + " live drivers x " + roadLegs + " road legs (" + journeys[0].Spans.Count + " spans each): "
            + elapsed.ToString("0.00") + " ms in total, slowest driver " + slowest.ToString("0.000") + " ms, " + bytes + " B (.NET)");
        Assert.Equal(0, bytes);
        PerfGate.Under(slowest, ClockRetimeStages.SliceMilliseconds, "one live driver of 480 legs, the unit a sliced swap validates per step (.NET)");
        var broken = journeys[0].Spans.Select(s => new TrafficRouteLeg { LinkId = s.LinkId, StartNominalMinutes = s.StartNominalMinutes,
            EndNominalMinutes = s.EndNominalMinutes, FreeFlowMinutes = s.FreeFlowMinutes * 1.01, StartLinkFraction = s.StartLinkFraction, EndLinkFraction = s.EndLinkFraction }).ToList();
        Assert.Throws<ArgumentException>(() => network.ValidateRemainingMapping(journeys[0].Journey, broken, journeys[0].Progress));
    }

    [Fact]
    public void AStaleQueueEntryTakesTheFullRebuildAndMatchesASteadyCity()
    {
        var steadyPlanner = new SwappablePlanner();
        var swappedPlanner = new SwappablePlanner();
        var steady = City(40, 60, steadyPlanner);
        var swapped = City(40, 60, swappedPlanner);
        steady.AdvanceTo(1441.7);
        swapped.AdvanceTo(1441.7);
        steady.RefreshTrafficClosures();
        swapped.RefreshTrafficClosures();
        int queued = swapped.QueuedPersonEvents;
        var simulator = typeof(CitySimulator);
        var dueType = simulator.GetNestedType("Due", BindingFlags.NonPublic)!;
        var stale = Activator.CreateInstance(dueType, 0)!;
        dueType.GetField("Citizen")!.SetValue(stale, new CitizenState { Identity = new CitizenIdentity { Id = "requeue-stale" } });
        dueType.GetField("Minute")!.SetValue(stale, 1442.0);
        var due = simulator.GetField("due", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(swapped)!;
        Assert.True((bool)due.GetType().GetMethod("Add")!.Invoke(due, new[] { stale })!);
        Assert.Equal(queued + 1, swapped.QueuedPersonEvents);
        swappedPlanner.TrafficNetwork = Network();
        swapped.RefreshTrafficClosures();
        Assert.Equal(queued, swapped.QueuedPersonEvents);
        Assert.Equal(steady.QueuedPersonEvents, swapped.QueuedPersonEvents);
        steady.AdvanceTo(3600);
        swapped.AdvanceTo(3600);
        Assert.Equal(SaveCodec.Write(steady.State), SaveCodec.Write(swapped.State));
    }
}
