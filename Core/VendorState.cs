using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PersistentCity.Core
{
    /// <summary>Street Food's <c>VendorApiStatus</c> (E6 plan 3.2), by value.</summary>
    public enum VendorApiStatus { Ready = 0, Off = 1, NotYet = 2, Refused = 3, Failed = 4 }

    /// <summary>Stable names derived from Street Food's vehicle id. The place id and the signature both come from the
    /// id, so a returning vehicle gets the same place and the next generation of the same signature.</summary>
    public static class VendorNames
    {
        public const string Owner = "persistentcity";
        public const int MaximumIdLength = 128;
        public const string PlacePrefix = "vendor:";
        public const string SignaturePrefix = "streetfood:vehicle:";

        /// <summary>1 to 128 UTF-16 units, no control characters, no unpaired surrogate.</summary>
        public static bool ValidId(string? id)
        {
            if (id == null || id.Length == 0 || id.Length > MaximumIdLength) return false;
            for (int i = 0; i < id.Length; i++)
            {
                char c = id[i];
                if (char.IsControl(c)) return false;
                if (char.IsHighSurrogate(c)) { if (i + 1 == id.Length || !char.IsLowSurrogate(id[i + 1])) return false; i++; }
                else if (char.IsLowSurrogate(c)) return false;
            }
            return true;
        }

        /// <summary>Letters, digits, '-', '_' and '.' stay; every other UTF-8 byte becomes %XX. Injective on valid ids.</summary>
        public static string Escape(string id)
        {
            if (!ValidId(id)) throw new ArgumentException("Invalid Street Food vehicle id.", nameof(id));
            StringBuilder? escaped = null;
            for (int i = 0; i < id.Length; i++)
            {
                char c = id[i];
                bool plain = c < 128 && (char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.');
                if (plain) { escaped?.Append(c); continue; }
                if (escaped == null) escaped = new StringBuilder(id, 0, i, id.Length * 3);
                int length = char.IsHighSurrogate(c) ? 2 : 1;
                foreach (byte b in Encoding.UTF8.GetBytes(id.ToCharArray(i, length))) escaped.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
                i += length - 1;
            }
            return escaped == null ? id : escaped.ToString();
        }

        /// <summary>Longest escaped id: a BMP unit outside ASCII escapes to three UTF-8 bytes, nine characters (a surrogate
        /// pair to four bytes, twelve characters for two units), so 128 units escape to at most 1,152 characters.</summary>
        public const int MaximumEscapedIdLength = 9 * MaximumIdLength;
        /// <summary>Longest place id, <c>vendor:&lt;escaped id&gt;</c>.</summary>
        public const int MaximumPlaceIdLength = 7 + MaximumEscapedIdLength;
        /// <summary>Longest business id, <c>streetfood:vehicle:&lt;escaped id&gt;:generation:&lt;g&gt;</c> with g up to int.MaxValue.</summary>
        public const int MaximumBusinessIdLength = 19 + MaximumEscapedIdLength + 12 + 10;

        /// <summary>A place id or business id derived from a vehicle id. Saved fields that hold them allow the vendor
        /// maxima above instead of their ordinary 256 and 512 characters.</summary>
        public static bool IsVendorIdentity(string? id) => IsVendorPlace(id) || id != null && id.StartsWith(SignaturePrefix, StringComparison.Ordinal);

        /// <summary>The length bound for a place or business field: a vendor place or business id may reach its escaped
        /// maximum; every other value keeps the field's own bound.</summary>
        public static int Bound(string? id, int max) => IsVendorPlace(id) ? Math.Max(max, MaximumPlaceIdLength)
            : IsVendorIdentity(id) ? Math.Max(max, MaximumBusinessIdLength) : max;

        public static string PlaceId(string vehicleId) => PlacePrefix + Escape(vehicleId);
        public static string Signature(string vehicleId) => SignaturePrefix + Escape(vehicleId);
        public static string BusinessId(string vehicleId, int generation) => generation < 1 ? throw new ArgumentOutOfRangeException(nameof(generation))
            : Signature(vehicleId) + ":generation:" + generation.ToString(CultureInfo.InvariantCulture);
        public static bool IsVendorPlace(string? placeId) => placeId != null && placeId.StartsWith(PlacePrefix, StringComparison.Ordinal);
    }

    /// <summary>One copied vehicle of a VendorApi snapshot. Slots are reused between copies; nothing here belongs to
    /// Street Food. <see cref="Check"/> decides whether the copy honours contract 1.</summary>
    public sealed class VendorVehicle
    {
        public const int MaximumItems = 8;
        public string VehicleId { get; set; } = "";
        public string Kind { get; set; } = "";
        public bool Open { get; set; }
        public long OpeningId { get; set; }
        public string OpeningOwner { get; set; } = "";
        public bool OpenedByCrew { get; set; }
        public bool EventOrCatering { get; set; }
        public int OpenedDay { get; set; }
        public int OpenedMinute { get; set; }
        public int ClosesDay { get; set; }
        public int ClosesMinute { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float QueueX { get; set; }
        public float QueueY { get; set; }
        public float QueueZ { get; set; }
        public float QueueHeading { get; set; }
        public int MealsPerHour { get; set; }
        public bool MenuOverflow { get; set; }
        /// <summary>Number of menu entries Street Food reported; above 8 only when the copy was malformed.</summary>
        public int ItemCount { get; set; }
        public string[] Items { get; } = new string[MaximumItems];
        public long[] PriceCents { get; } = new long[MaximumItems];
        public int[] Units { get; } = new int[MaximumItems];
        /// <summary>Set by the copier when the snapshot's shape is wrong (null or mismatched arrays).</summary>
        public string ShapeFault { get; set; } = "";

        public Position Window => new Position(X, Y, Z);
        public bool OwnedByPersistentCity => Open && OpeningOwner == VendorNames.Owner;
        /// <summary>Native hour of the opening, <c>day * 24 + hour</c>, the unit of Street Food's grant rule.</summary>
        public long OpenedHour => OpenedDay * 24L + OpenedMinute / 60;
        /// <summary>PC absolute minute (<c>day * 1440 + minute</c>) of the opening and of its planned close (-1 when none).</summary>
        public long OpenedAt => OpenedDay * 1440L + OpenedMinute;
        public long ClosesAt => ClosesDay < 0 ? -1 : ClosesDay * 1440L + ClosesMinute;

        public int ItemIndex(string item)
        {
            for (int i = 0; i < ItemCount && i < MaximumItems; i++) if (string.Equals(Items[i], item, StringComparison.Ordinal)) return i;
            return -1;
        }

        /// <summary>Null when this copy honours contract 1, otherwise the reason it does not.</summary>
        public string? Check()
        {
            if (ShapeFault.Length != 0) return ShapeFault;
            if (!VendorNames.ValidId(VehicleId)) return "invalid vehicle id";
            if (Kind != "cart" && Kind != "truck") return "unknown kind";
            if (OpeningId < 0 || Open && OpeningId == 0) return "invalid opening id";
            if (OpeningOwner == null || OpeningOwner.Length > 64) return "invalid opening owner";
            foreach (char c in OpeningOwner) if (char.IsControl(c)) return "invalid opening owner";
            if (Open)
            {
                if (OpenedDay < 0 || OpenedMinute < 0 || OpenedMinute >= 1440) return "invalid opening time";
                bool noClose = ClosesDay == -1 && ClosesMinute == -1;
                if (!noClose && (ClosesDay < OpenedDay || ClosesMinute < 0 || ClosesMinute >= 1440 || ClosesAt <= OpenedAt)) return "invalid closing time";
            }
            if (!Coordinate(X) || !Coordinate(Y) || !Coordinate(Z) || !Coordinate(QueueX) || !Coordinate(QueueY) || !Coordinate(QueueZ) || !Coordinate(QueueHeading))
                return "invalid position";
            if (MealsPerHour < 0) return "invalid serving rate";
            if (MenuOverflow) return ItemCount == 0 ? null : "menu items listed with MenuOverflow";
            if (ItemCount < 0 || ItemCount > MaximumItems) return "more than 8 menu items without MenuOverflow";
            for (int i = 0; i < ItemCount; i++)
            {
                string item = Items[i];
                if (string.IsNullOrWhiteSpace(item) || item.Length > 256) return "invalid menu item";
                foreach (char c in item) if (char.IsControl(c)) return "invalid menu item";
                for (int j = 0; j < i; j++) if (string.Equals(Items[j], item, StringComparison.Ordinal)) return "duplicate menu item " + item;
                if (PriceCents[i] <= 0) return "price of " + item + " is not positive";
                if (Units[i] < 0) return "negative units of " + item;
            }
            return null;
        }

        private static bool Coordinate(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && Math.Abs(value) <= 1e6f;
    }

    /// <summary>A validated, detached copy of Street Food's vehicles. Its 64 slots are allocated once.</summary>
    public sealed class VendorImage
    {
        public const int MaximumVehicles = 64;
        private readonly VendorVehicle[] slots = new VendorVehicle[MaximumVehicles];
        public VendorImage() { for (int i = 0; i < slots.Length; i++) slots[i] = new VendorVehicle(); }
        public VendorApiStatus Status { get; internal set; } = VendorApiStatus.NotYet;
        /// <summary>Why vehicles cannot be served from this image; empty when Ready.</summary>
        public string Reason { get; internal set; } = "Street Food has not been read yet.";
        public long Revision { get; internal set; } = long.MinValue;
        public int Count { get; internal set; }
        public bool Ready => Status == VendorApiStatus.Ready;
        public VendorVehicle this[int index] => (uint)index < (uint)Count ? slots[index] : throw new ArgumentOutOfRangeException(nameof(index));
        internal VendorVehicle Slot(int index) => slots[index];
        public VendorVehicle? Find(string vehicleId)
        {
            for (int i = 0; i < Count; i++) if (string.Equals(slots[i].VehicleId, vehicleId, StringComparison.Ordinal)) return slots[i];
            return null;
        }
        internal void Refuse(VendorApiStatus status, string reason, long revision)
        { Status = status; Reason = reason; Revision = revision; Count = 0; }
        /// <summary>Checks every copied vehicle and that ids are unique. Null when the whole image is valid.</summary>
        internal string? Validate()
        {
            for (int i = 0; i < Count; i++)
            {
                string? fault = slots[i].Check();
                if (fault != null) return "vehicle " + i.ToString(CultureInfo.InvariantCulture) + ": " + fault;
                for (int j = 0; j < i; j++)
                    if (string.Equals(slots[j].VehicleId, slots[i].VehicleId, StringComparison.Ordinal)) return "duplicate vehicle id";
            }
            return null;
        }
    }

    /// <summary>Street Food's answer to a confirmed one-item sale (<c>VendorApiSale</c>).</summary>
    public sealed class VendorSaleResult
    {
        public VendorSaleResult(int bookedDay, long cents, int unitsBefore, int unitsAfter, long sequence)
        { BookedDay = bookedDay; Cents = cents; UnitsBefore = unitsBefore; UnitsAfter = unitsAfter; Sequence = sequence; }
        public int BookedDay { get; }
        public long Cents { get; }
        public int UnitsBefore { get; }
        public int UnitsAfter { get; }
        public long Sequence { get; }
    }

    /// <summary>Persistent City's side of Street Food's VendorApi contract 1. The Compatibility bridge implements it
    /// with the owner fixed to <see cref="VendorNames.Owner"/>; tests use doubles. Main thread only. A broken contract
    /// throws <see cref="InvalidOperationException"/>.</summary>
    public interface IVendorApi
    {
        /// <summary>One static property read; no allocation.</summary>
        long Revision { get; }
        /// <summary>Calls Street Food's Read once and keeps its detached view for <see cref="CopyVehicle"/>.</summary>
        VendorApiStatus BeginRead(out int count);
        /// <summary>Copies vehicle <paramref name="index"/> of the last <see cref="BeginRead"/> into a reused slot.</summary>
        void CopyVehicle(int index, VendorVehicle into);
        VendorApiStatus RequestOwner(string vehicleId, bool want, out string reason);
        VendorApiStatus Renew(int nativeDay, int nativeMinute, out int leaseUntilDay, out int leaseUntilMinute);
        VendorApiStatus ReleaseAll();
        VendorApiStatus Sell(string vehicleId, long openingId, string item, long priceCents, int nativeDay, int nativeMinute,
            out VendorSaleResult? sale, out bool uncertain, out string reason);
    }

    public enum VendorRowState { Live = 0, Retired = 1, Tombstone = 2 }

    /// <summary>One opening at one position. Times are native day and minute of day; closes is -1 when none is planned.</summary>
    public sealed class VendorStand
    {
        public long OpeningId { get; set; }
        public Position Position { get; set; }
        public int OpenedDay { get; set; }
        public int OpenedMinute { get; set; }
        public int ClosesDay { get; set; } = -1;
        public int ClosesMinute { get; set; } = -1;
        /// <summary>True while the vendor place shows this stand's opening window.</summary>
        public bool Published { get; set; }
        public long OpenedAt => OpenedDay * 1440L + OpenedMinute;
        public long ClosesAt => ClosesDay < 0 ? -1 : ClosesDay * 1440L + ClosesMinute;
    }

    /// <summary>The ownership record of one vehicle (plan 3.5), in native hours (<c>day * 24 + hour</c>).</summary>
    public sealed class VendorAuthority
    {
        public bool Desired { get; set; }
        /// <summary>Hour of the last <c>RequestOwner(true)</c>; -1 when no request is recorded.</summary>
        public long RequestedHour { get; set; } = -1;
        public long FromHour { get; set; } = -1;
        public long UntilHour { get; set; } = -1;
        /// <summary>An owned opening that PC must not serve (lease lapsed, image not ready).</summary>
        public bool Suspended { get; set; }
        /// <summary>The Street Food opening PC accepted and serves; -1 when none.</summary>
        public long OpeningId { get; set; } = -1;
        /// <summary><c>RequestOwner(false)</c> was sent but not confirmed; retried before any new request.</summary>
        public bool HandbackPending { get; set; }
        public bool Requested => RequestedHour >= 0;
        public bool Managed => FromHour >= 0 && UntilHour < 0;
    }

    public sealed class VendorRow : IVendorRowView
    {
        internal VendorRow(string vehicleId, string kind)
        {
            VehicleId = vehicleId; Kind = kind;
            PlaceId = VendorNames.PlaceId(vehicleId); Signature = VendorNames.Signature(vehicleId);
        }
        public string VehicleId { get; }
        public string Kind { get; internal set; }
        public int Generation { get; internal set; } = 1;
        public VendorRowState State { get; internal set; }
        public string PlaceId { get; }
        public string Signature { get; }
        public string BusinessId => Signature + ":generation:" + Generation.ToString(CultureInfo.InvariantCulture);
        /// <summary>The stand the vendor place shows; null before the first accepted opening.</summary>
        public VendorStand? Stand { get; internal set; }
        /// <summary>A newer stand that waits until nothing short-lived names the old position.</summary>
        public VendorStand? DeferredStand { get; internal set; }
        public VendorAuthority Authority { get; internal set; } = new VendorAuthority();
        /// <summary>True while the vendor place and its Vehicle scope exist in the simulator.</summary>
        public bool PlacePublished { get; internal set; }
        /// <summary>Why this vehicle is not served right now (diagnostic, never saved).</summary>
        public string Reason { get; internal set; } = "";
        // F9e: the read-only view (getters only; no field, wire or save change).
        string IVendorRowView.VehicleId => VehicleId;
        string IVendorRowView.Kind => Kind;
        int IVendorRowView.Generation => Generation;
        VendorRowState IVendorRowView.State => State;
        string IVendorRowView.PlaceId => PlaceId;
        string IVendorRowView.Signature => Signature;
        string IVendorRowView.BusinessId => BusinessId;
        bool IVendorRowView.PlacePublished => PlacePublished;
        string IVendorRowView.Reason => Reason;
        bool IVendorRowView.AuthorityDesired => Authority.Desired;
        bool IVendorRowView.AuthorityManaged => Authority.Managed;
        bool IVendorRowView.Suspended => Authority.Suspended;
        bool IVendorRowView.HandbackPending => Authority.HandbackPending;
        long IVendorRowView.OpeningId => Authority.OpeningId;
        bool IVendorRowView.HasStand => Stand != null;
    }

    /// <summary>The vendor rows, ordinal by vehicle id, with the capacity invariant of plan 3.5:
    /// live &lt;= 64, live + retired &lt;= 128, tombstones &lt;= 1,024. Every transition keeps it.</summary>
    public sealed class VendorTable
    {
        public const int MaximumLive = 64, MaximumLiveAndRetired = 128, MaximumTombstones = 1024;
        private readonly List<VendorRow> rows = new List<VendorRow>();
        public IReadOnlyList<VendorRow> Rows => rows;
        public int Live { get; private set; }
        public int Retired { get; private set; }
        public int Tombstones { get; private set; }

        public VendorRow? Find(string vehicleId)
        {
            int index = IndexOf(vehicleId);
            return index >= 0 ? rows[index] : null;
        }

        public VendorRow? FindPlace(string placeId)
        {
            foreach (var row in rows) if (row.PlaceId == placeId) return row;
            return null;
        }

        /// <summary>Null when a new vehicle may become live, otherwise why it stays with Street Food.</summary>
        public string? AdmissionRefusal(VendorRow? existing)
        {
            if (existing?.State == VendorRowState.Live) return null;
            if (Live >= MaximumLive) return "vendor table full: 64 live vehicles";
            // A retired row becoming live keeps live + retired; a new or tombstoned row adds one.
            if (existing?.State != VendorRowState.Retired && Live + Retired >= MaximumLiveAndRetired) return "vendor table full: 128 live and retired vehicles";
            return null;
        }

        internal VendorRow Admit(string vehicleId, string kind)
        {
            if (AdmissionRefusal(null) != null) throw new InvalidOperationException("Vendor table is full.");
            int index = IndexOf(vehicleId);
            if (index >= 0) throw new InvalidOperationException("Vendor row already exists.");
            var row = new VendorRow(vehicleId, kind);
            rows.Insert(~index, row);
            Live++;
            return row;
        }

        internal void Move(VendorRow row, VendorRowState to)
        {
            if (row.State == to) return;
            if (to == VendorRowState.Live && AdmissionRefusal(row) != null) throw new InvalidOperationException("Vendor table is full.");
            if (to == VendorRowState.Tombstone && Tombstones >= MaximumTombstones) throw new InvalidOperationException("Vendor tombstones are full.");
            Count(row.State, -1); Count(to, 1);
            row.State = to;
            CheckInvariant();
        }

        internal void Remove(VendorRow row)
        {
            if (!rows.Remove(row)) throw new InvalidOperationException("Unknown vendor row.");
            Count(row.State, -1);
        }

        public void CheckInvariant()
        {
            if (Live > MaximumLive || Live + Retired > MaximumLiveAndRetired || Tombstones > MaximumTombstones || Live < 0 || Retired < 0 || Tombstones < 0)
                throw new InvalidOperationException("Vendor capacity invariant broken.");
        }

        private void Count(VendorRowState state, int delta)
        {
            if (state == VendorRowState.Live) Live += delta;
            else if (state == VendorRowState.Retired) Retired += delta;
            else Tombstones += delta;
        }

        private int IndexOf(string vehicleId)
        {
            int low = 0, high = rows.Count - 1;
            while (low <= high)
            {
                int middle = (low + high) >> 1, order = StringComparer.Ordinal.Compare(rows[middle].VehicleId, vehicleId);
                if (order == 0) return middle;
                if (order < 0) low = middle + 1; else high = middle - 1;
            }
            return ~low;
        }
    }
}
