using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

public sealed class ClockRateFoodSalesTests
{
    private readonly ITestOutputHelper output;
    public ClockRateFoodSalesTests(ITestOutputHelper output) => this.output = output;

    private const double Speed1 = 1.0 / 3, Speed20 = 20.0 / 3, WalkMetresPerSecond = 2;
    private const double Eight = 1440 + 8 * 60, TenThirty = Eight + 150, TwelveThirty = Eight + 270, FourteenThirty = Eight + 390;

    private sealed class NativePaceWalk : IRoutePlanner, IFoodWalkingPace
    {
        public double Rate = Speed1;
        public double WalkingMetresPerMinute => WalkMetresPerSecond / Rate;
        public Route? Plan(Place from, Place to, double at, TravelPreferences preferences)
        {
            double metres = Position.Distance(from.Position, to.Position);
            if (metres > preferences.WalkingToleranceMetres) return null;
            return new Route { Legs = new() { new RouteLeg { From = from.Position, To = to.Position, Mode = TravelMode.Walk,
                Minutes = Math.Max(.01, metres / WalkingMetresPerMinute) } } };
        }
    }

    private sealed class OwnedShop : IFoodMarket
    {
        public const string Id = "ba:street_fourthavenue#19";
        public double EarliestMinute => 0;
        public RloLivingReading Living = new(RloLivingState.On);
        public int Stock = 100;
        public readonly List<string> Orders = new();
        public readonly List<double> Times = new();
        private static readonly List<OpeningWindow> Hours =
            Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 6 * 60, ToMinute = 22 * 60 }).ToList();
        public IReadOnlyList<FoodBusiness> Read(double at) => new[] { new FoodBusiness { Id = Id, PlaceId = "shop:owned", Name = "FT RLO 13d Food v1",
            MealsPerHour = 60, Hours = Hours,
            Offers = new() { new FoodOffer { ProductId = FoodProducts.Croissant, Price = Money.FromCents(500), Units = Stock } } } };
        public readonly Dictionary<string, string> Receipts = new(StringComparer.Ordinal);
        public FoodSale Buy(ResidentMeal meal, double at)
        {
            if (FoodSaleReceipts.AlreadyPaid(Receipts, meal)) return new(true, "Native purchase");
            FoodSaleReceipts.Begin(Receipts, meal);
            if (RloOverrule.CheckoutRefusal(null, Living) is string refusal)
            { FoodSaleReceipts.Cancel(Receipts, meal); return new(false, refusal); }
            if (meal.BusinessId != Id || meal.ProductId != FoodProducts.Croissant || meal.Price != Money.FromCents(500) || Stock <= 0)
            { FoodSaleReceipts.Cancel(Receipts, meal); return new(false, "Price or stock changed"); }
            Assert.DoesNotContain(meal.IntentId, Orders);
            Orders.Add(meal.IntentId); Times.Add(at); Stock--;
            FoodSaleReceipts.Confirm(Receipts, meal);
            return new(true, "Native purchase");
        }
        public string[] PaidReceiptIntents => Receipts.Where(p => p.Value.StartsWith("paid|", StringComparison.Ordinal))
            .Select(p => p.Key.Substring(FoodSaleReceipts.Prefix.Length)).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        public int Between(double from, double to) => Times.Count(t => t >= from && t < to);
    }

    private static (CitySimulator City, OwnedShop Market, NativePaceWalk Routes) RigScale()
    {
        var identities = Enumerable.Range(0, 1200).Select(n => new CitizenIdentity {
            Id = "r" + n, Name = "Resident " + n, Age = 25 + n % 35,
            Persona = n % 3 == 0 ? Persona.Foodie : n % 3 == 1 ? Persona.Commuter : Persona.Grumpy }).ToArray();
        var places = new List<Place>();
        for (int i = 0; i < 160; i++)
            places.Add(new Place { Id = "home:" + i, Name = "Home " + i, Kind = PlaceKind.Home, Capacity = 7, ResidentialAreaSquareMetres = 400,
                Position = new((i % 16) * 40 - 300, 0, (i / 16) * 40 - 180) });
        for (int i = 0; i < 30; i++)
            places.Add(new Place { Id = "work:" + i, Name = "Work " + i, Kind = PlaceKind.Work, Capacity = 40,
                Position = new((i % 6) * 40 - 100, 0, (i / 6) * 40 - 80) });
        places.Add(new Place { Id = "study:0", Name = "School", Kind = PlaceKind.Study, Capacity = 200, Position = new(150, 0, 150) });
        places.Add(new Place { Id = "leisure:0", Name = "Park", Kind = PlaceKind.Leisure, Capacity = 200, Position = new(-150, 0, 150) });
        places.Add(new Place { Id = "shop:owned", Name = "FT RLO 13d Food v1", Kind = PlaceKind.Shop, Capacity = 40, Position = new(0, 0, 0) });
        var state = Population.CreateFromHousing(identities, places, "clock-rate-food", "0.40.4", 1613, Eight);
        state.Food = new FoodCommerceState { MealRhythmVersion = MealRhythm.PolicyVersion, HouseholdFinanceVersion = HouseholdFinance.PolicyVersion,
            RepresentationFactor = 1,
            Shops = new() { new FoodScope { PlaceId = "shop:owned", BusinessId = OwnedShop.Id, ProductId = FoodProducts.Croissant } } };
        TravelTiming.Apply(state, WalkMetresPerSecond, Speed1);
        var market = new OwnedShop();
        var routes = new NativePaceWalk();
        var city = new CitySimulator(state, routes);
        city.AttachFoodMarket(market);
        return (city, market, routes);
    }

    private static CitySimulator Retime(CitySimulator city, NativePaceWalk routes, double rate)
    {
        city.State.PrepareForSave();
        TravelTiming.Apply(city.State, WalkMetresPerSecond, rate);
        routes.Rate = rate;
        return city.RebuildRoutes(routes);
    }

    private static void Advance(CitySimulator city, double to) { while (!city.AdvanceTo(to, 4096).CaughtUp) { } }

    private static void AssertReceiptsMatchNativePurchases(CitySimulator city, OwnedShop market)
    {
        Assert.Equal(market.Orders.Count, market.Orders.Distinct().Count());
        Assert.Equal(market.Orders.Count, city.State.Food!.Residents.Sum(m => m.Purchases));
        Assert.Equal(100 - market.Stock, market.Orders.Count);
        Assert.Equal(market.Orders.OrderBy(id => id, StringComparer.Ordinal).ToArray(), market.PaidReceiptIntents);
        Assert.DoesNotContain(market.Receipts.Values, v => v.StartsWith("pending|", StringComparison.Ordinal));
        var paidMeals = city.State.Food!.Residents.Where(m => FoodSaleReceipts.ReadStatus(market.Receipts, m) == FoodReceiptStatus.Paid);
        Assert.All(paidMeals, m => Assert.Contains(m.IntentId, market.Orders));
    }

    private void Tally(string label, CitySimulator city)
    {
        foreach (var outcome in FoodClockReach.Outcomes(city.State.Food, 6))
            output.WriteLine(label + ": " + outcome.Count + " x " + outcome.Outcome);
    }

    [Fact]
    public void RigScaleFixtureMatchesTheRigPopulation()
    {
        var (city, _, _) = RigScale();
        Assert.Equal(CityBalance.CityResidentTotal, city.State.Citizens.Count);
    }

    [Fact]
    public void SpeedTwentyAdvanceThenSpeedOneKeepsPaidCustomersComing()
    {
        var (city, market, routes) = RigScale();
        city = Retime(city, routes, Speed20);
        Advance(city, TenThirty);
        AssertReceiptsMatchNativePurchases(city, market);
        Assert.Empty(market.PaidReceiptIntents);
        city = Retime(city, routes, Speed1);
        Advance(city, TwelveThirty);
        Assert.NotEmpty(market.PaidReceiptIntents);
        int window = market.Between(TenThirty, TwelveThirty);
        output.WriteLine("speed 20 08:00-10:30: " + market.Between(Eight, TenThirty) + " sales; speed 1 10:30-12:30: " + window + " sales");
        Tally("12:30", city);
        Assert.True(window > 0);
        AssertReceiptsMatchNativePurchases(city, market);
    }

    [Fact]
    public void LivingOffThenOnAfterSpeedTwentyKeepsPaidCustomersComing()
    {
        var (city, market, routes) = RigScale();
        city = Retime(city, routes, Speed20);
        Advance(city, TenThirty);
        city = Retime(city, routes, Speed1);
        AssertReceiptsMatchNativePurchases(city, market);
        market.Living = new RloLivingReading(RloLivingState.Off);
        Advance(city, TwelveThirty);
        AssertReceiptsMatchNativePurchases(city, market);
        int offReceipts = market.PaidReceiptIntents.Length;
        market.Living = new RloLivingReading(RloLivingState.On);
        Advance(city, FourteenThirty);
        Assert.True(market.PaidReceiptIntents.Length > offReceipts);
        int off = market.Between(TenThirty, TwelveThirty), on = market.Between(TwelveThirty, FourteenThirty);
        output.WriteLine("Living off 10:30-12:30: " + off + " sales; Living on 12:30-14:30: " + on + " sales");
        Assert.True(off > 0);
        Assert.True(on > 0);
        AssertReceiptsMatchNativePurchases(city, market);
    }

    [Fact]
    public void SustainedSpeedTwentyLeavesTheShopOutOfWalkingReachAndSaysSo()
    {
        var (city, market, routes) = RigScale();
        city = Retime(city, routes, Speed20);
        Advance(city, FourteenThirty);
        output.WriteLine("speed 20 08:00-14:30: " + market.Orders.Count + " sales; lunch reach "
            + FoodClockReach.LunchReachMetres(routes.WalkingMetresPerMinute) + " m");
        Tally("14:30", city);
        Assert.Empty(market.Orders);
        Assert.Empty(market.PaidReceiptIntents);
        Assert.Equal(0, city.State.Food!.Residents.Sum(m => m.Purchases));
        double nearestWork = city.State.Places.Where(p => p.Kind == PlaceKind.Work).Min(p => Position.Distance(p.Position, new Position(0, 0, 0)));
        Assert.True(FoodClockReach.LunchReachMetres(routes.WalkingMetresPerMinute) < nearestWork);
        Assert.True(FoodClockReach.LunchReachMetres(WalkMetresPerSecond / Speed1) > nearestWork);
        Assert.Contains(FoodClockReach.Outcomes(city.State.Food, 8), o => o.Outcome.StartsWith("Missed", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(1.0 / 3, "Food reach at clock rate 0.333 game minutes per second: walking covers 6.0 m per game minute, so a 60-minute lunch break with 20 minutes eating and 1 minute service reaches food shops within 117 m one way.")]
    [InlineData(20.0 / 3, "Food reach at clock rate 6.667 game minutes per second: walking covers 0.3 m per game minute, so a 60-minute lunch break with 20 minutes eating and 1 minute service reaches food shops within 6 m one way.")]
    public void RetimeLineNamesTheWalkingReach(double rate, string expected)
        => Assert.Equal(expected, FoodClockReach.RetimeLine(rate, WalkMetresPerSecond / rate));

    [Fact]
    public void OutcomeTallyGroupsSettledMealsAndSkipsPending()
    {
        var food = new FoodCommerceState { Residents = new() {
            new ResidentMeal { ResidentId = "a", Status = MealStatus.Missed, Reason = "No suitable stop in the meal window" },
            new ResidentMeal { ResidentId = "b", Status = MealStatus.Missed, Reason = "No suitable stop in the meal window" },
            new ResidentMeal { ResidentId = "c", Status = MealStatus.Paid, Reason = "Native purchase" },
            new ResidentMeal { ResidentId = "d", Status = MealStatus.Pending } } };
        var tally = FoodClockReach.Outcomes(food, 8);
        Assert.Equal(new[] { "Missed: No suitable stop in the meal window", "Paid: Native purchase" }, tally.Select(t => t.Outcome));
        Assert.Equal(new[] { 2, 1 }, tally.Select(t => t.Count));
        Assert.Empty(FoodClockReach.Outcomes(null, 8));
        Assert.Single(FoodClockReach.Outcomes(food, 1));
    }
}
