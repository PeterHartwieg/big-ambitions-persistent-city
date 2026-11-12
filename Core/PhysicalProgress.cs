using System;

namespace PersistentCity.Core
{
    public static class PhysicalProgress
    {
        public static bool TryProject(PhysicalJourneySample sample, Position actual, bool trustedNativeCornerReached, out double routeElapsedMinutes)
        {
            routeElapsedMinutes = sample.ElapsedMinutes;
            if (!Valid(sample) || !actual.IsFinite || sample.Stationary
                || (sample.Mode != TravelMode.Walk && sample.Mode != TravelMode.Car)) return false;
            double dx = sample.To.X - sample.From.X, dz = sample.To.Z - sample.From.Z;
            double square = dx * dx + dz * dz;
            if (!Position.Finite(square)) return false;
            if (square <= 0.000000000001)
                return TryProjectVerticalWalk(sample, actual, out routeElapsedMinutes);
            double along = ((actual.X - sample.From.X) * dx + (actual.Z - sample.From.Z) * dz) / square;
            if (!Position.Finite(along)) return false;
            double clamped = Math.Max(0, Math.Min(1, along));
            double expectedY = sample.From.Y + (sample.To.Y - sample.From.Y) * clamped;
            if (Math.Abs(actual.Y - expectedY) > 2) return false;
            double endTolerance = sample.Mode == TravelMode.Car ? 1.5 : 0.08;
            bool reached = trustedNativeCornerReached || Position.Distance(actual, sample.To) <= endTolerance;
            if (!reached)
            {
                double lineX = sample.From.X + dx * along, lineZ = sample.From.Z + dz * along;
                double crossX = actual.X - lineX, crossZ = actual.Z - lineZ;
                double cross = Math.Sqrt(crossX * crossX + crossZ * crossZ);
                double tolerance = sample.Mode == TravelMode.Car ? 2 : 0.35;
                if (!Position.Finite(cross) || cross > tolerance) return false;
                reached = along >= 1;
            }
            double fraction = reached ? 1 : Math.Max(sample.LegFraction, clamped);
            routeElapsedMinutes = Math.Min(sample.TotalMinutes,
                sample.ElapsedMinutes + (fraction - sample.LegFraction) * sample.LegMinutes);
            return true;
        }

        private static bool TryProjectVerticalWalk(PhysicalJourneySample sample, Position actual, out double elapsed)
        {
            elapsed = sample.ElapsedMinutes;
            if (sample.Mode != TravelMode.Walk) return false;
            double dx = sample.To.X - sample.From.X, dy = sample.To.Y - sample.From.Y, dz = sample.To.Z - sample.From.Z;
            double square = dx * dx + dy * dy + dz * dz;
            if (!Position.Finite(square) || square <= 0.000000000001) return false;
            double along = ((actual.X - sample.From.X) * dx + (actual.Y - sample.From.Y) * dy + (actual.Z - sample.From.Z) * dz) / square;
            if (!Position.Finite(along)) return false;
            double fraction = Math.Max(0, Math.Min(1, along));
            Position closest = Position.Lerp(sample.From, sample.To, fraction);
            if (Position.Distance(actual, closest) > 0.08) return false;
            double endpointTolerance = Math.Min(0.08, Math.Sqrt(square) * 0.01);
            if (Position.Distance(actual, sample.To) <= endpointTolerance) fraction = 1;
            fraction = Math.Max(sample.LegFraction, fraction);
            elapsed = Math.Min(sample.TotalMinutes, sample.ElapsedMinutes + (fraction - sample.LegFraction) * sample.LegMinutes);
            return true;
        }

        public static double AdvanceWait(PhysicalJourneySample sample, double elapsedSimulationMinutes)
        {
            if (!Position.Finite(elapsedSimulationMinutes) || elapsedSimulationMinutes < 0)
                throw new ArgumentOutOfRangeException(nameof(elapsedSimulationMinutes));
            if (!Valid(sample) || !sample.Stationary) throw new ArgumentException("A stationary committed leg is required.", nameof(sample));
            return Math.Min(sample.TotalMinutes, sample.ElapsedMinutes + Math.Min(sample.RemainingLegMinutes, elapsedSimulationMinutes));
        }

        private static bool Valid(PhysicalJourneySample sample) => sample.From.IsFinite && sample.To.IsFinite
            && Position.Finite(sample.ElapsedMinutes) && sample.ElapsedMinutes >= 0
            && Position.Finite(sample.TotalMinutes) && sample.TotalMinutes >= sample.ElapsedMinutes
            && Position.Finite(sample.LegMinutes) && sample.LegMinutes > 0
            && Position.Finite(sample.LegFraction) && sample.LegFraction >= 0 && sample.LegFraction <= 1
            && sample.ElapsedMinutes - sample.LegFraction * sample.LegMinutes >= -0.0000001
            && sample.ElapsedMinutes + sample.RemainingLegMinutes <= sample.TotalMinutes + 0.0000001;
    }
}
