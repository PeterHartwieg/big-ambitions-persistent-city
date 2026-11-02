using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace PersistentCity.Core
{
    /// <summary>Version-1 provider input. Only nullable scalar policy fields; copied at registration.
    /// Null means absent. This cannot define items, prices, resources or sale permission.</summary>
    public sealed class FoodProductMetadataInput
    {
        public MealClass? MealClass { get; set; }
        public int? TasteBand { get; set; }
        public int? BreakfastFitCents { get; set; }
        public int? LunchFitCents { get; set; }
        public int? DinnerFitCents { get; set; }
    }

    /// <summary>Provider-scoped lifetime. Dispose unregisters all its names; old handles cannot
    /// register again after disposal or registry unload. No provider code is called by PC.</summary>
    public sealed class FoodProductMetadataProvider : IDisposable
    {
        private readonly FoodProductMetadataRegistry registry;
        internal readonly Dictionary<string, FoodProductMetadataOverride> Entries = new Dictionary<string, FoodProductMetadataOverride>(StringComparer.Ordinal);
        internal bool Active = true;
        internal FoodProductMetadataProvider(FoodProductMetadataRegistry registry, string id) { this.registry = registry; Id = id; }
        public string Id { get; }
        public bool TryRegisterProductMetadata(string canonicalName, FoodProductMetadataInput metadata, int apiVersion, out string reason)
            => registry.Register(this, canonicalName, metadata, apiVersion, out reason);
        public void Dispose() => registry.Unregister(this);
    }

    /// <summary>Staged metadata only. Registration is synchronized; publication/unload are explicitly
    /// bound to the host main thread. There is no Update, timer, reflection or provider callback.</summary>
    public sealed class FoodProductMetadataRegistry
    {
        public const int ApiVersion = 1;
        public const int MaximumProviders = 256;
        public const int MaximumProducts = 256;
        public const int MaximumWireBudget = 512 * 1024;
        public const string ConflictReason = "conflicting-providers";
        private readonly object gate = new object();
        private readonly Dictionary<string, FoodProductMetadataProvider> providers = new Dictionary<string, FoodProductMetadataProvider>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> names = new Dictionary<string, int>(StringComparer.Ordinal);
        private int characters = 2, bytes = 2, mainThread;
        private long revision, publishedRevision = -1;
        private LoadedStockCatalogue? publishedCatalogue;
        private FoodProductMetadataSnapshot published = FoodProductMetadataSnapshot.Empty;

        public int ProviderCount { get { lock (gate) return providers.Count; } }
        public int ProductCount { get { lock (gate) return names.Count; } }
        public int BudgetCharacters { get { lock (gate) return characters; } }
        public int BudgetUtf8Bytes { get { lock (gate) return bytes; } }
        public long Publications { get; private set; }
        public void BindPublicationThread()
        {
            lock (gate)
            {
                if (mainThread != 0 && mainThread != Thread.CurrentThread.ManagedThreadId)
                    throw new InvalidOperationException("Metadata publication belongs to the host main thread.");
                mainThread = Thread.CurrentThread.ManagedThreadId;
            }
        }
        private void RequireMainThread()
        {
            if (mainThread == 0 || mainThread != Thread.CurrentThread.ManagedThreadId)
                throw new InvalidOperationException("Metadata publication requires the bound host main thread.");
        }
        public bool TryCreateProvider(string providerId, out FoodProductMetadataProvider provider, out string reason)
        {
            provider = null!;
            lock (gate)
            {
                if (!FoodProductMetadataResolver.ValidName(providerId)) { reason = "invalid-provider-id"; return false; }
                if (providers.ContainsKey(providerId)) { reason = "provider-already-registered"; return false; }
                if (providers.Count == MaximumProviders) { reason = "provider-bound"; return false; }
                Cost(providerId, out int c, out int b);
                if (!Fits(c, b)) { reason = "registry-budget"; return false; }
                provider = new FoodProductMetadataProvider(this, providerId);
                // An empty provider changes bookkeeping, not the effective product image.
                providers.Add(providerId, provider); characters += c; bytes += b;
                reason = "accepted"; return true;
            }
        }
        internal bool Register(FoodProductMetadataProvider provider, string name, FoodProductMetadataInput input, int version, out string reason)
        {
            lock (gate)
            {
                if (!provider.Active) { reason = "provider-unregistered"; return false; }
                if (version != ApiVersion) { reason = "incompatible-api-version"; return false; }
                if (!FoodProductMetadataResolver.ValidName(name)) { reason = "invalid-product-name"; return false; }
                if (input == null) { reason = "invalid-metadata"; return false; }
                FoodProductMetadataOverride metadata;
                try { metadata = new FoodProductMetadataOverride(input.MealClass, input.TasteBand,
                    input.BreakfastFitCents, input.LunchFitCents, input.DinnerFitCents); }
                catch (ArgumentException) { reason = "invalid-metadata"; return false; }
                bool existing = provider.Entries.TryGetValue(name, out var previous);
                names.TryGetValue(name, out int count);
                if (!existing)
                {
                    if (count == 0 && names.Count == MaximumProducts) { reason = "product-bound"; return false; }
                    int c = 128, b = 128; // One fixed bounded claim, including provider/name references and scalar fields.
                    if (count == 0) { Cost(name, out int nc, out int nb); c += nc; b += nb; }
                    if (!Fits(c, b)) { reason = "registry-budget"; return false; }
                    characters += c; bytes += b; names[name] = ++count;
                }
                // Identical same-provider input causes no new revision or publication.
                if (!existing || !Same(previous, metadata)) { provider.Entries[name] = metadata; revision++; }
                // Even identical claims by distinct providers invalidate the name: no implicit shared ownership.
                // Retain both bounded claims so rejection cannot accidentally leave the first provider winning.
                reason = count > 1 ? ConflictReason : "accepted";
                return count == 1;
            }
        }
        internal static bool Same(FoodProductMetadataOverride a, FoodProductMetadataOverride b)
            => a.OptionalMealClass == b.OptionalMealClass && a.TasteBand == b.TasteBand
                && a.OptionalBreakfastFitCents == b.OptionalBreakfastFitCents && a.OptionalLunchFitCents == b.OptionalLunchFitCents
                && a.OptionalDinnerFitCents == b.OptionalDinnerFitCents;
        private bool Fits(int c, int b) => characters + c <= MaximumWireBudget && bytes + b <= MaximumWireBudget;
        // Conservative wire accounting, not a claim about managed heap size: strings charge escaped JSON
        // and UTF-8 separately plus 64 units for keys, separators, lifecycle/conflict bookkeeping.
        // Claims charge 128 units even when fields are absent. No serialization/all-registry scan per edit.
        private static void Cost(string value, out int c, out int b)
        {
            c = value.Length + 64; b = Encoding.UTF8.GetByteCount(value) + 64;
            foreach (char ch in value)
            {
                if (ch == '"' || ch == '\\') { c++; b++; }
                else if (ch == '\u0085' || ch == '\u2028' || ch == '\u2029') { c += 5; b += ch == '\u0085' ? 4 : 3; }
            }
        }
        internal void Unregister(FoodProductMetadataProvider provider)
        {
            lock (gate)
            {
                if (!provider.Active) return;
                bool hadEntries = provider.Entries.Count > 0;
                foreach (var entry in provider.Entries)
                {
                    int count = names[entry.Key] - 1;
                    characters -= 128; bytes -= 128;
                    if (count == 0)
                    { names.Remove(entry.Key); Cost(entry.Key, out int nc, out int nb); characters -= nc; bytes -= nb; }
                    else names[entry.Key] = count;
                }
                provider.Entries.Clear(); provider.Active = false; providers.Remove(provider.Id);
                Cost(provider.Id, out int c, out int b); characters -= c; bytes -= b;
                if (hadEntries) revision++;
            }
        }
        /// <summary>Atomic immutable publication at a host context boundary. A sealed E5a detached
        /// catalogue proves name presence only, never stock/eligibility. At most 256 name lookups,
        /// and bounded claims; no full catalogue scan. Repeated clean calls return the same image.</summary>
        public FoodProductMetadataSnapshot Publish(LoadedStockCatalogue? catalogue)
        {
            lock (gate)
            {
                RequireMainThread();
                var ready = catalogue?.Ready == true ? catalogue : null;
                if (publishedRevision == revision && ReferenceEquals(publishedCatalogue, ready)) return published;
                var values = new Dictionary<string, FoodProductMetadataOverride>(StringComparer.Ordinal);
                var reasons = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var entry in names)
                {
                    if (entry.Value > 1) reasons.Add(entry.Key, ConflictReason);
                    else if (ready == null || !ready.TryGet(entry.Key, out _)) reasons.Add(entry.Key, "unloaded-product");
                }
                // No claim can contribute when every name is conflicted/unloaded. Avoid a
                // redundant walk of up to the full registry budget during conflict bursts.
                if (reasons.Count < names.Count)
                    foreach (var provider in providers.Values)
                        foreach (var entry in provider.Entries)
                            if (!reasons.ContainsKey(entry.Key)) values.Add(entry.Key, entry.Value);
                published = new FoodProductMetadataSnapshot(values, reasons);
                publishedRevision = revision; publishedCatalogue = ready; Publications++;
                return published;
            }
        }
        public void ClearOnUnload()
        {
            lock (gate)
            {
                RequireMainThread();
                foreach (var provider in providers.Values) { provider.Entries.Clear(); provider.Active = false; }
                providers.Clear(); names.Clear(); characters = bytes = 2; revision++;
                published = FoodProductMetadataSnapshot.Empty; publishedRevision = -1; publishedCatalogue = null;
                mainThread = 0;
            }
        }
    }

    /// <summary>Detached immutable API image; only the registry can construct it.</summary>
    public sealed class FoodProductMetadataSnapshot
    {
        public static readonly FoodProductMetadataSnapshot Empty = new FoodProductMetadataSnapshot(
            new Dictionary<string, FoodProductMetadataOverride>(StringComparer.Ordinal), new Dictionary<string, string>(StringComparer.Ordinal));
        private readonly Dictionary<string, FoodProductMetadataOverride> values;
        private readonly Dictionary<string, string> reasons;
        internal FoodProductMetadataSnapshot(Dictionary<string, FoodProductMetadataOverride> values, Dictionary<string, string> reasons)
        { this.values = values; this.reasons = reasons; }
        public int Count => values.Count;
        public bool TryGet(string name, out FoodProductMetadataOverride metadata) => values.TryGetValue(name, out metadata);
        public string Reason(string name) => reasons.TryGetValue(name, out var reason) ? reason : values.ContainsKey(name) ? "accepted" : "not-registered";
    }

    public enum FoodProductMetadataSource { Default, Api, Config, Original }
    public readonly struct FoodProductMetadataSources
    {
        public static readonly FoodProductMetadataSources Original = new FoodProductMetadataSources(FoodProductMetadataSource.Original);
        private FoodProductMetadataSources(FoodProductMetadataSource source)
        { MealClass = TasteBand = BreakfastFit = LunchFit = DinnerFit = source; }
        internal FoodProductMetadataSources(FoodProductMetadataOverride config, FoodProductMetadataOverride api)
        {
            MealClass = Source(config.OptionalMealClass.HasValue, api.OptionalMealClass.HasValue);
            TasteBand = Source(config.TasteBand.HasValue, api.TasteBand.HasValue);
            BreakfastFit = Source(config.OptionalBreakfastFitCents.HasValue, api.OptionalBreakfastFitCents.HasValue);
            LunchFit = Source(config.OptionalLunchFitCents.HasValue, api.OptionalLunchFitCents.HasValue);
            DinnerFit = Source(config.OptionalDinnerFitCents.HasValue, api.OptionalDinnerFitCents.HasValue);
        }
        private static FoodProductMetadataSource Source(bool config, bool api) => config ? FoodProductMetadataSource.Config
            : api ? FoodProductMetadataSource.Api : FoodProductMetadataSource.Default;
        public FoodProductMetadataSource MealClass { get; }
        public FoodProductMetadataSource TasteBand { get; }
        public FoodProductMetadataSource BreakfastFit { get; }
        public FoodProductMetadataSource LunchFit { get; }
        public FoodProductMetadataSource DinnerFit { get; }
    }
}
