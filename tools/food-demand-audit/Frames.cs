using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Text;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using PersistentCity.Core;
using PersistentCity.Runtime;
using Unity.Profiling;
using UnityEngine;

namespace FoodDemandAuditProbe
{
    public sealed partial class Probe
    {
        private const int FrameSampleLimit = FrameObservationLog.DefaultCapacity;
        private const string FrameOwner = "persistentcity.neighborhood-frame-audit";
        private static Probe? frameOwner;
        private bool recordingFrames;
        private readonly DiagnosticDispatchLog diagnosticDispatches = new DiagnosticDispatchLog(1024);
        private long totalPopulationCalls, totalAuthorityCalls, totalHourChangeCalls;
        private int excludedLifecycleModFrames;
        private long firstFrame;
        private FrameObservationLog? observations;
        private object? recordingSave;
        private string observationEvidence = "", stopReason = "not-started", sampleError = "";
        private int activeReportCount, lastActiveReportUnityFrame;
        private readonly List<int> activeReportUnityFrames = new List<int>();
        private string recordingSaveName = "";
        private bool replacedUnexportedCapture;
        private long droppedModFrames;
        private long initialManagedBytes;
        private int[] initialCollections = new int[3];
        private ProfilerRecorder allocations;
        private bool allocationCounter;
        private readonly List<double> frameTimes = new List<double>();
        private readonly List<double> runtimeTimes = new List<double>();
        private readonly List<double> allocatedBytes = new List<double>();
        private readonly List<double> mainThreadTimes = new List<double>();
        private readonly List<double> gpuTimes = new List<double>();
        private readonly FrameTiming[] timing = new FrameTiming[1];
        private int peakPedestrians, peakCars;
        private double startNativeMinute, stoppedNativeMinute, lastObservedNativeMinute;
        private long stoppedFrame, terminationObservedTicks;
        private int firstUnityFrame, currentWorkFrame = -1, measurementDepth, timedExceptions, crossFrameCalls;
        private long outerMeasurementStart, observedNativeHour = -1;
        private bool authorityHookAvailable;
        private string perFrameEvidence = "";
        private readonly List<double> authorityCallTimes = new List<double>();
        private readonly List<double> hourChangeCallTimes = new List<double>();
        private readonly List<ModWorkFrame> modWorkFrames = new List<ModWorkFrame>();
        private ModWorkFrame workFrame;
        private readonly Dictionary<MethodBase, int> componentMethods = new Dictionary<MethodBase, int>();
        private readonly List<ComponentTiming> componentTimings = new List<ComponentTiming>();
        private int componentTimedExceptions, componentCrossFrameCalls;
        private sealed class ComponentTiming
        {
            public string Name = "";
            public long Calls, OutsidePrimaryScopeCalls, TotalTicks;
            public readonly List<double> CallTimes = new List<double>();
        }
        private struct ModWorkFrame
        {
            public int UnityFrame, PopulationCalls, AuthorityCalls, HourChangeCalls;
            public long PopulationTicks, AuthorityTicks, CombinedTicks;
            public ComponentSlots Components;
            public EventWork SlowestEvent;
            public int EventSlicesDropped;
            public ManagedDelta Managed;
            public long FirstObservedTicks, ClosedAtTicks;
            public int NextObservedUnityFrame;
        }
        private struct ComponentMeasurement
        {
            public Probe? Owner;
            public long Start;
            public int UnityFrame, Index;
        }
        private struct FrameMeasurement
        {
            public Probe? Owner;
            public long Start;
            public int UnityFrame;
            public bool Authority, HourChanged;
        }
        private static double Milliseconds(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
        private object StartFrames()
        {
            bool replacing = firstFrame != 0 && observationEvidence.Length == 0;
            StopFrames(); observations = null; firstFrame = 0; replacedUnexportedCapture = replacing; frameTimes.Clear(); runtimeTimes.Clear(); allocatedBytes.Clear(); mainThreadTimes.Clear(); gpuTimes.Clear();
            authorityCallTimes.Clear(); hourChangeCallTimes.Clear(); modWorkFrames.Clear();
            componentMethods.Clear(); componentTimings.Clear(); componentTimedExceptions = componentCrossFrameCalls = 0;
            measurementDepth = timedExceptions = crossFrameCalls = 0; observedNativeHour = -1;
            stoppedFrame = terminationObservedTicks = 0; stoppedNativeMinute = 0; perFrameEvidence = observationEvidence = sampleError = "";
            activeReportCount = lastActiveReportUnityFrame = stoppedMeasurementDepth = 0; activeReportUnityFrames.Clear(); droppedModFrames = 0; stopReason = "recording";
            firstUnityFrame = currentWorkFrame = Time.frameCount;
            workFrame = new ModWorkFrame { UnityFrame = currentWorkFrame };
            peakPedestrians = peakCars = excludedLifecycleModFrames = 0;
            totalPopulationCalls = totalAuthorityCalls = totalHourChangeCalls = 0; diagnosticDispatches.Clear();
            foreach (var samples in new[] { frameTimes, runtimeTimes, allocatedBytes, mainThreadTimes, gpuTimes, authorityCallTimes, hourChangeCallTimes }) samples.Capacity = FrameSampleLimit;
            modWorkFrames.Capacity = FrameSampleLimit; activeReportUnityFrames.Capacity = 1024;
            try { allocations = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1); allocationCounter = allocations.Valid; }
            catch { allocationCounter = false; }
            frameOwner = this;
            try
            {
                var patches = new Harmony(FrameOwner);
                patches.Patch(AccessTools.Method(typeof(PopulationRuntime), "Update"),
                    prefix: new HarmonyMethod(typeof(Probe), nameof(RuntimeFrameBefore)) { priority = Priority.First },
                    finalizer: new HarmonyMethod(typeof(Probe), nameof(RuntimeFrameFinally)) { priority = Priority.Last });
                var ownerType = typeof(PopulationRuntime).Assembly.GetType("PersistentCity.Runtime.NativeFoodOwnership");
                authorityHookAvailable = ownerType != null;
                if (ownerType != null)
                {
                    var hook = AccessTools.Method(ownerType, "AfterNativeTick")
                        ?? throw new InvalidOperationException("Native authority timing seam changed; refuse incomplete attribution.");
                    patches.Patch(hook,
                        prefix: new HarmonyMethod(typeof(Probe), nameof(AuthorityFrameBefore)) { priority = Priority.First },
                        finalizer: new HarmonyMethod(typeof(Probe), nameof(AuthorityFrameFinally)) { priority = Priority.Last });
                }
                PatchComponent(patches, "PersistentCity.Runtime.RouteObjectDiscovery", "Step", typeof(bool));
                PatchComponent(patches, "PersistentCity.Runtime.PhysicalPopulation", "BeforeAdvance");
                PatchComponent(patches, "PersistentCity.Runtime.PhysicalPopulation", "Update");
                PatchComponent(patches, "PersistentCity.Runtime.PopulationRuntime", "AdvancePopulation", typeof(double));
                PatchComponent(patches, "PersistentCity.Runtime.MultimodalRoutes", "Prepare");
                PatchComponent(patches, "PersistentCity.Core.CitySimulator", "RefreshTrafficClosures");
                PatchComponent(patches, "PersistentCity.Runtime.PhysicalVehicles", "Update");
                PatchComponent(patches, "PersistentCity.Runtime.PhysicalTrafficHooks", "FixedStepPrefix", typeof(bool).MakeByRefType());
                PatchComponent(patches, "PersistentCity.Runtime.PhysicalTrafficHooks", "FixedStepPostfix", typeof(bool));
                PatchComponent(patches, "PersistentCity.Runtime.PhysicalTrafficHooks", "FixedStepFinalizer", typeof(Exception), typeof(bool));
                PatchComponent(patches, "PersistentCity.Runtime.PhysicalVehicles", "DeferNativeForce", typeof(Rigidbody), typeof(Vector3), typeof(ForceMode));
                PatchComponent(patches, "PersistentCity.Runtime.PhysicalVehicles", "ApplyNativeRotation", typeof(Rigidbody), typeof(Quaternion));
                PatchComponent(patches, "PersistentCity.Runtime.PhysicalIntersections", "UpdatePostfix", typeof(GleyTrafficSystem.IntersectionManager));
                StartEventMeasurements(patches);
                foreach (var component in componentTimings) component.CallTimes.Capacity = FrameSampleLimit;
                workFrame = NewWorkFrame(currentWorkFrame);
                startNativeMinute = SaveGameManager.Current.Day * 1440.0 + SaveGameManager.Current.Hour * 60 + SaveGameManager.Current.Minute;
                lastObservedNativeMinute = startNativeMinute;
                observedNativeHour = SaveGameManager.Current.Day * 24L + SaveGameManager.Current.Hour;
                observations = new FrameObservationLog(FrameObservationLog.DefaultCapacity, 0, Time.frameCount, 0, 0, 0);
                initialManagedBytes = GC.GetTotalMemory(false);
                for (int i = 0; i < 3; i++) initialCollections[i] = GC.CollectionCount(i);
                firstFrame = Stopwatch.GetTimestamp();
                observations.ResetSeed(firstFrame, Time.frameCount, GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
                recordingSave = SaveGameManager.Current; recordingSaveName = SaveGameManager.Current.SaveGameName; recordingFrames = true;
                RecordDiagnosticDispatch("frames-start");
            }
            catch { stopReason = "start-failure"; StopFrames(); observations = null; firstFrame = 0; throw; }
            return new { started = true, allocationCounter, authorityHookAvailable,
                components = componentTimings.Select(c => c.Name).ToArray(), environment = FrameEnvironment() };
        }
        private void PatchComponent(Harmony patches, string typeName, string methodName, params Type[] parameters)
        {
            var type = typeof(PopulationRuntime).Assembly.GetType(typeName) ?? typeof(CitySimulator).Assembly.GetType(typeName)
                ?? throw new InvalidOperationException("Component timing type unavailable: " + typeName);
            var method = AccessTools.Method(type, methodName, parameters)
                ?? throw new InvalidOperationException("Component timing method unavailable: " + typeName + "." + methodName);
            if (componentTimings.Count >= ComponentSlots.Capacity) throw new InvalidOperationException("Component timing capacity exceeded.");
            componentMethods.Add(method, componentTimings.Count);
            componentTimings.Add(new ComponentTiming { Name = type.Name + "." + methodName });
            patches.Patch(method,
                prefix: new HarmonyMethod(typeof(ComponentPatches), nameof(ComponentPatches.Before)) { priority = Priority.First },
                finalizer: new HarmonyMethod(typeof(ComponentPatches), nameof(ComponentPatches.Finally)) { priority = Priority.Last });
        }
        // Harmony keys __state by the patch method's declaring type, not its owner ID.
        // ClockAudit also patches AdvancePopulation; keep its long state independent.
        private static class ComponentPatches
        {
            public static void Before(MethodBase __originalMethod, out ComponentMeasurement __state)
                => ComponentBefore(__originalMethod, out __state);
            public static Exception? Finally(Exception? __exception, ComponentMeasurement __state)
                => ComponentFinally(__exception, __state);
        }
        private static void ComponentBefore(MethodBase __originalMethod, out ComponentMeasurement __state)
        {
            __state = default;
            var owner = frameOwner;
            if (owner?.recordingFrames != true || !owner.componentMethods.TryGetValue(__originalMethod, out int index)) return;
            int frame = Time.frameCount;
            owner.ObserveWorkFrame(frame);
            var component = owner.componentTimings[index];
            component.Calls++;
            if (owner.measurementDepth == 0) component.OutsidePrimaryScopeCalls++;
            __state = new ComponentMeasurement { Owner = owner, Index = index, UnityFrame = frame, Start = Stopwatch.GetTimestamp() };
        }
        private static Exception? ComponentFinally(Exception? __exception, ComponentMeasurement __state)
        {
            long end = Stopwatch.GetTimestamp();
            var owner = __state.Owner;
            if (__state.Start == 0 || owner?.recordingFrames != true) return __exception;
            long ticks = Math.Max(0, end - __state.Start);
            var component = owner.componentTimings[__state.Index];
            component.TotalTicks += ticks;
            if (component.CallTimes.Count < FrameSampleLimit) component.CallTimes.Add(Milliseconds(ticks));
            if (__exception != null) owner.componentTimedExceptions++;
            if (__state.UnityFrame != Time.frameCount || __state.UnityFrame != owner.currentWorkFrame) owner.componentCrossFrameCalls++;
            else
            {
                owner.workFrame.Components.Add(__state.Index, ticks);
            }
            return __exception;
        }
        private ModWorkFrame NewWorkFrame(int frame) => new ModWorkFrame { UnityFrame = frame, FirstObservedTicks = Stopwatch.GetTimestamp() };
        private static void RuntimeFrameBefore(out FrameMeasurement __state) => __state = BeginMeasurement(false);
        private static void AuthorityFrameBefore(out FrameMeasurement __state) => __state = BeginMeasurement(true);
        private static FrameMeasurement BeginMeasurement(bool authority)
        {
            var owner = frameOwner;
            if (owner?.recordingFrames != true) return default;
            int frame = Time.frameCount;
            owner.ObserveWorkFrame(frame);
            bool hourChanged = false;
            if (authority && SaveGameManager.Current != null)
            {
                long hour = SaveGameManager.Current.Day * 24L + SaveGameManager.Current.Hour;
                hourChanged = owner.observedNativeHour >= 0 && hour != owner.observedNativeHour;
                owner.observedNativeHour = hour;
            }
            long start = Stopwatch.GetTimestamp();
            if (owner.measurementDepth++ == 0) owner.outerMeasurementStart = start;
            return new FrameMeasurement { Owner = owner, Start = start, UnityFrame = frame, Authority = authority, HourChanged = hourChanged };
        }
        private static Exception? RuntimeFrameFinally(Exception? __exception, FrameMeasurement __state)
        { EndMeasurement(__state, __exception != null); return __exception; }
        private static Exception? AuthorityFrameFinally(Exception? __exception, FrameMeasurement __state)
        { EndMeasurement(__state, __exception != null); return __exception; }
        private static void EndMeasurement(FrameMeasurement state, bool failed)
        {
            long end = Stopwatch.GetTimestamp();
            var owner = state.Owner;
            if (state.Start == 0 || owner?.recordingFrames != true) return;
            long ticks = Math.Max(0, end - state.Start);
            if (failed) owner.timedExceptions++;
            if (state.Authority)
            {
                owner.totalAuthorityCalls++; if (state.HourChanged) owner.totalHourChangeCalls++;
                if (owner.authorityCallTimes.Count < FrameSampleLimit) owner.authorityCallTimes.Add(Milliseconds(ticks));
                if (state.HourChanged && owner.hourChangeCallTimes.Count < FrameSampleLimit) owner.hourChangeCallTimes.Add(Milliseconds(ticks));
            }
            else { owner.totalPopulationCalls++; if (owner.runtimeTimes.Count < FrameSampleLimit) owner.runtimeTimes.Add(Milliseconds(ticks)); }
            bool sameFrame = state.UnityFrame == Time.frameCount && state.UnityFrame == owner.currentWorkFrame;
            if (!sameFrame) owner.crossFrameCalls++;
            if (sameFrame)
            {
                if (state.Authority)
                { owner.workFrame.AuthorityTicks += ticks; owner.workFrame.AuthorityCalls++; if (state.HourChanged) owner.workFrame.HourChangeCalls++; }
                else { owner.workFrame.PopulationTicks += ticks; owner.workFrame.PopulationCalls++; }
            }
            if (--owner.measurementDepth == 0 && sameFrame)
                owner.workFrame.CombinedTicks += Math.Max(0, end - owner.outerMeasurementStart);
        }
        private void ObserveWorkFrame(int frame)
        {
            if (frame == currentWorkFrame) return;
            long managedEnd = ReadManagedCounter();
            workFrame.Managed = ManagedDelta.Between(managedStart, managedEnd, managedCounterKind == "thread-allocated-bytes");
            workFrame.SlowestEvent = eventWork.Slowest; workFrame.EventSlicesDropped = eventWork.Dropped;
            if (currentWorkFrame > firstUnityFrame && currentWorkFrame < frame)
            {
                workFrame.ClosedAtTicks = Stopwatch.GetTimestamp(); workFrame.NextObservedUnityFrame = frame;
                if (modWorkFrames.Count < FrameSampleLimit) modWorkFrames.Add(workFrame); else droppedModFrames++;
            }
            currentWorkFrame = frame;
            workFrame = NewWorkFrame(frame);
            eventWork.Clear(); managedStart = ReadManagedCounter();
        }
        // Called even when the request guard fails, so a load/save transition cannot bridge captures.
        private void RecordDiagnosticDispatch(string operation)
        { if (recordingFrames) diagnosticDispatches.Add(operation, Time.frameCount, Stopwatch.GetTimestamp()); }
        private void SampleFrameSafely(bool allowed)
        {
            if (!recordingFrames) return;
            if (!allowed || !ReferenceEquals(recordingSave, SaveGameManager.Current) || recordingSaveName != SaveGameManager.Current?.SaveGameName)
            { stopReason = "save-or-guard-changed"; StopFrames(); return; }
            try { SampleFrame(); }
            catch (Exception error) { sampleError = error.ToString(); stopReason = "sampler-error"; StopFrames(); }
        }
        private void SampleFrame()
        {
            if (!recordingFrames) return;
            ObserveWorkFrame(Time.frameCount);
            if (frameTimes.Count >= FrameSampleLimit) { stopReason = "aggregate-sample-limit"; StopFrames(); return; }
            var row = new FrameObservation { TimeScale = Time.timeScale };
            row.AllocationAvailable = allocationCounter && allocations.Valid && allocations.Count > 0;
            if (row.AllocationAvailable) { row.AllocationBytes = allocations.LastValue; row.AllocationAvailable = row.AllocationBytes >= 0; }
            if (FrameTimingManager.GetLatestTimings(1, timing) > 0)
            {
                row.CpuLatestMs = timing[0].cpuFrameTime; row.GpuLatestMs = timing[0].gpuFrameTime;
                row.CpuAvailable = !double.IsNaN(row.CpuLatestMs) && !double.IsInfinity(row.CpuLatestMs) && row.CpuLatestMs >= 0;
                row.GpuAvailable = !double.IsNaN(row.GpuLatestMs) && !double.IsInfinity(row.GpuLatestMs) && row.GpuLatestMs >= 0;
                row.TimingSourceTimestamp = timing[0].frameStartTimestamp;
            }
            var save = SaveGameManager.Current;
            if (save != null) { row.NativeMinute = save.Day * 1440.0 + save.Hour * 60 + save.Minute; row.NativeMinuteAvailable = true; lastObservedNativeMinute = row.NativeMinute; }
            long start = observations!.LastTicks, now = Stopwatch.GetTimestamp();
            if (!observations.Add(now, Time.frameCount, GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2), row)) return;
            frameTimes.Add(Milliseconds(now - start));
            if (row.AllocationAvailable) allocatedBytes.Add(row.AllocationBytes);
            if (row.CpuAvailable && row.CpuLatestMs > 0) mainThreadTimes.Add(row.CpuLatestMs);
            if (row.GpuAvailable && row.GpuLatestMs > 0) gpuTimes.Add(row.GpuLatestMs);
            FrameTimingManager.CaptureFrameTimings();
        }
        private static object Distribution(List<double> samples)
        {
            var values = samples.OrderBy(n => n).ToArray();
            double Quantile(double q) => values.Length == 0 ? 0 : values[Math.Min(values.Length - 1, (int)(values.Length * q))];
            return new { samples = values.Length, mean = values.Length == 0 ? 0 : values.Average(), p50 = Quantile(.5), p95 = Quantile(.95),
                p99 = Quantile(.99), maximum = values.Length == 0 ? 0 : values[values.Length - 1] };
        }
        private static object Coordinates(Vector3 value) => new { value.x, value.y, value.z };
        private object FrameEnvironment() => new { width = Screen.width, height = Screen.height,
            quality = QualitySettings.names[QualitySettings.GetQualityLevel()], vSync = QualitySettings.vSyncCount,
            focused = Application.isFocused, runInBackground = Application.runInBackground,
            targetFps = Application.targetFrameRate, calendarFactor = typeof(PopulationRuntime).Assembly.GetType("PersistentCity.Runtime.NativeCityClock")?.GetProperty("Factor")?.GetValue(null), gpu = SystemInfo.graphicsDeviceName, cpu = SystemInfo.processorType,
            graphicsApi = SystemInfo.graphicsDeviceType.ToString(), timeScale = Time.timeScale, camera = Camera.main == null ? null : new { position = Coordinates(Camera.main.transform.position), eulerAngles = Coordinates(Camera.main.transform.eulerAngles) } };
        private object FrameReport()
        {
            if (recordingFrames) { activeReportCount++; lastActiveReportUnityFrame = Time.frameCount; if (activeReportUnityFrames.Count < 1024) activeReportUnityFrames.Add(Time.frameCount); }
            var runtime = UnityEngine.Object.FindObjectOfType<PopulationRuntime>();
            var sim = runtime == null ? null : (CitySimulator?)typeof(PopulationRuntime).GetField("simulation", Fields)!.GetValue(runtime);
            var physical = runtime == null ? null : typeof(PopulationRuntime).GetField("physical", Fields)!.GetValue(runtime);
            var text = physical?.GetType().GetProperty("Diagnostics")?.GetValue(physical)?.ToString();
            if (text != null) {
                var p = System.Text.RegularExpressions.Regex.Match(text, @"pedestrians=(\d+), cars=(\d+)");
                if (p.Success) { peakPedestrians = Math.Max(peakPedestrians, int.Parse(p.Groups[1].Value)); peakCars = Math.Max(peakCars, int.Parse(p.Groups[2].Value)); }
            }
            if (recordingFrames) ObserveWorkFrame(Time.frameCount);
            double nativeMinute = stoppedFrame > 0 ? stoppedNativeMinute : (SaveGameManager.Current == null ? startNativeMinute : SaveGameManager.Current.Day * 1440.0 + SaveGameManager.Current.Hour * 60 + SaveGameManager.Current.Minute);
            return new { captureStartedTicks = firstFrame == 0 ? (long?)null : firstFrame, captureStoppedTicks = stoppedFrame == 0 ? (long?)null : stoppedFrame, realSeconds = firstFrame == 0 ? 0 : ((stoppedFrame > 0 ? stoppedFrame : Stopwatch.GetTimestamp()) - firstFrame) / (double)Stopwatch.Frequency,
                nativeGameMinutes = nativeMinute - startNativeMinute, citizens = sim?.State.Citizens.Count,
                populationLagMinutes = sim == null ? (double?)null : nativeMinute - sim.State.Minute,
                physical = text, peakObservedPedestrians = peakPedestrians, peakObservedCars = peakCars,
                frameMilliseconds = Distribution(frameTimes), totalPopulationUpdateMilliseconds = Distribution(runtimeTimes),
                authorityHookAvailable, authorityHookObserved = authorityCallTimes.Count > 0, populationUpdateCalls = runtimeTimes.Count,
                authorityHookCalls = authorityCallTimes.Count, authorityHookMilliseconds = Distribution(authorityCallTimes),
                authorityHookTotalMilliseconds = authorityCallTimes.Sum(),
                observedNativeHourChangeCalls = hourChangeCallTimes.Count, observedNativeHourChangeMilliseconds = Distribution(hourChangeCallTimes),
                completedUnityFrames = modWorkFrames.Count,
                populationUpdatePerUnityFrameMilliseconds = Distribution(modWorkFrames.Select(f => Milliseconds(f.PopulationTicks)).ToList()),
                authorityHookPerUnityFrameMilliseconds = Distribution(modWorkFrames.Select(f => Milliseconds(f.AuthorityTicks)).ToList()),
                combinedModWorkPerUnityFrameMilliseconds = Distribution(modWorkFrames.Select(f => Milliseconds(f.CombinedTicks)).ToList()),
                combinedFramesOver5ms = modWorkFrames.Count(f => Milliseconds(f.CombinedTicks) > 5),
                totalPopulationCalls, totalAuthorityCalls, totalHourChangeCalls,
                populationCallSamplesDropped = totalPopulationCalls - runtimeTimes.Count, authorityCallSamplesDropped = totalAuthorityCalls - authorityCallTimes.Count, hourChangeSamplesDropped = totalHourChangeCalls - hourChangeCallTimes.Count,
                diagnosticDispatchCount = diagnosticDispatches.Total, diagnosticDispatchDropped = diagnosticDispatches.Dropped, diagnosticDispatches = diagnosticDispatches.Snapshot(), excludedLifecycleModFrames,
                timedExceptions, crossFrameCalls, perFrameEvidence, observationEvidence, stopReason, sampleError,
                activeReportCount, lastActiveReportUnityFrame, activeReportUnityFrames = activeReportUnityFrames.ToArray(), activeReportIdsDropped = Math.Max(0, activeReportCount - activeReportUnityFrames.Count), droppedModFrames, replacedUnexportedCapture,
                rawObservations = observations == null ? null : new { observations.Count, observations.Capacity, observations.MarshaledStorageEstimateBytes, observations.Dropped, observations.Duplicates, observations.InvalidOrder, observations.SkippedUnityFrames, stopwatchFrequency = Stopwatch.Frequency, terminationObservedTicks, discardedLifecycleTailTicks = terminationObservedTicks > stoppedFrame ? terminationObservedTicks - stoppedFrame : 0, unobservedTailTicks = stoppedFrame > 0 ? Math.Max(0, stoppedFrame - observations.LastTicks) : 0, initialIntervalPartial = true, currentModFrameExcluded = true, outstandingPrimaryScopes = stoppedMeasurementDepth },
                componentTimedExceptions, componentCrossFrameCalls, eventCrossFrameCalls, eventMeasurementFailures, eventMeasurementsFailed, managedCounterKind,
                eventMeasurementNote = "A nonzero eventMeasurementFailures disables event/query capture until a new recording; partial event scratch is discarded, earlier rows remain but the event capture is incomplete. Other frame capture continues. Slowest event sums this frame's RunEvent/ContinuePlanning slices by citizen, simulation minute and entry branch. Branch names describe entry state, not every internal Step branch. Food steps and traffic advances are separate calls with blank citizen IDs. Native query fields time managed WalkingRoutes.Calculate (sampling, path, geometry) and RoadPlan.Continue (A-star slices plus completion), navMeshPathMs separately differences the existing native CalculatePath timer; aStarAdvanceMs times actual RoadSearch.Advance including its callbacks/result construction. These nested fields must not be added. Plans count MultimodalRoutes Plan/Begin calls in the measured scope. Timings exclude time suspended between frames. Allocation delta spans consecutive first observed work points, includes other main-thread work and diagnostic allocation, and is not mod-attributed. Heap fallback is signed live-heap change, not allocated bytes. Initial/current partial frames are excluded; frame gaps must be checked. Hook components time mod hook bodies, not the full native methods; force/rotation verdicts exclude native Rigidbody writes. No automatic performance pass.",
                components = componentTimings.Select((c, index) => new { name = c.Name, calls = c.Calls,
                    outsidePrimaryScopeCalls = c.OutsidePrimaryScopeCalls, callSamplesDropped = c.Calls - c.CallTimes.Count, totalInclusiveMilliseconds = Milliseconds(c.TotalTicks),
                    inclusiveCallMilliseconds = Distribution(c.CallTimes),
                    inclusivePerUnityFrameMilliseconds = Distribution(modWorkFrames.Select(f => Milliseconds(f.Components.Ticks(index))).ToList()) }).ToArray(),
                unityCpuFrameMilliseconds = Distribution(mainThreadTimes), unityGpuFrameMilliseconds = Distribution(gpuTimes),
                allocationCounterAvailable = allocationCounter, allocationCounterObservedPositive = allocatedBytes.Any(n => n > 0), allocatedBytesPerFrame = Distribution(allocatedBytes),
                managedBytesBefore = initialManagedBytes, managedBytesAfter = GC.GetTotalMemory(false),
                collections = Enumerable.Range(0, 3).Select(i => GC.CollectionCount(i) - initialCollections[i]).ToArray(),
                framesOver50ms = frameTimes.Count(n => n > 50), framesOver100ms = frameTimes.Count(n => n > 100), framesOver250ms = frameTimes.Count(n => n > 250),
                environment = FrameEnvironment(), note = "Whole-frame wall intervals include native rendering, other mods and diagnostics. Population Update includes route preparation and physical work. Authority-hook wall time includes native work it force-drains, and includes no-op hook calls. Observed native-hour-change calls are clock transitions, not actual ownership transfers. Combined mod work is the union of timed Update/authority scopes, so nested scopes count once; separate component totals must not be added when nested. Per-frame distributions exclude initial and current partial Unity frames and include zero-work completed frames. Instrumentation overhead remains; crossFrameCalls or timedExceptions invalidate clean attribution. No hour-change sample means no hourly-boundary performance proof. Named component timings are inclusive call and completed-frame totals; nested components (PhysicalVehicles.Update inside PhysicalPopulation.Update, for example) must not be added. Components do not extend or double-count the original Update/authority union. Outside-primary-scope component calls are reported separately, and nonzero componentCrossFrameCalls or componentTimedExceptions invalidate clean component attribution. Per-call and completed-frame samples cap at65536; total call counts remain uncapped. Fixed thirteen-slot value storage replaces per-frame component arrays; timing patches and existing sample lists still add diagnostic overhead. Raw observations are bounded and excess rows are counted as dropped. CPU/GPU availability means a finite nonnegative value was returned, not that Unity measured it: returned zero may be unmeasured. CPU/GPU raw values are latest asynchronous observations with no wall-interval mapping; historical CPU/GPU distributions retain their positive-only filter. Active status/report calls allocate and contaminate the capture; their count and last Unity frame are reported. Raw allocation values are latest recorder observations, not attributed mod allocations. Timestamp bounds are observations, not engine frame boundaries. Paused means timeScale equals zero. Other mod entry points are outside this attribution. Body peaks sampled only on frame-status calls. GPU/CPU counters absent when unsupported. GC recorder is whole game, not attributed solely to this mod; an all-zero recorder without positive observations does not establish zero allocations." };
        }
        private object StopAndReportFrames()
        {
            if (recordingFrames) stopReason = "frames-stop";
            StopFrames();
            string observationPath = Path.Combine(Root, "frames-observations-" + pendingId + ".csv");
            using (var writer = new StreamWriter(observationPath)) observations?.WriteCsv(writer);
            string modPath = Path.Combine(Root, "frames-mod-work-" + pendingId + ".csv");
            var csv = new StringBuilder("unityFrame,populationCalls,authorityCalls,observedHourChangeCalls,populationMs,authorityMs,combinedUnionMs,firstObservedTicks,closedAtTicks,nextObservedUnityFrame");
            foreach (var component in componentTimings) csv.Append(',').Append(component.Name).Append("Calls,").Append(component.Name).Append("InclusiveMs");
            csv.Append(",slowestCitizen,entryBranch,eventMinute,eventSlices,planCalls,eventMs,walkingCalculationMs,roadSearchSliceMs,navMeshPathMs,aStarAdvanceMs,eventFailures,eventSlicesDropped,managedDeltaBytes,managedDeltaAvailable,managedCounterKind");
            csv.Append('\n');
            foreach (var f in modWorkFrames)
            {
                csv.Append(f.UnityFrame).Append(',').Append(f.PopulationCalls).Append(',').Append(f.AuthorityCalls).Append(',')
                    .Append(f.HourChangeCalls).Append(',').Append(Milliseconds(f.PopulationTicks).ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(Milliseconds(f.AuthorityTicks).ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(Milliseconds(f.CombinedTicks).ToString("R", CultureInfo.InvariantCulture)).Append(',').Append(f.FirstObservedTicks).Append(',').Append(f.ClosedAtTicks).Append(',').Append(f.NextObservedUnityFrame);
                for (int i = 0; i < componentTimings.Count; i++)
                    csv.Append(',').Append(f.Components.Calls(i)).Append(',').Append(Milliseconds(f.Components.Ticks(i)).ToString("R", CultureInfo.InvariantCulture));
                AppendEventCsv(csv, f);
                csv.Append('\n');
            }
            File.WriteAllText(modPath, csv.ToString());
            observationEvidence = observationPath; perFrameEvidence = modPath;
            return FrameReport();
        }
        private int stoppedMeasurementDepth;
        private void StopFrames()
        {
            if (recordingFrames)
            {
                terminationObservedTicks = Stopwatch.GetTimestamp();
                bool interrupted = stopReason == "save-or-guard-changed";
                if (!interrupted) ObserveWorkFrame(Time.frameCount);
                stoppedFrame = interrupted && observations != null ? observations.LastTicks : terminationObservedTicks;
                if (interrupted)
                {
                    while (modWorkFrames.Count > 0 && modWorkFrames[modWorkFrames.Count - 1].ClosedAtTicks > stoppedFrame)
                    { modWorkFrames.RemoveAt(modWorkFrames.Count - 1); excludedLifecycleModFrames++; }
                }
                stoppedNativeMinute = lastObservedNativeMinute;
                if (!interrupted && SaveGameManager.Current != null && ReferenceEquals(recordingSave, SaveGameManager.Current) && recordingSaveName == SaveGameManager.Current.SaveGameName)
                    stoppedNativeMinute = SaveGameManager.Current.Day * 1440.0 + SaveGameManager.Current.Hour * 60 + SaveGameManager.Current.Minute;
            }
            if (recordingFrames) { stoppedMeasurementDepth = measurementDepth; if (stopReason == "recording") stopReason = "lifecycle-stop"; }
            recordingFrames = false; measurementDepth = 0; recordingSave = null;
            if (ReferenceEquals(frameOwner, this)) { new Harmony(FrameOwner).UnpatchAll(FrameOwner); frameOwner = null; }
            if (allocations.Valid) allocations.Dispose();
        }
        private object ClockOnlyBaseline()
        {
            var clock = typeof(PopulationRuntime).Assembly.GetType("PersistentCity.Runtime.NativeCityClock")!;
            var runtime = UnityEngine.Object.FindObjectOfType<PopulationRuntime>();
            if (runtime != null && typeof(PopulationRuntime).GetField("simulation", Fields)!.GetValue(runtime) != null)
                throw new InvalidOperationException("Clock-only baseline requires the persistent population to be disabled.");
            if (!(bool)clock.GetProperty("Active")!.GetValue(null)!) {
                clock.GetMethod("Stop")!.Invoke(null, null);
                clock.GetMethod("Start", new[] { typeof(object), typeof(double) })!.Invoke(null, new object[] { SaveGameManager.Current, 1.0 / 3 });
            }
            return new { factor = clock.GetProperty("Factor")!.GetValue(null), note = "Same calendar policy for the native-population performance control; no persistent population or food ownership enabled." };
        }
        private object PanelScreenshot()
        {
            typeof(PopulationRuntime).GetField("showEconomy", Fields)!.SetValue(Runtime, true);
            string path = Path.Combine(Root, "neighborhood-management.png"); ScreenCapture.CaptureScreenshot(path);
            return new { path, note = "Capture completes after this rendered frame." };
        }
    }
}
