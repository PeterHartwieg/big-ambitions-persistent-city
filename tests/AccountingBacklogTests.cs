using System;
using System.Linq;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed partial class FoodCommerceTests
{
    [Fact]
    public void CoordinatedOrdinaryClockPreservesInFlightDemandAcrossTheHour()
    {
        var (city, market, _) = Ready(60);
        double desired = city.State.Food!.Residents.Min(m => m.DesiredAt);
        Advance(city, desired + 6);
        int active = city.State.Food.Residents.Count(m => m.Status == MealStatus.Serving || m.Status == MealStatus.Travelling);
        Assert.True(active > 0);
        double target = (Math.Floor(city.State.Minute / 60) + 1) * 60;
        double clock = city.State.Minute;
        int hour = (int)(clock / 60); float minute = (float)(clock % 60);
        while (!city.IsCaughtUpTo(target))
        {
            float step = AccountingClock.Limit(city.IsCaughtUpTo(AccountingClock.Target(clock)), clock, (float)(target - clock));
            minute += step; if (minute >= 60) { minute -= 60; hour++; }
            clock = hour * 60.0 + minute;
            market.EarliestMinute = Math.Floor(clock / 60) * 60;
            city.AdvanceTo(Math.Max(city.State.Minute, AccountingClock.Target(clock)), 1);
        }
        Assert.DoesNotContain(city.State.Food.Residents, m => m.Reason == "Native clock passed the purchase window");
        Assert.Equal(0, city.HistoricalClockMisses);
    }
}

public sealed class AccountingBacklogTests
{
    [Theory]
    [InlineData(30, 1, .01)] [InlineData(60, 16, .5)] [InlineData(120, 128, 2)]
    public void TimestampedDayReconcilesOrdersMissesWalletsAndReloads(int fps, int units, double milliseconds)
    {
        var reference = AccountingScenario.Run(48, 60, 128, 2);
        var actual = AccountingScenario.Run(48, fps, units, milliseconds, reload: true, stallFrames: 30);
        Assert.Equal(reference.Snapshot, actual.Snapshot);
        Assert.Equal(reference.Orders, actual.Orders);
        Assert.Equal(reference.Purchases, actual.Purchases);
        Assert.Equal(reference.SpentUnits, actual.SpentUnits);
        Assert.Equal(reference.MissedMeals, actual.MissedMeals); Assert.Equal(reference.BalanceUnits, actual.BalanceUnits); Assert.Equal(reference.HeldUnits, actual.HeldUnits);
        Assert.True(actual.Orders.Length > 0); Assert.True(actual.Held > 0);
        Assert.InRange(actual.MaxLag, 0, AccountingClock.MaximumLagMinutes);
        Assert.True(actual.Reloads >= 2); Assert.True(actual.RecoverySeconds > 0);
    }
    [Fact]
    public void SkipHasTheSameEpochInputsAndEconomyAsOrdinaryPlay()
    {
        var ordinary = AccountingScenario.Run(48, 60, 16, .5);
        var skip = AccountingScenario.Run(48, 60, 1, .01, reload: true, skip: true);
        Assert.Equal(ordinary.Snapshot, skip.Snapshot); Assert.Equal(ordinary.Orders, skip.Orders);
    }
    [Theory]
    [InlineData(0)] [InlineData(59)] [InlineData(1439)] [InlineData(2879)] [InlineData(100000139)]
    public void LastNativeFloatIntervalDrainsBeforeCrossingExactlyOnce(double start)
    {
        double clock = start;
        float first = AccountingClock.Limit(true, clock, 120);
        clock = Math.Floor(clock / 60) * 60 + (float)((float)(clock % 60) + first);
        double fenceTarget = AccountingClock.Target(clock);
        Assert.True(fenceTarget < start + 1 && fenceTarget > clock);
        Assert.Equal(0, AccountingClock.Limit(false, clock, 120));
        float crossing = AccountingClock.Limit(true, clock, 120);
        clock = Math.Floor(clock / 60) * 60 + (float)((float)(clock % 60) + crossing);
        Assert.Equal(start + 1, clock);
        Assert.True(AccountingClock.Compatible(fenceTarget, clock));
        Assert.False(AccountingClock.Compatible(start - .01, clock));
    }
    [Theory]
    [InlineData(30, 32, 256, 4)] [InlineData(60, 16, 128, 2)] [InlineData(120, 8, 64, 1)]
    public void OrdinaryCapsProvideEqualWorkPerSecond(int fps, int citizens, int units, double milliseconds)
    {
        var budget = new PopulationFrameBudget(1.0 / fps, false);
        Assert.Equal(citizens, budget.CitizenEvents); Assert.Equal(units, budget.Units); Assert.Equal(milliseconds, budget.Milliseconds);
        var stalled = new PopulationFrameBudget(30, false);
        Assert.Equal(128, stalled.CitizenEvents); Assert.Equal(1024, stalled.Units); Assert.Equal(4, stalled.Milliseconds);
    }
    [Fact]
    public void SustainedAccountingCapsCannotRetimeTravel()
    {
        var tracker = new ClockRateTracker();
        for (int i = 0; i < 4; i++) tracker.Observe(i, 1.0 / 3, 1);
        for (int i = 4; i < 200; i++) tracker.Observe(i, .01, 1, accountingLimited: true);
        Assert.Equal(1.0 / 3, tracker.Rate); Assert.Empty(tracker.Error);
    }
}
