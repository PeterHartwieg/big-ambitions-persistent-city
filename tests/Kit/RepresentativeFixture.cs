using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using PersistentCity.Core;
using PersistentCity.Persistence;

public static class RepresentativeFixture
{
    public const int Seed = 23;
    public const double TargetMinute = 2910;

    public sealed class Routes : IRoutePlanner
    {
        public long Calls;
        public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences)
        {
            Calls++;
            return new Route { Legs = { new RouteLeg { From = from.Position, To = to.Position,
                Mode = TravelMode.Walk, Minutes = Math.Max(1, Position.Distance(from.Position, to.Position) / 80) } } };
        }
    }

    public sealed class Market : IFoodMarket
    {
        public long Reads, Sales;
        public double EarliestMinute => 0;
        public IReadOnlyList<FoodBusiness> Read(double minute)
        {
            Reads++;
            return new[] { new FoodBusiness { Id = "b1", PlaceId = "shop-1", Name = "Shop 1", MealsPerHour = 10000,
                Hours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 0, ToMinute = 1440 }).ToList(),
                Offers = new() { new FoodOffer { ProductId = FoodProducts.Croissant, Price = Money.FromCents(100), Units = 1000000 } } } };
        }
        public FoodSale Buy(ResidentMeal meal, double minute) { Sales++; return new FoodSale(true, "detached confirmed"); }
    }

    public sealed class Replay
    {
        public Replay(CitySimulator city, Routes routes, Market market, long events)
        { City = city; RoutePlanner = routes; FoodMarket = market; Events = events; }
        public CitySimulator City { get; }
        public CityState State => City.State;
        public Routes RoutePlanner { get; }
        public Market FoodMarket { get; }
        public long Events { get; }
    }

    public static Replay Build(int residents, int seed = Seed, double target = TargetMinute, int eventsPerSlice = 1)
    {
        var state = EconomyScenario.Create(residents, seed); state.Food = EconomyScenario.Food();
        PortalPlaces.Add(state, Portals);
        var routes = new Routes(); var market = new Market();
        var city = new CitySimulator(state, routes); city.AttachFoodMarket(market); city.EnableHouseholdFinances();
        long events = 0;
        while (!city.IsCaughtUpTo(target)) events += city.AdvanceFrameSlice(target, eventsPerSlice).Events;
        city.SynchronizeSnapshot();
        if (state.Food!.Residents.Any(m => !HouseholdFinance.CashConserved(m.Budget!))) throw new InvalidOperationException("Unconserved representative cash.");
        return new Replay(city, routes, market, events);
    }

    public static readonly PortalStation[] Portals =
    {
        new PortalStation("subway:econ-north", new Position(250, 0, 600)), new PortalStation("subway:econ-south", new Position(450, 0, -500)),
    };

    public static string Sha256(string payload) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
}
