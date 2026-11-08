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
/// F7d (issue #94, plan section 4 row F7d, compact-format decision): writer 34 stores candidate C, the columnar compact
/// wire with a shared route table and chained road-tag ranges, encoded from F7c's snapshot. No migration: every older
/// development schema, the JSON writers included, is refused with the distinct "start a new city" status and kept.
/// </summary>
public sealed class F7dCompactWriterTests
{
    private readonly ITestOutputHelper output;
    public F7dCompactWriterTests(ITestOutputHelper output) { this.output = output; }

    private static SaveCodec.CompactObject Envelope(string payload) => (SaveCodec.CompactObject)SaveCodec.CompactTree.Parse(payload, 64, canonical: true)!;
    private static string Spell(object? tree) => SaveCodec.CompactTree.Write(tree, 1 << 16, int.MaxValue);
    private static object? Field(SaveCodec.CompactObject o, string key) => o.Values[Array.IndexOf(o.Keys, key)];
    private static void SetField(SaveCodec.CompactObject o, string key, object? value) => o.Values[Array.IndexOf(o.Keys, key)] = value;

    // ---- Schema and golden writer ----

    [Theory]
    [InlineData(5150, "a3b70408cd5c5f0a23d9e41344d1530283232449f2b9ade5dd37755c92da4a70", 73192)]
    [InlineData(9217, "cb39d7938dfbe874054cf53afb46b660883686adc93d82654e43e7243e317c01", 72830)]
    public void CompactWriterGoldenIsPinnedAndRoundTripsByteForByte(int seed, string pin, int characters)
    {
        Assert.Equal(CityState.ChangeJournalSchema + 1, CityState.CompactSchema);
        Assert.Equal(CityState.CompactSchema, CityState.CurrentSchema);
        Assert.Equal(CityState.ChangeJournalSchema, SaveCodec.LatestDevelopmentSchema);
        var state = ChangeJournalTests.Fixture(seed);
        string payload = SaveCodec.Write(state);
        output.WriteLine($"F7d golden {seed}: {EconomicLedgerTests.Sha(payload)} chars={payload.Length} json33={DevelopmentChain.Write33(state).Length}");
        Assert.StartsWith("{\"Schema\":34,\"f\":\"pcc-c\",\"v\":1,\"k\":[", payload);
        var read = SaveCodec.Read(payload);
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(CityState.CompactSchema, read.SourceSchema);
        Assert.Equal(payload, SaveCodec.Write(read.State!));
        Assert.Equal(payload, SaveCodec.Encode(SaveCodec.Capture(state)));
        Assert.Equal(pin, EconomicLedgerTests.Sha(payload));
        Assert.Equal(characters, payload.Length);
    }

    [Fact]
    public void CompactBodyIsExactlyThePredecessorBodyAndTheWriter33ReaderStaysFrozen()
    {
        var state = ChangeJournalTests.Fixture(5150);
        string json33 = DevelopmentChain.Write33(state);
        string compact = SaveCodec.Write(state);
        Assert.Equal(ReaderCorpus.Relabel(json33, 33, 34), SaveCodec.ExpandCompact(compact));
        var frozen = SaveCodec.ReadWriter33(json33);
        Assert.True(frozen.Status == SaveReadStatus.Loaded, frozen.Error);
        Assert.Equal(json33, DevelopmentChain.Write33(frozen.State!));
        // The frozen reader sees the compact wire as a newer schema, exactly as an older build does.
        Assert.Equal(SaveReadStatus.Future, SaveCodec.ReadWriter33(compact).Status);
        Assert.Equal(34, SaveCodec.ReadWriter33(compact).SourceSchema);
        Assert.Null(SaveCodec.DevelopmentSchemaOf(compact));
        SaveCodec.RequireExecutableCapabilities(compact);
    }

    // ---- Fresh-seed state comparison against the predecessor JSON writer ----

    public static IEnumerable<object[]> FreshSeeds() => new[]
    {
        new object[] { "journal-5150" }, new object[] { "journal-9217" }, new object[] { "rush-957x2" }, new object[] { "encoding-max" },
    };

    private static CityState Build(string name) => name.StartsWith("journal-", StringComparison.Ordinal)
        ? ChangeJournalTests.Fixture(int.Parse(name.Substring(8))) : F7cFixtures.Build(name).State;

    [Theory]
    [MemberData(nameof(FreshSeeds))]
    public void SameSeededCityDecodesFieldForFieldFromCompactAndFromThePredecessorJsonWriter(string fixture)
    {
        var state = Build(fixture);
        string json33 = DevelopmentChain.Write33(state);
        string compact = SaveCodec.Write(state);
        var fromJson = SaveCodec.ReadWriter33(json33);
        var fromCompact = SaveCodec.Read(compact);
        Assert.True(fromJson.Status == SaveReadStatus.Loaded, fromJson.Error);
        Assert.True(fromCompact.Status == SaveReadStatus.Loaded, fromCompact.Error);
        var a = fromJson.State!; var b = fromCompact.State!;
        Assert.Equal(33, a.Schema); Assert.Equal(34, b.Schema);
        a.Schema = 34;
        var audit = GraphAudit.Compare(a, b);
        Assert.True(audit.Differences.Count == 0, string.Join("; ", audit.Differences));
        // IDs and money are exact against the live city, not only against each other.
        Assert.Equal(state.Citizens.Select(c => c.Identity.Id), b.Citizens.Select(c => c.Identity.Id));
        Assert.Equal(state.Visitors.Select(v => v.Id), b.Visitors.Select(v => v.Id));
        Assert.Equal(state.Places.Select(p => p.Id), b.Places.Select(p => p.Id));
        if (state.Food != null)
        {
            Assert.Equal(state.Food.Residents.Select(m => m.Budget?.Balance), b.Food!.Residents.Select(m => m.Budget?.Balance));
            Assert.Equal(state.Food.Residents.Select(m => m.Decision?.IntentId), b.Food.Residents.Select(m => m.Decision?.IntentId));
            Assert.Equal(state.Food.BusinessReferences.Select(r => r.BusinessId), b.Food.BusinessReferences.Select(r => r.BusinessId));
        }
        Assert.Equal(PersonTable.Schedules(state).Select(c => c.Journey?.Minutes), PersonTable.Schedules(b).Select(c => c.Journey?.Minutes));
        // Same state hash: both decoded cities write the same predecessor bytes and the same compact bytes.
        a.Schema = 33;
        Assert.Equal(json33, DevelopmentChain.Write33(a));
        Assert.Equal(json33, DevelopmentChain.Write33(b));
        Assert.Equal(compact, SaveCodec.Write(b));
        Assert.Equal(ReaderCorpus.Relabel(json33, 33, 34), SaveCodec.ExpandCompact(compact));
    }

    [Fact]
    public void CompactReadInternsEveryStringValueOncePerDistinctText()
    {
        var state = F7cFixtures.Build("rush-957x2").State;
        var json = SaveCodec.ReadWriter33(DevelopmentChain.Write33(state)).State!;
        var compact = SaveCodec.Read(SaveCodec.Write(state)).State!;
        static (int Instances, int Texts) Homes(CityState city)
        {
            var instances = new HashSet<string>(ReferenceEqualityComparer.Instance);
            foreach (var c in city.Citizens) instances.Add(c.HomeId);
            return (instances.Count, city.Citizens.Select(c => c.HomeId).Distinct(StringComparer.Ordinal).Count());
        }
        var fromCompact = Homes(compact); var fromJson = Homes(json);
        Assert.Equal(fromCompact.Texts, fromCompact.Instances);
        Assert.True(fromJson.Instances > fromJson.Texts, "the JSON reader is expected to allocate one string per token");
        output.WriteLine($"home id instances: json {fromJson.Instances}, compact {fromCompact.Instances}, distinct {fromCompact.Texts}");
    }

    // ---- No migration: older development cities ----

    public static IEnumerable<object[]> OlderWriters() => new[]
    {
        new object[] { 33 }, new object[] { 32 }, new object[] { 31 }, new object[] { 30 }, new object[] { 29 },
    };

    [Theory]
    [MemberData(nameof(OlderWriters))]
    public void OlderJsonWriterCityIsRefusedWithTheStartANewCityStatusAndKept(int schema)
    {
        var state = schema >= 32 ? ChangeJournalTests.Fixture(5150) : CurrentSchemaOnlyTests.City();
        string older = schema switch
        {
            33 => DevelopmentChain.Write33(state), 32 => DevelopmentChain.WriteAttendance(state), 31 => DevelopmentChain.WriteLedger(state),
            30 => DevelopmentChain.Write30(state), _ => DevelopmentChain.Write29(state),
        };
        Assert.Equal(schema, SaveCodec.DevelopmentSchemaOf(older));
        var data = new Dictionary<string, string>(StringComparer.Ordinal)
        { [SaveStore.StateKey] = older, ["persistentcity:food-sale-v2:intent-1"] = "pending", ["other"] = "kept" };
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
        Assert.Equal(CityState.CompactSchema, state.Schema);
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

    private static string Variant(string payload, Action<SaveCodec.CompactObject> edit)
    {
        var top = Envelope(payload);
        edit(top);
        return Spell(top);
    }

    [Fact]
    public void FutureUnknownMalformedDanglingAndOverboundCompactPayloadsAreRefusedAndKept()
    {
        string valid = Small();
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(valid).Status);
        Assert.Equal(valid, Spell(Envelope(valid)));
        var top = Envelope(valid);
        Assert.True(Columns(Field(top, "t"), 8).Count > 0, "the rush city has route references");

        var refusals = new (string Name, string Payload, SaveReadStatus Status)[]
        {
            ("future schema", valid.Replace("{\"Schema\":34,", "{\"Schema\":35,"), SaveReadStatus.Future),
            ("far future schema", valid.Replace("{\"Schema\":34,", "{\"Schema\":99,"), SaveReadStatus.Future),
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
            ("bare JSON body in writer 34", SaveCodec.ExpandCompact(valid), SaveReadStatus.Invalid),
            ("body schema mismatch", SaveCodec.CompactFromBody(SaveCodec.ExpandCompact(valid).Replace("{\"Schema\":34,", "{\"Schema\":33,")), SaveReadStatus.Invalid),
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
            EconomicLedgerTests.Refused(payload, status);
        }
    }

    [Fact]
    public void ACompactCityThatExpandsPastTheInputCapIsRefusedBeforeItsBodyIsMaterialized()
    {
        // One route holding a 1 Mi-character string, referenced 257 times: 1,001 characters of compact text, more than
        // 256 Mi characters of body. The reader stops writing the body at the cap.
        string big = new string('x', 1 << 20);
        var refs = string.Join(",", Enumerable.Repeat("0", 257));
        string payload = "{\"Schema\":34,\"f\":\"pcc-c\",\"v\":1,\"k\":[\"Citizens\",\"Journey\",\"Legs\",\"Schema\"],\"h\":[[0,3],[1],[2]],\"s\":[],"
            + "\"n\":1,\"r\":[4,2,[5,1,[1,\"" + big + "\"]]],\"t\":[4,0,[5,257,[4,1,[8," + refs + "]]],[0,34]]}";
        var read = SaveCodec.Read(payload);
        Assert.Equal(SaveReadStatus.Invalid, read.Status);
        Assert.Contains("input size limit", read.Error);
        EconomicLedgerTests.Refused(payload);
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
        var bodyCounts = DevelopmentChain.CountRecords(state, DevelopmentChain.Write33(state));
        Assert.Equal(bodyCounts.Streaming + tables, counts.Streaming);
        Assert.Equal(bodyCounts.Semantic + tables, counts.Semantic);
    }

    // ---- Exact number spelling, strings, shared routes and road ranges ----

    private const string EdgeBody = "{\"Schema\":34,"
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
        string json33 = DevelopmentChain.Write33(state);
        string compact = SaveCodec.Write(state);
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
