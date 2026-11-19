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
        var planner = new Gr5NetworkRequeueTests.SwappablePlanner { TrafficNetwork = network };
        var city = Gr5NetworkRequeueTests.RoadCity(10, 10, planner);
        city.RefreshTrafficClosures();
        var nudged = Gr5NetworkRequeueTests.Road();
        nudged.Links[5].FreeFlowMinutes = Math.BitIncrement(nudged.Links[5].FreeFlowMinutes);
        planner.TrafficNetwork = nudged;
        city.RefreshTrafficClosures();
        var kernel = city.Traffic!;
        Assert.Equal(nudged.Links.Count - 1, kernel.DefinitionsSharedWith(nudged.Links));
        Assert.Equal(1, kernel.DefinitionsSharedWith(network.Links.Where(l => l.Id == 5).ToList()));
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
}
