using System.Text.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;

// Positional arguments: output directory, optional food-shadow input document, expected city key.
string output = Path.GetFullPath(args.Length > 0 ? args[0] : "build/food-demand-demo");
if (args.Length == 2 || args.Length > 3) throw new ArgumentException("Usage: [output-directory] [input.json expected-city-key]");
if (args.Length == 3)
{
    string inputPath = Path.GetFullPath(args[1]);
    string[] outputs = { "initial.json", "report.json", "baseline.json", "higher-prices.json", "shop-closed.json", "no-stock.json", "index.html" };
    if (outputs.Any(name => string.Equals(inputPath, Path.Combine(output, name), StringComparison.OrdinalIgnoreCase)))
        throw new ArgumentException("Keep the source experiment outside the comparison's output files.");
}
FoodShadowState initial = args.Length == 3 ? FoodShadowCodec.Read(File.ReadAllText(args[1]), args[2]) : Demo();
if (initial.Minute != initial.StartedAt || initial.Intents.Any(i => i.Status != FoodIntentStatus.Planned))
    throw new ArgumentException("Comparisons require an unstarted experiment. Resume saved intentions through FoodDemand directly.");
string original = FoodShadowCodec.Write(initial);
var reports = new List<object>();
var snapshots = new Dictionary<string, string>();
string target = initial.Businesses.OrderBy(b => b.Id, StringComparer.Ordinal).LastOrDefault()?.Id
    ?? throw new ArgumentException("Include at least one business for comparison.");
foreach (string variant in new[] { "Baseline", "Higher prices", "Shop closed", "No stock" })
{
    var state = FoodShadowCodec.Read(original, initial.CityKey);
    var engine = new FoodDemand(state);
    var shop = state.Businesses.Single(b => b.Id == target);
    if (variant == "Higher prices") foreach (var offer in shop.Offers) engine.SetPrice(target, offer.ProductId, Math.Min(1000000, offer.PriceCents + 400));
    if (variant == "Shop closed") engine.Close(target);
    if (variant == "No stock") foreach (var business in state.Businesses)
        foreach (var offer in business.Offers) engine.SetStock(business.Id, offer.ProductId, 0);
    while (!engine.AdvanceTo(state.EndsAt)) { }
    string serialized = FoodShadowCodec.Write(state);
    var restored = FoodShadowCodec.Read(serialized, initial.CityKey);
    new FoodDemand(restored).AdvanceTo(restored.EndsAt);
    if (FoodShadowCodec.Write(restored) != serialized) throw new InvalidOperationException("Completed experiment replay changed state.");
    snapshots.Add(variant, serialized);
    reports.Add(new
    {
        name = variant, changedShop = shop.Name,
        fulfilled = state.Intents.Count(i => i.Status == FoodIntentStatus.Fulfilled),
        unmet = state.Intents.Count(i => i.Status == FoodIntentStatus.Unmet),
        unresolved = state.Intents.Count(i => i.Status == FoodIntentStatus.Unresolved),
        declined = state.Intents.Count(i => i.Status == FoodIntentStatus.DeclinedPurchase),
        spendingCents = state.Intents.Sum(i => i.SpentCents),
        businesses = state.Businesses.Select(b => new { id = b.Id, name = b.Name,
            meals = state.Intents.Count(i => i.BusinessId == b.Id && i.Status == FoodIntentStatus.Fulfilled),
            spendingCents = state.Intents.Where(i => i.BusinessId == b.Id).Sum(i => i.SpentCents) }),
        people = state.Intents.Select(i => new { id = i.Id, person = state.Consumers.Single(c => c.Id == i.ConsumerId).Name,
            day = i.Day, business = state.Businesses.FirstOrDefault(b => b.Id == i.BusinessId)?.Name ?? "None",
            origin = i.OriginPlaceId.Length > 0 ? i.OriginPlaceId : state.Consumers.Single(c => c.Id == i.ConsumerId).OriginPlaceId,
            activity = i.OriginActivity.ToString(), context = i.ContextEvidence, requestedAt = i.RequestedAt,
            deadline = i.Deadline, returnBy = i.ReturnBy, eatingMinutes = i.EatingMinutes, maximumQueueMinutes = i.MaximumQueueMinutes,
            status = i.Status.ToString(), spendingCents = i.SpentCents, outcome = i.Outcome,
            allowanceCents = state.Consumers.Single(c => c.Id == i.ConsumerId).DailyAllowanceCents,
            homeDistrict = state.Consumers.Single(c => c.Id == i.ConsumerId).HomeDistrictId,
            budgetEvidence = state.Consumers.Single(c => c.Id == i.ConsumerId).BudgetEvidence,
            outsideOption = i.OutsideOption.ToString(), outsideScoreCents = i.OutsideOptionScoreCents,
            outsideEvidence = i.OutsideOptionEvidence,
            choices = i.Choices.Select(c => new { business = state.Businesses.Single(b => b.Id == c.BusinessId).Name,
                district = state.Businesses.Single(b => b.Id == c.BusinessId).DistrictId,
                customerServicePercent = c.CustomerServicePercent, districtDemandPercent = c.DistrictDemandPercent,
                serviceQualityAdjustmentCents = c.ServiceQualityAdjustmentCents, districtAppealAdjustmentCents = c.DistrictAppealAdjustmentCents,
                product = c.ProductId, priceCents = c.PriceCents, travelMinutes = c.TravelMinutes,
                waitMinutes = c.AdmissionMinute.HasValue && c.TravelMinutes.HasValue ? c.AdmissionMinute - i.RequestedAt - c.TravelMinutes : null,
                returnMinutes = c.ReturnMinutes, serviceMinutes = c.ServiceEndsAt - c.AdmissionMinute,
                scoreCents = c.ScoreCents, reason = c.Reason }) })
    });
}
var document = new { evidence = initial.Evidence, scope = initial.Scope, citizens = initial.Consumers.Count,
    days = (initial.EndsAt - initial.StartedAt) / 1440, nativeSalesChanged = 0,
    routineMode = initial.Mode == FoodExperimentMode.ObservedRoutines,
    note = initial.Mode == FoodExperimentMode.ObservedRoutines
        ? "Counterfactual outings from observed routine stops. Customer service and district product demand influence choice. Provisional personal allowances can use home-district composition. A home/packed-food preference can decline purchase; it does not verify ingredients, household costs or a consumed meal. Missing evidence remains unresolved. No wages or automatic restocking. These trips do not alter the observed population."
        : "Fixed daily reference. Temporary allowances, supplied route estimates and stock in a detached market. No wages, service capacity or automatic restocking. Native sales are not attributed to these residents.",
    changedShop = initial.Businesses.Single(b => b.Id == target).Name, scenarios = reports };
Directory.CreateDirectory(output);
File.WriteAllText(Path.Combine(output, "initial.json"), original);
File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true }));
foreach (var snapshot in snapshots) File.WriteAllText(Path.Combine(output, snapshot.Key.Replace(' ', '-').ToLowerInvariant() + ".json"), snapshot.Value);
// The default encoder escapes '<', so imported names cannot break out of the script element.
string html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "report.html"))
    .Replace("/*REPORT_DATA*/null", JsonSerializer.Serialize(document));
File.WriteAllText(Path.Combine(output, "index.html"), html);
Console.WriteLine(JsonSerializer.Serialize(new { initial.Evidence, Consumers = initial.Consumers.Count,
    Days = (initial.EndsAt - initial.StartedAt) / 1440, Comparisons = reports.Count, NativeSalesChanged = 0 }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine("Comparison and saved intentions: " + output);

static FoodShadowState Demo()
{
    var people = Enumerable.Range(0, 24).Select(i => new FoodConsumer
    {
        Id = "sample:" + i.ToString("D2"), Name = "Resident " + (i + 1), OriginPlaceId = "sample:home:" + i,
        MealMinuteOfDay = 720 + i % 6 * 5, DailyAllowanceCents = i % 8 == 0 ? 450 : 1200,
        ValueOfTimeCentsPerMinute = 3 + i % 4 * 4, PreferredProductId = i % 3 == 0 ? "meal-near" : "meal-market"
    }).ToArray();
    var shops = new[]
    {
        new FoodBusiness { Id = "sample:1-corner", Name = "Corner Kitchen", PlaceId = "sample:corner-door",
            Offers = new() { new FoodOffer { ProductId = "meal-near", PriceCents = 650, Units = 60 } } },
        new FoodBusiness { Id = "sample:2-market", Name = "Market Deli", PlaceId = "sample:market-door",
            Offers = new() { new FoodOffer { ProductId = "meal-market", PriceCents = 500, Units = 60 } } }
    };
    foreach (var b in shops) b.Hours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 600, ToMinute = 1200 }).ToList();
    var routes = people.SelectMany((c, i) => shops.Select((b, j) => new FoodTravelQuote
    { ConsumerId = c.Id, BusinessId = b.Id, Minutes = j == 0 ? 4 + i % 5 * 2 : 10 + i % 7 * 3,
        Evidence = "Synthetic route-duration fixture; not measured in Big Ambitions" }));
    return FoodDemand.Create("synthetic-food-demo", "Synthetic demonstration: 24 fictional residents and two fictional shops. No live city was read.", 1, 2, people, shops, routes);
}
