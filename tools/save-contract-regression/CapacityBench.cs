using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using PersistentCity.Core;
using Activity = PersistentCity.Core.Activity;
using PersistentCity.Persistence;

static partial class Program
{
    private static int SchemaVersion => CityState.CurrentSchema;
    // Compiles unchanged against the archived A3 source. Reflection here is fixture infrastructure,
    // never production simulation. Run each case in a fresh process for process-peak memory.
    private static string Legacy17(CityState state)
    {
        if (SchemaVersion < 18) return SaveCodec.Write(state);
        int schema = state.Schema;
        try
        {
            state.Schema = 17;
            return (string)typeof(SaveCodec).Assembly.GetType("PersistentCity.Persistence.LegacyPopulationCodec")!.GetMethod("Write")!.Invoke(null, new object[] { state })!;
        }
        catch (TargetInvocationException e) { throw e.InnerException!; }
        finally { state.Schema = schema; }
    }
    private static object? Counts(CityState state, string json)
        => typeof(SaveCodec).GetMethod("CountRecords")?.Invoke(null, new object[] { state, json });

    private static void RetainVisits(CityState state, int visits)
    {
        for (int i = 0; i < state.Citizens.Count; i++)
        {
            var citizen = state.Citizens[i]; citizen.RecentVisits.Clear();
            for (int j = 0; j < visits; j++) citizen.RecentVisits.Add(new VisitRecord {
                AtMinute = j * 17.125 + 0.123456789, PlaceId = j % 2 == 0 ? citizen.HomeId : "shop-1", Activity = j % 2 == 0 ? Activity.Home : Activity.Shop });
        }
    }
    private static void AddReferencePlaces(CityState state)
    {
        for (int i = state.Places.Count; i < 20000; i++) state.Places.Add(new Place {
            Id = "address:ba%3Astreet_thirdstreet:" + i + ":shop", Name = "Historical shop " + i,
            District = "a", Kind = PlaceKind.Shop, Position = new Position(i, 0, 0) });
    }
    private static void FullMemoryAndTable(CityState state)
    {
        var referenceProperty = typeof(FoodCommerceState).GetProperty("BusinessReferences");
        var memoryProperty = typeof(ResidentMeal).GetProperty("Memory");
        if (referenceProperty == null || memoryProperty == null) return;
        var refs = (IList)referenceProperty.GetValue(state.Food)!;
        var refType = referenceProperty.PropertyType.GetGenericArguments()[0];
        string[] ids = Enumerable.Range(0, 20000).Select(i => "business:fixture:" + i.ToString("D5") + ":created:17:food:player:generation:1").ToArray();
        // Actual escaped ASCII table size: [ [1,slot,"complete identity","PPP"], ... ].
        int size = 1 + Enumerable.Range(0, 20000).Sum(i => 1 + ("[1," + i + ",\"" + ids[i] + "\",\"000\"]").Length);
        int padding = 2 * 1024 * 1024 - size;
        if (padding < 0) throw new Exception("Fixture reference identities already exceed their budget.");
        for (int i = 0; i < 20000; i++)
        {
            int extra = padding / (20000 - i); padding -= extra;
            ids[i] += new string('X', extra);
            string place = state.Places[i % state.Places.Count].Kind == PlaceKind.Shop ? state.Places[i % state.Places.Count].Id : "shop-1";
            refs.Add(JsonConvert.DeserializeObject(JsonConvert.SerializeObject(new { Slot = i, BusinessId = ids[i], PlaceId = place }), refType)!);
        }
        for (int i = 0; i < state.Food!.Residents.Count; i++)
        {
            int first = (i % 5000) * 4;
            var entries = Enumerable.Range(first, 4).Select(slot => new { BusinessRef = slot, Day = 0, Served = 35, Affinity = 17, Outcome = 1 });
            object memory = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(new { Watermark = new { Day = 0, Meal = 3, Context = 4, Stage = 4 }, Entries = entries }), memoryProperty.PropertyType)!;
            memoryProperty.SetValue(state.Food.Residents[i], memory);
        }
    }

    private static object CapacityBench(string name, int runs)
    {
        bool cf = name.StartsWith("CF", StringComparison.Ordinal);
        int residents = name == "reference" ? 957 : name == "5000" ? 5000 : 50000;
        int representation = name == "ceiling2" || name == "large2" ? 2 : residents < 50000 ? 2 : 3;
        int visits = name == "ceiling" || name == "ceiling2" ? 0 : name == "CF64" ? 64 : 32;
        var state = FoodCity(residents, true, representation); PopulateDecisions(state);
        RetainVisits(state, visits);
        if (cf) AddReferencePlaces(state);
        if (SchemaVersion >= 18) FullMemoryAndTable(state);
        // B full memory at all sizes uses all 20,000 refs, backed by existing places. AddReferencePlaces
        // is only CF's extra-existing-state allowance, not necessary for a reference table's identity cap.
        string? json = null, refused = null;
        try { json = SaveCodec.Write(state); }
        catch (SaveValidationException e) { refused = e.Message; }
        if (json == null)
        {
            // Report exact over-cap length without pretending it was a valid Write/migration source.
            object context = typeof(SaveCodec).GetNestedType("WireContext", BindingFlags.NonPublic)?.GetMethod("FromState")?.Invoke(null, new object[] { state })!;
            var method = typeof(SaveCodec).GetMethod("Settings", BindingFlags.NonPublic | BindingFlags.Static)!;
            var settings = (JsonSerializerSettings)method.Invoke(null, method.GetParameters().Length == 0 ? null : new[] { context })!;
            json = JsonConvert.SerializeObject(state, Formatting.None, settings);
            var rejectedCounts = Counts(state, json);
            var rejectedPair = rejectedCounts == null ? (0, 0) : ((int, int))rejectedCounts;
            return new { name, schema = SchemaVersion, residents, representation, visits, refused, chars = json.Length,
                streamingRecords = rejectedPair.Item1, semanticRecords = rejectedPair.Item2,
                utf8Bytes = Encoding.UTF8.GetByteCount(json), processPeakMiB = Process.GetCurrentProcess().PeakWorkingSet64 / 1048576.0 };
        }
        var counts = Counts(state, json);
        string? old = null, migrationRefusal = null;
        // Table and memory are deliberately absent from the original A3 source; they initialize empty.
        try { old = Legacy17(state); }
        catch (SaveValidationException e) { migrationRefusal = e.Message; }
        catch (InvalidOperationException e) { migrationRefusal = e.Message; }
        double write = Time(runs, () => SaveCodec.Write(state));
        double read = Time(runs, () => { var result = SaveCodec.Read(json); if (result.Status != SaveReadStatus.Loaded) throw new Exception(result.Error); });
        double? migration = old == null ? null : Time(runs, () => { var result = SaveCodec.Read(old); if (result.Status != SaveReadStatus.Loaded) throw new Exception(result.Error); });
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long before = GC.GetTotalMemory(true), allocated = GC.GetAllocatedBytesForCurrentThread();
        var retained = SaveCodec.Read(json).State!;
        long retainedBytes = GC.GetTotalMemory(true) - before, readAllocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        GC.KeepAlive(retained);
        var recordPair = counts == null ? (0, 0) : ((int, int))counts;
        // Future contracts reserve bytes/records in this manifest, never invented DTOs in schema 18.
        const int futureChars = 26694720; // full plan envelope less B memory's 9.6M and B table's 2Mi
        int memoryHeadroom = state.Food!.Residents.Count * 13; // 51 actual incl. slot overhead -> 64 allocation
        const int futureRecords = 191864; // every non-B row of CF64's published ledger (held image inclusive)
        int heldExistingRecords = 8 * (1 + 3 + 7 + 2); // actual image: shops/offers/windows/two single-leg routes
        int reservedRecords = futureRecords - heldExistingRecords - 1; // B0 already charges its capability header
        int bHeaderChars = ("\"Capabilities\":" + JsonConvert.SerializeObject(new { Active = false, Code = "B", Revision = 1 }) + ",").Length;
        int reservedChars = futureChars - bHeaderChars; // aggregate policy/header row already contains B0's header
        if (cf && SchemaVersion >= 18)
        {
            Check(json.Length + reservedChars + memoryHeadroom <= SaveCodec.MaxInputCharacters, "CF character budget.");
            Check(Encoding.UTF8.GetByteCount(json) + reservedChars + memoryHeadroom <= SaveCodec.MaxInputCharacters, "CF UTF-8 budget.");
            Check(Math.Max(recordPair.Item1, recordPair.Item2) + reservedRecords <= SaveCodec.MaxEmbeddedRecords, "CF record budget.");
            if (name == "CF32") Check(json.Length + reservedChars + memoryHeadroom <= SaveCodec.MaxInputCharacters * .85, "CF32 85% headroom.");
        }
        return new {
            name, schema = SchemaVersion, fixtureSeed = 11, cityMinute = state.Minute,
            visitTimeFormula = "j*17.125+0.123456789; j=0..visits-1; even=assigned home/Home, odd=shop-1/Shop",
            identityPolicy = "EconomyScenario.Create; donors <=5000, 1000 donors plus stable pc: housing expansion at 50k",
            referenceIdentityPolicy = "business:fixture:00000..19999:created:17:food:player:generation:1 plus bounded X padding to exactly 2Mi table",
            payloadSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(json))),
            residents, representation, consumers = state.Food.Residents.Count,
            visitsPerResident = visits, visits = residents * visits, placeCount = state.Places.Count,
            extraExistingPlaces = cf ? 19991 : 0,
            extraExistingPlaceChars = cf ? 6343907 : 0,
            existingGrowthAllowanceChars = cf ? 12582912 : 0,
            memoryEntries = SchemaVersion >= 18 ? state.Food.Residents.Count * 4 : 0,
            businessReferences = SchemaVersion >= 18 ? 20000 : 0,
            tableCharsAndUtf8 = SchemaVersion >= 18 ? 2097152 : 0,
            chars = json.Length, utf8Bytes = Encoding.UTF8.GetByteCount(json), streamingRecords = recordPair.Item1, semanticRecords = recordPair.Item2,
            runs, writeMs = write, readMs = read, migrationFrom17Ms = migration, migrationSourceChars = old?.Length, migrationRefusal,
            retainedBytes, readAllocatedBytes = readAllocated, processPeakBytes = Process.GetCurrentProcess().PeakWorkingSet64,
            syntheticReservation = cf && SchemaVersion >= 18 ? new {
                futureContractChars = reservedChars, alreadyCountedBHeaderChars = bHeaderChars, fixedMemoryHeadroomChars = memoryHeadroom,
                futureContractRecords = reservedRecords,
                totalBudgetedChars = json.Length + reservedChars + memoryHeadroom,
                totalBudgetedUtf8Bytes = Encoding.UTF8.GetByteCount(json) + reservedChars + memoryHeadroom,
                totalBudgetedRecords = Math.Max(recordPair.Item1, recordPair.Item2) + reservedRecords
            } : null
        };
    }
}
