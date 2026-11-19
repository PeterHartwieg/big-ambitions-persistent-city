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

    private static long Definitions(RetainedHeap heap) => heap.Types.Where(r => r.Name == nameof(TrafficLinkDefinition)).Sum(r => r.Objects);

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
        Assert.True(allocated < 107_000_000, "allocated " + allocated);
    }
}
