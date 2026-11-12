using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AI.Customers.CustomerEntries;
using BigAmbitions.Items;
using Buildings;
using Buildings.Retail.Businesses.Gym;
using Buildings.Retail.Businesses.Hairdresser;
using Buildings.Retail.Simulation;
using Entities;
using HarmonyLib;
using Helpers;
using Helpers.BusinessSimulation;
using Newtonsoft.Json;
using PersistentCity.Core;
using UnityEngine;

namespace PersistentCity.Runtime
{
    /// <summary>S2 sale proof, native side. Diagnostic only, FT cities only, one probe per console command. It changes no
    /// ownership: it sells one order at a player business through <see cref="NativeSalePath"/> (the business type's own
    /// simulator asset) and reports what the game did. Core <see cref="PurchaseProbe"/> owns the ceiling, wallet and receipt.</summary>
    internal sealed class NativeSaleProbe : IProbeNativeSale
    {
        private readonly NativeSalePath path;
        private NativeSaleProbe(NativeSalePath path) { this.path = path; }

        /// <summary>The probe's families are the contract's, in the same order.</summary>
        internal static ProbeFamily Family(BusinessSimulator? simulator) => (ProbeFamily)(int)NativeSalePath.FamilyOf(simulator);

        internal static ProbeKind Kind(ProbeFamily family, bool foodOwned)
            => foodOwned ? ProbeKind.Food : family == ProbeFamily.FullService || family == ProbeFamily.SelfService ? ProbeKind.Goods : ProbeKind.Service;

        internal static bool FoodOwned(BuildingRegistration reg) => NativeSalePath.FoodOwned(reg);

        /// <summary>True only inside a native ProcessCustomer call of the sale path for this registration.</summary>
        internal static bool IsProbing(BuildingRegistration reg) => NativeSalePath.IsSelling(reg);

        internal static bool Matches(BuildingRegistration reg, string placeId)
        {
            string shop = NativeFoodMarket.PlaceId(reg);
            return placeId == shop || placeId == shop.Substring(0, shop.Length - ":shop".Length) + ":leisure";
        }

        internal static NativeSaleProbe? Prepare(BuildingRegistration reg, out ProbeFamily family, out List<ProbeLine> basket, out string refusal)
        {
            var path = NativeSalePath.Prepare(reg, out var found, out refusal);
            family = (ProbeFamily)(int)found;
            basket = path == null ? new List<ProbeLine>() : path.Basket.Select(l => new ProbeLine(l.ItemId, l.Price, l.Mandatory)).ToList();
            return path == null ? null : new NativeSaleProbe(path);
        }

        internal Dictionary<string, object?> Evidence => path.Evidence;
        internal bool Claimed => path.Claimed;
        internal string SimulatorName => path.SimulatorName;

        public ProbeNativeOutcome Sell(IReadOnlyList<ProbeLine> lines)
        {
            var outcome = path.Sell(lines.Select(l => new PurchaseLine(l.ItemId, l.Price, l.Mandatory)).ToArray());
            if (!outcome.Sold) return ProbeNativeOutcome.Interrupted(outcome.Reason);
            return ProbeNativeOutcome.Processed(outcome.Paid.Select(l => new ProbeLine(l.ItemId, l.Price, false)).ToArray(), outcome.Unpriced);
        }

        public void Undo() => path.Undo();
        /// <summary>The probe was paid: replay the refills the sale held back.</summary>
        internal void Commit() => path.Commit();
    }

    public sealed partial class PopulationRuntime
    {
        private bool saleProbeRunning;

        /// <summary>pc probe status | pc probe businesses | pc probe &lt;resident&gt; &lt;place&gt; &lt;ceiling cents&gt;. FT cities only.</summary>
        private void SaleProbeCommand(string action, string detail, string value)
        {
            var save = SaveGameManager.Current;
            if (!VendorDiagnosticCity(save?.SaveGameName)) { Debug.LogWarning("[Persistent City] sale probe requires an FT city."); return; }
            if (failed || simulation == null || save == null || !ReferenceEquals(saveIdentity, save) || save.modData == null)
            { Debug.LogWarning("[Persistent City] sale probe unavailable: no active city."); return; }
            if (saleProbeRunning) { Debug.LogWarning("[Persistent City] sale probe already running."); return; }
            int recovered = PurchaseProbe.Recover(save.modData);
            if (action == "status")
            {
                // GR-3: the need purchase counters and native-count calibration rows (calibration only, never a decision).
                var purchases = new { attached = simulation.Purchases != null, fault = simulation.PurchaseFault, bought = simulation.PurchasesBought,
                    refused = simulation.PurchasesRefused, impulses = simulation.ImpulsesInserted,
                    last = simulation.LastPurchase == null ? null : new { simulation.LastPurchase.Key, refusal = simulation.LastPurchase.Refusal.ToString(),
                        total = simulation.LastPurchase.Total.ToCentsText(), simulation.LastPurchase.Reason },
                    calibration = simulation.Calibration.Select(r => new { r.VenueId, r.Hour, r.NativeCount, r.ResidentOrders }),
                    receipts = save.modData.Where(p => PurchaseContract.IsReceiptKey(p.Key)).Select(p => new { p.Key, p.Value }) };
                Debug.Log("[Persistent City] sale probe status\n" + JsonConvert.SerializeObject(new { recovered, purchases,
                    receipts = save.modData.Where(p => PurchaseProbe.IsReceiptKey(p.Key)).Select(p => new { p.Key, status = PurchaseProbe.Status(p.Value).ToString(), p.Value }) }));
                return;
            }
            var controllers = CityManager.Instance?.cityBuildingControllers;
            if (action == "businesses")
            {
                var rows = new List<object>();
                if (controllers != null) foreach (var c in controllers)
                {
                    var reg = c?.buildingRegistration;
                    if (reg == null || !reg.RentedByPlayer || !reg.HasEstablishedBusiness) continue;
                    var sim = BusinessTypeHelper.GetData(reg.businessTypeName)?.simulator;
                    var family = NativeSaleProbe.Family(sim); bool food = NativeSaleProbe.FoodOwned(reg);
                    rows.Add(new { place = NativeFoodMarket.PlaceId(reg), type = reg.businessTypeName, simulator = sim?.GetType().FullName,
                        family = family.ToString(), kind = family == ProbeFamily.Unsupported ? null : NativeSaleProbe.Kind(family, food).ToString(),
                        foodOwned = food, open = BusinessHelper.IsBusinessOpen(reg) });
                }
                Debug.Log("[Persistent City] sale probe businesses\n" + JsonConvert.SerializeObject(rows));
                return;
            }
            if (!long.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out long cents)
                || cents < 0 || cents > Money.MaximumNativePriceCents)
            { Debug.LogWarning("[Persistent City] use pc probe status | pc probe businesses | pc probe <resident> <place> <ceiling cents>"); return; }
            var person = simulation.State.Citizens.FirstOrDefault(c => c.Identity.Id == action);
            if (person == null) { Debug.LogWarning("[Persistent City] sale probe: unknown resident " + action); return; }
            BuildingRegistration? target = null;
            if (controllers != null) foreach (var c in controllers)
                if (c?.buildingRegistration != null && NativeSaleProbe.Matches(c.buildingRegistration, detail)) { target = c.buildingRegistration; break; }
            string Refuse(string why) => JsonConvert.SerializeObject(new { resident = action, place = detail, refused = why });
            if (target == null || !target.RentedByPlayer || !target.HasEstablishedBusiness) { Debug.LogWarning("[Persistent City] sale probe " + Refuse("Not a player business")); return; }
            if (!BusinessHelper.IsBusinessOpen(target)) { Debug.LogWarning("[Persistent City] sale probe " + Refuse("Closed")); return; }
            if (BuildingManager.IsInsideBuilding) { Debug.LogWarning("[Persistent City] sale probe " + Refuse("PlayerInside")); return; }
            var native = NativeSaleProbe.Prepare(target, out var familyFound, out var basket, out var refusal);
            if (native == null) { Debug.LogWarning("[Persistent City] sale probe " + Refuse(familyFound + ": " + refusal)); return; }
            var kind = NativeSaleProbe.Kind(familyFound, NativeSaleProbe.FoodOwned(target));
            long minute = save.Day * 1440L + save.Hour * 60L + (long)save.Minute;
            var request = new PurchaseProbeRequest(person.Identity.Id, detail, kind, minute, Money.FromCents(cents), basket);
            var walletBefore = person.Wallet?.Balance;
            int ordersBefore = target.unprocessedCompletedOrders.Count;
            PurchaseProbeResult result;
            saleProbeRunning = true;
            PurchaseProbeResult? ran = null;
            // A native save started inside the probe is re-entrant: it publishes no epoch, marks that save refused on load and
            // stops Persistent City afterwards. Its native books may hold the order without the debit; it is never settled.
            try { RunSaveEpochUnit(() => ran = PurchaseProbe.Run(request, person.Wallet, save.modData, native)); }
            finally { saleProbeRunning = false; }
            result = ran!;
            if (result.Receipt == ProbeReceiptStatus.Paid) native.Commit();
            Debug.Log("[Persistent City] sale probe\n" + JsonConvert.SerializeObject(new {
                resident = person.Identity.Id, place = detail, type = target.businessTypeName, simulator = native.SimulatorName,
                family = familyFound.ToString(), kind = kind.ToString(), claimedNativeEntry = native.Claimed, recovered,
                basket = basket.Select(l => new { l.ItemId, cents = l.Price.ToCentsText(), l.Mandatory }),
                ceiling = result.Quote?.Ceiling.ToCentsText(), quoted = result.Quote?.Total.ToCentsText(),
                refusal = result.Refusal.ToString(), receipt = result.Receipt.ToString(), result.Key, paid = result.Paid.ToCentsText(), result.Reason,
                walletBefore = walletBefore?.ToCentsText(), walletAfter = person.Wallet?.Balance.ToCentsText(),
                ordersBefore, ordersAfter = target.unprocessedCompletedOrders.Count, native = native.Evidence }));
        }
    }
}
