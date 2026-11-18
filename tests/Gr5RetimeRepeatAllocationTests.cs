using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

[Collection(StreetFoodHooks.Collection)]
public sealed class Gr5RetimeRepeatAllocationTests
{
    private readonly ITestOutputHelper output;
    public Gr5RetimeRepeatAllocationTests(ITestOutputHelper output) { this.output = output; }

    private static readonly FieldInfo Stage = typeof(TravelTiming.TravelRetime).GetField("stage", BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static string Phase(ClockRetimeDriver driver)
    {
        var pending = driver.Pending;
        if (pending == null) return driver.Holding ? "snapshot" : "begin";
        return (int)Stage.GetValue(pending)! switch
        {
            1 => "journeys", 4 => "owners", 5 => "rescale", 6 => "capture", 8 => "vehicles", int other => "stage" + other,
        };
    }

    private static (SortedDictionary<string, long> Bytes, CitySimulator City) Retime(CityState state, CitySimulator city, ClockRetimeDriver driver,
        double rate, double walk, Func<IRoutePlanner> planner)
    {
        var bytes = new SortedDictionary<string, long>(StringComparer.Ordinal);
        void Add(string key, long value) => bytes[key] = (bytes.TryGetValue(key, out long b) ? b : 0) + value;
        while (true)
        {
            string phase = Phase(driver);
            long before = GC.GetAllocatedBytesForCurrentThread();
            var outcome = driver.Update(state, rate, false, walk, 0);
            long spent = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.NotEqual(ClockRetimeOutcome.Restarted, outcome);
            Add(outcome == ClockRetimeOutcome.Committed ? "commit" : phase, spent);
            if (outcome != ClockRetimeOutcome.Committed) continue;
            before = GC.GetAllocatedBytesForCurrentThread();
            var rebuild = city.BeginRebuildRoutes(planner(), driver.TakeRetimedTraffic());
            while (!rebuild.Step(0)) { }
            Add("rebuild", GC.GetAllocatedBytesForCurrentThread() - before);
            return (bytes, rebuild.Result!);
        }
    }

    internal static (CityState State, CitySimulator City) TrafficCity()
    {
        var (state, _) = TravelTimingMaximumRouteTests.RigScaleCity(TravelTimingMaximumRouteTests.FixtureLinks);
        foreach (var c in state.Citizens) c.Journey = c.Journey!.Commit();
        return (state, new CitySimulator(state, new NoRoutes()));
    }

    internal static (CityState State, CitySimulator City) PeopleCity()
    {
        var state = Gr5AllocationMeasure.RigCity();
        var city = new CitySimulator(state, new Gr5AdvanceSegmentMeasure.Straight());
        city.AttachFoodMarket(new VisitorRoutineTests.Market { Shops = F5gPopulationFillTests.Shops(state) });
        city.AdvanceTo(Math.Floor(state.Minute / 1440) * 1440 + 1440 + 15.5 * 60, int.MaxValue);
        return (state, city);
    }

    private SortedDictionary<string, long> RepeatRetime(CityState state, CitySimulator city, double[] rates, double walk, Func<IRoutePlanner> planner, string label)
    {
        var driver = new ClockRetimeDriver();
        SortedDictionary<string, long>? last = null;
        foreach (double rate in rates)
        {
            (last, city) = Retime(state, city, driver, rate, walk, planner);
            output.WriteLine(label + " rate " + rate + ": " + string.Join(", ", last.Select(p => p.Key + " " + (p.Value / 1e6).ToString("0.000")))
                + "; total " + (last.Values.Sum() / 1e6).ToString("0.000") + " MB (.NET)");
        }
        return last!;
    }

    [Fact]
    public void ARepeatRetimeOfTheRigTrafficKernelStaysUnderItsByteCeiling()
    {
        var (state, city) = TrafficCity();
        var bytes = RepeatRetime(state, city, new[] { 3.0, 20, 1, 20 }, 2, () => new NoRoutes(), "13,965 links, 250 vehicles");
        long kernel = bytes["snapshot"] + bytes["rescale"] + bytes["capture"] + bytes["vehicles"];
        output.WriteLine("kernel snapshot + rescale + capture + vehicle check " + (kernel / 1e6).ToString("0.000") + " MB (.NET)");
        Assert.True(kernel < 6_200_000, "kernel " + kernel);
    }

    [Fact]
    public void ARepeatRetimeOfTheRigPopulationStaysUnderItsByteCeiling()
    {
        var (state, city) = PeopleCity();
        var bytes = RepeatRetime(state, city, new[] { 2.0, 1, 2, 1 }, 1.4, () => new Gr5AdvanceSegmentMeasure.Straight(), "1,000 residents, 2,000 visitors");
        long total = bytes.Values.Sum();
        Assert.True(total < 3_300_000, "population " + total);
    }

    [Fact]
    public void ARatedNetworkCopyStaysUnderItsByteCeiling()
    {
        var nodes = Gr5RetimeReuseTests.Grid(40, 1);
        var original = new TrafficNetwork("gr5-retime-native", nodes, 1);
        var signals = Gr5RetimeReuseTests.Grid(40, 2).ToDictionary(n => n.Id, n => n.Signal);
        _ = original.Rerated(2, id => signals[id]);
        long started = GC.GetAllocatedBytesForCurrentThread();
        _ = original.Rerated(2, id => signals[id]);
        long rerated = GC.GetAllocatedBytesForCurrentThread() - started;
        output.WriteLine(original.Links.Count + " links, " + nodes.Count + " nodes: rated copy " + (rerated / 1e6).ToString("0.000") + " MB (.NET)");
        Assert.True(rerated < 1_200_000, "rerated " + rerated);
    }

    [Fact]
    public void ACaptureThatReusesEqualListsWritesTheSameStateAsAFreshCapture()
    {
        var (state, _) = TravelTimingMaximumRouteTests.RigScaleCity(TravelTimingMaximumRouteTests.FixtureLinks);
        var kernel = TrafficKernel.Restore(state.Traffic!);
        var earlier = kernel.CaptureShared();
        kernel.AdvanceTo(state.Traffic!.Clock + 0.4, 100000);
        var reused = kernel.CaptureShared(earlier);
        var fresh = kernel.Capture();
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(fresh), System.Text.Json.JsonSerializer.Serialize(reused));
        int shared = 0;
        for (int i = 0; i < reused.Links.Count; i++)
            if (ReferenceEquals(reused.Links[i].IncomingLinks, earlier.Links[i].IncomingLinks)) shared++;
        Assert.True(shared > reused.Links.Count / 2, shared + " of " + reused.Links.Count);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(fresh), System.Text.Json.JsonSerializer.Serialize(TrafficKernel.Restore(reused).Capture()));
    }

    [Theory]
    [InlineData(1.0, 2.0)]
    [InlineData(2.0, 1.0)]
    [InlineData(1.0 / 3, 20.0)]
    public void ASharedEdgeTableRatesEveryEdgeLikeAFreshBuild(double before, double after)
    {
        var original = new TrafficNetwork("gr5-retime-native", Gr5RetimeReuseTests.Grid(12, before), before);
        var target = Gr5RetimeReuseTests.Grid(12, after);
        var signals = target.ToDictionary(n => n.Id, n => n.Signal);
        Gr5RetimeReuseTests.AssertSame(new TrafficNetwork("gr5-retime-native", target, after), original.Rerated(after, id => signals[id]), target);
    }
}
