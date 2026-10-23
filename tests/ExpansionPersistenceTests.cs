using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class ExpansionPersistenceTests
{
    private static CityState City() => Population.Create(
        new[] { new CitizenIdentity { Id="c0", Name="Ada Resident", Age=30, Persona=Persona.Commuter, AppearanceSeed=15 },
            new CitizenIdentity { Id="c1", Name="Bob Citizen", Age=60, Persona=Persona.Retiree, AppearanceSeed=20 } },
        new[] { new Place { Id="home", Name="Home", Kind=PlaceKind.Home, Capacity=2 },
            new Place { Id="work", Name="Work", Kind=PlaceKind.Work, Capacity=2, Position=new Position(50,0,0) },
            new Place { Id="leisure", Name="Park", Kind=PlaceKind.Leisure, Capacity=2, Position=new Position(25,0,0) } },
        "source", "0.40.4", 17, 1440);

    private static JObject Legacy(CityState city)
    {
        var root=JObject.Parse(SaveCodec.Write(city)); root["Schema"]=1;
        Keep(root,"Schema RloVersion PopulationKey Seed Minute Places Citizens");
        foreach(var token in root["Citizens"]!.Cast<JObject>())
            Keep(token,"Identity HomeId DayPlaceId ShopId LeisureId Preferences Phase Activity PlaceId DestinationId DestinationActivity Journey DepartureMinute NextEventMinute ActivityUntilMinute CompletedJourneys MissedActivities RecentVisits");
        foreach(var token in root["Places"]!.Cast<JObject>())
            Keep(token,"Id Name District Kind Position Capacity OpenMinute CloseMinute WeeklyHours");
        return root;
    }
    private static void Keep(JObject obj,string names)
    {
        var allowed=names.Split(' ').ToHashSet(StringComparer.Ordinal);
        foreach(var p in obj.Properties().ToArray()) if(!allowed.Contains(p.Name)) p.Remove();
    }

    [Fact]
    public void StrictLegacyMigrationPreservesActiveJourneyAndSourceIdentities()
    {
        var city=City(); var person=city.Citizens[0];
        person.Phase=CitizenPhase.Travelling; person.DestinationId="work"; person.DestinationActivity=Activity.Work;
        person.DepartureMinute=1435; person.NextEventMinute=1455; person.ActivityUntilMinute=2000;
        person.Journey=new Route { Legs=new List<RouteLeg> { new() {From=city.Places.Single(p=>p.Id=="home").Position,To=new Position(50,0,0),Minutes=20,Mode=TravelMode.Walk} } };
        string payload=Legacy(city).ToString();
        var data=new Dictionary<string,string> { [SaveStore.StateKey]=payload, ["rlo:data"]="untouched" };
        var read=SaveStore.ReadFrom(data,"source");
        Assert.Equal(SaveReadStatus.Loaded,read.Status); Assert.True(read.Migrated);
        Assert.Equal(1, read.SourceSchema);
        Assert.Equal(payload,data[SaveStore.StateKey]);
        var migrated=read.State!;
        Assert.Equal(CityState.CurrentSchema,migrated.Schema); Assert.Equal(2,migrated.SourceIdentities.Count);
        Assert.Equal(1455,migrated.Citizens[0].NextEventMinute);
        Assert.Equal(person.Journey.PositionAt(5).X,migrated.Citizens[0].Journey!.PositionAt(5).X);
        Assert.All(migrated.Citizens,c=>Assert.True(c.NextDepartureMinute>migrated.Minute));
        Assert.True(SaveStore.WriteTo(data,migrated).Success);
        Assert.False(SaveCodec.Read(data[SaveStore.StateKey]).Migrated);
        Assert.Equal("untouched",data["rlo:data"]);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("unknown")]
    [InlineData("bad-reference")]
    [InlineData("bad-type")]
    public void InvalidLegacyIsNeverRepairedByMigration(string corruption)
    {
        var json=Legacy(City());
        if(corruption=="missing") ((JObject)json["Citizens"]![0]!).Remove("NextEventMinute");
        if(corruption=="unknown") json["SourceIdentities"]=new JArray();
        if(corruption=="bad-reference") json["Citizens"]![0]!["HomeId"]="absent";
        if(corruption=="bad-type") json["Citizens"]![0]!["Identity"]!["Age"]="30";
        string payload=json.ToString(); var data=new Dictionary<string,string> { [SaveStore.StateKey]=payload };
        Assert.Equal(SaveReadStatus.Invalid,SaveStore.ReadFrom(data,"source").Status);
        Assert.False(SaveStore.WriteTo(data,City()).Success); Assert.Equal(payload,data[SaveStore.StateKey]);
    }

    private static void StripTiming(JObject root)
    {
        root.Remove("TravelTimingVersion"); root.Remove("TravelClockRate");
        root.Remove("Traffic"); root.Remove("TrafficTopologyId"); FoodCommerceTests.StripNewFields(root);
        foreach (var person in root["Citizens"]!.Cast<JObject>())
        {
            person.Remove("CommutePreparation");
            if (person["Car"] is JObject car) { car.Remove("ModelId"); car.Remove("PaintSeed"); }
        }
    }

    private static JObject SecondSchema(CityState city)
    {
        var root = JObject.Parse(SaveCodec.Write(city)); root["Schema"] = 2;
        root.Remove("PopulationTarget"); root.Remove("InitialImportedResidents");
        StripTiming(root);
        foreach (var place in root["Places"]!.Cast<JObject>()) place.Remove("ResidentialAreaSquareMetres");
        return root;
    }

    [Theory]
    [InlineData(1500, 2040)]
    [InlineData(1440, 1440)]
    [InlineData(1439, 1439)]
    [InlineData(1000000000, 1000000000)]
    public void SchemaTwoMigrationRetunesOnlyFutureWorkerJobAndPreservesPhysicalState(double oldJob, double expectedJob)
    {
        var city = City(); var person = city.Citizens[0];
        person.JoinedMinute = 0; person.NextJobChangeMinute = oldJob; person.Preferences.OwnsCar = true;
        person.Car = new CarState { Id = person.Identity.Id + ":car", ParkedPosition = city.Places.Single(p => p.Id == "home").Position };
        person.Phase = CitizenPhase.Travelling; person.DestinationId = "work"; person.DestinationActivity = Activity.Work;
        person.DepartureMinute = 1435; person.NextEventMinute = 1455; person.ActivityUntilMinute = 2000;
        person.Journey = new Route { Legs = new List<RouteLeg> { new() { From = person.Car.ParkedPosition, To = new Position(50,0,0), Minutes = 20, Mode = TravelMode.Car } } };
        var payload = SecondSchema(city);
        var result = SaveCodec.Read(payload.ToString());
        Assert.Equal(SaveReadStatus.Loaded, result.Status); Assert.True(result.Migrated);
        Assert.Equal(2, result.SourceSchema);
        var copy = result.State!; Assert.Equal(CityState.CurrentSchema, copy.Schema);
        Assert.Equal(2, copy.PopulationTarget); Assert.Equal(2, copy.InitialImportedResidents);
        Assert.Equal(expectedJob, copy.Citizens[0].NextJobChangeMinute);
        var wire = SecondSchema(copy); wire["Citizens"]![0]!["NextJobChangeMinute"] = oldJob;
        Assert.True(JToken.DeepEquals(payload, wire));
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(SaveCodec.Write(copy)).Status);
    }

    [Fact]
    public void SchemaThreeMigrationChangesOnlySchemaWithoutRetuningJobsOrReshapingResidents()
    {
        var city = City(); var person = city.Citizens[0];
        person.NextJobChangeMinute = city.Minute + 50;
        person.Preferences.OwnsCar = true;
        person.Car = new CarState { Id = person.Identity.Id + ":car", ParkedPosition = city.Places.Single(p => p.Id == "home").Position };
        person.Phase = CitizenPhase.Travelling; person.DestinationId = "work"; person.DestinationActivity = Activity.Work;
        person.DepartureMinute = 1435; person.NextEventMinute = 1455; person.ActivityUntilMinute = 2000;
        person.Journey = new Route { Legs = new List<RouteLeg> { new() { From = person.Car.ParkedPosition, To = new Position(50,0,0), Minutes = 20, Mode = TravelMode.Car } } };
        var original = JObject.Parse(SaveCodec.Write(city)); original["Schema"] = 3; StripTiming(original);
        string payload = original.ToString();
        var data = new Dictionary<string,string> { [SaveStore.StateKey] = payload };
        var read = SaveStore.ReadFrom(data, city.PopulationKey);
        Assert.Equal(SaveReadStatus.Loaded, read.Status); Assert.True(read.Migrated);
        Assert.Equal(3, read.SourceSchema);
        Assert.Same(payload, data[SaveStore.StateKey]);
        var migrated = read.State!;
        Assert.Equal(CityState.CurrentSchema, migrated.Schema);
        Assert.Equal(city.Citizens.Count, migrated.Citizens.Count);
        Assert.Equal(person.NextJobChangeMinute, migrated.Citizens[0].NextJobChangeMinute);
        Assert.Equal(person.HomeId, migrated.Citizens[0].HomeId);
        Assert.Equal(person.Car.Id, migrated.Citizens[0].Car!.Id);
        Assert.Equal(person.Journey.PositionAt(5).X, migrated.Citizens[0].Journey!.PositionAt(5).X);
        var after = JObject.Parse(SaveCodec.Write(migrated)); after["Schema"] = 3; StripTiming(after);
        Assert.True(JToken.DeepEquals(original, after));
        Assert.False(SaveCodec.Read(SaveCodec.Write(migrated)).Migrated);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("unknown")]
    [InlineData("accounting")]
    [InlineData("type")]
    public void MalformedSchemaThreeIsNotRepaired(string defect)
    {
        var json = JObject.Parse(SaveCodec.Write(City())); json["Schema"] = 3; StripTiming(json);
        if (defect == "missing") json.Remove("InitialImportedResidents");
        if (defect == "unknown") json["HouseholdPolicyVersion"] = 1;
        if (defect == "accounting") json["PopulationTarget"] = 1;
        if (defect == "type") json["Citizens"]![0]!["NextJobChangeMinute"] = "1500";
        string payload = json.ToString(); var data = new Dictionary<string,string> { [SaveStore.StateKey] = payload };
        Assert.Equal(SaveReadStatus.Invalid, SaveStore.ReadFrom(data, "source").Status);
        Assert.False(SaveStore.WriteTo(data, City()).Success);
        Assert.Same(payload, data[SaveStore.StateKey]);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("new-field")]
    [InlineData("sequence")]
    [InlineData("bad-type")]
    public void MalformedSchemaTwoIsNotRepaired(string defect)
    {
        var root = SecondSchema(City());
        if (defect == "missing") ((JObject)root["Citizens"]![0]!).Remove("NextJobChangeMinute");
        if (defect == "new-field") root["PopulationTarget"] = 2;
        if (defect == "sequence") root["NextResidentSequence"] = 5;
        if (defect == "bad-type") root["Citizens"]![0]!["NextJobChangeMinute"] = "1500";
        string payload = root.ToString();
        var data = new Dictionary<string,string> { [SaveStore.StateKey] = payload };
        Assert.Equal(SaveReadStatus.Invalid, SaveStore.ReadFrom(data, "source").Status);
        Assert.False(SaveStore.WriteTo(data, City()).Success);
        Assert.Equal(payload, data[SaveStore.StateKey]);
    }

    [Fact]
    public void TurnoverRoundTripPreservesOriginalSourceAndNewPersistentIdentity()
    {
        var city=City(); var source=city.SourceIdentities.Select(Population.CloneIdentity).ToArray();
        city.Citizens[0].NextDepartureMinute=city.Minute;
        var sim=new CitySimulator(city,new Direct()); sim.AdvanceTo(city.Minute);
        Assert.Equal(1,city.DepartedCount); Assert.Contains(city.Citizens,c=>c.Identity.Id=="pc:1");
        string json=SaveCodec.Write(city); var copy=SaveCodec.Read(json).State!;
        Assert.Equal(json,SaveCodec.Write(copy)); Assert.Equal(2,copy.Citizens.Count);
        Assert.All(source,id=>Assert.True(Population.EqualIdentity(id,copy.SourceIdentities.Single(c=>c.Id==id.Id))));
        Assert.DoesNotContain(copy.Citizens,c=>c.Identity.Id==copy.RecentDepartures[0].Identity.Id);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("forged-source")]
    [InlineData("sequence")]
    [InlineData("active-departure")]
    [InlineData("missing-car")]
    [InlineData("foreign-car")]
    public void ExpansionCorruptionIsRejected(string defect)
    {
        var state=City();
        if(defect=="source") state.SourceIdentities.Clear();
        if(defect=="forged-source") state.Citizens[0].Identity.Name="Changed";
        if(defect=="sequence") state.NextResidentSequence=4;
        if(defect=="active-departure") {state.DepartedCount=1;state.NextResidentSequence=2;state.RecentDepartures.Add(new DepartureRecord{Identity=Population.CloneIdentity(state.Citizens[0].Identity),JoinedMinute=0,DepartedMinute=1440});}
        if(defect=="missing-car" || defect=="foreign-car")
        {
            var person=state.Citizens[0];person.Preferences.OwnsCar=true;
            person.Phase=CitizenPhase.Travelling;person.DestinationId="work";person.DestinationActivity=Activity.Work;
            person.DepartureMinute=1440;person.NextEventMinute=1441;person.ActivityUntilMinute=2000;
            person.Journey=new Route{Legs=new List<RouteLeg>{new(){From=new Position(0,0,0),To=new Position(50,0,0),Minutes=1,Mode=TravelMode.Car}}};
            if(defect=="foreign-car")person.Car=new CarState{Id="c9:car",ParkedPosition=new Position(0,0,0)};
        }
        Assert.Throws<SaveValidationException>(()=>SaveCodec.Write(state));
    }
    [Fact]
    public void MigratedResidentWithoutOldLeisureCanVisitNewParkWithoutChangingPhysicalState()
    {
        var city=City();city.Places.RemoveAll(p=>p.Kind==PlaceKind.Leisure);
        foreach(var c in city.Citizens)c.LeisureId="";
        var migrated=SaveCodec.Read(Legacy(city).ToString()).State!;
        var person=migrated.Citizens[0];double next=person.NextEventMinute;
        CatalogueMigration.Enrich(migrated,new[]{new Place{Id="new-park",Name="Park",Kind=PlaceKind.Leisure,Capacity=64}});
        Assert.Equal("new-park",person.LeisureId);
        Assert.Equal(Activity.Leisure,DailyRoutine.At(person,migrated.Seed,5*1440+900,migrated.Places).Activity);
        Assert.Equal("home",person.PlaceId);Assert.Equal(next,person.NextEventMinute);Assert.Null(person.Journey);
        Assert.Equal(SaveReadStatus.Loaded,SaveCodec.Read(SaveCodec.Write(migrated)).Status);
    }
    [Fact]
    public void LegacySchemasKeepFourMiCharacterLimit()
    {
        foreach (var payload in new[] { Legacy(City()), SecondSchema(City()) })
        {
            string oversized = new string(' ', SaveCodec.LegacyMaxInputCharacters) + payload;
            var result = SaveCodec.Read(oversized);
            Assert.Equal(SaveReadStatus.Invalid, result.Status);
            Assert.Contains("Legacy state exceeds", result.Error);
        }
    }

    private sealed class Direct:IRoutePlanner
    {
        public Route Plan(Place from,Place to,double minute,TravelPreferences pref)=>new(){Legs=new List<RouteLeg>{new(){From=from.Position,To=to.Position,Minutes=1,Mode=TravelMode.Walk}}};
    }
}
