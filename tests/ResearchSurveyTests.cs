using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

/// <summary>C5b: the research survey runs in bounded units inside the F1 hold, at the order's drain point,
/// and never changes the city.</summary>
public sealed class ResearchSurveyTests
{
    private readonly ITestOutputHelper output;
    public ResearchSurveyTests(ITestOutputHelper output) { this.output = output; }

    private const string Agency = "address:Agency%20Street:1";
    // Day 1, 12:00. Shops are open, residents are at work, school or home.
    private const double Noon = 1440 + 720;

    private static Route Walk(Place from, Place to) => new()
    { Legs = { new RouteLeg { From = from.Position, To = to.Position, Mode = TravelMode.Walk,
        Minutes = Math.Max(1, Position.Distance(from.Position, to.Position) / 80) } } };

    private sealed class Walking : IRoutePlanner
    {
        public Route? Plan(Place from, Place to, double at, TravelPreferences preferences) => Walk(from, to);
    }

    // Each route answer arrives after three frame slices, as the native planner's jobs do.
    private sealed class Deferred : IResumableRoutePlanner
    {
        public readonly List<RouteRequest> Seen = new();
        public Route? Plan(Place from, Place to, double at, TravelPreferences preferences) => Walk(from, to);
        public IRoutePlanningJob Begin(RouteRequest request) { Seen.Add(request); return new Job(request); }
        private sealed class Job : IRoutePlanningJob
        {
            private readonly RouteRequest request; private int left = 3;
            public Job(RouteRequest request) { this.request = request; }
            public Route? Result { get; private set; }
            public bool Continue(double budgetMilliseconds) { if (--left > 0) return false; Result = Walk(request.From, request.To); return true; }
            public void Dispose() { }
        }
    }

    private sealed class Market : IFoodMarket, IFoodSaleEvidence
    {
        private static readonly (string Place, string Business, string[] Products)[] Shops =
        {
            ("shop-1", "b1", new[] { FoodProducts.Croissant, FoodProducts.Burger }),
            ("shop-2", "b2", new[] { FoodProducts.HotDog }),
            ("shop-3", "b3", new[] { FoodProducts.Burger, FoodProducts.HotDog, FoodProducts.Croissant }),
        };
        public readonly List<string> Orders = new();
        public int Reads;
        public double EarliestMinute => 0;
        public bool IsConfirmedPaid(ResidentMeal meal) => Orders.Contains(meal.IntentId);
        public IReadOnlyList<FoodBusiness> Read(double minute)
        {
            Reads++;
            return Shops.Select(s => new FoodBusiness
            {
                Id = s.Business, PlaceId = s.Place, Name = s.Place, MealsPerHour = 20,
                Hours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 360, ToMinute = 1320 }).ToList(),
                Offers = s.Products.Select(p => new FoodOffer { ProductId = p, Price = Money.FromCents(450), Units = 10000 }).ToList()
            }).ToList();
        }
        public FoodSale Buy(ResidentMeal meal, double minute) { Orders.Add(meal.IntentId); return new FoodSale(true, "Native fake confirmed"); }
    }

    private sealed class Run
    {
        public CitySimulator City = null!;
        public Market Market = new();
        public IRoutePlanner Routes = new Walking();
        public ResearchSurveyRunner Research => City.Research!;
        public readonly List<string> Trace = new();
    }

    private static Run Start(int residents = 60, int seed = 23, bool finances = true, IRoutePlanner? routes = null, int unemployedEvery = 0)
    {
        var state = EconomyScenario.Create(residents, seed);
        if (unemployedEvery > 0) for (int i = 0; i < state.Citizens.Count; i += unemployedEvery) state.Citizens[i].DayPlaceId = "";
        state.Food = EconomyScenario.Food();
        var run = new Run();
        if (routes != null) run.Routes = routes;
        run.City = new CitySimulator(state, run.Routes);
        run.City.AttachFoodMarket(run.Market);
        if (finances) run.City.EnableHouseholdFinances();
        Observe(run);
        return run;
    }

    private static void Observe(Run run)
    {
        var city = run.City;
        city.ActivityObserved += (id, _, minute) => run.Trace.Add(id + "@" + minute.ToString("R", CultureInfo.InvariantCulture));
    }

    private static void Reload(Run run, ResearchLedger? ledger, int unit = 256, IRoutePlanner? routes = null)
    {
        var read = SaveCodec.Read(SaveCodec.Write(run.City.State));
        Assert.Equal(SaveReadStatus.Loaded, read.Status);
        Assert.Equal(CityState.CurrentSchema, read.State!.Schema);
        if (routes != null) run.Routes = routes;
        run.City = new CitySimulator(read.State!, run.Routes);
        run.City.AttachFoodMarket(run.Market);
        if (ledger != null) run.City.AttachResearch(Clone(ledger), unit);
        Observe(run);
    }

    // The in-memory clone of the ledger the seam receives until C5c saves it in CityState.
    private static ResearchLedger Clone(ResearchLedger ledger)
    {
        Assert.True(ResearchLedger.TryRestore(ledger.NextSequence, ledger.Open.Select(ResearchOrderImage.Of).ToList(),
            ledger.Kept.Select(ResearchOrderImage.Of).ToList(), ledger.Closed.Select(ResearchOrderImage.Of).ToList(), out var clone, out var error), error);
        return clone!;
    }

    private static void Catch(CitySimulator city, double target, int budget = 10000)
    {
        for (int guard = 0; !city.AdvanceTo(target, budget).CaughtUp; guard++) Assert.True(guard < 1_000_000);
    }

    // Native frames: one bounded unit per slice; a pending route job gets one Continue per frame.
    private static int Frames(CitySimulator city, double target, int budget = 1, Action<AdvanceResult>? each = null)
    {
        int frames = 0;
        while (true)
        {
            Assert.True(++frames < 5_000_000, "frame loop did not finish");
            if (city.PlanningPending) { city.ContinuePlanning(0); continue; }
            var result = city.AdvanceFrameSlice(target, budget);
            each?.Invoke(result);
            if (result.CaughtUp) return frames;
        }
    }

    private static ResearchOrder Order(Run run, string product = ResearchProducts.DetailedCityCode, string business = "shop-1", double? minute = null)
    {
        var refusal = run.Research.TryOrder(product, business, Agency, false, 500, minute ?? run.City.State.Minute, out var order, out var ledger);
        Assert.Equal(ResearchSurveyRefusal.None, refusal); Assert.Equal(ResearchOrderRefusal.None, ledger);
        return order!;
    }

    private static int?[] Cells(ResearchOrder order)
    {
        Assert.Equal(ResearchOrderState.Surveyed, order.State);
        return order.Report!.FixtureCells.Select(c => c.Value).ToArray();
    }
    private static int? Cell(ResearchOrder order, MarketResearchMetric metric, MarketResearchBand band = MarketResearchBand.All)
        => order.Report!.FixtureCells.Single(c => c.Metric == metric && c.Band == band).Value;

    // One synchronous survey of the current state: Array.Sort index, one engine call. The reference for
    // the bounded runner.
    private static MarketResearchReport Reference(CitySimulator city, ResearchOrder order)
    {
        var roster = city.State.Food!.Residents;
        var index = Enumerable.Range(0, roster.Count).ToArray();
        Array.Sort(index, (a, b) => string.CompareOrdinal(roster[a].ResidentId, roster[b].ResidentId));
        var snapshot = new CityResearchSnapshot(city, order, roster, index, roster.Count);
        var scope = order.Product.Coverage == ResearchCoverage.City ? new MarketResearchScope()
            : new MarketResearchScope(order.BusinessAddress, city.FindPosition(order.BusinessAddress)!.Value, order.RadiusMetres);
        var engine = new MarketResearchEngine(snapshot, scope, order.Product.Tier);
        while (engine.Completed == null) engine.Continue(snapshot, 1024);
        return engine.Completed;
    }

    [Fact]
    public void HoldKeepsNativeTimeUntilTheSurveySealsThenEventsRunInTheirOldOrder()
    {
        var plain = Start(); var held = Start();
        Catch(plain.City, Noon); Catch(held.City, Noon);
        held.City.AttachResearch(new ResearchLedger(), 64);
        Assert.True(held.City.IsCaughtUpTo(Noon));
        bool drained = held.City.IsAccountingEpochDrained(Noon);
        plain.Trace.Clear(); held.Trace.Clear();
        var order = Order(held);
        Assert.Equal(Noon, order.SurveyAtMinute);
        Assert.False(held.City.IsCaughtUpTo(Noon)); Assert.False(held.City.IsCaughtUpTo(Noon + 120));
        // Research holds native time but is not unfinished previous-epoch work.
        Assert.Equal(drained, held.City.IsAccountingEpochDrained(Noon));
        int units = 0;
        double target = Noon + 120;
        while (order.State == ResearchOrderState.Ordered)
        {
            var result = held.City.AdvanceFrameSlice(target, 1);
            Assert.Equal(1, result.ResearchUnits); Assert.Equal(1, result.Events); Assert.Equal(0, result.CitizenEvents);
            Assert.False(result.CaughtUp);
            Assert.Equal(Noon, held.City.State.Minute);
            if (order.State == ResearchOrderState.Ordered) Assert.False(held.City.IsCaughtUpTo(Noon));
            Assert.Empty(held.Trace);
            units++;
        }
        Assert.True(held.Research.IndexUnits > 0); Assert.True(held.Research.SurveyUnits > 1);
        Assert.Equal(units, held.Research.IndexUnits + held.Research.SurveyUnits);
        Assert.True(held.City.IsCaughtUpTo(Noon));
        Frames(held.City, target); Frames(plain.City, target);
        Assert.NotEmpty(plain.Trace);
        Assert.Equal(plain.Trace, held.Trace);
        Assert.Equal(SaveCodec.Write(plain.City.State), SaveCodec.Write(held.City.State));
        Assert.Equal(plain.Market.Orders, held.Market.Orders);
    }

    private static int?[] Values(MarketResearchReport report) => report.FixtureCells.Select(c => c.Value).ToArray();

    [Theory]
    [InlineData(16, 1)] [InlineData(256, 1)] [InlineData(1024, 1)] [InlineData(16, 10000)] [InlineData(1024, 7)]
    public void UnitSizeAndFrameBudgetGiveTheSameCells(int unit, int budget)
    {
        int?[] Survey(int u, int b, out string id)
        {
            var run = Start(); Catch(run.City, Noon);
            run.City.AttachResearch(new ResearchLedger(), u);
            var order = Order(run, ResearchProducts.DetailedLocalCode, "shop-1");
            Frames(run.City, Noon, b);
            id = order.ReportId;
            Assert.Equal(Values(Reference(run.City, order)), Cells(order));
            return Cells(order);
        }
        var expected = Survey(256, 1, out var expectedId);
        Assert.Equal(expected, Survey(unit, budget, out var id));
        Assert.Equal(expectedId, id);
        Assert.Contains(expected, v => v > 0);
    }

    [Theory]
    [InlineData(1)] [InlineData(4)] [InlineData(7)]
    public void ReloadMidSurveyRestartsFromZeroAndGivesTheSameCells(int unitsBeforeSave)
    {
        var plain = Start(); Catch(plain.City, Noon);
        var ledger = new ResearchLedger();
        plain.City.AttachResearch(ledger, 16);
        var order = Order(plain, ResearchProducts.BasicLocalCode, "shop-2");
        var reloaded = Start(); Catch(reloaded.City, Noon);
        var reloadLedger = new ResearchLedger();
        reloaded.City.AttachResearch(reloadLedger, 16);
        Order(reloaded, ResearchProducts.BasicLocalCode, "shop-2");
        for (int i = 0; i < unitsBeforeSave; i++) Assert.Equal(1, reloaded.City.AdvanceFrameSlice(Noon + 60, 1).ResearchUnits);
        Assert.Equal(ResearchOrderState.Ordered, reloadLedger.Open.Single().State);
        // The save writes the order as Ordered; no cursor exists anywhere in the payload.
        Reload(reloaded, reloadLedger, 16);
        Assert.Equal(0, reloaded.Research.IndexUnits + reloaded.Research.SurveyUnits);
        Assert.False(reloaded.City.IsCaughtUpTo(Noon));
        Frames(plain.City, Noon + 60); Frames(reloaded.City, Noon + 60);
        var restored = reloaded.Research.Ledger.Open.Single();
        Assert.Equal(Cells(order), Cells(restored));
        Assert.Equal(order.ReportId, restored.ReportId);
        Assert.Equal(plain.Trace, reloaded.Trace);
        Assert.Equal(SaveCodec.Write(plain.City.State), SaveCodec.Write(reloaded.City.State));
    }

    [Fact]
    public void RouteRebuildMidSurveyRestartsOnTheReplacementWithTheSameCells()
    {
        var plain = Start(); Catch(plain.City, Noon);
        plain.City.AttachResearch(new ResearchLedger(), 16);
        var order = Order(plain);
        Frames(plain.City, Noon);
        var rebuilt = Start(); Catch(rebuilt.City, Noon);
        var ledger = new ResearchLedger();
        rebuilt.City.AttachResearch(ledger, 16);
        Order(rebuilt);
        for (int i = 0; i < 4; i++) Assert.Equal(1, rebuilt.City.AdvanceFrameSlice(Noon, 1).ResearchUnits);
        Assert.Equal(ResearchOrderState.Ordered, ledger.Open.Single().State);
        var replacement = rebuilt.City.RebuildRoutes(new Walking());
        Assert.Same(ledger, replacement.Research!.Ledger); Assert.Equal(16, replacement.Research.ConsumersPerUnit);
        Assert.False(replacement.IsCaughtUpTo(Noon));
        Frames(replacement, Noon);
        Assert.Equal(Cells(order), Cells(ledger.Open.Single()));
    }

    // An employment check whose workplace probe is still waiting for its route answer.
    private static (Run Run, string Resident) AtPendingProbe(Deferred planner)
    {
        var run = Start(routes: planner, unemployedEvery: 3);
        for (int guard = 0; ; guard++)
        {
            Assert.True(guard < 2_000_000, "no employment probe was reached");
            if (run.City.PlanningPending)
            {
                var request = planner.Seen[planner.Seen.Count - 1];
                // A probe is a detached resident with a cloned identity; event working copies share the live one.
                if (request.Citizen != null && !run.City.State.Citizens.Any(c => ReferenceEquals(c.Identity, request.Citizen.Identity))
                    && run.City.State.Citizens.Single(c => c.Identity.Id == request.Citizen.Identity.Id).DayPlaceId.Length == 0
                    && run.City.State.Minute >= Noon)
                    return (run, request.Citizen.Identity.Id);
                run.City.ContinuePlanning(0);
                continue;
            }
            run.City.AdvanceFrameSlice(4 * 1440, 1);
        }
    }

    [Fact]
    public void OrderWhileARouteProbeIsPendingSurveysTheCommittedDayPlaceUninterruptedAndAfterReload()
    {
        var (plain, resident) = AtPendingProbe(new Deferred());
        double minute = plain.City.State.Minute;
        var ledger = new ResearchLedger();
        plain.City.AttachResearch(ledger, 64);
        var order = Order(plain, ResearchProducts.DetailedCityCode, "shop-1", minute);
        Assert.True(plain.City.PlanningPending);
        Assert.False(plain.City.IsCaughtUpTo(minute));
        // The survey does not start before the probe commits.
        plain.City.ContinuePlanning(0);
        Assert.Equal(0, plain.Research.IndexUnits + plain.Research.SurveyUnits);
        Frames(plain.City, minute);
        var person = plain.City.State.Citizens.Single(c => c.Identity.Id == resident);
        Assert.NotEqual("", person.DayPlaceId);
        var cells = Cells(order);
        Assert.Equal(Values(Reference(plain.City, order)), cells);
        // Read with the uncommitted (empty) day place the resident would count in another day group.
        string committed = person.DayPlaceId; person.DayPlaceId = "";
        Assert.NotEqual(cells, Values(Reference(plain.City, order)));
        person.DayPlaceId = committed;

        // Same point in a second run; save and reload before the probe commits.
        var (reloaded, again) = AtPendingProbe(new Deferred());
        Assert.Equal(resident, again); Assert.Equal(minute, reloaded.City.State.Minute);
        var reloadLedger = new ResearchLedger();
        reloaded.City.AttachResearch(reloadLedger, 64);
        Order(reloaded, ResearchProducts.DetailedCityCode, "shop-1", minute);
        Assert.Equal("", reloaded.City.State.Citizens.Single(c => c.Identity.Id == resident).DayPlaceId);
        Reload(reloaded, reloadLedger, 64, new Deferred());
        Assert.False(reloaded.City.PlanningPending);
        Frames(reloaded.City, minute);
        Assert.Equal(committed, reloaded.City.State.Citizens.Single(c => c.Identity.Id == resident).DayPlaceId);
        Assert.Equal(cells, Cells(reloaded.Research.Ledger.Open.Single()));
        Assert.Equal(SaveCodec.Write(plain.City.State), SaveCodec.Write(reloaded.City.State));
    }

    [Fact]
    public void TwoOrdersInTheSameMinuteGetDifferentIdentities()
    {
        var run = Start(); Catch(run.City, Noon);
        run.City.AttachResearch(new ResearchLedger());
        var first = Order(run, ResearchProducts.DetailedCityCode, "shop-1");
        var second = Order(run, ResearchProducts.DetailedCityCode, "shop-2");
        Assert.Equal(first.SurveyAtMinute, second.SurveyAtMinute);
        Frames(run.City, Noon);
        Assert.Equal(2, run.Research.Completed);
        Assert.NotEqual(first.ReportId, second.ReportId);
        Assert.Equal(Cells(first), Cells(second));
    }

    [Fact]
    public void MissingInputsStayUnknown()
    {
        var run = Start(finances: false);
        run.City.State.Food!.MealRhythmVersion = 0;
        var roster = run.City.State.Food.Residents;
        var anchor = run.City.State.Citizens.First(c => c.Identity.Persona == Persona.Commuter);
        anchor.Traits = null;
        roster.First(m => m.Slot > 0 && m.AnchorId != anchor.Identity.Id).RepresentedTraits = null;
        var lost = run.City.State.Citizens.First(c => c.Identity.Persona == Persona.Foodie && c.DayPlaceId.Length > 0);
        lost.DayPlaceId = "demolished-workplace";
        run.City.AttachResearch(new ResearchLedger());
        var order = Order(run, ResearchProducts.DetailedCityCode);
        Frames(run.City, run.City.State.Minute);
        int consumers = roster.Count;
        Assert.Equal(consumers, Cell(order, MarketResearchMetric.Consumers));
        Assert.Equal(roster.Count(m => m.Slot > 0), Cell(order, MarketResearchMetric.RepresentedConsumers));
        Assert.Null(Cell(order, MarketResearchMetric.BreakfastOpportunities));
        Assert.Null(Cell(order, MarketResearchMetric.LunchOpportunities));
        Assert.Null(Cell(order, MarketResearchMetric.DinnerOpportunities));
        Assert.Equal(consumers, Cell(order, MarketResearchMetric.HouseholdFood, MarketResearchBand.Unknown));
        Assert.Equal(0, Cell(order, MarketResearchMetric.HouseholdFood, MarketResearchBand.Funded));
        Assert.Equal(consumers, Cell(order, MarketResearchMetric.DiscretionaryBudget, MarketResearchBand.Unknown));
        // The anchor's own slot and one represented slot.
        Assert.Equal(2, Cell(order, MarketResearchMetric.TraitsUnknown, MarketResearchBand.Unknown));
        Assert.Null(Cell(order, MarketResearchMetric.CroissantTaste, MarketResearchBand.Neutral));
        // Both consumers of the resident whose workplace is gone.
        Assert.Equal(2, Cell(order, MarketResearchMetric.UsualLocationUnknown));

        var known = Start(); Catch(known.City, Noon);
        known.City.AttachResearch(new ResearchLedger());
        var full = Order(known, ResearchProducts.DetailedCityCode);
        Frames(known.City, Noon);
        Assert.NotNull(Cell(full, MarketResearchMetric.LunchOpportunities));
        Assert.Equal(0, Cell(full, MarketResearchMetric.HouseholdFood, MarketResearchBand.Unknown));
        Assert.Equal(0, Cell(full, MarketResearchMetric.DiscretionaryBudget, MarketResearchBand.Unknown));
        Assert.Equal(0, Cell(full, MarketResearchMetric.TraitsUnknown, MarketResearchBand.Unknown));
        Assert.Equal(0, Cell(full, MarketResearchMetric.UsualLocationUnknown));
        Assert.True(Cell(full, MarketResearchMetric.HouseholdFood, MarketResearchBand.Funded) > 0);
    }

    [Fact]
    public void RosterChangeMidSurveyRestarts()
    {
        var run = Start(); Catch(run.City, Noon);
        run.City.AttachResearch(new ResearchLedger(), 16);
        var order = Order(run);
        var roster = run.City.State.Food!.Residents;
        int before = roster.Count;
        while (run.Research.SurveyUnits < 3) run.City.AdvanceFrameSlice(Noon, 1);
        roster.RemoveAt(roster.Count - 1);
        Frames(run.City, Noon);
        Assert.Equal(1, run.Research.Restarts);
        Assert.Equal(before - 1, Cell(order, MarketResearchMetric.Consumers));
        Assert.Equal(Values(Reference(run.City, order)), Cells(order));
    }

    [Fact]
    public void ACityWithoutAFoodRosterRefusesTheOrderAndHoldsNothing()
    {
        var state = EconomyScenario.Create(20, 5);
        var city = new CitySimulator(state, new Walking());
        var research = city.AttachResearch(new ResearchLedger());
        Catch(city, Noon);
        Assert.Equal(ResearchSurveyRefusal.NoFoodRoster,
            research.TryOrder(ResearchProducts.BasicLocalCode, "shop-1", Agency, false, 500, Noon, out var order, out _));
        Assert.Null(order); Assert.Equal(1, research.Ledger.NextSequence);
        // An order stored past the runner (as a direct ledger call could) is not surveyable and never holds time.
        Assert.Equal(ResearchOrderRefusal.None, research.Ledger.TryOrder(ResearchProducts.DetailedCityCode, "shop-1", Agency, false, 500, Noon, out var direct));
        Assert.True(city.IsCaughtUpTo(Noon));
        Assert.Equal(new[] { direct!.Id }, research.UnsurveyableOrders);
        Assert.Equal(0, city.AdvanceTo(Noon).ResearchUnits);
        Assert.Equal(ResearchOrderState.Ordered, direct.State);

        var run = Start(); Catch(run.City, Noon);
        var food = run.City.AttachResearch(new ResearchLedger());
        Assert.Equal(ResearchSurveyRefusal.UnknownBusinessPlace,
            food.TryOrder(ResearchProducts.BasicLocalCode, "no-such-shop", Agency, false, 500, Noon, out _, out _));
        Assert.Equal(ResearchSurveyRefusal.Ledger,
            food.TryOrder("in-depth-local", "shop-1", Agency, false, 500, Noon, out _, out var refusal));
        Assert.Equal(ResearchOrderRefusal.UnknownProduct, refusal);
        Assert.Equal(ResearchSurveyRefusal.ClockMismatch,
            food.TryOrder(ResearchProducts.BasicLocalCode, "shop-1", Agency, false, 500, Noon + 5, out _, out _));
        Assert.Equal(1, food.Ledger.NextSequence);
    }

    [Fact]
    public void TheSurveyChangesNoSaleReceiptBudgetRouteOrTrafficField()
    {
        var run = Start(); Catch(run.City, Noon);
        run.City.AttachResearch(new ResearchLedger(), 16);
        run.Trace.Clear();
        string before = EconomyScenario.Digest(SaveCodec.Write(run.City.State));
        int reads = run.Market.Reads, sales = run.Market.Orders.Count;
        var local = Order(run, ResearchProducts.DetailedLocalCode, "shop-3");
        var city = Order(run, ResearchProducts.DetailedCityCode, "shop-1");
        int frames = Frames(run.City, Noon);
        Assert.True(frames > 2);
        Assert.Equal(ResearchOrderState.Surveyed, local.State); Assert.Equal(ResearchOrderState.Surveyed, city.State);
        Assert.Equal(before, EconomyScenario.Digest(SaveCodec.Write(run.City.State)));
        Assert.Equal(reads, run.Market.Reads); Assert.Equal(sales, run.Market.Orders.Count);
        Assert.Empty(run.Trace);
    }

    [Fact]
    public void IdleRunnerChangesNothing()
    {
        var plain = Start(); var idle = Start();
        var research = idle.City.AttachResearch(new ResearchLedger());
        for (double at = Noon; at <= Noon + 600; at += 60)
        {
            int units = 0;
            Frames(plain.City, at); Frames(idle.City, at, each: r => units += r.ResearchUnits);
            Assert.Equal(0, units);
        }
        Assert.Equal(plain.Trace, idle.Trace);
        Assert.Equal(SaveCodec.Write(plain.City.State), SaveCodec.Write(idle.City.State));
        Assert.Null(research.SurveyingOrderId);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4095)] [InlineData(4096)] [InlineData(4097)] [InlineData(25000)]
    public void BoundedIndexIsAnOrdinalPermutationWithAtMost4096MovesPerUnit(int count)
    {
        var roster = Roster(count, 7);
        var index = new OrdinalRosterIndex(roster, count, new int[count], new int[count], new string[count]);
        int units = 0;
        while (!index.Done) { int moves = index.Continue(); Assert.InRange(moves, 0, OrdinalRosterIndex.MovesPerUnit); units++; }
        var order = index.Order.Take(count).ToArray();
        Assert.Equal(Enumerable.Range(0, count), order.OrderBy(i => i));
        for (int i = 1; i < count; i++) Assert.True(string.CompareOrdinal(roster[order[i - 1]].ResidentId, roster[order[i]].ResidentId) < 0);
        if (count > 4096) Assert.True(units > 1);
    }

    // Represented and anchor consumer IDs in a scrambled order, as FoodCommerce enrols them.
    private static List<ResidentMeal> Roster(int count, int seed)
    {
        var meals = new List<ResidentMeal>(count);
        for (int i = 0; i < count; i++)
        {
            string anchor = "rlo:econ:c" + (StableRandom.Hash(i.ToString(CultureInfo.InvariantCulture), seed) % 1000000).ToString("D6", CultureInfo.InvariantCulture) + ":" + i.ToString(CultureInfo.InvariantCulture);
            meals.Add(new ResidentMeal { ResidentId = FoodRepresentation.ConsumerId(anchor, i % 3), AnchorId = anchor, Slot = i % 3 });
        }
        return meals;
    }

    /// <summary>Offline perf for docs/perf/c5b.md. Runs only with PC_C5B_PERF=1.</summary>
    [Fact]
    public void Perf()
    {
        if (Environment.GetEnvironmentVariable("PC_C5B_PERF") != "1") return;
        double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
        string Stats(List<double> v)
        {
            v.Sort();
            double P(double q) => v.Count == 0 ? 0 : v[Math.Min(v.Count - 1, (int)Math.Ceiling(q * v.Count) - 1)];
            return string.Format(CultureInfo.InvariantCulture, "n={0} p50={1:F3} p95={2:F3} max={3:F3} ms", v.Count, P(.5), P(.95), v.Count == 0 ? 0 : v[^1]);
        }
        foreach (int consumers in new[] { 1914, 10000, 100000 })
        {
            // Index alone over a synthetic roster: bounded units, and the single synchronous sort.
            var roster = Roster(consumers, 3);
            var unitTimes = new List<double>(); var singles = new List<double>(); var keyless = new List<double>();
            for (int rep = 0; rep < 5; rep++)
            {
                var index = new OrdinalRosterIndex(roster, consumers, new int[consumers], new int[consumers], new string[consumers]);
                while (!index.Done) { long t = Stopwatch.GetTimestamp(); index.Continue(); unitTimes.Add(Ms(Stopwatch.GetTimestamp() - t)); }
            }
            for (int rep = 0; rep < 20; rep++)
            {
                var keys = new string[consumers]; var order = new int[consumers];
                long t = Stopwatch.GetTimestamp();
                for (int i = 0; i < consumers; i++) { keys[i] = roster[i].ResidentId; order[i] = i; }
                Array.Sort(keys, order, StringComparer.Ordinal);
                singles.Add(Ms(Stopwatch.GetTimestamp() - t));
                var direct = Enumerable.Range(0, consumers).ToArray();
                t = Stopwatch.GetTimestamp();
                Array.Sort(direct, (a, b) => string.CompareOrdinal(roster[a].ResidentId, roster[b].ResidentId));
                keyless.Add(Ms(Stopwatch.GetTimestamp() - t));
            }
            output.WriteLine($"{consumers} index units: {Stats(unitTimes)}; single sort (key array): {Stats(singles)}; single sort (roster reads): {Stats(keyless)}");

            // Whole survey in a city: index and engine units inside the hold.
            var run = Start(residents: consumers / 2, finances: true);
            Catch(run.City, run.City.State.Minute + 1);
            var research = run.City.AttachResearch(new ResearchLedger(), 256);
            var indexUnits = new List<double>(); var surveyUnits = new List<double>();
            long allocated = 0;
            for (int rep = 0; rep < 2; rep++)
            {
                var product = rep == 0 ? ResearchProducts.DetailedCityCode : ResearchProducts.DetailedLocalCode;
                var order = Order(run, product, rep == 0 ? "shop-1" : "shop-2");
                long gc = GC.GetAllocatedBytesForCurrentThread();
                int frames = 0, total = 0;
                while (order.State == ResearchOrderState.Ordered)
                {
                    long beforeIndex = research.IndexUnits;
                    long t = Stopwatch.GetTimestamp();
                    var result = run.City.AdvanceFrameSlice(run.City.State.Minute, 1);
                    double ms = Ms(Stopwatch.GetTimestamp() - t);
                    if (result.ResearchUnits == 0) continue;
                    total++;
                    (research.IndexUnits > beforeIndex ? indexUnits : surveyUnits).Add(ms);
                }
                frames = (total + 15) / 16;
                allocated = GC.GetAllocatedBytesForCurrentThread() - gc;
                output.WriteLine($"{consumers} survey {product}: {total} units, {frames} frames at 16 units/frame (60 FPS), allocated {allocated / 1024} KiB");
            }
            output.WriteLine($"{consumers} in-city index units: {Stats(indexUnits)}; engine units (256): {Stats(surveyUnits)}");
            // Idle: no open order.
            var idle = new List<double>();
            for (int i = 0; i < 20000; i++) { long t = Stopwatch.GetTimestamp(); run.City.IsCaughtUpTo(run.City.State.Minute); idle.Add(Ms(Stopwatch.GetTimestamp() - t)); }
            output.WriteLine($"{consumers} idle IsCaughtUpTo with runner attached: {Stats(idle)}");
        }
    }
}
