using System;
using System.Collections.Generic;
using System.Linq;

namespace PersistentCity.Core
{
    /// <summary>
    /// Sparse FIFO finite-storage traffic. Every mutation uses Clock, and every processed event moves
    /// at most one incoming vehicle and one terminal vehicle. No wall clock, actors, or per-car ticking.
    /// </summary>
    public sealed class TrafficKernel
    {
        private sealed class Link
        {
            internal readonly TrafficLinkDefinition Definition;
            internal readonly TrafficLinkState State;
            internal readonly LinkedList<string> Queue = new LinkedList<string>();
            internal readonly Queue<string> Sources = new Queue<string>();
            internal readonly SortedSet<int> Incoming = new SortedSet<int>();
            internal TrafficEventState? Event;
            internal Link(TrafficLinkDefinition definition)
            { Definition = definition; State = new TrafficLinkState { LinkId = definition.Id }; }
        }
        private sealed class EventComparer : IComparer<TrafficEventState>
        {
            public int Compare(TrafficEventState? a, TrafficEventState? b)
            {
                if (ReferenceEquals(a, b)) return 0;
                if (a == null) return -1;
                if (b == null) return 1;
                int c = a.At.CompareTo(b.At);
                if (c == 0) c = a.Order.CompareTo(b.Order);
                return c == 0 ? a.LinkId.CompareTo(b.LinkId) : c;
            }
        }
        private readonly SortedDictionary<int, Link> links = new SortedDictionary<int, Link>();
        private readonly SortedSet<int> activeLinks = new SortedSet<int>();
        private readonly SortedSet<int> closedLinks = new SortedSet<int>();
        private readonly Dictionary<string, TrafficVehicleState> vehicles = new Dictionary<string, TrafficVehicleState>(StringComparer.Ordinal);
        private readonly SortedSet<TrafficEventState> events = new SortedSet<TrafficEventState>(new EventComparer());
        private const int ForecastMaximumLinks = 32;
        private const int ForecastMaximumVehicles = 64;
        private const int ForecastMaximumReferences = 1024;
        private const int ForecastMaximumEvents = 256;
        private long nextEventOrder;
        private long nextVehicleOrder;
        public double Clock { get; private set; }
        public double RequestedTime { get; private set; }
        public int VehicleCount => vehicles.Count;
        public int PendingEventCount => events.Count;
        public int LinkCount => links.Count;
        public event Action<string, double>? VehicleCompleted;
        /// <summary>Stable detached closure-refresh set: demand/occupancy plus blocked heads' immediate receivers.</summary>
        public IEnumerable<int> RelevantLinkIds
        {
            get
            {
                var result = new List<int>();
                CopyRelevantLinkIds(result);
                return result;
            }
        }
        // Keep the detached ascending order while allowing the per-frame caller to reuse its buffer.
        // Sorting and compacting avoids allocating a tree node for every relevant section each frame.
        internal void CopyRelevantLinkIds(List<int> result)
        {
            result.Clear();
            foreach (int id in closedLinks) result.Add(id);
            foreach (int id in activeLinks)
            {
                Link link = links[id]; result.Add(id);
                if (link.Queue.First == null) continue;
                TrafficVehicleState head = vehicles[link.Queue.First.Value];
                if (head.RouteIndex + 1 < head.Route.Count) result.Add(head.Route[head.RouteIndex + 1].LinkId);
            }
            result.Sort();
            int count = 0;
            for (int i = 0; i < result.Count; i++)
                if (count == 0 || result[i] != result[count - 1]) result[count++] = result[i];
            if (count < result.Count) result.RemoveRange(count, result.Count - count);
        }
        public double? NextEventTime => events.Count == 0 ? (double?)null : events.Min!.At;
        public double NextEventMinute => NextEventTime ?? double.PositiveInfinity;

        public TrafficKernel(IEnumerable<TrafficLinkDefinition> network, double initialClock = 0)
        {
            RequireTime(initialClock, nameof(initialClock));
            if (network == null) throw new ArgumentNullException(nameof(network));
            Clock = RequestedTime = initialClock;
            foreach (TrafficLinkDefinition definition in network)
            {
                ValidateDefinition(definition);
                if (links.ContainsKey(definition.Id)) throw new ArgumentException("Duplicate traffic link ID.", nameof(network));
                links.Add(definition.Id, new Link(CopyDefinition(definition)));
            }
        }

        public void Enqueue(string vehicleId, IReadOnlyList<TrafficRouteLeg> route)
        {
            if (string.IsNullOrWhiteSpace(vehicleId)) throw new ArgumentException("A stable vehicle ID is required.", nameof(vehicleId));
            if (vehicles.ContainsKey(vehicleId)) throw new ArgumentException("Vehicle already owns traffic state.", nameof(vehicleId));
            List<TrafficRouteLeg> saved = ValidateAndCopyRoute(route);
            var vehicle = new TrafficVehicleState
            {
                VehicleId = vehicleId, Route = saved, Order = NextOrder(ref nextVehicleOrder), EnqueuedAt = Clock,
                NominalMinutes = saved[0].StartNominalMinutes
            };
            vehicles.Add(vehicleId, vehicle);
            for (int i = 1; i < saved.Count; i++) links[saved[i].LinkId].Incoming.Add(saved[i - 1].LinkId);
            Link first = links[saved[0].LinkId];
            first.Sources.Enqueue(vehicleId);
            Reconsider(first);
        }

        /// <summary>When interrupted, continue toward RequestedTime. Clock never jumps over unprocessed events.</summary>
        public TrafficAdvanceResult AdvanceTo(double targetMinute, int maximumEvents)
        {
            RequireTime(targetMinute, nameof(targetMinute));
            if (targetMinute < Clock || targetMinute < RequestedTime) throw new ArgumentOutOfRangeException(nameof(targetMinute));
            if (maximumEvents < 0) throw new ArgumentOutOfRangeException(nameof(maximumEvents));
            RequestedTime = targetMinute;
            int processed = 0;
            while (events.Count != 0 && events.Min!.At <= targetMinute && processed < maximumEvents)
            {
                TrafficEventState item = events.Min!;
                events.Remove(item);
                Link link = links[item.LinkId];
                link.Event = null;
                Clock = item.At;
                Process(link);
                processed++;
            }
            bool reached = events.Count == 0 || events.Min!.At > targetMinute;
            if (reached) Clock = targetMinute;
            return new TrafficAdvanceResult(Clock, RequestedTime, processed, reached);
        }

        public bool TryGetVehicle(string vehicleId, out TrafficVehicleSnapshot snapshot)
        {
            if (!vehicles.TryGetValue(vehicleId, out TrafficVehicleState? vehicle)) { snapshot = default; return false; }
            snapshot = Snapshot(vehicle);
            return true;
        }
        public TrafficVehicleSnapshot GetPermission(string vehicleId) => Snapshot(FindVehicle(vehicleId));

        /// <summary>Check a rebuilt native network without copying all queues or replacing saved logical signal phase.</summary>
        public bool HasCompatibleDefinitions(IReadOnlyList<TrafficLinkDefinition> network)
        {
            if (network == null || network.Count != links.Count) return false;
            foreach (var current in network)
            {
                if (!links.TryGetValue(current.Id, out var link)) return false;
                var saved = link.Definition;
                if (saved.Capacity != current.Capacity || !SameDuration(saved.FreeFlowMinutes, current.FreeFlowMinutes)
                    || !SameDuration(saved.HeadwayMinutes, current.HeadwayMinutes) || (saved.Signal == null) != (current.Signal == null)) return false;
                if (saved.Signal != null && current.Signal != null
                    && (!SameDuration(saved.Signal.CycleMinutes, current.Signal.CycleMinutes)
                        || !SameDuration(saved.Signal.GreenDurationMinutes, current.Signal.GreenDurationMinutes)
                        || !SameDuration(saved.Signal.GreenStartMinutes, current.Signal.GreenStartMinutes))) return false;
            }
            return true;
        }

        private static bool SameDuration(double a, double b) => Math.Abs(a - b) <= 1e-9 * Math.Max(1, Math.Max(Math.Abs(a), Math.Abs(b)));

        /// <summary>Acquisition/release preserves identity, demand order and occupied link. Call at the simulation cursor.</summary>
        public void SetPhysicalControl(string vehicleId, bool physicalControl)
        {
            TrafficVehicleState vehicle = FindVehicle(vehicleId);
            if (vehicle.Completed || vehicle.PhysicalControl == physicalControl) return;
            TrafficVehicleSnapshot previous = Snapshot(vehicle);
            vehicle.NominalMinutes = previous.NominalMinutes;
            vehicle.PhysicalControl = physicalControl;
            if (vehicle.RouteIndex >= 0 && !physicalControl)
            {
                TrafficRouteLeg leg = vehicle.Route[vehicle.RouteIndex];
                double fraction = (vehicle.NominalMinutes - leg.StartNominalMinutes) / (leg.EndNominalMinutes - leg.StartNominalMinutes);
                vehicle.MotionStartFraction = fraction;
                vehicle.MotionStartedAt = Clock;
                vehicle.ReadyAt = Clock + (1 - fraction) * leg.FreeFlowMinutes;
            }
            if (vehicle.RouteIndex >= 0) Reconsider(links[vehicle.Route[vehicle.RouteIndex].LinkId]);
        }

        /// <summary>
        /// Reports cannot grant queue priority or completion. The accepted coordinate is monotone and
        /// capped by permission; the native adapter must brake before MaximumNominalMinutes itself.
        /// </summary>
        public double ReportPhysicalProgress(string vehicleId, double nominalMinutes)
        {
            RequireTime(nominalMinutes, nameof(nominalMinutes));
            TrafficVehicleState vehicle = FindVehicle(vehicleId);
            if (!vehicle.PhysicalControl) throw new InvalidOperationException("Vehicle has no physical control lease.");
            if (vehicle.Completed) return vehicle.NominalMinutes;
            TrafficVehicleSnapshot permission = Snapshot(vehicle);
            vehicle.NominalMinutes = Math.Max(vehicle.NominalMinutes, Math.Min(nominalMinutes, permission.MaximumNominalMinutes));
            if (vehicle.RouteIndex >= 0) Reconsider(links[vehicle.Route[vehicle.RouteIndex].LinkId]);
            return vehicle.NominalMinutes;
        }

        public void SetLinkClosed(int linkId, bool closed)
        {
            Link link = FindLink(linkId);
            if (link.State.Closed == closed) return;
            link.State.Closed = closed;
            if (closed) closedLinks.Add(linkId); else closedLinks.Remove(linkId);
            Reconsider(link);
        }

        /// <summary>Only acknowledged terminal state can be discarded; rendering release uses SetPhysicalControl.</summary>
        public bool RemoveCompleted(string vehicleId)
        {
            if (!vehicles.TryGetValue(vehicleId, out TrafficVehicleState? vehicle) || !vehicle.Completed) return false;
            return vehicles.Remove(vehicleId);
        }

        public TrafficLinkForecast Forecast(int linkId, double entryMinute)
        {
            RequireTime(entryMinute, nameof(entryMinute));
            Link link = FindLink(linkId);
            double delay = link.State.Closed ? double.PositiveInfinity :
                Math.Max(0, link.State.NextDischargeAt - entryMinute) +
                (link.Queue.Count + link.Sources.Count) * link.Definition.HeadwayMinutes;
            if (!link.State.Closed)
            {
                double ready = Math.Max(Clock, entryMinute) + link.Definition.FreeFlowMinutes + delay;
                double green = link.Definition.Signal?.NextGreen(ready) ?? ready;
                delay += green - ready;
                if (link.Queue.Count >= link.Definition.Capacity && link.Queue.First != null)
                {
                    TrafficVehicleState head = vehicles[link.Queue.First.Value];
                    if (head.PhysicalControl)
                    {
                        TrafficRouteLeg leg = head.Route[head.RouteIndex];
                        double remainingFraction = (leg.EndNominalMinutes - head.NominalMinutes) /
                            (leg.EndNominalMinutes - leg.StartNominalMinutes);
                        double predicted = Math.Max(head.ReadyAt, Clock + remainingFraction * leg.FreeFlowMinutes);
                        predicted = Math.Max(predicted, link.State.NextDischargeAt);
                        predicted = link.Definition.Signal?.NextGreen(predicted) ?? predicted;
                        delay = Math.Max(delay, Math.Max(0, predicted - entryMinute));
                    }
                    if (head.RouteIndex + 1 < head.Route.Count && links[head.Route[head.RouteIndex + 1].LinkId].State.Closed)
                        delay = double.PositiveInfinity;
                }
            }
            if (link.Queue.Count >= link.Definition.Capacity && HasBlockedCycle(link)) delay = double.PositiveInfinity;
            return new TrafficLinkForecast(linkId, link.Queue.Count, link.Sources.Count, delay, link.State.Closed);
        }

        /// <summary>
        /// Extra, unweighted wait for an interior source's empty-section opportunity. Subtracts the
        /// queue/headway admission estimate already in Forecast, weighted by sectionShare. Current committed dependencies only;
        /// blocked queues or projection bounds return positive infinity. Physical copies extrapolate
        /// from observed progress at nominal speed; future collision/stop behavior is unknown.
        /// Boundary/empty fast paths allocate nothing. Projection is bounded to 32 links, 64 vehicles,
        /// 1024 inspected route/predecessor references and 256 events; it never mutates live queues.
        /// </summary>
        public double ForecastEntry(int linkId, double entryMinute, double startFraction, double sectionShare = 1)
        {
            RequireTime(entryMinute, nameof(entryMinute));
            if (!Finite(startFraction) || startFraction < 0 || startFraction >= 1) throw new ArgumentOutOfRangeException(nameof(startFraction));
            if (!Finite(sectionShare) || sectionShare <= 0 || sectionShare > 1) throw new ArgumentOutOfRangeException(nameof(sectionShare));
            Link link = FindLink(linkId);
            if (startFraction == 0) return 0;
            if (link.State.Closed) return double.PositiveInfinity;
            if (link.Queue.Count == 0 && link.Sources.Count == 0) return 0;
            if (link.Queue.Count + link.Sources.Count > ForecastMaximumVehicles) return double.PositiveInfinity;
            TrafficKernel? projection = BuildEntryProjection(linkId);
            if (projection == null) return double.PositiveInfinity;
            int remaining = ForecastMaximumEvents;
            double target = Math.Max(Clock, entryMinute);
            TrafficAdvanceResult advance = projection.AdvanceTo(target, remaining);
            remaining -= advance.ProcessedEvents;
            if (!advance.ReachedTarget) return double.PositiveInfinity;
            Link projected = projection.links[linkId];
            while (projected.Queue.Count != 0 || projected.Sources.Count != 0)
            {
                if (remaining-- == 0 || !projection.NextEventTime.HasValue) return double.PositiveInfinity;
                projection.AdvanceTo(Math.Max(target, projection.NextEventMinute), 1);
            }
            double included = Math.Max(0, link.State.NextDischargeAt - entryMinute)
                + (link.Queue.Count + link.Sources.Count) * link.Definition.HeadwayMinutes;
            if (link.Queue.Count >= link.Definition.Capacity && link.Queue.First != null)
            {
                TrafficVehicleState head = vehicles[link.Queue.First.Value];
                if (head.PhysicalControl)
                {
                    TrafficRouteLeg leg = head.Route[head.RouteIndex];
                    double residualFraction = (leg.EndNominalMinutes - head.NominalMinutes) / (leg.EndNominalMinutes - leg.StartNominalMinutes);
                    double predicted = Math.Max(head.ReadyAt, Clock + residualFraction * leg.FreeFlowMinutes);
                    predicted = Math.Max(predicted, link.State.NextDischargeAt);
                    predicted = link.Definition.Signal?.NextGreen(predicted) ?? predicted;
                    included = Math.Max(included, Math.Max(0, predicted - entryMinute));
                }
            }
            return Math.Max(0, projection.Clock - entryMinute - included * sectionShare);
        }

        private TrafficKernel? BuildEntryProjection(int firstId)
        {
            var selected = new HashSet<int>();
            var pending = new Queue<int>();
            var selectedVehicles = new List<TrafficVehicleState>();
            selected.Add(firstId); pending.Enqueue(firstId);
            int references = 0;
            bool Include(int id)
            {
                if (selected.Contains(id)) return true;
                if (selected.Count >= ForecastMaximumLinks) return false;
                selected.Add(id); pending.Enqueue(id); return true;
            }
            while (pending.Count != 0)
            {
                Link current = links[pending.Dequeue()];
                if (selectedVehicles.Count + current.Queue.Count + current.Sources.Count > ForecastMaximumVehicles) return null;
                foreach (string id in current.Queue.Concat(current.Sources))
                {
                    TrafficVehicleState vehicle = vehicles[id];
                    int start = Math.Max(0, vehicle.RouteIndex);
                    if (vehicle.Route.Count - start > ForecastMaximumReferences - references) return null;
                    selectedVehicles.Add(vehicle);
                    for (int index = start; index < vehicle.Route.Count; index++)
                    {
                        references++;
                        if (!Include(vehicle.Route[index].LinkId)) return null;
                    }
                }
                foreach (int incomingId in current.Incoming)
                {
                    if (++references > ForecastMaximumReferences) return null;
                    Link incoming = links[incomingId];
                    if ((incoming.Queue.Count != 0 || incoming.Sources.Count != 0) && !Include(incomingId)) return null;
                }
            }
            var projection = new TrafficKernel(selected.OrderBy(id => id).Select(id => links[id].Definition), Clock);
            foreach (int id in selected)
            {
                Link original = links[id], copy = projection.links[id];
                copy.State.Closed = original.State.Closed;
                copy.State.NextDischargeAt = original.State.NextDischargeAt;
                copy.State.MergeCursor = original.State.MergeCursor;
                foreach (string vehicle in original.Queue) copy.Queue.AddLast(vehicle);
                foreach (string vehicle in original.Sources) copy.Sources.Enqueue(vehicle);
            }
            foreach (TrafficVehicleState original in selectedVehicles)
            {
                int start = Math.Max(0, original.RouteIndex);
                var copy = new TrafficVehicleState
                {
                    VehicleId = original.VehicleId, Order = original.Order, RouteIndex = original.RouteIndex < 0 ? -1 : 0,
                    EnqueuedAt = original.EnqueuedAt, EnteredAt = original.EnteredAt, ReadyAt = original.ReadyAt,
                    MotionStartedAt = original.MotionStartedAt, MotionStartFraction = original.MotionStartFraction,
                    NominalMinutes = original.NominalMinutes, PhysicalControl = original.PhysicalControl
                };
                for (int index = start; index < original.Route.Count; index++)
                {
                    TrafficRouteLeg leg = original.Route[index];
                    copy.Route.Add(new TrafficRouteLeg { LinkId = leg.LinkId, StartNominalMinutes = leg.StartNominalMinutes,
                        EndNominalMinutes = leg.EndNominalMinutes, FreeFlowMinutes = leg.FreeFlowMinutes,
                        StartLinkFraction = leg.StartLinkFraction, EndLinkFraction = leg.EndLinkFraction });
                    if (copy.Route.Count > 1) projection.links[leg.LinkId].Incoming.Add(copy.Route[copy.Route.Count - 2].LinkId);
                }
                projection.vehicles.Add(copy.VehicleId, copy);
            }
            foreach (Link copy in projection.links.Values) projection.Reconsider(copy);
            // This is an ETA projection, never authority over a live actor. Extrapolate
            // measured physical points at nominal speed without moving live ownership.
            foreach (TrafficVehicleState original in selectedVehicles)
                if (original.PhysicalControl) projection.SetPhysicalControl(original.VehicleId, false);
            return projection;
        }

        private bool HasBlockedCycle(Link first)
        {
            var visited = new HashSet<int>();
            Link current = first;
            int references = 0;
            for (int inspected = 0; inspected < 64; inspected++)
            {
                if (current.Queue.Count < current.Definition.Capacity || current.Queue.First == null) return false;
                // Only geometrically possible curb exits can break a full-storage cycle.
                for (var node = current.Queue.First; node != null; node = node.Next)
                {
                    if (++references > ForecastMaximumReferences) return false;
                    if (IsCurbExit(vehicles[node.Value]) && MayCurbFreeStorage(current, node, ref references)) return false;
                }
                if (!visited.Add(current.Definition.Id)) return true;
                TrafficVehicleState head = vehicles[current.Queue.First.Value];
                if (head.RouteIndex + 1 >= head.Route.Count) return false;
                current = links[head.Route[head.RouteIndex + 1].LinkId];
                if (current.State.Closed) return true;
            }
            return false; // An unproven long dependency chain is not classified as a cycle.
        }

        private bool MayCurbFreeStorage(Link link, LinkedListNode<string> target, ref int references)
        {
            TrafficVehicleState vehicle = vehicles[target.Value];
            double end = vehicle.Route[vehicle.RouteIndex].EndLinkFraction;
            int separation = 0;
            for (var predecessor = target.Previous; predecessor != null; predecessor = predecessor.Previous)
            {
                if (++references > ForecastMaximumReferences) return true; // Unknown, not a proven cycle.
                TrafficVehicleState ahead = vehicles[predecessor.Value];
                if (end + ++separation / (double)link.Definition.Capacity > ahead.Route[ahead.RouteIndex].EndLinkFraction) return false;
            }
            return true;
        }

        private void Process(Link link)
        {
            string? completedId = null;
            // A curb exit does not cross the far-end stop line. It can leave behind a
            // predecessor that has already passed its destination, retaining through FIFO.
            for (var node = link.Queue.First; node != null; node = node.Next)
            {
                TrafficVehicleState vehicle = vehicles[node.Value];
                if (vehicle.RouteIndex != vehicle.Route.Count - 1) continue;
                bool curb = IsCurbExit(vehicle);
                if ((!curb && node != link.Queue.First) ||
                    (curb ? CurbExitAt(link, node) : EligibleAt(link, vehicle)) > Clock) continue;
                if (curb) link.Queue.Remove(node);
                else RemoveHead(link);
                vehicle.NominalMinutes = vehicle.Route[vehicle.RouteIndex].EndNominalMinutes;
                vehicle.Completed = true;
                vehicle.CompletedAt = Clock;
                completedId = vehicle.VehicleId;
                break;
            }
            if (!link.State.Closed && link.Queue.Count < link.Definition.Capacity)
            {
                int? candidate = SelectIncoming(link);
                if (candidate.HasValue)
                {
                    TrafficVehicleState vehicle;
                    if (candidate.Value == -1)
                    {
                        vehicle = vehicles[link.Sources.Dequeue()];
                        vehicle.RouteIndex = 0;
                    }
                    else
                    {
                        Link source = links[candidate.Value];
                        vehicle = vehicles[source.Queue.First!.Value];
                        RemoveHead(source);
                        vehicle.RouteIndex++;
                        Reconsider(source);
                    }
                    link.State.MergeCursor = candidate.Value;
                    TrafficRouteLeg leg = vehicle.Route[vehicle.RouteIndex];
                    vehicle.EnteredAt = vehicle.MotionStartedAt = Clock;
                    vehicle.MotionStartFraction = 0;
                    vehicle.ReadyAt = Clock + leg.FreeFlowMinutes;
                    vehicle.NominalMinutes = leg.StartNominalMinutes;
                    link.Queue.AddLast(vehicle.VehicleId);
                }
            }
            Reconsider(link);
            if (completedId != null) VehicleCompleted?.Invoke(completedId, Clock);
        }

        private void RemoveHead(Link source)
        {
            source.Queue.RemoveFirst();
            source.State.NextDischargeAt = Clock + source.Definition.HeadwayMinutes;
        }

        private int? SelectIncoming(Link destination)
        {
            int? wrap = null, after = null;
            bool sourceEligible = destination.Sources.Count > 0 && CanReceive(destination, vehicles[destination.Sources.Peek()], 0);
            if (sourceEligible) wrap = -1;
            foreach (int id in destination.Incoming)
            {
                Link source = links[id];
                if (!Feeds(source, destination.Definition.Id, out TrafficVehicleState? vehicle) || EligibleAt(source, vehicle!) > Clock ||
                    !CanReceive(destination, vehicle!, vehicle!.RouteIndex + 1)) continue;
                if (!wrap.HasValue) wrap = id;
                if (id > destination.State.MergeCursor && !after.HasValue) after = id;
            }
            if (sourceEligible && -1 > destination.State.MergeCursor) return -1;
            return after ?? wrap;
        }

        private void Reconsider(Link link)
        {
            UpdateActivity(link);
            if (link.Queue.First != null)
            {
                TrafficVehicleState head = vehicles[link.Queue.First.Value];
                double at = EligibleAt(link, head);
                if (head.RouteIndex == head.Route.Count - 1 && !IsCurbExit(head)) Schedule(link, at);
                else if (head.RouteIndex + 1 < head.Route.Count)
                {
                    Link destination = links[head.Route[head.RouteIndex + 1].LinkId];
                    if (CanReceive(destination, head, head.RouteIndex + 1)) Schedule(destination, at);
                }
            }
            for (var node = link.Queue.First; node != null; node = node.Next)
                if (IsCurbExit(vehicles[node.Value])) Schedule(link, CurbExitAt(link, node));
            if (link.State.Closed || link.Queue.Count >= link.Definition.Capacity) return;
            if (link.Sources.Count > 0 && CanReceive(link, vehicles[link.Sources.Peek()], 0)) Schedule(link, Clock);
            foreach (int sourceId in link.Incoming)
            {
                Link source = links[sourceId];
                if (Feeds(source, link.Definition.Id, out TrafficVehicleState? vehicle) && CanReceive(link, vehicle!, vehicle!.RouteIndex + 1))
                    Schedule(link, EligibleAt(source, vehicle!));
            }
        }

        private void UpdateActivity(Link link)
        {
            if (link.Queue.Count != 0 || link.Sources.Count != 0) activeLinks.Add(link.Definition.Id);
            else activeLinks.Remove(link.Definition.Id);
        }

        private static bool CanReceive(Link destination, TrafficVehicleState vehicle, int routeIndex)
        {
            // Interior source points cannot jump ahead of an existing FIFO queue. Waiting for an
            // empty section is conservative and wakes on discharge, never on a polling timer.
            return !destination.State.Closed && destination.Queue.Count < destination.Definition.Capacity &&
                (vehicle.Route[routeIndex].StartLinkFraction == 0 || destination.Queue.Count == 0);
        }

        private bool Feeds(Link source, int destinationId, out TrafficVehicleState? vehicle)
        {
            vehicle = source.Queue.First == null ? null : vehicles[source.Queue.First.Value];
            return vehicle != null && vehicle.RouteIndex + 1 < vehicle.Route.Count
                && vehicle.Route[vehicle.RouteIndex + 1].LinkId == destinationId;
        }

        private double EligibleAt(Link source, TrafficVehicleState vehicle)
        {
            TrafficRouteLeg leg = vehicle.Route[vehicle.RouteIndex];
            if (vehicle.PhysicalControl && vehicle.NominalMinutes < leg.EndNominalMinutes) return double.PositiveInfinity;
            double ready = Math.Max(Clock, Math.Max(vehicle.ReadyAt, source.State.NextDischargeAt));
            var signal = source.Definition.Signal;
            return signal == null ? ready : signal.NextGreen(ready);
        }

        private static bool IsCurbExit(TrafficVehicleState vehicle) => vehicle.RouteIndex == vehicle.Route.Count - 1
            && vehicle.Route[vehicle.RouteIndex].EndLinkFraction < 1;

        private double CurbExitAt(Link link, LinkedListNode<string> target)
        {
            TrafficVehicleState vehicle = vehicles[target.Value];
            TrafficRouteLeg leg = vehicle.Route[vehicle.RouteIndex];
            if (vehicle.PhysicalControl && vehicle.NominalMinutes < leg.EndNominalMinutes) return double.PositiveInfinity;
            double at = Math.Max(Clock, vehicle.ReadyAt);
            int separation = 0;
            // Each predecessor must clear the curb point plus one storage spacing.
            // Physical blockers wake this link on observation/release; background motion
            // supplies an analytical threshold, never a periodic retry.
            for (var predecessor = target.Previous; predecessor != null; predecessor = predecessor.Previous)
            {
                double required = leg.EndLinkFraction + ++separation / (double)link.Definition.Capacity;
                TrafficVehicleState ahead = vehicles[predecessor.Value];
                TrafficRouteLeg aheadLeg = ahead.Route[ahead.RouteIndex];
                if (required > aheadLeg.EndLinkFraction) return double.PositiveInfinity;
                double fraction = (required - aheadLeg.StartLinkFraction) / (aheadLeg.EndLinkFraction - aheadLeg.StartLinkFraction);
                double actual = (ahead.NominalMinutes - aheadLeg.StartNominalMinutes) / (aheadLeg.EndNominalMinutes - aheadLeg.StartNominalMinutes);
                if (actual >= fraction) continue;
                if (ahead.PhysicalControl) return double.PositiveInfinity;
                at = Math.Max(at, ahead.MotionStartedAt + Math.Max(0, fraction - ahead.MotionStartFraction) * aheadLeg.FreeFlowMinutes);
            }
            return at;
        }

        private void Schedule(Link link, double at)
        {
            if (double.IsPositiveInfinity(at)) return;
            at = Math.Max(Clock, at);
            if (link.Event != null)
            {
                if (link.Event.At <= at) return;
                events.Remove(link.Event);
            }
            var item = new TrafficEventState { LinkId = link.Definition.Id, At = at, Order = NextOrder(ref nextEventOrder) };
            link.Event = item;
            events.Add(item);
        }

        private TrafficVehicleSnapshot Snapshot(TrafficVehicleState vehicle)
        {
            if (vehicle.Completed)
                return new TrafficVehicleSnapshot(vehicle.VehicleId, vehicle.RouteIndex, null, vehicle.NominalMinutes,
                    vehicle.NominalMinutes, false, vehicle.PhysicalControl, true, vehicle.CompletedAt);
            if (vehicle.RouteIndex < 0)
                return new TrafficVehicleSnapshot(vehicle.VehicleId, -1, null, vehicle.NominalMinutes,
                    vehicle.NominalMinutes, false, vehicle.PhysicalControl, false, null);
            TrafficRouteLeg leg = vehicle.Route[vehicle.RouteIndex];
            Link link = links[leg.LinkId];
            int rank = 0;
            double previousFraction = 0;
            double maximum = vehicle.NominalMinutes;
            foreach (string id in link.Queue)
            {
                TrafficVehicleState occupant = vehicles[id];
                TrafficRouteLeg occupantLeg = occupant.Route[occupant.RouteIndex];
                double span = occupantLeg.EndNominalMinutes - occupantLeg.StartNominalMinutes;
                double floor = (occupant.NominalMinutes - occupantLeg.StartNominalMinutes) / span;
                double motion = Math.Min(1, occupant.MotionStartFraction + Math.Max(0, Clock - occupant.MotionStartedAt) / occupantLeg.FreeFlowMinutes);
                double spatialSpan = occupantLeg.EndLinkFraction - occupantLeg.StartLinkFraction;
                // A head has no predecessor. Constructing a synthetic point beyond the link
                // and subtracting storage spacing can leave its endpoint one ulp unreachable.
                double queueLimit = rank == 0 ? 1 : Math.Max(0, Math.Min(1,
                    (previousFraction - 1.0 / link.Definition.Capacity - occupantLeg.StartLinkFraction) / spatialSpan));
                double allowed = Math.Max(floor, Math.Min(motion, queueLimit));
                if (id == vehicle.VehicleId)
                {
                    // The committed endpoint is exact route evidence, not a reconstructed span.
                    maximum = allowed == 1 ? occupantLeg.EndNominalMinutes : occupantLeg.StartNominalMinutes + allowed * span;
                    // Reconstructing an already observed interior floor can round down. Keeping
                    // owned progress cannot grant new spacing or motion permission.
                    maximum = Math.Max(occupant.NominalMinutes, maximum);
                    break;
                }
                previousFraction = occupantLeg.StartLinkFraction + (occupant.PhysicalControl ? floor : allowed) * spatialSpan;
                rank++;
            }
            double progress = vehicle.PhysicalControl ? vehicle.NominalMinutes : maximum;
            bool cross = rank == 0 && Math.Max(vehicle.ReadyAt, link.State.NextDischargeAt) <= Clock
                && (link.Definition.Signal?.NextGreen(Clock) ?? Clock) <= Clock;
            if (cross && vehicle.RouteIndex + 1 < vehicle.Route.Count)
            {
                Link destination = links[vehicle.Route[vehicle.RouteIndex + 1].LinkId];
                cross = CanReceive(destination, vehicle, vehicle.RouteIndex + 1);
            }
            if (IsCurbExit(vehicle)) cross = CurbExitAt(link, link.Queue.Find(vehicle.VehicleId)!) <= Clock;
            return new TrafficVehicleSnapshot(vehicle.VehicleId, vehicle.RouteIndex, leg.LinkId, progress,
                maximum, cross, vehicle.PhysicalControl, false, null);
        }

        public TrafficKernelState Capture()
        {
            var state = new TrafficKernelState
            {
                Clock = Clock, RequestedTime = RequestedTime, NextEventOrder = nextEventOrder, NextVehicleOrder = nextVehicleOrder
            };
            foreach (Link link in links.Values)
            {
                state.Definitions.Add(CopyDefinition(link.Definition));
                state.Links.Add(new TrafficLinkState
                {
                    LinkId = link.Definition.Id, Closed = link.State.Closed, NextDischargeAt = link.State.NextDischargeAt,
                    MergeCursor = link.State.MergeCursor, Vehicles = link.Queue.ToList(), WaitingSources = link.Sources.ToList(),
                    IncomingLinks = link.Incoming.ToList()
                });
            }
            foreach (TrafficVehicleState vehicle in vehicles.Values.OrderBy(v => v.Order)) state.Vehicles.Add(CopyVehicle(vehicle));
            foreach (TrafficEventState item in events) state.Events.Add(new TrafficEventState { LinkId = item.LinkId, At = item.At, Order = item.Order });
            return state;
        }

        /// <summary>Restores a detached snapshot. By default, saved physical leases resume background travel from actual progress; false preserves exact leases for in-memory continuation.</summary>
        public static TrafficKernel Restore(TrafficKernelState state, bool releasePhysicalControl = true)
            => Restore(state, releasePhysicalControl, false);

        // Retiming can reconstruct derived wakeups. Saved states must already supply timely wakeups.
        private static TrafficKernel Restore(TrafficKernelState state, bool releasePhysicalControl, bool rebuildWakeups)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Schema != 2) throw new ArgumentException("Unsupported traffic schema.", nameof(state));
            RequireTime(state.Clock, nameof(state));
            RequireTime(state.RequestedTime, nameof(state));
            if (state.RequestedTime < state.Clock || state.NextEventOrder < 0 || state.NextVehicleOrder < 0 ||
                state.Definitions == null || state.Links == null || state.Vehicles == null || state.Events == null)
                throw new ArgumentException("Invalid traffic cursor.", nameof(state));
            var kernel = new TrafficKernel(state.Definitions, state.Clock)
            {
                RequestedTime = state.RequestedTime, nextEventOrder = state.NextEventOrder, nextVehicleOrder = state.NextVehicleOrder
            };
            var orders = new HashSet<long>();
            foreach (TrafficVehicleState vehicle in state.Vehicles)
            {
                if (vehicle == null || string.IsNullOrWhiteSpace(vehicle.VehicleId) || vehicle.Order < 0 ||
                    vehicle.Order >= state.NextVehicleOrder || !orders.Add(vehicle.Order) || kernel.vehicles.ContainsKey(vehicle.VehicleId))
                    throw new ArgumentException("Invalid traffic vehicle identity/order.", nameof(state));
                List<TrafficRouteLeg> route = kernel.ValidateAndCopyRoute(vehicle.Route);
                RequireTime(vehicle.EnqueuedAt, nameof(state));
                RequireTime(vehicle.EnteredAt, nameof(state));
                RequireTime(vehicle.ReadyAt, nameof(state));
                RequireTime(vehicle.MotionStartedAt, nameof(state));
                RequireTime(vehicle.NominalMinutes, nameof(state));
                if (vehicle.EnqueuedAt > state.Clock || vehicle.EnteredAt > state.Clock || vehicle.MotionStartedAt > state.Clock ||
                    !Finite(vehicle.MotionStartFraction) || vehicle.MotionStartFraction < 0 || vehicle.MotionStartFraction > 1 ||
                    vehicle.RouteIndex < -1 || vehicle.RouteIndex >= route.Count ||
                    (vehicle.RouteIndex >= 0 && (vehicle.ReadyAt < vehicle.EnteredAt || vehicle.MotionStartedAt < vehicle.EnteredAt)) ||
                    (vehicle.Completed != vehicle.CompletedAt.HasValue) || (vehicle.Completed && vehicle.RouteIndex != route.Count - 1))
                    throw new ArgumentException("Invalid traffic vehicle cursor.", nameof(state));
                TrafficRouteLeg leg = route[Math.Max(0, vehicle.RouteIndex)];
                if (vehicle.NominalMinutes < leg.StartNominalMinutes || vehicle.NominalMinutes > leg.EndNominalMinutes ||
                    (vehicle.RouteIndex < 0 && vehicle.NominalMinutes != leg.StartNominalMinutes) ||
                    (vehicle.Completed && vehicle.NominalMinutes != leg.EndNominalMinutes))
                    throw new ArgumentException("Invalid traffic geometry cursor.", nameof(state));
                if (vehicle.CompletedAt.HasValue && (!Finite(vehicle.CompletedAt.Value) || vehicle.CompletedAt < vehicle.EnqueuedAt || vehicle.CompletedAt > state.Clock))
                    throw new ArgumentException("Invalid traffic completion time.", nameof(state));
                if (!vehicle.Completed && vehicle.RouteIndex >= 0)
                {
                    double motionReady = vehicle.MotionStartedAt + (1 - vehicle.MotionStartFraction) * leg.FreeFlowMinutes;
                    double tolerance = 4 * Ulp(Math.Max(state.Clock, Math.Max(motionReady, vehicle.ReadyAt)));
                    if (!Finite(motionReady) || vehicle.ReadyAt < motionReady - tolerance)
                        throw new ArgumentException("Traffic readiness precedes its committed motion schedule.", nameof(state));
                }
                TrafficVehicleState copy = CopyVehicle(vehicle);
                copy.Route = route;
                kernel.vehicles.Add(vehicle.VehicleId, copy);
            }
            var seenLinks = new HashSet<int>();
            var occupied = new HashSet<string>(StringComparer.Ordinal);
            foreach (TrafficLinkState saved in state.Links)
            {
                if (saved == null || !kernel.links.TryGetValue(saved.LinkId, out Link? link) || !seenLinks.Add(saved.LinkId) ||
                    saved.Vehicles == null || saved.WaitingSources == null || saved.IncomingLinks == null ||
                    saved.Vehicles.Count > link.Definition.Capacity || saved.MergeCursor < -1 ||
                    (saved.MergeCursor >= 0 && !kernel.links.ContainsKey(saved.MergeCursor)))
                    throw new ArgumentException("Invalid traffic link state.", nameof(state));
                RequireTime(saved.NextDischargeAt, nameof(state));
                link.State.Closed = saved.Closed;
                if (saved.Closed) kernel.closedLinks.Add(saved.LinkId);
                link.State.NextDischargeAt = saved.NextDischargeAt;
                link.State.MergeCursor = saved.MergeCursor;
                foreach (int source in saved.IncomingLinks)
                    if (!kernel.links.ContainsKey(source) || source == saved.LinkId || !link.Incoming.Add(source))
                        throw new ArgumentException("Invalid traffic predecessor.", nameof(state));
                foreach (string id in saved.Vehicles)
                {
                    TrafficVehicleState vehicle = kernel.FindRestoredVehicle(id, occupied);
                    if (vehicle.Completed || vehicle.RouteIndex < 0 || vehicle.Route[vehicle.RouteIndex].LinkId != saved.LinkId)
                        throw new ArgumentException("Traffic queue and vehicle cursor disagree.", nameof(state));
                    link.Queue.AddLast(id);
                }
                foreach (string id in saved.WaitingSources)
                {
                    TrafficVehicleState vehicle = kernel.FindRestoredVehicle(id, occupied);
                    if (vehicle.Completed || vehicle.RouteIndex != -1 || vehicle.Route[0].LinkId != saved.LinkId)
                        throw new ArgumentException("Traffic source queue and vehicle cursor disagree.", nameof(state));
                    link.Sources.Enqueue(id);
                }
            }
            if (seenLinks.Count != kernel.links.Count || occupied.Count != kernel.vehicles.Values.Count(v => !v.Completed))
                throw new ArgumentException("Traffic occupancy is incomplete.", nameof(state));
            foreach (TrafficVehicleState vehicle in kernel.vehicles.Values)
                for (int i = 1; i < vehicle.Route.Count; i++)
                    if (!kernel.links[vehicle.Route[i].LinkId].Incoming.Contains(vehicle.Route[i - 1].LinkId))
                        throw new ArgumentException("Missing traffic predecessor.", nameof(state));
            var eventOrders = new HashSet<long>();
            foreach (TrafficEventState saved in state.Events)
            {
                if (saved == null || !kernel.links.TryGetValue(saved.LinkId, out Link? link) || link.Event != null ||
                    !Finite(saved.At) || saved.At < state.Clock || saved.Order < 0 || saved.Order >= state.NextEventOrder || !eventOrders.Add(saved.Order))
                    throw new ArgumentException("Invalid traffic event cursor.", nameof(state));
                var item = new TrafficEventState { LinkId = saved.LinkId, At = saved.At, Order = saved.Order };
                link.Event = item;
                kernel.events.Add(item);
            }
            foreach (Link link in kernel.links.Values) kernel.UpdateActivity(link);
            if (rebuildWakeups)
                foreach (Link link in kernel.links.Values) kernel.Reconsider(link);
            kernel.ValidateRequiredWakeups();
            if (releasePhysicalControl)
                foreach (TrafficVehicleState vehicle in kernel.vehicles.Values.OrderBy(v => v.Order))
                    if (vehicle.PhysicalControl)
                    {
                        if (vehicle.Completed) vehicle.PhysicalControl = false;
                        else kernel.SetPhysicalControl(vehicle.VehicleId, false);
                    }
            return kernel;
        }

        /// <summary>Throws on unreadable schemas, invalid identity/order/cursors, or inconsistent finite occupancy.</summary>
        public static void ValidateState(TrafficKernelState state) => Restore(state, false);

        /// <summary>Explicit demand cancellation, never used for body disposal. Other vehicles retain FIFO order.</summary>
        public bool Cancel(string vehicleId)
        {
            if (!vehicles.TryGetValue(vehicleId, out TrafficVehicleState? vehicle)) return false;
            if (!vehicle.Completed)
            {
                Link link = links[vehicle.Route[Math.Max(0, vehicle.RouteIndex)].LinkId];
                if (vehicle.RouteIndex >= 0) link.Queue.Remove(vehicleId);
                else
                {
                    int count = link.Sources.Count;
                    for (int i = 0; i < count; i++)
                    {
                        string id = link.Sources.Dequeue();
                        if (id != vehicleId) link.Sources.Enqueue(id);
                    }
                }
                vehicles.Remove(vehicleId);
                Reconsider(link);
            }
            else vehicles.Remove(vehicleId);
            return true;
        }

        /// <summary>
        /// Returns a detached kernel with future residual travel/headway/signal times scaled around
        /// the current Clock. Route geometry coordinates and historical/completion timestamps remain
        /// unchanged unless mapNominalMinutes is supplied. The active leg is clipped to its actual
        /// spatial point; the optional map must be affine over its remaining moving geometry.
        /// </summary>
        public TrafficKernel RescaleTime(double factor, Func<string, double, double>? mapNominalMinutes = null)
        {
            if (!Finite(factor) || factor <= 0) throw new ArgumentOutOfRangeException(nameof(factor));
            TrafficKernelState state = Capture();
            state.RequestedTime = Clock + (RequestedTime - Clock) * factor;
            foreach (TrafficLinkDefinition definition in state.Definitions)
            {
                definition.FreeFlowMinutes *= factor;
                definition.HeadwayMinutes *= factor;
                if (definition.Signal != null)
                {
                    definition.Signal.CycleMinutes *= factor;
                    definition.Signal.GreenStartMinutes *= factor;
                    definition.Signal.GreenDurationMinutes *= factor;
                    definition.Signal.PhaseOffsetMinutes = Clock + (definition.Signal.PhaseOffsetMinutes - Clock) * factor;
                }
            }
            foreach (TrafficLinkState link in state.Links)
                link.NextDischargeAt = Clock + Math.Max(0, link.NextDischargeAt - Clock) * factor;
            foreach (TrafficVehicleState vehicle in state.Vehicles)
            {
                int currentIndex = vehicle.RouteIndex;
                bool active = currentIndex >= 0 && !vehicle.Completed;
                double actual = Snapshot(vehicles[vehicle.VehicleId]).NominalMinutes;
                double motionCoordinate = actual, oldActualFraction = 0, oldDuration = 0, actualLinkFraction = 0;
                if (active)
                {
                    TrafficRouteLeg current = vehicle.Route[currentIndex];
                    double span = current.EndNominalMinutes - current.StartNominalMinutes;
                    oldActualFraction = (actual - current.StartNominalMinutes) / span;
                    oldDuration = current.FreeFlowMinutes;
                    actualLinkFraction = current.StartLinkFraction + oldActualFraction * (current.EndLinkFraction - current.StartLinkFraction);
                    double motionFraction = Math.Min(1, vehicle.MotionStartFraction +
                        Math.Max(0, Clock - vehicle.MotionStartedAt) / oldDuration);
                    motionCoordinate = current.StartNominalMinutes + motionFraction * span;
                    vehicle.MotionStartedAt = Clock;
                    vehicle.ReadyAt = Clock + Math.Max(0, vehicle.ReadyAt - Clock) * factor;
                }
                foreach (TrafficRouteLeg leg in vehicle.Route)
                {
                    leg.FreeFlowMinutes *= factor;
                    if (mapNominalMinutes != null)
                    {
                        leg.StartNominalMinutes = mapNominalMinutes(vehicle.VehicleId, leg.StartNominalMinutes);
                        leg.EndNominalMinutes = mapNominalMinutes(vehicle.VehicleId, leg.EndNominalMinutes);
                    }
                }
                vehicle.NominalMinutes = mapNominalMinutes == null ? actual : mapNominalMinutes(vehicle.VehicleId, actual);
                if (active)
                {
                    TrafficRouteLeg current = vehicle.Route[currentIndex];
                    if (oldActualFraction < 1)
                    {
                        // Retain the actual full-section spatial point when a completed nominal
                        // prefix is fixed while remaining route coordinates are retimed.
                        // A near-end point can round onto the endpoint in either coordinate
                        // system. Keep one representable residual step; a physical lease still
                        // requires an explicit endpoint observation before it can complete.
                        if (vehicle.NominalMinutes >= current.EndNominalMinutes)
                            vehicle.NominalMinutes = PreviousDouble(current.EndNominalMinutes);
                        current.StartNominalMinutes = vehicle.NominalMinutes;
                        current.StartLinkFraction = actualLinkFraction < current.EndLinkFraction
                            ? actualLinkFraction : PreviousDouble(current.EndLinkFraction);
                        current.FreeFlowMinutes = oldDuration * (1 - oldActualFraction) * factor;
                    }
                    double span = current.EndNominalMinutes - current.StartNominalMinutes;
                    double mappedMotion = mapNominalMinutes == null ? motionCoordinate : mapNominalMinutes(vehicle.VehicleId, motionCoordinate);
                    vehicle.MotionStartFraction = Math.Max(0, Math.Min(1, (mappedMotion - current.StartNominalMinutes) / span));
                    // Scaling an already rounded absolute readiness can amplify its error
                    // beyond the reconstructed residual motion's tolerance. Keep any later
                    // readiness constraint, but never discharge before that motion finishes.
                    double motionReady = vehicle.MotionStartedAt + (1 - vehicle.MotionStartFraction) * current.FreeFlowMinutes;
                    vehicle.ReadyAt = Math.Max(vehicle.ReadyAt, motionReady);
                }
            }
            foreach (TrafficEventState item in state.Events) item.At = Clock + (item.At - Clock) * factor;
            // Signal/curb calculations can round earlier than independently scaled events.
            // Reconsider in stable link order before validation: retain safe early events,
            // bring late ones forward, and let the normal admission guards decide movement.
            return Restore(state, false, true);
        }

        private void ValidateRequiredWakeups()
        {
            foreach (Link link in links.Values)
            {
                if (link.Queue.First != null)
                {
                    TrafficVehicleState head = vehicles[link.Queue.First.Value];
                    double at = EligibleAt(link, head);
                    if (head.RouteIndex == head.Route.Count - 1 && !IsCurbExit(head)) RequireScheduled(link, at);
                    else if (head.RouteIndex + 1 < head.Route.Count)
                    {
                        Link destination = links[head.Route[head.RouteIndex + 1].LinkId];
                        if (CanReceive(destination, head, head.RouteIndex + 1)) RequireScheduled(destination, at);
                    }
                }
                for (var node = link.Queue.First; node != null; node = node.Next)
                    if (IsCurbExit(vehicles[node.Value])) RequireScheduled(link, CurbExitAt(link, node));
                if (link.State.Closed || link.Queue.Count >= link.Definition.Capacity) continue;
                if (link.Sources.Count > 0 && CanReceive(link, vehicles[link.Sources.Peek()], 0)) RequireScheduled(link, Clock);
                foreach (int incoming in link.Incoming)
                {
                    Link source = links[incoming];
                    if (Feeds(source, link.Definition.Id, out TrafficVehicleState? vehicle) && CanReceive(link, vehicle!, vehicle!.RouteIndex + 1))
                        RequireScheduled(link, EligibleAt(source, vehicle!));
                }
            }
        }
        private void RequireScheduled(Link link, double at)
        {
            if (double.IsNaN(at)) throw new ArgumentException("Traffic wakeup is not representable.");
            if (double.IsPositiveInfinity(at)) return;
            double expected = Math.Max(Clock, at);
            if (link.Event == null || (link.Event.At > expected && link.Event.At - expected > 4 * Ulp(expected)))
                throw new ArgumentException("Traffic state is missing a required queue wakeup.");
        }

        private TrafficVehicleState FindRestoredVehicle(string id, HashSet<string> occupied)
        {
            if (id == null || !vehicles.TryGetValue(id, out TrafficVehicleState? vehicle) || !occupied.Add(id))
                throw new ArgumentException("Duplicate or unknown occupied vehicle.");
            return vehicle;
        }
        private TrafficVehicleState FindVehicle(string id) => vehicles.TryGetValue(id, out TrafficVehicleState? value)
            ? value : throw new KeyNotFoundException("Unknown traffic vehicle: " + id);
        private Link FindLink(int id) => links.TryGetValue(id, out Link? value)
            ? value : throw new KeyNotFoundException("Unknown traffic link: " + id);
        private List<TrafficRouteLeg> ValidateAndCopyRoute(IReadOnlyList<TrafficRouteLeg> route)
        {
            if (route == null || route.Count == 0) throw new ArgumentException("Traffic needs a committed route.", nameof(route));
            var copy = new List<TrafficRouteLeg>(route.Count);
            double previous = 0;
            int previousLink = -1;
            foreach (TrafficRouteLeg leg in route)
            {
                if (leg == null || !links.ContainsKey(leg.LinkId) || !Finite(leg.StartNominalMinutes) || !Finite(leg.EndNominalMinutes) ||
                    leg.StartNominalMinutes < previous || leg.EndNominalMinutes <= leg.StartNominalMinutes || leg.LinkId == previousLink ||
                    !Finite(leg.FreeFlowMinutes) || leg.FreeFlowMinutes < 0 ||
                    !Finite(leg.StartLinkFraction) || !Finite(leg.EndLinkFraction) || leg.StartLinkFraction < 0 ||
                    leg.EndLinkFraction > 1 || leg.StartLinkFraction >= leg.EndLinkFraction)
                    throw new ArgumentException("Invalid traffic route geometry/link.", nameof(route));
                copy.Add(new TrafficRouteLeg { LinkId = leg.LinkId, StartNominalMinutes = leg.StartNominalMinutes, EndNominalMinutes = leg.EndNominalMinutes,
                    FreeFlowMinutes = leg.FreeFlowMinutes > 0 ? leg.FreeFlowMinutes : leg.EndNominalMinutes - leg.StartNominalMinutes,
                    StartLinkFraction = leg.StartLinkFraction, EndLinkFraction = leg.EndLinkFraction });
                previous = leg.EndNominalMinutes;
                previousLink = leg.LinkId;
            }
            return copy;
        }
        private static void ValidateDefinition(TrafficLinkDefinition definition)
        {
            if (definition == null || definition.Id < 0 || definition.Capacity < 1 || !Finite(definition.FreeFlowMinutes) ||
                definition.FreeFlowMinutes <= 0 || !Finite(definition.HeadwayMinutes) || definition.HeadwayMinutes < 0)
                throw new ArgumentException("Invalid finite traffic link.");
            TrafficSignalSchedule? signal = definition.Signal;
            if (signal != null && (!Finite(signal.CycleMinutes) || signal.CycleMinutes <= 0 || !Finite(signal.GreenStartMinutes) ||
                signal.GreenStartMinutes < 0 || signal.GreenStartMinutes >= signal.CycleMinutes || !Finite(signal.GreenDurationMinutes) ||
                signal.GreenDurationMinutes < 0 || signal.GreenDurationMinutes > signal.CycleMinutes || !Finite(signal.PhaseOffsetMinutes)))
                throw new ArgumentException("Invalid traffic signal schedule.");
        }
        private static TrafficLinkDefinition CopyDefinition(TrafficLinkDefinition value) => new TrafficLinkDefinition
        {
            Id = value.Id, Capacity = value.Capacity, FreeFlowMinutes = value.FreeFlowMinutes, HeadwayMinutes = value.HeadwayMinutes,
            Signal = value.Signal == null ? null : new TrafficSignalSchedule
            {
                CycleMinutes = value.Signal.CycleMinutes, GreenStartMinutes = value.Signal.GreenStartMinutes,
                GreenDurationMinutes = value.Signal.GreenDurationMinutes, PhaseOffsetMinutes = value.Signal.PhaseOffsetMinutes
            }
        };
        private static TrafficVehicleState CopyVehicle(TrafficVehicleState value) => new TrafficVehicleState
        {
            VehicleId = value.VehicleId, Order = value.Order,
            Route = value.Route.Select(leg => new TrafficRouteLeg { LinkId = leg.LinkId, StartNominalMinutes = leg.StartNominalMinutes, EndNominalMinutes = leg.EndNominalMinutes, FreeFlowMinutes = leg.FreeFlowMinutes, StartLinkFraction = leg.StartLinkFraction, EndLinkFraction = leg.EndLinkFraction }).ToList(),
            RouteIndex = value.RouteIndex, EnqueuedAt = value.EnqueuedAt, EnteredAt = value.EnteredAt,
            ReadyAt = value.ReadyAt, MotionStartedAt = value.MotionStartedAt, MotionStartFraction = value.MotionStartFraction,
            NominalMinutes = value.NominalMinutes, PhysicalControl = value.PhysicalControl,
            Completed = value.Completed, CompletedAt = value.CompletedAt
        };
        private static long NextOrder(ref long counter)
        {
            if (counter == long.MaxValue) throw new InvalidOperationException("Traffic order exhausted.");
            return counter++;
        }
        private static double PreviousDouble(double value) => BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(value) - 1);
        private static double Ulp(double value)
        {
            double next = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(value) + 1);
            return double.IsPositiveInfinity(next) ? value - PreviousDouble(value) : next - value;
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static void RequireTime(double value, string name)
        { if (!Finite(value) || value < 0) throw new ArgumentOutOfRangeException(name); }
    }
}
