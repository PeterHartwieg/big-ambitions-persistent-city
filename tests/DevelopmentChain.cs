using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

internal static class DevelopmentChain
{
    public static SaveReadResult Read(string? source) => source != null && SaveCodec.DevelopmentSchemaOf(source) != null
        ? SaveCodec.Read(source) : SaveWire.Read(source);

    public static string Write(CityState state) => SaveWire.Text(state);

    public static void ValidateForActivation(CityState state) => SaveCodec.ValidateForActivation(state);

    public static (int Streaming, int Semantic) CountRecords(CityState state, string source) => SaveCodec.CountRecords(state, source);

    public static void RequireRefusedCapabilities(string source)
        => Assert.Contains("older development build", Assert.Throws<SaveValidationException>(() => SaveCodec.RequireExecutableCapabilities(source)).Message);
}
