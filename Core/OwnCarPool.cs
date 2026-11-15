using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public sealed class OwnCarPool<T> where T : class
    {
        private readonly Func<T, bool> live;
        private readonly Predicate<T> dead;
        private readonly Action<T> destroy;
        private readonly Dictionary<string, Stack<T>> idle = new Dictionary<string, Stack<T>>(StringComparer.Ordinal);
        private readonly LinkedList<(string Model, T Item)> idleOrder = new LinkedList<(string, T)>();
        private readonly Stack<LinkedListNode<(string Model, T Item)>> spareNodes = new Stack<LinkedListNode<(string Model, T Item)>>();
        private readonly Stack<T> evictScratch = new Stack<T>();
        private readonly List<T> all = new List<T>();
        private int frame = int.MinValue, created;

        public OwnCarPool(int capacity, Func<T, bool> live, Action<T> destroy, int createsPerFrame = 2)
        {
            Capacity = Math.Max(1, capacity); this.live = live; this.destroy = destroy; CreatesPerFrame = Math.Max(1, createsPerFrame);
            dead = item => !this.live(item);
        }

        public int Capacity { get; }
        public int CreatesPerFrame { get; }
        public long Creations { get; private set; }
        public int Count { get { Prune(); return all.Count; } }
        public IReadOnlyList<T> All => all;

        public T? Take(string model, int frameNumber, Func<T?> create)
        {
            if (create == null) throw new ArgumentNullException(nameof(create));
            return Take(model, frameNumber, static make => make(), create);
        }

        public T? Take<TState>(string model, int frameNumber, Func<TState, T?> create, TState state)
        {
            if (create == null) throw new ArgumentNullException(nameof(create));
            var item = TakeIdle(model);
            if (item != null) return item;
            if (frameNumber != frame) { frame = frameNumber; created = 0; }
            if (created >= CreatesPerFrame) return null;
            if (all.Count >= Capacity && !Evict()) return null;
            created++; Creations++;
            var made = create(state);
            if (made != null) all.Add(made);
            return made;
        }

        public T? TakeIdle(string model)
        {
            Prune();
            if (idle.TryGetValue(model, out var stack))
                while (stack.Count > 0)
                {
                    var item = stack.Pop();
                    RemoveOrder(item);
                    if (live(item)) return item;
                    all.Remove(item);
                }
            return null;
        }

        public void Return(string model, T item)
        {
            if (item == null || !live(item)) { if (item != null) all.Remove(item); return; }
            if (!idle.TryGetValue(model, out var stack)) idle.Add(model, stack = new Stack<T>());
            stack.Push(item);
            var node = spareNodes.Count > 0 ? spareNodes.Pop() : new LinkedListNode<(string Model, T Item)>(default);
            node.Value = (model, item);
            idleOrder.AddLast(node);
        }

        public void Clear()
        {
            foreach (var item in all) if (live(item)) destroy(item);
            all.Clear(); idle.Clear(); idleOrder.Clear(); spareNodes.Clear();
        }

        private bool Evict()
        {
            var node = idleOrder.First;
            if (node == null) return false;
            idleOrder.RemoveFirst();
            var (model, item) = node.Value;
            node.Value = default;
            spareNodes.Push(node);
            if (idle.TryGetValue(model, out var stack))
            {
                var keep = evictScratch;
                keep.Clear();
                while (stack.Count > 0) { var next = stack.Pop(); if (!ReferenceEquals(next, item)) keep.Push(next); }
                while (keep.Count > 0) stack.Push(keep.Pop());
            }
            all.Remove(item);
            if (live(item)) destroy(item);
            return true;
        }

        private void RemoveOrder(T item)
        {
            for (var node = idleOrder.First; node != null; node = node.Next)
                if (ReferenceEquals(node.Value.Item, item))
                {
                    idleOrder.Remove(node);
                    node.Value = default;
                    spareNodes.Push(node);
                    return;
                }
        }

        private void Prune() => all.RemoveAll(dead);
    }
}
