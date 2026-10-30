using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
static class Program
{
    private static CityState City(int count, int seed = 11)
    {
        var identities = Enumerable.Range(0, count).Select(i => new CitizenIdentity
        {
            Id = "rlo:test:c" + i.ToString("D3"), Name = "Citizen " + i, Age = 20 + i % 50, District = i % 2 == 0 ? "a" : "b",
            Persona = (Persona)(i % 8), AppearanceSeed = i + 1
        });
        var places = new[]
        {
            new Place { Id = "home-a", Name = "Home A", District = "a", Kind = PlaceKind.Home, Capacity = count, Position = new(0, 0, 0) },
            new Place { Id = "home-b", Name = "Home B", District = "b", Kind = PlaceKind.Home, Capacity = count, Position = new(900, 0, 0) },
            new Place { Id = "work-1", Name = "Work 1", District = "a", Kind = PlaceKind.Work, Capacity = count / 3 + 1, Position = new(300, 0, 400) },
            new Place { Id = "work-2", Name = "Work 2", District = "b", Kind = PlaceKind.Work, Capacity = count / 3 + 1, Position = new(1500, 0, -200) },
            new Place { Id = "work-3", Name = "Work 3", District = "a", Kind = PlaceKind.Work, Capacity = count / 3 + 1, Position = new(-600, 0, 900) },
            new Place { Id = "school", Name = "School", District = "a", Kind = PlaceKind.Study, Capacity = count, Position = new(200, 0, -700) },
            new Place { Id = "shop-1", Name = "Shop 1", District = "a", Kind = PlaceKind.Shop, Position = new(150, 0, 120), OpenMinute = 480, CloseMinute = 1200 },
            new Place { Id = "shop-2", Name = "Shop 2", District = "b", Kind = PlaceKind.Shop, Position = new(1000, 0, 300), OpenMinute = 540, CloseMinute = 1260 },
            new Place { Id = "shop-3", Name = "Shop 3", District = "a", Kind = PlaceKind.Shop, Position = new(-400, 0, -300), OpenMinute = 600, CloseMinute = 1080 },
            new Place { Id = "leisure-1", Name = "Leisure 1", District = "a", Kind = PlaceKind.Leisure, Position = new(60, 0, 250), OpenMinute = 600, CloseMinute = 1380 },
            new Place { Id = "leisure-2", Name = "Leisure 2", District = "b", Kind = PlaceKind.Leisure, Position = new(1200, 0, 600), OpenMinute = 660, CloseMinute = 1320 },
            new Place { Id = "leisure-3", Name = "Leisure 3", District = "a", Kind = PlaceKind.Leisure, Position = new(-200, 0, 800), OpenMinute = 720, CloseMinute = 1440 }
        };
        var city = Population.Create(identities, places, "resumable-test", "0.40.4", seed, 1440);
        // Some workers start unemployed so the daily employment check probes workplaces during the run.
        for (int i = 0; i < city.Citizens.Count; i += 5) city.Citizens[i].DayPlaceId = "";
        return city;
    }

    private sealed class NoMarket : IFoodMarket
    {
        public double EarliestMinute => 0;
        public IReadOnlyList<FoodBusiness> Read(double minute) => Array.Empty<FoodBusiness>();
        public FoodSale Buy(ResidentMeal meal, double minute) => throw new InvalidOperationException("No checkout in the size benchmark.");
    }
    private sealed class Straight : IRoutePlanner
    {
        public Route? Plan(Place from, Place to, double at, TravelPreferences preferences) => null;
    }

    // Schema-17-only members are reached by reflection so this file still compiles against a schema-16 tree.
    private static readonly System.Reflection.PropertyInfo? TodayProperty = typeof(ResidentMeal).GetProperty("Today");

    // A resident-food city at the runtime representation factor (2) with household finances. With steady set,
    // each schema-17 consumer carries an end-of-day image: three recorded outcomes (bought, alternative, skipped).
    private static CityState FoodCity(int residents, bool steady)
    {
        var state = EconomyScenario.Create(residents, 11);
        state.Food = EconomyScenario.Food();
        var city = new CitySimulator(state, new Straight()); city.AttachFoodMarket(new NoMarket()); city.EnableHouseholdFinances();
        if (steady && TodayProperty != null)
        {
            int day = (int)(state.Minute / 1440) - 1;
            string At(double offset) => (day * 1440 + offset).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            string template = "{\"Day\":" + day + ",\"Meals\":["
                + "{\"At\":" + At(480.25) + ",\"CheckoutAt\":" + At(480.25) + ",\"Factor1\":0,\"Factor2\":0,\"Kind\":1,\"Outside\":0,\"PlaceId\":\"shop-1\",\"ProductId\":\"" + FoodProducts.Croissant + "\",\"Reason\":1,\"Status\":0},"
                + "{\"At\":" + At(750.5) + ",\"CheckoutAt\":0.0,\"Factor1\":0,\"Factor2\":0,\"Kind\":2,\"Outside\":3,\"PlaceId\":\"\",\"ProductId\":\"\",\"Reason\":3,\"Status\":1},"
                + "{\"At\":" + At(1140.75) + ",\"CheckoutAt\":0.0,\"Factor1\":0,\"Factor2\":0,\"Kind\":3,\"Outside\":0,\"PlaceId\":\"shop-3\",\"ProductId\":\"" + FoodProducts.Burger + "\",\"Reason\":11,\"Status\":2}"
                + "],\"Unrecorded\":0}";
            foreach (var meal in state.Food.Residents) TodayProperty.SetValue(meal, JsonConvert.DeserializeObject(template, TodayProperty.PropertyType));
        }
        return state;
    }

    // The same payload as an earlier writer would have produced it (schema 16): schema-17 fields removed.
    private static string AsSchema16(string json) => Regex.Replace(Regex.Replace(Regex.Replace(json,
            "\"ChoicePolicyVersion\":1,\"ChoiceStartsDay\":[0-9]+,", ""),
            ",\"(Traits|RepresentedTraits)\":(null|\\{\"Bands\":\"[0-4]{10}\",\"Version\":1\\})|,\"Today\":null|,\"Decision\":null|,\"LastContextKey\":\"\"|,\"ContextAttempts\":0", ""),
            "\"Schema\":17,", "\"Schema\":16,");

    private static double Time(int runs, Action action)
    {
        var samples = new List<double>();
        for (int i = 0; i < runs; i++)
        {
            var watch = Stopwatch.StartNew(); action(); samples.Add(watch.Elapsed.TotalMilliseconds);
        }
        samples.Sort();
        return Math.Round(samples[samples.Count / 2], 1);
    }

    private static object Bench(int residents, int runs)
    {
        var state = FoodCity(residents, steady: true);
        string json = SaveCodec.Write(state);
        bool candidate = TodayProperty != null;
        // Schema-16 image of the same city without outcomes: what an upgrade actually reads.
        var fresh = FoodCity(residents, steady: false);
        string old = candidate ? AsSchema16(SaveCodec.Write(fresh)) : SaveCodec.Write(fresh);
        var migrated = SaveCodec.Read(old);
        if (migrated.Status != SaveReadStatus.Loaded) throw new Exception("Schema-16 image did not load: " + migrated.Error);
        migrated = null!;
        double write = Time(runs, () => SaveCodec.Write(state));
        double read = Time(runs, () => { if (SaveCodec.Read(json).Status != SaveReadStatus.Loaded) throw new Exception("read failed"); });
        double upgrade = Time(runs, () => SaveCodec.Read(old));
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long before = GC.GetTotalMemory(true);
        var retained = SaveCodec.Read(json).State;
        long after = GC.GetTotalMemory(true);
        GC.KeepAlive(retained);
        return new
        {
            residents, consumers = state.Food!.Residents.Count, chars = json.Length, utf8Bytes = Encoding.UTF8.GetByteCount(json),
            schema16Chars = old.Length, writeMs = write, readMs = read, upgradeFrom16Ms = upgrade,
            retainedMiB = Math.Round((after - before) / 1048576.0, 1)
        };
    }

    // Candidate only: trait generation and the outcome updater's hot cost (no-ops on a schema-16 tree).
    private static object? Hot()
    {
        var traits = Type.GetType("PersistentCity.Core.ResidentTraits, " + typeof(CityState).Assembly.FullName);
        var outcomes = Type.GetType("PersistentCity.Core.MealOutcomes, " + typeof(CityState).Assembly.FullName);
        if (traits == null || outcomes == null) return null;
        var generate = (Func<int, string, object>)Delegate.CreateDelegate(typeof(Func<int, string, object>), traits.GetMethod("Generate")!);
        var ids = Enumerable.Range(0, 50000).Select(i => "consumer:rlo%3Atest%3Ac" + i.ToString("D5") + ":1").ToArray();
        foreach (var id in ids.Take(1000)) generate(11, id);
        var watch = Stopwatch.StartNew();
        foreach (var id in ids) generate(11, id);
        double traitNs = watch.Elapsed.TotalMilliseconds * 1e6 / ids.Length;
        var record = (Action<ResidentMeal, double, bool, FoodOutsideOption>)Delegate.CreateDelegate(
            typeof(Action<ResidentMeal, double, bool, FoodOutsideOption>), outcomes.GetMethod("Record")!);
        var meal = new ResidentMeal { ResidentId = "r", AnchorId = "r", PlaceId = "shop-1", ProductId = FoodProducts.Burger, Reason = "Native fake confirmed" };
        const int calls = 3000000;
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        watch.Restart();
        for (int i = 0; i < calls; i++)
        {
            // Three outcomes a day, then the lazy advance to the next day on that consumer's first outcome.
            meal.Day = i / 3; meal.Kind = MealKind.Breakfast + i % 3;
            meal.Status = i % 3 == 0 ? MealStatus.Paid : i % 3 == 1 ? MealStatus.Declined : MealStatus.Missed;
            record(meal, meal.Day * 1440.0 + 600, i % 3 == 0, FoodOutsideOption.FundedHouseholdFood);
        }
        double recordNs = watch.Elapsed.TotalMilliseconds * 1e6 / calls;
        long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        return new { traitGenerateNs = Math.Round(traitNs), outcomeRecordNs = Math.Round(recordNs, 1),
            outcomeRecordBytesPerConsumerDay = Math.Round(bytes * 3.0 / calls) };
    }

    static void Main(string[] args)
    {
        var city = City(120);
        string expected = SaveCodec.Write(city);
        SaveCodec.Read(expected);
        long start = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 20; i++)
            if (SaveCodec.Write(city) != expected) throw new Exception("write changed bytes");
        long writeBytes = GC.GetAllocatedBytesForCurrentThread() - start;
        start = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 20; i++)
            if (SaveCodec.Read(expected).Status != SaveReadStatus.Loaded) throw new Exception("read failed");
        long readBytes = GC.GetAllocatedBytesForCurrentThread() - start;
        var refusals = new List<string>();
        foreach (string payload in new[] { "{}", "{\"Schema\":999}", "{\"Schema\":13,", expected.Replace("\"Citizens\":", "\"UnknownCitizens\":") })
        {
            var result = SaveCodec.Read(payload);
            refusals.Add(result.Status + ":" + result.Error);
        }
        // Schema 17 adds fields by design: the save digest is reported, the economy digest must match.
        var economy = EconomyScenario.Run();
        object? bench = null, hot = null;
        if (args.Contains("--bench"))
        {
            bool ceiling = args.Contains("--ceiling");
            var sizes = new List<object> { Bench(957, 5), Bench(5000, 3) };
            if (ceiling) sizes.Add(Bench(50000, 1));
            bench = sizes; hot = Hot();
        }
        Console.WriteLine(JsonConvert.SerializeObject(new { schema = CityState.CurrentSchema, digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(expected))),
            length = expected.Length, refusals, economy, writeBytes, readBytes, bench, hot }));
    }
}
