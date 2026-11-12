using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GleyTrafficSystem;
using HarmonyLib;
using UI;
using Helpers;
using PersistentCity.Core;
using UnityEngine;

namespace PersistentCity.Runtime
{
    internal readonly struct PhysicalRoadSample
    {
        public PhysicalRoadSample(Position position, Position directionTarget, int spawnNode, IReadOnlyList<int> remainingRoadNodes, string topologyId, IReadOnlyList<RoadBoundary> admissionBoundaries, double routeProgress)
        { AdmissionBoundaries = admissionBoundaries; RouteProgress = routeProgress; Position = position; DirectionTarget = directionTarget; SpawnNode = spawnNode; RemainingRoadNodes = remainingRoadNodes; TopologyId = topologyId; }
        public IReadOnlyList<RoadBoundary> AdmissionBoundaries { get; }
        public double RouteProgress { get; }
        public Position Position { get; }
        public Position DirectionTarget { get; }
        public int SpawnNode { get; }
        public IReadOnlyList<int> RemainingRoadNodes { get; }
        public string TopologyId { get; }
    }

    /// <summary>One native car model the mod can copy: its prefab, the root-to-front-axle offset and its size, read from a
    /// member of the native pool without activating, moving or keeping it.</summary>
    internal sealed class OwnCarModel
    {
        internal OwnCarModel(string name, GameObject prefab, Vector3 markerOffset, float length, float width, float height)
        { Name = name; Prefab = prefab; MarkerOffset = markerOffset; Length = length; Width = width; Height = height; }
        internal readonly string Name;
        internal readonly GameObject Prefab;
        internal readonly Vector3 MarkerOffset;
        internal readonly float Length, Width, Height;
        internal bool Refused;
    }

    /// <summary>A copy of a native car prefab with every script removed except the paint component. Its rigidbody is
    /// kinematic and its colliders are obstacles to native traffic and the player. Owned by the mod alone and reused
    /// through its own pool; no native pool slot, traffic index or vehicle save entry is involved.</summary>
    internal sealed class OwnCarInstance
    {
        internal OwnCarInstance(OwnCarModel model, GameObject root)
        {
            Model = model; Root = root;
            root.GetComponentsInChildren(true, Renderers);
            Features = root.GetComponentsInChildren<CarFeatures>(true);
        }
        internal readonly OwnCarModel Model;
        internal readonly GameObject Root;
        internal readonly List<Renderer> Renderers = new List<Renderer>();
        internal readonly CarFeatures[] Features;
        internal bool Live => Root != null;

        /// <summary>F2d Q3 fade: whether every renderer can show a base-colour alpha (<see cref="WalkerFollow.MaterialCanFade"/>).</summary>
        internal bool CanFade => canFade ??= ProbeFade();
        private bool? canFade;
        private bool ProbeFade()
        {
            bool any = false;
            foreach (var renderer in Renderers)
            {
                if (renderer == null) continue;
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null || material.shader == null) return false;
                    var facts = new MaterialFadeFacts(material.shader.name, material.renderQueue,
                        material.HasProperty(BaseColorId), material.HasProperty(ColorId),
                        material.HasProperty(SrcBlendId) ? material.GetFloat(SrcBlendId) : (float?)null,
                        material.HasProperty(DstBlendId) ? material.GetFloat(DstBlendId) : (float?)null,
                        material.HasProperty(SurfaceId) ? material.GetFloat(SurfaceId) : (float?)null,
                        material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"), material.IsKeywordEnabled("_ALPHABLEND_ON"),
                        material.IsKeywordEnabled("_ALPHATEST_ON"));
                    if (!WalkerFollow.MaterialCanFade(facts)) return false;
                    any = true;
                }
            }
            return any;
        }

        internal bool OnScreen
        {
            get
            {
                int frame = Time.frameCount;
                if (onScreenFrame == frame) return onScreen;
                onScreenFrame = frame;
                bool any = false, seen = false;
                foreach (var renderer in Renderers)
                {
                    if (renderer == null) continue;
                    any = true;
                    if (ScreenVisibility.Contains(renderer)) { seen = true; break; }
                }
                return onScreen = seen || !any && ScreenVisibility.Contains(null);
            }
        }
        private int onScreenFrame = -1;
        private bool onScreen;

        /// <summary>Scale the alpha of every renderer's base colour, starting from the colour it shows now (its property
        /// block's, else its material's). 1 puts each starting colour back, so the paint set at acquisition is kept.</summary>
        internal void SetFade(float alpha)
        {
            alpha = Mathf.Clamp01(alpha);
            if (alpha >= 1)
            {
                for (int i = 0; i < faded.Count; i++)
                {
                    var renderer = faded[i];
                    if (renderer == null) continue;
                    renderer.GetPropertyBlock(block);
                    block!.SetColor(originals[i].Id, originals[i].Colour);
                    renderer.SetPropertyBlock(block);
                }
                faded.Clear(); originals.Clear(); return;
            }
            if (!Live) return;
            block ??= new MaterialPropertyBlock();
            if (faded.Count == 0)
            {
                faded.AddRange(Renderers);
                foreach (var renderer in faded)
                {
                    var material = renderer != null ? renderer.sharedMaterial : null;
                    int id = material != null && material.HasProperty(BaseColorId) ? BaseColorId : ColorId;
                    Color colour = material != null && material.HasProperty(id) ? material.GetColor(id) : Color.white;
                    if (renderer != null)
                    {
                        renderer.GetPropertyBlock(block);
                        if (block.HasColor(id)) colour = block.GetColor(id);
                    }
                    originals.Add((id, colour));
                }
            }
            for (int i = 0; i < faded.Count; i++)
            {
                var renderer = faded[i];
                if (renderer == null) continue;
                var (id, colour) = originals[i];
                colour.a *= alpha;
                renderer.GetPropertyBlock(block);
                block.SetColor(id, colour);
                renderer.SetPropertyBlock(block);
            }
        }
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"), ColorId = Shader.PropertyToID("_Color"),
            SrcBlendId = Shader.PropertyToID("_SrcBlend"), DstBlendId = Shader.PropertyToID("_DstBlend"), SurfaceId = Shader.PropertyToID("_Surface");
        private readonly List<Renderer> faded = new List<Renderer>();
        private readonly List<(int Id, Color Colour)> originals = new List<(int, Color)>();
        private MaterialPropertyBlock? block;

        /// <summary>Put the root so the front axle stands on <paramref name="front"/>, facing <paramref name="rotation"/>.</summary>
        internal void Place(Position front, Quaternion rotation)
        {
            var forward = rotation * Vector3.forward;
            var root = PhysicalVehicleReference.Root(front, PhysicalVehicles.Point(Model.MarkerOffset), PhysicalVehicles.Point(forward));
            Root.transform.SetPositionAndRotation(PhysicalVehicles.Vector(root), rotation);
        }
    }

    /// <summary>GR-9 (G7): a resident's car on the road, an own kinematic car. It drives the committed road nodes one
    /// segment at a time under the follower's command limit and reports each corner it reaches, as the borrowed native
    /// car's reached marker did.</summary>
    internal sealed class PhysicalVehicleBody
    {
        internal PhysicalVehicleBody(string id, string citizenId, long generation, OwnCarInstance instance, string topology, List<int> path,
            Position from, Position to, double along, Quaternion rotation)
        {
            Id = id; CitizenId = citizenId; Generation = generation; Instance = instance; Topology = topology; Path = path;
            From = from; To = to; Length = Math.Max(0, Position.Distance(from, to)); Along = along; Rotation = rotation; Yaw = rotation.eulerAngles.y;
        }
        internal readonly OwnCarInstance Instance;
        internal readonly string Topology;
        /// <summary>The committed road nodes still ahead; Path[0] is the corner at the end of the current segment.</summary>
        internal readonly List<int> Path;
        internal Position From, To;
        internal double Length, Along, SpeedValue;
        internal Quaternion Rotation;
        internal double Yaw;
        internal bool Released;
        private readonly Queue<Position> reachedCorners = new Queue<Position>();
        private int lastReachedNode = -1;
        internal void RecordReached(int node, Position corner)
        {
            if (node == lastReachedNode) return;
            if (reachedCorners.Count < 512) reachedCorners.Enqueue(corner);
            lastReachedNode = node;
        }
        public bool TryPeekReached(Position currentTo, out Position measuredMarker)
        {
            measuredMarker = default;
            if (!Valid || reachedCorners.Count == 0 || Position.Distance(reachedCorners.Peek(), currentTo) > .01) return false;
            measuredMarker = reachedCorners.Peek(); return measuredMarker.IsFinite;
        }
        public bool ConsumeReached(Position currentTo)
        {
            if (!TryPeekReached(currentTo, out _)) return false;
            reachedCorners.Dequeue(); return true;
        }
        public string Id { get; }
        public string CitizenId { get; }
        public long Generation { get; }
        public bool Valid => !Released && Instance.Live && Instance.Root.activeInHierarchy;
        /// <summary>The front axle, on the current segment.</summary>
        public Position Position => Length <= 1e-9 ? To : Position.Lerp(From, To, Math.Min(1, Along / Length));
        public bool RouteFinished { get; internal set; }
        /// <summary>F2d: speed along the road in metres per second; 0 when not valid.</summary>
        public double Speed => Valid ? SpeedValue : 0;
        public bool OnScreen => Valid && Instance.OnScreen;
        public bool CanFade => Valid && Instance.CanFade;
        public void SetFade(float alpha) { if (Instance.Live) Instance.SetFade(alpha); }
    }

    /// <summary>A parked car shown as an object at its saved curb position.</summary>
    internal sealed class ParkedCarBody
    {
        internal ParkedCarBody(string carId, OwnCarInstance instance, Position at) { CarId = carId; Instance = instance; At = at; }
        internal readonly string CarId;
        internal readonly OwnCarInstance Instance;
        internal readonly Position At;
        internal double FadeIn, FadeOut = double.NaN;
    }

    /// <summary>
    /// GR-9 (G7): the mod's own cars. Nearby residents drive own kinematic copies of native car models; their parked cars
    /// stand as objects. No native car is borrowed and no Harmony patch is installed. Ordinary native random traffic is
    /// held back with Gley's public group-share call (groups made only of ordinary cars get no share; groups with taxis or
    /// service cars keep theirs) and leftover ordinary cars of those groups are removed off screen; everything is put
    /// back on disable.
    /// </summary>
    internal sealed class PhysicalVehicles : IDisposable
    {
        private readonly Func<string, int, Waypoint?> resolve;
        private readonly Dictionary<string, PhysicalVehicleBody> bodies = new Dictionary<string, PhysicalVehicleBody>(StringComparer.Ordinal);
        private readonly Dictionary<string, ParkedCarBody> parked = new Dictionary<string, ParkedCarBody>(StringComparer.Ordinal);
        private readonly OwnCarPool<OwnCarInstance> pool;
        private readonly int maximum;
        private GameObject? holder;
        private Dictionary<string, OwnCarModel>? models;
        private string[] modelNames = Array.Empty<string>();
        private long generation;
        private string recoveryError = "";
        private bool active, disposed;
        private int retireCursor;
        // Native ambient hold-back: what native set, what this adapter applied, and the native density when we had to lower it.
        private int[]? nativeShares, appliedShares;
        private int? nativeDensity;
        private readonly HashSet<VehicleGroupType> heldGroups = new HashSet<VehicleGroupType>();
        private long retired, shareReapplied;
        private static readonly FieldInfo? TrafficVehiclesField = AccessTools.Field(typeof(TrafficManager), "trafficVehicles");
        private static readonly FieldInfo? SharesField = AccessTools.Field(typeof(TrafficVehicles), "currentVehicleGroupTypePercentages");
        private static readonly FieldInfo? DensityManagerField = AccessTools.Field(typeof(TrafficManager), "densityManager");
        private static readonly FieldInfo? DensityField = AccessTools.Field(typeof(DensityManager), "maxNrOfVehicles");
        internal Func<PhysicalVehicleBody, PhysicalTrafficCommandLimit>? CommandLimit { get; set; }
        internal Func<PhysicalVehicleBody, int, bool>? NativeTargetPermission { get; set; }

        public PhysicalVehicles(Func<string, int, Waypoint?> resolveNode, int max = 24)
        {
            resolve = resolveNode; maximum = Math.Max(1, Math.Min(64, max));
            // Moving plus parked cars; at most two new copies a frame (Instantiate and script removal are not cheap).
            pool = new OwnCarPool<OwnCarInstance>(maximum * 2, i => i.Live, i => UnityEngine.Object.Destroy(i.Root));
        }
        /// <summary>The own-car holder: colliders under it are the mod's own cars, which the route planner ignores.</summary>
        internal static Transform? OwnHolder { get; private set; }
        internal static bool IsOwnCar(Collider? collider) => collider != null && OwnHolder != null && collider.transform.IsChildOf(OwnHolder);

        public bool Ready => !disposed && TrafficVehiclesField != null && SharesField != null && SharesField.FieldType == typeof(int[])
            && DensityManagerField != null && DensityField != null && DensityField.FieldType == typeof(int);
        public bool Active => active && !disposed;
        public string LastAdmissionStage { get; private set; } = "";
        public string LastAdmissionResident { get; private set; } = "";
        public string LastAdmissionError { get; private set; } = "";
        public string LastError => recoveryError.Length != 0 ? recoveryError : Ready ? "" : "Native traffic share members are missing.";
        public string OwnershipDiagnostics => "ownCars=" + bodies.Count + ", parkedCars=" + parked.Count + ", instances=" + pool.Count
            + ", heldGroups=" + string.Join("|", heldGroups.Select(g => g.ToString()).OrderBy(g => g, StringComparer.Ordinal))
            + ", retired=" + retired + ", shareReapplied=" + shareReapplied;

        public bool Activate()
        {
            if (!Ready || disposed || !TrafficManager.IsInitialized || !TrafficManager.HasInstance) return false;
            if (active) return true;
            var pool = Manager.GetVehicleList();
            var catalogue = new Dictionary<string, OwnCarModel>(StringComparer.Ordinal);
            foreach (var vehicle in pool)
            {
                if (!Ordinary(vehicle) || catalogue.ContainsKey(vehicle.prefab.name)) continue;
                var front = vehicle.GetFrontAxle();
                if (front == null || !front.IsChildOf(vehicle.transform)) continue;
                var offset = Quaternion.Inverse(vehicle.transform.rotation) * (front.position - vehicle.transform.position);
                if (!Point(offset).IsFinite || offset.sqrMagnitude > 400f || !Size(vehicle.length) || !Size(vehicle.colliderWidth) || !Size(vehicle.coliderHeight)) continue;
                catalogue.Add(vehicle.prefab.name, new OwnCarModel(vehicle.prefab.name, vehicle.prefab, offset, vehicle.length, vehicle.colliderWidth, vehicle.coliderHeight));
            }
            if (catalogue.Count == 0) return false;
            models = catalogue;
            modelNames = catalogue.Keys.OrderBy(n => n, StringComparer.Ordinal).ToArray();
            holder = new GameObject("PersistentCity own cars");
            UnityEngine.Object.DontDestroyOnLoad(holder);
            OwnHolder = holder.transform;
            HoldNativeAmbient(pool);
            active = true; return true;
        }
        private static bool Size(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0;

        // Groups whose every pool member is an ordinary car get share 0; a group with a taxi, service or foreign car keeps its share.
        private void HoldNativeAmbient(List<VehicleComponent> pool)
        {
            var manager = TrafficManager.Instance;
            var traffic = TrafficVehiclesField!.GetValue(manager) as TrafficVehicles;
            var shares = traffic == null ? null : SharesField!.GetValue(traffic) as int[];
            if (shares == null) return;
            var groups = new Dictionary<VehicleGroupType, bool>();
            foreach (var vehicle in pool)
            {
                if (vehicle == null) continue;
                bool ordinary = Ordinary(vehicle) && !IsTaxi(vehicle);
                groups[vehicle.vehicleGroupType] = groups.TryGetValue(vehicle.vehicleGroupType, out bool all) ? all && ordinary : ordinary;
            }
            heldGroups.Clear();
            var applied = (int[])shares.Clone();
            for (int i = 0; i < applied.Length; i++)
                if (groups.TryGetValue((VehicleGroupType)i, out bool ordinaryOnly) && ordinaryOnly && applied[i] > 0)
                { applied[i] = 0; heldGroups.Add((VehicleGroupType)i); }
            nativeShares = (int[])shares.Clone();
            if (applied.Sum() > 0)
            {
                if (heldGroups.Count != 0) manager.UpdateVehicleGroupTypePercentages(applied);
                appliedShares = applied;
            }
            else
            {
                // Every group is ordinary: an empty group pool would leave native nothing to draw. Lower the density instead.
                appliedShares = null;
                if (DensityManagerField!.GetValue(manager) is DensityManager density)
                { nativeDensity = (int)DensityField!.GetValue(density)!; Manager.SetTrafficDensity(0); }
            }
        }

        private void KeepNativeAmbientHeld()
        {
            if (appliedShares == null || Time.frameCount % 60 != 0) return;
            var traffic = TrafficVehiclesField!.GetValue(TrafficManager.Instance) as TrafficVehicles;
            if (traffic == null || !(SharesField!.GetValue(traffic) is int[] current) || current.SequenceEqual(appliedShares)) return;
            // Native set new shares (a time of day or settings change): keep them as native's, hold the ordinary groups again.
            nativeShares = (int[])current.Clone();
            var applied = (int[])current.Clone();
            foreach (var group in heldGroups) if ((int)group < applied.Length) applied[(int)group] = 0;
            if (applied.Sum() <= 0) return;
            TrafficManager.Instance.UpdateVehicleGroupTypePercentages(applied);
            appliedShares = applied; shareReapplied++;
        }

        private void ReleaseNativeAmbient()
        {
            if (!TrafficManager.IsInitialized || !TrafficManager.HasInstance) { nativeShares = appliedShares = null; nativeDensity = null; return; }
            try
            {
                if (appliedShares != null && nativeShares != null && heldGroups.Count != 0) TrafficManager.Instance.UpdateVehicleGroupTypePercentages(nativeShares);
                if (nativeDensity.HasValue) Manager.SetTrafficDensity(nativeDensity.Value);
            }
            finally { nativeShares = appliedShares = null; nativeDensity = null; heldGroups.Clear(); }
        }

        internal static bool IsTaxi(VehicleComponent vehicle) => vehicle != null && vehicle.GetComponentInChildren<TaxiController>(true) != null;
        internal static bool Ordinary(VehicleComponent vehicle) => vehicle != null && vehicle.vehicleType == VehicleTypes.Car && RandomAmbient(vehicle);
        internal static bool RandomAmbient(VehicleComponent vehicle)
        {
            if (vehicle == null || vehicle.prefab == null) return false;
            // Component ownership, not model names: a service or taxi model is never copied or held back.
            return vehicle.GetComponentInChildren<TaxiController>(true) == null
                && vehicle.GetComponentInChildren<PrivateDriverVehicle>(true) == null
                && vehicle.GetComponentInChildren<VehicleController>(true) == null
                && VehicleScriptOwnership.NativeOnly(vehicle);
        }

        public void Disable()
        {
            active = false;
            Exception? failure = null;
            foreach (var body in bodies.Values.ToArray())
                try { Release(body); } catch (Exception error) { failure = failure ?? error; }
            foreach (var car in parked.Values.ToArray())
                try { ReleaseParked(car); } catch (Exception error) { failure = failure ?? error; }
            try { ReleaseNativeAmbient(); } catch (Exception error) { failure = failure ?? error; }
            if (failure != null) throw new InvalidOperationException("One or more own cars could not be released.", failure);
        }

        internal static Position Point(Vector3 p) => new Position(p.x, p.y, p.z);
        internal static Vector3 Vector(Position p) => new Vector3((float)p.X, (float)p.Y, (float)p.Z);
        private static bool SceneReady => (!BuildingManager.IsInsideBuilding || NativeInteriorCityView.Active) && Time.timeScale > 0;

        public bool TryAcquire(CitizenState citizen, PhysicalRoadSample sample, out PhysicalVehicleBody body)
        {
            body = null!;
            LastAdmissionResident = citizen.Identity.Id; LastAdmissionError = "";
            LastAdmissionStage = "availability";
            if (!Active || !SceneReady || models == null || citizen.Car == null || bodies.Count >= maximum || !sample.Position.IsFinite
                || !sample.DirectionTarget.IsFinite || sample.RemainingRoadNodes == null || sample.RemainingRoadNodes.Count == 0 || sample.RemainingRoadNodes.Count > 512) return false;
            if (bodies.ContainsKey(citizen.Car.Id)) return false;
            LastAdmissionStage = "committed road validation";
            var path = new List<int>(sample.RemainingRoadNodes);
            Waypoint? first = null;
            for (int i = 0; i < path.Count; i++)
            {
                var node = resolve(sample.TopologyId, path[i]);
                if (node == null || node.temporaryDisabled) return false;
                if (i == 0) first = node;
                else { var previous = resolve(sample.TopologyId, path[i - 1]); if (previous?.neighbors == null || !previous.neighbors.Contains(path[i])) return false; }
            }
            var spawn = resolve(sample.TopologyId, sample.SpawnNode);
            if (spawn == null || spawn.neighbors == null || !spawn.neighbors.Contains(path[0])) return false;
            var segment = first!.position - spawn.position;
            var offset = Vector(sample.Position) - spawn.position;
            if (segment.sqrMagnitude < 0.001f) return false;
            float fraction = Vector3.Dot(offset, segment) / segment.sqrMagnitude;
            if (fraction < -0.001f || fraction > 1.001f || (offset - segment * fraction).sqrMagnitude > 0.25f) return false;
            LastAdmissionStage = "model catalogue";
            // F2d: derived from the stable car ID (or a saved assignment) and never written back (E1).
            if (!CarAppearance.TryModel(citizen.Car, () => modelNames, out string modelId) || !models.TryGetValue(modelId, out var model) || model.Refused) return false;
            LastAdmissionStage = "junction rear clearance";
            if (!PhysicalRoadAdmission.Allows(sample.AdmissionBoundaries, sample.RouteProgress, model.Length)) return false;
            LastAdmissionStage = "body geometry";
            var direction = first.position - spawn.position; direction.y = 0;
            if (direction.sqrMagnitude < 0.001f) return false;
            var rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            var markerOffset = model.MarkerOffset;
            var rootPosition = Vector(PhysicalVehicleReference.Root(sample.Position, Point(markerOffset), Point(direction)));
            var clearance = new Vector3(Math.Max(0.5f, model.Width + Math.Abs(markerOffset.x)),
                Math.Max(0.3f, model.Height / 2f - 0.15f), Math.Max(1f, model.Length + Math.Abs(markerOffset.z)));
            int mask = LayerMask.GetMask("Vehicles", "AiVehicles", "PlayerVehicles", "Obstacles", "Default");
            LastAdmissionStage = "spawn obstruction";
            // The resident's own parked car is not an obstruction: it is out of the check, and it leaves (fading where
            // seen) only once the driving car stands on the road.
            parked.TryGetValue(citizen.Car.Id, out var standing);
            bool blocked;
            if (standing != null) standing.Instance.Root.SetActive(false);
            try
            {
                blocked = mask == 0 || Physics.CheckBox(rootPosition + Vector3.up * (model.Height / 2f + 0.2f),
                    clearance, rotation, mask, QueryTriggerInteraction.Ignore);
            }
            finally { if (standing != null && standing.Instance.Live) standing.Instance.Root.SetActive(true); }
            if (blocked) return false;
            LastAdmissionStage = "own car instance";
            OwnCarInstance? instance = null;
            try
            {
                instance = Take(model, CarAppearance.Paint(citizen.Car));
                if (instance == null) return false;
                double along = Math.Max(0, Math.Min(1, fraction)) * Math.Sqrt(segment.sqrMagnitude);
                body = new PhysicalVehicleBody(citizen.Car.Id, citizen.Identity.Id, ++generation, instance, sample.TopologyId, path,
                    Point(spawn.position), Point(first.position), along, rotation);
                instance.Place(body.Position, rotation);
                instance.Root.SetActive(true);
                bodies.Add(body.Id, body);
                if (standing != null) LeaveParked(standing);
                LastAdmissionStage = "admitted";
                return true;
            }
            catch (Exception error)
            {
                LastAdmissionError = error.GetType().Name + ": " + error.Message;
                if (body != null) Release(body);
                else if (instance != null) Return(instance);
                body = null!; return false;
            }
        }

        /// <summary>F2d Decision 6: whether the native waypoint at the car's next corner is a stop point now (its light is
        /// red); null when unknown. Read-only; no signal phase is changed.</summary>
        internal bool? NativeRed(PhysicalVehicleBody body)
        {
            if (!Active || body == null || !body.Valid || body.Path.Count == 0) return null;
            var target = resolve(body.Topology, body.Path[0]);
            return target == null ? (bool?)null : target.stop;
        }

        public void Release(PhysicalVehicleBody body)
        {
            if (body == null || body.Released) return;
            body.Released = true;
            if (bodies.TryGetValue(body.Id, out var registered) && ReferenceEquals(registered, body)) bodies.Remove(body.Id);
            Return(body.Instance);
        }

        private void ReleaseParked(ParkedCarBody car)
        {
            if (parked.TryGetValue(car.CarId, out var registered) && ReferenceEquals(registered, car)) parked.Remove(car.CarId);
            Return(car.Instance);
        }

        // Own pool (OwnCarPool): an idle copy of the model, else a new copy within capacity and this frame's budget,
        // evicting another model's idle copy at capacity.
        private OwnCarInstance? Take(OwnCarModel model, int paintSeed)
        {
            if (holder == null) return null;
            var instance = pool.Take(model.Name, Time.frameCount, () => Create(model));
            if (instance != null) Paint(instance, paintSeed);
            return instance;
        }

        private void Return(OwnCarInstance instance)
        {
            if (instance == null || !instance.Live) return;
            instance.SetFade(1);
            instance.Root.SetActive(false);
            pool.Return(instance.Model.Name, instance);
        }

        private static void Paint(OwnCarInstance instance, int paintSeed)
        {
            var colors = InstanceBehavior<GlobalReferences>.Instance.vehicleColors;
            var palette = colors.Where(c => c != null).OrderBy(c => c.name, StringComparer.Ordinal).ToArray();
            if (palette.Length == 0) return;
            foreach (var features in instance.Features) if (features != null) features.SetColor(palette[(uint)paintSeed % (uint)palette.Length]);
        }

        private OwnCarInstance? Create(OwnCarModel model)
        {
            // Copy beneath an inactive parent so no prefab script runs Awake before it is removed.
            var staging = new GameObject("PersistentCity car staging");
            staging.SetActive(false); staging.transform.SetParent(holder!.transform, false);
            GameObject? root = null;
            try
            {
                root = UnityEngine.Object.Instantiate(model.Prefab, staging.transform);
                if (!Strip(root)) { model.Refused = true; UnityEngine.Object.Destroy(root); return null; }
                root.name = "PersistentCity car " + model.Name;
                root.SetActive(false);
                root.transform.SetParent(holder.transform, false);
                return new OwnCarInstance(model, root);
            }
            catch
            {
                if (root != null) UnityEngine.Object.Destroy(root);
                model.Refused = true; return null;
            }
            finally { UnityEngine.Object.Destroy(staging); }
        }

        /// <summary>Remove every script but the paint component; make bodies kinematic, triggers off, engine sound and
        /// NavMesh carving off; colliders move to the obstacle layer so native cars brake for them without looking up a
        /// traffic index. False when a script could not be removed (the model is then never copied again).</summary>
        private static bool Strip(GameObject root)
        {
            for (int pass = 0; pass < 4; pass++)
            {
                var scripts = root.GetComponentsInChildren<MonoBehaviour>(true);
                bool left = false;
                for (int i = scripts.Length - 1; i >= 0; i--)
                {
                    if (scripts[i] == null || scripts[i] is CarFeatures) continue;
                    UnityEngine.Object.DestroyImmediate(scripts[i]);
                    left = true;
                }
                if (!left) break;
            }
            foreach (var script in root.GetComponentsInChildren<MonoBehaviour>(true)) if (script != null && !(script is CarFeatures)) return false;
            foreach (var body in root.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.useGravity = false; body.detectCollisions = true; }
            foreach (var collider in root.GetComponentsInChildren<Collider>(true)) if (collider.isTrigger) collider.enabled = false;
            foreach (var obstacle in root.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true)) obstacle.enabled = false;
            foreach (var behaviour in root.GetComponentsInChildren<Behaviour>(true))
                if (behaviour != null && behaviour.GetType().Name == "AudioSource") behaviour.enabled = false;
            // Only collider objects change layer; renderers keep the prefab's layers, so culling and lighting stay native.
            int layer = LayerMask.NameToLayer("Obstacles");
            if (layer >= 0) foreach (var collider in root.GetComponentsInChildren<Collider>(true)) collider.gameObject.layer = layer;
            return true;
        }

        internal void AssertHealthy()
        {
            if (recoveryError.Length != 0) throw new InvalidOperationException("Own car adapter failed: " + LastError);
        }

        /// <summary>Drive every own car one frame under its command limit, fade parked cars, keep native ambient traffic
        /// held back and retire one leftover ordinary native car off screen.</summary>
        public void Update()
        {
            if (!Active) return;
            AssertHealthy();
            float seconds = Time.deltaTime;
            foreach (var body in bodies.Values.ToArray())
            {
                if (!body.Valid) { Release(body); continue; }
                try { Drive(body, seconds); }
                catch (Exception error)
                {
                    if (recoveryError.Length == 0) recoveryError = "Own car drive failed: " + error.Message;
                    throw;
                }
            }
            FadeParked(seconds);
            if (!TrafficManager.IsInitialized || !TrafficManager.HasInstance) return;
            KeepNativeAmbientHeld();
            if (heldGroups.Count == 0 && !nativeDensity.HasValue) return;
            var pool = Manager.GetVehicleList(); if (pool.Count == 0) return;
            var vehicle = pool[retireCursor++ % pool.Count];
            if (vehicle != null && vehicle.gameObject.activeInHierarchy && vehicle.presetPath == null
                && (nativeDensity.HasValue || heldGroups.Contains(vehicle.vehicleGroupType)) && Ordinary(vehicle) && !IsTaxi(vehicle)
                && !vehicle.GetComponentsInChildren<Renderer>().Any(r => r.isVisible))
            { Manager.RemoveVehicle(vehicle.GetIndex()); retired++; }
        }

        private void Drive(PhysicalVehicleBody body, float seconds)
        {
            if (CommandLimit == null || body.RouteFinished) return;
            var command = CommandLimit(body);
            // The own car's own safety on top of the kernel's limit: an obstacle ahead, a native red at its corner.
            var corner = body.Path.Count > 0 ? resolve(body.Topology, body.Path[0]) : null;
            bool red = corner != null && corner.stop;
            var limit = OwnCarMotion.Safe(command, command.HoldPosition ? null : ObstacleAhead(body), red, body.Length - body.Along);
            var step = OwnCarMotion.Step(body.Along, body.Length, body.SpeedValue, limit, seconds);
            body.Along = step.Along; body.SpeedValue = step.Speed;
            if (step.AtCorner && !red && body.Path.Count > 0)
            {
                int node = body.Path[0];
                if (NativeTargetPermission != null && NativeTargetPermission(body, node))
                {
                    body.RecordReached(node, body.To);
                    body.Path.RemoveAt(0);
                    if (body.Path.Count == 0) body.RouteFinished = true;
                    else
                    {
                        var next = resolve(body.Topology, body.Path[0]);
                        if (next == null) { body.RouteFinished = true; }
                        else
                        {
                            body.From = body.To; body.To = Point(next.position);
                            body.Length = Position.Distance(body.From, body.To); body.Along = 0;
                        }
                    }
                }
            }
            // A standing car keeps its pose: the root pivots on the front axle, so turning in place would move it.
            if (step.Moved <= 1e-9) return;
            var heading = Vector(body.To) - Vector(body.From); heading.y = 0;
            if (heading.sqrMagnitude > 0.0001f)
            {
                double target = Quaternion.LookRotation(heading.normalized, Vector3.up).eulerAngles.y;
                body.Yaw = OwnCarMotion.Yaw(body.Yaw, target, seconds, hold: false);
                body.Rotation = Quaternion.Euler(0, (float)body.Yaw, 0);
            }
            body.Instance.Place(body.Position, body.Rotation);
        }

        private readonly RaycastHit[] hits = new RaycastHit[16];
        private static int obstacleMask = -1;
        /// <summary>Distance from the front axle to the nearest vehicle, player or walker ahead within the braking
        /// distance, ignoring the car's own colliders; null when clear.</summary>
        private double? ObstacleAhead(PhysicalVehicleBody body)
        {
            if (obstacleMask < 0) obstacleMask = LayerMask.GetMask("Vehicles", "AiVehicles", "PlayerVehicles", "Player", "Obstacles", "Pedestrians", "Pedestrian", "Characters");
            if (obstacleMask == 0) return null;
            var model = body.Instance.Model;
            var forward = body.Rotation * Vector3.forward;
            var half = new Vector3(Math.Max(0.4f, model.Width / 2f), Math.Max(0.3f, model.Height / 2f - 0.15f), 0.25f);
            var centre = Vector(body.Position) + Vector3.up * (model.Height / 2f + 0.2f);
            float reach = (float)OwnCarMotion.LookAheadMetres(body.SpeedValue);
            int count = Physics.BoxCastNonAlloc(centre, half, forward, hits, body.Rotation, reach, obstacleMask, QueryTriggerInteraction.Ignore);
            double? nearest = null;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(body.Instance.Root.transform)) continue;
                if (!nearest.HasValue || hit.distance < nearest.Value) nearest = hit.distance;
            }
            return nearest;
        }

        /// <summary>GR-9: show these parked cars (nearest first, at most the vehicle cap). A car no longer wanted fades
        /// out and returns to the pool; a new one fades in. A car that drives is never shown parked as well.</summary>
        internal void ShowParked(IReadOnlyList<ParkedCar> wanted, Func<string, CarState?> car)
        {
            if (!Active || models == null) return;
            var keep = new HashSet<string>(StringComparer.Ordinal);
            int count = 0;
            foreach (var entry in wanted)
            {
                if (count >= maximum) break;
                if (bodies.ContainsKey(entry.CarId)) continue;
                keep.Add(entry.CarId); count++;
                if (parked.TryGetValue(entry.CarId, out var shown))
                {
                    if (Position.Distance(shown.At, entry.At) <= .01) { shown.FadeOut = double.NaN; continue; }
                    ReleaseParked(shown);
                }
                var state = car(entry.CarId);
                if (state == null || !SceneReady || !CarAppearance.TryModel(state, () => modelNames, out string modelId)
                    || !models.TryGetValue(modelId, out var model) || model.Refused) continue;
                var rotation = Quaternion.Euler(0, ParkedYaw(entry), 0);
                // A curb point is not a reserved space: another car (parked, driving, the player's) may already stand
                // there. This car stays hidden until the space is free; its saved position is unchanged.
                if (!ParkedClear(model, entry.At, rotation)) continue;
                var instance = Take(model, CarAppearance.Paint(state));
                if (instance == null) continue;
                // The model's root, not its front axle, stands on the curb point.
                instance.Root.transform.SetPositionAndRotation(Vector(entry.At), rotation);
                var body = new ParkedCarBody(entry.CarId, instance, entry.At);
                if (instance.CanFade) instance.SetFade(0); else body.FadeIn = BodyFade.FadeInSeconds;
                instance.Root.SetActive(true);
                parked.Add(entry.CarId, body);
            }
            foreach (var shown in parked.Values.ToArray())
                if (!keep.Contains(shown.CarId) && double.IsNaN(shown.FadeOut))
                {
                    if (shown.Instance.CanFade && shown.Instance.OnScreen) shown.FadeOut = 0;
                    else ReleaseParked(shown);
                }
        }

        private static bool ParkedClear(OwnCarModel model, Position at, Quaternion rotation)
        {
            int mask = LayerMask.GetMask("Vehicles", "AiVehicles", "PlayerVehicles", "Obstacles", "Player");
            var half = new Vector3(Math.Max(0.5f, model.Width / 2f + 0.2f), Math.Max(0.3f, model.Height / 2f - 0.15f), Math.Max(1f, model.Length / 2f + 0.3f));
            return mask != 0 && !Physics.CheckBox(Vector(at) + Vector3.up * (model.Height / 2f + 0.2f), half, rotation, mask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>A parked car whose owner now drives: it fades out where seen, else returns to the pool at once.</summary>
        private void LeaveParked(ParkedCarBody car)
        {
            if (car.Instance.Live && car.Instance.CanFade && car.Instance.OnScreen) { if (double.IsNaN(car.FadeOut)) car.FadeOut = 0; }
            else ReleaseParked(car);
        }

        private static float ParkedYaw(ParkedCar car)
        {
            // Along the nearest road where native knows one; else a stable angle from the car ID.
            try
            {
                if (TrafficManager.IsInitialized && TrafficManager.HasInstance
                    && TrafficManager.Instance.TryGetClosestRoadYaw(Vector(car.At), Vector3.forward, 12f, out float yaw)
                    && !float.IsNaN(yaw) && !float.IsInfinity(yaw)) return yaw;
            }
            catch (Exception) { }
            return CarAppearance.Hash(car.CarId + ":parked") % 360u;
        }

        private void FadeParked(float seconds)
        {
            foreach (var car in parked.Values.ToArray())
            {
                if (!car.Instance.Live) { parked.Remove(car.CarId); continue; }
                if (!double.IsNaN(car.FadeOut))
                {
                    car.FadeOut += seconds;
                    float alpha = BodyFade.Out(car.FadeOut);
                    if (alpha <= 0) { ReleaseParked(car); continue; }
                    car.Instance.SetFade(alpha);
                }
                else if (car.FadeIn < BodyFade.FadeInSeconds)
                {
                    car.FadeIn += seconds;
                    car.Instance.SetFade(BodyFade.In(car.FadeIn));
                }
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            try { Disable(); }
            finally
            {
                disposed = true;
                pool.Clear();
                if (holder != null && ReferenceEquals(OwnHolder, holder.transform)) OwnHolder = null;
                if (holder != null) UnityEngine.Object.Destroy(holder);
                holder = null;
            }
        }
    }
}
