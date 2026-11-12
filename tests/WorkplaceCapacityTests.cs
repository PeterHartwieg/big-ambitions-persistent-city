using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>F5e (#92): workplace capacity and employment. Work and Study places seat persons by F5a's area bands instead
/// of a fixed 16; Employment is assigned and ended by the existing daily draws per person; visitors compete for what
/// residents leave; Study carries no wage; no native staff member is used.</summary>
public sealed class WorkplaceCapacityTests
{
    // ---- Fixtures ----

    /// <summary>One business as Runtime's GamePlaces sees it: catalogue area, player ownership, school or not.</summary>
    internal readonly record struct Business(string Id, double Area, bool PlayerOwned = false, bool School = false);

    internal static readonly Business[] Businesses =
    {
        new("address:a:1", 30), new("address:b:2", 60), new("address:c:3", 120), new("address:d:4", 250),
        new("address:e:5", 500), new("address:f:6", 900), new("address:g:7", 0), new("address:h:8", 700, PlayerOwned: true),
        new("address:s:9", 100, School: true),
    };

    /// <summary>Work and Study places exactly as GamePlaces builds them under the F5e policy, plus the census facts.</summary>
    internal static (List<Place> Places, List<CensusWorkplace> Census) Workplaces(IEnumerable<Business> businesses, Func<Business, int>? baseRule = null)
    {
        var places = new List<Place>(); var census = new List<CensusWorkplace>();
        int i = 0;
        foreach (var b in businesses)
        {
            var at = new Position(150 + 60 * (i % 4), 0, -120 + 70 * (i / 4)); i++;
            places.Add(new Place { Id = b.Id + ":work", Name = "Business " + b.Id, Kind = PlaceKind.Work, Position = at,
                Capacity = baseRule?.Invoke(b) ?? WorkplaceCapacityPolicy.Work(b.Area, b.PlayerOwned), EvidenceSource = WorkplaceCapacityPolicy.Evidence });
            if (b.School)
                places.Add(new Place { Id = b.Id + ":study", Name = "School " + b.Id, Kind = PlaceKind.Study, Position = at,
                    Capacity = baseRule?.Invoke(b) ?? WorkplaceCapacityPolicy.Study(b.Area), EvidenceSource = WorkplaceCapacityPolicy.Evidence });
            census.Add(new CensusWorkplace { WorkPlaceId = b.Id + ":work", StudyPlaceId = b.School ? b.Id + ":study" : "",
                AreaSquareMetres = b.Area, PlayerOwned = b.PlayerOwned });
        }
        return (places, census);
    }

    internal static readonly CitizenIdentity[] Donors = Enumerable.Range(0, 10).Select(n => new CitizenIdentity
    {
        Id = "c" + n, Name = "Resident " + n, Age = n == 9 ? 71 : n == 8 ? 19 : n == 7 ? 21 : 24 + n * 3,
        Persona = n == 9 ? Persona.Retiree : n >= 7 ? Persona.Student : n % 3 == 0 ? Persona.Foodie : n % 3 == 1 ? Persona.Commuter : Persona.Parent
    }).ToArray();

    /// <summary>Twelve homes of seven beds (75 residents at the 90% fill), the businesses above, a park and three shops.</summary>
    internal static List<Place> Catalogue(Func<Business, int>? baseRule = null)
    {
        var places = Enumerable.Range(0, 12).Select(h => new Place { Id = "home:" + h.ToString("00"), Name = "Home " + h, Kind = PlaceKind.Home,
            Capacity = 7, ResidentialAreaSquareMetres = 160, Position = new Position(-60 + 25 * (h % 4), 0, -40 + 30 * (h / 4)) }).ToList();
        places.AddRange(Workplaces(Businesses, baseRule).Places);
        places.Add(new Place { Id = "leisure:a", Name = "Park", Kind = PlaceKind.Leisure, Capacity = 64, Position = new Position(60, 0, -80) });
        places.AddRange(Enumerable.Range(0, 3).Select(i => new Place { Id = "shop:" + i, Name = "Shop " + i, Kind = PlaceKind.Shop,
            Capacity = 16, Position = new Position(20 + i * 30, 0, 15 - i * 5) }));
        return places;
    }

    /// <summary>A fresh banded city in the current writer. With <paramref name="food"/> its roster (and its K−1 visitors per
    /// resident) is created when the simulator attaches the market; without food the visitors are created directly.</summary>
    internal static (CityState State, VisitorRoutineTests.Market Market) Fresh(int factor = 2, int seed = 5150, bool food = true,
        Func<Business, int>? baseRule = null, IEnumerable<CitizenIdentity>? donors = null, IEnumerable<Place>? places = null)
    {
        var (state, market) = Unattached(factor, seed, food, baseRule, donors, places);
        // Attaching the market creates the roster and its visitors, as Runtime does for a fresh city.
        if (food) new CitySimulator(state, new FreshCityTraceTests.Walk()).AttachFoodMarket(market);
        return (state, market);
    }

    private static (CityState State, VisitorRoutineTests.Market Market) Unattached(int factor, int seed, bool food,
        Func<Business, int>? baseRule, IEnumerable<CitizenIdentity>? donors, IEnumerable<Place>? places)
    {
        var state = Population.CreateFromHousing(donors ?? Donors, places ?? Catalogue(baseRule), "f5e-banded", "0.41.1", seed, 1440 + 6 * 60);
        PortalPlaces.Add(state, VisitorRoutineTests.Stations);
        var market = new VisitorRoutineTests.Market();
        if (food)
        {
            state.Food = new FoodCommerceState { MealRhythmVersion = MealRhythm.PolicyVersion, HouseholdFinanceVersion = HouseholdFinance.PolicyVersion,
                RepresentationFactor = factor,
                Shops = Enumerable.Range(0, 3).Select(i => new FoodScope { PlaceId = "shop:" + i, BusinessId = "b" + i, ProductId = FoodProducts.Croissant }).ToList() };
            market.Shops = state.Food.Shops.Select(s => new FoodBusiness { Id = s.BusinessId, PlaceId = s.PlaceId, Name = s.PlaceId, MealsPerHour = 30,
                Hours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 6 * 60, ToMinute = 22 * 60 }).ToList(),
                Offers = new() { new FoodOffer { ProductId = FoodProducts.Croissant, Price = Money.FromCents(250), Units = 1000 } } }).ToArray();
        }
        else VisitorSchedules.Create(state, VisitorSchedules.FreshVisitors(state.Citizens.Count, factor));
        return (state, market);
    }

    private static CitySimulator Start(CityState state, VisitorRoutineTests.Market market)
    {
        var city = new CitySimulator(state, new FreshCityTraceTests.Walk());
        if (state.Food != null) city.AttachFoodMarket(market);
        return city;
    }

    private static void Advance(CitySimulator city, double minute, int budget = 64) { while (!city.AdvanceTo(minute, budget).CaughtUp) { } }

    private static CitySimulator Reload(CitySimulator city, VisitorRoutineTests.Market market)
    {
        string payload = SaveCodec.Write(city.State);
        var read = SaveWire.Read(payload);
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(payload, SaveCodec.Write(read.State!));
        return Start(read.State!, market);
    }

    /// <summary>Persons seated per Work/Study place: residents, then visitor routines.</summary>
    private static Dictionary<string, int> Seated(CityState state)
    {
        var seated = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var person in PersonTable.Schedules(state))
            if (person.DayPlaceId.Length != 0) seated[person.DayPlaceId] = (seated.TryGetValue(person.DayPlaceId, out int n) ? n : 0) + 1;
        return seated;
    }

    private static void WithinCapacity(CityState state, string context)
    {
        var places = state.Places.ToDictionary(p => p.Id, StringComparer.Ordinal);
        foreach (var (id, count) in Seated(state))
        {
            var place = places[id];
            Assert.True(place.Kind == PlaceKind.Work || place.Kind == PlaceKind.Study, context + ": " + id + " is not a workplace");
            Assert.True(count <= place.Capacity, context + ": " + id + " seats " + count + " of " + place.Capacity);
        }
    }

    private static int Sequence(string visitorId) => int.Parse(visitorId.Substring(PersonIds.VisitorPrefix.Length), System.Globalization.CultureInfo.InvariantCulture);

    private static string Sha(string text) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private static void RefusedAndPreserved(string payload, CityState replacement, SaveReadStatus expected, string? error = null)
    {
        var read = SaveWire.Read(payload);
        Assert.True(read.Status == expected, read.Status + ": " + read.Error); Assert.Null(read.State);
        if (error != null) Assert.Contains(error, read.Error);
        var data = new Dictionary<string, string> { [SaveStore.StateKey] = payload, ["rlo:state"] = "kept", [FoodAuthorityStore.Key] = "manifest" };
        Assert.Equal(expected, SaveSession.Open(data, replacement.PopulationKey).InitialReadResult.Status);
        Assert.False(SaveStore.WriteTo(data, replacement).Success);
        Assert.False(SaveSession.Open(data, replacement.PopulationKey).WriteTo(data, replacement).Success);
        Assert.Same(payload, data[SaveStore.StateKey]); Assert.Equal("kept", data["rlo:state"]); Assert.Equal("manifest", data[FoodAuthorityStore.Key]);
    }

    // ---- Golden ----

    private static IEnumerable<(string Name, Func<CityState> Build)> GoldenFixtures() => new (string, Func<CityState>)[]
    {
        ("banded-visitors-factor2-30h", () => { var (state, market) = Fresh(); var city = Start(state, market); Advance(city, state.Minute + 30 * 60); return city.State; }),
    };

    /// <summary>The writer's golden for the banded city, re-pinned once by X3 at the held schema 1. A deliberate format change
    /// re-pins it in place (docs/decisions/2026-11-09-schema-one.md).</summary>
    [Theory]
    [InlineData("banded-visitors-factor2-30h", "7596770dde88abe087e9a4badd411a133e8a832d029df675064591fb10b25854", 213982)]
    public void GoldenBandedCityBytesArePinned(string name, string sha256, int characters)
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

    // ---- Older cities are refused and kept ----

    /// <summary>Older development builds wrote 2 to 36 (writer 27 seated cities under a fixed 16). Any number other than the
    /// held 1 is refused with the distinct status through codec, store, session and the early probe, and every key is kept.</summary>
    [Fact]
    public void AnotherSchemaNumberIsRefusedWithTheStartANewCityStatusAndKept()
    {
        var replacement = Fresh().State;
        foreach (int older in new[] { 26, 27, 37 })
        {
            string payload = SaveWire.Parse(SaveCodec.Write(replacement)).Also(r => r["Schema"] = older).ToString(Formatting.None);
            var read = SaveWire.Read(payload);
            Assert.Equal(SaveReadStatus.DevelopmentSchema, read.Status); Assert.Equal(older, read.SourceSchema);
            Assert.Contains("start a new city", read.Error);
            RefusedAndPreserved(payload, replacement, SaveReadStatus.DevelopmentSchema, "start a new city");
            Assert.Equal(older, SaveCodec.DevelopmentSchemaOf(payload));
            DevelopmentChain.RequireRefusedCapabilities(payload);
        }
    }

    // ---- The capacity rule ----

    [Theory]
    [InlineData(0, 0)] [InlineData(-5, 0)] [InlineData(double.NaN, 0)] [InlineData(1, 2)] [InlineData(39.99, 2)] [InlineData(40, 4)]
    [InlineData(74, 4)] [InlineData(75, 6)] [InlineData(149, 6)] [InlineData(150, 10)] [InlineData(299, 10)] [InlineData(300, 16)]
    [InlineData(599, 16)] [InlineData(600, 24)] [InlineData(100000, 24)]
    public void WorkAndStudyCapacityFollowTheAreaBands(double area, int work)
    {
        Assert.Equal(work, WorkplaceCapacityPolicy.Work(area, playerOwned: false));
        Assert.Equal(work * PopulationCensusPolicy.StudentsPerWorkplaceBandSlot, WorkplaceCapacityPolicy.Study(area));
        Assert.Equal(PopulationCensusPolicy.WorkplaceCapacity(area), WorkplaceCapacityPolicy.Work(area, false));
        // Persistent City persons are not the player's native staff.
        Assert.Equal(0, WorkplaceCapacityPolicy.Work(area, playerOwned: true));
    }

    /// <summary>The seated capacity equals the census's assignable capacity for the same business facts, so the F5a
    /// report and the city agree; the census then finds no over-capacity workplace in a fresh city.</summary>
    [Fact]
    public void FreshCityCapacityEqualsTheCensusAndIsNeverExceeded()
    {
        var (state, _) = Fresh();
        var (_, facts) = Workplaces(Businesses);
        var census = PopulationCensus.Compute(new PopulationCensusInput { Places = PopulationCensusInput.CapturePlaces(state.Places), Workplaces = facts,
            Residents = PopulationCensusInput.CaptureResidents(state.Citizens), StationIds = state.Portals });
        Assert.False(census.Refused, census.RefusalReason);
        Assert.Equal(state.Places.Where(p => p.Kind == PlaceKind.Work).Sum(p => (long)p.Capacity), census.WorkplaceCapacity);
        Assert.Equal(state.Places.Where(p => p.Kind == PlaceKind.Study).Sum(p => (long)p.Capacity), census.StudyCapacity);
        Assert.Equal(0, census.OverCapacityWorkplaces); Assert.True(census.JobsWithinCapacity); Assert.True(census.StudentsWithinCapacity);
        WithinCapacity(state, "fresh");
        // Unknown area and player-owned businesses seat nobody, residents or visitors.
        var seated = Seated(state);
        Assert.False(seated.ContainsKey("address:g:7:work")); Assert.False(seated.ContainsKey("address:h:8:work"));
        Assert.Equal(0, state.Places.Single(p => p.Id == "address:h:8:work").Capacity);
    }

    /// <summary>Residents are seated first (ordinal ID order), then visitors in <c>pv:</c> order compete for what is
    /// left. Because every vacancy can be chosen at creation, a working visitor is left unseated only once every
    /// workplace is full, and from then on no later visitor is seated: the fill order is a prefix.</summary>
    [Fact]
    public void ResidentsThenVisitorsFillCapacityInADeterministicOrder()
    {
        var (state, _) = Fresh();
        long capacity = state.Places.Where(p => p.Kind == PlaceKind.Work).Sum(p => (long)p.Capacity);
        int residentWorkers = state.Citizens.Count(c => c.DayPlaceId.Length != 0 && ResidentLifecycle.IsWorker(c));
        Assert.True(residentWorkers > 0 && residentWorkers < capacity, "residents leave vacancies: " + residentWorkers + " of " + capacity);
        Assert.All(state.Citizens.Where(ResidentLifecycle.IsWorker), c => Assert.NotEqual("", c.DayPlaceId));
        var workers = state.VisitorRoutines.Where(v => ResidentLifecycle.IsWorker(v) && v.Identity.Age >= 18).ToList();
        int seated = workers.Count(v => v.DayPlaceId.Length != 0);
        Assert.True(seated > 0 && seated < workers.Count, "visitors partly seated: " + seated + " of " + workers.Count);
        Assert.Equal(capacity, residentWorkers + seated);
        // Prefix: in pv order every seated working visitor precedes every unseated one, which carries outside income.
        var ordered = workers.OrderBy(v => Sequence(v.Identity.Id)).ToList();
        Assert.All(ordered.Take(seated), v => Assert.NotEqual("", v.DayPlaceId));
        Assert.All(ordered.Skip(seated), v => { Assert.Equal("", v.DayPlaceId); Assert.Equal(ResidentIncomeSource.ExternalEmployment, v.VisitorCarried); });
        WithinCapacity(state, "fill");
        // Shuffled catalogue and donor enumeration give the identical city.
        var shuffled = Fresh(donors: Donors.Reverse(), places: Catalogue().OrderBy(p => StableRandom.Hash(p.Id, 3))).State;
        Assert.Equal(SaveCodec.Write(state), SaveCodec.Write(shuffled));
    }

    // ---- Employment by the existing daily draws ----

    private sealed class DrawLog
    {
        public long EmployedChecks, Losses, UnemployedChecks, FindRolls, Seatings, CarriedEnded;
        public readonly Dictionary<(string Id, int Day), bool> Lost = new();
    }

    /// <summary>The daily employment check for every person on every day, as the simulator runs it at a safe home event
    /// (a route probe that always succeeds takes the first candidate). Checks capacity and one Employment after every
    /// day. Only the employment state moves; the simulator test below covers the same draws with reloads.</summary>
    private static (CityState State, DrawLog Log) Churn(CityState state, int days, WorkplaceOccupancy? index = null)
    {
        index?.Rebuild(state);
        var log = new DrawLog();
        int first = (int)(state.Minute / 1440) + 1;
        for (int day = first; day < first + days; day++)
        {
            double now = day * 1440.0 + 60;
            foreach (var person in PersonTable.Schedules(state).ToList())
            {
                if (!ResidentLifecycle.IsWorker(person)) continue;
                bool employed = person.DayPlaceId.Length != 0;
                bool carried = person.VisitorCarried.HasValue;
                string before = person.DayPlaceId;
                var candidates = ResidentLifecycle.BeginEmploymentCheck(state, person, now, null, index);
                index?.Move(before, person.DayPlaceId);
                if (employed)
                {
                    log.EmployedChecks++;
                    bool lost = person.DayPlaceId.Length == 0;
                    if (lost) log.Losses++;
                    log.Lost[(person.Identity.Id, day)] = lost;
                    continue;
                }
                log.UnemployedChecks++;
                if (carried && !person.VisitorCarried.HasValue) log.CarriedEnded++;
                if (candidates == null) continue;
                log.FindRolls++;
                if (candidates.Count == 0) continue;
                ResidentLifecycle.AssignWorkplace(person, candidates[0]);
                index?.Move("", person.DayPlaceId);
                log.Seatings++;
            }
            foreach (var visitor in state.Visitors) VisitorSchedules.Sync(visitor);
            WithinCapacity(state, "day " + day);
            if (index != null) Assert.True(index.Matches(state), "occupancy index after day " + day);
            foreach (var visitor in state.Visitors)
                Assert.Equal(visitor.Routine!.DayPlaceId, visitor.Employment?.WorkplaceId ?? "");
        }
        return (state, log);
    }

    /// <summary>Under two years of daily draws no workplace ever seats more than its capacity, every person has at most
    /// one Employment (residents through DayPlaceId, visitors' Employment equals their routine's), and every job change
    /// is counted. The codec refuses any payload that seats more than a place's capacity.</summary>
    [Fact]
    public void CapacityIsNeverExceededUnderChurn()
    {
        var (state, _) = Fresh();
        var (final, log) = Churn(state, 720);
        Assert.True(log.Losses > 0 && log.Seatings > 0, "churn happened: " + log.Losses + " losses, " + log.Seatings + " seatings");
        // One active Employment per person: the ownership contract over the table, checked on write.
        var employments = final.Citizens.Select(PersonTable.ResidentEmployment).Where(e => e != null).Select(e => e!.PersonId)
            .Concat(final.Visitors.Where(v => v.Employment != null).Select(v => v.Id)).ToList();
        Assert.Equal(employments.Count, employments.Distinct(StringComparer.Ordinal).Count());
        // Every job change is one logged loss or seating (JobChanges start at zero at creation).
        Assert.Equal(final.Citizens.Sum(c => c.JobChanges) + final.VisitorRoutines.Sum(v => v.JobChanges), log.Losses + log.Seatings);
        // A payload that seats one more person than a place's capacity is refused and kept.
        var saved = Fresh().State;
        var full = Seated(saved).First(kv => kv.Value > 0);
        var root = SaveWire.Parse(SaveCodec.Write(saved));
        foreach (var place in (JArray)root["Places"]!)
            if ((string)place["Id"]! == full.Key) place["Capacity"] = full.Value - 1;
        RefusedAndPreserved(root.ToString(Formatting.None), Fresh().State, SaveReadStatus.Invalid, "exceeds capacity");
    }

    /// <summary>The simulator's occupancy index gives exactly the vacancies a full recount gives: two identical cities
    /// churned for two years, one checking vacancies through the index and one recounting every person, end with the
    /// same assignments, and the index equals a recount after every day.</summary>
    [Fact]
    public void OccupancyIndexGivesTheSameVacanciesAsAFullRecount()
    {
        var recount = Churn(Fresh().State, 720).State;
        var indexed = Churn(Fresh().State, 720, new WorkplaceOccupancy()).State;
        Assert.Equal(PersonTable.Schedules(recount).Select(p => p.Identity.Id + "=" + p.DayPlaceId + "/" + p.JobChanges),
            PersonTable.Schedules(indexed).Select(p => p.Identity.Id + "=" + p.DayPlaceId + "/" + p.JobChanges));
        Assert.True(PersonTable.Schedules(indexed).Sum(p => p.JobChanges) > 0);
    }

    /// <summary>Fresh-seed comparison against the base rule (every Work and Study place 16): the same draw decides
    /// loss and job finding per person and day under both capacity rules. Where a person is employed in both arms its
    /// loss outcome is identical; the loss rate per employed day and the find-roll rate per unemployed day both match
    /// CityBalance's chances within four standard deviations, in both arms and across seeds. Only the seating after a
    /// successful roll depends on capacity.</summary>
    [Theory]
    [InlineData(5150)] [InlineData(77)]
    public void LossAndFindRatesPerPersonAreUnchangedAgainstTheBase(int seed)
    {
        var banded = Churn(Fresh(seed: seed).State, 1500).Log;
        var baseArm = Churn(Fresh(seed: seed, baseRule: _ => 16).State, 1500).Log;
        int shared = 0;
        foreach (var (key, lost) in banded.Lost)
            if (baseArm.Lost.TryGetValue(key, out bool other)) { shared++; Assert.Equal(other, lost); }
        Assert.True(shared > 10000, "shared employed person-days: " + shared);
        foreach (var log in new[] { banded, baseArm })
        {
            Within(log.Losses, log.EmployedChecks, CityBalance.DailyJobLossChance, "loss");
            // An unemployed check rolls the finding chance unless the loss chance already ended a carried employer.
            Within(log.FindRolls, log.UnemployedChecks - log.CarriedEnded, CityBalance.DailyJobFindingChance, "find");
        }
        static void Within(long hits, long trials, double p, string label)
        {
            Assert.True(trials > 0, label + " trials");
            double mean = trials * p, sd = Math.Sqrt(trials * p * (1 - p));
            Assert.True(Math.Abs(hits - mean) <= 4 * sd, label + ": " + hits + " of " + trials + ", expected " + mean.ToString("F1") + " ± " + (4 * sd).ToString("F1"));
        }
    }

    /// <summary>The simulator runs the same draws at safe home events. Over three months with a reload every ten days, a
    /// fresh banded city loses and refills jobs (each loss opens a vacancy that residents and visitors compete for) and
    /// never seats more than capacity; every visitor's Employment matches its routine, and every reload keeps its bytes.</summary>
    [Fact]
    public void SimulatedMonthsOfChurnWithReloadsStayWithinCapacity()
    {
        var (state, market) = Fresh();
        var city = Start(state, market);
        double start = state.Minute;
        var changes = new HashSet<string>(StringComparer.Ordinal);
        for (int day = 1; day <= 90; day++)
        {
            Advance(city, start + day * 1440);
            city.SynchronizeSnapshot();
            WithinCapacity(city.State, "day " + day);
            Assert.True(city.OccupancyMatchesFullCount(), "occupancy index on day " + day);
            foreach (var person in PersonTable.Schedules(city.State)) if (person.JobChanges > 0) changes.Add(person.Identity.Id);
            if (day % 10 == 0) { city = Reload(city, market); Assert.True(city.OccupancyMatchesFullCount(), "after reload " + day); }
        }
        Assert.True(changes.Count >= 3, "job changes: " + changes.Count);
        Assert.All(city.State.Visitors, v => Assert.Equal(v.Routine!.DayPlaceId, v.Employment?.WorkplaceId ?? ""));
        Assert.NotEmpty(market.Orders);
    }

    // ---- Study carries no wage; income composes with F5d ----

    /// <summary>A Study enrolment gives no employment income, for residents and visitors; a seated worker gets employment
    /// income from its own Employment; an unseated working visitor keeps its carried outside employer (F5d).</summary>
    [Fact]
    public void StudyCarriesNoWageAndIncomeFollowsEachPersonsOwnEmployment()
    {
        var (state, market) = Fresh();
        var city = Start(state, market);
        Advance(city, state.Minute + 30 * 60);
        city.SynchronizeSnapshot();
        var meals = city.State.Food!.Residents.ToDictionary(m => m.ResidentId, StringComparer.Ordinal);
        int students = 0, workers = 0;
        foreach (var person in PersonTable.Schedules(city.State))
        {
            var source = meals[person.Identity.Id].Budget!.IncomeSource;
            if (person.Identity.Persona == Persona.Student)
            {
                if (person.DayPlaceId.Length != 0) students++;
                Assert.NotEqual(ResidentIncomeSource.ExternalEmployment, source);
            }
            else if (person.DayPlaceId.Length != 0 && person.Identity.Age >= 18 && person.Identity.Persona != Persona.Retiree)
            { workers++; Assert.Equal(ResidentIncomeSource.ExternalEmployment, source); }
            else if (person.IsVisitor && person.VisitorCarried.HasValue) Assert.Equal(person.VisitorCarried.Value, source);
        }
        Assert.True(students > 0 && workers > 0, students + " students, " + workers + " workers");
        // The resident rule is the shared per-person rule: a Study Employment never selects employment income.
        foreach (var persona in Enum.GetValues<Persona>())
            foreach (int age in new[] { 16, 18, 40, 70 })
                foreach (string place in new[] { "", "work" })
                {
                    var job = place.Length == 0 ? null : new Employment { PersonId = "p", WorkplaceId = place,
                        Kind = persona == Persona.Student ? EmploymentKind.Study : EmploymentKind.Work };
                    Assert.Equal(PersonIncome.Select(persona, age, job, null), PersonIncome.ForDayPlace(persona, age, place));
                }
        Assert.NotEqual(ResidentIncomeSource.ExternalEmployment, PersonIncome.Select(Persona.Commuter, 30,
            new Employment { PersonId = "p", WorkplaceId = "school", Kind = EmploymentKind.Study }, null));
    }

    // ---- Vanished places ----

    /// <summary>The saved catalogue is the city's own; a workplace never disappears from it in play. A payload whose
    /// Employment names a missing place, a place of the wrong kind or a zero-capacity place is refused and kept.</summary>
    [Fact]
    public void EmploymentAtAVanishedOrWrongPlaceIsRefusedAndKept()
    {
        var (state, market) = Fresh();
        var city = Start(state, market); Advance(city, state.Minute + 120);
        string payload = SaveCodec.Write(city.State);
        var replacement = Fresh().State;
        var resident = city.State.Citizens.First(c => c.DayPlaceId.Length != 0 && c.Identity.Persona != Persona.Student);
        int index = city.State.Citizens.IndexOf(resident);
        string Edit(Action<JObject> change) { var r = SaveWire.Parse(payload); change(r); return r.ToString(Formatting.None); }
        RefusedAndPreserved(Edit(r => r["Citizens"]![index]!["DayPlaceId"] = "address:gone:1:work"), replacement, SaveReadStatus.Invalid);
        RefusedAndPreserved(Edit(r => r["Citizens"]![index]!["DayPlaceId"] = "shop:0"), replacement, SaveReadStatus.Invalid);
        // The workplace removed from the catalogue (its index shifts every later place): refused, never re-seated.
        RefusedAndPreserved(Edit(r => { var places = (JArray)r["Places"]!; places.Remove(places.First(p => (string)p["Id"]! == resident.DayPlaceId)); }),
            replacement, SaveReadStatus.Invalid);
        // A zero-capacity workplace (unknown area or player-owned) can hold nobody.
        RefusedAndPreserved(Edit(r => r["Citizens"]![index]!["DayPlaceId"] = "address:g:7:work"), replacement, SaveReadStatus.Invalid, "exceeds capacity");
        RefusedAndPreserved(Edit(r => r["Citizens"]![index]!["DayPlaceId"] = "address:h:8:work"), replacement, SaveReadStatus.Invalid, "exceeds capacity");
    }

    // ---- Native-employment source guard ----

    private static string RepositoryRoot()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Core", "WorkplaceCapacity.cs"))) root = root.Parent;
        Assert.NotNull(root);
        return root!.FullName;
    }

}

internal static class JsonTestExtensions
{
    public static JObject Also(this JObject value, Action<JObject> change) { change(value); return value; }
}
