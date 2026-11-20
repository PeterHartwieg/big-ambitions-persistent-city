using System;
using System.Globalization;
using System.Text;

namespace PersistentCity.Core
{
    public enum RetimeAllocationStage { Release, Journeys, Routes, Commit, Rebuild }

    public readonly struct RetimeAllocationMark
    {
        public RetimeAllocationMark(long counter, int collections) { Counter = counter; Collections = collections; }
        public long Counter { get; }
        public int Collections { get; }
    }

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
        public int Spans { get; private set; }
        public int InvalidSpans { get; private set; }

        public long this[RetimeAllocationStage stage] => bytes[(int)stage];

        public long Total
        {
            get { long total = 0; for (int i = 0; i < Stages; i++) total += bytes[i]; return total; }
        }

        public void Begin(long heap, long used)
        {
            if (open) return;
            Clear();
            open = true;
            HeapBefore = heap; UsedBefore = used;
        }

        public void Observe(ClockRetimeOutcome outcome)
        {
            if (outcome == ClockRetimeOutcome.None && open) Reset();
        }

        public void Add(RetimeAllocationStage stage, RetimeAllocationMark before, RetimeAllocationMark after)
        {
            if (!open) return;
            Spans++;
            if (after.Collections != before.Collections || after.Counter < before.Counter) { InvalidSpans++; return; }
            bytes[(int)stage] += after.Counter - before.Counter;
        }

        public void Finish(long heap, long used)
        {
            if (!open) throw new InvalidOperationException("The retime tally is not open.");
            HeapAfter = heap; UsedAfter = used;
            open = false;
        }

        public void Reset() { open = false; Clear(); }

        private void Clear()
        {
            Array.Clear(bytes, 0, Stages);
            HeapBefore = UsedBefore = HeapAfter = UsedAfter = 0; Spans = InvalidSpans = 0;
        }

        public string Describe(string counter)
        {
            var text = new StringBuilder("allocated (").Append(counter).Append(") ");
            for (int i = 0; i < Stages; i++)
            {
                if (i > 0) text.Append(" / ");
                text.Append(Names[i]).Append(' ').Append(bytes[i].ToString(CultureInfo.InvariantCulture)).Append(" B");
            }
            text.Append(", total ").Append(Total.ToString(CultureInfo.InvariantCulture)).Append(" B over ").Append(Spans.ToString(CultureInfo.InvariantCulture))
                .Append(" spans, ").Append(InvalidSpans.ToString(CultureInfo.InvariantCulture)).Append(" spans excluded because a collection ran inside them");
            if (InvalidSpans > 0) text.Append(" (totals are a lower bound)");
            return text.Append("; Mono heap / used at start ").Append(HeapBefore.ToString(CultureInfo.InvariantCulture)).Append(" / ").Append(UsedBefore.ToString(CultureInfo.InvariantCulture))
                .Append(" B, at finish ").Append(HeapAfter.ToString(CultureInfo.InvariantCulture)).Append(" / ").Append(UsedAfter.ToString(CultureInfo.InvariantCulture))
                .Append(" B, heap growth ").Append((HeapAfter - HeapBefore).ToString(CultureInfo.InvariantCulture)).Append(" B").ToString();
        }
    }
}
