using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

public sealed class Gr5RetimeSliceTests
{
    private readonly ITestOutputHelper output;
    public Gr5RetimeSliceTests(ITestOutputHelper output) { this.output = output; }

    private sealed class ModUpdates
    {
        public readonly CityState State;
        public CitySimulator City;
        public readonly bool Sliced;
        public readonly ClockRetimeDriver Driver = new ClockRetimeDriver();
        public double Budget = .3;
        public bool Bodies = true;
        public int Events, Rebuilds, Slices, Restarts;
        public double SlowestRetimeUpdate, CommitUpdate, Rebuild;
        public Action<ModUpdates>? WhilePreparing;
        public ModUpdates(bool sliced, bool atFence = false)
        {
            Sliced = sliced;
            State = Gr5AllocationMeasure.RigCity();
            City = new CitySimulator(State, new Gr5AdvanceSegmentMeasure.Straight());
            City.AttachFoodMarket(new VisitorRoutineTests.Market { Shops = F5gPopulationFillTests.Shops(State) });
            double noon = Math.Floor(State.Minute / 1440) * 1440 + 1440 + 12 * 60;
            City.AdvanceTo(atFence ? Math.BitDecrement(noon + 1) : noon, int.MaxValue);
        }
        public void Invalidate()
        {
            if (State.Minute == Math.Floor(State.Minute)) City.AdvanceTo(Math.BitDecrement(State.Minute + 1), int.MaxValue);
            else Assert.True(City.BeginAccountingObservations(Math.Floor(State.Minute) + 1));
        }
        public TravelTiming.TravelRetime? Pending => Driver.Pending;
        public void Update(double nativeRate, double target)
        {
            double before = State.TravelClockRate; var city = City;
            if (!Sliced)
            {
                var stage = ClockRetimeStages.Next(nativeRate, State.TravelClockRate, Bodies);
                if (stage == ClockRetimeStage.ReleaseBodies) { Bodies = false; return; }
                if (stage == ClockRetimeStage.RetimeAndRebuild)
                {
                    State.PrepareForSave();
                    TravelTiming.Apply(State, 1.4, nativeRate, out _);
                    City = City.RebuildRoutes(new Gr5AdvanceSegmentMeasure.Straight());
                    Rebuilds++;
                    return;
                }
            }
            else
            {
                long started = System.Diagnostics.Stopwatch.GetTimestamp();
                var outcome = Driver.Update(State, nativeRate, Bodies, 1.4, Budget);
                if (outcome == ClockRetimeOutcome.ReleaseBodies) { Bodies = false; return; }
                if (outcome != ClockRetimeOutcome.None)
                {
                    Slices++;
                    if (outcome == ClockRetimeOutcome.Restarted) Restarts++;
                    if (outcome == ClockRetimeOutcome.Committed)
                    {
                        long rebuild = System.Diagnostics.Stopwatch.GetTimestamp();
                        City = City.RebuildRoutes(new Gr5AdvanceSegmentMeasure.Straight());
                        Rebuild = Math.Max(Rebuild, (System.Diagnostics.Stopwatch.GetTimestamp() - rebuild) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
                        Rebuilds++;
                    }
                    double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                    SlowestRetimeUpdate = Math.Max(SlowestRetimeUpdate, ms);
                    if (outcome == ClockRetimeOutcome.Committed) CommitUpdate = Math.Max(CommitUpdate, ms);
                    Assert.True(State.TravelClockRate == before || !ReferenceEquals(city, City), "journeys were retimed without a rebuilt simulator");
                    Assert.True(Pending == null || State.TravelClockRate == before, "a pending retime changed the city before its commit");
                    if (Pending != null) WhilePreparing?.Invoke(this);
                    return;
                }
            }
            Bodies = true;
            for (int unit = 0; unit < 64; unit++)
            {
                if (City.PlanningPending) { City.ContinuePlanning(1); continue; }
                var result = City.AdvanceFrameSlice(target, 1);
                Events += result.Events;
                if (result.CaughtUp) break;
            }
        }
        public string Save() { City.SynchronizeSnapshot(); return SaveCodec.Write(State); }
    }

    private static void Settle(ModUpdates mod, double rate, double target)
    {
        while (ClockRetimeStages.Next(rate, mod.State.TravelClockRate, mod.Bodies) != ClockRetimeStage.None) mod.Update(rate, target);
    }

    private static CityState Noon()
    {
        var state = Gr5AllocationMeasure.RigCity();
        var city = new CitySimulator(state, new Gr5AdvanceSegmentMeasure.Straight());
        city.AttachFoodMarket(new VisitorRoutineTests.Market { Shops = F5gPopulationFillTests.Shops(state) });
        city.AdvanceTo(Math.Floor(state.Minute / 1440) * 1440 + 1440 + 12 * 60, int.MaxValue);
        city.SynchronizeSnapshot();
        return state;
    }

    private static string Clone(CityState state) { state.PrepareForSave(); return SaveCodec.Write(state); }

    [Fact]
    public void SlicedRetimeGivesTheUnslicedJourneysAndSaveThroughOneTwoOneRates()
    {
        var whole = new ModUpdates(false);
        var sliced = new ModUpdates(true);
        Assert.Equal(whole.Save(), sliced.Save());
        int travelling = sliced.State.Citizens.Count(c => c.Phase == CitizenPhase.Travelling);
        Assert.True(travelling > 20, travelling.ToString());
        foreach (double rate in new[] { 1.0, 2, 2, 1, 1, 3, 1 })
        {
            double target = whole.State.Minute + 1;
            Settle(whole, rate, target); Settle(sliced, rate, target);
            whole.Update(rate, target); sliced.Update(rate, target);
            Assert.Equal(whole.State.TravelClockRate, sliced.State.TravelClockRate);
            Assert.Equal(whole.Save(), sliced.Save());
        }
        Assert.True(sliced.Slices > sliced.Rebuilds * 2, sliced.Slices + " slices for " + sliced.Rebuilds + " rebuilds");
        Assert.Equal(whole.Rebuilds, sliced.Rebuilds);
        Assert.Equal(0, sliced.Restarts);
        Assert.True(sliced.City.ActiveJourneyIndexMatchesState());
        output.WriteLine("slowest retime update " + sliced.SlowestRetimeUpdate.ToString("0.00") + " ms, commit + rebuild update " + sliced.CommitUpdate.ToString("0.00") + " ms (rebuild " + sliced.Rebuild.ToString("0.00") + " ms)");
        PerfGate.Under(sliced.CommitUpdate, 30, "retime commit and simulator rebuild");
        PerfGate.Under(sliced.SlowestRetimeUpdate, 30, "slowest retime update");
    }

    [Fact]
    public void AnAccountingBoundaryBetweenSlicesRestartsTheRetimeInSlicesAndMatchesTheUnslicedRetimeAtTheNewMinute()
    {
        var whole = new ModUpdates(false, atFence: true);
        var sliced = new ModUpdates(true, atFence: true) { Budget = .1 };
        double start = sliced.State.Minute;
        Assert.Equal(start, whole.State.Minute);
        Assert.True(start < Math.Ceiling(start));
        int observed = 0;
        sliced.WhilePreparing = mod =>
        {
            Assert.Equal(0, AccountingClock.Limit(!mod.Driver.Holding, Math.Floor(mod.State.Minute) + .5, 1));
            if (observed >= 3 || mod.Driver.Pending!.Prepared < 200) return;
            observed++;
            mod.Invalidate();
        };
        Settle(sliced, 2, start + 10);
        Assert.Equal(3, observed);
        Assert.Equal(3, sliced.Restarts);
        Assert.Equal(1, sliced.Rebuilds);
        Assert.Equal(Math.Ceiling(start) + 1, sliced.State.Minute);
        for (int i = 0; i < 3; i++) whole.Invalidate();
        Settle(whole, 2, start + 10);
        Assert.Equal(whole.Save(), sliced.Save());
        output.WriteLine(sliced.Slices + " retime updates over 3 restarts, slowest " + sliced.SlowestRetimeUpdate.ToString("0.00") + " ms");
        PerfGate.Under(sliced.SlowestRetimeUpdate, 30, "retime update after repeated invalidation");
        sliced.WhilePreparing = null;
        double target = sliced.State.Minute + 1;
        whole.Update(2, target); sliced.Update(2, target);
        Assert.Equal(whole.Save(), sliced.Save());
    }

    [Fact]
    public void ACancelledRetimeForgetsItsRestartsAndTheNextRetimeStaysSliced()
    {
        var mod = new ModUpdates(true, atFence: true) { Budget = .1 };
        int observed = 0;
        mod.WhilePreparing = m =>
        {
            if (observed >= 2 || m.Driver.Pending!.Prepared < 100) return;
            observed++;
            m.Invalidate();
        };
        for (int i = 0; i < 400 && observed < 2; i++) mod.Update(2, mod.State.Minute);
        mod.Update(2, mod.State.Minute);
        Assert.Equal(2, mod.Driver.Restarts);
        mod.Update(1, mod.State.Minute);
        Assert.Null(mod.Pending);
        Assert.Equal(0, mod.Driver.Restarts);
        mod.WhilePreparing = null;
        int before = mod.Slices;
        Settle(mod, 2, mod.State.Minute);
        Assert.Equal(1, mod.Rebuilds);
        Assert.True(mod.Slices - before > 4, (mod.Slices - before).ToString());
        Assert.Equal(0, mod.Driver.Restarts);
    }

    [Fact]
    public void ARateChangeDuringAnUnfinishedSliceRestartsAtTheNewRateAndAReversalLeavesTheCityUntouched()
    {
        var state = Noon();
        string start = Clone(state);
        var job = TravelTiming.Begin(state, 1.4, 2)!;
        for (int i = 0; i < 50; i++) Assert.False(job.Step(0));
        Assert.Equal(start, Clone(state));
        var reference = SaveCodec.Read(start).State!;
        Assert.True(TravelTiming.Apply(reference, 1.4, 3));
        var restarted = TravelTiming.Begin(state, 1.4, 3)!;
        while (!restarted.Step(0)) { }
        Assert.Equal(start, Clone(state));
        Assert.True(restarted.Commit());
        Assert.Equal(Clone(reference), Clone(state));
        Assert.Null(TravelTiming.Begin(state, 1.4, 3));
    }

    [Fact]
    public void ACityChangeBetweenSlicesRefusesTheCommitAndKeepsTheOldJourneys()
    {
        foreach (var change in new Action<IEnumerable<CitizenState>>[]
        {
            s => s.First(x => x.Phase == CitizenPhase.Travelling && x.MealOuting == null).ActivityUntilMinute += 1,
            s => { var t = s.First(x => x.Phase == CitizenPhase.Travelling); t.NextEventMinute += .5; },
            s => { var t = s.Last(x => x.Phase == CitizenPhase.Travelling); t.Journey = new Route { Legs = t.Journey!.Legs.Select(l => new RouteLeg { From = l.From, To = l.To, Minutes = l.Minutes, Mode = l.Mode, ServiceId = l.ServiceId }).ToList() }; },
        })
        {
            var state = Noon();
            Clone(state);
            var job = TravelTiming.Begin(state, 1.4, 2)!;
            for (int i = 0; i < 400; i++) job.Step(0);
            Assert.Equal(400, job.Prepared);
            change(state.Citizens.Take(job.Prepared));
            var journeys = state.Citizens.Select(c => c.Journey).ToList();
            var events = state.Citizens.Select(c => c.NextEventMinute).ToList();
            while (!job.Step(0)) { }
            Assert.False(job.Commit());
            Assert.True(journeys.Zip(state.Citizens, (j, c) => ReferenceEquals(j, c.Journey)).All(same => same));
            Assert.Equal(events, state.Citizens.Select(c => c.NextEventMinute));
            Assert.Equal(1, state.TravelClockRate);
        }
    }

    [Fact]
    public void ModUpdatesNeverAdvanceAMinuteWhileARetimeIsPendingAndKeepFindingEventsAfterwards()
    {
        var mod = new ModUpdates(true);
        double minute = mod.State.Minute;
        var rates = new double[] { 1, 2, 2, 1, 2, 2, 3 }.Concat(Enumerable.Repeat(2.0, 60)).Concat(Enumerable.Repeat(1.0, 60)).ToArray();
        foreach (double rate in rates)
        {
            minute += .999;
            double before = mod.State.Minute; bool pending = mod.Pending != null;
            mod.Update(rate, minute);
            if (pending && mod.Pending != null) Assert.Equal(before, mod.State.Minute);
        }
        Assert.True(mod.Rebuilds >= 2, mod.Rebuilds.ToString());
        Assert.Equal(1, mod.State.TravelClockRate);
        int events = mod.Events;
        for (int frame = 0; frame < 3600; frame++) { minute += 1.0 / 30; mod.Update(1, minute); }
        Assert.True(mod.Events > events);
        Assert.True(mod.City.ActiveJourneyIndexMatchesState());
        output.WriteLine(mod.Slices + " slices, " + mod.Rebuilds + " rebuilds, " + mod.Restarts + " restarts");
    }
}
