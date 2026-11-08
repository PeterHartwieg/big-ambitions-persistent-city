using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>
/// F7c rows of the plan's section 3.8 interrupted-state matrix. Each case captures a snapshot at the named in-flight
/// state, lets the live city move on after the capture (as the game does once the save callback returns), encodes the
/// earlier snapshot, reloads it and continues under another frame cadence. The result must equal an unbroken run.
/// Rows owned by F7b (re-entrancy, failure markers, detach) and F7d (held context, receipts) are tested there.
/// </summary>
public sealed class SaveSnapshotInterruptedStateTests
{
    [Fact]
    public void PendingRouteAnswerIsNotSavedAsFinishedAndReplaysFromItsTimestamp()
    {
        var reference = new CitySimulator(ResumablePlanningTests.City(48), new ResumablePlanningTests.SyncPlanner());
        var planner = new ResumablePlanningTests.DeferredPlanner { ContinuesFor = index => 1 + index % 3 };
        var city = new CitySimulator(ResumablePlanningTests.City(48), planner);
        int checkpoints = 0, pending = 0;
        for (int day = 1; day <= 3; day++)
        {
            double target = ResumablePlanningTests.Target(day);
            reference.AdvanceTo(target);
            int guard = 0;
            while (true)
            {
                Assert.True(++guard < 10_000_000);
                if (city.PlanningPending)
                {
                    if (++pending % 5 == 0)
                    {
                        var request = planner.Seen[^1];
                        var snapshot = SaveCodec.Capture(city.State);
                        var held = snapshot.Wire.Citizens.Single(c => c.Identity.Id == request.Citizen!.Identity.Id);
                        Assert.Equal(snapshot.Minute, held.NextEventMinute); // the unstarted event, at the held clock
                        var phase = held.Phase;
                        // The live city receives the answer after the capture; the snapshot keeps the unstarted event.
                        while (city.PlanningPending) city.ContinuePlanning(0);
                        Assert.Equal(phase, held.Phase);
                        var read = SaveCodec.Read(SaveCodec.Encode(snapshot));
                        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
                        city = new CitySimulator(read.State!, planner); // the route-input lease and the job are transient
                        Assert.False(city.PlanningPending);
                        checkpoints++;
                        continue;
                    }
                    city.ContinuePlanning(0);
                    continue;
                }
                if (city.AdvanceFrameSlice(target, 1).CaughtUp) break;
            }
            city.SynchronizeSnapshot();
            Assert.Equal(SaveCodec.Write(reference.State), SaveCodec.Write(city.State));
        }
        Assert.True(checkpoints > 5, "too few mid-request checkpoints: " + checkpoints);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void MidnightPostingUnitsCapturedPartWayRebuildTheirQueueFromTimestamps(int units)
    {
        const double Midnight = FoodBudgetBatchTests.Midnight;
        var resumed = FoodBudgetBatchTests.Start(legacy: false, count: 100);
        var reference = FoodBudgetBatchTests.Start(legacy: false, count: 100);
        FoodBudgetBatchTests.Advance(resumed, Midnight - 1, 128); FoodBudgetBatchTests.Advance(reference, Midnight - 1, 128);
        int posted = units * FoodCommerce.MaximumBudgetPostingsPerUnit; // 32, 64, 96 of 100
        while (FoodBudgetBatchTests.Posted(resumed.City) != posted) Assert.False(resumed.City.AdvanceFrameSlice(Midnight, 1).CaughtUp);
        var snapshot = SaveCodec.Capture(resumed.City.State);
        Assert.Equal(posted, snapshot.Wire.Food!.Residents.Count(m => m.Budget!.LastPostedDay == 2));
        // The live city posts the next unit after the capture; no meal or order runs before every posting.
        int orders = resumed.Market.Orders.Count;
        Assert.False(resumed.City.AdvanceFrameSlice(Midnight, 1).CaughtUp);
        Assert.True(FoodBudgetBatchTests.Posted(resumed.City) > posted);
        Assert.Equal(orders, resumed.Market.Orders.Count);
        string partial = SaveCodec.Encode(snapshot);
        Assert.Equal(posted, SaveCodec.Read(partial).State!.Food!.Residents.Count(m => m.Budget!.LastPostedDay == 2));
        var read = SaveCodec.Read(partial); Assert.Equal("", read.Error);
        resumed.City = new CitySimulator(read.State!, resumed.Routes);
        resumed.City.AttachFoodMarket(resumed.Market);
        FoodBudgetBatchTests.Attach(resumed);
        FoodBudgetBatchTests.Advance(resumed, Midnight + 700, 1); FoodBudgetBatchTests.Advance(reference, Midnight + 700, 128);
        Assert.Equal(SaveCodec.Write(reference.City.State), SaveCodec.Write(resumed.City.State));
        Assert.Equal(reference.Market.Orders, resumed.Market.Orders);
    }

    private static void Run(CitySimulator city, double target, int slice)
    {
        while (!city.IsCaughtUpTo(target)) city.AdvanceFrameSlice(target, slice);
        city.SynchronizeSnapshot();
    }

    private static CitySimulator Reload(string payload)
    {
        var read = SaveCodec.Read(payload); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var city = new CitySimulator(read.State!, new RepresentativeFixture.Routes());
        city.AttachFoodMarket(new RepresentativeFixture.Market());
        return city;
    }

    [Fact]
    public void MidJourneyCheckpointContinuesLikeTheUnbrokenCityUnderAnotherCadence()
    {
        var reference = F7cFixtures.Build("rush-957x2");
        var resumed = F7cFixtures.Build("rush-957x2");
        var snapshot = SaveCodec.Capture(resumed.State);
        Assert.True(snapshot.SharedRoutes > 100, "too few committed journeys in flight: " + snapshot.SharedRoutes);
        Assert.Contains(snapshot.Wire.VisitorRoutines, r => r.Journey != null);
        double target = F7cFixtures.RushMinute + 4 * 60;
        // The live city keeps playing after the save; then the process stops and the save is loaded.
        Run(resumed.City, F7cFixtures.RushMinute + 60, 64);
        var city = Reload(SaveCodec.Encode(snapshot));
        Run(city, target, 1);
        Run(reference.City, target, 64);
        Assert.Equal(SaveCodec.Write(reference.State), SaveCodec.Write(city.State));
    }

    [Fact]
    public void PhysicalLeaseProgressIsCapturedAndTheLoadedJourneyResumesInBackground()
    {
        var built = F7cFixtures.Build("rush-957x2");
        var city = built.City; var state = built.State;
        // F2c: a walking journey with a visible body is not leased; the capture is its logical state.
        var person = state.Citizens.First(c => c.Journey != null && c.Phase == CitizenPhase.Travelling
            && c.Journey.Legs.All(l => l.Mode == TravelMode.Walk));
        Assert.False(city.TryAcquirePhysicalJourney(person.Identity.Id, out _));
        string atCapture = SaveCodec.Write(state);
        var snapshot = SaveCodec.Capture(state);
        double departure = person.DepartureMinute;
        // After the capture the city keeps moving.
        double later = state.Minute + 2;
        while (!city.IsCaughtUpTo(later)) city.AdvanceFrameSlice(later, 64);
        string payload = SaveCodec.Encode(snapshot);
        Assert.Equal(atCapture, payload);
        var loaded = Reload(payload);
        var restored = loaded.State.Citizens.Single(c => c.Identity.Id == person.Identity.Id);
        Assert.Equal(departure, restored.DepartureMinute);
        Assert.Equal(CitizenPhase.Travelling, restored.Phase);
        int completed = restored.CompletedJourneys;
        Run(loaded, snapshot.Minute + restored.Journey!.Minutes + 30, 64);
        Assert.True(restored.CompletedJourneys > completed || restored.Phase != CitizenPhase.Travelling);
    }
}
