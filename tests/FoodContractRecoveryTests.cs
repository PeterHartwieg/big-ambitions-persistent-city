using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

internal static class ContractRecoveryTestPaths
{
    internal static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}

/// <summary>E5b3 Decision 7: dynamic dirty recovery at advancing hours, with a fresh prepared image, after the
/// handoff completed and with no live native customers; a bounded, coalesced recovery queue.</summary>
public sealed class FoodContractRecoveryTests
{
    private const string Burger = ContractWorld.Burger, HotDog = ContractWorld.HotDog;

    private static void BreakStockName(ContractWorld.Shop shop) => shop.Holder(shop.Key + ":shelf:" + Burger).cargoInstances[0].itemName = HotDog;
    private static void RepairStockName(ContractWorld.Shop shop) => shop.Holder(shop.Key + ":shelf:" + Burger).cargoInstances[0].itemName = Burger;

    [Fact]
    public void ARefusedShopIsNeverRecoveredInItsRefusedHour()
    {
        var world = new ContractWorld();
        var shop = world.AddShop("address:main:1:shop", Burger, HotDog);
        world.Own(shop);
        long hour = world.Hour;
        var capture = world.Select(shop)!;
        BreakStockName(shop);
        Assert.Null(world.Checkout(capture, Burger, "a"));
        Assert.Equal(hour, world.Refused[shop.Key]);
        RepairStockName(shop);
        world.Requeue(shop); world.Drain(); world.VisitAll(); // a passing image in the refused hour itself
        Assert.True(world.Coordinator.Image(shop.Key)!.Passed);
        Assert.Equal(FoodContractPhase.Refused, world.Coordinator.Phase(shop.Key));
        Assert.Equal(FoodRecoveryRefusal.SameHour, world.Coordinator.RecoveryGate(shop.Key, hour, true, false));
        Assert.True(world.Refused.ContainsKey(shop.Key));
        Assert.False(shop.Row.Desired);
        Assert.Equal(0, world.RecoveryUnits);
    }

    [Fact]
    public void AnInPlaceRepairAfterHandbackGetsAFreshImageNextHourAndAcquiresOnlyThroughTheGates()
    {
        var world = new ContractWorld();
        var shop = world.AddShop("address:main:1:shop", Burger);
        world.Own(shop);
        // Break the shelf definition in place; no capture is in flight. The next hour's preparation catches it.
        world.Items[ContractWorld.ShelfItem].consumes = false;
        world.Boundary(world.Hour + 1); world.Drain(); world.VisitAll();
        long refused = world.Refused[shop.Key];
        Assert.False(shop.Row.Desired);
        Assert.True(shop.Row.Managed);
        // Repair before the handoff completed: no recovery while PC still owns the shop.
        world.Items[ContractWorld.ShelfItem].consumes = true;
        world.Requeue(shop); world.Drain();
        Assert.Equal(FoodRecoveryRefusal.SameHour, world.Coordinator.RecoveryGate(shop.Key, world.Hour, false, false));
        var handback = world.Boundary(world.Hour + 1);
        Assert.Contains(handback, o => o.Key == shop.Key && o.Outcome == FoodContractBoundary.Outcome.HandedBack);
        // The refused shop is in this hour's preparation queue, so its fresh image comes in this hour.
        world.Drain(); world.VisitAll();
        Assert.False(world.Refused.ContainsKey(shop.Key));
        Assert.True(world.Coordinator.Image(shop.Key)!.BuiltHour > refused);
        Assert.Equal(FoodContractPhase.Published, world.Coordinator.Phase(shop.Key));
        Assert.True(shop.Row.Desired);
        Assert.Equal(world.Hour, shop.Row.RequestedHour);
        Assert.False(shop.Row.Managed); // requested now, acquired only at a later boundary through the commit gate
        int gates = world.GateRuns;
        Assert.Contains(world.Boundary(world.Hour + 1), o => o.Key == shop.Key && o.Outcome == FoodContractBoundary.Outcome.Acquired);
        Assert.Equal(gates + 1, world.GateRuns);
    }

    [Fact]
    public void AnInitiallyFailedShopRecoversAfterAnInPlacePriceRepair()
    {
        var world = new ContractWorld();
        var shop = world.AddShop("address:main:1:shop", Burger);
        shop.PriceOf(Burger).price = 0;
        world.VisitAll(); world.Drain(); world.VisitAll();
        Assert.Equal(FoodEligibilityRefusal.Price, world.Coordinator.Image(shop.Key)!.Eligibility.Refusal);
        Assert.False(shop.Row.Desired);
        Assert.True(world.Refused.ContainsKey(shop.Key)); // recorded although it was never owned or wanted
        shop.PriceOf(Burger).price = 5; // in place: no stamp, no hint
        world.Boundary(world.Hour + 1); world.Drain(); world.VisitAll();
        Assert.True(world.Coordinator.Image(shop.Key)!.Passed);
        Assert.False(world.Refused.ContainsKey(shop.Key));
        Assert.True(shop.Row.Desired);
        Assert.Contains(world.Boundary(world.Hour + 1), o => o.Key == shop.Key && o.Outcome == FoodContractBoundary.Outcome.Acquired);
    }

    [Fact]
    public void ARestoredBagTagRefreshesTheCachedPolicyAndTheShopReacquires()
    {
        var world = new ContractWorld();
        var shop = world.AddShop("address:main:1:shop", Burger);
        world.Own(shop);
        var tags = world.Types[ContractWorld.ShopType].tags;
        tags.Remove(ContractWorld.NeedsBags); // in place
        world.Boundary(world.Hour + 1); world.Drain(); world.VisitAll();
        Assert.True(world.Refused.ContainsKey(shop.Key));
        Assert.False(world.SupportedBusiness(shop)); // the cached policy is now false
        world.Boundary(world.Hour + 1); world.Drain(); world.VisitAll(); // hand-back completes
        Assert.False(shop.Row.Managed);
        tags.Add(ContractWorld.NeedsBags); // restored in place
        world.Boundary(world.Hour + 1); world.Drain(); world.VisitAll();
        Assert.True(world.SupportedBusiness(shop));
        Assert.False(world.Refused.ContainsKey(shop.Key));
        Assert.True(shop.Row.Desired);
        Assert.Contains(world.Boundary(world.Hour + 1), o => o.Key == shop.Key && o.Outcome == FoodContractBoundary.Outcome.Acquired);
    }

    [Fact]
    public void RecoveryWaitsForTheCompletedHandoff()
    {
        var world = new ContractWorld();
        var shop = world.AddShop("address:main:1:shop", Burger);
        world.Own(shop);
        var capture = world.Select(shop)!;
        BreakStockName(shop);
        Assert.Null(world.Checkout(capture, Burger, "a"));
        RepairStockName(shop);
        // Next hour: hand-back and the fresh image happen together; recovery needs the handoff to be complete.
        var gate = world.Coordinator.RecoveryGate(shop.Key, world.Hour + 1, false, false);
        Assert.NotEqual(FoodRecoveryRefusal.None, gate);
        world.Boundary(world.Hour + 1); world.Drain(); world.VisitAll();
        Assert.False(shop.Row.Managed);
        Assert.False(world.Refused.ContainsKey(shop.Key));
    }

    [Fact]
    public void LiveNativeCustomersKeepARepairedShopNative()
    {
        var world = new ContractWorld();
        var shop = world.AddShop("address:main:1:shop", Burger);
        world.Own(shop);
        var capture = world.Select(shop)!;
        BreakStockName(shop);
        Assert.Null(world.Checkout(capture, Burger, "a"));
        world.Boundary(world.Hour + 1); // hand-back
        RepairStockName(shop);
        shop.LiveCustomers = true;
        world.Boundary(world.Hour + 1); world.Drain(); world.VisitAll();
        Assert.True(world.Refused.ContainsKey(shop.Key));
        Assert.Equal(FoodRecoveryRefusal.LiveNativeCustomers, world.Coordinator.RecoveryGate(shop.Key, world.Hour, true, true));
        shop.LiveCustomers = false;
        world.Boundary(world.Hour + 1); world.Drain(); world.VisitAll();
        Assert.False(world.Refused.ContainsKey(shop.Key));
        Assert.True(shop.Row.Desired);
    }

    [Fact]
    public void AnImageBuiltBeforeTheRefusalNeverRecovers()
    {
        var coordinator = new FoodContractCoordinator();
        var world = new ContractWorld();
        var shop = world.AddShop("address:main:1:shop", Burger);
        world.VisitAll(); world.Drain();
        var image = world.Coordinator.Image(shop.Key)!;
        coordinator.Publish(image);
        coordinator.Refuse(shop.Key, image.BuiltHour);
        Assert.Equal(FoodRecoveryRefusal.NoFreshImage, coordinator.RecoveryGate(shop.Key, image.BuiltHour + 1, true, false));
        Assert.False(coordinator.RequeueRecovery(shop.Key));
        Assert.Equal(0, coordinator.PendingRecoveries);
    }

    [Fact]
    public void ARecoveryStormDrainsInBoundedUnitsWithNoDuplicateOrder()
    {
        var world = new ContractWorld();
        var shops = Enumerable.Range(0, 64).Select(i => world.AddShop("address:storm:" + i.ToString("00") + ":shop", Burger, HotDog)).ToArray();
        world.Own(shops);
        var captures = shops.Select(s => world.Select(s)!).ToArray();
        int intent = 0;
        foreach (var c in captures) Assert.NotNull(world.Checkout(c, HotDog, "paid-" + intent++));
        // Break and repair every shop in the same hour; the checkouts in flight are refused before any receipt.
        foreach (var s in shops) BreakStockName(s);
        for (int i = 0; i < captures.Length; i++) Assert.Null(world.Checkout(captures[i], Burger, "broken-" + i));
        foreach (var s in shops) RepairStockName(s);
        // One hand-back hour, then 64 acquisitions at 16 per boundary.
        for (int hour = 0; hour < 6; hour++)
        {
            world.Boundary(world.Hour + 1);
            for (int f = 0; f < 200; f++) { world.Frame(); world.VisitAll(); }
        }
        Assert.True(world.MaxRecoveriesInFrame <= FoodContractCoordinator.RecoveriesPerFrame);
        Assert.Equal(64, world.RecoveryUnits); // one unit per shop, not per hint or frame
        Assert.Empty(world.Refused);
        Assert.All(shops, s => Assert.True(s.Row.CanServe));
        // Replaying every intent sells nothing twice.
        for (int i = 0; i < captures.Length; i++) world.Checkout(world.Select(shops[i])!, HotDog, "paid-" + i);
        Assert.Equal(world.Orders.Count, world.Orders.Distinct().Count());
        Assert.Equal(64, world.Orders.Count);
        Assert.DoesNotContain(world.Orders, o => o.StartsWith("broken-", StringComparison.Ordinal));
    }

    [Fact]
    public void TheRecoveryQueueTakesAtMostSixtyFourUnitsAndEachShopOncePerHour()
    {
        var world = new ContractWorld();
        var coordinator = world.Coordinator;
        var shops = Enumerable.Range(0, 130).Select(i => world.AddShop("address:q:" + i.ToString("000") + ":shop", Burger)).ToArray();
        world.VisitAll(); world.Drain();
        foreach (var s in shops) coordinator.Refuse(s.Key, world.Hour - 1);
        foreach (var s in shops) { coordinator.RequeueRecovery(s.Key); coordinator.RequeueRecovery(s.Key); }
        Assert.Equal(130, coordinator.PendingRecoveries);
        var due = new List<string>();
        Assert.Equal(64, coordinator.TakeRecoveries(world.Hour, due, 500));
        Assert.Equal(64, coordinator.TakeRecoveries(world.Hour, due));
        Assert.Equal(2, coordinator.TakeRecoveries(world.Hour, due));
        // Requeued in the same hour: never a second unit for the same shop in that hour.
        foreach (var s in shops) coordinator.RequeueRecovery(s.Key);
        Assert.Equal(0, coordinator.TakeRecoveries(world.Hour, due));
        foreach (var s in shops) coordinator.RequeueRecovery(s.Key);
        Assert.Equal(64, coordinator.TakeRecoveries(world.Hour + 1, due));
        // A unit the frame budget did not run goes back, still due in the same hour, and is never doubled.
        string skipped = due[63];
        coordinator.Requeue(skipped, world.Hour + 1); coordinator.Requeue(skipped, world.Hour + 1);
        var rest = new List<string>();
        while (coordinator.TakeRecoveries(world.Hour + 1, rest) > 0) if (rest.Contains(skipped)) break;
        Assert.Single(rest, skipped);
    }

    [Fact]
    public void ThePhaseMachineCoversEveryState()
    {
        var world = new ContractWorld();
        var shop = world.AddShop("address:main:1:shop", Burger);
        var c = world.Coordinator;
        Assert.Equal(FoodContractPhase.Clean, c.Phase(shop.Key));
        c.Prepare(shop.Key);
        Assert.Equal(FoodContractPhase.Dirty, c.Phase(shop.Key)); // pending, nothing serves yet
        world.Drain();
        Assert.Equal(FoodContractPhase.Published, c.Phase(shop.Key));
        c.Prepare(shop.Key);
        Assert.Equal(FoodContractPhase.Rebuilding, c.Phase(shop.Key));
        Assert.NotNull(c.Serving(shop.Key));
        world.Drain();
        Assert.True(c.Admit(shop.Key, true));
        Assert.False(c.Admit(shop.Key, false));
        Assert.Equal(FoodContractPhase.Dirty, c.Phase(shop.Key));
        world.Requeue(shop); world.Drain();
        c.Refuse(shop.Key, world.Hour);
        Assert.Equal(FoodContractPhase.Refused, c.Phase(shop.Key));
        Assert.Null(c.Serving(shop.Key));
        world.Hour++; world.Requeue(shop); world.Drain();
        Assert.Equal(FoodContractPhase.Recovering, c.Phase(shop.Key));
        c.Recovered(shop.Key);
        Assert.Equal(FoodContractPhase.Published, c.Phase(shop.Key));
    }
}
