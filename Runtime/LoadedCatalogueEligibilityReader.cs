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
    /// <summary>Explicit main-thread E5a reader. No hooks, timers or gameplay caller.
    /// Future notification owners call dirty methods after native mutation, then Continue at a
    /// simulation boundary. Clean cache access never touches native data. Reset before city unload.
    /// Arbitrary in-place third-party writes are not a proven notification surface.</summary>
    public sealed class LoadedCatalogueEligibilityReader : IDisposable
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
        private static List<string>? Tags(TaggedScriptableObject item)
        {
            var tags = AuthoredTags?.GetValue(item) as List<string>;
            if (tags == null || tags.Count > LoadedStockLimits.Rows) return null;
            for (int i = 0; i < tags.Count; i++) if (string.IsNullOrWhiteSpace(tags[i])) return null;
            return tags;
        }
        private static LoadedStockDefinition Definition(Item item)
        {
            var tags = Tags(item);
            var whitelist = item.itemsThatCanShowcase;
            var outputs = item.producerSettings?.itemsToProduce;
            bool rows = (whitelist == null || whitelist.Length <= LoadedStockLimits.Rows)
                && (outputs == null || outputs.Length <= LoadedStockLimits.Rows);
            return new LoadedStockDefinition(item.itemName, (item.type & ItemType.RetailProduct) != 0,
                (item.type & ItemType.ServiceProduct) != 0, (item.type & ItemType.ShowcaseShelf) != 0,
                (item.type & ItemType.EmployeeWorkstation) != 0, tags != null && rows,
                item.season == SeasonName.None && !item.isSpecialGift && !item.isSeasonalForSale
                    && !item.isSeasonalItemFromAIBusiness && (item.itemsBySeason == null || item.itemsBySeason.Length == 0)
                    && string.IsNullOrEmpty(item.itemToReplaceWithWhenOvertakingBusiness),
                tags?.Contains("ba:itemtag_isbag") == true, tags?.Contains("ba:itemtag_isfullservicecashregister") == true,
                item.producerSettings != null && item.producerSettings.ResourcesRequired,
                rows ? whitelist : null, rows ? outputs : null);
        }

        private sealed class Shop
        {
            internal Shop(BuildingRegistration registration, int limit) { Registration = registration; Limit = limit; }
            internal BuildingRegistration Registration;
            internal int Limit;
            internal long Revision, PublishedCatalogue;
            internal FoodEligibilityImage? Image;
            internal bool Queued;
        }
        private readonly Dictionary<string, Shop> shops = new Dictionary<string, Shop>(StringComparer.Ordinal);
        private readonly Queue<Shop> dirty = new Queue<Shop>();
        private IEnumerator<Item>? catalogueCursor;
        private LoadedStockCatalogue? buildingCatalogue;
        private LoadedStockCatalogue? catalogue;
        private bool catalogueDirty = true;
        private long catalogueRevision = 1;
        private Shop? scanning;
        private long scanningRevision;
        private LoadedStockMenuBuilder? menu;
        private Dictionary<string, ItemInstance>.Enumerator furnitureCursor;
        private bool furnitureOpen;
        private readonly StockCargoFields[] cargoBuffer = new StockCargoFields[LoadedStockLimits.Rows];
        // Transient only; catches shared cargo references, including sharing with storage.
        private readonly HashSet<object> cargoIdentities = new HashSet<object>(ReferenceComparer.Instance);
        public long CatalogueRevision => catalogueRevision;
        public int CatalogueCount => catalogue?.Count ?? 0;
        public FoodEligibilityRefusal CatalogueRefusal => catalogueDirty ? FoodEligibilityRefusal.Dirty
            : catalogue?.Refusal ?? FoodEligibilityRefusal.CatalogueUnavailable;
        public int PendingShops => dirty.Count + (scanning == null ? 0 : 1);
        public long DefinitionsRead { get; private set; }
        public long HoldersRead { get; private set; }
        public long CataloguePasses { get; private set; }
        public long ShopPasses { get; private set; }

        public void MarkCatalogueDirty()
        {
            // Repeated notifications coalesce until work starts. In-progress work is discarded.
            if (catalogueDirty && buildingCatalogue == null) return;
            catalogueRevision++;
            catalogueDirty = true; catalogue = null;
            CloseCatalogue(); CancelShop();
            foreach (var shop in shops.Values) Enqueue(shop);
        }
        public bool MarkShopDirty(string key, BuildingRegistration registration, int maximumOffers = LoadedStockLimits.Offers)
        {
            if (string.IsNullOrEmpty(key) || registration == null) return false;
            if (!shops.TryGetValue(key, out var shop))
            {
                if (shops.Count == LoadedStockLimits.Shops) return false;
                shop = new Shop(registration, maximumOffers); shops.Add(key, shop);
            }
            shop.Registration = registration; shop.Limit = maximumOffers;
            shop.Revision++; shop.Image = null;
            if (ReferenceEquals(shop, scanning)) CancelShop();
            Enqueue(shop); return true;
        }
        private void Enqueue(Shop shop)
        { if (!shop.Queued) { shop.Queued = true; dirty.Enqueue(shop); } }
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
                if (dirty.Count == 0) return false;
                scanning = dirty.Dequeue(); scanning.Queued = false; scanningRevision = scanning.Revision;
                StartShop(scanning);
            }
            ContinueShop(); return true;
        }
        private void ContinueCatalogue()
        {
            try
            {
                if (buildingCatalogue == null)
                {
                    buildingCatalogue = new LoadedStockCatalogue(catalogueRevision); CataloguePasses++;
                    var all = ItemsGetter.AllItems;
                    if (all == null) { PublishCatalogue(FoodEligibilityRefusal.CatalogueUnavailable); return; }
                    catalogueCursor = all.GetEnumerator();
                }
                for (int i = 0; i < LoadedStockLimits.CataloguePerUnit; i++)
                {
                    if (!catalogueCursor!.MoveNext()) { PublishCatalogue(); return; }
                    // The sole extra enumeration step detects overflow without reading Current.
                    if (buildingCatalogue.Count == LoadedStockLimits.Catalogue)
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
            buildingCatalogue!.Seal(refusal); catalogue = buildingCatalogue;
            CloseCatalogue(); catalogueDirty = false;
        }
        private void CloseCatalogue()
        { catalogueCursor?.Dispose(); catalogueCursor = null; buildingCatalogue = null; }
        private void StartShop(Shop shop)
        {
            ShopPasses++;
            var r = shop.Registration;
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
            scanning!.Image = result; scanning.PublishedCatalogue = catalogueRevision;
            CancelShop(false);
        }
        private void CancelShop(bool requeue = true)
        {
            if (furnitureOpen) { furnitureCursor.Dispose(); furnitureCursor = default; furnitureOpen = false; }
            if (scanning != null && requeue) Enqueue(scanning);
            scanning = null; menu = null; cargoIdentities.Clear(); Array.Clear(cargoBuffer, 0, cargoBuffer.Length);
        }
        public void Reset()
        {
            CloseCatalogue(); CancelShop(false); dirty.Clear(); shops.Clear(); catalogue = null;
            catalogueRevision++; catalogueDirty = true;
        }
        public void Dispose() => Reset();
    }
}
