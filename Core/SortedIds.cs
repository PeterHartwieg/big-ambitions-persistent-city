using System.Collections.Generic;

namespace PersistentCity.Core
{
    internal struct SortedIds
    {
        private static readonly List<int> None = new List<int>();
        private List<int>? items;

        public int Count => items?.Count ?? 0;
        public int this[int index] => (items ?? None)[index];

        public bool Add(int id)
        {
            var list = items ??= new List<int>();
            int at = list.BinarySearch(id);
            if (at >= 0) return false;
            list.Insert(~at, id);
            return true;
        }

        public bool Remove(int id)
        {
            if (items == null) return false;
            int at = items.BinarySearch(id);
            if (at < 0) return false;
            items.RemoveAt(at);
            return true;
        }

        public void Clear() => items?.Clear();
        public bool Contains(int id) => items != null && items.BinarySearch(id) >= 0;
        public List<int>.Enumerator GetEnumerator() => (items ?? None).GetEnumerator();
        public List<int> ToList() => items == null ? new List<int>() : new List<int>(items);
    }
}
