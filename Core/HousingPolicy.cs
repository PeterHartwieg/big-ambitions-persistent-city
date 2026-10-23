using System;

namespace PersistentCity.Core
{
    public static class HousingPolicy
    {
        public const int MaximumResidents = 50000;
        public const string PolicyId = "rentable-interior-occupant-bands-v1";

        // Provisional occupant limits for the residential interior the player can
        // rent or buy. Large homes allow a small family plus live-in staff; this
        // allocation does not model household relationships or whole-property area.
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

        // Apply occupancy once to the city total so small homes retain their beds.
        public static int Target(long totalHomeCapacity)
        {
            if (totalHomeCapacity <= 0) throw new InvalidOperationException("The catalogue has no residential capacity.");
            long target = totalHomeCapacity / 10 * 9 + totalHomeCapacity % 10 * 9 / 10;
            if (target < 1) throw new InvalidOperationException("Residential capacity produces an empty population target.");
            if (target > MaximumResidents)
                throw new InvalidOperationException("The housing population exceeds the reviewed maximum of " + MaximumResidents + " residents.");
            return (int)target;
        }
    }
}
