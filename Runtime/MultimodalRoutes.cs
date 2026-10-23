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
    internal sealed class MultimodalRoutes : ICitizenRoutePlanner, ICommuteTimingPlanner, ITrafficRoutePlanner, ITrafficEntryRoutePlanner, ITrafficClosureBatchPlanner, IWalkingTimeBound
    {
        private readonly double subwayMetresPerMinute;
        private readonly double clockRate;
        private readonly IRouteObjectSource? objects;
        private int objectRevision = -1;
        public string LastDiscoveryGateChange { get; private set; } = "";
        private const double SubwayWaitMinutes = 3;
        private readonly WalkingRoutes walking;
        private readonly double walkingSpeed;
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

        public Route? Plan(Place from, Place to, double departureMinute, CitizenState person)
        {
            if (from == null || to == null || person == null || person.Preferences == null
                || !from.Position.IsFinite || !to.Position.IsFinite || !Position.Finite(departureMinute) || departureMinute < 0) return null;
            var preferences = person.Preferences;
            if (!Position.Finite(preferences.WalkingToleranceMetres) || preferences.WalkingToleranceMetres < 0
                || !Position.Finite(preferences.FareWeightMinutes) || preferences.FareWeightMinutes < 0) return null;
            if (roads == null || !roads.Ready) throw new InvalidOperationException("Call Prepare before advancing the population.");
            if (!roads.IsCurrent) return null;
            EnsureAccess();
            // Ownership is established while physically home, independently of this trip's fastest mode.
            if (roads.Available && person.Car == null && preferences.OwnsCar && person.Phase == CitizenPhase.AtPlace
                && person.PlaceId == person.HomeId && from.Id == person.HomeId && places.TryGetValue(person.HomeId, out var home))
            {
                foreach (var curb in roads.Near(home.Position, 90))
                {
                    var homeAccess = walking.Plan(home, At(curb.Position), departureMinute, preferences);
                    if (homeAccess != null && TransportComposition.TryAllocateHomeCar(person, from, home, curb.Position, homeAccess)) break;
                }
            }
            Route? best = null; double bestScore = double.PositiveInfinity;
            Consider(walking.Plan(from, to, departureMinute, preferences));
            if (preferences.AllowTransit && bestScore > TransportComposition.ModeLowerBound(TravelMode.Subway,
                from.Position, to.Position, preferences, person.Identity.Id, walkingSpeed, gameMinutesPerScaledSecond: clockRate))
            {
                EnsureStations();
                if (subway != null && stations != null)
                {
                    var origins = Nearby(from.Position); var destinations = Nearby(to.Position);
                    var originWalks = new Dictionary<string, Route?>(); var destinationWalks = new Dictionary<string, Route?>();
                    foreach (var a in origins) originWalks[a.Id] = walking.Plan(from, a.Entrance, departureMinute, preferences);
                    foreach (var b in destinations) destinationWalks[b.Id] = walking.Plan(b.Entrance, to, departureMinute, preferences);
                    foreach (var a in origins)
                        foreach (var b in destinations)
                        {
                            if (a.Id == b.Id || originWalks[a.Id] == null || destinationWalks[b.Id] == null) continue;
                            var train = Train(a, b);
                            if (train != null) Consider(TransportComposition.Join(originWalks[a.Id]!, train, destinationWalks[b.Id]!));
                        }
                }
            }
            if (roads.Available && preferences.OwnsCar && person.Car != null
                && bestScore > TransportComposition.ModeLowerBound(TravelMode.Car, from.Position, to.Position,
                    preferences, person.Identity.Id, walkingSpeed, person.Car?.ParkedPosition, gameMinutesPerScaledSecond: clockRate))
            {
                if (person.Car != null)
                {
                    var pickup = At(person.Car.ParkedPosition);
                    var accessWalk = walking.Plan(from, pickup, departureMinute, preferences);
                    if (accessWalk != null)
                    {
                        double accessMetres = WalkMetres(accessWalk);
                        var starts = roads.AtParked(person.Car.ParkedPosition);
                        var ends = roads.Near(to.Position, 90);
                        // At most two road searches per request. Reachability does not trigger an unbounded candidate scan.
                        int searches = 0;
                        foreach (var end in ends)
                        {
                            var egress = walking.Plan(At(end.Position), to, departureMinute, preferences);
                            if (egress == null) continue;
                            if (accessMetres + WalkMetres(egress) > preferences.WalkingToleranceMetres) continue;
                            foreach (var start in starts)
                            {
                                if (searches >= 2) break;
                                // Exact access/egress durations plus a geometric drive lower bound can prove
                                // this candidate cannot beat the existing route before starting another A*.
                                double lower = accessWalk.Minutes + egress.Minutes + TransportComposition.ModeLowerBound(
                                    TravelMode.Car, start.Position, end.Position, preferences, person.Identity.Id, walkingSpeed, gameMinutesPerScaledSecond: clockRate);
                                if (bestScore <= lower) continue;
                                searches++;
                                double driveCeiling = TransportComposition.RemainingSearchMinutes(bestScore,
                                    accessWalk.EstimatedMinutes + TransportComposition.CarPreparationMinutes
                                    + TransportComposition.CarParkingMinutes + egress.EstimatedMinutes);
                                var drive = roads.Plan(start, end, departureMinute + accessWalk.Minutes + TransportComposition.CarPreparationMinutes,
                                    maximumEstimatedMinutes: driveCeiling);
                                if (drive != null) Consider(TransportComposition.Join(accessWalk,
                                    Pause(person.Car.ParkedPosition, TransportComposition.CarPreparationMinutes), drive,
                                    Pause(end.Position, TransportComposition.CarParkingMinutes), egress));
                            }
                            if (searches >= 2) break;
                        }
                    }
                }
            }
            return best;

            void Consider(Route? route)
            {
                if (route == null || !TransportComposition.Valid(route, from.Position, to.Position, preferences, person.Car?.ParkedPosition)) return;
                double score = route.EstimatedMinutes;
                if (!TransportComposition.IsFaster(score, bestScore)) return;
                best = route; bestScore = score;
            }
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
