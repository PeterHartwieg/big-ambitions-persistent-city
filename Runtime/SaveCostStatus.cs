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
        private int saveGen0, saveGen1, saveGen2, saveThroughCars;
        private long saveAllocatedBytes;

        private readonly struct SaveHeap
        {
            public SaveHeap(long allocated, int gen0, int gen1, int gen2) { Allocated = allocated; Gen0 = gen0; Gen1 = gen1; Gen2 = gen2; }
            public long Allocated { get; }
            public int Gen0 { get; }
            public int Gen1 { get; }
            public int Gen2 { get; }
        }

        private SaveHeap BeginSaveHeap()
        {
            saveThroughCars = simulation?.ThroughCarCount ?? 0;
            return new SaveHeap(GcAllocationCounter.Read(), GcCount(0), GcCount(1), GcCount(2));
        }

        private void EndSaveHeap(SaveHeap start)
        {
            saveAllocatedBytes = GcAllocationCounter.Read() - start.Allocated;
            saveGen0 = GcCount(0) - start.Gen0; saveGen1 = GcCount(1) - start.Gen1; saveGen2 = GcCount(2) - start.Gen2;
        }

        public bool SaveCostDiagnosticsRequested
        {
            get => saveCost != null;
            set
            {
                if (value && saveCost == null) GcAllocationCounter.Probe();
                saveCost = value ? saveCost ?? new SaveCostRecorder() : null;
                if (!value) { writerCost = null; writerCostSequence = 0; }
                NativeSaveHandOff.TimeNativeSave = value;
            }
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
                        observeStepsMs = new
                        {
                            accounting = cost.StepMilliseconds(SaveCostStep.Accounting), rlo = cost.StepMilliseconds(SaveCostStep.Rlo),
                            physical = cost.StepMilliseconds(SaveCostStep.Physical), foodMarket = cost.StepMilliseconds(SaveCostStep.FoodMarket),
                            epochCheck = cost.StepMilliseconds(SaveCostStep.EpochCheck), vendors = cost.StepMilliseconds(SaveCostStep.Vendors),
                            handOffCheck = cost.StepMilliseconds(SaveCostStep.HandOffCheck)
                        },
                        synchronizeStepsMs = new
                        {
                            prepare = cost.StepMilliseconds(SaveCostStep.Prepare), outflow = cost.StepMilliseconds(SaveCostStep.Outflow),
                            names = cost.StepMilliseconds(SaveCostStep.Names), wallets = cost.StepMilliseconds(SaveCostStep.Wallets)
                        },
                        captureSectionsMs = new
                        {
                            header = cost.CaptureSectionMilliseconds(SnapshotSection.Header), places = cost.CaptureSectionMilliseconds(SnapshotSection.Places),
                            residents = cost.CaptureSectionMilliseconds(SnapshotSection.Residents), visitors = cost.CaptureSectionMilliseconds(SnapshotSection.Visitors),
                            traffic = cost.CaptureSectionMilliseconds(SnapshotSection.Traffic), food = cost.CaptureSectionMilliseconds(SnapshotSection.Food),
                            research = cost.CaptureSectionMilliseconds(SnapshotSection.Research), vendors = cost.CaptureSectionMilliseconds(SnapshotSection.Vendors),
                            economy = cost.CaptureSectionMilliseconds(SnapshotSection.Economy), linkWallets = cost.StepMilliseconds(SaveCostStep.LinkWallets)
                        },
                        sharedTraffic = cost.SharedTraffic, throughCarsAtSave = saveThroughCars,
                        heap = new
                        {
                            source = GcAllocationCounter.Name, allocatedBytes = saveAllocatedBytes,
                            gen0Collections = saveGen0, gen1Collections = saveGen1, gen2Collections = saveGen2
                        },
                        atLeast30Ms = cost.TotalMilliseconds >= 30,
                        payloadCharacters = cost.PayloadCharacters, payloadUtf8Bytes = cost.PayloadUtf8Bytes, payloadUtf16Bytes = cost.PayloadUtf16Bytes,
                        streamingRecords = cost.StreamingRecords, semanticRecords = cost.SemanticRecords,
                        recordCountMs = cost.CountMilliseconds, recordCountOutsidePhases = true
                    },
                    handOff = HandOffDiagnostics(),
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
