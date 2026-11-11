using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;

namespace PersistentCity.PerfGateOffline;

/// <summary>Writes the offline run as F4a's <c>perf-gate-export/v1</c> with <c>source=offline</c>, and runs F4a's
/// analyzer (<c>tools/perf-gate/analyze.py</c> against <c>contract-v1.json</c>) to build the <c>perf-gate-report/v1</c>.
/// The analyzer owns every verdict, refusal and the report format; this file only maps offline measurements to export
/// fields. Offline-only detail (per game hour, hold causes, slowest unit, state hashes) rides in each window's
/// <c>offline</c> block, which the analyzer ignores.</summary>
internal static class Export
{
    public const string Format = "perf-gate-export/v1", Source = "offline", NotApplicable = "n/a";
    public const string Advancement = "AdvancePopulation", Driver = "presentation-driver";

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

    /// <summary>Contract arms: Decision 3's declared windows and G9's speed 1-3 segments with bodies are primary; speed 6 with
    /// bodies released and the time-machine skip are report-only (G9 asks M rows in the speed 1-3 segments only).</summary>
    public static string Arm(WindowSpec s) => s.Presentation == Presentation.Bodies && s.NativeSpeed <= 3 ? "primary" : "report-only";

    /// <summary>Condition with its frame rate, so 30, 60 and 120 FPS windows never pool or pair with each other.</summary>
    public static string Condition(WindowSpec s) => s.Condition + "@" + s.Fps.ToString(CultureInfo.InvariantCulture) + "fps";

    public static JObject Build(Inputs inputs)
    {
        var windows = new JArray();
        foreach (var w in inputs.Windows) windows.Add(Window(w));
        if (inputs.Accounting.Count > 0) windows.Add(AccountingWindow(inputs.Accounting));
        var export = new JObject
        {
            ["format"] = Format, ["source"] = Source,
            ["label"] = "Offline expectations only: .NET Stopwatch time of Core advancement plus the F2a presentation driver double. Not the PC union, not Mono, not presented frames. Never mixed with native windows.",
            ["header"] = Header(inputs),
            ["windows"] = windows,
            ["offline"] = new JObject
            {
                ["seed"] = inputs.Seed, ["command_line"] = inputs.CommandLine,
                ["tool_cost"] = new JObject
                {
                    ["total_seconds"] = R(inputs.TotalSeconds, 1), ["fixture_seconds"] = R(inputs.FixtureSeconds, 1),
                    ["discarded_warm_up_window_seconds"] = R(inputs.DiscardedWarmUpSeconds, 1), ["allocated_megabytes"] = R(inputs.AllocatedBytes / 1048576.0, 0),
                    ["window_seconds"] = new JObject(inputs.Windows.Select(w => new JProperty(w.Spec.Id, R(w.ToolSeconds, 2)))),
                },
            },
        };
        var s5 = S5(inputs.Windows);
        if (s5 != null) export["measurements"] = new JObject { ["S5"] = s5 };
        return export;
    }

    private static JObject Header(Inputs inputs) => new JObject
    {
        ["source_commit"] = Git("rev-parse HEAD"),
        ["source_dirty"] = Git("status --porcelain") is { Length: > 0 } dirty && dirty != "unknown",
        // No PersistentCity.dll offline: Core and Persistence are compiled into this tool, so the timed binary is the tool's.
        ["pc_dll_sha256"] = Sha256File(typeof(Export).Assembly.Location),
        ["pc_dll_note"] = "offline: SHA-256 of the PerfGateOffline assembly, which compiles Core and Persistence from source",
        ["probe_dll_sha256"] = NotApplicable, ["expected_scope_list_hash"] = NotApplicable, ["game_build"] = NotApplicable,
        ["mod_set"] = NotApplicable, ["rig_commit"] = NotApplicable, ["rig_overlay"] = NotApplicable,
        ["machine"] = new JObject
        {
            ["cpu"] = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? RuntimeInformation.ProcessArchitecture.ToString(),
            ["logical_processors"] = Environment.ProcessorCount,
            ["gpu"] = "none (offline)", ["ram"] = R(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1073741824.0, 1) + " GiB available to .NET",
            ["os_build"] = RuntimeInformation.OSDescription, ["gpu_driver"] = "none (offline)", ["refresh_hz"] = "none (offline)",
            ["power_plan"] = "not read offline", ["runtime"] = RuntimeInformation.FrameworkDescription, ["configuration"] = Configuration(),
        },
        ["settings_read_back"] = NotApplicable,
        ["pc_config"] = new JObject
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
            ["name"] = Fixture.Name, ["provenance_hash"] = inputs.Aged.Sha256, ["seed"] = inputs.Seed,
            ["provenance"] = "tests/F5hIntegratedAcceptanceTests.cs ReferenceShape and Prefilled (F5g City, finances on), aged uninterrupted to day 3 06:00; F6 change journal and ordinary visits attached at load (tools/perf-gate-offline/Windows.cs)",
            ["persons"] = inputs.Persons, ["residents"] = inputs.Residents, ["commuters"] = inputs.Commuters, ["pool"] = inputs.Pool,
            ["player_food_shops"] = 3, ["rivals"] = "none (the F5h fixture has no rival scope; rival service limits are not exercised)",
            ["cars"] = "none (the F5h planner routes walking legs only; vehicle bodies stay at 0)",
        }),
        ["contract_version"] = "perf-gate/v1",
        ["movement_state"] = "offline driver double (tests/movement-equivalence/PresentationDriver.cs: F2c walkers, F2d car follow, F2e followed list)",
        ["rig_capabilities"] = NotApplicable,
    };

    private static string Sha256File(string path)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

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

    /// <summary>Contract receipts. Offline satisfies autosave (no game save inside a window), PC readiness (the fill has
    /// reached its targets) and accounting diagnostics (read directly). The rest describe the rig session and are n/a, which
    /// the contract does not allow a candidate window: every offline window is invalid as a gate window, so it gives
    /// per-window expectations and never enters a row verdict.</summary>
    private static JObject Receipts() => new JObject
    {
        ["autosave"] = "pass", ["no_dotnet_activity"] = NotApplicable, ["priority_high"] = NotApplicable, ["focused"] = NotApplicable,
        ["settings_read_back"] = NotApplicable, ["probe_coverage"] = NotApplicable, ["probe_flags"] = NotApplicable,
        ["presentation_trace"] = NotApplicable, ["pc_readiness"] = "pass", ["accounting_diagnostics"] = "pass",
    };

    private static JObject Frame(long index, double coreMs, double driverMs, int gc) => new JObject
    {
        // No game frame offline: the whole frame is the measured mod work.
        ["frame"] = index, ["mod_work_ms"] = R(coreMs + driverMs), ["whole_frame_ms"] = R(coreMs + driverMs), ["observer_ms"] = 0.0, ["gc"] = gc,
        ["components"] = new JObject { [Advancement] = R(coreMs), [Driver] = R(driverMs) },
    };

    private static JObject Window(WindowResult w)
    {
        var s = w.Spec;
        double realSeconds = w.Frames / (double)s.Fps;
        double unheldSeconds = s.Presentation == Presentation.TimeMachine ? double.NaN : (s.To - s.From) / s.GameMinutesPerRealSecond;
        var frames = new JArray();
        for (int i = 0; i < w.ModWorkMs.Count; i++) frames.Add(Frame(i, w.CoreMs[i], w.DriverMs[i], i < w.FrameGc.Count ? w.FrameGc[i] : 0));
        var lag = new JArray();
        for (int i = 0; i < w.FrameLag.Count; i++) lag.Add(new JArray(R((i + 1) / (double)s.Fps, 4), R(w.FrameLag[i], 6)));
        return new JObject
        {
            ["id"] = s.Id, ["source"] = Source, ["condition"] = Condition(s), ["script"] = s.Script, ["build"] = "candidate", ["arm"] = Arm(s),
            ["repetition"] = 1, ["native_speed"] = s.NativeSpeed, ["checkpoint_minute"] = s.From, ["game_span_minutes"] = s.To - s.From,
            ["clean_real_seconds"] = R(realSeconds, 3), ["first_in_process"] = false, ["aborted"] = false,
            ["receipts"] = Receipts(), ["frames"] = frames, ["bound_components"] = new JArray(Advancement, Driver),
            ["accounting"] = new JObject
            {
                ["clock_misses"] = w.HistoricalClockMisses, ["lag_samples"] = lag,
                ["holds"] = new JArray(w.HoldSpans.Select(h => new JObject
                {
                    ["start_s"] = R(h.Start / (double)s.Fps, 4), ["end_s"] = R((h.End < 0 ? w.Frames : h.End) / (double)s.Fps, 4),
                    ["carried_in"] = false, ["ongoing"] = h.End < 0,
                })),
            },
            ["density"] = new JObject { ["median_pedestrians"] = w.MedianWalkers, ["median_cars"] = w.MedianCars,
                ["note"] = "offline body counts come from the driver double, not RIG-VIEW" },
            ["bodies"] = new JObject { ["median"] = w.MedianBodies, ["max_walkers"] = w.MaxWalkers, ["max_cars"] = w.MaxCars,
                ["acquired"] = w.BodyAcquisitions, ["released"] = w.BodyReleases },
            ["offline"] = new JObject
            {
                ["condition"] = s.Condition, ["fps"] = s.Fps, ["clock_factor"] = R(s.ClockFactor, 6), ["presentation"] = s.Presentation.ToString(),
                ["game_from"] = Fixture.Clock(s.From), ["game_to"] = Fixture.Clock(s.To), ["checkpoint_sha256"] = w.CheckpointSha256,
                ["pace_ratio"] = double.IsNaN(unheldSeconds) ? null : R(realSeconds / unheldSeconds, 4),
                ["held_frames"] = w.HeldFrames, ["holds"] = w.Holds, ["longest_hold_seconds"] = R(w.LongestHoldSeconds, 3),
                ["longest_recovery_seconds"] = R(w.LongestRecoverySeconds, 3), ["max_lag_minutes"] = R(w.MaxLag, 6),
                ["held_frames_by_previous_frame_stop"] = JObject.FromObject(w.HoldCauses),
                ["gc"] = new JObject { ["gen0"] = w.Gc0, ["gen1"] = w.Gc1, ["gen2"] = w.Gc2 },
                ["spikes"] = new JArray(w.Spikes.Select(x => new JObject
                {
                    ["frame"] = x.Frame - 1, ["game_minute"] = R(x.Minute, 3), ["clock"] = Fixture.Clock(Math.Floor(x.Minute)), ["mod_work_ms"] = R(x.Ms),
                    ["component"] = x.Component, ["gc_generation"] = x.Gc < 0 ? "none" : x.Gc.ToString(CultureInfo.InvariantCulture),
                })),
                ["slowest_unit"] = new JObject { ["ms"] = R(w.UnitMax), ["kind"] = w.UnitMaxKind, ["game_minute"] = R(w.UnitMaxMinute, 3) },
                ["work"] = new JObject { ["units"] = w.Units, ["citizen_events"] = w.CitizenEvents, ["planning_slices"] = w.PlanningSlices,
                    ["orders"] = w.Orders, ["journal_observations"] = w.JournalObservations, ["max_native_paths_in_frame"] = w.MaxPathsInFrame },
                ["hours"] = new JArray(w.Hours.Select(h => new JObject
                {
                    ["clock"] = Fixture.Clock(h.Day * 1440.0 + h.Hour * 60), ["game_minutes"] = R(h.GameMinutes, 3), ["frames"] = h.Frames,
                    ["real_seconds"] = R(h.Frames / (double)s.Fps, 3),
                    ["pace_ratio"] = s.Presentation == Presentation.TimeMachine ? null : R(HourRatio(h, s), 4),
                    ["held_seconds"] = R(h.HeldFrames / (double)s.Fps, 3), ["longest_hold_seconds"] = R(h.LongestHoldFrames / (double)s.Fps, 3),
                    ["max_lag_minutes"] = R(h.MaxLag, 6),
                })),
                ["state_hash"] = w.StateHash, ["uninterrupted_hash"] = w.UninterruptedHash.Length > 0 ? w.UninterruptedHash : null,
                ["sliced_equals_uninterrupted"] = w.UninterruptedHash.Length > 0 ? w.StateHash == w.UninterruptedHash : null,
            },
        };
    }

    /// <summary>Simulated real time of a game hour over the unheld clock's time for the same game minutes.</summary>
    public static double HourRatio(HourRow h, WindowSpec s)
        => h.GameMinutes <= 0 ? 0 : h.Frames / (double)s.Fps / (h.GameMinutes / s.GameMinutesPerRealSecond);

    /// <summary>W-accounting: the pooled rollover frames as one window. No accounting block, so its S rows are not established.</summary>
    private static JObject AccountingWindow(List<(WindowResult From, int Index)> pooled)
    {
        var first = pooled[0].From;
        var frames = new JArray(pooled.Select((f, i) => Frame(i, f.From.CoreMs[f.Index], f.From.DriverMs[f.Index], f.Index < f.From.FrameGc.Count ? f.From.FrameGc[f.Index] : 0)));
        return new JObject
        {
            ["id"] = "W-accounting/S-stationary/declared/" + first.Spec.Fps + "fps", ["source"] = Source,
            ["condition"] = "W-accounting@" + first.Spec.Fps + "fps", ["script"] = "S-stationary", ["build"] = "candidate", ["arm"] = "primary",
            ["repetition"] = 1, ["native_speed"] = 1, ["clean_real_seconds"] = R(pooled.Count / (double)first.Spec.Fps, 3),
            ["first_in_process"] = false, ["aborted"] = false, ["receipts"] = Receipts(), ["frames"] = frames,
            ["bound_components"] = new JArray(Advancement, Driver),
            ["offline"] = new JObject
            {
                ["pooled_from"] = new JArray(pooled.Select(f => f.From.Spec.Id).Distinct()),
                ["definition"] = "raw frames within one game minute either side of a native hour rollover (12:00, 13:00, 18:00, 19:00)",
                ["native_accounting"] = "Offline has no AfterNativeTick, no native hourly customers and no ownership transfer; these frames carry the Core work at the rollover only.",
            },
        };
    }

    /// <summary>G9's fastest-traveller pace for the contract's S5 row: every game hour of at least 30 minutes in the speed
    /// windows with bodies and the day, and the whole day. Null without a day window.</summary>
    private static JObject? S5(List<WindowResult> windows)
    {
        var fastest = windows.Where(w => w.Spec.Arm.StartsWith("fastest", StringComparison.Ordinal)).ToList();
        var day = fastest.FirstOrDefault(w => w.Spec.To - w.Spec.From >= 1440);
        if (day == null) return null;
        var paced = fastest.Where(w => w.Spec.Presentation != Presentation.TimeMachine).ToList();
        return new JObject
        {
            ["hours"] = new JArray(paced.SelectMany(w => w.Hours.Where(h => h.GameMinutes >= 30).Select(h => new JObject
            {
                ["window"] = w.Spec.Id, ["clock"] = Fixture.Clock(h.Day * 1440.0 + h.Hour * 60),
                ["real_s"] = R(h.Frames / (double)w.Spec.Fps, 4), ["unheld_real_s"] = R(h.GameMinutes / w.Spec.GameMinutesPerRealSecond, 4),
            }))),
            ["days"] = new JArray(new JObject
            {
                ["window"] = day.Spec.Id, ["real_s"] = R(day.Frames / (double)day.Spec.Fps, 4),
                ["unheld_real_s"] = R((day.Spec.To - day.Spec.From) / day.Spec.GameMinutesPerRealSecond, 4),
            }),
            ["s1_s2_hold"] = fastest.All(w => w.HistoricalClockMisses == 0 && w.MaxLag <= AccountingClock.MaximumLagMinutes),
        };
    }
}

/// <summary>Runs F4a's analyzer (<c>tools/perf-gate/analyze.py</c>) on an export. Python is <c>$PYTHON</c>, else
/// <c>python3</c> (<c>python</c> on Windows).</summary>
internal static class Analyzer
{
    public static string Directory([CallerFilePath] string here = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "perf-gate"));
    public static string Contract => Path.Combine(Directory(), "contract-v1.json");

    public static (int ExitCode, string Output) Run(string exportPath, string jsonOut, string mdOut)
    {
        string python = Environment.GetEnvironmentVariable("PYTHON") ?? (OperatingSystem.IsWindows() ? "python" : "python3");
        var info = new ProcessStartInfo(python) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var a in new[] { Path.Combine(Directory(), "analyze.py"), exportPath, "--json-out", jsonOut, "--md-out", mdOut }) info.ArgumentList.Add(a);
        using var process = Process.Start(info)!;
        var stderr = process.StandardError.ReadToEndAsync();
        string stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, (stdout + stderr.Result).Trim());
    }

    /// <summary>The contract's required header fields, read from F4a's file so the self-test follows it.</summary>
    public static string[] RequiredHeaderFields()
        => JObject.Parse(File.ReadAllText(Contract))["header"]!["required"]!.Select(t => (string)t!).ToArray();

    public static JObject Analyze(JObject export, string directory, string name, out int exitCode, out string output)
    {
        System.IO.Directory.CreateDirectory(directory);
        string exportPath = Path.Combine(directory, name + ".export.json"), json = Path.Combine(directory, name + ".report.json"), md = Path.Combine(directory, name + ".report.md");
        foreach (var f in new[] { json, md }) if (File.Exists(f)) File.Delete(f);
        File.WriteAllText(exportPath, export.ToString(Newtonsoft.Json.Formatting.None));
        (exitCode, output) = Run(exportPath, json, md);
        return exitCode == 0 && File.Exists(json) ? JObject.Parse(File.ReadAllText(json)) : new JObject();
    }
}
