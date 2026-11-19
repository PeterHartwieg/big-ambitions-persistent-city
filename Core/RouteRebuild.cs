using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace PersistentCity.Core
{
    public sealed class RouteRebuild
    {
        private readonly CitySimulator replacement;
        private CitySimulator? source;
        private IEnumerator<bool>? steps;

        internal RouteRebuild(CitySimulator source, CitySimulator replacement, IEnumerator<bool> steps)
        {
            this.source = source;
            this.replacement = replacement;
            this.steps = steps;
        }

        public CitySimulator TakeResult()
        {
            var result = Result ?? throw new InvalidOperationException("The route rebuild has not finished.");
            var retired = source;
            source = null;
            if (retired == null) return result;
            long started = Stopwatch.GetTimestamp();
            result.ActiveJourneys.TakeSpareCells(retired.ActiveJourneys);
            retired.ReleasePlannerHooks(result);
            double elapsed = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
            Milliseconds += elapsed;
            HandOverMilliseconds = elapsed;
            lastUpdateMilliseconds += elapsed;
            SlowestUpdateMilliseconds = Math.Max(SlowestUpdateMilliseconds, lastUpdateMilliseconds);
            return result;
        }

        public CitySimulator? Result { get; private set; }
        public int Updates { get; private set; }
        public int Units { get; private set; }
        public double Milliseconds { get; private set; }
        public double SlowestUpdateMilliseconds { get; private set; }
        public double HandOverMilliseconds { get; private set; }
        private double lastUpdateMilliseconds;

        public bool Step(long deadlineTimestamp)
        {
            if (Result != null) return true;
            if (steps == null) throw new InvalidOperationException("The route rebuild failed; begin a new one.");
            long started = Stopwatch.GetTimestamp();
            Updates++;
            try
            {
                while (true)
                {
                    bool more = steps.MoveNext();
                    Units++;
                    if (!more || steps.Current) { steps = null; Result = replacement; return true; }
                    if (Stopwatch.GetTimestamp() >= deadlineTimestamp) return false;
                }
            }
            catch { steps = null; throw; }
            finally
            {
                double elapsed = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
                Milliseconds += elapsed;
                lastUpdateMilliseconds = elapsed;
                SlowestUpdateMilliseconds = Math.Max(SlowestUpdateMilliseconds, elapsed);
            }
        }
    }
}
