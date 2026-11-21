using System;
using System.Collections.Generic;
using Buildings;
using PersistentCity.Core;
using UnityEngine;
using UnityEngine.AI;

namespace PersistentCity.Runtime
{
    internal sealed class DoorQueue
    {
        public DoorQueue(string key, Position door, Position entrance, IReadOnlyList<Position> spots) { Key = key; Door = door; Entrance = entrance; Spots = spots; }
        public string Key { get; }
        public Position Door { get; }
        public Position Entrance { get; }
        public IReadOnlyList<Position> Spots { get; }
    }

    internal sealed class DwellSpots
    {
        public const int QueueLength = 8;
        private int sampledFrame = -1;
        public bool SampledThisFrame => sampledFrame == Time.frameCount;
        public bool TryQueue(Place place, out DoorQueue? queue)
        {
            bool sampled = SampledThisFrame;
            return TryQueue(place, ref sampled, out queue);
        }
        public void Invalidate(Place place)
        { string key = Key(place); queues.Remove(key); retryAt.Remove(key); }
        private const float DoorMatchMetres = 2.5f, WallOffset = 0.6f, FirstGap = 1.1f, Spacing = 0.75f;
        private const float SampleRadius = 0.6f, LateralTolerance = 0.45f, OtherDoorClearance = 1.2f, RetrySeconds = 60f;
        private readonly Dictionary<string, DoorQueue> queues = new Dictionary<string, DoorQueue>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> retryAt = new Dictionary<string, float>(StringComparer.Ordinal);

        public static string KeyOf(Place place) => VendorNames.IsVendorPlace(place.Id) ? place.Id : DwellPlanner.DoorKey(place.Position);

        private sealed class PlaceReference : IEqualityComparer<Place>
        {
            public bool Equals(Place? a, Place? b) => ReferenceEquals(a, b);
            public int GetHashCode(Place place) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(place);
        }
        private readonly Dictionary<Place, (string Id, Position At, string Key)> keys = new Dictionary<Place, (string, Position, string)>(new PlaceReference());
        public string Key(Place place)
        {
            var at = place.Position;
            if (keys.TryGetValue(place, out var known) && ReferenceEquals(known.Id, place.Id) && known.At.X.Equals(at.X) && known.At.Y.Equals(at.Y) && known.At.Z.Equals(at.Z))
                return known.Key;
            string key = KeyOf(place);
            keys[place] = (place.Id, at, key);
            return key;
        }

        public bool TryQueue(Place place, ref bool sampledThisCall, out DoorQueue? queue)
        {
            string key = Key(place);
            if (VendorNames.IsVendorPlace(place.Id)) { queue = null; return false; }
            if (queues.TryGetValue(key, out var cached)) { queue = cached; return true; }
            queue = null;
            if (sampledThisCall || SampledThisFrame || retryAt.TryGetValue(key, out float retry) && Time.unscaledTime < retry) return false;
            sampledThisCall = true; sampledFrame = Time.frameCount;
            queue = Sample(key, place);
            if (queue == null) { retryAt[key] = Time.unscaledTime + RetrySeconds; return false; }
            retryAt.Remove(key);
            queues[key] = queue;
            return true;
        }

        public bool Unsampled(Place place)
        {
            string key = Key(place);
            return !queues.ContainsKey(key) && !(retryAt.TryGetValue(key, out float retry) && Time.unscaledTime < retry);
        }

        private readonly Dictionary<string, (long Opening, Position Entrance, Position Origin, float Heading, string Kind)> vendorLayouts
            = new Dictionary<string, (long, Position, Position, float, string)>(StringComparer.Ordinal);

        public bool TryVendorQueue(VendorRow row, VendorVehicle vehicle, out DoorQueue? queue)
        {
            queue = null;
            var stand = row.Stand;
            if (stand == null || !stand.Published || stand.OpeningId != vehicle.OpeningId || !vehicle.OwnedByPersistentCity) return false;
            string key = row.PlaceId;
            var origin = new Position(vehicle.QueueX, vehicle.QueueY, vehicle.QueueZ);
            var layout = (vehicle.OpeningId, stand.Position, origin, vehicle.QueueHeading, vehicle.Kind);
            if (!vendorLayouts.TryGetValue(key, out var previous) || previous.Opening != vehicle.OpeningId
                || previous.Heading != vehicle.QueueHeading || previous.Kind != vehicle.Kind
                || !SamePoint(previous.Entrance, stand.Position) || !SamePoint(previous.Origin, origin))
            {
                vendorLayouts[key] = layout;
                queues.Remove(key); retryAt.Remove(key);
            }
            if (queues.TryGetValue(key, out var cached)) { queue = cached; return true; }
            if (SampledThisFrame || retryAt.TryGetValue(key, out float retry) && Time.unscaledTime < retry) return false;
            sampledFrame = Time.frameCount;
            queue = SampleVendor(key, stand.Position, vehicle);
            if (queue == null) { retryAt[key] = Time.unscaledTime + RetrySeconds; return false; }
            retryAt.Remove(key); queues[key] = queue;
            return true;
        }

        private static bool SamePoint(Position a, Position b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;

        private static DoorQueue? SampleVendor(string key, Position entrance, VendorVehicle vehicle)
        {
            var origin = new Position(vehicle.QueueX, vehicle.QueueY, vehicle.QueueZ);
            if (!entrance.IsFinite || !origin.IsFinite || !Position.Finite(vehicle.QueueHeading)) return null;
            double heading = vehicle.QueueHeading * Math.PI / 180.0;
            double spacing = vehicle.Kind == "truck" ? 1.0 : 0.8;
            var points = new List<Position>(QueueLength);
            Position previous = entrance;
            for (int i = 0; i < QueueLength; i++)
            {
                var wanted = new Position(origin.X + Math.Sin(heading) * spacing * i, origin.Y, origin.Z + Math.Cos(heading) * spacing * i);
                if (!NavMesh.SamplePosition(PhysicalPedestrianBody.Vector(wanted), out NavMeshHit hit, 0.3f, WalkingRoutes.DryFilter)) break;
                var point = PhysicalPedestrianBody.Point(hit.position);
                if (Math.Abs(point.Y - wanted.Y) > 0.5 || Math.Sqrt(Math.Pow(point.X - wanted.X, 2) + Math.Pow(point.Z - wanted.Z, 2)) > 0.15
                    || NavMesh.Raycast(PhysicalPedestrianBody.Vector(previous), hit.position, out _, WalkingRoutes.DryFilter)) break;
                points.Add(point); previous = point;
            }
            return points.Count == 0 ? null : new DoorQueue(key, entrance, entrance, points);
        }

        public long DoorSearches { get; private set; }
        public long IndexedSearches { get; private set; }
        public long IndexBuilds { get; private set; }
        public int IndexedDoors => index?.Count ?? 0;
        public double MaximumSearchMilliseconds { get; private set; }
        public double MaximumNavigationMilliseconds { get; private set; }
        public double MaximumIndexSliceMilliseconds { get; private set; }
        public void ResetDiagnostics() { DoorSearches = IndexedSearches = IndexBuilds = 0; MaximumSearchMilliseconds = MaximumNavigationMilliseconds = MaximumIndexSliceMilliseconds = 0; }
        internal bool IndexDisabled { get; set; }
        public void ForgetDoors() { index = null; building = null; }

        private const float IndexCell = 20f, OtherDoorMetres = 15f;
        private DoorIndex? index, building;
        private readonly List<int> nearby = new List<int>();
        private readonly List<Vector3> otherDoors = new List<Vector3>();

        private sealed class DoorIndex
        {
            public DoorIndex(object manager, object controllers, int controllerCount) { Manager = manager; Controllers = controllers; ControllerCount = controllerCount; }
            public object Manager { get; }
            public object Controllers { get; }
            public int ControllerCount { get; }
            public int Cursor;
            public readonly List<Transform> Doors = new List<Transform>();
            public readonly Dictionary<long, List<int>> Cells = new Dictionary<long, List<int>>();
            public int Count => Doors.Count;
            public void Add(Transform door, Vector3 front)
            {
                long cell = Key(Cell(front.x), Cell(front.z));
                if (!Cells.TryGetValue(cell, out var at)) Cells.Add(cell, at = new List<int>(4));
                at.Add(Doors.Count);
                Doors.Add(door);
            }
            public void Near(Vector3 point, List<int> into)
            {
                into.Clear();
                int x = Cell(point.x), z = Cell(point.z);
                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                        if (Cells.TryGetValue(Key(x + dx, z + dz), out var at)) into.AddRange(at);
                into.Sort();
            }
            private static int Cell(float value) => (int)Math.Floor(value / IndexCell);
            private static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;
        }

        private static int ControllerCount(object controllers) => controllers is System.Collections.ICollection known ? known.Count : -1;

        private static bool Matches(DoorIndex? candidate, CityManager manager)
        {
            var controllers = manager.cityBuildingControllers;
            return candidate != null && ReferenceEquals(candidate.Manager, manager) && ReferenceEquals(candidate.Controllers, controllers)
                && candidate.ControllerCount == ControllerCount(controllers);
        }

        private static void IndexController(DoorIndex into, CityBuildingController? controller)
        {
            if (controller == null || controller.entranceDoors == null) return;
            foreach (var candidate in controller.entranceDoors)
            {
                if (candidate == null || candidate.doorTransform == null) continue;
                into.Add(candidate.doorTransform, candidate.doorTransform.position + candidate.doorTransform.forward * 0.5f);
            }
        }

        public bool WarmIndex(long deadline)
        {
            if (IndexDisabled) return true;
            var manager = CityManager.Instance;
            if (manager == null || manager.cityBuildingControllers == null) return false;
            if (Matches(index, manager)) return true;
            if (!(manager.cityBuildingControllers is System.Collections.IList list)) return false;
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            if (!Matches(building, manager)) building = new DoorIndex(manager, list, list.Count);
            try
            {
                var growing = building!;
                while (growing.Cursor < list.Count)
                {
                    IndexController(growing, list[growing.Cursor++] as CityBuildingController);
                    if (System.Diagnostics.Stopwatch.GetTimestamp() >= deadline) return false;
                }
                index = growing; building = null; IndexBuilds++;
                return true;
            }
            catch (Exception) { building = null; return false; }
            finally { MaximumIndexSliceMilliseconds = Math.Max(MaximumIndexSliceMilliseconds, Since(started)); }
        }

        private static double Since(long started) => (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

        internal Transform? FindDoor(CityManager manager, Vector3 entrance)
        {
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            DoorSearches++;
            Transform? door = null; float best = DoorMatchMetres;
            otherDoors.Clear();
            void Consider(Transform candidate)
            {
                Vector3 front = candidate.position + candidate.forward * 0.5f;
                float distance = Vector3.Distance(front, entrance);
                if (distance < OtherDoorMetres) otherDoors.Add(front);
                if (distance < best) { best = distance; door = candidate; }
            }
            if (!IndexDisabled && !Matches(index, manager))
            {
                var controllers = manager.cityBuildingControllers;
                var scanned = new DoorIndex(manager, controllers, ControllerCount(controllers));
                foreach (var controller in controllers) IndexController(scanned, controller);
                index = scanned; building = null; IndexBuilds++;
            }
            if (!IndexDisabled && index != null)
            {
                IndexedSearches++;
                index.Near(entrance, nearby);
                foreach (int at in nearby)
                {
                    var candidate = index.Doors[at];
                    if (candidate != null) Consider(candidate);
                }
            }
            else
                foreach (var controller in manager.cityBuildingControllers)
                {
                    if (controller == null || controller.entranceDoors == null) continue;
                    foreach (var candidate in controller.entranceDoors)
                        if (candidate != null && candidate.doorTransform != null) Consider(candidate.doorTransform);
                }
            MaximumSearchMilliseconds = Math.Max(MaximumSearchMilliseconds, Since(started));
            return door;
        }

        private DoorQueue? Sample(string key, Place place)
        {
            var manager = CityManager.Instance;
            if (manager == null || manager.cityBuildingControllers == null || !place.Position.IsFinite) return null;
            Vector3 entrance = PhysicalPedestrianBody.Vector(place.Position);
            var door = FindDoor(manager, entrance);
            if (door == null) return null;
            long navigation = System.Diagnostics.Stopwatch.GetTimestamp();
            try { return Queue(key, place, entrance, door); }
            finally { MaximumNavigationMilliseconds = Math.Max(MaximumNavigationMilliseconds, Since(navigation)); }
        }

        private DoorQueue? Queue(string key, Place place, Vector3 entrance, Transform door)
        {
            Vector3 ownFront = door.position + door.forward * 0.5f;
            for (int i = otherDoors.Count - 1; i >= 0; i--) if (Vector3.Distance(otherDoors[i], ownFront) < 0.05f) otherDoors.RemoveAt(i);
            Vector3 outward = Vector3.ProjectOnPlane(door.forward, Vector3.up);
            if (outward.sqrMagnitude < 0.01f) return null;
            outward.Normalize();
            Vector3 along = Vector3.Cross(Vector3.up, outward);
            var right = Line(door.position, outward, along, otherDoors);
            var left = Line(door.position, outward, -along, otherDoors);
            foreach (var spots in left.Count > right.Count ? new[] { left, right } : new[] { right, left })
                if (spots.Count > 0 && Reachable(entrance, spots[0]))
                    return new DoorQueue(key, PhysicalPedestrianBody.Point(door.position), place.Position, spots);
            return null;
        }

        private static bool Reachable(Vector3 from, Position to)
        {
            var path = new NavMeshPath();
            return NavMesh.CalculatePath(from, PhysicalPedestrianBody.Vector(to), WalkingRoutes.DryFilter, path)
                && path.status == NavMeshPathStatus.PathComplete;
        }

        private static List<Position> Line(Vector3 door, Vector3 outward, Vector3 along, List<Vector3> otherDoors)
        {
            var spots = new List<Position>();
            Vector3? previous = null;
            for (int index = 0; index < QueueLength; index++)
            {
                Vector3 wanted = door + outward * WallOffset + along * (FirstGap + Spacing * index);
                if (!NavMesh.SamplePosition(wanted, out NavMeshHit hit, SampleRadius, WalkingRoutes.DryFilter)) break;
                Vector3 offset = hit.position - wanted;
                if (new Vector2(offset.x, offset.z).magnitude > LateralTolerance || Mathf.Abs(offset.y) > 0.5f) break;
                bool blocksDoor = false;
                foreach (var other in otherDoors) if (Vector3.Distance(other, hit.position) < OtherDoorClearance) { blocksDoor = true; break; }
                if (blocksDoor) break;
                if (previous.HasValue && NavMesh.Raycast(previous.Value, hit.position, out _, WalkingRoutes.DryFilter)) break;
                spots.Add(PhysicalPedestrianBody.Point(hit.position));
                previous = hit.position;
            }
            return spots;
        }
    }
}
