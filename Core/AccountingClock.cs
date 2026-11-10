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
        // Use the hour's float spacing everywhere: adding across 60 and then subtracting
        // 60 must preserve this fence too (the float spacing near minute one is smaller).
        private const float FenceGap = 1f / 262144;
        private static float Fence(double nativeMinute) => (float)(Math.Floor(nativeMinute % 60) + 1) - FenceGap;
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
        public static double PreviousEpochTarget(double nativeMinute)
        { Validate(nativeMinute); return Previous(Math.Floor(nativeMinute)); }
        /// <summary>Load-time start decision. Native time runs unheld from the native load until Persistent City's
        /// clock hold starts, so a save taken late in a minute can load one minute later. Only that single crossing,
        /// inside one native hour (no hourly or daily accounting ran), is caught up; every other gap stays lost.</summary>
        public static LoadClockStart ClassifyLoad(double logicalMinute, double nativeMinute)
        {
            if (Compatible(logicalMinute, nativeMinute)) return LoadClockStart.Current;
            double epoch = Math.Floor(nativeMinute);
            return epoch % 60 != 0 && Compatible(logicalMinute, Previous(epoch)) ? LoadClockStart.CatchUp : LoadClockStart.Lost;
        }
        /// <summary>While a load catches up: native is held inside <paramref name="catchUpEpoch"/> and the logical
        /// clock is still in the minute before it. Native moving on to another epoch is lost as before.</summary>
        public static bool CompatibleDuringLoadCatchUp(double logicalMinute, double nativeMinute, double catchUpEpoch)
            => Compatible(logicalMinute, nativeMinute)
            || Math.Floor(nativeMinute) == catchUpEpoch && catchUpEpoch % 60 != 0 && Compatible(logicalMinute, Previous(catchUpEpoch));
        public static float Limit(bool caughtUp, double nativeMinute, float requestedMinutes)
        {
            Validate(nativeMinute);
            if (!Position.Finite(requestedMinutes) || requestedMinutes < 0) throw new ArgumentOutOfRangeException(nameof(requestedMinutes));
            if (!caughtUp || requestedMinutes == 0) return 0;
            float minute = (float)(nativeMinute % 60), fence = Fence(nativeMinute);
            // Once the ending epoch drained, use this tick's remaining allowance in the
            // next epoch instead of spending a rendered frame on a one-ulp crossing.
            float end = minute >= fence ? fence + 1 : fence;
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

    public enum LoadClockStart { Current, CatchUp, Lost }

    /// <summary>Elapsed-time allowance with the legacy 16-start/2ms per-frame floor. No debt
    /// accumulates after stalls; soft time limits still cannot preempt an individual native call.</summary>
    public readonly struct PopulationFrameBudget
    {
        public PopulationFrameBudget(double seconds, bool hiddenSkip)
        {
            if (!Position.Finite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            // Unity supplies float seconds; do not turn 16.0000008 into seventeen starts.
            // The epsilon is below a work unit and covers float conversion at these caps.
            Units = hiddenSkip ? 128 : Math.Max(128, Math.Min(1024, (int)Math.Ceiling(Math.Min(seconds, 1) * 7680 - .0001)));
            CitizenEvents = hiddenSkip ? 128 : Math.Max(16, Math.Min(128, (int)Math.Ceiling(Math.Min(seconds, 1) * 960 - .0001)));
            Milliseconds = hiddenSkip ? 16 : Math.Max(2, Math.Min(4, seconds * 120));
        }
        public int Units { get; }
        public int CitizenEvents { get; }
        public double Milliseconds { get; }
    }
}
