namespace PersistentCity.Core
{
    public sealed class TrafficRateCache
    {
        public const int Capacity = 3;
        private readonly double[] rates = new double[Capacity];
        private readonly TrafficNetwork?[] networks = new TrafficNetwork?[Capacity];
        private readonly long[] used = new long[Capacity];
        private long clock;

        public int Count { get; private set; }
        public long Hits { get; private set; }

        public bool TryGet(double clockRate, out TrafficNetwork network)
        {
            for (int i = 0; i < Count; i++)
                if (rates[i] == clockRate)
                {
                    network = networks[i]!; used[i] = ++clock; Hits++;
                    return true;
                }
            if (Position.Finite(clockRate) && clockRate > 0)
                for (int i = Count - 1; i >= 0; i--)
                    if (System.Math.Abs(rates[i] - clockRate) <= NearRate * System.Math.Max(rates[i], clockRate)) Release(i);
            network = null!;
            return false;
        }

        public const double NearRate = 1e-4;

        private void Release(int slot)
        {
            int last = Count - 1;
            rates[slot] = rates[last]; networks[slot] = networks[last]; used[slot] = used[last];
            rates[last] = 0; networks[last] = null; used[last] = 0;
            Count = last;
        }

        public void Remember(double clockRate, TrafficNetwork network)
        {
            if (!Position.Finite(clockRate) || clockRate <= 0 || network == null || network.RatedCopy) return;
            int slot = -1;
            for (int i = 0; i < Count; i++) if (rates[i] == clockRate) { slot = i; break; }
            if (slot < 0 && Count < Capacity) slot = Count++;
            if (slot < 0)
            {
                slot = 0;
                for (int i = 1; i < Count; i++) if (used[i] < used[slot]) slot = i;
            }
            rates[slot] = clockRate; networks[slot] = network; used[slot] = ++clock;
        }
    }
}
