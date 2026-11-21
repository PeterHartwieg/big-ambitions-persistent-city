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
        private FoodDecisionRecorder? foodDecisionRecorder;
        private FoodDecisionRecorder foodDecisions => foodDecisionRecorder ??= NewFoodDecisionRecorder();
        private static bool VendorDiagnosticCity(string? name) => name?.StartsWith("FT", StringComparison.Ordinal) == true;
        private void RegisterVendorDiagnostics()
        {
            try
            {
                CommandHelper.AddCommand<string, string>("pc", "pc population census | pc gc start|report|stop|frames | pc vendor status | pc vendor sales | pc food decisions | pc food contract | pc probe status | pc probe businesses | pc gc retained", RoutePcCommand);
                CommandHelper.AddCommand<string, string, string>("pc", "pc population person <id> | pc gc population <persons|off> (FT cities only, measurement) | pc gc frames <start|report|stop> | pc food decisions <start|stop> | pc vendor withdraw <vehicle> | pc food contract <status|capabilities|hints|stamps|catalogue> (FT cities only)", RoutePcDetail);
                CommandHelper.AddCommand<string, string, string, string>("pc", "pc movement corrections since <seq> | pc probe <resident> <place> <ceiling cents> (FT cities only)", RoutePcMovement);
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
            try { CommandHelper.RemoveCommand<string, string, string, string>(RoutePcMovement); }
            catch (Exception e) { log?.Warn("Movement diagnostics command removal failed: " + e.Message); }
            foodDecisions.Detach();
        }
        private void AttachVendorDecisionDiagnostics(CitySimulator city) => foodDecisions.Detach();
        private void RebindVendorDecisionDiagnostics(CitySimulator city) => foodDecisions.Rebind(city);
        private FoodDecisionRecorder NewFoodDecisionRecorder() => new FoodDecisionRecorder(trace => !VendorDiagnosticCity(SaveGameManager.Current?.SaveGameName) ? null
            : JsonConvert.SerializeObject(new { resident = trace.Consumer.Id, trace.Intent.Id, trace.MealKind,
                trace.Intent.BusinessId, vendors = trace.Businesses.Where(b => VendorNames.IsVendorPlace(b.PlaceId)),
                trace.ChoiceCosts, trace.Decision, trace.RouteChecks }),
            error => log?.Warn("Vendor decision diagnostics stopped: " + error.Message));
        private void FoodDecisionsCommand(string detail)
        {
            if (detail == "start")
            {
                foodDecisions.Start(simulation!);
                Debug.Log("[Persistent City] food decisions recording started (diagnostic only; traces every food decision and keeps the last 50). Use pc food decisions stop when done.");
                return;
            }
            if (detail == "stop") { foodDecisions.Stop(); Debug.Log("[Persistent City] food decisions recording stopped; the last 50 stay readable."); return; }
            if (detail.Length > 0) { Debug.LogWarning("[Persistent City] Unknown command. Use: pc food decisions [start|stop]"); return; }
            Debug.Log("[Persistent City] food decisions (recording " + (foodDecisions.Recording ? "on" : "off; use pc food decisions start") + ")\n" + string.Join("\n", foodDecisions.Lines));
        }
        private void RoutePcCommand(string area, string action) => RoutePcDetail(area, action, "");
        private void RoutePcMovement(string area, string action, string detail, string value)
        {
            DiagnosticFrames.Mark("pc " + area + " " + action);
            if (area == "movement") MovementCommand(action, detail, value);
            else if (area == "probe") SaleProbeCommand(action, detail, value);
            else Debug.LogWarning("[Persistent City] Unknown command. Use: pc movement corrections since <seq>");
        }
        private void RoutePcDetail(string area, string action, string detail)
        {
            DiagnosticFrames.Mark("pc " + area + " " + action);
            if (area == "movement") { MovementCommand(action, detail, ""); return; }
            if (area == "gc") { GcCommand(action, detail); return; }
            if (area == "workplace") { WorkplaceCommand(action, detail); return; }
            if (area == "probe") { SaleProbeCommand(action, detail, ""); return; }
            if (area == "food" && action == "contract") { FoodContractCommand(detail); return; }
            if (area != "vendor" && !(area == "food" && action == "decisions"))
            { if (detail.Length == 0) CensusCommand(area, action); else CensusDetailCommand(area, action, detail); return; }
            var save = SaveGameManager.Current;
            if (!VendorDiagnosticCity(save?.SaveGameName)) { Debug.LogWarning("[Persistent City] vendor diagnostics require an FT city."); return; }
            if (failed || simulation == null || save == null || !ReferenceEquals(saveIdentity, save) || vendorMarket == null)
            { Debug.LogWarning("[Persistent City] vendor diagnostics unavailable: no active city."); return; }
            if (area == "food") { FoodDecisionsCommand(detail); return; }
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
                        meals = CityViewReports.VendorMeals(simulation.View)
                            .Select(v => new { v.Record.IntentId, v.Record.ResidentId, v.Record.BusinessId, v.Record.ProductId, cents = v.Record.Price.ToCentsText(),
                                v.Record.Status, receipt = FoodSaleReceipts.ReadStatus(save.modData, v.Record).ToString(), wallet = v.Wallet }) }));
                    break;
                case "withdraw":
                    Debug.Log("[Persistent City] vendor withdraw " + JsonConvert.SerializeObject(new { vehicle = detail, accepted = market.Withdraw(detail) }));
                    break;
                default: Debug.LogWarning("[Persistent City] use pc vendor status | pc vendor sales | pc vendor withdraw <vehicle> | pc food decisions"); break;
            }
        }
    }
}
