using System;

namespace PersistentCity.Core
{
    public sealed class AtomicCheckReserve
    {
        public const int DefaultMaximumWaits = 8;
        private readonly double floorTicksPerItem;
        private readonly int maximumWaits;
        private double ticksPerItem;

        public AtomicCheckReserve(double floorTicksPerItem, int maximumWaits = DefaultMaximumWaits)
        {
            if (!(floorTicksPerItem > 0) || double.IsInfinity(floorTicksPerItem)) throw new ArgumentOutOfRangeException(nameof(floorTicksPerItem));
            if (maximumWaits < 0) throw new ArgumentOutOfRangeException(nameof(maximumWaits));
            this.floorTicksPerItem = ticksPerItem = floorTicksPerItem;
            this.maximumWaits = maximumWaits;
        }

        public double TicksPerItem => ticksPerItem;
        public long Deferred { get; private set; }
        public long Forced { get; private set; }

        public long ReserveTicks(int items) => items <= 0 ? 0 : (long)Math.Ceiling(ticksPerItem * items);

        public bool Admits(long now, long deadline, int items, ref int waits)
        {
            if (deadline == long.MaxValue || items <= 0 || deadline - now >= ReserveTicks(items)) { waits = 0; return true; }
            if (waits >= maximumWaits) { waits = 0; Forced++; return true; }
            waits++; Deferred++;
            return false;
        }

        public void Record(long ticks, int items)
        {
            if (items <= 0 || ticks < 0) return;
            double observed = ticks / (double)items;
            ticksPerItem = Math.Max(floorTicksPerItem, Math.Max(observed * 1.25, ticksPerItem * .95));
        }
    }
}
