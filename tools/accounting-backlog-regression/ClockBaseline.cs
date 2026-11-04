using System;
using System.Text.Json;
using PersistentCity.Core;

internal static class ClockBaseline
{
    private static object Measure(int fps, double rate, int minutes, int policy)
    {
        int hour = 24, ticks = 0, clips = 0; float minute = 0;
        double end = 1440 + minutes, withheld = 0;
        while (hour * 60.0 + minute < end)
        {
            if (++ticks > 5000000) throw new Exception("Zero-work baseline failed to progress");
            double native = hour * 60.0 + minute;
            float requested = (float)Math.Min(rate / fps, end - native), allowed = requested;
            if (policy == 1) allowed = AccountingClock.Limit(true, native, requested);
            if (policy == 2)
            {
                // Frozen pre-review minute policy, for the baseline comparison only.
                float fence = MathF.BitDecrement(MathF.Floor(minute) + 1);
                float stop = minute >= fence ? MathF.Floor(minute) + 1 : fence;
                allowed = Math.Min(requested, stop - minute);
                if (minute + allowed > stop) allowed = MathF.BitDecrement(allowed);
            }
            if (policy == 3)
            {
                // Removed legacy hour-granular skip policy, frozen as a benchmark control.
                double remaining = 60 - minute;
                double step = remaining > .0002 ? remaining - .0001 : remaining + .0001;
                allowed = (float)Math.Min(requested, step);
            }
            if (allowed < requested) { clips++; withheld += requested - allowed; }
            minute += allowed; while (minute >= 60) { minute -= 60; hour++; }
        }
        return new { baseline = true, policy, fps, requestedGameMinutesPerSecond = rate, logicalMinutes = minutes,
            ticks, simulatedWallSeconds = ticks / (double)fps, clips, withheldGameMinutes = withheld };
    }
    public static void Run()
    {
        foreach (int fps in new[] { 30, 60, 120 })
            foreach (double rate in new[] { 1.0 / 3, 2.0 })
                foreach (int policy in new[] { 0, 2, 1 }) Console.WriteLine(JsonSerializer.Serialize(Measure(fps, rate, 1440, policy)));
        foreach (int policy in new[] { 0, 3, 2, 1 }) Console.WriteLine(JsonSerializer.Serialize(Measure(60, 120 * 60, 480, policy)));
    }
}
