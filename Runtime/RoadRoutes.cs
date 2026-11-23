using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Extensions;
using GleyTrafficSystem;
using GleyUrbanAssets;
using PersistentCity.Core;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace PersistentCity.Runtime
{
    internal sealed class RoadRoutes
    {
        private const double ConnectorMetres = 8;
        private readonly List<TransportRoadNode> nodes = new List<TransportRoadNode>();
        private readonly Dictionary<(int X, int Z), List<int>> cells = new Dictionary<(int, int), List<int>>();
        private static RoadRoutes? donor;
        private TrafficRoadNetwork? trafficDonor;
        private bool[]? donorGateClear;
        private string donorGateTopology = "";
        private PublicRoadAccess? donorGateAccess;
        public bool AdoptedSnapshot { get; }
        public bool AdoptedGateClearance { get; private set; }
        internal static void ForgetSnapshot() => donor = null;
        private readonly Waypoint[] waypoints;
        private readonly TransportRoadGraph graph;
        private readonly CurrentSceneData? scene;
        private PublicRoadAccess access;
        private bool unsafeTopology;
        public string InvalidationReason { get; private set; } = "";
        public int InvalidationIndex { get; private set; } = -1;
        private bool Invalid(string reason, int index = -1)
        {
            unsafeTopology = true; donor = null;
            if (InvalidationReason.Length == 0) { InvalidationReason = reason; InvalidationIndex = index; }
            return false;
        }
        private bool TrafficInvalid() => Invalid(trafficBuilder?.InvalidationReason is string reason && reason.Length > 0
            ? reason : "traffic-import", trafficBuilder?.InvalidationIndex ?? -1);
        private int copiedNodes;
        private readonly double walkingSpeed;
        private readonly double clockRate;
        private readonly NativeRoadSpeeds speeds;
        private Func<int, double, TrafficLinkForecast>? trafficForecast;
        private Func<int, double, double, double, double>? trafficEntryForecast;
        private TrafficRoadNetwork? trafficBuilder;
        public TrafficNetwork? TrafficNetwork { get; private set; }
        public void SetTrafficForecast(Func<int, double, TrafficLinkForecast>? forecast) { trafficForecast = forecast; }
        public void SetTrafficEntryForecast(Func<int, double, double, double, double>? forecast) { trafficEntryForecast = forecast; }
        private bool closureBatch, closureBatchCurrent;
        private readonly ClosureValidationRota closureValidation = new ClosureValidationRota();
        public long ClosureValidationsDeferred => closureValidation.Deferred;
        private int closureGeneration;
        private int[] closureCheckedAt = Array.Empty<int>();
        private bool[] closureDisabled = Array.Empty<bool>();
        private bool[] closureGateClear = Array.Empty<bool>();
        public void BeginTrafficClosureBatch()
        {
            if (closureBatch) throw new InvalidOperationException("Traffic closure batch cannot be nested.");
            if (closureCheckedAt.Length != waypoints.Length)
            { closureCheckedAt = new int[waypoints.Length]; closureDisabled = new bool[waypoints.Length]; }
            if (closureGeneration == int.MaxValue)
            { Array.Clear(closureCheckedAt, 0, closureCheckedAt.Length); closureGeneration = 0; }
            closureGeneration++;
            closureValidation.Begin(waypoints.Length);
            trafficBuilder?.BeginQuery();
            closureBatchCurrent = Ready && IsCurrent;
            closureBatch = true;
        }
        public void EndTrafficClosureBatch()
        {
            closureBatch = false;
            trafficBuilder?.BeginQuery();
        }
        public bool IsTrafficLinkClosed(int linkId)
        {
            var network = TrafficNetwork;
            if (!Ready || (closureBatch ? unsafeTopology || !closureBatchCurrent : !IsCurrent) || network == null || linkId < 0 || linkId >= network.Links.Count) return true;
            if (!closureBatch) trafficBuilder?.BeginQuery();
            if (closureGateClear.Length != network.Links.Count) closureGateClear = new bool[network.Links.Count];
            var indices = network.NativeNodes(linkId);
            for (int i = 0; i < indices.Count; i++)
            {
                int index = indices[i];
                bool disabled;
                if (!closureBatch) disabled = Disabled(index);
                else
                {
                    if (closureCheckedAt[index] != closureGeneration)
                    {
                        closureDisabled[index] = closureValidation.TakeFull(index) ? Disabled(index) : ClosedByFlags(index);
                        closureCheckedAt[index] = closureGeneration;
                    }
                    disabled = closureDisabled[index];
                }
                if (disabled) return true;
                if (i > 0 && !closureGateClear[linkId]
                    && !access.ClearSegment(nodes[indices[i - 1]].Position, nodes[index].Position)) return true;
            }
            closureGateClear[linkId] = true;
            return false;
        }
        private readonly SHA256 topologyHash = SHA256.Create();
        private readonly MemoryStream topologyBuffer = new MemoryStream(256);
        private readonly BinaryWriter topologyWriter;
        private readonly List<int[]?> agentContracts = new List<int[]?>();
        private readonly List<VehicleTypes[]?> carContracts = new List<VehicleTypes[]?>();
        private readonly SharedContracts<int> sharedAgents = new SharedContracts<int>();
        private readonly SharedContracts<VehicleTypes> sharedCars = new SharedContracts<VehicleTypes>();
        private readonly List<Waypoint?> nativeContracts = new List<Waypoint?>();
        private readonly List<bool> nullNeighbours = new List<bool>();
        private readonly int[] searchedAt;
        private readonly bool[] searchDisabled;
        private int searchGeneration;
        private static readonly System.Reflection.FieldInfo? AssociatedIntersection = typeof(Waypoint).GetField("associatedIntersection",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        private bool sampleScope;
        private int sampleGeneration;
        private int[] sampledAt = Array.Empty<int>();
        private bool[] sampleDisabled = Array.Empty<bool>();
        public string TopologyId { get; private set; } = "";
        internal IReadOnlyList<TransportRoadNode> Nodes => nodes;
        internal bool ImportsNode(int index) => trafficBuilder?.ImportsNode(index) == true;
        internal int RoadId(int index) => scene?.roadIds != null && scene.roadIds.Length == waypoints.Length && index >= 0 && index < waypoints.Length ? scene.roadIds[index] : -1;
        public bool Ready => copiedNodes == waypoints.Length && TrafficNetwork != null;
        public bool Available => scene != null && waypoints.Length > 0;
        public bool SceneMissing => scene == null;
        public bool IsCurrent
        {
            get
            {
                if (unsafeTopology) return false;
                if (trafficBuilder != null && !trafficBuilder.ReferencesCurrent) return TrafficInvalid();
                if (hadScene && scene == null) return Invalid("scene-destroyed");
                if (scene == null || scene.allWaypoints == null ? waypoints.Length != 0 : !ReferenceEquals(scene.allWaypoints, waypoints))
                    return Invalid("waypoint-array");
                return true;
            }
        }
        private readonly bool hadScene;
        public void SetAccess(PublicRoadAccess current)
        {
            if (!ReferenceEquals(access, current)) Array.Clear(closureGateClear, 0, closureGateClear.Length);
            access = current;
        }
        private int sweepNode = -1, sweepJunction;
        public void BeginSweep()
        {
            if (Ready && sweepNode < 0) { sweepNode = 0; sweepJunction = 0; }
        }
        public void Sweep(double milliseconds, int maximumUnits)
        {
            if (sweepNode < 0) return;
            if (!Ready || !IsCurrent) { sweepNode = -1; return; }
            long started = Stopwatch.GetTimestamp();
            long budget = (long)(milliseconds * Stopwatch.Frequency / 1000);
            trafficBuilder?.BeginQuery();
            for (int units = 0; units < maximumUnits && Stopwatch.GetTimestamp() - started < budget; units++)
            {
                bool current;
                if (sweepNode < waypoints.Length)
                {
                    int index = sweepNode++;
                    current = SnapshotNodeCurrent(index) && (trafficBuilder == null || trafficBuilder.NodeCurrent(index));
                }
                else if (trafficBuilder != null && sweepJunction < trafficBuilder.JunctionCount)
                    current = trafficBuilder.JunctionCurrentAt(sweepJunction++);
                else { sweepNode = -1; return; }
                if (!current) { if (!unsafeTopology) TrafficInvalid(); sweepNode = -1; return; }
            }
        }
        private readonly int obstacleMask = LayerMask.GetMask("Obstacles", "Roads", "Default");
        internal sealed class Curb
        {
            public int Node;
            public Position Position;
        }
        public RoadRoutes(PublicRoadAccess access, double walkingSpeed = 2, double clockRate = 1, NativeRoadSpeeds? speeds = null)
            : this(Object.FindObjectOfType<CurrentSceneData>(), access, walkingSpeed, clockRate, speeds) { }
        public RoadRoutes(CurrentSceneData? scene, PublicRoadAccess access, double walkingSpeed = 2, double clockRate = 1, NativeRoadSpeeds? speeds = null)
        {
            if (!Position.Finite(walkingSpeed) || walkingSpeed <= 0) throw new ArgumentOutOfRangeException(nameof(walkingSpeed));
            this.walkingSpeed = walkingSpeed;
            this.clockRate = clockRate;
            this.speeds = speeds ?? new NativeRoadSpeeds();
            this.access = access;
            this.scene = scene; hadScene = scene != null;
            waypoints = scene != null && scene.allWaypoints != null ? scene.allWaypoints : Array.Empty<Waypoint>();
            var previous = donor;
            if (Adoptable(previous))
            {
                nodes = previous!.nodes; cells = previous.cells; agentContracts = previous.agentContracts; carContracts = previous.carContracts;
                nativeContracts = previous.nativeContracts; nullNeighbours = previous.nullNeighbours; edgeTags = previous.edgeTags;
                edgeTagTopology = previous.edgeTagTopology; copiedNodes = previous.copiedNodes; TopologyId = previous.TopologyId;
                trafficDonor = previous.trafficBuilder; AdoptedSnapshot = true;
                if (previous.TrafficNetwork != null)
                { donorGateClear = (bool[])previous.closureGateClear.Clone(); donorGateTopology = previous.TrafficNetwork.TopologyId; donorGateAccess = previous.access; }
                topologyHash.Dispose();
            }
            graph = new TransportRoadGraph(nodes, clockRate);
            topologyWriter = new BinaryWriter(topologyBuffer);
            searchedAt = new int[waypoints.Length];
            searchDisabled = new bool[waypoints.Length];
        }

        public bool Prepare()
        {
            speeds.RefreshForFrame(Time.frameCount);
            ModFrames.Detail(GcAllocationSegment.ModRoutesSpeeds);
            if (speeds.Waiting != null) return false;
            try { return PrepareNodes(); }
            catch (RoadSpeedsWaitingException) { return false; }
            finally { ModFrames.Detail(GcAllocationSegment.ModRoutesNodes); }
        }

        private bool PrepareNodes()
        {
            long watchStarted = Stopwatch.GetTimestamp();
            int startedAt = copiedNodes;
            int stop = Math.Min(waypoints.Length, copiedNodes + 2048);
            while (copiedNodes < stop)
            {
                int i = copiedNodes++;
                var w = waypoints[i];
                var node = new TransportRoadNode();
                if (w != null)
                {
                    node.Position = Point(w.position); node.KilometresPerHour = speeds.Canonical(w);
                    node.AllowsCar = w.allowedAgents != null && w.allowedAgents.Contains((int)VehicleTypes.Car)
                        && (w.allowedCars == null || w.allowedCars.Contains(VehicleTypes.Car));
                    node.Successors = w.neighbors == null ? Array.Empty<int>() : w.neighbors.ToArray();
                }
                nodes.Add(node);
                nativeContracts.Add(w);
                nullNeighbours.Add(w?.neighbors == null);
                agentContracts.Add(sharedAgents.Intern(w?.allowedAgents));
                carContracts.Add(sharedCars.Intern(w?.allowedCars));
                HashNode(i, w, node);
                if (node.AllowsCar && node.Position.IsFinite && node.KilometresPerHour > 0)
                {
                    var key = Cell(node.Position);
                    if (!cells.TryGetValue(key, out var list)) { list = new List<int>(); cells.Add(key, list); }
                    list.Add(i);
                }
                if ((copiedNodes & 31) == 0 && ((Stopwatch.GetTimestamp() - watchStarted) * 1000.0 / Stopwatch.Frequency) >= 2) break;
            }
            if (copiedNodes != waypoints.Length) return false;
            if (TopologyId.Length == 0)
            {
                topologyHash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                TopologyId = BitConverter.ToString(topologyHash.Hash!).Replace("-", "").ToLowerInvariant();
                topologyHash.Dispose();
                topologyWriter.Dispose();
            }
            if (Ready)
            {
                if (!IsCurrent) return false;
                donor = this;
                return true;
            }
            double remaining = 2 - ((Stopwatch.GetTimestamp() - watchStarted) * 1000.0 / Stopwatch.Frequency);
            int remainingUnits = 2048 - (copiedNodes - startedAt);
            if (remaining <= 0 || remainingUnits <= 0) return false;
            if (trafficBuilder == null) { trafficBuilder = new TrafficRoadNetwork(TopologyId, nodes, waypoints, scene, access, clockRate, SnapshotNodeCurrent, trafficDonor); trafficDonor = null; }
            if (!trafficBuilder.Prepare(remaining, remainingUnits))
            { if (trafficBuilder.Stale && !unsafeTopology) TrafficInvalid(); return false; }
            TrafficNetwork = trafficBuilder.Network;
            AdoptGateClearance();
            if (!Ready || !IsCurrent) return false;
            donor = this;
            return true;
        }
        private void AdoptGateClearance()
        {
            var clear = donorGateClear;
            var gates = donorGateAccess;
            string topology = donorGateTopology;
            donorGateClear = null; donorGateAccess = null; donorGateTopology = "";
            var network = TrafficNetwork;
            if (clear == null || gates == null || network == null || clear.Length != network.Links.Count
                || !string.Equals(topology, network.TopologyId, StringComparison.Ordinal) || !gates.SameGates(access)) return;
            closureGateClear = clear;
            AdoptedGateClearance = true;
        }
        private bool Adoptable(RoadRoutes? previous)
        {
            if (previous == null || scene == null || !ReferenceEquals(previous.scene, scene) || !ReferenceEquals(previous.waypoints, waypoints)
                || previous.copiedNodes != waypoints.Length || previous.TopologyId.Length == 0 || previous.trafficBuilder == null) return false;
            return previous.Ready && previous.IsCurrent;
        }
        private void HashNode(int index, Waypoint? native, TransportRoadNode node)
        {
            topologyBuffer.SetLength(0); topologyBuffer.Position = 0;
            var writer = topologyWriter;
            writer.Write(index); writer.Write(native != null); writer.Write(native?.listIndex ?? -1);
            writer.Write(node.Position.X); writer.Write(node.Position.Y); writer.Write(node.Position.Z);
            writer.Write(node.KilometresPerHour); writer.Write(node.AllowsCar);
            Write(agentContracts[index]);
            var cars = carContracts[index];
            writer.Write(cars?.Length ?? -1);
            if (cars != null) foreach (var car in cars) writer.Write((int)car);
            Write(native?.neighbors == null ? null : node.Successors);
            writer.Flush();
            var bytes = topologyBuffer.GetBuffer();
            topologyHash.TransformBlock(bytes, 0, checked((int)topologyBuffer.Length), bytes, 0);
            void Write(int[]? values) { writer.Write(values?.Length ?? -1); if (values != null) foreach (int value in values) writer.Write(value); }
        }

        public List<Curb> Near(Position position, double radius, int limit = 2) => Near(position, radius, limit, new List<Curb>());

        public List<Curb> Near(Position position, double radius, int limit, List<Curb> result)
        {
            var query = syncQuery ??= new NearQuery();
            BeginNear(query, position, radius, limit, result);
            ContinueNear(query, long.MaxValue);
            return result;
        }

        internal sealed class NearQuery : ICurbSource
        {
            internal readonly CurbSearch Search = new CurbSearch();
            internal RoadRoutes? Owner;
            internal Position Origin;
            internal double Radius;
            internal int Limit;
            internal List<Curb>? Result;
            public long Now() => Stopwatch.GetTimestamp();
            public void CollectCell(int x, int z, List<int> into) => Owner!.CollectCell(this, x, z, into);
            public void Order(List<int> nearby) => Owner!.OrderNearby(Origin, nearby);
            public bool Full => Result!.Count >= Limit;
            public bool Usable(int candidate) => !Owner!.Disabled(candidate);
            public void Attempt(int candidate) => Owner!.AttemptCurb(this, candidate);
            public bool DropUnusable()
            {
                var result = Result!;
                for (int i = 0; i < result.Count; i++)
                    if (Owner!.Disabled(result[i].Node)) { result.RemoveAt(i); return true; }
                return false;
            }
        }

        private NearQuery? syncQuery;

        internal void BeginNear(NearQuery query, Position position, double radius, int limit, List<Curb> result)
        {
            result.Clear();
            trafficBuilder?.BeginQuery();
            query.Owner = this; query.Origin = position; query.Radius = radius; query.Limit = limit; query.Result = result;
            if (!Ready || !position.IsFinite || Math.Abs(position.X) > 1000000 || Math.Abs(position.Z) > 1000000
                || !Position.Finite(radius) || radius < 0 || radius > 100 || limit < 1 || limit > 2) { query.Search.Cancel(); return; }
            var cell = Cell(position);
            query.Search.Begin(cell.X, cell.Z, (int)Math.Ceiling(radius / 50));
        }

        internal bool ContinueNear(NearQuery query, long deadline)
        {
            if (query.Search.Done) return true;
            if (!ReferenceEquals(query.Owner, this) || !Ready || query.Search.Calls > 0 && !IsCurrent) { query.Search.Cancel(); query.Result?.Clear(); return true; }
            trafficBuilder?.BeginQuery();
            return query.Search.Continue(query, deadline);
        }

        private void CollectCell(NearQuery query, int x, int z, List<int> into)
        {
            if (!cells.TryGetValue((x, z), out var indices)) return;
            foreach (int index in indices) if (!Disabled(index) && Position.Distance(query.Origin, nodes[index].Position) <= query.Radius) into.Add(index);
        }

        private void OrderNearby(Position origin, List<int> nearby)
        {
            var order = nearbyOrder ??= new NearbyOrder(nodes);
            order.Origin = origin;
            nearby.Sort(nearbyCompare ??= order.Compare);
        }

        private void AttemptCurb(NearQuery query, int index)
        {
            var result = query.Result!;
            var road = nodes[index].Position;
            if (!NavMesh.SamplePosition(Vector(road), out NavMeshHit hit, (float)ConnectorMetres, WalkingRoutes.DryFilter)) return;
            Position curb = Point(hit.position);
            foreach (var old in result) if (old.Node == index || Position.Distance(old.Position, curb) < 5) return;
            if (!ConnectorClear(curb, road)) return;
            result.Add(new Curb { Node = index, Position = curb });
        }

        private NearbyOrder? nearbyOrder;
        private Comparison<int>? nearbyCompare;
        private sealed class NearbyOrder : IComparer<int>
        {
            private readonly List<TransportRoadNode> nodes;
            internal Position Origin;
            internal NearbyOrder(List<TransportRoadNode> nodes) { this.nodes = nodes; }
            public int Compare(int a, int b)
            {
                int order = Position.Distance(Origin, nodes[a].Position).CompareTo(Position.Distance(Origin, nodes[b].Position));
                return order != 0 ? order : a.CompareTo(b);
            }
        }

        public List<Curb> AtParked(Position parked) => AtParked(parked, new List<Curb>());

        public List<Curb> AtParked(Position parked, List<Curb> candidates)
        {
            Near(parked, ConnectorMetres, 2, candidates);
            int kept = 0;
            for (int i = 0; i < candidates.Count; i++)
                if (ConnectorClear(parked, nodes[candidates[i].Node].Position)) candidates[kept++] = candidates[i];
            if (kept != candidates.Count) candidates.RemoveRange(kept, candidates.Count - kept);
            foreach (var c in candidates) c.Position = parked;
            return candidates;
        }

        public Route? Plan(Curb from, Curb to, double departureMinute = 0,
            double maximumEstimatedMinutes = double.PositiveInfinity)
        {
            using var plan = BeginPlan(from, to, departureMinute, maximumEstimatedMinutes);
            plan.Continue(long.MaxValue);
            return plan.Result;
        }

        public RoadPlan BeginPlan(Curb from, Curb to, double departureMinute = 0, double maximumEstimatedMinutes = double.PositiveInfinity)
            => new RoadPlan(this, from, to, departureMinute, maximumEstimatedMinutes);
        public int SearchRestarts { get; private set; }

        internal sealed class RoadPlan : IDisposable
        {
            internal const int ExpansionStep = 16, StepsPerUnit = 4;
            private readonly RoadRoutes owner;
            private readonly Curb from, to;
            private readonly double departureMinute, maximumEstimatedMinutes;
            private TransportRoadGraph.RoadSearch? search;
            private int generation;
            private Route? begin, end;
            private Dictionary<int, TrafficLinkForecast>? captured;
            private Dictionary<int, double>? entryDelays;
            private PlanScratch? scratch;
            private TrafficNetwork? network;
            public bool Finished { get; private set; }
            public Route? Result { get; private set; }
            public int Restarts { get; private set; }
            public bool WaitingForSpeeds { get; private set; }
            public int Expanded => search?.Expanded ?? 0;

            internal RoadPlan(RoadRoutes owner, Curb from, Curb to, double departureMinute, double maximumEstimatedMinutes)
            {
                this.owner = owner; this.from = from; this.to = to;
                this.departureMinute = departureMinute; this.maximumEstimatedMinutes = maximumEstimatedMinutes;
                Start();
            }

            private void Start()
            {
                var owner = this.owner; var from = this.from; var to = this.to;
                var speeds = owner.speeds; var nodes = owner.nodes; var waypoints = owner.waypoints;
                speeds.RefreshForFrame(Time.frameCount);
                if (speeds.Waiting != null) { WaitingForSpeeds = true; return; }
                WaitingForSpeeds = false;
                if (!owner.Ready || owner.TrafficNetwork == null || !Position.Finite(departureMinute) || departureMinute < 0
                    || from.Node == to.Node || !owner.ConnectorClear(from.Position, nodes[from.Node].Position) || !owner.ConnectorClear(to.Position, nodes[to.Node].Position))
                { Finish(null); return; }
                completing = completeResumed = recheckWaiting = RecheckDeferred = false; completeStage = completeIndex = recheckWaits = 0; recheckWaitFrame = long.MinValue;
                if (owner.searchGeneration == int.MaxValue) { Array.Clear(owner.searchedAt, 0, owner.searchedAt.Length); owner.searchGeneration = 0; }
                owner.searchGeneration++;
                generation = owner.searchGeneration;
                owner.trafficBuilder?.BeginQuery();
                begin = owner.Connector(from.Position, nodes[from.Node].Position);
                end = owner.Connector(nodes[to.Node].Position, to.Position);
                double roadCeiling = TransportComposition.RemainingSearchMinutes(maximumEstimatedMinutes, begin.Minutes + end.Minutes);
                var scratch = this.scratch ??= owner.planScratch.Count > 0 ? owner.planScratch.Pop() : new PlanScratch();
                scratch.Clear();
                captured = scratch.Captured;
                entryDelays = scratch.EntryDelays;
                network = owner.TrafficNetwork;
                scratch.Begin(speeds, waypoints, network, owner.trafficForecast, owner.trafficEntryForecast, from.Node, to.Node, departureMinute + begin.Minutes);
                search = owner.graph.BeginSearch(from.Node, to.Node, owner.disabledSearch ??= owner.SearchDisabled, owner.ClearSegment(), scratch.Traversal,
                    pathFound: scratch.PathFound, extraMinutes: scratch.Extra, maximumEstimatedMinutes: roadCeiling);
            }

            public bool Continue(long deadlineTimestamp)
            {
                if (Finished) return true;
                RecheckDeferred = false;
                if (WaitingForSpeeds || owner.searchGeneration != generation)
                {
                    if (!WaitingForSpeeds) { Restarts++; owner.SearchRestarts++; }
                    search?.Dispose(); search = null;
                    Start();
                    if (Finished) return true;
                    if (WaitingForSpeeds) return false;
                }
                int steps = 0;
                while (true)
                {
                    bool done;
                    if (!completing)
                    {
                        try { done = search!.Advance(ExpansionStep); }
                        catch (RoadSpeedsWaitingException) { search?.Dispose(); search = null; WaitingForSpeeds = true; return false; }
                        if (done) { completing = true; completeStage = completeIndex = 0; completeResumed = false; }
                    }
                    else
                    {
                        try { done = CompleteStep(ExpansionStep, deadlineTimestamp); }
                        catch (RoadSpeedsWaitingException) { search?.Dispose(); search = null; WaitingForSpeeds = true; return false; }
                        if (done) return true;
                        if (recheckWaiting) { recheckWaiting = false; RecheckDeferred = true; return false; }
                    }
                    if (deadlineTimestamp == long.MaxValue) continue;
                    if (++steps >= StepsPerUnit || Stopwatch.GetTimestamp() >= deadlineTimestamp) { completeResumed |= completing; return false; }
                }
            }

            private bool completing, completeResumed, recheckWaiting;
            private int completeStage, completeIndex, recheckWaits;
            private long recheckWaitFrame = long.MinValue;
            public bool RecheckDeferred { get; private set; }
            public int CompletionUnits { get; private set; }

            private bool CompleteStep(int units, long deadlineTimestamp)
            {
                var owner = this.owner; var road = search!.Result; var path = scratch?.Path; var network = this.network!;
                CompletionUnits++;
                if (road == null || path == null) { Finish(null); return true; }
                while (true)
                {
                    switch (completeStage)
                    {
                        case 0:
                            if (completeIndex >= path.Length) { completeStage = 1; continue; }
                            if (units-- <= 0) return false;
                            if (!owner.NativePathNodeValid(path, completeIndex++)) { Finish(null); return true; }
                            continue;
                        case 1:
                            if (owner.TopologyId.Length != 64 || road.Legs.Count != path.Length - 1) { Finish(null); return true; }
                            completeStage = 2; completeIndex = 0;
                            continue;
                        case 2:
                        {
                            if (completeIndex >= road.Legs.Count) { completeStage = 3; continue; }
                            if (units-- <= 0) return false;
                            int i = completeIndex++;
                            if (!network.TryGetEdge(path[i], path[i + 1], out var edge)
                                || captured!.TryGetValue(edge.LinkId, out var value) && (value.Closed || !Position.Finite(value.DelayMinutes))
                                || i == 0 && entryDelays!.TryGetValue(edge.LinkId, out var entryDelay) && (!Position.Finite(entryDelay) || entryDelay < 0)) { Finish(null); return true; }
                            road.Legs[i].ServiceId = owner.EdgeTag(path[i], path[i + 1]);
                            continue;
                        }
                        default:
                            if (completeResumed)
                            {
                                long checkStarted = Stopwatch.GetTimestamp();
                                if (!owner.edgeRecheck.Admits(checkStarted, deadlineTimestamp, path.Length, Time.frameCount, ref recheckWaits, ref recheckWaitFrame)) { recheckWaiting = true; return false; }
                                bool open = StillOpen(path);
                                owner.edgeRecheck.Record(Stopwatch.GetTimestamp() - checkStarted, path.Length);
                                if (!open) { Finish(null); return true; }
                            }
                            Finish(TransportComposition.Join(TransportComposition.Shared(begin!), TransportComposition.Frozen(road), TransportComposition.Shared(end!)));
                            return true;
                    }
                }
            }

            private bool StillOpen(int[] path)
            {
                if (!owner.Ready || !owner.IsCurrent || !ReferenceEquals(owner.TrafficNetwork, network)) return false;
                for (int i = 0; i < path.Length; i++)
                    if (owner.ClosedByFlags(path[i]) || i > 0 && !owner.EdgeStillLinked(path[i - 1], path[i])) return false;
                return true;
            }

            private void Finish(Route? result)
            {
                Finished = true; Result = result;
                search?.Dispose(); search = null;
                ReturnScratch();
            }

            public void Dispose()
            {
                if (!Finished) { Finished = true; Result = null; }
                search?.Dispose(); search = null;
                ReturnScratch();
            }

            private void ReturnScratch()
            {
                if (scratch == null) return;
                scratch.Clear();
                owner.planScratch.Push(scratch);
                scratch = null; captured = null; entryDelays = null;
            }
        }

        private readonly Dictionary<long, string> edgeTags = new Dictionary<long, string>();
        private string edgeTagTopology = "";
        internal int EdgeTags => edgeTags.Count;
        private string EdgeTag(int from, int to)
        {
            if (!string.Equals(edgeTagTopology, TopologyId, StringComparison.Ordinal)) { edgeTags.Clear(); edgeTagTopology = TopologyId; }
            long key = (long)from << 32 | (uint)to;
            if (edgeTags.TryGetValue(key, out var tag)) return tag;
            tag = string.Concat("native:road:v1:", TopologyId, ":", from.ToString(CultureInfo.InvariantCulture) + ":" + to.ToString(CultureInfo.InvariantCulture));
            edgeTags.Add(key, tag);
            return tag;
        }

        private sealed class PlanScratch
        {
            public readonly Dictionary<int, TrafficLinkForecast> Captured = new Dictionary<int, TrafficLinkForecast>();
            public readonly Dictionary<int, double> EntryDelays = new Dictionary<int, double>();
            public readonly Dictionary<int, double> SectionPace = new Dictionary<int, double>();
            public readonly Func<int, int, double, double> Traversal;
            public readonly Func<int, int, double> Extra;
            public readonly Action<int[]> PathFound;
            public int[]? Path;
            private NativeRoadSpeeds? speeds;
            private Waypoint[]? waypoints;
            private TrafficNetwork? network;
            private Func<int, double, TrafficLinkForecast>? forecast;
            private Func<int, double, double, double, double>? entryForecast;
            private int fromNode, toNode;
            private double entryMinute;

            public PlanScratch()
            {
                Traversal = TraversalMinutes;
                Extra = ExtraMinutes;
                PathFound = found => Path = found;
            }

            public void Begin(NativeRoadSpeeds speeds, Waypoint[] waypoints, TrafficNetwork network, Func<int, double, TrafficLinkForecast>? forecast,
                Func<int, double, double, double, double>? entryForecast, int fromNode, int toNode, double entryMinute)
            {
                this.speeds = speeds; this.waypoints = waypoints; this.network = network; this.forecast = forecast; this.entryForecast = entryForecast;
                this.fromNode = fromNode; this.toNode = toNode; this.entryMinute = entryMinute;
            }

            public void Clear()
            {
                Captured.Clear(); EntryDelays.Clear(); SectionPace.Clear();
                Path = null; speeds = null; waypoints = null; network = null; forecast = null; entryForecast = null;
            }

            private double TraversalMinutes(int a, int b, double baseline)
            {
                var speeds = this.speeds!; var network = this.network!; var waypoints = this.waypoints!;
                if (!speeds.HasOverrides) return baseline;
                if (!network.TryGetEdge(a, b, out var edge)) return double.NaN;
                if (!SectionPace.TryGetValue(edge.LinkId, out double factor))
                {
                    var indices = network.NativeNodes(edge.LinkId);
                    double effective = 0; bool changed = false;
                    for (int i = 1; i < indices.Count; i++)
                    {
                        int fromNode = indices[i - 1];
                        if (!network.TryGetEdge(fromNode, indices[i], out var part)) return double.NaN;
                        double original = Math.Min(40, speeds.Canonical(waypoints[fromNode]));
                        double current = Math.Min(40, speeds.Effective(waypoints[fromNode]));
                        changed |= original != current;
                        effective += part.FreeFlowMinutes * (original / current);
                    }
                    factor = changed ? Math.Max(1, effective / network.Links[edge.LinkId].FreeFlowMinutes) : 1;
                    SectionPace.Add(edge.LinkId, factor);
                }
                return baseline * factor;
            }

            private double ExtraMinutes(int a, int b)
            {
                var network = this.network!;
                if (!network.TryGetEdge(a, b, out var edge)) return 1e15;
                if (forecast == null) return 0;
                double entryDelay = 0;
                if (a == fromNode && edge.StartLinkFraction > 0 && entryForecast != null)
                {
                    if (!EntryDelays.TryGetValue(edge.LinkId, out entryDelay))
                    {
                        double endFraction = 1;
                        var sectionNodes = network.NativeNodes(edge.LinkId);
                        for (int i = 1; i < sectionNodes.Count; i++)
                            if (sectionNodes[i] == toNode && network.TryGetEdge(sectionNodes[i - 1], toNode, out var final)
                                && final.EndLinkFraction > edge.StartLinkFraction)
                            { endFraction = final.EndLinkFraction; break; }
                        entryDelay = entryForecast(edge.LinkId, entryMinute, edge.StartLinkFraction, endFraction - edge.StartLinkFraction);
                        EntryDelays.Add(edge.LinkId, entryDelay);
                    }
                    if (!Position.Finite(entryDelay) || entryDelay < 0) return 1e15;
                }
                if (!Captured.TryGetValue(edge.LinkId, out var value)) { value = forecast(edge.LinkId, entryMinute); Captured.Add(edge.LinkId, value); }
                if (value.Closed || !Position.Finite(value.DelayMinutes)) return 1e15;
                return entryDelay + value.DelayMinutes * edge.FreeFlowMinutes / network.Links[edge.LinkId].FreeFlowMinutes;
            }
        }
        private readonly Stack<PlanScratch> planScratch = new Stack<PlanScratch>();
        private readonly AtomicCheckReserve edgeRecheck = new AtomicCheckReserve(Stopwatch.Frequency * 2e-6);
        public long EdgeRechecksDeferred => edgeRecheck.Deferred;
        public long EdgeRechecksForced => edgeRecheck.Forced;

        private bool ConnectorClear(Position curb, Position road) => curb.IsFinite && road.IsFinite
            && Position.Distance(curb, road) <= ConnectorMetres && Math.Abs(curb.Y - road.Y) <= 1.5
            && !access.NearGate(curb, 3) && access.ClearSegment(curb, road) && PhysicalClear(curb, road);
        private bool PhysicalClear(Position a, Position b)
        {
            Vector3 start = Vector(a) + Vector3.up * 0.8f, end = Vector(b) + Vector3.up * 0.8f;
            if (obstacleMask == 0) return false;
            var delta = end - start;
            float length = delta.magnitude;
            if (length <= 0.000001f) return true;
            int count = Physics.RaycastNonAlloc(start, delta / length, clearHits, length, obstacleMask, QueryTriggerInteraction.Ignore);
            if (count == clearHits.Length) return !Physics.Linecast(start, end, obstacleMask, QueryTriggerInteraction.Ignore) || OnlyOwnCars(start, end);
            for (int i = 0; i < count; i++) if (!PhysicalVehicles.IsOwnCar(clearHits[i].collider)) return false;
            return true;
        }
        private readonly RaycastHit[] clearHits = new RaycastHit[32];
        private bool OnlyOwnCars(Vector3 start, Vector3 end)
        {
            foreach (var hit in Physics.RaycastAll(start, (end - start).normalized, (end - start).magnitude, obstacleMask, QueryTriggerInteraction.Ignore))
                if (!PhysicalVehicles.IsOwnCar(hit.collider)) return false;
            return true;
        }
        private Route Connector(Position from, Position to)
        {
            var route = new Route();
            double length = Position.Distance(from, to);
            if (length > 0.000001) route.Legs.Add(new RouteLeg { From = from, To = to, Mode = TravelMode.Car, Minutes = length / walkingSpeed });
            return route;
        }
        private bool NativePathNodeValid(int[] path, int i)
        {
            int index = path[i];
            if (Disabled(index)) return false;
            if (i == 0) return true;
            int previous = path[i - 1];
            var native = waypoints[previous];
            if (native.neighbors == null || !native.neighbors.Contains(index)) return Invalid("path-neighbours", previous);
            return access.ClearSegment(nodes[previous].Position, nodes[index].Position);
        }
        private bool Disabled(int index)
        {
            if (index < 0 || index >= waypoints.Length || waypoints[index] == null) return true;
            if (!SnapshotNodeCurrent(index)) return true;
            if (trafficBuilder != null && !trafficBuilder.NodeCurrent(index)) { TrafficInvalid(); return true; }
            return ClosedByFlags(index);
        }
        private bool EdgeStillLinked(int from, int to)
        {
            var native = waypoints[from];
            if (!ReferenceEquals(native, nativeContracts[from]) || !ReferenceEquals(waypoints[to], nativeContracts[to])
                || native.neighbors == null || !native.neighbors.Contains(to)) return Invalid("path-neighbours", from);
            return true;
        }
        private bool ClosedByFlags(int index)
        {
            if (index < 0 || index >= waypoints.Length || waypoints[index] == null) return true;
            var native = waypoints[index];
            if (native.temporaryDisabled || native.allowedAgents == null || !native.allowedAgents.Contains((int)VehicleTypes.Car)
                || (native.allowedCars != null && !native.allowedCars.Contains(VehicleTypes.Car))) return true;
            return false;
        }
        private bool SnapshotNodeCurrent(int index)
        {
            if (index < 0 || index >= copiedNodes || !ReferenceEquals(waypoints[index], nativeContracts[index]))
                return Invalid("waypoint-identity", index);
            var native = waypoints[index];
            if (native == null) return true;
            bool agentsMatch = SameContract(native.allowedAgents, agentContracts[index]);
            bool carsMatch = SameContract(native.allowedCars, carContracts[index]);
            bool edgesMatch = (native.neighbors == null) == nullNeighbours[index] && (native.neighbors == null
                ? nodes[index].Successors.Length == 0 : SameContract(native.neighbors, nodes[index].Successors));
            var currentPosition = Point(native.position); var capturedPosition = nodes[index].Position;
            bool positionMatches = currentPosition.IsFinite && capturedPosition.IsFinite
                ? Position.Distance(currentPosition, capturedPosition) <= .001
                : currentPosition.X.Equals(capturedPosition.X) && currentPosition.Y.Equals(capturedPosition.Y) && currentPosition.Z.Equals(capturedPosition.Z);
            if (!agentsMatch) return Invalid("agent-restrictions", index);
            if (!carsMatch) return Invalid("car-restrictions", index);
            if (!edgesMatch) return Invalid("neighbours", index);
            if (native.listIndex != index) return Invalid("waypoint-index", index);
            int canonical;
            try { canonical = speeds.Canonical(native); }
            catch (InvalidOperationException error) when (!(error is RoadSpeedsWaitingException))
            {
                if (InvalidationReason.Length == 0) { InvalidationReason = "unverified-speed"; InvalidationIndex = index; }
                throw;
            }
            if (canonical != nodes[index].KilometresPerHour) return Invalid("canonical-speed", index);
            if (!positionMatches) return Invalid("position", index);
            return true;
        }
        private static bool SameContract<T>(List<T>? current, T[]? captured)
        {
            if (current == null) return captured == null;
            if (captured == null || current.Count != captured.Length) return false;
            var comparer = EqualityComparer<T>.Default;
            for (int i = 0; i < captured.Length; i++)
                if (!comparer.Equals(current[i], captured[i])) return false;
            return true;
        }
        private Func<int, bool>? disabledSearch;
        private Func<Position, Position, bool>? clearSegment;
        private PublicRoadAccess? clearSegmentAccess;
        private Func<Position, Position, bool> ClearSegment()
        {
            if (clearSegment == null || !ReferenceEquals(clearSegmentAccess, access)) { clearSegment = access.ClearSegment; clearSegmentAccess = access; }
            return clearSegment;
        }
        private bool SearchDisabled(int index)
        {
            if (index < 0 || index >= waypoints.Length) return true;
            if (searchedAt[index] != searchGeneration)
            {
                searchDisabled[index] = Disabled(index);
                searchedAt[index] = searchGeneration;
            }
            return searchDisabled[index];
        }
        public Waypoint? ResolveWaypoint(string topology, int index)
        {
            trafficBuilder?.BeginQuery();
            if (!Ready || !IsCurrent || TopologyId.Length != 64 || !string.Equals(topology, TopologyId, StringComparison.Ordinal)
                || Disabled(index)) return null;
            return waypoints[index];
        }
        private Waypoint? ResolveSampled(string topology, int index)
        {
            if (!sampleScope) return ResolveWaypoint(topology, index);
            if (!Ready || !IsCurrent || TopologyId.Length != 64 || !string.Equals(topology, TopologyId, StringComparison.Ordinal)
                || index < 0 || index >= waypoints.Length) return null;
            if (sampledAt[index] != sampleGeneration)
            { sampleDisabled[index] = Disabled(index); sampledAt[index] = sampleGeneration; }
            return sampleDisabled[index] ? null : waypoints[index];
        }
        public bool TryPhysicalRoadSample(CitizenState citizen, PhysicalJourneySample sample, out PhysicalRoadSample roadSample)
        {
            roadSample = default;
            if (sampleScope) return false;
            if (sampledAt.Length != waypoints.Length) { sampledAt = new int[waypoints.Length]; sampleDisabled = new bool[waypoints.Length]; }
            if (sampleGeneration == int.MaxValue) { Array.Clear(sampledAt, 0, sampledAt.Length); sampleGeneration = 0; }
            sampleGeneration++;
            trafficBuilder?.BeginQuery();
            sampleScope = true;
            try { return SamplePhysicalRoad(citizen, sample, out roadSample); }
            finally { sampleScope = false; trafficBuilder?.BeginQuery(); }
        }
        private bool SamplePhysicalRoad(CitizenState citizen, PhysicalJourneySample sample, out PhysicalRoadSample roadSample)
        {
            roadSample = default;
            var legs = citizen.Journey?.Legs;
            if (legs == null || citizen.Car == null || sample.Mode != TravelMode.Car || sample.Stationary
                || sample.LegIndex < 0 || sample.LegIndex >= legs.Count || !sample.Position.IsFinite) return false;
            var current = legs[sample.LegIndex];
            if (current.ServiceId != sample.ServiceId || Position.Distance(current.From, sample.From) > 0.001
                || Position.Distance(current.To, sample.To) > 0.001 || !TryTag(current, out int from, out int to)) return false;
            int firstLeg = sample.LegIndex;
            while (firstLeg > 0)
            {
                var earlier = legs[firstLeg - 1];
                if (earlier == null || earlier.ServiceId == null) return false;
                if (!earlier.ServiceId.StartsWith("native:road:", StringComparison.Ordinal)) break;
                if (sample.LegIndex - firstLeg >= 510) return false;
                firstLeg--;
            }
            var targets = new List<int>();
            var boundaries = new List<RoadBoundary>();
            var junctions = new List<object>();
            var associated = AssociatedIntersection;
            if (associated == null) return false;
            double distance = 0, progress = 0;
            int previousFrom = -1, previousTo = -1, nodeCount = 0, lastBoundaryNode = -1;
            for (int i = firstLeg; i < legs.Count; i++)
            {
                var leg = legs[i];
                if (leg == null || leg.ServiceId == null) return false;
                if (!leg.ServiceId.StartsWith("native:road:", StringComparison.Ordinal)) break;
                bool fragment = leg.ServiceId.StartsWith("native:road:fragment1:", StringComparison.Ordinal);
                int a, b;
                if (++nodeCount >= 512 || !(fragment ? KnownPhysicalFragment(leg, out a, out b) : TryTag(leg, out a, out b))) return false;
                if (fragment && i > sample.LegIndex) break;
                if (previousTo >= 0 && (Position.Distance(legs[i - 1].To, leg.From) > .001
                    || (a != previousTo && (a != previousFrom || b != previousTo)))) return false;
                if ((!fragment || Position.Distance(leg.From, nodes[a].Position) <= .001) && !Boundary(a, distance)) return false;
                if (i == sample.LegIndex) progress = distance + Position.Distance(leg.From, sample.Position);
                distance += Position.Distance(leg.From, leg.To);
                if ((!fragment || Position.Distance(leg.To, nodes[b].Position) <= .001) && !Boundary(b, distance)) return false;
                if (i >= sample.LegIndex) targets.Add(b);
                previousFrom = a; previousTo = b;
            }
            roadSample = new PhysicalRoadSample(sample.Position, current.To, from, targets, TopologyId, boundaries, progress);
            return true;

            bool Boundary(int index, double at)
            {
                var native = ResolveSampled(TopologyId, index);
                if (native == null) return false;
                bool repeatedEndpoint = index == lastBoundaryNode;
                lastBoundaryNode = index;
                if (!native.enter && !native.exit) return true;
                var junction = associated.GetValue(native);
                if (junction == null || native.enter == native.exit) return false;
                if (repeatedEndpoint)
                {
                    var prior = boundaries[boundaries.Count - 1];
                    boundaries[boundaries.Count - 1] = new RoadBoundary(native.enter ? Math.Min(prior.Distance, at) : Math.Max(prior.Distance, at), prior.Enter, prior.Exit, prior.Junction);
                    return true;
                }
                int identity = junctions.FindIndex(j => ReferenceEquals(j, junction));
                if (identity < 0) { identity = junctions.Count; junctions.Add(junction); }
                boundaries.Add(new RoadBoundary(at, native.enter, native.exit, identity));
                return true;
            }
        }
        private bool KnownPhysicalFragment(RouteLeg leg, out int from, out int to)
        {
            from = to = -1;
            if (leg.Mode != TravelMode.Car || !leg.From.IsFinite || !leg.To.IsFinite || !Position.Finite(leg.Minutes) || leg.Minutes <= 0) return false;
            var tag = leg.RoadTag;
            if (tag.Kind != RoadTagKind.Fragment1 || !tag.IsTopology(leg.ServiceId, TopologyId) || tag.From == tag.To) return false;
            from = tag.From; to = tag.To;
            var a = ResolveSampled(TopologyId, from); var b = ResolveSampled(TopologyId, to);
            if (a == null || b == null || a.neighbors == null || !a.neighbors.Contains(to) || !access.ClearSegment(leg.From, leg.To)) return false;
            Position start = Point(a.position), end = Point(b.position);
            double x = end.X - start.X, y = end.Y - start.Y, z = end.Z - start.Z;
            double square = x * x + y * y + z * z;
            if (!Position.Finite(square) || square <= .000000000001) return false;
            double Project(Position point) => ((point.X - start.X) * x + (point.Y - start.Y) * y + (point.Z - start.Z) * z) / square;
            double first = Project(leg.From), last = Project(leg.To);
            return Position.Finite(first) && Position.Finite(last) && last >= first
                && Position.Distance(leg.From, Position.Lerp(start, end, Math.Max(0, Math.Min(1, first)))) <= .001
                && Position.Distance(leg.To, Position.Lerp(start, end, Math.Max(0, Math.Min(1, last)))) <= .001;
        }
        private bool TryTag(RouteLeg leg, out int from, out int to)
        {
            from = to = -1;
            if (leg.Mode != TravelMode.Car || string.IsNullOrEmpty(leg.ServiceId)) return false;
            var tag = leg.RoadTag;
            if (tag.Kind != RoadTagKind.V1 || !tag.IsTopology(leg.ServiceId, TopologyId)) return false;
            from = tag.From; to = tag.To;
            var a = ResolveSampled(TopologyId, from); var b = ResolveSampled(TopologyId, to);
            return a != null && b != null && a.neighbors != null && a.neighbors.Contains(to)
                && Position.Distance(Point(a.position), leg.From) <= 0.001 && Position.Distance(Point(b.position), leg.To) <= 0.001
                && access.ClearSegment(leg.From, leg.To);
        }
        private static (int X, int Z) Cell(Position point) => ((int)Math.Floor(point.X / 50), (int)Math.Floor(point.Z / 50));
        private static Position Point(Vector3 v) => new Position(v.x, v.y, v.z);
        private static Vector3 Vector(Position v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
    }
}
