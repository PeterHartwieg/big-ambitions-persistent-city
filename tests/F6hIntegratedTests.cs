using System;
using System.Linq;
using System.Reflection;
using PersistentCity.Core;
using PersistentCity.EconomyInterventions;
using Xunit;

namespace PersistentCity.Tests;

// Representative seam/invariant checks only. The 85 six-week sweeps remain opt-in in the tool.
public sealed class F6hIntegratedTests
{
    [Fact]
    public void ExistingDefaultsStayPinnedUntilTradeoffGateCanPass()
    {
        Assert.Equal(34, CityState.CurrentSchema);
        var rivals = new RivalFoodSettings();
        Assert.Equal(2, rivals.Lanes("fastfood"));
        Assert.Equal(60, rivals.MealPeriodUnits("fastfood"));
        Assert.Equal(5, rivals.RivalAssumedServiceMinutes);
        Assert.Equal(.5, CityBalance.JobLossPerMiss);
        var prices = new OrdinaryVisitPrices();
        Assert.Equal(Money.FromCents(500), prices.Price(OrdinaryVisitClass.Retail));
        Assert.Equal(Money.FromCents(1200), prices.Price(OrdinaryVisitClass.Leisure));
    }

    [Fact]
    public void LaneArmChangesAttachedCoreSettingsAndRevertsWithoutChangingUnits()
    {
        var (state, market) = EconomyFixture.Build(3, 8);
        var city = EconomyFixture.Attach(state, market, new EconomyRoutes(market), fresh: true);
        var food = (FoodCommerce)typeof(CitySimulator).GetField("food", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(city)!;
        Assert.Same(market.RivalSettings, food.RivalSettings);
        var arm = InterventionArms.All().Single(a => a.Name == "lanes-halved");
        market.Apply(arm.Inputs[0], (long)arm.Inputs[0].Minute);
        Assert.Equal(1, food.RivalSettings.Lanes("fastfood"));
        Assert.Equal(60, food.RivalSettings.MealPeriodUnits("fastfood"));
        market.Apply(arm.Inputs[1], (long)arm.Inputs[1].Minute);
        Assert.Equal(2, food.RivalSettings.Lanes("fastfood"));
    }

    static RunOptions Small(double? reload = null, Fault fault = Fault.None) => new() {
        Integrated = true, Seed = 3, Residents = 8, Days = 7, ReloadAt = reload,
        Fault = fault, FaultDay = 4,
        Inputs = new[] {
            new InterventionInput(3 * 1440, InputKind.ProductPrice, EconomyFixture.Shop, InterventionArms.Product, 1000),
            new InterventionInput(5 * 1440, InputKind.ProductPrice, EconomyFixture.Shop, InterventionArms.Product, 800) }
    };

    [Fact]
    public void IntegratedWagesOrdinaryRivalsAndJournalSurviveInterruptedReload()
    {
        var run = EconomyRun.Run(Small());
        var reloaded = EconomyRun.Run(Small(4 * 1440 + 720.5));
        Assert.Equal(1, reloaded.Reloads); Assert.True(reloaded.InFlightAtReload > 0);
        Assert.Equal(run.Json, reloaded.Json);
        Assert.Equal(run.FinalSaveSha256, reloaded.FinalSaveSha256);
        Assert.Equal(7 * 16, run.ConservationChecks);
        Assert.True(run.Days[^1].OrdinaryDiscretionaryUnits > 0);
        Assert.True(run.Days[^1].OutsideLedgerUnits > 0);
        Assert.True(run.JournalEntries > 0);
        Assert.Contains("persistentcity-f6h-metrics-1", run.Json);
        Assert.Contains("estimated (modeled service limits)", run.Json);
        Assert.All(run.Days, d => { Assert.Equal(0, d.DuplicateOrders); Assert.Equal(0, d.HistoricalClockMisses); });
    }

    [Theory]
    [InlineData(Fault.DuplicatedDebit)]
    [InlineData(Fault.UnbalancedDebit)]
    [InlineData(Fault.DuplicateNativeOrder)]
    public void IntegratedAccountingFaultsTurnTheRunRed(Fault fault)
        => Assert.Throws<LedgerViolation>(() => EconomyRun.Run(Small(fault: fault)));
    [Fact]
    public void RepresentativeCadenceAndBudgetIdentity()
    {
        var reference = EconomyRun.Run(new RunOptions { Integrated = true, Seed = 3, Residents = 8, Days = 2 });
        foreach (int fps in new[] { 30, 60, 120 })
            foreach (int budget in new[] { 2, 256 })
            {
                var run = EconomyRun.Run(new RunOptions { Integrated = true, Seed = 3, Residents = 8, Days = 2,
                    FramesPerSecond = fps, EventUnitsPerFrame = budget, MillisecondsPerFrame = budget == 2 ? .1 : 4 });
                Assert.Equal(reference.Json, run.Json);
                Assert.Equal(reference.FinalSaveSha256, run.FinalSaveSha256);
                Assert.True(run.Frames > 0);
            }
    }

}
