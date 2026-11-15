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
        public bool Bodies = true;
        public int Events, Rebuilds, Slices, Restarts;
        public TravelTiming.TravelRetime? Pending;
        public ModUpdates(bool sliced)
        {
            Sliced = sliced;
            State = Gr5AllocationMeasure.RigCity();
            City = new CitySimulator(State, new Gr5AdvanceSegmentMeasure.Straight());
            City.AttachFoodMarket(new VisitorRoutineTests.Market { Shops = F5gPopulationFillTests.Shops(State) });
            City.AdvanceTo(Math.Floor(State.Minute / 1440) * 1440 + 1440 + 12 * 60, int.MaxValue);
        }
        public void Update(double nativeRate, double target, Action<CityState>? betweenSlices = null)
        {
            double before = State.TravelClockRate; var city = City;
            var stage = ClockRetimeStages.Next(nativeRate, State.TravelClockRate, Bodies);
            if (stage == ClockRetimeStage.None) Pending = null;
            if (stage == ClockRetimeStage.ReleaseBodies) Bodies = false;
            else if (stage == ClockRetimeStage.RetimeAndRebuild)
            {
                if (!Sliced)
                {
                    State.PrepareForSave();
                    TravelTiming.Apply(State, 1.4, nativeRate, out _);
                    City = City.RebuildRoutes(new Gr5AdvanceSegmentMeasure.Straight());
                    Rebuilds++;
                }
                else
                {
                    if (Pending != null && Pending.ClockRate != nativeRate) Pending = null;
                    if (Pending == null) { State.PrepareForSave(); Pending = TravelTiming.Begin(State, 1.4, nativeRate)!; }
                    for (int i = 0; i < 97 && !Pending.Ready; i++) Pending.Step(0);
                    Slices++;
                    if (Pending.Ready)
                    {
                        if (Pending.Commit())
                        {
                            City = City.RebuildRoutes(new Gr5AdvanceSegmentMeasure.Straight());
                            Rebuilds++;
                        }
                        else Restarts++;
                        Pending = null;
                    }
                    else betweenSlices?.Invoke(State);
                }
            }
            else
            {
                Bodies = true;
                for (int unit = 0; unit < 64; unit++)
                {
                    if (City.PlanningPending) { City.ContinuePlanning(1); continue; }
                    var result = City.AdvanceFrameSlice(target, 1);
                    Events += result.Events;
                    if (result.CaughtUp) break;
                }
            }
            Assert.True(State.TravelClockRate == before || !ReferenceEquals(city, City), "journeys were retimed without a rebuilt simulator");
            Assert.True(Pending == null || State.TravelClockRate == before, "a pending retime changed the city before its commit");
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
