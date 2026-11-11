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
            // The city-load callback precedes the loaded save's native ticks; Update's order against them is not guaranteed.
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
        // Route planning across frames: slices driven, events that waited for a route, frames that ended with one pending.
        private long planningSlices, planningEvents, planningPendingFrames, seenPlanningRequests, planningRequests;
        // Event restarts are lifetime totals on the simulator; the summary reports the window's share.
        private long eventRestarts, inlineRestarts, seenEventRestarts, seenInlineRestarts;
        private double planningMilliseconds, maxPlanningSliceMilliseconds;
        private string slowestPlanningUnit = "";
        private long slowestPlanningUnitNavMeshQueries;
        // Issue #128 attribution of the slowest advancement call in the observation: its slowest single
        // unit and kind, optional-stop native reads inside it and garbage collections that completed during it.
        private double slowestAdvanceUnitMilliseconds, slowestAdvanceStopMilliseconds;
        private string slowestAdvanceUnit = "";
        private int slowestAdvanceCollections;
        private OptionalStopEnvironment? stopEnvironment;

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
                Districts = FoodHouseholdCatalogue.Read(simulation.State.Places.Where(p => p.Kind == PlaceKind.Home && !PortalPlaces.Is(p))
                    .Select(p => p.District).Where(d => !string.IsNullOrWhiteSpace(d)).Distinct(StringComparer.Ordinal).ToArray()).ToList() };
            var adapter = new NativeFoodMarket(state, simulation.State.Places, enrolling: true, rivalSettings: settings.Rivals, rivalWarning: message => log?.Warn(message), maximumOffers: settings.FoodChoice.FoodMenuMaximumOffersPerShop, economy: simulation.State.Economy);
            try { simulation.State.Food = state; simulation.AttachFoodMarket(ComposeFoodMarkets(adapter), settings.FoodChoice, settings.FoodMemory, settings.Awareness, rivalSettings: settings.Rivals); foodMarket = adapter; }
            catch { vendorMarket?.Dispose(); vendorMarket = null; simulation.State.Food = null; adapter.Dispose(); throw; }
        }
        private IFoodMarket ComposeFoodMarkets(NativeFoodMarket shops)
        {
            var current = simulation ?? throw new InvalidOperationException("Vendor market needs an active city.");
            var save = SaveGameManager.Current;
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "FoodTrucks");
            var api = StreetFoodBridge.Open(assembly, out string reason);
            // E5b4: one product policy for shops and vehicles, from config, the API publication and E5b2's loaded-name image.
            var policy = new FoodProductPolicySource(() => shops.LoadedNames, () => settings.FoodProductOverrides.Resolver, FoodProductMetadataApi.PublishSnapshot,
                message => log?.Warn("Food product policy fell back to the original three: " + message));
            shops.ProductPolicySource = policy; // E5b5: shop admission classes products with the same policy
            // E6f: the same policy decides which vehicle menus are executable; null while the loaded-name image is not
            // published yet (warm-up or rebuild), which the vehicle market does not treat as a refusal. The choice menu
            // width also bounds vehicle menus.
            vendorMarket = new StreetFoodVendorMarket(current, api, reason, new VendorNavMeshSampler(), save.modData, Now,
                () => ReferenceEquals(save, SaveGameManager.Current), () => shops.LoadedNames == null ? null : policy.Current,
                settings.FoodChoice.FoodMenuMaximumOffersPerShop);
            vendorMarket.WorkSpike = (component, ms) => log?.Warn("Vendor mod-work spike >= 30 ms: " + component + "="
                + ms.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " ms");
            AttachVendorDecisionDiagnostics(current);
            return new FoodMarkets(current.State.Food!, shops, vendorMarket, id => simulation!.FindPlace(id).Position, policy);
        }
        public void EnableHouseholdFinances()
        {
            if (simulation == null || failed || !ReferenceEquals(saveIdentity, SaveGameManager.Current))
                throw new InvalidOperationException("A ready current city is required.");
            simulation.EnableHouseholdFinances();
        }
        public object? FoodAwareness(string consumerId, int? day=null) => simulation?.InspectFoodAwareness(consumerId,day);
        public object RivalFoodDiagnostics => new { Core=simulation?.DumpRivalFood(), RefreshUnits=foodMarket?.RivalRefreshUnits ?? 0, DetailReads=foodMarket?.RivalDetailReads ?? 0, Unavailable=foodMarket?.RivalUnavailable ?? "" };
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
                inspector.ShopName = place => foodMarket?.NameOf(place);
                inspector.CurrentOwnShop = ObservedOwnShop;
                settings.FoodChoice.Clamp(message => log.Info(message));
                log.Info("Food menu policy v1: configured="+settings.FoodMenuMaximumOffersPerShop+", effective native maximum="+Math.Min(FoodMenuPolicy.LiveMaximumOffers,settings.FoodMenuMaximumOffersPerShop)+"; reserved product codes grant no sale permission.");
                settings.MarketResearch.Clamp(message => log.Info(message));
                // C5d/C5e native contract checks. A mismatch turns research off with a status line; the city still runs.
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
                if (Time.unscaledTime >= nextDependencyCheck)
                {
                    long checkStarted = Stopwatch.GetTimestamp();
                    nextDependencyCheck = Time.unscaledTime + 30;
                    var source = ReadRlo("periodic");
                    // F8d (D6 option B): the saved catalogue is authoritative after activation. A transient RLO answer (Off,
                    // NotYet, Refreshing) continues on it; a different catalogue or Shared, Failed, Incompatible stops as before.
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
                // Finish at most one vendor continuation unit before advancing any logical event.
                if (vendorMarket?.Prepare((long)target) == true) return;
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
                    vendorMarket?.Rebind(simulation);
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
                if (!NativeCityClock.CheckInputEpoch(target, simulation.State.Minute)) { StopAccounting(); return; }
                AdvancePopulation(HoldReplayLimit(Math.Max(AccountingClock.Target(target), simulation.State.Minute)));
                ApplyHoldReplay(); // F2e: in-game E2 replay only; inactive unless pc movement replay-holds started one
                if (settings.PhysicalEnabled && !physicalFailed && physical == null && !BuildingManager.IsInsideBuilding && !NativeTravelPace.HiddenTravel)
                {
                    try { physical = new PhysicalPopulation(transform, simulation, routes!, dependency!, log!, settings.MaximumPedestrians, settings.MaximumVehicles, settings.PhysicalRadiusMetres, dwellSpots, settings.MovementFaultAcquirePercent); }
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
                        + maxAdvanceEvents + " maximum events in one frame; these figures cover advancement calls only. Slowest advance: unit "
                        + slowestAdvanceUnitMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " ms ("
                        + (slowestAdvanceUnit.Length == 0 ? "none" : slowestAdvanceUnit) + "), optional-stop reads "
                        + slowestAdvanceStopMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " ms, "
                        + slowestAdvanceCollections + " garbage collections completed during it. Optional-stop reads this observation (refresh and advancement): "
                        + (stopEnvironment?.SeamCalls ?? 0) + " calls, "
                        + (stopEnvironment?.SeamMilliseconds ?? 0).ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " ms total, "
                        + (stopEnvironment?.MaximumSeamMilliseconds ?? 0).ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " ms maximum, "
                        + (stopEnvironment?.ParkTriggerScans ?? 0) + " park trigger scans. Routes: "
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
                        + " NavMesh path queries; event restarts " + eventRestarts + " (inline escapes removed in F3; compatibility count "
                        + inlineRestarts + "), road search restarts " + (routes?.RoadSearchRestarts ?? 0)
                        + ", units " + (routes?.JobUnits ?? 0) + ", query/unit admission deferrals " + (routes?.JobAdmissionDeferrals ?? 0)
                        + ", path reserve ms " + (routes?.JobPathReserveMilliseconds ?? 0) + ". "
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
            NativeCityClock.ObserveSimulationLag(target, active.State.Minute);
            int events = 0, citizenEvents = 0;
            double elapsed = 0, unitStarted = 0, slowestUnit = 0;
            string slowestKind = "";
            int collectionsBefore = GC.CollectionCount(0);
            double stopsBefore = stopEnvironment?.SeamMilliseconds ?? 0;
            void Unit(string kind)
            {
                if (elapsed - unitStarted > slowestUnit) { slowestUnit = elapsed - unitStarted; slowestKind = kind; }
                unitStarted = elapsed;
            }
            var budget = new PopulationFrameBudget(Time.unscaledDeltaTime, NativeCityClock.ProcessingEconomySkip);
            int citizenLimit = budget.CitizenEvents;
            double timeBudget = budget.Milliseconds;
            // Always allow the first event or planning unit. A native query cannot be preempted, so
            // the soft time budget can be exceeded by that one unit's duration, never by a whole plan.
            bool pendingAtEnd = false;
            try
            {
                for (int step = 0; step < budget.Units; step++)
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
                        Unit("route-planning");
                        // Not finished: still pending, or the event restarted and is due again unstarted.
                        if (!finished) { if (elapsed >= timeBudget || routes?.JobAdmissionExhaustedForFrame == true) { pendingAtEnd = active.PlanningPending; break; } continue; }
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
                    Unit(result.TrafficEvents > 0 ? "traffic" : result.FoodContinuationEvents > 0 ? "food-continuation"
                        : result.CitizenEvents > 0 ? (result.PlanningPending ? "citizen-route-begin" : "citizen") : "other");
                    if (result.PlanningPending) { pendingAtEnd = elapsed >= timeBudget; if (pendingAtEnd) break; continue; }
                    if (result.CaughtUp || citizenEvents >= citizenLimit || elapsed >= timeBudget) break;
                }
            }
            finally { active.SynchronizeSnapshot(); NativeCityClock.ObserveSimulationLag(target, active.State.Minute); }
            elapsed = (Stopwatch.GetTimestamp() - started) * MillisecondsPerTimestamp;
            Unit("snapshot");
            if (elapsed > maxAdvanceMilliseconds)
            {
                slowestAdvanceUnitMilliseconds = slowestUnit; slowestAdvanceUnit = slowestKind;
                slowestAdvanceCollections = GC.CollectionCount(0) - collectionsBefore;
                slowestAdvanceStopMilliseconds = (stopEnvironment?.SeamMilliseconds ?? 0) - stopsBefore;
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
            slowestAdvanceUnitMilliseconds = slowestAdvanceStopMilliseconds = 0; slowestAdvanceUnit = ""; slowestAdvanceCollections = 0;
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
            // Snapshot all Unity/RLO input on the main thread. Large population creation and
            // strict save decoding are pure work; never hold up a frame while creating 25,000 people.
            string? epochRefusal = SaveEpochStore.CheckForStart(save.modData, out var epochMarker);
            if (epochRefusal != null) { Fail("Save-epoch check refused: " + epochRefusal + " Every saved key is retained."); return; }
            save.modData.TryGetValue(SaveEpochStore.Key, out var epochText);
            initializingHadPayload = save.modData.TryGetValue(SaveStore.StateKey, out initializingPayload);
            var saved = new Dictionary<string, string>(StringComparer.Ordinal);
            if (initializingHadPayload) saved.Add(SaveStore.StateKey, initializingPayload!);
            var workplaces = new List<CensusWorkplace>();
            var places = GamePlaces.Read(workplaces);
            CaptureCensusCatalogue(places, workplaces);
            log?.Info(OutdoorPlaces.LastDiscoverySummary);
            double minute = Now();
            double clockRate = NativeTravelPace.InitialClockRate(settings.GameClockFactor);
            // Hold native time throughout detached initialization and route preparation too.
            // Otherwise the world can cross accounting epochs before its scheduler exists.
            if (!NativeCityClock.Active) StartCityClock(save);
            startupHold.Initializing();
            initializingSave = save;
            // F7a: load timing only when save-cost diagnostics are requested; otherwise no timestamps or fields.
            bool timed = timeActivation = saveCost != null;
            if (timed) { activationHadPayload = initializingHadPayload; activationPayloadCharacters = initializingPayload?.Length ?? 0; }
            // Rule 5: a clean save during initialization is deferred (nothing written, no stop); unit-on-stack,
            // nested, replaced-data and poisoned saves still refuse with both markers.
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
                SaveCodec.ValidateForActivation(state); // Validate completely before making it active or touching modData.
                if (!timed) return new PreparedCity { State = state, Saves = session, EpochMarker = epochText, EpochSequence = epochMarker?.Sequence ?? 0 };
                long validated = Stopwatch.GetTimestamp();
                return new PreparedCity { State = state, Saves = session, EpochMarker = epochText, EpochSequence = epochMarker?.Sequence ?? 0,
                    ReadMilliseconds = (created - readStarted) * MillisecondsPerTimestamp,
                    CreateMilliseconds = (validating - created) * MillisecondsPerTimestamp, ValidateMilliseconds = (validated - validating) * MillisecondsPerTimestamp };
            });
            Say("Preparing housing-based background population. Existing saved state is retained until initialization succeeds.");
        }

        /// <summary>Arms the startup guard from the city-load callback (<see cref="PersistentCityMod.OnLoadAsync"/>): each load
        /// creates a new runtime, so no earlier Update has armed it, and the first Update may follow the first native tick.</summary>
        public void ArmForCityLoad()
        {
            if (!enabled || failed) return;
            try { ArmStartupClock(SaveGameManager.Current); }
            catch (Exception error) { Fail(error.Message); }
        }

        /// <summary>While no city is attached, keep the startup guard armed for the current save (none at the menu), so
        /// native time is held before the loaded save's first tick. The hold belongs to one save: a different save stops a
        /// clock bound to the old one and starts over. Released (the wait expired) stays released for that save.</summary>
        private void ArmStartupClock(object? save)
        {
            if (simulation != null || initialization != null) return;
            if (startupHold.ForSave(save) && NativeCityClock.Bound) NativeCityClock.Stop();
            if (startupHold.Begin() || startupHold.Holding && !NativeCityClock.Armed && !NativeCityClock.Bound) NativeCityClock.Arm();
        }

        private void StartCityClock(object save)
        {
            NativeCityClock.Start(save, settings.GameClockFactor,
                () => simulation != null && routes?.ReadyForPopulation == true && simulation.IsCaughtUpTo(AccountingClock.Target(Now())),
                () => true, Now, () => simulation?.State.Minute ?? Now(), native => simulation == null || simulation.IsAccountingEpochDrained(native));
            NativeCityClock.StartupHold = true;
        }

        private void CompleteInitialization()
        {
            var pending = initialization;
            if (pending == null || !pending.IsCompleted) return;
            if (!timeActivation) { CompleteInitialization(pending); return; }
            // F7a: requested main-thread activation cost, once per completed load (primitives only).
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
            // Observe failures even if a different save was loaded during this computation.
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
            // F8d: activation stays strict; the class only names why it refused.
            rloDiffers = source.Status == RloReadStatus.Ready && !source.MatchesPopulation(state);
            if (!source.MatchesPopulation(state)) { Fail("RLO donor catalogue changed during initialization (" + source.Continuity(state) + ", " + source.Reason + "). Original payload retained."); return; }
            rloTransient = false;
            if (state.Minute > Now() + 0.001) { Fail("Population snapshot is ahead of this save's clock; original payload retained."); return; }
            if (!AccountingClock.Compatible(state.Minute, Now()))
            { StopAccounting(); return; }
            // F5d: a city whose first roster is about to be created gets its portal places now, before routes and
            // the scheduler index the catalogue. A saved city keeps its own station table.
            if (state.Food == null && state.Portals.Count == 0 && state.Visitors.Count == 0)
            {
                PortalPlaces.Add(state, NativePortalStations.Read());
                log?.Info("Visitor portals: " + state.Portals.Count + " subway stations.");
            }
            routeObjects = new RouteObjectDiscovery();
            routes = new MultimodalRoutes(state.Places, NativeTravelPace.WalkingMetresPerSecond / state.TravelClockRate, state.TravelClockRate, dependency, routeObjects);
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
            // F5g: every current-schema city fills toward the census of this load's catalogue from its next day boundary.
            var fill = new PopulationFillCatalogue(censusPlaces, censusWorkplaces);
            if (fill.Refused) log?.Warn("Population fill detached: census refused the catalogue (" + fill.RefusalReason + "). No person is created or removed.");
            simulation.AttachPopulationFill(fill);
            saveIdentity = save;
            AttachResearch(state);
            NativeInteriorCityView.Start(settings, log!, RestoreInterior);
            NativeTravelPace.ResetReading();
            log?.Info("City clock factor=" + settings.GameClockFactor.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                + "; Driven Rides coordinated=" + NativeCityClock.CoordinatesDrivenRides + ". Movement speed unchanged.");
            log?.Info(CapabilityActivationLine()); // F8d (D7): one line naming every capability that is not active
            log?.Info(HookConflictActivationLine()); // F8e: foreign owners on Persistent City targets, once per load
            ResetAdvanceDiagnostics();
            if (saveEpoch == null || !saveEpoch.BoundTo(save) || saveEpoch.Sequence != prepared.EpochSequence)
            { Fail("Save epoch changed during initialization. Every saved key is retained."); return; }
            NativeCityClock.StartupHold = false;
            saveEpoch.CompleteInitialization(); // Attached: publication allowed, detach now marks a still-current city.
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

        private RloReadResult ReadRlo(string context)
        {
            var result = rlo.Read(dependency);
            lastRloRead = result; // F8b: the capability report reads the last answer; no extra read
            log?.Info("RLO single-pass read context=" + context + " status=" + result.Status
                + " identities=" + result.Identities.Count + " elapsedMs="
                + rlo.LastReadMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                + " reads=" + rlo.Reads + " totalMs="
                + rlo.TotalReadMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                + " maxMs=" + rlo.MaximumReadMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
            return result;
        }

        /// <summary>Today's save steps, in order, inside the F7b epoch callback (<see cref="Save"/>).</summary>
        private SaveEpochCapture CaptureForSave(SaveEpochTicket ticket)
        {
            if (failed || simulation == null) return SaveEpochCapture.Refused("Population is not active.");
            var cost = saveCost; // F7a: null unless requested; then only timestamps are taken.
            cost?.Begin();
            var capture = SaveEpochCapture.Refused("Population save failed.");
            try { capture = CaptureSteps(ticket, cost); return capture; }
            catch (Exception exception) { capture = SaveEpochCapture.Refused("Population save failed: " + exception.Message); throw; }
            finally
            {
                if (cost != null)
                {
                    cost.End(capture.Error == null, capture.Error ?? "");
                    if (capture.Error == null) CountSavedRecords(cost, payload => SaveCodec.CountRecords(simulation!.State, payload));
                }
            }
        }

        private SaveEpochCapture CaptureSteps(SaveEpochTicket ticket, SaveCostRecorder? cost)
        {
            if (!BeginAccountingObservations()) return SaveEpochCapture.Refused("Accounting observations could not begin before saving.");
            var source = ReadRlo("save");
            // F8d: a transient window saves the same payload a Ready save writes; the snapshot is not touched here.
            var saveContinuity = source.Continuity(simulation!.State);
            rloDiffers = saveContinuity == DonorContinuity.Different;
            if (!DonorReads.Continues(saveContinuity))
                return SaveEpochCapture.Refused("RLO changed before saving; original population payload retained.");
            RunPhysical(() => physical?.BeforeSave());
            foodMarket?.CompleteCampaignsForSave();
            foodMarket?.RefreshKnownOwnership();
            if (!NativeCityClock.CheckInputEpoch(Now(), simulation.State.Minute))
                return SaveEpochCapture.Refused("Accounting input epoch changed before saving.");
            if (saveEpoch?.CanPublish(ticket, SaveGameManager.Current, SaveGameManager.Current.modData) != true)
                return SaveEpochCapture.Refused("Save epoch changed during capture; previous payload retained.");
            vendorMarket?.CompleteForSave();
            // Within the guarded input epoch, a partial snapshot resumes from its recorded minute after load.
            var result = (saveSession ?? throw new InvalidOperationException("Save session is missing."))
                .WriteTo(SaveGameManager.Current.modData, simulation.State, cost);
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

        /// <summary>C5e: the saved CityState.Research ledger reaches the survey through the C5b seam, the native bill
        /// through ResearchBilling and the agency dialog through its desk. C5f: MarketResearchSettings.Available is derived
        /// from ResearchActivation; ordering stays hidden whenever a check fails. A refused dialog, billing or delivery contract turns
        /// research off for the session: the ledger stays in the save unchanged, nothing surveys or charges, and the city keeps running.</summary>
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

        /// <summary>C5f: every attach and contract check research availability is derived from, read at call time. A later
        /// foreign patch on the bill detaches billing, and a stopped runtime fails the attach check.</summary>
        private MarketResearchActivation ResearchActivation() => new MarketResearchActivation(ResearchAttached(),
            ResearchAgencyDialog.Installed && ResearchAgencyDialog.Status.Length == 0,
            ResearchBilling.Installed && ResearchBilling.Status.Length == 0 && ResearchBilling.Attached,
            ResearchMessages.ContractPassed, simulation?.Research != null);

        private bool ResearchAttached() => !failed && !accountingStopped && simulation != null && saveEpoch != null
            && saveIdentity != null && ReferenceEquals(saveIdentity, SaveGameManager.Current);

        /// <summary>Plan section 9, Q4: a player-rented business whose food scope Persistent City owns and can serve now.
        /// Ordering and billing use this one predicate.</summary>
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
            settings.MarketResearch.ClearAvailability(); ResearchAgencyDialog.Detach(); ResearchBilling.Detach();
            DetachSaveEpoch();
            NativeInteriorCityView.Stop();
            vendorMarket?.Dispose(); vendorMarket = null;
            foodMarket?.Dispose(); foodMarket = null; ordinaryOwnershipIndex = null;
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
            ResearchAgencyDialog.Uninstall(); ResearchBilling.Uninstall();
            UnregisterVendorDiagnostics(); UnregisterResearchDiagnostics();
            ResetCensus();
            Detach();
            // A pure worker may finish after unload; observe faults and release all runtime references.
            if (initialization != null)
                _ = initialization.ContinueWith(task => { _ = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            initialization = null;
            initializingSave = null;
            initializingPayload = null;
            simulation = null; stopEnvironment = null;
            saveSession = null;
            routes = null;
            saveIdentity = null;
            enabled = false;
            if (metadataLifetime) { FoodProductMetadataApi.Unload(); metadataLifetime = false; }
        }
        private void OnDestroy() => Shutdown();
    }
}
