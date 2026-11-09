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

    /// <summary>E5b3 Decision 3a: the live fields of one loaded definition that the contract reads, uncopied.
    /// Runtime fills it from a live <c>Item</c> without allocating; <see cref="LoadedStockDefinition.Matches"/>
    /// compares it with the catalogue copy by content. Tag lists and the cargo whitelist enter as count plus an
    /// ordinal FNV-1a hash (<see cref="LoadedStockRows.Hash"/>); the derived bag, counter and verified facts are exact.</summary>
    public readonly struct LoadedStockFacts
    {
        public LoadedStockFacts(string name, bool retail, bool service, bool showcase, bool workstation,
            bool metadataVerified, bool ungated, bool bag, bool counter, bool consumes,
            IReadOnlyList<string>? whitelist, IReadOnlyList<string>? outputs,
            int tagCount = -1, ulong tagHash = 0, int cargoCapacity = 0, int cargoWhitelistCount = -1, ulong cargoWhitelistHash = 0)
        {
            Name = name; Retail = retail; Service = service; Showcase = showcase; Workstation = workstation;
            MetadataVerified = metadataVerified; Ungated = ungated; Bag = bag; Counter = counter; Consumes = consumes;
            Whitelist = whitelist; Outputs = outputs; TagCount = tagCount; TagHash = tagHash; CargoCapacity = cargoCapacity;
            CargoWhitelistCount = cargoWhitelistCount; CargoWhitelistHash = cargoWhitelistHash;
        }
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
        /// <summary>-1 when the list is null or unreadable.</summary>
        public int TagCount { get; }
        public ulong TagHash { get; }
        public int CargoCapacity { get; }
        public int CargoWhitelistCount { get; }
        public ulong CargoWhitelistHash { get; }
    }

    public static class LoadedStockRows
    {
        /// <summary>Ordinal FNV-1a over count, lengths and chars. Deterministic across runs; no allocation.</summary>
        public static ulong Hash(IReadOnlyList<string?>? values)
        {
            if (values == null) return 0;
            ulong h = 14695981039346656037UL;
            void Mix(int v) { h = (h ^ (uint)v) * 1099511628211UL; }
            Mix(values.Count);
            for (int i = 0; i < values.Count; i++)
            {
                string? s = values[i];
                if (s == null) { Mix(-1); continue; }
                Mix(s.Length);
                for (int j = 0; j < s.Length; j++) Mix(s[j]);
            }
            return h;
        }
        /// <summary>Element-by-element ordinal equality; null equals only null.</summary>
        public static bool Same(IReadOnlyList<string>? copy, IReadOnlyList<string>? live)
        {
            if (copy == null || live == null) return copy == null && live == null;
            if (copy.Count != live.Count) return false;
            for (int i = 0; i < copy.Count; i++) if (!string.Equals(copy[i], live[i], StringComparison.Ordinal)) return false;
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
            : this(new LoadedStockFacts(name, retail, service, showcase, workstation, metadataVerified, ungated, bag, counter, consumes,
                whitelist, outputs)) { }
        /// <summary>Copies the live rows once per catalogue revision (or per Decision 3a re-copy).</summary>
        public LoadedStockDefinition(in LoadedStockFacts facts)
        {
            Name = facts.Name; Retail = facts.Retail; Service = facts.Service; Showcase = facts.Showcase; Workstation = facts.Workstation;
            MetadataVerified = facts.MetadataVerified; Ungated = facts.Ungated; Bag = facts.Bag; Counter = facts.Counter; Consumes = facts.Consumes;
            Whitelist = Copy(facts.Whitelist); Outputs = Copy(facts.Outputs);
            TagCount = facts.TagCount; TagHash = facts.TagHash; CargoCapacity = facts.CargoCapacity;
            CargoWhitelistCount = facts.CargoWhitelistCount; CargoWhitelistHash = facts.CargoWhitelistHash;
        }
        private static IReadOnlyList<string>? Copy(IReadOnlyList<string>? values)
        {
            if (values == null) return null;
            if (values.Count == 0) return EmptyNames;
            if (values.Count > LoadedStockLimits.Rows) throw new ArgumentException("Overbound definition rows");
            var copy = new string[values.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = values[i];
            return Array.AsReadOnly(copy);
        }
        /// <summary>Decision 3a: live content equals this copy, element by element. An in-place element edit of
        /// <c>itemsThatCanShowcase</c> or <c>itemsToProduce</c>, or any tag edit, is a mismatch.</summary>
        public bool Matches(in LoadedStockFacts live)
            => string.Equals(Name, live.Name, StringComparison.Ordinal) && Retail == live.Retail && Service == live.Service
                && Showcase == live.Showcase && Workstation == live.Workstation && MetadataVerified == live.MetadataVerified
                && Ungated == live.Ungated && Bag == live.Bag && Counter == live.Counter && Consumes == live.Consumes
                && TagCount == live.TagCount && TagHash == live.TagHash && CargoCapacity == live.CargoCapacity
                && CargoWhitelistCount == live.CargoWhitelistCount && CargoWhitelistHash == live.CargoWhitelistHash
                && LoadedStockRows.Same(Whitelist, live.Whitelist) && LoadedStockRows.Same(Outputs, live.Outputs);
        /// <summary>Same copied facts as <paramref name="other"/>, field by field (a full catalogue rebuild makes new
        /// copy objects; an unchanged definition still compares equal).</summary>
        public bool SameContent(LoadedStockDefinition other)
            => other != null && Matches(new LoadedStockFacts(other.Name, other.Retail, other.Service, other.Showcase, other.Workstation,
                other.MetadataVerified, other.Ungated, other.Bag, other.Counter, other.Consumes, other.Whitelist, other.Outputs,
                other.TagCount, other.TagHash, other.CargoCapacity, other.CargoWhitelistCount, other.CargoWhitelistHash));
        public int TagCount { get; }
        public ulong TagHash { get; }
        public int CargoCapacity { get; }
        public int CargoWhitelistCount { get; }
        public ulong CargoWhitelistHash { get; }
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
        /// <summary>Most definitions one revision chain may re-copy in place before a full rebuild is required.</summary>
        public const int MaximumReplacements = 256;
        private readonly Dictionary<string, LoadedStockDefinition> definitions;
        // Decision 3a re-copies: a sealed revision that shares the base table and overrides a few names.
        private readonly Dictionary<string, LoadedStockDefinition>? replacements;
        private bool complete;
        public LoadedStockCatalogue(long revision)
        { Revision = revision; definitions = new Dictionary<string, LoadedStockDefinition>(StringComparer.Ordinal); }
        private LoadedStockCatalogue(long revision, Dictionary<string, LoadedStockDefinition> shared, Dictionary<string, LoadedStockDefinition> overrides)
        { Revision = revision; definitions = shared; replacements = overrides; complete = true; Refusal = FoodEligibilityRefusal.None; }
        public long Revision { get; }
        public FoodEligibilityRefusal Refusal { get; private set; } = FoodEligibilityRefusal.CatalogueUnavailable;
        public int Count => definitions.Count;
        public int Replacements => replacements?.Count ?? 0;
        public bool Ready => complete && Refusal == FoodEligibilityRefusal.None;
        /// <summary>Decision 3a: a new sealed revision whose named definitions are re-copied from live content.
        /// Every other definition is shared, so this costs the replaced rows only. Null when this catalogue is not
        /// ready, a name is not already present, or the chain would exceed <see cref="MaximumReplacements"/>:
        /// the caller then rebuilds the whole catalogue.</summary>
        public LoadedStockCatalogue? WithReplacements(long revision, IReadOnlyList<LoadedStockDefinition> changed)
        {
            if (!Ready || revision <= Revision || changed == null || changed.Count == 0) return null;
            var overrides = replacements == null ? new Dictionary<string, LoadedStockDefinition>(StringComparer.Ordinal)
                : new Dictionary<string, LoadedStockDefinition>(replacements, StringComparer.Ordinal);
            for (int i = 0; i < changed.Count; i++)
            {
                var d = changed[i];
                if (d == null || !definitions.ContainsKey(d.Name)) return null;
                overrides[d.Name] = d;
                if (overrides.Count > MaximumReplacements) return null;
            }
            return new LoadedStockCatalogue(revision, definitions, overrides);
        }
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
        {
            definition = null!;
            if (!Ready || name == null) return false;
            return replacements != null && replacements.TryGetValue(name, out definition!) || definitions.TryGetValue(name, out definition!);
        }
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
