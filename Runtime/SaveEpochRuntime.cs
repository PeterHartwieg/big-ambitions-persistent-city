using System;
using PersistentCity.Persistence;

namespace PersistentCity.Runtime
{
    public sealed partial class PopulationRuntime
    {
        private SaveEpochBinding? saveEpoch;
        private SaveEpochBinding? lastSaveEpoch;
        private int saveEpochUnits;
        private bool saveEpochStopPending;

        public object SaveEpochDiagnostics => new {
            policy = "save-epoch-v1", attached = saveEpoch != null,
            sequence = lastSaveEpoch?.Sequence ?? 0, published = lastSaveEpoch?.Published ?? 0,
            refused = lastSaveEpoch?.Refused ?? 0, deferred = lastSaveEpoch?.Deferred ?? 0, lastRefusal = lastSaveEpoch?.LastRefusal ?? "",
            detached = lastSaveEpoch?.Detached ?? false, stopPending = saveEpochStopPending
        };

        private void Update()
        {
            long gcFrame = GcFrameBegin();
            saveEpochUnits++;
            try { UpdateCity(); }
            finally
            {
                GcFrameEnd(gcFrame);
                saveEpochUnits--;
                if (saveEpochStopPending && saveEpochUnits == 0) FinishDeferredSaveEpochStop();
            }
        }

        internal void RunSaveEpochUnit(Action unit)
        {
            saveEpochUnits++;
            try { unit(); }
            finally
            {
                saveEpochUnits--;
                if (saveEpochStopPending && saveEpochUnits == 0) FinishDeferredSaveEpochStop();
            }
        }

        private void Save()
        {
            var save = SaveGameManager.Current;
            var result = SaveEpochCallback.Run(saveEpoch, save, save?.modData, saveEpochUnits > 0,
                CaptureForSave, StopAfterSaveEpochFailure, message => log?.Warn(message));
            if (result == SaveEpochResult.Reentrant) saveEpochStopPending = true;
            if (saveEpochStopPending && saveEpochUnits == 0 && saveEpoch?.Saving != true) FinishDeferredSaveEpochStop();
        }

        private void StopAfterSaveEpochFailure(string reason)
        {
            if (!failed) StopAccounting("Save epoch refused: " + reason);
        }

        private void FinishDeferredSaveEpochStop()
        {
            saveEpochStopPending = false;
            try { StopAfterSaveEpochFailure("A native save started while a Persistent City unit was executing."); }
            catch (Exception error) { log?.Warn("Persistent City stop after a re-entrant save was contained: " + error); }
        }

        private void BeginInitializingSaveEpoch(object save, System.Collections.Generic.IDictionary<string, string> data, long sequence)
        {
            DropSaveEpoch();
            lastSaveEpoch = saveEpoch = new SaveEpochBinding(save, data, sequence, initializing: true);
            GlobalEvents.onSaveGame += Save;
            GlobalEvents.onGameUnloaded += Shutdown;
            subscribed = true;
        }

        private void DropSaveEpoch()
        {
            var epoch = saveEpoch;
            saveEpoch = null;
            epoch?.Detach(null);
            if (!subscribed) return;
            GlobalEvents.onSaveGame -= Save;
            GlobalEvents.onGameUnloaded -= Shutdown;
            subscribed = false;
        }

        private void DetachSaveEpoch()
        {
            var epoch = saveEpoch;
            saveEpoch = null;
            if (epoch != null && epoch.Detach(SaveGameManager.Current))
                log?.Warn("Persistent City detached from the current save; later native saves of this city keep a refusal marker.");
        }
    }
}
