using System;
using System.Collections.Generic;
using System.Reflection;

namespace PerfUnionProbe
{
    public static class RigProfile
    {
        public const string WindowsProduct = "Big Ambitions Agent Tests", MacProduct = "Big Ambitions Agent Tests Mac";
        public static readonly string[] WindowsProfile = { "AppData", "LocalLow", "Hovgaard Games", "Big Ambitions Agent Tests" };
        public static readonly string[] MacProfile = { "Library", "Application Support", "com.hovgaardgames.bigambitions.agenttests.mac" };

        public static bool Isolated(IList<string> arguments, string product, string persistentDataPath, string userProfile)
        {
            if (arguments == null || !arguments.Contains("-agent-tests") || !arguments.Contains("-rig-test")) return false;
            if (string.IsNullOrEmpty(persistentDataPath) || string.IsNullOrEmpty(userProfile)) return false;
            if (product == WindowsProduct) return SamePath(persistentDataPath, userProfile, WindowsProfile, StringComparison.OrdinalIgnoreCase);
            if (product == MacProduct) return SamePath(persistentDataPath, userProfile, MacProfile, StringComparison.Ordinal);
            return false;
        }

        private static bool SamePath(string actual, string home, string[] parts, StringComparison comparison)
        {
            string expected = Normal(home);
            foreach (var part in parts) expected += "/" + part;
            return string.Equals(Normal(actual), expected, comparison);
        }

        private static string Normal(string path) => path.Replace('\\', '/').TrimEnd('/');
    }

    public static class PaceShim
    {
        public const double MinimumFactor = 0.1, MaximumFactor = 1;

        public static bool ValidFactor(double factor)
            => !double.IsNaN(factor) && !double.IsInfinity(factor) && factor >= MinimumFactor && factor <= MaximumFactor;

        public static bool Ordinary(bool loading, bool timeMachinePresent, bool timeMachineRunning)
            => !loading && timeMachinePresent && !timeMachineRunning;

        public static float Apply(float delta, bool loading, bool timeMachinePresent, bool timeMachineRunning, double factor)
        {
            if (!ValidFactor(factor)) throw new ArgumentOutOfRangeException(nameof(factor));
            if (float.IsNaN(delta) || float.IsInfinity(delta) || delta <= 0) return delta;
            return Ordinary(loading, timeMachinePresent, timeMachineRunning) ? delta * (float)factor : delta;
        }
    }

    public struct ClockCounters
    {
        public long HeldOrdinaryTicks, HeldEconomySkipTicks, BoundaryClips;
        public double WithheldMinutes;
        public ClockCounters(long heldOrdinary, long heldSkip, long clips, double withheld)
        { HeldOrdinaryTicks = heldOrdinary; HeldEconomySkipTicks = heldSkip; BoundaryClips = clips; WithheldMinutes = withheld; }
    }

    public struct TickRecord
    {
        public long Qpc;
        public double WithheldMinutes, NativeMinute, LogicalMinute;
        public int Frame;
        public bool Held, BoundaryClip, AccountingLimited, CountersReset;
    }

    public enum HoldEventKind { Hold, Resume }

    public struct HoldEvent
    {
        public HoldEventKind Kind;
        public int Frame;
        public long Qpc;
        public bool CarriedIn;
        public bool Ongoing;
    }

    public sealed class HoldTracker
    {
        private readonly List<HoldEvent> events = new List<HoldEvent>();
        private bool windowOpen;
        public bool Held { get; private set; }
        public long Ticks { get; private set; }
        public long HeldTicks { get; private set; }
        public long BoundaryClipTicks { get; private set; }
        public long CounterResets { get; private set; }
        public IReadOnlyList<HoldEvent> Events => events;
        public bool WindowOpen => windowOpen;

        public TickRecord Observe(in ClockCounters before, in ClockCounters after, bool accountingLimited,
            int frame, long qpc, double nativeMinute, double logicalMinute)
        {
            bool reset = after.HeldOrdinaryTicks < before.HeldOrdinaryTicks || after.HeldEconomySkipTicks < before.HeldEconomySkipTicks
                || after.BoundaryClips < before.BoundaryClips || after.WithheldMinutes < before.WithheldMinutes;
            bool held = !reset && (after.HeldOrdinaryTicks > before.HeldOrdinaryTicks || after.HeldEconomySkipTicks > before.HeldEconomySkipTicks);
            bool clip = !reset && !held && accountingLimited;
            Ticks++;
            if (reset) CounterResets++;
            if (held) HeldTicks++;
            if (clip) BoundaryClipTicks++;
            if (held != Held && windowOpen)
                events.Add(new HoldEvent { Kind = held ? HoldEventKind.Hold : HoldEventKind.Resume, Frame = frame, Qpc = qpc });
            Held = held;
            return new TickRecord { Frame = frame, Qpc = qpc, Held = held, BoundaryClip = clip, AccountingLimited = accountingLimited,
                CountersReset = reset, WithheldMinutes = reset ? 0 : Math.Max(0, after.WithheldMinutes - before.WithheldMinutes),
                NativeMinute = nativeMinute, LogicalMinute = logicalMinute };
        }

        public void BeginWindow(int firstFrame, long firstQpc)
        {
            events.Clear(); windowOpen = true;
            Ticks = HeldTicks = BoundaryClipTicks = CounterResets = 0;
            if (Held) events.Add(new HoldEvent { Kind = HoldEventKind.Hold, Frame = firstFrame, Qpc = firstQpc, CarriedIn = true });
        }

        public void EndWindow(int lastFrame, long lastQpc)
        {
            if (!windowOpen) return;
            if (Held) events.Add(new HoldEvent { Kind = HoldEventKind.Resume, Frame = lastFrame, Qpc = lastQpc, Ongoing = true });
            windowOpen = false;
        }
    }

    public static class AdvanceCallRule
    {
        public const int ThresholdMilliseconds = 30;
        public static bool Record(long durationTicks, long frequency)
        {
            if (frequency <= 0) throw new ArgumentOutOfRangeException(nameof(frequency));
            return durationTicks >= 0 && durationTicks * 1000 >= ThresholdMilliseconds * frequency;
        }
        public static bool RecordUnits(double maximumUnitOptionalStopMilliseconds) => maximumUnitOptionalStopMilliseconds > AdvanceCallRows.UnitThresholdMilliseconds;
        public static bool AdmitUnits(bool failed, double maximumUnitOptionalStopMilliseconds) => !failed && RecordUnits(maximumUnitOptionalStopMilliseconds);
        public static bool ExportUnits(bool failed) => !failed;
    }

    public struct AdvanceCallRecord
    {
        public int Frame;
        public long StartQpc, DurationTicks;
        public int CollectionsDelta;
        public double OptionalStopMilliseconds;
        public double TargetMinute;
        public bool Over30;
        public bool UnitsRead;
        public int Units;
        public string SlowestUnitKind;
        public double SlowestUnitMilliseconds, SlowestUnitOptionalStopMilliseconds, MaximumUnitOptionalStopMilliseconds, P95UnitOptionalStopMilliseconds;
        public double SlowestUnitCpuMilliseconds;
        public UnitOptionalStop[] UnitsOverThreshold;
    }

    public struct UnitOptionalStop
    {
        public int Unit;
        public string Kind;
        public double OptionalStopMilliseconds;
    }

    public interface IUnitSeamSource
    {
        int Units { get; }
        bool Completed { get; }
        string SlowestUnitKind { get; }
        double SlowestUnitMilliseconds { get; }
        double SlowestUnitSeamMilliseconds { get; }
        double SlowestUnitCpuMilliseconds { get; }
        double MaximumUnitSeamMilliseconds { get; }
        double P95UnitSeamMilliseconds();
        double UnitSeamMilliseconds(int unit);
        string UnitKind(int unit);
        int NextUnitOverThreshold(int from);
    }

    public sealed class ReflectedUnitSeams : IUnitSeamSource
    {
        private PropertyInfo units, completed, slowestKind, slowestMs, slowestSeam, maximumSeam, slowestCpu;
        private MethodInfo p95, unitSeam, unitKind, nextOver;
        public object Target;
        public static ReflectedUnitSeams Bind(Type type)
        {
            const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance;
            PropertyInfo P(string name, Type expected) { var p = type.GetProperty(name, Flags); return p != null && p.PropertyType == expected ? p : null; }
            MethodInfo M(string name, Type returns, params Type[] args) { var m = type.GetMethod(name, Flags, null, args, null); return m != null && m.ReturnType == returns ? m : null; }
            var bound = new ReflectedUnitSeams
            {
                units = P("Units", typeof(int)), completed = P("Completed", typeof(bool)), slowestKind = P("SlowestUnitKind", typeof(string)), slowestMs = P("SlowestUnitMilliseconds", typeof(double)),
                slowestSeam = P("SlowestUnitSeamMilliseconds", typeof(double)), maximumSeam = P("MaximumUnitSeamMilliseconds", typeof(double)),
                slowestCpu = P("SlowestUnitCpuMilliseconds", typeof(double)),
                p95 = M("P95UnitSeamMilliseconds", typeof(double)), unitSeam = M("UnitSeamMilliseconds", typeof(double), typeof(int)),
                unitKind = M("UnitKind", typeof(string), typeof(int)), nextOver = M("NextUnitOverThreshold", typeof(int), typeof(int)),
            };
            return bound.units == null || bound.slowestKind == null || bound.slowestMs == null || bound.slowestSeam == null || bound.maximumSeam == null
                || bound.p95 == null || bound.unitSeam == null || bound.unitKind == null || bound.completed == null || bound.nextOver == null ? null : bound;
        }
        public int Units => (int)units.GetValue(Target);
        public string SlowestUnitKind => (string)slowestKind.GetValue(Target);
        public double SlowestUnitMilliseconds => (double)slowestMs.GetValue(Target);
        public double SlowestUnitSeamMilliseconds => (double)slowestSeam.GetValue(Target);
        public double SlowestUnitCpuMilliseconds => slowestCpu == null ? -1 : (double)slowestCpu.GetValue(Target);
        public double MaximumUnitSeamMilliseconds => (double)maximumSeam.GetValue(Target);
        public double P95UnitSeamMilliseconds() => (double)p95.Invoke(Target, null);
        public double UnitSeamMilliseconds(int unit) => (double)unitSeam.Invoke(Target, new object[] { unit });
        public string UnitKind(int unit) => (string)unitKind.Invoke(Target, new object[] { unit });
        public bool Completed => (bool)completed.GetValue(Target);
        public int NextUnitOverThreshold(int from) => (int)nextOver.Invoke(Target, new object[] { from });
        public PropertyInfo MaximumProperty => maximumSeam;
    }

    public static class AdvanceCallRows
    {
        public const double UnitThresholdMilliseconds = 5;
        public const string UnitsOverThresholdField = "units_optional_stop_over_5ms";

        public static void ReadUnits(ref AdvanceCallRecord record, IUnitSeamSource source)
        {
            if (!source.Completed) { record.UnitsRead = false; return; }
            int units = source.Units;
            var over = new List<UnitOptionalStop>();
            for (int i = source.NextUnitOverThreshold(0); i >= 0 && i < units; i = source.NextUnitOverThreshold(i + 1))
                over.Add(new UnitOptionalStop { Unit = i, Kind = source.UnitKind(i), OptionalStopMilliseconds = source.UnitSeamMilliseconds(i) });
            record.Units = units;
            record.SlowestUnitKind = string.IsNullOrEmpty(source.SlowestUnitKind) ? "none" : source.SlowestUnitKind;
            record.SlowestUnitMilliseconds = source.SlowestUnitMilliseconds;
            record.SlowestUnitOptionalStopMilliseconds = source.SlowestUnitSeamMilliseconds;
            double cpu = source.SlowestUnitCpuMilliseconds;
            record.SlowestUnitCpuMilliseconds = double.IsNaN(cpu) || cpu < 0 ? double.NaN : cpu;
            record.MaximumUnitOptionalStopMilliseconds = source.MaximumUnitSeamMilliseconds;
            record.P95UnitOptionalStopMilliseconds = source.P95UnitSeamMilliseconds();
            record.UnitsOverThreshold = over.ToArray();
            record.UnitsRead = true;
        }

        public static List<KeyValuePair<string, object>> Fields(AdvanceCallRecord c, double toMs)
        {
            object Number(double value) => double.IsNaN(value) ? null : (object)value;
            bool read = c.UnitsRead;
            return new List<KeyValuePair<string, object>>
            {
                new KeyValuePair<string, object>("frame", c.Frame),
                new KeyValuePair<string, object>("start_qpc", c.StartQpc),
                new KeyValuePair<string, object>("duration_ms", c.DurationTicks * toMs),
                new KeyValuePair<string, object>("gc_delta", c.CollectionsDelta),
                new KeyValuePair<string, object>("target_minute", c.TargetMinute),
                new KeyValuePair<string, object>("over_30ms", c.Over30),
                new KeyValuePair<string, object>("record_reason", c.Over30 ? "duration_ge30" : "unit_optional_stop_over_5ms"),
                new KeyValuePair<string, object>("slowest_unit", read ? c.SlowestUnitKind : "unknown"),
                new KeyValuePair<string, object>("optional_stop_ms", Number(c.OptionalStopMilliseconds)),
                new KeyValuePair<string, object>("units", read ? (object)c.Units : null),
                new KeyValuePair<string, object>("slowest_unit_ms", read ? Number(c.SlowestUnitMilliseconds) : null),
                new KeyValuePair<string, object>("slowest_unit_optional_stop_ms", read ? Number(c.SlowestUnitOptionalStopMilliseconds) : null),
                new KeyValuePair<string, object>("slowest_unit_cpu_ms", read ? Number(c.SlowestUnitCpuMilliseconds) : null),
                new KeyValuePair<string, object>("slowest_unit_off_cpu_ms", read && !double.IsNaN(c.SlowestUnitCpuMilliseconds) ? (object)Math.Max(0, c.SlowestUnitMilliseconds - c.SlowestUnitCpuMilliseconds) : null),
                new KeyValuePair<string, object>("unit_optional_stop_max_ms", read ? Number(c.MaximumUnitOptionalStopMilliseconds) : null),
                new KeyValuePair<string, object>("unit_optional_stop_p95_ms", read ? Number(c.P95UnitOptionalStopMilliseconds) : null),
                new KeyValuePair<string, object>(UnitsOverThresholdField, read ? (object)(c.UnitsOverThreshold ?? new UnitOptionalStop[0]) : null),
            };
        }
    }

    public sealed class UnionClock
    {
        private readonly long[] calls, ticks, maxTicks, spikes, frameComponentTicks;
        private readonly long spikeTicks;
        private int depth;
        private long unionStart;
        public UnionClock(int scopes, int components, long frequency)
        {
            calls = new long[scopes]; ticks = new long[scopes]; maxTicks = new long[scopes]; spikes = new long[scopes];
            frameComponentTicks = new long[components];
            spikeTicks = AdvanceCallRule.ThresholdMilliseconds * frequency / 1000;
        }
        public int Depth => depth;
        public long FrameTicks { get; private set; }
        public long Underflows { get; private set; }
        public long ComponentsOutsideUnion { get; private set; }
        public int Scopes => calls.Length;
        public int Components => frameComponentTicks.Length;
        public long Calls(int scope) => calls[scope];
        public long Ticks(int scope) => ticks[scope];
        public long MaxTicks(int scope) => maxTicks[scope];
        public long Spikes(int scope) => spikes[scope];
        public long FrameComponentTicks(int component) => frameComponentTicks[component];
        public int FrameLargestScope { get; private set; } = -1;
        public long FrameLargestScopeTicks { get; private set; }

        public void Enter(long now) { if (depth++ == 0) unionStart = now; }
        public void Exit(int scope, long start, long now)
        {
            long duration = Math.Max(0, now - start);
            calls[scope]++; ticks[scope] += duration;
            if (duration > maxTicks[scope]) maxTicks[scope] = duration;
            if (duration >= spikeTicks) spikes[scope]++;
            if (duration > FrameLargestScopeTicks) { FrameLargestScopeTicks = duration; FrameLargestScope = scope; }
            if (depth == 0) { Underflows++; return; }
            if (--depth == 0) FrameTicks += Math.Max(0, now - unionStart);
        }
        public void Component(int component, long start, long now, bool insideUnion)
        {
            if (!insideUnion) ComponentsOutsideUnion++;
            frameComponentTicks[component] += Math.Max(0, now - start);
        }
        public void ResetFrame() { FrameTicks = 0; FrameLargestScope = -1; FrameLargestScopeTicks = 0; Array.Clear(frameComponentTicks, 0, frameComponentTicks.Length); }
        public void ResetAll()
        {
            ResetFrame(); depth = 0; Underflows = ComponentsOutsideUnion = 0;
            Array.Clear(calls, 0, calls.Length); Array.Clear(ticks, 0, ticks.Length);
            Array.Clear(maxTicks, 0, maxTicks.Length); Array.Clear(spikes, 0, spikes.Length);
        }
        public void Abandon() => depth = 0;
    }
    public struct ObservedPatch
    {
        public string Owner, Kind, Type, Method, Target;
        public bool PcAssembly;
    }

    public sealed class CoverageTracker
    {
        private readonly Dictionary<string, ExpectedScope> expected = new Dictionary<string, ExpectedScope>(StringComparer.Ordinal);
        private readonly HashSet<string> owners;
        private readonly SortedSet<string> unbindable = new SortedSet<string>(StringComparer.Ordinal);
        private readonly SortedSet<string> unexpected = new SortedSet<string>(StringComparer.Ordinal);
        private HashSet<string> installed = new HashSet<string>(StringComparer.Ordinal);
        private readonly string probeOwner;

        public CoverageTracker(IEnumerable<ExpectedScope> patches, Func<ExpectedScope, bool> bindable, string probeOwner)
        {
            this.probeOwner = probeOwner;
            owners = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in patches)
            {
                expected[Key(row.Owner, row.Kind.ToString(), row.Type, row.Method)] = row;
                owners.Add(row.Owner);
                if (row.Kind != ScopeKind.Transpiler && !bindable(row)) unbindable.Add(row.Label);
            }
        }

        public static string Key(string owner, string kind, string type, string method) => owner + "|" + kind + "|" + type + "|" + method;
        public long Scans { get; private set; }
        public int UnexpectedAtLastScan { get; private set; }
        public bool Complete => unbindable.Count == 0 && unexpected.Count == 0;
        public IReadOnlyCollection<string> Unbindable => unbindable;
        public IReadOnlyCollection<string> Unexpected => unexpected;
        public bool Installed(ExpectedScope row) => installed.Contains(Key(row.Owner, row.Kind.ToString(), row.Type, row.Method));

        public int Observe(IEnumerable<ObservedPatch> observed)
        {
            var now = new HashSet<string>(StringComparer.Ordinal);
            int found = 0;
            foreach (var p in observed)
            {
                if (p.Owner == probeOwner) continue;
                if (!owners.Contains(p.Owner ?? "") && !p.PcAssembly) continue;
                string key = Key(p.Owner, p.Kind, p.Type, p.Method);
                if (!expected.TryGetValue(key, out var row) || row.Target != p.Target || !now.Add(key))
                { unexpected.Add(p.Kind + " " + p.Type + "." + p.Method + " on " + p.Target + " by " + p.Owner); found++; }
            }
            installed = now; Scans++; UnexpectedAtLastScan = found;
            return found;
        }
    }

    public sealed class PatchSetWindow
    {
        public const string ChangedError = "f4b_patch_set_changed_in_window";
        private int unexpectedAtStart;
        public bool Changed { get; private set; }

        public void Begin(CoverageTracker coverage)
        {
            Changed = false;
            unexpectedAtStart = coverage?.Unexpected.Count ?? 0;
        }

        public string Scanned(PatchScanCache cache, PatchStateWatch watch, Action<MethodBase, List<ObservedPatch>> observe, List<ObservedPatch> into, int attempts, bool measuring, string error)
            => Note(measuring, cache.ScanSettled(watch, observe, into, attempts) > 0, error);

        public string Rescanned(CoverageTracker coverage, bool measuring, string error)
            => Note(measuring, coverage.Unexpected.Count > unexpectedAtStart, error);

        public string Note(bool measuring, bool changed, string error)
        {
            if (!measuring || !changed) return error;
            Changed = true;
            return string.IsNullOrEmpty(error) ? ChangedError : error;
        }
    }

    public sealed class PatchWindowScan
    {
        public const string FailedError = "f4b_patch_scan_failed";
        private readonly List<ObservedPatch> scanned = new List<ObservedPatch>();
        private readonly Action<MethodBase, List<ObservedPatch>> observe;
        private readonly Func<IEnumerable<MethodBase>> allPatched;
        private readonly int attempts;
        private readonly Action scan, matched;
        private PatchStateWatch watch;
        private CoverageTracker coverage;

        public PatchWindowScan(PatchScanCache cache, Action<MethodBase, List<ObservedPatch>> observe, Func<IEnumerable<MethodBase>> allPatched, int attempts)
        {
            Cache = cache; this.observe = observe; this.allPatched = allPatched; this.attempts = attempts;
            scan = () => coverage.Observe(Observe());
            matched = () => { Observe(); MatchedScans++; };
        }

        public PatchScanCache Cache { get; }
        public PatchSetWindow Window { get; } = new PatchSetWindow();
        public long MatchedScans { get; private set; }
        public void ResetMatched() => MatchedScans = 0;

        public string Error { get; set; } = "";
        public bool Measuring { get; private set; }
        public string StopScan { get; private set; } = "none";

        public bool Run(PatchStateWatch watch, CoverageTracker coverage, bool full)
        {
            this.watch = watch; this.coverage = coverage;
            try { return PatchStateWatch.Run(watch, full, coverage == null ? matched : scan); }
            finally { this.watch = null; this.coverage = null; }
        }

        public string Rescan(PatchStateWatch watch, CoverageTracker coverage, bool reference, bool full)
        {
            if (coverage == null && (!reference || !full && watch == null)) return "none";
            bool ran = false;
            try { ran = Run(watch, coverage, full); } catch { Error = FailedError; }
            if (coverage != null) Error = Window.Rescanned(coverage, Measuring, Error);
            return ran ? "full" : "watch-unchanged";
        }

        public string Start(PatchStateWatch watch, CoverageTracker coverage, bool reference)
        {
            Error = ""; StopScan = "none";
            string startScan = Rescan(watch, coverage, reference, false);
            Window.Begin(coverage); Measuring = true;
            return startScan;
        }

        public void Stop(PatchStateWatch watch, CoverageTracker coverage, bool reference, bool explicitStop)
        {
            if (explicitStop) StopScan = Rescan(watch, coverage, reference, false);
            Measuring = false;
        }

        public T Project<T>(T row, Action<T, string, object> set, bool candidate, Func<bool> coverageComplete)
        {
            foreach (var field in StatusFields(candidate, coverageComplete)) set(row, field.Key, field.Value);
            return row;
        }

        public static TReply MeasureStop<TStatus, TReply>(Func<TStatus> status, Action stop, Func<TStatus, TStatus, TReply> wrap)
        {
            var before = status();
            stop();
            return wrap(before, status());
        }

        public KeyValuePair<string, object>[] StatusFields(bool candidate, Func<bool> coverageComplete) => new[]
        {
            new KeyValuePair<string, object>("measuring", Measuring), new KeyValuePair<string, object>("error", Error ?? ""),
            new KeyValuePair<string, object>("coverage_complete", candidate ? (object)coverageComplete() : "n/a"),
            new KeyValuePair<string, object>("patch_set_changed_in_window", candidate ? (object)Window.Changed : "n/a"),
            new KeyValuePair<string, object>("stop_scan", StopScan),
        };

        private List<ObservedPatch> Observe()
        {
            scanned.Clear();
            if (watch?.Scanning != null) Error = Window.Scanned(Cache, watch, observe, scanned, attempts, Measuring, Error);
            else foreach (var target in allPatched()) observe(target, scanned);
            return scanned;
        }
    }

    public sealed class AdvanceCallBuffer
    {
        public AdvanceCallBuffer(int slowCapacity, int unitCapacity)
        {
            Slow = new AdvanceCallRecord[slowCapacity]; Unit = new AdvanceCallRecord[unitCapacity];
        }
        public AdvanceCallRecord[] Slow { get; }
        public AdvanceCallRecord[] Unit { get; }
        public int SlowCount { get; private set; }
        public int UnitCount { get; private set; }
        public long SlowDropped { get; private set; }
        public long UnitDropped { get; private set; }
        public bool Full(bool over30) => over30 ? SlowCount >= Slow.Length : UnitCount >= Unit.Length;
        public bool Add(AdvanceCallRecord record)
        {
            if (record.Over30)
            {
                if (SlowCount >= Slow.Length) { SlowDropped++; return false; }
                Slow[SlowCount++] = record; return true;
            }
            if (!record.UnitsRead) return false;
            if (UnitCount >= Unit.Length) { UnitDropped++; return false; }
            Unit[UnitCount++] = record; return true;
        }
        public int MergedCount => SlowCount + UnitCount;
        public AdvanceCallRecord[] Chronological()
        {
            var merged = new AdvanceCallRecord[MergedCount];
            int s = 0, u = 0, n = 0;
            while (s < SlowCount || u < UnitCount)
                merged[n++] = u >= UnitCount || (s < SlowCount && Slow[s].StartQpc <= Unit[u].StartQpc) ? Slow[s++] : Unit[u++];
            return merged;
        }
        public void Drop(bool over30) { if (over30) SlowDropped++; else UnitDropped++; }
        public void Reset() { SlowCount = UnitCount = 0; SlowDropped = UnitDropped = 0; }
    }

    public static class CallPage
    {
        public const int Separator = 1;
        public static int Fit(Func<int, int> rowLength, int start, int end, int budget)
        {
            int used = 0, taken = 0;
            for (int i = start; i < end; i++)
            {
                int length = rowLength(i) + Separator;
                if (taken > 0 && used + length > budget) break;
                used += length; taken++;
            }
            return taken;
        }
    }

    public static class ProbeCapacity
    {
        public const int Frames = 131072, Ticks = 131072, Calls = 4096, UnitCalls = 4096, Diagnostics = 8192;
        public const int FramePage = 96, TickPage = 256, DiagnosticPage = 32;
    }

    public sealed class RingCursor
    {
        public RingCursor(int capacity) { if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity)); Capacity = capacity; }
        public int Capacity { get; }
        public long Written { get; private set; }
        public long Drained { get; private set; }
        public long Dropped { get; private set; }
        public bool TryWrite(out int slot)
        {
            if (Written - Drained >= Capacity) { Dropped++; slot = -1; return false; }
            slot = (int)(Written % Capacity); Written++;
            return true;
        }
        public int Slot(long index) => (int)(index % Capacity);
        public bool Page(long offset, int limit, out int count)
        {
            count = 0;
            if (offset < Drained || offset > Written || limit < 0) return false;
            Drained = offset;
            count = (int)Math.Min(limit, Written - offset);
            return true;
        }
        public void Reset() { Written = Drained = Dropped = 0; }
    }

    public sealed class CollectionSeries
    {
        public const int Limit = 64;
        private readonly long[] used = new long[Limit];
        private readonly int[] frames = new int[Limit];
        private int mark = -1;
        public int Retained { get; private set; }
        public int Collections { get; private set; }
        public bool Truncated => Collections > Retained;
        public long UsedAt(int index) => index < Retained ? used[index] : throw new ArgumentOutOfRangeException(nameof(index));
        public int FrameAt(int index) => index < Retained ? frames[index] : throw new ArgumentOutOfRangeException(nameof(index));
        public long First => Retained > 0 ? used[0] : -1;
        public long Last => Retained > 0 ? used[Retained - 1] : -1;
        public long Growth => Retained > 1 ? used[Retained - 1] - used[0] : 0;
        public void Begin(int collections) { mark = collections; Retained = Collections = 0; }
        public bool Due(int collections) => mark >= 0 && collections != mark;
        public void Note(int collections, long usedBytes, int frame)
        {
            if (!Due(collections)) return;
            Collections += Math.Max(0, collections - mark);
            mark = collections;
            if (Retained == Limit) return;
            frames[Retained] = frame;
            used[Retained++] = usedBytes;
        }
    }
    public readonly struct StopRead
    {
        public StopRead(int collections, int frame, long heap, long used, long ticks) { Collections = collections; Frame = frame; Heap = heap; Used = used; Ticks = ticks; }
        public long Ticks { get; }
        public int Collections { get; }
        public int Frame { get; }
        public long Heap { get; }
        public long Used { get; }

        public void Close(CollectionSeries series, LoopPhaseRecorder phases, int frame, bool closePhases, double modWorkMs, bool late, double ticksToMs)
            => Close(series, phases, frame, closePhases, modWorkMs, late, ticksToMs, -1);

        public void Close(CollectionSeries series, LoopPhaseRecorder phases, int frame, bool closePhases, double modWorkMs, bool late, double ticksToMs, long modTicks)
        {
            series.Note(Collections, Used, Frame);
            if (closePhases) phases.Close(frame, Ticks, Collections, modWorkMs, true, late, ticksToMs, true, modTicks);
        }

        public double WholeMs(long frameStartTicks, double ticksToMs) => (Ticks - frameStartTicks) * ticksToMs;
        public int CollectionsSince(int atFrameStart) => Collections - atFrameStart;
    }

    public sealed class LoopPhaseRecorder
    {
        public const int Limit = 64, MaxPhases = 12, MaxSubPhases = 512;
        private readonly int capacity;
        private readonly long[] start;
        private readonly long[] modAt;
        private readonly int[] gcAt;
        private readonly bool[] stamped;
        private readonly double[] previous;
        private readonly double[] current;
        private readonly double[] previousMod;
        private readonly double[] currentMod;
        private readonly int[] phaseThrough;
        private readonly int[] rowPhaseThrough;
        private readonly double[] rowPhases;
        private readonly double[] rowPrevious;
        private readonly double[] rowPhaseMod;
        private readonly double[] rowPreviousMod;
        private readonly int[] rowFrame = new int[Limit], rowCollections = new int[Limit], rowGcPhase = new int[Limit], rowThrough = new int[Limit];
        private readonly double[] rowWhole = new double[Limit], rowPreviousWhole = new double[Limit], rowNextWhole = new double[Limit], rowMod = new double[Limit];
        private readonly bool[] rowDiagnostic = new bool[Limit], rowLate = new bool[Limit], rowPreviousCollected = new bool[Limit], rowPartial = new bool[Limit];
        private string[] names = Array.Empty<string>();
        private readonly int[] ranked;
        private double previousWhole = -1;
        private bool open, previousCollected, rowAwaitsNext;
        private int startCollections;

        public LoopPhaseRecorder() : this(MaxPhases) { }

        public LoopPhaseRecorder(int capacity)
        {
            if (capacity <= 0 || capacity > MaxSubPhases) throw new ArgumentOutOfRangeException(nameof(capacity));
            this.capacity = capacity;
            start = new long[capacity]; modAt = new long[capacity]; gcAt = new int[capacity]; stamped = new bool[capacity];
            previous = new double[capacity]; current = new double[capacity]; previousMod = new double[capacity]; currentMod = new double[capacity]; phaseThrough = new int[capacity];
            rowPhaseThrough = new int[Limit * capacity]; rowPhases = new double[Limit * capacity]; rowPrevious = new double[Limit * capacity];
            rowPhaseMod = new double[Limit * capacity]; rowPreviousMod = new double[Limit * capacity];
            ranked = new int[capacity];
        }

        public int Capacity => capacity;
        public int Phases => names.Length;
        public string PhaseName(int phase) => names[phase];
        public int Collections { get; private set; }
        public int Retained { get; private set; }
        public bool Truncated => Collections > Retained;

        public void Configure(string[] phaseNames)
        {
            if (phaseNames == null || phaseNames.Length == 0 || phaseNames.Length > capacity) throw new ArgumentOutOfRangeException(nameof(phaseNames));
            names = (string[])phaseNames.Clone();
            Reset();
        }

        public void Reset()
        {
            Collections = Retained = 0; open = previousCollected = rowAwaitsNext = false; previousWhole = -1;
            Array.Clear(stamped, 0, capacity); Array.Clear(previous, 0, capacity); Array.Clear(previousMod, 0, capacity);
        }

        public void Begin(long ticks, int collections) => Begin(ticks, collections, 0);

        public void Begin(long ticks, int collections, long modTicks)
        {
            Array.Clear(stamped, 0, capacity);
            open = names.Length > 0;
            startCollections = collections;
            if (open) Stamp(0, ticks, collections, modTicks);
        }

        public void Stamp(int phase, long ticks, int collections) => Stamp(phase, ticks, collections, 0);

        public void Stamp(int phase, long ticks, int collections, long modTicks)
        {
            if (!open || phase < 0 || phase >= names.Length || stamped[phase]) return;
            stamped[phase] = true; start[phase] = ticks; gcAt[phase] = collections; modAt[phase] = modTicks;
        }

        public void Close(int frame, long endTicks, int collections, double modWorkMs, bool diagnostic, bool late, double ticksToMs)
            => Close(frame, endTicks, collections, modWorkMs, diagnostic, late, ticksToMs, false);

        public void Close(int frame, long endTicks, int collections, double modWorkMs, bool diagnostic, bool late, double ticksToMs, bool partial)
            => Close(frame, endTicks, collections, modWorkMs, diagnostic, late, ticksToMs, partial, -1);

        public void Close(int frame, long endTicks, int collections, double modWorkMs, bool diagnostic, bool late, double ticksToMs, bool partial, long modTicks)
        {
            if (!open) return;
            open = false;
            int phases = names.Length;
            long next = endTicks, nextMod = modTicks; int nextGc = collections, gcPhase = -1;
            for (int p = phases - 1; p >= 0; p--)
            {
                if (!stamped[p]) { start[p] = next; gcAt[p] = nextGc; modAt[p] = nextMod; }
                current[p] = Math.Max(0, next - start[p]) * ticksToMs;
                currentMod[p] = modTicks < 0 ? 0 : Math.Max(0, nextMod - modAt[p]) * ticksToMs;
                phaseThrough[p] = nextGc;
                if (nextGc != gcAt[p]) gcPhase = p;
                next = start[p]; nextGc = gcAt[p]; nextMod = modAt[p];
            }
            double whole = Math.Max(0, endTicks - start[0]) * ticksToMs;
            bool collected = collections != startCollections;
            if (rowAwaitsNext) { rowNextWhole[Retained - 1] = collected ? -1 : whole; rowAwaitsNext = false; }
            if (collected)
            {
                Collections++;
                if (Retained < Limit)
                {
                    int r = Retained++;
                    rowFrame[r] = frame; rowWhole[r] = whole; rowMod[r] = modWorkMs; rowDiagnostic[r] = diagnostic; rowLate[r] = late; rowPartial[r] = partial;
                    rowCollections[r] = collections - startCollections; rowThrough[r] = collections; rowGcPhase[r] = gcPhase;
                    rowPreviousWhole[r] = previousCollected ? -1 : previousWhole; rowPreviousCollected[r] = previousCollected; rowNextWhole[r] = -1;
                    for (int p = 0; p < capacity; p++)
                    {
                        rowPhases[r * capacity + p] = p < phases ? current[p] : 0;
                        rowPhaseThrough[r * capacity + p] = p < phases ? phaseThrough[p] : collections;
                        rowPrevious[r * capacity + p] = p < phases && previousWhole >= 0 ? previous[p] : -1;
                        rowPhaseMod[r * capacity + p] = p < phases ? currentMod[p] : 0;
                        rowPreviousMod[r * capacity + p] = p < phases && previousWhole >= 0 ? previousMod[p] : -1;
                    }
                    rowAwaitsNext = !partial;
                }
            }
            for (int p = 0; p < phases; p++) { previous[p] = current[p]; previousMod[p] = currentMod[p]; }
            previousWhole = whole; previousCollected = collected;
        }

        public int Frame(int row) => rowFrame[Row(row)];
        public double WholeMs(int row) => rowWhole[Row(row)];
        public double PreviousWholeMs(int row) => rowPreviousWhole[Row(row)];
        public double NextWholeMs(int row) => rowNextWhole[Row(row)];
        public double ModWorkMs(int row) => rowMod[Row(row)];
        public bool Diagnostic(int row) => rowDiagnostic[Row(row)];
        public bool Late(int row) => rowLate[Row(row)];
        public bool Partial(int row) => rowPartial[Row(row)];
        public int RowCollections(int row) => rowCollections[Row(row)];
        public int Gen0Through(int row) => rowThrough[Row(row)];
        public int Gen0From(int row) => rowThrough[Row(row)] - rowCollections[Row(row)] + 1;
        public int PhaseGen0Through(int row, int phase) => rowPhaseThrough[Row(row) * capacity + Phase(phase)];
        public int PhaseGen0From(int row, int phase) => phase == 0 ? Gen0From(row) : PhaseGen0Through(row, phase - 1) + 1;
        public int PhaseCollections(int row, int phase) => PhaseGen0Through(row, phase) - PhaseGen0From(row, phase) + 1;

        public int CollectionPhase(int row, long ordinal)
        {
            for (int p = 0; p < names.Length; p++)
                if (PhaseCollections(row, p) > 0 && ordinal >= PhaseGen0From(row, p) && ordinal <= PhaseGen0Through(row, p)) return p;
            return -1;
        }

        public int RowForCollection(long ordinal)
        {
            for (int r = 0; r < Retained; r++)
                if (ordinal >= Gen0From(r) && ordinal <= Gen0Through(r)) return r;
            return -1;
        }

        public int GcPhase(int row) => rowGcPhase[Row(row)];
        public double PhaseMs(int row, int phase) => rowPhases[Row(row) * capacity + Phase(phase)];
        public double PreviousPhaseMs(int row, int phase) => rowPrevious[Row(row) * capacity + Phase(phase)];
        public double PhaseModMs(int row, int phase) => rowPhaseMod[Row(row) * capacity + Phase(phase)];
        public double PreviousPhaseModMs(int row, int phase) => rowPreviousMod[Row(row) * capacity + Phase(phase)];
        public double ExcessOutsideModMs(int row, int phase)
        {
            double before = PreviousPhaseMs(row, phase);
            return before < 0 ? double.NaN : PhaseMs(row, phase) - PhaseModMs(row, phase) - (before - PreviousPhaseModMs(row, phase));
        }

        public int LargestExcessOutsideMod(int row)
        {
            int best = -1; double most = double.NegativeInfinity;
            for (int p = 0; p < names.Length; p++)
            {
                double grew = ExcessOutsideModMs(row, p);
                if (double.IsNaN(grew)) return -1;
                if (grew > most) { most = grew; best = p; }
            }
            return best;
        }

        public double ModMs(int row)
        {
            double total = 0;
            for (int p = 0; p < names.Length; p++) total += PhaseModMs(row, p);
            return total;
        }

        public double ExcessMs(int row) => PreviousWholeMs(row) < 0 ? -1 : WholeMs(row) - PreviousWholeMs(row);

        public int LargestExcessPhase(int row)
        {
            int best = -1; double most = double.NegativeInfinity;
            for (int p = 0; p < names.Length; p++)
            {
                double before = PreviousPhaseMs(row, p);
                if (before < 0) return -1;
                double grew = PhaseMs(row, p) - before;
                if (grew > most) { most = grew; best = p; }
            }
            return best;
        }

        public int LargestExcessPhases(int row, int[] into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));
            int count = 0;
            for (int p = 0; p < names.Length; p++)
            {
                if (PreviousPhaseMs(row, p) < 0) return 0;
                ranked[count++] = p;
            }
            int taken = Math.Min(into.Length, count);
            for (int k = 0; k < taken; k++)
            {
                int best = k;
                for (int j = k + 1; j < count; j++)
                    if (Grew(row, ranked[j]) > Grew(row, ranked[best])) best = j;
                int swap = ranked[k]; ranked[k] = ranked[best]; ranked[best] = swap;
                into[k] = ranked[k];
            }
            return taken;
        }

        private double Grew(int row, int phase) => PhaseMs(row, phase) - PreviousPhaseMs(row, phase);

        public int CollectedPhases(int row, int[] into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));
            int taken = 0;
            for (int p = 0; p < names.Length && taken < into.Length; p++)
                if (PhaseCollections(row, p) > 0) into[taken++] = p;
            return taken;
        }

        private int Row(int row) => row >= 0 && row < Retained ? row : throw new ArgumentOutOfRangeException(nameof(row));
        private int Phase(int phase) => phase >= 0 && phase < names.Length ? phase : throw new ArgumentOutOfRangeException(nameof(phase));
    }

    public sealed class SubPhasePlan
    {
        public const string Tail = "loop-tail", Unstamped = ".unstamped";
        public static readonly string[] DetailedParents = { "PreUpdate", "Update", "PreLateUpdate", "PostLateUpdate", "TimeUpdate" };
        private readonly int[][] before;
        private readonly int[] rootBefore;

        private SubPhasePlan(string[] names, int[][] before, int[] rootBefore, bool truncated)
        { Names = names; this.before = before; this.rootBefore = rootBefore; Truncated = truncated; }

        public string[] Names { get; }
        public bool Truncated { get; }

        public int Before(int system, int child)
        {
            var stamps = before[system];
            return stamps == null || child < 0 || child >= stamps.Length ? -1 : stamps[child];
        }

        public int RootBefore(int system) => system < 0 || system >= rootBefore.Length ? -1 : rootBefore[system];

        public static SubPhasePlan Build(string[] systems, string[][] children, int early, ICollection<string> detailed, int capacity)
        {
            if (systems == null || children == null || children.Length != systems.Length) throw new ArgumentException("one child list per system");
            if (early < 0 || early >= systems.Length || children[early] == null) throw new ArgumentOutOfRangeException(nameof(early));
            int n = systems.Length;
            if (n + 1 > capacity) throw new ArgumentOutOfRangeException(nameof(capacity));
            var names = new List<string>();
            var before = new int[n][];
            var rootBefore = new int[n + 1];
            for (int r = 0; r <= n; r++) rootBefore[r] = -1;
            bool truncated = false;
            int Add(string name) { names.Add(name); return names.Count - 1; }
            for (int o = 0; o < n; o++)
            {
                int i = (early + o) % n;
                int later = n - 1 - o + (o <= n - 1 - early ? 1 : 0);
                var list = children[i];
                if (list == null) rootBefore[i] = Add(systems[i]);
                else
                {
                    var stamps = before[i] = new int[list.Length + 1];
                    for (int c = 0; c < stamps.Length; c++) stamps[c] = -1;
                    bool detail = detailed != null && detailed.Contains(systems[i]);
                    int firstChild = Array.FindIndex(list, c => c != null);
                    if (o == 0) Add(systems[i]);
                    else if (!detail || firstChild != 0) stamps[0] = Add(systems[i]);
                    if (detail)
                    {
                        for (int c = 0; c < list.Length; c++)
                        {
                            if (list[c] == null || (o == 0 && c == firstChild && firstChild == 0)) continue;
                            if (names.Count + 2 + later <= capacity) stamps[c] = Add(systems[i] + "." + list[c]);
                            else { stamps[c] = Add(systems[i] + Unstamped); truncated = true; break; }
                        }
                    }
                }
                if (i == n - 1) rootBefore[n] = Add(Tail);
            }
            return new SubPhasePlan(names.ToArray(), before, rootBefore, truncated);
        }
    }
}
