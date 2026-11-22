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
public sealed class Gr5RetimeKernelSourceTests
{
    private readonly ITestOutputHelper output;
    public Gr5RetimeKernelSourceTests(ITestOutputHelper output) { this.output = output; }

    private static readonly FieldInfo Stage = typeof(TravelTiming.TravelRetime).GetField("stage", BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static readonly FieldInfo Through = typeof(CitySimulator).GetField("throughTraffic", BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static string Phase(ClockRetimeDriver driver)
    {
        var pending = driver.Pending;
        if (pending == null) return driver.Holding ? "snapshot" : "begin";
        return (int)Stage.GetValue(pending)! switch
        {
            0 => "restore", 1 => "journeys", 2 => "evict", 3 => "evict", 4 => "owners", 5 => "rescale", 6 => "capture", 8 => "vehicles", int other => "stage" + other,
        };
    }

    internal static void AddThroughCars(CityState state, CitySimulator city, int throughCars, int batch, bool advance = true)
    {
        if (throughCars == 0) return;
        if (Through.GetValue(city) == null) city.AttachThroughTraffic(new ThroughTrafficSettings(0.1));
        var through = (ThroughTrafficState)Through.GetValue(city)!;
        var resident = new List<TrafficVehicleState>();
        city.Traffic!.CopyVehiclesInOrder(resident);
        var template = resident.First(v => !ThroughTraffic.IsThroughVehicle(v.VehicleId)).Route;
        for (int i = 0; i < throughCars; i++)
        {
            string id = ThroughTraffic.Prefix + batch.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            city.Traffic!.Enqueue(id, template.Select(r => new TrafficRouteLeg
            { LinkId = r.LinkId, StartNominalMinutes = r.StartNominalMinutes, EndNominalMinutes = r.EndNominalMinutes, FreeFlowMinutes = r.FreeFlowMinutes }).ToArray());
            through.InFlight.Add(id);
        }
        if (advance) city.AdvanceTo(state.Minute + 0.25, int.MaxValue);
    }

    private static string Hash(CityState state) => RushSpikeReplay.Sha(SaveCodec.Write(state));

    private static (SortedDictionary<string, long> Bytes, CitySimulator City) Retime(CityState state, CitySimulator city, double rate, bool legacy,
        Func<CityState, CitySimulator, ClockRetimeDriver, CitySimulator?>? committed = null)
    {
        var driver = new ClockRetimeDriver();
        if (legacy) state.RetimeSnapshotSteps = null;
        var bytes = new SortedDictionary<string, long>(StringComparer.Ordinal);
        void Add(string key, long value) => bytes[key] = (bytes.TryGetValue(key, out long b) ? b : 0) + value;
        while (true)
        {
            string phase = Phase(driver);
            long before = GC.GetAllocatedBytesForCurrentThread();
            var outcome = driver.Update(state, rate, false, 2, 0);
            long spent = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.NotEqual(ClockRetimeOutcome.Restarted, outcome);
            Add(outcome == ClockRetimeOutcome.Committed ? "commit" : phase, spent);
            if (outcome != ClockRetimeOutcome.Committed) continue;
            var replaced = committed?.Invoke(state, city, driver);
            if (replaced != null) return (bytes, replaced);
            before = GC.GetAllocatedBytesForCurrentThread();
            var rebuild = city.BeginRebuildRoutes(new NoRoutes(), driver.TakeRetimedTraffic());
            while (!rebuild.Step(0)) { }
            Add("rebuild", GC.GetAllocatedBytesForCurrentThread() - before);
            return (bytes, rebuild.Result!);
        }
    }

    private static string Line(string label, SortedDictionary<string, long> bytes)
        => label + ": " + string.Join(", ", bytes.Select(p => p.Key + " " + (p.Value / 1e6).ToString("0.000"))) + "; total " + (bytes.Values.Sum() / 1e6).ToString("0.000") + " MB";

    private static long Of(SortedDictionary<string, long> bytes, params string[] phases) => phases.Sum(p => bytes.TryGetValue(p, out long b) ? b : 0);

    private static readonly double[] Rates = { 3.0, 1, 3, 1 };

    private (string Hash, List<SortedDictionary<string, long>> Bytes) RetimeSeries(int throughCars, bool legacy)
    {
        var (state, city) = Gr5RetimeRepeatAllocationTests.TrafficCity();
        var series = new List<SortedDictionary<string, long>>();
        for (int i = 0; i < Rates.Length; i++)
        {
            AddThroughCars(state, city, throughCars, i, advance: i == 0);
            int inFlight = city.ThroughCarCount;
            SortedDictionary<string, long> bytes;
            (bytes, city) = Retime(state, city, Rates[i], legacy);
            series.Add(bytes);
            output.WriteLine(Line((legacy ? "before (snapshot, restore, capture)" : "after (live or excluded kernel)") + ", 13,965 links, 250 resident cars, "
                + inFlight + " through cars, rate " + Rates[i], bytes) + " (.NET)");
            Assert.Equal(0, city.ThroughCarCount);
        }
        city.AdvanceTo(state.Minute + 0.5, int.MaxValue);
        state.PrepareForSave();
        return (Hash(state), series);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(40)]
    public void RetimingFromTheLiveOrExcludedKernelMatchesTheSnapshotPathAndDropsItsKernelCopies(int throughCars)
    {
        var (before, beforeBytes) = RetimeSeries(throughCars, legacy: true);
        var (after, afterBytes) = RetimeSeries(throughCars, legacy: false);
        Assert.Equal(before, after);
        for (int i = 0; i < Rates.Length; i++)
        {
            Assert.Equal(0, Of(afterBytes[i], "restore", "capture"));
            long saved = beforeBytes[i].Values.Sum() - afterBytes[i].Values.Sum();
            output.WriteLine("retime " + i + ": " + (saved / 1e6).ToString("0.000") + " MB less (.NET)");
            Assert.True(saved > 4_500_000, "retime " + i + " saved " + saved);
        }
        for (int i = 0; i < Rates.Length; i++)
            Assert.True(Of(afterBytes[i], "snapshot") < (throughCars == 0 ? 1 : 3_000_000), "snapshot " + i + " " + Of(afterBytes[i], "snapshot"));
    }

    [Fact]
    public void ASaveOrAFreshSimulatorBetweenCommitAndRebuildSeesTheRetimedTraffic()
    {
        string Run(bool legacy, bool fresh)
        {
            var (state, city) = Gr5RetimeRepeatAllocationTests.TrafficCity();
            AddThroughCars(state, city, 40, 0);
            string? between = null;
            (_, city) = Retime(state, city, 3, legacy, (s, c, driver) =>
            {
                between = Hash(s);
                if (!fresh) return null;
                driver.TakeRetimedTraffic();
                return new CitySimulator(s, new NoRoutes());
            });
            (_, city) = Retime(state, city, 1, legacy);
            city.AdvanceTo(state.Minute + 0.5, int.MaxValue);
            state.PrepareForSave();
            return between + "/" + Hash(state);
        }
        string before = Run(legacy: true, fresh: false);
        Assert.Equal(before, Run(legacy: false, fresh: false));
        Assert.Equal(Run(legacy: true, fresh: true), Run(legacy: false, fresh: true));
    }

    [Fact]
    public void ARebuildWithoutTheRetimedKernelStillRestoresTheRetimedTraffic()
    {
        string Run(bool legacy)
        {
            var (state, city) = Gr5RetimeRepeatAllocationTests.TrafficCity();
            (_, city) = Retime(state, city, 3, legacy, (s, c, driver) =>
            {
                driver.TakeRetimedTraffic();
                var rebuild = c.BeginRebuildRoutes(new NoRoutes(), null);
                while (!rebuild.Step(0)) { }
                return rebuild.Result!;
            });
            city.AdvanceTo(state.Minute + 0.5, int.MaxValue);
            state.PrepareForSave();
            return Hash(state);
        }
        Assert.Equal(Run(legacy: true), Run(legacy: false));
    }

    private static string Json(TrafficKernelState state) => System.Text.Json.JsonSerializer.Serialize(state);

    [Fact]
    public void TheExcludedKernelMatchesARestoreOfTheExcludedSnapshotWithoutItsCopies()
    {
        var (state, city) = Gr5RetimeRepeatAllocationTests.TrafficCity();
        AddThroughCars(state, city, 40, 0);
        var live = city.Traffic!;
        Func<string, bool> through = ThroughTraffic.IsThroughVehicle;
        long started = GC.GetAllocatedBytesForCurrentThread();
        var snapshot = live.Capture(through);
        var restored = TrafficKernel.Restore(snapshot);
        long before = GC.GetAllocatedBytesForCurrentThread() - started;
        started = GC.GetAllocatedBytesForCurrentThread();
        var steps = live.WithoutVehiclesInSteps(through, 256);
        TrafficKernel? excluded = null;
        int slices = 0;
        while (steps.MoveNext()) { if (steps.Current != null) { excluded = steps.Current; break; } slices++; }
        long after = GC.GetAllocatedBytesForCurrentThread() - started;
        output.WriteLine("40 through cars at 13,965 links: excluded snapshot + restore " + (before / 1e6).ToString("0.000") + " MB, excluded kernel "
            + (after / 1e6).ToString("0.000") + " MB in " + slices + " slices (.NET)");
        Assert.NotNull(excluded);
        Assert.NotSame(live, excluded);
        Assert.True(slices > 13965 / 256, slices.ToString());
        Assert.Equal(Json(restored.Capture()), Json(excluded!.Capture()));
        Assert.Equal(Json(snapshot), Json(excluded.Capture()));
        Assert.DoesNotContain(excluded.Capture().Vehicles, v => ThroughTraffic.IsThroughVehicle(v.VehicleId));
        Assert.True(after * 4 < before, after + " vs " + before);
        var none = live.WithoutVehiclesInSteps(id => false, 256);
        Assert.True(none.MoveNext());
        Assert.Same(live, none.Current);
    }

    [Fact]
    public void AnAbandonedRetimeKeepsNoTrafficKernelInTheCityState()
    {
        var (state, city) = Gr5RetimeRepeatAllocationTests.TrafficCity();
        AddThroughCars(state, city, 40, 0);
        double one = state.TravelClockRate;
        var driver = new ClockRetimeDriver();
        ClockRetimeOutcome outcome;
        do outcome = driver.Update(state, one * 3, false, 2, 0);
        while (outcome == ClockRetimeOutcome.Preparing && driver.Pending == null);
        Assert.Equal(ClockRetimeOutcome.Began, outcome);
        Assert.NotSame(city.Traffic, SavedTraffic.GetValue(driver.Pending!));
        Assert.Null(state.RetimeSource);
        Assert.Equal(ClockRetimeOutcome.None, driver.Update(state, one, false, 2, 0));
        Assert.Null(driver.Pending);
        Assert.Null(state.RetimeSource);
        Assert.Null(state.PendingRetimedTraffic);
        Assert.NotNull(state.RetimeSnapshotSteps);
        (_, city) = Retime(state, city, one * 3, legacy: false);
        Assert.Null(state.PendingRetimedTraffic);
        Assert.Null(state.Traffic);
    }

    [Theory]
    [InlineData(0, false, "the live kernel")]
    [InlineData(40, false, "a copy without through cars")]
    [InlineData(40, true, "a save snapshot")]
    public void TheDriverNamesTheTrafficItRetimedFrom(int throughCars, bool legacy, string expected)
    {
        var (state, city) = Gr5RetimeRepeatAllocationTests.TrafficCity();
        AddThroughCars(state, city, throughCars, 0);
        string? named = null;
        Retime(state, city, 3, legacy, (s, c, driver) => { named = driver.TrafficSource; return null; });
        Assert.Equal(expected, named);
    }

    private static readonly FieldInfo SavedTraffic = typeof(TravelTiming.TravelRetime).GetField("savedTraffic", BindingFlags.NonPublic | BindingFlags.Instance)!;
}
