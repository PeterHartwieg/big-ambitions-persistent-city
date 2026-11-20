using System.Diagnostics;

namespace PersistentCity.Core
{
    public sealed class FoodContinuationMaximum
    {
        private long started;
        public double Milliseconds { get; private set; }
        public string Step { get; private set; } = "";

        public void Begin() => started = Stopwatch.GetTimestamp();

        public void End(bool food, string step)
        {
            if (!food) return;
            double elapsed = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
            if (elapsed > Milliseconds) { Milliseconds = elapsed; Step = step; }
        }

        public void Reset() { Milliseconds = 0; Step = ""; }
    }
}
