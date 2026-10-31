using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;

static partial class Program
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void PreserveB0(string source, CityState replacement)
    {
        var data = new Dictionary<string, string> { [SaveStore.StateKey] = source };
        Check(SaveCodec.Read(source).Status == SaveReadStatus.Invalid, "Expected source refusal.");
        Check(!SaveStore.WriteTo(data, replacement).Success, "Store accepted refused source.");
        Check(!SaveSession.Open(data, replacement.PopulationKey).WriteTo(data, replacement).Success, "Session accepted refused source.");
        Check(ReferenceEquals(source, data[SaveStore.StateKey]), "Refusal replaced source text/reference.");
    }
    private static void B0ContractChecks()
    {
        var city = FoodCity(3, true); FullMemoryAndTable(city);
        string json = SaveCodec.Write(city); Check(SaveCodec.Read(json).Status == SaveReadStatus.Loaded, "Full table/memory round trip.");
        var last = city.Food!.BusinessReferences.Last(); string id = last.BusinessId;
        last.BusinessId += "X";
        bool refused = false; try { SaveCodec.Write(city); } catch (SaveValidationException) { refused = true; }
        Check(refused, "One-character-over business table was written.");
        last.BusinessId = id;
        PreserveB0(json.Replace(JsonConvert.ToString(id), JsonConvert.ToString(id + "X")), city);
        string unicode = id.Substring(0, id.Length - 1) + "é";
        last.BusinessId = unicode; refused = false;
        try { SaveCodec.Write(city); } catch (SaveValidationException) { refused = true; }
        Check(refused, "One UTF-8 byte over business table was written."); last.BusinessId = id;
        PreserveB0(json.Replace(JsonConvert.ToString(id), JsonConvert.ToString(unicode)), city);
        var maximum = new ConsumerMemory { Watermark = new ExperienceWatermark { Day = MemoryWire.MaximumDay, Meal = MealKind.Dinner, Context = 4, Stage = ExperienceStage.Served },
            Entries = { new ShopMemory { BusinessRef = 19999, Day = MemoryWire.MaximumDay, Served = 35, Affinity = 8, Outcome = ShopExperience.Served } } };
        Check(MemoryWire.Encode(MemoryWire.Decode(MemoryWire.Encode(maximum))) == MemoryWire.Encode(maximum), "Memory maximum/canonical encoding.");
        Check(MemoryWire.Number(19999, 3) == "FFJ", "Independent base36 slot vector.");
    }

    private static object RecordBoundary()
    {
        var state = FoodCity(50000, true, 3); PopulateDecisions(state); AddReferencePlaces(state); RetainVisits(state, 64);
        // 300,000 consumer tuple slots + 104 captured decision records + 3 menu strings + 1 B header.
        int remaining = SaveCodec.MaxEmbeddedRecords - 300108 - 50000 * 64;
        foreach (var place in state.Places)
            for (int i = 0, n = Math.Min(40, remaining); i < n; i++, remaining--)
                place.WeeklyHours.Add(new OpeningWindow { Day = 0, FromMinute = 1, ToMinute = 2 });
        Check(remaining == 0, "Record fixture construction.");
        string json = SaveCodec.Write(state); var counts = SaveCodec.CountRecords(state, json);
        Check(counts.Streaming == 4000000 && counts.Semantic == 3999997, "Exact-cap counters disagree.");
        Check(SaveCodec.Read(json).Status == SaveReadStatus.Loaded, "Exact record cap did not load.");
        var last = state.Places.Last(p => p.WeeklyHours.Count > 0); last.WeeklyHours.Add(new OpeningWindow { Day = 0, FromMinute = 1, ToMinute = 2 });
        bool refused = false; try { SaveCodec.Write(state); } catch (SaveValidationException) { refused = true; }
        Check(refused, "One-record-over write accepted."); last.WeeklyHours.RemoveAt(last.WeeklyHours.Count - 1);
        int at = json.LastIndexOf("\"WeeklyHours\":[{", StringComparison.Ordinal);
        int end = json.IndexOf(']', at);
        string over = json.Insert(end, "," + JsonConvert.SerializeObject(new OpeningWindow { Day = 0, FromMinute = 1, ToMinute = 2 }));
        var replacement = FoodCity(1, false);
        PreserveB0(over, replacement);
        return new { streamingAtCap = counts.Streaming, semanticAtCap = counts.Semantic, oneRecordOver = "read/write/store/session refused and source reference preserved", chars = json.Length, utf8Bytes = System.Text.Encoding.UTF8.GetByteCount(json) };
    }

    private static object BaseRecovery()
    {
        var state = FoodCity(50000, true, 3); PopulateDecisions(state);
        string before = Legacy17(state), after = SaveCodec.Write(state);
        int recovered = before.Length - after.Length;
        Check(recovered >= 100 * 1024 * 1024, "Base recovery below 100 Mi-characters.");
        return new { residents = 50000, representation = 3, beforeChars = before.Length, afterChars = after.Length, recoveredChars = recovered,
            beforeUtf8 = System.Text.Encoding.UTF8.GetByteCount(before), afterUtf8 = System.Text.Encoding.UTF8.GetByteCount(after),
            streamingAndSemantic = SaveCodec.CountRecords(state, after), bInactive = !state.Capabilities.Active, memoriesEmpty = state.Food!.Residents.All(m => m.Memory.Entries.Count == 0) };
    }

    private static object InputBoundary()
    {
        var state = City(1); string small = SaveCodec.Write(state);
        string exact = small.PadRight(SaveCodec.MaxInputCharacters);
        Check(SaveCodec.Read(exact).Status == SaveReadStatus.Loaded, "Exact character/UTF-8 input cap.");
        exact = null!;
        string over = small.PadRight(SaveCodec.MaxInputCharacters + 1); PreserveB0(over, state); over = null!;
        state.Places[0].Name = "é";
        string unicode = SaveCodec.Write(state).PadRight(SaveCodec.MaxInputCharacters);
        Check(System.Text.Encoding.UTF8.GetByteCount(unicode) == SaveCodec.MaxInputCharacters + 1, "UTF-8 fixture construction.");
        PreserveB0(unicode, state); unicode = null!;
        string legacy = Legacy17(state);
        for (int schema = 13; schema <= 17; schema++)
        {
            var token = JObject.Parse(legacy); token["Schema"] = schema;
            foreach (JObject citizen in token["Citizens"]!)
            {
                if (schema < 17) citizen.Remove("Traits");
                if (schema < 16) citizen.Remove("OptionalStopChoice");
                if (schema < 15) citizen.Remove("OptionalStop");
            }
            string source = token.ToString(Formatting.None);
            Check(SaveCodec.Read(source).Status == SaveReadStatus.Loaded, "Invalid undersized source " + schema);
            PreserveB0(source.PadRight(SaveCodec.MaxInputCharacters + 1), state);
        }
        return new { exactCharactersAndUtf8 = SaveCodec.MaxInputCharacters, oneCharacterOver = "read/store/session preserved", oneUtf8ByteOver = "read/store/session preserved", oversizedSources = new[] {13,14,15,16,17} };
    }
}
