using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.EconomyInterventions;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

public sealed class F6iPriceAnchorTests
{
    private readonly ITestOutputHelper output;
    public F6iPriceAnchorTests(ITestOutputHelper output) { this.output = output; }
    static readonly Money Cap = Money.FromCents(1000000);
    static readonly ResidentTraits Neutral = new() { Bands = "2222222222" };
    static EvaluatedFoodOffer Offer(int cents, string business = "sole", double travel = 0)
        => new(business, FoodProducts.Burger, Money.FromCents(cents), travel, 0, 0, 90, null, null, MealKind.Legacy);
    static FoodDrawContext Draw(int seed) => new(seed, "resident", 1, MealKind.Lunch, "anchor", 1);
    static double Weight(int cents, Money allowance, ResidentTraits? traits = null, FoodChoiceSettings? settings = null)
        => FoodChoicePolicy.Weight(Offer(cents), traits ?? Neutral, allowance, settings ?? new FoodChoiceSettings());

    [Theory]
    [InlineData(924, 2)] [InlineData(1025, 2)] [InlineData(1250, 2)] [InlineData(1669, 2)]
    [InlineData(924, 4)] [InlineData(1025, 4)] [InlineData(1250, 4)] [InlineData(1669, 4)]
    public void NativePricesStayComparableToHomeAndMarkupStaysAboveFloor(int cents, int sensitivity)
    {
        var settings = new FoodChoiceSettings();
        var traits = new ResidentTraits { Bands = "222" + sensitivity + "222222" };
        var baseline = Offer(cents);
        // Exact +50%, including a half-cent, as supported by the native money unit.
        var markup = new EvaluatedFoodOffer("sole", FoodProducts.Burger,
            Money.FromUnits(baseline.Price.Units * 3 / 2), 0, 0, 0, 90, null, null, MealKind.Legacy);
        double w0 = FoodChoicePolicy.Weight(baseline, traits, Cap, settings);
        double w1 = FoodChoicePolicy.Weight(markup, traits, Cap, settings);
        int own0 = 0, own1 = 0;
        for (int seed = 0; seed < 4096; seed++)
        {
            if (FoodChoicePolicy.Select(new[] { baseline }, traits, Cap, Draw(seed), settings, true).Index == 0) own0++;
            if (FoodChoicePolicy.Select(new[] { markup }, traits, Cap, Draw(seed), settings, true).Index == 0) own1++;
        }
        output.WriteLine($"native cents {cents}, sensitivity {sensitivity}: weight {w0:R} -> {w1:R}, own/home {own0}/{4096-own0} -> {own1}/{4096-own1}");
        // Comparable to the funded-home weight of 1 at ordinary prices. F6k weight 1.2: band 4 spans .35 ($16.69) to 2.97 ($9.24).
        Assert.InRange(w0, .33, 3);
        Assert.True(w1 > settings.FoodChoiceWeightFloor * 3); // F6k weight 1.2: band 4 at $16.69 keeps .082 (was .124)
        // F6k: weight 1.2 puts the most sensitive band at (2/3)^3.6 = .23 (was .30 at weight 1).
        Assert.InRange(w1 / w0, .2, .5);
        Assert.True(own1 < own0 * .8);
        // The proportional markup penalty is identical at every native price level.
        Assert.Equal(Weight(1875, Cap, traits) / Weight(1250, Cap, traits), w1 / w0, 12);
        // Ordinary travel/time costs must not erase the high-sensitivity price response either.
        double WithTrip(Money price) => FoodChoicePolicy.Weight(new EvaluatedFoodOffer("sole", FoodProducts.Burger,
            price, 5, 0, 20, 90, null, null, MealKind.Legacy), traits, Cap, settings);
        Assert.True(WithTrip(markup.Price) > settings.FoodChoiceWeightFloor);
        Assert.True(WithTrip(markup.Price) < WithTrip(baseline.Price) * .5);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void FiftyPercentMarkupHasMaterialPenaltyAtCapForEveryPriceSensitivity(int sensitivity)
    {
        var traits = new ResidentTraits { Bands = "222" + sensitivity + "222222" };
        double reference = Weight(1250, Cap, traits), markup = Weight(1875, Cap, traits);
        output.WriteLine($"sensitivity {sensitivity}: reference {reference:R}, markup {markup:R}, ratio {markup / reference:R}");
        Assert.Equal(1, reference, 12);
        // F6k weight 1.2: from (2/3)^3.6 = .23 at band 4 to (2/3)^1.2 = .61 at band 0.
        Assert.InRange(markup / reference, .2, .68);
        // Savings cannot dilute the soft price signal, including a tight but affordable wallet.
        Assert.Equal(markup, Weight(1875, Money.FromCents(1875), traits));
        Assert.Equal(reference, Weight(1250, Money.FromCents(1875), traits));
    }

    [Fact]
    public void DeepDiscountDoesNotEraseTasteAtTheUpperWeightBound()
    {
        double neutral = Weight(100, Cap);
        double loved = Weight(100, Cap, new ResidentTraits { Bands = "2422222222" });
        double disliked = Weight(100, Cap, new ResidentTraits { Bands = "2022222222" });
        Assert.InRange(neutral, 1, 4);
        Assert.True(loved > neutral); Assert.True(disliked < neutral);
        Assert.True(loved < 64);
    }

    [Fact]
    public void SoleShopMarkupLosesToFundedHomeWithoutAnyCompetingShop()
    {
        var settings = new FoodChoiceSettings();
        int reference = 0, markup = 0;
        for (int seed = 0; seed < 4096; seed++)
        {
            var a = FoodChoicePolicy.Select(new[] { Offer(1250) }, Neutral, Cap, Draw(seed), settings, true);
            var b = FoodChoicePolicy.Select(new[] { Offer(1875) }, Neutral, Cap, Draw(seed), settings, true);
            if (a.Index == 0) reference++;
            if (b.Index == 0) { markup++; Assert.Equal(0, a.Index); }
            else { Assert.True(b.Outside); Assert.Equal(MealFactor.Household, b.Factor1); }
        }
        output.WriteLine($"sole shop / 4096 draws: reference {reference}, markup {markup}");
        Assert.InRange(reference, 1800, 2300);
        Assert.True(markup < reference * .65);
        Assert.True(Weight(1875, Cap) < Weight(1250, Cap) * .5);
        // An unfunded home is never invented. A sole feasible shop still wins when no outside option exists.
        Assert.Equal(0, FoodChoicePolicy.Select(new[] { Offer(1875) }, Neutral, Cap, Draw(0), settings, false).Index);
        // HouseholdWeight remains a multiplicative preference at the reference-price equivalent.
        settings.FoodChoiceHouseholdWeight = 2;
        int strongerHome = Enumerable.Range(0, 4096).Count(seed =>
            FoodChoicePolicy.Select(new[] { Offer(1250) }, Neutral, Cap, Draw(seed), settings, true).Outside);
        Assert.True(strongerHome > 4096 - reference);
    }

    [Fact]
    public void EqualOffersRankByPriceAndTasteDistancePriceHaveComparableStrength()
    {
        var settings = new FoodChoiceSettings();
        // F6k weight 1.2: a neutral resident's +50% ratio is 1.5^2.4 = 2.65 (was 1.5^2 = 2.25).
        Assert.InRange(Weight(1250, Cap) / Weight(1875, Cap), 2.5, 2.8);
        int cheaper = 0;
        var offers = new[] { Offer(1250, "cheap"), Offer(1875, "dear") };
        for (int seed = 0; seed < 4096; seed++)
            if (FoodChoicePolicy.Select(offers, Neutral, Cap, Draw(seed), settings, false).Index == 0) cheaper++;
        output.WriteLine($"cheaper {cheaper}");
        Assert.InRange(cheaper, 2800, 3050); // F6k weight 1.2: 2925 (was inside 2700-3000 at weight 1)
        double baseline = Weight(1250, Cap);
        double disliked = Weight(1250, Cap, new ResidentTraits { Bands = "2022222222" });
        double distant = FoodChoicePolicy.Weight(Offer(1250, travel: 10), Neutral, Cap, settings);
        Assert.InRange(disliked / baseline, .4, .6);
        Assert.InRange(distant / baseline, .4, .6);
        Assert.InRange(Weight(1875, Cap) / baseline, .3, .5);
        // Both legacy and metadata-backed products use the same meal-scale anchor.
        Assert.Equal(Weight(1875, Cap), FoodChoicePolicy.Weight(Offer(1875), Neutral, Cap, settings, new FoodProductMetadata()));
        var newProduct = new EvaluatedFoodOffer("sole", "f6i:new-product", Money.FromCents(1875), 0, 0, 0, 90,
            null, null, MealKind.Legacy, newTasteBand: ResidentTraits.Neutral);
        Assert.Equal(Weight(1875, Cap), FoodChoicePolicy.Weight(newProduct, Neutral, Cap, settings));
        Assert.Equal(Weight(1875, Cap), FoodChoicePolicy.Weight(newProduct, Neutral, Cap, settings,
            new FoodProductMetadata(MealClass.Food, ResidentTraits.Neutral, 0, 0, 0)));
    }

    [Theory]
    [InlineData(1000000)] [InlineData(750)] [InlineData(500)] [InlineData(0)]
    public void SelectionAndHoldUseSameHardLimitIncludingTightWallet(int allowanceCents)
    {
        var balance = Money.FromCents(allowanceCents + 600);
        var budget = new ResidentBudget { Balance = balance, OpeningBalance = balance,
            DailyHousingCents = 300, DailyEssentialsCents = 100, DailyHomeFoodCents = 200 };
        var allowance = HouseholdFinance.SpendingLimit(budget);
        Assert.Equal(Money.FromCents(allowanceCents), allowance);
        foreach (int cents in new[] { -1, 0, 500, 750, allowanceCents + 1 })
        {
            bool affordable = cents > 0 && cents <= allowanceCents;
            var selected = FoodChoicePolicy.Select(new[] { Offer(cents) }, Neutral, allowance, Draw(0), new FoodChoiceSettings(), false);
            Assert.Equal(affordable ? 0 : -1, selected.Index);
            Assert.Equal(affordable, HouseholdFinance.HoldPurchase(budget, Money.FromCents(cents)));
            Assert.True(HouseholdFinance.CashConserved(budget));
            if (affordable)
            {
                Assert.Equal(Money.Zero, HouseholdFinance.SpendingLimit(budget));
                Assert.False(HouseholdFinance.HoldPurchase(budget, Money.FromCents(1)));
                HouseholdFinance.RefusePurchase(budget);
            }
            Assert.Equal(balance, budget.Balance);
        }
        budget.Arrears = Money.FromCents(1);
        Assert.Equal(Money.Zero, HouseholdFinance.SpendingLimit(budget));
        Assert.False(HouseholdFinance.HoldPurchase(budget, Money.FromCents(1)));
    }

    [Fact]
    public void StrengthKnobChangesSoftPenaltyButNeverAffordability()
    {
        var settings = new FoodChoiceSettings { FoodChoicePriceWeight = 0 };
        Assert.Equal(Weight(1250, Cap, settings: settings), Weight(1875, Cap, settings: settings));
        Assert.Equal(-1, FoodChoicePolicy.Select(new[] { Offer(1875) }, Neutral, Money.FromCents(1250), Draw(0), settings, false).Index);
        settings.FoodChoicePriceWeight = 3;
        var unit = new FoodChoiceSettings { FoodChoicePriceWeight = 1 };
        Assert.True(Weight(1875, Cap, settings: settings) < Weight(1875, Cap, settings: unit) * .2);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ShortWholeMenuMarkupReducesQuantity(bool rivalListed)
    {
        // Three days, one seed, 128 residents + 128 visitors. High own capacity isolates demand;
        // it is a fixture input shared by both arms, not a changed production default.
        // Price levels from retained native reviews; the third legacy product is a salad-price proxy.
        var menu = new[] { (Product: FoodProducts.Burger, Cents: 1250),
            (Product: FoodProducts.Croissant, Cents: 924), (Product: FoodProducts.HotDog, Cents: 1669) };
        var common = new[] { new InterventionInput(EconomyFixture.StartMinute + 1,
            InputKind.MealsPerHour, EconomyFixture.Shop, "", 6000) }.Concat(menu.SelectMany(m => new[] {
                new InterventionInput(EconomyFixture.StartMinute + 1, InputKind.ProductPrice, EconomyFixture.Shop, m.Product, m.Cents),
                new InterventionInput(EconomyFixture.StartMinute + 1, InputKind.RivalPrice, EconomyFixture.RivalBusiness, m.Product, m.Cents) }));
        var price = menu.Select(m => new InterventionInput(2 * 1440, InputKind.ProductPrice,
            EconomyFixture.Shop, m.Product, (m.Cents * 3 + 1) / 2));
        bool measure = PerfGate.Variable("PC_F6I_MEASURE") == "1";
        RunResult Run(IEnumerable<InterventionInput> inputs) => EconomyRun.Run(new RunOptions {
            Integrated = true, Seed = 3, Residents = 128, Days = 3, RivalListed = rivalListed,
            Inputs = inputs.ToArray(), MeasureSlices = measure });
        var control = Run(common); var markup = Run(common.Concat(price));
        Assert.Equal(control.Days[0].OwnPurchases, markup.Days[0].OwnPurchases);
        long q0 = control.Days.Skip(1).Sum(d => d.OwnPurchases), q1 = markup.Days.Skip(1).Sum(d => d.OwnPurchases);
        long h0 = control.Days.Skip(1).Sum(d => d.HomeMeals), h1 = markup.Days.Skip(1).Sum(d => d.HomeMeals);
        output.WriteLine($"rival {rivalListed}: own {q0} -> {q1}, home {h0} -> {h1}, quantity change {(q1 / (double)q0 - 1):P3}, arc elasticity {((q1 - q0) / ((q1 + q0) / 2.0) / .4):F6}");
        foreach (var run in new[] { control, markup })
        {
            output.WriteLine($"tool seconds {run.Seconds:F6}, allocated bytes {run.AllocatedBytes}, daily owner checks {run.ConservationChecks}");
            Assert.Equal(3 * 256, run.ConservationChecks);
            Assert.All(run.Days, d => { Assert.Equal(0, d.DuplicateOrders); Assert.Equal(0, d.HistoricalClockMisses); });
            if (measure) Report("bounded advancement", run.SliceMilliseconds.ToArray());
        }
        Assert.True(q1 < q0 * .8, $"+50% menu must lose at least 20% quantity: {q0} -> {q1}");
        if (!rivalListed) Assert.True(h1 > h0);
    }

    [Fact]
    public void OptionalPolicyCost()
    {
        if (PerfGate.Variable("PC_F6I_MEASURE") != "1") return;
        var settings = new FoodChoiceSettings();
        var offer = Offer(1875, travel: 5);
        double sum = 0;
        for (int i = 0; i < 5000; i++) sum += FoodChoicePolicy.Weight(offer, Neutral, Cap, settings);
        var samples = new double[2000];
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < samples.Length; i++)
        {
            long begin = Stopwatch.GetTimestamp();
            for (int j = 0; j < 32; j++) sum += FoodChoicePolicy.Weight(offer, Neutral, Cap, settings);
            samples[i] = Stopwatch.GetElapsedTime(begin).TotalMilliseconds;
        }
        long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Report("32 offer weights", samples);
        output.WriteLine($"policy allocated bytes {bytes}; checksum {sum:R}");
        Assert.Equal(0, bytes); Assert.True(sum > 0);
    }

    void Report(string component, double[] samples)
    {
        Array.Sort(samples);
        output.WriteLine($"{component}: samples {samples.Length}, p95 {samples[(int)(samples.Length * .95)]:F6} ms, max {samples[^1]:F6} ms, >=30ms {samples.Count(s => s >= 30)}");
    }
}
