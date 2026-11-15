using System;

namespace PersistentCity.Core
{
    public enum ClockRetimeStage { None, ReleaseBodies, RetimeAndRebuild }

    public static class ClockRetimeStages
    {
        public const double Tolerance = .000001;

        public static ClockRetimeStage Next(double nativeRate, double stateRate, bool bodiesLive)
        {
            if (Math.Abs(nativeRate - stateRate) <= Tolerance) return ClockRetimeStage.None;
            return bodiesLive ? ClockRetimeStage.ReleaseBodies : ClockRetimeStage.RetimeAndRebuild;
        }
    }
}
