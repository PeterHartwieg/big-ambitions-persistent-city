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
    /// <summary>Conservative public access: all vehicle gates are excluded, regardless of player property ownership.</summary>
    internal sealed class PublicRoadAccess
    {
        private readonly List<GateBox> gates = new List<GateBox>();
        private const double GateCellMetres = 50;
        private const int MaximumGateCells = 64;
        private const int LinearGateLimit = 8;
        private readonly Dictionary<(int X, int Z), List<GateBox>> gateCells = new Dictionary<(int, int), List<GateBox>>();
        // Very large or malformed boxes keep the original scan instead of filling an unbounded grid.
        private readonly List<GateBox> unindexedGates = new List<GateBox>();
        private readonly struct GateBox
        {
            public GateBox(Bounds bounds, string door) : this(Point(bounds.min), Point(bounds.max), door) { }
            private GateBox(Position min, Position max, string door) { Min = min; Max = max; Door = door; }
            public Position Min { get; }
            public Position Max { get; }
            public string Door { get; }
            public bool Same(GateBox other) => Min.X.Equals(other.Min.X) && Min.Y.Equals(other.Min.Y) && Min.Z.Equals(other.Min.Z)
                && Max.X.Equals(other.Max.X) && Max.Y.Equals(other.Max.Y) && Max.Z.Equals(other.Max.Z);
            public bool Covers(GateBox other) => Min.X <= other.Min.X && Min.Y <= other.Min.Y && Min.Z <= other.Min.Z
                && Max.X >= other.Max.X && Max.Y >= other.Max.Y && Max.Z >= other.Max.Z;
            public GateBox Union(GateBox other) => new GateBox(
                new Position(Math.Min(Min.X, other.Min.X), Math.Min(Min.Y, other.Min.Y), Math.Min(Min.Z, other.Min.Z)),
                new Position(Math.Max(Max.X, other.Max.X), Math.Max(Max.Y, other.Max.Y), Math.Max(Max.Z, other.Max.Z)), Door);
        }
        // Per door: the box of its parts that do not move, and the gate box in use.
        private Dictionary<FenceDoor, (GateBox? Fixed, GateBox Gate)> doorBoxes = new Dictionary<FenceDoor, (GateBox?, GateBox)>();
        public PublicRoadAccess() : this(Doors(), null) { BuildIndex(); }
        public PublicRoadAccess(FenceDoor[] doors) : this(doors, null) { BuildIndex(); }
        private PublicRoadAccess(FenceDoor[] doors, PublicRoadAccess? previous)
        {
            foreach (var door in doors)
            {
                if (door == null || (!(door is HamptonsPrivateFenceDoor) && (!door.acceptsVehicles || door.IsForSale()))) continue;
                var colliders = door.GetComponentsInChildren<Collider>(true);
                GateBox? fixedParts = Combine(colliders.Where(collider => !Leaf(door, collider.transform))) is Bounds still
                    ? new GateBox(still, door.name) : (GateBox?)null;
                GateBox gate;
                if (Combine(colliders) is Bounds geometry)
                {
                    geometry.Expand(new Vector3(1, 2, 1)); gate = new GateBox(geometry, door.name);
                    // The leaves swing or slide whenever anyone passes. While the parts that do not move are
                    // unchanged, the box only grows to take in each new leaf pose: it always covers what a fresh
                    // read would give, and stops changing once the leaves have been seen at their extremes.
                    if (previous != null && fixedParts.HasValue && previous.doorBoxes.TryGetValue(door, out var known)
                        && known.Fixed.HasValue && known.Fixed.Value.Same(fixedParts.Value))
                        gate = known.Gate.Covers(gate) ? known.Gate : known.Gate.Union(gate);
                }
                // Source-compatibility fallback only: these are conservative local exclusion dimensions,
                // not a claim about the actual gate. A missing shape must never close the entire city.
                else gate = new GateBox(new Bounds(door.transform.position, new Vector3(40, 20, 40)), door.name);
                doorBoxes[door] = (fixedParts, gate);
                gates.Add(gate);
            }
        }
        // Capture first, then index only a snapshot that will be published. An unchanged refresh needs its
        // door records, but must not rebuild and immediately discard all of the retained grid's buckets.
        private void BuildIndex()
        {
            if (gates.Count <= LinearGateLimit) return;
            foreach (var gate in gates)
            {
                if (!CellRange(gate.Min.X, gate.Max.X, gate.Min.Z, gate.Max.Z, MaximumGateCells,
                    out int minX, out int maxX, out int minZ, out int maxZ))
                { unindexedGates.Add(gate); continue; }
                for (int x = minX; x <= maxX; x++)
                    for (int z = minZ; z <= maxZ; z++)
                    {
                        if (!gateCells.TryGetValue((x, z), out var bucket))
                        { bucket = new List<GateBox>(); gateCells.Add((x, z), bucket); }
                        bucket.Add(gate);
                    }
            }
        }
        private static bool CellRange(double minX, double maxX, double minZ, double maxZ, int limit,
            out int firstX, out int lastX, out int firstZ, out int lastZ)
        {
            firstX = lastX = firstZ = lastZ = 0;
            double x0 = Math.Floor(minX / GateCellMetres), x1 = Math.Floor(maxX / GateCellMetres);
            double z0 = Math.Floor(minZ / GateCellMetres), z1 = Math.Floor(maxZ / GateCellMetres);
            if (!Position.Finite(x0) || !Position.Finite(x1) || !Position.Finite(z0) || !Position.Finite(z1)
                || x0 < int.MinValue || x1 >= int.MaxValue || z0 < int.MinValue || z1 >= int.MaxValue
                || x1 < x0 || z1 < z0) return false;
            double width = x1 - x0 + 1, depth = z1 - z0 + 1;
            if (width > limit || depth > limit || width * depth > limit) return false;
            firstX = (int)x0; lastX = (int)x1; firstZ = (int)z0; lastZ = (int)z1;
            return true;
        }
        // Unsorted: the gate comparison does not depend on order.
        private static FenceDoor[] Doors() => Object.FindObjectsByType<FenceDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        /// <summary>Searches the scene for doors again. Returns null when that gives exactly these gates (in any order),
        /// otherwise the new snapshot, with the first gate that differs named in <paramref name="change"/>.</summary>
        public PublicRoadAccess? Refreshed(out string change)
            => Refreshed(Doors(), out change);
        public PublicRoadAccess? Refreshed(FenceDoor[] doors, out string change)
        {
            var next = new PublicRoadAccess(doors, this);
            var remaining = new List<GateBox>(gates);
            foreach (var gate in next.gates)
            {
                int match = remaining.FindIndex(old => old.Same(gate));
                if (match < 0) { change = "gate '" + gate.Door + "' is new or moved"; next.BuildIndex(); return next; }
                remaining.RemoveAt(match);
            }
            change = remaining.Count == 0 ? "" : "gate '" + remaining[0].Door + "' is gone or moved";
            if (change.Length > 0) { next.BuildIndex(); return next; }
            // Same gates, but a door may have been replaced by an identical one: keep the new per-door record. The
            // multiset matched, so every gate in that record is one of these gates.
            doorBoxes = next.doorBoxes;
            return null;
        }
        private static bool Leaf(FenceDoor door, Transform part)
        {
            if (door.doors == null) return false;
            foreach (var leaf in door.doors) if (leaf != null && part.IsChildOf(leaf)) return true;
            return false;
        }
        private static Bounds? Combine(IEnumerable<Collider> colliders)
        {
            Bounds? combined = null;
            foreach (var collider in colliders)
            {
                Bounds world = collider is BoxCollider box ? BoxBounds(box) : collider.bounds;
                if (!Point(world.min).IsFinite || !Point(world.max).IsFinite || world.size.sqrMagnitude < 0.0001f) continue;
                if (combined is Bounds have) { have.Encapsulate(world); combined = have; }
                else combined = world;
            }
            return combined;
        }
        public bool NearGate(Position point, double metres)
        {
            if (!point.IsFinite) return true;
            foreach (var gate in gates)
            {
                double x = Math.Max(gate.Min.X - point.X, Math.Max(0, point.X - gate.Max.X));
                double y = Math.Max(gate.Min.Y - point.Y, Math.Max(0, point.Y - gate.Max.Y));
                double z = Math.Max(gate.Min.Z - point.Z, Math.Max(0, point.Z - gate.Max.Z));
                if (x * x + y * y + z * z <= metres * metres) return true;
            }
            return false;
        }
        public bool ClearSegment(Position a, Position b)
        {
            if (!a.IsFinite || !b.IsFinite) return false;
            if (gates.Count > LinearGateLimit) return ClearIndexedSegment(a, b);
            foreach (var gate in gates)
                if (TransportAccessGeometry.IntersectsBounds(a, b, gate.Min, gate.Max)) return false;
            return true;
        }
        private bool ClearIndexedSegment(Position a, Position b)
        {
            // Boxes and queries include both boundary cells, including at negative coordinates. Only the broad
            // phase changes: every candidate still uses the same three-dimensional, boundary-inclusive test.
            // Short lists and long segments cost less to scan; never walk a city-sized query rectangle.
            if (!CellRange(Math.Min(a.X, b.X), Math.Max(a.X, b.X), Math.Min(a.Z, b.Z), Math.Max(a.Z, b.Z),
                Math.Min(MaximumGateCells, gates.Count), out int minX, out int maxX, out int minZ, out int maxZ))
                return ClearCandidates(a, b, gates);
            if (!ClearCandidates(a, b, unindexedGates)) return false;
            int remaining = gates.Count - unindexedGates.Count;
            for (int x = minX; x <= maxX; x++)
                for (int z = minZ; z <= maxZ; z++)
                    if (gateCells.TryGetValue((x, z), out var bucket))
                    {
                        // Overlapping boxes can occur in several queried cells. Bound repeated work by falling
                        // back before candidate tests reach a full scan, rather than scanning the same gates 64 times.
                        if (bucket.Count >= remaining) return ClearCandidates(a, b, gates);
                        remaining -= bucket.Count;
                        if (!ClearCandidates(a, b, bucket)) return false;
                    }
            return true;
        }
        private static bool ClearCandidates(Position a, Position b, List<GateBox> candidates)
        {
            foreach (var gate in candidates)
            {
                // Pure slab intersection avoids millions of Unity Bounds native calls during graph searches.
                if (TransportAccessGeometry.IntersectsBounds(a, b, gate.Min, gate.Max)) return false;
            }
            return true;
        }
        private static Bounds BoxBounds(BoxCollider box)
        {
            var half = box.size / 2;
            var result = new Bounds(box.transform.TransformPoint(box.center + half), Vector3.zero);
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                        result.Encapsulate(box.transform.TransformPoint(box.center + new Vector3(half.x * x, half.y * y, half.z * z)));
            return result;
        }
        private static Position Point(Vector3 p) => new Position(p.x, p.y, p.z);
    }

    /// <summary>Read-only native lane adapter. Curb points are abstract parking, never claimed occupied real spaces.</summary>
    internal sealed class RoadRoutes
    {
        private const double ConnectorMetres = 8;
        private readonly List<TransportRoadNode> nodes = new List<TransportRoadNode>();
        private readonly Dictionary<(int X, int Z), List<int>> cells = new Dictionary<(int, int), List<int>>();
        private readonly Waypoint[] waypoints;
        private readonly TransportRoadGraph graph;
        private readonly CurrentSceneData? scene;
        private PublicRoadAccess access;
        private bool unsafeTopology;
        public string InvalidationReason { get; private set; } = "";
        public int InvalidationIndex { get; private set; } = -1;
        private bool Invalid(string reason, int index = -1)
        {
            unsafeTopology = true;
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
        private bool closureBatch;
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
            trafficBuilder?.BeginQuery();
            closureBatch = true;
        }
        public void EndTrafficClosureBatch()
        {
            closureBatch = false;
            trafficBuilder?.BeginQuery();
        }
        /// <summary>Checks only this captured section at a kernel boundary; no city-wide scan per traffic event.</summary>
        public bool IsTrafficLinkClosed(int linkId)
        {
            var network = TrafficNetwork;
            if (!Ready || !IsCurrent || network == null || linkId < 0 || linkId >= network.Links.Count) return true;
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
                    { closureDisabled[index] = Disabled(index); closureCheckedAt[index] = closureGeneration; }
                    disabled = closureDisabled[index];
                }
                if (disabled) return true;
                if (i > 0 && !closureGateClear[linkId]
                    && !access.ClearSegment(nodes[indices[i - 1]].Position, nodes[index].Position)) return true;
            }
            // Access and captured positions are immutable. Only successful gate checks survive a batch;
            // every native node is still validated afresh on the next pass. Blocked checks retain order.
            closureGateClear[linkId] = true;
            return false;
        }
        private readonly SHA256 topologyHash = SHA256.Create();
        private readonly MemoryStream topologyBuffer = new MemoryStream(256);
        private readonly BinaryWriter topologyWriter;
        private readonly List<int[]?> agentContracts = new List<int[]?>();
        private readonly List<VehicleTypes[]?> carContracts = new List<VehicleTypes[]?>();
        private readonly List<Waypoint?> nativeContracts = new List<Waypoint?>();
        // The traffic capture treats a null neighbour list apart from an empty one, so the snapshot must too.
        private readonly List<bool> nullNeighbours = new List<bool>();
        private readonly int[] searchedAt;
        private readonly bool[] searchDisabled;
        private int searchGeneration;
        public string TopologyId { get; private set; } = "";
        public bool Ready => copiedNodes == waypoints.Length && TrafficNetwork != null;
        public bool Available => scene != null && waypoints.Length > 0;
        /// <summary>Captured without a road scene. Nothing marks such a snapshot stale, so the caller retries it.</summary>
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
        /// <summary>Starts a staged check of the whole snapshot against native data. Searches only check the nodes they
        /// touch; a node captured without car access is never touched, so only this sweep notices it changing.</summary>
        public void BeginSweep()
        {
            // A sweep still running carries on; restarting it could keep the last nodes from ever being checked.
            if (Ready && sweepNode < 0) { sweepNode = 0; sweepJunction = 0; }
        }
        /// <summary>Continues the sweep within the budget. A difference marks the snapshot stale, as a search would.</summary>
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
        // A structural rebuild can reuse the same live scene; discovery belongs to the planner lifetime.
        public RoadRoutes(CurrentSceneData? scene, PublicRoadAccess access, double walkingSpeed = 2, double clockRate = 1, NativeRoadSpeeds? speeds = null)
        {
            if (!Position.Finite(walkingSpeed) || walkingSpeed <= 0) throw new ArgumentOutOfRangeException(nameof(walkingSpeed));
            this.walkingSpeed = walkingSpeed;
            this.clockRate = clockRate;
            this.speeds = speeds ?? new NativeRoadSpeeds();
            this.access = access;
            this.scene = scene; hadScene = scene != null;
            waypoints = scene != null && scene.allWaypoints != null ? scene.allWaypoints : Array.Empty<Waypoint>();
            graph = new TransportRoadGraph(nodes, clockRate);
            topologyWriter = new BinaryWriter(topologyBuffer);
            searchedAt = new int[waypoints.Length];
            searchDisabled = new bool[waypoints.Length];
        }

        /// <summary>Incremental main-thread snapshot: at most 2048 nodes or two milliseconds per eligible request.
        /// The caller must finish preflight before advancing any citizen, preserving mode-choice determinism.</summary>
        public bool Prepare()
        {
            speeds.Refresh();
            var watch = Stopwatch.StartNew();
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
                    // The native AI reads allowedAgents. Conflicting populated contracts fail closed rather
                    // than granting a permission present in only the legacy enum list.
                    node.AllowsCar = w.allowedAgents != null && w.allowedAgents.Contains((int)VehicleTypes.Car)
                        && (w.allowedCars == null || w.allowedCars.Contains(VehicleTypes.Car));
                    node.Successors = w.neighbors == null ? Array.Empty<int>() : w.neighbors.ToArray();
                }
                nodes.Add(node);
                nativeContracts.Add(w);
                nullNeighbours.Add(w?.neighbors == null);
                agentContracts.Add(w?.allowedAgents?.ToArray());
                carContracts.Add(w?.allowedCars?.ToArray());
                HashNode(i, w, node);
                if (node.AllowsCar && node.Position.IsFinite && node.KilometresPerHour > 0)
                {
                    var key = Cell(node.Position);
                    if (!cells.TryGetValue(key, out var list)) { list = new List<int>(); cells.Add(key, list); }
                    list.Add(i);
                }
                if ((copiedNodes & 31) == 0 && watch.Elapsed.TotalMilliseconds >= 2) break;
            }
            if (copiedNodes != waypoints.Length) return false;
            if (TopologyId.Length == 0)
            {
                topologyHash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                TopologyId = BitConverter.ToString(topologyHash.Hash!).Replace("-", "").ToLowerInvariant();
                topologyHash.Dispose();
                topologyWriter.Dispose();
            }
            if (Ready) return IsCurrent;
            double remaining = 2 - watch.Elapsed.TotalMilliseconds;
            int remainingUnits = 2048 - (copiedNodes - startedAt);
            if (remaining <= 0 || remainingUnits <= 0) return false;
            if (trafficBuilder == null) trafficBuilder = new TrafficRoadNetwork(TopologyId, nodes, waypoints, scene, access, clockRate, SnapshotNodeCurrent);
            if (!trafficBuilder.Prepare(remaining, remainingUnits))
            { if (trafficBuilder.Stale && !unsafeTopology) TrafficInvalid(); return false; }
            TrafficNetwork = trafficBuilder.Network;
            return Ready && IsCurrent;
        }
        private void HashNode(int index, Waypoint? native, TransportRoadNode node)
        {
            // Keep the existing byte format (including null versus empty contracts) while
            // reusing the buffer and the arrays already captured by this snapshot.
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

        public List<Curb> Near(Position position, double radius, int limit = 2)
        {
            trafficBuilder?.BeginQuery();
            // Bound spatial-cell arithmetic too; malformed saved coordinates cannot wrap an integer loop.
            if (!Ready || !position.IsFinite || Math.Abs(position.X) > 1000000 || Math.Abs(position.Z) > 1000000
                || !Position.Finite(radius) || radius < 0 || radius > 100 || limit < 1 || limit > 2) return new List<Curb>();
            var nearby = new List<int>();
            var cell = Cell(position); int reach = (int)Math.Ceiling(radius / 50);
            for (int x = cell.X - reach; x <= cell.X + reach; x++)
                for (int z = cell.Z - reach; z <= cell.Z + reach; z++)
                    if (cells.TryGetValue((x, z), out var indices))
                        foreach (int index in indices) if (!Disabled(index) && Position.Distance(position, nodes[index].Position) <= radius) nearby.Add(index);
            nearby.Sort((a, b) => { int order = Position.Distance(position, nodes[a].Position).CompareTo(Position.Distance(position, nodes[b].Position)); return order != 0 ? order : a.CompareTo(b); });
            var result = new List<Curb>();
            // Hard bound on NavMesh/physics queries, even in a dense junction.
            int attempts = 0;
            foreach (int index in nearby)
            {
                if (++attempts > 12 || result.Count >= limit) break;
                var road = nodes[index].Position;
                if (!NavMesh.SamplePosition(Vector(road), out NavMeshHit hit, (float)ConnectorMetres, WalkingRoutes.DryFilter)) continue;
                Position curb = Point(hit.position);
                bool duplicate = false;
                foreach (var old in result) if (old.Node == index || Position.Distance(old.Position, curb) < 5) { duplicate = true; break; }
                if (duplicate || !ConnectorClear(curb, road)) continue;
                result.Add(new Curb { Node = index, Position = curb });
            }
            return result;
        }

        public List<Curb> AtParked(Position parked)
        {
            var candidates = Near(parked, ConnectorMetres, 2);
            candidates.RemoveAll(c => !ConnectorClear(parked, nodes[c.Node].Position));
            foreach (var c in candidates) c.Position = parked;
            return candidates;
        }

        public Route? Plan(Curb from, Curb to, double departureMinute = 0,
            double maximumEstimatedMinutes = double.PositiveInfinity)
        {
            speeds.Refresh();
            if (!Ready || TrafficNetwork == null || !Position.Finite(departureMinute) || departureMinute < 0
                || from.Node == to.Node || !ConnectorClear(from.Position, nodes[from.Node].Position) || !ConnectorClear(to.Position, nodes[to.Node].Position)) return null;
            // This search is synchronous on Unity's main thread and its access callbacks are
            // read-only. Repeated visits to a node therefore share one native validation;
            // every new search and the final selected-path validation still read live data.
            if (searchGeneration == int.MaxValue) { Array.Clear(searchedAt, 0, searchedAt.Length); searchGeneration = 0; }
            searchGeneration++;
            trafficBuilder?.BeginQuery();
            int[]? path = null;
            var begin = Connector(from.Position, nodes[from.Node].Position);
            var end = Connector(nodes[to.Node].Position, to.Position);
            double roadCeiling = TransportComposition.RemainingSearchMinutes(maximumEstimatedMinutes, begin.Minutes + end.Minutes);
            var forecast = trafficForecast;
            var captured = new Dictionary<int, TrafficLinkForecast>();
            var entryDelays = new Dictionary<int, double>();
            var network = TrafficNetwork;
            var sectionPace = new Dictionary<int, double>();
            double entryMinute = departureMinute + begin.Minutes;
            double Traversal(int a, int b, double baseline)
            {
                if (!speeds.HasOverrides) return baseline;
                if (!network.TryGetEdge(a, b, out var edge)) return double.NaN;
                if (!sectionPace.TryGetValue(edge.LinkId, out double factor))
                {
                    // FIFO spacing uses a linear distance/time coordinate inside a section.
                    // Average the full section's effective travel time, then apply one pace to
                    // all its edges. Partial curb journeys receive the same proportional pace.
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
                    sectionPace.Add(edge.LinkId, factor);
                }
                return baseline * factor;
            }
            // One read per section for this query. A* has no arrival-time callback, so this is a departure-time
            // local forecast. Proportional edge shares avoid repeatedly charging an entire section at every waypoint.
            double Extra(int a, int b)
            {
                if (!network.TryGetEdge(a, b, out var edge)) return 1e15;
                if (forecast == null) return 0;
                double entryDelay = 0;
                if (a == from.Node && edge.StartLinkFraction > 0 && trafficEntryForecast != null)
                {
                    if (!entryDelays.TryGetValue(edge.LinkId, out entryDelay))
                    {
                        double endFraction = 1;
                        var sectionNodes = network.NativeNodes(edge.LinkId);
                        for (int i = 1; i < sectionNodes.Count; i++)
                            if (sectionNodes[i] == to.Node && network.TryGetEdge(sectionNodes[i - 1], to.Node, out var final)
                                && final.EndLinkFraction > edge.StartLinkFraction)
                            { endFraction = final.EndLinkFraction; break; }
                        entryDelay = trafficEntryForecast(edge.LinkId, entryMinute, edge.StartLinkFraction, endFraction - edge.StartLinkFraction);
                        entryDelays.Add(edge.LinkId, entryDelay);
                    }
                    if (!Position.Finite(entryDelay) || entryDelay < 0) return 1e15;
                }
                if (!captured.TryGetValue(edge.LinkId, out var value)) { value = forecast(edge.LinkId, entryMinute); captured.Add(edge.LinkId, value); }
                if (value.Closed || !Position.Finite(value.DelayMinutes)) return 1e15;
                return entryDelay + value.DelayMinutes * edge.FreeFlowMinutes / network.Links[edge.LinkId].FreeFlowMinutes;
            }
            var road = graph.PlanWithTraversal(from.Node, to.Node, SearchDisabled, access.ClearSegment, Traversal,
                pathFound: found => path = found, extraMinutes: Extra, maximumEstimatedMinutes: roadCeiling);
            if (road == null || path == null || !NativePathValid(path)) return null;
            // A fresh A* search and live validation check every node, directed edge and gate. No route winner
            // is cached: an off-path closure reopening must immediately make its shorter path competitive.
            // Background cars do not collide with
            // traffic or artificial bridge skip hiders; local curb access receives the physical barrier test.
            if (TopologyId.Length != 64 || road.Legs.Count != path.Length - 1) return null;
            for (int i = 0; i < road.Legs.Count; i++)
            {
                if (!network.TryGetEdge(path[i], path[i + 1], out var edge)
                    || captured.TryGetValue(edge.LinkId, out var value) && (value.Closed || !Position.Finite(value.DelayMinutes))
                    || i == 0 && entryDelays.TryGetValue(edge.LinkId, out var entryDelay) && (!Position.Finite(entryDelay) || entryDelay < 0)) return null;
                road.Legs[i].ServiceId = "native:road:v1:" + TopologyId + ":" + path[i].ToString(CultureInfo.InvariantCulture)
                    + ":" + path[i + 1].ToString(CultureInfo.InvariantCulture);
            }
            return TransportComposition.Join(begin, road, end);
        }

        private bool ConnectorClear(Position curb, Position road) => curb.IsFinite && road.IsFinite
            && Position.Distance(curb, road) <= ConnectorMetres && Math.Abs(curb.Y - road.Y) <= 1.5
            && !access.NearGate(curb, 3) && access.ClearSegment(curb, road) && PhysicalClear(curb, road);
        private bool PhysicalClear(Position a, Position b)
        {
            Vector3 start = Vector(a) + Vector3.up * 0.8f, end = Vector(b) + Vector3.up * 0.8f;
            return obstacleMask != 0 && !Physics.Linecast(start, end, obstacleMask, QueryTriggerInteraction.Ignore);
        }
        private Route Connector(Position from, Position to)
        {
            var route = new Route();
            double length = Position.Distance(from, to);
            if (length > 0.000001) route.Legs.Add(new RouteLeg { From = from, To = to, Mode = TravelMode.Car, Minutes = length / walkingSpeed });
            return route;
        }
        private bool NativePathValid(int[] path)
        {
            for (int i = 0; i < path.Length; i++)
            {
                int index = path[i];
                if (Disabled(index)) return false;
                if (i == 0) continue;
                int previous = path[i - 1];
                var native = waypoints[previous];
                if (native.neighbors == null || !native.neighbors.Contains(index)) return Invalid("path-neighbours", previous);
                if (!access.ClearSegment(nodes[previous].Position, nodes[index].Position)) return false;
            }
            return true;
        }
        private bool Disabled(int index)
        {
            if (index < 0 || index >= waypoints.Length || waypoints[index] == null) return true;
            var native = waypoints[index];
            if (!SnapshotNodeCurrent(index)) return true;
            if (trafficBuilder != null && !trafficBuilder.NodeCurrent(index)) { TrafficInvalid(); return true; }
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
            catch (InvalidOperationException)
            {
                // Record the cause without changing the existing exception contract on subsequent reads.
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
            // Indexed lists avoid boxing an enumerator for every checked node/contract.
            var comparer = EqualityComparer<T>.Default;
            for (int i = 0; i < captured.Length; i++)
                if (!comparer.Equals(current[i], captured[i])) return false;
            return true;
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
        public bool TryPhysicalRoadSample(CitizenState citizen, PhysicalJourneySample sample, out PhysicalRoadSample roadSample)
        {
            roadSample = default;
            var legs = citizen.Journey?.Legs;
            if (legs == null || citizen.Car == null || sample.Mode != TravelMode.Car || sample.Stationary
                || sample.LegIndex < 0 || sample.LegIndex >= legs.Count || !sample.Position.IsFinite) return false;
            var current = legs[sample.LegIndex];
            if (current.ServiceId != sample.ServiceId || Position.Distance(current.From, sample.From) > 0.001
                || Position.Distance(current.To, sample.To) > 0.001 || !TryTag(current, out int from, out int to)) return false;
            // Retimed fragments remain logical travel and bound future driving targets, but their
            // verified history must retain junction entry/exit evidence and rear-clearance distances.
            // Later full edges may receive a body subject to ordinary junction clearance, not immediately.
            // Missing intersection metadata on intermediate nodes is not evidence of being outside a junction.
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
            var associated = typeof(Waypoint).GetField("associatedIntersection", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
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
                // A fragment beginning inside an edge cannot manufacture that edge's source boundary.
                // Successive fragments of one edge contribute their actual geometry only once.
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
                var native = ResolveWaypoint(TopologyId, index);
                if (native == null) return false;
                bool repeatedEndpoint = index == lastBoundaryNode;
                lastBoundaryNode = index;
                if (!native.enter && !native.exit) return true;
                var junction = associated.GetValue(native);
                if (junction == null || native.enter == native.exit) return false;
                // Endpoint tolerance can observe the same node on adjacent tiny fragments. This is
                // one passage until another native node intervenes, not a second entry/exit. Retain
                // the earliest entry and latest exit so uncertainty can only delay body admission.
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
            var parts = leg.ServiceId.Split(':');
            if (parts.Length != 6 || parts[0] != "native" || parts[1] != "road" || parts[2] != "fragment1" || parts[3] != TopologyId
                || !int.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out from)
                || !int.TryParse(parts[5], NumberStyles.None, CultureInfo.InvariantCulture, out to) || from == to) return false;
            var a = ResolveWaypoint(parts[3], from); var b = ResolveWaypoint(parts[3], to);
            if (a == null || b == null || a.neighbors == null || !a.neighbors.Contains(to) || !access.ClearSegment(leg.From, leg.To)) return false;
            Position start = Point(a.position), end = Point(b.position);
            double x = end.X - start.X, y = end.Y - start.Y, z = end.Z - start.Z;
            double square = x * x + y * y + z * z;
            if (!Position.Finite(square) || square <= .000000000001) return false;
            double Project(Position point) => ((point.X - start.X) * x + (point.Y - start.Y) * y + (point.Z - start.Z) * z) / square;
            double first = Project(leg.From), last = Project(leg.To);
            // Historical fragments may collapse to one representable point during retiming. They still
            // cannot claim another edge, reverse direction or turn malformed metadata into a boundary.
            return Position.Finite(first) && Position.Finite(last) && last >= first
                && Position.Distance(leg.From, Position.Lerp(start, end, Math.Max(0, Math.Min(1, first)))) <= .001
                && Position.Distance(leg.To, Position.Lerp(start, end, Math.Max(0, Math.Min(1, last)))) <= .001;
        }
        private bool TryTag(RouteLeg leg, out int from, out int to)
        {
            from = to = -1;
            if (leg.Mode != TravelMode.Car || string.IsNullOrEmpty(leg.ServiceId)) return false;
            var parts = leg.ServiceId.Split(':');
            if (parts.Length != 6 || parts[0] != "native" || parts[1] != "road" || parts[2] != "v1"
                || parts[3] != TopologyId || !int.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out from)
                || !int.TryParse(parts[5], NumberStyles.None, CultureInfo.InvariantCulture, out to)) return false;
            var a = ResolveWaypoint(parts[3], from); var b = ResolveWaypoint(parts[3], to);
            return a != null && b != null && a.neighbors != null && a.neighbors.Contains(to)
                && Position.Distance(Point(a.position), leg.From) <= 0.001 && Position.Distance(Point(b.position), leg.To) <= 0.001
                && access.ClearSegment(leg.From, leg.To);
        }
        private static (int X, int Z) Cell(Position point) => ((int)Math.Floor(point.X / 50), (int)Math.Floor(point.Z / 50));
        private static Position Point(Vector3 v) => new Position(v.x, v.y, v.z);
        private static Vector3 Vector(Position v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
    }
}
