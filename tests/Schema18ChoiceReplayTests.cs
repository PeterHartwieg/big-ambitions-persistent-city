using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class Schema18ChoiceReplayTests
{
    private static CitySimulator Migrate(CitySimulator city, WeightedFoodChoiceTests.Market market, WeightedFoodChoiceTests.Planner planner)
    {
        string source = LegacyWire.Write(city.State);
        var data = new Dictionary<string, string> { [SaveStore.StateKey] = source, [FoodAuthorityStore.Key] = "untouched authority", ["persistentcity:food-receipt:test"] = "untouched receipt" };
        var session = SaveSession.Open(data, city.State.PopulationKey); var read = session.InitialReadResult;
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error); Assert.Equal(17, read.SourceSchema);
        Assert.Equal(source, LegacyWire.Write(read.State!)); Assert.Same(source, data[SaveStore.StateKey]);
        Assert.True(session.WriteTo(data, read.State!).Success); Assert.Equal(CityState.CurrentSchema, SaveCodec.Read(data[SaveStore.StateKey]).SourceSchema);
        Assert.Equal("untouched authority", data[FoodAuthorityStore.Key]); Assert.Equal("untouched receipt", data["persistentcity:food-receipt:test"]);
        var restored = new CitySimulator(SaveCodec.Read(data[SaveStore.StateKey]).State!, planner); restored.AttachFoodMarket(market); return restored;
    }

    // D3a starts recording on migration; compare every older field separately from that new coverage marker.
    private static string PreOutflow(CityState state)
    {
        var root = JObject.Parse(SaveCodec.Write(state));
        ((JObject)root["Food"]!).Remove("Outflow");
        return root.ToString(Formatting.None);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    [InlineData(6)] [InlineData(7)] [InlineData(8)] [InlineData(9)] [InlineData(10)] [InlineData(11)]
    [InlineData(12)] [InlineData(13)] [InlineData(14)] [InlineData(15)] [InlineData(16)] [InlineData(17)] [InlineData(18)]
    public void EveryA3CaptureQuoteAndSelectedBoundaryMigratesWithoutAChangedPurchase(int units)
    {
        var (control, mc, pc) = WeightedFoodChoiceTests.Ready(79, 12);
        var (changed, mm, pm) = WeightedFoodChoiceTests.Ready(79, 12);
        double at = changed.State.Food!.Residents.Single().DesiredAt;
        for (int i = 0; i < units; i++) changed.AdvanceTo(at, 1);
        changed = Migrate(changed, mm, pm);
        control.State.WeatherStartsDay=changed.State.WeatherStartsDay; // additive next-day weather capability, economy stays exact
        while (!control.AdvanceTo(at + 30, 128).CaughtUp) { }
        while (!changed.AdvanceTo(at + 30, 1).CaughtUp) { }
        Assert.Equal(PreOutflow(control.State), PreOutflow(changed.State));
        Assert.True(changed.State.Food!.Outflow!.Reconciles(changed.State.Food));
        Assert.Equal(mc.Orders, mm.Orders); Assert.Single(mm.Orders); Assert.Equal(pc.Out, pm.Out); Assert.Equal(pc.Back, pm.Back);
        Assert.All(changed.State.Food!.Residents, m => { Assert.Empty(m.Memory.Entries); Assert.Null(m.Memory.Watermark); });
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(5)] [InlineData(12)] [InlineData(20)]
    public void CommittedA3TripServicePaymentAndReturnMigrateWithExactTerms(int offset)
    {
        var (control, mc, pc) = WeightedFoodChoiceTests.Ready(97, 12);
        var (changed, mm, pm) = WeightedFoodChoiceTests.Ready(97, 12);
        double at = changed.State.Food!.Residents.Single().DesiredAt;
        while (!changed.AdvanceTo(at + offset, 1).CaughtUp) { }
        changed = Migrate(changed, mm, pm);
        control.State.WeatherStartsDay=changed.State.WeatherStartsDay; // additive next-day weather capability, economy stays exact
        while (!control.AdvanceTo(at + 30, 128).CaughtUp) { }
        while (!changed.AdvanceTo(at + 30, 1).CaughtUp) { }
        Assert.Equal(PreOutflow(control.State), PreOutflow(changed.State));
        Assert.True(changed.State.Food!.Outflow!.Reconciles(changed.State.Food));
        Assert.Equal(mc.Orders, mm.Orders); Assert.Single(mm.Orders); Assert.Equal(1, mm.Attempts);
        Assert.Equal(pc.Out, pm.Out); Assert.Equal(pc.Back, pm.Back);
    }

    [Fact]
    public void UncertainA3ReceiptMigratesWithoutRetryEvenAfterMidnight()
    {
        var (city, market, planner) = WeightedFoodChoiceTests.Ready(12, 12); market.ThrowAfterSale = true;
        double at = city.State.Food!.Residents.Single().DesiredAt;
        while (!city.AdvanceTo(at + 10, 1).CaughtUp) { }
        var before = city.State.Food.Residents.Single(); Assert.Equal(MealStatus.Uncertain, before.Status);
        city = Migrate(city, market, planner);
        while (!city.AdvanceTo(at + 1500, 128).CaughtUp) { }
        var after = city.State.Food!.Residents.Single(); Assert.Equal(MealStatus.Uncertain, after.Status);
        Assert.Equal(before.IntentId, after.IntentId); Assert.Equal(before.DayAllowanceUsed, after.DayAllowanceUsed);
        Assert.Equal(before.Price, after.Price); Assert.Single(market.Orders); Assert.Equal(1, market.Attempts);
    }
}
