using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class FoodCommerceTests
{
    private sealed class BoundedWalking : IRoutePlanner, IWalkingTimeBound
    {
        public bool UseBound;
        public int Calls;
        public double MinimumWalkingMinutes(Place from, Place to) => UseBound ? Position.Distance(from.Position,to.Position)/2 : 0;
        public Route Plan(Place from, Place to, double at, TravelPreferences preferences)
        {
            Calls++;
            return new Route { Legs = new() { new RouteLeg { From=from.Position,To=to.Position,
                Minutes=Math.Max(.01,Position.Distance(from.Position,to.Position)/2),Mode=TravelMode.Walk } } };
        }
    }
    private sealed class Walking : IRoutePlanner
    {
        public bool Reachable = true;
        public double Minutes = 5;
        public Route? Plan(Place from, Place to, double at, TravelPreferences preferences) => !Reachable ? null : new()
        { Legs = new() { new RouteLeg { From = from.Position, To = to.Position, Minutes = Minutes, Mode = TravelMode.Walk } } };
    }
    private sealed class Market : IFoodMarket
    {
        public double EarliestMinute { get; set; }
        public int Units = 100, Price = 100, Capacity = 15;
        public bool ThrowAfterSale;
        public bool Closed { get; set; }
        public List<string> Orders = new();
        public List<double> Times = new();
        public IReadOnlyList<FoodBusiness> Read(double minute) => new[] { new FoodBusiness { Id = "b", PlaceId = "shop", Name = "Bakery",
            MealsPerHour = Capacity, Hours = Closed ? new() : Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 0, ToMinute = 1440 }).ToList(),
            Offers = new() { new FoodOffer { ProductId = "pastry", PriceCents = Price, Units = Units } } } };
        public FoodSale Buy(ResidentMeal meal, double at)
        {
            if (Units <= 0 || Capacity <= 0 || Closed || Price != meal.PriceCents) return new(false, "Unavailable at checkout");
            Assert.DoesNotContain(meal.IntentId, Orders); Orders.Add(meal.IntentId); Times.Add(at); Units--;
            if (ThrowAfterSale) throw new Exception("Lost native acknowledgement");
            return new(true, "Native fake confirmed");
        }
    }
    private static (CitySimulator city, Market market, Walking routes) Ready(int count = 1, bool atWork = false)
    {
        var state = Population.Create(Enumerable.Range(0, count).Select(n => new CitizenIdentity {
            Id = "r" + n, Name = "Resident " + n, Persona = Persona.Foodie, Age = 30 }).ToArray(), new[] {
                new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = count },
                new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = count, Position = new(1,0,0) },
                new Place { Id = "shop", Name = "Bakery", Kind = PlaceKind.Shop, Position = new(2,0,0) } }, "food-live", "0.40.4", 7, 1440);
        foreach (var c in state.Citizens)
        {
            c.NextDepartureMinute = c.NextJobChangeMinute = ResidentLifecycle.Never;
            c.Phase = CitizenPhase.AtPlace; c.PlaceId = atWork ? "work" : "home";
            c.Activity = atWork ? Activity.Work : Activity.Home;
            c.NextEventMinute = c.ActivityUntilMinute = 2870;
        }
        state.Food = new FoodCommerceState { Shops = new() { new FoodScope { PlaceId = "shop", BusinessId = "b", ProductId = "pastry" } } };
        var market = new Market(); var routes = new Walking(); var city = new CitySimulator(state, routes); city.AttachFoodMarket(market);
        return (city, market, routes);
    }
    private static void Advance(CitySimulator city, double at, int budget = 128)
    { while (!city.AdvanceTo(at, budget).CaughtUp) { } }
    private static CitySimulator Reload(CitySimulator city, Market market, Walking routes)
    {
        var read = SaveCodec.Read(SaveCodec.Write(city.State)); Assert.Equal("", read.Error);
        var restored = new CitySimulator(read.State!, routes); restored.AttachFoodMarket(market); return restored;
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NaturalOpportunityVisitsAndReturnsToOriginalActivity(bool atWork)
    {
        var (city, market, _) = Ready(atWork: atWork);
        double desired = city.State.Food!.Residents.Single().DesiredAt;
        Advance(city, desired - 1); Assert.Empty(market.Orders);
        Advance(city, desired + 40);
        Assert.Single(market.Orders); Assert.Equal(99, market.Units);
        var c = city.State.Citizens.Single(); Assert.Equal(atWork ? "work" : "home", c.PlaceId);
        Assert.Equal(atWork ? Activity.Work : Activity.Home, c.Activity); Assert.Null(c.MealOuting);
        Assert.Equal(2870, c.NextEventMinute);
        Assert.Equal(1, city.State.Food.Residents.Single().Purchases);
        Assert.Single(c.RecentVisits, v => v.Activity == Activity.Shop);
    }

    [Theory]
    [InlineData(8)] [InlineData(100)]
    public void ThousandResidentsKeepPurchasesWithConservativeRoutePruning(int metres)
    {
        var (a, ma, _) = Ready(1200); var (b, mb, _) = Ready(1200);
        foreach(var city in new[] {a,b}) city.State.Places.Single(p=>p.Id=="shop").Position=new(metres,0,0);
        var slow = new BoundedWalking(); var bounded = new BoundedWalking {UseBound=true};
        a=a.RebuildRoutes(slow); b=b.RebuildRoutes(bounded);
        double until=a.State.Food!.Residents.Max(m=>m.DesiredAt)+80;
        Advance(a,until); Advance(b,until,1);
        Assert.Equal(ma.Orders,mb.Orders);
        Assert.Equal(a.State.Food.Residents.Sum(m=>m.SpentCents),b.State.Food!.Residents.Sum(m=>m.SpentCents));
        Assert.Equal(a.State.Citizens.Sum(c=>c.CompletedJourneys),b.State.Citizens.Sum(c=>c.CompletedJourneys));
        if(metres==100) {Assert.Equal(0,bounded.Calls);Assert.Equal(2400,slow.Calls);}
        else Assert.NotEmpty(mb.Orders);
    }

    [Fact]
    public void FoodObservationSurvivesRouteRebuildAndCanBeUnsubscribed()
    {
        var (city, market, routes)=Ready();var original=city;int observed=0;
        Action<FoodDecisionTrace> handler=trace=>{observed++;Assert.Single(trace.Intent.Choices);};
        city.FoodDecisionObserved+=handler;city=city.RebuildRoutes(routes);
        Advance(city,city.State.Food!.Residents.Single().DesiredAt+40);Assert.Equal(1,observed);
        original.FoodDecisionObserved-=handler;Advance(city,3*1440);Assert.Equal(1,observed);
        Assert.True(market.Orders.Count>1);
    }
    [Theory]
    [InlineData(2)] [InlineData(5)] [InlineData(7)] [InlineData(10)] [InlineData(27)]
    public void ReloadBeforeAndAfterCheckoutNeverDuplicatesNativeOrder(double afterDecision)
    {
        var (city, market, routes) = Ready(atWork:true); double desired = city.State.Food!.Residents.Single().DesiredAt;
        Advance(city, desired + afterDecision);
        city = Reload(city, market, routes); city = city.RebuildRoutes(routes);
        Advance(city, desired + 40, 1); city = Reload(city, market, routes); Advance(city, desired + 80);
        Assert.Single(market.Orders); Assert.Equal(MealStatus.Paid, city.State.Food!.Residents.Single().Status);
        Assert.Null(city.State.Citizens.Single().MealOuting);
    }
    [Fact]
    public void DailyRecurrenceAndEventBudgetDoNotChangeDecisionsOrSales()
    {
        var (a, ma, _) = Ready(30); var (b, mb, routes) = Ready(30);
        Advance(a, 6 * 1440); for(double at = 1440; at <= 6 * 1440; at += 3) Advance(b, at, 1);
        b = Reload(b, mb, routes);
        Assert.Equal(SaveCodec.Write(a.State), SaveCodec.Write(b.State)); Assert.Equal(ma.Orders, mb.Orders);
        Assert.True(ma.Orders.Count > 30); Assert.Equal(ma.Orders.Count, ma.Orders.Distinct().Count());
        Assert.Equal(ma.Orders.Count, a.State.Food!.Residents.Sum(m => m.Purchases));
        Assert.All(ma.Times.Zip(ma.Times.Skip(1), (x,y) => y-x), gap => Assert.True(gap >= 4 - 1e-7));
    }
    [Theory]
    [InlineData(2, 1.0/3)] [InlineData(6, 1.0/3)] [InlineData(27, 1.0/3)]
    [InlineData(2, 3)] [InlineData(6, 3)] [InlineData(27, 3)]
    public void ClockChangeDuringMealPreservesServiceDeadlinesAndExactlyOnePurchase(double afterDecision, double rate)
    {
        var (city, market, routes) = Ready(atWork:true);
        city.State.TravelTimingVersion=1;city.State.TravelClockRate=1;
        double desired=city.State.Food!.Residents.Single().DesiredAt;
        Advance(city,desired+afterDecision);
        var person=city.State.Citizens.Single();var meal=city.State.Food.Residents.Single();
        double deadline=meal.Deadline, service=meal.ServiceAt, oldReturn=meal.ReturnMinutes;
        var before=person.Journey?.PositionAt(city.State.Minute-person.DepartureMinute);
        city.State.PrepareForSave();Assert.True(TravelTiming.Apply(city.State,2,rate));routes.Minutes*=rate;
        if(before.HasValue) Assert.True(Position.Distance(before.Value,person.Journey!.PositionAt(city.State.Minute-person.DepartureMinute))<1e-8);
        Assert.Equal(deadline,meal.Deadline);Assert.Equal(service,meal.ServiceAt);
        if(meal.Status==MealStatus.Travelling) Assert.Equal(person.NextEventMinute,meal.DueAt);
        if(meal.Status==MealStatus.Travelling || meal.Status==MealStatus.Serving) Assert.Equal(oldReturn*rate,meal.ReturnMinutes);
        city=Reload(city,market,routes);Advance(city,desired+74,1);city=Reload(city,market,routes);
        Assert.Single(market.Orders);Assert.Equal(99,market.Units);Assert.Null(city.State.Citizens.Single().MealOuting);
        Assert.Equal("work",city.State.Citizens.Single().PlaceId);Assert.Equal(2870,city.State.Citizens.Single().NextEventMinute);
        Assert.True(market.Times.Single()<deadline);
    }
    [Fact]
    public void SoldOutOrPriceChangeDuringTravelDoesNotInventSale()
    {
        var (city, market, _) = Ready(); var desired = city.State.Food!.Residents.Single().DesiredAt;
        Advance(city, desired + 2); market.Units = 0; market.Price = 200;
        Advance(city, desired + 40); Assert.Empty(market.Orders); Assert.Equal(MealStatus.Missed, city.State.Food.Residents.Single().Status);
    }
    [Fact]
    public void StaffingReductionDuringTravelExtendsServiceBeforeReturningToWork()
    {
        var (city, market, _) = Ready(atWork:true); var desired = city.State.Food!.Residents.Single().DesiredAt;
        Advance(city, desired + 2); market.Capacity = 5;
        Advance(city, desired + 16); Assert.Empty(market.Orders);
        Advance(city, desired + 40); Assert.Single(market.Orders); Assert.Equal(desired + 17, market.Times.Single());
        Assert.Equal("work", city.State.Citizens.Single().PlaceId);
    }

    [Fact]
    public void UnknownAcknowledgementSurvivesReloadWithoutRetry()
    {
        var (city, market, routes) = Ready(); market.ThrowAfterSale = true;
        var desired = city.State.Food!.Residents.Single().DesiredAt; Advance(city, desired + 10);
        city = Reload(city, market, routes); Advance(city, desired + 40);
        Assert.Single(market.Orders); Assert.Equal(MealStatus.Uncertain, city.State.Food!.Residents.Single().Status);
        Assert.Equal(0, city.State.Food.Residents.Single().Purchases);
    }
    [Fact]
    public void CatchupDoesNotBookHistoricalPurchasesFromCurrentStock()
    {
        var (city, market, _) = Ready(); market.EarliestMinute = 4 * 1440;
        Advance(city, 4 * 1440); Assert.Empty(market.Orders);
        Advance(city, 5 * 1440); Assert.All(market.Times, time => Assert.True(time >= 4 * 1440));
        Assert.Single(market.Orders);
    }
    [Fact]
    public void HighPriceMayProduceNoSalesAndDoesNotInterruptRoutine()
    {
        var (city, market, _) = Ready(); market.Price = 100000;
        Advance(city, city.State.Food!.Residents.Single().DesiredAt + 80);
        Assert.Empty(market.Orders); Assert.Equal(MealStatus.Declined, city.State.Food.Residents.Single().Status);
        Assert.Equal(0, city.State.Citizens.Single().CompletedJourneys);
    }
    [Fact]
    public void SchemaSevenMigrationHasNoAutomaticEconomyAndCorruptOwnershipIsRejected()
    {
        var (city, _, _) = Ready(); var wire = JObject.Parse(SaveCodec.Write(city.State));
        wire["Schema"] = 7; StripNewFields(wire);
        var read = SaveCodec.Read(wire.ToString()); Assert.Equal("", read.Error); Assert.Null(read.State!.Food);
        wire = JObject.Parse(SaveCodec.Write(city.State)); wire["Food"]!["Residents"]![0]!["ResidentId"] = "unknown";
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(wire.ToString()).Status);
    }
    [Fact]
    public void NativeReceiptPreventsReplayEvenWithAnOlderPopulationSnapshot()
    {
        var (city, market, _) = Ready(); var meal = city.State.Food!.Residents.Single();
        Advance(city, meal.DesiredAt + 2); var oldPopulation = SaveCodec.Write(city.State);
        var nativeData = new Dictionary<string,string>();
        FoodSaleReceipts.Begin(nativeData, meal); FoodSaleReceipts.Confirm(nativeData, meal);
        var restoredMeal = SaveCodec.Read(oldPopulation).State!.Food!.Residents.Single();
        Assert.True(FoodSaleReceipts.AlreadyPaid(nativeData, restoredMeal));
        Assert.Throws<InvalidOperationException>(() => FoodSaleReceipts.Begin(nativeData, restoredMeal));
        restoredMeal.PriceCents++; Assert.Throws<InvalidOperationException>(() => FoodSaleReceipts.AlreadyPaid(nativeData, restoredMeal));
        Assert.Empty(market.Orders);
    }
    [Fact]
    public void UncertainNativeReceiptCannotBeRetriedAndOldDaysAreBounded()
    {
        var (_, _, _) = Ready(); var meal = new ResidentMeal { ResidentId="r", Day=1, PriceCents=900, BusinessId="b", ProductId="pastry" };
        var nativeData = new Dictionary<string,string> { ["rlo:data"]="keep" };
        FoodSaleReceipts.Begin(nativeData,meal);
        Assert.Throws<InvalidOperationException>(() => FoodSaleReceipts.AlreadyPaid(nativeData,meal));
        FoodSaleReceipts.Prune(nativeData,2); Assert.Equal(2,nativeData.Count);
        FoodSaleReceipts.Prune(nativeData,3); Assert.Single(nativeData); Assert.Equal("keep",nativeData["rlo:data"]);
    }

    internal static void StripNewFields(JObject wire)
    {
        wire.Remove("Food"); foreach (JObject person in wire["Citizens"]!) person.Remove("MealOuting");
    }
}
