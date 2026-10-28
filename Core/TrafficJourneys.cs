using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public sealed partial class CitySimulator
    {
        private TrafficKernel? traffic;
        private TrafficNetwork? trafficNetwork;
        private readonly Dictionary<string, CitizenState> trafficCitizens = new Dictionary<string, CitizenState>(StringComparer.Ordinal);
        private readonly List<int> closureLinks = new List<int>();
        private bool refreshingClosures;
        public TrafficKernel? Traffic => traffic;

        /// <summary>Refresh only demanded/receiving sections once per native frame, including dormant blocked queues.</summary>
        public void RefreshTrafficClosures()
        {
            if (refreshingClosures) throw new InvalidOperationException("Traffic closure refresh cannot be nested.");
            refreshingClosures = true;
            try
            {
                EnsureTrafficNetwork();
                if (traffic == null || !(routes is ITrafficRoutePlanner planner)) return;
                // SetLinkClosed can change the scheduled set; collect IDs before applying observations.
                traffic.CopyRelevantLinkIds(closureLinks);
                var batch = routes as ITrafficClosureBatchPlanner;
                batch?.BeginTrafficClosureBatch();
                try { foreach (int id in closureLinks) traffic.SetLinkClosed(id, planner.IsTrafficLinkClosed(id)); }
                finally { batch?.EndTrafficClosureBatch(); }
            }
            finally { closureLinks.Clear(); refreshingClosures = false; }
        }

        private void RestoreTraffic()
        {
            State.SnapshotSynchronizer = CaptureTraffic;
            if (State.Traffic == null) return;
            if (State.Traffic.Clock != State.Minute || string.IsNullOrEmpty(State.TrafficTopologyId))
                throw new ArgumentException("Traffic and city clocks/topology do not match.");
            traffic = TrafficKernel.Restore(State.Traffic);
            traffic.VehicleCompleted += TrafficCompleted;
            foreach (var vehicle in State.Traffic.Vehicles)
            {
                if (!citizenEvents.TryGetValue(vehicle.VehicleId, out var item) || item.Citizen.Phase != CitizenPhase.Travelling)
                    throw new ArgumentException("Traffic vehicle has no travelling resident.");
                trafficCitizens.Add(vehicle.VehicleId, item.Citizen);
                due.Remove(item);
            }
            // Completed vehicles normally leave the kernel immediately. Handle an interrupted
            // capture at that boundary without admitting the resident twice.
            foreach (var vehicle in State.Traffic.Vehicles)
                if (vehicle.Completed) TrafficCompleted(vehicle.VehicleId, vehicle.CompletedAt!.Value);
        }

        private void EnsureTrafficNetwork()
        {
            if (!(routes is ITrafficRoutePlanner planner) || planner.TrafficNetwork == null) return;
            var network = planner.TrafficNetwork;
            if (ReferenceEquals(network, trafficNetwork)) return;
            foreach (var citizen in State.Citizens)
            {
                if (citizen.Phase != CitizenPhase.Travelling || citizen.Journey == null || trafficCitizens.ContainsKey(citizen.Identity.Id)) continue;
                double progress = JourneyProgress(citizen), offset = 0;
                bool needsRoad = false;
                double totalMinutes = citizen.Journey.Minutes;
                foreach (var leg in citizen.Journey.Legs)
                {
                    offset += leg.Minutes;
                    if (offset > progress + JourneyClockTolerance(State.Minute, totalMinutes)
                        && leg.ServiceId.StartsWith("native:road:", StringComparison.Ordinal)) needsRoad = true;
                }
                if (needsRoad && !network.TryMapRemainingSpans(citizen.Journey, MappingProgress(citizen, progress), mappingSpans))
                    throw new InvalidOperationException("Committed road geometry no longer matches the native network; preserve the saved journey.");
            }
            if (traffic != null && State.TrafficTopologyId != network.TopologyId && traffic.VehicleCount != 0)
                throw new InvalidOperationException("The road topology changed during committed traffic; preserve the saved state.");
            if (traffic != null && traffic.VehicleCount != 0)
            {
                // Binding is infrequent. Validate the saved spatial ownership against every
                // committed corner, including an untagged fragment split by clock conversion.
                foreach (var vehicle in traffic.Capture().Vehicles)
                    network.ValidateRemainingMapping(trafficCitizens[vehicle.VehicleId].Journey!, vehicle.Route, vehicle.NominalMinutes);
            }
            if (traffic == null || State.TrafficTopologyId != network.TopologyId)
            {
                traffic = new TrafficKernel(network.Links, State.Minute);
                traffic.VehicleCompleted += TrafficCompleted;
                State.TrafficTopologyId = network.TopologyId;
            }
            else if (!traffic.HasCompatibleDefinitions(network.Links))
                throw new InvalidOperationException("Saved road section policy disagrees with its topology/timing.");
            trafficNetwork = network;
            planner.SetTrafficForecast((id, at) => traffic.Forecast(id, at));
            if (routes is ITrafficEntryRoutePlanner entryPlanner)
                entryPlanner.SetTrafficEntryForecast((id, at, fraction, share) => traffic.ForecastEntry(id, at, fraction, share));
            // A native snapshot can finish after simulator construction. Register existing
            // journeys at their current geometry, never rewind them to their departure.
            // The event whose route request is in flight is outside the queue: CompleteEvent re-adds it with its
            // new minute, or AbortPending with its unstarted one. Queuing it here as well would leave it in the
            // set while CompleteEvent rewrites its sort key, duplicating the resident and breaking the order.
            due.Clear();
            foreach (var item in citizenEvents.Values)
                if (!InProgress(item) && !trafficCitizens.ContainsKey(item.Citizen.Identity.Id) && !physicalJourneys.ContainsKey(item.Citizen.Identity.Id))
                { item.Minute = ScheduledMinute(item.Citizen); due.Add(item); }
        }

        // Reused by the boundary queries above and below, which only read the first unfinished span.
        // Simulator calls are single-threaded and none of them nests another mapping.
        private readonly List<TrafficNetwork.MappedSpan> mappingSpans = new List<TrafficNetwork.MappedSpan>();

        private double ScheduledMinute(CitizenState citizen)
        {
            if (trafficNetwork != null && citizen.Phase == CitizenPhase.Travelling && citizen.Journey != null
                && trafficNetwork.TryMapRemainingSpans(citizen.Journey, MappingProgress(citizen, JourneyProgress(citizen)), mappingSpans))
            {
                double progress = JourneyProgress(citizen);
                double totalMinutes = citizen.Journey.Minutes;
                foreach (var leg in mappingSpans)
                    if (progress < leg.EndNominalMinutes - JourneyClockTolerance(State.Minute, totalMinutes))
                        return Math.Max(State.Minute, citizen.DepartureMinute + leg.StartNominalMinutes);
            }
            return citizen.NextEventMinute;
        }

        private bool TryStartTraffic(CitizenState citizen)
        {
            if (traffic == null || trafficNetwork == null || citizen.Phase != CitizenPhase.Travelling || citizen.Journey == null
                || !trafficNetwork.TryMapRemaining(citizen.Journey, MappingProgress(citizen, JourneyProgress(citizen)), out var mapped)) return false;
            double progress = JourneyProgress(citizen);
            double tolerance = JourneyClockTolerance(State.Minute, citizen.Journey.Minutes);
            if (progress + tolerance < mapped[0].StartNominalMinutes || progress >= mapped[mapped.Count - 1].EndNominalMinutes - tolerance) return false;
            // Schema migration and a clock conversion can resume part-way through a section.
            // Completed sections create no new demand; the unfinished section keeps its point.
            while (mapped.Count > 0 && mapped[0].EndNominalMinutes <= progress + tolerance) mapped.RemoveAt(0);
            if (mapped.Count == 0) return false;
            var first = mapped[0];
            if (progress + tolerance < first.StartNominalMinutes) return false;
            if (progress > first.StartNominalMinutes + tolerance)
            {
                double fraction = (progress - first.StartNominalMinutes) / (first.EndNominalMinutes - first.StartNominalMinutes);
                first.StartLinkFraction += fraction * (first.EndLinkFraction - first.StartLinkFraction);
                first.FreeFlowMinutes *= (first.EndNominalMinutes - progress) / (first.EndNominalMinutes - first.StartNominalMinutes);
                first.StartNominalMinutes = progress;
            }
            if (routes is ITrafficRoutePlanner planner)
                foreach (var leg in mapped) traffic.SetLinkClosed(leg.LinkId, planner.IsTrafficLinkClosed(leg.LinkId));
            traffic.Enqueue(citizen.Identity.Id, mapped);
            trafficCitizens.Add(citizen.Identity.Id, citizen);
            if (physicalJourneys.ContainsKey(citizen.Identity.Id)) traffic.SetPhysicalControl(citizen.Identity.Id, true);
            return true;
        }

        private double NextTrafficBoundary(CitizenState citizen, double progress)
        {
            if (trafficNetwork != null && citizen.Journey != null && trafficNetwork.TryMapRemainingSpans(citizen.Journey, MappingProgress(citizen, progress), mappingSpans))
            {
                double totalMinutes = citizen.Journey.Minutes;
                foreach (var leg in mappingSpans)
                    if (progress < leg.EndNominalMinutes - JourneyClockTolerance(State.Minute, totalMinutes))
                        return Math.Max(progress, leg.StartNominalMinutes);
            }
            return double.PositiveInfinity;
        }

        private double MappingProgress(CitizenState citizen, double progress)
        {
            // Subtracting large absolute clocks can put an exact curb departure a few
            // ulps inside a section. Do not turn it into an interior-source admission.
            double offset = 0, tolerance = JourneyClockTolerance(State.Minute, citizen.Journey!.Minutes);
            foreach (var leg in citizen.Journey.Legs)
            {
                if (Math.Abs(progress - offset) <= tolerance) return offset;
                offset += leg.Minutes;
            }
            return progress;
        }

        private void TrafficCompleted(string id, double at)
        {
            if (traffic == null || !trafficCitizens.TryGetValue(id, out var citizen)
                || !traffic.TryGetVehicle(id, out var vehicle) || !vehicle.IsCompleted || citizen.Journey == null)
                throw new InvalidOperationException("Traffic completion lost its resident ownership.");
            double progress = vehicle.NominalMinutes;
            citizen.DepartureMinute = at - progress;
            citizen.NextEventMinute = at + Math.Max(0, citizen.Journey.Minutes - progress);
            trafficCitizens.Remove(id);
            traffic.RemoveCompleted(id);
            if (physicalJourneys.TryGetValue(id, out var lease))
            {
                lease.Progress = Math.Max(lease.Progress, progress);
                RebasePhysicalJourney(lease);
            }
            else
            {
                var item = citizenEvents[id];
                item.Minute = citizen.NextEventMinute;
                if (!due.Add(item)) throw new InvalidOperationException("Traffic completion duplicated a resident event.");
            }
        }

        private double JourneyProgress(CitizenState citizen)
        {
            if (traffic != null && traffic.TryGetVehicle(citizen.Identity.Id, out var vehicle)) return vehicle.NominalMinutes;
            if (physicalJourneys.TryGetValue(citizen.Identity.Id, out var lease)) return lease.Progress;
            double elapsed = State.Minute - citizen.DepartureMinute;
            return citizen.Journey == null ? elapsed : NormalizeJourneyElapsed(elapsed, citizen.Journey.Minutes, State.Minute);
        }

        public bool TryGetTrafficPermission(string citizenId, out TrafficVehicleSnapshot permission)
        {
            permission = default;
            return traffic != null && traffic.TryGetVehicle(citizenId, out permission);
        }

        private void SynchronizeTrafficJourneys()
        {
            if (traffic == null) return;
            foreach (var entry in trafficCitizens)
            {
                var citizen = entry.Value;
                var progress = traffic.GetPermission(entry.Key).NominalMinutes;
                citizen.DepartureMinute = State.Minute - progress;
                citizen.NextEventMinute = citizen.DepartureMinute + citizen.Journey!.Minutes;
            }
        }

        private void CaptureTraffic()
        {
            SynchronizeSnapshot();
            State.Traffic = traffic?.Capture();
        }

        /// <summary>Publish progress after a batch of budgeted native-frame slices. Saving also calls this.</summary>
        public void SynchronizeSnapshot()
        {
            SynchronizePhysicalJourneys();
            SynchronizeTrafficJourneys();
        }
    }
}
