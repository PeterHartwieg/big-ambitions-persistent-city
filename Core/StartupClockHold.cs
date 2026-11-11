using System;

namespace PersistentCity.Core
{
    /// <summary>Native time is held from before a loaded save's first native tick (the guard is armed while no city is
    /// attached, including at the menu) until activation, not only once RLO reports Ready. A save taken late in a
    /// minute (rig 10 Nov b, S1 at 08:01.97) otherwise loads into the next minute before the hold starts, and
    /// activation refuses it as a lost accounting epoch. A startup that
    /// cannot proceed releases native time after <see cref="MaximumWaitSeconds"/> outside the loading screen, so a
    /// city whose RLO never becomes ready is never frozen; its later activation keeps today's refusal rules.</summary>
    public sealed class StartupClockHold
    {
        public const double MaximumWaitSeconds = 15;
        private double waited;
        /// <summary>The hold is installed and owned by startup (before initialization starts).</summary>
        public bool Holding { get; private set; }
        /// <summary>The wait expired for this save; no early hold is installed again until the save changes.</summary>
        public bool Released { get; private set; }

        private object? save;
        /// <summary>The hold belongs to one save. Returns true when <paramref name="current"/> differs from it; the hold
        /// then starts over for the new save (a release for the old one does not carry over).</summary>
        public bool ForSave(object? current)
        {
            if (ReferenceEquals(save, current)) return false;
            Reset(); save = current;
            return true;
        }

        /// <summary>Returns true when the caller must install the hold now.</summary>
        public bool Begin()
        {
            if (Holding || Released) return false;
            Holding = true; waited = 0;
            return true;
        }

        /// <summary>One frame of waiting for RLO. Returns true once, when the caller must release native time.</summary>
        public bool Wait(bool loading, double unscaledSeconds)
        {
            if (!Holding || loading) return false;
            if (!Position.Finite(unscaledSeconds) || unscaledSeconds < 0) throw new ArgumentOutOfRangeException(nameof(unscaledSeconds));
            waited += unscaledSeconds;
            if (waited < MaximumWaitSeconds) return false;
            Holding = false; Released = true;
            return true;
        }

        /// <summary>Initialization started; its own hold rules apply from here on.</summary>
        public void Initializing() => Holding = false;

        public void Reset() { Holding = Released = false; waited = 0; }
    }
}
