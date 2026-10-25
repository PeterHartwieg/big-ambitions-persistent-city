using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PersistentCity.Core
{
    /// <summary>A planner opts in only when it can validate cached geometry against current
    /// endpoints and navigation/public access. A timestamp alone is not validity evidence.</summary>
    public interface IWalkingQuoteValidity
    {
        bool IsWalkingQuoteValid(Place from, Place to, Route route, TravelPreferences preferences);
    }

    public static class FoodRepresentation
    {
        public static string ConsumerId(string anchorId, int slot) => slot == 0 ? anchorId
            : "consumer:" + Uri.EscapeDataString(anchorId) + ":" + slot.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Last eligible base stay, captured before personal food detours diverge.</summary>
    public sealed class FoodRoutineStop
    {
        public string AnchorId { get; set; } = "";
        public string PlaceId { get; set; } = "";
        public Activity Activity { get; set; }
        public double CapturedAt { get; set; }
        public double DepartureAt { get; set; }
    }

    /// <summary>One abstract walking meal visit. Never a citizen, body, car or traffic owner.</summary>
    public sealed class MealExcursion
    {
        public string OriginId { get; set; } = "";
        public string DestinationId { get; set; } = "";
        public Activity OriginActivity { get; set; }
        public Position OriginPosition { get; set; }
        public Position DestinationPosition { get; set; }
        public double DepartedAt { get; set; }
        public double ArrivesAt { get; set; }
        public double AdmissionAt { get; set; }
        public double SharedDepartureAt { get; set; }
        public double ReturnStartsAt { get; set; }
        public double ReturnsAt { get; set; }
        public int MaximumQueueMinutes { get; set; }
        public string ReturnGeometry { get; set; } = "";
        public bool Returning { get; set; }
        public bool Unresolved { get; set; }
        public string UnresolvedReason { get; set; } = "";
    }

    public sealed partial class FoodCommerce
    {
        private readonly Dictionary<string, FoodRoutineStop> routineStops = new Dictionary<string, FoodRoutineStop>(StringComparer.Ordinal);
        private readonly Dictionary<string, Route> walkingQuotes = new Dictionary<string, Route>(StringComparer.Ordinal);
        private readonly Queue<string> walkingQuoteOrder = new Queue<string>();
        private const int WalkingQuoteLimit = 512;
        private const int LocalFoodCandidates = 8;

        private FoodBusiness? ReadBusiness(string businessId, double now)
            => market is ILocalFoodMarket local ? local.ReadBusiness(businessId, now)
                : market.Read(now).SingleOrDefault(b => b.Id == businessId);

        private List<FoodBusiness> ReadLocalBusinesses(Place origin, double now, double tolerance)
        {
            var offers = market is ILocalFoodMarket local
                ? local.ReadLocal(origin, now, tolerance, LocalFoodCandidates) : market.Read(now);
            var eligible = offers.Where(b => state.Shops.Any(s => s.PlaceId == b.PlaceId && s.BusinessId == b.Id)).ToList();
            // Preserve the established small-market ordering, choices and route work.
            if (eligible.Count <= LocalFoodCandidates) return eligible;
            return eligible.Where(b => Position.Distance(origin.Position, city.FindPlace(b.PlaceId).Position) <= tolerance)
                .OrderBy(b => Position.Distance(origin.Position, city.FindPlace(b.PlaceId).Position))
                .ThenBy(b => b.Id, StringComparer.Ordinal).Take(LocalFoodCandidates).ToList();
        }

        private void InitializeRepresentation()
        {
            if (state.RepresentationFactor < 1 || state.RepresentationFactor > 3)
                throw new InvalidOperationException("Representation factor must be fixed between one and three at city initialization.");
            var identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (var person in city.State.Citizens)
                for (int slot = 0; slot < state.RepresentationFactor; slot++)
                    if (!identities.Add(FoodRepresentation.ConsumerId(person.Identity.Id, slot)))
                        throw new InvalidOperationException("Consumer identity collides with another stable anchor or slot.");
            foreach (var stop in state.RoutineStops) routineStops.Add(stop.AnchorId, stop);
            // A populated roster is persistent ownership, never permission to multiply wallets.
            if (records.Count != 0)
            {
                if (records.Count != city.State.Citizens.Count * state.RepresentationFactor)
                    throw new InvalidOperationException("Saved consumer roster does not match its fixed representation factor.");
                foreach (var person in city.State.Citizens)
                    for (int slot = 0; slot < state.RepresentationFactor; slot++)
                        if (!records.TryGetValue(FoodRepresentation.ConsumerId(person.Identity.Id, slot), out var meal)
                            || meal.AnchorId != person.Identity.Id || meal.Slot != slot)
                            throw new InvalidOperationException("Saved consumer identity does not match its anchor and slot.");
                return;
            }
            foreach (var person in city.State.Citizens)
            {
                CaptureRoutineStop(person);
                for (int slot = 0; slot < state.RepresentationFactor; slot++) Enrol(person, slot);
            }
        }

        private void CaptureRoutineStop(CitizenState person)
        {
            if (state.RepresentationFactor == 1 || person.MealOuting != null || person.Phase != CitizenPhase.AtPlace
                || person.Activity == Activity.Shop) return;
            double end = Math.Min(person.NextEventMinute, person.ActivityUntilMinute);
            if (end <= city.State.Minute) return;
            if (!routineStops.TryGetValue(person.Identity.Id, out var stop))
            {
                stop = new FoodRoutineStop { AnchorId = person.Identity.Id };
                routineStops.Add(stop.AnchorId, stop); state.RoutineStops.Add(stop);
            }
            if (stop.PlaceId == person.PlaceId && stop.DepartureAt == end && stop.Activity == person.Activity) return;
            stop.PlaceId = person.PlaceId; stop.Activity = person.Activity;
            stop.CapturedAt = city.State.Minute; stop.DepartureAt = end;
        }

        private bool SharedStopAvailable(CitizenState person, FoodRoutineStop stop)
        {
            if (city.State.Minute >= stop.DepartureAt) return false;
            if (person.MealOuting != null)
                return person.MealOuting.OriginId == stop.PlaceId && person.MealOuting.OriginActivity == stop.Activity;
            return person.Phase == CitizenPhase.AtPlace && person.PlaceId == stop.PlaceId && person.Activity == stop.Activity;
        }

        private void DecideRepresented(ResidentMeal meal, CitizenState person)
        {
            CaptureRoutineStop(person);
            if (meal.NeedsHomeResync && person.MealOuting == null && person.Phase == CitizenPhase.AtPlace
                && person.PlaceId == person.HomeId && person.Activity == Activity.Home)
                meal.NeedsHomeResync = false;
            if (!meal.NeedsHomeResync && routineStops.TryGetValue(person.Identity.Id, out var stop)
                && SharedStopAvailable(person, stop) && stop.DepartureAt - city.State.Minute >= 25)
            { Decide(meal, person); return; }
            meal.DueAt = Math.Min(meal.ExpiresAt, Math.Max(city.State.Minute + 1, person.NextEventMinute));
        }

        private int RepresentedBreak(CitizenState person, FoodRoutineStop stop, MealKind kind)
        {
            int available = FoodRoutinePlanning.BreakMinutes(person.Identity.Id, city.State.Seed, stop.Activity);
            if (kind == MealKind.Legacy || kind == MealKind.Lunch) return available;
            if (kind == MealKind.Breakfast) return Math.Min(45, available);
            return stop.Activity == Activity.Work || stop.Activity == Activity.Study ? available : 90;
        }

        private void ScheduleReturn(ResidentMeal meal)
        {
            var excursion = meal.Excursion!;
            // Checkout uncertainty does not erase movement ownership. Its wallet remains held
            // even if the independent walking return subsequently completes.
            double eating = meal.Status == MealStatus.Paid || meal.Status == MealStatus.Uncertain
                ? MealRhythm.EatingMinutes(meal.Kind) : 0;
            excursion.ReturnStartsAt = Math.Max(city.State.Minute, excursion.ArrivesAt) + eating;
            meal.DueAt = Math.Max(city.State.Minute + .001, excursion.ReturnStartsAt);
        }

        private bool StepExcursion(ResidentMeal meal, CitizenState person)
        {
            var excursion = meal.Excursion;
            if (meal.Slot == 0 || excursion == null || Active(meal)) return false;
            double now = city.State.Minute;
            if (excursion.Unresolved)
            {
                meal.DueAt = (Math.Floor(now / 1440) + 1) * 1440;
                return true;
            }
            if (!excursion.Returning)
            {
                if (now < excursion.ReturnStartsAt) { meal.DueAt = excursion.ReturnStartsAt; return true; }
                var origin = city.FindPlace(excursion.OriginId); var shop = city.FindPlace(excursion.DestinationId);
                var preferences = new TravelPreferences { AllowTransit = false, OwnsCar = false,
                    WalkingToleranceMetres = person.Preferences.WalkingToleranceMetres };
                // A changed endpoint must not teleport an away consumer to a replacement place.
                var back = Position.Distance(origin.Position, excursion.OriginPosition) > .01
                    || Position.Distance(shop.Position, excursion.DestinationPosition) > .01 ? null
                    : WalkingQuote(shop, origin, now, preferences);
                string geometry = back == null ? "" : GeometrySignature(back);
                double minutes = back == null || geometry == excursion.ReturnGeometry
                    ? meal.ReturnMinutes : back.Minutes;
                if (back == null || back.Legs.Any(l => l.Mode != TravelMode.Walk)
                    || now + minutes > meal.Deadline || now + minutes > excursion.SharedDepartureAt)
                {
                    excursion.Unresolved = true;
                    excursion.UnresolvedReason = "Return unavailable or exceeds the saved opportunity";
                    meal.DueAt = (Math.Floor(now / 1440) + 1) * 1440;
                    return true;
                }
                // Preserve saved timing for unchanged geometry across native pace changes.
                // A different validated path must fit using its own duration before commit.
                meal.ReturnMinutes = minutes; excursion.ReturnGeometry = geometry;
                excursion.Returning = true; excursion.ReturnsAt = now + minutes;
                meal.DueAt = Math.Max(now + .001, excursion.ReturnsAt);
                return true;
            }
            if (now < excursion.ReturnsAt) { meal.DueAt = excursion.ReturnsAt; return true; }
            // Only a completed valid return can release excursion ownership.
            CaptureRoutineStop(person);
            bool matching = routineStops.TryGetValue(person.Identity.Id, out var stop)
                && stop.PlaceId == excursion.OriginId && stop.Activity == excursion.OriginActivity
                && stop.DepartureAt == excursion.SharedDepartureAt && SharedStopAvailable(person, stop);
            meal.NeedsHomeResync = !matching;
            meal.Excursion = null;
            meal.DueAt = now + .001;
            return true;
        }

        private static Route CopyWalkingRoute(Route source)
        {
            var copy = new Route();
            foreach (var leg in source.Legs) copy.Legs.Add(new RouteLeg {
                From = leg.From, To = leg.To, Minutes = leg.Minutes, Mode = leg.Mode, ServiceId = leg.ServiceId });
            return copy;
        }

        private static string GeometrySignature(Route route)
        {
            var text = new StringBuilder();
            foreach (var leg in route.Legs)
                foreach (var point in new[] { leg.From, leg.To })
                    text.Append(point.X.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                        .Append(point.Y.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                        .Append(point.Z.ToString("R", CultureInfo.InvariantCulture)).Append(';');
            using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));
        }

        private Route? WalkingQuote(Place from, Place to, double at, TravelPreferences preferences, FoodDecisionTrace? trace = null)
        {
            // K=1 preserves the previous planner calls and decisions exactly.
            if (state.RepresentationFactor == 1)
            {
                if (trace != null) trace.RouteCalls++;
                return routes.Plan(from, to, at, preferences);
            }
            var validator = state.RepresentationFactor > 1 ? routes as IWalkingQuoteValidity : null;
            string key = from.Id + "\n" + to.Id;
            if (validator != null && walkingQuotes.TryGetValue(key, out var cached)
                && validator.IsWalkingQuoteValid(from, to, cached, preferences)) return CopyWalkingRoute(cached);
            if (trace != null) trace.RouteCalls++;
            var planned = routes.Plan(from, to, at, preferences);
            if (planned == null || planned.Legs.Count == 0 || planned.Legs.Count > 512
                || planned.Legs.Any(l => l.Mode != TravelMode.Walk || !l.From.IsFinite || !l.To.IsFinite
                    || !Position.Finite(l.Minutes) || l.Minutes < 0)
                || !Position.Finite(planned.Minutes) || planned.Minutes <= 0 || planned.Minutes > 1440
                || Position.Distance(planned.Legs[0].From, from.Position) > .01
                || Position.Distance(planned.Legs[planned.Legs.Count - 1].To, to.Position) > .01) return null;
            for (int i = 1; i < planned.Legs.Count; i++)
                if (Position.Distance(planned.Legs[i - 1].To, planned.Legs[i].From) > .01) return null;
            if (validator != null)
            {
                if (!validator.IsWalkingQuoteValid(from, to, planned, preferences)) return null;
                if (!walkingQuotes.ContainsKey(key))
                {
                    if (walkingQuotes.Count >= WalkingQuoteLimit) walkingQuotes.Remove(walkingQuoteOrder.Dequeue());
                    walkingQuoteOrder.Enqueue(key);
                }
                walkingQuotes[key] = CopyWalkingRoute(planned);
            }
            return planned;
        }
    }
}
