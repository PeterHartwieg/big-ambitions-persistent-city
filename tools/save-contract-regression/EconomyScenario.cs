using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;

/// <summary>
/// A fixed three-day food city (two consumers per resident, three meals, household finances, three shops,
/// checkout refusals, lost acknowledgements and a mid-run save/reload). Uses only API that exists before
/// and after schema 17, so the same source compiles against both trees. The economy digest excludes only
/// the fields schema 17 added; equal digests mean the same purchases, money and meal state.
/// </summary>
public static class EconomyScenario
{
    public static readonly string[] Schema17Fields =
        { "Traits", "RepresentedTraits", "Today", "Decision", "LastContextKey", "ContextAttempts", "ChoicePolicyVersion", "ChoiceStartsDay" };

    private sealed class Walking : IRoutePlanner
    {
        public Route? Plan(Place from, Place to, double at, TravelPreferences preferences) => new Route
        { Legs = { new RouteLeg { From = from.Position, To = to.Position, Mode = TravelMode.Walk,
            Minutes = Math.Max(1, Position.Distance(from.Position, to.Position) / 80) } } };
    }

    private sealed class Market : IFoodMarket, IFoodSaleEvidence
    {
        private static readonly (string Place, string Business, string[] Products)[] Shops =
        {
            ("shop-1", "b1", new[] { FoodProducts.Croissant, FoodProducts.Burger }),
            ("shop-2", "b2", new[] { FoodProducts.HotDog }),
            ("shop-3", "b3", new[] { FoodProducts.Burger, FoodProducts.HotDog, FoodProducts.Croissant }),
        };
        private readonly Dictionary<string, int> units = new Dictionary<string, int>(StringComparer.Ordinal);
        public readonly List<string> Orders = new List<string>();
        public double EarliestMinute => 0;
        private static Money Price(string product) => product == FoodProducts.Croissant ? Money.FromCents(350)
            : product == FoodProducts.Burger ? Money.FromCents(725) : Money.FromCents(480);
        public IReadOnlyList<FoodBusiness> Read(double minute) => Shops.Select(s => new FoodBusiness
        {
            Id = s.Business, PlaceId = s.Place, Name = s.Place, MealsPerHour = 20,
            Hours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 360, ToMinute = 1320 }).ToList(),
            Offers = s.Products.Select(p => new FoodOffer { ProductId = p, Price = Price(p),
                Units = units.TryGetValue(s.Place + "\n" + p, out int sold) ? Math.Max(0, 400 - sold) : 400 }).ToList()
        }).ToList();
        public FoodSale Buy(ResidentMeal meal, double minute)
        {
            if (meal.Price != Price(meal.ProductId)) return new FoodSale(false, "Price changed");
            int attempt = Orders.Count + 1;
            if (attempt % 17 == 5) { Orders.Add("refused:" + meal.IntentId); return new FoodSale(false, "Unavailable at checkout"); }
            string key = meal.PlaceId + "\n" + meal.ProductId;
            units[key] = (units.TryGetValue(key, out int sold) ? sold : 0) + 1;
            Orders.Add(meal.IntentId + "@" + minute.ToString("R", CultureInfo.InvariantCulture) + ":" + meal.Price.Units.ToString(CultureInfo.InvariantCulture));
            if (attempt % 23 == 11) throw new InvalidOperationException("Lost native acknowledgement");
            return new FoodSale(true, "Native fake confirmed");
        }
        public bool IsConfirmedPaid(ResidentMeal meal) => Orders.Any(o => o.StartsWith(meal.IntentId + "@", StringComparison.Ordinal));
    }

    public static CityState Create(int residents, int seed)
    {
        // At most 5,000 immutable donors exist; larger cities grow through housing expansion with pc: residents.
        int imported = residents <= 5000 ? residents : 1000;
        var identities = Enumerable.Range(0, imported).Select(i => new CitizenIdentity
        {
            Id = "rlo:econ:c" + i.ToString("D5", CultureInfo.InvariantCulture), Name = "Citizen " + i, Age = 20 + i % 50,
            District = i % 2 == 0 ? "a" : "b", Persona = (Persona)(i % 8), AppearanceSeed = i + 1
        });
        var places = new[]
        {
            new Place { Id = "home-a", Name = "Home A", District = "a", Kind = PlaceKind.Home, Capacity = residents, Position = new Position(0, 0, 0) },
            new Place { Id = "home-b", Name = "Home B", District = "b", Kind = PlaceKind.Home, Capacity = residents, Position = new Position(500, 0, 0) },
            new Place { Id = "work-1", Name = "Work 1", District = "a", Kind = PlaceKind.Work, Capacity = residents, Position = new Position(200, 0, 300) },
            new Place { Id = "work-2", Name = "Work 2", District = "b", Kind = PlaceKind.Work, Capacity = residents, Position = new Position(700, 0, -100) },
            new Place { Id = "school", Name = "School", District = "a", Kind = PlaceKind.Study, Capacity = residents, Position = new Position(100, 0, -400) },
            new Place { Id = "shop-1", Name = "Shop 1", District = "a", Kind = PlaceKind.Shop, Position = new Position(150, 0, 120) },
            new Place { Id = "shop-2", Name = "Shop 2", District = "b", Kind = PlaceKind.Shop, Position = new Position(600, 0, 100) },
            new Place { Id = "shop-3", Name = "Shop 3", District = "a", Kind = PlaceKind.Shop, Position = new Position(300, 0, -200) },
            new Place { Id = "leisure-1", Name = "Leisure", District = "a", Kind = PlaceKind.Leisure, Position = new Position(60, 0, 250), OpenMinute = 600, CloseMinute = 1380 },
        };
        var city = Population.Create(identities, places, "economy-scenario", "0.41.1", seed, 1440 + 300);
        if (imported == residents) return city;
        long beds = (long)residents * 10 / 9;
        while (HousingPolicy.Target(beds) < residents) beds++;
        city.Places.Single(p => p.Id == "home-a").Capacity = (int)(beds - beds / 2);
        city.Places.Single(p => p.Id == "home-b").Capacity = (int)(beds / 2);
        Population.ExpandToHousing(city);
        return city;
    }

    public static FoodCommerceState Food() => new FoodCommerceState
    {
        RepresentationFactor = 2, MealRhythmVersion = MealRhythm.PolicyVersion,
        Shops = new List<FoodScope>
        {
            new FoodScope { PlaceId = "shop-1", BusinessId = "b1", ProductId = FoodProducts.Croissant, AdditionalProductIds = { FoodProducts.Burger } },
            new FoodScope { PlaceId = "shop-2", BusinessId = "b2", ProductId = FoodProducts.HotDog },
            new FoodScope { PlaceId = "shop-3", BusinessId = "b3", ProductId = FoodProducts.Burger, AdditionalProductIds = { FoodProducts.HotDog, FoodProducts.Croissant } },
        }
    };

    /// <summary>Run the fixed scenario and return the order log and economy digests.</summary>
    public static object Run(int residents = 60, int seed = 23)
    {
        var state = Create(residents, seed);
        state.Food = Food();
        var routes = new Walking(); var market = new Market();
        var city = new CitySimulator(state, routes); city.AttachFoodMarket(market); city.EnableHouseholdFinances();
        string? reloaded = null;
        for (double at = state.Minute + 60; at <= 4 * 1440; at += 60)
        {
            while (!city.AdvanceTo(at, 64).CaughtUp) { }
            if (reloaded == null && at >= 2 * 1440 + 750)
            {
                reloaded = SaveCodec.Write(city.State);
                var read = SaveCodec.Read(reloaded);
                if (read.Status != SaveReadStatus.Loaded) throw new InvalidOperationException("Reload failed: " + read.Error);
                city = new CitySimulator(read.State!, routes); city.AttachFoodMarket(market);
            }
        }
        string final = SaveCodec.Write(city.State);
        var meals = city.State.Food!.Residents;
        return new
        {
            orders = market.Orders.Count,
            ordersDigest = Digest(string.Join("\n", market.Orders)),
            purchases = meals.Sum(m => m.Purchases),
            spentUnits = meals.Sum(m => m.Spent.Units),
            uncertain = meals.Count(m => m.Status == MealStatus.Uncertain),
            economyDigest = Digest(WithoutSchema17(final)),
            reloadDigest = Digest(WithoutSchema17(reloaded!)),
        };
    }

    public static string WithoutSchema17(string payload)
    {
        var token = JObject.Parse(payload);
        token.Remove("Schema");
        foreach (var property in token.Descendants().OfType<JProperty>().Where(p => Schema17Fields.Contains(p.Name)).ToList()) property.Remove();
        return token.ToString(Formatting.None);
    }

    public static string Digest(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
