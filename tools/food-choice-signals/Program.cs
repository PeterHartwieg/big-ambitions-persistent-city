using System.Diagnostics;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Activity = PersistentCity.Core.Activity;

string output = Path.GetFullPath(args.Length == 1 ? args[0] : "build/food-choice-signals-demo");
if (args.Length > 1) throw new ArgumentException("Usage: [output-directory]");
Directory.CreateDirectory(output);
const int people = 957, days = 7;
var capture = new FoodRoutineCapture { CityKey = "choice-signals-fixture", Seed = 526, StartedAt = 1440, EndsAt = 8 * 1440 };
for (int i = 0; i < people; i++)
{
    string id = "resident:" + i;
    capture.Residents.Add(new FoodRoutineResident { Identity = new CitizenIdentity { Id = id, Name = "Resident " + i }, HomeId = "home", WalkingToleranceMetres = 900 });
    capture.Spans.Add(new FoodActivitySpan { ConsumerId = id, PlaceId = "home", Activity = Activity.Home, From = 1440, Until = 8 * 1440 });
}
var plan = FoodRoutinePlanning.Build(capture, 1, days, 1800, allowOutsideOptions: false);
var scenarios = new[]
{
    (Name: "Equal service and district appeal", QualityA: 50, QualityB: 50, DemandA: 50, DemandB: 50, PriceB: 900),
    (Name: "Better service at the $1 dearer shop", QualityA: 40, QualityB: 90, DemandA: 50, DemandB: 50, PriceB: 900),
    (Name: "Better service but $4 dearer", QualityA: 40, QualityB: 90, DemandA: 50, DemandB: 50, PriceB: 1200),
    (Name: "Higher product appeal in the other district", QualityA: 50, QualityB: 50, DemandA: 20, DemandB: 90, PriceB: 900),
    (Name: "Both signals favor the dearer shop", QualityA: 40, QualityB: 90, DemandA: 20, DemandB: 90, PriceB: 900)
};
var reports = new List<object>();
var counts = new List<(int A, int B)>();
foreach (var s in scenarios)
{
    var shops = new[] { Shop("a", 800, s.QualityA, s.DemandA), Shop("b", s.PriceB, s.QualityB, s.DemandB) };
    var quotes = plan.Opportunities.SelectMany(i => shops.Select(b => new FoodTravelQuote { ConsumerId = i.ConsumerId,
        IntentId = i.Id, OriginPlaceId = i.OriginPlaceId, BusinessId = b.Id, Minutes = 5, ReturnMinutes = 5,
        Evidence = "Controlled equal complete walking estimates; synthetic adjacent district fixture" }));
    var state = FoodDemand.CreateObserved(capture.CityKey,
        "Synthetic service/district choice comparison; same residents, temporary allowances and equal routes. High service throughput isolates choice from queues. No native observation or sales.",
        1, days, plan.Consumers, shops, quotes, plan.Opportunities);
    if (s.Name == "Both signals favor the dearer shop")
    {
        var ids = state.Consumers.Take(24).Select(p => p.Id).ToHashSet();
        var sample = FoodDemand.CreateObserved(state.CityKey, "24-resident insight subset. " + state.Evidence, 1, days,
            state.Consumers.Where(p => ids.Contains(p.Id)), state.Businesses, state.TravelQuotes.Where(q => ids.Contains(q.ConsumerId)),
            state.Intents.Where(i => ids.Contains(i.ConsumerId)));
        File.WriteAllText(Path.Combine(output, "insight-input.json"), FoodShadowCodec.Write(sample));
    }
    var engine = new FoodDemand(state); var timer = Stopwatch.StartNew();
    while (!engine.AdvanceTo(state.EndsAt, 16)) { }
    double elapsed = timer.Elapsed.TotalMilliseconds;
    FoodShadowCodec.Write(state);
    int a = state.Intents.Count(i => i.Status == FoodIntentStatus.Fulfilled && i.BusinessId == "a");
    int b = state.Intents.Count(i => i.Status == FoodIntentStatus.Fulfilled && i.BusinessId == "b");
    counts.Add((a, b));
    reports.Add(new { s.Name, shopA = a, shopB = b, opportunities = state.Intents.Count,
        hypotheticalSpendCents = Money.Sum(state.Intents.Select(i => i.Spent)).ApproximateCents(), simulationMs = elapsed });
}
var checks = new Dictionary<string, bool>
{
    ["Equal signals favor cheaper shop"] = counts[0].B == 0,
    ["Service creates a premium choice for some residents"] = counts[1].B > 0 && counts[1].A > 0,
    ["Service does not justify any price"] = counts[2].B < counts[1].B,
    ["District appeal can change relative product choice"] = counts[3].B > 0 && counts[3].A > 0,
    ["Combined signals attract more customers"] = counts[4].B > Math.Max(counts[1].B, counts[3].B),
    ["No opportunities or buying power added by signals"] = counts.All(c => c.A + c.B == people * days)
};
var report = new { scenarios = reports, checks, note = "Provisional choice weights, not an empirically fitted demand curve. Same-product signals shared within a district do not split identical branches or create extra consumers. Home-food alternatives and optional purchase frequency remain future work." };
string json = JsonConvert.SerializeObject(report, Formatting.Indented);
File.WriteAllText(Path.Combine(output, "report.json"), json); Console.WriteLine(json);
if (checks.Any(c => !c.Value)) Environment.ExitCode = 2;

static FoodBusiness Shop(string id, int price, int quality, int demand) => new()
{
    Id = id, PlaceId = id, Name = id == "a" ? "West Corner" : "East Table", DistrictId = id == "a" ? "West" : "East",
    CustomerServicePercent = quality, CustomerServiceEvidence = "Controlled service rating",
    MealsPerHour = 10000, ServiceEvidence = "Controlled high throughput to isolate choice effects",
    Hours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 0, ToMinute = 1440 }).ToList(),
    Offers = new() { new FoodOffer { ProductId = "meal", Price = Money.FromCents(price), Units = 50000,
        DistrictDemandPercent = demand, DistrictDemandEvidence = "Controlled district product appeal" } }
};
