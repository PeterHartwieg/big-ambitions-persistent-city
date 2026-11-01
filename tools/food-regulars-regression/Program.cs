using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;

sealed class Paths : IRoutePlanner
{
    public Route? Plan(Place from, Place to, double at, TravelPreferences preferences)
        => throw new Exception("Projection requested a route");
}
sealed class Market : IFoodMarket
{
    public double EarliestMinute => 0;
    public IReadOnlyList<FoodBusiness> Read(double at) => throw new Exception("Projection read market");
    public FoodSale Buy(ResidentMeal meal, double at) => throw new Exception("Projection checked out");
}
static class Program
{
    static object Measure(Action action, int batch)
    {
        var samples = new List<double>(); long allocated = 0;
        for (int round = -5; round < 40; round++)
        {
            long bytes = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
            for (int i = 0; i < batch; i++) action();
            double elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            if (round < 0) continue;
            allocated += GC.GetAllocatedBytesForCurrentThread() - bytes;
            samples.Add(elapsed / batch);
        }
        samples.Sort();
        return new { p50Ms = samples[20], p95Ms = samples[37], maxMs = samples[39],
            allocatedBytesPerCall = allocated / (40.0 * batch),
            batchSpikesAtLeast30Ms = samples.Count(ms => ms * batch >= 30) };
    }

    static object Run(int residents)
    {
        var places = new List<Place> { new() { Id = "address:Maple%20Street:12:home", Name = "Home", Kind = PlaceKind.Home, Capacity = residents } };
        places.AddRange(Enumerable.Range(0, 16).Select(i => new Place { Id = "address:Market%20Lane:" + (i + 1) + ":shop", Name = "Fixture bakery", Kind = PlaceKind.Shop, Position = new(i + 1, 0, 0) }));
        var state = Population.Create(Enumerable.Range(0, residents).Select(i => new CitizenIdentity { Id = "r" + i, Name = "Resident", Age = 30, Persona = Persona.Foodie }),
            places, "regulars-fixture", "0.40.4", 31, 1440);
        foreach (var person in state.Citizens) person.ActivityUntilMinute = person.NextEventMinute = 2870;
        state.Capabilities.Active = true;
        state.Food = new() { RepresentationFactor = 2, MealRhythmVersion = 1, Shops = places.Skip(1).Select((p, i) =>
            new FoodScope { PlaceId = p.Id, BusinessId = "generation:" + i, ProductId = FoodProducts.Croissant }).ToList() };
        var setup = new CitySimulator(state, new Paths()); setup.AttachFoodMarket(new Market());
        var food = state.Food;
        for (int i = 0; i < 16; i++) food.BusinessReferences.Add(new() { Slot = i, BusinessId = food.Shops[i].BusinessId, PlaceId = food.Shops[i].PlaceId });
        for (int i = 0; i < food.Residents.Count; i++)
            for (int j = 0; j < 4; j++) food.Residents[i].Memory.Entries.Add(new() {
                BusinessRef = (i % 4) * 4 + j, Day = 1, Served = 35, Affinity = 8, Outcome = ShopExperience.Served });
        var city = new CitySimulator(state, new Paths()); city.AttachFoodMarket(new Market());
        string before = SaveCodec.Write(state);
        var projection = city.ProjectRegulars()!;
        int sink = 0; string text = "";
        var rebuild = Measure(() => { var p = city.ProjectRegulars()!; foreach (var shop in food.Shops) sink += p.CountOwnShop(shop.BusinessId); }, 20);
        var lookup = Measure(() => sink += projection.CountOwnShop(food.Shops[0].BusinessId), 10000);
        var panelRegularText = Measure(() => {
            var p = city.ProjectRegulars()!; var b = new StringBuilder();
            foreach (var shop in food.Shops) b.AppendLine(p.OwnShopText(shop.BusinessId));
            text = b.ToString();
        }, 20);
        var popup = Measure(() => text = city.Inspect(state.Citizens[0].Identity.Id)!.Text(), 100);
        string after = SaveCodec.Write(state);
        if (after != before) throw new Exception("Read-only surfaces changed payload");
        var restored = new CitySimulator(SaveCodec.Read(before).State!, new Paths()); restored.AttachFoodMarket(new Market());
        foreach (var shop in food.Shops)
            if (restored.ProjectRegulars()!.CountOwnShop(shop.BusinessId) != projection.CountOwnShop(shop.BusinessId))
                throw new Exception("Reload changed regulars");
        // Compare the actual transient dedup structure before/after the low-risk capacity change.
        var growingSet = Measure(() => { var seen = new HashSet<string>(StringComparer.Ordinal); foreach (var m in food.Residents) seen.Add(m.ResidentId); sink += seen.Count; }, 20);
        var presizedSet = Measure(() => { var seen = new HashSet<string>(food.Residents.Count, StringComparer.Ordinal); foreach (var m in food.Residents) seen.Add(m.ResidentId); sink += seen.Count; }, 20);
        // B2 installs no commit hook. Measure the unchanged production repeat-commit path separately.
        var store = new FoodMemoryStore(food); var settings = new FoodMemorySettings(); var meal = food.Residents[0];
        var traits = state.Citizens[0].Traits!; int day = 2;
        var commit = Measure(() => {
            meal.BusinessId = food.Shops[0].BusinessId; meal.PlaceId = food.Shops[0].PlaceId; meal.Day = day++; meal.ContextAttempts = 1;
            if (!store.CommitStage(meal, ExperienceStage.Committed) || !store.Experience(meal, ShopExperience.Served, meal.Day, traits, settings))
                throw new Exception("Commit failed");
        }, 1000);
        return new { residents, consumers = food.Residents.Count, memoryEntries = food.Residents.Count * 4,
            shops = 16, projectionRefresh = rebuild, perShopLookup = lookup, panelB2Text = panelRegularText,
            fullCorePopupCaptureAndText = popup, unchangedB1Commit = commit, projectionCommitHooks = 0,
            closedProjectionBuilds = 0, closedTextBuilds = 0, closedAddedAllocations = 0,
            growingDedupSet = growingSet, presizedDedupSet = presizedSet,
            savedBytesBefore = Encoding.UTF8.GetByteCount(before), savedBytesAfter = Encoding.UTF8.GetByteCount(after),
            savedByteGrowth = 0, sink, textLength = text.Length };
    }

    static void Main()
    {
        Run(10); // warm JIT outside recorded fixtures
        Console.WriteLine(JsonConvert.SerializeObject(new {
            boundary = "Offline .NET 10; 40 normalized batch means after 5 warmups, not individual-call percentiles. Saturated four-entry memory; 16 owned shops. Panel timing covers B2 text including projection; native account reads/Unity GUI excluded. Full Core popup capture/text included. Closed zero cost and zero commit hooks established by call-site/source tests, not a native timing window. Batch spikes flag benchmark batches, not mod-work frames.",
            populations = new[] { Run(957), Run(2500) },
            nativeModWork = "pending normal combined 6 Nov session: p95 <5 ms, flag >=30 ms; whole-frame separate"
        }, Formatting.Indented));
    }
}
