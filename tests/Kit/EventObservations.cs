using System;
using System.Collections.Generic;

namespace FoodDemandAuditProbe
{
    public struct EventWork
    {
        public string Citizen, Branch;
        public double Minute;
        public long Sequence;
        public long Ticks, NavMeshTicks, RoadSearchTicks;
        public long AStarTicks;
        public double NavMeshPathMilliseconds;
        public int Slices, Plans, Failures;
    }

    // One frame only. Resumed slices of the same event are summed, not compared individually.
    // Values copied into retained rows cannot alias the scratch dictionary.
    public sealed class EventWorkAccumulator
    {
        private readonly int capacity;
        private readonly Dictionary<(string, double, string, long), EventWork> events;
        public EventWork Slowest { get; private set; }
        public int Dropped { get; private set; }
        public EventWorkAccumulator(int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            this.capacity = capacity;
            events = new Dictionary<(string, double, string, long), EventWork>(capacity);
        }
        public void Clear() { events.Clear(); Slowest = default; Dropped = 0; }
        public void Add(EventWork slice)
        {
            var key = (slice.Citizen, slice.Minute, slice.Branch, slice.Sequence);
            if (!events.TryGetValue(key, out var total))
            {
                if (events.Count == capacity) { Dropped++; return; }
                total = new EventWork { Citizen = slice.Citizen, Minute = slice.Minute, Branch = slice.Branch, Sequence = slice.Sequence };
            }
            total.Ticks += slice.Ticks; total.NavMeshTicks += slice.NavMeshTicks;
            total.RoadSearchTicks += slice.RoadSearchTicks; total.Plans += slice.Plans;
            total.AStarTicks += slice.AStarTicks; total.NavMeshPathMilliseconds += slice.NavMeshPathMilliseconds;
            total.Slices += slice.Slices; total.Failures += slice.Failures;
            events[key] = total;
            if (total.Ticks > Slowest.Ticks) Slowest = total;
            else if (Slowest.Citizen == total.Citizen && Slowest.Minute == total.Minute && Slowest.Branch == total.Branch && Slowest.Sequence == total.Sequence)
                Slowest = total;
        }
    }

    // Cumulative allocation counter deltas and live heap deltas have different meanings.
    public struct ManagedDelta
    {
        public long Value;
        public bool Available;
        public static ManagedDelta Between(long before, long after, bool cumulative)
            => new ManagedDelta { Value = after - before, Available = before >= 0 && after >= 0 && (!cumulative || after >= before) };
    }
}
