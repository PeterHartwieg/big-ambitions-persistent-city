using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using PersistentCity.Core;

namespace PersistentCity.Persistence
{
    /// <summary>
    /// F7d compact wire (writer 34): candidate C of the F7d compact-format decision, columnar compact JSON with a per-save
    /// shared route table and chained road-tag ranges. The save string is one JSON object
    /// <c>{"Schema":34,"f":"pcc-c","v":1,"k":[keys],"h":[shapes],"s":[strings],"n":routes,"r":routeColumn,"t":rootColumn}</c>.
    /// The leading Schema field is the only addition to the prototype grammar: every reader scans the top-level Schema
    /// first, so an older build reports this payload as Future and preserves it.
    ///
    /// The encoded tree is the writer-33 body shape of the same snapshot, with Schema 34: the compact writer changes the
    /// encoding, not the entity semantics. The reader decodes the columns strictly, writes the body back out and then runs
    /// the full strict body reader (shape, caps, semantic validation), so a compact city is accepted exactly when its body
    /// would be. Strings decoded through the string table are one instance each, and the materialized city interns every
    /// string value on deserialization.
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
    ///   [8, ref|null...]     shared route table references (fields Journey and Route only)
    ///   [9, "0101", column, [from|null...], [to...]]  native road tags split into head and node range (field ServiceId)
    ///   [10, len|[len...], column...]     positional arrays: column i holds slot i of every row longer than i
    ///   [11, mode, "packed"] canonical integers as zigzag base-64 varints (mode 0 values, mode 1 deltas)
    ///   [12, mode, "packed"] the same for integral doubles spelled "N.0"
    /// </summary>
    public static partial class SaveCodec
    {
        public const string CompactFormat = "pcc-c";
        public const int CompactFormatVersion = 1;
        /// <summary>Open containers in the compact string; equal to the body reader's JSON depth, so an older reader can
        /// still scan the top-level Schema and report Future.</summary>
        public const int MaxCompactNesting = 48;
        /// <summary>Decoded column slots (one per value a column holds, nullable and mixed columns charged again for
        /// their inner column). The writer refuses above it and the reader refuses before allocating past it.</summary>
        public const int MaxCompactValues = 64 * 1024 * 1024;
        internal static readonly string[] CompactEnvelope = { "Schema", "f", "v", "k", "h", "s", "n", "r", "t" };
        internal static readonly string[] CompactRouteFields = { "Journey", "Route" };
        internal const string CompactRoadField = "ServiceId";
        private const string CompactRoadPrefix = "native:road:";

        /// <summary>Measured parts of one compact encode, reported by tests and the F7d measurement harness.</summary>
        internal sealed class CompactStats
        {
            public int Strings, Keys, Shapes, Routes, RouteReferences, RoadTags, Nesting;
            public long Values;
            public int TableEntries => Strings + Keys + Shapes + Routes;
        }

        /// <summary>Encode a writer body (the JSON the body codec produced for the snapshot) as the compact wire. The body
        /// already passed validation and both size caps; this adds the compact string's own caps and the table records.</summary>
        internal static string EncodeCompact(string body, int records, CompactStats? stats = null)
        {
            var tree = CompactTree.Parse(body, MaxCompactNesting, canonical: true);
            Require(tree is CompactObject, "Compact root is not an object.");
            stats ??= new CompactStats();
            string payload = new CompactEncoder(stats).Run(tree, body.Length / 3 + 64);
            Require(stats.Values <= MaxCompactValues, "Compact state exceeds its decoded value bound.");
            Require((long)records + stats.TableEntries <= MaxEmbeddedRecords, "Too many embedded records with the compact tables charged.");
            Require(payload.Length <= MaxInputCharacters, "Serialized state exceeds the input size limit.");
            Require(Encoding.UTF8.GetByteCount(payload) <= MaxInputCharacters, "Serialized state exceeds the UTF-8 size limit.");
            stats.Nesting = CompactTree.Nesting(payload);
            Require(stats.Nesting <= MaxCompactNesting, "Compact state nests too deeply.");
            return payload;
        }

        /// <summary>Strict compact read: envelope, tables and columns, then the body reader on the decoded body.</summary>
        private static SaveReadResult ReadCompact(string source)
        {
            Require(Encoding.UTF8.GetByteCount(source) <= MaxInputCharacters, "State exceeds the UTF-8 size limit.");
            var decoded = new CompactDecoder().Run(source);
            string body = CompactTree.Write(decoded.Root, (int)Math.Min(MaxInputCharacters, 1024L + source.Length * 4L), MaxInputCharacters);
            return ReadExact(body, CityState.CompactSchema, decoded.TableEntries, intern: true);
        }

        /// <summary>The body a compact payload carries (the writer-33 shape with Schema 34). Tests and diagnostics only;
        /// never a save string.</summary>
        internal static string ExpandCompact(string payload)
        {
            Require(payload != null && payload.Length <= MaxInputCharacters, "State exceeds the input size limit.");
            var decoded = new CompactDecoder().Run(payload!);
            return CompactTree.Write(decoded.Root, (int)Math.Min(MaxInputCharacters, 1024L + payload!.Length * 4L), MaxInputCharacters);
        }

        /// <summary>Compact encoding of an arbitrary body, without validation. Tests use it to build hostile payloads
        /// from an edited body; the reader still decides what is accepted.</summary>
        internal static string CompactFromBody(string body, CompactStats? stats = null)
            => new CompactEncoder(stats ?? new CompactStats()).Run(CompactTree.Parse(body, int.MaxValue, canonical: false), body.Length / 3 + 64);

        internal static int CompactTableEntries(string payload) => new CompactDecoder().Run(payload).TableEntries;

        /// <summary>The exact start of every writer-34 string; a match skips the generic top-level schema scan.</summary>
        internal static readonly string CompactPrefix = "{\"Schema\":" + CityState.CompactSchema.ToString(CultureInfo.InvariantCulture) + ",\"f\":";

        /// <summary>The early authority check for a compact payload: only the envelope, the tables and the root's first
        /// columns (Schema, Capabilities, MenuPolicy) are decoded; everything after them is never parsed.</summary>
        private static void RequireCompactCapabilities(string source)
        {
            var parser = new CompactTree.Parser(source, MaxCompactNesting, canonical: true, internKeys: false);
            parser.Expect('{');
            var header = new object?[6];
            for (int i = 0; i < CompactEnvelope.Length; i++)
            {
                if (i > 0) parser.Expect(',');
                Require(parser.String() == CompactEnvelope[i], "Unknown, missing or reordered compact fields.");
                parser.Expect(':');
                if (i == 6 || i == 7) { parser.SkipValue(1); continue; } // the route count and table never hold a header
                if (i < 6) { header[i] = parser.Value(1); continue; }
                var decoder = new CompactDecoder();
                decoder.Header(header);
                parser.Expect('['); Require(CompactDecoder.Int(parser.Value(2)) == 4, "Compact root is not an object.");
                parser.Expect(',');
                var shape = decoder.Shape(CompactDecoder.Index(parser.Value(2), decoder.ShapeCount));
                Require(shape.Length >= 3 && shape[0] == "Schema" && shape[1] == "Capabilities" && shape[2] == "MenuPolicy", "Missing population capabilities.");
                var values = new object?[3];
                for (int field = 0; field < 3; field++) { parser.Expect(','); values[field] = decoder.Column(parser.Value(2), 1, shape[field], false)[0]; }
                Require(values[0] is CompactNumber schema && schema.Text == CityState.CompactSchema.ToString(CultureInfo.InvariantCulture), "Unsupported population schema; preserve the payload.");
                var capabilities = values[1] as CompactObject;
                int revisionValue = 0;
                bool valid = capabilities != null && capabilities.Keys.Length == 3 && capabilities.Keys[0] == "Active" && capabilities.Keys[1] == "Code" && capabilities.Keys[2] == "Revision"
                    && capabilities.Values[0] is bool && capabilities.Values[1] is string code && code.Length <= 512
                    && capabilities.Values[2] is CompactNumber revision && TokenText.CanonicalUnits(revision.Text)
                    && int.TryParse(revision.Text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out revisionValue);
                Require(valid, "Invalid population capabilities.");
                ValidateCapabilities(new PopulationCapabilities { Active = (bool)capabilities!.Values[0]!, Code = (string)capabilities.Values[1]!, Revision = revisionValue });
                Require(values[2] is string policy && policy == FoodNameIdentity.Wire, "Unsupported menu product policy.");
                return; // Full population startup still decodes every column and the whole body.
            }
        }

        private static bool CompactRouteField(string key) => key == "Journey" || key == "Route";

        /// <summary>Intern on decode: every string value the body reader materializes for one compact read is one
        /// instance per distinct text. Read only; validation already ran on the token stream.</summary>
        private sealed class InternConverter : JsonConverter
        {
            private readonly Dictionary<string, string> pool = new Dictionary<string, string>(StringComparer.Ordinal);
            public override bool CanRead => true;
            public override bool CanWrite => false;
            public override bool CanConvert(Type objectType) => objectType == typeof(string);
            public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null) return null;
                Require(reader.TokenType == JsonToken.String, "Expected a string.");
                string value = (string)reader.Value!;
                if (pool.TryGetValue(value, out var shared)) return shared;
                pool.Add(value, value);
                return value;
            }
            public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) => throw new NotSupportedException();
        }

        /// <summary>native:road:(v1|fragment1):topology:from:to with canonical non-negative node indices, else false.</summary>
        internal static bool TrySplitRoad(string value, out string head, out int from, out int to)
        {
            head = ""; from = to = 0;
            if (!value.StartsWith(CompactRoadPrefix, StringComparison.Ordinal)) return false;
            string[] parts = value.Split(':');
            if (parts.Length != 6 || (parts[2] != "v1" && parts[2] != "fragment1")) return false;
            if (!int.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out from)
                || !int.TryParse(parts[5], NumberStyles.None, CultureInfo.InvariantCulture, out to)) return false;
            if (from.ToString(CultureInfo.InvariantCulture) != parts[4] || to.ToString(CultureInfo.InvariantCulture) != parts[5]) return false;
            head = value.Substring(0, value.Length - parts[4].Length - parts[5].Length - 2);
            return true;
        }

        private static string ShapeKey(string[] keys) => string.Join("\u0000", keys);

        private static void AppendInt(StringBuilder output, long value) => output.Append(value.ToString(CultureInfo.InvariantCulture));

        // ---- Tree ----

        /// <summary>A JSON number kept as its exact token text, so no integer or double is ever reformatted.</summary>
        internal sealed class CompactNumber
        {
            public CompactNumber(string text) { Text = text; }
            public string Text { get; }
        }

        /// <summary>A JSON object with its exact key order. Objects of one shape share one key array.</summary>
        internal sealed class CompactObject
        {
            public CompactObject(string[] keys, object?[] values) { Keys = keys; Values = values; }
            public string[] Keys { get; }
            public object?[] Values { get; }
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        /// <summary>
        /// Exact JSON tree: values are null, a boxed bool, <see cref="CompactNumber"/>, string, <see cref="CompactObject"/>
        /// or object?[]. The canonical parser refuses whitespace and any string spelling the writer would not produce.
        /// </summary>
        internal static class CompactTree
        {
            public static readonly object True = true, False = false;

            public static object? Parse(string json, int maxNesting, bool canonical)
            {
                var parser = new Parser(json, maxNesting, canonical, internKeys: true);
                object? value = parser.Value(0);
                Require(parser.Position == json.Length, "Trailing characters after the compact state.");
                return value;
            }

            /// <summary>Open containers in a canonical compact string (no whitespace; strings skipped).</summary>
            public static int Nesting(string json)
            {
                int depth = 0, max = 0;
                for (int i = 0; i < json.Length; i++)
                {
                    char c = json[i];
                    if (c == '"') { for (i++; json[i] != '"'; i++) if (json[i] == '\\') i++; continue; }
                    if (c == '[' || c == '{') { if (++depth > max) max = depth; }
                    else if (c == ']' || c == '}') depth--;
                }
                return max;
            }

            /// <summary>Writer spelling (no whitespace, the body serializer's string escaping, number text unchanged).
            /// Refuses once the text passes <paramref name="limit"/> characters, before allocating past it.</summary>
            public static string Write(object? root, int capacity, int limit)
            {
                var output = new StringBuilder(Math.Min(capacity, limit));
                Write(output, root, limit);
                Require(output.Length <= limit, "Serialized state exceeds the input size limit.");
                return output.ToString();
            }

            private static void Write(StringBuilder output, object? value, int limit)
            {
                if (output.Length > limit) Fail("Serialized state exceeds the input size limit.");
                switch (value)
                {
                    case null: output.Append("null"); return;
                    case bool b: output.Append(b ? "true" : "false"); return;
                    case CompactNumber n: output.Append(n.Text); return;
                    case string s: WriteString(output, s); return;
                    case CompactObject o:
                        output.Append('{');
                        for (int i = 0; i < o.Keys.Length; i++)
                        {
                            if (i > 0) output.Append(',');
                            WriteString(output, o.Keys[i]); output.Append(':'); Write(output, o.Values[i], limit);
                        }
                        output.Append('}');
                        return;
                    case object?[] a:
                        output.Append('[');
                        for (int i = 0; i < a.Length; i++) { if (i > 0) output.Append(','); Write(output, a[i], limit); }
                        output.Append(']');
                        return;
                    default: throw new InvalidOperationException("Unknown compact value " + value.GetType().Name);
                }
            }

            /// <summary>Newtonsoft StringEscapeHandling.Default for double-quoted strings (the body writer's escaping).</summary>
            public static void WriteString(StringBuilder output, string value)
            {
                output.Append('"');
                int start = 0;
                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    string? escape = Escape(c);
                    if (escape == null && c >= ' ') continue;
                    output.Append(value, start, i - start);
                    if (escape != null) output.Append(escape);
                    else output.Append("\\u00").Append(Hex(c >> 4)).Append(Hex(c & 15));
                    start = i + 1;
                }
                output.Append(value, start, value.Length - start).Append('"');
            }

            private static string? Escape(char c) => c switch
            {
                '"' => "\\\"", '\\' => "\\\\", '\n' => "\\n", '\r' => "\\r", '\t' => "\\t", '\b' => "\\b", '\f' => "\\f",
                '\u0085' => "\\u0085", (char)0x2028 => "\\u2028", (char)0x2029 => "\\u2029",
                _ => null,
            };

            private static bool NeedsEscape(char c) => c < ' ' || c == '"' || c == '\\' || c == '\u0085' || c == (char)0x2028 || c == (char)0x2029;

            private static char Hex(int n) => (char)(n <= 9 ? '0' + n : 'a' + n - 10);

            internal sealed class Parser
            {
                private readonly string text;
                private readonly int maxNesting;
                private readonly bool canonical;
                private readonly Dictionary<string, string>? keys;
                // Objects with the same key sequence share one key array, so shapes are found by reference.
                private readonly Dictionary<string, List<string[]>>? shapes;
                public int Position;

                public Parser(string text, int maxNesting, bool canonical, bool internKeys)
                {
                    this.text = text; this.maxNesting = maxNesting; this.canonical = canonical;
                    if (internKeys) { keys = new Dictionary<string, string>(StringComparer.Ordinal); shapes = new Dictionary<string, List<string[]>>(StringComparer.Ordinal); }
                }

                private char Peek()
                {
                    if (Position >= text.Length) Fail("Truncated compact state.");
                    return text[Position];
                }

                public void Expect(char c)
                {
                    if (!canonical) SkipSpace();
                    if (Peek() != c) Fail("Expected '" + c + "' at " + Position.ToString(CultureInfo.InvariantCulture) + ".");
                    Position++;
                }

                private void SkipSpace()
                {
                    while (Position < text.Length && (text[Position] == ' ' || text[Position] == '\t' || text[Position] == '\n' || text[Position] == '\r')) Position++;
                }

                public object? Value(int depth)
                {
                    if (!canonical) SkipSpace();
                    char c = Peek();
                    switch (c)
                    {
                        case '{': Open(depth); return Object(depth);
                        case '[': Open(depth); return Array(depth);
                        case '"': return String();
                        case 't': Literal("true"); return True;
                        case 'f': Literal("false"); return False;
                        case 'n': Literal("null"); return null;
                        default:
                            if (c == '-' || (c >= '0' && c <= '9')) return new CompactNumber(Number());
                            Fail("Unexpected character at " + Position.ToString(CultureInfo.InvariantCulture) + "."); return null;
                    }
                }

                /// <summary>Syntax check of one value without building it.</summary>
                public void SkipValue(int depth)
                {
                    char c = Peek();
                    switch (c)
                    {
                        case '{':
                        case '[':
                            Open(depth); Position++;
                            char close = c == '{' ? '}' : ']';
                            if (Peek() == close) { Position++; return; }
                            while (true)
                            {
                                if (c == '{') { String(); Expect(':'); }
                                SkipValue(depth + 1);
                                if (Peek() == ',') { Position++; continue; }
                                Expect(close); return;
                            }
                        case '"': String(); return;
                        default: Value(depth); return;
                    }
                }

                private void Open(int depth) { if (depth + 1 > maxNesting) Fail("Compact state nests too deeply."); }

                private void Literal(string word)
                {
                    if (Position + word.Length > text.Length || string.CompareOrdinal(text, Position, word, 0, word.Length) != 0) Fail("Bad literal.");
                    Position += word.Length;
                }

                private static bool Digit(char c) => c >= '0' && c <= '9';

                private string Number()
                {
                    int start = Position;
                    if (text[Position] == '-') Position++;
                    if (Position >= text.Length || !Digit(text[Position])) Fail("Bad number.");
                    if (text[Position] == '0') Position++; else while (Position < text.Length && Digit(text[Position])) Position++;
                    if (Position < text.Length && text[Position] == '.')
                    {
                        Position++;
                        if (Position >= text.Length || !Digit(text[Position])) Fail("Bad fraction.");
                        while (Position < text.Length && Digit(text[Position])) Position++;
                    }
                    if (Position < text.Length && (text[Position] == 'e' || text[Position] == 'E'))
                    {
                        Position++;
                        if (Position < text.Length && (text[Position] == '+' || text[Position] == '-')) Position++;
                        if (Position >= text.Length || !Digit(text[Position])) Fail("Bad exponent.");
                        while (Position < text.Length && Digit(text[Position])) Position++;
                    }
                    return text.Substring(start, Position - start);
                }

                public string String()
                {
                    if (!canonical) SkipSpace();
                    if (Peek() != '"') Fail("Expected a string.");
                    int open = Position++;
                    int start = Position;
                    while (true)
                    {
                        char c = Peek();
                        if (c == '"') { string plain = text.Substring(start, Position - start); Position++; return plain; }
                        if (c == '\\') break;
                        if (c < ' ') Fail("Control character in a string.");
                        if (canonical && NeedsEscape(c)) Fail("Noncanonical string encoding.");
                        Position++;
                    }
                    var builder = new StringBuilder(text, start, Position - start, Position - start + 16);
                    while (true)
                    {
                        char c = Peek(); Position++;
                        if (c == '"') break;
                        if (c < ' ') Fail("Control character in a string.");
                        if (c != '\\') { builder.Append(c); continue; }
                        char e = Peek(); Position++;
                        switch (e)
                        {
                            case '"': builder.Append('"'); break;
                            case '\\': builder.Append('\\'); break;
                            case '/': builder.Append('/'); break;
                            case 'b': builder.Append('\b'); break;
                            case 'f': builder.Append('\f'); break;
                            case 'n': builder.Append('\n'); break;
                            case 'r': builder.Append('\r'); break;
                            case 't': builder.Append('\t'); break;
                            case 'u':
                            {
                                ushort code = 0;
                                if (Position + 4 > text.Length || !ushort.TryParse(text.AsSpan(Position, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out code))
                                    Fail("Bad unicode escape.");
                                builder.Append((char)code); Position += 4; break;
                            }
                            default: Fail("Bad escape."); break;
                        }
                    }
                    string value = builder.ToString();
                    if (canonical)
                    {
                        // The writer's escaping is the only spelling accepted, so decode -> re-encode stays byte-identical.
                        var spelled = new StringBuilder(Position - open);
                        WriteString(spelled, value);
                        if (spelled.Length != Position - open || string.CompareOrdinal(spelled.ToString(), 0, text, open, Position - open) != 0) Fail("Noncanonical string encoding.");
                    }
                    return value;
                }

                private CompactObject Object(int depth)
                {
                    Position++;
                    var names = new List<string>(); var values = new List<object?>();
                    if (!canonical) SkipSpace();
                    if (Peek() == '}') { Position++; return new CompactObject(SharedKeys(names), values.ToArray()); }
                    while (true)
                    {
                        string name = String();
                        if (keys != null) { if (keys.TryGetValue(name, out var shared)) name = shared; else keys.Add(name, name); }
                        Expect(':');
                        names.Add(name); values.Add(Value(depth + 1));
                        if (!canonical) SkipSpace();
                        if (Peek() == ',') { Position++; continue; }
                        Expect('}');
                        return new CompactObject(SharedKeys(names), values.ToArray());
                    }
                }

                private string[] SharedKeys(List<string> names)
                {
                    if (shapes == null) return names.ToArray();
                    string first = names.Count == 0 ? "" : names[0];
                    if (!shapes.TryGetValue(first, out var known)) shapes.Add(first, known = new List<string[]>());
                    foreach (var candidate in known)
                    {
                        if (candidate.Length != names.Count) continue;
                        bool same = true;
                        for (int i = 0; i < candidate.Length && same; i++) same = ReferenceEquals(candidate[i], names[i]);
                        if (same) return candidate;
                    }
                    var array = names.ToArray();
                    known.Add(array);
                    return array;
                }

                private object?[] Array(int depth)
                {
                    Position++;
                    if (!canonical) SkipSpace();
                    if (Peek() == ']') { Position++; return System.Array.Empty<object?>(); }
                    var values = new List<object?>();
                    while (true)
                    {
                        values.Add(Value(depth + 1));
                        if (!canonical) SkipSpace();
                        if (Peek() == ',') { Position++; continue; }
                        Expect(']');
                        return values.ToArray();
                    }
                }
            }
        }

        // ---- Encoder ----

        private sealed class CompactEncoder
        {
            private readonly CompactStats stats;
            private readonly Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly Dictionary<string, int> strings = new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly Dictionary<string, int> keys = new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly Dictionary<object, string> joinedByArray = new Dictionary<object, string>(ReferenceComparer.Instance);
            private readonly Dictionary<string, string[]> shapeByJoined = new Dictionary<string, string[]>(StringComparer.Ordinal);
            private readonly Dictionary<object, int> shapeOfArray = new Dictionary<object, int>(ReferenceComparer.Instance);
            private readonly Dictionary<object, string> routeContent = new Dictionary<object, string>(ReferenceComparer.Instance);
            private readonly Dictionary<string, object> uniqueRoutes = new Dictionary<string, object>(StringComparer.Ordinal);
            private readonly Dictionary<string, int> routeIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            private StringBuilder output = new StringBuilder();
            private bool insideRoute;

            public CompactEncoder(CompactStats stats) { this.stats = stats; }

            public string Run(object? root, int capacity)
            {
                Collect(root, "");
                var stringTable = counts.Where(p => p.Value >= 2).Select(p => p.Key).OrderBy(s => s, StringComparer.Ordinal).ToArray();
                for (int i = 0; i < stringTable.Length; i++) strings.Add(stringTable[i], i);
                var keyTable = keys.Keys.OrderBy(s => s, StringComparer.Ordinal).ToArray();
                for (int i = 0; i < keyTable.Length; i++) keys[keyTable[i]] = i;
                var shapeTable = shapeByJoined.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Value).ToArray();
                var shapeIndex = new Dictionary<string, int>(StringComparer.Ordinal);
                for (int i = 0; i < shapeTable.Length; i++) shapeIndex.Add(ShapeKey(shapeTable[i]), i);
                foreach (var pair in joinedByArray) shapeOfArray.Add(pair.Key, shapeIndex[pair.Value]);
                var routeTable = uniqueRoutes.OrderBy(p => p.Key, StringComparer.Ordinal).ToArray();
                for (int i = 0; i < routeTable.Length; i++) routeIndex.Add(routeTable[i].Key, i);
                stats.Strings = stringTable.Length; stats.Keys = keyTable.Length; stats.Shapes = shapeTable.Length; stats.Routes = routeTable.Length;
                // The tables are bounded before any column is written.
                Require(stats.TableEntries <= MaxEmbeddedRecords, "Compact tables exceed the record bound.");

                output = new StringBuilder(capacity);
                output.Append("{\"Schema\":"); AppendInt(output, CityState.CompactSchema);
                output.Append(",\"f\":"); CompactTree.WriteString(output, CompactFormat);
                output.Append(",\"v\":"); AppendInt(output, CompactFormatVersion);
                output.Append(",\"k\":[");
                for (int i = 0; i < keyTable.Length; i++) { if (i > 0) output.Append(','); CompactTree.WriteString(output, keyTable[i]); }
                output.Append("],\"h\":[");
                for (int i = 0; i < shapeTable.Length; i++)
                {
                    if (i > 0) output.Append(',');
                    output.Append('[');
                    for (int j = 0; j < shapeTable[i].Length; j++) { if (j > 0) output.Append(','); AppendInt(output, keys[shapeTable[i][j]]); }
                    output.Append(']');
                }
                output.Append("],\"s\":[");
                for (int i = 0; i < stringTable.Length; i++) { if (i > 0) output.Append(','); CompactTree.WriteString(output, stringTable[i]); }
                output.Append("],\"n\":"); AppendInt(output, routeTable.Length);
                output.Append(",\"r\":");
                insideRoute = true;
                WriteColumn(routeTable.Select(p => p.Value).ToList<object?>(), "");
                insideRoute = false;
                output.Append(",\"t\":");
                WriteColumn(new List<object?> { root }, "");
                output.Append('}');
                return output.ToString();
            }

            private void Collect(object? value, string key)
            {
                switch (value)
                {
                    case string s:
                        // Count exactly the text the column writer emits: a road tag's head, otherwise the whole string.
                        if (key == CompactRoadField && TrySplitRoad(s, out var head, out _, out _)) s = head;
                        counts[s] = counts.TryGetValue(s, out int c) ? c + 1 : 1;
                        return;
                    case CompactObject o:
                        Joined(o.Keys);
                        for (int i = 0; i < o.Keys.Length; i++)
                        {
                            keys[o.Keys[i]] = 0;
                            var child = o.Values[i];
                            if (CompactRouteField(o.Keys[i]) && (child is CompactObject || child is object?[]))
                            {
                                // A route holds no route of its own in any supported entity; refusing one keeps the
                                // route table decodable before every reference into it.
                                Require(!insideRoute, "A committed route cannot contain another route.");
                                RegisterRoute(child!);
                            }
                            else Collect(child, o.Keys[i]);
                        }
                        return;
                    case object?[] a: foreach (var v in a) Collect(v, key); return;
                }
            }

            private void Joined(string[] array)
            {
                if (joinedByArray.ContainsKey(array)) return;
                string joined = ShapeKey(array);
                joinedByArray.Add(array, joined);
                if (!shapeByJoined.ContainsKey(joined)) shapeByJoined.Add(joined, array);
            }

            private void RegisterRoute(object route)
            {
                if (routeContent.ContainsKey(route)) return;
                string content = CompactTree.Write(route, 256, int.MaxValue);
                routeContent.Add(route, content);
                if (uniqueRoutes.ContainsKey(content)) return;
                uniqueRoutes.Add(content, route);
                insideRoute = true;
                Collect(route, "");
                insideRoute = false;
            }

            private void WriteColumn(List<object?> values, string key)
            {
                int n = values.Count;
                stats.Values += n;
                if (n == 0) { output.Append('0'); return; }
                int nulls = 0, bools = 0, numbers = 0, texts = 0, objects = 0, arrays = 0;
                foreach (var v in values)
                {
                    switch (v)
                    {
                        case null: nulls++; break; case bool _: bools++; break; case CompactNumber _: numbers++; break;
                        case string _: texts++; break; case CompactObject _: objects++; break; case object?[] _: arrays++; break;
                    }
                }
                int kinds = (bools > 0 ? 1 : 0) + (numbers > 0 ? 1 : 0) + (texts > 0 ? 1 : 0) + (objects > 0 ? 1 : 0) + (arrays > 0 ? 1 : 0);
                if (CompactRouteField(key) && texts + numbers + bools == 0 && objects + arrays > 0)
                {
                    output.Append("[8");
                    foreach (var v in values)
                    {
                        output.Append(',');
                        if (v == null) output.Append("null"); else { AppendInt(output, routeIndex[routeContent[v]]); stats.RouteReferences++; }
                    }
                    output.Append(']');
                    return;
                }
                if (nulls == n) { output.Append("[3,"); AppendInt(output, n); output.Append(']'); return; }
                if (nulls > 0 && kinds == 1)
                {
                    output.Append("[7,[");
                    var rest = new List<object?>(n - nulls);
                    bool first = true;
                    for (int i = 0; i < n; i++)
                    {
                        if (values[i] != null) { rest.Add(values[i]); continue; }
                        if (!first) output.Append(',');
                        AppendInt(output, i); first = false;
                    }
                    output.Append("],");
                    WriteColumn(rest, key);
                    output.Append(']');
                    return;
                }
                if (kinds > 1)
                {
                    output.Append("[6");
                    foreach (var v in values) { output.Append(','); WriteColumn(new List<object?>(1) { v }, key); }
                    output.Append(']');
                    return;
                }
                if (numbers == n && Packed(values)) return;
                if (numbers == n)
                {
                    output.Append("[0");
                    foreach (var v in values) output.Append(',').Append(((CompactNumber)v!).Text);
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
                if (texts == n) { Strings(values, key); return; }
                if (objects == n) { Objects(values); return; }
                Arrays(values, key);
            }

            /// <summary>An all-integer (or all "N.0") column as one varint string when that is shorter than the number list.
            /// Only tokens whose text is exactly the invariant spelling of a long qualify, so the decoder reproduces every
            /// token character for character.</summary>
            private bool Packed(List<object?> values)
            {
                int n = values.Count;
                var numbers = new long[n];
                bool integral = ((CompactNumber)values[0]!).Text.EndsWith(".0", StringComparison.Ordinal);
                long raw = n;
                for (int i = 0; i < n; i++)
                {
                    string text = ((CompactNumber)values[i]!).Text;
                    raw += text.Length;
                    if (integral != text.EndsWith(".0", StringComparison.Ordinal)) return false;
                    if (!Varint.TryCanonical(integral ? text.AsSpan(0, text.Length - 2) : text.AsSpan(), out numbers[i])) return false;
                }
                long plain = Varint.Length(numbers, false), delta = Varint.Length(numbers, true);
                bool useDelta = delta < plain;
                if (Math.Min(plain, delta) + 8 >= raw) return false;
                output.Append(integral ? "[12," : "[11,").Append(useDelta ? '1' : '0').Append(",\"");
                Varint.Append(output, numbers, useDelta);
                output.Append("\"]");
                return true;
            }

            private void Text(string s)
            {
                if (strings.TryGetValue(s, out int index)) AppendInt(output, index); else CompactTree.WriteString(output, s);
            }

            private void Strings(List<object?> values, string key)
            {
                if (key == CompactRoadField)
                {
                    var heads = new List<object?>(values.Count); var bitmap = new StringBuilder(values.Count);
                    var froms = new List<int>(); var tos = new List<int>(); var headOfRange = new List<string>();
                    foreach (var value in values)
                    {
                        string s = (string)value!;
                        if (TrySplitRoad(s, out var head, out int from, out int to)) { heads.Add(head); bitmap.Append('1'); froms.Add(from); tos.Add(to); headOfRange.Add(head); }
                        else { heads.Add(s); bitmap.Append('0'); }
                    }
                    if (froms.Count > 0)
                    {
                        stats.RoadTags += froms.Count;
                        output.Append("[9,\"").Append(bitmap).Append("\",");
                        stats.Values += heads.Count; // the reader decodes the heads as their own column
                        Strings(heads, "");
                        output.Append(",[");
                        for (int i = 0; i < froms.Count; i++)
                        {
                            if (i > 0) output.Append(',');
                            // A leg that starts where the previous road leg of the same topology ended is a range continuation.
                            if (i > 0 && froms[i] == tos[i - 1] && headOfRange[i] == headOfRange[i - 1]) output.Append("null");
                            else AppendInt(output, froms[i]);
                        }
                        output.Append("],[");
                        for (int i = 0; i < tos.Count; i++) { if (i > 0) output.Append(','); AppendInt(output, tos[i]); }
                        output.Append("]]");
                        return;
                    }
                }
                output.Append("[1");
                foreach (var v in values) { output.Append(','); Text((string)v!); }
                output.Append(']');
            }

            private void Objects(List<object?> values)
            {
                int n = values.Count;
                var rowShapes = new int[n];
                var union = new List<string>();
                var positions = new Dictionary<int, string[]>();
                for (int i = 0; i < n; i++)
                {
                    var o = (CompactObject)values[i]!;
                    rowShapes[i] = shapeOfArray[o.Keys];
                    if (positions.ContainsKey(rowShapes[i])) continue;
                    positions.Add(rowShapes[i], o.Keys);
                    foreach (var k in o.Keys) if (!union.Contains(k)) union.Add(k);
                }
                // Position of every union field in every distinct shape, so rows are not searched per field.
                var at = positions.ToDictionary(p => p.Key, p => union.Select(k => System.Array.IndexOf(p.Value, k)).ToArray());
                output.Append("[4,");
                if (positions.Count == 1) AppendInt(output, rowShapes[0]);
                else
                {
                    output.Append('[');
                    for (int i = 0; i < n; i++) { if (i > 0) output.Append(','); AppendInt(output, rowShapes[i]); }
                    output.Append(']');
                }
                for (int u = 0; u < union.Count; u++)
                {
                    var column = new List<object?>(n);
                    for (int i = 0; i < n; i++)
                    {
                        int position = at[rowShapes[i]][u];
                        if (position >= 0) column.Add(((CompactObject)values[i]!).Values[position]);
                    }
                    output.Append(',');
                    WriteColumn(column, union[u]);
                }
                output.Append(']');
            }

            private void Arrays(List<object?> values, string key)
            {
                var flat = new List<object?>();
                bool uniform = true; int length = ((object?[])values[0]!).Length, longest = 0;
                foreach (var v in values) { var a = (object?[])v!; if (a.Length != length) uniform = false; longest = Math.Max(longest, a.Length); flat.AddRange(a); }
                // Tuples (slots of different kinds, or numbers of different spellings) pack better slot by slot.
                bool positional = Kinds(flat) > 1 || NumberClasses(flat) > 1;
                output.Append(positional ? "[10," : "[5,");
                if (uniform) AppendInt(output, length);
                else
                {
                    output.Append('[');
                    for (int i = 0; i < values.Count; i++) { if (i > 0) output.Append(','); AppendInt(output, ((object?[])values[i]!).Length); }
                    output.Append(']');
                }
                if (positional)
                {
                    for (int slot = 0; slot < longest; slot++)
                    {
                        var column = new List<object?>(values.Count);
                        foreach (var v in values) { var a = (object?[])v!; if (a.Length > slot) column.Add(a[slot]); }
                        output.Append(',');
                        WriteColumn(column, key);
                    }
                    output.Append(']');
                    return;
                }
                output.Append(',');
                WriteColumn(flat, key);
                output.Append(']');
            }

            private static int NumberClasses(List<object?> values)
            {
                bool integer = false, integral = false, other = false;
                foreach (var v in values)
                {
                    if (!(v is CompactNumber number)) continue;
                    string text = number.Text;
                    if (Varint.TryCanonical(text.AsSpan(), out _)) integer = true;
                    else if (text.EndsWith(".0", StringComparison.Ordinal) && Varint.TryCanonical(text.AsSpan(0, text.Length - 2), out _)) integral = true;
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
                        case bool _: bools = true; break; case CompactNumber _: numbers = true; break; case string _: texts = true; break;
                        case CompactObject _: objects = true; break; case object?[] _: arrays = true; break;
                    }
                }
                return (bools ? 1 : 0) + (numbers ? 1 : 0) + (texts ? 1 : 0) + (objects ? 1 : 0) + (arrays ? 1 : 0);
            }
        }

        // ---- Decoder ----

        internal sealed class CompactDecoded
        {
            public CompactDecoded(object? root, int tableEntries) { Root = root; TableEntries = tableEntries; }
            public object? Root { get; }
            public int TableEntries { get; }
        }

        /// <summary>Strict compact decoder. Every count is checked against its bound before the column, table or row
        /// arrays it sizes are allocated; any malformed, unknown, dangling, duplicated or surplus part refuses.</summary>
        private sealed class CompactDecoder
        {
            private string[] strings = System.Array.Empty<string>();
            private string[][] shapes = System.Array.Empty<string[]>();
            private object?[] routes = System.Array.Empty<object?>();
            private bool[] referenced = System.Array.Empty<bool>();
            private bool decodingRoutes;
            private long values;
            private int tableEntries;

            public int ShapeCount => shapes.Length;
            public string[] Shape(int index) => shapes[index];

            public CompactDecoded Run(string payload)
            {
                Require(payload.Length <= MaxInputCharacters, "State exceeds the input size limit.");
                if (!(CompactTree.Parse(payload, MaxCompactNesting, canonical: true) is CompactObject top)) { Fail("Compact state is not an object."); return null!; }
                Require(top.Keys.Length == CompactEnvelope.Length && top.Keys.Zip(CompactEnvelope, (a, b) => string.Equals(a, b, StringComparison.Ordinal)).All(same => same), "Unknown, missing or reordered compact fields.");
                Header(top.Values);
                int count = Int(top.Values[6]);
                Require(count >= 0 && (long)tableEntries + count <= MaxEmbeddedRecords, "Compact route table exceeds the record bound.");
                tableEntries += count;
                decodingRoutes = true;
                routes = Column(top.Values[7], count, "", true);
                decodingRoutes = false;
                foreach (var route in routes) Require(route is CompactObject || route is object?[], "Compact route table holds a non-route value.");
                referenced = new bool[count];
                var root = Column(top.Values[8], 1, "", false)[0];
                Require(referenced.All(r => r), "Compact route table holds an unreferenced route.");
                Require(root is CompactObject, "Compact root is not an object.");
                return new CompactDecoded(root, tableEntries);
            }

            /// <summary>Schema, format, version and the key, shape and string tables (envelope slots 0 to 5).</summary>
            public void Header(object?[] header)
            {
                Require(header[0] is CompactNumber schema && schema.Text == CityState.CompactSchema.ToString(CultureInfo.InvariantCulture), "Unsupported compact state schema.");
                Require(header[1] is string format && format == CompactFormat, "Unknown compact format.");
                Require(Int(header[2]) == CompactFormatVersion, "Unsupported compact format version.");
                var keyTable = Table(header[3]);
                tableEntries = keyTable.Length;
                var keys = new string[keyTable.Length];
                for (int i = 0; i < keys.Length; i++)
                {
                    keys[i] = keyTable[i] as string ?? Unexpected<string>("Compact key is not a string.");
                    if (i > 0) Require(string.CompareOrdinal(keys[i - 1], keys[i]) < 0, "Compact key table is not ordinal or holds a duplicate.");
                }
                var shapeTable = Table(header[4]);
                tableEntries += shapeTable.Length;
                shapes = new string[shapeTable.Length][];
                var used = new bool[keys.Length];
                for (int i = 0; i < shapes.Length; i++)
                {
                    var fields = Array(shapeTable[i]);
                    var shape = new string[fields.Length];
                    for (int j = 0; j < shape.Length; j++) { int k = Index(fields[j], keys.Length); shape[j] = keys[k]; used[k] = true; }
                    Require(shape.Distinct(StringComparer.Ordinal).Count() == shape.Length, "Duplicate field in a compact shape.");
                    shapes[i] = shape;
                    if (i > 0) Require(string.CompareOrdinal(ShapeKey(shapes[i - 1]), ShapeKey(shape)) < 0, "Compact shape table is not ordinal or holds a duplicate.");
                }
                Require(used.All(u => u), "Compact key table holds an unused key.");
                var stringTable = Table(header[5]);
                tableEntries += stringTable.Length;
                strings = new string[stringTable.Length];
                for (int i = 0; i < strings.Length; i++)
                {
                    strings[i] = stringTable[i] as string ?? Unexpected<string>("Compact string table entry is not a string.");
                    if (i > 0) Require(string.CompareOrdinal(strings[i - 1], strings[i]) < 0, "Compact string table is not ordinal or holds a duplicate.");
                }
            }

            private object?[] Table(object? value)
            {
                var table = Array(value);
                Require((long)tableEntries + table.Length <= MaxEmbeddedRecords, "Compact tables exceed the record bound.");
                return table;
            }

            private static T Unexpected<T>(string message) { Fail(message); return default!; }

            private static object?[] Array(object? value) => value as object?[] ?? Unexpected<object?[]>("Expected a compact array.");

            internal static int Int(object? value)
            {
                int result = 0;
                if (!(value is CompactNumber n) || !int.TryParse(n.Text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out result)
                    || result.ToString(CultureInfo.InvariantCulture) != n.Text) Fail("Expected a canonical compact integer.");
                return result;
            }

            internal static int Index(object? value, int count)
            {
                int i = Int(value);
                Require(i >= 0 && i < count, "Compact table index out of range.");
                return i;
            }

            private void Charge(long n)
            {
                values += n;
                Require(values <= MaxCompactValues, "Compact state exceeds its decoded value bound.");
            }

            private void Room(long n) => Require(values + n <= MaxCompactValues, "Compact state exceeds its decoded value bound.");

            private static void Count(object?[] column, long length) => Require(column.Length == length, "Compact column length.");

            public object?[] Column(object? column, int n, string key, bool routeTable)
            {
                Require(n >= 0, "Negative compact column length.");
                Charge(n);
                if (n == 0)
                {
                    Require(column is CompactNumber zero && zero.Text == "0", "An empty compact column must be 0.");
                    return System.Array.Empty<object?>();
                }
                var c = Array(column);
                Require(c.Length > 0, "Untagged compact column.");
                int tag = Int(c[0]);
                switch (tag)
                {
                    case 0:
                    {
                        Count(c, (long)n + 1);
                        var result = new object?[n];
                        for (int i = 0; i < n; i++) result[i] = c[i + 1] as CompactNumber ?? Unexpected<CompactNumber>("Expected a number.");
                        return result;
                    }
                    case 11:
                    case 12:
                    {
                        Count(c, 3);
                        int mode = Int(c[1]);
                        Require(mode == 0 || mode == 1, "Unknown compact packing mode.");
                        var numbers = Varint.Unpack(c[2] as string ?? Unexpected<string>("Expected packed text."), n, mode == 1);
                        var result = new object?[n];
                        for (int i = 0; i < n; i++)
                            result[i] = new CompactNumber(tag == 12 ? numbers[i].ToString(CultureInfo.InvariantCulture) + ".0" : numbers[i].ToString(CultureInfo.InvariantCulture));
                        return result;
                    }
                    case 1:
                    {
                        Count(c, (long)n + 1);
                        var result = new object?[n];
                        for (int i = 0; i < n; i++) result[i] = c[i + 1] is string s ? s : strings[Index(c[i + 1], strings.Length)];
                        return result;
                    }
                    case 2:
                    {
                        Count(c, 2);
                        var bits = c[1] as string ?? Unexpected<string>("Expected a bitmap.");
                        Require(bits.Length == n, "Compact bitmap length.");
                        var result = new object?[n];
                        for (int i = 0; i < n; i++) result[i] = bits[i] == '1' ? CompactTree.True : bits[i] == '0' ? CompactTree.False : Unexpected<object>("Bad compact bit.");
                        return result;
                    }
                    case 3:
                        Count(c, 2);
                        Require(Int(c[1]) == n, "Compact null count.");
                        return new object?[n];
                    case 4: return Objects(c, n, routeTable);
                    case 5: return Arrays(c, n, key, false, routeTable);
                    case 10: return Arrays(c, n, key, true, routeTable);
                    case 6:
                    {
                        Count(c, (long)n + 1);
                        var result = new object?[n];
                        int kinds = 0;
                        for (int i = 0; i < n; i++) { result[i] = Column(c[i + 1], 1, key, routeTable)[0]; kinds |= Kind(result[i]); }
                        Require(BitCount(kinds & ~1) >= 2, "Noncanonical mixed compact column.");
                        return result;
                    }
                    case 7:
                    {
                        Count(c, 3);
                        var positions = Array(c[1]);
                        Require(positions.Length > 0 && positions.Length < n, "Noncanonical nullable compact column.");
                        var isNull = new bool[n]; int last = -1;
                        foreach (var p in positions) { int at = Index(p, n); Require(at > last, "Compact null rows are not ascending."); isNull[at] = true; last = at; }
                        var rest = Column(c[2], n - positions.Length, key, routeTable);
                        int kinds = 0;
                        foreach (var v in rest) { Require(v != null, "Null inside a nullable compact column body."); kinds |= Kind(v); }
                        Require(BitCount(kinds) == 1, "Noncanonical nullable compact column.");
                        var result = new object?[n];
                        for (int i = 0, j = 0; i < n; i++) result[i] = isNull[i] ? null : rest[j++];
                        return result;
                    }
                    case 8:
                    {
                        Require(CompactRouteField(key) && !decodingRoutes, "Route references outside a route field.");
                        Count(c, (long)n + 1);
                        var result = new object?[n];
                        for (int i = 0; i < n; i++)
                        {
                            if (c[i + 1] == null) continue;
                            int at = Index(c[i + 1], routes.Length);
                            referenced[at] = true;
                            result[i] = routes[at];
                        }
                        return result;
                    }
                    case 9: return Roads(c, n, key, routeTable);
                    default: Fail("Unknown compact column tag " + tag.ToString(CultureInfo.InvariantCulture) + "."); return null!;
                }
            }

            private static int Kind(object? v) => v switch
            {
                null => 1, bool _ => 2, CompactNumber _ => 4, string _ => 8, CompactObject _ => 16, object?[] _ => 32, _ => 0,
            };

            private static int BitCount(int v) { int c = 0; while (v != 0) { c += v & 1; v >>= 1; } return c; }

            private object?[] Roads(object?[] c, int n, string key, bool routeTable)
            {
                Require(key == CompactRoadField, "Road ranges outside a road field.");
                Count(c, 5);
                var bits = c[1] as string ?? Unexpected<string>("Expected a bitmap.");
                Require(bits.Length == n, "Compact bitmap length.");
                var heads = Column(c[2], n, "", routeTable);
                var froms = Array(c[3]); var tos = Array(c[4]);
                int roads = bits.Count(b => b == '1');
                Require(roads > 0 && froms.Length == roads && tos.Length == roads, "Compact road range length.");
                var result = new object?[n];
                int r = 0; string previousHead = ""; int previousTo = -1;
                for (int i = 0; i < n; i++)
                {
                    var head = heads[i] as string ?? Unexpected<string>("Road head is not a string.");
                    if (bits[i] == '0')
                    {
                        Require(!TrySplitRoad(head, out _, out _, out _), "Noncanonical road tag.");
                        result[i] = head; continue;
                    }
                    Require(bits[i] == '1', "Bad compact bit.");
                    int to = Int(tos[r]);
                    int from;
                    if (froms[r] == null)
                    {
                        Require(r > 0 && head == previousHead, "Unchained road range.");
                        from = previousTo;
                    }
                    else
                    {
                        from = Int(froms[r]);
                        Require(!(r > 0 && head == previousHead && from == previousTo), "Noncanonical road range.");
                    }
                    Require(from >= 0 && to >= 0, "Negative road node.");
                    string tag = head + ":" + from.ToString(CultureInfo.InvariantCulture) + ":" + to.ToString(CultureInfo.InvariantCulture);
                    Require(TrySplitRoad(tag, out var split, out int f, out int t) && split == head && f == from && t == to, "Invalid road head.");
                    result[i] = tag;
                    previousHead = head; previousTo = to; r++;
                }
                return result;
            }

            private object?[] Objects(object?[] c, int n, bool routeTable)
            {
                Require(c.Length >= 2, "Compact object column without shapes.");
                var rowShapes = new int[n];
                if (c[1] is object?[] perRow)
                {
                    Require(perRow.Length == n, "Compact shape count.");
                    for (int i = 0; i < n; i++) rowShapes[i] = Index(perRow[i], shapes.Length);
                    Require(rowShapes.Distinct().Count() >= 2, "Noncanonical compact shape list.");
                }
                else { int s = Index(c[1], shapes.Length); for (int i = 0; i < n; i++) rowShapes[i] = s; }
                long fields = 0;
                foreach (int s in rowShapes) fields += shapes[s].Length;
                // Every row's value array is charged by its field columns; check that room before allocating the rows.
                Room(fields);
                var rows = new object?[n];
                var union = new List<string>(); var seen = new List<int>();
                for (int i = 0; i < n; i++)
                {
                    var keys = shapes[rowShapes[i]];
                    rows[i] = new CompactObject(keys, new object?[keys.Length]);
                    if (seen.Contains(rowShapes[i])) continue;
                    seen.Add(rowShapes[i]);
                    foreach (var k in keys) if (!union.Contains(k)) union.Add(k);
                }
                Count(c, union.Count + 2L);
                var at = seen.ToDictionary(s => s, s => union.Select(k => System.Array.IndexOf(shapes[s], k)).ToArray());
                for (int u = 0; u < union.Count; u++)
                {
                    int m = 0;
                    for (int i = 0; i < n; i++) if (at[rowShapes[i]][u] >= 0) m++;
                    var column = Column(c[u + 2], m, union[u], routeTable);
                    for (int i = 0, j = 0; i < n; i++) { int position = at[rowShapes[i]][u]; if (position >= 0) ((CompactObject)rows[i]!).Values[position] = column[j++]; }
                }
                return rows;
            }

            private object?[] Arrays(object?[] c, int n, string key, bool positional, bool routeTable)
            {
                Require(c.Length >= 2, "Compact array column without lengths.");
                var lengths = new int[n];
                if (c[1] is object?[] perRow)
                {
                    Require(perRow.Length == n, "Compact length count.");
                    for (int i = 0; i < n; i++) { lengths[i] = Int(perRow[i]); Require(lengths[i] >= 0, "Negative compact length."); }
                    Require(lengths.Distinct().Count() >= 2, "Noncanonical compact length list.");
                }
                else { int l = Int(c[1]); Require(l >= 0, "Negative compact length."); for (int i = 0; i < n; i++) lengths[i] = l; }
                long total = 0;
                foreach (int l in lengths) total += l;
                Require(total <= int.MaxValue, "Flattened compact column too large.");
                // The child columns charge every element; check that room before allocating the rows.
                Room(total);
                var result = new object?[n];
                if (positional)
                {
                    int longest = lengths.Max();
                    Require(longest > 0, "Noncanonical positional compact column.");
                    Count(c, longest + 2L);
                    for (int i = 0; i < n; i++) result[i] = new object?[lengths[i]];
                    for (int slot = 0; slot < longest; slot++)
                    {
                        int m = 0;
                        foreach (int l in lengths) if (l > slot) m++;
                        var column = Column(c[slot + 2], m, key, routeTable);
                        for (int i = 0, j = 0; i < n; i++) if (lengths[i] > slot) ((object?[])result[i]!)[slot] = column[j++];
                    }
                    return result;
                }
                Count(c, 3);
                var flat = Column(c[2], (int)total, key, routeTable);
                for (int i = 0, at = 0; i < n; i++)
                {
                    var a = lengths[i] == 0 ? System.Array.Empty<object?>() : new object?[lengths[i]];
                    System.Array.Copy(flat, at, a, 0, a.Length); at += a.Length;
                    result[i] = a;
                }
                return result;
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
            public static bool TryCanonical(ReadOnlySpan<char> text, out long value)
            {
                value = 0;
                bool negative = text.Length > 0 && text[0] == '-';
                var digits = negative ? text.Slice(1) : text;
                if (digits.Length == 0 || digits.Length > 19 || (digits[0] == '0' && (digits.Length > 1 || negative))) return false;
                ulong magnitude = 0;
                foreach (char c in digits)
                {
                    if (c < '0' || c > '9') return false;
                    magnitude = magnitude * 10 + (ulong)(c - '0');
                }
                // Nineteen digits cannot overflow ulong; the sign decides the long range.
                if (negative ? magnitude > 1UL << 63 : magnitude > long.MaxValue) return false;
                value = negative ? unchecked((long)(0UL - magnitude)) : (long)magnitude;
                return true;
            }

            private static ulong Zigzag(long value) => unchecked((ulong)((value << 1) ^ (value >> 63)));

            /// <summary>Packed characters for these values without building the text.</summary>
            public static long Length(long[] numbers, bool delta)
            {
                long length = 0, previous = 0;
                foreach (long number in numbers)
                {
                    ulong zigzag = Zigzag(delta ? unchecked(number - previous) : number);
                    previous = number;
                    do { length++; zigzag >>= 5; } while (zigzag != 0);
                }
                return length;
            }

            public static void Append(StringBuilder builder, long[] numbers, bool delta)
            {
                long previous = 0;
                foreach (long number in numbers)
                {
                    ulong zigzag = Zigzag(delta ? unchecked(number - previous) : number);
                    previous = number;
                    do
                    {
                        int bits = (int)(zigzag & 31); zigzag >>= 5;
                        builder.Append(Alphabet[zigzag != 0 ? bits | 32 : bits]);
                    } while (zigzag != 0);
                }
            }

            /// <summary>Decode exactly <paramref name="count"/> values; every value takes at least one character, so the
            /// count is checked against the text before the array is allocated.</summary>
            public static long[] Unpack(string packed, int count, bool delta)
            {
                Require(packed.Length >= count, "Truncated packed compact column.");
                var numbers = new long[count];
                int at = 0; long previous = 0;
                for (int i = 0; i < count; i++)
                {
                    ulong zigzag = 0; int shift = 0;
                    while (true)
                    {
                        Require(at < packed.Length, "Truncated packed compact column.");
                        char c = packed[at++];
                        int v = c < 128 ? Values[c] : -1;
                        Require(v >= 0, "Bad packed compact character.");
                        Require(shift < 60 || (shift == 60 && (v & 31) <= 15 && (v & 32) == 0), "Packed compact value overflows 64 bits.");
                        zigzag |= (ulong)(v & 31) << shift; shift += 5;
                        if ((v & 32) == 0) { Require(!(v == 0 && shift > 5), "Noncanonical packed compact value."); break; }
                    }
                    long value = unchecked((long)(zigzag >> 1) ^ -(long)(zigzag & 1));
                    numbers[i] = delta ? unchecked(previous + value) : value;
                    previous = numbers[i];
                }
                Require(at == packed.Length, "Surplus packed compact characters.");
                return numbers;
            }
        }
    }
}
