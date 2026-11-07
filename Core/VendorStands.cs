using System;
using System.Collections.Generic;
using System.Globalization;

namespace PersistentCity.Core
{
    /// <summary>Walkable height at a stand. The Runtime implementation samples the NavMesh (E6d); Core only reads the answer.</summary>
    public interface IVendorStandSampler
    {
        bool TrySample(Position stand, double radiusMetres, out Position point);
    }

    /// <summary>What the vendor rules need from the simulator: the walking-only vendor place with its Vehicle scope,
    /// and the places that state still names. <see cref="CitySimulator"/> implements it.</summary>
    public interface IVendorPlaces
    {
        /// <summary>Creates or updates the row's vendor place at its stand, open only while the stand is published,
        /// and its Vehicle scope. <paramref name="menu"/> replaces the scope's products when given.</summary>
        void PublishVendorPlace(VendorRow row, VendorVehicle? menu);
        void RemoveVendorPlace(VendorRow row);
        void CollectVendorReferences(VendorReferences into);
    }

    /// <summary>Vendor places named by city state, split by how long the reference lives.</summary>
    public sealed class VendorReferences
    {
        /// <summary>Committed meals, excursions, held decisions and routine stops: these resolve within one meal window.</summary>
        public HashSet<string> ShortLived { get; } = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>Uncertain meals and retained market history rows: they block compaction, not stand moves.</summary>
        public HashSet<string> Retained { get; } = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>Memory business references. Never reclaimed, so they turn a compacted row into a tombstone.</summary>
        public HashSet<string> Memory { get; } = new HashSet<string>(StringComparer.Ordinal);
        public void Clear() { ShortLived.Clear(); Retained.Clear(); Memory.Clear(); }
    }

    /// <summary>The whole-vehicle and opening rules of E6 plan 3.3-3.5 and the section 9 defaults.</summary>
    public static class VendorRules
    {
        public const double StandRadiusMetres = 2;
        /// <summary>A stand within this distance of the current one is the same spot, not a move.</summary>
        public const double SameStandMetres = 0.1;

        /// <summary>Until E5b's held-policy execution lands, only the original three products are executable.</summary>
        public static bool OriginalThree(string item) => FoodProducts.KnownLegacy(item);

        /// <summary>Null when PC may own this vehicle's openings, otherwise why the whole vehicle stays with Street Food.
        /// Units of zero are sold out, not a refusal.</summary>
        public static string? MenuRefusal(VendorVehicle vehicle, Func<string, bool> executable)
        {
            if (vehicle.MenuOverflow) return "menu has more than 8 items";
            if (vehicle.ItemCount == 0) return "menu is empty";
            for (int i = 0; i < vehicle.ItemCount; i++)
            {
                string item = vehicle.Items[i];
                if (!FoodNameIdentity.IsCanonical(item)) return "menu item is not a canonical name: " + item;
                if (!executable(item)) return "menu item not executable: " + item;
            }
            return null;
        }

        /// <summary>Null when PC may serve this opening. Player-opened windows and city event or catering openings stay
        /// with Street Food (section 9, Q2 and Q4); a window must have a planned close less than a day after it opens.</summary>
        public static string? OpeningRefusal(VendorVehicle vehicle)
        {
            if (!vehicle.Open) return "vehicle is closed";
            if (vehicle.EventOrCatering) return "city event or catering opening stays with Street Food";
            if (!vehicle.OpenedByCrew) return "player-opened window stays with Street Food";
            if (vehicle.ClosesAt < 0) return "opening has no planned close";
            if (vehicle.ClosesAt - vehicle.OpenedAt >= 1440) return "opening is longer than one day";
            return null;
        }

        /// <summary>The stand of the vehicle's current opening at the sampled walkable point, or null with a reason.</summary>
        public static VendorStand? PlanStand(VendorVehicle vehicle, IVendorStandSampler sampler, out string reason)
        {
            var window = vehicle.Window;
            if (!sampler.TrySample(window, StandRadiusMetres, out var point) || !point.IsFinite || Position.Distance(window, point) > StandRadiusMetres)
            { reason = "no walkable point within 2 m of the stand"; return null; }
            reason = "";
            return new VendorStand { OpeningId = vehicle.OpeningId, Position = point, OpenedDay = vehicle.OpenedDay, OpenedMinute = vehicle.OpenedMinute,
                ClosesDay = vehicle.ClosesDay, ClosesMinute = vehicle.ClosesMinute };
        }

        /// <summary>DailyRoutine's weekday (Monday = 0) of a native day.</summary>
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

    /// <summary>Copies Street Food's snapshot into a validated Core image, at most 16 vehicles per unit, only when the
    /// revision changed. Two images are allocated once and swapped; an unchanged revision costs one property read.</summary>
    public sealed class VendorImageReader
    {
        public const int CopyUnit = 16;
        /// <summary>A non-Ready image is read again once per this many native minutes even at the same revision.</summary>
        public const int RetryMinutes = 15;
        private readonly IVendorApi api;
        private long retrySlot = long.MinValue;
        private VendorImage published = new VendorImage(), building = new VendorImage();
        private int copied = -1, total;
        private long buildingRevision;

        public VendorImageReader(IVendorApi api) { this.api = api ?? throw new ArgumentNullException(nameof(api)); }
        public VendorImage Image => published;
        public bool Pending => copied >= 0;

        /// <summary>One revision read. True when a copy is pending or a new image was published. With a native minute,
        /// an image that is not Ready is also read again once per 15 minutes: Street Food may recover without a new revision.</summary>
        public bool Poll(long minute = -1)
        {
            long revision = api.Revision;
            if (copied >= 0)
            {
                if (revision == buildingRevision) return true;
                copied = -1; // Street Food changed again mid-copy; start over from its new view.
            }
            else if (revision == published.Revision && (published.Ready || minute < 0 || minute / RetryMinutes == retrySlot)) return false;
            if (minute >= 0) retrySlot = minute / RetryMinutes;
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

        /// <summary>Copies one unit. True when this call published a new image.</summary>
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

    /// <summary>PC's lease with Street Food. Renewed in every 15-minute slot it is needed, so the 60-minute lease never
    /// runs out while PC is healthy, at any update cadence of 45 minutes or less.</summary>
    public sealed class VendorLease
    {
        public const int RenewMinutes = 15, LeaseMinutes = 60;
        private long renewedSlot = -1;
        /// <summary>PC absolute minute the lease runs to; -1 when none was granted.</summary>
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

    /// <summary>Drives the vendor rows from Street Food's snapshot: request, acquire, serve, release, lease, stands,
    /// retirement. Core only; Runtime calls <see cref="Poll"/> once per logical minute and <see cref="Continue"/> in
    /// bounded continuation units. Without the API every vehicle scope stays unserviceable and shops are untouched.</summary>
    public sealed class VendorCoordinator
    {
        /// <summary>How often rows that wait on residents (a deferred stand, a release) are checked again.</summary>
        public const int WaitingRecheckMinutes = 5;
        private readonly VendorTable table;
        private readonly IVendorApi? api;
        private readonly VendorImageReader? reader;
        private readonly IVendorPlaces places;
        private readonly IVendorStandSampler sampler;
        private readonly Func<string, bool> executable;
        private readonly VendorLease lease = new VendorLease();
        private readonly VendorReferences references = new VendorReferences();
        private readonly List<KeyValuePair<string, string>> refusals = new List<KeyValuePair<string, string>>();
        private bool referencesCollected, applyPending, stopped, leaseLapsed, kindChangePending, revivalPending;
        private long waitingSlot = -1;

        public VendorCoordinator(VendorTable table, IVendorApi? api, string unavailableReason, IVendorPlaces places, IVendorStandSampler sampler,
            Func<string, bool>? executable = null)
        {
            this.table = table ?? throw new ArgumentNullException(nameof(table));
            this.places = places ?? throw new ArgumentNullException(nameof(places));
            this.sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
            this.executable = executable ?? VendorRules.OriginalThree;
            this.api = api;
            if (api != null) reader = new VendorImageReader(api);
            Reason = api == null ? (string.IsNullOrEmpty(unavailableReason) ? "Street Food vendor API missing." : unavailableReason) : "";
        }

        public VendorTable Table => table;
        public VendorLease Lease => lease;
        public VendorImage? Image => reader?.Image;
        /// <summary>Why no vehicle is served at all; empty while the bridge is bound and its image is Ready.</summary>
        public string Reason { get; private set; }
        /// <summary>Vehicles in the last applied image that have no live row, with the reason (whole-vehicle refusal, table full).</summary>
        public IReadOnlyList<KeyValuePair<string, string>> Refusals => refusals;
        /// <summary>Why the last lease renewal failed; empty after a successful one.</summary>
        public string LeaseFault { get; private set; } = "";

        /// <summary>The logical-minute step: lease renewal and one revision read. True when continuation work is pending.</summary>
        public bool Poll(long minute)
        {
            if (api == null || stopped) return false;
            if (NeedsLease() && lease.Due(minute)) RenewLease(minute);
            if (reader!.Poll(minute)) applyPending = applyPending || !reader.Pending;
            if (Waiting() && minute / WaitingRecheckMinutes != waitingSlot) { waitingSlot = minute / WaitingRecheckMinutes; applyPending = true; }
            return reader.Pending || applyPending;
        }

        /// <summary>One bounded unit: copy at most 16 vehicles, or apply a finished image. False when nothing was pending.</summary>
        public bool Continue(long minute)
        {
            if (api == null || stopped) return false;
            if (reader!.Pending) { if (reader.Continue()) applyPending = true; return true; }
            if (!applyPending) return false;
            applyPending = false;
            Apply(minute);
            return true;
        }

        /// <summary>True only while the snapshot says open, the opening is the stand's, the owner is PC, the lease is valid
        /// and the whole-vehicle and opening rules hold.</summary>
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
                    && v.MealsPerHour > 0 && VendorRules.MenuRefusal(v, executable) == null && VendorRules.OpeningRefusal(v) == null;
            }
            return false;
        }

        /// <summary>Hands every vehicle back at once (stopped epoch, refused city) and stops until <see cref="Resume"/>.
        /// If Street Food cannot be reached the lease runs out by itself.</summary>
        public void ReleaseAll(long minute, string reason)
        {
            if (api == null) return;
            try { api.ReleaseAll(); } catch (InvalidOperationException) { } // the lease then runs out by itself
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

        /// <summary>At a save-consistent event boundary: removes retired rows nothing names, or keeps them as tombstones
        /// when memory names their place. <paramref name="pendingReceipt"/> reports a pending or uncertain receipt.</summary>
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
                if (remembered && table.Tombstones >= VendorTable.MaximumTombstones) continue; // stays retired, within 128
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
                // Street Food absent, off or broken: no row is retired, nothing is served, places show no hours.
                Reason = image.Reason;
                for (int r = 0; r < table.Rows.Count; r++)
                {
                    var row = table.Rows[r];
                    if (row.State == VendorRowState.Live) { row.Authority.Suspended = row.Authority.OpeningId >= 0; Close(row); row.Reason = image.Reason; }
                }
                return;
            }
            Reason = "";
            // Retire first: a live row turning retired keeps live + retired, and frees a live slot for a newcomer.
            for (int r = 0; r < table.Rows.Count; r++)
                if (table.Rows[r].State == VendorRowState.Live && image.Find(table.Rows[r].VehicleId) == null) Retire(table.Rows[r], hour);
            for (int i = 0; i < image.Count; i++)
            {
                var v = image[i];
                var row = table.Find(v.VehicleId);
                string? refusal = VendorRules.MenuRefusal(v, executable);
                if (row == null || row.State != VendorRowState.Live)
                {
                    string? admission = refusal ?? table.AdmissionRefusal(row);
                    // A retired vehicle that returns becomes the next generation only after residents committed to the
                    // previous generation at its place have resolved (the same drain rule as a kind change).
                    if (admission == null && row != null && row.State == VendorRowState.Retired && Committed(row))
                    { admission = "returning vehicle waits for committed residents"; revivalPending = true; }
                    if (admission != null) { refusals.Add(new KeyValuePair<string, string>(v.VehicleId, admission)); continue; }
                    if (row == null) row = table.Admit(v.VehicleId, v.Kind);
                    else
                    {
                        // A returning vehicle is a new business generation at the same place and signature.
                        table.Move(row, VendorRowState.Live);
                        row.Generation = checked(row.Generation + 1); row.Kind = v.Kind; row.Authority = new VendorAuthority();
                        if (row.PlacePublished) places.PublishVendorPlace(row, v);
                    }
                }
                if (v.Kind != row.Kind)
                {
                    // Another kind under the same id is another business: drain, release, then the next generation.
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
            if (refusal != null) a.Desired = false; // stop new captures at once
            else if (!a.Desired) a.Desired = true;
            if (lapsed) a.RequestedHour = -1;

            // The opening PC served has ended, or Street Food took it back (lease ran out).
            if (a.OpeningId >= 0 && (!owned || v.OpeningId != a.OpeningId))
            {
                if (v.Open && v.OpeningId == a.OpeningId) a.RequestedHour = -1; // handed back: request the next opening again
                a.OpeningId = -1; row.DeferredStand = null; Close(row);
            }
            // A handback Street Food has not confirmed blocks every new request and acquisition until it succeeds.
            if (a.HandbackPending && !Release(row, hour, row.Reason)) { row.Reason = "handback to Street Food not confirmed; retrying"; return; }
            if (refusal == null && !a.Desired) a.Desired = true; // after a confirmed retried handback an eligible vehicle asks again
            if (a.Desired && !a.Requested) Request(row, minute, hour);

            if (owned && a.OpeningId != v.OpeningId && a.Desired)
            {
                VendorStand? stand = null;
                string? why = v.OpenedHour <= a.RequestedHour ? "Street Food granted an opening that started in the request hour" : VendorRules.OpeningRefusal(v);
                if (why == null) stand = VendorRules.PlanStand(v, sampler, out why);
                if (stand == null)
                {
                    // No PC work exists for this opening yet: hand it back at once and ask for the next one,
                    // but only once Street Food has confirmed the handback.
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
            // Release only after PC's own work at the served opening has drained.
            if (!a.Desired && (a.Requested || a.Managed || a.OpeningId >= 0 || owned) && (a.OpeningId < 0 || !Committed(row)))
                Release(row, hour, refusal ?? "not desired");

            if (row.DeferredStand != null && a.OpeningId == row.DeferredStand.OpeningId && !Committed(row))
            {
                var next = row.DeferredStand; row.DeferredStand = null;
                row.Stand = next; next.Published = true; places.PublishVendorPlace(row, v);
            }
            else if (a.OpeningId >= 0 && row.Stand != null && row.Stand.OpeningId == a.OpeningId && !row.Stand.Published && row.DeferredStand == null)
            { row.Stand.Published = true; places.PublishVendorPlace(row, v); } // reopened after Street Food came back
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
            // Residents still walk to, wait at or hold a meal for the old position: keep it, with no open hours.
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

        /// <summary>Asks Street Food to take the vehicle back. PC stops serving at once either way; a handback Street Food
        /// did not confirm stays pending and is retried, because another vehicle may keep the shared lease alive.</summary>
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
            // Street Food handed every PC opening back when the lease ran out; ask again for later openings.
            if (held && !wasValid) { leaseLapsed = true; applyPending = true; }
        }

        private bool NeedsLease()
        {
            for (int r = 0; r < table.Rows.Count; r++)
            {
                var row = table.Rows[r];
                if (row.State != VendorRowState.Live) continue;
                var a = row.Authority;
                if (a.Desired || a.Requested || a.Managed || a.OpeningId >= 0 || a.HandbackPending) return true;
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
            place.OpenMinute = 0; place.CloseMinute = 0; // no hours outside the stand's one window
            var hours = new List<OpeningWindow>();
            if (stand.Published && stand.ClosesAt > stand.OpenedAt && stand.ClosesAt - stand.OpenedAt < 1440)
                hours.Add(new OpeningWindow { Day = VendorRules.Weekday(stand.OpenedDay), FromMinute = stand.OpenedMinute, ToMinute = stand.ClosesMinute });
            place.WeeklyHours = hours;
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

        /// <summary>One pass over meals, routine stops, history and memory references; only vendor places are kept.</summary>
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
            // A physical resident walking to, eating at or about to leave the stand: its return route starts from the
            // place's position, so the place must not move until the person has left it.
            foreach (var person in State.Citizens)
            {
                if (VendorNames.IsVendorPlace(person.PlaceId)) into.ShortLived.Add(person.PlaceId);
                if (VendorNames.IsVendorPlace(person.DestinationId)) into.ShortLived.Add(person.DestinationId);
            }
            foreach (var day in state.History) if (VendorNames.IsVendorPlace(day.PlaceId)) into.Retained.Add(day.PlaceId);
            foreach (var reference in state.BusinessReferences) if (VendorNames.IsVendorPlace(reference.PlaceId)) into.Memory.Add(reference.PlaceId);
        }

        /// <summary>False while the shared awareness image waits for a rebuild (after a vendor place change, for example).</summary>
        internal bool FoodAwarenessCurrent => food != null && food.AwarenessCurrent;

        /// <summary>A route rebuild replaces the simulator; vendor places live outside CityState.Places and move along.</summary>
        private void CopyVendorPlaces(CitySimulator from)
        {
            foreach (var pair in from.places)
                if (VendorNames.IsVendorPlace(pair.Key)) places.Add(pair.Key, pair.Value);
        }
    }
}
