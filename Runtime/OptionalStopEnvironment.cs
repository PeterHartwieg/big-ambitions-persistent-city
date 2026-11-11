using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Entities;
using Buildings;
using BigAmbitions.DayNightCycle;
using PersistentCity.Core;
using PlayerActivity;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace PersistentCity.Runtime
{
    /// <summary>Read-only spot discovery. No stationary crowd spawn, native sale or extra outing.
    /// F2h: answers do not depend on where the player stands. Frontages, park volumes and performer sites
    /// count once seen active (<see cref="StaticSceneMemory"/>); audience stops use the spawner's static
    /// performer area, not a live performer.</summary>
    internal sealed class OptionalStopEnvironment : IOptionalStopEnvironment
    {
        private readonly DwellSpots doors;
        private readonly AudienceGeometryCache audiences = new AudienceGeometryCache();
        private readonly Dictionary<string, CityBuildingController?> buildings = new Dictionary<string, CityBuildingController?>(StringComparer.Ordinal);
        private readonly Dictionary<string, OptionalStopSpot> validatedSpots = new Dictionary<string, OptionalStopSpot>(StringComparer.Ordinal);
        private readonly Dictionary<string, OptionalStopSpot[]> cached = new Dictionary<string, OptionalStopSpot[]>(StringComparer.Ordinal);
        private readonly Dictionary<string, Transform> targets = new Dictionary<string, Transform>(StringComparer.Ordinal);
        private readonly Dictionary<string, Place> places;
        private readonly StaticSceneMemory frontageActivity = new StaticSceneMemory();
        private readonly StaticSceneMemory parkActivity = new StaticSceneMemory();
        private readonly PerformerSites performers = new PerformerSites();
        // F2h cache size: validated frontages and park volumes, catalogued performer sites and places with a site list.
        public int ValidatedSceneObjects => frontageActivity.Count + parkActivity.Count;
        public int PerformerSiteCount => performers.Count;
        public int PerformerSitePlaces => performers.CachedPlaces;
        public OptionalStopEnvironment(IEnumerable<Place> places, DwellSpots doors)
        {
            this.places = places.ToDictionary(p => p.Id, StringComparer.Ordinal); this.doors = doors;
            PublishFrontages(this.places.Values);
        }
        public bool Raining => RainHelper.isRaining;
        // P2: whole-scene reads (frontage rebind, new-place frontage lookup, park and performer scans) run here, once a
        // frame before advancement, at most one per frame. The event path only reads their results; while one is due,
        // new spots are withheld and saved stops report Deferred, never Unavailable, so nothing is cancelled meanwhile.
        private bool rebindDue, parkScanDue = true;
        private readonly List<Place> unresolved = new List<Place>();
        public long MaintainScans { get; private set; }
        public void Maintain()
        {
            try
            {
                if (rebindDue || unresolved.Count > 0)
                {
                    MaintainScans++;
                    if (rebindDue) { rebindDue = false; PublishFrontages(places.Values, rebind: true); }
                    else PublishFrontages(unresolved);
                    unresolved.Clear();
                    return;
                }
                if (!SameScenes(parkTriggerScenes) || parkScanDue || parkTriggers == null)
                {
                    MaintainScans++; ParkTriggerScans++; parkScanDue = false;
                    parkTriggers = UnityEngine.Object.FindObjectsOfType<LocationHappinessTrigger>(true);
                    return;
                }
                if (!performersRead || !SameScenes(performerScenes)) { MaintainScans++; ScanPerformers(); }
            }
            catch (Exception) { }
        }
        private bool ScanDue(DwellKind kind) => kind == DwellKind.Audience ? !performersRead
            : kind == DwellKind.Resting ? parkScanDue || parkTriggers == null : rebindDue;
        // Issue #128 attribution: every Spots/Available read, both inside advancement units and from
        // RefreshOptionalStops before them. The per-advance delta isolates advancement reads.
        // Read and reset by the 60-second summary; timing adds no allocation.
        public long SeamCalls { get; private set; }
        public double SeamMilliseconds { get; private set; }
        public double MaximumSeamMilliseconds { get; private set; }
        public long ParkTriggerScans { get; private set; }
        public void ResetSeamDiagnostics() { SeamCalls = ParkTriggerScans = 0; SeamMilliseconds = MaximumSeamMilliseconds = 0; }
        // Available can read Spots; count the outer call only.
        private int seamDepth;
        private long BeginSeam() { seamDepth++; return System.Diagnostics.Stopwatch.GetTimestamp(); }
        private void RecordSeam(long started)
        {
            if (--seamDepth != 0) return;
            double elapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            SeamCalls++; SeamMilliseconds += elapsed;
            if (elapsed > MaximumSeamMilliseconds) MaximumSeamMilliseconds = elapsed;
        }
        public IReadOnlyList<OptionalStopSpot> Spots(Place place, DwellKind kind)
        {
            long started = BeginSeam();
            try
            {
                var result = ReadSpots(place, kind);
                foreach (var spot in result) validatedSpots[spot.Id] = spot;
                return result;
            }
            catch (Exception) { return Array.Empty<OptionalStopSpot>(); }
            finally { RecordSeam(started); }
        }
        private IReadOnlyList<OptionalStopSpot> ReadSpots(Place place, DwellKind kind)
        {
            if (kind == DwellKind.Audience) return Audience(place);
            string key = place.Id + ":" + kind;
            if (kind == DwellKind.Resting)
                return cached.TryGetValue(key, out var park) ? park : ParkEdge(place, key);
            // Ownership is already published. Check it before sampling so refused frontages
            // consume no facade budget. A rebind invalidates geometry before the single lookup.
            if (!NativeDoorPresent(place, kind) || !doors.TryQueue(place, out var line) || line == null)
                return Array.Empty<OptionalStopSpot>();
            if (cached.TryGetValue(key, out var known)) return known;
            // Geometry already validated beside the facade, reachable and clear of entrances.
            var result = line.Spots.Select((p, index) => new OptionalStopSpot { Id = key + ":" + index,
                TargetId = place.Id, Position = p, Facing = kind == DwellKind.WindowShopping ? line.Door
                    : new Position(2 * p.X - line.Door.X, p.Y, 2 * p.Z - line.Door.Z) }).ToArray();
            cached[key] = result; return result;
        }
        private bool NativeDoorPresent(Place place, DwellKind kind)
        {
            if (!CanHostFrontage(place)) return false;
            bool known = buildings.TryGetValue(place.Id, out var found);
            // A cached refusal is retained until publication. A destroyed previously owned
            // controller triggers a rebind; missing/ambiguous frontages are never polled each frame.
            if (rebindDue) return false;
            if (known && !ReferenceEquals(found, null) && found == null) { rebindDue = true; return false; }
            if (!known)
            {
                places[place.Id] = place;
                if (!unresolved.Contains(place)) unresolved.Add(place);
                return false;
            }
            // Access, business and closure are game reasons, read every time. Activity counts once (F2h).
            return found != null && GamePlaces.BuildingAccessible(found)
                && found.buildingRegistration != null && found.buildingRegistration.HasEstablishedBusiness
                && !found.buildingRegistration.temporarilyClosed
                && (kind != DwellKind.ChatOutsideVenue || found.buildingRegistration.businessTypeName == "ba:businesstype_nightclub")
                && frontageActivity.Admit(place.Id, found.gameObject.activeInHierarchy, false);
        }
        private void InvalidateFrontage(Place place, DwellKind kind)
        {
            string key = place.Id + ":" + kind;
            if (cached.TryGetValue(key, out var old)) foreach (var spot in old) validatedSpots.Remove(spot.Id);
            cached.Remove(key);
        }
        private readonly struct Frontage
        {
            public Frontage(CityBuildingController controller, string? address, Position entrance, bool hasEntrance)
            { Controller = controller; Address = address; Entrance = entrance; HasEntrance = hasEntrance; }
            public CityBuildingController Controller { get; }
            public string? Address { get; }
            public Position Entrance { get; }
            public bool HasEntrance { get; }
        }
        // Read-only bridge diagnostics; written only at publication/rebind, never per query.
        public long FrontageRebindCount { get; private set; }
        public double FrontageRebindLastMilliseconds { get; private set; }
        public double FrontageRebindTotalMilliseconds { get; private set; }
        public double FrontageRebindMaximumMilliseconds { get; private set; }
        public long FrontageRebindSpikes30 { get; private set; }
        private static bool CanHostFrontage(Place place) => place.Kind == PlaceKind.Work
            || place.Kind == PlaceKind.Shop || place.Kind == PlaceKind.Leisure;
        private sealed class FrontageImage
        {
            public readonly Dictionary<string, Frontage?> Addresses = new Dictionary<string, Frontage?>(StringComparer.Ordinal);
            public readonly Dictionary<(double X, double Y, double Z), List<Frontage>> Entrances
                = new Dictionary<(double, double, double), List<Frontage>>();
            public static (double X, double Y, double Z) Cell(Position at)
                => (Math.Floor(at.X / 0.1), Math.Floor(at.Y / 0.1), Math.Floor(at.Z / 0.1));
            public void Add(Frontage frontage)
            {
                if (frontage.Address != null)
                {
                    if (!Addresses.TryGetValue(frontage.Address, out var prior)) Addresses.Add(frontage.Address, frontage);
                    else if (!prior.HasValue || !ReferenceEquals(prior.Value.Controller, frontage.Controller)) Addresses[frontage.Address] = null;
                }
                if (!frontage.HasEntrance) return;
                var cell = Cell(frontage.Entrance);
                if (!Entrances.TryGetValue(cell, out var entries)) Entrances.Add(cell, entries = new List<Frontage>(1));
                entries.Add(frontage);
            }
        }
        private void PublishFrontages(IEnumerable<Place> venues, bool rebind = false)
        {
            long started = rebind ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            // Index every controller once. Resolve only frontage-capable roles against their
            // exact address and neighbouring entrance cells, never the entire city image.
            var image = ReadFrontages();
            foreach (var place in venues)
            {
                if (!CanHostFrontage(place)) continue;
                var owner = ResolveFrontage(place, image);
                if (buildings.TryGetValue(place.Id, out var previous) && !ReferenceEquals(previous, owner))
                {
                    doors.Invalidate(place);
                    frontageActivity.Forget(place.Id);
                    InvalidateFrontage(place, DwellKind.Break);
                    InvalidateFrontage(place, DwellKind.WindowShopping);
                    InvalidateFrontage(place, DwellKind.ChatOutsideVenue);
                }
                buildings[place.Id] = owner;
            }
            if (rebind)
            {
                double elapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                FrontageRebindCount++;
                FrontageRebindLastMilliseconds = elapsed;
                FrontageRebindTotalMilliseconds += elapsed;
                FrontageRebindMaximumMilliseconds = Math.Max(FrontageRebindMaximumMilliseconds, elapsed);
                if (elapsed >= 30) FrontageRebindSpikes30++;
            }
        }
        private static FrontageImage ReadFrontages()
        {
            var image = new FrontageImage();
            var manager = CityManager.Instance;
            if (manager == null || manager.cityBuildingControllers == null) return image;
            foreach (var candidate in manager.cityBuildingControllers)
            {
                if (candidate == null) continue;
                try
                {
                    var registration = candidate.buildingRegistration;
                    string? address = registration == null || string.IsNullOrWhiteSpace(registration.StreetName) ? null
                        : "address:" + Uri.EscapeDataString(registration.StreetName) + ":"
                            + registration.StreetNumber.ToString(CultureInfo.InvariantCulture) + ":";
                    bool hasEntrance = GamePlaces.TryEntrance(candidate, out var entrance) && entrance.IsFinite;
                    image.Add(new Frontage(candidate, address, entrance, hasEntrance));
                }
                catch (Exception)
                {
                    // Unreadable ownership cannot be ruled out as a conflict. Refuse the image.
                    return new FrontageImage();
                }
            }
            return image;
        }
        private static CityBuildingController? ResolveFrontage(Place place, FrontageImage image)
        {
            if (!place.Position.IsFinite) return null;
            bool addressPlace = place.Id.StartsWith("address:", StringComparison.Ordinal);
            string? address = addressPlace ? place.Id.Substring(0, place.Id.LastIndexOf(':') + 1) : null;
            CityBuildingController? owner = null;
            if (addressPlace)
            {
                if (address == null || !image.Addresses.TryGetValue(address, out var registered) || !registered.HasValue
                    || !registered.Value.HasEntrance || Position.Distance(registered.Value.Entrance, place.Position) >= 0.1) return null;
                owner = registered.Value.Controller;
            }
            var cell = FrontageImage.Cell(place.Position);
            // A match within 0.1m can cross any of the three quantisation boundaries.
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                    for (int z = -1; z <= 1; z++)
                    {
                        if (!image.Entrances.TryGetValue((cell.X + x, cell.Y + y, cell.Z + z), out var entries)) continue;
                        foreach (var candidate in entries)
                        {
                            if (Position.Distance(candidate.Entrance, place.Position) >= 0.1) continue;
                            if (candidate.Address == null || addressPlace && candidate.Address != address
                                || owner != null && !ReferenceEquals(owner, candidate.Controller)) return null;
                            owner = candidate.Controller;
                        }
                    }
            return owner;
        }
        private IReadOnlyList<OptionalStopSpot> ParkEdge(Place place, string key)
        {
            if (!place.Id.StartsWith("scene-park:", StringComparison.Ordinal)
                || !place.EvidenceSource.StartsWith("native-park-volume;", StringComparison.Ordinal)) return Array.Empty<OptionalStopSpot>();
            // A destroyed cached trigger may have been replaced inside an already-loaded scene; the
            // former per-call scan would see the replacement, so Maintain rescans before no match is final.
            if (parkTriggers == null || parkScanDue) return Array.Empty<OptionalStopSpot>();
            {
                bool destroyed = false;
                foreach (var trigger in parkTriggers)
                {
                    if (trigger == null) { destroyed = true; continue; }
                    if (!OutdoorPlaces.StaticSceneKey(trigger.transform, out string scene) || "scene-park:" + scene != place.Id
                        || !parkActivity.Admit(place.Id, trigger.gameObject.activeInHierarchy, false)) continue;
                    // Collider bounds of an inactive volume are empty: no spots now, nothing cached, retried later.
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
                if (destroyed) parkScanDue = true;
            }
            return Array.Empty<OptionalStopSpot>();
        }
        // Issue #128: ParkEdge ran a whole-scene FindObjectsOfType on every uncached call, inside an
        // advancement unit. Matching requires a scene-authored, non-clone trigger, so the result set can
        // only change when a scene loads, finishes loading or unloads. Rescan exactly then; every call still
        // applies the same null, activity and live scene-key checks in the same order.
        private LocationHappinessTrigger[]? parkTriggers;
        private readonly List<int> parkTriggerScenes = new List<int>();
        // Compares the loaded scene set with the recorded one, then records the current set.
        private static bool SameScenes(List<int> recorded)
        {
            int count = SceneManager.sceneCount;
            bool same = recorded.Count == count * 2;
            for (int i = 0; same && i < count; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                same = recorded[2 * i] == scene.handle && recorded[2 * i + 1] == (scene.isLoaded ? 1 : 0);
            }
            if (same) return true;
            recorded.Clear();
            for (int i = 0; i < count; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                recorded.Add(scene.handle); recorded.Add(scene.isLoaded ? 1 : 0);
            }
            return false;
        }
        // F2h: the native performer spawns only while its spawner is in the 60 m culling band around the player,
        // by chance and at a random point in the spawner's area. Catalogue the authored areas instead, inactive
        // spawners included whatever their activity at the scan (CityMapHider deactivates city objects while the
        // player is indoors or the map is open), and rescan only when the scene set changes. Spawner settings
        // and DLC visibility are the game reasons that exclude a site.
        private readonly List<int> performerScenes = new List<int>();
        private bool performersRead;
        public long PerformerScans { get; private set; }
        private void ScanPerformers()
        {
            performersRead = true; PerformerScans++;
            var readings = new List<PerformerSiteReading>();
            foreach (var zone in UnityEngine.Object.FindObjectsOfType<StationaryAiSpawner>(true))
            {
                try
                {
                    if (zone == null || !OutdoorPlaces.StaticSceneKey(zone.transform, out string key)) continue;
                    var area = zone.streetPerformerBoundsPositionGiver;
                    bool excluded = !zone.canSpawnStreetPerformers || area == null || zone.streetPerformerSpawnChance <= 0
                        || zone.disabled || OutdoorPlaces.DlcHidden(zone.transform);
                    // The native spawn point is uniform in the area's horizontal bounds, so its centre is the
                    // expected performer position.
                    var center = area == null ? zone.transform.position
                        : area.transform.position + area.transform.rotation * new Vector3(area.bounds.center.x, 0, area.bounds.center.z);
                    readings.Add(new PerformerSiteReading(key, excluded,
                        PhysicalPedestrianBody.Point(center), zone, area == null ? (object)zone : area));
                }
                catch (Exception) { }
            }
            performers.Replace(readings);
        }
        private IReadOnlyList<OptionalStopSpot> Audience(Place place)
        {
            if (!performersRead) return Array.Empty<OptionalStopSpot>();
            // Only pedestrians passing this entrance; no destination/trip is generated for a show.
            return audiences.Get(place, performers.Near(place), SampleAudience);
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
        public OptionalStopAvailability Available(OptionalStopState stop)
        {
            long started = BeginSeam();
            try { return ReadAvailable(stop); }
            catch (Exception) { return OptionalStopAvailability.Unavailable; }
            finally { RecordSeam(started); }
        }
        private OptionalStopAvailability ReadAvailable(OptionalStopState stop)
        {
            if (!places.TryGetValue(stop.PlaceId, out var place)) return OptionalStopAvailability.Unavailable;
            if (ScanDue(stop.Kind) || unresolved.Contains(place)) return OptionalStopAvailability.Deferred;
            if (stop.Kind != DwellKind.Audience && stop.Kind != DwellKind.Resting && !NativeDoorPresent(place, stop.Kind))
                return rebindDue || unresolved.Contains(place) ? OptionalStopAvailability.Deferred : OptionalStopAvailability.Unavailable;
            var geometryStatus = GeometryStatus(place, stop);
            if (geometryStatus != OptionalStopAvailability.Ready) return geometryStatus;
            // Rebind saved native identities once. A destroyed volume or a site gone from the scene ends the saved
            // stop; a later reappearance cannot restart it. A deactivation does not end it (F2h).
            if (stop.Kind == DwellKind.Audience)
            {
                if (!performers.Available(stop.TargetId, stop.Facing)) return OptionalStopAvailability.Unavailable;
            }
            if (stop.Kind == DwellKind.Resting && !targets.ContainsKey(stop.TargetId)) ParkEdge(place, place.Id + ":" + stop.Kind);
            if (ScanDue(stop.Kind)) return OptionalStopAvailability.Deferred;
            if (stop.Kind == DwellKind.Resting && (!targets.TryGetValue(stop.TargetId, out var target) || target == null
                || !parkActivity.Admit(stop.TargetId, target.gameObject.activeInHierarchy, false))) return OptionalStopAvailability.Unavailable;
            return NavMesh.SamplePosition(PhysicalPedestrianBody.Vector(stop.Position), out var hit, 0.15f, WalkingRoutes.DryFilter)
                && Position.Distance(PhysicalPedestrianBody.Point(hit.position), stop.Position) < 0.15
                ? OptionalStopAvailability.Ready : OptionalStopAvailability.Unavailable;
        }
        // A cold facade waiting for this frame's budget is not evidence of disappearance.
        private OptionalStopAvailability GeometryStatus(Place place, OptionalStopState stop)
        {
            if (!validatedSpots.TryGetValue(stop.SpotId, out var geometry))
            {
                Spots(place, stop.Kind);
                if (!validatedSpots.TryGetValue(stop.SpotId, out geometry))
                    return stop.Kind != DwellKind.Audience && stop.Kind != DwellKind.Resting
                        && doors.Unsampled(place) && doors.SampledThisFrame
                        ? OptionalStopAvailability.Deferred : OptionalStopAvailability.Unavailable;
            }
            if (Position.Distance(stop.Position, geometry.Position) > 0.01) return OptionalStopAvailability.Unavailable;
            return OptionalStopAvailability.Ready;
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
