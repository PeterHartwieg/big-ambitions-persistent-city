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
        /// <summary>On-demand rig projection; normal tick paths construct no diagnostic objects/strings.</summary>
        public object AccountingDiagnostics => new {
            policy = "minute-epoch-drain-v1", configuredCalendarFactor = NativeCityClock.Factor,
            nativeMinute = SaveGameManager.Current == null ? 0 : Now(), logicalMinute = simulation?.State.Minute,
            caughtUpToTarget = simulation != null && SaveGameManager.Current != null && simulation.IsCaughtUpTo(AccountingClock.Target(Now())),
            lagMinutes = simulation == null || SaveGameManager.Current == null ? 0
                : System.Math.Max(0, AccountingClock.Target(Now()) - simulation.State.Minute),
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
            if (NativeCityClock.Refused) return "Persistent City is stopping: accounting epoch changed. Native calendar continues; historical purchases are not replayed. Restore a synchronized checkpoint to resume Persistent City.";
            if (UnityEngine.Time.unscaledTime < nextAccountingText) return accountingText;
            nextAccountingText = UnityEngine.Time.unscaledTime + .25f;
            accountingText = "City accounting uses calendar holds to preserve resident demand; current lag "
                + NativeCityClock.LagMinutes.ToString("F3", CultureInfo.InvariantCulture) + " game minutes. Configured calendar factor "
                + NativeCityClock.Factor.ToString("F3", CultureInfo.InvariantCulture) + ".";
            return accountingText;
        }
    }
}
