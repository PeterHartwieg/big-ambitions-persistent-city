using System;

namespace PersistentCity.Core
{
    /// <summary>Reusable scratch data, never a cached route or a cached permission decision.</summary>
    internal sealed class RoadSearchWorkspace
    {
        private struct NodeState
        {
            public double Distance;
            public int Predecessor;
            public int Generation;
            public int FirstEntry;
        }
        private struct Entry
        {
            public (double Estimate, double Cost, int Node) Priority;
            public int HeapIndex;
            public int Previous;
            public int Next;
        }

        private NodeState[] states = Array.Empty<NodeState>();
        private Entry[] entries = Array.Empty<Entry>();
        private int[] heap = Array.Empty<int>();
        private int generation;
        private int entryCount;
        private int freeEntry;
        public int Count { get; private set; }

        public void Begin(int nodeCount)
        {
            EnsureNodes(nodeCount);
            if (generation == int.MaxValue)
            {
                Array.Clear(states, 0, states.Length);
                generation = 1;
            }
            else generation++;
            Count = 0; entryCount = 0; freeEntry = -1;
        }

        public bool TryGetDistance(int node, out double distance)
        {
            if (node < states.Length && states[node].Generation == generation)
            {
                distance = states[node].Distance;
                return true;
            }
            distance = 0;
            return false;
        }

        public bool TryGetPredecessor(int node, out int predecessor)
        {
            if (node >= 0 && node < states.Length && states[node].Generation == generation && states[node].Predecessor >= 0)
            {
                predecessor = states[node].Predecessor;
                return true;
            }
            predecessor = 0;
            return false;
        }

        public void SetDistance(int node, double distance, int predecessor)
        {
            // The IReadOnlyList may wrap a mutable list; a callback can add usable nodes.
            EnsureNodes(node + 1);
            ref var state = ref states[node];
            if (state.Generation != generation)
            {
                state.Generation = generation;
                state.FirstEntry = -1;
            }
            state.Distance = distance;
            state.Predecessor = predecessor;
        }

        public void Add((double Estimate, double Cost, int Node) priority)
        {
            if (Find(priority) >= 0) return; // Match SortedSet's full-tuple uniqueness.
            int slot;
            if (freeEntry >= 0)
            {
                slot = freeEntry;
                freeEntry = entries[slot].Next;
            }
            else
            {
                if (entryCount == entries.Length) Array.Resize(ref entries, Capacity(entries.Length, entryCount + 1));
                slot = entryCount++;
            }
            if (Count == heap.Length) Array.Resize(ref heap, Capacity(heap.Length, Count + 1));
            int first = states[priority.Node].FirstEntry;
            entries[slot] = new Entry { Priority = priority, HeapIndex = Count, Previous = -1, Next = first };
            if (first >= 0) entries[first].Previous = slot;
            states[priority.Node].FirstEntry = slot;
            heap[Count++] = slot;
            SiftUp(Count - 1);
        }

        public (double Estimate, double Cost, int Node) Pop()
        {
            int slot = heap[0];
            var priority = entries[slot].Priority;
            RemoveEntry(slot);
            return priority;
        }

        public void Remove((double Estimate, double Cost, int Node) priority)
        {
            int slot = Find(priority);
            if (slot >= 0) RemoveEntry(slot);
        }

        private int Find((double Estimate, double Cost, int Node) priority)
        {
            // Usually one entry per node. If a callback changes positions, the original
            // algorithm can fail to remove an old priority and retain multiple entries.
            // Keep those entries too, rather than changing expansion/callback semantics.
            for (int slot = states[priority.Node].FirstEntry; slot >= 0; slot = entries[slot].Next)
                if (Compare(entries[slot].Priority, priority) == 0) return slot;
            return -1;
        }

        private void RemoveEntry(int slot)
        {
            var entry = entries[slot];
            if (entry.Previous >= 0) entries[entry.Previous].Next = entry.Next;
            else states[entry.Priority.Node].FirstEntry = entry.Next;
            if (entry.Next >= 0) entries[entry.Next].Previous = entry.Previous;
            int index = entry.HeapIndex;
            Count--;
            if (index < Count)
            {
                heap[index] = heap[Count];
                entries[heap[index]].HeapIndex = index;
                if (index > 0 && Less(index, (index - 1) / 2)) SiftUp(index);
                else SiftDown(index);
            }
            entries[slot].Next = freeEntry;
            freeEntry = slot;
        }

        private bool Less(int left, int right) => Compare(entries[heap[left]].Priority, entries[heap[right]].Priority) < 0;

        // Double.CompareTo also preserves the default ValueTuple ordering for NaN,
        // infinities and signed zero, not just ordinary finite route costs.
        private static int Compare((double Estimate, double Cost, int Node) left, (double Estimate, double Cost, int Node) right)
        {
            int order = left.Estimate.CompareTo(right.Estimate);
            if (order != 0) return order;
            order = left.Cost.CompareTo(right.Cost);
            return order != 0 ? order : left.Node.CompareTo(right.Node);
        }

        private void SiftUp(int index)
        {
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (!Less(index, parent)) return;
                Swap(index, parent);
                index = parent;
            }
        }

        private void SiftDown(int index)
        {
            while (index < Count / 2)
            {
                int child = index * 2 + 1;
                if (child + 1 < Count && Less(child + 1, child)) child++;
                if (!Less(child, index)) return;
                Swap(child, index);
                index = child;
            }
        }

        private void Swap(int left, int right)
        {
            int slot = heap[left]; heap[left] = heap[right]; heap[right] = slot;
            entries[heap[left]].HeapIndex = left;
            entries[heap[right]].HeapIndex = right;
        }

        private void EnsureNodes(int count)
        {
            if (count > states.Length) Array.Resize(ref states, Capacity(states.Length, count));
        }

        private static int Capacity(int current, int required) => Math.Max(required, current == 0 ? 16 : current * 2);
    }
}
