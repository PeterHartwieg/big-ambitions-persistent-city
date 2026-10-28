using System;
using System.Collections.Generic;
using System.Globalization;

namespace PersistentCity.Core
{
    /// <summary>Retimes persisted travel before constructing a simulator, after releasing all physical leases.</summary>
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

        /// <summary>
        /// Clock rate is game minutes per real second. Version zero used compressed moving routes;
        /// migrate their remaining geometry to native pace. Version one rescales only remaining movement
        /// when the clock rate changes; stationary waits retain game minutes. Completed time and
        /// the departure anchor remain untouched.
        /// The caller must release leases and call PrepareForSave first, then reconstruct its simulator
        /// after a true result. Invalid conversion does not mutate the supplied snapshot or its owner.
        /// A 512-leg route inside native car movement cannot add the provenance-preserving split and
        /// is rejected atomically, including before traffic admission. Other occupied partial movement
        /// is also rejected rather than changing its queue position or pace inconsistently.
        /// </summary>
        public static bool Apply(CityState state, double nativeWalkMetresPerSecond, double clockRate)
        {
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
            if (state.Traffic != null)
            {
                if (legacy || state.Traffic.Clock != state.Minute)
                    throw new ArgumentException("Traffic needs native timing at the city clock.");
                savedTraffic = TrafficKernel.Restore(state.Traffic, false);
            }
            var changes = new List<Change>();
            var maps = new Dictionary<string, NominalMap>(StringComparer.Ordinal);
            var trafficChanges = new Dictionary<string, Change>(StringComparer.Ordinal);
            var places = new Dictionary<string, Place>(StringComparer.Ordinal);
            foreach (var place in state.Places) places.Add(place.Id, place);
            foreach (var citizen in state.Citizens)
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
                    // Traffic's measured coordinate is the journey authority. Subtracting the
                    // persisted departure from a city clock can round on either side of it;
                    // splitting there would retain future old-pace geometry or amplify that gap.
                    if (Math.Abs(ownedTraffic.NominalMinutes - rawElapsed) > 1e-7)
                        throw new ArgumentException("Traffic and journey progress disagree.", nameof(state));
                    elapsed = ownedTraffic.NominalMinutes;
                    trafficOwned = true;
                }
                double arrivalDifference = Math.Abs(citizen.NextEventMinute - (citizen.DepartureMinute + route.Minutes));
                // Only an explicitly queued completed arrival may inherit subtraction/readdition
                // roundoff at a large city clock. Ordinary future events keep the strict contract.
                double arrivalTolerance = citizen.NextEventMinute == state.Minute && clockElapsed == route.Minutes
                    ? Math.Max(1e-7, CitySimulator.JourneyClockTolerance(state.Minute, route.Minutes)) : 1e-7;
                if (elapsed > route.Minutes || arrivalDifference > arrivalTolerance)
                    throw new ArgumentException("Journey clock does not match its route.", nameof(state));
                if (route.Legs.Count == MaxLegs && (legacy || ratio != 1) && IsPartialNativeRoadMovement(route, elapsed))
                    throw new ArgumentException("Cannot retime a maximum-size native car route without splitting its current edge.", nameof(state));
                var replacement = Retime(route, elapsed, legacy, nativeWalkMetresPerSecond, clockRate, ratio, out var map);
                maps.Add(citizen.Identity.Id, map);
                ValidateCommittedRoute(replacement, origin.Position, destination.Position, citizen);
                // Unowned trips resume from the unchanged departure clock, not the traffic
                // map's folded coordinate. Owned trips are checked against their actual detached
                // kernel permission below, after rescaling has produced that coordinate.
                if (!trafficOwned)
                    ValidateSampledPose(citizen, replacement, elapsed,
                        CitySimulator.NormalizeJourneyElapsed(rawElapsed, replacement.Minutes, state.Minute));
                double arrival = citizen.DepartureMinute + replacement.Minutes;
                if (arrival < state.Minute && state.Minute - arrival <= CitySimulator.JourneyClockTolerance(state.Minute, replacement.Minutes))
                    arrival = state.Minute;
                if (!Position.Finite(arrival) || arrival < state.Minute || arrival > MaxMinute)
                    throw new ArgumentException("Retimed arrival exceeds the supported clock.", nameof(state));
                var change = new Change { Citizen = citizen, Route = replacement, EventMinute = arrival, Elapsed = elapsed };
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
                var kernel = TrafficKernel.Restore(state.Traffic, false);
                var citizens = new Dictionary<string, CitizenState>(StringComparer.Ordinal);
                foreach (var citizen in state.Citizens) citizens.Add(citizen.Identity.Id, citizen);
                foreach (var vehicle in state.Traffic.Vehicles)
                {
                    if (!citizens.TryGetValue(vehicle.VehicleId, out var citizen) || citizen.Journey == null
                        || !maps.ContainsKey(vehicle.VehicleId)) throw new ArgumentException("Traffic owner has no committed journey.", nameof(state));
                    double elapsed = kernel.GetPermission(vehicle.VehicleId).NominalMinutes;
                    if (Math.Abs(elapsed - (state.Minute - citizen.DepartureMinute)) > 1e-7)
                        throw new ArgumentException("Traffic and journey progress disagree.", nameof(state));
                    if (vehicle.RouteIndex >= 0 && !vehicle.Completed)
                    {
                        double observed = kernel.GetPermission(vehicle.VehicleId).NominalMinutes;
                        double endpoint = vehicle.Route[vehicle.RouteIndex].EndNominalMinutes;
                        var mapping = maps[vehicle.VehicleId];
                        if (observed < endpoint && mapping.Map(endpoint) <= mapping.Map(observed))
                            throw new ArgumentException("Cannot collapse an unreported occupied traffic section endpoint during clock retiming.");
                    }
                    // The ordinary 512-leg fallback intentionally retains one partial leg's old pace.
                    // That would require a piecewise current-section motion model in the traffic kernel.
                    if (citizen.Journey.Legs.Count == MaxLegs && IsPartialMovement(citizen.Journey, elapsed)
                        && ratio != 1 && vehicle.RouteIndex >= 0 && !vehicle.Completed)
                        throw new ArgumentException("Cannot retime an occupied maximum-size route without splitting its current section.", nameof(state));
                    double observedNominal = kernel.GetPermission(vehicle.VehicleId).NominalMinutes;
                    var nominalMap = maps[vehicle.VehicleId];
                    if (ratio != 1 && !vehicle.Completed
                        && nominalMap.HasFutureUnscaledTrafficPrefix(nominalMap.Map(observedNominal), vehicle.Route))
                        throw new ArgumentException("Cannot retain an unscaled native prefix ahead of actual traffic progress during clock retiming.");
                }
                var rescaled = kernel.RescaleTime(ratio, (id, value) => maps.TryGetValue(id, out var mapping)
                    ? mapping.Map(value) : throw new ArgumentException("Missing traffic geometry map.", nameof(state)));
                traffic = rescaled.Capture();
                TrafficKernel.ValidateState(traffic);
                // RescaleTime keeps its movement affine; a collapsed native tail may remove
                // a few old nominal ulps instead. Reuse binding's exact duration gate on the
                // detached result before publishing any route or kernel changes. Waiting sources
                // have future spans too; completed historical timing remains exempt.
                foreach (var vehicle in traffic.Vehicles)
                {
                    // SaveCodec compares the actual detached snapshot coordinate with the raw
                    // departure subtraction. A bounded old lead can grow beyond that unchanged
                    // persistence contract when nominal coordinates are rescaled.
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
                    // Represented excursions retain their committed abstract game-minute quotes.
                    // They validate the same return geometry/access before completing; only the
                    // physical anchor follows native walking pace retiming.
                    if (meal.Slot > 0) continue;
                    if (meal.Status != MealStatus.Travelling && meal.Status != MealStatus.Serving) continue;
                    double back = meal.ReturnMinutes * ratio;
                    double due = meal.Status == MealStatus.Travelling && arrivals.TryGetValue(meal.ResidentId, out var arrival)
                        ? arrival : meal.DueAt;
                    if (!Position.Finite(back) || back < 0 || back > MaxLegMinutes || !Position.Finite(due)
                        || due < state.Minute || due > MaxMinute) throw new ArgumentException("Retimed meal exceeds the supported clock.");
                    meals.Add((meal, back, due));
                }
            }
            // Nothing above mutates the snapshot: any invalid route/rate leaves all citizens intact.
            foreach (var change in changes)
            {
                if (change.Route != null) change.Citizen.Journey = change.Route;
                change.Citizen.NextEventMinute = change.EventMinute;
                if (change.MealStayUntil.HasValue) change.Citizen.ActivityUntilMinute = change.MealStayUntil.Value;
            }
            foreach (var citizen in state.Citizens) citizen.CommutePreparation = new CommutePreparation();
            foreach (var meal in meals) { meal.Meal.ReturnMinutes = meal.ReturnMinutes; meal.Meal.DueAt = meal.DueAt; }
            state.TravelTimingVersion = 1;
            state.TravelClockRate = clockRate;
            if (traffic != null) state.Traffic = traffic;
            state.SnapshotSynchronizer = null;
            return true;
        }

        private static void ValidateSampledPose(CitizenState citizen, Route replacement, double oldElapsed, double resumedElapsed)
        {
            // Reuse the exact physical sampler, including its existing boundary normalization.
            // Compare the coordinates the resumed owner will actually use before publication.
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

        private static bool IsPartialMovement(Route route, double elapsed)
        {
            double offset = 0;
            foreach (var leg in route.Legs)
            {
                double end = offset + leg.Minutes;
                if (elapsed > offset && elapsed < end) return Position.Distance(leg.From, leg.To) > 0.000001;
                offset = end;
            }
            return false;
        }

        private static bool IsPartialNativeRoadMovement(Route route, double elapsed)
        {
            double offset = 0;
            foreach (RouteLeg leg in route.Legs)
            {
                double end = offset + leg.Minutes;
                if (elapsed > offset && elapsed < end)
                    return leg.Mode == TravelMode.Car && leg.ServiceId.StartsWith("native:road:", StringComparison.Ordinal)
                        && Position.Distance(leg.From, leg.To) > 0;
                offset = end;
            }
            return false;
        }

        private static Route Retime(Route original, double elapsed, bool legacy, double walkSpeed, double rate, double ratio, out NominalMap map)
        {
            var route = new Route();
            var mapping = new NominalMap(); map = mapping;
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
                // Captured traffic-span overlap proves native ownership later, including older
                // untagged fragments. Ordinary curb connectors outside those spans stay exempt.
                if (leg.Mode == TravelMode.Car && Position.Distance(leg.From, leg.To) > 0)
                    mapping.UnscaledCarPrefixes.Add((start, newOffset));
            }
            if (elapsed == original.Minutes)
            {
                foreach (var leg in original.Legs)
                    AddUnscaledCarPrefix(leg, leg.From, leg.To, leg.Minutes, leg.ServiceId, leg.Minutes);
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
                // Committed traffic spans use cumulative route boundaries. Repeated sampler
                // subtraction can leave a phantom residue after an exact such boundary.
                if (!reachedPresent && elapsed >= oldLegEnd)
                {
                    AddUnscaledCarPrefix(leg, leg.From, leg.To, leg.Minutes, leg.ServiceId, leg.Minutes);
                    remainingElapsed -= leg.Minutes;
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
                        // Folded offsets can put elapsed just beyond a boundary while the
                        // unchanged sampler still reports the next leg at its exact start.
                        // Anchor that old accounting residue to the measured start, without a
                        // zero/tiny phantom prefix; retain the whole future leg (including waits).
                        mapping.Segments.Add((oldOffset, elapsed, newOffset, newOffset));
                        oldOffset = elapsed;
                        double duration = Duration(leg, leg.From, leg.To, leg.Minutes, legacy, walkSpeed, rate, ratio);
                        Add(leg, leg.From, leg.To, duration, leg.ServiceId, oldLegEnd - elapsed, oldLegEnd);
                        reachedPresent = true;
                        continue;
                    }
                    if (remainingElapsed <= 0 || (remainingElapsed > leg.Minutes && !sampledEndpoint))
                        throw new ArgumentException("A partial route cannot preserve its measured timing interval.");
                    if (original.Legs.Count == MaxLegs || (sampledEndpoint && RetainsFixedMinutes(leg)))
                    {
                        // Preserve fixed wait minutes as one unchanged leg when the sampler
                        // already exposes its next mode. The existing 512-leg fallback likewise
                        // retains this one leg; later movement still receives native timing.
                        AddUnscaledCarPrefix(leg, leg.From, leg.To, leg.Minutes, leg.ServiceId, leg.Minutes);
                    }
                    else
                    {
                        string splitService = SplitService(leg);
                        bool nativeTail = leg.Mode == TravelMode.Car && Position.Distance(leg.From, leg.To) > 0
                            && splitService.StartsWith("native:road:fragment1:", StringComparison.Ordinal);
                        // Reuse the existing sampler's endpoint decision. A raw fractional
                        // point can remain representable after the measured body pose has
                        // already normalized to the next corner; keeping that tail moves it back.
                        var point = sampledEndpoint ? leg.To : Position.Lerp(leg.From, leg.To, remainingElapsed / leg.Minutes);
                        // Iterative subtraction preserves the sampler's exact split pose, but
                        // recombining its duration with preceding legs need not recover elapsed.
                        // Anchor the old domain at the authoritative coordinate; its new endpoint
                        // is the actual cumulative prefix duration, even when that differs by an ulp.
                        AddUnscaledCarPrefix(leg, leg.From, point, remainingElapsed, splitService, remainingElapsed, elapsed);
                        double oldTail = leg.Minutes - remainingElapsed;
                        if (sampledEndpoint || (nativeTail && point.Equals(leg.To)))
                        {
                            if (!sampledEndpoint && oldTail > CitySimulator.JourneyClockTolerance(elapsed, leg.Minutes))
                                throw new ArgumentException("A material native road tail lost its representable geometry.");
                            // The measured point already equals the endpoint, either in double
                            // precision or by the unchanged physical sampler's boundary rule.
                            // Keep its exact observed prefix and accounting coordinate. Collapse
                            // only this sampled-complete residual's map; no zero-length moving
                            // leg or extra old-time progress is submitted to the traffic kernel.
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
                // Mirror the native mapper's projection on a whole tagged edge. Distinct
                // components alone do not prove a distinct directed coordinate on a slope.
                double x = leg.To.X - leg.From.X, y = leg.To.Y - leg.From.Y, z = leg.To.Z - leg.From.Z;
                double square = x * x + y * y + z * z;
                double projected = ((point.X - leg.From.X) * x + (point.Y - leg.From.Y) * y + (point.Z - leg.From.Z) * z) / square;
                if (!Position.Finite(square) || square <= 1e-12 || !Position.Finite(projected) || projected >= 1) return false;
            }
            // A pre-existing fragment does not carry its original whole-edge origin. Refuse
            // ambiguous near-end residues rather than snapping the measured pose. Native edges
            // are at most 60 metres, so that lower scale also covers a missing origin near zero.
            // Sixteen double epsilons cover endpoint interpolation and dot-product rounding;
            // this is a refusal threshold, never a movement/progress allowance.
            double coordinate = Math.Max(60, Math.Max(Math.Max(Math.Abs(leg.From.X), Math.Abs(leg.To.X)),
                Math.Max(Math.Max(Math.Abs(leg.From.Y), Math.Abs(leg.To.Y)), Math.Max(Math.Abs(leg.From.Z), Math.Abs(leg.To.Z)))));
            double resolution = 16 * 2.2204460492503131e-16 * coordinate;
            return Position.Distance(point, leg.To) > resolution;
        }

        private static bool RetainsFixedMinutes(RouteLeg leg)
        {
            double distance = Position.Distance(leg.From, leg.To);
            bool nativeMovement = leg.Mode == TravelMode.Car && leg.ServiceId.StartsWith("native:road:", StringComparison.Ordinal)
                && distance > 0 && SplitService(leg).Length != 0;
            return distance <= 0.000001 && !nativeMovement;
        }

        private static double Duration(RouteLeg leg, Position from, Position to, double oldDuration, bool legacy, double walkSpeed, double rate, double ratio)
        {
            double distance = Position.Distance(from, to);
            // Preparation, parking and station waits are policy in game minutes. Clock
            // changes retime spatial movement, never the remaining portion of these waits.
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
            // Preserve the old migration behavior for unrecognized native-looking metadata;
            // malformed IDs never become trusted fragment provenance.
            return "";
        }

        private static RouteLeg Copy(RouteLeg source, Position from, Position to, double minutes, string service) => new RouteLeg
        { From = from, To = to, Minutes = minutes, Mode = source.Mode, ServiceId = service };

        private static void Rate(double value, string name)
        {
            if (!Position.Finite(value) || value < 0.01 || value > 1000) throw new ArgumentOutOfRangeException(name);
        }
    }
}
