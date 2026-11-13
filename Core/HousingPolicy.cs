using System;

namespace PersistentCity.Core
{
    public static class HousingPolicy
    {
        public const int MaximumResidents = 50000;
        public const string PolicyId = "rentable-interior-occupant-bands-v1";
        public const string UnrentablePolicyId = "unrentable-footprint-floors-v1";

        public static int Capacity(double rentableInteriorAreaSquareMetres)
        {
            if (!Position.Finite(rentableInteriorAreaSquareMetres) || rentableInteriorAreaSquareMetres < 0)
                throw new ArgumentOutOfRangeException(nameof(rentableInteriorAreaSquareMetres));
            if (rentableInteriorAreaSquareMetres < 20) return 0;
            if (rentableInteriorAreaSquareMetres < 40) return 1;
            if (rentableInteriorAreaSquareMetres < 75) return 2;
            if (rentableInteriorAreaSquareMetres < 150) return 4;
            if (rentableInteriorAreaSquareMetres < 300) return 5;
            return 7;
        }

        public static bool TryUnrentableCapacity(double footprintSquareMetres, double floors, out int capacity)
        {
            capacity = 0;
            if (!Position.Finite(footprintSquareMetres) || !Position.Finite(floors) || footprintSquareMetres <= 0 || floors < 1) return false;
            double people = Math.Floor(footprintSquareMetres * Math.Floor(floors) * CityBalance.UnrentableHomeUsableShare
                / CityBalance.UnrentableHomeSquareMetresPerPerson);
            if (!Position.Finite(people)) return false;
            capacity = (int)Math.Min(people, MaximumResidents);
            return true;
        }

        public static int CityTarget(long totalHomeCapacity)
        {
            if (totalHomeCapacity <= 0) throw new InvalidOperationException("The catalogue has no residential capacity.");
            long target = Math.Min(Fill(totalHomeCapacity), Math.Min(CityBalance.CityResidentTotal, MaximumResidents));
            if (target < 1) throw new InvalidOperationException("Residential capacity produces an empty population target.");
            return (int)target;
        }

        internal static long Fill(long totalHomeCapacity) => totalHomeCapacity / 10 * 9 + totalHomeCapacity % 10 * 9 / 10;

        public static int Target(long totalHomeCapacity)
        {
            if (totalHomeCapacity <= 0) throw new InvalidOperationException("The catalogue has no residential capacity.");
            long target = Fill(totalHomeCapacity);
            if (target < 1) throw new InvalidOperationException("Residential capacity produces an empty population target.");
            if (target > MaximumResidents)
                throw new InvalidOperationException("The housing population exceeds the reviewed maximum of " + MaximumResidents + " residents.");
            return (int)target;
        }
    }

    public sealed class UnrentableHomeTally
    {
        public int Housed { get; private set; }
        public int Unreadable { get; private set; }
        public int Empty { get; private set; }
        public long Beds { get; private set; }

        public int Add(double footprintSquareMetres, double floors)
        {
            if (!HousingPolicy.TryUnrentableCapacity(footprintSquareMetres, floors, out int capacity)) { Unreadable++; return 0; }
            if (capacity <= 0) { Empty++; return 0; }
            Housed++; Beds += capacity;
            return capacity;
        }

        public string Summary() => "unrentable homes housed=" + Housed + " beds=" + Beds + " unreadable=" + Unreadable + " empty=" + Empty;
    }
}
