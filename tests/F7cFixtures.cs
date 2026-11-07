using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;

namespace PersistentCity.Tests;

/// <summary>
/// F7c golden fixtures, all fresh cities in the current schema (no migration). The F7a representative replay
/// (RepresentativeFixture, one natural day and midnight, factor 2) at its three sizes; the E5d size matrix at factor 3
/// (the factor-2 sizes are the representative ones); two saturated variants of the 957-resident city (64 recent visits
/// per resident, E5d's per-resident bound); an escaping variant (30 scopes of eight 256-character non-ASCII product
/// names, E5d's encoding-max shape inside the 256-name table); and morning-rush cities saved at 08:20 on the second
/// day, when many residents and visitors are mid-journey (the representative save at 00:30 has none). Uses only API that exists before and after F7c, so the same source builds the
/// before pins on the base commit.
/// </summary>
internal static class F7cFixtures
{
    public static readonly string[] All =
    {
        "representative-957x2", "fresh-957x3", "saturated-957x2", "saturated-957x3", "encoding-max", "rush-957x2",
        "representative-5000x2", "fresh-5000x3", "rush-5000x2", "representative-50000x2", "fresh-50000x3",
    };

    /// <summary>The fixtures small enough for the unit suite.</summary>
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

    /// <summary>08:20 the morning after the representative save.</summary>
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

    /// <summary>Fill every resident's recent visits to the 64-visit bound with ordered past home visits.</summary>
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

    /// <summary>30 shop scopes, each naming eight 256-character non-ASCII products, so characters and UTF-8 bytes diverge.</summary>
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
