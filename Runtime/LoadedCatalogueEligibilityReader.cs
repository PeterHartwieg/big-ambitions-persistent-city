using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using BigAmbitions.Items;
using BigAmbitions.Tags;
using Buildings.Retail.Simulation;
using Helpers;
using PersistentCity.Core;
using Seasons;

namespace PersistentCity.Runtime
{
    /// <summary>Main-thread E5a reader, live in shadow since E5b2: it publishes images and the loaded-name image
    /// but grants no authority. Dirty sources are Decision 1 hints (<see cref="IFoodContractHintSink"/>) and the
    /// Decision 2 stamp compared on each discovery <see cref="Visit"/>; neither is relied on for correctness.
    /// Continue runs one bounded unit. Clean frames touch no definition or holder. Reset before city unload.
    /// Arbitrary in-place third-party writes are not a proven notification surface.</summary>
    public sealed class LoadedCatalogueEligibilityReader : IDisposable, IFoodContractHintSink
    {
        static LoadedCatalogueEligibilityReader() { } // Bind only on explicit main-thread use.
        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceComparer Instance = new ReferenceComparer();
            public new bool Equals(object? a, object? b) => ReferenceEquals(a, b);
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }
        private static readonly FieldInfo? AuthoredTags = BindTags();
        private static FieldInfo? BindTags()
        {
            var f = typeof(TaggedScriptableObject).GetField("tags", BindingFlags.Instance | BindingFlags.NonPublic);
            return f != null && !f.IsStatic && f.FieldType == typeof(List<string>) ? f : null;
        }
        // E5b3: the same field through a DynamicMethod, so rechecks read tags without reflection per call.
        private static readonly Func<TaggedScriptableObject, List<string>?>? ReadTags = BindTagReader();
        private static Func<TaggedScriptableObject, List<string>?>? BindTagReader()
        {
            if (AuthoredTags == null) return null;
            try
            {
                var method = new System.Reflection.Emit.DynamicMethod("PersistentCityReadTags", typeof(List<string>),
                    new[] { typeof(TaggedScriptableObject) }, typeof(LoadedCatalogueEligibilityReader).Module, true);
                var il = method.GetILGenerator();
                il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0); il.Emit(System.Reflection.Emit.OpCodes.Ldfld, AuthoredTags);
                il.Emit(System.Reflection.Emit.OpCodes.Ret);
                return (Func<TaggedScriptableObject, List<string>?>)method.CreateDelegate(typeof(Func<TaggedScriptableObject, List<string>?>));
            }
            catch (Exception) { return null; }
        }
        internal static List<string>? RawTags(TaggedScriptableObject item)
            => ReadTags != null ? ReadTags(item) : AuthoredTags?.GetValue(item) as List<string>;
        private static List<string>? Tags(TaggedScriptableObject item) => ValidTags(RawTags(item));
        private static List<string>? ValidTags(List<string>? tags)
        {
            if (tags == null || tags.Count > LoadedStockLimits.Rows) return null;
            for (int i = 0; i < tags.Count; i++) if (string.IsNullOrWhiteSpace(tags[i])) return null;
            return tags;
        }
        /// <summary>E5b3 Decision 3a: the live fields the contract reads, uncopied and without allocation. The
        /// catalogue copy and every recheck use this one function, so the two cannot disagree on a field.</summary>
        internal static LoadedStockFacts Facts(Item item)
        {
            var raw = RawTags(item);
            var tags = ValidTags(raw);
            var whitelist = item.itemsThatCanShowcase;
            var outputs = item.producerSettings?.itemsToProduce;
            var cargoWhitelist = item.itemsWhitelist;
            bool rows = (whitelist == null || whitelist.Length <= LoadedStockLimits.Rows)
                && (outputs == null || outputs.Length <= LoadedStockLimits.Rows);
            return new LoadedStockFacts(item.itemName, (item.type & ItemType.RetailProduct) != 0,
                (item.type & ItemType.ServiceProduct) != 0, (item.type & ItemType.ShowcaseShelf) != 0,
                (item.type & ItemType.EmployeeWorkstation) != 0, tags != null && rows,
                item.season == SeasonName.None && !item.isSpecialGift && !item.isSeasonalForSale
                    && !item.isSeasonalItemFromAIBusiness && (item.itemsBySeason == null || item.itemsBySeason.Length == 0)
                    && string.IsNullOrEmpty(item.itemToReplaceWithWhenOvertakingBusiness),
                tags?.Contains("ba:itemtag_isbag") == true, tags?.Contains("ba:itemtag_isfullservicecashregister") == true,
                item.producerSettings != null && item.producerSettings.ResourcesRequired,
                rows ? whitelist : null, rows ? outputs : null,
                raw?.Count ?? -1, LoadedStockRows.Hash(raw), item.cargoCapacity,
                cargoWhitelist?.Length ?? -1, LoadedStockRows.Hash(cargoWhitelist));
        }
        private static LoadedStockDefinition Definition(Item item) => new LoadedStockDefinition(Facts(item));

        private sealed class Shop : IContractDirtyUnit
        {
            internal Shop(string key, BuildingRegistration registration, int limit) { Key = key; Registration = registration; Limit = limit; }
            internal readonly string Key;
            internal BuildingRegistration Registration;
            internal ShopAddress Address;
            internal int Limit;
            internal long Revision, PublishedCatalogue;
            internal FoodEligibilityImage? Image;
            // Taken when the pass starts, so a write during a multi-frame pass mismatches at the next visit.
            internal ShopContractStamp Start, Stamp;
            public bool Queued { get; set; }
        }
        private readonly struct ShopAddress : IEquatable<ShopAddress>
        {
            internal ShopAddress(string street, int number) { Street = street; Number = number; }
            internal readonly string Street;
            internal readonly int Number;
            public bool Equals(ShopAddress other) => Number == other.Number && string.Equals(Street, other.Street, StringComparison.Ordinal);
            public override bool Equals(object? obj) => obj is ShopAddress other && Equals(other);
            public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Street ?? "") * 31 + Number;
        }
        private sealed class AddressComparer : IEqualityComparer<ShopAddress>
        {
            internal static readonly AddressComparer Instance = new AddressComparer();
            public bool Equals(ShopAddress a, ShopAddress b) => a.Equals(b);
            public int GetHashCode(ShopAddress a) => a.GetHashCode();
        }
        private readonly Dictionary<string, Shop> shops = new Dictionary<string, Shop>(StringComparer.Ordinal);
        // Hint lookup only; both hold exactly the shops above and are cleared with them.
        private readonly Dictionary<object, Shop> byRegistration = new Dictionary<object, Shop>(ReferenceComparer.Instance);
        private readonly Dictionary<ShopAddress, Shop> byAddress = new Dictionary<ShopAddress, Shop>(AddressComparer.Instance);
        private readonly ContractDirtyQueue<Shop> dirty = new ContractDirtyQueue<Shop>();
        private IEnumerator<Item>? catalogueCursor;
        private readonly LoadedCatalogueRevision revision = new LoadedCatalogueRevision();
        private LoadedStockCatalogue? catalogue => revision.Catalogue;
        private bool catalogueDirty => revision.Dirty;
        private long catalogueRevision => revision.Revision;
        private Shop? scanning;
        private long scanningRevision;
        private LoadedStockMenuBuilder? menu;
        private Dictionary<string, ItemInstance>.Enumerator furnitureCursor;
        private bool furnitureOpen;
        private readonly StockCargoFields[] cargoBuffer = new StockCargoFields[LoadedStockLimits.Rows];
        // Transient only; catches shared cargo references, including sharing with storage.
        private readonly HashSet<object> cargoIdentities = new HashSet<object>(ReferenceComparer.Instance);
        // Decision 2 readers, bound by Prepare. Null until then or when a capability failed: Visit stays idle.
        private static CollectionVersionReader<List<string>>? menuVersion;
        private static CollectionVersionReader<List<RetailPrice>>? priceVersion;
        private static CollectionVersionReader<Dictionary<string, ItemInstance>>? furnitureVersion;
        private static bool stampsReady;

        /// <summary>Binds and self-tests the three Decision 2 version readers. Main thread, city start.</summary>
        public static bool Prepare(FoodContractCapabilities report)
        {
            stampsReady = false;
            menuVersion = CollectionVersionReader<List<string>>.Bind(out var menuFailure);
            priceVersion = CollectionVersionReader<List<RetailPrice>>.Bind(out var priceFailure);
            furnitureVersion = CollectionVersionReader<Dictionary<string, ItemInstance>>.Bind(out var furnitureFailure);
            bool bound = report.Record("version reader cachedAvailableProducts List<String>._version int", menuVersion == null ? menuFailure : null)
                & report.Record("version reader retailPrices List<RetailPrice>._version int", priceVersion == null ? priceFailure : null)
                & report.Record("version reader itemInstances Dictionary<String,ItemInstance>._version int", furnitureVersion == null ? furnitureFailure : null);
            bool tested = report.Record("self-test List<String> add, set, remove", FoodContractSelfTest.List(menuVersion, "a", "b"))
                & report.Record("self-test List<RetailPrice> add, set, remove", FoodContractSelfTest.List<RetailPrice>(priceVersion, null!, null!))
                & report.Record("self-test Dictionary<String,ItemInstance> add, set, remove", FoodContractSelfTest.Dictionary<ItemInstance>(furnitureVersion, null!, null!));
            stampsReady = bound && tested;
            return stampsReady;
        }
        public static bool StampsReady => stampsReady;
        /// <summary>E5b3: the registration's current Decision 2 stamp; default (never matching) until the readers bind.</summary>
        internal static ShopContractStamp CurrentStamp(BuildingRegistration r) => stampsReady && r != null ? Stamp(r) : default;
        private static ShopContractStamp Stamp(BuildingRegistration r)
        {
            var menu = r.cachedAvailableProducts; var prices = r.retailPrices; var furniture = r.itemInstances;
            return new ShopContractStamp(CollectionStamp.Of(menuVersion!, menu, menu?.Count ?? 0),
                CollectionStamp.Of(priceVersion!, prices, prices?.Count ?? 0),
                CollectionStamp.Of(furnitureVersion!, furniture, furniture?.Count ?? 0),
                r.businessTypeName, r.RentedByPlayer, r.HasEstablishedBusiness, r.AvailableForRent, r.temporarilyClosed);
        }

        public long CatalogueRevision => catalogueRevision;
        /// <summary>E5b3: the sealed catalogue of the current revision, or null while it is dirty or rebuilding.</summary>
        internal LoadedStockCatalogue? Catalogue => catalogueDirty ? null : catalogue;
        /// <summary>E5b3 coordinator: called once per finished shop pass with the pass's registration, image and start stamp.</summary>
        internal Action<string, BuildingRegistration, FoodEligibilityImage, ShopContractStamp>? ShopPublished;
        public long DefinitionRecopies { get; private set; }
        public long FullRecopies { get; private set; }
        /// <summary>E5b3 Decision 3a: re-copies the named live definitions into a new catalogue revision and
        /// requeues every shop, so every image built afterwards uses the fresh facts. Falls back to a full
        /// catalogue rebuild when the overlay cannot take them.</summary>
        internal void RecopyDefinitions(IReadOnlyList<Item> items)
        {
            if (items.Count == 0) return;
            var copies = new LoadedStockDefinition[items.Count];
            bool readable = true;
            for (int i = 0; i < copies.Length; i++)
            {
                var item = items[i];
                if (item == null || !LoadedStockLimits.CanonicalName(item.itemName)) { readable = false; break; }
                try { copies[i] = Definition(item); } catch (Exception) { readable = false; break; }
            }
            if (readable && revision.Replace(copies))
            {
                DefinitionRecopies += copies.Length; CancelShop();
                foreach (var shop in shops.Values) Dirty(shop);
                return;
            }
            FullRecopies++; MarkCatalogueDirty();
        }
        /// <summary>E5b3: requeues every tracked shop (a business-type re-copy), keeping the catalogue.</summary>
        internal void RequeueAllShops() { foreach (var shop in shops.Values) Dirty(shop); }
        /// <summary>The loaded-name image of the current catalogue revision, or null while it is dirty or rebuilding.</summary>
        public LoadedNameImage? LoadedNames => revision.Names;
        public LoadedNameImage? LastLoadedNames => revision.LastPublished;
        public bool CatalogueBuilding => revision.Building != null;
        public int TrackedShops => shops.Count;
        public long StampReads { get; private set; }
        public long StampMismatches { get; private set; }
        public long HintUnits => dirty.Marks - dirty.Coalesced;
        public long HintsCoalesced => dirty.Coalesced;
        public int CatalogueCount => catalogue?.Count ?? 0;
        public FoodEligibilityRefusal CatalogueRefusal => catalogueDirty ? FoodEligibilityRefusal.Dirty
            : catalogue?.Refusal ?? FoodEligibilityRefusal.CatalogueUnavailable;
        public int PendingShops => dirty.Count + (scanning == null ? 0 : 1);
        public long CatalogueBumps => revision.Bumps;
        public long DefinitionsRead { get; private set; }
        public long HoldersRead { get; private set; }
        public long CataloguePasses { get; private set; }
        public long ShopPasses { get; private set; }

        public bool MarkCatalogueDirty()
        {
            // Repeated notifications coalesce until work starts. In-progress work is discarded.
            if (!revision.MarkDirty()) return false;
            CloseCatalogue(); CancelShop();
            foreach (var shop in shops.Values) Enqueue(shop);
            return true;
        }
        public bool MarkShopDirty(string key, BuildingRegistration registration, int maximumOffers = LoadedStockLimits.Offers)
        {
            if (string.IsNullOrEmpty(key) || registration == null) return false;
            if (!shops.TryGetValue(key, out var shop))
            {
                if (shops.Count == LoadedStockLimits.Shops) return false;
                shop = new Shop(key, registration, maximumOffers); shops.Add(key, shop);
                Index(shop, registration);
            }
            else if (!ReferenceEquals(shop.Registration, registration)) { Unindex(shop); Index(shop, registration); }
            shop.Registration = registration; shop.Limit = maximumOffers;
            Dirty(shop); return true;
        }
        private bool Dirty(Shop shop)
        {
            shop.Revision++; shop.Image = null; shop.Stamp = default;
            if (ReferenceEquals(shop, scanning)) { CancelShop(); return true; }
            return Enqueue(shop);
        }
        private bool Enqueue(Shop shop) => dirty.Enqueue(shop);
        private void Index(Shop shop, BuildingRegistration registration)
        {
            byRegistration[registration] = shop;
            if (string.IsNullOrEmpty(registration.StreetName)) return;
            shop.Address = new ShopAddress(registration.StreetName, registration.StreetNumber);
            byAddress[shop.Address] = shop;
        }
        private void Unindex(Shop shop)
        {
            byRegistration.Remove(shop.Registration);
            if (shop.Address.Street != null && byAddress.TryGetValue(shop.Address, out var indexed) && ReferenceEquals(indexed, shop))
                byAddress.Remove(shop.Address);
            shop.Address = default;
        }

        /// <summary>Decision 2, once per existing discovery visit: constant time, no allocation for a tracked shop.
        /// The first visit, a replaced registration or a stamp mismatch queues one coalesced rebuild. Shadow only:
        /// no ownership or admission reads this.</summary>
        public void Visit(string key, BuildingRegistration registration, int maximumOffers)
        {
            if (!stampsReady || registration == null) return;
            if (!shops.TryGetValue(key, out var shop) || !ReferenceEquals(shop.Registration, registration))
            { MarkShopDirty(key, registration, maximumOffers); return; }
            if (shop.Queued || ReferenceEquals(shop, scanning) || shop.Image == null) return;
            StampReads++;
            var now = Stamp(registration);
            if (!now.Matches(shop.Stamp)) { StampMismatches++; shop.Limit = maximumOffers; Dirty(shop); }
        }
        /// <summary>Current versus published stamp of one tracked shop (diagnostic).</summary>
        public bool TryStamp(string key, out ShopContractStamp published, out ShopContractStamp current)
        {
            published = default; current = default;
            if (!stampsReady || !shops.TryGetValue(key, out var shop)) return false;
            published = shop.Stamp; current = Stamp(shop.Registration); return true;
        }
        public IEnumerable<string> ShopKeys => shops.Keys;

        FoodContractHint IFoodContractHintSink.Catalogue() => MarkCatalogueDirty() ? FoodContractHint.NewUnit : FoodContractHint.Coalesced;
        FoodContractHint IFoodContractHintSink.Registration(object registration)
            => byRegistration.TryGetValue(registration, out var shop) ? Hint(shop) : FoodContractHint.Unknown;
        FoodContractHint IFoodContractHintSink.Holder(string street, int number)
            => byAddress.TryGetValue(new ShopAddress(street, number), out var shop) ? Hint(shop) : FoodContractHint.Unknown;
        FoodContractHint IFoodContractHintSink.AllShops()
        {
            bool queued = false;
            foreach (var shop in shops.Values) queued |= Dirty(shop);
            return queued ? FoodContractHint.NewUnit : FoodContractHint.Coalesced;
        }
        private FoodContractHint Hint(Shop shop) => Dirty(shop) ? FoodContractHint.NewUnit : FoodContractHint.Coalesced;
        public bool TryGet(string key, out FoodEligibilityImage image)
        {
            image = null!;
            if (catalogueDirty || !shops.TryGetValue(key, out var shop) || shop.Image == null
                || shop.PublishedCatalogue != catalogueRevision) return false;
            image = shop.Image; return true;
        }
        public bool Continue()
        {
            if (catalogueDirty) { ContinueCatalogue(); return true; }
            if (scanning == null)
            {
                if (!dirty.TryDequeue(out var next)) return false;
                scanning = next; scanningRevision = scanning.Revision;
                StartShop(scanning);
            }
            ContinueShop(); return true;
        }
        private void ContinueCatalogue()
        {
            try
            {
                if (revision.Building == null)
                {
                    revision.Begin(); CataloguePasses++;
                    var all = ItemsGetter.AllItems;
                    if (all == null) { PublishCatalogue(FoodEligibilityRefusal.CatalogueUnavailable); return; }
                    catalogueCursor = all.GetEnumerator();
                }
                for (int i = 0; i < LoadedStockLimits.CataloguePerUnit; i++)
                {
                    if (!catalogueCursor!.MoveNext()) { PublishCatalogue(); return; }
                    // The sole extra enumeration step detects overflow without reading Current.
                    var buildingCatalogue = revision.Building!;
                    if (buildingCatalogue.Catalogue.Count == LoadedStockLimits.Catalogue)
                    { PublishCatalogue(FoodEligibilityRefusal.CatalogueBounds); return; }
                    var item = catalogueCursor.Current; DefinitionsRead++;
                    if (item == null || !LoadedStockLimits.CanonicalName(item.itemName))
                    { PublishCatalogue(FoodEligibilityRefusal.CatalogueIdentity); return; }
                    if (!ReferenceEquals(ItemsGetter.GetByName(item.itemName, true), item))
                    { PublishCatalogue(FoodEligibilityRefusal.CatalogueChanged); return; }
                    if (!buildingCatalogue.Add(Definition(item))) { PublishCatalogue(); return; }
                }
            }
            catch (Exception) { PublishCatalogue(FoodEligibilityRefusal.NativeRead); }
        }
        private void PublishCatalogue(FoodEligibilityRefusal refusal = FoodEligibilityRefusal.None)
        {
            // Catalogue and loaded-name image are published in one step from the same accepted definitions.
            revision.Publish(refusal);
            catalogueCursor?.Dispose(); catalogueCursor = null;
        }
        private void CloseCatalogue()
        { catalogueCursor?.Dispose(); catalogueCursor = null; }
        private void StartShop(Shop shop)
        {
            ShopPasses++;
            var r = shop.Registration;
            shop.Start = stampsReady ? Stamp(r) : default;
            try
            {
                var type = BusinessTypeHelper.GetData(r.businessTypeName);
                var tags = type == null ? null : Tags(type);
                bool business = r.RentedByPlayer && r.HasEstablishedBusiness && !r.AvailableForRent && !r.temporarilyClosed
                    && type?.simulator != null && type.simulator.GetType() == typeof(FullServiceBusinessSimulator)
                    && !type.hasEntranceFee && tags?.Contains("ba:businesstag_customersneedpaperbags") == true;
                StockPriceFields[]? prices = null;
                if (r.retailPrices != null && r.retailPrices.Count <= LoadedStockLimits.Rows)
                {
                    prices = new StockPriceFields[r.retailPrices.Count];
                    for (int i = 0; i < prices.Length; i++)
                    { var row = r.retailPrices[i]; if (row != null) prices[i] = new StockPriceFields(row.itemName, row.price); }
                }
                var instances = r.itemInstances;
                bool uniqueKeys = instances != null && (instances.Comparer == EqualityComparer<string>.Default || instances.Comparer == StringComparer.Ordinal);
                menu = new LoadedStockMenuBuilder(catalogue!, scanningRevision, business, r.cachedAvailableProducts, prices, shop.Limit,
                    instances?.Count ?? 0, uniqueKeys);
                if (instances == null || instances.Count > LoadedStockLimits.Furniture)
                    menu.Fail(FoodEligibilityRefusal.FurnitureBounds);
                else if (!uniqueKeys)
                    menu.Fail(FoodEligibilityRefusal.FurnitureIdentity);
                else if (menu.Refusal == FoodEligibilityRefusal.None)
                { furnitureCursor = instances.GetEnumerator(); furnitureOpen = true; }
            }
            catch (Exception)
            {
                menu = LoadedStockMenuBuilder.Refused(catalogue!, scanningRevision, FoodEligibilityRefusal.NativeRead);
            }
        }
        private void ContinueShop()
        {
            try
            {
                if (menu!.Refusal != FoodEligibilityRefusal.None) { PublishShop(); return; }
                for (int i = 0; i < LoadedStockLimits.FurniturePerUnit; i++)
                {
                    if (!furnitureCursor.MoveNext()) { PublishShop(); return; }
                    HoldersRead++;
                    var pair = furnitureCursor.Current; var instance = pair.Value;
                    if (instance == null) { menu.Fail(FoodEligibilityRefusal.FurnitureIdentity); PublishShop(); return; }
                    var cargo = instance.cargoInstances;
                    if (cargo != null && cargo.Count > LoadedStockLimits.Rows)
                    { menu.Fail(FoodEligibilityRefusal.CargoBounds); PublishShop(); return; }
                    int count = cargo?.Count ?? 0;
                    for (int j = 0; j < count; j++)
                    {
                        var c = cargo![j];
                        if (c == null) { menu.Fail(FoodEligibilityRefusal.CargoShape); break; }
                        if (!cargoIdentities.Add(c)) { menu.Fail(FoodEligibilityRefusal.SharedCargo); break; }
                        cargoBuffer[j] = new StockCargoFields(c.itemName, c.amount, c.nestedCargoInstances?.Count ?? -1);
                    }
                    menu.AddHolder(pair.Key, instance.id, instance.itemName, cargo != null,
                        new ReadOnlySpan<StockCargoFields>(cargoBuffer, 0, count));
                    // Drop cargo strings after every unit; never retain native cargo objects in an image.
                    Array.Clear(cargoBuffer, 0, count);
                    if (menu.Refusal != FoodEligibilityRefusal.None) { PublishShop(); return; }
                }
            }
            catch (Exception) { menu!.Fail(FoodEligibilityRefusal.NativeRead); PublishShop(); }
        }
        private void PublishShop()
        {
            var result = menu!.Finish();
            scanning!.Image = result; scanning.PublishedCatalogue = catalogueRevision; scanning.Stamp = scanning.Start;
            var shop = scanning; var stamp = scanning.Start;
            CancelShop(false);
            ShopPublished?.Invoke(shop.Key, shop.Registration, result, stamp);
        }
        private void CancelShop(bool requeue = true)
        {
            if (furnitureOpen) { furnitureCursor.Dispose(); furnitureCursor = default; furnitureOpen = false; }
            if (scanning != null && requeue) Enqueue(scanning);
            if (scanning != null) scanning.Start = default;
            scanning = null; menu = null; cargoIdentities.Clear(); Array.Clear(cargoBuffer, 0, cargoBuffer.Length);
        }
        /// <summary>Drops every native reference: registrations, indexes, cursors and images.</summary>
        public void Reset()
        {
            CloseCatalogue(); CancelShop(false); dirty.Clear(); shops.Clear(); byRegistration.Clear(); byAddress.Clear();
            revision.Reset();
        }
        public int RetainedNativeReferences => shops.Count + byRegistration.Count + byAddress.Count + dirty.Count
            + (scanning == null ? 0 : 1) + (catalogueCursor == null ? 0 : 1) + (furnitureOpen ? 1 : 0);
        public void Dispose() => Reset();
    }
}
