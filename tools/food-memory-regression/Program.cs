using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Activity = PersistentCity.Core.Activity;

sealed class Paths : IRoutePlanner
{
    public int Calls;
    public Route Plan(Place a, Place b, double at, TravelPreferences preferences)
    { Calls++; return new() { Legs = { new() { From = a.Position, To = b.Position, Minutes = 1, Mode = TravelMode.Walk } } }; }
}
sealed class Market : IFoodMarket, ILocalFoodMarket
{
    public FoodBusiness[] Shops = Array.Empty<FoodBusiness>();
    public double EarliestMinute => 0;
    public IReadOnlyList<FoodBusiness> Read(double at) => Shops;
    public IReadOnlyList<FoodBusiness> ReadLocal(Place p, double at, double tolerance, int count) => throw new Exception("Legacy shortlist");
    public FoodBusiness? ReadBusiness(string id, double at) => Shops.FirstOrDefault(b => b.Id == id);
    public FoodSale Buy(ResidentMeal meal, double at) => throw new Exception("Decision benchmark entered checkout");
}
static class Program
{
    static readonly ResidentTraits Traits = new() { Bands = "2222222222" };
    static readonly FoodMemorySettings Settings = new();
    static (CityState state, Market market, Paths paths) Fixture(int residents)
    {
        var places = new List<Place> { new() { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = residents } };
        places.AddRange(Enumerable.Range(0, 16).Select(i => new Place { Id = "s" + i, Name = "Shop", Kind = PlaceKind.Shop, Position = new(i + 1, 0, 0) }));
        var state = Population.Create(Enumerable.Range(0, residents).Select(i => new CitizenIdentity { Id = "r" + i, Name = "Resident", Age = 30, Persona = Persona.Foodie }), places, "memory-bench", "0.40.4", 31, 1440);
        foreach (var p in state.Citizens) p.NextEventMinute = p.ActivityUntilMinute = 2870;
        state.Capabilities.Active = true;
        state.Food = new() { RepresentationFactor = 2, MealRhythmVersion = 1, Shops = places.Skip(1).Select((p, i) => new FoodScope { PlaceId = p.Id, BusinessId = "business:fixture:player:generation:" + i, ProductId = FoodProducts.Croissant }).ToList() };
        var market = new Market { Shops = state.Food.Shops.Select(s => new FoodBusiness { Id = s.BusinessId, PlaceId = s.PlaceId, MealsPerHour = 6000,
            Hours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 0, ToMinute = 1440 }).ToList(),
            Offers = new() { new() { ProductId = FoodProducts.Croissant, Price = Money.FromCents(100), Units = 100000 } } }).ToArray() };
        var paths = new Paths(); var city = new CitySimulator(state, paths); city.AttachFoodMarket(market);
        state.Minute = 2160; state.Food.ChoiceStartsDay = 1;
        foreach (var m in state.Food.Residents) { m.Kind = MealKind.Lunch; m.DesiredAt = m.DueAt = 2160; m.ExpiresAt = 2205; }
        return (state, market, paths);
    }
    static void Populate(CityState state)
    {
        var food = state.Food!;
        for (int i = 0; i < 16; i++) food.BusinessReferences.Add(new() { Slot = i, BusinessId = food.Shops[i].BusinessId, PlaceId = food.Shops[i].PlaceId });
        for (int i = 0; i < food.Residents.Count; i++)
        {
            var memory = food.Residents[i].Memory;
            for (int j = 0; j < 4; j++) memory.Entries.Add(new() { BusinessRef = (i % 4) * 4 + j, Day = 1, Served = 35, Affinity = j * 4 - 8, Outcome = ShopExperience.Served });
            memory.Watermark = new() { Day = 0, Meal = MealKind.Dinner, Context = 4, Stage = ExperienceStage.Served };
        }
    }
    static object Stats(List<double> values)
    {
        values.Sort(); return new { p50Ms = values[values.Count / 2], p95Ms = values[(int)Math.Ceiling(values.Count * .95) - 1], maxMs = values[^1], spikesAtLeast30Ms = values.Count(v => v >= 30) };
    }
    static object PopulationBench(int residents)
    {
        var (state, _, _) = Fixture(residents);
        string empty = SaveCodec.Write(state);
        GC.Collect(); long before = GC.GetTotalMemory(true), bytes = GC.GetAllocatedBytesForCurrentThread();
        Populate(state);
        long allocations = GC.GetAllocatedBytesForCurrentThread() - bytes, retained = GC.GetTotalMemory(true) - before;
        string saturated = SaveCodec.Write(state);
        if (SaveCodec.Read(saturated).Status != SaveReadStatus.Loaded) throw new Exception("Saturated checkpoint refused");
        var store = new FoodMemoryStore(state.Food!); var residentsList = state.Food!.Residents;
        double sink = 0; var lookup = new List<double>(); var updates = new List<double>(); long lookupAllocations = 0, updateAllocations = 0;
        for (int round = -5; round < 40; round++)
        {
            bytes = GC.GetAllocatedBytesForCurrentThread(); long start = Stopwatch.GetTimestamp();
            foreach (var m in residentsList)
                for (int candidate = 0; candidate < 16; candidate++)
                    sink += FoodMemoryPolicy.Contribution(m.Memory, store.Find(state.Food.Shops[candidate].BusinessId), 1, Traits, Settings);
            double elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            if (round >= 0) { lookupAllocations += GC.GetAllocatedBytesForCurrentThread() - bytes; lookup.Add(elapsed / (residentsList.Count * 16)); }
            bytes = GC.GetAllocatedBytesForCurrentThread(); start = Stopwatch.GetTimestamp();
            foreach (var m in residentsList)
            {
                m.Day = round + 10; m.ContextAttempts = 1; m.BusinessId = state.Food.Shops[0].BusinessId; m.PlaceId = "s0";
                if (!store.CommitStage(m, ExperienceStage.Committed) || !store.Experience(m, ShopExperience.Served, round + 10, Traits, Settings)) throw new Exception("Commit watermark failed");
            }
            elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            if (round >= 0) { updateAllocations += GC.GetAllocatedBytesForCurrentThread() - bytes; updates.Add(elapsed / residentsList.Count); }
        }
        return new { residents, consumers = residentsList.Count, saturatedEntries = residentsList.Count * 4,
            retainedMemoryBytes = retained, populationAllocatedBytes = allocations,
            emptySavedBytes = Encoding.UTF8.GetByteCount(empty), saturatedSavedBytes = Encoding.UTF8.GetByteCount(saturated),
            savedByteGrowth = saturated.Length - empty.Length, perConsumerMemoryGrowth = 40,
            consumerExtensionInclusiveCeiling = 51, b0PerConsumerAllocation = 64,
            perCandidateLookup = Stats(lookup), lookupAllocatedBytesPerCandidate = lookupAllocations / (double)(40L * residentsList.Count * 16),
            perCommit = Stats(updates), allocatedBytesPerCommit = updateAllocations / (double)(40L * residentsList.Count), sink };
    }
    static readonly Dictionary<string,string> RushDigests = new();
    static object Rush(int residents, bool active, int unitBudget, int sample)
    {
        var (state, market, paths) = Fixture(residents); Populate(state);
        var city = new CitySimulator(state, paths); city.AttachFoodMarket(market, memorySettings: new() { FoodMemoryWeight = active ? 1 : 0 });
        int frames = 0, events = 0, completed = 0; var windows = new List<double>(); city.FoodDecisionObserved += _ => completed++;
        var total = Stopwatch.StartNew();
        while (!city.IsCaughtUpTo(2160))
        {
            int units = 0, citizens = 0; long start = Stopwatch.GetTimestamp();
            do
            {
                var result = city.AdvanceFrameSlice(2160, 1); units += result.Events; events += result.Events; citizens += result.CitizenEvents;
                if (result.CaughtUp) break;
            } while (units < unitBudget && citizens < 16 && Stopwatch.GetElapsedTime(start).TotalMilliseconds < 2);
            city.SynchronizeSnapshot(); frames++; windows.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }
        total.Stop();
        if (completed != state.Food!.Residents.Count || paths.Calls != completed * 16) throw new Exception("Changed route or decision bounds");
        int availableFrames = 1500; // 45 logical minutes at 3 minutes/sec and 100 fps (fastest modeled clock).
        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(SaveCodec.Write(state))));
        string key = residents + ":" + active;
        if (RushDigests.TryGetValue(key, out var expected) && digest != expected) throw new Exception("Budget/checkpoint replay changed choices/memory");
        RushDigests[key] = digest;
        return new { residents, consumers = completed, memoryWeight = active ? 1 : 0, unitBudget, sample, frames, events,
            routes = paths.Calls, decisionsPerSecond = completed / total.Elapsed.TotalSeconds,
            cpuMs = total.Elapsed.TotalMilliseconds, offlineModLoop = Stats(windows), wholeFrame = "unmeasured; separate native watch item",
            modeledCatchupLagMinutesAtFastestClock = Math.Max(0, frames - availableFrames) / 100.0 * 3,
            savedStateSha256 = digest };
    }
    static void Main(string[] args)
    {
        var rush = new List<object>();
        // Warm JIT, then interleave weight 1/0. Single-unit controls verify frame-budget replay.
        Rush(10, true, 128, -1); Rush(10, false, 128, -1);
        foreach (int residents in new[] { 957, 2500 })
        for (int sample = 0; sample < 3; sample++)
        foreach (bool active in sample % 2 == 0 ? new[] { true, false } : new[] { false, true }) rush.Add(Rush(residents, active, 128, sample));
        rush.Add(Rush(957, true, 1, 3)); rush.Add(Rush(957, false, 1, 3));
        Console.WriteLine(JsonConvert.SerializeObject(new { boundary = "Offline .NET 10 synthetic paths; feature cost only, no native mod-work or whole-frame gate claim", populations = new[] { PopulationBench(957), PopulationBench(2500) }, rush }, Formatting.Indented));
    }
}
