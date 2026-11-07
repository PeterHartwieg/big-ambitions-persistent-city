using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PersistentCity.EconomyInterventions;

// F6a/F6h intervention harness. Usage:
//   EconomyInterventions <outDir> [--seeds N] [--residents N] [--arms family/name,...] [--skip-curve] [--skip-perf] [--checks-only] [--integrated] [--price-weight 0..3]
// Writes one metrics JSON per arm and seed, a seed-band summary, today's rival saturation curve
// and tool-cost measurements. Offline Core evidence only; never native or rig proof.
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 0 || args[0].StartsWith("--", StringComparison.Ordinal))
        { Console.Error.WriteLine("usage: EconomyInterventions <outDir> [--seeds N] [--residents N] [--arms a,b] [--skip-curve] [--skip-perf] [--checks-only] [--integrated] [--price-weight 0..3]"); return 2; }
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
        double priceWeight = double.Parse(args.SkipWhile(a => a != "--price-weight").Skip(1).FirstOrDefault() ?? "1",
            System.Globalization.CultureInfo.InvariantCulture);
        // Integrated runs use the fast representative unit checks instead of silently running
        // the legacy 48-person six-week self-checks before every opt-in sweep.
        if (!integrated) SelfChecks.Run();
        if (args.Contains("--checks-only")) return 0;

        var arms = new[] { InterventionArms.Control }.Concat(InterventionArms.All())
            .Where(a => filter == null || a.Family == "control" || filter.Split(',').Contains(a.Family + "/" + a.Name)).ToList();
        var results = new Dictionary<string, List<RunResult>>(StringComparer.Ordinal);
        foreach (var arm in arms)
        {
            var list = new List<RunResult>();
            for (int seed = 0; seed < seeds; seed++)
            {
                var result = EconomyRun.Run(new RunOptions { Family = arm.Family, Arm = arm.Name, Seed = seed, Residents = residents, Inputs = arm.Inputs, Integrated = integrated, PriceWeight = priceWeight });
                File.WriteAllText(Path.Combine(outDir, "runs", arm.Family + "-" + arm.Name + "-seed" + seed + ".json"), result.Json);
                Console.WriteLine($"{arm.Family}/{arm.Name} seed {seed}: {result.Seconds:F1} s, {result.ConservationChecks} conservation checks, {result.InputsPublished} inputs, metrics sha256 {EconomyRun.Sha256(result.Json)[..16]}");
                list.Add(result);
            }
            if (arm == InterventionArms.Control && !integrated)
            {
                // Full-size save/reload equivalence on the measured fixture, outside any timing claim.
                var reloaded = EconomyRun.Run(new RunOptions { Seed = 0, Residents = residents, ReloadAt = SelfChecks.MidEpochReload });
                if (reloaded.Json != list[0].Json || reloaded.FinalSaveSha256 != list[0].FinalSaveSha256)
                    throw new InvalidOperationException("full-size mid-epoch reload changed metrics");
                Console.WriteLine($"PASS {residents}-resident mid-epoch reload ({reloaded.InFlightAtReload} meals in flight): byte-identical metrics and final save");
            }
            results[arm.Family + "/" + arm.Name] = list;
        }
        File.WriteAllText(Path.Combine(outDir, "baseline-summary.json"), Baseline.Summary(arms, results, residents, seeds, integrated));
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
