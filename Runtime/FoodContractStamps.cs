using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    /// <summary>E5b2 Decision 2. Reads a collection's private <c>int</c> version field through a
    /// <see cref="DynamicMethod"/>: constant time, no boxing, no reflection per read. Game-free so tests
    /// link it against doubles. Binding fails closed when the field is missing, renamed, static or not int.</summary>
    public sealed class CollectionVersionReader<TCollection> where TCollection : class
    {
        public const string VersionField = "_version";
        private readonly Func<TCollection, int> read;
        private CollectionVersionReader(Func<TCollection, int> read, FieldInfo field) { this.read = read; Field = field; }
        public FieldInfo Field { get; }
        public int Read(TCollection collection) => read(collection);

        public static CollectionVersionReader<TCollection>? Bind(out string failure) => Bind(VersionField, out failure);
        public static CollectionVersionReader<TCollection>? Bind(string fieldName, out string failure)
        {
            const BindingFlags declared = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            FieldInfo? field = null;
            for (Type? t = typeof(TCollection); t != null && field == null; t = t.BaseType) field = t.GetField(fieldName, declared);
            string name = typeof(TCollection).Name + "." + fieldName;
            if (field == null) { failure = name + " is missing"; return null; }
            if (field.FieldType != typeof(int)) { failure = name + " is " + field.FieldType.Name + ", not Int32"; return null; }
            try
            {
                var method = new DynamicMethod("PersistentCityReadVersion", typeof(int), new[] { typeof(TCollection) },
                    typeof(CollectionVersionReader<TCollection>).Module, true);
                var il = method.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, field); il.Emit(OpCodes.Ret);
                var reader = (Func<TCollection, int>)method.CreateDelegate(typeof(Func<TCollection, int>));
                failure = ""; return new CollectionVersionReader<TCollection>(reader, field);
            }
            catch (Exception e) { failure = name + " code generation failed: " + e.GetType().Name; return null; }
        }
    }

    /// <summary>Startup proof that a bound version reader moves on add, indexer set and remove.
    /// A null result is a pass; any other value names the failed step and keeps the capability off.</summary>
    public static class FoodContractSelfTest
    {
        public static string? Check<T>(CollectionVersionReader<T>? reader, Func<T> create, Action<T> add, Action<T> set, Action<T> remove)
            where T : class
        {
            if (reader == null) return typeof(T).Name + " version reader is not bound";
            try
            {
                var collection = create();
                int before = reader.Read(collection);
                if (reader.Read(collection) != before) return typeof(T).Name + " version read is unstable";
                add(collection); int added = reader.Read(collection);
                if (added == before) return typeof(T).Name + " version did not move on add";
                set(collection); int replaced = reader.Read(collection);
                if (replaced == added) return typeof(T).Name + " version did not move on indexer set";
                remove(collection);
                if (reader.Read(collection) == replaced) return typeof(T).Name + " version did not move on remove";
                return null;
            }
            catch (Exception e) { return typeof(T).Name + " self-test threw " + e.GetType().Name; }
        }
        public static string? List<TElement>(CollectionVersionReader<List<TElement>>? reader, TElement first, TElement second)
            => Check(reader, () => new List<TElement>(), l => l.Add(first), l => l[0] = second, l => l.RemoveAt(0));
        public static string? Dictionary<TValue>(CollectionVersionReader<Dictionary<string, TValue>>? reader, TValue first, TValue second)
        {
            const string key = "persistentcity:self-test";
            return Check(reader, () => new Dictionary<string, TValue>(StringComparer.Ordinal),
                d => d.Add(key, first), d => d[key] = second, d => d.Remove(key));
        }
    }

    /// <summary>Reference, version and count of one native collection. A null collection is the default stamp.</summary>
    public readonly struct CollectionStamp
    {
        public CollectionStamp(object? reference, int version, int count) { Reference = reference; Version = version; Count = count; }
        public object? Reference { get; }
        public int Version { get; }
        public int Count { get; }
        public static CollectionStamp Of<T>(CollectionVersionReader<T> reader, T? collection, int count) where T : class
            => collection == null ? default : new CollectionStamp(collection, reader.Read(collection), count);
        public bool Matches(in CollectionStamp other)
            => ReferenceEquals(Reference, other.Reference) && Version == other.Version && Count == other.Count;
        public override string ToString() => Reference == null ? "null" : "v" + Version + "/n" + Count;
    }

    /// <summary>E5b2 Decision 2 structural stamp of one registration: menu, price and furniture collections
    /// by reference, version and count; business type by reference; four scalar flags. In-place writes to an
    /// element's fields (a price, a stock name, an amount) do not move it; that limit is known and E5b3's
    /// recheck owns it. Shadow evidence only: a mismatch dirties the image, never ownership.</summary>
    public readonly struct ShopContractStamp
    {
        [Flags] private enum Flags : byte { Valid = 1, Rented = 2, Established = 4, ForRent = 8, Closed = 16 }
        private readonly Flags flags;
        public ShopContractStamp(in CollectionStamp menu, in CollectionStamp prices, in CollectionStamp instances, string? businessType,
            bool rented, bool established, bool forRent, bool closed)
        {
            Menu = menu; Prices = prices; Instances = instances; BusinessType = businessType;
            flags = Flags.Valid | (rented ? Flags.Rented : 0) | (established ? Flags.Established : 0)
                | (forRent ? Flags.ForRent : 0) | (closed ? Flags.Closed : 0);
        }
        public CollectionStamp Menu { get; }
        public CollectionStamp Prices { get; }
        public CollectionStamp Instances { get; }
        public string? BusinessType { get; }
        public bool Valid => (flags & Flags.Valid) != 0;
        public bool Rented => (flags & Flags.Rented) != 0;
        public bool Established => (flags & Flags.Established) != 0;
        public bool ForRent => (flags & Flags.ForRent) != 0;
        public bool Closed => (flags & Flags.Closed) != 0;
        /// <summary>An unrecorded (default) stamp never matches, so a missing stamp cannot read as clean.</summary>
        public bool Matches(in ShopContractStamp other)
            => Valid && other.Valid && flags == other.flags && ReferenceEquals(BusinessType, other.BusinessType)
                && Menu.Matches(other.Menu) && Prices.Matches(other.Prices) && Instances.Matches(other.Instances);
        public override string ToString() => !Valid ? "unrecorded" : "menu " + Menu + ", prices " + Prices + ", furniture " + Instances
            + ", type " + (BusinessType ?? "null") + ", rented " + Rented + ", established " + Established + ", forRent " + ForRent + ", closed " + Closed;
    }

    /// <summary>Decision 12 report: one entry per runtime capability, in check order. Empty means unproven.</summary>
    public sealed class FoodContractCapabilities
    {
        private readonly List<KeyValuePair<string, string?>> entries = new List<KeyValuePair<string, string?>>();
        public IReadOnlyList<KeyValuePair<string, string?>> Entries => entries;
        public bool Record(string capability, string? failure)
        { entries.Add(new KeyValuePair<string, string?>(capability, string.IsNullOrEmpty(failure) ? null : failure)); return string.IsNullOrEmpty(failure); }
        public bool Passed(string capability)
        {
            bool seen = false;
            foreach (var entry in entries) if (entry.Key == capability) { if (entry.Value != null) return false; seen = true; }
            return seen;
        }
        public bool AllPassed
        {
            get
            {
                if (entries.Count == 0) return false;
                foreach (var entry in entries) if (entry.Value != null) return false;
                return true;
            }
        }
        public string? FirstFailure
        {
            get
            {
                foreach (var entry in entries) if (entry.Value != null) return entry.Key + ": " + entry.Value;
                return entries.Count == 0 ? "no capability checked" : null;
            }
        }
    }

    /// <summary>The exact canonical names of one sealed E5a catalogue, with its revision. Immutable; published
    /// by one reference swap, so a reader sees a whole image or none. A refused catalogue admits no name.
    /// E5b4 consumes it; in E5b2 it grants nothing.</summary>
    public sealed class LoadedNameImage
    {
        private readonly HashSet<string> set;
        private readonly string[] names;
        internal LoadedNameImage(long revision, IReadOnlyList<string> source, FoodEligibilityRefusal refusal)
        {
            Revision = revision; Refusal = refusal;
            names = new string[refusal == FoodEligibilityRefusal.None ? source.Count : 0];
            for (int i = 0; i < names.Length; i++) names[i] = source[i];
            set = new HashSet<string>(names, StringComparer.Ordinal);
            Names = Array.AsReadOnly(names);
        }
        public long Revision { get; }
        public FoodEligibilityRefusal Refusal { get; }
        public int Count => names.Length;
        public IReadOnlyList<string> Names { get; }
        public bool Contains(string? name) => name != null && set.Contains(name);
    }

    /// <summary>Builds one catalogue and its name image together. Names are recorded only for definitions the
    /// catalogue accepted, so the image equals the catalogue's names.</summary>
    public sealed class LoadedNameImageBuilder
    {
        private readonly List<string> names = new List<string>();
        public LoadedNameImageBuilder(long revision) { Catalogue = new LoadedStockCatalogue(revision); }
        public LoadedStockCatalogue Catalogue { get; }
        public bool Add(LoadedStockDefinition definition)
        {
            if (!Catalogue.Add(definition)) return false;
            names.Add(definition.Name); return true;
        }
        public LoadedNameImage Publish(FoodEligibilityRefusal refusal = FoodEligibilityRefusal.None)
        {
            Catalogue.Seal(refusal);
            return new LoadedNameImage(Catalogue.Revision, names, Catalogue.Ready ? FoodEligibilityRefusal.None : Catalogue.Refusal);
        }
    }

    /// <summary>Catalogue revision state of the E5a reader. W11 hints bump the revision (coalesced until a rebuild
    /// starts; a hint during a rebuild discards it). The catalogue and its name image are published together;
    /// <see cref="Names"/> is null whenever the published image is not the current revision.</summary>
    public sealed class LoadedCatalogueRevision
    {
        private LoadedNameImage? published;
        public long Revision { get; private set; } = 1;
        public bool Dirty { get; private set; } = true;
        public LoadedNameImageBuilder? Building { get; private set; }
        public LoadedStockCatalogue? Catalogue { get; private set; }
        public LoadedNameImage? Names => Dirty || published == null || published.Revision != Revision ? null : published;
        /// <summary>The last image published, current or not (diagnostic).</summary>
        public LoadedNameImage? LastPublished => published;
        public long Bumps { get; private set; }
        public bool MarkDirty()
        {
            if (Dirty && Building == null) return false;
            Revision++; Bumps++; Dirty = true; Catalogue = null; Building = null;
            return true;
        }
        public LoadedNameImageBuilder Begin()
        {
            if (!Dirty) throw new InvalidOperationException("Catalogue is already published.");
            return Building ??= new LoadedNameImageBuilder(Revision);
        }
        public void Publish(FoodEligibilityRefusal refusal = FoodEligibilityRefusal.None)
        {
            var building = Building ?? throw new InvalidOperationException("No catalogue is being built.");
            var image = building.Publish(refusal);
            Catalogue = building.Catalogue; published = image; Building = null; Dirty = false;
        }
        public void Reset() { Revision++; Bumps++; Dirty = true; Catalogue = null; Building = null; published = null; }
    }

    public interface IContractDirtyUnit { bool Queued { get; set; } }

    /// <summary>Coalescing dirty queue: a unit is queued at most once however many hints arrive before it is taken.</summary>
    public sealed class ContractDirtyQueue<T> where T : class, IContractDirtyUnit
    {
        private readonly Queue<T> queue = new Queue<T>();
        public int Count => queue.Count;
        public long Marks { get; private set; }
        public long Coalesced { get; private set; }
        public bool Enqueue(T unit)
        {
            Marks++;
            if (unit.Queued) { Coalesced++; return false; }
            unit.Queued = true; queue.Enqueue(unit); return true;
        }
        public bool TryDequeue(out T unit)
        {
            if (queue.Count == 0) { unit = null!; return false; }
            unit = queue.Dequeue(); unit.Queued = false; return true;
        }
        public void Clear() { foreach (var unit in queue) unit.Queued = false; queue.Clear(); }
    }
}
