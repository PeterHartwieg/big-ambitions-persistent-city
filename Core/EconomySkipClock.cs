using System;

namespace PersistentCity.Core
{
    /// <summary>During hidden time skips, finish the current native hour's events before
    /// letting native stock/accounting move into another hour or daily settlement.</summary>
    public static class EconomySkipClock
    {
        // Native minutes are floats. Stop just below the boundary, drain the event queue,
        // then cross it in a tiny step rather than rounding a large step into the next hour.
        public const double BoundaryMarginMinutes = .0001;
        public static double Limit(bool populationCaughtUp, double nativeMinute, double requestedMinutes)
        {
            if (!Position.Finite(nativeMinute) || !Position.Finite(requestedMinutes)
                || nativeMinute < 0 || requestedMinutes < 0)
                throw new ArgumentOutOfRangeException("Invalid economy clock observation.");
            if (!populationCaughtUp) return 0;
            double remaining = 60 - nativeMinute % 60;
            double step = remaining > 2 * BoundaryMarginMinutes ? remaining - BoundaryMarginMinutes
                : remaining + BoundaryMarginMinutes;
            return Math.Min(requestedMinutes, step);
        }
    }
}
