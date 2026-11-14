using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using PersistentCity.Compatibility;
using PersistentCity.Core;
using Streets.Pedestrians;
using UI.Load;
using UnityEngine;

namespace PersistentCity.Runtime
{
    internal sealed class AmbientPedestrians : IDisposable
    {
        private const string Owner = "hart.persistentcity.ambientpedestrians.v1";
        private const float MinimumPause = 2f, PauseSpread = 4f, DepartSafetySeconds = 300f, RetryPause = 5f, DoorReach = 60f;
        private const float ArrivalMetres = 4f, WalkAwayMetres = 45f;
        private const int TargetTries = 4, DoorSearchesBeforeWalkAway = 2;
        private sealed class Departure
        {
            public Vector3 Target; public float Until; public bool WalkAway;
        }
        private static AmbientPedestrians? current;
        private readonly Harmony harmony = new Harmony(Owner);
        private readonly RloPhysicalReservations reservations;
        private readonly FieldInfo? lastAmount;
        private readonly MethodInfo? setActive;
        private readonly MethodInfo? endCycle;
        private readonly MethodInfo? changeState;
        private bool enabling, enableRetargeted;
        private readonly FieldInfo? isMoving;
        private readonly FieldInfo? movementHandler;
        private readonly FieldInfo? pathTryCount;
        private readonly HashSet<Pedestrian> held = new HashSet<Pedestrian>();
        private readonly List<Pedestrian> departingScratch = new List<Pedestrian>();
        private Predicate<Pedestrian>? heldGone;
        private readonly Dictionary<Pedestrian, float> resumeAt = new Dictionary<Pedestrian, float>();
        private readonly Dictionary<Pedestrian, Departure> departing = new Dictionary<Pedestrian, Departure>();
        private readonly Dictionary<Pedestrian, int> failedSearches = new Dictionary<Pedestrian, int>();
        private readonly StationaryCrowds stationary = new StationaryCrowds();
        private readonly List<Renderer> renderers = new List<Renderer>();
        private readonly Dictionary<Pedestrian, bool> offscreen = new Dictionary<Pedestrian, bool>();
        private readonly HashSet<Pedestrian> activeSet = new HashSet<Pedestrian>();
        private List<Pedestrian>? activeSetSource;
        private int reservationFrame = -1, offscreenFrame = -1, activeSetFrame = -1, activeSetCount = -1;
        private const int ActiveSetMinimum = 16;
        private int retiredFrame = -1, resumedFrame = -1;
        private long resumed;
        public long Resumed => resumed;
        private CityManager? city;
        private PedestrianSpawner? spawner;
        private object? save;
        private int desiredAmount;
        private bool active, disposed, ownsRegistration;
        public bool Ready => !disposed && LastError == "" && reservations.Ready;
        public string LastError { get; private set; } = "";
        public bool Active => active && Ready && SameCity;
        public IReadOnlyCollection<string> ReservedCitizens => reservations.ReservedCitizenIds;
        public IReadOnlyCollection<string> ReservedCitizenIds => reservations.ReservedCitizenIds;
        public bool IsReserved(string citizenId) => reservations.IsReserved(citizenId);
        public bool IsReservedNativePedestrian(Pedestrian pedestrian)
            => reservations.IsReservedNativePedestrian(pedestrian, pedestrian == null ? null : pedestrian.tpc);
        private bool SameCity => city != null && spawner != null && ReferenceEquals(InstanceBehavior<CityManager>.Instance, city)
            && ReferenceEquals(city.pedestrianSpawner, spawner) && ReferenceEquals(SaveGameManager.Current, save);

        public AmbientPedestrians(Assembly rlo, IReadOnlyList<CitizenIdentity> sources)
        {
            reservations = new RloPhysicalReservations(rlo, sources);
            try
            {
                if (!reservations.Ready) throw new InvalidOperationException(reservations.LastError);
                lastAmount = typeof(PedestrianSpawner).GetField("_lastSetAmount", BindingFlags.NonPublic | BindingFlags.Instance);
                setActive = typeof(PedestrianSpawner).GetMethod("SetActivePedestrians", new[] { typeof(int) });
                endCycle = typeof(Pedestrian).GetMethod("OnEndCycle", BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
                changeState = typeof(Pedestrian).GetMethod("ChangeState", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(bool) }, null);
                isMoving = typeof(Pedestrian).GetField("_isMoving", BindingFlags.NonPublic | BindingFlags.Instance);
                movementHandler = typeof(PedestrianWalkingAiBehavior).GetField("_movementHandler", BindingFlags.NonPublic | BindingFlags.Instance);
                pathTryCount = movementHandler?.FieldType.GetField("_pathTryCount", BindingFlags.NonPublic | BindingFlags.Instance);
                if (lastAmount == null || lastAmount.FieldType != typeof(int) || setActive == null || setActive.ReturnType != typeof(void)
                    || endCycle == null || endCycle.ReturnType != typeof(void) || changeState == null || changeState.ReturnType != typeof(void)
                    || isMoving == null || isMoving.FieldType != typeof(bool) || pathTryCount == null || pathTryCount.FieldType != typeof(int))
                    throw new InvalidOperationException("Unexpected native pedestrian spawner contract.");
            }
            catch (Exception error) { LastError = error.GetType().Name + ": " + error.Message; }
        }

        public void Activate()
        {
            if (active) return;
            if (!Ready) throw new InvalidOperationException("Ambient pedestrian preflight failed: " + (LastError == "" ? reservations.LastError : LastError));
            if (current != null || Harmony.HasAnyPatches(Owner))
            { LastError = "Another ambient pedestrian hook owner is already registered."; throw new InvalidOperationException(LastError); }
            try
            {
                city = InstanceBehavior<CityManager>.Instance;
                save = SaveGameManager.Current;
                spawner = city == null ? null : city.pedestrianSpawner;
                if (city == null || spawner == null || save == null) throw new InvalidOperationException("No live city pedestrian spawner.");
                desiredAmount = (int)lastAmount!.GetValue(spawner)!;
                current = this; ownsRegistration = true;
                harmony.Patch(setActive!, prefix: new HarmonyMethod(typeof(AmbientPedestrians), nameof(SetActivePrefix)));
                harmony.Patch(endCycle!, prefix: new HarmonyMethod(typeof(AmbientPedestrians), nameof(EndCyclePrefix)));
                harmony.Patch(changeState!, prefix: new HarmonyMethod(typeof(AmbientPedestrians), nameof(ChangeStatePrefix)));
                active = true;
                UnityEngine.Debug.Log("[Persistent City] Ordinary ambient pedestrian requests: suppressed; RLO scenes and global spawning retained.");
                stationary.Activate();
            }
            catch (Exception error)
            {
                LastError = error.GetType().Name + ": " + error.Message;
                if (ownsRegistration) { harmony.UnpatchAll(Owner); current = null; ownsRegistration = false; }
                active = false;
                throw new InvalidOperationException("Ambient pedestrian activation failed: " + LastError, error);
            }
        }

        private static bool SetActivePrefix(PedestrianSpawner __instance, int amount)
        {
            var owner = current;
            if (owner == null || !owner.Active || !ReferenceEquals(__instance, owner.spawner)) return true;
            owner.desiredAmount = amount;
            owner.lastAmount!.SetValue(__instance, amount);
            return false;
        }

        private static bool EndCyclePrefix(Pedestrian __instance)
        {
            var owner = current;
            if (owner == null || !owner.Active || owner.spawner!.activePedestrians == null
                || !owner.spawner.activePedestrians.Contains(__instance)) return true;
            if (!owner.IsOrdinaryUnreserved(__instance)) return true;
            if (owner.held.Contains(__instance)) return false;
            if (owner.departing.TryGetValue(__instance, out var departure))
            {
                owner.departing.Remove(__instance);
                if (!departure.WalkAway && !owner.city!.IsOutsidePlayerRange(__instance.transform.position)
                    && Vector3.Distance(__instance.transform.position, departure.Target) <= ArrivalMetres)
                {
                    owner.RefreshReservations(force: true);
                    if (!owner.Active || !owner.IsOrdinaryUnreserved(__instance)) return true;
                    owner.Retire(__instance, force: true); return false;
                }
            }
            owner.RefreshReservations(force: true);
            if (!owner.Active || !owner.IsOrdinaryUnreserved(__instance)) return true;
            if (owner.IsOffscreen(__instance) && owner.retiredFrame != Time.frameCount)
                owner.Retire(__instance);
            else owner.Hold(__instance);
            return false;
        }

        private static bool ChangeStatePrefix(Pedestrian __instance, bool isMoving)
        {
            var owner = current;
            if (owner == null || !owner.Active || __instance == null || !owner.departing.TryGetValue(__instance, out var departure)) return true;
            if (!owner.IsOrdinaryUnreserved(__instance)) { owner.departing.Remove(__instance); return true; }
            if (owner.enabling) { owner.enableRetargeted = true; return false; }
            owner.departing.Remove(__instance);
            if (departure.WalkAway && !isMoving && owner.IsOffscreen(__instance)) { owner.Retire(__instance, force: true); return false; }
            owner.Hold(__instance);
            return false;
        }

        private void Hold(Pedestrian pedestrian)
        {
            if (!held.Add(pedestrian)) return;
            pedestrian.OnRelease();
            var agent = pedestrian.tpc.navmeshAgent;
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
            resumeAt[pedestrian] = Time.time + MinimumPause + (pedestrian.GetInstanceID() & 0x3ff) / 1023f * PauseSpread;
        }

        private void Resume(Pedestrian pedestrian)
        {
            resumedFrame = Time.frameCount;
            var agent = pedestrian.tpc.navmeshAgent;
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) { resumeAt[pedestrian] = Time.time + RetryPause; return; }
            failedSearches.TryGetValue(pedestrian, out int failures);
            bool walkAway = false;
            if (!TryNearbyDoor(agent, out var target, out var registration))
            {
                int walkAwayTry = failures + 1 - DoorSearchesBeforeWalkAway;
                if (walkAwayTry < 0 || !TryWalkAway(agent, walkAwayTry, out target))
                { failedSearches[pedestrian] = failures + 1; resumeAt[pedestrian] = Time.time + RetryPause; return; }
                walkAway = true; registration = null!;
            }
            held.Remove(pedestrian); resumeAt.Remove(pedestrian); failedSearches.Remove(pedestrian);
            if (pedestrian.tpc.capsuleCollider != null) pedestrian.tpc.capsuleCollider.enabled = true;
            agent.isStopped = false;
            isMoving!.SetValue(pedestrian, true);
            pathTryCount!.SetValue(movementHandler!.GetValue(pedestrian.pedestrianWalkingAiBehavior), 0);
            departing[pedestrian] = new Departure { Target = target.Item1, Until = Time.time + DepartSafetySeconds, WalkAway = walkAway };
            enabling = true; enableRetargeted = false;
            try { pedestrian.pedestrianWalkingAiBehavior.Enable(target, registration); }
            finally { enabling = false; }
            if (enableRetargeted)
            {
                departing.Remove(pedestrian);
                Hold(pedestrian);
                resumeAt[pedestrian] = Time.time + RetryPause;
                return;
            }
            resumed++;
        }

        private bool TryNearbyDoor(UnityEngine.AI.NavMeshAgent agent, out (Vector3, Quaternion) target, out BuildingRegistration registration)
        {
            Vector3 from = agent.transform.position;
            for (int attempt = 0; attempt < TargetTries; attempt++)
            {
                target = PedestrianBuildingPositionProvider.GetRandomAvailableBuildingTarget(from, 0f, DoorReach, out registration);
                if (target.Item1 == default || registration == null || Vector3.Distance(from, target.Item1) > DoorReach + 5f) continue;
                if (registration.GetBuildingType() != "ba:buildingtype_residential" && !Helpers.BusinessHelper.IsBusinessOpen(registration)) continue;
                if (city!.IsOutsidePlayerRange(target.Item1)) continue;
                if (CompletePath(agent, target.Item1)) return true;
            }
            target = default; registration = null!;
            return false;
        }

        private bool TryWalkAway(UnityEngine.AI.NavMeshAgent agent, int attempt, out (Vector3, Quaternion) target)
        {
            target = default;
            var camera = Camera.main;
            if (camera == null || city == null) return false;
            Vector3 from = agent.transform.position;
            var filter = new UnityEngine.AI.NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            var candidates = new List<(Vector3 Point, float Score)>();
            foreach (float distance in new[] { WalkAwayMetres, WalkAwayMetres * 0.6f })
                for (int step = 0; step < 12; step++)
                {
                    Vector3 wanted = from + Quaternion.AngleAxis(step * 30f, Vector3.up) * Vector3.forward * distance;
                    if (!UnityEngine.AI.NavMesh.SamplePosition(wanted, out var hit, 5f, filter) || city.IsOutsidePlayerRange(hit.position)) continue;
                    Vector3 view = camera.WorldToViewportPoint(hit.position + Vector3.up);
                    float outside = view.z <= 0 ? 2f - view.z : Mathf.Max(Mathf.Max(-view.x, view.x - 1f), Mathf.Max(-view.y, view.y - 1f));
                    candidates.Add((hit.position, outside));
                }
            int paths = 0;
            var ranked = candidates.OrderByDescending(c => c.Score).ToList();
            if (ranked.Count == 0) return false;
            foreach (var candidate in ranked.Skip(attempt * 6 % ranked.Count))
            {
                if (++paths > 6) break;
                if (!CompletePath(agent, candidate.Point)) continue;
                target = (candidate.Point, Quaternion.identity);
                return true;
            }
            return false;
        }

        private bool CompletePath(UnityEngine.AI.NavMeshAgent agent, Vector3 target)
        {
            var path = new UnityEngine.AI.NavMeshPath();
            if (!agent.CalculatePath(target, path) || path.status != UnityEngine.AI.NavMeshPathStatus.PathComplete
                || path.corners.Length == 0 || Vector3.Distance(path.corners[path.corners.Length - 1], target) > 1f) return false;
            foreach (var corner in path.corners) if (city!.IsOutsidePlayerRange(corner)) return false;
            return true;
        }

        private void RefreshReservations(bool force = false)
        {
            int frame = Time.frameCount;
            if (!force && reservationFrame == frame) return;
            reservations.Update();
            reservationFrame = frame;
            if (!reservations.Ready) LastError = reservations.LastError;
        }

        private bool IsOrdinaryUnreserved(Pedestrian pedestrian) => pedestrian != null
            && pedestrian.GetType() == typeof(Pedestrian) && pedestrian.enabled
            && pedestrian.gameObject.activeInHierarchy && pedestrian.tpc != null
            && !IsReservedNativePedestrian(pedestrian);

        private bool CanRetire(Pedestrian pedestrian) => IsOrdinaryUnreserved(pedestrian) && IsOffscreen(pedestrian);

        private bool IsOffscreen(Pedestrian pedestrian)
        {
            int frame = Time.frameCount;
            if (offscreenFrame != frame) { offscreen.Clear(); offscreenFrame = frame; }
            else if (offscreen.TryGetValue(pedestrian, out bool known)) return known;
            bool answer = RenderersOffscreen(pedestrian);
            offscreen[pedestrian] = answer;
            return answer;
        }

        private bool RenderersOffscreen(Pedestrian pedestrian)
        {
            renderers.Clear();
            pedestrian.GetComponentsInChildren(true, renderers);
            if (renderers.Count == 0) return false;
            foreach (var renderer in renderers)
                if (renderer != null && renderer.isVisible) return false;
            return true;
        }

        private void Retire(Pedestrian pedestrian, bool force = false)
        {
            if (retiredFrame == Time.frameCount && !force) return;
            retiredFrame = Time.frameCount;
            held.Remove(pedestrian); resumeAt.Remove(pedestrian); departing.Remove(pedestrian); failedSearches.Remove(pedestrian);
            pedestrian.Release();
        }

        private bool InActivePool(List<Pedestrian> pool, Pedestrian pedestrian)
        {
            if (pool.Count < ActiveSetMinimum) return pool.Contains(pedestrian);
            if (activeSetFrame != Time.frameCount || !ReferenceEquals(activeSetSource, pool) || activeSetCount != pool.Count)
            {
                activeSet.Clear();
                foreach (var member in pool) activeSet.Add(member);
                activeSetFrame = Time.frameCount; activeSetSource = pool; activeSetCount = pool.Count;
            }
            return activeSet.Contains(pedestrian);
        }

        public void Update()
        {
            if (disposed) return;
            RefreshReservations();
            if (!Active || spawner!.activePedestrians == null) return;
            held.RemoveWhere(heldGone ??= pedestrian => pedestrian == null || !pedestrian.gameObject.activeInHierarchy
                || !InActivePool(spawner.activePedestrians, pedestrian));
            if (resumeAt.Count > held.Count)
                foreach (var gone in new List<Pedestrian>(resumeAt.Keys)) if (!held.Contains(gone)) resumeAt.Remove(gone);
            if (departing.Count > 0)
            {
                departingScratch.Clear();
                foreach (var key in departing.Keys) departingScratch.Add(key);
                foreach (var gone in departingScratch)
                {
                    if (gone == null || !gone.gameObject.activeInHierarchy || !InActivePool(spawner.activePedestrians, gone)) { departing.Remove(gone!); continue; }
                    if (Time.time >= departing[gone].Until) { departing.Remove(gone); if (IsOrdinaryUnreserved(gone)) Hold(gone); }
                }
                departingScratch.Clear();
            }
            if (failedSearches.Count > held.Count)
                foreach (var gone in new List<Pedestrian>(failedSearches.Keys)) if (!held.Contains(gone)) failedSearches.Remove(gone);
            if (resumedFrame != Time.frameCount)
                foreach (var pair in resumeAt)
                {
                    if (Time.time < pair.Value || !IsOrdinaryUnreserved(pair.Key) || IsOffscreen(pair.Key)
                        || city!.IsOutsidePlayerRange(pair.Key.transform.position)) continue;
                    Resume(pair.Key);
                    break;
                }
            if (retiredFrame == Time.frameCount) return;
            for (int index = spawner.activePedestrians.Count - 1; index >= 0; index--)
            {
                var pedestrian = spawner.activePedestrians[index];
                if (!CanRetire(pedestrian)) continue;
                Retire(pedestrian);
                break;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            bool restore = active && SameCity;
            disposed = true; active = false;
            stationary.Dispose();
            if (ownsRegistration)
            {
                harmony.UnpatchAll(Owner);
                if (ReferenceEquals(current, this)) current = null;
                ownsRegistration = false;
                Exception? firstError = null;
                try
                {
                    if (restore && spawner != null)
                    {
                        RefreshReservations(force: true);
                        foreach (var pedestrian in held)
                        {
                            try
                            {
                                if (!reservations.Ready && pedestrian != null && pedestrian.gameObject.activeInHierarchy)
                                {
                                    if (firstError == null) firstError = new InvalidOperationException(
                                        "An active held pedestrian could not be restored because RLO ownership could not be verified: " + reservations.LastError);
                                    continue;
                                }
                                if (pedestrian != null && spawner.activePedestrians.Contains(pedestrian) && IsOrdinaryUnreserved(pedestrian))
                                {
                                    if (pedestrian.tpc.capsuleCollider != null) pedestrian.tpc.capsuleCollider.enabled = true;
                                    changeState!.Invoke(pedestrian, new object[] { false });
                                }
                            }
                            catch (Exception error) { if (firstError == null) firstError = error; }
                        }
                    }
                }
                catch (Exception error) { if (firstError == null) firstError = error; }
                finally
                {
                    try
                    {
                        if (restore && SameCity && spawner != null) spawner.SetActivePedestrians(desiredAmount);
                    }
                    catch (Exception error) { if (firstError == null) firstError = error; }
                    finally { held.Clear(); resumeAt.Clear(); departing.Clear(); failedSearches.Clear(); }
                }
                if (firstError != null)
                {
                    LastError = "Ambient pedestrian restoration was incomplete; reload this city before continuing. " + firstError.Message;
                    throw new InvalidOperationException(LastError, firstError);
                }
            }
        }
    }
}
