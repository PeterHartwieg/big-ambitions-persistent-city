using System;
using System.Collections.Generic;
using PersistentCity.Core;

namespace PersistentCity.Persistence
{
    /// <summary>
    /// One save session. Only exact dictionary and immutable payload references validated
    /// by this session are reusable. A changed source always goes through the strict reader.
    /// Use on one thread at a time; the owner must serialize access to its save dictionary.
    /// </summary>
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
        /// <summary>Strict stored-payload reads, for save-cost diagnostics. Cache hits do not increment this count.</summary>
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
            // Do not cache an observation if a custom dictionary changed during validation.
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

        /// <summary>
        /// Transfer a snapshot validation to the live dictionary on the owning thread.
        /// Only the exact same immutable payload (or the same missing-key observation)
        /// can transfer without another read. Other contents are strictly revalidated.
        /// </summary>
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

        public SaveWriteResult WriteTo(IDictionary<string, string> modData, CityState state)
        {
            if (modData == null) throw new ArgumentNullException(nameof(modData));
            if (state == null) return new SaveWriteResult(false, "State is null.");
            if (!string.Equals(state.PopulationKey, populationKey, StringComparison.Ordinal))
                return new SaveWriteResult(false, "State belongs to another population than this save session.");
            try
            {
                // Validate the complete new state before considering any replacement.
                string payload = SaveCodec.Write(state);
                bool hasPayload = modData.TryGetValue(SaveStore.StateKey, out var existingPayload);
                bool trusted = ReferenceEquals(modData, validatedDictionary)
                    && hasPayload == validatedHasPayload && ReferenceEquals(existingPayload, validatedPayload);
                if (!trusted)
                {
                    var existing = ValidateSource(modData);
                    if (existing.Status != SaveReadStatus.Missing && existing.Status != SaveReadStatus.Loaded)
                        return new SaveWriteResult(false, existing.Error);
                    // A dictionary that changed while being read cannot authorize replacement.
                    if (!ReferenceEquals(validatedDictionary, modData))
                        return new SaveWriteResult(false, "Stored state changed during validation.");
                }
                modData[SaveStore.StateKey] = payload;
                validatedDictionary = modData;
                validatedHasPayload = true;
                validatedPayload = payload;
                return new SaveWriteResult(true, "");
            }
            catch (SaveValidationException exception) { return new SaveWriteResult(false, exception.Message); }
        }
    }
}
