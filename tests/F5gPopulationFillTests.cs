using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class F5gPopulationFillTests
{

    internal static readonly WorkplaceCapacityTests.Business[] Base = WorkplaceCapacityTests.Businesses;

    internal static List<Place> Places(int homes, IEnumerable<WorkplaceCapacityTests.Business> businesses)
    {
        var places = Enumerable.Range(0, homes).Select(h => new Place { Id = "home:" + h.ToString("000"), Name = "Home " + h, Kind = PlaceKind.Home,
            Capacity = 7, ResidentialAreaSquareMetres = 160, Position = new Position(-60 + 25 * (h % 8), 0, -40 + 30 * (h / 8)) }).ToList();
        places.AddRange(WorkplaceCapacityTests.Workplaces(businesses).Places);
        places.Add(new Place { Id = "leisure:a", Name = "Park", Kind = PlaceKind.Leisure, Capacity = 64, Position = new Position(60, 0, -80) });
        places.AddRange(Enumerable.Range(0, 3).Select(i => new Place { Id = "shop:" + i, Name = "Shop " + i, Kind = PlaceKind.Shop,
            Capacity = 16, Position = new Position(20 + i * 30, 0, 15 - i * 5) }));
        return places;
    }

    internal static PopulationFillCatalogue Catalogue(int homes, IEnumerable<WorkplaceCapacityTests.Business> businesses, IEnumerable<WorkplaceCapacityTests.Business>? since = null)
    {
        var all = businesses.Concat(since ?? Array.Empty<WorkplaceCapacityTests.Business>()).ToList();
        var places = Places(homes, all);
        return new PopulationFillCatalogue(places, WorkplaceCapacityTests.Workplaces(all).Census);
    }

    internal static WorkplaceCapacityTests.Business[] Since(int large, int small = 0)
        => Enumerable.Range(0, large).Select(i => new WorkplaceCapacityTests.Business("address:new:" + i.ToString("000"), 900))
            .Concat(Enumerable.Range(0, small).Select(i => new WorkplaceCapacityTests.Business("address:newsmall:" + i, 30))).ToArray();

    internal static (PopulationFillCatalogue Catalogue, WorkplaceCapacityTests.Business[] Since) CatalogueFor(CityState state, int homes, long target)
        => TryCatalogueFor(state, homes, target, Base) ?? throw new InvalidOperationException("No catalogue gives commuter target " + target);

    internal static (PopulationFillCatalogue Catalogue, WorkplaceCapacityTests.Business[] Since)? TryCatalogueFor(CityState state, int homes, long target,
        IEnumerable<WorkplaceCapacityTests.Business>? saved = null)
    {
        var own = (saved ?? Base).ToArray();
        long previous = -1;
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

    internal static (CityState State, VisitorRoutineTests.Market Market) City(int homes = 12, int factor = 1, int seed = 5150, bool portals = true,
        bool finances = true, IEnumerable<WorkplaceCapacityTests.Business>? businesses = null)
    {
        var state = Population.CreateFromHousing(WorkplaceCapacityTests.Donors, Places(homes, businesses ?? Base), "f5g-fill", "0.41.1", seed, 1440 + 6 * 60);
        if (portals) PortalPlaces.Add(state, VisitorRoutineTests.Stations);
        state.Food = new FoodCommerceState { MealRhythmVersion = MealRhythm.PolicyVersion, HouseholdFinanceVersion = finances ? HouseholdFinance.PolicyVersion : 0,
            RepresentationFactor = factor,
            Shops = Enumerable.Range(0, 3).Select(i => new FoodScope { PlaceId = "shop:" + i, BusinessId = "b" + i, ProductId = FoodProducts.Croissant }).ToList() };
        var market = new VisitorRoutineTests.Market { Shops = Shops(state) };
        new CitySimulator(state, new FreshCityTraceTests.Walk()).AttachFoodMarket(market);
        return (state, market);
    }

    internal static FoodBusiness[] Shops(CityState state) => state.Food!.Shops.Select(s => new FoodBusiness { Id = s.BusinessId, PlaceId = s.PlaceId,
        Name = s.PlaceId, MealsPerHour = 30, Hours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 6 * 60, ToMinute = 22 * 60 }).ToList(),
        Offers = new() { new FoodOffer { ProductId = FoodProducts.Croissant, Price = Money.FromCents(250), Units = 100000 } } }).ToArray();

    internal static CitySimulator Start(CityState state, VisitorRoutineTests.Market market, PopulationFillCatalogue? fill, IRoutePlanner? routes = null)
    {
        var city = new CitySimulator(state, routes ?? new FreshCityTraceTests.Walk());
        city.AttachFoodMarket(market);
        city.AttachPopulationFill(fill);
        return city;
    }

    internal static CitySimulator Reload(CitySimulator city, VisitorRoutineTests.Market market, PopulationFillCatalogue? fill, IRoutePlanner? routes = null)
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

    internal static void Invariants(CityState state, string context)
    {
        var persons = state.Citizens.Select(c => c.Identity.Id).Concat(state.Visitors.Select(v => v.Id)).ToList();
        var roster = state.Food!.Residents.Select(m => m.ResidentId).ToList();
        Assert.Equal(persons.Count, roster.Count); Assert.Equal(persons.OrderBy(i => i, StringComparer.Ordinal), roster.OrderBy(i => i, StringComparer.Ordinal));
        Assert.All(state.Food.Residents, m => Assert.Equal(state.Food.HouseholdFinanceVersion != 0, m.Budget != null));
        for (int i = 1; i < state.Visitors.Count; i++) Assert.True(string.CompareOrdinal(state.Visitors[i - 1].Id, state.Visitors[i].Id) < 0, context);
        Assert.True(state.NextVisitorSequence - 1 >= state.Visitors.Count, context);
        var present = state.Visitors.Select(v => int.Parse(v.Id.Substring(3))).ToHashSet();
        var identities = IdentityPolicy.V1(state.SourceIdentities);
        for (int n = 1; n < state.NextVisitorSequence; n++)
            if (!present.Contains(n)) Assert.False(VisitorSchedules.WorkingPersona(identities.PersonaOf(state.Seed, PersonIds.Visitor(n))), context + ": pv:" + n);
        for (int i = 0; i < state.Visitors.Count; i++) Assert.Same(state.Visitors[i].Routine, state.VisitorRoutines[i]);
        Assert.All(state.Visitors.Where(v => v.Stay.Pattern == VisitorPattern.DayVisit), v => { Assert.Null(v.Employment); Assert.Equal("", v.Routine!.DayPlaceId); });
        Assert.All(state.VisitorRoutines, r => { Assert.Null(r.Car); Assert.False(r.Preferences.OwnsCar);
            Assert.True(r.Journey?.Legs == null || r.Journey.Legs.All(l => l.Mode != TravelMode.Car)); });
    }


    [Fact]
    public void FillTargetsEqualTheCensusOnTheSameFacts()
    {
        foreach (int seed in new[] { 5150, 77, 9001 })
            foreach (var since in new[] { Since(0), Since(3, 1), Since(40, 2) })
            {
                var (state, _) = City(12, 1, seed);
                var places = Places(12, Base.Concat(since));
                var census = WorkplaceCapacityTests.Workplaces(Base.Concat(since)).Census;
                var targets = new PopulationFillCatalogue(places, census).Targets(state);
                var reference = PopulationCensus.Compute(new PopulationCensusInput { Places = places, Workplaces = census,
                    Residents = PopulationCensusInput.CaptureResidents(state.Citizens), StationIds = state.Portals.ToList() });
                Assert.False(reference.Refused);
                Assert.Equal(reference.CommuterTarget, targets.CommuterTarget); Assert.Equal(reference.DayVisitorPoolTarget, targets.PoolTarget);
                Assert.Equal(reference.VisitorCapacity, targets.VisitorCapacity);
                Assert.Equal(reference.CommuterClamped, targets.CommuterClamped); Assert.Equal(reference.PoolClamped, targets.PoolClamped);
                var shuffled = new PopulationFillCatalogue(places.AsEnumerable().Reverse().ToList(), census.AsEnumerable().Reverse().ToList()).Targets(state);
                Assert.Equal(targets.CommuterTarget, shuffled.CommuterTarget); Assert.Equal(targets.PoolTarget, shuffled.PoolTarget);
            }
    }

    [Fact]
    public void TargetClampsAtExactBoundariesAndWithoutStations()
    {
        var t = VisitorTargets.Compute(workplaceCapacity: 1000, eligibleResidentWorkers: 0, residents: 100, stations: 2);
        Assert.Equal(200, t.VisitorCapacity); Assert.Equal(800, t.CommuterDemand); Assert.Equal(200, t.CommuterTarget); Assert.Equal(0, t.PoolTarget);
        Assert.True(t.CommuterClamped); Assert.True(t.PoolClamped);
        t = VisitorTargets.Compute(250, 0, 100, 2);
        Assert.Equal(200, t.CommuterDemand); Assert.Equal(200, t.CommuterTarget); Assert.False(t.CommuterClamped); Assert.Equal(0, t.PoolTarget);
        t = VisitorTargets.Compute(250, 1, 100, 2);
        Assert.Equal(199, t.CommuterTarget); Assert.Equal(1, t.PoolTarget); Assert.True(t.PoolClamped);
        t = VisitorTargets.Compute(0, 0, 100, 1);
        Assert.Equal(15, t.DayVisitorsPerDay); Assert.Equal(60, t.PoolDemand); Assert.Equal(60, t.PoolTarget); Assert.Equal(0, t.CommuterTarget);
        t = VisitorTargets.Compute(1_000_000, 0, 50_000, 3);
        Assert.Equal(100_000, t.VisitorCapacity); Assert.Equal(100_000, t.CommuterTarget); Assert.Equal(0, t.PoolTarget);
        t = VisitorTargets.Compute(1000, 0, 100, 0);
        Assert.Equal(0, t.CommuterTarget); Assert.Equal(0, t.PoolTarget); Assert.False(t.CommuterClamped); Assert.False(t.PoolClamped);
        Assert.Equal(PopulationCensusPolicy.DayVisitProbabilityPercent * 100 * 7,
            Enumerable.Range(0, 7).Sum(d => VisitorTable.BasisPoints(VisitorPattern.DayVisit, d)));
    }


    [Fact]
    public void FillConvergesToEachClampedTargetInBoundedUnitsWithoutOvershoot()
    {
        var (state, market) = City(12, 1);
        Assert.Empty(state.Visitors);
        var fill = Catalogue(12, Base, Since(3));
        var city = Start(state, market, fill);
        int boundaryDay = (int)(state.Minute / 1440) + 1;
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
            Assert.True(commuters + pool <= targets.VisitorCapacity);
            Assert.Equal(Math.Max(previousCommuters, targets.CommuterTarget), commuters);
            Assert.Equal(Math.Max(previousPool, Math.Min(targets.PoolTarget, targets.VisitorCapacity - commuters)), pool);
            Assert.True(city.LastFillUnitPersons <= PopulationFillPolicy.MaximumPersonsPerUnit);
            Invariants(state, "day " + day); Conserved(state, "day " + day);
            previousCommuters = commuters; previousPool = pool;
        }
        Assert.True(previousCommuters > 0 && previousPool > 0);
        Assert.True(city.PopulationFillUnits >= (previousCommuters + previousPool + 63) / 64);
        Assert.All(state.VisitorRoutines.Where(r => r.Person!.Stay.Pattern == VisitorPattern.DayVisit),
            r => Assert.Equal(ResidentLifecycle.IsWorker(r) && r.Identity.Age >= 18 ? ResidentIncomeSource.ExternalEmployment : (ResidentIncomeSource?)null, r.Carried));
        Assert.True(city.OccupancyMatchesFullCount());
        Assert.All(state.VisitorRoutines.Where(r => r.Person!.Stay.Pattern == VisitorPattern.Commuter), r => Assert.True(VisitorSchedules.WorkingPersona(r.Identity.Persona)));
        Assert.Contains(state.VisitorRoutines, r => r.Person!.Stay.Pattern == VisitorPattern.DayVisit && !VisitorSchedules.WorkingPersona(r.Identity.Persona));
        Assert.True(city.PopulationFillSkipped > 0);
        Assert.Equal(state.NextVisitorSequence - 1 - state.Visitors.Count, city.PopulationFillSkipped);
    }

    [Fact]
    public void FillStopsExactlyAtTheAggregateCapacity()
    {
        var (state, market) = City(12, 1);
        var city = Start(state, market, Catalogue(12, Base, Since(60)));
        Advance(city, 3 * 1440 + 1);
        var targets = city.PopulationFillTargets!.Value;
        Assert.True(targets.CommuterClamped);
        Assert.Equal(2L * state.Citizens.Count, targets.VisitorCapacity);
        Assert.Equal((targets.VisitorCapacity, 0L), Counts(state));
        Invariants(state, "capacity");

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
        var places = Places(12, Base); places.Add(places[0]);
        var refused = new PopulationFillCatalogue(places, WorkplaceCapacityTests.Workplaces(Base).Census);
        Assert.True(refused.Refused);
        city.AttachPopulationFill(refused);
        Assert.False(city.PopulationFillAttached);
    }


    [Fact]
    public void FreshCitySeededAboveTargetCreatesNoCommutersAndRetiresNone()
    {
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
        Assert.Equal(Math.Min(targets.PoolTarget, targets.VisitorCapacity - commuters), pool);
        Assert.True(pool > 0);
        foreach (var (id, opening) in wallets) Assert.Equal(opening, state.Food.Residents.Single(m => m.ResidentId == id).Budget!.OpeningBalance);

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
        CityState state = null!; VisitorRoutineTests.Market market = null!; PopulationFillCatalogue thousand = null!;
        WorkplaceCapacityTests.Business[] own = Base;
        for (int extra = 0; ; extra++)
        {
            own = Base.Concat(Enumerable.Range(0, extra).Select(i => new WorkplaceCapacityTests.Business("address:own:" + i, 30))).ToArray();
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
        Assert.Equal(Math.Min(Math.Min(city.PopulationFillTargets!.Value.PoolTarget, capacity - 1000), PopulationFillPolicy.MaximumPersonsPerBoundary - 1000), pool);
        var ids = state.Visitors.Select(v => v.Id).ToList();
        var wallets = state.Food!.Residents.Where(m => m.ResidentId.StartsWith("pv:")).ToDictionary(m => m.ResidentId, m => m.Budget!.OpeningBalance);

        Advance(city, 2 * 1440 + 720);
        var seven = TryCatalogueFor(state, homes, 700, own)!.Value.Catalogue;
        city.AttachPopulationFill(seven);
        Assert.Equal(1000, city.PopulationFillTargets!.Value.CommuterTarget);
        Advance(city, 3 * 1440 - 1);
        Assert.Equal(1000, city.PopulationFillTargets!.Value.CommuterTarget);
        Advance(city, 3 * 1440 + 1);
        var shrunk = city.PopulationFillTargets!.Value;
        Assert.Equal(700, shrunk.CommuterTarget);
        var after = Counts(state);
        Assert.Equal(1000, after.Commuters);
        Assert.Equal(Math.Min(shrunk.PoolTarget, capacity - 1000), after.Pool);
        Assert.True(shrunk.PoolTarget > after.Pool, "the pool's own target is above what the aggregate capacity allows");
        Assert.True(after.Pool > pool, "the pool grew inside the remaining aggregate capacity while commuters stayed paused");
        Assert.All(ids, id => Assert.Contains(state.Visitors, v => v.Id == id));
        foreach (var (id, opening) in wallets) Assert.Equal(opening, state.Food.Residents.Single(m => m.ResidentId == id).Budget!.OpeningBalance);
        Invariants(state, "shrunk"); Conserved(state, "shrunk");
        city = Reload(city, market, seven);
        Advance(city, 5 * 1440 + 1);
        Assert.Equal(1000, Counts(city.State).Commuters);
        var kept = ids.ToHashSet(StringComparer.Ordinal);
        Assert.Equal(ids.Count, city.State.Visitors.Count(v => kept.Contains(v.Id)));
        Invariants(city.State, "reloaded"); Conserved(city.State, "reloaded");
    }


    private static string Run(Action<CitySimulator>? perFrame, int budget, bool frameSlices, bool reloadMidFill, bool reloadDaily, bool shuffled, int days = 4)
    {
        var (state, market) = City(12, 1, seed: 77);
        PopulationFillCatalogue Fill()
        {
            var since = Since(3, 1);
            var places = Places(12, Base.Concat(since)); var census = WorkplaceCapacityTests.Workplaces(Base.Concat(since)).Census;
            if (shuffled) { places = places.OrderBy(p => StableRandom.Hash(p.Id, 3)).ToList(); census = census.OrderBy(w => StableRandom.Hash(w.WorkPlaceId, 3)).ToList(); }
            return new PopulationFillCatalogue(places, census);
        }
        var fill = Fill();
        var city = Start(state, market, fill);
        double end = (2 + days) * 1440 + 30;
        long units = 0; int reloads = 0, reloadedDay = -1; double reloadedBoundary = -1;
        while (true)
        {
            bool caught;
            if (frameSlices) { var r = city.AdvanceFrameSlice(end, budget); caught = r.CaughtUp; }
            else caught = city.AdvanceTo(end, budget).CaughtUp;
            perFrame?.Invoke(city);
            if (reloadMidFill && city.PopulationFillPending && city.PopulationFillUnits > 0 && city.PopulationFillBoundary > reloadedBoundary)
            { reloadedBoundary = city.PopulationFillBoundary!.Value; city = Reload(city, market, Fill()); reloads++; units = 0; continue; }
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
        Assert.Matches("\\|pv:(6[5-9]|[7-9][0-9]|[1-9][0-9]{2,})\\|", reference);
        Assert.Equal(reference, Run(null, 1, true, false, false, false));
        Assert.Equal(reference, Run(null, 7, true, false, false, false));
        Assert.Equal(reference, Run(null, 64, false, false, false, false));
        Assert.Equal(reference, Run(null, 1, true, true, false, false));
        Assert.Equal(reference, Run(null, 13, false, false, true, false));
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

    [Fact]
    public void ACompletedBoundaryStaysClosedAcrossAttachmentAndReload()
    {
        var (state, market) = City(12, 1);
        market = new VisitorRoutineTests.Market { Shops = Shops(state) };
        var city = Start(state, market, Catalogue(12, Base));
        Advance(city, 2 * 1440);
        int filled = state.Visitors.Count;
        Assert.True(filled > 0);
        var larger = Catalogue(12, Base, Since(30));
        string checkpoint = SaveCodec.Write(state);
        city.AttachPopulationFill(larger);
        Advance(city, 2 * 1440 + 1);
        Assert.Equal(filled, state.Visitors.Count);
        var read = SaveCodec.Read(checkpoint); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var reloaded = Start(read.State!, new VisitorRoutineTests.Market { Shops = Shops(read.State!) }, larger);
        Advance(reloaded, 2 * 1440 + 1);
        Assert.Equal(filled, read.State!.Visitors.Count); Assert.Equal(0, reloaded.PopulationFillCreated);
        Advance(city, 3 * 1440 + 1); Advance(reloaded, 3 * 1440 + 1);
        Assert.True(state.Visitors.Count > filled);
        Assert.Equal(SaveCodec.Write(state), SaveCodec.Write(read.State!));

        var (old, oldMarket) = City(12, 1);
        var plain = Start(old, oldMarket, null);
        Advance(plain, 2 * 1440);
        var first = Reload(plain, oldMarket, Catalogue(12, Base));
        Advance(first, 2 * 1440);
        Advance(first, 3 * 1440 - 1);
        Assert.Empty(first.State.Visitors);
        Advance(first, 3 * 1440 + 1);
        Assert.NotEmpty(first.State.Visitors);
        Assert.Equal(3 * 1440.0, first.PopulationFillBoundary);
    }

    [Fact]
    public void ASaveInsideABoundarysFillStoresItsUnitsAndTheReloadContinuesTheRest()
    {
        var (a, ma) = City(12, 1, seed: 52); var (b, mb) = City(12, 1, seed: 52);
        var fill = Catalogue(12, Base, Since(3));
        var plain = Start(a, ma, fill); var city = Start(b, mb, fill);
        Advance(plain, 3 * 1440 + 30);
        while (city.PopulationFillUnits == 0) city.AdvanceFrameSlice(3 * 1440 + 30, 1);
        Assert.Equal(2 * 1440.0, city.State.Minute);
        Assert.True(city.PopulationFillPending);
        Assert.Equal(PopulationFillPolicy.MaximumPersonsPerUnit, city.State.Visitors.Count);
        Invariants(city.State, "inside the fill"); Conserved(city.State, "inside the fill");
        city = Reload(city, mb, fill);
        Assert.True(city.PopulationFillPending);
        Advance(city, 3 * 1440 + 30);
        Assert.True(city.PopulationFillCreated > 0);
        Assert.Equal(SaveCodec.Write(a), SaveCodec.Write(city.State));
        Assert.Equal(ma.Orders, mb.Orders);
    }

    [Fact]
    public void ABoundaryPublishesAtMostItsCapAndALargerDeficitFillsOverLaterDays()
    {
        var (state, market) = City(90, 1, seed: 1234);
        var city = Start(state, market, Catalogue(90, Base, Since(80)));
        Advance(city, 2 * 1440 + 1);
        Assert.Equal(PopulationFillPolicy.MaximumPersonsPerBoundary, state.Visitors.Count);
        Assert.True(city.LastFillUnitPersons <= PopulationFillPolicy.MaximumPersonsPerUnit);
        Assert.True(city.PopulationFillUnits >= PopulationFillPolicy.MaximumPersonsPerBoundary / PopulationFillPolicy.MaximumPersonsPerUnit);
        Advance(city, 3 * 1440 + 1);
        Assert.Equal(2L * state.Citizens.Count, state.Visitors.Count);
        Invariants(state, "two boundaries"); Conserved(state, "two boundaries");
    }

    [Fact]
    public void CapabilitiesActivateAtTheFirstMidnightWithTheFillAttached()
    {
        foreach (bool slices in new[] { false, true })
        {
            var (plainState, plainMarket) = City(12, 1, seed: 71); var (state, market) = City(12, 1, seed: 71);
            state.Capabilities.Active = plainState.Capabilities.Active = false;
            var plain = Start(plainState, plainMarket, null);
            var city = Start(state, market, Catalogue(12, Base, Since(3)));
            foreach (double target in new[] { 2 * 1440 + 1.0, 3 * 1440 + 1.0 })
            {
                if (slices) { while (!city.AdvanceFrameSlice(target, 1).CaughtUp) { } while (!plain.AdvanceFrameSlice(target, 1).CaughtUp) { } }
                else { Advance(city, target); Advance(plain, target); }
                Assert.True(plainState.Capabilities.Active);
                Assert.True(state.Capabilities.Active, (slices ? "slices " : "AdvanceTo ") + target);
            }
            Assert.NotEmpty(state.Visitors);
        }
        var (observedState, observedMarket) = City(12, 1, seed: 71); var (directState, directMarket) = City(12, 1, seed: 71);
        observedState.Capabilities.Active = directState.Capabilities.Active = false;
        var observed = Start(observedState, observedMarket, Catalogue(12, Base, Since(3)));
        var direct = Start(directState, directMarket, Catalogue(12, Base, Since(3)));
        Advance(observed, AccountingClock.PreviousEpochTarget(2 * 1440 + .5));
        Assert.True(observed.BeginAccountingObservations(2 * 1440 + .5));
        Assert.Equal(2 * 1440.0, observedState.Minute);
        Assert.True(observedState.Capabilities.Active);
        Advance(observed, 2 * 1440 + 30); Advance(direct, 2 * 1440 + 30);
        Assert.True(directState.Capabilities.Active);
        Assert.NotEmpty(observedState.Visitors);
        Assert.Equal(SaveCodec.Write(directState), SaveCodec.Write(observedState));
    }

    private sealed class TrafficWalk : IRoutePlanner, ITrafficRoutePlanner
    {
        public TrafficNetwork? TrafficNetwork { get; } = new TrafficNetwork("f5g-test-roads", new[]
        {
            new TrafficNetworkNode { Id = 0, Position = new Position(5000, 0, 0), KilometresPerHour = 36, AllowsCar = true, Boundary = true, Successors = new[] { 1 } },
            new TrafficNetworkNode { Id = 1, Position = new Position(5020, 0, 0), KilometresPerHour = 36, AllowsCar = true, Boundary = true }
        }, 1);
        public void SetTrafficForecast(Func<int, double, TrafficLinkForecast>? forecast) { }
        public bool IsTrafficLinkClosed(int linkId) => false;
        public Route? Plan(Place from, Place to, double at, TravelPreferences preferences) => new FreshCityTraceTests.Walk().Plan(from, to, at, preferences);
    }

    [Fact]
    public void AFrameSliceStoppingOnMidnightKeepsTheTrafficClockWithTheCityWhenATrafficEventIsDueThere()
    {
        var (state, market) = City(12, 1, seed: 63);
        var city = Start(state, market, Catalogue(12, Base, Since(3)), new TrafficWalk());
        Advance(city, 2 * 1440 - 2);
        var kernel = (TrafficKernel)typeof(CitySimulator).GetField("traffic", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(city)!;
        Assert.Equal(2 * 1440 - 2.0, kernel.Clock);
        var link = new TrafficWalk().TrafficNetwork!.Links[0];
        kernel.Enqueue("f5g-probe-vehicle", new[] { new TrafficRouteLeg { LinkId = link.Id, StartNominalMinutes = 0, EndNominalMinutes = link.FreeFlowMinutes } });
        Advance(city, 2 * 1440 - 1);
        Assert.Equal(2 * 1440.0, kernel.NextEventMinute);
        while (city.PopulationFillUnits == 0) city.AdvanceFrameSlice(2 * 1440 + 1, 1);
        Assert.Equal(2 * 1440.0, state.Minute);
        Assert.Equal(state.Minute, kernel.Clock);
        Assert.Equal(2 * 1440.0, kernel.NextEventMinute);
    }

    [Fact]
    public void TheObservationEpochAndTrafficClockStayOnMidnightWhileTheFillRuns()
    {
        var (a, ma) = City(12, 1, seed: 63); var (b, mb) = City(12, 1, seed: 63);
        var fill = Catalogue(12, Base, Since(3));
        var plain = Start(a, ma, fill, new TrafficWalk()); var city = Start(b, mb, fill, new TrafficWalk());
        double native = 2 * 1440 + .25;
        foreach (var c in new[] { plain, city })
        {
            Advance(c, AccountingClock.PreviousEpochTarget(native));
            Assert.True(c.BeginAccountingObservations(native));
            Assert.Equal(2 * 1440.0, c.State.Minute);
        }
        Advance(plain, AccountingClock.Target(native));
        while (city.PopulationFillUnits == 0) city.AdvanceFrameSlice(AccountingClock.Target(native), 1);
        Assert.Equal(2 * 1440.0, city.State.Minute);
        Assert.True(AccountingClock.Compatible(city.State.Minute, native));
        Assert.True(city.IsAccountingEpochDrained(native));
        Assert.True(city.PopulationFillPending);
        city = Reload(city, mb, fill, new TrafficWalk());
        Advance(city, AccountingClock.Target(native));
        Assert.Equal(SaveCodec.Write(a), SaveCodec.Write(city.State));
        Advance(plain, 3 * 1440 + 60); Advance(city, 3 * 1440 + 60);
        Assert.Equal(SaveCodec.Write(a), SaveCodec.Write(city.State));
        Assert.Equal(ma.Orders, mb.Orders);
        Invariants(city.State, "traffic"); Conserved(city.State, "traffic");
    }

    [Fact]
    public void SkippedCommuterNumbersAreOnlyNonWorkingIdentitiesOnRead()
    {
        var (state, market) = City(12, 1);
        var city = Start(state, market, Catalogue(12, Base, Since(3)));
        Advance(city, 2 * 1440 + 60);
        Assert.True(city.PopulationFillSkipped > 0);
        string payload = SaveCodec.Write(state);
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(payload).Status);
        var commuter = state.Visitors.First(v => v.Stay.Pattern == VisitorPattern.Commuter && int.Parse(v.Id.Substring(3)) > 1);
        var copy = SaveCodec.Read(payload).State!;
        int at = copy.Visitors.FindIndex(v => v.Id == commuter.Id);
        var meal = copy.Food!.Residents.Single(m => m.ResidentId == commuter.Id);
        copy.Visitors.RemoveAt(at); copy.VisitorRoutines.RemoveAt(at); copy.Food.Residents.Remove(meal);
        Assert.Throws<SaveValidationException>(() => SaveCodec.Write(copy));
        var root = SaveWire.Parse(payload);
        var visitors = (Newtonsoft.Json.Linq.JArray)root["Visitors"]!;
        int index = visitors.Select((v, i) => (v, i)).First(t => ((string)t.v!).Contains("|" + commuter.Id + "|")).i;
        visitors.RemoveAt(index); ((Newtonsoft.Json.Linq.JArray)root["VisitorRoutines"]!).RemoveAt(index);
        var refused = SaveWire.Read(root.ToString(Newtonsoft.Json.Formatting.None));
        Assert.NotEqual(SaveReadStatus.Loaded, refused.Status);
        var identities = IdentityPolicy.V1(state.SourceIdentities);
        int next = state.NextVisitorSequence;
        while (!VisitorSchedules.WorkingPersona(identities.PersonaOf(state.Seed, PersonIds.Visitor(next)))) next++;
        var forged = SaveWire.Parse(payload); forged["NextVisitorSequence"] = next + 1;
        var forgedRead = SaveWire.Read(forged.ToString(Newtonsoft.Json.Formatting.None));
        Assert.Equal(SaveReadStatus.Invalid, forgedRead.Status); Assert.Contains("not a student or retiree", forgedRead.Error);
    }


    [Fact]
    public void EveryCreatedPersonIsEnrolledOnceWithItsPortalDistrictMix()
    {
        var (state, market) = City(12, 1);
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

        var foodless = Population.CreateFromHousing(WorkplaceCapacityTests.Donors, Places(12, Base), "f5g-fill", "0.41.1", 5150, 1440 + 360);
        PortalPlaces.Add(foodless, VisitorRoutineTests.Stations);
        var plain = new CitySimulator(foodless, new FreshCityTraceTests.Walk());
        plain.AttachPopulationFill(Catalogue(12, Base, Since(10)));
        Advance(plain, 4 * 1440);
        Assert.Empty(foodless.Visitors);
        Assert.True(SaveCodec.Read(SaveCodec.Write(foodless)).Status == SaveReadStatus.Loaded);
    }


    [Fact]
    public void CatalogueChangesMoveTheTargetOnlyAtTheNextBoundaryAndAnExistingCityStartsFillingThere()
    {
        var (state, market) = City(12, 1);
        var old = Start(state, market, null);
        Advance(old, 3 * 1440 + 600);
        Assert.Empty(state.Visitors);
        int schema = state.Schema;
        var city = Reload(old, market, Catalogue(12, Base, Since(5)));
        Advance(city, 4 * 1440 - 1);
        Assert.Empty(city.State.Visitors);
        Advance(city, 4 * 1440 + 1);
        Assert.NotEmpty(city.State.Visitors);
        Assert.Equal(schema, city.State.Schema); Assert.Equal(CityState.CurrentSchema, city.State.Schema);
        var first = city.PopulationFillTargets!.Value;
        int count = city.State.Visitors.Count;
        city.AttachPopulationFill(Catalogue(12, Base, Since(25)));
        Advance(city, 5 * 1440 - 1);
        Assert.Equal(count, city.State.Visitors.Count);
        Assert.Equal(first.CommuterTarget, city.PopulationFillTargets!.Value.CommuterTarget);
        Advance(city, 5 * 1440 + 1);
        Assert.True(city.PopulationFillTargets!.Value.CommuterTarget > first.CommuterTarget);
        Assert.True(city.State.Visitors.Count > count);
        var latched = city.PopulationFillTargets!.Value;
        city.AttachPopulationFill(Catalogue(12, Base, Since(60)));
        Assert.Equal(latched.CommuterTarget, city.PopulationFillTargets!.Value.CommuterTarget);
    }


    [Fact]
    public void FourWeeksOfStaysReturnTheSamePersonsAndConserveEverythingThroughReloads()
    {
        var (a, ma) = City(12, 1, seed: 4242); var (b, mb) = City(12, 1, seed: 4242);
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
                Assert.False(meal.Status == MealStatus.Travelling || meal.Status == MealStatus.Serving, meal.ResidentId + " has dormant demand");
                Assert.Null(meal.Decision);
                if (minute % 1440 == 60) Assert.Equal(day, meal.Budget!.LastPostedDay);
            }
            Invariants(state, "hour " + minute); Conserved(state, "hour " + minute);
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
        var poolIds = seen.Where(p => p.Value == VisitorPattern.DayVisit).Select(p => p.Key).ToList();
        Assert.NotEmpty(poolIds);
        Assert.True(poolIds.Count(id => stayDays.TryGetValue(id, out var d) && d.Count >= 2) > poolIds.Count / 2, "pool members do not return");
        Assert.Contains(seen.Where(p => p.Value == VisitorPattern.Commuter), p => stayDays.TryGetValue(p.Key, out var d) && d.Count >= 10);
        int Weekday(int d) => ((d - 1) % 7 + 7) % 7;
        double weekend = poolIds.Sum(id => stayDays.TryGetValue(id, out var d) ? d.Count(x => Weekday(x) >= 4) : 0) / 12.0;
        double weekdays = poolIds.Sum(id => stayDays.TryGetValue(id, out var d) ? d.Count(x => Weekday(x) < 4) : 0) / 16.0;
        Assert.True(weekend > weekdays, $"weekend {weekend} weekday {weekdays}");
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
                    Assert.True(VisitorSchedules.VisitsOn(r, state.Seed, day) || VisitorSchedules.VisitsOn(r, state.Seed, day - 1), r.Identity.Id + " present on day " + day);
                    (visits.TryGetValue(r.Identity.Id, out var d) ? d : visits[r.Identity.Id] = new HashSet<int>()).Add(day);
                }
        }
        Assert.True(visits.Count > pool.Count / 2);
    }

    [Fact]
    public void NoPoolMemberStaysDormantForGoodOverTenWeeks()
    {
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
            Assert.All(state.VisitorRoutines.Where(r => r.Journey != null), r => Assert.True(ActiveJourneyIndex.ReceivesBody(r) && city.ActiveJourneys.BodyCandidates.Contains(r)));
        }
        Assert.True(planner.PortalHits > 0 && planner.PortalMisses > 0, $"hits {planner.PortalHits} misses {planner.PortalMisses}");
        Assert.True(planner.PortalHits > planner.PortalMisses);
        Invariants(state, "cached");

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


    [Fact]
    public void DayVisitorsRoundTripAndMalformedDayVisitorsAreRefusedAndKept()
    {
        var (state, market) = City(12, 1);
        var city = Start(state, market, Catalogue(12, Base, Since(2)));
        Advance(city, 2 * 1440 + 600);
        Assert.Contains(state.Visitors, v => v.Stay.Pattern == VisitorPattern.DayVisit);
        string payload = SaveCodec.Write(state);
        Assert.StartsWith("{\"Schema\":" + CityState.CurrentSchema + ",", payload);
        var read = SaveCodec.Read(payload);
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(payload, SaveCodec.Write(read.State!));

        var copy = SaveCodec.Read(payload).State!;
        var day = copy.Visitors.First(v => v.Stay.Pattern == VisitorPattern.DayVisit);
        var work = copy.Places.First(p => p.Kind == PlaceKind.Work && p.Capacity > 0);
        day.Routine!.DayPlaceId = work.Id; day.Routine.EmploymentStartDay = (int)(copy.Minute / 1440); day.Routine.Carried = null;
        VisitorSchedules.Sync(day);
        Assert.Throws<SaveValidationException>(() => SaveCodec.Write(copy));
        var seated = state.Visitors.First(v => v.Stay.Pattern == VisitorPattern.Commuter && v.Employment != null);
        var tuple = new System.Text.RegularExpressions.Regex("\\|" + System.Text.RegularExpressions.Regex.Escape(seated.Id) + "\\|(\\w{3})C");
        string body = SaveWire.Body(payload);
        Assert.Matches(tuple, body);
        string forged = SaveWire.Payload(tuple.Replace(body, m => "|" + seated.Id + "|" + m.Groups[1].Value + "D", 1));
        Assert.StartsWith(SaveCodec.BodyPrefix, forged);
        var refused = SaveCodec.Read(forged);
        Assert.Equal(SaveReadStatus.Invalid, refused.Status); Assert.Contains("day visitor holds no employment", refused.Error);
        var data = new Dictionary<string, string> { [SaveStore.StateKey] = forged, ["rlo:state"] = "kept" };
        Assert.Equal(SaveReadStatus.Invalid, SaveSession.Open(data, state.PopulationKey).InitialReadResult.Status);
        Assert.False(SaveStore.WriteTo(data, state).Success);
        Assert.Same(forged, data[SaveStore.StateKey]); Assert.Equal("kept", data["rlo:state"]);
    }
}
