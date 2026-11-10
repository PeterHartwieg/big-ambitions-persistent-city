using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;

namespace PersistentCity.Tests;

/// <summary>
/// F7d: fresh current-schema versions of the E5d saturated stress set (plan section 3.1 stress fixtures), built without
/// migration on F7c's fresh 50,000-resident cities. They apply E5d's saturation shapes: 32 recent visits per resident
/// (64 for CF64), 14 × 1,024 market-history rows of eight product cells each, scopes of eight product names drawn from
/// the full 256-name table (32 scopes; 1,024 for CF), and for CF32/CF64 the place table filled to the 20,000-place cap.
/// The history rows sit on the fresh city's own days, so their businesses are distinct per row instead of per day; the
/// row count and cell shape are E5d's. Offline measurement only.
/// </summary>
internal static class F7dStressFixtures
{
    public static readonly string[] All = { "saturated-50000x2", "saturated-50000x3", "CF32", "CF64" };

    public static bool Is(string name) => Array.IndexOf(All, name) >= 0;

    public static F7cFixtures.Built Build(string name)
    {
        bool cf = name.StartsWith("CF", StringComparison.Ordinal);
        var built = F7cFixtures.Build(name == "saturated-50000x2" ? "representative-50000x2" : "fresh-50000x3");
        var state = built.State;
        Visits(state, name == "CF64" ? 64 : 32);
        var shops = ShopPlaces(state, cf ? 1024 : 32);
        if (cf) FillPlaces(state);
        // The city's own names first (prepared by a capture), then synthetic ones, so the table is exactly full.
        var own = Persistence.SaveCodec.Capture(state).Wire.ProductNames;
        string[] names = own.Concat(Enumerable.Range(0, FoodNameIdentity.MaximumNames).Select(i => "f7d:product:" + i.ToString("D3")))
            .Distinct(StringComparer.Ordinal).Take(FoodNameIdentity.MaximumNames).ToArray();
        state.Food!.Shops.Clear();
        for (int i = 0; i < (cf ? 1024 : 32); i++)
            state.Food.Shops.Add(new FoodScope { PlaceId = shops[i].Id, BusinessId = "f7d:scope:" + i, ProductId = names[(i % 32) * 8],
                AdditionalProductIds = names.Skip((i % 32) * 8 + 1).Take(7).ToList() });
        History(state, shops, names);
        return built;
    }

    private static void Visits(CityState state, int count)
    {
        foreach (var citizen in state.Citizens)
        {
            var visits = new List<VisitRecord>(count);
            double first = Math.Max(0, state.Minute - count * 10);
            for (int i = 0; i < count; i++) visits.Add(new VisitRecord { AtMinute = first + i * 10 + 0.25, PlaceId = citizen.HomeId, Activity = Activity.Home });
            citizen.RecentVisits = visits;
        }
    }

    private static List<Place> ShopPlaces(CityState state, int count)
    {
        var shops = state.Places.Where(p => p.Kind == PlaceKind.Shop).ToList();
        for (int i = shops.Count; i < count; i++)
        {
            var place = new Place { Id = "f7d:shop:" + i.ToString("D4"), Name = "Historical shop", District = "a", Kind = PlaceKind.Shop, Position = new Position(10 * i, 0, 80) };
            state.Places.Add(place); shops.Add(place);
        }
        return shops;
    }

    private static void FillPlaces(CityState state)
    {
        for (int i = state.Places.Count; i < Persistence.SaveCodec.MaxPlaces; i++)
            state.Places.Add(new Place { Id = "f7d:place:" + i.ToString("D5"), Name = "Filler shop", District = "a", Kind = PlaceKind.Shop, Position = new Position(i % 1000, 0, 120 + i / 1000) });
    }

    private static void History(CityState state, List<Place> shops, string[] names)
    {
        int today = (int)(state.Minute / 1440);
        state.Food!.History.Clear();
        for (int row = 0; row < 14 * 1024; row++)
        {
            var day = new FoodShopDay { Day = row % (today + 1), PlaceId = shops[row % shops.Count].Id, BusinessId = "f7d:history:" + row.ToString("D5"),
                Purchases = 8, Revenue = Money.FromCents(8), PurchaseMeals = new long[] { 8, 0, 0, 0 },
                BuyerDistricts = { new FoodBuyerDistrict { DistrictId = "a", Purchases = 8 } } };
            day.PurchaseHours[0] = 8;
            for (int p = 0; p < 8; p++)
                day.ProductSales.Add(new FoodProductSales { ProductId = names[(row % 32) * 8 + p], Purchases = 1, Revenue = Money.FromCents(1), PurchaseMeals = new long[] { 1, 0, 0, 0 } });
            state.Food.History.Add(day);
        }
    }
}
