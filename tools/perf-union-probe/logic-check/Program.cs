using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using PerfUnionProbe;

internal static class Program
{
    private static int failures, checks;

    private static void Check(bool condition, string name)
    {
        checks++;
        if (condition) return;
        failures++;
        Console.WriteLine("FAIL " + name);
    }

    private static int Main(string[] args)
    {
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        string expectHash = null;
        bool perf = false;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--expect-hash" && i + 1 < args.Length) expectHash = args[++i];
            else if (args[i] == "--perf") perf = true;
            else { Console.WriteLine("usage: LogicCheck [--expect-hash HEX] [--perf]"); return 2; }
        }
        Profiles();
        Shim();
        Holds();
        BoundaryClips();
        CounterReset();
        AdvanceRecords();
        UnitRecords();
        Union();
        Inventory(expectHash);
        CoverageLifecycle();
        Capacity();
        HarmonyAdapter();
        PatchWatch();
        ScanCache();
        LoopPhases();
        Console.WriteLine("logic-check: " + (checks - failures) + "/" + checks + " checks passed");
        Console.WriteLine("expected_scope_hash " + ScopeInventory.Hash());
        if (perf) Perf();
        return failures == 0 ? 0 : 1;
    }

    private static void Profiles()
    {
        string[] rig = { "game", "-agent-tests", "-rig-test" };
        const string mac = "/Users/agent", win = "C:\\Users\\agent";
        string macData = mac + "/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac";
        string winData = "C:/Users/agent/AppData/LocalLow/Hovgaard Games/Big Ambitions Agent Tests";
        Check(RigProfile.Isolated(rig, RigProfile.MacProduct, macData, mac), "the Mac Agent Tests profile is isolated");
        Check(RigProfile.Isolated(rig, RigProfile.MacProduct, macData + "/", mac + "/"), "the Mac profile ignores a trailing separator");
        Check(RigProfile.Isolated(rig, RigProfile.WindowsProduct, winData, win), "the Windows Agent Tests profile is isolated");
        Check(RigProfile.Isolated(rig, RigProfile.WindowsProduct, winData.ToUpperInvariant(), win), "the Windows profile compares without case");
        Check(!RigProfile.Isolated(rig, RigProfile.MacProduct, macData.ToUpperInvariant(), mac), "the Mac profile compares with case");
        Check(!RigProfile.Isolated(rig, RigProfile.MacProduct, winData, win), "the Mac product refuses the Windows profile");
        Check(!RigProfile.Isolated(rig, RigProfile.WindowsProduct, macData, mac), "the Windows product refuses the Mac profile");
        Check(!RigProfile.Isolated(rig, "Big Ambitions", mac + "/Library/Application Support/com.hovgaardgames.bigambitions", mac), "the ordinary game is refused");
        Check(!RigProfile.Isolated(rig, RigProfile.MacProduct, mac + "/Library/Application Support/com.hovgaardgames.bigambitions", mac), "the ordinary Mac profile is refused");
        Check(!RigProfile.Isolated(new[] { "game", "-agent-tests" }, RigProfile.MacProduct, macData, mac), "a launch without -rig-test is refused");
        Check(!RigProfile.Isolated(new[] { "game", "-rig-test" }, RigProfile.MacProduct, macData, mac), "a launch without -agent-tests is refused");
        Check(!RigProfile.Isolated(rig, RigProfile.MacProduct, "", mac) && !RigProfile.Isolated(rig, RigProfile.MacProduct, macData, ""), "empty paths are refused");
    }

    private static void Shim()
    {
        const double third = 1.0 / 3;
        float Ordinary(float delta, double factor = third) => PaceShim.Apply(delta, loading: false, timeMachinePresent: true, timeMachineRunning: false, factor);
        Check(Ordinary(0.5f) == 0.5f * (float)third, "shim multiplies an ordinary positive finite delta by the factor");
        Check(Ordinary(float.Epsilon, 1) == float.Epsilon, "shim at factor 1 keeps the delta");
        Check(Ordinary(1f, 0.1) == 0.1f, "shim at the minimum factor");
        Check(PaceShim.Apply(0.5f, true, true, false, third) == 0.5f, "shim leaves a loading tick unchanged");
        Check(PaceShim.Apply(0.5f, false, true, true, third) == 0.5f, "shim leaves a time-machine tick unchanged");
        Check(PaceShim.Apply(0.5f, false, false, false, third) == 0.5f, "shim leaves a tick without a time machine unchanged");
        Check(Ordinary(0f) == 0f, "shim leaves zero unchanged");
        Check(Ordinary(-0.25f) == -0.25f, "shim leaves a negative delta unchanged");
        Check(float.IsPositiveInfinity(Ordinary(float.PositiveInfinity)), "shim leaves +infinity unchanged");
        Check(float.IsNegativeInfinity(Ordinary(float.NegativeInfinity)), "shim leaves -infinity unchanged");
        Check(float.IsNaN(Ordinary(float.NaN)), "shim leaves NaN unchanged");
        foreach (double bad in new[] { 0.05, 1.5, double.NaN, double.PositiveInfinity, 0 })
        {
            bool threw = false;
            try { Ordinary(1f, bad); } catch (ArgumentOutOfRangeException) { threw = true; }
            Check(threw && !PaceShim.ValidFactor(bad), "shim refuses factor " + bad);
        }
    }

    private struct Step { public bool Held, Limited; public Step(bool held, bool limited) { Held = held; Limited = limited; } }

    private sealed class FakeClock
    {
        public long Ordinary, Skip, Clips; public double Withheld;
        public bool Skipping;
        public ClockCounters Read() => new ClockCounters(Ordinary, Skip, Clips, Withheld);
        public bool Tick(Step step)
        {
            if (step.Held) { if (Skipping) Skip++; else Ordinary++; Withheld += 0.02; return true; }
            if (step.Limited) { Clips++; Withheld += 0.004; }
            return step.Limited;
        }
    }

    private static List<TickRecord> Drive(HoldTracker tracker, FakeClock clock, IEnumerable<Step> steps, ref int frame)
    {
        var records = new List<TickRecord>();
        foreach (var step in steps)
        {
            var before = clock.Read();
            bool limited = clock.Tick(step);
            records.Add(tracker.Observe(before, clock.Read(), limited, frame, frame * 100L, frame, frame - 0.5));
            frame++;
        }
        return records;
    }

    private static void Holds()
    {
        var ready = new Step(false, false); var held = new Step(true, true);
        var tracker = new HoldTracker(); var clock = new FakeClock(); int frame = 1;
        Drive(tracker, clock, new[] { ready, held, held }, ref frame);
        Check(tracker.Events.Count == 0, "no events before a window opens");
        tracker.BeginWindow(frame, frame * 100L);
        int first = frame;
        Drive(tracker, clock, new[] { held, ready, ready, held, held, held, ready, held }, ref frame);
        int last = frame - 1;
        tracker.EndWindow(last, last * 100L);
        var e = tracker.Events;
        Check(e.Count == 6, "synthetic sequence gives six events (got " + e.Count + ")");
        if (e.Count == 6)
        {
            Check(e[0].Kind == HoldEventKind.Hold && e[0].CarriedIn && e[0].Frame == first && !e[0].Ongoing, "carried-in hold opens at the window's first frame");
            Check(e[1].Kind == HoldEventKind.Resume && e[1].Frame == first + 1 && !e[1].CarriedIn && !e[1].Ongoing, "resume at the first ready tick");
            Check(e[2].Kind == HoldEventKind.Hold && e[2].Frame == first + 3 && !e[2].CarriedIn, "hold at the next held tick");
            Check(e[3].Kind == HoldEventKind.Resume && e[3].Frame == first + 6, "resume after a three-tick hold");
            Check(e[4].Kind == HoldEventKind.Hold && e[4].Frame == first + 7 && !e[4].Ongoing, "hold in the last tick");
            Check(e[5].Kind == HoldEventKind.Resume && e[5].Ongoing && e[5].Frame == last && !e[5].CarriedIn, "open hold closes at the window's last frame, flagged ongoing");
        }
        Check(tracker.HeldTicks == 5 && tracker.Ticks == 8, "held ticks counted inside the window only");
        var quiet = new HoldTracker(); var quietClock = new FakeClock(); frame = 1;
        quiet.BeginWindow(1, 100); Drive(quiet, quietClock, new[] { ready, held, ready }, ref frame); quiet.EndWindow(3, 300);
        Check(quiet.Events.Count == 2 && quiet.Events.All(x => !x.CarriedIn && !x.Ongoing), "released edges carry no flags");
        var skip = new HoldTracker(); var skipClock = new FakeClock { Skipping = true }; frame = 1;
        skip.BeginWindow(1, 100); var rows = Drive(skip, skipClock, new[] { held }, ref frame); skip.EndWindow(1, 100);
        Check(rows[0].Held && skip.Events.Count == 2 && skip.Events[1].Ongoing, "an economy-skip held tick is a hold");
        skip.BeginWindow(5, 500);
        Check(skip.Events.Count == 1 && skip.Events[0].CarriedIn, "a new window starts with only its carried-in hold");
    }

    private static void BoundaryClips()
    {
        var tracker = new HoldTracker(); var clock = new FakeClock(); int frame = 1;
        tracker.BeginWindow(1, 100);
        var rows = Drive(tracker, clock, new[] { new Step(false, true), new Step(false, true), new Step(false, false) }, ref frame);
        tracker.EndWindow(3, 300);
        Check(rows[0].AccountingLimited && rows[0].BoundaryClip && !rows[0].Held, "AccountingLimited without a held-counter increase is a boundary clip");
        Check(rows[0].WithheldMinutes > 0, "a boundary clip keeps its withheld minutes");
        Check(!rows[2].BoundaryClip && !rows[2].Held, "an unlimited ready tick is neither");
        Check(tracker.Events.Count == 0, "boundary clips create no hold or resume events");
        Check(tracker.BoundaryClipTicks == 2 && tracker.HeldTicks == 0, "boundary clips are counted apart from holds");
        var other = new HoldTracker(); other.BeginWindow(1, 1);
        var c = new ClockCounters(3, 0, 7, 1.5);
        var row = other.Observe(c, c, true, 1, 1, 0, 0);
        Check(!row.Held && row.BoundaryClip && other.Events.Count == 0, "limited without any counter increase is a clip, not a hold");
    }

    private static void CounterReset()
    {
        var tracker = new HoldTracker(); tracker.BeginWindow(1, 1);
        var row = tracker.Observe(new ClockCounters(10, 2, 5, 3), new ClockCounters(0, 0, 0, 0), false, 1, 1, 0, 0);
        Check(row.CountersReset && !row.Held && !row.BoundaryClip && row.WithheldMinutes == 0 && tracker.Events.Count == 0,
            "a clock restart (counters fall) is a reset, not a hold");
    }

    private static void AdvanceRecords()
    {
        foreach (long frequency in new[] { 10_000_000L, 1_000_000_000L, 3_579_545L, 1_000L })
        {
            long thirty = (30 * frequency + 999) / 1000;
            long below = 299 * frequency / 10_000;
            Check(AdvanceCallRule.Record(thirty, frequency), "30.0 ms call recorded at " + frequency + " Hz");
            Check(!AdvanceCallRule.Record(below, frequency), "29.9 ms call not recorded at " + frequency + " Hz");
            if (30 * frequency % 1000 != 0) Check(!AdvanceCallRule.Record(thirty - 1, frequency), "just below 30 ms not recorded at " + frequency + " Hz");
        }
        Check(AdvanceCallRule.Record(300_000, 10_000_000) && !AdvanceCallRule.Record(299_000, 10_000_000), "exact 30.0 / 29.9 ms at 10 MHz QPC");
        Check(!AdvanceCallRule.Record(-1, 10_000_000), "negative duration not recorded");
    }

    private static void UnitRecords()
    {
        var units = new PersistentCity.Core.OptionalStopUnitSeams();
        units.BeginCall();
        units.Seam(0.5); units.EndUnit("citizen", 2.0);
        units.Seam(6.0); units.Seam(1.0); units.EndUnit("traffic", 9.0);
        units.EndUnit("food-continuation", 1.0);
        units.Seam(5.5); units.EndUnit("citizen", 8.0);
        units.EndUnit("snapshot", 0.1);
        units.EndCall();
        var source = ReflectedUnitSeams.Bind(units.GetType());
        Check(source != null, "probe binds the Core per-unit optional-stop recorder by reflection");
        if (source == null) return;
        source.Target = units;
        var record = new AdvanceCallRecord { Frame = 7, DurationTicks = 400_000, OptionalStopMilliseconds = 13.0 };
        AdvanceCallRows.ReadUnits(ref record, source);
        var fields = AdvanceCallRows.Fields(record, 1e-4).ToDictionary(f => f.Key, f => f.Value);
        foreach (string name in new[] { "optional_stop_ms", "slowest_unit", "slowest_unit_ms", "slowest_unit_optional_stop_ms", "unit_optional_stop_max_ms",
                     "unit_optional_stop_p95_ms", "units", AdvanceCallRows.UnitsOverThresholdField })
            Check(fields.ContainsKey(name), "advance call record has " + name);
        Check(Equals(fields["optional_stop_ms"], 13.0), "call-total optional_stop_ms is the whole-call seam delta, unchanged");
        Check(Equals(fields["slowest_unit"], "traffic") && Equals(fields["slowest_unit_ms"], 9.0), "slowest unit is the longest unit, not unknown");
        Check(Equals(fields["slowest_unit_optional_stop_ms"], 7.0), "slowest unit's optional-stop time is its own seam, not the call total");
        Check(Equals(fields["unit_optional_stop_max_ms"], 7.0) && Equals(fields["unit_optional_stop_p95_ms"], 7.0), "unit seam maximum and p95");
        Check(Equals(fields["units"], 5), "unit count");
        var over = (UnitOptionalStop[])fields[AdvanceCallRows.UnitsOverThresholdField];
        Check(over.Length == 2 && over[0].Unit == 1 && over[0].Kind == "traffic" && over[0].OptionalStopMilliseconds == 7.0
            && over[1].Unit == 3 && over[1].Kind == "citizen" && over[1].OptionalStopMilliseconds == 5.5, "units over 5 ms are listed with their own seam");
        var unread = AdvanceCallRows.Fields(new AdvanceCallRecord { OptionalStopMilliseconds = double.NaN }, 1e-4).ToDictionary(f => f.Key, f => f.Value);
        Check(Equals(unread["slowest_unit"], "unknown") && unread["optional_stop_ms"] == null && unread["slowest_unit_optional_stop_ms"] == null
            && unread[AdvanceCallRows.UnitsOverThresholdField] == null, "a call without per-unit data reports unknown and nulls");
        Check(ReflectedUnitSeams.Bind(typeof(AdvanceCallRecord)) == null, "binding refuses a type without the per-unit contract");
        Check(AdvanceCallRule.RecordUnits(5.3) && !AdvanceCallRule.RecordUnits(5.0) && !AdvanceCallRule.RecordUnits(double.NaN),
            "a call under 30 ms is recorded when one unit's optional-stop time is over 5 ms");
        Check(Equals(fields["over_30ms"], false) && Equals(fields["record_reason"], "unit_optional_stop_over_5ms"), "record reason for a sub-30 ms unit crossing");
        var interrupted = new PersistentCity.Core.OptionalStopUnitSeams();
        interrupted.BeginCall();
        interrupted.Seam(6.0); interrupted.EndUnit("citizen", 7.0);
        interrupted.Seam(4.0);
        interrupted.EndCall(false);
        interrupted.Seam(9.0);
        source.Target = interrupted;
        var failed = new AdvanceCallRecord { DurationTicks = 400_000, OptionalStopMilliseconds = 10.0, Over30 = true };
        AdvanceCallRows.ReadUnits(ref failed, source);
        var failedFields = AdvanceCallRows.Fields(failed, 1e-4).ToDictionary(f => f.Key, f => f.Value);
        Check(!failed.UnitsRead && Equals(failedFields["slowest_unit"], "unknown") && failedFields["unit_optional_stop_max_ms"] == null
            && failedFields[AdvanceCallRows.UnitsOverThresholdField] == null, "an interrupted advance exports no per-unit fields");
        Check(Equals(failedFields["optional_stop_ms"], 10.0) && Equals(failedFields["duration_ms"], 40.0), "an interrupted advance keeps its call total and duration");
        Check(interrupted.CallSeamMilliseconds == 10.0, "seams after an interrupted call is closed are not charged to it");
        var stale = new PersistentCity.Core.OptionalStopUnitSeams();
        stale.BeginCall(); stale.Seam(6.0); stale.EndUnit("citizen", 7.0); stale.EndCall();
        source.Target = stale;
        Check(stale.Completed && AdvanceCallRule.AdmitUnits(false, stale.MaximumUnitSeamMilliseconds), "a completed crossing is admitted");
        Check(!AdvanceCallRule.AdmitUnits(true, stale.MaximumUnitSeamMilliseconds),
            "a call that throws before BeginCall is not admitted on the previous call's unit maximum");
        var thrown = new AdvanceCallRecord { DurationTicks = 400_000, OptionalStopMilliseconds = 2.5, Over30 = true };
        if (AdvanceCallRule.ExportUnits(true)) AdvanceCallRows.ReadUnits(ref thrown, source);
        var thrownFields = AdvanceCallRows.Fields(thrown, 1e-4).ToDictionary(f => f.Key, f => f.Value);
        Check(!thrown.UnitsRead && Equals(thrownFields["slowest_unit"], "unknown") && thrownFields["unit_optional_stop_max_ms"] == null
            && Equals(thrownFields["optional_stop_ms"], 2.5) && Equals(thrownFields["duration_ms"], 40.0),
            "a failed 30 ms call keeps duration and call total but not the previous call's unit timings");
        AdvanceBuffer();
    }

    private static void AdvanceBuffer()
    {
        var buffer = new AdvanceCallBuffer(2, 3);
        for (int i = 0; i < 5; i++) buffer.Add(new AdvanceCallRecord { Frame = i, UnitsRead = true });
        Check(buffer.UnitCount == 3 && buffer.UnitDropped == 2 && buffer.SlowCount == 0 && buffer.SlowDropped == 0, "threshold-only records fill only their own buffer");
        Check(buffer.Add(new AdvanceCallRecord { Frame = 10, Over30 = true }) && buffer.Add(new AdvanceCallRecord { Frame = 11, Over30 = true }),
            "30 ms records keep their capacity after the threshold-only buffer is full");
        Check(!buffer.Add(new AdvanceCallRecord { Frame = 12, Over30 = true }) && buffer.SlowDropped == 1 && buffer.UnitDropped == 2, "drops are counted per record type");
        Check(!buffer.Add(new AdvanceCallRecord { Frame = 13 }) && buffer.UnitDropped == 2, "a threshold-only record without unit data is not stored");
        Check(buffer.Full(true) && buffer.Full(false), "both buffers report full");
        buffer.Drop(false);
        Check(buffer.UnitDropped == 3, "explicit drop counts against its own type");
        var export = buffer.Chronological();
        Check(export.Length == 5 && export.Select(r => r.Frame).OrderBy(f => f).SequenceEqual(new[] { 0, 1, 2, 10, 11 }) && buffer.MergedCount == 5,
            "the calls export merges both record types in chronological order");
        var mixed = new AdvanceCallBuffer(4, 4);
        mixed.Add(new AdvanceCallRecord { Frame = 1, StartQpc = 100, Over30 = true });
        mixed.Add(new AdvanceCallRecord { Frame = 2, StartQpc = 200, UnitsRead = true, UnitsOverThreshold = new[] { new UnitOptionalStop { Unit = 3, Kind = "citizen", OptionalStopMilliseconds = 5.3 } } });
        mixed.Add(new AdvanceCallRecord { Frame = 3, StartQpc = 300, Over30 = true });
        var rows = mixed.Chronological().Select(r => AdvanceCallRows.Fields(r, 1e-4).ToDictionary(f => f.Key, f => f.Value)).ToArray();
        Check(rows.Select(r => (int)r["frame"]).SequenceEqual(new[] { 1, 2, 3 }) && Equals(rows[1]["over_30ms"], false)
            && Equals(rows[1]["record_reason"], "unit_optional_stop_over_5ms") && ((UnitOptionalStop[])rows[1][AdvanceCallRows.UnitsOverThresholdField]).Length == 1,
            "the normal calls export includes a sub-30 ms advance with a unit crossing between 30 ms calls");
        var window = new AdvanceCallBuffer(4096, 4096);
        window.Add(new AdvanceCallRecord { Frame = 0, StartQpc = 0, Over30 = true });
        for (int i = 1; i < 128; i++)
            window.Add(new AdvanceCallRecord { Frame = i, StartQpc = i, UnitsRead = true, UnitsOverThreshold = new[] { new UnitOptionalStop { Unit = 1, Kind = "citizen", OptionalStopMilliseconds = 5.5 } } });
        var paged = window.Chronological();
        int RowLength(int i) => paged[i].Over30 ? 330 : 426;
        int offset = 0, pages = 0, budget = 48000 - 4000 - 512;
        var seen = new List<AdvanceCallRecord>();
        while (offset < paged.Length && pages < 10)
        {
            int take = CallPage.Fit(RowLength, offset, Math.Min(paged.Length, offset + 128), budget);
            int used = Enumerable.Range(offset, take).Sum(i => RowLength(i) + CallPage.Separator);
            Check(take > 0 && used <= budget, "page " + pages + " fits the reply budget");
            seen.AddRange(paged.Skip(offset).Take(take));
            offset += take; pages++;
        }
        Check(pages > 1 && seen.Count == 128 && seen.Count(r => r.Over30) == 1 && seen.Count(r => !r.Over30) == 127
            && seen.Select(r => r.Frame).SequenceEqual(Enumerable.Range(0, 128)), "paging a mixed window recovers every slow and threshold-only record in order");
        Check(CallPage.Fit(_ => 100_000, 0, 3, 1000) == 1, "an oversized single row is still returned so paging advances");
        Check(CallPage.Fit(_ => 10, 5, 5, 1000) == 0, "an empty range returns no rows");
        buffer.Reset();
        Check(buffer.SlowCount == 0 && buffer.UnitCount == 0 && buffer.SlowDropped == 0 && buffer.UnitDropped == 0, "reset clears both buffers");
    }

    private static void Union()
    {
        var u = new UnionClock(3, 2, 10_000_000);
        u.Enter(0); u.Enter(10); u.Component(0, 12, 15, u.Depth > 0); u.Exit(1, 10, 20); u.Exit(0, 0, 30);
        Check(u.FrameTicks == 30, "nested scopes count once in the union");
        Check(u.Ticks(0) == 30 && u.Ticks(1) == 10, "each scope keeps its inclusive time");
        Check(u.FrameComponentTicks(0) == 3 && u.FrameTicks == 30, "component time is attribution only, not added to the union");
        u.Enter(40); u.Exit(2, 40, 50);
        Check(u.FrameTicks == 40, "disjoint top-level scopes add");
        u.Exit(2, 60, 61);
        Check(u.Underflows == 1 && u.FrameTicks == 40, "an unmatched exit is counted, never added");
        u.Component(1, 0, 5, u.Depth > 0);
        Check(u.ComponentsOutsideUnion == 1, "a component outside every union scope is counted");
        u.Enter(100); u.Exit(0, 100, 100 + 300_000);
        Check(u.Spikes(0) == 1 && u.MaxTicks(0) == 300_000, "a 30 ms scope counts as a spike");
        u.ResetFrame();
        Check(u.FrameTicks == 0 && u.FrameComponentTicks(0) == 0 && u.Calls(0) == 2, "frame reset keeps lifetime scope totals");
    }

    private static void Inventory(string expectHash)
    {
        var all = ScopeInventory.All.ToArray();
        Check(ScopeInventory.Patches.Count == 50, "50 expected patches (27 named hooks, 23 food-contract)");
        Check(ScopeInventory.Patches.Select(p => p.Owner + "|" + p.Kind + "|" + p.Type + "|" + p.Method).Distinct().Count() == ScopeInventory.Patches.Count, "patch keys are unique");
        Check(ScopeInventory.Owners.All(o => o.StartsWith("hart.persistentcity.") || o.StartsWith("persistentcity.")), "every expected owner is a Persistent City owner");
        Check(!ScopeInventory.Owners.Contains("f4b.perf-union-probe"), "the probe's own owner is outside the inventory");
        Check(ScopeInventory.Components.Any(c => c.Method == "ApplyHoldReplay") && ScopeInventory.Components.Any(c => c.Method == "ObservePlayerContact"), "F2e hold call sites are components");
        Check(ScopeInventory.Patches.Count(p => p.Kind == ScopeKind.Transpiler) == 0 && ScopeInventory.TranspilerHelpers.Count == 0, "no transpilers and no transpiler helpers");
        Check(ScopeInventory.PlayerLoops.Count == 1 && ScopeInventory.PlayerLoops.All(l => l.Kind == ScopeKind.PlayerLoop && l.Target.Length > 0), "one player-loop delegate row naming its marker type");
        Check(ScopeKind.PlayerLoop > ScopeKind.Transpiler && ScopeKind.PlayerLoop > ScopeKind.Finalizer, "player-loop rows are never treated as Harmony patches");
        Check(all.All(s => s.File.Length > 0 && s.Type.Length > 0 && s.Method.Length > 0), "every row names a file, type and method");
        Check(ScopeInventory.Hash() == ScopeInventory.Hash() && ScopeInventory.Hash().Length == 64, "hash is stable");
        if (expectHash != null) Check(ScopeInventory.Hash() == expectHash, "C# canonical hash equals check_scopes.py's (" + expectHash + ")");
    }

    private static ObservedPatch Observed(ExpectedScope row, string target = null, string owner = null, string kind = null) => new ObservedPatch
    { Owner = owner ?? row.Owner, Kind = kind ?? row.Kind.ToString(), Type = row.Type, Method = row.Method, Target = target ?? row.Target, PcAssembly = true };

    private static void CoverageLifecycle()
    {
        const string probe = "f4b.perf-union-probe";
        var patches = ScopeInventory.Patches;
        CoverageTracker Armed() => new CoverageTracker(patches, _ => true, probe);
        var installedLater = patches.Where(p => p.Owner == "hart.persistentcity.physicalvehicles.v1" || p.Owner == "hart.persistentcity.ambientpedestrians.v1").ToArray();
        var early = patches.Where(p => !installedLater.Contains(p) && p.Method != "RidePrefix" && p.Method != "RidePostfix").ToArray();

        var t = Armed();
        t.Observe(early.Select(p => Observed(p)));
        Check(t.Complete && !t.Installed(installedLater[0]), "arming before physical hooks exist is complete; they show as not installed");
        t.Observe(early.Concat(installedLater).Select(p => Observed(p)));
        Check(t.Complete && installedLater.All(t.Installed) && t.UnexpectedAtLastScan == 0, "hooks installed after arming are expected and stay complete");
        t.Observe(early.Select(p => Observed(p)));
        Check(t.Complete, "hooks uninstalled after arming stay complete");

        var extra = Armed();
        extra.Observe(early.Select(p => Observed(p)).Concat(new[] { new ObservedPatch { Owner = "hart.persistentcity.stationarycrowds.v1", Kind = "Postfix",
            Type = "PersistentCity.Runtime.StationaryCrowds", Method = "ExtraPostfix", Target = "StationaryAiSpawner.Spawn", PcAssembly = true } }));
        Check(!extra.Complete && extra.Unexpected.Count == 1, "a PC patch added after arming makes coverage incomplete");
        extra.Observe(early.Select(p => Observed(p)));
        Check(!extra.Complete, "an unexpected patch is never forgotten when it disappears");

        var spawn = patches.Single(p => p.Method == "SpawnPrefix");
        var drift = Armed();
        drift.Observe(new[] { Observed(spawn, target: "PedestrianSpawner.SetActivePedestrians") });
        Check(!drift.Complete, "an expected callback on another native target is unexpected");
        var twice = Armed();
        twice.Observe(new[] { Observed(spawn), Observed(spawn) });
        Check(!twice.Complete, "an expected callback registered twice is unexpected");
        var manipulator = Armed();
        manipulator.Observe(new[] { Observed(spawn, kind: "ILManipulators") });
        Check(!manipulator.Complete, "a PC IL manipulator is unexpected");
        var foreignOwner = Armed();
        foreignOwner.Observe(new[] { Observed(spawn, owner: "someone.else") });
        Check(!foreignOwner.Complete, "a PC-assembly patch under a foreign owner is unexpected");
        var others = Armed();
        others.Observe(new[] { new ObservedPatch { Owner = "someone.else", Kind = "Prefix", Type = "Other.Mod", Method = "Prefix", Target = "GameManager.RunMainGameTick" },
            new ObservedPatch { Owner = probe, Kind = "Prefix", Type = "PerfUnionProbe.ScopePatch", Method = "Before", Target = "NativeCityClock.TickPrefix" } });
        Check(others.Complete, "foreign mods and the probe's own patches are ignored");
        var unbindable = new CoverageTracker(patches, p => p.Method != "SpawnPrefix", probe);
        Check(!unbindable.Complete && unbindable.Unbindable.Count == 1, "an expected callback the probe cannot patch is incomplete");
        var transpilerOnly = new CoverageTracker(patches, p => p.Kind != ScopeKind.Transpiler, probe);
        Check(transpilerOnly.Complete, "transpilers are never scopes, so they cannot be unbindable");
    }

    private static void Capacity()
    {
        long Run(int capacity, long rows, int drainEvery, int page)
        {
            var ring = new RingCursor(capacity);
            long next = 0;
            for (long i = 0; i < rows; i++)
            {
                ring.TryWrite(out _);
                if (drainEvery > 0 && (i + 1) % drainEvery == 0)
                    while (ring.Page(next, page, out int took) && took > 0) next += took;
            }
            return ring.Dropped;
        }
        const int fps = 90;
        long g13 = 20L * 60 * fps;
        Check(g13 <= ProbeCapacity.Frames && Run(ProbeCapacity.Frames, g13, 0, 0) == 0, "a 20-minute G13 window at 90 FPS fits the frame buffer undrained");
        Check(Run(ProbeCapacity.Ticks, g13, 0, 0) == 0, "its ticks (one native tick per frame) fit undrained");
        long tickBytes = (long)ProbeCapacity.Ticks * Unsafe.SizeOf<TickRecord>();
        Check(Unsafe.SizeOf<TickRecord>() == 40 && tickBytes <= 5_300_000,
            "the tick ring holds " + (tickBytes / 1e6).ToString("0.00") + " MB of pointer-free rows (was 262,144 x 48 B = 12.58 MB)");
        Check(Run(ProbeCapacity.Diagnostics, 20 * 60, 0, 0) == 0, "its once-per-second diagnostics fit undrained");
        long soak = 4L * 3600 * fps;
        Check(Run(ProbeCapacity.Frames, soak, 0, 0) > 0, "an undrained 4-hour window reports dropped frames, never silent overwrite");
        Check(Run(ProbeCapacity.Frames, soak, 60 * fps, ProbeCapacity.FramePage) == 0, "a 4-hour window drained once per real minute loses no frame");
        Check(Run(ProbeCapacity.Ticks, soak, 60 * fps, ProbeCapacity.TickPage) == 0, "and no tick");
        Check(Run(ProbeCapacity.Diagnostics, 4 * 3600, 600, ProbeCapacity.DiagnosticPage) == 0, "diagnostics drained every 10 minutes lose no sample");
        var r = new RingCursor(4);
        for (int i = 0; i < 4; i++) r.TryWrite(out _);
        Check(!r.TryWrite(out _) && r.Dropped == 1 && r.Written == 4, "a full ring drops new rows and keeps undrained ones");
        Check(r.Page(0, 2, out int a) && a == 2 && r.Page(0, 2, out _) && r.Page(2, 2, out _) && r.Drained == 2, "paging acknowledges earlier rows; the same page may be read again");
        Check(!r.Page(1, 2, out _) && !r.Page(5, 2, out _), "a drained or future offset is refused");
        Check(r.TryWrite(out int slot) && slot == 0 && r.Written == 5, "drained slots are reused");
        var series = new CollectionSeries();
        series.Note(5, 900, 1);
        Check(series.Retained == 0 && !series.Due(5), "a collection series records nothing before the window begins");
        series.Begin(3);
        series.Note(3, 1000, 10);
        Check(series.Retained == 0 && series.Collections == 0, "a frame without a new collection adds no row");
        series.Note(4, 1100, 11); series.Note(4, 1150, 12);
        series.Note(4, 1180, 12);
        Check(series.Retained == 1 && series.Last == 1100, "a stop read in the same frame without a new collection keeps the last boundary sample");
        series.Note(6, 1300, 12);
        Check(series.Retained == 2 && series.Collections == 3 && series.FrameAt(1) == 12 && series.Last == 1300 && series.Growth == 200,
            "a collection followed by stop in the same frame is recorded at the stop read and counts every collection");
        for (int i = 0; i < CollectionSeries.Limit + 2; i++) series.Note(7 + i, 2000 + i, 20 + i);
        Check(series.Retained == CollectionSeries.Limit && series.Truncated && series.Collections == CollectionSeries.Limit + 5, "a full series counts further collections and reports truncation");
        series.Begin(100);
        Check(series.Retained == 0 && series.Collections == 0 && series.First == -1 && series.Growth == 0, "a new window starts an empty series");
    }

    private static void LoopPhases()
    {
        var names = new[] { "EarlyUpdate", "FixedUpdate", "PreUpdate", "Update", "PreLateUpdate", "PostLateUpdate", "TimeUpdate", "Initialization" };
        var p = new LoopPhaseRecorder();
        p.Configure(names);
        void Frame(int frame, long at, int gc, long[] starts, int gcPhase, double mod = 0, bool diagnostic = false)
        {
            p.Begin(at, gc);
            int collections = gc;
            for (int i = 1; i < starts.Length; i++)
            {
                if (gcPhase == i - 1) collections++;
                if (starts[i] >= 0) p.Stamp(i, at + starts[i], collections);
            }
            if (gcPhase == starts.Length - 1) collections++;
            p.Close(frame, at + 33, collections, mod, diagnostic, false, 1.0);
        }
        var plain = new long[] { 0, 2, 4, 5, 20, 22, 30, 31 };
        Frame(10, 0, 4, plain, -1);
        Check(p.Collections == 0 && p.Retained == 0, "a frame without a collection adds no collection row");
        var gcFrame = new long[] { 0, 2, 4, 5, 45, 47, 55, 56 };
        p.Begin(33, 4);
        int c = 4;
        for (int i = 1; i < gcFrame.Length; i++) { if (i == 4) c++; p.Stamp(i, 33 + gcFrame[i], c); }
        p.Close(11, 33 + 58, c, 2.5, false, false, 1.0);
        Check(p.Collections == 1 && p.Retained == 1 && p.Frame(0) == 11 && p.WholeMs(0) == 58 && p.PreviousWholeMs(0) == 33,
            "a collection frame keeps its whole time and the previous frame's");
        Check(p.PhaseMs(0, 3) == 40 && p.PreviousPhaseMs(0, 3) == 15 && p.GcPhase(0) == 3 && p.LargestExcessPhase(0) == 3 && p.ExcessMs(0) == 25,
            "the phase that ran the collection and the phase that grew are named against the previous frame");
        double sum = 0; for (int i = 0; i < names.Length; i++) sum += p.PhaseMs(0, i);
        Check(Math.Abs(sum - p.WholeMs(0)) < 1e-9, "phases tile the whole frame");
        Check(p.ModWorkMs(0) == 2.5 && p.NextWholeMs(0) == -1, "mod work is kept and the next frame is pending");
        Frame(12, 91, 5, new long[] { 0, -1, 4, 5, 20, 22, 30, 31 }, -1);
        Check(p.NextWholeMs(0) == 33, "the following frame fills the next-frame time");
        p.Begin(124, 5); p.Close(13, 124 + 33, 5, 0, false, false, 1.0);
        Check(p.Retained == 1, "an unstamped phase reads zero and adds no row");
        Frame(14, 157, 5, plain, 6, 0, true);
        Check(p.Retained == 2 && p.Diagnostic(1) && p.GcPhase(1) == 6, "a diagnostic collection frame is flagged, its collection placed in the next frame's time update");
        for (int i = 0; i < LoopPhaseRecorder.Limit + 3; i++) Frame(20 + i, 200 + 40 * i, 6 + i, plain, 3);
        Check(p.Retained == LoopPhaseRecorder.Limit && p.Truncated && p.Collections == LoopPhaseRecorder.Limit + 5, "rows stop at the limit and further collections are counted");
        Check(p.PreviousWholeMs(3) == -1, "a collection frame after a collection frame has no clean previous frame");
        p.Reset();
        var series = new CollectionSeries();
        series.Begin(40);
        Frame(300, 9000, 40, plain, -1);
        p.Begin(9033, 40);
        for (int i = 1; i < 4; i++) p.Stamp(i, 9033 + plain[i], i == 3 ? 41 : 40);
        series.Note(41, 1000, 301);
        p.Close(301, 9033 + 12, 41, 1.5, true, false, 1.0, true);
        Check(series.Collections == 1 && p.Collections == 1 && p.Retained == 1 && p.Frame(0) == series.FrameAt(0) && !p.Truncated,
            "a collection just before stop is in the collection series and the collections page");
        Check(p.Partial(0) && p.Diagnostic(0) && p.WholeMs(0) == 12 && p.GcPhase(0) == 2 && p.NextWholeMs(0) == -1 && p.PhaseMs(0, 4) == 0,
            "the stop frame row is partial, ends at the stop read and names the phase that ran the collection");
        p.Reset(); series.Begin(50);
        Frame(400, 20000, 50, plain, -1);
        p.Begin(20033, 50);
        for (int i = 1; i < 4; i++) p.Stamp(i, 20033 + plain[i], i == 3 ? 51 : 50);
        var stop = new StopRead(51, 401, 2000, 1500, 20033 + 9);
        stop.Close(series, p, 401, true, 0.5, false, 1.0);
        Check(stop.CollectionsSince(50) == 1 && p.RowCollections(0) == 1 && series.Collections == 1
            && stop.WholeMs(20033, 1.0) == p.WholeMs(0) && p.WholeMs(0) == 9,
            "a collection after the stop read, during the status and patch check, reaches none of the frames export, the collections page or the series");
        p.Reset();
        Frame(500, 30000, 5, plain, -1);
        Frame(501, 30033, 5, plain, 0);
        Check(p.Retained == 1 && p.Frame(0) == 501 && p.GcPhase(0) == 0 && p.Gen0Through(0) == 6 && p.RowCollections(0) == 1,
            "a collection in EarlyUpdate before the sampler's frame start sits under the new frame with the cumulative gen0 count the sampler row also carries");
        p.Reset();
        Frame(600, 40000, 5, plain, -1);
        p.Begin(40033, 5);
        p.Stamp(1, 40033 + plain[1], 6); p.Stamp(2, 40033 + plain[2], 6); p.Stamp(3, 40033 + plain[3], 6);
        for (int i = 4; i < plain.Length; i++) p.Stamp(i, 40033 + plain[i], 7);
        p.Close(601, 40033 + 33, 7, 0.5, false, false, 1.0);
        long[] samplerGen0 = { 1, 1 }, samplerThrough = { 6, 7 };
        Check(p.Retained == 1 && p.RowCollections(0) == 2 && p.Gen0From(0) == 6 && p.Gen0Through(0) == 7 && p.GcPhase(0) == 0,
            "one probe row holds the EarlyUpdate collection and the collection after GcFrameBegin in the same Unity frame");
        Check(p.PhaseCollections(0, 0) == 1 && p.PhaseGen0From(0, 0) == 6 && p.PhaseGen0Through(0, 0) == 6
            && p.PhaseCollections(0, 3) == 1 && p.PhaseGen0From(0, 3) == 7 && p.PhaseGen0Through(0, 3) == 7
            && p.PhaseCollections(0, 1) == 0 && p.PhaseCollections(0, 4) == 0,
            "the probe row exports the ordinal range of each phase that ran a collection");
        Check(samplerThrough[0] != p.Gen0Through(0) && p.RowForCollection(samplerThrough[0] - samplerGen0[0] + 1) == 0 && p.RowForCollection(samplerThrough[1]) == 0,
            "both sampler rows, ending at gen0 6 and 7, join the probe row through their collection ordinals where an equality join on gen0_through drops the first");
        Check(p.CollectionPhase(0, samplerThrough[0]) == 0 && p.CollectionPhase(0, samplerThrough[1]) == 3 && p.CollectionPhase(0, 8) == -1 && p.RowForCollection(8) == -1,
            "each sampler collection is attributed to its own phase, EarlyUpdate before GcFrameBegin and Update after it");
        p.Reset();
        Frame(700, 50000, 5, plain, -1);
        Frame(701, 50033, 5, plain, 3);
        Frame(702, 50066, 6, plain, 0);
        long reverseGen0 = 2, reverseThrough = 7;
        Check(p.Retained == 2 && p.Gen0From(0) == 6 && p.Gen0Through(0) == 6 && p.Gen0From(1) == 7 && p.Gen0Through(1) == 7,
            "a collection after GcFrameBegin in one frame and one in EarlyUpdate of the next give two probe rows");
        int firstRow = p.RowForCollection(reverseThrough - reverseGen0 + 1), secondRow = p.RowForCollection(reverseThrough);
        Check(firstRow == 0 && secondRow == 1 && p.CollectionPhase(firstRow, reverseThrough - reverseGen0 + 1) == 3 && p.CollectionPhase(secondRow, reverseThrough) == 0,
            "one sampler row covering gen0 6 to 7 joins both probe rows, each collection through its own row and phase");
        int PhaseRangesHolding(long ordinal)
        {
            int hits = 0;
            for (int r = 0; r < p.Retained; r++)
                for (int ph = 0; ph < p.Phases; ph++)
                    if (p.PhaseCollections(r, ph) > 0 && ordinal >= p.PhaseGen0From(r, ph) && ordinal <= p.PhaseGen0Through(r, ph)) hits++;
            return hits;
        }
        void BoundaryReplay(int closeRead, int openRead)
        {
            p.Reset(); series.Begin(5);
            p.Begin(60000, 5);
            for (int i = 1; i < plain.Length; i++) p.Stamp(i, 60000 + plain[i], 5);
            p.Close(800, 60033, closeRead, 0, false, false, 1.0);
            series.Note(closeRead, 100, 801);
            p.Begin(60033, openRead);
            for (int i = 1; i < plain.Length; i++) p.Stamp(i, 60033 + plain[i], i <= 3 ? 6 : 7);
            p.Close(801, 60066, 7, 0, false, false, 1.0);
            series.Note(7, 200, 802);
        }
        BoundaryReplay(5, 6);
        Check(p.Retained == 1 && p.RowForCollection(6) == -1 && PhaseRangesHolding(6) == 0 && series.Collections == 2,
            "reading gen0 again to open the frame after a collection during boundary bookkeeping leaves the sampler's ordinal 6 in no probe row");
        BoundaryReplay(5, 5);
        Check(p.Retained == 1 && p.RowForCollection(6) == 0 && p.CollectionPhase(0, 6) == 0 && PhaseRangesHolding(6) == 1
            && p.RowForCollection(7) == 0 && p.CollectionPhase(0, 7) == 3 && PhaseRangesHolding(7) == 1 && series.Collections == 2,
            "one gen0 read closes the old frame and opens the new one, so a collection during boundary bookkeeping is in exactly one probe phase range");
        p.Reset(); series.Begin(50);
        Frame(400, 20000, 50, plain, -1);
        p.Begin(20033, 50);
        for (int i = 1; i < 4; i++) p.Stamp(i, 20033 + plain[i], i == 3 ? 51 : 50);
        var stop2 = new StopRead(51, 401, 2000, 1500, 20033 + 9);
        stop2.Close(series, p, 401, true, 0.5, false, 1.0);
        p.Begin(9100, 41); p.Close(302, 9133, 41, 0, false, false, 1.0);
        Check(p.NextWholeMs(0) == -1 && p.Partial(0), "a partial row takes no next frame");
        p.Reset();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) Frame(100 + i, 10000 + 40 * i, 100 + i / 3, plain, i % 3 == 0 ? 3 : -1);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(allocated == 0, "phase stamps and collection rows allocate nothing after configuration (" + allocated + " B, .NET)");
        bool threw = false;
        try { p.Configure(new string[LoopPhaseRecorder.MaxPhases + 1]); } catch (ArgumentOutOfRangeException) { threw = true; }
        Check(threw, "more phases than the recorder holds are refused");
        SubPhases();
    }

    private static void SubPhases()
    {
        var names = new List<string> { "frame-start" };
        foreach (var phase in new[] { "Update", "PreLateUpdate", "PostLateUpdate", "TimeUpdate" })
            for (int k = 0; k < 9; k++) names.Add(phase + ".System" + k);
        names.Add("Initialization");
        var sub = new LoopPhaseRecorder(LoopPhaseRecorder.MaxSubPhases);
        sub.Configure(names.ToArray());
        Check(sub.Phases == 38 && sub.Capacity == LoopPhaseRecorder.MaxSubPhases, "the subsystem recorder holds more phases than the top-level recorder");
        int update = names.IndexOf("Update.System0"), skinned = names.IndexOf("PostLateUpdate.System3"), wait = names.IndexOf("TimeUpdate.System0");
        void Frame(int frame, long at, int gc, double updateMs, double skinnedMs, double waitMs, int gcAt)
        {
            sub.Begin(at, gc);
            long t = at + 5; int collections = gc;
            for (int i = 1; i < names.Count; i++)
            {
                if (i == gcAt) collections++;
                sub.Stamp(i, t, collections);
                t += i == update ? (long)updateMs : i == skinned ? (long)skinnedMs : i == wait ? (long)waitMs : 0;
            }
            sub.Close(frame, t + 1, collections, 0, false, false, 1.0);
        }
        Frame(1, 0, 3, 4, 2, 20, -1);
        Frame(2, 100, 3, 31, 35, 24, update);
        Check(sub.Retained == 1 && sub.GcPhase(0) == update - 1, "the collection is placed in the subsystem stamped before it");
        var top = new int[3];
        int taken = sub.LargestExcessPhases(0, top);
        Check(taken == 3 && top[0] == skinned && top[1] == update && top[2] == wait,
            "subsystems are ranked by growth against the previous frame (" + string.Join(",", top.Select(i => names[i])) + ")");
        Check(sub.PhaseMs(0, skinned) == 35 && sub.PreviousPhaseMs(0, skinned) == 2, "a subsystem keeps its time and the previous frame's");
        double sum = 0; for (int i = 0; i < names.Count; i++) sum += sub.PhaseMs(0, i);
        Check(Math.Abs(sum - sub.WholeMs(0)) < 1e-9, "subsystems tile the whole frame");
        Frame(3, 300, 4, 5, 3, 20, skinned + 1);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 500; i++) { Frame(10 + i, 1000 + 200 * i, 5 + i, 5, 3, 20, i % 5 == 0 ? wait : -1); sub.LargestExcessPhases(0, top); }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(allocated == 0, "subsystem stamps, rows and ranking allocate nothing after configuration (" + allocated + " B, .NET)");
        var cleared = new LoopPhaseRecorder(LoopPhaseRecorder.MaxSubPhases);
        cleared.Configure(names.ToArray());
        cleared.Begin(0, 1); cleared.Close(1, 33, 2, 0, false, false, 1.0);
        Check(cleared.Retained == 1 && cleared.LargestExcessPhases(0, top) == 0, "a first-frame collection has no previous frame to rank against");
        bool threw = false;
        try { _ = new LoopPhaseRecorder(LoopPhaseRecorder.MaxSubPhases + 1); } catch (ArgumentOutOfRangeException) { threw = true; }
        Check(threw, "a recorder above the subsystem capacity is refused");
        SubPhaseLoopOrder();
    }

    private static readonly string[] UnityLoop = { "TimeUpdate", "Initialization", "EarlyUpdate", "FixedUpdate", "PreUpdate", "Update", "PreLateUpdate", "PostLateUpdate" };
    private static readonly string[] Detailed = SubPhasePlan.DetailedParents;

    private static void Walk(SubPhasePlan plan, string[] systems, string[][] children, int early, LoopPhaseRecorder rec, int frame, ref long t, ref int gc, string[] gcAt, string slow, long slowMs)
    {
        int n = systems.Length;
        rec.Begin(t, gc);
        void Run(string key, ref long at, ref int collections)
        {
            if (Array.IndexOf(gcAt, key) >= 0) collections++;
            at += key == slow ? slowMs : 1;
        }
        for (int k = 0; k < n; k++)
        {
            int i = (early + k) % n;
            int root = plan.RootBefore(i);
            if (root >= 0) rec.Stamp(root, t, gc);
            if (children[i] == null) Run(systems[i], ref t, ref gc);
            else
                for (int c = 0; c <= children[i].Length; c++)
                {
                    int phase = plan.Before(i, c);
                    if (phase >= 0) rec.Stamp(phase, t, gc);
                    if (c == children[i].Length || children[i][c] == null) continue;
                    Run(systems[i] + "." + children[i][c], ref t, ref gc);
                }
            if (i == n - 1)
            {
                rec.Stamp(plan.RootBefore(n), t, gc);
                Run(SubPhasePlan.Tail, ref t, ref gc);
            }
        }
        rec.Close(frame, t, gc, 0, false, false, 1.0);
    }

    private static void SubPhaseLoopOrder()
    {
        var children = new[]
        {
            new[] { "WaitForLastPresentationAndUpdateTime" }, new[] { "PlayerUpdateTime", "AsyncUploadTimeSlicedUpdate" },
            new[] { null, "UpdateMainGameViewRect", "ScriptRunDelayedStartupFrame" }, new[] { "ScriptRunBehaviourFixedUpdate", "PhysicsFixedUpdate" },
            new[] { "PhysicsUpdate" }, new[] { "ScriptRunBehaviourUpdate", "ScriptRunDelayedDynamicFrameRate" },
            new[] { "ScriptRunBehaviourLateUpdate" }, new[] { "UpdateAllSkinnedMeshes", "PresentAfterDraw" }
        };
        var plan = SubPhasePlan.Build(UnityLoop, children, 2, Detailed, LoopPhaseRecorder.MaxSubPhases);
        var names = plan.Names.ToList();
        int At(string name) => names.IndexOf(name);
        var expected = new[] { "EarlyUpdate", "FixedUpdate", "PreUpdate.PhysicsUpdate", "Update.ScriptRunBehaviourUpdate", "PreLateUpdate.ScriptRunBehaviourLateUpdate",
            "PostLateUpdate.PresentAfterDraw", SubPhasePlan.Tail, "TimeUpdate.WaitForLastPresentationAndUpdateTime", "Initialization" };
        bool ordered = expected.All(n => At(n) >= 0);
        for (int k = 1; ordered && k < expected.Length; k++) ordered = At(expected[k - 1]) < At(expected[k]);
        Check(ordered && !plan.Truncated && names[0] == "EarlyUpdate" && names[names.Count - 1] == "Initialization"
            && At(SubPhasePlan.Tail) == At("PostLateUpdate.PresentAfterDraw") + 1 && plan.RootBefore(UnityLoop.Length) == At(SubPhasePlan.Tail),
            "subsystem stamps follow the installed loop from EarlyUpdate, name every top-level phase and put the loop tail after PostLateUpdate (" + string.Join(",", names) + ")");
        var rec = new LoopPhaseRecorder(LoopPhaseRecorder.MaxSubPhases);
        rec.Configure(plan.Names);
        long t = 0; int gc = 10;
        Walk(plan, UnityLoop, children, 2, rec, 1, ref t, ref gc, new string[0], null, 0);
        Walk(plan, UnityLoop, children, 2, rec, 2, ref t, ref gc, new[] { "FixedUpdate.PhysicsFixedUpdate" }, "FixedUpdate.PhysicsFixedUpdate", 30);
        Check(rec.Retained == 1 && rec.GcPhase(0) == At("FixedUpdate") && rec.LargestExcessPhase(0) == At("FixedUpdate"),
            "a FixedUpdate collection is charged to FixedUpdate, not frame start (" + names[Math.Max(0, rec.GcPhase(0))] + ")");
        Walk(plan, UnityLoop, children, 2, rec, 3, ref t, ref gc, new string[0], null, 0);
        Walk(plan, UnityLoop, children, 2, rec, 4, ref t, ref gc, new[] { SubPhasePlan.Tail }, SubPhasePlan.Tail, 50);
        Check(rec.Retained == 2 && rec.GcPhase(1) == At(SubPhasePlan.Tail) && rec.LargestExcessPhase(1) == At(SubPhasePlan.Tail),
            "work and a collection between PostLateUpdate and the next TimeUpdate go to the loop tail, not PostLateUpdate.PresentAfterDraw or .unstamped ("
            + names[Math.Max(0, rec.GcPhase(1))] + "," + names[Math.Max(0, rec.LargestExcessPhase(1))] + ")");
        Walk(plan, UnityLoop, children, 2, rec, 5, ref t, ref gc, new string[0], null, 0);
        int from = gc;
        Walk(plan, UnityLoop, children, 2, rec, 6, ref t, ref gc, new[] { "Update.ScriptRunBehaviourUpdate", "PostLateUpdate.PresentAfterDraw" }, null, 0);
        var collected = new int[LoopPhaseRecorder.MaxSubPhases];
        int phases = rec.CollectedPhases(2, collected);
        int update = At("Update.ScriptRunBehaviourUpdate"), present = At("PostLateUpdate.PresentAfterDraw");
        Check(rec.Retained == 3 && rec.RowCollections(2) == 2 && phases == 2 && collected[0] == update && collected[1] == present
            && rec.PhaseGen0From(2, update) == from + 1 && rec.PhaseGen0Through(2, update) == from + 1
            && rec.PhaseGen0From(2, present) == from + 2 && rec.PhaseGen0Through(2, present) == from + 2
            && rec.CollectionPhase(2, from + 2) == present,
            "two collections in one frame keep their own subsystem gen0 ranges");
        var wide = (string[][])children.Clone();
        wide[5] = Enumerable.Range(0, 20).Select(k => "System" + k).ToArray();
        var small = SubPhasePlan.Build(UnityLoop, wide, 2, Detailed, 12);
        var smallNames = small.Names.ToList();
        Check(small.Truncated && smallNames.Count <= 12 && smallNames.Contains("Update" + SubPhasePlan.Unstamped)
            && new[] { "PreLateUpdate", "PostLateUpdate", "TimeUpdate" }.All(p => smallNames.Any(n => n.StartsWith(p + ".", StringComparison.Ordinal)))
            && smallNames.Contains(SubPhasePlan.Tail) && smallNames[smallNames.Count - 1] == "Initialization",
            "children past the cap share a parent-level unstamped bucket and later phases keep their stamps (" + string.Join(",", smallNames) + ")");
        var capped = new LoopPhaseRecorder(LoopPhaseRecorder.MaxSubPhases);
        capped.Configure(small.Names);
        t = 0; gc = 0;
        Walk(small, UnityLoop, wide, 2, capped, 1, ref t, ref gc, new string[0], null, 0);
        Walk(small, UnityLoop, wide, 2, capped, 2, ref t, ref gc, new[] { "Update.System19" }, null, 0);
        Check(capped.Retained == 1 && capped.GcPhase(0) == smallNames.IndexOf("Update" + SubPhasePlan.Unstamped),
            "a collection in a child past the cap lands in its parent's unstamped bucket");
        RootCallbacks();
        UnityLoopWithoutTruncation();
        LargestScopePerFrame();
        ModWorkBySubPhase();
        WalkerStampAcrossRetime();
    }

    private const string Walker = "PersistentCityWalkerBound";

    private static List<string> WalkerAttach(List<string> children)
    {
        var list = new List<string>();
        foreach (var child in children)
        {
            if (child == "AIUpdate" || child == "AIUpdatePostScript") list.Add(Walker);
            list.Add(child);
        }
        return list;
    }

    private static void WalkerStampAcrossRetime()
    {
        string[] systems = { "TimeUpdate", "EarlyUpdate", "PreUpdate", "Update", "PreLateUpdate", "PostLateUpdate" };
        var native = new[]
        {
            new List<string> { "WaitForLastPresentationAndUpdateTime" }, new List<string> { "ProfilerStartFrame" },
            new List<string> { "PhysicsUpdate", "SendMouseEvents", "AIUpdate", "WindUpdate" }, new List<string> { "ScriptRunBehaviourUpdate" },
            new List<string> { "AIUpdatePostScript", "LegacyAnimationUpdate", "ScriptRunBehaviourLateUpdate" }, new List<string> { "PresentAfterDraw" }
        };
        var armed = native.Select(WalkerAttach).ToArray();
        var children = armed.Select(c => c.ToArray()).ToArray();
        const int early = 1;
        var plan = SubPhasePlan.Build(systems, children, early, Detailed, LoopPhaseRecorder.MaxSubPhases);
        var names = plan.Names.ToList();
        int preWalker = names.IndexOf("PreUpdate." + Walker), ai = names.IndexOf("PreUpdate.AIUpdate");
        int lateWalker = names.IndexOf("PreLateUpdate." + Walker), post = names.IndexOf("PreLateUpdate.AIUpdatePostScript");
        Check(preWalker >= 0 && ai >= 0 && lateWalker >= 0 && post >= 0 && !plan.Truncated,
            "the production detailed parents stamp both walker entries and the native AI updates after them (" + string.Join(",", names) + ")");
        var stamped = new List<string>();
        for (int o = 0; o < systems.Length; o++)
        {
            int i = (early + o) % systems.Length;
            for (int c = 0; c <= children[i].Length; c++)
            {
                int phase = plan.Before(i, c);
                if (phase >= 0) stamped.Add("#" + phase);
                if (c < children[i].Length) stamped.Add(systems[i] + "." + children[i][c]);
            }
        }
        string preEntry = "PreUpdate." + Walker, lateEntry = "PreLateUpdate." + Walker;
        var reinserted = new List<string>();
        foreach (var entry in stamped)
            if (entry != preEntry && entry != lateEntry) reinserted.Add(entry);
        for (int k = reinserted.Count - 1; k >= 0; k--)
            if (reinserted[k] == "PreUpdate.AIUpdate") reinserted.Insert(k, preEntry);
            else if (reinserted[k] == "PreLateUpdate.AIUpdatePostScript") reinserted.Insert(k, lateEntry);
        (double PreMod, double AiMod, double LateMod, double PostMod, int[] Gc) Measure(List<string> layout)
        {
            var rec = new LoopPhaseRecorder(LoopPhaseRecorder.MaxSubPhases);
            rec.Configure(plan.Names);
            long t = 0, mod = 0; int gc = 0;
            var collected = new List<int>();
            string[] gcIn = { null, preEntry, null, null, null, "PreUpdate.AIUpdate", null, lateEntry };
            bool[] attached = { true, true, false, false, true, true, true, true };
            for (int frame = 0; frame < gcIn.Length; frame++)
            {
                rec.Begin(t, gc, mod);
                foreach (var entry in layout)
                {
                    if (entry.StartsWith("#", StringComparison.Ordinal)) { rec.Stamp(int.Parse(entry.Substring(1)), t, gc, mod); continue; }
                    t += 1;
                    if (entry == preEntry && attached[frame]) { t += 2; mod += 2; }
                    if (entry == lateEntry && attached[frame]) { t += 3; mod += 3; }
                    if (entry == "PreUpdate.AIUpdate") t += 5;
                    if (entry == gcIn[frame]) gc++;
                }
                t += 2;
                rec.Close(frame, t, gc, mod, false, false, 1.0, false, mod);
            }
            for (int r = 0; r < rec.Retained; r++) collected.Add(rec.GcPhase(r));
            int last = rec.Retained - 1;
            return last < 0 ? (-1, -1, -1, -1, collected.ToArray())
                : (rec.PhaseModMs(last, preWalker), rec.PhaseModMs(last, ai), rec.PhaseModMs(last, lateWalker), rec.PhaseModMs(last, post), collected.ToArray());
        }
        var kept = Measure(stamped);
        var moved = Measure(reinserted);
        Check(reinserted.IndexOf(preEntry) > reinserted.IndexOf("#" + ai) && reinserted.IndexOf(lateEntry) > reinserted.IndexOf("#" + post),
            "removing and reinserting both walker entries after arming puts each behind its native AI stamp");
        Check(moved.PreMod == 0 && moved.AiMod == 2 && moved.LateMod == 0 && moved.PostMod == 3 && moved.Gc.SequenceEqual(new[] { ai, ai, post }),
            "with remove-and-reinsert, both walkers' mod time and collections are charged to the native AI updates (" + moved.PreMod + "," + moved.AiMod + "," + moved.LateMod + "," + moved.PostMod + "; " + string.Join(",", moved.Gc.Select(g => names[g])) + ")");
        Check(kept.PreMod == 2 && kept.AiMod == 0 && kept.LateMod == 3 && kept.PostMod == 0 && kept.Gc.SequenceEqual(new[] { preWalker, ai, lateWalker }),
            "with both walker entries kept across detach and reattach, PreUpdate and PreLateUpdate walker mod time and collections stay on their own stamps and native AIUpdate keeps its own collection ("
            + kept.PreMod + "," + kept.AiMod + "," + kept.LateMod + "," + kept.PostMod + "; " + string.Join(",", kept.Gc.Select(g => g >= 0 ? names[g] : "unknown")) + ")");
    }

    private static readonly string[][] Unity2022Children =
    {
        new[] { "WaitForLastPresentationAndUpdateTime" },
        new[] { "ProfilerStartFrame", "UpdateCameraMotionVectors", "DirectorSampleTime", "AsyncUploadTimeSlicedUpdate", "SynchronizeInputs", "SynchronizeState", "XREarlyUpdate" },
        Enumerable.Range(0, 33).Select(k => "EarlySystem" + k).ToArray(),
        new[] { "ClearLines", "DirectorFixedSampleTime", "AudioFixedUpdate", "ScriptRunBehaviourFixedUpdate", "DirectorFixedUpdate", "LegacyFixedAnimationUpdate", "XRFixedUpdate", "PhysicsFixedUpdate", "Physics2DFixedUpdate", "PhysicsClothFixedUpdate", "DirectorFixedUpdatePostPhysics", "ScriptRunDelayedFixedFrameRate" },
        new[] { "PhysicsUpdate", "Physics2DUpdate", "PhysicsClothUpdate", "CheckTexFieldInput", "IMGUISendQueuedEvents", "SendMouseEvents", "PersistentCityWalkerBound", "AIUpdate", "WindUpdate", "UpdateVideo", "NewInputUpdate" },
        new[] { "ScriptRunBehaviourUpdate", "ScriptRunDelayedDynamicFrameRate", "ScriptRunDelayedTasks", "DirectorUpdate", "RigInput" },
        new[] { "PersistentCityWalkerBound", "AIUpdatePostScript", "DirectorUpdateAnimationBegin", "LegacyAnimationUpdate", "DirectorUpdateAnimationEnd", "DirectorDeferredEvaluate", "UIElementsUpdatePanels",
            "EndGraphicsJobsAfterScriptUpdate", "ConstraintManagerUpdate", "ParticleSystemBeginUpdateAll", "Physics2DLateUpdate", "PhysicsLateUpdate", "ScriptRunBehaviourLateUpdate",
            "RigMouseLate" },
        Enumerable.Range(0, 44).Select(k => "PostSystem" + k).Concat(new[] { "PresentAfterDraw" }).ToArray()
    };

    private static void UnityLoopWithoutTruncation()
    {
        var plan = SubPhasePlan.Build(UnityLoop, Unity2022Children, 2, Detailed, LoopPhaseRecorder.MaxSubPhases);
        var names = plan.Names.ToList();
        Check(!plan.Truncated && names.Count <= LoopPhaseRecorder.MaxSubPhases && !names.Any(n => n.EndsWith(SubPhasePlan.Unstamped, StringComparison.Ordinal))
            && names.Contains("TimeUpdate.WaitForLastPresentationAndUpdateTime") && names.Contains("PostLateUpdate.PresentAfterDraw")
            && names.Contains("PreLateUpdate.PersistentCityWalkerBound") && names.Contains("PreUpdate.PersistentCityWalkerBound") && names.Contains("PreUpdate.AIUpdate"),
            "a Unity 2022 loop with 45 PostLateUpdate children stamps every child, TimeUpdate's wait included, with no unstamped bucket (" + names.Count + " of "
            + LoopPhaseRecorder.MaxSubPhases + ")");
        var old = SubPhasePlan.Build(UnityLoop, Unity2022Children, 2, Detailed, 64);
        Check(old.Truncated && old.Names.Contains("TimeUpdate" + SubPhasePlan.Unstamped),
            "the same loop at the earlier 64-stamp cap put TimeUpdate's wait in TimeUpdate.unstamped, as rig 16x read");
        var gameLoop = Unity2022Children.Select(c => c.ToArray()).ToArray();
        gameLoop[4] = gameLoop[4].Concat(Enumerable.Range(0, 12).Select(k => "GamePreUpdate" + k)).ToArray();
        gameLoop[6] = gameLoop[6].Concat(Enumerable.Range(0, 16).Select(k => "GameLateSystem" + k)).ToArray();
        gameLoop[7] = gameLoop[7].Take(44).Concat(Enumerable.Range(0, 40).Select(k => "GamePostSystem" + k)).Concat(new[] { "PresentAfterDraw" }).ToArray();
        var capped = SubPhasePlan.Build(UnityLoop, gameLoop, 2, Detailed, 128);
        Check(capped.Truncated && capped.Names.Length <= 128 && capped.Names.Contains("TimeUpdate" + SubPhasePlan.Unstamped),
            "a game loop with 85 PostLateUpdate children overflows the 128-stamp cap and falls back to TimeUpdate.unstamped, as rig 16y read in all four windows");
        var wide = SubPhasePlan.Build(UnityLoop, gameLoop, 2, Detailed, LoopPhaseRecorder.MaxSubPhases);
        var wideNames = wide.Names.ToList();
        Check(!wide.Truncated && wide.Names.Length > 128 && wide.Names.Length < LoopPhaseRecorder.MaxSubPhases
            && !wideNames.Any(n => n.EndsWith(SubPhasePlan.Unstamped, StringComparison.Ordinal))
            && wideNames.Contains("TimeUpdate.WaitForLastPresentationAndUpdateTime") && wideNames.Contains("PostLateUpdate.GamePostSystem39"),
            "the same game loop under the " + LoopPhaseRecorder.MaxSubPhases + "-stamp ceiling stamps every child, TimeUpdate's wait included (" + wide.Names.Length + " stamps)");
        var sized = new LoopPhaseRecorder(wide.Names.Length);
        sized.Configure(wide.Names);
        Check(sized.Capacity == wide.Names.Length && sized.Phases == wide.Names.Length, "the subsystem recorder is allocated to the installed plan, not the ceiling");
    }

    private static void LargestScopePerFrame()
    {
        var union = new UnionClock(3, 0, 1000);
        union.Enter(0); union.Exit(0, 0, 5);
        union.Enter(10); union.Enter(11); union.Exit(1, 11, 300); union.Exit(2, 10, 301);
        Check(union.FrameLargestScope == 2 && union.FrameLargestScopeTicks == 291 && union.FrameTicks == 296,
            "the union names the frame's longest scope (outer 291 ticks over inner 289) next to the frame's union of 296 ticks");
        union.ResetFrame();
        Check(union.FrameLargestScope == -1 && union.FrameLargestScopeTicks == 0, "a new frame starts with no largest scope");
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) { union.Enter(i); union.Exit(i % 3, i, i + 1); union.ResetFrame(); }
        Check(GC.GetAllocatedBytesForCurrentThread() == before, "tracking the largest scope allocates nothing per frame");
    }

    private static void ModWorkBySubPhase()
    {
        string[] names = { "EarlyUpdate", "FixedUpdate", "Update.ScriptRunBehaviourUpdate", "PreLateUpdate.ScriptRunBehaviourLateUpdate", "PostLateUpdate.PresentAfterDraw",
            SubPhasePlan.Tail, "TimeUpdate.WaitForLastPresentationAndUpdateTime" };
        var rec = new LoopPhaseRecorder(LoopPhaseRecorder.MaxSubPhases);
        rec.Configure(names);
        int update = 2, late = 3, wait = 6;
        void Frame(int frame, long at, int gc, long updateMs, long updateModMs, long lateModMs, long waitMs, int gcIn)
        {
            long mod = 0, t = at; int c = gc;
            rec.Begin(t, c, mod);
            for (int i = 1; i < names.Length; i++)
            {
                t += i == update + 1 ? updateMs : i == wait ? 2 : 1;
                if (i == update + 1) mod += updateModMs;
                if (i == late + 1) { t += lateModMs; mod += lateModMs; }
                if (i - 1 == gcIn) c++;
                rec.Stamp(i, t, c, mod);
            }
            t += waitMs;
            if (gcIn == wait) c++;
            rec.Close(frame, t, c, mod, false, false, 1.0, false, mod);
        }
        Frame(1, 0, 0, 6, 4, 1, 10, -1);
        Frame(2, 100, 0, 90, 5, 1, 10, update);
        Check(rec.Retained == 1 && rec.PhaseModMs(0, update) == 5 && rec.PreviousPhaseModMs(0, update) == 4 && rec.PhaseModMs(0, late) == 1
            && Math.Abs(rec.ExcessOutsideModMs(0, update) - 83) < 1e-9 && rec.LargestExcessOutsideMod(0) == update && rec.ModMs(0) == 6,
            "a 90 ms ScriptRunBehaviourUpdate with 5 ms of mod union is 83 ms of excess outside mod work, charged to that subsystem");
        Frame(3, 300, 1, 6, 4, 1, 10, -1);
        Frame(4, 400, 1, 6, 4, 1, 45, wait);
        Check(rec.Retained == 2 && rec.GcPhase(1) == wait && rec.PhaseModMs(1, wait) == 0 && Math.Abs(rec.ExcessOutsideModMs(1, wait) - 35) < 1e-9
            && rec.LargestExcessOutsideMod(1) == wait,
            "a collection finishing in TimeUpdate's wait carries no mod union and its 35 ms excess is outside mod work");
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 500; i++) { Frame(10 + i, 1000 + 200 * i, 2 + i, 6, 4, 1, 10, i % 5 == 0 ? update : -1); rec.LargestExcessOutsideMod(0); rec.ExcessOutsideModMs(0, update); }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(allocated == 0, "per-subsystem mod stamps and the outside-mod ranking allocate nothing after configuration (" + allocated + " B, .NET)");
        var legacy = new LoopPhaseRecorder(LoopPhaseRecorder.MaxSubPhases);
        legacy.Configure(names);
        legacy.Begin(0, 0); for (int i = 1; i < names.Length; i++) legacy.Stamp(i, i, 0); legacy.Close(1, 10, 0, 0, false, false, 1.0);
        legacy.Begin(10, 0); for (int i = 1; i < names.Length; i++) legacy.Stamp(i, 10 + i, i == 3 ? 1 : 0); legacy.Close(2, 20, 1, 0, false, false, 1.0);
        Check(legacy.Retained == 1 && legacy.ModMs(0) == 0, "callers without mod ticks read zero mod time per subsystem");
    }

    private static void RootCallbacks()
    {
        var systems = new[] { "TimeUpdate", "Initialization", "ModBeforeEarly", "EarlyUpdate", "FixedUpdate", "PreUpdate", "Update", "ModBetween", "PreLateUpdate", "PostLateUpdate", "ModAfterLast" };
        var children = new[]
        {
            new[] { "WaitForLastPresentationAndUpdateTime" }, new[] { "PlayerUpdateTime" }, null,
            new[] { null, "UpdateMainGameViewRect" }, new[] { "PhysicsFixedUpdate" }, new[] { "PhysicsUpdate" },
            new[] { "ScriptRunBehaviourUpdate" }, null, new[] { "ScriptRunBehaviourLateUpdate" }, new[] { "UpdateAllSkinnedMeshes", "PresentAfterDraw" }, null
        };
        const int early = 3;
        var plan = SubPhasePlan.Build(systems, children, early, Detailed, LoopPhaseRecorder.MaxSubPhases);
        var names = plan.Names.ToList();
        int At(string name) => names.IndexOf(name);
        var expected = new[] { "EarlyUpdate", "Update.ScriptRunBehaviourUpdate", "ModBetween", "PreLateUpdate.ScriptRunBehaviourLateUpdate",
            "PostLateUpdate.PresentAfterDraw", "ModAfterLast", SubPhasePlan.Tail, "TimeUpdate.WaitForLastPresentationAndUpdateTime", "Initialization", "ModBeforeEarly" };
        bool ordered = expected.All(n => At(n) >= 0);
        for (int k = 1; ordered && k < expected.Length; k++) ordered = At(expected[k - 1]) < At(expected[k]);
        Check(ordered && names[names.Count - 1] == "ModBeforeEarly" && plan.RootBefore(2) == At("ModBeforeEarly") && plan.RootBefore(7) == At("ModBetween")
            && plan.RootBefore(10) == At("ModAfterLast") && plan.RootBefore(systems.Length) == At(SubPhasePlan.Tail),
            "top-level callbacks without children get their own stamp in loop order (" + string.Join(",", names) + ")");
        var rec = new LoopPhaseRecorder(LoopPhaseRecorder.MaxSubPhases);
        rec.Configure(plan.Names);
        long t = 0; int gc = 0, row = 0;
        foreach (var key in new[] { "ModBetween", "ModAfterLast", "ModBeforeEarly", SubPhasePlan.Tail })
        {
            Walk(plan, systems, children, early, rec, 2 * row + 1, ref t, ref gc, new string[0], null, 0);
            Walk(plan, systems, children, early, rec, 2 * row + 2, ref t, ref gc, new[] { key }, key, 50);
            Check(rec.Retained == row + 1 && rec.GcPhase(row) == At(key) && rec.LargestExcessPhase(row) == At(key),
                "a 50 ms collection in " + key + " is charged to " + key + " (" + names[Math.Max(0, rec.GcPhase(row))] + "," + names[Math.Max(0, rec.LargestExcessPhase(row))] + ")");
            row++;
        }
    }

    private const string PinnedHarmonySha256 = "7b9e756306fa3d7620e02a857c8927a6ab04973f9bd8a77d3866700a6deac55c";
    private static void PatchBody() { }
    private static readonly MethodInfo PatchMethod = typeof(Program).GetMethod(nameof(PatchBody), BindingFlags.NonPublic | BindingFlags.Static);
    private static HarmonyLib.Patch NewPatch(string owner) => new HarmonyLib.Patch(PatchMethod, 0, owner, 400, Array.Empty<string>(), Array.Empty<string>(), false);
    private static HarmonyLib.Patches NewPatches(params HarmonyLib.Patch[][] lists) => new HarmonyLib.Patches(lists[0], lists[1], lists[2], lists[3], lists[4], lists[5]);

    private static void HarmonyAdapter()
    {
        string path = typeof(HarmonyLib.Patches).Assembly.Location;
        using (var sha = System.Security.Cryptography.SHA256.Create())
            Check(string.Concat(sha.ComputeHash(System.IO.File.ReadAllBytes(path)).Select(b => b.ToString("x2"))) == PinnedHarmonySha256, "logic-check loads the pinned 0Harmony.dll");
        var names = HarmonyScan.PatchLists.Select(f => f.Name).ToArray();
        Check(new[] { "Prefixes", "Postfixes", "Transpilers", "Finalizers", "InnerPrefixes", "InnerPostfixes" }.All(names.Contains) && names.Length == 6,
            "HarmonyScan reads all six patch-list fields (" + string.Join(",", names) + ")");
        Check(typeof(HarmonyLib.Patches).GetProperties(BindingFlags.Public | BindingFlags.Instance).All(p => !typeof(IEnumerable<HarmonyLib.Patch>).IsAssignableFrom(p.PropertyType)),
            "no patch list is a property, so a property-only reader would see nothing");
        var lists = Enumerable.Range(0, 6).Select(i => new[] { NewPatch("owner" + i) }).ToArray();
        var info = NewPatches(lists);
        var observed = new List<ObservedPatch>();
        HarmonyScan.Observe(PatchMethod, info, typeof(Program).Assembly, observed);
        Check(observed.Count == 6 && observed.All(o => o.PcAssembly && o.Method == nameof(PatchBody) && o.Target == "Program.PatchBody"), "every patch in every list is observed");
        bool kindsMatch = HarmonyScan.PatchLists.All(field =>
            ((IEnumerable<HarmonyLib.Patch>)field.GetValue(info)).All(p => observed.Any(o => o.Owner == p.owner && o.Kind == HarmonyScan.Kind(field.Name))));
        Check(kindsMatch && observed.Count(o => o.Kind == "Prefix") == 1 && observed.Count(o => o.Kind == "InnerPrefixes") == 1, "each observation carries its list's kind");
        var tracker = new CoverageTracker(ScopeInventory.Patches, _ => true, "f4b.perf-union-probe");
        var inner = new List<ObservedPatch>();
        HarmonyScan.Observe(PatchMethod, NewPatches(new HarmonyLib.Patch[0], new HarmonyLib.Patch[0], new HarmonyLib.Patch[0], new HarmonyLib.Patch[0],
            new[] { NewPatch("someone.else") }, new HarmonyLib.Patch[0]), typeof(Program).Assembly, inner);
        tracker.Observe(inner);
        Check(!tracker.Complete && tracker.UnexpectedAtLastScan == 1, "a PC patch in InnerPrefixes read through HarmonyScan makes coverage incomplete");
        var foreign = new CoverageTracker(ScopeInventory.Patches, _ => true, "f4b.perf-union-probe");
        var other = new List<ObservedPatch>();
        HarmonyScan.Observe(PatchMethod, NewPatches(new HarmonyLib.Patch[0], new HarmonyLib.Patch[0], new HarmonyLib.Patch[0], new HarmonyLib.Patch[0],
            new[] { NewPatch("someone.else") }, new HarmonyLib.Patch[0]), typeof(string).Assembly, other);
        foreign.Observe(other);
        Check(foreign.Complete, "a foreign owner outside the PC assembly is ignored");
    }

    private static void PatchWatch()
    {
        Check(PatchStateWatch.Shared() != null, "the pinned Harmony exposes its shared patch state to the watch");
        var methods = typeof(string).GetMethods().Take(600).Cast<MethodBase>().ToArray();
        var state = new Dictionary<MethodBase, byte[]>();
        foreach (var m in methods) state[m] = new byte[16];
        var watch = new PatchStateWatch(state);
        watch.Capture();
        Check(!watch.Changed(), "an unchanged patch state needs no rescan");
        state[methods[7]] = new byte[16];
        Check(watch.Changed(), "a re-serialized patch info on a known method is a change");
        watch.Capture();
        Check(!watch.Changed(), "the change is consumed by the next full scan");
        state.Remove(methods[3]);
        Check(watch.Changed(), "a method whose patches all went away is a change");
        watch.Capture();
        state[methods[3]] = new byte[16];
        Check(watch.Changed(), "a newly patched method is a change");
        watch.Capture();
        state.Remove(methods[5]); state[typeof(int).GetMethods()[0]] = new byte[16];
        Check(watch.Changed(), "one method removed and another added at the same count is a change");
        watch.Capture();
        int attempts = 0, published = 0;
        bool failNext = false;
        void Attempt() { attempts++; if (failNext) { failNext = false; throw new InvalidOperationException("scan failed"); } published++; }
        bool Periodic() { try { return PatchStateWatch.Run(watch, false, Attempt); } catch (InvalidOperationException) { return true; } }
        Check(!Periodic() && attempts == 0, "an unchanged periodic check runs no scan");
        failNext = true;
        try { PatchStateWatch.Run(watch, true, Attempt); } catch (InvalidOperationException) { }
        Check(attempts == 1 && published == 0 && watch.Pending, "a forced start scan that fails against an unchanged snapshot stays pending");
        Check(Periodic() && attempts == 2 && published == 1 && !watch.Pending, "the next periodic check retries it with identical Harmony state");
        Check(!Periodic() && attempts == 2, "after publishing, an unchanged check runs no scan");
        state[methods[9]] = new byte[16];
        failNext = true;
        Periodic();
        Check(attempts == 3 && published == 1 && watch.Pending, "a periodic scan that fails after a patch change stays pending");
        Check(Periodic() && attempts == 4 && published == 2, "and is retried on the next periodic check");
        state[methods[11]] = new byte[16];
        PatchStateWatch.Run(watch, false, () => { Attempt(); state[methods[12]] = new byte[16]; });
        Check(watch.Changed(), "a patch change during a scan triggers a follow-up scan");
        watch.Capture();
        long checksBefore = watch.Checks, changesBefore = watch.Changes;
        for (int i = 0; i < 64; i++) watch.Changed();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 600; i++) watch.Changed();
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(bytes == 0, "600 unchanged checks over 600 patched methods allocate nothing after warm-up (" + bytes + " B)");
        Check(watch.Checks - checksBefore == 64 + 600 && watch.Changes == changesBefore, "unchanged checks are counted and are not changes (" + watch.Checks + "/" + watch.Changes + ")");

        var infos = Enumerable.Range(0, 600).Select(i => NewPatches(new[] { NewPatch("a" + i) }, new[] { NewPatch("b" + i) }, new HarmonyLib.Patch[0],
            new HarmonyLib.Patch[0], new HarmonyLib.Patch[0], new HarmonyLib.Patch[0])).ToArray();
        var pc = typeof(Program).Assembly;
        void Scan()
        {
            var observed = new List<ObservedPatch>();
            foreach (var info in infos) HarmonyScan.Observe(PatchMethod, info, pc, observed);
            new CoverageTracker(ScopeInventory.Patches, _ => true, "f4b.perf-union-probe").Observe(observed);
        }
        Scan();
        before = GC.GetAllocatedBytesForCurrentThread();
        Scan();
        long scan = GC.GetAllocatedBytesForCurrentThread() - before;
        Console.WriteLine("patch watch: full scan of 600 patch infos without Harmony's deserialization " + scan + " B, unchanged watch check 0 B (.NET " + Environment.Version + ")");
    }

    private static byte[] PatchState(params string[] strings)
    {
        using (var stream = new System.IO.MemoryStream())
        using (var writer = new System.IO.BinaryWriter(stream, System.Text.Encoding.UTF8))
        {
            writer.Write((byte)0); writer.Write(1); writer.Write(-1); writer.Write(1); writer.Write(0);
            writer.Write("0Harmony, Version=2.2.2.0, Culture=neutral, PublicKeyToken=null");
            writer.Write("HarmonyLib.PatchInfo");
            foreach (var text in strings) { writer.Write((byte)6); writer.Write(text.Length); writer.Write(text); }
            writer.Write((byte)11);
            return stream.ToArray();
        }
    }

    private const int SerializableType = 0x2000, NotSerializedField = 0x80;

    private static void ScanCache()
    {
        var moduleField = typeof(HarmonyLib.Patch).GetField("moduleGUID", BindingFlags.NonPublic | BindingFlags.Instance);
        var ownerField = typeof(HarmonyLib.Patch).GetField("owner", BindingFlags.Public | BindingFlags.Instance);
        var sample = NewPatch("hart.persistentcity.cityclock.v1");
        Check(moduleField != null && moduleField.FieldType == typeof(string) && (string)moduleField.GetValue(sample) == PatchMethod.Module.ModuleVersionId.ToString(),
            "the pinned Harmony keeps each patch's module as the module version id string it serializes");
        Check(ownerField != null && ownerField.FieldType == typeof(string) && (string)ownerField.GetValue(sample) == "hart.persistentcity.cityclock.v1"
            && ((int)typeof(HarmonyLib.Patch).Attributes & SerializableType) != 0
            && ((int)ownerField.Attributes & NotSerializedField) == 0 && ((int)moduleField.Attributes & NotSerializedField) == 0,
            "the pinned Harmony serializes each patch's owner and module as string fields");

        byte[] haystack = System.Text.Encoding.UTF8.GetBytes("abcabd");
        Check(PatchScanCache.Contains(haystack, System.Text.Encoding.UTF8.GetBytes("abd")) && PatchScanCache.Contains(haystack, haystack)
            && !PatchScanCache.Contains(haystack, System.Text.Encoding.UTF8.GetBytes("bde")) && !PatchScanCache.Contains(haystack, System.Text.Encoding.UTF8.GetBytes("abcabdx"))
            && PatchScanCache.Contains(haystack, System.Text.Encoding.UTF8.GetBytes("a")) && !PatchScanCache.Contains(new byte[0], System.Text.Encoding.UTF8.GetBytes("a")),
            "the byte search finds a mention at the end and refuses partial and longer needles");

        const string probe = "f4b.perf-union-probe";
        string pcModule = Guid.NewGuid().ToString(), probeModule = Guid.NewGuid().ToString(), gameModule = Guid.NewGuid().ToString(), rigModule = Guid.NewGuid().ToString();
        var targets = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name.StartsWith("System", StringComparison.Ordinal))
            .SelectMany(a => { try { return a.GetTypes(); } catch (ReflectionTypeLoadException) { return Type.EmptyTypes; } })
            .Where(t => t.IsPublic).SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)).Cast<MethodBase>()
            .Distinct().Take(2400).ToArray();
        Check(targets.Length == 2400, "the scan model has 2,400 patched targets (" + targets.Length + ")");
        var state = new Dictionary<MethodBase, byte[]>();
        var truth = new Dictionary<MethodBase, ObservedPatch[]>();
        void Set(MethodBase target, byte[] bytes, ObservedPatch[] observed) { state[target] = bytes; truth[target] = observed; }
        ObservedPatch Foreign(string owner, string target) => new ObservedPatch { Owner = owner, Kind = "Prefix", Type = "Other.Mod", Method = "Prefix", Target = target };
        int next = 0;
        var pcTargets = new List<MethodBase>();
        foreach (var group in ScopeInventory.Patches.GroupBy(r => r.Target))
        {
            var target = targets[next++];
            pcTargets.Add(target);
            var rows = group.Select(r => new ObservedPatch { Owner = r.Owner, Kind = r.Kind.ToString(), Type = r.Type, Method = r.Method, Target = r.Target, PcAssembly = true })
                .Concat(new[] { Foreign("realliveoverhaul", group.Key) }).ToArray();
            Set(target, PatchState(rows.SelectMany(o => new[] { o.Owner, o.PcAssembly ? pcModule : gameModule }).ToArray()), rows);
        }
        var foreignTargets = new List<MethodBase>();
        while (next < targets.Length)
        {
            var target = targets[next++];
            foreignTargets.Add(target);
            string name = target.DeclaringType?.Name + "." + target.Name;
            var rows = new[] { Foreign("realliveoverhaul", name), Foreign("hart.foodtrucks", name) };
            Set(target, PatchState("realliveoverhaul", gameModule, "hart.foodtrucks", gameModule), rows);
        }
        int reads = 0;
        MethodBase failOn = null;
        void Read(MethodBase target, List<ObservedPatch> into)
        {
            if (ReferenceEquals(target, failOn)) { failOn = null; throw new InvalidOperationException("patch info read failed"); }
            reads++; into.AddRange(truth[target]);
        }
        Action<MethodBase, List<ObservedPatch>> reader = Read;
        var cache = new PatchScanCache();
        cache.Configure(ScopeInventory.Owners.Concat(new[] { pcModule }));
        var output = new List<ObservedPatch>();
        bool Same(string what)
        {
            var full = new CoverageTracker(ScopeInventory.Patches, _ => true, probe);
            full.Observe(state.Keys.SelectMany(t => truth[t]).ToList());
            var cached = new CoverageTracker(ScopeInventory.Patches, _ => true, probe);
            output.Clear();
            cache.Scan(state, reader, output);
            cached.Observe(output);
            bool same = full.Complete == cached.Complete && full.UnexpectedAtLastScan == cached.UnexpectedAtLastScan && full.Unexpected.SequenceEqual(cached.Unexpected)
                && ScopeInventory.Patches.All(r => full.Installed(r) == cached.Installed(r));
            Check(same, "a cached scan gives the coverage a full read gives: " + what);
            return same;
        }
        int pcCount = pcTargets.Count;
        Same("first scan");
        Check(cache.LastTargets == 2400 && cache.LastDeserialized == pcCount && cache.LastSkipped == 2400 - pcCount && cache.LastReused == 0 && reads == pcCount,
            "the first scan reads only the " + pcCount + " targets that name an inventory owner or the PC module (" + cache.LastDeserialized + " read, " + cache.LastSkipped + " skipped)");
        int firstReads = cache.LastDeserialized;
        Same("unchanged state");
        Check(cache.LastDeserialized == 0 && cache.LastReused == 2400 && cache.LastSkipped == 0, "an unchanged state reads nothing (" + cache.LastDeserialized + ")");
        for (int i = 0; i < 4; i++) { output.Clear(); cache.Scan(state, reader, output); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 50; i++) { output.Clear(); cache.Scan(state, reader, output); }
        long unchangedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(unchangedBytes == 0, "50 warm scans of 2,400 unchanged targets allocate nothing (" + unchangedBytes + " B)");

        foreach (var target in foreignTargets.Take(71))
        {
            string name = target.DeclaringType?.Name + "." + target.Name;
            Set(target, PatchState("realliveoverhaul", gameModule, "hart.foodtrucks", gameModule, "agent.rig.modwork", rigModule),
                truth[target].Concat(new[] { Foreign("agent.rig.modwork", name) }).ToArray());
        }
        foreach (var target in pcTargets.Take(5))
            Set(target, PatchState(truth[target].SelectMany(o => new[] { o.Owner, o.PcAssembly ? pcModule : gameModule }).Concat(new[] { probe, probeModule }).ToArray()),
                truth[target].Concat(new[] { Foreign(probe, truth[target][0].Target) }).ToArray());
        int afterRig = reads;
        Same("71 targets patched by another tool and 5 PC targets patched by the probe");
        Check(cache.LastDeserialized == 5 && reads - afterRig == 5 && cache.LastSkipped == 71,
            "another tool's patches are skipped unread and only the 5 PC targets whose state changed are read again (" + cache.LastDeserialized + " read, " + cache.LastSkipped + " skipped)");

        var stray = foreignTargets[100];
        Set(stray, PatchState("realliveoverhaul", gameModule, "someone.else", pcModule),
            new[] { Foreign("realliveoverhaul", "X.Y"), new ObservedPatch { Owner = "someone.else", Kind = "Postfix", Type = "PersistentCity.Runtime.Stray", Method = "Postfix", Target = "X.Y", PcAssembly = true } });
        Same("a PC-module patch under an unknown owner");
        Check(cache.LastDeserialized == 1 && output.Any(o => o.Owner == "someone.else" && o.PcAssembly), "a PC-module patch under an unknown owner is read through the module mention");
        var tracker = new CoverageTracker(ScopeInventory.Patches, _ => true, probe);
        tracker.Observe(output);
        Check(!tracker.Complete && tracker.Unexpected.Any(u => u.Contains("PersistentCity.Runtime.Stray")), "and makes coverage incomplete");
        Set(stray, PatchState("realliveoverhaul", gameModule), new[] { Foreign("realliveoverhaul", "X.Y") });

        var removed = pcTargets[3];
        state.Remove(removed); truth.Remove(removed);
        Same("a PC target whose patches all went away");
        Check(cache.Entries == state.Count, "a target that left Harmony's state leaves the cache (" + cache.Entries + "/" + state.Count + ")");

        var changed = pcTargets[6];
        Set(changed, PatchState(truth[changed].Select(o => o.Owner).Concat(new[] { pcModule }).ToArray()), truth[changed].ToArray());
        failOn = changed;
        bool threw = false;
        try { output.Clear(); cache.Scan(state, reader, output); } catch (InvalidOperationException) { threw = true; }
        Check(threw, "a failed patch-info read fails the scan");
        Same("the scan after a failed read");
        Check(cache.LastDeserialized == 1, "the target whose read failed is read again (" + cache.LastDeserialized + ")");

        var unfiltered = new PatchScanCache();
        unfiltered.Configure(new[] { "", null });
        output.Clear(); unfiltered.Scan(state, reader, output);
        Check(unfiltered.Needles == 0 && unfiltered.LastDeserialized == state.Count, "with no mention to look for every target is read");
        cache.Configure(ScopeInventory.Owners.Concat(new[] { pcModule }));
        Check(cache.Entries == state.Count, "configuring the same mentions keeps the cache");
        cache.Configure(ScopeInventory.Owners);
        Check(cache.Entries == 0, "different mentions clear it");

        Console.WriteLine("patch scan cache: " + targets.Length + " patched targets; Harmony patch-info reads per scan, before (every target) / after: first " + targets.Length + " / " + firstReads
            + ", unchanged " + targets.Length + " / 0 (" + unchangedBytes + " B over 50 warm scans), after another tool patches 71 targets and the probe 5 PC targets " + targets.Length + " / 5 (.NET " + Environment.Version + ")");
    }

    private static void ScanPerf(int infos)
    {
        var all = Enumerable.Range(0, infos).Select(i => NewPatches(new[] { NewPatch("a" + i), NewPatch("b" + i), NewPatch("c" + i) },
            new[] { NewPatch("d" + i) }, new HarmonyLib.Patch[0], new HarmonyLib.Patch[0], new HarmonyLib.Patch[0], new HarmonyLib.Patch[0])).ToArray();
        var pc = typeof(Program).Assembly;
        double Run(bool cached)
        {
            var observed = new List<ObservedPatch>();
            long start = Stopwatch.GetTimestamp();
            foreach (var info in all)
            {
                if (cached) { HarmonyScan.Observe(PatchMethod, info, pc, observed); continue; }
                foreach (var field in typeof(HarmonyLib.Patches).GetFields(BindingFlags.Public | BindingFlags.Instance))
                    if (field.GetValue(info) is IEnumerable<HarmonyLib.Patch> list)
                        foreach (var p in list)
                            observed.Add(new ObservedPatch { Owner = p.owner, Kind = HarmonyScan.Kind(field.Name), Type = p.PatchMethod?.DeclaringType?.FullName,
                                Method = p.PatchMethod?.Name, Target = PatchMethod.DeclaringType?.Name + "." + PatchMethod.Name, PcAssembly = p.PatchMethod?.DeclaringType?.Assembly == pc });
            }
            var tracker = new CoverageTracker(ScopeInventory.Patches, _ => true, "f4b.perf-union-probe");
            tracker.Observe(observed);
            return (Stopwatch.GetTimestamp() - start) * 1e6 / Stopwatch.Frequency;
        }
        for (int i = 0; i < 20; i++) { Run(false); Run(true); }
        var before = Enumerable.Range(0, 51).Select(_ => Run(false)).ToArray();
        var after = Enumerable.Range(0, 51).Select(_ => Run(true)).ToArray();
        Console.WriteLine($"perf rescan infos={infos} patches={infos * 4} uncached_fields_us median={Median(before):F1} max={before.Max():F1} cached_HarmonyScan_us median={Median(after):F1} max={after.Max():F1}");
    }


    private struct State { public long Start; public int Frame, Index; public bool Timed; }
    private static bool measuring = true;
    private static int armThread, frameCount = 1, currentFrame = 1, offMain;
    private static UnionClock union;
    private static Dictionary<object, int> index, fast;
    private static object key;
    private static int sink;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Work() { int s = 0; for (int i = 0; i < 40; i++) s += i * i; sink += s; }

    private static bool MainThread() { if (Thread.CurrentThread.ManagedThreadId == armThread) return true; offMain++; return false; }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Before(object method, out State s)
    {
        s = default;
        if (!measuring || !MainThread()) return;
        if (frameCount != currentFrame) currentFrame = frameCount;
        if (!fast.TryGetValue(method, out int i)) { if (!index.TryGetValue(method, out i)) return; fast[method] = i; }
        s.Index = i; s.Frame = frameCount; s.Timed = true;
        s.Start = Stopwatch.GetTimestamp();
        union.Enter(s.Start);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void After(Exception e, State s)
    {
        if (!s.Timed) return;
        long end = Stopwatch.GetTimestamp();
        if (frameCount != s.Frame) offMain++;
        union.Exit(s.Index, s.Start, end);
    }

    private sealed class ReferenceComparer : IEqualityComparer<object>
    {
        public new bool Equals(object a, object b) => ReferenceEquals(a, b);
        public int GetHashCode(object o) => RuntimeHelpers.GetHashCode(o);
    }

    private static double Median(IEnumerable<double> values) { var v = values.OrderBy(x => x).ToArray(); return v[v.Length / 2]; }

    private static double TimeLoop(int n, bool probed)
    {
        long start = Stopwatch.GetTimestamp();
        if (probed) for (int i = 0; i < n; i++) { Before(key, out var s); Work(); After(null, s); union.ResetFrame(); }
        else for (int i = 0; i < n; i++) { Work(); }
        return (Stopwatch.GetTimestamp() - start) * 1e9 / Stopwatch.Frequency / n;
    }

    private static double LookupCost(Dictionary<object, int> table, object[] keys, int n)
    {
        long start = Stopwatch.GetTimestamp(); int found = 0;
        for (int i = 0; i < n; i++) if (table.TryGetValue(keys[i % keys.Length], out int v)) found += v;
        sink += found;
        return (Stopwatch.GetTimestamp() - start) * 1e9 / Stopwatch.Frequency / n;
    }

    private static void Perf()
    {
        armThread = Thread.CurrentThread.ManagedThreadId;
        var methods = typeof(string).GetMethods().Concat<MethodBase>(typeof(List<int>).GetMethods()).Take(80).ToArray();
        union = new UnionClock(methods.Length, 11, Stopwatch.Frequency);
        key = methods[37];
        const int n = 2_000_000, runs = 7;
        var results = new Dictionary<string, double>();
        foreach (var (name, comparer) in new (string, IEqualityComparer<object>)[] { ("default", null), ("reference", new ReferenceComparer()) })
        {
            index = comparer == null ? new Dictionary<object, int>() : new Dictionary<object, int>(comparer);
            for (int i = 0; i < methods.Length; i++) index[methods[i]] = i;
            fast = comparer == null ? new Dictionary<object, int>() : new Dictionary<object, int>(comparer);
            if (comparer == null) fast = index;
            TimeLoop(n / 4, false); TimeLoop(n / 4, true);
            var overhead = Enumerable.Range(0, runs).Select(_ => TimeLoop(n, true) - TimeLoop(n, false)).ToArray();
            var lookups = Enumerable.Range(0, runs).Select(_ => LookupCost(index, methods, n)).ToArray();
            results[name] = Median(overhead);
            Console.WriteLine($"perf comparer={name} scope_overhead_ns_per_call median={Median(overhead):F1} min={overhead.Min():F1} max={overhead.Max():F1} lookup_ns={Median(lookups):F1}");
        }
        long held = 0, skip = 0, clips = 0; double withheld = 0;
        Func<long> readHeld = () => held, readSkip = () => skip, readClips = () => clips; Func<double> readWithheld = () => withheld;
        var tracker = new HoldTracker(); tracker.BeginWindow(0, 0);
        var ticks = new TickRecord[1024];
        var observer = Enumerable.Range(0, runs).Select(_ =>
        {
            long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < n; i++)
            {
                var before = new ClockCounters(readHeld(), readSkip(), readClips(), readWithheld());
                if ((i & 63) == 0) held++;
                var after = new ClockCounters(readHeld(), readSkip(), readClips(), readWithheld());
                ticks[i & 1023] = tracker.Observe(before, after, false, i, i, i, i);
                int gc = GC.CollectionCount(0);
                if (AdvanceCallRule.Record(i & 0xFFFFF, Stopwatch.Frequency)) sink += gc;
            }
            return (Stopwatch.GetTimestamp() - start) * 1e9 / Stopwatch.Frequency / n;
        }).ToArray();
        double observerNs = Median(observer), scopeNs = results["reference"];
        Console.WriteLine($"perf observer_ns_per_frame median={observerNs:F1} (one hold tick + one advance-call check)");
        foreach (int scopes in new[] { 50, 200, 1000 })
        {
            double probeNs = scopes * scopeNs + observerNs;
            Console.WriteLine($"perf frame scopes={scopes} probe_cost_us={probeNs / 1000:F2} observerMs_share={observerNs / probeNs:P1}");
        }
        ScanPerf(100); ScanPerf(300);
        Console.WriteLine("perf qualification: .NET " + Environment.Version + " stub loop; Harmony dispatch, Unity icalls (Time.frameCount) and Mono JIT are not represented; G2 measures the native share.");
    }
}
