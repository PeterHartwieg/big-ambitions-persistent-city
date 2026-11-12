using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PersistentCity.Core;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

public sealed class FirstSaleTravelPaceTests
{
    private readonly ITestOutputHelper output;
    public FirstSaleTravelPaceTests(ITestOutputHelper output) => this.output = output;
    private const double NativeWalkingMetresPerSecond = 2;
    private const string Shop = "shop-player", Business = "player";

    private sealed class PacedWalking : IRoutePlanner, IWalkingTimeBound, IFoodWalkingPace
    {
        public PacedWalking(double clockRate) => WalkingMetresPerMinute = NativeWalkingMetresPerSecond / clockRate;
        public double WalkingMetresPerMinute { get; }
        public double MinimumWalkingMinutes(Place from, Place to) => Math.Max(0, Position.Distance(from.Position, to.Position) - .1) / WalkingMetresPerMinute;
        public Route? Plan(Place from, Place to, double at, TravelPreferences preferences)
        {
            double metres = Position.Distance(from.Position, to.Position);
            if (metres > preferences.WalkingToleranceMetres) return null;
            return new Route { Legs = { new RouteLeg { From = from.Position, To = to.Position, Mode = TravelMode.Walk,
                Minutes = Math.Max(.01, metres / WalkingMetresPerMinute) } } };
        }
    }

    private sealed class PlayerShop : IFoodMarket
    {
        private int sold;
        public readonly List<string> Orders = new();
        public double EarliestMinute => 0;
        public IReadOnlyList<FoodBusiness> Read(double minute) => new[] { new FoodBusiness {
            Id = Business, PlaceId = Shop, Name = "Player coffee shop", MealsPerHour = 20, CustomerServicePercent = 50,
            Hours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 540, ToMinute = 1080 }).ToList(),
            Offers = { new FoodOffer { ProductId = FoodProducts.Croissant, Price = Money.FromCents(500), Units = Math.Max(0, 100 - sold) } } } };
        public FoodSale Buy(ResidentMeal meal, double minute)
        {
            sold++; Orders.Add(meal.IntentId + "@" + minute.ToString("R", CultureInfo.InvariantCulture));
            return new FoodSale(true, "Native fake confirmed");
        }
    }

    private (PlayerShop Market, Dictionary<string, int> Reasons) Run(double clockRate)
    {
        var identities = Enumerable.Range(0, 60).Select(i => new CitizenIdentity
        {
            Id = "rlo:firstsale:c" + i.ToString("D3", CultureInfo.InvariantCulture), Name = "Citizen " + i, Age = 20 + i % 45,
            District = "a", Persona = (Persona)(i % 8), AppearanceSeed = i + 1
        });
        var places = new[]
        {
            new Place { Id = "home", Name = "Home", District = "a", Kind = PlaceKind.Home, Capacity = 60, Position = new Position(90, 0, 0) },
            new Place { Id = "work", Name = "Work", District = "a", Kind = PlaceKind.Work, Capacity = 60, Position = new Position(0, 0, 60) },
            new Place { Id = "school", Name = "School", District = "a", Kind = PlaceKind.Study, Capacity = 60, Position = new Position(0, 0, -120) },
            new Place { Id = Shop, Name = "Player shop", District = "a", Kind = PlaceKind.Shop, Position = new Position(0, 0, 0) },
            new Place { Id = "leisure", Name = "Leisure", District = "a", Kind = PlaceKind.Leisure, Position = new Position(-75, 0, 0), OpenMinute = 600, CloseMinute = 1380 },
        };
        var state = Population.Create(identities, places, "first-sale-pace", "0.41.1", 23, 1440 + 300);
        state.TravelTimingVersion = 1; state.TravelClockRate = clockRate;
        state.Food = new FoodCommerceState { RepresentationFactor = 2, MealRhythmVersion = MealRhythm.PolicyVersion,
            Shops = { new FoodScope { PlaceId = Shop, BusinessId = Business, ProductId = FoodProducts.Croissant } } };
        var market = new PlayerShop();
        var city = new CitySimulator(state, new PacedWalking(clockRate)); city.AttachFoodMarket(market); city.EnableHouseholdFinances();
        var reasons = new Dictionary<string, int>(StringComparer.Ordinal);
        city.FoodDecisionObserved += trace =>
        {
            foreach (var choice in trace.Intent.Choices.Where(c => c.BusinessId == Business))
                reasons[choice.Reason] = reasons.TryGetValue(choice.Reason, out int n) ? n + 1 : 1;
        };
        for (double at = state.Minute + 60; at <= 2 * 1440 + 720; at += 60)
            while (!city.AdvanceTo(at, 64).CaughtUp) { }
        output.WriteLine("rate " + clockRate.ToString("R", CultureInfo.InvariantCulture) + ": " + market.Orders.Count + " orders; "
            + string.Join(", ", reasons.OrderBy(r => r.Key, StringComparer.Ordinal).Select(r => r.Key + "=" + r.Value)));
        return (market, reasons);
    }

    [Fact]
    public void StaffedShopAtNormalDistanceSellsAtTheNativeCalendarPace()
    {
        var (market, reasons) = Run(1.0 / 3);
        Assert.True(market.Orders.Count > 0, "No natural sale; reasons: " + string.Join(", ", reasons.Select(r => r.Key + "=" + r.Value)));
    }

    [Fact]
    public void FtSpeedTwentyClockRateLeavesNoBreakForARoundTrip()
    {
        var (market, reasons) = Run(20.0 / 3);
        Assert.Empty(market.Orders);
        Assert.Contains(reasons.Keys, r => r == "Service and return do not fit available time" || r == "Travel and service cannot fit break");
        Assert.DoesNotContain(reasons.Keys, r => r.StartsWith("Chosen", StringComparison.Ordinal));
    }
}
