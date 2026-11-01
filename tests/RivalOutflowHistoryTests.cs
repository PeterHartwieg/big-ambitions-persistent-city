using System;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class RivalOutflowHistoryTests
{
    static ResidentMeal Meal() => new() { ResidentId = "consumer", Status = MealStatus.Serving,
        External = "10" + RivalFoodTerms.Fingerprint(RivalOutflowTests.Observation()),
        Price = Money.FromNative(1.2345f), Allowance = Money.FromCents(100000) };
    static void Reopen(ResidentMeal meal)
    {
        meal.Status = MealStatus.Serving; meal.DayAllowanceUsed = Money.Zero;
        meal.External = "10" + RivalFoodTerms.Fingerprint(RivalOutflowTests.Observation());
    }
    [Fact]
    public void FourteenDaysReconcileIncludingLateRepeatedOutcomesAndExpiredReplay()
    {
        var meal = Meal(); var food = new FoodCommerceState(); food.Residents.Add(meal);
        var history = RivalOutflowHistory.Begin(food, 1440);
        for (int day = 1; day <= 30; day++)
        {
            Reopen(meal); meal.Day = day - 1; // A late meal belongs to its actual debit day.
            Assert.True(ExternalFoodSettlement.TryPay(meal, history, day * 1440));
            string paid = history.ToWire();
            for (int repeat = 0; repeat < 4; repeat++)
            {
                Assert.False(ExternalFoodSettlement.TryPay(meal, history, day * 1440));
                MealOutcomes.Record(meal, day * 1440 + repeat, true);
                Assert.Equal(paid, history.ToWire());
            }
            Assert.True(history.Reconciles(food));
            Assert.Equal(Math.Min(day, 14) + 1, history.RecordCount);
            history = RivalOutflowHistory.FromWire(history.ToWire()); // Replay retains D2 watermark.
        }
        var view = history.Capture(30);
        Assert.Equal(Enumerable.Range(17, 14), view.Days.Select(d => d.Day));
        Assert.Equal(14, view.Count); Assert.Equal((BigInteger)meal.Price.Units * 14, view.Units);
        history.Advance(60);
        Assert.Equal(1, history.RecordCount); Assert.Equal(BigInteger.Zero, history.Capture(60).Count);
        Assert.True(history.Reconciles(food));
        Assert.False(ExternalFoodSettlement.TryPay(meal, history, 60 * 1440));
        Reopen(meal);
        Assert.True(ExternalFoodSettlement.TryPay(meal, history, 10 * 1440));
        Assert.False(ExternalFoodSettlement.TryPay(meal, history, 10 * 1440));
        Assert.Equal(1, history.RecordCount); Assert.Equal(BigInteger.Zero, history.Capture(60).Count);
        Assert.True(history.Reconciles(food)); // A late old debit stays in the lifetime reconciliation only.
    }
    [Fact]
    public void UnknownPartialAndKnownZeroAreDifferentAndAmountsRemainExact()
    {
        var meal = Meal(); var food = new FoodCommerceState(); food.Residents.Add(meal);
        food.Outflow = RivalOutflowHistory.Begin(food, 10 * 1440 + 9);
        Assert.True(ExternalFoodSettlement.TryPay(meal, food.Outflow, 10 * 1440 + 20));
        var view = food.Outflow.Capture(12);
        Assert.False(view.Days.Single(d => d.Day == 9).Known);
        Assert.True(view.Days.Single(d => d.Day == 10).Partial);
        var zero = view.Days.Single(d => d.Day == 11);
        Assert.True(zero.Known); Assert.False(zero.Partial); Assert.Equal(BigInteger.Zero, zero.Count);
        Assert.Contains("unknown (history not recorded)", view.Text());
        Assert.Contains("partial day", view.Text());
        Assert.Contains("Day 11: $0 · 0 purchases", view.Text());
        Assert.Contains("$1.23450005054473876953125", view.Text());
    }
    [Fact]
    public void FreeProjectionCannotContainANamedAggregateOrMarketShare()
    {
        var meal = Meal(); meal.BusinessId = "SECRET RIVAL"; meal.PlaceId = "SECRET ADDRESS";
        var food = new FoodCommerceState(); food.Residents.Add(meal);
        var history = RivalOutflowHistory.Begin(food, 0);
        Assert.True(ExternalFoodSettlement.TryPay(meal, history, 1));
        var view = history.Capture(0);
        string json = JsonConvert.SerializeObject(view);
        Assert.DoesNotContain("SECRET", view.Text() + json + history.ToWire());
        Assert.Equal(new[] { "Count", "Days", "Units" }, typeof(RivalOutflowView).GetProperties().Select(p => p.Name).OrderBy(x => x));
        Assert.Equal(new[] { "Count", "Day", "Known", "Partial", "Units" }, typeof(RivalOutflowDay).GetProperties().Select(p => p.Name).OrderBy(x => x));
        Assert.DoesNotContain("share", json, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public void RealD2ReloadWorksWithInactiveMemoryAndAbsentCampaigns()
    {
        var (prepared, market, paths) = RivalOutflowTests.Ready(representation: 3);
        var city = new CitySimulator(prepared.State, paths);
        var food = city.State.Food!;
        city.AttachFoodMarket(market, memorySettings: new FoodMemorySettings { FoodMemoryWeight = 0 });
        double end = food.Residents.Max(m => m.DesiredAt) + 60;
        while (!city.AdvanceTo(end, 1).CaughtUp) { }
        Assert.Equal(3, food.Residents.Sum(m => m.Purchases));
        Assert.Empty(food.Campaigns);
        Assert.All(food.Residents, m => Assert.Empty(m.Memory.Entries));
        Assert.True(food.Outflow!.Reconciles(food)); Assert.Equal(3, city.FreeRivalOutflow()!.Count);
        Assert.Equal(0, market.NativeBuys);
        string before = SaveCodec.Write(city.State);
        var read = SaveCodec.Read(before); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var resumed = new CitySimulator(read.State!, paths);
        resumed.AttachFoodMarket(market, memorySettings: new FoodMemorySettings { FoodMemoryWeight = 0 });
        while (!resumed.AdvanceTo(end + 5, 1).CaughtUp) { }
        Assert.Equal(city.FreeRivalOutflow()!.Units, resumed.FreeRivalOutflow()!.Units);
        Assert.Equal(3, resumed.FreeRivalOutflow()!.Count); Assert.Equal(0, market.NativeBuys);
        Assert.True(resumed.State.Food!.Outflow!.Reconciles(resumed.State.Food));
        var counts = SaveCodec.CountRecords(resumed.State, SaveCodec.Write(resumed.State));
        Assert.Equal(counts.Semantic, counts.Streaming);
    }
    [Theory]
    [InlineData(21)] [InlineData(22)] [InlineData(23)]
    public void ExactOldMigrationKeepsHistoricalLedgerAsUnknownAndPreservesSource(int schema)
    {
        var (city, _, _) = RivalOutflowTests.Ready();
        double end = city.State.Food!.Residents.Single().DesiredAt + 7;
        while (!city.AdvanceTo(end, 1).CaughtUp) { }
        Assert.True(RivalOutflowHistory.Ledger(city.State.Food) > 0);
        city.State.Schema = schema;
        string source = SaveCodec.WriteForSchema(city.State, schema);
        Assert.DoesNotContain("Outflow", source);
        var read = SaveCodec.Read(source); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(schema, read.SourceSchema); Assert.Equal(24, read.State!.Schema);
        Assert.True(read.State.Food!.Outflow!.Reconciles(read.State.Food));
        Assert.Equal(BigInteger.Zero, read.State.Food.Outflow.Capture(1).Units);
        Assert.All(read.State.Food.Outflow.Capture(1).Days.Where(d => d.Day < 1), d => Assert.False(d.Known));
        read.State.Schema = schema;
        Assert.Equal(source, SaveCodec.WriteForSchema(read.State, schema));
    }
    [Theory]
    [InlineData("0|0|0|0|0|0|0,1,0")]
    [InlineData("0|0|0|0|0|0|0,01,1")]
    [InlineData("0|0|0|0|0|0|0,1,1|0,1,1")]
    [InlineData("0|0|15|0|0|0|0,1,1")]
    [InlineData("0|0|0|0|1|1")]
    public void CorruptHistoryRefuses(string wire)
        => Assert.Throws<FormatException>(() => RivalOutflowHistory.FromWire(wire));
    [Fact]
    public void MissingForgedForeignAndFutureHistoryRefuseWithoutChangingInput()
    {
        var (city, _, _) = RivalOutflowTests.Ready();
        string source = SaveCodec.Write(city.State);
        foreach (var value in new JToken?[] { null, JValue.CreateNull(), new JValue("1|0|1|1|0|0"), new JValue(new string('1', 1025)) })
        {
            var json = JObject.Parse(source);
            if (value == null) ((JObject)json["Food"]!).Remove("Outflow"); else json["Food"]!["Outflow"] = value;
            string bad = json.ToString(Formatting.None), original = bad;
            Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(bad).Status); Assert.Equal(original, bad);
        }
        var future = JObject.Parse(source); future["Schema"] = 25;
        Assert.Equal(SaveReadStatus.Future, SaveCodec.Read(future.ToString()).Status);
        var foreign = JObject.Parse(source); foreign["Schema"] = 23;
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(foreign.ToString()).Status);
    }
    [Fact]
    public void CachedFreeViewInvalidatesOnSettlementAndRolloverWithoutChangingPriorSnapshot()
    {
        var meal = Meal(); var food = new FoodCommerceState(); food.Residents.Add(meal);
        var history = RivalOutflowHistory.Begin(food, 0);
        var zero = history.Capture(0); Assert.Same(zero, history.Capture(0));
        string text = zero.Text(); Assert.Same(text, zero.Text());
        Assert.True(ExternalFoodSettlement.TryPay(meal, history, 1));
        var paid = history.Capture(0); Assert.NotSame(zero, paid); Assert.Equal(1, paid.Count);
        Assert.Equal(BigInteger.Zero, zero.Count); Assert.Same(text, zero.Text());
        history.Advance(14); Assert.Equal(BigInteger.Zero, history.Capture(14).Count);
        Assert.Equal(1, paid.Count); Assert.True(history.Reconciles(food));
    }
}

