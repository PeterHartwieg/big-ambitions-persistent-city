#if CUPCAKE
using CandidateReader = PersistentCity.Runtime.CupcakeFeasibilityReader;
#else
using CandidateReader = PersistentCity.Runtime.DonutFeasibilityReader;
#endif
using System.Diagnostics;
using System.Text.Json;
using BigAmbitions.Items;
using PersistentCity.Core;
using PersistentCity.Runtime;

// Field shapes and branch semantics inspected in the Steam assemblies, not copied game code.
// Fixture asset values are conditional examples, NOT proof of shipped donut equipment.
static class Program
{
    const string Product = CandidateReader.Product;
    static int checks;
    static void Check(bool ok, string why) { if (!ok) throw new Exception(why); checks++; }
    static BuildingRegistration Fixture(int shelves = 1)
    {
        ItemsGetter.Items = new() {
            [Product] = new() { type = ItemType.RetailProduct },
            ["fixture-shelf"] = new() { type = ItemType.ShowcaseShelf, itemsThatCanShowcase = new[] { Product } },
            ["fixture-counter"] = new() { AuthoredTags = { "ba:itemtag_isfullservicecashregister" } },
            ["fixture-bag"] = new() { AuthoredTags = { "ba:itemtag_isbag" } }
        };
        Helpers.BusinessTypeHelper.Data = new();
        var r = new BuildingRegistration();
        for (int i = 0; i < shelves; i++) r.itemInstances.Add("s" + i, new() {
            id = "s" + i, itemName = "fixture-shelf", cargoInstances = new() { new() { itemName = Product, amount = 7 } } });
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
        var result = CandidateReader.Read(r);
        Check(!result.DirectStockFieldsVerified && result.Refusal.Contains(expected), "specific refusal: " + expected + ": " + result.Refusal);
        Check(Fingerprint(r) == before, "refusal leaves native double unchanged");
    }
    static void Main()
    {
        // First explicit call includes this process's first reader JIT. Fixture/JIT of measuring APIs excluded.
        var r = Fixture(); var clock = Stopwatch.StartNew(); clock.Stop();
        long allocation = GC.GetAllocatedBytesForCurrentThread(); clock.Restart();
        var cold = CandidateReader.Read(r); clock.Stop();
        long coldBytes = GC.GetAllocatedBytesForCurrentThread() - allocation;
        double coldMs = clock.Elapsed.TotalMilliseconds;
        Check(cold.DirectStockFieldsVerified && cold.RawProductUnits == 7 && cold.RawBagUnits == 3 && cold.RawCounters == 1,
            "direct raw stock and shared bag fields");
        Check(cold.Price == Money.FromNative(10.2528f), "exact binary32 price");
        string before = Fingerprint(r);
        for (int i = 0; i < 100; i++) CandidateReader.Read(r);
        Check(before == Fingerprint(r), "100 reads preserve complete fixture fields");
        r.itemInstances["s0"].cargoInstances[0].amount = 0;
        Check(CandidateReader.Read(r).RawProductUnits == 0, "known empty direct stock");
        r.itemInstances["counter"].cargoInstances[0].amount = 0;
        Check(CandidateReader.Read(r).RawBagUnits == 0, "known bag shortage");
        Check(cold.RawProductUnits == 7 && cold.Equipment[0] == "fixture-shelf", "old image detached after native mutation");
        Check(cold.Eligibility.StartsWith("Unknown"), "raw counters never assert staffing/capacity");
        Refusal(x => x.RentedByPlayer = false, "player business");
        Refusal(x => x.temporarilyClosed = true, "player business");
        Refusal(x => x.AvailableForRent = true, "player business");
        Refusal(x => Helpers.BusinessTypeHelper.Data.simulator = new object(), "simulator");
        Refusal(x => Helpers.BusinessTypeHelper.Data.hasEntranceFee = true, "entrance fee");
        Refusal(x => Helpers.BusinessTypeHelper.Data.AuthoredTags.Clear(), "bag contract");
        Refusal(x => ItemsGetter.Items[Product].type = ItemType.ServiceProduct, "retail product");
        Refusal(x => x.cachedAvailableProducts.Clear(), "advertised");
        Refusal(x => x.cachedAvailableProducts.Add(Product), "ambiguous");
        Refusal(x => x.retailPrices.Add(new() { itemName = Product, price = 1 }), "ambiguous");
        foreach (float price in new[] { float.NaN, float.PositiveInfinity, -1, 10001, float.Epsilon, 0 })
            Refusal(x => x.retailPrices[0].price = price, "price");
        Refusal(x => ItemsGetter.Items["fixture-shelf"].itemsThatCanShowcase = new[] { "other" }, "whitelist mismatch");
        Refusal(x => ItemsGetter.Items["fixture-shelf"].producerSettings.ResourcesRequired = false, "resources");
        Refusal(x => { ItemsGetter.Items["fixture-shelf"].producerSettings.itemsToProduce = new[] { Product };
            x.itemInstances["s0"].cargoInstances[0].itemName = "converted-input"; }, "Converted input");
        Refusal(x => { ItemsGetter.Items["fixture-shelf"].type = ItemType.EmployeeWorkstation;
            ItemsGetter.Items["fixture-shelf"].producerSettings.itemsToProduce = new[] { Product }; }, "Workstation/shared");
        Refusal(x => { ItemsGetter.Items["fixture-shelf"].type = ItemType.EmployeeWorkstation;
            ItemsGetter.Items["fixture-shelf"].producerSettings.itemsToProduce = new[] { "other-output" }; }, "Workstation/shared");
        Refusal(x => ItemsGetter.Items["fixture-shelf"].producerSettings.itemsToProduce = new[] { Product, "other" }, "Shared producer outputs");
        Refusal(x => x.itemInstances["s0"].cargoInstances.Add(new() { itemName = Product }), "Shared, nested");
        Refusal(x => x.itemInstances["s0"].cargoInstances[0].nestedCargoInstances.Add(new()), "nested");
        Refusal(x => x.itemInstances["s0"].cargoInstances[0].amount = -1, "malformed");
        Refusal(x => x.itemInstances["counter"].cargoInstances[0].itemName = Product, "Bag/counter");
        Refusal(x => ItemsGetter.Items["fixture-counter"].type = ItemType.ShowcaseShelf, "Bag/counter");
        Refusal(x => x.itemInstances["counter"].cargoInstances[0].itemName = "other", "native bag");
        Refusal(x => x.itemInstances.Remove("counter"), "No bag counter");
        Refusal(x => x.itemInstances["s0"].id = "counter", "identity/sharing");
        Refusal(x => x.itemInstances = new(x.itemInstances, StringComparer.OrdinalIgnoreCase), "key comparer");
        Refusal(x => x.itemInstances["s0"].itemName = "missing", "definition");
        Refusal(x => { for (int i = 0; i < 129; i++) ItemsGetter.Items["fixture-shelf"].AuthoredTags.Add("other"); }, "authoring tags");
        Refusal(x => ItemsGetter.Items.Clear(), "retail product");
        Refusal(x => { for (int i = 0; i < 129; i++) x.cachedAvailableProducts.Add("other"); }, "oversized");
        r = Fixture(2);
        var shared = CandidateReader.Read(r);
        Check(shared.RawProductUnits == 14 && shared.RawBagUnits == 3 && shared.RawCounters == 1,
            "two producers do not multiply the business bag/counter pool");
        r.itemInstances["s0"].cargoInstances[0].amount = int.MaxValue;
        Check(CandidateReader.Read(r).Refusal.Contains("overflow"), "overflow refused");
        r = Fixture(); ItemsGetter.Items["fixture-storage"] = new();
        r.itemInstances.Add("storage", new() { id = "storage", itemName = "fixture-storage",
            cargoInstances = new() { new() { itemName = Product, amount = 1000 } } });
        Check(CandidateReader.Read(r).RawProductUnits == 7, "storage excluded from native direct-stock evidence");
#if CUPCAKE
        Check(CandidateReader.Product == "ba:itemname_cupcake" && cold.ProductId == CandidateReader.Product,
            "pinned cupcake identity on detached output");
        Refusal(x => x.retailPrices.Clear(), "advertised product/price");
        Refusal(x => x.cachedAvailableProducts[0] = "ba:itemname_plaincupcake", "advertised product/price");
        foreach (string input in new[] { "ba:itemname_plaincupcake", "ba:itemname_frosting" })
            Refusal(x => { ItemsGetter.Items["fixture-shelf"].producerSettings.itemsToProduce = new[] { Product };
                x.itemInstances["s0"].cargoInstances[0].itemName = input; }, "Converted input");
        Refusal(x => ItemsGetter.Items["fixture-shelf"].itemsThatCanShowcase = new[] { "ba:itemname_donut" }, "whitelist mismatch");
        Refusal(x => x.itemInstances["counter"].cargoInstances.Clear(), "Shared, nested");
        r = Fixture();
        ItemsGetter.Items[DonutFeasibilityReader.Product] = new() { type = ItemType.RetailProduct };
        r.cachedAvailableProducts.Add(DonutFeasibilityReader.Product);
        r.retailPrices.Add(new() { itemName = DonutFeasibilityReader.Product, price = 12 });
        r.itemInstances["s0"].cargoInstances[0].itemName = DonutFeasibilityReader.Product;
        ItemsGetter.Items["fixture-shelf"].itemsThatCanShowcase = new[] { DonutFeasibilityReader.Product };
        Check(CandidateReader.Read(r).Refusal == "No direct stocked candidate showcase shelf observed",
            "donut never substitutes for cupcake");
        Check(DonutFeasibilityReader.Read(r).DirectStockFieldsVerified, "descriptor switch reads donut independently");
        string mixedBefore = Fingerprint(r);
        for (int i = 0; i < 100; i++) { CandidateReader.Read(r); DonutFeasibilityReader.Read(r); }
        Check(mixedBefore == Fingerprint(r), "interleaved candidates share metadata only, never stock or results");
#endif
        var timings = new List<object>();
        foreach (int size in new[] { 1, 1999 })
        {
            r = Fixture(size); for (int i = 0; i < 30; i++) CandidateReader.Read(r);
            var samples = new double[1001]; long bytes = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                allocation = GC.GetAllocatedBytesForCurrentThread(); long start = Stopwatch.GetTimestamp();
                var value = CandidateReader.Read(r);
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

public sealed class BuildingRegistration
{
    public bool RentedByPlayer = true, HasEstablishedBusiness = true, AvailableForRent, temporarilyClosed;
    public string businessTypeName = "fixture-full";
    public List<string> cachedAvailableProducts = new() { CandidateReader.Product };
    public List<RetailPrice> retailPrices = new() { new() { itemName = CandidateReader.Product, price = 10.2528f } };
    public Dictionary<string, ItemInstance> itemInstances = new();
}
public sealed class RetailPrice { public string itemName = ""; public float price; }
namespace BigAmbitions.Tags
{
    public abstract class TaggedScriptableObject
    {
        private List<string> tags = new();
        public List<string> AuthoredTags => tags;
        public bool HasTag(int tag) => throw new Exception("Native tag cache refresh reached");
    }
    public static class TagRef
    {
        public static class Itemtag
        {
            public static int isbag => throw new Exception("Native generated tag getter reached");
            public static int isfullservicecashregister => throw new Exception("Native generated tag getter reached");
        }
        public static class Businesstag
        {
            public static int customersneedpaperbags => throw new Exception("Native generated tag getter reached");
        }
    }
}
namespace BigAmbitions.Items
{
    [Flags] public enum ItemType { RetailProduct = 1, ServiceProduct = 2, ShowcaseShelf = 4, EmployeeWorkstation = 8 }
    public sealed class CargoInstance
    {
        public string itemName = ""; public int amount;
        public List<object> nestedCargoInstances = new();
    }
    public sealed class ItemInstance
    {
        public string id = "", itemName = "";
        public List<CargoInstance> cargoInstances = new();
        [System.Text.Json.Serialization.JsonIgnore]
        public Item ItemCached => throw new Exception("Native lazy instance getter reached");
    }
    public sealed class Settings { public bool ResourcesRequired = true; public string[] itemsToProduce = Array.Empty<string>(); }
    public sealed class Item : Tags.TaggedScriptableObject
    {
        public ItemType type; public List<int> tagIndexes = new();
        public Settings producerSettings = new(); public string[] itemsThatCanShowcase = Array.Empty<string>();
    }
    public static class ItemsGetter
    {
        public static Dictionary<string, Item> Items = new();
        public static IEnumerable<Item> AllItems => Items.Values;
        public static Item? GetByName(string name, bool suppressError) => Items.GetValueOrDefault(name);
    }
}
namespace Buildings.Retail.Simulation { public sealed class FullServiceBusinessSimulator { } }
namespace Helpers
{
    public sealed class BusinessType : BigAmbitions.Tags.TaggedScriptableObject
    {
        public object simulator = new Buildings.Retail.Simulation.FullServiceBusinessSimulator();
        public bool hasEntranceFee;
        public BusinessType() { AuthoredTags.Add("ba:businesstag_customersneedpaperbags"); }
    }
    public static class BusinessTypeHelper
    {
        public static BusinessType Data = new();
        public static BusinessType GetData(string name) => Data;
    }
}
