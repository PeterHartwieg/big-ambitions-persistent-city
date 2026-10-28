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
    }

    [Theory]
    [InlineData(1092881272, "1025.279998779296875")] // 10.2528f; a 15-digit conversion stores 1025.279998779297
    [InlineData(1008981770, "0.999999977648258209228515625")] // .01f
    [InlineData(1036831949, "10.0000001490116119384765625")] // .1f
    [InlineData(1089051034, "730.000019073486328125")] // 7.3f
    [InlineData(1095227146, "1248.99997711181640625")] // 12.49f
    [InlineData(1051372203, "33.33333432674407958984375")] // 1/3f
    [InlineData(1176256511, "999999.90234375")] // 9999.999f
    [InlineData(1176256512, "1000000")] // 10000f
    public void NativePriceBecomesExactlyItsBinaryValueInCents(int bits, string expected)
    {
        decimal cents = FoodMoney.FromNative(BitConverter.Int32BitsToSingle(bits));
        Assert.Equal(expected, cents.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(ExactCents(bits), Rational(cents));
    }

    [Fact]
    public void EveryRepresentableNativePriceConvertsWithoutRounding()
    {
        var random = new Random(20261029);
        int checkedPrices = 0;
        for (int i = 0; i < 200000; i++)
        {
            int bits = random.Next(BitConverter.SingleToInt32Bits(.01f), BitConverter.SingleToInt32Bits(10000f) + 1);
            decimal cents = FoodMoney.FromNative(BitConverter.Int32BitsToSingle(bits));
            Assert.Equal(ExactCents(bits), Rational(cents));
            checkedPrices++;
        }
        Assert.Equal(200000, checkedPrices);
    }

    [Fact]
    public void SumOfResidentPaymentsEqualsTheExactSumOfNativeOrderPrices()
    {
        // Menu-scale native prices: the rig croissant (10.2528) and other fractional prices.
        int[] orders = new[] { 10.2528f, 7.3f, 12.49f, 3.99f, 10.2528f, 4.79f, 15.99f }.Select(BitConverter.SingleToInt32Bits).ToArray();
        var budget = new ResidentBudget { OpeningBalanceCents = 10000000, BalanceCents = 10000000 };
        var data = new Dictionary<string, string>();
        var nativeSum = (Numerator: System.Numerics.BigInteger.Zero, Denominator: System.Numerics.BigInteger.One);
        decimal receipts = 0;
        for (int i = 0; i < 1100; i++)
        {
            int bits = orders[i % orders.Length];
            decimal cents = FoodMoney.FromNative(BitConverter.Int32BitsToSingle(bits));
            var meal = new ResidentMeal { ResidentId = "r" + i, Day = 5, Kind = MealKind.Lunch,
                BusinessId = "shop", ProductId = FoodProducts.Croissant, PriceCents = cents };
            Assert.True(HouseholdFinance.HoldPurchase(budget, cents));
            FoodSaleReceipts.Begin(data, meal); FoodSaleReceipts.Confirm(data, meal);
            HouseholdFinance.ConfirmPurchase(budget);
            receipts = FoodMoney.Add(receipts, cents);
            var price = ExactCents(bits);
            nativeSum = Normal(nativeSum.Numerator * price.Denominator + price.Numerator * nativeSum.Denominator,
                nativeSum.Denominator * price.Denominator);
        }
        Assert.Equal(nativeSum, Rational(receipts));
        Assert.Equal(nativeSum, Rational(budget.PreparedFoodSpentCents));
        Assert.Equal(nativeSum, Rational(budget.OpeningBalanceCents - budget.BalanceCents));
        Assert.Equal(0, budget.HeldPurchaseCents);
    }

    // Exact cents of a binary32 dollar price, as a reduced fraction: m * 2^e * 100.
    private static (System.Numerics.BigInteger Numerator, System.Numerics.BigInteger Denominator) ExactCents(int bits)
    {
        int biased = (bits >> 23) & 255;
        var significand = new System.Numerics.BigInteger((bits & 0x7fffff) | 0x800000) * 100;
        int exponent = biased - 150;
        return exponent >= 0 ? Normal(significand << exponent, 1) : Normal(significand, System.Numerics.BigInteger.One << -exponent);
    }

    private static (System.Numerics.BigInteger Numerator, System.Numerics.BigInteger Denominator) Rational(decimal value)
    {
        int[] parts = decimal.GetBits(value);
        var unscaled = (new System.Numerics.BigInteger((uint)parts[2]) << 64) | (new System.Numerics.BigInteger((uint)parts[1]) << 32) | (uint)parts[0];
        if (parts[3] < 0) unscaled = -unscaled;
        return Normal(unscaled, System.Numerics.BigInteger.Pow(10, (parts[3] >> 16) & 255));
    }

    private static (System.Numerics.BigInteger Numerator, System.Numerics.BigInteger Denominator) Normal(System.Numerics.BigInteger numerator, System.Numerics.BigInteger denominator)
    {
        var divisor = System.Numerics.BigInteger.GreatestCommonDivisor(numerator, denominator);
        return (numerator / divisor, denominator / divisor);
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
        Assert.Equal(1092881272, BitConverter.SingleToInt32Bits(native));
        Assert.Equal(1025.279998779296875m, cents);
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
        Assert.Equal(111755.519866943359375m, receipts);
        Assert.Equal(cents * 109, receipts);
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
        Assert.Equal("1025.279998779296875", meal.SpentCents.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(market.Price, meal.Budget!.PreparedFoodSpentCents);
        Assert.Equal(0, meal.Budget.HeldPurchaseCents);
        Assert.Equal(market.Price, city.State.Food.History.Sum(h => h.RevenueCents));
        city = Reload(city, market, routes);
        Assert.Single(market.Orders);
    }

    [Fact]
    public void Schema14SavesWithTwelveDigitFractionalCentsStillLoadUnchanged()
    {
        var (city, market, routes) = Ready();
        // The amount that twelve-digit conversion stored for 10.2528f before bit-exact conversion.
        market.Price = 1025.279998779297m;
        var person = city.State.Citizens.Single(); person.PlaceId = "shop"; person.Activity = Activity.Shop;
        city = city.RebuildRoutes(routes); city.EnableHouseholdFinances();
        Advance(city, city.State.Food!.Residents.Single().DesiredAt + 30);
        string legacy = SaveCodec.Write(city.State);
        Assert.Contains("1025.279998779297", legacy);
        var read = SaveCodec.Read(legacy);
        Assert.Equal(SaveReadStatus.Loaded, read.Status);
        Assert.Equal(1025.279998779297m, read.State!.Food!.Residents.Single().SpentCents);
        Assert.Equal(legacy, SaveCodec.Write(read.State));
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
