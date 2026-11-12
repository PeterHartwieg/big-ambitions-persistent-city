using PersistentCity.Persistence;

namespace PersistentCity.Runtime
{
    public sealed partial class PopulationRuntime
    {
        private object? accountingFailureSave, developmentRefusedSave;
        private bool PrepareAccountingSaveChange()
        {
            if (!failed) return true;
            var current = SaveGameManager.Current;
            var refusedSave = accountingStopped ? accountingFailureSave : developmentRefusedSave;
            if (!accountingStopped && developmentRefusedSave == null || current == null || UI.Load.LoadScene.isLoading
                || ReferenceEquals(refusedSave, current)) return false;
            accountingStopped = false; accountingFailureSave = null; developmentRefusedSave = null;
            failed = false; status = "";
            ResetPopulationForSaveChange();
            nextStart = 0;
            return CanStartAccountingSave();
        }
        private bool CanStartAccountingSave()
        {
            if (SaveGameManager.Current?.modData != null)
            {
                NativeFoodOwnership.EnsureSave();
                if (NativeFoodOwnership.StandDown != null) { RefuseDevelopmentCity(); return false; }
            }
            if (SaveGameManager.Current?.modData != null && AccountingStopStore.IsStopped(SaveGameManager.Current.modData))
            { StopAccounting(AccountingStopStore.Cause(SaveGameManager.Current.modData)); return false; }
            return true;
        }
        private bool BeginAccountingObservations()
        {
            double native = Now();
            if (simulation != null && NativeCityClock.CheckInputEpoch(native, simulation.State.Minute)
                && simulation.BeginAccountingObservations(native)) return true;
            StopAccounting(); return false;
        }
        private void RefuseDevelopmentCity()
        {
            developmentRefusedSave = SaveGameManager.Current;
            Fail(SaveCodec.DevelopmentSchemaMessage + " Persistent City takes no food ownership; owned shops return to native customers after the current hour.");
        }
        private void StopAccounting(string cause = "Accounting input epoch was lost.")
        {
            accountingStopped = true;
            accountingFailureSave = SaveGameManager.Current;
            AccountingStopStore.Stop(SaveGameManager.Current.modData);
            try { NativeFoodOwnership.ReleaseAfterAccountingFailure(); }
            finally { Fail(AccountingRecoveryText + " " + cause + " Native calendar continues; Persistent City is stopped. "
                + "Unprocessed Persistent City demand is lost, not replayed. The original population payload is retained. "
                + "Native saves remain playable with Persistent City stopped."); }
        }
    }
}
