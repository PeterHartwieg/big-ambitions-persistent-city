using System;
using PersistentCity.Persistence;

namespace PersistentCity.Runtime
{
    /// <summary>F7b exact save epoch: binding, re-entrancy guard and detach markers (plan F7 section 3.3).</summary>
    public sealed partial class PopulationRuntime
    {
        private SaveEpochBinding? saveEpoch;
        // Retained after detach for diagnostics only; never written through.
        private SaveEpochBinding? lastSaveEpoch;
        // PC units, checkout and initialization all run inside Update; a native save there is re-entrant.
        private int saveEpochUnits;
        private bool saveEpochStopPending;

        /// <summary>On-demand rig projection; no frame path builds it.</summary>
        public object SaveEpochDiagnostics => new {
            policy = "save-epoch-v1", attached = saveEpoch != null,
            sequence = lastSaveEpoch?.Sequence ?? 0, published = lastSaveEpoch?.Published ?? 0,
            refused = lastSaveEpoch?.Refused ?? 0, deferred = lastSaveEpoch?.Deferred ?? 0, lastRefusal = lastSaveEpoch?.LastRefusal ?? "",
            detached = lastSaveEpoch?.Detached ?? false, stopPending = saveEpochStopPending
        };

        private void Update()
        {
            saveEpochUnits++;
            try { UpdateCity(); }
            finally
            {
                saveEpochUnits--;
                if (saveEpochStopPending && saveEpochUnits == 0) FinishDeferredSaveEpochStop();
            }
        }

        /// <summary>Rule 5 for a PC unit that native code runs outside Update (C5d: one research billing attempt
        /// inside native MarketingHelper.RunDaily). A save started inside it writes both failure markers, publishes
        /// no payload and stops Persistent City after the outermost unit returns.</summary>
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

        /// <summary>Binds the save epoch and subscribes the callbacks when initialization starts.</summary>
        private void BeginInitializingSaveEpoch(object save, System.Collections.Generic.IDictionary<string, string> data, long sequence)
        {
            DropSaveEpoch();
            lastSaveEpoch = saveEpoch = new SaveEpochBinding(save, data, sequence, initializing: true);
            GlobalEvents.onSaveGame += Save;
            GlobalEvents.onGameUnloaded += Shutdown;
            subscribed = true;
        }

        /// <summary>Abandoned initialization for a replaced save object: unsubscribe without writing anything.</summary>
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

        /// <summary>Rule 9: before any teardown, mark the still-current save object so a later native save is refused.</summary>
        private void DetachSaveEpoch()
        {
            var epoch = saveEpoch;
            saveEpoch = null;
            if (epoch != null && epoch.Detach(SaveGameManager.Current))
                log?.Warn("Persistent City detached from the current save; later native saves of this city keep a refusal marker.");
        }
    }
}
