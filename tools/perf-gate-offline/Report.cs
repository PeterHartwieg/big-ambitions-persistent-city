using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;

namespace PersistentCity.PerfGateOffline;

/// <summary>Writes and checks <c>perf-gate-report/v1</c> with <c>source=offline</c> (F4 plan, Decision 8). F4a's
/// <c>tools/perf-gate/contract-v1.json</c> has not merged at this base, so <see cref="Validate"/> checks the plan's documented
/// field list; validation against F4a's schema replaces it when F4a merges. Native-only fields carry an explicit
/// <c>n/a (offline)</c> value instead of being left out, so a missing field still refuses the report.</summary>
internal static class Report
{
    public const string Schema = "perf-gate-report/v1", Contract = "perf-gate/v1", Source = "offline";
    public const string NotApplicable = "n/a (offline)";
    public const string NotEstablished = "not established";

    /// <summary>Decision 8 header fields, in the plan's order.</summary>
    public static readonly string[] HeaderFields =
    {
        "sourceCommit", "persistentCityDllSha256", "probeDllSha256", "expectedScopeListHash", "gameBuild", "modSet",
        "rigCommit", "rigOverlay", "machine", "settingsReadBack", "pcConfig", "fixtures", "contractVersion", "movementState",
        "activeRigCapabilities",
    };
    /// <summary>Decision 1 machine fields.</summary>
    public static readonly string[] MachineFields = { "cpu", "gpu", "ram", "osBuild", "gpuDriver", "displayRefreshHz", "powerPlan" };
    /// <summary>Decision 3 invalidation receipts.</summary>
    public static readonly string[] ReceiptFields = { "autosave", "contamination", "priorityFocusSettings", "probeCoverage", "presentationTrace" };
    /// <summary>Decision 8 per-window table.</summary>
    public static readonly string[] WindowFields =
    {
        "condition", "script", "build", "repetition", "frames", "receipts", "M1", "M2", "M3", "M4", "P1", "P2", "P3",
        "reference", "P4", "S1", "S2", "S3", "S4", "S5", "density", "bodies", "gc",
    };
    /// <summary>Decision 4 rows, each needing a verdict.</summary>
    public static readonly string[] Rows = { "M1", "M2", "M3", "M4", "P1", "P2", "P3", "P4", "S1", "S2", "S3", "S4", "S5", "L1", "L2", "L3", "L4", "R1" };
    public static readonly string[] Verdicts = { "pass", "fail", NotEstablished };

    /// <summary>Nearest-rank percentile over one window's raw frames (Decision 4): the value at rank ceil(p·n).</summary>
    public static double Percentile(IReadOnlyList<double> sorted, double p)
        => sorted.Count == 0 ? double.NaN : sorted[Math.Max(0, Math.Min(sorted.Count - 1, (int)Math.Ceiling(p * sorted.Count) - 1))];

    private static JObject Stats(IEnumerable<double> values)
    {
        var s = values.OrderBy(v => v).ToArray();
        if (s.Length == 0) return new JObject { ["n"] = 0 };
        return new JObject
        {
            ["n"] = s.Length, ["p50"] = R(Percentile(s, .50)), ["p95"] = R(Percentile(s, .95)), ["p99"] = R(Percentile(s, .99)),
            ["max"] = R(s[^1]), ["mean"] = R(s.Average()), ["atLeast5Ms"] = s.Count(v => v >= 5), ["atLeast30Ms"] = s.Count(v => v >= 30),
        };
    }
    private static double R(double value, int digits = 4) => Math.Round(value, digits);

    public sealed class Inputs
    {
        public Checkpoint Aged = null!;
        public int Seed;
        public int Persons, Residents, Commuters, Pool;
        public double FixtureSeconds, TotalSeconds, DiscardedWarmUpSeconds;
        public long AllocatedBytes;
        public string CommandLine = "";
        public List<WindowResult> Windows = new();
        /// <summary>Raw frames pooled for W-accounting: the frames within one game minute of a native hour rollover.</summary>
        public List<(WindowResult From, int Index)> Accounting = new();
    }

    public static JObject Build(Inputs inputs)
    {
        var report = new JObject
        {
            ["schema"] = Schema, ["source"] = Source, ["contractVersion"] = Contract,
            ["schemaValidation"] = "F4a's tools/perf-gate/contract-v1.json is not merged at this base; validated against the F4 plan's Decision 8 field list (tools/perf-gate-offline/Report.cs). Validation against F4a's schema lands when F4a merges.",
            ["label"] = "Offline expectations only: .NET Stopwatch time of Core advancement plus the F2a presentation driver double. Not the PC union, not Mono, not presented frames. Never mixed with native windows.",
            ["header"] = Header(inputs),
        };
        var windows = new JArray();
        foreach (var w in inputs.Windows) windows.Add(Window(w));
        if (inputs.Accounting.Count > 0) windows.Add(AccountingWindow(inputs.Accounting));
        report["windows"] = windows;
        report["verdicts"] = VerdictRows(inputs);
        report["overall"] = new JObject { ["verdict"] = NotEstablished, ["reason"] = "source=offline: an offline run gives expectations, never the gate verdict (Decision 8)." };
        report["spikes"] = new JArray(inputs.Windows.SelectMany(w => w.Spikes.Select(s => new JObject
        {
            ["window"] = w.Spec.Id, ["frame"] = s.Frame, ["gameMinute"] = R(s.Minute, 3), ["clock"] = Fixture.Clock(Math.Floor(s.Minute)),
            ["modWorkMs"] = R(s.Ms), ["coreMs"] = R(s.CoreMs), ["driverMs"] = R(s.DriverMs), ["component"] = s.Component,
            ["gcGeneration"] = s.Gc < 0 ? "none" : s.Gc.ToString(CultureInfo.InvariantCulture),
        })));
        report["stalls"] = new JObject { ["rows"] = new JArray(), ["status"] = NotEstablished, ["reason"] = "No presented or whole-frame intervals offline (Decision 7 needs the rig trace)." };
        report["toolCost"] = new JObject
        {
            ["totalSeconds"] = R(inputs.TotalSeconds, 1), ["fixtureSeconds"] = R(inputs.FixtureSeconds, 1), ["discardedWarmUpWindowSeconds"] = R(inputs.DiscardedWarmUpSeconds, 1), ["allocatedMegabytes"] = R(inputs.AllocatedBytes / 1048576.0, 0),
            ["windowSeconds"] = new JObject(inputs.Windows.Select(w => new JProperty(w.Spec.Id, R(w.ToolSeconds, 2)))),
        };
        return report;
    }

    private static JObject Header(Inputs inputs) => new JObject
    {
        ["sourceCommit"] = Git("rev-parse HEAD"),
        ["sourceDirty"] = Git("status --porcelain") is { Length: > 0 } dirty && dirty != "unknown",
        ["persistentCityDllSha256"] = NotApplicable + ": Core and Persistence compiled from source into the tool",
        ["probeDllSha256"] = NotApplicable + ": no F4b probe",
        ["expectedScopeListHash"] = NotApplicable + ": no union scopes offline",
        ["gameBuild"] = NotApplicable,
        ["modSet"] = NotApplicable + ": no game, no RLO",
        ["rigCommit"] = NotApplicable, ["rigOverlay"] = NotApplicable,
        ["machine"] = new JObject
        {
            ["cpu"] = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? RuntimeInformation.ProcessArchitecture.ToString(),
            ["logicalProcessors"] = Environment.ProcessorCount,
            ["gpu"] = NotApplicable, ["ram"] = R(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1073741824.0, 1) + " GiB available to .NET",
            ["osBuild"] = RuntimeInformation.OSDescription, ["gpuDriver"] = NotApplicable, ["displayRefreshHz"] = NotApplicable,
            ["powerPlan"] = "not read offline", ["runtime"] = RuntimeInformation.FrameworkDescription,
            ["configuration"] = Configuration(),
        },
        ["settingsReadBack"] = NotApplicable + ": no presented frames; frame rate is the simulated cadence of each window",
        ["pcConfig"] = new JObject
        {
            ["GameClockFactor"] = new JObject { ["declared"] = R(Pace.DeclaredFactor, 6), ["fastestTraveller"] = Pace.FastestFactor },
            ["nativeSpeed1GameMinutesPerRealSecondAtFactor1"] = R(Pace.NativeSpeed1, 6),
            ["nativeSpeedScaling"] = "assumed linear in the speed (2, 3, 6); G9 measures it",
            ["MaximumPedestrians"] = WindowRunner.Pedestrians, ["MaximumVehicles"] = WindowRunner.Vehicles, ["PhysicalRadiusMetres"] = 120,
            ["PopulationFrameBudget"] = new JObject(new[] { 30, 60, 120 }.Select(fps =>
            {
                var b = new PopulationFrameBudget(1.0 / fps, false);
                return new JProperty(fps + "fps", new JObject { ["units"] = b.Units, ["citizenEvents"] = b.CitizenEvents, ["milliseconds"] = b.Milliseconds });
            }).Append(new JProperty("hiddenSkip", new JObject { ["units"] = 128, ["citizenEvents"] = 128, ["milliseconds"] = 16 }))),
            ["writer"] = CityState.CurrentSchema,
        },
        ["fixtures"] = new JArray(new JObject
        {
            ["name"] = Fixture.Name, ["seed"] = inputs.Seed,
            ["provenance"] = "tests/F5hIntegratedAcceptanceTests.cs ReferenceShape and Prefilled (F5g City, finances on), aged uninterrupted to day 3 06:00; F6 change journal and ordinary visits attached at load (tools/perf-gate-offline/Windows.cs)",
            ["provenanceSha256"] = inputs.Aged.Sha256,
            ["persons"] = inputs.Persons, ["residents"] = inputs.Residents, ["commuters"] = inputs.Commuters, ["pool"] = inputs.Pool,
            ["playerFoodShops"] = 3, ["rivals"] = "none (the F5h fixture has no rival scope; rival service limits are not exercised)",
            ["cars"] = "none (the F5h planner routes walking legs only; vehicle bodies stay at 0)",
        }),
        ["contractVersion"] = Contract,
        ["movementState"] = "offline driver double (tools/movement-equivalence/PresentationDriver.cs: F2c walkers, F2d car follow, F2e followed list)",
        ["activeRigCapabilities"] = new JArray(),
        ["commandLine"] = inputs.CommandLine,
    };

    private static string Configuration()
    {
#if DEBUG
        return "Debug";
#else
        return "Release";
#endif
    }

    private static string Git(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("git", arguments) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false })!;
            string text = process.StandardOutput.ReadToEnd().Trim(); process.WaitForExit();
            return process.ExitCode == 0 ? text : "unknown";
        }
        catch (System.ComponentModel.Win32Exception) { return "unknown"; }
    }

    private static JObject Receipts() => new JObject
    {
        ["autosave"] = NotApplicable + ": no game saves; no save inside a window",
        ["contamination"] = "not sampled offline",
        ["priorityFocusSettings"] = NotApplicable, ["probeCoverage"] = NotApplicable, ["presentationTrace"] = NotApplicable,
        ["valid"] = true,
    };

    private static JObject NotEstablishedValue(string reason) => new JObject { ["value"] = null, ["status"] = NotEstablished, ["reason"] = reason };
    private const string NoPresent = "no presented frames offline";

    private static bool Normal(WindowSpec spec) => spec.Presentation == Presentation.Bodies;

    private static JObject Window(WindowResult w)
    {
        var s = w.Spec;
        double unheldSeconds = s.Presentation == Presentation.TimeMachine ? double.NaN : (s.To - s.From) / s.GameMinutesPerRealSecond;
        double realSeconds = w.Frames / (double)s.Fps;
        var mod = w.ModWorkMs.OrderBy(v => v).ToArray();
        return new JObject
        {
            ["id"] = s.Id, ["condition"] = s.Condition, ["script"] = s.Script, ["arm"] = s.Arm, ["build"] = "candidate (offline)", ["repetition"] = 1,
            ["fps"] = s.Fps, ["clockFactor"] = R(s.ClockFactor, 6), ["nativeSpeed"] = s.NativeSpeed, ["presentation"] = s.Presentation.ToString(),
            ["gameFrom"] = Fixture.Clock(s.From), ["gameTo"] = Fixture.Clock(s.To), ["checkpointSha256"] = w.CheckpointSha256,
            ["frames"] = w.Frames, ["simulatedRealSeconds"] = R(realSeconds, 2),
            ["receipts"] = Receipts(),
            ["M1"] = new JObject { ["p95Ms"] = R(Percentile(mod, .95)), ["measure"] = "offline mod-work proxy: Core frame + presentation driver" },
            ["M2"] = new JObject { ["flags"] = w.Spikes.Count, ["withGc"] = w.Spikes.Count(x => x.Gc >= 0) },
            ["M3"] = new JObject { ["p99Ms"] = R(Percentile(mod, .99)) },
            ["M4"] = new JObject { ["meanMs"] = R(mod.Length == 0 ? 0 : mod.Average()) },
            ["P1"] = NotEstablishedValue(NoPresent), ["P2"] = NotEstablishedValue(NoPresent), ["P3"] = NotEstablishedValue(NoPresent),
            ["reference"] = NotEstablishedValue("no dependency-only arm offline"), ["P4"] = NotEstablishedValue("no matched reference offline"),
            ["S1"] = new JObject { ["historicalClockMisses"] = w.HistoricalClockMisses },
            ["S2"] = new JObject { ["maxLagMinutes"] = R(w.MaxLag, 6) },
            ["S3"] = new JObject { ["heldFrames"] = w.HeldFrames, ["holds"] = w.Holds, ["heldShare"] = R(w.Frames == 0 ? 0 : w.HeldFrames / (double)w.Frames, 5),
                ["longestHoldSeconds"] = R(w.LongestHoldSeconds, 3), ["applies"] = s.NativeSpeed <= 3 && Normal(s),
                ["heldFramesByPreviousFrameStop"] = JObject.FromObject(w.HoldCauses) },
            ["S4"] = new JObject { ["longestRecoverySeconds"] = R(w.LongestRecoverySeconds, 3) },
            ["S5"] = new JObject { ["paceRatio"] = double.IsNaN(unheldSeconds) ? null : R(realSeconds / unheldSeconds, 4),
                ["maxHourPaceRatio"] = w.Hours.Count == 0 || double.IsNaN(unheldSeconds) ? null : R(w.Hours.Max(h => HourRatio(h, s)), 4) },
            ["density"] = new JObject { ["dense"] = NotEstablished, ["reason"] = "offline body counts come from the driver double, not RIG-VIEW",
                ["medianPedestrians"] = w.MedianWalkers, ["medianCars"] = w.MedianCars },
            ["bodies"] = new JObject { ["median"] = w.MedianBodies, ["maxWalkers"] = w.MaxWalkers, ["maxCars"] = w.MaxCars,
                ["acquired"] = w.BodyAcquisitions, ["released"] = w.BodyReleases },
            ["gc"] = new JObject { ["gen0"] = w.Gc0, ["gen1"] = w.Gc1, ["gen2"] = w.Gc2 },
            ["coreFrameMs"] = Stats(w.CoreMs), ["driverMs"] = Stats(w.DriverMs), ["modWorkMs"] = Stats(w.ModWorkMs),
            ["slowestUnit"] = new JObject { ["ms"] = R(w.UnitMax), ["kind"] = w.UnitMaxKind, ["gameMinute"] = R(w.UnitMaxMinute, 3) },
            ["work"] = new JObject { ["units"] = w.Units, ["citizenEvents"] = w.CitizenEvents, ["planningSlices"] = w.PlanningSlices,
                ["orders"] = w.Orders, ["journalObservations"] = w.JournalObservations, ["maxNativePathsInFrame"] = w.MaxPathsInFrame },
            ["hours"] = new JArray(w.Hours.Select(h => new JObject
            {
                ["clock"] = Fixture.Clock(h.Day * 1440.0 + h.Hour * 60), ["gameMinutes"] = R(h.GameMinutes, 3), ["frames"] = h.Frames,
                ["realSeconds"] = R(h.Frames / (double)s.Fps, 3),
                ["paceRatio"] = s.Presentation == Presentation.TimeMachine ? null : R(HourRatio(h, s), 4),
                ["heldSeconds"] = R(h.HeldFrames / (double)s.Fps, 3), ["longestHoldSeconds"] = R(h.LongestHoldFrames / (double)s.Fps, 3),
                ["maxLagMinutes"] = R(h.MaxLag, 6),
            })),
            ["stateHash"] = w.StateHash, ["uninterruptedHash"] = w.UninterruptedHash.Length > 0 ? w.UninterruptedHash : null,
            ["slicedEqualsUninterrupted"] = w.UninterruptedHash.Length > 0 ? w.StateHash == w.UninterruptedHash : null,
        };
    }

    /// <summary>Simulated real time of a game hour over the unheld clock's time for the same game minutes.</summary>
    public static double HourRatio(HourRow h, WindowSpec s)
        => h.GameMinutes <= 0 ? 0 : h.Frames / (double)s.Fps / (h.GameMinutes / s.GameMinutesPerRealSecond);

    private static JObject AccountingWindow(List<(WindowResult From, int Index)> frames)
    {
        var mod = frames.Select(f => f.From.ModWorkMs[f.Index]).ToArray();
        var core = frames.Select(f => f.From.CoreMs[f.Index]).ToArray();
        var sorted = mod.OrderBy(v => v).ToArray();
        var first = frames[0].From;
        var sources = frames.Select(f => f.From.Spec.Id).Distinct().ToArray();
        return new JObject
        {
            ["id"] = "W-accounting/S-stationary/declared/60fps", ["condition"] = "W-accounting", ["script"] = "S-stationary", ["arm"] = "declared",
            ["build"] = "candidate (offline)", ["repetition"] = 1, ["fps"] = first.Spec.Fps,
            ["pooledFrom"] = new JArray(sources), ["definition"] = "raw frames within one game minute either side of a native hour rollover (12:00, 13:00, 18:00, 19:00)",
            ["frames"] = frames.Count, ["receipts"] = Receipts(),
            ["M1"] = new JObject { ["p95Ms"] = R(Percentile(sorted, .95)), ["measure"] = "offline mod-work proxy: Core frame + presentation driver" },
            ["M2"] = new JObject { ["flags"] = mod.Count(v => v >= 30) },
            ["M3"] = new JObject { ["p99Ms"] = R(Percentile(sorted, .99)) },
            ["M4"] = new JObject { ["meanMs"] = R(mod.Average()) },
            ["P1"] = NotEstablishedValue(NoPresent), ["P2"] = NotEstablishedValue(NoPresent), ["P3"] = NotEstablishedValue(NoPresent),
            ["reference"] = NotEstablishedValue("no dependency-only arm offline"), ["P4"] = NotEstablishedValue("no matched reference offline"),
            ["S1"] = NotEstablishedValue("see the pooled source windows"), ["S2"] = NotEstablishedValue("see the pooled source windows"),
            ["S3"] = NotEstablishedValue("see the pooled source windows"), ["S4"] = NotEstablishedValue("see the pooled source windows"),
            ["S5"] = NotEstablishedValue("not a pace window"),
            ["density"] = NotEstablishedValue("see the pooled source windows"), ["bodies"] = NotEstablishedValue("see the pooled source windows"),
            ["gc"] = NotEstablishedValue("collections are counted per source window"),
            ["coreFrameMs"] = Stats(core), ["modWorkMs"] = Stats(mod),
            ["nativeAccounting"] = "Offline has no AfterNativeTick, no native hourly customers and no ownership transfer; these frames carry the Core work at the rollover only.",
        };
    }

    private static JArray VerdictRows(Inputs inputs)
    {
        var declared = inputs.Windows.Where(w => w.Spec.Arm == "declared").ToList();
        var paced = inputs.Windows.Where(w => Normal(w.Spec) && w.Spec.NativeSpeed <= 3).ToList();
        var fastest = inputs.Windows.Where(w => w.Spec.Arm.StartsWith("fastest", StringComparison.Ordinal)).ToList();
        JObject Row(string row, string verdict, string reason, object? worst = null) => new JObject
        { ["row"] = row, ["verdict"] = verdict, ["reason"] = reason, ["worst"] = worst == null ? null : JToken.FromObject(worst), ["scope"] = "offline expectation" };
        double Worst(IEnumerable<WindowResult> ws, Func<double[], double> f) => ws.Select(w => f(w.ModWorkMs.OrderBy(v => v).ToArray())).DefaultIfEmpty(0).Max();
        double m1 = Worst(declared, s => Percentile(s, .95)), m3 = Worst(declared, s => Percentile(s, .99)), m4 = Worst(declared, s => s.Average());
        int flags = inputs.Windows.Sum(w => w.Spikes.Count);
        long misses = inputs.Windows.Sum(w => w.HistoricalClockMisses);
        double lag = inputs.Windows.Max(w => w.MaxLag);
        double heldShare = paced.Select(w => w.HeldFrames / (double)Math.Max(1, w.Frames)).DefaultIfEmpty(0).Max();
        double longestHold = paced.Select(w => w.LongestHoldSeconds).DefaultIfEmpty(0).Max();
        double recovery = paced.Select(w => w.LongestRecoverySeconds).DefaultIfEmpty(0).Max();
        var paceWindows = fastest.Where(w => w.Spec.Presentation != Presentation.TimeMachine).ToList();
        double hourRatio = paceWindows.SelectMany(w => w.Hours.Where(h => h.GameMinutes >= 30).Select(h => HourRatio(h, w.Spec))).DefaultIfEmpty(0).Max();
        var day = fastest.FirstOrDefault(w => w.Spec.To - w.Spec.From >= 1440);
        double dayRatio = day == null ? double.NaN : day.Frames / (double)day.Spec.Fps / ((day.Spec.To - day.Spec.From) / day.Spec.GameMinutesPerRealSecond);
        string Pass(bool ok) => ok ? "pass" : "fail";
        return new JArray
        {
            Row("M1", Pass(m1 < 5), "declared windows, offline mod-work proxy p95 < 5 ms", R(m1)),
            Row("M2", "pass", flags + " frames >= 30 ms flagged (a flag, not a fail by itself); listed under spikes", flags),
            Row("M3", Pass(m3 <= 10), "declared windows, offline proxy p99 <= 10 ms", R(m3)),
            Row("M4", Pass(m4 <= 1.5), "declared windows, offline proxy mean <= 1.5 ms", R(m4)),
            Row("P1", NotEstablished, NoPresent), Row("P2", NotEstablished, NoPresent), Row("P3", NotEstablished, NoPresent),
            Row("P4", NotEstablished, "no matched dependency-only reference offline"),
            Row("S1", Pass(misses == 0), "historicalClockMisses over every window and arm", misses),
            Row("S2", Pass(lag <= AccountingClock.MaximumLagMinutes), "largest lag sample in game minutes", R(lag, 6)),
            Row("S3", Pass(heldShare <= .01 && longestHold <= 3), "speeds 1-3 with bodies: held share <= 1 % and longest hold <= 3 s",
                new { heldShare = R(heldShare, 5), longestHoldSeconds = R(longestHold, 3) }),
            Row("S4", Pass(recovery <= 5), "longest recovery to lag <= 0.1 after a hold, real seconds", R(recovery, 3)),
            day == null ? Row("S5", NotEstablished, "no fastest-traveller day window in this run")
                : Row("S5", Pass(hourRatio <= 2 && dayRatio <= 1.25), "fastest traveller: per game hour <= 2.0x, per day <= 1.25x the unheld clock",
                    new { maxHourRatio = R(hourRatio, 4), dayRatio = R(dayRatio, 4) }),
            Row("L1", NotEstablished, "cold start is native"), Row("L2", NotEstablished, "reload activation is measured on the rig (F7a diagnostics)"),
            Row("L3", NotEstablished, "post-load hold is native"), Row("L4", NotEstablished, "needs the dependency-only skip reference"),
            Row("R1", NotEstablished, "soak is a rig run (G14)"),
        };
    }

    /// <summary>Checks a report against the plan's documented fields. Returns every problem; empty means valid.</summary>
    public static List<string> Validate(JObject report)
    {
        var errors = new List<string>();
        void Need(JObject? o, string path, IEnumerable<string> fields)
        {
            if (o == null) { errors.Add(path + " missing"); return; }
            foreach (var f in fields) if (o[f] == null) errors.Add(path + "." + f + " missing");
        }
        if ((string?)report["schema"] != Schema) errors.Add("schema is not " + Schema);
        if ((string?)report["source"] != Source) errors.Add("source is not offline");
        if ((string?)report["contractVersion"] != Contract) errors.Add("contractVersion is not " + Contract);
        var header = report["header"] as JObject;
        Need(header, "header", HeaderFields);
        if (header != null)
        {
            Need(header["machine"] as JObject, "header.machine", MachineFields);
            foreach (var f in HeaderFields)
                if (header[f] is JValue v && v.Type == JTokenType.String && ((string?)v)!.Length == 0) errors.Add("header." + f + " empty");
            if ((string?)header["contractVersion"] != Contract) errors.Add("header.contractVersion is not " + Contract);
            if (header["fixtures"] is not JArray { Count: > 0 } fixtures) errors.Add("header.fixtures empty");
            else foreach (var fx in fixtures) Need(fx as JObject, "header.fixtures[]", new[] { "name", "provenanceSha256" });
        }
        if (report["windows"] is not JArray { Count: > 0 } windows) errors.Add("windows empty");
        else
            foreach (var w in windows.OfType<JObject>())
            {
                string id = (string?)w["id"] ?? "?";
                Need(w, "windows[" + id + "]", WindowFields);
                Need(w["receipts"] as JObject, "windows[" + id + "].receipts", ReceiptFields);
            }
        if (report["verdicts"] is not JArray verdicts) errors.Add("verdicts missing");
        else
        {
            var rows = verdicts.OfType<JObject>().ToDictionary(v => (string?)v["row"] ?? "", v => v);
            foreach (var row in Rows)
                if (!rows.TryGetValue(row, out var v)) errors.Add("verdict " + row + " missing");
                else if (!Verdicts.Contains((string?)v["verdict"])) errors.Add("verdict " + row + " is not pass, fail or not established");
                else if (((string?)v["reason"] ?? "").Length == 0) errors.Add("verdict " + row + " has no reason");
        }
        if (report["overall"]?["verdict"] == null) errors.Add("overall missing");
        else if ((string?)report["overall"]!["verdict"] == "pass") errors.Add("an offline report cannot pass the gate");
        if (report["spikes"] is not JArray) errors.Add("spikes missing");
        if (report["stalls"] == null) errors.Add("stalls missing");
        return errors;
    }
}
