using System;

namespace PersistentCity.Core
{
    /// <summary>Accounting unit: decimal cents, with twelve fractional cent digits.
    /// Native prices are binary32 dollars. Conversion rounds to nearest, ties to even,
    /// and refuses any value that cannot round-trip to the identical native price.
    /// Whole-cent policy amounts remain whole cents. Never round a payment to display precision.</summary>
    public static class FoodMoney
    {
        public static decimal Add(decimal left, decimal right)
        {
            decimal result = checked(left + right);
            if (result - left != right || result - right != left)
                throw new OverflowException("Money addition would lose fractional precision.");
            return result;
        }

        public static decimal FromNative(float dollars)
        {
            if (float.IsNaN(dollars) || float.IsInfinity(dollars) || dollars <= 0 || dollars > 10000)
                throw new ArgumentOutOfRangeException(nameof(dollars));
            // Decode the significand, avoiding decimal's float cast (only seven significant digits).
            int bits = BitConverter.ToInt32(BitConverter.GetBytes(dollars), 0);
            int exponent = ((bits >> 23) & 255) - 150;
            decimal cents = ((bits & 0x7fffff) | 0x800000) * 100m;
            if (((bits >> 23) & 255) == 0) throw new ArgumentOutOfRangeException(nameof(dollars));
            while (exponent < 0) { cents /= 2m; exponent++; }
            while (exponent > 0) { cents *= 2m; exponent--; }
            cents = decimal.Round(cents, 12, MidpointRounding.ToEven);
            if (cents <= 0 || (float)(cents / 100m) != dollars)
                throw new ArgumentOutOfRangeException(nameof(dollars), "Price cannot be represented without losing native precision.");
            return cents;
        }
    }
}
