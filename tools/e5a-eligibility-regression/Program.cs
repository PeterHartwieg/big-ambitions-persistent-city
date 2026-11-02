using System.Diagnostics;
using System.Text.Json;
using BigAmbitions.Items;
using PersistentCity.Core;
using PersistentCity.Runtime;

// Hypothetical native field doubles only. No loaded game product/holder claim.
static class Regression
{
    static int checks;
    static readonly string[] Modded = { "foodtrucks:itemname_dumplings", "foodtrucks:itemname_choppedcheese" };
    static readonly string[] Eight = { DonutFeasibilityReader.Product, CupcakeFeasibilityReader.Product,
        SaladFeasibilityReader.Product, PizzaFeasibilityReader.Product, IcecreamFeasibilityReader.Product,
        FrenchFriesFeasibilityReader.Product, CheesePlatterFeasibilityReader.Product, CottonCandyFeasibilityReader.Product };
    static readonly Func<BuildingRegistration, DirectStockFeasibilitySnapshot>[] Old = {
        DonutFeasibilityReader.Read, CupcakeFeasibilityReader.Read, SaladFeasibilityReader.Read, PizzaFeasibilityReader.Read,
        IcecreamFeasibilityReader.Read, FrenchFriesFeasibilityReader.Read, CheesePlatterFeasibilityReader.Read,
        CottonCandyFeasibilityReader.Read };
    static readonly string[] AllNames = Eight.Concat(new[] { PopcornFeasibilityReader.Product, KabobFeasibilityReader.Product,
        CandyCaneFeasibilityReader.Product, ChristmasCookiesFeasibilityReader.Product }).ToArray();
    static readonly Func<BuildingRegistration, DirectStockFeasibilitySnapshot>[] AllFacades = Old.Concat(new Func<BuildingRegistration, DirectStockFeasibilitySnapshot>[] {
        PopcornFeasibilityReader.Read, KabobFeasibilityReader.Read, CandyCaneFeasibilityReader.Read, ChristmasCookiesFeasibilityReader.Read }).ToArray();
    static void Check(bool ok, string why) { checks++; if (!ok) throw new Exception(why); }
    static Item Def(string id, ItemType type = 0) => new() { itemName = id, type = type };
    static BuildingRegistration Fixture(string[]? products = null, int size = 0, int catalogueSize = 0)
    {
        products ??= Modded;
        ItemsGetter.Items = new(StringComparer.Ordinal); ItemsGetter.View = null; ItemsGetter.Unready = false;
        Helpers.BusinessTypeHelper.Data = new();
        Helpers.BusinessTypeHelper.ThrowOnRead = false;
        foreach (string id in products) ItemsGetter.Items[id] = Def(id, ItemType.RetailProduct);
        ItemsGetter.Items["fixture:holder"] = new() { itemName = "fixture:holder", type = ItemType.ShowcaseShelf, itemsThatCanShowcase = products.ToArray() };
        ItemsGetter.Items["fixture:counter"] = new() { itemName = "fixture:counter", AuthoredTags = { "ba:itemtag_isfullservicecashregister" } };
        ItemsGetter.Items["fixture:bag"] = new() { itemName = "fixture:bag", AuthoredTags = { "ba:itemtag_isbag" } };
        ItemsGetter.Items["fixture:storage"] = Def("fixture:storage");
        ItemsGetter.Items["fixture:ingredient"] = Def("fixture:ingredient");
        var r = new BuildingRegistration { cachedAvailableProducts = products.ToList(),
            retailPrices = products.Select(id => new RetailPrice { itemName = id, price = 10.2528f }).ToList() };
        int shelves = size == 0 ? products.Length : size - 1;
        for (int i = 0; i < shelves; i++) r.itemInstances.Add("s" + i, new() { id = "s" + i, itemName = "fixture:holder",
            cargoInstances = new() { new() { itemName = products[i % products.Length], amount = 7 } } });
        r.itemInstances.Add("counter", new() { id = "counter", itemName = "fixture:counter",
            cargoInstances = new() { new() { itemName = "fixture:bag", amount = 3 } } });
        for (int i = ItemsGetter.Items.Count; i < catalogueSize; i++) ItemsGetter.Items.Add("arbitrary:" + i, Def("arbitrary:" + i));
        return r;
    }
    static string Fingerprint(BuildingRegistration r) => JsonSerializer.Serialize(new { r, ItemsGetter.Items, Helpers.BusinessTypeHelper.Data },
        new JsonSerializerOptions { IncludeFields = true, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals });
    static void Drain(LoadedCatalogueEligibilityReader reader)
    {
        int units = 0;
        while (reader.Continue())
        { Check(++units < 100000, "bounded completion"); }
    }
    static FoodEligibilityImage Read(BuildingRegistration r, int limit = 8)
    {
        using var reader = new LoadedCatalogueEligibilityReader();
        Check(reader.MarkShopDirty("shop", r, limit), "queue shop"); Drain(reader);
        Check(reader.TryGet("shop", out var image), "atomic published result"); return image;
    }
    static void Refusal(string name, Action<BuildingRegistration> change)
    {
        var r = Fixture(); change(r); string before = Fingerprint(r);
        var image = Read(r);
        Check(!image.StockContractMatched && image.Offers.Count == 0 && image.Holders.Count == 0, name + ": " + image.Refusal);
        Check(before == Fingerprint(r), "refusal noninterference " + name);
    }
    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "cold")
        { Cold(int.Parse(args[1]), int.Parse(args[2]), int.Parse(args[3])); return; }
        NativeReadFailure(); Cases(); Lifecycle(); Bounds(); Differential();
        Console.WriteLine(JsonSerializer.Serialize(new { checks, measurements = Measure() }));
    }
    static void NativeReadFailure()
    {
        foreach (bool unready in new[] { false, true })
        {
            var r = Fixture(); ItemsGetter.Unready = unready;
            string before = Fingerprint(r);
            Helpers.BusinessTypeHelper.ThrowOnRead = true;
            try
            {
                var image = Read(r);
                Check(image.Refusal == FoodEligibilityRefusal.NativeRead,
                    "StartShop native exception must remain NativeRead, catalogue unready=" + unready + "; got " + image.Refusal);
                Check(!image.StockContractMatched && image.Offers.Count == 0 && image.Holders.Count == 0
                    && image.RawBagUnits == 0 && image.RawCounters == 0, "native failure publishes only refused evidence");
                Check(before == Fingerprint(r), "native failure preserves fixture fields");
            }
            finally { Helpers.BusinessTypeHelper.ThrowOnRead = false; }
        }
    }
    static void Cases()
    {
        foreach (string id in Modded.Concat(new[] { "arbitrary:vanilla", "BA:CaseSensitive", "食べ物:🍜", new string('x', 256) }))
        {
            var r = Fixture(new[] { id }); string before = Fingerprint(r); var result = Read(r);
            Check(result.StockContractMatched && result.Offers.Single().ProductId == id, "dynamic exact name " + id);
            Check(result.Offers[0].Price == Money.FromNative(10.2528f) && result.Offers[0].RawUnits == 7, "exact money and quantity");
            Check(result.RawBagUnits == 3 && result.RawCounters == 1 && result.Eligibility.StartsWith("Unknown"), "raw only");
            Check(before == Fingerprint(r), "success noninterference");
            r.itemInstances["s0"].cargoInstances[0].amount = 0; r.itemInstances["counter"].cargoInstances[0].amount = 0;
            Check(Read(r).StockContractMatched && Read(r).Offers[0].RawUnits == 0 && Read(r).RawBagUnits == 0, "known zero");
            Check(result.Offers[0].RawUnits == 7 && result.Holders is not FoodHolderResourceImage[], "detached result");
        }
        Refusal("owner", r => r.RentedByPlayer = false);
        Refusal("established", r => r.HasEstablishedBusiness = false);
        Refusal("available rent", r => r.AvailableForRent = true);
        Refusal("closed", r => r.temporarilyClosed = true);
        Refusal("simulator", r => Helpers.BusinessTypeHelper.Data.simulator = new object());
        Refusal("fee", r => Helpers.BusinessTypeHelper.Data.hasEntranceFee = true);
        Refusal("bags policy", r => Helpers.BusinessTypeHelper.Data.AuthoredTags.Clear());
        Refusal("missing product", r => ItemsGetter.Items.Remove(Modded[0]));
        Refusal("nonretail", r => ItemsGetter.Items[Modded[0]].type = 0);
        Refusal("service", r => ItemsGetter.Items[Modded[0]].type |= ItemType.ServiceProduct);
        Refusal("missing equipment", r => ItemsGetter.Items.Remove("fixture:holder"));
        Refusal("missing bag", r => ItemsGetter.Items.Remove("fixture:bag"));
        Refusal("menu duplicate", r => r.cachedAvailableProducts.Add(Modded[0]));
        Refusal("menu absent", r => r.cachedAvailableProducts.Clear());
        Refusal("missing explicit price whole menu", r => r.retailPrices.RemoveAt(0));
        Refusal("duplicate price", r => r.retailPrices.Add(r.retailPrices[0]));
        Refusal("null price", r => r.retailPrices[0] = null!);
        foreach (float p in new[] { 0, -1, float.NaN, float.NegativeInfinity, float.PositiveInfinity, float.Epsilon, 10001, 0.00000001f })
            Refusal("price " + p, r => r.retailPrices[0].price = p);
        Refusal("key id", r => r.itemInstances["s0"].id = "s1");
        Refusal("null instance", r => r.itemInstances["s0"] = null!);
        Refusal("comparer", r => r.itemInstances = new(r.itemInstances, StringComparer.OrdinalIgnoreCase));
        Refusal("whitelist", r => ItemsGetter.Items["fixture:holder"].itemsThatCanShowcase = new[] { "other" });
        Refusal("resource", r => ItemsGetter.Items["fixture:holder"].producerSettings.ResourcesRequired = false);
        Refusal("null settings", r => ItemsGetter.Items["fixture:holder"].producerSettings = null!);
        Refusal("null authoring metadata", r => typeof(BigAmbitions.Tags.TaggedScriptableObject).GetField("tags",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(ItemsGetter.Items[Modded[0]], null));
        Refusal("outputs", r => ItemsGetter.Items["fixture:holder"].producerSettings.itemsToProduce = Modded.ToArray());
        Refusal("wrong output", r => ItemsGetter.Items["fixture:holder"].producerSettings.itemsToProduce = new[] { "fixture:ingredient" });
        Refusal("workstation same cargo", r => ItemsGetter.Items["fixture:holder"].type = ItemType.EmployeeWorkstation);
        Refusal("nonshowcase declared", r => { ItemsGetter.Items["fixture:holder"].type = 0; ItemsGetter.Items["fixture:holder"].producerSettings.itemsToProduce = Modded; });
        Refusal("conversion", r => { ItemsGetter.Items["fixture:holder"].producerSettings.itemsToProduce = new[] { Modded[0] }; r.itemInstances["s0"].cargoInstances[0].itemName = "fixture:ingredient"; });
        Refusal("multi cargo", r => r.itemInstances["s0"].cargoInstances.Add(new() { itemName = "fixture:ingredient" }));
        Refusal("nested", r => r.itemInstances["s0"].cargoInstances[0].nestedCargoInstances.Add(new()));
        Refusal("null nested", r => r.itemInstances["s0"].cargoInstances[0].nestedCargoInstances = null!);
        Refusal("negative cargo", r => r.itemInstances["s0"].cargoInstances[0].amount = -1);
        Refusal("null cargo", r => r.itemInstances["s0"].cargoInstances = null!);
        Refusal("null slot", r => r.itemInstances["s0"].cargoInstances[0] = null!);
        Refusal("no holder", r => r.itemInstances.Remove("s0"));
        Refusal("no counter", r => r.itemInstances.Remove("counter"));
        Refusal("no counter cargo", r => r.itemInstances["counter"].cargoInstances.Clear());
        Refusal("not bag", r => ItemsGetter.Items["fixture:bag"].AuthoredTags.Clear());
        Refusal("counter product", r => r.itemInstances["counter"].cargoInstances[0].itemName = Modded[0]);
        Refusal("counter shelf", r => ItemsGetter.Items["fixture:counter"].type = ItemType.ShowcaseShelf);
        Refusal("counter produces", r => ItemsGetter.Items["fixture:counter"].producerSettings.itemsToProduce = Modded);
        Refusal("shared reference", r => r.itemInstances["s1"].cargoInstances = r.itemInstances["s0"].cargoInstances);
        Refusal("overflow", r => { r.itemInstances["s0"].cargoInstances[0].amount = int.MaxValue;
            r.itemInstances.Add("extra", new() { id = "extra", itemName = "fixture:holder", cargoInstances = new() { new() { itemName = Modded[0], amount = 1 } } }); });
        Refusal("bag overflow", r => { r.itemInstances["counter"].cargoInstances[0].amount = int.MaxValue;
            r.itemInstances.Add("extra", new() { id = "extra", itemName = "fixture:counter", cargoInstances = new() { new() { itemName = "fixture:bag", amount = 1 } } }); });
        foreach (string target in new[] { Modded[0], "fixture:holder", "fixture:counter", "fixture:bag" })
        {
            foreach (var season in new[] { Seasons.SeasonName.Halloween, Seasons.SeasonName.Christmas, (Seasons.SeasonName)999 })
                Refusal("season " + target, r => ItemsGetter.Items[target].season = season);
            Refusal("gift " + target, r => ItemsGetter.Items[target].isSpecialGift = true);
            Refusal("sale " + target, r => ItemsGetter.Items[target].isSeasonalForSale = true);
            Refusal("ai " + target, r => ItemsGetter.Items[target].isSeasonalItemFromAIBusiness = true);
            Refusal("substitution " + target, r => ItemsGetter.Items[target].itemsBySeason = new[] { new Seasons.ItemSeason() });
            Refusal("takeover " + target, r => ItemsGetter.Items[target].itemToReplaceWithWhenOvertakingBusiness = "anything");
            Refusal("tags " + target, r => { for (int i = 0; i < 129; i++) ItemsGetter.Items[target].AuthoredTags.Add("tag"); });
        }
        var shop = Fixture(); shop.itemInstances.Add("storage", new() { id = "storage", itemName = "fixture:storage",
            cargoInstances = new() { new() { itemName = Modded[0], amount = 999 } } });
        var image = Read(shop); Check(image.StockContractMatched && image.Offers.All(o => o.RawUnits == 7), "storage not supply");
        string fingerprint = Fingerprint(shop);
        using var cache = new LoadedCatalogueEligibilityReader(); cache.MarkShopDirty("shop", shop); Drain(cache);
        long fields = cache.HoldersRead, definitions = cache.DefinitionsRead, lookups = ItemsGetter.Lookups, all = ItemsGetter.AllReads;
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) { cache.TryGet("shop", out _); cache.Continue(); }
        Check(GC.GetAllocatedBytesForCurrentThread() == allocated, "clean cache zero allocation");
        Check(fields == cache.HoldersRead && definitions == cache.DefinitionsRead && lookups == ItemsGetter.Lookups && all == ItemsGetter.AllReads, "clean cache zero scans/lookups");
        Check(fingerprint == Fingerprint(shop), "10000 cached reads noninterference");
    }
    static void Lifecycle()
    {
        Check(LoadedCatalogueLifecycleMembers.Discover().Count == 19, "exact lifecycle signature discovery, never invokes");
        var r = Fixture(); using var cache = new LoadedCatalogueEligibilityReader();
        cache.MarkShopDirty("shop", r); Check(!cache.TryGet("shop", out _), "no partial catalogue/menu"); Drain(cache);
        cache.TryGet("shop", out var original);
        ItemsGetter.UnregisterModItem(Modded[0]); cache.MarkCatalogueDirty(); Check(!cache.TryGet("shop", out _), "unregister immediately unknown"); Drain(cache);
        Check(cache.TryGet("shop", out var absent) && !absent.StockContractMatched, "missing product refuses whole menu");
        ItemsGetter.RegisterModItem(Def(Modded[0], ItemType.RetailProduct)); cache.MarkCatalogueDirty(); Drain(cache);
        Check(cache.TryGet("shop", out var returned) && returned.StockContractMatched, "return exact same name");
        var winner = ItemsGetter.Items[Modded[0]];
        ItemsGetter.RegisterModItem(Def(Modded[0], ItemType.ServiceProduct)); cache.MarkCatalogueDirty(); Drain(cache);
        Check(ReferenceEquals(winner, ItemsGetter.Items[Modded[0]]) && cache.TryGet("shop", out var collision) && collision.StockContractMatched, "ignored native registration collision retains winner");
        foreach (bool reverse in new[] { true, false })
        {
            var defs = ItemsGetter.Items.Values.ToArray(); if (reverse) Array.Reverse(defs);
            ItemsGetter.OnItemsLoaded(defs); cache.MarkCatalogueDirty();
            r.cachedAvailableProducts.Reverse(); r.retailPrices.Reverse(); r.itemInstances = r.itemInstances.Reverse().ToDictionary(p => p.Key, p => p.Value);
            cache.MarkShopDirty("shop", r); Drain(cache); cache.TryGet("shop", out var permuted);
            Check(Shape(original) == Shape(permuted), "enumeration order independent");
        }
        var definitionsBeforeCollision = ItemsGetter.Items.Values.ToArray();
        var vanillaWinner = Def(Modded[0], ItemType.ServiceProduct);
        ItemsGetter.OnItemsLoaded(definitionsBeforeCollision.Where(d => d.itemName != Modded[0]).Append(vanillaWinner).ToArray());
        cache.MarkCatalogueDirty(); Drain(cache);
        Check(ReferenceEquals(ItemsGetter.Items[Modded[0]], vanillaWinner) && cache.TryGet("shop", out var vanillaCollision)
            && vanillaCollision.Refusal == FoodEligibilityRefusal.RetailType, "OnItemsLoaded vanilla collision winner determines truth");
        ItemsGetter.OnItemsLoaded(definitionsBeforeCollision); cache.MarkCatalogueDirty(); Drain(cache);
        for (int i = 0; i < 1000; i++) { cache.MarkCatalogueDirty(); cache.MarkShopDirty("shop", r); }
        long passes = cache.CataloguePasses, shops = cache.ShopPasses;
        Check(cache.PendingShops == 1, "dirty storm coalesces shop queue"); Drain(cache);
        Check(cache.CataloguePasses == passes + 1 && cache.ShopPasses == shops + 1, "one rebuild per storm");
        cache.Reset(); Check(!cache.TryGet("shop", out _) && cache.PendingShops == 0, "unload drops previous city");
        r = Fixture(new[] { "reload:another" }); cache.MarkShopDirty("new", r); Drain(cache);
        Check(cache.TryGet("new", out var next) && next.Offers[0].ProductId == "reload:another", "fresh load no remap");
        r = Fixture(new[] { "chunked:product" }, 2000, 16384); cache.Reset(); cache.MarkShopDirty("big", r);
        long d = cache.DefinitionsRead, h = cache.HoldersRead;
        for (int i = 0; i < 10; i++)
        {
            cache.Continue(); Check(cache.DefinitionsRead - d <= 256 && cache.HoldersRead - h <= 128, "unit scan bounds");
            d = cache.DefinitionsRead; h = cache.HoldersRead;
            Check(!cache.TryGet("big", out _), "no partial publish");
        }
        cache.MarkCatalogueDirty(); Drain(cache);
        Check(cache.TryGet("big", out var complete) && complete.StockContractMatched, "interrupted catalogue rebuild");
        cache.MarkShopDirty("big", r); cache.Continue();
        r.itemInstances["s0"].cargoInstances[0].amount = 0; cache.MarkShopDirty("big", r); Drain(cache);
        Check(cache.TryGet("big", out var changed) && changed.Offers[0].RawUnits == 1998 * 7, "interrupted holder rebuild discards previous work");
        cache.MarkShopDirty("big", r); cache.Continue(); r.itemInstances.Add("late", new() { id = "late", itemName = "fixture:storage" }); Drain(cache);
        Check(cache.TryGet("big", out var mutation) && mutation.Refusal == FoodEligibilityRefusal.NativeRead, "unnotified structural mutation invalidates enumeration");
        cache.Reset();
        for (int i = 0; i < 1024; i++) Check(cache.MarkShopDirty("q" + i, r), "bounded backlog admission");
        Check(!cache.MarkShopDirty("overflow", r) && cache.PendingShops == 1024, "1025 refuses without eviction");
        cache.Reset();
    }
    static string Shape(FoodEligibilityImage image) => JsonSerializer.Serialize(new { image.Offers, image.Holders, image.RawBagUnits, image.RawCounters, image.Refusal });
    static void Bounds()
    {
        foreach (string invalid in new[] { "", " ", "a|b", "a\\b", "a\nb", "x\uD800", "\uDC00x", new string('x', 257) })
        {
            Check(!LoadedStockLimits.CanonicalName(invalid), "canonical name refusal");
            var r = Fixture(new[] { invalid }); Check(!Read(r).StockContractMatched, "invalid loaded name");
        }
        Check(LoadedStockLimits.CanonicalName(" exact case ") && LoadedStockLimits.CanonicalName("食品:🍜"), "no trimming or normalization");
        var shop = Fixture(Eight, 2000, 16384); Check(Read(shop).StockContractMatched, "exact catalogue/furniture/eight menu bounds");
        Check(!Read(shop, 7).StockContractMatched, "configured lower limit");
        shop = Fixture(Eight.Concat(new[] { "ninth:product" }).ToArray()); Check(!Read(shop).StockContractMatched, "ninth rejects whole menu");
        shop = Fixture(new[] { Modded[0] }, 2001); Check(!Read(shop).StockContractMatched, "2001 rejects before scan");
        shop = Fixture(new[] { Modded[0] }, 2, 16385); using (var cache = new LoadedCatalogueEligibilityReader())
        {
            var enumeration = new CountedCatalogue(ItemsGetter.Items.Values); ItemsGetter.View = enumeration;
            cache.MarkShopDirty("s", shop); Drain(cache);
            Check(cache.CatalogueRefusal == FoodEligibilityRefusal.CatalogueBounds && cache.DefinitionsRead == 16384
                && enumeration.MoveNextCalls == 16385 && enumeration.CurrentReads == 16384, "16385 detected with one extra enumeration only");
        }
        foreach (string field in new[] { "menu", "prices", "cargo", "whitelist", "outputs" })
            Refusal("129 " + field, r => {
                switch (field)
                {
                    case "menu": r.cachedAvailableProducts = Enumerable.Repeat(Modded[0], 129).ToList(); break;
                    case "prices": r.retailPrices = Enumerable.Repeat(r.retailPrices[0], 129).ToList(); break;
                    case "cargo": r.itemInstances["s0"].cargoInstances = Enumerable.Range(0, 129).Select(_ => new CargoInstance { itemName = Modded[0] }).ToList(); break;
                    case "whitelist": ItemsGetter.Items["fixture:holder"].itemsThatCanShowcase = Enumerable.Repeat(Modded[0], 129).ToArray(); break;
                    case "outputs": ItemsGetter.Items["fixture:holder"].producerSettings.itemsToProduce = Enumerable.Repeat(Modded[0], 129).ToArray(); break;
                }
            });
        shop = Fixture(); ItemsGetter.View = new[] { ItemsGetter.Items[Modded[0]], ItemsGetter.Items[Modded[0]] };
        Check(!Read(shop).StockContractMatched, "duplicate visible canonical identity");
        shop = Fixture(); ItemsGetter.View = new[] { Def(Modded[0], ItemType.RetailProduct) };
        Check(!Read(shop).StockContractMatched, "definition lookup/view mismatch");
        shop = Fixture(); ItemsGetter.View = ThrowingView(); Check(!Read(shop).StockContractMatched, "throwing enumerator fail closed");
        shop = Fixture(); ItemsGetter.Unready = true; Check(!Read(shop).StockContractMatched, "unready catalogue");
        shop = Fixture(); ItemsGetter.Items.Clear(); Check(!Read(shop).StockContractMatched, "empty catalogue");
        var catalogue = new LoadedStockCatalogue(1);
        var a = new[] { "copied:product" };
        var definition = new LoadedStockDefinition("copied:holder", false, false, true, false, true, true, false, false, true, a, a);
        a[0] = "mutated"; Check(definition.Whitelist![0] == "copied:product" && definition.Outputs![0] == "copied:product", "immutable definition arrays");
        catalogue.Add(definition); catalogue.Seal();
        bool threw = false; try { catalogue.Add(definition); } catch (InvalidOperationException) { threw = true; }
        Check(threw, "sealed catalogue immutable");
        catalogue = new LoadedStockCatalogue(2);
        catalogue.Add(new LoadedStockDefinition("p", true, false, false, false, true, true, false, false, false, null, null));
        catalogue.Add(new LoadedStockDefinition("s", false, false, true, false, true, true, false, false, true, new[] { "p" }, null));
        catalogue.Seal();
        var pure = new LoadedStockMenuBuilder(catalogue, 1, true, new[] { "p" }, new[] { new StockPriceFields("p", 1) });
        pure.AddHolder("a", "a", "s", true, new[] { new StockCargoFields("p", 1, 0) });
        pure.AddHolder("a", "a", "s", true, new[] { new StockCargoFields("p", 1, 0) });
        Check(pure.Finish().Refusal == FoodEligibilityRefusal.FurnitureIdentity, "pure callers still refuse duplicate identity");
    }
    static IEnumerable<Item> ThrowingView() { yield return ItemsGetter.Items.Values.First(); throw new Exception("enumeration interrupted"); }
    sealed class CountedCatalogue : IEnumerable<Item>
    {
        readonly IEnumerable<Item> source;
        internal int MoveNextCalls, CurrentReads;
        internal CountedCatalogue(IEnumerable<Item> source) { this.source = source; }
        public IEnumerator<Item> GetEnumerator() => new Cursor(this, source.GetEnumerator());
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        sealed class Cursor : IEnumerator<Item>
        {
            readonly CountedCatalogue owner; readonly IEnumerator<Item> cursor;
            internal Cursor(CountedCatalogue owner, IEnumerator<Item> cursor) { this.owner = owner; this.cursor = cursor; }
            public Item Current { get { owner.CurrentReads++; return cursor.Current; } }
            object System.Collections.IEnumerator.Current => Current;
            public bool MoveNext() { owner.MoveNextCalls++; return cursor.MoveNext(); }
            public void Reset() => cursor.Reset();
            public void Dispose() => cursor.Dispose();
        }
    }
    static void Differential()
    {
        for (int i = 0; i < AllNames.Length; i++)
        {
            var r = Fixture(new[] { AllNames[i] }); var expected = AllFacades[i](r); var image = Read(r);
            Check(expected.DirectStockFieldsVerified && image.StockContractMatched && image.Offers[0].RawUnits == expected.RawProductUnits
                && image.Offers[0].Price == expected.Price && image.RawBagUnits == expected.RawBagUnits, "frozen facade differential");
            string before = Fingerprint(r);
            for (int j = 0; j < 10; j++) { AllFacades[i](r); Read(r); }
            Check(before == Fingerprint(r), "interleaved facade fields unchanged");
        }
    }
    static object Cold(int furniture, int products, int catalogueSize)
    {
        var r = Fixture(Enumerable.Range(0, products).Select(i => "cold:" + i).ToArray(), furniture, catalogueSize);
        if (catalogueSize == 0) ItemsGetter.Items.Clear();
        long allocated = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
        using var reader = new LoadedCatalogueEligibilityReader(); reader.MarkShopDirty("s", r);
        int units = 0; var unitTimes = new List<double>();
        while (true) { long tick = Stopwatch.GetTimestamp(); if (!reader.Continue()) break; unitTimes.Add(Stopwatch.GetElapsedTime(tick).TotalMilliseconds); units++; }
        var result = new { furniture, products, catalogueSize, totalMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds,
            bytes = GC.GetAllocatedBytesForCurrentThread() - allocated, units, maxUnitMs = unitTimes.Max(), spikesAtLeast30Ms = unitTimes.Count(t => t >= 30) };
        Console.WriteLine(JsonSerializer.Serialize(result)); return result;
    }
    static object Distribution(Action action, int samples = 101)
    {
        for (int i = 0; i < 20; i++) action();
        var times = new double[samples]; long bytes = 0; int gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2);
        for (int i = 0; i < samples; i++)
        {
            long allocation = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp(); action();
            times[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds; bytes += GC.GetAllocatedBytesForCurrentThread() - allocation;
        }
        Array.Sort(times);
        return new { samples, p50Ms = times[samples / 2], p95Ms = times[(int)(samples * .95)], maxMs = times[^1], bytesPerCall = bytes / samples,
            gen0 = GC.CollectionCount(0) - gc0, gen1 = GC.CollectionCount(1) - gc1, gen2 = GC.CollectionCount(2) - gc2,
            spikesAtLeast30Ms = times.Count(t => t >= 30) };
    }
    static object Measure()
    {
        var cold = new List<JsonElement>();
        foreach (int holders in new[] { 2, 2000 }) foreach (int products in new[] { 3, 8 }) foreach (int size in new[] { 0, 16, 16384 })
        {
            // Foreground synchronous apphost; no nested dotnet or background worker.
            var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "E5aEligibilityRegression.exe")) { RedirectStandardOutput = true, UseShellExecute = false };
            foreach (string arg in new[] { "cold", holders.ToString(), products.ToString(), size.ToString() }) start.ArgumentList.Add(arg);
            using var process = Process.Start(start)!; string output = process.StandardOutput.ReadToEnd(); process.WaitForExit();
            Check(process.ExitCode == 0, "cold child completion"); cold.Add(JsonDocument.Parse(output).RootElement.Clone());
        }
        var warm = new List<object>();
        foreach (int count in new[] { 1, 3, 8 }) foreach (int size in new[] { 2, 2000 }) foreach (int definitions in new[] { 0, 16, 16384 })
        {
            var r = Fixture(Enumerable.Range(0, count).Select(i => "warm:" + i).ToArray(), size, definitions);
            if (definitions == 0) ItemsGetter.Items.Clear();
            long cacheBaseline = GC.GetTotalMemory(true);
            using var reader = new LoadedCatalogueEligibilityReader(); reader.MarkShopDirty("s", r); Drain(reader);
            long cacheRetainedBytes = GC.GetTotalMemory(true) - cacheBaseline;
            var rebuild = Distribution(() => { reader.MarkShopDirty("s", r); while (reader.Continue()) { } });
            var catalogueRebuild = Distribution(() => { reader.MarkCatalogueDirty(); while (reader.Continue()) { } }, 21);
            long before = GC.GetTotalMemory(true); reader.MarkShopDirty("s", r); while (reader.Continue()) { }
            long retained = GC.GetTotalMemory(true) - before;
            long scans = reader.HoldersRead + reader.DefinitionsRead;
            var clean = Distribution(() => { reader.TryGet("s", out _); reader.Continue(); }, 1001);
            Check(scans == reader.HoldersRead + reader.DefinitionsRead, "warm clean scans unchanged");
            warm.Add(new { products = count, holders = size, definitions, rebuild, catalogueRebuild, clean, cacheRetainedBytes, retainedRebuildDeltaBytes = retained });
        }
        var fixture = Fixture(Eight, 2000, 16);
        var eightScans = Distribution(() => { foreach (var reader in Old) reader(fixture); });
        using var one = new LoadedCatalogueEligibilityReader(); one.MarkShopDirty("s", fixture); Drain(one);
        var onePass = Distribution(() => { one.MarkShopDirty("s", fixture); while (one.Continue()) { } });
        var storm = Distribution(() => { for (int i = 0; i < 1000; i++) one.MarkShopDirty("s", fixture); while (one.Continue()) { } });
        var catalogueStorm = Distribution(() => { for (int i = 0; i < 1000; i++) one.MarkCatalogueDirty(); while (one.Continue()) { } });
        var valid = new List<object>();
        foreach (int n in new[] { 3, 8 })
        {
            fixture = Fixture(Eight.Take(n).ToArray()); one.MarkCatalogueDirty(); one.MarkShopDirty("s", fixture); Drain(one);
            valid.Add(new { products = n, holders = n + 1, rebuild = Distribution(() => { one.MarkShopDirty("s", fixture); while (one.Continue()) { } }) });
        }
        fixture = Fixture(new[] { "backlog:product" }, 2000, 16384);
        using var backlog = new LoadedCatalogueEligibilityReader();
        long baseline = GC.GetTotalMemory(true);
        for (int i = 0; i < 1024; i++) backlog.MarkShopDirty("q" + i, fixture);
        var units = new List<double>(); long bytesStart = GC.GetAllocatedBytesForCurrentThread(), begun = Stopwatch.GetTimestamp();
        while (true) { long tick = Stopwatch.GetTimestamp(); if (!backlog.Continue()) break; units.Add(Stopwatch.GetElapsedTime(tick).TotalMilliseconds); }
        double elapsed = Stopwatch.GetElapsedTime(begun).TotalMilliseconds;
        long allocationBytes = GC.GetAllocatedBytesForCurrentThread() - bytesStart;
        long retainedBytes = GC.GetTotalMemory(true) - baseline;
        units.Sort();
        return new { cold, warm, valid, eightScans, onePass, storm, catalogueStorm,
            backlog = new { shops = 1024, units = units.Count, elapsedMs = elapsed, shopsPerSecond = 1024 * 1000 / elapsed,
                p50Ms = units[units.Count / 2], p95Ms = units[(int)(units.Count * .95)], maxMs = units[^1],
                spikesAtLeast30Ms = units.Count(t => t >= 30), allocationBytes, retainedBytes, pending = backlog.PendingShops },
            nativeSetups = 0, nativeRoutes = 0, wholeFrame = "not measured offline" };
    }
}
