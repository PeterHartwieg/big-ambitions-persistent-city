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
        private const string FrameOwner = "persistentcity.neighborhood-frame-audit";
        private static Probe? frameOwner;
        private bool recordingFrames;
        private long previousFrame, firstFrame;
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
        private double startNativeMinute, stoppedNativeMinute;
        private long stoppedFrame;
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
            public long[]? ComponentTicks;
            public int[]? ComponentCalls;
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
            StopFrames(); frameTimes.Clear(); runtimeTimes.Clear(); allocatedBytes.Clear(); mainThreadTimes.Clear(); gpuTimes.Clear();
            authorityCallTimes.Clear(); hourChangeCallTimes.Clear(); modWorkFrames.Clear();
            componentMethods.Clear(); componentTimings.Clear(); componentTimedExceptions = componentCrossFrameCalls = 0;
            measurementDepth = timedExceptions = crossFrameCalls = 0; observedNativeHour = -1;
            stoppedFrame = 0; stoppedNativeMinute = 0; perFrameEvidence = "";
            firstUnityFrame = currentWorkFrame = Time.frameCount;
            workFrame = new ModWorkFrame { UnityFrame = currentWorkFrame };
            peakPedestrians = peakCars = 0;
            initialManagedBytes = GC.GetTotalMemory(false);
            for (int i = 0; i < 3; i++) initialCollections[i] = GC.CollectionCount(i);
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
                workFrame = NewWorkFrame(currentWorkFrame);
                startNativeMinute = SaveGameManager.Current.Day * 1440.0 + SaveGameManager.Current.Hour * 60 + SaveGameManager.Current.Minute;
                observedNativeHour = SaveGameManager.Current.Day * 24L + SaveGameManager.Current.Hour;
                firstFrame = previousFrame = Stopwatch.GetTimestamp(); recordingFrames = true;
            }
            catch { StopFrames(); throw; }
            return new { started = true, allocationCounter, authorityHookAvailable,
                components = componentTimings.Select(c => c.Name).ToArray(), environment = FrameEnvironment() };
        }
        private void PatchComponent(Harmony patches, string typeName, string methodName, params Type[] parameters)
        {
            var type = typeof(PopulationRuntime).Assembly.GetType(typeName) ?? typeof(CitySimulator).Assembly.GetType(typeName)
                ?? throw new InvalidOperationException("Component timing type unavailable: " + typeName);
            var method = AccessTools.Method(type, methodName, parameters)
                ?? throw new InvalidOperationException("Component timing method unavailable: " + typeName + "." + methodName);
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
            if (component.CallTimes.Count < 600000) component.CallTimes.Add(Milliseconds(ticks));
            if (__exception != null) owner.componentTimedExceptions++;
            if (__state.UnityFrame != Time.frameCount || __state.UnityFrame != owner.currentWorkFrame) owner.componentCrossFrameCalls++;
            else
            {
                owner.workFrame.ComponentTicks![__state.Index] += ticks;
                owner.workFrame.ComponentCalls![__state.Index]++;
            }
            return __exception;
        }
        private ModWorkFrame NewWorkFrame(int frame) => new ModWorkFrame { UnityFrame = frame,
            ComponentTicks = new long[componentTimings.Count], ComponentCalls = new int[componentTimings.Count] };
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
                if (owner.authorityCallTimes.Count < 600000) owner.authorityCallTimes.Add(Milliseconds(ticks));
                if (state.HourChanged && owner.hourChangeCallTimes.Count < 600000) owner.hourChangeCallTimes.Add(Milliseconds(ticks));
            }
            else if (owner.runtimeTimes.Count < 600000) owner.runtimeTimes.Add(Milliseconds(ticks));
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
            if (currentWorkFrame > firstUnityFrame && currentWorkFrame < frame && modWorkFrames.Count < 600000)
                modWorkFrames.Add(workFrame);
            currentWorkFrame = frame;
            workFrame = NewWorkFrame(frame);
        }
        private void SampleFrame()
        {
            if (!recordingFrames) return;
            ObserveWorkFrame(Time.frameCount);
            if (frameTimes.Count >= 600000) { StopFrames(); return; }
            long now = Stopwatch.GetTimestamp(); frameTimes.Add((now - previousFrame) * 1000.0 / Stopwatch.Frequency); previousFrame = now;
            if (allocationCounter) allocatedBytes.Add(allocations.LastValue);
            if (FrameTimingManager.GetLatestTimings(1, timing) > 0)
            {
                if (timing[0].cpuFrameTime > 0) mainThreadTimes.Add(timing[0].cpuFrameTime);
                if (timing[0].gpuFrameTime > 0) gpuTimes.Add(timing[0].gpuFrameTime);
            }
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
            var runtime = UnityEngine.Object.FindObjectOfType<PopulationRuntime>();
            var sim = runtime == null ? null : (CitySimulator?)typeof(PopulationRuntime).GetField("simulation", Fields)!.GetValue(runtime);
            var physical = runtime == null ? null : typeof(PopulationRuntime).GetField("physical", Fields)!.GetValue(runtime);
            var text = physical?.GetType().GetProperty("Diagnostics")?.GetValue(physical)?.ToString();
            if (text != null) {
                var p = System.Text.RegularExpressions.Regex.Match(text, @"pedestrians=(\d+), cars=(\d+)");
                if (p.Success) { peakPedestrians = Math.Max(peakPedestrians, int.Parse(p.Groups[1].Value)); peakCars = Math.Max(peakCars, int.Parse(p.Groups[2].Value)); }
            }
            if (recordingFrames) ObserveWorkFrame(Time.frameCount);
            double nativeMinute = stoppedFrame > 0 ? stoppedNativeMinute : SaveGameManager.Current.Day * 1440.0 + SaveGameManager.Current.Hour * 60 + SaveGameManager.Current.Minute;
            return new { realSeconds = ((stoppedFrame > 0 ? stoppedFrame : Stopwatch.GetTimestamp()) - firstFrame) / (double)Stopwatch.Frequency,
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
                timedExceptions, crossFrameCalls, perFrameEvidence,
                componentTimedExceptions, componentCrossFrameCalls,
                components = componentTimings.Select((c, index) => new { name = c.Name, calls = c.Calls,
                    outsidePrimaryScopeCalls = c.OutsidePrimaryScopeCalls, totalInclusiveMilliseconds = Milliseconds(c.TotalTicks),
                    inclusiveCallMilliseconds = Distribution(c.CallTimes),
                    inclusivePerUnityFrameMilliseconds = Distribution(modWorkFrames.Select(f => Milliseconds(f.ComponentTicks![index])).ToList()) }).ToArray(),
                unityCpuFrameMilliseconds = Distribution(mainThreadTimes), unityGpuFrameMilliseconds = Distribution(gpuTimes),
                allocationCounterAvailable = allocationCounter, allocationCounterObservedPositive = allocatedBytes.Any(n => n > 0), allocatedBytesPerFrame = Distribution(allocatedBytes),
                managedBytesBefore = initialManagedBytes, managedBytesAfter = GC.GetTotalMemory(false),
                collections = Enumerable.Range(0, 3).Select(i => GC.CollectionCount(i) - initialCollections[i]).ToArray(),
                framesOver50ms = frameTimes.Count(n => n > 50), framesOver100ms = frameTimes.Count(n => n > 100), framesOver250ms = frameTimes.Count(n => n > 250),
                environment = FrameEnvironment(), note = "Whole-frame wall intervals include native rendering, other mods and diagnostics. Population Update includes route preparation and physical work. Authority-hook wall time includes native work it force-drains, and includes no-op hook calls. Observed native-hour-change calls are clock transitions, not actual ownership transfers. Combined mod work is the union of timed Update/authority scopes, so nested scopes count once; separate component totals must not be added when nested. Per-frame distributions exclude initial and current partial Unity frames and include zero-work completed frames. Instrumentation overhead remains; crossFrameCalls or timedExceptions invalidate clean attribution. No hour-change sample means no hourly-boundary performance proof. Named component timings are inclusive call and completed-frame totals; nested components (PhysicalVehicles.Update inside PhysicalPopulation.Update, for example) must not be added. Components do not extend or double-count the original Update/authority union. Outside-primary-scope component calls are reported separately, and nonzero componentCrossFrameCalls or componentTimedExceptions invalidate clean component attribution. Per-call samples cap at600000; total call counts remain uncapped. Per-frame component arrays and timing patches add diagnostic allocation/work overhead. Other mod entry points are outside this attribution. Body peaks sampled only on frame-status calls. GPU/CPU counters absent when unsupported. GC recorder is whole game, not attributed solely to this mod; an all-zero recorder without positive observations does not establish zero allocations." };
        }
        private object StopAndReportFrames()
        {
            StopFrames();
            perFrameEvidence = Path.Combine(Root, "frames-mod-work-" + pendingId + ".csv");
            var csv = new StringBuilder("unityFrame,populationCalls,authorityCalls,observedHourChangeCalls,populationMs,authorityMs,combinedUnionMs");
            foreach (var component in componentTimings) csv.Append(',').Append(component.Name).Append("Calls,").Append(component.Name).Append("InclusiveMs");
            csv.Append('\n');
            foreach (var f in modWorkFrames)
            {
                csv.Append(f.UnityFrame).Append(',').Append(f.PopulationCalls).Append(',').Append(f.AuthorityCalls).Append(',')
                    .Append(f.HourChangeCalls).Append(',').Append(Milliseconds(f.PopulationTicks).ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(Milliseconds(f.AuthorityTicks).ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(Milliseconds(f.CombinedTicks).ToString("R", CultureInfo.InvariantCulture));
                for (int i = 0; i < componentTimings.Count; i++)
                    csv.Append(',').Append(f.ComponentCalls![i]).Append(',').Append(Milliseconds(f.ComponentTicks![i]).ToString("R", CultureInfo.InvariantCulture));
                csv.Append('\n');
            }
            File.WriteAllText(perFrameEvidence, csv.ToString());
            return FrameReport();
        }
        private void StopFrames()
        {
            if (recordingFrames)
            {
                ObserveWorkFrame(Time.frameCount);
                stoppedFrame = Stopwatch.GetTimestamp();
                if (SaveGameManager.Current != null)
                    stoppedNativeMinute = SaveGameManager.Current.Day * 1440.0 + SaveGameManager.Current.Hour * 60 + SaveGameManager.Current.Minute;
            }
            recordingFrames = false; measurementDepth = 0;
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
