using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    /// <summary>Policy in simulation minutes; green intervals are half-open and repeat without polling.</summary>
    public sealed class TrafficSignalSchedule
    {
        public double CycleMinutes { get; set; }
        public double GreenStartMinutes { get; set; }
        public double GreenDurationMinutes { get; set; }
        public double PhaseOffsetMinutes { get; set; }
        internal double NextGreen(double time)
        {
            if (GreenDurationMinutes == 0) return double.PositiveInfinity;
            if (GreenDurationMinutes >= CycleMinutes) return time;
            double resolution = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(time) + 1) - time;
            if (CycleMinutes < resolution) return double.PositiveInfinity;
            // One canonical boundary per cycle: reconstructing it from the query time
            // makes the same pending green drift through cancellation as Clock advances.
            // Reduce the origin first so a distant phase cannot overflow/cancel the clock.
            double origin = PhaseOffsetMinutes % CycleMinutes;
            if (origin < 0) origin += CycleMinutes;
            double untilWrap = CycleMinutes - GreenStartMinutes;
            origin = origin >= untilWrap ? origin - untilWrap : origin + GreenStartMinutes;
            double cycle = Math.Floor((time - origin) / CycleMinutes);
            for (int correction = 0; correction < 3; correction++)
            {
                double start = origin + cycle * CycleMinutes;
                double next = origin + (cycle + 1) * CycleMinutes;
                // No representable adjacent boundaries means no provable green window.
                if (double.IsNaN(start) || double.IsInfinity(start) || double.IsNaN(next)
                    || double.IsInfinity(next) || next <= start) return double.PositiveInfinity;
                // Division can select the adjacent cycle exactly at a rounded boundary.
                if (time < start) { cycle--; continue; }
                if (time >= next) { cycle++; continue; }
                double end = start + GreenDurationMinutes;
                // A positive green window always includes its exact canonical start,
                // even when no later timestamp fits inside that very short window.
                return time == start || time < end ? time : next;
            }
            return double.PositiveInfinity;
        }


    }

    public sealed class TrafficLinkDefinition
    {
        public int Id { get; set; }
        public int Capacity { get; set; }
        public double FreeFlowMinutes { get; set; }
        public double HeadwayMinutes { get; set; }
        public TrafficSignalSchedule? Signal { get; set; }
    }

    /// <summary>Nominal coordinates address committed route geometry, never elapsed simulation time.</summary>
    public sealed class TrafficRouteLeg
    {
        public int LinkId { get; set; }
        public double StartNominalMinutes { get; set; }
        public double EndNominalMinutes { get; set; }
        /// <summary>Zero derives traversal time from the nominal geometry span; stored snapshots use an explicit positive value.</summary>
        public double FreeFlowMinutes { get; set; }
        /// <summary>Directed spatial coverage within the entire link, independent of nominal route timing.</summary>
        public double StartLinkFraction { get; set; }
        public double EndLinkFraction { get; set; } = 1;
    }

    public readonly struct TrafficAdvanceResult
    {
        public TrafficAdvanceResult(double clock, double requestedTime, int processedEvents, bool reachedTarget)
        { Clock = clock; RequestedTime = requestedTime; ProcessedEvents = processedEvents; ReachedTarget = reachedTarget; }
        public double Clock { get; }
        public double RequestedTime { get; }
        public int ProcessedEvents { get; }
        public bool ReachedTarget { get; }
    }

    public readonly struct TrafficVehicleSnapshot
    {
        internal TrafficVehicleSnapshot(string vehicleId, int routeIndex, int? linkId, double progress,
            double maximum, bool canCross, bool physical, bool completed, double? completedAt)
        {
            VehicleId = vehicleId; RouteIndex = routeIndex; LinkId = linkId; NominalMinutes = progress;
            MaximumNominalMinutes = maximum; CanCrossBoundary = canCross; PhysicalControl = physical;
            IsCompleted = completed; CompletedAt = completedAt;
        }
        public string VehicleId { get; }
        /// <summary>-1 while waiting outside finite link storage.</summary>
        public int RouteIndex { get; }
        public int? LinkId { get; }
        public double NominalMinutes { get; }
        public double MaximumNominalMinutes { get; }
        /// <summary>Advisory only. Crossing ownership changes only when AdvanceTo processes the transfer.</summary>
        public bool CanCrossBoundary { get; }
        public bool PhysicalControl { get; }
        public bool IsCompleted { get; }
        public double? CompletedAt { get; }
    }

    public readonly struct TrafficLinkForecast
    {
        internal TrafficLinkForecast(int id, int occupancy, int waiting, double delay, bool closed)
        { LinkId = id; Occupancy = occupancy; WaitingVehicles = waiting; DelayMinutes = delay; Closed = closed; }
        public int LinkId { get; }
        public int Occupancy { get; }
        public int WaitingVehicles { get; }
        /// <summary>Read-only local queue estimate, not a promise about future arrivals or spillback.</summary>
        public double DelayMinutes { get; }
        public bool Closed { get; }
    }

    // Explicit DTOs include the interrupted event cursor. Persist through the caller's versioned namespace.
    public sealed class TrafficKernelState
    {
        public int Schema { get; set; } = 2;
        public double Clock { get; set; }
        public double RequestedTime { get; set; }
        public long NextEventOrder { get; set; }
        public long NextVehicleOrder { get; set; }
        public List<TrafficLinkDefinition> Definitions { get; set; } = new List<TrafficLinkDefinition>();
        public List<TrafficLinkState> Links { get; set; } = new List<TrafficLinkState>();
        public List<TrafficVehicleState> Vehicles { get; set; } = new List<TrafficVehicleState>();
        public List<TrafficEventState> Events { get; set; } = new List<TrafficEventState>();
    }
    public sealed class TrafficLinkState
    {
        public int LinkId { get; set; }
        public bool Closed { get; set; }
        public double NextDischargeAt { get; set; }
        public int MergeCursor { get; set; } = -1;
        public List<string> Vehicles { get; set; } = new List<string>();
        public List<string> WaitingSources { get; set; } = new List<string>();
        public List<int> IncomingLinks { get; set; } = new List<int>();
    }
    public sealed class TrafficVehicleState
    {
        public string VehicleId { get; set; } = "";
        public long Order { get; set; }
        public List<TrafficRouteLeg> Route { get; set; } = new List<TrafficRouteLeg>();
        public int RouteIndex { get; set; } = -1;
        public double EnqueuedAt { get; set; }
        public double EnteredAt { get; set; }
        public double ReadyAt { get; set; }
        public double MotionStartedAt { get; set; }
        public double MotionStartFraction { get; set; }
        public double NominalMinutes { get; set; }
        public bool PhysicalControl { get; set; }
        public bool Completed { get; set; }
        public double? CompletedAt { get; set; }
    }
    public sealed class TrafficEventState
    {
        public int LinkId { get; set; }
        public double At { get; set; }
        public long Order { get; set; }
    }
}
