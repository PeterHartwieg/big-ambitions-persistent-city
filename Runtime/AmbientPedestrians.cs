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
    /// <summary>Replaces only ordinary walking-pool population requests. Never disables global street spawning.</summary>
    internal sealed class AmbientPedestrians : IDisposable
    {
        private const string Owner = "hart.persistentcity.ambientpedestrians.v1";
        // A walker that ends its trip in view pauses this long (scaled seconds), then walks to a
        // nearby open door and goes in. A departure older than DepartSafetySeconds is forgotten.
        private const float MinimumPause = 2f, PauseSpread = 4f, DepartSafetySeconds = 300f, RetryPause = 5f, DoorReach = 60f;
        // Arrival counts within this distance of the chosen door (native completion allows 3 m).
        private const float ArrivalMetres = 4f, WalkAwayMetres = 45f;
        // After this many pauses with no reachable open door, the walker walks away out of view instead.
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
        private readonly Dictionary<Pedestrian, float> resumeAt = new Dictionary<Pedestrian, float>();
        private readonly Dictionary<Pedestrian, Departure> departing = new Dictionary<Pedestrian, Departure>();
        private readonly Dictionary<Pedestrian, int> failedSearches = new Dictionary<Pedestrian, int>();
        private readonly StationaryCrowds stationary = new StationaryCrowds();
        private readonly List<Renderer> renderers = new List<Renderer>();
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
            // Native range checks may call this every frame. An already-held ordinary
            // body needs no new work or callbacks; RLO/disabled bodies remain exempt.
            if (!owner.IsOrdinaryUnreserved(__instance)) return true;
            if (owner.held.Contains(__instance)) return false;
            if (owner.departing.TryGetValue(__instance, out var departure))
            {
                owner.departing.Remove(__instance);
                // At its open door, within the game's 100 m range, the walker goes in, however long
                // the walk took. Anywhere else (path retries used up, or beyond the range where the
                // native handler skips arrival and crossings) the ordinary rule below holds or retires it.
                if (!departure.WalkAway && !owner.city!.IsOutsidePlayerRange(__instance.transform.position)
                    && Vector3.Distance(__instance.transform.position, departure.Target) <= ArrivalMetres)
                { owner.Retire(__instance, force: true); return false; }
            }
            // Native OnEndCycle calls OnSpawn directly, bypassing spawner population
            // requests. Recheck RLO's current leases before taking the body over.
            owner.RefreshReservations();
            if (!owner.Active || !owner.IsOrdinaryUnreserved(__instance)) return true;
            if (owner.IsOffscreen(__instance) && owner.retiredFrame != Time.frameCount)
                owner.Retire(__instance);
            else owner.Hold(__instance);
            return false; // No ordinary native re-randomization, including watched bodies.
        }

        /// <summary>
        /// A departing walker never takes a native retarget: that can pick one of the game's
        /// stationary zones or pool the body in view. A path that fails while Resume enables the
        /// walk is reported back to Resume; any later retarget (its door closed on the way) holds
        /// the walker again for another pause.
        /// </summary>
        private static bool ChangeStatePrefix(Pedestrian __instance, bool isMoving)
        {
            var owner = current;
            if (owner == null || !owner.Active || __instance == null || !owner.departing.TryGetValue(__instance, out var departure)) return true;
            if (!owner.IsOrdinaryUnreserved(__instance)) { owner.departing.Remove(__instance); return true; }
            if (owner.enabling) { owner.enableRetargeted = true; return false; }
            owner.departing.Remove(__instance);
            // A walk-away ends at its out-of-view point; it is returned to the pool there if unseen.
            if (departure.WalkAway && !isMoving && owner.IsOffscreen(__instance)) { owner.Retire(__instance, force: true); return false; }
            owner.Hold(__instance);
            return false;
        }

        private void Hold(Pedestrian pedestrian)
        {
            if (!held.Add(pedestrian)) return;
            // Native OnRelease only disables the two AI behaviors and their animation/
            // rotation callbacks. It does not return the pool lease or hide the body.
            // Keep Pedestrian enabled so RLO's ordinary takeover checks still work.
            pedestrian.OnRelease();
            var agent = pedestrian.tpc.navmeshAgent;
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
            // Stable per body, so the same walker always pauses for the same time.
            resumeAt[pedestrian] = Time.time + MinimumPause + (pedestrian.GetInstanceID() & 0x3ff) / 1023f * PauseSpread;
        }

        /// <summary>
        /// Walk a held walker to a nearby open door (or a home) and let it go in there. The native
        /// walking behaviour is re-enabled in place with that target, so the body keeps its
        /// appearance and does not head for the game's stationary zones. OnRelease left both
        /// behaviours disabled. With no door in reach, the walker waits a little longer.
        /// </summary>
        private void Resume(Pedestrian pedestrian)
        {
            resumedFrame = Time.frameCount;
            var agent = pedestrian.tpc.navmeshAgent;
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) { resumeAt[pedestrian] = Time.time + RetryPause; return; }
            failedSearches.TryGetValue(pedestrian, out int failures);
            bool walkAway = false;
            if (!TryNearbyDoor(agent, out var target, out var registration))
            {
                // Later walk-away tries move on to lower-ranked points, so unreachable favourites
                // can't keep a watched walker held for ever.
                int walkAwayTry = failures + 1 - DoorSearchesBeforeWalkAway;
                if (walkAwayTry < 0 || !TryWalkAway(agent, walkAwayTry, out target))
                { failedSearches[pedestrian] = failures + 1; resumeAt[pedestrian] = Time.time + RetryPause; return; }
                walkAway = true; registration = null!;
            }
            held.Remove(pedestrian); resumeAt.Remove(pedestrian); failedSearches.Remove(pedestrian);
            if (pedestrian.tpc.capsuleCollider != null) pedestrian.tpc.capsuleCollider.enabled = true;
            agent.isStopped = false;
            isMoving!.SetValue(pedestrian, true);
            // Native path retries survive Disable; an exhausted count would end the next walk at once.
            pathTryCount!.SetValue(movementHandler!.GetValue(pedestrian.pedestrianWalkingAiBehavior), 0);
            departing[pedestrian] = new Departure { Target = target.Item1, Until = Time.time + DepartSafetySeconds, WalkAway = walkAway };
            enabling = true; enableRetargeted = false;
            try { pedestrian.pedestrianWalkingAiBehavior.Enable(target, registration); }
            finally { enabling = false; }
            if (enableRetargeted)
            {
                // No path after all: stop again and try another destination after the next pause.
                departing.Remove(pedestrian);
                Hold(pedestrian);
                resumeAt[pedestrian] = Time.time + RetryPause;
                return;
            }
            resumed++;
        }

        /// <summary>
        /// The native picker can return a closed building anywhere in the city when nothing open is
        /// near, and a door on navmesh the walker only partly reaches. Accept only an open business
        /// or a home within reach that the walker's own agent has a complete path to, with the door
        /// and every path corner inside the game's 100 m range: beyond it the native range check
        /// ends the walk where the walker stands.
        /// </summary>
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

        /// <summary>
        /// No door in reach (a street of shops at night): walk out of the picture. Candidate points
        /// on a full circle are ranked by where they fall on screen, off screen first, then nearest
        /// a frame edge, and must stay inside the game's 100 m range so the native range check
        /// does not end the walk. The first one with a complete path wins.
        /// </summary>
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
                    // Negative inside the frame (how far from the nearest edge), positive outside it;
                    // behind the camera ranks above everything in front, the further behind the better.
                    float outside = view.z <= 0 ? 2f - view.z : Mathf.Max(Mathf.Max(-view.x, view.x - 1f), Mathf.Max(-view.y, view.y - 1f));
                    candidates.Add((hit.position, outside));
                }
            // Six path queries per try; each later try takes the next six candidates in rank order.
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

        private void RefreshReservations()
        {
            reservations.Update();
            if (!reservations.Ready) LastError = reservations.LastError;
        }

        private bool IsOrdinaryUnreserved(Pedestrian pedestrian) => pedestrian != null
            && pedestrian.GetType() == typeof(Pedestrian) && pedestrian.enabled
            && pedestrian.gameObject.activeInHierarchy && pedestrian.tpc != null
            && !IsReservedNativePedestrian(pedestrian);

        private bool CanRetire(Pedestrian pedestrian) => IsOrdinaryUnreserved(pedestrian) && IsOffscreen(pedestrian);

        private bool IsOffscreen(Pedestrian pedestrian)
        {
            // ThirdPersonCharacter.visible is its distance/LOD rendering switch, not
            // camera visibility. Match the vehicle adapter's renderer visibility rule.
            renderers.Clear();
            pedestrian.GetComponentsInChildren(true, renderers);
            if (renderers.Count == 0) return false; // Unknown/uninitialized appearance: retain it.
            foreach (var renderer in renderers)
                if (renderer != null && renderer.isVisible) return false;
            return true;
        }

        /// <summary>One pool return per frame, shared with the sweep. An arrival at a door may add one more.</summary>
        private void Retire(Pedestrian pedestrian, bool force = false)
        {
            if (retiredFrame == Time.frameCount && !force) return;
            retiredFrame = Time.frameCount;
            held.Remove(pedestrian); resumeAt.Remove(pedestrian); departing.Remove(pedestrian); failedSearches.Remove(pedestrian);
            pedestrian.Release();
        }

        public void Update()
        {
            if (disposed) return;
            RefreshReservations();
            if (!Active || spawner!.activePedestrians == null) return;
            held.RemoveWhere(pedestrian => pedestrian == null || !pedestrian.gameObject.activeInHierarchy
                || !spawner.activePedestrians.Contains(pedestrian));
            if (resumeAt.Count > held.Count)
                foreach (var gone in new List<Pedestrian>(resumeAt.Keys)) if (!held.Contains(gone)) resumeAt.Remove(gone);
            if (departing.Count > 0)
                foreach (var gone in new List<Pedestrian>(departing.Keys))
                {
                    if (gone == null || !gone.gameObject.activeInHierarchy || !spawner.activePedestrians.Contains(gone)) { departing.Remove(gone!); continue; }
                    // A walk that outlives the safety bound is stopped rather than handed back to
                    // native retargeting, which could send it to a stationary zone.
                    if (Time.time >= departing[gone].Until) { departing.Remove(gone); if (IsOrdinaryUnreserved(gone)) Hold(gone); }
                }
            if (failedSearches.Count > held.Count)
                foreach (var gone in new List<Pedestrian>(failedSearches.Keys)) if (!held.Contains(gone)) failedSearches.Remove(gone);
            if (resumedFrame != Time.frameCount)
                foreach (var pair in resumeAt)
                {
                    // One per frame. An off-screen held body is left to the retirement pass below.
                    // Beyond the game's 100 m range its movement handler would end the walk at once,
                    // so a body held out there waits until it is off screen and retired.
                    if (Time.time < pair.Value || !IsOrdinaryUnreserved(pair.Key) || IsOffscreen(pair.Key)
                        || city!.IsOutsidePlayerRange(pair.Key.transform.position)) continue;
                    Resume(pair.Key);
                    break;
                }
            if (retiredFrame == Time.frameCount) return;
            // Do not disturb a visible pedestrian, a derived special-purpose actor,
            // a disabled/leased pedestrian or any RLO social/casualty/ragdoll body.
            for (int index = spawner.activePedestrians.Count - 1; index >= 0; index--)
            {
                var pedestrian = spawner.activePedestrians[index];
                if (!CanRetire(pedestrian)) continue;
                Retire(pedestrian);
                break; // Shared with the cycle hook: at most one pool return per frame.
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
                        RefreshReservations();
                        foreach (var pedestrian in held)
                        {
                            try
                            {
                                if (!reservations.Ready && pedestrian != null && pedestrian.gameObject.activeInHierarchy)
                                {
                                    // A failed contract cannot distinguish our stopped ordinary
                                    // body from one RLO has since borrowed. Do not resume it, but
                                    // also do not report this uncertain restoration as successful.
                                    if (firstError == null) firstError = new InvalidOperationException(
                                        "An active held pedestrian could not be restored because RLO ownership could not be verified: " + reservations.LastError);
                                    continue;
                                }
                                if (pedestrian != null && spawner.activePedestrians.Contains(pedestrian) && IsOrdinaryUnreserved(pedestrian))
                                {
                                    // Native RotateTowards disables this collider before its
                                    // building callback. Restore it before resuming ordinary AI.
                                    if (pedestrian.tpc.capsuleCollider != null) pedestrian.tpc.capsuleCollider.enabled = true;
                                    // Resume the native stationary state at the existing position.
                                    // Its timer later picks a normal walking target; no teleport.
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
