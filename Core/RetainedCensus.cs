using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace PersistentCity.Core
{
    public sealed class RetainedCensus
    {
        public sealed class Row
        {
            internal Row(string name) { Name = name; }
            public string Name { get; }
            public long Objects { get; internal set; }
            public long Bytes { get; internal set; }
            public long ScannedBytes { get; internal set; }
            public bool Cache { get; internal set; }
            public int Bound { get; internal set; }
            internal Dictionary<Type, long[]>? Tracked;
            public long TrackedObjects(Type type) => Tracked != null && Tracked.TryGetValue(type, out var t) ? t[0] : 0;
            public long TrackedBytes(Type type) => Tracked != null && Tracked.TryGetValue(type, out var t) ? t[1] : 0;
        }

        public const string PolicyId = "retained-census-v1";
        public const int Header = 16, ArrayHeader = 32, Pointer = 8, TrackedRootsPerType = 8;
        private readonly Dictionary<Type, Layout> layouts = new Dictionary<Type, Layout>();
        private readonly Dictionary<object, bool> seen = new Dictionary<object, bool>(Identity.Instance);
        private readonly Dictionary<Type, Row> byType = new Dictionary<Type, Row>();
        private readonly Dictionary<Type, Row> foreign = new Dictionary<Type, Row>();
        private readonly List<Row> byRoot = new List<Row>();
        private readonly Func<Type, bool> walkable;
        private readonly Dictionary<Type, bool> walkableTypes = new Dictionary<Type, bool>();
        private readonly Stack<object> stack = new Stack<object>();
        private readonly HashSet<Type> perRoot = new HashSet<Type>();
        private readonly List<(string Name, object Root, int Bound)> caches = new List<(string, object, int)>();

        public RetainedCensus(Func<Type, bool>? walkable = null) { this.walkable = walkable ?? (_ => true); }

        private sealed class Layout
        {
            public int Bytes;
            public int References;
            public FieldInfo[] Fields = Array.Empty<FieldInfo>();
        }

        private sealed class Identity : IEqualityComparer<object>
        {
            public static readonly Identity Instance = new Identity();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }

        public IReadOnlyList<Row> Roots => byRoot;
        public IEnumerable<Row> Types => byType.Values.OrderByDescending(r => r.Bytes).ThenBy(r => r.Name, StringComparer.Ordinal);
        public IEnumerable<Row> Foreign => foreign.Values.OrderByDescending(r => r.Objects).ThenBy(r => r.Name, StringComparer.Ordinal);
        public long Objects => byRoot.Sum(r => r.Objects);
        public long Bytes => byRoot.Sum(r => r.Bytes);
        public long ScannedBytes => byRoot.Sum(r => r.ScannedBytes);
        public long ForeignObjects => foreign.Values.Sum(r => r.Objects);
        public int Failures { get; private set; }

        public void Exclude(object? value) { if (value != null) seen[value] = true; }

        public void TrackPerRoot(IEnumerable<Type> types) { foreach (var type in types) perRoot.Add(type); }

        public void DeferCache(string name, object? root, int bound)
        {
            if (root == null || seen.ContainsKey(root)) return;
            seen[root] = true;
            caches.Add((name, root, bound));
        }

        public void AddDeferredCaches()
        {
            foreach (var cache in caches)
            {
                seen.Remove(cache.Root);
                var row = Add(cache.Name, cache.Root);
                row.Cache = true; row.Bound = cache.Bound;
            }
            caches.Clear();
        }

        public IEnumerable<Row> CacheRoots => byRoot.Where(r => r.Cache);
        public long CachedObjectsOf(Type type) => byRoot.Where(r => r.Cache).Sum(r => r.TrackedObjects(type));
        public long CachedBytesOf(Type type) => byRoot.Where(r => r.Cache).Sum(r => r.TrackedBytes(type));

        public Row Add(string name, object? root)
        {
            var row = new Row(name);
            byRoot.Add(row);
            if (root == null || seen.ContainsKey(root)) return row;
            if (!Visit(root)) return row;
            while (stack.Count > 0)
            {
                var value = stack.Pop();
                try { Measure(value, row); }
                catch (Exception) { Failures++; }
            }
            return row;
        }

        public void AddStatics(IEnumerable<Assembly> assemblies, string prefix)
        {
            foreach (var assembly in assemblies)
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException partial) { types = partial.Types.Where(t => t != null).ToArray()!; Failures++; }
                foreach (var type in types.OrderBy(t => t.FullName, StringComparer.Ordinal))
                {
                    if (type.ContainsGenericParameters || type.Name.IndexOf('<') >= 0) continue;
                    foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (field.IsLiteral || field.FieldType.IsPrimitive || field.FieldType.IsEnum) continue;
                        object? value;
                        try { value = field.GetValue(null); }
                        catch (Exception) { Failures++; continue; }
                        if (value != null) Add(prefix + type.Name + "." + field.Name, value);
                    }
                }
            }
        }

        public void AddFields(string prefix, object? owner, Type? stopAt)
        {
            if (owner == null) return;
            Exclude(owner);
            for (var at = owner.GetType(); at != null && at != stopAt && at != typeof(object); at = at.BaseType)
                foreach (var field in at.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.FieldType.IsPrimitive || field.FieldType.IsEnum) continue;
                    object? value;
                    try { value = field.GetValue(owner); }
                    catch (Exception) { Failures++; continue; }
                    if (value != null) Add(prefix + field.Name, value);
                }
        }

        private bool Visit(object value)
        {
            if (seen.ContainsKey(value)) return false;
            seen.Add(value, true);
            var type = value.GetType();
            if (value is Type || value is MemberInfo || value is Assembly || value is Module || value is Delegate d && d.Target == null) return false;
            if (!type.IsArray && !(value is string) && !Walkable(type))
            {
                if (!foreign.TryGetValue(type, out var kind)) foreign.Add(type, kind = new Row(Name(type)));
                kind.Objects++;
                return false;
            }
            stack.Push(value);
            return true;
        }

        private bool Walkable(Type type)
        {
            if (walkableTypes.TryGetValue(type, out bool known)) return known;
            bool walk = walkable(type);
            walkableTypes.Add(type, walk);
            return walk;
        }

        public void AddOwnedEntries(IDictionary<string, string>? shared, string ownedKeyPrefix, string rootPrefix)
        {
            if (shared == null) return;
            Exclude(shared);
            var owned = new List<KeyValuePair<string, string>>();
            foreach (var entry in shared)
                if (entry.Key != null && entry.Key.StartsWith(ownedKeyPrefix, StringComparison.Ordinal)) owned.Add(entry);
            owned.Sort((a, b) => StringComparer.Ordinal.Compare(a.Key, b.Key));
            foreach (var entry in owned) Add(rootPrefix + entry.Key, entry.Value);
        }

        private void Measure(object value, Row row)
        {
            var type = value.GetType();
            long bytes;
            bool scanned;
            if (value is string text) { bytes = Align(Header + 4 + 2L * (text.Length + 1)); scanned = false; }
            else if (value is Array array)
            {
                var element = type.GetElementType()!;
                long length = array.LongLength;
                if (!element.IsValueType)
                {
                    bytes = Align(ArrayHeader + length * Pointer); scanned = length > 0;
                    foreach (var item in array) if (item != null) Visit(item);
                }
                else
                {
                    var layout = Of(element);
                    bytes = Align(ArrayHeader + length * layout.Bytes); scanned = layout.References > 0 && length > 0;
                    if (layout.References > 0) foreach (var item in array) if (item != null) Children(item, layout);
                }
            }
            else
            {
                var layout = Of(type);
                bytes = Align(Header + layout.Bytes); scanned = layout.References > 0;
                Children(value, layout);
            }
            row.Objects++; row.Bytes += bytes; if (scanned) row.ScannedBytes += bytes;
            if (!byType.TryGetValue(type, out var kind)) byType.Add(type, kind = new Row(Name(type)));
            kind.Objects++; kind.Bytes += bytes; if (scanned) kind.ScannedBytes += bytes;
            if (perRoot.Contains(type))
            {
                row.Tracked ??= new Dictionary<Type, long[]>();
                if (!row.Tracked.TryGetValue(type, out var counts)) row.Tracked.Add(type, counts = new long[2]);
                counts[0]++; counts[1] += bytes;
            }
        }

        private void Children(object value, Layout layout)
        {
            foreach (var field in layout.Fields)
            {
                object? child;
                try { child = field.GetValue(value); }
                catch (Exception) { Failures++; continue; }
                if (child == null) continue;
                if (field.FieldType.IsValueType) { Children(child, Of(field.FieldType)); continue; }
                Visit(child);
            }
        }

        private Layout Of(Type type)
        {
            if (layouts.TryGetValue(type, out var known)) return known;
            var layout = new Layout();
            var fields = new List<FieldInfo>();
            for (var at = type; at != null && at != typeof(object) && at != typeof(ValueType); at = at.BaseType)
                foreach (var field in at.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    var f = field.FieldType;
                    if (f.IsPointer || f == typeof(IntPtr) || f == typeof(UIntPtr)) { layout.Bytes += Pointer; continue; }
                    if (!f.IsValueType) { layout.Bytes += Pointer; layout.References++; fields.Add(field); continue; }
                    if (f.IsPrimitive || f.IsEnum) { layout.Bytes += Primitive(f); continue; }
                    if (f == type) { layout.Bytes += Pointer; continue; }
                    var inner = Of(f);
                    layout.Bytes += inner.Bytes; layout.References += inner.References;
                    if (inner.References > 0) fields.Add(field);
                }
            if (type.IsValueType && layout.Bytes == 0) layout.Bytes = 1;
            if (type.IsValueType && layout.Bytes > 1) layout.Bytes = (int)Align(layout.Bytes, Math.Min(Pointer, Largest(type)));
            layout.Fields = fields.ToArray();
            layouts[type] = layout;
            return layout;
        }

        private static int Largest(Type type)
        {
            int largest = 1;
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var f = field.FieldType;
                int size = !f.IsValueType || f.IsPointer || f == typeof(IntPtr) || f == typeof(UIntPtr) ? Pointer
                    : f.IsPrimitive || f.IsEnum ? Primitive(f) : f == type ? Pointer : Largest(f);
                if (size > largest) largest = size;
            }
            return largest;
        }

        private static int Primitive(Type type)
        {
            switch (Type.GetTypeCode(type.IsEnum ? Enum.GetUnderlyingType(type) : type))
            {
                case TypeCode.Boolean: case TypeCode.Byte: case TypeCode.SByte: return 1;
                case TypeCode.Char: case TypeCode.Int16: case TypeCode.UInt16: return 2;
                case TypeCode.Int32: case TypeCode.UInt32: case TypeCode.Single: return 4;
                default: return 8;
            }
        }

        private static long Align(long bytes) => Align(bytes, 8);
        private static long Align(long bytes, long to) => (bytes + to - 1) / to * to;

        public static string Name(Type type)
        {
            if (type.IsArray) return Name(type.GetElementType()!) + "[]";
            if (!type.IsGenericType) return type.Name;
            string name = type.Name;
            int tick = name.IndexOf('`');
            if (tick >= 0) name = name.Substring(0, tick);
            return name + "<" + string.Join(",", type.GetGenericArguments().Select(Name)) + ">";
        }

        public long ObjectsOf(Type type) => byType.TryGetValue(type, out var row) ? row.Objects : foreign.TryGetValue(type, out var outside) ? outside.Objects : 0;

        public long BytesOf(Type type) => byType.TryGetValue(type, out var row) ? row.Bytes : 0;

        public readonly struct JourneyLegLedger
        {
            public JourneyLegLedger(long journeys, long travelling, long legs, long splitPrefixes, long mostLegs)
                : this(journeys, travelling, legs, splitPrefixes, mostLegs, 0) { }
            public JourneyLegLedger(long journeys, long travelling, long legs, long splitPrefixes, long mostLegs, long mostSplitPrefixes)
                : this(journeys, travelling, legs, splitPrefixes, mostLegs, mostSplitPrefixes, 0, 0, 0) { }
            public JourneyLegLedger(long journeys, long travelling, long legs, long splitPrefixes, long mostLegs, long mostSplitPrefixes, long legArrays, long legArrayBytes, long legArraySlots)
            {
                Journeys = journeys; Travelling = travelling; Legs = legs; SplitPrefixes = splitPrefixes; MostLegs = mostLegs; MostSplitPrefixes = mostSplitPrefixes;
                LegArrays = legArrays; LegArrayBytes = legArrayBytes; LegArraySlots = legArraySlots;
            }
            public long LegArrays { get; }
            public long LegArraySlots { get; }
            public long LegArrayBytes { get; }
            public long Journeys { get; }
            public long Travelling { get; }
            public long Legs { get; }
            public long SplitPrefixes { get; }
            public long MostLegs { get; }
            public long MostSplitPrefixes { get; }
        }

        private void AppendLive(StringBuilder text, string name, Type type, long live)
        {
            long retained = ObjectsOf(type);
            long cached = CachedObjectsOf(type);
            text.Append('"').Append(name).Append("\":{\"retained\":").Append(retained.ToString(CultureInfo.InvariantCulture))
                .Append(",\"live\":").Append(live.ToString(CultureInfo.InvariantCulture))
                .Append(",\"in_caches\":").Append(cached.ToString(CultureInfo.InvariantCulture))
                .Append(",\"outside_live_and_caches\":").Append((retained - live - cached).ToString(CultureInfo.InvariantCulture)).Append('}');
        }

        public static JourneyLegLedger JourneyLegs(CityState? state)
        {
            if (state == null) return default;
            var legs = new HashSet<RouteLeg>(LegIdentity.Instance);
            var arrays = new HashSet<RouteLegs>(Identity.Instance);
            long journeys = 0, travelling = 0, splits = 0, most = 0, mostSplits = 0, arrayBytes = 0, slots = 0;
            foreach (var person in PersonTable.Schedules(state))
            {
                if (person.Phase == CitizenPhase.Travelling) travelling++;
                if (person.Journey == null) continue;
                journeys++;
                most = Math.Max(most, person.Journey.Legs.Count);
                if (person.Journey.Legs.Capacity > 0 && arrays.Add(person.Journey.Legs))
                {
                    slots += person.Journey.Legs.Capacity;
                    arrayBytes += Align(ArrayHeader + (long)person.Journey.Legs.Capacity * Pointer);
                }
                long own = 0;
                foreach (var leg in person.Journey.Legs)
                {
                    if (leg == null) continue;
                    if (leg.SplitPrefix) own++;
                    if (legs.Add(leg) && leg.SplitPrefix) splits++;
                }
                mostSplits = Math.Max(mostSplits, own);
            }
            return new JourneyLegLedger(journeys, travelling, legs.Count, splits, most, mostSplits, arrays.Count, arrayBytes, slots);
        }

        public readonly struct TrafficVehicleLedger
        {
            public TrafficVehicleLedger(long vehicles, long routes, long legs) { Vehicles = vehicles; Routes = routes; Legs = legs; }
            public long Vehicles { get; }
            public long Routes { get; }
            public long Legs { get; }
        }

        public static TrafficVehicleLedger TrafficVehicles(TrafficKernel? kernel)
        {
            if (kernel == null) return default;
            var vehicles = new List<TrafficVehicleState>();
            kernel.CopyVehiclesInOrder(vehicles);
            var routes = new HashSet<object>(Identity.Instance);
            var legs = new HashSet<object>(Identity.Instance);
            foreach (var vehicle in vehicles)
            {
                if (vehicle.Route == null) continue;
                routes.Add(vehicle.Route);
                foreach (var leg in vehicle.Route) if (leg != null) legs.Add(leg);
            }
            return new TrafficVehicleLedger(vehicles.Count, routes.Count, legs.Count);
        }

        private sealed class LegIdentity : IEqualityComparer<RouteLeg>
        {
            public static readonly LegIdentity Instance = new LegIdentity();
            public bool Equals(RouteLeg x, RouteLeg y) => ReferenceEquals(x, y);
            public int GetHashCode(RouteLeg value) => RuntimeHelpers.GetHashCode(value);
        }

        public string ReportJson(int top, double elapsedMilliseconds, IEnumerable<Type>? counted, Func<string, bool>? diagnostic, IEnumerable<Type>? tracked, JourneyLegLedger? journeyLegs)
            => ReportJson(top, elapsedMilliseconds, counted, diagnostic, tracked, journeyLegs, null);

        public string ReportJson(int top, double elapsedMilliseconds, IEnumerable<Type>? counted, Func<string, bool>? diagnostic, IEnumerable<Type>? tracked, JourneyLegLedger? journeyLegs, TrafficVehicleLedger? trafficVehicles)
        {
            string report = ReportJson(top, elapsedMilliseconds, counted, diagnostic);
            var text = new StringBuilder(report, 0, report.Length - 1, report.Length + 2048).Append(",\"tracked_types\":[");
            bool first = true;
            foreach (var type in tracked ?? Array.Empty<Type>())
            {
                if (!first) text.Append(',');
                first = false;
                text.Append("{\"name\":\"").Append(Name(type).Replace("\\", "\\\\").Replace("\"", "\\\""))
                    .Append("\",\"objects\":").Append(ObjectsOf(type).ToString(CultureInfo.InvariantCulture))
                    .Append(",\"bytes\":").Append(BytesOf(type).ToString(CultureInfo.InvariantCulture)).Append('}');
            }
            text.Append(']');
            if (perRoot.Count > 0)
            {
                text.Append(",\"tracked_types_outside_caches\":[");
                first = true;
                foreach (var type in tracked ?? Array.Empty<Type>())
                {
                    if (!perRoot.Contains(type)) continue;
                    if (!first) text.Append(',');
                    first = false;
                    text.Append("{\"name\":\"").Append(Name(type).Replace("\\", "\\\\").Replace("\"", "\\\""))
                        .Append("\",\"objects\":").Append((ObjectsOf(type) - CachedObjectsOf(type)).ToString(CultureInfo.InvariantCulture))
                        .Append(",\"bytes\":").Append((BytesOf(type) - CachedBytesOf(type)).ToString(CultureInfo.InvariantCulture)).Append('}');
                }
                text.Append("],\"tracked_roots\":[");
                first = true;
                foreach (var type in perRoot.OrderBy(t => Name(t), StringComparer.Ordinal))
                {
                    if (!first) text.Append(',');
                    first = false;
                    text.Append("{\"name\":\"").Append(Name(type).Replace("\\", "\\\\").Replace("\"", "\\\"")).Append("\",\"roots\":[");
                    bool firstRoot = true;
                    foreach (var root in byRoot.Where(r => r.TrackedObjects(type) > 0).OrderByDescending(r => r.TrackedObjects(type)).ThenBy(r => r.Name, StringComparer.Ordinal).Take(TrackedRootsPerType))
                    {
                        if (!firstRoot) text.Append(',');
                        firstRoot = false;
                        text.Append("{\"name\":\"").Append(root.Name.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append("\",\"cache\":").Append(root.Cache ? "true" : "false")
                            .Append(",\"objects\":").Append(root.TrackedObjects(type).ToString(CultureInfo.InvariantCulture))
                            .Append(",\"bytes\":").Append(root.TrackedBytes(type).ToString(CultureInfo.InvariantCulture)).Append('}');
                    }
                    text.Append("]}");
                }
                text.Append("],\"cache_roots\":[");
                first = true;
                foreach (var root in CacheRoots)
                {
                    if (!first) text.Append(',');
                    first = false;
                    text.Append("{\"name\":\"").Append(root.Name.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append("\",\"bound\":").Append(root.Bound.ToString(CultureInfo.InvariantCulture))
                        .Append(",\"objects\":").Append(root.Objects.ToString(CultureInfo.InvariantCulture)).Append(",\"bytes\":").Append(root.Bytes.ToString(CultureInfo.InvariantCulture))
                        .Append(",\"routes\":").Append(root.TrackedObjects(typeof(Route)).ToString(CultureInfo.InvariantCulture))
                        .Append(",\"route_legs\":").Append(root.TrackedObjects(typeof(RouteLeg)).ToString(CultureInfo.InvariantCulture)).Append('}');
                }
                text.Append(']');
            }
            if (journeyLegs is JourneyLegLedger ledger)
            {
                long retained = ObjectsOf(typeof(RouteLeg));
                long cached = CachedObjectsOf(typeof(RouteLeg));
                text.Append(",\"route_legs\":{\"retained\":").Append(retained.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"in_current_journeys\":").Append(ledger.Legs.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"outside_current_journeys\":").Append((retained - ledger.Legs).ToString(CultureInfo.InvariantCulture))
                    .Append(",\"journeys\":").Append(ledger.Journeys.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"travelling\":").Append(ledger.Travelling.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"split_prefix_legs\":").Append(ledger.SplitPrefixes.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"most_legs_in_a_journey\":").Append(ledger.MostLegs.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"most_split_prefixes_in_a_journey\":").Append(ledger.MostSplitPrefixes.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"in_caches\":").Append(cached.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"outside_current_journeys_and_caches\":").Append((retained - ledger.Legs - cached).ToString(CultureInfo.InvariantCulture));
                long arrayBytes = BytesOf(typeof(RouteLeg[]));
                long cachedArrayBytes = CachedBytesOf(typeof(RouteLeg[]));
                text.Append(",\"leg_arrays_in_current_journeys\":").Append(ledger.LegArrays.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"leg_array_slots_in_current_journeys\":").Append(ledger.LegArraySlots.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"leg_array_bytes_retained\":").Append(arrayBytes.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"leg_array_bytes_in_current_journeys\":").Append(ledger.LegArrayBytes.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"leg_array_bytes_in_caches\":").Append(cachedArrayBytes.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"leg_array_bytes_outside_current_journeys_and_caches\":").Append((arrayBytes - ledger.LegArrayBytes - cachedArrayBytes).ToString(CultureInfo.InvariantCulture)).Append('}');
            }
            if (trafficVehicles is TrafficVehicleLedger traffic)
            {
                text.Append(",\"traffic_vehicles\":{");
                AppendLive(text, "vehicles", typeof(TrafficVehicleState), traffic.Vehicles);
                text.Append(',');
                AppendLive(text, "routes", typeof(List<TrafficRouteLeg>), traffic.Routes);
                text.Append(',');
                AppendLive(text, "legs", typeof(TrafficRouteLeg), traffic.Legs);
                text.Append('}');
            }
            return text.Append('}').ToString();
        }

        public string ReportJson(int top, double elapsedMilliseconds, IEnumerable<Type>? counted = null) => ReportJson(top, elapsedMilliseconds, counted, null);

        public static bool InstrumentRoot(string root) => root == "runtime.gcSampler" || root == "static.PhysicalPedestrians.growLog"
            || root.StartsWith("static.RetainedCensus.", StringComparison.Ordinal) || root.StartsWith("static.ModFrames.", StringComparison.Ordinal)
            || root.StartsWith("static.PhysicalAllocation.", StringComparison.Ordinal) || root.StartsWith("static.AdvanceAllocation.", StringComparison.Ordinal);

        public Row Diagnostics(Func<string, bool> diagnostic)
        {
            var total = new Row("diagnostics");
            foreach (var root in byRoot)
                if (diagnostic(root.Name)) { total.Objects += root.Objects; total.Bytes += root.Bytes; total.ScannedBytes += root.ScannedBytes; }
            return total;
        }

        public string ReportJson(int top, double elapsedMilliseconds, IEnumerable<Type>? counted, Func<string, bool>? diagnostic)
        {
            string N(long value) => value.ToString(CultureInfo.InvariantCulture);
            string S(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            void Rows(StringBuilder text, string name, IEnumerable<Row> rows, bool bytes)
            {
                text.Append(",\"").Append(name).Append("\":[");
                bool first = true;
                foreach (var row in rows.Take(top))
                {
                    if (!first) text.Append(',');
                    first = false;
                    text.Append("{\"name\":").Append(S(row.Name)).Append(",\"objects\":").Append(N(row.Objects));
                    if (bytes) text.Append(",\"bytes\":").Append(N(row.Bytes)).Append(",\"scanned_bytes\":").Append(N(row.ScannedBytes));
                    text.Append('}');
                }
                text.Append(']');
            }
            var report = new StringBuilder(16 * 1024)
                .Append("{\"policy\":\"").Append(PolicyId).Append("\",\"layout\":\"mono-64\",\"objects\":").Append(N(Objects))
                .Append(",\"bytes\":").Append(N(Bytes)).Append(",\"scanned_bytes\":").Append(N(ScannedBytes))
                .Append(",\"roots\":").Append(N(byRoot.Count)).Append(",\"foreign_objects\":").Append(N(ForeignObjects))
                .Append(",\"failures\":").Append(N(Failures))
                .Append(",\"elapsed_ms\":").Append(Math.Round(elapsedMilliseconds, 1).ToString("0.#", CultureInfo.InvariantCulture));
            if (diagnostic != null)
            {
                var instruments = Diagnostics(diagnostic);
                report.Append(",\"diagnostic_objects\":").Append(N(instruments.Objects)).Append(",\"diagnostic_bytes\":").Append(N(instruments.Bytes))
                    .Append(",\"city_objects\":").Append(N(Objects - instruments.Objects)).Append(",\"city_bytes\":").Append(N(Bytes - instruments.Bytes))
                    .Append(",\"diagnostic_roots\":[");
                bool firstRoot = true;
                foreach (var root in byRoot)
                {
                    if (root.Objects == 0 || !diagnostic(root.Name)) continue;
                    if (!firstRoot) report.Append(',');
                    firstRoot = false;
                    report.Append(S(root.Name));
                }
                report.Append(']');
            }
            Rows(report, "top_roots", byRoot.Where(r => r.Objects > 0).OrderByDescending(r => r.Bytes).ThenBy(r => r.Name, StringComparer.Ordinal), true);
            Rows(report, "top_types", Types, true);
            Rows(report, "foreign_types", Foreign, false);
            report.Append(",\"type_counts\":{");
            bool firstCount = true;
            foreach (var type in counted ?? Array.Empty<Type>())
            {
                if (!firstCount) report.Append(',');
                firstCount = false;
                report.Append(S(Name(type))).Append(':').Append(N(ObjectsOf(type)));
            }
            report.Append('}');
            return report.Append('}').ToString();
        }
    }
}
