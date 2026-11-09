using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

public sealed class ChangeJournalTests
{
    private readonly ITestOutputHelper output;
    public ChangeJournalTests(ITestOutputHelper output) { this.output = output; }
    static ShopObservedOffer[] Offers(long cents = 250, int stock = 40) => new[] {
        new ShopObservedOffer { ProductId = FoodProducts.Croissant, PriceUnits = Money.FromCents(cents).Units, Stock = stock } };
    static List<OpeningWindow> Hours(int close = 1320) => new() { new OpeningWindow { Day = 0, FromMinute = 360, ToMinute = close } };
    static CityState Fresh(int seed = 5150)
    {
        var (state, market) = Schema28Tests.Fresh(3, seed);
        var city = new CitySimulator(state, new FreshCityTraceTests.Walk()); city.AttachFoodMarket(market);
        while (!city.AdvanceTo(2000, 128).CaughtUp) { }
        return state;
    }
    internal static CityState Fixture(int seed = 5150)
    {
        var state = Fresh(seed);
        var journal = new FoodChangeJournal(state.Food!, state.Economy);
        var shop = state.Food!.Shops[0];
        journal.ObserveSupply(shop.BusinessId, shop.PlaceId, 1900, Offers(), 1, 2);
        journal.ObserveHours(shop.BusinessId, shop.PlaceId, 1900, Hours());
        journal.ObserveSupply(shop.BusinessId, shop.PlaceId, 1951, Offers(275, 0), 1, 1);
        journal.ObserveHours(shop.BusinessId, shop.PlaceId, 1962, Hours(1200));
        state.Food.History.RemoveAll(h => h.BusinessId == shop.BusinessId && h.Day == 1);
        state.Food.History.Add(new FoodShopDay { Day = 1, PlaceId = shop.PlaceId, BusinessId = shop.BusinessId,
            StockoutObserved = true, PeriodCauses = new MealPeriodCauses { Counts = Enumerable.Range(0,16).ToArray() } });
        return state;
    }
    [Theory]
    [InlineData(5150, "570461024fd65a093220d73de492518a87b5395d4d6a93fd563db86a32fe4c13")]
    [InlineData(9217, "df8c95a30d401b72b580794efe27da15eb15d941ea20e05afe5ae36aa68b4404")]
    public void CurrentWriterGoldenSnapshotAndReload(int seed, string pin)
    {
        // F7d: writer 33 is now the frozen predecessor JSON writer; its pins and reader are unchanged.
        Assert.Equal(CityState.AttendanceSchema + 1, CityState.ChangeJournalSchema);
        Assert.Equal(CityState.ChangeJournalSchema + 2, CityState.CurrentSchema); // F7d's 34, then F2e's 35
        var state = Fixture(seed); string payload = DevelopmentChain.Write33(state);
        output.WriteLine($"F6g golden {seed}: {EconomicLedgerTests.Sha(payload)} chars={payload.Length}");
        var read = SaveCodec.ReadWriter33(payload); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(payload, DevelopmentChain.Write33(read.State!));
        var resumed = new FoodChangeJournal(read.State!.Food!, read.State.Economy); var scope = read.State.Food!.Shops[0];
        resumed.ObserveSupply(scope.BusinessId, scope.PlaceId, 2000, Offers(275, 0), 1, 1);
        resumed.ObserveHours(scope.BusinessId, scope.PlaceId, 2000, Hours(1200));
        Assert.Equal(payload, DevelopmentChain.Write33(read.State));
        state.Schema = CityState.ChangeJournalSchema;
        var snapshot = SaveCodec.CaptureForSchema(state, CityState.ChangeJournalSchema, null);
        var audit = GraphAudit.Compare(state, snapshot.Wire);
        Assert.Empty(audit.Differences); Assert.All(audit.Shared, shared => Assert.True(GraphAudit.Immutable(shared)));
        state.Economy.Shops[0].Supply[0] = Offers(999)[0]; state.Economy.Shops[0].Hours[0] = 1;
        state.Food!.History[0].PeriodCauses.Counts[0]++; state.Economy.Shops[0].Changes.Clear();
        Assert.Equal(payload, SaveCodec.EncodeForSchema(snapshot, CityState.ChangeJournalSchema, null));
        Assert.Equal(pin, EconomicLedgerTests.Sha(payload));
    }
    [Fact]
    public void OlderAttendancePayloadIsDistinctRefusalAndKept()
    {
        string older = DevelopmentChain.WriteAttendance(AttendanceWageTests.Fresh().State);
        EconomicLedgerTests.Refused(older, SaveReadStatus.DevelopmentSchema);
        Assert.Contains("start a new city", SaveWire.Read(older).Error);
        Assert.DoesNotContain("WageStartsDay", SaveCodec.Write(Fixture()));
    }
    [Fact]
    public void OnlyObservedShopChangesAtObservationMinuteAndReloadContinuesExactImage()
    {
        var state = Fresh(); var food = state.Food!;
        var a = food.Shops[0]; var b = food.Shops[1];
        var journal = new FoodChangeJournal(food, state.Economy);
        foreach (var shop in new[] { a, b }) journal.ObserveSupply(shop.BusinessId, shop.PlaceId, 1800, Offers(), 1, 2);
        journal.ObserveSupply(a.BusinessId, a.PlaceId, 1801, Offers(), 1, 2);
        Assert.All(state.Economy.Shops, row => Assert.Empty(row.Changes));
        // A fixture edit of the quiet shop exists outside the observer until the next read.
        var edited = Offers(300, 0);
        journal.ObserveSupply(a.BusinessId, a.PlaceId, 1811, Offers(275, 0), 1, 1);
        Assert.Empty(state.Economy.Shops[1].Changes);
        state.Minute = 2000; string saved = SaveCodec.Write(state);
        state = SaveWire.Read(saved).State!; food = state.Food!; journal = new FoodChangeJournal(food, state.Economy);
        string before = state.Economy.Shops[0].Fingerprint;
        journal.ObserveSupply(a.BusinessId, a.PlaceId, 1900, Offers(275, 0), 1, 1);
        Assert.Equal(3, state.Economy.Shops[0].Changes.Count); Assert.Equal(before, state.Economy.Shops[0].Fingerprint);
        journal.ObserveSupply(b.BusinessId, b.PlaceId, 1900, edited, 1, 2);
        Assert.All(state.Economy.Shops[0].Changes, c => Assert.Equal(1811, c.ObservedMinute));
        Assert.All(state.Economy.Shops[1].Changes, c => Assert.Equal(1900, c.ObservedMinute));
        Assert.Equal(new[] { ShopChangeKind.StaffedCounters, ShopChangeKind.Price, ShopChangeKind.Stock }, state.Economy.Shops[0].Changes.Select(c => c.Kind));
        Assert.Equal(saved, SaveCodec.Write(SaveWire.Read(saved).State!));
    }
    [Fact]
    public void ReorderingEquivalentHoursDoesNotLookLikeAnEdit()
    {
        var state = Fresh(); var shop = state.Food!.Shops[0]; var journal = new FoodChangeJournal(state.Food, state.Economy);
        var hours = Hours(); hours.Add(new OpeningWindow { Day = 2, FromMinute = 480, ToMinute = 720 });
        journal.ObserveHours(shop.BusinessId, shop.PlaceId, 1800, hours);
        string baseline = state.Economy.Shops[0].Fingerprint; hours.Reverse();
        journal.ObserveHours(shop.BusinessId, shop.PlaceId, 1900, hours);
        Assert.Empty(state.Economy.Shops[0].Changes); Assert.Equal(baseline, state.Economy.Shops[0].Fingerprint);
        EconomicLedgerTests.AssertRoundTrip(state);
        // The base Place contract permits repeated windows; the journal must preserve that contract.
        hours.Add(new OpeningWindow { Day = 0, FromMinute = 360, ToMinute = 1320 });
        journal.ObserveHours(shop.BusinessId, shop.PlaceId, 1901, hours);
        EconomicLedgerTests.AssertRoundTrip(state);
    }
    [Fact]
    public void SparseHoursRetainCanonicalWindowsAndBoundTheSavedImage()
    {
        var state = Fresh(); var scope = state.Food!.Shops[0]; var journal = new FoodChangeJournal(state.Food, state.Economy);
        var weekly = Enumerable.Range(0, 7).Select(day => new OpeningWindow { Day = day, FromMinute = 360, ToMinute = 1320 }).ToList();
        journal.ObserveSupply(scope.BusinessId, scope.PlaceId, 1800, Offers(), 1, 2);
        journal.ObserveHours(scope.BusinessId, scope.PlaceId, 1800, weekly);
        JArray Image() => (JArray)SaveWire.Parse(SaveCodec.Write(state))["Economy"]!["Shops"]![0]!;
        var image = Image(); Assert.Equal(56, ((string)image[4]!).Length);
        // State, supply, hours plus their three separators; excludes the already-frozen header/journal.
        int Extension(JArray row) => row.Skip(2).Sum(cell => cell.ToString(Newtonsoft.Json.Formatting.None).Length) + 3;
        Assert.Equal(109, Extension(image));
        EconomicLedgerTests.AssertRoundTrip(state);
        var full = Enumerable.Range(0, 64).Select(i => new OpeningWindow { Day = 0, FromMinute = i * 20, ToMinute = i * 20 + 10 }).ToList();
        journal.ObserveHours(scope.BusinessId, scope.PlaceId, 1900, full);
        image = Image(); Assert.Equal(398, ((string)image[4]!).Length);
        Assert.Equal(626, Extension(image) + 7 * 25); // Seven more fixed-width offer cells.
        EconomicLedgerTests.AssertRoundTrip(state);
        string sparse = (string)image[4]!;
        // Swap the first two windows: the sparse reader must still enforce canonical ordering.
        image[4] = sparse[..14] + sparse[20..26] + sparse[14..20] + sparse[26..];
        var root = SaveWire.Parse(SaveCodec.Write(state)); root["Economy"]!["Shops"]![0] = image;
        EconomicLedgerTests.Refused(root.ToString(Newtonsoft.Json.Formatting.None));
        output.WriteLine("exact image: weekly/one offer=109 chars/shop, 111616/1024 shops; max hours/eight offers=626, 641024/1024 shops; plan hash budget=32, 32768/1024 shops");
    }
    [Fact]
    public void SameMinuteReversalsKeepTheExactBaselineAndCanonicalJournal()
    {
        var state = Fresh(); var shop = state.Food!.Shops[0]; var journal = new FoodChangeJournal(state.Food, state.Economy);
        journal.ObserveSupply(shop.BusinessId, shop.PlaceId, 1800, Offers(), 1, 2);
        journal.ObserveSupply(shop.BusinessId, shop.PlaceId, 1801, Offers(275), 1, 2);
        journal.ObserveSupply(shop.BusinessId, shop.PlaceId, 1801, Offers(), 1, 2);
        journal.ObserveSupply(shop.BusinessId, shop.PlaceId, 1801, Offers(275), 1, 2);
        Assert.Equal(2, state.Economy.Shops[0].Changes.Count);
        Assert.Equal(Money.FromCents(275).Units, state.Economy.Shops[0].Supply[0].PriceUnits);
        EconomicLedgerTests.AssertRoundTrip(state);
    }
    [Fact]
    public void HoursAreIndependentFromSupplyAndJournalEvictsOldestFirstCountingEveryEntry()
    {
        var state = Fresh(); var shop = state.Food!.Shops[0];
        var journal = new FoodChangeJournal(state.Food, state.Economy);
        journal.ObserveHours(shop.BusinessId, shop.PlaceId, 1800, Hours());
        journal.ObserveSupply(shop.BusinessId, shop.PlaceId, 1801, Offers(), 1, 2);
        journal.ObserveHours(shop.BusinessId, shop.PlaceId, 1802, Hours());
        Assert.Empty(state.Economy.Shops[0].Changes);
        journal = new FoodChangeJournal(state.Food, state.Economy);
        journal.ObserveHours(shop.BusinessId, shop.PlaceId, 1803, Hours());
        Assert.Empty(state.Economy.Shops[0].Changes);
        for (int i = 1; i <= 12; i++) journal.ObserveSupply(shop.BusinessId, shop.PlaceId, 1802 + i, Offers(250 + i), 1, 2);
        var row = state.Economy.Shops[0];
        Assert.Equal(4, row.Evicted); Assert.Equal(Enumerable.Range(1807, 8), row.Changes.Select(c => c.ObservedMinute));
        journal.ObserveHours(shop.BusinessId, shop.PlaceId, 1900, Hours(1200));
        Assert.Equal(5, row.Evicted); Assert.Equal(ShopChangeKind.Hours, row.Changes.Last().Kind);
        Assert.Equal(1, row.Changes.Last().Subject); Assert.Equal(1320, row.Changes.Last().Before); Assert.Equal(1200, row.Changes.Last().After);
        state.Minute = 2000; EconomicLedgerTests.AssertRoundTrip(state);
        journal.Clear(shop.BusinessId); Assert.Empty(state.Economy.Shops); Assert.Null(journal.Find(shop.BusinessId));
        journal.ObserveSupply(shop.BusinessId, shop.PlaceId, 1901, Offers(500), 1, 4);
        Assert.Empty(state.Economy.Shops[0].Changes); Assert.Equal(0, state.Economy.Shops[0].Evicted);
    }
    [Fact]
    public void OverlapAndPartialCoverageAreFactsAndCompleteWindowShowsExactDelta()
    {
        var row = new ShopObservation { Changes = new() {
            new ShopChange { Kind = ShopChangeKind.Price, ProductId = FoodProducts.Croissant, ObservedMinute = 14 * 1440 + 724, Before = 1, After = 2 },
            new ShopChange { Kind = ShopChangeKind.Stock, ProductId = FoodProducts.Croissant, ObservedMinute = 15 * 1440, Before = 30, After = 0 } } };
        var rows = Enumerable.Range(7, 14).Select(d => new FoodShopDay { Day = d, Purchases = d < 14 ? 2 : 3 }).ToList();
        var text = new StringBuilder(); FoodFeedbackV2.AppendJournal(text, row, rows, 20, 1);
        Assert.Contains("First observed day 14, 12:04", text.ToString()); Assert.Contains("14 / 21 · change +7 · mixed", text.ToString());
        Assert.Contains("first observed", text.ToString());
        text.Clear(); FoodFeedbackV2.AppendPeriods(text, rows, 3, 1);
        Assert.Contains("partial", text.ToString()); Assert.DoesNotContain("change +", text.ToString());
        text.Clear(); FoodFeedbackV2.AppendJournal(text, row, rows, 30, 1);
        Assert.Contains("partial", text.ToString()); Assert.DoesNotContain("change +", text.ToString());
        var distant = row.Changes[1]; distant.ObservedMinute = 28 * 1440; row.Changes[1] = distant;
        text.Clear(); FoodFeedbackV2.AppendJournal(text, row, rows, 34, 1);
        Assert.DoesNotContain("mixed", text.ToString());
        row.Changes.RemoveAt(1); row.Evicted = 100;
        text.Clear(); FoodFeedbackV2.AppendJournal(text, row, rows, 20, 1);
        Assert.DoesNotContain("mixed", text.ToString()); Assert.Contains("evicted: 100", text.ToString());
    }
    [Fact]
    public void CoverageStartsOnCreationDayAndCounterLimitsAreLabeled()
    {
        var food = new FoodCommerceState { ChoiceStartsDay = 23 };
        Assert.Equal(22, FoodFeedbackV2.RecordingSince(food));
        var row = new FoodShopDay { Day = 22 }; row.PeriodCauses.Counts[8] = MealPeriodCauses.MaximumCount;
        var text = new StringBuilder(); FoodFeedbackV2.AppendPeriods(text, new[] { row }, 22, 22);
        Assert.Contains("partial", text.ToString()); Assert.Contains("at least 46655", text.ToString());
        Assert.Contains("lower bounds", text.ToString()); Assert.DoesNotContain("change +", text.ToString());
    }
    [Theory]
    [InlineData("missing-image")] [InlineData("future-minute")] [InlineData("supply-width")]
    [InlineData("hours-width")] [InlineData("supply-order")] [InlineData("staff-overflow")] [InlineData("hours-gap")] [InlineData("hours-counted-absent")]
    public void ImageFaultsAreRefusedAndKept(string fault)
    {
        var root = SaveWire.Parse(SaveCodec.Write(Fixture())); var shop = (JArray)root["Economy"]!["Shops"]![0]!;
        switch (fault)
        {
            case "missing-image": shop.RemoveAt(2); break;
            case "future-minute": { string cell = (string)shop[1]![0]!; shop[1]![0] = "ZZZZZZ" + cell[6..]; break; }
            case "supply-width": shop[3]![0] = "0"; break;
            case "hours-width": shop[4] = "000"; break;
            case "hours-gap": { string hours = (string)shop[4]!; shop[4] = "00" + hours[2..]; break; }
            case "hours-counted-absent": { string hours = (string)shop[4]!; shop[4] = hours[..14] + "141141"; break; }
            case "supply-order": ((JArray)shop[3]!).Add(shop[3]![0]!.DeepClone()); break;
            case "staff-overflow": shop[2] = new string('0', 13) + "ZZZZZZZ"; break;
        }
        EconomicLedgerTests.Refused(root.ToString(Newtonsoft.Json.Formatting.None));
    }
    [Fact]
    public void ExistingObservationPathHasNoAddedReaderOrShopWriterCallSites()
    {
        // This is a source-structure guard, not a native read-counter experiment.
        string runtime = Source("Runtime/NativeFoodMarket.cs");
        string added = runtime[runtime.IndexOf("private void ObserveJournalSupply", StringComparison.Ordinal)..runtime.IndexOf("private IReadOnlyCollection<string>? SelectedCompleteMenu", StringComparison.Ordinal)];
        foreach (string forbidden in new[] { "FoodSupplyReader.Read", "ReadMenu(", "SaveGameManager", "reg.", "SetUp(", ".Buy(" }) Assert.DoesNotContain(forbidden, added);
        Assert.Equal(5, runtime.Split("FoodSupplyReader.ReadMenu").Length - 1);
        Assert.Contains("cachedImage.Image.ObserveLive(supply)", runtime);
        Assert.True(runtime.IndexOf("ObserveJournalSupply(b, scope, supply", StringComparison.Ordinal)
            < runtime.IndexOf("if (supply.Values.Any(s => !s.Units.HasValue))", StringComparison.Ordinal));
        string hours = Source("Runtime/NativeFoodHours.cs");
        Assert.Contains("if (!closed) ObserveJournalHours", hours);
        Assert.Contains("candidateBusinesses.Contains(scope.BusinessId)", hours);
        Assert.Contains("changeJournal?.Clear(scope.BusinessId)", Source("Runtime/NativeFoodCandidates.cs"));
    }
    [Fact]
    public void SameMinuteLiveCaptureSupersedesAnOlderCachedHandle()
    {
        var state = Fresh(); var scope = state.Food!.Shops[0]; var journal = new FoodChangeJournal(state.Food, state.Economy);
        var cachedA = new DecisionSupplyImage<ShopObservedOffer[]>(Offers(stock: 40));
        journal.ObserveSupply(scope.BusinessId, scope.PlaceId, 1900, cachedA.Value, 1, 2);
        var liveB = Offers(stock: 0);
        cachedA.ObserveLive(liveB); // same production seam used by ReadSelected's live capture
        journal.ObserveSupply(scope.BusinessId, scope.PlaceId, 1900, liveB, 1, 2);
        journal.ObserveSupply(scope.BusinessId, scope.PlaceId, 1900, cachedA.Value, 1, 2);
        Assert.Same(liveB, cachedA.Value);
        Assert.Equal(0, journal.Find(scope.BusinessId)!.Supply[0].Stock);
        Assert.Single(journal.Find(scope.BusinessId)!.Changes);
    }
    [Fact]
    public void ClosedShopPriceEditsAndReversalsPreserveStockStaffAndExactMinutes()
    {
        var state = Fresh(); var scope = state.Food!.Shops[0]; var journal = new FoodChangeJournal(state.Food, state.Economy);
        journal.ObserveSupply(scope.BusinessId, scope.PlaceId, 1800, Offers(600), 1, 2);
        journal.ObserveSupply(scope.BusinessId, scope.PlaceId, 1801, Offers(700, 0), 1, 0, false);
        journal.ObserveSupply(scope.BusinessId, scope.PlaceId, 1802, Offers(600, 0), 1, 0, false);
        var row = journal.Find(scope.BusinessId)!;
        Assert.Equal(2, row.StaffedCounters); Assert.Equal(1, row.Supply[0].Stock);
        Assert.Equal(new[] { 1801, 1802 }, row.Changes.Select(c => c.ObservedMinute));
        Assert.All(row.Changes, c => Assert.Equal(ShopChangeKind.Price, c.Kind));
        EconomicLedgerTests.AssertRoundTrip(state);
        var coldState = Fresh(); var cold = new FoodChangeJournal(coldState.Food!, coldState.Economy);
        cold.ObserveSupply(scope.BusinessId, scope.PlaceId, 1800, Offers(600, 0), 1, 0, false);
        cold.ObserveSupply(scope.BusinessId, scope.PlaceId, 1801, Offers(700, 0), 1, 0, false);
        Assert.Equal(-1, cold.Find(scope.BusinessId)!.Supply[0].Stock);
        Assert.Equal(-1, cold.Find(scope.BusinessId)!.StaffedCounters);
        Assert.Single(cold.Find(scope.BusinessId)!.Changes);
        EconomicLedgerTests.AssertRoundTrip(coldState);
    }
    [Fact]
    public void ExactNativeMinuteAtTheCompatibleLogicalPredecessorRoundTrips()
    {
        var state = Fresh(); var scope = state.Food!.Shops[0]; var journal = new FoodChangeJournal(state.Food, state.Economy);
        state.Minute = AccountingClock.PreviousEpochTarget(2001);
        Assert.True(AccountingClock.Compatible(state.Minute, 2001));
        journal.ObserveSupply(scope.BusinessId, scope.PlaceId, 2000, Offers(), 1, 2);
        journal.ObserveHours(scope.BusinessId, scope.PlaceId, 2000, Hours());
        journal.ObserveSupply(scope.BusinessId, scope.PlaceId, 2001, Offers(300), 1, 2);
        journal.ObserveHours(scope.BusinessId, scope.PlaceId, 2001, Hours(1200));
        EconomicLedgerTests.AssertRoundTrip(state);
        Assert.Equal(2001, state.Economy.Shops[0].Changes[0].ObservedMinute);
        state.Minute = 2000.5;
        Assert.Throws<SaveValidationException>(() => SaveCodec.Write(state));
    }
    [Fact]
    public void HiddenFramesDoNotBuildAndPageInvalidationBuildsImmediately()
    {
        var city = AttendanceWageTests.Fresh(); var gate = new FoodPanelRefresh(); int buildEconomyTextCalls = 0;
        string BuildEconomyText() { buildEconomyTextCalls++; return city.FoodMoney!.Snapshot().Text(); }
        Func<string> build = BuildEconomyText;
        long requests = city.FoodMoney!.Requests, bytes = GC.GetAllocatedBytesForCurrentThread();
        for (int frame = 0; frame < 10000; frame++) gate.Update(false, frame / 60.0, build);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - bytes);
        Assert.Equal(0, buildEconomyTextCalls); Assert.Equal(0, gate.Builds); Assert.Equal(requests, city.FoodMoney.Requests);
        gate.Update(true, 200, build); Assert.Equal(1, buildEconomyTextCalls);
        gate.Update(true, 201, build); Assert.Equal(1, buildEconomyTextCalls);
        gate.Invalidate(); gate.Update(true, 201, build); Assert.Equal(2, buildEconomyTextCalls);
        Assert.Equal(requests + 2, city.FoodMoney.Requests);
        gate.Reset(); Assert.Equal("", gate.Text); Assert.Equal(0, gate.Builds);
        gate.Update(true, 201, build); Assert.Equal(1, gate.Builds);
        Assert.Contains("economyRefresh.Update(showEconomy, Time.unscaledTime, economyBuilder)", Source("Runtime/FoodEconomyPanel.cs"));
    }
    static string Source(string name)
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && !File.Exists(Path.Combine(d.FullName, "AGENTS.md"))) d = d.Parent;
        return File.ReadAllText(Path.Combine(d!.FullName, name));
    }
    [Fact]
    public void MeasurementAndHiddenPanel()
    {
        foreach (int count in new[] { 1, 64, 1024 })
        {
            var food = new FoodCommerceState(); var ledger = new EconomicLedger();
            for (int i = 0; i < count; i++) food.Shops.Add(new FoodScope { BusinessId = "b" + i, PlaceId = "p" + i, ProductId = FoodProducts.Croissant });
            var journal = new FoodChangeJournal(food, ledger); var offers = Offers(); var hours = Hours();
            foreach (var s in food.Shops) { journal.ObserveSupply(s.BusinessId, s.PlaceId, 1800, offers, 1, 2); journal.ObserveHours(s.BusinessId, s.PlaceId, 1800, hours); }
            int cursor = 0;
            Measure($"supply {count}", () => { var s = food.Shops[cursor++ % count]; journal.ObserveSupply(s.BusinessId, s.PlaceId, 1801, offers, 1, 2); });
            cursor = 0;
            Measure($"hours {count}", () => { var s = food.Shops[cursor++ % count]; journal.ObserveHours(s.BusinessId, s.PlaceId, 1801, hours); });
            int revision = 0;
            Measure($"changed supply {count}", () => { int visit = revision++; var s = food.Shops[visit % count]; offers[0].PriceUnits = Money.FromCents(250 + visit / count % 2).Units; journal.ObserveSupply(s.BusinessId, s.PlaceId, 1802 + visit / count, offers, 1, 2); }, Math.Max(2000, count * 4));
            var eight = Enumerable.Range(0, 8).Select(i => new ShopObservedOffer { ProductId = "ba:itemname_fixture_" + i, PriceUnits = Money.FromCents(300).Units, Stock = 40 }).ToArray();
            foreach (var s in food.Shops) journal.ObserveSupply(s.BusinessId, s.PlaceId, 5000, eight, 8, 2);
            cursor = 0;
            Measure($"supply8 {count}", () => { var s = food.Shops[cursor++ % count]; journal.ObserveSupply(s.BusinessId, s.PlaceId, 5001, eight, 8, 2); });
            revision = 0;
            Measure($"changed hours {count}", () => { int visit = revision++; var s = food.Shops[visit % count]; hours[0].ToMinute = 1200 + visit / count % 2 * 120; journal.ObserveHours(s.BusinessId, s.PlaceId, 5002 + visit / count, hours); }, Math.Max(100, count * 2));
            foreach (var s in food.Shops)
                foreach (int day in Enumerable.Range(7, 14)) food.History.Add(new FoodShopDay { Day = day, BusinessId = s.BusinessId, PlaceId = s.PlaceId,
                    OffersConsidered = 16, StaffRejections = 4, StockRejections = 4, ServiceRejections = 4, ClosedRejections = 4,
                    Purchases = 2, Revenue = Money.FromCents(500), StockoutObserved = day % 3 == 0,
                    PeriodCauses = new MealPeriodCauses { Counts = Enumerable.Repeat(1, 16).ToArray() } });
            foreach (var s in ledger.Shops)
            {
                while (s.Changes.Count < 8) s.Changes.Add(new ShopChange { Kind = ShopChangeKind.Price, ProductId = FoodProducts.Croissant, Before = 1, After = 2 });
                for (int i = 0; i < s.Changes.Count; i++) { var change = s.Changes[i]; change.ObservedMinute = 14 * 1440 + i; s.Changes[i] = change; }
            }
            Measure($"panel {count}", () => {
                var text = new StringBuilder(); var index = FoodFeedback.IndexHistory(food.History);
                for (int i = 0; i < Math.Min(ledger.Shops.Count, FoodFeedbackV2.ShopsPerPage); i++) {
                    var s = food.Shops[i]; var rows = index[FoodFeedback.Key(s.PlaceId, s.BusinessId)];
                    FoodFeedback.Weeks(rows, s.PlaceId, s.BusinessId, 20, 1).AppendTo(text);
                    FoodFeedbackV2.AppendPeriods(text, rows, 20, 1); FoodFeedbackV2.AppendJournal(text, ledger.Shops[i], rows, 20, 1);
                }
                _ = text.ToString();
            }, 100);
        }
        var cacheOffers = Offers(); var cacheImage = new DecisionSupplyImage<ShopObservedOffer[]>(cacheOffers);
        Measure("cache live replacement", () => cacheImage.ObserveLive(cacheOffers));
        var cacheSlots = new DecisionSupplyImage<ShopObservedOffer[]>[1];
        Measure("cache image creation", () => cacheSlots[0] = new DecisionSupplyImage<ShopObservedOffer[]>(cacheOffers));
        var city = AttendanceWageTests.Fresh(); AttendanceWageTests.Advance(city, 1440); var state = city.State; string payload = SaveCodec.Write(state);
        Measure("save write fresh", () => SaveCodec.Write(state), 50);
        Measure("save read fresh", () => { Assert.Equal(SaveReadStatus.Loaded, SaveWire.Read(payload).Status); }, 50);
        var observed = Fixture(); string populated = SaveCodec.Write(observed);
        output.WriteLine($"save observed fixture: residents={observed.Citizens.Count} visitors={observed.Visitors.Count} chars={populated.Length} predecessorChars={DevelopmentChain.WriteAttendance(observed).Length} records={SaveCodec.CountRecords(observed, populated)}");
        Measure("save write observed fixture", () => SaveCodec.Write(observed), 20);
        Measure("save read observed fixture", () => Assert.Equal(SaveReadStatus.Loaded, SaveWire.Read(populated).Status), 20);
        long requests = city.FoodMoney!.Requests; long bytes = GC.GetAllocatedBytesForCurrentThread();
        var gate = new FoodPanelRefresh(); Func<string> build = () => city.FoodMoney.Snapshot().Text();
        bytes = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) gate.Update(false, i / 60.0, build);
        long hiddenBytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
        Assert.Equal(0, gate.Builds);
        Assert.Equal(0, hiddenBytes); Assert.Equal(requests, city.FoodMoney.Requests);
        string panel = Source("Runtime/FoodEconomyPanel.cs"); Assert.Contains("if (showEconomy) DrawEconomy();", panel);
    }
    void Measure(string label, Action action, int samples = 2000)
    {
        for (int i = 0; i < 100; i++) action();
        var ticks = new long[samples]; long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < samples; i++) { long start = Stopwatch.GetTimestamp(); action(); ticks[i] = Stopwatch.GetTimestamp() - start; }
        long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated; Array.Sort(ticks);
        double ms(long n) => n * 1000.0 / Stopwatch.Frequency;
        output.WriteLine($"{label}: n={samples} p50={ms(ticks[samples/2]):F6}ms p95={ms(ticks[(int)(samples*.95)]):F6}ms max={ms(ticks[^1]):F6}ms B/call={(double)bytes/samples:F1} spikes>=30ms={ticks.Count(t=>ms(t)>=30)}");
        if (label.StartsWith("supply ") || label.StartsWith("supply8 ") || label.StartsWith("hours ")) Assert.Equal(0, bytes);
    }
}

public sealed partial class FoodCommerceTests
{
    [Fact]
    public void F6g_MealCausesFollowTheExistingClassifierAndRollWithExactlyFourteenDays()
    {
        var (city, market, _) = Ready(threeMeals: true);
        Advance(city, 20 * 1440); city.State.Food!.History.Clear();
        var commerce = typeof(CitySimulator).GetField("food", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(city)!;
        var record = typeof(FoodCommerce).GetMethod("RecordChoices", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var reasons = new[] { "No staffed counter", "Out of stock", "Service unavailable", "Expected queue exceeds patience", "Closed or too late", "Over allowance", "Route unknown" };
        for (int day = 1; day <= 20; day++)
        {
            foreach (MealKind period in Enum.GetValues<MealKind>())
            {
                var intent = new FoodIntent { Day = day, Choices = reasons.Select(reason => new FoodChoice { BusinessId = "b", ProductId = FoodProducts.Croissant, Reason = reason }).ToList() };
                record.Invoke(commerce, new object[] { intent, market.Read(city.State.Minute), period });
            }
        }
        var rows = city.State.Food!.History;
        Assert.Equal(Enumerable.Range(7, 14), rows.Select(r => r.Day));
        foreach (var row in rows)
        {
            Assert.True(row.StockoutObserved);
            Assert.Equal(Enumerable.Range(0, 4).SelectMany(_ => new[] { 1, 1, 2, 1 }), row.PeriodCauses.Counts);
            Assert.Equal(4, row.StaffRejections); Assert.Equal(4, row.StockRejections); Assert.Equal(8, row.ServiceRejections); Assert.Equal(4, row.ClosedRejections);
        }
        var text = new StringBuilder(); FoodFeedbackV2.AppendPeriods(text, rows, 20, 1);
        Assert.Contains("Lunch: 7 evaluated offers found no staffed counter (last week 7) · change +0", text.ToString());
        Assert.Contains("Lunch: 14 evaluated offers found service/queue (last week 14)", text.ToString());
        string saved = SaveCodec.Write(city.State); var read = SaveWire.Read(saved); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var restoredText = new StringBuilder(); FoodFeedbackV2.AppendPeriods(restoredText, read.State!.Food!.History, 20, 1);
        Assert.Equal(text.ToString(), restoredText.ToString()); Assert.Equal(saved, SaveCodec.Write(read.State));
        text.Clear(); FoodFeedbackV2.AppendCity(text, city.FoodMoney!.Snapshot(), city.State.Economy, 20);
        Assert.Contains("Household strain (person wallets, current)", text.ToString()); Assert.Contains("$0 this week · $0 last week", text.ToString());
    }
}

public sealed partial class FoodCommerceTests
{
    [Fact]
    public void F6g_ActualCheckoutDepletionPreservesPriceEntryAndReadCounts()
    {
        var baseline = Run(false); var observed = Run(true);
        Assert.True(observed.Sales >= 8, "Fixture must complete at least eight real FoodCommerce checkouts.");
        Assert.Equal(baseline.Reads, observed.Reads); Assert.Equal(baseline.Sales, observed.Sales);
        var row = observed.Journal!.Find("b")!;
        Assert.Single(row.Changes); Assert.Equal(ShopChangeKind.Price, row.Changes[0].Kind);
        Assert.Equal(0, row.Evicted); Assert.Equal(1, row.Supply[0].Stock);
        static (int Reads, int Sales, FoodChangeJournal? Journal) Run(bool attach)
        {
            var (city, market, _) = Ready(count: 24, threeMeals: true); city.EnableHouseholdFinances();
            market.Units = 1000;
            if (attach) market.Journal = new FoodChangeJournal(city.State.Food!, city.State.Economy);
            market.Read(1440); market.Price = Money.FromCents(125); market.Read(1440);
            Advance(city, 3 * 1440);
            return (market.Reads, market.Orders.Count, market.Journal);
        }
    }
}
