using System.Collections.Generic;
using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class RhythmPersistenceTests
{
    private static CityState City()
    {
        var city = Population.Create(Enumerable.Range(0, 4).Select(i => new CitizenIdentity
        { Id = "rhythm:" + i, Name = "Resident " + i, Age = 30, Persona = Persona.Commuter }), new[]
        {
            new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 4 },
            new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = 4, Position = new Position(120, 0, 0) },
            new Place { Id = "old-work", Name = "Old work", Kind = PlaceKind.Work, Capacity = 4 },
            new Place { Id = "study", Name = "School", Kind = PlaceKind.Study, Capacity = 4 }
        }, "rhythm-source", "0.40.4", 73, 80);
        city.Minute = 100;
        foreach (var person in city.Citizens)
        {
            person.PlaceId = person.HomeId = "home"; person.DayPlaceId = "work";
            person.Phase = CitizenPhase.AtPlace; person.Activity = Activity.Home;
            person.NextEventMinute = 170; person.ActivityUntilMinute = 200;
            person.CommutePreparation = Attempt();
        }
        var travelling = city.Citizens[0];
        travelling.Phase = CitizenPhase.Travelling; travelling.DestinationId = "work";
        travelling.DestinationActivity = Activity.Work; travelling.DepartureMinute = 90; travelling.NextEventMinute = 120;
        travelling.Journey = new Route { Legs = new()
        {
            new RouteLeg { From = new Position(0,0,0), To = new Position(0,0,0), Mode = TravelMode.Walk, Minutes = 12, ServiceId = "preparation-wait" },
            new RouteLeg { From = new Position(0,0,0), To = new Position(120,0,0), Mode = TravelMode.Walk, Minutes = 18, ServiceId = "walking" }
        } };
        for (int index = 1; index <= 2; index++)
        {
            var waiting = city.Citizens[index];
            waiting.Phase = index == 1 ? CitizenPhase.WaitingForRoute : CitizenPhase.WaitingForOpening;
            waiting.DestinationId = "work"; waiting.DestinationActivity = Activity.Work; waiting.NextEventMinute = 110;
        }
        return city;
    }

    private static CommutePreparation Attempt(double travel = 30) => new CommutePreparation
    { PlaceId = "work", WorkStartsAt = 120, WorkEndsAt = 200, PreparedAt = 80, TravelMinutes = travel };

    private static void Empty(CommutePreparation preparation)
    {
        Assert.Equal("", preparation.PlaceId); Assert.Equal(0, preparation.WorkStartsAt);
        Assert.Equal(0, preparation.WorkEndsAt); Assert.Equal(0, preparation.PreparedAt); Assert.Equal(0, preparation.TravelMinutes);
    }

    [Theory]
    [InlineData(0)] [InlineData(30)] [InlineData(512000000)]
    public void FailedOrSuccessfulPreparationRoundTripsAlongsideCommittedJourneyAndWaits(double travel)
    {
        var city = City(); foreach (var person in city.Citizens) person.CommutePreparation.TravelMinutes = travel;
        string source = SaveCodec.Write(city); var result = SaveCodec.Read(source);
        Assert.Equal(SaveReadStatus.Loaded, result.Status); Assert.Equal(CityState.CurrentSchema, result.SourceSchema);
        Assert.Equal(source, SaveCodec.Write(result.State!));
        foreach (var person in result.State!.Citizens)
        {
            Assert.Equal("work", person.CommutePreparation.PlaceId); Assert.Equal(120, person.CommutePreparation.WorkStartsAt);
            Assert.Equal(200, person.CommutePreparation.WorkEndsAt); Assert.Equal(80, person.CommutePreparation.PreparedAt);
            Assert.Equal(travel, person.CommutePreparation.TravelMinutes);
        }
        Assert.NotNull(result.State.Citizens[0].Journey); Assert.Null(result.State.Citizens[1].Journey);
        Assert.Equal(CitizenPhase.WaitingForOpening, result.State.Citizens[2].Phase);
        Assert.Equal(170, result.State.Citizens[3].NextEventMinute);
    }

    [Theory]
    [InlineData(90, 95)] [InlineData(90, 200)] [InlineData(120, 200)] [InlineData(999999999, 1000000000)]
    public void PreparationMayDescribePreviousCurrentOrFutureWindowAndPreviousWorkplace(double starts, double ends)
    {
        var city = City(); var preparation = city.Citizens[0].CommutePreparation;
        preparation.WorkStartsAt = starts; preparation.WorkEndsAt = ends; preparation.PlaceId = "old-work";
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(SaveCodec.Write(city)).Status);
    }

    [Fact]
    public void NewCitizensHaveIndependentEmptyPreparationAndStudentsCanPrepareStudy()
    {
        var first = new CitizenState(); var second = new CitizenState(); Empty(first.CommutePreparation);
        Assert.NotSame(first.CommutePreparation, second.CommutePreparation);
        var city = City(); var person = city.Citizens[3]; person.Identity.Persona = Persona.Student;
        person.DayPlaceId = "study"; person.CommutePreparation.PlaceId = "study";
        city.SourceIdentities.Single(i => i.Id == person.Identity.Id).Persona = Persona.Student;
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(SaveCodec.Write(city)).Status);
    }

    [Theory]
    [InlineData("missing-preparation")] [InlineData("null-preparation")] [InlineData("missing-field")]
    [InlineData("unknown-field")] [InlineData("duplicate-field")] [InlineData("wrong-case")]
    [InlineData("string-number")] [InlineData("null-field")] [InlineData("array-preparation")]
    [InlineData("unknown-target")] [InlineData("wrong-role")] [InlineData("empty-with-values")]
    [InlineData("zero-window")] [InlineData("backward-window")] [InlineData("future-preparation")]
    [InlineData("late-preparation")] [InlineData("retired")]
    public void SchemaSixRequiresExactPreparationShapeAndCoherentMeasuredAttempt(string defect)
    {
        var root = JObject.Parse(SaveCodec.Write(City())); var person = (JObject)root["Citizens"]![0]!;
        var preparation = (JObject)person["CommutePreparation"]!;
        switch (defect)
        {
            case "missing-preparation": person.Remove("CommutePreparation"); break;
            case "null-preparation": person["CommutePreparation"] = null; break;
            case "missing-field": preparation.Remove("TravelMinutes"); break;
            case "unknown-field": preparation["Route"] = new JObject(); break;
            case "wrong-case": preparation["placeId"] = preparation["PlaceId"]; preparation.Remove("PlaceId"); break;
            case "string-number": preparation["TravelMinutes"] = "30"; break;
            case "null-field": preparation["PlaceId"] = null; break;
            case "array-preparation": person["CommutePreparation"] = new JArray(); break;
            case "unknown-target": preparation["PlaceId"] = "missing"; break;
            case "wrong-role": preparation["PlaceId"] = "study"; break;
            case "empty-with-values": preparation["PlaceId"] = ""; break;
            case "zero-window": preparation["WorkEndsAt"] = 120; break;
            case "backward-window": preparation["WorkEndsAt"] = 119; break;
            case "future-preparation": preparation["PreparedAt"] = 101; break;
            case "late-preparation": preparation["WorkStartsAt"] = 79; break;
            case "retired":
                person["Identity"]!["Persona"] = (int)Persona.Retiree; person["DayPlaceId"] = "";
                root["SourceIdentities"]![0]!["Persona"] = (int)Persona.Retiree; break;
        }
        string source = root.ToString();
        if (defect == "duplicate-field") source = source.Replace("\"TravelMinutes\": 30", "\"TravelMinutes\": 30, \"TravelMinutes\": 30");
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(source).Status);
    }

    [Theory]
    [InlineData("WorkStartsAt", -1)] [InlineData("WorkStartsAt", 1000000001)]
    [InlineData("WorkEndsAt", -1)] [InlineData("WorkEndsAt", 1000000001)]
    [InlineData("PreparedAt", -1)] [InlineData("PreparedAt", 1000000001)]
    [InlineData("TravelMinutes", -1)] [InlineData("TravelMinutes", 512000001)]
    [InlineData("WorkStartsAt", double.NaN)] [InlineData("WorkEndsAt", double.PositiveInfinity)]
    [InlineData("PreparedAt", double.NegativeInfinity)] [InlineData("TravelMinutes", double.NaN)]
    public void PreparationNumericBoundsRejectBothReadAndWrite(string field, double value)
    {
        var city = City(); var root = JObject.Parse(SaveCodec.Write(city));
        root["Citizens"]![0]!["CommutePreparation"]![field] = value;
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(root.ToString()).Status);
        typeof(CommutePreparation).GetProperty(field)!.SetValue(city.Citizens[0].CommutePreparation, value);
        Assert.Throws<SaveValidationException>(() => SaveCodec.Write(city));
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void OldRhythmPayloadsAreRefusedRatherThanInventingPreparation(int schema)
    {
        var city = City(); string payload = Legacy(city, schema).ToString();
        var data = new Dictionary<string,string> { [SaveStore.StateKey] = payload };
        var read = SaveStore.ReadFrom(data, city.PopulationKey);
        Assert.Equal(SaveReadStatus.Invalid, read.Status); Assert.Null(read.State);
        Assert.False(SaveStore.WriteTo(data, city).Success); Assert.Equal(payload, data[SaveStore.StateKey]);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void LegacyMigrationFirstRejectsWrongOriginalShapeAndInvalidActiveRoute(int schema)
    {
        var wire = Legacy(City(), schema);
        wire["Citizens"]![0]!["CommutePreparation"] = JObject.FromObject(Attempt());
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(wire.ToString()).Status);
        ((JObject)wire["Citizens"]![0]!).Remove("CommutePreparation");
        ((JObject)wire["Citizens"]![0]!).Remove("NextEventMinute");
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(wire.ToString()).Status);
        wire = Legacy(City(), schema); wire["Citizens"]![0]!["Journey"]!["Legs"]![1]!["Minutes"] = 0;
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(wire.ToString()).Status);
    }

    [Theory]
    [InlineData(0, 100)] [InlineData(0, 105)] [InlineData(1, 100)] [InlineData(1, 105)]
    public void TimingChangeClearsPreparationWakesHomeAndPreservesCommittedTravelAndWaitSemantics(int timingVersion, double now)
    {
        var city = City(); city.Minute = now; city.TravelTimingVersion = timingVersion;
        var traveller = city.Citizens[0]; var original = traveller.Journey!;
        var before = original.PositionAt(now - traveller.DepartureMinute);
        Assert.True(TravelTiming.Apply(city, 2, 2));
        Assert.All(city.Citizens, person => Empty(person.CommutePreparation));
        Assert.Equal(now, city.Citizens[3].NextEventMinute);
        Assert.Equal(110, city.Citizens[1].NextEventMinute); Assert.Equal(110, city.Citizens[2].NextEventMinute);
        Assert.Equal(200, city.Citizens[2].ActivityUntilMinute);
        Assert.Equal(before.X, traveller.Journey!.PositionAt(now - traveller.DepartureMinute).X, 10);
        Assert.Equal(90, traveller.DepartureMinute);
        Assert.Equal(12, traveller.Journey.Legs.Where(l => l.ServiceId == "preparation-wait").Sum(l => l.Minutes));
        Assert.Equal(120, traveller.Journey.Legs.Last().To.X);
        string saved = SaveCodec.Write(city); var read = SaveCodec.Read(saved);
        Assert.Equal(SaveReadStatus.Loaded, read.Status); Assert.Equal(saved, SaveCodec.Write(read.State!));
    }

    [Fact]
    public void UnchangedTimingKeepsPreparationAndFailedConversionCannotPartlyClearIt()
    {
        var city = City(); var preparation = city.Citizens[0].CommutePreparation;
        string before = SaveCodec.Write(city);
        Assert.False(TravelTiming.Apply(city, 2, 1)); Assert.Same(preparation, city.Citizens[0].CommutePreparation);
        Assert.Equal(before, SaveCodec.Write(city));
        city.Citizens[2].NextEventMinute = 99;
        var route = city.Citizens[0].Journey; double arrival = city.Citizens[0].NextEventMinute;
        Assert.Throws<ArgumentException>(() => TravelTiming.Apply(city, 2, 2));
        Assert.Same(preparation, city.Citizens[0].CommutePreparation); Assert.Same(route, city.Citizens[0].Journey);
        Assert.Equal(arrival, city.Citizens[0].NextEventMinute); Assert.Equal(170, city.Citizens[3].NextEventMinute);
        Assert.Equal(1, city.TravelClockRate);
    }

    private static JObject Legacy(CityState city, int schema)
    {
        var wire = JObject.Parse(SaveCodec.Write(city)); wire["Schema"] = schema;
        wire.Remove("Traffic"); wire.Remove("TrafficTopologyId"); FoodCommerceTests.StripNewFields(wire);
        foreach (var person in wire["Citizens"]!.Cast<JObject>()) person.Remove("CommutePreparation");
        if (schema < 5)
        {
            wire.Remove("TravelTimingVersion"); wire.Remove("TravelClockRate");
            foreach (var car in wire["Citizens"]!.Select(p => p["Car"]).OfType<JObject>())
            { car.Remove("ModelId"); car.Remove("PaintSeed"); }
        }
        if (schema < 3)
        {
            wire.Remove("PopulationTarget"); wire.Remove("InitialImportedResidents");
            foreach (var place in wire["Places"]!.Cast<JObject>()) place.Remove("ResidentialAreaSquareMetres");
        }
        if (schema == 1)
        {
            Keep(wire, "Schema RloVersion PopulationKey Seed Minute Places Citizens");
            foreach (var person in wire["Citizens"]!.Cast<JObject>())
                Keep(person, "Identity HomeId DayPlaceId ShopId LeisureId Preferences Phase Activity PlaceId DestinationId DestinationActivity Journey DepartureMinute NextEventMinute ActivityUntilMinute CompletedJourneys MissedActivities RecentVisits");
            foreach (var place in wire["Places"]!.Cast<JObject>())
                Keep(place, "Id Name District Kind Position Capacity OpenMinute CloseMinute WeeklyHours");
        }
        return wire;
    }

    private static void Keep(JObject value, string fields)
    {
        var names = fields.Split(' ').ToHashSet(StringComparer.Ordinal);
        foreach (var property in value.Properties().ToArray()) if (!names.Contains(property.Name)) property.Remove();
    }
}
