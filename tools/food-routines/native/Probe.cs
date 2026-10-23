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

[assembly: RegisterModClass(typeof(FoodRoutineProbe.Entry))]
namespace FoodRoutineProbe
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
        private double maximumRouteBatchMs;
        private static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        public static bool Allowed() => Environment.GetCommandLineArgs().Contains("-agent-tests")
            && Application.persistentDataPath.EndsWith("/com.hovgaardgames.bigambitions.agenttests.mac", StringComparison.Ordinal);
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
                var selected = CityManager.Instance.cityBuildingControllers.Where(c => c != null && c.buildingRegistration != null)
                    .Select(c => c.buildingRegistration).Where(r => r.HasEstablishedBusiness && !r.AvailableForRent && r.retailPrices != null
                        && r.cachedAvailableProducts != null && r.retailPrices.Any(p => products.Contains(p.itemName) && r.cachedAvailableProducts.Contains(p.itemName)))
                    .OrderBy(r => r.StreetName, StringComparer.Ordinal).ThenBy(r => r.StreetNumber).Take(4)
                    .Select(r => "address:" + Uri.EscapeDataString(r.StreetName) + ":" + r.StreetNumber.ToString(CultureInfo.InvariantCulture) + ":shop").ToArray();
                market = FoodBusinessCatalogue.Read(selected, products);
                batch = Runtime.QuoteFoodRoutes(capture, plan, market.Businesses, 3);
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
            if (strict == null)
            {
                int day = (int)Math.Ceiling(capture.StartedAt / 1440);
                var state = FoodDemand.CreateObserved(capture.CityKey,
                    "Actual native population activity observation and native walking estimates. Stock and staffed throughput remain unknown.",
                    day, 1, plan.Consumers, market.Businesses, batch.Quotes, plan.Opportunities);
                File.WriteAllText(Path.Combine(Root, "strict-initial.json"), FoodShadowCodec.Write(state));
                strict = new FoodDemand(state);
                var counterfactual = FoodShadowCodec.Read(FoodShadowCodec.Write(state), state.CityKey);
                counterfactual.Evidence = "Native activities, advertised prices/hours and walking estimates; CONTROLLED 15 meals/hour and 10,000 hypothetical units per offer, not observed staffing/stock.";
                foreach (var shop in counterfactual.Businesses)
                {
                    shop.MealsPerHour = 15; shop.ServiceEvidence = "Controlled counterfactual throughput, not native measurement";
                    foreach (var offer in shop.Offers) offer.Units = 10000;
                }
                File.WriteAllText(Path.Combine(Root, "controlled-initial.json"), FoodShadowCodec.Write(counterfactual));
                controlled = new FoodDemand(counterfactual);
                return;
            }
            if (!strict.AdvanceTo(strict.State.EndsAt, 16)) return;
            if (!controlled!.AdvanceTo(controlled.State.EndsAt, 16)) return;
            File.WriteAllText(Path.Combine(Root, "strict-final.json"), FoodShadowCodec.Write(strict.State));
            File.WriteAllText(Path.Combine(Root, "controlled-final.json"), FoodShadowCodec.Write(controlled.State));
            bool marketUnchanged = marketBefore == Fingerprint();
            bool populationUnchanged = populationBefore == SaveCodec.Write(Simulation.State);
            if (!marketUnchanged || !populationUnchanged) throw new InvalidOperationException("Paused population/market changed during diagnostic replay.");
            Respond(new { capture.StartedAt, capture.EndsAt, residents = capture.Residents.Count, spans = capture.Spans.Count,
                origins = plan.Opportunities.GroupBy(i => i.OriginActivity).ToDictionary(g => g.Key.ToString(), g => g.Count()),
                knownOrigins = plan.Opportunities.Count(i => i.OriginPlaceId.Length > 0), shops = market.Businesses.Select(b => new { b.Id, b.Name }),
                batch.RouteCalls, batch.CandidateCount, completeRoundTrips = batch.Quotes.Count(q => q.Minutes.HasValue && q.ReturnMinutes.HasValue), maximumRouteBatchMs,
                strict = Summary(strict.State), controlled = Summary(controlled.State), marketUnchanged, populationUnchanged,
                evidence = "One full day of native population observations; paused diagnostic replay. Controlled service run is not observed sales or stock." });
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
        private static string Fingerprint() => JsonConvert.SerializeObject(SaveGameManager.Current.BuildingRegistrations.Select(r => new {
            r.StreetName, r.StreetNumber, r.retailPrices, r.temporarilyClosed, r.scheduleDays, r.unprocessedCompletedOrders, r.orderHistory,
            stock = r.itemInstances?.Select(i => new { i.Key, cargo = i.Value.cargoInstances.Select(c => new { c.itemName, c.amount, c.pricePerUnit }) }) }).ToArray());
    }
}
