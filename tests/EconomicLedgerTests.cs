using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

public sealed class EconomicLedgerTests
{
    private readonly ITestOutputHelper output;
    public EconomicLedgerTests(ITestOutputHelper output) { this.output = output; }
    internal static string Sha(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();
    internal static string Fingerprint => new string('0', 20);
    internal static CityState Fresh(int seed = 5150) => TestCity.Banded(seed);
    internal static void Populate(CityState state)
    {
        foreach (var p in PersonTable.Schedules(state).Where(p => p.DayPlaceId.Length != 0))
            p.Accrual = new EmploymentAccrual { AttendedMinutes = 1440, UnpostedWage = Money.FromUnits(long.MaxValue), MissMask = (1 << 14) - 1, Day = (int)(state.Minute / 1440), Shift = 1439 };
        foreach (var m in state.Food!.Residents)
        {
            var b = m.Budget!;
            b.Ledger = new OwnerAccounts { EmploymentIncome = Money.FromUnits(17), Discretionary = Money.FromUnits(17), VisitWatermark = (long)state.Minute };
        }
        int day = (int)(state.Minute / 1440);
        state.Economy.Outside.Add(new OutsideSpendingDay { Day = day, RetailUnits = BigInteger.Parse("99999999999999999999999999"), RetailVisits = BigInteger.Parse("99999999999999999999999999"), LeisureUnits = 21, LeisureVisits = 3 });
        var scope = state.Food.Shops[0];
        var reference = new FoodMemoryStore(state.Food).RegisterCampaign(scope.BusinessId, scope.PlaceId);
        state.Economy.Shops.Add(new ShopObservation { BusinessRef = reference, Fingerprint = Fingerprint,
            Changes = new List<ShopChange>
            {
                new() { ObservedMinute = (int)state.Minute, Kind = ShopChangeKind.Price, ProductId = FoodProducts.Croissant, Before = 1, After = Money.FromCents(123).Units },
                new() { ObservedMinute = (int)state.Minute, Kind = ShopChangeKind.Stock, ProductId = FoodProducts.Croissant, Before = int.MaxValue, After = 0 },
                new() { ObservedMinute = (int)state.Minute, Kind = ShopChangeKind.StaffedCounters, Before = 0, After = int.MaxValue },
                new() { ObservedMinute = (int)state.Minute, Kind = ShopChangeKind.Hours, Subject = 13, Before = 0, After = 1440 },
            } });
        if (state.Food.History.Count == 0) state.Food.History.Add(new FoodShopDay { Day = day, BusinessId = scope.BusinessId, PlaceId = scope.PlaceId });
        foreach (var row in state.Food.History) row.PeriodCauses.Counts = Enumerable.Range(0, 16).Select(i => i == 15 ? MealPeriodCauses.MaximumCount : i).ToArray();
    }
    internal static void AssertRoundTrip(CityState state)
    {
        string payload = SaveCodec.Write(state);
        var read = SaveWire.Read(payload); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(payload, SaveCodec.Write(read.State!));
        var records = SaveCodec.CountRecords(state, payload);
        Assert.True(records.Streaming <= SaveCodec.MaxEmbeddedRecords && records.Semantic <= SaveCodec.MaxEmbeddedRecords);
    }
    internal static void Refused(string payload, SaveReadStatus status = SaveReadStatus.Invalid)
    {
        payload = SaveWire.Payload(payload);
        var replacement = CurrentSchemaOnlyTests.City();
        var data = new Dictionary<string, string> { [SaveStore.StateKey] = payload, ["other"] = "kept" };
        var before = new Dictionary<string, string>(data);
        var read = SaveWire.Read(payload); Assert.True(read.Status == status, read.Error); Assert.Null(read.State);
        Assert.Equal(status, SaveStore.ReadFrom(data, replacement.PopulationKey).Status);
        Assert.False(SaveStore.WriteTo(data, replacement).Success);
        Assert.False(SaveSession.Open(data, replacement.PopulationKey).WriteTo(data, replacement).Success);
        Assert.Equal(before, data); Assert.Same(payload, data[SaveStore.StateKey]);
    }
    internal static void WriteRefused(CityState state)
    {
        string previous = SaveCodec.Write(CurrentSchemaOnlyTests.City());
        var data = new Dictionary<string, string> { [SaveStore.StateKey] = previous, ["other"] = "kept" };
        state.PopulationKey = CurrentSchemaOnlyTests.City().PopulationKey;
        Assert.Throws<SaveValidationException>(() => SaveCodec.Write(state));
        Assert.False(SaveStore.WriteTo(data, state).Success);
        Assert.False(SaveSession.Open(data, state.PopulationKey).WriteTo(data, state).Success);
        Assert.Same(previous, data[SaveStore.StateKey]); Assert.Equal("kept", data["other"]);
    }

    [Fact]
    public void FreshZeroShape()
    {
        var state = Fresh();
        Assert.Empty(state.Economy.Outside); Assert.Empty(state.Economy.Shops);
        Assert.All(PersonTable.Schedules(state), p => Assert.Equal(default, p.Accrual));
        Assert.All(state.Food!.Residents, m => Assert.Equal(default, m.Budget!.Ledger));
        string payload = SaveCodec.Write(state);
        Assert.DoesNotContain("WageStartsDay", payload);
        AssertRoundTrip(state);
    }
    [Theory]
    [InlineData(5150, "515602617768c104356842e868a8c10f9ce99dfa3deabab6f9b2bef552b31b32", 180546)]
    [InlineData(9217, "ae40b4e64d941a98b70f24c7fcb30560d19e8f4a25eefea18b1797cc55fb5cfe", 180510)]
    public void PopulatedWriterGoldensAndEveryNewFieldRoundTrips(int seed, string pin, int length)
    {
        var state = Fresh(seed); Populate(state);
        string payload = SaveCodec.Write(state);
        AssertRoundTrip(state);
        string actual = Sha(payload) + " " + payload.Length;
        Assert.True(pin + " " + length == actual, "golden " + seed + ": " + actual);
        var snapshot = SaveCodec.Capture(state);
        state.Citizens[0].Accrual = default; state.VisitorRoutines[0].Accrual = default;
        state.Food!.Residents[0].Budget!.Ledger = default;
        state.Food.History[0].PeriodCauses.Counts[0]++;
        state.Economy.Outside[0].RetailUnits = 0; state.Economy.Shops[0].Fingerprint = ""; state.Economy.Shops[0].Changes.Clear();
        Assert.Equal(payload, SaveCodec.Encode(snapshot));
    }
    [Fact]
    public void JournalOnlyProductKeepsItsIdentityWhenTheNameTableChanges()
    {
        var state = Fresh(); Populate(state);
        var j = state.Economy.Shops[0].Changes[0]; j.ProductId = "zmod:itemname_retired"; state.Economy.Shops[0].Changes[0] = j;
        AssertRoundTrip(state);
        state.Food!.Shops[0].AdditionalProductIds.Add("amod:itemname_new");
        string payload = SaveCodec.Write(state);
        var read = SaveWire.Read(payload); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(j.ProductId, read.State!.Economy.Shops[0].Changes[0].ProductId);
    }
    [Fact]
    public void FingerprintsStayCanonicalUnderHtmlEscapingSerializerDefaults()
    {
        var state = Fresh(); Populate(state);
        state.Economy.Shops[0].Fingerprint = string.Concat(Enumerable.Repeat("a<>&a", 4));
        string expected = SaveCodec.Write(state);
        var context = typeof(SaveCodec).GetNestedType("WireContext", System.Reflection.BindingFlags.NonPublic)!.GetMethod("FromState")!.Invoke(null, new object[] { state });
        var settings = (JsonSerializerSettings)typeof(SaveCodec).GetMethod("Settings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, new object[] { context! })!;
        settings.StringEscapeHandling = StringEscapeHandling.EscapeHtml;
        string payload = JsonConvert.SerializeObject(state, Formatting.None, settings);
        Assert.Contains("a<>&a", payload);
        var read = SaveWire.Read(payload); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(expected, SaveCodec.Write(read.State!));
    }

    [Fact]
    public void AnotherSchemaNumberIsRefusedAndKept()
    {
        var state = Fresh();
        foreach (int other in new[] { 30, 36, 37 })
        {
            var wire = SaveWire.Parse(SaveCodec.Write(state)); wire["Schema"] = other;
            Refused(wire.ToString(Formatting.None), SaveReadStatus.DevelopmentSchema);
            Assert.Contains("start a new city", SaveWire.Read(wire.ToString(Formatting.None)).Error);
        }
    }
    [Theory]
    [InlineData("unknown-root")] [InlineData("unknown-ledger")] [InlineData("missing-ledger")]
    [InlineData("missing-accrual")] [InlineData("missing-causes")] [InlineData("missing-owner")]
    [InlineData("duplicate-root")] [InlineData("duplicate-ledger")]
    [InlineData("bad-enum")] [InlineData("lowercase-money")] [InlineData("overflow-money")] [InlineData("escaped-money")]
    [InlineData("cause-width")] [InlineData("accrual-money-overflow")]
    [InlineData("wide-leading-zero")] [InlineData("wide-exponent")] [InlineData("wide-overflow")] [InlineData("wide-negative")]
    public void StrictReaderRefusalsPreserveOriginals(string fault)
    {
        var state = Fresh(); Populate(state); var root = SaveWire.Parse(SaveCodec.Write(state));
        var economy = root["Economy"]!; var shops = (JArray)economy["Shops"]!;
        string? raw = null;
        switch (fault)
        {
            case "unknown-root": root["Invented"] = 0; break;
            case "unknown-ledger": economy["Invented"] = 0; break;
            case "missing-ledger": root.Remove("Economy"); break;
            case "missing-accrual": ((JObject)root["Citizens"]![0]!).Remove("Accrual"); break;
            case "missing-causes": ((JObject)root["Food"]!["History"]![0]!).Remove("Causes"); break;
            case "missing-owner": ((JArray)root["Citizens"]![0]!["Wallet"]!).RemoveAt(20); break;
            case "duplicate-root": raw = root.ToString(Formatting.None).Replace("\"Economy\":", "\"Economy\":{},\"Economy\":"); break;
            case "duplicate-ledger": raw = root.ToString(Formatting.None).Replace("\"Outside\":", "\"Outside\":[],\"Outside\":"); break;
            case "cause-width": root["Food"]!["History"]![0]!["Causes"] = new string('0', 49); break;
            case "accrual-money-overflow": root["Citizens"]![0]!["Accrual"] = "0001Y2P0IJ32E8E8" + new string('0', 10); break;
            case "bad-enum": { string s = (string)shops[0][1]![0]!; shops[0][1]![0] = s[..6] + "Z" + s[7..]; break; }
            case "lowercase-money": root["Citizens"]![0]!["Wallet"]![20] = new string('a', 32); break;
            case "overflow-money": root["Citizens"]![0]!["Wallet"]![20] = new string('Z', 13) + new string('0', 19); break;
            case "escaped-money": raw = root.ToString(Formatting.None).Replace("\"" + new string('0', 26), "\"\\u0030" + new string('0', 25)); break;
            default:
                string bad = fault == "wide-leading-zero" ? "01" : fault == "wide-exponent" ? "1e1" : fault == "wide-negative" ? "-1" : new string('9', 27);
                economy["Outside"]![0] = "1," + bad + ",1,0,0"; break;
        }
        Refused(raw ?? root.ToString(Formatting.None));
    }

    [Theory]
    [InlineData("minutes")] [InlineData("mask")] [InlineData("shift")] [InlineData("day")] [InlineData("wage")]
    [InlineData("employment-income")] [InlineData("discretionary")] [InlineData("visit")]
    [InlineData("counter")] [InlineData("counter-width")] [InlineData("outside-rows")] [InlineData("outside-window")]
    [InlineData("outside-order")] [InlineData("wide")] [InlineData("journal")] [InlineData("fingerprint")]
    [InlineData("observation-ref")] [InlineData("journal-minute")] [InlineData("journal-subject")] [InlineData("journal-value")]
    public void OneOverOrMalformedValuesRefuseWritesAndReads(string bound)
    {
        var state = Fresh(); Populate(state);
        var person = PersonTable.Schedules(state).First(p => p.DayPlaceId.Length != 0);
        var accrual = person.Accrual;
        var budget = state.Food!.Residents[0].Budget!; var owner = budget.Ledger;
        var shop = state.Economy.Shops[0];
        switch (bound)
        {
            case "minutes": accrual.AttendedMinutes = 1441; break;
            case "mask": accrual.MissMask = 1 << 14; break;
            case "shift": accrual.Shift = 1440; break;
            case "day": accrual.Day = (int)(state.Minute / 1440) + 1; break;
            case "wage": accrual.UnpostedWage = Money.FromUnits(-1); break;
            case "employment-income": owner.EmploymentIncome = Money.FromUnits(-1); break;
            case "discretionary": owner.Discretionary = Money.FromUnits(-1); break;
            case "visit": owner.VisitWatermark = (long)state.Minute + 1; break;
            case "counter": state.Food.History[0].PeriodCauses.Counts[0] = MealPeriodCauses.MaximumCount + 1; break;
            case "counter-width": state.Food.History[0].PeriodCauses.Counts = new int[17]; break;
            case "outside-rows": state.Economy.Outside = Enumerable.Range(0, 15).Select(i => new OutsideSpendingDay { Day = i }).ToList(); break;
            case "outside-window": state.Economy.Outside[0].Day = -1; break;
            case "outside-order": state.Economy.Outside.Add(state.Economy.Outside[0]); break;
            case "wide": state.Economy.Outside[0].RetailUnits = BigInteger.Pow(10, 26); break;
            case "journal": shop.Changes = Enumerable.Range(0, 9).Select(i => new ShopChange { Kind = ShopChangeKind.StaffedCounters, Before = i, After = i + 1 }).ToList(); break;
            case "fingerprint": shop.Fingerprint += "A"; break;
            case "observation-ref": shop.BusinessRef = SaveCodec.MaxPlaces; break;
            case "journal-minute": { var c = shop.Changes[0]; c.ObservedMinute = (int)state.Minute + 1; shop.Changes[0] = c; break; }
            case "journal-subject": { var c = shop.Changes[3]; c.Subject = 7 * SaveCodec.MaxOpeningWindows * 2; shop.Changes[3] = c; break; }
            case "journal-value": { var c = shop.Changes[3]; c.After = 1442; shop.Changes[3] = c; break; }
        }
        person.Accrual = accrual; budget.Ledger = owner;
        // Capture is deliberately unvalidated; use the actual encoder settings to exercise the strict reader too.
        WriteRefused(state);
        try
        {
            var contextType = typeof(SaveCodec).GetNestedType("WireContext", System.Reflection.BindingFlags.NonPublic)!;
            var context = contextType.GetMethod("FromState")!.Invoke(null, new object[] { state });
            var settings = (JsonSerializerSettings)typeof(SaveCodec).GetMethod("Settings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .Invoke(null, new object[] { context! })!;
            Refused(JsonConvert.SerializeObject(state, Formatting.None, settings));
        }
        catch (SaveValidationException) { }
        catch (JsonSerializationException e) when (e.InnerException is SaveValidationException) { }
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void ExtendedConservationChecksOverflowOnBothSidesBeforePosting(bool left)
    {
        var state = Fresh(); var b = state.Food!.Residents[0].Budget!;
        if (left) { b.OpeningBalance = Money.FromUnits(long.MaxValue); b.Ledger = new OwnerAccounts { EmploymentIncome = Money.FromUnits(1) }; }
        else { b.Balance = Money.FromUnits(long.MaxValue); b.Ledger = new OwnerAccounts { Discretionary = Money.FromUnits(1) }; }
        Assert.Throws<OverflowException>(() => HouseholdFinance.CashConserved(b));
        WriteRefused(state);
    }
    [Fact]
    public void AllObservedShopsAtBoundAndOneOverRefuseWithoutTruncation()
    {
        var state = Fresh();
        state.Food!.Shops.Clear();
        for (int i = 0; i < EconomicLedger.MaximumShops; i++)
        {
            string id = "ledger-shop:" + i;
            state.Places.Add(new Place { Id = id, Name = id, Kind = PlaceKind.Shop });
            state.Food.Shops.Add(new FoodScope { BusinessId = id, PlaceId = id, ProductId = FoodProducts.Croissant });
            state.Food.BusinessReferences.Add(new BusinessReference { Slot = i, BusinessId = id, PlaceId = id });
            state.Economy.Shops.Add(new ShopObservation { BusinessRef = i });
        }
        AssertRoundTrip(state);
        var root = SaveWire.Parse(SaveCodec.Write(state));
        ((JArray)root["Economy"]!["Shops"]!).Add(root["Economy"]!["Shops"]![0]!.DeepClone());
        Refused(root.ToString(Formatting.None));
        state.Economy.Shops.Add(state.Economy.Shops[0]); WriteRefused(state);
        Assert.Equal(1025, state.Economy.Shops.Count);
    }
    [Fact]
    public void OwnerMoneyUsesTheEntireExactRange()
    {
        var state = Fresh(); var b = state.Food!.Residents[0].Budget!;
        b.OpeningBalance = b.IncomeReceived = b.HomeFoodPaid = b.EssentialsPaid = b.PreparedFoodSpent = b.ExternalFoodSpent = b.HeldPurchase = Money.Zero;
        b.Balance = Money.FromUnits(long.MaxValue);
        b.Ledger = new OwnerAccounts { EmploymentIncome = b.Balance };
        AssertRoundTrip(state);
        b.Balance = Money.Zero;
        b.Ledger = new OwnerAccounts { EmploymentIncome = Money.FromUnits(long.MaxValue), Discretionary = Money.FromUnits(long.MaxValue) };
        AssertRoundTrip(state);
        var root = SaveWire.Parse(SaveCodec.Write(state));
        const string overflow = "1Y2P0IJ32E8E8";
        root["Citizens"]![0]!["Wallet"]![20] = overflow + new string('0', 19);
        Refused(root.ToString(Formatting.None));
    }

    [Fact]
    public void HoursJournalCanCarryTheLastWindowEdgeAndAnAbsentEdge()
    {
        var state = Fresh(); Populate(state);
        var change = state.Economy.Shops[0].Changes[3];
        change.Subject = 7 * SaveCodec.MaxOpeningWindows * 2 - 1; change.After = 1441;
        state.Economy.Shops[0].Changes[3] = change;
        AssertRoundTrip(state);
    }

    [Fact]
    public void EmploymentIncomeTotalOverflowRefusesPostingAtomically()
    {
        var b = new ResidentBudget { IncomeSource = ResidentIncomeSource.ExternalSupport, Balance = Money.FromUnits(100), EssentialsPaid = Money.FromUnits(long.MaxValue - 100),
            Ledger = new OwnerAccounts { EmploymentIncome = Money.FromUnits(long.MaxValue) }, DailyIncomeCents = 1, DailyHomeFoodCents = 1 };
        Assert.True(HouseholdFinance.CashConserved(b));
        string before = JsonConvert.SerializeObject(b);
        Assert.Throws<OverflowException>(() => HouseholdFinance.PostDay(b, 0));
        Assert.Equal(before, JsonConvert.SerializeObject(b));
    }

    [Fact]
    public void ExtendedIdentityIncludesBothNewAccountsAndEveryExistingMutation()
    {
        var b = new ResidentBudget { IncomeSource = ResidentIncomeSource.ExternalSupport, OpeningBalance = Money.FromCents(1000), Balance = Money.FromCents(1000), DailyIncomeCents = 100, DailyHomeFoodCents = 1 };
        b.Ledger = new OwnerAccounts { EmploymentIncome = Money.FromUnits(9), Discretionary = Money.FromUnits(9) };
        Assert.True(HouseholdFinance.CashConserved(b));
        HouseholdFinance.PostDay(b, 0); Assert.True(HouseholdFinance.CashConserved(b));
        Assert.True(HouseholdFinance.HoldPurchase(b, Money.FromCents(2))); Assert.True(HouseholdFinance.CashConserved(b));
        HouseholdFinance.RefusePurchase(b); Assert.True(HouseholdFinance.CashConserved(b));
        Assert.True(HouseholdFinance.HoldPurchase(b, Money.FromCents(2))); HouseholdFinance.ConfirmPurchase(b); Assert.True(HouseholdFinance.CashConserved(b));
        var old = b.Ledger; old.Discretionary += Money.FromUnits(1); b.Ledger = old;
        Assert.False(HouseholdFinance.CashConserved(b));
    }
}
