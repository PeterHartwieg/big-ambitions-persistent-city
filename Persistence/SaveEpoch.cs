using System;
using System.Collections.Generic;
using System.Globalization;
using PersistentCity.Core;

namespace PersistentCity.Persistence
{
    public enum SaveEpochMarkerKind { Success, Failed }

    public sealed class SaveEpochMarker
    {
        internal SaveEpochMarker(SaveEpochMarkerKind kind, long sequence, int schema, double minute, string populationKey,
            IReadOnlyList<KeyValuePair<string, int>> payloads)
        { Kind = kind; Sequence = sequence; Schema = schema; Minute = minute; PopulationKey = populationKey; Payloads = payloads; }
        public SaveEpochMarkerKind Kind { get; }
        public long Sequence { get; }
        public int Schema { get; }
        public double Minute { get; }
        public string PopulationKey { get; }
        public IReadOnlyList<KeyValuePair<string, int>> Payloads { get; }
    }

    public static class SaveEpochStore
    {
        public const string Key = "persistentcity:save-epoch_v1";
        public const string Version = "v1";
        public const string FailedPrefix = "failed:";
        private const int MaxPayloadLines = 16;
        private const int MaxMarkerCharacters = 4096;
        public static readonly IReadOnlyList<string> PublishedKeys = new[] { SaveStore.StateKey };

        public static string Failed(long sequence) => FailedPrefix + sequence.ToString(CultureInfo.InvariantCulture);

        public static string Success(long sequence, int schema, double minute, string populationKey, IDictionary<string, string> data)
        {
            if (sequence < 1) throw new ArgumentOutOfRangeException(nameof(sequence));
            if (schema < 1) throw new ArgumentOutOfRangeException(nameof(schema));
            if (!IsFinite(minute) || minute < 0) throw new ArgumentOutOfRangeException(nameof(minute));
            if (!SafeText(populationKey)) throw new ArgumentException("Population key cannot be described by the epoch marker.", nameof(populationKey));
            var text = new System.Text.StringBuilder(160);
            text.Append(Version).Append("\nsequence=").Append(sequence.ToString(CultureInfo.InvariantCulture))
                .Append("\nschema=").Append(schema.ToString(CultureInfo.InvariantCulture))
                .Append("\nminute=").Append(minute.ToString("R", CultureInfo.InvariantCulture))
                .Append("\npopulation=").Append(populationKey);
            foreach (string key in PublishedKeys)
            {
                if (!data.TryGetValue(key, out var payload) || payload == null)
                    throw new InvalidOperationException("Published key " + key + " is missing.");
                text.Append("\npayload=").Append(payload.Length.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(key);
            }
            return text.ToString();
        }

        public static bool TryRead(string? text, out SaveEpochMarker? marker, out string error)
        {
            marker = null;
            if (text == null) { error = "Save-epoch marker is null."; return false; }
            if (text.Length > MaxMarkerCharacters) { error = "Save-epoch marker exceeds " + MaxMarkerCharacters + " characters."; return false; }
            if (text.StartsWith(FailedPrefix, StringComparison.Ordinal))
            {
                if (!TryCanonical(text.Substring(FailedPrefix.Length), out long failed) || failed < 0)
                { error = "Malformed failed save-epoch marker."; return false; }
                marker = new SaveEpochMarker(SaveEpochMarkerKind.Failed, failed, 0, 0, "", Array.Empty<KeyValuePair<string, int>>());
                error = ""; return true;
            }
            string[] lines = text.Split('\n');
            if (lines[0] != Version)
            {
                error = lines[0].Length > 1 && lines[0][0] == 'v' && TryCanonical(lines[0].Substring(1), out _)
                    ? "Unsupported save-epoch marker version " + lines[0] + "." : "Malformed save-epoch marker.";
                return false;
            }
            error = "Malformed save-epoch marker.";
            if (lines.Length < 6 || lines.Length > 5 + MaxPayloadLines) return false;
            if (!Field(lines[1], "sequence=", out string sequenceText) || !TryCanonical(sequenceText, out long sequence) || sequence < 1) return false;
            if (!Field(lines[2], "schema=", out string schemaText) || !TryCanonical(schemaText, out long schema) || schema < 1 || schema > int.MaxValue) return false;
            if (!Field(lines[3], "minute=", out string minuteText)
                || !double.TryParse(minuteText, NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out double minute)
                || !IsFinite(minute) || minute < 0 || minute.ToString("R", CultureInfo.InvariantCulture) != minuteText) return false;
            if (!Field(lines[4], "population=", out string population) || !SafeText(population)) return false;
            var payloads = new List<KeyValuePair<string, int>>(lines.Length - 5);
            for (int i = 5; i < lines.Length; i++)
            {
                if (!Field(lines[i], "payload=", out string row)) return false;
                int space = row.IndexOf(' ');
                if (space < 1 || !TryCanonical(row.Substring(0, space), out long chars) || chars > int.MaxValue) return false;
                string key = row.Substring(space + 1);
                if (!SafeText(key) || payloads.Count > 0 && string.CompareOrdinal(payloads[payloads.Count - 1].Key, key) >= 0) return false;
                payloads.Add(new KeyValuePair<string, int>(key, (int)chars));
            }
            bool hasState = false;
            foreach (var row in payloads) hasState |= row.Key == SaveStore.StateKey;
            if (!hasState) { error = "Save-epoch marker does not describe the population payload."; return false; }
            marker = new SaveEpochMarker(SaveEpochMarkerKind.Success, sequence, (int)schema, minute, population, payloads);
            error = ""; return true;
        }

        public static string? CheckForStart(IDictionary<string, string> data, out SaveEpochMarker? marker)
        {
            marker = null;
            if (!data.TryGetValue(Key, out var text)) return null;
            if (!TryRead(text, out marker, out string error)) { marker = null; return error; }
            if (marker!.Kind == SaveEpochMarkerKind.Failed)
                return "The last save attempt (sequence " + marker.Sequence.ToString(CultureInfo.InvariantCulture)
                    + ") did not publish a coherent Persistent City snapshot.";
            foreach (var row in marker.Payloads)
                if (!data.TryGetValue(row.Key, out var payload) || payload == null || payload.Length != row.Value)
                    return "Save-epoch marker does not describe the stored " + row.Key + " payload.";
            return null;
        }

        public static string? DescribesState(SaveEpochMarker? marker, int sourceSchema, CityState? state)
        {
            if (marker == null) return null;
            if (marker.Kind != SaveEpochMarkerKind.Success || state == null) return "Save-epoch marker describes no loadable population.";
            if (marker.Schema != sourceSchema) return "Save-epoch marker schema differs from the stored population payload.";
            if (!string.Equals(marker.PopulationKey, state.PopulationKey, StringComparison.Ordinal)) return "Save-epoch marker belongs to another population.";
            if (!marker.Minute.Equals(state.Minute)) return "Save-epoch marker minute differs from the stored population snapshot.";
            return null;
        }

        private static bool Field(string line, string name, out string value)
        {
            value = line.StartsWith(name, StringComparison.Ordinal) ? line.Substring(name.Length) : "";
            return value.Length > 0;
        }
        private static bool TryCanonical(string text, out long value)
        {
            value = 0;
            if (text.Length == 0 || text.Length > 18 || text.Length > 1 && text[0] == '0') return false;
            foreach (char c in text) { if (c < '0' || c > '9') return false; value = value * 10 + (c - '0'); }
            return true;
        }
        private static bool SafeText(string? text)
        {
            if (string.IsNullOrEmpty(text) || text!.Length > 512) return false;
            foreach (char c in text) if (char.IsControl(c)) return false;
            return true;
        }
        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    public sealed class SaveEpochBinding
    {
        private readonly object instance;
        private readonly IDictionary<string, string> data;
        private bool poisoned;

        public SaveEpochBinding(object instance, IDictionary<string, string> data, long lastSequence, bool initializing = false)
        {
            Initializing = initializing;
            this.instance = instance ?? throw new ArgumentNullException(nameof(instance));
            this.data = data ?? throw new ArgumentNullException(nameof(data));
            if (lastSequence < 0) throw new ArgumentOutOfRangeException(nameof(lastSequence));
            Sequence = lastSequence;
        }

        public long Sequence { get; private set; }
        public bool Saving { get; private set; }
        public bool Detached { get; private set; }
        public bool Initializing { get; private set; }
        public int Published { get; private set; }
        public int Refused { get; private set; }
        public int Deferred { get; private set; }
        public string LastRefusal { get; private set; } = "";

        public bool BoundTo(object? current) => !Detached && ReferenceEquals(current, instance);

        public SaveEpochTicket? Begin(object? current, IDictionary<string, string>? currentData, bool unitOnStack)
        {
            if (!BoundTo(current)) return null;
            string? refusal = !ReferenceEquals(currentData, data) ? "The save object's mod data dictionary was replaced."
                : unitOnStack ? "A Persistent City unit was executing when the native save started."
                : Saving ? "A native save started inside another Persistent City save callback."
                : poisoned ? "An earlier save epoch of this session failed."
                : null;
            if (refusal == null && Initializing)
            {
                Deferred++;
                return new SaveEpochTicket(this, Sequence) { Deferral = "Persistent City initialization has not completed." };
            }
            var ticket = new SaveEpochTicket(this, ++Sequence);
            if (refusal != null)
            {
                ticket.Refusal = refusal; ticket.Reentrant = unitOnStack || Saving;
                Fail(ticket, refusal); return ticket;
            }
            Saving = true; ticket.Open = true;
            return ticket;
        }

        public bool CanPublish(SaveEpochTicket ticket, object? current, IDictionary<string, string>? currentData) =>
            PublicationRefusal(ticket, current, currentData) == null;

        private string? PublicationRefusal(SaveEpochTicket ticket, object? current, IDictionary<string, string>? currentData) =>
            !ReferenceEquals(ticket.Owner, this) || !ticket.Open ? "Ticket is not open for this save epoch."
                : ticket.Sequence != Sequence ? "Another save callback started after this snapshot (stale sequence)."
                : !BoundTo(current) ? "The save object changed before publication."
                : !ReferenceEquals(currentData, data) ? "The save object's mod data dictionary was replaced."
                : poisoned ? "This save epoch already failed."
                : null;

        public bool Publish(SaveEpochTicket ticket, object? current, IDictionary<string, string>? currentData,
            int schema, double minute, string populationKey)
        {
            string? refusal = PublicationRefusal(ticket, current, currentData);
            string marker = "";
            if (refusal == null)
            {
                try { marker = SaveEpochStore.Success(ticket.Sequence, schema, minute, populationKey, data); }
                catch (Exception error) when (error is ArgumentException || error is InvalidOperationException) { refusal = error.Message; }
            }
            if (refusal != null) { Fail(ticket, refusal); return false; }
            data[SaveEpochStore.Key] = marker;
            Published++;
            return true;
        }

        public void End(SaveEpochTicket ticket)
        {
            if (!ReferenceEquals(ticket.Owner, this) || !ticket.Open) return;
            ticket.Open = false; Saving = false;
        }

        public void Fail(SaveEpochTicket ticket, string reason)
        {
            if (!ReferenceEquals(ticket.Owner, this)) return;
            Refused++; LastRefusal = reason ?? "";
            if (Detached) return;
            MarkFailed(AccountingStopStore.SaveEpochFailed);
        }

        public void CompleteInitialization()
        {
            if (Detached) throw new InvalidOperationException("A detached save epoch cannot complete initialization.");
            Initializing = false;
        }

        public bool Detach(object? current)
        {
            if (Detached) return false;
            bool same = !Initializing && ReferenceEquals(current, instance);
            if (same) MarkFailed(AccountingStopStore.DetachedAfterSave);
            Detached = true; Saving = false;
            return same;
        }

        private void MarkFailed(string stopValue)
        {
            poisoned = true;
            AccountingStopStore.Stop(data, stopValue);
            data[SaveEpochStore.Key] = SaveEpochStore.Failed(Sequence);
        }
    }

    public sealed class SaveEpochTicket
    {
        internal SaveEpochTicket(SaveEpochBinding owner, long sequence) { Owner = owner; Sequence = sequence; }
        internal SaveEpochBinding Owner { get; }
        internal bool Open { get; set; }
        public long Sequence { get; }
        public string? Refusal { get; internal set; }
        public string? Deferral { get; internal set; }
        public bool Reentrant { get; internal set; }
    }

    public enum SaveEpochResult { NotBound, Published, Refused, Reentrant, Deferred }

    public readonly struct SaveEpochCapture
    {
        private SaveEpochCapture(string? error, double minute, string populationKey) { Error = error; Minute = minute; PopulationKey = populationKey; }
        public string? Error { get; }
        public double Minute { get; }
        public string PopulationKey { get; }
        public static SaveEpochCapture Written(double minute, string populationKey) => new SaveEpochCapture(null, minute, populationKey);
        public static SaveEpochCapture Refused(string error) => new SaveEpochCapture(error ?? "Save refused.", 0, "");
    }

    public static class SaveEpochCallback
    {
        public static SaveEpochResult Run(SaveEpochBinding? binding, object? current, IDictionary<string, string>? currentData,
            bool unitOnStack, Func<SaveEpochTicket, SaveEpochCapture> capture, Action<string> stop, Action<string> report)
        {
            SaveEpochTicket? ticket = null;
            string reason;
            try
            {
                ticket = binding?.Begin(current, currentData, unitOnStack);
                if (ticket == null) return SaveEpochResult.NotBound;
                if (ticket.Deferral != null)
                {
                    Report(report, "Persistent City skipped the save during initialization; no Persistent City state was written: " + ticket.Deferral);
                    return SaveEpochResult.Deferred;
                }
                if (ticket.Refusal != null)
                {
                    Report(report, "Persistent City save epoch refused: " + ticket.Refusal);
                    if (ticket.Reentrant) return SaveEpochResult.Reentrant;
                    Stop(stop, ticket.Refusal, report);
                    return SaveEpochResult.Refused;
                }
                var captured = capture(ticket);
                if (captured.Error == null
                    && binding!.Publish(ticket, current, currentData, CityState.CurrentSchema, captured.Minute, captured.PopulationKey))
                    return SaveEpochResult.Published;
                reason = captured.Error ?? binding!.LastRefusal;
            }
            catch (Exception error) { reason = "Population save failed: " + error.Message; }
            finally { if (ticket != null) binding!.End(ticket); }
            try { if (ticket != null) binding!.Fail(ticket, reason); }
            catch (Exception error) { Report(report, "Persistent City save-epoch marker write failed: " + error); }
            Report(report, "Persistent City save refused: " + reason);
            Stop(stop, reason, report);
            return SaveEpochResult.Refused;
        }

        private static void Stop(Action<string> stop, string reason, Action<string> report)
        {
            try { stop(reason); }
            catch (Exception error) { Report(report, "Persistent City stop after a refused save was contained: " + error); }
        }

        private static void Report(Action<string> report, string message)
        {
            try { report(message); } catch { }
        }
    }
}
