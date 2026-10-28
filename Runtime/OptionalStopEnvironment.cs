using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Entities;
using Buildings;
using BigAmbitions.DayNightCycle;
using PersistentCity.Core;
using PlayerActivity;
using UnityEngine;
using UnityEngine.AI;

namespace PersistentCity.Runtime
{
    /// <summary>Read-only spot discovery. No stationary crowd spawn, native sale or extra outing.</summary>
    internal sealed class OptionalStopEnvironment : IOptionalStopEnvironment
    {
        private readonly DwellSpots doors;
        private readonly AudienceGeometryCache audiences = new AudienceGeometryCache();
        private readonly Dictionary<string, CityBuildingController> buildings = new Dictionary<string, CityBuildingController>(StringComparer.Ordinal);
        private readonly Dictionary<string, OptionalStopSpot> validatedSpots = new Dictionary<string, OptionalStopSpot>(StringComparer.Ordinal);
        private readonly Dictionary<string, OptionalStopSpot[]> cached = new Dictionary<string, OptionalStopSpot[]>(StringComparer.Ordinal);
        private readonly Dictionary<string, StationaryAiSpawner> performerZones = new Dictionary<string, StationaryAiSpawner>(StringComparer.Ordinal);
        private readonly Dictionary<string, Transform> targets = new Dictionary<string, Transform>(StringComparer.Ordinal);
        private readonly Dictionary<string, Place> places;
        private static readonly FieldInfo? Performer = typeof(StationaryAiSpawner).GetField("_streetPerformer", BindingFlags.Instance | BindingFlags.NonPublic);
        public OptionalStopEnvironment(IEnumerable<Place> places, DwellSpots doors)
        { this.places = places.ToDictionary(p => p.Id, StringComparer.Ordinal); this.doors = doors; }
        public bool Raining => RainHelper.isRaining;
        public IReadOnlyList<OptionalStopSpot> Spots(Place place, DwellKind kind)
        {
            try
            {
                var result = ReadSpots(place, kind);
                foreach (var spot in result) validatedSpots[spot.Id] = spot;
                return result;
            }
            catch (Exception) { return Array.Empty<OptionalStopSpot>(); }
        }
        private IReadOnlyList<OptionalStopSpot> ReadSpots(Place place, DwellKind kind)
        {
            if (kind == DwellKind.Audience) return Audience(place);
            string key = place.Id + ":" + kind;
            if (kind == DwellKind.Resting)
                return cached.TryGetValue(key, out var park) ? park : ParkEdge(place, key);
            // Refuse cold work before a separate native-building lookup when this frame's
            // shared facade budget has already been consumed.
            if (!doors.TryQueue(place, out var line) || line == null || !NativeDoorPresent(place, kind))
                return Array.Empty<OptionalStopSpot>();
            // A dead building reference can invalidate the shared facade during that check.
            if (!doors.TryQueue(place, out line) || line == null) return Array.Empty<OptionalStopSpot>();
            if (cached.TryGetValue(key, out var known)) return known;
            // Geometry already validated beside the facade, reachable and clear of entrances.
            var result = line.Spots.Select((p, index) => new OptionalStopSpot { Id = key + ":" + index,
                TargetId = place.Id, Position = p, Facing = kind == DwellKind.WindowShopping ? line.Door
                    : new Position(2 * p.X - line.Door.X, p.Y, 2 * p.Z - line.Door.Z) }).ToArray();
            cached[key] = result; return result;
        }
        private bool NativeDoorPresent(Place place, DwellKind kind)
        {
            bool known = buildings.TryGetValue(place.Id, out var found);
            if (known && found == null)
            {
                buildings.Remove(place.Id); doors.Invalidate(place);
                foreach (var doorKind in new[] { DwellKind.Break, DwellKind.WindowShopping, DwellKind.ChatOutsideVenue })
                {
                    string key = place.Id + ":" + doorKind;
                    if (cached.TryGetValue(key, out var old)) foreach (var spot in old) validatedSpots.Remove(spot.Id);
                    cached.Remove(key);
                }
            }
            if (found == null)
            {
                var manager = CityManager.Instance;
                if (manager == null || manager.cityBuildingControllers == null) return false;
                foreach (var candidate in manager.cityBuildingControllers)
                    if (candidate != null && GamePlaces.TryEntrance(candidate, out var entrance)
                        && Position.Distance(entrance, place.Position) < 0.1)
                    { buildings[place.Id] = found = candidate; break; }
            }
            return found != null && found.gameObject.activeInHierarchy && GamePlaces.BuildingAccessible(found)
                && found.buildingRegistration != null && found.buildingRegistration.HasEstablishedBusiness
                && !found.buildingRegistration.temporarilyClosed
                && (kind != DwellKind.ChatOutsideVenue || found.buildingRegistration.businessTypeName == "ba:businesstype_nightclub");
        }
        private IReadOnlyList<OptionalStopSpot> ParkEdge(Place place, string key)
        {
            if (!place.Id.StartsWith("scene-park:", StringComparison.Ordinal)
                || !place.EvidenceSource.StartsWith("native-park-volume;", StringComparison.Ordinal)) return Array.Empty<OptionalStopSpot>();
            foreach (var trigger in UnityEngine.Object.FindObjectsOfType<LocationHappinessTrigger>(true))
            {
                if (trigger == null || !trigger.gameObject.activeInHierarchy
                    || !OutdoorPlaces.StaticSceneKey(trigger.transform, out string scene) || "scene-park:" + scene != place.Id) continue;
                var result = new List<OptionalStopSpot>();
                foreach (var collider in trigger.GetComponentsInChildren<Collider>(false))
                {
                    if (collider == null || !collider.enabled || !collider.isTrigger
                        || collider is MeshCollider mesh && !mesh.convex) continue;
                    var b = collider.bounds; var center = b.center; center.y = (float)place.Position.Y;
                    foreach (var outside in new[] { new Vector3(b.min.x - 2, center.y, center.z), new Vector3(b.max.x + 2, center.y, center.z),
                        new Vector3(center.x, center.y, b.min.z - 2), new Vector3(center.x, center.y, b.max.z + 2) })
                    {
                        var edge = collider.ClosestPoint(outside);
                        if (!Sample(place.Position, edge, out var point) || (collider.ClosestPoint(PhysicalPedestrianBody.Vector(point))
                            - PhysicalPedestrianBody.Vector(point)).sqrMagnitude > 0.25f) continue;
                        result.Add(new OptionalStopSpot { Id = key + ":" + result.Count, TargetId = place.Id, Position = point, Facing = place.Position });
                    }
                }
                if (result.Count == 0) return result;
                targets[place.Id] = trigger.transform; cached[key] = result.ToArray(); return cached[key];
            }
            return Array.Empty<OptionalStopSpot>();
        }
        private IReadOnlyList<OptionalStopSpot> Audience(Place place)
        {
            var image = new List<AudienceTarget>();
            if (Performer == null) return Array.Empty<OptionalStopSpot>();
            foreach (var zone in StationaryAiSpawner.AllPedestrianSpawners)
            {
                if (zone == null || !(Performer.GetValue(zone) is StreetPerformer performer) || performer == null
                    || !performer.gameObject.activeInHierarchy) continue;
                var center = performer.transform.position;
                // Only pedestrians passing this entrance; no destination/trip is generated for a show.
                if (Position.Distance(place.Position, PhysicalPedestrianBody.Point(center)) > 8
                    || !OutdoorPlaces.StaticSceneKey(zone.transform, out string key)) continue;
                string target = "performer:" + key;
                targets[target] = performer.transform; performerZones[target] = zone;
                image.Add(new AudienceTarget(zone, performer, target, PhysicalPedestrianBody.Point(center)));
            }
            return audiences.Get(place, image, SampleAudience);
        }
        private static IReadOnlyList<OptionalStopSpot> SampleAudience(Place place, IReadOnlyList<AudienceTarget> image)
        {
            var result = new List<OptionalStopSpot>();
            foreach (var member in image)
            {
                var center = PhysicalPedestrianBody.Vector(member.Position);
                string target = member.Id;
                for (int i = 0; i < 8; i++)
                {
                    double angle = i * Math.PI / 4;
                    var wanted = center + new Vector3((float)Math.Cos(angle) * 2, 0, (float)Math.Sin(angle) * 2);
                    if (!Sample(place.Position, wanted, out var point)
                        || !NavMesh.FindClosestEdge(PhysicalPedestrianBody.Vector(point), out var edge, WalkingRoutes.DryFilter)
                        || Vector3.Distance(edge.position, PhysicalPedestrianBody.Vector(point)) > 0.8f) continue;
                    result.Add(new OptionalStopSpot { Id = target + ":" + i, TargetId = target,
                        Position = point, Facing = PhysicalPedestrianBody.Point(center) });
                }
            }
            return result;
        }
        public bool Available(OptionalStopState stop)
        {
            try { return ReadAvailable(stop); }
            catch (Exception) { return false; }
        }
        private bool ReadAvailable(OptionalStopState stop)
        {
            if (!places.TryGetValue(stop.PlaceId, out var place)) return false;
            if (stop.Kind != DwellKind.Audience && stop.Kind != DwellKind.Resting && !NativeDoorPresent(place, stop.Kind)) return false;
            if (!validatedSpots.TryGetValue(stop.SpotId, out var geometry))
            {
                Spots(place, stop.Kind);
                if (!validatedSpots.TryGetValue(stop.SpotId, out geometry)) return false;
            }
            if (Position.Distance(stop.Position, geometry.Position) > 0.01) return false;
            // Rebind saved native identities once. A missing performer/volume ends the saved stop;
            // a later reappearance cannot restart it.
            if (stop.Kind == DwellKind.Audience && !targets.ContainsKey(stop.TargetId)) Audience(place);
            if (stop.Kind == DwellKind.Resting && !targets.ContainsKey(stop.TargetId)) ParkEdge(place, place.Id + ":" + stop.Kind);
            if ((stop.Kind == DwellKind.Audience || stop.Kind == DwellKind.Resting)
                && (!targets.TryGetValue(stop.TargetId, out var target) || target == null || !target.gameObject.activeInHierarchy)) return false;
            if (stop.Kind == DwellKind.Audience
                && (!performerZones.TryGetValue(stop.TargetId, out var zone) || zone == null
                    || !(Performer?.GetValue(zone) is StreetPerformer live) || live == null
                    || live.transform != targets[stop.TargetId]
                    || Position.Distance(stop.Facing, PhysicalPedestrianBody.Point(live.transform.position)) > 0.5)) return false;
            return NavMesh.SamplePosition(PhysicalPedestrianBody.Vector(stop.Position), out var hit, 0.15f, WalkingRoutes.DryFilter)
                && Position.Distance(PhysicalPedestrianBody.Point(hit.position), stop.Position) < 0.15;
        }
        private static bool Sample(Position from, Vector3 wanted, out Position point)
        {
            point = default;
            if (!NavMesh.SamplePosition(wanted, out var hit, 0.45f, WalkingRoutes.DryFilter)
                || Vector3.Distance(hit.position, wanted) > 0.45f) return false;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(PhysicalPedestrianBody.Vector(from), hit.position, WalkingRoutes.DryFilter, path)
                || path.status != NavMeshPathStatus.PathComplete) return false;
            point = PhysicalPedestrianBody.Point(hit.position); return true;
        }
    }
}
