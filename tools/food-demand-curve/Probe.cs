using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BAModAPI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

[assembly: RegisterModClass(typeof(FoodDemandCurveProbe.Entry))]
namespace FoodDemandCurveProbe
{
    [ModEntryOnCityLoad] public sealed class Entry : IModBigAmbitions
    {
        private GameObject? host;
        public string[] RelativeAssetBundlePaths => Array.Empty<string>();
        public Task OnLoadAsync(ModContext context)
        {
            if (Probe.Allowed()) { host = new GameObject("Food routine diagnostic"); host.AddComponent<Probe>().Root = context.ModRootPath; }
            return Task.CompletedTask;
        }
        public Task OnUnloadAsync() { if (host != null) Object.Destroy(host); host = null; return Task.CompletedTask; }
    }

    public sealed class Probe : MonoBehaviour
    {
        public string Root = "";
        private string last = "", pendingId = "", marketBefore = "", populationBefore = "";
        private float poll;
        private FoodRoutineRecorder? recorder;
        private FoodRoutineCapture? capture;
        private FoodRoutinePlan? plan;
        private FoodCatalogueObservation? market;
        private FoodRouteBatch? batch;
        private FoodDemand? strict, controlled;
        private FoodShadowState? initial;
        private string targetId = "";
        private int pointIndex;
        private readonly JArray points = new JArray();
        private readonly double[] factors = Enumerable.Range(10, 31).Select(i => i / 20.0).ToArray();
        private JObject? selection;
        private JObject? native;
        private double maximumRouteBatchMs;
        private static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        public static bool Allowed() => Environment.GetCommandLineArgs().Contains("-agent-tests")
            && Application.persistentDataPath.Replace('\\', '/').EndsWith("/Hovgaard Games/Big Ambitions Agent Tests", StringComparison.Ordinal);
        private void OnDestroy() { recorder?.Dispose(); }
        private PopulationRuntime Runtime => Object.FindObjectOfType<PopulationRuntime>() ?? throw new InvalidOperationException("Population runtime absent.");
        private CitySimulator Simulation => (CitySimulator?)typeof(PopulationRuntime).GetField("simulation", Fields)!.GetValue(Runtime)
            ?? throw new InvalidOperationException("Population not ready.");
        private void Update()
        {
            if (!Allowed() || SaveGameManager.Current?.SaveGameName?.StartsWith("FT", StringComparison.Ordinal) != true) return;
            try
            {
                if (pendingId.Length > 0) { Continue(); return; }
                if (Time.unscaledTime < poll) return;
                poll = Time.unscaledTime + .25f;
                string path = Path.Combine(Root, "request.json");
                if (!File.Exists(path) || new FileInfo(path).Length > 2048) return;
                string text = File.ReadAllText(path); if (text == last) return; last = text;
                var request = JObject.Parse(text); string id = (string?)request["id"] ?? "";
                if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-zA-Z0-9_-]{1,60}$")) return;
                pendingId = id;
                string op = (string?)request["op"] ?? "";
                if (op == "status") { Respond(new { minute = Simulation.State.Minute, residents = Simulation.State.Citizens.Count, observing = recorder != null }); return; }
                if (op == "fast-forward" || op == "normal")
                {
                    var speed = Object.FindObjectOfType<GameSpeedController>() ?? throw new InvalidOperationException("Native speed control absent.");
                    speed.isFastForwarding = op == "fast-forward";
                    speed.Reset();
                    Respond(new { nativeFastForward = speed.isFastForwarding });
                    return;
                }
                if (op == "begin")
                {
                    if (recorder != null) throw new InvalidOperationException("Already observing.");
                    string before = SaveCodec.Write(Simulation.State);
                    recorder = Runtime.ObserveFoodRoutines();
                    Respond(new { minute = Simulation.State.Minute, residents = Simulation.State.Citizens.Count,
                        populationUnchanged = before == SaveCodec.Write(Simulation.State), assembly = typeof(FoodDemand).Assembly.FullName });
                    return;
                }
                if (op != "finish" || recorder == null) throw new InvalidOperationException("Use status, begin, then finish.");
                populationBefore = SaveCodec.Write(Simulation.State); marketBefore = Fingerprint();
                capture = recorder.Finish(); recorder = null;
                int day = (int)Math.Ceiling(capture.StartedAt / 1440);
                plan = FoodRoutinePlanning.Build(capture, day, 1);
                string[] products = { "ba:itemname_croissant", "ba:itemname_burger", "ba:itemname_hotdog", "ba:itemname_pizza", "ba:itemname_salad" };
                var places = Simulation.State.Places.ToDictionary(p => p.Id);
                var origins = plan.Opportunities.Where(i => places.ContainsKey(i.OriginPlaceId)).Select(i => places[i.OriginPlaceId]).ToArray();
                var eligible = CityManager.Instance.cityBuildingControllers.Where(c => c != null && c.buildingRegistration != null)
                    .Select(c => c.buildingRegistration).Where(r => r.HasEstablishedBusiness && !r.AvailableForRent && r.retailPrices != null
                        && r.cachedAvailableProducts != null && r.retailPrices.Any(p => products.Contains(p.itemName) && r.cachedAvailableProducts.Contains(p.itemName)))
                    .Where(r => places.ContainsKey(PlaceId(r))).ToArray();
                var ranked = eligible.Select(r => new { Reg = r, Place = places[PlaceId(r)],
                    Nearby = origins.Count(o => Position.Distance(o.Position, places[PlaceId(r)].Position) <= 40),
                    Weight = origins.Sum(o => 1.0 / (1 + Math.Pow(Position.Distance(o.Position, places[PlaceId(r)].Position) / 25, 2))) })
                    .OrderByDescending(x => x.Nearby).ThenByDescending(x => x.Weight).ThenBy(x => PlaceId(x.Reg), StringComparer.Ordinal).ToArray();
                var small = ranked.Where(x => x.Reg.customerCapacity <= 30 && x.Reg.customerCapacity > 0).ToArray();
                var target = (small.Length > 0 ? small : ranked).First();
                var selected = eligible.OrderBy(r => Position.Distance(places[PlaceId(r)].Position, target.Place.Position))
                    .ThenBy(r => PlaceId(r), StringComparer.Ordinal).Take(8).Select(PlaceId).ToArray();
                market = FoodBusinessCatalogue.Read(selected, products);
                targetId = market.Businesses.Single(b => b.PlaceId == PlaceId(target.Reg)).Id;
                selection = JObject.FromObject(new { rule = "Small native food premises (capacity <=30), greatest count of recorded meal origins within 40 world metres; deterministic geometric selection before prices are swept. Target and seven nearest food competitors.",
                    targetId, name = target.Reg.BusinessName, address = PlaceId(target.Reg), nominalNativeCapacity = target.Reg.customerCapacity,
                    target.Nearby, target.Weight, eligible = ranked.Select(x => new { place = PlaceId(x.Reg), name = x.Reg.BusinessName, capacity = x.Reg.customerCapacity, x.Nearby, x.Weight }),
                    shops = market.Businesses });
                native = NativeReference(target.Reg);
                File.WriteAllText(Path.Combine(Root, "selection.json"), selection.ToString());
                File.WriteAllText(Path.Combine(Root, "native-reference.json"), native.ToString());
                batch = Runtime.QuoteFoodRoutes(capture, plan, market.Businesses, 8);
                File.WriteAllText(Path.Combine(Root, "observed-routines.json"), JsonConvert.SerializeObject(capture));
                File.WriteAllText(Path.Combine(Root, "observed-market.json"), JsonConvert.SerializeObject(market, Formatting.Indented));
            }
            catch (Exception e) { Respond(new { error = e.ToString() }); }
        }

        private void Continue()
        {
            if (batch == null || capture == null || plan == null || market == null) throw new InvalidOperationException("Missing quote work.");
            if (!batch.Complete)
            {
                long start = Stopwatch.GetTimestamp(); batch.Step(2);
                maximumRouteBatchMs = Math.Max(maximumRouteBatchMs, (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
                return;
            }
            if (initial == null)
            {
                int day = (int)Math.Ceiling(capture.StartedAt / 1440);
                initial = FoodDemand.CreateObserved(capture.CityKey,
                    "PC native population activity observation, shop prices/hours, service/district signals and native walking estimates. Stock/staffed throughput unknown.",
                    day, 1, plan.Consumers, market.Businesses, batch.Quotes, plan.Opportunities);
                File.WriteAllText(Path.Combine(Root, "strict-initial.json"), FoodShadowCodec.Write(initial));
                strict = new FoodDemand(FoodShadowCodec.Read(FoodShadowCodec.Write(initial), initial.CityKey));
                foreach (var shop in initial.Businesses)
                {
                    shop.MealsPerHour = 15; shop.ServiceEvidence = "Controlled 15 meals/hour counterfactual, not measured staffing";
                    foreach (var offer in shop.Offers) offer.Units = 10000;
                }
                initial.Evidence = "PC native activities, prices/hours, service/district signals and complete native walking quotes. Controlled 15 meals/hour per shop and 10000 hypothetical units per offer. Same one-day cohort for every price. No booked sales.";
                File.WriteAllText(Path.Combine(Root, "controlled-initial.json"), FoodShadowCodec.Write(initial));
                return;
            }
            if (!strict!.AdvanceTo(strict.State.EndsAt, 32)) return;
            if (pointIndex < factors.Length)
            {
                if (controlled == null)
                {
                    var state = FoodShadowCodec.Read(FoodShadowCodec.Write(initial), initial.CityKey);
                    foreach (var offer in state.Businesses.Single(b => b.Id == targetId).Offers)
                        offer.Price = Money.FromCents((int)Math.Round(offer.Price.ApproximateCents() * factors[pointIndex]));
                    controlled = new FoodDemand(state); return;
                }
                if (!controlled.AdvanceTo(controlled.State.EndsAt, 32)) return;
                var chosen = controlled.State.Intents.Where(i => i.Status == FoodIntentStatus.Fulfilled && i.BusinessId == targetId).ToArray();
                points.Add(JObject.FromObject(new { factor = factors[pointIndex], meals = chosen.Length,
                    revenueCents = Money.Sum(chosen.Select(i => i.Spent)).ApproximateCents(), allShops = Summary(controlled.State),
                    targetPrices = controlled.State.Businesses.Single(b => b.Id == targetId).Offers.Select(o => new { o.ProductId, o.Price }),
                    targetReasons = controlled.State.Intents.SelectMany(i => i.Choices).Where(c => c.BusinessId == targetId).GroupBy(c => c.Reason).ToDictionary(g => g.Key, g => g.Count()) }));
                if (Math.Abs(factors[pointIndex] - 1) < .0001) File.WriteAllText(Path.Combine(Root, "baseline-final.json"), FoodShadowCodec.Write(controlled.State));
                pointIndex++; controlled = null; return;
            }
            bool marketUnchanged = marketBefore == Fingerprint();
            bool populationUnchanged = populationBefore == SaveCodec.Write(Simulation.State);
            if (!marketUnchanged || !populationUnchanged) throw new InvalidOperationException("Paused population/market changed during diagnostic replay.");
            File.WriteAllText(Path.Combine(Root, "curve.json"), points.ToString());
            Respond(new { capture.StartedAt, capture.EndsAt, residents = capture.Residents.Count, spans = capture.Spans.Count,
                origins = plan.Opportunities.GroupBy(i => i.OriginActivity).ToDictionary(g => g.Key.ToString(), g => g.Count()),
                knownOrigins = plan.Opportunities.Count(i => i.OriginPlaceId.Length > 0), targetId, selection, native, points,
                batch.RouteCalls, batch.CandidateCount, completeRoundTrips = batch.Quotes.Count(q => q.Minutes.HasValue && q.ReturnMinutes.HasValue), maximumRouteBatchMs,
                strict = Summary(strict.State), marketUnchanged, populationUnchanged,
                evidence = "PC execution: one full day of resident activities; paused read-only shadow price sweep, 31 menu prices 50%-200%. Controlled throughput/stock; not observed sales." });
        }
        private static string PlaceId(BuildingRegistration r) => "address:" + Uri.EscapeDataString(r.StreetName) + ":" + r.StreetNumber.ToString(CultureInfo.InvariantCulture) + ":shop";
        private JObject NativeReference(BuildingRegistration reg)
        {
            var saved = UnityEngine.Random.state;
            try
            {
                var curves = new JArray();
                foreach (var p in reg.retailPrices.Where(p => reg.cachedAvailableProducts.Contains(p.itemName) && new[] { "ba:itemname_croissant", "ba:itemname_burger", "ba:itemname_hotdog", "ba:itemname_pizza", "ba:itemname_salad" }.Contains(p.itemName)))
                {
                    var classes = new JArray();
                    foreach (AI.Citizens.SocialClass sc in Enum.GetValues(typeof(AI.Citizens.SocialClass)))
                    {
                        var citizen = new AI.Citizens.CitizenData { Neighbourhood = reg.Neighborhood, SocialClass = sc };
                        classes.Add(JObject.FromObject(new { socialClass = sc.ToString(), points = factors.Select(f => new { factor = f, price = Math.Round(p.price * f, 2), accepts = citizen.IsPriceAcceptable(p.itemName, (float)Math.Round(p.price * f, 2)) }).ToArray() }));
                    }
                    curves.Add(new JObject { ["product"] = p.itemName, ["basePrice"] = p.price, ["classes"] = classes });
                }
                var calc = new AI.Customers.CustomerEntries.CustomerEntriesCalculatorRetail(); calc.Init(reg);
                float initial = calc.GetInitialCustomers();
                var hourly = Enumerable.Range(0, 24).Select(h => new { hour = h, open = Helpers.BusinessHelper.IsBusinessOpen(reg, h), potential = calc.GetCustomersByHour(h, TimeHelper.GetDayOfWeek(), initial) }).ToArray();
                var n = NeighborhoodHelper.GetData(reg.Neighborhood);
                return JObject.FromObject(new { curves, hourly, classShares = new { working = n.workingClassPercentage, middle = n.middleClassPercentage, upper = n.upperClassClassPercentage },
                    evidence = "Native price-acceptance methods on detached customer records; hourly potential calculator. Neither is booked or realized sales." });
            }
            finally { UnityEngine.Random.state = saved; }
        }
        private static object Summary(FoodShadowState state) => new { fulfilled = state.Intents.Count(i => i.Status == FoodIntentStatus.Fulfilled),
            unmet = state.Intents.Count(i => i.Status == FoodIntentStatus.Unmet), unresolved = state.Intents.Count(i => i.Status == FoodIntentStatus.Unresolved),
            shopMeals = state.Businesses.ToDictionary(b => b.Id, b => state.Intents.Count(i => i.BusinessId == b.Id && i.Status == FoodIntentStatus.Fulfilled)) };
        private void Respond(object value)
        {
            if (pendingId.Length == 0) return;
            File.WriteAllText(Path.Combine(Root, "response-" + pendingId + ".json"), JsonConvert.SerializeObject(value, Formatting.Indented));
            pendingId = "";
        }
        private static string Fingerprint() => JsonConvert.SerializeObject(new { market = SaveGameManager.Current.productMarketEntries, buildings = SaveGameManager.Current.BuildingRegistrations.Select(r => new {
            r.StreetName, r.StreetNumber, r.retailPrices, r.temporarilyClosed, r.scheduleDays, r.unprocessedCompletedOrders, r.orderHistory, r.satisfaction,
            stock = r.itemInstances?.Select(i => new { i.Key, cargo = i.Value.cargoInstances.Select(c => new { c.itemName, c.amount, c.pricePerUnit }) }) }).ToArray() });
    }
}
