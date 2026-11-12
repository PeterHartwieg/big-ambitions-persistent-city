using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;

namespace PersistentCity.Tests;

internal static class F7cFixtures
{
    public static readonly string[] All =
    {
        "representative-957x2", "fresh-957x3", "saturated-957x2", "saturated-957x3", "encoding-max", "rush-957x2",
        "representative-5000x2", "fresh-5000x3", "rush-5000x2", "representative-50000x2", "fresh-50000x3",
    };

    public static readonly string[] Unit = { "representative-957x2", "fresh-957x3", "saturated-957x3", "encoding-max", "rush-957x2" };

    public sealed class Built
    {
        public Built(CitySimulator city, RepresentativeFixture.Routes routes) { City = city; Routes = routes; }
        public CitySimulator City { get; }
        public CityState State => City.State;
        public RepresentativeFixture.Routes Routes { get; }
    }

    public static Built Build(string name)
    {
        string size = name.Substring(name.IndexOf('-') + 1);
        int residents = name == "encoding-max" ? 957 : int.Parse(size.Split('x')[0]);
        int factor = name == "encoding-max" ? 2 : int.Parse(size.Split('x')[1]);
        Built built;
        if (name.StartsWith("representative-", StringComparison.Ordinal))
        {
            var replay = RepresentativeFixture.Build(residents);
            built = new Built(replay.City, replay.RoutePlanner);
        }
        else built = Fresh(residents, factor, name.StartsWith("rush-", StringComparison.Ordinal) ? RushMinute : RepresentativeFixture.TargetMinute);
        if (name.StartsWith("saturated-", StringComparison.Ordinal)) SaturateVisits(built.State);
        if (name == "encoding-max") EscapingNames(built.State);
        return built;
    }

    public const double RushMinute = RepresentativeFixture.TargetMinute + 7 * 60 + 50;

    private static Built Fresh(int residents, int factor, double target)
    {
        var state = EconomyScenario.Create(residents, RepresentativeFixture.Seed); state.Food = EconomyScenario.Food();
        state.Food.RepresentationFactor = factor;
        PortalPlaces.Add(state, RepresentativeFixture.Portals);
        var routes = new RepresentativeFixture.Routes();
        var city = new CitySimulator(state, routes); city.AttachFoodMarket(new RepresentativeFixture.Market()); city.EnableHouseholdFinances();
        while (!city.IsCaughtUpTo(target)) city.AdvanceFrameSlice(target, 64);
        city.SynchronizeSnapshot();
        return new Built(city, routes);
    }

    private static void SaturateVisits(CityState state)
    {
        foreach (var citizen in state.Citizens)
        {
            var visits = new List<VisitRecord>(64);
            double first = Math.Max(0, state.Minute - 64 * 10);
            for (int i = 0; i < 64; i++) visits.Add(new VisitRecord { AtMinute = first + i * 10 + 0.25, PlaceId = citizen.HomeId, Activity = Activity.Home });
            citizen.RecentVisits = visits;
        }
    }

    private static void EscapingNames(CityState state)
    {
        string Name(int n) => new string('é', 255) + (char)('Ā' + n);
        var shops = state.Places.Where(p => p.Kind == PlaceKind.Shop).ToList();
        for (int i = shops.Count; i < 30; i++)
        {
            var place = new Place { Id = "f7c:escape:" + i.ToString("D2"), Name = "Shop", District = "a", Kind = PlaceKind.Shop, Position = new Position(10 * i, 0, 40) };
            state.Places.Add(place); shops.Add(place);
        }
        state.Food!.Shops.Clear();
        for (int i = 0; i < 30; i++)
            state.Food.Shops.Add(new FoodScope { PlaceId = shops[i].Id, BusinessId = "escape:" + i, ProductId = Name(i * 8),
                AdditionalProductIds = Enumerable.Range(i * 8 + 1, 7).Select(Name).ToList() });
    }
}
