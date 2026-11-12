using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
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
            double origin = PhaseOffsetMinutes % CycleMinutes;
            if (origin < 0) origin += CycleMinutes;
            double untilWrap = CycleMinutes - GreenStartMinutes;
            origin = origin >= untilWrap ? origin - untilWrap : origin + GreenStartMinutes;
            double cycle = Math.Floor((time - origin) / CycleMinutes);
            for (int correction = 0; correction < 3; correction++)
            {
                double start = origin + cycle * CycleMinutes;
                double next = origin + (cycle + 1) * CycleMinutes;
                if (double.IsNaN(start) || double.IsInfinity(start) || double.IsNaN(next)
                    || double.IsInfinity(next) || next <= start) return double.PositiveInfinity;
                if (time < start) { cycle--; continue; }
                if (time >= next) { cycle++; continue; }
                double end = start + GreenDurationMinutes;
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

    public sealed class TrafficRouteLeg
    {
        public int LinkId { get; set; }
        public double StartNominalMinutes { get; set; }
        public double EndNominalMinutes { get; set; }
        public double FreeFlowMinutes { get; set; }
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
            double maximum, bool canCross, bool completed, double? completedAt,
            bool signalGreen = true, double linkEndNominalMinutes = double.NaN)
        {
            VehicleId = vehicleId; RouteIndex = routeIndex; LinkId = linkId; NominalMinutes = progress;
            MaximumNominalMinutes = maximum; CanCrossBoundary = canCross;
            IsCompleted = completed; CompletedAt = completedAt; SignalGreen = signalGreen; LinkEndNominalMinutes = linkEndNominalMinutes;
        }
        public string VehicleId { get; }
        public int RouteIndex { get; }
        public int? LinkId { get; }
        public double NominalMinutes { get; }
        public double MaximumNominalMinutes { get; }
        public bool CanCrossBoundary { get; }
        public bool IsCompleted { get; }
        public double? CompletedAt { get; }
        public bool SignalGreen { get; }
        public double LinkEndNominalMinutes { get; }
    }

    public readonly struct TrafficLinkForecast
    {
        internal TrafficLinkForecast(int id, int occupancy, int waiting, double delay, bool closed)
        { LinkId = id; Occupancy = occupancy; WaitingVehicles = waiting; DelayMinutes = delay; Closed = closed; }
        public int LinkId { get; }
        public int Occupancy { get; }
        public int WaitingVehicles { get; }
        public double DelayMinutes { get; }
        public bool Closed { get; }
    }

    public sealed class TrafficKernelState
    {
        public const int CurrentSchema = 1;
        public int Schema { get; set; } = CurrentSchema;
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
