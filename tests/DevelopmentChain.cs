using System.Collections.Generic;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>F5c-lite: <see cref="SaveCodec.Read"/> refuses schemas 1-25 as DevelopmentSchema and no product path
/// reaches the older codecs. Tests of those frozen codecs call the preserved chain explicitly until the slice that
/// removes them. For schema-26 payloads and states both members are exactly SaveCodec.Read/Write.</summary>
internal static class DevelopmentChain
{
    public static SaveReadResult Read(string? source) => source != null && SaveCodec.DevelopmentSchemaOf(source) != null
        ? SaveCodec.ReadDevelopmentChain(source) : SaveCodec.Read(source);

    public static string Write(CityState state) => state.Schema == SaveCodec.LatestDevelopmentSchema
        ? SaveCodec.WriteForSchema(state, SaveCodec.LatestDevelopmentSchema) : SaveCodec.Write(state);

    /// <summary>The writer-25 bytes of a fresh state, for tests that relabel a payload as an older schema.</summary>
    public static string Write25(CityState state)
    {
        int schema = state.Schema;
        try { state.Schema = SaveCodec.LatestDevelopmentSchema; return SaveCodec.WriteForSchema(state, SaveCodec.LatestDevelopmentSchema); }
        finally { state.Schema = schema; }
    }

    /// <summary>Roster tuple slot of the held decision: tuple v3 (schema 26) omits AnchorId and Slot.</summary>
    public static int DecisionSlot(Newtonsoft.Json.Linq.JToken root) => (int)root["Schema"]! >= SaveCodec.PersonSchema ? 8 : 9;

    /// <summary>The product session refuses an older development payload with DevelopmentSchema and keeps every
    /// key byte-identical; the frozen chain still decodes it for the old-codec tests.</summary>
    public static SaveReadResult RefusedThenRead(IDictionary<string, string> data, string populationKey)
    {
        var before = new Dictionary<string, string>(data);
        var refused = SaveSession.Open(data, populationKey).InitialReadResult;
        Assert.Equal(SaveReadStatus.DevelopmentSchema, refused.Status); Assert.Null(refused.State);
        Assert.Equal(SaveReadStatus.DevelopmentSchema, SaveStore.ReadFrom(data, populationKey).Status);
        Assert.Equal(before, data);
        var read = SaveCodec.ReadDevelopmentChain(data[SaveStore.StateKey]);
        if (read.Status == SaveReadStatus.Loaded) Assert.Equal(populationKey, read.State!.PopulationKey);
        return read;
    }

    /// <summary>The early authority probe refuses an older development payload with the development message.</summary>
    public static void RequireRefusedCapabilities(string source)
        => Assert.Contains("older development build", Assert.Throws<SaveValidationException>(() => SaveCodec.RequireExecutableCapabilities(source)).Message);
}
