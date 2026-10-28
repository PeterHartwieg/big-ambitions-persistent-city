using System;

namespace PersistentCity.Core
{
    /// <summary>Accounting unit: decimal cents. A native price is a binary32 dollar amount and
    /// becomes exactly that binary value in cents: no rounding, ledger or tolerance. Every
    /// supported price is m * 2^e dollars, so its cents are a finite decimal; a price whose exact
    /// cents do not fit System.Decimal (scale 28, 96-bit significand) is refused, never rounded.
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
            // Decode the IEEE fields, avoiding decimal's float cast (only seven significant digits).
            int bits = BitConverter.ToInt32(BitConverter.GetBytes(dollars), 0);
            int biased = (bits >> 23) & 255;
            if (biased == 0) throw new ArgumentOutOfRangeException(nameof(dollars));
            // dollars = m * 2^(biased - 150); cents = m * 25 * 2^(biased - 148).
            long significand = ((bits & 0x7fffff) | 0x800000) * 25L;
            int exponent = biased - 148;
            if (exponent >= 0) return significand * (decimal)(1L << exponent);
            int scale = -exponent;
            while (scale > 0 && (significand & 1) == 0) { significand >>= 1; scale--; }
            // n / 2^k == n * 5^k / 10^k: an integer significand with decimal scale k, exactly.
            if (scale > 28) throw new ArgumentOutOfRangeException(nameof(dollars), "Price cannot be represented without losing native precision.");
            decimal digits = significand;
            try { for (int i = 0; i < scale; i++) digits = checked(digits * 5m); }
            catch (OverflowException) { throw new ArgumentOutOfRangeException(nameof(dollars), "Price cannot be represented without losing native precision."); }
            int[] parts = decimal.GetBits(digits);
            decimal cents = new decimal(parts[0], parts[1], parts[2], false, (byte)scale);
            if ((float)(cents / 100m) != dollars)
                throw new ArgumentOutOfRangeException(nameof(dollars), "Price cannot be represented without losing native precision.");
            return cents;
        }
    }
}
