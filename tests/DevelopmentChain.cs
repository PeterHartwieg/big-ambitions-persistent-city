using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>Test entry points to the one writer. The schema is held at 1 until release
/// (docs/decisions/2026-11-09-schema-one.md): <see cref="SaveCodec.Read"/> refuses every other schema number as
/// DevelopmentSchema, and there are no frozen writers or readers.</summary>
internal static class DevelopmentChain
{
    /// <summary>The product reader for a payload with another schema number, else the strict reader of an edited body.</summary>
    public static SaveReadResult Read(string? source) => source != null && SaveCodec.DevelopmentSchemaOf(source) != null
        ? SaveCodec.Read(source) : SaveWire.Read(source);

    /// <summary>The writer's payload (the JSON body itself), for tests that edit saved text.</summary>
    public static string Write(CityState state) => SaveWire.Text(state);

    public static void ValidateForActivation(CityState state) => SaveCodec.ValidateForActivation(state);

    public static (int Streaming, int Semantic) CountRecords(CityState state, string source) => SaveCodec.CountRecords(state, source);

    /// <summary>The early authority probe refuses an older development payload with the development message.</summary>
    public static void RequireRefusedCapabilities(string source)
        => Assert.Contains("older development build", Assert.Throws<SaveValidationException>(() => SaveCodec.RequireExecutableCapabilities(source)).Message);
}
