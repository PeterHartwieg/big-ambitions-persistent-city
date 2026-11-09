using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using PersistentCity.Core;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

/// <summary>E5b5: dynamic acquisition and width (docs/plans/e5b-remaining.md section 4, E5b5 row; Decision 12, R12, Q1 and
/// Q2 defaults). The shop model is <see cref="ContractWorld"/>, whose SupportedBusiness, candidate rule and admission
/// call the same Core code as Runtime: <see cref="FoodMenuPolicy.LiveMenu"/>, the product policy's resolver and
/// <see cref="CompleteFoodMenu.Check"/>.</summary>
public sealed class DynamicAcquisitionTests
{
    private readonly ITestOutputHelper output;
    public DynamicAcquisitionTests(ITestOutputHelper output) { this.output = output; }

    private const string Burger = ContractWorld.Burger, HotDog = ContractWorld.HotDog, Croissant = ContractWorld.Croissant;
    // A loaded vanilla product outside the original three, and a modded double (Street Food's namespace).
    private const string Donut = "ba:itemname_donut", Dumplings = "foodtrucks:itemname_dumplings";
    private static readonly string[] Extras = { Donut, Dumplings, "ba:itemname_cupcake", "ba:itemname_salad", "ba:itemname_bagel", "foodtrucks:itemname_choppedcheese" };

    private static FoodProductPolicy Policy(IEnumerable<string> loaded, params (string Name, MealClass Class)[] overrides)
        => new FoodProductPolicy(overrides.Length == 0 ? FoodProductMetadataResolver.Empty
            : new FoodProductMetadataResolver(overrides.Select(o => new KeyValuePair<string, FoodProductMetadataOverride>(o.Name,
                new FoodProductMetadataOverride(o.Class, null, null, null, null)))),
            new FoodProductMetadataLoadedNames(loaded));

    /// <summary>A world whose catalogue and loaded-name image hold the original three and every extra product, each a
    /// retail item a building showcase can hold. The empty override table classes every loaded name Food.</summary>
    private static ContractWorld World(params (string Name, MealClass Class)[] overrides)
    {
        var world = new ContractWorld();
        foreach (var name in Extras) world.Add(new ContractWorld.Item { itemName = name, retail = true });
        world.Items[ContractWorld.ShelfItem].itemsThatCanShowcase = new[] { Burger, HotDog, Croissant }.Concat(Extras).ToArray();
        world.RebuildCatalogue();
        world.Policy = Policy(world.Items.Keys, overrides);
        return world;
    }
    private static string[] Menu(int offers) => new[] { Burger, HotDog, Croissant }.Concat(Extras).Take(offers).ToArray();

    [Fact]
    public void RuntimeUsesTheLiveMenuRuleAndTheWidthIsEight()
    {
        Assert.Equal(8, FoodMenuPolicy.LiveMaximumOffers);
        Assert.Equal(FoodMenuPolicy.MaximumOffers, FoodMenuPolicy.LiveMaximumOffers);
        Assert.Equal(8, new FoodChoiceSettings().FoodMenuMaximumOffersPerShop);
        var root = ContractRecoveryTestPaths.Root();
        string market = File.ReadAllText(Path.Combine(root, "Runtime", "NativeFoodMarket.cs"));
        string coordinator = File.ReadAllText(Path.Combine(root, "Runtime", "FoodContractDiagnostics.cs"));
        string host = File.ReadAllText(Path.Combine(root, "Runtime", "PersistentCityMod.cs"));
        // Decision 12: the original-three guard is gone from admission; LiveMenu decides with the policy only while every
        // capability passed and the coordinator is active.
        Assert.DoesNotContain("FoodProducts.Known(r.cachedAvailableProducts", market);
        Assert.Contains("if (LiveMenu(r.cachedAvailableProducts) != FoodLiveMenuRefusal.None) return false;", market);
        Assert.Contains("private bool DynamicAcquisition => contractMode == ContractMode.Active && contractCapabilities.AllPassed;", market);
        Assert.Contains("FoodMenuPolicy.LiveMenu(menu, original || !DynamicAcquisition ? null : CurrentProductPolicy)", market);
        Assert.Contains("DynamicIntegrationRefusal + \": \" + DynamicRefusalReason", market);
        Assert.Contains("\"capability failed: \" + contractCapabilities.FirstFailure", market);
        Assert.Contains("CompleteFoodMenu.Check(eligibility, AdmissionResolver,", coordinator);
        Assert.Contains("LiveMenu(r.cachedAvailableProducts) == FoodLiveMenuRefusal.None", coordinator);
        Assert.Contains("shops.ProductPolicySource = policy;", host);
        Assert.DoesNotContain("FoodProducts.Known", coordinator);
        // Q2: a price-only change at checkout is refused without RefuseCheckoutContract.
        Assert.Contains("if (completePrices == FoodPriceVerdict.PriceOnly) { CheckoutPriceOnlyRefusals++; return new FoodSale(false, PriceOnlyRefusal); }", market);
    }

    [Theory]
    [InlineData(Donut)]
    [InlineData(Dumplings)]
    public void EmptyTableFoodProductIsAdmittedAfterTheFullContract(string product)
    {
        var world = World();
        var mixed = world.AddShop("address:main:1:shop", Burger, product);
        var single = world.AddShop("address:main:2:shop", product);
        Assert.Equal(MealClass.Food, world.Policy!.Resolver.ResolveMealClass(product)); // empty table: default class
        Assert.True(world.Policy.Executable(product)); // Core decisions accept the same name
        world.Own(mixed, single);
        foreach (var shop in new[] { mixed, single })
        {
            var image = world.Coordinator.Serving(shop.Key)!;
            Assert.True(image.Passed);
            Assert.Equal(CompleteFoodMenuRefusal.None, image.Admission);
            Assert.Contains(image.Eligibility.Offers, o => o.ProductId == product);
            var capture = world.Select(shop)!;
            Assert.NotNull(capture);
            Assert.Equal("intent:" + shop.Key, world.Checkout(capture, product, "intent:" + shop.Key));
        }
        Assert.Equal(2, world.Orders.Count);
        Assert.Equal(0, world.Reconciliations);
    }

    [Theory]
    [InlineData(MealClass.Drink)]
    [InlineData(MealClass.Bundle)]
    [InlineData(MealClass.NonFood)]
    [InlineData(MealClass.Unknown)]
    public void EachExcludedClassRefusesTheWholeShop(MealClass excluded)
    {
        var world = World((Donut, excluded));
        var mixed = world.AddShop("address:main:1:shop", Burger, HotDog, Donut);
        var plain = world.AddShop("address:main:2:shop", Burger, HotDog);
        world.Own(plain);
        Assert.Equal(FoodLiveMenuRefusal.MealClass, world.LiveMenu(mixed));
        Assert.False(world.Policy!.Executable(Donut));
        // The image fails admission too: CompleteFoodMenu.Check reads the same resolver.
        Assert.Equal(CompleteFoodMenuRefusal.MealClass, world.Coordinator.Image(mixed.Key)!.Admission);
        Assert.False(world.SupportedMenu(mixed));
        Assert.False(mixed.Row.Desired);
        Assert.False(mixed.Row.Managed);
        Assert.Null(world.Select(mixed)); // no capture of the original products either: the whole shop stays native
        for (int i = 0; i < 3; i++) { world.Boundary(world.Hour + 1); world.Drain(); world.VisitAll(); }
        Assert.False(mixed.Row.Managed);
        Assert.True(plain.Row.CanServe);
    }

    [Theory]
    [InlineData(false, false)] // a capability failed: Runtime stays in fallback with the E5b1 contract
    [InlineData(true, true)]   // capabilities passed, but the coordinator is still warming
    public void WithoutDecision12TheOriginalThreeGuardStays(bool capabilities, bool warming)
    {
        var world = World();
        world.CapabilitiesPassed = capabilities;
        world.Active = capabilities && !warming; world.Warming = warming;
        var dynamic = world.AddShop("address:main:1:shop", Burger, Donut);
        var original = world.AddShop("address:main:2:shop", Burger, HotDog);
        world.VisitAll(); world.Drain(); world.VisitAll();
        Assert.Equal(FoodLiveMenuRefusal.OriginalThreeGuard, FoodMenuPolicy.LiveMenu(dynamic.Menu, null));
        if (!warming) Assert.Equal(FoodLiveMenuRefusal.OriginalThreeGuard, world.LiveMenu(dynamic));
        for (int i = 0; i < 4 && !original.Row.CanServe; i++) { world.Boundary(world.Hour + 1); world.Drain(); world.VisitAll(); }
        Assert.True(original.Row.CanServe);
        if (capabilities)
        {
            // Warming ended. The image built while warming used the policy's resolver, so the shop is wanted now and
            // acquires through the normal boundary.
            Assert.False(world.Warming);
            world.VisitAll();
            Assert.True(dynamic.Row.Desired);
            for (int i = 0; i < 4 && !dynamic.Row.CanServe; i++) { world.Boundary(world.Hour + 1); world.Drain(); world.VisitAll(); }
            Assert.True(dynamic.Row.CanServe);
        }
        else
        {
            for (int i = 0; i < 3; i++) { world.Boundary(world.Hour + 1); world.Drain(); world.VisitAll(); }
            Assert.False(dynamic.Row.Desired);
            Assert.False(dynamic.Row.Managed);
            Assert.False(world.Candidate(dynamic));
        }
        // Without a policy source (no loaded-name image) the guard also holds once active.
        var unset = World(); unset.Policy = null;
        var shop = unset.AddShop("address:main:3:shop", Burger, Donut);
        unset.VisitAll(); unset.Drain(); unset.VisitAll();
        Assert.Equal(FoodLiveMenuRefusal.NotLoaded, unset.LiveMenu(shop));
        Assert.False(shop.Row.Desired);
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(5, true)]
    [InlineData(8, true)]
    [InlineData(9, false)]
    public void MenusUpToEightOffersAreAcquiredAndNineStayNative(int offers, bool acquired)
    {
        var world = World();
        var shop = world.AddShop("address:main:1:shop", Menu(offers));
        Assert.Equal(offers, shop.Menu.Count);
        Assert.Equal(acquired ? FoodLiveMenuRefusal.None : FoodLiveMenuRefusal.Width, world.LiveMenu(shop));
        if (acquired)
        {
            world.Own(shop);
            var capture = world.Select(shop)!;
            Assert.Equal(offers, capture.Prices.Count);
            Assert.Equal(offers, world.Coordinator.Serving(shop.Key)!.Eligibility.Offers.Count);
            Assert.NotNull(world.Checkout(capture, shop.Menu[offers - 1], "last"));
        }
        else
        {
            world.VisitAll(); world.Drain(); world.VisitAll();
            for (int i = 0; i < 3; i++) { world.Boundary(world.Hour + 1); world.Drain(); world.VisitAll(); }
            Assert.False(shop.Row.Desired);
            Assert.False(shop.Row.Managed);
            Assert.False(world.Candidate(shop));
            Assert.Equal(FoodEligibilityRefusal.MenuBounds, world.Coordinator.Image(shop.Key)!.Eligibility.Refusal);
        }
    }

    [Fact]
    public void AConfiguredSmallerWidthKeepsAWiderShopNative()
    {
        var world = World();
        world.MenuLimit = 4; // FoodMenuMaximumOffersPerShop = 4: Runtime menuLimit
        var wide = world.AddShop("address:main:1:shop", Menu(5));
        var narrow = world.AddShop("address:main:2:shop", Menu(4));
        world.Own(narrow);
        for (int i = 0; i < 2; i++) { world.Boundary(world.Hour + 1); world.Drain(); world.VisitAll(); }
        Assert.True(narrow.Row.CanServe);
        Assert.False(wide.Row.Managed);
        Assert.Equal(8, new FoodChoiceSettings { FoodMenuMaximumOffersPerShop = 99 }.Also(s => s.Clamp()).FoodMenuMaximumOffersPerShop);
    }

    [Fact]
    public void ModRemovalMidHourHandsBackAtTheBoundaryAndReturnRevalidatesBeforeReacquisition()
    {
        var world = World();
        var shop = world.AddShop("address:main:1:shop", Burger, Dumplings);
        world.Own(shop);
        var capture = world.Select(shop)!;
        Assert.NotNull(world.Checkout(capture, Dumplings, "before-removal"));
        world.History.Add(new FoodShopDay { Day = (int)(world.Hour / 24), PlaceId = shop.Key, BusinessId = shop.Row.BusinessId,
            ProductSales = new List<FoodProductSales> { new FoodProductSales { ProductId = Dumplings } } });
        var history = world.History.ToArray(); var orders = world.Orders.ToArray();
        long removedHour = world.Hour;

        // Street Food unregisters mid-hour: the definition leaves the catalogue and the loaded-name image.
        var definition = world.Items[Dumplings];
        world.Items.Remove(Dumplings);
        world.RebuildCatalogue();
        world.Policy = Policy(world.Items.Keys);
        world.VisitAll();
        Assert.Equal(FoodLiveMenuRefusal.NotLoaded, world.LiveMenu(shop));
        Assert.False(shop.Row.Desired); // desire withdrawn at once
        Assert.True(shop.Row.Managed);  // the hand-back waits for the boundary
        Assert.True(world.Refused.ContainsKey(shop.Key));
        Assert.Null(world.Select(shop)); // no capture on the removed product
        Assert.Contains(world.Boundary(world.Hour + 1), o => o.Key == shop.Key && o.Outcome == FoodContractBoundary.Outcome.HandedBack);
        Assert.False(shop.Row.Managed);
        Assert.Equal(history, world.History); // history and receipts kept
        Assert.Equal(orders, world.Orders);

        // The mod returns (a new definition object). The recorded refusal keeps the shop native until a fresh image,
        // recovery after the refused hour and the commit gate.
        world.Add(new ContractWorld.Item { itemName = Dumplings, retail = true });
        Assert.NotSame(definition, world.Items[Dumplings]);
        world.RebuildCatalogue();
        world.Policy = Policy(world.Items.Keys);
        world.VisitAll();
        Assert.Equal(FoodLiveMenuRefusal.None, world.LiveMenu(shop));
        Assert.False(shop.Row.Desired);
        int gates = world.GateRuns;
        long reacquired = -1;
        for (int i = 0; i < 6 && reacquired < 0; i++)
        {
            world.Drain(); world.VisitAll();
            var outcomes = world.Boundary(world.Hour + 1);
            if (outcomes.Any(o => o.Key == shop.Key && o.Outcome == FoodContractBoundary.Outcome.Acquired)) reacquired = world.Hour;
        }
        Assert.True(reacquired > removedHour + 1, "reacquired at " + reacquired);
        Assert.False(world.Refused.ContainsKey(shop.Key));
        Assert.True(world.GateRuns > gates); // the commit gate revalidated before the transfer
        Assert.Same(world.Items[Dumplings], world.Coordinator.Serving(shop.Key)!.Definitions.Single(d => d.Name == Dumplings).Live);
        Assert.Equal(orders, world.Orders);
        world.VisitAll();
        Assert.NotNull(world.Checkout(world.Select(shop)!, Dumplings, "after-return"));
    }

    [Fact]
    public void APriceOnlyChangeKeepsOwnershipAndTheNextCaptureUsesTheNewPrice()
    {
        var world = World();
        var shop = world.AddShop("address:main:1:shop", Burger, Donut);
        world.Own(shop);
        var capture = world.Select(shop)!;
        shop.PriceOf(Donut).price = 6; // W10 in place, a valid new price, between capture and checkout
        Assert.Null(world.Checkout(capture, Donut, "old-terms"));
        Assert.Empty(world.Orders); // never a sale on the changed terms
        Assert.Equal(1, world.PriceOnlyRefusals);
        Assert.True(shop.Row.Desired);
        Assert.True(shop.Row.CanServe);
        Assert.False(world.Refused.ContainsKey(shop.Key));
        Assert.DoesNotContain(world.Boundary(world.Hour + 1), o => o.Key == shop.Key && o.Outcome == FoodContractBoundary.Outcome.HandedBack);
        world.Drain(); world.VisitAll();
        Assert.True(shop.Row.CanServe);
        var next = world.Select(shop)!;
        Assert.Equal(Money.FromNative(6f), next.Prices[Donut]);
        Assert.Equal("new-terms", world.Checkout(next, Donut, "new-terms"));
        // A price change before the capture is recorded at the capture recheck, also without a hand-back.
        shop.PriceOf(Burger).price = 4.5f;
        var later = world.Select(shop)!;
        Assert.Equal(Money.FromNative(4.5f), later.Prices[Burger]);
        Assert.Equal(Money.FromNative(4.5f), world.Coordinator.Serving(shop.Key)!.Prices[0]);
        Assert.True(shop.Row.Desired);
    }

    [Theory]
    [InlineData("invalid-price")]
    [InlineData("menu")]
    [InlineData("holder")]
    [InlineData("bag")]
    public void AnyOtherChangeStillHandsBack(string change)
    {
        var world = World();
        var shop = world.AddShop("address:main:1:shop", Burger, Donut);
        world.Own(shop);
        var capture = world.Select(shop)!;
        switch (change)
        {
            case "invalid-price": shop.PriceOf(Donut).price = 0; break;
            case "menu":
                shop.Menu.Add(HotDog); shop.Prices.Add(new ContractWorld.Price { itemName = HotDog, price = 5 });
                break;
            case "holder": shop.Holders.Remove(shop.Key + ":shelf:" + Burger); break;
            case "bag": shop.Holder(shop.Key + ":counter").cargoInstances[0].itemName = Burger; break;
        }
        Assert.Null(world.Checkout(capture, Donut, "intent"));
        Assert.Empty(world.Orders);
        Assert.Equal(0, world.PriceOnlyRefusals);
        Assert.False(shop.Row.Desired);
        Assert.True(world.Refused.ContainsKey(shop.Key));
        Assert.Contains(world.Boundary(world.Hour + 1), o => o.Key == shop.Key && o.Outcome == FoodContractBoundary.Outcome.HandedBack);
    }

    [Fact]
    public void LiveMenuRefusesEachMalformedShapeAndAllocatesNothing()
    {
        var policy = Policy(new[] { Burger, Donut, Dumplings });
        Assert.Equal(FoodLiveMenuRefusal.Empty, FoodMenuPolicy.LiveMenu(null, policy));
        Assert.Equal(FoodLiveMenuRefusal.Empty, FoodMenuPolicy.LiveMenu(new List<string>(), policy));
        Assert.Equal(FoodLiveMenuRefusal.Duplicate, FoodMenuPolicy.LiveMenu(new List<string> { Donut, Burger, Donut }, policy));
        Assert.Equal(FoodLiveMenuRefusal.Duplicate, FoodMenuPolicy.LiveMenu(new List<string> { Burger, Burger }, null));
        Assert.Equal(FoodLiveMenuRefusal.Name, FoodMenuPolicy.LiveMenu(new List<string> { "bad|name" }, policy));
        Assert.Equal(FoodLiveMenuRefusal.NotLoaded, FoodMenuPolicy.LiveMenu(new List<string> { "ba:itemname_bagel" }, policy));
        Assert.Equal(FoodLiveMenuRefusal.NotLoaded, FoodMenuPolicy.LiveMenu(new List<string> { Donut }, FoodProductPolicy.OriginalThree));
        Assert.Equal(FoodLiveMenuRefusal.None, FoodMenuPolicy.LiveMenu(new List<string> { Burger, HotDog, Croissant }, null));
        Assert.Equal(FoodLiveMenuRefusal.None, FoodMenuPolicy.LiveMenu(new List<string> { Burger, Donut, Dumplings }, policy));
        var menu = new List<string>(Menu(8));
        var wide = Policy(menu);
        FoodMenuPolicy.LiveMenu(menu, wide);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) FoodMenuPolicy.LiveMenu(menu, wide);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    /// <summary>Offline perf arms for docs/perf/e5b5.md. Not a gate beyond the asserted bounds.</summary>
    [Fact]
    public void EightOfferCostsAndA1024ShopRecoveryWave()
    {
        // LiveMenu per discovery visit of an eight-offer dynamic shop (the only per-visit addition).
        var menu = new List<string>(Menu(8)); var policy = Policy(menu);
        var visit = Percentiles(5000, () => FoodMenuPolicy.LiveMenu(menu, policy));
        output.WriteLine($"liveMenu8 p50={visit.P50:F5} p95={visit.P95:F5} max={visit.Max:F5} ms");

        // Selected capture and checkout of an eight-offer shop through the model (recheck, supply, reconciliation).
        var world = World();
        var shop = world.AddShop("address:main:1:shop", Menu(8));
        foreach (var c in shop.Holders.Values) foreach (var cargo in c.cargoInstances) cargo.amount = 100000;
        world.Own(shop);
        var captures = new List<double>(); var checkouts = new List<double>();
        for (int i = 0; i < 1200; i++)
        {
            long t0 = Stopwatch.GetTimestamp();
            var capture = world.Select(shop)!;
            long t1 = Stopwatch.GetTimestamp();
            Assert.NotNull(world.Checkout(capture, shop.Menu[i % 8], "i" + i));
            long t2 = Stopwatch.GetTimestamp();
            if (i >= 200) { captures.Add(Ms(t1 - t0)); checkouts.Add(Ms(t2 - t1)); }
        }
        var cap = Stats(captures); var chk = Stats(checkouts);
        output.WriteLine($"capture8 p50={cap.P50:F4} p95={cap.P95:F4} max={cap.Max:F4} ms; checkout8 p50={chk.P50:F4} p95={chk.P95:F4} max={chk.Max:F4} ms");

        // 1,024 owned eight-offer shops (half dynamic) refused and repaired in one hour: the recovery wave.
        var wave = World();
        var shops = Enumerable.Range(0, 1024).Select(i => wave.AddShop("address:w:" + i + ":shop", i % 2 == 0 ? Menu(8) : Menu(8).Reverse().ToArray())).ToArray();
        // 16 acquisitions commit per boundary, so owning 1,024 shops takes 64 boundaries.
        wave.VisitAll(); wave.Drain(); wave.VisitAll();
        for (int i = 0; i < 80 && shops.Any(x => !x.Row.CanServe); i++) { wave.Boundary(wave.Hour + 1); wave.Drain(); wave.VisitAll(); }
        Assert.All(shops, x => Assert.True(x.Row.CanServe));
        foreach (var s in shops) wave.Refuse(s);
        wave.Boundary(wave.Hour + 1);
        Assert.All(shops, s => Assert.False(s.Row.Managed));
        var frames = new List<double>();
        int publishes = wave.Publishes;
        wave.BeginHour(wave.Hour + 1);
        for (int f = 0; f < 10000 && (wave.PendingRebuilds > 0 || wave.Coordinator.PendingPreparations > 0 || wave.Coordinator.PendingRecoveries > 0); f++)
        {
            long t = Stopwatch.GetTimestamp(); wave.Frame(); frames.Add(Ms(Stopwatch.GetTimestamp() - t));
        }
        // The model frame has no time budget; Runtime stops at 0.5 ms and requeues. Report the work per unit instead.
        var frame = Stats(frames);
        int units = 1024 + (wave.Publishes - publishes) + wave.RecoveryUnits; // preparations + passes + recoveries
        double total = frames.Sum();
        output.WriteLine($"recoveryWave1024 modelFrames={frames.Count} frameP95={frame.P95:F4} frameMax={frame.Max:F4} ms; total={total:F3} ms; "
            + $"units={units} meanUnit={total / units:F4} ms; recoveries={wave.RecoveryUnits} maxRecoveriesInFrame={wave.MaxRecoveriesInFrame}; "
            + $"framesAt0.5ms={Math.Ceiling(total / 0.5)}");
        Assert.True(wave.MaxRecoveriesInFrame <= FoodContractCoordinator.RecoveriesPerFrame);
        Assert.All(shops, s => Assert.False(wave.Refused.ContainsKey(s.Key)));
        wave.VisitAll();
        Assert.All(shops, s => Assert.True(s.Row.Desired));
        // Reacquisition is spread at 16 per boundary.
        var acquired = wave.Boundary(wave.Hour + 1).Count(o => o.Outcome == FoodContractBoundary.Outcome.Acquired);
        Assert.Equal(FoodContractCoordinator.CommitsPerBoundary, acquired);
    }

    private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
    private static (double P50, double P95, double Max) Percentiles(int n, Action action)
    {
        for (int i = 0; i < 200; i++) action();
        var samples = new List<double>(n);
        for (int i = 0; i < n; i++) { long t = Stopwatch.GetTimestamp(); action(); samples.Add(Ms(Stopwatch.GetTimestamp() - t)); }
        return Stats(samples);
    }
    private static (double P50, double P95, double Max) Stats(List<double> samples)
    {
        var sorted = samples.OrderBy(x => x).ToArray();
        double At(double q) => sorted[Math.Min(sorted.Length - 1, (int)Math.Ceiling(sorted.Length * q) - 1)];
        return (At(.5), At(.95), sorted[^1]);
    }
}
