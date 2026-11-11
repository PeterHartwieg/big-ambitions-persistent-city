using System.Diagnostics;

namespace PersistentCity.Persistence
{
    /// <summary>Contiguous save phases inside one native onSaveGame callback (F7a). F7c adds Capture, the snapshot
    /// copy between Synchronize and Validate; it is appended so the earlier phase numbers stay. In the single-pass save
    /// (P-A) Capture is the snapshot copy, Validate the semantic check of that snapshot and Encode the one writer pass
    /// that produces the save string; nothing else runs between them.</summary>
    public enum SaveCostPhase { Observe, Synchronize, Validate, Encode, Publish, Capture }

    /// <summary>
    /// Explicitly requested save-cost record. Callers pass null when diagnostics are off, so the
    /// write path then does no timing work at all. A non-null recorder only reads Stopwatch
    /// timestamps and stores primitives; it never changes what is serialized. Phases are
    /// contiguous: Enter charges the time since the previous switch to the current phase, so
    /// the phase sum equals the total measured span. Use on the owning (main) thread only.
    /// </summary>
    public sealed class SaveCostRecorder
    {
        public const int PhaseCount = 6;
        private readonly long[] ticks = new long[PhaseCount];
        private long started, last;
        private SaveCostPhase current;
        private int visited;

        /// <summary>Start a new record in the observe phase; clears every previous value.</summary>
        public void Begin()
        {
            current = SaveCostPhase.Observe; visited = 1;
            for (int i = 0; i < PhaseCount; i++) ticks[i] = 0;
            PayloadCharacters = 0; PayloadUtf8Bytes = 0; StreamingRecords = SemanticRecords = -1;
            CountMilliseconds = 0; Success = false; Error = "";
            Sequence++;
            started = last = Stopwatch.GetTimestamp();
        }

        /// <summary>Charge the time since the previous switch to the current phase, then switch to <paramref name="phase"/>.</summary>
        public void Enter(SaveCostPhase phase)
        {
            long now = Stopwatch.GetTimestamp();
            ticks[(int)current] += now - last;
            last = now; current = phase; visited |= 1 << (int)phase;
        }

        /// <summary>True once the record has entered <paramref name="phase"/> (a fast phase may still measure zero ticks).</summary>
        public bool Covered(SaveCostPhase phase) => (visited & (1 << (int)phase)) != 0;

        internal void Payload(int characters, long utf8Bytes) { PayloadCharacters = characters; PayloadUtf8Bytes = utf8Bytes; }

        /// <summary>Close the record; the remaining time belongs to the phase in progress (the failing one on refusal).</summary>
        public void End(bool success, string error)
        {
            Enter(current);
            Success = success; Error = error ?? ""; LastPhase = current;
        }

        /// <summary>The phase in progress when the record closed.</summary>
        public SaveCostPhase LastPhase { get; private set; }

        /// <summary>Record counts are measured after publication, outside the phase timers.</summary>
        public void Counts(int streaming, int semantic, double milliseconds)
        { StreamingRecords = streaming; SemanticRecords = semantic; CountMilliseconds = milliseconds; }

        public long Sequence { get; private set; }
        public double Milliseconds(SaveCostPhase phase) => ticks[(int)phase] * 1000.0 / Stopwatch.Frequency;
        public double TotalMilliseconds => (last - started) * 1000.0 / Stopwatch.Frequency;
        public long PhaseTicks(SaveCostPhase phase) => ticks[(int)phase];
        public long TotalTicks => last - started;
        public int PayloadCharacters { get; private set; }
        public long PayloadUtf8Bytes { get; private set; }
        /// <summary>Native Odin storage of the string is UTF-16 (2 bytes per character).</summary>
        public long PayloadUtf16Bytes => 2L * PayloadCharacters;
        /// <summary>-1 when counts were not measured.</summary>
        public int StreamingRecords { get; private set; } = -1;
        public int SemanticRecords { get; private set; } = -1;
        public double CountMilliseconds { get; private set; }
        public bool Success { get; private set; }
        public string Error { get; private set; } = "";
    }
}
