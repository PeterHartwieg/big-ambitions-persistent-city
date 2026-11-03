using System;
using System.Runtime.InteropServices;

namespace PersistentCity.Core
{
    /// <summary>One native minute is an input epoch. Drain every event strictly before its end
    /// before allowing native time (and hourly/daily accounting) to enter the next epoch.
    /// Checkout stays in the current native hour; no historical inventory is replayed.</summary>
    public static class AccountingClock
    {
        public const double MaximumLagMinutes = 1;
        [StructLayout(LayoutKind.Explicit)]
        private struct FloatBits
        {
            [FieldOffset(0)] public float Value;
            [FieldOffset(0)] public int Bits;
        }
        private static float Previous(float value)
        { var bits = new FloatBits { Value = value }; bits.Bits--; return bits.Value; }
        private static double Previous(double value) => value == 0 ? 0
            : BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(value) - 1);

        // Use the float minute-of-hour that native time actually stores. A decimal epsilon
        // is unsafe: the last substep can round into an hour before its final events drain.
        private static float Fence(double nativeMinute) => Previous((float)(Math.Floor(nativeMinute % 60) + 1));
        public static double Target(double nativeMinute)
        {
            Validate(nativeMinute);
            return (float)(nativeMinute % 60) >= Fence(nativeMinute)
                ? Previous(Math.Floor(nativeMinute) + 1) : nativeMinute;
        }
        public static bool Compatible(double logicalMinute, double nativeMinute)
        {
            Validate(nativeMinute);
            // Immediately after crossing, the last published logical minute is the double
            // predecessor of this boundary. All earlier work was drained before crossing.
            return Position.Finite(logicalMinute) && logicalMinute >= Previous(Math.Floor(nativeMinute))
                && logicalMinute <= Target(nativeMinute);
        }
        public static float Limit(bool caughtUp, double nativeMinute, float requestedMinutes)
        {
            Validate(nativeMinute);
            if (!Position.Finite(requestedMinutes) || requestedMinutes < 0) throw new ArgumentOutOfRangeException(nameof(requestedMinutes));
            if (!caughtUp || requestedMinutes == 0) return 0;
            float minute = (float)(nativeMinute % 60), fence = Fence(nativeMinute);
            float end = minute >= fence ? (float)(Math.Floor(minute) + 1) : fence;
            float allowed = Math.Min(requestedMinutes, end - minute);
            // Subtraction and subsequent native float addition can round in opposite directions.
            if (minute + allowed > end) allowed = Previous(allowed);
            return allowed;
        }
        private static void Validate(double minute)
        {
            if (!Position.Finite(minute) || minute < 0 || minute > 1e9) throw new ArgumentOutOfRangeException(nameof(minute));
        }
    }

    /// <summary>Equal ordinary work allowance per wall second at 30/60/120 FPS. No debt
    /// accumulates after stalls; soft time limits still cannot preempt an individual native call.</summary>
    public readonly struct PopulationFrameBudget
    {
        public PopulationFrameBudget(double seconds, bool hiddenSkip)
        {
            if (!Position.Finite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            // Unity supplies float seconds; do not turn 16.0000008 into seventeen starts.
            // The epsilon is below a work unit and covers float conversion at these caps.
            Units = hiddenSkip ? 128 : Math.Max(1, Math.Min(1024, (int)Math.Ceiling(Math.Min(seconds, 1) * 7680 - .0001)));
            CitizenEvents = hiddenSkip ? 128 : Math.Max(1, Math.Min(128, (int)Math.Ceiling(Math.Min(seconds, 1) * 960 - .0001)));
            Milliseconds = hiddenSkip ? 16 : Math.Max(.25, Math.Min(4, seconds * 120));
        }
        public int Units { get; }
        public int CitizenEvents { get; }
        public double Milliseconds { get; }
    }
}
