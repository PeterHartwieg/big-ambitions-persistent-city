using System;
using System.Collections.Generic;
using System.Globalization;

namespace PersistentCity.Core
{
    public interface IVendorStandSampler
    {
        bool TrySample(Position stand, double radiusMetres, out Position point);
    }

    public interface IVendorPlaces
    {
        void PublishVendorPlace(VendorRow row, VendorVehicle? menu);
        void RemoveVendorPlace(VendorRow row);
        void CollectVendorReferences(VendorReferences into);
    }

    public sealed class VendorReferences
    {
        public HashSet<string> ShortLived { get; } = new HashSet<string>(StringComparer.Ordinal);
        public HashSet<string> Retained { get; } = new HashSet<string>(StringComparer.Ordinal);
        public HashSet<string> Memory { get; } = new HashSet<string>(StringComparer.Ordinal);
        public void Clear() { ShortLived.Clear(); Retained.Clear(); Memory.Clear(); }
    }

    public static class VendorRules
    {
        public const double StandRadiusMetres = 2;
        public const double SameStandMetres = 0.1;

        public static bool OriginalThree(string item) => FoodProducts.KnownLegacy(item);

        public static string? MenuRefusal(VendorVehicle vehicle, Func<string, bool> executable, int maximumItems = VendorVehicle.MaximumItems)
        {
            if (vehicle.MenuOverflow) return "menu has more than 8 items";
            if (vehicle.ItemCount == 0) return "menu is empty";
            if (vehicle.ItemCount > maximumItems) return "menu has more than " + maximumItems + " items for resident choice";
            for (int i = 0; i < vehicle.ItemCount; i++)
            {
                string item = vehicle.Items[i];
                if (!FoodNameIdentity.IsCanonical(item)) return "menu item is not a canonical name: " + item;
                if (!executable(item)) return "menu item not executable: " + item;
            }
            return null;
        }

        public static string? OpeningRefusal(VendorVehicle vehicle)
        {
            if (!vehicle.Open) return "vehicle is closed";
            if (vehicle.EventOrCatering) return "city event or catering opening stays with Street Food";
            if (!vehicle.OpenedByCrew) return "player-opened window stays with Street Food";
            if (vehicle.ClosesAt < 0) return "opening has no planned close";
            if (vehicle.ClosesAt - vehicle.OpenedAt >= 1440) return "opening is longer than one day";
            return null;
        }

        public static VendorStand? PlanStand(VendorVehicle vehicle, IVendorStandSampler sampler, out string reason)
        {
            var window = vehicle.Window;
            if (!sampler.TrySample(window, StandRadiusMetres, out var point) || !point.IsFinite || Position.Distance(window, point) > StandRadiusMetres)
            { reason = "no walkable point within 2 m of the stand"; return null; }
            reason = "";
            return new VendorStand { OpeningId = vehicle.OpeningId, Position = point, OpenedDay = vehicle.OpenedDay, OpenedMinute = vehicle.OpenedMinute,
                ClosesDay = vehicle.ClosesDay, ClosesMinute = vehicle.ClosesMinute };
        }

        public static List<OpeningWindow> StandHours(VendorStand stand)
        {
            var hours = new List<OpeningWindow>();
            if (stand.Published && stand.ClosesAt > stand.OpenedAt && stand.ClosesAt - stand.OpenedAt < 1440)
                hours.Add(new OpeningWindow { Day = Weekday(stand.OpenedDay), FromMinute = stand.OpenedMinute, ToMinute = stand.ClosesMinute });
            return hours;
        }

        public static int Weekday(int day) => ((day - 1) % 7 + 7) % 7;

        public static string Describe(VendorApiStatus status)
        {
            switch (status)
            {
                case VendorApiStatus.Off: return "Street Food's vendor API is switched off.";
                case VendorApiStatus.NotYet: return "Street Food has not loaded its vehicles yet.";
                case VendorApiStatus.Refused: return "Street Food has more than 64 vehicles; they stay with Street Food.";
                case VendorApiStatus.Failed: return "Street Food's vendor API reported a fault.";
                default: return "";
            }
        }
    }

    public sealed class VendorImageReader
    {
        public const int CopyUnit = 16;
        public const int RetryMinutes = 15;
        private readonly IVendorApi api;
        private long retrySlot = long.MinValue;
        private bool refresh;
        private VendorImage published = new VendorImage(), building = new VendorImage();
        private int copied = -1, total;
        private long buildingRevision;

        public VendorImageReader(IVendorApi api) { this.api = api ?? throw new ArgumentNullException(nameof(api)); }
        public VendorImage Image => published;
        public bool Pending => copied >= 0;
        public void Refresh() => refresh = true;

        public bool Poll(long minute = -1)
        {
            long revision = api.Revision;
            if (copied >= 0)
            {
                if (revision == buildingRevision) return true;
                copied = -1;
            }
            else if (revision == published.Revision && !refresh && (published.Ready || minute < 0 || minute / RetryMinutes == retrySlot)) return false;
            if (minute >= 0) retrySlot = minute / RetryMinutes;
            refresh = false;
            buildingRevision = revision;
            VendorApiStatus status; int count;
            try { status = api.BeginRead(out count); }
            catch (InvalidOperationException error) { Publish(VendorApiStatus.Failed, error.Message); return true; }
            if (status != VendorApiStatus.Ready) { Publish(status, VendorRules.Describe(status)); return true; }
            if (count < 0 || count > VendorImage.MaximumVehicles)
            { Publish(VendorApiStatus.Failed, "Street Food VendorApi: Read returned " + count.ToString(CultureInfo.InvariantCulture) + " vehicles."); return true; }
            copied = 0; total = count;
            if (count == 0) Continue();
            return true;
        }

        public bool Continue()
        {
            if (copied < 0) return false;
            int end = Math.Min(total, copied + CopyUnit);
            try { for (int i = copied; i < end; i++) { var slot = building.Slot(i); slot.ShapeFault = ""; api.CopyVehicle(i, slot); } }
            catch (InvalidOperationException error) { copied = -1; Publish(VendorApiStatus.Failed, error.Message); return true; }
            copied = end;
            if (copied < total) return false;
            copied = -1;
            building.Count = total;
            string? fault = building.Validate();
            if (fault != null) { Publish(VendorApiStatus.Failed, "Street Food VendorApi: " + fault + "."); return true; }
            building.Status = VendorApiStatus.Ready; building.Reason = ""; building.Revision = buildingRevision;
            Swap();
            return true;
        }

        private void Publish(VendorApiStatus status, string reason) { building.Refuse(status, reason, buildingRevision); Swap(); }
        private void Swap() { var old = published; published = building; building = old; }
    }

    public sealed class VendorLease
    {
        public const int RenewMinutes = 15, LeaseMinutes = 60;
        private long renewedSlot = -1;
        public long UntilMinute { get; private set; } = -1;
        public bool Valid(long minute) => minute < UntilMinute;
        public bool Due(long minute) => minute / RenewMinutes != renewedSlot;

        internal bool Renew(IVendorApi api, long minute, out string reason)
        {
            renewedSlot = minute / RenewMinutes;
            var status = api.Renew((int)(minute / 1440), (int)(minute % 1440), out int day, out int at);
            if (status != VendorApiStatus.Ready) { reason = "lease not renewed: " + status; return false; }
            long until = day * 1440L + at;
            if (at < 0 || at >= 1440 || until <= minute || until > minute + LeaseMinutes)
            { UntilMinute = -1; reason = "Street Food VendorApi: Renew returned a lease outside the next 60 minutes."; return false; }
            UntilMinute = until; reason = "";
            return true;
        }

        internal void End() { UntilMinute = -1; renewedSlot = -1; }
    }

    public sealed class VendorCoordinator
    {
        public const int WaitingRecheckMinutes = 5;
        private readonly VendorTable table;
        private readonly IVendorApi? api;
        private readonly VendorImageReader? reader;
        private readonly IVendorPlaces places;
        private readonly IVendorStandSampler sampler;
        private readonly Func<string, bool> executable;
        private readonly int maximumItems;
        private readonly VendorLease lease = new VendorLease();
        private readonly VendorReferences references = new VendorReferences();
        private readonly List<KeyValuePair<string, string>> refusals = new List<KeyValuePair<string, string>>();
        private bool referencesCollected, applyPending, stopped, leaseLapsed, kindChangePending, revivalPending;
        private long waitingSlot = -1;

        public VendorCoordinator(VendorTable table, IVendorApi? api, string unavailableReason, IVendorPlaces places, IVendorStandSampler sampler,
            Func<string, bool>? executable = null, int maximumItems = VendorVehicle.MaximumItems)
        {
            this.table = table ?? throw new ArgumentNullException(nameof(table));
            this.places = places ?? throw new ArgumentNullException(nameof(places));
            this.sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
            this.executable = executable ?? VendorRules.OriginalThree;
            this.maximumItems = maximumItems;
            this.api = api;
            if (api != null) reader = new VendorImageReader(api);
            Reason = api == null ? (string.IsNullOrEmpty(unavailableReason) ? "Street Food vendor API missing." : unavailableReason) : "";
        }

        public VendorTable Table => table;
        public VendorLease Lease => lease;
        public VendorImage? Image => reader?.Image;
        public string Reason { get; private set; }
        public IReadOnlyList<KeyValuePair<string, string>> Refusals => refusals;
        public string LeaseFault { get; private set; } = "";

        public bool Poll(long minute)
        {
            if (api == null || stopped) return false;
            if (NeedsLease() && lease.Due(minute)) RenewLease(minute);
            if (reader!.Poll(minute)) applyPending = applyPending || !reader.Pending;
            if (Waiting() && minute / WaitingRecheckMinutes != waitingSlot) { waitingSlot = minute / WaitingRecheckMinutes; applyPending = true; }
            return reader.Pending || applyPending;
        }

        public bool Continue(long minute)
        {
            if (api == null || stopped) return false;
            if (reader!.Pending) { if (reader.Continue()) applyPending = true; return true; }
            if (!applyPending || Deferred) return false;
            applyPending = false;
            Apply(minute);
            return true;
        }

        public bool CanServe(string businessId, long minute)
        {
            if (api == null || stopped || !lease.Valid(minute)) return false;
            var image = reader!.Image;
            if (!image.Ready) return false;
            for (int r = 0; r < table.Rows.Count; r++)
            {
                var row = table.Rows[r];
                if (row.State != VendorRowState.Live || row.BusinessId != businessId) continue;
                var a = row.Authority; var stand = row.Stand; var v = image.Find(row.VehicleId);
                return a.Desired && !a.Suspended && a.OpeningId >= 0 && v != null && v.OwnedByPersistentCity && v.OpeningId == a.OpeningId
                    && v.Kind == row.Kind && stand != null && stand.OpeningId == a.OpeningId && stand.Published && row.DeferredStand == null
                    && v.MealsPerHour > 0 && VendorRules.MenuRefusal(v, executable, maximumItems) == null && VendorRules.OpeningRefusal(v) == null;
            }
            return false;
        }

        public void ReleaseAll(long minute, string reason)
        {
            if (api == null) return;
            try { api.ReleaseAll(); } catch (InvalidOperationException) { }
            for (int r = 0; r < table.Rows.Count; r++)
            {
                var row = table.Rows[r];
                if (row.State == VendorRowState.Tombstone) continue;
                var a = row.Authority;
                a.Desired = false; a.RequestedHour = -1; a.OpeningId = -1; a.Suspended = false;
                if (a.Managed) a.UntilHour = minute / 60;
                row.DeferredStand = null; Close(row); row.Reason = reason;
            }
            lease.End(); stopped = true; Reason = reason;
        }

        public void Resume() { stopped = false; applyPending = true; Reason = ""; }

        public void Revalidate() { if (api != null && !stopped) applyPending = true; }

        public bool Deferred { get; set; }

        public int Compact(Func<VendorRow, bool>? pendingReceipt = null)
        {
            int changed = 0;
            referencesCollected = false;
            for (int i = table.Rows.Count - 1; i >= 0; i--)
            {
                var row = table.Rows[i];
                if (row.State != VendorRowState.Retired) continue;
                References();
                if (references.ShortLived.Contains(row.PlaceId) || references.Retained.Contains(row.PlaceId) || pendingReceipt?.Invoke(row) == true) continue;
                bool remembered = references.Memory.Contains(row.PlaceId);
                if (remembered && table.Tombstones >= VendorTable.MaximumTombstones) continue;
                if (row.PlacePublished) { places.RemoveVendorPlace(row); row.PlacePublished = false; }
                if (remembered)
                {
                    table.Move(row, VendorRowState.Tombstone);
                    row.Kind = ""; row.Stand = null; row.DeferredStand = null; row.Authority = new VendorAuthority(); row.Reason = "";
                }
                else table.Remove(row);
                changed++;
            }
            table.CheckInvariant();
            return changed;
        }

        private void Apply(long minute)
        {
            referencesCollected = false;
            refusals.Clear();
            kindChangePending = false; revivalPending = false;
            bool lapsed = leaseLapsed; leaseLapsed = false;
            var image = reader!.Image;
            long hour = minute / 60;
            if (!image.Ready)
            {
                Reason = image.Reason;
                for (int r = 0; r < table.Rows.Count; r++)
                {
                    var row = table.Rows[r];
                    if (row.State == VendorRowState.Live) { row.Authority.Suspended = row.Authority.OpeningId >= 0; Close(row); row.Reason = image.Reason; }
                }
                return;
            }
            Reason = "";
            for (int r = 0; r < table.Rows.Count; r++)
                if (table.Rows[r].State == VendorRowState.Live && image.Find(table.Rows[r].VehicleId) == null) Retire(table.Rows[r], hour);
            for (int i = 0; i < image.Count; i++)
            {
                var v = image[i];
                var row = table.Find(v.VehicleId);
                string? refusal = VendorRules.MenuRefusal(v, executable, maximumItems);
                if (row == null || row.State != VendorRowState.Live)
                {
                    string? admission = refusal ?? table.AdmissionRefusal(row);
                    if (admission == null && row != null && row.State == VendorRowState.Retired && Committed(row))
                    { admission = "returning vehicle waits for committed residents"; revivalPending = true; }
                    if (admission != null) { refusals.Add(new KeyValuePair<string, string>(v.VehicleId, admission)); continue; }
                    if (row == null) row = table.Admit(v.VehicleId, v.Kind);
                    else
                    {
                        table.Move(row, VendorRowState.Live);
                        row.Generation = checked(row.Generation + 1); row.Kind = v.Kind; row.Authority = new VendorAuthority();
                        if (row.PlacePublished) places.PublishVendorPlace(row, v);
                    }
                }
                if (v.Kind != row.Kind)
                {
                    var a = row.Authority;
                    if (!a.Requested && !a.Managed && a.OpeningId < 0 && !Committed(row))
                    {
                        row.Generation = checked(row.Generation + 1); row.Kind = v.Kind; row.Authority = new VendorAuthority(); row.DeferredStand = null;
                        if (row.PlacePublished) places.PublishVendorPlace(row, v);
                    }
                    else { refusal = "vehicle kind changed"; kindChangePending = true; }
                }
                Step(row, v, refusal, lapsed, minute, hour);
            }
            table.CheckInvariant();
        }

        private void Step(VendorRow row, VendorVehicle v, string? refusal, bool lapsed, long minute, long hour)
        {
            var a = row.Authority;
            bool owned = v.OwnedByPersistentCity;
            row.Reason = refusal ?? "";
            if (refusal != null) a.Desired = false;
            else if (!a.Desired) a.Desired = true;
            if (lapsed) a.RequestedHour = -1;

            if (a.OpeningId >= 0 && (!owned || v.OpeningId != a.OpeningId))
            {
                if (v.Open && v.OpeningId == a.OpeningId) a.RequestedHour = -1;
                a.OpeningId = -1; row.DeferredStand = null; Close(row);
            }
            if (a.HandbackPending)
            {
                if (!lease.Valid(minute)) a.HandbackPending = false;
                else
                {
                    if (!Release(row, hour, row.Reason)) { row.Reason = "handback to Street Food not confirmed; retrying"; return; }
                    if (refusal == null) a.Desired = true;
                    reader!.Refresh();
                    return;
                }
            }
            if (a.Desired && !a.Requested) Request(row, minute, hour);

            if (owned && a.OpeningId != v.OpeningId && a.Desired)
            {
                VendorStand? stand = null;
                string? why = !a.Requested ? "Street Food granted an opening without a confirmed ownership request"
                    : v.OpenedHour <= a.RequestedHour ? "Street Food granted an opening that started in the request hour" : VendorRules.OpeningRefusal(v);
                if (why == null) stand = VendorRules.PlanStand(v, sampler, out why);
                if (stand == null)
                {
                    if (Release(row, hour, why!)) { a.Desired = true; Request(row, minute, hour); }
                    row.Reason = why!;
                }
                else
                {
                    a.OpeningId = v.OpeningId;
                    if (!a.Managed) { a.FromHour = v.OpenedHour; a.UntilHour = -1; }
                    Place(row, stand, v);
                }
            }
            if (!a.Desired && (a.Requested || a.Managed || a.OpeningId >= 0 || owned) && (a.OpeningId < 0 || !Committed(row)))
                Release(row, hour, refusal ?? "not desired");

            if (row.DeferredStand != null && a.OpeningId == row.DeferredStand.OpeningId && !Committed(row))
            {
                var next = row.DeferredStand; row.DeferredStand = null;
                row.Stand = next; next.Published = true; places.PublishVendorPlace(row, v);
            }
            else if (a.OpeningId >= 0 && row.Stand != null && row.Stand.OpeningId == a.OpeningId && !row.Stand.Published && row.DeferredStand == null)
            { row.Stand.Published = true; places.PublishVendorPlace(row, v); }
            a.Suspended = a.OpeningId >= 0 && !lease.Valid(minute);
        }

        private void Place(VendorRow row, VendorStand stand, VendorVehicle v)
        {
            var old = row.Stand;
            if (old == null || !row.PlacePublished || old.OpeningId == stand.OpeningId
                || Position.Distance(old.Position, stand.Position) <= VendorRules.SameStandMetres || !Committed(row))
            {
                row.Stand = stand; row.DeferredStand = null; stand.Published = true;
                places.PublishVendorPlace(row, v); row.PlacePublished = true;
                return;
            }
            row.DeferredStand = stand; old.Published = false;
            places.PublishVendorPlace(row, null);
            row.Reason = "stand move waits for committed residents";
        }

        private void Request(VendorRow row, long minute, long hour)
        {
            if (!lease.Valid(minute)) RenewLease(minute);
            string reason;
            VendorApiStatus status;
            try { status = api!.RequestOwner(row.VehicleId, true, out reason); }
            catch (InvalidOperationException error) { row.Reason = error.Message; return; }
            if (status == VendorApiStatus.Ready) row.Authority.RequestedHour = hour;
            else row.Reason = "ownership request refused: " + (string.IsNullOrEmpty(reason) ? status.ToString() : reason);
        }

        private bool Release(VendorRow row, long hour, string reason)
        {
            var a = row.Authority;
            bool released;
            try { released = api!.RequestOwner(row.VehicleId, false, out _) == VendorApiStatus.Ready; }
            catch (InvalidOperationException) { released = false; }
            a.HandbackPending = !released;
            if (a.Managed) a.UntilHour = hour;
            a.RequestedHour = -1; a.OpeningId = -1; a.Suspended = false; a.Desired = false;
            row.DeferredStand = null; Close(row); row.Reason = reason;
            return released;
        }

        private void Retire(VendorRow row, long hour)
        {
            var a = row.Authority;
            a.Desired = false; a.RequestedHour = -1; a.OpeningId = -1; a.Suspended = false; a.HandbackPending = false;
            if (a.Managed) a.UntilHour = hour;
            row.DeferredStand = null; Close(row); row.Reason = "vehicle missing from Street Food";
            table.Move(row, VendorRowState.Retired);
        }

        private void Close(VendorRow row)
        {
            if (row.Stand == null || !row.Stand.Published) return;
            row.Stand.Published = false;
            if (row.PlacePublished) places.PublishVendorPlace(row, null);
        }

        private void RenewLease(long minute)
        {
            bool wasValid = lease.Valid(minute), held = lease.UntilMinute >= 0;
            string reason;
            bool renewed;
            try { renewed = lease.Renew(api!, minute, out reason); }
            catch (InvalidOperationException error) { renewed = false; reason = error.Message; }
            LeaseFault = reason;
            if (!renewed) return;
            if (held && !wasValid) { leaseLapsed = true; applyPending = true; }
        }

        private bool NeedsLease()
        {
            for (int r = 0; r < table.Rows.Count; r++)
            {
                var row = table.Rows[r];
                if (row.State != VendorRowState.Live) continue;
                var a = row.Authority;
                if (a.Desired || a.Requested || a.Managed || a.OpeningId >= 0) return true;
            }
            return false;
        }

        private bool Waiting()
        {
            if (kindChangePending || revivalPending) return true;
            for (int r = 0; r < table.Rows.Count; r++)
            {
                var row = table.Rows[r];
                if (row.State != VendorRowState.Live) continue;
                var a = row.Authority;
                if (row.DeferredStand != null || a.HandbackPending || !a.Desired && (a.Requested || a.Managed || a.OpeningId >= 0)) return true;
            }
            return false;
        }

        private bool Committed(VendorRow row) => row.PlacePublished && References().ShortLived.Contains(row.PlaceId);

        private VendorReferences References()
        {
            if (!referencesCollected) { places.CollectVendorReferences(references); referencesCollected = true; }
            return references;
        }
    }

    public sealed partial class CitySimulator : IVendorPlaces
    {
        public void PublishVendorPlace(VendorRow row, VendorVehicle? menu)
        {
            var stand = row.Stand ?? throw new InvalidOperationException("A vendor place needs a stand.");
            var food = State.Food ?? throw new InvalidOperationException("Vendor places need a food scope.");
            if (!places.TryGetValue(row.PlaceId, out var place))
            {
                place = new Place { Id = row.PlaceId, Name = "Street Food " + row.Kind, Kind = PlaceKind.Shop, Capacity = 1, EvidenceSource = "streetfood" };
                places.Add(place.Id, place);
            }
            place.Position = stand.Position;
            place.OpenMinute = 0; place.CloseMinute = 0;
            place.WeeklyHours = VendorRules.StandHours(stand);
            var scope = food.Shops.Find(s => s.PlaceId == row.PlaceId);
            if (scope == null)
            {
                if (menu == null || menu.ItemCount == 0) throw new InvalidOperationException("A new vendor scope needs the vehicle's menu.");
                scope = new FoodScope { AlternativeKind = FoodAlternativeKind.Player, PlaceId = row.PlaceId };
                scope.UseChannel(FoodScopeChannel.Vehicle);
                food.Shops.Add(scope);
            }
            else if (scope.Channel != FoodScopeChannel.Vehicle) throw new InvalidOperationException("A shop scope already uses vendor place " + row.PlaceId + ".");
            scope.BusinessId = row.BusinessId;
            if (menu != null && menu.ItemCount > 0)
            {
                scope.ProductId = menu.Items[0];
                scope.AdditionalProductIds.Clear();
                for (int i = 1; i < menu.ItemCount; i++) scope.AdditionalProductIds.Add(menu.Items[i]);
            }
            InvalidateFoodAwareness();
        }

        public void RemoveVendorPlace(VendorRow row)
        {
            places.Remove(row.PlaceId);
            State.Food?.Shops.RemoveAll(s => s.PlaceId == row.PlaceId && s.Channel == FoodScopeChannel.Vehicle);
            InvalidateFoodAwareness();
        }

        public void CollectVendorReferences(VendorReferences into)
        {
            into.Clear();
            var state = State.Food;
            if (state == null) return;
            foreach (var meal in state.Residents)
            {
                bool active = meal.Status == MealStatus.Pending || meal.Status == MealStatus.Travelling || meal.Status == MealStatus.Serving;
                if (VendorNames.IsVendorPlace(meal.PlaceId))
                {
                    if (active) into.ShortLived.Add(meal.PlaceId);
                    else if (meal.Status == MealStatus.Uncertain) into.Retained.Add(meal.PlaceId);
                }
                var excursion = meal.Excursion;
                if (excursion != null && VendorNames.IsVendorPlace(excursion.DestinationId)) into.ShortLived.Add(excursion.DestinationId);
                if (active && meal.Decision != null)
                    foreach (var shop in meal.Decision.Shops) if (VendorNames.IsVendorPlace(shop.PlaceId)) into.ShortLived.Add(shop.PlaceId);
            }
            foreach (var stop in state.RoutineStops) if (VendorNames.IsVendorPlace(stop.PlaceId)) into.ShortLived.Add(stop.PlaceId);
            foreach (var person in State.Citizens) Person(person);
            foreach (var routine in State.VisitorRoutines) Person(routine);
            void Person(CitizenState person)
            {
                if (VendorNames.IsVendorPlace(person.PlaceId)) into.ShortLived.Add(person.PlaceId);
                if (VendorNames.IsVendorPlace(person.DestinationId)) into.ShortLived.Add(person.DestinationId);
                foreach (var visit in person.RecentVisits) if (VendorNames.IsVendorPlace(visit.PlaceId)) into.Retained.Add(visit.PlaceId);
            }
            foreach (var day in state.History) if (VendorNames.IsVendorPlace(day.PlaceId)) into.Retained.Add(day.PlaceId);
            foreach (var reference in state.BusinessReferences) if (VendorNames.IsVendorPlace(reference.PlaceId)) into.Memory.Add(reference.PlaceId);
        }

        internal bool FoodAwarenessCurrent => food != null && food.AwarenessCurrent;

        private void CopyVendorPlaces(CitySimulator from)
        {
            foreach (var pair in from.places)
                if (VendorNames.IsVendorPlace(pair.Key)) places[pair.Key] = pair.Value;
        }

        private void RestoreVendorPlaces()
        {
            var table = State.Vendors;
            if (table == null) return;
            foreach (var row in table.Rows)
                if (row.State != VendorRowState.Tombstone && row.Stand != null) { PublishVendorPlace(row, null); row.PlacePublished = true; }
        }
    }
}
