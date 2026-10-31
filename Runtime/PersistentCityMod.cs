using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BAModAPI;
using Newtonsoft.Json;
using PersistentCity.Compatibility;
using PersistentCity.Core;
using PersistentCity.Persistence;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

[assembly: AssemblyVersion("0.10.0.0")]
[assembly: AssemblyInformationalVersion("0.10.0-persistent-city-candidate")]
[assembly: RegisterModClass(typeof(PersistentCity.Runtime.PersistentCityMod))]

namespace PersistentCity.Runtime
{
    [ModEntryOnCityLoad]
    public sealed class PersistentCityMod : IModBigAmbitions
    {
        private GameObject? host;
        public string[] RelativeAssetBundlePaths => Array.Empty<string>();

        public Task OnLoadAsync(ModContext context)
        {
            const string name = "Persistent City Runtime";
            if (GameObject.Find(name) != null)
            {
                context.Logger.Warn("Persistent City is already loaded. This duplicate stays off.");
                return Task.CompletedTask;
            }
            host = new GameObject(name);
            host.AddComponent<PopulationRuntime>().Initialize(context);
            return Task.CompletedTask;
        }

        public Task OnUnloadAsync()
        {
            if (host != null)
            {
                host.GetComponent<PopulationRuntime>()?.Shutdown();
                host.name += " (unloading)";
                UnityEngine.Object.Destroy(host);
                host = null;
            }
            return Task.CompletedTask;
        }
    }

    public sealed partial class PopulationRuntime : MonoBehaviour
    {
        private const int MaxAdvanceEventsPerFrame = 128;
        private const int MaxCitizenEventsPerFrame = 16;
        private const double AdvanceBudgetMilliseconds = 2;
        private const double HiddenSkipBudgetMilliseconds = 16;
        private static readonly double MillisecondsPerTimestamp = 1000.0 / Stopwatch.Frequency;
        private readonly RloIdentityAdapter rlo = new RloIdentityAdapter();
        private IModLogger? log;
        private PrototypeSettings settings = new PrototypeSettings();
        private CitySimulator? simulation;
        private MultimodalRoutes? routes;
        private RouteObjectDiscovery? routeObjects;
        private PhysicalPopulation? physical;
        private DwellSpots dwellSpots = new DwellSpots();
        private bool physicalFailed;
        private readonly ResidentInspector inspector = new ResidentInspector();
        private bool inspectorFailed;
        private Action<string>? inspectorWarning;
        private object? saveIdentity;
        private object? initializingSave;
        private sealed class PreparedCity
        {
            public CityState State = null!;
            public SaveSession Saves = null!;
        }
        private Task<PreparedCity>? initialization;
        private SaveSession? saveSession;
        private NativeFoodMarket? foodMarket;
        private string? initializingPayload;
        private bool initializingHadPayload;
        private Assembly? dependency;
        private float nextStart, nextDependencyCheck, nextStationScan, nextSummary;
        private int gateChanges, roadSnapshots, seenRoadSnapshots, waitingFrames;
        private readonly Dictionary<string, int> roadRebuildReasons = new Dictionary<string, int>(StringComparer.Ordinal);
        private string lastRoadRebuild = "";
        private int slowRoutingFrames, routingFrameLogs;
        private bool clockRetimingRoutes;
        private double maxRloCheckMilliseconds, maxGateCheckMilliseconds, maxStationSearchMilliseconds, maxPrepareMilliseconds;
        private bool failed, subscribed;
        private string status = "";
        private long advanceFrames, advanceEvents;
        private double advanceMilliseconds, maxAdvanceMilliseconds;
        private double closureMilliseconds, maxClosureMilliseconds;
        private long closureFrames;
        private int maxAdvanceEvents;
        // Route planning across frames: slices driven, events that waited for a route, frames that ended with one pending.
        private long planningSlices, planningEvents, planningPendingFrames, seenPlanningRequests, planningRequests;
        // Event restarts are lifetime totals on the simulator; the summary reports the window's share.
        private long eventRestarts, inlineRestarts, seenEventRestarts, seenInlineRestarts;
        private double planningMilliseconds, maxPlanningSliceMilliseconds;
        private string slowestPlanningUnit = "";
        private long slowestPlanningUnitNavMeshQueries;

        /// <summary>Legacy diagnostic fixture helper for a small selected retail slice.
        /// Normal initialization creates food demand and discovers eligible shops automatically.</summary>
        public void StartFoodRoutines(IReadOnlyCollection<string> shops)
        {
            if (simulation == null || routes == null || !routes.ReadyForPopulation || failed
                || !ReferenceEquals(saveIdentity, SaveGameManager.Current) || simulation.State.Food != null)
                throw new InvalidOperationException("A ready city without an existing food scope is required.");
            if (shops == null || shops.Count < 1 || shops.Count > 8 || shops.Distinct(StringComparer.Ordinal).Count() != shops.Count
                || shops.Any(id => !simulation.State.Places.Any(p => p.Id == id && p.Kind == PlaceKind.Shop)))
                throw new InvalidOperationException("Select one to eight established shops recognized by this city's population.");
            var catalogue = FoodBusinessCatalogue.Read(shops, FoodProducts.Ids.ToArray());
            if (catalogue.Businesses.Count != shops.Count) throw new InvalidOperationException("Food scope is incomplete.");
            var state = new FoodCommerceState { MealRhythmVersion = MealRhythm.PolicyVersion, Shops = catalogue.Businesses.Select(b => new FoodScope {
                PlaceId = b.PlaceId, BusinessId = b.Id, ProductId = b.Offers.First().ProductId,
                AdditionalProductIds = b.Offers.Skip(1).Select(o => o.ProductId).ToList() }).ToList(),
                Districts = FoodHouseholdCatalogue.Read(simulation.State.Places.Where(p => p.Kind == PlaceKind.Home)
                    .Select(p => p.District).Where(d => !string.IsNullOrWhiteSpace(d)).Distinct(StringComparer.Ordinal).ToArray()).ToList() };
            var adapter = new NativeFoodMarket(state, simulation.State.Places, enrolling: true);
            try { simulation.State.Food = state; simulation.AttachFoodMarket(adapter, settings.FoodChoice); foodMarket = adapter; }
            catch { simulation.State.Food = null; adapter.Dispose(); throw; }
        }
        public void EnableHouseholdFinances()
        {
            if (simulation == null || failed || !ReferenceEquals(saveIdentity, SaveGameManager.Current))
                throw new InvalidOperationException("A ready current city is required.");
            simulation.EnableHouseholdFinances();
        }
        public object FoodDiagnostics => new { active = foodMarket != null, hourlyCustomerBatchesSuppressed = foodMarket?.SuppressedBatches ?? 0,
            nativeDrawsSuppressed = foodMarket?.SuppressedDraws ?? 0, nativePurchasesThisSession = foodMarket?.NativePurchases ?? 0 };

        /// <summary>Explicit read-only diagnostic; the caller must finish/dispose the bounded recorder.</summary>
        public FoodRoutineRecorder ObserveFoodRoutines()
        {
            if (failed || simulation == null || routes == null || !routes.ReadyForPopulation
                || !ReferenceEquals(saveIdentity, SaveGameManager.Current))
                throw new InvalidOperationException("A ready current population is required for food observation.");
            var homeDistricts = simulation.State.Places.Where(p => p.Kind == PlaceKind.Home && !string.IsNullOrWhiteSpace(p.District))
                .Select(p => p.District).Distinct(StringComparer.Ordinal).ToArray();
            return new FoodRoutineRecorder(simulation, FoodHouseholdCatalogue.Read(homeDistricts));
        }

        /// <summary>Explicit main-thread walking quotes. Call batch.Step with a small route-call budget.</summary>
        public FoodRouteBatch QuoteFoodRoutes(FoodRoutineCapture capture, FoodRoutinePlan plan,
            IReadOnlyList<FoodBusiness> businesses, int maximumCandidates = 3)
        {
            if (failed || simulation == null || routes == null || !routes.ReadyForPopulation
                || !ReferenceEquals(saveIdentity, SaveGameManager.Current) || capture.CityKey != simulation.State.PopulationKey)
                throw new InvalidOperationException("The observed city must still be active for route quotes.");
            var observedRoutes = routes;
            var observedSave = saveIdentity;
            var guarded = new FoodObservationRoutes(observedRoutes, () => !failed && ReferenceEquals(saveIdentity, observedSave)
                && ReferenceEquals(observedSave, SaveGameManager.Current) && ReferenceEquals(routes, observedRoutes) && observedRoutes.ReadyForPopulation);
            return new FoodRouteBatch(capture, plan, businesses, simulation.State.Places, guarded,
                "Native complete public walking paths at diagnostic capture time; historical traffic/access is not replayed", maximumCandidates);
        }

        private sealed class FoodObservationRoutes : IRoutePlanner
        {
            private readonly IRoutePlanner source;
            private readonly Func<bool> current;
            public FoodObservationRoutes(IRoutePlanner source, Func<bool> current) { this.source = source; this.current = current; }
            public Route? Plan(Place from, Place to, double at, TravelPreferences preferences)
            {
                if (!current()) throw new InvalidOperationException("City/navigation changed during food observation; discard the unfinished quote batch.");
                return source.Plan(from, to, at, preferences);
            }
        }

        public void Initialize(ModContext context)
        {
            log = context.Logger;
            try
            {
                NativeFoodOwnership.Install();
                NativeFoodOwnership.EnsureSave();
                if (NativeFoodOwnership.Error.Length > 0) throw new InvalidDataException(NativeFoodOwnership.Error);
                string path = Path.Combine(context.ModRootPath, "persistent-city.json");
                if (File.Exists(path)) settings = JsonConvert.DeserializeObject<PrototypeSettings>(File.ReadAllText(path)) ?? throw new InvalidDataException("Configuration is null.");
                settings.FoodChoice.Clamp(message => log.Info(message));
                if (!Position.Finite(settings.GameClockFactor) || settings.GameClockFactor < .1 || settings.GameClockFactor > 1)
                    throw new InvalidDataException("GameClockFactor must be between 0.1 and 1 (one third gives three times as much travel time per day).");
                if (settings.MaximumPedestrians < 1 || settings.MaximumPedestrians > 256 || settings.MaximumVehicles < 1 || settings.MaximumVehicles > 64
                    || !Position.Finite(settings.PhysicalRadiusMetres) || settings.PhysicalRadiusMetres < 30 || settings.PhysicalRadiusMetres > 300)
                    throw new InvalidDataException("Physical limits must be 1–256 pedestrians, 1–64 vehicles and a 30–300 metre radius.");
                settings.ReportInteriorSettings(message => log.Info(message));
                if (!settings.Enabled)
                {
                    Say("Persistent City is disabled by a developer configuration override. Remove the override and reload the city to start it.");
                    enabled = false;
                }
            }
            catch (Exception error) { Fail(error.Message); }
        }

        private void Update()
        {
            if (failed) return;
            try
            {
                var save = SaveGameManager.Current;
                if (NativeInteriorCityView.Configured && (save == null || UI.Load.LoadScene.isLoading
                    || saveIdentity != null && !ReferenceEquals(saveIdentity, save))) RestoreInterior();
                if (save == null) return;
                if (saveIdentity != null && !ReferenceEquals(saveIdentity, save))
                {
                    physical?.Dispose(); physical = null; physicalFailed = false;
                    inspector.Reset(); inspectorFailed = false;
                    Detach();
                    simulation = null;
                    saveSession = null;
                    routes = null; clockRetimingRoutes = false;
                    saveIdentity = null;
                }
                if (simulation == null)
                {
                    if (initialization != null) { CompleteInitialization(); return; }
                    if (Time.unscaledTime < nextStart) return;
                    nextStart = Time.unscaledTime + 1;
                    TryStart();
                    return;
                }
                foodMarket?.RefreshDiscovery();
                routeObjects?.Step(preparingPausedCity: Time.timeScale == 0);
                if (Time.unscaledTime >= nextDependencyCheck)
                {
                    long checkStarted = Stopwatch.GetTimestamp();
                    nextDependencyCheck = Time.unscaledTime + 30;
                    var source = ReadRlo("periodic");
                    maxRloCheckMilliseconds = Math.Max(maxRloCheckMilliseconds, (Stopwatch.GetTimestamp() - checkStarted) * MillisecondsPerTimestamp);
                    if (!source.MatchesPopulation(simulation.State))
                    {
                        Fail("RLO donor catalogue is no longer available or differs from this snapshot: " + source.Message + " Existing saved state is preserved.");
                        return;
                    }
                    long gateStarted = Stopwatch.GetTimestamp();
                    string gateChange = routes?.Refresh() ?? "";
                    maxGateCheckMilliseconds = Math.Max(maxGateCheckMilliseconds, (Stopwatch.GetTimestamp() - gateStarted) * MillisecondsPerTimestamp);
                    if (gateChange.Length > 0) { gateChanges++; log?.Info("Public gates changed (" + gateChange + "); road snapshot discarded; a new one follows station preparation and reuses the live road scene."); }
                    // Phase station geometry checks halfway between gate geometry checks.
                    nextStationScan = Time.unscaledTime + 15;
                }
                else if (Time.unscaledTime >= nextStationScan)
                {
                    nextStationScan = float.PositiveInfinity;
                    routes?.RefreshStations();
                }
                double target = Now();
                if (target < simulation.State.Minute - 0.001)
                {
                    Fail("Game clock moved behind the population snapshot. Reload the matching save; no state was overwritten.");
                    return;
                }
                foodMarket?.RefreshRoutineHours(target);
                // Build/refresh native road data in bounded slices before advancing any event.
                // Mode availability must not depend on which citizen happened to request a route
                // while the graph was half prepared, or on the number of rendered frames.
                simulation.RefreshOptionalStops();
                RunPhysical(() => physical?.BeforeAdvance());
                double clockRate = NativeTravelPace.ClockRate;
                if (Math.Abs(clockRate - simulation.State.TravelClockRate) > .000001)
                {
                    physical?.Dispose(); physical = null;
                    simulation.State.PrepareForSave();
                    TravelTiming.Apply(simulation.State, NativeTravelPace.WalkingMetresPerSecond, clockRate);
                    var retimedRoutes = new MultimodalRoutes(simulation.State.Places, NativeTravelPace.WalkingMetresPerSecond / clockRate, clockRate, dependency, routeObjects);
                    if (routes != null) retimedRoutes.ReuseRoadSceneForRetiming(routes);
                    routes = retimedRoutes;
                    seenRoadSnapshots = 0; clockRetimingRoutes = true;
                    simulation = simulation.RebuildRoutes(routes);
                    seenPlanningRequests = seenEventRestarts = seenInlineRestarts = 0;
                }
                long prepareStarted = Stopwatch.GetTimestamp();
                bool prepared = routes == null || routes.Prepare();
                if (routes != null && routes.LastDiscoveryGateChange.Length > 0)
                { gateChanges++; log?.Info("Discovered public gate change: " + routes.LastDiscoveryGateChange + "."); }
                double prepareElapsed = (Stopwatch.GetTimestamp() - prepareStarted) * MillisecondsPerTimestamp;
                maxPrepareMilliseconds = Math.Max(maxPrepareMilliseconds, prepareElapsed);
                maxStationSearchMilliseconds = Math.Max(maxStationSearchMilliseconds, routes?.TakeStationSearchMilliseconds() ?? 0);
                int snapshots = routes?.RoadSnapshots ?? 0;
                if (snapshots > seenRoadSnapshots)
                {
                    roadSnapshots += snapshots - seenRoadSnapshots;
                    string reason = clockRetimingRoutes ? "clock-rate" : routes!.LastRoadRebuildReason;
                    clockRetimingRoutes = false;
                    roadRebuildReasons.TryGetValue(reason, out int count); roadRebuildReasons[reason] = count + snapshots - seenRoadSnapshots;
                    lastRoadRebuild = reason + " index=" + routes!.LastRoadInvalidationIndex;
                    log?.Info("Road snapshot started: frame=" + Time.frameCount + ", reason=" + lastRoadRebuild
                        + ", scene searches in this planner=" + routes.RoadSceneSearches + ".");
                }
                seenRoadSnapshots = snapshots;
                if (!prepared) { RecordRoutingFrame(prepareElapsed, 0, false); waitingFrames++; RunPhysical(() => physical?.Update()); inspector.Suspend(); return; }
                long closureStarted = Stopwatch.GetTimestamp();
                simulation.RefreshTrafficClosures();
                double closureElapsed = (Stopwatch.GetTimestamp() - closureStarted) * MillisecondsPerTimestamp;
                closureFrames++; closureMilliseconds += closureElapsed;
                maxClosureMilliseconds = Math.Max(maxClosureMilliseconds, closureElapsed);
                RecordRoutingFrame(prepareElapsed, closureElapsed, true);
                AdvancePopulation(Math.Max(target, simulation.State.Minute));
                if (settings.PhysicalEnabled && !physicalFailed && physical == null && !BuildingManager.IsInsideBuilding && !NativeTravelPace.HiddenTravel)
                {
                    try { physical = new PhysicalPopulation(transform, simulation, routes!, dependency!, log!, settings.MaximumPedestrians, settings.MaximumVehicles, settings.PhysicalRadiusMetres, dwellSpots); }
                    catch (Exception error)
                    {
                        physicalFailed = true;
                        if (NativeInteriorCityView.Configured) NativeInteriorCityView.FailClosed();
                        log?.Warn("Physical population unavailable; ordinary spawning restored, persistent background simulation remains active: " + error);
                    }
                }
                RunPhysical(() => physical?.Update());
                UpdateInspector();
                if (Time.unscaledTime >= nextSummary)
                {
                    nextSummary = Time.unscaledTime + 60;
                    var state = simulation.State;
                    log?.Info("Population: " + state.Citizens.Count + " citizens; " + state.Citizens.Count(c => c.Phase == CitizenPhase.Travelling)
                        + " travelling, " + state.Citizens.Count(c => c.Phase == CitizenPhase.WaitingForRoute) + " awaiting a route, "
                        + state.Citizens.Count(c => c.Phase == CitizenPhase.WaitingForOpening) + " awaiting opening; clock "
                        + state.Minute.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + ", lag "
                        + Math.Max(0, target - state.Minute).ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
                        + " game minutes. Advancement this observation: " + advanceFrames + " frames, " + advanceEvents + " events, "
                        + advanceMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " ms total, "
                        + maxAdvanceMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " ms maximum advance, "
                        + maxAdvanceEvents + " maximum events in one frame; these figures cover advancement calls only. Routes: "
                        + roadSnapshots + " road snapshots started (" + gateChanges + " for gate changes), "
                        + waitingFrames + " frames waiting for routes; rebuild reasons ["
                        + string.Join(", ", roadRebuildReasons.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + "=" + pair.Value))
                        + "]; slow routing frames " + slowRoutingFrames + " (first " + routingFrameLogs + " logged); last rebuild " + lastRoadRebuild
                        + "; this planner scene searches " + (routes?.RoadSceneSearches ?? 0) + ", maximum "
                        + (routes?.MaximumRoadSceneSearchMilliseconds ?? 0).ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                        + " ms, station captures " + (routes?.StationSearches ?? 0)
                        + "; discovery sweeps " + (routeObjects?.Sweeps ?? 0) + ", last branch captures " + (routeObjects?.LastSweepBranchCaptures ?? 0)
                        + ", split hints " + (routeObjects?.SplitBranches ?? 0)
                        + ", maximum slice " + (routeObjects?.MaximumSliceMilliseconds ?? 0).ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                        + " ms, last sweep " + (routeObjects?.LastSweepSeconds ?? 0).ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                        + " s / " + (routeObjects?.LastSweepMilliseconds ?? 0).ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                        + " ms work; slowest RLO check "
                        + maxRloCheckMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " ms (RLO contract bindings this session "
                        + rlo.Bindings + ", " + rlo.BindingMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " ms), door search "
                        + maxGateCheckMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " ms, station search "
                        + maxStationSearchMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " ms, preparation frame "
                        + maxPrepareMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " ms. Closure refresh: "
                        + closureFrames + " calls, " + closureMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                        + " ms total, " + maxClosureMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " ms maximum. Route planning across frames: "
                        + planningRequests + " requests, " + planningSlices + " slices, " + planningEvents + " events completed, "
                        + planningPendingFrames + " frames ended with a plan pending, "
                        + planningMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " ms total, slowest slice "
                        + maxPlanningSliceMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " ms ending in "
                        + (slowestPlanningUnit.Length == 0 ? "none" : slowestPlanningUnit) + " with " + slowestPlanningUnitNavMeshQueries
                        + " NavMesh path queries; event restarts " + eventRestarts + " (run inline after repeated restarts "
                        + inlineRestarts + "), road search restarts " + (routes?.RoadSearchRestarts ?? 0)
                        + ", units " + (routes?.JobUnits ?? 0) + ". "
                        + (physical?.Diagnostics ?? "Physical population inactive."));
                    if (simulation.Traffic != null)
                        log?.Info("Citywide traffic: " + simulation.Traffic.VehicleCount + " persistent cars, "
                            + simulation.Traffic.PendingEventCount + " scheduled road events, " + simulation.Traffic.LinkCount + " road sections.");
                    ResetAdvanceDiagnostics();
                }
            }
            catch (Exception error) { Fail(error.ToString()); }
        }

        private void AdvancePopulation(double target)
        {
            var active = simulation ?? throw new InvalidOperationException("Population is not active.");
            long started = Stopwatch.GetTimestamp();
            int events = 0, citizenEvents = 0;
            double elapsed = 0;
            // The time machine already hides physical play and gates native accounting.
            // Use a larger, still bounded batch there so waiting does not scale with
            // the normal-play 16-resident limit. Ordinary play keeps its existing budget.
            bool hiddenSkip = NativeCityClock.ProcessingEconomySkip;
            int citizenLimit = hiddenSkip ? MaxAdvanceEventsPerFrame : MaxCitizenEventsPerFrame;
            double timeBudget = hiddenSkip ? HiddenSkipBudgetMilliseconds : AdvanceBudgetMilliseconds;
            // Always allow the first event or planning unit. A native query cannot be preempted, so
            // the soft time budget can be exceeded by that one unit's duration, never by a whole plan.
            bool pendingAtEnd = false;
            try
            {
                for (int step = 0; step < MaxAdvanceEventsPerFrame; step++)
                {
                    // A prior event can discover changed native topology. Stop before the next citizen sees an
                    // unavailable graph as a failed route; the next Update will finish staged preflight first.
                    if (routes != null && !routes.ReadyForPopulation) { pendingAtEnd = active.PlanningPending; break; }
                    if (active.PlanningPending)
                    {
                        // The event whose route is being planned holds the clock; give its job the remaining budget.
                        long sliceStarted = Stopwatch.GetTimestamp();
                        long queriesBefore = routes?.Metrics.NavMeshPathQueries ?? 0;
                        bool finished = active.ContinuePlanning(Math.Max(0, timeBudget - elapsed));
                        double slice = (Stopwatch.GetTimestamp() - sliceStarted) * MillisecondsPerTimestamp;
                        planningSlices++; planningMilliseconds += slice;
                        if (slice > maxPlanningSliceMilliseconds)
                        {
                            maxPlanningSliceMilliseconds = slice;
                            slowestPlanningUnit = routes?.LastJobUnit ?? "";
                            slowestPlanningUnitNavMeshQueries = (routes?.Metrics.NavMeshPathQueries ?? 0) - queriesBefore;
                        }
                        elapsed = (Stopwatch.GetTimestamp() - started) * MillisecondsPerTimestamp;
                        // Not finished: still pending, or the event restarted and is due again unstarted.
                        if (!finished) { if (elapsed >= timeBudget) { pendingAtEnd = active.PlanningPending; break; } continue; }
                        events++; citizenEvents++; planningEvents++;
                        if (citizenEvents >= citizenLimit || elapsed >= timeBudget) break;
                        continue;
                    }
                    var result = active.AdvanceFrameSlice(target, 1);
                    events += result.Events;
                    // Food decisions count once at begin; their bounded continuation units run
                    // under the time and total-unit caps without spending another citizen slot.
                    citizenEvents += result.CitizenEvents;
                    elapsed = (Stopwatch.GetTimestamp() - started) * MillisecondsPerTimestamp;
                    if (result.PlanningPending) { pendingAtEnd = elapsed >= timeBudget; if (pendingAtEnd) break; continue; }
                    if (result.CaughtUp || citizenEvents >= citizenLimit || elapsed >= timeBudget) break;
                }
            }
            finally { active.SynchronizeSnapshot(); }
            elapsed = (Stopwatch.GetTimestamp() - started) * MillisecondsPerTimestamp;
            advanceFrames++;
            advanceEvents += events;
            advanceMilliseconds += elapsed;
            maxAdvanceMilliseconds = Math.Max(maxAdvanceMilliseconds, elapsed);
            maxAdvanceEvents = Math.Max(maxAdvanceEvents, events);
            if (pendingAtEnd || active.PlanningPending) planningPendingFrames++;
            long requests = active.PlanningRequests;
            planningRequests += requests - seenPlanningRequests;
            seenPlanningRequests = requests;
            long restarts = active.EventRestarts, inline = active.InlineAfterRestarts;
            eventRestarts += restarts - seenEventRestarts; inlineRestarts += inline - seenInlineRestarts;
            seenEventRestarts = restarts; seenInlineRestarts = inline;
        }

        private void UpdateInspector()
        {
            if (inspectorFailed || simulation == null) return;
            try { inspector.Attach(inspectorWarning ??= message => log?.Warn(message)); inspector.Update(simulation, physical, economyRect); }
            catch (Exception error)
            {
                // The inspector is read-only; its failure must not stop the population.
                inspectorFailed = true; inspector.Reset();
                log?.Warn("Resident inspector stopped; the city continues unchanged: " + error);
            }
        }

        private void RunPhysical(Action action)
        {
            try { action(); }
            catch (Exception error)
            {
                // A failed view adapter must not stop the valid background population. If cleanup
                // itself fails, propagate to the outer stop path so uncertain state is not saved.
                physicalFailed = true;
                if (NativeInteriorCityView.Configured) NativeInteriorCityView.FailClosed();
                // Commerce remains attached to the background population. Only a full
                // runtime stop may release its native price and ownership hooks.
                try { physical?.Dispose(); }
                finally { physical = null; }
                log?.Warn("Physical population stopped; ordinary spawning restored and background simulation continues: " + error);
            }
        }

        private void RecordRoutingFrame(double preparation, double closure, bool prepared)
        {
            if (preparation < 16 && closure < 16) return;
            slowRoutingFrames++;
            if (routingFrameLogs >= 4) return; // Bound log overhead during a pathological observation.
            routingFrameLogs++;
            log?.Info("Slow routing frame=" + Time.frameCount + ", realtime="
                + Time.unscaledTime.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                + ", prepared=" + prepared + ", preparation_ms=" + preparation.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                + ", closure_ms=" + closure.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                + ", last_rebuild=" + lastRoadRebuild + ".");
        }

        private void ResetAdvanceDiagnostics()
        {
            advanceFrames = advanceEvents = 0;
            advanceMilliseconds = maxAdvanceMilliseconds = 0;
            planningSlices = planningEvents = planningPendingFrames = planningRequests = 0;
            eventRestarts = inlineRestarts = 0;
            planningMilliseconds = maxPlanningSliceMilliseconds = 0;
            slowestPlanningUnit = ""; slowestPlanningUnitNavMeshQueries = 0;
            closureFrames = 0; closureMilliseconds = maxClosureMilliseconds = 0;
            maxAdvanceEvents = 0;
            gateChanges = roadSnapshots = waitingFrames = 0;
            roadRebuildReasons.Clear(); slowRoutingFrames = routingFrameLogs = 0;
            maxRloCheckMilliseconds = maxGateCheckMilliseconds = maxStationSearchMilliseconds = maxPrepareMilliseconds = 0;
        }

        private void TryStart()
        {
            if (!NativeTravelPace.StartObservation()) { Fail(NativeTravelPace.LastError); return; }
            dependency = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "RealLifeOverhaul");
            var source = ReadRlo("startup");
            if (source.Status != RloReadStatus.Ready)
            {
                Say(source.Message);
                if (source.Status == RloReadStatus.Incompatible) Fail(source.Message);
                return;
            }
            var save = SaveGameManager.Current;
            if (save == null || save.modData == null) return;
            NativeFoodOwnership.EnsureSave();
            if (NativeFoodOwnership.Error.Length > 0) { Fail(NativeFoodOwnership.Error); return; }
            // Snapshot all Unity/RLO input on the main thread. Large population creation and
            // strict save decoding are pure work; never hold up a frame while creating 25,000 people.
            initializingHadPayload = save.modData.TryGetValue(SaveStore.StateKey, out initializingPayload);
            var saved = new Dictionary<string, string>(StringComparer.Ordinal);
            if (initializingHadPayload) saved.Add(SaveStore.StateKey, initializingPayload!);
            var places = GamePlaces.Read();
            log?.Info(OutdoorPlaces.LastDiscoverySummary);
            double minute = Now();
            double clockRate = NativeTravelPace.InitialClockRate(settings.GameClockFactor);
            initializingSave = save;
            initialization = Task.Run(() =>
            {
                var session = SaveSession.Open(saved, source.PopulationKey);
                var stored = session.InitialReadResult;
                CityState state;
                if (stored.Status == SaveReadStatus.Missing)
                    state = Population.CreateFromHousing(source.Identities, places, source.PopulationKey, source.SourceVersion, source.CitySeed, minute);
                else if (stored.Status == SaveReadStatus.Loaded && stored.State != null)
                {
                    state = stored.State;
                    if (!source.MatchesPopulation(state)) throw new InvalidDataException("Snapshot donor identities differ from current RLO profiles.");
                }
                else throw new InvalidDataException("Population save refused: " + stored.Error);
                NativeTravelPace.PrepareInitialTiming(state, stored.Status == SaveReadStatus.Missing, clockRate);
                SaveCodec.ValidateForActivation(state); // Validate completely before making it active or touching modData.
                return new PreparedCity { State = state, Saves = session };
            });
            Say("Preparing housing-based background population. Existing saved state is retained until initialization succeeds.");
        }

        private void CompleteInitialization()
        {
            var pending = initialization;
            if (pending == null || !pending.IsCompleted) return;
            initialization = null;
            var save = SaveGameManager.Current;
            var expectedSave = initializingSave;
            var expectedPayload = initializingPayload;
            initializingSave = null;
            initializingPayload = null;
            // Observe failures even if a different save was loaded during this computation.
            if (pending.IsFaulted)
            {
                string error = pending.Exception?.GetBaseException().Message ?? "Unknown initialization failure.";
                if (ReferenceEquals(expectedSave, save)) Fail(error + " Original payload retained.");
                return;
            }
            if (pending.IsCanceled || save == null || !ReferenceEquals(expectedSave, save)) return;
            bool hasPayload = save.modData.TryGetValue(SaveStore.StateKey, out var currentPayload);
            if (hasPayload != initializingHadPayload || currentPayload != expectedPayload)
            { Fail("Population payload changed during initialization. Original payload retained."); return; }
            var prepared = pending.Result;
            var state = prepared.State;
            var source = ReadRlo("activation");
            if (!source.MatchesPopulation(state)) { Fail("RLO donor catalogue changed during initialization. Original payload retained."); return; }
            if (state.Minute > Now() + 0.001) { Fail("Population snapshot is ahead of this save's clock; original payload retained."); return; }
            var binding = prepared.Saves.BindTo(save.modData);
            if (!binding.Success) { Fail("Population save binding refused: " + binding.Error); return; }
            saveSession = prepared.Saves;
            routeObjects = new RouteObjectDiscovery();
            routes = new MultimodalRoutes(state.Places, NativeTravelPace.WalkingMetresPerSecond / state.TravelClockRate, state.TravelClockRate, dependency, routeObjects);
            simulation = new CitySimulator(state, routes);
            dwellSpots = new DwellSpots();
            simulation.AttachOptionalStops(new OptionalStopEnvironment(state.Places, dwellSpots), settings.OptionalStopChoicePercentage);
            seenPlanningRequests = seenEventRestarts = seenInlineRestarts = 0;
            if (state.Food == null)
                state.Food = new FoodCommerceState { MealRhythmVersion = MealRhythm.PolicyVersion, HouseholdFinanceVersion = HouseholdFinance.PolicyVersion,
                    RepresentationFactor = CityBalance.ConsumerRepresentationFactor,
                    Districts = FoodHouseholdCatalogue.Read(state.Places.Where(p => p.Kind == PlaceKind.Home)
                        .Select(p => p.District).Where(d => !string.IsNullOrWhiteSpace(d)).Distinct(StringComparer.Ordinal).ToArray()).ToList() };
            if (state.Food.HouseholdFinanceVersion != HouseholdFinance.PolicyVersion || state.Food.MealRhythmVersion != MealRhythm.PolicyVersion)
                throw new InvalidDataException("Unsupported experimental food policy; preserve this city and use a fresh development baseline.");
            foodMarket = new NativeFoodMarket(state.Food, state.Places);
            simulation.AttachFoodMarket(foodMarket, settings.FoodChoice);
            saveIdentity = save;
            NativeInteriorCityView.Start(settings, log!, RestoreInterior);
            NativeCityClock.Start(save, settings.GameClockFactor, () => simulation?.IsCaughtUpTo(Now()) == true, () => simulation?.State.Food != null, Now);
            NativeTravelPace.ResetReading();
            log?.Info("City clock factor=" + settings.GameClockFactor.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                + "; Driven Rides coordinated=" + NativeCityClock.CoordinatesDrivenRides + ". Movement speed unchanged.");
            ResetAdvanceDiagnostics();
            GlobalEvents.onSaveGame += Save;
            GlobalEvents.onGameUnloaded += Shutdown;
            subscribed = true;
            nextDependencyCheck = Time.unscaledTime + 30;
            nextStationScan = float.PositiveInfinity;
            seenRoadSnapshots = 0; clockRetimingRoutes = false;
            Say("Persistent population active: " + state.Citizens.Count + " residents, " + state.Places.Count
                + " places. Native-pace walking, subway and car journeys; physical population preflight follows road initialization. Ctrl+F8 opens food business feedback.");
        }

        private static double Now()
        {
            var save = SaveGameManager.Current;
            return save.Day * 1440.0 + save.Hour * 60.0 + save.Minute;
        }

        private RloReadResult ReadRlo(string context)
        {
            var result = rlo.Read(dependency);
            log?.Info("RLO single-pass read context=" + context + " status=" + result.Status
                + " identities=" + result.Identities.Count + " elapsedMs="
                + rlo.LastReadMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                + " reads=" + rlo.Reads + " totalMs="
                + rlo.TotalReadMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                + " maxMs=" + rlo.MaximumReadMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
            return result;
        }

        private void Save()
        {
            if (failed || simulation == null || !ReferenceEquals(saveIdentity, SaveGameManager.Current)) return;
            try
            {
                var source = ReadRlo("save");
                if (!source.MatchesPopulation(simulation.State))
                { Fail("RLO changed before saving; original population payload retained."); return; }
                RunPhysical(() => physical?.BeforeSave());
                foodMarket?.RefreshKnownOwnership();
                // A partially caught-up snapshot is valid: replay continues from its recorded minute after load.
                var result = (saveSession ?? throw new InvalidOperationException("Save session is missing."))
                    .WriteTo(SaveGameManager.Current.modData, simulation.State);
                if (!result.Success) Fail("Population save refused: " + result.Error);
            }
            catch (Exception error) { Fail("Population save failed: " + error.Message); }
        }

        private void RestoreInterior()
        {
            try
            {
                if (NativeInteriorCityView.State.Active || physical?.HasBodies == true)
                    RunPhysical(() => physical?.RestoreInterior());
            }
            catch (Exception error) { Fail("Interior view restoration failed: " + error.Message); }
            finally { NativeInteriorCityView.Suspend(); }
        }

        private void Say(string message)
        {
            if (message == status) return;
            status = message;
            log?.Info(message);
        }

        private void Fail(string message)
        {
            failed = true;
            status = message;
            log?.Warn("Persistent City stopped: " + message);
            Detach();
        }

        private void Detach()
        {
            NativeInteriorCityView.Stop();
            foodMarket?.Dispose(); foodMarket = null;
            inspector.Detach();
            try { physical?.Dispose(); }
            finally { physical = null; routeObjects?.Dispose(); routeObjects = null; NativeCityClock.Stop(); NativeTravelPace.StopObservation(); }
            if (!subscribed) return;
            GlobalEvents.onSaveGame -= Save;
            GlobalEvents.onGameUnloaded -= Shutdown;
            subscribed = false;
        }

        public void Shutdown()
        {
            Detach();
            // A pure worker may finish after unload; observe faults and release all runtime references.
            if (initialization != null)
                _ = initialization.ContinueWith(task => { _ = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            initialization = null;
            initializingSave = null;
            initializingPayload = null;
            simulation = null;
            saveSession = null;
            routes = null;
            saveIdentity = null;
            enabled = false;
        }
        private void OnDestroy() => Shutdown();
    }
}
