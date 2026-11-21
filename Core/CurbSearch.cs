using System.Collections.Generic;

namespace PersistentCity.Core
{
    public interface ICurbSource
    {
        long Now();
        void CollectCell(int x, int z, List<int> into);
        void Order(List<int> nearby);
        bool Full { get; }
        void Attempt(int candidate);
    }

    public sealed class CurbSearch
    {
        public const int MaximumAttempts = 12;
        private enum Stage { Done, Collect, Attempt }
        private readonly List<int> nearby = new List<int>();
        private Stage stage;
        private int fromX, toX, fromZ, toZ, x, z, cursor, attempts;

        public bool Done => stage == Stage.Done;
        public int Candidates => nearby.Count;
        public int Attempts => attempts;
        public int Calls { get; private set; }

        public void Begin(int cellX, int cellZ, int reach)
        {
            nearby.Clear();
            fromX = cellX - reach; toX = cellX + reach; fromZ = cellZ - reach; toZ = cellZ + reach;
            x = fromX; z = fromZ; cursor = attempts = 0; Calls = 0;
            stage = Stage.Collect;
        }

        public void Cancel() { stage = Stage.Done; nearby.Clear(); }

        public bool Continue(ICurbSource source, long deadline)
        {
            if (stage == Stage.Done) return true;
            Calls++;
            bool worked = false;
            while (stage == Stage.Collect)
            {
                if (x > toX) { source.Order(nearby); stage = Stage.Attempt; break; }
                if (worked && source.Now() >= deadline) return false;
                source.CollectCell(x, z, nearby);
                worked = true;
                if (++z > toZ) { z = fromZ; x++; }
            }
            while (stage == Stage.Attempt)
            {
                if (cursor >= nearby.Count || attempts + 1 > MaximumAttempts || source.Full) { stage = Stage.Done; return true; }
                if (worked && source.Now() >= deadline) return false;
                attempts++;
                source.Attempt(nearby[cursor++]);
                worked = true;
            }
            return true;
        }
    }
}
