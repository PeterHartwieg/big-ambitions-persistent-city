using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

[Collection(StreetFoodHooks.Collection)]
public sealed class Gr5RouteRebuildStepTests
{
    private readonly ITestOutputHelper output;
    public Gr5RouteRebuildStepTests(ITestOutputHelper output) { this.output = output; }

    private static readonly double[] Rates = { 2.0, 1, 3, 1 };

    private static string Hash(CityState state) { state.PrepareForSave(); return RushSpikeReplay.Sha(SaveCodec.Write(state)); }

    private static TravelTiming.RetimedTraffic? CommitRetime(ClockRetimeDriver driver, CityState state, double rate)
    {
        while (driver.Update(state, rate, false, 1.4, ClockRetimeStages.SliceMilliseconds) != ClockRetimeOutcome.Committed) { }
        return driver.TakeRetimedTraffic();
    }

    private static (string Final, List<string> AfterCommit, List<RouteRebuild> Rebuilds) FoodRun(bool staged, Action<CityState, RouteRebuild>? perUnit = null)
    {
        var (state, city) = Gr5FoodDecisionTests.FoodCity();
        while (!city.AdvanceTo(2 * 1440 + 12 * 60, 256).CaughtUp) { }
        var driver = new ClockRetimeDriver();
        var hashes = new List<string>(); var rebuilds = new List<RouteRebuild>();
        foreach (double rate in Rates)
        {
            var traffic = CommitRetime(driver, state, rate);
            if (staged)
            {
                var rebuild = city.BeginRebuildRoutes(new Gr5AdvanceSegmentMeasure.Straight(), traffic);
                while (!rebuild.Step(0)) perUnit?.Invoke(state, rebuild);
                city = rebuild.Result!;
                rebuilds.Add(rebuild);
            }
            else city = city.RebuildRoutes(new Gr5AdvanceSegmentMeasure.Straight(), traffic);
            hashes.Add(Hash(state));
            Gr5FoodDecisionTests.Run(city, state.Minute, state.Minute + 2);
        }
        Assert.True(city.ActiveJourneyIndexMatchesState());
        city.AdvanceTo(state.Minute + 30, int.MaxValue);
        return (Hash(state), hashes, rebuilds);
    }

    [Fact]
    public void AStagedRebuildOneUnitPerUpdateMatchesTheWholeRebuildThroughFoodStopsAndTraffic()
    {
        var whole = FoodRun(staged: false);
        var staged = FoodRun(staged: true);
        Assert.Equal(whole.AfterCommit, staged.AfterCommit);
        Assert.Equal(whole.Final, staged.Final);
        foreach (var rebuild in staged.Rebuilds)
        {
            Assert.Equal(rebuild.Units, rebuild.Updates);
            Assert.True(rebuild.Updates > 10, "updates " + rebuild.Updates);
        }
        output.WriteLine("updates per staged rebuild: " + string.Join(", ", staged.Rebuilds.Select(r => r.Updates)));
    }

    [Fact]
    public void ASaveBetweenRebuildUnitsWritesTheCommittedCity()
    {
        var saves = new List<string>();
        var staged = FoodRun(staged: true, (state, _) => saves.Add(Hash(state)));
        var whole = FoodRun(staged: false);
        Assert.Equal(whole.Final, staged.Final);
        Assert.NotEmpty(saves);
        var distinct = saves.Distinct().ToList();
        output.WriteLine(saves.Count + " saves between units, " + distinct.Count + " distinct");
        foreach (var hash in distinct) Assert.Contains(hash, whole.AfterCommit);
    }

    [Fact]
    public void TheRigScaleRetimeKeepsItsPinnedHashThroughAStagedRebuild()
    {
        var (state, _) = TravelTimingMaximumRouteTests.RigScaleCity(TravelTimingMaximumRouteTests.FixtureLinks);
        var simulator = new CitySimulator(state, new NoRoutes());
        var driver = new ClockRetimeDriver();
        foreach (double rate in new[] { 3.0, 20, 1, 20 })
        {
            TravelTiming.RetimedTraffic? traffic;
            while (true)
            {
                var outcome = driver.Update(state, rate, false, 2, ClockRetimeStages.SliceMilliseconds);
                if (outcome == ClockRetimeOutcome.Committed) { traffic = driver.TakeRetimedTraffic(); break; }
            }
            var rebuild = simulator.BeginRebuildRoutes(new NoRoutes(), traffic);
            while (!rebuild.Step(0)) { }
            simulator = rebuild.Result!;
        }
        simulator.AdvanceTo(state.Minute + 0.5, int.MaxValue);
        state.PrepareForSave();
        Assert.Equal("222E663F2FD8440FF74517903D8F0E151B9DEAB5270102C90E679DB4D0B0F03C", RushSpikeReplay.Sha(SaveCodec.Write(state)));
    }

    [Fact]
    public void EveryStagedRebuildUpdateStaysInsideItsSliceOnARigScaleCity()
    {
        var (state, _) = TravelTimingMaximumRouteTests.RigScaleCity(TravelTimingMaximumRouteTests.FixtureLinks);
        var simulator = new CitySimulator(state, new NoRoutes());
        var driver = new ClockRetimeDriver();
        var slowest = new List<double>(); var whole = new List<double>();
        foreach (double rate in new[] { 3.0, 20, 1, 20 })
        {
            var traffic = CommitRetime(driver, state, rate);
            var rebuild = simulator.BeginRebuildRoutes(new NoRoutes(), traffic);
            long budget = (long)(UpdateCityBudget.MinimumSliceMilliseconds * Stopwatch.Frequency / 1000);
            while (!rebuild.Step(Stopwatch.GetTimestamp() + budget)) { }
            simulator = rebuild.Result!;
            slowest.Add(rebuild.SlowestUpdateMilliseconds); whole.Add(rebuild.Milliseconds);
        }
        output.WriteLine("rebuild ms whole / slowest 1 ms update: " + string.Join(", ", whole.Zip(slowest, (w, s) => w.ToString("0.00") + "/" + s.ToString("0.00"))));
        PerfGate.Under(slowest.Skip(1).Max(), 2 * UpdateCityBudget.MinimumSliceMilliseconds + 1, "slowest staged rebuild update on a 1 ms slice (.NET)");
    }

    [Fact]
    public void EveryRetimeUpdateOnTheRigCityLeavesTheWholeUpdateUnderFiveMilliseconds()
    {
        const double prelude = 1, frame = 5;
        var state = Gr5AllocationMeasure.RigCity();
        var city = new CitySimulator(state, new Gr5AdvanceSegmentMeasure.Straight());
        city.AttachFoodMarket(new VisitorRoutineTests.Market { Shops = F5gPopulationFillTests.Shops(state) });
        city.AdvanceTo(Math.Floor(state.Minute / 1440) * 1440 + 1440 + 15.5 * 60, int.MaxValue);
        var budget = new UpdateCityBudget(); var overshoot = new UnitOvershoot();
        var driver = new ClockRetimeDriver();
        var work = new List<double>(); int warmFrom = 0;
        foreach (double rate in new[] { 2.0, 1, 2, 1 })
        {
            if (rate == 1 && warmFrom == 0) warmFrom = work.Count;
            RouteRebuild? rebuild = null;
            while (true)
            {
                int collections = GC.CollectionCount(0);
                long started = Stopwatch.GetTimestamp();
                double slice = budget.Exclusive(prelude, ClockRetimeStages.SliceMilliseconds);
                if (rebuild != null)
                {
                    long deadline = overshoot.Deadline(started, slice);
                    bool done = rebuild.Step(deadline);
                    overshoot.Observe(deadline, Stopwatch.GetTimestamp());
                    work.Add(GC.CollectionCount(0) != collections ? double.NaN : (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency);
                    if (done) { city = rebuild.Result!; break; }
                    continue;
                }
                var outcome = driver.Update(state, rate, false, 1.4, slice);
                work.Add(GC.CollectionCount(0) != collections ? double.NaN : (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency);
                if (outcome == ClockRetimeOutcome.Committed) { rebuild = city.BeginRebuildRoutes(new Gr5AdvanceSegmentMeasure.Straight(), driver.TakeRetimedTraffic()); overshoot.Reset(); }
            }
            Gr5FoodDecisionTests.Run(city, state.Minute, state.Minute + 1);
        }
        Assert.True(city.ActiveJourneyIndexMatchesState());
        var warm = work.Skip(warmFrom).Where(w => !double.IsNaN(w)).ToList();
        output.WriteLine(work.Count + " retime updates (" + work.Count(double.IsNaN) + " with a collection, timed by the gc sampler instead), slowest " + work.Where(w => !double.IsNaN(w)).Max().ToString("0.00") + " ms, warm slowest " + warm.Max().ToString("0.00")
            + " ms, warm p95 " + warm.OrderBy(w => w).ElementAt((int)(warm.Count * .95)).ToString("0.00") + " ms on a " + prelude + " ms prelude (.NET)");
        PerfGate.Under(prelude + warm.Max(), frame, "UpdateCity with a 1 ms prelude in the slowest warm retime update (.NET)");
    }

    [Fact]
    public void AFailedRebuildCannotBeSteppedAgainAndAFinishedOneReturnsItsResult()
    {
        var (state, city) = Gr5FoodDecisionTests.FoodCity();
        while (!city.AdvanceTo(2 * 1440 + 12 * 60, 256).CaughtUp) { }
        var rebuild = city.BeginRebuildRoutes(new Gr5AdvanceSegmentMeasure.Straight(), null);
        Assert.True(rebuild.Step(long.MaxValue));
        var result = rebuild.Result;
        Assert.True(rebuild.Step(0));
        Assert.Same(result, rebuild.Result);
        state.Citizens[0].NextEventMinute = double.NaN;
        var broken = city.BeginRebuildRoutes(new Gr5AdvanceSegmentMeasure.Straight(), null);
        Assert.Throws<ArgumentException>(() => { while (!broken.Step(0)) { } });
        Assert.Throws<InvalidOperationException>(() => broken.Step(0));
        Assert.Null(broken.Result);
    }

    [Fact]
    public void TheExclusiveSliceIgnoresThePhysicalReserveAndKeepsItsFloor()
    {
        var budget = new UpdateCityBudget();
        budget.ObservePhysical(2);
        Assert.Equal(UpdateCityBudget.DefaultTargetMilliseconds, budget.Exclusive(0, ClockRetimeStages.SliceMilliseconds), 9);
        Assert.Equal(UpdateCityBudget.DefaultTargetMilliseconds - 1.25, budget.Exclusive(1.25, ClockRetimeStages.SliceMilliseconds), 9);
        Assert.Equal(UpdateCityBudget.MinimumSliceMilliseconds, budget.Exclusive(3.9, ClockRetimeStages.SliceMilliseconds), 9);
        Assert.Equal(UpdateCityBudget.MinimumSliceMilliseconds, budget.Exclusive(double.NaN, 1), 9);
        Assert.Equal(UpdateCityBudget.DefaultTargetMilliseconds, budget.Exclusive(double.NegativeInfinity, ClockRetimeStages.SliceMilliseconds), 9);
        Assert.Throws<ArgumentOutOfRangeException>(() => budget.Exclusive(0, .5));
    }

    [Fact]
    public void TheUnitOvershootShortensTheNextDeadlineAndDecaysBack()
    {
        var overshoot = new UnitOvershoot();
        long ms = Stopwatch.Frequency / 1000;
        Assert.Equal(1000 + 4 * ms, overshoot.Deadline(1000, 4));
        overshoot.Observe(5 * ms, 7 * ms);
        Assert.Equal(2, overshoot.Milliseconds, 9);
        Assert.Equal(1000 + 2 * ms, overshoot.Deadline(1000, 4));
        Assert.Equal(1000, overshoot.Deadline(1000, 1));
        Assert.Equal(1000, overshoot.Deadline(1000, double.NaN));
        overshoot.Observe(5 * ms, 4 * ms);
        Assert.Equal(2 * (1 - UnitOvershoot.Decay), overshoot.Milliseconds, 9);
        overshoot.Observe(0, 3 * ms);
        Assert.Equal(3, overshoot.Milliseconds, 9);
        overshoot.Reset();
        Assert.Equal(0, overshoot.Milliseconds);
    }

    [Fact]
    public void SteppingAFinishedRebuildAndTheBudgetsAllocateNothing()
    {
        var (_, city) = Gr5FoodDecisionTests.FoodCity();
        var rebuild = city.BeginRebuildRoutes(new Gr5AdvanceSegmentMeasure.Straight(), null);
        rebuild.Step(long.MaxValue);
        var budget = new UpdateCityBudget(); var overshoot = new UnitOvershoot();
        void Run(int count)
        {
            for (int i = 0; i < count; i++)
            {
                rebuild.Step(0);
                long deadline = overshoot.Deadline(i, budget.Exclusive(i % 5, ClockRetimeStages.SliceMilliseconds));
                overshoot.Observe(deadline, deadline + i % 7);
            }
        }
        Run(10000);
        long before = GC.GetAllocatedBytesForCurrentThread();
        Run(10000);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
