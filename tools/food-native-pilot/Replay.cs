using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;

string root = Path.GetFullPath(args.Single()), captured = Path.Combine(root, "probe-output");
var selection = JObject.Parse(File.ReadAllText(Path.Combine(captured, "selection.json")));
var native = JObject.Parse(File.ReadAllText(Path.Combine(captured, "native-reference.json")));
var capture = JsonConvert.DeserializeObject<FoodRoutineCapture>(File.ReadAllText(Path.Combine(captured, "observed-routines.json")))!;
string input = File.ReadAllText(Path.Combine(captured, "controlled-initial.json"));
string key = (string)JObject.Parse(input)["CityKey"]!, target = (string)selection["targetId"]!;
var points = new JArray();
var nativeResult = JObject.Parse(File.ReadAllText(Path.Combine(root, "native-result.json")));
bool completeNativeSweep = nativeResult["points"] is JArray;
object Summary(FoodShadowState s) => new {
    fulfilled = s.Intents.Count(i => i.Status == FoodIntentStatus.Fulfilled),
    declined = s.Intents.Count(i => i.Status == FoodIntentStatus.DeclinedPurchase),
    unmet = s.Intents.Count(i => i.Status == FoodIntentStatus.Unmet),
    unresolved = s.Intents.Count(i => i.Status == FoodIntentStatus.Unresolved) };
foreach (double factor in Enumerable.Range(10, 31).Select(i => i / 20.0))
{
    string? previous = null;
    foreach (int budget in new[] { 1, 128 })
    {
        var state = FoodShadowCodec.Read(input, key);
        foreach (var offer in state.Businesses.Single(b => b.Id == target).Offers)
            offer.PriceCents = (int)Math.Round(offer.PriceCents * factor);
        var engine = new FoodDemand(state);
        while (!engine.AdvanceTo(state.EndsAt, budget)) { }
        FoodDemand.Validate(state);
        var served = state.Intents.Where(i => i.Status == FoodIntentStatus.Fulfilled).ToArray();
        foreach (var shop in state.Businesses)
        {
            var sales = served.Where(i => i.BusinessId == shop.Id).OrderBy(i => i.ServiceEndsAt).ToArray();
            for (int i = 1; i < sales.Length; i++)
                if (sales[i].ServiceEndsAt - sales[i - 1].ServiceEndsAt < 4 - 1e-6) throw new Exception("Service capacity exceeded");
            foreach (var offer in shop.Offers)
                if (offer.Units != 10000 - sales.Count(i => i.ProductId == offer.ProductId)) throw new Exception("Stock conservation failed");
        }
        // A bounded report, not a replacement save format. Preserve the original full input.
        string outcomes = JsonConvert.SerializeObject(state.Intents.Select(i => new {
            i.Id, i.Status, i.BusinessId, i.ProductId, i.SpentCents, i.ArrivalMinute, i.ServiceEndsAt, i.ReturnBy, i.Outcome }));
        if (previous != null && previous != outcomes) throw new Exception("Event budget changes outcomes");
        previous = outcomes;
        if (budget == 1) continue;
        var chosen = served.Where(i => i.BusinessId == target).ToArray();
        if (completeNativeSweep)
        {
            var nativePoint = nativeResult["points"]!.Single(p => (double)p["factor"]! == factor);
            if (chosen.Length != (int)nativePoint["meals"]! || chosen.Sum(i => i.SpentCents) != (int)nativePoint["revenueCents"]!)
                throw new Exception("Complete native sweep differs from offline replay");
        }
        points.Add(JObject.FromObject(new { factor, meals = chosen.Length, revenueCents = chosen.Sum(i => i.SpentCents),
            allShops = Summary(state), targetPrices = state.Businesses.Single(b => b.Id == target).Offers.Select(o => new { o.ProductId, o.PriceCents }),
            eventBudgetIndependent = true, stockConserved = true, throughputRespected = true }));
        if (factor == 1) File.WriteAllText(Path.Combine(root, "offline-baseline-outcomes.json"), outcomes);
    }
}
var original = FoodShadowCodec.Read(input, key);
var strict = FoodShadowCodec.Read(File.ReadAllText(Path.Combine(captured, "strict-initial.json")), key);
var strictEngine = new FoodDemand(strict); while (!strictEngine.AdvanceTo(strict.EndsAt, 128)) { }
var report = new { capture.StartedAt, capture.EndsAt, residents = capture.Residents.Count, spans = capture.Spans.Count,
    opportunities = original.Intents.Count, knownOrigins = original.Intents.Count(i => i.OriginPlaceId.Length > 0),
    routeQuotes = original.TravelQuotes.Count, completeRoundTrips = original.TravelQuotes.Count(q => q.Minutes.HasValue && q.ReturnMinutes.HasValue),
    targetId = target, selection, native, points, strict = Summary(strict), completeNativeSweep,
    evidence = "Offline replay of original three-day PC routines, home districts, native walking quotes and price-acceptance references. 31 prices at event budgets 1 and 128. Separate from actual native purchases. completeNativeSweep states whether a finished in-game price sweep was available for comparison; the original native result/error is retained. Document limits are unchanged." };
File.WriteAllText(Path.Combine(root, "offline-comparison.json"), JsonConvert.SerializeObject(report, Formatting.Indented));
Console.WriteLine($"{points.Count} price points at two event budgets: identical outcomes, conserved stock and bounded service. {original.Intents.Count} opportunities from {capture.Spans.Count} recorded spans.");
