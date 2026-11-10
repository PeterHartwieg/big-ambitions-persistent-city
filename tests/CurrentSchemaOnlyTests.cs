using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class CurrentSchemaOnlyTests
{
    internal static CityState City() => Population.Create(
        new[] { new CitizenIdentity { Id = "r", Name = "Resident", Age = 35, Persona = Persona.Commuter } },
        new[] { new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 2 } },
        "current-development", "0.41.1", 42, 0);

    /// <summary>Every schema from 1 to the newest older writer: X1 removed the pre-26 reader chain, and this keeps each one
    /// a DevelopmentSchema refusal.</summary>
    public static IEnumerable<object[]> OldDevelopmentSchemas() => Enumerable.Range(1, SaveCodec.LatestDevelopmentSchema).Select(s => new object[] { s });

    [Theory]
    [MemberData(nameof(OldDevelopmentSchemas))]
    public void EveryOldDevelopmentSchemaBlocksCodecStoreAndSessionWrites(int schema)
    {
        var city = City(); var wire = SaveWire.Parse(DevelopmentChain.Write(city)); wire["Schema"] = schema;
        string payload = wire.ToString();
        var data = new Dictionary<string,string> { [SaveStore.StateKey] = payload, ["rlo:state"] = "owned by RLO" };
        var direct = SaveCodec.Read(payload);
        Assert.Equal(SaveReadStatus.DevelopmentSchema, direct.Status); Assert.Equal(schema, direct.SourceSchema);
        Assert.Equal(SaveCodec.DevelopmentSchemaMessage + " (schema " + schema + ")", direct.Error);
        var read = SaveStore.ReadFrom(data, city.PopulationKey);
        // F5c-lite: no migration before release; every older development schema is its own refusal, not corruption.
        Assert.Equal(SaveReadStatus.DevelopmentSchema, read.Status); Assert.Null(read.State); Assert.Equal(schema, read.SourceSchema);
        Assert.Contains("older development build; start a new city", read.Error);
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
        var wire = SaveWire.Parse(data[SaveStore.StateKey]); wire["Schema"] = 12;
        string oldPayload = wire.ToString(); data[SaveStore.StateKey] = oldPayload;
        Assert.False(session.WriteTo(data, city).Success);
        Assert.Same(oldPayload, data[SaveStore.StateKey]);
    }

    [Fact]
    public void CurrentVersionRequiresCurrentShapeRatherThanFillingMissingFields()
    {
        var city = City(); var wire = SaveWire.Parse(DevelopmentChain.Write(city)); wire.Remove("Food");
        string incomplete = wire.ToString();
        var data = new Dictionary<string,string> { [SaveStore.StateKey] = incomplete };
        Assert.Equal(SaveReadStatus.Invalid, SaveStore.ReadFrom(data, city.PopulationKey).Status);
        Assert.False(SaveStore.WriteTo(data, city).Success); Assert.Same(incomplete, data[SaveStore.StateKey]);
    }
}
