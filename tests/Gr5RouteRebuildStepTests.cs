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
        Assert.Equal("4826F51E48A5445470E7ABD34FB32B53EB2ACE10A64D7235983D6E666CA804B0", RushSpikeReplay.Sha(SaveCodec.Write(state)));
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
        var handOvers = new List<double>(); var publications = new List<int>();
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
                    double stepped = rebuild.Milliseconds;
                    if (done) city = rebuild.TakeResult();
                    work.Add(GC.CollectionCount(0) != collections ? double.NaN : (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency);
                    if (done)
                    {
                        Assert.Equal(stepped + rebuild.HandOverMilliseconds, rebuild.Milliseconds, 9);
                        Assert.True(rebuild.HandOverMilliseconds > 0, "hand-over timed");
                        handOvers.Add(rebuild.HandOverMilliseconds);
                        publications.Add(work.Count - 1);
                        break;
                    }
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
        var published = publications.Where(i => i >= warmFrom && !double.IsNaN(work[i])).Select(i => work[i]).ToList();
        output.WriteLine("publication updates with the grid hand-over: " + string.Join(", ", publications.Select(i => work[i].ToString("0.00"))) + " ms; hand-overs "
            + string.Join(", ", handOvers.Select(h => h.ToString("0.000"))) + " ms (.NET)");
        Assert.Equal(4, handOvers.Count);
        Assert.NotEmpty(published);
        PerfGate.Under(prelude + warm.Max(), frame, "UpdateCity with a 1 ms prelude in the slowest warm retime update (.NET)");
        PerfGate.Under(prelude + published.Max(), frame, "UpdateCity with a 1 ms prelude in the warm publication update including the grid hand-over (.NET)");
    }

    [Fact]
    public void TheRigScaleFoodAttachRunsInMealSizedUnitsAndKeepsTheMoneyCounters()
    {
        var state = Gr5AllocationMeasure.RigCity();
        var city = new CitySimulator(state, new Gr5AdvanceSegmentMeasure.Straight());
        city.AttachFoodMarket(new VisitorRoutineTests.Market { Shops = F5gPopulationFillTests.Shops(state) });
        city.AttachOptionalStops(new Gr5AllocationMeasure.StopSpots(), OptionalStopPolicy.ApprovedChoicePercentage);
        city.AdvanceTo(Math.Floor(state.Minute / 1440) * 1440 + 1440 + 15.5 * 60, int.MaxValue);
        int meals = state.Food!.Residents.Count;
        Assert.True(meals >= 3000, "meals " + meals);
        var times = new List<List<double>>(); var units = new List<int>();
        string? committed = null;
        foreach (double rate in new[] { 2.0, 1, 2, 1 })
        {
            var rebuild = city.BeginRebuildRoutes(new Gr5AdvanceSegmentMeasure.Straight(), null);
            var unitTimes = new List<double>();
            while (true)
            {
                int collections = GC.CollectionCount(0);
                long started = Stopwatch.GetTimestamp();
                bool done = rebuild.Step(0);
                double unit = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
                unitTimes.Add(GC.CollectionCount(0) == collections ? unit : double.MaxValue);
                if (done) break;
            }
            city = rebuild.TakeResult();
            Assert.Equal(rebuild.Units, rebuild.Updates);
            units.Add(rebuild.Units); times.Add(unitTimes);
            Assert.Equal(FoodMoneyCounters.Rescan(state.Food!), city.FoodMoney!.Snapshot());
            Assert.Equal(1, city.FoodMoney!.Rebuilds);
            string hash = Hash(state);
            committed ??= hash;
            Assert.Equal(committed, hash);
        }
        var whole = city.RebuildRoutes(new Gr5AdvanceSegmentMeasure.Straight(), null);
        Assert.Equal(committed, Hash(state));
        Assert.Equal(FoodMoneyCounters.Rescan(state.Food!), whole.FoodMoney!.Snapshot());
        int chunks = (meals + FoodCommerce.AttachMealsPerStep - 1) / FoodCommerce.AttachMealsPerStep;
        Assert.All(units, u => Assert.Equal(units[0], u));
        var best = Enumerable.Range(0, units[0]).Select(i => times.Min(t => t[i])).ToList();
        double slowest = best.Max();
        output.WriteLine("rig-scale rebuild units " + string.Join(", ", units) + ", slowest unit (best of 4) " + slowest.ToString("0.000") + " ms at unit " + best.IndexOf(slowest) + " (.NET)");
        Assert.True(units[0] >= 3 * chunks, "units " + units[0]);
        PerfGate.Under(slowest, 1.25, "slowest rig-scale rebuild unit with the food attach sliced, best of 4 (.NET)");
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

    private sealed class FailingTraffic : IRoutePlanner, ITrafficRoutePlanner
    {
        public Route? Plan(Place from, Place to, double departureMinute, TravelPreferences preferences) => null;
        public TrafficNetwork? TrafficNetwork => throw new InvalidOperationException("traffic setup failed");
        public void SetTrafficForecast(Func<int, double, TrafficLinkForecast>? forecast) { }
        public bool IsTrafficLinkClosed(int linkId) => false;
    }

    [Fact]
    public void TheSourceJourneyIndexStaysIntactUntilTheRebuildIsTakenAndAfterAFailure()
    {
        var (state, city, planner) = Gr5AdvanceAllocationTests.City();
        while (!city.AdvanceTo(2 * 1440 + 15.5 * 60, 256).CaughtUp) { }
        int indexed = city.ActiveJourneys.Count;
        Assert.True(indexed > 0);
        var rebuild = city.BeginRebuildRoutes(planner, null);
        int steps = 0;
        while (!rebuild.Step(0))
        {
            steps++;
            Assert.True(city.ActiveJourneyIndexMatchesState(), "source index after step " + steps);
            Assert.Equal(indexed, city.ActiveJourneys.Count);
        }
        Assert.True(steps > 3);
        Assert.True(city.ActiveJourneyIndexMatchesState());
        Assert.Equal(indexed, city.ActiveJourneys.Count);
        var replacement = rebuild.TakeResult();
        Assert.Same(replacement, rebuild.TakeResult());
        Assert.True(replacement.ActiveJourneyIndexMatchesState());
        Assert.Equal(indexed, replacement.ActiveJourneys.Count);
        Assert.Equal(indexed, replacement.ActiveJourneys.Grid.Count);

        var failing = replacement.BeginRebuildRoutes(new FailingTraffic(), null);
        Assert.Throws<InvalidOperationException>(() => { while (!failing.Step(0)) { } });
        Assert.Throws<InvalidOperationException>(() => failing.TakeResult());
        Assert.True(replacement.ActiveJourneyIndexMatchesState(), "source index after a failed rebuild");
        Assert.Equal(indexed, replacement.ActiveJourneys.Count);
    }

    private static CitizenState Walker(string id, double x)
    {
        var route = new Route();
        route.Legs.Add(new RouteLeg { From = new Position(x, 0, 0), To = new Position(x + 1, 0, 0), Minutes = 1, Mode = TravelMode.Walk });
        return new CitizenState { Identity = new CitizenIdentity { Id = id }, Phase = CitizenPhase.Travelling, Journey = route.Commit() };
    }

    [Fact]
    public void AnEntryTakenFromARetiredGridIsFoundByTheFirstQuery()
    {
        var first = Walker("a", 1); var second = Walker("b", 2);
        var source = new ActiveJourneyIndex();
        source.File(first); source.File(second);
        var output = new List<IPersonView>();
        Assert.Equal(2, source.Grid.Query(new Position(1.5, 0, 0), 5, output).Count);
        var replacement = new ActiveJourneyIndex();
        replacement.File(first);
        replacement.TakeSpareCells(source);
        replacement.File(second);
        var found = replacement.Grid.Query(new Position(1.5, 0, 0), 5, output);
        Assert.Contains(second, found);
        Assert.Equal(2, found.Count);
        Assert.Equal(0, replacement.Grid.Pooled);
    }

    [Fact]
    public void RepeatedRebuildsKeepTheGridEntryCapacityAtAPlateauAndTimeTheHandOver()
    {
        var (state, city, planner) = Gr5AdvanceAllocationTests.City();
        while (!city.AdvanceTo(2 * 1440 + 15.5 * 60, 256).CaughtUp) { }
        int live = city.ActiveJourneys.Grid.Count;
        var capacity = new List<int>();
        for (int round = 0; round < 6; round++)
        {
            var rebuild = city.BeginRebuildRoutes(planner, null);
            while (!rebuild.Step(0)) { }
            double before = rebuild.SlowestUpdateMilliseconds;
            city = rebuild.TakeResult();
            Assert.True(rebuild.HandOverMilliseconds >= 0);
            Assert.True(rebuild.SlowestUpdateMilliseconds >= before);
            Assert.True(rebuild.Milliseconds >= rebuild.HandOverMilliseconds);
            Assert.Equal(live, city.ActiveJourneys.Grid.Count);
            Assert.True(city.ActiveJourneyIndexMatchesState());
            capacity.Add(city.ActiveJourneys.Grid.Count + city.ActiveJourneys.Grid.Pooled);
            output.WriteLine("rebuild " + round + ": live " + city.ActiveJourneys.Grid.Count + ", pooled " + city.ActiveJourneys.Grid.Pooled
                + ", hand-over " + rebuild.HandOverMilliseconds.ToString("0.000") + " ms, slowest update " + rebuild.SlowestUpdateMilliseconds.ToString("0.000") + " ms");
        }
        Assert.All(capacity, c => Assert.Equal(capacity[0], c));
    }

    [Fact]
    public void RebuildingALongRouteGridKeepsBothQueryOrdersAndReducesTheTransientGraph()
    {
        var travellers = new List<CitizenState>();
        for (int i = 0; i < 1000; i++)
        {
            var person = Walker("r" + i, i);
            double z = i % 50 * 64 + 1;
            person.Journey = new Route { Legs =
                { new RouteLeg { From = new Position(1, 0, z), To = new Position(10_241, 0, z), Mode = TravelMode.Walk, Minutes = 200 } } }.Commit();
            travellers.Add(person);
        }
        var source = new PresentationGrid();
        foreach (var person in travellers) source.File(person);
        var replacement = new PresentationGrid();
        long started = GC.GetAllocatedBytesForCurrentThread();
        foreach (var person in travellers) replacement.File(person);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - started;
        var model = new RetainedHeap();
        model.Exclude(travellers);
        foreach (var person in travellers) model.Exclude(person);
        model.Add("source", source);
        model.Add("replacement", replacement);
        output.WriteLine($"tight grid: {replacement.Registrations} registrations, rebuild {allocated} B (.NET), overlap {model.Bytes} B, {model.Objects} objects, {model.ScannedBytes} scanned B, {model.References} references (Mono layout model)");
        var first = new List<IPersonView>(travellers.Count);
        var second = new List<IPersonView>(travellers.Count);
        foreach (double radius in new[] { 0.0, 120.0, double.PositiveInfinity })
        {
            var focus = new Position(300, 0, 3);
            source.Query(focus, radius, first);
            replacement.Query(focus, radius, second);
            Assert.Equal(first, second);
        }
        replacement.TakeSpareCells(source);
        Assert.True(replacement.Matches(travellers, recompute: true));
        var oldest = travellers[0];
        replacement.Remove(oldest);
        replacement.File(oldest);
        Assert.True(replacement.Matches(travellers, recompute: true));
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 32; i++)
        {
            replacement.Remove(oldest);
            replacement.File(oldest);
            replacement.Query(new Position(300, 0, 3), 120, first);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(allocated < 11_000_000, allocated.ToString());
        Assert.True(model.Bytes < 12_000_000, model.Bytes.ToString());
    }
}
