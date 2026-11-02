using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public static class LoadedStockLimits
    {
        public const int Catalogue = 16384, Name = 256, Furniture = 2000, Rows = 128, Offers = 8, Shops = 1024;
        public const int CataloguePerUnit = 256, FurniturePerUnit = 128;
        public static bool CanonicalName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name) || name!.Length > Name) return false;
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsControl(c) || c == '\\' || c == '|') return false;
                if (char.IsHighSurrogate(c))
                { if (++i == name.Length || !char.IsLowSurrogate(name[i])) return false; }
                else if (char.IsLowSurrogate(c)) return false;
            }
            return true;
        }
    }

    /// <summary>Detached loaded fields. Optional native arrays may be null (no entries).
    /// Missing/unreadable required metadata is represented explicitly, never as an ungated default.</summary>
    public sealed class LoadedStockDefinition
    {
        private static readonly IReadOnlyList<string> EmptyNames = Array.AsReadOnly(Array.Empty<string>());
        public LoadedStockDefinition(string name, bool retail, bool service, bool showcase, bool workstation,
            bool metadataVerified, bool ungated, bool bag, bool counter, bool consumes,
            string[]? whitelist, string[]? outputs)
        {
            Name = name; Retail = retail; Service = service; Showcase = showcase; Workstation = workstation;
            MetadataVerified = metadataVerified; Ungated = ungated; Bag = bag; Counter = counter; Consumes = consumes;
            Whitelist = Copy(whitelist); Outputs = Copy(outputs);
        }
        private static IReadOnlyList<string>? Copy(string[]? values)
            => values == null ? null : values.Length == 0 ? EmptyNames : values.Length > LoadedStockLimits.Rows
                ? throw new ArgumentException("Overbound definition rows") : Array.AsReadOnly((string[])values.Clone());
        public string Name { get; }
        public bool Retail { get; }
        public bool Service { get; }
        public bool Showcase { get; }
        public bool Workstation { get; }
        public bool MetadataVerified { get; }
        public bool Ungated { get; }
        public bool Bag { get; }
        public bool Counter { get; }
        public bool Consumes { get; }
        public IReadOnlyList<string>? Whitelist { get; }
        public IReadOnlyList<string>? Outputs { get; }
    }

    public sealed class LoadedStockCatalogue
    {
        private readonly Dictionary<string, LoadedStockDefinition> definitions = new Dictionary<string, LoadedStockDefinition>(StringComparer.Ordinal);
        private bool complete;
        public LoadedStockCatalogue(long revision) { Revision = revision; }
        public long Revision { get; }
        public FoodEligibilityRefusal Refusal { get; private set; } = FoodEligibilityRefusal.CatalogueUnavailable;
        public int Count => definitions.Count;
        public bool Ready => complete && Refusal == FoodEligibilityRefusal.None;
        public bool Add(LoadedStockDefinition definition)
        {
            if (complete) throw new InvalidOperationException("Catalogue already sealed");
            if (definitions.Count == LoadedStockLimits.Catalogue) { Seal(FoodEligibilityRefusal.CatalogueBounds); return false; }
            if (definition == null || !LoadedStockLimits.CanonicalName(definition.Name) || definitions.ContainsKey(definition.Name))
            { Seal(FoodEligibilityRefusal.CatalogueIdentity); return false; }
            definitions.Add(definition.Name, definition); return true;
        }
        public void Seal(FoodEligibilityRefusal refusal = FoodEligibilityRefusal.None)
        { if (complete) return; complete = true; Refusal = refusal; }
        public bool TryGet(string name, out LoadedStockDefinition definition)
        { definition = null!; return Ready && name != null && definitions.TryGetValue(name, out definition!); }
    }

    public readonly struct StockPriceFields
    {
        public StockPriceFields(string name, float price) { Name = name; Price = price; }
        public string Name { get; }
        public float Price { get; }
    }
    public readonly struct StockCargoFields
    {
        public StockCargoFields(string name, int amount, int nestedCount) { Name = name; Amount = amount; NestedCount = nestedCount; }
        public string Name { get; }
        public int Amount { get; }
        public int NestedCount { get; }
    }

    /// <summary>Pure, incremental whole-menu validator. Only Finish publishes a detached image.
    /// Raw zero is known evidence; it is never a promise of sellable supply.</summary>
    public sealed class LoadedStockMenuBuilder
    {
        private readonly LoadedStockCatalogue catalogue;
        private readonly long revision;
        private FoodEligibilityRefusal refusal;
        private readonly string[] products;
        private readonly Money[] prices;
        private readonly int[] stock, observed;
        private readonly List<FoodHolderResourceImage> holders;
        private readonly HashSet<string>? identities;
        private int bags, counters, furniture;
        private bool finished;
        public static LoadedStockMenuBuilder Refused(LoadedStockCatalogue catalogue, long shopRevision, FoodEligibilityRefusal reason)
            => new LoadedStockMenuBuilder(catalogue, shopRevision, reason);
        private LoadedStockMenuBuilder(LoadedStockCatalogue catalogue, long shopRevision, FoodEligibilityRefusal reason)
        {
            if (reason == FoodEligibilityRefusal.None) throw new ArgumentOutOfRangeException(nameof(reason));
            this.catalogue = catalogue; revision = shopRevision; refusal = reason;
            products = Array.Empty<string>(); prices = Array.Empty<Money>(); stock = observed = Array.Empty<int>();
            holders = new List<FoodHolderResourceImage>();
        }
        public LoadedStockMenuBuilder(LoadedStockCatalogue catalogue, long shopRevision, bool businessContract,
            IReadOnlyList<string>? menu, IReadOnlyList<StockPriceFields>? priceRows, int maximumOffers = LoadedStockLimits.Offers,
            int furnitureCount = 0, bool uniqueOrdinalDictionaryKeys = false)
        {
            this.catalogue = catalogue; revision = shopRevision;
            // Runtime proves default/ordinal dictionary keys and key == instance ID. Other
            // pure callers retain duplicate detection. Capacity is checked before allocation.
            identities = uniqueOrdinalDictionaryKeys ? null : new HashSet<string>(StringComparer.Ordinal);
            holders = new List<FoodHolderResourceImage>();
            products = Array.Empty<string>(); prices = Array.Empty<Money>(); stock = observed = Array.Empty<int>();
            if (furnitureCount < 0 || furnitureCount > LoadedStockLimits.Furniture) { Fail(FoodEligibilityRefusal.FurnitureBounds); return; }
            if (!catalogue.Ready) { Fail(catalogue.Refusal); return; }
            if (!businessContract) { Fail(FoodEligibilityRefusal.BusinessContract); return; }
            if (maximumOffers < 1 || maximumOffers > LoadedStockLimits.Offers || menu == null || menu.Count < 1
                || menu.Count > maximumOffers || priceRows == null || priceRows.Count > LoadedStockLimits.Rows)
            { Fail(FoodEligibilityRefusal.MenuBounds); return; }
            products = new string[menu.Count]; prices = new Money[menu.Count]; stock = new int[menu.Count]; observed = new int[menu.Count];
            for (int i = 0; i < products.Length; i++)
            {
                string id = menu[i];
                if (!LoadedStockLimits.CanonicalName(id)) { Fail(FoodEligibilityRefusal.MenuIdentity); return; }
                for (int j = 0; j < i; j++) if (id == products[j]) { Fail(FoodEligibilityRefusal.MenuIdentity); return; }
                products[i] = id;
                if (!Definition(id, out var d)) return;
                if (!d.Retail || d.Service) { Fail(FoodEligibilityRefusal.RetailType); return; }
                int count = 0; float price = 0;
                for (int j = 0; j < priceRows.Count; j++) if (priceRows[j].Name == id) { count++; price = priceRows[j].Price; }
                if (count != 1) { Fail(FoodEligibilityRefusal.Price); return; }
                try { prices[i] = Money.FromNative(price); }
                catch (ArgumentOutOfRangeException) { Fail(FoodEligibilityRefusal.Price); return; }
                if (prices[i] == Money.Zero) { Fail(FoodEligibilityRefusal.Price); return; }
            }
            holders.Capacity = furnitureCount;
        }
        public FoodEligibilityRefusal Refusal => refusal;
        public void Fail(FoodEligibilityRefusal reason) { if (refusal == FoodEligibilityRefusal.None) refusal = reason; }
        private bool Definition(string id, out LoadedStockDefinition d)
        {
            if (!catalogue.TryGet(id, out d!)) { Fail(FoodEligibilityRefusal.MissingDefinition); return false; }
            if (!d.MetadataVerified) { Fail(FoodEligibilityRefusal.Metadata); return false; }
            if (!d.Ungated) { Fail(FoodEligibilityRefusal.Seasonal); return false; }
            return true;
        }
        private int Product(string name)
        { for (int i = 0; i < products.Length; i++) if (products[i] == name) return i; return -1; }
        private static bool Contains(IReadOnlyList<string>? values, string name)
        { if (values != null) for (int i = 0; i < values.Count; i++) if (values[i] == name) return true; return false; }
        public void AddHolder(string key, string instanceId, string definitionId, bool cargoPresent,
            ReadOnlySpan<StockCargoFields> cargo, bool sharedCargo = false)
        {
            if (finished) throw new InvalidOperationException("Menu already finished");
            if (refusal != FoodEligibilityRefusal.None) return;
            if (++furniture > LoadedStockLimits.Furniture) { Fail(FoodEligibilityRefusal.FurnitureBounds); return; }
            if (string.IsNullOrEmpty(instanceId) || key != instanceId || identities != null && !identities.Add(instanceId))
            { Fail(FoodEligibilityRefusal.FurnitureIdentity); return; }
            // Equipment must exist; only participating holder/bag definitions need ungated metadata.
            if (!catalogue.TryGet(definitionId, out var d)) { Fail(FoodEligibilityRefusal.MissingDefinition); return; }
            if (!d.MetadataVerified) { Fail(FoodEligibilityRefusal.Metadata); return; }
            if (cargo.Length > LoadedStockLimits.Rows) { Fail(FoodEligibilityRefusal.CargoBounds); return; }
            bool contains = false, produces = false;
            for (int i = 0; i < cargo.Length; i++)
            {
                if (!catalogue.TryGet(cargo[i].Name, out _)) { Fail(FoodEligibilityRefusal.MissingDefinition); return; }
                contains |= Product(cargo[i].Name) >= 0;
            }
            for (int i = 0; i < products.Length; i++) produces |= Contains(d.Outputs, products[i]);
            if (!d.Showcase && (produces || contains && d.Workstation)) { Fail(FoodEligibilityRefusal.Workstation); return; }
            if (!d.Counter && !contains && !produces) return;
            if (d.Counter && (contains || produces || d.Showcase)) { Fail(FoodEligibilityRefusal.CounterProductSharing); return; }
            if (!d.Counter && !d.Showcase) return; // storage never contributes stock
            if (!d.Ungated) { Fail(FoodEligibilityRefusal.Seasonal); return; }
            if (sharedCargo) { Fail(FoodEligibilityRefusal.SharedCargo); return; }
            if (!d.Consumes) { Fail(FoodEligibilityRefusal.Resources); return; }
            if (!cargoPresent || cargo.Length != 1 || cargo[0].Amount < 0 || cargo[0].NestedCount != 0)
            { Fail(FoodEligibilityRefusal.CargoShape); return; }
            var c = cargo[0];
            if (d.Counter)
            {
                if (!Definition(c.Name, out var bag)) return;
                if (!bag.Bag) { Fail(FoodEligibilityRefusal.CargoShape); return; }
                if (c.Amount > int.MaxValue - bags) { Fail(FoodEligibilityRefusal.Overflow); return; }
                bags += c.Amount; counters++;
            }
            else
            {
                int p = Product(c.Name);
                if (p < 0) { Fail(FoodEligibilityRefusal.ConvertedInput); return; }
                if (!Contains(d.Whitelist, c.Name)) { Fail(FoodEligibilityRefusal.Whitelist); return; }
                if (d.Outputs != null && d.Outputs.Count > 0 && (d.Outputs.Count != 1 || d.Outputs[0] != c.Name))
                { Fail(FoodEligibilityRefusal.Outputs); return; }
                if (c.Amount > int.MaxValue - stock[p]) { Fail(FoodEligibilityRefusal.Overflow); return; }
                stock[p] += c.Amount; observed[p]++;
            }
            holders.Add(new FoodHolderResourceImage(instanceId, definitionId, c.Name, d.Counter));
        }
        public FoodEligibilityImage Finish()
        {
            finished = true;
            if (refusal == FoodEligibilityRefusal.None)
            {
                for (int i = 0; i < observed.Length; i++) if (observed[i] == 0) { Fail(FoodEligibilityRefusal.NoHolder); break; }
                if (counters == 0) Fail(FoodEligibilityRefusal.NoCounter);
            }
            if (refusal != FoodEligibilityRefusal.None) return new FoodEligibilityImage(catalogue.Revision, revision, refusal);
            var offers = new FoodStockOfferImage[products.Length];
            for (int i = 0; i < offers.Length; i++) offers[i] = new FoodStockOfferImage(products[i], prices[i], stock[i]);
            // Stable output independent of native dictionary/menu enumeration order.
            Array.Sort(offers, (a, b) => StringComparer.Ordinal.Compare(a.ProductId, b.ProductId));
            holders.Sort((a, b) => StringComparer.Ordinal.Compare(a.HolderId, b.HolderId));
            return new FoodEligibilityImage(catalogue.Revision, revision, refusal, offers, holders.ToArray(), bags, counters);
        }
    }
}
