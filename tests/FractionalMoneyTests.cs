using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
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
        var money = Money.FromNative(price);
        // units < 2^53 and the divisor is 25 * 2^30: the double quotient is the binary32 value exactly.
        Assert.Equal(BitConverter.SingleToInt32Bits(price), BitConverter.SingleToInt32Bits((float)(money.Units / (double)Money.UnitsPerDollar)));
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
        var money = Money.FromNative(BitConverter.Int32BitsToSingle(bits));
        Assert.Equal(expected, money.ToCentsText());
        Assert.Equal(ExactUnits(bits), money.Units);
        Assert.True(Money.TryParseCents(expected, 28, out var parsed));
        Assert.Equal(money, parsed);
    }

    [Fact]
    public void EveryBinary32PriceUpToTheCapConvertsExactlyAndOnlySubUnitPricesAreRefused()
    {
        // Exhaustive over every positive normal binary32 value up to $10,000 (about 1.18 billion values).
        // Independent oracle: f * 25 * 2^30 is exact in double (a 24-bit significand times 25 and a power
        // of two), so it is the exact unit count; FromNative must equal it or refuse exactly when it is
        // fractional. Every price of at least 2^-7 dollars, so every price of a cent or more, is a whole unit.
        int first = 0x00800000, last = BitConverter.SingleToInt32Bits(10000f);
        int floor = BitConverter.SingleToInt32Bits(1f / 128);
        long accepted = 0, refused = 0; int failures = 0, refusedAtOrAboveFloor = 0;
        Parallel.For(0, 64, () => (Accepted: 0L, Refused: 0L), (chunk, _, totals) =>
        {
            long span = ((long)last - first + 64) / 64;
            int from = (int)Math.Min(last + 1L, first + chunk * span), to = (int)Math.Min(last + 1L, first + (chunk + 1) * span);
            for (int bits = from; bits < to; bits++)
            {
                float price = BitConverter.Int32BitsToSingle(bits);
                double exact = price * 26843545600.0;
                bool whole = exact == Math.Floor(exact);
                if (Money.TryFromNative(price, out var money))
                {
                    if (!whole || money.Units != (long)exact) System.Threading.Interlocked.Increment(ref failures);
                    totals.Accepted++;
                }
                else
                {
                    if (whole) System.Threading.Interlocked.Increment(ref failures);
                    if (bits >= floor) System.Threading.Interlocked.Increment(ref refusedAtOrAboveFloor);
                    totals.Refused++;
                }
            }
            return totals;
        }, totals => { System.Threading.Interlocked.Add(ref accepted, totals.Accepted); System.Threading.Interlocked.Add(ref refused, totals.Refused); });
        Assert.Equal(0, failures);
        Assert.Equal(0, refusedAtOrAboveFloor);
        Assert.Equal((long)last - first + 1, accepted + refused);
        Assert.True(accepted >= (long)last - floor + 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => Money.FromNative(BitConverter.Int32BitsToSingle(floor - 1)));
    }

    [Fact]
    public void SumOfResidentPaymentsEqualsTheExactSumOfNativeOrderPrices()
    {
        // Menu-scale native prices: the rig croissant (10.2528) and other fractional prices.
        int[] orders = new[] { 10.2528f, 7.3f, 12.49f, 3.99f, 10.2528f, 4.79f, 15.99f }.Select(BitConverter.SingleToInt32Bits).ToArray();
        var budget = new ResidentBudget { OpeningBalance = Money.FromCents(10000000), Balance = Money.FromCents(10000000) };
        var data = new Dictionary<string, string>();
        var nativeSum = BigInteger.Zero;
        var receipts = Money.Zero;
        for (int i = 0; i < 1100; i++)
        {
            int bits = orders[i % orders.Length];
            var price = Money.FromNative(BitConverter.Int32BitsToSingle(bits));
            var meal = new ResidentMeal { ResidentId = "r" + i, Day = 5, Kind = MealKind.Lunch,
                BusinessId = "shop", ProductId = FoodProducts.Croissant, Price = price };
            Assert.True(HouseholdFinance.HoldPurchase(budget, price));
            FoodSaleReceipts.Begin(data, meal); FoodSaleReceipts.Confirm(data, meal);
            HouseholdFinance.ConfirmPurchase(budget);
            receipts += price;
            nativeSum += ExactUnits(bits);
        }
        Assert.Equal(nativeSum, receipts.Units);
        Assert.Equal(nativeSum, budget.PreparedFoodSpent.Units);
        Assert.Equal(nativeSum, (budget.OpeningBalance - budget.Balance).Units);
        Assert.Equal(Money.Zero, budget.HeldPurchase);
    }

    [Fact]
    public void MixedMagnitudeSumsEqualTheExactRationalSumOfNativePrices()
    {
        // Sub-dollar, menu-scale and cap-scale prices interleaved with whole-cent postings.
        float[] prices = { .01f, .3f, 1.37f, 10.2528f, 9999.999f, .07f, 7.3f, .24f, 3333.333f };
        var nativeSum = BigInteger.Zero;
        var total = Money.FromCents(7920000);
        var history = new FoodShopDay { Revenue = Money.FromCents(8000000) };
        for (int i = 0; i < 20000; i++)
        {
            float price = prices[i % prices.Length];
            var money = Money.FromNative(price);
            total += money; history.Revenue += money; total += Money.FromCents(12345);
            nativeSum += ExactUnits(BitConverter.SingleToInt32Bits(price));
        }
        var cents = new BigInteger(Money.UnitsPerCent);
        Assert.Equal(nativeSum + 7920000 * cents + 20000 * 12345 * cents, total.Units);
        Assert.Equal(nativeSum + 8000000 * cents, history.Revenue.Units);
        // The rational sum in cents: exact text of an integer count of 2^-28 cents.
        Assert.Equal(Money.CentsText(nativeSum + 8000000 * cents), history.Revenue.ToCentsText());
    }

    // Exact units (2^-28 cents) of a binary32 dollar price: m * 2^(biased-150) * 25 * 2^30.
    private static BigInteger ExactUnits(int bits)
    {
        int biased = (bits >> 23) & 255;
        var units = new BigInteger((bits & 0x7fffff) | 0x800000) * 25;
        int exponent = biased - 120;
        if (exponent >= 0) return units << exponent;
        Assert.True((units % (BigInteger.One << -exponent)).IsZero);
        return units >> -exponent;
    }

    [Theory]
    [InlineData(0f)] [InlineData(-1f)] [InlineData(float.NaN)] [InlineData(float.PositiveInfinity)]
    [InlineData(float.Epsilon)] [InlineData(10001f)]
    public void UnsupportedNativePriceIsRefusedWithoutAnInventedCharge(float price)
        => Assert.Throws<ArgumentOutOfRangeException>(() => Money.FromNative(price));

    [Fact]
    public void The109SaleRegressionConservesWalletHoldsReceiptsAndNativePrice()
    {
        const float native = 10.2528f;
        var price = Money.FromNative(native);
        Assert.Equal(1092881272, BitConverter.SingleToInt32Bits(native));
        Assert.Equal("1025.279998779296875", price.ToCentsText());
        var budget = new ResidentBudget { OpeningBalance = Money.FromCents(200000), Balance = Money.FromCents(200000) };
        var data = new Dictionary<string, string>();
        var receipts = Money.Zero;
        for (int i = 0; i < 109; i++)
        {
            var meal = new ResidentMeal { ResidentId = "c" + i, Day = 5, Kind = MealKind.Lunch,
                BusinessId = "shop", ProductId = FoodProducts.Croissant, Price = price };
            Assert.True(HouseholdFinance.HoldPurchase(budget, price));
            FoodSaleReceipts.Begin(data, meal); FoodSaleReceipts.Confirm(data, meal);
            Assert.True(FoodSaleReceipts.AlreadyPaid(new Dictionary<string, string>(data), meal));
            HouseholdFinance.ConfirmPurchase(budget); receipts += price;
        }
        Assert.Equal(receipts, budget.PreparedFoodSpent);
        Assert.Equal(receipts, budget.OpeningBalance - budget.Balance);
        Assert.Equal(Money.Zero, budget.HeldPurchase);
        Assert.Equal("111755.519866943359375", receipts.ToCentsText());
        Assert.Equal(price * 109, receipts);
        Assert.Equal("112780.79986572265625", (receipts + price).ToCentsText()); // 110 sales: $1127.8079986572265625
        Assert.True(Math.Abs(1025.0 * 109 / 100 - (double)native * 109) > .30);
        var before = budget.Balance;
        Assert.True(HouseholdFinance.HoldPurchase(budget, price));
        HouseholdFinance.RefusePurchase(budget);
        Assert.Equal(before, budget.Balance);
    }

    [Theory]
    [InlineData("paid", FoodReceiptStatus.Paid)] [InlineData("pending", FoodReceiptStatus.Pending)]
    public void LegacyReceiptsKeepTheirExactOldAmountAndBlockDuplicateCheckout(string status, FoodReceiptStatus expected)
    {
        var meal = new ResidentMeal { ResidentId = "old", Day = 5, Kind = MealKind.Lunch,
            BusinessId = "shop", ProductId = FoodProducts.Croissant, Price = Money.FromCents(1025) };
        string key = FoodSaleReceipts.LegacyPrefix + meal.IntentId;
        string value = status + "|1025|shop|" + Uri.EscapeDataString(meal.ProductId);
        var data = new Dictionary<string,string> { [key] = value };
        Assert.Equal(expected, FoodSaleReceipts.ReadStatus(data, meal));
        Assert.Throws<InvalidOperationException>(() => FoodSaleReceipts.Begin(data, meal));
        FoodSaleReceipts.Prune(data, 100, new[] { meal.IntentId });
        Assert.Equal(value, data[key]);
        meal.Price = Money.FromNative(10.2528f);
        Assert.Equal(FoodReceiptStatus.Conflict, FoodSaleReceipts.ReadStatus(data, meal));
        Assert.Equal(value, data[key]);
    }

    [Theory]
    [InlineData("paid", FoodReceiptStatus.Paid)] [InlineData("pending", FoodReceiptStatus.Pending)]
    public void ExactDecimalReceiptsKeepTheirIdentityAfterTheUnitChange(string status, FoodReceiptStatus expected)
    {
        // A v2 marker written by the decimal-cents implementation for the exact rig croissant price.
        var meal = new ResidentMeal { ResidentId = "old", Day = 5, Kind = MealKind.Lunch,
            BusinessId = "shop", ProductId = FoodProducts.Croissant, Price = Money.FromNative(10.2528f) };
        string key = FoodSaleReceipts.Prefix + meal.IntentId;
        string value = status + "|1025.279998779296875|shop|" + Uri.EscapeDataString(meal.ProductId);
        var data = new Dictionary<string, string> { [key] = value };
        Assert.Equal(expected, FoodSaleReceipts.ReadStatus(data, meal));
        Assert.Throws<InvalidOperationException>(() => FoodSaleReceipts.Begin(data, meal));
        Assert.Equal(value, data[key]);
        meal.Price += Money.FromUnits(1);
        Assert.Equal(FoodReceiptStatus.Conflict, FoodSaleReceipts.ReadStatus(data, meal));
    }

    [Fact]
    public void AmbiguousReceiptVersionsNeverAuthorizeARepeat()
    {
        var meal = new ResidentMeal { ResidentId = "old", Price = Money.FromCents(100), BusinessId = "shop", ProductId = "product" };
        var data = new Dictionary<string,string>();
        FoodSaleReceipts.Begin(data, meal); FoodSaleReceipts.Confirm(data, meal);
        data[FoodSaleReceipts.LegacyPrefix + meal.IntentId] = data.Values.Single();
        Assert.Equal(FoodReceiptStatus.Conflict, FoodSaleReceipts.ReadStatus(data, meal));
        FoodSaleReceipts.Prune(data, 10000); Assert.Equal(2, data.Count);
    }

    [Fact]
    public void UnitExhaustionRefusesHoldBeforeChangingFunds()
    {
        var budget = new ResidentBudget { Balance = Money.FromCents(500000), OpeningBalance = Money.FromCents(500000),
            PreparedFoodSpent = Money.FromUnits(long.MaxValue - 5) };
        Assert.False(HouseholdFinance.HoldPurchase(budget, Money.FromNative(10.2528f)));
        Assert.Equal(Money.FromCents(500000), budget.Balance); Assert.Equal(Money.Zero, budget.HeldPurchase);
        Assert.Equal(Money.FromUnits(long.MaxValue - 5), budget.PreparedFoodSpent);
    }

    [Fact]
    public void PostingOverflowLeavesTheBudgetUnchanged()
    {
        var budget = new ResidentBudget { BaseIncomeCents = 50000, DailyIncomeCents = 50000, DailyHousingCents = 7000,
            DailyEssentialsCents = 1500, DailyHomeFoodCents = 900, LastPostedDay = 0, NextPostingMinute = 1440,
            Balance = Money.FromCents(1000), IncomeReceived = Money.FromUnits(long.MaxValue - 1) };
        string before = Newtonsoft.Json.JsonConvert.SerializeObject(budget);
        Assert.Throws<OverflowException>(() => HouseholdFinance.PostDay(budget, 1440));
        Assert.Equal(before, Newtonsoft.Json.JsonConvert.SerializeObject(budget));
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
        market.Price = Money.FromNative(10.2528f);
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
        Assert.Equal(market.Price, meal.Spent);
        Assert.Equal("1025.279998779296875", meal.Spent.ToCentsText());
        Assert.Equal(market.Price, meal.Budget!.PreparedFoodSpent);
        Assert.Equal(Money.Zero, meal.Budget.HeldPurchase);
        Assert.Equal(market.Price, Money.Sum(city.State.Food.History.Select(h => h.Revenue)));
        city = Reload(city, market, routes);
        Assert.Single(market.Orders);
    }

    [Fact]
    public void EarlierSchema14TwelveDigitCentsAreRefusedAndPreserved()
    {
        var (city, market, routes) = Ready();
        var person = city.State.Citizens.Single(); person.PlaceId = "shop"; person.Activity = Activity.Shop;
        city = city.RebuildRoutes(routes); city.EnableHouseholdFinances();
        Advance(city, city.State.Food!.Residents.Single().DesiredAt + 30);
        // The amount twelve-digit conversion stored for 10.2528f: not a whole number of 2^-28 cents.
        var token = LegacyMoneyWire.AsSchema(LegacyMoneyWire.ToLegacy(JObject.Parse(SaveCodec.Write(city.State)), wholeCents: false), 14);
        token["Food"]!["Residents"]![0]!["PriceCents"] = 1025.279998779297m;
        string legacy = token.ToString(Newtonsoft.Json.Formatting.None);
        Assert.Contains("\"PriceCents\":1025.279998779297", legacy);
        var data = new Dictionary<string, string> { [SaveStore.StateKey] = legacy };
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(legacy).Status);
        Assert.False(SaveStore.WriteTo(data, city.State).Success); Assert.Equal(legacy, data[SaveStore.StateKey]);
    }

    [Fact]
    public void EarlierSchema14ExactDecimalCentsMigrateExactlyAndKeepReceiptIdentity()
    {
        var (city, market, routes) = Ready();
        market.Price = Money.FromNative(10.2528f);
        var person = city.State.Citizens.Single(); person.PlaceId = "shop"; person.Activity = Activity.Shop;
        city = city.RebuildRoutes(routes); city.EnableHouseholdFinances();
        market.ThrowAfterSale = true;
        Advance(city, city.State.Food!.Residents.Single().DesiredAt + 30);
        Assert.Equal(MealStatus.Uncertain, city.State.Food.Residents.Single().Status);
        string current = SaveCodec.Write(city.State);
        // What the decimal-cents writer produced for this state: the same amounts as decimal cents.
        string legacy = LegacyMoneyWire.AsSchema(LegacyMoneyWire.ToLegacy(JObject.Parse(current), wholeCents: false), 14).ToString(Newtonsoft.Json.Formatting.None);
        Assert.Contains("\"PriceCents\":1025.279998779296875", legacy);
        Assert.Contains("\"BalanceCents\":", legacy); Assert.DoesNotContain("\"Balance\":", legacy);
        var read = SaveCodec.Read(legacy);
        Assert.Equal(SaveReadStatus.Loaded, read.Status); Assert.Equal(14, read.SourceSchema);
        Assert.Equal(LegacyMoneyWire.WithoutUpgradeFacts(current), LegacyMoneyWire.WithoutUpgradeFacts(SaveCodec.Write(read.State!)));
        // A native v2 receipt written before the unit change still identifies this intention.
        var meal = read.State!.Food!.Residents.Single();
        var receipt = new Dictionary<string, string> { [FoodSaleReceipts.Prefix + meal.IntentId] = "paid|1025.279998779296875|b|pastry" };
        Assert.Equal(FoodReceiptStatus.Paid, FoodSaleReceipts.ReadStatus(receipt, meal));
        // Mixing both field generations is refused rather than guessed.
        string mixed = legacy.Replace("\"ArrearsCents\":0.0", "\"Arrears\":0");
        Assert.NotEqual(legacy, mixed);
        var refused = SaveCodec.Read(mixed);
        Assert.Equal(SaveReadStatus.Invalid, refused.Status); Assert.Contains("mixes money field generations", refused.Error);
    }

    [Fact]
    public void MigrationStillRejectsLegacyLedgerOverflowAndPreservesThePayload()
    {
        var (city, _, _) = Ready(); city.EnableHouseholdFinances();
        var token = JObject.Parse(Schema13(city.State));
        var b = token["Food"]!["Residents"]![0]!["Budget"]!;
        // Whole cents above the unit's range (about $343.6 million per amount) cannot convert exactly.
        b["OpeningBalanceCents"] = long.MaxValue; b["BalanceCents"] = long.MaxValue;
        b["IncomeReceivedCents"] = 1; b["EssentialsPaidCents"] = 1;
        string old = token.ToString(); var data = new Dictionary<string,string> { [SaveStore.StateKey] = old };
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(old).Status);
        Assert.False(SaveStore.WriteTo(data, city.State).Success); Assert.Equal(old, data[SaveStore.StateKey]);
    }

    private static string Schema13(CityState state)
    {
        var token = LegacyMoneyWire.AsSchema(LegacyMoneyWire.ToLegacy(JObject.Parse(SaveCodec.Write(state)), wholeCents: true), 13);
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
        Assert.Equal(LegacyMoneyWire.WithoutUpgradeFacts(SaveCodec.Write(city.State)), LegacyMoneyWire.WithoutUpgradeFacts(SaveCodec.Write(read.State)));
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
