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
        }

        public const string PolicyId = "retained-census-v1";
        public const int Header = 16, ArrayHeader = 32, Pointer = 8;
        private static readonly Dictionary<Type, Layout> Layouts = new Dictionary<Type, Layout>();
        private readonly Dictionary<object, bool> seen = new Dictionary<object, bool>(Identity.Instance);
        private readonly Dictionary<Type, Row> byType = new Dictionary<Type, Row>();
        private readonly Dictionary<Type, Row> foreign = new Dictionary<Type, Row>();
        private readonly List<Row> byRoot = new List<Row>();
        private readonly Func<Type, bool> walkable;
        private readonly Dictionary<Type, bool> walkableTypes = new Dictionary<Type, bool>();
        private readonly Stack<object> stack = new Stack<object>();

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

        private static Layout Of(Type type)
        {
            lock (Layouts)
            {
                if (Layouts.TryGetValue(type, out var known)) return known;
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
                Layouts[type] = layout;
                return layout;
            }
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
