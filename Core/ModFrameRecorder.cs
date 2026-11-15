using System;
using System.Globalization;
using System.Text;

namespace PersistentCity.Core
{
    public struct ModFrameRow
    {
        public long Frame;
        public double ModMs;
        public int Phase;
        public double PhaseMs;
        public int Detail;
        public double DetailMs;
        public int Sub;
        public double SubMs;
        public int GcPhase;
        public int Collections;
        public int AllocPhase;
        public long AllocBytes;
        public long Bytes;
    }

    public sealed class ModFrameRecorder
    {
        public const int TopFrames = 16;
        public const int SpikeCapacity = 1024;
        public const double SpikeMilliseconds = 30;
        private const int Segments = GcPauseSampler.Segments;
        private readonly long[] ticks = new long[Segments];
        private readonly long[] bytes = new long[Segments];
        private readonly long[] totalTicks = new long[Segments];
        private readonly int[] gcAt = new int[Segments];
        private readonly ModFrameRow[] top = new ModFrameRow[TopFrames];
        private readonly ModFrameRow[] spikes = new ModFrameRow[SpikeCapacity];
        private int topCount, spikeCount;
        private long frame, began, mark, detailMark, counterMark, detailCounterMark;
        private int collectionsMark, detailCollectionsMark, epochMark;
        private bool open, countersValid;
        private double ticksPerMillisecond = 1;

        public bool Armed { get; private set; }
        public long Frames { get; private set; }
        public long Spikes { get; private set; }
        public int TopCount => topCount;
        public int SpikeCount => spikeCount;
        public bool SpikesTruncated => Spikes > spikeCount;
        public ModFrameRow Top(int index) => top[index];
        public ModFrameRow Spike(int index) => spikes[index];
        public bool Open => open;
        public double TotalMilliseconds(GcAllocationSegment segment) => totalTicks[(int)segment] / ticksPerMillisecond;

        public static bool Overlaps(GcAllocationSegment segment) => segment == GcAllocationSegment.PhysicalSpawnRandomize
            || segment == GcAllocationSegment.PhysicalSpawnBake || segment == GcAllocationSegment.PhysicalSpawnGrow || GcPauseSampler.IsSub(segment);

        public void Arm(double ticksPerSecond)
        {
            if (!(ticksPerSecond > 0) || double.IsInfinity(ticksPerSecond)) throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
            Reset();
            ticksPerMillisecond = ticksPerSecond / 1000.0;
            Armed = true;
        }

        public void Disarm() { Armed = false; open = false; }

        public void Reset()
        {
            Array.Clear(ticks, 0, Segments); Array.Clear(bytes, 0, Segments); Array.Clear(totalTicks, 0, Segments);
            topCount = spikeCount = 0; Frames = Spikes = 0; open = false;
        }

        public void Begin(long frameNumber, long timestamp, long counter, int collections) => Begin(frameNumber, timestamp, counter, collections, 0);

        public void Begin(long frameNumber, long timestamp, long counter, int collections, int counterEpoch)
        {
            if (!Armed) return;
            frame = frameNumber; open = true;
            began = mark = detailMark = timestamp;
            counterMark = detailCounterMark = counter; countersValid = counter >= 0; epochMark = counterEpoch;
            collectionsMark = detailCollectionsMark = collections;
            Array.Clear(ticks, 0, Segments); Array.Clear(bytes, 0, Segments);
            for (int i = 0; i < Segments; i++) gcAt[i] = 0;
        }

        public void Charge(GcAllocationSegment segment, long timestamp, long counter, int collections) => Charge(segment, timestamp, counter, collections, 0);

        public void Charge(GcAllocationSegment segment, long timestamp, long counter, int collections, int counterEpoch)
        {
            if (!open) return;
            int index = (int)segment;
            if (Overlaps(segment)) { Detail(segment, timestamp, counter, collections, counterEpoch); return; }
            if (timestamp > mark) ticks[index] += timestamp - mark;
            if (counterEpoch != epochMark) countersValid = false;
            if (countersValid && counter >= counterMark) bytes[index] += counter - counterMark; else countersValid = false;
            if (collections > collectionsMark) gcAt[index] += collections - collectionsMark;
            mark = detailMark = timestamp; counterMark = detailCounterMark = counter; collectionsMark = detailCollectionsMark = collections;
        }

        public void Detail(GcAllocationSegment segment, long timestamp, long counter, int collections) => Detail(segment, timestamp, counter, collections, 0);

        public void Detail(GcAllocationSegment segment, long timestamp, long counter, int collections, int counterEpoch)
        {
            if (!open) return;
            int index = (int)segment;
            if (timestamp > detailMark) ticks[index] += timestamp - detailMark;
            if (counterEpoch != epochMark) countersValid = false;
            if (countersValid && counter >= detailCounterMark) bytes[index] += counter - detailCounterMark;
            detailMark = timestamp; detailCounterMark = counter; detailCollectionsMark = collections;
        }

        public void MarkDetail(long timestamp, long counter, int collections)
        {
            if (!open) return;
            detailMark = timestamp; detailCounterMark = counter; detailCollectionsMark = collections;
        }

        public void End(long timestamp, long counter, int collections, GcPauseSampler? sampler) => End(timestamp, counter, collections, 0, sampler);

        public void End(long timestamp, long counter, int collections, int counterEpoch, GcPauseSampler? sampler)
        {
            if (!open) return;
            double modMilliseconds = (timestamp - began) / ticksPerMillisecond;
            Charge(GcAllocationSegment.ModOther, timestamp, counter, collections, counterEpoch);
            open = false;
            var row = new ModFrameRow { Frame = frame, ModMs = modMilliseconds, Phase = -1, Detail = -1, Sub = -1, GcPhase = -1, AllocPhase = -1 };
            long phaseTicks = 0, detailTicks = 0, subTicks = 0;
            for (int i = 0; i < Segments; i++)
            {
                var segment = (GcAllocationSegment)i;
                totalTicks[i] += ticks[i];
                if (GcPauseSampler.IsParent(segment)) continue;
                if (Overlaps(segment))
                {
                    if (!GcPauseSampler.IsSub(segment)) { if (ticks[i] > detailTicks) { detailTicks = ticks[i]; row.Detail = i; } }
                    else if (ticks[i] > subTicks) { subTicks = ticks[i]; row.Sub = i; }
                    if (sampler != null && GcPauseSampler.IsOuter(segment) && (ticks[i] > 0 || bytes[i] > 0)) sampler.Outer(segment, bytes[i], countersValid);
                    continue;
                }
                row.Bytes += bytes[i];
                if (ticks[i] > phaseTicks) { phaseTicks = ticks[i]; row.Phase = i; }
                if (bytes[i] > row.AllocBytes) { row.AllocBytes = bytes[i]; row.AllocPhase = i; }
                if (gcAt[i] > 0) { row.Collections += gcAt[i]; if (row.GcPhase < 0) row.GcPhase = i; }
                if (sampler != null && GcPauseSampler.IsOuter(segment) && (ticks[i] > 0 || bytes[i] > 0)) sampler.Outer(segment, bytes[i], countersValid);
            }
            row.PhaseMs = phaseTicks / ticksPerMillisecond;
            row.DetailMs = detailTicks / ticksPerMillisecond;
            row.SubMs = subTicks / ticksPerMillisecond;
            Frames++;
            if (modMilliseconds >= SpikeMilliseconds)
            {
                Spikes++;
                if (spikeCount < SpikeCapacity) spikes[spikeCount++] = row;
            }
            Insert(row);
        }

        private void Insert(ModFrameRow row)
        {
            if (topCount == TopFrames && !(row.ModMs > top[topCount - 1].ModMs)) return;
            int at = topCount < TopFrames ? topCount++ : TopFrames - 1;
            while (at > 0 && top[at - 1].ModMs < row.ModMs) { top[at] = top[at - 1]; at--; }
            top[at] = row;
        }

        public void AppendJson(StringBuilder text)
        {
            string N(long value) => value.ToString(CultureInfo.InvariantCulture);
            string D(double value) => Math.Round(value, 3).ToString("0.###", CultureInfo.InvariantCulture);
            string Name(int index) => index < 0 ? "none" : GcPauseSampler.SegmentName((GcAllocationSegment)index);
            void Rows(string key, ModFrameRow[] rows, int count)
            {
                text.Append('"').Append(key).Append("\":[");
                for (int i = 0; i < count; i++)
                {
                    var r = rows[i];
                    if (i > 0) text.Append(',');
                    text.Append("{\"frame\":").Append(N(r.Frame)).Append(",\"mod_ms\":").Append(D(r.ModMs))
                        .Append(",\"phase\":\"").Append(Name(r.Phase)).Append("\",\"phase_ms\":").Append(D(r.PhaseMs))
                        .Append(",\"detail\":\"").Append(Name(r.Detail)).Append("\",\"detail_ms\":").Append(D(r.DetailMs))
                        .Append(",\"sub\":\"").Append(Name(r.Sub)).Append("\",\"sub_ms\":").Append(D(r.SubMs))
                        .Append(",\"gc_phase\":\"").Append(Name(r.GcPhase)).Append("\",\"collections\":").Append(N(r.Collections))
                        .Append(",\"alloc_phase\":\"").Append(Name(r.AllocPhase)).Append("\",\"alloc_phase_bytes\":").Append(N(r.AllocBytes))
                        .Append(",\"alloc_bytes\":").Append(N(r.Bytes)).Append('}');
                }
                text.Append(']');
            }
            text.Append("\"phase_frames\":").Append(N(Frames)).Append(",\"phase_spikes_30ms\":").Append(N(Spikes))
                .Append(",\"phase_spikes_retained\":").Append(N(spikeCount)).Append(",\"phase_spikes_truncated\":").Append(SpikesTruncated ? "true" : "false")
                .Append(",\"phase_spike_cap\":").Append(N(SpikeCapacity)).Append(',');
            Rows("top_frames", top, topCount);
            text.Append(',');
            Rows("spike_frames", spikes, spikeCount);
            text.Append(",\"phase_ms\":{");
            for (int i = 0; i < Segments; i++)
            {
                if (i > 0) text.Append(',');
                text.Append('"').Append(Name(i)).Append("\":").Append(D(totalTicks[i] / ticksPerMillisecond));
            }
            text.Append('}');
        }

        public string ReportJson()
        {
            var text = new StringBuilder(4096).Append("{\"policy\":\"mod-frames-v1\",\"armed\":").Append(Armed ? "true" : "false").Append(',');
            AppendJson(text);
            return text.Append('}').ToString();
        }

        public string ReportHuman()
        {
            var text = new StringBuilder(512).Append("[Persistent City] mod frames (diagnostic only): frames=").Append(Frames.ToString(CultureInfo.InvariantCulture))
                .Append(" spikes>=30ms=").Append(Spikes.ToString(CultureInfo.InvariantCulture))
                .Append(" retained=").Append(spikeCount.ToString(CultureInfo.InvariantCulture)).Append(SpikesTruncated ? " truncated" : "");
            for (int i = 0; i < topCount && i < 5; i++)
            {
                var r = top[i];
                text.Append("; frame ").Append(r.Frame.ToString(CultureInfo.InvariantCulture)).Append(' ')
                    .Append(r.ModMs.ToString("0.0", CultureInfo.InvariantCulture)).Append(" ms ")
                    .Append(r.Phase < 0 ? "none" : GcPauseSampler.SegmentName((GcAllocationSegment)r.Phase))
                    .Append(r.GcPhase < 0 ? "" : " gc@" + GcPauseSampler.SegmentName((GcAllocationSegment)r.GcPhase));
            }
            return text.ToString();
        }
    }
}
