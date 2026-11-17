using System.Diagnostics;

namespace PersistentCity.Runtime
{
    internal static class UpdateCityClock
    {
        private static readonly double MillisecondsPerTick = 1000.0 / Stopwatch.Frequency;
        private static long started;
        private static double built;

        internal static void Begin() { started = Stopwatch.GetTimestamp(); built = 0; }
        internal static void Reset() { started = 0; built = 0; }
        internal static double Elapsed() => started == 0 ? 0 : (Stopwatch.GetTimestamp() - started) * MillisecondsPerTick;
        internal static void AddBuild(double milliseconds) => built += milliseconds;
        internal static double BuildMilliseconds => built;
    }
}
