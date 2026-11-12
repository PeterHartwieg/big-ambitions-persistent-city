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
        { string key = KeyOf(place); queues.Remove(key); retryAt.Remove(key); }
        private const float DoorMatchMetres = 2.5f, WallOffset = 0.6f, FirstGap = 1.1f, Spacing = 0.75f;
        private const float SampleRadius = 0.6f, LateralTolerance = 0.45f, OtherDoorClearance = 1.2f, RetrySeconds = 60f;
        private readonly Dictionary<string, DoorQueue> queues = new Dictionary<string, DoorQueue>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> retryAt = new Dictionary<string, float>(StringComparer.Ordinal);

        public static string KeyOf(Place place) => VendorNames.IsVendorPlace(place.Id) ? place.Id : DwellPlanner.DoorKey(place.Position);

        public bool TryQueue(Place place, ref bool sampledThisCall, out DoorQueue? queue)
        {
            string key = KeyOf(place);
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
            string key = KeyOf(place);
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

        private static DoorQueue? Sample(string key, Place place)
        {
            var manager = CityManager.Instance;
            if (manager == null || manager.cityBuildingControllers == null || !place.Position.IsFinite) return null;
            Vector3 entrance = PhysicalPedestrianBody.Vector(place.Position);
            Transform? door = null; float best = DoorMatchMetres;
            var otherDoors = new List<Vector3>();
            foreach (var controller in manager.cityBuildingControllers)
            {
                if (controller == null || controller.entranceDoors == null) continue;
                foreach (var candidate in controller.entranceDoors)
                {
                    if (candidate == null || candidate.doorTransform == null) continue;
                    Vector3 front = candidate.doorTransform.position + candidate.doorTransform.forward * 0.5f;
                    float distance = Vector3.Distance(front, entrance);
                    if (distance < 15f) otherDoors.Add(front);
                    if (distance < best) { best = distance; door = candidate.doorTransform; }
                }
            }
            if (door == null) return null;
            Vector3 ownFront = door.position + door.forward * 0.5f;
            otherDoors.RemoveAll(front => Vector3.Distance(front, ownFront) < 0.05f);
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
                // The point must be walkable, near where it was asked for (not snapped out into the
                // street or onto another level), clear of other doors, and reachable in a straight
                // line from the spot before it. The door's own carve is not tested.
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
