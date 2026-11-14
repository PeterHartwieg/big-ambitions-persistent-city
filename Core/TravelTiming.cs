using System;
using System.Collections.Generic;
using System.Globalization;

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
            Rate(nativeWalkMetresPerSecond, nameof(nativeWalkMetresPerSecond)); Rate(clockRate, nameof(clockRate));
            Rate(state.TravelClockRate, nameof(state.TravelClockRate));
            if (state.TravelTimingVersion != 0 && state.TravelTimingVersion != 1)
                throw new ArgumentException("Unsupported travel timing version.", nameof(state));
            if (!Position.Finite(state.Minute) || state.Minute < 0 || state.Minute > MaxMinute)
                throw new ArgumentException("Invalid city clock.", nameof(state));
            bool legacy = state.TravelTimingVersion == 0;
            if (!legacy && state.TravelClockRate == clockRate) return false;
            double ratio = clockRate / state.TravelClockRate;
            TrafficKernel? savedTraffic = null;
            var trafficRoutes = new Dictionary<string, TrafficVehicleState>(StringComparer.Ordinal);
            var evicted = new HashSet<string>(StringComparer.Ordinal);
            if (state.Traffic != null)
            {
                if (legacy || state.Traffic.Clock != state.Minute)
                    throw new ArgumentException("Traffic needs native timing at the city clock.");
                savedTraffic = TrafficKernel.Restore(state.Traffic);
                foreach (var vehicle in state.Traffic.Vehicles) trafficRoutes[vehicle.VehicleId] = vehicle;
            }
            var changes = new List<Change>();
            var maps = new Dictionary<string, NominalMap>(StringComparer.Ordinal);
            var trafficChanges = new Dictionary<string, Change>(StringComparer.Ordinal);
            var places = new Dictionary<string, Place>(StringComparer.Ordinal);
            foreach (var place in state.Places) places.Add(place.Id, place);
            foreach (var citizen in PersonTable.Schedules(state))
            {
                if (citizen == null || !Position.Finite(citizen.NextEventMinute)
                    || citizen.NextEventMinute < state.Minute || citizen.NextEventMinute > MaxMinute)
                    throw new ArgumentException("Invalid citizen event.", nameof(state));
                if (citizen.Phase != CitizenPhase.Travelling)
                {
                    if (ResidentLifecycle.IsSafelyHome(citizen))
                        changes.Add(new Change { Citizen = citizen, EventMinute = state.Minute });
                    continue;
                }
                var route = citizen.Journey;
                if (route == null || !places.TryGetValue(citizen.PlaceId, out var origin)
                    || !places.TryGetValue(citizen.DestinationId, out var destination))
                    throw new ArgumentException("Missing committed journey or place.", nameof(state));
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
                var replacement = Retime(route, elapsed, legacy, nativeWalkMetresPerSecond, clockRate, ratio, false, out var map, out int completed);
                if (replacement.Legs.Count > MaxLegs
                    && !MergeCompletedLegs(replacement, completed, elapsed, trafficOwned ? trafficRoutes[citizen.Identity.Id] : null))
                {
                    replacement = Retime(route, elapsed, legacy, nativeWalkMetresPerSecond, clockRate, ratio, true, out map, out _);
                    degraded++;
                    if (trafficOwned) { evicted.Add(citizen.Identity.Id); trafficOwned = false; }
                }
                else if (trafficOwned && TrafficCannotFollow(trafficRoutes[citizen.Identity.Id], map, elapsed, ratio))
                {
                    degraded++;
                    evicted.Add(citizen.Identity.Id); trafficOwned = false;
                }
                maps.Add(citizen.Identity.Id, map);
                ValidateCommittedRoute(replacement, origin.Position, destination.Position, citizen);
                double departure = citizen.DepartureMinute;
                if (evicted.Contains(citizen.Identity.Id))
                {
                    double resumed = map.Map(elapsed);
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
            TrafficKernelState? traffic = null;
            if (state.Traffic != null)
            {
                if (legacy || state.Traffic.Clock != state.Minute)
                    throw new ArgumentException("Traffic needs native timing at the city clock.", nameof(state));
                var kernel = TrafficKernel.Restore(state.Traffic);
                if (evicted.Count > 0) kernel = TrafficKernel.Restore(kernel.Capture(evicted.Contains));
                var citizens = new Dictionary<string, CitizenState>(StringComparer.Ordinal);
                foreach (var citizen in state.Citizens) citizens.Add(citizen.Identity.Id, citizen);
                foreach (var vehicle in state.Traffic.Vehicles)
                {
                    if (evicted.Contains(vehicle.VehicleId)) continue;
                    if (!citizens.TryGetValue(vehicle.VehicleId, out var citizen) || citizen.Journey == null
                        || !maps.ContainsKey(vehicle.VehicleId)) throw new ArgumentException("Traffic owner has no committed journey.", nameof(state));
                    double elapsed = kernel.GetPermission(vehicle.VehicleId).NominalMinutes;
                    if (Math.Abs(elapsed - (state.Minute - citizen.DepartureMinute)) > 1e-7)
                        throw new ArgumentException("Traffic and journey progress disagree.", nameof(state));
                }
                var rescaled = kernel.RescaleTime(ratio, (id, value) => maps.TryGetValue(id, out var mapping)
                    ? mapping.Map(value) : throw new ArgumentException("Missing traffic geometry map.", nameof(state)));
                traffic = rescaled.Capture();
                TrafficKernel.ValidateState(traffic);
                foreach (var vehicle in traffic.Vehicles)
                {
                    double expected = rescaled.GetPermission(vehicle.VehicleId).NominalMinutes;
                    var change = trafficChanges[vehicle.VehicleId];
                    ValidateSampledPose(change.Citizen, change.Route!, change.Elapsed, expected);
                    double actual = state.Minute - citizens[vehicle.VehicleId].DepartureMinute;
                    if (Math.Abs(expected - actual) > 1e-7)
                        throw new ArgumentException("Retimed traffic progress and committed journey disagree.");
                    if (vehicle.Completed) continue;
                    foreach (var span in vehicle.Route)
                        if (span.EndNominalMinutes > vehicle.NominalMinutes)
                            TrafficNetwork.ValidateCommittedSpanTiming(span);
                }
            }
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
            foreach (var change in changes)
            {
                if (change.Route != null) change.Citizen.Journey = change.Route.Commit();
                if (change.DepartureMinute.HasValue) change.Citizen.DepartureMinute = change.DepartureMinute.Value;
                change.Citizen.NextEventMinute = change.EventMinute;
                if (change.MealStayUntil.HasValue) change.Citizen.ActivityUntilMinute = change.MealStayUntil.Value;
            }
            foreach (var citizen in PersonTable.Schedules(state)) citizen.CommutePreparation = new CommutePreparation();
            foreach (var meal in meals) { meal.Meal.ReturnMinutes = meal.ReturnMinutes; meal.Meal.DueAt = meal.DueAt; }
            state.TravelTimingVersion = 1;
            state.TravelClockRate = clockRate;
            if (traffic != null) state.Traffic = traffic;
            state.SnapshotSynchronizer = null;
            return true;
        }

        private static void ValidateSampledPose(CitizenState citizen, Route replacement, double oldElapsed, double resumedElapsed)
        {
            var oldSample = CitySimulator.SampleRoute(citizen.Journey!, oldElapsed, citizen.Car?.ParkedPosition);
            var newSample = CitySimulator.SampleRoute(replacement, resumedElapsed, citizen.Car?.ParkedPosition);
            if (!oldSample.Position.Equals(newSample.Position) || !Nullable.Equals(oldSample.CarPosition, newSample.CarPosition)
                || oldSample.Mode != newSample.Mode)
                throw new ArgumentException("Retimed journey cannot preserve its measured physical pose.");
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

        private static double SegmentDistance(Position point, Position from, Position to)
        {
            double x = to.X - from.X, y = to.Y - from.Y, z = to.Z - from.Z;
            double square = x * x + y * y + z * z;
            double t = square > 0 ? ((point.X - from.X) * x + (point.Y - from.Y) * y + (point.Z - from.Z) * z) / square : 0;
            return Position.Distance(point, Position.Lerp(from, to, Math.Max(0, Math.Min(1, t))));
        }

        private static Route Retime(Route original, double elapsed, bool legacy, double walkSpeed, double rate, double ratio,
            bool keepCurrent, out NominalMap map, out int completed)
        {
            var route = new Route();
            var mapping = new NominalMap(); map = mapping;
            mapping.Segments.Capacity = original.Legs.Count + 2;
            completed = 0;
            double oldOffset = 0, newOffset = 0;
            void Add(RouteLeg leg, Position from, Position to, double minutes, string service, double oldMinutes, double? oldEnd = null)
            {
                route.Legs.Add(Copy(leg, from, to, minutes, service));
                double end = oldEnd ?? oldOffset + oldMinutes;
                mapping.Segments.Add((oldOffset, end, newOffset, newOffset + minutes));
                oldOffset = end; newOffset += minutes;
            }
            void AddUnscaledCarPrefix(RouteLeg leg, Position from, Position to, double minutes, string service, double oldMinutes, double? oldEnd = null)
            {
                double start = newOffset;
                Add(leg, from, to, minutes, service, oldMinutes, oldEnd);
                if (leg.Mode == TravelMode.Car && Position.Distance(leg.From, leg.To) > 0)
                    mapping.UnscaledCarPrefixes.Add((start, newOffset));
            }
            if (elapsed == original.Minutes)
            {
                foreach (var leg in original.Legs)
                    AddUnscaledCarPrefix(leg, leg.From, leg.To, leg.Minutes, leg.ServiceId, leg.Minutes);
                completed = route.Legs.Count;
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
                    AddUnscaledCarPrefix(leg, leg.From, leg.To, leg.Minutes, leg.ServiceId, leg.Minutes);
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
                        mapping.Segments.Add((oldOffset, elapsed, newOffset, newOffset));
                        oldOffset = elapsed;
                        double duration = Duration(leg, leg.From, leg.To, leg.Minutes, legacy, walkSpeed, rate, ratio);
                        Add(leg, leg.From, leg.To, duration, leg.ServiceId, oldLegEnd - elapsed, oldLegEnd);
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
                        AddUnscaledCarPrefix(leg, leg.From, point, remainingElapsed, splitService, remainingElapsed, elapsed);
                        completed = route.Legs.Count;
                        double oldTail = leg.Minutes - remainingElapsed;
                        if (sampledEndpoint || (nativeTail && point.Equals(leg.To)))
                        {
                            if (!sampledEndpoint && oldTail > CitySimulator.JourneyClockTolerance(elapsed, leg.Minutes))
                                throw new ArgumentException("A material native road tail lost its representable geometry.");
                            mapping.Segments.Add((oldOffset, oldLegEnd, newOffset, newOffset));
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
                else
                {
                    double duration = Duration(leg, leg.From, leg.To, leg.Minutes, legacy, walkSpeed, rate, ratio);
                    Add(leg, leg.From, leg.To, duration, leg.ServiceId, leg.Minutes);
                }
                reachedPresent = true;
            }
            return route;
        }

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
            if (leg.Mode != TravelMode.Car || !leg.ServiceId.StartsWith("native:road:", StringComparison.Ordinal)) return leg.ServiceId;
            string[] parts = leg.ServiceId.Split(':');
            if (parts.Length == 6 && (parts[2] == "v1" || parts[2] == "fragment1") && parts[3].Length > 0 &&
                int.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out int from) &&
                int.TryParse(parts[5], NumberStyles.None, CultureInfo.InvariantCulture, out int to) && from != to)
                return parts[2] == "fragment1" ? leg.ServiceId : "native:road:fragment1:" + parts[3] + ":" + parts[4] + ":" + parts[5];
            return "";
        }

        private static RouteLeg Copy(RouteLeg source, Position from, Position to, double minutes, string service)
            => source.CopyAs(from, to, minutes, service);

        private static void Rate(double value, string name)
        {
            if (!Position.Finite(value) || value < 0.01 || value > 1000) throw new ArgumentOutOfRangeException(name);
        }
    }
}
