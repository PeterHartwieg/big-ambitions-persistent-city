using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

[Collection(StreetFoodHooks.Collection)]
public sealed class F6dHardCapTests
{
    private readonly ITestOutputHelper output;
    public F6dHardCapTests(ITestOutputHelper output) { this.output = output; }
    private static string Unchecked(CityState state)
    {
        var flags = BindingFlags.NonPublic | BindingFlags.Static;
        var context = typeof(SaveCodec).GetNestedType("WireContext", BindingFlags.NonPublic)!.GetMethod("FromState")!.Invoke(null, new object[] { state });
        var settings = (JsonSerializerSettings)typeof(SaveCodec).GetMethod("Settings", flags)!.Invoke(null, new object[] { context! })!;
        return JsonConvert.SerializeObject(state, Formatting.None, settings);
    }
    private static void WriteKept(CityState state, string prior)
    {
        var data = new Dictionary<string, string> { [SaveStore.StateKey] = prior, ["other"] = "kept" };
        Assert.False(SaveStore.WriteTo(data, state).Success);
        Assert.False(SaveSession.Open(data, state.PopulationKey).WriteTo(data, state).Success);
        Assert.Same(prior, data[SaveStore.StateKey]); Assert.Equal("kept", data["other"]);
    }
    [Fact]
    public void CurrentLedgerAtGlobalRecordAndCharacterCapsAndOneOver()
    {
        string? path = PerfGate.Variable("PC_F6D_CAPS"); if (path == null) return;
        var state = F6dMeasure.Build(50000, 3, 64, true); F6dMeasure.FillLedger(state);
        state.MenuPolicy = FoodNameIdentity.Wire; state.Food!.Residents[0].Decision = null;
        state.PopulationKey = CurrentSchemaOnlyTests.City().PopulationKey;
        state.PrepareForSave();
        // Freeze the fixture's coherent state: synthetic journeys below must not be replaced by a live scheduler.
        state.SnapshotSynchronizer = null;
        string baseline = SaveCodec.Write(state);
        var counts = SaveCodec.CountRecords(state, baseline);
        int remaining = SaveCodec.MaxEmbeddedRecords - Math.Max(counts.Streaming, counts.Semantic);
        var routes = new List<(CitizenState Person, Route Route)>();
        foreach (var p in state.Citizens)
        {
            if (remaining == 0) break;
            var position = state.Places.First(x => x.Id == p.PlaceId).Position;
            var route = new Route(); int count = Math.Min(remaining, SaveCodec.MaxRouteLegs - 1);
            for (int i = 0; i < count; i++) route.Legs.Add(new RouteLeg { From = position, To = position, Minutes = 1, Mode = TravelMode.Walk });
            p.Phase = CitizenPhase.Travelling; p.DestinationId = p.PlaceId; p.DestinationActivity = p.Activity;
            p.Journey = route; p.DepartureMinute = state.Minute; p.NextEventMinute = state.Minute + count;
            routes.Add((p, route)); remaining -= count;
        }
        Assert.Equal(0, remaining);
        string atRecords = SaveCodec.Write(state); counts = SaveCodec.CountRecords(state, atRecords);
        Assert.Equal(SaveCodec.MaxEmbeddedRecords, Math.Max(counts.Streaming, counts.Semantic));
        var read = SaveWire.Read(atRecords); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(atRecords, SaveCodec.Write(read.State!)); read = null!;
        var (person, last) = routes[^1];
        var extra = new RouteLeg { From = last.Legs[^1].To, To = last.Legs[^1].To, Minutes = 1, Mode = TravelMode.Walk };
        last.Legs.Add(extra); person.NextEventMinute++;
        Assert.Throws<SaveValidationException>(() => SaveCodec.Write(state));
        WriteKept(state, atRecords);
        EconomicLedgerTests.Refused(Unchecked(state));
        last.Legs.RemoveAt(last.Legs.Count - 1); person.NextEventMinute--;
        // Fill only existing scalar strings; embedded-record count stays exactly at the cap.
        int padding = SaveCodec.MaxInputCharacters - atRecords.Length;
        foreach (var (_, route) in routes)
            foreach (var leg in route.Legs)
            {
                int n = Math.Min(256, padding); leg.ServiceId = new string('x', n); padding -= n;
                if (padding == 0) break;
            }
        // Spend any remaining space in owned-resident names, which never alias the imported source table.
        foreach (var p in state.Citizens.Where(p => p.Identity.Id.StartsWith("pc:", StringComparison.Ordinal)))
        {
            int n = Math.Min(512 - p.Identity.Name.Length, padding); p.Identity.Name += new string('x', n); padding -= n;
            if (padding == 0) break;
        }
        Assert.Equal(0, padding);
        string atCharacters = SaveCodec.Write(state);
        Assert.Equal(SaveCodec.MaxInputCharacters, atCharacters.Length);
        Assert.Equal(SaveCodec.MaxInputCharacters, System.Text.Encoding.UTF8.GetByteCount(atCharacters));
        read = SaveWire.Read(atCharacters); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(atCharacters, SaveCodec.Write(read.State!)); read = null!;
        var growName = state.Citizens.First(p => p.Identity.Id.StartsWith("pc:", StringComparison.Ordinal) && p.Identity.Name.Length < 512);
        growName.Identity.Name += "x";
        Assert.Throws<SaveValidationException>(() => SaveCodec.Write(state)); WriteKept(state, atCharacters);
        EconomicLedgerTests.Refused(atCharacters + " ");
        // Same character count but one more UTF-8 byte also refuses.
        growName.Identity.Name = growName.Identity.Name[..^1];
        var nonAscii = routes[0].Route.Legs[0]; nonAscii.ServiceId = "é" + nonAscii.ServiceId[1..];
        Assert.Throws<SaveValidationException>(() => SaveCodec.Write(state)); WriteKept(state, atCharacters);
        string wide = Unchecked(state); Assert.Equal(atCharacters.Length, wide.Length);
        EconomicLedgerTests.Refused(wide);
        string result = $"records={counts.Streaming}/{counts.Semantic}; characters/UTF8={atCharacters.Length}; one-record/one-character/one-UTF8-byte over: read/write/store/session refused, originals kept";
        File.AppendAllText(path, result + Environment.NewLine); output.WriteLine(result);
    }
}
