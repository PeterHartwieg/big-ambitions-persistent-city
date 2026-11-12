using System;
using System.Collections.Generic;
using PersistentCity.Core;

namespace PersistentCity.Persistence
{
    public sealed class SaveSession
    {
        private IDictionary<string, string>? validatedDictionary;
        private string? validatedPayload;
        private bool validatedHasPayload;
        private readonly string populationKey;

        private SaveSession(IDictionary<string, string> modData, string expectedPopulationKey)
        {
            if (modData == null) throw new ArgumentNullException(nameof(modData));
            if (string.IsNullOrWhiteSpace(expectedPopulationKey)) throw new ArgumentException("A population key is required.", nameof(expectedPopulationKey));
            populationKey = expectedPopulationKey;
            InitialReadResult = ValidateSource(modData);
        }

        public SaveReadResult InitialReadResult { get; }
        public int StoredPayloadValidationCount { get; private set; }

        public static SaveSession Open(IDictionary<string, string> modData, string expectedPopulationKey)
            => new SaveSession(modData, expectedPopulationKey);

        private SaveReadResult ValidateSource(IDictionary<string, string> modData)
        {
            bool hadPayload = modData.TryGetValue(SaveStore.StateKey, out var payload);
            StoredPayloadValidationCount++;
            var snapshot = new Dictionary<string, string>();
            if (hadPayload) snapshot.Add(SaveStore.StateKey, payload!);
            var result = SaveStore.ReadFrom(snapshot, populationKey);
            bool stillHasPayload = modData.TryGetValue(SaveStore.StateKey, out var current);
            if ((result.Status == SaveReadStatus.Missing || result.Status == SaveReadStatus.Loaded)
                && hadPayload == stillHasPayload && ReferenceEquals(payload, current))
            {
                validatedDictionary = modData;
                validatedHasPayload = hadPayload;
                validatedPayload = payload;
            }
            else validatedDictionary = null;
            return result;
        }

        public SaveWriteResult BindTo(IDictionary<string, string> modData)
        {
            if (modData == null) throw new ArgumentNullException(nameof(modData));
            bool hasPayload = modData.TryGetValue(SaveStore.StateKey, out var payload);
            if (validatedDictionary != null && hasPayload == validatedHasPayload
                && ReferenceEquals(payload, validatedPayload))
            {
                validatedDictionary = modData;
                return new SaveWriteResult(true, "");
            }
            var result = ValidateSource(modData);
            return result.Status == SaveReadStatus.Loaded || result.Status == SaveReadStatus.Missing
                ? new SaveWriteResult(ReferenceEquals(modData, validatedDictionary), ReferenceEquals(modData, validatedDictionary) ? "" : "Stored state changed during validation.")
                : new SaveWriteResult(false, result.Error);
        }

        public SaveWriteResult WriteTo(IDictionary<string, string> modData, CityState state) => WriteTo(modData, state, null);

        public SaveWriteResult WriteTo(IDictionary<string, string> modData, CityState state, SaveCostRecorder? cost)
        {
            if (modData == null) throw new ArgumentNullException(nameof(modData));
            if (state == null) return new SaveWriteResult(false, "State is null.");
            if (!string.Equals(state.PopulationKey, populationKey, StringComparison.Ordinal))
                return new SaveWriteResult(false, "State belongs to another population than this save session.");
            SaveSnapshot snapshot;
            try
            {
                cost?.Enter(SaveCostPhase.Validate);
                SaveCodec.RequireExecutablePolicy(state);
                snapshot = SaveCodec.Capture(state, cost);
            }
            catch (SaveValidationException exception) { return new SaveWriteResult(false, exception.Message); }
            return WriteTo(modData, snapshot, cost);
        }

        public SaveWriteResult WriteTo(IDictionary<string, string> modData, SaveSnapshot snapshot, SaveCostRecorder? cost = null)
        {
            if (modData == null) throw new ArgumentNullException(nameof(modData));
            if (snapshot == null) return new SaveWriteResult(false, "Snapshot is null.");
            if (snapshot.Published) return new SaveWriteResult(false, "A save snapshot is published at most once.");
            if (!string.Equals(snapshot.PopulationKey, populationKey, StringComparison.Ordinal))
                return new SaveWriteResult(false, "State belongs to another population than this save session.");
            try
            {
                cost?.Enter(SaveCostPhase.Validate);
                SaveCodec.RequireExecutablePolicy(snapshot.Wire);
                string payload = SaveCodec.Encode(snapshot, cost);
                cost?.Enter(SaveCostPhase.Publish);
                bool hasPayload = modData.TryGetValue(SaveStore.StateKey, out var existingPayload);
                bool trusted = ReferenceEquals(modData, validatedDictionary)
                    && hasPayload == validatedHasPayload && ReferenceEquals(existingPayload, validatedPayload);
                if (!trusted)
                {
                    var existing = ValidateSource(modData);
                    if (existing.Status != SaveReadStatus.Missing && existing.Status != SaveReadStatus.Loaded)
                        return new SaveWriteResult(false, existing.Error);
                    if (!ReferenceEquals(validatedDictionary, modData))
                        return new SaveWriteResult(false, "Stored state changed during validation.");
                }
                modData[SaveStore.StateKey] = payload;
                snapshot.Published = true;
                validatedDictionary = modData;
                validatedHasPayload = true;
                validatedPayload = payload;
                return new SaveWriteResult(true, "");
            }
            catch (SaveValidationException exception) { return new SaveWriteResult(false, exception.Message); }
        }
    }
}
