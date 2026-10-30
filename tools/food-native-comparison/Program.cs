using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;

if (args.Length < 2 || args.Length > 3) throw new ArgumentException("Usage: native-capture.json output-directory [target-shop-index]");
var native = JObject.Parse(File.ReadAllText(args[0]));
if (native["error"] != null || (bool?)native["nativeMarketUnchanged"] != true) throw new InvalidDataException("Native capture failed or did not preserve market fields.");
var markets = (JArray)native["markets"]!;
if (markets.Count < 2) throw new InvalidDataException("At least two observed competing businesses are required.");
var shops = markets.Select(m => m["business"]!.ToObject<FoodBusiness>()!).ToArray();
if (shops.Any(b => b.Offers.Count == 0 || b.Hours.Count == 0)) throw new InvalidDataException("Selected business lacks observed offers or opening hours.");
// These are controlled finite supplies. They do not claim to measure native inventory.
foreach (var offer in shops.SelectMany(b => b.Offers)) offer.Units = 100000;
int targetIndex = args.Length == 3 ? int.Parse(args[2]) : 0;
if (targetIndex < 0 || targetIndex >= shops.Length) throw new ArgumentOutOfRangeException(nameof(targetIndex));
string target = shops[targetIndex].Id;
var productIds = shops.SelectMany(s => s.Offers).Select(o => o.ProductId).Distinct().Order().ToArray();
var factors = new[] { .5, .75, 1, 1.1, 1.25, 1.5, 2, 3 };
var cases = factors.Select(f => ($"price-{f:0.##}", f)).Concat(new[] { ("closed", 1.0), ("target-stockout", 1.0), ("all-stockout", 1.0), ("double-travel", 1.0) }).ToArray();
var runs = new List<Run>();
var output = Path.GetFullPath(args[1]); Directory.CreateDirectory(output);
string evidence = "Observed native food prices and schedules; 240 synthetic residents, controlled allowances/preferences/routes/supply. Not actual city trips or sales.";
for (int seed = 1; seed <= 12; seed++)
{
    var random = new Random(8000 + seed);
    var people = Enumerable.Range(0, 240).Select(i => new FoodConsumer
    {
        Id = "comparison:" + i.ToString("D3"), Name = "Resident " + (i + 1), OriginPlaceId = "controlled-home:" + (i / 3),
        MealMinuteOfDay = new[] { 480, 720, 1080 }[random.Next(3)] + random.Next(120), MealWindowMinutes = 120,
        DailyAllowanceCents = 400 + random.Next(1601), ValueOfTimeCentsPerMinute = 2 + random.Next(19),
        PreferredProductId = productIds[random.Next(productIds.Length)], PreferenceBonusCents = random.Next(301)
    }).ToArray();
    var routes = people.SelectMany(p => shops.Select(b => new FoodTravelQuote { ConsumerId = p.Id, BusinessId = b.Id,
        Minutes = 3 + random.Next(48), Evidence = "Controlled 3–50 minute travel quote; not native navigation or measured movement" })).ToArray();
    var initial = FoodDemand.Create("native-food-comparison", evidence, 1, 7, people, shops, routes);
    var original = FoodShadowCodec.Write(initial);
    foreach (var (name, factor) in cases)
    {
        var state = FoodShadowCodec.Read(original, initial.CityKey);
        var engine = new FoodDemand(state);
        var changed = state.Businesses.Single(b => b.Id == target);
        if (name.StartsWith("price-")) foreach (var offer in changed.Offers) engine.SetPrice(target, offer.ProductId, (int)Math.Round((double)offer.PriceCents * factor));
        if (name == "closed") engine.Close(target);
        if (name == "target-stockout") foreach (var offer in changed.Offers) engine.SetStock(target, offer.ProductId, 0);
        if (name == "all-stockout") foreach (var b in state.Businesses) foreach (var o in b.Offers) engine.SetStock(b.Id, o.ProductId, 0);
        if (name == "double-travel") foreach (var q in state.TravelQuotes.Where(q => q.BusinessId == target)) q.Minutes *= 2;
        while (!engine.AdvanceTo(state.EndsAt)) { }
        var fulfilled = state.Intents.Where(i => i.Status == FoodIntentStatus.Fulfilled).ToArray();
        var groups = fulfilled.GroupBy(i => i.BusinessId).ToArray();
        double entropy = groups.Sum(g => { double p = (double)g.Count() / Math.Max(1, fulfilled.Length); return -p * Math.Log(p); });
        var repeatGroups = fulfilled.GroupBy(i => i.ConsumerId).Where(g => g.Count() > 1).ToArray();
        runs.Add(new Run(seed, name, factor, fulfilled.Length, fulfilled.Count(i => i.BusinessId == target),
            state.Intents.Count(i => i.Status == FoodIntentStatus.Unmet), state.Intents.Count(i => i.Status == FoodIntentStatus.Unresolved),
            fulfilled.Sum(i => i.SpentCents), groups.Length == 0 ? 0 : Math.Exp(entropy),
            repeatGroups.Length == 0 ? 0 : 100.0 * repeatGroups.Count(g => g.Select(i => i.BusinessId).Distinct().Count() > 1) / repeatGroups.Length,
            state.Businesses.ToDictionary(b => b.Name, b => fulfilled.Count(i => i.BusinessId == b.Id))));
        if (seed == 1 && name == "price-1")
        {
            File.WriteAllText(Path.Combine(output, "baseline-initial.json"), original);
            File.WriteAllText(Path.Combine(output, "baseline-final.json"), FoodShadowCodec.Write(state));
            // Constant opening hours separate changing weekdays from inherent day-to-day variety.
            var same = FoodShadowCodec.Read(original, initial.CityKey);
            foreach (var b in same.Businesses) b.Hours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 0, ToMinute = 1440 }).ToList();
            var stable = new FoodDemand(same); while (!stable.AdvanceTo(same.EndsAt)) { }
            var switched = same.Intents.Where(i => i.Status == FoodIntentStatus.Fulfilled).GroupBy(i => i.ConsumerId)
                .Count(g => g.Select(i => i.BusinessId).Distinct().Count() > 1);
            File.WriteAllText(Path.Combine(output, "constant-context.json"), JsonConvert.SerializeObject(new { seed, days = 7, consumers = 240, residentsSwitchingShop = switched, explanation = "Constant market, allowance, preferences, origin and meal time; no learned habit or daily appetite variation exists in current model." }, Formatting.Indented));
        }
    }
}
var invariants = new Dictionary<string, bool>
{
    ["Higher target prices never increase its fulfilled meals in any cohort"] = Enumerable.Range(1, 12).All(s => runs.Where(r => r.Seed == s && r.Scenario.StartsWith("price-")).OrderBy(r => r.Factor).Select(r => r.Target).Zip(runs.Where(r => r.Seed == s && r.Scenario.StartsWith("price-")).OrderBy(r => r.Factor).Select(r => r.Target).Skip(1), (a, b) => b <= a).All(x => x)),
    ["Closed shop receives no meals"] = runs.Where(r => r.Scenario == "closed").All(r => r.Target == 0),
    ["Target stockout receives no meals"] = runs.Where(r => r.Scenario == "target-stockout").All(r => r.Target == 0),
    ["All stockout leaves all needs unmet"] = runs.Where(r => r.Scenario == "all-stockout").All(r => r.Fulfilled == 0 && r.Unmet == 1680),
    ["Longer target travel never increases its meals"] = Enumerable.Range(1, 12).All(s => runs.Single(r => r.Seed == s && r.Scenario == "double-travel").Target <= runs.Single(r => r.Seed == s && r.Scenario == "price-1").Target),
    ["Native quote price matches detached requested price"] = markets.SelectMany(m => m["priceCurves"]!).SelectMany(c => c["classes"]!).SelectMany(c => c["points"]!).All(p => Math.Abs((double)p["requestedPrice"]! - (double)p["nativePrice"]!) < .011),
    ["Native price acceptance never improves as price increases"] = markets.SelectMany(m => m["priceCurves"]!).SelectMany(c => c["classes"]!).All(c => { bool rejected = false; foreach (var p in c["points"]!) { if ((bool)p["nativePaid"]! && rejected) return false; if (!(bool)p["nativePaid"]!) rejected = true; } return true; }),
    ["Native temporarily closed businesses have no open hours"] = markets.All(m => (int)m["nativeClosedHoursOpen"]! == 0),
    ["Native missing sellable producer never completes a sale"] = markets.SelectMany(m => m["priceCurves"]!).SelectMany(c => c["classes"]!).SelectMany(c => c["points"]!).All(p => !(bool)p["nativePaidWithoutProducer"]!),
    ["Native unavailable service never completes a sale"] = markets.SelectMany(m => m["priceCurves"]!).SelectMany(c => c["classes"]!).SelectMany(c => c["points"]!).All(p => !(bool)p["nativePaidWithoutService"]!)
};
var summaries = cases.Select(c => new { scenario = c.Item1, factor = c.Item2, fulfilled = runs.Where(r => r.Scenario == c.Item1).Average(r => r.Fulfilled),
    targetMeals = runs.Where(r => r.Scenario == c.Item1).Average(r => r.Target), unmet = runs.Where(r => r.Scenario == c.Item1).Average(r => r.Unmet),
    effectiveShops = runs.Where(r => r.Scenario == c.Item1).Average(r => r.EffectiveShops), switchingResidentsPercent = runs.Where(r => r.Scenario == c.Item1).Average(r => r.SwitchingPercent) }).ToArray();
File.WriteAllText(Path.Combine(output, "comparison.json"), JsonConvert.SerializeObject(new { evidence, targetShop = shops[targetIndex].Name, cohorts = 12, residentsPerCohort = 240, days = 7, totalSimulatedIntentions = runs.Count * 1680, invariants, summaries, runs }, Formatting.Indented));
Console.WriteLine(JsonConvert.SerializeObject(new { targetShop = shops[targetIndex].Name, invariants, summaries }, Formatting.Indented));
if (invariants.Any(p => !p.Value)) Environment.ExitCode = 2;
record Run(int Seed, string Scenario, double Factor, int Fulfilled, int Target, int Unmet, int Unresolved, decimal SpendingCents, double EffectiveShops, double SwitchingPercent, Dictionary<string, int> ShopMeals);
