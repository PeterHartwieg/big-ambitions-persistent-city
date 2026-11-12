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
    /// <summary>
    /// GR-3 native sale path, grown from S2's probe. One prepared sale at one player business through the business type's
    /// own simulator asset (the adapter of its <see cref="PurchaseFamily"/>). Core <see cref="PurchaseContract"/> owns the
    /// ceiling, the wallet and the receipt; this class only prices the basket, sells through the native processor and can
    /// restore the native books. The S2 console probe runs through the same path.
    /// </summary>
    internal sealed class NativeSalePath : IPurchaseNative
    {
        private static BuildingRegistration? selling;
        private static MethodInfo? process;
        private const string PatchId = "persistentcity.sale-path";
        private static readonly Harmony patches = new Harmony(PatchId);
        /// <summary>Native automatic restocking (a shelf's last unit refilled from stock boxes or parked-vehicle cargo)
        /// moves stock outside the building image Undo restores, so it does not run inside a sale. The shelf is refilled
        /// at the native closing-hour restock (or by hand); until then it stays empty and later native customers skip it.</summary>
        private static bool SkipRefillWhileSelling() => selling == null;
        /// <summary>True only inside this path's native ProcessCustomer call for this registration; the food authority's
        /// customer gate lets it through as it does the mod's own food checkout.</summary>
        internal static bool IsSelling(BuildingRegistration reg) => ReferenceEquals(selling, reg);

        private readonly object save;
        private readonly BuildingRegistration reg;
        private readonly BusinessType type;
        private readonly CustomerEntry? claimed;
        private readonly string? fee;
        private readonly int hour;
        internal readonly Dictionary<string, object?> Evidence = new Dictionary<string, object?>(StringComparer.Ordinal);
        // Pre-sale image for Undo: every holder's cargo list and amounts, the booked-order count, the claimed entry's slot.
        private readonly List<(List<CargoInstance> list, CargoInstance[] cargo, int[] amounts, float[] prices)> stock = new List<(List<CargoInstance>, CargoInstance[], int[], float[])>();
        private string[] itemIds = Array.Empty<string>();
        private int ordersBefore = -1, claimedSlot = -1;

        private NativeSalePath(object save, BuildingRegistration reg, BusinessType type, PurchaseFamily family, CustomerEntry? claimed, string? fee, List<PurchaseLine> basket)
        { this.save = save; this.reg = reg; this.type = type; Family = family; this.claimed = claimed; this.fee = fee; Basket = basket; hour = SaveGameManager.Current.Hour; }

        public PurchaseFamily Family { get; }
        public IReadOnlyList<PurchaseLine> Basket { get; }
        public int ServedThisHour { get; internal set; }
        public int CapacityThisHour { get; internal set; } = int.MaxValue;
        public int? NativeHourlyCount { get; internal set; }

        /// <summary>The adapter row: exact simulator classes only (J0, S2's log). A subclass is a modded shop with its own
        /// code and stays native, as does any other processor.</summary>
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

        /// <summary>The uncompleted native entries of this hour at this address: what the game itself plans to serve.</summary>
        internal static int NativeEntriesThisHour(BuildingRegistration reg)
        {
            var save = SaveGameManager.Current;
            int count = 0;
            foreach (var entry in CustomerEntriesHelper.GetEntriesByAddress(reg.Address) ?? new List<CustomerEntry>())
                if (entry?.spawnTime != null && entry.spawnTime.Day == save.Day && entry.spawnTime.Hour == save.Hour) count++;
            return count;
        }

        /// <summary>Reads only. The basket is one uncompleted native entry of this hour (J0 step 1); with none it is one unit
        /// of the first priced product, as the food path writes its own entry. The entrance fee is priced up front for every
        /// family whose processor appends it (all but cinema and theatre, whose ticket is already the first order line).</summary>
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
            else if (adapter.NeedsNativeEntry)
            {
                // A written entry would have to rebuild the native ticket-first order; this family needs a native one.
                refusal = "No uncompleted native entry this hour"; return null;
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
            // A fresh entry holding only the quoted lines; the claimed native entry is untouched until the sale is confirmed.
            // The processor appends the entrance fee itself, so the priced fee line is not written into the order.
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
                    return PurchaseNativeOutcome.Interrupted("Save, hour, opening, player position or a price changed during native setup");
                // Native ProcessAllCustomersFromThisHour, not SetUp, sets the theatre's no-actors refusal; check it here.
                if (Family == PurchaseFamily.CinemaTheater && BusinessHelper.ShouldWarnTheaterHasNoActors(reg))
                    return PurchaseNativeOutcome.Refused(PurchaseRefusal.VenueNotReady, "Theatre has no actors");
                var refill = AccessTools.Method(typeof(ReStockingHelper), "RefillSingleItemWithLimit", new[] { typeof(ItemInstance) });
                if (refill == null) return PurchaseNativeOutcome.Interrupted("ReStockingHelper.RefillSingleItemWithLimit(ItemInstance) not found");
                patches.Patch(refill, prefix: new HarmonyMethod(typeof(NativeSalePath), nameof(SkipRefillWhileSelling)) { priority = Priority.First });
                Capture();
                selling = reg;
                try { process.Invoke(simulator, new object[] { customer }); }
                catch (Exception error) { failure = error is TargetInvocationException { InnerException: { } inner } ? inner : error; }
            }
            finally
            {
                selling = null;
                try { patches.UnpatchAll(PatchId); } catch (Exception error) { Evidence["refillUnpatch"] = error.Message; }
                UnityEngine.Object.Destroy(simulator);
            }
            bool completed = customer.order.completed;
            int added = reg.unprocessedCompletedOrders.Count - orders;
            Evidence["orderCompleted"] = completed; Evidence["ordersAdded"] = added;
            Evidence["entries"] = customer.order.entries.Select(e => new { e.itemName, e.price, e.available, e.priceAccceptable, e.paid }).ToArray();
            if (failure != null) Evidence["nativeException"] = failure.Message;
            // A skipped body (another owner's gate, or the family's own not-ready refusal) leaves the order untouched.
            if (failure == null && !completed && added == 0 && customer.order.entries.All(e => !e.processed && !e.paid))
                return PurchaseNativeOutcome.Refused(PurchaseRefusal.VenueNotReady, "Native processor skipped the order (" + PurchaseAdapters.For(Family).NotReady + ")");
            // Anything but one booked order is reversed: Core calls Undo for a throw. Below, nothing throws.
            if (added != 1 || !completed)
                throw new InvalidOperationException("Native order booking is uncertain (" + added + " orders added)", failure);
            var paid = new List<PurchaseLine>();
            var skipped = new List<string>();
            foreach (var entry in customer.order.entries)
            {
                if (!entry.paid || ItemsGetter.GetByName(entry.itemName)?.HasTag(BigAmbitions.Tags.TagRef.Itemtag.isbag) == true) continue;
                // A paid entry at price 0 is a consumable the processor records (a haircut's hair-care product), not a charge.
                if (entry.price == 0f) continue;
                if (Money.TryFromNative(entry.price, out var price)) paid.Add(new PurchaseLine(entry.itemName, price, entry.itemName == fee));
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

        /// <summary>Restores the pre-sale stock (cargo lists, amounts and unit costs), drops orders booked since, and puts
        /// the claimed entry back unserved. Dirt and other furniture wear from processing are not restored. A building item
        /// added or removed during the sale is outside this image: the restore is refused, and Core keeps an uncertain
        /// receipt instead of reporting the books restored.</summary>
        public void Undo()
        {
            if (ordersBefore < 0) return; // Sell stopped before its first native effect
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

    /// <summary>
    /// GR-3 purchase market: the player's supported non-food businesses, for residents with a due need or an impulse.
    /// Attached only when the prototype setting enables it (Q4: the mod takes over customers at every player business only
    /// after S2 and GR-4 are proven in game). Food-owned shops keep the food checkout. Unsupported processors stay native.
    /// </summary>
    internal sealed class NativePurchaseMarket : IPurchaseMarket
    {
        private readonly IDictionary<string, string> receipts;
        private readonly Action<Action> guarded;
        private readonly Dictionary<string, BuildingRegistration> served = new Dictionary<string, BuildingRegistration>(StringComparer.Ordinal);
        private readonly List<string> venues = new List<string>();
        // Contract sales per venue this hour: the service limit counts claimed and written entries alike.
        private readonly Dictionary<string, int> soldThisHour = new Dictionary<string, int>(StringComparer.Ordinal);
        private long refreshedHour = long.MinValue, soldHour = long.MinValue;

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

        /// <summary>One walk over the city's buildings per game hour; ownership and opening change at hour boundaries.</summary>
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
                // A player business may stand behind its shop or its leisure place id; both name the same building.
                foreach (var id in new[] { place, place.Substring(0, place.Length - ":shop".Length) + ":leisure" })
                    if (!served.ContainsKey(id)) { served.Add(id, reg); venues.Add(id); }
            }
        }

        public IPurchaseNative? Open(PurchaseRequest request, out PurchaseRefusal refusal, out string reason)
        {
            refusal = PurchaseRefusal.None; reason = "";
            if (!Serves(request.VenueId) || !served.TryGetValue(request.VenueId, out var reg))
            { refusal = PurchaseRefusal.Unsupported; reason = "Not a supported player business"; return null; }
            if (!BusinessHelper.IsBusinessOpen(reg)) { refusal = PurchaseRefusal.Closed; reason = "Closed"; return null; }
            // The indoor customer path serves the building the player stands in; the mod claims nothing then.
            if (BuildingManager.IsInsideBuilding) { refusal = PurchaseRefusal.PlayerInside; reason = "Player is inside a building"; return null; }
            var native = NativeSalePath.Prepare(reg, out var family, out var why);
            if (native == null) { refusal = family == PurchaseFamily.Unsupported ? PurchaseRefusal.Unsupported : PurchaseRefusal.VenueNotReady; reason = family + ": " + why; return null; }
            long hour = Hour();
            if (hour != soldHour) { soldThisHour.Clear(); soldHour = hour; }
            soldThisHour.TryGetValue(request.VenueId, out int sold);
            native.ServedThisHour = sold;
            native.CapacityThisHour = reg.customerCapacity > 0 ? reg.customerCapacity : int.MaxValue;
            native.NativeHourlyCount = NativeSalePath.NativeEntriesThisHour(reg);
            return new Counted(native, () => soldThisHour[request.VenueId] = sold + 1);
        }

        /// <summary>Counts a confirmed sale against the venue's hour.</summary>
        private sealed class Counted : IPurchaseNative
        {
            private readonly NativeSalePath inner; private readonly Action sold;
            public Counted(NativeSalePath inner, Action sold) { this.inner = inner; this.sold = sold; }
            public PurchaseFamily Family => inner.Family;
            public IReadOnlyList<PurchaseLine> Basket => inner.Basket;
            public int ServedThisHour => inner.ServedThisHour;
            public int CapacityThisHour => inner.CapacityThisHour;
            public int? NativeHourlyCount => inner.NativeHourlyCount;
            public PurchaseNativeOutcome Sell(IReadOnlyList<PurchaseLine> lines)
            {
                var outcome = inner.Sell(lines);
                if (outcome.Sold) sold();
                return outcome;
            }
            public void Undo() => inner.Undo();
        }
    }
}
