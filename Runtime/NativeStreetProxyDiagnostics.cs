using System;
using System.Collections.Generic;
using System.Globalization;
using BigAmbitions.InteriorDesigner.InteriorElements;
using PersistentCity.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PersistentCity.Runtime
{
    // No initializer, Update participant, event subscriptions, caches or render resources.
    // Every native read and allocation is downstream of an explicit bridge request.
    internal static class NativeStreetProxyDiagnostics
    {
        internal static Position Point(Vector3 p) => new Position(p.x, p.y, p.z);
        private static StreetFrame Frame(Transform t)
        {
            var m = t.localToWorldMatrix;
            return new StreetFrame(Point(t.position), Point(m.MultiplyVector(Vector3.right)),
                Point(m.MultiplyVector(Vector3.up)), Point(m.MultiplyVector(Vector3.forward)));
        }
        private static string Name(string? value)
        {
            value = value ?? "";
            return value.Length <= StreetInventoryBudget.MaximumNameLength ? value : value.Substring(0, StreetInventoryBudget.MaximumNameLength);
        }
        private static BuildingManager Context()
        {
            var manager = InstanceBehavior<BuildingManager>.Instance;
            if (!BuildingManager.IsInsideBuilding || manager == null || manager.enteringBuilding || manager.exitingBuilding
                || manager.building == null || manager.currentBuildingVersion == null || manager.cityBuildingController == null
                || manager.cityBuildingController.building != manager.building)
                throw new InvalidOperationException("stable loaded shop context required");
            return manager;
        }

        internal static StreetProxyDoorSnapshot ReadDoors(IReadOnlyList<StreetEdgePair>? edges)
        {
            var snapshot = new StreetProxyDoorSnapshot { Frame = Time.frameCount, ViewConfigured = NativeInteriorCityView.Configured,
                GuardDetected = NativeInteriorGuard.Verdict.Detected, GuardReason = NativeInteriorGuard.Verdict.Reason };
            try
            {
                var manager = Context();
                var controller = manager.cityBuildingController;
                snapshot.Address = Name(manager.building.Address.ToString()); snapshot.Size = Name(manager.building.BuildingSize);
                snapshot.BuildingVersion = manager.building.BuildingVersion;
                snapshot.LayoutInstance = manager.currentBuildingVersion.GetInstanceID(); snapshot.Layout = Frame(manager.currentBuildingVersion);
                snapshot.MultipleHeights = manager.multipleHeightsBuildingController != null;
                var exterior = controller.entranceDoors; var interior = manager.exitZones;
                if (exterior == null || exterior.Length == 0 || interior == null || interior.Count == 0)
                    throw new InvalidOperationException("missing doors/exits");
                if (exterior.Length > StreetProxyGeometry.MaximumDoors || interior.Count > StreetProxyGeometry.MaximumDoors
                    || (edges?.Count ?? 0) > StreetProxyGeometry.MaximumEdges) throw new InvalidOperationException("door/edge cap");
                snapshot.Exterior = new StreetDoorFrame[exterior.Length]; snapshot.Interior = new StreetDoorFrame[interior.Count];
                for (int i = 0; i < exterior.Length; i++)
                {
                    var door = exterior[i];
                    if (door == null || door.doorTransform == null) throw new InvalidOperationException("missing exterior frame");
                    snapshot.Exterior[i] = new StreetDoorFrame(door.doorId, Frame(door.doorTransform));
                }
                bool elevator = snapshot.MultipleHeights;
                for (int i = 0; i < interior.Count; i++)
                {
                    var exit = interior[i];
                    if (exit == null || exit.despawner == null || !exit.transform.IsChildOf(manager.currentBuildingVersion)
                        || !exit.despawner.transform.IsChildOf(exit.transform)) throw new InvalidOperationException("missing/foreign exit despawner");
                    elevator |= exit.isElevator;
                    if (exit.isDriveInBay || exit.despawner.isCasinoExit || exit.despawner.isParkingExit || exit.despawner.hasSpecialCondition)
                        throw new InvalidOperationException("unsupported special exit");
                    snapshot.Interior[i] = new StreetDoorFrame(exit.despawner.exitToZoneId, Frame(exit.transform), exit.isElevator);
                }
                // Bound the cache observation too. No F0 Refresh/Active getter is invoked.
                if (InteriorElementsHelper.InteriorElementsCache.Count > StreetInventoryBudget.MaximumNodes)
                    throw new InvalidOperationException("floor cache cap");
                foreach (var element in InteriorElementsHelper.InteriorElementsCache.Values)
                    if (element != null && element.IsFloor && element.transform.IsChildOf(manager.currentBuildingVersion))
                    { snapshot.FloorHeight = element.transform.position.y; break; }
                var reg = manager.buildingRegistration;
                bool shop = manager.building.BuildingType == "ba:buildingtype_retail" && reg != null && reg.HasEstablishedBusiness
                    && !string.IsNullOrEmpty(reg.businessTypeName) && reg.businessTypeName != "ba:businesstype_empty"
                    && GamePlaces.IsRetailShopActivity(reg.businessTypeName);
                snapshot.Eligibility = InteriorViewEligibility.Check(shop, elevator, snapshot.Exterior[0].Frame.Origin,
                    snapshot.FloorHeight ?? double.NaN).Fallback.ToString();
                snapshot.SuppliedEdges = new StreetEdgePair[edges?.Count ?? 0];
                for (int i = 0; i < snapshot.SuppliedEdges.Length; i++)
                {
                    var edge = edges![i];
                    if ((edge.Feature?.Length ?? 0) > StreetInventoryBudget.MaximumNameLength || (edge.Point?.Length ?? 0) > StreetInventoryBudget.MaximumNameLength)
                        throw new InvalidOperationException("edge label cap");
                    snapshot.SuppliedEdges[i] = edge;
                }
                snapshot.Decision = StreetProxyGeometry.Evaluate(snapshot.Exterior, snapshot.Interior, snapshot.MultipleHeights, snapshot.SuppliedEdges);
                snapshot.Complete = true;
                if (snapshot.Eligibility != "None") snapshot.Refusal = "ineligible: " + snapshot.Eligibility;
                else if (!snapshot.Decision.Consistent) snapshot.Refusal = snapshot.Decision.Failure.ToString();
            }
            catch (Exception e) { snapshot.Refusal = "observation refused: " + Name(e.Message); snapshot.Complete = false; snapshot.Decision = null; }
            return snapshot;
        }

        internal static StreetProxyInventorySnapshot ReadInventory(PhysicalPopulation? physical)
        {
            var snapshot = new StreetProxyInventorySnapshot { Frame = Time.frameCount };
            var budget = new StreetInventoryBudget();
            try
            {
                var manager = Context();
                var observedLayout = manager.currentBuildingVersion;
                int observedScenes = SceneManager.sceneCount;
                var doors = manager.cityBuildingController.entranceDoors;
                if (doors == null || doors.Length == 0 || doors[0] == null || doors[0].doorTransform == null)
                    throw new InvalidOperationException("missing exterior anchor");
                snapshot.Address = Name(manager.building.Address.ToString()); snapshot.Anchor = Point(doors[0].doorTransform.position);
                if (!snapshot.Anchor.IsFinite) throw new InvalidOperationException("invalid anchor");
                snapshot.Ambient = "mode=" + RenderSettings.ambientMode + "; intensity=" + RenderSettings.ambientIntensity.ToString(CultureInfo.InvariantCulture)
                    + "; fog=" + RenderSettings.fog;
                var camera = Camera.main;
                if (camera == null) throw new InvalidOperationException("main camera missing");
                snapshot.MainCameraView = Matrix(camera.worldToCameraMatrix); snapshot.MainCameraProjection = Matrix(camera.projectionMatrix);
                var owned = physical?.ReadStreetProxyBodies() ?? new List<StreetProxyBodyMetadata>();
                var ownedIds = new HashSet<int>();
                foreach (var body in owned)
                {
                    if (!ownedIds.Add(body.Instance)) throw new InvalidOperationException("duplicate owned body");
                    AddBody(snapshot, budget, body);
                }
                // Enumerate loaded scene roots and traverse without global FindObjects arrays.
                // Inactive native renderers are included. No Addressables/assets are loaded.
                var roots = new List<GameObject>(); var components = new List<Component>(); var materials = new List<Material>();
                var ancestry = new List<Component>(); var hints = new List<string>();
                var owners = new List<Component>();
                int rootCount = 0;
                if (SceneManager.sceneCount > 64) throw new InvalidOperationException("scene cap");
                for (int s = 0; s < SceneManager.sceneCount; s++)
                {
                    var scene = SceneManager.GetSceneAt(s);
                    if (!scene.isLoaded) continue;
                    rootCount += scene.rootCount;
                    if (rootCount > StreetInventoryBudget.MaximumRoots) throw new InvalidOperationException("root cap");
                    scene.GetRootGameObjects(roots);
                    foreach (var root in roots)
                    {
                        var node = root.transform;
                        while (node != null)
                        {
                            if (!budget.VisitNode()) throw new InvalidOperationException("node cap");
                            node.GetComponents(components);
                            if (components.Count > StreetInventoryBudget.MaximumComponents) throw new InvalidOperationException("component cap");
                            foreach (var component in components)
                            {
                                if (component == null) throw new InvalidOperationException("missing scene script");
                                if (component is LODGroup || component is ViewBlockingEntity)
                                {
                                    if (owners.Count == StreetInventoryBudget.MaximumOwners) throw new InvalidOperationException("owner cap");
                                    owners.Add(component);
                                }
                                if (component is Renderer renderer) AddRenderer(snapshot, budget, renderer, ancestry, hints, materials);
                                else if (component is Light light) AddLight(snapshot, budget, light);
                                else if (component.GetType().FullName == "UnityEngine.Rendering.Volume") AddVolume(snapshot, budget, component);
                                else if (component is GleyTrafficSystem.VehicleComponent vehicle && !ownedIds.Contains(node.GetInstanceID()))
                                {
                                    if (vehicle.rb == null) throw new InvalidOperationException("native traffic velocity unreadable");
                                    AddBody(snapshot, budget, new StreetProxyBodyMetadata { Instance = node.GetInstanceID(), Kind = "native-traffic",
                                        Position = Point(node.position), Speed = vehicle.rb.velocity.magnitude, Active = node.gameObject.activeInHierarchy });
                                }
                            }
                            // Depth-first successor; no per-node stack/list or child array.
                            if (node.childCount != 0) { node = node.GetChild(0); continue; }
                            while (node != root.transform && node.GetSiblingIndex()+1 >= node.parent.childCount) node = node.parent;
                            node = node == root.transform ? null : node.parent.GetChild(node.GetSiblingIndex()+1);
                        }
                    }
                }
                ReadOwnerMembership(snapshot.Renderers, owners);
                // Detect a lifecycle/frame mismatch rather than publish mixed contexts.
                if (Context() != manager || snapshot.Frame != Time.frameCount || manager.currentBuildingVersion != observedLayout
                    || SceneManager.sceneCount != observedScenes)
                    throw new InvalidOperationException("context changed");
                snapshot.Complete = true;
            }
            catch (Exception e) { snapshot.Refusal = "inventory refused: " + Name(e.Message); snapshot.Complete = false; }
            snapshot.NodesVisited = budget.Nodes; snapshot.Rows = budget.Rows;
            return snapshot;
        }

        private static double[] Matrix(Matrix4x4 matrix)
        {
            var values = new double[16];
            for (int i = 0; i < values.Length; i++)
            { values[i] = matrix[i]; if (!Position.Finite(values[i])) throw new InvalidOperationException("invalid camera matrix"); }
            return values;
        }
        private static StreetBoundsDistances Distances(Position anchor, Bounds bounds) => new StreetBoundsDistances(anchor, Point(bounds.min), Point(bounds.max));
        private static void Row(StreetInventoryBudget budget) { if (!budget.AddRow()) throw new InvalidOperationException("metadata row cap"); }

        private static void ReadOwnerMembership(List<StreetProxyRendererMetadata> renderers, List<Component> owners)
        {
            // Resolve explicit references after traversal: native owners need not be ancestors,
            // and may appear after their renderers. Index only rows actually included in this dump.
            var included = new HashSet<int>(); var membership = new Dictionary<int, List<string>>();
            foreach (var renderer in renderers) included.Add(renderer.Instance);
            int references = 0;
            void Reference(Renderer renderer, string label)
            {
                if (renderer == null) throw new InvalidOperationException("missing owner renderer reference");
                int id = renderer.GetInstanceID(); if (!included.Contains(id)) return;
                if (!membership.TryGetValue(id, out var labels)) membership.Add(id, labels = new List<string>());
                if (labels.Contains(label)) return;
                if (labels.Count == StreetInventoryBudget.MaximumOwnersPerRenderer) throw new InvalidOperationException("per-renderer owner cap");
                labels.Add(label);
            }
            void Reserve(int count)
            {
                if (count < 0 || count > StreetInventoryBudget.MaximumOwnerReferences-references) throw new InvalidOperationException("owner reference cap");
                references += count;
            }
            void Collection(IReadOnlyList<Renderer> collection, string label)
            {
                if (collection == null) throw new InvalidOperationException("unreadable owner collection");
                Reserve(collection.Count);
                foreach (var renderer in collection) Reference(renderer, label);
            }
            foreach (var owner in owners)
            {
                string label = Name(owner.GetType().Name) + "#" + owner.GetInstanceID().ToString(CultureInfo.InvariantCulture);
                if (owner is LODGroup group)
                {
                    var lods = group.GetLODs();
                    if (lods == null || lods.Length > StreetInventoryBudget.MaximumLods) throw new InvalidOperationException("LOD cap/missing data");
                    for (int i = 0; i < lods.Length; i++) Collection(lods[i].renderers, label + ":lod" + i.ToString(CultureInfo.InvariantCulture));
                }
                if (owner is ViewBlockingEntity fade)
                {
                    Collection(fade.renderersToFade, label + ":fade");
                    Collection(fade.renderersToHide, label + ":hide");
                }
                if (owner is CityBuildingController building && building.lodVersion != null)
                { Reserve(1); Reference(building.lodVersion, label + ":lodVersion"); }
            }
            foreach (var renderer in renderers)
                if (membership.TryGetValue(renderer.Instance, out var labels)) renderer.OwnerMembership = labels.ToArray();
        }

        private static void AddRenderer(StreetProxyInventorySnapshot snapshot, StreetInventoryBudget budget, Renderer renderer,
            List<Component> ancestry, List<string> hints, List<Material> materials)
        {
            var distances = Distances(snapshot.Anchor, renderer.bounds);
            if (!distances.Valid) throw new InvalidOperationException("invalid renderer bounds");
            if (distances.Classify(120) == StreetRadiusClass.Outside) return;
            Row(budget);
            bool movingOwner = false; hints.Clear();
            var parent = renderer.transform; int depth = 0;
            while (parent != null)
            {
                if (++depth > StreetInventoryBudget.MaximumAncestry) throw new InvalidOperationException("ancestry cap");
                parent.GetComponents(ancestry);
                if (ancestry.Count > StreetInventoryBudget.MaximumComponents) throw new InvalidOperationException("owner component cap");
                foreach (var owner in ancestry)
                {
                    if (owner == null) throw new InvalidOperationException("missing owner script");
                    string kind = owner.GetType().Name;
                    movingOwner |= owner is Rigidbody || owner is Animator || owner is UnityEngine.AI.NavMeshAgent
                        || owner is ThirdPersonCharacter || owner is GleyTrafficSystem.VehicleComponent;
                    if (owner is LODGroup || owner is CityBuildingController || kind == "TreeController" || kind == "ViewBlockingObject")
                    {
                        if (hints.Count == StreetInventoryBudget.MaximumAncestry) throw new InvalidOperationException("owner hint cap");
                        hints.Add(Name(kind + "#" + owner.GetInstanceID().ToString(CultureInfo.InvariantCulture)));
                    }
                }
                parent = parent.parent;
            }
            var filter = renderer.GetComponent<MeshFilter>();
            Mesh? mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh : filter == null ? null : filter.sharedMesh;
            renderer.GetSharedMaterials(materials);
            if (materials.Count > StreetInventoryBudget.MaximumMaterials || (mesh != null && mesh.subMeshCount > StreetInventoryBudget.MaximumMaterials))
                throw new InvalidOperationException("material/submesh cap");
            var names = new string[materials.Count]; var shaders = new string[materials.Count];
            for (int i = 0; i < materials.Count; i++)
            {
                var material = materials[i];
                names[i] = material == null ? "<missing>" : Name(material.name);
                shaders[i] = material == null || material.shader == null ? "<missing>" : Name(material.shader.name);
            }
            bool candidate = renderer is MeshRenderer && mesh != null && renderer.gameObject.isStatic && !movingOwner;
            snapshot.Renderers.Add(new StreetProxyRendererMetadata { Instance = renderer.GetInstanceID(), Name = Name(renderer.name),
                Scene = Name(renderer.gameObject.scene.name), Kind = Name(renderer.GetType().Name), Minimum = Point(renderer.bounds.min), Maximum = Point(renderer.bounds.max),
                Radius30 = distances.Classify(30), Radius60 = distances.Classify(60), Radius120 = distances.Classify(120),
                Enabled = renderer.enabled, Active = renderer.gameObject.activeInHierarchy, MarkedStatic = renderer.gameObject.isStatic,
                CandidateStaticMesh = candidate, Exclusion = candidate ? "unproven materials/owners/clipping" : "nonstatic/moving/nonmesh/missing mesh",
                Mesh = mesh == null ? "<missing>" : Name(mesh.name), Submeshes = mesh == null ? 0 : mesh.subMeshCount, Materials = names, Shaders = shaders,
                HasPropertyBlock = renderer.HasPropertyBlock(), Shadows = renderer.shadowCastingMode.ToString(), OwnerHints = hints.ToArray() });
        }
        private static void AddBody(StreetProxyInventorySnapshot snapshot, StreetInventoryBudget budget, StreetProxyBodyMetadata body)
        {
            if (!body.Position.IsFinite || !Position.Finite(body.Speed) || body.Speed < 0) throw new InvalidOperationException("invalid body sample");
            double distance = Position.Distance(snapshot.Anchor, body.Position);
            if (distance > 120) return;
            Row(budget); snapshot.Bodies.Add(body);
            int[] active = body.Kind == "resident" ? snapshot.ActiveResidents : body.Kind == "owned-vehicle" ? snapshot.ActiveOwnedVehicles : snapshot.ActiveNativeTraffic;
            int[] moving = body.Kind == "resident" ? snapshot.MovingResidents : body.Kind == "owned-vehicle" ? snapshot.MovingOwnedVehicles : snapshot.MovingNativeTraffic;
            for (int i = 0; i < 3; i++) if (distance <= (i == 0 ? 30 : i == 1 ? 60 : 120) && body.Active)
            { active[i]++; if (body.Speed > .01) moving[i]++; }
        }
        private static void AddLight(StreetProxyInventorySnapshot snapshot, StreetInventoryBudget budget, Light light)
        {
            var p = Point(light.transform.position);
            if (!p.IsFinite || !Position.Finite(light.range) || !Position.Finite(light.intensity)) throw new InvalidOperationException("invalid light");
            // Include lights near either detached interior or street, plus global directionals.
            var manager = Context();
            if (light.type != LightType.Directional && Position.Distance(snapshot.Anchor, p) > 120 + light.range
                && Position.Distance(Point(manager.currentBuildingVersion.position), p) > 120 + light.range) return;
            Row(budget);
            snapshot.Environment.Add(new StreetProxyEnvironmentMetadata { Instance = light.GetInstanceID(), Kind = "Light:" + light.type,
                Position = p, Enabled = light.enabled, Active = light.gameObject.activeInHierarchy, Range = light.range, IntensityOrWeight = light.intensity,
                Detail = "cullingMask=" + light.cullingMask.ToString(CultureInfo.InvariantCulture) + "; shadows=" + light.shadows });
        }
        private static void AddVolume(StreetProxyInventorySnapshot snapshot, StreetInventoryBudget budget, Component volume)
        {
            // Public raw scalar fields only. Never access Volume.profile (can instantiate a profile).
            Row(budget); var type = volume.GetType();
            object Read(string name) => type.GetField(name)?.GetValue(volume) ?? throw new InvalidOperationException("unreadable volume " + name);
            double weight = Convert.ToDouble(Read("weight"), CultureInfo.InvariantCulture);
            double blend = Convert.ToDouble(Read("blendDistance"), CultureInfo.InvariantCulture);
            double priority = Convert.ToDouble(Read("priority"), CultureInfo.InvariantCulture);
            bool global = (bool)(type.GetProperty("isGlobal")?.GetValue(volume) ?? throw new InvalidOperationException("unreadable volume global state"));
            if (!Position.Finite(weight) || !Position.Finite(blend) || !Position.Finite(priority)) throw new InvalidOperationException("invalid volume");
            snapshot.Environment.Add(new StreetProxyEnvironmentMetadata { Instance = volume.GetInstanceID(), Kind = "Volume",
                Position = Point(volume.transform.position), Enabled = volume is Behaviour behaviour && behaviour.enabled,
                Active = volume.gameObject.activeInHierarchy, IntensityOrWeight = weight, Range = blend,
                Detail = "global=" + global + "; priority=" + priority.ToString(CultureInfo.InvariantCulture) + "; profile values not read" });
        }
    }
}
