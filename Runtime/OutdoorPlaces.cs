using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Extensions;
using PersistentCity.Core;
using PlayerActivity;
using UnityEngine;
using UnityEngine.AI;

namespace PersistentCity.Runtime
{
    /// <summary>Read-only discovery of native outdoor happiness volumes; no native queue, trigger or economic calls.</summary>
    internal static class OutdoorPlaces
    {
        private const int MaximumAreas = 64;
        private const int MaximumColliders = 8;
        private const int MaximumAnchors = 4;
        private const int MaximumSamples = 1024;
        private const int MaximumPaths = 128;
        private const int ParkAllocation = 64; // Simulation allocation, not native park capacity.
        private const int BeachAllocation = 96; // Simulation allocation, not a native attendance measurement.
        private const string Hamptons = "ba:neighborhood_thehamptons";
        private const string VerifiedPartyPierSource = "Buildings/Pier[0]/SM_Harbor%20Import%2002_Building_Pier4_Exterior[8]/HappinessTrigger[6]";
        // Exact native scene identities captured read-only on the isolated 1.0 test rig.
        // A new/unrecognized beach volume remains unavailable until its public context is reviewed.
        private static readonly HashSet<string> VerifiedBeachSources = new HashSet<string>(StringComparer.Ordinal)
        {
            "TheHamptons/TheHamptons[0]/Roads[1]/SM_HamptonsTerrain[16]/Beach[4]/BigBeach[0]/BeachHappinessTrigger[10]",
            "TheHamptons/TheHamptons[0]/Roads[1]/SM_HamptonsTerrain[16]/Beach[4]/SmallBeach[1]/BeachHappinessTrigger[13]"
        };
        private static readonly FieldInfo? Balance = typeof(LocationHappinessTrigger).GetField("balanceConfig", BindingFlags.Instance | BindingFlags.NonPublic);
        public static string LastDiscoverySummary { get; private set; } = "Outdoor catalogue has not been read.";

        internal static List<Place> Read(IReadOnlyList<Place> buildingPlaces)
        {
            var result = new List<Place>();
            int unsupported = 0, inaccessible = 0, failed = 0, limited = 0, processed = 0, samples = 0, paths = 0;
            var manager = CityManager.Instance;
            if (manager == null || SaveGameManager.Current == null || Balance == null)
            {
                LastDiscoverySummary = "Native park adapter unavailable; no outdoor places added.";
                return result;
            }
            LocationHappinessTrigger[] triggers;
            HamptonsPrivateFenceDoor[] gates;
            try
            {
                triggers = UnityEngine.Object.FindObjectsOfType<LocationHappinessTrigger>(true);
                gates = UnityEngine.Object.FindObjectsOfType<HamptonsPrivateFenceDoor>(true);
            }
            catch (Exception)
            {
                LastDiscoverySummary = "Native park discovery failed; no outdoor places added.";
                return result;
            }
            var anchors = buildingPlaces.Where(p => p.Kind != PlaceKind.Home)
                .GroupBy(p => p.Id.Substring(0, p.Id.LastIndexOf(':')), StringComparer.Ordinal)
                .Select(g => g.First()).OrderBy(p => p.Id, StringComparer.Ordinal).ToArray();
            var sources = new List<(LocationHappinessTrigger Trigger, string Id, bool Skate, bool Beach, bool Pier)>();
            var publicAccess = new PublicRoadAccess();
            foreach (var trigger in triggers)
            {
                try
                {
                    if (trigger == null || !trigger.gameObject.activeInHierarchy || !StaticSceneKey(trigger.transform, out string key)) continue;
                    var config = Balance.GetValue(trigger) as PlayerActivityBalanceConfig;
                    string? type = config?.TemporalType;
                    bool skate = type == "ba:happinessmodifier_in_the_skate_park";
                    bool beach = type == "ba:happinessmodifier_walking_in_the_beach" && trigger.gameObject.scene.name == "TheHamptons"
                        && VerifiedBeachSources.Contains(key);
                    bool pier = type == "ba:happinessmodifier_partying_at_the_pier" && trigger.gameObject.scene.name == "Buildings"
                        && key == VerifiedPartyPierSource;
                    if (!skate && !beach && !pier && type != "ba:happinessmodifier_walking_in_the_park") { unsupported++; continue; }
                    // Keep existing park identities unchanged when expanding the catalogue.
                    sources.Add((trigger, (pier ? "scene-party-pier:" : beach ? "scene-beach:" : "scene-park:") + key, skate, beach, pier));
                }
                catch (Exception) { failed++; }
            }
            // Preserve park discovery priority within the shared bounded budget.
            sources.Sort((a, b) => a.Pier != b.Pier ? a.Pier.CompareTo(b.Pier)
                : a.Beach != b.Beach ? a.Beach.CompareTo(b.Beach) : StringComparer.Ordinal.Compare(a.Id, b.Id));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var source in sources)
            {
                if (processed++ >= MaximumAreas || samples >= MaximumSamples || paths >= MaximumPaths) { limited++; continue; }
                try
                {
                    if (!ids.Add(source.Id)) { failed++; continue; }
                    var trigger = source.Trigger;
                    var parentBuilding = trigger.GetComponentInParent<CityBuildingController>();
                    // The reviewed native source is a separate outdoor mesh/volume. A
                    // future building/boat parent invalidates that public-walking context.
                    if (source.Pier && (parentBuilding != null || trigger.GetComponentInParent<CasinoBoatManager>() != null)) { inaccessible++; continue; }
                    if (parentBuilding != null && !GamePlaces.BuildingAccessible(parentBuilding)) { inaccessible++; continue; }
                    if (trigger.GetComponentsInParent<ToggleObjectDLC>(true).Any(t => t.showWhenOwned != t.dlc.DlcIsOwned())) { inaccessible++; continue; }
                    var colliders = trigger.GetComponentsInChildren<Collider>(false).Where(Usable)
                        .OrderBy(c => SceneKey(c.transform), StringComparer.Ordinal).ToArray();
                    if (colliders.Length == 0 || colliders.Length > MaximumColliders) { inaccessible++; continue; }
                    var accessAnchors = source.Pier ? anchors.Where(p => !p.Id.StartsWith("address:" + Uri.EscapeDataString("ba:street_pier") + ":5:", StringComparison.Ordinal)).ToArray() : anchors;
                    if (!TryPoint(colliders, accessAnchors, gates, publicAccess, source.Beach, trigger.transform.position.y,
                        ref samples, ref paths, out Position position, out string district)) { inaccessible++; continue; }
                    result.Add(new Place
                    {
                        Id = source.Id, Name = source.Pier ? "Outdoor Party Pier" : source.Beach ? (source.Id.IndexOf("/BigBeach[", StringComparison.Ordinal) >= 0 ? "Hamptons Big Beach" : "Hamptons Small Beach")
                            : source.Skate ? "Native skate park" : "Native park", District = district,
                        Kind = PlaceKind.Leisure, Position = position, Capacity = source.Beach ? BeachAllocation : ParkAllocation,
                        // The volume has no native opening hours. All-day outdoor access is
                        // an explicit simulation policy; it never asserts a native schedule.
                        OpenMinute = 0, CloseMinute = 1440, AttractionWeight = source.Pier ? 3 : source.Beach ? 4 : source.Skate ? 1.5 : 2.5,
                        NativeTrafficIndex = -1,
                        EvidenceSource = (source.Pier ? "native-party-pier-volume;verified-scene-source;walking-only;casino-boarding-unmodeled;"
                            : source.Beach ? "native-beach-volume;verified-scene-source;" : "native-park-volume;")
                            + "policy:public-ground,all-day-outdoor,tier-" + (source.Pier ? "pier" : source.Beach ? "beach" : source.Skate ? "skate" : "park")
                    });
                }
                catch (Exception) { failed++; }
            }
            LastDiscoverySummary = "Native outdoors: " + result.Count(p => p.Id.StartsWith("scene-park:", StringComparison.Ordinal)) + " parks and "
                + result.Count(p => p.Id.StartsWith("scene-beach:", StringComparison.Ordinal)) + " beaches and "
                + result.Count(p => p.Id.StartsWith("scene-party-pier:", StringComparison.Ordinal)) + " outdoor party piers accepted by public-ground checks, " + inaccessible + " failed those checks, "
                + failed + " failed, " + limited + " over discovery limit, " + unsupported + " other happiness volumes; " + samples + " samples, " + paths + " path checks. "
                + "Party pier means outdoor walking only; casino boarding is not modeled. Other map activity anchors await public-access/schedule evidence.";
            return result;
        }

        private static bool Usable(Collider collider) => collider != null && collider.enabled && collider.isTrigger
            && collider.gameObject.activeInHierarchy && (!(collider is MeshCollider mesh) || mesh.convex);

        private static bool TryPoint(Collider[] colliders, Place[] anchors, HamptonsPrivateFenceDoor[] gates, PublicRoadAccess publicAccess,
            bool beach, float nativeHeight, ref int samples, ref int paths, out Position position, out string district)
        {
            position = default; district = "";
            foreach (var collider in colliders)
            {
                Bounds bounds = collider.bounds;
                if (!Finite(bounds.center) || !Finite(bounds.size) || bounds.size.x < 1 || bounds.size.z < 1) continue;
                // A deterministic 5 x 5 grid, centre first, bounds inset at every edge.
                // A high trigger's geometric centre is not the ground. Sample near its bottom.
                // Preserve the original park sampling order and ground points. Beaches have
                // volumes extending below terrain: try native transform height, nearby levels
                // and the volume centre rather than assume its bottom is pedestrian ground.
                var levels = beach ? new[] { nativeHeight, nativeHeight - 2, nativeHeight + 2, bounds.center.y,
                    bounds.min.y + Math.Min(0.25f, bounds.size.y * 0.5f) }
                    : new[] { bounds.min.y + Math.Min(0.25f, bounds.size.y * 0.5f) };
                // A rotated, narrow beach may have its only NPC-walkable shoreline near
                // the boundary, outside the inset bounds grid. Use measured native public
                // entrances projected onto the actual volume as additional beach candidates.
                // These still require a dry sample inside the volume and a complete route.
                var projected = beach ? anchors.Where(a => a.District == Hamptons)
                    .OrderBy(a => Distance(a.Position, bounds.center)).ThenBy(a => a.Id, StringComparer.Ordinal)
                    .Take(12).Select(a => collider.ClosestPoint(ToVector(a.Position))).ToArray() : Array.Empty<Vector3>();
                int gridSamples = 26 * levels.Length;
                var dryFilter = WalkingRoutes.DryFilter;
                for (int sample = 0; sample < gridSamples + projected.Length; sample++)
                {
                    if (samples >= MaximumSamples || paths >= MaximumPaths) return false;
                    samples++;
                    int horizontal = sample / levels.Length;
                    float y = Math.Max(bounds.min.y, Math.Min(bounds.max.y, levels[sample % levels.Length]));
                    float x = horizontal == 0 ? 0.5f : 0.1f + ((horizontal - 1) % 5) * 0.2f;
                    float z = horizontal == 0 ? 0.5f : 0.1f + ((horizontal - 1) / 5) * 0.2f;
                    Vector3 candidate = sample < gridSamples ? new Vector3(bounds.min.x + bounds.size.x * x, y, bounds.min.z + bounds.size.z * z) : projected[sample - gridSamples];
                    if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2.5f, dryFilter)
                        || !Finite(hit.position) || Math.Abs(hit.position.y - candidate.y) > 2.5f) continue;
                    // ClosestPoint rejects holes and points outside the actual volume. Concave
                    // meshes were excluded because their closest-point test is unsupported.
                    if ((collider.ClosestPoint(hit.position) - hit.position).sqrMagnitude > 0.0625f) continue;
                    if (NearPrivateGate(hit.position, gates)) continue;
                    string inferredDistrict = NearestBuildingDistrict(hit.position);
                    if (string.IsNullOrEmpty(inferredDistrict) || (beach ? inferredDistrict != Hamptons : inferredDistrict == Hamptons)) continue;
                    var nearest = anchors.Select(a => (Place: a, Distance: Distance(a.Position, hit.position)))
                        .Where(a => a.Distance <= 350 && a.Place.District == inferredDistrict).OrderBy(a => a.Distance).ThenBy(a => a.Place.Id, StringComparer.Ordinal)
                        .Take(MaximumAnchors);
                    foreach (var anchor in nearest)
                    {
                        // District inference is for classification, never an assertion of a
                        // surveyed boundary. Only reviewed native beach sources enable Hamptons.
                        if (paths >= MaximumPaths) return false;
                        paths++;
                        var path = new NavMeshPath();
                        if (!NavMesh.CalculatePath(ToVector(anchor.Place.Position), hit.position, dryFilter, path)
                            || path.status != NavMeshPathStatus.PathComplete) continue;
                        var corners = path.corners;
                        if (corners.Length == 0 || corners.Length > 128 || !PublicPath(corners, gates, publicAccess)) continue;
                        position = new Position(hit.position.x, hit.position.y, hit.position.z);
                        district = inferredDistrict;
                        return true;
                    }
                }
            }
            return false;
        }

        private static string NearestBuildingDistrict(Vector3 point)
        {
            double nearest = double.MaxValue;
            string district = "";
            var controllers = CityManager.Instance?.cityBuildingControllers;
            if (controllers == null) return "";
            foreach (var controller in controllers)
            {
                if (controller == null || controller.building == null) continue;
                Vector3 p = controller.transform.position;
                double distance = (p.x - point.x) * (p.x - point.x) + (p.z - point.z) * (p.z - point.z);
                if (distance < nearest) { nearest = distance; district = controller.building.Neighbourhood ?? ""; }
            }
            return district;
        }

        private static bool NearPrivateGate(Vector3 point, HamptonsPrivateFenceDoor[] gates)
        {
            foreach (var gate in gates)
                if (gate != null && gate.gameObject.scene.IsValid() && Distance(point, gate.transform.position) < 15) return true;
            return false;
        }

        private static bool PublicPath(Vector3[] points, HamptonsPrivateFenceDoor[] gates, PublicRoadAccess publicAccess)
        {
            double length = 0;
            for (int i = 0; i < points.Length; i++)
            {
                if (!Finite(points[i]) || NearPrivateGate(points[i], gates)) return false;
                if (i == 0) continue;
                if (!publicAccess.ClearSegment(new Position(points[i - 1].x, points[i - 1].y, points[i - 1].z),
                    new Position(points[i].x, points[i].y, points[i].z))) return false;
                length += Vector3.Distance(points[i - 1], points[i]);
                foreach (var gate in gates)
                {
                    if (gate == null || !gate.gameObject.scene.IsValid()) continue;
                    Vector3 a = points[i - 1], b = points[i], p = gate.transform.position;
                    double dx = b.x - a.x, dz = b.z - a.z, square = dx * dx + dz * dz;
                    double t = square > 0 ? Math.Max(0, Math.Min(1, ((p.x - a.x) * dx + (p.z - a.z) * dz) / square)) : 0;
                    double gx = a.x + dx * t - p.x, gz = a.z + dz * t - p.z;
                    if (gx * gx + gz * gz < 225) return false;
                }
            }
            return length <= 1500;
        }

        private static bool StaticSceneKey(Transform transform, out string key)
        {
            key = "";
            if (!transform.gameObject.scene.IsValid() || string.IsNullOrEmpty(transform.gameObject.scene.name)
                || transform.gameObject.scene.name == "DontDestroyOnLoad") return false;
            int depth = 0;
            for (var parent = transform; parent != null; parent = parent.parent)
                if (++depth > 64 || parent.name.IndexOf("(Clone)", StringComparison.Ordinal) >= 0) return false;
            key = SceneKey(transform);
            return true;
        }

        private static string SceneKey(Transform transform)
        {
            var segments = new List<string>();
            for (var parent = transform; parent != null; parent = parent.parent)
                segments.Add(Uri.EscapeDataString(parent.name) + "[" + parent.GetSiblingIndex().ToString(CultureInfo.InvariantCulture) + "]");
            segments.Reverse();
            // Scene hierarchy identities are deterministic for this game build, not native
            // persistent save IDs. Native map changes require catalogue reconciliation.
            return Uri.EscapeDataString(transform.gameObject.scene.name) + "/" + string.Join("/", segments);
        }

        private static Vector3 ToVector(Position p) => new Vector3((float)p.X, (float)p.Y, (float)p.Z);
        private static bool Finite(Vector3 p) => Position.Finite(p.x) && Position.Finite(p.y) && Position.Finite(p.z);
        private static double Distance(Position a, Vector3 b) => Distance(ToVector(a), b);
        private static double Distance(Vector3 a, Vector3 b) => Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z));
    }
}
