using System;
using System.Globalization;
using System.Text;

namespace PersistentCity.Core
{
    public enum RetimeAllocationStage { Release, Journeys, Routes, Commit, Rebuild }

    public sealed class RetimeAllocationTally
    {
        private const int Stages = 5;
        private static readonly string[] Names = { "body release", "journey retime", "routes", "commit", "rebuild" };
        private readonly long[] bytes = new long[Stages];
        private bool open;

        public bool Open => open;
        public long HeapBefore { get; private set; }
        public long UsedBefore { get; private set; }
        public long HeapAfter { get; private set; }
        public long UsedAfter { get; private set; }

        public long this[RetimeAllocationStage stage] => bytes[(int)stage];

        public long Total
        {
            get { long total = 0; for (int i = 0; i < Stages; i++) total += bytes[i]; return total; }
        }

        public void Begin(long heap, long used)
        {
            if (open) return;
            open = true;
            Array.Clear(bytes, 0, Stages);
            HeapBefore = heap; UsedBefore = used; HeapAfter = UsedAfter = 0;
        }

        public void Add(RetimeAllocationStage stage, long before, long after)
        {
            if (!open) throw new InvalidOperationException("The retime tally is not open.");
            if (after > before) bytes[(int)stage] += after - before;
        }

        public void Finish(long heap, long used)
        {
            if (!open) throw new InvalidOperationException("The retime tally is not open.");
            HeapAfter = heap; UsedAfter = used;
            open = false;
        }

        public void Reset() { open = false; Array.Clear(bytes, 0, Stages); HeapBefore = UsedBefore = HeapAfter = UsedAfter = 0; }

        public string Describe(string counter)
        {
            var text = new StringBuilder("allocated (").Append(counter).Append(") ");
            for (int i = 0; i < Stages; i++)
            {
                if (i > 0) text.Append(" / ");
                text.Append(Names[i]).Append(' ').Append(bytes[i].ToString(CultureInfo.InvariantCulture)).Append(" B");
            }
            return text.Append(", total ").Append(Total.ToString(CultureInfo.InvariantCulture))
                .Append(" B; Mono heap / used at start ").Append(HeapBefore.ToString(CultureInfo.InvariantCulture)).Append(" / ").Append(UsedBefore.ToString(CultureInfo.InvariantCulture))
                .Append(" B, at finish ").Append(HeapAfter.ToString(CultureInfo.InvariantCulture)).Append(" / ").Append(UsedAfter.ToString(CultureInfo.InvariantCulture))
                .Append(" B, heap growth ").Append((HeapAfter - HeapBefore).ToString(CultureInfo.InvariantCulture)).Append(" B").ToString();
        }
    }
}
