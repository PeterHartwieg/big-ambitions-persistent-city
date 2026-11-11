using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PersistentCity.Core;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

/// <summary>
/// Rig 11h/11i natural first sale: a ready, staffed 09:00–18:00 food shop at a normal distance from
/// homes and workplaces. Runtime walks at the native pedestrian pace of 2 m per scaled second divided
/// by the observed clock rate (game minutes per scaled second). The rig checkpoints carry
/// TravelClockRate 1/3 before the FT speed-20 advance and 20/3 after it; at 20/3 walking covers 0.3 m
/// per game minute, so no break fits a round trip to any shop the person is not already standing in.
/// </summary>
public sealed class FirstSaleTravelPaceTests
{
    private readonly ITestOutputHelper output;
    public FirstSaleTravelPaceTests(ITestOutputHelper output) => this.output = output;
    private const double NativeWalkingMetresPerSecond = 2; // NativeTravelPace.WalkingMetresPerSecond
    private const string Shop = "shop-player", Business = "player";

    /// <summary>Straight-line walking at Runtime's pace; the same bound and pace contracts MultimodalRoutes offers.</summary>
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

    /// <summary>The rig shop: one croissant at $5, 100 units, one staffed counter (20 meals per hour), 09:00–18:00 daily.</summary>
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
        // Distances from the shop: home 90 m, work 60 m, school 120 m, leisure 75 m.
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
        // The rig window: through the first full shop day and on to next-day noon.
        for (double at = state.Minute + 60; at <= 3 * 1440 + 720; at += 60)
            while (!city.AdvanceTo(at, 64).CaughtUp) { }
        output.WriteLine("rate " + clockRate.ToString("R", CultureInfo.InvariantCulture) + ": " + market.Orders.Count + " orders; "
            + string.Join(", ", reasons.OrderBy(r => r.Key, StringComparer.Ordinal).Select(r => r.Key + "=" + r.Value)));
        return (market, reasons);
    }

    [Fact]
    public void StaffedShopAtNormalDistanceSellsAtTheNativeCalendarPace()
    {
        var (market, reasons) = Run(1.0 / 3); // MinutesMultiplier 1 × default clock factor 1/3: 6 m per game minute
        Assert.True(market.Orders.Count > 0, "No natural sale; reasons: " + string.Join(", ", reasons.Select(r => r.Key + "=" + r.Value)));
    }

    [Fact]
    public void FtSpeedTwentyClockRateLeavesNoBreakForARoundTrip()
    {
        // The rate saved in every rig 11i checkpoint after "run_until … speed 20": 0.3 m per game minute.
        var (market, reasons) = Run(20.0 / 3);
        Assert.Empty(market.Orders);
        Assert.Contains(reasons.Keys, r => r == "Service and return do not fit available time" || r == "Travel and service cannot fit break");
        Assert.DoesNotContain(reasons.Keys, r => r.StartsWith("Chosen", StringComparison.Ordinal));
    }
}
