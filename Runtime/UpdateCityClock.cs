using System.Diagnostics;

namespace PersistentCity.Runtime
{
    internal static class UpdateCityClock
    {
        private static readonly double MillisecondsPerTick = 1000.0 / Stopwatch.Frequency;
        private static long started;
        private static double built;
        private static bool reserved;

        internal static void Begin() { started = Stopwatch.GetTimestamp(); built = 0; reserved = false; }
        internal static void Reset() { started = 0; built = 0; reserved = false; }
        internal static double Elapsed() => started == 0 ? 0 : (Stopwatch.GetTimestamp() - started) * MillisecondsPerTick;
        internal static void Reserve() => reserved = true;
        internal static bool Reserved => reserved;
        internal static double OptionalElapsed() => reserved ? double.PositiveInfinity : Elapsed();
        internal static void AddBuild(double milliseconds) => built += milliseconds;
        internal static double BuildMilliseconds => built;
    }
}
