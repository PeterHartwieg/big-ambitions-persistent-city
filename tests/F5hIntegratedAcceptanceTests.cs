using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>F5h (#92): integrated acceptance of F5a–F5g together on fresh cities in writer 34. The reference city is the
/// F5g fixture at the declared load: 152 homes give 957 residents, and the census catalogue gives the largest reachable
/// commuter target at or below 1,000, so the fill grows it to about 2,500 persisted persons over two day boundaries. Every
/// check here runs in the ordinary test project; the 10,000-person matrix and the stress measurements are opt-in
/// (<see cref="F5hMeasure"/>).</summary>
public sealed class F5hIntegratedAcceptanceTests : IClassFixture<F5hIntegratedAcceptanceTests.Runs>
{
    /// <summary>The expensive runs, shared by this class's facts. A class fixture, not static fields, so xUnit drops the
    /// finished cities when the class is done: retained for the whole process they would enlarge every later GC, which
    /// the timed RushSpikeReplayTests intervals measure.</summary>
    public sealed class Runs
    {
        private readonly Lazy<(SevenDays Plain, SevenDays Reloaded)> sevenDays = new(() => (RunSevenDays(false, 100_000), RunSevenDays(true, 7)));
        private readonly Lazy<string> referenceDay2 = new(() => Prefilled(ReferenceShape(), 2));
        private readonly Lazy<Window> referenceWindow;
        public Runs()
        {
            referenceWindow = new(() => Accounting(referenceDay2.Value, ReferenceShape().Catalogue, 60, 128, 2, reload: false, skip: false));
            replay = new(() => SynchronousReplay(referenceDay2.Value, ReferenceShape().Catalogue));
        }
        internal (SevenDays Plain, SevenDays Reloaded) SevenDays => sevenDays.Value;
        internal string ReferenceDay2 => referenceDay2.Value;
        internal Window ReferenceWindow => referenceWindow.Value;
        private readonly Lazy<(long[] PerHour, string Snapshot)> replay;
        internal (long[] PerHour, string Snapshot) Replay => replay.Value;
    }

    private readonly Runs runs;
    public F5hIntegratedAcceptanceTests(Runs runs) { this.runs = runs; }

    // ---- Reference city ----

    internal const int ReferenceHomes = 152;
    internal const int StressHomes = 530;
    internal const int Seed = 2026;

    /// <summary>One integrated city shape: its homes and the businesses that are both its saved places and its census
    /// catalogue, so every workplace the census counts is one commuters can be seated in (review Astra-r1-1).</summary>
    internal sealed class Shape
    {
        public Shape(int homes, Schema28Tests.Business[] businesses)
        { Homes = homes; Businesses = businesses; Catalogue = F5gPopulationFillTests.Catalogue(homes, businesses); }
        public int Homes { get; }
        public Schema28Tests.Business[] Businesses { get; }
        public PopulationFillCatalogue Catalogue { get; }
        public (CityState State, Schema27Tests.Market Market) City(int factor = 1) => F5gPopulationFillTests.City(Homes, factor, seed: Seed, businesses: Businesses);
    }

    private static readonly Dictionary<(int Homes, long Target), Schema28Tests.Business[]> extras = new();

    /// <summary>The large businesses (900 m², 24 seats) added to F5g's base set for a fresh city of <paramref name="homes"/>:
    /// the fewest for which the city built with them as saved places has a commuter target of at least
    /// <paramref name="commuterTarget"/>, or of the whole visitor capacity minus the pool demand when negative (as F5g's
    /// stress arm). The new workplaces also seat residents, so the target is measured on the city itself.</summary>
    private static Schema28Tests.Business[] Extras(int homes, long commuterTarget)
    {
        lock (extras)
        {
            if (extras.TryGetValue((homes, commuterTarget), out var cached)) return cached;
            long Reached(int large)
            {
                var businesses = F5gPopulationFillTests.Base.Concat(F5gPopulationFillTests.Since(large)).ToArray();
                var (state, _) = F5gPopulationFillTests.City(homes, 1, seed: Seed, businesses: businesses);
                return F5gPopulationFillTests.Catalogue(homes, businesses).Targets(state).CommuterTarget;
            }
            long target = commuterTarget;
            if (target < 0)
            {
                var (probe, _) = F5gPopulationFillTests.City(homes, 1, seed: Seed);
                var t = VisitorTargets.Compute(0, 0, probe.Citizens.Count, probe.Portals.Count);
                target = t.VisitorCapacity - t.PoolDemand;
            }
            int low = 0, high = 1;
            while (Reached(high) < target) { low = high; high *= 2; Assert.True(high < 1 << 16, "no workplace count reaches " + target); }
            while (high - low > 1) { int mid = (low + high) / 2; if (Reached(mid) >= target) high = mid; else low = mid; }
            return extras[(homes, commuterTarget)] = F5gPopulationFillTests.Since(high);
        }
    }

    internal static Shape ReferenceShape() => new(ReferenceHomes, F5gPopulationFillTests.Base.Concat(Extras(ReferenceHomes, 1000)).ToArray());
    internal static Shape StressShape() => new(StressHomes, F5gPopulationFillTests.Base.Concat(Extras(StressHomes, -1)).ToArray());

    /// <summary>The reference city's added workplaces scaled by homes, for the factor-2 stress sizes: the same seats per
    /// home, so R seeded commuters find seats in the same proportion as at the declared load.</summary>
    internal static Shape ScaledShape(int homes)
    {
        int large = Extras(ReferenceHomes, 1000).Length;
        return new(homes, F5gPopulationFillTests.Base.Concat(F5gPopulationFillTests.Since((int)Math.Round(large * (double)homes / ReferenceHomes))).ToArray());
    }

    /// <summary>Lost acknowledgements keyed on the intent, so both runs lose the same ones and receipts stay uncertain.</summary>
    private static bool Lose(ResidentMeal meal) => StableRandom.Hash(meal.IntentId, 11) % 23 == 0;

    // ---- Seven days with a save and reload at every day boundary ----

    internal sealed class SevenDays
    {
        public string Final = "";
        public List<string> Orders = new();
        public int Reloads, MidFillReloads, UncertainReloads, Checkpoints;
        public List<(double Boundary, long Commuters, long Pool, VisitorTargets Targets, long Created)> Boundaries = new();
        public CityState State = null!;
        public long PortalHits, PortalMisses;
        /// <summary>Game minute of every reload, in order.</summary>
        public List<double> ReloadMinutes = new();
        /// <summary>Most commuters at their city workplace at one checkpoint, and seated commuters at the end.</summary>
        public int PeakCommutersAtWork, SeatedCommuters;
        public Shape Shape = null!;
    }

    /// <summary>A fresh reference city from 06:00 on day 1 for seven in-game days. Each hour is a checkpoint: per-wallet
    /// CashConserved, the city-wide BigInteger ledger, the F5g structural invariants and the fill bounds. With
    /// <paramref name="reload"/>, the city is saved and loaded in a new simulator (and a new planner cache) when the
    /// clock first stands on each day boundary, and again between two units of that boundary's fill.</summary>
    internal static SevenDays RunSevenDays(bool reload, int budget)
    {
        var shape = ReferenceShape();
        var (state, market) = shape.City();
        market.LoseAcknowledgement = Lose;
        var fill = shape.Catalogue;
        var planner = new F5gPopulationFillTests.CachedPortalPlanner();
        var city = F5gPopulationFillTests.Start(state, market, fill, planner);
        var run = new SevenDays();
        double start = city.State.Minute, end = start + 7 * 1440;
        Assert.Equal(1440 + 360, start);
        var seen = new Dictionary<string, VisitorPattern>(StringComparer.Ordinal);
        long lastCommuters = 0, lastPool = 0, eveCommuters = 0, evePool = 0;
        double reloadedAt = -1, midFillAt = -1;
        void Reload()
        {
            run.PortalHits += planner.PortalHits; run.PortalMisses += planner.PortalMisses;
            if (city.State.Food!.Residents.Any(m => m.Status == MealStatus.Uncertain)) run.UncertainReloads++;
            planner = new F5gPopulationFillTests.CachedPortalPlanner();
            city = F5gPopulationFillTests.Reload(city, market, fill, planner);
            run.Reloads++; run.ReloadMinutes.Add(city.State.Minute);
        }
        for (double hour = start + 60; hour <= end; hour += 60)
        {
            while (!city.IsCaughtUpTo(hour))
            {
                city.AdvanceFrameSlice(hour, budget);
                if (!reload) continue;
                double minute = city.State.Minute;
                // Between two units of the boundary's fill (the units already run are in the save).
                bool midFill = city.PopulationFillPending && city.PopulationFillUnits > 0 && minute > midFillAt;
                if (midFill) { midFillAt = minute; run.MidFillReloads++; }
                if (minute % 1440 == 0 && minute > reloadedAt || midFill) { reloadedAt = minute; Reload(); }
            }
            Checkpoint(city, hour);
        }
        void Checkpoint(CitySimulator current, double minute)
        {
            var s = current.State;
            run.Checkpoints++;
            F5gPopulationFillTests.Invariants(s, "hour " + minute);
            F5gPopulationFillTests.Conserved(s, "hour " + minute);
            Assert.Equal(s.Citizens.Count + s.Visitors.Count, s.Food!.Residents.Count);
            var (commuters, pool) = F5gPopulationFillTests.Counts(s);
            // Per-pattern counts never decrease and every visitor seen stays with its pattern.
            Assert.True(commuters >= lastCommuters && pool >= lastPool, "a pattern count decreased at " + minute);
            foreach (var visitor in s.Visitors)
                if (seen.TryGetValue(visitor.Id, out var pattern)) Assert.Equal(pattern, visitor.Stay.Pattern);
                else seen[visitor.Id] = visitor.Stay.Pattern;
            Assert.Equal(seen.Count, s.Visitors.Count);
            if (minute % 1440 == 60)
            {
                // One hour after a boundary its fill is complete (at most 16 units on the boundary minute).
                var targets = current.PopulationFillTargets!.Value;
                Assert.Equal(minute - 60, current.PopulationFillBoundary);
                // Counted from 23:00 the evening before: the fill may already have run by the 00:00 checkpoint.
                long created = commuters + pool - eveCommuters - evePool;
                Assert.True(commuters + pool <= targets.VisitorCapacity, "visitor capacity passed at " + minute);
                Assert.InRange(created, 0, PopulationFillPolicy.MaximumPersonsPerBoundary);
                // Never above a pattern's target: growth happens only below it.
                Assert.True(commuters <= Math.Max(eveCommuters, targets.CommuterTarget));
                Assert.True(pool <= Math.Max(evePool, Math.Min(targets.PoolTarget, targets.VisitorCapacity - commuters)));
                if (created < PopulationFillPolicy.MaximumPersonsPerBoundary)
                {
                    // A boundary below its cap reaches each clamped target exactly.
                    Assert.Equal(Math.Max(eveCommuters, targets.CommuterTarget), commuters);
                    Assert.Equal(Math.Max(evePool, Math.Min(targets.PoolTarget, targets.VisitorCapacity - commuters)), pool);
                }
                run.Boundaries.Add((minute - 60, commuters, pool, targets, created));
            }
            // F5g owner decision: every commuter the fill created has a working persona.
            Assert.All(s.VisitorRoutines.Where(r => r.Person!.Stay.Pattern == VisitorPattern.Commuter),
                r => Assert.True(VisitorSchedules.WorkingPersona(r.Identity.Persona)));
            // Commuter work journeys: seated commuters at their city workplace (review Astra-r1-1).
            run.PeakCommutersAtWork = Math.Max(run.PeakCommutersAtWork, s.VisitorRoutines.Count(r => r.Person!.Stay.Pattern == VisitorPattern.Commuter
                && r.DayPlaceId.Length > 0 && r.Activity == PersistentCity.Core.Activity.Work && r.PlaceId == r.DayPlaceId));
            lastCommuters = commuters; lastPool = pool;
            if (minute % 1440 == 1380) { eveCommuters = commuters; evePool = pool; }
        }
        city.SynchronizeSnapshot();
        Assert.True(city.OccupancyMatchesFullCount());
        Assert.Equal(0, city.HistoricalClockMisses);
        Assert.Equal(0, city.PopulationFillRefusals);
        run.PortalHits += planner.PortalHits; run.PortalMisses += planner.PortalMisses;
        run.Shape = shape;
        run.SeatedCommuters = city.State.Visitors.Count(v => v.Stay.Pattern == VisitorPattern.Commuter && v.Employment != null);
        run.Final = SaveCodec.Write(city.State);
        run.Orders = market.Orders.ToList();
        run.State = city.State;
        return run;
    }


    [Fact]
    public void FreshReferenceCityRunsSevenDaysWithASaveAndReloadAtEveryDayBoundaryAndEqualsTheUninterruptedRun()
    {
        var (plain, reloaded) = runs.SevenDays;
        Assert.Equal(CityState.CurrentSchema, reloaded.State.Schema);
        Assert.StartsWith("{\"Schema\":" + CityState.CurrentSchema + ",", reloaded.Final);
        // Seven boundaries (2,880 … 11,520), each reloaded exactly once on its minute. At the first two the slice that lands
        // on the boundary has already run fill units, so that one reload falls between fill units.
        Assert.Equal(7, reloaded.Boundaries.Count);
        Assert.Equal(Enumerable.Range(2, 7).Select(d => d * 1440.0), reloaded.Boundaries.Select(b => b.Boundary));
        Assert.Equal(Enumerable.Range(2, 7).Select(d => d * 1440.0), reloaded.ReloadMinutes);
        Assert.Equal(2, reloaded.MidFillReloads);
        Assert.True(reloaded.UncertainReloads > 0, "no reload with an uncertain receipt");
        Assert.Equal(7 * 24, reloaded.Checkpoints);
        // The reloaded city ends byte-identical to the uninterrupted one, with the same native orders.
        Assert.Equal(plain.Final, reloaded.Final);
        Assert.Equal(plain.Orders, reloaded.Orders);
        Assert.True(plain.Orders.Count > 1000, "orders " + plain.Orders.Count);
        Assert.Equal(plain.Boundaries.Select(b => (b.Boundary, b.Commuters, b.Pool, b.Created)), reloaded.Boundaries.Select(b => (b.Boundary, b.Commuters, b.Pool, b.Created)));
    }

    [Fact]
    public void PopulationFillConvergesToTheCensusTargetsAtTheDeclaredLoadWithoutOvershoot()
    {
        var (plain, _) = runs.SevenDays;
        var first = plain.Boundaries[0];
        // The first boundary publishes its cap; the second reaches both clamped targets. Later ones only follow the
        // commuter target as residents lose or find city jobs: each then equals its target again (checked at every
        // boundary in the run), adding a handful of commuters and no pool members.
        Assert.Equal(PopulationFillPolicy.MaximumPersonsPerBoundary, first.Created);
        var second = plain.Boundaries[1];
        Assert.Equal(second.Targets.CommuterTarget, second.Commuters);
        Assert.Equal(Math.Min(second.Targets.PoolTarget, second.Targets.VisitorCapacity - second.Commuters), second.Pool);
        Assert.All(plain.Boundaries.Skip(2), b =>
        {
            Assert.InRange(b.Created, 0, 10);
            Assert.Equal(b.Targets.CommuterTarget, b.Commuters);
            Assert.Equal(second.Pool, b.Pool);
        });
        Assert.All(plain.Boundaries, b => Assert.True(b.Commuters + b.Pool <= b.Targets.VisitorCapacity));
        // The capacity is the census's: twice the residents, on the same facts (F5g proves the full formula identity).
        var census = PopulationCensus.Compute(new PopulationCensusInput
        {
            Places = F5gPopulationFillTests.Places(ReferenceHomes, plain.Shape.Businesses),
            Workplaces = Schema28Tests.Workplaces(plain.Shape.Businesses).Census,
            Residents = PopulationCensusInput.CaptureResidents(plain.State.Citizens), StationIds = plain.State.Portals.ToList()
        });
        Assert.False(census.Refused);
        Assert.Equal(census.VisitorCapacity, second.Targets.VisitorCapacity);
        Assert.Equal(2L * plain.State.Citizens.Count, second.Targets.VisitorCapacity);
        var s = plain.State;
        Assert.Equal(957, s.Citizens.Count);
        int persons = s.Citizens.Count + s.Visitors.Count;
        Assert.InRange(persons, 2230, 2870);
        // Commuters are working personas only; the pool holds other personas too.
        Assert.Contains(s.VisitorRoutines, r => r.Person!.Stay.Pattern == VisitorPattern.DayVisit && !VisitorSchedules.WorkingPersona(r.Identity.Persona));
        // No visitor holds a car, a lease or a body before F5i (Invariants checks cars at every checkpoint).
        Assert.All(s.VisitorRoutines, r => Assert.Null(r.Car));
        Assert.True(plain.PortalHits > plain.PortalMisses);
        // Commuters hold city seats and travel to them (review Astra-r1-1): the saved places are the census's workplaces.
        Assert.True(plain.SeatedCommuters * 10 >= s.Visitors.Count(v => v.Stay.Pattern == VisitorPattern.Commuter) * 9,
            "seated commuters " + plain.SeatedCommuters);
        Assert.True(plain.PeakCommutersAtWork * 2 >= plain.SeatedCommuters, "commuters at work " + plain.PeakCommutersAtWork);
    }

    [Fact]
    public void TheIntegratedCityWrittenByTheOlderWriterIsRefusedWithStartANewCityAndKept()
    {
        var (_, reloaded) = runs.SevenDays;
        string older = DevelopmentChain.Write33(reloaded.State);
        Assert.Equal(CityState.ChangeJournalSchema, SaveCodec.DevelopmentSchemaOf(older));
        var read = SaveCodec.Read(older);
        Assert.Equal(SaveReadStatus.DevelopmentSchema, read.Status);
        Assert.Null(read.State);
        Assert.Contains("start a new city", read.Error);
        Assert.DoesNotContain("Invalid", read.Error);
        // Refused through the store and the session, never overwritten by a fresh city, and kept byte for byte.
        EconomicLedgerTests.Refused(older, SaveReadStatus.DevelopmentSchema);
        // The current writer reads the same city.
        var current = SaveCodec.Read(reloaded.Final);
        Assert.True(current.Status == SaveReadStatus.Loaded, current.Error);
        Assert.Equal(reloaded.Final, SaveCodec.Write(current.State!));
    }

    /// <summary>Review Sol-r2-1: an event waiting for its route has left the due queue but is still due work, so the
    /// backlog counter counts it. Checked at every slice that leaves planning pending on a small filled city.</summary>
    [Fact]
    public void ThePendingRoutedEventCountsAsDueWork()
    {
        var (state, market) = F5gPopulationFillTests.City(12, 1, seed: Seed);
        var planner = new AdmittedPortalPlanner();
        var city = new CitySimulator(state, planner); city.AttachFoodMarket(market);
        city.AttachPopulationFill(F5gPopulationFillTests.Catalogue(12, F5gPopulationFillTests.Base));
        double end = state.Minute + 1440;
        int pendingSlices = 0;
        while (!city.IsCaughtUpTo(end))
        {
            planner.NewFrame();
            city.AdvanceFrameSlice(end);
            if (!city.PlanningPending) continue;
            pendingSlices++;
            // The pending event is due at or before the clock and is not in the queue.
            Assert.True(city.DuePersonEventsAtOrBefore(city.State.Minute) >= 1, "pending routed event not counted at " + city.State.Minute);
            Assert.Equal(city.QueuedPersonEvents + 1, city.DuePersonEventsAtOrBefore(double.PositiveInfinity));
            while (city.PlanningPending) { planner.NewFrame(); city.ContinuePlanning(2); }
        }
        Assert.True(pendingSlices > 0, "no event waited for its route");
    }

    /// <summary>F5h changes no wire: writer 34 and both of its pinned golden hashes are as F7d left them. Since F2e took writer
    /// 35, the writer-34 bytes are the frozen shape (DevelopmentChain.Write34); F2e pins its own golden.</summary>
    [Theory]
    [InlineData(5150, "a3b70408cd5c5f0a23d9e41344d1530283232449f2b9ade5dd37755c92da4a70", 73192)]
    [InlineData(9217, "cb39d7938dfbe874054cf53afb46b660883686adc93d82654e43e7243e317c01", 72830)]
    public void CurrentWriterGoldenHashIsUnchanged(int seed, string pin, int characters)
    {
        Assert.Equal(34, CityState.CompactSchema);
        string payload = DevelopmentChain.Write34(ChangeJournalTests.Fixture(seed));
        Assert.Equal(pin, EconomicLedgerTests.Sha(payload));
        Assert.Equal(characters, payload.Length);
    }

    // ---- The F1 accounting harness with the whole F5 population ----

    internal static FoodCommerce Food(CitySimulator city) => (FoodCommerce)typeof(CitySimulator)
        .GetField("food", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(city)!;

    internal sealed class Window
    {
        public string Snapshot = "";
        public string[] Orders = Array.Empty<string>();
        public long Frames, Held, HeldAtBoundary, PlannedEvents, SliceEvents, FramesWithBacklog, FillUnits, FillCreated, HistoricalClockMisses, Postings, CitizenUnits, FoodUnits;
        public double MaxLag;
        public int PeakDueBacklog, LongestBacklogFrames;
        public int Reloads, FillReloads, MaxPathsInFrame, MaxFramesWaited, PeakPersonQueue, PeakMealQueue, PeakPlanningFrames;
        public long Paths, Jobs, Deferred, PlanningRequests, PortalHits, PortalMisses;
        public long[] EventsPerHour = new long[24];
        public int PersonsAtStart, PersonsAtEnd;
        /// <summary>Per-frame mod-work and its parts (fill units, budget postings, planning, every other unit), in ms.</summary>
        public List<double> Frame = new(), FillMs = new(), PostingMs = new(), PlanningMs = new(), EventMs = new();
        /// <summary>Single units: one fill unit (at most 64 persons) or one posting unit (at most 32 wallets) per slice.</summary>
        /// <summary>Every single unit of 5 ms or more: its time, component, game minute and the GC generation collected during it (-1 none).</summary>
        public List<(double Ms, string Component, double Minute, int Gc)> Spikes = new();
        public List<double> FillUnitMs = new(), FillUnitBytes = new(), PostingUnitMs = new(), CitizenUnitMs = new(), FoodUnitMs = new();
    }

    /// <summary>F5g's admitted planner with the geometry cache model of <see cref="F5gPopulationFillTests.CachedPortalPlanner"/>:
    /// a request whose endpoint pair is cached (1,024 entries, FIFO, negative entries kept) needs no native path; a miss
    /// needs one or two, admitted at most 16 per frame like Runtime's MultimodalRoutes. Every result is a fresh copy.</summary>
    internal sealed class AdmittedPortalPlanner : IResumableRoutePlanner, ICommuteTimingPlanner
    {
        public const int PathsPerFrame = 16;
        private readonly HashSet<string> cache = new(StringComparer.Ordinal);
        private readonly Queue<string> order = new();
        private int used;
        public long Paths, Jobs, Deferred, Requests, PortalRequests, PortalHits, PortalMisses;
        public int MaxPathsInFrame, MaxFramesWaited;
        public void NewFrame() { MaxPathsInFrame = Math.Max(MaxPathsInFrame, used); used = 0; }
        /// <summary>Runtime's <c>JobAdmissionExhaustedForFrame</c>: this frame's native paths are spent.</summary>
        public bool AdmissionExhausted => used >= PathsPerFrame;
        public IRoutePlanningJob Begin(RouteRequest request) { Jobs++; return new Job(this, request); }
        public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences) { Lookup(from, to); return Walk(from, to); }
        private bool Lookup(Place from, Place to)
        {
            Requests++;
            bool portal = PortalPlaces.Is(from) || PortalPlaces.Is(to);
            string key = from.Id + "\n" + to.Id;
            bool hit = cache.Contains(key);
            if (portal) { PortalRequests++; if (hit) PortalHits++; else PortalMisses++; }
            if (!hit)
            {
                if (cache.Count >= 1024) cache.Remove(order.Dequeue());
                cache.Add(key); order.Enqueue(key);
            }
            return hit;
        }
        private static Route Walk(Place from, Place to) => new Route { Legs = new() { new RouteLeg { From = from.Position, To = to.Position,
            Mode = TravelMode.Walk, Minutes = Math.Max(1, Position.Distance(from.Position, to.Position) / 80) } } };
        private sealed class Job : IRoutePlanningJob
        {
            private readonly AdmittedPortalPlanner owner; private readonly RouteRequest request;
            private int need = -1, frames;
            public Job(AdmittedPortalPlanner owner, RouteRequest request) { this.owner = owner; this.request = request; }
            public Route? Result { get; private set; }
            public bool Continue(double budgetMilliseconds)
            {
                if (need < 0) need = owner.Lookup(request.From, request.To) ? 0 : 1 + (int)(StableRandom.Hash(request.From.Id + ">" + request.To.Id, 5) % 2);
                while (need > 0 && owner.used < PathsPerFrame) { owner.used++; owner.Paths++; need--; }
                if (need > 0) { owner.Deferred++; frames++; owner.MaxFramesWaited = Math.Max(owner.MaxFramesWaited, frames); return false; }
                Result = Walk(request.From, request.To);
                return true;
            }
            public void Dispose() { }
        }
    }

    /// <summary>A fresh city advanced (synchronous planner, no frame loop) to 06:00 on <paramref name="day"/>, saved. The
    /// window that follows crosses that night's day boundary.</summary>
    internal static string Prefilled(Shape shape, int day, int factor = 1)
    {
        var (state, market) = shape.City(factor);
        var city = F5gPopulationFillTests.Start(state, market, shape.Catalogue, new F5gPopulationFillTests.CachedPortalPlanner());
        F5gPopulationFillTests.Advance(city, day * 1440 + 360, 100_000);
        city.SynchronizeSnapshot();
        return SaveCodec.Write(city.State);
    }

    /// <summary>One game day of the F1 frame loop (as <see cref="F5gAccountingTests.Run"/>) from a saved city: the native
    /// clock is held by <see cref="AccountingClock.Limit"/>, the market refuses checkouts outside the current native hour,
    /// route jobs are admitted at most 16 native paths per frame, and frames run <paramref name="eventBudget"/> units within
    /// <paramref name="milliseconds"/>. With <paramref name="reload"/> the city is saved and loaded as the clock reaches the
    /// midnight boundary and again between two units of its fill. With <paramref name="citizenLimit"/> the frame is shaped as
    /// Runtime's (<c>PersistentCityMod</c>): <paramref name="eventBudget"/> caps all units and only citizen events (a finished
    /// route plan included) count against the citizen limit; without it every unit counts against the event budget.</summary>
    internal static Window Accounting(string payload, PopulationFillCatalogue fill, int fps, int eventBudget, double milliseconds, bool reload, bool skip, int? citizenLimit = null)
    {
        var read = SaveCodec.Read(payload); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var state = read.State!;
        var market = new F5gAccountingTests.ClockMarket { Shops = F5gPopulationFillTests.Shops(state), Clock = state.Minute };
        var planner = new AdmittedPortalPlanner();
        CitySimulator Start(CityState s) { var c = new CitySimulator(s, planner); c.AttachFoodMarket(market); c.AttachPopulationFill(fill); return c; }
        var city = Start(state);
        var w = new Window { PersonsAtStart = state.Citizens.Count + state.Visitors.Count };
        double start = state.Minute, boundary = (Math.Floor(start / 1440) + 1) * 1440, end = start + 1440;
        double native = start;
        bool midnightReload = false, fillReload = false;
        long creditedUnits = 0, creditedCreated = 0, creditedRequests = 0, creditedPostings = 0;
        int planningFrames = 0, backlogFrames = 0;
        void Credit()
        {
            w.FillUnits += city.PopulationFillUnits - creditedUnits; w.FillCreated += city.PopulationFillCreated - creditedCreated;
            w.PlanningRequests += city.PlanningRequests - creditedRequests; w.Postings += Food(city).BudgetPostings - creditedPostings;
        }
        void Reload()
        {
            Credit();
            string saved = SaveCodec.Write(city.State);
            var again = SaveCodec.Read(saved); Assert.True(again.Status == SaveReadStatus.Loaded, again.Error);
            Assert.Equal(saved, SaveCodec.Write(again.State!));
            w.Reloads++;
            city = Start(again.State!);
            creditedUnits = creditedCreated = creditedRequests = creditedPostings = 0;
        }
        double Ms(long from, long to) => (to - from) * 1000.0 / Stopwatch.Frequency;
        while (!city.IsCaughtUpTo(end))
        {
            Assert.True(++w.Frames < 4_000_000, "nonterminating window");
            planner.NewFrame();
            long started = Stopwatch.GetTimestamp(), excluded = 0;
            Assert.True(AccountingClock.Compatible(city.State.Minute, native), "input epoch escaped");
            Assert.True(city.IsAccountingEpochDrained(native), "unfinished ending epoch escaped");
            double target = AccountingClock.Target(native);
            bool ready = city.IsCaughtUpTo(target);
            float requested = (float)Math.Min(skip ? 120 : 30.0 / fps, end - native);
            float allowed = AccountingClock.Limit(ready, native, requested);
            if (allowed == 0 && requested > 0) { w.Held++; if (city.PopulationFillPending) w.HeldAtBoundary++; }
            int nativeHour = (int)Math.Floor(native / 60); float nativeMinute = (float)(native - nativeHour * 60.0) + allowed;
            while (nativeMinute >= 60) { nativeMinute -= 60; nativeHour++; }
            native = nativeHour * 60.0 + nativeMinute;
            market.Clock = native;
            Assert.True(city.BeginAccountingObservations(native), "observation epoch not published");
            target = Math.Max(city.State.Minute, AccountingClock.Target(native));
            if (reload && !midnightReload && city.State.Minute == boundary)
            { long saveStarted = Stopwatch.GetTimestamp(); midnightReload = true; Reload(); excluded += Stopwatch.GetTimestamp() - saveStarted; }
            w.MaxLag = Math.Max(w.MaxLag, target - city.State.Minute);
            double fillMs = 0, postingMs = 0, planningMs = 0, eventMs = 0;
            var food = Food(city);
            // PersistentCityMod's loop: the first unit always runs; a planning slice gets the frame's remaining time; an
            // unfinished plan ends the frame only when the time or the native-path admission is spent (otherwise the
            // event exposed its next request and planning continues); a finished plan is one citizen event and unit.
            int citizens = 0;
            double Elapsed() => Ms(started + excluded, Stopwatch.GetTimestamp());
            for (int unit = 0; unit < eventBudget; unit++)
            {
                if (city.PlanningPending)
                {
                    long p = Stopwatch.GetTimestamp(); bool done = city.ContinuePlanning(Math.Max(0, milliseconds - Elapsed())); planningMs += Ms(p, Stopwatch.GetTimestamp());
                    if (!done) { if (Elapsed() >= milliseconds || planner.AdmissionExhausted) break; continue; }
                    w.CitizenUnits++; w.PlannedEvents++;
                    w.EventsPerHour[(int)Math.Min(23, Math.Max(0, (city.State.Minute - start) / 60))]++;
                    citizens++;
                    if ((citizenLimit.HasValue && citizens >= citizenLimit.Value) || Elapsed() >= milliseconds) break;
                    continue;
                }
                long fillBefore = city.PopulationFillUnits, postedBefore = food.BudgetPostings, bytes = GC.GetAllocatedBytesForCurrentThread();
                int gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2);
                long t = Stopwatch.GetTimestamp();
                var advanced = city.AdvanceFrameSlice(target);
                double ms = Ms(t, Stopwatch.GetTimestamp());
                if (ms >= 5)
                {
                    int gc = GC.CollectionCount(2) != gc2 ? 2 : GC.CollectionCount(1) != gc1 ? 1 : GC.CollectionCount(0) != gc0 ? 0 : -1;
                    string component = city.PopulationFillUnits != fillBefore ? "fill" : food.BudgetPostings != postedBefore ? "posting"
                        : advanced.FoodContinuationEvents > 0 ? "food" : advanced.TrafficEvents > 0 ? "traffic" : "citizen";
                    w.Spikes.Add((ms, component, city.State.Minute, gc));
                }
                if (city.PopulationFillUnits != fillBefore) { fillMs += ms; w.FillUnitMs.Add(ms); w.FillUnitBytes.Add(GC.GetAllocatedBytesForCurrentThread() - bytes); }
                else if (food.BudgetPostings != postedBefore) { postingMs += ms; w.PostingUnitMs.Add(ms); }
                else if (advanced.Events > 0 || advanced.PlanningPending)
                {
                    eventMs += ms;
                    if (advanced.FoodContinuationEvents > 0) { w.FoodUnits += advanced.Events; w.FoodUnitMs.Add(ms); }
                    else { w.CitizenUnits += advanced.Events; w.CitizenUnitMs.Add(ms); }
                }
                w.EventsPerHour[(int)Math.Min(23, Math.Max(0, (city.State.Minute - start) / 60))] += advanced.Events;
                w.SliceEvents += advanced.Events;
                if (citizenLimit.HasValue) citizens += advanced.CitizenEvents;
                if (advanced.PlanningPending) { if (Elapsed() >= milliseconds) break; continue; }
                if (advanced.CaughtUp || (citizenLimit.HasValue && citizens >= citizenLimit.Value) || Elapsed() >= milliseconds) break;
            }
            city.SynchronizeSnapshot();
            w.Frame.Add(Ms(started + excluded, Stopwatch.GetTimestamp()));
            if (fillMs > 0) w.FillMs.Add(fillMs);
            if (postingMs > 0) w.PostingMs.Add(postingMs);
            if (planningMs > 0) w.PlanningMs.Add(planningMs);
            if (eventMs > 0) w.EventMs.Add(eventMs);
            planningFrames = city.PlanningPending ? planningFrames + 1 : 0;
            w.PeakPlanningFrames = Math.Max(w.PeakPlanningFrames, planningFrames);
            // Due work left after the frame: events and meals due at or before the frame's target that have not run.
            int backlog = city.DuePersonEventsAtOrBefore(target) + city.DueMealEventsAtOrBefore(target);
            w.PeakDueBacklog = Math.Max(w.PeakDueBacklog, backlog);
            backlogFrames = backlog > 0 ? backlogFrames + 1 : 0;
            w.LongestBacklogFrames = Math.Max(w.LongestBacklogFrames, backlogFrames);
            if (backlog > 0) w.FramesWithBacklog++;
            w.PeakPersonQueue = Math.Max(w.PeakPersonQueue, city.QueuedPersonEvents);
            w.PeakMealQueue = Math.Max(w.PeakMealQueue, city.QueuedMealEvents);
            if (reload && !fillReload && city.PopulationFillPending && city.PopulationFillUnits > 0) { fillReload = true; w.FillReloads++; Reload(); }
        }
        Credit();
        w.HistoricalClockMisses = city.HistoricalClockMisses;
        w.Snapshot = SaveCodec.Write(city.State); w.Orders = market.Orders.ToArray();
        w.Paths = planner.Paths; w.Jobs = planner.Jobs; w.Deferred = planner.Deferred; w.PortalHits = planner.PortalHits; w.PortalMisses = planner.PortalMisses;
        planner.NewFrame();
        w.MaxPathsInFrame = planner.MaxPathsInFrame; w.MaxFramesWaited = planner.MaxFramesWaited;
        w.PersonsAtEnd = city.State.Citizens.Count + city.State.Visitors.Count;
        F5gPopulationFillTests.Invariants(city.State, "window"); F5gPopulationFillTests.Conserved(city.State, "window");
        return w;
    }

    /// <summary>F5g's clock market without the native-hour check, for a replay that has no native clock: the same
    /// prices, the same duplicate refusal and the same lost acknowledgement on every 29th order.</summary>
    private sealed class ReplayMarket : IFoodMarket, IFoodSaleEvidence
    {
        public FoodBusiness[] Shops = Array.Empty<FoodBusiness>();
        public readonly List<string> Orders = new();
        private readonly HashSet<string> paid = new(StringComparer.Ordinal);
        public double EarliestMinute => 0;
        public IReadOnlyList<FoodBusiness> Read(double minute) => Shops;
        public FoodSale Buy(ResidentMeal meal, double at)
        {
            var offer = Shops.Single(b => b.Id == meal.BusinessId).Offers.Single(o => o.ProductId == meal.ProductId);
            if (offer.Price != meal.Price) return new FoodSale(false, "Price changed");
            Assert.True(paid.Add(meal.IntentId), "duplicate native order " + meal.IntentId);
            Orders.Add(meal.IntentId + "@" + at.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            if (Orders.Count % 29 == 0) throw new InvalidOperationException("Lost native acknowledgement");
            return new FoodSale(true, "Confirmed");
        }
        public bool IsConfirmedPaid(ResidentMeal meal) => paid.Contains(meal.IntentId);
    }

    /// <summary>The window planner's routes and commute-timing role, answered synchronously.</summary>
    private sealed class SynchronousWalk : ICommuteTimingPlanner
    {
        public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences) => new Route { Legs = new() { new RouteLeg { From = from.Position,
            To = to.Position, Mode = TravelMode.Walk, Minutes = Math.Max(1, Position.Distance(from.Position, to.Position) / 80) } } };
    }

    /// <summary>The same day replayed with the synchronous planner, one unit per slice and no frame loop: its unit
    /// total and per-hour units are what the frame loop's counts must reconcile with (review Astra-r1-3).</summary>
    internal static (long[] PerHour, string Snapshot) SynchronousReplay(string payload, PopulationFillCatalogue fill)
    {
        var read = SaveCodec.Read(payload); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var state = read.State!;
        var market = new ReplayMarket { Shops = F5gPopulationFillTests.Shops(state) };
        var city = new CitySimulator(state, new SynchronousWalk());
        city.AttachFoodMarket(market); city.AttachPopulationFill(fill);
        double start = state.Minute, end = start + 1440;
        var perHour = new long[24];
        while (!city.IsCaughtUpTo(end))
        {
            var advanced = city.AdvanceFrameSlice(end, 1);
            perHour[(int)Math.Min(23, Math.Max(0, (state.Minute - start) / 60))] += advanced.Events;
        }
        city.SynchronizeSnapshot();
        return (perHour, SaveCodec.Write(state) + "\n" + string.Join(",", market.Orders));
    }

    /// <summary>The plan's F1 arms (section 3): 30 FPS / 32 events / 4 ms, 60 / 16 / 2, 120 / 128 / 2 and a hidden skip.</summary>
    public static IEnumerable<object[]> Arms() => new[]
    {
        new object[] { 30, 32, 4.0, false }, new object[] { 60, 16, 2.0, false }, new object[] { 120, 128, 2.0, false }, new object[] { 60, 32, 4.0, true },
    };


    /// <summary>The declared load through the F1 matrix: from 06:00 on day 2 (957 residents and the first boundary's 1,024
    /// visitors) to 06:00 on day 3, across the second boundary that completes the fill. Every arm, with a save at
    /// midnight and one between fill units, ends equal to the unsaved reference in save and orders.</summary>
    [Theory]
    [MemberData(nameof(Arms))]
    public void DeclaredLoadF1MatrixEqualsTheReferenceAcrossBudgetsCadencesSkipAndReload(int fps, int budget, double milliseconds, bool skip)
    {
        var reference = runs.ReferenceWindow;
        Assert.Equal(957 + PopulationFillPolicy.MaximumPersonsPerBoundary, reference.PersonsAtStart);
        Assert.InRange(reference.PersonsAtEnd, 2230, 2870);
        Assert.True(reference.FillCreated > 0);
        foreach (bool reload in new[] { false, true })
        {
            var actual = Accounting(runs.ReferenceDay2, ReferenceShape().Catalogue, fps, budget, milliseconds, reload, skip);
            Assert.Equal(reference.Snapshot, actual.Snapshot);
            Assert.Equal(reference.Orders, actual.Orders);
            Assert.Equal(0, actual.HistoricalClockMisses);
            Assert.InRange(actual.MaxLag, 0, AccountingClock.MaximumLagMinutes);
            Assert.InRange(actual.MaxPathsInFrame, 1, AdmittedPortalPlanner.PathsPerFrame);
            Assert.Equal(reference.FillCreated, actual.FillCreated);
            if (reload) Assert.True(actual.Reloads >= 1);
        }
        Assert.True(reference.Orders.Length > 0);
        Assert.Equal(0, reference.HistoricalClockMisses);
        // Every event is counted once, those finished through resumable planning included: the frame loop's unit total
        // equals the synchronous replay's, and the two cities end equal.
        var replay = runs.Replay;
        Assert.Equal(replay.Snapshot, reference.Snapshot + "\n" + string.Join(",", reference.Orders));
        Assert.True(reference.PlannedEvents > 0);
        Assert.Equal(replay.PerHour.Sum(), reference.EventsPerHour.Sum());
    }
}
