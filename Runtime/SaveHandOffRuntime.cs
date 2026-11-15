using System;
using PersistentCity.Persistence;

namespace PersistentCity.Runtime
{
    public sealed partial class PopulationRuntime
    {
        private const int SettleWaitMilliseconds = 30000;
        private PendingSave? pendingSave, splicingSave, queuedSave, publishingSave;
        private SaveCostRecorder? writerCost;
        private bool backgroundSaveDisabled;
        private string backgroundSaveStatus = "";
        private long backgroundSaves, synchronousSaves, settledSaves, splicedSaves;
        private bool lastSaveBackground;
        private double lastWriterMilliseconds, lastCompressWaitMilliseconds, lastCompressMilliseconds;
        private SaveSpliceOutcome lastSplice;
        private string lastSpliceError = "";
        private long writerCostSequence;

        private bool BackgroundSaveAvailable()
        {
            if (backgroundSaveDisabled) return false;
            string? conflict = SaveHandOff.RefusalOutstanding ? "A cancelled save hand-off still waits for its native compressor."
                : SaveHandOff.PublicationOutstanding ? "An earlier save is still being published by its native compressor." : NativeSaveHandOff.Conflict();
            if (conflict == null) return true;
            if (!string.Equals(conflict, backgroundSaveStatus, StringComparison.Ordinal))
                log?.Warn("Persistent City writes this save on the main thread: " + conflict);
            backgroundSaveStatus = conflict;
            return false;
        }

        private void PollHandOff()
        {
            if (pendingSave != null && pendingSave.Resolved) SettlePendingSave(false);
            if (splicingSave != null && splicingSave.CompressionSettled) ReportSplice(splicingSave, true);
            ReportPublication(true);
        }

        private void ReportPublication(bool stopOnRefusal)
        {
            if (publishingSave == null || !publishingSave.CompressionSettled) return;
            publishingSave = null;
            var settled = SaveHandOff.TakeSettledPublication();
            if (settled != null) ReportSplice(settled, stopOnRefusal);
        }

        private void SettlePendingSave(bool wait, bool stopOnRefusal = true)
        {
            var pending = pendingSave;
            if (pending == null) return;
            if (!pending.Resolved)
            {
                if (!wait) return;
                pending.WaitOrAbort(SettleWaitMilliseconds, "The background save writer did not finish within "
                    + (SettleWaitMilliseconds / 1000) + " s; the save carries a refusal marker.");
            }
            pendingSave = null;
            settledSaves++;
            lastWriterMilliseconds = pending.WriteMilliseconds;
            if (pending.Cost != null) writerCostSequence = pending.Sequence;
            var session = saveSession;
            var epoch = saveEpoch ?? lastSaveEpoch;
            string? refusal = null;
            if (session != null && ReferenceEquals(session.InFlight, pending))
            {
                var stored = session.Settle(pending);
                if (!stored.Success) refusal = stored.Error;
            }
            else if (!pending.Succeeded) refusal = pending.Error;
            if (epoch != null && epoch.Owns(pending.Target) && !epoch.Settle(pending, refusal) && refusal == null) refusal = epoch.LastRefusal;
            pending.Release();
            if (refusal == null) return;
            log?.Warn("Persistent City save refused: " + refusal);
            if (!stopOnRefusal) return;
            try { StopAfterSaveEpochFailure(refusal); }
            catch (Exception error) { log?.Warn("Persistent City stop after a refused background save was contained: " + error); }
        }

        private void ReportSplice(PendingSave spliced, bool stopOnRefusal)
        {
            splicingSave = null;
            splicedSaves++;
            lastSplice = spliced.Splice;
            lastSpliceError = spliced.SpliceError;
            lastCompressWaitMilliseconds = spliced.CompressWaitMilliseconds;
            lastCompressMilliseconds = spliced.CompressMilliseconds;
            string? refusal = spliced.FileRefusal;
            if (refusal == null) return;
            backgroundSaveDisabled = true;
            backgroundSaveStatus = refusal + " Later saves write on the main thread.";
            log?.Warn("Persistent City save refused: " + backgroundSaveStatus);
            if (!stopOnRefusal) return;
            try { StopAfterSaveEpochFailure(refusal); }
            catch (Exception error) { log?.Warn("Persistent City stop after a refused background save file was contained: " + error); }
        }

        private void AwaitSplice(bool stopOnRefusal)
        {
            var splicing = splicingSave;
            if (splicing == null) return;
            if (!SaveHandOff.AwaitCompression(splicing))
                log?.Warn("Persistent City save publication did not finish within " + SaveHandOff.PublishWaitMilliseconds / 1000
                    + " s; it keeps its file and later saves write on the main thread until it finishes.");
            if (splicing.CompressionSettled) ReportSplice(splicing, stopOnRefusal);
            else
            {
                splicingSave = null;
                if (SaveHandOff.PublicationOutstanding) publishingSave = splicing;
            }
        }

        private void BeginSaveHandOff()
        {
            SettlePendingSave(true);
            AwaitSplice(true);
            ReportPublication(true);
            SaveHandOff.Clear();
            queuedSave = null;
        }

        private void QueueSaveHandOff()
        {
            var queued = queuedSave;
            queuedSave = null;
            if (queued == null) return;
            string? temp = null;
            try { temp = SaveGamePathHelper.GetTempSavePath(); }
            catch (Exception error) { log?.Warn("Native temporary save path is unavailable; the next compression takes the payload: " + error.Message); }
            SaveHandOff.Expect(queued, temp);
            pendingSave = splicingSave = queued;
        }

        private SaveEpochCapture BeginBackgroundWrite(SaveSession session, SaveCostRecorder? cost)
        {
            var data = SaveGameManager.Current.modData;
            var writer = cost == null ? null : writerCost ??= new SaveCostRecorder();
            var result = session.BeginWrite(data, simulation!.State, SaveHandOffWriter.Shared, cost, writer, out var pending);
            if (!result.Success || pending == null) return SaveEpochCapture.Refused("Population save refused: " + result.Error);
            queuedSave = pending;
            backgroundSaves++;
            lastSaveBackground = true;
            return SaveEpochCapture.Queued(pending);
        }

        private void FlushSaveHandOff()
        {
            try
            {
                SettlePendingSave(true, false);
                AwaitSplice(false);
                ReportPublication(false);
                SaveHandOff.Clear();
            }
            catch (Exception error) { log?.Warn("Persistent City background save flush failed: " + error.Message); }
        }

        private void OnApplicationQuit() => FlushSaveHandOff();

        private object? WriterDiagnostics()
        {
            var cost = writerCost;
            if (cost == null || writerCostSequence == 0 || pendingSave != null) return null;
            return new
            {
                sequence = writerCostSequence, success = cost.Success, error = cost.Error, totalMs = lastWriterMilliseconds,
                validateMs = cost.Milliseconds(SaveCostPhase.Validate), encodeMs = cost.Milliseconds(SaveCostPhase.Encode),
                markerMs = cost.Milliseconds(SaveCostPhase.Publish),
                encodeSectionsMs = new
                {
                    header = cost.SectionMilliseconds(SaveCostSection.Header), places = cost.SectionMilliseconds(SaveCostSection.Places),
                    residents = cost.SectionMilliseconds(SaveCostSection.Residents), visitors = cost.SectionMilliseconds(SaveCostSection.Visitors),
                    traffic = cost.SectionMilliseconds(SaveCostSection.Traffic), food = cost.SectionMilliseconds(SaveCostSection.Food),
                    economy = cost.SectionMilliseconds(SaveCostSection.Economy)
                },
                validateSectionsMs = new
                {
                    header = cost.ValidateSectionMilliseconds(SaveCostSection.Header), places = cost.ValidateSectionMilliseconds(SaveCostSection.Places),
                    residents = cost.ValidateSectionMilliseconds(SaveCostSection.Residents), food = cost.ValidateSectionMilliseconds(SaveCostSection.Food),
                    traffic = cost.ValidateSectionMilliseconds(SaveCostSection.Traffic), economy = cost.ValidateSectionMilliseconds(SaveCostSection.Economy)
                },
                payloadCharacters = cost.PayloadCharacters, payloadUtf8Bytes = cost.PayloadUtf8Bytes,
                streamingRecords = cost.StreamingRecords, semanticRecords = cost.SemanticRecords, recordCountMs = cost.CountMilliseconds
            };
        }

        private object HandOffDiagnostics() => new
        {
            mode = lastSaveBackground ? "background" : "main-thread", available = NativeSaveHandOff.Installed && !backgroundSaveDisabled,
            status = backgroundSaveStatus.Length > 0 ? backgroundSaveStatus : NativeSaveHandOff.Status,
            backgroundSaves, synchronousSaves, settledSaves, splicedSaves, inFlight = pendingSave != null,
            writer = WriterDiagnostics(),
            compress = splicedSaves == 0 ? null : new
            {
                splice = lastSplice.ToString(), error = lastSpliceError,
                waitedForWriterMs = lastCompressWaitMilliseconds, spliceAndCompressMs = lastCompressMilliseconds
            },
            nativeSave = NativeSaveHandOff.NativeSaves == 0 ? null : new
            {
                saves = NativeSaveHandOff.NativeSaves, lastMainThreadMs = NativeSaveHandOff.LastNativeSaveMilliseconds
            }
        };
    }
}
