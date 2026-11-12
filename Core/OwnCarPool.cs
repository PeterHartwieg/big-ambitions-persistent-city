using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    /// <summary>
    /// GR-9 (G7): the own-car instance pool, by model. At most <see cref="Capacity"/> instances exist. A request reuses an
    /// idle copy of its model; at capacity it destroys an idle copy of another model (the longest idle) to make room;
    /// destroyed copies leave the count. New copies are limited to <see cref="CreatesPerFrame"/> per frame, so a street
    /// coming into view does not instantiate every car in one frame.
    /// </summary>
    public sealed class OwnCarPool<T> where T : class
    {
        private readonly Func<T, bool> live;
        private readonly Action<T> destroy;
        private readonly Dictionary<string, Stack<T>> idle = new Dictionary<string, Stack<T>>(StringComparer.Ordinal);
        // Idle copies in the order they were returned, for eviction across models.
        private readonly LinkedList<(string Model, T Item)> idleOrder = new LinkedList<(string, T)>();
        private readonly List<T> all = new List<T>();
        private int frame = int.MinValue, created;

        public OwnCarPool(int capacity, Func<T, bool> live, Action<T> destroy, int createsPerFrame = 2)
        { Capacity = Math.Max(1, capacity); this.live = live; this.destroy = destroy; CreatesPerFrame = Math.Max(1, createsPerFrame); }

        public int Capacity { get; }
        public int CreatesPerFrame { get; }
        public int Count { get { Prune(); return all.Count; } }
        public IReadOnlyList<T> All => all;

        /// <summary>An idle copy of <paramref name="model"/>, else a new one from <paramref name="create"/> when capacity
        /// and this frame's create budget allow (evicting another model's idle copy at capacity). Null otherwise.</summary>
        public T? Take(string model, int frameNumber, Func<T?> create)
        {
            Prune();
            if (idle.TryGetValue(model, out var stack))
                while (stack.Count > 0)
                {
                    var item = stack.Pop();
                    RemoveOrder(model, item);
                    if (live(item)) return item;
                    all.Remove(item);
                }
            if (frameNumber != frame) { frame = frameNumber; created = 0; }
            if (created >= CreatesPerFrame) return null;
            if (all.Count >= Capacity && !Evict()) return null;
            created++;
            var made = create();
            if (made != null) all.Add(made);
            return made;
        }

        public void Return(string model, T item)
        {
            if (item == null || !live(item)) { if (item != null) all.Remove(item); return; }
            if (!idle.TryGetValue(model, out var stack)) idle.Add(model, stack = new Stack<T>());
            stack.Push(item);
            idleOrder.AddLast((model, item));
        }

        public void Clear()
        {
            foreach (var item in all) if (live(item)) destroy(item);
            all.Clear(); idle.Clear(); idleOrder.Clear();
        }

        private bool Evict()
        {
            var node = idleOrder.First;
            if (node == null) return false;
            idleOrder.RemoveFirst();
            var (model, item) = node.Value;
            if (idle.TryGetValue(model, out var stack))
            {
                var keep = new Stack<T>();
                while (stack.Count > 0) { var next = stack.Pop(); if (!ReferenceEquals(next, item)) keep.Push(next); }
                while (keep.Count > 0) stack.Push(keep.Pop());
            }
            all.Remove(item);
            if (live(item)) destroy(item);
            return true;
        }

        private void RemoveOrder(string model, T item)
        {
            for (var node = idleOrder.First; node != null; node = node.Next)
                if (ReferenceEquals(node.Value.Item, item)) { idleOrder.Remove(node); return; }
        }

        private void Prune() => all.RemoveAll(item => !live(item));
    }
}
