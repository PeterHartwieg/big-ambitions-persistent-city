using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

/// <summary>Offline model of the E5b3 Runtime protocol (Runtime/FoodContractDiagnostics.cs and the call sites in
/// NativeFoodMarket, NativeFoodCandidates and NativeFoodOwnership) over doubles of the native objects. Every decision
/// goes through the same Core types the game build uses: <see cref="FoodContractCoordinator"/>, the rechecks, the
/// reconciliation, <see cref="FoodContractBoundary"/>, <see cref="FoodAuthority"/> and the E5a builder. Only the native
/// reads are doubled. Lists are the real BCL List, whose _version moves like the game's; furniture uses a Mono-like
/// dictionary double (see FoodContractStampTests).</summary>
internal sealed class ContractWorld
{
    public const string Burger = FoodProducts.Burger, HotDog = FoodProducts.HotDog, Croissant = FoodProducts.Croissant;
    public const string ShelfItem = "ba:itemname_showcase", CounterItem = "ba:itemname_counter", BagItem = "ba:itemname_paperbag";
    public const string StationItem = "ba:itemname_grill", ShopType = "ba:businesstype_fastfood";
    public const string BagTag = "ba:itemtag_isbag", RegisterTag = "ba:itemtag_isfullservicecashregister", NeedsBags = "ba:businesstag_customersneedpaperbags";

    public sealed class Item
    {
        public string itemName = "";
        public bool retail, showcase, workstation, consumes = true, seasonal;
        public string[]? itemsThatCanShowcase, itemsToProduce, itemsWhitelist;
        public List<string>? tags = new List<string>();
        public int cargoCapacity = 1;
        // Mirrors LoadedCatalogueEligibilityReader.Facts field for field.
        public LoadedStockFacts Facts()
        {
            var valid = tags != null && tags.Count <= LoadedStockLimits.Rows ? tags : null;
            if (valid != null) for (int i = 0; i < valid.Count; i++) if (string.IsNullOrWhiteSpace(valid[i])) { valid = null; break; }
            bool rows = (itemsThatCanShowcase == null || itemsThatCanShowcase.Length <= LoadedStockLimits.Rows)
                && (itemsToProduce == null || itemsToProduce.Length <= LoadedStockLimits.Rows);
            return new LoadedStockFacts(itemName, retail, false, showcase, workstation, valid != null && rows, !seasonal,
                valid?.Contains(BagTag) == true, valid?.Contains(RegisterTag) == true, consumes,
                rows ? itemsThatCanShowcase : null, rows ? itemsToProduce : null,
                tags?.Count ?? -1, LoadedStockRows.Hash(tags), cargoCapacity, itemsWhitelist?.Length ?? -1, LoadedStockRows.Hash(itemsWhitelist));
        }
    }
    public sealed class FullService { }
    public sealed class BusinessType
    {
        public Type simulator = typeof(FullService);
        public bool hasEntranceFee;
        public List<string> tags = new List<string> { NeedsBags, "ba:businesstag_food" };
    }
    public sealed class Cargo { public string itemName = ""; public int amount; public List<Cargo>? nested = new List<Cargo>(); }
    public sealed class Holder { public string id = "", itemName = ""; public List<Cargo> cargoInstances = new List<Cargo>(); }
    public sealed class Price { public string itemName = ""; public float price; }
    /// <summary>Mono-like dictionary: _version moves on add, indexer set and remove.</summary>
    public sealed class Furniture
    {
        private readonly Dictionary<string, Holder> inner = new Dictionary<string, Holder>(StringComparer.Ordinal);
        private int _version;
        public int Count => inner.Count;
        public IEnumerable<Holder> Values => inner.Values;
        public void Add(Holder h) { _version++; inner.Add(h.id, h); }
        public bool Remove(string id) { if (!inner.Remove(id)) return false; _version++; return true; }
        public Holder this[string id] { get => inner[id]; set { _version++; inner[id] = value; } }
        public bool TryGetValue(string id, out Holder h) => inner.TryGetValue(id, out h!);
    }
    public sealed class Shop
    {
        public string Key = "";
        public List<string> Menu = new List<string>();
        public List<Price> Prices = new List<Price>();
        public Furniture Holders = new Furniture();
        public string BusinessTypeName = ShopType;
        public bool Rented = true, Established = true, ForRent, Closed, LiveCustomers;
        public FoodAuthority Row = new FoodAuthority();
        // Model of the reader's own image stamp: a mismatch at a visit requeues a rebuild (E5b2).
        public ShopContractStamp ReaderStamp;
        public bool Queued;
        public string Signature => "business:" + Key;
        public ShopContractStamp Stamp() => new ShopContractStamp(CollectionStamp.Of(MenuVersion, Menu, Menu.Count),
            CollectionStamp.Of(PriceVersion, Prices, Prices.Count), CollectionStamp.Of(FurnitureVersion, Holders, Holders.Count),
            BusinessTypeName, Rented, Established, ForRent, Closed);
        public Holder Holder(string id) => Holders[id];
        public Price PriceOf(string product) => Prices.Single(p => p.itemName == product);
    }

    internal static readonly CollectionVersionReader<List<string>> MenuVersion = Bound<List<string>>();
    internal static readonly CollectionVersionReader<List<Price>> PriceVersion = Bound<List<Price>>();
    internal static readonly CollectionVersionReader<Furniture> FurnitureVersion = Bound<Furniture>();
    internal static readonly CollectionVersionReader<List<Cargo>> CargoVersion = Bound<List<Cargo>>();
    private static CollectionVersionReader<T> Bound<T>() where T : class
        => CollectionVersionReader<T>.Bind(out var failure) ?? throw new InvalidOperationException(failure);

    public readonly Dictionary<string, Item> Items = new Dictionary<string, Item>(StringComparer.Ordinal);
    public readonly Dictionary<string, BusinessType> Types = new Dictionary<string, BusinessType>(StringComparer.Ordinal);
    public readonly List<Shop> Shops = new List<Shop>();
    public readonly LoadedCatalogueRevision Catalogue = new LoadedCatalogueRevision();
    public readonly FoodContractCoordinator Coordinator = new FoodContractCoordinator();
    public readonly FoodDefinitionEpoch CaptureEpoch = new FoodDefinitionEpoch(), PreparationEpoch = new FoodDefinitionEpoch(), BoundaryEpoch = new FoodDefinitionEpoch();
    public readonly Dictionary<string, long> Refused = new Dictionary<string, long>(StringComparer.Ordinal);
    public readonly List<string> Orders = new List<string>();
    public readonly List<FoodShopDay> History = new List<FoodShopDay>();
    public FoodReferencedNames Names = new FoodReferencedNames();
    private readonly Queue<Shop> rebuilds = new Queue<Shop>();
    public long Hour;
    public bool Active = true;
    /// <summary>Runtime's warming mode after a load: E5b1 rules until every shop has an image; acquisitions deferred.</summary>
    public bool Warming;
    public int MenuLimit = FoodMenuPolicy.LiveMaximumOffers;
    public int CatalogueWaits, PriceOnlyRefusals;
    public int Recopies, FullRebuilds, Discards, Publishes, GateRuns, RecoveryUnits, MaxRecoveriesInFrame, Reconciliations;
    public FoodReconciliationRefusal LastReconciliation;
    /// <summary>Runs between the passes's start stamp and its publish (a write during a multi-frame pass).</summary>
    public Action<Shop>? DuringPass;
    /// <summary>Runs after the drain and before the commit gates (a write between drain and commit).</summary>
    public Action? AfterDrain;
    /// <summary>Runs inside a checkout after native setup, before the second recheck.</summary>
    public Action? AfterNativeSetup;

    public ContractWorld(long hour = 100)
    {
        Hour = hour;
        Add(new Item { itemName = Burger, retail = true });
        Add(new Item { itemName = HotDog, retail = true });
        Add(new Item { itemName = Croissant, retail = true });
        Add(new Item { itemName = BagItem, tags = new List<string> { BagTag } });
        Add(new Item { itemName = ShelfItem, showcase = true, itemsThatCanShowcase = new[] { Burger, HotDog, Croissant }, itemsToProduce = null });
        Add(new Item { itemName = CounterItem, tags = new List<string> { RegisterTag } });
        Add(new Item { itemName = StationItem, workstation = true, itemsToProduce = new[] { Burger } });
        Types.Add(ShopType, new BusinessType());
        RebuildCatalogue();
    }
    public void Add(Item item) => Items[item.itemName] = item;
    public void RebuildCatalogue()
    {
        if (!Catalogue.Dirty) Catalogue.MarkDirty();
        var building = Catalogue.Begin();
        foreach (var item in Items.Values.OrderBy(i => i.itemName, StringComparer.Ordinal)) building.Add(new LoadedStockDefinition(item.Facts()));
        Catalogue.Publish();
        foreach (var s in Shops) Requeue(s);
    }

    public Shop AddShop(string key, params string[] menu)
    {
        var s = new Shop { Key = key };
        s.Row.PlaceId = key;
        foreach (var product in menu)
        {
            s.Menu.Add(product);
            s.Prices.Add(new Price { itemName = product, price = 5 });
            s.Holders.Add(new Holder { id = key + ":shelf:" + product, itemName = ShelfItem,
                cargoInstances = new List<Cargo> { new Cargo { itemName = product, amount = 10 } } });
        }
        s.Holders.Add(new Holder { id = key + ":counter", itemName = CounterItem, cargoInstances = new List<Cargo> { new Cargo { itemName = BagItem, amount = 20 } } });
        Shops.Add(s);
        return s;
    }
    public Shop Find(string key) => Shops.Single(s => s.Key == key);

    /// <summary>E5b5 Decision 12: every runtime capability passed at city start (Runtime contractCapabilities.AllPassed).</summary>
    public bool CapabilitiesPassed = true;
    /// <summary>The market's product policy (Runtime ProductPolicySource); null stands for an unset source.</summary>
    public FoodProductPolicy? Policy;
    public bool DynamicAcquisition => Active && !Warming && CapabilitiesPassed;
    /// <summary>When set, the real E5b4 source over this world's catalogue revision supplies the policy (Runtime wiring).</summary>
    public FoodProductPolicySource? PolicySource;
    private FoodProductPolicy? observedPolicy;
    private readonly FoodLivePolicy livePolicy = new FoodLivePolicy();
    // Runtime CurrentProductPolicy: a rebuilding catalogue keeps the last policy that had loaded names.
    private FoodProductPolicy CurrentPolicy => livePolicy.Resolve(PolicySource?.Current ?? Policy ?? FoodProductPolicy.OriginalThree,
        Catalogue.Names == null);
    // Runtime LiveMenu: an all-original menu never reads the policy.
    public FoodLiveMenuRefusal LiveMenu(Shop s)
        => FoodMenuPolicy.LiveMenu(s.Menu, s.Menu.All(FoodProducts.Known) || !DynamicAcquisition ? null : CurrentPolicy);
    // Runtime LiveMenu(registration): a shop the save already owns keeps service through startup (warming).
    public FoodLiveMenuRefusal ServiceMenu(Shop s)
    {
        var menu = LiveMenu(s);
        if (menu != FoodLiveMenuRefusal.OriginalThreeGuard || !Warming || !s.Row.Managed) return menu;
        return FoodMenuPolicy.LiveMenu(s.Menu, CurrentPolicy, loadedNamesPending: true);
    }
    /// <summary>Runtime AwaitingValidation (StartupHeld): ownership kept, but no read, capture or sale until validated.</summary>
    public bool AwaitingValidation(Shop s) => Warming && s.Row.Managed && LiveMenu(s) == FoodLiveMenuRefusal.OriginalThreeGuard;
    // E5b5 SupportedBusiness: Core LiveMenu (the original-three guard only while dynamic acquisition is off).
    public bool SupportedBusiness(Shop s)
    {
        if (!s.Rented || !s.Established || s.ForRent) return false;
        if (ServiceMenu(s) != FoodLiveMenuRefusal.None) return false;
        return Types.TryGetValue(s.BusinessTypeName, out var t) && t.simulator == typeof(FullService) && !t.hasEntranceFee && CachedBags(t);
    }
    // Runtime's authoredBagPolicies: read once per type, re-cached only by a business-type re-copy or RefreshBagPolicy.
    public readonly Dictionary<BusinessType, bool> BagPolicies = new Dictionary<BusinessType, bool>();
    private bool CachedBags(BusinessType t)
    {
        if (!BagPolicies.TryGetValue(t, out bool bags)) BagPolicies[t] = bags = t.tags.Contains(NeedsBags);
        return bags;
    }
    private void RefreshBagPolicy(Shop s)
    {
        if (Types.TryGetValue(s.BusinessTypeName, out var t) && BagPolicies.TryGetValue(t, out bool cached) && cached != t.tags.Contains(NeedsBags))
            BagPolicies[t] = t.tags.Contains(NeedsBags);
    }
    public bool SupportedMenu(Shop s)
    {
        if (Refused.ContainsKey(s.Key)) return false;
        if (!SupportedBusiness(s)) { RefuseRemovedProduct(s); return false; }
        return s.Menu.Count <= MenuLimit && ContractAdmits(s);
    }
    // Runtime RefuseRemovedProduct: a product leaving the loaded image or the Food class under an owned or wanted shop.
    private void RefuseRemovedProduct(Shop s)
    {
        if (!DynamicAcquisition || !s.Row.Managed && !s.Row.Desired) return;
        var menu = LiveMenu(s);
        if (menu != FoodLiveMenuRefusal.NotLoaded && menu != FoodLiveMenuRefusal.MealClass) return;
        if (!Refused.ContainsKey(s.Key)) Refused[s.Key] = Hour;
        Coordinator.Refuse(s.Key, Hour);
    }
    // Runtime ContractCandidate: the live food-shop shape without the cached bag policy.
    public bool Candidate(Shop s) => s.Rented && s.Established && !s.ForRent && LiveMenu(s) == FoodLiveMenuRefusal.None;
    private bool Interest(Shop s) => s.Row.Managed || s.Row.Desired || Candidate(s);
    private void RefuseStructural(Shop s)
    {
        Coordinator.Invalidate(s.Key);
        if (!Interest(s)) return;
        if (!Refused.ContainsKey(s.Key)) Refused[s.Key] = Hour;
        Coordinator.Refuse(s.Key, Hour);
    }
    public bool ContractAdmits(Shop s)
    {
        if (!Active) return true;
        var image = Coordinator.Serving(s.Key);
        if (image == null) return false;
        if (!(image.Stamp is ShopContractStamp st) || !s.Stamp().Matches(st)) { RefuseStructural(s); return false; }
        var catalogue = Catalogue.Catalogue;
        if (catalogue != null && Coordinator.VerifiedCatalogue(s.Key) != catalogue.Revision)
        {
            if (!FoodContractRecheck.CatalogueCurrent(image, catalogue)) { RefuseStructural(s); return false; }
            Coordinator.Rebase(s.Key, catalogue.Revision);
        }
        return true;
    }
    /// <summary>Replaces the registration at a shop's place (a new business lifetime): Runtime IndexContractKey.</summary>
    public Shop ReplaceRegistration(Shop old)
    {
        var s = new Shop { Key = old.Key, Menu = new List<string>(old.Menu), Prices = old.Prices.Select(p => new Price { itemName = p.itemName, price = p.price }).ToList() };
        foreach (var h in old.Holders.Values)
            s.Holders.Add(new Holder { id = h.id, itemName = h.itemName, cargoInstances = h.cargoInstances.Select(c => new Cargo { itemName = c.itemName, amount = c.amount }).ToList() });
        s.Row = old.Row;
        s.Row.Observe(s.Signature, false, Hour, reset: true); // FoodAuthority's replacement rule (generation + 1)
        Shops[Shops.IndexOf(old)] = s;
        Refused.Remove(old.Key); // refusedContracts is keyed by the old registration object
        Coordinator.Remove(old.Key);
        return s;
    }

    // Existing discovery visit: the reader's stamp check, then Observe with SupportedMenu.
    public void Visit(Shop s)
    {
        if (!s.Queued && !s.ReaderStamp.Matches(s.Stamp())) Requeue(s);
        s.Row.Observe(s.Signature, SupportedMenu(s), Hour);
    }
    public void VisitAll() { foreach (var s in Shops) Visit(s); }
    public void Requeue(Shop s) { if (s.Queued) return; s.Queued = true; rebuilds.Enqueue(s); }
    public int PendingRebuilds => rebuilds.Count;

    public void Refuse(Shop s)
    {
        if (!Refused.ContainsKey(s.Key)) Refused[s.Key] = Hour;
        if (Active) Coordinator.Refuse(s.Key, Hour);
        s.Row.Observe(s.Signature, false, Hour);
    }

    // Decision 4 step 1 (RefreshKnownOwnership at an advancing hour).
    public void BeginHour(long hour)
    {
        Hour = hour;
        Coordinator.BeginHour(hour); PreparationEpoch.Begin();
        foreach (var s in Shops)
        {
            s.Row.Observe(s.Signature, SupportedMenu(s), Hour);
            bool refused = Refused.ContainsKey(s.Key);
            if (!s.Row.Managed && !s.Row.Desired && !refused) continue;
            Coordinator.Prepare(s.Key);
            if (refused) Coordinator.RequeueRecovery(s.Key);
        }
    }

    /// <summary>One frame: preparation units, recovery units and at most <paramref name="units"/> shop passes.</summary>
    public void Frame(int units = 64)
    {
        // Runtime ObserveProductPolicy: a new policy publication rebuilds every shop.
        var policy = PolicySource?.Current ?? Policy;
        if ((Active || Warming) && policy != null && !ReferenceEquals(policy, observedPolicy))
        {
            if (observedPolicy != null) foreach (var s in Shops) Requeue(s);
            observedPolicy = policy;
        }
        for (int i = 0; i < 64 && Coordinator.TryNextPrepare(out var key); i++) PrepareUnit(Find(key));
        if (Active && Coordinator.PendingRecoveries > 0)
        {
            var due = new List<string>();
            Coordinator.TakeRecoveries(Hour, due);
            MaxRecoveriesInFrame = Math.Max(MaxRecoveriesInFrame, due.Count);
            foreach (var key in due) { RecoveryUnits++; Recover(Find(key)); }
        }
        for (int i = 0; i < units && rebuilds.Count > 0; i++) { var s = rebuilds.Dequeue(); s.Queued = false; Pass(s); }
        FinishWarming();
    }
    /// <summary>Exactly one unit: a preparation unit, else one shop pass.</summary>
    public bool Step()
    {
        if (Coordinator.TryNextPrepare(out var key)) { PrepareUnit(Find(key)); return true; }
        if (rebuilds.Count == 0) return false;
        var s = rebuilds.Dequeue(); s.Queued = false; Pass(s); FinishWarming(); return true;
    }
    private void FinishWarming()
    {
        if (!Warming || rebuilds.Count > 0 || Shops.Any(s => Coordinator.Image(s.Key) == null)) return;
        Warming = false; Active = true;
        foreach (var s in Shops) if (Coordinator.Image(s.Key) is { Passed: false } && Interest(s)) Refuse(s);
    }
    public void Drain(int units = 64) { for (int i = 0; i < 10000 && (rebuilds.Count > 0 || Coordinator.PendingPreparations > 0 || Coordinator.PendingRecoveries > 0); i++) Frame(units); }

    private void PrepareUnit(Shop s)
    {
        var image = Coordinator.Image(s.Key);
        var live = new Live(this, s); var changed = new List<FoodDefinitionDependency>();
        if (image != null && image.Passed)
        {
            var refusal = FoodContractRecheck.Definitions(image, live, PreparationEpoch, changed);
            if (refusal == FoodContractRecheckRefusal.None) refusal = FoodContractRecheck.BusinessType(image, live);
            if (refusal != FoodContractRecheckRefusal.None) { Recopy(changed, refusal); if (Active) Refuse(s); }
        }
        RefreshBagPolicy(s);
        var stale = new List<object>();
        if (Catalogue.Catalogue != null && FoodContractRecheck.StaleCopies(Coordinator.Watched(s.Key), Catalogue.Catalogue, live, PreparationEpoch, stale) > 0)
        {
            var copies = stale.Select(o => new LoadedStockDefinition(((Item)o).Facts())).ToArray();
            if (Catalogue.Replace(copies)) Recopies += copies.Length; else { FullRebuilds++; RebuildCatalogue(); }
            foreach (var other in Shops) Requeue(other);
        }
        Requeue(s);
    }

    // The E5a reader pass followed by OnContractPublished.
    private void Pass(Shop s)
    {
        if (!Shops.Contains(s)) return; // a replaced registration: Runtime drops its pass (registrations[key] differs)
        var start = s.Stamp();
        var catalogue = Catalogue.Catalogue!;
        var type = Types.TryGetValue(s.BusinessTypeName, out var t) ? t : null;
        bool business = s.Rented && s.Established && !s.ForRent && !s.Closed && type != null && type.simulator == typeof(FullService)
            && !type.hasEntranceFee && type.tags.Contains(NeedsBags);
        var builder = new LoadedStockMenuBuilder(catalogue, Hour, business, s.Menu,
            s.Prices.Select(p => new StockPriceFields(p.itemName, p.price)).ToArray(), MenuLimit, s.Holders.Count, true);
        var passNames = new List<string>(s.Menu);
        foreach (var h in s.Holders.Values)
        {
            var cargo = h.cargoInstances.Select(c => new StockCargoFields(c.itemName, c.amount, c.nested?.Count ?? -1)).ToArray();
            foreach (var c in h.cargoInstances) if (!passNames.Contains(c.itemName)) passNames.Add(c.itemName);
            if (!passNames.Contains(h.itemName)) passNames.Add(h.itemName);
            builder.AddHolder(h.id, h.id, h.itemName, true, cargo);
        }
        var eligibility = builder.Finish();
        s.ReaderStamp = start;
        DuringPass?.Invoke(s);
        Publish(s, eligibility, start, passNames);
    }
    private void Publish(Shop s, FoodEligibilityImage eligibility, ShopContractStamp start, List<string> passNames)
    {
        if (!s.Stamp().Matches(start)) { Discards++; return; }
        var catalogue = Catalogue.Catalogue;
        if (catalogue == null || catalogue.Revision != eligibility.CatalogueRevision) { Discards++; return; }
        var live = new Live(this, s);
        var holders = new FoodHolderSlot[eligibility.Holders.Count];
        for (int i = 0; i < holders.Length; i++)
        {
            var h = eligibility.Holders[i];
            if (!live.ReadHolder(new FoodHolderSlot(h.HolderId, h.DefinitionId, h.ResourceId, h.IsBagCounter, null, null, 0, 0, null, null, 0), out holders[i])
                || !holders[i].Shaped || holders[i].DefinitionId != h.DefinitionId) { Discards++; Requeue(s); return; }
        }
        var definitions = Array.Empty<FoodDefinitionDependency>();
        FoodBusinessTypeCopy? business = null;
        var admission = CompleteFoodMenuRefusal.None;
        if (eligibility.StockContractMatched)
        {
            var names = new List<string>();
            foreach (var o in eligibility.Offers) if (!names.Contains(o.ProductId)) names.Add(o.ProductId);
            foreach (var h in eligibility.Holders) { if (!names.Contains(h.DefinitionId)) names.Add(h.DefinitionId); if (!names.Contains(h.ResourceId)) names.Add(h.ResourceId); }
            int participating = names.Count;
            foreach (var n in passNames) if (!names.Contains(n)) names.Add(n);
            var list = new List<FoodDefinitionDependency>();
            var stale = new List<FoodDefinitionDependency>();
            for (int i = 0; i < names.Count; i++)
            {
                if (!Items.TryGetValue(names[i], out var item) || !catalogue.TryGet(names[i], out var copy))
                { if (i >= participating) continue; Discards++; Requeue(s); return; }
                var d = new FoodDefinitionDependency(names[i], item, copy);
                list.Add(d);
                if (!copy.Matches(item.Facts())) stale.Add(d);
            }
            if (stale.Count > 0) { Discards++; Recopy(stale, FoodContractRecheckRefusal.DefinitionContent); return; }
            definitions = list.ToArray();
            admission = Admission(s, eligibility, eligibility.CatalogueRevision == Catalogue.Revision);
        }
        else definitions = passNames.Where(n => Items.ContainsKey(n) && catalogue.TryGet(n, out _))
            .Select(n => { catalogue.TryGet(n, out var copy); return new FoodDefinitionDependency(n, Items[n], copy); }).ToArray();
        if (live.ReadBusinessType(out var facts)) business = new FoodBusinessTypeCopy(facts);
        var image = new FoodContractImage(s.Key, Hour, eligibility, holders, definitions, business, admission, start);
        var serving = Coordinator.Serving(s.Key);
        bool staleCatalogue = Active && serving != null && (Coordinator.VerifiedCatalogue(s.Key) != catalogue.Revision
            && !FoodContractRecheck.CatalogueCurrent(serving, catalogue) || !FoodContractRecheck.ReferencesCurrent(serving, live));
        Coordinator.Publish(image); Publishes++;
        if (staleCatalogue && Interest(s)) Refuse(s);
        else if (!image.Passed && Active && Interest(s)) Refuse(s);
    }
    public CompleteFoodMenuRefusal Admission(Shop s, FoodEligibilityImage eligibility, bool current)
    {
        var products = new List<string>();
        CompleteFoodMenu.HistoryProducts(History, s.Key, s.Row.BusinessId, (int)(Hour / 24), products);
        // Runtime AdmissionResolver: the policy's resolver once the capabilities passed, also while warming.
        return CompleteFoodMenu.Check(eligibility, CapabilitiesPassed ? CurrentPolicy.Resolver : FoodProductMetadataResolver.Empty,
            current, true, products, Names.Names);
    }
    private void Recopy(List<FoodDefinitionDependency> changed, FoodContractRecheckRefusal refusal)
    {
        if (changed.Count > 0)
        {
            var copies = changed.Select(d => new LoadedStockDefinition(((Item)d.Live).Facts())).ToArray();
            if (Catalogue.Replace(copies)) Recopies += copies.Length; else { FullRebuilds++; RebuildCatalogue(); }
            foreach (var s in Shops) Requeue(s);
        }
        else if (refusal == FoodContractRecheckRefusal.DefinitionReference) { FullRebuilds++; RebuildCatalogue(); }
        else if (refusal == FoodContractRecheckRefusal.BusinessType) { BagPolicies.Clear(); foreach (var s in Shops) Requeue(s); }
    }

    // Decision 3 at a capture (checkout=false) or after native setup at checkout.
    public bool Recheck(Shop s, bool checkout)
    {
        if (!Active) return true;
        var image = Coordinator.Serving(s.Key);
        if (image == null) return false;
        if (Catalogue.Catalogue == null) { CatalogueWaits++; return false; } // the sale waits; no refusal
        if (checkout) CaptureEpoch.Begin();
        var live = new Live(this, s); var changed = new List<FoodDefinitionDependency>();
        var refusal = FoodContractRecheck.Structure(image, live, CaptureEpoch, changed);
        var current = new Money[8];
        var prices = refusal == FoodContractRecheckRefusal.None ? FoodContractRecheck.Prices(image, live, current) : FoodPriceVerdict.Same;
        if (refusal == FoodContractRecheckRefusal.None && prices != FoodPriceVerdict.Refused)
        {
            if (prices == FoodPriceVerdict.PriceOnly) Coordinator.Replace(image, image.WithPrices(current.Take(image.Prices.Count).ToArray()));
            return true;
        }
        if (refusal != FoodContractRecheckRefusal.None) Recopy(changed, refusal);
        Refuse(s);
        return false;
    }

    public sealed class Capture
    {
        public Capture(Shop shop, Dictionary<string, Money> prices) { Shop = shop; Prices = prices; }
        public Shop Shop { get; }
        public Dictionary<string, Money> Prices { get; }
    }
    /// <summary>A selected capture: SupportedMenu, CanServe, the Decision 3 recheck, then live prices.</summary>
    public Capture? Select(Shop s)
    {
        if (AwaitingValidation(s)) return null; // Runtime MenuStatus: StartupValidationRefusal
        CaptureEpoch.Begin();
        if (!SupportedMenu(s)) { s.Row.Observe(s.Signature, false, Hour); return null; }
        if (!s.Row.CanServe || !Recheck(s, false)) return null;
        if (!Supply(s, out _)) { Refuse(s); return null; }
        return new Capture(s, s.Menu.ToDictionary(p => p, p => Money.FromNative(s.PriceOf(p).price)));
    }
    /// <summary>Checkout: recheck, supply with reconciliation, native setup, recheck again, exact approved price,
    /// then the receipt. Null on a refusal before the receipt.</summary>
    public string? Checkout(Capture c, string product, string intent)
    {
        var s = c.Shop;
        if (Orders.Contains(intent)) return intent;
        if (AwaitingValidation(s)) return null; // Runtime BuyLive: StartupValidationRefusal
        if (!SupportedMenu(s)) { s.Row.Observe(s.Signature, false, Hour); return null; }
        if (!s.Row.CanServe || !Recheck(s, true)) return null;
        int reconciled = Reconciliations;
        if (!Supply(s, out var units)) { if (Reconciliations != reconciled) Refuse(s); return null; }
        if (units[product] <= 0) return null;
        // After native setup (Runtime CompletePrices, then Decision 3 again). E5b5 Q2: a different valid price is
        // price-only; the sale is refused below without a hand-back.
        var verdict = FoodPriceVerdict.Same;
        if (s.Menu.Count != c.Prices.Count) { Refuse(s); return null; }
        foreach (var p in s.Menu)
        {
            var rows = s.Prices.Where(r => r.itemName == p).ToArray();
            if (!c.Prices.ContainsKey(p) || rows.Length != 1 || !Money.TryFromNative(rows[0].price, out var live)) { Refuse(s); return null; }
            if (live != c.Prices[p]) verdict = FoodPriceVerdict.PriceOnly;
        }
        AfterNativeSetup?.Invoke();
        // Runtime SellLine: a rebuilding catalogue waits; any other false here is RefuseCheckoutContract.
        if (Active && Catalogue.Catalogue == null) return null;
        if (!SupportedMenu(s)) { s.Row.Observe(s.Signature, false, Hour); return null; }
        if (!Recheck(s, true)) { Refuse(s); return null; }
        if (verdict == FoodPriceVerdict.PriceOnly) { PriceOnlyRefusals++; return null; }
        Orders.Add(intent);
        var stock = s.Holders.Values.First(h => h.cargoInstances.Count == 1 && h.cargoInstances[0].itemName == product && h.cargoInstances[0].amount > 0);
        stock.cargoInstances[0].amount--; // W1 in place: quantity only
        return intent;
    }
    /// <summary>Emulated native setup: showcases index their stocked product when amount &gt; 0, workstations index
    /// every itemsToProduce entry, staffed counters are points of sale. Then Decision 5.</summary>
    public bool Supply(Shop s, out Dictionary<string, int> units)
    {
        units = s.Menu.ToDictionary(p => p, _ => 0);
        var native = NativeSetup(s);
        var image = Active ? Coordinator.Serving(s.Key) : null;
        if (image != null)
        {
            var refusal = FoodHolderReconciliation.Check(image, native, s.Menu, stock => ((Cargo)stock).amount);
            if (refusal != FoodReconciliationRefusal.None) { Reconciliations++; LastReconciliation = refusal; return false; }
        }
        foreach (var n in native) if (!n.Counter && n.Product != null && units.ContainsKey(n.Product))
            units[n.Product] += ((Holder)n.Instance).cargoInstances.Where(c => c.itemName == n.Product).Sum(c => c.amount);
        return true;
    }
    public List<FoodNativeHolder> NativeSetup(Shop s)
    {
        var native = new List<FoodNativeHolder>();
        foreach (var h in s.Holders.Values)
        {
            if (!Items.TryGetValue(h.itemName, out var d)) continue;
            if (d.tags?.Contains(RegisterTag) == true && !UnstaffedCounters.Contains(h.id)) native.Add(new FoodNativeHolder(h, null, false, true));
            if (d.showcase && h.cargoInstances.Count >= 1 && h.cargoInstances[0].amount > 0 && s.Menu.Contains(h.cargoInstances[0].itemName)
                && !OmittedByNative.Contains(h.id))
                native.Add(new FoodNativeHolder(h, h.cargoInstances[0].itemName, d.workstation, false));
            if (d.workstation && d.itemsToProduce != null)
                foreach (var p in d.itemsToProduce) if (s.Menu.Contains(p)) native.Add(new FoodNativeHolder(h, p, true, false));
        }
        native.AddRange(ExtraNative);
        return native;
    }
    public readonly HashSet<string> UnstaffedCounters = new HashSet<string>(StringComparer.Ordinal);
    public readonly HashSet<string> OmittedByNative = new HashSet<string>(StringComparer.Ordinal);
    public readonly List<FoodNativeHolder> ExtraNative = new List<FoodNativeHolder>();

    // Decision 7 recovery unit.
    private void Recover(Shop s)
    {
        if (!Refused.TryGetValue(s.Key, out long refusedHour) || Hour <= refusedHour) return;
        bool handoff = !s.Row.Managed && !s.Row.OwnsHour(Hour);
        if (Coordinator.RecoveryGate(s.Key, Hour, handoff, s.LiveCustomers) != FoodRecoveryRefusal.None) return;
        var image = Coordinator.Image(s.Key)!;
        if (!SupportedBusiness(s) || s.Closed || !(image.Stamp is ShopContractStamp st) || !s.Stamp().Matches(st)) return;
        var epoch = new FoodDefinitionEpoch(); epoch.Begin();
        var live = new Live(this, s);
        if (FoodContractRecheck.Structure(image, live, epoch) != FoodContractRecheckRefusal.None
            || FoodContractRecheck.Prices(image, live) == FoodPriceVerdict.Refused) return;
        var native = NativeSetup(s);
        if (FoodHolderReconciliation.Check(image, native, s.Menu, stock => ((Cargo)stock).amount) != FoodReconciliationRefusal.None) return;
        Refused.Remove(s.Key); Coordinator.Recovered(s.Key);
    }

    /// <summary>AfterNativeTick at an advancing hour: refresh and preparation queueing, drain, refresh, commit in order.</summary>
    public List<(string Key, FoodContractBoundary.Outcome Outcome)> Boundary(long hour, bool drained = true)
    {
        BeginHour(hour);
        var result = new List<(string, FoodContractBoundary.Outcome)>();
        if (!drained) return result;
        AfterDrain?.Invoke();
        VisitAll();
        BoundaryEpoch.Begin();
        foreach (var s in Shops.OrderBy(s => s.Row, Comparer<FoodAuthority>.Create(FoodContractBoundary.Order)).ToArray())
        {
            FoodContractBoundary.Outcome outcome;
            if (Warming)
            {
                if (s.Row.Pending(hour, true, s.LiveCustomers, true) != FoodBoundaryStep.Acquire)
                    outcome = s.Row.Boundary(hour, true, s.LiveCustomers, true) ? FoodContractBoundary.Outcome.HandedBack : FoodContractBoundary.Outcome.None;
                else { s.Row.Boundary(hour, true, s.LiveCustomers, true, false); outcome = FoodContractBoundary.Outcome.Deferred; }
            }
            else outcome = FoodContractBoundary.Commit(s.Row, Active ? Coordinator : null, hour, s.LiveCustomers, true, Catalogue.Revision, Gate);
            if (outcome != FoodContractBoundary.Outcome.None) result.Add((s.Key, outcome));
        }
        return result;
    }
    private bool Gate(FoodAuthority row, FoodContractImage image)
    {
        GateRuns++;
        var s = Find(row.PlaceId);
        if (!(image.Stamp is ShopContractStamp st) || !s.Stamp().Matches(st))
        { RefuseStructural(s); s.Row.Observe(s.Signature, false, Hour); return false; }
        var live = new Live(this, s); var changed = new List<FoodDefinitionDependency>();
        var refusal = FoodContractRecheck.Structure(image, live, BoundaryEpoch, changed);
        var current = new Money[8];
        var prices = refusal == FoodContractRecheckRefusal.None ? FoodContractRecheck.Prices(image, live, current) : FoodPriceVerdict.Same;
        var admission = refusal == FoodContractRecheckRefusal.None && prices != FoodPriceVerdict.Refused
            ? Admission(s, image.Eligibility, Coordinator.VerifiedCatalogue(s.Key) == Catalogue.Revision) : CompleteFoodMenuRefusal.None;
        if (refusal == FoodContractRecheckRefusal.None && prices != FoodPriceVerdict.Refused && admission == CompleteFoodMenuRefusal.None
            && SupportedBusiness(s) && !Refused.ContainsKey(s.Key))
        {
            if (prices == FoodPriceVerdict.PriceOnly) Coordinator.Replace(image, image.WithPrices(current.Take(image.Prices.Count).ToArray()));
            return true;
        }
        if (refusal != FoodContractRecheckRefusal.None) Recopy(changed, refusal);
        Refuse(s);
        return false;
    }

    /// <summary>Discovery, a full rebuild, then boundaries until every listed shop is owned.</summary>
    public void Own(params Shop[] shops)
    {
        VisitAll(); Drain(); VisitAll();
        for (int i = 0; i < 6 && shops.Any(s => !s.Row.CanServe); i++) { Boundary(Hour + 1); Drain(); VisitAll(); }
        Assert.All(shops, s => Assert.True(s.Row.CanServe, s.Key + " was not acquired"));
    }

    public sealed class Live : IFoodContractLive
    {
        private readonly ContractWorld world; private readonly Shop shop;
        public Live(ContractWorld world, Shop shop) { this.world = world; this.shop = shop; }
        public bool ReadHolder(in FoodHolderSlot image, out FoodHolderSlot live)
        {
            live = default;
            if (!shop.Holders.TryGetValue(image.HolderId, out var h)) return false;
            var cargo = h.cargoInstances; var stock = cargo.Count > 0 ? cargo[0] : null;
            live = new FoodHolderSlot(image.HolderId, h.itemName, image.ResourceId, image.IsBagCounter, h, cargo, CargoVersion.Read(cargo),
                cargo.Count, stock, stock?.itemName, stock == null ? 0 : stock.nested?.Count ?? -1);
            return true;
        }
        public bool ReadDefinition(string name, out object? live, out LoadedStockFacts facts)
        {
            if (world.Items.TryGetValue(name, out var item)) { live = item; facts = item.Facts(); return true; }
            live = null; facts = default; return false;
        }
        public bool ReadBusinessType(out FoodBusinessTypeFacts live)
        {
            if (!world.Types.TryGetValue(shop.BusinessTypeName, out var t)) { live = default; return false; }
            live = new FoodBusinessTypeFacts(t, t.simulator, t.hasEntranceFee, t.tags); return true;
        }
        public int PriceRows(string product, out float price)
        {
            price = 0; int rows = 0;
            foreach (var p in shop.Prices) if (p.itemName == product) { rows++; price = p.price; }
            return rows;
        }
    }
}

/// <summary>E5b3 Decisions 2-4, 3a, 6, 8 and 13: atomic images, rechecks, catalogue re-copy, the boundary protocol
/// and the capacity preflight (docs/plans/e5b-remaining.md section 4, E5b3 row).</summary>
public sealed class FoodContractCoordinatorTests
{
    private readonly ITestOutputHelper output;
    public FoodContractCoordinatorTests(ITestOutputHelper output) { this.output = output; }
    private const string Burger = ContractWorld.Burger, HotDog = ContractWorld.HotDog;

    private static (ContractWorld World, ContractWorld.Shop Shop) Owned(params string[] menu)
    {
        var world = new ContractWorld();
        var shop = world.AddShop("address:main:1:shop", menu.Length == 0 ? new[] { Burger, HotDog } : menu);
        world.Own(shop);
        return (world, shop);
    }

    // Section 2.2 writer rows, each applied between a capture and its checkout.
    // W10-price (a valid in-place price) is Q2's price-only change since E5b5: DynamicAcquisitionTests covers it.
    public static IEnumerable<object[]> WriterRows() => new[] { "W3", "W4", "W5", "W6", "W7", "W8", "W9", "W10-row", "W11-field", "W11-unregister" }
        .Select(w => new object[] { w });
    private static void Write(ContractWorld world, ContractWorld.Shop shop, string row)
    {
        var shelf = shop.Holder(shop.Key + ":shelf:" + Burger);
        switch (row)
        {
            case "W3": shop.Holders.Remove(shop.Key + ":shelf:" + HotDog); break; // emptied container removed
            case "W4": shelf.cargoInstances.Add(new ContractWorld.Cargo { itemName = Burger, amount = 1 }); break;
            case "W5": shelf.cargoInstances.RemoveAt(0); break;
            case "W6": shelf.cargoInstances = new List<ContractWorld.Cargo> { new ContractWorld.Cargo { itemName = Burger, amount = 10 } }; break;
            case "W7": shelf.cargoInstances[0].itemName = HotDog; break; // in place, no list change
            case "W8": shop.Menu = new List<string>(shop.Menu); break;
            case "W9": shop.Holders.Add(new ContractWorld.Holder { id = "added", itemName = ContractWorld.ShelfItem }); break;
            case "W10-row": shop.Prices.Add(new ContractWorld.Price { itemName = ContractWorld.Croissant, price = 3 }); break;
            case "W10-price": shop.PriceOf(Burger).price = 6; break; // in place
            case "W11-field": world.Items[ContractWorld.ShelfItem].consumes = false; break; // in place on a loaded Item
            case "W11-unregister": world.Items.Remove(ContractWorld.BagItem); break;
            default: throw new ArgumentException(row);
        }
    }

    [Theory]
    [MemberData(nameof(WriterRows))]
    public void EachWriterRowBetweenCaptureAndCheckoutRefusesBeforeTheReceipt(string row)
    {
        var (world, shop) = Owned();
        var capture = world.Select(shop)!;
        Assert.NotNull(capture);
        Write(world, shop, row);
        Assert.Null(world.Checkout(capture, Burger, "intent-1"));
        Assert.Empty(world.Orders);
        Assert.False(shop.Row.Desired); // desire withdrawn at once; hand-back waits for the boundary
        Assert.True(shop.Row.Managed);
        Assert.Contains(world.Boundary(world.Hour + 1), o => o.Key == shop.Key && o.Outcome == FoodContractBoundary.Outcome.HandedBack);
    }

    [Fact]
    public void AStructuralChangeWithAPassingSameHourRebuildStaysRefusedUntilSafeRecovery()
    {
        var (world, shop) = Owned();
        long hour = world.Hour;
        shop.Menu = new List<string>(shop.Menu); // W8: a valid replacement, same products
        world.VisitAll();
        Assert.False(shop.Row.Desired);
        Assert.True(world.Refused.ContainsKey(shop.Key));
        world.Drain(); world.VisitAll(); // the same-hour rebuild passes
        Assert.True(world.Coordinator.Image(shop.Key)!.Passed);
        Assert.Equal(FoodContractPhase.Refused, world.Coordinator.Phase(shop.Key));
        Assert.False(shop.Row.Desired); // never back to service in the hour it changed
        Assert.True(shop.Row.Managed);
        Assert.Equal(hour, world.Hour);
        Assert.Contains(world.Boundary(world.Hour + 1), o => o.Key == shop.Key && o.Outcome == FoodContractBoundary.Outcome.HandedBack);
        world.Drain(); world.VisitAll();
        Assert.False(world.Refused.ContainsKey(shop.Key)); // recovered after an advancing hour and the completed hand-back
        Assert.True(shop.Row.Desired);
        Assert.Contains(world.Boundary(world.Hour + 1), o => o.Key == shop.Key && o.Outcome == FoodContractBoundary.Outcome.Acquired);
    }

    [Fact]
    public void ANonparticipatingFurnitureDefinitionIsWatchedAndItsInPlaceEditIsCaught()
    {
        var world = new ContractWorld();
        const string Oven = "ba:itemname_oven";
        world.Add(new ContractWorld.Item { itemName = Oven, workstation = true, itemsToProduce = new[] { ContractWorld.Croissant } });
        world.RebuildCatalogue();
        var shop = world.AddShop("address:main:1:shop", Burger);
        shop.Holders.Add(new ContractWorld.Holder { id = "oven", itemName = Oven, cargoInstances = new List<ContractWorld.Cargo>() });
        world.Own(shop); // the oven produces nothing on the menu, so it does not participate
        Assert.Contains(world.Coordinator.Serving(shop.Key)!.Definitions, d => d.Name == Oven);
        world.Items[Oven].itemsToProduce![0] = Burger; // in place: it would now produce the advertised burger
        world.Boundary(world.Hour + 1); world.Drain(); world.VisitAll();
        Assert.True(world.Recopies > 0);
        Assert.True(world.Refused.ContainsKey(shop.Key));
        Assert.False(shop.Row.Desired);
        Assert.Equal(FoodEligibilityRefusal.Workstation, world.Coordinator.Image(shop.Key)!.Eligibility.Refusal);
    }

    [Fact]
    public void ADefinitionBrokenWithAStructuralChangeIsStillWatchedForItsRepair()
    {
        var (world, shop) = Owned();
        shop.Menu = new List<string>(shop.Menu);              // stamp: the image is dropped
        world.Items[ContractWorld.ShelfItem].consumes = false; // and the shelf definition breaks in place
        world.VisitAll(); world.Drain(); world.VisitAll();
        var failed = world.Coordinator.Image(shop.Key)!;
        Assert.Equal(FoodEligibilityRefusal.Resources, failed.Eligibility.Refusal);
        Assert.Contains(world.Coordinator.Watched(shop.Key), d => d.Name == ContractWorld.ShelfItem);
        world.Boundary(world.Hour + 1); world.Drain(); world.VisitAll(); // hand-back
        Assert.False(shop.Row.Managed);
        world.Items[ContractWorld.ShelfItem].consumes = true; // repaired in place: no stamp, no hint
        world.Boundary(world.Hour + 1); world.Drain(); world.VisitAll();
        Assert.True(world.Coordinator.Image(shop.Key)!.Passed);
        Assert.False(world.Refused.ContainsKey(shop.Key));
        Assert.True(shop.Row.Desired);
        Assert.Contains(world.Boundary(world.Hour + 1), o => o.Key == shop.Key && o.Outcome == FoodContractBoundary.Outcome.Acquired);
    }

    [Fact]
    public void ACatalogueInvalidationReachesServiceAtOnceWhileRebuildsAreBacklogged()
    {
        var world = new ContractWorld();
        var shops = Enumerable.Range(0, 8).Select(i => world.AddShop("address:main:" + i + ":shop", Burger, HotDog)).ToArray();
        world.Own(shops);
        world.BeginHour(world.Hour + 1); // a preparation backlog
        world.Catalogue.MarkDirty();      // a W11 hint
        Assert.All(shops, s => Assert.Null(world.Select(s))); // no capture on an unverifiable catalogue
        Assert.True(world.CatalogueWaits >= 8);
        Assert.Empty(world.Refused);
        Assert.All(shops, s => Assert.True(s.Row.CanServe)); // ownership waits, it is not withdrawn
        world.RebuildCatalogue(); // new copy objects with the same content
        world.VisitAll();
        Assert.All(shops, s => Assert.Equal(world.Catalogue.Revision, world.Coordinator.VerifiedCatalogue(s.Key)));
        Assert.All(shops, s => Assert.NotNull(world.Select(s)));
        // A rebuild whose content differs for a dependency withdraws through the refusal path.
        world.Items[ContractWorld.ShelfItem].itemsThatCanShowcase![2] = "ba:itemname_other";
        world.RebuildCatalogue(); world.VisitAll();
        Assert.All(shops, s => { Assert.True(world.Refused.ContainsKey(s.Key)); Assert.False(s.Row.Desired); });
    }

    [Fact]
    public void ACatalogueChangeIsRefusedEvenWhenThePassingRebuildPublishesBeforeTheNextVisit()
    {
        var (world, shop) = Owned();
        world.Items[ContractWorld.ShelfItem].itemsThatCanShowcase![2] = "ba:itemname_other"; // a dependency's content
        world.RebuildCatalogue(); world.Drain(); world.VisitAll(); // the rebuild publishes first, then discovery
        Assert.True(world.Coordinator.Image(shop.Key)!.Passed);
        Assert.True(world.Refused.ContainsKey(shop.Key));
        Assert.False(shop.Row.Desired);
        Assert.True(shop.Row.Managed);
        // The same safe sequence as discovery-first: hand-back at the next boundary, recovery, later acquisition.
        Assert.Contains(world.Boundary(world.Hour + 1), o => o.Key == shop.Key && o.Outcome == FoodContractBoundary.Outcome.HandedBack);
        world.Drain(); world.VisitAll();
        Assert.False(world.Refused.ContainsKey(shop.Key));
        Assert.Contains(world.Boundary(world.Hour + 1), o => o.Key == shop.Key && o.Outcome == FoodContractBoundary.Outcome.Acquired);
        // An unchanged rebuild published before the visit is rebased, not refused.
        world.RebuildCatalogue(); world.Drain(); world.VisitAll();
        Assert.False(world.Refused.ContainsKey(shop.Key));
        Assert.True(shop.Row.CanServe);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AReplacedDefinitionObjectIsRefusedWhicheverRunsFirst(bool publicationFirst)
    {
        var (world, shop) = Owned();
        var capture = world.Select(shop)!;
        var old = world.Items[ContractWorld.ShelfItem];
        // A field-identical new object under the same name (W11 re-register): same content, different reference.
        world.Items[ContractWorld.ShelfItem] = new ContractWorld.Item { itemName = old.itemName, showcase = old.showcase,
            itemsThatCanShowcase = (string[])old.itemsThatCanShowcase!.Clone(), itemsToProduce = old.itemsToProduce, tags = new List<string>(old.tags!),
            cargoCapacity = old.cargoCapacity };
        world.RebuildCatalogue();
        if (publicationFirst) { world.Drain(); world.VisitAll(); }
        else Assert.Null(world.Checkout(capture, Burger, "x"));
        Assert.True(world.Refused.ContainsKey(shop.Key));
        Assert.False(shop.Row.Desired);
        Assert.True(shop.Row.Managed);
        Assert.Empty(world.Orders);
        // The same safe sequence in both orders: hand-back, recovery against the new object, later acquisition.
        world.Drain(); world.VisitAll();
        Assert.Contains(world.Boundary(world.Hour + 1), o => o.Key == shop.Key && o.Outcome == FoodContractBoundary.Outcome.HandedBack);
        world.Drain(); world.VisitAll();
        Assert.False(world.Refused.ContainsKey(shop.Key));
        Assert.Same(world.Items[ContractWorld.ShelfItem], world.Coordinator.Serving(shop.Key)!.Definitions.Single(d => d.Name == ContractWorld.ShelfItem).Live);
        Assert.Contains(world.Boundary(world.Hour + 1), o => o.Key == shop.Key && o.Outcome == FoodContractBoundary.Outcome.Acquired);
    }

    [Fact]
    public void ACheckoutDuringACatalogueRebuildWaitsWithoutARefusal()
    {
        var (world, shop) = Owned();
        var capture = world.Select(shop)!;
        world.AfterNativeSetup = () => world.Catalogue.MarkDirty(); // a W11 hint during native setup
        Assert.Null(world.Checkout(capture, Burger, "wait"));
        Assert.Empty(world.Orders);
        Assert.Empty(world.Refused);
        Assert.True(shop.Row.CanServe);
        world.AfterNativeSetup = null;
        world.RebuildCatalogue(); world.VisitAll();
        Assert.Equal("wait", world.Checkout(world.Select(shop)!, Burger, "wait"));
    }

    [Fact]
    public void ARecopyOfAnUnrelatedDefinitionKeepsImagesCurrentAndAcquisitionsReady()
    {
        var world = new ContractWorld();
        var shop = world.AddShop("address:main:1:shop", Burger);
        world.VisitAll(); world.Drain(); world.VisitAll();
        long built = world.Coordinator.Image(shop.Key)!.CatalogueRevision;
        world.Items[ContractWorld.Croissant].cargoCapacity = 9;
        Assert.True(world.Catalogue.Replace(new[] { new LoadedStockDefinition(world.Items[ContractWorld.Croissant].Facts()) }));
        world.VisitAll();
        Assert.True(world.Catalogue.Revision > built);
        Assert.Equal(world.Catalogue.Revision, world.Coordinator.VerifiedCatalogue(shop.Key));
        Assert.True(shop.Row.Desired);
        Assert.Contains(world.Boundary(world.Hour + 1), o => o.Key == shop.Key && o.Outcome == FoodContractBoundary.Outcome.Acquired);
    }

    [Fact]
    public void AReplacedRefusedRegistrationStartsANewLifetimeAndAcquiresSafely()
    {
        var (world, shop) = Owned();
        var capture = world.Select(shop)!;
        shop.Holder(shop.Key + ":shelf:" + Burger).cargoInstances[0].itemName = HotDog;
        Assert.Null(world.Checkout(capture, Burger, "x"));
        Assert.True(world.Coordinator.RefusedHour(shop.Key) >= 0);
        shop.Holder(shop.Key + ":shelf:" + Burger).cargoInstances[0].itemName = Burger; // the new business is laid out correctly
        var replaced = world.ReplaceRegistration(shop);
        Assert.Equal(-1, world.Coordinator.RefusedHour(replaced.Key));
        world.VisitAll(); world.Drain(); world.VisitAll();
        Assert.True(world.Coordinator.Image(replaced.Key)!.Passed);
        Assert.True(replaced.Row.Desired);
        Assert.False(replaced.Row.CanServe); // suspended until a drained boundary
        Assert.Contains(world.Boundary(world.Hour + 1), o => o.Key == replaced.Key && o.Outcome == FoodContractBoundary.Outcome.Acquired);
        Assert.True(replaced.Row.CanServe);
        // Runtime: replacement resets the coordinator entry under the place key.
    }

    [Fact]
    public void ACheckoutReconciliationMismatchWithdrawsTheWholeShop()
    {
        var (world, shop) = Owned();
        var capture = world.Select(shop)!;
        world.OmittedByNative.Add(shop.Key + ":shelf:" + Burger);
        Assert.Null(world.Checkout(capture, Burger, "x"));
        Assert.True(world.Refused.ContainsKey(shop.Key));
        Assert.False(shop.Row.Desired);
    }

    [Fact]
    public void ACaptureOutsideABatchStartsItsOwnDefinitionEpoch()
    {
        // Without a new epoch an in-place edit after the last comparison is answered from the cache.
        var (world, shop) = Owned();
        var image = world.Coordinator.Serving(shop.Key)!;
        var epoch = new FoodDefinitionEpoch(); epoch.Begin();
        Assert.Equal(FoodContractRecheckRefusal.None, FoodContractRecheck.Definitions(image, new ContractWorld.Live(world, shop), epoch));
        world.Items[ContractWorld.ShelfItem].consumes = false;
        Assert.Equal(FoodContractRecheckRefusal.None, FoodContractRecheck.Definitions(image, new ContractWorld.Live(world, shop), epoch));
        epoch.Begin();
        Assert.Equal(FoodContractRecheckRefusal.DefinitionContent, FoodContractRecheck.Definitions(image, new ContractWorld.Live(world, shop), epoch));
    }

    [Theory]
    [InlineData("closed")]
    [InlineData("for-rent")]
    [InlineData("entrance-fee")]
    [InlineData("seasonal")]
    public void ScalarWritesWithdrawTheWholeShopBeforeTheReceipt(string write)
    {
        var (world, shop) = Owned();
        var capture = world.Select(shop)!;
        switch (write)
        {
            case "closed": shop.Closed = true; break;      // W9 TemporarilyClose: stamp flag
            case "for-rent": shop.ForRent = true; break;   // stamp flag
            case "entrance-fee": world.Types[ContractWorld.ShopType].hasEntranceFee = true; break; // business type in place
            case "seasonal": world.Items[Burger].seasonal = true; break; // W11 in place
        }
        Assert.Null(world.Checkout(capture, Burger, "scalar"));
        Assert.Empty(world.Orders);
        Assert.False(shop.Row.Desired);
    }

    [Fact]
    public void QuantityWritesAreLiveAndNeverDirty()
    {
        var (world, shop) = Owned();
        int publishes = world.Publishes;
        var capture = world.Select(shop)!;
        shop.Holder(shop.Key + ":shelf:" + Burger).cargoInstances[0].amount = 3;  // W1/W2/W3 amounts in place
        shop.Holder(shop.Key + ":counter").cargoInstances[0].amount = 1;
        world.VisitAll(); world.Drain();
        Assert.Equal(publishes, world.Publishes);
        Assert.Equal("intent", world.Checkout(capture, Burger, "intent"));
        Assert.True(world.Supply(shop, out var units));
        Assert.Equal(2, units[Burger]);
        Assert.True(shop.Row.CanServe);
    }

    [Fact]
    public void InPlaceItemNameAndPriceAreCaughtAtCheckout()
    {
        var (world, shop) = Owned();
        var first = world.Select(shop)!;
        shop.Holder(shop.Key + ":shelf:" + Burger).cargoInstances[0].itemName = HotDog;
        Assert.Equal(shop.ReaderStamp.ToString(), shop.Stamp().ToString()); // W7 moves no stamp
        Assert.Null(world.Checkout(first, Burger, "w7"));

        (world, shop) = Owned();
        var second = world.Select(shop)!;
        shop.PriceOf(HotDog).price = 9;
        Assert.True(shop.ReaderStamp.Matches(shop.Stamp())); // W10 in place moves no stamp
        Assert.Null(world.Checkout(second, Burger, "w10"));
        Assert.Empty(world.Orders);
    }

    [Fact]
    public void NoPartialImageIsEverVisible()
    {
        var (world, shop) = Owned();
        var before = world.Coordinator.Serving(shop.Key)!;
        var seen = new List<FoodContractImage?>();
        world.DuringPass = s => seen.Add(world.Coordinator.Serving(s.Key));
        world.BeginHour(world.Hour + 1);
        Assert.Equal(FoodContractPhase.Rebuilding, world.Coordinator.Phase(shop.Key));
        Assert.Same(before, world.Coordinator.Serving(shop.Key));
        world.Drain();
        var after = world.Coordinator.Serving(shop.Key)!;
        Assert.NotSame(before, after);
        Assert.All(seen, image => Assert.Same(before, image)); // the old whole image served during the pass
        Assert.Equal(before.Holders.Count, after.Holders.Count);
        Assert.Equal(world.Hour, after.BuiltHour);

        // A write between pass start and publish discards the pass; the stamp then drops the old image too.
        world.DuringPass = s => s.Menu.Add(ContractWorld.Croissant);
        world.BeginHour(world.Hour + 1);
        int discards = world.Discards;
        world.Frame();
        Assert.Equal(discards + 1, world.Discards);
        world.DuringPass = null;
        Assert.False(world.SupportedMenu(shop));
        Assert.Null(world.Coordinator.Serving(shop.Key));
        // A structural change is a recorded refusal (Decision 7), not only a dropped image.
        Assert.Equal(FoodContractPhase.Refused, world.Coordinator.Phase(shop.Key));
    }

    public static IEnumerable<object[]> DefinitionEdits() => new[] { "showcase-element", "produce-element", "counter-tags", "business-tags", "cargo-whitelist" }
        .Select(e => new object[] { e });

    [Theory]
    [MemberData(nameof(DefinitionEdits))]
    public void InPlaceDefinitionElementEditsRefuseBeforeCheckoutAndTheNextImageUsesRecopiedFacts(string edit)
    {
        var world = new ContractWorld();
        // The shelf definition produces exactly the product it shows, so an output edit stays visible to E5a.
        if (edit == "produce-element") world.Items[ContractWorld.ShelfItem].itemsToProduce = new[] { Burger };
        if (edit == "produce-element") world.RebuildCatalogue();
        var shop = world.AddShop("address:main:1:shop", Burger);
        world.Own(shop);
        long revision = world.Catalogue.Revision;
        var capture = world.Select(shop)!;
        var shelf = world.Items[ContractWorld.ShelfItem];
        string name = ContractWorld.ShelfItem;
        switch (edit)
        {
            case "showcase-element": var showcase = shelf.itemsThatCanShowcase!; showcase[2] = "ba:itemname_other"; break; // same array
            case "produce-element": var produce = shelf.itemsToProduce!; produce[0] = HotDog; break;
            case "counter-tags": world.Items[ContractWorld.CounterItem].tags!.Add("ba:itemtag_extra"); name = ContractWorld.CounterItem; break;
            case "business-tags": world.Types[ContractWorld.ShopType].tags.Add("ba:businesstag_extra"); break;
            case "cargo-whitelist": shelf.itemsWhitelist = new[] { Burger }; break; // a field E5a never reads still re-copies
        }
        Assert.True(shop.ReaderStamp.Matches(shop.Stamp())); // no stamp moves
        Assert.Null(world.Checkout(capture, Burger, "edit"));
        Assert.Empty(world.Orders);
        Assert.False(shop.Row.Desired);
        if (edit == "business-tags")
        {
            Assert.Equal(revision, world.Catalogue.Revision);
        }
        else
        {
            Assert.True(world.Catalogue.Revision > revision);
            Assert.True(world.Catalogue.Catalogue!.TryGet(name, out var copy));
            Assert.True(copy.Matches(world.Items[name].Facts())); // re-copied from the live object
            Assert.Equal(world.Catalogue.Catalogue!.Count, world.Catalogue.Names!.Count); // names republished with it
        }
        world.Drain();
        var next = world.Coordinator.Image(shop.Key)!;
        Assert.Equal(world.Catalogue.Revision, next.CatalogueRevision);
        if (edit == "produce-element") Assert.Equal(FoodEligibilityRefusal.Outputs, next.Eligibility.Refusal); // the fresh output fact decides
        else
        {
            Assert.True(next.Passed);
            if (edit == "business-tags") Assert.Contains("ba:businesstag_extra", next.Business!.Tags!);
            else Assert.True(next.Definitions.Single(d => d.Name == name).Copy.Matches(world.Items[name].Facts()));
        }
    }

    [Fact]
    public void UnchangedOwnedShopsKeepServiceAcrossBoundariesWhileTheirRebuildIsUnfinished()
    {
        var world = new ContractWorld();
        var shops = Enumerable.Range(0, 8).Select(i => world.AddShop("address:main:" + i + ":shop", Burger, HotDog)).ToArray();
        world.Own(shops);
        var requested = shops.ToDictionary(s => s.Key, s => s.Row.RequestedHour);
        long from = shops[0].Row.FromHour;
        for (int hour = 1; hour <= 6; hour++)
        {
            // One shop pass per frame and only two frames before the next boundary: most rebuilds are unfinished.
            var outcomes = world.Boundary(world.Hour + 1);
            Assert.Empty(outcomes);
            world.Frame(1); world.VisitAll(); world.Frame(1); world.VisitAll();
            Assert.All(shops, s => { Assert.True(s.Row.CanServe); Assert.Equal(requested[s.Key], s.Row.RequestedHour); });
            Assert.All(shops, s => Assert.NotNull(world.Select(s)));
        }
        Assert.All(shops, s => Assert.True(s.Row.FromHour <= from));
    }

    [Fact]
    public void AFailingRebuildWithdrawsAtOnceAndHandsBackAtTheNextBoundaryAfterDrain()
    {
        var (world, shop) = Owned();
        world.BeginHour(world.Hour + 1);
        shop.Holder(shop.Key + ":shelf:" + Burger).cargoInstances[0].nested!.Add(new ContractWorld.Cargo()); // in place, no stamp
        world.Drain();
        Assert.False(world.Coordinator.Image(shop.Key)!.Passed);
        Assert.False(shop.Row.Desired);
        Assert.True(shop.Row.Managed); // ending-hour work still drains under the old owner
        Assert.Empty(world.Boundary(world.Hour + 1, drained: false));
        Assert.True(shop.Row.Managed);
        Assert.Contains(world.Boundary(world.Hour + 1), o => o.Key == shop.Key && o.Outcome == FoodContractBoundary.Outcome.HandedBack);
        Assert.False(shop.Row.Managed);
    }

    [Fact]
    public void AnUnfinishedAcquisitionIsDeferredWithoutResettingItsRequestAndAcquiresAfterAPassingImage()
    {
        var world = new ContractWorld();
        var shop = world.AddShop("address:main:1:shop", Burger);
        world.VisitAll(); world.Drain(); world.VisitAll();
        long requested = shop.Row.RequestedHour;
        Assert.True(shop.Row.Desired);
        // Native customers block the first boundary; the hour's preparation rebuild then never runs before the next one.
        shop.LiveCustomers = true;
        Assert.Empty(world.Boundary(world.Hour + 1));
        shop.LiveCustomers = false;
        var outcomes = world.Boundary(world.Hour + 1);
        Assert.Contains(outcomes, o => o.Key == shop.Key && o.Outcome == FoodContractBoundary.Outcome.Deferred);
        Assert.Equal(FoodContractReadiness.Stale, world.Coordinator.LastReadiness(shop.Key, out _));
        Assert.True(shop.Row.Deferred);
        Assert.True(shop.Row.Desired);
        Assert.Equal(requested, shop.Row.RequestedHour);
        Assert.Equal(0, world.GateRuns);
        world.Drain(); world.VisitAll();
        Assert.Equal(requested, shop.Row.RequestedHour);
        Assert.Contains(world.Boundary(world.Hour + 1), o => o.Key == shop.Key && o.Outcome == FoodContractBoundary.Outcome.Acquired);
        Assert.False(shop.Row.Deferred);
        Assert.Equal(requested, shop.Row.RequestedHour);
    }

    [Fact]
    public void WarmingServesOwnedShopsOnTheE5b1ContractAndDefersAcquisitions()
    {
        var world = new ContractWorld();
        var owned = world.AddShop("address:main:1:shop", Burger);
        world.Own(owned);
        var waiting = world.AddShop("address:main:2:shop", HotDog);
        world.VisitAll(); world.Drain(); world.VisitAll();
        Assert.True(waiting.Row.Desired);
        var reloaded = new ContractWorld(world.Hour);
        foreach (var s in world.Shops) { var c = reloaded.AddShop(s.Key, s.Menu.ToArray()); c.Row = FoodAuthorityStore.Read(FoodAuthorityStore.Write(new[] { s.Row }))[0]; }
        reloaded.Active = false; reloaded.Warming = true;
        var o = reloaded.Find(owned.Key); var w = reloaded.Find(waiting.Key);
        reloaded.VisitAll();
        Assert.True(o.Row.CanServe); Assert.True(w.Row.Desired); // nothing withdrawn before the first images
        var outcomes = reloaded.Boundary(reloaded.Hour + 1);
        Assert.Contains(outcomes, x => x.Key == w.Key && x.Outcome == FoodContractBoundary.Outcome.Deferred);
        Assert.True(o.Row.CanServe);
        reloaded.Drain(); reloaded.VisitAll();
        Assert.True(reloaded.Active);
        Assert.Contains(reloaded.Boundary(reloaded.Hour + 1), x => x.Key == w.Key && x.Outcome == FoodContractBoundary.Outcome.Acquired);
        Assert.True(o.Row.CanServe);
    }

    [Fact]
    public void RepeatedHourlyPreparationNeverBlocksAShop()
    {
        var world = new ContractWorld();
        var owned = Enumerable.Range(0, 4).Select(i => world.AddShop("address:own:" + i + ":shop", Burger)).ToArray();
        world.Own(owned);
        for (int hour = 0; hour < 24; hour++)
        {
            if (hour == 5) world.AddShop("address:new:1:shop", HotDog);
            world.Boundary(world.Hour + 1); world.Drain(); world.VisitAll();
            Assert.All(owned, s => Assert.True(s.Row.CanServe));
        }
        var added = world.Find("address:new:1:shop");
        Assert.True(added.Row.CanServe);
        Assert.True(added.Row.FromHour <= added.Row.RequestedHour + 1);
        Assert.False(added.Row.Deferred);
    }

    [Fact]
    public void AStampChangeBetweenDrainAndCommitBlocksTheTransfer()
    {
        var world = new ContractWorld();
        var shop = world.AddShop("address:main:1:shop", Burger);
        world.VisitAll(); world.Drain(); world.VisitAll();
        Assert.True(shop.Row.Desired);
        world.AfterDrain = () => shop.Prices.Add(new ContractWorld.Price { itemName = HotDog, price = 2 });
        // The visit after the drain sees the stamp and withdraws; the gate would block it too.
        var outcomes = world.Boundary(world.Hour + 1);
        Assert.DoesNotContain(outcomes, o => o.Outcome == FoodContractBoundary.Outcome.Acquired);
        Assert.False(shop.Row.Managed);
        Assert.False(shop.Row.Desired);

        // Directly at the gate: a write after the last visit, between drain and commit.
        world = new ContractWorld();
        shop = world.AddShop("address:main:1:shop", Burger);
        world.VisitAll(); world.Drain(); world.VisitAll();
        world.BeginHour(world.Hour + 1);
        shop.Prices.Add(new ContractWorld.Price { itemName = HotDog, price = 2 });
        world.BoundaryEpoch.Begin();
        var outcome = FoodContractBoundary.Commit(shop.Row, world.Coordinator, world.Hour, false, true, world.Catalogue.Revision,
            (row, image) => !(image.Stamp is ShopContractStamp s) || !shop.Stamp().Matches(s) ? Withdraw() : true);
        bool Withdraw() { world.Coordinator.Invalidate(shop.Key); shop.Row.Observe(shop.Signature, false, world.Hour); return false; }
        Assert.Equal(FoodContractBoundary.Outcome.Refused, outcome);
        Assert.False(shop.Row.Managed);
        Assert.Equal(FoodContractPhase.Dirty, world.Coordinator.Phase(shop.Key));
    }

    [Fact]
    public void ThePostDrainGateCatchesAnInPlaceHolderOutputEditWithNoCapture()
    {
        var world = new ContractWorld();
        world.Items[ContractWorld.ShelfItem].itemsToProduce = new[] { Burger };
        world.RebuildCatalogue();
        var shop = world.AddShop("address:main:1:shop", Burger);
        world.VisitAll(); world.Drain(); world.VisitAll();
        Assert.True(world.Coordinator.Image(shop.Key)!.Passed);
        world.AfterDrain = () => world.Items[ContractWorld.ShelfItem].itemsToProduce![0] = HotDog; // no capture, no stamp
        // Skip the visit's admission path: the edit moves no stamp, so SupportedMenu still admits; only the gate sees it.
        var outcomes = world.Boundary(world.Hour + 1);
        Assert.Contains(outcomes, o => o.Key == shop.Key && o.Outcome == FoodContractBoundary.Outcome.Refused);
        Assert.False(shop.Row.Managed);
        Assert.True(world.Refused.ContainsKey(shop.Key));
        Assert.True(world.Catalogue.Catalogue!.TryGet(ContractWorld.ShelfItem, out var copy));
        Assert.Equal(HotDog, copy.Outputs![0]); // re-copied
    }

    [Theory]
    [InlineData(0f, false)]
    [InlineData(float.NaN, false)]
    [InlineData(-1f, false)]
    [InlineData(0.003f, false)] // overprecise
    [InlineData(7.5f, true)]
    public void ThePostDrainGateRefusesAnInvalidPriceAndAcceptsAnotherValidPrice(float price, bool acquires)
    {
        var world = new ContractWorld();
        var shop = world.AddShop("address:main:1:shop", Burger, HotDog);
        world.VisitAll(); world.Drain(); world.VisitAll();
        world.AfterDrain = () => shop.PriceOf(HotDog).price = price; // in place, after the prepared image passed
        var outcomes = world.Boundary(world.Hour + 1);
        Assert.Equal(acquires, shop.Row.Managed);
        Assert.Contains(outcomes, o => o.Key == shop.Key && o.Outcome == (acquires ? FoodContractBoundary.Outcome.Acquired : FoodContractBoundary.Outcome.Refused));
        if (acquires) Assert.Equal(Money.FromNative(7.5f), world.Coordinator.Serving(shop.Key)!.Prices[1]); // Q2: recorded, not blocking
    }

    [Fact]
    public void MoreThanSixteenReadyAcquisitionsSpreadOverLaterBoundariesInRequestOrder()
    {
        var world = new ContractWorld();
        var shops = Enumerable.Range(0, 20).Select(i => world.AddShop("address:main:" + i.ToString("00") + ":shop", Burger)).ToArray();
        world.VisitAll(); world.Drain(); world.VisitAll();
        var first = world.Boundary(world.Hour + 1);
        Assert.Equal(16, first.Count(o => o.Outcome == FoodContractBoundary.Outcome.Acquired));
        Assert.Equal(4, first.Count(o => o.Outcome == FoodContractBoundary.Outcome.Deferred));
        Assert.Equal(16, world.GateRuns);
        Assert.Equal(shops.Take(16).Select(s => s.Key), first.Where(o => o.Outcome == FoodContractBoundary.Outcome.Acquired).Select(o => o.Key));
        Assert.All(shops.Skip(16), s => Assert.Equal(FoodContractReadiness.Budget, world.Coordinator.LastReadiness(s.Key, out _)));
        world.Drain(); world.VisitAll();
        var second = world.Boundary(world.Hour + 1);
        Assert.Equal(shops.Skip(16).Select(s => s.Key), second.Where(o => o.Outcome == FoodContractBoundary.Outcome.Acquired).Select(o => o.Key));
        Assert.All(shops, s => Assert.True(s.Row.CanServe));
    }

    [Fact]
    public void AcquisitionNeedsDrainAndNoLiveNativeCustomers()
    {
        var world = new ContractWorld();
        var shop = world.AddShop("address:main:1:shop", Burger);
        world.VisitAll(); world.Drain(); world.VisitAll();
        Assert.Empty(world.Boundary(world.Hour + 1, drained: false));
        Assert.False(shop.Row.Managed);
        world.Drain(); world.VisitAll();
        shop.LiveCustomers = true;
        Assert.Empty(world.Boundary(world.Hour + 1));
        Assert.Equal(0, world.GateRuns); // the gate never runs for a row that cannot transfer
        Assert.False(shop.Row.Deferred);
        world.Drain(); world.VisitAll();
        shop.LiveCustomers = false;
        Assert.Contains(world.Boundary(world.Hour + 1), o => o.Outcome == FoodContractBoundary.Outcome.Acquired);
    }

    [Fact]
    public void NinthHistoryCellAndTwoHundredFiftySeventhNameRefuseBeforeNativeEffects()
    {
        var world = new ContractWorld();
        var shop = world.AddShop("address:main:1:shop", Burger);
        world.VisitAll(); world.Drain(); world.VisitAll(); world.Boundary(world.Hour + 1); world.Drain(); world.VisitAll();
        Assert.True(shop.Row.CanServe);
        var row = new FoodShopDay { Day = (int)(world.Hour / 24), PlaceId = shop.Key, BusinessId = shop.Row.BusinessId };
        for (int i = 0; i < 8; i++) row.ProductSales.Add(new FoodProductSales { ProductId = "fixture:old" + i });
        world.History.Add(row);
        world.BeginHour(world.Hour + 1); world.Drain();
        Assert.Equal(CompleteFoodMenuRefusal.HistoryCapacity, world.Coordinator.Image(shop.Key)!.Admission);
        Assert.False(shop.Row.Desired);
        Assert.Null(world.Select(shop));
        Assert.Empty(world.Orders);

        world = new ContractWorld();
        for (int i = 0; i < FoodNameIdentity.MaximumNames; i++) world.Names.Add("fixture:name" + i.ToString("000"));
        shop = world.AddShop("address:main:1:shop", Burger);
        world.VisitAll(); world.Drain(); world.VisitAll();
        Assert.Equal(CompleteFoodMenuRefusal.NameCapacity, world.Coordinator.Image(shop.Key)!.Admission);
        Assert.False(shop.Row.Desired);
        Assert.Empty(world.Boundary(world.Hour + 1));
        Assert.Null(world.Select(shop));
        world.Names = new FoodReferencedNames();
        for (int i = 0; i < FoodNameIdentity.MaximumNames - 1; i++) world.Names.Add("fixture:name" + i.ToString("000"));
        world.Requeue(shop); world.Drain(); world.VisitAll();
        Assert.True(world.Coordinator.Image(shop.Key)!.Passed); // the 256th name fits
    }

    [Fact]
    public void ReferencedNamesSeedFromEverySavedSourceAndGrowWithScopes()
    {
        var food = new FoodCommerceState();
        food.Shops.Add(new FoodScope { PlaceId = "p", ProductId = Burger, AdditionalProductIds = new List<string> { HotDog } });
        food.History.Add(new FoodShopDay { ProductSales = new List<FoodProductSales> { new FoodProductSales { ProductId = "fixture:sold" } } });
        var names = FoodReferencedNames.From(food, null);
        Assert.Equal(new[] { Burger, HotDog, "fixture:sold" }, names.Names);
        Assert.False(names.Add(Burger));
        Assert.True(names.Add(ContractWorld.Croissant));
        Assert.Equal(4, names.Count);
    }

    // Shared scenario: a few shops acquire, one breaks and repairs, across several boundaries.
    private static Dictionary<string, string> Scenario(int unitsPerFrame, bool reverse, bool reload)
    {
        var world = new ContractWorld();
        var keys = Enumerable.Range(0, 6).Select(i => "address:main:" + i + ":shop").ToArray();
        foreach (var key in reverse ? keys.Reverse() : keys) world.AddShop(key, Burger, HotDog);
        var log = new Dictionary<string, string>();
        for (int step = 0; step < 8; step++)
        {
            if (step == 2) world.Find(keys[3]).Holder(keys[3] + ":shelf:" + Burger).cargoInstances[0].itemName = HotDog;
            if (step == 4) world.Find(keys[3]).Holder(keys[3] + ":shelf:" + Burger).cargoInstances[0].itemName = Burger;
            if (reload && step == 5) world = Reload(world);
            world.VisitAll();
            for (int f = 0; f < 200; f++) { world.Frame(unitsPerFrame); world.VisitAll(); }
            world.Boundary(world.Hour + 1);
        }
        foreach (var key in keys)
        {
            var row = world.Find(key).Row;
            log[key] = row.FromHour + "/" + row.UntilHour + "/" + row.Desired + "/" + row.Managed;
        }
        return log;
    }
    // Reload: the authority rows round-trip through the saved manifest; images, queues and refusals start empty.
    private static ContractWorld Reload(ContractWorld old)
    {
        var rows = FoodAuthorityStore.Read(FoodAuthorityStore.Write(old.Shops.Select(s => s.Row).ToArray()));
        var world = new ContractWorld(old.Hour);
        foreach (var s in old.Shops)
        {
            var copy = world.AddShop(s.Key, s.Menu.ToArray());
            copy.Holders = s.Holders; copy.Prices = s.Prices; copy.Menu = s.Menu;
            copy.Row = rows.Single(r => r.PlaceId == s.Key);
        }
        // Contract refusals are Runtime memory (E5b1 and E5b3 alike): a reload starts without them, warming.
        world.Active = false; world.Warming = true;
        return world;
    }

    [Fact]
    public void OutcomesAreIdenticalAcrossUpdateCadenceControllerOrderAndReload()
    {
        var reference = Scenario(64, false, false);
        Assert.Equal(reference, Scenario(1, false, false));
        Assert.Equal(reference, Scenario(3, true, false));
        Assert.Equal(reference, Scenario(64, false, true));
        Assert.Equal(reference, Scenario(1, true, true));
        Assert.Contains(reference, pair => pair.Value.StartsWith("-1", StringComparison.Ordinal) == false);
    }

    [Fact]
    public void DeferredStateIsNeverSavedAndTheE5b1BoundaryIsUnchanged()
    {
        var a = new FoodAuthority { PlaceId = "address:a:1:shop" };
        a.Observe("business:sig", true, 10);
        Assert.False(a.Boundary(11, true, false, true, contractReady: false));
        Assert.True(a.Deferred);
        var b = new FoodAuthority { PlaceId = "address:a:1:shop" };
        b.Observe("business:sig", true, 10);
        Assert.Equal(FoodAuthorityStore.Write(new[] { b }), FoodAuthorityStore.Write(new[] { a }));
        Assert.False(FoodAuthorityStore.Read(FoodAuthorityStore.Write(new[] { a }))[0].Deferred);
        // The four-argument boundary is the five-argument one with a ready contract, across the whole matrix.
        foreach (bool desired in new[] { false, true })
        foreach (bool managed in new[] { false, true })
        foreach (bool drained in new[] { false, true })
        foreach (bool live in new[] { false, true })
        foreach (bool market in new[] { false, true })
        foreach (long hour in new long[] { 10, 11 })
        {
            FoodAuthority Row() { var r = new FoodAuthority { PlaceId = "address:p" }; r.Observe("business:sig", managed, 5); if (managed) r.Boundary(6, true, false, true); r.Observe("business:sig", desired, 10); return r; }
            var x = Row(); var y = Row();
            Assert.Equal(x.Boundary(hour, drained, live, market), y.Boundary(hour, drained, live, market, true));
            Assert.Equal(FoodAuthorityStore.Write(new[] { x }), FoodAuthorityStore.Write(new[] { y }));
        }
    }

    [Fact]
    public void WithoutAPublishedPolicyADynamicProductIsNeverAcquired()
    {
        // E5b5 replaced the original-three guard with Core LiveMenu (pinned in DynamicAcquisitionTests). With no product
        // policy (no loaded-name image), a dynamic product never passes SupportedBusiness, whatever its image says.
        var world = new ContractWorld();
        world.Add(new ContractWorld.Item { itemName = "foodtrucks:itemname_dumplings", retail = true });
        world.Items[ContractWorld.ShelfItem].itemsThatCanShowcase = new[] { Burger, HotDog, ContractWorld.Croissant, "foodtrucks:itemname_dumplings" };
        world.RebuildCatalogue();
        var shop = world.AddShop("address:main:1:shop", "foodtrucks:itemname_dumplings");
        world.VisitAll(); world.Drain(); world.VisitAll();
        Assert.True(world.Coordinator.Image(shop.Key)!.Passed);
        Assert.False(shop.Row.Desired);
        Assert.Empty(world.Boundary(world.Hour + 1));
    }

    [Fact]
    public void CatalogueReplacementSharesTheTableAndFallsBackWhenOverbound()
    {
        var revision = new LoadedCatalogueRevision();
        var building = revision.Begin();
        for (int i = 0; i < 300; i++) building.Add(new LoadedStockDefinition("fixture:" + i, true, false, false, false, true, true, false, false, true, null, null));
        revision.Publish();
        long before = revision.Revision;
        var changed = new LoadedStockDefinition("fixture:7", true, false, true, false, true, true, false, false, true, new[] { "x" }, null);
        Assert.True(revision.Replace(new[] { changed }));
        Assert.Equal(before + 1, revision.Revision);
        Assert.Equal(before + 1, revision.Names!.Revision);
        Assert.Equal(300, revision.Names.Count);
        Assert.True(revision.Catalogue!.TryGet("fixture:7", out var copy) && copy.Showcase);
        Assert.True(revision.Catalogue.TryGet("fixture:8", out var shared) && !shared.Showcase);
        Assert.False(revision.Replace(new[] { new LoadedStockDefinition("fixture:missing", true, false, false, false, true, true, false, false, true, null, null) }));
        var many = Enumerable.Range(0, LoadedStockCatalogue.MaximumReplacements + 1)
            .Select(i => new LoadedStockDefinition("fixture:" + i, true, false, false, false, true, true, false, false, true, null, null)).ToArray();
        Assert.False(revision.Replace(many)); // the caller rebuilds the whole catalogue instead
        revision.MarkDirty();
        Assert.False(revision.Replace(new[] { changed }));
    }

    [Fact]
    public void DefinitionFactsCompareByContentNotReference()
    {
        var shows = new[] { Burger, HotDog };
        var tags = new List<string> { "a", "b" };
        var live = new LoadedStockFacts("x", true, false, true, false, true, true, false, false, true, shows, null,
            tags.Count, LoadedStockRows.Hash(tags), 4, -1, 0);
        var copy = new LoadedStockDefinition(live);
        Assert.True(copy.Matches(live));
        shows[1] = Burger; // in-place element edit, same array reference
        Assert.False(copy.Matches(live));
        shows[1] = HotDog;
        Assert.True(copy.Matches(live));
        tags[1] = "c";
        Assert.False(copy.Matches(new LoadedStockFacts("x", true, false, true, false, true, true, false, false, true, shows, null,
            tags.Count, LoadedStockRows.Hash(tags), 4, -1, 0)));
        Assert.False(copy.Matches(new LoadedStockFacts("x", true, false, true, false, true, true, false, false, true, shows, null,
            2, LoadedStockRows.Hash(new List<string> { "a", "b" }), 5, -1, 0))); // cargo capacity
        Assert.NotEqual(LoadedStockRows.Hash(new[] { "ab", "c" }), LoadedStockRows.Hash(new[] { "a", "bc" }));
    }

    [Fact]
    public void DistinctDefinitionsAreComparedOncePerEpochAndShared()
    {
        var world = new ContractWorld();
        var shops = Enumerable.Range(0, 16).Select(i => world.AddShop("address:main:" + i + ":shop", Burger, HotDog)).ToArray();
        world.Own(shops);
        world.CaptureEpoch.Begin();
        long compared = world.CaptureEpoch.Compared;
        foreach (var s in shops)
            Assert.Equal(FoodContractRecheckRefusal.None, FoodContractRecheck.Definitions(world.Coordinator.Serving(s.Key)!, new ContractWorld.Live(world, s), world.CaptureEpoch));
        // burger, hot dog, shelf, counter, bag: five distinct definitions for sixteen shops.
        Assert.Equal(5, world.CaptureEpoch.Compared - compared);
    }

    [Fact]
    public void RecheckBoundaryRebuildAndCommitGateCosts()
    {
        var rows = new List<string>();
        RecheckBoundaryRebuildAndCommitGateTimings(rows);
        // Retained image bytes per shop.
        foreach (int holders in new[] { 3, 16, 128 })
        {
            var (image, _) = Synthetic(2, holders);
            long before = GC.GetAllocatedBytesForCurrentThread();
            var built = Rebuild(image);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            GC.KeepAlive(built);
            rows.Add($"image bytes holders={holders}: {bytes} B (contract image, slots, dependencies, business copy; E5a image shared)");
            Assert.True(bytes < 32 * 1024);
        }
        foreach (var row in rows) output.WriteLine(row);
    }
    // The wall-clock part of RecheckBoundaryRebuildAndCommitGateCosts; TimingBudgetTests also runs it beside a CPU load.
    internal static void RecheckBoundaryRebuildAndCommitGateTimings(List<string> rows)
    {
        // Recheck at 1/4/8 offers and 2/16/128 holders (target p95 <= 0.05 ms).
        foreach (int decor in new[] { 0, 32 })
        foreach (int offers in new[] { 1, 4, 8 })
        foreach (int holders in new[] { 2, 16, 128 })
        {
            string line = "";
            TimingBudget.Run(timing =>
            {
                var (image, live) = Synthetic(offers, holders, decor);
                var epoch = new FoodDefinitionEpoch();
                var samples = new double[2000];
                for (int i = 0; i < 200; i++) { epoch.Begin(); FoodContractRecheck.Structure(image, live, epoch); FoodContractRecheck.Prices(image, live); }
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                int failures = 0;
                for (int i = 0; i < samples.Length; i++)
                {
                    var t = timing.Start();
                    epoch.Begin();
                    if (FoodContractRecheck.Structure(image, live, epoch) != FoodContractRecheckRefusal.None
                        | FoodContractRecheck.Prices(image, live) == FoodPriceVerdict.Refused) failures++;
                    samples[i] = timing.Stop(t, "recheck p95");
                }
                long bytes = (GC.GetAllocatedBytesForCurrentThread() - allocated) / samples.Length;
                Assert.Equal(0, failures);
                Array.Sort(samples);
                line = $"recheck offers={offers} holders={holders} decor={decor} dependencies={image.Definitions.Count}: p50 {samples[1000]:0.0000} ms, p95 {samples[1899]:0.0000} ms, max {samples[^1]:0.0000} ms, {bytes} B/call";
                timing.Under("recheck p95", samples[1899], 1.0);
                Assert.Equal(0, bytes);
            });
            rows.Add(line);
        }
        // Boundary rebuild of 1/64/1,024 owned shops under the 0.5 ms frame budget (target unit p95 <= 0.05 ms).
        foreach (int count in new[] { 1, 64, 1024 })
        {
            string line = "";
            TimingBudget.Run(timing =>
            {
                var world = new ContractWorld();
                var shops = Enumerable.Range(0, count).Select(i => world.AddShop("address:perf:" + i + ":shop", Burger, HotDog)).ToArray();
                world.VisitAll(); world.Drain(); world.VisitAll();
                foreach (var s in shops) { s.Row.Boundary(world.Hour + 1, true, false, true); }
                world.BeginHour(world.Hour + 1);
                var units = new List<double>(); var frames = new List<double>(); var frameUnits = new List<int>();
                while (world.PendingRebuilds > 0 || world.Coordinator.PendingPreparations > 0)
                {
                    // The Runtime frame loop: stop before a unit whose estimated cost (the previous unit's) would overrun 0.5 ms.
                    long frame = Stopwatch.GetTimestamp(); int n = 0; double last = 0;
                    while (n == 0 || (Stopwatch.GetTimestamp() - frame) * 1000.0 / Stopwatch.Frequency + last < 0.5)
                    {
                        var t = timing.Start();
                        bool stepped = world.Step();
                        var sample = timing.End(t);
                        last = sample.Ms;
                        if (!stepped) break;
                        n++; units.Add(timing.Keep("rebuild unit max", sample));
                    }
                    frames.Add((Stopwatch.GetTimestamp() - frame) * 1000.0 / Stopwatch.Frequency); frameUnits.Add(n);
                }
                units.Sort(); frames.Sort();
                line = $"rebuild shops={count}: units {units.Count}, unit p95 {units[(int)(units.Count * 0.95)]:0.0000} ms, max {units[^1]:0.0000} ms, frames {frames.Count}, frame p95 {frames[(int)(frames.Count * 0.95)]:0.0000} ms, frame max {frames[^1]:0.0000} ms, max units/frame {frameUnits.Max()}, units >= 30 ms {units.Count(u => u >= 30)}";
                Assert.All(shops, s => Assert.Equal(world.Hour, world.Coordinator.Image(s.Key)!.BuiltHour));
                // No unit at or above the 30 ms spike line.
                timing.MaxUnder("rebuild unit max", units[^1], 30);
            });
            rows.Add(line);
        }
        // Commit-gate boundary-frame cost at 1/16 acquisitions, including the distinct-definition comparison and price rows.
        foreach (int count in new[] { 1, 16 })
        {
            string line = "";
            TimingBudget.Run(timing =>
            {
                var samples = new List<double>();
                for (int run = 0; run < 50; run++)
                {
                    var world = new ContractWorld();
                    var shops = Enumerable.Range(0, count).Select(i => world.AddShop("address:gate:" + i + ":shop", Burger, HotDog)).ToArray();
                    world.VisitAll(); world.Drain(); world.VisitAll(); world.BeginHour(world.Hour + 1);
                    var t = timing.Start();
                    world.BoundaryEpoch.Begin();
                    foreach (var s in shops)
                        Assert.Equal(FoodContractBoundary.Outcome.Acquired, FoodContractBoundary.Commit(s.Row, world.Coordinator, world.Hour, false, true, world.Catalogue.Revision,
                            (row, image) => FoodContractRecheck.Structure(image, new ContractWorld.Live(world, s), world.BoundaryEpoch) == FoodContractRecheckRefusal.None
                                && FoodContractRecheck.Prices(image, new ContractWorld.Live(world, s)) != FoodPriceVerdict.Refused));
                    var sample = timing.End(t);
                    if (run >= 10) samples.Add(timing.Keep("commit gate max", sample));
                }
                samples.Sort();
                line = $"commit gate acquisitions={count}: p95 {samples[(int)(samples.Count * 0.95)]:0.0000} ms, max {samples[^1]:0.0000} ms";
                timing.MaxUnder("commit gate max", samples[^1], 30);
            });
            rows.Add(line);
        }
    }
    private static FoodContractImage Rebuild(FoodContractImage source)
    {
        var slots = new FoodHolderSlot[source.Holders.Count];
        for (int i = 0; i < slots.Length; i++) slots[i] = source.Holders[i];
        var deps = new FoodDefinitionDependency[source.Definitions.Count];
        for (int i = 0; i < deps.Length; i++) deps[i] = new FoodDefinitionDependency(source.Definitions[i].Name, source.Definitions[i].Live, source.Definitions[i].Copy);
        var business = new FoodBusinessTypeCopy(new FoodBusinessTypeFacts(source.Business!.Live, source.Business.Simulator, false, source.Business.Tags));
        return new FoodContractImage(source.Key, source.BuiltHour, source.Eligibility, slots, deps, business, source.Admission, (object)default(ShopContractStamp));
    }
    // A passing image with N offers and N holders (counter included), built through the world model.
    // decor: extra non-participating furniture, each with its own definition (a dependency since review round 1).
    private static (FoodContractImage Image, IFoodContractLive Live) Synthetic(int offers, int holders, int decor = 0)
    {
        var world = new ContractWorld { MenuLimit = LoadedStockLimits.Offers };
        var products = Enumerable.Range(0, offers).Select(i => i < 3 ? new[] { Burger, HotDog, ContractWorld.Croissant }[i] : "fixture:p" + i).ToArray();
        foreach (var p in products.Skip(3)) world.Add(new ContractWorld.Item { itemName = p, retail = true });
        world.Items[ContractWorld.ShelfItem].itemsThatCanShowcase = products.Concat(new[] { Burger, HotDog, ContractWorld.Croissant }).Distinct().ToArray();
        world.RebuildCatalogue();
        var shop = world.AddShop("address:synthetic:1:shop", products);
        for (int i = shop.Holders.Count; i < holders; i++)
            shop.Holders.Add(new ContractWorld.Holder { id = "extra" + i, itemName = ContractWorld.ShelfItem,
                cargoInstances = new List<ContractWorld.Cargo> { new ContractWorld.Cargo { itemName = products[i % offers], amount = 2 } } });
        for (int i = 0; i < decor; i++)
        {
            world.Add(new ContractWorld.Item { itemName = "fixture:decor" + i.ToString("00") });
            shop.Holders.Add(new ContractWorld.Holder { id = "decor" + i, itemName = "fixture:decor" + i.ToString("00") });
        }
        if (decor > 0) world.RebuildCatalogue();
        world.Active = false; // the E5a menu bound (8) is the builder's; SupportedBusiness's 4 is not involved here
        world.VisitAll(); world.Drain();
        var image = world.Coordinator.Image(shop.Key)!;
        Assert.True(image.Eligibility.StockContractMatched, image.Eligibility.Refusal.ToString());
        return (image, new ContractWorld.Live(world, shop));
    }
}
