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

    public struct GcCollectionRow
    {
        public long Frame;
        public double FrameMs;
        public double ModMs;
        public int Gen0;
        public int Gen1;
        public int Gen2;
        public long HeapBefore;
        public long HeapAfter;
        public double PreviousFrameMs;
        public double NextFrameMs;
        public int OwnedBodies;
        public int BoundBodies;
    }

    public enum GcAllocationSegment
    {
        Mod, Advance, Physical, PhysicalAmbient, PhysicalVehicles, PhysicalBindings,
        PhysicalVendorMeals, PhysicalFade, PhysicalFocus, PhysicalObserve, PhysicalRelease, PhysicalParked, PhysicalIndex, PhysicalQueues,
        PhysicalRoster, PhysicalSelection, PhysicalSpawnPedestrian, PhysicalSpawnAppearance, PhysicalSpawnCheck, PhysicalSpawnVehicle, PhysicalUnattributed,
        PhysicalSpawnRandomize, PhysicalSpawnBake,
        AdvancePlanning, AdvanceEvents, AdvanceStops, AdvanceOther,
        PhysicalSpawnGrow, ModPrelude, ModRlo, ModFood, ModOptionalStops, ModRoutes, ModClosures, ModHoldReplay, ModPhysicalSetup,
        ModInspector, ModSummary, ModOther,
        ModRoutesSpeeds, ModRoutesNodes, ModRoutesSweep, ModRoutesRebuild, ModClosuresNetwork, ModClosuresLinks,
        AdvanceEventsTraffic, AdvanceEventsFood, AdvanceEventsCitizen, AdvanceEventsRouteBegin, AdvanceEventsOther,
        AdvancePlanningSlice, AdvancePlanningFinish,
        PhysicalSetupPlaces, PhysicalSetupBodies, PhysicalSetupVehicles, PhysicalSetupAmbient, PhysicalSetupPreflight,
        PhysicalUnattributedSetup, PhysicalUnattributedUpdate, PhysicalUnattributedWaiting, PhysicalUnattributedEntry,
        ModRoutesRetime, ModOptionalStopsRefresh, ModOptionalStopsPhysical,
    }

    public sealed class GcPauseSampler
    {
        public const int Segments = 64;
        private static readonly string[] SegmentNames =
        {
            "mod", "advance", "physical", "physical_ambient", "physical_vehicles", "physical_bindings",
            "physical_vendor_meals", "physical_fade", "physical_focus", "physical_observe", "physical_release", "physical_parked", "physical_index", "physical_queues",
            "physical_roster", "physical_selection", "physical_spawn_pedestrian", "physical_spawn_appearance", "physical_spawn_check", "physical_spawn_vehicle", "physical_unattributed",
            "physical_spawn_randomize", "physical_spawn_bake",
            "advance_planning", "advance_events", "advance_stops", "advance_other",
            "physical_spawn_grow", "mod_prelude", "mod_rlo", "mod_food", "mod_optional_stops", "mod_routes", "mod_closures", "mod_hold_replay", "mod_physical_setup",
            "mod_inspector", "mod_summary", "mod_other",
            "mod_routes_speeds", "mod_routes_nodes", "mod_routes_sweep", "mod_routes_rebuild", "mod_closures_network", "mod_closures_links",
            "advance_events_traffic", "advance_events_food", "advance_events_citizen", "advance_events_route_begin", "advance_events_other",
            "advance_planning_slice", "advance_planning_finish",
            "physical_setup_places", "physical_setup_bodies", "physical_setup_vehicles", "physical_setup_ambient", "physical_setup_preflight",
            "physical_unattributed_setup", "physical_unattributed_update", "physical_unattributed_waiting", "physical_unattributed_entry",
            "mod_routes_retime", "mod_optional_stops_refresh", "mod_optional_stops_physical",
        };

        public static string SegmentName(GcAllocationSegment segment) => SegmentNames[(int)segment];

        public static bool IsOuter(GcAllocationSegment segment) => segment >= GcAllocationSegment.ModPrelude && segment <= GcAllocationSegment.ModOther
            || segment >= GcAllocationSegment.ModRoutesSpeeds && segment <= GcAllocationSegment.ModClosuresLinks
            || segment >= GcAllocationSegment.ModRoutesRetime;

        public static bool IsSub(GcAllocationSegment segment) => segment >= GcAllocationSegment.ModRoutesSpeeds;

        public static GcAllocationSegment AdvanceEventSegment(int trafficEvents, int foodContinuationEvents, int citizenEvents, bool planningPending) =>
            planningPending ? GcAllocationSegment.AdvanceEventsRouteBegin
            : trafficEvents > 0 ? GcAllocationSegment.AdvanceEventsTraffic
            : foodContinuationEvents > 0 ? GcAllocationSegment.AdvanceEventsFood
            : citizenEvents > 0 ? GcAllocationSegment.AdvanceEventsCitizen
            : GcAllocationSegment.AdvanceEventsOther;

        public static GcAllocationSegment SubParent(GcAllocationSegment segment) => segment switch
        {
            >= GcAllocationSegment.ModRoutesSpeeds and <= GcAllocationSegment.ModRoutesRebuild or GcAllocationSegment.ModRoutesRetime => GcAllocationSegment.ModRoutes,
            >= GcAllocationSegment.ModClosuresNetwork and <= GcAllocationSegment.ModClosuresLinks => GcAllocationSegment.ModClosures,
            >= GcAllocationSegment.AdvanceEventsTraffic and <= GcAllocationSegment.AdvanceEventsOther => GcAllocationSegment.AdvanceEvents,
            >= GcAllocationSegment.AdvancePlanningSlice and <= GcAllocationSegment.AdvancePlanningFinish => GcAllocationSegment.AdvancePlanning,
            >= GcAllocationSegment.ModOptionalStopsRefresh => GcAllocationSegment.ModOptionalStops,
            >= GcAllocationSegment.PhysicalSetupPlaces => GcAllocationSegment.PhysicalUnattributed,
            _ => segment,
        };

        public static bool IsParent(GcAllocationSegment segment) =>
            segment == GcAllocationSegment.Mod || segment == GcAllocationSegment.Physical || segment == GcAllocationSegment.Advance;
        public const string PolicyId = "gc-sampler-v1";
        public const int Generations = 3;
        public const int DefaultCapacity = 65536;
        public const double SpikeMilliseconds = 30;
        public const string ReportPrefix = "[Persistent City] gc report ";
        public const int CollectionRowCapacity = 64;
        private readonly GcCollectionRow[] collectionRows = new GcCollectionRow[CollectionRowCapacity];
        private int collectionRowCount;
        private readonly double[] neighbourScratch = new double[CollectionRowCapacity];
        private double previousFrameMs = -1;
        private bool rowAwaitsNext, previousCollected;
        private long frameNumber = -1, lastFrameNumber = -1;

        private readonly float[] frameMs;
        private readonly float[] modMs;
        private readonly bool[] collected;
        private double[] spreadScratch = Array.Empty<double>();
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
        private readonly long[] pendingBytes = new long[Segments];
        private readonly int[] pendingSpans = new int[Segments];
        private long lastCounter = -1, counterFrameBytes, counterFrames;
        private int lastCounterCollections;
        private bool nested, nestedValid;
        private long nestedMark, detailMark;
        private int nestedCollections;

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

        public string AllocationCounter { get; private set; } = "heap_used";

        public void Start(long heapBytes, string allocationCounter = "heap_used")
        {
            AllocationCounter = allocationCounter ?? "heap_used";
            nested = false; lastCounter = -1; counterFrameBytes = counterFrames = 0;
            Array.Clear(collections, 0, collections.Length);
            stored = 0; collectionRowCount = 0; previousFrameMs = -1; rowAwaitsNext = previousCollected = false; lastFrameNumber = -1; Frames = 0; CollectionFrames = 0; heapBeforeSum = heapAfterSum = heapPairs = 0;
            MaxFrameMs = MaxModMs = 0; collectionSpikes = frameSpikes = 0;
            frameAllocatedBytes = allocationFrames = 0;
            FrameRecorder.Reset();
            Array.Clear(segmentBytes, 0, Segments); Array.Clear(segmentSamples, 0, Segments); Array.Clear(segmentSkipped, 0, Segments);
            HeapStart = HeapEnd = HeapPeak = lastHeap = heapBytes;
            baselined = false; pendingModMs = 0; ownedBodies = boundBodies = -1;
            Running = true;
        }

        public void Stop(long heapBytes)
        {
            if (!Running) return;
            HeapEnd = heapBytes; Running = false;
        }

        public void Observe(double stampMilliseconds, int gen0Total, int gen1Total, int gen2Total, long heapBytes)
            => Observe(stampMilliseconds, gen0Total, gen1Total, gen2Total, heapBytes, -1);

        public void Observe(double stampMilliseconds, int gen0Total, int gen1Total, int gen2Total, long heapBytes, long unityFrame)
        {
            if (!Running) return;
            if (baselined)
            {
                frameNumber = lastFrameNumber;
                Frame(stampMilliseconds - lastStampMs, pendingModMs, gen0Total - lastCounts[0], gen1Total - lastCounts[1], gen2Total - lastCounts[2], heapBytes);
                frameNumber = -1;
            }
            else if (heapBytes >= 0) lastHeap = heapBytes;
            lastFrameNumber = unityFrame;
            lastStampMs = stampMilliseconds; lastCounts[0] = gen0Total; lastCounts[1] = gen1Total; lastCounts[2] = gen2Total;
            pendingModMs = 0; baselined = true;
        }

        public void ModWork(double milliseconds) { if (Running && baselined) pendingModMs = milliseconds; }

        private int ownedBodies = -1, boundBodies = -1;

        public void NoteBodies(int owned, int bound) { ownedBodies = owned; boundBodies = bound; }

        public void Allocated(GcAllocationSegment segment, long heapBefore, long heapAfter, bool collected)
        {
            if (!Running || heapBefore < 0 || heapAfter < 0) return;
            int index = (int)segment;
            if (collected || heapAfter < heapBefore) { segmentSkipped[index]++; return; }
            segmentBytes[index] += heapAfter - heapBefore; segmentSamples[index]++;
        }

        public void CounterFrame(long counter, int collections)
        {
            if (!Running) return;
            if (lastCounter >= 0 && counter >= lastCounter && collections == lastCounterCollections) { counterFrameBytes += counter - lastCounter; counterFrames++; }
            lastCounter = counter; lastCounterCollections = collections;
        }

        public long CounterFrames => counterFrames;
        public long CounterFrameBytes => counterFrameBytes;

        public bool Nested => nested;

        public void BeginNested(long counter, int collections)
        {
            nested = false;
            if (!Running || counter < 0) return;
            Array.Clear(pendingBytes, 0, Segments); Array.Clear(pendingSpans, 0, Segments);
            nested = nestedValid = true; nestedMark = detailMark = counter; nestedCollections = collections;
        }

        public static bool IsDetail(GcAllocationSegment segment) =>
            segment == GcAllocationSegment.PhysicalSpawnRandomize || segment == GcAllocationSegment.PhysicalSpawnBake
            || segment >= GcAllocationSegment.AdvancePlanning && segment <= GcAllocationSegment.PhysicalSpawnGrow || IsSub(segment);

        public ModFrameRecorder FrameRecorder { get; } = new ModFrameRecorder();

        public void Outer(GcAllocationSegment segment, long bytes, bool valid)
        {
            if (!Running || !IsOuter(segment)) return;
            int index = (int)segment;
            if (!valid || bytes < 0) { segmentSkipped[index]++; return; }
            segmentBytes[index] += bytes; segmentSamples[index]++;
        }

        public void Charge(GcAllocationSegment segment, long counter, int collections)
        {
            if (!nested) return;
            if (IsDetail(segment)) { Detail(segment, counter, collections); return; }
            int index = (int)segment;
            if (counter < 0 || counter < nestedMark || collections != nestedCollections) nestedValid = false;
            else pendingBytes[index] += counter - nestedMark;
            pendingSpans[index]++;
            nestedMark = detailMark = counter; nestedCollections = collections;
        }

        public void Mark(long counter, int collections)
        {
            if (!nested) return;
            if (counter < 0 || counter < detailMark || collections != nestedCollections) nestedValid = false;
            detailMark = counter;
        }

        public void Detail(GcAllocationSegment segment, long counter, int collections)
        {
            if (!nested) return;
            int index = (int)segment;
            if (counter < 0 || counter < detailMark || collections != nestedCollections) nestedValid = false;
            else pendingBytes[index] += counter - detailMark;
            pendingSpans[index]++;
            detailMark = counter;
        }

        public void Sub(GcAllocationSegment segment, long counter, int collections)
        {
            if (!nested || !IsSub(segment)) return;
            int index = (int)segment;
            if (counter < 0 || counter < detailMark || collections != nestedCollections) nestedValid = false;
            else pendingBytes[index] += counter - detailMark;
            pendingSpans[index]++;
        }

        public void EndNested(GcAllocationSegment parent, GcAllocationSegment remainder, long counter, int collections)
        {
            if (!nested) return;
            Charge(remainder, counter, collections);
            nested = false;
            if (!Running) return;
            int index = (int)parent;
            if (!nestedValid)
            {
                segmentSkipped[index]++;
                for (int i = 0; i < Segments; i++) if (pendingSpans[i] > 0) segmentSkipped[i]++;
                return;
            }
            long total = 0;
            for (int i = 0; i < Segments; i++)
            {
                if (pendingSpans[i] == 0 || IsOuter((GcAllocationSegment)i)) continue;
                segmentBytes[i] += pendingBytes[i]; segmentSamples[i] += pendingSpans[i];
                if (!IsDetail((GcAllocationSegment)i)) total += pendingBytes[i];
            }
            segmentBytes[index] += total; segmentSamples[index]++;
        }

        public void EndDetails(GcAllocationSegment remainder, long counter, int collections)
        {
            if (!nested) return;
            Detail(remainder, counter, collections);
            nested = false;
            if (!Running) return;
            for (int i = 0; i < Segments; i++)
            {
                if (pendingSpans[i] == 0) continue;
                if (!nestedValid) { segmentSkipped[i]++; continue; }
                segmentBytes[i] += pendingBytes[i]; segmentSamples[i] += pendingSpans[i];
            }
        }

        public long AllocationFrames => allocationFrames;
        public long FrameAllocatedBytes => frameAllocatedBytes;
        public long SegmentBytes(GcAllocationSegment segment) => segmentBytes[(int)segment];
        public long SegmentSamples(GcAllocationSegment segment) => segmentSamples[(int)segment];
        public long SegmentSkipped(GcAllocationSegment segment) => segmentSkipped[(int)segment];

        public void Rebaseline() { baselined = false; pendingModMs = 0; lastCounter = -1; previousFrameMs = -1; previousCollected = rowAwaitsNext = false; }

        public void Frame(double frameMilliseconds, double modMilliseconds, int gen0, int gen1, int gen2, long heapBytes)
        {
            if (!Running) return;
            bool gc = gen0 > 0 || gen1 > 0 || gen2 > 0;
            collections[0] += Math.Max(0, gen0); collections[1] += Math.Max(0, gen1); collections[2] += Math.Max(0, gen2);
            Frames++;
            if (frameMilliseconds > MaxFrameMs) MaxFrameMs = frameMilliseconds;
            if (modMilliseconds > MaxModMs) MaxModMs = modMilliseconds;
            if (rowAwaitsNext) { collectionRows[collectionRowCount - 1].NextFrameMs = gc ? -1 : frameMilliseconds; rowAwaitsNext = false; }
            if (gc)
            {
                CollectionFrames++;
                if (lastHeap >= 0 && heapBytes >= 0) { heapBeforeSum += lastHeap; heapAfterSum += heapBytes; heapPairs++; }
                if (collectionRowCount < CollectionRowCapacity)
                {
                    collectionRows[collectionRowCount++] = new GcCollectionRow { Frame = frameNumber, FrameMs = frameMilliseconds, ModMs = modMilliseconds,
                        Gen0 = gen0, Gen1 = gen1, Gen2 = gen2, HeapBefore = lastHeap, HeapAfter = heapBytes, PreviousFrameMs = previousCollected ? -1 : previousFrameMs, NextFrameMs = -1,
                        OwnedBodies = ownedBodies, BoundBodies = boundBodies };
                    rowAwaitsNext = true;
                }
            }
            else if (lastHeap >= 0 && heapBytes >= lastHeap) { frameAllocatedBytes += heapBytes - lastHeap; allocationFrames++; }
            previousFrameMs = frameMilliseconds; previousCollected = gc;
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
        public GcSpread PauseWithoutModSpread() => Spread(frameMs, true, FrameSpread(false).P50, modMs);
        public int CollectionRowCount => collectionRowCount;
        public bool CollectionRowsTruncated => CollectionFrames > collectionRowCount;
        public GcCollectionRow CollectionRow(int index) => index < collectionRowCount ? collectionRows[index] : throw new ArgumentOutOfRangeException(nameof(index));

        public int CollectionSpikes() => collectionSpikes;

        public GcSpread PauseOverNeighboursSpread()
        {
            int count = 0;
            for (int i = 0; i < collectionRowCount; i++)
            {
                var row = collectionRows[i];
                if (row.PreviousFrameMs < 0 || row.NextFrameMs < 0) continue;
                neighbourScratch[count++] = Math.Max(0, row.FrameMs - 0.5 * (row.PreviousFrameMs + row.NextFrameMs));
            }
            if (count == 0) return new GcSpread(0, 0, 0, 0);
            Array.Sort(neighbourScratch, 0, count);
            return new GcSpread(count, Rank(neighbourScratch, count, .50), Rank(neighbourScratch, count, .95), neighbourScratch[count - 1]);
        }

        public int FrameSpikes() => frameSpikes;

        public long HeapBeforeCollection => heapPairs == 0 ? -1 : heapBeforeSum / heapPairs;
        public long HeapAfterCollection => heapPairs == 0 ? -1 : heapAfterSum / heapPairs;

        private GcSpread Spread(float[] source, bool withCollection, double baseline, float[]? less = null)
        {
            int count = 0;
            for (int i = 0; i < stored; i++) if (collected[i] == withCollection) count++;
            if (count == 0) return new GcSpread(0, 0, 0, 0);
            if (spreadScratch.Length < count) spreadScratch = new double[stored];
            var values = spreadScratch;
            int at = 0;
            for (int i = 0; i < stored; i++) if (collected[i] == withCollection) values[at++] = Math.Max(0, source[i] - baseline - (less == null ? 0 : less[i]));
            Array.Sort(values, 0, count);
            return new GcSpread(count, Rank(values, count, .50), Rank(values, count, .95), values[count - 1]);
        }

        internal static double Rank(double[] sorted, int count, double fraction)
        {
            int index = (int)Math.Ceiling(fraction * count) - 1;
            return sorted[Math.Max(0, Math.Min(count - 1, index))];
        }

        public long SpreadScratchBytes => (long)spreadScratch.Length * sizeof(double);

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
                .Append(",\"alloc_frames\":").Append(N(allocationFrames)).Append(",\"frame_alloc_bytes\":").Append(N(frameAllocatedBytes))
                .Append(",\"frame_alloc_counter\":\"heap_used\"")
                .Append(",\"alloc_counter\":\"").Append(AllocationCounter).Append('"')
                .Append(",\"counter_frames\":").Append(N(counterFrames)).Append(",\"counter_frame_bytes\":").Append(N(counterFrameBytes));
            for (int i = 0; i < Segments; i++)
            {
                string name = SegmentNames[i];
                text.Append(",\"").Append(name).Append("_alloc_bytes\":").Append(N(segmentBytes[i]))
                    .Append(",\"").Append(name).Append("_alloc_samples\":").Append(N(segmentSamples[i]))
                    .Append(",\"").Append(name).Append("_alloc_skipped\":").Append(N(segmentSkipped[i]));
            }
            FrameRecorder.AppendJson(text.Append(','));
            S(text, "pause_ms_without_mod", PauseWithoutModSpread());
            text.Append(",\"collection_rows_retained\":").Append(N(collectionRowCount))
                .Append(",\"collection_rows_truncated\":").Append(CollectionRowsTruncated ? "true" : "false")
                .Append(",\"collection_rows\":[");
            for (int i = 0; i < collectionRowCount; i++)
            {
                var r = collectionRows[i];
                if (i > 0) text.Append(',');
                text.Append("{\"frame\":").Append(N(r.Frame)).Append(",\"frame_ms\":").Append(D(r.FrameMs)).Append(",\"mod_ms\":").Append(D(r.ModMs))
                    .Append(",\"gen0\":").Append(N(r.Gen0)).Append(",\"gen1\":").Append(N(r.Gen1)).Append(",\"gen2\":").Append(N(r.Gen2))
                    .Append(",\"heap_before_bytes\":").Append(N(r.HeapBefore)).Append(",\"heap_after_bytes\":").Append(N(r.HeapAfter))
                    .Append(",\"previous_frame_ms\":").Append(D(r.PreviousFrameMs)).Append(",\"next_frame_ms\":").Append(D(r.NextFrameMs))
                    .Append(",\"owned_bodies\":").Append(N(r.OwnedBodies)).Append(",\"bound_bodies\":").Append(N(r.BoundBodies)).Append('}');
            }
            text.Append(']');
            S(text, "pause_ms_over_neighbours", PauseOverNeighboursSpread());
            text.Append('}');
            return text.ToString();
        }

        public string ReportHuman(GcReportContext context)
        {
            var pause = PauseSpread();
            var outside = PauseWithoutModSpread();
            var neighbours = PauseOverNeighboursSpread();
            string M(long bytes) => bytes < 0 ? "n/a" : (bytes / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture) + " MB";
            string D(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);
            return "[Persistent City] gc measurement (diagnostic only): persons=" + (context.Residents + context.Visitors).ToString(CultureInfo.InvariantCulture)
                + " frames=" + Frames.ToString(CultureInfo.InvariantCulture) + " collections gen0/1/2=" + collections[0].ToString(CultureInfo.InvariantCulture)
                + "/" + collections[1].ToString(CultureInfo.InvariantCulture) + "/" + collections[2].ToString(CultureInfo.InvariantCulture)
                + " pause p50/p95/max=" + D(pause.P50) + "/" + D(pause.P95) + "/" + D(pause.Max) + " ms (outside UpdateCity p95/max=" + D(outside.P95) + "/" + D(outside.Max) + " ms, over neighbouring frames p95/max=" + D(neighbours.P95) + "/" + D(neighbours.Max) + " ms), gc frames >= 30 ms=" + CollectionSpikes().ToString(CultureInfo.InvariantCulture)
                + ", heap " + M(HeapStart) + " -> " + M(HeapEnd) + " (peak " + M(HeapPeak) + ")";
        }
    }
}
