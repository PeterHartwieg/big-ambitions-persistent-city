using PersistentCity.Persistence;

namespace PersistentCity.Runtime
{
    public sealed partial class PopulationRuntime
    {
        private object? accountingFailureSave;
        // Runs before Update's failure gate. Other failures retain their existing refusal;
        // only an accounting stop may be left by loading a different save object.
        private bool PrepareAccountingSaveChange()
        {
            if (!failed) return true;
            var current = SaveGameManager.Current;
            if (!accountingStopped || current == null || UI.Load.LoadScene.isLoading
                || ReferenceEquals(accountingFailureSave, current)) return false;
            accountingStopped = false; accountingFailureSave = null;
            failed = false; status = "";
            ResetPopulationForSaveChange();
            nextStart = 0;
            return CanStartAccountingSave();
        }
        private bool CanStartAccountingSave()
        {
            if (SaveGameManager.Current?.modData != null && AccountingStopStore.IsStopped(SaveGameManager.Current.modData))
            { StopAccounting(); return false; }
            return true;
        }
        private bool BeginAccountingObservations()
        {
            double native = Now();
            if (simulation != null && NativeCityClock.CheckInputEpoch(native, simulation.State.Minute)
                && simulation.BeginAccountingObservations(native)) return true;
            StopAccounting(); return false;
        }
        private void StopAccounting()
        {
            accountingStopped = true;
            accountingFailureSave = SaveGameManager.Current;
            AccountingStopStore.Stop(SaveGameManager.Current.modData);
            try { NativeFoodOwnership.ReleaseAfterAccountingFailure(); }
            finally { Fail(AccountingRecoveryText + " Accounting input epoch was lost. Native calendar continues; Persistent City is stopped. "
                + "Unprocessed Persistent City demand is lost, not replayed. The original population payload is retained. "
                + "Native saves remain playable with Persistent City stopped."); }
        }
    }
}
