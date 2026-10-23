using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class TrafficClosureRefreshTests
{
    private class Planner : IRoutePlanner, ITrafficRoutePlanner
    {
        public TrafficNetwork? TrafficNetwork { get; } = new(new string('a', 64), Enumerable.Range(0, 5).Select(i =>
            new TrafficNetworkNode { Id = i, Position = new Position(i * 12, 0, 0), KilometresPerHour = 36,
                AllowsCar = true, Boundary = true, Successors = i < 4 ? new[] { i + 1 } : Array.Empty<int>() }).ToArray(), 1);
        public HashSet<int> Closed = new();
        public List<int> Reads = new();
        public Action? OnRead;
        public virtual bool IsTrafficLinkClosed(int id) { Reads.Add(id); OnRead?.Invoke(); return Closed.Contains(id); }
        public void SetTrafficForecast(Func<int, double, TrafficLinkForecast>? forecast) { }
        public Route? Plan(Place a, Place b, double at, TravelPreferences preferences) => null;
    }
    private sealed class BatchPlanner : Planner, ITrafficClosureBatchPlanner
    {
        public int Begins, Ends;
        public bool Active, FailBegin, FailEnd;
        public void BeginTrafficClosureBatch()
        {
            Assert.False(Active); Begins++;
            if (FailBegin) throw new InvalidOperationException("begin");
            Active = true;
        }
        public void EndTrafficClosureBatch()
        { Assert.True(Active); Active = false; Ends++; if (FailEnd) throw new InvalidOperationException("end"); }
        public override bool IsTrafficLinkClosed(int id) { Assert.True(Active); return base.IsTrafficLinkClosed(id); }
    }
    private static CitySimulator Engine(Planner planner)
    {
        var state = Population.Create(new[] { new CitizenIdentity { Id = "resident", Name = "Resident", Age = 30,
            AppearanceSeed = 1, Persona = Persona.Commuter } }, new[] {
            new Place { Id = "home", Kind = PlaceKind.Home, Capacity = 1, Position = new Position(0,0,0) } },
            "closure-test", "0.40.4", 7, 1440);
        var engine = new CitySimulator(state, planner);
        engine.RefreshTrafficClosures(); planner.Reads.Clear();
        return engine;
    }
    private static List<TrafficRouteLeg> Route(params int[] ids) => ids.Select((id, i) =>
        new TrafficRouteLeg { LinkId = id, StartNominalMinutes = i, EndNominalMinutes = i + 1 }).ToList();
    private static string State(TrafficKernel traffic) => JsonSerializer.Serialize(traffic.Capture());

    [Fact]
    public void RelevantIdsAreSortedUniqueDetachedAndIncludeDormantReceivers()
    {
        var kernel = new TrafficKernel(new[] { 9, 2, 5, 17 }.Select(id => new TrafficLinkDefinition
            { Id = id, Capacity = 1, FreeFlowMinutes = 1, HeadwayMinutes = 0 }));
        kernel.Enqueue("a", Route(9, 2)); kernel.Enqueue("b", Route(5, 2));
        kernel.SetLinkClosed(2, true); kernel.SetLinkClosed(17, true);
        kernel.AdvanceTo(100, 100);
        var detached = kernel.RelevantLinkIds;
        Assert.Equal(new[] { 2, 5, 9, 17 }, detached);
        Assert.Null(kernel.NextEventTime);
        kernel.SetLinkClosed(17, false);
        Assert.Equal(new[] { 2, 5, 9, 17 }, detached);
        Assert.Equal(new[] { 2, 5, 9 }, kernel.RelevantLinkIds);
    }

    [Fact]
    public void BatchAndLegacyRefreshHaveIdenticalOrderedStateThroughCloseReopenAndRestore()
    {
        var old = new Planner(); var batched = new BatchPlanner();
        var a = Engine(old); var b = Engine(batched);
        foreach (var engine in new[] { a, b })
        {
            engine.Traffic!.SetLinkClosed(2, true);
            for (int i = 0; i < 12; i++) engine.Traffic.Enqueue("fixture" + i, Route(0, 1, 2, 3));
            engine.Traffic.AdvanceTo(1445, 100);
        }
        for (int pass = 0; pass < 20; pass++)
        {
            old.Closed.Clear(); if (pass % 3 == 0) old.Closed.Add(pass % 4);
            batched.Closed = new HashSet<int>(old.Closed);
            old.Reads.Clear(); batched.Reads.Clear();
            a.RefreshTrafficClosures(); b.RefreshTrafficClosures();
            Assert.Equal(old.Reads, batched.Reads);
            Assert.Equal(old.Reads.OrderBy(id => id).Distinct(), old.Reads);
            Assert.Equal(State(a.Traffic!), State(b.Traffic!));
            Assert.Equal(State(TrafficKernel.Restore(a.Traffic!.Capture())), State(TrafficKernel.Restore(b.Traffic!.Capture())));
            Assert.False(batched.Active);
        }
        Assert.Equal(batched.Begins, batched.Ends);
    }

    [Theory]
    [InlineData("begin")]
    [InlineData("read")]
    [InlineData("end")]
    [InlineData("nested")]
    public void FailedRefreshReleasesItsScopeAndScratchForTheNextCall(string failure)
    {
        var planner = new BatchPlanner(); var engine = Engine(planner);
        engine.Traffic!.SetLinkClosed(1, true);
        planner.FailBegin = failure == "begin"; planner.FailEnd = failure == "end";
        if (failure == "read") planner.OnRead = () => throw new InvalidOperationException("read");
        if (failure == "nested") planner.OnRead = engine.RefreshTrafficClosures;
        int ends = planner.Ends;
        Assert.Throws<InvalidOperationException>(engine.RefreshTrafficClosures);
        Assert.False(planner.Active);
        Assert.Equal(ends + (failure == "begin" ? 0 : 1), planner.Ends);
        planner.FailBegin = planner.FailEnd = false; planner.OnRead = null; planner.Reads.Clear();
        engine.Traffic.SetLinkClosed(1, true); // End may have failed after the observation was already applied.
        engine.RefreshTrafficClosures();
        Assert.Equal(new[] { 1 }, planner.Reads);
        Assert.False(engine.Traffic.Forecast(1, engine.State.Minute).Closed);
        planner.Reads.Clear(); engine.RefreshTrafficClosures(); Assert.Empty(planner.Reads);
    }
}
