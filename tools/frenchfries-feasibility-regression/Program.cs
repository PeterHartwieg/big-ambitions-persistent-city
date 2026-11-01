using System.Diagnostics;
using System.Text.Json;
using BigAmbitions.Items;
using PersistentCity.Core;
using PersistentCity.Runtime;

// Conditional field doubles only. No shipped fryer type, recipe or ingredient pairing is asserted.
static class FrenchFriesProgram
{
    const string Product = FrenchFriesFeasibilityReader.Product;
    const string Fryer = "ba:itemname_industrialfryermachine";
    const string Potato = "ba:itemname_russetpotatoes";
    static int checks;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static BuildingRegistration Fixture(string product = Product, int shelves = 1)
    {
        ItemsGetter.Items = new() {
            [product] = new() { type = ItemType.RetailProduct },
            ["conditional-holder"] = new() { type = ItemType.ShowcaseShelf, itemsThatCanShowcase = new[] { product } },
            ["counter"] = new() { AuthoredTags = { "ba:itemtag_isfullservicecashregister" } },
            ["bag"] = new() { AuthoredTags = { "ba:itemtag_isbag" } }
        };
        Helpers.BusinessTypeHelper.Data = new();
        var r = new BuildingRegistration { cachedAvailableProducts = new() { product },
            retailPrices = new() { new() { itemName = product, price = 10.2528f } } };
        for (int i = 0; i < shelves; i++) r.itemInstances.Add("s" + i, new() { id = "s" + i,
            itemName = "conditional-holder", cargoInstances = new() { new() { itemName = product, amount = 7 } } });
        r.itemInstances.Add("c", new() { id = "c", itemName = "counter",
            cargoInstances = new() { new() { itemName = "bag", amount = 3 } } });
        return r;
    }
    static string Fingerprint(BuildingRegistration r) => JsonSerializer.Serialize(new { r, ItemsGetter.Items,
        Helpers.BusinessTypeHelper.Data }, new JsonSerializerOptions { IncludeFields = true });
    static void Refuse(Action<BuildingRegistration> change, string reason)
    {
        var r = Fixture(); change(r); var before = Fingerprint(r);
        var value = FrenchFriesFeasibilityReader.Read(r);
        Check(!value.DirectStockFieldsVerified && value.Refusal.Contains(reason), reason + ": " + value.Refusal);
        Check(Fingerprint(r) == before, "refused fixture unchanged");
    }
    static void Functional()
    {
        var r = Fixture(); string before = Fingerprint(r);
        var value = FrenchFriesFeasibilityReader.Read(r);
        Check(value.DirectStockFieldsVerified && value.ProductId == Product && value.RawProductUnits == 7
            && value.RawBagUnits == 3 && value.RawCounters == 1 && value.Price == Money.FromNative(10.2528f), "pinned conditional fields");
        Check(value.Eligibility.StartsWith("Unknown"), "raw fields never certify eligibility");
        for (int i = 0; i < 100; i++) FrenchFriesFeasibilityReader.Read(r);
        Check(Fingerprint(r) == before, "100 reads leave complete native fixture unchanged");
        r.itemInstances["s0"].cargoInstances[0].amount = 0;
        r.itemInstances["c"].cargoInstances[0].amount = 0;
        var empty = FrenchFriesFeasibilityReader.Read(r);
        Check(empty.DirectStockFieldsVerified && empty.RawProductUnits == 0 && empty.RawBagUnits == 0, "known shortages stay zero");
        Check(value.RawProductUnits == 7 && value.RawBagUnits == 3 && value.Equipment.Single() == "conditional-holder", "snapshot detached");
        Refuse(x => x.RentedByPlayer = false, "player business");
        Refuse(x => x.temporarilyClosed = true, "player business");
        Refuse(x => Helpers.BusinessTypeHelper.Data.simulator = new object(), "simulator");
        Refuse(x => ItemsGetter.Items[Product].type = ItemType.ServiceProduct, "retail product");
        Refuse(x => ItemsGetter.Items.Clear(), "retail product");
        Refuse(x => x.retailPrices.Clear(), "advertised product/price");
        Refuse(x => x.retailPrices.Add(new() { itemName = Product, price = 1 }), "ambiguous");
        Refuse(x => x.cachedAvailableProducts.Add(Product), "ambiguous");
        Refuse(x => ItemsGetter.Items["conditional-holder"].itemsThatCanShowcase = new[] { Potato }, "whitelist mismatch");
        Refuse(x => x.itemInstances["s0"].itemName = "missing-holder", "definition");
        Refuse(x => ItemsGetter.Items["conditional-holder"].producerSettings.ResourcesRequired = false, "resources");
        Refuse(x => ItemsGetter.Items["conditional-holder"].producerSettings.itemsToProduce = new[] { Product, "other" }, "Shared producer outputs");
        Refuse(x => x.itemInstances["s0"].cargoInstances.Add(new() { itemName = Potato }), "Shared, nested");
        Refuse(x => x.itemInstances["s0"].cargoInstances[0].nestedCargoInstances.Add(new()), "nested");
        Refuse(x => x.itemInstances["c"].cargoInstances.Clear(), "Shared, nested");
        Refuse(x => x.itemInstances["c"].cargoInstances[0].itemName = Potato, "native bag");
        Refuse(x => x.itemInstances.Remove("c"), "No bag counter");
        Refuse(x => x.itemInstances["c"].cargoInstances[0].itemName = Product, "Bag/counter");
        Refuse(x => ItemsGetter.Items["counter"].type = ItemType.ShowcaseShelf, "Bag/counter");
        Refuse(x => x.itemInstances["s0"].id = "c", "identity/sharing");
        foreach (string input in new[] { Potato, "unestablished-ingredient" })
        {
            Refuse(x => x.cachedAvailableProducts[0] = input, "advertised product/price");
            Refuse(x => { ItemsGetter.Items["conditional-holder"].producerSettings.itemsToProduce = new[] { Product };
                x.itemInstances["s0"].cargoInstances[0].itemName = input; }, "Converted input");
        }
        // The inspected native name never authorizes a producer. Its actual shipped type is Unknown.
        r = Fixture(); var holder = ItemsGetter.Items["conditional-holder"];
        ItemsGetter.Items.Remove("conditional-holder"); ItemsGetter.Items[Fryer] = holder;
        r.itemInstances["s0"].itemName = Fryer;
        Check(FrenchFriesFeasibilityReader.Read(r).DirectStockFieldsVerified, "hypothetical named showcase uses fields only");
        holder.type = ItemType.EmployeeWorkstation; holder.producerSettings.itemsToProduce = new[] { Product };
        before = Fingerprint(r);
        Check(FrenchFriesFeasibilityReader.Read(r).Refusal == "Workstation/shared or converted input: unsupported", "same-ID cargo does not authorize fryer workstation");
        r.itemInstances["s0"].cargoInstances[0].itemName = Potato;
        Check(FrenchFriesFeasibilityReader.Read(r).Refusal == "Workstation/shared or converted input: unsupported", "converted fryer workstation refused");
        r.itemInstances["s0"].cargoInstances[0].itemName = Product;
        Check(Fingerprint(r) == before, "named workstation reads do not mutate");
        r = Fixture( Product, 2);
        var shared = FrenchFriesFeasibilityReader.Read(r);
        Check(shared.RawProductUnits == 14 && shared.RawBagUnits == 3 && shared.RawCounters == 1, "shared bag pool not multiplied");
        r.itemInstances["s0"].cargoInstances[0].amount = int.MaxValue;
        Check(FrenchFriesFeasibilityReader.Read(r).Refusal.Contains("overflow"), "overflow refused");
        r = Fixture(); ItemsGetter.Items["storage"] = new();
        r.itemInstances.Add("storage", new() { id = "storage", itemName = "storage", cargoInstances = new() { new() { itemName = Product, amount = 1000 } } });
        Check(FrenchFriesFeasibilityReader.Read(r).RawProductUnits == 7, "storage not direct stock");
        foreach (string product in new[] { DonutFeasibilityReader.Product, CupcakeFeasibilityReader.Product, SaladFeasibilityReader.Product })
        {
            r = Fixture(product); ItemsGetter.Items[Product] = new() { type = ItemType.RetailProduct };
            r.cachedAvailableProducts.Add(Product); r.retailPrices.Add(new() { itemName = Product, price = 12 });
            before = Fingerprint(r);
            for (int i = 0; i < 100; i++)
            {
                Check(FrenchFriesFeasibilityReader.Read(r).Refusal == "No direct stocked candidate showcase shelf observed", "other products never substitute for fries");
                Check(Read(product, r).DirectStockFieldsVerified, "other pinned facade reads its own stock");
            }
            Check(Fingerprint(r) == before, "interleaved candidates leave native fields unchanged");
        }
    }
    static DirectStockFeasibilitySnapshot Read(string product, BuildingRegistration r) => product switch {
        DonutFeasibilityReader.Product => DonutFeasibilityReader.Read(r),
        CupcakeFeasibilityReader.Product => CupcakeFeasibilityReader.Read(r),
        SaladFeasibilityReader.Product => SaladFeasibilityReader.Read(r),
        Product => FrenchFriesFeasibilityReader.Read(r),
        _ => throw new ArgumentException("Pinned benchmark candidate required")
    };
    static void Main(string[] args)
    {
        string product = args.Length == 0 ? Product : args[0];
        var r = Fixture(product); var clock = Stopwatch.StartNew(); clock.Stop();
        long allocation = GC.GetAllocatedBytesForCurrentThread(); clock.Restart();
        var cold = Read(product, r); clock.Stop();
        long coldBytes = GC.GetAllocatedBytesForCurrentThread() - allocation;
        double coldMs = clock.Elapsed.TotalMilliseconds;
        Check(cold.DirectStockFieldsVerified, "cold readable");
        if (args.Length == 0) Functional();
        var timings = new List<object>();
        foreach (int size in new[] { 1, 1999 })
        {
            r = Fixture(product, size); for (int i = 0; i < 30; i++) Read(product, r);
            var samples = new double[1001]; long bytes = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                allocation = GC.GetAllocatedBytesForCurrentThread(); long start = Stopwatch.GetTimestamp();
                var value = Read(product, r);
                samples[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                bytes += GC.GetAllocatedBytesForCurrentThread() - allocation;
                Check(value.DirectStockFieldsVerified, "timed fixture readable");
            }
            Array.Sort(samples);
            timings.Add(new { furniture = size + 1, p50Ms = samples[500], p95Ms = samples[950], maxMs = samples[^1],
                bytesPerCall = bytes / samples.Length, spikesAtLeast30Ms = samples.Count(s => s >= 30) });
        }
        Console.WriteLine(JsonSerializer.Serialize(new { product, checks, cold = new { samples = 1, coldMs, coldBytes }, warm = timings }));
    }
}
