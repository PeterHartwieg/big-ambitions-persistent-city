using System;
using System.Collections.Generic;
using System.Linq;
using IngameDebugConsole;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using UnityEngine;

namespace PersistentCity.Runtime
{
    public sealed partial class PopulationRuntime
    {
        private bool vendorCommands;
        private readonly Queue<string> vendorDecisions = new Queue<string>();
        private CitySimulator? vendorObservedCity;
        private static bool VendorDiagnosticCity(string? name) => name?.StartsWith("FT", StringComparison.Ordinal) == true;
        private void RegisterVendorDiagnostics()
        {
            try
            {
                // One router per existing pc arity, preserving the population console commands.
                CommandHelper.AddCommand<string, string>("pc", "pc population census | pc vendor status | pc vendor sales | pc food decisions", RoutePcCommand);
                CommandHelper.AddCommand<string, string, string>("pc", "pc population person <id> | pc vendor withdraw <vehicle> (FT cities only)", RoutePcDetail);
                vendorCommands = true;
            }
            catch (Exception e) { log?.Warn("Vendor diagnostics registration failed: " + e.Message); }
        }
        private void UnregisterVendorDiagnostics()
        {
            if (!vendorCommands) return;
            vendorCommands = false;
            try { CommandHelper.RemoveCommand<string, string>(RoutePcCommand); }
            catch (Exception e) { log?.Warn("Vendor diagnostics command removal failed: " + e.Message); }
            try { CommandHelper.RemoveCommand<string, string, string>(RoutePcDetail); }
            catch (Exception e) { log?.Warn("Vendor diagnostics detail command removal failed: " + e.Message); }
            if (vendorObservedCity != null) vendorObservedCity.FoodDecisionObserved -= RecordVendorDecision;
            vendorObservedCity = null; vendorDecisions.Clear();
        }
        private void AttachVendorDecisionDiagnostics(CitySimulator city)
        {
            if (vendorObservedCity != null) vendorObservedCity.FoodDecisionObserved -= RecordVendorDecision;
            vendorObservedCity = city; vendorDecisions.Clear();
            if (VendorDiagnosticCity(SaveGameManager.Current?.SaveGameName)) city.FoodDecisionObserved += RecordVendorDecision;
        }
        private void RecordVendorDecision(FoodDecisionTrace trace)
        {
            if (!VendorDiagnosticCity(SaveGameManager.Current?.SaveGameName)) return;
            try
            {
                // Store detached text, bounded to 50 decisions; no diagnostic objects enter saved state.
                string line = JsonConvert.SerializeObject(new { resident = trace.Consumer.Id, trace.Intent.Id, trace.MealKind,
                    trace.Intent.BusinessId, vendors = trace.Businesses.Where(b => VendorNames.IsVendorPlace(b.PlaceId)),
                    trace.ChoiceCosts, trace.Decision, trace.RouteChecks });
                if (vendorDecisions.Count == 50) vendorDecisions.Dequeue();
                vendorDecisions.Enqueue(line);
            }
            catch (Exception error)
            {
                if (vendorObservedCity != null) vendorObservedCity.FoodDecisionObserved -= RecordVendorDecision;
                log?.Warn("Vendor decision diagnostics stopped: " + error.Message);
            }
        }
        private void RoutePcCommand(string area, string action) => RoutePcDetail(area, action, "");
        private void RoutePcDetail(string area, string action, string detail)
        {
            if (area == "workplace") { WorkplaceCommand(action, detail); return; }
            if (area != "vendor" && !(area == "food" && action == "decisions"))
            { if (detail.Length == 0) CensusCommand(area, action); else CensusDetailCommand(area, action, detail); return; }
            var save = SaveGameManager.Current;
            if (!VendorDiagnosticCity(save?.SaveGameName)) { Debug.LogWarning("[Persistent City] vendor diagnostics require an FT city."); return; }
            if (failed || simulation == null || save == null || !ReferenceEquals(saveIdentity, save) || vendorMarket == null)
            { Debug.LogWarning("[Persistent City] vendor diagnostics unavailable: no active city."); return; }
            if (area == "food") { Debug.Log("[Persistent City] food decisions\n" + string.Join("\n", vendorDecisions)); return; }
            var market = vendorMarket;
            switch (action)
            {
                case "status":
                    Debug.Log("[Persistent City] vendor status\n" + JsonConvert.SerializeObject(new {
                        bridge = market.Coordinator.Image?.Status.ToString() ?? "Absent", market.Coordinator.Reason,
                        market.WorkSpikes, market.MaximumWorkMilliseconds, leaseUntil = market.Coordinator.Lease.UntilMinute, market.Coordinator.LeaseFault, market.Coordinator.Refusals,
                        rows = market.Coordinator.Table.Rows.Select(r => new { r.VehicleId, r.BusinessId, r.PlaceId, r.State,
                            r.Authority, r.Stand, r.DeferredStand, r.Reason, canServe = market.CanServe(r.BusinessId) }) }));
                    break;
                case "sales":
                    Debug.Log("[Persistent City] vendor sales\n" + JsonConvert.SerializeObject(new {
                        market.Purchases, receipts = save.modData.Where(p => FoodSaleReceipts.IsReceiptKey(p.Key)
                            && p.Value.Contains(Uri.EscapeDataString(VendorNames.SignaturePrefix))),
                        meals = simulation.State.Food!.Residents.Where(m => VendorNames.IsVendorPlace(m.PlaceId))
                            .Select(m => new { m.IntentId, m.ResidentId, m.BusinessId, m.ProductId, cents = m.Price.ToCentsText(),
                                m.Status, receipt = FoodSaleReceipts.ReadStatus(save.modData, m).ToString(), wallet = m.Budget }) }));
                    break;
                case "withdraw":
                    Debug.Log("[Persistent City] vendor withdraw " + JsonConvert.SerializeObject(new { vehicle = detail, accepted = market.Withdraw(detail) }));
                    break;
                default: Debug.LogWarning("[Persistent City] use pc vendor status | pc vendor sales | pc vendor withdraw <vehicle> | pc food decisions"); break;
            }
        }
    }
}
