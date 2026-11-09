using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace PersistentCity.PerfGateOffline;

/// <summary>The F4e row's offline tests: same seed, same state hashes; frame-sliced runs equal an uninterrupted advance
/// at every cadence and presentation (as the #128 replay does); the report carries every documented field with
/// source=offline, and a report missing any of them is refused.</summary>
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
        // 1. Same seed: two fixtures built from nothing are byte-identical, and the same window run on each ends in the same state.
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

        // 2. Sliced runs equal the uninterrupted advance: every frame rate with bodies, fast-forward with bodies released, and the skip.
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
        // Across an hour rollover with the journal observing: the window started at 19:00 reaches the same state as the one through it.
        var fromHour = Window(ca[3 * 1440 + 19 * 60.0], 60, Presentation.Bodies, to, pace);
        Check(fromHour.StateHash == fromHour.UninterruptedHash, "a window starting on an hour rollover equals the uninterrupted advance");

        // 3. The report carries every documented field with source=offline; each missing field or a wrong source is refused.
        var inputs = new Report.Inputs { Aged = a, Seed = seed, Windows = { first, fast, skip }, CommandLine = "--self-test" };
        for (int i = 0; i < first.FrameMinute.Count; i++) if (Math.Abs(first.FrameMinute[i] - (3 * 1440 + 19 * 60)) <= 1) inputs.Accounting.Add((first, i));
        var report = Report.Build(inputs);
        var errors = Report.Validate(report);
        Check(errors.Count == 0, "report validates against the plan's field list" + (errors.Count > 0 ? ": " + string.Join("; ", errors) : ""));
        Check((string?)report["source"] == "offline", "report source=offline");
        int refused = 0, mutations = 0;
        void Refuses(string what, Action<JObject> mutate)
        {
            var copy = (JObject)report.DeepClone(); mutate(copy); mutations++;
            if (Report.Validate(copy).Count > 0) refused++; else Console.Error.WriteLine("FAIL not refused: " + what);
        }
        foreach (var f in Report.HeaderFields) Refuses("header." + f, r => ((JObject)r["header"]!).Remove(f));
        foreach (var f in Report.MachineFields) Refuses("header.machine." + f, r => ((JObject)r["header"]!["machine"]!).Remove(f));
        foreach (var f in Report.WindowFields) Refuses("window." + f, r => ((JObject)r["windows"]![0]!).Remove(f));
        foreach (var f in Report.ReceiptFields) Refuses("receipt." + f, r => ((JObject)r["windows"]![0]!["receipts"]!).Remove(f));
        foreach (var row in Report.Rows) Refuses("verdict " + row, r => ((JArray)r["verdicts"]!).First(v => (string?)v["row"] == row).Remove());
        Refuses("source=native", r => r["source"] = "native");
        Refuses("overall pass", r => r["overall"]!["verdict"] = "pass");
        Refuses("empty commit", r => r["header"]!["sourceCommit"] = "");
        Check(refused == mutations, refused + " of " + mutations + " mutated reports refused");

        Console.Error.WriteLine(failures == 0 ? "self-test passed" : failures + " self-test checks failed");
        return failures == 0 ? 0 : 1;
    }
}
