using System;

namespace PersistentCity.Core
{
    /// <summary>Numerical evidence only; success never grants progress beyond an existing permission.</summary>
    public static class PhysicalTrafficPrecision
    {
        private const double MaximumSpatialAllowance = .001;

        /// <summary>
        /// Four local float32 spacings cover rounding a placed root and assembling its world marker.
        /// This is a bounded engineering allowance, not a general physics/movement tolerance. Reject
        /// coordinates whose precision would require more than one millimetre. Height is not included.
        /// </summary>
        public static bool TryGetHorizontalAllowance(Position reference, Position actual, out double metres)
        {
            metres = 0;
            if (!reference.IsFinite || !actual.IsFinite) return false;
            double x = 4 * Math.Max(FloatSpacing(reference.X), FloatSpacing(actual.X));
            double z = 4 * Math.Max(FloatSpacing(reference.Z), FloatSpacing(actual.Z));
            double allowance = Math.Sqrt(x * x + z * z);
            if (!Position.Finite(allowance) || allowance <= 0 || allowance > MaximumSpatialAllowance) return false;
            metres = allowance;
            return true;
        }

        /// <summary>
        /// Accept only positive excess explained by a float-position-sized displacement from the
        /// current leg's permission point and independently supported by its raw along-leg projection.
        /// Endpoint/corner credit, a nearby parallel body, or a large minute tolerance cannot supply
        /// this evidence. On success accepted equals maximum exactly; Core accounting stays strict.
        /// </summary>
        public static bool TryClampNumericalExcess(PhysicalJourneySample sample, Position actual,
            double projectedNominalMinutes, double maximumNominalMinutes, out double acceptedNominalMinutes)
        {
            acceptedNominalMinutes = 0;
            if (sample.Mode != TravelMode.Car || sample.Stationary || !sample.From.IsFinite || !sample.To.IsFinite || !actual.IsFinite
                || !Position.Finite(sample.ElapsedMinutes) || sample.ElapsedMinutes < 0
                || !Position.Finite(sample.TotalMinutes) || sample.TotalMinutes < sample.ElapsedMinutes
                || !Position.Finite(sample.LegMinutes) || sample.LegMinutes <= 0
                || !Position.Finite(sample.LegFraction) || sample.LegFraction < 0 || sample.LegFraction > 1
                || !Position.Finite(projectedNominalMinutes) || !Position.Finite(maximumNominalMinutes)
                || maximumNominalMinutes < sample.ElapsedMinutes || projectedNominalMinutes <= maximumNominalMinutes
                || projectedNominalMinutes > sample.TotalMinutes) return false;
            double permissionFraction = sample.LegFraction + (maximumNominalMinutes - sample.ElapsedMinutes) / sample.LegMinutes;
            double projectedFraction = sample.LegFraction + (projectedNominalMinutes - sample.ElapsedMinutes) / sample.LegMinutes;
            if (!Position.Finite(permissionFraction) || !Position.Finite(projectedFraction)
                || permissionFraction < 0 || permissionFraction > 1 || projectedFraction > 1) return false;
            double dx = sample.To.X - sample.From.X, dz = sample.To.Z - sample.From.Z;
            double square = dx * dx + dz * dz, length = Math.Sqrt(square);
            if (!Position.Finite(length) || length <= .000001) return false;
            Position permitted = Position.Lerp(sample.From, sample.To, permissionFraction);
            if (!TryGetHorizontalAllowance(permitted, actual, out double allowance) || Math.Abs(actual.Y - permitted.Y) > 2) return false;
            double ox = actual.X - permitted.X, oz = actual.Z - permitted.Z;
            if (Math.Sqrt(ox * ox + oz * oz) > allowance) return false;
            double rawFraction = ((actual.X - sample.From.X) * dx + (actual.Z - sample.From.Z) * dz) / square;
            rawFraction = Math.Max(sample.LegFraction, Math.Max(0, Math.Min(1, rawFraction)));
            // Nominal-coordinate subtraction has its own double rounding. If it cannot resolve
            // this spatial allowance, refuse rather than broadening it for a short/ill-scaled leg.
            double roundoff = 8 * (DoubleSpacing(sample.ElapsedMinutes) + DoubleSpacing(projectedNominalMinutes)
                + DoubleSpacing(maximumNominalMinutes) + DoubleSpacing(sample.LegMinutes)) * length / sample.LegMinutes;
            double excess = (projectedNominalMinutes - maximumNominalMinutes) / sample.LegMinutes * length;
            if (!Position.Finite(rawFraction) || !Position.Finite(roundoff) || roundoff > allowance
                || !Position.Finite(excess) || excess > MaximumSpatialAllowance || excess > allowance + roundoff
                || Math.Abs(projectedFraction - rawFraction) * length > roundoff) return false;
            acceptedNominalMinutes = maximumNominalMinutes;
            return true;
        }

        private static double FloatSpacing(double value)
        {
            float coordinate = (float)Math.Abs(value);
            if (float.IsInfinity(coordinate)) return double.PositiveInfinity;
            int exponent = (BitConverter.SingleToInt32Bits(coordinate) >> 23) & 255;
            return Math.Pow(2, exponent == 0 ? -149 : exponent - 150);
        }
        private static double DoubleSpacing(double value)
        {
            long exponent = (BitConverter.DoubleToInt64Bits(Math.Abs(value)) >> 52) & 2047;
            return Math.Pow(2, exponent == 0 ? -1074 : exponent - 1075);
        }
    }
}
