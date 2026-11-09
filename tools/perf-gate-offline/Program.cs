using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Tests;

namespace PersistentCity.PerfGateOffline;

// F4e offline pre-gate (docs/plans/f4-perf-gate.md, F4e row). Usage:
//   dotnet run --project tools/perf-gate-offline -c Release -- [--out <report.json>] [--seed N] [--only <window prefix>]
//   dotnet run --project tools/perf-gate-offline -c Release -- --self-test
static class Program
{
    const double Day3 = 3 * 1440, Day4 = 4 * 1440;

    static int Main(string[] args)
    {
        string? Arg(string name) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
        int seed = int.TryParse(Arg("--seed"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int s) ? s : Fixture.DefaultSeed;
        if (args.Contains("--self-test")) return SelfTest.Run(seed);
        string output = Arg("--out") ?? System.IO.Path.Combine("build", "perf-gate-offline", "report-offline.json");
        string? only = Arg("--only");

        var total = Stopwatch.StartNew();
        long allocated = GC.GetTotalAllocatedBytes(true);
        var shape = Fixture.Shape();
        var aged = Fixture.Aged(shape, seed);
        var specs = Matrix().Where(w => only == null || w.Id.StartsWith(only, StringComparison.Ordinal)).ToList();
        var checkpoints = Fixture.Checkpoints(aged, shape.Catalogue, specs.Select(w => w.From));
        double fixtureSeconds = total.Elapsed.TotalSeconds;
        Console.Error.WriteLine($"fixture and {checkpoints.Count} checkpoints in {fixtureSeconds:F1} s");

        var read = Persistence.SaveCodec.Read(aged.Payload).State!;
        var (commuters, pool) = F5gPopulationFillTests.Counts(read);
        var inputs = new Report.Inputs
        {
            Aged = aged, Seed = seed, Residents = read.Citizens.Count, Commuters = (int)commuters, Pool = (int)pool,
            Persons = read.Citizens.Count + read.Visitors.Count, CommandLine = string.Join(" ", args),
        };
        var stationary = new[] { Kerb(read) };
        var path = WalkPath(read);
        // Decision 3 discards each process's first window (JIT); the offline run does the same with the first spec.
        var warm = Stopwatch.StartNew();
        if (specs.Count > 0) WindowRunner.Run(specs[0], checkpoints[specs[0].From], shape.Catalogue, stationary, uninterruptedCheck: false);
        inputs.DiscardedWarmUpSeconds = warm.Elapsed.TotalSeconds;
        foreach (var spec in specs)
        {
            var result = WindowRunner.Run(spec, checkpoints[spec.From], shape.Catalogue, spec.Script == "S-path" ? path : stationary, uninterruptedCheck: true);
            inputs.Windows.Add(result);
            var mod = result.ModWorkMs.OrderBy(v => v).ToArray();
            Console.Error.WriteLine($"{spec.Id,-48} frames {result.Frames,7} p95 {Report.Percentile(mod, .95):F4} p99 {Report.Percentile(mod, .99):F4} max {mod[^1]:F3} ms"
                + $" >=30 {result.Spikes.Count} lag {result.MaxLag:F3} held {result.HeldFrames} bodies {result.MedianBodies} sliced=uninterrupted {result.StateHash == result.UninterruptedHash} ({result.ToolSeconds:F1} s)");
            if (result.StateHash != result.UninterruptedHash) { Console.Error.WriteLine("FAIL: sliced run differs from the uninterrupted advance in " + spec.Id); return 1; }
            if (spec.Arm == "declared" && spec.Fps == 60 && (spec.Condition == "W-lunch" || spec.Condition == "W-evening") && spec.Script == "S-stationary")
                for (int i = 0; i < result.FrameMinute.Count; i++)
                {
                    double m = result.FrameMinute[i], rollover = Math.Round(m / 60) * 60;
                    if (Math.Abs(m - rollover) <= 1) inputs.Accounting.Add((result, i));
                }
        }
        inputs.FixtureSeconds = fixtureSeconds;
        inputs.TotalSeconds = total.Elapsed.TotalSeconds;
        inputs.AllocatedBytes = GC.GetTotalAllocatedBytes(true) - allocated;
        var report = Report.Build(inputs);
        var errors = Report.Validate(report);
        if (errors.Count > 0) { foreach (var e in errors) Console.Error.WriteLine("report invalid: " + e); return 1; }
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(output))!);
        File.WriteAllText(output, report.ToString(Formatting.Indented) + "\n");
        foreach (var v in report["verdicts"]!) Console.Error.WriteLine($"{v["row"],-3} {v["verdict"],-16} {v["worst"]?.ToString(Formatting.None)} {v["reason"]}");
        Console.Error.WriteLine($"wrote {output} ({total.Elapsed.TotalSeconds:F1} s)");
        return 0;
    }

    /// <summary>Decision 3's windows on FT-F4-Declared (day 3, idle on day 4), the declared arm at 30/60/120 FPS with the
    /// driver at 48/24, and G9's fastest-traveller sequence and day at 60 FPS.</summary>
    static IEnumerable<WindowSpec> Matrix()
    {
        double declared = Pace.At(Pace.DeclaredFactor, 1);
        var windows = new (string Condition, double From, double To)[]
        {
            ("W-morning", Day3 + 7 * 60, Day3 + 8 * 60 + 30), ("W-lunch", Day3 + 11 * 60 + 45, Day3 + 13 * 60 + 15),
            ("W-evening", Day3 + 17 * 60 + 45, Day3 + 19 * 60 + 15), ("W-midnight", Day3 + 23 * 60 + 30, Day4 + 60),
            ("W-idle", Day4 + 3 * 60, Day4 + 4 * 60 + 30),
        };
        foreach (var (condition, from, to) in windows)
            foreach (int fps in new[] { 30, 60, 120 })
                yield return new WindowSpec(condition, "S-stationary", "declared", from, to, fps, declared, Presentation.Bodies, Pace.DeclaredFactor, 1);
        yield return new WindowSpec("W-evening", "S-path", "declared", Day3 + 17 * 60 + 45, Day3 + 19 * 60 + 15, 60, declared, Presentation.Bodies, Pace.DeclaredFactor, 1);
        double start = Day3 + 6 * 60;
        for (int speed = 1; speed <= 3; speed++)
            yield return new WindowSpec("G9a-speed" + speed, "S-stationary", "fastest-traveller", start + (speed - 1) * 60, start + speed * 60, 60,
                Pace.At(Pace.FastestFactor, speed), Presentation.Bodies, Pace.FastestFactor, speed);
        yield return new WindowSpec("G9a-speed6", "hidden", "fastest-traveller", start + 180, start + 420, 60, Pace.At(Pace.FastestFactor, 6), Presentation.Hidden, Pace.FastestFactor, 6);
        yield return new WindowSpec("G9a-skip120", "time-machine", "fastest-traveller", start + 420, start + 540, 60, Pace.At(Pace.FastestFactor, 6), Presentation.TimeMachine, Pace.FastestFactor, 0);
        yield return new WindowSpec("G9b-day-speed3", "S-stationary", "fastest-traveller", start, start + 1440, 60, Pace.At(Pace.FastestFactor, 3), Presentation.Bodies, Pace.FastestFactor, 3);
    }

    /// <summary>S-stationary: a kerb point 3 m from the first player shop.</summary>
    internal static Position Kerb(CityState state)
    {
        var shop = state.Places.Single(p => p.Id == state.Food!.Shops[0].PlaceId).Position;
        return new Position(shop.X + 3, shop.Y, shop.Z);
    }

    /// <summary>S-path: from the shop kerb to the city's centroid (the dense part) and to the farthest home (quiet) and back.</summary>
    internal static IReadOnlyList<Position> WalkPath(CityState state)
    {
        var kerb = Kerb(state);
        var centre = new Position(state.Places.Average(p => p.Position.X), 0, state.Places.Average(p => p.Position.Z));
        var far = state.Places.Where(p => p.Kind == PlaceKind.Home).OrderByDescending(p => Position.Distance(p.Position, kerb)).ThenBy(p => p.Id, StringComparer.Ordinal).First().Position;
        return new[] { kerb, centre, far };
    }
}
