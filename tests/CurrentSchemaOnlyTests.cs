using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class CurrentSchemaOnlyTests
{
    internal static CityState City() => TestCity.Minimal();

    public static IEnumerable<object[]> OldDevelopmentSchemas() => new[] { 0, 2, 12, 25, 26, 35, 36, 37 }.Select(s => new object[] { s });

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
