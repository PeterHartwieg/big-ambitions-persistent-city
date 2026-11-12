using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed partial class FoodCommerceTests
{
    [Theory]
    [InlineData(.01f)] [InlineData(.02f)] [InlineData(.03f)] [InlineData(.04f)] [InlineData(.05f)]
    [InlineData(.06f)] [InlineData(.07f)] [InlineData(.08f)] [InlineData(.10f)] [InlineData(.11f)]
    [InlineData(.12f)] [InlineData(.14f)] [InlineData(.17f)] [InlineData(.20f)] [InlineData(.21f)]
    [InlineData(.23f)] [InlineData(.24f)]
    public void R7_CheapOddPriceFirstCheckoutSucceedsExactly(float native)
    {
        var (city, market, routes) = Ready();
        market.Price = Money.FromNative(native);
        var person = city.State.Citizens.Single(); person.PlaceId = "shop"; person.Activity = Activity.Shop;
        city = city.RebuildRoutes(routes); city.EnableHouseholdFinances();
        Advance(city, city.State.Food!.Residents.Single().DesiredAt + 30);
        var meal = city.State.Food.Residents.Single();
        Assert.Single(market.Orders);
        Assert.Equal(MealStatus.Paid, meal.Status);
        Assert.Equal(market.Price, meal.Budget!.PreparedFoodSpent);
        Assert.Equal(market.Price, meal.Spent);
        var b = meal.Budget;
        Assert.Equal(b.OpeningBalance + b.IncomeReceived, b.Balance + b.EssentialsPaid + b.HomeFoodPaid + b.PreparedFoodSpent + b.HeldPurchase);
        Assert.Equal(market.Price, Money.Sum(city.State.Food.History.Select(h => h.Revenue)));
        var read = DevelopmentChain.Read(DevelopmentChain.Write(city.State));
        Assert.Equal(SaveReadStatus.Loaded, read.Status);
        Assert.Equal(market.Price, read.State!.Food!.Residents.Single().Spent);
    }

    [Fact]
    public void R7_DayPostingAfterExactPurchaseMovesBalanceByExactPostings()
    {
        var b = new ResidentBudget { IncomeSource = ResidentIncomeSource.ExternalSupport, OpeningBalance = Money.FromCents(7920000), Balance = Money.FromCents(7920000),
            BaseIncomeCents = 50000, DailyIncomeCents = 50000, DailyHousingCents = 3000,
            DailyEssentialsCents = 1500, DailyHomeFoodCents = 900, LastPostedDay = 1, NextPostingMinute = 2880 };
        var price = Money.FromNative(.3f);
        Assert.Equal("30.0000011920928955078125", price.ToCentsText());
        Assert.True(HouseholdFinance.HoldPurchase(b, price));
        HouseholdFinance.ConfirmPurchase(b);
        var before = b.Balance;
        HouseholdFinance.PostDay(b, 2880);
        Assert.Equal(Money.FromCents(44600), b.Balance - before);
        Assert.Equal(b.OpeningBalance + b.IncomeReceived, b.Balance + b.EssentialsPaid + b.HomeFoodPaid + b.PreparedFoodSpent + b.HeldPurchase);
    }

    [Fact]
    public void R7_LongLivedTopBandWalletStaysExactThroughDay20000()
    {
        var price = Money.FromNative(1.37f);
        var b = new ResidentBudget { IncomeSource = ResidentIncomeSource.ExternalSupport, OpeningBalance = Money.FromCents(350000), Balance = Money.FromCents(350000), BaseIncomeCents = 50000, DailyIncomeCents = 50000,
            DailyHousingCents = 7000, DailyEssentialsCents = 1500, DailyHomeFoodCents = 900, NextPostingMinute = 1440, LastPostedDay = 0 };
        Assert.True(HouseholdFinance.HoldPurchase(b, price)); HouseholdFinance.ConfirmPurchase(b);
        for (int day = 1; day <= 20000; day++)
        {
            var before = b.Balance;
            HouseholdFinance.PostDay(b, day * 1440.0);
            Assert.Equal(before + Money.FromCents(40600), b.Balance);
            Assert.Equal(b.OpeningBalance + b.IncomeReceived, b.Balance + b.EssentialsPaid + b.HomeFoodPaid + b.PreparedFoodSpent + b.HeldPurchase);
        }
        Assert.Equal(Money.FromCents(350000 + 20000L * 40600) - price, b.Balance);
        var (city, _, _) = Ready(); city.EnableHouseholdFinances();
        var resident = city.State.Food!.Residents.Single();
        b.NextPostingMinute = resident.Budget!.NextPostingMinute; b.LastPostedDay = resident.Budget.LastPostedDay; b.HomeFoodFundedDay = -1;
        city.State.Citizens.Single().Wallet = b; resident.Budget = b;
        string encoded = DevelopmentChain.Write(city.State);
        var read = DevelopmentChain.Read(encoded);
        Assert.Equal(SaveReadStatus.Loaded, read.Status);
        Assert.Equal(b.Balance, read.State!.Food!.Residents.Single().Budget!.Balance);
        Assert.Equal(encoded, DevelopmentChain.Write(read.State));
    }

    [Fact]
    public void R7_MillionDollarWalletHoldsCheapNativePriceExactly()
    {
        var price = Money.FromNative(1.37f);
        var b = new ResidentBudget { IncomeSource = ResidentIncomeSource.ExternalSupport, OpeningBalance = Money.FromCents(100000000), Balance = Money.FromCents(100000000), DailyHousingCents = 7000, DailyEssentialsCents = 1500, DailyHomeFoodCents = 900 };
        Assert.True(HouseholdFinance.HoldPurchase(b, price));
        HouseholdFinance.ConfirmPurchase(b);
        Assert.Equal(Money.FromCents(100000000) - price, b.Balance);
        Assert.Equal(price, b.PreparedFoodSpent);
        Assert.Equal("99999862.999999523162841796875", b.Balance.ToCentsText());
    }

    [Fact]
    public void R7_HistoryAggregatesAcceptedNativePriceExactly()
    {
        var (city, market, routes) = Ready();
        market.Price = Money.FromNative(.3f);
        var person = city.State.Citizens.Single(); person.PlaceId = "shop"; person.Activity = Activity.Shop;
        city = city.RebuildRoutes(routes); city.EnableHouseholdFinances();
        var row = new FoodShopDay { Day = 1, PlaceId = "shop", BusinessId = "b", Purchases = 8, Revenue = Money.FromCents(8000000), PurchaseMeals = new long[] { 8, 0, 0, 0 },
            ProductSales = new() { new FoodProductSales { ProductId = FoodProducts.Croissant, Purchases = 8, Revenue = Money.FromCents(8000000), PurchaseMeals = new long[] { 8, 0, 0, 0 } } },
            BuyerDistricts = new() { new FoodBuyerDistrict { DistrictId = "", Purchases = 8 } } };
        row.PurchaseHours[0] = 8; city.State.Food!.History.Add(row);
        Assert.Equal(SaveReadStatus.Loaded, DevelopmentChain.Read(DevelopmentChain.Write(city.State)).Status);
        Advance(city, city.State.Food.Residents.Single().DesiredAt + 30);
        Assert.Single(market.Orders);
        Assert.Equal(MealStatus.Paid, city.State.Food.Residents.Single().Status);
        Assert.Equal(market.Price, row.Revenue - Money.FromCents(8000000));
        Assert.Equal(market.Price, row.ProductSales.Single().Revenue - Money.FromCents(8000000));
        Assert.Equal("8000030.0000011920928955078125", row.Revenue.ToCentsText());
        string encoded = DevelopmentChain.Write(city.State);
        var read = DevelopmentChain.Read(encoded);
        Assert.Equal(SaveReadStatus.Loaded, read.Status);
        Assert.Equal(row.Revenue, read.State!.Food!.History.Single(h => h.Day == 1).Revenue);
        Assert.Equal(encoded, DevelopmentChain.Write(read.State));
    }

    [Theory]
    [InlineData("none")] [InlineData("spent")] [InlineData("history")] [InlineData("product")] [InlineData("prepared")]
    public void R7_SettlementOverflowIsRefusedBeforeHoldOrNativeCheckout(string ledger)
    {
        var (city, market, routes) = Ready();
        var person = city.State.Citizens.Single(); person.PlaceId = "shop"; person.Activity = Activity.Shop;
        city = city.RebuildRoutes(routes); city.EnableHouseholdFinances();
        var meal = city.State.Food!.Residents.Single();
        var full = Money.FromUnits(long.MaxValue - 10);
        int day = (int)(meal.DesiredAt / 1440);
        var row = new FoodShopDay { Day = day, PlaceId = "shop", BusinessId = "b",
            ProductSales = new() { new FoodProductSales { ProductId = FoodProducts.Croissant } } };
        if (ledger == "spent") meal.Spent = full;
        if (ledger == "history") row.Revenue = full;
        if (ledger == "product") row.ProductSales[0].Revenue = full;
        if (ledger == "prepared") meal.Budget!.PreparedFoodSpent = full;
        if (ledger == "history" || ledger == "product") city.State.Food.History.Add(row);
        var prepared = meal.Budget!.PreparedFoodSpent;
        // F6d checks the extended identity at every ledger mutation. This deliberately invalid fixture now
        // refuses before its daily posting, still before any hold or native checkout.
        if (ledger == "prepared")
        {
            Assert.Throws<OverflowException>(() => Advance(city, meal.DesiredAt + 30));
            Assert.Empty(market.Orders); Assert.Equal(prepared, meal.Budget!.PreparedFoodSpent);
            Assert.Equal(Money.Zero, meal.Budget.HeldPurchase); return;
        }
        Advance(city, meal.DesiredAt + 30);
        if (ledger == "none") { Assert.Single(market.Orders); Assert.Equal(MealStatus.Paid, meal.Status); return; }
        Assert.Empty(market.Orders);
        Assert.Equal(ledger == "prepared" ? "Household funds no longer available at checkout" : "Purchase would exceed the exact money range", meal.Reason);
        var b = meal.Budget;
        Assert.Equal(prepared, b.PreparedFoodSpent); Assert.Equal(Money.Zero, b.HeldPurchase);
        // The "prepared" fixture is deliberately unbalanced; the others must still conserve.
        if (ledger != "prepared") Assert.Equal(Money.TotalUnits(new[] { b.OpeningBalance, b.IncomeReceived }),
            Money.TotalUnits(new[] { b.Balance, b.EssentialsPaid, b.HomeFoodPaid, b.PreparedFoodSpent }));
        Assert.NotEqual(MealStatus.Uncertain, meal.Status); Assert.NotEqual(MealStatus.Paid, meal.Status);
        Assert.Equal(0, meal.Purchases);
    }

    [Fact]
    public void R7_MoneyWireNamesAreUnambiguousInEverySavedType()
    {
        var types = new HashSet<Type>(); var pending = new Stack<Type>(new[] { typeof(CityState), typeof(FoodShadowState) });
        var moneyNames = new HashSet<string>(LegacyMoneyWire.Names.Keys);
        moneyNames.UnionWith(new[] { "ExternalFoodSpent", "LegacyExternalFoodSpent", "EmploymentIncome", "Discretionary", "UnpostedWage" });
        while (pending.Count > 0)
        {
            var type = pending.Pop();
            if (!types.Add(type) || type.Namespace != typeof(CityState).Namespace || type == typeof(Money)) continue;
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite))
            {
                var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                Assert.False(LegacyMoneyWire.Names.Values.Contains(property.Name), type.Name + "." + property.Name + " reuses a retired money name.");
                Assert.True(moneyNames.Contains(property.Name) == (propertyType == typeof(Money)), type.Name + "." + property.Name + " money naming.");
                Assert.NotEqual(typeof(decimal), propertyType);
                foreach (var nested in propertyType.IsGenericType ? propertyType.GetGenericArguments() : propertyType.IsArray ? new[] { propertyType.GetElementType()! } : new[] { propertyType })
                    pending.Push(nested);
            }
        }
        Assert.Contains(typeof(ResidentBudget), types); Assert.Contains(typeof(FoodShopDay), types); Assert.Contains(typeof(FoodOffer), types);
    }
}

public sealed class MoneyUnitTests
{
    [Theory]
    [InlineData("0", 0L)] [InlineData("1", 268435456L)] [InlineData("12345", 12345L * 268435456)]
    [InlineData("0.5", 134217728L)] [InlineData("1025.279998779296875", 275221504000L)]
    [InlineData("0.0000000037252902984619140625", 1L)] [InlineData("-3.25", -872415232L)] [InlineData("216908.0", 216908L * 268435456)]
    public void CentsTextParsesExactly(string text, long units)
    {
        Assert.True(Money.TryParseCents(text, 28, out var money));
        Assert.Equal(units, money.Units);
        Assert.Equal(text.EndsWith(".0") ? text.Substring(0, text.Length - 2) : text, money.ToCentsText());
    }

    [Theory]
    [InlineData("")] [InlineData("-")] [InlineData(".5")] [InlineData("5.")] [InlineData("01")] [InlineData("+1")]
    [InlineData("1e2")] [InlineData("0.1")] [InlineData("0.00000000372529029846191406251")] [InlineData("1,5")]
    [InlineData("34359738368")]
    public void CentsTextThatIsNotAWholeUnitCountIsRefused(string text)
        => Assert.False(Money.TryParseCents(text, 28, out _));

    [Fact]
    public void WholeCentsAreWholeUnitsAndScaleLimitsApply()
    {
        Assert.True(Money.TryParseCents("12345", 0, out var whole)); Assert.Equal(Money.FromCents(12345), whole);
        Assert.False(Money.TryParseCents("12345.5", 0, out _));
        Assert.Equal(Money.UnitsPerDollar, Money.FromCents(100).Units);
        Assert.Equal(25L << 30, Money.UnitsPerDollar);
        Assert.Throws<OverflowException>(() => Money.FromUnits(long.MaxValue) + Money.FromUnits(1));
        Assert.Throws<OverflowException>(() => Money.FromUnits(long.MinValue) - Money.FromUnits(1));
        Assert.Throws<OverflowException>(() => Money.FromCents(long.MaxValue / 1000));
        Assert.Throws<OverflowException>(() => Money.FromUnits(long.MaxValue / 2) * 3);
        Assert.False(Money.TryAdd(Money.FromUnits(long.MaxValue), Money.FromUnits(1), out _));
        Assert.True(Money.TryAdd(Money.FromUnits(long.MaxValue - 1), Money.FromUnits(1), out var max)); Assert.Equal(long.MaxValue, max.Units);
        Assert.Throws<OverflowException>(() => Money.Sum(new[] { Money.FromUnits(long.MaxValue), Money.FromUnits(1) }));
        Assert.Equal("34359738367.99999999627470970153808593750".TrimEnd('0'), Money.CentsText(long.MaxValue));
        Assert.Equal(Money.CentsText(new System.Numerics.BigInteger(long.MaxValue) * 2), Money.CentsText(Money.TotalUnits(new[] { Money.FromUnits(long.MaxValue), Money.FromUnits(long.MaxValue) })));
    }
}
