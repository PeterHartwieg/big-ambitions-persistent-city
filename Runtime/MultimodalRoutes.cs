using System;
using System.Collections.Generic;
using PersistentCity.Core;
using PersistentCity.Compatibility;
using System.Reflection;
using Extensions;
using GleyUrbanAssets;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace PersistentCity.Runtime
{
    internal interface IRouteObjectSource
    {
        bool Ready { get; }
        int Revision { get; }
        FenceDoor[] Doors { get; }
        SubwayStation[] Stations { get; }
        void RequestSweep();
    }
    /// <summary>Background routes only. Native geometry, explicit simulation timing, no actors or economy writes.</summary>
    internal sealed class MultimodalRoutes : ICitizenRoutePlanner, ICommuteTimingPlanner, ITrafficRoutePlanner, ITrafficEntryRoutePlanner, ITrafficClosureBatchPlanner, IWalkingTimeBound, IFoodWalkingPace, IWalkingQuoteValidity, IWalkingQuoteOperation, IResumableRoutePlanner
    {
        private readonly double subwayMetresPerMinute;
        private readonly double clockRate;
        private readonly IRouteObjectSource? objects;
        private int objectRevision = -1;
        public string LastDiscoveryGateChange { get; private set; } = "";
        private const double SubwayWaitMinutes = 3;
        private readonly WalkingRoutes walking;
        public NativeRouteMetrics Metrics => walking.Metrics;
        private readonly double walkingSpeed;
        public double WalkingMetresPerMinute => walkingSpeed;
        private readonly Dictionary<string, Place> places = new Dictionary<string, Place>(StringComparer.Ordinal);
        private List<Station>? stations;
        private SubwaySystem? subway;
        private RoadRoutes? roads;
        private CurrentSceneData? roadScene;
        private CityManager? roadCity;
        private bool roadSceneSearched;
        private string pendingRoadReason = "initial";
        public string LastRoadRebuildReason { get; private set; } = "";
        public int LastRoadInvalidationIndex { get; private set; } = -1;
        public int RoadSceneSearches { get; private set; }
        public double MaximumRoadSceneSearchMilliseconds { get; private set; }
        public int StationSearches { get; private set; }
        private readonly NativeRoadSpeeds roadSpeeds;
        public double MinimumWalkingMinutes(Place from, Place to)
            => Math.Max(0, Position.Distance(from.Position, to.Position) - .1) / walkingSpeed;
        private PublicRoadAccess? access;
        private Func<int, double, TrafficLinkForecast>? trafficForecast;
        private Func<int, double, double, double, double>? trafficEntryForecast;
        private sealed class Station
        {
            public string Id = "";
            public string District = "";
            public Position Platform;
            public Place Entrance = new Place();
        }

        public MultimodalRoutes(IReadOnlyList<Place> places, double walkingMetresPerMinute = 2, double clockRate = 1)
            : this(places, walkingMetresPerMinute, clockRate, AppDomain.CurrentDomain.GetAssemblies()
                .SingleOrDefault(assembly => assembly.GetName().Name == "RealLifeOverhaul")) { }

        public MultimodalRoutes(IReadOnlyList<Place> places, double walkingMetresPerMinute, double clockRate,
            Assembly? dependency, IRouteObjectSource? objects = null)
        {
            this.objects = objects;
            if (!Position.Finite(clockRate) || clockRate <= 0) throw new ArgumentOutOfRangeException(nameof(clockRate));
            if (!Position.Finite(walkingMetresPerMinute) || walkingMetresPerMinute <= 0) throw new ArgumentOutOfRangeException(nameof(walkingMetresPerMinute));
            this.clockRate = clockRate; subwayMetresPerMinute = 10 / clockRate;
            walkingSpeed = walkingMetresPerMinute;
            if (dependency == null) roadSpeeds = new NativeRoadSpeeds();
            else
            {
                var reviewed = new RloRoadSpeedOverrides(dependency);
                roadSpeeds = new NativeRoadSpeeds(() => reviewed.Capture().ToDictionary(pair => pair.Key,
                    pair => (pair.Value.OriginalKilometresPerHour, pair.Value.EffectiveKilometresPerHour),
                    NativeRoadSpeeds.ReferenceComparer));
            }
            walking = new WalkingRoutes(walkingMetresPerMinute, (from, to) => access != null && access.ClearSegment(from, to));
            foreach (var place in places) this.places[place.Id] = place;
        }
        /// <summary>Retiming changes movement costs, not the native scene container. Reuse only
        /// that live reference within the same save-scoped discovery owner and CityManager.
        /// Access, stations, road geometry and timing still undergo fresh preparation.</summary>
        internal bool ReuseRoadSceneForRetiming(MultimodalRoutes previous)
        {
            var city = CityManager.Instance;
            if (roadSceneSearched || objects == null || !ReferenceEquals(objects, previous.objects)
                || city == null || !ReferenceEquals(previous.roadCity, city)
                || !previous.roadSceneSearched || previous.roadScene == null) return false;
            roadScene = previous.roadScene; roadCity = city; roadSceneSearched = true;
            return true;
        }
        /// <summary>Periodic refresh. Gate geometry is recaptured and the walking cache dropped every time. Refresh never
        /// discards the road snapshot unless the public gates changed, since the snapshot cannot see them; it starts a
        /// staged check of the whole snapshot instead, which marks it stale on any native difference. Returns the
        /// changed gate, or "" when the gates are the same.</summary>
        public string Refresh()
        {
            if (objects != null && !objects.Ready) return "";
            walking.Clear();
            // Nothing marks a snapshot taken without a road scene stale, so look for the scene again.
            if (roads != null && roads.SceneMissing)
            { roads = null; roadSceneSearched = false; pendingRoadReason = "scene-missing-retry"; }
            if (access == null) return "";
            string change;
            PublicRoadAccess? changed;
            if (objects == null) { searchFrame = Time.frameCount; changed = access.Refreshed(out change); }
            else changed = access.Refreshed(objects.Doors, out change);
            if (changed == null) { roads?.BeginSweep(); return ""; }
            // The traffic network filters its sections by gates when it is built, so a gate change needs a new one.
            access = changed; stations = null; roads = null; pendingRoadReason = "gate-change";
            return change;
        }
        /// <summary>Recaptures live station eligibility and geometry on the next preparation.</summary>
        public void RefreshStations() => stations = null;
        /// <summary>The slowest station search since the last call, in milliseconds.</summary>
        public double TakeStationSearchMilliseconds() { double slowest = stationSearchMilliseconds; stationSearchMilliseconds = 0; return slowest; }
        private double stationSearchMilliseconds;
        // Shared by every instance, so routes replaced after a clock-rate change cannot search in the same frame.
        private static int searchFrame = -1;
        // At most one whole-scene search (doors, stations or the road scene) a frame; the next one waits a frame.
        private bool SearchAllowed()
        {
            if (searchFrame == Time.frameCount) return false;
            searchFrame = Time.frameCount;
            return true;
        }
        /// <summary>Road snapshots started since the routes were created, for whatever reason.</summary>
        public int RoadSnapshots { get; private set; }
        public TrafficNetwork? TrafficNetwork => roads?.TrafficNetwork;
        public bool IsTrafficLinkClosed(int linkId) => roads?.IsTrafficLinkClosed(linkId) ?? true;
        private RoadRoutes? closureRoads;
        public void BeginTrafficClosureBatch()
        {
            if (closureRoads != null) throw new InvalidOperationException("Traffic closure batch cannot be nested.");
            roads?.BeginTrafficClosureBatch();
            closureRoads = roads;
        }
        public void EndTrafficClosureBatch()
        {
            closureRoads?.EndTrafficClosureBatch();
            closureRoads = null;
        }
        public void SetTrafficForecast(Func<int, double, TrafficLinkForecast>? forecast)
        {
            trafficForecast = forecast;
            roads?.SetTrafficForecast(forecast);
        }
        public void SetTrafficEntryForecast(Func<int, double, double, double, double>? forecast)
        {
            trafficEntryForecast = forecast;
            roads?.SetTrafficEntryForecast(forecast);
        }
        public bool ReadyForPopulation => (objects == null || (objects.Ready && objectRevision == objects.Revision))
            && stations != null && roads != null && roads.Ready && roads.IsCurrent;
        /// <summary>Runtime preflight: do not advance any citizen until this returns true. Staged infrastructure
        /// readiness must not change which modes are available to an individual planning request.</summary>
        public bool Prepare()
        {
            LastDiscoveryGateChange = "";
            // Reuse a scene for structural rebuilds, scoped to this save/planner and CityManager.
            // Destroyed Unity objects compare null. A different city must rediscover before publication.
            var city = CityManager.Instance;
            bool cityChanged = roadSceneSearched && !ReferenceEquals(roadCity, city);
            bool sceneLost = roadSceneSearched && !ReferenceEquals(roadScene, null) && roadScene == null;
            if (cityChanged || sceneLost)
            {
                roads = null; roadScene = null; roadSceneSearched = false;
                access = null; stations = null; subway = null; walking.Clear();
                pendingRoadReason = cityChanged ? "city-instance" : "scene-destroyed";
                objects?.RequestSweep();
            }
            if (objects != null)
            {
                // Initial discovery and structural changes require a complete generation before
                // publication; a lost scene/city above must never reuse a ready old inventory.
                if (!objects.Ready) return false;
                if (objectRevision != objects.Revision)
                {
                    LastDiscoveryGateChange = Refresh();
                    stations = null; objectRevision = objects.Revision;
                }
            }
            if (access == null) { if (objects == null && !SearchAllowed()) return false; EnsureAccess(); }
            if (stations == null)
            {
                if (objects == null && !SearchAllowed()) return false;
                long started = System.Diagnostics.Stopwatch.GetTimestamp();
                EnsureStations();
                stationSearchMilliseconds = Math.Max(stationSearchMilliseconds,
                    (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            }
            if (roads == null || !roads.IsCurrent)
            {
                if (!roadSceneSearched)
                {
                    if (!SearchAllowed()) return false;
                    long started = System.Diagnostics.Stopwatch.GetTimestamp();
                    roadScene = Object.FindObjectOfType<CurrentSceneData>();
                    MaximumRoadSceneSearchMilliseconds = Math.Max(MaximumRoadSceneSearchMilliseconds,
                        (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
                    RoadSceneSearches++; roadSceneSearched = true; roadCity = city;
                }
                LastRoadRebuildReason = roads == null ? pendingRoadReason : roads.InvalidationReason;
                LastRoadInvalidationIndex = roads?.InvalidationIndex ?? -1;
                roads = new RoadRoutes(roadScene, access!, walkingSpeed, clockRate, roadSpeeds);
                RoadSnapshots++;
                roads.SetTrafficForecast(trafficForecast);
                roads.SetTrafficEntryForecast(trafficEntryForecast);
            }
            else roads.SetAccess(access!);
            if (!roads.Prepare()) return false;
            roads.Sweep(1, 1024);
            return roads.IsCurrent;
        }
        public GleyTrafficSystem.Waypoint? ResolveWaypoint(string topology, int index) => roads?.ResolveWaypoint(topology, index);
        public bool TryPhysicalRoadSample(CitizenState citizen, PhysicalJourneySample sample, out PhysicalRoadSample roadSample)
        {
            roadSample = default;
            return roads != null && roads.TryPhysicalRoadSample(citizen, sample, out roadSample);
        }
        public Route? Plan(Place from, Place to, double departureMinute, TravelPreferences preferences)
        {
            EnsureAccess();
            return walking.Plan(from, to, departureMinute, preferences);
        }

        public bool IsWalkingQuoteValid(Place from, Place to, Route route, TravelPreferences preferences)
            => ReadyForPopulation && walking.IsWalkingQuoteValid(from, to, route, preferences);
        public void BeginWalkingQuote() => walking.BeginWalkingQuote();
        public void EndWalkingQuote() => walking.EndWalkingQuote();

        public Route? Plan(Place from, Place to, double departureMinute, CitizenState person)
        {
            // The synchronous plan is the resumable job driven without a budget, so both share one code path.
            using var job = new PlanningJob(this, from, to, departureMinute, person, person?.Preferences, preferenceOnly: false);
            job.Continue(double.PositiveInfinity);
            return job.Result;
        }

        public IRoutePlanningJob Begin(RouteRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return new PlanningJob(this, request.From, request.To, request.DepartureMinute, request.Citizen, request.Preferences,
                preferenceOnly: request.Citizen == null);
        }

        /// <summary>Road searches of the current snapshot that started over while suspended.</summary>
        public int RoadSearchRestarts => roads?.SearchRestarts ?? 0;
        /// <summary>Units of planning work performed by jobs, for the frame summary.</summary>
        public long JobUnits { get; private set; }
        /// <summary>The kind of the unit a job performed last, for attributing a slow slice.</summary>
        public string LastJobUnit { get; private set; } = "";
        private static readonly string[] StageNames =
        {
            "validate", "home-curbs", "home-curb-walk", "direct-walk", "subway-stations", "subway-connector", "subway-candidates",
            "car-access-walk", "car-starts", "car-ends", "car-egress-walk", "car-search-begin", "car-search", "finish", "done"
        };

        /// <summary>The former synchronous citizen plan as a resumable job. Each unit is one native query (a walk,
        /// a curb search, a station capture) or one slice of a road A*; Continue runs units until the budget is
        /// spent and always at least one. The job captures the road snapshot, public access and station list it
        /// plans against; if any of them was replaced while it was suspended, Continue throws
        /// RoutePlanningRestartRequired and the simulator replays the whole citizen event, so no event mixes two
        /// snapshots. Forecasts and node validations it reads are live at the moment of each read, as
        /// they are inside the synchronous plan.</summary>
        private sealed class PlanningJob : IRoutePlanningJob
        {
            private enum Stage
            {
                Validate, HomeCurbs, HomeCurbWalk, DirectWalk, SubwayStations, SubwayConnector, SubwayCandidates,
                CarAccessWalk, CarStarts, CarEnds, CarEgressWalk, CarSearchBegin, CarSearch, Finish, Done
            }
            private readonly MultimodalRoutes owner;
            private readonly Place? from, to;
            private readonly double departureMinute;
            private readonly CitizenState? person;
            private readonly TravelPreferences? preferences;
            private readonly bool preferenceOnly;
            private Stage stage;
            // Captured when the job is begun, so a change at any later slice boundary, including the one before
            // the first slice, is seen: an earlier answer of the same event was given on this same state.
            private RoadRoutes? roads;
            private PublicRoadAccess? access;
            private List<Station>? stationList;
            private readonly bool roadsCurrentAtBegin;
            // Working state of the former local variables.
            private Route? best;
            private double bestScore = double.PositiveInfinity;
            private Place? home;
            private List<RoadRoutes.Curb>? homeCurbs;
            private int homeCurbIndex;
            private List<Station>? origins, destinations;
            private Dictionary<string, Route?>? originWalks, destinationWalks;
            private int connectorIndex;
            private Route? accessWalk, egress;
            private double accessMetres, driveCeiling;
            private List<RoadRoutes.Curb>? starts, ends;
            private int endIndex, startIndex, searches;
            private RoadRoutes.RoadPlan? drive;
            public Route? Result { get; private set; }
            public bool Completed => stage == Stage.Done;

            internal PlanningJob(MultimodalRoutes owner, Place? from, Place? to, double departureMinute, CitizenState? person,
                TravelPreferences? preferences, bool preferenceOnly)
            {
                this.owner = owner; this.from = from; this.to = to; this.departureMinute = departureMinute;
                this.person = person; this.preferences = preferences; this.preferenceOnly = preferenceOnly;
                if (!preferenceOnly)
                {
                    owner.Metrics.CitizenPlanRequests++;
                    roads = owner.roads; access = owner.access; stationList = owner.stations;
                    roadsCurrentAtBegin = roads != null && roads.IsCurrent;
                }
            }

            public bool Continue(double budgetMilliseconds)
            {
                if (Completed) return true;
                if (double.IsNaN(budgetMilliseconds) || budgetMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(budgetMilliseconds));
                long started = Stopwatch.GetTimestamp();
                long deadline = double.IsPositiveInfinity(budgetMilliseconds)
                    ? long.MaxValue : started + (long)(budgetMilliseconds * Stopwatch.Frequency / 1000);
                // A car this attempt allocated and the event's earlier answers belong to the captured snapshot,
                // so the caller discards the working copy and replays the event instead of this job starting over.
                if (RevisionChanged())
                    throw new RoutePlanningRestartRequired("The road snapshot, public access or station list changed while a route plan was suspended.");
                bool first = true;
                while (!Completed)
                {
                    if (!first && Stopwatch.GetTimestamp() >= deadline) return false;
                    first = false;
                    owner.JobUnits++; owner.LastJobUnit = StageNames[(int)stage];
                    Unit(deadline);
                }
                return true;
            }

            private bool RevisionChanged()
            {
                if (preferenceOnly) return false;
                // Access is created lazily by EnsureAccess, so its first appearance is not a replacement. A snapshot
                // that was already stale when the job began answers "no route" in Validate, as the one-shot plan does.
                return !ReferenceEquals(owner.roads, roads) || (access != null && !ReferenceEquals(owner.access, access))
                    || (roadsCurrentAtBegin && (roads == null || !roads.IsCurrent)) || !ReferenceEquals(owner.stations, stationList);
            }

            private void Unit(long deadline)
            {
                var owner = this.owner;
                switch (stage)
                {
                    case Stage.Validate:
                        if (preferenceOnly)
                        {
                            // A preference-only request is the walking plan of IRoutePlanner.Plan.
                            owner.EnsureAccess();
                            Result = owner.walking.Plan(from!, to!, departureMinute, preferences!);
                            stage = Stage.Done; return;
                        }
                        if (from == null || to == null || person == null || person.Preferences == null
                            || !from.Position.IsFinite || !to.Position.IsFinite || !Position.Finite(departureMinute) || departureMinute < 0)
                        { Result = null; stage = Stage.Done; return; }
                        var checkedPreferences = person.Preferences;
                        if (!Position.Finite(checkedPreferences.WalkingToleranceMetres) || checkedPreferences.WalkingToleranceMetres < 0
                            || !Position.Finite(checkedPreferences.FareWeightMinutes) || checkedPreferences.FareWeightMinutes < 0)
                        { Result = null; stage = Stage.Done; return; }
                        if (owner.roads == null || !owner.roads.Ready) throw new InvalidOperationException("Call Prepare before advancing the population.");
                        if (!owner.roads.IsCurrent) { Result = null; stage = Stage.Done; return; }
                        owner.EnsureAccess();
                        roads = owner.roads; access = owner.access;
                        best = null; bestScore = double.PositiveInfinity;
                        // Ownership is established while physically home, independently of this trip's fastest mode.
                        if (roads.Available && person.Car == null && checkedPreferences.OwnsCar && person.Phase == CitizenPhase.AtPlace
                            && person.PlaceId == person.HomeId && from.Id == person.HomeId && owner.places.TryGetValue(person.HomeId, out var homePlace))
                        { home = homePlace; stage = Stage.HomeCurbs; }
                        else stage = Stage.DirectWalk;
                        return;
                    case Stage.HomeCurbs:
                        homeCurbs = roads!.Near(home!.Position, 90); homeCurbIndex = 0;
                        stage = homeCurbs.Count == 0 ? Stage.DirectWalk : Stage.HomeCurbWalk;
                        return;
                    case Stage.HomeCurbWalk:
                    {
                        var curb = homeCurbs![homeCurbIndex++];
                        var homeAccess = owner.walking.Plan(home!, At(curb.Position), departureMinute, Preferences);
                        if ((homeAccess != null && TransportComposition.TryAllocateHomeCar(person!, from!, home!, curb.Position, homeAccess))
                            || homeCurbIndex >= homeCurbs.Count) stage = Stage.DirectWalk;
                        return;
                    }
                    case Stage.DirectWalk:
                        Consider(owner.walking.Plan(from!, to!, departureMinute, Preferences));
                        if (Preferences.AllowTransit && bestScore > TransportComposition.ModeLowerBound(TravelMode.Subway,
                            from!.Position, to!.Position, Preferences, person!.Identity.Id, owner.walkingSpeed, gameMinutesPerScaledSecond: owner.clockRate))
                            stage = Stage.SubwayStations;
                        else AfterModes();
                        return;
                    case Stage.SubwayStations:
                        owner.EnsureStations();
                        stationList = owner.stations;
                        if (owner.subway != null && owner.stations != null)
                        {
                            origins = owner.Nearby(from!.Position); destinations = owner.Nearby(to!.Position);
                            originWalks = new Dictionary<string, Route?>(); destinationWalks = new Dictionary<string, Route?>();
                            connectorIndex = 0;
                            stage = origins.Count + destinations.Count > 0 ? Stage.SubwayConnector : Stage.SubwayCandidates;
                        }
                        else AfterModes();
                        return;
                    case Stage.SubwayConnector:
                    {
                        int index = connectorIndex++;
                        if (index < origins!.Count)
                        { var a = origins[index]; originWalks![a.Id] = owner.SubwayConnector(from!, a.Entrance, departureMinute, Preferences); }
                        else
                        { var b = destinations![index - origins.Count]; destinationWalks![b.Id] = owner.SubwayConnector(b.Entrance, to!, departureMinute, Preferences); }
                        if (connectorIndex >= origins.Count + destinations!.Count) stage = Stage.SubwayCandidates;
                        return;
                    }
                    case Stage.SubwayCandidates:
                        foreach (var a in origins!)
                            foreach (var b in destinations!)
                            {
                                if (a.Id == b.Id || originWalks![a.Id] == null || destinationWalks![b.Id] == null) continue;
                                owner.Metrics.SubwayCandidates++;
                                var train = owner.Train(a, b);
                                if (train != null) Consider(TransportComposition.Join(originWalks[a.Id]!, train, destinationWalks[b.Id]!));
                            }
                        AfterModes();
                        return;
                    case Stage.CarAccessWalk:
                        accessWalk = owner.walking.Plan(from!, At(person!.Car!.ParkedPosition), departureMinute, Preferences);
                        if (accessWalk == null) { stage = Stage.Finish; return; }
                        accessMetres = WalkMetres(accessWalk);
                        stage = Stage.CarStarts;
                        return;
                    case Stage.CarStarts:
                        starts = roads!.AtParked(person!.Car!.ParkedPosition);
                        stage = Stage.CarEnds;
                        return;
                    case Stage.CarEnds:
                        ends = roads!.Near(to!.Position, 90);
                        // At most two road searches per request. Reachability does not trigger an unbounded candidate scan.
                        endIndex = 0; searches = 0;
                        NextEnd();
                        return;
                    case Stage.CarEgressWalk:
                    {
                        var end = ends![endIndex];
                        egress = owner.walking.Plan(At(end.Position), to!, departureMinute, Preferences);
                        if (egress == null || accessMetres + WalkMetres(egress) > Preferences.WalkingToleranceMetres) { endIndex++; NextEnd(); return; }
                        startIndex = 0;
                        NextStart();
                        return;
                    }
                    case Stage.CarSearchBegin:
                        drive = roads!.BeginPlan(starts![startIndex], ends![endIndex],
                            departureMinute + accessWalk!.Minutes + TransportComposition.CarPreparationMinutes, driveCeiling);
                        stage = Stage.CarSearch;
                        return;
                    case Stage.CarSearch:
                    {
                        if (!drive!.Continue(deadline)) return;
                        var road = drive.Result; drive.Dispose(); drive = null;
                        if (road != null) Consider(TransportComposition.Join(accessWalk!,
                            Pause(person!.Car!.ParkedPosition, TransportComposition.CarPreparationMinutes), road,
                            Pause(ends![endIndex].Position, TransportComposition.CarParkingMinutes), egress!));
                        startIndex++;
                        NextStart();
                        return;
                    }
                    case Stage.Finish:
                        if (best == null) owner.Metrics.NoFeasibleRoute++;
                        else if (best.Legs.Any(leg => leg.Mode == TravelMode.Car)) owner.Metrics.SelectedCar++;
                        else if (best.Legs.Any(leg => leg.Mode == TravelMode.Subway)) owner.Metrics.SelectedSubway++;
                        else owner.Metrics.SelectedWalking++;
                        Result = best;
                        stage = Stage.Done;
                        return;
                    default:
                        return;
                }
            }

            private TravelPreferences Preferences => person!.Preferences;

            // After walking and subway: the car is considered only when its geometric bound can still beat the incumbent.
            private void AfterModes()
            {
                var car = person!.Car;
                stage = roads!.Available && Preferences.OwnsCar && car != null
                    && bestScore > TransportComposition.ModeLowerBound(TravelMode.Car, from!.Position, to!.Position,
                        Preferences, person.Identity.Id, owner.walkingSpeed, car.ParkedPosition, gameMinutesPerScaledSecond: owner.clockRate)
                    ? Stage.CarAccessWalk : Stage.Finish;
            }

            private void NextEnd() => stage = searches >= 2 || endIndex >= ends!.Count ? Stage.Finish : Stage.CarEgressWalk;

            private void NextStart()
            {
                while (startIndex < starts!.Count && searches < 2)
                {
                    var start = starts[startIndex]; var end = ends![endIndex];
                    // Exact access/egress durations plus a geometric drive lower bound can prove
                    // this candidate cannot beat the existing route before starting another A*.
                    double lower = accessWalk!.Minutes + egress!.Minutes + TransportComposition.ModeLowerBound(
                        TravelMode.Car, start.Position, end.Position, Preferences, person!.Identity.Id, owner.walkingSpeed, gameMinutesPerScaledSecond: owner.clockRate);
                    if (bestScore <= lower) { startIndex++; continue; }
                    searches++;
                    driveCeiling = TransportComposition.RemainingSearchMinutes(bestScore,
                        accessWalk.EstimatedMinutes + TransportComposition.CarPreparationMinutes
                        + TransportComposition.CarParkingMinutes + egress.EstimatedMinutes);
                    stage = Stage.CarSearchBegin;
                    return;
                }
                endIndex++;
                NextEnd();
            }

            private void Consider(Route? route)
            {
                if (route == null || !TransportComposition.Valid(route, from!.Position, to!.Position, Preferences, person!.Car?.ParkedPosition)) return;
                double score = route.EstimatedMinutes;
                if (!TransportComposition.IsFaster(score, bestScore)) return;
                best = route; bestScore = score;
            }

            public void Dispose()
            {
                drive?.Dispose(); drive = null;
                if (!Completed) { Result = null; stage = Stage.Done; }
            }
        }

        private Route? SubwayConnector(Place from, Place to, double minute, TravelPreferences preferences)
        {
            Metrics.SubwayConnectorRequests++;
            long hits = Metrics.WalkingCacheHits, misses = Metrics.WalkingCacheMisses;
            var route = walking.Plan(from, to, minute, preferences);
            Metrics.SubwayConnectorCacheHits += Metrics.WalkingCacheHits - hits;
            Metrics.SubwayConnectorCacheMisses += Metrics.WalkingCacheMisses - misses;
            return route;
        }

        private void EnsureAccess()
        {
            if (access == null) access = objects == null ? new PublicRoadAccess() : new PublicRoadAccess(objects.Doors);
        }
        private void EnsureStations()
        {
            if (stations != null) return;
            stations = new List<Station>();
            var city = CityManager.Instance;
            if (city == null) return;
            subway = city.subwaySystem;
            StationSearches++;
            // Discovery must include newly added/inactive stations. Avoid Unity's global sorted search;
            // order this small result by instance ID to preserve the existing duplicate-name tie behavior.
            foreach (var station in (objects?.Stations ?? Object.FindObjectsByType<SubwayStation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                .OrderBy(station => station.GetInstanceID()))
            {
                if (station == null || !station.gameObject.activeInHierarchy
                    || station.GetComponentsInParent<ToggleObjectDLC>(true).Any(t => t.showWhenOwned != t.dlc.DlcIsOwned())) continue;
                Position position;
                try { position = Point(station.GetNavMeshTargetPosition()); }
                catch (Exception) { continue; }
                var platform = Point(station.transform.position);
                if (!position.IsFinite || !platform.IsFinite || access!.NearGate(position, 12)) continue;
                string id = "subway:" + station.stationName;
                stations.Add(new Station { Id = id, District = station.neighbourhood ?? "", Platform = platform, Entrance = new Place { Id = id, Position = position } });
            }
            stations.Sort((a, b) => StringComparer.Ordinal.Compare(a.Id, b.Id));
        }
        private List<Station> Nearby(Position position)
        {
            var sorted = new List<Station>(stations!);
            sorted.Sort((a, b) => { int compare = Position.Distance(position, a.Entrance.Position).CompareTo(Position.Distance(position, b.Entrance.Position)); return compare != 0 ? compare : StringComparer.Ordinal.Compare(a.Id, b.Id); });
            if (sorted.Count > 2) sorted.RemoveRange(2, sorted.Count - 2);
            return sorted;
        }
        private Route? Train(Station from, Station to)
        {
            var points = new List<Position> { from.Entrance.Position, from.Platform };
            if (IndustrySide(from.District) != IndustrySide(to.District))
            {
                var bridge = IndustrySide(from.District) ? subway!.manhattanBridgeIcToLm : subway!.manhattanBridgeLmToIc;
                if (bridge == null || bridge.Length < 2) return null;
                points.Add(Point(bridge[0])); points.Add(Point(bridge[1]));
            }
            points.Add(to.Platform); points.Add(to.Entrance.Position);
            var route = new Route();
            route.Legs.Add(new RouteLeg { From = points[0], To = points[0], Mode = TravelMode.Subway, Minutes = SubwayWaitMinutes, ServiceId = "native:subway" });
            Position previous = points[0];
            for (int i = 1; i < points.Count; i++)
            {
                if (!points[i].IsFinite) return null;
                double length = Position.Distance(previous, points[i]);
                if (!Position.Finite(length)) return null;
                if (length > 0.000001) route.Legs.Add(new RouteLeg { From = previous, To = points[i], Mode = TravelMode.Subway, Minutes = length / subwayMetresPerMinute, ServiceId = "native:subway" });
                previous = points[i];
            }
            return route;
        }
        private static bool IndustrySide(string district) => district == "ba:neighborhood_industrycity" || district == "ba:neighborhood_thehamptons";
        private static Place At(Position position) => new Place { Position = position };
        private static double WalkMetres(Route route)
        {
            double metres = 0;
            foreach (var leg in route.Legs) metres += Position.Distance(leg.From, leg.To);
            return metres;
        }
        private static Route Pause(Position position, double minutes) => new Route
        {
            Legs = new List<RouteLeg> { new RouteLeg { From = position, To = position, Mode = TravelMode.Car, Minutes = minutes } }
        };
        private static Position Point(Vector3 p) => new Position(p.x, p.y, p.z);
    }
}
