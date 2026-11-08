using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PersistentCity.EconomyInterventions;

// F6a/F6h intervention harness. Usage:
//   EconomyInterventions <outDir> [--seeds N] [--residents N] [--arms family/name,...] [--skip-curve] [--skip-perf] [--checks-only] [--integrated] [--price-weight 0..3] [--meals-per-hour N] [--menu legacy|native] [--no-rival]
// Writes one metrics JSON per arm and seed, a seed-band summary, today's rival saturation curve
// and tool-cost measurements. Offline Core evidence only; never native or rig proof.
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 0 || args[0].StartsWith("--", StringComparison.Ordinal))
        { Console.Error.WriteLine("usage: EconomyInterventions <outDir> [--seeds N] [--residents N] [--arms a,b] [--skip-curve] [--skip-perf] [--checks-only] [--integrated] [--price-weight 0..3] [--meals-per-hour N] [--menu legacy|native] [--no-rival]"); return 2; }
        string outDir = args[0];
        int seeds = Option(args, "--seeds", EconomyRun.Seeds), residents = Option(args, "--residents", EconomyFixture.Residents);
        string? filter = args.SkipWhile(a => a != "--arms").Skip(1).FirstOrDefault();
        Directory.CreateDirectory(Path.Combine(outDir, "runs"));

        bool integrated = args.Contains("--integrated");
        if (integrated && args.Contains("--checks-only"))
        {
            Console.Error.WriteLine("Integrated checks: dotnet test tests/PersistentCity.Tests.csproj --filter FullyQualifiedName~F6hIntegratedTests");
            return 2;
        }
        string? priceArg = args.SkipWhile(a => a != "--price-weight").Skip(1).FirstOrDefault();
        double priceWeight = priceArg == null ? new RunOptions().PriceWeight
            : double.Parse(priceArg, System.Globalization.CultureInfo.InvariantCulture);
        // Integrated runs use the fast representative unit checks instead of silently running
        // the legacy 48-person six-week self-checks before every opt-in sweep.
        if (!integrated) SelfChecks.Run();
        if (args.Contains("--checks-only")) return 0;

        int mealsPerHour = Option(args, "--meals-per-hour", EconomyFixture.MealsPerHour);
        string menu = args.SkipWhile(a => a != "--menu").Skip(1).FirstOrDefault() ?? "legacy";
        if (menu != "legacy" && menu != "native" || mealsPerHour <= 0) { Console.Error.WriteLine("--menu must be legacy or native; --meals-per-hour must be positive"); return 2; }
        var profile = mealsPerHour == EconomyFixture.MealsPerHour && menu == "legacy" ? FixtureProfile.CapacityBound
            : new FixtureProfile((mealsPerHour == EconomyFixture.MealsPerHour ? "capacity-bound" : "non-binding-" + mealsPerHour) + "-" + menu + "-menu",
                mealsPerHour, menu == "native" ? EconomyFixture.NativeMenu : EconomyFixture.Menu);
        // --no-rival: the sole-shop (monopoly) price fixture; the rival is never listed.
        bool rivalListed = !args.Contains("--no-rival");
        if (!rivalListed && profile == FixtureProfile.CapacityBound) { Console.Error.WriteLine("--no-rival is a price-only profile option"); return 2; }
        if (!rivalListed) profile = profile with { Name = profile.Name + "-no-rival" };
        // Staffing/other inputs restore the original fixture's terms, so a changed profile is price-only.
        var candidates = profile == FixtureProfile.CapacityBound ? InterventionArms.All() : InterventionArms.Price(profile);
        var arms = new[] { InterventionArms.Control }.Concat(candidates)
            .Where(a => filter == null || a.Family == "control" || filter.Split(',').Contains(a.Family + "/" + a.Name)).ToList();
        var results = new Dictionary<string, List<RunResult>>(StringComparer.Ordinal);
        foreach (var arm in arms)
        {
            var list = new List<RunResult>();
            for (int seed = 0; seed < seeds; seed++)
            {
                var result = EconomyRun.Run(new RunOptions { Family = arm.Family, Arm = arm.Name, Seed = seed, Residents = residents, Inputs = arm.Inputs, Integrated = integrated, PriceWeight = priceWeight, Profile = profile, RivalListed = rivalListed });
                File.WriteAllText(Path.Combine(outDir, "runs", arm.Family + "-" + arm.Name + "-seed" + seed + ".json"), result.Json);
                Console.WriteLine($"{arm.Family}/{arm.Name} seed {seed}: {result.Seconds:F1} s, {result.ConservationChecks} conservation checks, {result.InputsPublished} inputs, metrics sha256 {EconomyRun.Sha256(result.Json)[..16]}");
                list.Add(result);
            }
            if (arm == InterventionArms.Control)
            {
                // Full-size save/reload equivalence on the measured fixture, outside any timing claim.
                var reloaded = EconomyRun.Run(new RunOptions { Seed = 0, Residents = residents, ReloadAt = SelfChecks.MidEpochReload, Integrated = integrated, PriceWeight = priceWeight, Profile = profile, RivalListed = rivalListed });
                if (reloaded.Json != list[0].Json || reloaded.FinalSaveSha256 != list[0].FinalSaveSha256)
                    throw new InvalidOperationException("full-size mid-epoch reload changed metrics");
                Console.WriteLine($"PASS {residents}-resident mid-epoch reload ({reloaded.InFlightAtReload} meals in flight): byte-identical metrics and final save");
                var proof = new Json().Open().Field("residents", residents).Field("consumers", residents * EconomyFixture.RepresentationFactor)
                    .Field("days", EconomyRun.Days).Field("integrated", integrated).Field("seed", 0)
                    .Field("reloads", reloaded.Reloads).Field("inFlightAtReload", reloaded.InFlightAtReload)
                    .Field("metricsSha256", EconomyRun.Sha256(list[0].Json))
                    .Field("reloadedMetricsSha256", EconomyRun.Sha256(reloaded.Json))
                    .Field("saveSha256", list[0].FinalSaveSha256).Field("reloadedSaveSha256", reloaded.FinalSaveSha256).Close();
                File.WriteAllText(Path.Combine(outDir, "reload-equivalence.json"), proof.ToString());
            }
            results[arm.Family + "/" + arm.Name] = list;
        }
        File.WriteAllText(Path.Combine(outDir, "baseline-summary.json"), Baseline.Summary(arms, results, residents, seeds, integrated, profile));
        Console.WriteLine("RECORDED summary: " + arms.Count + " arms x " + seeds + " seeds");
        if (integrated) Console.WriteLine("F6h acceptance is separate: run acceptance.py; recording a sweep is not acceptance.");
        if (!integrated && !args.Contains("--skip-curve"))
        {
            File.WriteAllText(Path.Combine(outDir, "rival-curve.json"), Baseline.RivalCurve());
            Console.WriteLine("PASS unlimited rival curve recorded");
        }
        if (!integrated && !args.Contains("--skip-perf"))
        {
            File.WriteAllText(Path.Combine(outDir, "perf.json"), Baseline.Perf(results));
            Console.WriteLine("PASS harness cost recorded");
        }
        return 0;
    }

    private static int Option(string[] args, string name, int fallback)
    {
        int at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length ? int.Parse(args[at + 1], System.Globalization.CultureInfo.InvariantCulture) : fallback;
    }
}
