using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
#if NAME_SCHEMA24
using NameBaselineCodec = PersistentCity.Persistence.SaveCodec;
#else
using NameBaselineCodec = PersistentCity.Persistence.PopulationSchema24;
#endif

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
        #if !NAME_SCHEMA24
        if (name == "encoding" || name == "encoding-max") { NameEncoding25(name == "encoding-max"); return; }
        if (name == "migration-record-limit") { Console.WriteLine(JsonConvert.SerializeObject(MigrationRecordLimit25())); return; }
#endif
        if (name == "record-boundary") { Console.WriteLine(JsonConvert.SerializeObject(RecordBoundary())); return; }
        if (name == "input-boundary") { Console.WriteLine(JsonConvert.SerializeObject(InputBoundary())); return; }
        bool cf = name == "CF32" || name == "CF64", placeLimit = name == "CF64-limits";
        int residents = cf || placeLimit || name.StartsWith("50000", StringComparison.Ordinal) ? 50000 : name.StartsWith("5000", StringComparison.Ordinal) ? 5000 : 957;
        int factor = cf || placeLimit || name.EndsWith("x3", StringComparison.Ordinal) ? 3 : 2;
        var state = FoodCity(residents, true, factor); PopulateDecisions(state);
        RetainVisits(state, name.StartsWith("CF64", StringComparison.Ordinal) ? 64 : 32);
        int existingPlaceGrowth = cf ? AddCfPlaces25(state) : 0;
        if (placeLimit) AddReferencePlaces(state); // Separate 20,000-place hard-cap stress, beyond CF's shared growth allowance.
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
        // FoodCity's clock is day 1. Move the fixture coherently to day 13 without processing
        // purchases: all relative schedule fields retain their original differences.
        const double shift = 12 * 1440;
        Shift25(state, shift);
        string[] names = products.Concat(Enumerable.Range(8, 248).Select(i => "name25:product:" + i.ToString("D3"))).ToArray();
        int originalScopeChars = ScopeCharacters25(state.Food!.Shops);
        state.Food!.Shops.Clear();
        for (int i = 0; i < (cf ? 1024 : 32); i++) state.Food.Shops.Add(new FoodScope { PlaceId = places[i].Id,
            BusinessId = "name25:scope:" + i, ProductId = names[(i % 32) * 8], AdditionalProductIds = names.Skip((i % 32) * 8 + 1).Take(7).ToList() });
        int scopeGrowthChars = ScopeCharacters25(state.Food.Shops) - originalScopeChars;
        state.Schema = 24; state.MenuPolicy = FoodMenuPolicy.Wire;
        var historySize = HistorySize25(state);
        if (cf)
        {
            Check(historySize.Cells <= 114688 * 128, "CF history exceeds its cell-byte allocation.");
            // All row structure and scope growth share the same 12 Mi existing-state allowance.
            // Cell payloads consume the separate E4 allowance only by their measured byte cost.
            var own = state.Citizens.Where(c => c.Identity.Id.StartsWith("pc:", StringComparison.Ordinal)).ToArray();
            int remaining = scopeGrowthChars + historySize.Rows;
            for (int i = 0; i < own.Length; i++)
            {
                int take = remaining / (own.Length - i); remaining -= take;
                string value = own[i].Identity.Name;
                Check(take <= value.Length && value.Skip(value.Length - take).All(c => c == 'X'), "Existing-state padding reduction.");
                own[i].Identity.Name = value.Substring(0, value.Length - take);
            }
            Check(remaining == 0, "History-row/scope growth was not charged to existing-state allowance.");
        }
        string before = NameBaselineCodec.Write(state);
        var beforeCounts = NameBaselineCodec.CountRecordsForSchema(state, before, 24);
        object baseline = Measure25(samples, () => NameBaselineCodec.Write(state), () => NameBaselineCodec.Read(before),
            () => NameBaselineCodec.ValidateForActivation(state), () => NameBaselineCodec.RequireExecutableCapabilities(before));
        long baselineRetained = Retained25(() => NameBaselineCodec.Read(before));
#if NAME_SCHEMA24
        Console.WriteLine(JsonConvert.SerializeObject(new { name, residents, factor, samples, source = "034bb7629391ef0fedb61d7122b0d6ee3c451ff9", schema = CityState.CurrentSchema,
            scopes = state.Food!.Shops.Count, places = state.Places.Count,
            scopeGrowthChars, historyRowGrowthChars = historySize.Rows, historyCellChars = historySize.Cells,
            before = new { chars = before.Length, bytes = Encoding.UTF8.GetByteCount(before), beforeCounts.Streaming, beforeCounts.Semantic,
                retainedBytes = baselineRetained, timing = baseline }, sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(before))) }));
        return;
#else
        long migrationStart = Stopwatch.GetTimestamp();
        var migrated = SaveCodec.Read(before); Check(migrated.Status == SaveReadStatus.Loaded, migrated.Error);
        double migrationMs = Stopwatch.GetElapsedTime(migrationStart).TotalMilliseconds;
        var upgraded = migrated.State!;
        upgraded.Schema = 24; upgraded.MenuPolicy = FoodMenuPolicy.Wire;
        Check(NameBaselineCodec.Write(upgraded) == before, "24 -> 25 changed predecessor identity/money/history/terms.");
        upgraded.Schema = 25; upgraded.MenuPolicy = FoodNameIdentity.Wire;
        state = upgraded;
        string after = SaveCodec.Write(state);
        var afterCounts = SaveCodec.CountRecords(state, after);
        Check(state.ProductNames.Count == 256, "Expected complete 256-name table.");
        Check(NameBaselineCodec.Read(after).Status == SaveReadStatus.Future, "Old executable accepted 25.");
        var candidate = Measure25(samples, () => SaveCodec.Write(state), () => SaveCodec.Read(after),
            () => SaveCodec.ValidateForActivation(state), () => SaveCodec.RequireExecutableCapabilities(after));
        long candidateRetained = Retained25(() => SaveCodec.Read(after));
        if (placeLimit)
        {
            Console.WriteLine(JsonConvert.SerializeObject(new { name, residents, factor, samples, places = state.Places.Count,
                scopes = state.Food!.Shops.Count,
                before = new { chars = before.Length, bytes = Encoding.UTF8.GetByteCount(before), beforeCounts.Streaming, beforeCounts.Semantic,
                    retainedBytes = baselineRetained, timing = baseline },
                after = new { chars = after.Length, bytes = Encoding.UTF8.GetByteCount(after), afterCounts.Streaming, afterCounts.Semantic,
                    retainedBytes = candidateRetained, timing = candidate },
                beforeSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(before))),
                migrationMs, composition = "20,000-place hard-cap stress beyond CF's shared existing-state allowance; no future-reserve clearance",
                limits = GlobalWriteLimits25(state, after) }));
            return;
        }
        string budgetWire = after;
        var budgetCounts = afterCounts;
        object? maximumHeld = cf || name == "held64" ? MaximumHeld25(state, samples, out budgetWire, out budgetCounts) : null;
        // Preserve the established B-E synthetic reserves, then add E5d's entire conservative
        // allocation without reclaiming the earlier header reserve. Actual DTO cost is separate.
        int memoryHeadroom = state.Food!.Residents.Count * 13;
        int reservedChars = 26694720 - ("\"Capabilities\":" + JsonConvert.SerializeObject(new { Active = false, Code = "B", Revision = 1 }) + ",").Length
            - (205 + 8 * 25) - AwarenessChars(state) - RivalChars(state) - WeatherChars20(state)
            - CampaignChars22(state) - ("\"MenuPolicy\":" + JsonConvert.ToString(FoodMenuPolicy.Wire) + ",").Length - OutflowChars24(state)
            - historySize.Cells;
        int reservedRecords = 191864 - 8 * (1 + 3 + 7 + 2) - 1 - AwarenessRecords(state) - WeatherRecords20(state)
            - CampaignRecords22(state) - OutflowRecords24(state) - state.Food.History.Sum(r => 1 + r.ProductSales.Count);
        // Retain the unspent E4 cell-byte allowance; full cell cardinality alone does not spend it.
        long totalChars = budgetWire.Length + reservedChars + memoryHeadroom + 540672;
        long totalBytes = Encoding.UTF8.GetByteCount(budgetWire) + reservedChars + memoryHeadroom + 540672;
        int totalRecords = Math.Max(budgetCounts.Streaming, budgetCounts.Semantic) + reservedRecords + 320;
        if (cf)
        {
            Check(totalChars <= SaveCodec.MaxInputCharacters && totalBytes <= SaveCodec.MaxInputCharacters, "Complete CF byte/character composition failed.");
            Check(totalRecords <= SaveCodec.MaxEmbeddedRecords, "Complete CF record composition failed.");
            if (name == "CF32") Check(totalChars <= SaveCodec.MaxInputCharacters * .85, "CF32 85% composition failed.");
        }
        Console.WriteLine(JsonConvert.SerializeObject(new { name, residents, factor, samples, seed = state.Seed,
            scopes = state.Food!.Shops.Count, places = state.Places.Count,
            scopeGrowthChars, historyRowGrowthChars = historySize.Rows, historyCellChars = historySize.Cells,
            fixture = "32/64 retained visits; full 20k business references/four memories; 14x1024x8 history; 256 names; 8x3 legacy held offers",
            before = new { chars = before.Length, bytes = Encoding.UTF8.GetByteCount(before), beforeCounts.Streaming, beforeCounts.Semantic, retainedBytes = baselineRetained, timing = baseline },
            after = new { chars = after.Length, bytes = Encoding.UTF8.GetByteCount(after), afterCounts.Streaming, afterCounts.Semantic, retainedBytes = candidateRetained, timing = candidate },
            migrationMs, semanticComparison = "every schema-24 field identical", tableCount = state.ProductNames.Count,
            beforeSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(before))),
            afterSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(after))), maximumHeld,
            rows = state.Food.History.Count, cells = state.Food.History.Sum(r => r.ProductSales.Count),
            composition = cf ? new { reservedChars, reservedRecords, memoryHeadroom, e5dConservativeChars = 540672, e5dRecords = 320,
                existingPlaceGrowthChars = existingPlaceGrowth, existingNameGrowthChars = 12582912 - existingPlaceGrowth - scopeGrowthChars - historySize.Rows,
                existingScopeGrowthChars = scopeGrowthChars, existingHistoryRowGrowthChars = historySize.Rows,
                existingGrowthChars = 12582912, totalChars, totalBytes, totalRecords } : null,
            processPeakBytes = Process.GetCurrentProcess().PeakWorkingSet64 }));
#endif
    }
    static int ScopeCharacters25(IEnumerable<FoodScope> scopes) => JsonConvert.SerializeObject(scopes.Select(s => new {
        s.AdditionalProductIds, s.BusinessId, s.NextServiceAt, s.PlaceId, s.ProductId }), Formatting.None).Length;
    static JsonSerializerSettings BaselineSettings25(CityState state)
    {
        var codec = typeof(NameBaselineCodec);
        object context = codec.GetNestedType("WireContext", System.Reflection.BindingFlags.NonPublic)!
            .GetMethod("FromState")!.Invoke(null, new object[] { state })!;
        return (JsonSerializerSettings)codec.GetMethod("Settings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, new[] { context })!;
    }
    static int AddCfPlaces25(CityState state)
    {
        // CF's 12 Mi allowance is shared by places, scopes, old history rows and names. These
        // fixtures spend it on 14,000 places plus the full row/scope image; the independent
        // CF64-limits case retains all 20,000 places and tests both codec caps explicitly.
        AddReferencePlaces(state);
        var settings = BaselineSettings25(state);
        int full = JsonConvert.SerializeObject(state.Places, Formatting.None, settings).Length;
        state.Places.RemoveRange(14000, state.Places.Count - 14000);
        int recovered = full - JsonConvert.SerializeObject(state.Places, Formatting.None, settings).Length;
        var own = state.Citizens.Where(c => c.Identity.Id.StartsWith("pc:", StringComparison.Ordinal)).ToArray();
        int remaining = recovered;
        for (int i = 0; i < own.Length; i++)
        {
            int extra = remaining / (own.Length - i); remaining -= extra;
            own[i].Identity.Name += new string('X', extra);
            Check(own[i].Identity.Name.Length <= 512, "CF name padding exceeds its original bound.");
        }
        return 6343907 - recovered;
    }
    static (int Cells, int Rows) HistorySize25(CityState state)
    {
        // Fixture accounting only: use the exact frozen writer, including compact versus opaque
        // cells. Row keys, counters, array brackets and separators consume existing-state space.
        var settings = BaselineSettings25(state);
        var history = state.Food!.History;
        string wire = JsonConvert.SerializeObject(history, Formatting.None, settings);
        int cells = history.Sum(r => JsonConvert.SerializeObject(r.ProductSales, Formatting.None, settings).Length - 2);
        return (cells, wire.Length - 2 - cells);
    }
    static void Shift25(CityState state, double shift)
    {
        state.Minute += shift;
        foreach (var c in state.Citizens) { c.NextEventMinute += shift; c.ActivityUntilMinute += shift; if (c.W != null) c.W = new WeatherCommitment(c.W.Boundary + shift, c.W.Weather, c.W.Value != 0); }
        foreach (var m in state.Food!.Residents)
        {
            m.Day += (int)(shift / 1440); m.DesiredAt += shift; m.ExpiresAt += shift; m.DueAt += shift;
            if (m.Budget != null) { if (m.Budget.LastPostedDay >= 0) m.Budget.LastPostedDay += (int)(shift / 1440); if (m.Budget.HomeFoodFundedDay >= 0) m.Budget.HomeFoodFundedDay += (int)(shift / 1440); m.Budget.NextPostingMinute += shift; }
            if (m.Today != null) { m.Today.Day += (int)(shift / 1440); foreach (var r in m.Today.Meals) { r.At += (int)shift; r.CheckoutAt += (int)shift; } }
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
#if !NAME_SCHEMA24
    static object GlobalWriteLimits25(CityState state, string valid)
    {
        object Refuses(string dimension)
        {
            // Inspect the refused candidate's actual bytes without bypassing any production
            // publication path. This raw serializer output is never a valid Write result.
            object context = typeof(SaveCodec).GetNestedType("WireContext", System.Reflection.BindingFlags.NonPublic)!
                .GetMethod("FromState")!.Invoke(null, new object[] { state })!;
            var settings = (JsonSerializerSettings)typeof(SaveCodec).GetMethod("Settings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .Invoke(null, new[] { context })!;
            string raw = JsonConvert.SerializeObject(state, Formatting.None, settings);
            int bytes = Encoding.UTF8.GetByteCount(raw);
            Check(dimension == "UTF-8" ? raw.Length <= SaveCodec.MaxInputCharacters && bytes > SaveCodec.MaxInputCharacters
                : raw.Length > SaveCodec.MaxInputCharacters, "Global refusal fixture does not isolate its dimension.");
            void Rejects(Action action)
            { try { action(); } catch (SaveValidationException) { return; } throw new Exception("Overbound candidate was accepted."); }
            Rejects(() => SaveCodec.Write(state)); Rejects(() => SaveCodec.ValidateForActivation(state));
            Check(SaveCodec.Read(raw).Status == SaveReadStatus.Invalid, "Overbound source was read.");
            var data = new Dictionary<string, string> { [SaveCodec.StateKey] = valid, ["other:sentinel"] = "keep" };
            Check(!SaveStore.WriteTo(data, state).Success, "Overbound store write succeeded.");
            Check(!SaveSession.Open(data, state.PopulationKey).WriteTo(data, state).Success, "Overbound session write succeeded.");
            Check(ReferenceEquals(valid, data[SaveCodec.StateKey]) && data["other:sentinel"] == "keep", "Overbound write replaced its original source.");
            return new { dimension, characters = raw.Length, bytes, result = "read/write/activation/store/session refused; original text/reference and unrelated key preserved" };
        }
        foreach (var place in state.Places) place.Name = new string('\ue001', 512);
        object utf8 = Refuses("UTF-8");
        foreach (var place in state.Places) place.Name = new string('X', 512);
        foreach (var citizen in state.Citizens.Where(c => c.Identity.Id.StartsWith("pc:", StringComparison.Ordinal))) citizen.Identity.Name = new string('X', 512);
        object characters = Refuses("characters");
        return new { utf8, characters };
    }
    static object MigrationRecordLimit25()
    {
        var state = FoodCity(50000, true, 3); PopulateDecisions(state); AddReferencePlaces(state); RetainVisits(state, 64);
        state.Schema = 24; state.MenuPolicy = FoodMenuPolicy.Wire;
        int remaining = SaveCodec.MaxEmbeddedRecords - NameBaselineCodec.CountRecords(state, NameBaselineCodec.Write(state)).Streaming;
        foreach (var place in state.Places) for (int i = 0, count = Math.Min(40, remaining); i < count; i++, remaining--)
            place.WeeklyHours.Add(new OpeningWindow { Day = 0, FromMinute = 1, ToMinute = 2 });
        Check(remaining == 0, "Exact predecessor record fixture construction.");
        string source = NameBaselineCodec.Write(state); var counts = NameBaselineCodec.CountRecords(state, source);
        Check(counts.Streaming == SaveCodec.MaxEmbeddedRecords, "Predecessor did not reach its exact record cap.");
        Check(NameBaselineCodec.Read(source).Status == SaveReadStatus.Loaded, "Record-bound predecessor is not originally valid.");
        var result = SaveCodec.Read(source);
        Check(result.Status == SaveReadStatus.Invalid, "Oversized migrated record graph was published.");
        PreserveB0(source, FoodCity(1, false));
        return new { chars = source.Length, bytes = Encoding.UTF8.GetByteCount(source), counts.Streaming, counts.Semantic,
            requiredNewRecords = 27, migration = "refused; originally valid 24 source text/reference preserved by codec/store/session", result.Error };
    }
    static object MaximumHeld25(CityState state, int samples, out string wire, out (int Streaming, int Semantic) counts)
    {
        var held = state.Food!.Residents.Single(m => m.Decision != null).Decision!;
        for (int shop = 0; shop < 8; shop++) for (int i = 0; i < 5; i++)
            held.Shops[shop].Offers.Add(new FoodDecisionOffer { ProductId = "name25:product:" + (8 + shop * 5 + i).ToString("D3"),
                Price = Money.FromCents(100), Units = 1, Metadata = new FoodOfferMetadata(2, FoodMealClass.Unknown, 2, 900, 100, 450) });
        held.Tuning = FoodDecisionTuning.Capture(new FoodChoiceSettings { FoodMenuMaximumOffersPerShop = 8 });
        state.MenuPolicy = FoodNameIdentity.DynamicWire;
        string source = SaveCodec.Write(state); wire = source; counts = SaveCodec.CountRecords(state, source);
        Check(SaveCodec.Write(SaveCodec.Read(source).State!) == source, "Maximum held image changed on cold round trip.");
        void Refuses(Action action)
        { try { action(); } catch (SaveValidationException) { return; } throw new Exception("Inactive dynamic execution was accepted."); }
        var timing = Measure25(samples, () => SaveCodec.Write(state), () => SaveCodec.Read(source),
            () => Refuses(() => SaveCodec.ValidateForActivation(state)), () => Refuses(() => SaveCodec.RequireExecutableCapabilities(source)));
        return new { offers = 64, characters = source.Length, bytes = Encoding.UTF8.GetByteCount(source), counts.Streaming, counts.Semantic,
            retainedBytes = Retained25(() => SaveCodec.Read(source)), execution = "early/full activation refused; cold storage only", timing };
    }
    static void NameEncoding25(bool maximumEscaping)
    {
        var state = FoodCity(1, false); state.Food!.Shops.Clear();
        foreach (var m in state.Food.Residents) { m.PreferredProductId = ""; m.PreferenceBonusCents = 0; m.Today = null; }
        for (int i = 0; i < 32; i++)
        {
            string place = "name25:" + i; state.Places.Add(new Place { Id = place, Name = "Shop", Kind = PlaceKind.Shop });
            string Name(int n) => maximumEscaping ? new string('\ue001', 255) + (char)('\ue100' + n)
                : new string('\ue001', 253) + n.ToString("D3");
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
        Console.WriteLine(JsonConvert.SerializeObject(new { maximumEscaping, names = state.ProductNames.Count, nameLength = 256, chars = wire.Length,
            bytes = Encoding.UTF8.GetByteCount(wire), escapedChars = escaped.Length, escapedBytes = Encoding.UTF8.GetByteCount(escaped),
            escapedTableChars = table.Length + 16, escapedTableBytes = Encoding.UTF8.GetByteCount(table) + 16, timing = Measure25(7, () => SaveCodec.Write(state), () => SaveCodec.Read(escaped), () => SaveCodec.ValidateForActivation(state), () => SaveCodec.RequireExecutableCapabilities(escaped)), counts = SaveCodec.CountRecords(state, escaped) }));
    }
#endif
}
