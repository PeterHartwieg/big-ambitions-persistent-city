using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Runtime;
using Xunit;

namespace PersistentCity.Tests;

public sealed class FoodContractStampTests
{
    public sealed class Price { public string itemName = ""; public float price; }
    public sealed class Cargo { public string itemName = ""; public int amount; }
    public sealed class Instance { public string itemName = ""; public List<Cargo> cargoInstances = new List<Cargo>(); }

    public sealed class MonoDictionary<TKey, TValue> where TKey : notnull
    {
        private readonly Dictionary<TKey, TValue> inner = new Dictionary<TKey, TValue>();
        private int _version;
        public int Count => inner.Count;
        public void Add(TKey key, TValue value) { _version++; inner.Add(key, value); }
        public TValue this[TKey key] { get => inner[key]; set { _version++; inner[key] = value; } }
        public bool Remove(TKey key) { if (!inner.Remove(key)) return false; _version++; return true; }
        public void Clear() { _version++; inner.Clear(); }
        public int Version => _version;
    }

    public sealed class Registration
    {
        public List<string>? cachedAvailableProducts = new List<string> { "ba:itemname_burger", "ba:itemname_hotdog" };
        public List<Price>? retailPrices = new List<Price>
        { new Price { itemName = "ba:itemname_burger", price = 9 }, new Price { itemName = "ba:itemname_hotdog", price = 7 } };
        public MonoDictionary<string, Instance>? itemInstances = new MonoDictionary<string, Instance>();
        public string businessTypeName = "ba:businesstype_fastfoodrestaurant";
        public bool RentedByPlayer = true, AvailableForRent, temporarilyClosed;
        public string BusinessName = "Mine";
        public bool HasEstablishedBusiness => !string.IsNullOrEmpty(BusinessName);
        public Registration()
        {
            itemInstances.Add("shelf", new Instance { itemName = "ba:itemname_showcase",
                cargoInstances = new List<Cargo> { new Cargo { itemName = "ba:itemname_burger", amount = 10 } } });
            itemInstances.Add("counter", new Instance { itemName = "ba:itemname_counter" });
        }
    }

    internal static readonly CollectionVersionReader<List<string>> Menu = Bound<List<string>>();
    internal static readonly CollectionVersionReader<List<Price>> Prices = Bound<List<Price>>();
    internal static readonly CollectionVersionReader<MonoDictionary<string, Instance>> Furniture = Bound<MonoDictionary<string, Instance>>();
    internal static readonly CollectionVersionReader<List<Cargo>> CargoList = Bound<List<Cargo>>();
    private static CollectionVersionReader<T> Bound<T>() where T : class
        => CollectionVersionReader<T>.Bind(out var failure) ?? throw new InvalidOperationException(failure);

    internal static ShopContractStamp Stamp(Registration r) => new ShopContractStamp(
        CollectionStamp.Of(Menu, r.cachedAvailableProducts, r.cachedAvailableProducts?.Count ?? 0),
        CollectionStamp.Of(Prices, r.retailPrices, r.retailPrices?.Count ?? 0),
        CollectionStamp.Of(Furniture, r.itemInstances, r.itemInstances?.Count ?? 0),
        r.businessTypeName, r.RentedByPlayer, r.HasEstablishedBusiness, r.AvailableForRent, r.temporarilyClosed);
    private static CollectionStamp Holder(Instance i) => CollectionStamp.Of(CargoList, i.cargoInstances, i.cargoInstances?.Count ?? 0);


    private sealed class NoVersion { public int Count = 0; }
    private sealed class RenamedVersion { private int _ver; public void Touch() => _ver++; }
    private sealed class LongVersion { private long _version; public void Touch() => _version++; }
    private sealed class StaticVersion { private static int _version; public static void Touch() => _version++; }
    private class BaseWithVersion { protected int _version; }
    private sealed class DerivedCollection : BaseWithVersion { public void Add() => _version++; }

    [Fact]
    public void ReaderBindsTheRealListVersionAndReadsWithoutReflection()
    {
        var list = new List<string>();
        int before = Menu.Read(list);
        list.Add("a"); list[0] = "b"; list.Insert(0, "c"); list.RemoveAt(1);
        Assert.Equal(before + 4, Menu.Read(list));
        Assert.Equal(typeof(int), Menu.Field.FieldType);
        Assert.Equal("_version", Menu.Field.Name);
    }

    [Fact]
    public void BindingFailsClosedWhenTheFieldIsMissingRenamedStaticOrNotInt()
    {
        Assert.Null(CollectionVersionReader<NoVersion>.Bind(out var missing));
        Assert.Contains("is missing", missing);
        Assert.Null(CollectionVersionReader<RenamedVersion>.Bind(out var renamed));
        Assert.Contains("_version is missing", renamed);
        Assert.Null(CollectionVersionReader<LongVersion>.Bind(out var wide));
        Assert.Contains("not Int32", wide);
        Assert.Null(CollectionVersionReader<StaticVersion>.Bind(out var shared));
        Assert.Contains("is missing", shared);
        var derived = CollectionVersionReader<DerivedCollection>.Bind(out _);
        Assert.NotNull(derived);
        var c = new DerivedCollection(); c.Add();
        Assert.Equal(1, derived!.Read(c));
    }


    private sealed class Probe
    {
        private int _version;
        public bool MoveOnAdd = true, MoveOnSet = true, MoveOnRemove = true;
        public void Add() { if (MoveOnAdd) _version++; }
        public void Set() { if (MoveOnSet) _version++; }
        public void Remove() { if (MoveOnRemove) _version++; }
    }
    private static string? ProbeTest(Probe probe)
        => FoodContractSelfTest.Check(CollectionVersionReader<Probe>.Bind(out _), () => probe, p => p.Add(), p => p.Set(), p => p.Remove());

    [Fact]
    public void SelfTestPassesOnlyWhenAddSetAndRemoveEachMoveTheVersion()
    {
        Assert.Null(FoodContractSelfTest.List(Menu, "a", "b"));
        Assert.Null(FoodContractSelfTest.List<Price>(Prices, null!, null!));
        Assert.Null(FoodContractSelfTest.Check(Furniture, () => new MonoDictionary<string, Instance>(),
            d => d.Add("k", new Instance()), d => d["k"] = new Instance(), d => d.Remove("k")));
        Assert.Null(ProbeTest(new Probe()));
        Assert.Contains("did not move on add", ProbeTest(new Probe { MoveOnAdd = false }));
        Assert.Contains("did not move on indexer set", ProbeTest(new Probe { MoveOnSet = false }));
        Assert.Contains("did not move on remove", ProbeTest(new Probe { MoveOnRemove = false }));
    }

    [Fact]
    public void SelfTestFailsClosedWhenTheReaderIsUnboundOrTheCollectionThrows()
    {
        Assert.Contains("not bound", FoodContractSelfTest.Check<NoVersion>(null, () => new NoVersion(), _ => { }, _ => { }, _ => { }));
        Assert.Contains("not bound", FoodContractSelfTest.List<string>(null, "a", "b"));
        Assert.Contains("threw InvalidOperationException", FoodContractSelfTest.Check(Menu, () => new List<string>(),
            l => throw new InvalidOperationException(), l => { }, l => { }));
    }

    [Fact]
    public void CapabilityReportPassesOnlyWhenEveryRecordedCapabilityPassed()
    {
        var report = new FoodContractCapabilities();
        Assert.False(report.AllPassed);
        Assert.Equal("no capability checked", report.FirstFailure);
        Assert.True(report.Record("version reader", null));
        Assert.True(report.AllPassed);
        Assert.False(report.Record("hook X.Y()", "Unverified native lifecycle member"));
        Assert.False(report.AllPassed);
        Assert.Equal("hook X.Y(): Unverified native lifecycle member", report.FirstFailure);
        Assert.True(report.Passed("version reader"));
        Assert.False(report.Passed("hook X.Y()"));
        Assert.False(report.Passed("never recorded"));
    }


    public static IEnumerable<object[]> ShopWrites() => new (string Writer, Action<Registration> Write)[]
    {
        ("W3 FillUp removes an emptied container", r => r.itemInstances!.Remove("counter")),
        ("W8 UpdateCachedAvailableProducts assigns a new menu list", r => r.cachedAvailableProducts = new List<string>(r.cachedAvailableProducts!)),
        ("W8 cinema/theater Insert into the menu", r => r.cachedAvailableProducts!.Insert(0, "ba:itemname_popcorn")),
        ("W8 menu entry replaced by indexer", r => r.cachedAvailableProducts![1] = "ba:itemname_croissant"),
        ("W8 RemoveUnusedRetailPrices RemoveAt", r => r.retailPrices!.RemoveAt(1)),
        ("W9 AddItemInstanceToBuilding", r => r.itemInstances!.Add("new", new Instance())),
        ("W9 RemoveItemInstanceFromBuilding", r => r.itemInstances!.Remove("shelf")),
        ("W9 furniture replaced under its key", r => r.itemInstances!["shelf"] = new Instance()),
        ("W9 Reset assigns new collections", r => { r.itemInstances = new MonoDictionary<string, Instance>(); r.retailPrices = new List<Price>(); r.cachedAvailableProducts = new List<string>(); }),
        ("W9 Reset nulls the menu", r => r.cachedAvailableProducts = null),
        ("W9 OnBusinessTypeChanged type name", r => r.businessTypeName = "ba:businesstype_bakery"),
        ("W9 TemporarilyClose", r => r.temporarilyClosed = true),
        ("W9 rent ended", r => r.RentedByPlayer = false),
        ("W9 business name cleared", r => r.BusinessName = ""),
        ("W9 offered for rent", r => r.AvailableForRent = true),
        ("W10 InventoryProductsScrollerController adds a price row", r => r.retailPrices!.Add(new Price { itemName = "ba:itemname_croissant", price = 3 })),
        ("W10 BizManTransfer assigns a copied price list", r => r.retailPrices = new List<Price>(r.retailPrices!)),
        ("W10 price row replaced by indexer", r => r.retailPrices![0] = new Price { itemName = "ba:itemname_burger", price = 9 }),
        ("W10 remove and re-add keeps the count", r => { var row = r.retailPrices![0]; r.retailPrices.RemoveAt(0); r.retailPrices.Add(row); }),
    }.Select(w => new object[] { w.Writer, w.Write });

    [Theory]
    [MemberData(nameof(ShopWrites))]
    public void ShopStampChangesOnEachCollectionOperation(string writer, Action<Registration> write)
    {
        var r = new Registration();
        var before = Stamp(r);
        Assert.True(before.Matches(Stamp(r)), writer);
        write(r);
        Assert.False(before.Matches(Stamp(r)), writer);
    }

    public static IEnumerable<object[]> HolderWrites() => new (string Writer, Action<Instance> Write)[]
    {
        ("W4 DeliverCargoToBuilding AddToCargo of a new slot", i => i.cargoInstances.Add(new Cargo { itemName = "ba:itemname_burger", amount = 4 })),
        ("W5 AddToCargo at an index", i => i.cargoInstances.Insert(0, new Cargo())),
        ("W5 RemoveFromCargo", i => i.cargoInstances.RemoveAt(0)),
        ("W5 ReduceFromCargo to zero removes the slot", i => { i.cargoInstances[0].amount = 0; i.cargoInstances.RemoveAt(0); }),
        ("W5 slot replaced by indexer", i => i.cargoInstances[0] = new Cargo { itemName = "ba:itemname_burger", amount = 10 }),
        ("W6 ParseIntoItemInstance assigns a new cargo list", i => i.cargoInstances = new List<Cargo>(i.cargoInstances)),
    }.Select(w => new object[] { w.Writer, w.Write });

    [Theory]
    [MemberData(nameof(HolderWrites))]
    public void HolderCargoStampChangesOnEachCollectionOperation(string writer, Action<Instance> write)
    {
        var holder = new Registration().itemInstances!["shelf"];
        var before = Holder(holder);
        Assert.True(before.Matches(Holder(holder)), writer);
        write(holder);
        Assert.False(before.Matches(Holder(holder)), writer);
    }

    public static IEnumerable<object[]> InPlaceWrites() => new (string Writer, Action<Registration> Write)[]
    {
        ("W1 SubtractFromStock amount", r => r.itemInstances!["shelf"].cargoInstances[0].amount -= 1),
        ("W2 MergeAmount amount", r => r.itemInstances!["shelf"].cargoInstances[0].amount += 5),
        ("W7 in-place stock itemName", r => r.itemInstances!["shelf"].cargoInstances[0].itemName = "ba:itemname_hotdog"),
        ("W7 in-place holder itemName", r => r.itemInstances!["shelf"].itemName = "ba:itemname_other"),
        ("W10 in-place RetailPrice.price", r => r.retailPrices![0].price = 0),
        ("W10 in-place RetailPrice.itemName", r => r.retailPrices![0].itemName = "ba:itemname_croissant"),
    }.Select(w => new object[] { w.Writer, w.Write });

    /// <summary>The known limit: element field writes move no collection stamp. E5b3's recheck owns them.</summary>
    [Theory]
    [MemberData(nameof(InPlaceWrites))]
    public void StampsDoNotMoveOnInPlaceElementWrites(string writer, Action<Registration> write)
    {
        var r = new Registration();
        var shop = Stamp(r); var holder = Holder(r.itemInstances!["shelf"]);
        write(r);
        Assert.True(shop.Matches(Stamp(r)), writer);
        Assert.True(holder.Matches(Holder(r.itemInstances!["shelf"])), writer);
    }

    [Fact]
    public void UnrecordedStampNeverMatchesAndTypeNameComparesByReference()
    {
        var r = new Registration();
        var stamp = Stamp(r);
        Assert.False(default(ShopContractStamp).Matches(default));
        Assert.False(stamp.Matches(default));
        Assert.False(default(ShopContractStamp).Matches(stamp));
        r.businessTypeName = new string(r.businessTypeName.ToCharArray());
        Assert.False(stamp.Matches(Stamp(r)));
        Assert.Equal("unrecorded", default(ShopContractStamp).ToString());
    }

    [Fact]
    public void StampReadAndCompareAllocateNothing()
    {
        var r = new Registration();
        var recorded = Stamp(r);
        int matches = 0;
        for (int i = 0; i < 100; i++) if (Stamp(r).Matches(recorded)) matches++;
        long allocated = long.MaxValue;
        for (int round = 0; round < 5; round++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10_000; i++) if (Stamp(r).Matches(recorded)) matches++;
            allocated = Math.Min(allocated, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        Assert.Equal(0, allocated);
        Assert.Equal(50_100, matches);
    }


    internal static LoadedStockDefinition Definition(string name)
        => new LoadedStockDefinition(name, true, false, false, false, true, true, false, false, false, null, null);

    [Fact]
    public void NameImageEqualsTheCatalogueNamesAndIsInvisibleUntilPublished()
    {
        var builder = new LoadedNameImageBuilder(7);
        string[] names = { "ba:itemname_burger", "ba:itemname_hotdog", "streetfood:itemname_dumplings", "ba:itemname_paperbag" };
        foreach (var n in names) Assert.True(builder.Add(Definition(n)));
        var image = builder.Publish();
        Assert.Equal(7, image.Revision);
        Assert.Equal(FoodEligibilityRefusal.None, image.Refusal);
        Assert.Equal(names, image.Names);
        Assert.Equal(builder.Catalogue.Count, image.Count);
        foreach (var n in image.Names) Assert.True(builder.Catalogue.TryGet(n, out _));
        Assert.False(image.Contains("BA:ITEMNAME_BURGER"));
        Assert.False(image.Contains(null));
        Assert.Throws<NotSupportedException>(() => ((IList<string>)image.Names).Add("x"));
    }

    [Fact]
    public void RefusedCatalogueAdmitsNoName()
    {
        var builder = new LoadedNameImageBuilder(3);
        Assert.True(builder.Add(Definition("ba:itemname_burger")));
        Assert.False(builder.Add(Definition("ba:itemname_burger")));
        var image = builder.Publish();
        Assert.Equal(FoodEligibilityRefusal.CatalogueIdentity, image.Refusal);
        Assert.Equal(0, image.Count);
        Assert.False(image.Contains("ba:itemname_burger"));
        var bounded = new LoadedNameImageBuilder(4);
        Assert.True(bounded.Add(Definition("ba:itemname_burger")));
        var refused = bounded.Publish(FoodEligibilityRefusal.CatalogueBounds);
        Assert.Equal(FoodEligibilityRefusal.CatalogueBounds, refused.Refusal);
        Assert.False(refused.Contains("ba:itemname_burger"));
    }


    internal static object Stats(List<double> values)
    {
        var s = values.OrderBy(v => v).ToList();
        return new { count = s.Count, p50 = Math.Round(s[s.Count / 2], 6), p95 = Math.Round(s[Math.Min(s.Count - 1, (int)Math.Ceiling(s.Count * .95) - 1)], 6),
            max = Math.Round(s[^1], 6), atLeast30Ms = s.Count(v => v >= 30) };
    }
    internal static void Write(string arm, object result)
    {
        string? path = PerfGate.Variable("PC_E5B2_MEASURE");
        if (!string.IsNullOrEmpty(path)) File.AppendAllText(path, JsonConvert.SerializeObject(new { arm, result }) + "\n");
    }

    [Fact]
    public void MeasureStampReadPerDiscoveryVisit()
    {
        if (string.IsNullOrEmpty(PerfGate.Variable("PC_E5B2_MEASURE"))) return;
        var r = new Registration();
        var recorded = Stamp(r);
        const int warm = 1_000, samples = 100_000;
        var ms = new List<double>(samples);
        double tick = 1000.0 / Stopwatch.Frequency;
        int matched = 0;
        for (int i = 0; i < warm; i++) if (Stamp(r).Matches(recorded)) matched++;
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < samples; i++)
        {
            long t = Stopwatch.GetTimestamp();
            if (Stamp(r).Matches(recorded)) matched++;
            ms.Add((Stopwatch.GetTimestamp() - t) * tick);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
        Write("stamp read + compare per visit", new { stats = Stats(ms), bytesPerCall = (double)allocated / samples, matched });
    }
}
