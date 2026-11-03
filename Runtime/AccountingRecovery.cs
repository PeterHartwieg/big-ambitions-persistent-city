using PersistentCity.Persistence;

namespace PersistentCity.Runtime
{
    public sealed partial class PopulationRuntime
    {
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
            AccountingStopStore.Stop(SaveGameManager.Current.modData);
            try { NativeFoodOwnership.ReleaseAfterAccountingFailure(); }
            finally { Fail("Accounting input epoch was lost. Native calendar continues; Persistent City is stopped. "
                + "Unprocessed Persistent City demand is lost, not replayed. The original population payload is retained. "
                + "Native saves remain playable with Persistent City stopped; restore a synchronized checkpoint to resume it."); }
        }
    }
}
