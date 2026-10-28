using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

/// <summary>
/// The runtime replaces the road snapshot while a route job is suspended and refreshes traffic closures
/// before planning resumes, so EnsureTrafficNetwork rebuilds the scheduler queue with an event in flight.
/// That event must stay out of the rebuilt queue until CompleteEvent or AbortPending re-adds it; queued
/// twice, its sort key changed inside the set, duplicating the resident and moving the clock backwards.
/// Adapted from an independent reviewer's reproduction of the 27 October resumable planning change.
/// </summary>
public sealed class PendingEventQueueTests
{
    private readonly ITestOutputHelper output;
    public PendingEventQueueTests(ITestOutputHelper output) { this.output = output; }

    private static TrafficNetwork Network() => new(new string('a', 64), Enumerable.Range(0, 5).Select(i =>
        new TrafficNetworkNode { Id = i, Position = new Position(i * 12, 0, 0), KilometresPerHour = 36,
            AllowsCar = true, Boundary = true, Successors = i < 4 ? new[] { i + 1 } : Array.Empty<int>() }).ToArray(), 1);

    private sealed class Planner : IResumableRoutePlanner, ITrafficRoutePlanner
    {
        public TrafficNetwork? TrafficNetwork { get; set; } = Network();
        public int ContinuesPerJob = 3;
        public bool ThrowNext;
        public bool IsTrafficLinkClosed(int id) => false;
        public void SetTrafficForecast(Func<int, double, TrafficLinkForecast>? forecast) { }
        public Route? Plan(Place a, Place b, double at, TravelPreferences p) => Walk(a, b);
        public static Route? Walk(Place a, Place b)
        {
            if (Position.Distance(a.Position, b.Position) < 0.001) return null;
            return new Route { Legs = { new RouteLeg { From = a.Position, To = b.Position, Mode = TravelMode.Walk,
                Minutes = Position.Distance(a.Position, b.Position) / 80 } } };
        }
        public IRoutePlanningJob Begin(RouteRequest request) => new Job(this, request);
        private sealed class Job : IRoutePlanningJob
        {
            private readonly Planner owner; private readonly RouteRequest request; private int left;
            public Job(Planner owner, RouteRequest request) { this.owner = owner; this.request = request; left = owner.ContinuesPerJob; }
            public Route? Result { get; private set; }
            public bool Continue(double budget)
            {
                if (owner.ThrowNext) { owner.ThrowNext = false; throw new InvalidOperationException("planner failure"); }
                if (--left > 0) return false;
                Result = Walk(request.From, request.To); return true;
            }
            public void Dispose() { }
        }
    }

    private static CityState City()
    {
        var ids = Enumerable.Range(0, 12).Select(i => new CitizenIdentity { Id = "rlo:t:c" + i.ToString("D2"), Name = "C" + i,
            Age = 25 + i, District = "a", Persona = (Persona)(i % 8), AppearanceSeed = i + 1 });
        var places = new[]
        {
            new Place { Id = "home", Name = "home", Kind = PlaceKind.Home, District = "a", Capacity = 20, Position = new(0, 0, 0) },
            new Place { Id = "work", Name = "work", Kind = PlaceKind.Work, District = "a", Capacity = 20, Position = new(300, 0, 400) },
            new Place { Id = "school", Name = "school", Kind = PlaceKind.Study, District = "a", Capacity = 20, Position = new(200, 0, -300) },
            new Place { Id = "shop", Name = "shop", Kind = PlaceKind.Shop, District = "a", Position = new(150, 0, 120), OpenMinute = 480, CloseMinute = 1200 },
            new Place { Id = "leisure", Name = "leisure", Kind = PlaceKind.Leisure, District = "a", Position = new(60, 0, 250), OpenMinute = 600, CloseMinute = 1380 },
        };
        return Population.Create(ids, places, "repro", "0.40.4", 5, 1440);
    }

    private static int DueCount(CitySimulator sim)
    {
        var field = typeof(CitySimulator).GetField("due", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return ((ICollection)field.GetValue(sim)!).Count;
    }

    private static void FrameUntilPending(CitySimulator sim, double target)
    {
        for (int i = 0; i < 100000 && !sim.PlanningPending; i++)
        {
            var r = sim.AdvanceFrameSlice(target, 1);
            if (r.CaughtUp && !sim.PlanningPending) throw new Exception("caught up without a request");
        }
        Assert.True(sim.PlanningPending);
    }

    [Fact]
    public void QueueRebuildWhilePendingLeavesTheInFlightEventForItsCommit()
    {
        var planner = new Planner(); var state = City(); var sim = new CitySimulator(state, planner);
        sim.RefreshTrafficClosures();
        int citizens = state.Citizens.Count;
        Assert.Equal(citizens, DueCount(sim));
        FrameUntilPending(sim, 1440 * 3);
        output.WriteLine("pending at " + sim.State.Minute + " due=" + DueCount(sim));
        Assert.Equal(citizens - 1, DueCount(sim));
        planner.TrafficNetwork = Network();      // replacement snapshot, same topology
        sim.RefreshTrafficClosures();            // the runtime does this every prepared frame
        output.WriteLine("after network swap due=" + DueCount(sim));
        Assert.Equal(citizens - 1, DueCount(sim));
        while (!sim.ContinuePlanning(0)) { }
        output.WriteLine("after commit due=" + DueCount(sim) + " citizens=" + citizens);
        Assert.Equal(citizens, DueCount(sim));
    }

    [Fact]
    public void PlannerFailureAfterAQueueRebuildRequeuesTheEventOnce()
    {
        var planner = new Planner(); var state = City(); var sim = new CitySimulator(state, planner);
        sim.RefreshTrafficClosures();
        FrameUntilPending(sim, 1440 * 3);
        planner.TrafficNetwork = Network();
        sim.RefreshTrafficClosures();
        planner.ThrowNext = true;
        var error = Assert.ThrowsAny<Exception>(() => sim.ContinuePlanning(0));
        output.WriteLine("exception: " + error.Message);
        Assert.Equal("planner failure", error.Message);
        Assert.False(sim.PlanningPending);
        Assert.Equal(state.Citizens.Count, DueCount(sim));
    }

    private static List<(double Minute, string Id)> DueOrder(CitySimulator sim)
    {
        var field = typeof(CitySimulator).GetField("due", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var list = new List<(double, string)>();
        foreach (var item in (IEnumerable)field.GetValue(sim)!)
        {
            var t = item.GetType();
            double m = (double)t.GetField("Minute")!.GetValue(item)!;
            var c = (CitizenState)t.GetField("Citizen")!.GetValue(item)!;
            list.Add((m, c.Identity.Id));
        }
        return list;
    }

    [Fact]
    public void QueueStaysOrderedAcrossRebuildsWhilePending()
    {
        var planner = new Planner(); var state = City(); var sim = new CitySimulator(state, planner);
        sim.RefreshTrafficClosures();
        double target = 1440 * 3; int violations = 0, swaps = 0;
        var observed = new List<string>();
        sim.ActivityObserved += (id, c, at) => observed.Add(id + "@" + at.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        for (int guard = 0; guard < 2_000_000; guard++)
        {
            if (sim.PlanningPending)
            {
                if (swaps < 40) { planner.TrafficNetwork = Network(); sim.RefreshTrafficClosures(); swaps++; }
                bool done = sim.ContinuePlanning(0);
                if (done)
                {
                    var order = DueOrder(sim);
                    for (int i = 1; i < order.Count; i++)
                    {
                        int cmp = order[i - 1].Minute.CompareTo(order[i].Minute);
                        if (cmp > 0 || (cmp == 0 && string.CompareOrdinal(order[i - 1].Id, order[i].Id) > 0))
                        { violations++; output.WriteLine("out of order after swap " + swaps + ": " + order[i - 1] + " before " + order[i] + " clock " + sim.State.Minute); break; }
                    }
                }
                continue;
            }
            var r = sim.AdvanceFrameSlice(target, 1);
            if (r.CaughtUp) break;
        }
        output.WriteLine("swaps=" + swaps + " violations=" + violations + " events=" + observed.Count);
        Assert.Equal(0, violations);
        Assert.Equal(40, swaps);
        // Observed events never go back in time.
        var minutes = observed.Select(o => double.Parse(o.Substring(o.IndexOf('@') + 1), System.Globalization.CultureInfo.InvariantCulture)).ToList();
        for (int i = 1; i < minutes.Count; i++) Assert.True(minutes[i - 1] <= minutes[i], "clock moved backwards at event " + i);
    }

    [Fact]
    public void RebuildsWhilePendingMatchTheUninterruptedRun()
    {
        (string Save, List<string> Events) Run(bool swap)
        {
            var planner = new Planner(); var state = City(); var sim = new CitySimulator(state, planner);
            var events = new List<string>();
            sim.ActivityObserved += (id, c, at) => events.Add(id + "@" + at.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ":" + c.Phase + ":" + c.PlaceId);
            sim.RefreshTrafficClosures();
            double target = 1440 * 3; int swaps = 0;
            for (int guard = 0; guard < 2_000_000; guard++)
            {
                if (sim.PlanningPending)
                {
                    // A replacement snapshot before every suspended request, the worst case for the queue rebuild.
                    if (swap) { planner.TrafficNetwork = Network(); sim.RefreshTrafficClosures(); swaps++; }
                    sim.ContinuePlanning(0); continue;
                }
                var r = sim.AdvanceFrameSlice(target, 1);
                if (r.CaughtUp) break;
            }
            Assert.True(sim.IsCaughtUpTo(target));
            Assert.True(!swap || swaps > 20, "too few swaps: " + swaps);
            sim.State.PrepareForSave();
            return (SaveCodec.Write(sim.State), events);
        }
        var reference = Run(false);
        (string Save, List<string> Events) swapped;
        try { swapped = Run(true); }
        catch (Exception e) { output.WriteLine("swapped run threw: " + e.GetType().Name + ": " + e.Message); throw; }
        Assert.Equal(reference.Events, swapped.Events);
        Assert.Equal(reference.Save, swapped.Save);
    }
}
