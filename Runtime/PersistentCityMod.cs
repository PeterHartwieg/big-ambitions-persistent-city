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
            var runtime = host.AddComponent<PopulationRuntime>();
            runtime.Initialize(context);
            runtime.ArmForCityLoad();
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
        private readonly PhysicalSetupGate physicalSetupGate = new PhysicalSetupGate();
        private readonly ResidentInspector inspector = new ResidentInspector();
        private bool inspectorFailed;
        private Action<string>? inspectorWarning;
        private object? saveIdentity;
        private object? initializingSave;
        private sealed class PreparedCity
        {
            public CityState State = null!;
            public SaveSession Saves = null!;
            public double ReadMilliseconds, CreateMilliseconds, ValidateMilliseconds;
            public string? EpochMarker;
            public long EpochSequence;
        }
        private Task<PreparedCity>? initialization;
        private SaveSession? saveSession;
        private NativeFoodMarket? foodMarket;
        private StreetFoodVendorMarket? vendorMarket;
        private string? initializingPayload;
        private bool initializingHadPayload;
        private Assembly? dependency;
        private float nextStart, nextDependencyCheck, nextStationScan, nextSummary;
        private readonly StartupClockHold startupHold = new StartupClockHold();
        private int gateChanges, roadSnapshots, seenRoadSnapshots, waitingFrames;
        private readonly Dictionary<string, int> roadRebuildReasons = new Dictionary<string, int>(StringComparer.Ordinal);
        private string lastRoadRebuild = "";
        private int slowRoutingFrames, routingFrameLogs;
        private bool clockRetimingRoutes;
        private double maxRloCheckMilliseconds, maxGateCheckMilliseconds, maxStationSearchMilliseconds, maxPrepareMilliseconds;
        private bool failed, subscribed;
        private bool metadataLifetime;
        private string status = "";
        private long advanceFrames, advanceEvents;
        private double advanceMilliseconds, maxAdvanceMilliseconds;
        private double closureMilliseconds, maxClosureMilliseconds;
        private long closureFrames;
        private int maxAdvanceEvents;
        private long planningSlices, planningEvents, planningPendingFrames, seenPlanningRequests, planningRequests;
        private long eventRestarts, inlineRestarts, seenEventRestarts, seenInlineRestarts;
        private double planningMilliseconds, maxPlanningSliceMilliseconds;
        private string slowestPlanningUnit = "";
        private long slowestPlanningUnitNavMeshQueries;
        private string slowestSliceUnitStage = "";
        private double slowestSliceUnitMilliseconds, slowestSliceUnitNavMeshMilliseconds, slowestSliceJobMilliseconds;
        private int slowestSliceUnitNavMeshQueries;
        private long slowestSliceUnitNavMeshSamples;
        private readonly FoodContinuationMaximum slowestFoodContinuation = new FoodContinuationMaximum();
        private double slowestAdvanceUnitMilliseconds, slowestAdvanceStopMilliseconds, slowestAdvanceCallStopMilliseconds;
        private double slowestAdvanceMaximumUnitStopMilliseconds, slowestAdvanceP95UnitStopMilliseconds;
        private int slowestAdvanceStopUnits, slowestAdvanceUnitsOverStopThreshold;
        private string slowestAdvanceUnitsOverStopText = "";
        private int observedStopCrossingUnits, observedStopCrossingAdvances;
        private double observedStopCrossingMaximumMilliseconds;
        private string observedStopCrossingMaximumKind = "", observedStopCrossingText = "";
        private const int ListedStopCrossingAdvances = 4;
        private string slowestAdvanceUnit = "";
        private int slowestAdvanceCollections;
        private OptionalStopEnvironment? stopEnvironment;

        private IFoodMarket ComposeFoodMarkets(NativeFoodMarket shops)
        {
            var current = simulation ?? throw new InvalidOperationException("Vendor market needs an active city.");
            var save = SaveGameManager.Current;
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "FoodTrucks");
            var api = StreetFoodBridge.Open(assembly, out string reason);
            var policy = new FoodProductPolicySource(() => shops.LoadedNames, () => settings.FoodProductOverrides.Resolver, FoodProductMetadataApi.PublishSnapshot,
                message => log?.Warn("Food product policy fell back to the original three: " + message));
            shops.ProductPolicySource = policy;
            vendorMarket = new StreetFoodVendorMarket(current, api, reason, new VendorNavMeshSampler(), save.modData, Now,
                () => ReferenceEquals(save, SaveGameManager.Current), () => shops.LoadedNames == null ? null : policy.Current,
                settings.FoodChoice.FoodMenuMaximumOffersPerShop);
            vendorMarket.WorkSpike = (component, ms) => log?.Warn("Vendor mod-work spike >= 30 ms: " + component + "="
                + ms.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " ms");
            AttachVendorDecisionDiagnostics(current);
            return new FoodMarkets(current.State.Food!, shops, vendorMarket, id => simulation!.FindPlace(id).Position, policy);
        }
        public object? FoodAwareness(string consumerId, int? day=null) => simulation?.InspectFoodAwareness(consumerId,day);
        public object RivalFoodDiagnostics => new { Core=simulation?.DumpRivalFood(), RefreshUnits=foodMarket?.RivalRefreshUnits ?? 0, DetailReads=foodMarket?.RivalDetailReads ?? 0, Unavailable=foodMarket?.RivalUnavailable ?? "" };
        public object FoodDiagnostics => new { active = foodMarket != null, hourlyCustomerBatchesSuppressed = foodMarket?.SuppressedBatches ?? 0,
            nativeDrawsSuppressed = foodMarket?.SuppressedDraws ?? 0, nativePurchasesThisSession = foodMarket?.NativePurchases ?? 0,
            travelClockRate = simulation?.State.TravelClockRate ?? 0, walkingMetresPerGameMinute = routes?.WalkingMetresPerMinute ?? 0,
            lunchReachMetres = routes == null ? 0 : FoodClockReach.LunchReachMetres(routes.WalkingMetresPerMinute),
            mealOutcomes = FoodClockReach.Outcomes(simulation?.State.Food, 8).Select(o => new { outcome = o.Outcome, count = o.Count }).ToArray() };

        public FoodRoutineRecorder ObserveFoodRoutines()
        {
            if (failed || simulation == null || routes == null || !routes.ReadyForPopulation
                || !ReferenceEquals(saveIdentity, SaveGameManager.Current))
                throw new InvalidOperationException("A ready current population is required for food observation.");
            var homeDistricts = simulation.State.Places.Where(p => p.Kind == PlaceKind.Home && !string.IsNullOrWhiteSpace(p.District))
                .Select(p => p.District).Distinct(StringComparer.Ordinal).ToArray();
            return new FoodRoutineRecorder(simulation, FoodHouseholdCatalogue.Read(homeDistricts));
        }

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
            RegisterVendorDiagnostics();
            RegisterResearchDiagnostics();
            try
            {
                FoodProductMetadataApi.BindMainThread(); metadataLifetime = true;
                NativeFoodOwnership.Install();
                NativeFoodOwnership.EnsureSave();
                if (NativeFoodOwnership.Error.Length > 0) throw new InvalidDataException(NativeFoodOwnership.Error);
                string path = Path.Combine(context.ModRootPath, "persistent-city.json");
                if (File.Exists(path)) settings = JsonConvert.DeserializeObject<PrototypeSettings>(File.ReadAllText(path)) ?? throw new InvalidDataException("Configuration is null.");
                settings.FoodProductOverrides.ReportOnce(message => log.Info(message));
                SaveCostDiagnosticsRequested = settings.SaveCostDiagnostics;
                NativeSaveHandOff.Install();
                if (NativeSaveHandOff.Status.Length > 0) log.Warn("Persistent City saves on the main thread: " + NativeSaveHandOff.Status);
                inspector.ShopName = place => foodMarket?.NameOf(place);
                inspector.CurrentOwnShop = ObservedOwnShop;
                settings.FoodChoice.Clamp(message => log.Info(message));
                log.Info("Food menu policy v1: configured="+settings.FoodMenuMaximumOffersPerShop+", effective native maximum="+Math.Min(FoodMenuPolicy.LiveMaximumOffers,settings.FoodMenuMaximumOffersPerShop)+"; reserved product codes grant no sale permission.");
                settings.MarketResearch.Clamp(message => log.Info(message));
                ResearchBilling.Install(); ResearchAgencyDialog.Install(); ResearchMessages.Check();
                settings.Rivals.Clamp(message => log.Info(message));
                log.Info("Rival choice revision 1: estimated availability; weight="+settings.RivalChoiceWeight+", service="+settings.RivalAssumedServiceMinutes+", refresh="+settings.RivalRefreshGameMinutes+", reads/unit="+settings.RivalReadsPerUnit+", service limits "+RivalServiceCalibration.Label+": "+settings.Rivals.ServiceLimitsText());
                settings.Awareness.Clamp(message => log.Info(message));
                settings.Campaigns.Clamp(message => log.Info(message));

                settings.Weather.Clamp(message => log.Info(message));
                log.Info("E1 weather revision 1; missing/failed source is unknown (neutral).");
                log.Info("Food awareness v1: radius="+settings.Awareness.RadiusMetres.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    +", draws/anchor="+settings.Awareness.DrawsPerAnchor+", discovery="+settings.Awareness.DiscoveryProbability.ToString(System.Globalization.CultureInfo.InvariantCulture));
                settings.FoodMemory.Clamp(message => log.Info(message));
                log.Info("Regulars projection: served visits=" + settings.FoodMemoryRegularServedVisits
                    + ", recent days=" + settings.FoodMemoryRegularRecentDays + "; read-only observed feedback.");
                log.Info("Food memory policy B revision 1; next-boundary activation, captured decision contributions.");
                if (!Position.Finite(settings.GameClockFactor) || settings.GameClockFactor < .1 || settings.GameClockFactor > 1)
                    throw new InvalidDataException("GameClockFactor must be between 0.1 and 1 (one third gives three times as much travel time per day).");
                if (settings.MaximumPedestrians < 1 || settings.MaximumPedestrians > 256 || settings.MaximumVehicles < 1 || settings.MaximumVehicles > 64
                    || !Position.Finite(settings.PhysicalRadiusMetres) || settings.PhysicalRadiusMetres < 30 || settings.PhysicalRadiusMetres > 300)
                    throw new InvalidDataException("Physical limits must be 1–256 pedestrians, 1–64 vehicles and a 30–300 metre radius.");
                settings.ReportInteriorSettings(message => log.Info(message));
                settings.ReportStreetLifeSettings(message => log.Info(message));
                if (!settings.Enabled)
                {
                    Say("Persistent City is disabled by a developer configuration override. Remove the override and reload the city to start it.");
                    enabled = false;
                }
            }
            catch (Exception error) { Fail(error.Message); }
        }

        private void UpdateCity()
        {
            UpdateCityClock.Begin();
            try
            {
                if (!PrepareAccountingSaveChange()) return;
                var save = SaveGameManager.Current;
                if (NativeInteriorCityView.Configured && (save == null || UI.Load.LoadScene.isLoading
                    || saveIdentity != null && !ReferenceEquals(saveIdentity, save))) RestoreInterior();
                ArmStartupClock(save);
                if (save == null) return;
                if (saveIdentity != null && !ReferenceEquals(saveIdentity, save))
                    ResetPopulationForSaveChange();
                if (simulation == null)
                {
                    if (initialization != null) { CompleteInitialization(); return; }
                    ArmStartupClock(save);
                    if (startupHold.Wait(UI.Load.LoadScene.isLoading, Time.unscaledDeltaTime))
                    {
                        NativeCityClock.Stop();
                        log?.Warn("Persistent City startup is still waiting after " + StartupClockHold.MaximumWaitSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)
                            + " seconds; native time is released. If native time enters another minute before the city attaches, the saved city is refused as before.");
                    }
                    if (Time.unscaledTime < nextStart) return;
                    nextStart = Time.unscaledTime + 1;
                    TryStart();
                    return;
                }
                if (!BeginAccountingObservations()) return;
                foodMarket?.RefreshDiscovery();
                routeObjects?.Step(preparingPausedCity: Time.timeScale == 0);
                RloOverrules.Poll(Time.unscaledTime, log);
                ModFrames.Charge(GcAllocationSegment.ModPrelude);
                if (Time.unscaledTime >= nextDependencyCheck)
                {
                    long checkStarted = Stopwatch.GetTimestamp();
                    nextDependencyCheck = Time.unscaledTime + 30;
                    var source = ReadRlo("periodic", simulation.State);
                    var continuity = source.Continuity(simulation.State);
                    rloDiffers = continuity == DonorContinuity.Different;
                    maxRloCheckMilliseconds = Math.Max(maxRloCheckMilliseconds, (Stopwatch.GetTimestamp() - checkStarted) * MillisecondsPerTimestamp);
                    if (!DonorReads.Continues(continuity))
                    {
                        Fail("RLO donor catalogue is no longer available or differs from this snapshot (" + continuity + ", " + source.Reason + "): " + source.Message + " Existing saved state is preserved.");
                        return;
                    }
                    if ((continuity == DonorContinuity.Transient) != rloTransient)
                    {
                        rloTransient = !rloTransient;
                        log?.Info(rloTransient ? "RLO transient (" + source.Reason + "): " + source.Message + " The city continues and saves on its saved donor catalogue; no identity is created, renamed or regenerated."
                            : "RLO ready again and matching the saved donor catalogue; the transient window ended.");
                    }
                    gateRefreshDue = true;
                    nextStationScan = Time.unscaledTime + 15;
                }
                else if (gateRefreshDue)
                {
                    gateRefreshDue = false;
                    long gateStarted = Stopwatch.GetTimestamp();
                    string gateChange = routes?.Refresh() ?? "";
                    maxGateCheckMilliseconds = Math.Max(maxGateCheckMilliseconds, (Stopwatch.GetTimestamp() - gateStarted) * MillisecondsPerTimestamp);
                    if (gateChange.Length > 0) { gateChanges++; log?.Info("Public gates changed (" + gateChange + "); road snapshot discarded; a new one follows station preparation and reuses the live road scene."); }
                }
                else if (Time.unscaledTime >= nextStationScan)
                {
                    nextStationScan = float.PositiveInfinity;
                    routes?.RefreshStations();
                }
                long rloCharged = Stopwatch.GetTimestamp();
                ModFrames.Charge(GcAllocationSegment.ModRlo, rloCharged);
                double target = Now();
                if (target < simulation.State.Minute - 0.001)
                {
                    Fail("Game clock moved behind the population snapshot. Reload the matching save; no state was overwritten.");
                    return;
                }
                if (retimeReport != null) { WriteRetimeReport(); long reported = Stopwatch.GetTimestamp(); WaitForRetime(reported); CloseRetimeReport(rloCharged, reported, "own update"); return; }
                if (routeRebuild != null) { long rebuildStarted = Stopwatch.GetTimestamp(); var rebuildMark = RetimeMark(); ContinueRouteRebuild(rebuildMark, rebuildStarted); WaitForRetime(); return; }
                if (vendorMarket?.Prepare((long)target) == true) { ModFrames.Charge(GcAllocationSegment.ModFood); return; }
                foodMarket?.RefreshRoutineHours(target);
                RefreshBusinessTypes(simulation.State.Places, target, "hour");
                ModFrames.Charge(GcAllocationSegment.ModFood);
                simulation.RefreshOptionalStops();
                ModFrames.Detail(GcAllocationSegment.ModOptionalStopsRefresh);
                RunPhysical(physicalBeforeAdvance ??= () => physical?.BeforeAdvance());
                ModFrames.Detail(GcAllocationSegment.ModOptionalStopsPhysical);
                ModFrames.Charge(GcAllocationSegment.ModOptionalStops);
                double clockRate = NativeTravelPace.ClockRate;
                long retimeStarted = Stopwatch.GetTimestamp();
                if (retimeDriver.Holding && physical == null && (retimedRoutes == null || retimedRoutesRate != clockRate)
                    && ClockRetimeStages.Next(clockRate, simulation.State.TravelClockRate, false) != ClockRetimeStage.None)
                {
                    var routesMark = RetimeMark();
                    retimedRoutes = new MultimodalRoutes(simulation.State.Places, TravelTiming.WalkingMetresPerMinute(NativeTravelPace.WalkingMetresPerSecond, clockRate), clockRate, dependency, routeObjects);
                    retimedRoutesRate = clockRate;
                    retimeAllocation.Add(RetimeAllocationStage.Routes, routesMark, GcAllocationCounter.Mark());
                    retimeFrames.Observe(RetimeFrameStage.Routes, (Stopwatch.GetTimestamp() - retimeStarted) * MillisecondsPerTimestamp, "", Time.frameCount);
                    WaitForRetime(); return;
                }
                double retimeBudget = frameBudget.Exclusive(UpdateCityClock.Elapsed(), ClockRetimeStages.SliceMilliseconds);
                GcAllocationCounter.Probe();
                var retimeMark = GcAllocationCounter.Mark();
                var retime = retimeDriver.Update(simulation.State, clockRate, physical != null, NativeTravelPace.WalkingMetresPerSecond, retimeBudget);
                var updated = retime == ClockRetimeOutcome.None ? retimeMark : GcAllocationCounter.Mark();
                retimeAllocation.Observe(retime);
                if (retime != ClockRetimeOutcome.None)
                {
                    OpenRetimeTally();
                    retimeAllocation.Add(retime == ClockRetimeOutcome.Committed ? RetimeAllocationStage.Commit : RetimeAllocationStage.Journeys, retimeMark, updated);
                }
                if (retime == ClockRetimeOutcome.None)
                {
                    retimeReleaseMilliseconds = 0; retimeReleaseUpdates = 0; retimedRoutes = null; retimeFrames.Reset();
                    if (physical?.ReleasingForRetime == true)
                    {
                        if (ReleaseBodiesForRetime(retimeStarted, retimeBudget, tracked: false)) { physical.Dispose(); physical = null; }
                        else { WaitForRetime(); return; }
                    }
                    else releaseOvershoot.Reset();
                }
                else
                {
                    if (retime == ClockRetimeOutcome.ReleaseBodies)
                    {
                        string releaseStage = physical!.RetimeReleaseStage;
                        if (ReleaseBodiesForRetime(retimeStarted, retimeBudget, tracked: true)) { physical!.Dispose(); physical = null; }
                        retimeReleaseUpdates++;
                        double releaseElapsed = (Stopwatch.GetTimestamp() - retimeStarted) * MillisecondsPerTimestamp;
                        retimeReleaseMilliseconds += releaseElapsed;
                        retimeFrames.Observe(RetimeFrameStage.Release, releaseElapsed, releaseStage, Time.frameCount);
                    }
                    else if (retime == ClockRetimeOutcome.Restarted)
                    {
                        retimeFrames.Observe(RetimeFrameStage.Journeys, (Stopwatch.GetTimestamp() - retimeStarted) * MillisecondsPerTimestamp, "restart", Time.frameCount);
                        log?.Info("Clock retime inputs changed between slices; the retime restarts in slices (restart "
                            + retimeDriver.Restarts.ToString(System.Globalization.CultureInfo.InvariantCulture) + ").");
                    }
                    else if (retime == ClockRetimeOutcome.Committed)
                    {
                        retimeCommitMilliseconds = (Stopwatch.GetTimestamp() - retimeStarted) * MillisecondsPerTimestamp;
                        if (retimeDriver.Degraded > 0)
                            log?.Info("Clock retime degraded " + retimeDriver.Degraded.ToString(System.Globalization.CultureInfo.InvariantCulture)
                                + " journeys: each keeps its old pace on its current road edge or drives outside traffic until its next road section.");
                        var swapped = retimedRoutes != null && retimedRoutesRate == clockRate ? retimedRoutes
                            : new MultimodalRoutes(simulation.State.Places, TravelTiming.WalkingMetresPerMinute(NativeTravelPace.WalkingMetresPerSecond, clockRate), clockRate, dependency, routeObjects);
                        retimedRoutes = null;
                        if (foodMarket != null) log?.Info(FoodClockReach.RetimeLine(clockRate, swapped.WalkingMetresPerMinute));
                        if (routes != null) { swapped.ReuseRoadSceneForRetiming(routes); routes.ReleaseRetainedPaths(); }
                        routes = swapped;
                        seenRoadSnapshots = 0; clockRetimingRoutes = true;
                        var swapMark = GcAllocationCounter.Mark();
                        retimeAllocation.Add(RetimeAllocationStage.Routes, updated, swapMark);
                        routeRebuild = simulation.BeginRebuildRoutes(routes, retimeDriver.TakeRetimedTraffic());
                        retimeAllocation.Add(RetimeAllocationStage.Rebuild, swapMark, GcAllocationCounter.Mark());
                        rebuildOvershoot.Reset();
                        retimeFrames.Observe(RetimeFrameStage.Commit, (Stopwatch.GetTimestamp() - retimeStarted) * MillisecondsPerTimestamp, "", Time.frameCount);
                    }
                    else retimeFrames.Observe(RetimeFrameStage.Journeys, (Stopwatch.GetTimestamp() - retimeStarted) * MillisecondsPerTimestamp, "", Time.frameCount);
                    WaitForRetime(); return;
                }
                ModFrames.Detail(GcAllocationSegment.ModRoutesRetime);
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
                        + ", scene searches in this planner=" + routes.RoadSceneSearches + ", reused unchanged snapshot=" + routes.LastRoadSnapshotReused + ".");
                }
                seenRoadSnapshots = snapshots;
                ModFrames.Charge(GcAllocationSegment.ModRoutes);
                if (!prepared)
                {
                    RecordRoutingFrame(prepareElapsed, 0, false); waitingFrames++;
                    PhysicalAllocation.Begin(gcSampler);
                    try { RunPhysical(physicalUpdate ??= () => physical?.Update()); }
                    finally { PhysicalAllocation.End(GcAllocationSegment.PhysicalUnattributedWaiting); }
                    inspector.Suspend(); return;
                }
                long closureStarted = Stopwatch.GetTimestamp();
                bool networkReady = simulation.TryRefreshTrafficClosures(frameBudget.Slice(UpdateCityClock.Elapsed(), ClockRetimeStages.SliceMilliseconds));
                double closureElapsed = (Stopwatch.GetTimestamp() - closureStarted) * MillisecondsPerTimestamp;
                if (!networkReady)
                {
                    RecordRoutingFrame(prepareElapsed, closureElapsed, false); waitingFrames++;
                    ModFrames.Charge(GcAllocationSegment.ModClosures);
                    PhysicalAllocation.Begin(gcSampler);
                    try { RunPhysical(physicalUpdate ??= () => physical?.Update()); }
                    finally { PhysicalAllocation.End(GcAllocationSegment.PhysicalUnattributedWaiting); }
                    inspector.Suspend(); return;
                }
                closureFrames++; closureMilliseconds += closureElapsed;
                maxClosureMilliseconds = Math.Max(maxClosureMilliseconds, closureElapsed);
                RecordRoutingFrame(prepareElapsed, closureElapsed, true);
                if (!NativeCityClock.CheckInputEpoch(target, simulation.State.Minute)) { StopAccounting(); return; }
                ModFrames.Charge(GcAllocationSegment.ModClosures);
                bool publishedNetwork = simulation.NetworkPublished;
                if (publishedNetwork) UpdateCityClock.Reserve();
                bool setupWanted = !publishedNetwork && settings.PhysicalEnabled && !physicalFailed && physical == null && !BuildingManager.IsInsideBuilding && !NativeTravelPace.HiddenTravel;
                if (!publishedNetwork && !physicalSetupGate.Reserve(setupWanted))
                {
                    var advanceHeap = GcAllocationBegin();
                    AdvanceAllocation.Begin(gcSampler);
                    try { AdvancePopulation(HoldReplayLimit(Math.Max(AccountingClock.Target(target), simulation.State.Minute))); }
                    finally { AdvanceAllocation.End(); }
                    GcAllocationEnd(GcAllocationSegment.Advance, advanceHeap);
                }
                if (simulation.PurchaseFault is string purchaseFault)
                {
                    log?.Warn("Need purchase native restore failed; the uncertain receipt is kept: " + purchaseFault);
                    StopAccounting("Need purchase native restore failed: " + purchaseFault); return;
                }
                ApplyHoldReplay();
                ModFrames.Charge(GcAllocationSegment.ModHoldReplay);
                double beforeSetup = UpdateCityClock.Elapsed();
                if (setupWanted && physicalSetupGate.Admit(beforeSetup))
                {
                    PhysicalAllocation.Begin(gcSampler);
                    try
                    {
                        physical = new PhysicalPopulation(transform, simulation, routes!, dependency!, log!, settings.MaximumPedestrians, settings.MaximumVehicles, settings.PhysicalRadiusMetres, dwellSpots, settings.MovementFaultAcquirePercent);
                        log?.Info("Physical population setup: " + Milliseconds(UpdateCityClock.Elapsed() - beforeSetup) + " ms after "
                            + Milliseconds(beforeSetup) + " ms of this update, waited " + physicalSetupGate.LastWait.ToString(System.Globalization.CultureInfo.InvariantCulture)
                            + " updates for a light frame (" + physicalSetupGate.Deferred.ToString(System.Globalization.CultureInfo.InvariantCulture) + " deferred updates in total, "
                            + physicalSetupGate.Reserved.ToString(System.Globalization.CultureInfo.InvariantCulture) + " updates reserved without advancement).");
                    }
                    catch (Exception error)
                    {
                        physicalFailed = true;
                        PhysicalPedestrians.DestroyReserve();
                        if (NativeInteriorCityView.Configured) NativeInteriorCityView.FailClosed();
                        log?.Warn("Physical population unavailable; ordinary spawning restored, persistent background simulation remains active: " + error);
                    }
                    finally { PhysicalAllocation.End(GcAllocationSegment.PhysicalUnattributedSetup); }
                }
                ModFrames.Charge(GcAllocationSegment.ModPhysicalSetup);
                PhysicalAllocation.Begin(gcSampler);
                double physicalStarted = UpdateCityClock.Elapsed();
                try { RunPhysical(physicalUpdate ??= () => physical?.Update()); }
                finally { PhysicalAllocation.End(); }
                if (physical != null) frameBudget.ObservePhysical(UpdateCityClock.Elapsed() - physicalStarted - UpdateCityClock.BuildMilliseconds);
                UpdateInspector();
                ModFrames.Charge(GcAllocationSegment.ModInspector);
                if (Time.unscaledTime >= nextSummary)
                {
                    nextSummary = Time.unscaledTime + 60;
                    LogPeriodicSummary(simulation.State, target);
                    ResetAdvanceDiagnostics();
                }
                ModFrames.Charge(GcAllocationSegment.ModSummary);
            }
            catch (Exception error) { Fail(error.ToString()); }
        }

        private void AdvancePopulation(double target)
        {
            var active = simulation ?? throw new InvalidOperationException("Population is not active.");
            long started = Stopwatch.GetTimestamp();
            NativeCityClock.ObserveSimulationLag(target, active.State.Minute);
            int events = 0, citizenEvents = 0;
            double elapsed = 0, unitStarted = 0, slowestUnit = 0;
            string slowestKind = "";
            int collectionsBefore = GC.CollectionCount(0);
            var unitSeams = stopEnvironment?.UnitSeams;
            bool unitCpu = unitSeams?.RecordsCpu == true && ThreadCpuClock.Available;
            long callCpu = unitCpu ? ThreadCpuClock.Nanoseconds() : -1;
            stopEnvironment?.Maintain();
            AdvanceAllocation.Detail(GcAllocationSegment.AdvanceStops);
            bool pendingAtEnd = false, completed = false;
            unitSeams?.BeginCall(callCpu);
            unitStarted = (Stopwatch.GetTimestamp() - started) * MillisecondsPerTimestamp;
            unitSeams?.Maintained(unitStarted, unitCpu ? ThreadCpuClock.Nanoseconds() : -1);
            void Unit(string kind)
            {
                unitSeams?.EndUnitAt(kind, elapsed, unitCpu ? ThreadCpuClock.Nanoseconds() : -1);
                if (elapsed - unitStarted > slowestUnit) { slowestUnit = elapsed - unitStarted; slowestKind = kind; }
                unitStarted = elapsed;
            }
            try
            {
                bool hiddenSkip = NativeCityClock.ProcessingEconomySkip;
                var budget = new PopulationFrameBudget(Time.unscaledDeltaTime, hiddenSkip);
                int citizenLimit = budget.CitizenEvents;
                double timeBudget = hiddenSkip ? budget.Milliseconds : frameBudget.Advance(budget.Milliseconds, UpdateCityClock.Elapsed());
                try
                {
                    for (int step = 0; step < budget.Units; step++)
                    {
                        if (routes != null && !routes.ReadyForPopulation) { pendingAtEnd = active.PlanningPending; break; }
                        if (active.PlanningPending)
                        {
                            long sliceStarted = Stopwatch.GetTimestamp();
                            long queriesBefore = routes?.Metrics.NavMeshPathQueries ?? 0;
                            routes?.ResetSliceAnatomy();
                            bool finished = active.ContinuePlanning(Math.Max(0, timeBudget - elapsed));
                            AdvanceAllocation.Sub(finished ? GcAllocationSegment.AdvancePlanningFinish : GcAllocationSegment.AdvancePlanningSlice);
                            AdvanceAllocation.Detail(GcAllocationSegment.AdvancePlanning);
                            double slice = (Stopwatch.GetTimestamp() - sliceStarted) * MillisecondsPerTimestamp;
                            planningSlices++; planningMilliseconds += slice;
                            if (slice > maxPlanningSliceMilliseconds)
                            {
                                maxPlanningSliceMilliseconds = slice;
                                slowestPlanningUnit = routes?.LastJobUnit ?? "";
                                slowestPlanningUnitNavMeshQueries = (routes?.Metrics.NavMeshPathQueries ?? 0) - queriesBefore;
                                slowestSliceUnitStage = routes?.SliceUnitStage ?? ""; slowestSliceUnitMilliseconds = routes?.SliceUnitMilliseconds ?? 0;
                                slowestSliceUnitNavMeshMilliseconds = routes?.SliceUnitNavMeshMilliseconds ?? 0;
                                slowestSliceUnitNavMeshQueries = routes?.SliceUnitNavMeshQueries ?? 0;
                                slowestSliceUnitNavMeshSamples = routes?.SliceUnitNavMeshSamples ?? 0;
                                slowestSliceJobMilliseconds = routes?.LastContinueMilliseconds ?? 0;
                            }
                            elapsed = (Stopwatch.GetTimestamp() - started) * MillisecondsPerTimestamp;
                            Unit("route-planning");
                            if (!finished) { if (elapsed >= timeBudget || routes?.JobAdmissionExhaustedForFrame == true) { pendingAtEnd = active.PlanningPending; break; } continue; }
                            events++; citizenEvents++; planningEvents++;
                            if (citizenEvents >= citizenLimit || elapsed >= timeBudget) break;
                            continue;
                        }
                        var result = active.AdvanceFrameSlice(target, 1);
                        slowestFoodContinuation.Observe(result.FoodContinuationEvents > 0, active.LastFoodContinuationMilliseconds, active.FoodContinuationStep);
                        AdvanceAllocation.Sub(GcPauseSampler.AdvanceEventSegment(result.TrafficEvents, result.FoodContinuationEvents, result.CitizenEvents, result.PlanningPending));
                        AdvanceAllocation.Detail(GcAllocationSegment.AdvanceEvents);
                        events += result.Events;
                        citizenEvents += result.CitizenEvents;
                        elapsed = (Stopwatch.GetTimestamp() - started) * MillisecondsPerTimestamp;
                        Unit(result.PlanningPending ? "citizen-route-begin" : result.TrafficEvents > 0 ? "traffic"
                            : result.FoodContinuationEvents > 0 ? "food-continuation" : result.CitizenEvents > 0 ? "citizen" : "other");
                        if (result.PlanningPending) { pendingAtEnd = elapsed >= timeBudget; if (pendingAtEnd) break; continue; }
                        if (result.CaughtUp || result.ThroughRoutePending || citizenEvents >= citizenLimit || elapsed >= timeBudget) break;
                    }
                }
                finally { active.SynchronizeSnapshot(); NativeCityClock.ObserveSimulationLag(target, active.State.Minute); }
                elapsed = (Stopwatch.GetTimestamp() - started) * MillisecondsPerTimestamp;
                Unit("snapshot");
                completed = true;
            }
            finally { unitSeams?.EndCall(completed); }
            ObserveStopCrossings(unitSeams, elapsed);
            if (elapsed > maxAdvanceMilliseconds)
            {
                slowestAdvanceUnitMilliseconds = slowestUnit; slowestAdvanceUnit = slowestKind;
                slowestAdvanceCollections = GC.CollectionCount(0) - collectionsBefore;
                ObserveSlowestAdvanceStops(unitSeams);
            }
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

        private void ObserveStopCrossings(OptionalStopUnitSeams? unitSeams, double advanceMilliseconds)
        {
            if (unitSeams == null || !unitSeams.Completed || unitSeams.MaximumUnitSeamMilliseconds <= OptionalStopUnitSeams.ThresholdMilliseconds) return;
            observedStopCrossingAdvances++;
            observedStopCrossingUnits += unitSeams.UnitsOverThreshold();
            if (unitSeams.MaximumUnitSeamMilliseconds > observedStopCrossingMaximumMilliseconds)
            {
                observedStopCrossingMaximumMilliseconds = unitSeams.MaximumUnitSeamMilliseconds;
                observedStopCrossingMaximumKind = unitSeams.MaximumUnitSeamKind;
            }
            if (observedStopCrossingAdvances > ListedStopCrossingAdvances) return;
            observedStopCrossingText += (observedStopCrossingText.Length == 0 ? "" : " | ") + "advance "
                + advanceMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " ms: " + unitSeams.OverThresholdText();
        }

        private void ObserveSlowestAdvanceStops(OptionalStopUnitSeams? unitSeams)
        {
            slowestAdvanceStopMilliseconds = unitSeams?.SlowestUnitSeamMilliseconds ?? 0;
            slowestAdvanceCallStopMilliseconds = unitSeams?.CallSeamMilliseconds ?? 0;
            slowestAdvanceMaximumUnitStopMilliseconds = unitSeams?.MaximumUnitSeamMilliseconds ?? 0;
            slowestAdvanceP95UnitStopMilliseconds = unitSeams?.P95UnitSeamMilliseconds() ?? 0;
            slowestAdvanceStopUnits = unitSeams?.Units ?? 0;
            slowestAdvanceUnitsOverStopThreshold = unitSeams?.UnitsOverThreshold() ?? 0;
            slowestAdvanceUnitsOverStopText = slowestAdvanceUnitsOverStopThreshold == 0 ? "" : unitSeams!.OverThresholdText();
        }

        private void UpdateInspector()
        {
            if (inspectorFailed || simulation == null) return;
            try { inspector.Attach(inspectorWarning ??= message => log?.Warn(message)); inspector.Update(simulation, physical, economyRect); }
            catch (Exception error)
            {
                inspectorFailed = true; inspector.Reset();
                log?.Warn("Resident inspector stopped; the city continues unchanged: " + error);
            }
        }

        private Action? physicalBeforeAdvance, physicalUpdate, physicalCatchLoads;
        private bool gateRefreshDue;
        private readonly UpdateCityBudget frameBudget = new UpdateCityBudget();

        private void RunPhysical(Action action)
        {
            try { action(); }
            catch (Exception error)
            {
                physicalFailed = true;
                if (NativeInteriorCityView.Configured) NativeInteriorCityView.FailClosed();
                try { physical?.Dispose(); }
                finally { physical = null; PhysicalPedestrians.DestroyReserve(); }
                log?.Warn("Physical population stopped; ordinary spawning restored and background simulation continues: " + error);
            }
        }

        private double retimeReleaseMilliseconds, retimedRoutesRate;
        private int retimeReleaseUpdates;
        private readonly ClockRetimeDriver retimeDriver = new ClockRetimeDriver();
        private MultimodalRoutes? retimedRoutes;
        private RouteRebuild? routeRebuild, retimeReport;
        private readonly UnitOvershoot releaseOvershoot = new UnitOvershoot(), rebuildOvershoot = new UnitOvershoot();
        private double retimeCommitMilliseconds;
        private static string Milliseconds(double value) => value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        private readonly RetimeAllocationTally retimeAllocation = new RetimeAllocationTally();
        private readonly RetimeFrameLedger retimeFrames = new RetimeFrameLedger();

        private void OpenRetimeTally()
        {
            if (retimeAllocation.Open) return;
            long heap = -1, used = -1;
            try { heap = UnityEngine.Profiling.Profiler.GetMonoHeapSizeLong(); used = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong(); } catch (Exception) { }
            retimeAllocation.Begin(heap, used);
        }

        private RetimeAllocationMark RetimeMark() { OpenRetimeTally(); return GcAllocationCounter.Mark(); }

        private bool ReleaseBodiesForRetime(long started, double budgetMilliseconds, bool tracked)
        {
            var mark = tracked ? RetimeMark() : GcAllocationCounter.Mark();
            long deadline = releaseOvershoot.Deadline(started, budgetMilliseconds);
            bool released = physical!.ReleaseForRetime(deadline);
            releaseOvershoot.Observe(deadline, Stopwatch.GetTimestamp());
            retimeAllocation.Add(RetimeAllocationStage.Release, mark, GcAllocationCounter.Mark());
            return released;
        }

        private void ContinueRouteRebuild(RetimeAllocationMark mark, long started)
        {
            var rebuild = routeRebuild!;
            long deadline = rebuildOvershoot.Deadline(Stopwatch.GetTimestamp(), frameBudget.Exclusive(UpdateCityClock.Elapsed(), ClockRetimeStages.SliceMilliseconds));
            bool done = rebuild.Step(deadline);
            rebuildOvershoot.Observe(deadline, Stopwatch.GetTimestamp());
            if (done) { FinishRouteRebuild(rebuild, mark, started, "with the last rebuild step", reportNow: false); return; }
            retimeAllocation.Add(RetimeAllocationStage.Rebuild, mark, GcAllocationCounter.Mark());
            retimeFrames.Observe(RetimeFrameStage.Rebuild, (Stopwatch.GetTimestamp() - started) * MillisecondsPerTimestamp, "", Time.frameCount);
        }

        private void FinishRouteRebuild(RouteRebuild rebuild) => FinishRouteRebuild(rebuild, RetimeMark(), Stopwatch.GetTimestamp(), "save", reportNow: true);

        private void FinishRouteRebuild(RouteRebuild rebuild, RetimeAllocationMark mark, long started, string detail, bool reportNow)
        {
            if (rebuild.Result == null && !rebuild.Step(long.MaxValue)) throw new InvalidOperationException("The route rebuild did not finish.");
            routeRebuild = null;
            simulation = rebuild.TakeResult();
            ForgetStaleHoldReplay();
            vendorMarket?.Rebind(simulation);
            RebindVendorDecisionDiagnostics(simulation);
            ForgetResearchSnapshot();
            seenPlanningRequests = seenEventRestarts = seenInlineRestarts = 0;
            retimeAllocation.Add(RetimeAllocationStage.Rebuild, mark, GcAllocationCounter.Mark());
            CloseRetimeTally();
            retimeFrames.Observe(RetimeFrameStage.HandOver, (Stopwatch.GetTimestamp() - started) * MillisecondsPerTimestamp, detail, Time.frameCount);
            retimeReport = rebuild;
            if (reportNow) { long reportStarted = Stopwatch.GetTimestamp(); WriteRetimeReport(); CloseRetimeReport(reportStarted, Stopwatch.GetTimestamp(), "save"); }
        }

        private void CloseRetimeTally()
        {
            if (!retimeAllocation.Open) OpenRetimeTally();
            long heap = -1, used = -1;
            try { heap = UnityEngine.Profiling.Profiler.GetMonoHeapSizeLong(); used = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong(); } catch (Exception) { }
            retimeAllocation.Finish(heap, used);
        }

        private void WriteRetimeReport()
        {
            var rebuild = retimeReport!;
            retimeReport = null;
            log?.Info("Clock retime: body release " + Milliseconds(retimeReleaseMilliseconds) + " ms over "
                + retimeReleaseUpdates.ToString(System.Globalization.CultureInfo.InvariantCulture) + " updates, journey retime "
                + Milliseconds(retimeDriver.SliceMilliseconds) + " ms over " + retimeDriver.Slices.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " updates (snapshot " + retimeDriver.SnapshotSlices.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ", slowest " + Milliseconds(retimeDriver.SlowestSliceMilliseconds) + " ms, restarts "
                + retimeDriver.Restarts.ToString(System.Globalization.CultureInfo.InvariantCulture) + "), final slice and commit " + Milliseconds(retimeCommitMilliseconds)
                + " ms, simulation rebuild " + Milliseconds(rebuild.Milliseconds) + " ms over "
                + rebuild.Updates.ToString(System.Globalization.CultureInfo.InvariantCulture) + " updates (slowest " + Milliseconds(rebuild.SlowestUpdateMilliseconds) + " ms); journeys with a split leg "
                + retimeDriver.Split.ToString(System.Globalization.CultureInfo.InvariantCulture) + ", earlier splits joined "
                + retimeDriver.Joined.ToString(System.Globalization.CultureInfo.InvariantCulture) + ", joins refused for an inexact pose "
                + retimeDriver.JoinsRefused.ToString(System.Globalization.CultureInfo.InvariantCulture) + ", shared leg copies "
                + retimeDriver.SharedLegs.ToString(System.Globalization.CultureInfo.InvariantCulture) + ", earlier shared legs reused "
                + retimeDriver.ReusedLegs.ToString(System.Globalization.CultureInfo.InvariantCulture) + ", traffic retimed from " + retimeDriver.RetimedFrom + ".");
            log?.Info("Clock retime allocation: " + retimeAllocation.Describe(GcAllocationCounter.Name) + ".");
            log?.Info(retimeFrames.Describe());
        }

        private void CloseRetimeReport(long started, long ended, string detail)
        {
            retimeFrames.Observe(RetimeFrameStage.Report, (ended - started) * MillisecondsPerTimestamp, detail, Time.frameCount);
            bool over = retimeFrames.ReportOverBound;
            string breach = over ? retimeFrames.DescribeReport() : "";
            retimeFrames.Reset();
            retimeReleaseMilliseconds = 0; retimeReleaseUpdates = 0;
            if (over) log?.Info(breach);
        }

        private void WaitForRetime() => WaitForRetime(Stopwatch.GetTimestamp());

        private void WaitForRetime(long timestamp)
        {
            ModFrames.Detail(GcAllocationSegment.ModRoutesRetime, timestamp);
            ModFrames.Charge(GcAllocationSegment.ModRoutes);
            waitingFrames++;
            inspector.Suspend();
        }

        private void RecordRoutingFrame(double preparation, double closure, bool prepared)
        {
            if (preparation < 16 && closure < 16) return;
            slowRoutingFrames++;
            if (routingFrameLogs >= 4) return;
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
            slowestSliceUnitStage = ""; slowestSliceUnitMilliseconds = slowestSliceUnitNavMeshMilliseconds = slowestSliceJobMilliseconds = 0;
            slowestSliceUnitNavMeshQueries = 0; slowestSliceUnitNavMeshSamples = 0;
            slowestFoodContinuation.Reset();
            closureFrames = 0; closureMilliseconds = maxClosureMilliseconds = 0;
            maxAdvanceEvents = 0;
            slowestAdvanceUnitMilliseconds = slowestAdvanceStopMilliseconds = slowestAdvanceCallStopMilliseconds = 0; slowestAdvanceUnit = ""; slowestAdvanceCollections = 0;
            slowestAdvanceMaximumUnitStopMilliseconds = slowestAdvanceP95UnitStopMilliseconds = 0;
            slowestAdvanceStopUnits = slowestAdvanceUnitsOverStopThreshold = 0; slowestAdvanceUnitsOverStopText = "";
            observedStopCrossingUnits = observedStopCrossingAdvances = 0; observedStopCrossingMaximumMilliseconds = 0;
            observedStopCrossingMaximumKind = observedStopCrossingText = "";
            stopEnvironment?.ResetSeamDiagnostics();
            gateChanges = roadSnapshots = waitingFrames = 0;
            roadRebuildReasons.Clear(); slowRoutingFrames = routingFrameLogs = 0;
            maxRloCheckMilliseconds = maxGateCheckMilliseconds = maxStationSearchMilliseconds = maxPrepareMilliseconds = 0;
        }

        private void TryStart()
        {
            if (!CanStartAccountingSave()) return;
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
            string? epochRefusal = SaveEpochStore.CheckForStart(save.modData, out var epochMarker);
            if (epochRefusal != null) { Fail("Save-epoch check refused: " + epochRefusal + " Every saved key is retained."); return; }
            save.modData.TryGetValue(SaveEpochStore.Key, out var epochText);
            initializingHadPayload = save.modData.TryGetValue(SaveStore.StateKey, out initializingPayload);
            var saved = new Dictionary<string, string>(StringComparer.Ordinal);
            if (initializingHadPayload) saved.Add(SaveStore.StateKey, initializingPayload!);
            var workplaces = new List<CensusWorkplace>();
            PublicRoadAccess.ForgetTally();
            var places = GamePlaces.Read(workplaces);
            CaptureCensusCatalogue(places, workplaces);
            log?.Info(OutdoorPlaces.LastDiscoverySummary);
            double minute = Now();
            double clockRate = NativeTravelPace.InitialClockRate(settings.GameClockFactor);
            if (!NativeCityClock.Active) StartCityClock(save);
            startupHold.Initializing();
            initializingSave = save;
            bool timed = timeActivation = saveCost != null;
            if (timed) { activationHadPayload = initializingHadPayload; activationPayloadCharacters = initializingPayload?.Length ?? 0; }
            BeginInitializingSaveEpoch(save, save.modData, epochMarker?.Sequence ?? 0);
            initialization = Task.Run(() =>
            {
                long readStarted = timed ? Stopwatch.GetTimestamp() : 0;
                var session = SaveSession.Open(saved, source.PopulationKey);
                var stored = session.InitialReadResult;
                long created = timed ? Stopwatch.GetTimestamp() : 0;
                CityState state;
                if (stored.Status == SaveReadStatus.Missing)
                    state = Population.CreateFromHousing(source.Identities, places, source.PopulationKey, source.SourceVersion, source.CitySeed, minute);
                else if (stored.Status == SaveReadStatus.Loaded && stored.State != null)
                {
                    state = stored.State;
                    if (!source.MatchesPopulation(state)) throw new InvalidDataException("Snapshot donor identities differ from current RLO profiles.");
                }
                else throw new InvalidDataException("Population save refused: " + stored.Error);
                string? epochMismatch = SaveEpochStore.DescribesState(epochMarker, stored.SourceSchema, stored.State);
                if (epochMismatch != null) throw new InvalidDataException("Save-epoch check refused: " + epochMismatch);
                NativeTravelPace.PrepareInitialTiming(state, stored.Status == SaveReadStatus.Missing, clockRate);
                long validating = timed ? Stopwatch.GetTimestamp() : 0;
                SaveCodec.ValidateForActivation(state);
                if (!timed) return new PreparedCity { State = state, Saves = session, EpochMarker = epochText, EpochSequence = epochMarker?.Sequence ?? 0 };
                long validated = Stopwatch.GetTimestamp();
                return new PreparedCity { State = state, Saves = session, EpochMarker = epochText, EpochSequence = epochMarker?.Sequence ?? 0,
                    ReadMilliseconds = (created - readStarted) * MillisecondsPerTimestamp,
                    CreateMilliseconds = (validating - created) * MillisecondsPerTimestamp, ValidateMilliseconds = (validated - validating) * MillisecondsPerTimestamp };
            });
            Say("Preparing housing-based background population. Existing saved state is retained until initialization succeeds.");
        }

        public void ArmForCityLoad()
        {
            if (!enabled || failed) return;
            try { ArmStartupClock(SaveGameManager.Current); }
            catch (Exception error) { Fail(error.Message); }
        }

        private void ArmStartupClock(object? save)
        {
            if (simulation != null || initialization != null) return;
            if (startupHold.ForSave(save) && NativeCityClock.Bound) NativeCityClock.Stop();
            if (startupHold.Begin() || startupHold.Holding && !NativeCityClock.Armed && !NativeCityClock.Bound) NativeCityClock.Arm();
        }

        private void StartCityClock(object save)
        {
            NativeCityClock.Start(save, settings.GameClockFactor,
                () => simulation != null && !retimeDriver.Holding && routeRebuild == null && routes?.ReadyForPopulation == true && simulation.IsCaughtUpTo(AccountingClock.Target(Now())),
                () => true, Now, () => simulation?.State.Minute ?? Now(), native => simulation == null || simulation.IsAccountingEpochDrained(native));
            NativeCityClock.StartupHold = true;
        }

        private void CompleteInitialization()
        {
            var pending = initialization;
            if (pending == null || !pending.IsCompleted) return;
            if (!timeActivation) { CompleteInitialization(pending); return; }
            long started = Stopwatch.GetTimestamp();
            try { CompleteInitialization(pending); }
            finally
            {
                var done = pending.Status == TaskStatus.RanToCompletion ? pending.Result : null;
                RecordActivation(started, done != null && simulation != null && ReferenceEquals(simulation.State, done.State) && subscribed,
                    done?.ReadMilliseconds ?? 0, done?.CreateMilliseconds ?? 0, done?.ValidateMilliseconds ?? 0);
            }
        }

        private void CompleteInitialization(Task<PreparedCity> pending)
        {
            initialization = null;
            var save = SaveGameManager.Current;
            var expectedSave = initializingSave;
            var expectedPayload = initializingPayload;
            initializingSave = null;
            initializingPayload = null;
            if (pending.IsFaulted)
            {
                string error = pending.Exception?.GetBaseException().Message ?? "Unknown initialization failure.";
                if (ReferenceEquals(expectedSave, save)) Fail(error + " Original payload retained.");
                else { NativeCityClock.Stop(); DropSaveEpoch(); }
                return;
            }
            if (pending.IsCanceled || save == null || !ReferenceEquals(expectedSave, save)) { NativeCityClock.Stop(); DropSaveEpoch(); return; }
            bool hasPayload = save.modData.TryGetValue(SaveStore.StateKey, out var currentPayload);
            if (hasPayload != initializingHadPayload || currentPayload != expectedPayload)
            { Fail("Population payload changed during initialization. Original payload retained."); return; }
            var prepared = pending.Result;
            var state = prepared.State;
            save.modData.TryGetValue(SaveEpochStore.Key, out var currentEpoch);
            if (!ReferenceEquals(currentEpoch, prepared.EpochMarker)) { Fail("Save-epoch marker changed during initialization. Every saved key is retained."); return; }
            var source = ReadRlo("activation");
            rloDiffers = source.Status == RloReadStatus.Ready && !source.MatchesPopulation(state);
            if (!source.MatchesPopulation(state)) { Fail("RLO donor catalogue changed during initialization (" + source.Continuity(state) + ", " + source.Reason + "). Original payload retained."); return; }
            rloTransient = false;
            if (state.Minute > Now() + 0.001) { Fail("Population snapshot is ahead of this save's clock; original payload retained."); return; }
            if (!AccountingClock.Compatible(state.Minute, Now()))
            { StopAccounting(); return; }
            if (state.Food == null && state.Portals.Count == 0 && state.Visitors.Count == 0)
            {
                PortalPlaces.Add(state, NativePortalStations.Read());
                log?.Info("Visitor portals: " + state.Portals.Count + " subway stations.");
            }
            businessTypeHour = long.MinValue;
            RefreshBusinessTypes(state.Places, state.Minute, "load");
            routeObjects = new RouteObjectDiscovery();
            routes = new MultimodalRoutes(state.Places, TravelTiming.WalkingMetresPerMinute(NativeTravelPace.WalkingMetresPerSecond, state.TravelClockRate), state.TravelClockRate, dependency, routeObjects);
            simulation = new CitySimulator(state, routes);
            simulation.AttachWorkplaceAvailability(new NativeWorkplaceAvailability(state.Places));
            ordinaryOwnershipIndex = NativeOrdinaryOwnership.Create(state.Places, state.Minute);
            simulation.AttachOrdinaryExchanges(ordinaryOwnershipIndex, new OrdinaryVisitPrices(settings.OrdinaryRetailVisitCents, settings.OrdinaryLeisureVisitCents));
            if (!simulation.IsAccountingEpochDrained(Now()))
            { StopAccounting(); return; }
            var binding = prepared.Saves.BindTo(save.modData);
            if (!binding.Success) { Fail("Population save binding refused: " + binding.Error); return; }
            saveSession = prepared.Saves;
            dwellSpots = new DwellSpots();
            var stopEnvironment=this.stopEnvironment=new OptionalStopEnvironment(state.Places,dwellSpots);
            simulation.AttachOptionalStops(stopEnvironment, settings.OptionalStopChoicePercentage, settings.StopTiming());
            simulation.AttachWeather(new FrameWeatherSource(()=>UnityEngine.Time.frameCount,()=>stopEnvironment.Raining),settings.Weather);
            simulation.AttachThroughTraffic(new ThroughTrafficSettings(settings.ThroughTrafficShare));
            simulation.HouseholdOutingsEnabled = settings.HouseholdOutingsEnabled;
            seenPlanningRequests = seenEventRestarts = seenInlineRestarts = 0;
            if (state.Food == null)
                state.Food = new FoodCommerceState { MealRhythmVersion = MealRhythm.PolicyVersion, HouseholdFinanceVersion = HouseholdFinance.PolicyVersion,
                    RepresentationFactor = CityBalance.ConsumerRepresentationFactor,
                    Districts = FoodHouseholdCatalogue.Read(state.Places.Where(p => p.Kind == PlaceKind.Home && !PortalPlaces.Is(p))
                        .Select(p => p.District).Where(d => !string.IsNullOrWhiteSpace(d)).Distinct(StringComparer.Ordinal).ToArray()).ToList() };
            if (state.Food.HouseholdFinanceVersion != HouseholdFinance.PolicyVersion || state.Food.MealRhythmVersion != MealRhythm.PolicyVersion)
                throw new InvalidDataException("Unsupported experimental food policy; preserve this city and use a fresh development baseline.");
            foodMarket = new NativeFoodMarket(state.Food, state.Places, rivalSettings: settings.Rivals, rivalWarning: message => log?.Warn(message), maximumOffers: settings.FoodChoice.FoodMenuMaximumOffersPerShop, economy: state.Economy);
            simulation.AttachFoodMarket(ComposeFoodMarkets(foodMarket), settings.FoodChoice, settings.FoodMemory, settings.Awareness, campaign: foodMarket.AttachCampaignAwareness(state.Seed, settings.Campaigns), rivalSettings: settings.Rivals);
            if (settings.NeedPurchasesEnabled)
            {
                int abandoned = PurchaseContract.Recover(save.modData);
                PurchaseContract.Prune(save.modData, (int)(state.Minute / 1440) - 1);
                simulation.AttachPurchases(new NativePurchaseMarket(save.modData, RunSaveEpochUnit));
                log?.Info("Need purchases attached (GR-3); abandoned pending receipts=" + abandoned + ".");
            }
            var fill = new PopulationFillCatalogue(censusPlaces, censusWorkplaces);
            if (fill.Refused) log?.Warn("Population fill detached: census refused the catalogue (" + fill.RefusalReason + "). No person is created or removed.");
            simulation.AttachPopulationFill(fill);
            saveIdentity = save;
            AttachResearch(state);
            NativeInteriorCityView.Start(settings, log!, RestoreInterior);
            NativeTravelPace.ResetReading();
            log?.Info("City clock factor=" + settings.GameClockFactor.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                + "; Driven Rides coordinated=" + NativeCityClock.CoordinatesDrivenRides + ". Movement speed unchanged.");
            LogActivationLines();
            ResetAdvanceDiagnostics();
            if (saveEpoch == null || !saveEpoch.BoundTo(save) || saveEpoch.Sequence != prepared.EpochSequence)
            { Fail("Save epoch changed during initialization. Every saved key is retained."); return; }
            NativeCityClock.StartupHold = false;
            saveEpoch.CompleteInitialization();
            nextDependencyCheck = Time.unscaledTime + 30;
            nextStationScan = float.PositiveInfinity;
            seenRoadSnapshots = 0; clockRetimingRoutes = false;
            Say("Persistent population active: " + state.Citizens.Count + " residents, " + state.Visitors.Count + " visitors, " + state.Places.Count
                + " places. Native-pace walking, subway and car journeys; physical population preflight follows road initialization. Ctrl+F8 opens food business feedback.");
        }

        private static double Now()
        {
            var save = SaveGameManager.Current;
            return save.Day * 1440.0 + save.Hour * 60.0 + save.Minute;
        }

        private readonly System.Text.StringBuilder summaryBuilder = new System.Text.StringBuilder(2048);
        private readonly List<string> summaryReasonKeys = new List<string>(8);
        private void LogPeriodicSummary(CityState state, double target)
        {
            if (log == null) return;
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            int travelling = 0, awaitingRoute = 0, awaitingOpening = 0;
            for (int i = 0; i < state.Citizens.Count; i++)
            {
                var phase = state.Citizens[i].Phase;
                if (phase == CitizenPhase.Travelling) travelling++;
                else if (phase == CitizenPhase.WaitingForRoute) awaitingRoute++;
                else if (phase == CitizenPhase.WaitingForOpening) awaitingOpening++;
            }
            var sb = summaryBuilder;
            sb.Clear();
            sb.Append("Population: ").Append(state.Citizens.Count).Append(" citizens; ").Append(travelling)
                .Append(" travelling, ").Append(awaitingRoute).Append(" awaiting a route, ").Append(awaitingOpening)
                .Append(" awaiting opening; clock ").Append(state.Minute.ToString("F1", culture)).Append(", lag ")
                .Append(Math.Max(0, target - state.Minute).ToString("F1", culture))
                .Append(" game minutes. Advancement this observation: ").Append(advanceFrames).Append(" frames, ").Append(advanceEvents).Append(" events, ")
                .Append(advanceMilliseconds.ToString("F3", culture)).Append(" ms total, ").Append(maxAdvanceMilliseconds.ToString("F3", culture))
                .Append(" ms maximum advance, ").Append(maxAdvanceEvents)
                .Append(" maximum events in one frame; these figures cover advancement calls only. Slowest advance: unit ")
                .Append(slowestAdvanceUnitMilliseconds.ToString("F3", culture)).Append(" ms (")
                .Append(slowestAdvanceUnit.Length == 0 ? "none" : slowestAdvanceUnit).Append("), optional-stop reads ")
                .Append(slowestAdvanceStopMilliseconds.ToString("F3", culture)).Append(" ms, ").Append(slowestAdvanceCollections)
                .Append(" garbage collections completed during it. Optional-stop reads per unit in the slowest advance: ")
                .Append(slowestAdvanceStopUnits).Append(" units, call total ").Append(slowestAdvanceCallStopMilliseconds.ToString("F3", culture))
                .Append(" ms, maximum ").Append(slowestAdvanceMaximumUnitStopMilliseconds.ToString("F3", culture))
                .Append(" ms, p95 ").Append(slowestAdvanceP95UnitStopMilliseconds.ToString("F3", culture))
                .Append(" ms, ").Append(slowestAdvanceUnitsOverStopThreshold).Append(" units over 5 ms [").Append(slowestAdvanceUnitsOverStopText)
                .Append("]. Optional-stop units over 5 ms this observation: ").Append(observedStopCrossingUnits).Append(" units in ")
                .Append(observedStopCrossingAdvances).Append(" advances, maximum ").Append(observedStopCrossingMaximumMilliseconds.ToString("F3", culture))
                .Append(" ms (").Append(observedStopCrossingMaximumKind.Length == 0 ? "none" : observedStopCrossingMaximumKind).Append(") [")
                .Append(observedStopCrossingText);
            if (observedStopCrossingAdvances > ListedStopCrossingAdvances)
                sb.Append(" | +").Append(observedStopCrossingAdvances - ListedStopCrossingAdvances).Append(" more advances");
            sb.Append("]. Optional-stop reads this observation (refresh and advancement): ").Append(stopEnvironment?.SeamCalls ?? 0).Append(" calls, ")
                .Append((stopEnvironment?.SeamMilliseconds ?? 0).ToString("F3", culture)).Append(" ms total, ")
                .Append((stopEnvironment?.MaximumSeamMilliseconds ?? 0).ToString("F3", culture)).Append(" ms maximum, ")
                .Append(stopEnvironment?.ParkTriggerScans ?? 0).Append(" park trigger scans; geometry door samples ")
                .Append(stopEnvironment?.DoorSamples ?? 0).Append(" (maximum ").Append((stopEnvironment?.MaximumDoorMilliseconds ?? 0).ToString("F3", culture))
                .Append(" ms; door search maximum ").Append((stopEnvironment?.Doors.MaximumSearchMilliseconds ?? 0).ToString("F3", culture))
                .Append(" ms over ").Append(stopEnvironment?.Doors.DoorSearches ?? 0).Append(" searches, ").Append(stopEnvironment?.Doors.IndexedSearches ?? 0)
                .Append(" indexed, NavMesh queue maximum ").Append((stopEnvironment?.Doors.MaximumNavigationMilliseconds ?? 0).ToString("F3", culture))
                .Append(" ms, door index builds ").Append(stopEnvironment?.Doors.IndexBuilds ?? 0).Append(" (").Append(stopEnvironment?.Doors.IndexedDoors ?? 0)
                .Append(" doors, ").Append(stopEnvironment?.Doors.PendingRequests ?? 0).Append(" requests waited for the index, slice maximum ")
                .Append((stopEnvironment?.Doors.MaximumIndexSliceMilliseconds ?? 0).ToString("F3", culture)).Append(" ms)), park builds on demand ")
                .Append(stopEnvironment?.ParkBuilds ?? 0).Append(" (maximum ").Append((stopEnvironment?.MaximumParkMilliseconds ?? 0).ToString("F3", culture))
                .Append(" ms with setup), audience builds on demand ").Append(stopEnvironment?.AudienceBuilds ?? 0).Append(" (maximum ")
                .Append((stopEnvironment?.MaximumAudienceMilliseconds ?? 0).ToString("F3", culture)).Append(" ms), warm-up park/audience slices ")
                .Append(stopEnvironment?.ParkWarmup.Slices ?? 0).Append('/').Append(stopEnvironment?.AudienceWarmup.Slices ?? 0)
                .Append(" (setups ").Append(stopEnvironment?.ParkWarmup.Setups ?? 0).Append(", maximum ")
                .Append(Math.Max(stopEnvironment?.ParkWarmup.MaximumMilliseconds ?? 0, stopEnvironment?.AudienceWarmup.MaximumMilliseconds ?? 0).ToString("F3", culture))
                .Append(" ms, failures ").Append((stopEnvironment?.ParkWarmup.Failures ?? 0) + (stopEnvironment?.AudienceWarmup.Failures ?? 0))
                .Append("), frame-budget deferrals ").Append(stopEnvironment?.GeometryDeferrals ?? 0).Append(". Slowest scene scan for optional stops: ")
                .Append((stopEnvironment?.MaximumScanMilliseconds ?? 0).ToString("F3", culture)).Append(" ms (")
                .Append(stopEnvironment?.SlowestScan ?? "none").Append("). Routes: ").Append(roadSnapshots).Append(" road snapshots started (")
                .Append(gateChanges).Append(" for gate changes), ").Append(waitingFrames).Append(" frames waiting for routes; rebuild reasons [");
            summaryReasonKeys.Clear();
            foreach (var key in roadRebuildReasons.Keys) summaryReasonKeys.Add(key);
            summaryReasonKeys.Sort(StringComparer.Ordinal);
            for (int i = 0; i < summaryReasonKeys.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(summaryReasonKeys[i]).Append('=').Append(roadRebuildReasons[summaryReasonKeys[i]]);
            }
            summaryReasonKeys.Clear();
            sb.Append("]; slow routing frames ").Append(slowRoutingFrames).Append(" (first ").Append(routingFrameLogs)
                .Append(" logged); last rebuild ").Append(lastRoadRebuild).Append("; this planner scene searches ")
                .Append(routes?.RoadSceneSearches ?? 0).Append(", maximum ").Append((routes?.MaximumRoadSceneSearchMilliseconds ?? 0).ToString("F3", culture))
                .Append(" ms, station captures ").Append(routes?.StationSearches ?? 0).Append("; discovery sweeps ")
                .Append(routeObjects?.Sweeps ?? 0).Append(", last branch captures ").Append(routeObjects?.LastSweepBranchCaptures ?? 0)
                .Append(", split hints ").Append(routeObjects?.SplitBranches ?? 0).Append(", maximum slice ")
                .Append((routeObjects?.MaximumSliceMilliseconds ?? 0).ToString("F3", culture)).Append(" ms, last sweep ")
                .Append((routeObjects?.LastSweepSeconds ?? 0).ToString("F3", culture)).Append(" s / ")
                .Append((routeObjects?.LastSweepMilliseconds ?? 0).ToString("F3", culture)).Append(" ms work; slowest RLO check ")
                .Append(maxRloCheckMilliseconds.ToString("F3", culture)).Append(" ms (RLO contract bindings this session ")
                .Append(rlo.Bindings).Append(", ").Append(rlo.BindingMilliseconds.ToString("F3", culture)).Append(" ms), door search ")
                .Append(maxGateCheckMilliseconds.ToString("F3", culture)).Append(" ms, station search ")
                .Append(maxStationSearchMilliseconds.ToString("F3", culture)).Append(" ms, preparation frame ")
                .Append(maxPrepareMilliseconds.ToString("F3", culture)).Append(" ms. Closure refresh: ").Append(closureFrames)
                .Append(" calls, ").Append(closureMilliseconds.ToString("F3", culture)).Append(" ms total, ")
                .Append(maxClosureMilliseconds.ToString("F3", culture)).Append(" ms maximum, gate clearance reused ")
                .Append(routes?.LastRoadGateClearanceReused == true).Append(", node validations deferred ")
                .Append(routes?.ClosureValidationsDeferred ?? 0).Append(". Route planning across frames: ").Append(planningRequests)
                .Append(" requests, ").Append(planningSlices).Append(" slices, ").Append(planningEvents).Append(" events completed, ")
                .Append(planningPendingFrames).Append(" frames ended with a plan pending, ").Append(planningMilliseconds.ToString("F3", culture))
                .Append(" ms total, slowest slice ").Append(maxPlanningSliceMilliseconds.ToString("F3", culture)).Append(" ms ending in ")
                .Append(slowestPlanningUnit.Length == 0 ? "none" : slowestPlanningUnit).Append(" with ").Append(slowestPlanningUnitNavMeshQueries)
                .Append(" NavMesh path queries; event restarts ").Append(eventRestarts)
                .Append(" (inline escapes removed in F3; compatibility count ").Append(inlineRestarts).Append("), road search restarts ")
                .Append(routes?.RoadSearchRestarts ?? 0).Append(", units ").Append(routes?.JobUnits ?? 0).Append(", reused plan jobs ")
                .Append(routes?.JobsReused ?? 0).Append(", query/unit admission deferrals ").Append(routes?.JobAdmissionDeferrals ?? 0)
                .Append(", path reserve ms ").Append(routes?.JobPathReserveMilliseconds ?? 0).Append(". ")
                .Append(PlanningSliceAnatomy.Text(maxPlanningSliceMilliseconds, slowestSliceJobMilliseconds, slowestSliceUnitStage, slowestSliceUnitMilliseconds,
                    slowestSliceUnitNavMeshMilliseconds, slowestSliceUnitNavMeshQueries, slowestSliceUnitNavMeshSamples))
                .Append(" Slowest food continuation: ").Append(slowestFoodContinuation.Milliseconds.ToString("F3", culture))
                .Append(" ms (").Append(slowestFoodContinuation.Step.Length == 0 ? "none" : slowestFoodContinuation.Step).Append("). ")
                .Append(physical?.Diagnostics ?? "Physical population inactive.");
            log.Info(sb.ToString());
            if (simulation!.Traffic != null)
            {
                sb.Clear();
                sb.Append("Citywide traffic: ").Append(simulation.Traffic.VehicleCount - simulation.ThroughCarCount).Append(" persistent cars, ")
                    .Append(simulation.ThroughCarCount).Append(" through cars (share ").Append(simulation.ThroughTrafficShare.ToString("0.00", culture))
                    .Append(", ").Append(simulation.ThroughCarsGenerated).Append(" generated), ")
                    .Append(simulation.Traffic.PendingEventCount).Append(" scheduled road events, ")
                    .Append(simulation.Traffic.LinkCount).Append(" road sections.");
                log.Info(sb.ToString());
            }
        }

        private RloReadResult ReadRlo(string context, CityState? expected = null)
        {
            var result = expected != null ? rlo.ReadMatching(dependency, expected) : rlo.Read(dependency);
            lastRloRead = result;
            if (log != null)
            {
                var culture = System.Globalization.CultureInfo.InvariantCulture;
                var sb = summaryBuilder;
                sb.Clear();
                sb.Append("RLO single-pass read context=").Append(context).Append(" status=").Append(result.Status)
                    .Append(" identities=").Append(result.Identities.Count).Append(" elapsedMs=")
                    .Append(rlo.LastReadMilliseconds.ToString("F3", culture)).Append(" reads=").Append(rlo.Reads)
                    .Append(" totalMs=").Append(rlo.TotalReadMilliseconds.ToString("F3", culture))
                    .Append(" maxMs=").Append(rlo.MaximumReadMilliseconds.ToString("F3", culture));
                log.Info(sb.ToString());
            }
            return result;
        }

        private SaveEpochCapture CaptureForSave(SaveEpochTicket ticket)
        {
            if (failed || simulation == null) return SaveEpochCapture.Refused("Population is not active.");
            var cost = saveCost;
            var heap = cost == null ? default : BeginSaveHeap();
            cost?.Begin();
            var capture = SaveEpochCapture.Refused("Population save failed.");
            try { capture = CaptureSteps(ticket, cost); return capture; }
            catch (Exception exception) { capture = SaveEpochCapture.Refused("Population save failed: " + exception.Message); throw; }
            finally
            {
                if (cost != null)
                {
                    cost.End(capture.Error == null, capture.Error ?? "");
                    EndSaveHeap(heap);
                    if (capture.Error == null && capture.Pending == null) CountSavedRecords(cost, payload => SaveCodec.CountRecords(simulation!.State, payload));
                }
            }
        }

        private SaveEpochCapture CaptureSteps(SaveEpochTicket ticket, SaveCostRecorder? cost)
        {
            if (routeRebuild != null) FinishRouteRebuild(routeRebuild);
            if (!BeginAccountingObservations()) return SaveEpochCapture.Refused("Accounting observations could not begin before saving.");
            cost?.Step(SaveCostStep.Accounting);
            var source = ReadRlo("save");
            var saveContinuity = source.Continuity(simulation!.State);
            rloDiffers = saveContinuity == DonorContinuity.Different;
            if (!DonorReads.Continues(saveContinuity))
                return SaveEpochCapture.Refused("RLO changed before saving; original population payload retained.");
            cost?.Step(SaveCostStep.Rlo);
            RunPhysical(() => physical?.BeforeSave());
            cost?.Step(SaveCostStep.Physical);
            foodMarket?.CompleteCampaignsForSave();
            foodMarket?.RefreshKnownOwnership();
            cost?.Step(SaveCostStep.FoodMarket);
            if (!NativeCityClock.CheckInputEpoch(Now(), simulation.State.Minute))
                return SaveEpochCapture.Refused("Accounting input epoch changed before saving.");
            if (saveEpoch?.CanPublish(ticket, SaveGameManager.Current, SaveGameManager.Current.modData) != true)
                return SaveEpochCapture.Refused("Save epoch changed during capture; previous payload retained.");
            cost?.Step(SaveCostStep.EpochCheck);
            vendorMarket?.CompleteForSave();
            cost?.Step(SaveCostStep.Vendors);
            var session = saveSession ?? throw new InvalidOperationException("Save session is missing.");
            bool background = BackgroundSaveAvailable();
            cost?.Step(SaveCostStep.HandOffCheck);
            if (background) return BeginBackgroundWrite(session, cost);
            synchronousSaves++;
            lastSaveBackground = false;
            var result = session.WriteTo(SaveGameManager.Current.modData, simulation.State, cost);
            return result.Success ? SaveEpochCapture.Written(simulation.State.Minute, simulation.State.PopulationKey)
                : SaveEpochCapture.Refused("Population save refused: " + result.Error);
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

        private void AttachResearch(CityState state)
        {
            if (!ResearchEligibility.SessionEnabled(ResearchAgencyDialog.Installed, ResearchBilling.Installed, ResearchMessages.ContractPassed))
            {
                log?.Warn("Research off for this session; saved research orders are kept unchanged. "
                    + ResearchAgencyDialog.Status + " " + ResearchBilling.Status + " " + ResearchMessages.Status);
                return;
            }
            simulation!.AttachResearch(state.Research, settings.MarketResearch.MarketResearchConsumersPerUnit);
            ResearchBilling.Attach(state.Research, ResearchAttached, ResearchEligible, RunSaveEpochUnit);
            ResearchAgencyDialog.Attach(new ResearchDesk(ResearchAttached, () => settings.MarketResearch.Available, ResearchEligible,
                () => simulation?.Research, () => settings.MarketResearch.MarketResearchCatchmentMetres, Now, message => log?.Info(message)));
            settings.MarketResearch.DeriveAvailability(ResearchActivation);
        }

        private MarketResearchActivation ResearchActivation() => new MarketResearchActivation(ResearchAttached(),
            ResearchAgencyDialog.Installed && ResearchAgencyDialog.Status.Length == 0,
            ResearchBilling.Installed && ResearchBilling.Status.Length == 0 && ResearchBilling.Attached,
            ResearchMessages.ContractPassed, simulation?.Research != null);

        private bool ResearchAttached() => !failed && !accountingStopped && simulation != null && saveEpoch != null
            && saveIdentity != null && ReferenceEquals(saveIdentity, SaveGameManager.Current);

        private bool ResearchEligible(BuildingRegistration registration)
        {
            if (registration == null || !registration.RentedByPlayer || string.IsNullOrEmpty(registration.BusinessName)) return false;
            var food = simulation?.State.Food;
            var market = foodMarket;
            if (food == null || market == null) return false;
            string place = NativeFoodMarket.PlaceId(registration);
            foreach (var scope in food.Shops)
                if (scope.Channel == FoodScopeChannel.Shop && string.Equals(scope.PlaceId, place, StringComparison.Ordinal))
                {
                    bool observed = market.ObservedOwnShop(scope);
                    return ResearchEligibility.Decide(true, scope.BusinessId, observed, observed ? NativeFoodOwnership.Find(registration) : null);
                }
            return false;
        }

        private void Detach()
        {
            foodDecisions.Detach(); ForgetResearchSnapshot();
            FlushSaveHandOff();
            settings.MarketResearch.ClearAvailability(); ResearchAgencyDialog.Detach(); ResearchBilling.Detach();
            DetachSaveEpoch();
            NativeInteriorCityView.Stop();
            vendorMarket?.Dispose(); vendorMarket = null;
            foodMarket?.Dispose(); foodMarket = null; ordinaryOwnershipIndex = null;
            try { NativeSalePath.Unpatch(); } catch (Exception error) { log?.Warn("Sale path unpatch failed: " + error.Message); }
            inspector.Detach();
            try { physical?.Dispose(); }
            finally { physical = null; PhysicalPedestrians.DestroyReserve(); routeObjects?.Dispose(); routeObjects = null; NativeCityClock.Stop(); NativeTravelPace.StopObservation(); }
            if (!subscribed) return;
            GlobalEvents.onSaveGame -= Save;
            GlobalEvents.onGameUnloaded -= Shutdown;
            subscribed = false;
        }

        public void Shutdown()
        {
            ResearchAgencyDialog.Uninstall(); ResearchBilling.Uninstall();
            UnregisterVendorDiagnostics(); UnregisterResearchDiagnostics();
            ResetCensus();
            Detach();
            if (initialization != null)
                _ = initialization.ContinueWith(task => { _ = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            initialization = null;
            initializingSave = null;
            initializingPayload = null;
            simulation = null; stopEnvironment = null; retimeDriver.Cancel(); retimeAllocation.Reset(); retimeFrames.Reset(); retimedRoutes = null; routeRebuild = null; retimeReport = null;
            saveSession = null;
            routes = null;
            saveIdentity = null;
            enabled = false;
            if (metadataLifetime) { FoodProductMetadataApi.Unload(); metadataLifetime = false; }
        }
        private void OnDestroy() => Shutdown();
    }
}
