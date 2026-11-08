using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>F5g (#92): population fill and visitor transport in writer 33 without a schema bump. At each day boundary the
/// city creates commuters and the day-visitor pool toward the census targets in units of at most 64 persons, through the
/// one event queue; it never retires, clamps or rewrites persisted persons, wallets or obligations.</summary>
public sealed class F5gPopulationFillTests
{
    // ---- Fixtures ----

    internal static readonly Schema28Tests.Business[] Base = Schema28Tests.Businesses;

    /// <summary>Homes of seven beds, the given businesses (Work/Study places as GamePlaces builds them), a park and three shops.</summary>
    internal static List<Place> Places(int homes, IEnumerable<Schema28Tests.Business> businesses)
    {
        var places = Enumerable.Range(0, homes).Select(h => new Place { Id = "home:" + h.ToString("000"), Name = "Home " + h, Kind = PlaceKind.Home,
            Capacity = 7, ResidentialAreaSquareMetres = 160, Position = new Position(-60 + 25 * (h % 8), 0, -40 + 30 * (h / 8)) }).ToList();
        places.AddRange(Schema28Tests.Workplaces(businesses).Places);
        places.Add(new Place { Id = "leisure:a", Name = "Park", Kind = PlaceKind.Leisure, Capacity = 64, Position = new Position(60, 0, -80) });
        places.AddRange(Enumerable.Range(0, 3).Select(i => new Place { Id = "shop:" + i, Name = "Shop " + i, Kind = PlaceKind.Shop,
            Capacity = 16, Position = new Position(20 + i * 30, 0, 15 - i * 5) }));
        return places;
    }

    /// <summary>The census catalogue Runtime reads at load: the city's businesses plus businesses established since
    /// (catalogue-only, never saved places), as many as needed for the requested commuter demand.</summary>
    internal static PopulationFillCatalogue Catalogue(int homes, IEnumerable<Schema28Tests.Business> businesses, IEnumerable<Schema28Tests.Business>? since = null)
    {
        var all = businesses.Concat(since ?? Array.Empty<Schema28Tests.Business>()).ToList();
        var places = Places(homes, all);
        return new PopulationFillCatalogue(places, Schema28Tests.Workplaces(all).Census);
    }

    internal static Schema28Tests.Business[] Since(int large, int small = 0)
        => Enumerable.Range(0, large).Select(i => new Schema28Tests.Business("address:new:" + i.ToString("000"), 900))
            .Concat(Enumerable.Range(0, small).Select(i => new Schema28Tests.Business("address:newsmall:" + i, 30))).ToArray();

    /// <summary>A catalogue whose commuter target for <paramref name="state"/> is exactly <paramref name="target"/>.</summary>
    /// <remarks>Every band capacity is even, so round(80% of capacity) skips some integers; whether a target is reachable
    /// depends on the residents' worker count. <see cref="TryCatalogueFor"/> reports that instead of throwing.</remarks>
    internal static (PopulationFillCatalogue Catalogue, Schema28Tests.Business[] Since) CatalogueFor(CityState state, int homes, long target)
        => TryCatalogueFor(state, homes, target, Base) ?? throw new InvalidOperationException("No catalogue gives commuter target " + target);

    internal static (PopulationFillCatalogue Catalogue, Schema28Tests.Business[] Since)? TryCatalogueFor(CityState state, int homes, long target,
        IEnumerable<Schema28Tests.Business>? saved = null)
    {
        var own = (saved ?? Base).ToArray();
        long previous = -1;
        // Twelve small businesses (1.6 each after the 80% fill) span one large step (19.2).
        for (int large = 0; large < 600; large++)
            for (int small = 0; small <= 12; small++)
            {
                var since = Since(large, small);
                var catalogue = Catalogue(homes, own, since);
                long reached = catalogue.Targets(state).CommuterTarget;
                if (reached == target) return (catalogue, since);
                if (small == 0 && previous > target) return null;
                if (small == 0) previous = reached;
            }
        return null;
    }

    internal static (CityState State, Schema27Tests.Market Market) City(int homes = 12, int factor = 1, int seed = 5150, bool portals = true,
        bool finances = true, IEnumerable<Schema28Tests.Business>? businesses = null)
    {
        var state = Population.CreateFromHousing(Schema28Tests.Donors, Places(homes, businesses ?? Base), "f5g-fill", "0.41.1", seed, 1440 + 6 * 60);
        if (portals) PortalPlaces.Add(state, Schema27Tests.Stations);
        state.Food = new FoodCommerceState { MealRhythmVersion = MealRhythm.PolicyVersion, HouseholdFinanceVersion = finances ? HouseholdFinance.PolicyVersion : 0,
            RepresentationFactor = factor,
            Shops = Enumerable.Range(0, 3).Select(i => new FoodScope { PlaceId = "shop:" + i, BusinessId = "b" + i, ProductId = FoodProducts.Croissant }).ToList() };
        var market = new Schema27Tests.Market { Shops = Shops(state) };
        // Attaching the market creates the roster and the factor's K−1 visitors per resident, as Runtime does.
        new CitySimulator(state, new FreshCityTraceTests.Walk()).AttachFoodMarket(market);
        return (state, market);
    }

    internal static FoodBusiness[] Shops(CityState state) => state.Food!.Shops.Select(s => new FoodBusiness { Id = s.BusinessId, PlaceId = s.PlaceId,
        Name = s.PlaceId, MealsPerHour = 30, Hours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 6 * 60, ToMinute = 22 * 60 }).ToList(),
        Offers = new() { new FoodOffer { ProductId = FoodProducts.Croissant, Price = Money.FromCents(250), Units = 100000 } } }).ToArray();

    internal static CitySimulator Start(CityState state, Schema27Tests.Market market, PopulationFillCatalogue? fill, IRoutePlanner? routes = null)
    {
        var city = new CitySimulator(state, routes ?? new FreshCityTraceTests.Walk());
        city.AttachFoodMarket(market);
        city.AttachPopulationFill(fill);
        return city;
    }

    internal static CitySimulator Reload(CitySimulator city, Schema27Tests.Market market, PopulationFillCatalogue? fill, IRoutePlanner? routes = null)
    {
        string payload = SaveCodec.Write(city.State);
        var read = SaveCodec.Read(payload);
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(payload, SaveCodec.Write(read.State!));
        return Start(read.State!, market, fill, routes);
    }

    internal static void Advance(CitySimulator city, double minute, int budget = 64) { while (!city.AdvanceTo(minute, budget).CaughtUp) { } }

    internal static (long Commuters, long Pool) Counts(CityState state)
        => (state.Visitors.LongCount(v => v.Stay.Pattern == VisitorPattern.Commuter), state.Visitors.LongCount(v => v.Stay.Pattern == VisitorPattern.DayVisit));

    /// <summary>Per-wallet CashConserved and the city-wide BigInteger ledger (sources equal uses).</summary>
    internal static void Conserved(CityState state, string context)
    {
        BigInteger sources = 0, uses = 0;
        foreach (var b in state.Food!.Residents.Select(m => m.Budget).Where(b => b != null))
        {
            Assert.True(HouseholdFinance.CashConserved(b!), context + ": wallet not conserved");
            sources += Money.TotalUnits(new[] { b!.OpeningBalance, b.IncomeReceived, b.Ledger.EmploymentIncome });
            uses += Money.TotalUnits(new[] { b.Balance, b.EssentialsPaid, b.HomeFoodPaid, b.PreparedFoodSpent, b.ExternalFoodSpent, b.Ledger.Discretionary, b.HeldPurchase });
        }
        Assert.True(sources == uses, context + ": city ledger " + sources + " != " + uses);
    }

    /// <summary>Structural F5g invariants at any instant: one roster record (and wallet with finances) per person, ordinal
    /// visitor lists, sequence equal to the count, aggregate within capacity unless seeded above it.</summary>
    internal static void Invariants(CityState state, string context)
    {
        var persons = state.Citizens.Select(c => c.Identity.Id).Concat(state.Visitors.Select(v => v.Id)).ToList();
        var roster = state.Food!.Residents.Select(m => m.ResidentId).ToList();
        Assert.Equal(persons.Count, roster.Count); Assert.Equal(persons.OrderBy(i => i, StringComparer.Ordinal), roster.OrderBy(i => i, StringComparer.Ordinal));
        Assert.All(state.Food.Residents, m => Assert.Equal(state.Food.HouseholdFinanceVersion != 0, m.Budget != null));
        for (int i = 1; i < state.Visitors.Count; i++) Assert.True(string.CompareOrdinal(state.Visitors[i - 1].Id, state.Visitors[i].Id) < 0, context);
        Assert.Equal(state.Visitors.Count + 1, state.NextVisitorSequence);
        for (int i = 0; i < state.Visitors.Count; i++) Assert.Same(state.Visitors[i].Routine, state.VisitorRoutines[i]);
        Assert.All(state.Visitors.Where(v => v.Stay.Pattern == VisitorPattern.DayVisit), v => { Assert.Null(v.Employment); Assert.Equal("", v.Routine!.DayPlaceId); });
        Assert.All(state.VisitorRoutines, r => { Assert.Null(r.Car); Assert.False(r.Preferences.OwnsCar);
            Assert.True(r.Journey?.Legs == null || r.Journey.Legs.All(l => l.Mode != TravelMode.Car)); });
    }

    // ---- Targets ----

    [Fact]
    public void FillTargetsEqualTheCensusOnTheSameFacts()
    {
        foreach (int seed in new[] { 5150, 77, 9001 })
            foreach (var since in new[] { Since(0), Since(3, 1), Since(40, 2) })
            {
                var (state, _) = City(12, 1, seed);
                var places = Places(12, Base.Concat(since));
                var census = Schema28Tests.Workplaces(Base.Concat(since)).Census;
                var targets = new PopulationFillCatalogue(places, census).Targets(state);
                var reference = PopulationCensus.Compute(new PopulationCensusInput { Places = places, Workplaces = census,
                    Residents = PopulationCensusInput.CaptureResidents(state.Citizens), StationIds = state.Portals.ToList() });
                Assert.False(reference.Refused);
                Assert.Equal(reference.CommuterTarget, targets.CommuterTarget); Assert.Equal(reference.DayVisitorPoolTarget, targets.PoolTarget);
                Assert.Equal(reference.VisitorCapacity, targets.VisitorCapacity);
                Assert.Equal(reference.CommuterClamped, targets.CommuterClamped); Assert.Equal(reference.PoolClamped, targets.PoolClamped);
                // Order of the catalogue is irrelevant.
                var shuffled = new PopulationFillCatalogue(places.AsEnumerable().Reverse().ToList(), census.AsEnumerable().Reverse().ToList()).Targets(state);
                Assert.Equal(targets.CommuterTarget, shuffled.CommuterTarget); Assert.Equal(targets.PoolTarget, shuffled.PoolTarget);
            }
    }

    [Fact]
    public void TargetClampsAtExactBoundariesAndWithoutStations()
    {
        // VisitorCapacity = min(2R, 100000); commuters first, then the pool in the capacity left.
        var t = VisitorTargets.Compute(workplaceCapacity: 1000, eligibleResidentWorkers: 0, residents: 100, stations: 2);
        Assert.Equal(200, t.VisitorCapacity); Assert.Equal(800, t.CommuterDemand); Assert.Equal(200, t.CommuterTarget); Assert.Equal(0, t.PoolTarget);
        Assert.True(t.CommuterClamped); Assert.True(t.PoolClamped);
        // Demand exactly equal to capacity: no commuter clamp, no pool room.
        t = VisitorTargets.Compute(250, 0, 100, 2);
        Assert.Equal(200, t.CommuterDemand); Assert.Equal(200, t.CommuterTarget); Assert.False(t.CommuterClamped); Assert.Equal(0, t.PoolTarget);
        // One below capacity: exactly one pool place.
        t = VisitorTargets.Compute(250, 1, 100, 2);
        Assert.Equal(199, t.CommuterTarget); Assert.Equal(1, t.PoolTarget); Assert.True(t.PoolClamped);
        // Pool demand ceil(round(15% R) / 25%) when unclamped.
        t = VisitorTargets.Compute(0, 0, 100, 1);
        Assert.Equal(15, t.DayVisitorsPerDay); Assert.Equal(60, t.PoolDemand); Assert.Equal(60, t.PoolTarget); Assert.Equal(0, t.CommuterTarget);
        // The 100,000 hard cap.
        t = VisitorTargets.Compute(1_000_000, 0, 50_000, 3);
        Assert.Equal(100_000, t.VisitorCapacity); Assert.Equal(100_000, t.CommuterTarget); Assert.Equal(0, t.PoolTarget);
        // No portal: no target and no clamp.
        t = VisitorTargets.Compute(1000, 0, 100, 0);
        Assert.Equal(0, t.CommuterTarget); Assert.Equal(0, t.PoolTarget); Assert.False(t.CommuterClamped); Assert.False(t.PoolClamped);
        // Weekend weighting averages the census probability over a week.
        Assert.Equal(PopulationCensusPolicy.DayVisitProbabilityPercent * 100 * 7,
            4 * PopulationCensusPolicy.DayVisitWeekdayBasisPoints + 3 * PopulationCensusPolicy.DayVisitWeekendBasisPoints);
    }

    // ---- Growth and convergence ----

    [Fact]
    public void FillConvergesToEachClampedTargetInBoundedUnitsWithoutOvershoot()
    {
        var (state, market) = City(12, 1);
        Assert.Empty(state.Visitors);
        var fill = Catalogue(12, Base, Since(3));
        var city = Start(state, market, fill);
        int boundaryDay = (int)(state.Minute / 1440) + 1;
        // Nothing is created before the next day boundary.
        Advance(city, boundaryDay * 1440 - 1);
        Assert.Empty(state.Visitors); Assert.Equal(0, city.PopulationFillUnits);
        long previousCommuters = 0, previousPool = 0;
        for (int day = boundaryDay; day < boundaryDay + 5; day++)
        {
            Advance(city, day * 1440 + 1);
            var targets = city.PopulationFillTargets!.Value;
            Assert.Equal(day * 1440.0, city.PopulationFillBoundary);
            var (commuters, pool) = Counts(state);
            Assert.True(commuters >= previousCommuters && pool >= previousPool, "a pattern count decreased");
            // Each pattern reaches its clamped target unless the aggregate capacity stops it; never beyond.
            Assert.True(commuters + pool <= targets.VisitorCapacity);
            Assert.Equal(Math.Max(previousCommuters, targets.CommuterTarget), commuters);
            Assert.Equal(Math.Max(previousPool, Math.Min(targets.PoolTarget, targets.VisitorCapacity - commuters)), pool);
            Assert.True(city.LastFillUnitPersons <= PopulationFillPolicy.MaximumPersonsPerUnit);
            Invariants(state, "day " + day); Conserved(state, "day " + day);
            previousCommuters = commuters; previousPool = pool;
        }
        Assert.True(previousCommuters > 0 && previousPool > 0);
        Assert.True(city.PopulationFillUnits >= (previousCommuters + previousPool + 63) / 64);
        // Day visitors carry their outside employer exactly like unseated commuter workers; they never hold a city seat.
        Assert.All(state.VisitorRoutines.Where(r => r.Person!.Stay.Pattern == VisitorPattern.DayVisit),
            r => Assert.Equal(ResidentLifecycle.IsWorker(r) && r.Identity.Age >= 18 ? ResidentIncomeSource.ExternalEmployment : (ResidentIncomeSource?)null, r.Carried));
        Assert.True(city.OccupancyMatchesFullCount());
    }

    [Fact]
    public void FillStopsExactlyAtTheAggregateCapacity()
    {
        // Commuter demand far above 2R: commuters take the whole capacity and the pool none.
        var (state, market) = City(12, 1);
        var city = Start(state, market, Catalogue(12, Base, Since(60)));
        Advance(city, 3 * 1440 + 1);
        var targets = city.PopulationFillTargets!.Value;
        Assert.True(targets.CommuterClamped);
        Assert.Equal(2L * state.Citizens.Count, targets.VisitorCapacity);
        Assert.Equal((targets.VisitorCapacity, 0L), Counts(state));
        Invariants(state, "capacity");

        // One or two below capacity (whichever the even band capacities reach) leaves exactly that many pool places.
        var (second, market2) = City(12, 1);
        long cap = 2L * second.Citizens.Count, gap = 1;
        var near = TryCatalogueFor(second, 12, cap - gap);
        if (near == null) { gap = 2; near = TryCatalogueFor(second, 12, cap - gap); }
        city = Start(second, market2, near!.Value.Catalogue);
        Advance(city, 2 * 1440 + 1);
        Assert.Equal((cap - gap, gap), Counts(second));
        Assert.Equal(gap, city.PopulationFillTargets!.Value.PoolTarget);
        Assert.Equal(cap, second.Visitors.Count);
    }

    [Fact]
    public void NoDiscoveredPortalCreatesNoVisitor()
    {
        var (state, market) = City(12, 2, portals: false);
        Assert.Empty(state.Portals); Assert.Empty(state.Visitors);
        var city = Start(state, market, Catalogue(12, Base, Since(20)));
        Advance(city, 4 * 1440);
        Assert.Empty(state.Visitors); Assert.Equal(0, city.PopulationFillCreated);
        Assert.Equal(0, city.PopulationFillTargets!.Value.CommuterTarget); Assert.Equal(0, city.PopulationFillTargets!.Value.PoolTarget);
        Assert.DoesNotContain(state.Places, PortalPlaces.Is);
    }

    [Fact]
    public void WithoutAnAttachedCatalogueNothingIsFilled()
    {
        var (state, market) = City(12, 1);
        var city = Start(state, market, null);
        Advance(city, 4 * 1440);
        Assert.Empty(state.Visitors); Assert.False(city.PopulationFillAttached);
        // A refused catalogue (duplicate place) detaches the fill too.
        var places = Places(12, Base); places.Add(places[0]);
        var refused = new PopulationFillCatalogue(places, Schema28Tests.Workplaces(Base).Census);
        Assert.True(refused.Refused);
        city.AttachPopulationFill(refused);
        Assert.False(city.PopulationFillAttached);
    }

    // ---- Retention: above target and shrinking census ----

    [Fact]
    public void FreshCitySeededAboveTargetCreatesNoCommutersAndRetiresNone()
    {
        // Factor 2 seeds one commuter per resident, far above this catalogue's commuter target.
        var (state, market) = City(12, 2);
        var seeded = state.Visitors.Select(v => v.Id).ToList();
        var wallets = state.Food!.Residents.ToDictionary(m => m.ResidentId, m => m.Budget!.OpeningBalance);
        var city = Start(state, market, Catalogue(12, Base));
        Advance(city, 3 * 1440 + 1);
        var targets = city.PopulationFillTargets!.Value;
        Assert.True(seeded.Count > targets.CommuterTarget);
        var (commuters, pool) = Counts(state);
        Assert.Equal(seeded.Count, commuters);
        Assert.All(seeded, id => Assert.Contains(state.Visitors, v => v.Id == id));
        // The pool fills toward its own target inside the remaining aggregate capacity.
        Assert.Equal(Math.Min(targets.PoolTarget, targets.VisitorCapacity - commuters), pool);
        Assert.True(pool > 0);
        foreach (var (id, opening) in wallets) Assert.Equal(opening, state.Food.Residents.Single(m => m.ResidentId == id).Budget!.OpeningBalance);

        // Factor 3 seeds the whole capacity: nothing is created and nothing removed.
        var (full, market3) = City(12, 3);
        int fullCount = full.Visitors.Count;
        city = Start(full, market3, Catalogue(12, Base, Since(20)));
        Advance(city, 3 * 1440 + 1);
        Assert.Equal(2 * full.Citizens.Count, fullCount);
        Assert.Equal(fullCount, full.Visitors.Count); Assert.Equal(0, city.PopulationFillCreated);
        Invariants(full, "factor 3");
    }

    [Fact]
    public void ShrinkingCensusKeepsAThousandCommutersAndPausesOnlyThatPattern()
    {
        const int homes = 90;
        // A seed whose resident worker count lets the even band capacities reach both 1,000 and 700 exactly.
        CityState state = null!; Schema27Tests.Market market = null!; PopulationFillCatalogue thousand = null!;
        Schema28Tests.Business[] own = Base;
        for (int extra = 0; ; extra++)
        {
            // Small businesses of the city itself change both the capacity and the seated resident workers.
            own = Base.Concat(Enumerable.Range(0, extra).Select(i => new Schema28Tests.Business("address:own:" + i, 30))).ToArray();
            (state, market) = City(homes, 1, seed: 1234, businesses: own);
            var found = TryCatalogueFor(state, homes, 1000, own);
            if (found != null && TryCatalogueFor(state, homes, 700, own) != null) { thousand = found.Value.Catalogue; break; }
            Assert.True(extra < 8, "no city reaches both targets");
        }
        Assert.True(2 * state.Citizens.Count > 1000);
        var city = Start(state, market, thousand);
        Advance(city, 2 * 1440 + 1);
        var (commuters, pool) = Counts(state);
        Assert.Equal(1000, commuters);
        long capacity = city.PopulationFillTargets!.Value.VisitorCapacity;
        Assert.Equal(Math.Min(city.PopulationFillTargets!.Value.PoolTarget, capacity - 1000), pool);
        var ids = state.Visitors.Select(v => v.Id).ToList();
        var wallets = state.Food!.Residents.Where(m => m.ResidentId.StartsWith("pv:")).ToDictionary(m => m.ResidentId, m => m.Budget!.OpeningBalance);

        // The census falls to 700 commuters: attached at noon, it applies only at the next boundary.
        Advance(city, 2 * 1440 + 720);
        var seven = TryCatalogueFor(state, homes, 700, own)!.Value.Catalogue;
        city.AttachPopulationFill(seven);
        Assert.Equal(1000, city.PopulationFillTargets!.Value.CommuterTarget);
        Advance(city, 3 * 1440 - 1);
        Assert.Equal(1000, city.PopulationFillTargets!.Value.CommuterTarget);
        Advance(city, 3 * 1440 + 1);
        var shrunk = city.PopulationFillTargets!.Value;
        Assert.Equal(700, shrunk.CommuterTarget);
        // Every identity and wallet is kept; commuters stay above target; the pool fills only inside the remaining capacity.
        var after = Counts(state);
        Assert.Equal(1000, after.Commuters);
        Assert.Equal(Math.Min(shrunk.PoolTarget, capacity - 1000), after.Pool);
        Assert.True(shrunk.PoolTarget > after.Pool, "the pool's own target is above what the aggregate capacity allows");
        Assert.All(ids, id => Assert.Contains(state.Visitors, v => v.Id == id));
        foreach (var (id, opening) in wallets) Assert.Equal(opening, state.Food.Residents.Single(m => m.ResidentId == id).Budget!.OpeningBalance);
        Invariants(state, "shrunk"); Conserved(state, "shrunk");
        // A reload keeps every person and the pause.
        city = Reload(city, market, seven);
        Advance(city, 5 * 1440 + 1);
        Assert.Equal(1000, Counts(city.State).Commuters);
        var kept = ids.ToHashSet(StringComparer.Ordinal);
        Assert.Equal(ids.Count, city.State.Visitors.Count(v => kept.Contains(v.Id)));
        Invariants(city.State, "reloaded"); Conserved(city.State, "reloaded");
    }

    // ---- Determinism ----

    private static string Run(Action<CitySimulator>? perFrame, int budget, bool frameSlices, bool reloadMidFill, bool reloadDaily, bool shuffled, int days = 4)
    {
        var (state, market) = City(12, 1, seed: 77);
        PopulationFillCatalogue Fill()
        {
            var since = Since(3, 1);
            var places = Places(12, Base.Concat(since)); var census = Schema28Tests.Workplaces(Base.Concat(since)).Census;
            if (shuffled) { places = places.OrderBy(p => StableRandom.Hash(p.Id, 3)).ToList(); census = census.OrderBy(w => StableRandom.Hash(w.WorkPlaceId, 3)).ToList(); }
            return new PopulationFillCatalogue(places, census);
        }
        var fill = Fill();
        var city = Start(state, market, fill);
        double end = (2 + days) * 1440 + 30;
        long units = 0; int reloads = 0, reloadedDay = -1;
        while (true)
        {
            bool caught;
            if (frameSlices) { var r = city.AdvanceFrameSlice(end, budget); caught = r.CaughtUp; }
            else caught = city.AdvanceTo(end, budget).CaughtUp;
            perFrame?.Invoke(city);
            if (reloadMidFill && city.PopulationFillUnits != units && city.PopulationFillUnits % 2 == 1 && city.State.Minute % 1440 == 0)
            { city = Reload(city, market, Fill()); reloads++; units = 0; continue; }
            units = city.PopulationFillUnits;
            int today = (int)(city.State.Minute / 1440);
            if (reloadDaily && !caught && today > reloadedDay && city.State.Minute >= today * 1440 + 720)
            { city = Reload(city, market, Fill()); reloads++; reloadedDay = today; }
            if (caught) break;
        }
        city.SynchronizeSnapshot();
        if (reloadMidFill) Assert.True(reloads > 0, "no reload happened inside a fill");
        if (reloadDaily) Assert.True(reloads >= days, "a daily reload was skipped");
        Invariants(city.State, "run"); Conserved(city.State, "run");
        return SaveCodec.Write(city.State) + "\n" + string.Join(",", market.Orders);
    }

    [Fact]
    public void CreationOrderIdsAndDrawsAreIndependentOfBudgetsCadenceSkipReloadAndOrder()
    {
        string reference = Run(null, 100000, false, false, false, false);
        Assert.Contains("pv:1|", reference); Assert.Contains("pv:64|", reference); Assert.Contains("pv:65|", reference);
        Assert.Equal(reference, Run(null, 1, true, false, false, false));
        Assert.Equal(reference, Run(null, 7, true, false, false, false));
        Assert.Equal(reference, Run(null, 64, false, false, false, false));
        // Reload between bounded units inside a boundary's fill, and at noon each day.
        Assert.Equal(reference, Run(null, 1, true, true, false, false));
        Assert.Equal(reference, Run(null, 13, false, false, true, false));
        // Shuffled catalogue input order.
        Assert.Equal(reference, Run(null, 64, false, false, false, true));
    }

    [Fact]
    public void HiddenSkipCrossesEveryBoundaryAndEqualsDayByDay()
    {
        var (a, ma) = City(12, 1, seed: 9001); var (b, mb) = City(12, 1, seed: 9001);
        var fa = Catalogue(12, Base, Since(10)); var fb = Catalogue(12, Base, Since(10));
        var skip = Start(a, ma, fa); var daily = Start(b, mb, fb);
        Advance(skip, 9 * 1440 + 3, 1_000_000);
        for (int day = 2; day <= 9; day++) Advance(daily, day * 1440 + 1, 16);
        Advance(daily, 9 * 1440 + 3, 16);
        Assert.Equal(SaveCodec.Write(b), SaveCodec.Write(a));
        Assert.True(skip.PopulationFillUnits > 0);
    }

    [Fact]
    public void ReloadExactlyAtTheBoundaryBeforeAndAfterTheFillGivesTheSameCity()
    {
        var (a, ma) = City(12, 1, seed: 31); var (b, mb) = City(12, 1, seed: 31);
        var fill = Catalogue(12, Base, Since(30));
        var plain = Start(a, ma, fill);
        Advance(plain, 3 * 1440 + 60);
        var reloaded = Start(b, mb, fill);
        // Caught up to the boundary minute itself: the fill has run (the boundary is work at its own minute).
        Advance(reloaded, 2 * 1440);
        Assert.True(reloaded.PopulationFillCreated > 0);
        Assert.Equal(2 * 1440.0, reloaded.State.Minute);
        reloaded = Reload(reloaded, mb, fill);
        Advance(reloaded, 2 * 1440);
        Assert.Equal(0, reloaded.PopulationFillCreated);
        reloaded = Reload(reloaded, mb, fill);
        Advance(reloaded, 3 * 1440 + 60);
        Assert.Equal(SaveCodec.Write(a), SaveCodec.Write(reloaded.State));
    }

    // ---- Enrolment ----

    [Fact]
    public void EveryCreatedPersonIsEnrolledOnceWithItsPortalDistrictMix()
    {
        var (state, market) = City(12, 1);
        // Give the homes two districts with distinct budget mixes; portals take the district of their nearest saved place.
        foreach (var place in state.Places.Where(p => p.Kind == PlaceKind.Home)) place.District = place.Position.X < 0 ? "west" : "east";
        foreach (var place in state.Places.Where(p => p.Kind != PlaceKind.Home && !PortalPlaces.Is(p))) place.District = "";
        var nearest = new Dictionary<string, string>();
        foreach (var portal in state.Places.Where(PortalPlaces.Is))
        {
            var best = state.Places.Where(p => !PortalPlaces.Is(p)).OrderBy(p => Position.Distance(p.Position, portal.Position)).ThenBy(p => p.Id, StringComparer.Ordinal).First();
            portal.District = best.District; nearest[portal.Id] = best.District;
        }
        state.Food!.Districts = new List<FoodDistrictBudgetMix> { new FoodDistrictBudgetMix { DistrictId = "west", WorkingPercent = 70, MiddlePercent = 20, UpperPercent = 10, Evidence = "test" } };
        var city = Start(state, market, Catalogue(12, Base, Since(2)));
        double boundary = 2 * 1440;
        Advance(city, boundary + 1);
        Assert.NotEmpty(state.Visitors);
        foreach (var routine in state.VisitorRoutines)
        {
            var meal = state.Food.Residents.Single(m => m.ResidentId == routine.Identity.Id);
            var district = state.Places.Single(p => p.Id == routine.HomeId).District;
            Assert.Equal(nearest[routine.HomeId], district);
            var expected = HouseholdFinance.Create(routine, state.Seed, boundary, state.Food.Districts.SingleOrDefault(d => d.DistrictId == district), meal.ResidentId);
            Assert.Equal(expected.OpeningBalance, meal.Budget!.OpeningBalance);
            Assert.Equal(expected.BaseIncomeCents, meal.Budget.BaseIncomeCents);
            Assert.Equal(expected.DailyHousingCents, meal.Budget.DailyHousingCents);
            Assert.Equal(expected.DailyEssentialsCents, meal.Budget.DailyEssentialsCents);
            Assert.Equal(meal.ResidentId, meal.HostId); Assert.Equal(0, meal.Slot); Assert.Null(meal.Excursion);
        }
        Assert.Contains(state.VisitorRoutines, r => state.Places.Single(p => p.Id == r.HomeId).District == "west");
        Assert.Contains(state.VisitorRoutines, r => state.Places.Single(p => p.Id == r.HomeId).District != "west");
        // A reload never re-seeds a wallet or meal record; later boundaries enrol only newly created persons.
        var before = state.Food.Residents.ToDictionary(m => m.ResidentId, m => m.Budget!.OpeningBalance);
        city = Reload(city, market, Catalogue(12, Base, Since(14)));
        Advance(city, 4 * 1440 + 1);
        foreach (var (id, opening) in before) Assert.Equal(opening, city.State.Food!.Residents.Single(m => m.ResidentId == id).Budget!.OpeningBalance);
        Invariants(city.State, "after reload"); Conserved(city.State, "after reload");
    }

    [Fact]
    public void FinancesOffEnrolsMealsWithoutWalletsAndAFoodlessCityCreatesNoVisitor()
    {
        var (state, market) = City(12, 1, finances: false);
        var city = Start(state, market, Catalogue(12, Base, Since(10)));
        Advance(city, 2 * 1440 + 1);
        Assert.NotEmpty(state.Visitors);
        Invariants(state, "finances off");
        Assert.All(state.Food!.Residents, m => Assert.Null(m.Budget));
        city = Reload(city, market, Catalogue(12, Base, Since(10)));
        Advance(city, 3 * 1440 + 1);
        Invariants(city.State, "finances off reload");

        // Without a food roster the current writer refuses visitors, so the fill creates none.
        var foodless = Population.CreateFromHousing(Schema28Tests.Donors, Places(12, Base), "f5g-fill", "0.41.1", 5150, 1440 + 360);
        PortalPlaces.Add(foodless, Schema27Tests.Stations);
        var plain = new CitySimulator(foodless, new FreshCityTraceTests.Walk());
        plain.AttachPopulationFill(Catalogue(12, Base, Since(10)));
        Advance(plain, 4 * 1440);
        Assert.Empty(foodless.Visitors);
        Assert.True(SaveCodec.Read(SaveCodec.Write(foodless)).Status == SaveReadStatus.Loaded);
    }

    // ---- Target timing ----

    [Fact]
    public void CatalogueChangesMoveTheTargetOnlyAtTheNextBoundaryAndAnExistingCityStartsFillingThere()
    {
        // An existing current-schema city ran two days without fill (as before F5g), then is saved and reloaded.
        var (state, market) = City(12, 1);
        var old = Start(state, market, null);
        Advance(old, 3 * 1440 + 600);
        Assert.Empty(state.Visitors);
        int schema = state.Schema;
        var city = Reload(old, market, Catalogue(12, Base, Since(5)));
        // Fill starts at its next day boundary, with no schema change or switch-over.
        Advance(city, 4 * 1440 - 1);
        Assert.Empty(city.State.Visitors);
        Advance(city, 4 * 1440 + 1);
        Assert.NotEmpty(city.State.Visitors);
        Assert.Equal(schema, city.State.Schema); Assert.Equal(CityState.CurrentSchema, city.State.Schema);
        var first = city.PopulationFillTargets!.Value;
        int count = city.State.Visitors.Count;
        // A larger catalogue attached mid-day creates nobody until the next boundary.
        city.AttachPopulationFill(Catalogue(12, Base, Since(25)));
        Advance(city, 5 * 1440 - 1);
        Assert.Equal(count, city.State.Visitors.Count);
        Assert.Equal(first.CommuterTarget, city.PopulationFillTargets!.Value.CommuterTarget);
        Advance(city, 5 * 1440 + 1);
        Assert.True(city.PopulationFillTargets!.Value.CommuterTarget > first.CommuterTarget);
        Assert.True(city.State.Visitors.Count > count);
        // A catalogue attached at the boundary after its plan latched keeps the latched targets for that boundary.
        var latched = city.PopulationFillTargets!.Value;
        city.AttachPopulationFill(Catalogue(12, Base, Since(60)));
        Assert.Equal(latched.CommuterTarget, city.PopulationFillTargets!.Value.CommuterTarget);
    }

    // ---- Stays over weeks ----

    [Fact]
    public void FourWeeksOfStaysReturnTheSamePersonsAndConserveEverythingThroughReloads()
    {
        var (a, ma) = City(12, 1, seed: 4242); var (b, mb) = City(12, 1, seed: 4242);
        // Lost acknowledgements for some visitor meals keep receipts uncertain; evidence is withheld for a while.
        Func<ResidentMeal, bool> lose = m => m.ResidentId.StartsWith("pv:") && StableRandom.Hash(m.IntentId, 11) % 9 == 0;
        ma.LoseAcknowledgement = lose; mb.LoseAcknowledgement = lose;
        var fill = Catalogue(12, Base, Since(2));
        var plain = Start(a, ma, fill); var city = Start(b, mb, fill);
        double start = b.Minute, end = 2 * 1440 + 29 * 1440;
        var seen = new Dictionary<string, VisitorPattern>(StringComparer.Ordinal);
        var stayDays = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
        int reloads = 0, deferralReloads = 0, dormantUncertainReloads = 0, arrivalReloads = 0;
        long lastCommuters = 0, lastPool = 0;
        for (double minute = start + 60; minute <= end; minute += 60)
        {
            Advance(plain, minute); Advance(city, minute);
            var state = city.State;
            int day = (int)(minute / 1440);
            // Evidence returns every third day; uncertain receipts then settle exactly once (the market refuses a second order).
            ma.EvidenceAvailable = mb.EvidenceAvailable = day % 3 == 0;
            var (commuters, pool) = Counts(state);
            Assert.True(commuters >= lastCommuters && pool >= lastPool, "a pattern count decreased at " + minute);
            if (commuters > lastCommuters) Assert.True(lastCommuters < city.PopulationFillTargets!.Value.CommuterTarget);
            if (pool > lastPool) Assert.True(lastPool < city.PopulationFillTargets!.Value.PoolTarget);
            lastCommuters = commuters; lastPool = pool;
            foreach (var visitor in state.Visitors)
            {
                if (seen.TryGetValue(visitor.Id, out var pattern)) Assert.Equal(pattern, visitor.Stay.Pattern);
                else seen[visitor.Id] = visitor.Stay.Pattern;
                if (visitor.Stay.Presence == VisitorPresence.Active)
                    (stayDays.TryGetValue(visitor.Id, out var days) ? days : stayDays[visitor.Id] = new HashSet<int>()).Add(day);
            }
            Assert.All(seen.Keys, id => Assert.Contains(state.Visitors, v => v.Id == id));
            foreach (var meal in state.Food!.Residents)
            {
                var visitor = state.Visitors.FirstOrDefault(v => v.Id == meal.ResidentId);
                if (visitor == null || visitor.Stay.Presence != VisitorPresence.Dormant) continue;
                // No demand while dormant: no travelling or serving meal and no open decision.
                Assert.False(meal.Status == MealStatus.Travelling || meal.Status == MealStatus.Serving, meal.ResidentId + " has dormant demand");
                Assert.Null(meal.Decision);
                // Daily postings continue for dormant persons.
                if (minute % 1440 == 60) Assert.Equal(day, meal.Budget!.LastPostedDay);
            }
            Invariants(state, "hour " + minute); Conserved(state, "hour " + minute);
            // Reloads at an arrival, mid-stay, a departure deferral (meal outing at stay end), dormancy with an uncertain
            // receipt and the next arrival; plus a fixed noon reload each week.
            bool arrival = state.Visitors.Any(v => v.LastArrival != null && v.LastArrival.AtMinute > minute - 60 && v.Stay.Pattern == VisitorPattern.DayVisit);
            bool deferral = state.VisitorRoutines.Any(r => r.MealOuting != null);
            bool dormantUncertain = state.Food.Residents.Any(m => m.Status == MealStatus.Uncertain
                && state.Visitors.Any(v => v.Id == m.ResidentId && v.Stay.Presence == VisitorPresence.Dormant));
            if (arrival && arrivalReloads < 6 || deferral && deferralReloads < 6 || dormantUncertain && dormantUncertainReloads < 6 || minute % (7 * 1440) == 720)
            {
                if (arrival) arrivalReloads++; if (deferral) deferralReloads++; if (dormantUncertain) dormantUncertainReloads++;
                city = Reload(city, mb, fill); reloads++;
            }
        }
        Assert.Equal(SaveCodec.Write(a), SaveCodec.Write(city.State));
        Assert.Equal(ma.Orders, mb.Orders);
        Assert.True(arrivalReloads > 0 && deferralReloads > 0 && dormantUncertainReloads > 0, $"reload coverage {arrivalReloads}/{deferralReloads}/{dormantUncertainReloads}");
        // The same pool members return on several days across the weeks; commuters return on weekdays.
        var poolIds = seen.Where(p => p.Value == VisitorPattern.DayVisit).Select(p => p.Key).ToList();
        Assert.NotEmpty(poolIds);
        Assert.True(poolIds.Count(id => stayDays.TryGetValue(id, out var d) && d.Count >= 2) > poolIds.Count / 2, "pool members do not return");
        Assert.Contains(seen.Where(p => p.Value == VisitorPattern.Commuter), p => stayDays.TryGetValue(p.Key, out var d) && d.Count >= 10);
        // Day visits are weighted to the weekend: more pool presence on weekend days than on weekdays.
        int Weekday(int d) => ((d - 1) % 7 + 7) % 7;
        double weekend = poolIds.Sum(id => stayDays.TryGetValue(id, out var d) ? d.Count(x => Weekday(x) >= 4) : 0) / 12.0;
        double weekdays = poolIds.Sum(id => stayDays.TryGetValue(id, out var d) ? d.Count(x => Weekday(x) < 4) : 0) / 16.0;
        Assert.True(weekend > weekdays, $"weekend {weekend} weekday {weekdays}");
        // No uncertain receipt was ever ordered twice (the market asserts that) and every one was settled by the end.
        Assert.True(mb.Orders.Count > 0);
    }

    [Fact]
    public void DayVisitDrawsFollowTheirSeededDays()
    {
        var (state, market) = City(12, 1);
        var city = Start(state, market, Catalogue(12, Base));
        Advance(city, 2 * 1440 + 1);
        var pool = state.VisitorRoutines.Where(r => r.Person!.Stay.Pattern == VisitorPattern.DayVisit).ToList();
        Assert.NotEmpty(pool);
        var visits = new Dictionary<string, HashSet<int>>();
        for (double minute = 2 * 1440 + 60; minute <= 23 * 1440; minute += 30)
        {
            Advance(city, minute);
            int day = (int)(minute / 1440);
            foreach (var r in pool)
                if (r.Person!.Stay.Presence == VisitorPresence.Active)
                {
                    // A pool member is present only on a seeded visit day (or finishing that day's stay after midnight).
                    Assert.True(VisitorSchedules.VisitsOn(r, state.Seed, day) || VisitorSchedules.VisitsOn(r, state.Seed, day - 1), r.Identity.Id + " present on day " + day);
                    (visits.TryGetValue(r.Identity.Id, out var d) ? d : visits[r.Identity.Id] = new HashSet<int>()).Add(day);
                }
        }
        Assert.True(visits.Count > pool.Count / 2);
    }

    [Fact]
    public void NoPoolMemberStaysDormantForGoodOverTenWeeks()
    {
        // Review r1-1: a member whose drawn visit days all fall outside the routine's nine-day window once got a Home span to
        // the clock limit and never woke again. It now wakes at the window end and looks further ahead.
        var (state, market) = City(12, 1, seed: 606);
        var city = Start(state, market, Catalogue(12, Base));
        Advance(city, 2 * 1440 + 1);
        var pool = state.VisitorRoutines.Where(r => r.Person!.Stay.Pattern == VisitorPattern.DayVisit).ToList();
        Assert.True(pool.Count >= 30);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        for (double minute = 2 * 1440 + 60; minute <= 72 * 1440; minute += 60)
        {
            Advance(city, minute);
            if (minute % (14 * 1440) == 720) city = Reload(city, market, Catalogue(12, Base));
            foreach (var routine in city.State.VisitorRoutines.Where(r => r.Person!.Stay.Pattern == VisitorPattern.DayVisit))
            {
                Assert.True(routine.NextEventMinute <= minute + 10 * 1440, routine.Identity.Id + " sleeps until " + routine.NextEventMinute);
                if (routine.Person!.Stay.Presence == VisitorPresence.Active) visited.Add(routine.Identity.Id);
            }
        }
        Assert.All(pool, r => Assert.Contains(r.Identity.Id, visited));
    }

    // ---- Transport ----

    /// <summary>A planner double with an endpoint-keyed bounded geometry cache like Runtime's WalkingRoutes (1,024 entries,
    /// first-in first-out, negative entries kept). Each request returns its own copy, so no two persons share a route,
    /// queue position or choice; only geometry and timing are shared.</summary>
    internal sealed class CachedPortalPlanner : IRoutePlanner
    {
        private readonly Dictionary<string, Route?> cache = new(StringComparer.Ordinal);
        private readonly Queue<string> order = new();
        public Func<Place, Place, bool> Reachable = (_, _) => true;
        public long PortalRequests, PortalHits, PortalMisses, Requests;
        public Route? Plan(Place from, Place to, double at, TravelPreferences preferences)
        {
            Requests++;
            bool portal = PortalPlaces.Is(from) || PortalPlaces.Is(to);
            string key = from.Id + "\n" + to.Id;
            if (!cache.TryGetValue(key, out var geometry))
            {
                if (portal) { PortalRequests++; PortalMisses++; }
                geometry = Reachable(from, to) ? new Route { Legs = new() { new RouteLeg { From = from.Position, To = to.Position, Mode = TravelMode.Walk,
                    Minutes = Math.Max(1, Position.Distance(from.Position, to.Position) / 80) } } } : null;
                if (cache.Count >= 1024) cache.Remove(order.Dequeue());
                cache.Add(key, geometry); order.Enqueue(key);
            }
            else if (portal) { PortalRequests++; PortalHits++; }
            if (geometry == null) return null;
            return new Route { Legs = geometry.Legs.Select(l => new RouteLeg { From = l.From, To = l.To, Mode = l.Mode, Minutes = l.Minutes }).ToList() };
        }
    }

    [Fact]
    public void PortalRoutesShareCachedGeometryButNeverRoutesAndUnreachablePortalsNeverArrive()
    {
        var (state, market) = City(12, 1);
        var planner = new CachedPortalPlanner();
        var city = Start(state, market, Catalogue(12, Base, Since(2)), planner);
        Advance(city, 2 * 1440 + 1);
        var journeys = new List<Route>();
        for (double minute = 2 * 1440 + 30; minute <= 4 * 1440; minute += 15)
        {
            Advance(city, minute);
            var active = state.VisitorRoutines.Where(r => r.Journey != null).Select(r => r.Journey!).ToList();
            Assert.Equal(active.Count, active.Distinct(ReferenceEqualityComparer.Instance).Count());
            Assert.Equal(active.Count, active.Select(r => r.Legs).Distinct(ReferenceEqualityComparer.Instance).Count());
            journeys.AddRange(active);
            Assert.True(city.ActiveJourneyIndexMatchesState());
            // No visitor is ever a body candidate or receives a lease.
            Assert.All(state.VisitorRoutines.Where(r => r.Journey != null), r => Assert.False(city.TryAcquirePhysicalJourney(r.Identity.Id, out _)));
        }
        Assert.True(planner.PortalHits > 0 && planner.PortalMisses > 0, $"hits {planner.PortalHits} misses {planner.PortalMisses}");
        Assert.True(planner.PortalHits > planner.PortalMisses);
        Invariants(state, "cached");

        // Every portal route refused: created visitors wait at their portals, retry, never arrive and never buy.
        var (blocked, blockedMarket) = City(12, 1);
        var refusing = new CachedPortalPlanner { Reachable = (from, to) => !PortalPlaces.Is(from) && !PortalPlaces.Is(to) };
        city = Start(blocked, blockedMarket, Catalogue(12, Base, Since(10)), refusing);
        for (double minute = blocked.Minute + 60; minute <= 6 * 1440; minute += 60)
        {
            Advance(city, minute);
            if (minute == 4 * 1440) city = Reload(city, blockedMarket, Catalogue(12, Base, Since(10)), refusing);
            Assert.All(city.State.Visitors, v => { Assert.Equal(VisitorPresence.Dormant, v.Stay.Presence); Assert.Null(v.LastArrival); });
            Assert.All(city.State.VisitorRoutines, r => Assert.Equal(0, r.CompletedJourneys));
        }
        Assert.NotEmpty(city.State.Visitors);
        Assert.True(refusing.PortalRequests > 0);
        Assert.DoesNotContain(blockedMarket.Orders, o => o.Contains("pv%3A") || o.Contains("pv:"));
        Assert.All(city.State.Food!.Residents.Where(m => m.ResidentId.StartsWith("pv:")), m => Assert.Equal(0, m.Purchases));
        Conserved(city.State, "blocked");
    }

    [Fact]
    public void DayVisitorShopArrivalAdmitsServiceThroughReloadAndNeverFabricatesIt()
    {
        var (state, market) = City(12, 1, seed: 808);
        var city = Start(state, market, Catalogue(12, Base));
        Advance(city, 2 * 1440 + 1);
        var poolIds = state.Visitors.Where(v => v.Stay.Pattern == VisitorPattern.DayVisit).Select(v => v.Id).ToHashSet();
        Assert.NotEmpty(poolIds);
        int checks = 0;
        for (double minute = 2 * 1440 + 15; minute <= 9 * 1440; minute += 15)
        {
            Advance(city, minute);
            if ((int)(minute / 15) % 41 == 0) city = Reload(city, market, Catalogue(12, Base));
            foreach (var meal in city.State.Food!.Residents.Where(m => poolIds.Contains(m.ResidentId) && m.Status == MealStatus.Serving && m.TargetKind == FoodAlternativeKind.Player))
            {
                var visitor = city.State.Visitors.Single(v => v.Id == meal.ResidentId);
                // Service was admitted only through a Shop arrival at the meal's place no earlier than its desired minute.
                Assert.NotNull(visitor.LastArrival);
                Assert.Equal(meal.PlaceId, visitor.LastArrival!.PlaceId);
                Assert.Equal(Activity.Shop, visitor.LastArrival.Activity);
                Assert.True(visitor.LastArrival.AtMinute >= meal.DesiredAt);
                checks++;
            }
        }
        Assert.True(checks > 0, "no day visitor was served at a player shop");
        Assert.Contains(market.Orders, o => poolIds.Any(id => o.Contains(Uri.EscapeDataString(id)) || o.Contains(id)));
    }

    // ---- Persistence ----

    [Fact]
    public void DayVisitorsRoundTripInWriter33AndMalformedDayVisitorsAreRefusedAndKept()
    {
        Assert.Equal(33, CityState.CurrentSchema);
        var (state, market) = City(12, 1);
        var city = Start(state, market, Catalogue(12, Base, Since(2)));
        Advance(city, 2 * 1440 + 600);
        Assert.Contains(state.Visitors, v => v.Stay.Pattern == VisitorPattern.DayVisit);
        string payload = SaveCodec.Write(state);
        Assert.Contains("\"Schema\":33", payload);
        var read = SaveCodec.Read(payload);
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(payload, SaveCodec.Write(read.State!));

        // A day visitor with an Employment is refused on write and on read; the payload stays byte-identical.
        var copy = SaveCodec.Read(payload).State!;
        var day = copy.Visitors.First(v => v.Stay.Pattern == VisitorPattern.DayVisit);
        var work = copy.Places.First(p => p.Kind == PlaceKind.Work && p.Capacity > 0);
        day.Routine!.DayPlaceId = work.Id; day.Routine.EmploymentStartDay = (int)(copy.Minute / 1440); day.Routine.Carried = null;
        VisitorSchedules.Sync(day);
        Assert.Throws<SaveValidationException>(() => SaveCodec.Write(copy));
        var seated = state.Visitors.First(v => v.Stay.Pattern == VisitorPattern.Commuter && v.Employment != null);
        var tuple = new System.Text.RegularExpressions.Regex("\\|" + System.Text.RegularExpressions.Regex.Escape(seated.Id) + "\\|(\\w{3})C");
        Assert.Matches(tuple, payload);
        string forged = tuple.Replace(payload, m => "|" + seated.Id + "|" + m.Groups[1].Value + "D", 1);
        var refused = SaveCodec.Read(forged);
        Assert.Equal(SaveReadStatus.Invalid, refused.Status); Assert.Contains("day visitor holds no employment", refused.Error);
        var data = new Dictionary<string, string> { [SaveStore.StateKey] = forged, ["rlo:state"] = "kept" };
        Assert.Equal(SaveReadStatus.Invalid, SaveSession.Open(data, state.PopulationKey).InitialReadResult.Status);
        Assert.False(SaveStore.WriteTo(data, state).Success);
        Assert.Same(forged, data[SaveStore.StateKey]); Assert.Equal("kept", data["rlo:state"]);
    }
}
