using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

public sealed class Gr5NetworkFootprintTests
{
    private readonly ITestOutputHelper output;
    public Gr5NetworkFootprintTests(ITestOutputHelper output) { this.output = output; }

    private static string M(long bytes) => (bytes / 1e6).ToString("0.000");

    private void Print(string title, RetainedHeap heap, int links)
    {
        output.WriteLine(title + ": " + heap.Objects + " objects (" + (heap.Objects / (double)links).ToString("0.00") + " per link), "
            + M(heap.Bytes) + " MB (" + (heap.Bytes / (double)links).ToString("0") + " B per link), Mono 64-bit layout model");
        foreach (var row in heap.Types.Take(10)) output.WriteLine("  " + row.Name + ": " + row.Objects + " objects, " + M(row.Bytes) + " MB");
    }

    private static long Definitions(RetainedHeap heap) => heap.Types.Where(r => r.Name == nameof(TrafficLinkDefinition) || r.Name == "FrozenLink").Sum(r => r.Objects);

    [Fact]
    public void ARigScaleNetworkKeepsAboutOneObjectPerLink()
    {
        var network = Gr5NetworkRequeueTests.Road();
        int links = network.Links.Count;
        var heap = new RetainedHeap();
        heap.Add("network", network);
        Print("14,000-link network", heap, links);
        Assert.Equal(links, Definitions(heap));
        Assert.True(heap.Objects < 2.2 * links, "objects " + heap.Objects);
        Assert.True(heap.Bytes < 1_000L * links, "bytes " + heap.Bytes);
    }

    [Fact]
    public void ARatedCopySharesTheGeometryAndNodeLists()
    {
        var nodes = Gr5RetimeReuseTests.Grid(40, 1);
        var network = new TrafficNetwork("gr5-footprint", nodes, 1);
        var signals = Gr5RetimeReuseTests.Grid(40, 2).ToDictionary(n => n.Id, n => n.Signal);
        var rated = network.Rerated(2, id => signals[id]);
        int links = network.Links.Count;
        var heap = new RetainedHeap();
        heap.Add("network", network);
        var extra = heap.Add("rated", rated);
        Print("grid network plus one rated copy, " + links + " links", heap, links);
        output.WriteLine("rated copy adds " + extra.Objects + " objects, " + M(extra.Bytes) + " MB");
        Gr5RetimeReuseTests.AssertSame(new TrafficNetwork("gr5-footprint", Gr5RetimeReuseTests.Grid(40, 2), 2), rated, nodes);
        Assert.True(extra.Objects < 2.5 * links, "rated objects " + extra.Objects);
    }

    [Fact]
    public void TheLiveKernelUsesTheNetworkDefinitionsInsteadOfACopy()
    {
        var network = Gr5NetworkRequeueTests.Road();
        var planner = new Gr5NetworkRequeueTests.SwappablePlanner { TrafficNetwork = network };
        var city = Gr5NetworkRequeueTests.RoadCity(750, 2250, planner);
        city.RefreshTrafficClosures();
        var kernel = city.Traffic!;
        Assert.Equal(network.Links.Count, kernel.DefinitionsSharedWith(network.Links));
        var heap = new RetainedHeap();
        heap.Exclude(city);
        heap.Exclude(city.State);
        heap.Add("network", network);
        var own = heap.Add("kernel", kernel);
        Print("network plus live kernel, 750 drivers", heap, network.Links.Count);
        output.WriteLine("kernel own: " + own.Objects + " objects, " + M(own.Bytes) + " MB");
        Assert.Equal(network.Links.Count, Definitions(heap));
    }

    [Fact]
    public void AKernelRestoredFromASaveAdoptsIdenticalNetworkDefinitions()
    {
        var network = Gr5NetworkRequeueTests.Road();
        var planner = new Gr5NetworkRequeueTests.SwappablePlanner { TrafficNetwork = network };
        var city = Gr5NetworkRequeueTests.RoadCity(750, 2250, planner);
        city.RefreshTrafficClosures();
        city.AdvanceTo(city.State.Minute + 30, int.MaxValue);
        string payload = SaveCodec.Write(city.State);
        var read = SaveCodec.Read(payload);
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var reloaded = new CitySimulator(read.State!, new Gr5NetworkRequeueTests.SwappablePlanner { TrafficNetwork = network });
        reloaded.RefreshTrafficClosures();
        Assert.Equal(network.Links.Count, reloaded.Traffic!.DefinitionsSharedWith(network.Links));
        Assert.Equal(payload, SaveCodec.Write(reloaded.State));
    }

    [Fact]
    public void ADifferentNetworkDefinitionIsNotAdopted()
    {
        var network = Gr5NetworkRequeueTests.Road();
        var kernel = TrafficKernel.SharingDefinitions(network.Links, 0);
        var nodes = Gr5NetworkRequeueTests.RoadNodesForTest();
        nodes[22].Position = new Position(nodes[22].Position.X, 0, 1e-4);
        var nudged = new TrafficNetwork("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", nodes, 1);
        int differing = Enumerable.Range(0, network.Links.Count).Count(i => network.Links[i].FreeFlowMinutes != nudged.Links[i].FreeFlowMinutes);
        Assert.True(differing >= 1);
        Assert.True(kernel.AdoptCompatibleDefinitions(nudged.Links));
        Assert.Equal(nudged.Links.Count - differing, kernel.DefinitionsSharedWith(nudged.Links));
        Assert.Equal(differing, kernel.DefinitionsSharedWith(network.Links));
    }

    [Fact]
    public void SharedDefinitionsAreNotChangedByTrafficOrARetime()
    {
        var network = Gr5NetworkRequeueTests.Road();
        var before = network.Links.Select(l => (l.Capacity, BitConverter.DoubleToInt64Bits(l.FreeFlowMinutes), BitConverter.DoubleToInt64Bits(l.HeadwayMinutes))).ToList();
        var planner = new Gr5NetworkRequeueTests.SwappablePlanner { TrafficNetwork = network };
        var city = Gr5NetworkRequeueTests.RoadCity(750, 250, planner);
        city.RefreshTrafficClosures();
        city.AdvanceTo(city.State.Minute + 120, int.MaxValue);
        _ = city.Traffic!.RescaleTime(2);
        _ = city.Traffic!.Capture();
        var after = network.Links.Select(l => (l.Capacity, BitConverter.DoubleToInt64Bits(l.FreeFlowMinutes), BitConverter.DoubleToInt64Bits(l.HeadwayMinutes))).ToList();
        Assert.Equal(before, after);
    }

    [Fact]
    public void AKernelRetimedByAPowerOfTwoSharesTheRatedNetworkDefinitions()
    {
        var network = Gr5NetworkRequeueTests.Road();
        var planner = new Gr5NetworkRequeueTests.SwappablePlanner { TrafficNetwork = network };
        var city = Gr5NetworkRequeueTests.RoadCity(750, 250, planner);
        city.RefreshTrafficClosures();
        city.AdvanceTo(city.State.Minute + 30, int.MaxValue);
        var rated = network.Rerated(2, _ => null);
        var doubled = city.Traffic!.RescaleTime(2);
        Assert.True(doubled.AdoptCompatibleDefinitions(rated.Links));
        Assert.Equal(rated.Links.Count, doubled.DefinitionsSharedWith(rated.Links));
        var back = doubled.RescaleTime(0.5);
        Assert.True(back.AdoptCompatibleDefinitions(network.Links));
        Assert.Equal(network.Links.Count, back.DefinitionsSharedWith(network.Links));
        var third = back.RescaleTime(3);
        Assert.True(third.AdoptCompatibleDefinitions(network.Rerated(3, _ => null).Links));
    }

    private static TrafficKernel QueuedOnSignal(TrafficNetwork network, out TrafficLinkDefinition signalled)
    {
        signalled = network.Links.First(l => l.Signal != null);
        var kernel = TrafficKernel.SharingDefinitions(network.Links, 0);
        for (int i = 0; i < 1; i++)
            kernel.Enqueue("queued" + i, new List<TrafficRouteLeg> { new TrafficRouteLeg
            { LinkId = signalled.Id, StartNominalMinutes = 0, EndNominalMinutes = signalled.FreeFlowMinutes, FreeFlowMinutes = signalled.FreeFlowMinutes } });
        kernel.AdvanceTo(signalled.FreeFlowMinutes * 0.25, int.MaxValue);
        return kernel;
    }

    [Fact]
    public void ADefinitionSharedWithAQueuedKernelCannotBeEdited()
    {
        var network = new TrafficNetwork("gr5-frozen", Gr5RetimeReuseTests.Grid(12, 1), 1);
        var kernel = QueuedOnSignal(network, out var signalled);
        Assert.Equal(network.Links.Count, kernel.DefinitionsSharedWith(network.Links));
        int capacity = signalled.Capacity;
        double phase = signalled.Signal!.PhaseOffsetMinutes;
        string before = Describe(kernel.Capture());
        Assert.Throws<InvalidOperationException>(() => signalled.Capacity = 0);
        Assert.Throws<InvalidOperationException>(() => signalled.FreeFlowMinutes = 0.001);
        Assert.Throws<InvalidOperationException>(() => signalled.Signal = null);
        Assert.Throws<InvalidOperationException>(() => signalled.Signal!.PhaseOffsetMinutes = phase + 5);
        Assert.Throws<InvalidOperationException>(() => signalled.Signal!.GreenDurationMinutes = 0);
        Assert.Equal(capacity, signalled.Capacity);
        Assert.Equal(phase, signalled.Signal!.PhaseOffsetMinutes);
        var captured = kernel.Capture();
        Assert.Equal(before, Describe(captured));
        var restored = TrafficKernel.Restore(captured);
        Assert.True(restored.HasCompatibleDefinitions(network.Links));
        Assert.True(kernel.AdoptCompatibleDefinitions(network.Links));
    }

    private static string Describe(TrafficKernelState state) => string.Join(";", state.Vehicles.Select(v => v.VehicleId + ":" + v.RouteIndex + ":" + v.NominalMinutes.ToString("R")))
        + "|" + string.Join(";", state.Events.Select(e => e.LinkId + "@" + e.At.ToString("R")))
        + "|" + string.Join(";", state.Definitions.Select(d => d.Id + ":" + d.Capacity + ":" + d.FreeFlowMinutes.ToString("R") + ":" + (d.Signal?.PhaseOffsetMinutes.ToString("R") ?? "-")));

    [Fact]
    public void AKernelBuiltFromEditableDefinitionsKeepsItsOwnCopies()
    {
        var editable = new TrafficNetwork("gr5-frozen", Gr5RetimeReuseTests.Grid(6, 1), 1).Links
            .Select(l => new TrafficLinkDefinition { Id = l.Id, Capacity = l.Capacity, FreeFlowMinutes = l.FreeFlowMinutes, HeadwayMinutes = l.HeadwayMinutes }).ToList();
        var kernel = TrafficKernel.SharingDefinitions(editable, 0);
        Assert.Equal(0, kernel.DefinitionsSharedWith(editable));
        Assert.True(kernel.AdoptCompatibleDefinitions(editable));
        Assert.Equal(0, kernel.DefinitionsSharedWith(editable));
        editable[0].Capacity = 99;
        Assert.False(kernel.HasCompatibleDefinitions(editable));
    }

    [Fact]
    public void AdoptionRebindsWarmForecastProjectionsAndKeepsThemAllocationFree()
    {
        var first = Gr5NetworkRequeueTests.Road();
        var planner = new Gr5NetworkRequeueTests.SwappablePlanner { TrafficNetwork = first };
        var city = Gr5NetworkRequeueTests.RoadCity(750, 250, planner);
        city.RefreshTrafficClosures();
        city.AdvanceTo(city.State.Minute + 3, int.MaxValue);
        var kernel = city.Traffic!;
        double minute = city.State.Minute;
        void Forecasts() { for (int link = 0; link < first.Links.Count; link += 7) kernel.ForecastEntry(link, minute + 0.5, 0.4, 0.6); }
        Forecasts(); Forecasts();
        Assert.Equal(0, kernel.ProjectionDefinitionsNotLive());
        var second = Gr5NetworkRequeueTests.Road();
        planner.TrafficNetwork = second;
        city.RefreshTrafficClosures();
        Assert.Same(kernel, city.Traffic);
        Assert.Equal(second.Links.Count, kernel.DefinitionsSharedWith(second.Links));
        Assert.Equal(0, kernel.ProjectionDefinitionsNotLive());
        long started = GC.GetAllocatedBytesForCurrentThread();
        Forecasts();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - started;
        var heap = new RetainedHeap();
        heap.Exclude(city); heap.Exclude(city.State); heap.Exclude(planner);
        heap.Add("network", second);
        heap.Add("kernel", kernel);
        output.WriteLine("after a compatible swap: " + Definitions(heap) + " definitions reachable from the new network and the kernel; warm forecasts allocated " + allocated + " B (.NET)");
        Assert.Equal(second.Links.Count, Definitions(heap));
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void ColdNetworkConstructionAllocatesNoMoreThanTheUnflattenedLayout()
    {
        var nodes = Gr5NetworkRequeueTests.RoadNodesForTest();
        _ = new TrafficNetwork("gr5-cold", nodes, 1);
        long started = GC.GetAllocatedBytesForCurrentThread();
        var network = new TrafficNetwork("gr5-cold", nodes, 1);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - started;
        var heap = new RetainedHeap();
        heap.Add("network", network);
        output.WriteLine(network.Links.Count + "-link cold construction allocated " + M(allocated) + " MB, retained " + M(heap.Bytes) + " MB (.NET allocation, Mono layout model for retained)");
        Assert.True(allocated < 107_400_000, "allocated " + allocated + " B; the unflattened layout at aeb8974a allocated 107,488,920 B for this fixture (.NET 10)");
    }

    [Fact]
    public void AnUnwarmedRateCacheGrowsByLessThanTheCensusAllowanceThroughAOneTwoOneRetime()
    {
        const long allowance = 1_000_000;
        var nodes = Gr5RetimeReuseTests.Grid(42, 1);
        var one = new TrafficNetwork("gr5-rate-allowance", nodes, 1);
        var doubled = Gr5RetimeReuseTests.Grid(42, 2).ToDictionary(n => n.Id, n => n.Signal);
        var cache = new TrafficRateCache();
        cache.Remember(1, one);
        var kernel = TrafficKernel.SharingDefinitions(one.Links, 600);
        var before = new RetainedHeap();
        long cacheBefore = before.Add("cache", cache).Bytes;
        long kernelBefore = before.Add("kernel", kernel).Bytes;
        Assert.False(cache.TryGet(2, out _));
        var two = one.Rerated(2, id => doubled[id]);
        cache.Remember(2, two);
        var retimed = kernel.RescaleTime(2);
        Assert.True(retimed.AdoptCompatibleDefinitions(two.Links));
        Assert.True(cache.TryGet(1, out var back));
        Assert.Same(one, back);
        var restored = retimed.RescaleTime(0.5);
        Assert.True(restored.AdoptCompatibleDefinitions(one.Links));
        var after = new RetainedHeap();
        long cacheAfter = after.Add("cache", cache).Bytes;
        long kernelAfter = after.Add("kernel", restored).Bytes;
        int signals = one.Links.Count(l => l.Signal != null);
        output.WriteLine(one.Links.Count + " links (" + signals + " signalled), unwarmed cache through 1->2->1: cache +" + (cacheAfter - cacheBefore)
            + " B, kernel +" + (kernelAfter - kernelBefore) + " B, total +" + (cacheAfter - cacheBefore + kernelAfter - kernelBefore) + " B (Mono 64-bit layout model)");
        Assert.True(one.Links.Count >= 13_965, "links " + one.Links.Count);
        Assert.True(cacheAfter - cacheBefore < allowance, "cache growth " + (cacheAfter - cacheBefore));
        Assert.True(cacheAfter - cacheBefore + kernelAfter - kernelBefore < allowance, "cache and kernel growth " + (cacheAfter - cacheBefore + kernelAfter - kernelBefore));
    }

    private static List<TrafficNetworkNode> SignalledRoad(double clockRate)
    {
        var nodes = Gr5NetworkRequeueTests.RoadNodesForTest();
        foreach (var node in nodes)
            if (node.Id % 40 == 0 && node.Id > 0)
                node.Signal = new TrafficSignalSchedule { CycleMinutes = 1.0 * clockRate, GreenStartMinutes = node.Id % 80 == 0 ? 0 : 0.5 * clockRate, GreenDurationMinutes = 0.45 * clockRate };
        return nodes;
    }

    private static CitySimulator FullRetime(CityState state, CitySimulator city, ClockRetimeDriver driver, double rate, TrafficNetwork network)
    {
        while (true)
        {
            var outcome = driver.Update(state, rate, false, 2, 0);
            Assert.NotEqual(ClockRetimeOutcome.Restarted, outcome);
            if (outcome != ClockRetimeOutcome.Committed) continue;
            var rebuild = city.BeginRebuildRoutes(new Gr5NetworkRequeueTests.SwappablePlanner { TrafficNetwork = network }, driver.TakeRetimedTraffic());
            while (!rebuild.Step(0)) { }
            var next = rebuild.Result!;
            next.RefreshTrafficClosures();
            return next;
        }
    }

    private static RetainedHeap Census(CitySimulator city, TrafficRateCache cache)
    {
        var heap = new RetainedHeap();
        heap.Add("simulation", city);
        heap.Add("cache", cache);
        return heap;
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.0000001064368653)]
    public void AFullOneTwoOneRetimeWithAnUnwarmedCacheAndPlayAtTwoTimesStaysUnderTheCensusAllowance(double returnRate)
    {
        const long allowance = 1_000_000;
        const string native = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var one = new TrafficNetwork(native, SignalledRoad(1), 1);
        var doubled = SignalledRoad(2).ToDictionary(n => n.Id, n => n.Signal);
        var planner = new Gr5NetworkRequeueTests.SwappablePlanner { TrafficNetwork = one };
        var city = Gr5NetworkRequeueTests.RoadCity(750, 2250, planner);
        var state = city.State;
        city.RefreshTrafficClosures();
        city.AdvanceTo(state.Minute + 2, int.MaxValue);
        var driver = new ClockRetimeDriver();
        var halved = SignalledRoad(0.5).ToDictionary(n => n.Id, n => n.Signal);
        city = FullRetime(state, city, driver, 0.5, one.Rerated(0.5, id => halved[id]));
        city.AdvanceTo(state.Minute + 0.05, int.MaxValue);
        city = FullRetime(state, city, driver, 1, one);
        city.AdvanceTo(state.Minute + 0.02, int.MaxValue);
        Assert.True(city.Traffic!.VehicleCount > 0);
        var cache = new TrafficRateCache();
        cache.Remember(1, one);
        var before = Census(city, cache);
        Assert.False(cache.TryGet(2, out _));
        var two = one.Rerated(2, id => doubled[id]);
        cache.Remember(2, two);
        city = FullRetime(state, city, driver, 2, two);
        Assert.Equal(two.Links.Count, city.Traffic!.DefinitionsSharedWith(two.Links) + two.Links.Count(l => l.Signal != null));
        city.AdvanceTo(state.Minute + 0.05, int.MaxValue);
        bool exact = returnRate == 1;
        Assert.Equal(exact, cache.TryGet(returnRate, out var back));
        if (!exact)
        {
            Assert.Equal(0, cache.Count);
            var returned = SignalledRoad(returnRate).ToDictionary(n => n.Id, n => n.Signal);
            back = two.Rerated(returnRate, id => returned[id]);
            cache.Remember(returnRate, back);
        }
        city = FullRetime(state, city, driver, returnRate, back);
        city.AdvanceTo(state.Minute + 0.01, int.MaxValue);
        var after = Census(city, cache);
        Assert.True(city.Traffic!.VehicleCount > 0);
        int signals = one.Links.Count(l => l.Signal != null);
        long growth = after.Bytes - before.Bytes;
        output.WriteLine(one.Links.Count + " links (" + signals + " signalled), 750 drivers, before read after a warm-up 1->0.5->1 retime, unwarmed rate-2 cache, retime 1->2, 0.05 game minutes at 2x, retime 2->" + returnRate.ToString("R") + ": simulation "
            + before.Roots[0].Bytes + " -> " + after.Roots[0].Bytes + " B, cache " + before.Roots[1].Bytes + " -> " + after.Roots[1].Bytes + " B, total growth " + growth
            + " B; " + city.Traffic!.VehicleCount + " vehicles in traffic; kernel shares " + city.Traffic!.DefinitionsSharedWith(back.Links) + " of " + back.Links.Count + " definitions with the returned network (Mono 64-bit layout model)");
        foreach (string name in new[] { "CitySimulator", "TrafficKernel", "TrafficJourneys", "ActiveJourneyIndex" })
            output.WriteLine("  count " + name + ": " + (before.Types.FirstOrDefault(r => r.Name == name)?.Objects ?? 0) + " -> " + (after.Types.FirstOrDefault(r => r.Name == name)?.Objects ?? 0));
        foreach (var row in after.Types.Take(12))
        {
            var prior = before.Types.FirstOrDefault(r => r.Name == row.Name);
            output.WriteLine("  " + row.Name + ": " + (prior?.Objects ?? 0) + " -> " + row.Objects + " objects, " + ((row.Bytes - (prior?.Bytes ?? 0)) / 1e6).ToString("+0.000;-0.000") + " MB");
        }
        Assert.True(growth < allowance, "retained growth " + growth);
    }

    [Theory]
    [InlineData(0.05, true)]
    [InlineData(0.25, true)]
    [InlineData(2.0, false)]
    public void ARetimedCityRetainsNoMoreThanAnUnretimedCityAtTheSameGameMinute(double playMinutes, bool sameState)
    {
        const long allowance = 1_000_000;
        const string native = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        (CityState State, CitySimulator City, TrafficNetwork One, TrafficRateCache Cache, double Start) Build()
        {
            var one = new TrafficNetwork(native, SignalledRoad(1), 1);
            var city = Gr5NetworkRequeueTests.RoadCity(750, 2250, new Gr5NetworkRequeueTests.SwappablePlanner { TrafficNetwork = one });
            city.RefreshTrafficClosures();
            city.AdvanceTo(city.State.Minute + 2, int.MaxValue);
            var cache = new TrafficRateCache();
            cache.Remember(1, one);
            return (city.State, city, one, cache, city.State.Minute);
        }
        var retimed = Build();
        var control = Build();
        Assert.Equal(retimed.Start, control.Start);
        var before = Census(retimed.City, retimed.Cache);
        var controlBefore = Census(control.City, control.Cache);
        Assert.Equal(controlBefore.Bytes, before.Bytes);
        var driver = new ClockRetimeDriver();
        var doubled = SignalledRoad(2).ToDictionary(n => n.Id, n => n.Signal);
        var two = retimed.One.Rerated(2, id => doubled[id]);
        var city = FullRetime(retimed.State, retimed.City, driver, 2, two);
        city.AdvanceTo(retimed.Start + playMinutes, int.MaxValue);
        Assert.True(retimed.Cache.TryGet(1, out var back));
        city = FullRetime(retimed.State, city, driver, 1, back);
        double end = retimed.State.Minute + 0.01;
        city.AdvanceTo(end, int.MaxValue);
        control.City.AdvanceTo(end, int.MaxValue);
        Assert.Equal(end, control.State.Minute);
        var after = Census(city, retimed.Cache);
        var controlAfter = Census(control.City, control.Cache);
        long excess = after.Bytes - controlAfter.Bytes;
        output.WriteLine("750 drivers, " + retimed.One.Links.Count + " links; retime 1->2, " + playMinutes.ToString("R") + " game minutes at 2x, retime 2->1, 0.01 minutes; control plays the same minutes at 1x. Retimed growth "
            + (after.Bytes - before.Bytes) + " B, control growth " + (controlAfter.Bytes - controlBefore.Bytes) + " B, retimed minus control " + excess + " B (Mono 64-bit layout model)");
        foreach (var row in after.Types.Take(12))
        {
            var other = controlAfter.Types.FirstOrDefault(r => r.Name == row.Name);
            output.WriteLine("  " + row.Name + ": control " + (other?.Objects ?? 0) + " / retimed " + row.Objects + " objects, " + ((row.Bytes - (other?.Bytes ?? 0)) / 1e6).ToString("+0.000;-0.000") + " MB");
        }
        Assert.Equal(1, after.Types.First(r => r.Name == "CitySimulator").Objects);
        Assert.All(city.State.Citizens.Where(c => c.Phase == CitizenPhase.Travelling), c => Assert.Null(c.Journey!.PresentationCells));
        foreach (string name in new[] { "RouteLeg", "RouteLeg[]", "Position[]", "Double[]", "Int64[]", "TrafficLinkDefinition", "FrozenLink" })
        {
            var start = before.Types.FirstOrDefault(r => r.Name == name);
            var last = after.Types.FirstOrDefault(r => r.Name == name);
            output.WriteLine("  final read " + name + ": " + (start?.Objects ?? 0) + " -> " + (last?.Objects ?? 0) + " objects, " + (((last?.Bytes ?? 0) - (start?.Bytes ?? 0)) / 1e6).ToString("+0.000;-0.000") + " MB since the before read");
        }
        if (sameState) Assert.True(excess < allowance, "retimed minus control " + excess);
        Assert.True(after.Bytes - before.Bytes < allowance, "raw retained growth " + (after.Bytes - before.Bytes));
    }
}
