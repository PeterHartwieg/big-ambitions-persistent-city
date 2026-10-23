using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GleyTrafficSystem;
using GleyUrbanAssets;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    /// <summary>Staged main-thread native import, pure background construction, staged validation before publication.</summary>
    internal sealed class TrafficRoadNetwork
    {
        private static readonly FieldInfo? Associated = typeof(Waypoint).GetField("associatedIntersection", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly string topology;
        private readonly IReadOnlyList<TransportRoadNode> roads;
        private readonly Waypoint[] waypoints;
        private readonly CurrentSceneData? scene;
        private readonly bool hadScene;
        private readonly PriorityIntersection[] priorities;
        private readonly TrafficLightsIntersection[] lights;
        private readonly PublicRoadAccess access;
        private readonly double clockRate;
        private readonly Func<int, bool> baseCurrent;
        private readonly Dictionary<GenericIntersection, int> junctionIds = new Dictionary<GenericIntersection, int>();
        private readonly List<Junction> junctions = new List<Junction>();
        private readonly Dictionary<int, TrafficSignalSchedule> signals = new Dictionary<int, TrafficSignalSchedule>();
        private readonly NativeNode?[] nativeNodes;
        private TrafficNetworkNode[] snapshot;
        private readonly int[]? roadIds;
        private readonly Dictionary<int, bool> junctionQueryResults = new Dictionary<int, bool>();
        private Task<TrafficNetwork>? construction;
        private TrafficNetwork? constructed;
        private int junctionCursor, nodeCursor, validationCursor;
        private Stage stage;
        public bool Stale { get; private set; }
        public string InvalidationReason { get; private set; } = "";
        public int InvalidationIndex { get; private set; } = -1;
        private bool Invalid(string reason, int index = -1)
        {
            if (InvalidationReason.Length == 0) { InvalidationReason = reason; InvalidationIndex = index; }
            return false;
        }
        public TrafficNetwork? Network { get; private set; }
        private enum Stage { Junctions, Nodes, Constructing, ValidateJunctions, ValidateNodes, Complete }
        private sealed class NativeNode
        {
            public Waypoint Native = null!;
            public object? Association;
            public int JunctionId = -1;
            public int Flags;
            public int[]? Lanes;
            public int RoadId;
        }
        private sealed class Junction
        {
            public GenericIntersection Native = null!;
            public string Name = "";
            public string Contract = "";
            public int[]? Exits;
            public int[]?[] Groups = Array.Empty<int[]?>();
            public float Green, Yellow;
        }

        public TrafficRoadNetwork(string topology, IReadOnlyList<TransportRoadNode> roads, Waypoint[] waypoints,
            CurrentSceneData? scene, PublicRoadAccess access, double clockRate, Func<int, bool> baseCurrent)
        {
            if (Associated == null || Associated.FieldType != typeof(IIntersection))
                throw new MissingFieldException("Native traffic intersection association contract.");
            this.topology = topology; this.roads = roads; this.waypoints = waypoints; this.scene = scene;
            this.access = access; this.clockRate = clockRate; this.baseCurrent = baseCurrent;
            hadScene = scene != null;
            priorities = scene?.allPriorityIntersections ?? Array.Empty<PriorityIntersection>();
            lights = scene?.allLightsIntersections ?? Array.Empty<TrafficLightsIntersection>();
            nativeNodes = new NativeNode?[waypoints.Length]; snapshot = new TrafficNetworkNode[waypoints.Length];
            // Capture each road ID in its node slice; this array stays on the main thread.
            roadIds = scene?.roadIds != null && scene.roadIds.Length == waypoints.Length ? scene.roadIds : null;
        }

        public bool ReferencesCurrent
        {
            get
            {
                if (Stale) return false;
                if (hadScene && scene == null) return Invalid("scene-destroyed");
                if (scene == null) return true;
                if (scene.allWaypoints == null ? waypoints.Length != 0 : !ReferenceEquals(scene.allWaypoints, waypoints))
                    return Invalid("waypoint-array");
                if (!ReferenceEquals(scene.allPriorityIntersections ?? Array.Empty<PriorityIntersection>(), priorities))
                    return Invalid("priority-junction-array");
                if (!ReferenceEquals(scene.allLightsIntersections ?? Array.Empty<TrafficLightsIntersection>(), lights))
                    return Invalid("signal-junction-array");
                return true;
            }
        }

        public bool Prepare(double milliseconds = 2, int maximumUnits = 2048)
        {
            if (!ReferencesCurrent) { Stale = true; return false; }
            var watch = Stopwatch.StartNew(); int units = 0;
            while (units < maximumUnits && watch.Elapsed.TotalMilliseconds < milliseconds)
            {
                switch (stage)
                {
                    case Stage.Junctions:
                        if (junctionCursor < priorities.Length + lights.Length)
                        { CaptureJunction(junctionCursor++); units++; break; }
                        stage = Stage.Nodes; continue;
                    case Stage.Nodes:
                        if (nodeCursor < waypoints.Length) { CaptureNode(nodeCursor++); units++; break; }
                        // This worker closes over ONLY pure immutable DTOs, a string and a number.
                        // Unity/game references stay on this importer and are never read by the worker.
                        var pureNodes = snapshot; string nativeTopology = topology; double rate = clockRate;
                        construction = Task.Run(() => new TrafficNetwork(nativeTopology, pureNodes, rate));
                        stage = Stage.Constructing; return false;
                    case Stage.Constructing:
                        if (!construction!.IsCompleted) return false;
                        constructed = construction.GetAwaiter().GetResult();
                        // The pure constructor has finished; retain native shape validators, not a second
                        // city-sized set of DTOs/structural strings for the lifetime of this revision.
                        snapshot = Array.Empty<TrafficNetworkNode>();
                        stage = Stage.ValidateJunctions; validationCursor = 0; continue;
                    case Stage.ValidateJunctions:
                        if (validationCursor < junctions.Count)
                        {
                            if (!JunctionCurrent(validationCursor++)) { Stale = true; return false; }
                            units++; break;
                        }
                        stage = Stage.ValidateNodes; validationCursor = 0; BeginQuery(); continue;
                    case Stage.ValidateNodes:
                        if (validationCursor < waypoints.Length)
                        {
                            int index = validationCursor++;
                            if (!baseCurrent(index) || !NodeCurrent(index)) { Stale = true; return false; }
                            units++; break;
                        }
                        if (!ReferencesCurrent) { Stale = true; return false; }
                        Network = constructed; stage = Stage.Complete; return true;
                    default: return true;
                }
            }
            // A validation batch can span frames; never retain its junction results into a new frame.
            BeginQuery(); return stage == Stage.Complete;
        }

        public void BeginQuery() => junctionQueryResults.Clear();
        public int JunctionCount => junctions.Count;
        public bool JunctionCurrentAt(int id) => ReferencesCurrent && id >= 0 && id < junctions.Count && JunctionCurrent(id);
        public bool NodeCurrent(int index)
        {
            if (!ReferencesCurrent || index < 0 || index >= nativeNodes.Length) return false;
            var captured = nativeNodes[index]; var native = waypoints[index];
            if (captured == null) return native == null || Invalid("waypoint-identity", index);
            if (native == null || !ReferenceEquals(native, captured.Native)) return Invalid("waypoint-identity", index);
            if (Flags(native) != captured.Flags) return Invalid("waypoint-flags", index);
            object? association = Associated!.GetValue(native);
            if (!ReferenceEquals(association, captured.Association)) return Invalid("junction-association", index);
            // stop is live light/priority state, not topology. Keep the capture prerequisite live too:
            // a newly stopped, unassociated waypoint is still an unverifiable native contract.
            if (native.stop && association == null) return Invalid("unassociated-stop", index);
            if (!Same(native.otherLanes, captured.Lanes)) return Invalid("lane-links", index);
            var currentIds = scene?.roadIds != null && scene.roadIds.Length == waypoints.Length ? scene.roadIds : null;
            if ((currentIds == null ? -1 : currentIds[index]) != captured.RoadId) return Invalid("road-id", index);
            if (captured.JunctionId < 0) return true;
            if (!junctionQueryResults.TryGetValue(captured.JunctionId, out bool current))
            { current = JunctionCurrent(captured.JunctionId); junctionQueryResults.Add(captured.JunctionId, current); }
            return current;
        }

        private GenericIntersection NativeJunction(int id) => id < priorities.Length ? priorities[id] : lights[id - priorities.Length];
        private void CaptureJunction(int id)
        {
            var native = NativeJunction(id);
            if (native == null || junctionIds.ContainsKey(native)) throw new InvalidOperationException("Native junction list is ambiguous.");
            var groups = native.GetWaypoints();
            var captured = new Junction { Native = native, Name = native.name, Exits = native.exitWaypoints?.ToArray(),
                Groups = groups.Select(g => g?.roadWaypoints?.ToArray()).ToArray() };
            string contract = id.ToString(CultureInfo.InvariantCulture) + ":" + native.GetType().FullName + ":" + captured.Name + ":exits=" + Values(captured.Exits);
            foreach (var group in captured.Groups) contract += ";group=" + Values(group);
            if (native is TrafficLightsIntersection light)
            {
                captured.Green = light.greenLightTime; captured.Yellow = light.yellowLightTime;
                double green = captured.Green * clockRate, yellow = captured.Yellow * clockRate;
                double slot = green + yellow, cycle = captured.Groups.Length * slot;
                if (!Position.Finite(green) || green <= 0 || !Position.Finite(yellow) || yellow < 0 || !Position.Finite(cycle) || cycle <= 0)
                    throw new InvalidOperationException("Native light timing cannot define a periodic logical schedule.");
                contract += ";green=" + captured.Green.ToString("R", CultureInfo.InvariantCulture) + ";yellow=" + captured.Yellow.ToString("R", CultureInfo.InvariantCulture);
                for (int group = 0; group < captured.Groups.Length; group++)
                {
                    var indices = captured.Groups[group] ?? throw new InvalidOperationException("Native light group is absent.");
                    foreach (int index in indices)
                    {
                        if (index < 0 || index >= waypoints.Length || waypoints[index] == null
                            || !ReferenceEquals(Associated!.GetValue(waypoints[index]), native) || !waypoints[index].enter || signals.ContainsKey(index))
                            throw new InvalidOperationException("Native light group association is ambiguous.");
                        signals.Add(index, new TrafficSignalSchedule { CycleMinutes = cycle, GreenStartMinutes = group * slot,
                            GreenDurationMinutes = green, PhaseOffsetMinutes = 0 });
                    }
                }
            }
            captured.Contract = contract; junctionIds.Add(native, id); junctions.Add(captured);
        }
        private void CaptureNode(int index)
        {
            var native = waypoints[index]; var road = roads[index];
            if (native == null) { snapshot[index] = new TrafficNetworkNode { Id = index }; return; }
            object? association = Associated!.GetValue(native);
            if (native.stop && association == null) throw new InvalidOperationException("Native stop waypoint has no verified junction association.");
            int junctionId = association is GenericIntersection generic && junctionIds.TryGetValue(generic, out int knownId) ? knownId : -1;
            bool known = association == null || junctionId >= 0;
            bool valid = known && (!(native.enter || native.exit || native.giveWay) || association != null || native.neighbors?.Count == 0);
            int roadId = roadIds == null ? -1 : roadIds[index];
            nativeNodes[index] = new NativeNode { Native = native, Association = association, JunctionId = junctionId,
                Flags = Flags(native), Lanes = native.otherLanes?.ToArray(), RoadId = roadId };
            string contract = Values(native.allowedAgents) + "/" + Values(native.allowedCars?.Select(c => (int)c))
                + ";road=" + roadId.ToString(CultureInfo.InvariantCulture) + ";enter=" + native.enter + ";exit=" + native.exit
                + ";giveWay=" + native.giveWay + ";junction=" + (junctionId >= 0 ? junctions[junctionId].Contract : association == null ? "none" : "unknown")
                + ";lanes=" + Values(native.otherLanes);
            int[] successors = road.Successors.Where(next => next >= 0 && next < roads.Count && access.ClearSegment(road.Position, roads[next].Position)).ToArray();
            bool gate = access.NearGate(road.Position, 0);
            snapshot[index] = new TrafficNetworkNode { Id = index, Position = road.Position, KilometresPerHour = road.KilometresPerHour,
                AllowsCar = road.AllowsCar && native.listIndex == index && valid && !gate,
                Boundary = association != null || native.enter || native.exit || native.giveWay || native.otherLanes?.Count > 0
                    || successors.Length != road.Successors.Length || gate,
                StructuralContract = contract, Successors = successors, Signal = signals.TryGetValue(index, out var signal) ? signal : null };
        }
        private bool JunctionCurrent(int id)
        {
            var captured = junctions[id]; var native = captured.Native;
            if (native == null || !ReferenceEquals(NativeJunction(id), native)) return Invalid("junction-identity", id);
            if (native.name != captured.Name) return Invalid("junction-name", id);
            if (!Same(native.exitWaypoints, captured.Exits)) return Invalid("junction-exits", id);
            var groups = native.GetWaypoints();
            if (groups.Count != captured.Groups.Length) return Invalid("junction-groups", id);
            for (int group = 0; group < groups.Count; group++)
                if (!Same(groups[group]?.roadWaypoints, captured.Groups[group])) return Invalid("junction-groups", id);
            if (native is TrafficLightsIntersection lights && (lights.greenLightTime != captured.Green || lights.yellowLightTime != captured.Yellow))
                return Invalid("signal-timing", id);
            return true;
        }
        // Only immutable shape flags belong here. Native lights/priority queues change stop routinely.
        private static int Flags(Waypoint native) => (native.enter ? 1 : 0) | (native.exit ? 2 : 0) | (native.giveWay ? 4 : 0);
        private static bool Same(List<int>? current, int[]? captured)
        {
            if (current == null) return captured == null;
            if (captured == null || current.Count != captured.Length) return false;
            for (int i = 0; i < captured.Length; i++) if (current[i] != captured[i]) return false;
            return true;
        }
        private static string Values(IEnumerable<int>? values) => values == null ? "null"
            : string.Join(",", values.Select(v => v.ToString(CultureInfo.InvariantCulture)));
    }
}
