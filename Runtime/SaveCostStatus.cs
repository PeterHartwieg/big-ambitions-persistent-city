using System;
using PersistentCity.Persistence;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace PersistentCity.Runtime
{
    public sealed partial class PopulationRuntime
    {
        private SaveCostRecorder? saveCost;
        private bool timeActivation;
        private long activationSequence;
        private double activationReadMilliseconds, activationCreateMilliseconds, activationValidateMilliseconds, activationMainThreadMilliseconds;
        private int activationPayloadCharacters;
        private bool activationHadPayload, activationSucceeded;

        public bool SaveCostDiagnosticsRequested
        {
            get => saveCost != null;
            set => saveCost = value ? saveCost ?? new SaveCostRecorder() : null;
        }

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

        private void CountSavedRecords(SaveCostRecorder cost, Func<string, (int Streaming, int Semantic)> count)
        {
            if (simulation == null || !SaveGameManager.Current.modData.TryGetValue(SaveStore.StateKey, out var payload)) return;
            long start = Stopwatch.GetTimestamp();
            try
            {
                var counts = count(payload);
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
