using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;
using Xunit;

namespace PersistentCity.Tests;

[Collection(StreetFoodHooks.Collection)]
public sealed class VehicleMarketReviewTests
{
    public VehicleMarketReviewTests() => StreetFoodHooks.Reset();

    [Theory]
    [InlineData("absent")] [InlineData("off")] [InlineData("withdrawn")] [InlineData("menu")]
    public void LostAuthorityDoesNotSupplyClosedStandEvidence(string reason)
    {
        var f = new VehicleMarketTests.Fixture(); var meal = f.Meal();
        switch (reason)
        {
            case "absent": f.Reload(false); break;
            case "off": StreetFoodHooks.ReadStatus = 1; StreetFoodHooks.Revision++; f.Run(f.Now + 1); break;
            case "withdrawn": Assert.True(f.Market.Withdraw("v00")); f.Run(f.Now + 1); break;
            case "menu": StreetFoodHooks.Find("v00").Items[0] = "unsupported"; StreetFoodHooks.Revision++; f.Run(f.Now + 1); break;
        }
        Assert.False(f.Market.CanServe(meal.BusinessId));
        Assert.Null(f.Market.ReadClosedStand(meal.BusinessId, meal.PlaceId, f.Now));
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void CommittedResidentReachesClosedOrMovedStandAndLearnsClosed(bool move, bool newChoice)
    {
        var f = new VehicleMarketTests.Fixture();
        var state = f.City.State; long start = f.Now;
        state.Minute = start; state.Capabilities.Active = true;
        var person = state.Citizens.Single();
        person.Phase = CitizenPhase.AtPlace; person.Activity = Activity.Home; person.PlaceId = person.HomeId;
        person.NextEventMinute = person.ActivityUntilMinute = start + 120;
        var meal = f.Meal(); meal.ResidentId = meal.AnchorId = person.Identity.Id; meal.Kind = MealKind.Legacy;
        meal.Status = MealStatus.Travelling; meal.DesiredAt = start; meal.DueAt = start + 1;
        meal.ExpiresAt = meal.Deadline = start + 60; meal.ContextAttempts = 1;
        meal.Memory.Watermark = new ExperienceWatermark { Day = meal.Day, Meal = meal.Kind, Context = 1, Stage = ExperienceStage.Committed };
        meal.Budget = new ResidentBudget { OpeningBalance = Money.FromCents(1000), Balance = Money.FromCents(1000),
            LastPostedDay = meal.Day, NextPostingMinute = start + 1440 };
        state.Food!.Residents = new List<ResidentMeal> { meal };
        state.Food.ChoiceStartsDay = meal.Day + (newChoice ? 0 : 1);
        f.City = new CitySimulator(state, f.Planner); f.Market.Rebind(f.City);
        var shop = new VehicleMarketTests.ShopMarket(state.Food, f.Receipts);
        var composite = new FoodMarkets(state.Food, shop, f.Market, id => f.City.FindPlace(id).Position);
        f.City.AttachFoodMarket(composite, rivalSettings: new RivalFoodSettings { RivalChoiceWeight = 0 });
        var destination = f.City.FindPlace(meal.PlaceId).Position;
        var outward = new Route { Legs = new() { new RouteLeg { From = f.City.FindPlace(person.HomeId).Position,
            To = destination, Mode = TravelMode.Walk, Minutes = 5 } } };
        Assert.True(f.City.TryBeginMealVisit(person.Identity.Id, meal.PlaceId, outward, 5, 10, start + 60));
        StreetFoodHooks.Close("v00");
        if (move) StreetFoodHooks.Open("v00", x: 400);
        for (long minute = start + 1; minute <= start + 5; minute++)
        {
            f.Run(minute);
            Assert.False(f.Market.CanServe(meal.BusinessId));
            Assert.Null(f.Market.CaptureBusiness(meal.BusinessId, minute));
            while (!f.City.AdvanceTo(minute, 1).CaughtUp) { }
            if (minute < start + 5) { Assert.Equal(MealStatus.Travelling, meal.Status); Assert.Empty(meal.Memory.Entries); }
        }
        Assert.Equal(MealStatus.Missed, meal.Status);
        Assert.Equal(ShopExperience.Closed, Assert.Single(meal.Memory.Entries).Outcome);
        Assert.Equal(ExperienceStage.Failed, meal.Memory.Watermark!.Stage);
        Assert.Equal(Money.FromCents(1000), meal.Budget.Balance);
        Assert.Equal(0, f.Api.SellCalls); Assert.Empty(f.Receipts);
        Assert.Equal(destination, f.City.FindPlace(meal.PlaceId).Position);
        if (move) Assert.NotNull(state.Vendors.Find("v00")!.DeferredStand);
    }

    [Theory]
    [InlineData(2)] [InlineData(10)]
    public void ShopOnlyLocalReadPreservesNativeOrderingToleranceAndBatchedRead(int count)
    {
        var f = new VehicleMarketTests.Fixture(shops: count);
        f.City.State.Food!.Shops.RemoveAll(s => s.Channel == FoodScopeChannel.Vehicle);
        var shop = new VehicleMarketTests.ShopMarket(f.City.State.Food, f.Receipts);
        var composite = new FoodMarkets(f.City.State.Food, shop, f.Market, id => f.City.FindPlace(id).Position);
        var result = composite.ReadLocal(f.City.FindPlace("home"), f.Now, 0, 8);
        Assert.Equal(1, shop.LocalReads); Assert.Equal(0, shop.Captures);
        Assert.Equal(Math.Min(count, 8), result.Count);
        Assert.Equal(Enumerable.Range(0, result.Count).Select(i => "b" + i), result.Select(b => b.Id));
    }

    [Fact]
    public void SmallMixedReadRetainsFullReadOrderingAndDoesNotColdCaptureShops()
    {
        var f = new VehicleMarketTests.Fixture();
        var shop = new VehicleMarketTests.ShopMarket(f.City.State.Food!, f.Receipts);
        var composite = new FoodMarkets(f.City.State.Food!, shop, f.Market, id => f.City.FindPlace(id).Position);
        Assert.Equal(composite.Read(f.Now).Select(b => b.Id), composite.ReadLocal(new Place(), f.Now, 0, 8).Select(b => b.Id));
        Assert.Equal(0, shop.Captures);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PreEffectStockoutIsLearnedOnlyWithConfirmedZeroUnits(bool stockout)
    {
        var f = new VehicleMarketTests.Fixture(); var meal = f.Meal();
        if (stockout) f.Api.OnSell = () => StreetFoodHooks.Find("v00").Units[0] = 0;
        else f.Api.Fault = "refuse3";
        var sale = f.Market.Buy(meal, f.Now);
        Assert.False(sale.Paid);
        Assert.Equal(stockout ? ShopExperience.Stockout : (ShopExperience?)null, sale.Failure);
        Assert.Equal(FoodReceiptStatus.Missing, FoodSaleReceipts.ReadStatus(f.Receipts, meal));
        Assert.Equal(0, StreetFoodHooks.Sequence); Assert.Equal(1, f.Api.SellCalls);
    }

    [Fact]
    public void FailedOptionalStockoutReadKeepsDefiniteRefusalAndCancelledReceipt()
    {
        var f = new VehicleMarketTests.Fixture(); var meal = f.Meal(); f.Api.Fault = "refuse3";
        f.Api.OnSell = () => StreetFoodHooks.ThrowOnRead = true;
        var sale = f.Market.Buy(meal, f.Now);
        Assert.False(sale.Paid); Assert.Null(sale.Failure);
        Assert.Equal(FoodReceiptStatus.Missing, FoodSaleReceipts.ReadStatus(f.Receipts, meal));
    }
}
