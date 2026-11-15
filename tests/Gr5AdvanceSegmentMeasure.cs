using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using PersistentCity.Core;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

[Collection(StreetFoodHooks.Collection)]
public sealed class Gr5AdvanceSegmentMeasure
{
    private readonly ITestOutputHelper output;
    public Gr5AdvanceSegmentMeasure(ITestOutputHelper output) { this.output = output; }

    private sealed class Types : EventListener
    {
        public readonly Dictionary<(GcAllocationSegment, string), long> Bytes = new Dictionary<(GcAllocationSegment, string), long>();
        public GcAllocationSegment Segment = GcAllocationSegment.AdvanceOther;
        protected override void OnEventSourceCreated(EventSource source)
        {
            if (source.Name == "Microsoft-Windows-DotNETRuntime") EnableEvents(source, EventLevel.Verbose, (EventKeywords)0x1);
        }
        protected override void OnEventWritten(EventWrittenEventArgs e)
        {
            if (e.EventName == null || !e.EventName.StartsWith("GCAllocationTick", StringComparison.Ordinal) || e.Payload == null) return;
            int name = e.PayloadNames!.IndexOf("TypeName"), amount = e.PayloadNames.IndexOf("AllocationAmount64");
            if (name < 0) return;
            var key = (Segment, (string)e.Payload[name]!);
            long bytes = amount >= 0 ? Convert.ToInt64(e.Payload[amount]) : 100_000;
            lock (Bytes) Bytes[key] = (Bytes.TryGetValue(key, out long b) ? b : 0) + bytes;
        }
    }

    private sealed class Straight : IResumableRoutePlanner, ICommuteTimingPlanner
    {
        public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences)
        {
            var route = new Route();
            route.Legs.Add(new RouteLeg { From = from.Position, To = to.Position, Mode = TravelMode.Walk, Minutes = Math.Max(0.5, Position.Distance(from.Position, to.Position) / 80) });
            return route;
        }
        public IRoutePlanningJob Begin(RouteRequest request) => new Job(this, request);
        private sealed class Job : IRoutePlanningJob
        {
            private readonly Straight owner; private readonly RouteRequest request; private int slices;
            public Job(Straight owner, RouteRequest request) { this.owner = owner; this.request = request; }
            public Route? Result { get; private set; }
            public bool Continue(double budgetMilliseconds)
            {
                if (++slices < 2 && !double.IsPositiveInfinity(budgetMilliseconds)) return false;
                Result = owner.Plan(request.From, request.To, request.DepartureMinute, request.Preferences);
                return true;
            }
            public void Dispose() { }
        }
    }

    [Fact]
    public void MeasureAdvanceBytesBySubSegment()
    {
        if (PerfGate.Variable("PC_GR5_SEGMENTS") == null) return;
        var state = Gr5AllocationMeasure.RigCity();
        var simulator = new CitySimulator(state, new Straight());
        simulator.AttachFoodMarket(new VisitorRoutineTests.Market { Shops = F5gPopulationFillTests.Shops(state) });
        double day = Math.Floor(state.Minute / 1440) * 1440 + 1440;
        using var types = new Types();
        foreach (var (window, start) in new[] { ("night", day + 60), ("day", day + 15.5 * 60) })
        {
            simulator.AdvanceTo(start - 20, int.MaxValue);
            var bytes = new long[GcPauseSampler.Segments]; var calls = new long[GcPauseSampler.Segments];
            lock (types.Bytes) types.Bytes.Clear();
            for (double minute = start - 20; minute < start + 20; minute += 1.0 / 30)
            {
                bool measured = minute >= start;
                for (int unit = 0; unit < 64; unit++)
                {
                    long before = GC.GetAllocatedBytesForCurrentThread();
                    GcAllocationSegment segment;
                    if (simulator.PlanningPending)
                    {
                        types.Segment = GcAllocationSegment.AdvancePlanningSlice;
                        bool finished = simulator.ContinuePlanning(2);
                        segment = finished ? GcAllocationSegment.AdvancePlanningFinish : GcAllocationSegment.AdvancePlanningSlice;
                    }
                    else
                    {
                        types.Segment = GcAllocationSegment.AdvanceEvents;
                        var result = simulator.AdvanceFrameSlice(minute, 1);
                        segment = GcPauseSampler.AdvanceEventSegment(result.TrafficEvents, result.FoodContinuationEvents, result.CitizenEvents, result.PlanningPending);
                        if (result.CaughtUp) { if (measured) { bytes[(int)segment] += GC.GetAllocatedBytesForCurrentThread() - before; calls[(int)segment]++; } break; }
                    }
                    if (measured) { bytes[(int)segment] += GC.GetAllocatedBytesForCurrentThread() - before; calls[(int)segment]++; }
                }
            }
            foreach (var s in new[] { GcAllocationSegment.AdvanceEventsTraffic, GcAllocationSegment.AdvanceEventsFood, GcAllocationSegment.AdvanceEventsCitizen,
                GcAllocationSegment.AdvanceEventsRouteBegin, GcAllocationSegment.AdvanceEventsOther, GcAllocationSegment.AdvancePlanningSlice, GcAllocationSegment.AdvancePlanningFinish })
                output.WriteLine(window + " " + GcPauseSampler.SegmentName(s) + ": " + bytes[(int)s] + " B / " + calls[(int)s] + " calls = "
                    + (calls[(int)s] == 0 ? 0 : bytes[(int)s] / calls[(int)s]) + " B/call");
            lock (types.Bytes)
                foreach (var pair in types.Bytes.OrderByDescending(p => p.Value).Take(25))
                    output.WriteLine(window + " type " + pair.Key.Item1 + " " + pair.Key.Item2 + " " + pair.Value);
        }
    }
}
