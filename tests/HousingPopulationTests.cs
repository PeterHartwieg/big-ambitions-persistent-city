using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class HousingPopulationTests
{
    private static CitizenIdentity[] Donors(int count) => Enumerable.Range(0, count).Select(i => new CitizenIdentity
    {
        Id = "source:" + i.ToString("D4"), Name = "Given" + i + " Family" + i,
        District = "district", Age = 30 + i % 30, Persona = Persona.Commuter, AppearanceSeed = 100 + i
    }).ToArray();

    private static Place[] Places(int beds) => new[]
    {
        new Place { Id = "home", Kind = PlaceKind.Home, Capacity = beds, District = "district" },
        new Place { Id = "work-a", Kind = PlaceKind.Work, Capacity = beds, Position = new(100, 0, 0) },
        new Place { Id = "work-b", Kind = PlaceKind.Work, Capacity = beds, Position = new(200, 0, 0) }
    };

    private static CityState Housing(int donors, int beds) => Population.CreateFromHousing(Donors(donors), Places(beds), "source", "0.40.4", 7, 1440);

    private static void CheckAccounting(CityState city)
    {
        Assert.Equal((long)city.Citizens.Count + city.DepartedCount,
            (long)city.InitialImportedResidents + city.NextResidentSequence - 1);
        Assert.Equal(city.Citizens.Count, city.Citizens.Select(c => c.Identity.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void HousingChoosesStableOriginalPrefixAndPreservesEntireDetachedDonorBank()
    {
        var donors = Donors(20);
        var city = Population.CreateFromHousing(donors.Reverse(), Places(10), "source", "0.40.4", 7, 1440);
        Assert.Equal(9, city.PopulationTarget);
        Assert.Equal(9, city.InitialImportedResidents);
        Assert.Equal(donors.Take(9).Select(c => c.Id), city.Citizens.Select(c => c.Identity.Id));
        Assert.Equal(20, city.SourceIdentities.Count);
        Assert.All(city.Citizens, c => Assert.NotSame(c.Identity, city.SourceIdentities.Single(s => s.Id == c.Identity.Id)));
        donors[0].Name = "Changed caller";
        city.Citizens[0].Identity.Name = "Changed resident";
        Assert.Equal("Given0 Family0", city.SourceIdentities[0].Name);
        CheckAccounting(city);
    }

    [Fact]
    public void HousingLargerThanDonorBankCreatesModOwnedResidentsWithoutMutatingSources()
    {
        var donors = Donors(2);
        string original = JsonConvert.SerializeObject(donors);
        var city = Population.CreateFromHousing(donors, Places(10), "source", "0.40.4", 7, 1440);
        Assert.Equal(9, city.Citizens.Count);
        Assert.Equal(2, city.InitialImportedResidents);
        Assert.Equal(8, city.NextResidentSequence);
        Assert.Equal(new[] { "source:0000", "source:0001", "pc:1", "pc:2", "pc:3", "pc:4", "pc:5", "pc:6", "pc:7" },
            city.Citizens.Select(c => c.Identity.Id));
        Assert.Equal(original, JsonConvert.SerializeObject(city.SourceIdentities));
        Assert.Equal(original, JsonConvert.SerializeObject(donors));
        for (int sequence = 1; sequence <= 7; sequence++)
            Assert.True(Population.EqualIdentity(ResidentLifecycle.RemixIdentity(city, sequence), city.Citizens[sequence + 1].Identity));
        CheckAccounting(city);
    }

    [Fact]
    public void OccupancyAppliesToCombinedCapacityIncludingSingleBedHomes()
    {
        var homes = Enumerable.Range(0, 10).Select(i => new Place { Id = "home:" + i, Kind = PlaceKind.Home, Capacity = 1 }).ToArray();
        var city = Population.CreateFromHousing(Donors(20), homes, "source", "0.40.4", 7, 1440);
        Assert.Equal(9, city.Citizens.Count);
        Assert.Equal(9, city.Citizens.Select(c => c.HomeId).Distinct().Count());
        Assert.All(homes, home => Assert.True(city.Citizens.Count(c => c.HomeId == home.Id) <= home.Capacity));
    }

    [Fact]
    public void PlaceAndDonorEnumerationOrderCannotChangeHousingAssignments()
    {
        var homes = Enumerable.Range(0, 5).Select(i => new Place { Id = "home:" + i, Kind = PlaceKind.Home, Capacity = 4 }).Concat(Places(20).Skip(1)).ToArray();
        var first = Population.CreateFromHousing(Donors(3), homes, "source", "0.40.4", 7, 1440);
        var second = Population.CreateFromHousing(Donors(3).Reverse(), homes.Reverse(), "source", "0.40.4", 7, 1440);
        Assert.Equal(JsonConvert.SerializeObject(first), JsonConvert.SerializeObject(second));
    }

    [Fact]
    public void ExpansionPreservesActiveJourneyAndAssignmentsAndSequencesAcrossTurnover()
    {
        var city = Housing(2, 10);
        var person = city.Citizens[0];
        person.Phase = CitizenPhase.Travelling; person.DestinationId = "work-a";
        person.DestinationActivity = Activity.Work; person.DepartureMinute = 1430;
        person.NextEventMinute = 1500; person.ActivityUntilMinute = 2000;
        person.Journey = new Route { Legs = new List<RouteLeg> { new() { From = new(0, 0, 0), To = new(100, 0, 0), Minutes = 70 } } };
        string before = JsonConvert.SerializeObject(city.Citizens);
        string sources = JsonConvert.SerializeObject(city.SourceIdentities);
        var existing = city.Citizens.ToArray();
        city.Places.Single(p => p.Kind == PlaceKind.Home).Capacity = 20;
        Population.ExpandToHousing(city);
        Assert.Equal(18, city.Citizens.Count);
        Assert.Equal(18, city.PopulationTarget);
        Assert.Equal(before, JsonConvert.SerializeObject(city.Citizens.Take(9)));
        for (int index = 0; index < existing.Length; index++) Assert.Same(existing[index], city.Citizens[index]);
        Assert.Equal("pc:16", city.Citizens.Last().Identity.Id);
        CheckAccounting(city);

        foreach (var resident in city.Citizens)
        { resident.NextDepartureMinute = ResidentLifecycle.Never; resident.NextJobChangeMinute = ResidentLifecycle.Never; }
        city.Citizens[1].NextDepartureMinute = city.Minute;
        new CitySimulator(city, new Direct()).AdvanceTo(city.Minute);
        Assert.Equal("pc:17", city.Citizens[1].Identity.Id);
        Assert.Equal(1, city.DepartedCount);
        CheckAccounting(city);
        Assert.Equal(sources, JsonConvert.SerializeObject(city.SourceIdentities));

        city.Places.Single(p => p.Kind == PlaceKind.Home).Capacity = 10;
        var retained = city.Citizens.ToArray();
        Population.ExpandToHousing(city);
        Assert.Equal(18, city.PopulationTarget);
        Assert.Equal(retained, city.Citizens);
        CheckAccounting(city);
    }

    [Fact]
    public void ExactCountFactoryAndTurnoverRespectFullHomeAndWorkplaceCapacities()
    {
        var places = Places(2);
        places[1].Capacity = 1; places[2].Capacity = 1;
        var city = Population.Create(Donors(2), places, "source", "0.40.4", 7, 1440);
        Assert.Equal(2, city.PopulationTarget);
        Assert.Equal(2, city.InitialImportedResidents);
        Assert.Equal(2, city.Citizens.Select(c => c.DayPlaceId).Distinct().Count());
        foreach (var person in city.Citizens) person.NextDepartureMinute = city.Minute;
        new CitySimulator(city, new Direct()).AdvanceTo(city.Minute);
        Assert.All(city.Places, p => Assert.True(city.Citizens.Count(c => c.HomeId == p.Id || c.DayPlaceId == p.Id) <= p.Capacity));
        CheckAccounting(city);
        Assert.Throws<InvalidOperationException>(() => Population.Create(Donors(3), places, "source", "0.40.4", 7, 1440));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(55557)]
    [InlineData(long.MaxValue)]
    public void EmptyOrUnreviewedHousingTargetsRefuseRatherThanSilentlyCap(long capacity)
        => Assert.Throws<InvalidOperationException>(() => HousingPolicy.Target(capacity));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(19.999, 0)]
    [InlineData(20, 1)]
    [InlineData(39.999, 1)]
    [InlineData(40, 2)]
    [InlineData(74.999, 2)]
    [InlineData(75, 4)]
    [InlineData(149.999, 4)]
    [InlineData(150, 5)]
    [InlineData(299.999, 5)]
    [InlineData(300, 7)]
    public void RentableInteriorOccupancyBandsHaveExplicitBoundaries(double area, int occupants)
        => Assert.Equal(occupants, HousingPolicy.Capacity(area));

    [Fact]
    public void VillasStayBoundedToSevenOccupantsRatherThanGrowingLinearlyWithArea()
    {
        Assert.Equal(5, HousingPolicy.Capacity(204));
        Assert.Equal(7, HousingPolicy.Capacity(589));
        Assert.Equal(7, HousingPolicy.Capacity(5890));
        Assert.Equal(7, HousingPolicy.Capacity(double.MaxValue));
        Assert.True(HousingPolicy.Capacity(589) / 589.0 < HousingPolicy.Capacity(54) / 54.0);
    }

    [Fact]
    public void InvalidInteriorAreasRefuseAndGlobalPopulationLimitStillApplies()
    {
        foreach (double area in new[] { -1.0, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => HousingPolicy.Capacity(area));
        Assert.Equal(HousingPolicy.MaximumResidents, HousingPolicy.Target(55556));
    }

    [Fact]
    public void CapturedRentableInteriorInventoryAllocates1064OccupantsAnd957InitialResidents()
    {
        int capacity = 182 * HousingPolicy.Capacity(54) + 105 * HousingPolicy.Capacity(96)
            + 28 * HousingPolicy.Capacity(204) + 20 * HousingPolicy.Capacity(589);
        Assert.Equal(1064, capacity);
        Assert.Equal(957, HousingPolicy.Target(capacity));
    }

    [Fact]
    public void EstablishedWorkersHaveStaggeredNineHundredDayWaitsWithoutANinetyDayQuietPeriod()
    {
        var workers = Enumerable.Range(0, 5000).Select(i => new CitizenState { Identity = Donors(1)[0] }).ToArray();
        for (int index = 0; index < workers.Length; index++)
        { workers[index].Identity.Id = "distribution:" + index; ResidentLifecycle.Initialize(workers[index], 7, 1440); }
        var days = workers.Select(c => (c.NextJobChangeMinute - 1440) / 1440).ToArray();
        Assert.All(days, value => Assert.True(value >= 1));
        Assert.InRange(days.Average(), 850, 950);
        Assert.InRange(days.Count(d => d <= 11), 25, 90);
        Assert.True(days.Count(d => d < 90) > 300);
        Assert.True(days.Distinct().Count() > 4990);
    }

    [Fact]
    public void SuccessfulJobChangeAndNewArrivalGetAtLeastNinetyDaysBeforeAnotherChange()
    {
        var city = Housing(2, 10);
        foreach (var person in city.Citizens)
        { person.NextDepartureMinute = ResidentLifecycle.Never; person.NextJobChangeMinute = city.Minute; }
        new CitySimulator(city, new Direct()).AdvanceTo(city.Minute);
        Assert.All(city.Citizens, c => { Assert.Equal(1, c.JobChanges); Assert.True(c.NextJobChangeMinute >= city.Minute + 90 * 1440); });
        city.Citizens[0].NextDepartureMinute = city.Minute;
        city.Citizens[0].NextEventMinute = city.Minute;
        new CitySimulator(city, new Direct()).AdvanceTo(city.Minute);
        Assert.Equal(0, city.Citizens[0].JobChanges);
        Assert.StartsWith("pc:", city.Citizens[0].Identity.Id);
        Assert.True(city.Citizens[0].NextJobChangeMinute >= city.Minute + 90 * 1440);
    }

    [Fact]
    public void LegacyRetuneOnlyStretchesFutureWorkerDatesAndPreservesPhysicalState()
    {
        var city = Population.Create(Donors(5), Places(5), "source", "0.40.4", 7, 1440);
        var future = city.Citizens[0];
        future.NextJobChangeMinute = city.Minute + 50;
        future.Phase = CitizenPhase.Travelling; future.DestinationId = "work-a"; future.DestinationActivity = Activity.Work;
        future.DepartureMinute = 1430; future.NextEventMinute = 1500; future.ActivityUntilMinute = 2000;
        future.Journey = new Route { Legs = new() { new() { From = new(0, 0, 0), To = new(100, 0, 0), Minutes = 70 } } };
        city.Citizens[1].NextJobChangeMinute = city.Minute;
        city.Citizens[2].NextJobChangeMinute = ResidentLifecycle.Never;
        city.Citizens[3].Identity.Persona = Persona.Student;
        city.Citizens[3].NextJobChangeMinute = city.Minute + 50;
        city.Citizens[4].NextJobChangeMinute = ResidentLifecycle.Never - 1;
        string before = JsonConvert.SerializeObject(city);
        ResidentLifecycle.RetuneLegacyJobs(city);
        Assert.Equal(city.Minute + 500, future.NextJobChangeMinute);
        Assert.Equal(city.Minute, city.Citizens[1].NextJobChangeMinute);
        Assert.Equal(ResidentLifecycle.Never, city.Citizens[2].NextJobChangeMinute);
        Assert.Equal(city.Minute + 50, city.Citizens[3].NextJobChangeMinute);
        Assert.Equal(ResidentLifecycle.Never, city.Citizens[4].NextJobChangeMinute);
        future.NextJobChangeMinute = city.Minute + 50;
        city.Citizens[4].NextJobChangeMinute = ResidentLifecycle.Never - 1;
        Assert.Equal(before, JsonConvert.SerializeObject(city));
    }

    private sealed class Direct : IRoutePlanner
    {
        public Route Plan(Place from, Place to, double minute, TravelPreferences preferences)
            => new() { Legs = new() { new() { From = from.Position, To = to.Position, Minutes = 1, Mode = TravelMode.Walk } } };
    }
}
