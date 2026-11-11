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
    /// ownership: it sells one order at a player business through the business type's own simulator asset and reports
    /// what the game did. Core <see cref="PurchaseProbe"/> owns the ceiling, wallet and receipt.</summary>
    internal sealed class NativeSaleProbe : IProbeNativeSale
    {
        private static BuildingRegistration? probing;
        private static MethodInfo? process;
        /// <summary>True only inside a probe's native ProcessCustomer call for this registration; the food authority's
        /// customer gate lets it through as it does the mod's own food checkout.</summary>
        internal static bool IsProbing(BuildingRegistration reg) => ReferenceEquals(probing, reg);

        private readonly object save;
        private readonly BuildingRegistration reg;
        private readonly BusinessType type;
        private readonly ProbeFamily family;
        private readonly CustomerEntry? claimed;
        private readonly string? fee;
        private readonly int hour;
        internal readonly Dictionary<string, object?> Evidence = new Dictionary<string, object?>(StringComparer.Ordinal);
        // Pre-sale image for Undo: every holder's cargo list and amounts, the booked-order count, the claimed entry's slot.
        private readonly List<(List<CargoInstance> list, CargoInstance[] cargo, int[] amounts)> stock = new List<(List<CargoInstance>, CargoInstance[], int[])>();
        private int ordersBefore = -1, claimedSlot = -1;

        private NativeSaleProbe(object save, BuildingRegistration reg, BusinessType type, ProbeFamily family, CustomerEntry? claimed, string? fee)
        { this.save = save; this.reg = reg; this.type = type; this.family = family; this.claimed = claimed; this.fee = fee; hour = SaveGameManager.Current.Hour; }

        internal static ProbeFamily Family(BusinessSimulator? simulator)
        {
            // Exact classes only (J0): a subclass is a modded shop with its own code and stays native.
            var t = simulator?.GetType();
            return t == typeof(FullServiceBusinessSimulator) ? ProbeFamily.FullService
                : t == typeof(SelfServiceBusinessSimulator) ? ProbeFamily.SelfService
                : t == typeof(HairdresserBusinessSimulator) ? ProbeFamily.Hairdresser
                : t == typeof(GymBusinessSimulator) ? ProbeFamily.Gym
                : t == typeof(CinemaTheaterBusinessSimulator) ? ProbeFamily.CinemaTheater
                : t == typeof(NightclubBusinessSimulator) ? ProbeFamily.Nightclub : ProbeFamily.Unsupported;
        }

        internal static ProbeKind Kind(ProbeFamily family, bool foodOwned)
            => foodOwned ? ProbeKind.Food : family == ProbeFamily.FullService || family == ProbeFamily.SelfService ? ProbeKind.Goods : ProbeKind.Service;

        internal static bool FoodOwned(BuildingRegistration reg)
            => NativeFoodOwnership.Row(NativeFoodMarket.PlaceId(reg))?.OwnsHour(NativeFoodOwnership.Hour) == true;

        internal static bool Matches(BuildingRegistration reg, string placeId)
        {
            string shop = NativeFoodMarket.PlaceId(reg);
            return placeId == shop || placeId == shop.Substring(0, shop.Length - ":shop".Length) + ":leisure";
        }

        /// <summary>Reads only. The basket is one uncompleted native entry of this hour (J0 step 1); with none (an owned
        /// food shop draws none) it is one unit of the first priced product, as the food path writes its own entry.</summary>
        internal static NativeSaleProbe? Prepare(BuildingRegistration reg, out ProbeFamily family, out List<ProbeLine> basket, out string refusal)
        {
            basket = new List<ProbeLine>(); family = ProbeFamily.Unsupported; refusal = "";
            var type = BusinessTypeHelper.GetData(reg.businessTypeName);
            family = Family(type?.simulator);
            if (type == null || family == ProbeFamily.Unsupported) { refusal = "Unsupported simulator " + (type?.simulator?.GetType().FullName ?? "none"); return null; }
            var save = SaveGameManager.Current;
            CustomerEntry? claimed = null;
            if (!FoodOwned(reg))
                foreach (var entry in CustomerEntriesHelper.GetEntriesByAddress(reg.Address) ?? new List<CustomerEntry>())
                    if (entry != null && !entry.completed && entry.spawnTime != null && entry.spawnTime.Day == save.Day && entry.spawnTime.Hour == save.Hour
                        && entry.order?.entries != null && entry.order.entries.Count > 0 && entry.order.entries.Count <= 32)
                    { claimed = entry; break; }
            // ProcessEntranceFee runs in every family's processing except cinema and theatre, and adds a line the basket lacks.
            string? fee = family != ProbeFamily.CinemaTheater && type.hasEntranceFee ? BusinessTypeHelper.GetEntranceFeeNameForBusinessType(type) : null;
            if (fee != null && !Line(fee, reg, true, basket)) { refusal = "Entrance fee " + fee + " has no supported price"; return null; }
            if (claimed != null)
            {
                foreach (var entry in claimed.order.entries)
                    if (entry == null || !Line(entry.itemName, reg, IsTicket(entry.itemName), basket)) { refusal = "Claimed entry has an unpriced line"; return null; }
            }
            else if (family == ProbeFamily.CinemaTheater)
            {
                // A written entry would have to rebuild the native ticket-first order; the probe needs a native one here.
                refusal = "No uncompleted native entry this hour"; return null;
            }
            else
            {
                string? product = reg.cachedAvailableProducts?.FirstOrDefault(p => p != fee && !IsTicket(p) && Price(p, reg).HasValue);
                if (product == null || !Line(product, reg, false, basket)) { refusal = "No priced product to write an entry for"; return null; }
            }
            return new NativeSaleProbe(save, reg, type, family, claimed, fee);
        }

        private static bool IsTicket(string item) => ItemsGetter.GetByName(item)?.HasTag(BigAmbitions.Tags.TagRef.Itemtag.isticket) == true;
        private static Money? Price(string item, BuildingRegistration reg)
            => Money.TryFromNative(ItemHelper.GetPrice(item, reg), out var price) ? price : (Money?)null;
        private static bool Line(string item, BuildingRegistration reg, bool mandatory, List<ProbeLine> basket)
        {
            var price = Price(item, reg);
            if (!price.HasValue) return false;
            basket.Add(new ProbeLine(item, price.Value, mandatory)); return true;
        }

        internal bool Claimed => claimed != null;
        internal string SimulatorName => type.simulator.GetType().Name;

        public ProbeNativeOutcome Sell(IReadOnlyList<ProbeLine> lines)
        {
            process ??= AccessTools.Method(typeof(RetailBusinessSimulator), "ProcessCustomer", new[] { typeof(CustomerEntry) });
            if (process == null) return ProbeNativeOutcome.Interrupted("RetailBusinessSimulator.ProcessCustomer(CustomerEntry) not found");
            // A fresh entry holding only the quoted lines; the claimed native entry is untouched until the sale is confirmed.
            var customer = new CustomerEntry { spawnTime = TimeHelper.Now(), order = new Order {
                entries = lines.Where(l => l.ItemId != fee || !l.Mandatory).Select(l => new OrderEntry { itemName = l.ItemId }).ToList(),
                customerDemandTypes = claimed?.order.customerDemandTypes != null ? new List<string>(claimed.order.customerDemandTypes) : new List<string>() } };
            int orders = reg.unprocessedCompletedOrders.Count;
            var simulator = UnityEngine.Object.Instantiate(type.simulator);
            Exception? failure = null;
            try
            {
                simulator.SetUp(reg, hour);
                // Native setup may run callbacks: recheck everything before the first native effect.
                if (!ReferenceEquals(save, SaveGameManager.Current) || SaveGameManager.Current.Hour != hour || !reg.RentedByPlayer
                    || !reg.HasEstablishedBusiness || !BusinessHelper.IsBusinessOpen(reg) || BuildingManager.IsInsideBuilding
                    || lines.Any(l => Price(l.ItemId, reg) != l.Price))
                    return ProbeNativeOutcome.Interrupted("Save, hour, opening, player position or a price changed during native setup");
                // Native ProcessAllCustomersFromThisHour, not SetUp, sets the theatre's no-actors refusal; check it here.
                if (family == ProbeFamily.CinemaTheater && BusinessHelper.ShouldWarnTheaterHasNoActors(reg))
                    return ProbeNativeOutcome.Interrupted("Theatre has no actors");
                Capture();
                probing = reg;
                try { process.Invoke(simulator, new object[] { customer }); }
                catch (Exception error) { failure = error is TargetInvocationException { InnerException: { } inner } ? inner : error; }
            }
            finally { probing = null; UnityEngine.Object.Destroy(simulator); }
            bool completed = customer.order.completed;
            int added = reg.unprocessedCompletedOrders.Count - orders;
            Evidence["orderCompleted"] = completed; Evidence["ordersAdded"] = added;
            Evidence["entries"] = customer.order.entries.Select(e => new { e.itemName, e.price, e.available, e.priceAccceptable, e.paid }).ToArray();
            if (failure != null) Evidence["nativeException"] = failure.Message;
            // A skipped body (another owner's gate) leaves the order untouched: no native effect.
            if (failure == null && !completed && added == 0 && customer.order.entries.All(e => !e.processed && !e.paid))
                return ProbeNativeOutcome.Interrupted("Native processor skipped the order");
            // Anything but one booked order is reversed: Core calls Undo for a throw. Below, nothing throws.
            if (added != 1 || !completed)
                throw new InvalidOperationException("Native order booking is uncertain (" + added + " orders added)", failure);
            var paid = new List<ProbeLine>();
            var skipped = new List<string>();
            foreach (var entry in customer.order.entries)
            {
                if (!entry.paid || ItemsGetter.GetByName(entry.itemName)?.HasTag(BigAmbitions.Tags.TagRef.Itemtag.isbag) == true) continue;
                // A paid entry at price 0 is a consumable the processor records (a haircut's hair-care product), not a charge.
                if (entry.price == 0f) continue;
                if (Money.TryFromNative(entry.price, out var price)) paid.Add(new ProbeLine(entry.itemName, price, false));
                else skipped.Add(entry.itemName);
            }
            if (skipped.Count > 0) Evidence["unpricedPaidEntries"] = skipped;
            // The hourly batch queues every entry of the hour whatever its completed flag, so the served entry leaves the list.
            // Native regeneration subtracts only the hour's listed entries, not booked orders: a regeneration later in this
            // hour (a price, schedule or furniture change) may add one replacement native entry. The flag shows it on the rig.
            if (claimed != null)
            {
                claimed.completed = true;
                var list = CustomerEntriesHelper.GetEntriesByAddress(reg.Address);
                claimedSlot = list?.IndexOf(claimed) ?? -1;
                if (claimedSlot >= 0) list!.RemoveAt(claimedSlot);
                Evidence["claimedEntryRetired"] = claimedSlot >= 0;
                Evidence["sameHourRegenerationMayAddOneEntry"] = claimedSlot >= 0;
            }
            return ProbeNativeOutcome.Processed(paid, skipped.Count);
        }

        private void Capture()
        {
            stock.Clear(); ordersBefore = reg.unprocessedCompletedOrders.Count;
            if (reg.itemInstances == null || reg.itemInstances.Count > 2000) throw new InvalidOperationException("Unsupported native item list.");
            foreach (var instance in reg.itemInstances.Values)
            {
                var list = instance?.cargoInstances;
                if (list == null) continue;
                var cargo = list.ToArray();
                stock.Add((list, cargo, cargo.Select(c => c?.amount ?? 0).ToArray()));
            }
        }

        /// <summary>Restores the pre-sale stock (cargo lists and amounts), drops orders booked since, and puts the claimed
        /// entry back unserved. Dirt and other furniture wear from processing are not restored.</summary>
        public void Undo()
        {
            if (ordersBefore < 0) return; // Sell stopped before its first native effect
            foreach (var (list, cargo, amounts) in stock)
            {
                list.Clear(); list.AddRange(cargo);
                for (int i = 0; i < cargo.Length; i++) if (cargo[i] != null) cargo[i].amount = amounts[i];
            }
            int added = reg.unprocessedCompletedOrders.Count - ordersBefore;
            if (added < 0) throw new InvalidOperationException("Native orders were removed during the probe; restore refused.");
            reg.unprocessedCompletedOrders.RemoveRange(ordersBefore, added);
            if (claimed != null)
            {
                claimed.completed = false;
                var list = CustomerEntriesHelper.GetEntriesByAddress(reg.Address);
                if (claimedSlot >= 0 && list != null && !list.Contains(claimed)) list.Insert(Math.Min(claimedSlot, list.Count), claimed);
            }
            Evidence["nativeRestored"] = true;
        }
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
                Debug.Log("[Persistent City] sale probe status\n" + JsonConvert.SerializeObject(new { recovered,
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
            // A native save started inside the probe is re-entrant: it publishes no epoch and stops Persistent City afterwards.
            try { RunSaveEpochUnit(() => ran = PurchaseProbe.Run(request, person.Wallet, save.modData, native)); }
            finally { saleProbeRunning = false; }
            result = ran!;
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
