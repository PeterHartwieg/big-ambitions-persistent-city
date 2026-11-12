using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{

    public enum FoodContractPhase
    {
        Clean,
        Dirty,
        Rebuilding,
        Published,
        Refused,
        Recovering
    }

    public readonly struct FoodHolderSlot
    {
        public FoodHolderSlot(string holderId, string definitionId, string resourceId, bool bag, object? instance,
            object? cargo, int cargoVersion, int cargoCount, object? stock, string? stockName, int nested)
        {
            HolderId = holderId; DefinitionId = definitionId; ResourceId = resourceId; IsBagCounter = bag; Instance = instance;
            Cargo = cargo; CargoVersion = cargoVersion; CargoCount = cargoCount; Stock = stock; StockName = stockName; Nested = nested;
        }
        public string HolderId { get; }
        public string DefinitionId { get; }
        public string ResourceId { get; }
        public bool IsBagCounter { get; }
        public object? Instance { get; }
        public object? Cargo { get; }
        public int CargoVersion { get; }
        public int CargoCount { get; }
        public object? Stock { get; }
        public string? StockName { get; }
        public int Nested { get; }
        public bool Shaped => Instance != null && Cargo != null && CargoCount == 1 && Stock != null && Nested == 0
            && string.Equals(StockName, ResourceId, StringComparison.Ordinal);
        public bool Matches(in FoodHolderSlot live)
            => Instance != null && ReferenceEquals(Instance, live.Instance) && string.Equals(DefinitionId, live.DefinitionId, StringComparison.Ordinal)
                && Cargo != null && ReferenceEquals(Cargo, live.Cargo) && CargoVersion == live.CargoVersion && CargoCount == live.CargoCount
                && Stock != null && ReferenceEquals(Stock, live.Stock) && string.Equals(StockName, live.StockName, StringComparison.Ordinal)
                && Nested == live.Nested;
    }

    public sealed class FoodDefinitionDependency
    {
        public FoodDefinitionDependency(string name, object live, LoadedStockDefinition copy)
        { Name = name; Live = live; Copy = copy; }
        public string Name { get; }
        public object Live { get; }
        public LoadedStockDefinition Copy { get; }
    }

    public readonly struct FoodBusinessTypeFacts
    {
        public FoodBusinessTypeFacts(object? live, Type? simulator, bool entranceFee, IReadOnlyList<string>? tags)
        { Live = live; Simulator = simulator; EntranceFee = entranceFee; Tags = tags; }
        public object? Live { get; }
        public Type? Simulator { get; }
        public bool EntranceFee { get; }
        public IReadOnlyList<string>? Tags { get; }
    }

    public sealed class FoodBusinessTypeCopy
    {
        public FoodBusinessTypeCopy(in FoodBusinessTypeFacts facts)
        {
            Live = facts.Live; Simulator = facts.Simulator; EntranceFee = facts.EntranceFee;
            if (facts.Tags != null && facts.Tags.Count <= LoadedStockLimits.Rows)
            {
                var tags = new string[facts.Tags.Count];
                for (int i = 0; i < tags.Length; i++) tags[i] = facts.Tags[i];
                Tags = Array.AsReadOnly(tags);
            }
        }
        public object? Live { get; }
        public Type? Simulator { get; }
        public bool EntranceFee { get; }
        public IReadOnlyList<string>? Tags { get; }
        public bool Matches(in FoodBusinessTypeFacts live)
            => Live != null && ReferenceEquals(Live, live.Live) && Simulator == live.Simulator && EntranceFee == live.EntranceFee
                && Tags != null && LoadedStockRows.Same(Tags, live.Tags);
    }

    public sealed class FoodContractImage
    {
        private readonly FoodHolderSlot[] holders;
        private readonly FoodDefinitionDependency[] definitions;
        private readonly Money[] prices;
        public FoodContractImage(string key, long builtHour, FoodEligibilityImage eligibility, FoodHolderSlot[] holders,
            FoodDefinitionDependency[] definitions, FoodBusinessTypeCopy? business, CompleteFoodMenuRefusal admission, object? stamp = null)
        {
            if (string.IsNullOrEmpty(key) || eligibility == null || holders == null || definitions == null) throw new ArgumentException("Incomplete contract image.");
            Key = key; BuiltHour = builtHour; Eligibility = eligibility; this.holders = holders; this.definitions = definitions;
            Business = business; Admission = admission; Stamp = stamp;
            prices = new Money[eligibility.Offers.Count];
            for (int i = 0; i < prices.Length; i++) prices[i] = eligibility.Offers[i].Price;
        }
        private FoodContractImage(FoodContractImage source, Money[] prices)
        {
            Key = source.Key; BuiltHour = source.BuiltHour; Eligibility = source.Eligibility; holders = source.holders;
            definitions = source.definitions; Business = source.Business; Admission = source.Admission; Stamp = source.Stamp;
            this.prices = prices;
        }
        public string Key { get; }
        public long BuiltHour { get; }
        public long CatalogueRevision => Eligibility.CatalogueRevision;
        public FoodEligibilityImage Eligibility { get; }
        public IReadOnlyList<FoodHolderSlot> Holders => holders;
        public IReadOnlyList<FoodDefinitionDependency> Definitions => definitions;
        public FoodBusinessTypeCopy? Business { get; }
        public CompleteFoodMenuRefusal Admission { get; }
        public object? Stamp { get; }
        public IReadOnlyList<Money> Prices => prices;
        public bool Passed => Eligibility.StockContractMatched && Admission == CompleteFoodMenuRefusal.None && holders.Length > 0;
        public FoodContractImage WithPrices(Money[] next)
        {
            if (next == null || next.Length != prices.Length) throw new ArgumentException("Price row count differs.");
            return new FoodContractImage(this, (Money[])next.Clone());
        }
        public bool HasHolder(object instance) => HolderIndex(instance) >= 0;

        private Dictionary<object, int>? index;
        private int[]? marks;
        private int markEpoch;
        internal int HolderIndex(object instance)
        {
            if (index == null)
            {
                index = new Dictionary<object, int>(holders.Length, ReferenceKey.Instance);
                for (int i = 0; i < holders.Length; i++)
                    if (holders[i].Instance != null && !index.ContainsKey(holders[i].Instance!)) index.Add(holders[i].Instance!, i);
            }
            return instance != null && index.TryGetValue(instance, out int found) ? found : -1;
        }
        internal int[] Marks(out int epoch)
        {
            marks ??= new int[holders.Length];
            if (++markEpoch == int.MaxValue) { Array.Clear(marks, 0, marks.Length); markEpoch = 1; }
            epoch = markEpoch; return marks;
        }
    }

    internal sealed class ReferenceKey : IEqualityComparer<object>
    {
        internal static readonly ReferenceKey Instance = new ReferenceKey();
        public new bool Equals(object? a, object? b) => ReferenceEquals(a, b);
        public int GetHashCode(object value) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
    }

    public interface IFoodContractLive
    {
        bool ReadHolder(in FoodHolderSlot image, out FoodHolderSlot live);
        bool ReadDefinition(string name, out object? live, out LoadedStockFacts facts);
        bool ReadBusinessType(out FoodBusinessTypeFacts live);
        int PriceRows(string product, out float price);
    }

    public enum FoodContractRecheckRefusal { None, Holder, DefinitionReference, DefinitionContent, BusinessType, Price }

    public enum FoodPriceVerdict { Same, PriceOnly, Refused }

    public sealed class FoodDefinitionEpoch
    {
        private const int MaximumEntries = 4096;
        private struct Seen { internal long Epoch; internal object Live; internal bool Same, Loaded; }
        private readonly Dictionary<LoadedStockDefinition, Seen> seen = new Dictionary<LoadedStockDefinition, Seen>();
        public long Epoch { get; private set; }
        public long Compared { get; private set; }
        public long Shared { get; private set; }
        public void Begin() { Epoch++; if (seen.Count > MaximumEntries) seen.Clear(); }
        internal bool TryGet(FoodDefinitionDependency d, out bool same)
        {
            if (seen.TryGetValue(d.Copy, out var s) && s.Epoch == Epoch && ReferenceEquals(s.Live, d.Live)) { Shared++; same = s.Same; return true; }
            same = false; return false;
        }
        internal void Set(FoodDefinitionDependency d, bool same, bool loaded)
        { Compared++; seen[d.Copy] = new Seen { Epoch = Epoch, Live = d.Live, Same = same, Loaded = loaded }; }
        internal bool Loaded(FoodDefinitionDependency d) => seen.TryGetValue(d.Copy, out var s) && s.Loaded;
        public void Clear() { seen.Clear(); }
    }

    public static class FoodContractRecheck
    {
        public static FoodContractRecheckRefusal Structure(FoodContractImage image, IFoodContractLive live, FoodDefinitionEpoch epoch,
            List<FoodDefinitionDependency>? changed = null)
        {
            var refusal = Holders(image, live);
            if (refusal == FoodContractRecheckRefusal.None) refusal = Definitions(image, live, epoch, changed);
            if (refusal == FoodContractRecheckRefusal.None) refusal = BusinessType(image, live);
            return refusal;
        }
        public static FoodContractRecheckRefusal Holders(FoodContractImage image, IFoodContractLive live)
        {
            var holders = image.Holders;
            for (int i = 0; i < holders.Count; i++)
            {
                var slot = holders[i];
                if (!live.ReadHolder(slot, out var now) || !slot.Matches(now)) return FoodContractRecheckRefusal.Holder;
            }
            return FoodContractRecheckRefusal.None;
        }
        public static FoodContractRecheckRefusal Definitions(FoodContractImage image, IFoodContractLive live, FoodDefinitionEpoch epoch,
            List<FoodDefinitionDependency>? changed = null)
        {
            var refusal = FoodContractRecheckRefusal.None;
            var definitions = image.Definitions;
            for (int i = 0; i < definitions.Count; i++)
            {
                var d = definitions[i];
                if (epoch.TryGet(d, out bool same)) { if (same) continue; }
                else
                {
                    bool loaded = live.ReadDefinition(d.Name, out var current, out var facts) && ReferenceEquals(current, d.Live);
                    same = loaded && d.Copy.Matches(facts);
                    epoch.Set(d, same, loaded);
                    if (same) continue;
                    if (loaded) changed?.Add(d);
                }
                var reason = epoch.Loaded(d) ? FoodContractRecheckRefusal.DefinitionContent : FoodContractRecheckRefusal.DefinitionReference;
                if (refusal != FoodContractRecheckRefusal.DefinitionReference) refusal = reason;
                if (changed == null) return refusal;
            }
            return refusal;
        }
        public static int StaleCopies(IReadOnlyList<FoodDefinitionDependency> dependencies, LoadedStockCatalogue catalogue,
            IFoodContractLive live, FoodDefinitionEpoch epoch, List<object> stale)
        {
            int found = 0;
            for (int i = 0; i < dependencies.Count; i++)
            {
                string name = dependencies[i].Name;
                if (!catalogue.TryGet(name, out var copy) || !live.ReadDefinition(name, out var current, out var facts) || current == null) continue;
                var probe = new FoodDefinitionDependency(name, current, copy);
                if (epoch.TryGet(probe, out bool same)) continue;
                same = copy.Matches(facts);
                epoch.Set(probe, same, true);
                if (!same && !stale.Contains(current)) { stale.Add(current); found++; }
            }
            return found;
        }

        public static bool CatalogueCurrent(FoodContractImage image, LoadedStockCatalogue? catalogue)
        {
            if (catalogue == null || !catalogue.Ready) return false;
            if (catalogue.Revision == image.CatalogueRevision) return true;
            var definitions = image.Definitions;
            for (int i = 0; i < definitions.Count; i++)
            {
                var d = definitions[i];
                if (!catalogue.TryGet(d.Name, out var copy)) return false;
                if (!ReferenceEquals(copy, d.Copy) && !copy.SameContent(d.Copy)) return false;
            }
            return true;
        }

        public static bool ReferencesCurrent(FoodContractImage image, IFoodContractLive live)
        {
            var definitions = image.Definitions;
            for (int i = 0; i < definitions.Count; i++)
                if (!live.ReadDefinition(definitions[i].Name, out var current, out _) || !ReferenceEquals(current, definitions[i].Live)) return false;
            return true;
        }

        public static FoodContractRecheckRefusal BusinessType(FoodContractImage image, IFoodContractLive live)
            => image.Business == null || !live.ReadBusinessType(out var type) || !image.Business.Matches(type)
                ? FoodContractRecheckRefusal.BusinessType : FoodContractRecheckRefusal.None;

        public static FoodPriceVerdict Prices(FoodContractImage image, IFoodContractLive live, Money[]? current = null)
        {
            var verdict = FoodPriceVerdict.Same;
            var offers = image.Eligibility.Offers;
            for (int i = 0; i < offers.Count; i++)
            {
                if (live.PriceRows(offers[i].ProductId, out float price) != 1 || !Money.TryFromNative(price, out var money) || money == Money.Zero)
                    return FoodPriceVerdict.Refused;
                if (current != null) current[i] = money;
                if (money != image.Prices[i]) verdict = FoodPriceVerdict.PriceOnly;
            }
            return verdict;
        }
    }

    public enum FoodReconciliationRefusal { None, Workstation, UnvalidatedProducer, NativeCounterNotInImage, OmittedPositiveHolder }

    public readonly struct FoodNativeHolder
    {
        public FoodNativeHolder(object instance, string? product, bool workstation, bool counter)
        { Instance = instance; Product = product; Workstation = workstation; Counter = counter; }
        public object Instance { get; }
        public string? Product { get; }
        public bool Workstation { get; }
        public bool Counter { get; }
    }

    public static class FoodHolderReconciliation
    {
        public static FoodReconciliationRefusal Check(FoodContractImage image, IReadOnlyList<FoodNativeHolder> native,
            IReadOnlyList<string> menu, Func<object, int> liveAmount)
        {
            var holders = image.Holders;
            var marks = image.Marks(out int epoch);
            for (int n = 0; n < native.Count; n++)
            {
                var entry = native[n];
                int i = image.HolderIndex(entry.Instance);
                if (entry.Counter)
                {
                    if (i < 0 || !holders[i].IsBagCounter) return FoodReconciliationRefusal.NativeCounterNotInImage;
                    continue;
                }
                if (entry.Workstation) return FoodReconciliationRefusal.Workstation;
                if (i < 0 || holders[i].IsBagCounter || !string.Equals(holders[i].ResourceId, entry.Product, StringComparison.Ordinal))
                    return FoodReconciliationRefusal.UnvalidatedProducer;
                marks[i] = epoch;
            }
            for (int i = 0; i < holders.Count; i++)
            {
                var h = holders[i];
                if (h.IsBagCounter || h.Stock == null || marks[i] == epoch || !Contains(menu, h.ResourceId) || liveAmount(h.Stock) <= 0) continue;
                return FoodReconciliationRefusal.OmittedPositiveHolder;
            }
            return FoodReconciliationRefusal.None;
        }
        private static bool Contains(IReadOnlyList<string> menu, string name)
        {
            for (int i = 0; i < menu.Count; i++) if (string.Equals(menu[i], name, StringComparison.Ordinal)) return true;
            return false;
        }
    }

    public enum FoodContractReadiness { Ready, NoImage, Failed, Stale, Catalogue, Refused, Budget, Gate }

    public enum FoodRecoveryRefusal { None, NotRefused, SameHour, NoFreshImage, HandoffPending, LiveNativeCustomers }

    public sealed class FoodContractCoordinator
    {
        public const int MaximumShops = LoadedStockLimits.Shops;
        public const int RecoveriesPerFrame = 64;
        public const int CommitsPerBoundary = 16;
        private sealed class Entry
        {
            internal Entry(string key) { Key = key; }
            internal readonly string Key;
            internal FoodContractImage? Image;
            internal bool Invalidated, Preparing, PrepareQueued, RecoveryQueued;
            internal long RefusedHour = -1, LastRecoveryHour = -1;
            internal FoodContractReadiness LastReadiness = FoodContractReadiness.NoImage;
            internal long LastBoundary = -1;
            internal long VerifiedCatalogue = -1;
            internal FoodDefinitionDependency[] Watched = Array.Empty<FoodDefinitionDependency>();
        }
        public const int MaximumWatched = 512;
        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly Queue<Entry> prepare = new Queue<Entry>();
        private readonly Queue<Entry> recovery = new Queue<Entry>();
        private long commitBoundary = -1;
        private int commits;

        public int Count => entries.Count;
        public long Hour { get; private set; } = -1;
        public int PendingPreparations => prepare.Count;
        public int PendingRecoveries => recovery.Count;
        public long Publishes { get; private set; }
        public long Invalidations { get; private set; }
        public long Refusals { get; private set; }
        public long Recoveries { get; private set; }
        public long Deferrals { get; private set; }
        public long Commits { get; private set; }
        public IEnumerable<string> Keys => entries.Keys;

        private Entry? Track(string key)
        {
            if (entries.TryGetValue(key, out var e)) return e;
            if (entries.Count == MaximumShops || string.IsNullOrEmpty(key)) return null;
            e = new Entry(key); entries.Add(key, e); return e;
        }
        public bool Tracks(string key) => entries.ContainsKey(key);

        public FoodContractPhase Phase(string key)
        {
            if (!entries.TryGetValue(key, out var e)) return FoodContractPhase.Clean;
            var image = e.Image;
            if (e.RefusedHour >= 0)
                return image != null && image.Passed && image.BuiltHour > e.RefusedHour ? FoodContractPhase.Recovering : FoodContractPhase.Refused;
            if (image == null) return e.Invalidated || e.Preparing ? FoodContractPhase.Dirty : FoodContractPhase.Clean;
            if (!image.Passed) return FoodContractPhase.Refused;
            return e.Preparing ? FoodContractPhase.Rebuilding : FoodContractPhase.Published;
        }
        public FoodContractImage? Image(string key) => entries.TryGetValue(key, out var e) ? e.Image : null;
        public long RefusedHour(string key) => entries.TryGetValue(key, out var e) ? e.RefusedHour : -1;
        public FoodContractReadiness LastReadiness(string key, out long boundary)
        {
            boundary = -1;
            if (!entries.TryGetValue(key, out var e)) return FoodContractReadiness.NoImage;
            boundary = e.LastBoundary; return e.LastReadiness;
        }

        public FoodContractImage? Serving(string key)
            => entries.TryGetValue(key, out var e) && e.RefusedHour < 0 && !e.Invalidated && e.Image != null && e.Image.Passed ? e.Image : null;

        public bool Admit(string key, bool stampMatches)
        {
            var image = Serving(key);
            if (image == null) return false;
            if (stampMatches) return true;
            Invalidate(key); return false;
        }
        public void Invalidate(string key)
        {
            if (!entries.TryGetValue(key, out var e) || e.Image == null && e.Invalidated) return;
            e.Image = null; e.Invalidated = true; Invalidations++;
        }

        public void BeginHour(long hour) { if (hour > Hour) Hour = hour; }
        public bool Prepare(string key)
        {
            var e = Track(key);
            if (e == null) return false;
            e.Preparing = true;
            if (e.PrepareQueued) return false;
            e.PrepareQueued = true; prepare.Enqueue(e); return true;
        }
        public bool TryNextPrepare(out string key)
        {
            while (prepare.Count > 0)
            {
                var e = prepare.Dequeue(); e.PrepareQueued = false;
                if (entries.TryGetValue(e.Key, out var current) && ReferenceEquals(current, e)) { key = e.Key; return true; }
            }
            key = ""; return false;
        }

        public FoodContractPhase Publish(FoodContractImage image)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            var e = Track(image.Key);
            if (e == null) return FoodContractPhase.Clean;
            e.Image = image; e.Invalidated = false; e.Preparing = false; e.VerifiedCatalogue = image.CatalogueRevision; Publishes++;
            e.Watched = image.Passed ? Copy(image.Definitions) : Union(image.Definitions, e.Watched);
            if (e.RefusedHour >= 0 && image.Passed && image.BuiltHour > e.RefusedHour && !e.RecoveryQueued)
            { e.RecoveryQueued = true; recovery.Enqueue(e); }
            return Phase(image.Key);
        }
        private static FoodDefinitionDependency[] Copy(IReadOnlyList<FoodDefinitionDependency> source)
        {
            var result = new FoodDefinitionDependency[Math.Min(source.Count, MaximumWatched)];
            for (int i = 0; i < result.Length; i++) result[i] = source[i];
            return result;
        }
        private static FoodDefinitionDependency[] Union(IReadOnlyList<FoodDefinitionDependency> next, FoodDefinitionDependency[] previous)
        {
            var result = new List<FoodDefinitionDependency>(Math.Min(next.Count + previous.Length, MaximumWatched));
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < next.Count && result.Count < MaximumWatched; i++) if (names.Add(next[i].Name)) result.Add(next[i]);
            for (int i = 0; i < previous.Length && result.Count < MaximumWatched; i++) if (names.Add(previous[i].Name)) result.Add(previous[i]);
            return result.ToArray();
        }
        public IReadOnlyList<FoodDefinitionDependency> Watched(string key)
            => entries.TryGetValue(key, out var e) ? e.Watched : Array.Empty<FoodDefinitionDependency>();
        public long VerifiedCatalogue(string key) => entries.TryGetValue(key, out var e) && e.Image != null ? e.VerifiedCatalogue : -1;
        public void Rebase(string key, long revision)
        { if (entries.TryGetValue(key, out var e) && e.Image != null && revision > e.VerifiedCatalogue) e.VerifiedCatalogue = revision; }

        public bool Replace(FoodContractImage current, FoodContractImage next)
        {
            if (!entries.TryGetValue(current.Key, out var e) || !ReferenceEquals(e.Image, current) || next.Key != current.Key) return false;
            e.Image = next; return true;
        }
        public void Refuse(string key, long hour)
        {
            var e = Track(key);
            if (e == null) return;
            Refusals++;
            if (e.RefusedHour < 0) e.RefusedHour = hour;
        }

        public FoodRecoveryRefusal RecoveryGate(string key, long hour, bool handoffComplete, bool liveNativeCustomers)
        {
            if (!entries.TryGetValue(key, out var e) || e.RefusedHour < 0) return FoodRecoveryRefusal.NotRefused;
            if (hour <= e.RefusedHour) return FoodRecoveryRefusal.SameHour;
            var image = e.Image;
            if (image == null || !image.Passed || e.Invalidated || image.BuiltHour <= e.RefusedHour || image.BuiltHour < hour - 1)
                return FoodRecoveryRefusal.NoFreshImage;
            if (!handoffComplete) return FoodRecoveryRefusal.HandoffPending;
            if (liveNativeCustomers) return FoodRecoveryRefusal.LiveNativeCustomers;
            return FoodRecoveryRefusal.None;
        }
        public int TakeRecoveries(long hour, List<string> into, int maximum = RecoveriesPerFrame)
        {
            into.Clear();
            maximum = Math.Min(maximum, RecoveriesPerFrame);
            while (into.Count < maximum && recovery.Count > 0)
            {
                var e = recovery.Dequeue(); e.RecoveryQueued = false;
                if (!entries.TryGetValue(e.Key, out var current) || !ReferenceEquals(current, e) || e.RefusedHour < 0 || e.LastRecoveryHour == hour) continue;
                e.LastRecoveryHour = hour; into.Add(e.Key);
            }
            return into.Count;
        }
        public void Requeue(string key, long hour)
        {
            if (!entries.TryGetValue(key, out var e) || e.RecoveryQueued || e.RefusedHour < 0) return;
            if (e.LastRecoveryHour == hour) e.LastRecoveryHour = -1;
            e.RecoveryQueued = true; recovery.Enqueue(e);
        }
        public bool RequeueRecovery(string key)
        {
            if (!entries.TryGetValue(key, out var e) || e.RefusedHour < 0 || e.RecoveryQueued || e.Image == null || !e.Image.Passed
                || e.Image.BuiltHour <= e.RefusedHour) return false;
            e.RecoveryQueued = true; recovery.Enqueue(e); return true;
        }
        public void Recovered(string key)
        {
            if (!entries.TryGetValue(key, out var e) || e.RefusedHour < 0) return;
            e.RefusedHour = -1; Recoveries++;
        }

        public FoodContractReadiness Ready(string key, long boundaryHour, long requestedHour, long catalogueRevision)
        {
            if (!entries.TryGetValue(key, out var e)) return FoodContractReadiness.NoImage;
            return e.RefusedHour >= 0 ? FoodContractReadiness.Refused
                : e.Image == null || e.Invalidated ? FoodContractReadiness.NoImage
                : !e.Image.Passed ? FoodContractReadiness.Failed
                : e.Image.BuiltHour < boundaryHour - 1 || e.Image.BuiltHour < requestedHour ? FoodContractReadiness.Stale
                : e.VerifiedCatalogue != catalogueRevision ? FoodContractReadiness.Catalogue
                : FoodContractReadiness.Ready;
        }
        public bool TakeCommit(long boundaryHour)
        {
            if (commitBoundary != boundaryHour) { commitBoundary = boundaryHour; commits = 0; }
            if (commits == CommitsPerBoundary) return false;
            commits++; Commits++; return true;
        }
        internal void Record(string key, long boundary, FoodContractReadiness readiness)
        {
            if (!entries.TryGetValue(key, out var e)) return;
            e.LastBoundary = boundary; e.LastReadiness = readiness;
            if (readiness != FoodContractReadiness.Ready) Deferrals++;
        }

        public bool Remove(string key) => entries.Remove(key);
        public void Clear() { entries.Clear(); prepare.Clear(); recovery.Clear(); commitBoundary = -1; commits = 0; Hour = -1; }
    }

    public static class FoodContractBoundary
    {
        public enum Outcome { None, Acquired, HandedBack, Deferred, Refused }

        public static Outcome Commit(FoodAuthority row, FoodContractCoordinator? coordinator, long hour, bool liveNativeCustomers,
            bool marketReady, long catalogueRevision, Func<FoodAuthority, FoodContractImage, bool> gate)
        {
            var step = row.Pending(hour, true, liveNativeCustomers, marketReady);
            if (step == FoodBoundaryStep.None) return Outcome.None;
            bool ready = true, refused = false;
            if (step == FoodBoundaryStep.Acquire && coordinator != null)
            {
                var readiness = coordinator.Ready(row.PlaceId, hour, row.RequestedHour, catalogueRevision);
                if (readiness == FoodContractReadiness.Ready && !coordinator.TakeCommit(hour)) readiness = FoodContractReadiness.Budget;
                if (readiness == FoodContractReadiness.Ready && !gate(row, coordinator.Image(row.PlaceId)!))
                { readiness = FoodContractReadiness.Gate; refused = true; }
                coordinator.Record(row.PlaceId, hour, readiness);
                ready = readiness == FoodContractReadiness.Ready;
            }
            if (row.Boundary(hour, true, liveNativeCustomers, marketReady, ready))
                return step == FoodBoundaryStep.Acquire ? Outcome.Acquired : Outcome.HandedBack;
            return refused ? Outcome.Refused : row.Deferred ? Outcome.Deferred : Outcome.None;
        }
        public static int Order(FoodAuthority a, FoodAuthority b)
        {
            int c = a.RequestedHour.CompareTo(b.RequestedHour);
            return c != 0 ? c : string.CompareOrdinal(a.PlaceId, b.PlaceId);
        }
    }
}
