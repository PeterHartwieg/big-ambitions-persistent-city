using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using UnityEngine;
using UnityEngine.AI;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace PersistentCity.Runtime
{
    /// <summary>Runtime's only stand-height dependency. The query can be replaced by an offline mesh double.</summary>
    public sealed class VendorNavMeshSampler : IVendorStandSampler
    {
        private readonly Func<Position, double, Position?> query;
        public VendorNavMeshSampler() : this(Query) { }
        public VendorNavMeshSampler(Func<Position, double, Position?> query) { this.query = query; }
        private static Position? Query(Position p, double radius)
            => NavMesh.SamplePosition(new Vector3((float)p.X, (float)p.Y, (float)p.Z), out var hit, (float)radius, NavMesh.AllAreas)
                ? new Position(hit.position.x, hit.position.y, hit.position.z) : (Position?)null;
        public bool TrySample(Position stand, double radiusMetres, out Position point)
        {
            point = default;
            if (!stand.IsFinite || !Position.Finite(radiusMetres) || radiusMetres < 0 || radiusMetres > VendorRules.StandRadiusMetres) return false;
            var found = query(stand, radiusMetres);
            if (!found.HasValue || !found.Value.IsFinite || Position.Distance(stand, found.Value) > radiusMetres) return false;
            point = found.Value; return true;
        }
    }

    /// <summary>Street Food owns stock, ledgers and player money. PC holds only an exact receipt for one API sale.</summary>
    public sealed class StreetFoodVendorMarket : IFoodMarket, ILocalFoodMarket, IFoodCandidateMarket, IFoodBusinessAuthority,
        IFoodSaleEvidence, IFoodFailureMarket, IFoodAwarenessRevision, IDisposable
    {
        private CitySimulator city;
        private readonly FoodCommerceState state;
        private readonly IVendorApi? api;
        private readonly IDictionary<string, string> receipts;
        private readonly Func<double> nativeMinute;
        private readonly Func<bool> sameSave;
        private readonly VendorVehicle before = new VendorVehicle(), after = new VendorVehicle();
        private readonly Dictionary<string, long> withdrawn = new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly VendorReferences references = new VendorReferences();
        private long polled = -1;
        private int receiptDay = -1;
        private long withdrawalMinute = -1;
        private bool pending, disposed, capturing;
        private double captureMinute;
        public StreetFoodVendorMarket(CitySimulator city, IVendorApi? api, string unavailableReason, IVendorStandSampler sampler,
            IDictionary<string, string> receipts, Func<double> nativeMinute, Func<bool> sameSave)
        {
            this.city = city; state = city.State.Food ?? throw new InvalidOperationException("Vendor market needs food state.");
            this.api = api == null ? null : new ExactPriceApi(api); this.receipts = receipts; this.nativeMinute = nativeMinute; this.sameSave = sameSave;
            ValidatePopulation(state, city.State.Vendors, receipts);
            Coordinator = new VendorCoordinator(city.State.Vendors, this.api, unavailableReason, new Places(this), sampler);
            // A saved handback in progress remains withdrawn while its committed residents drain.
            foreach (var row in city.State.Vendors.Rows)
                if (!row.Authority.Desired && row.Authority.Managed && row.Authority.OpeningId >= 0)
                    withdrawn[row.VehicleId] = row.Authority.OpeningId;
        }
        private static string? PriceRefusal(VendorVehicle v)
        {
            for (int i = 0; i < v.ItemCount && i < VendorVehicle.MaximumItems; i++)
                if (v.PriceCents[i] > long.MaxValue / Money.UnitsPerCent)
                    return "price outside exact resident money range: " + v.Items[i];
            return null;
        }
        // Reject ownership requests before Street Food sees them when PC cannot represent the complete menu.
        // This adapter preserves the public snapshot and never edits a Street Food-owned value.
        private sealed class ExactPriceApi : IVendorApi
        {
            private readonly IVendorApi inner;
            private readonly Dictionary<string, string> refused = new Dictionary<string, string>(StringComparer.Ordinal);
            public ExactPriceApi(IVendorApi inner) { this.inner = inner; }
            public long Revision => inner.Revision;
            public VendorApiStatus BeginRead(out int count)
            {
                var status = inner.BeginRead(out count);
                if (status == VendorApiStatus.Ready) refused.Clear();
                return status;
            }
            public void CopyVehicle(int index, VendorVehicle into)
            {
                inner.CopyVehicle(index, into);
                string? reason = PriceRefusal(into);
                if (reason != null) refused[into.VehicleId] = reason;
            }
            public VendorApiStatus RequestOwner(string id, bool want, out string reason)
            {
                if (want && refused.TryGetValue(id, out var refusal)) { reason = refusal; return VendorApiStatus.Refused; }
                return inner.RequestOwner(id, want, out reason);
            }
            public VendorApiStatus Renew(int day, int minute, out int untilDay, out int untilMinute) => inner.Renew(day, minute, out untilDay, out untilMinute);
            public VendorApiStatus ReleaseAll() => inner.ReleaseAll();
            public VendorApiStatus Sell(string id, long opening, string item, long cents, int day, int minute,
                out VendorSaleResult? sale, out bool uncertain, out string reason)
                => inner.Sell(id, opening, item, cents, day, minute, out sale, out uncertain, out reason);
        }
        // Route retiming replaces the simulator while preserving the same saved population and market.
        public void Rebind(CitySimulator replacement)
        {
            if (!ReferenceEquals(replacement.State.Food, state)) throw new InvalidOperationException("Vendor city changed.");
            city = replacement;
        }
        private sealed class Places : IVendorPlaces
        {
            private readonly StreetFoodVendorMarket owner;
            public Places(StreetFoodVendorMarket owner) { this.owner = owner; }
            public void PublishVendorPlace(VendorRow row, VendorVehicle? menu) => owner.city.PublishVendorPlace(row, menu);
            public void RemoveVendorPlace(VendorRow row) => owner.city.RemoveVendorPlace(row);
            public void CollectVendorReferences(VendorReferences into) => owner.city.CollectVendorReferences(into);
        }
        public VendorCoordinator Coordinator { get; }
        public double EarliestMinute => Math.Floor(nativeMinute());
        public long AwarenessRevision { get; private set; }
        public long Purchases { get; private set; }
        public long WorkSpikes { get; private set; }
        public double MaximumWorkMilliseconds { get; private set; }
        public Action<string, double>? WorkSpike { get; set; }
        private void RecordWork(string component, long started)
        {
            double ms = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
            MaximumWorkMilliseconds = Math.Max(MaximumWorkMilliseconds, ms);
            if (ms < 30) return;
            WorkSpikes++; WorkSpike?.Invoke(component, ms);
        }

        /// <summary>Logical-event preparation: one poll per native minute, then one bounded copy/apply unit per frame.
        /// The caller waits before advancing events while true. No per-frame vehicle scan.</summary>
        public bool Prepare(long minute)
        {
            long started = Stopwatch.GetTimestamp();
            try { return PrepareUnit(minute); } finally { RecordWork("prepare", started); }
        }
        private bool PrepareUnit(long minute)
        {
            if (disposed || !sameSave()) return false;
            if (polled != minute)
            {
                polled = minute;
                if (receiptDay != minute / 1440)
                {
                    receiptDay = (int)(minute / 1440);
                    FoodSaleReceipts.Prune(receipts, receiptDay, state.Residents.Where(m => m.Status == MealStatus.Uncertain).Select(m => m.IntentId));
                }
                pending = Coordinator.Poll(minute);
            }
            if (pending)
            {
                pending = Coordinator.Continue(minute);
                if (!pending) RefreshMenus();
            }
            ApplyWithdrawals(minute);
            return pending;
        }
        private void RefreshMenus()
        {
            var image = Coordinator.Image;
            if (image?.Ready != true) return;
            foreach (var row in Coordinator.Table.Rows)
            {
                var v = image.Find(row.VehicleId);
                if (v == null) continue;
                string? priceRefusal = PriceRefusal(v);
                if (priceRefusal != null)
                {
                    if (row.Authority.OpeningId >= 0 && !withdrawn.ContainsKey(row.VehicleId)) Withdraw(row.VehicleId);
                    row.Authority.Desired = false; row.Reason = priceRefusal;
                    continue;
                }
                if (!Coordinator.CanServe(row.BusinessId, polled)) continue;
                var scope = state.Shops.Find(s => s.Channel == FoodScopeChannel.Vehicle && s.PlaceId == row.PlaceId);
                if (scope != null && !scope.Products().SequenceEqual(v.Items.Take(v.ItemCount), StringComparer.Ordinal))
                    city.PublishVendorPlace(row, v);
            }
        }
        private VendorRow? Row(string business)
        {
            // Saved scope/place strings are stable. Avoid constructing every row's generation id on every read.
            var scope = state.Shops.Find(s => s.Channel == FoodScopeChannel.Vehicle && s.BusinessId == business);
            if (scope == null) return null;
            var row = Coordinator.Table.FindPlace(scope.PlaceId);
            return row?.BusinessId == business ? row : null;
        }
        public bool CanServe(string id) => !disposed && sameSave() && Coordinator.CanServe(id, (long)EarliestMinute)
            && Row(id) is VendorRow row && !withdrawn.ContainsKey(row.VehicleId)
            && Coordinator.Image?.Find(row.VehicleId) is VendorVehicle v && PriceRefusal(v) == null;
        public bool IsCandidate(FoodScope scope) => scope.Channel == FoodScopeChannel.Vehicle && CanServe(scope.BusinessId);
        public bool IsConfirmedPaid(ResidentMeal meal) => sameSave() && FoodSaleReceipts.ReadStatus(receipts, meal) == FoodReceiptStatus.Paid;
        public IReadOnlyList<FoodBusiness> Read(double minute)
        {
            long started = Stopwatch.GetTimestamp();
            try { return state.Shops.Where(s => s.Channel == FoodScopeChannel.Vehicle).Select(s => ReadBusiness(s.BusinessId, minute)).Where(b => b != null).Cast<FoodBusiness>().ToArray(); }
            finally { RecordWork("read", started); }
        }
        public IReadOnlyList<FoodBusiness> ReadLocal(Place origin, double minute, double tolerance, int maximumCandidates)
        {
            if (maximumCandidates < 1 || maximumCandidates > 8 || !Position.Finite(tolerance) || tolerance < 0) throw new ArgumentException("Invalid local food observation bound.");
            return state.Shops.Where(IsCandidate).Select(s => new { Scope = s, Distance = Position.Distance(origin.Position, city.FindPlace(s.PlaceId).Position) })
                .Where(x => x.Distance <= tolerance).OrderBy(x => x.Distance).ThenBy(x => x.Scope.BusinessId, StringComparer.Ordinal).Take(maximumCandidates)
                .Select(x => ReadBusiness(x.Scope.BusinessId, minute)!).ToArray();
        }
        public FoodBusiness? ReadBusiness(string id, double minute)
        {
            if (!CanServe(id)) return null;
            var row = Row(id)!; var v = Coordinator.Image!.Find(row.VehicleId)!;
            var b = new FoodBusiness { Id = id, PlaceId = row.PlaceId, Name = "Street Food " + row.Kind, MealsPerHour = v.MealsPerHour,
                Hours = VendorRules.StandHours(row.Stand!), ServiceEvidence = "Street Food vendor API exact supply and serving rate",
                NextServiceMinute = minute };
            for (int i = 0; i < v.ItemCount; i++) b.Offers.Add(new FoodOffer { ProductId = v.Items[i], Price = Money.FromCents(v.PriceCents[i]), Units = v.Units[i] });
            return b;
        }
        public FoodBusiness? ReadFailureBusiness(string id, string place, double minute)
        {
            var row = Row(id);
            if (row == null || row.PlaceId != place) return null;
            return ReadBusiness(id, minute) ?? new FoodBusiness { Id = id, PlaceId = place, Name = "Street Food " + row.Kind,
                MealsPerHour = 0, ServiceUnavailableReason = row.Reason.Length == 0 ? Coordinator.Reason : row.Reason };
        }
        private sealed class Capture : IDisposable
        {
            private StreetFoodVendorMarket? owner;
            public Capture(StreetFoodVendorMarket owner) { this.owner = owner; }
            public void Dispose() { if (owner != null) owner.capturing = false; owner = null; }
        }
        public IDisposable BeginCapture(double minute)
        {
            if (capturing || disposed || !sameSave()) throw new InvalidOperationException("Vendor capture unavailable or nested.");
            capturing = true; captureMinute = minute; return new Capture(this);
        }
        public FoodBusiness? CaptureBusiness(string id, double minute)
        {
            long started = Stopwatch.GetTimestamp();
            try
            {
                if (capturing && captureMinute != minute) throw new InvalidOperationException("Vendor capture minute changed.");
                return ReadBusiness(id, minute);
            }
            finally { RecordWork("capture", started); }
        }

        private bool ReadLive(string vehicle, VendorVehicle into)
        {
            if (api == null || api.BeginRead(out int count) != VendorApiStatus.Ready) return false;
            if (count < 0 || count > VendorImage.MaximumVehicles) return false;
            // Usually the detached view keeps its order. Verify the identity at the saved slot before
            // reusing it; a changed order falls back to the bounded scan, never to another vehicle.
            var image = Coordinator.Image;
            int hint = -1;
            if (image != null) for (int i = 0; i < image.Count; i++) if (image[i].VehicleId == vehicle) { hint = i; break; }
            if (hint >= 0 && hint < count)
            {
                into.ShapeFault = ""; api.CopyVehicle(hint, into);
                if (into.VehicleId == vehicle) return into.Check() == null;
            }
            for (int i = 0; i < count; i++)
            {
                if (i == hint) continue;
                into.ShapeFault = ""; api.CopyVehicle(i, into);
                if (into.VehicleId == vehicle) return into.Check() == null;
            }
            return false;
        }
        private bool LiveOpening(VendorRow row, VendorVehicle v, long minute) => v.OwnedByPersistentCity && v.Kind == row.Kind
            && v.OpeningId == row.Stand?.OpeningId && row.DeferredStand == null && row.Stand.Published
            && VendorRules.OpeningRefusal(v) == null && VendorRules.MenuRefusal(v, VendorRules.OriginalThree) == null
            && minute >= v.OpenedAt && minute < v.ClosesAt && Coordinator.Lease.Valid(minute)
            && Position.Distance(v.Window, row.Stand.Position) <= VendorRules.StandRadiusMetres;
        public FoodSale Buy(ResidentMeal meal, double minute)
        {
            long started = Stopwatch.GetTimestamp();
            try { return BuyLive(meal, minute); } finally { RecordWork("checkout", started); }
        }
        private FoodSale BuyLive(ResidentMeal meal, double minute)
        {
            if (meal.TargetKind != FoodAlternativeKind.Player) return new FoodSale(false, "Rivals have no vendor checkout authority");
            long now = (long)EarliestMinute;
            if (disposed || !sameSave() || !Position.Finite(minute) || Math.Floor(minute) != now)
                return new FoodSale(false, "Native clock differs from service time");
            if (FoodSaleReceipts.AlreadyPaid(receipts, meal)) return new FoodSale(true, "Saved vendor receipt already confirms this intention");
            var row = Row(meal.BusinessId);
            if (row == null || row.PlaceId != meal.PlaceId || !CanServe(meal.BusinessId))
                return new FoodSale(false, "Vehicle is no longer serving this stand", ShopExperience.Closed);
            if (!ReadLive(row.VehicleId, before) || !LiveOpening(row, before, now))
                return new FoodSale(false, "Vehicle closed or stand changed before service", ShopExperience.Closed);
            int item = before.ItemIndex(meal.ProductId);
            if (item < 0 || !meal.Price.IsWholeCents() || before.PriceCents[item] != meal.Price.Units / Money.UnitsPerCent)
                return new FoodSale(false, "Approved price or product changed");
            if (before.Units[item] == 0) return new FoodSale(false, "Confirmed stockout before service", ShopExperience.Stockout);
            if (meal.Price <= Money.Zero || meal.Price > meal.Allowance || before.MealsPerHour <= 0
                || !Position.Finite(meal.ServiceMinutes) || meal.ServiceMinutes + 1e-7 < 60.0 / before.MealsPerHour)
                return new FoodSale(false, "Stock, allowance or staffing changed");
            FoodSaleReceipts.Begin(receipts, meal);
            try
            {
                var status = api!.Sell(row.VehicleId, before.OpeningId, meal.ProductId, before.PriceCents[item], (int)(now / 1440), (int)(now % 1440),
                    out var sale, out bool uncertain, out string reason);
                // A definite refusal is the only path allowed to remove pending evidence.
                if (!uncertain && sale == null && status >= VendorApiStatus.Off && status <= VendorApiStatus.Failed)
                {
                    FoodSaleReceipts.Cancel(receipts, meal);
                    return new FoodSale(false, "Street Food refused before effects: " + reason);
                }
                if (uncertain || status != VendorApiStatus.Ready || sale == null || sale.BookedDay != now / 1440
                    || sale.Cents != before.PriceCents[item] || sale.UnitsBefore != before.Units[item]
                    || sale.UnitsAfter != sale.UnitsBefore - 1 || sale.Sequence <= 0
                    || !ReadLive(row.VehicleId, after) || after.OpeningId != before.OpeningId || after.ItemCount != before.ItemCount)
                    throw new InvalidOperationException("Vendor sale evidence mismatch");
                for (int i = 0; i < before.ItemCount; i++)
                    if (after.Items[i] != before.Items[i] || after.Units[i] != before.Units[i] - (i == item ? 1 : 0))
                        throw new InvalidOperationException("Vendor product or units changed during checkout");
                FoodSaleReceipts.Confirm(receipts, meal);
                Purchases++;
                // Keep same-minute captures exact without another revision poll or another API read.
                var snapshot = Coordinator.Image?.Find(row.VehicleId);
                if (snapshot != null && snapshot.OpeningId == after.OpeningId && snapshot.ItemCount == after.ItemCount)
                    for (int i = 0; i < after.ItemCount; i++) { snapshot.Units[i] = after.Units[i]; snapshot.PriceCents[i] = after.PriceCents[i]; }
                return new FoodSale(true, "Street Food confirmed exact cents, day and one unit");
            }
            catch (Exception error) { throw new InvalidOperationException("Vendor sale outcome is uncertain; do not retry.", error); }
        }

        public bool Withdraw(string vehicle)
        {
            var row = Coordinator.Table.Find(vehicle);
            if (row == null || row.State != VendorRowState.Live || row.Authority.OpeningId < 0 || disposed || !sameSave()) return false;
            withdrawn[vehicle] = Coordinator.Image?.Find(vehicle)?.OpeningId ?? row.Authority.OpeningId;
            row.Authority.Desired = false; row.Reason = "diagnostic withdrawal; waiting for committed work";
            AwarenessRevision++; withdrawalMinute = -1; ApplyWithdrawals((long)EarliestMinute); return true;
        }
        private void ApplyWithdrawals(long minute)
        {
            if (withdrawn.Count == 0 || api == null) return;
            foreach (var id in withdrawn.Keys)
            {
                var row = Coordinator.Table.Find(id);
                if (row != null) row.Authority.Desired = false;
            }
            // Drain checks scan population references once per logical minute, never once per rendered frame.
            if (withdrawalMinute == minute) return;
            withdrawalMinute = minute;
            city.CollectVendorReferences(references);
            foreach (var id in withdrawn.Keys.ToArray())
            {
                var row = Coordinator.Table.Find(id); var v = Coordinator.Image?.Find(id);
                if (row == null) { withdrawn.Remove(id); continue; }
                if (v != null && v.OpeningId != withdrawn[id]) { withdrawn.Remove(id); continue; }
                row.Authority.Desired = false;
                if (references.ShortLived.Contains(row.PlaceId)) continue;
                if (!row.Authority.Requested && !row.Authority.Managed && row.Authority.OpeningId < 0) { withdrawn.Remove(id); continue; }
                if (api.RequestOwner(id, false, out _) != VendorApiStatus.Ready) { row.Authority.HandbackPending = true; continue; }
                row.Authority.RequestedHour = -1; row.Authority.OpeningId = -1; row.Authority.UntilHour = minute / 60;
                row.Authority.HandbackPending = false;
                // The fresh snapshot will request a later opening. This handed-back opening cannot be reacquired.
                withdrawn.Remove(id);
            }
        }
        /// <summary>Explicit save boundary only: finish the transient image and compact unreferenced retired rows.</summary>
        public void CompleteForSave()
        {
            while (Prepare((long)EarliestMinute)) { }
            Coordinator.Compact(row => receipts.Any(p => FoodSaleReceipts.IsReceiptKey(p.Key)
                && !p.Value.StartsWith("paid|", StringComparison.Ordinal)
                && p.Value.Contains(Uri.EscapeDataString(row.Signature + ":generation:"))));
        }
        public void Dispose()
        {
            if (disposed) return;
            if (sameSave()) Coordinator.ReleaseAll((long)EarliestMinute, "Persistent City vendor market stopped");
            disposed = true;
        }

        public static void ValidatePopulation(FoodCommerceState state, VendorTable table, IDictionary<string, string> receipts)
        {
            table.CheckInvariant();
            var scopes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var s in state.Shops)
            {
                if (s.Channel != FoodScopeChannel.Shop && s.Channel != FoodScopeChannel.Vehicle) throw new InvalidOperationException("Unknown food scope channel");
                if (s.Channel != FoodScopeChannel.Vehicle) { if (VendorNames.IsVendorPlace(s.PlaceId)) throw new InvalidOperationException("Shop scope names a vendor place"); continue; }
                var row = table.FindPlace(s.PlaceId);
                if (row == null || row.State == VendorRowState.Tombstone || row.Stand == null || row.BusinessId != s.BusinessId || !scopes.Add(s.PlaceId))
                    throw new InvalidOperationException("Vehicle scope differs from its vendor table");
            }
            foreach (var row in table.Rows)
                if (row.State != VendorRowState.Tombstone && row.Stand != null && !scopes.Contains(row.PlaceId))
                    throw new InvalidOperationException("Vendor stand has no vehicle scope");
            void Check(string place, string business)
            {
                if (!VendorNames.IsVendorPlace(place) && !business.StartsWith(VendorNames.SignaturePrefix, StringComparison.Ordinal)) return;
                var row = table.FindPlace(place);
                if (row == null || row.State == VendorRowState.Tombstone || !BusinessMatches(row, business))
                    throw new InvalidOperationException("Vehicle meal or decision names a missing vendor row");
            }
            foreach (var meal in state.Residents)
            {
                Check(meal.PlaceId, meal.BusinessId);
                if (meal.Decision != null) foreach (var s in meal.Decision.Shops) Check(s.PlaceId, s.BusinessId);
            }
            foreach (var receipt in receipts.Where(p => FoodSaleReceipts.IsReceiptKey(p.Key)))
            {
                var fields = receipt.Value.Split('|');
                if (fields.Length < 3) continue;
                string business = Uri.UnescapeDataString(fields[2]);
                if (business.StartsWith(VendorNames.SignaturePrefix, StringComparison.Ordinal) && !table.Rows.Any(r => BusinessMatches(r, business)))
                    throw new InvalidOperationException("Vehicle receipt names a missing vendor row");
            }
        }
        private static bool BusinessMatches(VendorRow row, string business)
        {
            string prefix = row.Signature + ":generation:";
            if (!business.StartsWith(prefix, StringComparison.Ordinal)) return false;
            string text = business.Substring(prefix.Length);
            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int generation) && generation >= 1 && generation <= row.Generation
                && text == generation.ToString(CultureInfo.InvariantCulture);
        }
    }
}
