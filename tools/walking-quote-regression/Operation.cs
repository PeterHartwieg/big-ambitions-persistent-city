using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;
using UnityEngine;
using UnityEngine.AI;

// One shared-quote request (IWalkingQuoteOperation) may answer a repeat of its own latest native
// calculation. Its first calculation and every public-access check stay live; nothing survives End.
internal static class Operation
{
    private static int checks;
    private static void Check(bool value, string name)
    { if (!value) throw new InvalidOperationException(name); checks++; }
    private static Position P(double x, double z = 0) => new Position(x, 0, z);
    private static Vector3 V(float x, float z = 0) => new Vector3(x, 0, z);
    private static string Geometry(Route? route) => route == null ? "null" : string.Join(";", route.Legs.Select(l =>
        l.From.X.ToString("R") + "," + l.From.Z.ToString("R") + ">" + l.To.X.ToString("R") + "," + l.To.Z.ToString("R") + "@" + l.Minutes.ToString("R")));
    private static Route Copy(Route route)
    {
        var copy = new Route();
        foreach (var leg in route.Legs) copy.Legs.Add(new RouteLeg { From = leg.From, To = leg.To, Minutes = leg.Minutes, Mode = leg.Mode });
        return copy;
    }

    public static void Run()
    {
        Requests();
        Integrated();
        Console.WriteLine("Walking quote operation: " + checks + " checks passed (actual Core FoodCommerce and Runtime WalkingRoutes; native calls replaced by controlled doubles).");
    }

    private static void Requests()
    {
        var from = new Place { Id = "from", Position = P(0) };
        var to = new Place { Id = "to", Position = P(20) };
        var other = new Place { Id = "other", Position = P(20, 1) };
        var preferences = new TravelPreferences { WalkingToleranceMetres = 100 };
        int startPaths = NavMesh.PathCalls;
        int accessReads = 0, closeAt = int.MaxValue; Action? onAccess = null;
        var planner = new WalkingRoutes(6, (_, __) => { bool open = ++accessReads < closeAt; onAccess?.Invoke(); return open; });
        var direct = new[] { V(0), V(10, 5), V(20) };
        var detour = new[] { V(0), V(5, 5), V(15, 5), V(20) };
        void Reset(Vector3[] corners)
        {
            NavMesh.Calculated = NavMesh.Sampled = true; NavMesh.Status = NavMeshPathStatus.PathComplete;
            NavMesh.SampleTransform = null; NavMesh.PathFor = null; NavMeshHelper.NpcAgentType = 0;
            NavMesh.Corners = corners; planner.Clear(); closeAt = int.MaxValue; onAccess = null; accessReads = 0;
        }
        // Core's order for one request: the shared quote's check, else a plan and the plan's check.
        Route? Request(Route? shared, bool scoped)
        {
            if (scoped) planner.BeginWalkingQuote();
            try
            {
                if (shared != null && planner.IsWalkingQuoteValid(from, to, shared, preferences)) return Copy(shared);
                var planned = planner.Plan(from, to, 500, preferences);
                return planned != null && planner.IsWalkingQuoteValid(from, to, planned, preferences) ? planned : null;
            }
            finally { if (scoped) planner.EndWalkingQuote(); }
        }
        (Route? Route, int Paths, int Samples, int Access, long Reuses) Measure(Func<Route?> request)
        {
            int paths = NavMesh.PathCalls, samples = NavMesh.SampleCalls; accessReads = 0;
            long reuses = planner.Metrics.NavMeshPathReuses;
            var route = request();
            return (route, NavMesh.PathCalls - paths, NavMesh.SampleCalls - samples, accessReads, planner.Metrics.NavMeshPathReuses - reuses);
        }

        Reset(direct); var coldPlain = Measure(() => Request(null, false));
        Reset(direct); var cold = Measure(() => Request(null, true));
        Check(coldPlain.Route != null && coldPlain.Paths == 2 && coldPlain.Samples == 4 && coldPlain.Access == 6, "Unscoped fresh plan validates with a second live query");
        Check(Geometry(cold.Route) == Geometry(coldPlain.Route) && cold.Paths == 1 && cold.Samples == 2 && cold.Reuses == 1,
            "Fresh plan's own calculation answers its immediate validation");
        Check(cold.Access == coldPlain.Access, "Planned and fresh legs still receive every public-access check");

        // A shared quote is checked live at the start of every request, including repeats in one frame.
        var shared = cold.Route!;
        for (int repeat = 0; repeat < 3; repeat++)
        {
            var warm = Measure(() => Request(shared, true));
            Check(Geometry(warm.Route) == Geometry(shared) && warm.Paths == 1 && warm.Reuses == 0 && warm.Access == 4,
                "Warm shared quote lost its live query or access checks");
        }

        Reset(direct); shared = Request(null, false)!; NavMesh.Corners = detour;
        long clears = planner.Metrics.WalkingCacheClears;
        var stalePlain = Measure(() => Request(shared, false));
        Check(stalePlain.Route!.Legs.Count == 3 && stalePlain.Paths == 3 && planner.Metrics.WalkingCacheClears == clears + 1,
            "Unscoped stale replacement recalculates three times");
        Reset(direct); shared = Request(null, false)!; NavMesh.Corners = detour;
        clears = planner.Metrics.WalkingCacheClears;
        var stale = Measure(() => Request(shared, true));
        Check(Geometry(stale.Route) == Geometry(stalePlain.Route) && stale.Paths == 1 && stale.Reuses == 2
            && planner.Metrics.WalkingCacheClears == clears + 1, "Stale replacement reuses the observation that refused the quote");
        Check(stale.Access == stalePlain.Access, "Stale replacement skipped an access check");

        // Navigation edits between requests in the same frame are seen by the next request.
        shared = stale.Route!; NavMesh.Corners = direct;
        var restored = Measure(() => Request(shared, true));
        Check(restored.Route!.Legs.Count == 2 && restored.Paths == 1 && restored.Reuses == 2, "Edit between requests was hidden");
        NavMesh.Status = NavMeshPathStatus.PathPartial;
        var broken = Measure(() => Request(restored.Route, true));
        Check(broken.Route == null && broken.Paths == 1 && broken.Reuses == 1, "Partial path between requests was hidden or recalculated");
        // The replacement plan stored that failure in the ordinary geometry cache, as a live query would;
        // it lasts until Clear, never because of the request.
        NavMesh.Status = NavMeshPathStatus.PathComplete;
        var negative = Measure(() => Request(null, true));
        Check(negative.Route == null && negative.Paths == 0 && negative.Reuses == 0, "Ordinary negative geometry entry changed");
        planner.Clear();
        var recovered = Measure(() => Request(null, true));
        Check(recovered.Route!.Legs.Count == 2 && recovered.Paths == 1 && recovered.Reuses == 1, "Cleared failure was not recalculated live");

        // Gates changing during a request: identical outcomes and reads, scoped or not, for every read position.
        foreach (bool stalePath in new[] { false, true })
            for (int close = 1; close <= 10; close++)
            {
                Reset(direct); var quote = stalePath ? Request(null, false) : null; if (stalePath) NavMesh.Corners = detour;
                closeAt = close; var plainRun = Measure(() => Request(quote, false));
                Reset(direct); quote = stalePath ? Request(null, false) : null; if (stalePath) NavMesh.Corners = detour;
                closeAt = close; var scopedRun = Measure(() => Request(quote, true));
                Check(Geometry(plainRun.Route) == Geometry(scopedRun.Route) && plainRun.Access == scopedRun.Access,
                    "Access closing at read " + close + (stalePath ? " during stale replacement" : "") + " changed the outcome");
            }
        // The fresh-leg checks after the reused calculation (reads 5 and 6) still refuse a closing gate.
        foreach (int close in new[] { 5, 6 })
        {
            Reset(direct); closeAt = close;
            var late = Measure(() => Request(null, true));
            Check(late.Route == null && late.Reuses == 1, "Access closing after the reused calculation was not checked");
        }

        // Documented contract: navigation must not change inside one request. If it did, the reused
        // calculation would not see it, whereas a second live query would refuse the plan.
        Reset(direct); onAccess = () => { if (accessReads == 2) NavMesh.Corners = detour; };
        var insidePlain = Measure(() => Request(null, false));
        Reset(direct); onAccess = () => { if (accessReads == 2) NavMesh.Corners = detour; };
        var insideScoped = Measure(() => Request(null, true));
        Check(insidePlain.Route == null && insideScoped.Route != null && insideScoped.Route.Legs.Count == 2,
            "Fixture no longer demonstrates the in-request navigation assumption");

        // Only a repeat of the latest calculation with the same endpoints and filter is reused.
        Reset(direct);
        planner.BeginWalkingQuote();
        try
        {
            var first = Measure(() => planner.Plan(from, to, 500, preferences));
            NavMesh.Corners = new[] { V(0), V(10, 5), V(20, 1) };
            var second = Measure(() => planner.Plan(from, other, 500, preferences));
            NavMesh.Corners = direct;
            var back = Measure(() => planner.IsWalkingQuoteValid(from, to, first.Route!, preferences) ? first.Route : null);
            Check(first.Paths == 1 && second.Paths == 1 && back.Route != null && back.Paths == 1 && back.Reuses == 0,
                "An older calculation or different endpoint was reused");
            planner.Clear();
            var cleared = Measure(() => planner.IsWalkingQuoteValid(from, to, first.Route!, preferences) ? first.Route : null);
            Check(cleared.Route != null && cleared.Paths == 1 && cleared.Reuses == 0, "Clear inside a request kept its observation");
            NavMeshHelper.NpcAgentType = 1;
            var agent = Measure(() => planner.IsWalkingQuoteValid(from, to, first.Route!, preferences) ? first.Route : null);
            Check(agent.Paths == 1 && agent.Reuses == 0, "Changed navigation filter reused a calculation");
            NavMeshHelper.NpcAgentType = 0;
            bool nested = false;
            try { planner.BeginWalkingQuote(); } catch (InvalidOperationException) { nested = true; }
            Check(nested, "Nested request was accepted");
        }
        finally { planner.EndWalkingQuote(); }
        // After End, standalone validity is live on every call again.
        var plan = planner.Plan(from, to, 500, preferences)!;
        for (int i = 0; i < 2; i++)
        {
            var alone = Measure(() => planner.IsWalkingQuoteValid(from, to, plan, preferences) ? plan : null);
            Check(alone.Route != null && alone.Paths == 1 && alone.Reuses == 0, "Standalone validity reused a calculation after End");
        }
        Check(planner.Metrics.NavMeshPathQueries == NavMesh.PathCalls - startPaths, "Path query metric counts only live native queries");
    }

    private sealed class Market : IFoodMarket
    {
        public int Units = 1000;
        public readonly List<string> Orders = new List<string>();
        public double EarliestMinute => 0;
        public IReadOnlyList<FoodBusiness> Read(double minute) => new[] { Shop("b", "shop"), Shop("c", "shop2") };
        private FoodBusiness Shop(string id, string place) => new FoodBusiness { Id = id, PlaceId = place, Name = id, MealsPerHour = 15,
            Hours = Enumerable.Range(0, 7).Select(d => new OpeningWindow { Day = d, FromMinute = 0, ToMinute = 1440 }).ToList(),
            Offers = new List<FoodOffer> { new FoodOffer { ProductId = "pastry", PriceCents = 100, Units = Units } } };
        public FoodSale Buy(ResidentMeal meal, double minute)
        {
            if (Units <= 0 || meal.PriceCents != 100) return new FoodSale(false, "Unavailable at checkout");
            Orders.Add(meal.IntentId + "@" + meal.BusinessId + "@" + minute.ToString("R")); Units--;
            return new FoodSale(true, "Native fake confirmed");
        }
    }
    // Forwards exactly as MultimodalRoutes does, without the road readiness gate.
    private class Adapter : IRoutePlanner, IWalkingQuoteValidity
    {
        public readonly WalkingRoutes Walking = new WalkingRoutes(84, (_, __) => true);
        public Route? Plan(Place from, Place to, double at, TravelPreferences preferences) => Walking.Plan(from, to, at, preferences);
        public bool IsWalkingQuoteValid(Place from, Place to, Route route, TravelPreferences preferences)
            => Walking.IsWalkingQuoteValid(from, to, route, preferences);
    }
    private sealed class ScopedAdapter : Adapter, IWalkingQuoteOperation
    {
        public void BeginWalkingQuote() => Walking.BeginWalkingQuote();
        public void EndWalkingQuote() => Walking.EndWalkingQuote();
    }

    // Actual FoodCommerce K=2 decisions and returns over three days, with corridor edits and sub-millimetre
    // native jitter between requests. Unscoped and scoped planners must produce identical saved cities.
    private static void Integrated()
    {
        (string State, string Orders, int Paths, long Reuses, long Checks, long Rejected) Day(bool scoped)
        {
            NavMesh.Calculated = NavMesh.Sampled = true; NavMesh.Status = NavMeshPathStatus.PathComplete;
            NavMesh.SampleTransform = null; NavMeshHelper.NpcAgentType = 0;
            float bend = 3, jitter = 0;
            NavMesh.PathFor = (a, b) => new[] { a, new Vector3((a.x + b.x) / 2, jitter, (a.z + b.z) / 2 + bend), b };
            var state = Population.Create(Enumerable.Range(0, 6).Select(n => new CitizenIdentity {
                Id = "r" + n, Name = "Resident " + n, Persona = Persona.Foodie, Age = 30 }).ToArray(), new[] {
                    new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 6 },
                    new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = 6, Position = new Position(40, 0, 0) },
                    new Place { Id = "shop", Name = "Bakery", Kind = PlaceKind.Shop, Position = new Position(60, 0, 30) },
                    new Place { Id = "shop2", Name = "Deli", Kind = PlaceKind.Shop, Position = new Position(20, 0, -35) } },
                "walking-operation", "0.40.4", 7, 1440);
            foreach (var c in state.Citizens)
            {
                c.Phase = CitizenPhase.AtPlace; c.PlaceId = "work"; c.Activity = Activity.Work;
                c.NextEventMinute = c.ActivityUntilMinute = 2870;
            }
            state.Food = new FoodCommerceState { RepresentationFactor = 2, MealRhythmVersion = MealRhythm.PolicyVersion, Shops = new List<FoodScope> {
                new FoodScope { PlaceId = "shop", BusinessId = "b", ProductId = "pastry" },
                new FoodScope { PlaceId = "shop2", BusinessId = "c", ProductId = "pastry" } } };
            var routes = scoped ? new ScopedAdapter() : new Adapter();
            var market = new Market();
            var city = new CitySimulator(state, routes); city.AttachFoodMarket(market); city.EnableHouseholdFinances();
            int paths = NavMesh.PathCalls;
            for (int step = 1; step <= 3 * 48; step++)
            {
                while (!city.AdvanceTo(1440 + step * 30, 7).CaughtUp) { }
                // Between requests: a real corridor change, its reversal, or a 0.5 mm vertical corner jitter
                // within both tolerances, so a warm quote stays valid and must keep its saved geometry.
                bend = step % 9 == 0 ? 6 : 3; jitter = step % 4 == 0 ? .0005f : 0;
            }
            NavMesh.PathFor = null;
            return (SaveCodec.Write(city.State), string.Join(",", market.Orders), NavMesh.PathCalls - paths,
                routes.Walking.Metrics.NavMeshPathReuses, routes.Walking.Metrics.WalkingQuoteChecks, routes.Walking.Metrics.WalkingQuoteRejected);
        }
        var plain = Day(false); var scoped = Day(true);
        Check(plain.Rejected > 0 && plain.Rejected < plain.Checks, "Fixture lacks both stale and warm valid quotes");
        Check(plain.Orders.Length > 0 && plain.Orders == scoped.Orders, "Scoped requests changed purchases");
        Check(plain.State == scoped.State, "Scoped requests changed the saved city");
        Check(plain.Reuses == 0 && scoped.Reuses > 0 && scoped.Paths < plain.Paths, "Integrated requests reused no calculation");
        Check(plain.Paths == scoped.Paths + scoped.Reuses && plain.Checks == scoped.Checks && plain.Rejected == scoped.Rejected,
            "A reuse did not replace exactly one live query, or Core's checks changed");
        Console.WriteLine("Integrated K=2 walking requests: native path queries " + plain.Paths + " unscoped, " + scoped.Paths
            + " scoped (" + scoped.Reuses + " reused); quote checks " + scoped.Checks + ", rejected " + scoped.Rejected + "; purchases identical.");
    }
}
