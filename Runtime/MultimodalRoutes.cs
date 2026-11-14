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
    internal sealed class MultimodalRoutes : ICitizenRoutePlanner, ICommuteTimingPlanner, ITrafficRoutePlanner, ITrafficEntryRoutePlanner, ITrafficClosureBatchPlanner, IWalkingTimeBound, IFoodWalkingPace, IWalkingQuoteValidity, IWalkingQuoteOperation, IResumableRoutePlanner, IRouteEventInputs
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
                    NativeRoadSpeeds.ReferenceComparer), error => error is RloRoadSpeedsNotReadyException ? error.Message : null);
            }
            walking = new WalkingRoutes(walkingMetresPerMinute, (from, to) => access != null && access.ClearSegment(from, to));
            foreach (var place in places) this.places[place.Id] = place;
        }
        internal bool ReuseRoadSceneForRetiming(MultimodalRoutes previous)
        {
            var city = CityManager.Instance;
            if (objects != null && ReferenceEquals(objects, previous.objects)) stationActivity.Include(previous.stationActivity);
            if (roadSceneSearched || objects == null || !ReferenceEquals(objects, previous.objects)
                || city == null || !ReferenceEquals(previous.roadCity, city)
                || !previous.roadSceneSearched || previous.roadScene == null) return false;
            roadScene = previous.roadScene; roadCity = city; roadSceneSearched = true;
            return true;
        }
        private bool routeEventHeld, refreshDeferred, stationsDeferred;
        public void BeginRouteEvent()
        {
            if (routeEventHeld) throw new InvalidOperationException("Route event input lease cannot nest.");
            routeEventHeld = true;
        }
        public void EndRouteEvent() => routeEventHeld = false;
        public string Refresh()
        {
            if (routeEventHeld) { refreshDeferred = true; roads?.BeginSweep(); return ""; }
            if (objects != null && !objects.Ready) return "";
            walking.Clear();
            if (roads != null && roads.SceneMissing)
            { roads = null; roadSceneSearched = false; pendingRoadReason = "scene-missing-retry"; }
            if (access == null) return "";
            string change;
            PublicRoadAccess? changed;
            if (objects == null) { searchFrame = Time.frameCount; changed = access.Refreshed(out change); }
            else changed = access.Refreshed(objects.Doors, out change);
            if (changed == null) { roads?.BeginSweep(); return ""; }
            access = changed; stations = null; roads = null; pendingRoadReason = "gate-change";
            return change;
        }
        public void RefreshStations()
        {
            if (routeEventHeld) { stationsDeferred = true; return; }
            stations = null;
        }
        public double TakeStationSearchMilliseconds() { double slowest = stationSearchMilliseconds; stationSearchMilliseconds = 0; return slowest; }
        private double stationSearchMilliseconds;
        private static int searchFrame = -1;
        private bool SearchAllowed()
        {
            if (searchFrame == Time.frameCount) return false;
            searchFrame = Time.frameCount;
            return true;
        }
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
        internal string? RoadSpeedWaiting => roadSpeeds.Waiting;
        internal bool RoadSpeedsFromRlo => roadSpeeds.FromRlo;
        internal bool RoadSpeedsVerified => roadSpeeds.Verified;
        internal string? RoadSpeedRefusal => roadSpeeds.LastRefusal;
        public bool ReadyForPopulation => roadSpeeds.Waiting == null && (routeEventHeld || !refreshDeferred && !stationsDeferred)
            && (routeEventHeld || objects == null || (objects.Ready && objectRevision == objects.Revision))
            && stations != null && roads != null && roads.Ready && roads.IsCurrent;
        public bool Prepare()
        {
            LastDiscoveryGateChange = "";
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
            if (roads != null && (routeEventHeld || roadSpeeds.Waiting != null) && roadSpeeds.WaitsAfterRefresh()) return false;
            if (routeEventHeld && !cityChanged && !sceneLost && roads?.Ready == true && roads.IsCurrent)
            {
                try { roads.Sweep(1, 1024); }
                catch (RoadSpeedsWaitingException) { return false; }
                return roads.IsCurrent;
            }
            if (!routeEventHeld)
            {
                if (refreshDeferred) { refreshDeferred = false; LastDiscoveryGateChange = Refresh(); }
                if (stationsDeferred) { stationsDeferred = false; stations = null; }
            }
            if (objects != null)
            {
                if (!objects.Ready) return false;
                if (objectRevision != objects.Revision)
                {
                    string gateChange = Refresh();
                    if (gateChange.Length != 0) LastDiscoveryGateChange = gateChange;
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
            try { roads.Sweep(1, 1024); }
            catch (RoadSpeedsWaitingException) { return false; }
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

        public int RoadSearchRestarts => roads?.SearchRestarts ?? 0;
        public long JobUnits { get; private set; }
        internal const int MaximumJobUnitsPerSlice = 8;
        internal const int MaximumJobPathsPerFrame = 16;
        private int jobPathFrame = -1, jobFramePaths, jobAdmissionHeldFrame = -1;
        private double jobPathReserveMilliseconds = .05;
        public long JobAdmissionDeferrals { get; private set; }
        public double JobPathReserveMilliseconds => jobPathReserveMilliseconds;
        public bool JobAdmissionExhaustedForFrame => jobAdmissionHeldFrame == Time.frameCount
            || jobPathFrame == Time.frameCount && jobFramePaths >= MaximumJobPathsPerFrame;
        public string LastJobUnit { get; private set; } = "";
        private static readonly string[] StageNames =
        {
            "validate", "home-curbs", "home-curb-walk", "direct-walk", "subway-stations", "subway-connector", "subway-candidates",
            "car-access-walk", "car-starts", "car-ends", "car-egress-walk", "car-search-begin", "car-search", "finish", "done"
        };

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
            private RoadRoutes? roads;
            private PublicRoadAccess? access;
            private List<Station>? stationList;
            private readonly bool roadsCurrentAtBegin;
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
                if (RevisionChanged())
                    throw new RoutePlanningRestartRequired("The road snapshot, public access or station list changed while a route plan was suspended.");
                bool first = true;
                bool finite = !double.IsPositiveInfinity(budgetMilliseconds);
                int units = 0;
                if (finite && owner.jobPathFrame != Time.frameCount)
                { owner.jobPathFrame = Time.frameCount; owner.jobFramePaths = 0; }
                while (!Completed)
                {
                    if (!first && Stopwatch.GetTimestamp() >= deadline) return false;
                    if (finite && units >= MaximumJobUnitsPerSlice)
                    { owner.JobAdmissionDeferrals++; return false; }
                    bool pathStage = stage == Stage.HomeCurbWalk || stage == Stage.DirectWalk || stage == Stage.SubwayConnector
                        || stage == Stage.CarAccessWalk || stage == Stage.CarEgressWalk || preferenceOnly && stage == Stage.Validate;
                    if (finite && pathStage && (owner.jobFramePaths >= MaximumJobPathsPerFrame
                        || owner.jobFramePaths > 0 && (deadline - Stopwatch.GetTimestamp()) * 1000.0 / Stopwatch.Frequency <= owner.jobPathReserveMilliseconds))
                    {
                        owner.jobAdmissionHeldFrame = Time.frameCount; owner.JobAdmissionDeferrals++; return false;
                    }
                    first = false;
                    units++;
                    owner.JobUnits++; owner.LastJobUnit = StageNames[(int)stage];
                    long before = owner.Metrics.NavMeshPathQueries;
                    double pathTimeBefore = owner.Metrics.NavMeshPathMilliseconds;
                    try { Unit(deadline); }
                    finally
                    {
                        int queries = (int)(owner.Metrics.NavMeshPathQueries - before);
                        if (finite) owner.jobFramePaths += queries;
                        if (queries > 0)
                        {
                            double observed = (owner.Metrics.NavMeshPathMilliseconds - pathTimeBefore) / queries;
                            owner.jobPathReserveMilliseconds = Math.Max(.05,Math.Max(observed*1.25,owner.jobPathReserveMilliseconds*.95));
                        }
                    }
                }
                return true;
            }

            private bool RevisionChanged()
            {
                if (preferenceOnly) return false;
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
        private readonly StaticSceneMemory stationActivity = new StaticSceneMemory();
        public int ValidatedStations => stationActivity.Count;
        private void EnsureStations()
        {
            if (stations != null) return;
            stations = new List<Station>();
            var city = CityManager.Instance;
            if (city == null) return;
            subway = city.subwaySystem;
            StationSearches++;
            foreach (var station in (objects?.Stations ?? Object.FindObjectsByType<SubwayStation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                .OrderBy(station => station.GetInstanceID()))
            {
                if (station == null) continue;
                bool hidden = station.GetComponentsInParent<ToggleObjectDLC>(true).Any(t => t.showWhenOwned != t.dlc.DlcIsOwned());
                bool active = station.gameObject.activeInHierarchy;
                if (OutdoorPlaces.StaticSceneKey(station.transform, out string sceneKey)
                    ? !stationActivity.Admit(sceneKey, active, hidden) : hidden || !active) continue;
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
            Station? first = null, second = null;
            double firstDistance = 0, secondDistance = 0;
            foreach (var station in stations!)
            {
                double distance = Position.Distance(position, station.Entrance.Position);
                if (first == null || Closer(distance, station, firstDistance, first))
                { second = first; secondDistance = firstDistance; first = station; firstDistance = distance; }
                else if (second == null || Closer(distance, station, secondDistance, second))
                { second = station; secondDistance = distance; }
            }
            var nearest = new List<Station>(2);
            if (first != null) nearest.Add(first);
            if (second != null) nearest.Add(second);
            return nearest;
        }
        private static bool Closer(double distance, Station station, double otherDistance, Station other)
        {
            int compare = distance.CompareTo(otherDistance);
            return compare != 0 ? compare < 0 : StringComparer.Ordinal.Compare(station.Id, other.Id) < 0;
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
