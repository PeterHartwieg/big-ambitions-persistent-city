using System.Collections.Generic;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>F5c-lite: <see cref="SaveCodec.Read"/> refuses every older development schema as DevelopmentSchema. X1 removed
/// the pre-26 reader chain, so a payload up to schema 25 reads exactly as the product reads it; the frozen exact
/// readers from 26 on remain for their own tests.</summary>
internal static class DevelopmentChain
{
    /// <summary>The frozen writer 25 (the last JSON writer before the person table). Its payloads are refused.</summary>
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

    public static SaveReadResult Read(string? source) => source == null ? SaveWire.Read(source)
        : SaveCodec.DevelopmentSchemaOf(source) == CityState.ChangeJournalSchema ? SaveCodec.ReadWriter33(source)
        : SaveCodec.DevelopmentSchemaOf(source) == SaveCodec.EconomicLedgerSchema ? SaveCodec.ReadLedgerWriter(source)
        : SaveCodec.DevelopmentSchemaOf(source) == SaveCodec.PersonSchema ? SaveCodec.ReadWriter26(source)
        : SaveCodec.DevelopmentSchemaOf(source) == SaveCodec.IndependentSchema ? SaveCodec.ReadWriter27(source)
        : SaveCodec.DevelopmentSchemaOf(source) == SaveCodec.WorkplaceCapacitySchema ? SaveCodec.ReadWriter28(source)
        : SaveCodec.DevelopmentSchemaOf(source) == SaveCodec.VendorSchema ? SaveCodec.ReadWriter30(source)
        : SaveCodec.DevelopmentSchemaOf(source) == SaveCodec.ResearchSchema ? SaveCodec.ReadWriter29(source)
        : SaveCodec.DevelopmentSchemaOf(source) != null ? SaveCodec.Read(source) : SaveWire.Read(source);

    /// <summary>F5e: the frozen writer-27 shape (F5d's, seated under the fixed BusinessAllocation of 16). Writer 28 has
    /// the same shape and differs only in the capacity rule its cities were seated under.</summary>
    public const int Independent27 = SaveCodec.IndependentSchema;

    /// <summary>The frozen writer-27 bytes of a current state (same shape), relabelled; the state is restored.</summary>
    public static string Write27(CityState state)
        => Frozen(state, Independent27);

    /// <summary>C5c: the frozen writer-28 shape (F5e's, before the research section).</summary>
    public const int Workplace28 = SaveCodec.WorkplaceCapacitySchema;

    /// <summary>The frozen writer-28 bytes of a current state (its research section is not written); the state is restored.</summary>
    public static string Write28(CityState state)
        => Frozen(state, Workplace28);

    /// <summary>E6c: the frozen writer-29 shape (C5c's, before the vendor table and the scope channel).</summary>
    public const int Research29 = SaveCodec.ResearchSchema;

    /// <summary>The frozen writer-29 bytes of a current state without vendor rows or vehicle scopes; the state is restored.</summary>
    public static string Write29(CityState state)
        => Frozen(state, Research29);

    public static string WriteLedger(CityState state)
        => Frozen(state, SaveCodec.EconomicLedgerSchema);

    public static string Write30(CityState state)
        => Frozen(state, SaveCodec.VendorSchema);

    /// <summary>F7d: the frozen writer-33 JSON body (F6g's shape, the predecessor of the compact wire) of a current
    /// state; the state is restored.</summary>
    public static string Write33(CityState state)
        => Frozen(state, CityState.ChangeJournalSchema);

    /// <summary>E5b4: the frozen writer-35 bytes (F2e's compact wire, original-name policy); the state is restored.</summary>
    public static string Write35(CityState state)
        => Frozen(state, CityState.PlayerHoldSchema);

    /// <summary>A frozen writer's bytes of this state, with the schema relabelled and, unless the state holds revision-2
    /// tuples, the policy that writer executed (E5b4: only writer 36 executes DynamicWire). Both are restored.</summary>
    private static string Frozen(CityState state, int frozen)
    {
        int schema = state.Schema; object? policy = PolicyField.GetValue(state);
        try
        {
            bool dynamic = state.MenuPolicy == FoodNameIdentity.DynamicWire;
            state.Schema = frozen;
            if (dynamic && !HoldsDynamicTuples(state)) state.MenuPolicy = CityState.ExecutableMenuPolicy(frozen);
            return SaveCodec.WriteForSchema(state, frozen);
        }
        finally { state.Schema = schema; PolicyField.SetValue(state, policy); }
    }
    // The unset backing field is restored as unset, so the state's default still follows its schema.
    private static readonly System.Reflection.FieldInfo PolicyField = typeof(CityState).GetField("menuPolicy",
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
    private static bool HoldsDynamicTuples(CityState state)
    {
        if (state.Food?.Residents == null) return false;
        foreach (var meal in state.Food.Residents)
            if (meal?.Decision?.Shops != null) foreach (var shop in meal.Decision.Shops)
                if (shop?.Offers != null) foreach (var offer in shop.Offers)
                    if (offer?.Metadata?.Revision == 2) return true;
        return false;
    }

    /// <summary>F2e: the frozen writer-34 bytes (F7d's compact wire without the schema-35 hold fields); the state is restored.</summary>
    public static string Write34(CityState state)
        => Frozen(state, CityState.CompactSchema);

    public static string WriteAttendance(CityState state)
        => Frozen(state, CityState.AttendanceSchema);

    public static string Write(CityState state) => state.Schema == ChainOutput || state.Schema == Represented || state.Schema == Independent27 || state.Schema == Workplace28 || state.Schema == Research29 || state.Schema == SaveCodec.VendorSchema || state.Schema == SaveCodec.EconomicLedgerSchema
        ? SaveCodec.WriteForSchema(state, state.Schema) : SaveWire.Text(state); // F7d: the compact payload's body

    /// <summary>The activation save contract for the state's own schema (the current writer or a frozen one).</summary>
    public static void ValidateForActivation(CityState state)
    {
        if (state.Schema == Represented || state.Schema == ChainOutput || state.Schema == Independent27 || state.Schema == Workplace28 || state.Schema == Research29 || state.Schema == SaveCodec.VendorSchema) SaveCodec.ValidateForActivation(state, state.Schema);
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

    /// <summary>F5d: the product reader refuses a frozen writer-26 payload as an older development city before its shape.</summary>
    public static SaveReadStatus ProductStatus(string payload, SaveReadStatus frozen)
        => SaveCodec.DevelopmentSchemaOf(payload) != null ? SaveReadStatus.DevelopmentSchema : frozen;

    /// <summary>The capability-header probe of a frozen writer-26 payload: the product refuses its schema, and the same
    /// headers relabelled as the current writer pass the probe (it reads only the headers).</summary>
    public static void ProbeCapabilities(string payload)
    {
        if (SaveCodec.DevelopmentSchemaOf(payload) != SaveCodec.PersonSchema) { SaveCodec.RequireExecutableCapabilities(payload); return; }
        RequireRefusedCapabilities(payload);
        // E5b4: relabelled to the current writer, the header also carries that writer's executable policy.
        SaveCodec.RequireExecutableCapabilities(payload.Replace("\"Schema\":26,", "\"Schema\":" + CityState.CurrentSchema + ",")
            .Replace("\"MenuPolicy\":\"" + FoodNameIdentity.Wire + "\"", "\"MenuPolicy\":\"" + CityState.ExecutableMenuPolicy(CityState.CurrentSchema) + "\""));
    }

    /// <summary>The early authority probe refuses an older development payload with the development message.</summary>
    public static void RequireRefusedCapabilities(string source)
        => Assert.Contains("older development build", Assert.Throws<SaveValidationException>(() => SaveCodec.RequireExecutableCapabilities(source)).Message);
}
