using System;
using System.Globalization;
using System.Text;

namespace PersistentCity.Core
{
    public enum GrowKind : byte { SyncInstantiate, AsyncStart, AsyncFinish }

    public struct GrowTiming
    {
        public int Frame;
        public GrowKind Kind;
        public double Staging, Result, Checks, Parent, Own, Total;
        public bool Failed;
    }

    public sealed class GrowTimingLog
    {
        public const int Capacity = 32;
        public const double SlowMilliseconds = 10;
        private readonly GrowTiming[] slow = new GrowTiming[Capacity];
        private int next;
        private GrowTiming slowest;

        public long Recorded { get; private set; }
        public long SlowRecorded { get; private set; }
        public int SlowCount => (int)Math.Min(SlowRecorded, Capacity);
        public GrowTiming Slowest => slowest;

        public void Record(in GrowTiming timing)
        {
            Recorded++;
            if (Recorded == 1 || timing.Total > slowest.Total) slowest = timing;
            if (timing.Total < SlowMilliseconds) return;
            slow[next] = timing;
            next = (next + 1) % Capacity;
            SlowRecorded++;
        }

        public GrowTiming Slow(int index)
        {
            if (index < 0 || index >= SlowCount) throw new ArgumentOutOfRangeException(nameof(index));
            int oldest = SlowRecorded > Capacity ? next : 0;
            return slow[(oldest + index) % Capacity];
        }

        public string Report()
        {
            var text = new StringBuilder("grow operations ").Append(Recorded.ToString(CultureInfo.InvariantCulture))
                .Append(", slow (>= ").Append(SlowMilliseconds.ToString("0", CultureInfo.InvariantCulture)).Append(" ms) ")
                .Append(SlowRecorded.ToString(CultureInfo.InvariantCulture)).Append(" retained ").Append(SlowCount.ToString(CultureInfo.InvariantCulture))
                .Append(" truncated=").Append(SlowRecorded > Capacity ? "true" : "false");
            if (Recorded > 0) Append(text.Append("; slowest "), slowest);
            for (int i = 0; i < SlowCount; i++) Append(text.Append("; "), Slow(i));
            return text.ToString();
        }

        private static void Append(StringBuilder text, GrowTiming timing)
        {
            text.Append("frame=").Append(timing.Frame.ToString(CultureInfo.InvariantCulture)).Append(' ')
                .Append(KindName(timing.Kind))
                .Append(timing.Failed ? " failed " : " ").Append(Ms(timing.Total)).Append(" ms (staging ").Append(Ms(timing.Staging)).Append(" / result ").Append(Ms(timing.Result)).Append(" / checks ").Append(Ms(timing.Checks))
                .Append(" / parent ").Append(Ms(timing.Parent)).Append(" / own ").Append(Ms(timing.Own)).Append(')');
        }

        public static string KindName(GrowKind kind) => kind == GrowKind.SyncInstantiate ? "sync" : kind == GrowKind.AsyncStart ? "async_start" : "async_finish";

        private static string Ms(double value) => value.ToString("0.000", CultureInfo.InvariantCulture);
    }

    public sealed class GrowTimer
    {
        public const int Staging = 0, Result = 1, Checks = 2, Parent = 3, Own = 4;
        private readonly double ticksPerMillisecond;
        private GrowTiming timing;
        private long started, mark;

        public GrowTimer(double ticksPerSecond)
        {
            if (!(ticksPerSecond > 0) || double.IsInfinity(ticksPerSecond)) throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
            ticksPerMillisecond = ticksPerSecond / 1000.0;
        }

        public int Active { get; private set; }

        public void Begin(int frame, GrowKind kind, long timestamp)
        {
            timing = new GrowTiming { Frame = frame, Kind = kind, Failed = true };
            started = mark = timestamp;
            Active = kind == GrowKind.AsyncFinish ? Result : Staging;
        }

        public void Done(int step, long timestamp)
        {
            Add(step, timestamp);
            Active = step + 1;
        }

        public void Outcome(bool succeeded) => timing.Failed = !succeeded;

        public GrowTiming End(long timestamp)
        {
            if (timing.Failed && Active <= Own) Add(Active, timestamp);
            timing.Total = (timestamp - started) / ticksPerMillisecond;
            return timing;
        }

        private void Add(int step, long timestamp)
        {
            double elapsed = (timestamp - mark) / ticksPerMillisecond;
            mark = timestamp;
            switch (step)
            {
                case Staging: timing.Staging += elapsed; break;
                case Result: timing.Result += elapsed; break;
                case Checks: timing.Checks += elapsed; break;
                case Parent: timing.Parent += elapsed; break;
                default: timing.Own += elapsed; break;
            }
        }
    }
}
