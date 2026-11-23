using System;
using System.Collections.Generic;
using System.Linq;
using Buildings;
using PersistentCity.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PersistentCity.Runtime
{
    internal sealed class PublicRoadAccess
    {
        private readonly List<GateBox> gates = new List<GateBox>();
        private const double GateCellMetres = 50;
        private const int MaximumGateCells = 64;
        private const int LinearGateLimit = 8;
        private readonly Dictionary<(int X, int Z), List<GateBox>> gateCells = new Dictionary<(int, int), List<GateBox>>();
        private readonly List<GateBox> unindexedGates = new List<GateBox>();
        private static readonly Vector3 GeometryMargin = new Vector3(1, 2, 1);
        private static readonly Vector3 FallbackSize = new Vector3(40, 20, 40);
        private static readonly Action<string> DefaultLog = message => Debug.LogWarning(message);
        private readonly Action<string> log;
        private string[] fallbackDoors = Array.Empty<string>();
        public int FallbackGateCount => fallbackDoors.Length;
        public IReadOnlyList<string> FallbackGateDoors => fallbackDoors;
        private enum GateSource { Colliders, Renderers, Footprint, Fallback }
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
        private Dictionary<FenceDoor, (GateSource Source, GateBox? Fixed, GateBox Gate)> doorBoxes
            = new Dictionary<FenceDoor, (GateSource, GateBox?, GateBox)>();
        public PublicRoadAccess() : this(Doors(), null, DefaultLog) { BuildIndex(); }
        public PublicRoadAccess(FenceDoor[] doors) : this(doors, null, DefaultLog) { BuildIndex(); }
        public PublicRoadAccess(FenceDoor[] doors, Action<string> log) : this(doors, null, log) { BuildIndex(); }
        private PublicRoadAccess(FenceDoor[] doors, PublicRoadAccess? previous, Action<string> log)
        {
            this.log = log;
            List<string>? fallback = null;
            foreach (var door in doors)
            {
                if (door == null || (!(door is HamptonsPrivateFenceDoor) && (!door.acceptsVehicles || door.IsForSale()))) continue;
                var colliders = door.GetComponentsInChildren<Collider>(true);
                GateBox? fixedParts = Combine(colliders.Where(collider => !Leaf(door, collider.transform))) is Bounds still
                    ? new GateBox(still, door.name) : (GateBox?)null;
                var source = GateSource.Colliders;
                Bounds? shape = Combine(colliders);
                if (!shape.HasValue)
                {
                    var renderers = door.GetComponentsInChildren<Renderer>(true);
                    if ((shape = Combine(renderers)).HasValue)
                    {
                        source = GateSource.Renderers;
                        fixedParts = Combine(renderers.Where(renderer => !Leaf(door, renderer.transform))) is Bounds stillRenderers
                            ? new GateBox(stillRenderers, door.name) : (GateBox?)null;
                    }
                    else if ((shape = FootprintBounds(door)).HasValue) source = GateSource.Footprint;
                }
                GateBox gate;
                if (shape is Bounds geometry)
                {
                    geometry.Expand(GeometryMargin); gate = new GateBox(geometry, door.name);
                    if (previous != null && fixedParts.HasValue && previous.doorBoxes.TryGetValue(door, out var known)
                        && known.Source == source && known.Fixed.HasValue && known.Fixed.Value.Same(fixedParts.Value))
                        gate = known.Gate.Covers(gate) ? known.Gate : known.Gate.Union(gate);
                }
                else
                {
                    source = GateSource.Fallback;
                    gate = new GateBox(new Bounds(door.transform.position, FallbackSize), door.name);
                    (fallback ??= new List<string>()).Add(door.name);
                }
                doorBoxes[door] = (source, fixedParts, gate);
                gates.Add(gate);
            }
            if (fallback != null) { fallback.Sort(StringComparer.Ordinal); fallbackDoors = fallback.ToArray(); }
            if (!SameDoors(fallbackDoors, previous?.fallbackDoors ?? Array.Empty<string>()))
                log("[Persistent City] Vehicle gates with no collider, renderer or building bounds: " + fallbackDoors.Length
                    + " using the " + FallbackSize.x + "x" + FallbackSize.y + "x" + FallbackSize.z + " m fallback"
                    + (fallbackDoors.Length == 0 ? "." : ": " + string.Join(", ", fallbackDoors)));
        }
        private static bool SameDoors(string[] left, string[] right)
        {
            if (left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++) if (!string.Equals(left[i], right[i], StringComparison.Ordinal)) return false;
            return true;
        }
        private static Bounds? FootprintBounds(FenceDoor door)
        {
            var building = door.GetComponentInParent<CityBuildingController>(true);
            return building == null ? null : BuildingFootprint.ReadBounds(building, usableOnly: true);
        }
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
        private static FenceDoor[] Doors() => Object.FindObjectsByType<FenceDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        public PublicRoadAccess? Refreshed(out string change)
            => Refreshed(Doors(), out change);
        public PublicRoadAccess? Refreshed(FenceDoor[] doors, out string change)
        {
            var next = new PublicRoadAccess(doors, this, log);
            var remaining = new List<GateBox>(gates);
            foreach (var gate in next.gates)
            {
                int match = remaining.FindIndex(old => old.Same(gate));
                if (match < 0) { change = "gate '" + gate.Door + "' is new or moved"; next.BuildIndex(); return next; }
                remaining.RemoveAt(match);
            }
            change = remaining.Count == 0 ? "" : "gate '" + remaining[0].Door + "' is gone or moved";
            if (change.Length > 0) { next.BuildIndex(); return next; }
            doorBoxes = next.doorBoxes;
            fallbackDoors = next.fallbackDoors;
            return null;
        }
        public bool SameGates(PublicRoadAccess other)
        {
            if (ReferenceEquals(this, other)) return true;
            if (other == null || other.gates.Count != gates.Count) return false;
            for (int i = 0; i < gates.Count; i++)
            {
                bool found = false;
                for (int j = 0; j < other.gates.Count && !found; j++) found = other.gates[j].Same(gates[i]);
                if (!found) return false;
            }
            for (int i = 0; i < other.gates.Count; i++)
            {
                bool found = false;
                for (int j = 0; j < gates.Count && !found; j++) found = gates[j].Same(other.gates[i]);
                if (!found) return false;
            }
            return true;
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
                combined = IncludeBounds(combined, collider is BoxCollider box ? BoxBounds(box) : collider.bounds);
            return combined;
        }
        private static Bounds? Combine(IEnumerable<Renderer> renderers)
        {
            Bounds? combined = null;
            foreach (var renderer in renderers) combined = IncludeBounds(combined, renderer.bounds);
            return combined;
        }
        private static Bounds? IncludeBounds(Bounds? combined, Bounds world)
        {
            if (!BuildingFootprint.Usable(world)) return combined;
            if (!(combined is Bounds have)) return world;
            have.Encapsulate(world);
            return have;
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
            if (!CellRange(Math.Min(a.X, b.X), Math.Max(a.X, b.X), Math.Min(a.Z, b.Z), Math.Max(a.Z, b.Z),
                Math.Min(MaximumGateCells, gates.Count), out int minX, out int maxX, out int minZ, out int maxZ))
                return ClearCandidates(a, b, gates);
            if (!ClearCandidates(a, b, unindexedGates)) return false;
            int remaining = gates.Count - unindexedGates.Count;
            for (int x = minX; x <= maxX; x++)
                for (int z = minZ; z <= maxZ; z++)
                    if (gateCells.TryGetValue((x, z), out var bucket))
                    {
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
}
