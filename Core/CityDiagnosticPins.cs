using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public sealed class FoodDecisionRecorder
    {
        public const int Capacity = 50;
        private readonly Queue<string> lines = new Queue<string>();
        private readonly Func<FoodDecisionTrace, string?> format;
        private readonly Action<Exception> failed;
        private CitySimulator? observed;

        public FoodDecisionRecorder(Func<FoodDecisionTrace, string?> format, Action<Exception> failed)
        {
            this.format = format ?? throw new ArgumentNullException(nameof(format));
            this.failed = failed ?? throw new ArgumentNullException(nameof(failed));
        }

        public bool Recording => observed != null;
        public IEnumerable<string> Lines => lines;
        internal CitySimulator? Observed => observed;

        public void Start(CitySimulator city)
        {
            if (city == null) throw new ArgumentNullException(nameof(city));
            if (ReferenceEquals(observed, city)) return;
            Stop();
            observed = city;
            city.FoodDecisionObserved += Record;
        }

        public void Stop()
        {
            if (observed != null) observed.FoodDecisionObserved -= Record;
            observed = null;
        }

        public void Rebind(CitySimulator city)
        {
            var current = observed;
            if (current == null || city == null || ReferenceEquals(current, city)) return;
            current.FoodDecisionObserved -= Record;
            observed = city;
            city.FoodDecisionObserved += Record;
        }

        public void Detach() { Stop(); lines.Clear(); }

        private void Record(FoodDecisionTrace trace)
        {
            try
            {
                string? line = format(trace);
                if (line == null) return;
                if (lines.Count == Capacity) lines.Dequeue();
                lines.Enqueue(line);
            }
            catch (Exception error) { Stop(); failed(error); }
        }
    }

    public sealed class ResearchSnapshotCache
    {
        public const double LifetimeSeconds = .25;
        private ResearchLedger? snapshot;
        private CitySimulator? city;
        private double until;

        internal CitySimulator? City => city;

        public ResearchLedger? Get(CitySimulator? current, double nowSeconds)
        {
            if (current == null) { Forget(); return null; }
            if (!ReferenceEquals(current, city) || nowSeconds >= until)
            { snapshot = CityViewReports.ResearchSnapshot(current.View); city = current; until = nowSeconds + LifetimeSeconds; }
            return snapshot;
        }

        public void Forget() { snapshot = null; city = null; }
    }
}
