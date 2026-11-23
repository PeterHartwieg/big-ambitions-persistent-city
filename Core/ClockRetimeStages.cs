using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace PersistentCity.Core
{
    public enum ClockRetimeStage { None, ReleaseBodies, RetimeAndRebuild }

    public enum ClockRetimeOutcome { None, ReleaseBodies, Began, Preparing, Restarted, Committed }

    public enum RetimeFrameStage { Routes, Release, Journeys, Commit, Rebuild, HandOver, Report }

    public sealed class RetimeFrameLedger
    {
        private const int Stages = 7;
        private const int RetimeStages = 6;
        public const double BoundMilliseconds = 11.3;
        public const int ListedOverBound = 8;
        private readonly int[] overFrames = new int[ListedOverBound];
        private readonly RetimeFrameStage[] overStages = new RetimeFrameStage[ListedOverBound];
        private readonly double[] overMilliseconds = new double[ListedOverBound];
        private readonly string[] overDetails = new string[ListedOverBound];
        private static readonly string[] Names = { "routes", "body release", "journey retime", "commit", "rebuild", "hand-over", "report" };
        private readonly double[] slowest = new double[Stages];
        private readonly int[] updates = new int[Stages];
        private readonly string[] details = new string[Stages];

        public bool Observed { get; private set; }
        public int OverBound { get; private set; }
        public int OverBoundFrame(int index) => index >= 0 && index < Math.Min(OverBound, ListedOverBound) ? overFrames[index] : throw new ArgumentOutOfRangeException(nameof(index));
        public RetimeFrameStage OverBoundStage(int index) => index >= 0 && index < Math.Min(OverBound, ListedOverBound) ? overStages[index] : throw new ArgumentOutOfRangeException(nameof(index));
        public double OverBoundMilliseconds(int index) => index >= 0 && index < Math.Min(OverBound, ListedOverBound) ? overMilliseconds[index] : throw new ArgumentOutOfRangeException(nameof(index));
        public int Updates(RetimeFrameStage stage) => updates[(int)stage];
        public double SlowestMilliseconds(RetimeFrameStage stage) => slowest[(int)stage];
        public string SlowestDetail(RetimeFrameStage stage) => details[(int)stage] ?? "";

        public RetimeFrameStage SlowestStage
        {
            get
            {
                int found = 0;
                for (int i = 1; i < RetimeStages; i++) if (slowest[i] > slowest[found]) found = i;
                return (RetimeFrameStage)found;
            }
        }

        public void Observe(RetimeFrameStage stage, double milliseconds, string detail = "", int frame = -1)
        {
            if (double.IsNaN(milliseconds) || milliseconds < 0) milliseconds = 0;
            int index = (int)stage;
            updates[index]++; Observed = true;
            if (stage == RetimeFrameStage.Report) ReportFrame = frame;
            if (updates[index] == 1 || milliseconds > slowest[index]) { slowest[index] = milliseconds; details[index] = detail; }
            if (milliseconds <= BoundMilliseconds) return;
            if (OverBound < ListedOverBound)
            {
                overFrames[OverBound] = frame; overStages[OverBound] = stage; overMilliseconds[OverBound] = milliseconds; overDetails[OverBound] = detail;
            }
            OverBound++;
        }

        public int ReportFrame { get; private set; } = -1;
        public bool ReportOverBound => updates[(int)RetimeFrameStage.Report] > 0 && slowest[(int)RetimeFrameStage.Report] > BoundMilliseconds;

        public string DescribeReport()
        {
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            int index = (int)RetimeFrameStage.Report;
            return "Clock retime report: frame " + ReportFrame.ToString(culture) + " report " + slowest[index].ToString("0.0", culture) + " ms, over "
                + BoundMilliseconds.ToString("0.0", culture) + " ms (" + (details[index] ?? "") + ").";
        }

        public void Reset()
        {
            if (!Observed) return;
            Array.Clear(slowest, 0, Stages); Array.Clear(updates, 0, Stages); Array.Clear(details, 0, Stages);
            Array.Clear(overDetails, 0, ListedOverBound);
            Observed = false; OverBound = 0; ReportFrame = -1;
        }

        public string Describe()
        {
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            var text = new System.Text.StringBuilder("Clock retime frames: slowest update ");
            var top = SlowestStage;
            text.Append(slowest[(int)top].ToString("0.0", culture)).Append(" ms in ").Append(Names[(int)top]);
            for (int i = 0; i < RetimeStages; i++)
            {
                text.Append(i == 0 ? "; " : ", ").Append(Names[i]).Append(' ').Append(updates[i].ToString(culture))
                    .Append(updates[i] == 1 ? " update" : " updates");
                if (updates[i] == 0) continue;
                text.Append(" slowest ").Append(slowest[i].ToString("0.0", culture)).Append(" ms");
                if (!string.IsNullOrEmpty(details[i])) text.Append(" (").Append(details[i]).Append(')');
            }
            if (OverBound == 0) return text.Append("; no update over ").Append(BoundMilliseconds.ToString("0.0", culture)).Append(" ms.").ToString();
            text.Append("; updates over ").Append(BoundMilliseconds.ToString("0.0", culture)).Append(" ms:");
            int listed = Math.Min(OverBound, ListedOverBound);
            for (int i = 0; i < listed; i++)
            {
                text.Append(i == 0 ? " " : ", ").Append("frame ").Append(overFrames[i].ToString(culture)).Append(' ').Append(Names[(int)overStages[i]])
                    .Append(' ').Append(overMilliseconds[i].ToString("0.0", culture)).Append(" ms");
                if (!string.IsNullOrEmpty(overDetails[i])) text.Append(" (").Append(overDetails[i]).Append(')');
            }
            if (OverBound > listed) text.Append(" and ").Append((OverBound - listed).ToString(culture)).Append(" more");
            return text.Append('.').ToString();
        }
    }

    public static class ClockRetimeStages
    {
        public const double Tolerance = .000001;
        public const double SliceMilliseconds = 4;

        public static ClockRetimeStage Next(double nativeRate, double stateRate, bool bodiesLive)
        {
            if (Math.Abs(nativeRate - stateRate) <= Tolerance) return ClockRetimeStage.None;
            return bodiesLive ? ClockRetimeStage.ReleaseBodies : ClockRetimeStage.RetimeAndRebuild;
        }
    }

    public sealed class ClockRetimeDriver
    {
        public const int SnapshotLinksPerStep = 256;
        private bool finished, restartPending;
        private int sharedLegHint;
        private IEnumerator<bool>? snapshot;
        private CityState? snapshotState;
        private double snapshotMinute;
        private TrafficKernel? snapshotTraffic;
        private long snapshotRevision;

        public TravelTiming.TravelRetime? Pending { get; private set; }
        public bool Holding => Pending != null || snapshot != null;
        public int Restarts { get; private set; }
        public int Slices { get; private set; }
        public int SnapshotSlices { get; private set; }
        public double SliceMilliseconds { get; private set; }
        public double SlowestSliceMilliseconds { get; private set; }
        public int Degraded { get; private set; }
        public int Split { get; private set; }
        public int Joined { get; private set; }
        public int JoinsRefused { get; private set; }
        public TravelTiming.RetimedTraffic? RetimedTraffic { get; private set; }
        public int SharedLegs { get; private set; }
        public int ReusedLegs { get; private set; }
        public long SourceLegsExamined { get; private set; }
        public string RetimedFrom { get; private set; } = "";
        internal bool CoalesceReversals { get; set; } = true;

        public ClockRetimeOutcome Update(CityState state, double nativeRate, bool bodiesLive, double walkMetresPerSecond, double budgetMilliseconds)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var stage = ClockRetimeStages.Next(nativeRate, state.TravelClockRate, bodiesLive);
            if (stage == ClockRetimeStage.None) { Cancel(); return ClockRetimeOutcome.None; }
            if (stage == ClockRetimeStage.ReleaseBodies) return ClockRetimeOutcome.ReleaseBodies;
            if (Pending != null && (Pending.ClockRate != nativeRate || !ReferenceEquals(Pending.State, state))) Cancel();
            if (Pending != null && Pending.Stale) { Pending = null; Restarts++; restartPending = true; }
            if (Pending == null)
            {
                if (snapshot != null && (!ReferenceEquals(snapshotState, state) || snapshotMinute != state.Minute
                    || !state.LiveTrafficUnchanged(snapshotTraffic, snapshotRevision)))
                { snapshot = null; snapshotState = null; snapshotTraffic = null; Restarts++; restartPending = true; }
                if (snapshot == null)
                {
                    if (finished) Reset();
                    snapshot = state.PrepareForRetimeInSteps(SnapshotLinksPerStep);
                    snapshotState = state; snapshotMinute = state.Minute;
                }
                long started = Stopwatch.GetTimestamp();
                long deadline = started + (long)(budgetMilliseconds * Stopwatch.Frequency / 1000);
                while (true)
                {
                    if (!snapshot.MoveNext()) throw new InvalidOperationException("The retime snapshot ended without a result.");
                    if (snapshot.Current) break;
                    if (Stopwatch.GetTimestamp() >= deadline)
                    {
                        CountSlice(started); SnapshotSlices++;
                        snapshotTraffic = state.CurrentLiveTraffic(out snapshotRevision);
                        return ClockRetimeOutcome.Preparing;
                    }
                }
                snapshot = null; snapshotState = null; snapshotTraffic = null;
                bool restarted = restartPending;
                restartPending = false;
                var source = state.TakeRetimeSource();
                RetimedFrom = source == null ? "a save snapshot" : source.Kernel == null ? "no traffic" : source.WithoutThroughCars ? "a copy without through cars" : "the live kernel";
                Pending = TravelTiming.Begin(state, walkMetresPerSecond, nativeRate, source, sharedLegHint, CoalesceReversals);
                CountSlice(started);
                if (Pending == null) { Degraded = 0; finished = true; return ClockRetimeOutcome.Committed; }
                return restarted ? ClockRetimeOutcome.Restarted : ClockRetimeOutcome.Began;
            }
            if (!Pending.Ready)
            {
                long started = Stopwatch.GetTimestamp();
                Pending.Step(started + (long)(budgetMilliseconds * Stopwatch.Frequency / 1000));
                CountSlice(started);
                return ClockRetimeOutcome.Preparing;
            }
            if (!Pending.Commit()) { Pending = null; Restarts++; return ClockRetimeOutcome.Restarted; }
            Degraded = Pending.Degraded; Split = Pending.Split; Joined = Pending.Joined; JoinsRefused = Pending.JoinsRefused; SharedLegs = Pending.SharedLegs; ReusedLegs = Pending.ReusedLegs; SourceLegsExamined = Pending.SourceLegsExamined; sharedLegHint = Pending.SharedPool;
            RetimedTraffic = Pending.TakeRestoredTraffic(); Pending = null; finished = true;
            return ClockRetimeOutcome.Committed;
        }

        private void CountSlice(long started)
        {
            double slice = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
            Slices++; SliceMilliseconds += slice; SlowestSliceMilliseconds = Math.Max(SlowestSliceMilliseconds, slice);
        }

        public void Cancel() { Pending = null; snapshot = null; snapshotState = null; snapshotTraffic = null; restartPending = false; Reset(); }

        public TravelTiming.RetimedTraffic? TakeRetimedTraffic()
        {
            var taken = RetimedTraffic;
            RetimedTraffic = null;
            return taken;
        }

        private void Reset()
        {
            finished = false; Restarts = 0; Slices = 0; SnapshotSlices = 0; SliceMilliseconds = SlowestSliceMilliseconds = 0; Degraded = 0; Split = Joined = JoinsRefused = 0; RetimedTraffic = null; SharedLegs = ReusedLegs = 0; SourceLegsExamined = 0; RetimedFrom = "";
        }
    }
}
