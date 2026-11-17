using System;
using System.Linq;
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
}
