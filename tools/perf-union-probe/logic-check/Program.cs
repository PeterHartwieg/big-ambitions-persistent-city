// Offline checks for the F4b probe's Unity-free rules (ProbeLogic.cs, ScopeInventory.cs) and the
// stub-loop overhead measurement recorded in docs/perf/f4b.md. Exit 0 only when every check passes.
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
        Shim();
        Holds();
        BoundaryClips();
        CounterReset();
        AdvanceRecords();
        Union();
        Inventory(expectHash);
        CoverageLifecycle();
        Capacity();
        Console.WriteLine("logic-check: " + (checks - failures) + "/" + checks + " checks passed");
        Console.WriteLine("expected_scope_hash " + ScopeInventory.Hash());
        if (perf) Perf();
        return failures == 0 ? 0 : 1;
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

    // A tick step: whether the population was ready and whether the delta was clipped at a minute boundary.
    private struct Step { public bool Held, Limited; public Step(bool held, bool limited) { Held = held; Limited = limited; } }

    /// <summary>Drives HoldTracker like NativeCityClock.AccountingPrefix: a held tick increments a held counter
    /// and withholds minutes; a ready clipped tick increments BoundaryClips.</summary>
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
        // Before the window: a hold starts and is still open when the window begins.
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
        // A window that starts released and ends released has no edge flags.
        var quiet = new HoldTracker(); var quietClock = new FakeClock(); frame = 1;
        quiet.BeginWindow(1, 100); Drive(quiet, quietClock, new[] { ready, held, ready }, ref frame); quiet.EndWindow(3, 300);
        Check(quiet.Events.Count == 2 && quiet.Events.All(x => !x.CarriedIn && !x.Ongoing), "released edges carry no flags");
        // Economy-skip holds count as holds too.
        var skip = new HoldTracker(); var skipClock = new FakeClock { Skipping = true }; frame = 1;
        skip.BeginWindow(1, 100); var rows = Drive(skip, skipClock, new[] { held }, ref frame); skip.EndWindow(1, 100);
        Check(rows[0].Held && skip.Events.Count == 2 && skip.Events[1].Ongoing, "an economy-skip held tick is a hold");
        // Reopening a window clears the previous events.
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
        // Limited with no counter change at all (another cap ahead of PC) is also not a hold.
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
            long thirty = (30 * frequency + 999) / 1000;     // smallest tick count that is at least 30.0 ms
            long below = 299 * frequency / 10_000;          // 29.9 ms
            Check(AdvanceCallRule.Record(thirty, frequency), "30.0 ms call recorded at " + frequency + " Hz");
            Check(!AdvanceCallRule.Record(below, frequency), "29.9 ms call not recorded at " + frequency + " Hz");
            if (30 * frequency % 1000 != 0) Check(!AdvanceCallRule.Record(thirty - 1, frequency), "just below 30 ms not recorded at " + frequency + " Hz");
        }
        Check(AdvanceCallRule.Record(300_000, 10_000_000) && !AdvanceCallRule.Record(299_000, 10_000_000), "exact 30.0 / 29.9 ms at 10 MHz QPC");
        Check(!AdvanceCallRule.Record(-1, 10_000_000), "negative duration not recorded");
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
        Check(ScopeInventory.Patches.Count == 58, "58 expected patches (35 from the plan's section 2, 23 food-contract)");
        Check(ScopeInventory.Patches.Select(p => p.Owner + "|" + p.Kind + "|" + p.Type + "|" + p.Method).Distinct().Count() == ScopeInventory.Patches.Count, "patch keys are unique");
        Check(ScopeInventory.Owners.All(o => o.StartsWith("hart.persistentcity.") || o.StartsWith("persistentcity.")), "every expected owner is a Persistent City owner");
        Check(!ScopeInventory.Owners.Contains("f4b.perf-union-probe"), "the probe's own owner is outside the inventory");
        Check(ScopeInventory.Components.Any(c => c.Method == "ApplyHoldReplay") && ScopeInventory.Components.Any(c => c.Method == "ObservePlayerContact"), "F2e hold call sites are components");
        Check(ScopeInventory.Patches.Count(p => p.Kind == ScopeKind.Transpiler) == 2 && ScopeInventory.TranspilerHelpers.Count == 3, "two transpilers, three timed helpers");
        Check(all.All(s => s.File.Length > 0 && s.Type.Length > 0 && s.Method.Length > 0), "every row names a file, type and method");
        Check(ScopeInventory.Hash() == ScopeInventory.Hash() && ScopeInventory.Hash().Length == 64, "hash is stable");
        if (expectHash != null) Check(ScopeInventory.Hash() == expectHash, "C# canonical hash equals check_scopes.py's (" + expectHash + ")");
    }

    private static ObservedPatch Observed(ExpectedScope row, string target = null, string owner = null, string kind = null) => new ObservedPatch
    { Owner = owner ?? row.Owner, Kind = kind ?? row.Kind.ToString(), Type = row.Type, Method = row.Method, Target = target ?? row.Target, PcAssembly = true };

    /// <summary>Sol-r1-1/Sol-r1-3: hooks installed after arming stay timed and complete; drift and duplicates fail closed.</summary>
    private static void CoverageLifecycle()
    {
        const string probe = "f4b.perf-union-probe";
        var patches = ScopeInventory.Patches;
        CoverageTracker Armed() => new CoverageTracker(patches, _ => true, probe);
        var installedLater = patches.Where(p => p.Owner == "hart.persistentcity.physicalvehicles.v1" || p.Owner == "hart.persistentcity.ambientpedestrians.v1").ToArray();
        var early = patches.Where(p => !installedLater.Contains(p) && p.Method != "RidePrefix" && p.Method != "RidePostfix").ToArray();

        // Arm inside a building: traffic and ambient hooks are not installed yet.
        var t = Armed();
        t.Observe(early.Select(p => Observed(p)));
        Check(t.Complete && !t.Installed(installedLater[0]), "arming before physical hooks exist is complete; they show as not installed");
        // Exit the building: PhysicalPopulation installs its hooks after arming. They were bound at arm, so they are timed.
        t.Observe(early.Concat(installedLater).Select(p => Observed(p)));
        Check(t.Complete && installedLater.All(t.Installed) && t.UnexpectedAtLastScan == 0, "hooks installed after arming are expected and stay complete");
        // And removed again (bodies released): still complete.
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

    /// <summary>Sol-r1-2: the planned windows fit, by buffer or by draining while measuring.</summary>
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
        long g13 = 20L * 60 * fps; // 20 real minutes at the declared 90 FPS cap
        Check(g13 <= ProbeCapacity.Frames && Run(ProbeCapacity.Frames, g13, 0, 0) == 0, "a 20-minute G13 window at 90 FPS fits the frame buffer undrained");
        Check(Run(ProbeCapacity.Ticks, g13, 0, 0) == 0, "its ticks (one native tick per frame) fit undrained");
        Check(Run(ProbeCapacity.Diagnostics, 20 * 60, 0, 0) == 0, "its once-per-second diagnostics fit undrained");
        long soak = 4L * 3600 * fps; // longer than G14's three game days at speed 3 (about 80 real minutes)
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
    }

    // ---- Offline overhead on a stub loop (docs/perf/f4b.md) ----

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

    // Same steps as ScopePatch.Before/After, with Time.frameCount replaced by a static field.
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
            // "default": the default-comparer table alone (before); "reference": the probe's reference cache in front (after).
            fast = comparer == null ? new Dictionary<object, int>() : new Dictionary<object, int>(comparer);
            if (comparer == null) fast = index;
            TimeLoop(n / 4, false); TimeLoop(n / 4, true); // warm-up
            var overhead = Enumerable.Range(0, runs).Select(_ => TimeLoop(n, true) - TimeLoop(n, false)).ToArray();
            var lookups = Enumerable.Range(0, runs).Select(_ => LookupCost(index, methods, n)).ToArray();
            results[name] = Median(overhead);
            Console.WriteLine($"perf comparer={name} scope_overhead_ns_per_call median={Median(overhead):F1} min={overhead.Min():F1} max={overhead.Max():F1} lookup_ns={Median(lookups):F1}");
        }
        // Per-frame observer work: one AccountingPrefix tick (four counter reads, Observe) and one advance call record.
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
        Console.WriteLine("perf qualification: .NET " + Environment.Version + " stub loop; Harmony dispatch, Unity icalls (Time.frameCount) and Mono JIT are not represented; G2 measures the native share.");
    }
}
