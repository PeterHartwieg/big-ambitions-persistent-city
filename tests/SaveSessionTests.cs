using System.Collections.Generic;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class SaveSessionTests
{
    private static CityState City(string key = "source") => Population.Create(
        new[] { new CitizenIdentity { Id = "resident", Name = "Resident", Age = 35, Persona = Persona.Commuter } },
        new[] { new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 2 } }, key, "0.40.4", 42, 0);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RepeatedSavesReuseValidationAndPublishLoadableCurrentState(bool existing)
    {
        var city = City(); var data = new Dictionary<string,string>();
        if (existing) data[SaveStore.StateKey] = SaveCodec.Write(city);
        var session = SaveSession.Open(data, city.PopulationKey);
        Assert.Equal(existing ? SaveReadStatus.Loaded : SaveReadStatus.Missing, session.InitialReadResult.Status);
        for (int i = 1; i <= 3; i++)
        {
            city.Citizens[0].CompletedJourneys = i;
            Assert.True(session.WriteTo(data, city).Success);
            Assert.Equal(i, SaveStore.ReadFrom(data, city.PopulationKey).State!.Citizens[0].CompletedJourneys);
        }
        Assert.Equal(1, session.StoredPayloadValidationCount);
    }

    [Theory]
    [InlineData("corrupt")]
    [InlineData("future")]
    [InlineData("foreign")]
    [InlineData("null")]
    public void ChangedPayloadIsStrictlyRejectedAndPreserved(string defect)
    {
        var data = new Dictionary<string,string>(); var city = City();
        var session = SaveSession.Open(data, city.PopulationKey);
        Assert.True(session.WriteTo(data, city).Success);
        string changed = defect == "future" ? "{\"Schema\":" + (CityState.CurrentSchema + 1) + "}" : defect == "foreign" ? SaveCodec.Write(City("foreign")) : defect == "null" ? null! : "corrupt";
        data[SaveStore.StateKey] = changed;
        Assert.False(session.WriteTo(data, city).Success);
        Assert.Same(changed, data[SaveStore.StateKey]);
        Assert.Equal(2, session.StoredPayloadValidationCount);
    }

    [Fact]
    public void DifferentDictionaryAndEqualButDifferentStringReferencesAreRevalidated()
    {
        var city = City(); string raw = SaveCodec.Write(city);
        var first = new Dictionary<string,string> { [SaveStore.StateKey] = raw };
        var session = SaveSession.Open(first, city.PopulationKey);
        var second = new Dictionary<string,string> { [SaveStore.StateKey] = raw };
        Assert.True(session.WriteTo(second, city).Success);
        Assert.Equal(2, session.StoredPayloadValidationCount);
        second[SaveStore.StateKey] = new string(second[SaveStore.StateKey].ToCharArray());
        Assert.True(session.WriteTo(second, city).Success);
        Assert.Equal(3, session.StoredPayloadValidationCount);
        Assert.True(session.WriteTo(second, city).Success);
        Assert.Equal(3, session.StoredPayloadValidationCount);
        Assert.Same(raw, first[SaveStore.StateKey]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SnapshotCanBindToExactLivePayloadWithoutAnotherDecode(bool existing)
    {
        var city = City(); var snapshot = new Dictionary<string,string>();
        if (existing) snapshot[SaveStore.StateKey] = SaveCodec.Write(city);
        var session = SaveSession.Open(snapshot, city.PopulationKey);
        var live = new Dictionary<string,string>(snapshot);
        Assert.True(session.BindTo(live).Success);
        Assert.True(session.WriteTo(live, city).Success);
        Assert.Equal(1, session.StoredPayloadValidationCount);
    }

    [Theory]
    [InlineData("future")]
    [InlineData("foreign")]
    [InlineData("null")]
    [InlineData("corrupt")]
    public void BindingToChangedLivePayloadStrictlyRefusesDamage(string defect)
    {
        var city = City(); var snapshot = new Dictionary<string,string> { [SaveStore.StateKey] = SaveCodec.Write(city) };
        var session = SaveSession.Open(snapshot, city.PopulationKey);
        string raw = defect == "future" ? "{\"Schema\":" + (CityState.CurrentSchema + 1) + "}" : defect == "foreign" ? SaveCodec.Write(City("foreign")) : defect == "null" ? null! : "corrupt";
        var live = new Dictionary<string,string> { [SaveStore.StateKey] = raw };
        Assert.False(session.BindTo(live).Success);
        Assert.Same(raw, live[SaveStore.StateKey]);
        Assert.Equal(2, session.StoredPayloadValidationCount);
        Assert.False(session.WriteTo(live, city).Success);
        Assert.Same(raw, live[SaveStore.StateKey]);
    }

    [Fact]
    public void MissingAndNullRemainDistinctAndPopulationCannotChange()
    {
        var city = City(); var data = new Dictionary<string,string>();
        var session = SaveSession.Open(data, city.PopulationKey);
        Assert.False(session.WriteTo(data, City("foreign")).Success);
        Assert.Empty(data);
        Assert.True(session.WriteTo(data, city).Success);
        data.Remove(SaveStore.StateKey);
        Assert.True(session.WriteTo(data, city).Success);
        Assert.Equal(2, session.StoredPayloadValidationCount);
        data[SaveStore.StateKey] = null!;
        var invalid = SaveSession.Open(data, city.PopulationKey);
        Assert.Equal(SaveReadStatus.Invalid, invalid.InitialReadResult.Status);
        Assert.False(invalid.WriteTo(data, city).Success);
        Assert.Null(data[SaveStore.StateKey]);
    }

    [Fact]
    public void InvalidNewStateNeverChangesPayloadOrCachedValidation()
    {
        var city = City(); var data = new Dictionary<string,string> { [SaveStore.StateKey] = SaveCodec.Write(city) };
        var session = SaveSession.Open(data, city.PopulationKey); string raw = data[SaveStore.StateKey];
        city.Citizens[0].HomeId = "missing";
        Assert.False(session.WriteTo(data, city).Success);
        Assert.Same(raw, data[SaveStore.StateKey]);
        Assert.Equal(1, session.StoredPayloadValidationCount);
    }
}
