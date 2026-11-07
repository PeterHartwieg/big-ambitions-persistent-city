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
    /// <summary>The frozen pre-26 chain migrates every schema up to 25 to exactly writer 25.</summary>
    public const int ChainOutput = 25;
    /// <summary>F5d: the frozen writer-26 shape (represented consumers on their host's excursion schedule). Schema 27
    /// has no represented consumer, so tests of that path build their city in this schema.</summary>
    public const int Represented = SaveCodec.PersonSchema;

    /// <summary>A city with represented consumers (factor 2/3) runs the frozen writer-26 path.</summary>
    public static CityState RepresentedSchema(CityState state, int factor)
    {
        if (factor > 1) state.Schema = Represented;
        return state;
    }

    public static SaveReadResult Read(string? source) => source == null ? SaveCodec.Read(source)
        : SaveCodec.DevelopmentSchemaOf(source) == SaveCodec.PersonSchema ? SaveCodec.ReadWriter26(source)
        : SaveCodec.DevelopmentSchemaOf(source) == SaveCodec.IndependentSchema ? SaveCodec.ReadWriter27(source)
        : SaveCodec.DevelopmentSchemaOf(source) == SaveCodec.WorkplaceCapacitySchema ? SaveCodec.ReadWriter28(source)
        : SaveCodec.DevelopmentSchemaOf(source) == SaveCodec.ResearchSchema ? SaveCodec.ReadWriter29(source)
        : SaveCodec.DevelopmentSchemaOf(source) != null ? SaveCodec.ReadDevelopmentChain(source) : SaveCodec.Read(source);

    /// <summary>F5e: the frozen writer-27 shape (F5d's, seated under the fixed BusinessAllocation of 16). Writer 28 has
    /// the same shape and differs only in the capacity rule its cities were seated under.</summary>
    public const int Independent27 = SaveCodec.IndependentSchema;

    /// <summary>The frozen writer-27 bytes of a current state (same shape), relabelled; the state is restored.</summary>
    public static string Write27(CityState state)
    {
        int schema = state.Schema;
        try { state.Schema = Independent27; return SaveCodec.WriteForSchema(state, Independent27); }
        finally { state.Schema = schema; }
    }

    /// <summary>C5c: the frozen writer-28 shape (F5e's, before the research section).</summary>
    public const int Workplace28 = SaveCodec.WorkplaceCapacitySchema;

    /// <summary>The frozen writer-28 bytes of a current state (its research section is not written); the state is restored.</summary>
    public static string Write28(CityState state)
    {
        int schema = state.Schema;
        try { state.Schema = Workplace28; return SaveCodec.WriteForSchema(state, Workplace28); }
        finally { state.Schema = schema; }
    }

    /// <summary>E6c: the frozen writer-29 shape (C5c's, before the vendor table and the scope channel).</summary>
    public const int Research29 = SaveCodec.ResearchSchema;

    /// <summary>The frozen writer-29 bytes of a current state without vendor rows or vehicle scopes; the state is restored.</summary>
    public static string Write29(CityState state)
    {
        int schema = state.Schema;
        try { state.Schema = Research29; return SaveCodec.WriteForSchema(state, Research29); }
        finally { state.Schema = schema; }
    }

    public static string Write(CityState state) => state.Schema == ChainOutput || state.Schema == Represented || state.Schema == Independent27 || state.Schema == Workplace28 || state.Schema == Research29
        ? SaveCodec.WriteForSchema(state, state.Schema) : SaveCodec.Write(state);

    /// <summary>The activation save contract for the state's own schema (the current writer or a frozen one).</summary>
    public static void ValidateForActivation(CityState state)
    {
        if (state.Schema == Represented || state.Schema == ChainOutput || state.Schema == Independent27 || state.Schema == Workplace28 || state.Schema == Research29) SaveCodec.ValidateForActivation(state, state.Schema);
        else SaveCodec.ValidateForActivation(state);
    }

    /// <summary>Both record counters for the state's own schema (the current writer or a frozen one).</summary>
    public static (int Streaming, int Semantic) CountRecords(CityState state, string source) => SaveCodec.CountRecordsForSchema(state, source, state.Schema);

    /// <summary>The writer-25 bytes of a fresh state, for tests that relabel a payload as an older schema.</summary>
    public static string Write25(CityState state)
    {
        int schema = state.Schema;
        try { state.Schema = ChainOutput; return Legacy(state, () => SaveCodec.WriteForSchema(state, ChainOutput)); }
        finally { state.Schema = schema; }
    }

    /// <summary>Runs a frozen pre-26 writer on a fresh state in the pre-26 roster form, then restores the state.
    /// Schema 26 stores each roster record as its person's own (slot 0, own anchor, factor 1); schemas up to 25
    /// stored a represented consumer under its host and slot with the factor. The scheduling host is the same.</summary>
    public static T Legacy<T>(CityState state, System.Func<T> write)
    {
        var food = state.Food;
        if (food == null || state.Citizens.Count == 0) return write();
        var saved = new List<(ResidentMeal Meal, string Anchor, int Slot)>(food.Residents.Count);
        int factor = food.RepresentationFactor;
        foreach (var meal in food.Residents) { saved.Add((meal, meal.AnchorId, meal.Slot)); meal.AnchorId = meal.HostId; meal.Slot = meal.HostSlot; }
        food.RepresentationFactor = food.Residents.Count % state.Citizens.Count == 0 ? food.Residents.Count / state.Citizens.Count : factor;
        try { return write(); }
        finally
        {
            foreach (var (meal, anchor, slot) in saved) { meal.AnchorId = anchor; meal.Slot = slot; }
            food.RepresentationFactor = factor;
        }
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

    /// <summary>F5d: the product reader refuses a frozen writer-26 payload as an older development city before its shape.</summary>
    public static SaveReadStatus ProductStatus(string payload, SaveReadStatus frozen)
        => SaveCodec.DevelopmentSchemaOf(payload) != null ? SaveReadStatus.DevelopmentSchema : frozen;

    /// <summary>The capability-header probe of a frozen writer-26 payload: the product refuses its schema, and the same
    /// headers relabelled as the current writer pass the probe (it reads only the headers).</summary>
    public static void ProbeCapabilities(string payload)
    {
        if (SaveCodec.DevelopmentSchemaOf(payload) != SaveCodec.PersonSchema) { SaveCodec.RequireExecutableCapabilities(payload); return; }
        RequireRefusedCapabilities(payload);
        SaveCodec.RequireExecutableCapabilities(payload.Replace("\"Schema\":26,", "\"Schema\":" + CityState.CurrentSchema + ","));
    }

    /// <summary>The early authority probe refuses an older development payload with the development message.</summary>
    public static void RequireRefusedCapabilities(string source)
        => Assert.Contains("older development build", Assert.Throws<SaveValidationException>(() => SaveCodec.RequireExecutableCapabilities(source)).Message);
}
