using System;
using System.Collections.Generic;
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
    /// <summary>Main-thread-only, exclusively owned native bodies. Never borrows native pedestrian leases.</summary>
    internal sealed class PhysicalPedestrians : IDisposable
    {
        private readonly Transform parent;
        private readonly int maximum;
        private readonly List<ThirdPersonCharacter> all = new List<ThirdPersonCharacter>();
        private readonly Stack<ThirdPersonCharacter> idle = new Stack<ThirdPersonCharacter>();
        private readonly Dictionary<string, PhysicalPedestrianBody> active = new Dictionary<string, PhysicalPedestrianBody>(StringComparer.Ordinal);
        private long generation;
        private bool disposed;

        public PhysicalPedestrians(Transform parent, int maximum)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (maximum < 1 || maximum > 256) throw new ArgumentOutOfRangeException(nameof(maximum));
            this.parent = parent; this.maximum = maximum;
        }

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
                if (idle.Count > 0) character = idle.Pop();
                else
                {
                    if (all.Count >= maximum) return false;
                    // Instantiate beneath an inactive parent so prefab player flags cannot run Awake before review.
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
                character.gameObject.name = "PersistentCity " + identity.Id;
                character.gameObject.SetActive(true);
                character.navmeshAgent.enabled = true;
                if (!character.navmeshAgent.Warp(hit.position) || !character.navmeshAgent.isOnNavMesh) throw new InvalidOperationException("NPC warp failed");
                character.navmeshAgent.ResetPath(); character.navmeshAgent.isStopped = true;
                character.ResetAnimator();
                var appearance = character.appearanceSetter;
                int daysPerYear = SaveGameManager.Current?.gameVariables.daysPerYear ?? 60;
                if (daysPerYear < 8) daysPerYear = 60;
                appearance.data = new CharacterData { name = identity.Name, ageInDays = checked(Mathf.Clamp(identity.Age, 18, 85) * daysPerYear) };
                UnityEngine.Random.InitState(identity.AppearanceSeed);
                appearance.RandomizeElements(identity.Female ? Gender.Female : Gender.Male,
                    SkinColorHelper.GetRandom(identity.AppearanceSeed), new[] { AppearanceTag.Casual }, identity.AppearanceSeed);
                appearance.data.fatness = Mathf.Min(appearance.data.fatness, 0.55f);
                // An empty selection makes UpdateVisuals silently randomize. Refuse it before/after rendering.
                if (appearance.data.elements == null || appearance.data.elements.Count == 0) throw new InvalidOperationException("Empty appearance");
                string expectedAppearance = JsonConvert.SerializeObject(appearance.data);
                appearance.UpdateVisuals();
                if (JsonConvert.SerializeObject(appearance.data) != expectedAppearance || appearance.GetSelectedMeshRenderers().Count == 0) throw new InvalidOperationException("Appearance meshes unavailable");
                // Native culling can leave an idle lease's renderers/animator disabled.
                // Rebind visibility after the new mesh merge, even when the visible flag is stale.
                character.ToggleVisibility(true, includePhysics: false);
                character.OnVisibleChanged(true);
                character.animator.enabled = true;
                if (appearance.lastMergedMeshRenderer != null) appearance.lastMergedMeshRenderer.enabled = true;
                if (character.shadowCaster != null) character.shadowCaster.enabled = true;
                body = new PhysicalPedestrianBody(character, identity.Id, checked(++generation));
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

        public void Release(PhysicalPedestrianBody body)
        {
            if (body == null || !active.TryGetValue(body.IdentityId, out var current) || !ReferenceEquals(current, body)) return;
            active.Remove(body.IdentityId);
            ThirdPersonCharacter character = body.Character;
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
            idle.Push(character);
        }

        private static void NotifyRemoved(ThirdPersonCharacter character)
        {
            // Notification subscribers must not prevent cleanup of owned scene objects.
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

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
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
        // Collider membership is separate from signal permission: overlapping volumes
        // for the same crossing share one entry decision and cannot clear each other.
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
        public ThirdPersonCharacter Character { get; }
        public CapsuleCollider Capsule => Character.capsuleCollider;
        public NavMeshAgent Agent => Character.navmeshAgent;
        public bool Valid => valid && Character != null && Character.gameObject.activeInHierarchy && Agent.enabled && Agent.isOnNavMesh && Position.IsFinite;
        public Position Position => Character == null ? new Position(double.NaN, double.NaN, double.NaN) : Point(Character.transform.position);
        public bool SetTarget(Position corner, double metresPerScaledSecond)
        {
            if (!Valid || !Fits(corner) || !PersistentCity.Core.Position.Finite(metresPerScaledSecond) || metresPerScaledSecond <= 0 || metresPerScaledSecond > 12) return false;
            var path = new NavMeshPath();
            if (!Agent.CalculatePath(Vector(corner), path) || path.status != NavMeshPathStatus.PathComplete) return false;
            // Root hands over one authoritative straight corner segment. Reject native detours.
            var corners = path.corners;
            if (corners == null || corners.Length < 1 || PersistentCity.Core.Position.Distance(Point(corners[corners.Length - 1]), corner) > 0.05) return false;
            for (int i = 1; i + 1 < corners.Length; i++)
                if (DistanceToSegment(corners[i], Character.transform.position, Vector(corner)) > 0.15f) return false;
            Agent.speed = (float)metresPerScaledSecond;
            if (!Agent.SetPath(path)) return false;
            stopped = false; Tick(); return true;
        }
        /// <summary>
        /// Walk a short complete navmesh path, corners allowed. For queue moves beside a door, where
        /// no journey corner is being followed and a doorway kink must not cancel the walk.
        /// </summary>
        public bool SetPathTarget(Position target, double metresPerScaledSecond)
        {
            if (!Valid || !Fits(target) || !PersistentCity.Core.Position.Finite(metresPerScaledSecond) || metresPerScaledSecond <= 0 || metresPerScaledSecond > 12) return false;
            var path = new NavMeshPath();
            if (!Agent.CalculatePath(Vector(target), path) || path.status != NavMeshPathStatus.PathComplete) return false;
            var corners = path.corners;
            if (corners == null || corners.Length < 1 || PersistentCity.Core.Position.Distance(Point(corners[corners.Length - 1]), target) > 0.3) return false;
            Agent.speed = (float)metresPerScaledSecond;
            if (!Agent.SetPath(path)) return false;
            stopped = false; Tick(); return true;
        }
        public void Stop(bool stop) { stopped = stop; Tick(); }
        /// <summary>Turn a standing body toward a point, level with the ground.</summary>
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
                // Once cars have red, let the walker clear this crossing even if the
                // signal changes while they are still within its collider volumes.
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

    internal sealed class PhysicalPedestrianCrosswalk : MonoBehaviour
    {
        internal PhysicalPedestrianBody? Body;
        private void OnTriggerEnter(Collider other) { Body?.Enter(other); }
        private void OnTriggerExit(Collider other) { Body?.Exit(other); }
    }
}
