using System;
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
        private bool finished;

        public TravelTiming.TravelRetime? Pending { get; private set; }
        public bool Holding => Pending != null;
        public int Restarts { get; private set; }
        public int Slices { get; private set; }
        public double SliceMilliseconds { get; private set; }
        public double SlowestSliceMilliseconds { get; private set; }
        public int Degraded { get; private set; }
        public TravelTiming.RetimedTraffic? RetimedTraffic { get; private set; }

        public ClockRetimeOutcome Update(CityState state, double nativeRate, bool bodiesLive, double walkMetresPerSecond, double budgetMilliseconds)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var stage = ClockRetimeStages.Next(nativeRate, state.TravelClockRate, bodiesLive);
            if (stage == ClockRetimeStage.None) { Cancel(); return ClockRetimeOutcome.None; }
            if (stage == ClockRetimeStage.ReleaseBodies) return ClockRetimeOutcome.ReleaseBodies;
            if (Pending != null && (Pending.ClockRate != nativeRate || !ReferenceEquals(Pending.State, state))) Cancel();
            bool restarted = false;
            if (Pending != null && Pending.Stale) { Pending = null; Restarts++; restarted = true; }
            if (Pending == null)
            {
                if (finished) Reset();
                state.PrepareForSave();
                Pending = TravelTiming.Begin(state, walkMetresPerSecond, nativeRate);
                if (Pending == null) { Degraded = 0; finished = true; return ClockRetimeOutcome.Committed; }
                return restarted ? ClockRetimeOutcome.Restarted : ClockRetimeOutcome.Began;
            }
            if (!Pending.Ready)
            {
                long started = Stopwatch.GetTimestamp();
                Pending.Step(started + (long)(budgetMilliseconds * Stopwatch.Frequency / 1000));
                double slice = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
                Slices++; SliceMilliseconds += slice; SlowestSliceMilliseconds = Math.Max(SlowestSliceMilliseconds, slice);
                return ClockRetimeOutcome.Preparing;
            }
            if (!Pending.Commit()) { Pending = null; Restarts++; return ClockRetimeOutcome.Restarted; }
            Degraded = Pending.Degraded; RetimedTraffic = Pending.TakeRestoredTraffic(); Pending = null; finished = true;
            return ClockRetimeOutcome.Committed;
        }

        public void Cancel() { Pending = null; Reset(); }

        public TravelTiming.RetimedTraffic? TakeRetimedTraffic()
        {
            var taken = RetimedTraffic;
            RetimedTraffic = null;
            return taken;
        }

        private void Reset()
        {
            finished = false; Restarts = 0; Slices = 0; SliceMilliseconds = SlowestSliceMilliseconds = 0; Degraded = 0; RetimedTraffic = null;
        }
    }
}
