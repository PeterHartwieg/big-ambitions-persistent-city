using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    /// <summary>An exclusive, simulator-local authority over one committed journey. Never persist this handle.</summary>
    public sealed class PhysicalJourneyLease
    {
        internal PhysicalJourneyLease(CitySimulator owner, long generation, CitizenState citizen)
        { Owner = owner; Generation = generation; Citizen = citizen; Identity = citizen.Identity; Route = citizen.Journey!; CitizenId = citizen.Identity.Id; }
        internal CitySimulator Owner { get; }
        internal CitizenState Citizen { get; }
        internal CitizenIdentity Identity { get; }
        internal Route Route { get; }
        internal double Progress;
        /// <summary>Exact credited route minutes, before geometric boundary normalization in a sample.</summary>
        public double ObservedMinutes => Progress;
        public string CitizenId { get; }
        public long Generation { get; }
    }

    public readonly struct PhysicalJourneySample
    {
        internal PhysicalJourneySample(double elapsed, double total, int leg, double fraction, RouteLeg segment, Position position, Position? car, TravelMode? nextMode)
        { ElapsedMinutes = elapsed; TotalMinutes = total; LegIndex = leg; LegFraction = fraction; Mode = segment.Mode;
          From = segment.From; To = segment.To; Position = position; CarPosition = car; ServiceId = segment.ServiceId; LegMinutes = segment.Minutes; NextMode = nextMode; }
        public double ElapsedMinutes { get; }
        public double TotalMinutes { get; }
        public int LegIndex { get; }
        public double LegFraction { get; }
        public TravelMode Mode { get; }
        public string ServiceId { get; }
        public Position From { get; }
        public Position To { get; }
        public Position Position { get; }
        public Position? CarPosition { get; }
        public double LegMinutes { get; }
        public double RemainingMinutes => Math.Max(0, TotalMinutes - ElapsedMinutes);
        public double RemainingLegMinutes => LegMinutes * (1 - LegFraction);
        public bool AtEnd => ElapsedMinutes >= TotalMinutes;
        public TravelMode? NextMode { get; }
        public Position Direction
        {
            get
            {
                double distance = PersistentCity.Core.Position.Distance(From, To);
                return distance <= 0.000001 ? new Position(0, 0, 0)
                    : new Position((To.X - From.X) / distance, (To.Y - From.Y) / distance, (To.Z - From.Z) / distance);
            }
        }
        public bool Stationary => PersistentCity.Core.Position.Distance(From, To) <= 0.000001;
    }

    public sealed partial class CitySimulator
    {
        private readonly Dictionary<string, PhysicalJourneyLease> physicalJourneys = new Dictionary<string, PhysicalJourneyLease>(StringComparer.Ordinal);
        private long physicalGeneration;

        /// <summary>Acquire after advancing the city. Its committed route progress freezes until observed or released.</summary>
        public bool TryAcquirePhysicalJourney(string citizenId, out PhysicalJourneyLease? lease)
        {
            lease = null;
            if (citizenId == null) throw new ArgumentNullException(nameof(citizenId));
            if (physicalJourneys.ContainsKey(citizenId) || !citizenEvents.TryGetValue(citizenId, out var item) || InProgress(item)) return false;
            var c = item.Citizen;
            if (c.Phase != CitizenPhase.Travelling || c.Journey == null || c.Identity.Id != citizenId || !CurrentCitizenSlot(item, c)) return false;
            double progress = JourneyProgress(c);
            if (progress < 0 || progress >= c.Journey.Minutes) return false;
            if (traffic != null && traffic.TryGetVehicle(citizenId, out var trafficPosition) && !trafficPosition.LinkId.HasValue) return false;
            if (!trafficCitizens.ContainsKey(citizenId) && NextTrafficBoundary(c, progress) <= progress) return false;
            ValidateRoute(c.Journey, places[c.PlaceId].Position, places[c.DestinationId].Position);
            ValidateCarRoute(c.Journey, c);
            lease = new PhysicalJourneyLease(this, checked(++physicalGeneration), c) { Progress = progress };
            if (trafficCitizens.ContainsKey(citizenId)) traffic!.SetPhysicalControl(citizenId, true);
            else if (!due.Remove(item)) throw new InvalidOperationException("Journey event is not queued.");
            physicalJourneys.Add(citizenId, lease);
            return true;
        }

        public bool IsPhysicalJourneyValid(PhysicalJourneyLease lease) => ValidPhysicalLease(lease);

        private bool ValidPhysicalLease(PhysicalJourneyLease? lease) => lease != null && ReferenceEquals(lease.Owner, this)
            && physicalJourneys.TryGetValue(lease.CitizenId, out var current) && ReferenceEquals(current, lease)
            && ReferenceEquals(lease.Citizen.Identity, lease.Identity) && lease.Citizen.Identity.Id == lease.CitizenId
            && ReferenceEquals(lease.Citizen.Journey, lease.Route) && lease.Citizen.Phase == CitizenPhase.Travelling
            && citizenEvents.TryGetValue(lease.CitizenId, out var item) && ReferenceEquals(item.Citizen, lease.Citizen)
            && CurrentCitizenSlot(item, lease.Citizen);

        private bool CurrentCitizenSlot(Due item, CitizenState citizen) => item.ListIndex >= 0
            && item.ListIndex < State.Citizens.Count && ReferenceEquals(State.Citizens[item.ListIndex], citizen);

        /// <summary>
        /// Report measured progress along the persisted legs, expressed in their nominal route minutes.
        /// Equal progress means blocked/waiting. Call after AdvanceTo; progress cannot exceed the city clock
        /// because schema 4 represents it as a nonnegative departure time. No admission happens here.
        /// </summary>
        public bool ObservePhysicalJourney(PhysicalJourneyLease lease, double routeElapsedMinutes)
        {
            if (!Position.Finite(routeElapsedMinutes) || routeElapsedMinutes < 0) throw new ArgumentOutOfRangeException(nameof(routeElapsedMinutes));
            if (!ValidPhysicalLease(lease)) return false;
            if (routeElapsedMinutes < lease.Progress || routeElapsedMinutes > lease.Route.Minutes || routeElapsedMinutes > State.Minute)
                throw new ArgumentOutOfRangeException(nameof(routeElapsedMinutes), "Progress must be monotonic and inside the route and city clock.");
            if (!trafficCitizens.ContainsKey(lease.CitizenId))
            {
                double boundary = NextTrafficBoundary(lease.Citizen, lease.Progress);
                if (routeElapsedMinutes >= boundary)
                {
                    lease.Progress = boundary;
                    RebasePhysicalJourney(lease);
                    if (!TryStartTraffic(lease.Citizen)) throw new InvalidOperationException("Physical access lost its traffic handoff.");
                    return routeElapsedMinutes - boundary <= 1e-7;
                }
            }
            if (trafficCitizens.ContainsKey(lease.CitizenId))
            {
                double accepted = traffic!.ReportPhysicalProgress(lease.CitizenId, routeElapsedMinutes);
                // The adapter must constrain commanded movement before this boundary. Refuse a
                // material physical overshoot instead of crediting movement through a queue.
                if (routeElapsedMinutes - accepted > 1e-7)
                { lease.Progress = accepted; RebasePhysicalJourney(lease); return false; }
                routeElapsedMinutes = accepted;
            }
            lease.Progress = routeElapsedMinutes;
            RebasePhysicalJourney(lease);
            // Arrival runs through the ordinary due queue (including its budget and opening checks).
            if (routeElapsedMinutes == lease.Route.Minutes) ReturnPhysicalEvent(lease);
            return true;
        }

        /// <summary>Resume background travel at the last observed point with the remaining route duration.</summary>
        public bool ReleasePhysicalJourney(PhysicalJourneyLease lease)
        {
            if (!ValidPhysicalLease(lease)) return false;
            RebasePhysicalJourney(lease);
            if (trafficCitizens.ContainsKey(lease.CitizenId)) traffic!.SetPhysicalControl(lease.CitizenId, false);
            ReturnPhysicalEvent(lease);
            return true;
        }

        public bool TrySamplePhysicalJourney(PhysicalJourneyLease lease, out PhysicalJourneySample sample)
        {
            sample = default;
            if (!ValidPhysicalLease(lease)) return false;
            sample = SampleJourney(lease.Citizen, lease.Progress);
            return true;
        }

        /// <summary>Sample committed geometry, including zero-distance preparation/wait legs and the parked/driven car.</summary>
        public PhysicalJourneySample SampleJourney(CitizenState citizen)
        {
            if (citizen == null) throw new ArgumentNullException(nameof(citizen));
            double elapsed = JourneyProgress(citizen);
            if (citizen.Journey != null) elapsed = NormalizeJourneyElapsed(elapsed, citizen.Journey.Minutes, State.Minute);
            return SampleJourney(citizen, elapsed);
        }

        // A completed lease stores departure = clock - duration. Reversing that subtraction can
        // exceed duration by an ulp at a large city clock; this is not evidence of extra travel.
        internal static double JourneyClockTolerance(double clock, double total) =>
            4 * 2.2204460492503131e-16 * Math.Max(1, Math.Max(Math.Abs(clock), Math.Abs(total)));

        internal static double NormalizeJourneyElapsed(double elapsed, double total, double clock)
        {
            if (Math.Abs(elapsed - total) <= JourneyClockTolerance(clock, total)) return total;
            return elapsed;
        }

        public PhysicalJourneySample SampleJourney(CitizenState citizen, double routeElapsedMinutes)
        {
            if (citizen == null) throw new ArgumentNullException(nameof(citizen));
            var route = citizen.Journey;
            if (citizen.Phase != CitizenPhase.Travelling || route == null || route.Legs.Count == 0)
                throw new InvalidOperationException("Citizen has no committed journey.");
            return SampleRoute(route, routeElapsedMinutes, citizen.Car?.ParkedPosition);
        }

        // Shared pure sampling keeps clock-retime validation on the exact physical pose contract.
        internal static PhysicalJourneySample SampleRoute(Route route, double routeElapsedMinutes, Position? parkedCar)
        {
            double totalMinutes = route.Minutes;
            if (!Position.Finite(routeElapsedMinutes) || routeElapsedMinutes < 0 || routeElapsedMinutes > totalMinutes)
                throw new ArgumentOutOfRangeException(nameof(routeElapsedMinutes));
            double remaining = routeElapsedMinutes;
            double sampledElapsed = routeElapsedMinutes;
            Position? car = parkedCar;
            for (int i = 0; i < route.Legs.Count; i++)
            {
                var leg = route.Legs[i];
                // Summing fractional legs and then subtracting them can leave an endpoint a few
                // ulps short. Snap only a negligible fraction of this leg, so a reached corner
                // cannot remain leased forever while the native body has already stopped there.
                double boundaryTolerance = Math.Min(leg.Minutes * 1e-9, Math.Max(1e-12, Math.Abs(routeElapsedMinutes) * 1e-14));
                if (Math.Abs(remaining - leg.Minutes) <= boundaryTolerance)
                {
                    // Endpoint normalization may move the geometry back by a few ulps when the
                    // measured progress was just beyond the corner. Never move its clock back.
                    sampledElapsed = Math.Max(routeElapsedMinutes, sampledElapsed + leg.Minutes - remaining);
                    remaining = leg.Minutes;
                }
                // At a boundary, expose the next leg (important for walk/car/transit handoffs).
                if (remaining < leg.Minutes || i == route.Legs.Count - 1)
                {
                    double fraction = Math.Min(1, remaining / leg.Minutes);
                    var position = Position.Lerp(leg.From, leg.To, fraction);
                    if (leg.Mode == TravelMode.Car) car = position;
                    return new PhysicalJourneySample(i == route.Legs.Count - 1 && fraction == 1 ? totalMinutes : Math.Min(totalMinutes, Math.Max(routeElapsedMinutes, sampledElapsed)), totalMinutes, i, fraction, leg, position, car, i + 1 < route.Legs.Count ? route.Legs[i + 1].Mode : (TravelMode?)null);
                }
                if (leg.Mode == TravelMode.Car) car = leg.To;
                remaining -= leg.Minutes;
            }
            throw new InvalidOperationException("Invalid route.");
        }

        private void ReturnPhysicalEvent(PhysicalJourneyLease lease)
        {
            physicalJourneys.Remove(lease.CitizenId);
            if (trafficCitizens.ContainsKey(lease.CitizenId)) return;
            var item = citizenEvents[lease.CitizenId];
            item.Minute = ScheduledMinute(item.Citizen);
            if (!due.Add(item)) throw new InvalidOperationException("Duplicate physical journey event.");
        }

        private void RebasePhysicalJourney(PhysicalJourneyLease lease)
        {
            double totalMinutes = lease.Route.Minutes;
            lease.Citizen.DepartureMinute = State.Minute - lease.Progress;
            lease.Citizen.NextEventMinute = lease.Progress == totalMinutes
                ? State.Minute : lease.Citizen.DepartureMinute + totalMinutes;
        }

        private void ValidatePhysicalClock(double target)
        {
            foreach (var lease in physicalJourneys.Values)
            {
                if (!ValidPhysicalLease(lease)) throw new InvalidOperationException("Physical journey ownership changed outside the simulator.");
                if (target + (lease.Route.Minutes - lease.Progress) > 1e9)
                    throw new ArgumentOutOfRangeException(nameof(target), "Delayed arrival exceeds the supported clock.");
            }
        }

        private void SynchronizePhysicalJourneys()
        {
            foreach (var lease in physicalJourneys.Values) RebasePhysicalJourney(lease);
        }
    }
}
