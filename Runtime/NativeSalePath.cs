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
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    internal sealed class NativeSalePath : IPurchaseNative
    {
        private static BuildingRegistration? selling;
        private static NativeSalePath? current;
        private static MethodInfo? process, refill, capacity;
        private static bool patched;
        private const string PatchId = "persistentcity.sale-path";
        private static readonly Harmony patches = new Harmony(PatchId);
        private static bool DeferRefillWhileSelling(object[] __args)
        {
            if (selling == null) return true;
            if (current != null && __args.Length == 1 && __args[0] is ItemInstance instance) current.refills.Add(instance);
            return false;
        }

        private static bool EnsurePatched()
        {
            if (patched) return true;
            refill ??= AccessTools.Method(typeof(ReStockingHelper), "RefillSingleItemWithLimit", new[] { typeof(ItemInstance) });
            if (refill == null) return false;
            patches.Patch(refill, prefix: new HarmonyMethod(typeof(NativeSalePath), nameof(DeferRefillWhileSelling)) { priority = Priority.First });
            patched = true;
            return true;
        }

        internal static void Unpatch()
        {
            if (!patched) return;
            patched = false;
            patches.UnpatchAll(PatchId);
        }
        internal static bool IsSelling(BuildingRegistration reg) => ReferenceEquals(selling, reg);

        private readonly object save;
        private readonly BuildingRegistration reg;
        private readonly BusinessType type;
        private readonly CustomerEntry? claimed;
        private readonly string? fee;
        private readonly int hour;
        internal readonly Dictionary<string, object?> Evidence = new Dictionary<string, object?>(StringComparer.Ordinal);
        private readonly List<(List<CargoInstance> list, CargoInstance[] cargo, int[] amounts, float[] prices)> stock = new List<(List<CargoInstance>, CargoInstance[], int[], float[])>();
        private string[] itemIds = Array.Empty<string>();
        private int ordersBefore = -1, claimedSlot = -1;
        private readonly List<ItemInstance> refills = new List<ItemInstance>();

        private NativeSalePath(object save, BuildingRegistration reg, BusinessType type, PurchaseFamily family, CustomerEntry? claimed, string? fee, List<PurchaseLine> basket)
        { this.save = save; this.reg = reg; this.type = type; Family = family; this.claimed = claimed; this.fee = fee; Basket = basket; hour = SaveGameManager.Current.Hour; }

        public PurchaseFamily Family { get; }
        public IReadOnlyList<PurchaseLine> Basket { get; }
        public int ServedThisHour { get; internal set; }
        public int CapacityThisHour { get; internal set; }
        public int? NativeHourlyCount { get; internal set; }

        internal static PurchaseFamily FamilyOf(BusinessSimulator? simulator)
        {
            var t = simulator?.GetType();
            return t == typeof(FullServiceBusinessSimulator) ? PurchaseFamily.FullService
                : t == typeof(SelfServiceBusinessSimulator) ? PurchaseFamily.SelfService
                : t == typeof(HairdresserBusinessSimulator) ? PurchaseFamily.Hairdresser
                : t == typeof(GymBusinessSimulator) ? PurchaseFamily.Gym
                : t == typeof(CinemaTheaterBusinessSimulator) ? PurchaseFamily.CinemaTheater
                : t == typeof(NightclubBusinessSimulator) ? PurchaseFamily.Nightclub : PurchaseFamily.Unsupported;
        }

        internal static bool FoodOwned(BuildingRegistration reg)
            => NativeFoodOwnership.Row(NativeFoodMarket.PlaceId(reg))?.OwnsHour(NativeFoodOwnership.Hour) == true;

        internal static int NativeEntriesThisHour(BuildingRegistration reg)
        {
            var save = SaveGameManager.Current;
            int count = 0;
            foreach (var entry in CustomerEntriesHelper.GetEntriesByAddress(reg.Address) ?? new List<CustomerEntry>())
                if (entry?.spawnTime != null && entry.spawnTime.Day == save.Day && entry.spawnTime.Hour == save.Hour) count++;
            return count;
        }

        internal static NativeSalePath? Prepare(BuildingRegistration reg, out PurchaseFamily family, out string refusal)
        {
            var basket = new List<PurchaseLine>(); family = PurchaseFamily.Unsupported; refusal = "";
            var type = BusinessTypeHelper.GetData(reg.businessTypeName);
            family = FamilyOf(type?.simulator);
            if (type == null || family == PurchaseFamily.Unsupported) { refusal = "Unsupported simulator " + (type?.simulator?.GetType().FullName ?? "none"); return null; }
            var adapter = PurchaseAdapters.For(family);
            var save = SaveGameManager.Current;
            CustomerEntry? claimed = null;
            if (!FoodOwned(reg))
                foreach (var entry in CustomerEntriesHelper.GetEntriesByAddress(reg.Address) ?? new List<CustomerEntry>())
                    if (entry != null && !entry.completed && entry.spawnTime != null && entry.spawnTime.Day == save.Day && entry.spawnTime.Hour == save.Hour
                        && entry.order?.entries != null && entry.order.entries.Count > 0 && entry.order.entries.Count <= 32)
                    { claimed = entry; break; }
            string? fee = adapter.AppendsEntranceFee && type.hasEntranceFee ? BusinessTypeHelper.GetEntranceFeeNameForBusinessType(type) : null;
            if (fee != null && !Line(fee, reg, true, basket)) { refusal = "Entrance fee " + fee + " has no supported price"; return null; }
            if (claimed != null)
            {
                foreach (var entry in claimed.order.entries)
                    if (entry == null || !Line(entry.itemName, reg, IsTicket(entry.itemName), basket)) { refusal = "Claimed entry has an unpriced line"; return null; }
            }
            else if (adapter.TicketFirst)
            {
                string? ticket = reg.cachedAvailableProducts?.FirstOrDefault(p => IsTicket(p) && Price(p, reg).HasValue);
                if (ticket == null || !Line(ticket, reg, true, basket)) { refusal = "No priced ticket to write an entry for"; return null; }
            }
            else
            {
                string? product = reg.cachedAvailableProducts?.FirstOrDefault(p => p != fee && !IsTicket(p) && Price(p, reg).HasValue);
                if (product == null || !Line(product, reg, false, basket)) { refusal = "No priced product to write an entry for"; return null; }
            }
            return new NativeSalePath(save, reg, type, family, claimed, fee, basket);
        }

        private static bool IsTicket(string item) => ItemsGetter.GetByName(item)?.HasTag(BigAmbitions.Tags.TagRef.Itemtag.isticket) == true;
        private static Money? Price(string item, BuildingRegistration reg)
            => Money.TryFromNative(ItemHelper.GetPrice(item, reg), out var price) ? price : (Money?)null;
        private static bool Line(string item, BuildingRegistration reg, bool mandatory, List<PurchaseLine> basket)
        {
            var price = Price(item, reg);
            if (!price.HasValue) return false;
            basket.Add(new PurchaseLine(item, price.Value, mandatory)); return true;
        }

        internal bool Claimed => claimed != null;
        internal string SimulatorName => type.simulator.GetType().Name;

        public PurchaseNativeOutcome Sell(IReadOnlyList<PurchaseLine> lines)
        {
            process ??= AccessTools.Method(typeof(RetailBusinessSimulator), "ProcessCustomer", new[] { typeof(CustomerEntry) });
            if (process == null) return PurchaseNativeOutcome.Interrupted("RetailBusinessSimulator.ProcessCustomer(CustomerEntry) not found");
            var customer = new CustomerEntry { spawnTime = TimeHelper.Now(), order = new Order {
                entries = lines.Where(l => l.ItemId != fee || !l.Mandatory).Select(l => new OrderEntry { itemName = l.ItemId }).ToList(),
                customerDemandTypes = claimed?.order.customerDemandTypes != null ? new List<string>(claimed.order.customerDemandTypes) : new List<string>() } };
            int orders = reg.unprocessedCompletedOrders.Count;
            var simulator = UnityEngine.Object.Instantiate(type.simulator);
            Exception? failure = null;
            try
            {
                simulator.SetUp(reg, hour);
                if (!ReferenceEquals(save, SaveGameManager.Current) || SaveGameManager.Current.Hour != hour || !reg.RentedByPlayer
                    || !reg.HasEstablishedBusiness || !BusinessHelper.IsBusinessOpen(reg) || BuildingManager.IsInsideBuilding
                    || lines.Any(l => Price(l.ItemId, reg) != l.Price))
                    return PurchaseNativeOutcome.Interrupted("Save, hour, opening, player position or a price changed during native setup");
                if (Family == PurchaseFamily.CinemaTheater && BusinessHelper.ShouldWarnTheaterHasNoActors(reg))
                    return PurchaseNativeOutcome.Refused(PurchaseRefusal.VenueNotReady, "Theatre has no actors");
                capacity ??= AccessTools.Method(typeof(RetailBusinessSimulator), "GetMaxCustomerCapacityThisHour");
                if (capacity == null || capacity.GetParameters().Length != 0)
                    return PurchaseNativeOutcome.Interrupted("RetailBusinessSimulator.GetMaxCustomerCapacityThisHour() not found");
                int staffed = Convert.ToInt32(capacity.Invoke(simulator, null));
                Evidence["staffedCapacityThisHour"] = staffed;
                if (ServedThisHour >= staffed)
                    return PurchaseNativeOutcome.Refused(PurchaseRefusal.VenueFull, "Venue served its staffed capacity this hour (" + staffed + ")");
                if (!EnsurePatched()) return PurchaseNativeOutcome.Interrupted("ReStockingHelper.RefillSingleItemWithLimit(ItemInstance) not found");
                Capture();
                refills.Clear();
                selling = reg; current = this;
                try { process.Invoke(simulator, new object[] { customer }); }
                catch (Exception error) { failure = error is TargetInvocationException { InnerException: { } inner } ? inner : error; }
            }
            finally
            {
                selling = null; current = null;
                UnityEngine.Object.Destroy(simulator);
            }
            bool completed = customer.order.completed;
            int added = reg.unprocessedCompletedOrders.Count - orders;
            Evidence["orderCompleted"] = completed; Evidence["ordersAdded"] = added;
            Evidence["entries"] = customer.order.entries.Select(e => new { e.itemName, e.price, e.available, e.priceAccceptable, e.paid }).ToArray();
            if (failure != null) Evidence["nativeException"] = failure.Message;
            if (failure == null && !completed && added == 0 && customer.order.entries.All(e => !e.processed && !e.paid))
                return PurchaseNativeOutcome.Refused(PurchaseRefusal.VenueNotReady, "Native processor skipped the order (" + PurchaseAdapters.For(Family).NotReady + ")");
            if (added != 1 || !completed)
                throw new InvalidOperationException("Native order booking is uncertain (" + added + " orders added)", failure);
            var paid = new List<PurchaseLine>();
            var skipped = new List<string>();
            foreach (var entry in customer.order.entries)
            {
                if (!entry.paid || ItemsGetter.GetByName(entry.itemName)?.HasTag(BigAmbitions.Tags.TagRef.Itemtag.isbag) == true) continue;
                if (entry.price == 0f) continue;
                if (Money.TryFromNative(entry.price, out var price)) paid.Add(new PurchaseLine(entry.itemName, price, entry.itemName == fee));
                else skipped.Add(entry.itemName);
            }
            if (skipped.Count > 0) Evidence["unpricedPaidEntries"] = skipped;
            if (claimed != null)
            {
                claimed.completed = true;
                var list = CustomerEntriesHelper.GetEntriesByAddress(reg.Address);
                claimedSlot = list?.IndexOf(claimed) ?? -1;
                if (claimedSlot >= 0) list!.RemoveAt(claimedSlot);
                Evidence["claimedEntryRetired"] = claimedSlot >= 0;
                Evidence["sameHourRegenerationMayAddOneEntry"] = claimedSlot >= 0;
            }
            return PurchaseNativeOutcome.Processed(paid, skipped.Count);
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
                stock.Add((list, cargo, cargo.Select(c => c?.amount ?? 0).ToArray(), cargo.Select(c => c?.pricePerUnit ?? 0f).ToArray()));
            }
            itemIds = reg.itemInstances.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
        }

        public void Commit()
        {
            var pending = refills.ToArray();
            refills.Clear();
            int done = 0;
            foreach (var instance in pending)
            {
                try { refill!.Invoke(null, new object[] { instance }); done++; }
                catch (Exception error) { Evidence["refillFailed"] = (error is TargetInvocationException { InnerException: { } inner } ? inner : error).Message; }
            }
            Evidence["refillsReplayed"] = done;
        }

        public void Undo()
        {
            refills.Clear();
            if (ordersBefore < 0) return;
            if (!reg.itemInstances.Keys.OrderBy(k => k, StringComparer.Ordinal).SequenceEqual(itemIds, StringComparer.Ordinal))
                throw new InvalidOperationException("Building items changed during the sale; restore refused.");
            foreach (var (list, cargo, amounts, prices) in stock)
            {
                list.Clear(); list.AddRange(cargo);
                for (int i = 0; i < cargo.Length; i++) if (cargo[i] != null) { cargo[i].amount = amounts[i]; cargo[i].pricePerUnit = prices[i]; }
            }
            int added = reg.unprocessedCompletedOrders.Count - ordersBefore;
            if (added < 0) throw new InvalidOperationException("Native orders were removed during the sale; restore refused.");
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

    internal sealed class NativePurchaseMarket : IPurchaseMarket
    {
        private readonly IDictionary<string, string> receipts;
        private readonly Action<Action> guarded;
        private readonly Dictionary<string, BuildingRegistration> served = new Dictionary<string, BuildingRegistration>(StringComparer.Ordinal);
        private readonly List<string> venues = new List<string>();
        private readonly Dictionary<string, int> soldThisHour = new Dictionary<string, int>(StringComparer.Ordinal);
        private long refreshedHour = long.MinValue, soldHour = long.MinValue;
        private int prunedDay = int.MinValue;

        internal NativePurchaseMarket(IDictionary<string, string> receipts, Action<Action> guarded)
        {
            this.receipts = receipts ?? throw new ArgumentNullException(nameof(receipts));
            this.guarded = guarded ?? throw new ArgumentNullException(nameof(guarded));
        }

        public IDictionary<string, string> Receipts => receipts;
        public void Guarded(Action unit) => guarded(unit);
        public IReadOnlyList<string> Venues { get { Refresh(); return venues; } }
        public bool Serves(string venueId) { Refresh(); return served.ContainsKey(venueId); }

        private static long Hour()
        {
            var save = SaveGameManager.Current;
            return save == null ? long.MinValue : save.Day * 24L + save.Hour;
        }

        private void Refresh()
        {
            long hour = Hour();
            if (hour == refreshedHour) return;
            refreshedHour = hour;
            served.Clear(); venues.Clear();
            var controllers = CityManager.Instance?.cityBuildingControllers;
            if (controllers == null) return;
            foreach (var c in controllers)
            {
                var reg = c?.buildingRegistration;
                if (reg == null || !reg.RentedByPlayer || !reg.HasEstablishedBusiness || NativeSalePath.FoodOwned(reg)) continue;
                if (NativeSalePath.FamilyOf(BusinessTypeHelper.GetData(reg.businessTypeName)?.simulator) == PurchaseFamily.Unsupported) continue;
                string place = NativeFoodMarket.PlaceId(reg);
                foreach (var id in new[] { place, Leisure(place) })
                    if (!served.ContainsKey(id)) { served.Add(id, reg); venues.Add(id); }
            }
        }

        private static string Leisure(string shop) => shop.Substring(0, shop.Length - ":shop".Length) + ":leisure";

        public IPurchaseNative? Open(PurchaseRequest request, out PurchaseRefusal refusal, out string reason)
        {
            refusal = PurchaseRefusal.None; reason = "";
            int day = (int)(request.Minute / 1440);
            if (day != prunedDay) { prunedDay = day; PurchaseContract.Prune(receipts, day); }
            if (!Serves(request.VenueId) || !served.TryGetValue(request.VenueId, out var reg))
            { refusal = PurchaseRefusal.Unsupported; reason = "Not a supported player business"; return null; }
            if (!BusinessHelper.IsBusinessOpen(reg)) { refusal = PurchaseRefusal.Closed; reason = "Closed"; return null; }
            if (BuildingManager.IsInsideBuilding) { refusal = PurchaseRefusal.PlayerInside; reason = "Player is inside a building"; return null; }
            var native = NativeSalePath.Prepare(reg, out var family, out var why);
            if (native == null) { refusal = family == PurchaseFamily.Unsupported ? PurchaseRefusal.Unsupported : PurchaseRefusal.VenueNotReady; reason = family + ": " + why; return null; }
            long hour = request.Minute / 60;
            if (hour != soldHour)
            {
                soldThisHour.Clear(); soldHour = hour;
                foreach (var pair in PurchaseContract.ServedInHour(receipts, hour)) soldThisHour[pair.Key] = pair.Value;
            }
            string building = PurchaseContract.Building(NativeFoodMarket.PlaceId(reg));
            soldThisHour.TryGetValue(building, out int sold);
            native.ServedThisHour = sold;
            native.CapacityThisHour = Math.Max(0, reg.customerCapacity);
            native.NativeHourlyCount = NativeSalePath.NativeEntriesThisHour(reg);
            return new Counted(native, delta => { soldThisHour.TryGetValue(building, out int now); soldThisHour[building] = Math.Max(0, now + delta); });
        }

        private sealed class Counted : IPurchaseNative
        {
            private readonly NativeSalePath inner; private readonly Action<int> count;
            private bool counted;
            public Counted(NativeSalePath inner, Action<int> count) { this.inner = inner; this.count = count; }
            public PurchaseFamily Family => inner.Family;
            public IReadOnlyList<PurchaseLine> Basket => inner.Basket;
            public int ServedThisHour => inner.ServedThisHour;
            public int CapacityThisHour => inner.CapacityThisHour;
            public int? NativeHourlyCount => inner.NativeHourlyCount;
            public PurchaseNativeOutcome Sell(IReadOnlyList<PurchaseLine> lines)
            {
                var outcome = inner.Sell(lines);
                if (outcome.Sold) { count(1); counted = true; }
                return outcome;
            }
            public void Undo()
            {
                inner.Undo();
                if (counted) { count(-1); counted = false; }
            }
            public void Commit() => inner.Commit();
        }
    }
}
