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
            // A completed journey stores departure = clock - duration (traffic completion does this; before F2c a
            // walk lease did). Walk legs are no longer leased (F2c), so the completed state is set as that rebase leaves it.
            Assert.False(new CitySimulator(state, new Direct()).TryAcquirePhysicalJourney(citizen.Identity.Id, out _));
            citizen.DepartureMinute = clock - citizen.Journey.Minutes; citizen.NextEventMinute = clock;
            if (citizen.DepartureMinute + citizen.Journey.Minutes < clock) roundedBelowClock++;
            if (retime) Assert.True(TravelTiming.Apply(state, 2, 2));
            string encoded = DevelopmentChain.Write(state); var read = DevelopmentChain.Read(encoded);
            Assert.Equal(SaveReadStatus.Loaded, read.Status);
            Assert.Equal(encoded, DevelopmentChain.Write(read.State!));
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
        string encoded = DevelopmentChain.Write(city);
        if (defect == "overdue-event") person.NextEventMinute = city.Minute - .00000001;
        if (defect == "future-departure") person.DepartureMinute = Math.BitIncrement(city.Minute);
        if (defect == "expired-route") person.DepartureMinute -= .00001;
        Assert.Throws<SaveValidationException>(() => DevelopmentChain.Write(city));
        var json = SaveWire.Parse(encoded);
        json["Citizens"]![0]!["NextEventMinute"] = person.NextEventMinute;
        json["Citizens"]![0]!["DepartureMinute"] = person.DepartureMinute;
        Assert.Equal(SaveReadStatus.Invalid, DevelopmentChain.Read(json.ToString()).Status);
    }

    private sealed class Direct : IRoutePlanner
    {
        public Route Plan(Place from, Place to, double minute, TravelPreferences preferences) => new Route
        { Legs = new List<RouteLeg> { new() { From = from.Position, To = to.Position, Minutes = 1, Mode = TravelMode.Walk } } };
    }

    private static JObject SchemaFour(CityState city)
    {
        var root = SaveWire.Parse(DevelopmentChain.Write(city)); root["Schema"] = 4;
        root.Remove("TravelTimingVersion"); root.Remove("TravelClockRate");
        root.Remove("Traffic"); root.Remove("TrafficTopologyId"); FoodCommerceTests.StripNewFields(root);
        foreach (JObject person in root["Citizens"]!) person.Remove("CommutePreparation");
        var car = (JObject)root["Citizens"]![0]!["Car"]!; car.Remove("ModelId"); car.Remove("PaintSeed");
        return root;
    }

    [Fact]
    public void SchemaFourIsRefusedWithoutReplacingThePhysicalPayload()
    {
        var city = Journey(); string payload = SchemaFour(city).ToString();
        var data = new Dictionary<string,string> { [SaveStore.StateKey] = payload };
        var read = SaveStore.ReadFrom(data, city.PopulationKey);
        Assert.Equal(SaveReadStatus.DevelopmentSchema, read.Status); Assert.Null(read.State);
        Assert.False(SaveStore.WriteTo(data, city).Success); Assert.Equal(payload, data[SaveStore.StateKey]);
    }

    [Fact]
    public void ReadMetadataDistinguishesSourceSchemaWithoutChangingExistingStatuses()
    {
        Assert.Equal(0, DevelopmentChain.Read(null).SourceSchema);
        Assert.Equal(0, DevelopmentChain.Read("corrupt").SourceSchema);
        int futureSchema = CityState.CurrentSchema + 1;
        var future = DevelopmentChain.Read("{\"Schema\":" + futureSchema + "}");
        Assert.Equal(SaveReadStatus.Future, future.Status); Assert.Equal(futureSchema, future.SourceSchema);
        Assert.Null(future.State);
        var loaded = DevelopmentChain.Read(DevelopmentChain.Write(Journey()));
        Assert.Equal(CityState.CurrentSchema, loaded.SourceSchema);

    }

    [Theory]
    [InlineData(int.MinValue, 0, 0.01)]
    [InlineData(int.MaxValue, 1, 1000)]
    public void AssignedCarAppearanceAndTimingRoundTripExactly(int paint, int timing, double rate)
    {
        var city = Journey(); city.TravelTimingVersion = timing; city.TravelClockRate = rate;
        city.Citizens[0].Car!.PaintSeed = paint;
        string encoded = DevelopmentChain.Write(city); var read = DevelopmentChain.Read(encoded);
        Assert.Equal(SaveReadStatus.Loaded, read.Status);
        Assert.Equal(encoded, DevelopmentChain.Write(read.State!));
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
        Assert.Throws<SaveValidationException>(() => DevelopmentChain.Write(city));
        var json = SaveWire.Parse(DevelopmentChain.Write(Journey()));
        json["TravelTimingVersion"] = city.TravelTimingVersion; json["TravelClockRate"] = city.TravelClockRate;
        json["Citizens"]![0]!["Car"]!["ModelId"] = city.Citizens[0].Car!.ModelId;
        Assert.Equal(SaveReadStatus.Invalid, DevelopmentChain.Read(json.ToString()).Status);
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
        var json = SaveWire.Parse(DevelopmentChain.Write(Journey())); var car = (JObject)json["Citizens"]![0]!["Car"]!;
        if (defect == "missing-clock") json.Remove("TravelClockRate");
        if (defect == "string-clock") json["TravelClockRate"] = "1.75";
        if (defect == "float-version") json["TravelTimingVersion"] = 1.0;
        if (defect == "missing-model") car.Remove("ModelId");
        if (defect == "float-paint") car["PaintSeed"] = 1.0;
        if (defect == "string-paint") car["PaintSeed"] = "10";
        Assert.Equal(SaveReadStatus.Invalid, DevelopmentChain.Read(json.ToString()).Status);
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
        Assert.Equal(SaveReadStatus.DevelopmentSchema, SaveStore.ReadFrom(data, "physical").Status);
        Assert.False(SaveStore.WriteTo(data, Journey()).Success); Assert.Same(payload, data[SaveStore.StateKey]);
    }
}
