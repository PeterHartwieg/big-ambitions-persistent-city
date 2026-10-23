using System;

namespace PersistentCity.Core
{
    /// <summary>Keep native corner admission and observed endpoint arithmetic on one contract.</summary>
    public static class PhysicalTrafficCornerPermission
    {
        /// <summary>The existing native target predicate, including its one-nanominute arithmetic allowance.</summary>
        public static bool CanReachEndpoint(PhysicalJourneySample sample, double maximumNominalMinutes)
        {
            return Valid(sample) && Position.Finite(maximumNominalMinutes) && maximumNominalMinutes >= sample.ElapsedMinutes
                && maximumNominalMinutes + 1e-9 >= sample.ElapsedMinutes + sample.RemainingLegMinutes;
        }

        /// <summary>
        /// A trusted native corner can credit the endpoint a few ulps above the permission that
        /// admitted it. Accept only that exact endpoint result and return the existing permission
        /// unchanged. This never tolerates ordinary moving projection or supplies new queue credit.
        /// </summary>
        public static bool TryClampEndpointExcess(PhysicalJourneySample sample, bool nativeCorner,
            double projectedNominalMinutes, double maximumNominalMinutes, out double acceptedNominalMinutes)
        {
            acceptedNominalMinutes = 0;
            return projectedNominalMinutes > maximumNominalMinutes
                && TryReconcileEndpoint(sample, nativeCorner, projectedNominalMinutes, maximumNominalMinutes, out acceptedNominalMinutes);
        }

        /// <summary>
        /// The trusted endpoint and its admitted permission can differ in either arithmetic direction.
        /// Reconcile only that exact endpoint within the existing native predicate's bounded gap.
        /// Never consume permission for another corner or infer movement from ordinary projection.
        /// </summary>
        public static bool TryReconcileEndpoint(PhysicalJourneySample sample, bool nativeCorner,
            double projectedNominalMinutes, double maximumNominalMinutes, out double acceptedNominalMinutes)
        {
            acceptedNominalMinutes = 0;
            if (!nativeCorner || !Position.Finite(projectedNominalMinutes)
                || !CanReachEndpoint(sample, maximumNominalMinutes)
                || maximumNominalMinutes > sample.TotalMinutes || Math.Abs(projectedNominalMinutes - maximumNominalMinutes) > 1e-9
                || projectedNominalMinutes != Math.Min(sample.TotalMinutes, sample.ElapsedMinutes + sample.RemainingLegMinutes)) return false;
            acceptedNominalMinutes = maximumNominalMinutes;
            return true;
        }

        /// <summary>
        /// SampleJourney can expose the next leg at fraction zero while the lease retains the raw
        /// observed coordinate a few ulps below that corner. Only that documented normalization
        /// envelope can explain a difference between geometry and the actual accounting coordinate.
        /// </summary>
        public static bool IsNormalizedBoundary(PhysicalJourneySample sample, double observedMinutes)
        {
            return Valid(sample) && sample.LegFraction == 0 && Position.Finite(observedMinutes) && observedMinutes >= 0
                && sample.ElapsedMinutes >= observedMinutes
                // SampleJourney adds a leg duration and subtracts the remaining duration when
                // normalizing. Their double rounding can put the resulting delta just above
                // the comparison envelope used before those operations.
                && sample.ElapsedMinutes - observedMinutes <= BoundaryAllowance(sample, observedMinutes);
        }

        /// <summary>Clamp normalization of already observed progress only, never additional projected movement.</summary>
        public static bool TryClampNormalizedBoundary(PhysicalJourneySample sample, double observedMinutes,
            double projectedNominalMinutes, double maximumNominalMinutes, out double acceptedNominalMinutes)
        {
            acceptedNominalMinutes = 0;
            return projectedNominalMinutes > maximumNominalMinutes
                && TryReconcileNormalizedBoundary(sample, observedMinutes, projectedNominalMinutes, maximumNominalMinutes, out acceptedNominalMinutes);
        }

        /// <summary>
        /// Recover an already observed fraction-zero boundary in either rounding direction. Both the
        /// exposed geometry and accepted permission must stay within one normalization envelope of
        /// the raw lease coordinate; repeated calls cannot accumulate a new movement allowance.
        /// </summary>
        public static bool TryReconcileNormalizedBoundary(PhysicalJourneySample sample, double observedMinutes,
            double projectedNominalMinutes, double maximumNominalMinutes, out double acceptedNominalMinutes)
        {
            acceptedNominalMinutes = 0;
            if (!IsNormalizedBoundary(sample, observedMinutes) || !Position.Finite(maximumNominalMinutes)
                || maximumNominalMinutes < observedMinutes || !Position.Finite(projectedNominalMinutes)
                || maximumNominalMinutes > sample.TotalMinutes || projectedNominalMinutes != sample.ElapsedMinutes
                || maximumNominalMinutes - observedMinutes > BoundaryAllowance(sample, observedMinutes)
                || Math.Abs(maximumNominalMinutes - projectedNominalMinutes) > BoundaryAllowance(sample, observedMinutes)) return false;
            acceptedNominalMinutes = maximumNominalMinutes;
            return true;
        }

        private static double BoundaryAllowance(PhysicalJourneySample sample, double observedMinutes) =>
            Math.Max(1e-12, Math.Abs(observedMinutes) * 1e-14)
                + CitySimulator.JourneyClockTolerance(observedMinutes, sample.ElapsedMinutes);

        private static bool Valid(PhysicalJourneySample sample)
        {
            if (sample.Mode != TravelMode.Car || sample.Stationary || !sample.From.IsFinite || !sample.To.IsFinite
                || !Position.Finite(sample.ElapsedMinutes) || sample.ElapsedMinutes < 0
                || !Position.Finite(sample.TotalMinutes) || sample.TotalMinutes < sample.ElapsedMinutes
                || !Position.Finite(sample.LegMinutes) || sample.LegMinutes <= 0
                || !Position.Finite(sample.LegFraction) || sample.LegFraction < 0 || sample.LegFraction > 1) return false;
            double end = sample.ElapsedMinutes + sample.RemainingLegMinutes;
            return Position.Finite(end) && end <= sample.TotalMinutes + 1e-9;
        }
    }
}
