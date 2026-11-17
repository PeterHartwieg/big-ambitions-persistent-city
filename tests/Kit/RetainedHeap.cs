using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace PersistentCity.Tests;

internal sealed class RetainedHeap
{
    internal sealed class Row
    {
        public string Name = "";
        public long Objects, Bytes, ScannedBytes, References;
    }

    private const int Header = 16, ArrayHeader = 32, Pointer = 8;
    private static readonly ConcurrentDictionary<Type, Layout> Layouts = new ConcurrentDictionary<Type, Layout>();
    private readonly HashSet<object> seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, Row> byType = new Dictionary<string, Row>(StringComparer.Ordinal);
    private readonly List<Row> byRoot = new List<Row>();

    private sealed class Layout
    {
        public int Bytes;
        public int References;
        public FieldInfo[] Fields = Array.Empty<FieldInfo>();
    }

    public IReadOnlyList<Row> Roots => byRoot;
    public IEnumerable<Row> Types => byType.Values.OrderByDescending(r => r.Bytes);
    public long Objects => byRoot.Sum(r => r.Objects);
    public long Bytes => byRoot.Sum(r => r.Bytes);
    public long ScannedBytes => byRoot.Sum(r => r.ScannedBytes);
    public long References => byRoot.Sum(r => r.References);

    public void Exclude(object? value) { if (value != null) seen.Add(value); }

    public Row Add(string name, object? root)
    {
        var row = new Row { Name = name };
        byRoot.Add(row);
        if (root == null || !seen.Add(root)) return row;
        var stack = new Stack<object>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var value = stack.Pop();
            var type = value.GetType();
            long bytes, references;
            if (value is string text) { bytes = Align(Header + 4 + 2 * (text.Length + 1)); references = 0; }
            else if (value is Array array)
            {
                var element = type.GetElementType()!;
                long length = array.LongLength;
                if (!element.IsValueType)
                {
                    bytes = Align(ArrayHeader + length * Pointer); references = length;
                    foreach (var item in array) if (item != null && Walkable(item) && seen.Add(item)) stack.Push(item);
                }
                else
                {
                    var layout = Of(element);
                    bytes = Align(ArrayHeader + length * layout.Bytes); references = length * layout.References;
                    if (layout.References > 0) foreach (var item in array) Children(item!, layout, stack);
                }
            }
            else
            {
                var layout = Of(type);
                bytes = Align(Header + layout.Bytes); references = layout.References;
                Children(value, layout, stack);
            }
            Count(row, bytes, references);
            if (!byType.TryGetValue(Name(type), out var kind)) byType.Add(Name(type), kind = new Row { Name = Name(type) });
            Count(kind, bytes, references);
        }
        return row;
    }

    private static void Count(Row row, long bytes, long references)
    {
        row.Objects++; row.Bytes += bytes; row.References += references;
        if (references > 0) row.ScannedBytes += bytes;
    }

    private static bool Walkable(object value) => !(value is Type || value is MemberInfo || value is Assembly || value is Delegate d && d.Target == null);

    private void Children(object value, Layout layout, Stack<object> stack)
    {
        foreach (var field in layout.Fields)
        {
            var child = field.GetValue(value);
            if (child == null) continue;
            if (field.FieldType.IsValueType) { Children(child, Of(field.FieldType), stack); continue; }
            if (Walkable(child) && seen.Add(child)) stack.Push(child);
        }
    }

    private static Layout Of(Type type) => Layouts.GetOrAdd(type, t =>
    {
        var layout = new Layout();
        var fields = new List<FieldInfo>();
        for (var at = t; at != null && at != typeof(object) && at != typeof(ValueType); at = at.BaseType)
            foreach (var field in at.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                var f = field.FieldType;
                if (f.IsPointer || f == typeof(IntPtr) || f == typeof(UIntPtr)) { layout.Bytes += Pointer; continue; }
                if (!f.IsValueType) { layout.Bytes += Pointer; layout.References++; fields.Add(field); continue; }
                if (f.IsPrimitive || f.IsEnum) { layout.Bytes += Primitive(f); continue; }
                var inner = Of(f);
                layout.Bytes += inner.Bytes; layout.References += inner.References;
                if (inner.References > 0) fields.Add(field);
            }
        if (t.IsValueType && layout.Bytes == 0) layout.Bytes = 1;
        if (t.IsValueType && !t.IsPrimitive && !t.IsEnum) layout.Bytes = Math.Max(layout.Bytes, StructSize(t));
        layout.Fields = fields.ToArray();
        return layout;
    });

    private static int StructSize(Type type) => (int)typeof(Unsafe).GetMethod(nameof(Unsafe.SizeOf))!.MakeGenericMethod(type).Invoke(null, null)!;

    private static int Primitive(Type type) => Type.GetTypeCode(type.IsEnum ? Enum.GetUnderlyingType(type) : type) switch
    {
        TypeCode.Boolean or TypeCode.Byte or TypeCode.SByte => 1,
        TypeCode.Char or TypeCode.Int16 or TypeCode.UInt16 => 2,
        TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Single => 4,
        _ => 8,
    };

    private static long Align(long bytes) => (bytes + 7) & ~7L;

    private static string Name(Type type)
    {
        if (!type.IsGenericType) return type.IsArray ? Name(type.GetElementType()!) + "[]" : type.Name;
        string name = type.Name;
        int tick = name.IndexOf('`');
        return (tick < 0 ? name : name.Substring(0, tick)) + "<" + string.Join(",", type.GetGenericArguments().Select(Name)) + ">";
    }
}
