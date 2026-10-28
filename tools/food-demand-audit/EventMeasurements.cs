using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using PersistentCity.Core;
using PersistentCity.Runtime;
using UnityEngine;

namespace FoodDemandAuditProbe
{
    public sealed partial class Probe
    {
        private readonly EventWorkAccumulator eventWork = new EventWorkAccumulator(512);
        private EventScope? activeEvent;
        private long eventCrossFrameCalls;
        private long eventSequence;
        private bool eventMeasurementsFailed;
        private long eventMeasurementFailures;
        private PropertyInfo? foodNextMinute;
        private PropertyInfo? walkingMetrics, navMeshMilliseconds;
        private Func<long>? managedCounter;
        private string managedCounterKind = "unavailable";
        private long managedStart = -1;
        private FieldInfo? pendingField, pendingItemField, dueCitizenField;
        private sealed class EventScope
        {
            public EventWork Work;
            public long Start;
            public int Frame;
        }
        private struct QueryScope
        {
            public EventScope? Event;
            public long Start;
            public bool Road;
            public bool AStar;
            public object? Metrics;
            public double NavMeshBefore;
        }
        private void StartEventMeasurements(Harmony patches)
        {
            eventWork.Clear(); activeEvent = null; eventCrossFrameCalls = eventSequence = eventMeasurementFailures = 0; eventMeasurementsFailed = false;
            pendingField = AccessTools.Field(typeof(CitySimulator), "pending");
            var pendingType = typeof(CitySimulator).GetNestedType("PendingEvent", BindingFlags.NonPublic);
            var dueType = typeof(CitySimulator).GetNestedType("Due", BindingFlags.NonPublic);
            pendingItemField = pendingType == null ? null : AccessTools.Field(pendingType, "Item");
            dueCitizenField = dueType == null ? null : AccessTools.Field(dueType, "Citizen");
            if (pendingField == null || pendingItemField == null || dueCitizenField == null)
                throw new InvalidOperationException("Event timing contract changed.");
            PatchEvent(patches, typeof(CitySimulator), "RunEvent");
            PatchEvent(patches, typeof(CitySimulator), "ContinuePlanning");
            var foodType = typeof(CitySimulator).Assembly.GetType("PersistentCity.Core.FoodCommerce")!;
            foodNextMinute = AccessTools.Property(foodType, "NextMinute") ?? throw new InvalidOperationException("Food timing contract changed.");
            PatchEvent(patches, foodType, "Step");
            PatchEvent(patches, typeof(TrafficKernel), "AdvanceTo");
            var routes = typeof(PopulationRuntime).Assembly.GetType("PersistentCity.Runtime.MultimodalRoutes")!;
            foreach (var method in routes.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                if (method.Name == "Plan" || method.Name == "Begin")
                    patches.Patch(method, prefix: new HarmonyMethod(typeof(EventPatches), nameof(EventPatches.Plan)));
            // Time the actual managed walking calculation wrapper, not a Harmony patch of an extern Unity call.
            var walking = typeof(PopulationRuntime).Assembly.GetType("PersistentCity.Runtime.WalkingRoutes")!;
            walkingMetrics = AccessTools.Property(walking, "Metrics") ?? throw new InvalidOperationException("Walking metrics unavailable.");
            navMeshMilliseconds = AccessTools.Property(walkingMetrics.PropertyType, "NavMeshPathMilliseconds")
                ?? throw new InvalidOperationException("NavMesh timing unavailable.");
            PatchQuery(patches, walking, "Calculate");
            var road = typeof(PopulationRuntime).Assembly.GetType("PersistentCity.Runtime.RoadRoutes+RoadPlan")!;
            PatchQuery(patches, road, "Continue");
            PatchQuery(patches, typeof(TransportRoadGraph.RoadSearch), "Advance");
            managedCounter = null; managedCounterKind = "unavailable";
            try
            {
                var method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", Type.EmptyTypes);
                if (method != null)
                {
                    var read = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method);
                    long before = read(); var probe = new byte[128]; long after = read(); GC.KeepAlive(probe);
                    if (before >= 0 && after > before) { managedCounter = read; managedCounterKind = "thread-allocated-bytes"; }
                }
            }
            catch { /* Mono may expose a counter which is not implemented. */ }
            if (managedCounter == null)
            {
                var method = typeof(UnityEngine.Profiling.Profiler).GetMethod("GetMonoUsedSizeLong", Type.EmptyTypes);
                if (method != null)
                {
                    managedCounter = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method);
                    managedCounterKind = "mono-used-heap-bytes";
                }
            }
            managedStart = ReadManagedCounter();
        }
        private long ReadManagedCounter()
        {
            try { return managedCounter?.Invoke() ?? -1; }
            catch { managedCounter = null; return -1; }
        }
        private static void PatchEvent(Harmony patches, Type type, string name)
        {
            var method = AccessTools.Method(type, name) ?? throw new InvalidOperationException("Missing event seam: " + name);
            patches.Patch(method, prefix: new HarmonyMethod(typeof(EventPatches), nameof(EventPatches.Before)),
                finalizer: new HarmonyMethod(typeof(EventPatches), nameof(EventPatches.Finally)));
        }
        private static void PatchQuery(Harmony patches, Type type, string name)
        {
            var method = AccessTools.Method(type, name) ?? throw new InvalidOperationException("Missing query seam: " + name);
            patches.Patch(method, prefix: new HarmonyMethod(typeof(QueryPatches), nameof(QueryPatches.Before)),
                finalizer: new HarmonyMethod(typeof(QueryPatches), nameof(QueryPatches.Finally)));
        }
        // No Unity, reflection or logging here: a broken observer must not interrupt the city.
        private void DisableEventMeasurements()
        {
            eventMeasurementsFailed = true;
            eventMeasurementFailures++;
            activeEvent = null;
            eventWork.Clear(); // Discard the partial event image rather than export it as complete.
        }

        private static class EventPatches
        {
            public static void Plan() { var owner = frameOwner; if (owner?.eventMeasurementsFailed == false && owner.activeEvent != null) owner.activeEvent.Work.Plans++; }
            public static void Before(object __instance, MethodBase __originalMethod, object[] __args, out EventScope? __state)
            {
                var owner = frameOwner;
                __state = null;
                try
                {
                    if (owner?.recordingFrames != true || owner.eventMeasurementsFailed || owner.activeEvent != null) return;
                    owner.ObserveWorkFrame(Time.frameCount);
                    CitizenState? citizen = null;
                    double minute;
                    string branch;
                    if (__instance is CitySimulator simulator)
                    {
                        object? item = __originalMethod.Name == "RunEvent" ? __args[0]
                            : owner.pendingField!.GetValue(simulator) is object pending ? owner.pendingItemField!.GetValue(pending) : null;
                        if (item == null) return;
                        citizen = (CitizenState)owner.dueCitizenField!.GetValue(item)!;
                        minute = simulator.State.Minute;
                        // Entry branch, not a claim that no later branch ran within this event.
                        branch = citizen.MealOuting != null ? "meal-outing" : citizen.Phase == CitizenPhase.Travelling ? "arrival"
                            : citizen.Phase == CitizenPhase.WaitingForOpening ? "opening-wait"
                            : citizen.Phase == CitizenPhase.WaitingForRoute ? "route-wait" : "routine";
                    }
                    else
                    {
                        minute = __instance is TrafficKernel traffic ? traffic.Clock : (double)owner.foodNextMinute!.GetValue(__instance)!;
                        branch = __instance is TrafficKernel ? "traffic-advance" : "food-step";
                    }
                    __state = new EventScope { Frame = Time.frameCount, Start = Stopwatch.GetTimestamp(),
                        Work = new EventWork { Citizen = citizen?.Identity.Id ?? "", Branch = branch, Minute = minute, Slices = 1,
                            Sequence = citizen == null ? ++owner.eventSequence : 0 } };
                    owner.activeEvent = __state;
                }
                catch (Exception)
                {
                    owner?.DisableEventMeasurements();
                    __state = null;
                }
            }
            public static Exception? Finally(Exception? __exception, EventScope? __state)
            {
                var owner = frameOwner;
                try
                {
                    if (__state == null || owner == null) return __exception;
                    owner.activeEvent = null;
                    if (!owner.recordingFrames || owner.eventMeasurementsFailed) return __exception;
                    __state.Work.Ticks = Math.Max(0, Stopwatch.GetTimestamp() - __state.Start);
                    if (__exception != null) __state.Work.Failures++;
                    if (__state.Frame != Time.frameCount || __state.Frame != owner.currentWorkFrame) owner.eventCrossFrameCalls++;
                    else owner.eventWork.Add(__state.Work);
                    return __exception;
                }
                catch (Exception)
                {
                    owner?.DisableEventMeasurements();
                    return __exception;
                }
            }
        }
        private static class QueryPatches
        {
            public static void Before(object __instance, MethodBase __originalMethod, out QueryScope __state)
            {
                var owner = frameOwner;
                __state = default;
                try
                {
                    var evt = owner?.activeEvent;
                    if (evt == null || owner!.eventMeasurementsFailed) return;
                    object? metrics = __originalMethod.Name == "Calculate" ? owner!.walkingMetrics!.GetValue(__instance) : null;
                    __state = new QueryScope { Event = evt, Start = Stopwatch.GetTimestamp(), Road = __originalMethod.Name == "Continue",
                        AStar = __originalMethod.Name == "Advance", Metrics = metrics,
                        NavMeshBefore = metrics == null ? 0 : (double)owner!.navMeshMilliseconds!.GetValue(metrics)! };
                }
                catch (Exception)
                {
                    owner?.DisableEventMeasurements();
                    __state = default;
                }
            }
            public static Exception? Finally(Exception? __exception, QueryScope __state)
            {
                var owner = frameOwner;
                try
                {
                    if (__state.Event != null && owner?.eventMeasurementsFailed == false)
                    {
                        long elapsed = Math.Max(0, Stopwatch.GetTimestamp() - __state.Start);
                        if (__state.AStar) __state.Event.Work.AStarTicks += elapsed;
                        else if (__state.Road) __state.Event.Work.RoadSearchTicks += elapsed;
                        else __state.Event.Work.NavMeshTicks += elapsed;
                        if (__state.Metrics != null)
                            __state.Event.Work.NavMeshPathMilliseconds += Math.Max(0,
                                (double)owner.navMeshMilliseconds!.GetValue(__state.Metrics)! - __state.NavMeshBefore);
                    }
                    return __exception;
                }
                catch (Exception)
                {
                    owner?.DisableEventMeasurements();
                    return __exception;
                }
            }
        }
        private static string CsvText(string? value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
        private void AppendEventCsv(StringBuilder csv, ModWorkFrame frame)
        {
            var e = frame.SlowestEvent;
            csv.Append(',').Append(CsvText(e.Citizen)).Append(',').Append(CsvText(e.Branch)).Append(',')
                .Append(e.Minute.ToString("R", CultureInfo.InvariantCulture)).Append(',').Append(e.Slices).Append(',').Append(e.Plans)
                .Append(',').Append(Milliseconds(e.Ticks).ToString("R", CultureInfo.InvariantCulture))
                .Append(',').Append(Milliseconds(e.NavMeshTicks).ToString("R", CultureInfo.InvariantCulture))
                .Append(',').Append(Milliseconds(e.RoadSearchTicks).ToString("R", CultureInfo.InvariantCulture))
                .Append(',').Append(e.NavMeshPathMilliseconds.ToString("R", CultureInfo.InvariantCulture))
                .Append(',').Append(Milliseconds(e.AStarTicks).ToString("R", CultureInfo.InvariantCulture))
                .Append(',').Append(e.Failures).Append(',').Append(frame.EventSlicesDropped)
                .Append(',').Append(frame.Managed.Available ? frame.Managed.Value.ToString(CultureInfo.InvariantCulture) : "")
                .Append(',').Append(frame.Managed.Available ? 1 : 0).Append(',').Append(managedCounterKind);
        }
    }
}
