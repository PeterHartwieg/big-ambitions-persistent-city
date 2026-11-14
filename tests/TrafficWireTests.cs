using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class TrafficWireTests
{
    private static List<TrafficLinkDefinition> Definitions() => new()
    {
        new() { Id = 0, Capacity = 1, FreeFlowMinutes = 0.12507885233586022, HeadwayMinutes = 0.6000000167127065 },
        new() { Id = 1, Capacity = 3, FreeFlowMinutes = 2, HeadwayMinutes = 0.6000000167127065 },
        new() { Id = 7, Capacity = 1, FreeFlowMinutes = 1e-9, HeadwayMinutes = 0,
            Signal = new TrafficSignalSchedule { CycleMinutes = 1.5, GreenStartMinutes = 0.25, GreenDurationMinutes = 0.75, PhaseOffsetMinutes = -3.125 } },
        new() { Id = 8, Capacity = 1024, FreeFlowMinutes = 5, HeadwayMinutes = 0,
            Signal = new TrafficSignalSchedule { CycleMinutes = 2, GreenStartMinutes = 0, GreenDurationMinutes = 2, PhaseOffsetMinutes = 0 } },
        new() { Id = 9, Capacity = 2, FreeFlowMinutes = 5, HeadwayMinutes = 0.1 },
    };

    private static List<TrafficLinkState> Links() => new()
    {
        new() { LinkId = 0, NextDischargeAt = 3420.032815296203 },
        new() { LinkId = 1, NextDischargeAt = 3420.032815296203, MergeCursor = 0, IncomingLinks = { 0, 12 } },
        new() { LinkId = 7, Closed = true, NextDischargeAt = 3426.6311747, Vehicles = { "pc:197", "c 1,2;3%" }, WaitingSources = { "véhicule" } },
        new() { LinkId = 8, NextDischargeAt = 3426.6311747 },
        new() { LinkId = 9, NextDischargeAt = 0, WaitingSources = { "c0" } },
    };

    private static string Json<T>(T value) => JsonConvert.SerializeObject(value);

    [Fact]
    public void DefinitionsAndLinksRoundTripBitForBit()
    {
        var definitions = Definitions(); var links = Links();
        int records = 0;
        var readDefinitions = SaveCodec.ReadTrafficDefinitions(SaveCodec.TrafficDefinitionsWire(definitions), ref records);
        Assert.Equal(definitions.Count, records);
        records = 0;
        var readLinks = SaveCodec.ReadTrafficLinks(SaveCodec.TrafficLinksWire(links), ref records);
        Assert.Equal(links.Count + links.Sum(l => l.IncomingLinks.Count + l.Vehicles.Count + l.WaitingSources.Count), records);
        Assert.Equal(Json(definitions), Json(readDefinitions));
        Assert.Equal(Json(links), Json(readLinks));
        Assert.Equal(BitConverter.DoubleToInt64Bits(-3.125), BitConverter.DoubleToInt64Bits(readDefinitions[2].Signal!.PhaseOffsetMinutes));
    }

    [Fact]
    public void IdleLinksAndRepeatedValuesAreLeftOut()
    {
        var links = Enumerable.Range(0, 1000).Select(i => new TrafficLinkState { LinkId = i, NextDischargeAt = 3420.032815296203 }).ToList();
        string wire = SaveCodec.TrafficLinksWire(links);
        string bits = BitConverter.DoubleToInt64Bits(3420.032815296203).ToString("X16");
        Assert.Equal("1;,," + bits + string.Concat(Enumerable.Repeat(";", 999)), wire);
        var definitions = Enumerable.Range(0, 3).Select(i => new TrafficLinkDefinition { Id = i, Capacity = 1, FreeFlowMinutes = 1, HeadwayMinutes = 0.5 }).ToList();
        Assert.Equal("1;,,3FF0000000000000,3FE0000000000000;,,3FF0000000000000;,,3FF0000000000000", SaveCodec.TrafficDefinitionsWire(definitions));
        Assert.Equal("1", SaveCodec.TrafficLinksWire(new List<TrafficLinkState>()));
        int records = 0;
        Assert.Empty(SaveCodec.ReadTrafficLinks("1", ref records));
        Assert.Empty(SaveCodec.ReadTrafficDefinitions("1", ref records));
    }

    [Theory]
    [InlineData("2;,,3FF0000000000000,3FE0000000000000")]
    [InlineData("1;0,,3FF0000000000000,3FE0000000000000")]
    [InlineData("1;,1,3FF0000000000000,3FE0000000000000")]
    [InlineData("1;,,3ff0000000000000,3FE0000000000000")]
    [InlineData("1;,,3FF000000000000,3FE0000000000000")]
    [InlineData("1;,,3FF0000000000000")]
    [InlineData("1;,,3FF0000000000000,")]
    [InlineData("1;,,3FF0000000000000,3FE0000000000000;,,3FF0000000000000,3FE0000000000000")]
    [InlineData("1;,,3FF0000000000000,3FE0000000000000,3FF0000000000000")]
    [InlineData("1;")]
    [InlineData("1;,,3FF0000000000000,3FE0000000000000;")]
    [InlineData("1;,02,3FF0000000000000,3FE0000000000000")]
    public void NoncanonicalDefinitionsAreRefused(string wire)
    {
        int records = 0;
        Assert.Throws<SaveValidationException>(() => SaveCodec.ReadTrafficDefinitions(wire, ref records));
    }

    [Theory]
    [InlineData("1;")]
    [InlineData("1;0,,40AAB8000000000A")]
    [InlineData("1;,,40AAB8000000000A,")]
    [InlineData("1;,,40AAB8000000000A;,,40AAB8000000000A")]
    [InlineData("1;,0,40AAB8000000000A")]
    [InlineData("1;,,40AAB8000000000A,,1  2")]
    [InlineData("1;,,40AAB8000000000A,,,%61")]
    [InlineData("1;,,40AAB8000000000A,,,%e9")]
    [InlineData("1;,,40AAB8000000000A,,,c\"0")]
    [InlineData("1;,,40AAB8000000000A,,,,,,")]
    [InlineData("1;,,40AAB8000000000A,,,,,x,y")]
    public void NoncanonicalLinksAreRefused(string wire)
    {
        int records = 0;
        Assert.Throws<SaveValidationException>(() => SaveCodec.ReadTrafficLinks(wire, ref records));
    }

    [Fact]
    public void SavedCityReadsBackAndLegacyArrayShapeStillLoads()
    {
        var state = TrafficPersistenceTests.QueuedCity();
        string payload = SaveCodec.Write(state);
        var wire = JObject.Parse(payload);
        Assert.Equal(JTokenType.String, wire["Traffic"]!["Links"]!.Type);
        Assert.Equal(JTokenType.String, wire["Traffic"]!["Definitions"]!.Type);
        var read = SaveCodec.Read(payload);
        Assert.Equal(SaveReadStatus.Loaded, read.Status);
        Assert.Equal(payload, SaveCodec.Write(read.State!));
        var counts = SaveCodec.CountRecords(state, payload);
        Assert.Equal(counts.Semantic, counts.Streaming);

        var legacy = (JObject)wire.DeepClone();
        legacy["Traffic"]!["Links"] = JArray.FromObject(state.Traffic!.Links);
        legacy["Traffic"]!["Definitions"] = JArray.FromObject(state.Traffic.Definitions);
        string old = legacy.ToString(Formatting.None);
        var oldRead = SaveCodec.Read(old);
        Assert.Equal(SaveReadStatus.Loaded, oldRead.Status);
        Assert.Equal(payload, SaveCodec.Write(oldRead.State!));
        var oldCounts = SaveCodec.CountRecords(state, old);
        Assert.Equal(counts, oldCounts);
    }

    [Fact]
    public void IntegralNumbersWriteTheSameTextAsNewtonsoft()
    {
        var values = new[] { 0.0, 1, -1, 3856, 1440 * 365.0, 4320, -4096, 999999999999999, -999999999999999, 1e15, -1e15, 1e16, -0.0, 0.5,
            3420.032815296203, double.Epsilon, 9007199254740992.0, 123456789012345 };
        foreach (double value in values)
        {
            string expected = JsonConvert.ToString(value);
            if (SaveCodec.IntegralNumber(value)) Assert.Equal(expected, ((long)value).ToString(System.Globalization.CultureInfo.InvariantCulture) + ".0");
            else Assert.False(value == Math.Floor(value) && Math.Abs(value) < 1e15 && BitConverter.DoubleToInt64Bits(value) != BitConverter.DoubleToInt64Bits(-0.0));
        }
        var state = TrafficPersistenceTests.QueuedCity();
        state.Places.Add(new Place { Id = "spare", Name = "Spare", Kind = PlaceKind.Leisure, Capacity = 1, Position = new Position(-0.0, 1000000, 12.25) });
        string payload = SaveCodec.Write(state);
        Assert.Contains("\"X\":-0.0", payload);
        Assert.Contains("\"Y\":1000000.0", payload);
        Assert.Contains("\"Z\":12.25", payload);
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(payload).Status);
    }

    [Fact]
    public void CapturedMonoSpellingsNormalizeWithoutChangingValues()
    {
        static string Shortest(string mono) => double.Parse(mono, System.Globalization.CultureInfo.InvariantCulture).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(Shortest("69.383535500870565"), CapturedPayload.Normalize("69.383535500870565"));
        Assert.NotEqual("69.383535500870565", Shortest("69.383535500870565"));
        Assert.Equal("[3856.0,0.6000000167127065,12.5]", CapturedPayload.Normalize("[3856.0,0.6000000167127065,12.5]"));
        Assert.Equal("\"1|3900|" + Shortest("3528.8271746817823") + "|x\"", CapturedPayload.Normalize("\"1|3900|3528.8271746817823|x\""));
        var state = TrafficPersistenceTests.QueuedCity();
        string payload = SaveCodec.Write(state);
        Assert.Equal(payload, CapturedPayload.Normalize(payload));
        string wrapped = new Newtonsoft.Json.Linq.JObject { ["values"] = new Newtonsoft.Json.Linq.JObject { [SaveCodec.StateKey] = payload } }.ToString();
        Assert.Equal(payload, CapturedPayload.Extract(wrapped));
    }
}
