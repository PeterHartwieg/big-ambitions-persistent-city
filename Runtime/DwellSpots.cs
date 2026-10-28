using System;
using System.Collections.Generic;
using Buildings;
using PersistentCity.Core;
using UnityEngine;
using UnityEngine.AI;

namespace PersistentCity.Runtime
{
    /// <summary>A queue line beside one door: standing points along the wall, each facing the door.</summary>
    internal sealed class DoorQueue
    {
        public DoorQueue(string key, Position door, Position entrance, IReadOnlyList<Position> spots) { Key = key; Door = door; Entrance = entrance; Spots = spots; }
        /// <summary>One line per entrance. Places that share a door (two customer places at one entrance) share it.</summary>
        public string Key { get; }
        public Position Door { get; }
        /// <summary>The places' walkable entrance point: where a queuer comes out to the line and goes in at opening.</summary>
        public Position Entrance { get; }
        public IReadOnlyList<Position> Spots { get; }
    }

    /// <summary>
    /// Read-only, cached sampling of dwell spots. Queue spots run along the building's wall beside
    /// the door, close to the facade and out of the pavement's walking line, and stop short of any
    /// other door.
    /// </summary>
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
        // Native entrances are sampled 0.5 m in front of the door transform (GamePlaces.TryEntrance).
        private const float DoorMatchMetres = 2.5f, WallOffset = 0.6f, FirstGap = 1.1f, Spacing = 0.75f;
        private const float SampleRadius = 0.6f, LateralTolerance = 0.45f, OtherDoorClearance = 1.2f, RetrySeconds = 60f;
        private readonly Dictionary<string, DoorQueue> queues = new Dictionary<string, DoorQueue>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> retryAt = new Dictionary<string, float>(StringComparer.Ordinal);

        public static string KeyOf(Place place) => DwellPlanner.DoorKey(place.Position);

        /// <summary>
        /// At most one uncached door is sampled per frame across all callers. A door with no usable line is tried again
        /// after a minute, in case its navmesh was not ready.
        /// </summary>
        public bool TryQueue(Place place, ref bool sampledThisCall, out DoorQueue? queue)
        {
            string key = KeyOf(place);
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

        /// <summary>True when this door has not been sampled yet and is not waiting out a retry.</summary>
        public bool Unsampled(Place place)
        {
            string key = KeyOf(place);
            return !queues.ContainsKey(key) && !(retryAt.TryGetValue(key, out float retry) && Time.unscaledTime < retry);
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
            // Prefer the side with the longer clear line; ties go to the door's right.
            var right = Line(door.position, outward, along, otherDoors);
            var left = Line(door.position, outward, -along, otherDoors);
            // A queuer comes out of the entrance and walks to the line, so the line must be
            // reachable from it on the walking navmesh, round the door's own carve if need be.
            // If the longer side isn't, the other side is used.
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
