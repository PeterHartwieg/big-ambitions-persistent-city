using System.Diagnostics;
using System.Text.Json;
using BigAmbitions.Items;
using PersistentCity.Core;
using PersistentCity.Runtime;

// All loaded definitions below are hypothetical doubles, never shipped asset claims.
static class RemainingRegression
{
    internal sealed record Candidate(string Name, string Product,
        Func<BuildingRegistration, DirectStockFeasibilitySnapshot> Read, bool Seasonal = false);
    static readonly Candidate[] Existing = {
        new("Donut", DonutFeasibilityReader.Product, DonutFeasibilityReader.Read),
        new("Cupcake", CupcakeFeasibilityReader.Product, CupcakeFeasibilityReader.Read),
        new("Salad", SaladFeasibilityReader.Product, SaladFeasibilityReader.Read),
        new("Icecream", IcecreamFeasibilityReader.Product, IcecreamFeasibilityReader.Read),
        new("FrenchFries", FrenchFriesFeasibilityReader.Product, FrenchFriesFeasibilityReader.Read),
        new("Pizza", PizzaFeasibilityReader.Product, PizzaFeasibilityReader.Read)
    };
#if !BASELINE
    static readonly Candidate[] Batch = {
        new("CheesePlatter", CheesePlatterFeasibilityReader.Product, CheesePlatterFeasibilityReader.Read),
        new("CottonCandy", CottonCandyFeasibilityReader.Product, CottonCandyFeasibilityReader.Read),
        new("Popcorn", PopcornFeasibilityReader.Product, PopcornFeasibilityReader.Read),
        new("Kabob", KabobFeasibilityReader.Product, KabobFeasibilityReader.Read),
        new("CandyCane", CandyCaneFeasibilityReader.Product, CandyCaneFeasibilityReader.Read, true),
        new("ChristmasCookies", ChristmasCookiesFeasibilityReader.Product, ChristmasCookiesFeasibilityReader.Read, true)
    };
#else
    static readonly Candidate[] Batch = Array.Empty<Candidate>();
#endif
    static Candidate[] All => Existing.Concat(Batch).ToArray();
    static int checks;
    static void Check(bool ok, string why) { if (!ok) throw new Exception(why); checks++; }
    static BuildingRegistration Fixture(Candidate c, int shelves = 1)
    {
        ItemsGetter.Items = new() {
            [c.Product] = new() { type = ItemType.RetailProduct },
            ["fixture-holder"] = new() { type = ItemType.ShowcaseShelf, itemsThatCanShowcase = new[] { c.Product } },
            ["fixture-counter"] = new() { AuthoredTags = { "ba:itemtag_isfullservicecashregister" } },
            ["fixture-bag"] = new() { AuthoredTags = { "ba:itemtag_isbag" } }
        };
        Helpers.BusinessTypeHelper.Data = new();
        var r = new BuildingRegistration { cachedAvailableProducts = new() { c.Product },
            retailPrices = new() { new() { itemName = c.Product, price = 10.2528f } } };
        for (int i = 0; i < shelves; i++) r.itemInstances.Add("s" + i, new() {
            id = "s" + i, itemName = "fixture-holder",
            cargoInstances = new() { new() { itemName = c.Product, amount = 7 } } });
        r.itemInstances.Add("counter", new() { id = "counter", itemName = "fixture-counter",
            cargoInstances = new() { new() { itemName = "fixture-bag", amount = 3 } } });
        return r;
    }
    static string Fingerprint(BuildingRegistration r) => JsonSerializer.Serialize(new { r, ItemsGetter.Items,
        Helpers.BusinessTypeHelper.Data }, new JsonSerializerOptions { IncludeFields = true,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals });
    static void Refusal(Candidate c, Action<BuildingRegistration> change, string expected)
    {
        var r = Fixture(c); change(r); string before = Fingerprint(r);
        var result = c.Read(r);
        Check(!result.DirectStockFieldsVerified && result.Refusal.Contains(expected), c.Name + ": " + expected + ": " + result.Refusal);
        Check(before == Fingerprint(r), c.Name + " refused read mutated fields");
    }
    static void Cases(Candidate c)
    {
        var r = Fixture(c); string before = Fingerprint(r); var snapshot = c.Read(r);
        Check(snapshot.ProductId == c.Product && snapshot.DirectStockFieldsVerified, c.Name + " pinned conditional identity");
        Check(snapshot.RawProductUnits == 7 && snapshot.RawBagUnits == 3 && snapshot.RawCounters == 1, c.Name + " raw fields");
        Check(snapshot.Price == Money.FromNative(10.2528f) && snapshot.Eligibility.StartsWith("Unknown"), "money/eligibility");
        for (int i = 0; i < 100; i++) c.Read(r);
        Check(before == Fingerprint(r), "100 reads preserve every fixture field");
        r.itemInstances["s0"].cargoInstances[0].amount = 0;
        r.itemInstances["counter"].cargoInstances[0].amount = 0;
        Check(c.Read(r).DirectStockFieldsVerified && c.Read(r).RawProductUnits == 0 && c.Read(r).RawBagUnits == 0, "known zeros");
        Check(snapshot.RawProductUnits == 7 && snapshot.RawBagUnits == 3 && snapshot.Equipment is not string[], "detached output");
        Refusal(c, x => x.RentedByPlayer = false, "player business");
        Refusal(c, x => x.temporarilyClosed = true, "player business");
        Refusal(c, x => x.HasEstablishedBusiness = false, "player business");
        Refusal(c, x => Helpers.BusinessTypeHelper.Data.simulator = new object(), "simulator");
        Refusal(c, x => Helpers.BusinessTypeHelper.Data.hasEntranceFee = true, "entrance fee");
        Refusal(c, x => Helpers.BusinessTypeHelper.Data.AuthoredTags.Clear(), "bag contract");
        Refusal(c, x => ItemsGetter.Items.Remove(c.Product), "retail product");
        Refusal(c, x => ItemsGetter.Items[c.Product].type = ItemType.ServiceProduct, "retail product");
        Refusal(c, x => ItemsGetter.Items[c.Product].type = 0, "retail product");
        Refusal(c, x => x.cachedAvailableProducts.Clear(), "advertised");
        Refusal(c, x => x.cachedAvailableProducts.Add(c.Product), "ambiguous");
        Refusal(c, x => x.retailPrices.Clear(), "advertised product/price");
        Refusal(c, x => x.retailPrices.Add(new() { itemName = c.Product, price = 1 }), "ambiguous");
        foreach (float price in new[] { 0, -1, float.NaN, float.PositiveInfinity, float.Epsilon, 10001 })
            Refusal(c, x => x.retailPrices[0].price = price, "price");
        // These exact independent legacy identities supply no ingredient/recipe relationship.
        foreach (string input in new[] { "ba:itemname_cheese", "ba:itemname_slicedcheese", "ba:itemname_cheesesauce",
            "ba:itemname_sugar", "ba:itemname_swirledsugar", "ba:itemname_decorativepopcorn" })
        {
            Refusal(c, x => x.cachedAvailableProducts[0] = input, "advertised");
            Refusal(c, x => { ItemsGetter.Items["fixture-holder"].producerSettings.itemsToProduce = new[] { c.Product };
                x.itemInstances["s0"].cargoInstances[0].itemName = input; }, "Converted input");
        }
        Refusal(c, x => ItemsGetter.Items["fixture-holder"].itemsThatCanShowcase = new[] { "other" }, "whitelist mismatch");
        Refusal(c, x => ItemsGetter.Items["fixture-holder"].producerSettings.ResourcesRequired = false, "resources");
        Refusal(c, x => ItemsGetter.Items["fixture-holder"].producerSettings.itemsToProduce = new[] { c.Product, "other" }, "Shared producer outputs");
        Refusal(c, x => x.itemInstances["s0"].cargoInstances.Add(new() { itemName = "other" }), "Shared, nested");
        Refusal(c, x => x.itemInstances["s0"].cargoInstances[0].nestedCargoInstances.Add(new()), "nested");
        Refusal(c, x => x.itemInstances["s0"].cargoInstances[0].amount = -1, "malformed");
        Refusal(c, x => x.itemInstances["counter"].cargoInstances.Clear(), "Shared, nested");
        Refusal(c, x => x.itemInstances["counter"].cargoInstances[0].itemName = "missing", "native bag");
        Refusal(c, x => x.itemInstances["counter"].cargoInstances[0].itemName = c.Product, "Bag/counter");
        Refusal(c, x => x.itemInstances.Remove("counter"), "No bag counter");
        Refusal(c, x => x.itemInstances["s0"].id = "counter", "identity/sharing");
        Refusal(c, x => x.itemInstances["s0"].itemName = "missing", "definition");
        Refusal(c, x => { for (int i = 0; i < 129; i++) x.cachedAvailableProducts.Add("other"); }, "oversized");
        foreach (string output in new[] { c.Product, "other" })
            Refusal(c, x => { ItemsGetter.Items["fixture-holder"].type = ItemType.EmployeeWorkstation;
                ItemsGetter.Items["fixture-holder"].producerSettings.itemsToProduce = new[] { output }; }, "Workstation/shared");
        r = Fixture(c, 2);
        Check(c.Read(r).RawProductUnits == 14 && c.Read(r).RawBagUnits == 3, "holders share one bag pool");
        r.itemInstances["s0"].cargoInstances[0].amount = int.MaxValue;
        Check(c.Read(r).Refusal.Contains("overflow"), "overflow");
        r = Fixture(c); ItemsGetter.Items["fixture-storage"] = new();
        r.itemInstances.Add("storage", new() { id = "storage", itemName = "fixture-storage",
            cargoInstances = new() { new() { itemName = c.Product, amount = 1000 } } });
        Check(c.Read(r).RawProductUnits == 7, "storage excluded");
        foreach (var other in All.Where(o => o.Product != c.Product))
        {
            r = Fixture(other); ItemsGetter.Items[c.Product] = new() { type = ItemType.RetailProduct };
            r.cachedAvailableProducts.Add(c.Product); r.retailPrices.Add(new() { itemName = c.Product, price = 12 });
            var expected = JsonSerializer.Serialize(other.Read(r)); before = Fingerprint(r);
            for (int i = 0; i < 10; i++)
            {
                Check(c.Read(r).Refusal == "No direct stocked candidate showcase shelf observed", "no candidate substitution");
                Check(JsonSerializer.Serialize(other.Read(r)) == expected, "unchanged interleaved " + other.Name);
            }
            Check(before == Fingerprint(r), "interleaved native noninterference");
        }
        if (c.Seasonal)
        {
            foreach (var season in new[] { Seasons.SeasonName.Halloween, Seasons.SeasonName.Christmas, (Seasons.SeasonName)999 })
                Refusal(c, x => ItemsGetter.Items[c.Product].season = season, "Seasonal availability");
            Refusal(c, x => ItemsGetter.Items[c.Product].isSpecialGift = true, "Seasonal availability");
            Refusal(c, x => ItemsGetter.Items[c.Product].isSeasonalForSale = true, "Seasonal availability");
            Refusal(c, x => ItemsGetter.Items[c.Product].isSeasonalItemFromAIBusiness = true, "Seasonal availability");
            Refusal(c, x => ItemsGetter.Items[c.Product].itemsBySeason = new[] { new Seasons.ItemSeason {
                seasonName = Seasons.SeasonName.Christmas, itemName = "other" } }, "Seasonal availability");
            r = Fixture(c); ItemsGetter.Items[c.Product].itemsBySeason = Array.Empty<Seasons.ItemSeason>();
            Check(c.Read(r).DirectStockFieldsVerified, "explicitly ungated hypothetical definition only");
        }
    }
    static void Concessions()
    {
        var trio = Batch.Take(3).ToArray();
        foreach (var c in trio)
            foreach (string name in new[] { "ba:itemname_concessionscounter", "ba:itemname_concessionsdisplaycase", "ba:itemname_concessionsstandregister" })
            {
                var r = Fixture(c); ItemsGetter.Items[name] = ItemsGetter.Items["fixture-holder"];
                r.itemInstances["s0"].itemName = name;
                Check(c.Read(r).DirectStockFieldsVerified && c.Read(r).Equipment.Single() == name, "fields, never name authorize holder");
                ItemsGetter.Items[name].type = ItemType.EmployeeWorkstation;
                ItemsGetter.Items[name].producerSettings.itemsToProduce = new[] { c.Product };
                Check(c.Read(r).Refusal.Contains("Workstation/shared"), "named equipment does not authorize workstation");
                ItemsGetter.Items[name].type = ItemType.ShowcaseShelf;
                ItemsGetter.Items[name].AuthoredTags.Add("ba:itemtag_isfullservicecashregister");
                Check(c.Read(r).Refusal.Contains("Bag/counter"), "named register cannot merge bag/product stock");
            }
        if (trio.Length == 0) return;
        var shop = Fixture(trio[0]); var ids = trio.Select(c => c.Product).ToArray();
        ItemsGetter.Items["fixture-holder"].itemsThatCanShowcase = ids;
        for (int i = 1; i < trio.Length; i++)
        {
            ItemsGetter.Items[ids[i]] = new() { type = ItemType.RetailProduct };
            shop.cachedAvailableProducts.Add(ids[i]); shop.retailPrices.Add(new() { itemName = ids[i], price = 10 + i });
            shop.itemInstances.Add("s" + i, new() { id = "s" + i, itemName = "fixture-holder",
                cargoInstances = new() { new() { itemName = ids[i], amount = 7 + i } } });
        }
        string before = Fingerprint(shop);
        for (int i = 0; i < trio.Length; i++)
        {
            var read = trio[i].Read(shop);
            Check(read.RawProductUnits == 7 + i && read.RawBagUnits == 3 && read.RawCounters == 1,
                "same equipment definition and whitelist, independent per-product cargo; one bag pool");
        }
        Check(before == Fingerprint(shop), "shared-equipment reads preserve fields");
        shop.itemInstances["s1"].cargoInstances[0].amount = 0;
        Check(trio[1].Read(shop).RawProductUnits == 0 && trio[0].Read(shop).RawProductUnits == 7
            && trio[2].Read(shop).RawProductUnits == 9, "one product shortage never drains others");
        ItemsGetter.Items["fixture-holder"].producerSettings.itemsToProduce = ids;
        for (int i = 0; i < trio.Length; i++)
            Check(trio[i].Read(shop).Refusal.Contains(i == 0 ? "Shared producer outputs" : "Converted input"),
                "shared output holder refuses before any pool can be reported");
        ItemsGetter.Items["fixture-holder"].producerSettings.itemsToProduce = Array.Empty<string>();
        shop.itemInstances["s0"].cargoInstances.Add(new() { itemName = ids[1], amount = 1 });
        foreach (var c in trio.Take(2)) Check(c.Read(shop).Refusal.Contains("Shared, nested"), "no mixed cargo bundle");
    }
    static object MeasureOne(Candidate c)
    {
        var r = Fixture(c); long start = Stopwatch.GetTimestamp(); Stopwatch.GetElapsedTime(start);
        long allocated = GC.GetAllocatedBytesForCurrentThread(); start = Stopwatch.GetTimestamp();
        var cold = c.Read(r); double coldMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        long coldBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Check(cold.DirectStockFieldsVerified, "cold fixture");
        var warm = new List<object>();
        foreach (int shelves in new[] { 1, 1999 })
        {
            r = Fixture(c, shelves); for (int i = 0; i < 30; i++) c.Read(r);
            var samples = new double[1001]; long bytes = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                allocated = GC.GetAllocatedBytesForCurrentThread(); start = Stopwatch.GetTimestamp();
                var result = c.Read(r);
                samples[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                bytes += GC.GetAllocatedBytesForCurrentThread() - allocated;
                Check(result.DirectStockFieldsVerified, "warm fixture");
            }
            Array.Sort(samples);
            warm.Add(new { furniture = shelves + 1, p50Ms = samples[500], p95Ms = samples[950], maxMs = samples[^1],
                bytesPerCall = bytes / samples.Length, spikesAtLeast30Ms = samples.Count(x => x >= 30) });
        }
        allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1001; i++) GC.KeepAlive(r);
        long idleBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        return new { candidate = c.Name, cold = new { samples = 1, coldMs, coldBytes, spikesAtLeast30Ms = coldMs >= 30 ? 1 : 0 },
            warm, uncalledControlBytes = idleBytes };
    }
    static void Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "measure-one")
        {
            Console.WriteLine(JsonSerializer.Serialize(MeasureOne(All.Single(c => c.Name == args[1])))); return;
        }
        if (args.Length == 1 && args[0] == "measure")
        {
            // Each cold sample gets a fresh, synchronously awaited foreground apphost process.
            // One dotnet build/run workload for the entire batch; no detached test workers.
            var results = new List<JsonElement>();
            foreach (var c in All)
            {
                var info = new ProcessStartInfo(Environment.ProcessPath!) { RedirectStandardOutput = true, RedirectStandardError = true };
                info.ArgumentList.Add("measure-one"); info.ArgumentList.Add(c.Name);
                using var process = Process.Start(info)!;
                string output = process.StandardOutput.ReadToEnd(); string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0) throw new Exception(c.Name + ": " + error + output);
                results.Add(JsonSerializer.Deserialize<JsonElement>(output));
            }
            Console.WriteLine(JsonSerializer.Serialize(new { measurements = results })); return;
        }
        foreach (var c in All) Cases(c);
        Concessions();
        Console.WriteLine(JsonSerializer.Serialize(new { checks, existing = Existing.Length, batch = Batch.Length,
            candidates = Batch.Select(c => c.Name), nativeEvidence = false }));
    }
}
