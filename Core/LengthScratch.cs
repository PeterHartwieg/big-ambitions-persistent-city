using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public sealed class LengthScratch<T> where T : struct
    {
        private sealed class Slot
        {
            public T[][] Arrays = Array.Empty<T[]>();
            public long LastUse;
        }

        private readonly Dictionary<int, Slot> byLength = new Dictionary<int, Slot>();
        private readonly int width;
        private readonly long elementBytes;
        private long clock;

        public LengthScratch(int width, long elementBytes, long budgetBytes)
        {
            if (width < 1) throw new ArgumentOutOfRangeException(nameof(width));
            if (elementBytes < 1) throw new ArgumentOutOfRangeException(nameof(elementBytes));
            if (budgetBytes < 1) throw new ArgumentOutOfRangeException(nameof(budgetBytes));
            this.width = width; this.elementBytes = elementBytes; BudgetBytes = budgetBytes;
        }

        public long BudgetBytes { get; }
        public long Bytes { get; private set; }
        public int Count => byLength.Count;
        public long Allocations { get; private set; }
        public long Evictions { get; private set; }

        public long CostOf(int length) => width * elementBytes * length;

        public T[][] Take(int length)
        {
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            if (byLength.TryGetValue(length, out var known)) { known.LastUse = ++clock; return known.Arrays; }
            long cost = CostOf(length);
            while (byLength.Count > 0 && Bytes + cost > BudgetBytes) EvictOldest();
            var arrays = new T[width][];
            for (int i = 0; i < width; i++) arrays[i] = new T[length];
            byLength.Add(length, new Slot { Arrays = arrays, LastUse = ++clock });
            Bytes += cost;
            Allocations++;
            return arrays;
        }

        public void Clear() { byLength.Clear(); Bytes = 0; }

        private void EvictOldest()
        {
            int oldest = 0;
            long stamp = long.MaxValue;
            foreach (var pair in byLength)
                if (pair.Value.LastUse < stamp) { stamp = pair.Value.LastUse; oldest = pair.Key; }
            byLength.Remove(oldest);
            Bytes -= CostOf(oldest);
            Evictions++;
        }
    }
}
