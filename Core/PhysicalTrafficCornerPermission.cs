using System;

namespace PersistentCity.Core
{
    /// <summary>F2d: presentation only. Whether Gley may take a visible car past the corner it is driving to.</summary>
    public static class PhysicalTrafficCornerPermission
    {
        /// <summary>The native target predicate, including its one-nanominute arithmetic allowance: the cap (the
        /// kernel's coordinate) reaches the end of the car's own leg. It shapes the visible turn and never gates a
        /// logical crossing.</summary>
        public static bool CanReachEndpoint(PhysicalJourneySample sample, double maximumNominalMinutes)
        {
            return Valid(sample) && Position.Finite(maximumNominalMinutes) && maximumNominalMinutes >= sample.ElapsedMinutes
                && maximumNominalMinutes + 1e-9 >= sample.ElapsedMinutes + sample.RemainingLegMinutes;
        }

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
