using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

[Collection("Serialization defaults")]
public sealed class ActivationValidationTests
{
    private static CityState Example() => (CityState)typeof(PersistenceTests)
        .GetMethod("Example", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!;
    private static TextWriter Counter() => (TextWriter)Activator.CreateInstance(typeof(SaveCodec)
        .GetNestedType("CharacterCountingWriter", BindingFlags.NonPublic)!, true)!;
    private static long Count(TextWriter writer) => (long)writer.GetType().GetProperty("Count")!.GetValue(writer)!;

    [Fact]
    public void ActivationMatchesWriteAndPreservesInterruptedJourneyAndEscapedText()
    {
        var state = Example();
        state.Places[0].Name = "quote\" slash\\ newline\n tab\t control\u0001 unicode日 surrogate😀";
        string before = SaveCodec.Write(state);
        SaveCodec.ValidateForActivation(state);
        Assert.Equal(before, SaveCodec.Write(state));
        var resumed = SaveCodec.Read(before);
        Assert.Equal(SaveReadStatus.Loaded, resumed.Status);
        SaveCodec.ValidateForActivation(resumed.State!);
        Assert.Equal(before, SaveCodec.Write(resumed.State!));
        using var counter = Counter();
        var settings = (JsonSerializerSettings)typeof(SaveCodec).GetMethod("Settings", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!;
        using (var json = new JsonTextWriter(counter)) JsonSerializer.CreateDefault(settings).Serialize(json, state);
        Assert.Equal(before.Length, Count(counter));
    }

    [Fact]
    public void ActivationPreparesTheSnapshotBeforeValidationAndSerialization()
    {
        var state = Example();
        int calls = 0;
        typeof(CityState).GetField("SnapshotSynchronizer", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(state, (Action)(() => { calls++; state.Places[0].Name = "coherent snapshot"; }));
        SaveCodec.ValidateForActivation(state);
        Assert.Equal(1, calls); Assert.Equal("coherent snapshot", state.Places[0].Name);
    }

    [Fact]
    public void ActivationAndWriteRefuseMalformedStateWithIdenticalErrors()
    {
        Action<CityState>[] mutations = {
            s => s.Schema++, s => s.Minute = double.NaN,
            s => s.Places.Add(s.Places[0]), s => s.Places.Insert(0, null!),
            s => s.Citizens[0].DestinationId = "missing",
            s => s.Citizens[0].Journey!.Legs[0].Minutes = -1,
            s => s.Places[0].Name = null!
        };
        foreach (var mutate in mutations)
        {
            var state = Example(); mutate(state);
            var original = Record.Exception(() => SaveCodec.Write(state));
            var activation = Record.Exception(() => SaveCodec.ValidateForActivation(state));
            Assert.NotNull(original); Assert.NotNull(activation);
            Assert.Equal(original.GetType(), activation.GetType());
            Assert.Equal(original.Message, activation.Message);
        }
    }

    [Fact]
    public void LookupMatchesAnyWithDuplicatesNullsUnknownKindsAndOrdinalIds()
    {
        var type = typeof(SaveCodec).GetNestedType("FoodPlaceLookup", BindingFlags.NonPublic)!;
        foreach (bool hasNull in new[] { false, true })
        {
            var places = new List<Place> { new() { Id = "a", Kind = PlaceKind.Home },
                new() { Id = "a", Kind = PlaceKind.Shop }, new() { Id = null!, Kind = (PlaceKind)999 } };
            if (hasNull) places.Insert(1, null!);
            var lookup = Activator.CreateInstance(type, new object[] { places })!;
            foreach (string? id in new[] { "a", "A", "missing", null })
            foreach (int kind in new[] { (int)PlaceKind.Home, (int)PlaceKind.Shop, 999, -1 })
            {
                bool expected = false, actual = false;
                var oldError = Record.Exception(() => expected = places.Any(p => p.Id == id && (int)p.Kind == kind));
                var newError = Record.Exception(() => actual = (bool)type.GetMethod("Contains")!.Invoke(lookup, new object?[] { id, kind })!);
                Assert.Equal(oldError?.GetType(), (newError as TargetInvocationException)?.InnerException?.GetType() ?? newError?.GetType());
                if (oldError == null) Assert.Equal(expected, actual);
            }
        }
    }

    private sealed class ExactLengthConverter : JsonConverter<CityState>
    {
        private readonly int length;
        public ExactLengthConverter(int length) { this.length = length; }
        public override void WriteJson(JsonWriter writer, CityState? value, JsonSerializer serializer)
        {
            writer.WriteRawValue("\"");
            string chunk = new string('a', 4096);
            int remaining = length - 2;
            while (remaining >= chunk.Length) { writer.WriteRaw(chunk); remaining -= chunk.Length; }
            writer.WriteRaw(new string('a', remaining)); writer.WriteRaw("\"");
        }
        public override CityState ReadJson(JsonReader reader, Type type, CityState? value, bool hasExistingValue, JsonSerializer serializer)
            => throw new NotSupportedException();
    }

    [Theory]
    [InlineData(-1)] [InlineData(0)] [InlineData(1)]
    public void ActivationEnforcesExactUtf16LimitWithGlobalConverterAndExplicitCompactFormatting(int delta)
    {
        var previous = JsonConvert.DefaultSettings;
        try
        {
            int length = SaveCodec.MaxInputCharacters + delta;
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings {
                Formatting = Formatting.Indented, Converters = { new ExactLengthConverter(length) }
            };
            var state = Example();
            if (delta <= 0) SaveCodec.ValidateForActivation(state);
            else Assert.Equal("Serialized state exceeds the input size limit.",
                Assert.Throws<SaveValidationException>(() => SaveCodec.ValidateForActivation(state)).Message);
            // Small output proves the global converter is also used by the retained save path.
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings { Converters = { new ExactLengthConverter(7) } };
            Assert.Equal("\"aaaaa\"", SaveCodec.Write(state));
            SaveCodec.ValidateForActivation(state);
        }
        finally { JsonConvert.DefaultSettings = previous; }
    }

    [Fact]
    public void CounterCountsEscapingAndOversizedSerializationWithoutRetainingPayload()
    {
        string value = new string('\u0001', 4096) + "😀\"\\\n";
        long itemLength = JsonConvert.SerializeObject(value).Length;
        int items = SaveCodec.MaxInputCharacters / (int)itemLength + 1;
        using var counter = Counter();
        using (var json = new JsonTextWriter(counter)) JsonSerializer.CreateDefault().Serialize(json, Enumerable.Repeat(value, items));
        Assert.Equal(2L + items * itemLength + items - 1, Count(counter));
        Assert.True(Count(counter) > SaveCodec.MaxInputCharacters);
    }
}

[CollectionDefinition("Serialization defaults", DisableParallelization = true)]
public sealed class SerializationDefaultsCollection { }
