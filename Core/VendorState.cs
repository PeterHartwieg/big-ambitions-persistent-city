using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PersistentCity.Core
{
    public enum VendorApiStatus { Ready = 0, Off = 1, NotYet = 2, Refused = 3, Failed = 4 }

    public static class VendorNames
    {
        public const string Owner = "persistentcity";
        public const int MaximumIdLength = 128;
        public const string PlacePrefix = "vendor:";
        public const string SignaturePrefix = "streetfood:vehicle:";

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

        public const int MaximumEscapedIdLength = 9 * MaximumIdLength;
        public const int MaximumPlaceIdLength = 7 + MaximumEscapedIdLength;
        public const int MaximumBusinessIdLength = 19 + MaximumEscapedIdLength + 12 + 10;

        public static bool IsVendorIdentity(string? id) => IsVendorPlace(id) || id != null && id.StartsWith(SignaturePrefix, StringComparison.Ordinal);

        public static int Bound(string? id, int max) => IsVendorPlace(id) ? Math.Max(max, MaximumPlaceIdLength)
            : IsVendorIdentity(id) ? Math.Max(max, MaximumBusinessIdLength) : max;

        public static string PlaceId(string vehicleId) => PlacePrefix + Escape(vehicleId);
        public static string Signature(string vehicleId) => SignaturePrefix + Escape(vehicleId);
        public static string BusinessId(string vehicleId, int generation) => generation < 1 ? throw new ArgumentOutOfRangeException(nameof(generation))
            : Signature(vehicleId) + ":generation:" + generation.ToString(CultureInfo.InvariantCulture);
        public static bool IsVendorPlace(string? placeId) => placeId != null && placeId.StartsWith(PlacePrefix, StringComparison.Ordinal);
    }

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
        public int ItemCount { get; set; }
        public string[] Items { get; } = new string[MaximumItems];
        public long[] PriceCents { get; } = new long[MaximumItems];
        public int[] Units { get; } = new int[MaximumItems];
        public string ShapeFault { get; set; } = "";

        public Position Window => new Position(X, Y, Z);
        public bool OwnedByPersistentCity => Open && OpeningOwner == VendorNames.Owner;
        public long OpenedHour => OpenedDay * 24L + OpenedMinute / 60;
        public long OpenedAt => OpenedDay * 1440L + OpenedMinute;
        public long ClosesAt => ClosesDay < 0 ? -1 : ClosesDay * 1440L + ClosesMinute;

        public int ItemIndex(string item)
        {
            for (int i = 0; i < ItemCount && i < MaximumItems; i++) if (string.Equals(Items[i], item, StringComparison.Ordinal)) return i;
            return -1;
        }

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

    public sealed class VendorImage
    {
        public const int MaximumVehicles = 64;
        private readonly VendorVehicle[] slots = new VendorVehicle[MaximumVehicles];
        public VendorImage() { for (int i = 0; i < slots.Length; i++) slots[i] = new VendorVehicle(); }
        public VendorApiStatus Status { get; internal set; } = VendorApiStatus.NotYet;
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

    public interface IVendorApi
    {
        long Revision { get; }
        VendorApiStatus BeginRead(out int count);
        void CopyVehicle(int index, VendorVehicle into);
        VendorApiStatus RequestOwner(string vehicleId, bool want, out string reason);
        VendorApiStatus Renew(int nativeDay, int nativeMinute, out int leaseUntilDay, out int leaseUntilMinute);
        VendorApiStatus ReleaseAll();
        VendorApiStatus Sell(string vehicleId, long openingId, string item, long priceCents, int nativeDay, int nativeMinute,
            out VendorSaleResult? sale, out bool uncertain, out string reason);
    }

    public enum VendorRowState { Live = 0, Retired = 1, Tombstone = 2 }

    public sealed class VendorStand
    {
        public long OpeningId { get; set; }
        public Position Position { get; set; }
        public int OpenedDay { get; set; }
        public int OpenedMinute { get; set; }
        public int ClosesDay { get; set; } = -1;
        public int ClosesMinute { get; set; } = -1;
        public bool Published { get; set; }
        public long OpenedAt => OpenedDay * 1440L + OpenedMinute;
        public long ClosesAt => ClosesDay < 0 ? -1 : ClosesDay * 1440L + ClosesMinute;
    }

    public sealed class VendorAuthority
    {
        public bool Desired { get; set; }
        public long RequestedHour { get; set; } = -1;
        public long FromHour { get; set; } = -1;
        public long UntilHour { get; set; } = -1;
        public bool Suspended { get; set; }
        public long OpeningId { get; set; } = -1;
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
        public VendorStand? Stand { get; internal set; }
        public VendorStand? DeferredStand { get; internal set; }
        public VendorAuthority Authority { get; internal set; } = new VendorAuthority();
        public bool PlacePublished { get; internal set; }
        public string Reason { get; internal set; } = "";
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

        public string? AdmissionRefusal(VendorRow? existing)
        {
            if (existing?.State == VendorRowState.Live) return null;
            if (Live >= MaximumLive) return "vendor table full: 64 live vehicles";
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
