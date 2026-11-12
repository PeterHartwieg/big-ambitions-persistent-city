using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using PersistentCity.Core;
using PersistentCity.Runtime;
using Xunit;

namespace PersistentCity.Tests;

[Collection("FoodContractHooks")]
public sealed class FoodContractHookTests
{
    private sealed class Unit : IContractDirtyUnit
    {
        public Unit(string key) { Key = key; }
        public string Key { get; }
        public bool Queued { get; set; }
    }
    private sealed class ReferenceKey : IEqualityComparer<object>
    {
        public new bool Equals(object? a, object? b) => ReferenceEquals(a, b);
        public int GetHashCode(object o) => RuntimeHelpers.GetHashCode(o);
    }

    private sealed class Sink : IFoodContractHintSink
    {
        public readonly LoadedCatalogueRevision Revision = new LoadedCatalogueRevision();
        public readonly ContractDirtyQueue<Unit> Queue = new ContractDirtyQueue<Unit>();
        public readonly List<Unit> Units = new List<Unit>();
        public readonly Dictionary<object, Unit> ByRegistration = new Dictionary<object, Unit>(new ReferenceKey());
        public readonly Dictionary<(string, int), Unit> ByAddress = new Dictionary<(string, int), Unit>();
        public int CatalogueCalls;
        public bool Throw;
        public Unit Track(string key, object registration, string street, int number)
        {
            var unit = new Unit(key); Units.Add(unit); ByRegistration[registration] = unit; ByAddress[(street, number)] = unit; return unit;
        }
        public FoodContractHint Catalogue()
        {
            if (Throw) throw new InvalidOperationException("sink");
            CatalogueCalls++;
            if (!Revision.MarkDirty()) return FoodContractHint.Coalesced;
            foreach (var unit in Units) Queue.Enqueue(unit);
            return FoodContractHint.NewUnit;
        }
        public FoodContractHint Registration(object registration)
        {
            if (Throw) throw new InvalidOperationException("sink");
            return ByRegistration.TryGetValue(registration, out var unit) ? Mark(unit) : FoodContractHint.Unknown;
        }
        public FoodContractHint Holder(string street, int number)
            => ByAddress.TryGetValue((street, number), out var unit) ? Mark(unit) : FoodContractHint.Unknown;
        public FoodContractHint AllShops()
        {
            bool queued = false;
            foreach (var unit in Units) queued |= Queue.Enqueue(unit);
            return queued ? FoodContractHint.NewUnit : FoodContractHint.Coalesced;
        }
        private FoodContractHint Mark(Unit unit) => Queue.Enqueue(unit) ? FoodContractHint.NewUnit : FoodContractHint.Coalesced;
        public List<string> Drain()
        {
            var taken = new List<string>();
            while (Queue.TryDequeue(out var unit)) taken.Add(unit.Key);
            return taken;
        }
        public void Publish(params string[] names)
        {
            var building = Revision.Begin();
            foreach (var n in names) Assert.True(building.Add(FoodContractStampTests.Definition(n)));
            Revision.Publish();
        }
    }

    private sealed class Patcher : IFoodContractPatcher
    {
        public readonly HashSet<int> Patched = new HashSet<int>();
        public readonly HashSet<int> Fail = new HashSet<int>();
        public int Unpatches;
        public string? Patch(FoodContractHook hook)
        {
            if (Fail.Contains(hook.Id)) return "Unverified native lifecycle member: " + hook.Signature;
            Patched.Add(hook.Id); return null;
        }
        public void UnpatchAll() { Patched.Clear(); Unpatches++; }
    }

    private static FoodContractHintRouter Install(Sink sink, Patcher? patcher = null)
    {
        FoodContractHintRouter.Active?.Dispose();
        return FoodContractHintRouter.Install(sink, patcher ?? new Patcher(), Environment.CurrentManagedThreadId);
    }


    private static readonly string[] PinnedSignatures =
    {
        "W11 Catalogue static ItemsGetter.OnItemsLoaded(IList`1)",
        "W11 Catalogue static ItemsGetter.RegisterModItem(Item)",
        "W11 Catalogue static ItemsGetter.UnregisterModItem(String)",
        "W11 Catalogue static ItemsGetter.ResetStaticData()",
        "W8 Registration static BusinessHelper.UpdateCachedAvailableProducts(BuildingRegistration)",
        "W9 Registration BuildingRegistration.AddItemInstanceToBuilding(ItemInstance)",
        "W9 Registration BuildingRegistration.RemoveItemInstanceFromBuilding(ItemInstance, Boolean)",
        "W9 Registration BuildingRegistration.OnBusinessTypeChanged()",
        "W9 Registration BuildingRegistration.Reset(Boolean)",
        "W3 Quantity static ItemHelper.FillUpShowcaseShelfOrPointOfSale(ItemInstance)",
        "W1 Quantity static ItemHelper.SubtractFromStock(ItemInstance, Int32)",
        "W5 Holder ItemInstance.AddToCargo(CargoInstance, Int32)",
        "W5 Holder ItemInstance.RemoveFromCargo(CargoInstance)",
        "W5 Quantity ItemInstance.ReduceFromCargo(CargoInstance, Int32)",
        "W5 Quantity ItemInstance.MergeCargo(CargoInstance, CargoInstance)",
        "W5 Quantity ItemInstance.MergeCargo(CargoInstance, CargoInstance, Int32)",
        "W2 Quantity CargoInstance.MergeAmount(CargoInstance, Int32)",
        "W2 Quantity static ReStockingHelper.RefillSingleItemWithLimit(ItemInstance)",
        "W2 Quantity static ReStockingHelper.TryAddStockAmount(ItemInstance, CargoInstance, Int32)",
        "W6 Holder CargoInstance.ParseIntoItemInstance(ItemInstance)",
        "W9 Registration BuildingRegistration.TemporarilyClose(Boolean)",
        "W8 Registration BuildingRegistration.RemoveUnusedRetailPrices()",
        "W7 Unattributed CargoInstance.ResetItemCached()",
    };

    [Fact]
    public void WriterInventoryPinsEveryBoundMemberSignature()
    {
        var table = FoodContractHookTable.All;
        Assert.Equal(PinnedSignatures, table.Select(h => h.Writer + " " + h.Kind + " " + h.Signature));
        Assert.Equal(Enumerable.Range(0, table.Count), table.Select(h => h.Id));
        Assert.Equal(19, FoodContractHookTable.E5aMembers);
        Assert.Equal(table.Count, table.Select(h => h.Signature).Distinct().Count());
    }

    private static string Root([CallerFilePath] string path = "") => Path.GetDirectoryName(Path.GetDirectoryName(path)!)!;


    [Fact]
    public void OneDirtyUnitPerShopUnderATenThousandCallStorm()
    {
        var sink = new Sink(); sink.Publish("ba:itemname_burger");
        object shopA = new object(), shopB = new object();
        sink.Track("A", shopA, "Main Street", 5); sink.Track("B", shopB, "Side Street", 9);
        using var router = Install(sink);
        for (int i = 0; i < 10_000; i++)
        {
            FoodContractHintRouter.Registration(5, shopA);
            FoodContractHintRouter.Registration(4, shopA);
            FoodContractHintRouter.Holder(11, "Side Street", 9);
            FoodContractHintRouter.Holder(12, "Side Street", 9);
        }
        Assert.Equal(2, sink.Queue.Count);
        Assert.Equal(10_000, router.Calls(5));
        Assert.Equal(1, router.NewUnits(5) + router.NewUnits(4));
        Assert.Equal(19_999, router.CoalescedHints(5) + router.CoalescedHints(4));
        Assert.Equal(1, router.NewUnits(11) + router.NewUnits(12));
        Assert.Equal(new[] { "A", "B" }, sink.Drain());
        FoodContractHintRouter.Registration(20, shopA);
        Assert.Equal(new[] { "A" }, sink.Drain());
        Assert.Equal(0, sink.Revision.Bumps);
    }

    [Fact]
    public void PureQuantityWritesNeverHint()
    {
        var sink = new Sink(); sink.Publish("ba:itemname_burger");
        object shop = new object(); sink.Track("A", shop, "Main Street", 5);
        long revision = sink.Revision.Revision;
        using var router = Install(sink);
        foreach (var hook in FoodContractHookTable.All.Where(h => h.Kind is FoodContractHookKind.Quantity or FoodContractHookKind.Unattributed))
            for (int i = 0; i < 10_000; i++)
            {
                if (hook.Kind == FoodContractHookKind.Quantity) FoodContractHintRouter.Quantity(hook.Id);
                else FoodContractHintRouter.Unattributed(hook.Id);
            }
        Assert.Equal(0, sink.Queue.Count);
        Assert.Equal(revision, sink.Revision.Revision);
        Assert.NotNull(sink.Revision.Names);
        Assert.Equal(10_000, router.Calls(10));
        Assert.Equal(0, router.TotalNewUnits);
        Assert.Equal(8, FoodContractHookTable.All.Count(h => h.Kind == FoodContractHookKind.Quantity));
    }

    [Fact]
    public void UnknownOwnersAreCountedAndIgnored()
    {
        var sink = new Sink();
        using var router = Install(sink);
        FoodContractHintRouter.Registration(5, new object());
        FoodContractHintRouter.Registration(5, null);
        FoodContractHintRouter.Holder(11, "Nowhere", 1);
        FoodContractHintRouter.Holder(19, null, 0);
        Assert.Equal(2, router.UnknownHints(5));
        Assert.Equal(1, router.UnknownHints(11));
        Assert.Equal(1, router.UnknownHints(19));
        Assert.Equal(0, sink.Queue.Count);
    }

    [Fact]
    public void RoutingAllocatesNothingInSteadyState()
    {
        var sink = new Sink(); sink.Publish("ba:itemname_burger");
        object shop = new object(); sink.Track("A", shop, "Main Street", 5);
        using var router = Install(sink);
        FoodContractHintRouter.Registration(5, shop); FoodContractHintRouter.Holder(11, "Main Street", 5);
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10_000; i++)
        {
            FoodContractHintRouter.Quantity(10);
            FoodContractHintRouter.Registration(5, shop);
            FoodContractHintRouter.Holder(11, "Main Street", 5);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - bytes);
    }


    [Fact]
    public void CatalogueRevisionBumpsOnLoadRegisterUnregisterAndResetAndRepublishesTheNameImage()
    {
        var sink = new Sink();
        object shop = new object(); sink.Track("A", shop, "Main Street", 5);
        using var router = Install(sink);
        string[] vanilla = { "ba:itemname_burger", "ba:itemname_hotdog", "ba:itemname_paperbag" };
        sink.Publish(vanilla);
        AssertImage(sink, vanilla);
        sink.Drain();

        var steps = new (int Hook, string[] Names)[]
        {
            (0, vanilla),
            (1, vanilla.Append("streetfood:itemname_dumplings").ToArray()),
            (2, vanilla),
            (3, new[] { "ba:itemname_burger" }),
        };
        foreach (var step in steps)
        {
            long before = sink.Revision.Revision;
            var previous = sink.Revision.Names!;
            FoodContractHintRouter.Catalogue(step.Hook);
            Assert.Equal(before + 1, sink.Revision.Revision);
            Assert.Null(sink.Revision.Names);
            Assert.Equal(new[] { "A" }, sink.Drain());
            FoodContractHintRouter.Catalogue(step.Hook);
            Assert.Equal(before + 1, sink.Revision.Revision);

            var building = sink.Revision.Begin();
            Assert.True(building.Add(FoodContractStampTests.Definition(step.Names[0])));
            Assert.Null(sink.Revision.Names);
            Assert.Same(previous, sink.Revision.LastPublished);
            foreach (var n in step.Names.Skip(1)) Assert.True(building.Add(FoodContractStampTests.Definition(n)));
            sink.Revision.Publish();
            AssertImage(sink, step.Names);
            Assert.Equal(before + 1, sink.Revision.Names!.Revision);
        }
        Assert.Equal(1, router.NewUnits(0)); Assert.Equal(1, router.CoalescedHints(0));

        FoodContractHintRouter.Catalogue(1);
        sink.Revision.Begin().Add(FoodContractStampTests.Definition("ba:itemname_burger"));
        long mid = sink.Revision.Revision;
        FoodContractHintRouter.Catalogue(1);
        Assert.Equal(mid + 1, sink.Revision.Revision);
        Assert.Null(sink.Revision.Building);
        Assert.Null(sink.Revision.Names);

        sink.Publish(vanilla);
        long published = sink.Revision.Revision;
        sink.Revision.Reset();
        Assert.Equal(published + 1, sink.Revision.Revision);
        Assert.Null(sink.Revision.Names);
        Assert.Null(sink.Revision.LastPublished);
    }

    private static void AssertImage(Sink sink, string[] names)
    {
        var image = sink.Revision.Names!;
        var catalogue = sink.Revision.Catalogue!;
        Assert.Equal(sink.Revision.Revision, image.Revision);
        Assert.Equal(catalogue.Revision, image.Revision);
        Assert.Equal(names, image.Names);
        Assert.Equal(catalogue.Count, image.Count);
        foreach (var n in image.Names) Assert.True(catalogue.TryGet(n, out var definition) && definition.Name == n);
    }


    [Fact]
    public void ReloadAndUnloadLeaveNoHookOrSinkReference()
    {
        FoodContractHintRouter.Active?.Dispose();
        var patcher = new Patcher();
        var first = new Sink(); object shop = new object(); first.Track("A", shop, "Main Street", 5);
        long g0 = FoodContractHintRouter.Generation;
        var router = FoodContractHintRouter.Install(first, patcher, Environment.CurrentManagedThreadId);
        Assert.Same(router, FoodContractHintRouter.Active);
        Assert.True(router.AllInstalled);
        Assert.Equal(FoodContractHookTable.All.Count, patcher.Patched.Count);
        Assert.Equal(FoodContractHookTable.All.Count, router.InstalledCount);
        Assert.Equal(g0 + 1, router.InstalledGeneration);
        Assert.Throws<InvalidOperationException>(() => FoodContractHintRouter.Install(new Sink(), new Patcher(), Environment.CurrentManagedThreadId));

        router.Dispose();
        Assert.Null(FoodContractHintRouter.Active);
        Assert.False(router.Attached);
        Assert.Empty(patcher.Patched);
        Assert.Equal(1, patcher.Unpatches);
        Assert.Equal(0, router.InstalledCount);
        Assert.Equal(g0 + 2, FoodContractHintRouter.Generation);
        FoodContractHintRouter.Registration(5, shop); FoodContractHintRouter.Catalogue(0); FoodContractHintRouter.Quantity(10);
        Assert.Equal(0, router.TotalCalls);
        Assert.Equal(0, first.Queue.Count);
        router.Dispose();
        Assert.Equal(1, patcher.Unpatches);

        var second = new Sink(); object shop2 = new object(); second.Track("B", shop2, "Main Street", 5);
        using var reloaded = FoodContractHintRouter.Install(second, patcher, Environment.CurrentManagedThreadId);
        Assert.Equal(g0 + 3, reloaded.InstalledGeneration);
        FoodContractHintRouter.Holder(11, "Main Street", 5);
        Assert.Equal(0, first.Queue.Count);
        Assert.Equal(1, second.Queue.Count);
        var unit = second.Units[0];
        second.Queue.Clear();
        Assert.False(unit.Queued);
        Assert.Equal(0, second.Queue.Count);
    }

    [Fact]
    public void FailedHookIsReportedAndOthersStillRoute()
    {
        var patcher = new Patcher(); patcher.Fail.Add(7);
        var sink = new Sink(); object shop = new object(); sink.Track("A", shop, "Main Street", 5);
        using var router = Install(sink, patcher);
        Assert.False(router.AllInstalled);
        Assert.Contains("OnBusinessTypeChanged", router.Failure(7));
        Assert.Null(router.Failure(5));
        Assert.Equal(FoodContractHookTable.All.Count - 1, router.InstalledCount);
        FoodContractHintRouter.Registration(5, shop);
        Assert.Equal(1, sink.Queue.Count);
    }

    [Fact]
    public void OffThreadCallsBecomeOneMainThreadHint()
    {
        var sink = new Sink(); sink.Publish("ba:itemname_burger");
        object shop = new object(); sink.Track("A", shop, "Main Street", 5);
        using var router = Install(sink);
        long revision = sink.Revision.Revision;
        var worker = new Thread(() =>
        {
            for (int i = 0; i < 100; i++) { FoodContractHintRouter.Registration(5, shop); FoodContractHintRouter.Quantity(10); }
        });
        worker.Start(); worker.Join();
        Assert.Equal(200, router.OffThreadCalls);
        Assert.Equal(0, router.TotalCalls);
        Assert.Equal(0, sink.Queue.Count);
        router.Drain();
        Assert.Equal(revision, sink.Revision.Revision);
        Assert.NotNull(sink.Revision.Names);
        Assert.Equal(1, sink.Queue.Count);
        router.Drain();
        Assert.Equal(1, sink.Queue.Count);
        var quantity = new Thread(() => FoodContractHintRouter.Quantity(10));
        quantity.Start(); quantity.Join();
        router.Drain();
        Assert.Equal(revision, sink.Revision.Revision);
        var catalogue = new Thread(() => FoodContractHintRouter.Catalogue(1));
        catalogue.Start(); catalogue.Join();
        router.Drain();
        Assert.Equal(revision + 1, sink.Revision.Revision);
        Assert.Null(sink.Revision.Names);
    }

    [Fact]
    public void SinkFaultsNeverReachNativeCode()
    {
        var sink = new Sink { Throw = true };
        using var router = Install(sink);
        FoodContractHintRouter.Catalogue(1);
        FoodContractHintRouter.Registration(5, new object());
        Assert.Equal(2, router.Faults);
        Assert.Contains("InvalidOperationException", router.LastFault);
    }

    [Fact]
    public void MeasurePostfixCost()
    {
        if (string.IsNullOrEmpty(PerfGate.Variable("PC_E5B2_MEASURE"))) return;
        var sink = new Sink(); sink.Publish("ba:itemname_burger");
        object shop = new object(); sink.Track("A", shop, "Main Street", 5);
        using var router = Install(sink);
        double tick = 1000.0 / Stopwatch.Frequency;
        const int warm = 1_000, samples = 100_000;
        void Arm(string name, Action call)
        {
            for (int i = 0; i < warm; i++) call();
            var ms = new List<double>(samples);
            long bytes = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < samples; i++) { long t = Stopwatch.GetTimestamp(); call(); ms.Add((Stopwatch.GetTimestamp() - t) * tick); }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
            FoodContractStampTests.Write(name, new { stats = FoodContractStampTests.Stats(ms), bytesPerCall = (double)allocated / samples });
        }
        Arm("postfix SubtractFromStock (quantity, counted)", () => FoodContractHintRouter.Quantity(10));
        Arm("postfix AddItemInstanceToBuilding (registration, coalesced)", () => FoodContractHintRouter.Registration(5, shop));
        Arm("postfix AddToCargo (holder address, coalesced)", () => FoodContractHintRouter.Holder(11, "Main Street", 5));
    }
}
