using System;
using System.Collections.Generic;
using System.Linq;

namespace PersistentCity.MovementEquivalence;

public sealed class Comparison
{
    public string Arm = "", Reference = "";
    public SortedDictionary<string, bool> Equal = new(StringComparer.Ordinal);
    public SortedDictionary<string, double> FirstDifferentMinute = new(StringComparer.Ordinal);
    public SortedDictionary<string, double> Delta = new(StringComparer.Ordinal);
    public bool Identical => Equal.Values.All(v => v);
}

public static class Equivalence
{
    public static readonly string[] Projections = { "save", "sales", "journeys", "parked", "wallets", "fifo", "visitors" };
    public static readonly string[] Outcomes = {
        "journeys.walk.meanMinutes", "journeys.walk.p95Minutes", "journeys.car.meanMinutes", "journeys.car.p95Minutes",
        "journeys.arrivals", "visits.shopArrivals", "visits.admittedAtShop", "visits.workArrivals", "routines.missedActivities",
        "meals.bought", "meals.ownSales", "meals.skipped", "revenue.ownUnits", "wallets.balanceUnits", "cars.parkedAway",
        "visitors.arrivals", "visitors.purchases", "visitors.ownSales", "visitors.walletUnits", "visitors.foodSpentUnits" };

    private static string Pick(Boundary b, string projection) => projection switch
    {
        "save" => b.SaveSha256, "sales" => b.SalesSha256, "journeys" => b.JourneysSha256,
        "parked" => b.ParkedSha256, "wallets" => b.WalletsSha256, "visitors" => b.VisitorsSha256, _ => b.FifoSha256
    };

    public static Comparison Compare(MovementResult arm, MovementResult reference)
    {
        if (arm.Boundaries.Count != reference.Boundaries.Count) throw new HarnessViolation("arms sampled different boundaries");
        var result = new Comparison { Arm = arm.Arm, Reference = reference.Arm };
        foreach (var projection in Projections)
        {
            result.Equal[projection] = true;
            for (int i = 0; i < arm.Boundaries.Count; i++)
            {
                if (arm.Boundaries[i].Minute != reference.Boundaries[i].Minute) throw new HarnessViolation("boundary minutes differ");
                if (Pick(arm.Boundaries[i], projection) == Pick(reference.Boundaries[i], projection)) continue;
                result.Equal[projection] = false; result.FirstDifferentMinute[projection] = arm.Boundaries[i].Minute;
                break;
            }
        }
        foreach (var key in Outcomes)
        {
            arm.Metrics.TryGetValue(key, out double a); reference.Metrics.TryGetValue(key, out double r);
            result.Delta[key] = a - r;
        }
        return result;
    }
}
