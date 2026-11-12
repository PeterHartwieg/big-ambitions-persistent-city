using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Runtime.Serialization;

namespace PersistentCity.Core
{
    [DataContract]
    public readonly struct Money : IEquatable<Money>, IComparable<Money>
    {
        public const int CentBits = 28;
        public const long UnitsPerCent = 1L << CentBits;
        public const long UnitsPerDollar = 100 * UnitsPerCent;
        public const long MaximumNativePriceCents = 1000000;
        private const string Unrepresentable = "Price cannot be represented without losing native precision.";

        [DataMember(Name = nameof(Units))] private readonly long units;
        private Money(long units) { this.units = units; }
        public long Units => units;
        public static Money Zero => default;
        public static Money FromUnits(long units) => new Money(units);
        public static Money FromCents(long cents) => new Money(checked(cents * UnitsPerCent));

        public static Money operator +(Money left, Money right) => new Money(checked(left.Units + right.Units));
        public static Money operator -(Money left, Money right) => new Money(checked(left.Units - right.Units));
        public static Money operator -(Money value) => new Money(checked(-value.Units));
        public static Money operator *(Money value, long factor) => new Money(checked(value.Units * factor));
        public static bool operator ==(Money left, Money right) => left.Units == right.Units;
        public static bool operator !=(Money left, Money right) => left.Units != right.Units;
        public static bool operator <(Money left, Money right) => left.Units < right.Units;
        public static bool operator >(Money left, Money right) => left.Units > right.Units;
        public static bool operator <=(Money left, Money right) => left.Units <= right.Units;
        public static bool operator >=(Money left, Money right) => left.Units >= right.Units;
        public static Money Min(Money left, Money right) => left <= right ? left : right;
        public static Money Max(Money left, Money right) => left >= right ? left : right;

        public static bool TryAdd(Money left, Money right, out Money result)
        {
            long sum = unchecked(left.Units + right.Units);
            bool fits = ((left.Units ^ sum) & (right.Units ^ sum)) >= 0;
            result = fits ? new Money(sum) : default;
            return fits;
        }
        public static Money Sum(IEnumerable<Money> values)
        {
            var total = Zero;
            foreach (var value in values) total += value;
            return total;
        }
        public static BigInteger TotalUnits(IEnumerable<Money> values)
        {
            var total = BigInteger.Zero;
            foreach (var value in values) total += value.Units;
            return total;
        }
        public static string TotalCentsText(IEnumerable<Money> values) => CentsText(TotalUnits(values));

        public bool IsWholeCents() => Units % UnitsPerCent == 0;
        public double ApproximateCents() => Units / (double)UnitsPerCent;

        public static Money FromNative(float dollars)
            => TryFromNative(dollars, out var money) ? money : throw new ArgumentOutOfRangeException(nameof(dollars), Unrepresentable);

        public static bool TryFromNative(float dollars, out Money money)
        {
            money = default;
            if (float.IsNaN(dollars) || float.IsInfinity(dollars) || dollars <= 0 || dollars > MaximumNativePriceCents / 100) return false;
            int bits = new Binary32 { Value = dollars }.Bits;
            int biased = (bits >> 23) & 255;
            if (biased == 0) return false;
            long significand = (bits & 0x7fffff) | 0x800000;
            int exponent = biased - 120;
            while (exponent < 0 && (significand & 1) == 0) { significand >>= 1; exponent++; }
            if (exponent < 0) return false;
            money = new Money(significand * 25 << exponent);
            return true;
        }

        public string ToCentsText() => CentsText(Units);
        public static string CentsText(BigInteger units)
        {
            var magnitude = BigInteger.Abs(units);
            var whole = magnitude >> CentBits;
            var fraction = magnitude - (whole << CentBits);
            string text = whole.ToString(CultureInfo.InvariantCulture);
            if (!fraction.IsZero)
                text += "." + (fraction * BigInteger.Pow(5, CentBits)).ToString(CultureInfo.InvariantCulture).PadLeft(CentBits, '0').TrimEnd('0');
            return units.Sign < 0 ? "-" + text : text;
        }
        public override string ToString() => ToCentsText();

        public static bool TryParseCents(string text, int maximumFractionDigits, out Money value)
        {
            value = default;
            if (string.IsNullOrEmpty(text) || maximumFractionDigits < 0 || maximumFractionDigits > CentBits) return false;
            int start = text[0] == '-' ? 1 : 0, point = text.IndexOf('.');
            int end = point < 0 ? text.Length : point;
            if (end == start || (end - start > 1 && text[start] == '0') || !Digits(text, start, end)) return false;
            int places = point < 0 ? 0 : text.Length - point - 1;
            if (point >= 0 && (places == 0 || !Digits(text, point + 1, text.Length))) return false;
            if (places > maximumFractionDigits) return false;
            var scaled = BigInteger.Parse(text.Substring(start, end - start) + (point < 0 ? "" : text.Substring(point + 1)), NumberStyles.None, CultureInfo.InvariantCulture);
            var divisor = BigInteger.Pow(5, places);
            if (!(scaled % divisor).IsZero) return false;
            var units = scaled / divisor << (CentBits - places);
            if (start == 1) units = -units;
            if (units < long.MinValue || units > long.MaxValue) return false;
            value = new Money((long)units);
            return true;
        }
        private static bool Digits(string text, int from, int to)
        {
            for (int i = from; i < to; i++) if (text[i] < '0' || text[i] > '9') return false;
            return true;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
        private struct Binary32
        {
            [System.Runtime.InteropServices.FieldOffset(0)] public float Value;
            [System.Runtime.InteropServices.FieldOffset(0)] public int Bits;
        }

        public bool Equals(Money other) => Units == other.Units;
        public override bool Equals(object? obj) => obj is Money other && Equals(other);
        public override int GetHashCode() => Units.GetHashCode();
        public int CompareTo(Money other) => Units.CompareTo(other.Units);
    }
}
