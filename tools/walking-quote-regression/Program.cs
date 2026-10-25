using System;
using PersistentCity.Core;
using PersistentCity.Runtime;
using UnityEngine;
using UnityEngine.AI;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string name)
    { if (!value) throw new InvalidOperationException(name); checks++; }
    private static Position P(double x, double z = 0) => new Position(x, 0, z);
    private static Vector3 V(float x, float z = 0) => new Vector3(x, 0, z);
    private static Route Copy(Route route)
    {
        var copy = new Route();
        foreach (var leg in route.Legs) copy.Legs.Add(new RouteLeg { From = leg.From, To = leg.To, Minutes = leg.Minutes, Mode = leg.Mode });
        return copy;
    }
    private static void Main()
    {
        var from = new Place { Id = "from", Position = P(0) };
        var to = new Place { Id = "to", Position = P(20) };
        var preferences = new TravelPreferences { WalkingToleranceMetres = 100 };
        bool publicAccess = true;
        var planner = new WalkingRoutes(6, (_, __) => publicAccess);
        NavMesh.Corners = new[] { V(0), V(10, 5), V(20) };
        Route Original()
        {
            NavMesh.Calculated = NavMesh.Sampled = true; NavMesh.Status = NavMeshPathStatus.PathComplete;
            NavMesh.SampleTransform = null; publicAccess = true;
            NavMesh.Corners = new[] { V(0), V(10, 5), V(20) }; planner.Clear();
            return planner.Plan(from, to, 500, preferences)!;
        }
        var quote = Original();
        Check(quote != null && quote.Legs.Count == 2, "Native complete corner path planned");
        Check(NavMesh.Raycast(V(0), V(10, 5), out _, NavMeshHelper.NpcNavMeshFilter), "Fixture models corner segment raycast refusal");
        int paths = NavMesh.PathCalls, samples = NavMesh.SampleCalls, rays = NavMesh.RaycastCalls;
        Check(planner.IsWalkingQuoteValid(from, to, quote!, preferences), "Complete authoritative corner path must validate despite segment raycast refusal");
        Check(NavMesh.PathCalls == paths + 1 && NavMesh.SampleCalls == samples + 2, "Validation replans whole path using original endpoints only");
        Check(NavMesh.RaycastCalls == rays && NavMesh.LastMask == 7, "Validation retains dry filter and avoids independent corner raycasts");
        paths = NavMesh.PathCalls;
        Check(planner.IsWalkingQuoteValid(from, to, quote!, preferences) && NavMesh.PathCalls == paths + 1, "Reuse performs live query every time");
        Check(planner.Metrics.NavMeshPathQueries == NavMesh.PathCalls, "Path metrics include validation queries");
        Check(planner.Metrics.WalkingCalculations == 1 && planner.Metrics.WalkingQuoteChecks == 2, "Planner calculations and quote checks are separate counters");
        publicAccess = false;
        long clears = planner.Metrics.WalkingCacheClears;
        Check(!planner.IsWalkingQuoteValid(from, to, quote!, preferences) && planner.Metrics.WalkingCacheClears > clears, "Live closed public gate refuses and clears geometry");
        Check(planner.Plan(from, to, 501, preferences) == null, "Closed gate cannot be bypassed by fresh plan");
        quote = Original(); NavMesh.Status = NavMeshPathStatus.PathPartial;
        Check(!planner.IsWalkingQuoteValid(from, to, quote, preferences), "Partial current path refuses cached complete quote");
        Check(planner.Plan(from, to, 501, preferences) == null, "Partial current path cannot survive in walking cache");
        quote = Original(); NavMesh.Calculated = false;
        Check(!planner.IsWalkingQuoteValid(from, to, quote, preferences), "Failed native query refuses quote");
        quote = Original(); NavMesh.Corners = new[] { V(0), V(5, 5), V(15, 5), V(20) };
        Check(!planner.IsWalkingQuoteValid(from, to, quote, preferences), "Changed corridor refuses former geometry");
        var detour = planner.Plan(from, to, 501, preferences)!;
        Check(detour.Legs.Count == 3 && planner.IsWalkingQuoteValid(from, to, detour, preferences), "After invalidation new current geometry can be planned and validated");
        quote = Original(); NavMesh.Corners = new[] { V(0), V(10, -5), V(20) };
        Check(!planner.IsWalkingQuoteValid(from, to, quote, preferences), "Same length and timing with different corridor still refuses");
        quote = Original(); var wrong = Copy(quote); wrong.Legs[0].Minutes += 1;
        Check(!planner.IsWalkingQuoteValid(from, to, wrong, preferences), "Tampered travel time refuses");
        wrong = Copy(quote); wrong.Legs[1].From = P(11, 5);
        Check(!planner.IsWalkingQuoteValid(from, to, wrong, preferences), "Disconnected quote refuses");
        wrong = Copy(quote); wrong.Legs[0].Mode = TravelMode.Car;
        Check(!planner.IsWalkingQuoteValid(from, to, wrong, preferences), "Nonwalking quote refuses");
        Check(!planner.IsWalkingQuoteValid(from, new Place { Position = P(21) }, quote, preferences), "Wrong destination refuses");
        Check(!planner.IsWalkingQuoteValid(from, to, quote, new TravelPreferences { WalkingToleranceMetres = 21 }), "Complete route still respects total walk tolerance");
        quote = Original(); NavMesh.Sampled = false;
        Check(!planner.IsWalkingQuoteValid(from, to, quote, preferences), "Missing endpoint navmesh refuses");
        quote = Original(); NavMesh.SampleTransform = p => new Vector3(p.x, p.y + .051f, p.z);
        Check(!planner.IsWalkingQuoteValid(from, to, quote, preferences), "Endpoint outside5cm bound refuses");
        quote = Original(); NavMesh.Corners = new[] { V(.1f), V(10, 5), V(20) };
        Check(!planner.IsWalkingQuoteValid(from, to, quote, preferences), "Complete native path with wrong first endpoint refuses");
        quote = Original(); NavMesh.Corners = new Vector3[514];
        Check(!planner.IsWalkingQuoteValid(from, to, quote, preferences), "Native path remains bounded to512legs");
        quote = Original(); int accessReads = 0;
        var switchingGate = new WalkingRoutes(6, (_, __) => ++accessReads <= 2);
        Check(!switchingGate.IsWalkingQuoteValid(from, to, quote, preferences), "Fresh geometry public access is checked again after native query");
        NavMesh.Corners = new[] { V(0), V(0) }; planner.Clear();
        var stay = planner.Plan(from, from, 502, preferences)!;
        Check(planner.IsWalkingQuoteValid(from, from, stay, preferences), "Same endpoint stationary walk retains bounded native sample validation");
        Check(planner.Metrics.WalkingQuoteRejected > 0 && planner.Metrics.WalkingQuoteCheckMilliseconds >= 0, "Quote refusal and duration metrics retained");
        Console.WriteLine("Walking quote regression: " + checks + " checks passed (actual Runtime source; native calls replaced by controlled doubles).");
    }
}
