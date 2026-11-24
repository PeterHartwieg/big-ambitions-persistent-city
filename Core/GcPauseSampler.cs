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
        public double LateMs;
        public int Gen0;
        public int Gen1;
        public int Gen2;
        public long HeapBefore;
        public long HeapAfter;
        public double PreviousFrameMs;
        public double NextFrameMs;
        public int OwnedBodies;
        public int BoundBodies;
        public string Diagnostic;
        public GcAsyncGrow AsyncGrow;
        public long Gen0Through;
        public double FrameCpuMs;
        public double ModCpuMs;
        public double BeginStampMs;
        public double EndStampMs;
        public long Instantiated;
        public long Destroyed;
        public long Activated;
        public long AsyncStarted;
        public long AsyncCompleted;
        public long MeshRequests;
    }

    public enum GcAsyncGrow { None, Pending, Integrated, Cancelled, Unknown }

    public sealed class GcGrowLedger
    {
        public long Started { get; private set; }
        public long CancelledPending { get; private set; }
        public void Start() => Started++;
        public void CancelPending() => CancelledPending++;
        public GcGrowSample Sample(bool pending) => new GcGrowSample(pending, Started, CancelledPending);
    }

    public readonly struct GcGrowSample
    {
        public GcGrowSample(bool pending, long started, long cancelledPending) { Pending = pending; Started = started; CancelledPending = cancelledPending; }
        public bool Pending { get; }
        public long Started { get; }
        public long CancelledPending { get; }

        public static GcAsyncGrow Outcome(GcGrowSample before, GcGrowSample now)
        {
            long started = now.Started - before.Started, cancelled = now.CancelledPending - before.CancelledPending;
            if (started < 0 || cancelled < 0) return GcAsyncGrow.Unknown;
            long ended = (before.Pending ? 1 : 0) + started - (now.Pending ? 1 : 0);
            if (ended < 0) return GcAsyncGrow.Unknown;
            if (ended == 0) return cancelled != 0 ? GcAsyncGrow.Unknown : now.Pending ? GcAsyncGrow.Pending : GcAsyncGrow.None;
            if (cancelled == 0) return GcAsyncGrow.Integrated;
            return cancelled == ended ? GcAsyncGrow.Cancelled : GcAsyncGrow.Unknown;
        }
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
        private long frameNumber = -1, lastFrameNumber = -1, gen0Through = -1;

        private readonly float[] frameMs;
        private readonly float[] modMs;
        private readonly float[] lateMs;
        private readonly float[] frameCpuMs;
        private readonly bool[] collected;
        private readonly bool[] diagnostic;
        private int lastMarks;
        private double[] spreadScratch = Array.Empty<double>();
        private readonly long[] collections = new long[Generations];
        private int stored;
        private long heapBeforeSum, heapAfterSum, heapPairs;
        private long lastHeap = -1;
        private readonly int[] lastCounts = new int[Generations];
        private double lastStampMs, pendingModMs, pendingLateMs;
        private double pendingFrameCpuMs = -1, pendingModCpuMs = -1, closingFrameCpuMs = -1, closingModCpuMs = -1;
        private double closingBeginStampMs = -1, closingEndStampMs = -1;
        private long cpuFrames, offCpuFrames, offCpuCollectionFrames;
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
            frameMs = new float[capacity]; modMs = new float[capacity]; lateMs = new float[capacity]; frameCpuMs = new float[capacity]; collected = new bool[capacity]; diagnostic = new bool[capacity];
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
        public double MaxLateMs { get; private set; }
        public long DiagnosticFrameCount { get; private set; }
        public long DiagnosticCollectionFrames { get; private set; }
        public long Collections(int generation) => collections[generation];

        public string AllocationCounter { get; private set; } = "heap_used";

        public void Start(long heapBytes, string allocationCounter = "heap_used")
        {
            AllocationCounter = allocationCounter ?? "heap_used";
            nested = false; lastCounter = -1; counterFrameBytes = counterFrames = 0;
            Array.Clear(collections, 0, collections.Length);
            stored = 0; collectionRowCount = 0; previousFrameMs = -1; rowAwaitsNext = previousCollected = false; lastFrameNumber = -1; Frames = 0; CollectionFrames = 0; heapBeforeSum = heapAfterSum = heapPairs = 0;
            MaxFrameMs = MaxModMs = MaxLateMs = 0; collectionSpikes = frameSpikes = 0; DiagnosticFrameCount = DiagnosticCollectionFrames = 0; lastMarks = -1;
            frameAllocatedBytes = allocationFrames = 0;
            cpuFrames = offCpuFrames = offCpuCollectionFrames = 0; pendingFrameCpuMs = pendingModCpuMs = closingFrameCpuMs = closingModCpuMs = -1;
            FrameRecorder.Reset();
            Array.Clear(segmentBytes, 0, Segments); Array.Clear(segmentSamples, 0, Segments); Array.Clear(segmentSkipped, 0, Segments);
            HeapStart = HeapEnd = HeapPeak = lastHeap = heapBytes;
            baselined = false; pendingModMs = pendingLateMs = 0; ownedBodies = boundBodies = -1; growBefore = growNow = default; growSampled = false;
            instantiatedNow = destroyedNow = activatedNow = instantiatedBefore = destroyedBefore = activatedBefore = -1;
            asyncStartedNow = asyncCompletedNow = meshNow = asyncStartedBefore = asyncCompletedBefore = meshBefore = -1;
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
            => Observe(stampMilliseconds, gen0Total, gen1Total, gen2Total, heapBytes, unityFrame, -1, null);

        public void Observe(double stampMilliseconds, int gen0Total, int gen1Total, int gen2Total, long heapBytes, long unityFrame, DiagnosticMark mark)
            => Observe(stampMilliseconds, gen0Total, gen1Total, gen2Total, heapBytes, unityFrame, mark.Sequence, mark.Source);

        public void Observe(double stampMilliseconds, int gen0Total, int gen1Total, int gen2Total, long heapBytes, long unityFrame, int diagnosticMarks, string? diagnosticSource)
        {
            if (!Running) return;
            int marks = diagnosticMarks;
            if (baselined)
            {
                frameNumber = lastFrameNumber; gen0Through = gen0Total; closingFrameCpuMs = pendingFrameCpuMs; closingModCpuMs = pendingModCpuMs; closingBeginStampMs = lastStampMs; closingEndStampMs = stampMilliseconds;
                Frame(stampMilliseconds - lastStampMs, pendingModMs, pendingLateMs, gen0Total - lastCounts[0], gen1Total - lastCounts[1], gen2Total - lastCounts[2], heapBytes,
                    marks >= 0 && lastMarks >= 0 && marks != lastMarks ? (string.IsNullOrEmpty(diagnosticSource) ? "unnamed" : diagnosticSource) : null);
                frameNumber = -1; gen0Through = -1; closingFrameCpuMs = closingModCpuMs = closingBeginStampMs = closingEndStampMs = -1;
            }
            else if (heapBytes >= 0) lastHeap = heapBytes;
            lastMarks = marks; growBefore = growNow; growBeforeSampled = growSampled;
            instantiatedBefore = instantiatedNow; destroyedBefore = destroyedNow; activatedBefore = activatedNow;
            asyncStartedBefore = asyncStartedNow; asyncCompletedBefore = asyncCompletedNow; meshBefore = meshNow;
            lastFrameNumber = unityFrame;
            lastStampMs = stampMilliseconds; lastCounts[0] = gen0Total; lastCounts[1] = gen1Total; lastCounts[2] = gen2Total;
            pendingModMs = pendingLateMs = 0; pendingFrameCpuMs = pendingModCpuMs = -1; baselined = true;
        }

        public void ModWork(double milliseconds) { if (Running && baselined) pendingModMs = milliseconds; }

        public void FrameCpu(double milliseconds) { if (Running && baselined && milliseconds >= 0) pendingFrameCpuMs = milliseconds; }

        public void ModWorkCpu(double milliseconds) { if (Running && baselined && milliseconds >= 0) pendingModCpuMs = milliseconds; }

        public long CpuFrames => cpuFrames;
        public long OffCpuFrames => offCpuFrames;
        public long OffCpuCollectionFrames => offCpuCollectionFrames;

        public void LateModWork(double milliseconds) { if (Running && baselined && milliseconds > 0) pendingLateMs += milliseconds; }

        private int ownedBodies = -1, boundBodies = -1;

        private GcGrowSample growBefore, growNow;
        private bool growSampled, growBeforeSampled;

        public void NoteBodies(int owned, int bound) { ownedBodies = owned; boundBodies = bound; }

        private long instantiatedNow = -1, destroyedNow = -1, activatedNow = -1, instantiatedBefore = -1, destroyedBefore = -1, activatedBefore = -1;

        private long asyncStartedNow = -1, asyncCompletedNow = -1, meshNow = -1, asyncStartedBefore = -1, asyncCompletedBefore = -1, meshBefore = -1;

        public const string UnityWorkCounts = "requests", DestroyCompletion = "unavailable", AsyncInstantiateCompletion = "observed_when_done", MeshUploadCompletion = "unavailable";

        public void NoteUnityWork(long instantiated, long destroyed, long activated, long asyncStarted, long asyncCompleted, long meshRequests)
        {
            instantiatedNow = instantiated; destroyedNow = destroyed; activatedNow = activated;
            asyncStartedNow = asyncStarted; asyncCompletedNow = asyncCompleted; meshNow = meshRequests;
        }

        private static long Delta(long before, long now) => before < 0 || now < before ? -1 : now - before;

        public void NoteBodies(int owned, int bound, GcGrowSample grow) { ownedBodies = owned; boundBodies = bound; growNow = grow; growSampled = true; }

        private static string GrowName(GcAsyncGrow grow) => grow switch
        {
            GcAsyncGrow.Integrated => "integrated", GcAsyncGrow.Pending => "pending", GcAsyncGrow.Cancelled => "cancelled", GcAsyncGrow.Unknown => "unknown", _ => "none",
        };

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

        public void Rebaseline() { baselined = false; pendingModMs = pendingLateMs = 0; pendingFrameCpuMs = pendingModCpuMs = -1; lastCounter = -1; previousFrameMs = -1; previousCollected = rowAwaitsNext = false; }

        public void Frame(double frameMilliseconds, double modMilliseconds, int gen0, int gen1, int gen2, long heapBytes, string? diagnosticSource = null)
            => Frame(frameMilliseconds, modMilliseconds, 0, gen0, gen1, gen2, heapBytes, diagnosticSource);

        public void Frame(double frameMilliseconds, double modMilliseconds, double lateMilliseconds, int gen0, int gen1, int gen2, long heapBytes, string? diagnosticSource = null)
        {
            if (!Running) return;
            bool gc = gen0 > 0 || gen1 > 0 || gen2 > 0;
            bool tooling = !string.IsNullOrEmpty(diagnosticSource);
            if (tooling) { DiagnosticFrameCount++; if (gc) DiagnosticCollectionFrames++; }
            collections[0] += Math.Max(0, gen0); collections[1] += Math.Max(0, gen1); collections[2] += Math.Max(0, gen2);
            Frames++;
            if (frameMilliseconds > MaxFrameMs) MaxFrameMs = frameMilliseconds;
            if (modMilliseconds > MaxModMs) MaxModMs = modMilliseconds;
            if (lateMilliseconds > MaxLateMs) MaxLateMs = lateMilliseconds;
            if (rowAwaitsNext) { collectionRows[collectionRowCount - 1].NextFrameMs = gc ? -1 : frameMilliseconds; rowAwaitsNext = false; }
            if (gc)
            {
                CollectionFrames++;
                if (lastHeap >= 0 && heapBytes >= 0) { heapBeforeSum += lastHeap; heapAfterSum += heapBytes; heapPairs++; }
                if (collectionRowCount < CollectionRowCapacity)
                {
                    collectionRows[collectionRowCount++] = new GcCollectionRow { Frame = frameNumber, FrameMs = frameMilliseconds, ModMs = modMilliseconds, LateMs = lateMilliseconds,
                        Gen0 = gen0, Gen1 = gen1, Gen2 = gen2, HeapBefore = lastHeap, HeapAfter = heapBytes, PreviousFrameMs = previousCollected ? -1 : previousFrameMs, NextFrameMs = -1,
                        OwnedBodies = ownedBodies, BoundBodies = boundBodies, Diagnostic = diagnosticSource ?? "",
                        AsyncGrow = growBeforeSampled && growSampled ? GcGrowSample.Outcome(growBefore, growNow) : GcAsyncGrow.Unknown, Gen0Through = gen0Through,
                        FrameCpuMs = closingFrameCpuMs, ModCpuMs = closingModCpuMs, BeginStampMs = closingBeginStampMs, EndStampMs = closingEndStampMs,
                        Instantiated = Delta(instantiatedBefore, instantiatedNow), Destroyed = Delta(destroyedBefore, destroyedNow), Activated = Delta(activatedBefore, activatedNow),
                        AsyncStarted = Delta(asyncStartedBefore, asyncStartedNow), AsyncCompleted = Delta(asyncCompletedBefore, asyncCompletedNow), MeshRequests = Delta(meshBefore, meshNow) };
                    rowAwaitsNext = true;
                }
            }
            else if (lastHeap >= 0 && heapBytes >= lastHeap) { frameAllocatedBytes += heapBytes - lastHeap; allocationFrames++; }
            previousFrameMs = frameMilliseconds; previousCollected = gc;
            if (heapBytes > HeapPeak) HeapPeak = heapBytes;
            if (heapBytes >= 0) { lastHeap = heapBytes; HeapEnd = heapBytes; }
            if (frameMilliseconds >= SpikeMilliseconds) { frameSpikes++; if (gc) collectionSpikes++; }
            if (closingFrameCpuMs >= 0)
            {
                cpuFrames++;
                if (frameMilliseconds - closingFrameCpuMs >= SpikeMilliseconds) { offCpuFrames++; if (gc) offCpuCollectionFrames++; }
            }
            if (stored == frameMs.Length) return;
            frameMs[stored] = (float)frameMilliseconds; modMs[stored] = (float)modMilliseconds; lateMs[stored] = (float)lateMilliseconds;
            frameCpuMs[stored] = (float)closingFrameCpuMs;
            collected[stored] = gc; diagnostic[stored] = tooling;
            stored++;
        }

        public GcSpread FrameSpread(bool withCollection) => Spread(frameMs, withCollection, 0);
        public GcSpread ModSpread(bool withCollection) => Spread(modMs, withCollection, 0);
        public GcSpread LateSpread(bool withCollection) => Spread(lateMs, withCollection, 0);
        public GcSpread ModWorkSpread(bool withCollection) => Spread(modMs, withCollection, 0, null, false, false, lateMs);
        public GcSpread PauseSpread() => Spread(frameMs, true, FrameSpread(false).P50);
        public GcSpread PauseWithoutModSpread() => Spread(frameMs, true, FrameSpread(false).P50, modMs);
        public GcSpread PauseWithoutModWorkSpread() => Spread(frameMs, true, FrameSpread(false).P50, modMs, false, false, null, lateMs);
        public GcSpread PauseIncludingDiagnosticSpread() => Spread(frameMs, true, FrameSpread(false).P50, null, true);
        public GcSpread DiagnosticFrameSpread() => Spread(frameMs, null, 0, null, true, true);
        public GcSpread FrameCpuSpread(bool withCollection) => CpuSpread(withCollection, false, 0);
        public GcSpread OffCpuSpread(bool withCollection) => CpuSpread(withCollection, true, 0);
        public GcSpread PauseCpuSpread() => CpuSpread(true, false, FrameCpuSpread(false).P50);

        private GcSpread CpuSpread(bool withCollection, bool offCpu, double baseline)
        {
            int count = 0;
            for (int i = 0; i < stored; i++) if (frameCpuMs[i] >= 0 && Counted(i, withCollection, false, false)) count++;
            if (count == 0) return new GcSpread(0, 0, 0, 0);
            if (spreadScratch.Length < count) spreadScratch = new double[stored];
            var values = spreadScratch;
            int at = 0;
            for (int i = 0; i < stored; i++)
                if (frameCpuMs[i] >= 0 && Counted(i, withCollection, false, false))
                    values[at++] = Math.Max(0, (offCpu ? frameMs[i] - frameCpuMs[i] : frameCpuMs[i]) - baseline);
            Array.Sort(values, 0, count);
            return new GcSpread(count, Rank(values, count, .50), Rank(values, count, .95), values[count - 1]);
        }
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
                if (row.PreviousFrameMs < 0 || row.NextFrameMs < 0 || !string.IsNullOrEmpty(row.Diagnostic)) continue;
                neighbourScratch[count++] = Math.Max(0, row.FrameMs - 0.5 * (row.PreviousFrameMs + row.NextFrameMs));
            }
            if (count == 0) return new GcSpread(0, 0, 0, 0);
            Array.Sort(neighbourScratch, 0, count);
            return new GcSpread(count, Rank(neighbourScratch, count, .50), Rank(neighbourScratch, count, .95), neighbourScratch[count - 1]);
        }

        public int FrameSpikes() => frameSpikes;

        public long HeapBeforeCollection => heapPairs == 0 ? -1 : heapBeforeSum / heapPairs;
        public long HeapAfterCollection => heapPairs == 0 ? -1 : heapAfterSum / heapPairs;

        private GcSpread Spread(float[] source, bool? withCollection, double baseline, float[]? less = null, bool includeDiagnostic = false, bool onlyDiagnostic = false,
            float[]? plus = null, float[]? lessToo = null)
        {
            int count = 0;
            for (int i = 0; i < stored; i++) if (Counted(i, withCollection, includeDiagnostic, onlyDiagnostic)) count++;
            if (count == 0) return new GcSpread(0, 0, 0, 0);
            if (spreadScratch.Length < count) spreadScratch = new double[stored];
            var values = spreadScratch;
            int at = 0;
            for (int i = 0; i < stored; i++) if (Counted(i, withCollection, includeDiagnostic, onlyDiagnostic)) values[at++] = Math.Max(0, source[i] + (plus == null ? 0 : plus[i]) - baseline - (less == null ? 0 : less[i]) - (lessToo == null ? 0 : lessToo[i]));
            Array.Sort(values, 0, count);
            return new GcSpread(count, Rank(values, count, .50), Rank(values, count, .95), values[count - 1]);
        }

        private bool Counted(int i, bool? withCollection, bool includeDiagnostic, bool onlyDiagnostic)
            => (withCollection == null || collected[i] == withCollection.Value) && (onlyDiagnostic ? diagnostic[i] : includeDiagnostic || !diagnostic[i]);

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
            S(text, "pause_ms_without_mod_work", PauseWithoutModWorkSpread());
            S(text, "late_ms_gc", LateSpread(true));
            S(text, "late_ms_plain", LateSpread(false));
            S(text, "mod_work_ms_gc", ModWorkSpread(true));
            S(text, "mod_work_ms_plain", ModWorkSpread(false));
            text.Append(",\"max_late_ms\":").Append(D(MaxLateMs));
            text.Append(",\"collection_rows_retained\":").Append(N(collectionRowCount))
                .Append(",\"collection_rows_truncated\":").Append(CollectionRowsTruncated ? "true" : "false")
                .Append(",\"mod_unity_work\":{\"counts\":\"").Append(UnityWorkCounts).Append("\",\"destroy_completion\":\"").Append(DestroyCompletion)
                .Append("\",\"async_instantiate_completion\":\"").Append(AsyncInstantiateCompletion).Append("\",\"mesh_upload_completion\":\"").Append(MeshUploadCompletion).Append("\"}")
                .Append(",\"collection_rows\":[");
            for (int i = 0; i < collectionRowCount; i++)
            {
                var r = collectionRows[i];
                if (i > 0) text.Append(',');
                text.Append("{\"frame\":").Append(N(r.Frame)).Append(",\"frame_ms\":").Append(D(r.FrameMs)).Append(",\"mod_ms\":").Append(D(r.ModMs)).Append(",\"late_ms\":").Append(D(r.LateMs))
                    .Append(",\"gen0\":").Append(N(r.Gen0)).Append(",\"gen1\":").Append(N(r.Gen1)).Append(",\"gen2\":").Append(N(r.Gen2))
                    .Append(",\"heap_before_bytes\":").Append(N(r.HeapBefore)).Append(",\"heap_after_bytes\":").Append(N(r.HeapAfter))
                    .Append(",\"previous_frame_ms\":").Append(D(r.PreviousFrameMs)).Append(",\"next_frame_ms\":").Append(D(r.NextFrameMs))
                    .Append(",\"owned_bodies\":").Append(N(r.OwnedBodies)).Append(",\"bound_bodies\":").Append(N(r.BoundBodies))
                    .Append(",\"diagnostic\":\"").Append(Label(r.Diagnostic)).Append("\",\"async_grow\":\"").Append(GrowName(r.AsyncGrow)).Append("\",\"gen0_through\":").Append(N(r.Gen0Through))
                    .Append(",\"frame_cpu_ms\":").Append(r.FrameCpuMs < 0 ? "null" : D(r.FrameCpuMs)).Append(",\"mod_cpu_ms\":").Append(r.ModCpuMs < 0 ? "null" : D(r.ModCpuMs))
                    .Append(",\"off_cpu_ms\":").Append(r.FrameCpuMs < 0 ? "null" : D(Math.Max(0, r.FrameMs - r.FrameCpuMs)))
                    .Append(",\"begin_stamp_ms\":").Append(r.BeginStampMs < 0 ? "null" : D(r.BeginStampMs)).Append(",\"end_stamp_ms\":").Append(r.EndStampMs < 0 ? "null" : D(r.EndStampMs))
                    .Append(",\"mod_instantiate_requests\":").Append(r.Instantiated < 0 ? "null" : N(r.Instantiated)).Append(",\"mod_destroy_requests\":").Append(r.Destroyed < 0 ? "null" : N(r.Destroyed))
                    .Append(",\"mod_activate_requests\":").Append(r.Activated < 0 ? "null" : N(r.Activated))
                    .Append(",\"mod_async_instantiate_starts\":").Append(r.AsyncStarted < 0 ? "null" : N(r.AsyncStarted))
                    .Append(",\"mod_async_instantiate_completions\":").Append(r.AsyncCompleted < 0 ? "null" : N(r.AsyncCompleted))
                    .Append(",\"mod_mesh_requests\":").Append(r.MeshRequests < 0 ? "null" : N(r.MeshRequests)).Append('}');
            }
            text.Append(']');
            S(text, "pause_ms_over_neighbours", PauseOverNeighboursSpread());
            text.Append(",\"diagnostic_frames\":").Append(N(DiagnosticFrameCount)).Append(",\"diagnostic_collection_frames\":").Append(N(DiagnosticCollectionFrames));
            S(text, "pause_ms_including_diagnostic", PauseIncludingDiagnosticSpread());
            S(text, "diagnostic_frame_ms", DiagnosticFrameSpread());
            text.Append(",\"cpu_frames\":").Append(N(cpuFrames)).Append(",\"off_cpu_frames_30ms\":").Append(N(offCpuFrames))
                .Append(",\"off_cpu_collection_frames_30ms\":").Append(N(offCpuCollectionFrames));
            S(text, "frame_cpu_ms_gc", FrameCpuSpread(true));
            S(text, "frame_cpu_ms_plain", FrameCpuSpread(false));
            S(text, "off_cpu_ms_gc", OffCpuSpread(true));
            S(text, "off_cpu_ms_plain", OffCpuSpread(false));
            S(text, "pause_cpu_ms", PauseCpuSpread());
            text.Append('}');
            return text.ToString();
        }

        private static string Label(string? source)
        {
            if (string.IsNullOrEmpty(source)) return "";
            var text = new StringBuilder(source!.Length);
            foreach (char c in source) text.Append(c == '"' || c == '\\' || c < ' ' ? '_' : c);
            return text.ToString();
        }

        public string ReportHuman(GcReportContext context)
        {
            var pause = PauseSpread();
            var outside = PauseWithoutModSpread();
            var outsideWork = PauseWithoutModWorkSpread();
            var neighbours = PauseOverNeighboursSpread();
            string M(long bytes) => bytes < 0 ? "n/a" : (bytes / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture) + " MB";
            string D(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);
            return "[Persistent City] gc measurement (diagnostic only): persons=" + (context.Residents + context.Visitors).ToString(CultureInfo.InvariantCulture)
                + " frames=" + Frames.ToString(CultureInfo.InvariantCulture) + " collections gen0/1/2=" + collections[0].ToString(CultureInfo.InvariantCulture)
                + "/" + collections[1].ToString(CultureInfo.InvariantCulture) + "/" + collections[2].ToString(CultureInfo.InvariantCulture)
                + " pause p50/p95/max=" + D(pause.P50) + "/" + D(pause.P95) + "/" + D(pause.Max) + " ms (outside UpdateCity p95/max=" + D(outside.P95) + "/" + D(outside.Max) + " ms, outside UpdateCity and LateUpdate p95/max=" + D(outsideWork.P95) + "/" + D(outsideWork.Max) + " ms, over neighbouring frames p95/max=" + D(neighbours.P95) + "/" + D(neighbours.Max) + " ms), gc frames >= 30 ms=" + CollectionSpikes().ToString(CultureInfo.InvariantCulture)
                + ", heap " + M(HeapStart) + " -> " + M(HeapEnd) + " (peak " + M(HeapPeak) + ")"
                + ", diagnostic frames " + DiagnosticFrameCount.ToString(CultureInfo.InvariantCulture) + " (" + DiagnosticCollectionFrames.ToString(CultureInfo.InvariantCulture)
                + " with a collection, left out of the pause; pause p95/max with them " + D(PauseIncludingDiagnosticSpread().P95) + "/" + D(PauseIncludingDiagnosticSpread().Max) + " ms)"
                + (cpuFrames == 0 ? ", main-thread cpu unavailable" : ", main thread off cpu >= 30 ms in " + offCpuFrames.ToString(CultureInfo.InvariantCulture) + " frames ("
                    + offCpuCollectionFrames.ToString(CultureInfo.InvariantCulture) + " with a collection), pause by main-thread cpu p95/max=" + D(PauseCpuSpread().P95) + "/" + D(PauseCpuSpread().Max) + " ms");
        }
    }
}
