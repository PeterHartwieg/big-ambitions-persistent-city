using System.Globalization;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    public sealed partial class PopulationRuntime
    {
        /// <summary>On-demand rig projection; normal tick paths construct no diagnostic objects/strings.</summary>
        public object AccountingDiagnostics => new {
            policy = "minute-epoch-drain-v1", configuredCalendarFactor = NativeCityClock.Factor,
            nativeMinute = SaveGameManager.Current == null ? 0 : Now(), logicalMinute = simulation?.State.Minute,
            lagMinutes = NativeCityClock.LagMinutes, maxLagMinutes = NativeCityClock.MaximumLagMinutes,
            supportedMaxLagMinutes = AccountingClock.MaximumLagMinutes,
            heldOrdinaryTicks = NativeCityClock.HeldOrdinaryTicks, heldSkipTicks = NativeCityClock.HeldEconomySkipTicks,
            boundaryClips = NativeCityClock.BoundaryClips, withheldGameMinutes = NativeCityClock.WithheldMinutes,
            waitingSeconds = NativeCityClock.WaitingSeconds, maxRecoverySeconds = NativeCityClock.MaximumRecoverySeconds,
            refused = NativeCityClock.Refused, pacingAffected = NativeCityClock.PacingAffected,
            historicalClockMisses = simulation?.HistoricalClockMisses ?? 0
        };
        private string AccountingStatusText() => NativeCityClock.Refused
            ? "City accounting paused: the native clock left the simulation's input epoch. Historical purchases cannot be replayed safely. Reload a synchronized save."
            : "City accounting uses calendar holds to preserve resident demand; current lag "
                + NativeCityClock.LagMinutes.ToString("F3", CultureInfo.InvariantCulture) + " game minutes. Configured calendar factor "
                + NativeCityClock.Factor.ToString("F3", CultureInfo.InvariantCulture) + ".";
    }
}
