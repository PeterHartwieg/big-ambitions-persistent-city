using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using PersistentCity.Core;
using PersistentCity.EconomyInterventions;
using PersistentCity.Persistence;
using PersistentCity.Tests.Acceptance;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

/// <summary>F9c (plan f9-city-acceptance.md, Decision 3 without body arms): the integrated city in two sizes, the F1 cadence
/// and budget arms, a hidden skip and the in-flight reload arm, each compared with the synchronously drained reference, with
/// the identity, conservation, event-order and native-order oracles, C5 research and an E6 vehicle in the same city.
/// The Street Food stand-in is static, so this class runs in its collection. Traits <c>Size=city-s</c> and
/// <c>Size=city-m</c> select one size. The scenario never pins its own save hashes.</summary>
[Collection(StreetFoodHooks.Collection)]
public sealed class F9IntegratedCityTests : IClassFixture<F9IntegratedCityTests.Runs>
{
    /// <summary>Every run, computed once per size and arm and shared by the facts.</summary>
    public sealed class Runs
    {
        private readonly Dictionary<string, CityCheckpoint> checkpoints = new(StringComparer.Ordinal);
        private readonly Dictionary<(string, string), CityRun> runs = new();
        internal CityCheckpoint Checkpoint(string size)
        {
            lock (checkpoints)
                return checkpoints.TryGetValue(size, out var c) ? c : checkpoints[size] = CityFixture.Create(size == "city-s" ? CitySize.Small() : CitySize.Medium());
        }
        internal CityRun Run(string size, string arm)
        {
            var checkpoint = Checkpoint(size);
            lock (runs)
            {
                if (runs.TryGetValue((size, arm), out var run)) return run;
                var spec = arm == "reference" ? CityArm.ReferenceArm : CityArm.Arms().Single(a => a.Name == arm);
                return runs[(size, arm)] = CityScenario.Run(checkpoint, spec);
            }
        }
    }

    private readonly Runs runs;
    private readonly ITestOutputHelper output;
    public F9IntegratedCityTests(Runs runs, ITestOutputHelper output) { this.runs = runs; this.output = output; }

    public static IEnumerable<object[]> ArmNames() => CityArm.Arms().Select(a => new object[] { a.Name });
    public static IEnumerable<object[]> Faults() => new[]
    {
        new object[] { CityFault.DuplicateSale, "duplicate native order" },
        new object[] { CityFault.SkippedInput, "skipped" },
        new object[] { CityFault.ResearchChargeWithoutReport, "without a delivered report" },
        new object[] { CityFault.VendorSaleOutsideLease, "vendor sale outside its lease" },
    };

    // ---- The fixture ----

    [Fact, Trait("Size", "city-s")] public void SmallFixtureIsTheDeclaredComposition() => Composition("city-s", 126, 1, 150, 260);
    [Fact, Trait("Size", "city-m")] public void MediumFixtureIsTheDeclaredComposition() => Composition("city-m", 957, 3, 2230, 2870);

    private void Composition(string size, int residents, int shops, int minPersons, int maxPersons)
    {
        var checkpoint = runs.Checkpoint(size);
        var state = SaveCodec.Read(checkpoint.Payload).State!;
        Assert.Equal(CityState.CurrentSchema, state.Schema);
        Assert.Equal(CitySize.Seed, state.Seed);
        Assert.Equal(CityFixture.StartMinute, state.Minute);
        Assert.Equal(residents, state.Citizens.Count);
        Assert.Empty(state.Visitors);
        Assert.Equal(shops, state.Food!.Shops.Count);
        Assert.All(state.Food.Shops, s => Assert.Equal(FoodScopeChannel.Shop, s.Channel));
        Assert.Equal(2, state.Portals.Count);
        Assert.Contains(state.Places, p => p.Id == CityFixture.RivalPlace);
        Assert.Empty(state.Vendors.Rows);
        Assert.Empty(state.Research.Open);
        // The fill grows the city toward its census targets over the run (F5h's declared load for city-m).
        var reference = runs.Run(size, "reference");
        int persons = reference.State.Citizens.Count + reference.State.Visitors.Count;
        Assert.InRange(persons, minPersons, maxPersons);
        // The vehicle joined the city as a vehicle scope; research holds the three delivered reports.
        Assert.Single(reference.State.Food!.Shops, s => s.Channel == FoodScopeChannel.Vehicle);
        Assert.Equal(3, reference.State.Research.Kept.Count);
        output.WriteLine($"{size}: fixture sha256 {checkpoint.Sha256}, {residents} residents, {persons} persons after {CityScenario.Days} days");
    }

    // ---- Every arm equals the reference ----

    [Theory, Trait("Size", "city-s"), MemberData(nameof(ArmNames))]
    public void SmallArmEqualsTheReference(string arm) => ArmEqualsTheReference("city-s", arm);

    [Theory, Trait("Size", "city-m"), MemberData(nameof(ArmNames))]
    public void MediumArmEqualsTheReference(string arm) => ArmEqualsTheReference("city-m", arm);

    private void ArmEqualsTheReference(string size, string name)
    {
        var reference = runs.Run(size, "reference");
        var arm = runs.Run(size, name);
        // Ledger in order, the save at every drained day boundary, native orders, vendor sales and the final save.
        CityOracles.Equal(reference, arm);
        Assert.Equal(new long[] { 2 * 1440, 3 * 1440, 4 * 1440 }, arm.DaySaves.Select(d => d.Boundary));
        Assert.Equal(CityScenario.Days * 24 + 1, arm.Checkpoints);
        Assert.Equal(0, arm.HistoricalClockMisses);
        Assert.Equal(0, arm.HistoricalCheckouts);
        Assert.InRange(arm.MaxLag, 0, AccountingClock.MaximumLagMinutes);
        Assert.True(arm.Frames > CityScenario.Days * 1440, "frames " + arm.Frames);
        // The reference is a populated city: meals, native orders, vendor sales, journeys and every scheduled input.
        Assert.True(reference.Orders.Count > (size == "city-s" ? 200 : 2000), "orders " + reference.Orders.Count);
        Assert.True(reference.VendorSales.Count > 50, "vendor sales " + reference.VendorSales.Count);
        Assert.True(reference.Ledger.Count(l => l.StartsWith("a|", StringComparison.Ordinal)) > reference.State.Citizens.Count * CityScenario.Days);
        Assert.Equal(reference.Inputs.Count, reference.InputsPublished);
        Assert.Equal(reference.Inputs.Select(i => i.Json()), arm.Inputs.Select(i => i.Json()));
        // Daily and shift postings are events in the ledger, in the same order as the reference's.
        Assert.True(reference.Postings > reference.State.Citizens.Count, "postings " + reference.Postings);
        Assert.Equal(reference.Postings, arm.Postings);
        output.WriteLine($"{size} {name}: {arm.Seconds:F2} s tool time ({arm.ToolMsPerDay:F0} ms per simulated day), {arm.Frames} frames, "
            + $"{arm.Ledger.Count} events; reference {reference.Seconds:F2} s ({reference.ToolMsPerDay:F0} ms per day)");
    }

    // ---- In-flight reloads ----

    [Fact, Trait("Size", "city-s")] public void SmallReloadArmReachesEveryInFlightState() => ReloadStates("city-s");
    [Fact, Trait("Size", "city-m")] public void MediumReloadArmReachesEveryInFlightState() => ReloadStates("city-m");

    private void ReloadStates(string size)
    {
        var run = runs.Run(size, "reload");
        // Each state was true when the arm saved, and the scenario checked it again on the loaded city.
        foreach (CityReload state in Enum.GetValues(typeof(CityReload))) Assert.Contains(run.Reloads, r => r.State == state);
        Assert.Equal(new double[] { 2 * 1440, 3 * 1440, 4 * 1440 }, run.Reloads.Where(r => r.State == CityReload.DayBoundary).Select(r => r.Minute));
        // The fill reload falls on the first boundary, while its fill is still running.
        Assert.Equal(2 * 1440, run.Reloads.Single(r => r.State == CityReload.BetweenFillUnits).Minute);
        // The research reload falls between the survey and that night's bill; the vendor reload inside the first opening.
        double research = run.Reloads.Single(r => r.State == CityReload.ResearchPendingBilling).Minute;
        Assert.InRange(research, CityFixture.ResearchOrders()[0].Minute, 2 * 1440);
        double vendor = run.Reloads.Single(r => r.State == CityReload.VendorLeaseOpen).Minute;
        Assert.InRange(vendor, 1440 + CityFixture.VehicleOpens, 1440 + CityFixture.VehicleCloses);
        output.WriteLine(size + " reloads: " + string.Join(", ", run.Reloads.Select(r => r.State + "@" + r.Minute.ToString("F2", CultureInfo.InvariantCulture))));
    }

    // ---- Timestamped native changes ----

    [Fact, Trait("Size", "city-s")] public void SmallNativeChangesTakeEffectAtTheirMinute() => NativeChanges("city-s");
    [Fact, Trait("Size", "city-m")] public void MediumNativeChangesTakeEffectAtTheirMinute() => NativeChanges("city-m");

    private static (double Minute, string Business, string Product, long Units) Order(string row)
    {
        var f = row.Split('|');
        return (double.Parse(f[1], CultureInfo.InvariantCulture), f[3], f[4], long.Parse(f[5], CultureInfo.InvariantCulture));
    }

    private void NativeChanges(string size)
    {
        var reference = runs.Run(size, "reference");
        var orders = reference.Orders.Select(Order).Where(o => o.Business == CityFixture.Shop(0)).ToList();
        long Base(string product) => Money.FromCents(CityFixture.Menu.Single(m => m.Product == product).Cents).Units;
        long Raised(string product) => Money.FromCents((int)Math.Round(CityFixture.Menu.Single(m => m.Product == product).Cents * 1.5, MidpointRounding.AwayFromZero)).Units;
        double raise = 2 * 1440 + 9 * 60, revert = 3 * 1440 + 5 * 60;
        // Before the raise every order is at the base menu, between raise and revert at +50%, after the revert at the base again.
        Assert.All(orders.Where(o => o.Minute < raise), o => Assert.Equal(Base(o.Product), o.Units));
        Assert.All(orders.Where(o => o.Minute >= raise && o.Minute < revert), o => Assert.Equal(Raised(o.Product), o.Units));
        Assert.All(orders.Where(o => o.Minute >= revert), o => Assert.Equal(Base(o.Product), o.Units));
        Assert.Contains(orders, o => o.Minute >= raise && o.Minute < revert);
        Assert.Contains(orders, o => o.Minute >= revert);
        // The sold-out product sells before 12:00 on day 2 and not again until the revert; nothing sells after the 18:00 close.
        Assert.Contains(orders, o => o.Product == FoodProducts.HotDog && o.Minute < 2 * 1440 + 12 * 60);
        Assert.DoesNotContain(orders, o => o.Product == FoodProducts.HotDog && o.Minute >= 2 * 1440 + 12 * 60 && o.Minute < revert);
        Assert.DoesNotContain(orders, o => o.Minute >= 2 * 1440 + 18 * 60 && o.Minute < 3 * 1440);
        Assert.Contains(orders, o => o.Product == FoodProducts.HotDog && o.Minute >= revert);
        // The workplace closure reaches workers' routines only while it is published.
        var closed = reference.Ledger.Where(l => l.StartsWith("r|", StringComparison.Ordinal) && l.Split('|')[4] == "closed")
            .Select(l => double.Parse(l.Split('|')[1], CultureInfo.InvariantCulture)).ToList();
        Assert.NotEmpty(closed);
        Assert.All(closed, minute => Assert.InRange(minute, 2 * 1440 + 5 * 60, 3 * 1440 + 60 + 24 * 60));
        // Each input appears once in the ledger, at its own minute, in schedule order.
        var published = reference.Ledger.Where(l => l.StartsWith("i|", StringComparison.Ordinal)).Select(l => l.Substring(2)).ToList();
        Assert.Equal(reference.Inputs.Select(i => i.Json()), published);
    }

    // ---- C5 research and the E6 vehicle in the same city ----

    [Fact, Trait("Size", "city-s")] public void SmallResearchOrderOfEachProductIsBilledOnceAndReportedTheNextMorning() => Research("city-s");
    [Fact, Trait("Size", "city-m")] public void MediumResearchOrderOfEachProductIsBilledOnceAndReportedTheNextMorning() => Research("city-m");

    private void Research(string size)
    {
        foreach (var name in new[] { "reference", "reload" })
        {
            var run = runs.Run(size, name);
            Assert.Equal(ResearchProducts.All.Select(p => p.Code).OrderBy(c => c, StringComparer.Ordinal), run.Research.Keys.OrderBy(c => c, StringComparer.Ordinal));
            foreach (var fact in run.Research.Values)
            {
                double midnight = Math.Floor(fact.OrderedAt / 1440) * 1440 + 1440;
                // Surveyed inside the F1 hold of the order's own minute, billed at that night's bill, reported that morning.
                Assert.Equal(fact.OrderedAt + 1, fact.SurveyedBy);
                Assert.Equal(midnight, fact.BilledAt);
                Assert.Equal("Delivered", fact.Notice);
                Assert.Equal(midnight, fact.NoticeAt);
                Assert.True(fact.ReadableNextMorning, fact.Product + " report not readable at 06:00");
            }
            // One charge per order at its price; native money moved by exactly the three prices.
            Assert.Equal(3, run.ResearchCharges);
            // Each charge is booked to the native day its midnight bill closes (Runtime's port: the previous day).
            Assert.Equal(run.Research.Values.Select(f => (int)(f.BilledAt / 1440) - 1).OrderBy(d => d),
                run.ResearchChargeDays.Select(c => c.Day).OrderBy(d => d));
            Assert.All(run.ResearchChargeDays, c => Assert.Equal((int)(run.State.Research.Find(c.Order)!.OrderMinute / 1440), c.Day));
            Assert.Equal(3, run.State.Research.Kept.Count);
            Assert.Empty(run.State.Research.Open);
            Assert.Empty(run.State.Research.Closed);
            Assert.Single(run.Ledger, l => l.StartsWith("b|" + (2 * 1440) + "|", StringComparison.Ordinal));
        }
    }

    [Fact, Trait("Size", "city-s")] public void SmallVendorScopeSellsToResidentsAndHandsBackOnRelease() => Vendor("city-s");
    [Fact, Trait("Size", "city-m")] public void MediumVendorScopeSellsToResidentsAndHandsBackOnRelease() => Vendor("city-m");

    private void Vendor(string size)
    {
        foreach (var name in new[] { "reference", "reload" })
        {
            var run = runs.Run(size, name);
            Assert.NotEqual("", run.VendorBusiness);
            // Residents chose the vehicle and it sold to them, every sale inside an opening and under the lease (oracle).
            Assert.Contains(run.Ledger, l => l.StartsWith("d|", StringComparison.Ordinal) && l.Split('|')[5] == run.VendorBusiness);
            Assert.NotEmpty(run.VendorSales);
            Assert.All(run.VendorSales, s => Assert.InRange(s.Minute % 1440, (long)CityFixture.VehicleOpens, (long)CityFixture.VehicleCloses - 1));
            foreach (int day in new[] { 1, 2, 3 }) Assert.Contains(run.VendorSales, s => s.Minute / 1440 == day);
            // Withdrawn on day 3: handed back once committed residents drained, while Street Food keeps the vehicle open,
            // and no sale after the handback.
            Assert.NotNull(run.HandbackMinute);
            Assert.InRange(run.HandbackMinute!.Value, (long)CityFixture.WithdrawMinute + 1, 3 * 1440 + (long)CityFixture.VehicleCloses - 1);
            Assert.Equal("", run.HandbackOwner);
            Assert.DoesNotContain(run.VendorSales, s => s.Minute >= run.HandbackMinute.Value);
            Assert.Single(run.State.Vendors.Rows);
        }
    }

    // ---- Fault injection fails loudly ----

    [Theory, Trait("Size", "city-s"), MemberData(nameof(Faults))]
    public void AnInjectedFaultFailsTheScenario(CityFault fault, string message)
    {
        var checkpoint = runs.Checkpoint("city-s");
        var error = Assert.Throws<CityViolation>(() => CityScenario.Run(checkpoint, CityArm.ReferenceArm with { Name = "fault-" + fault, Fault = fault }));
        Assert.Contains(message, error.Message);
        output.WriteLine(fault + ": " + error.Message);
    }

    [Fact, Trait("Size", "city-s")]
    public void AnArmThatSkipsAnInputDiffersFromTheReferenceEvenWithoutTheScheduleOracle()
    {
        // The comparison alone also catches a dropped event: remove one published input from a copy of the reference ledger.
        var reference = runs.Run("city-s", "reference");
        var copy = new CityRun { Arm = "dropped" };
        copy.Ledger.AddRange(reference.Ledger.Where((l, i) => i != reference.Ledger.FindIndex(x => x.StartsWith("i|", StringComparison.Ordinal))));
        Assert.Contains("differs", Assert.Throws<CityViolation>(() => CityOracles.Equal(reference, copy)).Message);
    }

    [Fact, Trait("Size", "city-s")]
    public void PostingsSwappedInExecutionWithEqualBalancesFailTheEventOrderOracle()
    {
        // The posting queue's first two midnight wallets post in the other order. Every oracle passes and every amount is
        // equal; only the posting capture, which records each posting where it executes, sees the change.
        var reference = runs.Run("city-s", "reference");
        var swapped = CityScenario.Run(runs.Checkpoint("city-s"), CityArm.ReferenceArm with { Name = "fault-reordered-postings", Fault = CityFault.ReorderedPostings });
        Assert.Equal(reference.Postings, swapped.Postings);
        Assert.Equal(reference.Ledger.Where(l => l.StartsWith("h|", StringComparison.Ordinal)), swapped.Ledger.Where(l => l.StartsWith("h|", StringComparison.Ordinal)));
        Assert.Equal(reference.Final, swapped.Final);
        var error = Assert.Throws<CityViolation>(() => CityOracles.Equal(reference, swapped));
        Assert.Contains("differs", error.Message);
        Assert.Contains("'p|" + (2 * 1440), error.Message);
        output.WriteLine(error.Message);
        // Each sale row also carries its buyer's wallet as checkout found it.
        Assert.All(reference.Orders, o => Assert.Equal(7, o.Split('|')[6].Split('/').Length));
    }

    // ---- Saves ----

    [Fact, Trait("Size", "city-s")]
    public void TheScenarioCityWrittenAsTheOlderWriterIsRefusedWithStartANewCityAndKept()
    {
        var reference = runs.Run("city-s", "reference");
        int older = CityState.CurrentSchema - 1;
        Assert.Equal(SaveCodec.LatestDevelopmentSchema, older);
        string prefix = "{\"Schema\":" + CityState.CurrentSchema.ToString(CultureInfo.InvariantCulture) + ",";
        Assert.StartsWith(prefix, reference.Final);
        var payloads = new List<string> { "{\"Schema\":" + older.ToString(CultureInfo.InvariantCulture) + "," + reference.Final.Substring(prefix.Length) };
        // The frozen writer of that schema, where the development chain has one, writes the same city as an older build did.
        var frozen = typeof(DevelopmentChain).GetMethod("Write" + older.ToString(CultureInfo.InvariantCulture), BindingFlags.Public | BindingFlags.Static);
        if (frozen != null) payloads.Add((string)frozen.Invoke(null, new object[] { SaveCodec.Read(reference.Final).State! })!);
        foreach (var payload in payloads)
        {
            Assert.Equal(older, SaveCodec.DevelopmentSchemaOf(payload));
            var read = SaveCodec.Read(payload);
            Assert.Equal(SaveReadStatus.DevelopmentSchema, read.Status);
            Assert.Null(read.State);
            Assert.Contains("start a new city", read.Error);
            // Refused through the store and the session, never overwritten, kept byte for byte.
            EconomicLedgerTests.Refused(payload, SaveReadStatus.DevelopmentSchema);
        }
        // The current writer reads the same city back byte for byte.
        var current = SaveCodec.Read(reference.Final);
        Assert.True(current.Status == SaveReadStatus.Loaded, current.Error);
        Assert.Equal(reference.Final, SaveCodec.Write(current.State!));
        output.WriteLine("refused payloads: " + payloads.Count + (frozen == null ? " (header only; no frozen writer " + older + ")" : ""));
    }

    /// <summary>F9c changes no wire: the writer-35 golden pins (F2e, PlayerHoldTests) are unchanged on this branch. Once a later
    /// writer merges, writer 35 is frozen as DevelopmentChain.Write35 and the same pins apply to it.</summary>
    [Theory]
    [InlineData(5150, "4187373b9cca6eb6c619e7aae9e1d270d067e5bc1ac40ee64c1aee5d02e210ae", 73332)]
    [InlineData(9217, "149d635dbab32312988b96bcde19d011fe1a06814db5a9f175380d29741a5abb", 72970)]
    public void CurrentWriterGoldenHashIsUnchanged(int seed, string pin, int characters)
    {
        Assert.Equal(35, CityState.PlayerHoldSchema);
        var state = ChangeJournalTests.Fixture(seed);
        string payload;
        int writer = CityState.CurrentSchema;
        if (writer == CityState.PlayerHoldSchema) payload = SaveCodec.Write(state);
        else
        {
            var frozen = typeof(DevelopmentChain).GetMethod("Write35", BindingFlags.Public | BindingFlags.Static);
            Assert.True(frozen != null, "a later writer merged without freezing writer 35 in DevelopmentChain");
            payload = (string)frozen!.Invoke(null, new object[] { state })!;
        }
        Assert.Equal(pin + " " + characters, EconomicLedgerTests.Sha(payload) + " " + payload.Length);
    }
}
