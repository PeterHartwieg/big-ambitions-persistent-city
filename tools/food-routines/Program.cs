using System.Diagnostics;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;

string output = Path.GetFullPath(args.Length > 0 ? args[0] : "build/food-routines-demo");
int count = args.Length > 1 ? int.Parse(args[1]) : 957;
if (args.Length > 2 || count < 10 || count > 5000) throw new ArgumentException("Usage: [output-directory] [10..5000 residents]");
Directory.CreateDirectory(output);
const int days = 7;
var city = Fixture(count);
var routePlanner = new FixtureWalking();
var simulation = new CitySimulator(city, routePlanner);
using var recorder = new FoodRoutineRecorder(simulation);
var timer = Stopwatch.StartNew();
while (!simulation.AdvanceTo(8 * 1440, 128).CaughtUp) { }
var capture = recorder.Finish();
double populationReplayMs = timer.Elapsed.TotalMilliseconds;
timer.Restart(); var plan = FoodRoutinePlanning.Build(capture, 1, days, 1800, allowOutsideOptions: false);
double opportunityMs = timer.Elapsed.TotalMilliseconds;
var markets = new[]
{
    (Name: "one-small", Shops: new[] { Shop("a", "cafe-a", 15) }),
    (Name: "two-neighborhoods", Shops: new[] { Shop("a", "cafe-a", 15), Shop("b", "cafe-b", 15) }),
    (Name: "one-larger", Shops: new[] { Shop("a", "cafe-a", 30) }),
    (Name: "two-same-location", Shops: new[] { Shop("a", "cafe-a", 15), Shop("a2", "cafe-a", 15) }),
    (Name: "second-unavailable", Shops: new[] { Shop("a", "cafe-a", 15), Shop("b", "cafe-b", 0) }),
    (Name: "second-price-plus-25pct", Shops: new[] { Shop("a", "cafe-a", 15), Shop("b", "cafe-b", 15, 1000) })
};
const string evidence = "Synthetic two-neighborhood city replayed through the actual population/routine engine. Connected fixture paths, controlled prices/stock/staffed hourly throughput and temporary allowances. No native sales or measured profitability.";
var reports = new List<ScenarioResult>();
FoodShadowState? baselineInitial = null;
foreach (var market in markets)
{
    routePlanner = new FixtureWalking();
    timer.Restart();
    var batch = new FoodRouteBatch(capture, plan, market.Shops, city.Places, routePlanner, "Controlled connected fixture walking graph", 3);
    int maximumCalls = 0;
    while (!batch.Complete)
    {
        int before = batch.RouteCalls; batch.Step(2); maximumCalls = Math.Max(maximumCalls, batch.RouteCalls - before);
    }
    double quoteMs = timer.Elapsed.TotalMilliseconds;
    var state = FoodDemand.CreateObserved(capture.CityKey, evidence, 1, days, plan.Consumers, market.Shops, batch.Quotes, plan.Opportunities);
    if (market.Name == "two-neighborhoods")
    {
        File.WriteAllText(Path.Combine(output, "initial.json"), FoodShadowCodec.Write(state));
        baselineInitial = FoodShadowCodec.Read(FoodShadowCodec.Write(state), state.CityKey);
    }
    var engine = new FoodDemand(state);
    timer.Restart();
    double maximumBatchMs = 0; int batches = 0;
    while (true)
    {
        long before = Stopwatch.GetTimestamp(); bool done = engine.AdvanceTo(state.EndsAt, 16);
        maximumBatchMs = Math.Max(maximumBatchMs, Stopwatch.GetElapsedTime(before).TotalMilliseconds); batches++;
        if (done) break;
    }
    double simulationMs = timer.Elapsed.TotalMilliseconds;
    var successful = state.Intents.Where(i => i.Status == FoodIntentStatus.Fulfilled).ToArray();
    var report = new ScenarioResult(market.Name, successful.Length,
        state.Intents.Count(i => i.Status == FoodIntentStatus.Unmet), state.Intents.Count(i => i.Status == FoodIntentStatus.Unresolved),
        market.Shops.ToDictionary(b => b.Id, b => successful.Count(i => i.BusinessId == b.Id)),
        successful.Length == 0 ? 0 : successful.Average(i => i.ReturnBy!.Value - i.RequestedAt),
        successful.Length == 0 ? 0 : successful.Average(i => i.ServiceEndsAt!.Value - i.ArrivalMinute!.Value - 60.0 / market.Shops.Single(b => b.Id == i.BusinessId).MealsPerHour!.Value),
        batch.RouteCalls, maximumCalls, quoteMs, simulationMs, maximumBatchMs, batches);
    reports.Add(report);
    if (market.Name == "two-neighborhoods") File.WriteAllText(Path.Combine(output, "final.json"), FoodShadowCodec.Write(state));
}
var first = reports.Single(r => r.Name == "one-small");
var two = reports.Single(r => r.Name == "two-neighborhoods");
var large = reports.Single(r => r.Name == "one-larger");
var unavailable = reports.Single(r => r.Name == "second-unavailable");
var invariants = new Dictionary<string, bool>
{
    ["Second neighborhood serves additional needs"] = two.Fulfilled > first.Fulfilled,
    ["First shop retains its customers when second covers another neighborhood"] = two.ShopMeals["a"] == first.ShopMeals["a"],
    ["Two local shops reach more needs than one larger distant shop in this fixture"] = two.Fulfilled > large.Fulfilled,
    ["Unavailable service makes no sale"] = unavailable.ShopMeals["b"] == 0,
    ["No extra needs or purchasing power created by adding shops"] = reports.All(r => r.Fulfilled + r.Unmet + r.Unresolved == count * days),
    ["Quote work never exceeds two route calls per batch"] = reports.All(r => r.MaximumRouteCallsPerBatch <= 2)
};
// Keep the interactive insight small, while retaining the city-scale source and result separately.
var sampleIds = baselineInitial!.Consumers.Where((_, i) => i < 12 || i >= count / 2 && i < count / 2 + 12).Select(p => p.Id).ToHashSet();
var insight = FoodDemand.CreateObserved(capture.CityKey, "24-resident subset of the synthetic routine replay; " + evidence, 1, days,
    baselineInitial.Consumers.Where(p => sampleIds.Contains(p.Id)), baselineInitial.Businesses,
    baselineInitial.TravelQuotes.Where(q => sampleIds.Contains(q.ConsumerId)), baselineInitial.Intents.Where(i => sampleIds.Contains(i.ConsumerId)));
File.WriteAllText(Path.Combine(output, "insight-input.json"), FoodShadowCodec.Write(insight));
var summary = new { evidence, residents = count, days, purchasedMealOpportunities = count * days,
    actualObservedSpans = capture.Spans.Count, opportunityOrigins = plan.Opportunities.GroupBy(i => i.OriginActivity.ToString()).ToDictionary(g => g.Key, g => g.Count()),
    distinctDailyMealTimes = plan.Opportunities.GroupBy(i => i.ConsumerId).Average(g => g.Select(i => i.DesiredAt % 1440).Distinct().Count()),
    populationReplayMs, opportunityMs, invariants, scenarios = reports,
    limitations = "One purchased meal/day; provisional lunch breaks; hourly service smoothing; walking outings; no home food, wages, operating costs or live native stock/service validation. Timings are offline .NET measurements, not Unity frame-time evidence." };
File.WriteAllText(Path.Combine(output, "report.json"), JsonConvert.SerializeObject(summary, Formatting.Indented));
Console.WriteLine(JsonConvert.SerializeObject(summary, Formatting.Indented));
if (invariants.Any(p => !p.Value)) Environment.ExitCode = 2;

static FoodBusiness Shop(string id, string place, int capacity, int price = 800) => new()
{
    Id = id, PlaceId = place, Name = id == "b" ? "East Corner" : id == "a2" ? "West Corner Annex" : "West Corner",
    MealsPerHour = capacity, ServiceEvidence = "Controlled effective staffed meals/hour, with sufficient stock; not a measured native shop",
    Hours = Enumerable.Range(0, 7).Select(day => new OpeningWindow { Day = day, FromMinute = 420, ToMinute = 1380 }).ToList(),
    Offers = new() { new FoodOffer { ProductId = "meal", PriceCents = price, Units = 50000 } }
};

static CityState Fixture(int count)
{
    var places = new List<Place>();
    foreach (var side in new[] { "a", "b" })
    {
        int x = side == "a" ? 0 : 6000;
        places.Add(new Place { Id = "home-" + side, Name = "Homes " + side, Kind = PlaceKind.Home, Capacity = count, Position = new(x, 0, 0) });
        places.Add(new Place { Id = "work-" + side, Name = "Work " + side, Kind = PlaceKind.Work, Capacity = count, Position = new(x + 500, 0, 0) });
        places.Add(new Place { Id = "cafe-" + side, Name = "Cafe " + side, Kind = PlaceKind.Shop, Capacity = 15, Position = new(x + 250, 0, 0) });
        places.Add(new Place { Id = "park-" + side, Name = "Park " + side, Kind = PlaceKind.Leisure, Capacity = count, Position = new(x + 600, 0, 0) });
    }
    var city = Population.Create(Enumerable.Range(0, count).Select(i => new CitizenIdentity { Id = "resident:" + i.ToString("D4"), Name = "Resident " + i,
        Persona = i % 20 == 0 ? Persona.Retiree : i % 5 == 0 ? Persona.NightOwl : i % 7 == 0 ? Persona.Parent : Persona.Commuter }), places,
        "routine-two-neighborhoods", "0.40.4", 811, 1440);
    for (int i = 0; i < count; i++)
    {
        var p = city.Citizens[i]; string side = i < count / 2 ? "a" : "b";
        p.HomeId = p.PlaceId = "home-" + side; p.DayPlaceId = p.Identity.Persona == Persona.Retiree ? "" : "work-" + side;
        p.ShopId = "cafe-" + side; p.LeisureId = "park-" + side;
        p.Preferences.OwnsCar = false; p.Preferences.AllowTransit = false; p.Preferences.WalkingToleranceMetres = 900;

    }
    return city;
}

sealed class FixtureWalking : ICommuteTimingPlanner
{
    public Route? Plan(Place from, Place to, double at, TravelPreferences preferences)
    {
        double distance = Position.Distance(from.Position, to.Position);
        if (distance > preferences.WalkingToleranceMetres) return null;
        return new Route { Legs = new() { new RouteLeg { From = from.Position, To = to.Position, Mode = TravelMode.Walk, Minutes = Math.Max(.01, distance / 100) } } };
    }
}

record ScenarioResult(string Name, int Fulfilled, int Unmet, int Unresolved, Dictionary<string, int> ShopMeals,
    double MeanOutingMinutes, double MeanQueueMinutes, int RouteCalls, int MaximumRouteCallsPerBatch,
    double QuoteMs, double SimulationMs, double Maximum16EventBatchMs, int Batches);
