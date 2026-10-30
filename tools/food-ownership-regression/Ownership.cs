using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;
using UnityEngine;

namespace FoodDemandAuditProbe
{
    // Only linked into the standalone diagnostic helper. Never shipped in PersistentCity.dll.
    public sealed partial class Probe
    {
        private const string FixtureMarker = "persistentcity:test-ownership-baseline";
        private readonly Dictionary<string, List<string>> ownershipMenus = new Dictionary<string, List<string>>();
        private object OwnershipOperation(string op, JObject request)
        {
            var save = SaveGameManager.Current;
            string name = save?.SaveGameName ?? "";
            if (!Allowed() || !System.Text.RegularExpressions.Regex.IsMatch(name, "^FT Food Routines Ownership [A-Za-z0-9_-]{1,40}$"))
                throw new InvalidOperationException("A named Ownership copy on the isolated agent-test rig is required.");
            // SaveAs retains the original fixture marker. Reading a named disposable
            // snapshot needs no mutation authority; all mutations still require their guards.
            if (op == "ownership-status") return OwnershipStatus();
            if (op == "ownership-fresh-copy")
            {
                if ((string?)request["expectedName"] != name || (string?)request["sourceName"] == name
                    || (string?)request["sourceName"] is not string source || !source.StartsWith("FT Food Routines ", StringComparison.Ordinal)
                    || (string?)request["confirmation"] != "new-copy-original-backed-up"
                    || save!.modData.ContainsKey(FixtureMarker) || Time.timeScale != 0)
                    throw new InvalidOperationException("Fresh distinct paused copy, retained original backup and explicit confirmation required. Existing initialized copies cannot be reset.");
                string backup = Path.Combine(Root, name.Replace(' ', '-') + "-original-moddata.json");
                using (var file = new StreamWriter(new FileStream(backup, FileMode.CreateNew, FileAccess.Write)))
                    file.Write(JsonConvert.SerializeObject(new { source, copy = name, data = save.modData }, Formatting.Indented));
                // Stop all pending population writes. The passive saved guard remains until
                // reload; do not advance this paused native city between clearing and saving.
                Runtime.Shutdown();
                var removed = save.modData.Keys.Where(k => k.StartsWith("persistentcity:", StringComparison.Ordinal)).ToArray();
                foreach (string key in removed) save.modData.Remove(key);
                save.modData[FixtureMarker] = name;
                return new { copy = name, source, removedKeys = removed.Length, backup,
                    next = "Keep paused, save this named copy, then reload it. Never overwrite or clear the source checkpoint." };
            }
            if (!save!.modData.TryGetValue(FixtureMarker, out var marker) || marker != name)
                throw new InvalidOperationException("Initialize this fresh Ownership copy explicitly before diagnostic mutations.");
            if (op == "ownership-fixtures")
            {
                if (Simulation.State.Food?.Shops.Count != 0 || Simulation.State.Food.Residents.Any(m => m.Purchases != 0))
                    throw new InvalidOperationException("Fixture setup requires the untouched fresh automatic city before any scope or sale.");
                var forwarded = (JObject)request.DeepClone();
                return SetupNeighborhood(forwarded);
            }
            string place = (string?)request["shop"] ?? throw new InvalidOperationException("Explicit fixture shop required.");
            var shop = save.BuildingRegistrations.Single(r => PlaceId(r) == place);
            if (shop.BusinessName != "Persistent City food routines") throw new InvalidOperationException("Named disposable shop required.");
            if (op == "ownership-menu-native")
            {
                if (ownershipMenus.ContainsKey(place)) throw new InvalidOperationException("Menu edit already pending.");
                ownershipMenus.Add(place, shop.cachedAvailableProducts.ToList());
                shop.cachedAvailableProducts.Add("ba:itemname_pizza");
                return OwnershipStatus();
            }
            if (op == "ownership-menu-supported")
            {
                if (!ownershipMenus.TryGetValue(place, out var products)) throw new InvalidOperationException("No retained menu to restore in this session.");
                shop.cachedAvailableProducts = products; ownershipMenus.Remove(place);
                return OwnershipStatus();
            }
            if (op == "ownership-reset")
            {
                if ((string?)request["confirmation"] != "discard-this-fixture-copy" || Time.timeScale != 0)
                    throw new InvalidOperationException("Reset requires a paused disposable branch and explicit discard confirmation.");
                var before = OwnershipStatus(); shop.Reset();
                return new { before, after = OwnershipStatus(), note = "Actual native same-address Reset invoked. Discard branch after capture; no fixture restoration or free replenishment." };
            }
            throw new InvalidOperationException("Unknown ownership diagnostic operation.");
        }
        private object OwnershipStatus()
        {
            var save = SaveGameManager.Current;
            save.modData.TryGetValue(FoodAuthorityStore.Key, out var authority);
            var native = (NativeFoodMarket?)typeof(PopulationRuntime).GetField("foodMarket", Fields)!.GetValue(Runtime);
            var sim = (CitySimulator?)typeof(PopulationRuntime).GetField("simulation", Fields)!.GetValue(Runtime);
            var routes = typeof(PopulationRuntime).GetField("routes", Fields)!.GetValue(Runtime);
            var metrics = routes?.GetType().GetProperty("Metrics")?.GetValue(routes);
            // Fixed-size cumulative counters only. Capture values now rather than retaining
            // the live mutable metrics object; compare deltas only within the same planner.
            var routeMetrics = metrics == null ? null : JObject.FromObject(metrics);
            return new { save.SaveGameName, save.Day, save.Hour, save.Minute, authority = FoodAuthorityStore.Read(authority),
                routeMetrics, routeMetricsPlanner = routes == null ? (int?)null : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(routes),
                food = sim?.State.Food == null ? null : new { sim.State.Food.RepresentationFactor, sim.State.Food.HouseholdFinanceVersion,
                    scopes = sim.State.Food.Shops, purchases = sim.State.Food.Residents.Sum(m => m.Purchases),
                    representedBlockedReturns = sim.State.Food.Residents.Count(m => m.Slot > 0 && m.Excursion?.Unresolved == true),
                    representedNeedsHomeResync = sim.State.Food.Residents.Count(m => m.Slot > 0 && m.NeedsHomeResync),
                    uncertain = sim.State.Food.Residents.Where(m => m.Status == MealStatus.Uncertain).Select(m => new { m.IntentId, m.BusinessId,
                        m.ProductId, m.PriceCents, held = m.Budget?.HeldPurchaseCents, receipt = FoodSaleReceipts.ReadStatus(save.modData, m).ToString() }) },
                diagnostics = Runtime.FoodDiagnostics,
                nativeBodies = IndoorCustomerSpawner.Customers.Count,
                inside = BuildingManager.Instance?.buildingRegistration == null ? null : PlaceId(BuildingManager.Instance.buildingRegistration),
                receipts = save.modData.Where(p => p.Key.StartsWith("persistentcity:food-sale-v1:", StringComparison.Ordinal)).ToDictionary(p => p.Key, p => p.Value),
                shops = save.BuildingRegistrations.Where(r => r.BusinessName == "Persistent City food routines").Select(r => new {
                    place = PlaceId(r), r.businessTypeName, r.RentedByPlayer, r.cachedAvailableProducts,
                    exclusion = native?.ScopeStatus(PlaceId(r)), completedOrders = r.unprocessedCompletedOrders.Count,
                    supply = r.cachedAvailableProducts.Where(FoodProducts.Known).Select(p => FoodSupplyReader.Read(r, p)).ToArray() }) };
        }
    }
}
