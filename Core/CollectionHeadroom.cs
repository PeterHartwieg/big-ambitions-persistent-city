using System;
using System.Globalization;
using System.Text;

namespace PersistentCity.Core
{
    public sealed class CollectionHeadroom
    {
        public const long MarginBytes = 256L * 1024;
        public const long MaximumBurstBytes = 4L * 1024 * 1024;
        public const double BurstDecay = 0.99;
        public const long MinimumCycleBytes = 16L * 1024 * 1024;
        public const int MaximumDeferredUpdates = 8;
        public const int History = 16;

        private long lastBytes = -1, cycleLive = -1, predicted = -1;
        private int lastCollections = -1, deferredThisCycle;
        private double ratio = double.NaN;
        private long burst;
        private readonly long[] predictedHistory = new long[History], observedHistory = new long[History];
        private readonly int[] deferredHistory = new int[History];
        private long recorded;

        public long Predicted => predicted;
        public long Burst => burst;
        public double Ratio => ratio;
        public long Cycles { get; private set; }
        public long DeferredUpdates { get; private set; }
        public long DeferredCycles { get; private set; }
        public long CappedCycles { get; private set; }
        public long Invalidated { get; private set; }
        public long TornSamples { get; private set; }
        public long StaleDeferrals { get; private set; }

        public static bool ClockAllowsDeferral(bool caughtUpLastUpdate, double nativeMinute, float nextTickMinutes)
        {
            if (!caughtUpLastUpdate || AccountingClock.Target(nativeMinute) != nativeMinute) return false;
            if (!Position.Finite(nextTickMinutes) || nextTickMinutes < 0) return false;
            double ahead = nativeMinute + 2.0 * nextTickMinutes;
            return Math.Floor(ahead) == Math.Floor(nativeMinute) && AccountingClock.Target(ahead) == ahead;
        }

        public int CarriedUnits { get; private set; }
        public int CarriedCitizenEvents { get; private set; }
        public bool CatchingUp => CarriedCitizenEvents > 0;

        public void Carry(PopulationFrameBudget skipped)
        {
            CarriedUnits = Math.Min(PopulationFrameBudget.MaximumUnits, CarriedUnits + skipped.Units);
            CarriedCitizenEvents += skipped.CitizenEvents;
        }

        public (int Units, int CitizenEvents) Limits(PopulationFrameBudget current)
            => (Math.Min(PopulationFrameBudget.MaximumUnits, current.Units + CarriedUnits), current.CitizenEvents + CarriedCitizenEvents);

        public void Advanced(bool caughtUp) { if (caughtUp) CarriedUnits = CarriedCitizenEvents = 0; }

        public static bool ClockReady(bool caughtUp, bool guardDeferred, double logicalMinute, double nativeMinute)
            => caughtUp || guardDeferred && AccountingClock.Target(nativeMinute) == nativeMinute && AccountingClock.Compatible(logicalMinute, nativeMinute);

        public void Observe(int collectionsBefore, long bytesSinceCollection, long liveBytes, int collectionsAfter)
        {
            if (collectionsBefore != collectionsAfter) { TornSamples++; return; }
            Observe(bytesSinceCollection, liveBytes, collectionsAfter);
        }

        public void Observe(long bytesSinceCollection, long liveBytes, int collections)
        {
            if (bytesSinceCollection < 0 || collections < 0) { Forget(); return; }
            if (lastCollections < 0) { lastBytes = bytesSinceCollection; lastCollections = collections; return; }
            if (collections != lastCollections)
            {
                if (collections == lastCollections + 1 && lastBytes >= MinimumCycleBytes && cycleLive > 0)
                {
                    int slot = (int)(recorded++ % History);
                    predictedHistory[slot] = predicted; observedHistory[slot] = lastBytes; deferredHistory[slot] = deferredThisCycle;
                    ratio = (double)lastBytes / cycleLive;
                    Cycles++;
                }
                if (deferredThisCycle > 0) DeferredCycles++;
                deferredThisCycle = 0;
                cycleLive = collections == lastCollections + 1 && liveBytes > 0 ? liveBytes : -1;
                predicted = cycleLive > 0 && !double.IsNaN(ratio) ? (long)(ratio * cycleLive) : -1;
            }
            else if (bytesSinceCollection < lastBytes) { Invalidated++; cycleLive = -1; predicted = -1; }
            lastBytes = bytesSinceCollection; lastCollections = collections;
        }

        public void NoteAdvance(long allocatedBytes)
        {
            if (allocatedBytes < 0) return;
            long decayed = (long)(burst * BurstDecay);
            burst = Math.Min(MaximumBurstBytes, Math.Max(allocatedBytes, decayed));
        }

        public bool Defer(int collectionsBefore, long bytesSinceCollection, int collectionsAfter)
        {
            if (collectionsBefore != collectionsAfter || collectionsAfter != lastCollections) { StaleDeferrals++; return false; }
            return Defer(bytesSinceCollection);
        }

        public bool Defer(long bytesSinceCollection)
        {
            if (predicted < MinimumCycleBytes || bytesSinceCollection < 0 || bytesSinceCollection + burst < predicted - MarginBytes) return false;
            if (deferredThisCycle >= MaximumDeferredUpdates)
            {
                if (deferredThisCycle == MaximumDeferredUpdates) { CappedCycles++; deferredThisCycle++; }
                return false;
            }
            deferredThisCycle++; DeferredUpdates++;
            return true;
        }

        public void ResetCounters()
        {
            Cycles = DeferredUpdates = DeferredCycles = CappedCycles = Invalidated = TornSamples = StaleDeferrals = 0;
            recorded = 0;
        }

        private void Forget() { lastBytes = -1; lastCollections = -1; cycleLive = -1; predicted = -1; deferredThisCycle = 0; }

        public int RecordedCycles => (int)Math.Min(recorded, History);

        public (long Predicted, long Observed, int Deferred) Cycle(int index)
        {
            int count = RecordedCycles;
            if (index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
            int slot = (int)((recorded - count + index) % History);
            return (predictedHistory[slot], observedHistory[slot], deferredHistory[slot]);
        }

        public string ReportJson(bool available)
        {
            var text = new StringBuilder(256 + History * 64);
            string N(long value) => value.ToString(CultureInfo.InvariantCulture);
            text.Append("{\"available\":").Append(available ? "true" : "false")
                .Append(",\"margin_bytes\":").Append(N(MarginBytes)).Append(",\"maximum_deferred_updates\":").Append(N(MaximumDeferredUpdates))
                .Append(",\"cycles\":").Append(N(Cycles)).Append(",\"deferred_updates\":").Append(N(DeferredUpdates))
                .Append(",\"deferred_cycles\":").Append(N(DeferredCycles)).Append(",\"capped_cycles\":").Append(N(CappedCycles))
                .Append(",\"invalidated\":").Append(N(Invalidated)).Append(",\"torn_samples\":").Append(N(TornSamples)).Append(",\"stale_deferrals\":").Append(N(StaleDeferrals)).Append(",\"advance_burst_bytes\":").Append(N(burst)).Append(",\"predicted_bytes\":").Append(N(predicted))
                .Append(",\"ratio\":").Append(double.IsNaN(ratio) ? "null" : ratio.ToString("0.######", CultureInfo.InvariantCulture))
                .Append(",\"recent_cycles\":[");
            for (int i = 0; i < RecordedCycles; i++)
            {
                var cycle = Cycle(i);
                if (i > 0) text.Append(',');
                text.Append("{\"predicted_bytes\":").Append(N(cycle.Predicted)).Append(",\"observed_bytes\":").Append(N(cycle.Observed))
                    .Append(",\"error_bytes\":").Append(cycle.Predicted < 0 ? "null" : N(cycle.Observed - cycle.Predicted))
                    .Append(",\"deferred_updates\":").Append(N(cycle.Deferred)).Append('}');
            }
            return text.Append("]}").ToString();
        }
    }
}
