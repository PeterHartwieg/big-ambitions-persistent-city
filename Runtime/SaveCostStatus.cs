using PersistentCity.Persistence;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace PersistentCity.Runtime
{
    public sealed partial class PopulationRuntime
    {
        // F7a. Null unless explicitly requested: ordinary saves then pass null and do no timing work.
        private SaveCostRecorder? saveCost;
        // Requested load timings are written once per activation into primitives; nothing runs per frame.
        // Captured at load start: a request armed later times the next load, never a half-timed one.
        private bool timeActivation;
        private long activationSequence;
        private double activationReadMilliseconds, activationCreateMilliseconds, activationValidateMilliseconds, activationMainThreadMilliseconds;
        private int activationPayloadCharacters;
        private bool activationHadPayload, activationSucceeded;

        /// <summary>
        /// Arms or disarms the per-save record (rig console/bridge). Arming allocates one recorder;
        /// every later save reuses it. Disarming discards the last record.
        /// </summary>
        public bool SaveCostDiagnosticsRequested
        {
            get => saveCost != null;
            set => saveCost = value ? saveCost ?? new SaveCostRecorder() : null;
        }

        /// <summary>On-demand rig projection of the last save and the last activation; built only when read.</summary>
        public object SaveCostDiagnostics
        {
            get
            {
                var cost = saveCost;
                return new
                {
                    policy = "f7a-save-cost-v1", requested = cost != null, writerSchema = PersistentCity.Core.CityState.CurrentSchema,
                    save = cost == null || cost.Sequence == 0 ? null : new
                    {
                        sequence = cost.Sequence, success = cost.Success, error = cost.Error, lastPhase = cost.LastPhase.ToString(),
                        observeMs = cost.Milliseconds(SaveCostPhase.Observe), synchronizeMs = cost.Milliseconds(SaveCostPhase.Synchronize),
                        captureMs = cost.Milliseconds(SaveCostPhase.Capture),
                        validateMs = cost.Milliseconds(SaveCostPhase.Validate), encodeMs = cost.Milliseconds(SaveCostPhase.Encode),
                        publishMs = cost.Milliseconds(SaveCostPhase.Publish), totalMs = cost.TotalMilliseconds,
                        atLeast30Ms = cost.TotalMilliseconds >= 30,
                        payloadCharacters = cost.PayloadCharacters, payloadUtf8Bytes = cost.PayloadUtf8Bytes, payloadUtf16Bytes = cost.PayloadUtf16Bytes,
                        streamingRecords = cost.StreamingRecords, semanticRecords = cost.SemanticRecords,
                        recordCountMs = cost.CountMilliseconds, recordCountOutsidePhases = true
                    },
                    activation = activationSequence == 0 ? null : new
                    {
                        sequence = activationSequence, success = activationSucceeded, hadPayload = activationHadPayload,
                        payloadCharacters = activationPayloadCharacters,
                        backgroundReadMs = activationReadMilliseconds, backgroundCreateMs = activationCreateMilliseconds,
                        backgroundValidateMs = activationValidateMilliseconds, mainThreadMs = activationMainThreadMilliseconds,
                        mainThreadAtLeast30Ms = activationMainThreadMilliseconds >= 30
                    }
                };
            }
        }

        /// <summary>Requested-only record counts after publication; timed separately and never thrown into native Save.</summary>
        private void CountSavedRecords(SaveCostRecorder cost)
        {
            if (simulation == null || !SaveGameManager.Current.modData.TryGetValue(SaveStore.StateKey, out var payload)) return;
            long start = Stopwatch.GetTimestamp();
            try
            {
                // F9e named exception: the save codec counts records of the state it wrote.
                var counts = SaveCodec.CountRecords(simulation.State, payload);
                cost.Counts(counts.Streaming, counts.Semantic, (Stopwatch.GetTimestamp() - start) * MillisecondsPerTimestamp);
            }
            catch (System.Exception error) { log?.Warn("Save-cost record count failed: " + error.Message); }
        }

        private void RecordActivation(long started, bool succeeded, double read, double create, double validate)
        {
            activationSequence++;
            activationSucceeded = succeeded;
            activationReadMilliseconds = read; activationCreateMilliseconds = create; activationValidateMilliseconds = validate;
            activationMainThreadMilliseconds = (Stopwatch.GetTimestamp() - started) * MillisecondsPerTimestamp;
        }
    }
}
