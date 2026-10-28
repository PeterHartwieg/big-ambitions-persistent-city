using System;

namespace PersistentCity.Core
{
    /// <summary>Common mechanics for an explicitly selected optional stop. No kind, frequency,
    /// night density, destination or duration is selected here. Runtime scheduling awaits policy approval.</summary>
    public static class OptionalStops
    {
        public static Dwell? Plan(string citizenId, DwellKind selectedKind, string spotId, string facingId,
            double startMinute, double requestedMinutes, double freeUntilMinute, bool raining)
        {
            var rule = DwellRules.For(selectedKind);
            if (!rule.Optional) throw new ArgumentException("This is not an optional stop.", nameof(selectedKind));
            if (string.IsNullOrEmpty(citizenId) || string.IsNullOrEmpty(spotId))
                throw new ArgumentException("A stop requires an existing citizen and spot identity.");
            if (!Position.Finite(startMinute) || startMinute < 0 || !Position.Finite(requestedMinutes)
                || requestedMinutes <= 0 || !Position.Finite(freeUntilMinute))
                throw new ArgumentOutOfRangeException(nameof(requestedMinutes));
            // Rain cancellation is the existing Optional rule. No trips or committed routes are changed.
            if (raining || freeUntilMinute <= startMinute) return null;
            double end = Math.Min(freeUntilMinute, startMinute + Math.Min(requestedMinutes, rule.MaximumMinutes));
            if (end <= startMinute) return null;
            return new Dwell(citizenId, selectedKind, spotId, startMinute, end, facingId);
        }

        /// <summary>A committed stop cannot restart or gain time on visibility changes or reload.
        /// Cancellation ends the stop; the caller must preserve its original continuation.</summary>
        public static bool ShouldEnd(Dwell committed, double minute, bool raining, bool spotAvailable)
        {
            if (!DwellRules.For(committed.Kind).Optional) throw new ArgumentException("This is not an optional stop.", nameof(committed));
            if (!Position.Finite(minute)) throw new ArgumentOutOfRangeException(nameof(minute));
            return minute >= committed.EndMinute || raining || !spotAvailable;
        }
    }
}
