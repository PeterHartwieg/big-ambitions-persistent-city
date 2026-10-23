using System;

namespace PersistentCity.Core
{
    /// <summary>Infer calendar pace from complete frames. A boundary-clipped tick is not a new travel policy.</summary>
    public sealed class ClockRateTracker
    {
        private int frame = -1;
        private double minutes, seconds;
        private double? candidate;
        public double? Rate { get; private set; }
        public bool Held { get; private set; }
        public string Error { get; private set; } = "";

        public void Observe(int currentFrame, double gameMinutes, double scaledSeconds)
        {
            if (!Position.Finite(gameMinutes) || gameMinutes < 0 || !Position.Finite(scaledSeconds) || scaledSeconds <= 0)
            { Error = "Unsupported native clock sample: negative or nonfinite minutes, or invalid frame duration."; return; }
            if (frame != currentFrame)
            {
                if (frame >= 0) CompleteFrame();
                frame = currentFrame; minutes = 0; seconds = scaledSeconds;
            }
            minutes += gameMinutes;
            Held = minutes == 0;
        }
        private void CompleteFrame()
        {
            double rate = minutes / seconds;
            if (rate == 0) { candidate = null; return; }
            if (Rate.HasValue && Math.Abs(rate - Rate.Value) <= .000001)
            { candidate = null; Error = ""; return; }
            // Two complete consecutive frames must agree. Preserve the last positive pace
            // through pauses and a one-off capped tick; do not rebuild every resident's routes.
            if (!candidate.HasValue || Math.Abs(rate - candidate.Value) > .000001)
            { candidate = rate; return; }
            if (!Position.Finite(rate) || rate < .01 || rate > 1000)
            { Error = "Unsupported sustained native clock rate (supported .01–1000 game minutes per scaled second)."; return; }
            Rate = rate; candidate = null; Error = "";
        }
    }
}
