using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class Gr5PopulationTableTests
{
    private static CitizenIdentity[] Donors(int count) => Enumerable.Range(0, count).Select(i => new CitizenIdentity
    {
        Id = "source:" + i.ToString("D5"), Name = "Given" + i + " Family" + i,
        District = "district", Age = 25 + i % 40, Persona = Persona.Commuter, AppearanceSeed = 100 + i
    }).ToArray();

    private static List<Place> Catalogue(int unrentableBlocks, double footprint, double floors)
    {
        var tally = new UnrentableHomeTally();
        var places = new List<Place>
        {
            new Place { Id = "home-rentable", Kind = PlaceKind.Home, Capacity = HousingPolicy.Capacity(54), ResidentialAreaSquareMetres = 54, District = "a" },
            new Place { Id = "work-a", Kind = PlaceKind.Work, Capacity = 100000, Position = new(100, 0, 0) },
            new Place { Id = "shop-a", Kind = PlaceKind.Shop, Capacity = 16, Position = new(50, 0, 0), BusinessType = "ba:businesstype_clothingstore" },
            new Place { Id = "shop-b", Kind = PlaceKind.Shop, Capacity = 16, Position = new(60, 0, 0), BusinessType = "ba:businesstype_coffeeshop" },
            new Place { Id = "shop-c", Kind = PlaceKind.Shop, Capacity = 16, Position = new(70, 0, 0), BusinessType = "ba:businesstype_furniturestore" },
        };
        for (int i = 0; i < unrentableBlocks; i++)
        {
            int beds = tally.Add(footprint, floors);
            if (beds > 0) places.Add(new Place { Id = "home-block-" + i.ToString("D4"), Kind = PlaceKind.Home, Capacity = beds, District = "a",
                Position = new(i, 0, 10), EvidenceSource = HousingPolicy.UnrentablePolicyId });
        }
        return places;
    }

    [Theory]
    [InlineData(400, 5, 40)]
    [InlineData(400, 5.9, 40)]
    [InlineData(100, 1, 2)]
    [InlineData(49.9, 1, 0)]
    [InlineData(1000, 12, 240)]
    public void SecondCapacityRuleFloorsFootprintTimesFloorsTimesShareOverSpacePerPerson(double footprint, double floors, int expected)
    {
        Assert.True(HousingPolicy.TryUnrentableCapacity(footprint, floors, out int capacity));
        Assert.Equal(expected, capacity);
        Assert.Equal((int)Math.Floor(footprint * Math.Floor(floors) * CityBalance.UnrentableHomeUsableShare / CityBalance.UnrentableHomeSquareMetresPerPerson), capacity);
    }

    [Fact]
    public void MissingOrNonFiniteInputHousesNobodyAndIsCounted()
    {
        var bad = new (double Footprint, double Floors)[]
        {
            (double.NaN, 3), (400, double.NaN), (double.PositiveInfinity, 3), (400, double.NegativeInfinity),
            (0, 3), (-10, 3), (400, 0), (400, 0.5), (double.MaxValue, double.MaxValue)
        };
        var tally = new UnrentableHomeTally();
        foreach (var (footprint, floors) in bad)
        {
            Assert.False(HousingPolicy.TryUnrentableCapacity(footprint, floors, out int capacity));
            Assert.Equal(0, capacity);
            Assert.Equal(0, tally.Add(footprint, floors));
        }
        Assert.Equal(bad.Length, tally.Unreadable);
        Assert.Equal(0, tally.Housed); Assert.Equal(0, tally.Beds);
        Assert.Equal(0, tally.Add(10, 1));
        Assert.Equal(1, tally.Empty);
        Assert.Equal(40, tally.Add(400, 5));
        Assert.Equal(1, tally.Housed); Assert.Equal(40, tally.Beds);
        Assert.Contains("unreadable=" + bad.Length, tally.Summary());
    }

    [Fact]
    public void FillKeepsNinetyPercentAndTheCityTotalAndMaximumResidents()
    {
        Assert.True(CityBalance.CityResidentTotal <= HousingPolicy.MaximumResidents);
        Assert.True(CityBalance.CityResidentTotal + 2L * CityBalance.CityResidentTotal <= PopulationCensusPolicy.MaximumPersons);
        Assert.Equal(9, HousingPolicy.CityTarget(10));
        Assert.Equal(HousingPolicy.Target(1064), HousingPolicy.CityTarget(1064));
        Assert.Equal(CityBalance.CityResidentTotal, HousingPolicy.CityTarget(1_000_000));
        Assert.Equal(CityBalance.CityResidentTotal, HousingPolicy.CityTarget(long.MaxValue));
        Assert.Throws<InvalidOperationException>(() => HousingPolicy.CityTarget(0));
        Assert.Throws<InvalidOperationException>(() => HousingPolicy.CityTarget(1));

        var small = Population.CreateFromHousing(Donors(50), Catalogue(5, 400, 5), "source", "0.40.4", 7, 1440);
        long beds = small.Places.Where(p => p.Kind == PlaceKind.Home).Sum(p => (long)p.Capacity);
        Assert.Equal(202, beds);
        Assert.Equal(HousingPolicy.Target(beds), small.Citizens.Count);

        var large = Population.CreateFromHousing(Donors(50), Catalogue(200, 1000, 12), "source", "0.40.4", 7, 1440);
        Assert.Equal(CityBalance.CityResidentTotal, large.Citizens.Count);
        Assert.Equal(CityBalance.CityResidentTotal, large.PopulationTarget);
    }

    [Fact]
    public void EveryResidentHasAHomeAndAPlaceToInspect()
    {
        var city = Population.CreateFromHousing(Donors(30), Catalogue(40, 600, 6), "source", "0.40.4", 7, 1440);
        var byId = city.Places.ToDictionary(p => p.Id, StringComparer.Ordinal);
        var used = new Dictionary<string, int>(StringComparer.Ordinal);
        Assert.Contains(city.Citizens, c => c.HomeId.StartsWith("home-block-", StringComparison.Ordinal));
        foreach (var person in city.Citizens)
        {
            Assert.False(string.IsNullOrWhiteSpace(person.Identity.Name));
            Assert.True(byId.TryGetValue(person.HomeId, out var home) && home.Kind == PlaceKind.Home, person.Identity.Id);
            Assert.True(byId.ContainsKey(person.PlaceId), person.Identity.Id);
            used[person.HomeId] = used.TryGetValue(person.HomeId, out int n) ? n + 1 : 1;
        }
        foreach (var (home, count) in used) Assert.True(count <= byId[home].Capacity, home);
    }

    [Fact]
    public void CensusIsReproducibleFromTheSameInputs()
    {
        var places = Catalogue(25, 500, 4);
        var city = Population.CreateFromHousing(Donors(40), places, "source", "0.40.4", 7, 1440);
        PopulationCensus Run() => PopulationCensus.Compute(new PopulationCensusInput
        {
            Places = PopulationCensusInput.CapturePlaces(city.Places),
            Residents = PopulationCensusInput.CaptureResidents(city.Citizens),
            StationIds = new[] { "subway:A", "subway:B" }
        });
        var first = Run(); var second = Run();
        Assert.False(first.Refused);
        Assert.Equal(first.Summary(), second.Summary());
        Assert.Contains("visitor_groups=" + VisitorTable.Describe() + "\n", first.Summary());
        var again = Population.CreateFromHousing(Donors(40), Catalogue(25, 500, 4), "source", "0.40.4", 7, 1440);
        Assert.Equal(JsonConvert.SerializeObject(city.Citizens), JsonConvert.SerializeObject(again.Citizens));
    }

    [Fact]
    public void VisitorTableSharesAndWeekdaysAreComplete()
    {
        Assert.Equal(10000, VisitorTable.Groups.Sum(g => g.ShareBasisPoints));
        Assert.Equal(VisitorTable.Groups.Count, VisitorTable.Groups.Select(g => g.Id).Distinct(StringComparer.Ordinal).Count());
        foreach (var group in VisitorTable.Groups)
            Assert.All(group.Leaning.Values, w => Assert.True(w >= 1 && w <= 4));
        for (int day = 0; day < 7; day++)
        {
            Assert.InRange(VisitorTable.BasisPoints(VisitorPattern.DayVisit, day), 0, 10000);
            Assert.InRange(VisitorTable.BasisPoints(VisitorPattern.Commuter, day), 0, 10000);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => VisitorTable.BasisPoints(VisitorPattern.DayVisit, 7));
        var counts = VisitorTable.Groups.ToDictionary(g => g.Id, _ => 0);
        for (int n = 1; n <= 20000; n++) counts[VisitorTable.GroupOf(PersonIds.Visitor(n), 7).Id]++;
        foreach (var group in VisitorTable.Groups) Assert.InRange(counts[group.Id], group.ShareBasisPoints * 2 - 400, group.ShareBasisPoints * 2 + 400);
        Assert.Same(VisitorTable.GroupOf("pc:v:12", 7), VisitorTable.GroupOf("pc:v:12", 7));
    }

    [Fact]
    public void AGroupChangesWhoComesButNeverAShopsPriceOrPayment()
    {
        Assert.Equal(new[] { "Id", "Leaning", "ShareBasisPoints" },
            typeof(VisitorGroup).GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray());
        var city = Population.CreateFromHousing(Donors(30), Catalogue(10, 400, 5), "source", "0.40.4", 7, 1440);
        city.Food = EconomyScenario.Food();
        PortalPlaces.Add(city, VisitorRoutineTests.Stations);
        string places = JsonConvert.SerializeObject(city.Places), food = JsonConvert.SerializeObject(city.Food);
        var visitors = VisitorSchedules.Create(city, 600);
        Assert.Equal(places, JsonConvert.SerializeObject(city.Places));
        Assert.Equal(food, JsonConvert.SerializeObject(city.Food));
        var shopType = city.Places.Where(p => p.Kind == PlaceKind.Shop).ToDictionary(p => p.Id, p => p.BusinessType, StringComparer.Ordinal);
        foreach (var group in VisitorTable.Groups)
        {
            var members = visitors.Where(v => VisitorTable.GroupOf(v.Identity.Id, city.Seed) == group).ToList();
            Assert.NotEmpty(members);
            var favoured = group.Leaning.Keys.ToHashSet(StringComparer.Ordinal);
            double share = members.Count(v => favoured.Contains(shopType[v.ShopId])) / (double)members.Count;
            Assert.True(share > 1 / 3.0, group.Id + " " + share);
        }
    }
}

public sealed class Gr5SaveMeasure
{
    [Fact]
    public void MeasureSaveEncodeAtCitySizes()
    {
        string? path = PerfGate.Variable("PC_GR5_MEASURE");
        if (string.IsNullOrEmpty(path)) return;
        foreach (int residents in new[] { 957, CityBalance.CityResidentTotal, 3500 })
        {
            var state = EconomyScenario.Create(residents, RepresentativeFixture.Seed);
            PortalPlaces.Add(state, RepresentativeFixture.Portals);
            VisitorSchedules.Create(state, 2 * residents);
            state.Food = new FoodCommerceState { MealRhythmVersion = MealRhythm.PolicyVersion, HouseholdFinanceVersion = HouseholdFinance.PolicyVersion,
                Shops = Enumerable.Range(1, 3).Select(i => new FoodScope { PlaceId = "shop-" + i, BusinessId = "b" + i, ProductId = FoodProducts.Croissant }).ToList() };
            new CitySimulator(state, new FreshCityTraceTests.Walk()).AttachFoodMarket(new VisitorRoutineTests.Market { Shops = F5gPopulationFillTests.Shops(state) });
            string payload = SaveCodec.Write(state);
            var times = new List<double>();
            for (int i = 0; i < 21; i++)
            {
                long started = System.Diagnostics.Stopwatch.GetTimestamp();
                payload = SaveCodec.Write(state);
                times.Add((System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            }
            times.Sort();
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            SaveCodec.Write(state);
            allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
            System.IO.File.AppendAllText(path, JsonConvert.SerializeObject(new
            {
                residents, visitors = state.Visitors.Count, persons = state.Citizens.Count + state.Visitors.Count,
                payloadChars = payload.Length, encodeMedianMs = times[10], encodeP95Ms = times[19], encodeMaxMs = times[20], allocatedBytes = allocated
            }) + Environment.NewLine);
        }
    }
}
