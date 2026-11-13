using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class PopulationCensusTests
{
    private static Place Home(string id, int beds, double area = 60) => new Place { Id = id, Kind = PlaceKind.Home, Capacity = beds, ResidentialAreaSquareMetres = area };
    private static Place Work(string id) => new Place { Id = id, Kind = PlaceKind.Work, Capacity = 16 };
    private static Place Study(string id) => new Place { Id = id, Kind = PlaceKind.Study, Capacity = 16 };
    private static CitizenState Citizen(string id, Persona persona, string home, string day = "") => new CitizenState
    {
        Identity = new CitizenIdentity { Id = id, Persona = persona }, HomeId = home, PlaceId = home, DayPlaceId = day
    };
    private static CensusResident Resident(string id, Persona persona, string home, string day = "")
        => PopulationCensusInput.CaptureResidents(new[] { Citizen(id, persona, home, day) })[0];
    private static CensusWorkplace Live(string work, double area, bool player = false, string study = "", int staff = -1)
        => new CensusWorkplace { WorkPlaceId = work, AreaSquareMetres = area, PlayerOwned = player, StudyPlaceId = study, NativeStaff = staff };

    private static PopulationCensusInput Mixed(IReadOnlyList<string>? stations = null) => new PopulationCensusInput
    {
        Places = new List<Place>
        {
            Home("h1", 4), Home("h2", 2), Home("h3", 7, 320),
            Work("w1"), Work("w2"), Work("w3"), Work("w4"), Work("s:work"), Study("s:study"),
            new Place { Id = "shop", Kind = PlaceKind.Shop, Capacity = 16 }
        },
        Workplaces = new List<CensusWorkplace>
        {
            Live("w1", 50, staff: 3), Live("w2", 700, staff: 9), Live("w3", 200, player: true), Live("s:work", 100, study: "s:study")
        },
        Residents = new List<CensusResident>
        {
            Resident("r1", Persona.Commuter, "h1", "w1"), Resident("r2", Persona.Parent, "h1", "w2"),
            Resident("r3", Persona.Student, "h1", "s:study"), Resident("r4", Persona.Retiree, "h2"),
            Resident("r5", Persona.Foodie, "h2", "w3"), Resident("r6", Persona.NightOwl, "h1", "w1"),
            Resident("r7", Persona.Sporty, "h3", "w4")
        },
        StationIds = stations ?? new[] { "subway:B", "subway:A", "subway:A" }
    };

    [Fact]
    public void Mixed_fixture_arithmetic()
    {
        var census = PopulationCensus.Compute(Mixed());
        Assert.False(census.Refused);
        Assert.Equal(3, census.HomeInteriors);
        Assert.Equal(13, census.Beds);
        Assert.Equal(11, census.ResidentTarget);
        Assert.Equal(HousingPolicy.Target(13), census.ResidentTarget);
        Assert.Equal(7, census.Residents);
        Assert.Equal(3, census.Households);
        Assert.Equal(0, census.OverBandHomes);
        Assert.Equal(5, census.Workplaces);
        Assert.Equal(1, census.PlayerOwnedWorkplaces);
        Assert.Equal(10, census.PlayerOwnedCapacity);
        Assert.Equal(4 + 24 + 0 + 6, census.WorkplaceCapacity);
        Assert.Equal(1, census.UnknownAreaWorkplaces);
        Assert.Equal(1, census.UnmatchedWorkplaces);
        Assert.Equal(1, census.OverCapacityWorkplaces);
        Assert.Equal(2, census.NativeStaffWorkplaces);
        Assert.Equal(12, census.NativeStaff);
        Assert.Equal(1, census.StudyPlaces);
        Assert.Equal(12, census.StudyCapacity);
        Assert.Equal(5, census.ResidentWorkers);
        Assert.Equal(1, census.ResidentWorkersAtPlayerOwned);
        Assert.Equal(1, census.Students);
        Assert.Equal(2, census.Stations);
        Assert.Equal(27 - 4, census.CommuterDemand);
        Assert.Equal(14, census.VisitorCapacity);
        Assert.Equal(14, census.CommuterTarget);
        Assert.True(census.CommuterClamped);
        Assert.Equal(1, census.DayVisitorsPerDay);
        Assert.Equal(4, census.DayVisitorPoolDemand);
        Assert.Equal(0, census.DayVisitorPoolTarget);
        Assert.True(census.PoolClamped);
        Assert.Equal(0, census.DayVisitorsPresent);
        Assert.Equal(21, census.PersistedPersons);
        Assert.Equal(2 * (5 + 1 + 14), census.ImpliedCommuteTrips);
        Assert.Equal(0, census.ImpliedVisitTrips);
        Assert.Equal(3 * (7 + 14), census.ImpliedMealIntents);
        Assert.Contains("clamped=commuters,pool\n", census.Summary());
    }

    [Fact]
    public void Empty_world_is_all_zero_and_stations_pending()
    {
        var census = PopulationCensus.Compute(new PopulationCensusInput());
        Assert.False(census.Refused);
        Assert.Equal(0, census.Beds);
        Assert.Equal(0, census.ResidentTarget);
        Assert.Equal(0, census.PersistedPersons);
        Assert.False(census.StationsKnown);
        Assert.Contains("discovered_stations=pending\n", census.Summary());
        Assert.Equal("", census.WorkplaceDetail());
    }

    [Fact]
    public void Homes_only_city_has_day_visitors_but_no_commuters()
    {
        var places = new List<Place> { Home("a", 7, 400), Home("b", 5, 200) };
        var residents = Enumerable.Range(0, 100).Select(i => Resident("r" + i.ToString("D3"), Persona.Commuter, i % 2 == 0 ? "a" : "b")).ToList();
        var census = PopulationCensus.Compute(new PopulationCensusInput { Places = places, Residents = residents, StationIds = new[] { "subway:A" } });
        Assert.Equal(12, census.Beds);
        Assert.Equal(2, census.Households);
        Assert.Equal(2, census.OverBandHomes);
        Assert.True(census.HouseholdsWithinBeds);
        Assert.Equal(0, census.Workplaces);
        Assert.Equal(0, census.CommuterDemand);
        Assert.Equal(15, census.DayVisitorsPerDay);
        Assert.Equal(60, census.DayVisitorPoolDemand);
        Assert.Equal(60, census.DayVisitorPoolTarget);
        Assert.False(census.PoolClamped);
        Assert.Equal(15, census.DayVisitorsPresent);
        Assert.Equal(160, census.PersistedPersons);
        Assert.Equal(30, census.ImpliedVisitTrips);
        Assert.Equal(3 * 115, census.ImpliedMealIntents);
    }

    [Fact]
    public void No_stations_leaves_visitors_at_zero()
    {
        foreach (var stations in new[] { Array.Empty<string>(), new[] { "", "" } })
        {
            var census = PopulationCensus.Compute(Mixed(stations));
            Assert.True(census.StationsKnown);
            Assert.Equal(0, census.Stations);
            Assert.Equal(23, census.CommuterDemand);
            Assert.Equal(0, census.CommuterTarget);
            Assert.Equal(0, census.DayVisitorPoolTarget);
            Assert.Equal(0, census.DayVisitorsPresent);
            Assert.False(census.CommuterClamped);
            Assert.Equal(census.Residents, census.PersistedPersons);
        }
        var pending = PopulationCensus.Compute(new PopulationCensusInput { Places = Mixed().Places, Workplaces = Mixed().Workplaces, Residents = Mixed().Residents });
        Assert.False(pending.StationsKnown);
        Assert.Equal(0, pending.CommuterTarget);
    }

    [Fact]
    public void Over_bound_and_invalid_inputs_refuse_without_counts()
    {
        var tooManyPlaces = Enumerable.Range(0, PopulationCensusPolicy.MaximumPlaces + 1).Select(i => Home("h" + i, 1)).ToList();
        var exactPlaces = tooManyPlaces.Take(PopulationCensusPolicy.MaximumPlaces).ToList();
        Assert.False(PopulationCensus.Compute(new PopulationCensusInput { Places = exactPlaces }).Refused);
        AssertRefused(new PopulationCensusInput { Places = tooManyPlaces }, "places");

        var exactResidents = Enumerable.Range(0, HousingPolicy.MaximumResidents).Select(i => Resident("r" + i, Persona.Commuter, "h")).ToList();
        var homes = new List<Place> { Home("h", 50000) };
        Assert.False(PopulationCensus.Compute(new PopulationCensusInput { Places = homes, Residents = exactResidents }).Refused);
        AssertRefused(new PopulationCensusInput { Places = homes, Residents = exactResidents.Append(Resident("x", Persona.Commuter, "h")).ToList() }, "residents");

        Assert.False(PopulationCensus.Compute(new PopulationCensusInput { Places = new List<Place> { Home("h", 55556) } }).Refused);
        var huge = PopulationCensus.Compute(new PopulationCensusInput { Places = new List<Place> { Home("h", 55557) } });
        Assert.False(huge.Refused);
        Assert.Equal(CityBalance.CityResidentTotal, huge.ResidentTarget);

        var stations = Enumerable.Range(0, PopulationCensusPolicy.MaximumStations).Select(i => "subway:" + i).ToList();
        Assert.False(PopulationCensus.Compute(new PopulationCensusInput { StationIds = stations }).Refused);
        AssertRefused(new PopulationCensusInput { StationIds = stations.Append("subway:extra").ToList() }, "stations");

        AssertRefused(new PopulationCensusInput { Places = new List<Place> { Home("h", 1), Work("h") } }, "duplicate place");
        AssertRefused(new PopulationCensusInput { Workplaces = new List<CensusWorkplace> { Live("w", 1), Live("w", 2) } }, "duplicate workplace");
        AssertRefused(new PopulationCensusInput { Places = new List<Place> { new Place { Id = "h", Kind = PlaceKind.Home, Capacity = -1 } } }, "negative capacity");

        static void AssertRefused(PopulationCensusInput input, string reason)
        {
            var census = PopulationCensus.Compute(input);
            Assert.True(census.Refused);
            Assert.StartsWith(reason, census.RefusalReason);
            Assert.Equal(0, census.Beds);
            Assert.Equal(0, census.PersistedPersons);
            Assert.Equal("policy=" + PopulationCensusPolicy.PolicyId + "\nrefused=" + census.RefusalReason + "\n", census.Summary());
        }
    }

    [Fact]
    public void Persons_bound_holds_at_maximum_residents()
    {
        var residents = Enumerable.Range(0, HousingPolicy.MaximumResidents).Select(i => Resident("r" + i, Persona.Commuter, "h")).ToList();
        var places = new List<Place> { Home("h", 55556) };
        places.AddRange(Enumerable.Range(0, 10000).Select(i => Work("w" + i.ToString("D5"))));
        var live = Enumerable.Range(0, 10000).Select(i => Live("w" + i.ToString("D5"), 1000)).ToList();
        var census = PopulationCensus.Compute(new PopulationCensusInput { Places = places, Workplaces = live, Residents = residents, StationIds = new[] { "subway:A" } });
        Assert.False(census.Refused);
        Assert.Equal(100000, census.VisitorCapacity);
        Assert.Equal(100000, census.CommuterTarget);
        Assert.Equal(0, census.DayVisitorPoolTarget);
        Assert.Equal(PopulationCensusPolicy.MaximumPersons, census.PersistedPersons);
    }

    [Fact]
    public void Ordering_permutations_give_identical_reports()
    {
        string summary = PopulationCensus.Compute(Mixed()).Summary();
        string detail = PopulationCensus.Compute(Mixed()).WorkplaceDetail();
        var random = new Random(92);
        for (int trial = 0; trial < 25; trial++)
        {
            var source = Mixed();
            var shuffled = new PopulationCensusInput
            {
                Places = source.Places.OrderBy(_ => random.Next()).ToList(),
                Workplaces = source.Workplaces.OrderBy(_ => random.Next()).ToList(),
                Residents = source.Residents.OrderBy(_ => random.Next()).ToList(),
                StationIds = source.StationIds!.OrderBy(_ => random.Next()).ToList()
            };
            var census = PopulationCensus.Compute(shuffled);
            Assert.Equal(summary, census.Summary());
            Assert.Equal(detail, census.WorkplaceDetail());
        }
        Assert.Matches(new Regex("\nhash=[0-9a-f]{8}\n$"), summary);
        var lines = detail.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Split(' ')[0]).ToList();
        Assert.Equal(lines.OrderBy(l => l, StringComparer.Ordinal), lines);
    }

    [Theory]
    [InlineData(-1, 0)] [InlineData(0, 0)] [InlineData(double.NaN, 0)] [InlineData(double.PositiveInfinity, 0)]
    [InlineData(0.5, 2)] [InlineData(39.999, 2)] [InlineData(40, 4)] [InlineData(74.999, 4)] [InlineData(75, 6)]
    [InlineData(149.999, 6)] [InlineData(150, 10)] [InlineData(299.999, 10)] [InlineData(300, 16)]
    [InlineData(599.999, 16)] [InlineData(600, 24)] [InlineData(5000, 24)]
    public void Workplace_band_edges(double area, int capacity)
    {
        Assert.Equal(capacity, PopulationCensusPolicy.WorkplaceCapacity(area));
        Assert.Equal(capacity * 2, PopulationCensusPolicy.StudyCapacity(area));
    }

    [Fact]
    public void Consistency_rules_report_jobs_and_households()
    {
        var census = PopulationCensus.Compute(Mixed());
        Assert.True(census.Households <= census.Beds);
        Assert.True(census.HouseholdsWithinBeds);
        Assert.True(census.JobsWithinCapacity);
        Assert.True(census.StudentsWithinCapacity);
        Assert.True(census.CommuterTarget + census.DayVisitorPoolTarget <= census.VisitorCapacity);
        Assert.True(census.CommuterTarget >= 0 && census.DayVisitorPoolTarget >= 0);
        Assert.Contains("check_households_within_beds=ok\n", census.Summary());

        var crowded = new PopulationCensusInput
        {
            Places = new List<Place> { Home("h", 30), Work("w"), Study("s") },
            Workplaces = new List<CensusWorkplace> { Live("w", 50) },
            Residents = Enumerable.Range(0, 20).Select(i => Resident("r" + i, Persona.Commuter, "h", "w"))
                .Append(Resident("s1", Persona.Student, "h", "s")).ToList()
        };
        var over = PopulationCensus.Compute(crowded);
        Assert.False(over.JobsWithinCapacity);
        Assert.False(over.StudentsWithinCapacity);
        Assert.Equal(1, over.OverCapacityWorkplaces);
        Assert.Equal(0, over.CommuterDemand);
        Assert.Contains("check_jobs_within_capacity=failed\n", over.Summary());

        var homesWithoutBeds = PopulationCensus.Compute(new PopulationCensusInput
        {
            Places = new List<Place> { Home("h", 0) }, Residents = new List<CensusResident> { Resident("r", Persona.Commuter, "h") }
        });
        Assert.False(homesWithoutBeds.HouseholdsWithinBeds);
        Assert.Equal(1, homesWithoutBeds.OverBandHomes);
        var homeless = PopulationCensus.Compute(new PopulationCensusInput { Residents = new List<CensusResident> { Resident("r", Persona.Commuter, "missing") } });
        Assert.Equal(1, homeless.ResidentsWithoutHome);
        Assert.Equal(0, homeless.Households);
    }

    [Fact]
    public void Player_owned_workplaces_are_flagged_and_excluded()
    {
        var input = Mixed();
        var census = PopulationCensus.Compute(input);
        var player = census.WorkplaceLines.Single(l => l.PlayerOwned);
        Assert.Equal("w3", player.PlaceId);
        Assert.Equal(10, player.Capacity);
        Assert.Equal(1, player.ResidentsAssigned);
        Assert.Contains("w3 area=200 capacity=10 study=0 assigned=1 owner=player native_staff=unknown\n", census.WorkplaceDetail());
        Assert.Contains("player_owned_workplaces=1\nplayer_owned_capacity=10\nplayer_owned_flag=excluded-from-assignment\n", census.Summary());
        Assert.Contains("workplaces=4\nworkplace_capacity=34\n", census.Summary());

        input.Workplaces = input.Workplaces.Select(w => w.WorkPlaceId == "w3" ? Live("w3", 200) : w).ToList();
        var other = PopulationCensus.Compute(input);
        Assert.Equal(0, other.PlayerOwnedWorkplaces);
        Assert.Equal(census.WorkplaceCapacity + 10, other.WorkplaceCapacity);
        Assert.Equal(0, other.ResidentWorkersAtPlayerOwned);
        Assert.Equal(census.ResidentWorkers, other.ResidentWorkers);
        Assert.Equal((44 * 80 + 50) / 100 - 5, other.CommuterDemand);
    }

    [Fact]
    public void Census_does_not_change_its_inputs()
    {
        var input = Mixed();
        string before = Snapshot(input);
        var first = PopulationCensus.Compute(input).Summary();
        Assert.Equal(before, Snapshot(input));
        Assert.Equal(first, PopulationCensus.Compute(input).Summary());

        static string Snapshot(PopulationCensusInput value) => string.Join("|", value.Places.Select(p => p.Id + ":" + p.Kind + ":" + p.Capacity))
            + "#" + string.Join("|", value.Workplaces.Select(w => w.WorkPlaceId + ":" + w.AreaSquareMetres + ":" + w.PlayerOwned + ":" + w.NativeStaff))
            + "#" + string.Join("|", value.Residents.Select(r => r.HomeId + ":" + r.DayPlaceId + ":" + r.Persona))
            + "#" + string.Join("|", value.StationIds!);
    }

    [Fact]
    public void Policy_bounds_match_existing_contracts()
    {
        Assert.Equal(SaveCodec.MaxPlaces, PopulationCensusPolicy.MaximumPlaces);
        Assert.Equal(SaveCodec.MaxCitizens, HousingPolicy.MaximumResidents);
        Assert.Equal(PopulationCensusPolicy.MaximumPersons, HousingPolicy.MaximumResidents + PopulationCensusPolicy.MaximumVisitors);
        Assert.Equal(PopulationCensusPolicy.MaximumPersons, SaveCodec.MaxCitizens * 3);
        Assert.Equal(3, Enum.GetValues(typeof(MealKind)).Length - 1);
        Assert.False(PopulationCensusPolicy.VisitorsDrive);
        Assert.True(PopulationCensusPolicy.ExcludePlayerOwnedWorkplaces);
    }

    [Fact]
    public void Business_established_at_a_saved_vacant_address_is_counted_from_the_load_catalogue()
    {
        var saved = new List<Place> { Home("h", 4), new Place { Id = "address:Main:1:shop", Kind = PlaceKind.Shop, Capacity = 16 } };
        var fresh = new List<Place> { Home("h", 4), new Place { Id = "address:Main:1:shop", Kind = PlaceKind.Shop, Capacity = 16 }, Work("address:Main:1:work") };
        var live = new List<CensusWorkplace> { Live("address:Main:1:work", 120, player: true) };
        var residents = new List<CensusResident> { Resident("r1", Persona.Commuter, "h") };
        var stale = PopulationCensus.Compute(new PopulationCensusInput { Places = saved, Workplaces = live, Residents = residents, StationIds = new[] { "subway:A" } });
        Assert.Equal(0, stale.Workplaces);
        var census = PopulationCensus.Compute(new PopulationCensusInput { Places = PopulationCensusInput.CapturePlaces(fresh), Workplaces = live, Residents = residents, StationIds = new[] { "subway:A" } });
        Assert.Equal(1, census.Workplaces);
        Assert.Equal(1, census.PlayerOwnedWorkplaces);
        Assert.Equal(6, census.PlayerOwnedCapacity);
        Assert.Equal(0, census.UnmatchedWorkplaces);
        Assert.Equal(1, census.Households);
        Assert.Contains("address:Main:1:work area=120 capacity=6 study=0 assigned=0 owner=player", census.WorkplaceDetail());
    }

    [Fact]
    public void Captured_inputs_are_detached_from_live_state()
    {
        var citizens = new List<CitizenState> { Citizen("r1", Persona.Commuter, "h1", "w1"), Citizen("r2", Persona.Student, "h1", "s:study") };
        var places = Mixed().Places;
        var input = new PopulationCensusInput
        {
            Places = PopulationCensusInput.CapturePlaces(places), Workplaces = Mixed().Workplaces,
            Residents = PopulationCensusInput.CaptureResidents(citizens), StationIds = new[] { "subway:A" }
        };
        string before = PopulationCensus.Compute(input).Summary();
        citizens[0].HomeId = "h2"; citizens[0].DayPlaceId = ""; citizens.Add(Citizen("r3", Persona.Commuter, "h3"));
        foreach (var place in places) place.Capacity = 99;
        Assert.Equal(before, PopulationCensus.Compute(input).Summary());
        Assert.Equal(2, input.Residents.Count);
        Assert.Equal(new CensusResident("h1", "w1", Persona.Commuter), input.Residents[0]);
    }

    private static string Root()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Runtime", "PopulationCensusDiagnostics.cs"))) root = root.Parent;
        return root?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

}
