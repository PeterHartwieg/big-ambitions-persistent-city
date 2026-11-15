using System.Threading.Tasks;

namespace PersistentCity.Runtime
{
    public sealed partial class PopulationRuntime
    {
        private void Fail(string message)
        {
            failed = true;
            status = message;
            log?.Warn("Persistent City stopped: " + message);
            Detach();
        }

        private void ResetPopulationForSaveChange()
        {
            physical?.Dispose(); physical = null; physicalFailed = false;
            inspector.Reset(); inspectorFailed = false;
            Detach();
            if (initialization != null)
                _ = initialization.ContinueWith(task => { _ = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            initialization = null; initializingSave = null; initializingPayload = null; initializingHadPayload = false;
            startupHold.Reset();
            simulation = null; saveSession = null; stopEnvironment = null; pendingRetime = null; retimeRestarts = 0;
            routes = null; clockRetimingRoutes = false; saveIdentity = null; RoadRoutes.ForgetSnapshot();
            accountingText = ""; nextAccountingText = 0;
            economyRefresh.Reset();
        }
    }
}
