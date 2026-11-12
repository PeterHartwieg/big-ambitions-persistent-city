using System;

namespace PersistentCity.Core
{
    public static class HousingPolicy
    {
        public const int MaximumResidents = 50000;
        public const string PolicyId = "rentable-interior-occupant-bands-v1";

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
}
