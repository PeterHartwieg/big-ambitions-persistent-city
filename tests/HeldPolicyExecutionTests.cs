using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

public sealed class HeldPolicyExecutionTests
{
    private readonly ITestOutputHelper output;
    public HeldPolicyExecutionTests(ITestOutputHelper output) { this.output = output; }

    private const string Dumplings = FoodMetadataApiTests.Dumplings, ChoppedCheese = FoodMetadataApiTests.ChoppedCheese;

    internal sealed class PolicyMarket : IFoodMarket, IFoodProductPolicyMarket, IFoodSaleEvidence
    {
        public double EarliestMinute => 0;
        public FoodBusiness[] Shops = Array.Empty<FoodBusiness>();
        public IFoodProductPolicy Policy = FoodProductPolicy.OriginalThree;
        public readonly List<string> Orders = new();
        public readonly HashSet<string> Paid = new(StringComparer.Ordinal);
        public bool ThrowAfterSale;
        public int Attempts, PolicyReads;
        public IFoodProductPolicy ProductPolicy { get { PolicyReads++; return Policy; } }
        public IReadOnlyList<FoodBusiness> Read(double at) => Shops;
        public FoodSale Buy(ResidentMeal m, double at)
        {
            Attempts++; var offer = Shops.Single(b => b.Id == m.BusinessId).Offers.Single(o => o.ProductId == m.ProductId);
            if (offer.Price != m.Price || offer.Units <= 0) return new(false, "Price or stock changed");
            Assert.DoesNotContain(m.IntentId, Orders); Orders.Add(m.IntentId); Paid.Add(m.IntentId);
            if (ThrowAfterSale) throw new InvalidOperationException("Acknowledgement lost");
            return new(true, "Confirmed");
        }
        public bool IsConfirmedPaid(ResidentMeal meal) => false;
    }

    private static FoodBusiness Business(string id, string place, params string[] products) => new()
    {
        Id = id, PlaceId = place, Name = place, MealsPerHour = 60,
        Hours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 0, ToMinute = 1440 }).ToList(),
        Offers = products.Select((p, i) => new FoodOffer { ProductId = p, Price = Money.FromCents(100 + 10 * i), Units = 100 }).ToList(),
    };

    private static (CitySimulator City, PolicyMarket Market, WeightedFoodChoiceTests.Planner Planner) Ready(int seed, IFoodProductPolicy policy, params string[][] menus)
    {
        var (seeded, _, planner) = WeightedFoodChoiceTests.Ready(seed, menus.Length);
        var state = seeded.State;
        Assert.Equal(CityState.CurrentSchema, state.Schema);
        Assert.Equal(FoodNameIdentity.DynamicWire, state.MenuPolicy);
        for (int i = 0; i < menus.Length; i++)
        {
            var scope = state.Food!.Shops[i];
            scope.ProductId = menus[i][0]; scope.AdditionalProductIds = menus[i].Skip(1).ToList();
        }
        var market = new PolicyMarket { Policy = policy, Shops = state.Food!.Shops.Select((s, i) => Business(s.BusinessId, s.PlaceId, menus[i])).ToArray() };
        var city = new CitySimulator(state, planner); city.AttachFoodMarket(market);
        return (city, market, planner);
    }

    private static CitySimulator Reload(CitySimulator city, PolicyMarket market, WeightedFoodChoiceTests.Planner planner, out string payload)
    {
        var data = new Dictionary<string, string>();
        var session = SaveSession.Open(data, city.State.PopulationKey);
        var write = session.WriteTo(data, city.State); Assert.True(write.Success, write.Error);
        payload = data[SaveCodec.StateKey];
        var read = SaveSession.Open(data, city.State.PopulationKey).InitialReadResult;
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var restored = new CitySimulator(read.State!, planner); restored.AttachFoodMarket(market);
        return restored;
    }

    private static FoodProductMetadataResolver Config(params (string Name, FoodProductMetadataOverride Value)[] entries)
        => new(entries.Select(e => new KeyValuePair<string, FoodProductMetadataOverride>(e.Name, e.Value)));

    private static FoodProductPolicy Policy(FoodProductMetadataResolver? config = null, params string[] loaded)
        => new(config ?? FoodProductMetadataResolver.Empty,
            FoodMetadataApiTests.Catalogue(loaded.Length == 0 ? new[] { Dumplings, ChoppedCheese, FoodProducts.Burger } : loaded));

    private static ResidentMeal Meal(CitySimulator city) => city.State.Food!.Residents.Single();

    private static void ToCapture(CitySimulator city)
    {
        var meal = Meal(city);
        for (int i = 0; i < 64 && (meal.Decision == null || meal.Decision.Shops.Count == 0); i++) city.AdvanceTo(meal.DesiredAt, 1);
        Assert.NotNull(meal.Decision); Assert.NotEmpty(meal.Decision!.Shops);
    }

    private static IEnumerable<FoodDecisionOffer> HeldOffers(CitySimulator city)
        => Meal(city).Decision?.Shops.SelectMany(s => s.Offers) ?? Enumerable.Empty<FoodDecisionOffer>();

    private static string Tuples(CitySimulator city)
        => JsonConvert.SerializeObject(HeldOffers(city).Select(o => new { o.ProductId, o.Metadata }));

    private static ResidentTraits Traits(CitySimulator city) => city.State.Citizens.Single().Traits!;


    [Fact]
    public void FreshCityIsDynamicWireAndSavesAndReloadsByteIdentical()
    {
        var state = CurrentSchemaOnlyTests.City();
        Assert.Equal(FoodNameIdentity.DynamicWire, state.MenuPolicy);
        string payload = SaveCodec.Write(state);
        Assert.StartsWith(SaveCodec.BodyPrefix, payload);
        Assert.Contains(FoodNameIdentity.DynamicWire, payload);
        SaveCodec.RequireExecutableCapabilities(payload);
        var read = SaveCodec.Read(payload); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(FoodNameIdentity.DynamicWire, read.State!.MenuPolicy);
        Assert.Equal(payload, SaveCodec.Write(read.State));
        Assert.Equal(payload, SaveCodec.Encode(SaveCodec.Capture(read.State)));
        _ = new CitySimulator(read.State, new WeightedFoodChoiceTests.Planner());
        var (city, market, planner) = Ready(3, Policy(), new[] { Dumplings, FoodProducts.Burger }, new[] { ChoppedCheese });
        ToCapture(city);
        string before = SaveCodec.Write(city.State);
        var again = Reload(city, market, planner, out string saved);
        Assert.Equal(before, saved); Assert.Equal(before, SaveCodec.Write(again.State));
    }

    [Fact]
    public void AnotherSchemaNumberIsRefusedAndKept()
    {
        string current = SaveCodec.Write(ChangeJournalTests.Fixture(5150));
        foreach (int other in new[] { 2, 36, 37 })
        {
            string payload = current.Replace(SaveCodec.BodyPrefix, "{\"Schema\":" + other + ",\"Capabilities\":");
            Assert.Equal(SaveReadStatus.DevelopmentSchema, SaveCodec.Read(payload).Status);
            EconomicLedgerTests.Refused(payload, SaveReadStatus.DevelopmentSchema);
        }
    }

    [Fact]
    public void TheWriterExecutesDynamicWireAndNothingElse()
    {
        var state = CurrentSchemaOnlyTests.City();
        SaveCodec.RequireExecutablePolicy(state);
        _ = new CitySimulator(state, new WeightedFoodChoiceTests.Planner());
        state.MenuPolicy = FoodNameIdentity.Wire;
        Assert.Throws<SaveValidationException>(() => SaveCodec.RequireExecutablePolicy(state));
        Assert.Throws<InvalidOperationException>(() => new CitySimulator(state, new WeightedFoodChoiceTests.Planner()));
        Assert.Throws<SaveValidationException>(() => SaveCodec.Write(state));
        string body = SaveWire.Text(CurrentSchemaOnlyTests.City()).Replace(FoodNameIdentity.DynamicWire, FoodNameIdentity.Wire);
        Assert.Throws<SaveValidationException>(() => SaveCodec.RequireExecutableCapabilities(body));
    }


    [Theory]
    [InlineData(3)] [InlineData(11)] [InlineData(40)]
    public void ARevision2HeldDecisionReplaysAcrossReloadWhileConfigAndApiChangeAndTheNextContextUsesTheNewValues(int seed)
    {
        var registry = FoodMetadataApiTests.Registry(); using var provider = FoodMetadataApiTests.Provider(registry);
        var input = new FoodProductMetadataInput { MealClass = MealClass.Food, TasteBand = 4, BreakfastFitCents = 10, LunchFitCents = 20, DinnerFitCents = 30 };
        Assert.True(provider.TryRegisterProductMetadata(ChoppedCheese, input, 1, out _));
        var loaded = FoodMetadataApiTests.Catalogue(Dumplings, ChoppedCheese, FoodProducts.Burger);
        var configA = Config((Dumplings, new FoodProductMetadataOverride(MealClass.Food, 3, 40, 50, 60)));
        var policyA = new FoodProductPolicy(configA.WithApi(registry.Publish(loaded)), loaded);
        string[] s0 = { Dumplings, FoodProducts.Burger }, s1 = { ChoppedCheese };
        var (control, cm, cp) = Ready(seed, policyA, s0, s1);
        var (changed, mm, pm) = Ready(seed, policyA, s0, s1);
        ToCapture(control); ToCapture(changed);
        string held = Tuples(changed);
        var traits = Traits(changed); string resident = Meal(changed).ResidentId; int citySeed = changed.State.Seed;
        foreach (var offer in HeldOffers(changed))
        {
            Assert.NotNull(offer.Metadata);
            if (offer.ProductId == FoodProducts.Burger) Assert.Equal(1, offer.Metadata!.Revision);
            else
            {
                Assert.Equal(2, offer.Metadata!.Revision);
                Assert.Equal(JsonConvert.SerializeObject(policyA.Capture(offer.ProductId, traits, citySeed, resident)), JsonConvert.SerializeObject(offer.Metadata));
            }
        }
        Assert.Contains(HeldOffers(changed), o => o.ProductId == Dumplings && o.Metadata!.TasteBand == 3 && o.Metadata.LunchFitCents == 50);
        Assert.Contains(HeldOffers(changed), o => o.ProductId == ChoppedCheese && o.Metadata!.TasteBand == 4 && o.Metadata.LunchFitCents == 20);

        input.TasteBand = 0; input.LunchFitCents = 5000;
        Assert.True(provider.TryRegisterProductMetadata(ChoppedCheese, input, 1, out _));
        var configB = Config((Dumplings, new FoodProductMetadataOverride(MealClass.Drink, 0, 0, 0, 0)));
        var policyB = new FoodProductPolicy(configB.WithApi(registry.Publish(loaded)), loaded);
        mm.Policy = policyB;
        changed = Reload(changed, mm, pm, out _);
        Assert.Equal(held, Tuples(changed));
        double at = Meal(control).DesiredAt;
        while (!control.AdvanceTo(at + 60, 128).CaughtUp) { }
        while (!changed.AdvanceTo(at + 60, 1).CaughtUp) { }
        Assert.Equal(SaveCodec.Write(control.State), SaveCodec.Write(changed.State));
        Assert.Equal(cm.Orders, mm.Orders); Assert.Single(mm.Orders);

        FoodDecisionState? nextDecision = null;
        changed.FoodDecisionObserved += t => nextDecision ??= t.Decision;
        double end = changed.State.Minute + 3 * 1440;
        while (nextDecision == null && changed.State.Minute < end) changed.AdvanceTo(changed.State.Minute + 30, 64);
        Assert.NotNull(nextDecision);
        var nextOffers = nextDecision!.Shops.SelectMany(s => s.Offers).ToList();
        Assert.DoesNotContain(nextOffers, o => o.ProductId == Dumplings);
        var cheese = nextOffers.Single(o => o.ProductId == ChoppedCheese).Metadata!;
        Assert.Equal(2, cheese.Revision); Assert.Equal(0, cheese.TasteBand); Assert.Equal(5000, cheese.LunchFitCents);
        string next = SaveCodec.Write(changed.State);
        Assert.Equal(next, SaveCodec.Write(SaveCodec.Read(next).State!));
    }

    [Theory]
    [InlineData(MealClass.Drink)] [InlineData(MealClass.Bundle)] [InlineData(MealClass.NonFood)] [InlineData(MealClass.Unknown)]
    public void DrinkBundleNonFoodAndUnknownNamesNeverEnterADecision(MealClass excluded)
    {
        var policy = Policy(Config((Dumplings, new FoodProductMetadataOverride(excluded, 2, 900, 100, 450))));
        Assert.False(policy.Executable(Dumplings));
        Assert.Throws<ArgumentException>(() => policy.Capture(Dumplings, new ResidentTraits { Bands = "2222222222" }, 1, "r"));
        var (city, _, _) = Ready(5, policy, new[] { Dumplings }, new[] { FoodProducts.Burger, Dumplings }, new[] { ChoppedCheese });
        ToCapture(city);
        Assert.DoesNotContain(HeldOffers(city), o => o.ProductId == Dumplings);
        Assert.Contains(HeldOffers(city), o => o.ProductId == ChoppedCheese);
        Assert.DoesNotContain(Meal(city).Decision!.Shops, s => s.BusinessId == "b0" || s.BusinessId == "b1");
    }

    [Fact]
    public void UnloadedNonCanonicalAndUnpublishedNamesAreNotExecutable()
    {
        var policy = Policy(null, ChoppedCheese);
        Assert.False(policy.Executable(Dumplings));
        Assert.True(policy.Executable(ChoppedCheese));
        Assert.False(policy.Executable("bad|name")); Assert.False(policy.Executable(""));
        var unpublished = new FoodProductPolicy(FoodProductMetadataResolver.Empty, null);
        Assert.False(unpublished.Executable(ChoppedCheese));
        Assert.False(FoodProductPolicy.OriginalThree.Executable(ChoppedCheese));
        foreach (var original in new[] { FoodProducts.Croissant, FoodProducts.Burger, FoodProducts.HotDog })
        { Assert.True(policy.Executable(original)); Assert.True(unpublished.Executable(original)); Assert.True(FoodProductPolicy.OriginalThree.Executable(original)); }
        var (city, _, _) = Ready(5, policy, new[] { Dumplings }, new[] { ChoppedCheese });
        ToCapture(city);
        Assert.Equal(new[] { ChoppedCheese }, HeldOffers(city).Select(o => o.ProductId).ToArray());
    }

    [Fact]
    public void TheOriginalThreeKeepTraitsFitsAndLegacyCaptureEvenUnderOverrides()
    {
        var traits = new ResidentTraits { Bands = "0123401234" };
        var config = Config((FoodProducts.Burger, new FoodProductMetadataOverride(MealClass.Drink, 0, 5000, 5000, 5000)));
        var policy = Policy(config);
        foreach (var original in new[] { FoodProducts.Croissant, FoodProducts.Burger, FoodProducts.HotDog })
        {
            Assert.True(policy.Executable(original));
            Assert.Equal(JsonConvert.SerializeObject(LegacyFoodOfferMetadata.Capture(traits, 957, "pc:1", original)),
                JsonConvert.SerializeObject(policy.Capture(original, traits, 957, "pc:1")));
            Assert.Equal(1, policy.Capture(original, traits, 957, "pc:1").Revision);
        }
        foreach (int seed in new[] { 2, 9, 31 })
        {
            var (a, ma, _) = Ready(seed, policy, new[] { FoodProducts.Croissant, FoodProducts.Burger }, new[] { FoodProducts.HotDog });
            var (b, mb, _) = Ready(seed, FoodProductPolicy.OriginalThree, new[] { FoodProducts.Croissant, FoodProducts.Burger }, new[] { FoodProducts.HotDog });
            double at = Meal(a).DesiredAt + 120;
            while (!a.AdvanceTo(at, 64).CaughtUp) { }
            while (!b.AdvanceTo(at, 64).CaughtUp) { }
            Assert.Equal(SaveCodec.Write(b.State), SaveCodec.Write(a.State));
            Assert.Equal(mb.Orders, ma.Orders);
            Assert.True(ma.PolicyReads > 0);
        }
    }

    [Fact]
    public void AnUncertainDynamicReceiptIsNeverRetriedAcrossReload()
    {
        var (city, market, planner) = Ready(7, Policy(), new[] { Dumplings }, new[] { ChoppedCheese });
        market.ThrowAfterSale = true;
        double at = Meal(city).DesiredAt;
        while (!city.AdvanceTo(at + 60, 8).CaughtUp) { }
        Assert.Equal(1, market.Attempts);
        var meal = Meal(city);
        Assert.Contains(meal.ProductId, new[] { Dumplings, ChoppedCheese });
        Assert.Equal(MealStatus.Uncertain, meal.Status);
        market.ThrowAfterSale = false;
        city = Reload(city, market, planner, out _);
        while (!city.AdvanceTo(at + 600, 8).CaughtUp) { }
        Assert.Equal(1, market.Attempts); Assert.Single(market.Orders);
    }

    [Fact]
    public void CatalogueAndShopOrderChangesDoNotRerollTheDecision()
    {
        var a = Policy(null, Dumplings, ChoppedCheese, FoodProducts.Burger, "ba:itemname_salad");
        var b = Policy(null, "ba:itemname_salad", FoodProducts.Burger, ChoppedCheese, Dumplings);
        string[][] menus = { new[] { Dumplings, FoodProducts.Burger }, new[] { ChoppedCheese }, new[] { "ba:itemname_salad" }, new[] { Dumplings } };
        var (x, _, _) = Ready(15, a, menus);
        var (y, my, _) = Ready(15, b, menus);
        y.State.Food!.Shops.Reverse(); Array.Reverse(my.Shops);
        FoodDecisionState? dx = null, dy = null;
        x.FoodDecisionObserved += t => dx = t.Decision; y.FoodDecisionObserved += t => dy = t.Decision;
        double at = Meal(x).DesiredAt;
        while (!x.AdvanceTo(at, 128).CaughtUp) { }
        while (!y.AdvanceTo(at, 1).CaughtUp) { }
        Assert.NotNull(dx);
        Assert.Equal(JsonConvert.SerializeObject(dx), JsonConvert.SerializeObject(dy));
        Assert.Contains(dx!.Shops.SelectMany(s => s.Offers), o => o.Metadata!.Revision == 2);
    }

    [Fact]
    public void DefaultTasteVectorsThroughThePolicyEqualTheE5cReference()
    {
        var policy = Policy(null, Dumplings, ChoppedCheese);
        var traits = new ResidentTraits { Bands = "2222222222" };
        var bands = new List<int>();
        for (int c = 0; c < 100; c++)
            foreach (var name in new[] { Dumplings, ChoppedCheese })
            {
                var held = policy.Capture(name, traits, 957, "pc:" + c);
                Assert.Equal(2, held.Revision); Assert.Equal(FoodProductMetadataResolver.DefaultTaste(957, "pc:" + c, name), held.TasteBand);
                bands.Add(held.TasteBand);
            }
        string text = string.Join(",", bands);
        string sha = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        Assert.Equal("937617912baf43dfdc29f75444d394ee4505fa89bea2dbc72c3f938194b3949e", sha);
    }


    private sealed class Seller : IFoodMarket, IFoodProductPolicyMarket
    {
        public double EarliestMinute => 0;
        public IFoodProductPolicy ProductPolicy { get; } = new FoodProductPolicy(FoodProductMetadataResolver.Empty, FoodMetadataApiTests.Catalogue("other:name"));
        public IReadOnlyList<FoodBusiness> Read(double at) => Array.Empty<FoodBusiness>();
        public FoodSale Buy(ResidentMeal m, double at) => new(false, "unused");
    }

    private static LoadedNameImage Image(long revision, params string[] names)
        => new(revision, names, FoodEligibilityRefusal.None);

    [Fact]
    public void ShopAndVehicleScopesShareTheCompositesOnePolicyAndItIsRebuiltOnlyWhenAnInputChanges()
    {
        LoadedNameImage? image = Image(1, Dumplings, ChoppedCheese);
        var config = Config((Dumplings, new FoodProductMetadataOverride(MealClass.Food, 1, 1, 1, 1)));
        var snapshot = FoodProductMetadataSnapshot.Empty; int apiCalls = 0; FoodProductMetadataLoadedNames? lastCatalogue = null;
        var source = new FoodProductPolicySource(() => image, () => config, c => { apiCalls++; lastCatalogue = c; return snapshot; });
        var composite = new FoodMarkets(new FoodCommerceState(), new Seller(), new Seller(), _ => new Position(0, 0, 0), source);
        var first = composite.ProductPolicy;
        Assert.Same(first, source.Current);
        Assert.True(first.Executable(Dumplings)); Assert.False(first.Executable("other:name"));
        Assert.Equal(1, first.Capture(Dumplings, new ResidentTraits { Bands = "2222222222" }, 1, "r").TasteBand);
        long published = source.Publications;
        for (int i = 0; i < 100; i++) Assert.Same(first, composite.ProductPolicy);
        Assert.Equal(published, source.Publications); Assert.NotNull(lastCatalogue); Assert.True(lastCatalogue!.Contains(ChoppedCheese));
        config = Config((Dumplings, new FoodProductMetadataOverride(MealClass.Drink, 1, 1, 1, 1)));
        var second = composite.ProductPolicy; Assert.NotSame(first, second); Assert.False(second.Executable(Dumplings));
        image = Image(2, ChoppedCheese);
        var third = composite.ProductPolicy; Assert.NotSame(second, third); Assert.True(third.Executable(ChoppedCheese));
        Assert.False(lastCatalogue.Contains(Dumplings));
        image = null;
        var dirty = composite.ProductPolicy; Assert.False(dirty.Executable(ChoppedCheese)); Assert.True(dirty.Executable(FoodProducts.Burger));
        image = new LoadedNameImage(3, new[] { ChoppedCheese }, FoodEligibilityRefusal.CargoBounds);
        Assert.False(composite.ProductPolicy.Executable(ChoppedCheese));
        var failingImage = Image(1, Dumplings); bool unbound = true; var reports = new List<string>();
        var failing = new FoodProductPolicySource(() => failingImage, () => config,
            c => unbound ? throw new InvalidOperationException("Metadata publication requires the bound host main thread.") : FoodProductMetadataSnapshot.Empty,
            reports.Add);
        Assert.Same(FoodProductPolicy.OriginalThree, failing.Current); Assert.Contains("main thread", failing.Failure);
        for (int i = 0; i < 50; i++) Assert.Same(FoodProductPolicy.OriginalThree, failing.Current);
        Assert.Equal(1, failing.PublishAttempts); Assert.Single(reports);
        unbound = false; failingImage = Image(2, ChoppedCheese);
        Assert.True(failing.Current.Executable(ChoppedCheese)); Assert.Equal("", failing.Failure); Assert.Single(reports);
        Assert.Same(FoodProductPolicy.OriginalThree, new FoodMarkets(new FoodCommerceState(), new Seller(), new Seller(), _ => new Position(0, 0, 0)).ProductPolicy);
        Assert.True(apiCalls > 0);
    }

    [Fact]
    public void ThePolicyReadAndTheLegacyWeightPathAllocateNothingInSteadyState()
    {
        var image = Image(1, Dumplings, ChoppedCheese);
        var config = FoodProductMetadataResolver.Empty; var snapshot = FoodProductMetadataSnapshot.Empty;
        var source = new FoodProductPolicySource(() => image, () => config, _ => snapshot);
        var traits = new ResidentTraits { Bands = "2222222222" }; var settings = new FoodChoiceSettings();
        var draw = new FoodDrawContext(957, "pc:1", 3, MealKind.Lunch, "ctx", 1);
        var offers = Enumerable.Range(0, 8).Select(i => new EvaluatedFoodOffer("b" + i, FoodProducts.Burger, Money.FromCents(900 + i), 4, 0, 20, 60, 60, 50, MealKind.Lunch)).ToArray();
        var held = new EvaluatedFoodOffer("b9", Dumplings, Money.FromCents(900), 4, 0, 20, 60, 60, 50, MealKind.Lunch, newTasteBand: 3,
            metadata: new FoodProductMetadata(MealClass.Food, 3, 900, 100, 450));
        for (int i = 0; i < 100; i++) { _ = source.Current; FoodChoicePolicy.Select(offers, traits, Money.FromCents(100000), draw, settings, true); }
        long allocated = long.MaxValue;
        for (int round = 0; round < 5; round++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 2000; i++)
            {
                _ = source.Current.Executable(Dumplings);
                FoodChoicePolicy.Select(offers, traits, Money.FromCents(100000), draw, settings, true);
                FoodChoicePolicy.Weight(held, traits, Money.Zero, settings, held.Metadata!.Value);
            }
            allocated = Math.Min(allocated, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        Assert.Equal(0, allocated);
        var policy = source.Current;
        Assert.Equal(policy.Capture(Dumplings, traits, 957, "pc:1").TasteBand, policy.TasteBand(Dumplings, traits, 957, "pc:1"));
        Assert.Equal(policy.Capture(FoodProducts.Burger, traits, 957, "pc:1").TasteBand, policy.TasteBand(FoodProducts.Burger, traits, 957, "pc:1"));
        allocated = long.MaxValue;
        for (int round = 0; round < 5; round++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) _ = policy.TasteBand(Dumplings, traits, 957, "pc:1");
            allocated = Math.Min(allocated, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void RivalsStayOriginalThree()
    {
        Assert.Throws<ArgumentException>(() => new FoodAdvertisedOffer(Dumplings, Money.FromCents(500)));
        Assert.Throws<ArgumentException>(() => new FoodAdvertisedOffer(ChoppedCheese, Money.FromCents(500)));
        Assert.Equal(FoodProducts.Burger, new FoodAdvertisedOffer(FoodProducts.Burger, Money.FromCents(500)).ProductId);
    }

    [Fact]
    public void HeldDynamicTuplesAtTheNewWriterMustBeAdmittedFoodNames()
    {
        var (city, _, _) = Ready(3, Policy(), new[] { Dumplings, FoodProducts.Burger }, new[] { ChoppedCheese });
        ToCapture(city);
        Assert.NotNull(SaveCodec.Write(city.State));
        var offer = HeldOffers(city).First(o => o.Metadata!.Revision == 2);
        var original = offer.Metadata!;
        offer.Metadata = new FoodOfferMetadata(2, FoodMealClass.Drink, original.TasteBand, original.BreakfastFitCents, original.LunchFitCents, original.DinnerFitCents);
        Assert.Contains("admitted Food name", Assert.Throws<SaveValidationException>(() => SaveCodec.Write(city.State)).Message);
        offer.Metadata = original;
        Assert.NotNull(SaveCodec.Write(city.State));
    }
}
