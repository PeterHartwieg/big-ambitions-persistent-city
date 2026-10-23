using System;
using System.Collections.Generic;
using PersistentCity.Core;

namespace PersistentCity.Persistence
{
    public sealed class SaveWriteResult
    {
        internal SaveWriteResult(bool success, string error) { Success = success; Error = error; }
        public bool Success { get; }
        public string Error { get; }
    }

    /// <summary>Only owns StateKey. The caller must retain the dictionary and failed raw payload.</summary>
    public static class SaveStore
    {
        public const string StateKey = SaveCodec.StateKey;

        public static SaveReadResult ReadFrom(IDictionary<string, string> modData, string expectedPopulationKey)
        {
            if (modData == null) throw new ArgumentNullException(nameof(modData));
            if (string.IsNullOrWhiteSpace(expectedPopulationKey)) throw new ArgumentException("A population key is required.", nameof(expectedPopulationKey));
            if (!modData.TryGetValue(StateKey, out var source)) return SaveCodec.Read(null);
            if (source == null) return new SaveReadResult(SaveReadStatus.Invalid, null, "Stored state is null.");
            var result = SaveCodec.Read(source);
            if (result.Status == SaveReadStatus.Loaded && !string.Equals(result.State!.PopulationKey, expectedPopulationKey, StringComparison.Ordinal))
                return new SaveReadResult(SaveReadStatus.Invalid, null, "Stored state belongs to another population.");
            return result;
        }

        public static SaveWriteResult WriteTo(IDictionary<string, string> modData, CityState state)
        {
            if (modData == null) throw new ArgumentNullException(nameof(modData));
            if (state == null) return new SaveWriteResult(false, "State is null.");
            try
            {
                // Encode and validate completely before touching modData.
                var payload = SaveCodec.Write(state);
                var existing = ReadFrom(modData, state.PopulationKey);
                if (existing.Status != SaveReadStatus.Missing && existing.Status != SaveReadStatus.Loaded)
                    return new SaveWriteResult(false, existing.Error);
                modData[StateKey] = payload;
                return new SaveWriteResult(true, "");
            }
            catch (SaveValidationException exception) { return new SaveWriteResult(false, exception.Message); }
        }
    }
}
