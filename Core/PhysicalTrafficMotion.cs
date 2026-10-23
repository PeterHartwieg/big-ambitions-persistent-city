using System;

namespace PersistentCity.Core
{
    public readonly struct PhysicalTrafficCommandLimit
    {
        public PhysicalTrafficCommandLimit(Position direction, double advance, bool holdPosition = false)
        { Direction = direction; MaximumAdvance = advance; HoldPosition = holdPosition; }
        public Position Direction { get; }
        public double MaximumAdvance { get; }
        public bool HoldPosition { get; }
    }
    public readonly struct PhysicalTrafficRotationLimit
    {
        internal PhysicalTrafficRotationLimit(double fraction, double reserved) { Fraction = fraction; ReservedAdvance = reserved; }
        public double Fraction { get; }
        public double ReservedAdvance { get; }
    }
    /// <summary>Additional command braking only. External collision impulses remain physical evidence to validate separately.</summary>
    public static class PhysicalTrafficMotion
    {
        public static double MaximumSpeed(PhysicalJourneySample sample, Position actual, double maximumNominalMinutes, double fixedSeconds)
        {
            if (!Position.Finite(fixedSeconds) || fixedSeconds <= 0) return 0;
            return MaximumAdvance(sample, actual, maximumNominalMinutes) / fixedSeconds;
        }
        public static double MaximumAdvance(PhysicalJourneySample sample, Position actual, double maximumNominalMinutes)
        {
            if (!actual.IsFinite || !sample.From.IsFinite || !sample.To.IsFinite || sample.Mode != TravelMode.Car
                || !Position.Finite(maximumNominalMinutes)
                || !Position.Finite(sample.LegMinutes) || sample.LegMinutes <= 0) return 0;
            double start = sample.ElapsedMinutes - sample.LegFraction * sample.LegMinutes;
            double fraction = Math.Max(0, Math.Min(1, (maximumNominalMinutes - start) / sample.LegMinutes));
            double x = sample.To.X - sample.From.X, z = sample.To.Z - sample.From.Z;
            double length = Math.Sqrt(x * x + z * z);
            if (!Position.Finite(length) || length <= .000001) return 0;
            double actualAlong = ((actual.X - sample.From.X) * x + (actual.Z - sample.From.Z) * z) / length;
            // Keep a small margin at an interior queue limit. Reached native endpoints get no permanent
            // setback: the head must be able to substantiate the boundary and release finite storage.
            double setback = fraction < 1 - 1e-10 ? .15 : 0;
            double remaining = Math.Max(0, length * fraction - actualAlong - setback);
            return remaining;
        }

        /// <summary>
        /// Bound the entire commanded rotation arc, not just its final orientation. Native yaw is around
        /// the body's current local up axis; projected marker movement is A(cos(theta)-1)+B sin(theta).
        /// A prefix maximum is monotone, so bounded bisection finds a safe prefix without translating
        /// a stopped root backwards to compensate for rotation.
        /// </summary>
        public static PhysicalTrafficRotationLimit LimitRotation(Position markerOffset, Position axis, Position routeDirection,
            double proposedRadians, double maximumAdvance)
        {
            if (!markerOffset.IsFinite || !axis.IsFinite || !routeDirection.IsFinite || !Position.Finite(proposedRadians)
                || Math.Abs(proposedRadians) > Math.PI + 1e-6 || !Position.Finite(maximumAdvance) || maximumAdvance < 0)
                throw new ArgumentException("Invalid physical traffic rotation.");
            double axisLength = Math.Sqrt(axis.X * axis.X + axis.Y * axis.Y + axis.Z * axis.Z);
            double directionLength = Math.Sqrt(routeDirection.X * routeDirection.X + routeDirection.Z * routeDirection.Z);
            if (!Position.Finite(axisLength) || axisLength <= .000001 || !Position.Finite(directionLength) || directionLength <= .000001)
                throw new ArgumentException("Invalid physical traffic rotation axis/direction.");
            double ax = axis.X / axisLength, ay = axis.Y / axisLength, az = axis.Z / axisLength;
            double dx = routeDirection.X / directionLength, dz = routeDirection.Z / directionLength;
            double parallel = markerOffset.X * ax + markerOffset.Y * ay + markerOffset.Z * az;
            double a = dx * (markerOffset.X - ax * parallel) + dz * (markerOffset.Z - az * parallel);
            double b = dx * (ay * markerOffset.Z - az * markerOffset.Y) + dz * (ax * markerOffset.Y - ay * markerOffset.X);
            if (!Position.Finite(a) || !Position.Finite(b) || !Position.Finite(a * a + b * b))
                throw new ArgumentException("Invalid physical traffic marker projection.");
            double full = MaximumRotationAdvance(a, b, proposedRadians);
            if (full == 0) return new PhysicalTrafficRotationLimit(1, 0);
            // Reserve float quaternion/application roundoff only when rotation can advance the marker.
            const double guard = .0001;
            double budget = Math.Max(0, maximumAdvance - guard);
            if (full <= budget) return new PhysicalTrafficRotationLimit(1, full + guard);
            if (budget == 0) return new PhysicalTrafficRotationLimit(0, 0);
            double low = 0, high = 1;
            for (int i = 0; i < 28; i++)
            {
                double middle = (low + high) / 2;
                if (MaximumRotationAdvance(a, b, proposedRadians * middle) <= budget) low = middle;
                else high = middle;
            }
            double reserved = MaximumRotationAdvance(a, b, proposedRadians * low);
            return new PhysicalTrafficRotationLimit(low, reserved > 0 ? Math.Min(maximumAdvance, reserved + guard) : 0);
        }
        private static double MaximumRotationAdvance(double a, double b, double angle)
        {
            double low = Math.Min(0, angle), high = Math.Max(0, angle);
            double result = Math.Max(0, a * (Math.Cos(angle) - 1) + b * Math.Sin(angle));
            double phase = Math.Atan2(b, a);
            for (int turn = -1; turn <= 1; turn++)
            {
                double peak = phase + turn * 2 * Math.PI;
                if (peak >= low && peak <= high) result = Math.Max(result, Math.Sqrt(a * a + b * b) - a);
            }
            return result;
        }

        /// <summary>Reduce the native requested horizontal speed, preserving its direction, vertical forces and any stronger native stop.</summary>
        public static Position LimitVelocityChange(Position velocity, Position nativeChange, double maximumSpeed)
        {
            if (!velocity.IsFinite || !nativeChange.IsFinite || !Position.Finite(maximumSpeed) || maximumSpeed < 0)
                throw new ArgumentException("Invalid physical traffic command.");
            double x = velocity.X + nativeChange.X, z = velocity.Z + nativeChange.Z;
            double speed = Math.Sqrt(x * x + z * z);
            if (speed <= maximumSpeed || speed <= .000000001) return nativeChange;
            double scale = maximumSpeed / speed;
            return new Position(x * scale - velocity.X, nativeChange.Y, z * scale - velocity.Z);
        }
    }
}
