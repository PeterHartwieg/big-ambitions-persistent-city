using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace PersistentCity.Core
{
    public enum ClockRetimeStage { None, ReleaseBodies, RetimeAndRebuild }

    public enum ClockRetimeOutcome { None, ReleaseBodies, Began, Preparing, Restarted, Committed }

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
        public string RetimedFrom { get; private set; } = "";

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
                Pending = TravelTiming.Begin(state, walkMetresPerSecond, nativeRate, source, sharedLegHint);
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
            Degraded = Pending.Degraded; Split = Pending.Split; Joined = Pending.Joined; JoinsRefused = Pending.JoinsRefused; SharedLegs = Pending.SharedLegs; ReusedLegs = Pending.ReusedLegs; sharedLegHint = Pending.SharedPool;
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
            finished = false; Restarts = 0; Slices = 0; SnapshotSlices = 0; SliceMilliseconds = SlowestSliceMilliseconds = 0; Degraded = 0; Split = Joined = JoinsRefused = 0; RetimedTraffic = null; SharedLegs = ReusedLegs = 0; RetimedFrom = "";
        }
    }
}
