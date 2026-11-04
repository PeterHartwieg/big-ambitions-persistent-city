using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;

namespace PersistentCity.EconomyInterventions;

/// <summary>Seed-band summary, today's unlimited rival curve and tool cost. Section 3.5 metric
/// definitions; thresholds are F6h work and are not set here.</summary>
public static class Baseline
{
    private const int Weeks = EconomyRun.Days / 7;

    private static double Mean(IEnumerable<double> values) { var v = values.ToArray(); return v.Length == 0 ? double.NaN : v.Average(); }
    private static double Sd(IEnumerable<double> values)
    {
        var v = values.ToArray(); if (v.Length < 2) return 0;
        double m = v.Average(); return Math.Sqrt(v.Sum(x => (x - m) * (x - m)) / (v.Length - 1));
    }
    private static double Trailing(List<DayMetrics> days, int day, Func<DayMetrics, long> metric)
        => Mean(days.Where(d => d.Day > day - 3 && d.Day <= day).Select(d => (double)metric(d)));
    private static long[] Weekly(List<DayMetrics> days, Func<DayMetrics, long> metric, bool endOfWeek = false)
        => Enumerable.Range(0, Weeks).Select(w => endOfWeek ? metric(days.Single(d => d.Day == w * 7 + 7))
            : days.Where(d => (d.Day - 1) / 7 == w).Sum(metric)).ToArray();

    private static readonly (string Name, Func<DayMetrics, long> Metric, bool EndOfWeek)[] WeeklyMetrics = {
        ("ownPurchases", d => d.OwnPurchases, false), ("ownRevenueUnits", d => d.OwnRevenueUnits, false),
        ("burgerPurchases", d => d.ProductPurchases(FoodProducts.Burger), false),
        ("rivalMeals", d => d.RivalMeals, false), ("homeMeals", d => d.HomeMeals, false), ("skipped", d => d.Skipped, false),
        ("resolvedMeals", d => d.Resolved, false),
        ("noStaffedCounterOffers", d => d.Shops.Values.Sum(s => s.Staff), false), ("stockOffers", d => d.Shops.Values.Sum(s => s.Stock), false),
        ("closedLateOffers", d => d.Shops.Values.Sum(s => s.Closed), false), ("serviceQueueOffers", d => d.Shops.Values.Sum(s => s.Service), false),
        ("priceOffers", d => d.Shops.Values.Sum(s => s.Price), false), ("travelTimeOffers", d => d.Shops.Values.Sum(s => s.Access), false),
        ("incomeReceivedUnits", d => d.IncomeReceivedUnits, false), ("unfedDays", d => d.UnfedDays, false),
        ("missedActivities", d => d.MissedActivities, false), ("jobLosses", d => d.JobLosses, false), ("jobFinds", d => d.JobFinds, false),
        ("arrearsOwnersEndOfWeek", d => d.ArrearsOwners, true), ("medianBalanceUnitsEndOfWeek", d => d.MedianBalanceUnits, true),
        ("p10BalanceUnitsEndOfWeek", d => d.P10BalanceUnits, true), ("employedEndOfWeek", d => d.Employed, true),
    };

    public static string Summary(IReadOnlyList<Arm> arms, Dictionary<string, List<RunResult>> results, int residents, int seeds)
    {
        var control = results["control/control"];
        int reverted = InterventionArms.FirstRevertedDay;
        // Control seed band per day of the 3-day trailing mean own purchases (mean +/- 2 sd over seeds).
        var band = Enumerable.Range(1, EconomyRun.Days).ToDictionary(day => day, day =>
        {
            var values = control.Select(r => Trailing(r.Days, day, d => d.OwnPurchases)).ToArray();
            return (Low: Mean(values) - 2 * Sd(values), High: Mean(values) + 2 * Sd(values));
        });
        double InterventionMean(RunResult r) => r.Days.Where(d => DayMetrics.Phase(d.Day) == "intervention").Average(d => (double)d.OwnPurchases);
        var controlIntervention = control.Select(InterventionMean).ToArray();
        var json = new Json();
        json.Open().Field("schema", EconomyRun.Schema + "-summary").Field("residents", residents)
            .Field("consumers", residents * EconomyFixture.RepresentationFactor).Field("seeds", seeds).Field("unitsPerCent", Money.UnitsPerCent)
            .Open("protocol").Field("baselineDays", EconomyRun.BaselineDays).Field("interventionDays", EconomyRun.InterventionDays)
            .Field("revertedDays", EconomyRun.RevertedDays).Field("outcomeSampleMinute", EconomyRun.OutcomeSampleMinute)
            .Field("controlBand", "per-day mean +/- 2 sd over seeds of the 3-day trailing mean own purchases").Close()
            .Field("evidence", "Offline detached Core scenario; not native or rig evidence. Thresholds are F6h work.");
        json.OpenArray("arms");
        foreach (var arm in arms)
        {
            var runs = results[arm.Family + "/" + arm.Name];
            json.Open().Field("family", arm.Family).Field("arm", arm.Name).Field("description", arm.Description).Field("inputs", arm.Inputs.Count);
            json.Open("weekly");
            foreach (var (name, metric, end) in WeeklyMetrics)
            {
                var perSeed = runs.Select(r => Weekly(r.Days, metric, end)).ToArray();
                json.Open(name).Ratios("mean", Enumerable.Range(0, Weeks).Select(w => Mean(perSeed.Select(s => (double)s[w]))))
                    .Ratios("sd", Enumerable.Range(0, Weeks).Select(w => Sd(perSeed.Select(s => (double)s[w]))));
                json.OpenArray("perSeed"); foreach (var s in perSeed) json.RawItem("[" + string.Join(",", s) + "]"); json.CloseArray().Close();
            }
            json.Close();
            var armIntervention = runs.Select(InterventionMean).ToArray();
            double low = Mean(controlIntervention) - 2 * Sd(controlIntervention), high = Mean(controlIntervention) + 2 * Sd(controlIntervention);
            json.Open("effect").Ratio("interventionOwnPurchasesPerDay", Mean(armIntervention))
                .Ratio("controlOwnPurchasesPerDay", Mean(controlIntervention)).Ratio("controlBandLow", low).Ratio("controlBandHigh", high)
                .Field("outsideControlBand", Mean(armIntervention) < low || Mean(armIntervention) > high).Close();
            if (arm.Family != "control")
            {
                var recovery = runs.Select(r => Enumerable.Range(reverted + 2, EconomyRun.Days - reverted - 1)
                    .Where(day => { double v = Trailing(r.Days, day, d => d.OwnPurchases); return v >= band[day].Low && v <= band[day].High; })
                    .Select(day => (long?)(day - reverted + 1)).FirstOrDefault()).ToArray();
                json.Open("recovery").Field("definition", "days after reversion until the 3-day mean (wholly after reversion) is within the control band")
                    .Raw("daysPerSeed", "[" + string.Join(",", recovery.Select(v => v?.ToString() ?? "null")) + "]")
                    .Field("allWithin14Days", recovery.All(v => v.HasValue && v <= 14));
                var residual = runs.Select((r, i) => Trailing(r.Days, EconomyRun.Days, d => d.OwnPurchases) - Trailing(control[i].Days, EconomyRun.Days, d => d.OwnPurchases)).ToArray();
                json.Ratios("residualAfter14DaysPerSeed", residual).Ratio("residualAfter14DaysMean", Mean(residual)).Close();
            }
            if (arm.Family == "price")
            {
                bool menu = arm.Name == "menu-plus-50";
                var priceInput = arm.Inputs.First(i => i.Kind == InputKind.ProductPrice);
                double basePrice = EconomyFixture.Menu.Single(m => m.Product == priceInput.Product).Cents, changed = priceInput.Value;
                var elasticity = runs.Select((r, i) =>
                {
                    Func<DayMetrics, long> q = menu ? d => d.OwnPurchases : d => d.ProductPurchases(InterventionArms.Product);
                    double qa = r.Days.Where(d => DayMetrics.Phase(d.Day) == "intervention").Sum(q);
                    double qc = control[i].Days.Where(d => DayMetrics.Phase(d.Day) == "intervention").Sum(q);
                    return (qa - qc) / ((qa + qc) / 2) / ((changed - basePrice) / ((changed + basePrice) / 2));
                }).ToArray();
                json.Open("arcElasticity").Field("quantity", menu ? "own purchases" : "burger purchases")
                    .Field("against", "same-seed control, intervention weeks").Ratios("perSeed", elasticity).Ratio("mean", Mean(elasticity)).Close();
            }
            json.Open("accounting").Field("conservationChecks", runs.Sum(r => r.ConservationChecks))
                .Field("duplicateReceipts", runs.Sum(r => r.Days.Last().DuplicateOrders)).Field("historicalClockMisses", runs.Sum(r => r.Days.Last().HistoricalClockMisses))
                .Field("epochReadViolations", runs.Sum(r => r.Days.Last().EpochReadViolations)).Field("inputsPublished", runs.Sum(r => r.InputsPublished))
                .OpenArray("metricsSha256"); foreach (var r in runs) json.RawItem("\"" + EconomyRun.Sha256(r.Json) + "\""); json.CloseArray().Close();
            json.Close();
        }
        json.CloseArray();
        var controlP10 = control.Select(r => Weekly(r.Days, d => d.P10BalanceUnits, true)).ToArray();
        json.Open("householdDrift").Field("p10FallsEveryWeekInControl", controlP10.Select(s => Enumerable.Range(1, Weeks - 1).All(w => s[w] < s[w - 1])).ToArray().All(x => x))
            .Field("note", "Recorded for F6h; F6a sets no threshold.").Close();
        json.Close();
        return json.ToString();
    }

    public static readonly int[] CurveResidents = { 32, 64, 128, 256, 512, 957, 1914 };
    public const int CurveDays = 3;

    /// <summary>Same menu, price, hours, distance and service rating; one side listed at a time.
    /// Served meals per day under rising demand. Today the rival has no ceiling.</summary>
    public static string RivalCurve()
    {
        var json = new Json();
        json.Open().Field("schema", EconomyRun.Schema + "-rival-curve").Field("days", CurveDays).Field("measuredDays", "2.." + CurveDays)
            .Field("ownStaffedCeilingPerHour", EconomyFixture.MealsPerHour)
            .Field("ownStaffedCeilingPerDay", EconomyFixture.MealsPerHour * (EconomyFixture.CloseMinute - EconomyFixture.OpenMinute) / 60)
            .Field("evidence", "Offline Core curve before F6b; rival terms are today's unlimited parallel service with unknown stock.");
        json.OpenArray("points");
        foreach (int residents in CurveResidents)
        {
            var own = EconomyRun.Run(new RunOptions { Family = "curve", Arm = "own-only", Residents = residents, Days = CurveDays, RivalListed = false });
            var rival = EconomyRun.Run(new RunOptions { Family = "curve", Arm = "rival-only", Residents = residents, Days = CurveDays, OwnListed = false });
            var ownDays = own.Days.Where(d => d.Day >= 2).ToList(); var rivalDays = rival.Days.Where(d => d.Day >= 2).ToList();
            json.Open().Field("residents", residents).Field("consumers", residents * EconomyFixture.RepresentationFactor)
                .Ratio("ownServedPerDay", ownDays.Average(d => (double)d.OwnPurchases))
                .Ratio("rivalServedPerDay", rivalDays.Average(d => (double)d.RivalMeals))
                .Field("ownPeakHour", ownDays.Max(d => d.Shops.Values.SelectMany(s => s.Hours).DefaultIfEmpty(0).Max()))
                .Field("rivalPeakHour", rivalDays.Max(d => d.RivalHours.Max()))
                .Ratio("ownNoStaffedOrQueueOffersPerDay", ownDays.Average(d => (double)d.Shops.Values.Sum(s => s.Service + s.Staff)))
                .Close();
            Console.WriteLine($"curve {residents}: own {ownDays.Average(d => d.OwnPurchases):F1}/day, rival {rivalDays.Average(d => d.RivalMeals):F1}/day");
        }
        json.CloseArray().Close();
        return json.ToString();
    }

    /// <summary>Tool cost only (no gate): wall time and allocation per simulated week.</summary>
    public static string Perf(Dictionary<string, List<RunResult>> results)
    {
        var json = new Json();
        json.Open().Field("schema", EconomyRun.Schema + "-perf").Field("note", "Harness runtime and process allocation per simulated week; detached tool cost, not mod-work.");
        json.OpenArray("sizes");
        foreach (int residents in new[] { EconomyFixture.Residents, 2000 })
        {
            var runs = Enumerable.Range(0, 3).Select(seed => EconomyRun.Run(new RunOptions { Family = "perf", Arm = "control-week", Seed = seed, Residents = residents, Days = 7 })).ToList();
            var seconds = runs.Select(r => r.Seconds).OrderBy(x => x).ToArray();
            var bytes = runs.Select(r => (double)r.AllocatedBytes).OrderBy(x => x).ToArray();
            json.Open().Field("residents", residents).Field("consumers", residents * EconomyFixture.RepresentationFactor).Field("weeks", 1).Field("repetitions", runs.Count)
                .Ratio("secondsPerWeekMedian", seconds[1]).Ratio("secondsPerWeekMax", seconds[^1])
                .Field("allocatedBytesPerWeekMedian", (long)bytes[1]).Field("allocatedBytesPerWeekMax", (long)bytes[^1])
                .Field("epochsPerWeek", runs[0].Epochs).Close();
            Console.WriteLine($"perf {residents}: {seconds[1]:F2} s/week, {bytes[1] / 1e6:F1} MB/week");
        }
        json.CloseArray();
        if (results.TryGetValue("control/control", out var control))
            json.Ratio("sixWeekArmSecondsMedian", control.Select(r => r.Seconds).OrderBy(x => x).ElementAt(control.Count / 2))
                .Field("sixWeekArmAllocatedBytesMedian", control.Select(r => r.AllocatedBytes).OrderBy(x => x).ElementAt(control.Count / 2));
        json.Close();
        return json.ToString();
    }
}
