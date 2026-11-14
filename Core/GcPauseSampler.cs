using System;
using System.Globalization;
using System.Text;

namespace PersistentCity.Core
{
    public readonly struct GcReportContext
    {
        public GcReportContext(long residents, long visitors, int? personTarget, int maxGeneration, bool incremental, long monoHeapBytes, long monoUsedBytes)
        {
            Residents = residents; Visitors = visitors; PersonTarget = personTarget; MaxGeneration = maxGeneration; Incremental = incremental;
            MonoHeapBytes = monoHeapBytes; MonoUsedBytes = monoUsedBytes;
        }
        public long Residents { get; }
        public long Visitors { get; }
        public int? PersonTarget { get; }
        public int MaxGeneration { get; }
        public bool Incremental { get; }
        public long MonoHeapBytes { get; }
        public long MonoUsedBytes { get; }
    }

    public readonly struct GcSpread
    {
        public GcSpread(int count, double p50, double p95, double max) { Count = count; P50 = p50; P95 = p95; Max = max; }
        public int Count { get; }
        public double P50 { get; }
        public double P95 { get; }
        public double Max { get; }
    }

    public enum GcAllocationSegment { Mod, Advance, Physical }

    public sealed class GcPauseSampler
    {
        public const int Segments = 3;
        public const string PolicyId = "gc-sampler-v1";
        public const int Generations = 3;
        public const int DefaultCapacity = 65536;
        public const double SpikeMilliseconds = 30;
        public const string ReportPrefix = "[Persistent City] gc report ";

        private readonly float[] frameMs;
        private readonly float[] modMs;
        private readonly bool[] collected;
        private readonly long[] collections = new long[Generations];
        private int stored;
        private long heapBeforeSum, heapAfterSum, heapPairs;
        private long lastHeap = -1;
        private readonly int[] lastCounts = new int[Generations];
        private double lastStampMs, pendingModMs;
        private bool baselined;
        private int collectionSpikes, frameSpikes;
        private long frameAllocatedBytes, allocationFrames;
        private readonly long[] segmentBytes = new long[Segments];
        private readonly long[] segmentSamples = new long[Segments];
        private readonly long[] segmentSkipped = new long[Segments];

        public GcPauseSampler(int capacity = DefaultCapacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            frameMs = new float[capacity]; modMs = new float[capacity]; collected = new bool[capacity];
        }

        public bool Running { get; private set; }
        public int Capacity => frameMs.Length;
        public long Frames { get; private set; }
        public long Dropped => Frames - stored;
        public long CollectionFrames { get; private set; }
        public long HeapStart { get; private set; } = -1;
        public long HeapEnd { get; private set; } = -1;
        public long HeapPeak { get; private set; } = -1;
        public double MaxFrameMs { get; private set; }
        public double MaxModMs { get; private set; }
        public long Collections(int generation) => collections[generation];

        public void Start(long heapBytes)
        {
            Array.Clear(collections, 0, collections.Length);
            stored = 0; Frames = 0; CollectionFrames = 0; heapBeforeSum = heapAfterSum = heapPairs = 0;
            MaxFrameMs = MaxModMs = 0; collectionSpikes = frameSpikes = 0;
            frameAllocatedBytes = allocationFrames = 0;
            Array.Clear(segmentBytes, 0, Segments); Array.Clear(segmentSamples, 0, Segments); Array.Clear(segmentSkipped, 0, Segments);
            HeapStart = HeapEnd = HeapPeak = lastHeap = heapBytes;
            baselined = false; pendingModMs = 0;
            Running = true;
        }

        public void Stop(long heapBytes)
        {
            if (!Running) return;
            HeapEnd = heapBytes; Running = false;
        }

        public void Observe(double stampMilliseconds, int gen0Total, int gen1Total, int gen2Total, long heapBytes)
        {
            if (!Running) return;
            if (baselined)
                Frame(stampMilliseconds - lastStampMs, pendingModMs, gen0Total - lastCounts[0], gen1Total - lastCounts[1], gen2Total - lastCounts[2], heapBytes);
            else if (heapBytes >= 0) lastHeap = heapBytes;
            lastStampMs = stampMilliseconds; lastCounts[0] = gen0Total; lastCounts[1] = gen1Total; lastCounts[2] = gen2Total;
            pendingModMs = 0; baselined = true;
        }

        public void ModWork(double milliseconds) { if (Running && baselined) pendingModMs = milliseconds; }

        public void Allocated(GcAllocationSegment segment, long heapBefore, long heapAfter, bool collected)
        {
            if (!Running || heapBefore < 0 || heapAfter < 0) return;
            int index = (int)segment;
            if (collected || heapAfter < heapBefore) { segmentSkipped[index]++; return; }
            segmentBytes[index] += heapAfter - heapBefore; segmentSamples[index]++;
        }

        public long AllocationFrames => allocationFrames;
        public long FrameAllocatedBytes => frameAllocatedBytes;
        public long SegmentBytes(GcAllocationSegment segment) => segmentBytes[(int)segment];
        public long SegmentSamples(GcAllocationSegment segment) => segmentSamples[(int)segment];
        public long SegmentSkipped(GcAllocationSegment segment) => segmentSkipped[(int)segment];

        public void Rebaseline() { baselined = false; pendingModMs = 0; }

        public void Frame(double frameMilliseconds, double modMilliseconds, int gen0, int gen1, int gen2, long heapBytes)
        {
            if (!Running) return;
            bool gc = gen0 > 0 || gen1 > 0 || gen2 > 0;
            collections[0] += Math.Max(0, gen0); collections[1] += Math.Max(0, gen1); collections[2] += Math.Max(0, gen2);
            Frames++;
            if (frameMilliseconds > MaxFrameMs) MaxFrameMs = frameMilliseconds;
            if (modMilliseconds > MaxModMs) MaxModMs = modMilliseconds;
            if (gc)
            {
                CollectionFrames++;
                if (lastHeap >= 0 && heapBytes >= 0) { heapBeforeSum += lastHeap; heapAfterSum += heapBytes; heapPairs++; }
            }
            else if (lastHeap >= 0 && heapBytes >= lastHeap) { frameAllocatedBytes += heapBytes - lastHeap; allocationFrames++; }
            if (heapBytes > HeapPeak) HeapPeak = heapBytes;
            if (heapBytes >= 0) { lastHeap = heapBytes; HeapEnd = heapBytes; }
            if (frameMilliseconds >= SpikeMilliseconds) { frameSpikes++; if (gc) collectionSpikes++; }
            if (stored == frameMs.Length) return;
            frameMs[stored] = (float)frameMilliseconds; modMs[stored] = (float)modMilliseconds; collected[stored] = gc;
            stored++;
        }

        public GcSpread FrameSpread(bool withCollection) => Spread(frameMs, withCollection, 0);
        public GcSpread ModSpread(bool withCollection) => Spread(modMs, withCollection, 0);
        public GcSpread PauseSpread() => Spread(frameMs, true, FrameSpread(false).P50);

        public int CollectionSpikes() => collectionSpikes;

        public int FrameSpikes() => frameSpikes;

        public long HeapBeforeCollection => heapPairs == 0 ? -1 : heapBeforeSum / heapPairs;
        public long HeapAfterCollection => heapPairs == 0 ? -1 : heapAfterSum / heapPairs;

        private GcSpread Spread(float[] source, bool withCollection, double baseline)
        {
            int count = 0;
            for (int i = 0; i < stored; i++) if (collected[i] == withCollection) count++;
            if (count == 0) return new GcSpread(0, 0, 0, 0);
            var values = new double[count];
            int at = 0;
            for (int i = 0; i < stored; i++) if (collected[i] == withCollection) values[at++] = Math.Max(0, source[i] - baseline);
            Array.Sort(values);
            return new GcSpread(count, Rank(values, .50), Rank(values, .95), values[count - 1]);
        }

        internal static double Rank(double[] sorted, double fraction)
        {
            int index = (int)Math.Ceiling(fraction * sorted.Length) - 1;
            return sorted[Math.Max(0, Math.Min(sorted.Length - 1, index))];
        }

        public string ReportJson(GcReportContext context)
        {
            string N(long value) => value.ToString(CultureInfo.InvariantCulture);
            string D(double value) => Math.Round(value, 3).ToString("0.###", CultureInfo.InvariantCulture);
            void S(StringBuilder text, string name, GcSpread spread) => text.Append(",\"").Append(name).Append("\":{\"count\":").Append(N(spread.Count))
                .Append(",\"p50\":").Append(D(spread.P50)).Append(",\"p95\":").Append(D(spread.P95)).Append(",\"max\":").Append(D(spread.Max)).Append('}');
            var text = new StringBuilder(768)
                .Append("{\"policy\":\"").Append(PolicyId).Append("\",\"mode\":\"measurement\",\"running\":").Append(Running ? "true" : "false")
                .Append(",\"residents\":").Append(N(context.Residents)).Append(",\"visitors\":").Append(N(context.Visitors))
                .Append(",\"persons\":").Append(N(context.Residents + context.Visitors))
                .Append(",\"person_target\":").Append(context.PersonTarget is int target ? N(target) : "null")
                .Append(",\"max_generation\":").Append(N(context.MaxGeneration)).Append(",\"incremental\":").Append(context.Incremental ? "true" : "false")
                .Append(",\"frames\":").Append(N(Frames)).Append(",\"frames_stored\":").Append(N(stored)).Append(",\"frames_dropped\":").Append(N(Dropped))
                .Append(",\"collection_frames\":").Append(N(CollectionFrames))
                .Append(",\"gen0\":").Append(N(collections[0])).Append(",\"gen1\":").Append(N(collections[1])).Append(",\"gen2\":").Append(N(collections[2]))
                .Append(",\"heap_start_bytes\":").Append(N(HeapStart)).Append(",\"heap_end_bytes\":").Append(N(HeapEnd)).Append(",\"heap_peak_bytes\":").Append(N(HeapPeak))
                .Append(",\"heap_before_gc_bytes\":").Append(N(HeapBeforeCollection)).Append(",\"heap_after_gc_bytes\":").Append(N(HeapAfterCollection))
                .Append(",\"mono_heap_bytes\":").Append(N(context.MonoHeapBytes)).Append(",\"mono_used_bytes\":").Append(N(context.MonoUsedBytes));
            S(text, "frame_ms_gc", FrameSpread(true));
            S(text, "frame_ms_plain", FrameSpread(false));
            S(text, "pause_ms", PauseSpread());
            S(text, "mod_ms_gc", ModSpread(true));
            S(text, "mod_ms_plain", ModSpread(false));
            text.Append(",\"gc_spikes_30ms\":").Append(N(CollectionSpikes())).Append(",\"frame_spikes_30ms\":").Append(N(FrameSpikes()))
                .Append(",\"max_frame_ms\":").Append(D(MaxFrameMs)).Append(",\"max_mod_ms\":").Append(D(MaxModMs))
                .Append(",\"alloc_frames\":").Append(N(allocationFrames)).Append(",\"frame_alloc_bytes\":").Append(N(frameAllocatedBytes));
            for (int i = 0; i < Segments; i++)
            {
                string name = ((GcAllocationSegment)i).ToString().ToLowerInvariant();
                text.Append(",\"").Append(name).Append("_alloc_bytes\":").Append(N(segmentBytes[i]))
                    .Append(",\"").Append(name).Append("_alloc_samples\":").Append(N(segmentSamples[i]))
                    .Append(",\"").Append(name).Append("_alloc_skipped\":").Append(N(segmentSkipped[i]));
            }
            text.Append('}');
            return text.ToString();
        }

        public string ReportHuman(GcReportContext context)
        {
            var pause = PauseSpread();
            string M(long bytes) => bytes < 0 ? "n/a" : (bytes / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture) + " MB";
            string D(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);
            return "[Persistent City] gc measurement (diagnostic only): persons=" + (context.Residents + context.Visitors).ToString(CultureInfo.InvariantCulture)
                + " frames=" + Frames.ToString(CultureInfo.InvariantCulture) + " collections gen0/1/2=" + collections[0].ToString(CultureInfo.InvariantCulture)
                + "/" + collections[1].ToString(CultureInfo.InvariantCulture) + "/" + collections[2].ToString(CultureInfo.InvariantCulture)
                + " pause p50/p95/max=" + D(pause.P50) + "/" + D(pause.P95) + "/" + D(pause.Max) + " ms, gc frames >= 30 ms=" + CollectionSpikes().ToString(CultureInfo.InvariantCulture)
                + ", heap " + M(HeapStart) + " -> " + M(HeapEnd) + " (peak " + M(HeapPeak) + ")";
        }
    }
}
