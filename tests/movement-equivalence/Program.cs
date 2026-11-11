// F2a movement-equivalence harness (city audit F2 #89, docs/plans/f2-movement.md section 4).
// Usage: dotnet run --project tests/movement-equivalence -c Release -- [--residents 957] [--days 2]
//        [--arms <substring,...>] [--out <file.json>] [--dump <directory>] [--no-self-checks] [--pose-aim]
// --pose-aim (P-C comparison): walkers aim with the F2c pose rule instead of one path per leg.
// Runs the self-checks and the F2e E2 replay checks, then every arm and its bodies-off reference, prints one line per arm and
// writes the full JSON (schema persistentcity-f2a-movement-1). Exit code 1 if a self-check fails or (F2g) an arm DIFFERS.
// Since F2d (cars follow the traffic kernel) every body arm is expected to equal its bodies-off run; a DIFFERS line
// is a regression. Before F2d the mixed-city arms were the recorded divergence (red).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using PersistentCity.MovementEquivalence;

int residents = MovementFixture.Residents, days = MovementRun.Days;
string? output = null, dump = null; string[] filters = Array.Empty<string>(); bool selfChecks = true, poseAim = false;
for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--residents": residents = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--days": days = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--arms": filters = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries); break;
        case "--out": output = args[++i]; break;
        case "--dump": dump = args[++i]; break;
        case "--no-self-checks": selfChecks = false; break;
        case "--pose-aim": poseAim = true; break;
        default: Console.Error.WriteLine("unknown argument " + args[i]); return 2;
    }
}

var checks = new List<object>();
bool failed = false;
// F2g: wall time per stage (self-checks, E2, arms) for the whole-harness cost.
var watch = System.Diagnostics.Stopwatch.StartNew();
double selfCheckSeconds = 0, e2Seconds = 0;
if (selfChecks)
{
    foreach (var (check, pass, detail) in SelfChecks.Run())
    {
        Console.WriteLine((pass ? "PASS " : "FAIL ") + check + (detail.Length > 0 ? " (" + detail + ")" : ""));
        checks.Add(new { check, pass, detail });
        failed |= !pass;
    }
}

selfCheckSeconds = watch.Elapsed.TotalSeconds;
// F2e E2: a player-contact run with checkpoints, its bodies-off replays and the segment refusals.
var e2 = new List<object>();
if (selfChecks)
{
    // F2g: the same E2 arm with visitor bodies on.
    foreach (var (check, pass, detail) in HoldReplay.Check(residents, Math.Min(days, 1)).Concat(HoldReplay.Check(residents, Math.Min(days, 1), visitors: true)))
    {
        Console.WriteLine((pass ? "PASS " : "FAIL ") + check + (detail.Length > 0 ? " (" + detail + ")" : ""));
        e2.Add(new { check, pass, detail });
        failed |= !pass;
    }
}

e2Seconds = watch.Elapsed.TotalSeconds - selfCheckSeconds;
var arms = MovementArms.All(residents, days);
var wanted = arms.Where(a => filters.Length == 0 || filters.Any(f => a.Name.Contains(f, StringComparison.Ordinal))).ToList();
// References run first so each comparison has its bodies-off run.
var needed = new HashSet<string>(wanted.Select(a => a.Name), StringComparer.Ordinal);
foreach (var arm in wanted.ToList()) { var r = arm; while (r.Reference.Length > 0 && needed.Add(r.Reference)) r = arms.First(a => a.Name == r.Reference); }
var results = new Dictionary<string, MovementResult>(StringComparer.Ordinal);
var comparisons = new List<Comparison>();
foreach (var arm in arms.Where(a => needed.Contains(a.Name)).OrderBy(a => a.Reference.Length == 0 ? 0 : a.Family == "bodies-off" ? 1 : 2))
{
    var options = arm.Options();
    options.DumpDirectory = dump;
    options.Bodies.PoseAim = poseAim;
    var result = MovementRun.Run(options);
    results[arm.Name] = result;
    string line = arm.Name.PadRight(36) + " " + result.Seconds.ToString("F1", CultureInfo.InvariantCulture) + " s";
    if (arm.Reference.Length > 0)
    {
        var comparison = Equivalence.Compare(result, results[arm.Reference]);
        comparisons.Add(comparison);
        failed |= !comparison.Identical;
        line += (comparison.Identical ? "  EQUAL " : "  DIFFERS ") + string.Join(" ", comparison.Equal.Select(e => e.Key + "=" + (e.Value ? "=" : "x")))
            + "  sales " + comparison.Delta["meals.ownSales"].ToString("+0;-0;0", CultureInfo.InvariantCulture)
            + "  missed " + comparison.Delta["routines.missedActivities"].ToString("+0;-0;0", CultureInfo.InvariantCulture)
            + "  bodies " + result.Metrics["bodies.acquired"].ToString(CultureInfo.InvariantCulture)
            + "  paths " + result.Metrics["walkers.pathRequests"].ToString(CultureInfo.InvariantCulture)
            + "  lag_p95 " + result.Metrics["walkers.lagP95Metres"].ToString("F2", CultureInfo.InvariantCulture)
            + "  ahead " + result.Metrics["walkers.aheadFrames"].ToString(CultureInfo.InvariantCulture);
    }
    Console.WriteLine(line);
}

double totalSeconds = watch.Elapsed.TotalSeconds;
Console.WriteLine("self-checks " + selfCheckSeconds.ToString("F1", CultureInfo.InvariantCulture) + " s, e2 " + e2Seconds.ToString("F1", CultureInfo.InvariantCulture)
    + " s, arms " + (totalSeconds - selfCheckSeconds - e2Seconds).ToString("F1", CultureInfo.InvariantCulture) + " s, total " + totalSeconds.ToString("F1", CultureInfo.InvariantCulture) + " s");
if (output != null)
{
    var json = new
    {
        schema = MovementRun.Schema, residents, days, representationFactor = MovementFixture.RepresentationFactor,
        gameMinutesPerRealSecond = MovementRun.GameMinutesPerRealSecond, selfCheckSeconds, e2Seconds, totalSeconds, selfChecks = checks, e2,
        arms = arms.Where(a => results.ContainsKey(a.Name)).Select(a => new
        {
            arm = a.Name, family = a.Family, reference = a.Reference, result = results[a.Name],
            comparison = comparisons.FirstOrDefault(c => c.Arm == a.Name)
        })
    };
    File.WriteAllText(output, JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true }));
}
return failed ? 1 : 0;
