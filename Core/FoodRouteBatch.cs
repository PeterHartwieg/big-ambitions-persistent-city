using System;
using System.Collections.Generic;
using System.Linq;

namespace PersistentCity.Core
{
    /// <summary>
    /// Bounded read-only quote work. Geometric distance only selects candidates; each direction
    /// still needs a route. A missing return route never becomes a one-way lunch outing.
    /// Caller owns navigation snapshot freshness and calls Step on its permitted thread.
    /// </summary>
    public sealed class FoodRouteBatch
    {
        private sealed class Request
        {
            public FoodIntent Intent = null!;
            public FoodBusiness Business = null!;
            public Place Origin = null!;
            public Place Shop = null!;
            public TravelPreferences Preferences = null!;
        }
        private readonly Queue<Request> pending = new Queue<Request>();
        private readonly IRoutePlanner routes;
        private readonly string evidence;
        private Request? active;
        private double? outbound;
        private readonly List<FoodTravelQuote> quotes = new List<FoodTravelQuote>();
        public IReadOnlyList<FoodTravelQuote> Quotes => quotes;
        public int RouteCalls { get; private set; }
        public int CandidateCount { get; private set; }
        public bool Complete => active == null && pending.Count == 0;

        public FoodRouteBatch(FoodRoutineCapture capture, FoodRoutinePlan plan, IReadOnlyList<FoodBusiness> businesses,
            IReadOnlyList<Place> places, IRoutePlanner routes, string evidence, int maximumCandidates = 3)
        {
            if (capture == null || plan == null || businesses == null || places == null || routes == null
                || string.IsNullOrWhiteSpace(evidence) || evidence.Length > 700 || maximumCandidates < 1 || maximumCandidates > 8
                || businesses.Count > 8 || plan.Opportunities.Count > 35000)
                throw new ArgumentException("Use a bounded observed market and explicit route evidence.");
            this.routes = routes; this.evidence = evidence;
            var byPlace = places.ToDictionary(p => p.Id, StringComparer.Ordinal);
            var residents = capture.Residents.ToDictionary(p => p.Identity.Id, StringComparer.Ordinal);
            foreach (var intent in plan.Opportunities.OrderBy(i => i.RequestedAt).ThenBy(i => i.Id, StringComparer.Ordinal))
            {
                if (intent.OriginPlaceId.Length == 0 || !byPlace.TryGetValue(intent.OriginPlaceId, out var origin)) continue;
                var resident = residents[intent.ConsumerId];
                foreach (var business in businesses.Where(b => byPlace.ContainsKey(b.PlaceId))
                    .OrderBy(b => Position.Distance(origin.Position, byPlace[b.PlaceId].Position)).ThenBy(b => b.Id, StringComparer.Ordinal).Take(maximumCandidates))
                {
                    // Copy mutable input fields; later population changes cannot silently retarget a quote.
                    pending.Enqueue(new Request
                    {
                        Intent = new FoodIntent { Id = intent.Id, ConsumerId = intent.ConsumerId, OriginPlaceId = intent.OriginPlaceId,
                            RequestedAt = intent.RequestedAt, EatingMinutes = intent.EatingMinutes },
                        Business = new FoodBusiness { Id = business.Id, MealsPerHour = business.MealsPerHour },
                        Origin = Copy(origin), Shop = Copy(byPlace[business.PlaceId]),
                        Preferences = new TravelPreferences { OwnsCar = false, AllowTransit = false,
                            WalkingToleranceMetres = resident.WalkingToleranceMetres }
                    });
                    CandidateCount++;
                }
            }
        }

        public bool Step(int maximumRouteCalls = 2)
        {
            if (maximumRouteCalls < 1 || maximumRouteCalls > 128) throw new ArgumentOutOfRangeException(nameof(maximumRouteCalls));
            for (int call = 0; call < maximumRouteCalls; call++)
            {
                if (active == null)
                {
                    if (pending.Count == 0) return true;
                    active = pending.Dequeue();
                    // A complete walk cannot be shorter than this lower bound. This is
                    // a refusal by the resident's walking policy, never a fabricated route.
                    if (Position.Distance(active.Origin.Position, active.Shop.Position) > active.Preferences.WalkingToleranceMetres)
                    { outbound = null; Finish(null); continue; }
                    outbound = Quote(routes.Plan(active.Origin, active.Shop, active.Intent.RequestedAt, active.Preferences), active.Origin, active.Shop);
                    RouteCalls++;
                    if (!outbound.HasValue) Finish(null);
                }
                else
                {
                    double service = active.Business.MealsPerHour > 0 ? 60.0 / active.Business.MealsPerHour.Value : 0;
                    double returnAt = active.Intent.RequestedAt + outbound!.Value + service + active.Intent.EatingMinutes;
                    var back = Quote(routes.Plan(active.Shop, active.Origin, returnAt, active.Preferences), active.Shop, active.Origin);
                    RouteCalls++;
                    Finish(back);
                }
            }
            return Complete;
        }

        private void Finish(double? back)
        {
            var request = active!;
            quotes.Add(new FoodTravelQuote { ConsumerId = request.Intent.ConsumerId, IntentId = request.Intent.Id,
                OriginPlaceId = request.Intent.OriginPlaceId, BusinessId = request.Business.Id,
                Minutes = outbound, ReturnMinutes = back,
                Evidence = evidence + "; separate walking out/return estimates from observed origin; not executed journeys" });
            active = null; outbound = null;
        }

        private static double? Quote(Route? route, Place from, Place to)
        {
            if (route == null) return null;
            if (route.Legs.Count == 0 || route.Legs.Count > 512 || route.Legs.Any(l => l.Mode != TravelMode.Walk || !l.From.IsFinite || !l.To.IsFinite
                || !Position.Finite(l.Minutes) || l.Minutes < 0) || !Position.Finite(route.EstimatedMinutes)
                || route.EstimatedMinutes < 0)
                throw new ArgumentException("Food outing requires a finite complete walking route.");
            if (Position.Distance(route.Legs[0].From, from.Position) > .01 || Position.Distance(route.Legs[route.Legs.Count - 1].To, to.Position) > .01)
                throw new ArgumentException("Food route does not reach the requested endpoints.");
            for (int i = 1; i < route.Legs.Count; i++)
                if (Position.Distance(route.Legs[i - 1].To, route.Legs[i].From) > .01)
                    throw new ArgumentException("Food route is not contiguous.");
            // A valid native route can exceed a whole game day under the active clock pace.
            // It is outside this food slice's horizon, not corrupt navigation or a batch failure.
            if (route.EstimatedMinutes > 1440) return null;
            return route.EstimatedMinutes;
        }

        private static Place Copy(Place source) => new Place { Id = source.Id, Position = source.Position, Kind = source.Kind };
    }
}
