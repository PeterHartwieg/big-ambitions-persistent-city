using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

/// <summary>
/// F7d (issue #94, plan section 4 row F7d, compact-format decision): the writer stores candidate C, the columnar compact
/// wire with a shared route table and chained road-tag ranges, encoded from F7c's snapshot. No migration: any other
/// schema number is refused with the distinct "start a new city" status and kept.
/// </summary>
public sealed class F7dCompactWriterTests
{
    private readonly ITestOutputHelper output;
    public F7dCompactWriterTests(ITestOutputHelper output) { this.output = output; }

    private static SaveCodec.CompactObject Envelope(string payload) => (SaveCodec.CompactObject)SaveCodec.CompactTree.Parse(payload, 64, canonical: true)!;
    private static string Spell(object? tree) => SaveCodec.CompactTree.Write(tree, 1 << 16, int.MaxValue);
    private static object? Field(SaveCodec.CompactObject o, string key) => o.Values[Array.IndexOf(o.Keys, key)];
    private static void SetField(SaveCodec.CompactObject o, string key, object? value) => o.Values[Array.IndexOf(o.Keys, key)] = value;

    [Fact]
    public void CompactReadInternsEveryStringValueOncePerDistinctText()
    {
        var state = F7cFixtures.Build("rush-957x2").State;
        var compact = SaveCodec.Read(SaveCodec.Write(state)).State!;
        static (int Instances, int Texts) Homes(CityState city)
        {
            var instances = new HashSet<string>(ReferenceEqualityComparer.Instance);
            foreach (var c in city.Citizens) instances.Add(c.HomeId);
            return (instances.Count, city.Citizens.Select(c => c.HomeId).Distinct(StringComparer.Ordinal).Count());
        }
        var fromCompact = Homes(compact);
        Assert.Equal(fromCompact.Texts, fromCompact.Instances);
        output.WriteLine($"home id instances: compact {fromCompact.Instances}, distinct {fromCompact.Texts}");
    }

    // ---- No migration: older development cities ----

    /// <summary>Older development builds wrote 2 to 36, as JSON or in the compact wire; any number other than the held 1 is
    /// refused the same way.</summary>
    [Theory]
    [InlineData(36, true)] [InlineData(34, true)] [InlineData(33, false)] [InlineData(29, false)] [InlineData(2, false)] [InlineData(37, true)]
    public void AnotherSchemaNumberIsRefusedWithTheStartANewCityStatusAndKept(int schema, bool compact)
    {
        var state = ChangeJournalTests.Fixture(5150);
        string older = compact ? SaveCodec.Write(state).Replace(SaveCodec.CompactPrefix, "{\"Schema\":" + schema + ",\"f\":")
            : SaveWire.Text(state).Replace("{\"Schema\":" + CityState.CurrentSchema + ",", "{\"Schema\":" + schema + ",");
        Assert.Equal(schema, SaveCodec.DevelopmentSchemaOf(older));
        var data = new Dictionary<string, string>(StringComparer.Ordinal)
        { [SaveStore.StateKey] = older, ["persistentcity:food-sale-v1:intent-1"] = "pending", ["other"] = "kept" };
        var before = new Dictionary<string, string>(data);
        var read = SaveCodec.Read(older);
        Assert.Equal(SaveReadStatus.DevelopmentSchema, read.Status);
        Assert.Null(read.State);
        Assert.Equal(schema, read.SourceSchema);
        Assert.Contains("start a new city", read.Error);
        Assert.DoesNotContain("Invalid", read.Error);
        var session = SaveSession.Open(data, state.PopulationKey);
        Assert.Equal(SaveReadStatus.DevelopmentSchema, session.InitialReadResult.Status);
        Assert.Equal(SaveReadStatus.DevelopmentSchema, SaveStore.ReadFrom(data, state.PopulationKey).Status);
        // A fresh city cannot overwrite it, through either publication path.
        var fresh = CurrentSchemaOnlyTests.City(); fresh.PopulationKey = state.PopulationKey;
        Assert.False(session.WriteTo(data, fresh).Success);
        Assert.False(SaveStore.WriteTo(data, fresh).Success);
        var error = Assert.Throws<SaveValidationException>(() => SaveCodec.RequireExecutableCapabilities(older));
        Assert.Contains("start a new city", error.Message);
        Assert.Equal(before, data);
        Assert.Same(older, data[SaveStore.StateKey]);
    }

    [Fact]
    public void FreshCitiesAreCreatedInTheCompactFormat()
    {
        var state = CurrentSchemaOnlyTests.City();
        Assert.Equal(CityState.CurrentSchema, state.Schema);
        var data = new Dictionary<string, string>(StringComparer.Ordinal) { ["other"] = "kept" };
        var session = SaveSession.Open(data, state.PopulationKey);
        Assert.Equal(SaveReadStatus.Missing, session.InitialReadResult.Status);
        Assert.True(session.WriteTo(data, state).Success);
        Assert.StartsWith(SaveCodec.CompactPrefix, data[SaveStore.StateKey]);
        var reopened = SaveSession.Open(data, state.PopulationKey);
        Assert.True(reopened.InitialReadResult.Status == SaveReadStatus.Loaded, reopened.InitialReadResult.Error);
        Assert.Equal("kept", data["other"]);
    }

    // ---- Strict reader: unknown, future, malformed and overbound compact payloads ----

    private static string Small() => SaveCodec.Write(F7cFixtures.Build("rush-957x2").State);

    private static List<object?[]> Columns(object? node, int tag)
    {
        var found = new List<object?[]>();
        void Walk(object? value)
        {
            switch (value)
            {
                case SaveCodec.CompactObject o: foreach (var v in o.Values) Walk(v); break;
                case object?[] a:
                    if (a.Length > 0 && a[0] is SaveCodec.CompactNumber n && n.Text == tag.ToString(System.Globalization.CultureInfo.InvariantCulture)) found.Add(a);
                    foreach (var v in a) Walk(v);
                    break;
            }
        }
        Walk(node);
        return found;
    }

    /// <summary>The product refuses <paramref name="payload"/> through every entry point and keeps every key; unlike the
    /// shared helper it never re-encodes a body, so a bare schema-34 body reaches the reader as stored.</summary>
    private static void RefusedAndKept(string payload, SaveReadStatus status)
    {
        var replacement = CurrentSchemaOnlyTests.City();
        var data = new Dictionary<string, string> { [SaveStore.StateKey] = payload, ["persistentcity:food-sale-v1:i"] = "pending", ["other"] = "kept" };
        var before = new Dictionary<string, string>(data);
        var read = SaveCodec.Read(payload); Assert.True(read.Status == status, read.Error); Assert.Null(read.State);
        Assert.Equal(status, SaveStore.ReadFrom(data, replacement.PopulationKey).Status);
        Assert.Equal(status, SaveSession.Open(data, replacement.PopulationKey).InitialReadResult.Status);
        Assert.False(SaveStore.WriteTo(data, replacement).Success);
        Assert.False(SaveSession.Open(data, replacement.PopulationKey).WriteTo(data, replacement).Success);
        Assert.Equal(before, data); Assert.Same(payload, data[SaveStore.StateKey]);
    }

    private static string Variant(string payload, Action<SaveCodec.CompactObject> edit)
    {
        var top = Envelope(payload);
        edit(top);
        return Spell(top);
    }

    [Fact]
    public void OtherSchemaUnknownMalformedDanglingAndOverboundCompactPayloadsAreRefusedAndKept()
    {
        string valid = Small();
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(valid).Status);
        Assert.Equal(valid, Spell(Envelope(valid)));
        var top = Envelope(valid);
        Assert.True(Columns(Field(top, "t"), 8).Count > 0, "the rush city has route references");

        var refusals = new (string Name, string Payload, SaveReadStatus Status)[]
        {
            ("another schema", valid.Replace("{\"Schema\":1,", "{\"Schema\":37,"), SaveReadStatus.DevelopmentSchema),
            ("far other schema", valid.Replace("{\"Schema\":1,", "{\"Schema\":99,"), SaveReadStatus.DevelopmentSchema),
            ("unknown format", valid.Replace(",\"f\":\"pcc-c\",", ",\"f\":\"pcc-d\","), SaveReadStatus.Invalid),
            ("unknown version", valid.Replace(",\"v\":1,", ",\"v\":2,"), SaveReadStatus.Invalid),
            ("unknown field", valid.Substring(0, valid.Length - 1) + ",\"x\":0}", SaveReadStatus.Invalid),
            ("duplicate field", valid.Substring(0, valid.Length - 1) + ",\"t\":0}", SaveReadStatus.Invalid),
            ("missing field", Variant(valid, o => SetField(o, "n", null)), SaveReadStatus.Invalid),
            ("reordered fields", valid.Replace(",\"f\":\"pcc-c\",\"v\":1,", ",\"v\":1,\"f\":\"pcc-c\","), SaveReadStatus.Invalid),
            ("truncated by one", valid.Substring(0, valid.Length - 1), SaveReadStatus.Invalid),
            ("truncated half", valid.Substring(0, valid.Length / 2), SaveReadStatus.Invalid),
            ("trailing content", valid + "0", SaveReadStatus.Invalid),
            ("whitespace", valid.Replace(",\"k\":[", ", \"k\":["), SaveReadStatus.Invalid),
            ("noncanonical escape", valid.Replace(",\"f\":\"pcc-c\",", ",\"f\":\"pcc\\u002dc\","), SaveReadStatus.Invalid),
            ("bare JSON body in the current writer", SaveCodec.ExpandCompact(valid), SaveReadStatus.Invalid),
            ("body schema mismatch", SaveCodec.CompactFromBody(SaveCodec.ExpandCompact(valid).Replace("{\"Schema\":1,", "{\"Schema\":2,")), SaveReadStatus.Invalid),
            ("bad string index", Variant(valid, o => { var s = (object?[])Field(o, "s")!; var c = Columns(Field(o, "t"), 1).First(col => col.Skip(1).Any(v => v is SaveCodec.CompactNumber));
                int at = Array.FindIndex(c, 1, v => v is SaveCodec.CompactNumber); c[at] = new SaveCodec.CompactNumber(s.Length.ToString()); }), SaveReadStatus.Invalid),
            ("negative string index", Variant(valid, o => { var c = Columns(Field(o, "t"), 1).First(col => col.Skip(1).Any(v => v is SaveCodec.CompactNumber));
                int at = Array.FindIndex(c, 1, v => v is SaveCodec.CompactNumber); c[at] = new SaveCodec.CompactNumber("-1"); }), SaveReadStatus.Invalid),
            ("bad shape index", Variant(valid, o => { var root = (object?[])Field(o, "t")!; root[1] = new SaveCodec.CompactNumber(((object?[])Field(o, "h")!).Length.ToString()); }), SaveReadStatus.Invalid),
            ("dangling route reference", Variant(valid, o => { var c = Columns(Field(o, "t"), 8)[0]; int at = Array.FindIndex(c, 1, v => v != null); c[at] = new SaveCodec.CompactNumber(Field(o, "n") is SaveCodec.CompactNumber n ? n.Text : "0"); }), SaveReadStatus.Invalid),
            ("route count mismatch", Variant(valid, o => SetField(o, "n", new SaveCodec.CompactNumber((int.Parse(((SaveCodec.CompactNumber)Field(o, "n")!).Text) + 1).ToString()))), SaveReadStatus.Invalid),
            ("unreferenced route", Variant(valid, o => { foreach (var c in Columns(Field(o, "t"), 8)) for (int i = 1; i < c.Length; i++) if (c[i] is SaveCodec.CompactNumber n && n.Text == "0") c[i] = new SaveCodec.CompactNumber("1"); }), SaveReadStatus.Invalid),
            ("route reference outside a route field", Variant(valid, o => { var root = (object?[])Field(o, "t")!; root[2] = new object?[] { new SaveCodec.CompactNumber("8"), new SaveCodec.CompactNumber("0") }; }), SaveReadStatus.Invalid),
            ("unknown column tag", Variant(valid, o => { var c = Columns(Field(o, "t"), 1)[0]; c[0] = new SaveCodec.CompactNumber("13"); }), SaveReadStatus.Invalid),
            ("duplicate string table entry", Variant(valid, o => { var s = (object?[])Field(o, "s")!; s[1] = s[0]; }), SaveReadStatus.Invalid),
            ("unordered string table", Variant(valid, o => { var s = (object?[])Field(o, "s")!; (s[0], s[1]) = (s[1], s[0]); }), SaveReadStatus.Invalid),
            ("duplicate key table entry", Variant(valid, o => { var k = (object?[])Field(o, "k")!; k[1] = k[0]; }), SaveReadStatus.Invalid),
            ("duplicate shape", Variant(valid, o => { var h = (object?[])Field(o, "h")!; h[1] = h[0]; }), SaveReadStatus.Invalid),
            ("overbound route table", Variant(valid, o => SetField(o, "n", new SaveCodec.CompactNumber((SaveCodec.MaxEmbeddedRecords + 1).ToString()))), SaveReadStatus.Invalid),
            ("overbound column", Variant(valid, o => { var root = (object?[])Field(o, "t")!; root[3] = new object?[] { new SaveCodec.CompactNumber("5"), new SaveCodec.CompactNumber(SaveCodec.MaxCompactValues.ToString()), new object?[] { new SaveCodec.CompactNumber("3"), new SaveCodec.CompactNumber(SaveCodec.MaxCompactValues.ToString()) } }; }), SaveReadStatus.Invalid),
            ("short packed column", Variant(valid, o => { var c = Columns(Field(o, "t"), 11)[0]; c[2] = ((string)c[2]!).Substring(1); }), SaveReadStatus.Invalid),
            ("noncanonical nullable column", Variant(valid, o => { var root = (object?[])Field(o, "t")!; root[3] = new object?[] { new SaveCodec.CompactNumber("7"), new object?[] { new SaveCodec.CompactNumber("0") }, new object?[] { new SaveCodec.CompactNumber("0"), new SaveCodec.CompactNumber("1") } }; }), SaveReadStatus.Invalid),
        };
        foreach (var (name, payload, status) in refusals)
        {
            Assert.False(payload == valid, name + " did not change the payload");
            var read = SaveCodec.Read(payload);
            Assert.True(read.Status == status, name + ": " + read.Status + " " + read.Error);
            RefusedAndKept(payload, status);
        }
    }

    [Fact]
    public void NoncanonicalTablesAreRefusedEvenWhenTheDecodedCityIsUnchanged()
    {
        // Duplicate routes: every reference gets its own entry, so identical journeys appear twice in the table. The
        // decoded city would be the same, but the next write would merge them, so the reader refuses.
        string rush = Small();
        string duplicates = SaveCodec.CompactFromBody(SaveCodec.ExpandCompact(rush), shareRoutes: false);
        Assert.NotEqual(rush, duplicates);
        Assert.Equal(SaveCodec.ExpandCompact(rush), SaveCodec.CompactTree.Write(SaveCodec.CompactTree.Parse(SaveCodec.ExpandCompact(rush), 64, canonical: true), 1 << 16, int.MaxValue));
        Assert.Contains("duplicate or unordered route", SaveCodec.Read(duplicates).Error);
        RefusedAndKept(duplicates, SaveReadStatus.Invalid);

        string minimal = SaveCodec.Write(CurrentSchemaOnlyTests.City());
        var cases = new (string Name, string Payload, string Error)[]
        {
            ("unused ordinal-last string", Variant(minimal, o => SetField(o, "s", ((object?[])Field(o, "s")!).Append("￿ unused").ToArray())), "fewer than twice"),
            ("unused ordinal-last shape", Variant(minimal, o =>
            {
                var shapes = (object?[])Field(o, "h")!; var keys = (object?[])Field(o, "k")!;
                var last = (object?[])shapes[^1]!;
                var extra = Enumerable.Range(0, keys.Length).Select(i => i.ToString()).First(i => !last.OfType<SaveCodec.CompactNumber>().Any(n => n.Text == i));
                SetField(o, "h", shapes.Append(last.Append(new SaveCodec.CompactNumber(extra)).ToArray()).ToArray());
            }), "unused shape"),
            ("inline string seen twice", Variant(minimal, o =>
            {
                var inline = Columns(Field(o, "t"), 1).SelectMany(c => c.Skip(1).Select((v, i) => (Column: c, At: i + 1, Value: v))).Where(x => x.Value is string).ToList();
                Assert.True(inline.Count >= 2, "the minimal city has inline strings");
                inline[1].Column[inline[1].At] = inline[0].Value;
            }), "inline"),
            ("table string written inline", Variant(minimal, o =>
            {
                var table = (object?[])Field(o, "s")!;
                var reference = Columns(Field(o, "t"), 1).SelectMany(c => c.Skip(1).Select((v, i) => (Column: c, At: i + 1, Value: v))).First(x => x.Value is SaveCodec.CompactNumber);
                reference.Column[reference.At] = table[int.Parse(((SaveCodec.CompactNumber)reference.Value!).Text)];
            }), "fewer than twice"),
        };
        foreach (var (name, payload, error) in cases)
        {
            Assert.False(payload == minimal, name);
            var read = SaveCodec.Read(payload);
            Assert.True(read.Status == SaveReadStatus.Invalid, name + ": " + read.Status);
            Assert.True(read.Error.Contains(error) || name == "table string written inline" && read.Error.Contains("inline"), name + ": " + read.Error);
            RefusedAndKept(payload, SaveReadStatus.Invalid);
        }
    }

    private static long Allocated(Func<SaveReadResult> read, out SaveReadResult result)
    {
        read(); // warm up the code path once
        long before = GC.GetAllocatedBytesForCurrentThread();
        result = read();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [Fact]
    public void OverboundTablesAndColumnsAreRefusedBeforeTheirArraysAreAllocated()
    {
        const string head = "{\"Schema\":1,\"f\":\"pcc-c\",\"v\":1,\"k\":[\"Citizens\",\"Schema\"],\"h\":[[0,1]],\"s\":[],\"n\":0,\"r\":0,";
        // 500,001 citizen slots from about 120 characters: refused by the body's own citizen bound before allocation.
        string citizens = head + "\"t\":[4,0,[5,500001,[3,500001]],[0,1]]}";
        long bytes = Allocated(() => SaveCodec.Read(citizens), out var read);
        Assert.Equal(SaveReadStatus.Invalid, read.Status);
        Assert.Contains("collection length", read.Error);
        Assert.True(bytes < 64 * 1024, bytes + " B allocated");
        // Within the citizen bound but far more slots than a payload this short can carry.
        string dense = head + "\"t\":[4,0,[5," + SaveCodec.MaxResidents + ",[3," + SaveCodec.MaxResidents + "]],[0,1]]}";
        bytes = Allocated(() => SaveCodec.Read(dense), out read);
        Assert.Equal(SaveReadStatus.Invalid, read.Status);
        Assert.Contains("decoded value bound", read.Error);
        Assert.True(bytes < 64 * 1024, bytes + " B allocated");
        // One key over the record bound: counted without building the table.
        var keys = new StringBuilder("{\"Schema\":1,\"f\":\"pcc-c\",\"v\":1,\"k\":[");
        for (int i = 0; i <= SaveCodec.MaxEmbeddedRecords; i++) { if (i > 0) keys.Append(','); keys.Append("\"k").Append(i.ToString("D7")).Append('"'); }
        string overKeys = keys.Append("],\"h\":[],\"s\":[],\"n\":0,\"r\":0,\"t\":0}").ToString();
        bytes = Allocated(() => SaveCodec.Read(overKeys), out read);
        Assert.Equal(SaveReadStatus.Invalid, read.Status);
        Assert.Contains("tables exceed the record bound", read.Error);
        Assert.True(bytes < 64 * 1024, bytes + " B allocated");
        output.WriteLine($"one-over key table: {overKeys.Length:N0} chars refused after {bytes:N0} B allocated");
        var data = new Dictionary<string, string> { [SaveStore.StateKey] = overKeys };
        Assert.Equal(SaveReadStatus.Invalid, SaveStore.ReadFrom(data, "any").Status);
        Assert.Same(overKeys, data[SaveStore.StateKey]);
        // One shape naming 4,000,001 fields against a one-key table: bounded by the key count during the count pass.
        var shape = new StringBuilder("{\"Schema\":1,\"f\":\"pcc-c\",\"v\":1,\"k\":[\"Schema\"],\"h\":[[");
        for (int i = 0; i <= SaveCodec.MaxEmbeddedRecords; i++) { if (i > 0) shape.Append(','); shape.Append('0'); }
        string overShape = shape.Append("]],\"s\":[],\"n\":0,\"r\":0,\"t\":0}").ToString();
        bytes = Allocated(() => SaveCodec.Read(overShape), out read);
        Assert.Equal(SaveReadStatus.Invalid, read.Status);
        Assert.Contains("Duplicate field in a compact shape", read.Error); // refused at the second index
        Assert.True(bytes < 64 * 1024, bytes + " B allocated");
        // A typed table: a key that is not a string is refused before the table is built.
        Assert.Contains("not a string", SaveCodec.Read(head.Replace("\"k\":[\"Citizens\",\"Schema\"]", "\"k\":[\"Citizens\",[0]]") + "\"t\":0}").Error);
        // 1,025 food scopes: within the value budget (a long table string makes room) but over the body's 1,024-scope
        // bound, refused before the scope array is allocated.
        Assert.Equal(1024, SaveCodec.CompactCollectionLimit("Shops")); // FoodScope and ShopObservation both allow 1,024
        Assert.Equal(SaveCodec.MaxResidents, SaveCodec.CompactCollectionLimit("Citizens"));
        Assert.Equal(SaveCodec.MaxRecentVisits, SaveCodec.CompactCollectionLimit("RecentVisits"));
        int shopBound = SaveCodec.CompactCollectionLimit("Shops");
        string scopes = "{\"Schema\":1,\"f\":\"pcc-c\",\"v\":1,\"k\":[\"Food\",\"Schema\",\"Shops\"],\"h\":[[0,1],[2]],\"s\":[\"" + new string('x', shopBound) + "\"],\"n\":0,\"r\":0,"
            + "\"t\":[4,0,[4,1,[5," + (shopBound + 1) + ",[3," + (shopBound + 1) + "]]],[0,1]]}";
        bytes = Allocated(() => SaveCodec.Read(scopes), out read);
        Assert.Equal(SaveReadStatus.Invalid, read.Status);
        Assert.Contains("collection length", read.Error);
        Assert.True(bytes < 64 * 1024 + 4L * shopBound, bytes + " B allocated");
        output.WriteLine($"Shops bound {shopBound}; one-over refused after {bytes:N0} B");
        // The early authority probe uses the same counts.
        Assert.Contains("tables exceed the record bound", Assert.Throws<SaveValidationException>(() => SaveCodec.RequireExecutableCapabilities(overKeys)).Message);
    }

    private static string Envelope(IEnumerable<string> keys, string shapes, string strings, string root)
        => "{\"Schema\":1,\"f\":\"pcc-c\",\"v\":1,\"k\":[" + string.Join(",", keys.Select(k => "\"" + k + "\"")) + "],\"h\":" + shapes
            + ",\"s\":" + strings + ",\"n\":0,\"r\":0,\"t\":" + root + "}";

    [Fact]
    public void ManyIndividuallyBoundedShapesAreRefusedBeforeTheShapeTableIsBuilt()
    {
        // 2,001 shapes of 2,000 zero indices against 2,000 keys: each passes the per-shape length, the duplicate index is
        // refused during the count pass.
        var keys = Enumerable.Range(0, 2000).Select(i => "k" + i.ToString("D4")).ToArray();
        string zeros = "[" + string.Join(",", Enumerable.Repeat("[" + string.Join(",", Enumerable.Repeat("0", 2000)) + "]", 2001)) + "]";
        string duplicates = Envelope(keys, zeros, "[]", "0");
        long bytes = Allocated(() => SaveCodec.Read(duplicates), out var read);
        Assert.Equal(SaveReadStatus.Invalid, read.Status);
        Assert.Contains("Duplicate field in a compact shape", read.Error);
        Assert.True(bytes < 512 * 1024, bytes + " B allocated");
        // 300 shapes naming all 300 keys in distinct orders: 90,000 indices, over the aggregate bound of 65,536.
        var small = Enumerable.Range(0, 300).Select(i => "k" + i.ToString("D3")).ToArray();
        string rotations = "[" + string.Join(",", Enumerable.Range(0, 300).Select(r => "[" + string.Join(",", Enumerable.Range(0, 300).Select(i => ((i + r) % 300).ToString())) + "]")) + "]";
        string wide = Envelope(small, rotations, "[]", "0");
        bytes = Allocated(() => SaveCodec.Read(wide), out read);
        Assert.Equal(SaveReadStatus.Invalid, read.Status);
        Assert.Contains("shapes exceed their field bound", read.Error);
        Assert.True(bytes < 512 * 1024, bytes + " B allocated");
        // Noncanonical and out-of-range indices are refused in the same pass.
        Assert.Contains("canonical key index", SaveCodec.Read(Envelope(small, "[[01]]", "[]", "0")).Error);
        Assert.Contains("outside the key table", SaveCodec.Read(Envelope(small, "[[300]]", "[]", "0")).Error);
        output.WriteLine($"shape probes refused after {bytes:N0} B");
        RefusedAndKept(duplicates, SaveReadStatus.Invalid);
    }

    [Fact]
    public void SpecializedBodyBoundsApplyToMenuIdsProductNamesAndPortals()
    {
        Assert.Equal(FoodMenuPolicy.MaximumOffers - 1, SaveCodec.CompactCollectionLimit("AdditionalProductIds"));
        Assert.Equal(FoodNameIdentity.MaximumNames, SaveCodec.CompactCollectionLimit("ProductNames"));
        Assert.Equal(SaveCodec.MaxPortals, SaveCodec.CompactCollectionLimit("Portals"));
        string pad(int n) => "[\"" + new string('x', n) + "\"]"; // an unused table string makes value-budget room
        var cases = new[]
        {
            ("menu IDs", Envelope(new[] { "AdditionalProductIds", "Food", "Schema", "Shops" }, "[[0],[1,2],[3]]", pad(64),
                "[4,1,[4,2,[5,1,[4,0,[5," + FoodMenuPolicy.MaximumOffers + ",[3," + FoodMenuPolicy.MaximumOffers + "]]]]],[0,1]]")),
            ("product names", Envelope(new[] { "ProductNames", "Schema" }, "[[0,1]]", pad(FoodNameIdentity.MaximumNames),
                "[4,0,[5," + (FoodNameIdentity.MaximumNames + 1) + ",[3," + (FoodNameIdentity.MaximumNames + 1) + "]],[0,1]]")),
            ("portals", Envelope(new[] { "Portals", "Schema" }, "[[0,1]]", pad(SaveCodec.MaxPortals),
                "[4,0,[5," + (SaveCodec.MaxPortals + 1) + ",[3," + (SaveCodec.MaxPortals + 1) + "]],[0,1]]")),
        };
        foreach (var (name, payload) in cases)
        {
            long bytes = Allocated(() => SaveCodec.Read(payload), out var read);
            Assert.True(read.Status == SaveReadStatus.Invalid, name);
            Assert.True(read.Error.Contains("collection length"), name + ": " + read.Error);
            Assert.True(bytes < 64 * 1024 + 4L * payload.Length, name + ": " + bytes + " B allocated");
            RefusedAndKept(payload, SaveReadStatus.Invalid);
        }
    }

    [Fact]
    public void FieldLimitTableBuildsOnceAndCheaply()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var table = SaveCodec.BuildFieldLimits();
        double ms = watch.Elapsed.TotalMilliseconds;
        output.WriteLine($"field-limit table: {table.Count} names in {ms:F2} ms");
        Assert.True(table.Count > 50);
        Assert.Equal(SaveCodec.MaxResidents, table["Citizens"]);
    }

    [Fact]
    public void ACompactCityThatExpandsPastTheInputCapIsRefusedBeforeItsBodyIsMaterialized()
    {
        // One route holding a 1 Mi-character string, referenced 257 times: 1,001 characters of compact text, more than
        // 256 Mi characters of body. The reader stops writing the body at the cap.
        string big = new string('x', 1 << 20);
        var refs = string.Join(",", Enumerable.Repeat("0", 257));
        string payload = "{\"Schema\":1,\"f\":\"pcc-c\",\"v\":1,\"k\":[\"Citizens\",\"Journey\",\"Legs\",\"Schema\"],\"h\":[[0,3],[1],[2]],\"s\":[],"
            + "\"n\":1,\"r\":[4,2,[5,1,[1,\"" + big + "\"]]],\"t\":[4,0,[5,257,[4,1,[8," + refs + "]]],[0,1]]}";
        var read = SaveCodec.Read(payload);
        Assert.Equal(SaveReadStatus.Invalid, read.Status);
        Assert.Contains("input size limit", read.Error);
        RefusedAndKept(payload, SaveReadStatus.Invalid);
        // The body writer's own bound: exactly the limit is written, one more character is refused.
        var tree = SaveCodec.CompactTree.Parse("{\"a\":[1,2,3]}", 8, canonical: true);
        Assert.Equal("{\"a\":[1,2,3]}", SaveCodec.CompactTree.Write(tree, 16, 13));
        Assert.Throws<SaveValidationException>(() => SaveCodec.CompactTree.Write(tree, 16, 12));
    }

    [Fact]
    public void OneCharacterOverTheInputCapIsRefusedAndKept()
    {
        string payload = SaveCodec.CompactPrefix + new string('0', SaveCodec.MaxInputCharacters + 1 - SaveCodec.CompactPrefix.Length);
        Assert.Equal(SaveCodec.MaxInputCharacters + 1, payload.Length);
        var data = new Dictionary<string, string> { [SaveStore.StateKey] = payload };
        var read = SaveCodec.Read(payload);
        Assert.Equal(SaveReadStatus.Invalid, read.Status);
        Assert.Contains("input size limit", read.Error);
        Assert.Equal(SaveReadStatus.Invalid, SaveStore.ReadFrom(data, "any").Status);
        Assert.Same(payload, data[SaveStore.StateKey]);
    }

    [Fact]
    public void TheWriterChargesItsTablesAgainstTheRecordCapToTheExactRecord()
    {
        var state = CurrentSchemaOnlyTests.City();
        string compact = SaveCodec.Write(state);
        string body = SaveCodec.ExpandCompact(compact);
        int tables = SaveCodec.CompactTableEntries(compact);
        Assert.Equal(compact, SaveCodec.EncodeCompact(body, SaveCodec.MaxEmbeddedRecords - tables));
        var over = Assert.Throws<SaveValidationException>(() => SaveCodec.EncodeCompact(body, SaveCodec.MaxEmbeddedRecords - tables + 1));
        Assert.Contains("compact tables", over.Message);
        var counts = SaveCodec.CountRecords(state, compact);
        var bodyCounts = DevelopmentChain.CountRecords(state, body);
        Assert.Equal(bodyCounts.Streaming + tables, counts.Streaming);
        Assert.Equal(bodyCounts.Semantic + tables, counts.Semantic);
    }

    // ---- Exact number spelling, strings, shared routes and road ranges ----

    private const string EdgeBody = "{\"Schema\":1,"
        + "\"Numbers\":[0,-0,-1,1E-05,0.1,-0.0,12.0,9223372036854775807,-9223372036854775808,92233720368547758070,-92233720368547758090,1.7976931348623157E+308,5e3,-1E-07],"
        + "\"Ints\":[1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,9223372036854775807,-9223372036854775808],"
        + "\"Deltas\":[1000,1001,1002,1003,1004,1005,1006,1007,1008,1009,1010,1011,1012,1013],"
        + "\"Integral\":[1.0,2.0,-3.0,40000.0,5.0,6.0,7.0,8.0,9.0,10.0,11.0,12.0],"
        + "\"Text\":[\"\\u0000\\u001f\\n\\r\\t\\b\\f\\\"\\\\\",\"\\u0085\\u2028\\u2029\",\"\ud83d\ude00 non-BMP\",\"\ud83d\",\"\",\"same\",\"same\",\"\u00e9\u4e2d\"],"
        + "\"Rows\":[{\"Id\":\"a\",\"V\":1,\"Opt\":null},{\"Id\":\"b\",\"V\":2},{\"V\":3,\"Id\":\"a\",\"Extra\":[true,false]},{\"Id\":\"c\",\"V\":null,\"Opt\":\"s\"}],"
        + "\"Tuples\":[[1,\"x\",2.5,[1,2]],[1,\"y\",3.0],[1,\"x\",null,[],{\"k\":\"x\"}]],"
        + "\"Empty\":[],\"EmptyObject\":{},\"Mixed\":[1,\"1\",true,null,{},[]],"
        + "\"Citizens\":[{\"Journey\":{\"Legs\":[{\"Minutes\":1.5,\"ServiceId\":\"native:road:v1:topo:1:2\"},{\"Minutes\":2.0,\"ServiceId\":\"native:road:v1:topo:2:7\"},"
        + "{\"Minutes\":0.5,\"ServiceId\":\"\"},{\"Minutes\":0.5,\"ServiceId\":\"native:road:fragment1:topo:07:8\"},{\"Minutes\":0.5,\"ServiceId\":\"native:road:v1:other:7:9\"},"
        + "{\"Minutes\":0.5,\"ServiceId\":\"native:road:v1:other:9:3\"},{\"Minutes\":0.5,\"ServiceId\":\"native:road:v1:topo:3:4:5\"}]}},"
        + "{\"Journey\":{\"Legs\":[{\"Minutes\":1.5,\"ServiceId\":\"native:road:v1:topo:1:2\"},{\"Minutes\":2.0,\"ServiceId\":\"native:road:v1:topo:2:7\"},"
        + "{\"Minutes\":0.5,\"ServiceId\":\"\"},{\"Minutes\":0.5,\"ServiceId\":\"native:road:fragment1:topo:07:8\"},{\"Minutes\":0.5,\"ServiceId\":\"native:road:v1:other:7:9\"},"
        + "{\"Minutes\":0.5,\"ServiceId\":\"native:road:v1:other:9:3\"},{\"Minutes\":0.5,\"ServiceId\":\"native:road:v1:topo:3:4:5\"}]}},"
        + "{\"Journey\":null},{\"Journey\":{\"Legs\":[]}}],"
        + "\"Vehicles\":[{\"Route\":[{\"LinkId\":\"l1\",\"Start\":0.25},{\"LinkId\":\"l2\",\"Start\":-0.0}]},{\"Route\":[]},{\"Route\":[{\"LinkId\":\"l1\",\"Start\":0.25},{\"LinkId\":\"l2\",\"Start\":-0.0}]}]}";

    [Fact]
    public void NumberSpellingsStringsSharedRoutesAndRoadRangesSurviveExactly()
    {
        var stats = new SaveCodec.CompactStats();
        string payload = SaveCodec.CompactFromBody(EdgeBody, stats);
        output.WriteLine(payload);
        Assert.Equal(EdgeBody, SaveCodec.ExpandCompact(payload));
        Assert.Equal(payload, SaveCodec.CompactFromBody(SaveCodec.ExpandCompact(payload)));
        // Two identical journeys and two identical vehicle routes share one entry each; the empty ones are routes too.
        Assert.Equal(4, stats.Routes);
        Assert.Equal(6, stats.RouteReferences);
        // Road tags are split into a head plus a chained node range; the leading-zero and four-part tags stay plain.
        Assert.Equal(4, stats.RoadTags);
        Assert.Contains("[9,\"1100110\"", payload);
        Assert.Contains("[8,", payload);
        foreach (string spelling in new[] { "[0,-0]", "1E-05", "-0.0", "92233720368547758070", "-92233720368547758090", "1.7976931348623157E+308", "5e3", "-1E-07" })
            Assert.Contains(spelling, payload);
        // Decoding through the strict reader's parser is canonical too: the writer spelling is the only one accepted.
        Assert.Equal(payload, Spell(Envelope(payload)));
    }

    [Fact]
    public void RouteTableAndRoadRangesAreSharedInARealMorningRushCity()
    {
        var state = F7cFixtures.Build("rush-957x2").State;
        var snapshot = SaveCodec.Capture(state);
        string body = SaveCodec.ExpandCompact(SaveCodec.Encode(snapshot));
        var stats = new SaveCodec.CompactStats();
        string payload = SaveCodec.CompactFromBody(body, stats);
        Assert.Equal(SaveCodec.Write(state), payload);
        int journeys = PersonTable.Schedules(state).Count(c => c.Journey != null);
        Assert.True(journeys > 100, "journeys in flight: " + journeys);
        // Resident journeys are Journey fields and go through the route table. Visitor journeys travel inside the
        // writer-33 routine tuple, which the grammar keeps as a positional column.
        Assert.Equal(state.Citizens.Count(c => c.Journey != null), stats.RouteReferences);
        Assert.True(stats.RouteReferences > 50);
        Assert.True(stats.Routes <= journeys);
        Assert.True(snapshot.SharedRoutes >= journeys - snapshot.CopiedRoutes);
        output.WriteLine($"rush-957x2: {journeys} journeys, {stats.Routes} distinct routes, {stats.RoadTags} road tags, nesting {stats.Nesting}, values {stats.Values}");
    }

    // ---- Section 3.1 budgets as offline ceilings ----

    [Theory]
    [InlineData("representative-957x2")]
    [InlineData("fresh-957x3")]
    [InlineData("saturated-957x3")]
    [InlineData("encoding-max")]
    [InlineData("rush-957x2")]
    public void CompactBytesStayInsideTheSection31Ceilings(string fixture)
    {
        var state = F7cFixtures.Build(fixture).State;
        string compact = SaveCodec.Write(state);
        string json33 = SaveCodec.ExpandCompact(compact); // the JSON body the compact wire carries
        int bytes33 = Encoding.UTF8.GetByteCount(json33), bytes = Encoding.UTF8.GetByteCount(compact);
        double ratio = (double)compact.Length / json33.Length;
        var counts = SaveCodec.CountRecords(state, compact);
        int nesting = SaveCodec.CompactTree.Nesting(compact);
        output.WriteLine($"{fixture}: json33 {json33.Length} chars / {bytes33} B; compact {compact.Length} chars / {bytes} B; ratio {ratio:F4}; records {counts.Streaming}/{counts.Semantic}; nesting {nesting}");
        // Typical target at the representative city: at most 35 % of the predecessor and 6 Mi characters.
        if (fixture == "representative-957x2") { Assert.True(ratio <= 0.35, "ratio " + ratio); Assert.True(compact.Length <= 6 * 1024 * 1024); }
        // Every unit fixture stays under the stress ratio (40 %) and is smaller in UTF-8 bytes as well.
        Assert.True(ratio <= 0.40, "ratio " + ratio);
        Assert.True(bytes < bytes33);
        Assert.True(counts.Streaming <= SaveCodec.MaxEmbeddedRecords && counts.Semantic <= SaveCodec.MaxEmbeddedRecords);
        Assert.True(nesting <= SaveCodec.MaxCompactNesting);
    }
}
