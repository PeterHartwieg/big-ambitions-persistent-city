using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace PersistentCity.Core
{
    public sealed class FoodProductMetadataInput
    {
        public MealClass? MealClass { get; set; }
        public int? TasteBand { get; set; }
        public int? BreakfastFitCents { get; set; }
        public int? LunchFitCents { get; set; }
        public int? DinnerFitCents { get; set; }
    }

    public sealed class FoodProductMetadataLoadedNames
    {
        public const int MaximumNames = 16384;
        private readonly HashSet<string> names;
        public FoodProductMetadataLoadedNames(IEnumerable<string> loadedNames)
        {
            if (loadedNames == null) throw new ArgumentNullException(nameof(loadedNames));
            int capacity = loadedNames is ICollection<string> collection ? collection.Count : 0;
            if (capacity < 0 || capacity > MaximumNames) throw new ArgumentException("Overbound loaded-name image.");
            names = new HashSet<string>(capacity, StringComparer.Ordinal);
            foreach (string name in loadedNames)
            {
                if (names.Count == MaximumNames || !FoodProductMetadataRegistry.ValidApiName(name) || !names.Add(name))
                    throw new ArgumentException("Invalid, duplicate or overbound loaded-name image.");
            }
        }
        public int Count => names.Count;
        public bool Contains(string name) => names.Contains(name);
    }

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
        private long revision, publishedRevision = -1, publications;
        private FoodProductMetadataLoadedNames? publishedCatalogue;
        private FoodProductMetadataSnapshot published = FoodProductMetadataSnapshot.Empty;

        public int ProviderCount { get { lock (gate) return providers.Count; } }
        public int ProductCount { get { lock (gate) return names.Count; } }
        public int BudgetCharacters { get { lock (gate) return characters; } }
        public int BudgetUtf8Bytes { get { lock (gate) return bytes; } }
        public long Publications { get { lock (gate) return publications; } }
        internal static bool ValidApiName(string? name) => FoodProductMetadataResolver.ValidName(name) && name!.IndexOf('\\') < 0;
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
                if (!ValidApiName(providerId)) { reason = "invalid-provider-id"; return false; }
                if (providers.ContainsKey(providerId)) { reason = "provider-already-registered"; return false; }
                if (providers.Count == MaximumProviders) { reason = "provider-bound"; return false; }
                Cost(providerId, out int c, out int b);
                if (!Fits(c, b)) { reason = "registry-budget"; return false; }
                provider = new FoodProductMetadataProvider(this, providerId);
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
                if (!ValidApiName(name)) { reason = "invalid-product-name"; return false; }
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
                    int c = 128, b = 128;
                    if (count == 0) { Cost(name, out int nc, out int nb); c += nc; b += nb; }
                    if (!Fits(c, b)) { reason = "registry-budget"; return false; }
                    characters += c; bytes += b; names[name] = ++count;
                }
                if (!existing || !Same(previous, metadata)) { provider.Entries[name] = metadata; revision++; }
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
        public FoodProductMetadataSnapshot Publish(FoodProductMetadataLoadedNames? catalogue)
        {
            lock (gate)
            {
                RequireMainThread();
                var ready = catalogue;
                if (publishedRevision == revision && ReferenceEquals(publishedCatalogue, ready)) return published;
                var values = new Dictionary<string, FoodProductMetadataOverride>(StringComparer.Ordinal);
                var reasons = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var entry in names)
                {
                    if (entry.Value > 1) reasons.Add(entry.Key, ConflictReason);
                    else if (ready == null || !ready.Contains(entry.Key)) reasons.Add(entry.Key, "unloaded-product");
                }
                if (reasons.Count < names.Count)
                    foreach (var provider in providers.Values)
                        foreach (var entry in provider.Entries)
                            if (!reasons.ContainsKey(entry.Key)) values.Add(entry.Key, entry.Value);
                published = new FoodProductMetadataSnapshot(values, reasons);
                publishedRevision = revision; publishedCatalogue = ready; publications++;
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
