using System;
using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

public sealed class HousingPersistenceTests
{
    private readonly ITestOutputHelper output;
    public HousingPersistenceTests(ITestOutputHelper output) { this.output = output; }

    private static CityState Housing(int beds, int donors = 400) => Population.CreateFromHousing(
        Enumerable.Range(0, donors).Select(i => new CitizenIdentity { Id = "rlo:" + i.ToString("D4"), Name = "Resident Sample " + i, Age = 35, Persona = Persona.Commuter }),
        new[] { new Place { Id = "home", Name = "Residential building", Kind = PlaceKind.Home, Capacity = beds, ResidentialAreaSquareMetres = beds * 40 },
            new Place { Id = "work", Name = "Workplace", Kind = PlaceKind.Work, Capacity = 50000, Position = new Position(100, 0, 0) } },
        "housing-sample", "0.40.4", 27, 1440);

    [Theory]
    [InlineData(100, 90)]
    [InlineData(1000, 900)]
    public void IndependentDonorCatalogueAndAllocationRoundTrip(int beds, int count)
    {
        var city = Housing(beds);
        Assert.Equal(count, city.Citizens.Count);
        Assert.Equal(400, city.SourceIdentities.Count);
        Assert.Equal(Math.Min(400, count), city.InitialImportedResidents);
        Assert.Equal(count - city.InitialImportedResidents + 1, city.NextResidentSequence);
        var json = SaveCodec.Write(city);
        var result = SaveCodec.Read(json);
        Assert.Equal(SaveReadStatus.Loaded, result.Status);

        Assert.Equal(json, SaveCodec.Write(result.State!));
        var changed = JObject.Parse(json); changed["InitialImportedResidents"] = city.InitialImportedResidents - 1;
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(changed.ToString()).Status);
        changed = JObject.Parse(json); changed["PopulationTarget"] = count + 1;
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(changed.ToString()).Status);
    }

    [Theory]
    [InlineData(3, 1)]
    [InlineData(150, 5)]
    public void LargeCurrentPopulationWithVisitsAndCarJourneysRoundTripsWithinBound(int legs, int travellingEvery)
    {
        // Large synthetic allocation stress fixture; this count is not a claim
        // about the interior housing policy or native performance.
        var city = Housing(28328);
        Assert.Equal(25495, city.Citizens.Count);
        int index = 0;
        foreach (var person in city.Citizens)
        {
            person.RecentVisits = Enumerable.Range(0, 32).Select(i => new VisitRecord { AtMinute = i * 40, PlaceId = "home", Activity = Activity.Home }).ToList();
            if (index++ % travellingEvery != 0) continue;
            person.Preferences.OwnsCar = true;
            person.Car = new CarState { Id = person.Identity.Id + ":car", ParkedPosition = new Position(5, 0, 0) };
            person.Phase = CitizenPhase.Travelling; person.DestinationId = "work"; person.DestinationActivity = Activity.Work;
            person.DepartureMinute = 1435; person.NextEventMinute = 1455; person.ActivityUntilMinute = 2000;
            person.Journey = new Route { Legs = new List<RouteLeg> {
                new() { From = new Position(0,0,0), To = new Position(5,0,0), Mode = TravelMode.Walk, Minutes = 2 },
                new() { From = new Position(5,0,0), To = new Position(95,0,0), Mode = TravelMode.Car, Minutes = 16 },
                new() { From = new Position(95,0,0), To = new Position(100,0,0), Mode = TravelMode.Walk, Minutes = 2 } } };
            if (legs > 3)
            {
                person.Journey.Legs.RemoveAt(1);
                for (int leg = 0; leg < legs - 2; leg++)
                    person.Journey.Legs.Insert(leg + 1, new RouteLeg { From = new Position(5 + 90.0 * leg / (legs - 2), 0, 0),
                        To = new Position(5 + 90.0 * (leg + 1) / (legs - 2), 0, 0), Mode = TravelMode.Car, Minutes = 16.0 / (legs - 2) });
            }
        }
        long allocated = GC.GetTotalAllocatedBytes(true);
        var watch = Stopwatch.StartNew();
        string json = SaveCodec.Write(city);
        var writeTime = watch.Elapsed;
        string source = json;
        var read = SaveCodec.Read(source);
        watch.Stop();
        output.WriteLine($"Residents={city.Citizens.Count}; legs={legs}; travelling=1/{travellingEvery}; chars={json.Length}; UTF8 bytes={System.Text.Encoding.UTF8.GetByteCount(json)}; write={writeTime.TotalSeconds:F2}s; total={watch.Elapsed.TotalSeconds:F2}s; allocated={GC.GetTotalAllocatedBytes(true) - allocated} bytes; managed snapshot={GC.GetTotalMemory(false)} bytes");
        Assert.True(json.Length > 4 * 1024 * 1024);
        Assert.True(json.Length < SaveCodec.MaxInputCharacters);
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);

        Assert.Equal(CityState.CurrentSchema, read.SourceSchema);
        Assert.Equal(city.NextResidentSequence, read.State!.NextResidentSequence);
        Assert.Equal(400, read.State.SourceIdentities.Count);
        Assert.All(read.State.Citizens, c => { Assert.Equal(32, c.RecentVisits.Count); if (c.Journey != null) { Assert.Equal(legs, c.Journey.Legs.Count); Assert.Equal(c.Identity.Id + ":car", c.Car!.Id); } });
        Assert.Equal(json, SaveCodec.Write(read.State));
    }

    [Fact]
    public void CurrentOversizedHomeRetainsPopulationThroughInteriorCorrectionAndReload()
    {
        // A bounded version of the old whole-property experiment: 90 occupants in
        // one home now discovered as a two-person rentable interior.
        var original = Housing(100, 4);
        var read = SaveCodec.Read(SaveCodec.Write(original));
        Assert.Equal(SaveReadStatus.Loaded, read.Status);
        var city = read.State!;
        int activeCount = city.Citizens.Count;
        var originalIds = city.Citizens.Select(c => c.Identity.Id).ToHashSet(StringComparer.Ordinal);
        int nextSequence = city.NextResidentSequence;
        var home = city.Places.Single(p => p.Id == "home");
        int correctedCapacity = HousingPolicy.Capacity(54);
        Assert.Equal(2, correctedCapacity);
        Assert.True(activeCount > correctedCapacity);
        CatalogueMigration.Enrich(city, new[] { new Place { Id = home.Id, Name = "Rentable interior", Kind = PlaceKind.Home,
            Capacity = correctedCapacity, ResidentialAreaSquareMetres = 54, EvidenceSource = HousingPolicy.PolicyId } });
        Population.ExpandToHousing(city);
        Assert.Equal(activeCount, city.Citizens.Count);
        Assert.Equal(activeCount, city.PopulationTarget);
        Assert.Equal(activeCount, home.Capacity);
        Assert.Equal(54, home.ResidentialAreaSquareMetres);
        Assert.Contains("existing residents retained", home.EvidenceSource);
        Assert.True(originalIds.SetEquals(city.Citizens.Select(c => c.Identity.Id)));
        Assert.All(city.Citizens, c => { Assert.Equal(home.Id, c.HomeId); Assert.Equal(home.Id, c.PlaceId); });

        new CitySimulator(city, new Direct()).AdvanceTo(city.Minute);
        Assert.Equal(nextSequence, city.NextResidentSequence);
        Assert.True(originalIds.SetEquals(city.Citizens.Select(c => c.Identity.Id)));
        Assert.Equal((long)city.Citizens.Count, (long)city.InitialImportedResidents + city.NextResidentSequence - 1);
        Assert.Equal(activeCount, city.Citizens.Count(c => c.HomeId == home.Id));
        string saved = SaveCodec.Write(city);
        var reloaded = SaveCodec.Read(saved);
        Assert.Equal(SaveReadStatus.Loaded, reloaded.Status);
        Assert.Equal(saved, SaveCodec.Write(reloaded.State!));
    }

    private sealed class Direct : IRoutePlanner
    {
        public Route Plan(Place from, Place to, double minute, TravelPreferences preferences) => new Route
        { Legs = new List<RouteLeg> { new() { From = from.Position, To = to.Position, Minutes = 1, Mode = TravelMode.Walk } } };
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("missing")]
    [InlineData("unknown")]
    [InlineData("number")]
    public void StreamingReaderRetainsStrictNestedShape(string fault)
    {
        string json = SaveCodec.Write(Housing(10, 2));
        if (fault == "duplicate") json = json.Replace("\"JobChanges\":0", "\"JobChanges\":0,\"JobChanges\":0");
        if (fault == "missing") json = json.Replace("\"JobChanges\":0,", "");
        if (fault == "unknown") json = json.Replace("\"JobChanges\":0", "\"JobChanges\":0,\"Foreign\":0");
        if (fault == "number") json = json.Replace("\"JobChanges\":0", "\"JobChanges\":0.0");
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(json).Status);
    }
}
