using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PersistentCity.Tools.CompactFormat
{
    /// <summary>A JSON number kept as its exact token text, so no double or integer is ever reformatted.</summary>
    public sealed class WireNumber
    {
        public WireNumber(string text) { Text = text; }
        public string Text { get; }
    }

    /// <summary>A JSON object with its exact key order. Keys may be a shared array; values are never shared with Keys.</summary>
    public sealed class WireObject
    {
        public WireObject(string[] keys, object?[] values) { Keys = keys; Values = values; }
        public string[] Keys { get; }
        public object?[] Values { get; }
    }

    /// <summary>
    /// F7p stand-in for the F7c save snapshot: a detached, exact tree of the writer-25 payload. Values are
    /// null, a boxed bool, <see cref="WireNumber"/>, string, <see cref="WireObject"/> or object?[] (array).
    /// Offline prototype only; nothing here is wired into Core, Persistence or Runtime.
    /// </summary>
    public static class WireTree
    {
        public static readonly object True = true, False = false;
        public const int MaxDepth = 64;

        /// <summary>Parse strict JSON. With <paramref name="internKeys"/> every property name is one shared instance.</summary>
        public static object? Parse(string json, bool internKeys = true)
        {
            var parser = new Parser(json, internKeys);
            parser.SkipSpace();
            object? value = parser.Value(0);
            parser.SkipSpace();
            if (parser.Position != json.Length) throw new FormatException("Trailing characters after JSON value.");
            return value;
        }

        /// <summary>Detached deep copy: every object and array is new; immutable leaves and key arrays are shared.</summary>
        public static object? Clone(object? value)
        {
            switch (value)
            {
                case WireObject o:
                    var values = new object?[o.Values.Length];
                    for (int i = 0; i < values.Length; i++) values[i] = Clone(o.Values[i]);
                    return new WireObject(o.Keys, values);
                case object?[] a:
                    var copy = new object?[a.Length];
                    for (int i = 0; i < a.Length; i++) copy[i] = Clone(a[i]);
                    return copy;
                default: return value;
            }
        }

        /// <summary>Writer-25 spelling: no whitespace, Newtonsoft default string escaping, number text unchanged.</summary>
        public static string Write(object? root, int capacity = 1024)
        {
            var builder = new StringBuilder(capacity);
            Write(builder, root);
            return builder.ToString();
        }

        public static void Write(StringBuilder output, object? value)
        {
            switch (value)
            {
                case null: output.Append("null"); return;
                case bool b: output.Append(b ? "true" : "false"); return;
                case WireNumber n: output.Append(n.Text); return;
                case string s: WriteString(output, s); return;
                case WireObject o:
                    output.Append('{');
                    for (int i = 0; i < o.Keys.Length; i++)
                    {
                        if (i > 0) output.Append(',');
                        WriteString(output, o.Keys[i]); output.Append(':'); Write(output, o.Values[i]);
                    }
                    output.Append('}');
                    return;
                case object?[] a:
                    output.Append('[');
                    for (int i = 0; i < a.Length; i++) { if (i > 0) output.Append(','); Write(output, a[i]); }
                    output.Append(']');
                    return;
                default: throw new InvalidOperationException("Unknown wire value " + value.GetType().Name);
            }
        }

        /// <summary>Newtonsoft StringEscapeHandling.Default for double-quoted strings (checked against JsonConvert by test).</summary>
        public static void WriteString(StringBuilder output, string value)
        {
            output.Append('"');
            int start = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                string? escape = c switch
                {
                    '"' => "\\\"", '\\' => "\\\\", '\n' => "\\n", '\r' => "\\r", '\t' => "\\t", '\b' => "\\b", '\f' => "\\f",
                    '\u0085' => "\\u0085", (char)0x2028 => "\\u2028", (char)0x2029 => "\\u2029",
                    _ => null,
                };
                if (escape == null && c >= ' ') continue;
                output.Append(value, start, i - start);
                if (escape != null) output.Append(escape);
                else output.Append("\\u00").Append(Hex(c >> 4)).Append(Hex(c & 15));
                start = i + 1;
            }
            output.Append(value, start, value.Length - start).Append('"');
        }

        private static char Hex(int n) => (char)(n <= 9 ? '0' + n : 'a' + n - 10);

        /// <summary>Field-by-field equality: kinds, key order, exact number text, exact strings, element order.</summary>
        public static bool DeepEqual(object? a, object? b, out string difference)
        {
            var trail = new List<string>();
            if (Equal(a, b, trail)) { difference = ""; return true; }
            trail.Reverse();
            difference = "$" + string.Concat(trail);
            return false;
        }

        // The failing path is built only while unwinding from a difference, so equal trees allocate nothing per field.
        private static bool Equal(object? a, object? b, List<string> trail)
        {
            switch (a)
            {
                case null: if (b == null) return true; break;
                case bool ab: if (b is bool bb && ab == bb) return true; break;
                case WireNumber an: if (b is WireNumber bn && an.Text == bn.Text) return true; break;
                case string astr: if (b is string bstr && string.Equals(astr, bstr, StringComparison.Ordinal)) return true; break;
                case WireObject ao:
                    if (b is WireObject bo && bo.Keys.Length == ao.Keys.Length)
                    {
                        for (int i = 0; i < ao.Keys.Length; i++)
                        {
                            if (!string.Equals(ao.Keys[i], bo.Keys[i], StringComparison.Ordinal)) { trail.Add(": key " + ao.Keys[i] + " vs " + bo.Keys[i]); return false; }
                            if (!Equal(ao.Values[i], bo.Values[i], trail)) { trail.Add("." + ao.Keys[i]); return false; }
                        }
                        return true;
                    }
                    break;
                case object?[] aa:
                    if (b is object?[] ba && ba.Length == aa.Length)
                    {
                        for (int i = 0; i < aa.Length; i++) if (!Equal(aa[i], ba[i], trail)) { trail.Add("[" + i + "]"); return false; }
                        return true;
                    }
                    break;
            }
            trail.Add(": " + Describe(a) + " vs " + Describe(b));
            return false;
        }

        private static string Describe(object? v) => v switch
        {
            null => "null", bool b => b ? "true" : "false", WireNumber n => n.Text, string s => "\"" + s + "\"",
            WireObject o => "object(" + o.Keys.Length + ")", object?[] a => "array(" + a.Length + ")", _ => v.GetType().Name,
        };

        /// <summary>Counts of leaves compared by <see cref="DeepEqual"/>, reported with every round-trip.</summary>
        public sealed class LeafCounts
        {
            public long Objects, Arrays, Strings, Numbers, Bools, Nulls;
            public long Fields => Strings + Numbers + Bools + Nulls;
        }

        public static LeafCounts Count(object? root)
        {
            var counts = new LeafCounts();
            var stack = new Stack<object?>(); stack.Push(root);
            while (stack.Count > 0)
            {
                switch (stack.Pop())
                {
                    case null: counts.Nulls++; break;
                    case bool _: counts.Bools++; break;
                    case WireNumber _: counts.Numbers++; break;
                    case string _: counts.Strings++; break;
                    case WireObject o: counts.Objects++; foreach (var v in o.Values) stack.Push(v); break;
                    case object?[] a: counts.Arrays++; foreach (var v in a) stack.Push(v); break;
                }
            }
            return counts;
        }

        private sealed class Parser
        {
            private readonly string text;
            private readonly Dictionary<string, string>? keys;
            public int Position;

            public Parser(string text, bool internKeys)
            {
                this.text = text;
                keys = internKeys ? new Dictionary<string, string>(StringComparer.Ordinal) : null;
            }

            public void SkipSpace()
            {
                while (Position < text.Length && (text[Position] == ' ' || text[Position] == '\t' || text[Position] == '\n' || text[Position] == '\r')) Position++;
            }

            private char Peek() => Position < text.Length ? text[Position] : throw new FormatException("Unexpected end of JSON.");

            private void Expect(char c)
            {
                if (Peek() != c) throw new FormatException("Expected '" + c + "' at " + Position);
                Position++;
            }

            public object? Value(int depth)
            {
                if (depth > MaxDepth) throw new FormatException("JSON depth exceeds " + MaxDepth);
                char c = Peek();
                switch (c)
                {
                    case '{': return Object(depth);
                    case '[': return Array(depth);
                    case '"': return String();
                    case 't': Literal("true"); return True;
                    case 'f': Literal("false"); return False;
                    case 'n': Literal("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return Number();
                        throw new FormatException("Unexpected '" + c + "' at " + Position);
                }
            }

            private void Literal(string word)
            {
                if (string.CompareOrdinal(text, Position, word, 0, word.Length) != 0) throw new FormatException("Bad literal at " + Position);
                Position += word.Length;
            }

            private WireNumber Number()
            {
                int start = Position;
                if (text[Position] == '-') Position++;
                if (Position >= text.Length || !char.IsAsciiDigit(text[Position])) throw new FormatException("Bad number at " + start);
                if (text[Position] == '0') Position++; else while (Position < text.Length && char.IsAsciiDigit(text[Position])) Position++;
                if (Position < text.Length && text[Position] == '.')
                {
                    Position++;
                    if (Position >= text.Length || !char.IsAsciiDigit(text[Position])) throw new FormatException("Bad fraction at " + start);
                    while (Position < text.Length && char.IsAsciiDigit(text[Position])) Position++;
                }
                if (Position < text.Length && (text[Position] == 'e' || text[Position] == 'E'))
                {
                    Position++;
                    if (Position < text.Length && (text[Position] == '+' || text[Position] == '-')) Position++;
                    if (Position >= text.Length || !char.IsAsciiDigit(text[Position])) throw new FormatException("Bad exponent at " + start);
                    while (Position < text.Length && char.IsAsciiDigit(text[Position])) Position++;
                }
                return new WireNumber(text.Substring(start, Position - start));
            }

            public string String()
            {
                Expect('"');
                int start = Position;
                while (true)
                {
                    char c = Peek();
                    if (c == '"') { string plain = text.Substring(start, Position - start); Position++; return plain; }
                    if (c == '\\') break;
                    if (c < ' ') throw new FormatException("Control character in string at " + Position);
                    Position++;
                }
                var builder = new StringBuilder(text, start, Position - start, Position - start + 16);
                while (true)
                {
                    char c = Peek(); Position++;
                    if (c == '"') return builder.ToString();
                    if (c < ' ') throw new FormatException("Control character in string at " + (Position - 1));
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
                            if (Position + 4 > text.Length || !ushort.TryParse(text.AsSpan(Position, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ushort code))
                                throw new FormatException("Bad unicode escape at " + Position);
                            builder.Append((char)code); Position += 4; break;
                        default: throw new FormatException("Bad escape at " + Position);
                    }
                }
            }

            private WireObject Object(int depth)
            {
                Expect('{'); SkipSpace();
                var names = new List<string>(); var values = new List<object?>();
                if (Peek() == '}') { Position++; return new WireObject(names.ToArray(), values.ToArray()); }
                while (true)
                {
                    SkipSpace();
                    string name = String();
                    if (keys != null) { if (keys.TryGetValue(name, out var shared)) name = shared; else keys.Add(name, name); }
                    SkipSpace(); Expect(':'); SkipSpace();
                    names.Add(name); values.Add(Value(depth + 1));
                    SkipSpace();
                    if (Peek() == ',') { Position++; continue; }
                    Expect('}');
                    return new WireObject(names.ToArray(), values.ToArray());
                }
            }

            private object?[] Array(int depth)
            {
                Expect('['); SkipSpace();
                var values = new List<object?>();
                if (Peek() == ']') { Position++; return values.ToArray(); }
                while (true)
                {
                    SkipSpace(); values.Add(Value(depth + 1)); SkipSpace();
                    if (Peek() == ',') { Position++; continue; }
                    Expect(']');
                    return values.ToArray();
                }
            }
        }
    }
}
