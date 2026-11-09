using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PersistentCity.Core;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

/// <summary>E5b3 Decision 5: native effective producers and counters against the image's validated holders, after the
/// setup the supply read already made (docs/plans/e5b-remaining.md, Decision 5 and the E5b3 row).</summary>
public sealed class FoodHolderReconciliationTests
{
    private readonly ITestOutputHelper output;
    public FoodHolderReconciliationTests(ITestOutputHelper output) { this.output = output; }
    private const string Burger = ContractWorld.Burger, HotDog = ContractWorld.HotDog;

    private static (ContractWorld World, ContractWorld.Shop Shop) Owned()
    {
        var world = new ContractWorld();
        var shop = world.AddShop("address:main:1:shop", Burger, HotDog);
        world.Own(shop);
        return (world, shop);
    }
    private static FoodReconciliationRefusal Check(ContractWorld world, ContractWorld.Shop shop)
        => FoodHolderReconciliation.Check(world.Coordinator.Serving(shop.Key)!, world.NativeSetup(shop), shop.Menu, s => ((ContractWorld.Cargo)s).amount);

    [Fact]
    public void AValidatedShowcaseLayoutPassesWithNativeProducersASubsetOfImageHolders()
    {
        var (world, shop) = Owned();
        Assert.Equal(FoodReconciliationRefusal.None, Check(world, shop));
        var native = world.NativeSetup(shop);
        var image = world.Coordinator.Serving(shop.Key)!;
        Assert.All(native, n => Assert.True(image.HasHolder(n.Instance)));
        Assert.NotNull(world.Select(shop));
    }

    // Each case withdraws the whole business: the capture is refused, the contract recorded and desire withdrawn.
    public static IEnumerable<object[]> Mismatches() => new[]
    {
        new object[] { "workstation", FoodReconciliationRefusal.Workstation },
        new object[] { "unvalidated-shelf", FoodReconciliationRefusal.UnvalidatedProducer },
        new object[] { "counter-not-in-image", FoodReconciliationRefusal.NativeCounterNotInImage },
        new object[] { "omitted-positive-holder", FoodReconciliationRefusal.OmittedPositiveHolder },
        new object[] { "wrong-product", FoodReconciliationRefusal.UnvalidatedProducer },
    };

    [Theory]
    [MemberData(nameof(Mismatches))]
    public void EachMismatchWithdrawsTheWholeShop(string mismatch, FoodReconciliationRefusal expected)
    {
        var (world, shop) = Owned();
        var burgerShelf = shop.Holder(shop.Key + ":shelf:" + Burger);
        switch (mismatch)
        {
            // Native indexes every EmployeeWorkstation's itemsToProduce; this one is outside the image.
            case "workstation": world.ExtraNative.Add(new FoodNativeHolder(new ContractWorld.Holder { id = "grill", itemName = ContractWorld.StationItem }, Burger, true, false)); break;
            // A shelf the image never validated (here a storage unit) indexed as a producer.
            case "unvalidated-shelf": world.ExtraNative.Add(new FoodNativeHolder(new ContractWorld.Holder { id = "storage" }, HotDog, false, false)); break;
            case "counter-not-in-image": world.ExtraNative.Add(new FoodNativeHolder(new ContractWorld.Holder { id = "till" }, null, false, true)); break;
            case "omitted-positive-holder": world.OmittedByNative.Add(burgerShelf.id); break;
            case "wrong-product": world.ExtraNative.Add(new FoodNativeHolder(burgerShelf, HotDog, false, false)); break;
        }
        Assert.Equal(expected, Check(world, shop));
        Assert.Null(world.Select(shop));
        Assert.Equal(expected, world.LastReconciliation);
        Assert.True(world.Refused.ContainsKey(shop.Key));
        Assert.False(shop.Row.Desired);
        Assert.Empty(world.Orders);
        Assert.Contains(world.Boundary(world.Hour + 1), o => o.Key == shop.Key && o.Outcome == FoodContractBoundary.Outcome.HandedBack);
    }

    [Fact]
    public void AnImageHolderIndexedAsAWorkstationIsRefusedEvenWhenItsCargoMatches()
    {
        var (world, shop) = Owned();
        var shelf = shop.Holder(shop.Key + ":shelf:" + Burger);
        world.ExtraNative.Add(new FoodNativeHolder(shelf, Burger, true, false));
        Assert.Equal(FoodReconciliationRefusal.Workstation, Check(world, shop));
    }

    [Fact]
    public void ARemovedCounterWithdrawsTheWholeShopBeforeAnySupplyRead()
    {
        var (world, shop) = Owned();
        var capture = world.Select(shop)!;
        shop.Holders.Remove(shop.Key + ":counter");
        Assert.Null(world.Checkout(capture, Burger, "intent"));
        Assert.False(shop.Row.Desired);
        world.VisitAll(); world.Drain();
        Assert.Equal(FoodEligibilityRefusal.NoCounter, world.Coordinator.Image(shop.Key)!.Eligibility.Refusal);
    }

    [Fact]
    public void KnownZeroStaysZeroAndUnknownStaysUnknown()
    {
        var (world, shop) = Owned();
        var shelf = shop.Holder(shop.Key + ":shelf:" + Burger);
        shelf.cargoInstances[0].amount = 0; // retained same-ID slot, amount 0: native leaves it out of the index
        Assert.Equal(FoodReconciliationRefusal.None, Check(world, shop));
        Assert.True(world.Supply(shop, out var units));
        Assert.Equal(0, units[Burger]);   // known zero
        Assert.Equal(10, units[HotDog]);
        Assert.NotNull(world.Select(shop));
        // The same holder with stock that native setup omits is unknown, never zero.
        shelf.cargoInstances[0].amount = 4;
        world.OmittedByNative.Add(shelf.id);
        Assert.False(world.Supply(shop, out _));
        Assert.Equal(FoodReconciliationRefusal.OmittedPositiveHolder, world.LastReconciliation);
    }

    [Fact]
    public void AnUnstaffedCounterIsNotAMismatch()
    {
        var (world, shop) = Owned();
        world.UnstaffedCounters.Add(shop.Key + ":counter");
        Assert.Equal(FoodReconciliationRefusal.None, Check(world, shop));
    }

    [Fact]
    public void ReconciliationAllocatesNothingAndStaysBounded()
    {
        string line = "";
        TimingBudget.Run(timing =>
        {
            var world = new ContractWorld { MenuLimit = LoadedStockLimits.Offers };
            var shop = world.AddShop("address:main:1:shop", Burger, HotDog);
            for (int i = 0; i < 125; i++)
                shop.Holders.Add(new ContractWorld.Holder { id = "extra" + i.ToString("000"), itemName = ContractWorld.ShelfItem,
                    cargoInstances = new List<ContractWorld.Cargo> { new ContractWorld.Cargo { itemName = i % 2 == 0 ? Burger : HotDog, amount = 1 } } });
            world.Own(shop);
            var image = world.Coordinator.Serving(shop.Key)!;
            Assert.Equal(128, image.Holders.Count);
            var native = world.NativeSetup(shop);
            IReadOnlyList<string> menu = shop.Menu;
            Func<object, int> amount = s => ((ContractWorld.Cargo)s).amount;
            for (int i = 0; i < 100; i++) FoodHolderReconciliation.Check(image, native, menu, amount);
            var samples = new double[1000];
            int failures = 0;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < samples.Length; i++)
            {
                long t = Stopwatch.GetTimestamp();
                if (FoodHolderReconciliation.Check(image, native, menu, amount) != FoodReconciliationRefusal.None) failures++;
                samples[i] = (Stopwatch.GetTimestamp() - t) * 1000.0 / Stopwatch.Frequency;
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(0, failures);
            Assert.Equal(0, allocated);
            Array.Sort(samples);
            line = $"reconciliation 128 holders: p50 {samples[500]:0.0000} ms, p95 {samples[949]:0.0000} ms, max {samples[^1]:0.0000} ms, 0 B/call";
            timing.Under("reconciliation p95", samples[949], 1.0);
        });
        output.WriteLine(line);
    }
}
