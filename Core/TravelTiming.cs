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
            internal double Elapsed;
            internal double? MealStayUntil;
            internal double? DepartureMinute;
        }
        private sealed class NominalMap
        {
            internal readonly List<(double OldStart, double OldEnd, double NewStart, double NewEnd)> Segments =
                new List<(double, double, double, double)>();
            internal readonly List<(double Start, double End)> UnscaledCarPrefixes = new List<(double, double)>();
            internal bool HasFutureUnscaledTrafficPrefix(double observed, IReadOnlyList<TrafficRouteLeg> route)
            {
                foreach (var prefix in UnscaledCarPrefixes)
                {
                    double start = Math.Max(observed, prefix.Start);
                    if (prefix.End <= start) continue;
                    foreach (var span in route)
                        if (Map(span.EndNominalMinutes) > start && Map(span.StartNominalMinutes) < prefix.End) return true;
                }
                return false;
            }
            internal double Map(double value)
            {
                if (!Position.Finite(value) || value < 0) throw new ArgumentException("Invalid traffic route offset.");
                foreach (var segment in Segments)
                {
                    if (value > segment.OldEnd) continue;
                    if (value < segment.OldStart) break;
                    if (value == segment.OldStart) return segment.NewStart;
                    if (value == segment.OldEnd) return segment.NewEnd;
                    return segment.NewStart + (value - segment.OldStart) / (segment.OldEnd - segment.OldStart)
                        * (segment.NewEnd - segment.NewStart);
                }
                throw new ArgumentException("Traffic offset is outside the committed route.");
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

        internal static TravelRetime? Begin(CityState state, double nativeWalkMetresPerSecond, double clockRate, RetimeSource? source, int sharedLegHint = 0)
        {
            if (!Needed(state, nativeWalkMetresPerSecond, clockRate)) return null;
            return new TravelRetime(state, nativeWalkMetresPerSecond, clockRate, state.TravelTimingVersion == 0, source, sharedLegHint);
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
            private readonly double walk, rate, ratio, minute, stateRate;
            private readonly bool legacy;
            private readonly int version, places;
            private readonly TrafficKernelState? trafficState;
            private readonly List<CitizenState> schedules = new List<CitizenState>();
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
            private readonly Dictionary<string, Change> trafficChanges = new Dictionary<string, Change>(StringComparer.Ordinal);
            private readonly Dictionary<string, Place> placeIndex = new Dictionary<string, Place>(StringComparer.Ordinal);
            private TrafficKernelState? traffic;
            private TrafficKernel? rescaledKernel;
            private Dictionary<string, CitizenState>? citizens;
            private int next, checkedVehicles, stage;
            private bool trafficDone, committed;
            private readonly bool kernelMode;
            private readonly Func<int, IEnumerator<bool>>? retimeSnapshot;
            private readonly List<TrafficVehicleState>? sourceVehicles;
            private List<TrafficVehicleState>? checkVehicles;
            private Dictionary<RouteLeg, RouteLeg>? sharedLegs;
            private readonly int sharedLegHint;
            private readonly TrafficKernel? liveKernel;
            private readonly long liveRevision;
            public int SharedLegs { get; private set; }
            private bool HasTraffic => kernelMode ? sourceVehicles != null : trafficState != null;
            private IReadOnlyList<TrafficVehicleState> OwnedVehicles => kernelMode ? sourceVehicles! : trafficState!.Vehicles;

            internal TravelRetime(CityState state, double walk, double rate, bool legacy, RetimeSource? source = null, int sharedLegHint = 0)
            {
                State = state; this.walk = walk; this.rate = rate; this.legacy = legacy; this.sharedLegHint = Math.Max(0, sharedLegHint);
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
                        foreach (var vehicle in sourceVehicles) trafficRoutes[vehicle.VehicleId] = vehicle;
                        savedTraffic = kernel;
                    }
                }
                else if (state.Traffic != null)
                {
                    if (legacy || state.Traffic.Clock != state.Minute)
                        throw new ArgumentException("Traffic needs native timing at the city clock.");
                    foreach (var vehicle in state.Traffic.Vehicles) trafficRoutes[vehicle.VehicleId] = vehicle;
                    if (state.SnapshotSynchronizer != null && ReferenceEquals(state.TrafficSourceState, state.Traffic)
                        && state.TrafficSource != null && state.TrafficSource.Clock == state.Minute)
                        liveTraffic = state.TrafficSource;
                }
                foreach (var place in state.Places) placeIndex.Add(place.Id, place);
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
                SharedLegs = sharedLegs?.Count ?? 0;
                sharedLegs = null;
                foreach (var change in changes)
                {
                    if (change.Route != null) change.Citizen.Journey = change.Route.Commit();
                    if (change.DepartureMinute.HasValue) change.Citizen.DepartureMinute = change.DepartureMinute.Value;
                    change.Citizen.NextEventMinute = change.EventMinute;
                    if (change.MealStayUntil.HasValue) change.Citizen.ActivityUntilMinute = change.MealStayUntil.Value;
                }
                foreach (var citizen in schedules) citizen.CommutePreparation = new CommutePreparation();
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
                    if (input.SafelyHome)
                        changes.Add(new Change { Citizen = citizen, EventMinute = state.Minute });
                    return;
                }
                var route = citizen.Journey;
                if (route == null || !placeIndex.TryGetValue(citizen.PlaceId, out var origin)
                    || !placeIndex.TryGetValue(citizen.DestinationId, out var destination))
                    throw new ArgumentException("Missing committed journey or place.", nameof(state));
                input.Origin = origin; input.Destination = destination;
                input.OriginPosition = origin.Position; input.DestinationPosition = destination.Position;
                ValidateCommittedRoute(route, origin.Position, destination.Position, citizen);
                if (!Position.Finite(citizen.DepartureMinute) || citizen.DepartureMinute < 0 || citizen.DepartureMinute > state.Minute)
                    throw new ArgumentException("Invalid journey departure.", nameof(state));
                double rawElapsed = state.Minute - citizen.DepartureMinute;
                double clockElapsed = CitySimulator.NormalizeJourneyElapsed(rawElapsed, route.Minutes, state.Minute);
                double elapsed = clockElapsed;
                bool trafficOwned = false;
                if (savedTraffic != null && savedTraffic.TryGetVehicle(citizen.Identity.Id, out var ownedTraffic))
                {
                    if (Math.Abs(ownedTraffic.NominalMinutes - rawElapsed) > 1e-7)
                        throw new ArgumentException("Traffic and journey progress disagree.", nameof(state));
                    elapsed = ownedTraffic.NominalMinutes;
                    trafficOwned = true;
                }
                double arrivalDifference = Math.Abs(citizen.NextEventMinute - (citizen.DepartureMinute + route.Minutes));
                double arrivalTolerance = citizen.NextEventMinute == state.Minute && clockElapsed == route.Minutes
                    ? Math.Max(1e-7, CitySimulator.JourneyClockTolerance(state.Minute, route.Minutes)) : 1e-7;
                if (elapsed > route.Minutes || arrivalDifference > arrivalTolerance)
                    throw new ArgumentException("Journey clock does not match its route.", nameof(state));
                bool mapped = trafficOwned;
                var shared = sharedLegs ??= new Dictionary<RouteLeg, RouteLeg>(sharedLegHint, RouteLeg.SameContent);
                var replacement = RetimeJoiningSplits(route, elapsed, legacy, walk, rate, ratio, false, mapped, true, trafficOwned ? trafficRoutes[citizen.Identity.Id] : null,
                    out var map, out int completed, out int merges, shared);
                if (merges > 0 && !SameJoinedPose(citizen, replacement, elapsed,
                    map != null ? map.Map(elapsed) : CitySimulator.NormalizeJourneyElapsed(rawElapsed, replacement.Minutes, state.Minute)))
                {
                    var unjoined = RetimeSharing(route, elapsed, legacy, walk, rate, ratio, false, mapped, out var unjoinedMap, out int unjoinedCompleted, shared);
                    if (map != null && unjoinedMap != null && JoinKeepsMappedPose(citizen, replacement, map.Map(elapsed), unjoined, unjoinedMap.Map(elapsed), elapsed)) Joined++;
                    else
                    {
                        replacement = unjoined; map = unjoinedMap; completed = unjoinedCompleted;
                        JoinsRefused++;
                    }
                }
                else if (merges > 0) Joined++;
                if (replacement.Legs.Count > MaxLegs
                    && !MergeCompletedLegs(replacement, completed, elapsed, trafficOwned ? trafficRoutes[citizen.Identity.Id] : null))
                {
                    replacement = RetimeSharing(route, elapsed, legacy, walk, rate, ratio, true, mapped, out map, out _, shared);
                    Degraded++;
                    if (trafficOwned) { evicted.Add(citizen.Identity.Id); trafficOwned = false; }
                }
                else if (trafficOwned && TrafficCannotFollow(trafficRoutes[citizen.Identity.Id], map!, elapsed, ratio))
                {
                    Degraded++;
                    evicted.Add(citizen.Identity.Id); trafficOwned = false;
                }
                if (map != null) maps.Add(citizen.Identity.Id, map);
                ValidateCommittedRoute(replacement, origin.Position, destination.Position, citizen);
                double departure = citizen.DepartureMinute;
                if (evicted.Contains(citizen.Identity.Id))
                {
                    double resumed = map!.Map(elapsed);
                    departure = state.Minute - resumed;
                    if (!Position.Finite(departure) || departure < 0 || departure > state.Minute)
                        throw new ArgumentException("Invalid journey departure.", nameof(state));
                    ValidateSampledPose(citizen, replacement, elapsed, resumed);
                }
                else if (!trafficOwned)
                    ValidateSampledPose(citizen, replacement, elapsed,
                        CitySimulator.NormalizeJourneyElapsed(rawElapsed, replacement.Minutes, state.Minute));
                double arrival = departure + replacement.Minutes;
                if (arrival < state.Minute && state.Minute - arrival <= CitySimulator.JourneyClockTolerance(state.Minute, replacement.Minutes))
                    arrival = state.Minute;
                if (!Position.Finite(arrival) || arrival < state.Minute || arrival > MaxMinute)
                    throw new ArgumentException("Retimed arrival exceeds the supported clock.", nameof(state));
                if (replacement.Legs.Count > route.Legs.Count) Split++;
                PresentationGrid.Precompute(replacement.Commit());
                var change = new Change { Citizen = citizen, Route = replacement, EventMinute = arrival, Elapsed = elapsed,
                    DepartureMinute = departure == citizen.DepartureMinute ? (double?)null : departure };
                if (citizen.MealOuting != null && !citizen.MealOuting.Returning)
                {
                    double until = citizen.ActivityUntilMinute + arrival - citizen.NextEventMinute;
                    if (!Position.Finite(until) || until < arrival || until > MaxMinute)
                        throw new ArgumentException("Retimed meal stay exceeds the supported clock.");
                    change.MealStayUntil = until;
                }
                changes.Add(change);
                if (trafficOwned) trafficChanges.Add(citizen.Identity.Id, change);
            }

            private void CheckVehicle(TrafficVehicleState vehicle)
            {
                double expected = rescaledKernel!.GetPermission(vehicle.VehicleId).NominalMinutes;
                var change = trafficChanges[vehicle.VehicleId];
                ValidateSampledPose(change.Citizen, change.Route!, change.Elapsed, expected);
                double actual = State.Minute - citizens![vehicle.VehicleId].DepartureMinute;
                if (Math.Abs(expected - actual) > 1e-7)
                    throw new ArgumentException("Retimed traffic progress and committed journey disagree.");
                if (vehicle.Completed) return;
                foreach (var span in vehicle.Route)
                    if (span.EndNominalMinutes > vehicle.NominalMinutes)
                        TrafficNetwork.ValidateCommittedSpanTiming(span);
            }
        }

        private static void ValidateSampledPose(CitizenState citizen, Route replacement, double oldElapsed, double resumedElapsed)
        {
            if (!SamePose(citizen, replacement, oldElapsed, resumedElapsed))
                throw new ArgumentException("Retimed journey cannot preserve its measured physical pose.");
        }

        private static bool SameJoinedPose(CitizenState citizen, Route replacement, double oldElapsed, double resumedElapsed)
            => SamePose(citizen, replacement, oldElapsed, resumedElapsed)
                && Nullable.Equals(CarAt(citizen.Journey!, oldElapsed, citizen.Car?.ParkedPosition), CarAt(replacement, resumedElapsed, citizen.Car?.ParkedPosition));

        private static bool JoinKeepsMappedPose(CitizenState citizen, Route joined, double joinedElapsed, Route unjoined, double unjoinedElapsed, double oldElapsed)
        {
            if (!SamePose(citizen, joined, oldElapsed, joinedElapsed) || !SamePose(citizen, unjoined, oldElapsed, unjoinedElapsed)) return false;
            var parked = citizen.Car?.ParkedPosition;
            Position? old = CarAt(citizen.Journey!, oldElapsed, parked), join = CarAt(joined, joinedElapsed, parked), apart = CarAt(unjoined, unjoinedElapsed, parked);
            if (old == null || join == null || apart == null) return old == null && join == null && apart == null;
            return Position.Distance(old.Value, join.Value) <= Math.Max(Position.Distance(old.Value, apart.Value), UlpMetres(old.Value));
        }

        private static double UlpMetres(Position point)
            => 16 * 2.2204460492503131e-16 * Math.Max(60, Math.Max(Math.Abs(point.X), Math.Max(Math.Abs(point.Y), Math.Abs(point.Z))));

        private static Position? CarAt(Route route, double elapsed, Position? parked)
        {
            if (parked == null) return null;
            var position = parked.Value;
            elapsed = Math.Max(0, elapsed);
            foreach (var leg in route.Legs)
            {
                if (elapsed <= leg.Minutes) return leg.Mode == TravelMode.Car ? Position.Toward(leg.From, leg.To, elapsed / leg.Minutes) : position;
                if (leg.Mode == TravelMode.Car) position = leg.To;
                elapsed -= leg.Minutes;
            }
            return position;
        }

        private static bool SamePose(CitizenState citizen, Route replacement, double oldElapsed, double resumedElapsed)
        {
            var oldSample = CitySimulator.SampleRoute(citizen.Journey!, oldElapsed, citizen.Car?.ParkedPosition);
            var newSample = CitySimulator.SampleRoute(replacement, resumedElapsed, citizen.Car?.ParkedPosition);
            return oldSample.Position.Equals(newSample.Position) && Nullable.Equals(oldSample.CarPosition, newSample.CarPosition)
                && oldSample.Mode == newSample.Mode;
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
            => RetimeJoiningSplits(original, elapsed, legacy, walkSpeed, rate, ratio, keepCurrent, mapped, false, null, out map, out completed, out _);

        private static Route RetimeSharing(Route original, double elapsed, bool legacy, double walkSpeed, double rate, double ratio,
            bool keepCurrent, bool mapped, out NominalMap? map, out int completed, Dictionary<RouteLeg, RouteLeg>? shared)
            => RetimeJoiningSplits(original, elapsed, legacy, walkSpeed, rate, ratio, keepCurrent, mapped, false, null, out map, out completed, out _, shared);

        private static RouteLeg ScaledFuture(RouteLeg leg, Dictionary<RouteLeg, RouteLeg>? shared, bool legacy, double walkSpeed, double rate, double ratio)
        {
            bool reuse = shared != null && leg.Shared;
            if (reuse && shared!.TryGetValue(leg, out var known)) return known;
            double duration = Duration(leg, leg.From, leg.To, leg.Minutes, legacy, walkSpeed, rate, ratio);
            var copy = Copy(leg, leg.From, leg.To, duration, leg.ServiceId);
            if (reuse) { copy.Share(); shared!.Add(leg, copy); }
            return copy;
        }

        private static Route RetimeJoiningSplits(Route original, double elapsed, bool legacy, double walkSpeed, double rate, double ratio,
            bool keepCurrent, bool mapped, bool mergeFragments, TrafficVehicleState? traffic, out NominalMap? map, out int completed, out int merges,
            Dictionary<RouteLeg, RouteLeg>? shared = null)
        {
            merges = 0;
            int mergeCount = 0;
            var route = new Route(new RouteLegs(original.Legs.Count + 1));
            NominalMap? mapping = null;
            if (mapped) { mapping = new NominalMap(); mapping.Segments.Capacity = original.Legs.Count + 2; mapping.UnscaledCarPrefixes.Capacity = original.Legs.Count + 1; }
            map = mapping;
            completed = 0;
            double oldOffset = 0, newOffset = 0;
            void Add(RouteLeg leg, Position from, Position to, double minutes, string service, double oldMinutes, double? oldEnd = null, bool splitPrefix = false)
                => AddLeg(Copy(leg, from, to, minutes, service, splitPrefix), oldMinutes, oldEnd);
            void AddLeg(RouteLeg copy, double oldMinutes, double? oldEnd = null)
            {
                route.Legs.Add(copy);
                double minutes = copy.Minutes;
                double end = oldEnd ?? oldOffset + oldMinutes;
                mapping?.Segments.Add((oldOffset, end, newOffset, newOffset + minutes));
                oldOffset = end; newOffset += minutes;
            }
            void AddUnscaledCarPrefix(RouteLeg leg, Position from, Position to, double minutes, string service, double oldMinutes, double? oldEnd = null, bool splitPrefix = false)
            {
                double start = newOffset;
                Add(leg, from, to, minutes, service, oldMinutes, oldEnd, splitPrefix);
                if (mapping != null && leg.Mode == TravelMode.Car && Position.Distance(leg.From, leg.To) > 0)
                    mapping.UnscaledCarPrefixes.Add((start, newOffset));
            }
            int fragment = -1;
            double fragmentStart = 0, fragmentRemaining = 0;
            void AddCompleted(RouteLeg leg, Position from, Position to, double minutes, string service, double oldMinutes, double? oldEnd, bool splitPrefix, double remaining)
            {
                if (mergeFragments && fragment >= 0 && fragment == route.Legs.Count - 1)
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
                        mapping?.Segments.Add((oldOffset, segmentEnd, newOffset, joinedEnd));
                        if (mapping != null && leg.Mode == TravelMode.Car && Position.Distance(from, to) > 0) mapping.UnscaledCarPrefixes.Add((newOffset, joinedEnd));
                        route.Legs[fragment] = previous.CopyAs(previous.From, to, merged, service, splitPrefix);
                        oldOffset = segmentEnd; newOffset = joinedEnd;
                        mergeCount++;
                        return;
                    }
                }
                fragmentStart = newOffset; fragmentRemaining = remaining;
                AddUnscaledCarPrefix(leg, from, to, minutes, service, oldMinutes, oldEnd, splitPrefix);
                fragment = route.Legs.Count - 1;
            }
            if (elapsed == original.Minutes)
            {
                foreach (var leg in original.Legs)
                    AddUnscaledCarPrefix(leg, leg.From, leg.To, leg.Minutes, leg.ServiceId, leg.Minutes);
                completed = route.Legs.Count;
                merges = mergeCount;
                return route;
            }
            double remainingElapsed = elapsed;
            bool reachedPresent = false;
            var measuredSample = CitySimulator.SampleRoute(original, elapsed, null);
            for (int originalIndex = 0; originalIndex < original.Legs.Count; originalIndex++)
            {
                var leg = original.Legs[originalIndex];
                if (!Position.Finite(leg.Minutes) || leg.Minutes <= 0 || leg.Minutes > MaxLegMinutes)
                    throw new ArgumentException("Invalid input leg duration.");
                double oldLegEnd = oldOffset + leg.Minutes;
                if (!reachedPresent && elapsed >= oldLegEnd)
                {
                    AddCompleted(leg, leg.From, leg.To, leg.Minutes, leg.ServiceId, leg.Minutes, null, leg.SplitPrefix, remainingElapsed);
                    remainingElapsed -= leg.Minutes;
                    completed = route.Legs.Count;
                    continue;
                }
                if (!reachedPresent && elapsed > oldOffset)
                {
                    bool sampledStart = measuredSample.LegIndex == originalIndex && measuredSample.LegFraction == 0;
                    bool sampledEndpoint =
                        (measuredSample.LegIndex == originalIndex + 1 && measuredSample.LegFraction == 0)
                        || (originalIndex == original.Legs.Count - 1 && measuredSample.LegIndex == originalIndex
                            && measuredSample.LegFraction == 1);
                    if (sampledStart)
                    {
                        mapping?.Segments.Add((oldOffset, elapsed, newOffset, newOffset));
                        oldOffset = elapsed;
                        AddLeg(ScaledFuture(leg, shared, legacy, walkSpeed, rate, ratio), oldLegEnd - elapsed, oldLegEnd);
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
                        AddCompleted(leg, leg.From, point, remainingElapsed, splitService, remainingElapsed, elapsed, true, remainingElapsed);
                        completed = route.Legs.Count;
                        double oldTail = leg.Minutes - remainingElapsed;
                        if (sampledEndpoint || (nativeTail && point.Equals(leg.To)))
                        {
                            if (!sampledEndpoint && oldTail > CitySimulator.JourneyClockTolerance(elapsed, leg.Minutes))
                                throw new ArgumentException("A material native road tail lost its representable geometry.");
                            mapping?.Segments.Add((oldOffset, oldLegEnd, newOffset, newOffset));
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
                else AddLeg(ScaledFuture(leg, shared, legacy, walkSpeed, rate, ratio), leg.Minutes);
                reachedPresent = true;
            }
            merges = mergeCount;
            return route;
        }

        private const double FragmentDeviationMetres = 0.000001;

        private static bool ContinuesFragment(RouteLeg previous, TravelMode mode, Position from, Position to, string service)
            => previous.SplitPrefix && previous.Mode == mode
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
            bool nativeMovement = leg.Mode == TravelMode.Car && leg.ServiceId.StartsWith("native:road:", StringComparison.Ordinal)
                && distance > 0 && SplitService(leg).Length != 0;
            return distance <= 0.000001 && !nativeMovement;
        }

        private static double Duration(RouteLeg leg, Position from, Position to, double oldDuration, bool legacy, double walkSpeed, double rate, double ratio)
        {
            double distance = Position.Distance(from, to);
            double value = RetainsFixedMinutes(leg) ? oldDuration
                : !legacy ? oldDuration * ratio
                : leg.Mode == TravelMode.Walk ? distance / walkSpeed * rate
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
            if (tag.Kind == RoadTagKind.None || tag.TopologyLength == 0 || tag.From == tag.To) return "";
            return tag.Kind == RoadTagKind.Fragment1 ? service : FragmentService(service, tag.TopologyStart);
        }

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
