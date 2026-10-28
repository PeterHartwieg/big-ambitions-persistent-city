using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;

string input = Path.GetFullPath(args[0]), output = Path.GetFullPath(args[1]); Directory.CreateDirectory(output);
var native = JObject.Parse(File.ReadAllText(Path.Combine(input, "native-result.json")));
string target = (string)native["targetId"]!;
var capture = JsonConvert.DeserializeObject<FoodRoutineCapture>(File.ReadAllText(Path.Combine(input, "probe-output/observed-routines.json")))!;
string original = File.ReadAllText(Path.Combine(input, "probe-output/controlled-initial.json"));
var old = FoodShadowCodec.Read(original, capture.CityKey);
var variations = new[] { "Previous common $18", "Individual budgets", "Individual budgets + outside choice" };
var reports = new List<object>();
FoodShadowState? current = null;
foreach (string name in variations)
{
    FoodShadowState initial;
    if (name == variations[0]) initial = FoodShadowCodec.Read(original, capture.CityKey);
    else
    {
        var plan = FoodRoutinePlanning.Build(capture, (int)(old.StartedAt / 1440), 1, allowOutsideOptions: name == variations[2]);
        initial = FoodDemand.CreateObserved(old.CityKey,
            "OFFLINE replay of retained PC routines, routes and market. Original capture has no verified home-district mix, so the new policy uses its explicit neutral individual prior. Controlled original stock/throughput, no native sale or new native execution.",
            (int)(old.StartedAt / 1440), 1, plan.Consumers, old.Businesses, old.TravelQuotes, plan.Opportunities);
    }
    string json = FoodShadowCodec.Write(initial);
    if (name == variations[2]) { File.WriteAllText(Path.Combine(output, "initial.json"), json); current = initial; }
    var points = new List<object>();
    foreach (var p in (JArray)native["points"]!)
    {
        double factor = (double)p["factor"]!; var state = FoodShadowCodec.Read(json, initial.CityKey);
        foreach (var o in state.Businesses.Single(b => b.Id == target).Offers) o.Price = Money.FromCents((int)Math.Round(o.Price.ApproximateCents() * factor));
        Finish(state);
        var served = state.Intents.Where(i => i.Status == FoodIntentStatus.Fulfilled && i.BusinessId == target).ToArray();
        if (name == variations[0] && (served.Length != (int)p["meals"]! || Money.Sum(served.Select(i => i.Spent)) != Money.FromCents((int)p["revenueCents"]!)))
            throw new Exception("Migration changed original PC demand curve");
        if (name == variations[2] && factor == 1) File.WriteAllText(Path.Combine(output, "baseline-final.json"), FoodShadowCodec.Write(state));
        int fulfilled = state.Intents.Count(i => i.Status == FoodIntentStatus.Fulfilled), declined = state.Intents.Count(i => i.Status == FoodIntentStatus.DeclinedPurchase),
            unmet = state.Intents.Count(i => i.Status == FoodIntentStatus.Unmet), unresolved = state.Intents.Count(i => i.Status == FoodIntentStatus.Unresolved);
        if (fulfilled + declined + unmet + unresolved != old.Intents.Count) throw new Exception("Lost opportunity");
        if (Money.Sum(state.Consumers.Select(c => c.DailyAllowance)) != Money.Sum(initial.Consumers.Select(c => c.DailyAllowance))) throw new Exception("Price changed buying power");
        foreach (var b in state.Businesses)
        foreach (var o in b.Offers)
            if (o.Units != 10000 - state.Intents.Count(i => i.Status == FoodIntentStatus.Fulfilled && i.BusinessId == b.Id && i.ProductId == o.ProductId)) throw new Exception("Stock mismatch");
        points.Add(new { factor, priceCents = state.Businesses.Single(b => b.Id == target).Offers[0].Price, meals = served.Length,
            revenueCents = Money.Sum(served.Select(i => i.Spent)).ApproximateCents(), fulfilled, declined, unmet, unresolved });
    }
    reports.Add(new { name, points, allowances = initial.Consumers.Select(c => c.DailyAllowance),
        homeDistrictEvidence = name == variations[0] ? "Original fixed control" : "Unknown in retained PC capture; neutral individual prior" });
}
// Separate distribution sensitivity: same IDs/seed, controlled home-district composition.
var districts = new List<object>();
foreach (var sample in new[] { (Name: "Working-heavy district", Working: 80, Middle: 15, Upper: 5), (Name: "Affluent district", Working: 15, Middle: 45, Upper: 40) })
{
    var c = JsonConvert.DeserializeObject<FoodRoutineCapture>(JsonConvert.SerializeObject(capture))!;
    foreach (var r in c.Residents) r.HomeDistrictId = "controlled-home";
    c.HomeDistrictMixes = new() { new FoodDistrictBudgetMix { DistrictId = "controlled-home", WorkingPercent = sample.Working,
        MiddlePercent = sample.Middle, UpperPercent = sample.Upper, Evidence = "Controlled district sensitivity; not measured PC home data" } };
    var plan = FoodRoutinePlanning.Build(c, (int)(old.StartedAt / 1440), 1);
    districts.Add(new { name = sample.Name, sample.Working, sample.Middle, sample.Upper, allowances = plan.Consumers.Select(p => p.DailyAllowance.ApproximateCents()), mean = plan.Consumers.Average(p => p.DailyAllowance.ApproximateCents()) });
}
var low = current!.Consumers.Min(c => c.DailyAllowance); var high = current.Consumers.Max(c => c.DailyAllowance);
File.WriteAllText(Path.Combine(output, "report.json"), JsonConvert.SerializeObject(new { target = (string)native["selection"]!["name"]!, scenarios = reports, districtSensitivity = districts,
    retainedPcPricePointsUnchanged = 31, individualBudgetRangeCents = new[] { low, high },
    note = "New policy is offline only. Home district is unknown in the original PC capture. District examples are separate controlled distributions, not measurements. No shop gets guaranteed customers; no household food or costs are booked." }, Formatting.Indented));
Console.WriteLine($"31 retained PC price points unchanged after schema migration; 62 new counterfactual points conserve opportunities, stock and buying power. Individual budgets ${low.ApproximateCents()/100:F2}–${high.ApproximateCents()/100:F2}; district sensitivity kept separate.");

static void Finish(FoodShadowState state) { var e = new FoodDemand(state); while (!e.AdvanceTo(state.EndsAt, 128)) { } FoodShadowCodec.Write(state); }
