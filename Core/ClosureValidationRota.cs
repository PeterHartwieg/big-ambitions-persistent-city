using System;

namespace PersistentCity.Core
{
    public sealed class ClosureValidationRota
    {
        public const int DefaultPerBatch = 256;
        public const int DefaultRevalidateBatches = 8;
        private readonly int perBatch, revalidateBatches;
        private int[] validatedAt = Array.Empty<int>();
        private int generation, left, calls, interval = 1;

        public ClosureValidationRota(int perBatch = DefaultPerBatch, int revalidateBatches = DefaultRevalidateBatches)
        {
            if (perBatch < 1) throw new ArgumentOutOfRangeException(nameof(perBatch));
            if (revalidateBatches < 1) throw new ArgumentOutOfRangeException(nameof(revalidateBatches));
            this.perBatch = perBatch; this.revalidateBatches = revalidateBatches;
        }

        public long Validated { get; private set; }
        public long Deferred { get; private set; }
        public int PerBatch => perBatch;
        public int RevalidateBatches => revalidateBatches;
        public int Interval => interval;

        public void Begin(int nodes)
        {
            if (nodes < 0) throw new ArgumentOutOfRangeException(nameof(nodes));
            if (validatedAt.Length != nodes) { validatedAt = new int[nodes]; generation = 0; }
            if (generation == int.MaxValue) { Array.Clear(validatedAt, 0, validatedAt.Length); generation = 0; }
            generation++;
            interval = Math.Max(revalidateBatches, (calls + perBatch - 1) / perBatch + 1);
            left = perBatch; calls = 0;
        }

        public bool TakeFull(int index)
        {
            if (index < 0 || index >= validatedAt.Length) return true;
            calls++;
            int last = validatedAt[index];
            if (last != 0 && generation - last < interval) return false;
            if (left == 0) { Deferred++; return false; }
            left--; validatedAt[index] = generation; Validated++;
            return true;
        }
    }
}
