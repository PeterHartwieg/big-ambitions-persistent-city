using System;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;

// The E5d saturated matrix (tools/name-identity-regression), rebuilt with that tool's own private
// helpers through this partial class and migrated 24 -> 25 exactly as E5d does. Hash equality
// with the retained E5d afterSha256 is checked by the harness; nothing here edits E5d's tool.
static partial class Program
{
    public static readonly string[] StressNames = { "957x2", "957x3", "5000x2", "5000x3", "50000x2", "50000x3", "churn", "CF32", "CF64", "encoding-max" };

    public static CityState StressFixture(string name)
    {
        if (!StressNames.Contains(name)) throw new ArgumentException("Unknown E5d stress fixture " + name);
        if (name == "encoding-max") return MaximumEscapingFixture();
        bool cf = name == "CF32" || name == "CF64";
        int residents = cf || name.StartsWith("50000", StringComparison.Ordinal) ? 50000 : name.StartsWith("5000", StringComparison.Ordinal) ? 5000 : 957;
        int factor = cf || name.EndsWith("x3", StringComparison.Ordinal) ? 3 : 2;
        var state = FoodCity(residents, true, factor); PopulateDecisions(state);
        RetainVisits(state, name.StartsWith("CF64", StringComparison.Ordinal) ? 64 : 32);
        if (cf) AddCfPlaces25(state);
        FullMemoryAndTable(state); PopulateWeather20(state);
        var products = new[] { FoodProducts.Croissant, FoodProducts.Burger, FoodProducts.HotDog,
            "ba:itemname_donut", "ba:itemname_cupcake", "ba:itemname_salad",
            "foodtrucks:itemname_dumplings", "foodtrucks:itemname_choppedcheese" };
        var places = state.Places.Where(p => p.Kind == PlaceKind.Shop).ToList();
        for (int i = places.Count; i < 1024; i++)
        {
            var place = new Place { Id = "name25:shop:" + i, Name = "Historical shop", Kind = PlaceKind.Shop };
            state.Places.Add(place); places.Add(place);
        }
        int generationsPerDay = name == "churn" ? 2048 : 1024;
        for (int day = 0; day < 14; day++) for (int i = 0; i < generationsPerDay; i++)
        {
            var row = new FoodShopDay { Day = day, PlaceId = places[i % 1024].Id, BusinessId = "name25:history:" + i,
                Purchases = 8, Revenue = Money.FromCents(8), PurchaseMeals = new long[] { 8,0,0,0 },
                BuyerDistricts = { new FoodBuyerDistrict { DistrictId = "a", Purchases = 8 } } };
            row.PurchaseHours[0] = 8;
            foreach (string product in products) row.ProductSales.Add(new FoodProductSales { ProductId = product,
                Purchases = 1, Revenue = Money.FromCents(1), PurchaseMeals = new long[] { 1,0,0,0 } });
            state.Food!.History.Add(row);
        }
        Shift25(state, 12 * 1440);
        string[] names = products.Concat(Enumerable.Range(8, 248).Select(i => "name25:product:" + i.ToString("D3"))).ToArray();
        int originalScopeChars = ScopeCharacters25(state.Food!.Shops);
        state.Food!.Shops.Clear();
        for (int i = 0; i < (cf ? 1024 : 32); i++) state.Food.Shops.Add(new FoodScope { PlaceId = places[i].Id,
            BusinessId = "name25:scope:" + i, ProductId = names[(i % 32) * 8], AdditionalProductIds = names.Skip((i % 32) * 8 + 1).Take(7).ToList() });
        int scopeGrowthChars = ScopeCharacters25(state.Food.Shops) - originalScopeChars;
        state.Schema = 24; state.MenuPolicy = FoodMenuPolicy.Wire;
        var historySize = HistorySize25(state);
        if (cf)
        {
            var own = state.Citizens.Where(c => c.Identity.Id.StartsWith("pc:", StringComparison.Ordinal)).ToArray();
            int remaining = scopeGrowthChars + historySize.Rows;
            for (int i = 0; i < own.Length; i++)
            {
                int take = remaining / (own.Length - i); remaining -= take;
                string value = own[i].Identity.Name;
                own[i].Identity.Name = value.Substring(0, value.Length - take);
            }
        }
        // The fixture was a schema-24 city migrated to 25. SaveCodec.Read refuses schema 24 since F5c-lite, and X1
        // removed the frozen schema-24 writer, so only the encoding-max fixture still builds on the current tree.
        throw new NotSupportedException("E5d stress fixture " + name + " needs the schema-24 writer X1 removed; run it against an archived source tree.");
    }

    /// <summary>
    /// E5d's maximum-escaping case (NameEncoding25(true)): 32 scopes x 8 product names of 256
    /// non-ASCII characters, so characters and UTF-8 bytes diverge. Same construction as E5d.
    /// </summary>
    static CityState MaximumEscapingFixture()
    {
        var state = FoodCity(1, false); state.Food!.Shops.Clear();
        foreach (var m in state.Food.Residents) { m.PreferredProductId = ""; m.PreferenceBonusCents = 0; m.Today = null; }
        for (int i = 0; i < 32; i++)
        {
            string place = "name25:" + i; state.Places.Add(new Place { Id = place, Name = "Shop", Kind = PlaceKind.Shop });
            string Name(int n) => new string('', 255) + (char)('' + n);
            state.Food.Shops.Add(new FoodScope { PlaceId = place, BusinessId = "encoding:" + i, ProductId = Name(i * 8),
                AdditionalProductIds = Enumerable.Range(i * 8 + 1, 7).Select(Name).ToList() });
        }
        return state;
    }
}
