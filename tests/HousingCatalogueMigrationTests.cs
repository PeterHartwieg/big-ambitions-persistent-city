using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;
public sealed class HousingCatalogueMigrationTests
{
    [Fact]
    public void OmittedLegacyHomesKeepOnlyExistingOccupantsAndPhysicalReferences()
    {
        var home = new Place { Id="old", Name="Old", Kind=PlaceKind.Home, Capacity=16, Position=new(10,0,0) };
        var city = Population.Create(new[] { new CitizenIdentity { Id="a", Name="A Resident", Age=30 } },new[]{home},"key","0.40.4",7,100);
        city.Places.Add(new Place { Id="empty", Name="Empty", Kind=PlaceKind.Home, Capacity=16 });
        var citizen=city.Citizens[0]; var identity=citizen.Identity;
        CatalogueMigration.Enrich(city,new[]{new Place { Id="verified",Name="Verified",Kind=PlaceKind.Home,Capacity=10,ResidentialAreaSquareMetres=400 }});
        Population.ExpandToHousing(city);
        Assert.Equal(1,home.Capacity);Assert.Equal(0,city.Places.Single(p=>p.Id=="empty").Capacity);
        Assert.Equal(9,city.Citizens.Count);Assert.Same(identity,citizen.Identity);
        Assert.Equal("old",citizen.HomeId);Assert.Equal("old",citizen.PlaceId);
        Assert.Equal(10,home.Position.X);Assert.Equal(100,citizen.NextEventMinute);
        Assert.All(city.Citizens.Skip(1),c=>Assert.Equal("verified",c.HomeId));
        Assert.Equal(SaveReadStatus.Loaded,SaveCodec.Read(SaveCodec.Write(city)).Status);
    }
    [Fact]
    public void SoleExistingResidentSurvivesEvenIfNoAreaCanBeRediscovered()
    {
        var city=Population.Create(new[]{new CitizenIdentity { Id="a",Name="A Resident",Age=30 }},
            new[]{new Place { Id="old",Name="Old",Kind=PlaceKind.Home,Capacity=16 }},"key","0.40.4",7,100);
        CatalogueMigration.Enrich(city,System.Array.Empty<Place>());
        Population.ExpandToHousing(city);
        Assert.Single(city.Citizens);Assert.Equal(1,city.PopulationTarget);Assert.Equal(1,city.Places[0].Capacity);
        Assert.Equal(SaveReadStatus.Loaded,SaveCodec.Read(SaveCodec.Write(city)).Status);
    }
}
