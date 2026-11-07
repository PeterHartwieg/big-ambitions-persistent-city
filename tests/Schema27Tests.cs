using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>F5d (#92): writer 27, independent schedules. Fresh cities run every visitor on its own routine from the
/// first day (portal arrival and departure, own stops, meal rhythm, income source); there is no switch-over day, no
/// host or anchor read and no represented excursion. Older development cities are refused and kept unchanged.</summary>
public sealed class Schema27Tests
{
    // ---- Fixtures ----

    internal static readonly PortalStation[] Stations =
    {
        new("subway:north", new Position(120, 0, 60)), new("subway:south", new Position(-80, 0, -60)),
    };

    /// <summary>A detached native food double: confirmed sales, optional lost acknowledgements (Uncertain) and receipt
    /// evidence that can be withheld, so an uncertain checkout can stay unresolved across dormancy.</summary>
    internal sealed class Market : IFoodMarket, IFoodSaleEvidence
    {
        public FoodBusiness[] Shops = Array.Empty<FoodBusiness>();
        public readonly List<string> Orders = new();
        public readonly HashSet<string> Paid = new(StringComparer.Ordinal);
        public Func<ResidentMeal, bool> LoseAcknowledgement = _ => false;
        public bool EvidenceAvailable = true;
        public double EarliestMinute => 0;
        public IReadOnlyList<FoodBusiness> Read(double minute) => Shops;
        public FoodSale Buy(ResidentMeal meal, double minute)
        {
            var offer = Shops.Single(b => b.Id == meal.BusinessId).Offers.Single(o => o.ProductId == meal.ProductId);
            if (offer.Price != meal.Price || offer.Units <= 0) return new FoodSale(false, "Price or stock changed");
            Assert.True(Paid.Add(meal.IntentId), "duplicate native order " + meal.IntentId);
            Orders.Add(meal.IntentId);
            if (LoseAcknowledgement(meal)) throw new InvalidOperationException("Lost native acknowledgement");
            return new FoodSale(true, "Confirmed");
        }
        public bool IsConfirmedPaid(ResidentMeal meal) => EvidenceAvailable && Paid.Contains(meal.IntentId);
    }

    internal static (CityState State, Market Market) Fresh(int factor = 2, int seed = 4242, bool portals = true)
    {
        var (state, trace) = FreshCityTraceTests.Fresh(factor, true, seed, CityState.CurrentSchema);
        if (portals) PortalPlaces.Add(state, Stations);
        return (state, new Market { Shops = trace.Shops });
    }

    internal static CitySimulator Start(CityState state, Market market, IRoutePlanner? routes = null)
    {
        var city = new CitySimulator(state, routes ?? new FreshCityTraceTests.Walk());
        city.AttachFoodMarket(market);
        return city;
    }

    internal static CitySimulator Running(int factor = 2, double hours = 30, int seed = 4242)
    {
        var (state, market) = Fresh(factor, seed);
        var city = Start(state, market);
        Advance(city, state.Minute + hours * 60);
        return city;
    }

    private static void Advance(CitySimulator city, double minute, int budget = 64) { while (!city.AdvanceTo(minute, budget).CaughtUp) { } }

    private static CitySimulator Reload(CitySimulator city, Market market, IRoutePlanner? routes = null)
    {
        string payload = SaveCodec.Write(city.State);
        var read = SaveCodec.Read(payload);
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(payload, SaveCodec.Write(read.State!));
        return Start(read.State!, market, routes);
    }

    private static IEnumerable<ResidentMeal> VisitorMeals(CityState state)
        => state.Food!.Residents.Where(m => m.ResidentId.StartsWith(PersonIds.VisitorPrefix, StringComparison.Ordinal));

    private static string Sha(string text) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private static void RefusedAndPreserved(string payload, CityState replacement, SaveReadStatus expected, string? error = null)
    {
        var read = SaveCodec.Read(payload);
        Assert.True(read.Status == expected, read.Status + ": " + read.Error); Assert.Null(read.State);
        if (error != null) Assert.Contains(error, read.Error);
        var data = new Dictionary<string, string> { [SaveStore.StateKey] = payload, ["rlo:state"] = "kept", [FoodAuthorityStore.Key] = "manifest" };
        Assert.Equal(expected, SaveSession.Open(data, replacement.PopulationKey).InitialReadResult.Status);
        Assert.False(SaveStore.WriteTo(data, replacement).Success);
        Assert.False(SaveSession.Open(data, replacement.PopulationKey).WriteTo(data, replacement).Success);
        Assert.Same(payload, data[SaveStore.StateKey]); Assert.Equal("kept", data["rlo:state"]); Assert.Equal("manifest", data[FoodAuthorityStore.Key]);
    }

    private static BigInteger[] Ledger(CityState state)
    {
        var totals = new BigInteger[2];
        foreach (var b in state.Food!.Residents.Select(m => m.Budget!))
        {
            Assert.True(HouseholdFinance.CashConserved(b));
            totals[0] += Money.TotalUnits(new[] { b.OpeningBalance, b.IncomeReceived });
            totals[1] += Money.TotalUnits(new[] { b.Balance, b.EssentialsPaid, b.HomeFoodPaid, b.PreparedFoodSpent, b.ExternalFoodSpent, b.HeldPurchase });
        }
        return totals;
    }

    // ---- Golden current writer ----

    private static string GoldenPath => Path.Combine(AppContext.BaseDirectory, "Writer27", "golden.json");

    private static IEnumerable<(string Name, Func<CityState> Build)> GoldenFixtures() => new (string, Func<CityState>)[]
    {
        ("fresh-visitors-factor2-30h", () => Running(2, 30).State),
        ("current-schema-minimal", CurrentSchemaOnlyTests.City),
    };

    /// <summary>Two seeded fixtures pinned for writer 27. F5e: writer 27 is now the frozen F5d writer (same shape as
    /// writer 28, seated under the fixed allocation); its pins are unchanged and its frozen reader round-trips them.</summary>
    [Fact]
    public void GoldenWriter27BytesArePinned()
    {
        Assert.Equal(27, SaveCodec.IndependentSchema);
        var pins = JObject.Parse(File.ReadAllText(GoldenPath));
        Assert.Equal(27, (int)pins["writer"]!);
        var actual = new List<string>(); var expected = new List<string>();
        foreach (var (name, build) in GoldenFixtures())
        {
            var state = build();
            string payload = DevelopmentChain.Write27(state);
            actual.Add(name + " " + Sha(payload) + " " + payload.Length);
            expected.Add(name + " " + (string)pins["fixtures"]![name]!["sha256"]! + " " + (int)pins["fixtures"]![name]!["characters"]!);
            var read = SaveCodec.ReadWriter27(payload); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
            Assert.Equal(payload, SaveCodec.WriteForSchema(read.State!, 27));
        }
        Assert.True(expected.SequenceEqual(actual), string.Join("; ", actual));
    }

    [Fact]
    public void GoldenFixturesAreDeterministic()
    {
        foreach (var (_, build) in GoldenFixtures()) Assert.Equal(SaveCodec.Write(build()), SaveCodec.Write(build()));
    }

    // ---- Fresh cities: independent persons from the first day ----

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void FreshCityCreatesVisitorPersonsWithTheirOwnRoutinesAndRoundTrips(int factor)
    {
        var city = Running(factor, 26);
        var state = city.State;
        Assert.Equal((factor - 1) * state.Citizens.Count, state.Visitors.Count);
        Assert.Equal(state.Visitors.Count, state.VisitorRoutines.Count);
        Assert.Equal(state.Visitors.Count + 1, state.NextVisitorSequence);
        Assert.Equal(new[] { "subway:north", "subway:south" }, state.Portals);
        Assert.Equal(state.Visitors.Select(v => v.Id).OrderBy(id => id, StringComparer.Ordinal), state.Visitors.Select(v => v.Id));
        for (int i = 0; i < state.Visitors.Count; i++)
        {
            var visitor = state.Visitors[i]; var routine = state.VisitorRoutines[i];
            Assert.Equal(PersonIdFormat.Visitor, PersonIds.Classify(visitor.Id));
            Assert.Same(routine, visitor.Routine); Assert.Equal(visitor.Id, routine.Identity.Id);
            Assert.Equal(VisitorPattern.Commuter, visitor.Stay.Pattern);
            Assert.Contains(visitor.Stay.OriginPortalId, state.Portals);
            Assert.Equal(visitor.Stay.OriginPortalId, routine.HomeId);
            Assert.Null(routine.Car); Assert.False(routine.Preferences.OwnsCar);
            Assert.Equal(VisitorSchedules.Dormant(routine) ? VisitorPresence.Dormant : VisitorPresence.Active, visitor.Stay.Presence);
            Assert.Equal(routine.DayPlaceId.Length == 0, visitor.Employment == null);
        }
        // Rule 2: one own roster record per person, no host, routine stop or represented excursion.
        Assert.Equal(PersonTable.PersonCount(state), state.Food!.Residents.Count);
        Assert.All(state.Food.Residents, m => { Assert.Equal(m.ResidentId, m.HostId); Assert.Equal(0, m.HostSlot); Assert.Null(m.Excursion); });
        Assert.Empty(state.Food.RoutineStops);
        string payload = SaveCodec.Write(state);
        Assert.Contains("\"Schema\":" + CityState.CurrentSchema + ",", payload);
        Assert.Contains("\"VisitorRoutines\":", payload);
        Assert.DoesNotContain("consumer:", payload);
        Assert.DoesNotContain("RepresentationFactor", payload);
        var read = SaveCodec.Read(payload);
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(payload, SaveCodec.Write(read.State!));
        // The reloaded city keeps running on the same routines.
        var again = Start(read.State!, new Market { Shops = ((Market)MarketOf(factor)).Shops });
        Advance(again, again.State.Minute + 180);
    }

    private static IFoodMarket MarketOf(int factor) => Fresh(factor).Market;

    [Fact]
    public void VisitorsArriveThroughTheirPortalWorkEatAndLeaveOnWeekdaysOnly()
    {
        var (state, market) = Fresh();
        var city = Start(state, market);
        var arrivals = new Dictionary<string, List<(double At, string Place, Activity Activity)>>(StringComparer.Ordinal);
        city.ActivityObserved += (id, person, at) =>
        {
            if (!person.IsVisitor) return;
            if (!arrivals.TryGetValue(id, out var list)) arrivals[id] = list = new();
            if (list.Count == 0 || list[^1].Place != person.PlaceId || list[^1].Activity != person.Activity) list.Add((at, person.PlaceId, person.Activity));
        };
        // Day 1 (a weekday) through day 7: days 5-7 are the three-day weekend.
        for (int day = 1; day <= 7; day++)
        {
            Advance(city, day * 1440 + 1440 - 1);
            bool weekend = ((day - 1) % 7 + 7) % 7 >= 4;
            foreach (var routine in state.VisitorRoutines)
                if (weekend) Assert.True(VisitorSchedules.Dormant(routine) || routine.Phase == CitizenPhase.Travelling && routine.DestinationId == routine.HomeId,
                    routine.Identity.Id + " is in the city at the weekend on day " + day);
        }
        Assert.All(state.VisitorRoutines, r => Assert.True(r.CompletedJourneys > 0));
        foreach (var routine in state.VisitorRoutines.Where(r => r.DayPlaceId.Length != 0))
            Assert.Contains(arrivals[routine.Identity.Id], a => a.Place == routine.DayPlaceId);
        // Every stay starts and ends at the visitor's own portal.
        Assert.All(arrivals, pair => Assert.All(pair.Value.Where(a => a.Activity == Activity.Home),
            a => Assert.Equal(state.Visitors.Single(v => v.Id == pair.Key).Stay.OriginPortalId, a.Place)));
        Assert.Contains(VisitorMeals(state), m => m.Purchases > 0);
        Assert.Equal(0, city.FoodAnchorReads);
        // The read-only person diagnostic behind `pc population person <id>`.
        string line = VisitorSchedules.DescribePerson(state, state.Visitors[0].Id, city.FoodAnchorReads)!;
        Assert.Contains("kind=visitor", line); Assert.Contains("portal=" + state.Visitors[0].Stay.OriginPortalId, line); Assert.Contains("food_anchor_reads=0", line);
        Assert.Contains("kind=resident", VisitorSchedules.DescribePerson(state, state.Citizens[0].Identity.Id, 0)!);
        Assert.Null(VisitorSchedules.DescribePerson(state, "pv:999999", 0));
    }

    // ---- No anchor read at all ----

    [Theory]
    [InlineData(2)] [InlineData(3)]
    public void NoFoodOrFinancePathReadsAnotherPersonsSchedule(int factor)
    {
        var (state, market) = Fresh(factor);
        var city = Start(state, market);
        for (int hour = 1; hour <= 72; hour++)
        {
            Advance(city, state.Minute + 60);
            Assert.Equal(0, city.FoodAnchorReads);
            Assert.Empty(state.Food!.RoutineStops);
            Assert.All(state.Food.Residents, m => { Assert.Equal(m.ResidentId, m.HostId); Assert.Null(m.Excursion); });
            if (hour % 24 == 0) { city = Reload(city, market); state = city.State; }
        }
        Assert.Contains(VisitorMeals(state), m => m.Purchases > 0);
    }

    // ---- Per-person schedule independence ----

    private static Dictionary<string, string> DayTraces(int seed)
    {
        var (state, market) = Fresh(2, seed);
        var city = Start(state, market);
        var traces = new Dictionary<string, StringBuilder>(StringComparer.Ordinal);
        city.ActivityObserved += (id, person, at) =>
        {
            if (!traces.TryGetValue(id, out var text)) traces[id] = text = new StringBuilder();
            text.Append(at.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(':').Append(person.PlaceId).Append(':').Append((int)person.Phase).Append(';');
        };
        Advance(city, state.Minute + 24 * 60);
        return traces.ToDictionary(p => p.Key, p => p.Value.ToString(), StringComparer.Ordinal);
    }

    [Fact]
    public void EveryPersonRunsItsOwnScheduleAcrossSeeds()
    {
        var bySeed = new List<Dictionary<string, string>>();
        foreach (int seed in new[] { 11, 4242, 90001 })
        {
            var traces = DayTraces(seed);
            Assert.Equal(traces, DayTraces(seed)); // deterministic per seed
            var visitors = traces.Where(p => p.Key.StartsWith(PersonIds.VisitorPrefix, StringComparison.Ordinal)).ToList();
            var residents = traces.Where(p => !p.Key.StartsWith(PersonIds.VisitorPrefix, StringComparison.Ordinal)).ToList();
            Assert.NotEmpty(visitors);
            // No visitor follows a resident's day, and visitors do not share one day between them.
            Assert.All(visitors, v => Assert.DoesNotContain(residents, r => r.Value == v.Value));
            Assert.True(visitors.Select(v => v.Value).Distinct().Count() >= visitors.Count - 1, "visitor days collapse onto each other");
            bySeed.Add(traces);
        }
        Assert.NotEqual(bySeed[0].Where(p => p.Key.StartsWith("pv:")).Select(p => p.Value), bySeed[1].Where(p => p.Key.StartsWith("pv:")).Select(p => p.Value));
    }

    // ---- Cadence, budgets, skips and reload ----

    private static (string Save, string Orders) Day(int budget, Func<CitySimulator, bool>? reloadWhen = null, bool frameSlices = false)
    {
        var (state, market) = Fresh(2);
        market.LoseAcknowledgement = m => m.IntentId.GetHashCode(StringComparison.Ordinal) % 9 == 0 && m.ResidentId.StartsWith("pv:");
        var city = Start(state, market);
        double end = state.Minute + 36 * 60;
        int reloads = 0;
        while (!city.IsCaughtUpTo(end))
        {
            if (frameSlices)
            {
                var slice = city.AdvanceFrameSlice(end, budget);
                while (slice.PlanningPending && !city.ContinuePlanning(double.PositiveInfinity)) { }
            }
            else city.AdvanceTo(end, budget);
            if (reloadWhen != null && reloadWhen(city) && reloads < 400) { city = Reload(city, market); reloads++; }
        }
        Assert.Equal(0, city.HistoricalClockMisses);
        city.SynchronizeSnapshot();
        return (SaveCodec.Write(city.State), string.Join(",", market.Orders));
    }

    [Fact]
    public void SameLogicalDayAtEveryBudgetCadenceAndReloadPoint()
    {
        var reference = Day(100000);
        Assert.Equal(reference, Day(1));
        Assert.Equal(reference, Day(7, frameSlices: true));
        Assert.Equal(reference, Day(64));
        // Reload with an in-flight meal outing, an uncertain receipt and a held decision, each owned by a visitor.
        Assert.Equal(reference, Day(3, c => VisitorMeals(c.State).Any(m => m.Status == MealStatus.Travelling || m.Status == MealStatus.Serving
            || m.Status == MealStatus.Uncertain || m.Decision != null)));
        Assert.Equal(reference, Day(16, c => c.State.Minute % 37 < 1));
    }

    /// <summary>The F1 accounting-backlog harness, persons variant (tools/accounting-backlog-regression): one visitor
    /// per resident, the same logical day at 30/60/120 FPS, 1/16/128 units, a stall, hidden skip and reload.</summary>
    [Fact]
    public void AccountingHarnessPersonsVariantIsEqualAcrossArms()
    {
        var reference = AccountingScenario.Run(24, 60, 128, 2, persons: true);
        Assert.Equal(24, reference.Visitors); Assert.True(reference.Conserved); Assert.True(reference.VisitorPurchases > 0);
        foreach (var arm in new[] { (30, 1, .01, false), (60, 16, .5, false), (120, 128, 2.0, false), (30, 1, .01, true) })
        {
            var result = AccountingScenario.Run(24, arm.Item1, arm.Item2, arm.Item3, stallFrames: arm.Item1 * 2, skip: arm.Item4, persons: true);
            Assert.Equal(reference.Snapshot, result.Snapshot); Assert.Equal(reference.Orders, result.Orders); Assert.True(result.Conserved);
        }
        var reloaded = AccountingScenario.Run(24, 30, 1, .01, reload: true, persons: true);
        Assert.Equal(reference.Snapshot, reloaded.Snapshot); Assert.Equal(2, reloaded.Reloads);
    }

    // ---- Rival settlement owned by a visitor, across reload ----

    private static (CitySimulator City, RivalOutflowTests.Market Market) RivalCity()
    {
        var (state, _) = Fresh(2);
        var food = state.Food!; food.Shops.Clear();
        var market = new RivalOutflowTests.Market();
        for (int i = 0; i < 3; i++)
        {
            string id = "rival" + i;
            market.Rivals.Add(id, new FoodScope { AlternativeKind = FoodAlternativeKind.Rival, BusinessId = id, PlaceId = "shop:" + i, ProductId = FoodProducts.Croissant });
            market.Observations.Add(id, RivalOutflowTests.Observation(id, "shop:" + i));
        }
        var city = new CitySimulator(state, new FreshCityTraceTests.Walk());
        city.AttachFoodMarket(market, rivalSettings: new RivalFoodSettings());
        food.ChoiceStartsDay = food.AwarenessStartsDay = food.RivalStartsDay = 1;
        return (city, market);
    }

    [Fact]
    public void VisitorRivalSettlementSettlesOnceAcrossEveryReload()
    {
        var (plain, _) = RivalCity();
        var (reloaded, market) = RivalCity();
        double end = plain.State.Minute + 30 * 60;
        Advance(plain, end);
        int reloads = 0;
        while (!reloaded.IsCaughtUpTo(end))
        {
            reloaded.AdvanceTo(end, 5);
            if (VisitorMeals(reloaded.State).Any(m => m.TargetKind == FoodAlternativeKind.Rival && m.Status != MealStatus.Pending) && reloads < 300)
            {
                var read = SaveCodec.Read(SaveCodec.Write(reloaded.State)); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
                reloaded = new CitySimulator(read.State!, new FreshCityTraceTests.Walk());
                reloaded.AttachFoodMarket(market, rivalSettings: new RivalFoodSettings()); reloads++;
            }
        }
        Assert.True(reloads > 0);
        var settled = VisitorMeals(reloaded.State).Where(m => m.Budget!.ExternalFoodSpent > Money.Zero).ToList();
        Assert.NotEmpty(settled);
        Assert.Equal(SaveCodec.Write(plain.State), SaveCodec.Write(reloaded.State));
        Assert.All(reloaded.State.Food!.Residents, m => Assert.True(HouseholdFinance.CashConserved(m.Budget!)));
        Assert.Equal(0, reloaded.FoodAnchorReads);
    }

    [Fact]
    public void StreamingAndSemanticRecordCountersAgree()
    {
        foreach (int factor in new[] { 1, 2, 3 })
        {
            var state = Running(factor, 30).State;
            var count = SaveCodec.CountRecords(state, SaveCodec.Write(state));
            Assert.Equal(count.Streaming, count.Semantic);
        }
    }

    private static CitySimulator WithTravellingVisitor()
    {
        var (state, market) = Fresh(2);
        var city = Start(state, market);
        for (int step = 0; step < 200000; step++)
        {
            city.AdvanceTo(state.Minute + 600, 1);
            if (state.VisitorRoutines.Any(r => r.Phase == CitizenPhase.Travelling && r.Journey != null)) return city;
        }
        throw new InvalidOperationException("No visitor travelled.");
    }

    /// <summary>Review fix Sol-r1-1: a travelling visitor's route legs are charged by both counters, so the write-side
    /// cap check sees every record the streaming reader charges.</summary>
    [Fact]
    public void TravellingVisitorRouteLegsAreChargedByBothCounters()
    {
        var city = WithTravellingVisitor();
        var state = city.State;
        int legs = state.VisitorRoutines.Where(r => r.Journey != null).Sum(r => r.Journey!.Legs.Count);
        Assert.True(legs > 0);
        var count = SaveCodec.CountRecords(state, SaveCodec.Write(state));
        Assert.Equal(count.Streaming, count.Semantic);
        Assert.True(count.Semantic >= legs + PersonTable.PersonCount(state));
    }

    /// <summary>Review fix Sol-r1-2: a visitor journey with a car leg is refused and preserved before any simulation.</summary>
    [Fact]
    public void VisitorJourneyWithACarLegIsRefusedAndPreserved()
    {
        var city = WithTravellingVisitor();
        var state = city.State;
        string payload = SaveCodec.Write(state);
        int index = state.VisitorRoutines.FindIndex(r => r.Phase == CitizenPhase.Travelling && r.Journey != null);
        string bad = Root(payload, r => r["VisitorRoutines"]![index]!["J"]!["Legs"]![0]!["Mode"] = (int)TravelMode.Car);
        Assert.NotEqual(payload, bad);
        var read = SaveCodec.Read(bad);
        Assert.Equal(SaveReadStatus.Invalid, read.Status); Assert.Null(read.State); Assert.Contains("car leg", read.Error);
        RefusedAndPreserved(bad, Running(2, 1).State, SaveReadStatus.Invalid, "car leg");
        // The writer refuses the same in-memory state before publication.
        state.VisitorRoutines[index].Journey!.Legs[0].Mode = TravelMode.Car;
        Assert.Contains("car leg", Assert.Throws<SaveValidationException>(() => SaveCodec.Write(state)).Message);
    }

    // ---- Conservation from city creation ----

    [Fact]
    public void LedgerConservesEveryHourFromCityCreation()
    {
        var (state, market) = Fresh(3);
        var city = Start(state, market);
        int persons = PersonTable.PersonCount(state);
        int employed = state.Citizens.Count(c => c.DayPlaceId.Length != 0) + state.VisitorRoutines.Count(r => r.DayPlaceId.Length != 0);
        long changes = state.Citizens.Sum(c => c.JobChanges) + state.VisitorRoutines.Sum(r => r.JobChanges);
        for (int hour = 0; hour <= 4 * 24; hour++)
        {
            var ledger = Ledger(state);
            Assert.Equal(ledger[0], ledger[1]);
            Assert.Equal(persons, PersonTable.PersonCount(state));
            int nowEmployed = state.Citizens.Count(c => c.DayPlaceId.Length != 0) + state.VisitorRoutines.Count(r => r.DayPlaceId.Length != 0);
            long nowChanges = state.Citizens.Sum(c => c.JobChanges) + state.VisitorRoutines.Sum(r => r.JobChanges);
            // Employment changes only through logged lifecycle events (each one a job change).
            Assert.True(Math.Abs(nowEmployed - employed) <= nowChanges - changes);
            Advance(city, state.Minute + 60);
            if (hour % 31 == 0) { city = Reload(city, market); state = city.State; }
        }
        Assert.All(state.Food!.Residents, m => Assert.True(m.Budget!.LastPostedDay >= 4));
    }

    // ---- Unreachable portal ----

    private sealed class PortalBlocked : IRoutePlanner
    {
        private readonly HashSet<string> portals;
        public int Refusals;
        public PortalBlocked(IEnumerable<string> portals) { this.portals = new HashSet<string>(portals, StringComparer.Ordinal); }
        public Route? Plan(Place from, Place to, double at, TravelPreferences preferences)
        {
            if (portals.Contains(from.Id) || portals.Contains(to.Id)) { Refusals++; return null; }
            return new FreshCityTraceTests.Walk().Plan(from, to, at, preferences);
        }
    }

    [Fact]
    public void UnreachablePortalWaitsAndRetriesWithoutEverArriving()
    {
        var (state, market) = Fresh();
        var routes = new PortalBlocked(Stations.Select(s => s.Id));
        var city = Start(state, market, routes);
        var start = state.Food!.Residents.Where(m => m.ResidentId.StartsWith("pv:")).ToDictionary(m => m.ResidentId, m => m.Budget!.IncomeReceived);
        Advance(city, state.Minute + 2 * 1440);
        Assert.True(routes.Refusals > state.Visitors.Count, "visitors retried their portal route");
        foreach (var routine in state.VisitorRoutines)
        {
            Assert.Equal(routine.HomeId, routine.PlaceId);
            Assert.Equal(0, routine.CompletedJourneys);
            Assert.True(VisitorSchedules.Dormant(routine));
        }
        Assert.All(state.Visitors, v => Assert.Null(v.LastArrival));
        foreach (var meal in VisitorMeals(state))
        {
            // No demand from off-map persons, but their wallets keep posting.
            Assert.Equal(0, meal.Purchases); Assert.Null(meal.Decision); Assert.Equal("", meal.PlaceId);
            Assert.True(meal.Budget!.IncomeReceived > start[meal.ResidentId]);
        }
        Assert.DoesNotContain(market.Orders, o => o.Contains("pv%3A") || o.Contains("pv:"));
        var reloaded = Reload(city, market, routes);
        Advance(reloaded, reloaded.State.Minute + 120);
    }

    // ---- No visitor car or traffic record ----

    [Fact]
    public void VisitorsOwnNoCarTrafficRecordOrBody()
    {
        var (state, market) = Fresh(3);
        var city = Start(state, market);
        bool leaseTried = false;
        for (int step = 0; step < 4000 && state.Minute < 1440 * 2 + 1200; step++)
        {
            city.AdvanceTo(state.Minute + 5, 64);
            foreach (var routine in state.VisitorRoutines)
            {
                Assert.Null(routine.Car); Assert.False(routine.Preferences.OwnsCar);
                Assert.True(routine.Journey == null || routine.Journey.Legs.All(l => l.Mode != TravelMode.Car));
                Assert.DoesNotContain(routine, city.ActiveJourneys.BodyCandidates);
                if (routine.Phase == CitizenPhase.Travelling)
                { Assert.False(city.TryAcquirePhysicalJourney(routine.Identity.Id, out _)); leaseTried = true; }
            }
            Assert.True(city.ActiveJourneyIndexMatchesState());
        }
        Assert.True(leaseTried);
        Assert.True(state.Traffic == null || state.Traffic.Vehicles.All(v => !v.VehicleId.StartsWith("pv:", StringComparison.Ordinal)));
        // A visitor with a car is refused before anything is written.
        state.VisitorRoutines[0].Car = new CarState { Id = state.VisitorRoutines[0].Identity.Id + ":car" };
        Assert.Contains("owns no car", Assert.Throws<SaveValidationException>(() => SaveCodec.Write(state)).Message);
    }

    // ---- Arrival evidence: residents unchanged, visitors by LastArrival ----

    [Fact]
    public void ResidentGuardEqualsTheLatestArrivalOnRecordedResidentTraces()
    {
        var (state, market) = Fresh(1);
        var city = Start(state, market);
        int checks = 0;
        double end = state.Minute + 2 * 1440;
        while (!city.IsCaughtUpTo(end))
        {
            city.AdvanceTo(end, 1);
            foreach (var meal in state.Food!.Residents.Where(m => m.Status == MealStatus.Travelling))
            {
                var person = state.Citizens.Single(c => c.Identity.Id == meal.ResidentId);
                if (person.MealOuting == null || person.MealOuting.Returning || person.Phase != CitizenPhase.AtPlace || person.PlaceId != meal.PlaceId) continue;
                bool resident = person.RecentVisits.Any(v => v.PlaceId == meal.PlaceId && v.Activity == Activity.Shop && v.AtMinute >= meal.DesiredAt);
                var last = person.RecentVisits[^1];
                bool latest = last.PlaceId == meal.PlaceId && last.Activity == Activity.Shop && last.AtMinute >= meal.DesiredAt;
                Assert.Equal(resident, latest); checks++;
            }
        }
        Assert.True(checks > 0);
    }

    private static (CitySimulator City, Market Market, ResidentMeal Meal, VisitorPerson Visitor) AtShopDoor(bool reloadOnTheWay)
    {
        var (state, market) = Fresh();
        var city = Start(state, market);
        bool reloaded = false;
        for (int step = 0; step < 200000; step++)
        {
            city.AdvanceTo(state.Minute + 600, 1);
            foreach (var meal in VisitorMeals(state).Where(m => m.Status == MealStatus.Travelling))
            {
                var routine = state.VisitorRoutines.Single(r => r.Identity.Id == meal.ResidentId);
                if (reloadOnTheWay && !reloaded && routine.Phase == CitizenPhase.Travelling)
                { city = Reload(city, market); state = city.State; reloaded = true; break; }
                if (routine.Phase == CitizenPhase.AtPlace && routine.PlaceId == meal.PlaceId && routine.MealOuting != null)
                    return (city, market, meal, routine.Person!);
            }
        }
        throw new InvalidOperationException("No visitor reached a shop door.");
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)] [InlineData(false, true)] [InlineData(true, true)]
    public void VisitorIsAdmittedThroughItsLastArrivalAcrossReload(bool reloadOnTheWay, bool reloadAtTheDoor)
    {
        var (city, market, meal, visitor) = AtShopDoor(reloadOnTheWay);
        Assert.NotNull(visitor.LastArrival);
        Assert.Equal((meal.PlaceId, Activity.Shop), (visitor.LastArrival!.PlaceId, visitor.LastArrival.Activity));
        string intent = meal.IntentId, id = meal.ResidentId;
        if (reloadAtTheDoor) city = Reload(city, market);
        Advance(city, city.State.Minute + 120);
        var after = city.State.Food!.Residents.Single(m => m.ResidentId == id);
        Assert.Contains(intent, market.Orders);
        Assert.Single(market.Orders, o => o == intent);
        Assert.True(after.Purchases >= 1);
    }

    [Theory]
    [InlineData("place")] [InlineData("activity")] [InlineData("minute")]
    public void VisitorWithoutMatchingArrivalIsRefusedServiceNeverFabricated(string mismatch)
    {
        var (city, market, meal, visitor) = AtShopDoor(false);
        var routine = visitor.Routine!;
        var arrival = routine.RecentVisits[0];
        var tampered = mismatch == "place" ? new VisitRecord { AtMinute = arrival.AtMinute, PlaceId = city.State.Places.First(p => p.Kind == PlaceKind.Shop && p.Id != meal.PlaceId).Id, Activity = Activity.Shop }
            : mismatch == "activity" ? new VisitRecord { AtMinute = arrival.AtMinute, PlaceId = routine.DayPlaceId.Length != 0 ? routine.DayPlaceId : routine.HomeId, Activity = routine.DayPlaceId.Length != 0 ? (routine.Identity.Persona == Persona.Student ? Activity.Study : Activity.Work) : Activity.Home }
            : new VisitRecord { AtMinute = meal.DesiredAt - 1, PlaceId = meal.PlaceId, Activity = Activity.Shop };
        routine.RecentVisits[0] = tampered; VisitorSchedules.Sync(visitor);
        Assert.False(VisitorSchedules.ArrivedForMeal(visitor, meal.PlaceId, meal.DesiredAt));
        string intent = meal.IntentId;
        Advance(city, city.State.Minute + 120);
        Assert.DoesNotContain(intent, market.Orders);
        Assert.NotEqual(MealStatus.Paid, city.State.Food!.Residents.Single(m => m.ResidentId == visitor.Id).Day == meal.Day
            && city.State.Food.Residents.Single(m => m.ResidentId == visitor.Id).Kind == meal.Kind
            ? city.State.Food.Residents.Single(m => m.ResidentId == visitor.Id).Status : MealStatus.Missed);
    }

    // ---- Income continuity ----

    [Fact]
    public void IncomeSourceIsPerPersonCarriedForUnseatedWorkersAndContinuousAcrossReload()
    {
        var (state, market) = Fresh(2);
        // Leave no workplace vacancy for visitors, so working visitors keep an outside employer (carried income).
        foreach (var place in state.Places.Where(p => p.Kind == PlaceKind.Work || p.Kind == PlaceKind.Study))
            place.Capacity = state.Citizens.Count(c => c.DayPlaceId == place.Id);
        var city = Start(state, market);
        Assert.All(state.VisitorRoutines, r => Assert.Equal("", r.DayPlaceId));
        foreach (var routine in state.VisitorRoutines)
        {
            var expected = routine.Identity.Persona == Persona.Retiree ? ResidentIncomeSource.ExternalPension
                : ResidentLifecycle.IsWorker(routine) && routine.Identity.Age >= 18 ? ResidentIncomeSource.ExternalEmployment : ResidentIncomeSource.ExternalSupport;
            Assert.Equal(expected, state.Food!.Residents.Single(m => m.ResidentId == routine.Identity.Id).Budget!.IncomeSource);
            Assert.Equal(expected == ResidentIncomeSource.ExternalEmployment ? expected : (ResidentIncomeSource?)null, routine.Person!.CarriedIncomeSource);
        }
        Assert.Contains(state.Visitors, v => v.CarriedIncomeSource == ResidentIncomeSource.ExternalEmployment);
        var continuous = Start(Fresh(2).State.Also(s => { foreach (var p in s.Places.Where(p => p.Kind == PlaceKind.Work || p.Kind == PlaceKind.Study)) p.Capacity = s.Citizens.Count(c => c.DayPlaceId == p.Id); }), new Market { Shops = market.Shops });
        for (int day = 0; day < 3; day++)
        {
            Advance(city, state.Minute + 1440); Advance(continuous, continuous.State.Minute + 1440);
            var before = state.Food!.Residents.ToDictionary(m => m.ResidentId, m => (m.Budget!.IncomeSource, m.Budget.DailyIncomeCents, m.Budget.IncomeReceived));
            city = Reload(city, market); state = city.State;
            Assert.Equal(before, state.Food!.Residents.ToDictionary(m => m.ResidentId, m => (m.Budget!.IncomeSource, m.Budget.DailyIncomeCents, m.Budget.IncomeReceived)));
            Assert.Equal(continuous.State.Food!.Residents.Select(m => (m.ResidentId, m.Budget!.IncomeReceived)), state.Food.Residents.Select(m => (m.ResidentId, m.Budget!.IncomeReceived)));
        }
    }

    // ---- Departure deferral and dormancy ----

    [Fact]
    public void DepartureWaitsForAnInProgressMealAndNoDormantVisitorHasActiveDemand()
    {
        var (city, market, meal, visitor) = AtShopDoor(false);
        var routine = visitor.Routine!;
        // The visitor's stay now ends while its meal is in progress: the original activity is over.
        routine.MealOuting!.ResumeEvent = routine.MealOuting.ResumeUntil = city.State.Minute + 1;
        string intent = meal.IntentId;
        for (int step = 0; step < 20000 && !VisitorSchedules.Dormant(routine); step++)
        {
            city.AdvanceTo(city.State.Minute + 600, 1);
            foreach (var r in city.State.VisitorRoutines.Where(VisitorSchedules.Dormant))
                Assert.DoesNotContain(VisitorMeals(city.State), m => m.ResidentId == r.Identity.Id && (m.Status == MealStatus.Travelling || m.Status == MealStatus.Serving || m.Decision != null));
        }
        Assert.True(VisitorSchedules.Dormant(routine));
        Assert.Equal(routine.HomeId, routine.PlaceId);
        Assert.Contains(intent, market.Orders); // the meal finished before the visitor left
        Assert.Null(visitor.LastArrival);
        Assert.Equal(VisitorPresence.Dormant, visitor.Stay.Presence);
    }

    [Fact]
    public void DormantVisitorKeepsItsUncertainReceiptAndHeldMoneyUntilEvidenceSettlesIt()
    {
        var (state, market) = Fresh();
        market.EvidenceAvailable = false;
        string? lost = null;
        market.LoseAcknowledgement = m => { if (lost == null && m.ResidentId.StartsWith("pv:")) { lost = m.IntentId; return true; } return false; };
        var city = Start(state, market);
        ResidentMeal? meal = null;
        for (int step = 0; step < 400000 && meal == null; step++)
        {
            city.AdvanceTo(state.Minute + 600, 4);
            meal = VisitorMeals(state).FirstOrDefault(m => m.IntentId == lost && m.Status == MealStatus.Uncertain);
        }
        Assert.NotNull(meal);
        var routine = state.VisitorRoutines.Single(r => r.Identity.Id == meal!.ResidentId);
        var held = meal!.Budget!.HeldPurchase; Assert.True(held > Money.Zero);
        while (!VisitorSchedules.Dormant(routine)) city.AdvanceTo(state.Minute + 600, 4);
        city = Reload(city, market); state = city.State;
        meal = state.Food!.Residents.Single(m => m.ResidentId == routine.Identity.Id);
        routine = state.VisitorRoutines.Single(r => r.Identity.Id == meal.ResidentId);
        Assert.Equal(MealStatus.Uncertain, meal.Status); Assert.Equal(held, meal.Budget!.HeldPurchase);
        int orders = market.Orders.Count(o => o.Contains(routine.Identity.Id));
        Advance(city, state.Minute + 12 * 60);
        Assert.Equal(MealStatus.Uncertain, meal.Status); Assert.Equal(held, meal.Budget.HeldPurchase);
        market.EvidenceAvailable = true;
        Advance(city, state.Minute + 2 * 1440);
        Assert.Equal(Money.Zero, meal.Budget.HeldPurchase);
        Assert.Single(market.Orders, o => o == lost);
        Assert.True(HouseholdFinance.CashConserved(meal.Budget));
        Assert.True(market.Orders.Count(o => o.Contains(routine.Identity.Id)) >= orders);
    }

    // ---- Persons created later are enrolled (AppendResidents gap) ----

    [Fact]
    public void ResidentsAppendedAfterFoodInitializationGetMealsAndWallets()
    {
        var city = Running(2, 6);
        var state = city.State;
        int before = state.Citizens.Count;
        var home = state.Places.First(p => p.Kind == PlaceKind.Home && !PortalPlaces.Is(p));
        home.Capacity += 4;
        Population.ExpandToHousing(state);
        Assert.True(state.Citizens.Count > before);
        var market = Fresh().Market;
        var rebuilt = city.RebuildRoutes(new FreshCityTraceTests.Walk());
        Assert.Equal(PersonTable.PersonCount(state), state.Food!.Residents.Count);
        foreach (var person in state.Citizens.Skip(before))
        {
            var meal = state.Food.Residents.Single(m => m.ResidentId == person.Identity.Id);
            Assert.NotNull(meal.Budget); Assert.True(HouseholdFinance.CashConserved(meal.Budget!));
        }
        var read = SaveCodec.Read(SaveCodec.Write(state)); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Advance(rebuilt, state.Minute + 120);
    }

    // ---- Older development cities are refused and kept ----

    [Fact]
    public void Writer26CityIsRefusedWithTheStartANewCityStatusAndKept()
    {
        var (state, _) = FreshCityTraceTests.Fresh(2, true, schema: DevelopmentChain.Represented);
        var simulator = new CitySimulator(state, new FreshCityTraceTests.Walk()); simulator.AttachFoodMarket(new WeightedFoodChoiceTests.Market());
        Advance(simulator, state.Minute + 300);
        string payload = DevelopmentChain.Write(state);
        Assert.Contains("\"Schema\":26,", payload);
        var read = SaveCodec.Read(payload);
        Assert.Equal(SaveReadStatus.DevelopmentSchema, read.Status); Assert.Equal(26, read.SourceSchema);
        Assert.Contains("start a new city", read.Error);
        RefusedAndPreserved(payload, Running(2, 1).State, SaveReadStatus.DevelopmentSchema, "start a new city");
        Assert.Equal(26, SaveCodec.DevelopmentSchemaOf(payload));
        DevelopmentChain.RequireRefusedCapabilities(payload);
        // The frozen writer-26 reader still decodes it, so the refusal is the schema, not damage.
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.ReadWriter26(payload).Status);
        var (oneFactor, oneMarket) = Fresh(1, portals: false); Start(oneFactor, oneMarket);
        string older = DevelopmentChain.Write25(oneFactor);
        RefusedAndPreserved(older, Running(2, 1).State, SaveReadStatus.DevelopmentSchema, "start a new city");
    }

    // ---- Strict writer-27 reader ----

    private static string Root(string payload, Action<JObject> change)
    {
        var root = JObject.Parse(payload); change(root); return root.ToString(Formatting.None);
    }

    private static string Tuple(string payload, int index, Func<string[], string> change)
        => Root(payload, r => { var t = r["VisitorRoutines"]![index]!["T"]!; r["VisitorRoutines"]![index]!["T"] = change(((string)t!).Split('|')); });

    [Fact]
    public void MalformedOrInconsistentPersonsAreRefusedAndPreserved()
    {
        // 08:00 on day 2: early workers are on their way, everyone else is still off-map at a portal.
        var state = Running(2, 26).State;
        string payload = SaveCodec.Write(state);
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(payload).Status);
        var replacement = Running(2, 1).State;
        int checks = 0;
        void Invalid(string bad, string? error = null)
        {
            checks++;
            var read = SaveCodec.Read(bad);
            Assert.True(read.Status == SaveReadStatus.Invalid, "check " + checks + ": " + read.Status + " " + read.Error);
            RefusedAndPreserved(bad, replacement, SaveReadStatus.Invalid, error);
        }
        int dormant = state.Visitors.FindIndex(v => v.Stay.Presence == VisitorPresence.Dormant);
        int active = state.Visitors.FindIndex(v => v.Stay.Presence == VisitorPresence.Active);
        Assert.True(dormant >= 0);
        Invalid(Root(payload, r => ((JArray)r["VisitorRoutines"]!).RemoveAt(0)), "exactly one routine");
        Invalid(Root(payload, r => r["VisitorRoutines"]![0]!["X"] = 1), "Unknown field");
        Invalid(Root(payload, r => ((JObject)r["VisitorRoutines"]![0]!).Remove("J")), "Missing object fields");
        Invalid(Tuple(payload, 0, p => string.Join("|", p.Take(15))), "arity");
        Invalid(Tuple(payload, 0, p => { p[0] = "2"; return string.Join("|", p); }), "version");
        Invalid(Tuple(payload, 0, p => { p[2] = "ZZZ"; return string.Join("|", p); }), "visitor place");
        Invalid(Tuple(payload, 0, p => { p[4] = p[4] + ".0"; return string.Join("|", p); }), "visitor next event");
        Invalid(Tuple(payload, 0, p => { p[1] = "9" + p[1].Substring(1); return string.Join("|", p); }), "visitor phase");
        Invalid(Tuple(payload, 0, p => { p[7] = "01"; return string.Join("|", p); }), "visitor journeys");
        // Presence must follow the routine; a dormant visitor holds no arrival.
        Invalid(Root(payload, r => { var parts = ((string)r["Visitors"]![dormant]!).Split('|'); parts[2] = parts[2].Substring(0, 4) + "A" + parts[2].Substring(5); r["Visitors"]![dormant] = string.Join("|", parts); }));
        if (active >= 0)
            Invalid(Root(payload, r => { var parts = ((string)r["Visitors"]![active]!).Split('|'); parts[2] = parts[2].Substring(0, 4) + "D" + parts[2].Substring(5); r["Visitors"]![active] = string.Join("|", parts); }));
        // Every visitor arrives through a portal; schema 27 has no represented consumers.
        Invalid(Root(payload, r => { var parts = ((string)r["Visitors"]![0]!).Split('|'); parts[2] = "---" + parts[2].Substring(3); r["Visitors"]![0] = string.Join("|", parts); }), "portal");
        Invalid(Root(payload, r => { var parts = ((string)r["Visitors"]![0]!).Split('|'); parts[1] = "consumer:c0:1"; r["Visitors"]![0] = string.Join("|", parts); }));
        Invalid(Root(payload, r => r["NextVisitorSequence"] = (int)r["NextVisitorSequence"]! + 1), "Visitor creation");
        Invalid(Root(payload, r => ((JArray)r["Portals"]!).Add("subway:ghost")));
        // A carried source is only for a visitor without its own Employment.
        int employed = state.Visitors.FindIndex(v => v.Employment != null);
        if (employed >= 0)
            Invalid(Root(payload, r => { var parts = ((string)r["Visitors"]![employed]!).Split('|'); parts[4] = "0"; r["Visitors"]![employed] = string.Join("|", parts); }), "carried");
        // Future and older schemas.
        RefusedAndPreserved(Root(payload, r => r["Schema"] = CityState.CurrentSchema + 1), replacement, SaveReadStatus.Future);
        RefusedAndPreserved(Root(payload, r => r["Schema"] = 27), replacement, SaveReadStatus.DevelopmentSchema, "start a new city");
        RefusedAndPreserved(Root(payload, r => r["Schema"] = 26), replacement, SaveReadStatus.DevelopmentSchema, "start a new city");
    }
}

internal static class StateTestExtensions
{
    public static T Also<T>(this T value, Action<T> change) { change(value); return value; }
}
