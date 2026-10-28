using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class CurrentSchemaOnlyTests
{
    private static CityState City() => Population.Create(
        new[] { new CitizenIdentity { Id = "r", Name = "Resident", Age = 35, Persona = Persona.Commuter } },
        new[] { new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 2 } },
        "current-development", "0.41.1", 42, 0);

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)]
    [InlineData(9)] [InlineData(10)] [InlineData(11)] [InlineData(12)]
    public void EveryOldDevelopmentSchemaBlocksCodecStoreAndSessionWrites(int schema)
    {
        var city = City(); var wire = JObject.Parse(SaveCodec.Write(city)); wire["Schema"] = schema;
        string payload = wire.ToString();
        var data = new Dictionary<string,string> { [SaveStore.StateKey] = payload, ["rlo:state"] = "owned by RLO" };
        var read = SaveStore.ReadFrom(data, city.PopulationKey);
        Assert.Equal(SaveReadStatus.Invalid, read.Status); Assert.Null(read.State);
        Assert.Contains("Unsupported experimental state schema", read.Error);
        Assert.False(SaveStore.WriteTo(data, city).Success);
        var session = SaveSession.Open(data, city.PopulationKey);
        Assert.False(session.WriteTo(data, city).Success);
        Assert.Same(payload, data[SaveStore.StateKey]); Assert.Equal("owned by RLO", data["rlo:state"]);
    }

    [Fact]
    public void APreviouslyWritableSessionCannotOverwriteAnOldPayloadInsertedAfterLoad()
    {
        var city = City(); var data = new Dictionary<string,string>();
        var session = SaveSession.Open(data, city.PopulationKey); Assert.True(session.WriteTo(data, city).Success);
        var wire = JObject.Parse(data[SaveStore.StateKey]); wire["Schema"] = 12;
        string oldPayload = wire.ToString(); data[SaveStore.StateKey] = oldPayload;
        Assert.False(session.WriteTo(data, city).Success);
        Assert.Same(oldPayload, data[SaveStore.StateKey]);
    }

    [Fact]
    public void CurrentVersionRequiresCurrentShapeRatherThanFillingMissingFields()
    {
        var city = City(); var wire = JObject.Parse(SaveCodec.Write(city)); wire.Remove("Food");
        string incomplete = wire.ToString();
        var data = new Dictionary<string,string> { [SaveStore.StateKey] = incomplete };
        Assert.Equal(SaveReadStatus.Invalid, SaveStore.ReadFrom(data, city.PopulationKey).Status);
        Assert.False(SaveStore.WriteTo(data, city).Success); Assert.Same(incomplete, data[SaveStore.StateKey]);
    }
}
