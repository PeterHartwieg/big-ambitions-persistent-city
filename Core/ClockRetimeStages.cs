using System;

namespace PersistentCity.Core
{
    public enum ClockRetimeStage { None, ReleaseBodies, RetimeJourneys, RebuildSimulation }

    public static class ClockRetimeStages
    {
        public const double Tolerance = .000001;

        public static ClockRetimeStage Next(double nativeRate, double stateRate, double routesRate, bool bodiesLive)
        {
            if (Math.Abs(nativeRate - stateRate) > Tolerance) return bodiesLive ? ClockRetimeStage.ReleaseBodies : ClockRetimeStage.RetimeJourneys;
            return Math.Abs(routesRate - stateRate) > Tolerance ? ClockRetimeStage.RebuildSimulation : ClockRetimeStage.None;
        }
    }
}
