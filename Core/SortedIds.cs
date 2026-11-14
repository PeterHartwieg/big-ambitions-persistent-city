using System.Collections.Generic;

namespace PersistentCity.Core
{
    internal sealed class SortedIds
    {
        private readonly List<int> items = new List<int>();

        public int Count => items.Count;
        public int this[int index] => items[index];

        public bool Add(int id)
        {
            int at = items.BinarySearch(id);
            if (at >= 0) return false;
            items.Insert(~at, id);
            return true;
        }

        public bool Remove(int id)
        {
            int at = items.BinarySearch(id);
            if (at < 0) return false;
            items.RemoveAt(at);
            return true;
        }

        public void Clear() => items.Clear();
        public bool Contains(int id) => items.BinarySearch(id) >= 0;
        public List<int>.Enumerator GetEnumerator() => items.GetEnumerator();
        public List<int> ToList() => new List<int>(items);
    }
}
