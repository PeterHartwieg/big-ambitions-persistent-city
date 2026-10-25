using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Runtime;
using UnityEngine;

namespace FoodDemandAuditProbe
{
    public sealed partial class Probe
    {
        private object ReturnQuoteCheck(JObject request)
        {
            var save = SaveGameManager.Current;
            if (!Allowed() || save?.SaveGameName?.StartsWith("FT Food Routines", StringComparison.Ordinal) != true
                || (string?)request["saveName"] != save.SaveGameName || Time.timeScale != 0)
                throw new InvalidOperationException("Exact named isolated FT Food Routines city must be paused.");
            if (!(request["consumers"] is JArray requested) || requested.Count < 1 || requested.Count > 3
                || requested.Any(value => value.Type != JTokenType.String))
                throw new InvalidOperationException("Supply one to three explicit consumer IDs.");
            var ids = requested.Select(value => (string)value!).ToArray();
            if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
                throw new InvalidOperationException("Consumer IDs must be unique.");
            var sim = Simulation;
            var meals = ids.Select(id => sim.State.Food?.Residents.SingleOrDefault(m => m.ResidentId == id)
                ?? throw new InvalidOperationException("Existing consumer required: " + id)).ToArray();
            if (meals.Any(meal => meal.Slot == 0 || meal.Excursion == null))
                throw new InvalidOperationException("Each consumer must own an existing represented excursion.");
            var nativeRoutes = typeof(PopulationRuntime).GetField("routes", Fields)!.GetValue(Runtime)
                ?? throw new InvalidOperationException("Runtime routes unavailable.");
            if (nativeRoutes.GetType().GetProperty("ReadyForPopulation")?.GetValue(nativeRoutes) is not bool ready || !ready)
                throw new InvalidOperationException("Current native routes must be ready.");
            var livePlanner = nativeRoutes as IRoutePlanner ?? throw new InvalidOperationException("Walking planner contract unavailable.");
            var liveWalking = nativeRoutes.GetType().GetField("walking", Fields)?.GetValue(nativeRoutes)
                ?? throw new InvalidOperationException("Current walking planner unavailable.");
            var walkingType = liveWalking.GetType();
            double pace = (double)(walkingType.GetField("metresPerGameMinute", Fields)?.GetValue(liveWalking)
                ?? throw new InvalidOperationException("Current walking pace unavailable."));
            var access = walkingType.GetField("publicSegment", Fields)?.GetValue(liveWalking) as Func<Position, Position, bool>
                ?? throw new InvalidOperationException("Current public access validator unavailable.");
            var constructor = walkingType.GetConstructor(new[] { typeof(double), typeof(Func<Position, Position, bool>) })
                ?? throw new InvalidOperationException("Detached walking planner constructor changed.");
            var signature = typeof(FoodCommerce).GetMethod("GeometrySignature", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("Return geometry signature contract changed.");
            string? Signature(Route? route) => route == null ? null : (string)signature.Invoke(null, new object[] { route })!;
            // R strings preserve exact doubles even if diagnostic JSON rounds numeric fields.
            object? Geometry(Route? route) => route == null ? null : new { route,
                coordinates = route.Legs.Select(leg => new { from = Exact(leg.From), to = Exact(leg.To) }).ToArray(),
                signature = Signature(route) };
            double minute = sim.State.Minute;
            string before = JsonConvert.SerializeObject(meals);
            var reports = new List<object>();
            foreach (var meal in meals)
            {
                var excursion = meal.Excursion!;
                var person = sim.State.Citizens.Single(p => p.Identity.Id == meal.AnchorId);
                var origin = sim.State.Places.Single(p => p.Id == excursion.OriginId);
                var shop = sim.State.Places.Single(p => p.Id == excursion.DestinationId);
                var preferences = new TravelPreferences { AllowTransit = false, OwnsCar = false,
                    WalkingToleranceMetres = person.Preferences.WalkingToleranceMetres };
                var detached = constructor.Invoke(new object[] { pace, access });
                var freshPlanner = (IRoutePlanner)detached;
                // A detached instance alone receives validator-driven Clear(). Live Plan may
                // populate its ordinary geometry cache; no live validation/clear is invoked.
                var fresh = freshPlanner.Plan(shop, origin, minute, preferences);
                var live = livePlanner.Plan(shop, origin, minute, preferences);
                var validate = walkingType.GetMethod("IsWalkingQuoteValid")
                    ?? throw new InvalidOperationException("Walking validation contract changed.");
                bool Valid(Route? route) => route != null && (bool)validate.Invoke(detached,
                    new object[] { shop, origin, route, preferences })!;
                reports.Add(new { consumerId = meal.ResidentId, meal.AnchorId, meal.Status, minute,
                    expectedReturnGeometry = excursion.ReturnGeometry, freshGeometry = Signature(fresh), liveGeometry = Signature(live),
                    freshMatchesSaved = fresh != null && Signature(fresh) == excursion.ReturnGeometry,
                    liveMatchesSaved = live != null && Signature(live) == excursion.ReturnGeometry,
                    freshValidatorResult = Valid(fresh), liveValidatorResult = Valid(live),
                    fresh = Geometry(fresh), live = Geometry(live), pace, preferences,
                    savedOrigin = excursion.OriginPosition, currentOrigin = origin.Position,
                    savedDestination = excursion.DestinationPosition, currentDestination = shop.Position,
                    originDriftMetres = Position.Distance(origin.Position, excursion.OriginPosition),
                    destinationDriftMetres = Position.Distance(shop.Position, excursion.DestinationPosition),
                    excursion.ReturnStartsAt, excursion.ReturnsAt, excursion.Returning, excursion.Unresolved,
                    excursion.UnresolvedReason, meal.ReturnMinutes, meal.Deadline, excursion.SharedDepartureAt,
                    scheduledCompletion = excursion.ReturnStartsAt + meal.ReturnMinutes,
                    scheduledDeadlineSlack = meal.Deadline - excursion.ReturnStartsAt - meal.ReturnMinutes,
                    scheduledRoutineSlack = excursion.SharedDepartureAt - excursion.ReturnStartsAt - meal.ReturnMinutes,
                    completionIfStartedNow = minute + meal.ReturnMinutes,
                    deadlineSlackNow = meal.Deadline - minute - meal.ReturnMinutes });
            }
            if (!ReferenceEquals(save, SaveGameManager.Current) || sim.State.Minute != minute
                || JsonConvert.SerializeObject(meals) != before)
                throw new InvalidOperationException("Read-only return check observed changed city/consumer state.");
            return new { saveName = save.SaveGameName, minute, reports, modelUnchanged = true,
                note = "Bounded current native geometry evidence, not a replay of the failed return event. No excursion, wallet, receipt or scheduling changes. Native live Plan may populate its ordinary cache; validation uses a detached same-pace/current-access planner and cannot clear the live cache." };
        }
        private static string[] Exact(Position point) => new[] { point.X.ToString("R", CultureInfo.InvariantCulture),
            point.Y.ToString("R", CultureInfo.InvariantCulture), point.Z.ToString("R", CultureInfo.InvariantCulture) };
    }
}
