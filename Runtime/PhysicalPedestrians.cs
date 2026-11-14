using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BigAmbitions.Characters;
using BigAmbitions.Characters.Appearance;
using Extensions;
using Helpers;
using Character;
using UI;
using UI.Load;
using Newtonsoft.Json;
using PersistentCity.Core;
using UnityEngine;
using UnityEngine.AI;

namespace PersistentCity.Runtime
{
    internal sealed class PhysicalPedestrians : IDisposable
    {
        private readonly Transform parent;
        private readonly int maximum;
        private readonly List<ThirdPersonCharacter> all = new List<ThirdPersonCharacter>();
        private readonly DressedBodyPool<ThirdPersonCharacter> idle = new DressedBodyPool<ThirdPersonCharacter>();
        public long AppearanceBuilds { get; private set; }
        public long AppearanceReuses { get; private set; }
        private readonly Dictionary<string, PhysicalPedestrianBody> active = new Dictionary<string, PhysicalPedestrianBody>(StringComparer.Ordinal);
        private long generation;
        private bool disposed;

        public PhysicalPedestrians(Transform parent, int maximum)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (maximum < 1 || maximum > 256) throw new ArgumentOutOfRangeException(nameof(maximum));
            this.parent = parent; this.maximum = maximum;
            NavigationHook.Attach(this);
        }

        public Action? BeforeNavigation { get; set; }

        public bool TryAcquire(CitizenIdentity identity, Position position, out PhysicalPedestrianBody? body)
        {
            body = null;
            if (disposed || identity == null || string.IsNullOrEmpty(identity.Id) || identity.AppearanceSeed == 0
                || active.ContainsKey(identity.Id) || !PhysicalPedestrianBody.Fits(position)) return false;
            var filter = WalkingRoutes.DryFilter;
            if (!NavMesh.SamplePosition(PhysicalPedestrianBody.Vector(position), out NavMeshHit hit, 0.05f, filter)
                || Position.Distance(position, PhysicalPedestrianBody.Point(hit.position)) > 0.05) return false;
            ThirdPersonCharacter? character = null;
            UnityEngine.Random.State dice = UnityEngine.Random.state;
            try
            {
                int daysPerYear = SaveGameManager.Current?.gameVariables.daysPerYear ?? 60;
                if (daysPerYear < 8) daysPerYear = 60;
                var dressing = new PersonDressing(identity, daysPerYear);
                if (idle.TryTake(dressing, out var pooled, out bool dressed)) character = pooled;
                else
                {
                    if (all.Count >= maximum) return false;
                    var staging = new GameObject("PersistentCity character staging");
                    staging.SetActive(false); staging.transform.SetParent(parent, false);
                    try
                    {
                        GameObject prefab = PrefabHelper.LoadPrefabAssetByName("Characters/HumanDefinitionLow");
                        if (prefab == null || prefab.GetComponent<ThirdPersonCharacter>() == null) return false;
                        GameObject instance = UnityEngine.Object.Instantiate(prefab, staging.transform);
                        character = instance.GetComponent<ThirdPersonCharacter>();
                        if (character == null || character.navmeshAgent == null || character.capsuleCollider == null
                            || character.appearanceSetter == null || character.animator == null || character.animator.runtimeAnimatorController == null
                            || instance.GetComponentInChildren<Streets.Pedestrians.Pedestrian>(true) != null)
                        {
                            UnityEngine.Object.Destroy(instance); character = null; return false;
                        }
                        character.gameObject.SetActive(false);
                        character.isPlayer = false; character.updatePlayerPos = false;
                        character.transform.SetParent(parent, false);
                        all.Add(character);
                    }
                    finally { UnityEngine.Object.Destroy(staging); }
                }
                character.isPlayer = false; character.updatePlayerPos = false;
                character.navmeshAgent.enabled = false;
                character.transform.position = hit.position;
                character.navmeshAgent.agentTypeID = filter.agentTypeID;
                character.navmeshAgent.areaMask = filter.areaMask;
                character.navmeshAgent.autoRepath = false;
                character.navmeshAgent.autoTraverseOffMeshLink = false;
                character.navmeshAgent.updatePosition = true;
                character.navmeshAgent.updateRotation = true;
                character.navmeshAgent.stoppingDistance = 0.02f;
                character.capsuleCollider.enabled = true;
                if (character.characterRigidbody != null) character.characterRigidbody.isKinematic = true;
                if (!dressed) character.gameObject.name = "PersistentCity " + identity.Id;
                character.gameObject.SetActive(true);
                character.navmeshAgent.enabled = true;
                if (!character.navmeshAgent.Warp(hit.position) || !character.navmeshAgent.isOnNavMesh) throw new InvalidOperationException("NPC warp failed");
                character.navmeshAgent.ResetPath(); character.navmeshAgent.isStopped = true;
                character.ResetAnimator();
                var appearance = character.appearanceSetter;
                if (dressed)
                {
                    if (appearance.data?.elements == null || appearance.data.elements.Count == 0) throw new InvalidOperationException("Empty appearance");
                    AppearanceReuses++;
                }
                else Dress(appearance, identity, daysPerYear);
                character.ToggleVisibility(true, includePhysics: false);
                character.OnVisibleChanged(true);
                character.animator.enabled = true;
                if (appearance.lastMergedMeshRenderer != null) appearance.lastMergedMeshRenderer.enabled = true;
                if (character.shadowCaster != null) character.shadowCaster.enabled = true;
                body = new PhysicalPedestrianBody(character, identity.Id, checked(++generation)) { Dressing = dressing };
                var relay = character.capsuleCollider.gameObject.GetComponent<PhysicalPedestrianCrosswalk>();
                if (relay == null) relay = character.capsuleCollider.gameObject.AddComponent<PhysicalPedestrianCrosswalk>();
                relay.Body = body;
                active.Add(identity.Id, body);
                return true;
            }
            catch
            {
                if (character != null) DestroyOwned(character);
                body = null; return false;
            }
            finally { UnityEngine.Random.state = dice; }
        }

        private void Dress(AppearanceSetter appearance, CitizenIdentity identity, int daysPerYear)
        {
            PhysicalAllocation.Charge(PersistentCity.Core.GcAllocationSegment.PhysicalSpawnPedestrian);
            appearance.data = new CharacterData { name = identity.Name, ageInDays = checked(Mathf.Clamp(identity.Age, 18, 85) * daysPerYear) };
            var look = PersonAppearance.For(identity);
            var gender = identity.Female ? Gender.Female : Gender.Male;
            var skin = SkinColorHelper.GetRandom(identity.AppearanceSeed);
            UnityEngine.Random.InitState(look.Seed);
            appearance.RandomizeElements(gender, skin, new[] { Tag(look.Wardrobe) }, look.Seed);
            if (look.Wardrobe != Wardrobe.Casual && (appearance.data.elements == null || appearance.data.elements.Count == 0))
            {
                UnityEngine.Random.InitState(look.Seed);
                appearance.RandomizeElements(gender, skin, new[] { AppearanceTag.Casual }, look.Seed);
            }
            appearance.data.fatness = Mathf.Min(appearance.data.fatness, 0.55f);
            if (appearance.data.elements == null || appearance.data.elements.Count == 0) throw new InvalidOperationException("Empty appearance");
            PhysicalAllocation.Charge(PersistentCity.Core.GcAllocationSegment.PhysicalSpawnAppearance);
            Snapshot(appearance.data, expectedAppearance);
            PhysicalAllocation.Charge(PersistentCity.Core.GcAllocationSegment.PhysicalSpawnCheck);
            appearance.UpdateVisuals();
            PhysicalAllocation.Charge(PersistentCity.Core.GcAllocationSegment.PhysicalSpawnAppearance);
            Snapshot(appearance.data, actualAppearance);
            bool same = SameText(expectedAppearance, actualAppearance);
            PhysicalAllocation.Charge(PersistentCity.Core.GcAllocationSegment.PhysicalSpawnCheck);
            if (!same || appearance.GetSelectedMeshRenderers().Count == 0) throw new InvalidOperationException("Appearance meshes unavailable");
            PhysicalAllocation.Charge(PersistentCity.Core.GcAllocationSegment.PhysicalSpawnAppearance);
            AppearanceBuilds++;
        }

        private static JsonSerializer? appearanceSerializer;
        private static readonly StringBuilder expectedAppearance = new StringBuilder(8192), actualAppearance = new StringBuilder(8192);
        private static void Snapshot(CharacterData data, StringBuilder into)
        {
            into.Length = 0;
            appearanceSerializer ??= JsonSerializer.CreateDefault();
            using var writer = new StringWriter(into, CultureInfo.InvariantCulture);
            using var json = new JsonTextWriter(writer) { Formatting = Formatting.None };
            appearanceSerializer.Serialize(json, data);
        }
        private static bool SameText(StringBuilder a, StringBuilder b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static AppearanceTag Tag(Wardrobe wardrobe) => wardrobe switch
        {
            Wardrobe.Formal => AppearanceTag.Formal,
            Wardrobe.Sport => AppearanceTag.Sport,
            Wardrobe.Party => AppearanceTag.Party,
            _ => AppearanceTag.Casual,
        };

        public void Release(PhysicalPedestrianBody body)
        {
            if (body == null || !active.TryGetValue(body.IdentityId, out var current) || !ReferenceEquals(current, body)) return;
            active.Remove(body.IdentityId);
            ThirdPersonCharacter character = body.Character;
            body.SetFade(1);
            body.Invalidate();
            if (character == null) return;
            var relay = character.capsuleCollider.gameObject.GetComponent<PhysicalPedestrianCrosswalk>();
            if (relay != null) relay.Body = null;
            character.RemoveHandObject(); character.RemoveHeadObject(); character.RemoveUpperChestObject();
            if (character.navmeshAgent.enabled && character.navmeshAgent.isOnNavMesh)
            { character.navmeshAgent.isStopped = true; character.navmeshAgent.ResetPath(); }
            NotifyRemoved(character);
            character.navmeshAgent.enabled = false; character.capsuleCollider.enabled = false;
            character.gameObject.SetActive(false);
            idle.Return(character, body.Dressing);
        }

        private static void NotifyRemoved(ThirdPersonCharacter character)
        {
            try { if (character != null && character.capsuleCollider != null) GleyTrafficSystem.Manager.TriggerColliderRemovedEvent(character.capsuleCollider); }
            catch (Exception) { }
        }

        private void DestroyOwned(ThirdPersonCharacter character)
        {
            all.Remove(character);
            if (character != null)
            {
                NotifyRemoved(character);
                UnityEngine.Object.Destroy(character.gameObject);
            }
        }

        public bool Visible(Position at) => ScreenVisibility.ContainsPoint(at);

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            BeforeNavigation = null; NavigationHook.Detach(this);
            foreach (var body in active.Values) body.Invalidate();
            active.Clear(); idle.Clear();
            foreach (var character in all) if (character != null)
            {
                NotifyRemoved(character);
                UnityEngine.Object.Destroy(character.gameObject);
            }
            all.Clear();
        }
    }

    internal sealed class PhysicalPedestrianBody
    {
        private bool valid = true;
        private bool stopped = true;
        private readonly Dictionary<Collider, Crossing> crossings = new Dictionary<Collider, Crossing>();
        private readonly List<Collider> departedCrossings = new List<Collider>();
        private sealed class Crossing
        {
            internal Crossing(CrosswalkToTrafficLightLink link) { Link = link; }
            internal readonly CrosswalkToTrafficLightLink Link;
            internal bool Permitted;
        }
        internal PhysicalPedestrianBody(ThirdPersonCharacter character, string identityId, long generation)
        { Character = character; IdentityId = identityId; Generation = generation; }
        public string IdentityId { get; }
        public long Generation { get; }
        internal PersonDressing Dressing { get; set; }
        public ThirdPersonCharacter Character { get; }
        public CapsuleCollider Capsule => Character.capsuleCollider;
        public NavMeshAgent Agent => Character.navmeshAgent;
        public bool Valid => valid && Character != null && Character.gameObject.activeInHierarchy && Agent.enabled && Agent.isOnNavMesh && Position.IsFinite;
        public Position Position => Character == null ? new Position(double.NaN, double.NaN, double.NaN) : Point(Character.transform.position);
        public bool SetTarget(Position corner, double metresPerScaledSecond)
        {
            if (!Valid || !Fits(corner) || !PersistentCity.Core.Position.Finite(metresPerScaledSecond) || metresPerScaledSecond <= 0 || metresPerScaledSecond > 12) return false;
            var path = SharedPath;
            if (!Agent.CalculatePath(Vector(corner), path) || path.status != NavMeshPathStatus.PathComplete) return false;
            int count = Corners(path);
            if (count < 1 || PersistentCity.Core.Position.Distance(Point(corners[count - 1]), corner) > 0.05) return false;
            for (int i = 1; i + 1 < count; i++)
                if (DistanceToSegment(corners[i], Character.transform.position, Vector(corner)) > 0.15f) return false;
            Agent.speed = (float)metresPerScaledSecond;
            if (!Agent.SetPath(path)) return false;
            stopped = false; Tick(); return true;
        }
        public bool SetPathTarget(Position target, double metresPerScaledSecond)
        {
            if (!Valid || !Fits(target) || !PersistentCity.Core.Position.Finite(metresPerScaledSecond) || metresPerScaledSecond <= 0 || metresPerScaledSecond > 12) return false;
            var path = SharedPath;
            if (!Agent.CalculatePath(Vector(target), path) || path.status != NavMeshPathStatus.PathComplete) return false;
            int count = Corners(path);
            if (count < 1 || PersistentCity.Core.Position.Distance(Point(corners[count - 1]), target) > 0.3) return false;
            Agent.speed = (float)metresPerScaledSecond;
            if (!Agent.SetPath(path)) return false;
            stopped = false; Tick(); return true;
        }
        private static NavMeshPath? sharedPath;
        private static NavMeshPath SharedPath => sharedPath ??= new NavMeshPath();
        private static Vector3[] corners = new Vector3[32];
        private static int Corners(NavMeshPath path)
        {
            int count = path.GetCornersNonAlloc(corners);
            while (count == corners.Length)
            {
                corners = new Vector3[corners.Length * 2];
                count = path.GetCornersNonAlloc(corners);
            }
            return count;
        }
        public void Stop(bool stop) { stopped = stop; Tick(); }
        public bool HasPath => Valid && Agent.hasPath && !Agent.isPathStale;
        public void Pace(double metresPerScaledSecond)
        {
            if (!Valid) return;
            if (!(metresPerScaledSecond > 0) || metresPerScaledSecond > 12) { stopped = true; Agent.velocity = Vector3.zero; Tick(); return; }
            Agent.speed = (float)metresPerScaledSecond;
            Vector3 velocity = Agent.velocity;
            if (velocity.sqrMagnitude > Agent.speed * Agent.speed) Agent.velocity = velocity.normalized * Agent.speed;
            stopped = false; Tick();
        }
        public bool CanFade => canFade ??= ProbeFade();
        private bool? canFade;
        private bool ProbeFade()
        {
            if (Character == null) return false;
            Character.GetComponentsInChildren(true, probe);
            bool any = false;
            foreach (var renderer in probe)
            {
                if (renderer == null) continue;
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null || material.shader == null) { probe.Clear(); return false; }
                    var facts = new MaterialFadeFacts(material.shader.name, material.renderQueue,
                        material.HasProperty(BaseColorId), material.HasProperty(ColorId),
                        material.HasProperty(SrcBlendId) ? material.GetFloat(SrcBlendId) : (float?)null,
                        material.HasProperty(DstBlendId) ? material.GetFloat(DstBlendId) : (float?)null,
                        material.HasProperty(SurfaceId) ? material.GetFloat(SurfaceId) : (float?)null,
                        material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"), material.IsKeywordEnabled("_ALPHABLEND_ON"),
                        material.IsKeywordEnabled("_ALPHATEST_ON"));
                    if (!WalkerFollow.MaterialCanFade(facts)) { probe.Clear(); return false; }
                    any = true;
                }
            }
            probe.Clear();
            return any;
        }
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend"), DstBlendId = Shader.PropertyToID("_DstBlend"),
            SurfaceId = Shader.PropertyToID("_Surface");
        public void SetFade(float alpha)
        {
            if (Character == null) return;
            alpha = Mathf.Clamp01(alpha);
            if (alpha >= 1)
            {
                foreach (var renderer in faded) if (renderer != null) renderer.SetPropertyBlock(null);
                faded.Clear(); originals.Clear(); return;
            }
            block ??= new MaterialPropertyBlock();
            if (faded.Count == 0)
            {
                Character.GetComponentsInChildren(true, faded);
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
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"), ColorId = Shader.PropertyToID("_Color");
        private readonly List<Renderer> faded = new List<Renderer>(), probe = new List<Renderer>();
        private readonly List<(int Id, Color Colour)> originals = new List<(int, Color)>();
        private MaterialPropertyBlock? block;
        public bool TryProjectPose(Position pose, out Position projected)
        {
            projected = pose;
            if (!Valid || !Fits(pose) || !NavMesh.SamplePosition(Vector(pose), out NavMeshHit hit, 0.5f, WalkingRoutes.DryFilter)) return false;
            projected = Point(hit.position);
            return projected.IsFinite;
        }
        public bool Warp(Position target)
        {
            if (!Valid || !Fits(target)) return false;
            if (!NavMesh.SamplePosition(Vector(target), out NavMeshHit hit, 0.5f, WalkingRoutes.DryFilter)) return false;
            if (!Agent.Warp(hit.position) || !Agent.isOnNavMesh) return false;
            Agent.ResetPath(); stopped = true; Tick();
            return Valid;
        }
        public bool OnScreen
        {
            get
            {
                if (!Valid) return false;
                Renderer? renderer = Character.appearanceSetter != null ? Character.appearanceSetter.lastMergedMeshRenderer : null;
                if (renderer == null) renderer = Character.GetComponentInChildren<Renderer>();
                return ScreenVisibility.Contains(renderer);
            }
        }
        public void Face(Position target)
        {
            if (!Valid || !Fits(target)) return;
            Vector3 direction = Vector(target) - Character.transform.position; direction.y = 0;
            if (direction.sqrMagnitude > 0.0001f) Character.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
        public void Tick()
        {
            if (!Valid) return;
            bool redForPedestrian = false;
            departedCrossings.Clear();
            foreach (var pair in crossings)
            {
                if (pair.Key == null || !pair.Key.enabled || !pair.Key.gameObject.activeInHierarchy || pair.Value.Link == null)
                { departedCrossings.Add(pair.Key!); continue; }
                Crossing crossing = pair.Value;
                if (!crossing.Permitted && crossing.Link.redTrafficLight != null && crossing.Link.redTrafficLight.gameObject.activeSelf)
                    crossing.Permitted = true;
                if (!crossing.Permitted && crossing.Link.redTrafficLight != null) redForPedestrian = true;
            }
            foreach (var collider in departedCrossings) crossings.Remove(collider);
            bool pause = LoadScene.isLoading || (BuildingManager.IsInsideBuilding && !NativeInteriorCityView.Active)
                || (InstanceBehavior<UIs>.Instance != null && InstanceBehavior<UIs>.Instance.gameSpeed.Paused);
            Agent.isStopped = stopped || redForPedestrian || pause;
        }
        internal void Enter(Collider other)
        {
            if (!valid || other == null || !other.CompareTag("Crosswalk") || crossings.ContainsKey(other)) return;
            var link = other.GetComponent<CrosswalkToTrafficLightLink>();
            if (link == null) return;
            Crossing? crossing = null;
            foreach (var current in crossings.Values)
                if (current.Link == link) { crossing = current; break; }
            crossings.Add(other, crossing ?? new Crossing(link));
            Tick();
        }
        internal void Exit(Collider other)
        {
            if (other != null && crossings.Remove(other)) Tick();
        }
        internal void Invalidate()
        {
            valid = false; stopped = true; crossings.Clear(); departedCrossings.Clear();
        }
        internal static bool Fits(Position p) => p.IsFinite && Math.Abs(p.X) <= float.MaxValue && Math.Abs(p.Y) <= float.MaxValue && Math.Abs(p.Z) <= float.MaxValue;
        internal static Vector3 Vector(Position p) => new Vector3((float)p.X, (float)p.Y, (float)p.Z);
        internal static Position Point(Vector3 p) => new Position(p.x, p.y, p.z);
        private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        { Vector3 d = b-a; float t = d.sqrMagnitude > 0 ? Mathf.Clamp01(Vector3.Dot(p-a,d)/d.sqrMagnitude) : 0; return Vector3.Distance(p,a+d*t); }
    }

    internal static class ScreenVisibility
    {
        private static readonly Plane[] planes = new Plane[6];
        private static int frame = -1;
        private static Camera? camera;
        internal static bool Contains(Renderer? renderer)
        {
            if (frame != Time.frameCount)
            {
                frame = Time.frameCount;
                camera = Camera.main;
                if (camera != null) GeometryUtility.CalculateFrustumPlanes(camera, planes);
            }
            if (camera == null || renderer == null) return true;
            return renderer.enabled && renderer.isVisible && GeometryUtility.TestPlanesAABB(planes, renderer.bounds);
        }
        internal static bool ContainsPoint(Position at)
        {
            Contains(null);
            if (camera == null || !PhysicalPedestrianBody.Fits(at)) return true;
            var centre = PhysicalPedestrianBody.Vector(at) + Vector3.up * 0.9f;
            return GeometryUtility.TestPlanesAABB(planes, new Bounds(centre, new Vector3(0.8f, 1.8f, 0.8f)));
        }
    }

    internal static class NavigationHook
    {
        private struct PersistentCityWalkerBound { }
        private static PhysicalPedestrians? owner;
        private static bool installed;

        internal static void Attach(PhysicalPedestrians pool)
        {
            owner = pool;
            if (installed) return;
            var loop = UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop();
            int inserted = 0;
            loop = Insert(loop, ref inserted);
            if (inserted == 0) { Debug.LogWarning("[Persistent City] navigation update not found; walkers are bounded in Update only."); return; }
            UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(loop);
            installed = true;
        }

        internal static void Detach(PhysicalPedestrians pool)
        {
            if (!ReferenceEquals(owner, pool)) return;
            owner = null;
            if (!installed) return;
            UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(Remove(UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop()));
            installed = false;
        }

        private static void Run()
        {
            var hook = owner?.BeforeNavigation;
            if (hook == null) return;
            try { hook(); }
            catch (Exception error)
            {
                if (owner != null) owner.BeforeNavigation = null;
                Debug.LogWarning("[Persistent City] walker navigation bound disabled: " + error.Message);
            }
        }

        private static UnityEngine.LowLevel.PlayerLoopSystem Insert(UnityEngine.LowLevel.PlayerLoopSystem system, ref int inserted)
        {
            if (system.subSystemList == null) return system;
            var list = new List<UnityEngine.LowLevel.PlayerLoopSystem>(system.subSystemList.Length + 1);
            foreach (var child in system.subSystemList)
            {
                if (child.type == typeof(UnityEngine.PlayerLoop.PreUpdate.AIUpdate) || child.type == typeof(UnityEngine.PlayerLoop.PreLateUpdate.AIUpdatePostScript))
                {
                    list.Add(new UnityEngine.LowLevel.PlayerLoopSystem { type = typeof(PersistentCityWalkerBound), updateDelegate = Run });
                    inserted++;
                }
                list.Add(Insert(child, ref inserted));
            }
            system.subSystemList = list.ToArray();
            return system;
        }

        private static UnityEngine.LowLevel.PlayerLoopSystem Remove(UnityEngine.LowLevel.PlayerLoopSystem system)
        {
            if (system.subSystemList == null) return system;
            var list = new List<UnityEngine.LowLevel.PlayerLoopSystem>(system.subSystemList.Length);
            foreach (var child in system.subSystemList)
                if (child.type != typeof(PersistentCityWalkerBound)) list.Add(Remove(child));
            system.subSystemList = list.ToArray();
            return system;
        }
    }

    internal sealed class PhysicalPedestrianCrosswalk : MonoBehaviour
    {
        internal PhysicalPedestrianBody? Body;
        private void OnTriggerEnter(Collider other) { Body?.Enter(other); }
        private void OnTriggerExit(Collider other) { Body?.Exit(other); }
    }
}
