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
            Assert.True(city.BeginAccountingObservations(clock));
            city.AdvanceTo(Math.Max(city.State.Minute, AccountingClock.Target(clock)), 1);
        }
        Assert.DoesNotContain(city.State.Food.Residents, m => m.Reason == "Native clock passed the purchase window");
        Assert.Equal(0, city.HistoricalClockMisses);
    }

    [Fact]
    public void PurchaseInTheLastNativeFloatGapCommitsBeforeTheHourCloses()
    {
        var (city, market, _) = Ready();
        var meal = city.State.Food!.Residents.Single();
        Advance(city, meal.DesiredAt + 6); Assert.Equal(MealStatus.Serving, meal.Status);
        double boundary = (Math.Floor(meal.ServiceAt / 60) + 1) * 60;
        // Move this legacy fixture's reserved service into the otherwise unobservable gap.
        meal.DueAt = meal.ServiceAt = boundary - 1e-10; meal.Deadline = boundary + 20;
        city.State.Food.Shops.Single().NextServiceAt = meal.ServiceAt;
        city.ExtendMealStay(city.State.Citizens.Single(), boundary + 15);
        double native = boundary - 1 + AccountingClock.Limit(true, boundary - 1, 120);
        market.EarliestMinute = Math.Floor(native / 60) * 60;
        Advance(city, native); Assert.Empty(market.Orders);
        double target = AccountingClock.Target(native);
        Assert.True(native < meal.ServiceAt && meal.ServiceAt < target);
        Assert.Equal(0, AccountingClock.Limit(city.IsCaughtUpTo(target), native, 120));
        Advance(city, target, 1);
        Assert.Single(market.Orders); Assert.Equal(meal.ServiceAt, market.Times.Single());
        Assert.Equal(MealStatus.Paid, meal.Status); Assert.Equal(meal.Price, meal.Spent);
        Assert.True(AccountingClock.Limit(city.IsCaughtUpTo(target), native, 120) > 0);
        Assert.Equal(0, city.HistoricalClockMisses);
    }
    [Fact]
    public void ExactPredecessorWithAnUnfinishedMealIsNotADrainedEndingEpoch()
    {
        var (city, market, routes) = Ready(); var meal = city.State.Food!.Residents.Single();
        Advance(city, meal.DesiredAt + 6); Assert.Equal(MealStatus.Serving, meal.Status);
        double boundary = (Math.Floor(meal.ServiceAt / 60) + 1) * 60;
        double predecessor = AccountingClock.PreviousEpochTarget(boundary);
        meal.DueAt = meal.ServiceAt = predecessor; meal.Deadline = boundary + 20;
        city.State.Food.Shops.Single().NextServiceAt = predecessor;
        city.ExtendMealStay(city.State.Citizens.Single(), boundary + 15);
        city.State.Minute = predecessor;
        // A bypassing native writer or old snapshot can have equal minutes but unfinished work.
        var detached = new CitySimulator(city.State, routes);
        Assert.True(AccountingClock.Compatible(predecessor, boundary));
        Assert.False(detached.IsAccountingEpochDrained(boundary)); // Before market attachment: saved timestamps.
        Assert.False(detached.BeginAccountingObservations(boundary));
        detached.AttachFoodMarket(market);
        Assert.False(detached.IsAccountingEpochDrained(boundary)); // After attachment: scheduler roots.
        meal.DueAt = meal.ServiceAt = boundary;
        Assert.True(detached.IsAccountingEpochDrained(boundary)); // Work in the new epoch is allowed.
        Assert.False(detached.IsCaughtUpTo(boundary));
        Assert.Empty(market.Orders);
    }
}

public sealed class AccountingBacklogTests
{
    [Fact]
    public void Writer25ResumesTheRoundingGapWithoutPersistedReadinessFlags()
    {
        var state = EconomyScenario.Create(1, 7); state.Food = EconomyScenario.Food();
        // The shared scenario starts at 05:00; use its real codec-valid city and no new meals.
        var routes = new EmptyRoutes(); var market = new EmptyMarket();
        var city = new CitySimulator(state, routes); city.AttachFoodMarket(market); city.EnableHouseholdFinances();
        double native = state.Minute + AccountingClock.Limit(true, state.Minute, 1);
        double target = AccountingClock.Target(native);
        while (!city.AdvanceTo(target, 1).CaughtUp) { }
        string before = PersistentCity.Persistence.SaveCodec.Write(city.State);
        var read = SaveWire.Read(before);
        var restoredState = read.State!;
        Assert.Equal(CityState.CurrentSchema, restoredState.Schema); Assert.Equal(before, PersistentCity.Persistence.SaveCodec.Write(restoredState));
        Assert.True(AccountingClock.Compatible(restoredState.Minute, native));
        var beforeMarket = new CitySimulator(restoredState, routes);
        Assert.True(beforeMarket.IsAccountingEpochDrained(Math.Floor(native) + 1));
        var restored = new CitySimulator(restoredState, routes); restored.AttachFoodMarket(market);
        Assert.True(restored.IsCaughtUpTo(target));
        Assert.True(AccountingClock.Limit(restored.IsCaughtUpTo(target), native, 1) > 0);
    }
    private sealed class EmptyRoutes : IRoutePlanner
    {
        public Route? Plan(Place from, Place to, double at, TravelPreferences preferences) => null;
    }
    private sealed class EmptyMarket : IFoodMarket
    {
        public double EarliestMinute => 0;
        public System.Collections.Generic.IReadOnlyList<FoodBusiness> Read(double at) => Array.Empty<FoodBusiness>();
        public FoodSale Buy(ResidentMeal meal, double at) => throw new Exception("No meal in rounding-gap fixture");
    }
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
        Assert.Equal(start + 1, Math.Floor(clock));
        Assert.True(clock < start + 2);
        Assert.True(AccountingClock.Compatible(fenceTarget, clock));
        Assert.False(AccountingClock.Compatible(start - .01, clock));
    }
    [Theory]
    [InlineData(30, 32, 256, 4)] [InlineData(60, 16, 128, 2)] [InlineData(120, 16, 128, 2)]
    public void OrdinaryCapsRetainLegacyPerFrameFloor(int fps, int citizens, int units, double milliseconds)
    {
        var budget = new PopulationFrameBudget(1.0 / fps, false);
        Assert.Equal(citizens, budget.CitizenEvents); Assert.Equal(units, budget.Units); Assert.Equal(milliseconds, budget.Milliseconds);
        var stalled = new PopulationFrameBudget(30, false);
        Assert.Equal(128, stalled.CitizenEvents); Assert.Equal(1024, stalled.Units); Assert.Equal(4, stalled.Milliseconds);
    }
    [Theory]
    [InlineData(30, 32, 256)] [InlineData(60, 16, 128)] [InlineData(120, 16, 128)]
    public void UnityFloatFrameDurationDoesNotRoundUpAWholeExtraWorkUnit(int fps, int citizens, int units)
    {
        var budget = new PopulationFrameBudget((float)(1.0 / fps), false);
        Assert.Equal(citizens, budget.CitizenEvents); Assert.Equal(units, budget.Units);
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
