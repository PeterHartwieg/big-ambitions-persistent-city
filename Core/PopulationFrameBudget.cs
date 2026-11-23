using System;

namespace PersistentCity.Core
{
    public readonly struct PopulationFrameBudget
    {
        public PopulationFrameBudget(double seconds, bool hiddenSkip)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            Units = hiddenSkip ? 128 : Math.Max(128, Math.Min(MaximumUnits, (int)Math.Ceiling(Math.Min(seconds, 1) * 7680 - .0001)));
            CitizenEvents = hiddenSkip ? 128 : Math.Max(16, Math.Min(128, (int)Math.Ceiling(Math.Min(seconds, 1) * 960 - .0001)));
            Milliseconds = hiddenSkip ? 16 : Math.Max(2, Math.Min(4, seconds * 120));
        }
        public const int MaximumUnits = 1024;
        public int Units { get; }
        public int CitizenEvents { get; }
        public double Milliseconds { get; }
    }
}
