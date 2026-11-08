using System;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed partial class TrafficClosureRefreshTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void BoundaryReopeningUsesPublishedEpochRatherThanDrainedPredecessor(bool publish)
    {
        var planner = new Planner(); var city = Engine(planner);
        planner.Closed.Add(0); city.Traffic!.SetLinkClosed(0, true);
        city.Traffic.Enqueue("blocked", Route(0, 1));
        double predecessor = AccountingClock.PreviousEpochTarget(1500);
        while (!city.AdvanceTo(predecessor).CaughtUp) { }
        Assert.True(city.IsAccountingEpochDrained(1500));
        if (publish) Assert.True(city.BeginAccountingObservations(1500));
        planner.Closed.Clear(); city.RefreshTrafficClosures();
        Assert.Equal(publish ? 1500 : predecessor, city.Traffic.NextEventMinute);
        Assert.Equal(publish, city.IsAccountingEpochDrained(1500));
        if (publish)
        {
            Assert.False(city.IsCaughtUpTo(1500));
            while (!city.AdvanceTo(1500).CaughtUp) { }
            Assert.True(city.IsAccountingEpochDrained(1500));
        }
    }
}
public sealed partial class PhysicalJourneyTests
{
    // F2c: a body observation can no longer create arrival work at an epoch boundary, because walk legs are never
    // leased. The arrival is the logical one, at its own minute, before and after a boundary save.
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void BeforeAdvanceObservationCannotCreateArrivalWork(bool publish)
    {
        var city = Engine(); var c = city.State.Citizens[0];
        Assert.False(city.TryAcquirePhysicalJourney(c.Identity.Id, out _));
        city.AdvanceTo(AccountingClock.PreviousEpochTarget(1500));
        Assert.True(city.IsAccountingEpochDrained(1500));
        if (publish) Assert.True(city.BeginAccountingObservations(1500));
        Assert.True(city.IsAccountingEpochDrained(1500));
        Assert.Equal(1, c.CompletedJourneys);
        Assert.Equal(1452, Assert.Single(c.RecentVisits).AtMinute);
    }
    [Fact]
    public void BeforeSaveAtBoundaryReloadsTheLogicalArrival()
    {
        var city = Engine();
        city.AdvanceTo(AccountingClock.PreviousEpochTarget(1500));
        Assert.True(city.BeginAccountingObservations(1500));
        string payload = SaveCodec.Write(city.State);
        var read = SaveCodec.Read(payload); Assert.Equal(SaveReadStatus.Loaded, read.Status);
        var restored = new CitySimulator(read.State!, new Planner());
        Assert.True(AccountingClock.Compatible(restored.State.Minute, 1500));
        Assert.True(restored.IsAccountingEpochDrained(1500));
        restored.AdvanceTo(1500, 1); restored.AdvanceTo(1500, 1);
        Assert.Equal(1, restored.State.Citizens[0].CompletedJourneys);
        Assert.Equal(1452, Assert.Single(restored.State.Citizens[0].RecentVisits).AtMinute);
    }
}
