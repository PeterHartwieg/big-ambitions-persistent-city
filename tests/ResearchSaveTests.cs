using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

/// <summary>C5c: CityState.Research in the next-free writer (plan section 5). The ledger round-trips byte for byte; every
/// out-of-bound, unknown or non-canonical value refuses the payload and keeps it; an older development city is refused
/// with the "start a new city" status and kept; the writer's bytes are pinned.</summary>
public sealed class ResearchSaveTests
{
    private const double OrderMinute = 1440 + 8 * 60 + 15.25;
    private readonly ITestOutputHelper output;
    public ResearchSaveTests(ITestOutputHelper output) { this.output = output; }

    // ---- Fixtures ----

    /// <summary>A real C4 report for the order: the engine's own cell layout, query key and logical day.</summary>
    private static MarketResearchReport Report(ResearchOrder order, int consumers = 40)
    {
        var source = new ResearchFixture(consumers)
        { Identity = new MarketResearchIdentity("c5c:" + order.Id, (int)Math.Floor(order.SurveyAtMinute / 1440), 1, 1, 1) };
        var scope = order.Product.Coverage == ResearchCoverage.City ? new MarketResearchScope()
            : new MarketResearchScope(order.BusinessAddress, new Position(10, 0, 10), order.RadiusMetres);
        return ResearchFixture.Finish(source, 256, order.Product.Tier, scope);
    }

    private static ResearchOrder Place(ResearchLedger ledger, string business, string code, bool tax = false, double minute = OrderMinute,
        string agency = "agency:7")
    {
        Assert.Equal(ResearchOrderRefusal.None, ledger.TryOrder(code, business, agency, tax, 500, minute, out var order));
        return order!;
    }

    private static ResearchOrder Seal(ResearchLedger ledger, ResearchOrder order)
    {
        Assert.Equal(ResearchMoveRefusal.None, ledger.TrySeal(order.Id, Report(order)));
        return order;
    }

    /// <summary>Every state: open Ordered and Surveyed; 16 kept Paid; closed Declined (both reasons), Cancelled,
    /// PaymentUnknown and Replaced (the 17th delivery replaces the oldest report).</summary>
    internal static ResearchLedger AllStates()
    {
        var ledger = new ResearchLedger();
        var products = ResearchProducts.All;
        for (int i = 0; i < 17; i++)
        {
            var paid = Seal(ledger, Place(ledger, "address:paid:" + i, products[i % products.Count].Code, tax: i % 2 == 0, minute: OrderMinute + i * 37.5));
            Assert.Equal(ResearchMoveRefusal.None, ledger.TryMarkPaid(paid.Id));
        }
        Assert.Equal(ResearchMoveRefusal.None, ledger.TryDecline(Seal(ledger, Place(ledger, "address:poor", ResearchProducts.DetailedCityCode)).Id, ResearchDeclineReason.InsufficientFunds));
        Assert.Equal(ResearchMoveRefusal.None, ledger.TryDecline(Seal(ledger, Place(ledger, "address:rich", ResearchProducts.BasicLocalCode)).Id, ResearchDeclineReason.BalancePrecision));
        Assert.Equal(ResearchMoveRefusal.None, ledger.TryCancel(Seal(ledger, Place(ledger, "address:sold", ResearchProducts.DetailedLocalCode)).Id));
        Assert.Equal(ResearchMoveRefusal.None, ledger.TryMarkPaymentUnknown(Seal(ledger, Place(ledger, "address:threw", ResearchProducts.BasicLocalCode)).Id));
        Place(ledger, "address:waiting", ResearchProducts.DetailedLocalCode, minute: 3 * 1440 + 59.75);
        Seal(ledger, Place(ledger, "address:billing", ResearchProducts.DetailedCityCode, tax: true));
        return ledger;
    }

    /// <summary>The section at its bounds: 8 Surveyed open orders, 16 kept and 16 closed. Every order is DetailedLocal,
    /// the widest sold tier (62 cells) whose report also carries the business address, and every business and agency
    /// address has the maximum length.</summary>
    internal static ResearchLedger AtBounds(int addressLength = ResearchLedger.MaximumAddressLength)
    {
        var ledger = new ResearchLedger();
        string Address(string kind, int i) => (kind + ":" + i.ToString("D3", CultureInfo.InvariantCulture) + ":").PadRight(addressLength, 'x');
        for (int i = 0; i < ResearchLedger.MaximumClosedOrders; i++)
            Assert.Equal(ResearchMoveRefusal.None, ledger.TryMarkPaymentUnknown(Seal(ledger, Place(ledger, Address("closed", i), ResearchProducts.DetailedLocalCode, agency: Address("agency", i))).Id));
        for (int i = 0; i < ResearchLedger.MaximumKeptReports; i++)
            Assert.Equal(ResearchMoveRefusal.None, ledger.TryMarkPaid(Seal(ledger, Place(ledger, Address("kept", i), ResearchProducts.DetailedLocalCode, agency: Address("agency", i))).Id));
        for (int i = 0; i < ResearchLedger.MaximumOpenOrders; i++)
            Seal(ledger, Place(ledger, Address("open", i), ResearchProducts.DetailedLocalCode, agency: Address("agency", i)));
        foreach (var order in ledger.Open.Concat(ledger.Kept).Concat(ledger.Closed))
            Assert.Equal((addressLength, addressLength), (order.BusinessAddress.Length, order.AgencyAddress.Length));
        foreach (var order in ledger.Open.Concat(ledger.Kept))
            Assert.Equal((62, addressLength), (order.Report!.FixtureCells.Count, order.Report.Address!.Length));
        Assert.Equal((8, 16, 16), (ledger.Open.Count, ledger.Kept.Count, ledger.Closed.Count));
        return ledger;
    }

    private static CityState City(ResearchLedger? ledger = null)
    {
        var city = CurrentSchemaOnlyTests.City();
        if (ledger != null) city.Research = ledger;
        return city;
    }

    private static string Sha(string text) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private static void RefusedAndPreserved(string payload, SaveReadStatus expected, string? error = null)
    {
        payload = SaveWire.Payload(payload);
        var replacement = City(AllStates());
        var read = SaveWire.Read(payload);
        Assert.True(read.Status == expected, read.Status + ": " + read.Error); Assert.Null(read.State);
        if (error != null) Assert.Contains(error, read.Error);
        var data = new Dictionary<string, string> { [SaveStore.StateKey] = payload, ["rlo:state"] = "kept", [FoodAuthorityStore.Key] = "manifest" };
        Assert.Equal(expected, SaveSession.Open(data, replacement.PopulationKey).InitialReadResult.Status);
        Assert.False(SaveStore.WriteTo(data, replacement).Success);
        Assert.False(SaveSession.Open(data, replacement.PopulationKey).WriteTo(data, replacement).Success);
        Assert.Same(payload, data[SaveStore.StateKey]); Assert.Equal("kept", data["rlo:state"]); Assert.Equal("manifest", data[FoodAuthorityStore.Key]);
    }

    /// <summary>The payload with one change to its research section, written compactly like the writer.</summary>
    private static string Mutate(string payload, Action<JArray> change)
    {
        var root = SaveWire.Parse(payload, new JsonLoadSettings { LineInfoHandling = LineInfoHandling.Ignore });
        change((JArray)root["Research"]!);
        return root.ToString(Formatting.None);
    }

    private static JArray Open(JArray research) => (JArray)research[2];
    private static JArray Kept(JArray research) => (JArray)research[3];
    private static JArray Closed(JArray research) => (JArray)research[4];
    private static JArray Cells(JToken order) => (JArray)((JArray)order[13]!)[6]!;

    [Fact]
    public void AFreshCityHasAnEmptyLedger()
    {
        string payload = SaveWire.Text(City());
        Assert.Contains("\"Research\":[1,1,[],[],[]]", payload);
        var read = SaveWire.Read(payload); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(1, read.State!.Research.NextSequence);
        Assert.False(read.State.Research.HasOpenOrders);
    }

    // ---- Round trip ----

    [Fact]
    public void EveryOrderStateRoundTripsByteForByte()
    {
        var ledger = AllStates();
        string payload = SaveWire.Text(City(ledger));
        var read = SaveWire.Read(payload); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(payload, SaveWire.Text(read.State!));
        var restored = read.State!.Research;
        Assert.Equal(ledger.NextSequence, restored.NextSequence);
        var states = restored.Open.Concat(restored.Kept).Concat(restored.Closed).Select(o => o.State).Distinct().OrderBy(s => s).ToArray();
        Assert.Equal(Enum.GetValues(typeof(ResearchOrderState)).Cast<ResearchOrderState>().ToArray(), states);
        foreach (var (before, after) in ledger.Open.Concat(ledger.Kept).Concat(ledger.Closed).Zip(restored.Open.Concat(restored.Kept).Concat(restored.Closed)))
        {
            Assert.Equal((before.Id, before.ProductCode, before.Price, before.BusinessAddress, before.AgencyAddress, before.TaxDeductible, before.RadiusMetres),
                (after.Id, after.ProductCode, after.Price, after.BusinessAddress, after.AgencyAddress, after.TaxDeductible, after.RadiusMetres));
            Assert.Equal((before.OrderMinute, before.SurveyAtMinute, before.State, before.DeclineReason, before.ReportId),
                (after.OrderMinute, after.SurveyAtMinute, after.State, after.DeclineReason, after.ReportId));
            Assert.Equal(before.Report == null, after.Report == null);
            if (before.Report == null) continue;
            Assert.Equal((before.Report.Id, before.Report.Tier, before.Report.LogicalDay, before.Report.Address, before.Report.RadiusMetres),
                (after.Report!.Id, after.Report.Tier, after.Report.LogicalDay, after.Report.Address, after.Report.RadiusMetres));
            Assert.Equal(before.Report.FixtureCells.Select(c => (c.Metric, c.Band, c.Value)), after.Report.FixtureCells.Select(c => (c.Metric, c.Band, c.Value)));
        }
        // The restored ledger is the entitlement: a Paid report reads, a Replaced one does not, an open one does not.
        var paid = restored.Kept[0];
        Assert.True(paid.Report!.TryRead(restored, paid.Id, out var numbers)); Assert.Equal(paid.Report.FixtureCells.Count, numbers!.Count);
        var replaced = restored.Closed.Single(o => o.State == ResearchOrderState.Replaced);
        Assert.Null(replaced.Report); Assert.True(restored.TryGet(replaced.Id, out var grant) && grant!.Replaced);
        var surveyed = restored.Open.Single(o => o.State == ResearchOrderState.Surveyed);
        Assert.False(surveyed.Report!.TryRead(restored, surveyed.Id, out _));
        // A new order after reload takes the next sequence; no ID is reused.
        Assert.Equal(ResearchOrderRefusal.None, restored.TryOrder(ResearchProducts.BasicLocalCode, "address:new", "agency:7", false, 500, OrderMinute, out var next));
        Assert.Equal("pcr-" + ledger.NextSequence.ToString(CultureInfo.InvariantCulture), next!.Id);
    }

    [Fact]
    public void TheSectionAtItsBoundsRoundTripsAndStaysUnderSixtyFourKilobytes()
    {
        var empty = SaveWire.Text(City());
        string payload = SaveWire.Text(City(AtBounds()));
        var read = SaveWire.Read(payload); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(payload, SaveWire.Text(read.State!));
        int section = Encoding.UTF8.GetByteCount(payload) - Encoding.UTF8.GetByteCount(empty);
        output.WriteLine("research section at bounds: " + section + " bytes over the empty ledger");
        // 24 report-holding orders of 62 sold cells plus 16 closed rows, with three 256-character addresses per report-holding
        // order and two per closed one. The plan's "well under 20 KB" assumed short addresses (docs/perf/c5c.md). Cell
        // values up to the 150,000-consumer cap add at most 4 digits to each of the 1,488 cells, about 6 KB more.
        Assert.True(section + 4 * 62 * 24 < 64 * 1024, section + " bytes");
        // Both record counters charge one record per order.
        var records = SaveCodec.CountRecords(read.State!, payload);
        var baseline = SaveCodec.CountRecords(SaveWire.Read(empty).State!, empty);
        Assert.Equal(40, records.Streaming - baseline.Streaming);
        Assert.Equal(40, records.Semantic - baseline.Semantic);
    }

    // ---- Strict refusal ----

    // JToken indexers are nullable; every slot below exists in the AllStates payload.
#nullable disable
    public static IEnumerable<object[]> Refusals() => new (string Name, Action<JArray> Change)[]
    {
        ("unknown section version", r => r[0] = 2),
        ("next sequence zero", r => r[1] = 0),
        ("surplus section slot", r => r.Add(0)),
        ("missing section slot", r => r.RemoveAt(4)),
        ("unknown product code", r => Open(r)[0][2] = "in-depth-city"),
        ("empty product code", r => Open(r)[0][2] = ""),
        ("unknown state", r => Closed(r)[0][10] = 7),
        ("negative state", r => Closed(r)[0][10] = -1),
        ("state in the wrong list", r => Kept(r)[0][10] = (int)ResearchOrderState.Surveyed),
        ("unknown decline reason", r => Closed(r)[1][11] = 3),
        ("decline reason without decline", r => Closed(r)[3][11] = 1),
        ("decline without reason", r => Closed(r).First(o => (int)o[10] == (int)ResearchOrderState.Declined)[11] = 0),
        ("cells over 64", r => { var cells = Cells(Kept(r)[1]); while (cells.Count <= 64) cells.Add(new JArray(0, 0, 0)); }),
        ("cell count differs from tier", r => Cells(Kept(r)[0]).Add(new JArray(13, 1, 0))),
        ("cell missing", r => Cells(Kept(r)[0]).RemoveAt(12)),
        ("unknown metric", r => Cells(Kept(r)[0])[0][0] = 21),
        ("unknown band", r => Cells(Kept(r)[0])[0][1] = 12),
        ("cell off the C4 layout", r => Cells(Kept(r)[0])[5][1] = (int)MarketResearchBand.Unfunded),
        ("negative cell value", r => Cells(Kept(r)[0])[0][2] = -1),
        ("cell value over the consumer cap", r => Cells(Kept(r)[0])[0][2] = 150001),
        ("cell value as text", r => Cells(Kept(r)[0])[0][2] = "7"),
        ("cell value fractional", r => Cells(Kept(r)[0])[0][2] = 7.5),
        ("surplus cell slot", r => ((JArray)Cells(Kept(r)[0])[0]).Add(0)),
        ("open orders over 8", r => { for (int i = 0; i < 7; i++) Open(r).Add(Open(r)[0].DeepClone()); }),
        ("kept orders over 16", r => Kept(r).Add(Kept(r)[0].DeepClone())),
        ("closed orders over 16", r => { while (Closed(r).Count <= 16) Closed(r).Add(Closed(r)[0].DeepClone()); }),
        ("duplicate sequence across lists", r => Closed(r)[0][1] = Kept(r)[0][1]),
        ("reused sequence at the next sequence", r => Open(r)[1][1] = r[1]),
        ("next sequence below a saved order", r => r[1] = (long)Open(r)[1][1]),
        ("open orders out of sequence", r => { var first = Open(r)[0]; Open(r).RemoveAt(0); Open(r).Add(first); }),
        ("two open orders for one business", r => Open(r)[1][4] = Open(r)[0][4]),
        ("duplicate report ID", r => { Kept(r)[1][12] = Kept(r)[0][12]; ((JArray)Kept(r)[1][13])[1] = Kept(r)[0][12]; }),
        ("price differs from the product", r => Open(r)[0][3] = (long)Open(r)[0][3] + 1),
        ("price as a fraction", r => Open(r)[0][3] = 500.0),
        ("price as text", r => Open(r)[0][3] = "500"),
        ("radius over 2000", r => Open(r)[0][7] = 2001),
        ("catchment radius below 50", r => Open(r)[0][7] = 49),
        ("city radius not zero", r => Open(r)[1][7] = 500),
        ("survey minute not the accounting target", r => Open(r)[0][9] = (double)Open(r)[0][9] + 1),
        ("negative order minute", r => Open(r)[0][8] = -1.0),
        ("business address over 256", r => Open(r)[0][4] = new string('a', 257)),
        ("blank agency address", r => Open(r)[0][5] = " "),
        ("tax flag as an integer", r => Open(r)[0][6] = 1),
        ("ordered order with a report ID", r => Open(r)[0][12] = Kept(r)[0][12]),
        ("surveyed order without a report", r => Open(r)[1][13] = JValue.CreateNull()),
        ("closed order keeping a report", r => Closed(r)[0][13] = Kept(r)[0][13].DeepClone()),
        ("report ID differs from its order", r => Kept(r)[0][12] = Kept(r)[1][12]),
        ("malformed report ID", r => { string id = "q-" + new string('a', 42); Kept(r)[0][12] = id; ((JArray)Kept(r)[0][13])[1] = id; }),
        ("report tier differs from product", r => ((JArray)Kept(r)[0][13])[2] = 2),
        ("report day differs from survey minute", r => ((JArray)Kept(r)[0][13])[3] = (int)((JArray)Kept(r)[0][13])[3] + 1),
        ("report address differs from business", r => ((JArray)Kept(r)[0][13])[4] = "address:elsewhere"),
        ("report radius differs from order", r => ((JArray)Kept(r)[0][13])[5] = 600),
        ("unknown report version", r => ((JArray)Kept(r)[0][13])[0] = 0),
        ("unknown order version", r => Open(r)[0][0] = 2),
        ("surplus order slot", r => ((JArray)Open(r)[0]).Add(0)),
        ("order as an object", r => Open(r)[0] = new JObject()),
    }.Select(row => new object[] { row.Name, row.Change });
#nullable restore

    [Theory]
    [MemberData(nameof(Refusals))]
    public void AnyOutOfBoundUnknownOrInconsistentValueIsRefusedAndKept(string name, Action<JArray> change)
    {
        string original = SaveWire.Text(City(AllStates()));
        Assert.Equal(original, Mutate(original, _ => { }));
        string payload = Mutate(original, change);
        Assert.NotEqual(original, payload);
        Assert.Equal(SaveReadStatus.Invalid, SaveWire.Read(payload).Status);
        RefusedAndPreserved(payload, SaveReadStatus.Invalid);
        Assert.False(string.IsNullOrEmpty(name));
    }

    /// <summary>Money and integers must be the writer's own canonical tokens; Json.NET would otherwise read "010" as
    /// octal 8, round fractions and accept hexadecimal.</summary>
    [Theory]
    [InlineData("price", "{0}.0")] [InlineData("price", "0{0}")] [InlineData("price", "{0}e0")] [InlineData("price", "+{0}")]
    [InlineData("sequence", "0{0}")] [InlineData("sequence", "0x{0}")] [InlineData("radius", "0{0}")] [InlineData("cell", "0{0}")]
    [InlineData("next", "0{0}")] [InlineData("next", "{0}.0")]
    public void NonCanonicalNumbersAreRefusedAndKept(string slot, string form)
    {
        var ledger = AllStates();
        string payload = SaveWire.Text(City(ledger));
        var order = ledger.Open[0];
        string original = slot switch
        {
            "price" => "[1," + order.Sequence + ",\"" + order.ProductCode + "\"," + order.Price.Units.ToString(CultureInfo.InvariantCulture) + ",",
            "sequence" => "[1," + order.Sequence + ",\"" + order.ProductCode + "\",",
            "radius" => "\"agency:7\",false,500,",
            "cell" => "[[0,0,40],",
            _ => "\"Research\":[1," + ledger.NextSequence + ",",
        };
        Assert.Contains(original, payload);
        string number = slot switch
        {
            "price" => order.Price.Units.ToString(CultureInfo.InvariantCulture), "sequence" => order.Sequence.ToString(CultureInfo.InvariantCulture),
            "radius" => "500", "cell" => "40", _ => ledger.NextSequence.ToString(CultureInfo.InvariantCulture),
        };
        int at = original.LastIndexOf(number, StringComparison.Ordinal);
        string replaced = original.Substring(0, at) + string.Format(CultureInfo.InvariantCulture, form, number) + original.Substring(at + number.Length);
        string variant = ReplaceFirst(payload, original, replaced);
        Assert.NotEqual(payload, variant);
        RefusedAndPreserved(variant, SaveReadStatus.Invalid);
    }

    private static string ReplaceFirst(string text, string from, string to)
    {
        int at = text.IndexOf(from, StringComparison.Ordinal);
        return text.Substring(0, at) + to + text.Substring(at + from.Length);
    }

    [Fact]
    public void AMissingNullOrDuplicatedSectionIsRefusedAndKept()
    {
        string payload = SaveWire.Text(City(AllStates()));
        var root = SaveWire.Parse(payload);
        var missing = (JObject)root.DeepClone(); missing.Remove("Research");
        RefusedAndPreserved(missing.ToString(Formatting.None), SaveReadStatus.Invalid);
        var empty = (JObject)root.DeepClone(); empty["Research"] = JValue.CreateNull();
        RefusedAndPreserved(empty.ToString(Formatting.None), SaveReadStatus.Invalid);
        var asObject = (JObject)root.DeepClone(); asObject["Research"] = new JObject { ["NextSequence"] = 1 };
        RefusedAndPreserved(asObject.ToString(Formatting.None), SaveReadStatus.Invalid);
        string section = "\"Research\":" + root["Research"]!.ToString(Formatting.None);
        RefusedAndPreserved(payload.Replace(section, section + "," + section), SaveReadStatus.Invalid);
    }

    /// <summary>The writer runs the same rules: a ledger holding a report that C4 could not have produced is not written,
    /// and the stored city is not replaced.</summary>
    [Fact]
    public void TheWriterRefusesALedgerTheReaderWouldRefuse()
    {
        var ledger = new ResearchLedger();
        var order = Place(ledger, "address:a", ResearchProducts.BasicLocalCode);
        var cells = new[] { new MarketResearchCell(MarketResearchMetric.Consumers, MarketResearchBand.All, 7) };
        Assert.Equal(ResearchMoveRefusal.None, ledger.TrySeal(order.Id, new MarketResearchReport("q-short", MarketResearchTier.Basic, 1, "address:a", 500, cells)));
        var city = City(ledger);
        Assert.Throws<SaveValidationException>(() => SaveWire.Text(city));
        Assert.Throws<SaveValidationException>(() => SaveCodec.ValidateForActivation(city));
        var data = new Dictionary<string, string>();
        Assert.False(SaveStore.WriteTo(data, city).Success);
        Assert.False(data.ContainsKey(SaveStore.StateKey));
        city.Research = null!;
        Assert.Throws<SaveValidationException>(() => SaveWire.Text(city));
    }

    // ---- Older development cities ----

    /// <summary>A city with another schema number is refused with the "start a new city" status through codec, store,
    /// session and the early probe, and every key is kept.</summary>
    [Fact]
    public void AnOlderDevelopmentCityIsRefusedWithTheStartANewCityStatusAndKept()
    {
        var city = City(AllStates());
        // Older development builds wrote 2 to 36; the research section does not make any of them readable.
        foreach (int older in new[] { 28, 36, 37 })
        {
            string payload = SaveWire.Text(city).Replace("\"Schema\":" + CityState.CurrentSchema + ",", "\"Schema\":" + older + ",");
            var read = SaveWire.Read(payload);
            Assert.Equal(SaveReadStatus.DevelopmentSchema, read.Status); Assert.Equal(older, read.SourceSchema);
            Assert.Contains("start a new city", read.Error);
            RefusedAndPreserved(payload, SaveReadStatus.DevelopmentSchema, "start a new city");
            Assert.Equal(older, SaveCodec.DevelopmentSchemaOf(payload));
            DevelopmentChain.RequireRefusedCapabilities(payload);
        }
    }

    // ---- Golden writer ----

    private static IEnumerable<(string Name, Func<CityState> Build)> GoldenFixtures() => new (string, Func<CityState>)[]
    {
        ("research-all-states", () => City(AllStates())),
        ("research-at-bounds", () => City(AtBounds())),
        ("banded-visitors-factor2-30h-research", BandedCity),
    };

    /// <summary>F5e's banded visitor city after 30 hours, holding every research state.</summary>
    private static CityState BandedCity()
    {
        var (state, market) = WorkplaceCapacityTests.Fresh();
        var city = new CitySimulator(state, new FreshCityTraceTests.Walk());
        city.AttachFoodMarket(market);
        double target = state.Minute + 30 * 60;
        while (!city.AdvanceTo(target, 64).CaughtUp) { }
        city.State.Research = AllStates();
        return city.State;
    }

    /// <summary>The writer's golden for each research fixture, re-pinned once by X3 at the held schema 1. A deliberate
    /// format change re-pins it in place (docs/decisions/2026-11-09-schema-one.md).</summary>
    [Theory]
    [InlineData("research-all-states", "58aec060f7aa3de3eeec12d572773ea70d6a3f297c266d4907391f0a92f15b8a", 13620)]
    [InlineData("research-at-bounds", "6b990fb796bcbaad391d8ac839170da96bca863e7b6cd5be8f7e1f005e9fb08a", 48271)]
    [InlineData("banded-visitors-factor2-30h-research", "fb8cd93bde5ea64f81e8457da2b4976f64eed90a89582a9ff2895e5e7c584944", 224665)]
    public void GoldenResearchWriterBytesArePinned(string name, string sha256, int characters)
    {
        string payload = SaveCodec.Write(GoldenFixtures().Single(f => f.Name == name).Build());
        var read = SaveCodec.Read(payload); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(payload, SaveCodec.Write(read.State!));
        string actual = Sha(payload) + " " + payload.Length;
        Assert.True(sha256 + " " + characters == actual, "golden " + name + ": " + actual);
    }

    [Fact]
    public void GoldenFixturesAreDeterministic()
    {
        foreach (var (_, build) in GoldenFixtures()) Assert.Equal(SaveCodec.Write(build()), SaveCodec.Write(build()));
    }

    /// <summary>The codec's cell table is C4's: every tier's engine report has exactly that layout and length.</summary>
    [Theory]
    [InlineData(MarketResearchTier.Basic, 13)] [InlineData(MarketResearchTier.Detailed, 62)] [InlineData(MarketResearchTier.InDepth, 64)]
    public void TheSavedCellLayoutIsTheEngines(MarketResearchTier tier, int length)
    {
        var report = ResearchFixture.Finish(new ResearchFixture(30), 256, tier);
        Assert.Equal(length, report.FixtureCells.Count);
        Assert.Equal(length, SaveCodec.ResearchCellCount29(tier));
        Assert.True(length <= SaveCodec.MaxResearchCells);
        for (int i = 0; i < length; i++)
            Assert.Equal(SaveCodec.ResearchCell29(i), (report.FixtureCells[i].Metric, report.FixtureCells[i].Band));
        Assert.Equal(SaveCodec.ResearchReportIdLength, report.Id.Length);
    }

    // ---- Measurement (docs/perf/c5c.md) ----

    /// <summary>Save and load cost of the section at its bounds, as the difference between a minimal city with the
    /// bounded ledger and the same city with an empty ledger. Reported, not gated: it runs only when PC_C5C_MEASURE is set.</summary>
    [Fact]
    public void MeasureSectionCostAtBounds()
    {
        if (PerfGate.Variable("PC_C5C_MEASURE") == null) return;
        var full = City(AtBounds()); var empty = City();
        string fullPayload = SaveWire.Text(full), emptyPayload = SaveWire.Text(empty);
        for (int i = 0; i < 200; i++) { SaveWire.Read(SaveWire.Text(full)); SaveWire.Read(SaveWire.Text(empty)); }
        const int runs = 2000;
        var write = new double[runs]; var read = new double[runs]; var writeEmpty = new double[runs]; var readEmpty = new double[runs];
        long allocFull = 0, allocEmpty = 0;
        for (int i = 0; i < runs; i++)
        {
            long a0 = GC.GetAllocatedBytesForCurrentThread();
            var watch = Stopwatch.StartNew(); SaveWire.Text(full); write[i] = watch.Elapsed.TotalMilliseconds;
            watch.Restart(); var loaded = SaveWire.Read(fullPayload); read[i] = watch.Elapsed.TotalMilliseconds;
            long a1 = GC.GetAllocatedBytesForCurrentThread();
            watch.Restart(); SaveWire.Text(empty); writeEmpty[i] = watch.Elapsed.TotalMilliseconds;
            watch.Restart(); SaveWire.Read(emptyPayload); readEmpty[i] = watch.Elapsed.TotalMilliseconds;
            long a2 = GC.GetAllocatedBytesForCurrentThread();
            allocFull += a1 - a0; allocEmpty += a2 - a1;
            Assert.Equal(SaveReadStatus.Loaded, loaded.Status);
        }
        string Stats(double[] v) { var s = v.OrderBy(x => x).ToArray(); return "p50 " + s[runs / 2].ToString("F4", CultureInfo.InvariantCulture)
            + " p95 " + s[runs * 95 / 100].ToString("F4", CultureInfo.InvariantCulture) + " max " + s[runs - 1].ToString("F4", CultureInfo.InvariantCulture) + " ms"; }
        output.WriteLine("payload bytes: empty ledger " + Encoding.UTF8.GetByteCount(emptyPayload) + ", at bounds " + Encoding.UTF8.GetByteCount(fullPayload)
            + ", at bounds with 32-character addresses " + Encoding.UTF8.GetByteCount(SaveWire.Text(City(AtBounds(32)))));
        output.WriteLine("write at bounds " + Stats(write) + "; empty " + Stats(writeEmpty));
        output.WriteLine("read at bounds " + Stats(read) + "; empty " + Stats(readEmpty));
        output.WriteLine("allocation per write+read: at bounds " + allocFull / runs + " B, empty " + allocEmpty / runs + " B");
    }
}
