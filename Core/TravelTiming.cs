using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public static class TravelTiming
    {
        private const double MaxMinute = 1000000000;
        private const double MaxLegMinutes = 1000000;
        private const int MaxLegs = 512;
        private sealed class Change
        {
            internal CitizenState Citizen = null!;
            internal Route? Route;
            internal double EventMinute;
            internal double? MealStayUntil;
            internal double? DepartureMinute;
        }
        private readonly struct Pose
        {
            internal Pose(in PhysicalJourneySample sample) { Position = sample.Position; Car = sample.CarPosition; Mode = sample.Mode; }
            internal Position Position { get; }
            internal Position? Car { get; }
            internal TravelMode Mode { get; }
        }
        private readonly struct TrafficCheck
        {
            internal TrafficCheck(Change change, Pose before) { Change = change; Before = before; }
            internal Change Change { get; }
            internal Pose Before { get; }
        }
        private sealed class SourceLegs : IReadOnlyList<RouteLeg>
        {
            private RouteLegs legs = new RouteLegs();
            internal long Reads;
            internal double Minutes { get; private set; }
            internal SourceLegs Over(RouteLegs over, double minutes = 0) { legs = over; Minutes = minutes; return this; }
            public int Count => legs.Count;
            public RouteLeg this[int index] { get { Reads++; return legs[index]; } }
            public IEnumerator<RouteLeg> GetEnumerator() { for (int i = 0; i < legs.Count; i++) yield return this[i]; }
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }
        private sealed class NominalMap
        {
            internal readonly List<(double OldEnd, double NewEnd)> Segments = new List<(double, double)>();
            private List<(double Start, double End)>? unscaledCarPrefixes;
            internal void AddUnscaledCarPrefix(double start, double end) =>
                (unscaledCarPrefixes ??= new List<(double, double)>()).Add((start, end));

            internal bool HasFutureUnscaledTrafficPrefix(double observed, IReadOnlyList<TrafficRouteLeg> route)
            {
                if (unscaledCarPrefixes == null) return false;
                foreach (var prefix in unscaledCarPrefixes)
                {
                    double start = Math.Max(observed, prefix.Start);
                    if (prefix.End <= start) continue;
                    for (int i = 0; i < route.Count; i++)
                    {
                        var span = route[i];
                        if (Map(span.EndNominalMinutes) > start && Map(span.StartNominalMinutes) < prefix.End) return true;
                    }
                }
                return false;
            }
            internal double Map(double value)
            {
                if (!Position.Finite(value) || value < 0) throw new ArgumentException("Invalid traffic route offset.");
                int first = 0, end = Segments.Count;
                while (first < end)
                {
                    int middle = first + (end - first) / 2;
                    if (value > Segments[middle].OldEnd) first = middle + 1;
                    else end = middle;
                }
                if (first == Segments.Count)
                    throw new ArgumentException("Traffic offset is outside the committed route.");
                var segment = Segments[first];
                var start = first == 0 ? default : Segments[first - 1];
                if (value < start.OldEnd) throw new ArgumentException("Traffic offset is outside the committed route.");
                if (value == start.OldEnd) return start.NewEnd;
                if (value == segment.OldEnd) return segment.NewEnd;
                return start.NewEnd + (value - start.OldEnd) / (segment.OldEnd - start.OldEnd)
                    * (segment.NewEnd - start.NewEnd);
            }
        }

        public static bool Apply(CityState state, double nativeWalkMetresPerSecond, double clockRate)
            => Apply(state, nativeWalkMetresPerSecond, clockRate, out _);

        public static bool Apply(CityState state, double nativeWalkMetresPerSecond, double clockRate, out int degraded)
        {
            degraded = 0;
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!Needed(state, nativeWalkMetresPerSecond, clockRate)) return false;
            bool captured = state.Traffic == null && state.SnapshotSynchronizer != null;
            if (captured) state.SnapshotSynchronizer!();
            var snapshot = state.Traffic;
            try
            {
                var job = Begin(state, nativeWalkMetresPerSecond, clockRate)!;
                while (!job.Step(long.MaxValue)) { }
                if (!job.Commit()) throw new InvalidOperationException("Retime inputs changed during an unsliced retime.");
                degraded = job.Degraded;
                return true;
            }
            catch
            {
                if (captured && ReferenceEquals(state.Traffic, snapshot))
                { state.Traffic = null; state.TrafficSource = null; state.TrafficSourceState = null; }
                throw;
            }
        }

        public static TravelRetime? Begin(CityState state, double nativeWalkMetresPerSecond, double clockRate)
            => Begin(state, nativeWalkMetresPerSecond, clockRate, null);

        internal static TravelRetime? Begin(CityState state, double nativeWalkMetresPerSecond, double clockRate, RetimeSource? source, int sharedLegHint = 0, bool coalesce = true)
        {
            if (!Needed(state, nativeWalkMetresPerSecond, clockRate)) return null;
            return new TravelRetime(state, nativeWalkMetresPerSecond, clockRate, state.TravelTimingVersion == 0, source, sharedLegHint, coalesce);
        }

        internal sealed class RetimeSource
        {
            internal RetimeSource(TrafficKernel? kernel, bool withoutThroughCars = false) { Kernel = kernel; WithoutThroughCars = withoutThroughCars; }
            internal TrafficKernel? Kernel { get; }
            internal bool WithoutThroughCars { get; }
        }

        private static bool Needed(CityState state, double nativeWalkMetresPerSecond, double clockRate)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            Rate(nativeWalkMetresPerSecond, nameof(nativeWalkMetresPerSecond)); Rate(clockRate, nameof(clockRate));
            Rate(state.TravelClockRate, nameof(state.TravelClockRate));
            if (state.TravelTimingVersion != 0 && state.TravelTimingVersion != 1)
                throw new ArgumentException("Unsupported travel timing version.", nameof(state));
            if (!Position.Finite(state.Minute) || state.Minute < 0 || state.Minute > MaxMinute)
                throw new ArgumentException("Invalid city clock.", nameof(state));
            return state.TravelTimingVersion == 0 || state.TravelClockRate != clockRate;
        }

        public sealed class RetimedTraffic
        {
            internal RetimedTraffic(TrafficKernelState? source, TrafficKernel kernel) { Source = source; Kernel = kernel; }
            internal TrafficKernelState? Source { get; }
            internal TrafficKernel Kernel { get; }
        }

        public sealed class TravelRetime
        {
            private struct Input
            {
                internal CitizenState Citizen;
                internal CitizenPhase Phase;
                internal Route? Journey;
                internal double Departure, NextEvent, ActivityUntil;
                internal string PlaceId, DestinationId;
                internal MealOuting? Meal;
                internal bool Returning, SafelyHome;
                internal Place? Origin, Destination;
                internal Position OriginPosition, DestinationPosition;
            }
            private readonly double walk, rate, legRate, ratio, minute, stateRate;
            private readonly bool legacy;
            private readonly int version, places;
            private readonly TrafficKernelState? trafficState;
            private readonly List<CitizenState> schedules;
            private readonly Input[] inputs;
            private const int LinksPerStep = 256;
            private TrafficKernel? savedTraffic, kernel, liveTraffic;
            private System.Collections.IEnumerator? pass;
            private TrafficKernelState? excluded;
            private RetimedTraffic? restoredTraffic;
            private readonly Dictionary<string, TrafficVehicleState> trafficRoutes = new Dictionary<string, TrafficVehicleState>(StringComparer.Ordinal);
            private readonly HashSet<string> evicted = new HashSet<string>(StringComparer.Ordinal);
            private readonly List<Change> changes = new List<Change>();
            private readonly Dictionary<string, NominalMap> maps = new Dictionary<string, NominalMap>(StringComparer.Ordinal);
            private readonly Dictionary<string, TrafficCheck> trafficChanges;
            private readonly SourceLegs sourceLegs = new SourceLegs();
            private Route? primaryRoute, alternateRoute;
            private readonly Dictionary<string, Place> placeIndex;
            private TrafficKernelState? traffic;
            private TrafficKernel? rescaledKernel;
            private Dictionary<string, CitizenState>? citizens;
            private int next, seeded, checkedVehicles, stage;
            private bool trafficDone, committed;
            private readonly bool kernelMode;
            private readonly Func<int, IEnumerator<bool>>? retimeSnapshot;
            private readonly List<TrafficVehicleState>? sourceVehicles;
            private List<TrafficVehicleState>? checkVehicles;
            private SharedLegPool? sharedLegs;
            private readonly int sharedLegHint;
            private readonly TrafficKernel? liveKernel;
            private readonly long liveRevision;
            public int SharedLegs { get; private set; }
            public int ReusedLegs { get; private set; }
            internal int SharedPool { get; private set; }
            public long SourceLegsExamined { get; private set; }
            private bool HasTraffic => kernelMode ? sourceVehicles != null : trafficState != null;
            private IReadOnlyList<TrafficVehicleState> OwnedVehicles => kernelMode ? sourceVehicles! : trafficState!.Vehicles;

            private readonly bool coalesce;

            internal TravelRetime(CityState state, double walk, double rate, bool legacy, RetimeSource? source = null, int sharedLegHint = 0, bool coalesce = true)
            {
                State = state; this.walk = walk; this.rate = rate; this.legacy = legacy; this.sharedLegHint = Math.Max(0, sharedLegHint); this.coalesce = coalesce;
                legRate = LegClockRate(rate);
                ratio = rate / state.TravelClockRate; minute = state.Minute; stateRate = state.TravelClockRate;
                version = state.TravelTimingVersion; places = state.Places.Count; trafficState = state.Traffic;
                kernelMode = source != null;
                if (kernelMode)
                {
                    retimeSnapshot = state.RetimeSnapshotSteps;
                    var kernel = source!.Kernel;
                    if (kernel != null)
                    {
                        if (legacy || kernel.Clock != state.Minute)
                            throw new ArgumentException("Traffic needs native timing at the city clock.");
                        sourceVehicles = new List<TrafficVehicleState>(kernel.VehicleCount);
                        kernel.CopyVehiclesInOrder(sourceVehicles);
                        trafficRoutes.EnsureCapacity(sourceVehicles.Count);
                        foreach (var vehicle in sourceVehicles) trafficRoutes[vehicle.VehicleId] = vehicle;
                        savedTraffic = kernel;
                    }
                }
                else if (state.Traffic != null)
                {
                    if (legacy || state.Traffic.Clock != state.Minute)
                        throw new ArgumentException("Traffic needs native timing at the city clock.");
                    trafficRoutes.EnsureCapacity(state.Traffic.Vehicles.Count);
                    foreach (var vehicle in state.Traffic.Vehicles) trafficRoutes[vehicle.VehicleId] = vehicle;
                    if (state.SnapshotSynchronizer != null && ReferenceEquals(state.TrafficSourceState, state.Traffic)
                        && state.TrafficSource != null && state.TrafficSource.Clock == state.Minute)
                        liveTraffic = state.TrafficSource;
                }
                trafficChanges = new Dictionary<string, TrafficCheck>(trafficRoutes.Count, StringComparer.Ordinal);
                placeIndex = new Dictionary<string, Place>(places, StringComparer.Ordinal);
                foreach (var place in state.Places) placeIndex.Add(place.Id, place);
                schedules = new List<CitizenState>(state.Citizens.Count + state.VisitorRoutines.Count);
                foreach (var citizen in PersonTable.Schedules(state)) schedules.Add(citizen);
                inputs = new Input[schedules.Count];
                stage = kernelMode || trafficState == null ? 1 : 0;
                if (liveTraffic != null) { savedTraffic = liveTraffic; stage = 1; }
                liveKernel = state.CurrentLiveTraffic(out liveRevision);
            }

            public CityState State { get; }
            public double ClockRate => rate;
            public int Degraded { get; private set; }
            public int Split { get; private set; }
            public int Joined { get; private set; }
            public int JoinsRefused { get; private set; }
            public int Prepared => next;
            public int Count => schedules.Count;
            public bool Ready => trafficDone;

            public bool Stale => committed || State.Minute != minute || State.TravelClockRate != stateRate
                || State.TravelTimingVersion != version || !ReferenceEquals(State.Traffic, trafficState)
                || (kernelMode && !ReferenceEquals(State.RetimeSnapshotSteps, retimeSnapshot))
                || !State.LiveTrafficUnchanged(liveKernel, liveRevision)
                || State.Places.Count != places || State.Citizens.Count + State.VisitorRoutines.Count != schedules.Count;

            public bool Step(long deadlineTimestamp)
            {
                if (committed) throw new InvalidOperationException("The retime was already committed.");
                if (Stale) throw new InvalidOperationException("The retime inputs changed; begin a new retime.");
                bool worked = false;
                while (!trafficDone)
                {
                    if (stage == 1)
                    {
                        while (seeded < schedules.Count)
                        {
                            Seed(seeded++); worked = true;
                            if ((seeded & SeedsPerDeadlineCheck - 1) == 0 && System.Diagnostics.Stopwatch.GetTimestamp() >= deadlineTimestamp) return false;
                        }
                        while (next < schedules.Count)
                        {
                            Prepare(next++); worked = true;
                            if (System.Diagnostics.Stopwatch.GetTimestamp() >= deadlineTimestamp) return false;
                        }
                        stage = !HasTraffic ? 9 : evicted.Count > 0 ? 2 : 4;
                        if (stage == 4) kernel = savedTraffic;
                        continue;
                    }
                    if (stage == 4)
                    {
                        if (worked && System.Diagnostics.Stopwatch.GetTimestamp() >= deadlineTimestamp) return false;
                        CheckTrafficOwners(); worked = true; stage = 5;
                        continue;
                    }
                    if (stage == 6 && kernelMode)
                    {
                        restoredTraffic = new RetimedTraffic(null, rescaledKernel!);
                        checkVehicles = new List<TrafficVehicleState>(rescaledKernel!.VehicleCount);
                        rescaledKernel.CopyVehiclesInOrder(checkVehicles);
                        stage = 8;
                        continue;
                    }
                    if (stage == 8)
                    {
                        var checking = kernelMode ? checkVehicles! : traffic!.Vehicles;
                        while (checkedVehicles < checking.Count)
                        {
                            if (worked && System.Diagnostics.Stopwatch.GetTimestamp() >= deadlineTimestamp) return false;
                            CheckVehicle(checking[checkedVehicles++]); worked = true;
                        }
                        stage = 9; rescaledKernel = null;
                        continue;
                    }
                    if (stage == 9) { trafficDone = true; break; }
                    if (worked && System.Diagnostics.Stopwatch.GetTimestamp() >= deadlineTimestamp) return false;
                    pass ??= StartPass();
                    while (true)
                    {
                        if (!pass.MoveNext()) throw new InvalidOperationException("Traffic retime pass ended without a result.");
                        worked = true;
                        if (pass.Current != null) { FinishPass(pass.Current); pass = null; stage = stage == 6 ? 8 : stage + 1; break; }
                        if (System.Diagnostics.Stopwatch.GetTimestamp() >= deadlineTimestamp) return false;
                    }
                }
                return true;
            }

            private System.Collections.IEnumerator StartPass()
            {
                var state = State;
                switch (stage)
                {
                    case 0: return TrafficKernel.RestoreInSteps(trafficState!, LinksPerStep);
                    case 2: return savedTraffic!.CaptureInSteps(evicted.Contains, LinksPerStep);
                    case 3: return TrafficKernel.RestoreInSteps(excluded!, LinksPerStep);
                    case 5:
                        return kernel!.RescaleTimeInSteps(ratio, (id, value) => maps.TryGetValue(id, out var mapping)
                            ? mapping.Map(value) : throw new ArgumentException("Missing traffic geometry map.", nameof(state)), LinksPerStep);
                    case 6: return rescaledKernel!.CaptureSharedInSteps(LinksPerStep);
                    default: throw new InvalidOperationException("Unknown traffic retime pass.");
                }
            }

            private void FinishPass(object result)
            {
                switch (stage)
                {
                    case 0: savedTraffic = (TrafficKernel)result; break;
                    case 2: excluded = (TrafficKernelState)result; break;
                    case 3: kernel = (TrafficKernel)result; excluded = null; break;
                    case 5: rescaledKernel = (TrafficKernel)result; kernel = null; break;
                    case 6: traffic = (TrafficKernelState)result; restoredTraffic = new RetimedTraffic(traffic, rescaledKernel!); break;
                }
            }

            private void CheckTrafficOwners()
            {
                var state = State;
                citizens = new Dictionary<string, CitizenState>(StringComparer.Ordinal);
                foreach (var citizen in state.Citizens) citizens.Add(citizen.Identity.Id, citizen);
                foreach (var vehicle in OwnedVehicles)
                {
                    if (evicted.Contains(vehicle.VehicleId)) continue;
                    if (!citizens.TryGetValue(vehicle.VehicleId, out var citizen) || citizen.Journey == null
                        || !maps.ContainsKey(vehicle.VehicleId)) throw new ArgumentException("Traffic owner has no committed journey.", nameof(state));
                    double elapsed = kernel!.GetPermission(vehicle.VehicleId).NominalMinutes;
                    if (Math.Abs(elapsed - (state.Minute - citizen.DepartureMinute)) > 1e-7)
                        throw new ArgumentException("Traffic and journey progress disagree.", nameof(state));
                }
            }

            public RetimedTraffic? TakeRestoredTraffic()
            {
                var restored = committed ? restoredTraffic : null;
                restoredTraffic = null;
                return restored;
            }

            public bool Commit()
            {
                if (!trafficDone || committed) throw new InvalidOperationException("The retime is not ready to commit.");
                var state = State;
                if (!Unchanged()) return false;
                var meals = new List<(ResidentMeal Meal, double ReturnMinutes, double DueAt)>();
                if (state.Food != null)
                {
                    var arrivals = new Dictionary<string, double>(StringComparer.Ordinal);
                    foreach (var change in changes)
                        if (change.Route != null && change.Citizen.MealOuting != null && !change.Citizen.MealOuting.Returning)
                            arrivals.Add(change.Citizen.Identity.Id, change.EventMinute);
                    foreach (var meal in state.Food.Residents)
                    {
                        if (meal.HostSlot > 0) continue;
                        if (meal.Status != MealStatus.Travelling && meal.Status != MealStatus.Serving) continue;
                        double back = meal.ReturnMinutes * ratio;
                        double due = meal.Status == MealStatus.Travelling && arrivals.TryGetValue(meal.ResidentId, out var arrival)
                            ? arrival : meal.DueAt;
                        if (!Position.Finite(back) || back < 0 || back > MaxLegMinutes || !Position.Finite(due)
                            || due < state.Minute || due > MaxMinute) throw new ArgumentException("Retimed meal exceeds the supported clock.");
                        meals.Add((meal, back, due));
                    }
                }
                committed = true;
                SharedLegs = sharedLegs?.Copies ?? 0; ReusedLegs = sharedLegs?.Reused ?? 0; SharedPool = sharedLegs?.Count ?? 0; SourceLegsExamined = sourceLegs.Reads;
                sharedLegs = null;
                foreach (var change in changes)
                {
                    if (change.Route != null) change.Citizen.Journey = change.Route.Commit();
                    if (change.DepartureMinute.HasValue) change.Citizen.DepartureMinute = change.DepartureMinute.Value;
                    change.Citizen.NextEventMinute = change.EventMinute;
                    if (change.MealStayUntil.HasValue) change.Citizen.ActivityUntilMinute = change.MealStayUntil.Value;
                }
                for (int i = 0; i < schedules.Count; i++)
                {
                    var citizen = schedules[i];
                    if (inputs[i].SafelyHome) citizen.NextEventMinute = state.Minute;
                    citizen.CommutePreparation = CommutePreparation.Reset(citizen.CommutePreparation);
                }
                foreach (var meal in meals) { meal.Meal.ReturnMinutes = meal.ReturnMinutes; meal.Meal.DueAt = meal.DueAt; }
                state.TravelTimingVersion = 1;
                state.TravelClockRate = rate;
                state.RetimeSnapshotSteps = null; state.RetimeSource = null; state.LiveTraffic = null;
                if (kernelMode)
                {
                    state.Traffic = null;
                    state.PendingRetimedTraffic = restoredTraffic?.Kernel;
                    state.SnapshotSynchronizer = restoredTraffic != null ? state.CaptureRetimedTraffic : (Action?)null;
                    state.SnapshotSteps = restoredTraffic != null ? state.CaptureRetimedTrafficInSteps : (Func<int, IEnumerator<bool>>?)null;
                }
                else
                {
                    if (traffic != null) state.Traffic = traffic;
                    state.SnapshotSynchronizer = null;
                    state.SnapshotSteps = null;
                    state.PendingRetimedTraffic = null;
                }
                state.TrafficSource = null; state.TrafficSourceState = null;
                return true;
            }

            private bool Unchanged()
            {
                var state = State;
                if (Stale) return false;
                int index = 0;
                foreach (var citizen in PersonTable.Schedules(state))
                {
                    if (!ReferenceEquals(citizen, schedules[index])) return false;
                    ref var input = ref inputs[index++];
                    if (citizen.Phase != input.Phase || !ReferenceEquals(citizen.Journey, input.Journey)
                        || citizen.DepartureMinute != input.Departure || citizen.NextEventMinute != input.NextEvent
                        || citizen.ActivityUntilMinute != input.ActivityUntil || !ReferenceEquals(citizen.MealOuting, input.Meal)
                        || (citizen.MealOuting != null && citizen.MealOuting.Returning != input.Returning)
                        || !string.Equals(citizen.PlaceId, input.PlaceId, StringComparison.Ordinal)
                        || !string.Equals(citizen.DestinationId, input.DestinationId, StringComparison.Ordinal)) return false;
                    if (citizen.Phase != CitizenPhase.Travelling)
                    {
                        if (ResidentLifecycle.IsSafelyHome(citizen) != input.SafelyHome) return false;
                    }
                    else if (input.Origin == null || input.Destination == null
                        || !placeIndex.TryGetValue(citizen.PlaceId, out var origin) || !ReferenceEquals(origin, input.Origin)
                        || !placeIndex.TryGetValue(citizen.DestinationId, out var destination) || !ReferenceEquals(destination, input.Destination)
                        || !origin.Position.Equals(input.OriginPosition) || !destination.Position.Equals(input.DestinationPosition)) return false;
                }
                for (int i = 0; i < state.Places.Count; i++)
                    if (!placeIndex.TryGetValue(state.Places[i].Id, out var place) || !ReferenceEquals(place, state.Places[i])) return false;
                return true;
            }

            private const int SeedsPerDeadlineCheck = 64;

            private void Seed(int index)
            {
                var citizen = schedules[index];
                if (citizen == null || citizen.Phase != CitizenPhase.Travelling || citizen.Journey == null) return;
                var pool = sharedLegs ??= new SharedLegPool(sharedLegHint);
                var legs = sourceLegs.Over(citizen.Journey.Legs);
                double elapsed = State.Minute - citizen.DepartureMinute, end = 0;
                for (int i = 0; i < legs.Count; i++)
                {
                    var leg = legs[i];
                    end += leg.Minutes;
                    if (!(end <= elapsed)) return;
                    if (leg.Shared && !leg.SplitPrefix) pool.Seed(leg);
                }
            }

            private void Prepare(int index)
            {
                var state = State;
                var citizen = schedules[index];
                if (citizen == null || !Position.Finite(citizen.NextEventMinute)
                    || citizen.NextEventMinute < state.Minute || citizen.NextEventMinute > MaxMinute)
                    throw new ArgumentException("Invalid citizen event.", nameof(state));
                ref var input = ref inputs[index];
                input.Citizen = citizen; input.Phase = citizen.Phase; input.Journey = citizen.Journey;
                input.Departure = citizen.DepartureMinute; input.NextEvent = citizen.NextEventMinute; input.ActivityUntil = citizen.ActivityUntilMinute;
                input.PlaceId = citizen.PlaceId; input.DestinationId = citizen.DestinationId;
                input.Meal = citizen.MealOuting; input.Returning = citizen.MealOuting?.Returning ?? false;
                if (citizen.Phase != CitizenPhase.Travelling)
                {
                    input.SafelyHome = ResidentLifecycle.IsSafelyHome(citizen);
                    return;
                }
                var route = citizen.Journey;
                if (route == null || !placeIndex.TryGetValue(citizen.PlaceId, out var origin)
                    || !placeIndex.TryGetValue(citizen.DestinationId, out var destination))
                    throw new ArgumentException("Missing committed journey or place.", nameof(state));
                input.Origin = origin; input.Destination = destination;
                input.OriginPosition = origin.Position; input.DestinationPosition = destination.Position;
                ValidateCommittedRoute(route, origin.Position, destination.Position, citizen);
                double routeMinutes = route.Minutes;
                if (!Position.Finite(citizen.DepartureMinute) || citizen.DepartureMinute < 0 || citizen.DepartureMinute > state.Minute)
                    throw new ArgumentException("Invalid journey departure.", nameof(state));
                double rawElapsed = state.Minute - citizen.DepartureMinute;
                double clockElapsed = CitySimulator.NormalizeJourneyElapsed(rawElapsed, routeMinutes, state.Minute);
                double elapsed = clockElapsed;
                bool trafficOwned = false;
                if (savedTraffic != null && savedTraffic.TryGetVehicle(citizen.Identity.Id, out var ownedTraffic))
                {
                    if (Math.Abs(ownedTraffic.NominalMinutes - rawElapsed) > 1e-7)
                        throw new ArgumentException("Traffic and journey progress disagree.", nameof(state));
                    elapsed = ownedTraffic.NominalMinutes;
                    trafficOwned = true;
                }
                double arrivalDifference = Math.Abs(citizen.NextEventMinute - (citizen.DepartureMinute + routeMinutes));
                double arrivalTolerance = citizen.NextEventMinute == state.Minute && clockElapsed == routeMinutes
                    ? Math.Max(1e-7, CitySimulator.JourneyClockTolerance(state.Minute, routeMinutes)) : 1e-7;
                if (elapsed > routeMinutes || arrivalDifference > arrivalTolerance)
                    throw new ArgumentException("Journey clock does not match its route.", nameof(state));
                bool mapped = trafficOwned;
                var shared = sharedLegs ??= new SharedLegPool(sharedLegHint);
                var parked = citizen.Car?.ParkedPosition;
                var source = sourceLegs.Over(route.Legs, routeMinutes);
                var measured = CitySimulator.SampleLegs(source, source.Minutes, elapsed, parked);
                var before = new Pose(measured);
                var primary = primaryRoute ??= new Route(new RouteLegs(MaxLegs + 1));
                var replacement = RetimeJoiningSplits(source, elapsed, legacy, walk, rate, legRate, ratio, false, mapped, true, trafficOwned ? trafficRoutes[citizen.Identity.Id] : null,
                    out var map, out int completed, out int merges, shared, measured, primary);
                Position? oldCar = merges > 0 ? CarAt(source, elapsed, parked) : null;
                if (merges > 0 && !SameJoinedPose(before, oldCar, parked, replacement,
                    map != null ? map.Map(elapsed) : CitySimulator.NormalizeJourneyElapsed(rawElapsed, replacement.Minutes, state.Minute)))
                {
                    var unjoined = RetimeSharing(source, elapsed, legacy, walk, rate, legRate, ratio, false, mapped, out var unjoinedMap, out int unjoinedCompleted, shared, measured, AlternateRoute());
                    if (map != null && unjoinedMap != null && JoinKeepsMappedPose(before, oldCar, parked, replacement, map.Map(elapsed), unjoined, unjoinedMap.Map(elapsed))) Joined++;
                    else
                    {
                        replacement = unjoined; map = unjoinedMap; completed = unjoinedCompleted;
                        JoinsRefused++;
                    }
                }
                else if (merges > 0) Joined++;
                bool split = replacement.Legs.Count > route.Legs.Count;
                if (replacement.Legs.Count > MaxLegs
                    && !MergeCompletedLegs(replacement, completed, elapsed, trafficOwned ? trafficRoutes[citizen.Identity.Id] : null))
                {
                    replacement = RetimeSharing(source, elapsed, legacy, walk, rate, legRate, ratio, true, mapped, out map, out _, shared, measured, AlternateRoute());
                    Degraded++;
                    if (trafficOwned) { evicted.Add(citizen.Identity.Id); trafficOwned = false; }
                }
                else if (trafficOwned && TrafficCannotFollow(trafficRoutes[citizen.Identity.Id], map!, elapsed, ratio))
                {
                    Degraded++;
                    evicted.Add(citizen.Identity.Id); trafficOwned = false;
                }
                if (coalesce && !trafficOwned && map == null && measured.LegIndex >= 1 && measured.LegFraction == 0
                    && CoalesceBoundarySplit(measured, source, parked, replacement, rawElapsed, state.Minute, legacy, walk, legRate, shared,
                        ReferenceEquals(replacement, primary) ? AlternateRoute() : primary, out var coalesced))
                    replacement = coalesced;
                if (map != null) maps.Add(citizen.Identity.Id, map);
                ValidateCommittedRoute(replacement, origin.Position, destination.Position, citizen);
                double departure = citizen.DepartureMinute;
                if (evicted.Contains(citizen.Identity.Id))
                {
                    double resumed = map!.Map(elapsed);
                    departure = state.Minute - resumed;
                    if (!Position.Finite(departure) || departure < 0 || departure > state.Minute)
                        throw new ArgumentException("Invalid journey departure.", nameof(state));
                    ValidateSampledPose(before, parked, replacement, resumed);
                }
                else if (!trafficOwned)
                    ValidateSampledPose(before, parked, replacement,
                        CitySimulator.NormalizeJourneyElapsed(rawElapsed, replacement.Minutes, state.Minute));
                double arrival = departure + replacement.Minutes;
                if (arrival < state.Minute && state.Minute - arrival <= CitySimulator.JourneyClockTolerance(state.Minute, replacement.Minutes))
                    arrival = state.Minute;
                if (!Position.Finite(arrival) || arrival < state.Minute || arrival > MaxMinute)
                    throw new ArgumentException("Retimed arrival exceeds the supported clock.", nameof(state));
                if (split) Split++;
                var published = new Route(new RouteLegs(replacement.Legs)).Commit();
                primary.Legs.Clear();
                alternateRoute?.Legs.Clear();
                PresentationGrid.Precompute(published);
                var change = new Change { Citizen = citizen, Route = published, EventMinute = arrival,
                    DepartureMinute = departure == citizen.DepartureMinute ? (double?)null : departure };
                if (citizen.MealOuting != null && !citizen.MealOuting.Returning)
                {
                    double until = citizen.ActivityUntilMinute + arrival - citizen.NextEventMinute;
                    if (!Position.Finite(until) || until < arrival || until > MaxMinute)
                        throw new ArgumentException("Retimed meal stay exceeds the supported clock.");
                    change.MealStayUntil = until;
                }
                changes.Add(change);
                if (trafficOwned) trafficChanges.Add(citizen.Identity.Id, new TrafficCheck(change, before));
            }

            private Route AlternateRoute() => alternateRoute ??= new Route(new RouteLegs(MaxLegs + 1));

            private void CheckVehicle(TrafficVehicleState vehicle)
            {
                double expected = rescaledKernel!.GetPermission(vehicle.VehicleId).NominalMinutes;
                var check = trafficChanges[vehicle.VehicleId];
                var change = check.Change;
                ValidateSampledPose(check.Before, change.Citizen.Car?.ParkedPosition, change.Route!, expected);
                double actual = State.Minute - citizens![vehicle.VehicleId].DepartureMinute;
                if (Math.Abs(expected - actual) > 1e-7)
                    throw new ArgumentException("Retimed traffic progress and committed journey disagree.");
                if (vehicle.Completed) return;
                foreach (var span in vehicle.Route)
                    if (span.EndNominalMinutes > vehicle.NominalMinutes)
                        TrafficNetwork.ValidateCommittedSpanTiming(span);
            }
        }

        private const int CoalesceMinuteSteps = 8;

        private static bool Coalescable(RouteLeg leg) => leg.Mode == TravelMode.Walk || leg.Mode == TravelMode.Subway;

        private static bool CoalesceBoundarySplit(in PhysicalJourneySample measured, SourceLegs source, Position? parked, Route route,
            double rawElapsed, double minute, bool legacy, double walkSpeed, double legRate, SharedLegPool shared, Route scratch, out Route coalesced)
        {
            coalesced = route;
            var split = source[measured.LegIndex - 1];
            if (!Coalescable(split) || !ContinuesInto(split, source, measured.LegIndex - 1)) return false;
            var before = new Pose(measured);
            var sample = CitySimulator.SampleRoute(route, CitySimulator.NormalizeJourneyElapsed(rawElapsed, route.Minutes, minute), parked);
            int index = sample.LegIndex;
            if (index < 1 || sample.LegFraction != 0) return false;
            RouteLeg previous = route.Legs[index - 1], leg = route.Legs[index];
            if (!Coalescable(previous) || !ContinuesFragment(previous, leg.Mode, leg.From, leg.To, leg.ServiceId)) return false;
            double length = Position.Distance(previous.From, previous.To), nextLength = Position.Distance(leg.From, leg.To);
            if (!(length > 0.000001) || !(nextLength > 0.000001)) return false;
            double pace = previous.Minutes / length, nextPace = leg.Minutes / nextLength;
            if (Math.Abs(pace - nextPace) > SplitPaceTolerance * Math.Max(pace, nextPace)) return false;
            double minutes = previous.Minutes + leg.Minutes;
            if (!Position.Finite(minutes) || minutes > MaxLegMinutes) return false;
            double metresPerMinute = legacy ? 0 : PlannedMetresPerMinute(previous, walkSpeed, legRate);
            double planned = metresPerMinute > 0 ? Position.Distance(previous.From, leg.To) / metresPerMinute : 0;
            bool tryPlanned = Position.Finite(planned) && planned != minutes && Math.Abs(planned - minutes) <= PlannedPaceTolerance * minutes;
            RouteLeg? joined = null;
            Route? candidateRoute = null;
            for (int step = tryPlanned ? -1 : 0; step <= 2 * CoalesceMinuteSteps; step++)
            {
                double candidate = step < 0 ? planned
                    : BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(minutes) + (step + 1) / 2 * (step % 2 == 0 ? -1 : 1));
                if (!(candidate > previous.Minutes) || candidate > MaxLegMinutes) continue;
                if (joined == null)
                {
                    joined = previous.CopyAs(previous.From, leg.To, candidate, leg.ServiceId, false);
                    var legs = scratch.Legs;
                    legs.Clear();
                    for (int i = 0; i < route.Legs.Count; i++)
                        if (i == index - 1) legs.Add(joined);
                        else if (i != index) legs.Add(route.Legs[i]);
                    candidateRoute = scratch;
                }
                else joined.Minutes = candidate;
                double resumed = CitySimulator.NormalizeJourneyElapsed(rawElapsed, candidateRoute!.Minutes, minute);
                if (!SamePose(before, parked, candidateRoute, resumed)) continue;
                var probe = CitySimulator.SampleRoute(candidateRoute, resumed, parked);
                if (probe.LegIndex != index - 1 || !(probe.LegFraction > 0) || !(probe.LegFraction < 1)) continue;
                candidateRoute.Legs[index - 1] = shared.Join(joined);
                coalesced = candidateRoute;
                return true;
            }
            return false;
        }

        private static void ValidateSampledPose(in Pose before, Position? parked, Route replacement, double resumedElapsed)
        {
            if (!SamePose(before, parked, replacement, resumedElapsed))
                throw new ArgumentException("Retimed journey cannot preserve its measured physical pose.");
        }

        private static bool SameJoinedPose(in Pose before, Position? oldCar, Position? parked, Route replacement, double resumedElapsed)
            => SamePose(before, parked, replacement, resumedElapsed) && Nullable.Equals(oldCar, CarAt(replacement.Legs, resumedElapsed, parked));

        private static bool JoinKeepsMappedPose(in Pose before, Position? old, Position? parked, Route joined, double joinedElapsed, Route unjoined, double unjoinedElapsed)
        {
            if (!SamePose(before, parked, joined, joinedElapsed) || !SamePose(before, parked, unjoined, unjoinedElapsed)) return false;
            Position? join = CarAt(joined.Legs, joinedElapsed, parked), apart = CarAt(unjoined.Legs, unjoinedElapsed, parked);
            if (old == null || join == null || apart == null) return old == null && join == null && apart == null;
            return Position.Distance(old.Value, join.Value) <= Math.Max(Position.Distance(old.Value, apart.Value), UlpMetres(old.Value));
        }

        private static double UlpMetres(Position point)
            => 16 * 2.2204460492503131e-16 * Math.Max(60, Math.Max(Math.Abs(point.X), Math.Max(Math.Abs(point.Y), Math.Abs(point.Z))));

        private static Position? CarAt(IReadOnlyList<RouteLeg> legs, double elapsed, Position? parked)
        {
            if (parked == null) return null;
            var position = parked.Value;
            elapsed = Math.Max(0, elapsed);
            for (int i = 0; i < legs.Count; i++)
            {
                var leg = legs[i];
                if (elapsed <= leg.Minutes) return leg.Mode == TravelMode.Car ? Position.Toward(leg.From, leg.To, elapsed / leg.Minutes) : position;
                if (leg.Mode == TravelMode.Car) position = leg.To;
                elapsed -= leg.Minutes;
            }
            return position;
        }

        private static bool SamePose(in Pose before, Position? parked, Route replacement, double resumedElapsed)
        {
            var newSample = CitySimulator.SampleRoute(replacement, resumedElapsed, parked);
            return before.Position.Equals(newSample.Position) && Nullable.Equals(before.Car, newSample.CarPosition)
                && before.Mode == newSample.Mode;
        }

        private static void ValidateCommittedRoute(Route route, Position from, Position to, CitizenState citizen)
        {
            try
            {
                CitySimulator.ValidateRoute(route, from, to);
                CitySimulator.ValidateCarRoute(route, citizen);
            }
            catch (InvalidOperationException e) { throw new ArgumentException("Invalid committed route.", nameof(route), e); }
        }

        private static bool TrafficCannotFollow(TrafficVehicleState vehicle, NominalMap map, double observed, double ratio)
        {
            if (vehicle.Completed) return false;
            if (vehicle.RouteIndex >= 0)
            {
                double endpoint = vehicle.Route[vehicle.RouteIndex].EndNominalMinutes;
                if (observed < endpoint && map.Map(endpoint) <= map.Map(observed)) return true;
            }
            return ratio != 1 && map.HasFutureUnscaledTrafficPrefix(map.Map(observed), vehicle.Route);
        }

        private static bool MergeCompletedLegs(Route route, int completed, double elapsed, TrafficVehicleState? traffic)
        {
            var legs = route.Legs;
            int best = -1;
            double bestDeviation = double.PositiveInfinity, bestMinutes = 0, start = 0;
            for (int i = 0; i + 1 < completed; i++)
            {
                RouteLeg first = legs[i], second = legs[i + 1];
                double middle = start + first.Minutes, end = middle + second.Minutes, minutes = end - start;
                if (first.Mode == second.Mode && (first.Mode == TravelMode.Car || first.Mode == TravelMode.Walk || first.ServiceId == second.ServiceId)
                    && minutes > 0 && minutes <= MaxLegMinutes && start + minutes == end && !OverlapsRetainedTraffic(traffic, start, end, elapsed))
                {
                    double deviation = SegmentDistance(first.To, first.From, second.To);
                    if (deviation < bestDeviation) { best = i; bestDeviation = deviation; bestMinutes = minutes; }
                }
                start = middle;
            }
            if (best < 0) return false;
            RouteLeg a = legs[best], b = legs[best + 1];
            string service = a.ServiceId == b.ServiceId && !a.ServiceId.StartsWith("native:road:", StringComparison.Ordinal) ? a.ServiceId
                : a.Mode == TravelMode.Car || a.Mode == TravelMode.Walk ? "" : a.ServiceId;
            legs[best] = new RouteLeg { From = a.From, To = b.To, Minutes = bestMinutes, Mode = a.Mode, ServiceId = service };
            legs.RemoveAt(best + 1);
            return true;
        }

        private static bool OverlapsRetainedTraffic(TrafficVehicleState? vehicle, double start, double end, double elapsed)
        {
            if (vehicle == null) return false;
            bool active = vehicle.RouteIndex >= 0 && !vehicle.Completed && elapsed < vehicle.Route[vehicle.RouteIndex].EndNominalMinutes;
            for (int i = 0; i < vehicle.Route.Count; i++)
            {
                if (active && i == vehicle.RouteIndex) continue;
                var span = vehicle.Route[i];
                if (span.StartNominalMinutes < end && span.EndNominalMinutes > start) return true;
            }
            return false;
        }

        private const double SpanGeometryMetres = 0.0001;

        private static bool SpanGeometryKept(TrafficVehicleState? vehicle, RouteLeg previous, double start, Position to, double minutes, double merged)
        {
            if (vehicle == null) return true;
            double end = start + merged, middle = start + previous.Minutes;
            foreach (var span in vehicle.Route)
                if (!BoundaryKept(span.StartNominalMinutes, previous, start, middle, end, to, minutes, merged)
                    || !BoundaryKept(span.EndNominalMinutes, previous, start, middle, end, to, minutes, merged)) return false;
            return true;
        }

        private static bool BoundaryKept(double boundary, RouteLeg previous, double start, double middle, double end, Position to, double minutes, double merged)
        {
            if (!(boundary > start) || !(boundary < end)) return true;
            Position separate = boundary <= middle
                ? Position.Lerp(previous.From, previous.To, (boundary - start) / previous.Minutes)
                : Position.Lerp(previous.To, to, Math.Min(1, (boundary - middle) / minutes));
            Position joined = Position.Lerp(previous.From, to, (boundary - start) / merged);
            return Position.Distance(separate, joined) <= SpanGeometryMetres;
        }

        private static double SegmentDistance(Position point, Position from, Position to)
        {
            double x = to.X - from.X, y = to.Y - from.Y, z = to.Z - from.Z;
            double square = x * x + y * y + z * z;
            double t = square > 0 ? ((point.X - from.X) * x + (point.Y - from.Y) * y + (point.Z - from.Z) * z) / square : 0;
            return Position.Distance(point, Position.Lerp(from, to, Math.Max(0, Math.Min(1, t))));
        }

        private static Route Retime(Route original, double elapsed, bool legacy, double walkSpeed, double rate, double ratio,
            bool keepCurrent, bool mapped, out NominalMap? map, out int completed)
        {
            var source = new SourceLegs().Over(original.Legs, original.Minutes);
            return RetimeJoiningSplits(source, elapsed, legacy, walkSpeed, rate, LegClockRate(rate), ratio, keepCurrent, mapped, false, null, out map, out completed, out _,
                null, CitySimulator.SampleLegs(source, source.Minutes, elapsed, null));
        }

        private static Route RetimeSharing(SourceLegs original, double elapsed, bool legacy, double walkSpeed, double rate, double legRate, double ratio,
            bool keepCurrent, bool mapped, out NominalMap? map, out int completed, SharedLegPool? shared, in PhysicalJourneySample measured, Route scratch)
            => RetimeJoiningSplits(original, elapsed, legacy, walkSpeed, rate, legRate, ratio, keepCurrent, mapped, false, null, out map, out completed, out _, shared, measured, scratch);

        public const double SubwayMetresPerScaledSecond = 10;
        private const double PlannedPaceTolerance = 0.001;

        public static double LegClockRate(double clockRate)
        {
            if (!Position.Finite(clockRate) || clockRate <= 0) return clockRate;
            double scale = Math.Pow(10, 4 - Math.Floor(Math.Log10(clockRate)));
            double stable = Math.Round(clockRate * scale) / scale;
            return Position.Finite(stable) && stable > 0 ? stable : clockRate;
        }

        public static double WalkingMetresPerMinute(double walkMetresPerSecond, double clockRate) => walkMetresPerSecond / LegClockRate(clockRate);

        public static double SubwayMetresPerMinute(double clockRate) => SubwayMetresPerScaledSecond / LegClockRate(clockRate);

        private static double FutureDuration(RouteLeg leg, bool legacy, double walkSpeed, double rate, double legRate, double ratio)
        {
            double scaled = Duration(leg, leg.From, leg.To, leg.Minutes, legacy, walkSpeed, rate, ratio);
            if (legacy) return scaled;
            double metresPerMinute = PlannedMetresPerMinute(leg, walkSpeed, legRate);
            if (metresPerMinute <= 0) return scaled;
            double length = Position.Distance(leg.From, leg.To);
            if (!(length > 0.000001)) return scaled;
            double planned = length / metresPerMinute;
            return Position.Finite(planned) && planned > 0 && Math.Abs(planned - scaled) <= PlannedPaceTolerance * scaled ? planned : scaled;
        }

        private static double PlannedMetresPerMinute(RouteLeg leg, double walkSpeed, double legRate)
            => leg.Mode == TravelMode.Walk && leg.ServiceId.Length == 0 ? walkSpeed / legRate
                : leg.Mode == TravelMode.Subway && string.Equals(leg.ServiceId, "native:subway", StringComparison.Ordinal) ? SubwayMetresPerScaledSecond / legRate
                : 0;

        private sealed class SharedLegPool
        {
            private readonly HashSet<RouteLeg> legs;
            private readonly RouteLeg probe = new RouteLeg();
            internal SharedLegPool(int capacity) { legs = new HashSet<RouteLeg>(Math.Max(0, capacity), RouteLeg.SameContent); }
            internal int Count => legs.Count;
            internal int Copies { get; private set; }
            internal int Reused { get; private set; }
            internal void Seed(RouteLeg leg) => legs.Add(leg);
            internal bool TryGet(RouteLeg source, double minutes, out RouteLeg known)
            {
                probe.Aim(source, minutes);
                if (!legs.TryGetValue(probe, out known!)) return false;
                Reused++;
                return true;
            }
            internal void Add(RouteLeg copy) { legs.Add(copy); Copies++; }
            internal RouteLeg Join(RouteLeg joined)
            {
                if (legs.TryGetValue(joined, out var known)) { Reused++; return known; }
                joined.Share();
                legs.Add(joined);
                return joined;
            }
            internal bool TryFind(RouteLeg leg, out RouteLeg known) => legs.TryGetValue(leg, out known!);
        }

        private static RouteLeg Representative(RouteLeg source, RouteLeg copy, SharedLegPool? shared)
            => shared != null && ReferenceEquals(copy, source) && source.Shared && !source.SplitPrefix && shared.TryFind(source, out var known) ? known : copy;

        private static RouteLeg ScaledFuture(RouteLeg leg, SharedLegPool? shared, bool legacy, double walkSpeed, double rate, double legRate, double ratio)
        {
            double duration = FutureDuration(leg, legacy, walkSpeed, rate, legRate, ratio);
            bool reuse = shared != null && leg.Shared;
            if (reuse && shared!.TryGet(leg, duration, out var known)) return known;
            var copy = Copy(leg, leg.From, leg.To, duration, leg.ServiceId);
            if (reuse) { copy.Share(); shared!.Add(copy); }
            return copy;
        }

        private static Route RetimeJoiningSplits(SourceLegs original, double elapsed, bool legacy, double walkSpeed, double rate, double legRate, double ratio,
            bool keepCurrent, bool mapped, bool mergeFragments, TrafficVehicleState? traffic, out NominalMap? map, out int completed, out int merges,
            SharedLegPool? shared, in PhysicalJourneySample measuredSample, Route? scratch = null)
        {
            merges = 0;
            int mergeCount = 0;
            var route = scratch ?? new Route(new RouteLegs(original.Count + 1));
            route.Legs.Clear();
            NominalMap? mapping = null;
            if (mapped) { mapping = new NominalMap(); mapping.Segments.Capacity = original.Count + 2; }
            map = mapping;
            completed = 0;
            double oldOffset = 0, newOffset = 0;
            void Add(RouteLeg leg, Position from, Position to, double minutes, string service, double oldMinutes, double? oldEnd = null, bool splitPrefix = false)
                => AddLeg(Representative(leg, Copy(leg, from, to, minutes, service, splitPrefix), shared), oldMinutes, oldEnd);
            void AddLeg(RouteLeg copy, double oldMinutes, double? oldEnd = null)
            {
                route.Legs.Add(copy);
                double minutes = copy.Minutes;
                double end = oldEnd ?? oldOffset + oldMinutes;
                mapping?.Segments.Add((end, newOffset + minutes));
                oldOffset = end; newOffset += minutes;
            }
            void AddUnscaledCarPrefix(RouteLeg leg, Position from, Position to, double minutes, string service, double oldMinutes, double? oldEnd = null, bool splitPrefix = false)
            {
                double start = newOffset;
                Add(leg, from, to, minutes, service, oldMinutes, oldEnd, splitPrefix);
                if (mapping != null && oldOffset >= elapsed && leg.Mode == TravelMode.Car && Position.Distance(leg.From, leg.To) > 0)
                    mapping.AddUnscaledCarPrefix(start, newOffset);
            }
            int fragment = -1;
            bool fragmentContinues = false, fragmentJoined = false;
            double fragmentStart = 0, fragmentRemaining = 0;
            void ShareJoined()
            {
                if (fragmentJoined && shared != null) route.Legs[fragment] = shared.Join(route.Legs[fragment]);
                fragmentJoined = false;
            }
            void AddCompleted(RouteLeg leg, Position from, Position to, double minutes, string service, double oldMinutes, double? oldEnd, bool splitPrefix, bool continues, double remaining)
            {
                if (mergeFragments && fragment >= 0 && fragment == route.Legs.Count - 1 && fragmentContinues)
                {
                    var previous = route.Legs[fragment];
                    double end = newOffset + minutes;
                    double after = splitPrefix && oldEnd.HasValue ? 0 : remaining - minutes;
                    double merged = fragmentRemaining - after;
                    if (fragmentRemaining - merged != after) merged = end - fragmentStart;
                    double joinedEnd = fragmentStart + merged;
                    if (ContinuesFragment(previous, leg.Mode, from, to, service) && Position.Finite(merged) && merged > 0 && merged <= MaxLegMinutes
                        && Position.Finite(joinedEnd) && joinedEnd >= newOffset && !OverlapsRetainedTraffic(traffic, fragmentStart, joinedEnd, elapsed)
                        && SpanGeometryKept(traffic, previous, fragmentStart, to, minutes, merged))
                    {
                        double segmentEnd = oldEnd ?? oldOffset + oldMinutes;
                        mapping?.Segments.Add((segmentEnd, joinedEnd));
                        if (mapping != null && segmentEnd >= elapsed && leg.Mode == TravelMode.Car && Position.Distance(from, to) > 0)
                            mapping.AddUnscaledCarPrefix(newOffset, joinedEnd);
                        route.Legs[fragment] = previous.CopyAs(previous.From, to, merged, service, splitPrefix);
                        oldOffset = segmentEnd; newOffset = joinedEnd;
                        mergeCount++;
                        fragmentContinues = continues; fragmentJoined = true;
                        return;
                    }
                }
                ShareJoined();
                fragmentStart = newOffset; fragmentRemaining = remaining;
                AddUnscaledCarPrefix(leg, from, to, minutes, service, oldMinutes, oldEnd, splitPrefix);
                fragment = route.Legs.Count - 1;
                fragmentContinues = continues;
            }
            if (elapsed == original.Minutes)
            {
                for (int i = 0; i < original.Count; i++)
                {
                    var leg = original[i];
                    AddUnscaledCarPrefix(leg, leg.From, leg.To, leg.Minutes, leg.ServiceId, leg.Minutes);
                }
                completed = route.Legs.Count;
                merges = mergeCount;
                return route;
            }
            double remainingElapsed = elapsed;
            bool reachedPresent = false;
            for (int originalIndex = 0; originalIndex < original.Count; originalIndex++)
            {
                var leg = original[originalIndex];
                if (!Position.Finite(leg.Minutes) || leg.Minutes <= 0 || leg.Minutes > MaxLegMinutes)
                    throw new ArgumentException("Invalid input leg duration.");
                double oldLegEnd = oldOffset + leg.Minutes;
                if (!reachedPresent && elapsed >= oldLegEnd)
                {
                    AddCompleted(leg, leg.From, leg.To, leg.Minutes, leg.ServiceId, leg.Minutes, null, leg.SplitPrefix, ContinuesInto(leg, original, originalIndex), remainingElapsed);
                    remainingElapsed -= leg.Minutes;
                    completed = route.Legs.Count;
                    continue;
                }
                if (!reachedPresent && elapsed > oldOffset)
                {
                    bool sampledStart = measuredSample.LegIndex == originalIndex && measuredSample.LegFraction == 0;
                    bool sampledEndpoint =
                        (measuredSample.LegIndex == originalIndex + 1 && measuredSample.LegFraction == 0)
                        || (originalIndex == original.Count - 1 && measuredSample.LegIndex == originalIndex
                            && measuredSample.LegFraction == 1);
                    if (sampledStart)
                    {
                        mapping?.Segments.Add((elapsed, newOffset));
                        oldOffset = elapsed;
                        AddLeg(ScaledFuture(leg, shared, legacy, walkSpeed, rate, legRate, ratio), oldLegEnd - elapsed, oldLegEnd);
                        reachedPresent = true;
                        continue;
                    }
                    if (remainingElapsed <= 0 || (remainingElapsed > leg.Minutes && !sampledEndpoint))
                        throw new ArgumentException("A partial route cannot preserve its measured timing interval.");
                    if (keepCurrent || (sampledEndpoint && RetainsFixedMinutes(leg)))
                    {
                        string kept = keepCurrent && leg.Mode == TravelMode.Car
                            && leg.ServiceId.StartsWith("native:road:", StringComparison.Ordinal) ? "" : leg.ServiceId;
                        AddUnscaledCarPrefix(leg, leg.From, leg.To, leg.Minutes, kept, leg.Minutes);
                    }
                    else
                    {
                        string splitService = SplitService(leg);
                        bool nativeTail = leg.Mode == TravelMode.Car && Position.Distance(leg.From, leg.To) > 0
                            && splitService.StartsWith("native:road:fragment1:", StringComparison.Ordinal);
                        var point = sampledEndpoint ? leg.To : Position.Lerp(leg.From, leg.To, remainingElapsed / leg.Minutes);
                        AddCompleted(leg, leg.From, point, remainingElapsed, splitService, remainingElapsed, elapsed, true, true, remainingElapsed);
                        completed = route.Legs.Count;
                        double oldTail = leg.Minutes - remainingElapsed;
                        if (sampledEndpoint || (nativeTail && point.Equals(leg.To)))
                        {
                            if (!sampledEndpoint && oldTail > CitySimulator.JourneyClockTolerance(elapsed, leg.Minutes))
                                throw new ArgumentException("A material native road tail lost its representable geometry.");
                            mapping?.Segments.Add((oldLegEnd, newOffset));
                            oldOffset = oldLegEnd;
                        }
                        else
                        {
                            if (nativeTail && !RepresentableNativeTail(leg, point))
                                throw new ArgumentException("A remaining native road fragment has ambiguous double-precision projection.");
                            double tail = Duration(leg, point, leg.To, oldTail, legacy, walkSpeed, rate, ratio);
                            Add(leg, point, leg.To, tail, splitService, oldTail, oldLegEnd);
                        }
                    }
                }
                else AddLeg(ScaledFuture(leg, shared, legacy, walkSpeed, rate, legRate, ratio), leg.Minutes);
                reachedPresent = true;
            }
            ShareJoined();
            merges = mergeCount;
            return route;
        }

        private const double FragmentDeviationMetres = 0.000001;

        private const double SplitPaceTolerance = 1e-9;

        private static bool ContinuesInto(RouteLeg leg, SourceLegs legs, int index)
        {
            if (index + 1 >= legs.Count) return false;
            var next = legs[index + 1];
            if (!ContinuesFragment(leg, next.Mode, next.From, next.To, next.ServiceId)) return false;
            double length = Position.Distance(leg.From, leg.To), nextLength = Position.Distance(next.From, next.To);
            if (leg.ServiceId.StartsWith(FragmentPrefix, StringComparison.Ordinal) || !(length > 0.000001) || !(nextLength > 0.000001)) return true;
            double pace = leg.Minutes / length, nextPace = next.Minutes / nextLength;
            return Math.Abs(pace - nextPace) > SplitPaceTolerance * Math.Max(pace, nextPace);
        }

        private static bool ContinuesFragment(RouteLeg previous, TravelMode mode, Position from, Position to, string service)
            => previous.Mode == mode
                && string.Equals(previous.ServiceId, service, StringComparison.Ordinal) && previous.To.Equals(from)
                && (previous.From.Equals(from) && from.Equals(to)
                    || Position.Distance(previous.From, from) > 0.000001 && Position.Distance(from, to) > 0.000001
                        && SegmentDistance(from, previous.From, to) <= FragmentDeviationMetres);

        private static bool RepresentableNativeTail(RouteLeg leg, Position point)
        {
            if (leg.ServiceId.StartsWith("native:road:v1:", StringComparison.Ordinal))
            {
                double x = leg.To.X - leg.From.X, y = leg.To.Y - leg.From.Y, z = leg.To.Z - leg.From.Z;
                double square = x * x + y * y + z * z;
                double projected = ((point.X - leg.From.X) * x + (point.Y - leg.From.Y) * y + (point.Z - leg.From.Z) * z) / square;
                if (!Position.Finite(square) || square <= 1e-12 || !Position.Finite(projected) || projected >= 1) return false;
            }
            double coordinate = Math.Max(60, Math.Max(Math.Max(Math.Abs(leg.From.X), Math.Abs(leg.To.X)),
                Math.Max(Math.Max(Math.Abs(leg.From.Y), Math.Abs(leg.To.Y)), Math.Max(Math.Abs(leg.From.Z), Math.Abs(leg.To.Z)))));
            double resolution = 16 * 2.2204460492503131e-16 * coordinate;
            return Position.Distance(point, leg.To) > resolution;
        }

        private static bool RetainsFixedMinutes(RouteLeg leg)
        {
            double distance = Position.Distance(leg.From, leg.To);
            if (distance > 0.000001) return false;
            bool nativeMovement = leg.Mode == TravelMode.Car && distance > 0 && IsRoadMovement(leg.RoadTag);
            return distance <= 0.000001 && !nativeMovement;
        }

        private static double Duration(RouteLeg leg, Position from, Position to, double oldDuration, bool legacy, double walkSpeed, double rate, double ratio)
        {
            double value = RetainsFixedMinutes(leg) ? oldDuration
                : !legacy ? oldDuration * ratio
                : leg.Mode == TravelMode.Walk ? Position.Distance(from, to) / walkSpeed * rate
                : oldDuration * 60 * rate;
            if (!Position.Finite(value) || value <= 0 || value > MaxLegMinutes)
                throw new ArgumentException("Retimed leg duration exceeds supported bounds.");
            return value;
        }

        private static string SplitService(RouteLeg leg)
        {
            string service = leg.ServiceId;
            if (leg.Mode != TravelMode.Car || !service.StartsWith("native:road:", StringComparison.Ordinal)) return service;
            var tag = leg.RoadTag;
            if (!IsRoadMovement(tag)) return "";
            return tag.Kind == RoadTagKind.Fragment1 ? service : FragmentService(service, tag.TopologyStart);
        }

        private static bool IsRoadMovement(RoadTag tag) => tag.Kind != RoadTagKind.None && tag.TopologyLength != 0 && tag.From != tag.To;

        private const string FragmentPrefix = "native:road:fragment1:";

        private static string FragmentService(string service, int topologyStart)
            => string.Create(FragmentPrefix.Length + service.Length - topologyStart, (service, topologyStart), (target, source) =>
            {
                FragmentPrefix.AsSpan().CopyTo(target);
                source.service.AsSpan(source.topologyStart).CopyTo(target.Slice(FragmentPrefix.Length));
            });

        private static RouteLeg Copy(RouteLeg source, Position from, Position to, double minutes, string service, bool splitPrefix = false)
            => source.CopyAs(from, to, minutes, service, splitPrefix);

        private static void Rate(double value, string name)
        {
            if (!Position.Finite(value) || value < 0.01 || value > 1000) throw new ArgumentOutOfRangeException(name);
        }
    }
}
