using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace PersistentCity.PerfGateOffline;

internal static class SelfTest
{
    private static int failures;
    private static void Check(bool ok, string what)
    {
        Console.Error.WriteLine((ok ? "PASS " : "FAIL ") + what);
        if (!ok) failures++;
    }

    public static int Run(int seed)
    {
        var shape = Fixture.Shape();
        var a = Fixture.Aged(shape, seed);
        var b = Fixture.Aged(shape, seed);
        Check(a.Payload == b.Payload, "same seed builds the same aged fixture (" + a.Sha256[..16] + ")");
        var other = Fixture.Aged(shape, seed + 1);
        Check(other.Payload != a.Payload, "a different seed builds a different fixture (the seed reaches the fixture)");

        double from = 3 * 1440 + 18 * 60 + 30, to = 3 * 1440 + 19 * 60 + 5;
        var ca = Fixture.Checkpoints(a, shape.Catalogue, new[] { from, 3 * 1440 + 19 * 60 + 0.0 });
        var cb = Fixture.Checkpoints(b, shape.Catalogue, new[] { from });
        Check(ca[from].Payload == cb[from].Payload, "same seed gives the same window checkpoint");
        double pace = Pace.At(Pace.DeclaredFactor, 1);
        var stationary = new[] { Program.Kerb(Persistence.SaveCodec.Read(a.Payload).State!) };
        WindowResult Window(Checkpoint c, int fps, Presentation presentation, double end, double speedPace, bool check = true)
            => WindowRunner.Run(new WindowSpec("self-test-" + presentation + "-" + Fixture.Clock(c.Minute), presentation == Presentation.Bodies ? "S-stationary" : presentation.ToString(), "declared", c.Minute, end, fps, speedPace, presentation, Pace.DeclaredFactor, 1),
                c, shape.Catalogue, stationary, check);
        var first = Window(ca[from], 60, Presentation.Bodies, to, pace);
        var second = Window(cb[from], 60, Presentation.Bodies, to, pace, check: false);
        Check(first.StateHash == second.StateHash, "same seed, same window: identical state hashes (" + first.StateHash[..16] + ")");
        Check(first.Orders > 0 && first.MedianBodies > 0, "the window did work: " + first.Orders + " orders, median " + first.MedianBodies + " bodies");

        Check(first.StateHash == first.UninterruptedHash, "60 FPS with bodies equals the uninterrupted advance");
        var hashes = new List<string> { first.StateHash };
        foreach (int fps in new[] { 30, 120 })
        {
            var run = Window(ca[from], fps, Presentation.Bodies, to, pace);
            Check(run.StateHash == run.UninterruptedHash, fps + " FPS with bodies equals the uninterrupted advance");
            hashes.Add(run.StateHash);
        }
        Check(hashes.Distinct().Count() == 1, "30, 60 and 120 FPS end in the same state");
        var fast = Window(ca[from], 60, Presentation.Hidden, to, Pace.At(Pace.FastestFactor, 6));
        Check(fast.StateHash == fast.UninterruptedHash && fast.StateHash == first.StateHash, "fast-forward (speed 6, factor 1) equals the uninterrupted advance");
        var skip = Window(ca[from], 60, Presentation.TimeMachine, to, pace);
        Check(skip.StateHash == skip.UninterruptedHash && skip.StateHash == first.StateHash, "a time-machine skip equals the uninterrupted advance");
        var fromHour = Window(ca[3 * 1440 + 19 * 60.0], 60, Presentation.Bodies, to, pace);
        Check(fromHour.StateHash == fromHour.UninterruptedHash, "a window starting on an hour rollover equals the uninterrupted advance");

        string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "perf-gate-offline-self-test-" + Environment.ProcessId);
        var inputs = new Export.Inputs { Aged = a, Seed = seed, Windows = { first, fast, skip }, CommandLine = "--self-test" };
        for (int i = 0; i < first.FrameMinute.Count; i++) if (Math.Abs(first.FrameMinute[i] - (3 * 1440 + 19 * 60)) <= 1) inputs.Accounting.Add((first, i));
        var export = Export.Build(inputs);
        var report = Analyzer.Analyze(export, dir, "accepted", out int code, out string output);
        Check(code == 0, "F4a's analyzer accepts the offline export" + (code != 0 ? ": " + output : ""));
        Check((string?)report["format"] == "perf-gate-report/v1" && (string?)report["source"] == "offline" && (string?)report["contract"] == "perf-gate/v1",
            "the report is perf-gate-report/v1, contract perf-gate/v1, source=offline");
        Check((string?)report["overall"] != "pass" && report["windows"]!.All(w => (bool?)w["valid"] == false),
            "every offline window is invalid as a gate window and the overall verdict is " + report["overall"]);
        Check(report["windows"]!.Count() == 4 && report["windows"]!.All(w => (string?)w["rows"]?["M1"]?["verdict"] is "pass" or "fail"),
            "each window, W-accounting included, carries its own M1 expectation");
        int refused = 0, mutations = 0;
        void Refuses(string what, Action<JObject> mutate)
        {
            var copy = (JObject)export.DeepClone(); mutate(copy); mutations++;
            Analyzer.Analyze(copy, dir, "refused", out int exit, out string text);
            if (exit == 2) refused++; else Console.Error.WriteLine("FAIL not refused (exit " + exit + "): " + what + " " + text);
        }
        foreach (var f in Analyzer.RequiredHeaderFields()) Refuses("header." + f, r => ((JObject)r["header"]!).Remove(f));
        Refuses("header.machine.cpu", r => ((JObject)r["header"]!["machine"]!).Remove("cpu"));
        Refuses("fixture provenance_hash", r => ((JObject)r["header"]!["fixtures"]![0]!).Remove("provenance_hash"));
        Refuses("contract_version", r => r["header"]!["contract_version"] = "perf-gate/v0");
        Refuses("a native window", r => r["windows"]![0]!["source"] = "native");
        Refuses("source=native with offline n/a fields", r => { r["source"] = "native"; foreach (var w in r["windows"]!) w["source"] = "native"; });
        Check(refused == mutations, refused + " of " + mutations + " mutated exports refused by F4a's analyzer");

        var (_, perFrame) = WindowRunner.BodyMovement(60);
        Check(first.HeldFrames > 0 && Math.Abs(first.HeldBodyMoveMinutes - first.HeldFrames * perFrame) < 1e-9,
            "bodies keep moving on " + first.HeldFrames + " held frames");
        var fastBodies = Window(ca[from], 60, Presentation.Bodies, to, Pace.At(Pace.FastestFactor, 3));
        Check(Math.Abs(first.BodyMoveMinutes / first.Frames - perFrame) < 1e-12 && Math.Abs(fastBodies.BodyMoveMinutes / fastBodies.Frames - perFrame) < 1e-12,
            "body movement per frame is the same at factor 1/3 speed 1 and factor 1 speed 3");

        WindowResult Synthetic(string condition, string arm, int speed, Presentation presentation, double ms)
        {
            var w = new WindowResult { Spec = new WindowSpec(condition, "S-stationary", arm, 0, 60, 60, Pace.At(Pace.FastestFactor, Math.Max(1, speed)), presentation, 1, speed),
                Frames = 100, StateHash = "x" };
            for (int i = 0; i < 100; i++) { w.ModWorkMs.Add(ms); w.CoreMs.Add(ms); w.DriverMs.Add(0); w.FrameMinute.Add(i * .6); w.FrameGc.Add(0); w.FrameLag.Add(0); }
            return w;
        }
        JObject Analyzed(Export.Inputs i) => Analyzer.Analyze(Export.Build(i), dir, "synthetic", out _, out _);
        string M1(JObject r, string condition) => (string)r["windows"]!.First(w => ((string)w["id"]!).StartsWith(condition, StringComparison.Ordinal))["rows"]!["M1"]!["verdict"]!;
        var okDeclared = Synthetic("synthetic-declared", "declared", 1, Presentation.Bodies, .1);
        var slowFastest = Synthetic("synthetic-speed3", "fastest-traveller", 3, Presentation.Bodies, 8);
        var both = Analyzed(new Export.Inputs { Aged = a, Windows = { okDeclared, slowFastest } });
        Check(M1(both, "synthetic-speed3") == "fail" && M1(both, "synthetic-declared") == "pass", "a slow speed-3 window fails M1, a fast declared window passes");
        var accounting = new Export.Inputs { Aged = a, Windows = { okDeclared } };
        var slowAccounting = Synthetic("synthetic-lunch", "declared", 1, Presentation.Bodies, 8);
        for (int i = 0; i < 100; i++) accounting.Accounting.Add((slowAccounting, i));
        Check(M1(Analyzed(accounting), "W-accounting") == "fail", "slow W-accounting frames fail M1");
        var hiddenOnly = Analyzed(new Export.Inputs { Aged = a, Windows = { Synthetic("synthetic-speed6", "fastest-traveller", 6, Presentation.Hidden, .1) } });
        Check((string?)hiddenOnly["rows"]?["S5"]?["verdict"] == "not established" && (string?)hiddenOnly["windows"]![0]!["arm"] == "report-only",
            "without G9's day window S5 is not established; speed 6 is report-only");
        try { System.IO.Directory.Delete(dir, true); } catch (System.IO.IOException) { }

        Console.Error.WriteLine(failures == 0 ? "self-test passed" : failures + " self-test checks failed");
        return failures == 0 ? 0 : 1;
    }
}
