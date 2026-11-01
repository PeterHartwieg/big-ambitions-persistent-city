using System.Diagnostics;
using System.Text.Json;
using BigAmbitions.Items;
using PersistentCity.Core;
using PersistentCity.Runtime;

// Conditional field fixtures, never assertions about the shipped oven or a pizza recipe.
static class PizzaRegression
{
    const string Product = PizzaFeasibilityReader.Product;
    static int checks;
    static void Check(bool ok, string why) { if (!ok) throw new Exception(why); checks++; }
    static BuildingRegistration Fixture(int shelves = 1)
    {
        ItemsGetter.Items = new() {
            [Product] = new() { type = ItemType.RetailProduct },
            ["fixture-holder"] = new() { type = ItemType.ShowcaseShelf, itemsThatCanShowcase = new[] { Product } },
            ["fixture-counter"] = new() { AuthoredTags = { "ba:itemtag_isfullservicecashregister" } },
            ["fixture-bag"] = new() { AuthoredTags = { "ba:itemtag_isbag" } }
        };
        Helpers.BusinessTypeHelper.Data = new();
        var r = new BuildingRegistration {
            cachedAvailableProducts = new() { Product },
            retailPrices = new() { new() { itemName = Product, price = 10.2528f } }
        };
        for (int i = 0; i < shelves; i++) r.itemInstances.Add("s" + i, new() {
            id = "s" + i, itemName = "fixture-holder", cargoInstances = new() { new() { itemName = Product, amount = 7 } } });
        r.itemInstances.Add("counter", new() { id = "counter", itemName = "fixture-counter",
            cargoInstances = new() { new() { itemName = "fixture-bag", amount = 3 } } });
        return r;
    }
    static string Fingerprint(BuildingRegistration r) => JsonSerializer.Serialize(new { r, ItemsGetter.Items,
        Helpers.BusinessTypeHelper.Data }, new JsonSerializerOptions { IncludeFields = true,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals });
    static void Refusal(Action<BuildingRegistration> change, string expected)
    {
        var r = Fixture(); change(r); string before = Fingerprint(r);
        var result = PizzaFeasibilityReader.Read(r);
        Check(!result.DirectStockFieldsVerified && result.Refusal.Contains(expected), expected + ": " + result.Refusal);
        Check(before == Fingerprint(r), "refusal must not mutate native fields");
    }
    static void RenameHolder(BuildingRegistration r, string name, ItemType type)
    {
        ItemsGetter.Items[name] = ItemsGetter.Items["fixture-holder"];
        ItemsGetter.Items[name].type = type;
        r.itemInstances["s0"].itemName = name;
    }
    static void Main()
    {
        var r = Fixture(); var clock = Stopwatch.StartNew(); clock.Stop();
        long allocation = GC.GetAllocatedBytesForCurrentThread(); clock.Restart();
        var cold = PizzaFeasibilityReader.Read(r); clock.Stop();
        long coldBytes = GC.GetAllocatedBytesForCurrentThread() - allocation;
        double coldMs = clock.Elapsed.TotalMilliseconds;
        Check(Product == "ba:itemname_pizza" && cold.ProductId == Product, "pinned exact pizza identity");
        Check(cold.DirectStockFieldsVerified && cold.RawProductUnits == 7 && cold.RawBagUnits == 3 && cold.RawCounters == 1,
            "conditional finished-stock fields and shared bag pool");
        Check(cold.Price == Money.FromNative(10.2528f) && cold.Eligibility.StartsWith("Unknown"), "exact money; eligibility unknown");
        string before = Fingerprint(r);
        for (int i = 0; i < 100; i++) PizzaFeasibilityReader.Read(r);
        Check(before == Fingerprint(r), "100 repeated reads preserve all fixture fields");
        r.itemInstances["s0"].cargoInstances[0].amount = 0;
        r.itemInstances["counter"].cargoInstances[0].amount = 0;
        var empty = PizzaFeasibilityReader.Read(r);
        Check(empty.DirectStockFieldsVerified && empty.RawProductUnits == 0 && empty.RawBagUnits == 0, "known raw zeros");
        Check(cold.RawProductUnits == 7 && cold.RawBagUnits == 3, "snapshot detached from later stock changes");
        Check(cold.Equipment is not string[], "equipment is read-only");
        Refusal(x => x.RentedByPlayer = false, "player business");
        Refusal(x => x.temporarilyClosed = true, "player business");
        Refusal(x => x.HasEstablishedBusiness = false, "player business");
        Refusal(x => Helpers.BusinessTypeHelper.Data.simulator = new object(), "simulator");
        Refusal(x => Helpers.BusinessTypeHelper.Data.hasEntranceFee = true, "entrance fee");
        Refusal(x => Helpers.BusinessTypeHelper.Data.AuthoredTags.Clear(), "bag contract");
        Refusal(x => ItemsGetter.Items[Product].type = ItemType.ServiceProduct, "retail product");
        Refusal(x => x.cachedAvailableProducts.Clear(), "advertised");
        Refusal(x => x.cachedAvailableProducts.Add(Product), "ambiguous");
        Refusal(x => x.retailPrices.Clear(), "advertised product/price");
        Refusal(x => x.retailPrices.Add(new() { itemName = Product, price = 1 }), "ambiguous");
        foreach (float price in new[] { 0, -1, float.NaN, float.PositiveInfinity, float.Epsilon, 10001 })
            Refusal(x => x.retailPrices[0].price = price, "price");
        foreach (string input in new[] { "ba:itemname_rawpizza", "ba:itemname_dough", "ba:itemname_laminateddough",
            "ba:itemname_slicedcheese", "ba:itemname_cheese", "ba:itemname_tomatopaste", "ba:itemname_tomato",
            "ba:itemname_bakingmix", "ba:itemname_yeast" })
        {
            Refusal(x => x.cachedAvailableProducts[0] = input, "advertised");
            Refusal(x => { ItemsGetter.Items["fixture-holder"].producerSettings.itemsToProduce = new[] { Product };
                x.itemInstances["s0"].cargoInstances[0].itemName = input; }, "Converted input");
        }
        Refusal(x => ItemsGetter.Items["fixture-holder"].itemsThatCanShowcase = new[] { "ba:itemname_rawpizza" }, "whitelist mismatch");
        Refusal(x => ItemsGetter.Items["fixture-holder"].producerSettings.ResourcesRequired = false, "resources");
        Refusal(x => ItemsGetter.Items["fixture-holder"].producerSettings.itemsToProduce = new[] { Product, "other" }, "Shared producer outputs");
        Refusal(x => x.itemInstances["s0"].cargoInstances.Add(new() { itemName = "ba:itemname_rawpizza" }), "Shared, nested");
        Refusal(x => x.itemInstances["s0"].cargoInstances[0].nestedCargoInstances.Add(new()), "nested");
        Refusal(x => x.itemInstances["s0"].cargoInstances[0].amount = -1, "malformed");
        Refusal(x => x.itemInstances["counter"].cargoInstances.Clear(), "Shared, nested");
        Refusal(x => x.itemInstances["counter"].cargoInstances[0].itemName = "not-bag", "native bag");
        Refusal(x => x.itemInstances["counter"].cargoInstances[0].itemName = Product, "Bag/counter");
        Refusal(x => x.itemInstances.Remove("counter"), "No bag counter");
        Refusal(x => x.itemInstances["s0"].id = "counter", "identity/sharing");
        Refusal(x => x.itemInstances = new(x.itemInstances, StringComparer.OrdinalIgnoreCase), "key comparer");
        Refusal(x => x.itemInstances["s0"].itemName = "missing", "definition");
        Refusal(x => { for (int i = 0; i < 129; i++) x.cachedAvailableProducts.Add("other"); }, "oversized");
        // The oven name grants no authorization. Hypothetical workstation fields always refuse,
        // including finished pizza cargo and an unrelated output consuming pizza as input.
        foreach (string output in new[] { Product, "other" })
            Refusal(x => { RenameHolder(x, "ba:itemname_pizzaoven", ItemType.EmployeeWorkstation);
                ItemsGetter.Items["ba:itemname_pizzaoven"].producerSettings.itemsToProduce = new[] { output }; }, "Workstation/shared");
        Refusal(x => { RenameHolder(x, "ba:itemname_pizzaoven", ItemType.EmployeeWorkstation);
            ItemsGetter.Items["ba:itemname_pizzaoven"].producerSettings.itemsToProduce = new[] { Product };
            x.itemInstances["s0"].cargoInstances[0].itemName = "ba:itemname_rawpizza"; }, "Workstation/shared");
        r = Fixture(); RenameHolder(r, "ba:itemname_pizzaoven", ItemType.ShowcaseShelf);
        Check(PizzaFeasibilityReader.Read(r).DirectStockFieldsVerified, "conditional type/fields, never equipment name, authorize the probe");
        r = Fixture(2);
        var two = PizzaFeasibilityReader.Read(r);
        Check(two.RawProductUnits == 14 && two.RawBagUnits == 3 && two.RawCounters == 1, "two holders share one bag pool");
        r.itemInstances["s0"].cargoInstances[0].amount = int.MaxValue;
        Check(PizzaFeasibilityReader.Read(r).Refusal.Contains("overflow"), "product overflow refused");
        r = Fixture(); ItemsGetter.Items["fixture-storage"] = new();
        r.itemInstances.Add("storage", new() { id = "storage", itemName = "fixture-storage",
            cargoInstances = new() { new() { itemName = Product, amount = 1000 } } });
        Check(PizzaFeasibilityReader.Read(r).RawProductUnits == 7, "storage excluded");
        foreach (var other in new (string Product, Func<BuildingRegistration, DirectStockFeasibilitySnapshot> Read)[] {
            (DonutFeasibilityReader.Product, DonutFeasibilityReader.Read),
            (CupcakeFeasibilityReader.Product, CupcakeFeasibilityReader.Read),
            (SaladFeasibilityReader.Product, SaladFeasibilityReader.Read) })
        {
            r = Fixture(); ItemsGetter.Items[other.Product] = new() { type = ItemType.RetailProduct };
            r.cachedAvailableProducts.Add(other.Product); r.retailPrices.Add(new() { itemName = other.Product, price = 12 });
            r.itemInstances["s0"].cargoInstances[0].itemName = other.Product;
            ItemsGetter.Items["fixture-holder"].itemsThatCanShowcase = new[] { other.Product };
            Check(PizzaFeasibilityReader.Read(r).Refusal == "No direct stocked candidate showcase shelf observed", "no candidate substitution");
            var expected = JsonSerializer.Serialize(other.Read(r));
            before = Fingerprint(r);
            for (int i = 0; i < 100; i++) { PizzaFeasibilityReader.Read(r); Check(JsonSerializer.Serialize(other.Read(r)) == expected, "unchanged other candidate snapshot"); }
            Check(before == Fingerprint(r), "interleaved candidates preserve all fields");
        }
        var timings = new List<object>();
        foreach (int size in new[] { 1, 1999 })
        {
            r = Fixture(size); for (int i = 0; i < 30; i++) PizzaFeasibilityReader.Read(r);
            var samples = new double[1001]; long bytes = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                allocation = GC.GetAllocatedBytesForCurrentThread(); long start = Stopwatch.GetTimestamp();
                var value = PizzaFeasibilityReader.Read(r);
                samples[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                bytes += GC.GetAllocatedBytesForCurrentThread() - allocation;
                Check(value.DirectStockFieldsVerified, "benchmark fixture readable");
            }
            Array.Sort(samples);
            timings.Add(new { furniture = size + 1, p50Ms = samples[500], p95Ms = samples[950], maxMs = samples[^1],
                bytesPerCall = bytes / samples.Length, spikesAtLeast30Ms = samples.Count(s => s >= 30) });
        }
        Console.WriteLine(JsonSerializer.Serialize(new { checks, cold = new { samples = 1, coldMs, coldBytes }, warm = timings }));
    }
}
