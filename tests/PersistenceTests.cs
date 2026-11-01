using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class PersistenceTests
{
    [Fact]
    public async System.Threading.Tasks.Task ConcurrentCodecsShareOnlyMetadataAndStillRejectInvalidState()
    {
        string expected = SaveCodec.Write(Example());
        var operations = Enumerable.Range(0, 32).Select(_ => System.Threading.Tasks.Task.Run(() =>
        {
            var loaded = SaveCodec.Read(expected);
            Assert.Equal(SaveReadStatus.Loaded, loaded.Status);
            Assert.Equal(expected, SaveCodec.Write(loaded.State!));
            loaded.State!.Citizens[0].NextEventMinute = double.NaN;
            Assert.Throws<SaveValidationException>(() => SaveCodec.Write(loaded.State));
            Assert.Equal(expected, SaveCodec.Write(Example()));
        }));
        await System.Threading.Tasks.Task.WhenAll(operations);
    }

    [Fact]
    public void MidJourneyRoundTripPreservesExactPositionIdentityAndVisits()
    {
        var original = Example();
        string encoded = SaveCodec.Write(original);
        var result = SaveCodec.Read(encoded);
        Assert.Equal(SaveReadStatus.Loaded, result.Status);
        var loaded = Assert.IsType<CityState>(result.State);
        var before = original.Citizens[0]; var after = loaded.Citizens[0];
        Assert.Equal(before.Identity.Id, after.Identity.Id);
        Assert.Equal(before.Identity.AppearanceSeed, after.Identity.AppearanceSeed);
        var expected = before.Journey!.PositionAt(original.Minute - before.DepartureMinute);
        var actual = after.Journey!.PositionAt(loaded.Minute - after.DepartureMinute);
        Assert.Equal(expected.X, actual.X); Assert.Equal(expected.Y, actual.Y); Assert.Equal(expected.Z, actual.Z);
        Assert.NotEqual(0, actual.X); Assert.NotEqual(0, actual.Y); Assert.NotEqual(0, actual.Z);
        Assert.Equal(before.RecentVisits[0].PlaceId, after.RecentVisits[0].PlaceId);
        Assert.Equal(encoded, SaveCodec.Write(loaded));
        Assert.Equal(encoded, SaveCodec.Write(original));
    }

    [Fact]
    public void InterruptedJourneyResumesToSameStateAsContinuousSimulation()
    {
        var continuous = new CitySimulator(Example(), new DirectRoutes());
        continuous.AdvanceTo(20.75);
        Assert.Equal(CitizenPhase.Travelling, continuous.State.Citizens[0].Phase);
        var read = SaveCodec.Read(SaveCodec.Write(continuous.State));
        Assert.Equal(SaveReadStatus.Loaded, read.Status);
        var resumed = new CitySimulator(read.State!, new DirectRoutes());
        continuous.AdvanceTo(2500);
        foreach (double minute in new[] { 35.75, 200, 700, 1440, 2000, 2500 }) resumed.AdvanceTo(minute);
        Assert.True(continuous.State.Citizens[0].CompletedJourneys > 1);
        Assert.Equal(SaveCodec.Write(continuous.State), SaveCodec.Write(resumed.State));
    }

    [Theory]
    [InlineData("future", SaveReadStatus.Future)]
    [InlineData("{\"Schema\":1,", SaveReadStatus.Invalid)]
    [InlineData("{\"Schema\":9999999999999999999999999999999999}", SaveReadStatus.Invalid)]
    [InlineData("{}", SaveReadStatus.Invalid)]
    [InlineData("", SaveReadStatus.Invalid)]
    [InlineData("{\"Schema\":1,\"Schema\":2}", SaveReadStatus.Invalid)]
    public void InvalidOrFuturePayloadIsPreservedAndCannotBeOverwritten(string payload, SaveReadStatus expected)
    {
        if (payload == "future") payload = "{\"Schema\":" + (CityState.CurrentSchema + 1) + "}";
        var data = new Dictionary<string, string> { [SaveStore.StateKey] = payload, ["rlo:resident"] = "unchanged" };
        var result = SaveStore.ReadFrom(data, "population-a");
        Assert.Equal(expected, result.Status); Assert.Null(result.State); Assert.NotEmpty(result.Error);
        Assert.False(SaveStore.WriteTo(data, Example()).Success);
        Assert.Equal(payload, data[SaveStore.StateKey]); Assert.Equal("unchanged", data["rlo:resident"]);
    }

    [Fact]
    public void MissingAndUnsupportedOldVersionAreDistinct()
    {
        Assert.Equal(SaveReadStatus.Missing, SaveCodec.Read(null).Status);
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read("{\"Schema\":0}").Status);
        var data = new Dictionary<string, string>();
        Assert.Equal(SaveReadStatus.Missing, SaveStore.ReadFrom(data, "population-a").Status);
        Assert.True(SaveStore.WriteTo(data, Example()).Success);
        Assert.Equal(SaveReadStatus.Loaded, SaveStore.ReadFrom(data, "population-a").Status);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonfiniteValuesAreRejectedByReaderAndWriter(double invalid)
    {
        var state = Example(); state.Minute = invalid;
        Assert.Throws<SaveValidationException>(() => SaveCodec.Write(state));
        var json = JObject.Parse(SaveCodec.Write(Example())); json["Minute"] = invalid;
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(json.ToString()).Status);
        state = Example(); state.Places[0].Position = new Position(invalid, 2, 3);
        Assert.Throws<SaveValidationException>(() => SaveCodec.Write(state));
        state = Example(); state.Citizens[0].Journey!.Legs[0].Minutes = invalid;
        Assert.Throws<SaveValidationException>(() => SaveCodec.Write(state));
    }

    [Fact]
    public void MissingPositionComponentCannotSilentlyBecomeZero()
    {
        var json = JObject.Parse(SaveCodec.Write(Example()));
        ((JObject)json["Places"]![0]!["Position"]!).Remove("X");
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(json.ToString()).Status);
    }

    [Fact]
    public void DuplicateIdsBrokenReferencesWrongRolesAndUnknownEnumsAreRejected()
    {
        Reject(state => state.Places.Add(state.Places[0]));
        Reject(state => state.Citizens.Add(state.Citizens[0]));
        Reject(state => state.Citizens[0].HomeId = "missing");
        Reject(state => state.Citizens[0].HomeId = "work");
        Reject(state => state.Citizens[0].ShopId = "work");
        Reject(state => state.Citizens[0].Identity.Persona = (Persona)999);
        Reject(state => state.Citizens[0].Journey!.Legs[0].Mode = (TravelMode)999);
        Reject(state => state.Citizens[0].Phase = (CitizenPhase)999);
    }

    [Theory]
    [InlineData(Persona.Student, "work")]
    [InlineData(Persona.Commuter, "study")]
    [InlineData(Persona.Retiree, "work")]
    public void IncompatibleDayAssignmentIsRejectedBeforeSimulationAndOriginalIsPreserved(Persona persona, string wrongPlace)
    {
        var state = Example(); var citizen = state.Citizens[0];
        state.Places.Add(new Place { Id = "study", Name = "School", Kind = PlaceKind.Study, Position = new Position(25, 0, 0) });
        citizen.Identity.Persona = persona;
        citizen.DayPlaceId = persona == Persona.Student ? "study" : persona == Persona.Retiree ? "" : "work";
        citizen.Phase = CitizenPhase.AtPlace; citizen.DestinationId = ""; citizen.Journey = null; citizen.DepartureMinute = 0;
        citizen.NextEventMinute = state.Minute; citizen.ActivityUntilMinute = state.Minute;
        InitializeFixture(state);
        var json = JObject.Parse(SaveCodec.Write(state));
        // Leave identity and population fingerprint untouched, as in a damaged assignment record.
        json["Citizens"]![0]!["DayPlaceId"] = wrongPlace;
        string payload = json.ToString();
        var data = new Dictionary<string, string> { [SaveStore.StateKey] = payload, ["rlo:readonly"] = "unchanged" };
        var read = SaveStore.ReadFrom(data, state.PopulationKey);
        Assert.Equal(SaveReadStatus.Invalid, read.Status); Assert.Null(read.State);
        Assert.False(SaveStore.WriteTo(data, state).Success);
        Assert.Equal(payload, data[SaveStore.StateKey]); Assert.Equal("unchanged", data["rlo:readonly"]);
        citizen.DayPlaceId = wrongPlace;
        Assert.Throws<SaveValidationException>(() => SaveCodec.Write(state));
    }

    [Fact]
    public void EveryPersonaMayHaveNoDayAssignment()
    {
        foreach (Persona persona in Enum.GetValues(typeof(Persona)))
        {
            var state = Example(); state.Citizens[0].Identity.Persona = persona; state.Citizens[0].DayPlaceId = "";
            InitializeFixture(state);
            Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(SaveCodec.Write(state)).Status);
        }
    }

    [Fact]
    public void DisconnectedRoutesAndIncoherentTimelinesAreRejected()
    {
        Reject(state => state.Citizens[0].Journey!.Legs[0].From = new Position(99, 99, 99));
        Reject(state => state.Citizens[0].Journey!.Legs[1].From = new Position(99, 99, 99));
        Reject(state => state.Citizens[0].Journey!.Legs[1].To = new Position(99, 99, 99));
        Reject(state => state.Citizens[0].Journey!.Legs[0].Minutes = 0);
        Reject(state => state.Citizens[0].DepartureMinute = state.Minute + 1);
        Reject(state => state.Citizens[0].NextEventMinute += 1);
        Reject(state => state.Minute = 101);
        Reject(state => state.Citizens[0].RecentVisits[0].AtMinute = state.Minute + 1);
        Reject(state => state.Citizens[0].DestinationActivity = Activity.Leisure);
    }

    [Theory]
    [InlineData("reference")]
    [InlineData("duplicate")]
    [InlineData("enum")]
    [InlineData("route")]
    [InlineData("missing")]
    [InlineData("unknown")]
    [InlineData("null")]
    [InlineData("float-enum")]
    [InlineData("float-age")]
    [InlineData("string-minute")]
    [InlineData("string-bool")]
    [InlineData("wrong-case")]
    public void ReaderValidatesWholeStateRatherThanAcceptingDeserializerDefaults(string corruption)
    {
        var json = JObject.Parse(SaveCodec.Write(Example()));
        var citizen = (JObject)json["Citizens"]![0]!;
        switch (corruption)
        {
            case "reference": citizen["HomeId"] = "unknown"; break;
            case "duplicate": ((JArray)json["Citizens"]!).Add(citizen.DeepClone()); break;
            case "enum": citizen["Phase"] = 100; break;
            case "route": citizen["Journey"]!["Legs"]![1]!["From"]!["X"] = 999; break;
            case "missing": citizen.Remove("DepartureMinute"); break;
            case "unknown": citizen["UnexpectedField"] = "ignore me"; break;
            case "null": citizen["Identity"] = null; break;
            case "float-enum": citizen["Phase"] = 1.01; break;
            case "float-age": citizen["Identity"]!["Age"] = 35.2; break;
            case "string-minute": json["Minute"] = "14.125"; break;
            case "string-bool": citizen["Identity"]!["Female"] = "true"; break;
            case "wrong-case": citizen["phase"] = citizen["Phase"]; citizen.Remove("Phase"); break;
        }
        var read = SaveCodec.Read(json.ToString());
        Assert.Equal(SaveReadStatus.Invalid, read.Status); Assert.Null(read.State); Assert.NotEmpty(read.Error);
    }

    [Fact]
    public void OversizedInputCollectionsAndStringsAreRejected()
    {
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(new string(' ', SaveCodec.MaxInputCharacters + 1)).Status);
        Reject(state => { while (state.Places.Count <= SaveCodec.MaxPlaces) state.Places.Add(state.Places[0]); });
        Reject(state => { while (state.Citizens.Count <= SaveCodec.MaxCitizens) state.Citizens.Add(state.Citizens[0]); });
        Reject(state => state.PopulationKey = new string('a', 257));
        Reject(state => { while (state.Citizens[0].Journey!.Legs.Count <= SaveCodec.MaxRouteLegs) state.Citizens[0].Journey!.Legs.Add(state.Citizens[0].Journey!.Legs[0]); });
        Reject(state => { while (state.Citizens[0].RecentVisits.Count <= SaveCodec.MaxRecentVisits) state.Citizens[0].RecentVisits.Add(state.Citizens[0].RecentVisits[0]); });
        Reject(state => state.Citizens.Clear());
        Reject(state => state.Places.Clear());
        Reject(state => state.Places[0].Capacity = 0);
    }

    [Fact]
    public void SavesAreIsolatedAndForeignPopulationCannotOverwriteExistingState()
    {
        var first = new Dictionary<string, string> { ["rlo:readonly"] = "first original" };
        var second = new Dictionary<string, string> { ["rlo:readonly"] = "second original" };
        var a = Example(); var b = Example(); b.PopulationKey = "population-b"; b.Citizens[0].Identity.Name = "Other Resident"; InitializeFixture(b);
        Assert.True(SaveStore.WriteTo(first, a).Success); Assert.True(SaveStore.WriteTo(second, b).Success);
        string firstRaw = first[SaveStore.StateKey]; string secondRaw = second[SaveStore.StateKey];
        Assert.False(SaveStore.WriteTo(first, b).Success); Assert.Equal(firstRaw, first[SaveStore.StateKey]);
        Assert.Equal(SaveReadStatus.Invalid, SaveStore.ReadFrom(first, "population-b").Status);
        var restored = SaveStore.ReadFrom(first, "population-a").State!; restored.Citizens[0].Identity.Name = "Changed";
        Assert.Equal("Other Resident", SaveStore.ReadFrom(second, "population-b").State!.Citizens[0].Identity.Name);
        Assert.Equal(secondRaw, second[SaveStore.StateKey]); Assert.Equal("first original", first["rlo:readonly"]); Assert.Equal("second original", second["rlo:readonly"]);
    }

    [Fact]
    public void OptionalAssignmentsAndLateActivityWindowRemainLoadable()
    {
        var state = Example(); state.Citizens[0].DayPlaceId = ""; state.Citizens[0].ShopId = ""; state.Citizens[0].LeisureId = "";
        state.Citizens[0].ActivityUntilMinute = 1;
        state.Citizens[0].Identity.District = ""; state.Places[0].District = ""; InitializeFixture(state);
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(SaveCodec.Write(state)).Status);
    }

    [Fact]
    public void WaitingForRouteRoundTripRetainsTargetAndRetryWithoutCreatingAJourney()
    {
        var state = Example(); var citizen = state.Citizens[0];
        citizen.Phase = CitizenPhase.WaitingForRoute; citizen.Journey = null; citizen.DepartureMinute = 0; citizen.NextEventMinute = 30;
        var read = SaveCodec.Read(SaveCodec.Write(state));
        Assert.Equal(SaveReadStatus.Loaded, read.Status);
        var loaded = read.State!.Citizens[0];
        Assert.Null(loaded.Journey); Assert.Equal("work", loaded.DestinationId); Assert.Equal(Activity.Work, loaded.DestinationActivity); Assert.Equal(30, loaded.NextEventMinute);
    }

    [Fact]
    public void OpeningWaitRetainsDestinationAndDeadlineAndRejectsImpossibleWakeTimes()
    {
        var state = Example(); var citizen = state.Citizens[0];
        state.Places[1].OpenMinute = 60; state.Places[1].CloseMinute = 480;
        citizen.Phase = CitizenPhase.WaitingForOpening; citizen.Journey = null; citizen.DepartureMinute = 0;
        citizen.NextEventMinute = 60; citizen.ActivityUntilMinute = 480;
        string encoded = SaveCodec.Write(state);
        var read = SaveCodec.Read(encoded);
        Assert.Equal(SaveReadStatus.Loaded, read.Status);
        Assert.Equal(encoded, SaveCodec.Write(read.State!));
        foreach (string field in new[] { "ActivityUntilMinute", "NextEventMinute" })
        {
            var json = JObject.Parse(encoded);
            json["Citizens"]![0]![field] = field == "ActivityUntilMinute" ? 60 : 59;
            Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(json.ToString()).Status);
        }
        citizen.ActivityUntilMinute = 60;
        Assert.Throws<SaveValidationException>(() => SaveCodec.Write(state));
    }

    [Fact]
    public void WeeklyHoursRetainOvernightAndWholeDayWindowsExactly()
    {
        var state = Example();
        state.Places[1].WeeklyHours.Add(new OpeningWindow { Day = 0, FromMinute = 1320, ToMinute = 120 });
        state.Places[1].WeeklyHours.Add(new OpeningWindow { Day = 6, FromMinute = 0, ToMinute = 1440 });
        string payload = SaveCodec.Write(state);
        var result = SaveCodec.Read(payload);
        Assert.Equal(SaveReadStatus.Loaded, result.Status);
        var windows = result.State!.Places[1].WeeklyHours;
        Assert.Equal(2, windows.Count);
        Assert.Equal(0, windows[0].Day); Assert.Equal(1320, windows[0].FromMinute); Assert.Equal(120, windows[0].ToMinute);
        Assert.Equal(6, windows[1].Day); Assert.Equal(0, windows[1].FromMinute); Assert.Equal(1440, windows[1].ToMinute);
        Assert.Empty(result.State.Places[0].WeeklyHours);
        Assert.Equal(payload, SaveCodec.Write(result.State));
    }

    [Theory]
    [InlineData(-1, 0, 120)]
    [InlineData(7, 0, 120)]
    [InlineData(0, -1, 120)]
    [InlineData(0, 0, 1441)]
    [InlineData(0, 1441, 120)]
    [InlineData(0, 120, -1)]
    [InlineData(0, 0, 0)]
    [InlineData(0, 120, 120)]
    [InlineData(0, 1440, 0)]
    public void InvalidWeeklyHoursAreRejectedByReaderAndWriter(int day, int from, int to)
    {
        var state = Example(); state.Places[1].WeeklyHours.Add(new OpeningWindow { Day = day, FromMinute = from, ToMinute = to });
        Assert.Throws<SaveValidationException>(() => SaveCodec.Write(state));
        var json = JObject.Parse(SaveCodec.Write(Example()));
        ((JArray)json["Places"]![1]!["WeeklyHours"]!).Add(new JObject { ["Day"] = day, ["FromMinute"] = from, ["ToMinute"] = to });
        var result = SaveCodec.Read(json.ToString());
        Assert.Equal(SaveReadStatus.Invalid, result.Status); Assert.Null(result.State);
    }

    [Fact]
    public void WeeklyHoursHaveStrictShapeAndBoundedCount()
    {
        Reject(state => state.Places[0].WeeklyHours = null!);
        Reject(state => state.Places[0].WeeklyHours.Add(null!));
        Reject(state => { for (int i = 0; i <= SaveCodec.MaxOpeningWindows; i++) state.Places[0].WeeklyHours.Add(new OpeningWindow { Day = 0, FromMinute = 0, ToMinute = 1440 }); });
        var json = JObject.Parse(SaveCodec.Write(Example()));
        ((JArray)json["Places"]![1]!["WeeklyHours"]!).Add(new JObject { ["Day"] = 0.5, ["FromMinute"] = 0, ["ToMinute"] = 120 });
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(json.ToString()).Status);
        json = JObject.Parse(SaveCodec.Write(Example())); ((JObject)json["Places"]![1]!).Remove("WeeklyHours");
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(json.ToString()).Status);
    }

    private static void Reject(Action<CityState> mutation)
    {
        var state = Example(); mutation(state);
        Assert.Throws<SaveValidationException>(() => SaveCodec.Write(state));
    }

    private sealed class DirectRoutes : IRoutePlanner
    {
        public Route Plan(Place from, Place to, double departureMinute, TravelPreferences preferences) => new Route
        {
            Legs = new List<RouteLeg> { new RouteLeg { From = from.Position, To = to.Position, Minutes = 9.25, Mode = TravelMode.Walk } }
        };
    }

    private static void InitializeFixture(CityState state)
    {
        state.SourceIdentities = state.Citizens.Select(c => Population.CloneIdentity(c.Identity)).ToList();
        state.InitialImportedResidents = state.PopulationTarget = state.Citizens.Count;
        foreach (var person in state.Citizens) ResidentLifecycle.Initialize(person, state.Seed, state.Minute);
    }

    private static CityState Example()
    {
        var home = new Position(10.1234567890123, 2.5, -8.125);
        var connection = new Position(40.75, 5.125, 20.875);
        var work = new Position(100.25, 11.5, 60.625);
        var state = new CityState
        {
            PopulationKey = "population-a", RloVersion = "verified-test-contract", Seed = 42, Minute = 14.125,
            Places = new List<Place>
            {
                new Place { Id = "home", Name = "Home", District = "Midtown", Kind = PlaceKind.Home, Position = home },
                new Place { Id = "work", Name = "Work", District = "Midtown", Kind = PlaceKind.Work, Position = work },
                new Place { Id = "shop", Name = "Shop", District = "Midtown", Kind = PlaceKind.Shop, Position = new Position(1, 2, 3) },
                new Place { Id = "leisure", Name = "Park", District = "Midtown", Kind = PlaceKind.Leisure, Position = new Position(7, 8, 9) }
            },
            Citizens = new List<CitizenState>
            {
                new CitizenState
                {
                    Identity = new CitizenIdentity { Id = "resident-101", Name = "Ada Resident", District = "Midtown", Age = 36, Persona = Persona.Commuter, AppearanceSeed = 912 },
                    Traits = ResidentTraits.Generate(42, "resident-101"),
                    HomeId = "home", DayPlaceId = "work", ShopId = "shop", LeisureId = "leisure", PlaceId = "home", DestinationId = "work",
                    Phase = CitizenPhase.Travelling, Activity = Activity.Home, DestinationActivity = Activity.Work,
                    DepartureMinute = 5.25, NextEventMinute = 35.75, ActivityUntilMinute = 480,
                    Journey = new Route { Legs = new List<RouteLeg>
                    {
                        new RouteLeg { From = home, To = connection, Minutes = 10.25, Mode = TravelMode.Walk },
                        new RouteLeg { From = connection, To = work, Minutes = 20.25, Mode = TravelMode.Subway, ServiceId = "line-a" }
                    } },
                    RecentVisits = new List<VisitRecord> { new VisitRecord { AtMinute = 0, PlaceId = "home", Activity = Activity.Home } }
                }
            }
        };
        InitializeFixture(state);
        return state;
    }
}
