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
        // The F6h rerun after F6j keeps the choice weights and the F6j extras rule (reserved for Peter).
        var choice = new FoodChoiceSettings();
        Assert.Equal(1, choice.FoodChoicePriceWeight);
        Assert.Equal(1, choice.FoodChoiceHouseholdWeight);
        Assert.Equal((75, 125, .35, .05, 3), (FoodMealExtras.CheaperPercent, FoodMealExtras.SimilarPercent,
            FoodMealExtras.CheaperChance, FoodMealExtras.SimilarChance, FoodMealExtras.MaximumExtras));
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

    static readonly FixtureProfile NonBindingNative = new("non-binding-6000-native-menu", 6000, EconomyFixture.NativeMenu);

    [Fact]
    public void PriceProfileIsMatchedAcrossShopsAndArmsAndCanonicalBytesStayUnchanged()
    {
        var market = EconomyFixture.Build(3, 8, profile: NonBindingNative).Market;
        Assert.All(market.Shops, s => Assert.Equal(6000, s.MealsPerHour));
        foreach (var (product, cents) in EconomyFixture.NativeMenu)
        {
            Assert.All(market.Shops, s => Assert.Equal(cents, s.Prices[product]));
            Assert.Equal(cents, market.RivalShop.Prices[product]);
        }
        // Menu +50% scales from the profile's own menu; revert restores it exactly.
        var menu = InterventionArms.Price(NonBindingNative).Single(a => a.Name == "menu-plus-50");
        Assert.Equal(new[] { 1875, 1386, 2504 }, menu.Inputs.Take(3).Select(i => i.Value));
        Assert.Equal(EconomyFixture.NativeMenu.Select(m => m.Cents), menu.Inputs.Skip(3).Select(i => i.Value));
        // The canonical fixture's price arms and metric bytes are unchanged by the profile seam.
        Assert.Equal(InterventionArms.Price(FixtureProfile.CapacityBound).SelectMany(a => a.Inputs),
            InterventionArms.All().Where(a => a.Family == "price").SelectMany(a => a.Inputs));
        var canonical = EconomyRun.Run(new RunOptions { Integrated = true, Seed = 3, Residents = 8, Days = 1 });
        Assert.DoesNotContain("\"profile\"", canonical.Json);
        Assert.Contains("\"mealsPerHour\":60", canonical.Json);
        var profiled = EconomyRun.Run(new RunOptions { Integrated = true, Seed = 3, Residents = 8, Days = 1, Profile = NonBindingNative });
        Assert.Contains("\"name\":\"non-binding-6000-native-menu\"", profiled.Json);
    }

    [Fact]
    public void NonBindingMenuMarkupCostsDemandWithoutWalletExhaustion()
    {
        // Representative of the six-week non-binding native-menu arm on the F6j base (-27.2% meals, -26.5% items,
        // +10.8% revenue, refusals 2.03% -> 2.13%; post-F6i -26.0% / +11.2%). Four days, 128 residents + 128 visitors; +50% from day 2.
        var markup = EconomyFixture.NativeMenu.Select(m => new InterventionInput(2 * 1440, InputKind.ProductPrice,
            EconomyFixture.Shop, m.Product, (int)Math.Round(m.Cents * 1.5, MidpointRounding.AwayFromZero))).ToArray();
        RunResult Run(InterventionInput[] inputs) => EconomyRun.Run(new RunOptions {
            Integrated = true, Seed = 3, Residents = 128, Days = 4, Profile = NonBindingNative, Inputs = inputs });
        var control = Run(Array.Empty<InterventionInput>());
        var high = Run(markup);
        Assert.Equal(control.Days[0].OwnPurchases, high.Days[0].OwnPurchases);
        double Sum(RunResult r, Func<DayMetrics, long> f) => r.Days.Skip(1).Sum(f);
        double q0 = Sum(control, d => d.OwnPurchases), q1 = Sum(high, d => d.OwnPurchases);
        double r0 = Sum(control, d => d.OwnRevenueUnits), r1 = Sum(high, d => d.OwnRevenueUnits);
        double Refused(RunResult r) => Sum(r, d => d.Shops.Values.Sum(s => s.Price)) / Sum(r, d => d.Shops.Values.Sum(s => s.Considered));
        Assert.True(q1 < q0 * .85, $"markup must cost at least 15% quantity: {q0} -> {q1}");
        Assert.True(r1 < r0 * 1.25, $"revenue must rise well under one-for-one: {r0} -> {r1}");
        Assert.True(Refused(high) - Refused(control) < .01, $"affordability refusals {Refused(control):P2} -> {Refused(high):P2}");
        Assert.True(high.Days.Skip(1).Sum(d => d.HomeMeals) > control.Days.Skip(1).Sum(d => d.HomeMeals));
        Assert.All(control.Days.Concat(high.Days), d => Assert.True(d.Shops.Values.Max(s => s.Hours.Max()) < 6000));
        // F6j: the measured quantity and revenue are whole tickets. Items include same-visit extras, so they
        // exceed meals (visits) in both arms; capacity is judged on meals per hour.
        double Meals(RunResult r) => Sum(r, d => d.OwnMealHours.Values.Sum(h => h.Sum()));
        Assert.True(q0 > Meals(control) && q1 > Meals(high), $"extras must be in the ticket: items {q0}/{q1}, meals {Meals(control)}/{Meals(high)}");
        Assert.True(Meals(high) < Meals(control) * .85, $"markup must cost at least 15% of meals: {Meals(control)} -> {Meals(high)}");
        Assert.All(control.Days.Concat(high.Days), d => Assert.True(d.OwnMealHours.Values.Select(h => h.Max()).DefaultIfEmpty(0).Max() < 6000));
    }
}
