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

        private TrafficNetwork? preparedNetwork;
        private int preparedStage, preparedIndex;
        private double preparedMinute = double.NaN;
        private long preparedHolds;
        private bool PreparedFor(TrafficNetwork network) => ReferenceEquals(preparedNetwork, network) && preparedMinute == State.Minute && preparedHolds == State.PlayerHoldSequence;
        private bool NetworkPrepared(TrafficNetwork network) => preparedStage == 3 && PreparedFor(network);
        private readonly TrafficKernel.DefinitionAdoption adoption = new TrafficKernel.DefinitionAdoption();
        public int NetworkPreparationUpdates { get; private set; }

        public bool TryRefreshTrafficClosures(double budgetMilliseconds)
        {
            if (double.IsNaN(budgetMilliseconds) || budgetMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(budgetMilliseconds));
            long deadline = double.IsPositiveInfinity(budgetMilliseconds) ? long.MaxValue
                : System.Diagnostics.Stopwatch.GetTimestamp() + (long)(budgetMilliseconds * System.Diagnostics.Stopwatch.Frequency / 1000);
            NetworkPublished = false;
            int preparing = NetworkPreparationUpdates;
            if (!PrepareTrafficNetwork(deadline)) return false;
            if (deadline != long.MaxValue && SwapPending())
            {
                bool sharedUpdate = NetworkPreparationUpdates != preparing;
                bool exhausted = budgetMilliseconds <= UpdateCityBudget.MinimumSliceMilliseconds && swapWaits < MaximumSwapWaits;
                if (sharedUpdate || exhausted) { NetworkSwapsDeferred++; if (!sharedUpdate) swapWaits++; return false; }
            }
            var before = trafficNetwork;
            RefreshTrafficClosures();
            if (!ReferenceEquals(before, trafficNetwork)) { NetworkPublished = true; NetworkPublications++; swapWaits = 0; }
            return true;
        }

        public const int MaximumSwapWaits = 4;
        private int swapWaits;
        public long NetworkSwapsDeferred { get; private set; }
        public long NetworkPublications { get; private set; }
        public bool NetworkPublished { get; private set; }

        private bool SwapPending() => routes is ITrafficRoutePlanner planner && planner.TrafficNetwork != null && !ReferenceEquals(planner.TrafficNetwork, trafficNetwork);

        private void ForgetPreparedNetwork()
        {
            if (preparedNetwork == null) return;
            preparedNetwork = null; preparedStage = preparedIndex = 0; preparedMinute = double.NaN;
            liveVehicles.Clear(); adoption.Clear();
        }

        private bool PrepareTrafficNetwork(long deadline)
        {
            if (!(routes is ITrafficRoutePlanner planner) || planner.TrafficNetwork == null) return true;
            var network = planner.TrafficNetwork;
            if (ReferenceEquals(network, trafficNetwork)) { ForgetPreparedNetwork(); return true; }
            if (!PreparedFor(network))
            {
                ForgetPreparedNetwork();
                preparedNetwork = network; preparedMinute = State.Minute; preparedHolds = State.PlayerHoldSequence;
                networkMinutes.Clear();
            }
            if (preparedStage == 3) return true;
            NetworkPreparationUpdates++;
            bool first = true;
            while (preparedStage < 3)
            {
                if (!first && System.Diagnostics.Stopwatch.GetTimestamp() >= deadline) return false;
                first = false;
                if (preparedStage == 0)
                {
                    if (preparedIndex < State.Citizens.Count) { MapForNetwork(network, State.Citizens[preparedIndex++]); continue; }
                    preparedStage = 1; preparedIndex = 0;
                    if (traffic != null && traffic.VehicleCount != 0 && State.TrafficTopologyId == network.TopologyId) traffic.CopyVehiclesInOrder(liveVehicles);
                    continue;
                }
                if (preparedStage == 2)
                {
                    if (!adoption.Done) { traffic!.StepAdoption(adoption); continue; }
                    preparedStage = 3;
                    continue;
                }
                if (preparedIndex < liveVehicles.Count)
                {
                    var vehicle = liveVehicles[preparedIndex++];
                    if (!ThroughTraffic.IsThroughVehicle(vehicle.VehicleId)) network.ValidateRemainingMapping(trafficCitizens[vehicle.VehicleId].Journey!, vehicle.Route, vehicle.NominalMinutes);
                    continue;
                }
                liveVehicles.Clear();
                preparedStage = 2;
                if (traffic != null && State.TrafficTopologyId == network.TopologyId) traffic.BeginAdoption(adoption, network.Links);
            }
            return true;
        }

        private void MapForNetwork(TrafficNetwork network, CitizenState citizen)
        {
            if (citizen.Phase != CitizenPhase.Travelling || citizen.Journey == null || trafficCitizens.ContainsKey(citizen.Identity.Id)) return;
            double progress = JourneyProgress(citizen), offset = 0;
            bool needsRoad = false;
            double totalMinutes = citizen.Journey.Minutes;
            foreach (var leg in citizen.Journey.Legs)
            {
                offset += leg.Minutes;
                if (offset > progress + JourneyClockTolerance(State.Minute, totalMinutes)
                    && leg.ServiceId.StartsWith("native:road:", StringComparison.Ordinal)) needsRoad = true;
            }
            if (!needsRoad) return;
            if (!network.TryMapRemainingSpans(citizen.Journey, MappingProgress(citizen, progress), mappingSpans))
                throw new InvalidOperationException("Committed road geometry no longer matches the native network; preserve the saved journey.");
            networkMinutes[citizen] = SpanMinute(citizen, progress);
        }

        public void RefreshTrafficClosures()
        {
            if (refreshingClosures) throw new InvalidOperationException("Traffic closure refresh cannot be nested.");
            refreshingClosures = true;
            try
            {
                EnsureTrafficNetwork();
                if (traffic == null || !(routes is ITrafficRoutePlanner planner)) return;
                traffic.CopyRelevantLinkIds(closureLinks);
                var batch = routes as ITrafficClosureBatchPlanner;
                batch?.BeginTrafficClosureBatch();
                try { foreach (int id in closureLinks) traffic.SetLinkClosed(id, planner.IsTrafficLinkClosed(id)); }
                finally { batch?.EndTrafficClosureBatch(); }
                CancelBlockedThroughCars();
            }
            finally { closureLinks.Clear(); refreshingClosures = false; }
        }

        private sealed class ForecastHook
        {
            internal CitySimulator? City;
            internal TrafficLinkForecast Link(int id, double at) => City?.traffic is TrafficKernel kernel ? kernel.Forecast(id, at) : default;
            internal double Entry(int id, double at, double fraction, double share) => City?.traffic is TrafficKernel kernel ? kernel.ForecastEntry(id, at, fraction, share) : 0;
        }
        private ForecastHook? forecastHook;
        private Func<int, double, TrafficLinkForecast>? linkForecast;
        private Func<int, double, double, double, double>? entryForecast;
        internal bool OwnsPlannerHooks => forecastHook?.City == this;

        internal void ReleasePlannerHooks(CitySimulator replacement)
        {
            if (forecastHook != null) forecastHook.City = null;
            if (ReferenceEquals(routes, replacement.routes) && replacement.OwnsPlannerHooks) return;
            if (routes is ITrafficRoutePlanner planner) planner.SetTrafficForecast(null);
            if (routes is ITrafficEntryRoutePlanner entryPlanner) entryPlanner.SetTrafficEntryForecast(null);
        }

        private void PublishTrafficSnapshot()
        {
            State.SnapshotSynchronizer = CaptureTraffic;
            State.SnapshotSteps = CaptureTrafficInSteps;
            State.TrafficSource = null; State.TrafficSourceState = null;
        }

        private void RestoreTraffic(TravelTiming.RetimedTraffic? retimed, bool publishSnapshot)
        {
            if (publishSnapshot) PublishTrafficSnapshot();
            if (State.Traffic == null) return;
            if (State.Traffic.Clock != State.Minute || string.IsNullOrEmpty(State.TrafficTopologyId))
                throw new ArgumentException("Traffic and city clocks/topology do not match.");
            traffic = retimed != null && ReferenceEquals(retimed.Source, State.Traffic) ? retimed.Kernel : TrafficKernel.Restore(State.Traffic);
            traffic.VehicleCompleted += TrafficCompleted;
            foreach (var vehicle in State.Traffic.Vehicles)
            {
                if (!citizenEvents.TryGetValue(vehicle.VehicleId, out var item) || item.Citizen.Phase != CitizenPhase.Travelling)
                    throw new ArgumentException("Traffic vehicle has no travelling resident.");
                trafficCitizens.Add(vehicle.VehicleId, item.Citizen);
                due.Remove(item);
            }
            foreach (var vehicle in State.Traffic.Vehicles)
                if (vehicle.Completed) TrafficCompleted(vehicle.VehicleId, vehicle.CompletedAt!.Value);
        }

        private void EnsureTrafficNetwork()
        {
            if (!(routes is ITrafficRoutePlanner planner) || planner.TrafficNetwork == null) return;
            var network = planner.TrafficNetwork;
            if (ReferenceEquals(network, trafficNetwork)) return;
            bool prepared = NetworkPrepared(network);
            if (!prepared)
            {
                ForgetPreparedNetwork();
                networkMinutes.Clear();
                foreach (var citizen in State.Citizens) MapForNetwork(network, citizen);
            }
            var preparedKernel = adoption.Kernel;
            preparedNetwork = null; preparedStage = preparedIndex = 0; preparedMinute = double.NaN; liveVehicles.Clear();
            if (throughTraffic != null && traffic != null) DropThroughCars(throughTraffic);
            if (traffic != null && State.TrafficTopologyId != network.TopologyId && traffic.VehicleCount != 0)
                throw new InvalidOperationException("The road topology changed during committed traffic; preserve the saved state.");
            if (!prepared && traffic != null && traffic.VehicleCount != 0)
            {
                traffic.CopyVehiclesInOrder(liveVehicles);
                try
                {
                    foreach (var vehicle in liveVehicles)
                        if (!ThroughTraffic.IsThroughVehicle(vehicle.VehicleId)) network.ValidateRemainingMapping(trafficCitizens[vehicle.VehicleId].Journey!, vehicle.Route, vehicle.NominalMinutes);
                }
                finally { liveVehicles.Clear(); }
            }
            if (traffic == null || State.TrafficTopologyId != network.TopologyId)
            {
                traffic = TrafficKernel.SharingDefinitions(network.Links, State.Minute);
                traffic.VehicleCompleted += TrafficCompleted;
                State.TrafficTopologyId = network.TopologyId;
            }
            else if (!(prepared && ReferenceEquals(preparedKernel, traffic) ? traffic.FinishAdoption(adoption, network.Links) : traffic.AdoptCompatibleDefinitions(network.Links)))
                throw new InvalidOperationException("Saved road section policy disagrees with its topology/timing.");
            adoption.Clear();
            trafficNetwork = network;
            var hook = forecastHook ??= new ForecastHook { City = this };
            planner.SetTrafficForecast(linkForecast ??= hook.Link);
            if (routes is ITrafficEntryRoutePlanner entryPlanner)
                entryPlanner.SetTrafficEntryForecast(entryForecast ??= hook.Entry);
            RequeueForNetwork();
        }

        private void RequeueForNetwork()
        {
            try { RequeueChanged(); }
            finally { networkMinutes.Clear(); }
        }

        private void RequeueChanged()
        {
            int queued = 0;
            foreach (var item in citizenEvents.Values)
            {
                if (InProgress(item) || trafficCitizens.ContainsKey(item.Citizen.Identity.Id)) { due.Remove(item); continue; }
                double minute = networkMinutes.TryGetValue(item.Citizen, out double mapped) ? mapped : ScheduledMinute(item.Citizen);
                queued++;
                if (minute.Equals(item.Minute) && due.Contains(item)) continue;
                due.Remove(item); item.Minute = minute; due.Add(item);
            }
            if (due.Count == queued) return;
            due.Clear();
            foreach (var item in citizenEvents.Values)
                if (!InProgress(item) && !trafficCitizens.ContainsKey(item.Citizen.Identity.Id))
                { item.Minute = ScheduledMinute(item.Citizen); due.Add(item); }
        }

        private readonly List<TrafficNetwork.MappedSpan> mappingSpans = new List<TrafficNetwork.MappedSpan>();
        private readonly List<TrafficVehicleState> liveVehicles = new List<TrafficVehicleState>();
        private readonly Dictionary<CitizenState, double> networkMinutes = new Dictionary<CitizenState, double>();

        private double ScheduledMinute(CitizenState citizen)
        {
            if (trafficNetwork != null && citizen.Phase == CitizenPhase.Travelling && citizen.Journey != null
                && trafficNetwork.TryMapRemainingSpans(citizen.Journey, MappingProgress(citizen, JourneyProgress(citizen)), mappingSpans))
                return SpanMinute(citizen, JourneyProgress(citizen));
            return citizen.NextEventMinute;
        }

        private double SpanMinute(CitizenState citizen, double progress)
        {
            double totalMinutes = citizen.Journey!.Minutes;
            foreach (var leg in mappingSpans)
                if (progress < leg.EndNominalMinutes - JourneyClockTolerance(State.Minute, totalMinutes))
                    return Math.Max(State.Minute, citizen.DepartureMinute + leg.StartNominalMinutes);
            return citizen.NextEventMinute;
        }

        private bool TryStartTraffic(CitizenState citizen)
        {
            if (traffic == null || trafficNetwork == null || citizen.Phase != CitizenPhase.Travelling || citizen.Journey == null
                || !trafficNetwork.TryMapRemaining(citizen.Journey, MappingProgress(citizen, JourneyProgress(citizen)), out var mapped)) return false;
            double progress = JourneyProgress(citizen);
            double tolerance = JourneyClockTolerance(State.Minute, citizen.Journey.Minutes);
            if (progress + tolerance < mapped[0].StartNominalMinutes || progress >= mapped[mapped.Count - 1].EndNominalMinutes - tolerance) return false;
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
            CountResidentCar();
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
            if (traffic != null && ThroughCompleted(id)) return;
            if (traffic == null || !trafficCitizens.TryGetValue(id, out var citizen)
                || !traffic.TryGetVehicle(id, out var vehicle) || !vehicle.IsCompleted || citizen.Journey == null)
                throw new InvalidOperationException("Traffic completion lost its resident ownership.");
            double progress = vehicle.NominalMinutes;
            citizen.DepartureMinute = at - progress;
            citizen.NextEventMinute = at + Math.Max(0, citizen.Journey.Minutes - progress);
            trafficCitizens.Remove(id);
            traffic.RemoveCompleted(id);
            var item = citizenEvents[id];
            item.Minute = citizen.NextEventMinute;
            if (!due.Add(item)) throw new InvalidOperationException("Traffic completion duplicated a resident event.");
        }

        private double JourneyProgress(CitizenState citizen)
        {
            if (traffic != null && traffic.TryGetVehicle(citizen.Identity.Id, out var vehicle)) return vehicle.NominalMinutes;
            double elapsed = State.Minute - citizen.DepartureMinute;
            return citizen.Journey == null ? elapsed : NormalizeJourneyElapsed(elapsed, citizen.Journey.Minutes, State.Minute);
        }

        public bool TryGetTrafficPermission(string citizenId, out TrafficVehicleSnapshot permission)
        {
            permission = default;
            return traffic != null && traffic.TryGetVehicle(citizenId, out permission);
        }

        public bool TryGetTrafficSignalGreen(CitizenState citizen, double routeMinutes, out bool green)
        {
            green = false;
            if (citizen == null) throw new ArgumentNullException(nameof(citizen));
            if (traffic == null || trafficNetwork == null || citizen.Phase != CitizenPhase.Travelling || citizen.Journey == null
                || !Position.Finite(routeMinutes)) return false;
            double tolerance = JourneyClockTolerance(State.Minute, citizen.Journey.Minutes);
            if (!trafficNetwork.TryMapRemainingSpans(citizen.Journey, MappingProgress(citizen, routeMinutes), mappingSpans)) return false;
            foreach (var span in mappingSpans)
                if (routeMinutes <= span.EndNominalMinutes + tolerance)
                {
                    if (routeMinutes + tolerance < span.StartNominalMinutes) return false;
                    green = traffic.IsSignalGreen(span.LinkId);
                    return true;
                }
            return false;
        }

        internal double RoutineRetryMinute(CitizenState citizen)
        {
            if (traffic != null && trafficCitizens.ContainsKey(citizen.Identity.Id))
                return State.Minute - traffic.GetPermission(citizen.Identity.Id).NominalMinutes + citizen.Journey!.Minutes;
            return citizen.NextEventMinute;
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
            State.Traffic = traffic == null ? null : ThroughCarCount > 0 ? traffic.Capture(ThroughTraffic.IsThroughVehicle) : traffic.CaptureShared();
            NoteTrafficSource(ThroughCarCount == 0);
        }

        private void NoteTrafficSource(bool whole)
        {
            State.TrafficSource = whole && State.Traffic != null ? traffic : null;
            State.TrafficSourceState = State.TrafficSource != null ? State.Traffic : null;
        }

        private IEnumerator<bool> CaptureTrafficInSteps(int linksPerStep)
        {
            SynchronizeSnapshot();
            if (traffic == null) { State.Traffic = null; NoteTrafficSource(false); yield return true; yield break; }
            bool whole = ThroughCarCount == 0;
            var capture = whole ? traffic.CaptureSharedInSteps(linksPerStep) : traffic.CaptureInSteps(ThroughTraffic.IsThroughVehicle, linksPerStep);
            while (capture.MoveNext())
            {
                if (capture.Current != null) { State.Traffic = capture.Current; NoteTrafficSource(whole); yield return true; yield break; }
                yield return false;
            }
            throw new InvalidOperationException("Traffic capture ended without a result.");
        }

        public void SynchronizeSnapshot() => SynchronizeTrafficJourneys();
        internal int TrafficJourneyCount => traffic == null ? 0 : trafficCitizens.Count;
    }
}
