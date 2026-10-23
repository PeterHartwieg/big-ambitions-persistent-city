using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class PhysicalPersistenceTests
{
    private static CityState Journey()
    {
        var city = Population.Create(new[] { new CitizenIdentity { Id = "rlo:1", Name = "Resident", Age = 35, Persona = Persona.Commuter } },
            new[] { new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home },
                new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Position = new Position(100, 0, 0) } }, "physical", "0.40.4", 42, 60);
        city.TravelClockRate = 1.75;
        var person = city.Citizens[0];
        person.Preferences.OwnsCar = true;
        person.Car = new CarState { Id = person.Identity.Id + ":car", ModelId = "sedan-stable-model", PaintSeed = int.MinValue, ParkedPosition = new Position(5, 0, 0) };
        person.NextJobChangeMinute = 1000;
        person.Phase = CitizenPhase.Travelling; person.DestinationId = "work"; person.DestinationActivity = Activity.Work;
        person.DepartureMinute = 50; person.NextEventMinute = 70; person.ActivityUntilMinute = 480;
        person.Journey = new Route { Legs = new List<RouteLeg> {
            new() { From = new Position(0,0,0), To = new Position(5,0,0), Minutes = 2, Mode = TravelMode.Walk },
            new() { From = new Position(5,0,0), To = new Position(100,0,0), Minutes = 18, Mode = TravelMode.Car } } };
        return city;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompletedFractionalJourneyCanRoundTripBeforeQueuedArrival(bool retime)
    {
        var random = new Random(3417); int roundedBelowClock = 0;
        for (int example = 0; example < 128; example++)
        {
            var state = Journey(); var citizen = state.Citizens[0];
            double clock = (example < 64 ? 144000.37 : 900000000.37) + example * .1;
            if (example == 0) clock = 637461.0326307263;
            if (example == 1 && !retime) clock = 900000439.17;
            state.Minute = clock; state.TravelClockRate = 1;
            citizen.Car = null; citizen.Preferences.OwnsCar = false;
            citizen.ActivityUntilMinute = clock + 480;
            citizen.Journey!.Legs.Clear(); int count = 3 + example % 8;
            for (int leg = 0; leg < count; leg++)
                citizen.Journey.Legs.Add(new RouteLeg { From = new Position(100.0 * leg / count, 0, 0),
                    To = new Position(100.0 * (leg + 1) / count, 0, 0), Minutes = .01 + random.NextDouble() * 70, Mode = TravelMode.Walk });
            if (example == 0 || (example == 1 && !retime))
            {
                citizen.Journey.Legs.Clear();
                citizen.Journey.Legs.Add(new RouteLeg { From = new Position(0,0,0), To = new Position(100,0,0),
                    Minutes = example == 0 ? 86731.89583988494 : 567634.374533236, Mode = TravelMode.Walk });
            }
            citizen.DepartureMinute = clock; citizen.NextEventMinute = clock + citizen.Journey.Minutes;
            var engine = new CitySimulator(state, new Direct());
            Assert.True(engine.TryAcquirePhysicalJourney(citizen.Identity.Id, out var lease));
            Assert.True(engine.ObservePhysicalJourney(lease!, citizen.Journey.Minutes));
            if (citizen.DepartureMinute + citizen.Journey.Minutes < clock) roundedBelowClock++;
            if (retime) Assert.True(TravelTiming.Apply(state, 2, 2));
            string encoded = SaveCodec.Write(state); var read = SaveCodec.Read(encoded);
            Assert.Equal(SaveReadStatus.Loaded, read.Status);
            Assert.Equal(encoded, SaveCodec.Write(read.State!));
            var resumed = new CitySimulator(read.State!, new Direct()); var restored = read.State!.Citizens[0];
            Assert.Equal(clock, restored.NextEventMinute);
            Assert.True(resumed.SampleJourney(restored).AtEnd);
            Assert.Equal(1, resumed.AdvanceTo(clock).Events);
            Assert.Equal("work", restored.PlaceId); Assert.Equal(1, restored.CompletedJourneys);
        }
        Assert.True(roundedBelowClock > 0, "The fixture must exercise computed arrivals below their explicit queued event.");
    }

    [Theory]
    [InlineData("overdue-event")]
    [InlineData("future-departure")]
    [InlineData("expired-route")]
    public void ArrivalRoundoffDoesNotRelaxExplicitDatesOrMeaningfullyExpiredRoutes(string defect)
    {
        var city = Journey(); var person = city.Citizens[0];
        city.Minute = 144000.37;
        person.DepartureMinute = city.Minute - person.Journey!.Minutes;
        person.NextEventMinute = city.Minute;
        string encoded = SaveCodec.Write(city);
        if (defect == "overdue-event") person.NextEventMinute = city.Minute - .00000001;
        if (defect == "future-departure") person.DepartureMinute = Math.BitIncrement(city.Minute);
        if (defect == "expired-route") person.DepartureMinute -= .00001;
        Assert.Throws<SaveValidationException>(() => SaveCodec.Write(city));
        var json = JObject.Parse(encoded);
        json["Citizens"]![0]!["NextEventMinute"] = person.NextEventMinute;
        json["Citizens"]![0]!["DepartureMinute"] = person.DepartureMinute;
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(json.ToString()).Status);
    }

    private sealed class Direct : IRoutePlanner
    {
        public Route Plan(Place from, Place to, double minute, TravelPreferences preferences) => new Route
        { Legs = new List<RouteLeg> { new() { From = from.Position, To = to.Position, Minutes = 1, Mode = TravelMode.Walk } } };
    }

    private static JObject SchemaFour(CityState city)
    {
        var root = JObject.Parse(SaveCodec.Write(city)); root["Schema"] = 4;
        root.Remove("TravelTimingVersion"); root.Remove("TravelClockRate");
        root.Remove("Traffic"); root.Remove("TrafficTopologyId"); FoodCommerceTests.StripNewFields(root);
        foreach (JObject person in root["Citizens"]!) person.Remove("CommutePreparation");
        var car = (JObject)root["Citizens"]![0]!["Car"]!; car.Remove("ModelId"); car.Remove("PaintSeed");
        return root;
    }

    [Fact]
    public void SchemaFourMigrationPreservesAllOldFieldsAndAwaitsRuntimeTimingAndCarAssignment()
    {
        var original = Journey(); var old = SchemaFour(original); string payload = old.ToString();
        var data = new Dictionary<string,string> { [SaveStore.StateKey] = payload };
        var read = SaveStore.ReadFrom(data, original.PopulationKey);
        Assert.Equal(SaveReadStatus.Loaded, read.Status); Assert.True(read.Migrated);
        Assert.Equal(4, read.SourceSchema);
        Assert.Same(payload, data[SaveStore.StateKey]);
        var migrated = read.State!;
        Assert.Equal(CityState.CurrentSchema, migrated.Schema);
        Assert.Equal(0, migrated.TravelTimingVersion); Assert.Equal(1, migrated.TravelClockRate);
        Assert.Equal("", migrated.Citizens[0].Car!.ModelId); Assert.Equal(0, migrated.Citizens[0].Car!.PaintSeed);
        Assert.Equal(original.Citizens[0].NextJobChangeMinute, migrated.Citizens[0].NextJobChangeMinute);
        Assert.Equal(original.Citizens[0].Journey!.PositionAt(10).X, migrated.Citizens[0].Journey!.PositionAt(10).X);
        Assert.True(JToken.DeepEquals(old, SchemaFour(migrated)));
        var roundTrip = SaveCodec.Read(SaveCodec.Write(migrated));
        Assert.Equal(SaveReadStatus.Loaded, roundTrip.Status); Assert.False(roundTrip.Migrated);
        Assert.Equal(CityState.CurrentSchema, roundTrip.SourceSchema);
        Assert.Equal(0, roundTrip.State!.TravelTimingVersion);
    }

    [Fact]
    public void ReadMetadataDistinguishesSourceSchemaWithoutChangingExistingStatuses()
    {
        Assert.Equal(0, SaveCodec.Read(null).SourceSchema);
        Assert.Equal(0, SaveCodec.Read("corrupt").SourceSchema);
        int futureSchema = CityState.CurrentSchema + 1;
        var future = SaveCodec.Read("{\"Schema\":" + futureSchema + "}");
        Assert.Equal(SaveReadStatus.Future, future.Status); Assert.Equal(futureSchema, future.SourceSchema);
        Assert.Null(future.State); Assert.False(future.Migrated);
        var loaded = SaveCodec.Read(SaveCodec.Write(Journey()));
        Assert.Equal(CityState.CurrentSchema, loaded.SourceSchema);
        Assert.False(loaded.Migrated);
    }

    [Theory]
    [InlineData(int.MinValue, 0, 0.01)]
    [InlineData(int.MaxValue, 1, 1000)]
    public void AssignedCarAppearanceAndTimingRoundTripExactly(int paint, int timing, double rate)
    {
        var city = Journey(); city.TravelTimingVersion = timing; city.TravelClockRate = rate;
        city.Citizens[0].Car!.PaintSeed = paint;
        string encoded = SaveCodec.Write(city); var read = SaveCodec.Read(encoded);
        Assert.Equal(SaveReadStatus.Loaded, read.Status);
        Assert.Equal(encoded, SaveCodec.Write(read.State!));
        Assert.Equal(city.Citizens[0].Car!.ModelId, read.State!.Citizens[0].Car!.ModelId);
        Assert.Equal(paint, read.State.Citizens[0].Car!.PaintSeed);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("clock-zero")]
    [InlineData("clock-high")]
    [InlineData("clock-nan")]
    [InlineData("model-long")]
    [InlineData("model-null")]
    public void InvalidCurrentFieldsAreRejectedOnReadAndWrite(string defect)
    {
        var city = Journey();
        if (defect == "version") city.TravelTimingVersion = 2;
        if (defect == "clock-zero") city.TravelClockRate = 0;
        if (defect == "clock-high") city.TravelClockRate = 1001;
        if (defect == "clock-nan") city.TravelClockRate = double.NaN;
        if (defect == "model-long") city.Citizens[0].Car!.ModelId = new string('a', 129);
        if (defect == "model-null") city.Citizens[0].Car!.ModelId = null!;
        Assert.Throws<SaveValidationException>(() => SaveCodec.Write(city));
        var json = JObject.Parse(SaveCodec.Write(Journey()));
        json["TravelTimingVersion"] = city.TravelTimingVersion; json["TravelClockRate"] = city.TravelClockRate;
        json["Citizens"]![0]!["Car"]!["ModelId"] = city.Citizens[0].Car!.ModelId;
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(json.ToString()).Status);
    }

    [Theory]
    [InlineData("missing-clock")]
    [InlineData("string-clock")]
    [InlineData("float-version")]
    [InlineData("missing-model")]
    [InlineData("float-paint")]
    [InlineData("string-paint")]
    public void CurrentPhysicalFieldsHaveStrictRequiredShape(string defect)
    {
        var json = JObject.Parse(SaveCodec.Write(Journey())); var car = (JObject)json["Citizens"]![0]!["Car"]!;
        if (defect == "missing-clock") json.Remove("TravelClockRate");
        if (defect == "string-clock") json["TravelClockRate"] = "1.75";
        if (defect == "float-version") json["TravelTimingVersion"] = 1.0;
        if (defect == "missing-model") car.Remove("ModelId");
        if (defect == "float-paint") car["PaintSeed"] = 1.0;
        if (defect == "string-paint") car["PaintSeed"] = "10";
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(json.ToString()).Status);
    }

    [Theory]
    [InlineData("timing")]
    [InlineData("model")]
    [InlineData("missing")]
    public void InvalidOldShapeCannotBeRepairedByPhysicalMigration(string defect)
    {
        var json = SchemaFour(Journey());
        if (defect == "timing") json["TravelTimingVersion"] = 0;
        if (defect == "model") json["Citizens"]![0]!["Car"]!["ModelId"] = "sedan";
        if (defect == "missing") ((JObject)json["Citizens"]![0]!["Car"]!).Remove("ParkedPosition");
        string payload = json.ToString(); var data = new Dictionary<string,string> { [SaveStore.StateKey] = payload };
        Assert.Equal(SaveReadStatus.Invalid, SaveStore.ReadFrom(data, "physical").Status);
        Assert.False(SaveStore.WriteTo(data, Journey()).Success); Assert.Same(payload, data[SaveStore.StateKey]);
    }
}
