using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;

string root = Path.GetFullPath(args.Single());
var native = JObject.Parse(File.ReadAllText(Path.Combine(root, "native-result.json")));
string target = (string)native["targetId"]!;
string input = File.ReadAllText(Path.Combine(root, "probe-output/controlled-initial.json"));
string key = (string)JObject.Parse(input)["CityKey"]!;
var results = new List<object>();
foreach (var point in (JArray)native["points"]!)
{
    double factor = (double)point["factor"]!;
    string? outcomes = null;
    foreach (int budget in new[] { 1, 128 })
    {
        var state = FoodShadowCodec.Read(input, key);
        foreach (var offer in state.Businesses.Single(b => b.Id == target).Offers) offer.PriceCents = (int)Math.Round(offer.PriceCents * factor);
        var engine = new FoodDemand(state);
        while (!engine.AdvanceTo(state.EndsAt, budget)) { }
        FoodShadowCodec.Write(state);
        var served = state.Intents.Where(i => i.Status == FoodIntentStatus.Fulfilled).ToArray();
        var chosen = served.Where(i => i.BusinessId == target).ToArray();
        if (chosen.Length != (int)point["meals"]! || chosen.Sum(i => i.SpentCents) != (int)point["revenueCents"]!
            || served.Length != (int)point["allShops"]!["fulfilled"]!) throw new Exception("PC replay mismatch at " + factor);
        foreach (var shop in state.Businesses)
        {
            var completions = served.Where(i => i.BusinessId == shop.Id).OrderBy(i => i.ServiceEndsAt).ToArray();
            for (int i = 1; i < completions.Length; i++)
                if (completions[i].ServiceEndsAt - completions[i-1].ServiceEndsAt < 4 - 1e-6) throw new Exception("Throughput exceeded");
            foreach (var offer in shop.Offers)
                if (offer.Units != 10000 - completions.Count(i => i.ProductId == offer.ProductId)) throw new Exception("Stock conservation failed");
        }
        string result = JsonConvert.SerializeObject(state.Intents.Select(i => new { i.Id, i.Status, i.BusinessId, i.ProductId, i.SpentCents, i.ReturnBy }));
        if (outcomes != null && outcomes != result) throw new Exception("Event budget changes outcome");
        outcomes = result;
        if (factor == 1 && budget == 128)
        {
            var pc = FoodShadowCodec.Read(File.ReadAllText(Path.Combine(root, "probe-output/baseline-final.json")), key);
            if (result != JsonConvert.SerializeObject(pc.Intents.Select(i => new { i.Id, i.Status, i.BusinessId, i.ProductId, i.SpentCents, i.ReturnBy }))) throw new Exception("Baseline per-person native replay mismatch");
        }
    }
    results.Add(new { factor, pcCountsAndRevenueMatch = true, eventBudgetIndependent = true, stockConserved = true, throughputRespected = true });
}
File.WriteAllText(Path.Combine(root, "replay-checks.json"), JsonConvert.SerializeObject(results, Formatting.Indented));
Console.WriteLine($"{results.Count} PC price points reproduced at event budgets 1 and 128; baseline per-person outcomes match; stock and 15/hour throughput conserved.");
