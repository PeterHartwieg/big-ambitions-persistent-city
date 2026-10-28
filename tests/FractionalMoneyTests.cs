using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Compatibility;
using Xunit;

namespace PersistentCity.Tests;

public sealed class FractionalMoneyTests
{
    [Theory]
    [InlineData(.01f)] [InlineData(.1f)] [InlineData(7.3f)] [InlineData(10.2528f)]
    [InlineData(9999.999f)] [InlineData(10000f)]
    public void NativePriceRetainsItsBinary32Identity(float price)
    {
        decimal cents = FoodMoney.FromNative(price);
        Assert.Equal(BitConverter.SingleToInt32Bits(price), BitConverter.SingleToInt32Bits((float)(cents / 100m)));
        Assert.Equal(cents, decimal.Round(cents, 12));
    }

    [Theory]
    [InlineData(0f)] [InlineData(-1f)] [InlineData(float.NaN)] [InlineData(float.PositiveInfinity)]
    [InlineData(float.Epsilon)] [InlineData(10001f)]
    public void UnsupportedNativePriceIsRefusedWithoutAnInventedCharge(float price)
        => Assert.Throws<ArgumentOutOfRangeException>(() => FoodMoney.FromNative(price));

    [Fact]
    public void The109SaleRegressionConservesWalletHoldsReceiptsAndNativePrice()
    {
        const float native = 10.2528f;
        decimal cents = FoodMoney.FromNative(native);
        Assert.Equal(1025.279998779297m, cents);
        var budget = new ResidentBudget { OpeningBalanceCents = 200000, BalanceCents = 200000 };
        var data = new Dictionary<string, string>();
        decimal receipts = 0;
        for (int i = 0; i < 109; i++)
        {
            var meal = new ResidentMeal { ResidentId = "c" + i, Day = 5, Kind = MealKind.Lunch,
                BusinessId = "shop", ProductId = FoodProducts.Croissant, PriceCents = cents };
            Assert.True(HouseholdFinance.HoldPurchase(budget, cents));
            FoodSaleReceipts.Begin(data, meal); FoodSaleReceipts.Confirm(data, meal);
            Assert.True(FoodSaleReceipts.AlreadyPaid(new Dictionary<string, string>(data), meal));
            HouseholdFinance.ConfirmPurchase(budget); receipts += cents;
        }
        Assert.Equal(receipts, budget.PreparedFoodSpentCents);
        Assert.Equal(receipts, budget.OpeningBalanceCents - budget.BalanceCents);
        Assert.Equal(0, budget.HeldPurchaseCents);
        Assert.True(Math.Abs((double)(receipts / 100m) - (double)native * 109) < 1e-9);
        Assert.True(Math.Abs((double)(1025m * 109 / 100m) - (double)native * 109) > .30);
        decimal before = budget.BalanceCents;
        Assert.True(HouseholdFinance.HoldPurchase(budget, cents));
        HouseholdFinance.RefusePurchase(budget);
        Assert.Equal(before, budget.BalanceCents);
    }

    [Theory]
    [InlineData("paid", FoodReceiptStatus.Paid)] [InlineData("pending", FoodReceiptStatus.Pending)]
    public void LegacyReceiptsKeepTheirExactOldAmountAndBlockDuplicateCheckout(string status, FoodReceiptStatus expected)
    {
        var meal = new ResidentMeal { ResidentId = "old", Day = 5, Kind = MealKind.Lunch,
            BusinessId = "shop", ProductId = FoodProducts.Croissant, PriceCents = 1025 };
        string key = FoodSaleReceipts.LegacyPrefix + meal.IntentId;
        string value = status + "|1025|shop|" + Uri.EscapeDataString(meal.ProductId);
        var data = new Dictionary<string,string> { [key] = value };
        Assert.Equal(expected, FoodSaleReceipts.ReadStatus(data, meal));
        Assert.Throws<InvalidOperationException>(() => FoodSaleReceipts.Begin(data, meal));
        FoodSaleReceipts.Prune(data, 100, new[] { meal.IntentId });
        Assert.Equal(value, data[key]);
        meal.PriceCents = FoodMoney.FromNative(10.2528f);
        Assert.Equal(FoodReceiptStatus.Conflict, FoodSaleReceipts.ReadStatus(data, meal));
        Assert.Equal(value, data[key]);
    }

    [Fact]
    public void AmbiguousReceiptVersionsNeverAuthorizeARepeat()
    {
        var meal = new ResidentMeal { ResidentId = "old", PriceCents = 100, BusinessId = "shop", ProductId = "product" };
        var data = new Dictionary<string,string>();
        FoodSaleReceipts.Begin(data, meal); FoodSaleReceipts.Confirm(data, meal);
        data[FoodSaleReceipts.LegacyPrefix + meal.IntentId] = data.Values.Single();
        Assert.Equal(FoodReceiptStatus.Conflict, FoodSaleReceipts.ReadStatus(data, meal));
        FoodSaleReceipts.Prune(data, 10000); Assert.Equal(2, data.Count);
    }

    [Fact]
    public void PrecisionExhaustionRefusesHoldBeforeChangingFunds()
    {
        var budget = new ResidentBudget { BalanceCents = long.MaxValue, OpeningBalanceCents = long.MaxValue };
        decimal before = budget.BalanceCents;
        Assert.Throws<OverflowException>(() => HouseholdFinance.HoldPurchase(budget, FoodMoney.FromNative(10.2528f)));
        Assert.Equal(before, budget.BalanceCents); Assert.Equal(0, budget.HeldPurchaseCents);
    }

    [Fact]
    public void RloTimingIncludesRefusalsWithoutChangingResult()
    {
        var adapter = new RloIdentityAdapter();
        for (int i = 1; i <= 3; i++)
        {
            Assert.Equal(RloReadStatus.MissingDependency, adapter.Read(null).Status);
            Assert.Equal(i, adapter.Reads);
            Assert.True(adapter.LastReadMilliseconds >= 0);
            Assert.True(adapter.TotalReadMilliseconds >= adapter.MaximumReadMilliseconds);
            Assert.True(adapter.MaximumReadMilliseconds >= adapter.LastReadMilliseconds);
        }
    }
}

public sealed partial class FoodCommerceTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FractionalCheckoutSurvivesInterruptedConfirmationAndReload(bool interrupt)
    {
        var (city, market, routes) = Ready();
        market.Price = FoodMoney.FromNative(10.2528f);
        var person = city.State.Citizens.Single(); person.PlaceId = "shop"; person.Activity = Activity.Shop;
        city = city.RebuildRoutes(routes); city.EnableHouseholdFinances();
        market.ThrowAfterSale = interrupt;
        Advance(city, city.State.Food!.Residents.Single().DesiredAt + 30);
        var meal = city.State.Food.Residents.Single();
        Assert.Single(market.Orders);
        Assert.Equal(interrupt ? MealStatus.Uncertain : MealStatus.Paid, meal.Status);
        string encoded = SaveCodec.Write(city.State);
        city = Reload(city, market, routes);
        Assert.Equal(encoded, SaveCodec.Write(city.State));
        if (interrupt)
        {
            market.ConfirmEvidence = true;
            Advance(city, city.State.Food!.Residents.Single().DueAt);
        }
        meal = city.State.Food!.Residents.Single();
        Assert.Equal(market.Price, meal.SpentCents);
        Assert.Equal(market.Price, meal.Budget!.PreparedFoodSpentCents);
        Assert.Equal(0, meal.Budget.HeldPurchaseCents);
        Assert.Equal(market.Price, city.State.Food.History.Sum(h => h.RevenueCents));
        city = Reload(city, market, routes);
        Assert.Single(market.Orders);
    }

    [Fact]
    public void MigrationStillRejectsLegacyLedgerOverflowAndPreservesThePayload()
    {
        var (city, _, _) = Ready(); city.EnableHouseholdFinances();
        var token = JObject.Parse(Schema13(city.State));
        var b = token["Food"]!["Residents"]![0]!["Budget"]!;
        b["OpeningBalanceCents"] = long.MaxValue; b["BalanceCents"] = long.MaxValue;
        b["IncomeReceivedCents"] = 1; b["EssentialsPaidCents"] = 1;
        string old = token.ToString(); var data = new Dictionary<string,string> { [SaveStore.StateKey] = old };
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(old).Status);
        Assert.False(SaveStore.WriteTo(data, city.State).Success); Assert.Equal(old, data[SaveStore.StateKey]);
    }

    private static string Schema13(CityState state)
    {
        var token = JObject.Parse(SaveCodec.Write(state)); token["Schema"] = 13;
        foreach (var citizen in token["Citizens"]!.OfType<JObject>()) citizen.Remove("OptionalStop");
        foreach (var prop in token.Descendants().OfType<JProperty>().Where(p => p.Name.EndsWith("Cents", StringComparison.Ordinal)).ToArray())
            prop.Value = checked((long)(decimal)prop.Value);
        return token.ToString();
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(6)] [InlineData(40)]
    public void Schema13MigratesWholeCentBalancesAndJourneysWithoutReplayingSales(int elapsed)
    {
        var (city, market, routes) = Ready(); city.EnableHouseholdFinances();
        Advance(city, city.State.Food!.Residents.Single().DesiredAt + elapsed);
        string old = Schema13(city.State); int orders = market.Orders.Count;
        var data = new Dictionary<string,string> { [SaveStore.StateKey] = old };
        var read = SaveStore.ReadFrom(data, city.State.PopulationKey);
        Assert.Equal(SaveReadStatus.Loaded, read.Status); Assert.Equal(13, read.SourceSchema);
        Assert.Equal(CityState.CurrentSchema, read.State!.Schema); Assert.Equal(old, data[SaveStore.StateKey]);
        Assert.Equal(SaveCodec.Write(city.State), SaveCodec.Write(read.State));
        var loaded = new CitySimulator(read.State, routes); loaded.AttachFoodMarket(market);
        Assert.Equal(orders, market.Orders.Count);
        Assert.True(SaveStore.WriteTo(data, loaded.State).Success);
        Assert.Equal(CityState.CurrentSchema, SaveCodec.Read(data[SaveStore.StateKey]).SourceSchema);
        var damaged = JObject.Parse(old); damaged["Food"]!["Residents"]![0]!["PriceCents"] = 1.1;
        data[SaveStore.StateKey] = damaged.ToString(); string bad = data[SaveStore.StateKey];
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(bad).Status);
        Assert.False(SaveStore.WriteTo(data, loaded.State).Success); Assert.Equal(bad, data[SaveStore.StateKey]);
    }
}
