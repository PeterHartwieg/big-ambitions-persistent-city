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
    private sealed class Market : IFoodMarket, IFoodSaleEvidence
    {
        public double EarliestMinute { get; set; }
        public int Units = 100, Capacity = 15, OpenFromMinute;
        public Money Price = Money.FromCents(100);
        public string BusinessId = "b";
        public string UnavailableReason = "";
        public string[] Products = new[] { FoodProducts.Croissant };
        public bool ThrowAfterSale;
        public string ThrowForResident = "";
        public bool ConfirmEvidence;
        public bool ThrowOnEvidence;
        public bool Closed { get; set; }
        public List<string> Orders = new();
        public List<double> Times = new();
        public bool IsConfirmedPaid(ResidentMeal meal) => ThrowOnEvidence
            ? throw new InvalidOperationException("Evidence unavailable") : ConfirmEvidence && Orders.Contains(meal.IntentId);
        public IReadOnlyList<FoodBusiness> Read(double minute) => new[] { new FoodBusiness { Id = BusinessId, PlaceId = "shop", Name = "Bakery", ServiceUnavailableReason = UnavailableReason,
            MealsPerHour = Capacity, Hours = Closed ? new() : Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = OpenFromMinute, ToMinute = 1440 }).ToList(),
            Offers = Products.Select(p => new FoodOffer { ProductId = p, Price = Price, Units = Units }).ToList() } };
        public FoodSale Buy(ResidentMeal meal, double at)
        {
            if (Units <= 0 || Capacity <= 0 || Closed || Price != meal.Price) return new(false, "Unavailable at checkout");
            Assert.DoesNotContain(meal.IntentId, Orders); Orders.Add(meal.IntentId); Times.Add(at); Units--;
            if (ThrowAfterSale || ThrowForResident == meal.ResidentId) throw new Exception("Lost native acknowledgement");
            return new(true, "Native fake confirmed");
        }
    }
    private static (CitySimulator city, Market market, Walking routes) Ready(int count = 1, bool atWork = false, bool threeMeals = false, int representation = 1)
    {
        var state = Population.Create(Enumerable.Range(0, count).Select(n => new CitizenIdentity {
            Id = "r" + n, Name = "Resident " + n, Persona = Persona.Foodie, Age = 30 }).ToArray(), new[] {
                new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = count },
                new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = count, Position = new(1,0,0) },
                new Place { Id = "shop", Name = "Bakery", Kind = PlaceKind.Shop, Position = new(2,0,0) } }, "food-live", "0.40.4", 7, 1440);
        foreach (var c in state.Citizens)
        {

            c.Phase = CitizenPhase.AtPlace; c.PlaceId = atWork ? "work" : "home";
            c.Activity = atWork ? Activity.Work : Activity.Home;
            c.NextEventMinute = c.ActivityUntilMinute = 2870;
        }
        state.Food = new FoodCommerceState { RepresentationFactor = representation, MealRhythmVersion = threeMeals ? MealRhythm.PolicyVersion : 0, Shops = new() { new FoodScope { PlaceId = "shop", BusinessId = "b", ProductId = FoodProducts.Croissant } } };
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
        Assert.Equal(Money.Sum(a.State.Food.Residents.Select(m=>m.Spent)),Money.Sum(b.State.Food!.Residents.Select(m=>m.Spent)));
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
        Advance(city, desired + 2); market.Units = 0; market.Price = Money.FromCents(200);
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
        var (city, market, _) = Ready(); market.Price = Money.FromCents(100000);
        Advance(city, city.State.Food!.Residents.Single().DesiredAt + 80);
        Assert.Empty(market.Orders); Assert.Equal(MealStatus.Declined, city.State.Food.Residents.Single().Status);
        Assert.Equal(0, city.State.Citizens.Single().CompletedJourneys);
    }
    [Fact]
    public void OldPopulationSchemaAndCurrentCorruptFoodOwnershipAreRejected()
    {
        var (city, _, _) = Ready(); var wire = JObject.Parse(LegacyWire.Write(city.State));
        wire["Schema"] = 7; StripNewFields(wire);
        var read = SaveCodec.Read(wire.ToString()); Assert.Equal(SaveReadStatus.Invalid, read.Status); Assert.Null(read.State);
        wire = JObject.Parse(LegacyWire.Write(city.State)); wire["Food"]!["Residents"]![0]!["ResidentId"] = "unknown";
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
        restoredMeal.Price += Money.FromCents(1); Assert.Throws<InvalidOperationException>(() => FoodSaleReceipts.AlreadyPaid(nativeData, restoredMeal));
        Assert.Empty(market.Orders);
    }
    [Fact]
    public void UncertainNativeReceiptCannotExpireIntoASecondPurchase()
    {
        var (_, _, _) = Ready(); var meal = new ResidentMeal { ResidentId="r", Day=1, Price=Money.FromCents(900), BusinessId="b", ProductId=FoodProducts.Croissant };
        var nativeData = new Dictionary<string,string> { ["rlo:data"]="keep" };
        FoodSaleReceipts.Begin(nativeData,meal);
        Assert.Throws<InvalidOperationException>(() => FoodSaleReceipts.AlreadyPaid(nativeData,meal));
        FoodSaleReceipts.Prune(nativeData,2); Assert.Equal(2,nativeData.Count);
        FoodSaleReceipts.Prune(nativeData,3); Assert.Equal(2,nativeData.Count); Assert.Equal("keep",nativeData["rlo:data"]);
        Assert.Throws<InvalidOperationException>(() => FoodSaleReceipts.AlreadyPaid(nativeData,meal));
        Assert.Throws<InvalidOperationException>(() => FoodSaleReceipts.Begin(nativeData,meal));
    }

    internal static void StripNewFields(JObject wire)
    {
        wire.Remove("Food"); foreach (JObject person in wire["Citizens"]!) person.Remove("MealOuting");
    }
    [Theory]
    [InlineData(1)] [InlineData(128)]
    public void ExistingShopStopPaysWithoutAnotherJourneyAndSurvivesReload(int budget)
    {
        var (city, market, routes) = Ready();
        var person = city.State.Citizens.Single();
        person.PlaceId = "shop"; person.Activity = Activity.Shop;
        double end = person.NextEventMinute;
        city = city.RebuildRoutes(routes);
        double desired = city.State.Food!.Residents.Single().DesiredAt;
        Advance(city, desired + 2, budget);
        Assert.Equal(MealStatus.Serving, city.State.Food.Residents.Single().Status);
        Assert.Null(person.MealOuting); Assert.Equal(0, person.CompletedJourneys);
        city = Reload(city, market, routes);
        Advance(city, desired + 30, budget);
        Assert.Single(market.Orders);
        var meal = city.State.Food!.Residents.Single();
        Assert.Equal(1, meal.ExistingStopPurchases); Assert.True(meal.ExistingStop);
        Assert.Equal(Money.FromCents(100), meal.Spent);
        Assert.Equal(end, city.State.Citizens.Single().NextEventMinute);
        Assert.Equal(0, city.State.Citizens.Single().CompletedJourneys);
        city = Reload(city, market, routes);
        Advance(city, desired + 100, budget);
        Assert.Single(market.Orders);
    }

    [Fact]
    public void LaterOrdinaryStopCanFulfilPreviouslyUnmetNeedWithoutNewIntention()
    {
        var (city, market, routes) = Ready();
        routes.Reachable = false;
        var meal = city.State.Food!.Residents.Single();
        string intention = meal.IntentId;
        Advance(city, meal.DesiredAt + 1);
        Assert.Equal(MealStatus.Missed, meal.Status);
        routes.Reachable = true;
        var c = city.State.Citizens.Single();
        var outward = routes.Plan(city.State.Places.Single(p => p.Id == "home"),
            city.State.Places.Single(p => p.Id == "shop"), city.State.Minute, c.Preferences)!;
        Assert.True(city.TryBeginOptionalShopVisit(c.Identity.Id, "shop", outward, 5, 30, c.NextEventMinute));
        Advance(city, city.State.Minute + 15);
        Assert.Single(market.Orders); Assert.Equal(intention, market.Orders.Single());
        Assert.Equal(1, meal.ExistingStopPurchases); Assert.Null(c.MealOuting);
    }

    [Theory]
    [InlineData(100, 0, 100)]
    [InlineData(100, 100, 0)]
    [InlineData(1000000, 100, 15)]
    public void ExistingStopStillRespectsStockServiceAndAffordability(int price, int units, int capacity)
    {
        var (city, market, routes) = Ready();
        var person = city.State.Citizens.Single(); person.PlaceId = "shop"; person.Activity = Activity.Shop;
        market.Price = Money.FromCents(price); market.Units = units; market.Capacity = capacity;
        city = city.RebuildRoutes(routes);
        Advance(city, city.State.Food!.Residents.Single().DesiredAt + 30);
        Assert.Empty(market.Orders); Assert.Null(person.MealOuting);
        Assert.Equal(0, person.CompletedJourneys);
    }

    [Fact]
    public void AttachedPurchaseCannotExtendAnExistingShortVisit()
    {
        var (city, market, routes) = Ready();
        var meal = city.State.Food!.Residents.Single();
        var c = city.State.Citizens.Single(); c.PlaceId = "shop"; c.Activity = Activity.Shop;
        c.NextEventMinute = c.ActivityUntilMinute = meal.DesiredAt + 10;
        city = city.RebuildRoutes(routes);
        Advance(city, meal.DesiredAt + 9);
        Assert.Empty(market.Orders); Assert.Equal(meal.DesiredAt + 10, c.NextEventMinute);
    }

    [Fact]
    public void SchemaEightFoodHistoryIsPreservedByRefusingUpgrade()
    {
        var (city, market, _) = Ready(); Advance(city, city.State.Food!.Residents.Single().DesiredAt + 40);
        var payload = JObject.Parse(LegacyWire.Write(city.State)); payload["Schema"] = 8;
        RemoveMealRhythmFields(payload);
        foreach (var m in payload["Food"]!["Residents"]!.Children<JObject>())
        { m.Remove("ExistingStop"); m.Remove("ExistingStopPurchases"); m.Remove("Budget"); }
        ((JObject)payload["Food"]!).Remove("HouseholdFinanceVersion");
        ((JObject)payload["Food"]!).Remove("DepartedBudgetAssetsCents"); ((JObject)payload["Food"]!).Remove("History");
        string source = payload.ToString(); var data = new Dictionary<string,string> { [SaveStore.StateKey] = source };
        var read = SaveStore.ReadFrom(data, city.State.PopulationKey);
        Assert.Equal(SaveReadStatus.Invalid, read.Status); Assert.Null(read.State);
        Assert.False(SaveStore.WriteTo(data, city.State).Success); Assert.Equal(source, data[SaveStore.StateKey]); Assert.Single(market.Orders);
    }

    [Fact]
    public void PersistentHouseholdPaysEssentialsAndActualPurchaseOnceAcrossReload()
    {
        var (city, market, routes) = Ready();
        var c = city.State.Citizens.Single(); c.PlaceId = "shop"; c.Activity = Activity.Shop;
        city = city.RebuildRoutes(routes); city.EnableHouseholdFinances();
        Advance(city, city.State.Minute + 1);
        var budget = city.State.Food!.Residents.Single().Budget!;
        Money before = budget.Balance;
        Assert.True(budget.IncomeReceived > Money.Zero); Assert.True(budget.HomeFoodPaid > Money.Zero);
        Assert.True(budget.EssentialsPaid > Money.Zero);
        double desired = city.State.Food.Residents.Single().DesiredAt;
        Advance(city, desired + 2); city = Reload(city, market, routes);
        Advance(city, desired + 30);
        budget = city.State.Food!.Residents.Single().Budget!;
        Assert.Single(market.Orders); Assert.Equal(before - market.Price, budget.Balance);
        Assert.Equal(market.Price, budget.PreparedFoodSpent); Assert.Equal(Money.FromCents(0), budget.HeldPurchase);
        Money paid = budget.IncomeReceived;
        city = Reload(city, market, routes); Advance(city, desired + 60);
        Assert.Equal(paid, city.State.Food!.Residents.Single().Budget!.IncomeReceived);
        Assert.Single(market.Orders);
    }

    [Fact]
    public void UncertainNativeSaleRetainsFundsWithoutFundingAnotherPurchase()
    {
        var (city, market, routes) = Ready();
        var c = city.State.Citizens.Single(); c.PlaceId = "shop"; c.Activity = Activity.Shop;
        city = city.RebuildRoutes(routes); city.EnableHouseholdFinances(); market.ThrowAfterSale = true;
        double desired = city.State.Food!.Residents.Single().DesiredAt;
        Advance(city, desired + 30);
        var meal = city.State.Food.Residents.Single();
        Assert.Equal(MealStatus.Uncertain, meal.Status); Assert.Equal(market.Price, meal.Budget!.HeldPurchase);
        Assert.Equal(Money.FromCents(0), meal.Budget.PreparedFoodSpent);
        city = Reload(city, market, routes); Advance(city, desired + 1440 + 60);
        Assert.Single(market.Orders);
        Assert.Equal(market.Price, city.State.Food!.Residents.Single().Budget!.HeldPurchase);
        Assert.Equal(Money.FromCents(0), HouseholdFinance.SpendingLimit(city.State.Food.Residents.Single().Budget!));
    }

    [Fact]
    public void RefusedNativeCheckoutRefundsOnlyItsReservedHouseholdFunds()
    {
        var (city, market, routes) = Ready();
        var c = city.State.Citizens.Single(); c.PlaceId = "shop"; c.Activity = Activity.Shop;
        city = city.RebuildRoutes(routes); city.EnableHouseholdFinances();
        double desired = city.State.Food!.Residents.Single().DesiredAt;
        Advance(city, desired + 2); var budget = city.State.Food.Residents.Single().Budget!;
        Money before = budget.Balance; market.Units = 0;
        Advance(city, desired + 30);
        Assert.Empty(market.Orders); Assert.Equal(before, budget.Balance);
        Assert.Equal(Money.FromCents(0), budget.HeldPurchase); Assert.Equal(Money.FromCents(0), budget.PreparedFoodSpent);
    }

    [Fact]
    public void FinancialPostingIsIndependentOfBatchSizeAndReloadBoundaries()
    {
        var (a, ma, ra) = Ready(24); var (b, mb, _) = Ready(24);
        a.EnableHouseholdFinances(); b.EnableHouseholdFinances();
        Advance(a, 3 * 1440 + 700, 1);
        Advance(b, 2 * 1440 + 1, 128); b = Reload(b, mb, ra); Advance(b, 3 * 1440 + 700, 128);
        Assert.Equal(SaveCodec.Write(a.State), SaveCodec.Write(b.State));
        Assert.Equal(ma.Orders, mb.Orders);
    }

    [Fact]
    public void HouseholdSaveRejectsCreatedCashAndUnknownFinancePolicy()
    {
        var (city, _, _) = Ready(); city.EnableHouseholdFinances(); Advance(city, city.State.Minute + 1);
        var payload = JObject.Parse(LegacyWire.Write(city.State));
        payload["Food"]!["Residents"]![0]!["Budget"]!["Balance"] = 999999999L;
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(payload.ToString()).Status);
        payload = JObject.Parse(LegacyWire.Write(city.State)); payload["Food"]!["HouseholdFinanceVersion"] = 99;
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(payload.ToString()).Status);
    }

    [Fact]
    public void SchemaNineCannotSilentlyBecomeACurrentBudgetedCity()
    {
        var (city, _, _) = Ready(); var payload = JObject.Parse(LegacyWire.Write(city.State)); payload["Schema"] = 9;
        RemoveMealRhythmFields(payload);
        ((JObject)payload["Food"]!).Remove("HouseholdFinanceVersion"); ((JObject)payload["Food"]!).Remove("DepartedBudgetAssetsCents");
        ((JObject)payload["Food"]!).Remove("History");
        foreach (var m in payload["Food"]!["Residents"]!.Children<JObject>()) m.Remove("Budget");
        string source = payload.ToString(); var data = new Dictionary<string,string> { [SaveStore.StateKey] = source };
        var read = SaveStore.ReadFrom(data, city.State.PopulationKey);
        Assert.Equal(SaveReadStatus.Invalid, read.Status); Assert.Null(read.State);
        Assert.False(SaveStore.WriteTo(data, city.State).Success); Assert.Equal(source, data[SaveStore.StateKey]);
    }

    [Fact]
    public void ABetterLaterOfferCanReplaceAnEarlierDeclineButCannotBuyTwice()
    {
        var (city, market, routes) = Ready();
        var meal = city.State.Food!.Residents.Single(); market.Price = Money.FromCents(100000);
        Advance(city, meal.DesiredAt + 1); Assert.Equal(MealStatus.Declined, meal.Status);
        string intention = meal.IntentId; market.Price = Money.FromCents(100);
        var c = city.State.Citizens.Single();
        var outward = routes.Plan(city.State.Places.Single(p => p.Id == "home"),
            city.State.Places.Single(p => p.Id == "shop"), city.State.Minute, c.Preferences)!;
        Assert.True(city.TryBeginOptionalShopVisit(c.Identity.Id, "shop", outward, 5, 30, c.NextEventMinute));
        Advance(city, city.State.Minute + 20);
        Assert.Equal(intention, market.Orders.Single()); Assert.Equal(1, meal.Purchases);
        city = Reload(city, market, routes); Advance(city, city.State.Minute + 30);
        Assert.Single(market.Orders);
    }

    [Fact]
    public void ManagementHistoryKeepsFourteenDaysAndSurvivesReloadWithoutDoubleCounting()
    {
        var (city, market, routes) = Ready(4); market.Units = 1000;
        city.EnableHouseholdFinances();
        Advance(city, 22 * 1440);
        var history = city.State.Food!.History;
        Assert.NotEmpty(history); Assert.True(history.Count <= 14);
        Assert.True(history.Min(h => h.Day) >= history.Max(h => h.Day) - 13);
        Assert.All(history, h => {
            Assert.Equal(h.Purchases, h.PurchaseHours.Sum());
            Assert.Equal(h.Purchases, h.BuyerDistricts.Sum(d => d.Purchases));
            Assert.Equal(h.OffersConsidered, h.ChosenOffers + h.PriceRejections + h.AccessRejections + h.ClosedRejections
                + h.StockRejections + h.ServiceRejections + h.UnknownEvidence + h.PreferredAlternatives);
        });
        int orders = market.Orders.Count; string before = SaveCodec.Write(city.State);
        city = Reload(city, market, routes);
        Assert.Equal(before, SaveCodec.Write(city.State)); Assert.Equal(orders, market.Orders.Count);
        Assert.True(history.Sum(h => h.Purchases) > 0);
    }

    [Fact]
    public void ManagementHistoryRejectsInventedCustomerCounts()
    {
        var (city, _, _) = Ready(); Advance(city, city.State.Food!.Residents.Single().DesiredAt + 40);
        var payload = JObject.Parse(LegacyWire.Write(city.State));
        payload["Food"]!["History"]![0]!["PurchaseHours"]![0] = 999;
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(payload.ToString()).Status);
    }

    [Theory]
    [InlineData(1)] [InlineData(16)] [InlineData(128)]
    public void HiddenClockWaitsForBudgetedEventsAndPreservesNativePurchasesAcrossHours(int eventBudget)
    {
        var (reference, expected, _) = Ready(60);
        var (city, actual, _) = Ready(60);
        reference.EnableHouseholdFinances(); city.EnableHouseholdFinances();
        double target = 4 * 1440.0, clock = city.State.Minute;
        Advance(reference, target);
        int frames = 0, held = 0;
        while (!city.IsCaughtUpTo(target))
        {
            Assert.True(++frames < 100000);
            double step = EconomySkipClock.Limit(city.IsCaughtUpTo(clock), clock, target - clock);
            if (step == 0) held++;
            clock += step; actual.EarliestMinute = Math.Floor(clock / 60) * 60;
            city.AdvanceTo(clock, eventBudget);
        }
        Assert.True(held > 0); Assert.NotEmpty(actual.Orders);
        Assert.Equal(expected.Orders, actual.Orders);
        Assert.Equal(SaveCodec.Write(reference.State), SaveCodec.Write(city.State));
    }

}
