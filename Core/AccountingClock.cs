using System;
using System.Runtime.InteropServices;

namespace PersistentCity.Core
{
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
            return Position.Finite(logicalMinute) && logicalMinute >= Previous(Math.Floor(nativeMinute))
                && logicalMinute <= Target(nativeMinute);
        }
        public static double PreviousEpochTarget(double nativeMinute)
        { Validate(nativeMinute); return Previous(Math.Floor(nativeMinute)); }
        public static float Limit(bool caughtUp, double nativeMinute, float requestedMinutes)
        {
            Validate(nativeMinute);
            if (!Position.Finite(requestedMinutes) || requestedMinutes < 0) throw new ArgumentOutOfRangeException(nameof(requestedMinutes));
            if (!caughtUp || requestedMinutes == 0) return 0;
            float minute = (float)(nativeMinute % 60), fence = Fence(nativeMinute);
            float end = minute >= fence ? fence + 1 : fence;
            float allowed = Math.Min(requestedMinutes, end - minute);
            if (minute + allowed > end) allowed = Previous(allowed);
            return allowed;
        }
        private static void Validate(double minute)
        {
            if (!Position.Finite(minute) || minute < 0 || minute > 1e9) throw new ArgumentOutOfRangeException(nameof(minute));
        }
    }

    public readonly struct PopulationFrameBudget
    {
        public PopulationFrameBudget(double seconds, bool hiddenSkip)
        {
            if (!Position.Finite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            Units = hiddenSkip ? 128 : Math.Max(128, Math.Min(1024, (int)Math.Ceiling(Math.Min(seconds, 1) * 7680 - .0001)));
            CitizenEvents = hiddenSkip ? 128 : Math.Max(16, Math.Min(128, (int)Math.Ceiling(Math.Min(seconds, 1) * 960 - .0001)));
            Milliseconds = hiddenSkip ? 16 : Math.Max(2, Math.Min(4, seconds * 120));
        }
        public int Units { get; }
        public int CitizenEvents { get; }
        public double Milliseconds { get; }
    }
}
