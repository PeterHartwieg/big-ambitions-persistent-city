using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    // E5b3: the pure half of the contract coordinator (E5b remainder plan, Decisions 3, 3a, 4-8, 13).
    // Runtime reads native objects into these detached shapes; everything that decides lives here so it is
    // testable against doubles. Nothing here is saved: images, queues and refusals are rebuilt at load.

    /// <summary>Per-shop state of the coordinator. Derived from the held image, the pending flags and the
    /// recorded refusal, never stored separately.</summary>
    public enum FoodContractPhase
    {
        /// <summary>Tracked, no image yet and nothing pending.</summary>
        Clean,
        /// <summary>A stamp mismatch dropped the image; a rebuild is pending and nothing serves.</summary>
        Dirty,
        /// <summary>A preparation rebuild is pending while the last passing image keeps serving.</summary>
        Rebuilding,
        /// <summary>A passing image serves.</summary>
        Published,
        /// <summary>The image failed, or a contract refusal is recorded with no later passing image.</summary>
        Refused,
        /// <summary>A recorded refusal with a passing image built in a later hour, waiting for the recovery gates.</summary>
        Recovering
    }

    /// <summary>Decision 3: one contract holder as the image saw it. References are native objects held only
    /// while the image is published; the image is dropped at unload with the coordinator.</summary>
    public readonly struct FoodHolderSlot
    {
        public FoodHolderSlot(string holderId, string definitionId, string resourceId, bool bag, object? instance,
            object? cargo, int cargoVersion, int cargoCount, object? stock, string? stockName, int nested)
        {
            HolderId = holderId; DefinitionId = definitionId; ResourceId = resourceId; IsBagCounter = bag; Instance = instance;
            Cargo = cargo; CargoVersion = cargoVersion; CargoCount = cargoCount; Stock = stock; StockName = stockName; Nested = nested;
        }
        public string HolderId { get; }
        /// <summary>The holder's furniture definition name (live: the instance's current name).</summary>
        public string DefinitionId { get; }
        /// <summary>The product (shelf) or bag (counter) the image validated in the single stock slot.</summary>
        public string ResourceId { get; }
        public bool IsBagCounter { get; }
        public object? Instance { get; }
        public object? Cargo { get; }
        public int CargoVersion { get; }
        public int CargoCount { get; }
        public object? Stock { get; }
        public string? StockName { get; }
        /// <summary>Nested cargo count; -1 when the nested list is null.</summary>
        public int Nested { get; }
        /// <summary>The shape the image requires: one stock slot naming the validated resource, empty nested list.</summary>
        public bool Shaped => Instance != null && Cargo != null && CargoCount == 1 && Stock != null && Nested == 0
            && string.Equals(StockName, ResourceId, StringComparison.Ordinal);
        /// <summary>Same instance under its key, same definition name, same cargo list by reference, version and
        /// count, same stock slot by reference and ordinal name, same nested count. Quantity is not compared (Decision 6).</summary>
        public bool Matches(in FoodHolderSlot live)
            => Instance != null && ReferenceEquals(Instance, live.Instance) && string.Equals(DefinitionId, live.DefinitionId, StringComparison.Ordinal)
                && Cargo != null && ReferenceEquals(Cargo, live.Cargo) && CargoVersion == live.CargoVersion && CargoCount == live.CargoCount
                && Stock != null && ReferenceEquals(Stock, live.Stock) && string.Equals(StockName, live.StockName, StringComparison.Ordinal)
                && Nested == live.Nested;
    }

    /// <summary>Decision 3a: one loaded definition an image depends on, by live reference and catalogue copy.
    /// The copy object is shared by every image built from the same catalogue revision.</summary>
    public sealed class FoodDefinitionDependency
    {
        public FoodDefinitionDependency(string name, object live, LoadedStockDefinition copy)
        { Name = name; Live = live; Copy = copy; }
        public string Name { get; }
        public object Live { get; }
        public LoadedStockDefinition Copy { get; }
    }

    /// <summary>Live business-type fields the contract reads: simulator type, entrance fee and authored tags (uncopied).</summary>
    public readonly struct FoodBusinessTypeFacts
    {
        public FoodBusinessTypeFacts(object? live, Type? simulator, bool entranceFee, IReadOnlyList<string>? tags)
        { Live = live; Simulator = simulator; EntranceFee = entranceFee; Tags = tags; }
        public object? Live { get; }
        public Type? Simulator { get; }
        public bool EntranceFee { get; }
        public IReadOnlyList<string>? Tags { get; }
    }

    /// <summary>Decision 3a copy of the business type, compared by content (tags element by element, at most 128).</summary>
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
        /// <summary>Null when the live list was null or over 128 rows; such a type never matches.</summary>
        public IReadOnlyList<string>? Tags { get; }
        public bool Matches(in FoodBusinessTypeFacts live)
            => Live != null && ReferenceEquals(Live, live.Live) && Simulator == live.Simulator && EntranceFee == live.EntranceFee
                && Tags != null && LoadedStockRows.Same(Tags, live.Tags);
    }

    /// <summary>One atomic published contract image of a shop: the E5a eligibility image plus every native reference
    /// and definition copy the rechecks compare. Immutable; a price-only change (Q2) publishes a new object.</summary>
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
        /// <summary>The accounting hour (day * 24 + hour) the rebuild finished in.</summary>
        public long BuiltHour { get; }
        public long CatalogueRevision => Eligibility.CatalogueRevision;
        public FoodEligibilityImage Eligibility { get; }
        public IReadOnlyList<FoodHolderSlot> Holders => holders;
        public IReadOnlyList<FoodDefinitionDependency> Definitions => definitions;
        public FoodBusinessTypeCopy? Business { get; }
        /// <summary>Decision 8 admission (<see cref="CompleteFoodMenu.Check"/>) at publish.</summary>
        public CompleteFoodMenuRefusal Admission { get; }
        /// <summary>Runtime's structural stamp taken when the pass started (opaque here).</summary>
        public object? Stamp { get; }
        /// <summary>Current approved price per offer, in <see cref="FoodEligibilityImage.Offers"/> order.</summary>
        public IReadOnlyList<Money> Prices => prices;
        public bool Passed => Eligibility.StockContractMatched && Admission == CompleteFoodMenuRefusal.None && holders.Length > 0;
        /// <summary>Q2: the same image with new valid prices. Never mutates this one.</summary>
        public FoodContractImage WithPrices(Money[] next)
        {
            if (next == null || next.Length != prices.Length) throw new ArgumentException("Price row count differs.");
            return new FoodContractImage(this, (Money[])next.Clone());
        }
        public bool HasHolder(object instance) => HolderIndex(instance) >= 0;

        // Decision 5 lookup: holder index by instance reference, built once per image on first use. Main thread.
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

    /// <summary>Runtime reads the native world through this; tests implement it with doubles. Each call reads one
    /// live value and allocates nothing.</summary>
    public interface IFoodContractLive
    {
        /// <summary>The live slot under <paramref name="image"/>'s holder key; false when the key is missing.</summary>
        bool ReadHolder(in FoodHolderSlot image, out FoodHolderSlot live);
        /// <summary>The definition currently loaded under <paramref name="name"/> and its live facts; false when none is.</summary>
        bool ReadDefinition(string name, out object? live, out LoadedStockFacts facts);
        bool ReadBusinessType(out FoodBusinessTypeFacts live);
        /// <summary>How many price rows name <paramref name="product"/>, and the last one's native price.</summary>
        int PriceRows(string product, out float price);
    }

    public enum FoodContractRecheckRefusal { None, Holder, DefinitionReference, DefinitionContent, BusinessType, Price }

    public enum FoodPriceVerdict { Same, PriceOnly, Refused }

    /// <summary>Decision 3a sharing: each distinct catalogue copy is compared once per epoch (a capture batch, a
    /// checkout or a boundary) and the verdict is shared by every shop in it.</summary>
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

    /// <summary>Decision 3 and the Decision 4 commit gate, against one image. Bounded by the image: at most 8
    /// offers, its holders (each collection at most 128) and its distinct definitions.</summary>
    public static class FoodContractRecheck
    {
        /// <summary>Holders, then definitions, then business type. <paramref name="changed"/> receives definitions
        /// whose live content differs from the copy (re-copy candidates).</summary>
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
        /// <summary>Decision 3a: every dependency by live reference and by content. Each distinct copy is compared
        /// once per epoch. A missing or replaced definition is a reference refusal and is not a re-copy candidate.</summary>
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
        /// <summary>Decision 3a and Decision 7 at preparation: each dependency's live content against the catalogue's
        /// <em>current</em> copy. Live objects whose copy is stale go to <paramref name="stale"/> (re-copy). This also
        /// runs for refused shops, whose failed image carries the dependencies forward, so an in-place definition
        /// repair after a hand-back reaches the next image.</summary>
        public static int StaleCopies(IReadOnlyList<FoodDefinitionDependency> dependencies, LoadedStockCatalogue catalogue,
            IFoodContractLive live, FoodDefinitionEpoch epoch, List<object> stale)
        {
            int found = 0;
            for (int i = 0; i < dependencies.Count; i++)
            {
                string name = dependencies[i].Name;
                if (!catalogue.TryGet(name, out var copy) || !live.ReadDefinition(name, out var current, out var facts) || current == null) continue;
                var probe = new FoodDefinitionDependency(name, current, copy);
                if (epoch.TryGet(probe, out bool same)) continue; // first sight already recorded it
                same = copy.Matches(facts);
                epoch.Set(probe, same, true);
                if (!same && !stale.Contains(current)) { stale.Add(current); found++; }
            }
            return found;
        }

        /// <summary>Whether an image built under an older catalogue revision still describes <paramref name="catalogue"/>:
        /// every dependency's current copy is the image's copy, or has the same content. False when the catalogue is
        /// not ready. A re-copy of an unrelated definition therefore leaves the image current.</summary>
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

        /// <summary>Every dependency still resolves to the loaded object the image recorded. A field-identical
        /// replacement object is a different definition (Decision 3a reference check), whatever its content.</summary>
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

        /// <summary>One row per menu product with an exact positive price that <see cref="Money.TryFromNative"/>
        /// accepts (the <see cref="LoadedStockMenuBuilder"/> rule). A valid price that differs from the image is a
        /// price-only change (Q2): written to <paramref name="current"/>, never a refusal.</summary>
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

    /// <summary>One entry native setup produced: a producer indexed for a menu product, or a point of sale.</summary>
    public readonly struct FoodNativeHolder
    {
        public FoodNativeHolder(object instance, string? product, bool workstation, bool counter)
        { Instance = instance; Product = product; Workstation = workstation; Counter = counter; }
        public object Instance { get; }
        public string? Product { get; }
        public bool Workstation { get; }
        public bool Counter { get; }
    }

    /// <summary>Decision 5: native effective holders against the image's validated holders, after the setup the
    /// supply read already made. Any mismatch withdraws the whole business.</summary>
    public static class FoodHolderReconciliation
    {
        /// <param name="liveAmount">Current amount of an image holder's stock slot object.</param>
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
            // A validated holder with positive stock that native setup left out is unknown, never zero.
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

    /// <summary>E5b3 coordinator: per-shop images, the Decision 4 preparation queue, readiness and commit budget,
    /// and the Decision 7 recovery queue. Main thread. Bounded at 1,024 shops; every queue is coalesced.</summary>
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
            // The catalogue revision the held image was last shown to describe (Rebase), and every definition any
            // image of this lifetime depended on. Both survive invalidation and failed publication.
            internal long VerifiedCatalogue = -1;
            internal FoodDefinitionDependency[] Watched = Array.Empty<FoodDefinitionDependency>();
        }
        /// <summary>Most watched dependencies one shop keeps for preparation re-copy.</summary>
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
        /// <summary>The held image, passing or not (diagnostic and recovery).</summary>
        public FoodContractImage? Image(string key) => entries.TryGetValue(key, out var e) ? e.Image : null;
        public long RefusedHour(string key) => entries.TryGetValue(key, out var e) ? e.RefusedHour : -1;
        public FoodContractReadiness LastReadiness(string key, out long boundary)
        {
            boundary = -1;
            if (!entries.TryGetValue(key, out var e)) return FoodContractReadiness.NoImage;
            boundary = e.LastBoundary; return e.LastReadiness;
        }

        /// <summary>The image sales and captures may use: passing, not invalidated, no refusal recorded.</summary>
        public FoodContractImage? Serving(string key)
            => entries.TryGetValue(key, out var e) && e.RefusedHour < 0 && !e.Invalidated && e.Image != null && e.Image.Passed ? e.Image : null;

        /// <summary>Decision 2 at a visit or a <c>SupportedMenu</c> read: a stamp mismatch drops the image at once.</summary>
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

        /// <summary>Decision 4 step 1, once per advancing accounting hour.</summary>
        public void BeginHour(long hour) { if (hour > Hour) Hour = hour; }
        /// <summary>Queues one coalesced preparation rebuild. The held image keeps serving until it is replaced.</summary>
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

        /// <summary>Decision 4 step 3: one atomic swap. Returns the phase it leaves. A passing image built after a
        /// recorded refusal queues one recovery unit.</summary>
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
        /// <summary>Every definition this shop's images depended on, kept across invalidation and failed images (Decision 3a).</summary>
        public IReadOnlyList<FoodDefinitionDependency> Watched(string key)
            => entries.TryGetValue(key, out var e) ? e.Watched : Array.Empty<FoodDefinitionDependency>();
        public long VerifiedCatalogue(string key) => entries.TryGetValue(key, out var e) && e.Image != null ? e.VerifiedCatalogue : -1;
        /// <summary>Records that the held image still describes catalogue <paramref name="revision"/>
        /// (<see cref="FoodContractRecheck.CatalogueCurrent"/> passed).</summary>
        public void Rebase(string key, long revision)
        { if (entries.TryGetValue(key, out var e) && e.Image != null && revision > e.VerifiedCatalogue) e.VerifiedCatalogue = revision; }

        /// <summary>Q2 price record: swaps only if <paramref name="current"/> is still the held image.</summary>
        public bool Replace(FoodContractImage current, FoodContractImage next)
        {
            if (!entries.TryGetValue(current.Key, out var e) || !ReferenceEquals(e.Image, current) || next.Key != current.Key) return false;
            e.Image = next; return true;
        }
        /// <summary>A recorded contract refusal. The first refused hour is kept until recovery succeeds.</summary>
        public void Refuse(string key, long hour)
        {
            var e = Track(key);
            if (e == null) return;
            Refusals++;
            if (e.RefusedHour < 0) e.RefusedHour = hour;
        }

        /// <summary>Decision 7 gates that need no native read. <paramref name="handoffComplete"/> is false while
        /// PC still manages the shop or owns the hour.</summary>
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
        /// <summary>At most <paramref name="maximum"/> (64) recovery units for this frame, each shop at most once
        /// per accounting hour. A unit taken in an hour it cannot pass is dropped; the next passing image requeues it.</summary>
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
        /// <summary>Returns a unit taken this frame but not run (frame budget) to the queue, still due this hour.</summary>
        public void Requeue(string key, long hour)
        {
            if (!entries.TryGetValue(key, out var e) || e.RecoveryQueued || e.RefusedHour < 0) return;
            if (e.LastRecoveryHour == hour) e.LastRecoveryHour = -1;
            e.RecoveryQueued = true; recovery.Enqueue(e);
        }
        /// <summary>Requeues a refused shop whose held passing image waits for a later hour (start of each hour).</summary>
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

        /// <summary>Decision 4 step 4, after the drain: the held image must pass, match the current catalogue
        /// revision and have been built in the hour before the boundary or later, and not before the request.</summary>
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
        /// <summary>At most 16 commit gates per boundary; the rest are deferred to the next one.</summary>
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

    /// <summary>Decision 4 boundary protocol for one drained row. Acquisitions are ordered by
    /// <see cref="Order"/> so the commit budget never depends on controller or dictionary order.</summary>
    public static class FoodContractBoundary
    {
        public enum Outcome { None, Acquired, HandedBack, Deferred, Refused }

        /// <param name="gate">Decision 4's final synchronous checks against the image; it records any refusal itself.</param>
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
        /// <summary>Oldest request first, then place ID (ordinal).</summary>
        public static int Order(FoodAuthority a, FoodAuthority b)
        {
            int c = a.RequestedHour.CompareTo(b.RequestedHour);
            return c != 0 ? c : string.CompareOrdinal(a.PlaceId, b.PlaceId);
        }
    }
}
