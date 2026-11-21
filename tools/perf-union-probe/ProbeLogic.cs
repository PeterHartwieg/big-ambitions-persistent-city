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
        public int Frame;
        public long Qpc;
        public bool Held, BoundaryClip, AccountingLimited, CountersReset;
        public double WithheldMinutes, NativeMinute, LogicalMinute;
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
        double MaximumUnitSeamMilliseconds { get; }
        double P95UnitSeamMilliseconds();
        double UnitSeamMilliseconds(int unit);
        string UnitKind(int unit);
        int NextUnitOverThreshold(int from);
    }

    public sealed class ReflectedUnitSeams : IUnitSeamSource
    {
        private PropertyInfo units, completed, slowestKind, slowestMs, slowestSeam, maximumSeam;
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

        public void Enter(long now) { if (depth++ == 0) unionStart = now; }
        public void Exit(int scope, long start, long now)
        {
            long duration = Math.Max(0, now - start);
            calls[scope]++; ticks[scope] += duration;
            if (duration > maxTicks[scope]) maxTicks[scope] = duration;
            if (duration >= spikeTicks) spikes[scope]++;
            if (depth == 0) { Underflows++; return; }
            if (--depth == 0) FrameTicks += Math.Max(0, now - unionStart);
        }
        public void Component(int component, long start, long now, bool insideUnion)
        {
            if (!insideUnion) ComponentsOutsideUnion++;
            frameComponentTicks[component] += Math.Max(0, now - start);
        }
        public void ResetFrame() { FrameTicks = 0; Array.Clear(frameComponentTicks, 0, frameComponentTicks.Length); }
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
        public const int Frames = 131072, Ticks = 262144, Calls = 4096, UnitCalls = 4096, Diagnostics = 8192;
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
    public sealed class LoopPhaseRecorder
    {
        public const int Limit = 64, MaxPhases = 12;
        private readonly long[] start = new long[MaxPhases];
        private readonly int[] gcAt = new int[MaxPhases];
        private readonly bool[] stamped = new bool[MaxPhases];
        private readonly double[] previous = new double[MaxPhases];
        private readonly double[] current = new double[MaxPhases];
        private readonly double[] rowPhases = new double[Limit * MaxPhases];
        private readonly double[] rowPrevious = new double[Limit * MaxPhases];
        private readonly int[] rowFrame = new int[Limit], rowCollections = new int[Limit], rowGcPhase = new int[Limit];
        private readonly double[] rowWhole = new double[Limit], rowPreviousWhole = new double[Limit], rowNextWhole = new double[Limit], rowMod = new double[Limit];
        private readonly bool[] rowDiagnostic = new bool[Limit], rowLate = new bool[Limit], rowPreviousCollected = new bool[Limit];
        private string[] names = Array.Empty<string>();
        private double previousWhole = -1;
        private bool open, previousCollected, rowAwaitsNext;
        private int startCollections;

        public int Phases => names.Length;
        public string PhaseName(int phase) => names[phase];
        public int Collections { get; private set; }
        public int Retained { get; private set; }
        public bool Truncated => Collections > Retained;

        public void Configure(string[] phaseNames)
        {
            if (phaseNames == null || phaseNames.Length == 0 || phaseNames.Length > MaxPhases) throw new ArgumentOutOfRangeException(nameof(phaseNames));
            names = (string[])phaseNames.Clone();
            Reset();
        }

        public void Reset()
        {
            Collections = Retained = 0; open = previousCollected = rowAwaitsNext = false; previousWhole = -1;
            Array.Clear(stamped, 0, MaxPhases); Array.Clear(previous, 0, MaxPhases);
        }

        public void Begin(long ticks, int collections)
        {
            Array.Clear(stamped, 0, MaxPhases);
            open = names.Length > 0;
            startCollections = collections;
            if (open) Stamp(0, ticks, collections);
        }

        public void Stamp(int phase, long ticks, int collections)
        {
            if (!open || phase < 0 || phase >= names.Length || stamped[phase]) return;
            stamped[phase] = true; start[phase] = ticks; gcAt[phase] = collections;
        }

        public void Close(int frame, long endTicks, int collections, double modWorkMs, bool diagnostic, bool late, double ticksToMs)
        {
            if (!open) return;
            open = false;
            int phases = names.Length;
            long next = endTicks; int nextGc = collections, gcPhase = -1;
            for (int p = phases - 1; p >= 0; p--)
            {
                if (!stamped[p]) { start[p] = next; gcAt[p] = nextGc; }
                current[p] = Math.Max(0, next - start[p]) * ticksToMs;
                if (nextGc != gcAt[p]) gcPhase = p;
                next = start[p]; nextGc = gcAt[p];
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
                    rowFrame[r] = frame; rowWhole[r] = whole; rowMod[r] = modWorkMs; rowDiagnostic[r] = diagnostic; rowLate[r] = late;
                    rowCollections[r] = collections - startCollections; rowGcPhase[r] = gcPhase;
                    rowPreviousWhole[r] = previousCollected ? -1 : previousWhole; rowPreviousCollected[r] = previousCollected; rowNextWhole[r] = -1;
                    for (int p = 0; p < MaxPhases; p++)
                    {
                        rowPhases[r * MaxPhases + p] = p < phases ? current[p] : 0;
                        rowPrevious[r * MaxPhases + p] = p < phases && previousWhole >= 0 ? previous[p] : -1;
                    }
                    rowAwaitsNext = true;
                }
            }
            for (int p = 0; p < phases; p++) previous[p] = current[p];
            previousWhole = whole; previousCollected = collected;
        }

        public int Frame(int row) => rowFrame[Row(row)];
        public double WholeMs(int row) => rowWhole[Row(row)];
        public double PreviousWholeMs(int row) => rowPreviousWhole[Row(row)];
        public double NextWholeMs(int row) => rowNextWhole[Row(row)];
        public double ModWorkMs(int row) => rowMod[Row(row)];
        public bool Diagnostic(int row) => rowDiagnostic[Row(row)];
        public bool Late(int row) => rowLate[Row(row)];
        public int RowCollections(int row) => rowCollections[Row(row)];
        public int GcPhase(int row) => rowGcPhase[Row(row)];
        public double PhaseMs(int row, int phase) => rowPhases[Row(row) * MaxPhases + Phase(phase)];
        public double PreviousPhaseMs(int row, int phase) => rowPrevious[Row(row) * MaxPhases + Phase(phase)];

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

        private int Row(int row) => row >= 0 && row < Retained ? row : throw new ArgumentOutOfRangeException(nameof(row));
        private int Phase(int phase) => phase >= 0 && phase < names.Length ? phase : throw new ArgumentOutOfRangeException(nameof(phase));
    }
}
