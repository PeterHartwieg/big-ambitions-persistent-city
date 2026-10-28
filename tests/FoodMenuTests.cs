using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;
namespace PersistentCity.Tests;
public sealed partial class FoodCommerceTests
{
    private static void RemoveMenuFields(JObject payload)
    {
        foreach (var shop in payload["Food"]!["Shops"]!.Children<JObject>()) shop.Remove("AdditionalProductIds");
        foreach (var history in payload["Food"]!["History"]!.Children<JObject>()) history.Remove("ProductSales");
    }
    private static FoodIntent MenuChoice(MealKind kind, int burgerPrice = 800, bool burgerStock = true)
    {
        var consumer = new FoodConsumer { Id = "r", DailyAllowanceCents = 3000, ValueOfTimeCentsPerMinute = 10 };
        var intent = new FoodIntent { Id = "trial", ConsumerId = "r", OriginPlaceId = "home", Deadline = 1000,
            EatingMinutes = 15, MaximumQueueMinutes = 15, OutsideOptionScoreCents = 2500 };
        var shop = new FoodBusiness { Id = "b", PlaceId = "shop", MealsPerHour = 15,
            Hours = Enumerable.Range(0,7).Select(d => new OpeningWindow { Day = d, FromMinute = 0, ToMinute = 1440 }).ToList(),
            Offers = new() { new FoodOffer { ProductId = FoodProducts.Croissant, PriceCents = 800, Units = 10 },
                new FoodOffer { ProductId = FoodProducts.Burger, PriceCents = burgerPrice, Units = burgerStock ? 10 : 0 } } };
        FoodDemand.ChooseObserved(consumer, new[] { shop }, new[] { new FoodTravelQuote { ConsumerId = "r", IntentId = "trial", BusinessId = "b", Minutes = 5, ReturnMinutes = 5 } }, intent, 700, mealKind: kind);
        return intent;
    }
    [Fact]
    public void SameMenuOffersDifferentBreakfastAndDinnerAndPriceOrStockCausesSubstitution()
    {
        Assert.Equal(FoodProducts.Croissant, MenuChoice(MealKind.Breakfast).ProductId);
        Assert.Equal(FoodProducts.Burger, MenuChoice(MealKind.Dinner).ProductId);
        Assert.Equal(FoodProducts.Croissant, MenuChoice(MealKind.Dinner, 2200).ProductId);
        Assert.Equal(FoodProducts.Croissant, MenuChoice(MealKind.Dinner, burgerStock: false).ProductId);
        Assert.False(FoodProducts.Known("ba:itemname_unknown"));
    }
    [Theory]
    [InlineData("stock")] [InlineData("service")] [InlineData("closed")] [InlineData("price")]
    public void KnownUnavailableShopsNeedNoRoutes(string reason)
    {
        var (city, market, _) = Ready(1200); var routes = new BoundedWalking(); city = city.RebuildRoutes(routes);
        if (reason == "stock") market.Units = 0;
        if (reason == "service") market.Capacity = 0;
        if (reason == "closed") market.Closed = true;
        if (reason == "price") market.Price = 1000001;
        Advance(city, city.State.Food!.Residents.Max(m => m.DesiredAt) + 80);
        Assert.Equal(0, routes.Calls); Assert.Empty(market.Orders);
    }
    [Fact]
    public void UnknownEvidenceIsNotSilentlyClassifiedAsUnavailable()
    {
        var business = new FoodBusiness { MealsPerHour = null,
            Hours = Enumerable.Range(0,7).Select(d => new OpeningWindow { Day = d, FromMinute = 0, ToMinute = 1440 }).ToList(),
            Offers = new() { new FoodOffer { ProductId = FoodProducts.Burger, PriceCents = 100, Units = null } } };
        Assert.Null(FoodDemand.KnownUnavailable(business, new FoodConsumer { DailyAllowanceCents = 1000 }, 1, 60));
    }
    [Theory]
    [InlineData(2, false)] [InlineData(7, false)] [InlineData(40, false)] [InlineData(40, true)]
    public void SchemaElevenPendingPaidOrUncertainPurchasesAreRefusedWithoutReplacingFunds(int after, bool uncertain)
    {
        var (city, market, _) = Ready(threeMeals: true); city.EnableHouseholdFinances(); market.ThrowAfterSale = uncertain;
        Advance(city, city.State.Food!.Residents.Single().DesiredAt + after);
        var payload = JObject.Parse(SaveCodec.Write(city.State)); payload["Schema"] = 11; RemoveMenuFields(payload);
        string source = payload.ToString(); var orders = market.Orders.ToArray();
        var data = new Dictionary<string,string> { [SaveStore.StateKey] = source };
        var read = SaveStore.ReadFrom(data, city.State.PopulationKey);
        Assert.Equal(SaveReadStatus.Invalid, read.Status); Assert.Null(read.State);
        Assert.False(SaveStore.WriteTo(data, city.State).Success); Assert.Equal(source, data[SaveStore.StateKey]);
        Assert.Equal(orders, market.Orders);
        if (uncertain) Assert.Equal(100, city.State.Food.Residents.Single().Budget!.HeldPurchaseCents);
    }
    [Theory]
    [InlineData("duplicate")] [InlineData("too-many")] [InlineData("null")] [InlineData("missing")]
    public void MalformedMenusAreRejected(string error)
    {
        var (city, _, _) = Ready(); var payload = JObject.Parse(SaveCodec.Write(city.State));
        var shop = (JObject)payload["Food"]!["Shops"]![0]!;
        if (error == "duplicate") shop["AdditionalProductIds"] = new JArray("pastry");
        if (error == "too-many") shop["AdditionalProductIds"] = new JArray("a", "b", "c", "d");
        if (error == "null") shop["AdditionalProductIds"] = null;
        if (error == "missing") shop.Remove("AdditionalProductIds");
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(payload.ToString()).Status);
    }
    [Fact]
    public void ProductSalesReconcileWithMealAndRevenueHistory()
    {
        var (city, _, _) = Ready(threeMeals: true); Advance(city, 2860);
        var row = city.State.Food!.History.Single(); Assert.Equal(3, row.ProductSales.Single().Purchases);
        var payload = JObject.Parse(SaveCodec.Write(city.State));
        payload["Food"]!["History"]![0]!["ProductSales"]![0]!["RevenueCents"] = 1;
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(payload.ToString()).Status);
    }
    private sealed class MenuMarket : IFoodMarket
    {
        public double EarliestMinute => 0;
        public int Bags = 10000;
        public Dictionary<string,int> Stock = new() { [FoodProducts.Croissant] = 10000, [FoodProducts.Burger] = 10000 };
        public List<string> Orders = new();
        public IReadOnlyList<FoodBusiness> Read(double at) => new[] { new FoodBusiness { Id = "b", PlaceId = "shop", MealsPerHour = 15,
            Hours = Enumerable.Range(0,7).Select(d => new OpeningWindow { Day=d, FromMinute=0, ToMinute=1440 }).ToList(),
            Offers = Stock.Select(p => new FoodOffer { ProductId=p.Key, PriceCents=100, Units=Math.Min(p.Value,Bags) }).ToList() } };
        public FoodSale Buy(ResidentMeal meal, double at)
        {
            if (Bags == 0 || Stock[meal.ProductId] == 0) return new(false, "Stock changed");
            Assert.DoesNotContain(meal.IntentId, Orders); Orders.Add(meal.IntentId); Bags--; Stock[meal.ProductId]--;
            return new(true, "Menu order confirmed");
        }
    }
    private static (CitySimulator city, MenuMarket market, Walking routes) ReadyMenu(int count, bool threeMeals = false)
    {
        var (city, _, routes) = Ready(count, threeMeals: threeMeals);
        var scope = city.State.Food!.Shops.Single(); scope.ProductId = FoodProducts.Croissant;
        scope.AdditionalProductIds.Add(FoodProducts.Burger);
        var market = new MenuMarket(); city = new CitySimulator(city.State, routes); city.AttachFoodMarket(market);
        return (city, market, routes);
    }
    [Fact]
    public void ReservingOneProductDoesNotConsumeAnotherProductsInventory()
    {
        var (city, market, routes) = ReadyMenu(2);
        var meals = city.State.Food!.Residents; var first = meals[0]; var second = meals[1];
        second.DesiredAt = first.DesiredAt; second.DueAt = first.DueAt; second.ExpiresAt = first.ExpiresAt;
        market.Stock[FoodProducts.Croissant] = market.Stock[FoodProducts.Burger] = 1; market.Bags = 2;
        city = new CitySimulator(city.State, routes); city.AttachFoodMarket(market);
        Advance(city, first.DesiredAt + 60);
        Assert.Equal(2, market.Orders.Count); Assert.All(market.Stock.Values, n => Assert.Equal(0, n));
        Assert.Equal(0, market.Bags); Assert.Equal(200, meals.Sum(m => m.SpentCents));
        Assert.Equal(2, city.State.Food!.History.Single().ProductSales.Count); SaveCodec.Write(city.State);
    }
    [Fact]
    public void LastSharedBagDoesNotCauseTwoDebitsForDifferentProducts()
    {
        var (city, market, routes) = ReadyMenu(2);
        var meals = city.State.Food!.Residents; var first = meals[0]; var second = meals[1];
        second.DesiredAt = first.DesiredAt; second.DueAt = first.DueAt; second.ExpiresAt = first.ExpiresAt;
        market.Stock[FoodProducts.Croissant] = market.Stock[FoodProducts.Burger] = 1; market.Bags = 1;
        city = new CitySimulator(city.State, routes); city.AttachFoodMarket(market); city.EnableHouseholdFinances();
        Advance(city, first.DesiredAt + 60);
        Assert.Single(market.Orders); Assert.Equal(0, market.Bags); Assert.Equal(1, market.Stock.Values.Sum());
        Assert.Equal(100, meals.Sum(m => m.Budget!.PreparedFoodSpentCents));
        Assert.All(meals, m => Assert.Equal(0, m.Budget!.HeldPurchaseCents)); SaveCodec.Write(city.State);
    }
    [Fact]
    public void TwelveHundredMenuBuyersHaveTheSameResultsAcrossBatchingAndReload()
    {
        var (a, ma, _) = ReadyMenu(1200, true); var (b, mb, routes) = ReadyMenu(1200, true);
        a.EnableHouseholdFinances(); b.EnableHouseholdFinances();
        Advance(a, 2860); Advance(b, 2400, 1);
        var read = SaveCodec.Read(SaveCodec.Write(b.State)); Assert.Equal("", read.Error);
        b = new CitySimulator(read.State!, routes); b.AttachFoodMarket(mb); Advance(b, 2860, 1);
        Assert.Equal(ma.Orders, mb.Orders); Assert.Equal(ma.Bags, mb.Bags);
        Assert.Equal(SaveCodec.Write(a.State), SaveCodec.Write(b.State));
    }

    [Theory]
    [InlineData("closed")] [InlineData("service")] [InlineData("stock")] [InlineData("price")]
    public void AvailabilityReasonsAreIdenticalWithFullRoutesAndEarlyRejection(string cause)
    {
        var consumer = new FoodConsumer { Id="r", DailyAllowanceCents=500 };
        var shop = new FoodBusiness { Id="b", PlaceId="shop", MealsPerHour=cause=="service"?0:15,
            Hours=cause=="closed"?new():Enumerable.Range(0,7).Select(d=>new OpeningWindow{Day=d,FromMinute=0,ToMinute=1440}).ToList(),
            Offers=new(){new(){ProductId=FoodProducts.Croissant,PriceCents=cause=="price"?600:100,Units=cause=="stock"?0:10}} };
        FoodIntent Intent()=>new(){Id="trial",ConsumerId="r",OriginPlaceId="home",Deadline=800,OutsideOptionScoreCents=1000};
        var full=Intent();var pruned=Intent();
        FoodDemand.ChooseObserved(consumer,new[]{shop},new[]{new FoodTravelQuote{ConsumerId="r",IntentId="trial",BusinessId="b",Minutes=5,ReturnMinutes=5}},full,700);
        var reason=FoodDemand.KnownUnavailable(shop,consumer,700,800);Assert.NotNull(reason);
        FoodDemand.ChooseObserved(consumer,new[]{shop},Array.Empty<FoodTravelQuote>(),pruned,700,new Dictionary<string,string>{{"b",reason!}});
        Assert.Equal(Newtonsoft.Json.JsonConvert.SerializeObject(full),Newtonsoft.Json.JsonConvert.SerializeObject(pruned));
    }

}
