using System.Diagnostics;
using System.Text;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;

static object Measure(Action action)
{
    for (int i = 0; i < 2000; i++) action();
    var samples = new double[10000];
    long bytes = GC.GetAllocatedBytesForCurrentThread();
    for (int i = 0; i < samples.Length; i++)
    {
        long start = Stopwatch.GetTimestamp(); action();
        samples[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }
    bytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
    Array.Sort(samples);
    return new { p50Ms = samples[5000], p95Ms = samples[9499], maxMs = samples[^1],
        spikesAtLeast30Ms = samples.Count(s => s >= 30), allocatedBytesPerCall = (double)bytes / samples.Length };
}
static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
var state = Population.Create(new[] { new CitizenIdentity { Id = "r", Name = "Resident", Age = 30, Persona = Persona.Foodie } },
    new[] { new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 1 }, new Place { Id = "s0", Name = "Shop", Kind = PlaceKind.Shop } },
    "d4-bench", "0.40.4", 79, 1440);
state.Capabilities.Active = true;
state.Food = new FoodCommerceState();
var food = state.Food;
var meal = new ResidentMeal { ResidentId = "r", AnchorId = "r", Day = 1, Kind = MealKind.Lunch, ContextAttempts = 1, PlaceId = "s0" };
// Retained footprint comparison uses the exact same four entries, with equal-length IDs.
// Neither the contributor nor the modeled/native provenance adds any saved field.
var traits = new ResidentTraits { Bands = "2222422222" };
var settings = new FoodMemorySettings();
var empty = new ConsumerMemory();
var store = new FoodMemoryStore(food);
for (int i = 0; i < 4; i++)
{
    meal.BusinessId = i % 2 == 0 ? "own" + i : "rival" + i;
    meal.Day++; store.CommitStage(meal, ExperienceStage.Committed);
    Check(store.Experience(meal, ShopExperience.Served, meal.Day, traits, settings), "initial capture");
}
double sink = 0;
var emptyLookup = Measure(() => sink += FoodMemoryPolicy.Contribution(empty, -1, 6, traits, settings));
var mixedLookup = Measure(() => sink += FoodMemoryPolicy.Contribution(meal.Memory, store.Find("rival3"), 6, traits, settings));
var mixedUpdate = Measure(() =>
{
    meal.Day++; store.CommitStage(meal, ExperienceStage.Committed);
    Check(store.Experience(meal, ShopExperience.Served, meal.Day, traits, settings), "update");
});
Check(meal.Memory.Entries.Count == 4 && food.BusinessReferences.Count == 4, "bounded retained collections");
string memory = MemoryWire.Encode(meal.Memory);
Check(memory.Length == 48, "unchanged full memory width");
// Persist a valid ordinary fixture and compare attached active/empty contributors without
// changing the saved state. The update microbench above is deliberately outside this city.
var city = new CitySimulator(state, new Paths());
city.AttachFoodMarket(new EmptyMarket(), memorySettings: new FoodMemorySettings { FoodMemoryWeight = 0 });
food.Residents[0].Memory.Entries = meal.Memory.Entries.Select(e => new ShopMemory {
    BusinessRef = e.BusinessRef, Day = 1, Served = e.Served, Affinity = e.Affinity, Outcome = e.Outcome }).ToList();
string neutral = SaveCodec.Write(state);
city = new CitySimulator(state, new Paths());
city.AttachFoodMarket(new EmptyMarket(), memorySettings: settings);
string active = SaveCodec.Write(state);
Check(neutral == active, "contributor alone changes no save bytes");
var counts = SaveCodec.CountRecords(state, active);
var ownControl = SaveCodec.Read(active);
Check(ownControl.Status == SaveReadStatus.Loaded, "mixed memory reload");
foreach (var reference in ownControl.State!.Food!.BusinessReferences)
    if (reference.BusinessId.StartsWith("rival", StringComparison.Ordinal))
        reference.BusinessId = "ownxx" + reference.Slot; // equal-length historical own generation
string ownWire = SaveCodec.Write(ownControl.State);
var ownCounts = SaveCodec.CountRecords(ownControl.State, ownWire);
Check(Encoding.UTF8.GetByteCount(ownWire) == Encoding.UTF8.GetByteCount(active)
    && counts == ownCounts, "mixed provenance adds no byte/record overhead over four own entries");
var observation = new FoodAlternativeObservation("business:Test:1:1:food:rival", "s0", "Test", 1, 1, "food", "rival", FoodAlternativeKind.Rival,
    true, true, false, null, 0, new[] { new FoodAdvertisedOffer(FoodProducts.Croissant, Money.FromCents(100)) },
    Enumerable.Range(0, 7).Select(d => new FoodAdvertisedHours(d, 0, 1440)));
bool open = false;
var beforeOpening = Measure(() =>
{
    var hours = new Place { WeeklyHours = observation.Hours.Select(h => new OpeningWindow { Day = h.Day, FromMinute = h.FromMinute, ToMinute = h.ToMinute }).ToList() };
    open = DailyRoutine.Open(hours, 1500);
});
var afterOpening = Measure(() => open = !RivalFoodMemory.Closed(observation, 1500));
Check(open, "opening equivalence");
const string ownGeneration = "business:Test:1:1:food:player:generation:1";
bool modeled = true;
var beforeOwnWords = Measure(() => modeled = ownGeneration.StartsWith("business:", StringComparison.Ordinal)
    && ownGeneration.Split(':').Length == 6);
var afterOwnWords = Measure(() => modeled = RivalFoodMemory.ModeledGeneration(ownGeneration));
Check(!modeled, "player authority is not modeled rival evidence");
Console.WriteLine(JsonConvert.SerializeObject(new { source = "D4 offline .NET 10 Release; warmed single operations; no native frame gate",
    emptyLookup, mixedFourEntryLookup = mixedLookup, mixedFourEntryUpdate = mixedUpdate,
    retainedEntries = meal.Memory.Entries.Count, retainedReferences = food.BusinessReferences.Count,
    saturatedMemoryCharacters = memory.Length, contributorNeutralBytes = Encoding.UTF8.GetByteCount(neutral),
    contributorActiveBytes = Encoding.UTF8.GetByteCount(active), allOwnFourEntryBytes = Encoding.UTF8.GetByteCount(ownWire),
    streamingRecords = counts.Streaming, semanticRecords = counts.Semantic,
    allOwnStreamingRecords = ownCounts.Streaming, allOwnSemanticRecords = ownCounts.Semantic, writer = state.Schema,
    beforeOpening, afterOpening, beforeOwnWords, afterOwnWords, sink }, Formatting.Indented));

sealed class EmptyMarket : IFoodMarket
{
    public double EarliestMinute => 0;
    public IReadOnlyList<FoodBusiness> Read(double at) => Array.Empty<FoodBusiness>();
    public FoodSale Buy(ResidentMeal meal, double at) => throw new Exception("No checkout in memory benchmark");
}

sealed class Paths : IRoutePlanner
{
    public Route Plan(Place a, Place b, double at, TravelPreferences preferences) => new Route {
        Legs = { new RouteLeg { From = a.Position, To = b.Position, Minutes = 1, Mode = TravelMode.Walk } } };
}
