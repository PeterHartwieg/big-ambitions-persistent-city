using System;

namespace PersistentCity.Core
{
    public sealed class PhysicalSetupGate
    {
        public const double StartMilliseconds = 2;
        public const int MaximumWaitFrames = 15;
        private readonly double start;
        private readonly int maximumWait;
        private int waited;

        public PhysicalSetupGate(double startMilliseconds = StartMilliseconds, int maximumWaitFrames = MaximumWaitFrames)
        {
            if (!(startMilliseconds >= 0) || double.IsInfinity(startMilliseconds)) throw new ArgumentOutOfRangeException(nameof(startMilliseconds));
            if (maximumWaitFrames < 0) throw new ArgumentOutOfRangeException(nameof(maximumWaitFrames));
            start = startMilliseconds; maximumWait = maximumWaitFrames;
        }

        public long Deferred { get; private set; }
        public long Reserved { get; private set; }
        public bool Due => waited >= maximumWait;

        public bool Reserve(bool setupWanted)
        {
            if (!setupWanted || !Due) return false;
            Reserved++;
            return true;
        }
        public int LastWait { get; private set; }

        public bool Admit(double elapsedMilliseconds)
        {
            if (elapsedMilliseconds <= start || waited >= maximumWait)
            {
                LastWait = waited; waited = 0;
                return true;
            }
            waited++; Deferred++;
            return false;
        }
    }
}
