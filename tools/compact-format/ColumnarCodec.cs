using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PersistentCity.Tools.CompactFormat
{
    /// <summary>
    /// F7p columnar compact JSON (candidates B and C, plan section 3.6). Every array of entities becomes one
    /// column per field, recursively, so nested lists flatten into a lengths vector plus one child column.
    /// Strings that occur at least twice go into one ordinally sorted table and are written as payload-local
    /// indices; field names and field sequences ("shapes") are tables too. Number tokens are copied as exact
    /// text, so integer money and round-trip doubles cannot change. Indices are never identities: they are
    /// recomputed from content on every encode and never leave one payload.
    ///
    /// Column grammar (first element is the tag; an empty column is the literal 0):
    ///   [0, n...]            numbers (exact token text)
    ///   [1, i|"s"...]        strings: table index, or inline literal for a string seen once
    ///   [2, "0101"]          booleans
    ///   [3, count]           all null
    ///   [4, shape|[shape...], column...]  objects; one column per field in first-appearance order
    ///   [5, len|[len...], column]         arrays; one flattened child column
    ///   [6, column...]       mixed kinds, one single-value column per row
    ///   [7, [nullRow...], column]         nulls plus one other kind
    ///   [8, ref|null...]     shared route table references (candidate C)
    ///   [9, "0101", column, [from|null...], [to...]]  native road tags split into head and node range (C)
    ///   [11, mode, "packed"] canonical integers as zigzag base-64 varints (mode 0 values, mode 1 deltas)
    ///   [12, mode, "packed"] the same for integral doubles spelled "N.0"
    ///   [10, len|[len...], column...]     positional arrays (writer-25 tuples): column i holds slot i of every
    ///                        row longer than i; used when the flattened slots would mix kinds
    /// Offline prototype only; F7d decides the shipped grammar.
    /// </summary>
    public sealed class ColumnarOptions
    {
        public ColumnarOptions(string format, bool routeTable, bool roadTags)
        { Format = format; RouteTable = routeTable; RoadTags = roadTags; }
        public string Format { get; }
        /// <summary>Deduplicate committed route values (fields named in <see cref="ColumnarCodec.RouteFields"/>) by exact content.</summary>
        public bool RouteTable { get; }
        /// <summary>Split native road-tag ServiceIds into a topology head plus a chained node range.</summary>
        public bool RoadTags { get; }

        public static readonly ColumnarOptions B = new ColumnarOptions("pcc-b", false, false);
        public static readonly ColumnarOptions C = new ColumnarOptions("pcc-c", true, true);
    }

    public sealed class ColumnarStats
    {
        public int Strings, Keys, Shapes, Routes, RouteReferences, RoadTags, Containers;
        public Dictionary<string, long> PathCharacters = new Dictionary<string, long>(StringComparer.Ordinal);
        public int TableEntries => Strings + Keys + Shapes + Routes;
    }

    public static class ColumnarCodec
    {
        public const int Version = 1;
        /// <summary>Committed route values: the citizen Journey (Route with Legs) and the traffic vehicle Route leg list.</summary>
        public static readonly string[] RouteFields = { "Journey", "Route" };
        public const string RoadServiceField = "ServiceId";
        private const string RoadPrefix = "native:road:";
        /// <summary>Depth of per-field size accounting reported in the metrics (not part of the format).</summary>
        public const int PathDepth = 6;

        public static string Encode(object? root, ColumnarOptions options, ColumnarStats? stats = null, int capacity = 1024)
            => new Encoder(options, stats).Run(root, capacity);

        public static object? Decode(string payload, ColumnarOptions options) => new Decoder(options).Run(payload);

        private static bool IsRouteField(string key) => Array.IndexOf(RouteFields, key) >= 0;

        private static string ShapeKey(string[] keys) => string.Join("\u0000", keys);

        /// <summary>native:road:(v1|fragment1):topology:from:to with canonical non-negative node indices, else false.</summary>
        internal static bool TrySplitRoad(string value, out string head, out int from, out int to)
        {
            head = ""; from = to = 0;
            if (!value.StartsWith(RoadPrefix, StringComparison.Ordinal)) return false;
            string[] parts = value.Split(':');
            if (parts.Length != 6 || (parts[2] != "v1" && parts[2] != "fragment1")) return false;
            if (!int.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out from)
                || !int.TryParse(parts[5], NumberStyles.None, CultureInfo.InvariantCulture, out to)) return false;
            if (from.ToString(CultureInfo.InvariantCulture) != parts[4] || to.ToString(CultureInfo.InvariantCulture) != parts[5]) return false;
            head = value.Substring(0, value.Length - parts[4].Length - parts[5].Length - 2);
            return true;
        }

        private sealed class Encoder
        {
            private readonly ColumnarOptions options;
            private readonly ColumnarStats? stats;
            private readonly Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly Dictionary<string, int> strings = new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly Dictionary<string, int> keys = new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly Dictionary<string[], string> joinedByArray = new Dictionary<string[], string>(ReferenceEqualityComparer.Instance);
            private readonly Dictionary<string, string[]> shapeByJoined = new Dictionary<string, string[]>(StringComparer.Ordinal);
            private readonly Dictionary<string, int> shapes = new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly Dictionary<string[], int> shapeOfArray = new Dictionary<string[], int>(ReferenceEqualityComparer.Instance);
            private readonly Dictionary<object, string> routeContent = new Dictionary<object, string>(ReferenceEqualityComparer.Instance);
            private readonly Dictionary<string, object> uniqueRoutes = new Dictionary<string, object>(StringComparer.Ordinal);
            private readonly Dictionary<string, int> routeIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            private StringBuilder output = new StringBuilder();

            public Encoder(ColumnarOptions options, ColumnarStats? stats) { this.options = options; this.stats = stats; }

            public string Run(object? root, int capacity)
            {
                Collect(root);
                var stringTable = counts.Where(p => p.Value >= 2).Select(p => p.Key).OrderBy(s => s, StringComparer.Ordinal).ToArray();
                for (int i = 0; i < stringTable.Length; i++) strings.Add(stringTable[i], i);
                var keyTable = keys.Keys.OrderBy(s => s, StringComparer.Ordinal).ToArray();
                for (int i = 0; i < keyTable.Length; i++) keys[keyTable[i]] = i;
                var shapeTable = shapeByJoined.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Value).ToArray();
                for (int i = 0; i < shapeTable.Length; i++) shapes.Add(ShapeKey(shapeTable[i]), i);
                foreach (var pair in joinedByArray) shapeOfArray.Add(pair.Key, shapes[pair.Value]);
                var routeTable = uniqueRoutes.OrderBy(p => p.Key, StringComparer.Ordinal).ToArray();
                for (int i = 0; i < routeTable.Length; i++) routeIndex.Add(routeTable[i].Key, i);

                output = new StringBuilder(capacity);
                output.Append("{\"f\":"); WireTree.WriteString(output, options.Format);
                output.Append(",\"v\":").Append(Version).Append(",\"k\":[");
                for (int i = 0; i < keyTable.Length; i++) { if (i > 0) output.Append(','); WireTree.WriteString(output, keyTable[i]); }
                output.Append("],\"h\":[");
                for (int i = 0; i < shapeTable.Length; i++)
                {
                    if (i > 0) output.Append(',');
                    output.Append('[');
                    for (int j = 0; j < shapeTable[i].Length; j++) { if (j > 0) output.Append(','); output.Append(keys[shapeTable[i][j]]); }
                    output.Append(']');
                }
                output.Append("],\"s\":[");
                for (int i = 0; i < stringTable.Length; i++) { if (i > 0) output.Append(','); WireTree.WriteString(output, stringTable[i]); }
                output.Append(']');
                if (options.RouteTable)
                {
                    output.Append(",\"n\":").Append(routeTable.Length).Append(",\"r\":");
                    Column(routeTable.Select(p => p.Value).ToList<object?>(), "routes", "#routes", 1);
                }
                output.Append(",\"t\":");
                Column(new List<object?> { root }, "", "$", 0);
                output.Append('}');
                if (stats != null)
                {
                    stats.Strings = stringTable.Length; stats.Keys = keyTable.Length; stats.Shapes = shapeTable.Length; stats.Routes = routeTable.Length;
                }
                return output.ToString();
            }

            private void Collect(object? value, string key = "")
            {
                switch (value)
                {
                    case string s:
                        // Count exactly the text the column writer emits: a road tag's head, otherwise the whole string.
                        if (options.RoadTags && key == RoadServiceField && TrySplitRoad(s, out var head, out _, out _)) s = head;
                        counts[s] = counts.TryGetValue(s, out int c) ? c + 1 : 1;
                        return;
                    case WireObject o:
                        Joined(o.Keys);
                        for (int i = 0; i < o.Keys.Length; i++)
                        {
                            keys[o.Keys[i]] = 0;
                            var child = o.Values[i];
                            if (options.RouteTable && IsRouteField(o.Keys[i]) && (child is WireObject || child is object?[])) RegisterRoute(child!);
                            else Collect(child, o.Keys[i]);
                        }
                        return;
                    case object?[] a: foreach (var v in a) Collect(v, key); return;
                }
            }

            /// <summary>Field sequence of one key array, computed once per distinct array instance.</summary>
            private string Joined(string[] array)
            {
                if (joinedByArray.TryGetValue(array, out var joined)) return joined;
                joined = ShapeKey(array);
                joinedByArray.Add(array, joined);
                if (!shapeByJoined.ContainsKey(joined)) shapeByJoined.Add(joined, array);
                return joined;
            }

            private void RegisterRoute(object route)
            {
                if (routeContent.ContainsKey(route)) return;
                string content = WireTree.Write(route, 256);
                routeContent.Add(route, content);
                if (uniqueRoutes.ContainsKey(content)) return;
                uniqueRoutes.Add(content, route);
                Collect(route);
            }

            private void Column(List<object?> values, string key, string path, int depth)
            {
                int before = output.Length;
                WriteColumn(values, key, path, depth);
                if (stats != null && depth <= PathDepth)
                    stats.PathCharacters[path] = (stats.PathCharacters.TryGetValue(path, out long sum) ? sum : 0) + output.Length - before;
            }

            private void WriteColumn(List<object?> values, string key, string path, int depth)
            {
                int n = values.Count;
                if (n == 0) { output.Append('0'); return; }
                if (stats != null) stats.Containers++;
                int nulls = 0, bools = 0, numbers = 0, texts = 0, objects = 0, arrays = 0;
                foreach (var v in values)
                {
                    switch (v)
                    {
                        case null: nulls++; break; case bool _: bools++; break; case WireNumber _: numbers++; break;
                        case string _: texts++; break; case WireObject _: objects++; break; case object?[] _: arrays++; break;
                    }
                }
                int kinds = (bools > 0 ? 1 : 0) + (numbers > 0 ? 1 : 0) + (texts > 0 ? 1 : 0) + (objects > 0 ? 1 : 0) + (arrays > 0 ? 1 : 0);
                if (options.RouteTable && IsRouteField(key) && texts + numbers + bools == 0 && objects + arrays > 0)
                {
                    output.Append("[8");
                    foreach (var v in values)
                    {
                        output.Append(',');
                        if (v == null) output.Append("null"); else { output.Append(routeIndex[routeContent[v]]); if (stats != null) stats.RouteReferences++; }
                    }
                    output.Append(']');
                    return;
                }
                if (nulls == n) { output.Append("[3,").Append(n).Append(']'); return; }
                if (nulls > 0 && kinds == 1)
                {
                    output.Append("[7,[");
                    var rest = new List<object?>(n - nulls);
                    bool first = true;
                    for (int i = 0; i < n; i++)
                    {
                        if (values[i] != null) { rest.Add(values[i]); continue; }
                        if (!first) output.Append(',');
                        output.Append(i); first = false;
                    }
                    output.Append("],");
                    WriteColumn(rest, key, path, depth);
                    output.Append(']');
                    return;
                }
                if (kinds > 1)
                {
                    output.Append("[6");
                    foreach (var v in values) { output.Append(','); WriteColumn(new List<object?> { v }, key, path, depth); }
                    output.Append(']');
                    return;
                }
                if (numbers == n && Packed(values)) return;
                if (numbers == n)
                {
                    output.Append("[0");
                    foreach (var v in values) output.Append(',').Append(((WireNumber)v!).Text);
                    output.Append(']');
                    return;
                }
                if (bools == n)
                {
                    output.Append("[2,\"");
                    foreach (var v in values) output.Append((bool)v! ? '1' : '0');
                    output.Append("\"]");
                    return;
                }
                if (texts == n) { Strings(values, key, path, depth); return; }
                if (objects == n) { Objects(values, path, depth); return; }
                Arrays(values, key, path, depth);
            }

            /// <summary>
            /// Write an all-integer (or all "N.0") column as one varint string when that is shorter than the
            /// number list. Only tokens whose text is exactly the invariant spelling of a long qualify, so the
            /// decoder reproduces every token character for character.
            /// </summary>
            private bool Packed(List<object?> values)
            {
                int n = values.Count;
                var numbers = new long[n];
                bool integral = ((WireNumber)values[0]!).Text.EndsWith(".0", StringComparison.Ordinal);
                long raw = n;
                for (int i = 0; i < n; i++)
                {
                    string text = ((WireNumber)values[i]!).Text;
                    raw += text.Length;
                    if (integral != text.EndsWith(".0", StringComparison.Ordinal)) return false;
                    if (!Varint.TryCanonical(integral ? text.Substring(0, text.Length - 2) : text, out numbers[i])) return false;
                }
                string plain = Varint.Pack(numbers, false), delta = Varint.Pack(numbers, true);
                bool useDelta = delta.Length < plain.Length;
                string packed = useDelta ? delta : plain;
                if (packed.Length + 8 >= raw) return false;
                output.Append(integral ? "[12," : "[11,").Append(useDelta ? '1' : '0').Append(",\"").Append(packed).Append("\"]");
                return true;
            }

            private void Text(string s)
            {
                if (strings.TryGetValue(s, out int index)) output.Append(index); else WireTree.WriteString(output, s);
            }

            private void Strings(List<object?> values, string key, string path, int depth)
            {
                if (options.RoadTags && key == RoadServiceField)
                {
                    var heads = new List<object?>(values.Count); var bitmap = new StringBuilder(values.Count);
                    var froms = new List<int>(); var tos = new List<int>(); var headOfRange = new List<string>();
                    foreach (string s in values.Cast<string>())
                    {
                        if (TrySplitRoad(s, out var head, out int from, out int to)) { heads.Add(head); bitmap.Append('1'); froms.Add(from); tos.Add(to); headOfRange.Add(head); }
                        else { heads.Add(s); bitmap.Append('0'); }
                    }
                    if (froms.Count > 0)
                    {
                        if (stats != null) stats.RoadTags += froms.Count;
                        output.Append("[9,\"").Append(bitmap).Append("\",");
                        Strings(heads, "", path, depth);
                        output.Append(",[");
                        for (int i = 0; i < froms.Count; i++)
                        {
                            if (i > 0) output.Append(',');
                            // A leg that starts where the previous road leg of the same topology ended is a range continuation.
                            if (i > 0 && froms[i] == tos[i - 1] && headOfRange[i] == headOfRange[i - 1]) output.Append("null");
                            else output.Append(froms[i]);
                        }
                        output.Append("],[").AppendJoin(',', tos).Append("]]");
                        return;
                    }
                }
                output.Append("[1");
                foreach (var v in values) { output.Append(','); Text((string)v!); }
                output.Append(']');
            }

            private void Objects(List<object?> values, string path, int depth)
            {
                int n = values.Count;
                var rowShapes = new int[n];
                var union = new List<string>();
                var positions = new Dictionary<int, string[]>();
                for (int i = 0; i < n; i++)
                {
                    var o = (WireObject)values[i]!;
                    rowShapes[i] = shapeOfArray[o.Keys];
                    if (positions.ContainsKey(rowShapes[i])) continue;
                    positions.Add(rowShapes[i], o.Keys);
                    foreach (var k in o.Keys) if (!union.Contains(k)) union.Add(k);
                }
                // Position of every union field in every distinct shape, so rows are not searched per field.
                var at = positions.ToDictionary(p => p.Key, p => union.Select(k => Array.IndexOf(p.Value, k)).ToArray());
                output.Append("[4,");
                if (positions.Count == 1) output.Append(rowShapes[0]);
                else { output.Append('[').AppendJoin(',', rowShapes).Append(']'); }
                for (int u = 0; u < union.Count; u++)
                {
                    var column = new List<object?>(n);
                    for (int i = 0; i < n; i++)
                    {
                        int position = at[rowShapes[i]][u];
                        if (position >= 0) column.Add(((WireObject)values[i]!).Values[position]);
                    }
                    output.Append(',');
                    Column(column, union[u], path + "." + union[u], depth + 1);
                }
                output.Append(']');
            }

            private void Arrays(List<object?> values, string key, string path, int depth)
            {
                var flat = new List<object?>();
                bool uniform = true; int length = ((object?[])values[0]!).Length, longest = 0;
                foreach (var v in values) { var a = (object?[])v!; if (a.Length != length) uniform = false; longest = Math.Max(longest, a.Length); flat.AddRange(a); }
                // Tuples (slots of different kinds, or numbers of different spellings) pack better slot by slot.
                bool positional = Kinds(flat) > 1 || NumberClasses(flat) > 1;
                output.Append(positional ? "[10," : "[5,");
                if (uniform) output.Append(length);
                else
                {
                    output.Append('[');
                    for (int i = 0; i < values.Count; i++) { if (i > 0) output.Append(','); output.Append(((object?[])values[i]!).Length); }
                    output.Append(']');
                }
                if (positional)
                {
                    for (int slot = 0; slot < longest; slot++)
                    {
                        var column = new List<object?>(values.Count);
                        foreach (var v in values) { var a = (object?[])v!; if (a.Length > slot) column.Add(a[slot]); }
                        output.Append(',');
                        Column(column, key, path + "[" + slot + "]", depth + 1);
                    }
                    output.Append(']');
                    return;
                }
                output.Append(',');
                Column(flat, key, path + "[]", depth + 1);
                output.Append(']');
            }

            private static int NumberClasses(List<object?> values)
            {
                bool integer = false, integral = false, other = false;
                foreach (var v in values)
                {
                    if (!(v is WireNumber number)) continue;
                    string text = number.Text;
                    if (Varint.TryCanonical(text, out _)) integer = true;
                    else if (text.EndsWith(".0", StringComparison.Ordinal) && Varint.TryCanonical(text.Substring(0, text.Length - 2), out _)) integral = true;
                    else other = true;
                    if ((integer ? 1 : 0) + (integral ? 1 : 0) + (other ? 1 : 0) > 1) return 2;
                }
                return 1;
            }

            private static int Kinds(List<object?> values)
            {
                bool bools = false, numbers = false, texts = false, objects = false, arrays = false;
                foreach (var v in values)
                {
                    switch (v)
                    {
                        case bool _: bools = true; break; case WireNumber _: numbers = true; break; case string _: texts = true; break;
                        case WireObject _: objects = true; break; case object?[] _: arrays = true; break;
                    }
                }
                return (bools ? 1 : 0) + (numbers ? 1 : 0) + (texts ? 1 : 0) + (objects ? 1 : 0) + (arrays ? 1 : 0);
            }
        }

        private sealed class Decoder
        {
            private readonly ColumnarOptions options;
            private string[] strings = System.Array.Empty<string>();
            private string[][] shapes = System.Array.Empty<string[]>();
            private object?[] routes = System.Array.Empty<object?>();

            public Decoder(ColumnarOptions options) { this.options = options; }

            public object? Run(string payload)
            {
                if (!(WireTree.Parse(payload, false) is WireObject top)) throw new FormatException("Compact payload is not an object.");
                string[] expected = options.RouteTable ? new[] { "f", "v", "k", "h", "s", "n", "r", "t" } : new[] { "f", "v", "k", "h", "s", "t" };
                if (!top.Keys.SequenceEqual(expected)) throw new FormatException("Unknown, missing or reordered compact fields.");
                if (!(top.Values[0] is string format) || format != options.Format) throw new FormatException("Wrong compact format.");
                if (Int(top.Values[1]) != Version) throw new FormatException("Unsupported compact version.");
                var keyTable = Array(top.Values[2]).Select(v => v as string ?? throw new FormatException("Key is not a string.")).ToArray();
                if (keyTable.Distinct(StringComparer.Ordinal).Count() != keyTable.Length) throw new FormatException("Duplicate key.");
                for (int i = 1; i < keyTable.Length; i++) if (string.CompareOrdinal(keyTable[i - 1], keyTable[i]) >= 0) throw new FormatException("Key table not ordinal.");
                shapes = Array(top.Values[3]).Select(v =>
                {
                    var shape = Array(v).Select(i => keyTable[Index(i, keyTable.Length)]).ToArray();
                    if (shape.Distinct(StringComparer.Ordinal).Count() != shape.Length) throw new FormatException("Duplicate field in shape.");
                    return shape;
                }).ToArray();
                for (int i = 1; i < shapes.Length; i++) if (string.CompareOrdinal(ShapeKey(shapes[i - 1]), ShapeKey(shapes[i])) >= 0) throw new FormatException("Shape table not ordinal.");
                strings = Array(top.Values[4]).Select(v => v as string ?? throw new FormatException("Table entry is not a string.")).ToArray();
                for (int i = 1; i < strings.Length; i++) if (string.CompareOrdinal(strings[i - 1], strings[i]) >= 0) throw new FormatException("String table not ordinal.");
                if (options.RouteTable) routes = Column(top.Values[6], Int(top.Values[5]));
                return Column(top.Values[options.RouteTable ? 7 : 5], 1)[0];
            }

            private static object?[] Array(object? value) => value as object?[] ?? throw new FormatException("Expected an array.");

            private static int Int(object? value)
            {
                if (!(value is WireNumber n) || !int.TryParse(n.Text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int result)
                    || result.ToString(CultureInfo.InvariantCulture) != n.Text) throw new FormatException("Expected a canonical integer.");
                return result;
            }

            private static int Index(object? value, int count)
            {
                int i = Int(value);
                if (i < 0 || i >= count) throw new FormatException("Table index out of range.");
                return i;
            }

            private object?[] Column(object? column, int n)
            {
                if (n < 0) throw new FormatException("Negative column length.");
                if (n == 0)
                {
                    if (!(column is WireNumber zero) || zero.Text != "0") throw new FormatException("Empty column must be 0.");
                    return System.Array.Empty<object?>();
                }
                var c = Array(column);
                if (c.Length == 0) throw new FormatException("Untagged column.");
                int tag = Int(c[0]);
                var result = new object?[n];
                switch (tag)
                {
                    case 0:
                        Count(c, n + 1);
                        for (int i = 0; i < n; i++) result[i] = c[i + 1] as WireNumber ?? throw new FormatException("Expected a number.");
                        return result;
                    case 11:
                    case 12:
                    {
                        Count(c, 3);
                        int mode = Int(c[1]);
                        if (mode != 0 && mode != 1) throw new FormatException("Unknown packing mode.");
                        var numbers = Varint.Unpack(c[2] as string ?? throw new FormatException("Expected packed text."), n, mode == 1);
                        for (int i = 0; i < n; i++)
                            result[i] = new WireNumber(tag == 12 ? numbers[i].ToString(CultureInfo.InvariantCulture) + ".0" : numbers[i].ToString(CultureInfo.InvariantCulture));
                        return result;
                    }
                    case 1:
                        Count(c, n + 1);
                        for (int i = 0; i < n; i++) result[i] = c[i + 1] is string s ? s : strings[Index(c[i + 1], strings.Length)];
                        return result;
                    case 2:
                    {
                        Count(c, 2);
                        var bits = c[1] as string ?? throw new FormatException("Expected a bitmap.");
                        if (bits.Length != n) throw new FormatException("Bitmap length.");
                        for (int i = 0; i < n; i++) result[i] = bits[i] == '1' ? WireTree.True : bits[i] == '0' ? WireTree.False : throw new FormatException("Bad bit.");
                        return result;
                    }
                    case 3:
                        Count(c, 2);
                        if (Int(c[1]) != n) throw new FormatException("Null count.");
                        return result;
                    case 4: return Objects(c, n);
                    case 5: return Arrays(c, n, false);
                    case 10: return Arrays(c, n, true);
                    case 6:
                        Count(c, n + 1);
                        for (int i = 0; i < n; i++) result[i] = Column(c[i + 1], 1)[0];
                        return result;
                    case 7:
                    {
                        Count(c, 3);
                        var positions = Array(c[1]);
                        var isNull = new bool[n]; int last = -1;
                        foreach (var p in positions) { int at = Index(p, n); if (at <= last) throw new FormatException("Null rows not ascending."); isNull[at] = true; last = at; }
                        if (positions.Length == 0 || positions.Length == n) throw new FormatException("Non-canonical nullable column.");
                        var rest = Column(c[2], n - positions.Length);
                        if (rest.Any(v => v == null)) throw new FormatException("Null inside a nullable column body.");
                        for (int i = 0, j = 0; i < n; i++) result[i] = isNull[i] ? null : rest[j++];
                        return result;
                    }
                    case 8:
                        if (!options.RouteTable) throw new FormatException("Route references need the route table.");
                        Count(c, n + 1);
                        for (int i = 0; i < n; i++) result[i] = c[i + 1] == null ? null : routes[Index(c[i + 1], routes.Length)];
                        return result;
                    case 9:
                    {
                        if (!options.RoadTags) throw new FormatException("Road ranges need the road-tag option.");
                        Count(c, 5);
                        var bits = c[1] as string ?? throw new FormatException("Expected a bitmap.");
                        if (bits.Length != n) throw new FormatException("Bitmap length.");
                        var heads = Column(c[2], n);
                        var froms = Array(c[3]); var tos = Array(c[4]);
                        int roads = bits.Count(b => b == '1');
                        if (froms.Length != roads || tos.Length != roads) throw new FormatException("Road range length.");
                        int r = 0; string previousHead = ""; int previousTo = -1;
                        for (int i = 0; i < n; i++)
                        {
                            var head = heads[i] as string ?? throw new FormatException("Road head is not a string.");
                            if (bits[i] == '0') { result[i] = head; continue; }
                            if (bits[i] != '1') throw new FormatException("Bad bit.");
                            int to = Int(tos[r]);
                            int from = froms[r] == null ? (r > 0 && head == previousHead ? previousTo : throw new FormatException("Unchained road range.")) : Int(froms[r]);
                            if (from < 0 || to < 0) throw new FormatException("Negative road node.");
                            result[i] = head + ":" + from.ToString(CultureInfo.InvariantCulture) + ":" + to.ToString(CultureInfo.InvariantCulture);
                            previousHead = head; previousTo = to; r++;
                        }
                        return result;
                    }
                    default: throw new FormatException("Unknown column tag " + tag);
                }
            }

            private static void Count(object?[] column, int length)
            {
                if (column.Length != length) throw new FormatException("Column length " + column.Length + ", expected " + length);
            }

            private object?[] Objects(object?[] c, int n)
            {
                var rowShapes = new int[n];
                if (c.Length < 2) throw new FormatException("Object column without shapes.");
                if (c[1] is object?[] perRow)
                {
                    if (perRow.Length != n) throw new FormatException("Shape count.");
                    for (int i = 0; i < n; i++) rowShapes[i] = Index(perRow[i], shapes.Length);
                    if (rowShapes.Distinct().Count() < 2) throw new FormatException("Non-canonical shape list.");
                }
                else { int s = Index(c[1], shapes.Length); for (int i = 0; i < n; i++) rowShapes[i] = s; }
                var rows = new object?[n];
                var union = new List<string>(); var seen = new HashSet<int>();
                for (int i = 0; i < n; i++)
                {
                    var keys = shapes[rowShapes[i]];
                    rows[i] = new WireObject(keys, new object?[keys.Length]);
                    if (seen.Add(rowShapes[i])) foreach (var k in keys) if (!union.Contains(k)) union.Add(k);
                }
                if (c.Length != union.Count + 2) throw new FormatException("Object column field count.");
                var at = seen.ToDictionary(s => s, s => union.Select(k => System.Array.IndexOf(shapes[s], k)).ToArray());
                for (int u = 0; u < union.Count; u++)
                {
                    int m = 0;
                    for (int i = 0; i < n; i++) if (at[rowShapes[i]][u] >= 0) m++;
                    var values = Column(c[u + 2], m);
                    for (int i = 0, j = 0; i < n; i++) { int position = at[rowShapes[i]][u]; if (position >= 0) ((WireObject)rows[i]!).Values[position] = values[j++]; }
                }
                return rows;
            }

            private object?[] Arrays(object?[] c, int n, bool positional)
            {
                if (c.Length < 2) throw new FormatException("Array column without lengths.");
                var lengths = new int[n];
                if (c[1] is object?[] perRow)
                {
                    if (perRow.Length != n) throw new FormatException("Length count.");
                    for (int i = 0; i < n; i++) { lengths[i] = Int(perRow[i]); if (lengths[i] < 0) throw new FormatException("Negative length."); }
                    if (lengths.Distinct().Count() < 2) throw new FormatException("Non-canonical length list.");
                }
                else { int l = Int(c[1]); if (l < 0) throw new FormatException("Negative length."); for (int i = 0; i < n; i++) lengths[i] = l; }
                var result = new object?[n];
                if (positional)
                {
                    int longest = lengths.Max();
                    Count(c, longest + 2);
                    for (int i = 0; i < n; i++) result[i] = new object?[lengths[i]];
                    for (int slot = 0; slot < longest; slot++)
                    {
                        var values = Column(c[slot + 2], lengths.Count(l => l > slot));
                        for (int i = 0, j = 0; i < n; i++) if (lengths[i] > slot) ((object?[])result[i]!)[slot] = values[j++];
                    }
                    return result;
                }
                Count(c, 3);
                long total = lengths.Sum(l => (long)l);
                if (total > int.MaxValue) throw new FormatException("Flattened column too large.");
                var flat = Column(c[2], (int)total);
                for (int i = 0, at = 0; i < n; i++)
                {
                    var a = new object?[lengths[i]];
                    System.Array.Copy(flat, at, a, 0, a.Length); at += a.Length;
                    result[i] = a;
                }
                return result;
            }
        }
    }

    /// <summary>Zigzag varints in a 64-symbol JSON-safe alphabet: five data bits per character, bit 5 = more follows.</summary>
    internal static class Varint
    {
        private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz-_";
        private static readonly sbyte[] Values = BuildValues();

        private static sbyte[] BuildValues()
        {
            var values = new sbyte[128];
            for (int i = 0; i < values.Length; i++) values[i] = -1;
            for (int i = 0; i < Alphabet.Length; i++) values[Alphabet[i]] = (sbyte)i;
            return values;
        }

        /// <summary>True only when <paramref name="text"/> is exactly long.ToString(InvariantCulture) of its value.</summary>
        public static bool TryCanonical(string text, out long value)
        {
            value = 0;
            if (text.Length == 0 || text.Length > 20) return false;
            int start = text[0] == '-' ? 1 : 0;
            if (start == text.Length || (text[start] == '0' && text.Length > start + 1) || (start == 1 && text == "-0")) return false;
            for (int i = start; i < text.Length; i++) if (text[i] < '0' || text[i] > '9') return false;
            return long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value)
                && value.ToString(CultureInfo.InvariantCulture) == text;
        }

        public static string Pack(long[] numbers, bool delta)
        {
            var builder = new StringBuilder(numbers.Length * 2);
            long previous = 0;
            foreach (long number in numbers)
            {
                long value = delta ? unchecked(number - previous) : number;
                previous = number;
                ulong zigzag = unchecked((ulong)((value << 1) ^ (value >> 63)));
                do
                {
                    int bits = (int)(zigzag & 31); zigzag >>= 5;
                    builder.Append(Alphabet[zigzag != 0 ? bits | 32 : bits]);
                } while (zigzag != 0);
            }
            return builder.ToString();
        }

        public static long[] Unpack(string packed, int count, bool delta)
        {
            var numbers = new long[count];
            int at = 0; long previous = 0;
            for (int i = 0; i < count; i++)
            {
                ulong zigzag = 0; int shift = 0;
                while (true)
                {
                    if (at >= packed.Length) throw new FormatException("Truncated packed column.");
                    char c = packed[at++];
                    int v = c < 128 ? Values[c] : -1;
                    if (v < 0) throw new FormatException("Bad packed character.");
                    if (shift > 60 || (shift == 60 && (v & 31) > 15)) throw new FormatException("Packed value overflows 64 bits.");
                    zigzag |= (ulong)(v & 31) << shift; shift += 5;
                    if ((v & 32) == 0) { if (v == 0 && shift > 5) throw new FormatException("Non-canonical packed value."); break; }
                }
                long value = unchecked((long)(zigzag >> 1) ^ -(long)(zigzag & 1));
                numbers[i] = delta ? unchecked(previous + value) : value;
                previous = numbers[i];
            }
            if (at != packed.Length) throw new FormatException("Surplus packed characters.");
            return numbers;
        }
    }
}
