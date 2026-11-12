using System.Diagnostics;

namespace PersistentCity.Persistence
{
    public enum SaveCostPhase { Observe, Synchronize, Validate, Encode, Publish, Capture }

    public sealed class SaveCostRecorder
    {
        public const int PhaseCount = 6;
        private readonly long[] ticks = new long[PhaseCount];
        private long started, last;
        private SaveCostPhase current;
        private int visited;

        public void Begin()
        {
            current = SaveCostPhase.Observe; visited = 1;
            for (int i = 0; i < PhaseCount; i++) ticks[i] = 0;
            PayloadCharacters = 0; PayloadUtf8Bytes = 0; StreamingRecords = SemanticRecords = -1;
            CountMilliseconds = 0; Success = false; Error = "";
            Sequence++;
            started = last = Stopwatch.GetTimestamp();
        }

        public void Enter(SaveCostPhase phase)
        {
            long now = Stopwatch.GetTimestamp();
            ticks[(int)current] += now - last;
            last = now; current = phase; visited |= 1 << (int)phase;
        }

        public bool Covered(SaveCostPhase phase) => (visited & (1 << (int)phase)) != 0;

        internal void Payload(int characters, long utf8Bytes) { PayloadCharacters = characters; PayloadUtf8Bytes = utf8Bytes; }

        public void End(bool success, string error)
        {
            Enter(current);
            Success = success; Error = error ?? ""; LastPhase = current;
        }

        public SaveCostPhase LastPhase { get; private set; }

        public void Counts(int streaming, int semantic, double milliseconds)
        { StreamingRecords = streaming; SemanticRecords = semantic; CountMilliseconds = milliseconds; }

        public long Sequence { get; private set; }
        public double Milliseconds(SaveCostPhase phase) => ticks[(int)phase] * 1000.0 / Stopwatch.Frequency;
        public double TotalMilliseconds => (last - started) * 1000.0 / Stopwatch.Frequency;
        public long PhaseTicks(SaveCostPhase phase) => ticks[(int)phase];
        public long TotalTicks => last - started;
        public int PayloadCharacters { get; private set; }
        public long PayloadUtf8Bytes { get; private set; }
        public long PayloadUtf16Bytes => 2L * PayloadCharacters;
        public int StreamingRecords { get; private set; } = -1;
        public int SemanticRecords { get; private set; } = -1;
        public double CountMilliseconds { get; private set; }
        public bool Success { get; private set; }
        public string Error { get; private set; } = "";
    }
}
