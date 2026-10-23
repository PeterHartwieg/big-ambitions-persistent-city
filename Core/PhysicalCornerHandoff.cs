using System;

namespace PersistentCity.Core
{
    /// <summary>
    /// A native corner receipt consumes one measured marker. Its existing projection onto the
    /// following leg is not a second movement. Hold that pose until traffic admits it, without
    /// accepting any additional forward motion beyond the original receipt.
    /// </summary>
    public sealed class PhysicalCornerHandoff
    {
        private int leg = -1;
        private Position from, to, marker;
        private double along, nominal;

        public void Begin(PhysicalJourneySample previous, PhysicalJourneySample next, Position reachedMarker)
        {
            leg = -1;
            if (previous.Mode != TravelMode.Car || next.Mode != TravelMode.Car
                || next.LegIndex != previous.LegIndex + 1 || !reachedMarker.IsFinite
                || Position.Distance(previous.To, next.From) > .000001
                || Position.Distance(reachedMarker, next.From) > 1.5
                || !TryAlong(next, reachedMarker, out double measured, out double length)) return;
            double start = next.ElapsedMinutes - next.LegFraction * next.LegMinutes;
            double pending = start + Math.Max(0, Math.Min(1, measured / length)) * next.LegMinutes;
            if (pending <= next.ElapsedMinutes) return;
            leg = next.LegIndex; from = next.From; to = next.To; marker = reachedMarker;
            along = measured; nominal = pending;
        }

        public bool MustHold(PhysicalJourneySample sample, double maximum)
        {
            if (leg != sample.LegIndex || !from.Equals(sample.From) || !to.Equals(sample.To)
                || !Position.Finite(maximum) || maximum >= nominal)
            { leg = -1; return false; }
            return leg >= 0;
        }

        public bool TryHold(PhysicalJourneySample sample, Position actual, double maximum, out double accepted)
            => TryHold(sample, actual, maximum, sample.ElapsedMinutes, out accepted);

        public bool TryHold(PhysicalJourneySample sample, Position actual, double maximum, double observedMinutes, out double accepted)
        {
            accepted = observedMinutes;
            if (!MustHold(sample, maximum) || !Position.Finite(observedMinutes) || observedMinutes < 0
                || maximum < observedMinutes || (maximum < sample.ElapsedMinutes
                    && !PhysicalTrafficCornerPermission.IsNormalizedBoundary(sample, observedMinutes))
                || !TryAlong(sample, actual, out double measured, out _)
                || !PhysicalTrafficPrecision.TryGetHorizontalAllowance(marker, actual, out double tolerance)
                || measured > along + tolerance) return false;
            // PhysicalProgress already checks the current leg's height/cross-track geometry.
            // Credit at most the traffic kernel's current permission, never the unadmitted pose.
            accepted = maximum;
            return true;
        }

        private static bool TryAlong(PhysicalJourneySample sample, Position actual, out double measured, out double length)
        {
            measured = length = 0;
            if (!sample.From.IsFinite || !sample.To.IsFinite || !actual.IsFinite
                || !Position.Finite(sample.LegMinutes) || sample.LegMinutes <= 0) return false;
            double x = sample.To.X - sample.From.X, z = sample.To.Z - sample.From.Z;
            length = Math.Sqrt(x * x + z * z);
            if (!Position.Finite(length) || length <= .000001) return false;
            measured = ((actual.X - sample.From.X) * x + (actual.Z - sample.From.Z) * z) / length;
            return Position.Finite(measured);
        }
    }
}
