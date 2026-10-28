using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using PersistentCity.Core;

namespace UnityEngine
{
    public static class Time
    {
        private static int frame;
        public static int Reads, FailAtRead = -1;
        public static int frameCount
        {
            get { if (++Reads == FailAtRead) throw new InvalidOperationException("injected observer clock fault"); return frame; }
            set { frame = value; }
        }
    }
}
namespace UnityEngine.Profiling { public static class Profiler { public static long GetMonoUsedSizeLong() => GC.GetTotalMemory(false); } }
namespace PersistentCity.Runtime
{
    public class PopulationRuntime { }
    public class WalkingRoutes
    {
        public NativeRouteMetrics Metrics { get; } = new NativeRouteMetrics();
        [MethodImpl(MethodImplOptions.NoInlining)] public void Calculate() { Metrics.Add(.25); }
    }
    public class NativeRouteMetrics
    {
        private double value;
        public static int Reads, FailAtRead = -1;
        public double NavMeshPathMilliseconds
        {
            get { if (++Reads == FailAtRead) throw new InvalidOperationException("injected observer metric fault"); return value; }
        }
        public void Add(double amount) { value += amount; }
    }
    public class RoadRoutes
    {
        public class RoadPlan
        {
            [MethodImpl(MethodImplOptions.NoInlining)] public bool Continue(long deadline)
            {
                var graph = new TransportRoadGraph(new[] {
                    new TransportRoadNode { Position = new Position(0, 0, 0), Successors = new[] { 1 }, AllowsCar = true, KilometresPerHour = 30 },
                    new TransportRoadNode { Position = new Position(50, 0, 0), AllowsCar = true, KilometresPerHour = 30 } });
                graph.Plan(0, 1, _ => false, (_, _) => true, 100, null);
                return true;
            }
        }
    }
    public class MultimodalRoutes : IResumableRoutePlanner
    {
        public bool FailNext;
        [MethodImpl(MethodImplOptions.NoInlining)] public Route? Plan(Place from, Place to, double at, TravelPreferences preferences)
            => Answer(new RouteRequest(from, to, at, null, preferences));
        [MethodImpl(MethodImplOptions.NoInlining)] public IRoutePlanningJob Begin(RouteRequest request) => new Job(this, request);
        private static Route Answer(RouteRequest request)
        {
            new WalkingRoutes().Calculate(); new RoadRoutes.RoadPlan().Continue(0);
            var route = new Route();
            route.Legs.Add(new RouteLeg { From = request.From.Position, To = request.To.Position, Mode = TravelMode.Walk,
                Minutes = Math.Max(.01, Position.Distance(request.From.Position, request.To.Position) / 80) });
            return route;
        }
        private sealed class Job : IRoutePlanningJob
        {
            private readonly MultimodalRoutes owner;
            private readonly RouteRequest request;
            private int calls;
            public Job(MultimodalRoutes owner, RouteRequest request) { this.owner = owner; this.request = request; }
            public Route? Result { get; private set; }
            public bool Continue(double budget)
            {
                if (owner.FailNext) { owner.FailNext = false; throw new InvalidOperationException("injected"); }
                if (++calls == 1 && !double.IsPositiveInfinity(budget)) return false;
                Result = Answer(request); return true;
            }
            public void Dispose() { }
        }
    }
}
namespace FoodDemandAuditProbe
{
    public sealed partial class Probe
    {
        private static Probe? frameOwner;
        private bool recordingFrames;
        private int currentWorkFrame;
        private struct ModWorkFrame { public EventWork SlowestEvent; public int EventSlicesDropped; public ManagedDelta Managed; }
        public long Samples, Plans, Failures, NavTicks, RoadTicks, AStarTicks;
        public double NavMeshPathMilliseconds;
        public long DiagnosticFailures => eventMeasurementFailures;
        public bool EventCaptureDisabled => eventMeasurementsFailed && activeEvent == null;
        public void InjectObserverFault(string seam)
        {
            if (seam == "event-prefix") UnityEngine.Time.FailAtRead = UnityEngine.Time.Reads + 1;
            else if (seam == "event-finalizer") UnityEngine.Time.FailAtRead = UnityEngine.Time.Reads + 3;
            else if (seam == "query-prefix") walkingMetrics = null;
            else if (seam == "query-finalizer") PersistentCity.Runtime.NativeRouteMetrics.FailAtRead = PersistentCity.Runtime.NativeRouteMetrics.Reads + 2;
            else throw new ArgumentException(seam);
        }
        public void VerifyOriginalExceptionSurvivesObserverFault(bool query)
        {
            var original = new InvalidOperationException("original simulation failure");
            var scope = new EventScope { Start = System.Diagnostics.Stopwatch.GetTimestamp() };
            activeEvent = scope;
            Exception? result;
            if (query)
            {
                navMeshMilliseconds = null;
                result = QueryPatches.Finally(original, new QueryScope { Event = scope, Metrics = new object() });
            }
            else
            {
                UnityEngine.Time.FailAtRead = UnityEngine.Time.Reads + 1;
                result = EventPatches.Finally(original, scope);
            }
            if (!ReferenceEquals(result, original) || !EventCaptureDisabled || DiagnosticFailures != 1)
                throw new Exception("Observer changed original exception or failed to disable capture");
        }
        public void Start()
        {
            frameOwner = this;
            StartEventMeasurements(new Harmony("event-regression"));
            recordingFrames = true; currentWorkFrame = UnityEngine.Time.frameCount;
        }
        public void Stop()
        {
            ObserveWorkFrame(++UnityEngine.Time.frameCount);
            recordingFrames = false; new Harmony("event-regression").UnpatchAll("event-regression"); frameOwner = null;
            // Exercise the actual CSV writer as well as the production aggregation helper.
            var csv = new System.Text.StringBuilder();
            AppendEventCsv(csv, new ModWorkFrame { SlowestEvent = default, EventSlicesDropped = 0, Managed = default });
            if (!csv.ToString().Contains(managedCounterKind)) throw new Exception("counter kind missing");
        }
        private void ObserveWorkFrame(int frame)
        {
            if (frame == currentWorkFrame) return;
            var e = eventWork.Slowest;
            if (e.Slices > 0) { Samples++; Plans += e.Plans; Failures += e.Failures; NavTicks += e.NavMeshTicks; RoadTicks += e.RoadSearchTicks;
                AStarTicks += e.AStarTicks; NavMeshPathMilliseconds += e.NavMeshPathMilliseconds; }
            eventWork.Clear(); currentWorkFrame = frame;
        }
        private static double Milliseconds(long ticks) => ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
    }
}
