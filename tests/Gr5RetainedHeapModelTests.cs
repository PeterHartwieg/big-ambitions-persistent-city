using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using PersistentCity.Core;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

[Collection(StreetFoodHooks.Collection)]
public sealed class Gr5RetainedHeapModelTests
{
    private readonly ITestOutputHelper output;
    public Gr5RetainedHeapModelTests(ITestOutputHelper output) { this.output = output; }

    private void Print(string title, RetainedHeap heap)
    {
        string M(long bytes) => (bytes / 1e6).ToString("0.000");
        output.WriteLine(title + ": objects " + heap.Objects + ", MB " + M(heap.Bytes) + ", scanned MB " + M(heap.ScannedBytes) + ", references " + heap.References);
        foreach (var row in heap.Roots) output.WriteLine("  root " + row.Name + ": objects " + row.Objects + ", MB " + M(row.Bytes) + ", scanned MB " + M(row.ScannedBytes) + ", references " + row.References);
        foreach (var row in heap.Types.Take(25)) output.WriteLine("  type " + row.Name + ": objects " + row.Objects + ", MB " + M(row.Bytes) + ", references " + row.References);
    }

    private static (CityState State, CitySimulator Simulator) DayCity()
    {
        var state = Gr5AllocationMeasure.RigCity();
        var simulator = new CitySimulator(state, new FreshCityTraceTests.Walk());
        simulator.AttachFoodMarket(new VisitorRoutineTests.Market { Shops = F5gPopulationFillTests.Shops(state) });
        double day = Math.Floor(state.Minute / 1440) * 1440 + 1440;
        simulator.AdvanceTo(day + 15.5 * 60, int.MaxValue);
        return (state, simulator);
    }

    [Fact]
    public void TheRigScaleCityRetainsABoundedPointerDenseHeap()
    {
        var (state, simulator) = DayCity();
        var heap = new RetainedHeap();
        heap.Add("citizens", state.Citizens);
        heap.Add("visitors", state.Visitors);
        heap.Add("places", state.Places);
        heap.Add("traffic", state.Traffic);
        heap.Add("food", state.Food);
        heap.Add("simulator", simulator);
        heap.Add("state other", state);
        Print("rig-scale city at 15:30 (.NET layout, Mono 64-bit header model)", heap);
        Assert.Equal(CityBalance.CityResidentTotal, state.Citizens.Count);
        int persons = state.Citizens.Count + state.Visitors.Count;
        Assert.True(heap.Objects < 60L * persons, "objects " + heap.Objects);
        Assert.True(heap.ScannedBytes < 4500L * persons, "scanned bytes " + heap.ScannedBytes);
    }

    [Fact]
    public void ALiveRigScaleTrafficKernelRetainsNoUnusedLinkState()
    {
        var (state, owned) = TravelTimingMaximumRouteTests.RigScaleCity(TravelTimingMaximumRouteTests.FixtureLinks);
        var restore = TrafficKernel.RestoreInSteps(state.Traffic!, int.MaxValue);
        TrafficKernel? kernel = null;
        while (restore.MoveNext()) kernel = restore.Current ?? kernel;
        var heap = new RetainedHeap();
        heap.Add("kernel", kernel);
        Print("live kernel, " + TravelTimingMaximumRouteTests.FixtureLinks + " links, " + owned + " vehicles", heap);
        Assert.DoesNotContain(heap.Types, row => row.Name == nameof(TrafficLinkState));
        Assert.True(heap.Objects < 12 * TravelTimingMaximumRouteTests.FixtureLinks + 8 * owned * 4, "objects " + heap.Objects);
        Assert.True(heap.Objects < 5 * TravelTimingMaximumRouteTests.FixtureLinks, "objects " + heap.Objects);
        Assert.DoesNotContain(heap.Types, row => row.Name.StartsWith("Queue", StringComparison.Ordinal));
        Assert.True(heap.Types.Where(row => row.Name == nameof(TrafficEventState)).Sum(row => row.Objects) <= owned, "events");
    }

    [Fact]
    public void TheGcSamplerAndFrameRecorderRetainOnlyPointerFreeArrays()
    {
        var heap = new RetainedHeap();
        var sampler = new GcPauseSampler();
        heap.Add("sampler", sampler);
        Print("gc sampler", heap);
        Assert.True(heap.ScannedBytes < 64 * 1024, "scanned bytes " + heap.ScannedBytes);
    }

    private sealed class Owned { public int[] Values = new int[1000]; public Foreign? Outside; public string Label = "owned"; }
    private sealed class Foreign { public byte[] Payload = new byte[1_000_000]; }

    [Fact]
    public void TheRuntimeCensusMatchesTheTestModelAndStopsAtForeignObjects()
    {
        var (state, simulator) = DayCity();
        var model = new RetainedHeap();
        model.Add("state", state); model.Add("simulator", simulator);
        var census = new RetainedCensus();
        census.Add("state", state); census.Add("simulator", simulator);
        output.WriteLine("census MB " + (census.Bytes / 1e6).ToString("0.000") + " vs model MB " + (model.Bytes / 1e6).ToString("0.000") + ", objects " + census.Objects + " vs " + model.Objects);
        Assert.Equal(0, census.Failures);
        Assert.Equal(model.Objects, census.Objects);
        Assert.InRange(census.Bytes, model.Bytes * 0.9, model.Bytes * 1.1);

        var shared = new Owned { Outside = new Foreign() };
        var bounded = new RetainedCensus(type => type != typeof(Foreign));
        var first = bounded.Add("first", shared);
        var second = bounded.Add("second", new[] { shared });
        Assert.Equal(3, first.Objects);
        Assert.Equal(1, second.Objects);
        Assert.Equal(1, bounded.ForeignObjects);
        Assert.True(first.Bytes < 8 * 1024, "foreign payload walked: " + first.Bytes);
        Assert.Equal(40L + 4032 + 32, first.Bytes);
        using var json = JsonDocument.Parse(bounded.ReportJson(8, 1.5));
        Assert.Equal(new[] { "policy", "layout", "objects", "bytes", "scanned_bytes", "roots", "foreign_objects", "failures", "elapsed_ms", "top_roots", "top_types", "foreign_types", "type_counts" },
            json.RootElement.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal("first", json.RootElement.GetProperty("top_roots")[0].GetProperty("name").GetString());
        Assert.Equal("Foreign", json.RootElement.GetProperty("foreign_types")[0].GetProperty("name").GetString());
    }

    [Fact]
    public void AFoodCityRetainsItsStateOnceAndARetiredSimulatorKeepsOnlyItsPrivateParts()
    {
        var (state, city, planner) = Gr5AdvanceAllocationTests.City();
        while (!city.AdvanceTo(2 * 1440 + 15.5 * 60, 256).CaughtUp) { }
        var live = new RetainedCensus();
        live.Add("fixture planner", planner);
        live.Add("food", state.Food); live.Add("state", state); live.Add("simulator", city);
        Print("food city at day 2 15:30", live);
        var rebuild = city.BeginRebuildRoutes(planner, null);
        while (!rebuild.Step(long.MaxValue)) { }
        var replacement = rebuild.TakeResult();
        var after = new RetainedCensus();
        after.Add("fixture planner", planner);
        after.Add("state", state); after.Add("replacement", replacement);
        var retired = after.Add("retired simulator", city);
        Print("after a rebuild, retired simulator private part", after);
        Assert.True(retired.Bytes > 0);
        Assert.True(retired.Bytes < live.Bytes, "retired " + retired.Bytes + " of " + live.Bytes);
    }

    private void Print(string title, RetainedCensus census)
    {
        string M(long bytes) => (bytes / 1e6).ToString("0.000");
        output.WriteLine(title + ": objects " + census.Objects + ", MB " + M(census.Bytes) + ", scanned MB " + M(census.ScannedBytes));
        foreach (var row in census.Roots) output.WriteLine("  root " + row.Name + ": objects " + row.Objects + ", MB " + M(row.Bytes) + ", scanned MB " + M(row.ScannedBytes));
        foreach (var row in census.Types.Take(15)) output.WriteLine("  type " + row.Name + ": objects " + row.Objects + ", MB " + M(row.Bytes));
    }

    private sealed class RuntimeShape { public IDictionary<string, string>? Validated; public ReceiptsShape? Receipts; public string? Payload; }
    private sealed class ReceiptsShape { public IDictionary<string, string>? Data; }

    [Fact]
    public void TheCensusCountsOnlyTheModsOwnSaveEntriesThroughTheSharedDictionary()
    {
        string mine = new string('p', 100_000), receipt = new string('r', 10_000), foreign = new string('f', 1_000_000);
        var modData = new Dictionary<string, string> { ["persistentcity:state_v1"] = mine, ["persistentcity:food-sale-v1:a"] = receipt, ["othermod:state"] = foreign, ["persistentcitytools:state"] = foreign + "t" };
        var runtime = new RuntimeShape { Validated = modData, Receipts = new ReceiptsShape { Data = modData }, Payload = mine };
        var census = new RetainedCensus();
        census.AddOwnedEntries(modData, "persistentcity:", "save.modData.");
        census.AddFields("runtime.", runtime, null);
        var roots = census.Roots.ToDictionary(r => r.Name, r => r);
        long Text(string value) => (16 + 4 + 2L * (value.Length + 1) + 7) / 8 * 8;
        Assert.Equal(Text(mine), roots["save.modData.persistentcity:state_v1"].Bytes);
        Assert.Equal(Text(receipt), roots["save.modData.persistentcity:food-sale-v1:a"].Bytes);
        Assert.DoesNotContain(census.Roots, r => r.Name.Contains("othermod") || r.Name.Contains("persistentcitytools"));
        Assert.True(census.Bytes < Text(mine) + Text(receipt) + 1024, "census counted the shared dictionary: " + census.Bytes);
        Assert.Equal(0, roots["runtime.Validated"].Objects);
        Assert.Equal(0, roots["runtime.Payload"].Objects);
        Assert.Equal(1, roots["runtime.Receipts"].Objects);
    }

    [Fact]
    public void TheCensusCountsEverySimulatorOutsideTheTopTypes()
    {
        var (state, city, planner) = Gr5AdvanceAllocationTests.City();
        while (!city.AdvanceTo(2 * 1440 + 15.5 * 60, 256).CaughtUp) { }
        var rebuild = city.BeginRebuildRoutes(planner, null);
        while (!rebuild.Step(long.MaxValue)) { }
        var replacement = rebuild.TakeResult();
        string Report(params object[] roots)
        {
            var census = new RetainedCensus();
            for (int i = 0; i < roots.Length; i++) census.Add("root" + i, roots[i]);
            return census.ReportJson(2, 0, new[] { typeof(CitySimulator), typeof(TrafficKernel) });
        }
        using var one = JsonDocument.Parse(Report(planner, replacement));
        using var two = JsonDocument.Parse(Report(planner, replacement, city));
        Assert.DoesNotContain(one.RootElement.GetProperty("top_types").EnumerateArray(), row => row.GetProperty("name").GetString() == nameof(CitySimulator));
        Assert.Equal(1, one.RootElement.GetProperty("type_counts").GetProperty(nameof(CitySimulator)).GetInt64());
        Assert.Equal(2, two.RootElement.GetProperty("type_counts").GetProperty(nameof(CitySimulator)).GetInt64());
        GC.KeepAlive(state);
    }
}
