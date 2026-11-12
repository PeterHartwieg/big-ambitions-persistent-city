using System.Globalization;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    public sealed partial class PopulationRuntime
    {
        private float nextAccountingText;
        private string accountingText = "";
        private bool accountingStopped = false;
        internal const string PreparingAccountingText = "Preparing Persistent City. Calendar held until population and routes are ready.";
        internal const string BoundaryAccountingText = "Calendar boundary caps apply. Ctrl+F8: pacing details.";
        internal const string AccountingRecoveryText = "Load a synchronized pre-failure checkpoint or another city to restart Persistent City. This stopped save stays refused; native time continues.";
        public object AccountingDiagnostics => new {
            policy = "minute-epoch-drain-v1", configuredCalendarFactor = NativeCityClock.Factor,
            nativeMinute = SaveGameManager.Current == null ? 0 : Now(), logicalMinute = simulation?.View.Minute,
            caughtUpToTarget = simulation != null && SaveGameManager.Current != null && simulation.IsCaughtUpTo(AccountingClock.Target(Now())),
            lagMinutes = simulation == null || SaveGameManager.Current == null ? 0
                : System.Math.Max(0, AccountingClock.Target(Now()) - simulation.View.Minute),
            maxLagMinutes = NativeCityClock.MaximumLagMinutes,
            supportedMaxLagMinutes = AccountingClock.MaximumLagMinutes,
            heldOrdinaryTicks = NativeCityClock.HeldOrdinaryTicks, heldSkipTicks = NativeCityClock.HeldEconomySkipTicks,
            boundaryClips = NativeCityClock.BoundaryClips, withheldGameMinutes = NativeCityClock.WithheldMinutes,
            waitingSeconds = NativeCityClock.WaitingSeconds, maxRecoverySeconds = NativeCityClock.MaximumRecoverySeconds,
            refused = NativeCityClock.Refused, pacingAffected = NativeCityClock.PacingAffected,
            stoppedAfterAccountingFailure = accountingStopped,
            historicalClockMisses = simulation?.HistoricalClockMisses ?? 0
        };
        private string AccountingStatusText()
        {
            if (NativeCityClock.Refused) return AccountingRecoveryText + " Persistent City is stopping: accounting epoch changed. Historical purchases are not replayed.";
            if (UnityEngine.Time.unscaledTime < nextAccountingText) return accountingText;
            nextAccountingText = UnityEngine.Time.unscaledTime + .25f;
            accountingText = "City accounting uses calendar holds to preserve resident demand; current lag "
                + NativeCityClock.LagMinutes.ToString("F3", CultureInfo.InvariantCulture) + " game minutes. Configured calendar factor "
                + NativeCityClock.Factor.ToString("F3", CultureInfo.InvariantCulture) + ".";
            return accountingText;
        }
    }
}
