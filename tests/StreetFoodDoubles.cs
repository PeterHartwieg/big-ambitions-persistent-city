using System;
using System.Collections.Generic;
using PersistentCity.Tests;

#pragma warning disable CS0649, CS0169, CS8618 // the variants' DTO fields only exist to be bound, never assigned

// A contract-shaped stand-in for Street Food's VendorApi contract 1 (E6 plan 3.2): the same public names, types and
// signatures, answering from StreetFoodHooks. It is not Street Food's code and reads nothing of Street Food's.
namespace FoodTrucks.ModApi
{
    public enum VendorApiStatus { Ready = 0, Off = 1, NotYet = 2, Refused = 3, Failed = 4 }

    public sealed class VendorApiVehicle
    {
        public readonly string VehicleId;
        public readonly string Kind;
        public readonly bool Open;
        public readonly long OpeningId;
        public readonly string OpeningOwner;
        public readonly bool OpenedByCrew;
        public readonly bool EventOrCatering;
        public readonly int OpenedDay, OpenedMinute;
        public readonly int ClosesDay, ClosesMinute;
        public readonly float X, Y, Z;
        public readonly float QueueX, QueueY, QueueZ, QueueHeading;
        public readonly int MealsPerHour;
        public readonly bool MenuOverflow;
        public readonly string[] Items;
        public readonly long[] PriceCents;
        public readonly int[] Units;

        public VendorApiVehicle(StreetFoodHooks.Vehicle v)
        {
            VehicleId = v.Id; Kind = v.Kind; Open = v.Open; OpeningId = v.OpeningId; OpeningOwner = v.Owner; OpenedByCrew = v.Crew; EventOrCatering = v.Event;
            OpenedDay = v.OpenedDay; OpenedMinute = v.OpenedMinute; ClosesDay = v.ClosesDay; ClosesMinute = v.ClosesMinute;
            X = v.X; Y = v.Y; Z = v.Z; QueueX = v.X + 1; QueueY = v.Y; QueueZ = v.Z; QueueHeading = 90;
            MealsPerHour = v.MealsPerHour; MenuOverflow = v.Items.Length > 8;
            Items = MenuOverflow ? new string[0] : (string[])v.Items.Clone();
            PriceCents = MenuOverflow ? new long[0] : (long[])v.Prices.Clone();
            Units = MenuOverflow ? new int[0] : (int[])v.Units.Clone();
            if (v.NullItems) Items = null!;
        }
    }

    public sealed class VendorApiSale
    {
        public readonly int BookedDay;
        public readonly long Cents;
        public readonly int UnitsBefore, UnitsAfter;
        public readonly long Sequence;
        public VendorApiSale(int day, long cents, int before, int after, long sequence) { BookedDay = day; Cents = cents; UnitsBefore = before; UnitsAfter = after; Sequence = sequence; }
    }

    public static class VendorApi
    {
        public static int ContractVersion => StreetFoodHooks.ContractVersion;
        public static long Revision => StreetFoodHooks.Revision;
        public static VendorApiStatus Read(VendorApiVehicle[] into, out int count) => (VendorApiStatus)StreetFoodHooks.Read(into, out count);
        public static VendorApiStatus RequestOwner(string owner, string vehicleId, bool want, out string reason) => (VendorApiStatus)StreetFoodHooks.RequestOwner(owner, vehicleId, want, out reason);
        public static VendorApiStatus Renew(string owner, int nativeDay, int nativeMinute, out int leaseUntilDay, out int leaseUntilMinute)
            => (VendorApiStatus)StreetFoodHooks.Renew(owner, nativeDay, nativeMinute, out leaseUntilDay, out leaseUntilMinute);
        public static VendorApiStatus ReleaseAll(string owner) => (VendorApiStatus)StreetFoodHooks.ReleaseAll(owner);
        public static VendorApiStatus Sell(string owner, string vehicleId, long openingId, string item, long priceCents, int nativeDay, int nativeMinute,
            out VendorApiSale? sale, out bool uncertain, out string reason)
        {
            int status = StreetFoodHooks.Sell(owner, vehicleId, openingId, item, priceCents, nativeDay, nativeMinute, out var made, out uncertain, out reason);
            sale = made == null ? null : new VendorApiSale(made.Value.Day, made.Value.Cents, made.Value.Before, made.Value.After, made.Value.Sequence);
            return (VendorApiStatus)status;
        }
    }
}

// Contract variants that the bridge must refuse. Their methods are never called: binding fails first.
namespace StreetFoodVariants.Version2.FoodTrucks.ModApi
{
    public enum VendorApiStatus { Ready = 0, Off = 1, NotYet = 2, Refused = 3, Failed = 4 }
    public sealed class VendorApiVehicle { public readonly string VehicleId; }
    public sealed class VendorApiSale { public readonly int BookedDay; }
    public static class VendorApi { public static int ContractVersion => 2; public static long Revision => 0; }
}

namespace StreetFoodVariants.MissingRenew.FoodTrucks.ModApi
{
    public enum VendorApiStatus { Ready = 0, Off = 1, NotYet = 2, Refused = 3, Failed = 4 }
    public sealed class VendorApiVehicle
    {
        public readonly string VehicleId, Kind, OpeningOwner; public readonly bool Open, OpenedByCrew, EventOrCatering, MenuOverflow; public readonly long OpeningId;
        public readonly int OpenedDay, OpenedMinute, ClosesDay, ClosesMinute, MealsPerHour; public readonly float X, Y, Z, QueueX, QueueY, QueueZ, QueueHeading;
        public readonly string[] Items; public readonly long[] PriceCents; public readonly int[] Units;
    }
    public sealed class VendorApiSale { public readonly int BookedDay; public readonly long Cents; public readonly int UnitsBefore, UnitsAfter; public readonly long Sequence; }
    public static class VendorApi
    {
        public static int ContractVersion => 1;
        public static long Revision => 0;
        public static VendorApiStatus Read(VendorApiVehicle[] into, out int count) { count = 0; return VendorApiStatus.Ready; }
        public static VendorApiStatus RequestOwner(string owner, string vehicleId, bool want, out string reason) { reason = ""; return VendorApiStatus.Ready; }
        public static VendorApiStatus ReleaseAll(string owner) => VendorApiStatus.Ready;
        public static VendorApiStatus Sell(string owner, string vehicleId, long openingId, string item, long priceCents, int nativeDay, int nativeMinute,
            out VendorApiSale? sale, out bool uncertain, out string reason) { sale = null; uncertain = false; reason = ""; return VendorApiStatus.Refused; }
    }
}

namespace StreetFoodVariants.IntPrices.FoodTrucks.ModApi
{
    public enum VendorApiStatus { Ready = 0, Off = 1, NotYet = 2, Refused = 3, Failed = 4 }
    public sealed class VendorApiVehicle
    {
        public readonly string VehicleId, Kind, OpeningOwner; public readonly bool Open, OpenedByCrew, EventOrCatering, MenuOverflow; public readonly long OpeningId;
        public readonly int OpenedDay, OpenedMinute, ClosesDay, ClosesMinute, MealsPerHour; public readonly float X, Y, Z, QueueX, QueueY, QueueZ, QueueHeading;
        public readonly string[] Items; public readonly int[] PriceCents; public readonly int[] Units; // float-free but not the contract's long cents
    }
    public sealed class VendorApiSale { public readonly int BookedDay; public readonly long Cents; public readonly int UnitsBefore, UnitsAfter; public readonly long Sequence; }
    public static class VendorApi { public static int ContractVersion => 1; public static long Revision => 0; }
}

namespace StreetFoodVariants.IntRevision.FoodTrucks.ModApi
{
    public enum VendorApiStatus { Ready = 0, Off = 1, NotYet = 2, Refused = 3, Failed = 4 }
    public sealed class VendorApiVehicle { public readonly string VehicleId; }
    public sealed class VendorApiSale { public readonly int BookedDay; }
    public static class VendorApi { public static int ContractVersion => 1; public static int Revision => 0; }
}

namespace StreetFoodVariants.ShiftedStatus.FoodTrucks.ModApi
{
    public enum VendorApiStatus { Ready = 1, Off = 0, NotYet = 2, Refused = 3, Failed = 4 }
    public sealed class VendorApiVehicle { public readonly string VehicleId; }
    public sealed class VendorApiSale { public readonly int BookedDay; }
    public static class VendorApi { public static int ContractVersion => 1; public static long Revision => 0; }
}

namespace PersistentCity.Tests
{
    /// <summary>The stand-in is static, and the E6b perf lines need a quiet CPU: this collection runs alone.</summary>
    [Xunit.CollectionDefinition(StreetFoodHooks.Collection, DisableParallelization = true)]
    public sealed class StreetFoodCollection { }

    /// <summary>The stand-in's behaviour: Street Food's promised contract 1 rules (grant only to an opening in a later
    /// native hour than the recorded request while the lease is valid; a repeated request keeps the first hour; release
    /// and lease expiry hand back at once). Static, so every test using it resets it and runs in one collection.</summary>
    public static class StreetFoodHooks
    {
        public const string Collection = "Street Food vendor API stand-in";
        public sealed class Vehicle
        {
            public string Id = "", Kind = "cart", Owner = "";
            public bool Open, Crew = true, Event, NullItems;
            public long OpeningId;
            public int OpenedDay, OpenedMinute, ClosesDay = -1, ClosesMinute = -1, MealsPerHour = 30;
            public float X, Y, Z;
            public string[] Items = { PersistentCity.Core.FoodProducts.HotDog };
            public long[] Prices = { 450 };
            public int[] Units = { 20 };
        }
        public readonly struct Sale
        {
            public Sale(int day, long cents, int before, int after, long sequence) { Day = day; Cents = cents; Before = before; After = after; Sequence = sequence; }
            public int Day { get; } public long Cents { get; } public int Before { get; } public int After { get; } public long Sequence { get; }
        }

        public static int ContractVersion;
        public static long Revision;
        public static int ReadStatus;
        public static int ReadCalls;
        public static bool ThrowOnRead;
        public static readonly HashSet<string> FailRelease = new HashSet<string>(StringComparer.Ordinal);
        public static readonly HashSet<string> FailRequest = new HashSet<string>(StringComparer.Ordinal);
        public static readonly List<Vehicle> Vehicles = new List<Vehicle>();
        public static readonly Dictionary<string, long> Wishes = new Dictionary<string, long>(StringComparer.Ordinal);
        public static readonly List<string> Calls = new List<string>();
        public static long LeaseUntil = -1; // PC absolute minute
        public static long Now;               // native clock, PC absolute minute (day * 1440 + minute)
        public static long Sequence;

        public static void Reset()
        {
            ContractVersion = 1; Revision = 1; ReadStatus = 0; ReadCalls = 0; ThrowOnRead = false; FailRelease.Clear(); FailRequest.Clear();
            Vehicles.Clear(); Wishes.Clear(); Calls.Clear(); LeaseUntil = -1; Now = 2 * 1440 + 8 * 60; Sequence = 0;
        }

        public static Vehicle Add(string id, string kind = "cart", float x = 100, params string[] items)
        {
            var v = new Vehicle { Id = id, Kind = kind, X = x };
            if (items.Length > 0) { v.Items = items; v.Prices = new long[items.Length]; v.Units = new int[items.Length]; for (int i = 0; i < items.Length; i++) { v.Prices[i] = 400 + i; v.Units[i] = 10; } }
            Vehicles.Add(v); Revision++;
            return v;
        }
        public static Vehicle Find(string id) => Vehicles.Find(v => v.Id == id) ?? throw new InvalidOperationException("no vehicle " + id);

        /// <summary>Opens the vehicle now for <paramref name="minutes"/>, applying Street Food's grant rule.</summary>
        public static void Open(string id, int minutes = 120, bool crew = true, bool cityEvent = false, float? x = null)
        {
            Expire();
            var v = Find(id);
            v.Open = true; v.OpeningId++; v.Crew = crew; v.Event = cityEvent; if (x.HasValue) v.X = x.Value;
            v.OpenedDay = (int)(Now / 1440); v.OpenedMinute = (int)(Now % 1440);
            long close = Now + minutes; v.ClosesDay = (int)(close / 1440); v.ClosesMinute = (int)(close % 1440);
            long hour = Now / 60;
            v.Owner = Wishes.TryGetValue(id, out long wished) && hour > wished && Now < LeaseUntil ? PersistentCity.Core.VendorNames.Owner : "";
            Revision++;
        }
        public static void Close(string id) { var v = Find(id); v.Open = false; v.Owner = ""; Revision++; }
        public static void Remove(string id) { Vehicles.RemoveAll(v => v.Id == id); Wishes.Remove(id); Revision++; }

        /// <summary>A lease that has run out hands every opening back and drops the recorded requests.</summary>
        public static void Expire()
        {
            if (LeaseUntil < 0 || Now < LeaseUntil) return;
            bool changed = Wishes.Count > 0;
            Wishes.Clear();
            foreach (var v in Vehicles) if (v.Owner.Length != 0) { v.Owner = ""; changed = true; }
            if (changed) Revision++;
        }

        public static int Read(Array into, out int count)
        {
            ReadCalls++;
            if (ThrowOnRead) throw new InvalidOperationException("stand-in fault");
            count = 0;
            if (ReadStatus != 0) return ReadStatus;
            if (Vehicles.Count > 64) return 3;
            for (int i = 0; i < Vehicles.Count; i++) into.SetValue(new FoodTrucks.ModApi.VendorApiVehicle(Vehicles[i]), i);
            count = Vehicles.Count;
            return 0;
        }

        public static int RequestOwner(string owner, string id, bool want, out string reason)
        {
            Calls.Add((want ? "request " : "release ") + id);
            reason = "";
            var v = Vehicles.Find(x => x.Id == id);
            if (v == null) { reason = "unknown vehicle"; return 3; }
            if (want && FailRequest.Contains(id)) { reason = "stand-in request fault"; return 4; }
            if (want) { if (!Wishes.ContainsKey(id)) Wishes[id] = Now / 60; return 0; }
            if (FailRelease.Contains(id)) { reason = "stand-in release fault"; return 4; }
            Wishes.Remove(id);
            if (v.Owner == owner) { v.Owner = ""; Revision++; }
            return 0;
        }

        public static int Renew(string owner, int day, int minute, out int untilDay, out int untilMinute)
        {
            Calls.Add("renew");
            Expire();
            LeaseUntil = day * 1440L + minute + 60;
            untilDay = (int)(LeaseUntil / 1440); untilMinute = (int)(LeaseUntil % 1440);
            return 0;
        }

        public static int ReleaseAll(string owner)
        {
            Calls.Add("release all");
            Wishes.Clear();
            foreach (var v in Vehicles) if (v.Owner == owner) v.Owner = "";
            Revision++;
            return 0;
        }

        public static int Sell(string owner, string id, long opening, string item, long cents, int day, int minute, out Sale? sale, out bool uncertain, out string reason)
        {
            sale = null; uncertain = false; reason = "";
            var v = Vehicles.Find(x => x.Id == id);
            int index = v == null ? -1 : Array.IndexOf(v.Items, item);
            if (v == null || !v.Open || v.OpeningId != opening || v.Owner != owner || index < 0) { reason = "not this opening"; return 3; }
            if (v.Prices[index] != cents) { reason = "price changed"; return 3; }
            if (v.Units[index] == 0) { reason = "sold out"; return 3; }
            v.Units[index]--; Revision++;
            sale = new Sale(day, cents, v.Units[index] + 1, v.Units[index], ++Sequence);
            return 0;
        }
    }
}
