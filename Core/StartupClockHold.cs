using System;

namespace PersistentCity.Core
{
    public sealed class StartupClockHold
    {
        public const double MaximumWaitSeconds = 15;
        private double waited;
        public bool Holding { get; private set; }
        public bool Released { get; private set; }

        private object? save;
        public bool ForSave(object? current)
        {
            if (ReferenceEquals(save, current)) return false;
            Reset(); save = current;
            return true;
        }

        public bool Begin()
        {
            if (Holding || Released) return false;
            Holding = true; waited = 0;
            return true;
        }

        public bool Wait(bool loading, double unscaledSeconds)
        {
            if (!Holding || loading) return false;
            if (!Position.Finite(unscaledSeconds) || unscaledSeconds < 0) throw new ArgumentOutOfRangeException(nameof(unscaledSeconds));
            waited += unscaledSeconds;
            if (waited < MaximumWaitSeconds) return false;
            Holding = false; Released = true;
            return true;
        }

        public void Initializing() => Holding = false;

        public void Reset() { Holding = Released = false; waited = 0; }
    }
}
