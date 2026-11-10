using PersistentCity.Persistence;

namespace PersistentCity.Runtime
{
    public sealed partial class PopulationRuntime
    {
        private object? accountingFailureSave, developmentRefusedSave;
        // Runs before Update's failure gate. Other failures retain their existing refusal; only an accounting stop or
        // a refused older development city (F5c-lite) may be left by loading a different save object.
        private bool PrepareAccountingSaveChange()
        {
            if (!failed) return true;
            var current = SaveGameManager.Current;
            // A stopped accounting line or a refused development city stays stopped for its own save only.
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
            // F5c-lite: refuse an older development city before any accounting, epoch or authority write.
            if (SaveGameManager.Current?.modData != null)
            {
                NativeFoodOwnership.EnsureSave();
                if (NativeFoodOwnership.StandDown != null) { RefuseDevelopmentCity(); return false; }
            }
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
