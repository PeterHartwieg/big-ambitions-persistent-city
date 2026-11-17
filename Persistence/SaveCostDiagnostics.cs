using System.Diagnostics;

namespace PersistentCity.Persistence
{
    public enum SaveCostPhase { Observe, Synchronize, Validate, Encode, Publish, Capture }

    public enum SaveCostSection { Header, Places, Residents, Visitors, Traffic, Food, Economy }

    public enum SaveCostStep { Accounting, Rlo, Physical, FoodMarket, EpochCheck, Vendors, HandOffCheck, Prepare, Outflow, Names, Wallets, LinkWallets }

    public sealed class SaveCostRecorder
    {
        public const int PhaseCount = 6;
        public const int SectionCount = 7;
        public const int StepCount = 12;
        public const int CaptureSectionCount = 9;
        private readonly long[] stepTicks = new long[StepCount];
        private readonly long[] captureTicks = new long[CaptureSectionCount];
        private long stepStarted;
        private readonly long[] ticks = new long[PhaseCount];
        private readonly long[] sectionTicks = new long[SectionCount];
        private readonly long[] validateTicks = new long[SectionCount];
        private long sectionStarted, validateStarted;
        private int section = -1, validating = -1;
        private long started, last;
        private SaveCostPhase current;
        private int visited;

        public void Begin()
        {
            current = SaveCostPhase.Observe; visited = 1;
            for (int i = 0; i < PhaseCount; i++) ticks[i] = 0;
            for (int i = 0; i < SectionCount; i++) sectionTicks[i] = validateTicks[i] = 0;
            for (int i = 0; i < StepCount; i++) stepTicks[i] = 0;
            for (int i = 0; i < CaptureSectionCount; i++) captureTicks[i] = 0;
            SharedTraffic = false;
            section = validating = -1;
            PayloadCharacters = 0; PayloadUtf8Bytes = 0; StreamingRecords = SemanticRecords = -1;
            CountMilliseconds = 0; Success = false; Error = "";
            Sequence++;
            started = last = stepStarted = Stopwatch.GetTimestamp();
        }

        public void Enter(SaveCostPhase phase)
        {
            long now = Stopwatch.GetTimestamp();
            ticks[(int)current] += now - last;
            last = stepStarted = now; current = phase; visited |= 1 << (int)phase;
        }

        public void Step(SaveCostStep step)
        {
            long now = Stopwatch.GetTimestamp();
            stepTicks[(int)step] += now - stepStarted;
            stepStarted = now;
        }

        internal void Charge(SaveCostStep step, long elapsed) => stepTicks[(int)step] += elapsed;

        internal void CaptureSection(SnapshotSection captured, long elapsed) => captureTicks[(int)captured] += elapsed;

        public double StepMilliseconds(SaveCostStep step) => stepTicks[(int)step] * 1000.0 / Stopwatch.Frequency;
        public long StepTicks(SaveCostStep step) => stepTicks[(int)step];
        public double CaptureSectionMilliseconds(SnapshotSection captured) => captureTicks[(int)captured] * 1000.0 / Stopwatch.Frequency;
        public long CaptureSectionTicks(SnapshotSection captured) => captureTicks[(int)captured];
        public bool SharedTraffic { get; internal set; }

        public bool Covered(SaveCostPhase phase) => (visited & (1 << (int)phase)) != 0;

        internal void Section(SaveCostSection next)
        {
            long now = Stopwatch.GetTimestamp();
            if (section >= 0) sectionTicks[section] += now - sectionStarted;
            section = (int)next; sectionStarted = now;
        }

        internal void EndSections()
        {
            if (section >= 0) sectionTicks[section] += Stopwatch.GetTimestamp() - sectionStarted;
            section = -1;
        }

        internal void ValidateSection(SaveCostSection next)
        {
            long now = Stopwatch.GetTimestamp();
            if (validating >= 0) validateTicks[validating] += now - validateStarted;
            validating = (int)next; validateStarted = now;
        }

        internal void EndValidateSections()
        {
            if (validating >= 0) validateTicks[validating] += Stopwatch.GetTimestamp() - validateStarted;
            validating = -1;
        }

        public double ValidateSectionMilliseconds(SaveCostSection validated) => validateTicks[(int)validated] * 1000.0 / Stopwatch.Frequency;
        public long ValidateSectionTicks(SaveCostSection validated) => validateTicks[(int)validated];
        public double SectionMilliseconds(SaveCostSection encoded) => sectionTicks[(int)encoded] * 1000.0 / Stopwatch.Frequency;
        public long SectionTicks(SaveCostSection encoded) => sectionTicks[(int)encoded];

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
