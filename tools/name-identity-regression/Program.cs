using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;

static class NameIdentityProgram
{
    static void Main(string[] args) => Program.Name25Main(args);
}
static partial class Program
{
    public static void Name25Main(string[] args)
    {
        string name = args.Length == 0 ? "957x2" : args[0];
        int samples = args.Length < 2 ? 3 : int.Parse(args[1]);
        if (name == "encoding") { NameEncoding25(); return; }
        bool cf = name.StartsWith("CF", StringComparison.Ordinal);
        int residents = cf || name.StartsWith("50000", StringComparison.Ordinal) ? 50000 : name.StartsWith("5000", StringComparison.Ordinal) ? 5000 : 957;
        int factor = cf || name.EndsWith("x3", StringComparison.Ordinal) ? 3 : 2;
        var state = FoodCity(residents, true, factor); PopulateDecisions(state);
        RetainVisits(state, name == "CF64" ? 64 : 32);
        if (cf) AddReferencePlaces(state);
        FullMemoryAndTable(state); PopulateWeather20(state);
        var products = new[] { FoodProducts.Croissant, FoodProducts.Burger, FoodProducts.HotDog,
            "ba:itemname_donut", "ba:itemname_cupcake", "ba:itemname_salad",
            "foodtrucks:itemname_dumplings", "foodtrucks:itemname_choppedcheese" };
        // Complete eight-cell/day history allocation, with two actual mod namespaces as opaque
        // predecessor history. Conditional offline facts only; no loaded definition or native sale.
        var places = state.Places.Where(p => p.Kind == PlaceKind.Shop).ToList();
        for (int i = places.Count; i < 1024; i++)
        {
            var place = new Place { Id = "name25:shop:" + i, Name = "Historical shop", Kind = PlaceKind.Shop };
            state.Places.Add(place); places.Add(place);
        }
        int generationsPerDay = name == "churn" ? 2048 : 1024;
        for (int day = 0; day < 14; day++) for (int i = 0; i < generationsPerDay; i++)
        {
            var row = new FoodShopDay { Day = day, PlaceId = places[i % 1024].Id, BusinessId = "name25:history:" + i,
                Purchases = 8, Revenue = Money.FromCents(8), PurchaseMeals = new long[] { 8,0,0,0 },
                BuyerDistricts = { new FoodBuyerDistrict { DistrictId = "a", Purchases = 8 } } };
            row.PurchaseHours[0] = 8;
            foreach (string product in products) row.ProductSales.Add(new FoodProductSales { ProductId = product,
                Purchases = 1, Revenue = Money.FromCents(1), PurchaseMeals = new long[] { 1,0,0,0 } });
            state.Food!.History.Add(row);
        }
        // FoodCity's clock is day 1. Move the fixture coherently to day 14 without processing
        // purchases: all relative schedule fields retain their original differences.
        const double shift = 13 * 1440;
        Shift25(state, shift);
        string[] names = products.Concat(Enumerable.Range(8, 248).Select(i => "name25:product:" + i.ToString("D3"))).ToArray();
        state.Food!.Shops.Clear();
        for (int i = 0; i < 32; i++) state.Food.Shops.Add(new FoodScope { PlaceId = places[i].Id,
            BusinessId = "name25:scope:" + i, ProductId = names[i * 8], AdditionalProductIds = names.Skip(i * 8 + 1).Take(7).ToList() });
        state.Schema = 24; state.MenuPolicy = FoodMenuPolicy.Wire;
        string before = PopulationSchema24.Write(state);
        var beforeCounts = PopulationSchema24.CountRecordsForSchema(state, before, 24);
        var baseline = Measure25(samples, () => PopulationSchema24.Write(state), () => PopulationSchema24.Read(before),
            () => PopulationSchema24.ValidateForActivation(state), () => PopulationSchema24.RequireExecutableCapabilities(before));
        long baselineRetained = Retained25(() => PopulationSchema24.Read(before));
        long migrationStart = Stopwatch.GetTimestamp();
        var migrated = SaveCodec.Read(before); Check(migrated.Status == SaveReadStatus.Loaded, migrated.Error);
        double migrationMs = Stopwatch.GetElapsedTime(migrationStart).TotalMilliseconds;
        var upgraded = migrated.State!;
        upgraded.Schema = 24; upgraded.MenuPolicy = FoodMenuPolicy.Wire;
        Check(PopulationSchema24.Write(upgraded) == before, "24 -> 25 changed predecessor identity/money/history/terms.");
        upgraded.Schema = 25; upgraded.MenuPolicy = FoodNameIdentity.Wire;
        state = upgraded;
        string after = SaveCodec.Write(state);
        var afterCounts = SaveCodec.CountRecords(state, after);
        Check(state.ProductNames.Count == 256, "Expected complete 256-name table.");
        Check(PopulationSchema24.Read(after).Status == SaveReadStatus.Future, "Old executable accepted 25.");
        var candidate = Measure25(samples, () => SaveCodec.Write(state), () => SaveCodec.Read(after),
            () => SaveCodec.ValidateForActivation(state), () => SaveCodec.RequireExecutableCapabilities(after));
        long candidateRetained = Retained25(() => SaveCodec.Read(after));
        // Preserve the established B-E synthetic reserves, then add E5d's entire conservative
        // allocation without reclaiming the earlier header reserve. Actual DTO cost is separate.
        int memoryHeadroom = state.Food!.Residents.Count * 13;
        int reservedChars = 26694720 - ("\"Capabilities\":" + JsonConvert.SerializeObject(new { Active = false, Code = "B", Revision = 1 }) + ",").Length
            - (205 + 8 * 25) - AwarenessChars(state) - RivalChars(state) - WeatherChars20(state)
            - CampaignChars22(state) - ("\"MenuPolicy\":" + JsonConvert.ToString(FoodMenuPolicy.Wire) + ",").Length - OutflowChars24(state)
            - HistoryReserveUsed25(state);
        int reservedRecords = 191864 - 8 * (1 + 3 + 7 + 2) - 1 - AwarenessRecords(state) - WeatherRecords20(state)
            - CampaignRecords22(state) - OutflowRecords24(state) - state.Food.History.Sum(r => 1 + r.ProductSales.Count);
        // The history byte/cell reservation has been materialized above; do not double-charge it.
        long totalChars = after.Length + reservedChars + memoryHeadroom + 540672;
        long totalBytes = Encoding.UTF8.GetByteCount(after) + reservedChars + memoryHeadroom + 540672;
        int totalRecords = Math.Max(afterCounts.Streaming, afterCounts.Semantic) + reservedRecords + 320;
        if (cf)
        {
            Check(totalChars <= SaveCodec.MaxInputCharacters && totalBytes <= SaveCodec.MaxInputCharacters, "Complete CF byte/character composition failed.");
            Check(totalRecords <= SaveCodec.MaxEmbeddedRecords, "Complete CF record composition failed.");
            if (name == "CF32") Check(totalChars <= SaveCodec.MaxInputCharacters * .85, "CF32 85% composition failed.");
        }
        Console.WriteLine(JsonConvert.SerializeObject(new { name, residents, factor, samples, seed = state.Seed,
            fixture = "32/64 retained visits; full 20k business references/four memories; 14x1024x8 history; 256 names; 8x3 legacy held offers",
            before = new { chars = before.Length, bytes = Encoding.UTF8.GetByteCount(before), beforeCounts.Streaming, beforeCounts.Semantic, retainedBytes = baselineRetained, timing = baseline },
            after = new { chars = after.Length, bytes = Encoding.UTF8.GetByteCount(after), afterCounts.Streaming, afterCounts.Semantic, retainedBytes = candidateRetained, timing = candidate },
            migrationMs, semanticComparison = "every schema-24 field identical", tableCount = state.ProductNames.Count,
            rows = state.Food.History.Count, cells = state.Food.History.Sum(r => r.ProductSales.Count),
            composition = cf ? new { reservedChars, reservedRecords, memoryHeadroom, e5dConservativeChars = 540672, e5dRecords = 320, totalChars, totalBytes, totalRecords } : null,
            processPeakBytes = Process.GetCurrentProcess().PeakWorkingSet64 }));
    }
    static int HistoryReserveUsed25(CityState state)
    {
        // Materialize E4's 114,688-cell reservation once. Row text remains actual
        // additional existing state; no invented per-row character reserve is reclaimed.
        return 114688 * 128;
    }
    static void Shift25(CityState state, double shift)
    {
        state.Minute += shift;
        foreach (var c in state.Citizens) { c.NextEventMinute += shift; c.ActivityUntilMinute += shift; if (c.W != null) c.W = new WeatherCommitment(c.W.Boundary + shift, c.W.Weather, c.W.Value != 0); }
        foreach (var m in state.Food!.Residents)
        {
            m.Day += 13; m.DesiredAt += shift; m.ExpiresAt += shift; m.DueAt += shift;
            if (m.Budget != null) { if (m.Budget.LastPostedDay >= 0) m.Budget.LastPostedDay += 13; if (m.Budget.HomeFoodFundedDay >= 0) m.Budget.HomeFoodFundedDay += 13; m.Budget.NextPostingMinute += shift; }
            if (m.Today != null) { m.Today.Day += 13; foreach (var r in m.Today.Meals) { r.At += (int)shift; r.CheckoutAt += (int)shift; } }
            if (m.Decision != null)
            {
                m.Decision.IntentId = m.IntentId; m.Decision.OriginAt += shift; m.Decision.Deadline += shift;
                foreach (var s in m.Decision.Shops) s.NextServiceAt += shift;
            }
        }
    }
    static object Measure25(int samples, Func<string> write, Func<SaveReadResult> read, Action activation, Action early)
    {
        object Run(Action operation)
        {
            var times = new List<double>(); long allocation = 0;
            operation();
            for (int i = 0; i < samples; i++)
            {
                long bytes = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp(); operation();
                times.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds); allocation += GC.GetAllocatedBytesForCurrentThread() - bytes;
            }
            times.Sort();
            return new { p50 = times[times.Count / 2], p95 = times[Math.Min(times.Count - 1, (int)(times.Count * .95))], max = times.Last(),
                spikes30 = times.Count(t => t >= 30), allocatedBytes = allocation / samples };
        }
        return new { write = Run(() => write()), read = Run(() => { var r = read(); Check(r.Status == SaveReadStatus.Loaded, r.Error); }),
            activation = Run(activation), early = Run(early) };
    }
    static long Retained25(Func<SaveReadResult> read)
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); long before = GC.GetTotalMemory(true);
        var result = read(); Check(result.Status == SaveReadStatus.Loaded, result.Error);
        long retained = GC.GetTotalMemory(true) - before; GC.KeepAlive(result); return retained;
    }
    static void NameEncoding25()
    {
        var state = FoodCity(1, false); state.Food!.Shops.Clear();
        foreach (var m in state.Food.Residents) { m.PreferredProductId = ""; m.PreferenceBonusCents = 0; m.Today = null; }
        for (int i = 0; i < 32; i++)
        {
            string place = "name25:" + i; state.Places.Add(new Place { Id = place, Name = "Shop", Kind = PlaceKind.Shop });
            string Name(int n) => new string('\ue001', 253) + n.ToString("D3");
            state.Food.Shops.Add(new FoodScope { PlaceId = place, BusinessId = "encoding:" + i, ProductId = Name(i * 8),
                AdditionalProductIds = Enumerable.Range(i * 8 + 1, 7).Select(Name).ToList() });
        }
        string wire = SaveCodec.Write(state);
        var escaped = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(wire)!.ToString(Formatting.None,
            new JsonConverter[0]);
        // JSON string escaping mode has no effect on identity; exercise the true escaped source too.
        var settings = new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii };
        escaped = JsonConvert.SerializeObject(Newtonsoft.Json.Linq.JObject.Parse(wire), Formatting.None, settings);
        Check(SaveCodec.Read(escaped).Status == SaveReadStatus.Loaded, "Maximum escaped names refused.");
        Check(SaveCodec.Write(SaveCodec.Read(escaped).State!) == wire, "Escaping changed canonical identity.");
        string table = JsonConvert.SerializeObject(state.ProductNames, Formatting.None, settings);
        Console.WriteLine(JsonConvert.SerializeObject(new { names = state.ProductNames.Count, nameLength = 256, chars = wire.Length,
            bytes = Encoding.UTF8.GetByteCount(wire), escapedChars = escaped.Length, escapedBytes = Encoding.UTF8.GetByteCount(escaped),
            escapedTableChars = table.Length + 16, escapedTableBytes = Encoding.UTF8.GetByteCount(table) + 16, timing = Measure25(7, () => SaveCodec.Write(state), () => SaveCodec.Read(escaped), () => SaveCodec.ValidateForActivation(state), () => SaveCodec.RequireExecutableCapabilities(escaped)), counts = SaveCodec.CountRecords(state, escaped) }));
    }
}
