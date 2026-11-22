using System;
using System.Collections.Generic;
using System.Linq;

namespace PersistentCity.Core
{
    public sealed class TrafficKernel
    {
        internal sealed class Link
        {
            internal TrafficLinkDefinition Definition;
            internal bool Closed;
            internal double NextDischargeAt;
            internal int MergeCursor = -1;
            private static readonly LinkedList<TrafficVehicleState> NoVehicles = new LinkedList<TrafficVehicleState>();
            private static readonly Queue<string> NoSources = new Queue<string>();
            private LinkedList<TrafficVehicleState>? queue;
            private Queue<string>? sources;
            private TrafficEventState? linkEvent;
            internal LinkedList<TrafficVehicleState> Queue => queue ?? NoVehicles;
            internal LinkedList<TrafficVehicleState> OwnQueue => queue ??= new LinkedList<TrafficVehicleState>();
            internal Queue<string> Sources => sources ?? NoSources;
            internal Queue<string> OwnSources => sources ??= new Queue<string>();
            internal void ClearSources() => sources?.Clear();
            internal readonly SortedIds Incoming = new SortedIds();
            internal TrafficEventState Event => linkEvent ??= new TrafficEventState { LinkId = Definition.Id };
            internal int HeapIndex = -1;
            internal bool Seen;
            internal bool Scheduled => HeapIndex >= 0;
            internal Link(TrafficLinkDefinition definition) { Definition = definition; }
        }
        private sealed class EventComparer : IComparer<TrafficEventState>
        {
            internal static readonly EventComparer Instance = new EventComparer();
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
        private sealed class EventHeap
        {
            private Link[] items = new Link[16];
            public int Count { get; private set; }
            public Link Min => items[0];

            public void Add(Link link)
            {
                if (Count == items.Length) Array.Resize(ref items, Count * 2);
                items[Count] = link;
                link.HeapIndex = Count++;
                Up(link.HeapIndex);
            }

            public void Remove(Link link)
            {
                int index = link.HeapIndex;
                link.HeapIndex = -1;
                Count--;
                if (index < Count)
                {
                    Link last = items[Count];
                    items[index] = last;
                    last.HeapIndex = index;
                    if (index > 0 && Less(last, items[(index - 1) / 2])) Up(index);
                    else Down(index);
                }
                items[Count] = null!;
            }

            public void Clear()
            {
                for (int i = 0; i < Count; i++) { items[i].HeapIndex = -1; items[i] = null!; }
                Count = 0;
            }

            public void CopyTo(List<TrafficEventState> into)
            {
                for (int i = 0; i < Count; i++) into.Add(items[i].Event);
            }

            private static bool Less(Link a, Link b) => EventComparer.Instance.Compare(a.Event, b.Event) < 0;

            private void Up(int index)
            {
                Link item = items[index];
                while (index > 0)
                {
                    int parent = (index - 1) / 2;
                    if (!Less(item, items[parent])) break;
                    items[index] = items[parent]; items[index].HeapIndex = index;
                    index = parent;
                }
                items[index] = item; item.HeapIndex = index;
            }

            private void Down(int index)
            {
                Link item = items[index];
                while (true)
                {
                    int child = index * 2 + 1;
                    if (child >= Count) break;
                    if (child + 1 < Count && Less(items[child + 1], items[child])) child++;
                    if (!Less(items[child], item)) break;
                    items[index] = items[child]; items[index].HeapIndex = index;
                    index = child;
                }
                items[index] = item; item.HeapIndex = index;
            }
        }
        private sealed class ProjectionScratch
        {
            internal readonly TrafficKernel Kernel = new TrafficKernel();
            internal readonly HashSet<int> Selected = new HashSet<int>();
            internal readonly Queue<int> Pending = new Queue<int>();
            internal readonly List<int> Order = new List<int>();
            internal readonly List<int> Used = new List<int>();
            internal readonly List<TrafficVehicleState> Vehicles = new List<TrafficVehicleState>();
            internal readonly List<TrafficVehicleState> Copies = new List<TrafficVehicleState>();
        }
        private readonly Dictionary<int, Link> links;
        private readonly List<Link> orderedLinks;
        private readonly SortedIds activeLinks = new SortedIds();
        private readonly SortedIds closedLinks = new SortedIds();
        private readonly Dictionary<string, TrafficVehicleState> vehicles;
        private readonly EventHeap events = new EventHeap();
        private readonly Stack<LinkedListNode<TrafficVehicleState>> spareNodes = new Stack<LinkedListNode<TrafficVehicleState>>();
        private ProjectionScratch? projectionScratch;
        private HashSet<int>? cycleVisited;
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
        public long Reconsiders { get; private set; }
        public long ProcessedEvents { get; private set; }
        public event Action<string, double>? VehicleCompleted;
        public IEnumerable<int> RelevantLinkIds
        {
            get
            {
                var result = new List<int>();
                CopyRelevantLinkIds(result);
                return result;
            }
        }
        private static readonly Comparison<int> AscendingIds = (a, b) => a.CompareTo(b);
        private static readonly Comparison<TrafficEventState> EventOrder = EventComparer.Instance.Compare;
        private static readonly Comparison<TrafficVehicleState> VehicleOrder = (a, b) => a.Order.CompareTo(b.Order);
        internal void CopyVehiclesInOrder(List<TrafficVehicleState> into)
        {
            into.Clear();
            foreach (var vehicle in vehicles.Values) into.Add(vehicle);
            into.Sort(VehicleOrder);
        }
        internal void CopyRelevantLinkIds(List<int> result)
        {
            result.Clear();
            foreach (int id in closedLinks) result.Add(id);
            foreach (int id in activeLinks)
            {
                Link link = links[id]; result.Add(id);
                if (link.Queue.First == null) continue;
                TrafficVehicleState head = link.Queue.First.Value;
                if (head.RouteIndex + 1 < head.Route.Count) result.Add(head.Route[head.RouteIndex + 1].LinkId);
            }
            result.Sort(AscendingIds);
            int count = 0;
            for (int i = 0; i < result.Count; i++)
                if (count == 0 || result[i] != result[count - 1]) result[count++] = result[i];
            if (count < result.Count) result.RemoveRange(count, result.Count - count);
        }
        public double? NextEventTime => events.Count == 0 ? (double?)null : events.Min.Event.At;
        public double NextEventMinute => NextEventTime ?? double.PositiveInfinity;

        internal void BeginObservationEpoch(double minute)
        {
            RequireTime(minute, nameof(minute));
            if (minute < Clock || NextEventMinute < minute) throw new InvalidOperationException("Unfinished traffic epoch.");
            Clock = minute; RequestedTime = Math.Max(RequestedTime, minute);
        }

        private TrafficKernel() : this(0, 0) { }

        public TrafficKernel(IEnumerable<TrafficLinkDefinition> network, double initialClock = 0)
            : this(0, network is ICollection<TrafficLinkDefinition> sized ? sized.Count : 0)
        {
            RequireTime(initialClock, nameof(initialClock));
            if (network == null) throw new ArgumentNullException(nameof(network));
            Clock = RequestedTime = initialClock;
            bool ascending = true;
            foreach (TrafficLinkDefinition definition in network) ascending &= AddDefinition(definition);
            if (!ascending) SortLinks();
        }

        private TrafficKernel(double initialClock, int linkCapacity, int vehicleCapacity = 0)
        {
            links = new Dictionary<int, Link>(linkCapacity);
            orderedLinks = new List<Link>(linkCapacity);
            vehicles = new Dictionary<string, TrafficVehicleState>(vehicleCapacity, StringComparer.Ordinal);
            Clock = RequestedTime = initialClock;
        }

        private bool AddDefinition(TrafficLinkDefinition definition, bool adopt = false)
        {
            ValidateDefinition(definition);
            if (links.ContainsKey(definition.Id)) throw new ArgumentException("Duplicate traffic link ID.", "network");
            var link = new Link(adopt ? definition : CopyDefinition(definition));
            links.Add(definition.Id, link);
            bool ascending = orderedLinks.Count == 0 || orderedLinks[orderedLinks.Count - 1].Definition.Id <= definition.Id;
            orderedLinks.Add(link);
            return ascending;
        }

        private void SortLinks() => orderedLinks.Sort((a, b) => a.Definition.Id.CompareTo(b.Definition.Id));

        private static T RunToEnd<T>(IEnumerator<T?> steps) where T : class
        {
            while (steps.MoveNext()) if (steps.Current != null) return steps.Current;
            throw new InvalidOperationException("Traffic steps ended without a result.");
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
            first.OwnSources.Enqueue(vehicleId);
            Reconsider(first);
        }

        public TrafficAdvanceResult AdvanceTo(double targetMinute, int maximumEvents)
        {
            RequireTime(targetMinute, nameof(targetMinute));
            if (targetMinute < Clock || targetMinute < RequestedTime) throw new ArgumentOutOfRangeException(nameof(targetMinute));
            if (maximumEvents < 0) throw new ArgumentOutOfRangeException(nameof(maximumEvents));
            RequestedTime = targetMinute;
            int processed = 0;
            while (events.Count != 0 && events.Min.Event.At <= targetMinute && processed < maximumEvents)
            {
                Link link = events.Min;
                events.Remove(link);
                Clock = link.Event.At;
                Process(link);
                processed++;
                ProcessedEvents++;
            }
            bool reached = events.Count == 0 || events.Min.Event.At > targetMinute;
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

        internal bool AdoptCompatibleDefinitions(IReadOnlyList<TrafficLinkDefinition> network)
        {
            if (network == null || network.Count != links.Count) return false;
            bool compatible = true;
            foreach (var current in network)
            {
                if (current == null || !links.TryGetValue(current.Id, out var link)) return false;
                var saved = link.Definition;
                if (ReferenceEquals(saved, current)) continue;
                if (current.IsFrozen() && Identical(saved, current)) { Rebind(link, current); continue; }
                if (saved.Capacity != current.Capacity || !SameDuration(saved.FreeFlowMinutes, current.FreeFlowMinutes)
                    || !SameDuration(saved.HeadwayMinutes, current.HeadwayMinutes) || (saved.Signal == null) != (current.Signal == null)) compatible = false;
                else if (saved.Signal != null && current.Signal != null
                    && (!SameDuration(saved.Signal.CycleMinutes, current.Signal.CycleMinutes)
                        || !SameDuration(saved.Signal.GreenDurationMinutes, current.Signal.GreenDurationMinutes)
                        || !SameDuration(saved.Signal.GreenStartMinutes, current.Signal.GreenStartMinutes))) compatible = false;
            }
            return compatible;
        }

        internal sealed class DefinitionAdoption
        {
            internal TrafficKernel? Kernel;
            internal IReadOnlyList<TrafficLinkDefinition>? Network;
            internal int Index;
            internal bool Compatible;
            internal readonly List<Link> Links = new List<Link>();
            internal readonly List<TrafficLinkDefinition> Definitions = new List<TrafficLinkDefinition>();
            internal bool Done => Network == null || Index >= Network.Count;
            internal void Clear() { Kernel = null; Network = null; Index = 0; Compatible = false; Links.Clear(); Definitions.Clear(); }
        }

        internal void BeginAdoption(DefinitionAdoption adoption, IReadOnlyList<TrafficLinkDefinition> network)
        {
            adoption.Clear();
            adoption.Kernel = this; adoption.Network = network;
            adoption.Compatible = network != null && network.Count == links.Count;
            if (!adoption.Compatible) adoption.Network = null;
        }

        internal void StepAdoption(DefinitionAdoption adoption)
        {
            var network = adoption.Network!;
            var current = network[adoption.Index++];
            if (current == null || !links.TryGetValue(current.Id, out var link)) { adoption.Compatible = false; adoption.Index = network.Count; return; }
            var saved = link.Definition;
            if (ReferenceEquals(saved, current)) return;
            if (current.IsFrozen() && Identical(saved, current)) { adoption.Links.Add(link); adoption.Definitions.Add(current); return; }
            if (saved.Capacity != current.Capacity || !SameDuration(saved.FreeFlowMinutes, current.FreeFlowMinutes)
                || !SameDuration(saved.HeadwayMinutes, current.HeadwayMinutes) || (saved.Signal == null) != (current.Signal == null)) adoption.Compatible = false;
            else if (saved.Signal != null && current.Signal != null
                && (!SameDuration(saved.Signal.CycleMinutes, current.Signal.CycleMinutes)
                    || !SameDuration(saved.Signal.GreenDurationMinutes, current.Signal.GreenDurationMinutes)
                    || !SameDuration(saved.Signal.GreenStartMinutes, current.Signal.GreenStartMinutes))) adoption.Compatible = false;
        }

        internal bool FinishAdoption(DefinitionAdoption adoption, IReadOnlyList<TrafficLinkDefinition> network)
        {
            if (!ReferenceEquals(adoption.Kernel, this) || !ReferenceEquals(adoption.Network, network) || !adoption.Done)
            { adoption.Clear(); return AdoptCompatibleDefinitions(network); }
            bool compatible = adoption.Compatible;
            for (int i = 0; i < adoption.Links.Count; i++) Rebind(adoption.Links[i], adoption.Definitions[i]);
            adoption.Clear();
            return compatible;
        }

        internal static TrafficKernel SharingDefinitions(IReadOnlyList<TrafficLinkDefinition> network, double initialClock)
        {
            RequireTime(initialClock, nameof(initialClock));
            if (network == null) throw new ArgumentNullException(nameof(network));
            var kernel = new TrafficKernel(initialClock, network.Count);
            bool ascending = true;
            for (int i = 0; i < network.Count; i++) ascending &= kernel.AddDefinition(network[i], adopt: network[i] != null && network[i].IsFrozen());
            if (!ascending) kernel.SortLinks();
            return kernel;
        }

        internal int DefinitionsSharedWith(IReadOnlyList<TrafficLinkDefinition> network)
        {
            int shared = 0;
            foreach (var current in network)
                if (current != null && links.TryGetValue(current.Id, out var link) && ReferenceEquals(link.Definition, current)) shared++;
            return shared;
        }

        private void Rebind(Link link, TrafficLinkDefinition definition)
        {
            link.Definition = definition;
            if (projectionScratch != null && projectionScratch.Kernel.links.TryGetValue(definition.Id, out Link? projected)) projected.Definition = definition;
        }

        internal int ProjectionDefinitionsNotLive()
        {
            if (projectionScratch == null) return 0;
            int stale = 0;
            foreach (var pair in projectionScratch.Kernel.links)
                if (!links.TryGetValue(pair.Key, out var live) || !ReferenceEquals(live.Definition, pair.Value.Definition)) stale++;
            return stale;
        }

        private static bool Identical(TrafficLinkDefinition a, TrafficLinkDefinition b)
        {
            if (a.Id != b.Id || a.Capacity != b.Capacity || !Bits(a.FreeFlowMinutes, b.FreeFlowMinutes) || !Bits(a.HeadwayMinutes, b.HeadwayMinutes)) return false;
            if (a.Signal == null || b.Signal == null) return a.Signal == null && b.Signal == null;
            return Bits(a.Signal.CycleMinutes, b.Signal.CycleMinutes) && Bits(a.Signal.GreenStartMinutes, b.Signal.GreenStartMinutes)
                && Bits(a.Signal.GreenDurationMinutes, b.Signal.GreenDurationMinutes) && Bits(a.Signal.PhaseOffsetMinutes, b.Signal.PhaseOffsetMinutes);
        }

        private static bool Bits(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);

        private static bool SameDuration(double a, double b) => Math.Abs(a - b) <= 1e-9 * Math.Max(1, Math.Max(Math.Abs(a), Math.Abs(b)));

        public TrafficHoldResult HoldVehicle(string vehicleId, double minutes, double maximumCharge = double.PositiveInfinity)
        {
            if (!Finite(minutes) || minutes <= 0) throw new ArgumentOutOfRangeException(nameof(minutes));
            if (double.IsNaN(maximumCharge) || maximumCharge <= 0) throw new ArgumentOutOfRangeException(nameof(maximumCharge));
            if (vehicleId == null || !vehicles.TryGetValue(vehicleId, out TrafficVehicleState? vehicle)
                || vehicle.Completed || vehicle.RouteIndex < 0) return default;
            TrafficRouteLeg leg = vehicle.Route[vehicle.RouteIndex];
            Link link = links[leg.LinkId];
            LinkedListNode<TrafficVehicleState>? node = link.Queue.Find(vehicle);
            if (node == null) return default;
            double free = leg.FreeFlowMinutes;
            double now = Math.Min(1, vehicle.MotionStartFraction + Math.Max(0, Clock - vehicle.MotionStartedAt) / free);
            double motion = Math.Min(1, vehicle.MotionStartFraction + Math.Max(0, Clock - minutes - vehicle.MotionStartedAt) / free);
            double floor = (vehicle.NominalMinutes - leg.StartNominalMinutes) / (leg.EndNominalMinutes - leg.StartNominalMinutes);
            double spatial = leg.EndLinkFraction - leg.StartLinkFraction;
            double follower = double.NaN, spacing = 0;
            if (node.Next != null)
            {
                follower = SpatialPosition(link, node.Next);
                spacing = (follower + 1.0 / link.Definition.Capacity - leg.StartLinkFraction) / spatial;
            }
            double u = Math.Min(now, Math.Max(Math.Max(motion, 0), Math.Max(floor, spacing)));
            double start = vehicle.MotionStartFraction, at = vehicle.MotionStartedAt, ready = vehicle.ReadyAt;
            double before = DischargeAt(link, node);
            void Set(double value)
            {
                if (value < now) { vehicle.MotionStartFraction = value; vehicle.MotionStartedAt = Clock; }
                else { vehicle.MotionStartFraction = start; vehicle.MotionStartedAt = at; }
                vehicle.ReadyAt = Math.Max(ready, Clock + (1 - value) * free);
            }
            Set(u);
            for (int step = 0; node.Next != null && u < now && SpatialPosition(link, node.Next) < follower; step++)
            {
                u = step < 64 ? Math.Min(now, NextDouble(u)) : now;
                Set(u);
            }
            double Charge()
            {
                double after = DischargeAt(link, node);
                return double.IsPositiveInfinity(before) || double.IsPositiveInfinity(after) ? 0 : Math.Max(0, after - before);
            }
            double charge = Charge();
            if (charge > maximumCharge)
            {
                double low = u, high = now;
                for (int step = 0; step < 128; step++)
                {
                    double middle = low + (high - low) / 2;
                    if (middle <= low || middle >= high) break;
                    Set(middle);
                    if (Charge() > maximumCharge) low = middle; else high = middle;
                }
                u = high;
                Set(u);
                charge = Charge();
            }
            if (!(u < now) && !(charge > 0) || charge > maximumCharge)
            {
                vehicle.MotionStartFraction = start; vehicle.MotionStartedAt = at; vehicle.ReadyAt = ready;
                return default;
            }
            Reconsider(link);
            return new TrafficHoldResult(true, charge, vehicle.MotionStartFraction, vehicle.MotionStartedAt, vehicle.ReadyAt);
        }

        private double DischargeAt(Link link, LinkedListNode<TrafficVehicleState> node)
            => IsCurbExit(node.Value) ? CurbExitAt(link, node) : EligibleAt(link, node.Value);

        private double SpatialPosition(Link link, LinkedListNode<TrafficVehicleState> target)
        {
            double previous = 0;
            int rank = 0;
            for (var queued = link.Queue.First; queued != null; queued = queued.Next, rank++)
            {
                TrafficVehicleState occupant = queued.Value;
                TrafficRouteLeg leg = occupant.Route[occupant.RouteIndex];
                double floor = (occupant.NominalMinutes - leg.StartNominalMinutes) / (leg.EndNominalMinutes - leg.StartNominalMinutes);
                double motion = Math.Min(1, occupant.MotionStartFraction + Math.Max(0, Clock - occupant.MotionStartedAt) / leg.FreeFlowMinutes);
                double spatial = leg.EndLinkFraction - leg.StartLinkFraction;
                double limit = rank == 0 ? 1 : Math.Max(0, Math.Min(1, (previous - 1.0 / link.Definition.Capacity - leg.StartLinkFraction) / spatial));
                double allowed = Math.Max(floor, Math.Min(motion, limit));
                previous = leg.StartLinkFraction + allowed * spatial;
                if (ReferenceEquals(queued, target)) return previous;
            }
            throw new InvalidOperationException("The vehicle is not queued on its link.");
        }

        public bool IsSignalGreen(int linkId) => (FindLink(linkId).Definition.Signal?.NextGreen(Clock) ?? Clock) <= Clock;

        public void SetLinkClosed(int linkId, bool closed)
        {
            Link link = FindLink(linkId);
            if (link.Closed == closed) return;
            link.Closed = closed;
            if (closed) closedLinks.Add(linkId); else closedLinks.Remove(linkId);
            Reconsider(link);
        }

        internal bool IsBlockedByClosure(string vehicleId)
        {
            if (!vehicles.TryGetValue(vehicleId, out TrafficVehicleState? vehicle) || vehicle.Completed) return false;
            if (vehicle.RouteIndex < 0) return links[vehicle.Route[0].LinkId].Closed;
            Link link = links[vehicle.Route[vehicle.RouteIndex].LinkId];
            return vehicle.RouteIndex + 1 < vehicle.Route.Count && ReferenceEquals(link.Queue.First?.Value, vehicle)
                && links[vehicle.Route[vehicle.RouteIndex + 1].LinkId].Closed;
        }

        public bool RemoveCompleted(string vehicleId)
        {
            if (!vehicles.TryGetValue(vehicleId, out TrafficVehicleState? vehicle) || !vehicle.Completed) return false;
            return vehicles.Remove(vehicleId);
        }

        public TrafficLinkForecast Forecast(int linkId, double entryMinute)
        {
            RequireTime(entryMinute, nameof(entryMinute));
            Link link = FindLink(linkId);
            double delay = link.Closed ? double.PositiveInfinity :
                Math.Max(0, link.NextDischargeAt - entryMinute) +
                (link.Queue.Count + link.Sources.Count) * link.Definition.HeadwayMinutes;
            if (!link.Closed)
            {
                double ready = Math.Max(Clock, entryMinute) + link.Definition.FreeFlowMinutes + delay;
                double green = link.Definition.Signal?.NextGreen(ready) ?? ready;
                delay += green - ready;
                if (link.Queue.Count >= link.Definition.Capacity && link.Queue.First != null)
                {
                    TrafficVehicleState head = link.Queue.First.Value;
                    if (head.RouteIndex + 1 < head.Route.Count && links[head.Route[head.RouteIndex + 1].LinkId].Closed)
                        delay = double.PositiveInfinity;
                }
            }
            if (link.Queue.Count >= link.Definition.Capacity && HasBlockedCycle(link)) delay = double.PositiveInfinity;
            return new TrafficLinkForecast(linkId, link.Queue.Count, link.Sources.Count, delay, link.Closed);
        }

        public double ForecastEntry(int linkId, double entryMinute, double startFraction, double sectionShare = 1)
        {
            RequireTime(entryMinute, nameof(entryMinute));
            if (!Finite(startFraction) || startFraction < 0 || startFraction >= 1) throw new ArgumentOutOfRangeException(nameof(startFraction));
            if (!Finite(sectionShare) || sectionShare <= 0 || sectionShare > 1) throw new ArgumentOutOfRangeException(nameof(sectionShare));
            Link link = FindLink(linkId);
            if (startFraction == 0) return 0;
            if (link.Closed) return double.PositiveInfinity;
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
            double included = Math.Max(0, link.NextDischargeAt - entryMinute)
                + (link.Queue.Count + link.Sources.Count) * link.Definition.HeadwayMinutes;
            return Math.Max(0, projection.Clock - entryMinute - included * sectionShare);
        }

        private TrafficKernel? BuildEntryProjection(int firstId)
        {
            var scratch = projectionScratch ??= new ProjectionScratch();
            HashSet<int> selected = scratch.Selected;
            Queue<int> pending = scratch.Pending;
            List<int> order = scratch.Order;
            List<TrafficVehicleState> selectedVehicles = scratch.Vehicles;
            selected.Clear(); pending.Clear(); order.Clear(); selectedVehicles.Clear();
            selected.Add(firstId); order.Add(firstId); pending.Enqueue(firstId);
            int references = 0;
            bool Include(int id)
            {
                if (selected.Contains(id)) return true;
                if (selected.Count >= ForecastMaximumLinks) return false;
                selected.Add(id); order.Add(id); pending.Enqueue(id); return true;
            }
            bool Select(TrafficVehicleState vehicle)
            {
                int start = Math.Max(0, vehicle.RouteIndex);
                if (vehicle.Route.Count - start > ForecastMaximumReferences - references) return false;
                selectedVehicles.Add(vehicle);
                for (int index = start; index < vehicle.Route.Count; index++)
                {
                    references++;
                    if (!Include(vehicle.Route[index].LinkId)) return false;
                }
                return true;
            }
            while (pending.Count != 0)
            {
                Link current = links[pending.Dequeue()];
                if (selectedVehicles.Count + current.Queue.Count + current.Sources.Count > ForecastMaximumVehicles) return null;
                foreach (TrafficVehicleState vehicle in current.Queue) if (!Select(vehicle)) return null;
                foreach (string id in current.Sources) if (!Select(vehicles[id])) return null;
                foreach (int incomingId in current.Incoming)
                {
                    if (++references > ForecastMaximumReferences) return null;
                    Link incoming = links[incomingId];
                    if ((incoming.Queue.Count != 0 || incoming.Sources.Count != 0) && !Include(incomingId)) return null;
                }
            }
            order.Sort(AscendingIds);
            TrafficKernel projection = scratch.Kernel;
            foreach (int id in scratch.Used)
            {
                Link used = projection.links[id];
                while (used.Queue.First != null) projection.Detach(used, used.Queue.First);
                used.ClearSources(); used.Incoming.Clear();
            }
            scratch.Used.Clear();
            projection.events.Clear(); projection.vehicles.Clear(); projection.activeLinks.Clear(); projection.closedLinks.Clear();
            projection.Clock = projection.RequestedTime = Clock;
            projection.nextEventOrder = projection.nextVehicleOrder = 0;
            projection.Reconsiders = projection.ProcessedEvents = 0;
            foreach (int id in order)
            {
                Link original = links[id];
                if (!projection.links.TryGetValue(id, out Link? copy)) { copy = new Link(original.Definition); projection.links.Add(id, copy); }
                else copy.Definition = original.Definition;
                scratch.Used.Add(id);
                copy.Closed = original.Closed;
                copy.NextDischargeAt = original.NextDischargeAt;
                copy.MergeCursor = original.MergeCursor;
                foreach (string vehicle in original.Sources) copy.OwnSources.Enqueue(vehicle);
            }
            List<TrafficVehicleState> copies = scratch.Copies;
            for (int i = 0; i < selectedVehicles.Count; i++)
            {
                TrafficVehicleState original = selectedVehicles[i];
                if (i == copies.Count) copies.Add(new TrafficVehicleState());
                TrafficVehicleState copy = copies[i];
                int start = Math.Max(0, original.RouteIndex);
                copy.VehicleId = original.VehicleId; copy.Order = original.Order; copy.RouteIndex = original.RouteIndex < 0 ? -1 : 0;
                copy.EnqueuedAt = original.EnqueuedAt; copy.EnteredAt = original.EnteredAt; copy.ReadyAt = original.ReadyAt;
                copy.MotionStartedAt = original.MotionStartedAt; copy.MotionStartFraction = original.MotionStartFraction;
                copy.NominalMinutes = original.NominalMinutes; copy.Completed = false; copy.CompletedAt = null;
                copy.Route.Clear();
                for (int index = start; index < original.Route.Count; index++)
                {
                    TrafficRouteLeg leg = original.Route[index];
                    copy.Route.Add(leg);
                    if (copy.Route.Count > 1) projection.links[leg.LinkId].Incoming.Add(copy.Route[copy.Route.Count - 2].LinkId);
                }
                projection.vehicles.Add(copy.VehicleId, copy);
            }
            foreach (int id in order)
                foreach (var vehicle in links[id].Queue)
                    projection.Append(projection.links[id], projection.vehicles[vehicle.VehicleId]);
            foreach (int id in order) projection.Reconsider(projection.links[id]);
            return projection;
        }

        private void Append(Link link, TrafficVehicleState vehicle)
        {
            if (spareNodes.Count == 0) { link.OwnQueue.AddLast(vehicle); return; }
            LinkedListNode<TrafficVehicleState> node = spareNodes.Pop();
            node.Value = vehicle;
            link.OwnQueue.AddLast(node);
        }

        private void Detach(Link link, LinkedListNode<TrafficVehicleState> node)
        {
            link.Queue.Remove(node);
            node.Value = null!;
            spareNodes.Push(node);
        }

        private bool HasBlockedCycle(Link first)
        {
            var visited = cycleVisited ??= new HashSet<int>();
            visited.Clear();
            Link current = first;
            int references = 0;
            for (int inspected = 0; inspected < 64; inspected++)
            {
                if (current.Queue.Count < current.Definition.Capacity || current.Queue.First == null) return false;
                for (var node = current.Queue.First; node != null; node = node.Next)
                {
                    if (++references > ForecastMaximumReferences) return false;
                    if (IsCurbExit(node.Value) && MayCurbFreeStorage(current, node, ref references)) return false;
                }
                if (!visited.Add(current.Definition.Id)) return true;
                TrafficVehicleState head = current.Queue.First.Value;
                if (head.RouteIndex + 1 >= head.Route.Count) return false;
                current = links[head.Route[head.RouteIndex + 1].LinkId];
                if (current.Closed) return true;
            }
            return false;
        }

        private bool MayCurbFreeStorage(Link link, LinkedListNode<TrafficVehicleState> target, ref int references)
        {
            TrafficVehicleState vehicle = target.Value;
            double end = vehicle.Route[vehicle.RouteIndex].EndLinkFraction;
            int separation = 0;
            for (var predecessor = target.Previous; predecessor != null; predecessor = predecessor.Previous)
            {
                if (++references > ForecastMaximumReferences) return true;
                TrafficVehicleState ahead = predecessor.Value;
                if (end + ++separation / (double)link.Definition.Capacity > ahead.Route[ahead.RouteIndex].EndLinkFraction) return false;
            }
            return true;
        }

        private void Process(Link link)
        {
            string? completedId = null;
            for (var node = link.Queue.First; node != null; node = node.Next)
            {
                TrafficVehicleState vehicle = node.Value;
                if (vehicle.RouteIndex != vehicle.Route.Count - 1) continue;
                bool curb = IsCurbExit(vehicle);
                if ((!curb && node != link.Queue.First) ||
                    (curb ? CurbExitAt(link, node) : EligibleAt(link, vehicle)) > Clock) continue;
                if (curb) Detach(link, node);
                else RemoveHead(link);
                vehicle.NominalMinutes = vehicle.Route[vehicle.RouteIndex].EndNominalMinutes;
                vehicle.Completed = true;
                vehicle.CompletedAt = Clock;
                completedId = vehicle.VehicleId;
                break;
            }
            if (!link.Closed && link.Queue.Count < link.Definition.Capacity)
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
                        vehicle = source.Queue.First!.Value;
                        RemoveHead(source);
                        vehicle.RouteIndex++;
                        Reconsider(source);
                    }
                    link.MergeCursor = candidate.Value;
                    TrafficRouteLeg leg = vehicle.Route[vehicle.RouteIndex];
                    vehicle.EnteredAt = vehicle.MotionStartedAt = Clock;
                    vehicle.MotionStartFraction = 0;
                    vehicle.ReadyAt = Clock + leg.FreeFlowMinutes;
                    vehicle.NominalMinutes = leg.StartNominalMinutes;
                    Append(link, vehicle);
                }
            }
            Reconsider(link);
            if (completedId != null) VehicleCompleted?.Invoke(completedId, Clock);
        }

        private void RemoveHead(Link source)
        {
            Detach(source, source.Queue.First!);
            source.NextDischargeAt = Clock + source.Definition.HeadwayMinutes;
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
                if (id > destination.MergeCursor && !after.HasValue) after = id;
            }
            if (sourceEligible && -1 > destination.MergeCursor) return -1;
            return after ?? wrap;
        }

        private void Reconsider(Link link)
        {
            Reconsiders++;
            UpdateActivity(link);
            if (link.Queue.First != null)
            {
                TrafficVehicleState head = link.Queue.First.Value;
                double at = EligibleAt(link, head);
                if (head.RouteIndex == head.Route.Count - 1 && !IsCurbExit(head)) Schedule(link, at);
                else if (head.RouteIndex + 1 < head.Route.Count)
                {
                    Link destination = links[head.Route[head.RouteIndex + 1].LinkId];
                    if (CanReceive(destination, head, head.RouteIndex + 1)) Schedule(destination, at);
                }
            }
            for (var node = link.Queue.First; node != null; node = node.Next)
                if (IsCurbExit(node.Value)) Schedule(link, CurbExitAt(link, node));
            if (link.Closed || link.Queue.Count >= link.Definition.Capacity) return;
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
            return !destination.Closed && destination.Queue.Count < destination.Definition.Capacity &&
                (vehicle.Route[routeIndex].StartLinkFraction == 0 || destination.Queue.Count == 0);
        }

        private bool Feeds(Link source, int destinationId, out TrafficVehicleState? vehicle)
        {
            vehicle = source.Queue.First == null ? null : source.Queue.First.Value;
            return vehicle != null && vehicle.RouteIndex + 1 < vehicle.Route.Count
                && vehicle.Route[vehicle.RouteIndex + 1].LinkId == destinationId;
        }

        private double EligibleAt(Link source, TrafficVehicleState vehicle)
        {
            TrafficRouteLeg leg = vehicle.Route[vehicle.RouteIndex];
            double ready = Math.Max(Clock, Math.Max(vehicle.ReadyAt, source.NextDischargeAt));
            var signal = source.Definition.Signal;
            return signal == null ? ready : signal.NextGreen(ready);
        }

        private static bool IsCurbExit(TrafficVehicleState vehicle) => vehicle.RouteIndex == vehicle.Route.Count - 1
            && vehicle.Route[vehicle.RouteIndex].EndLinkFraction < 1;

        private double CurbExitAt(Link link, LinkedListNode<TrafficVehicleState> target)
        {
            TrafficVehicleState vehicle = target.Value;
            TrafficRouteLeg leg = vehicle.Route[vehicle.RouteIndex];
            double at = Math.Max(Clock, vehicle.ReadyAt);
            int separation = 0;
            for (var predecessor = target.Previous; predecessor != null; predecessor = predecessor.Previous)
            {
                double required = leg.EndLinkFraction + ++separation / (double)link.Definition.Capacity;
                TrafficVehicleState ahead = predecessor.Value;
                TrafficRouteLeg aheadLeg = ahead.Route[ahead.RouteIndex];
                if (required > aheadLeg.EndLinkFraction) return double.PositiveInfinity;
                double fraction = (required - aheadLeg.StartLinkFraction) / (aheadLeg.EndLinkFraction - aheadLeg.StartLinkFraction);
                double actual = (ahead.NominalMinutes - aheadLeg.StartNominalMinutes) / (aheadLeg.EndNominalMinutes - aheadLeg.StartNominalMinutes);
                if (actual >= fraction) continue;
                at = Math.Max(at, ahead.MotionStartedAt + Math.Max(0, fraction - ahead.MotionStartFraction) * aheadLeg.FreeFlowMinutes);
            }
            return at;
        }

        private void Schedule(Link link, double at)
        {
            if (double.IsPositiveInfinity(at)) return;
            at = Math.Max(Clock, at);
            if (link.Scheduled)
            {
                if (link.Event.At <= at) return;
                events.Remove(link);
            }
            link.Event.At = at;
            link.Event.Order = NextOrder(ref nextEventOrder);
            events.Add(link);
        }

        private TrafficVehicleSnapshot Snapshot(TrafficVehicleState vehicle)
        {
            if (vehicle.Completed)
                return new TrafficVehicleSnapshot(vehicle.VehicleId, vehicle.RouteIndex, null, vehicle.NominalMinutes,
                    vehicle.NominalMinutes, false, true, vehicle.CompletedAt);
            if (vehicle.RouteIndex < 0)
                return new TrafficVehicleSnapshot(vehicle.VehicleId, -1, null, vehicle.NominalMinutes,
                    vehicle.NominalMinutes, false, false, null);
            TrafficRouteLeg leg = vehicle.Route[vehicle.RouteIndex];
            Link link = links[leg.LinkId];
            int rank = 0;
            double previousFraction = 0;
            double maximum = vehicle.NominalMinutes;
            var queueNode = link.Queue.First;
            for (; queueNode != null; queueNode = queueNode.Next)
            {
                TrafficVehicleState occupant = queueNode.Value;
                TrafficRouteLeg occupantLeg = occupant.Route[occupant.RouteIndex];
                double span = occupantLeg.EndNominalMinutes - occupantLeg.StartNominalMinutes;
                double floor = (occupant.NominalMinutes - occupantLeg.StartNominalMinutes) / span;
                double motion = Math.Min(1, occupant.MotionStartFraction + Math.Max(0, Clock - occupant.MotionStartedAt) / occupantLeg.FreeFlowMinutes);
                double spatialSpan = occupantLeg.EndLinkFraction - occupantLeg.StartLinkFraction;
                double queueLimit = rank == 0 ? 1 : Math.Max(0, Math.Min(1,
                    (previousFraction - 1.0 / link.Definition.Capacity - occupantLeg.StartLinkFraction) / spatialSpan));
                double allowed = Math.Max(floor, Math.Min(motion, queueLimit));
                if (ReferenceEquals(occupant, vehicle))
                {
                    maximum = allowed == 1 ? occupantLeg.EndNominalMinutes : occupantLeg.StartNominalMinutes + allowed * span;
                    maximum = Math.Max(occupant.NominalMinutes, maximum);
                    break;
                }
                previousFraction = occupantLeg.StartLinkFraction + allowed * spatialSpan;
                rank++;
            }
            bool green = (link.Definition.Signal?.NextGreen(Clock) ?? Clock) <= Clock;
            bool cross = rank == 0 && Math.Max(vehicle.ReadyAt, link.NextDischargeAt) <= Clock && green;
            if (cross && vehicle.RouteIndex + 1 < vehicle.Route.Count)
            {
                Link destination = links[vehicle.Route[vehicle.RouteIndex + 1].LinkId];
                cross = CanReceive(destination, vehicle, vehicle.RouteIndex + 1);
            }
            if (IsCurbExit(vehicle)) cross = CurbExitAt(link, queueNode!) <= Clock;
            return new TrafficVehicleSnapshot(vehicle.VehicleId, vehicle.RouteIndex, leg.LinkId, maximum,
                maximum, cross, false, null, green, leg.EndNominalMinutes);
        }

        public TrafficKernelState Capture() => RunToEnd(CaptureInSteps(int.MaxValue));

        public IEnumerator<TrafficKernelState?> CaptureInSteps(int linksPerStep)
        {
            if (linksPerStep < 1) throw new ArgumentOutOfRangeException(nameof(linksPerStep));
            return CaptureSteps(linksPerStep, true);
        }

        internal TrafficKernelState CaptureShared() => RunToEnd(CaptureSharedInSteps(int.MaxValue));

        internal IEnumerator<TrafficKernelState?> CaptureSharedInSteps(int linksPerStep)
        {
            if (linksPerStep < 1) throw new ArgumentOutOfRangeException(nameof(linksPerStep));
            return CaptureSteps(linksPerStep, true, shareDefinitions: true);
        }

        private IEnumerator<TrafficKernelState?> CaptureSteps(int linksPerStep, bool withLinks, bool shareDefinitions = false)
        {
            int work = 0;
            var state = new TrafficKernelState
            {
                Clock = Clock, RequestedTime = RequestedTime, NextEventOrder = nextEventOrder, NextVehicleOrder = nextVehicleOrder,
                Definitions = new List<TrafficLinkDefinition>(orderedLinks.Count), Links = new List<TrafficLinkState>(withLinks ? orderedLinks.Count : 0),
                Vehicles = new List<TrafficVehicleState>(vehicles.Count), Events = new List<TrafficEventState>(events.Count)
            };
            foreach (Link link in orderedLinks)
            {
                state.Definitions.Add(shareDefinitions ? link.Definition : CopyDefinition(link.Definition));
                if (!withLinks) { if (++work % linksPerStep == 0) yield return null; continue; }
                var queuedIds = new List<string>(link.Queue.Count);
                foreach (var vehicle in link.Queue) queuedIds.Add(vehicle.VehicleId);
                var sources = new List<string>(link.Sources.Count);
                foreach (string source in link.Sources) sources.Add(source);
                state.Links.Add(new TrafficLinkState(queuedIds, sources, link.Incoming.ToList())
                {
                    LinkId = link.Definition.Id, Closed = link.Closed, NextDischargeAt = link.NextDischargeAt,
                    MergeCursor = link.MergeCursor
                });
                if (++work % linksPerStep == 0) yield return null;
            }
            foreach (TrafficVehicleState vehicle in vehicles.Values.OrderBy(v => v.Order)) state.Vehicles.Add(CopyVehicle(vehicle));
            var scheduled = new List<TrafficEventState>(events.Count);
            events.CopyTo(scheduled);
            scheduled.Sort(EventOrder);
            foreach (TrafficEventState item in scheduled) state.Events.Add(new TrafficEventState { LinkId = item.LinkId, At = item.At, Order = item.Order });
            yield return state;
        }

        public TrafficKernelState Capture(Func<string, bool> exclude)
        {
            if (exclude == null) throw new ArgumentNullException(nameof(exclude));
            return RunToEnd(CaptureExcludingSteps(exclude, int.MaxValue));
        }

        public IEnumerator<TrafficKernelState?> CaptureInSteps(Func<string, bool> exclude, int linksPerStep)
        {
            if (exclude == null) throw new ArgumentNullException(nameof(exclude));
            if (linksPerStep < 1) throw new ArgumentOutOfRangeException(nameof(linksPerStep));
            return CaptureExcludingSteps(exclude, linksPerStep);
        }

        private IEnumerator<TrafficKernelState?> CaptureExcludingSteps(Func<string, bool> exclude, int linksPerStep)
        {
            int work = 0;
            TrafficKernelState? state = null;
            var capture = CaptureInSteps(linksPerStep);
            while (capture.MoveNext())
            {
                if (capture.Current != null) { state = capture.Current; break; }
                yield return null;
            }
            if (state == null) throw new InvalidOperationException("Traffic capture ended without a result.");
            if (state.Vehicles.RemoveAll(v => exclude(v.VehicleId)) == 0) { yield return state; yield break; }
            var rebased = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (Link link in orderedLinks)
            {
                bool behind = false;
                double previous = 0;
                int rank = 0;
                for (var queued = link.Queue.First; queued != null; queued = queued.Next, rank++)
                {
                    TrafficVehicleState occupant = queued.Value;
                    TrafficRouteLeg leg = occupant.Route[occupant.RouteIndex];
                    double floor = (occupant.NominalMinutes - leg.StartNominalMinutes) / (leg.EndNominalMinutes - leg.StartNominalMinutes);
                    double motion = Math.Min(1, occupant.MotionStartFraction + Math.Max(0, Clock - occupant.MotionStartedAt) / leg.FreeFlowMinutes);
                    double spatial = leg.EndLinkFraction - leg.StartLinkFraction;
                    double limit = rank == 0 ? 1 : Math.Max(0, Math.Min(1, (previous - 1.0 / link.Definition.Capacity - leg.StartLinkFraction) / spatial));
                    double allowed = Math.Max(floor, Math.Min(motion, limit));
                    previous = leg.StartLinkFraction + allowed * spatial;
                    if (exclude(occupant.VehicleId)) behind = true;
                    else if (behind && allowed < motion) rebased[occupant.VehicleId] = allowed;
                }
                if (++work % linksPerStep == 0) yield return null;
            }
            foreach (TrafficVehicleState vehicle in state.Vehicles)
            {
                if (!rebased.TryGetValue(vehicle.VehicleId, out double at)) continue;
                double free = vehicle.Route[vehicle.RouteIndex].FreeFlowMinutes;
                vehicle.MotionStartFraction = at; vehicle.MotionStartedAt = Clock;
                vehicle.ReadyAt = Math.Max(vehicle.ReadyAt, Clock + (1 - at) * free);
            }
            foreach (TrafficLinkState link in state.Links)
            {
                link.Vehicles.RemoveAll(id => exclude(id));
                link.WaitingSources.RemoveAll(id => exclude(id));
                if (++work % linksPerStep == 0) yield return null;
            }
            TrafficKernel? rebuilt = null;
            var restore = RestoreSteps(state, true, linksPerStep, adoptDefinitions: true);
            while (restore.MoveNext())
            {
                if (restore.Current != null) { rebuilt = restore.Current; break; }
                yield return null;
            }
            var recapture = rebuilt!.CaptureInSteps(linksPerStep);
            while (recapture.MoveNext())
            {
                if (recapture.Current != null) { yield return recapture.Current; yield break; }
                yield return null;
            }
        }

        public static TrafficKernel Restore(TrafficKernelState state) => Restore(state, false);

        public static IEnumerator<TrafficKernel?> RestoreInSteps(TrafficKernelState state, int linksPerStep) => RestoreSteps(state, false, linksPerStep);

        private static TrafficKernel Restore(TrafficKernelState state, bool rebuildWakeups) => RunToEnd(RestoreSteps(state, rebuildWakeups, int.MaxValue));

        private static IEnumerator<TrafficKernel?> RestoreSteps(TrafficKernelState state, bool rebuildWakeups, int linksPerStep, bool adoptDefinitions = false,
            TrafficKernel? linkSource = null, double dischargeFactor = 1)
        {
            if (linksPerStep < 1) throw new ArgumentOutOfRangeException(nameof(linksPerStep));
            int work = 0;
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Schema != TrafficKernelState.CurrentSchema) throw new ArgumentException("Unsupported traffic schema.", nameof(state));
            RequireTime(state.Clock, nameof(state));
            RequireTime(state.RequestedTime, nameof(state));
            if (state.RequestedTime < state.Clock || state.NextEventOrder < 0 || state.NextVehicleOrder < 0 ||
                state.Definitions == null || state.Links == null || state.Vehicles == null || state.Events == null)
                throw new ArgumentException("Invalid traffic cursor.", nameof(state));
            var kernel = new TrafficKernel(state.Clock, state.Definitions.Count, state.Vehicles.Count)
            {
                RequestedTime = state.RequestedTime, nextEventOrder = state.NextEventOrder, nextVehicleOrder = state.NextVehicleOrder
            };
            bool ascending = true;
            foreach (TrafficLinkDefinition definition in state.Definitions)
            {
                ascending &= kernel.AddDefinition(definition, adoptDefinitions);
                if (++work % linksPerStep == 0) yield return null;
            }
            if (!ascending) kernel.SortLinks();
            var orders = new HashSet<long>(state.Vehicles.Count);
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
                TrafficVehicleState copy = CopyVehicle(vehicle, route);
                kernel.vehicles.Add(vehicle.VehicleId, copy);
            }
            var seenLinks = linkSource == null ? new HashSet<int>(state.Links.Count) : null;
            int seenCount = 0;
            var occupied = new HashSet<string>(state.Vehicles.Count, StringComparer.Ordinal);
            if (linkSource != null)
                foreach (Link original in linkSource.orderedLinks)
                {
                    int linkId = original.Definition.Id;
                    if (!kernel.links.TryGetValue(linkId, out Link? link) || link.Seen || original.Queue.Count > link.Definition.Capacity ||
                        (original.MergeCursor >= 0 && !kernel.links.ContainsKey(original.MergeCursor)))
                        throw new ArgumentException("Invalid traffic link state.", nameof(state));
                    link.Seen = true; seenCount++;
                    double discharge = linkSource.Clock + Math.Max(0, original.NextDischargeAt - linkSource.Clock) * dischargeFactor;
                    RequireTime(discharge, nameof(state));
                    link.Closed = original.Closed;
                    if (original.Closed) kernel.closedLinks.Add(linkId);
                    link.NextDischargeAt = discharge;
                    link.MergeCursor = original.MergeCursor;
                    foreach (int source in original.Incoming)
                        if (!kernel.links.ContainsKey(source) || source == linkId || !link.Incoming.Add(source))
                            throw new ArgumentException("Invalid traffic predecessor.", nameof(state));
                    foreach (TrafficVehicleState queued in original.Queue)
                    {
                        TrafficVehicleState vehicle = kernel.FindRestoredVehicle(queued.VehicleId, occupied);
                        if (vehicle.Completed || vehicle.RouteIndex < 0 || vehicle.Route[vehicle.RouteIndex].LinkId != linkId)
                            throw new ArgumentException("Traffic queue and vehicle cursor disagree.", nameof(state));
                        kernel.Append(link, vehicle);
                    }
                    foreach (string id in original.Sources)
                    {
                        TrafficVehicleState vehicle = kernel.FindRestoredVehicle(id, occupied);
                        if (vehicle.Completed || vehicle.RouteIndex != -1 || vehicle.Route[0].LinkId != linkId)
                            throw new ArgumentException("Traffic source queue and vehicle cursor disagree.", nameof(state));
                        link.OwnSources.Enqueue(id);
                    }
                    if (++work % linksPerStep == 0) yield return null;
                }
            else foreach (TrafficLinkState saved in state.Links)
            {
                if (saved == null || !kernel.links.TryGetValue(saved.LinkId, out Link? link) || !seenLinks!.Add(saved.LinkId) ||
                    saved.Vehicles == null || saved.WaitingSources == null || saved.IncomingLinks == null ||
                    saved.Vehicles.Count > link.Definition.Capacity || saved.MergeCursor < -1 ||
                    (saved.MergeCursor >= 0 && !kernel.links.ContainsKey(saved.MergeCursor)))
                    throw new ArgumentException("Invalid traffic link state.", nameof(state));
                RequireTime(saved.NextDischargeAt, nameof(state));
                link.Closed = saved.Closed;
                if (saved.Closed) kernel.closedLinks.Add(saved.LinkId);
                link.NextDischargeAt = saved.NextDischargeAt;
                link.MergeCursor = saved.MergeCursor;
                foreach (int source in saved.IncomingLinks)
                    if (!kernel.links.ContainsKey(source) || source == saved.LinkId || !link.Incoming.Add(source))
                        throw new ArgumentException("Invalid traffic predecessor.", nameof(state));
                foreach (string id in saved.Vehicles)
                {
                    TrafficVehicleState vehicle = kernel.FindRestoredVehicle(id, occupied);
                    if (vehicle.Completed || vehicle.RouteIndex < 0 || vehicle.Route[vehicle.RouteIndex].LinkId != saved.LinkId)
                        throw new ArgumentException("Traffic queue and vehicle cursor disagree.", nameof(state));
                    kernel.Append(link, vehicle);
                }
                foreach (string id in saved.WaitingSources)
                {
                    TrafficVehicleState vehicle = kernel.FindRestoredVehicle(id, occupied);
                    if (vehicle.Completed || vehicle.RouteIndex != -1 || vehicle.Route[0].LinkId != saved.LinkId)
                        throw new ArgumentException("Traffic source queue and vehicle cursor disagree.", nameof(state));
                    link.OwnSources.Enqueue(id);
                }
                if (++work % linksPerStep == 0) yield return null;
            }
            if ((seenLinks?.Count ?? seenCount) != kernel.links.Count || occupied.Count != kernel.vehicles.Values.Count(v => !v.Completed))
                throw new ArgumentException("Traffic occupancy is incomplete.", nameof(state));
            foreach (TrafficVehicleState vehicle in kernel.vehicles.Values)
                for (int i = 1; i < vehicle.Route.Count; i++)
                    if (!kernel.links[vehicle.Route[i].LinkId].Incoming.Contains(vehicle.Route[i - 1].LinkId))
                        throw new ArgumentException("Missing traffic predecessor.", nameof(state));
            var eventOrders = new HashSet<long>(state.Events.Count);
            foreach (TrafficEventState saved in state.Events)
            {
                if (saved == null || !kernel.links.TryGetValue(saved.LinkId, out Link? link) || link.Scheduled ||
                    !Finite(saved.At) || saved.At < state.Clock || saved.Order < 0 || saved.Order >= state.NextEventOrder || !eventOrders.Add(saved.Order))
                    throw new ArgumentException("Invalid traffic event cursor.", nameof(state));
                link.Event.At = saved.At;
                link.Event.Order = saved.Order;
                kernel.events.Add(link);
            }
            foreach (Link link in kernel.orderedLinks)
            {
                kernel.UpdateActivity(link);
                if (++work % linksPerStep == 0) yield return null;
            }
            if (rebuildWakeups)
                foreach (Link link in kernel.orderedLinks)
                {
                    kernel.Reconsider(link);
                    if (++work % linksPerStep == 0) yield return null;
                }
            var wakeups = kernel.RequiredWakeupSteps(linksPerStep);
            while (wakeups.MoveNext()) yield return null;
            yield return kernel;
        }

        public static void ValidateState(TrafficKernelState state) => Restore(state, false);

        public bool Cancel(string vehicleId)
        {
            if (!vehicles.TryGetValue(vehicleId, out TrafficVehicleState? vehicle)) return false;
            if (!vehicle.Completed)
            {
                Link link = links[vehicle.Route[Math.Max(0, vehicle.RouteIndex)].LinkId];
                if (vehicle.RouteIndex >= 0)
                {
                    LinkedListNode<TrafficVehicleState>? node = link.Queue.Find(vehicle);
                    if (node != null) Detach(link, node);
                }
                else
                {
                    int count = link.Sources.Count;
                    for (int i = 0; i < count; i++)
                    {
                        string id = link.Sources.Dequeue();
                        if (id != vehicleId) link.OwnSources.Enqueue(id);
                    }
                }
                vehicles.Remove(vehicleId);
                Reconsider(link);
            }
            else vehicles.Remove(vehicleId);
            return true;
        }

        public TrafficKernel RescaleTime(double factor, Func<string, double, double>? mapNominalMinutes = null)
        {
            if (!Finite(factor) || factor <= 0) throw new ArgumentOutOfRangeException(nameof(factor));
            return RunToEnd(RescaleTimeSteps(factor, mapNominalMinutes, int.MaxValue));
        }

        public IEnumerator<TrafficKernel?> RescaleTimeInSteps(double factor, Func<string, double, double>? mapNominalMinutes, int linksPerStep)
        {
            if (!Finite(factor) || factor <= 0) throw new ArgumentOutOfRangeException(nameof(factor));
            if (linksPerStep < 1) throw new ArgumentOutOfRangeException(nameof(linksPerStep));
            return RescaleTimeSteps(factor, mapNominalMinutes, linksPerStep);
        }

        private IEnumerator<TrafficKernel?> RescaleTimeSteps(double factor, Func<string, double, double>? mapNominalMinutes, int linksPerStep)
        {
            int work = 0;
            TrafficKernelState? state = null;
            var capture = CaptureSteps(linksPerStep, false);
            while (capture.MoveNext())
            {
                if (capture.Current != null) { state = capture.Current; break; }
                yield return null;
            }
            if (state == null) throw new InvalidOperationException("Traffic capture ended without a result.");
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
                if (++work % linksPerStep == 0) yield return null;
            }
            foreach (TrafficVehicleState vehicle in state.Vehicles)
            {
                if (vehicle.RouteIndex > 0)
                {
                    vehicle.Route.RemoveRange(0, vehicle.RouteIndex);
                    vehicle.RouteIndex = 0;
                }
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
                    double motionReady = vehicle.MotionStartedAt + (1 - vehicle.MotionStartFraction) * current.FreeFlowMinutes;
                    vehicle.ReadyAt = Math.Max(vehicle.ReadyAt, motionReady);
                }
            }
            foreach (TrafficEventState item in state.Events) item.At = Clock + (item.At - Clock) * factor;
            var restore = RestoreSteps(state, true, linksPerStep, adoptDefinitions: true, linkSource: this, dischargeFactor: factor);
            while (restore.MoveNext())
            {
                if (restore.Current != null) { yield return restore.Current; yield break; }
                yield return null;
            }
        }

        private IEnumerator<object?> RequiredWakeupSteps(int linksPerStep)
        {
            int work = 0;
            foreach (Link link in orderedLinks)
            {
                if (++work % linksPerStep == 0) yield return null;
                if (link.Queue.First != null)
                {
                    TrafficVehicleState head = link.Queue.First.Value;
                    double at = EligibleAt(link, head);
                    if (head.RouteIndex == head.Route.Count - 1 && !IsCurbExit(head)) RequireScheduled(link, at);
                    else if (head.RouteIndex + 1 < head.Route.Count)
                    {
                        Link destination = links[head.Route[head.RouteIndex + 1].LinkId];
                        if (CanReceive(destination, head, head.RouteIndex + 1)) RequireScheduled(destination, at);
                    }
                }
                for (var node = link.Queue.First; node != null; node = node.Next)
                    if (IsCurbExit(node.Value)) RequireScheduled(link, CurbExitAt(link, node));
                if (link.Closed || link.Queue.Count >= link.Definition.Capacity) continue;
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
            if (!link.Scheduled || (link.Event.At > expected && link.Event.At - expected > 4 * Ulp(expected)))
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
        private static List<TrafficRouteLeg> CopyRoute(List<TrafficRouteLeg> route)
        {
            var copy = new List<TrafficRouteLeg>(route.Count);
            foreach (var leg in route)
                copy.Add(new TrafficRouteLeg { LinkId = leg.LinkId, StartNominalMinutes = leg.StartNominalMinutes, EndNominalMinutes = leg.EndNominalMinutes,
                    FreeFlowMinutes = leg.FreeFlowMinutes, StartLinkFraction = leg.StartLinkFraction, EndLinkFraction = leg.EndLinkFraction });
            return copy;
        }
        private static TrafficVehicleState CopyVehicle(TrafficVehicleState value, List<TrafficRouteLeg>? route = null) => new TrafficVehicleState(route ?? CopyRoute(value.Route))
        {
            VehicleId = value.VehicleId, Order = value.Order,
            RouteIndex = value.RouteIndex, EnqueuedAt = value.EnqueuedAt, EnteredAt = value.EnteredAt,
            ReadyAt = value.ReadyAt, MotionStartedAt = value.MotionStartedAt, MotionStartFraction = value.MotionStartFraction,
            NominalMinutes = value.NominalMinutes,
            Completed = value.Completed, CompletedAt = value.CompletedAt
        };
        private static long NextOrder(ref long counter)
        {
            if (counter == long.MaxValue) throw new InvalidOperationException("Traffic order exhausted.");
            return counter++;
        }
        private static double PreviousDouble(double value) => BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(value) - 1);
        private static double NextDouble(double value) => value == 0 ? double.Epsilon : BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(value) + 1);
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
