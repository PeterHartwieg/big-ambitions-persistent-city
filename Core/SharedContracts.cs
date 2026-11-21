using System.Collections.Generic;

namespace PersistentCity.Core
{
    public sealed class SharedContracts<T>
    {
        private static readonly EqualityComparer<T> Comparer = EqualityComparer<T>.Default;
        private readonly Dictionary<int, T[]> shared = new Dictionary<int, T[]>();

        public int Distinct => shared.Count;

        public T[]? Intern(List<T>? source)
        {
            if (source == null) return null;
            int hash = Hash(source);
            if (shared.TryGetValue(hash, out var candidate) && Same(source, candidate)) return candidate;
            var copy = source.ToArray();
            if (candidate == null) shared.Add(hash, copy);
            return copy;
        }

        private static int Hash(List<T> source)
        {
            unchecked
            {
                int hash = 17 + source.Count;
                for (int i = 0; i < source.Count; i++) hash = hash * 31 + Comparer.GetHashCode(source[i]!);
                return hash;
            }
        }

        private static bool Same(List<T> source, T[] candidate)
        {
            if (source.Count != candidate.Length) return false;
            for (int i = 0; i < candidate.Length; i++)
                if (!Comparer.Equals(source[i], candidate[i])) return false;
            return true;
        }
    }
}
