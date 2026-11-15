using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using PersistentCity.Core;

namespace PersistentCity.Persistence
{
    public enum SaveSpliceOutcome { Pending, Spliced, Absent, Partial, Failed }

    public sealed class PendingSave
    {
        public const string TokenPrefix = "persistentcity:pending-";
        private readonly object gate = new object();
        private readonly SaveSnapshot snapshot;
        private readonly SaveHandOffWriter writer;
        private readonly bool countRecords;
        private bool resolved, started, cancelled, publishing;
        private int spliceDone, serialized, claimed;

        internal PendingSave(SaveSnapshot snapshot, IDictionary<string, string> target, SaveHandOffWriter writer,
            bool hadPreviousPayload, string? previousPayload, SaveCostRecorder? cost, bool countRecords)
        {
            this.snapshot = snapshot; this.writer = writer; this.countRecords = countRecords;
            Target = target; HadPreviousPayload = hadPreviousPayload; PreviousPayload = previousPayload; Cost = cost;
            string id = Guid.NewGuid().ToString("N");
            PayloadToken = TokenPrefix + "state-" + id;
            MarkerToken = TokenPrefix + "epoch-" + id;
        }

        public IDictionary<string, string> Target { get; }
        public string PayloadToken { get; }
        public string MarkerToken { get; }
        public long Sequence { get; internal set; }
        public double Minute => snapshot.Minute;
        public string PopulationKey => snapshot.PopulationKey;
        public bool HadPreviousPayload { get; }
        public string? PreviousPayload { get; }
        public SaveCostRecorder? Cost { get; }
        public bool Succeeded { get; private set; }
        public string Payload { get; private set; } = "";
        public string Marker { get; private set; } = "";
        public string Error { get; private set; } = "";
        public double WriteMilliseconds { get; private set; }
        public long WriterAllocatedBytes { get; private set; } = -1;
        public bool Aborted { get; private set; }
        public SaveSpliceOutcome Splice { get; private set; }
        public string SpliceError { get; private set; } = "";
        public double CompressWaitMilliseconds { get; private set; }
        public double CompressMilliseconds { get; private set; }
        public bool SpliceDone => Volatile.Read(ref spliceDone) != 0;
        public bool Serialized => Volatile.Read(ref serialized) != 0;
        public bool Claimed => Volatile.Read(ref claimed) != 0;
        internal SaveSession? Owner { get; set; }

        public bool Cancelled { get { lock (gate) return cancelled; } }
        public string CancelReason { get; private set; } = "";
        public bool CompressionSettled => SpliceDone || Cancelled;

        public string? FileRefusal
        {
            get
            {
                if (Cancelled)
                    return "The native save of this city was not completed with the Persistent City hand-off (" + CancelReason
                        + "). Persistent City refuses to publish it; if native compression wrote it without the hand-off, that file carries a pending marker and reload stops accounting.";
                if (!SpliceDone || Splice == SaveSpliceOutcome.Spliced) return null;
                return "The native save file was not replaced because the Persistent City hand-off failed (" + Splice
                    + (SpliceError.Length > 0 ? ": " + SpliceError : "") + "); the previous file in that slot is kept.";
            }
        }

        internal string? TempPath { get; set; }
        internal string? OutputPath { get; set; }

        internal bool TryBeginPublish()
        {
            lock (gate)
            {
                if (cancelled) return false;
                publishing = true;
                return true;
            }
        }

        public bool Cancel(string reason)
        {
            lock (gate)
            {
                if (publishing || spliceDone != 0) return false;
                if (!cancelled) { cancelled = true; CancelReason = reason ?? "cancelled"; }
                Monitor.PulseAll(gate);
                return true;
            }
        }

        internal void MarkSerialized(int compressorThreadId)
        {
            CompressorThreadId = compressorThreadId;
            Volatile.Write(ref serialized, 1);
        }

        public int CompressorThreadId { get; private set; }
        internal void MarkClaimed() { lock (gate) { claimed = 1; Monitor.PulseAll(gate); } }

        public bool WaitClaimed(int milliseconds) => WaitFor(milliseconds, () => claimed != 0 || spliceDone != 0);
        public bool WaitSpliced(int milliseconds) => WaitFor(milliseconds, () => spliceDone != 0);

        private bool WaitFor(int milliseconds, Func<bool> done)
        {
            long deadline = Stopwatch.GetTimestamp() + (long)(milliseconds * (double)Stopwatch.Frequency / 1000);
            lock (gate)
            {
                while (!done())
                {
                    if (milliseconds < 0) { Monitor.Wait(gate); continue; }
                    long remaining = (deadline - Stopwatch.GetTimestamp()) * 1000 / Stopwatch.Frequency;
                    if (remaining <= 0) return false;
                    Monitor.Wait(gate, (int)Math.Min(remaining, int.MaxValue));
                }
                return true;
            }
        }

        public bool Resolved { get { lock (gate) return resolved; } }

        internal string FilePayload => Succeeded ? Payload : PreviousPayload ?? "";
        internal string FileMarker => Succeeded ? Marker : SaveEpochStore.Failed(Sequence);

        public void Start()
        {
            lock (gate)
            {
                if (started) throw new InvalidOperationException("A pending save starts once.");
                started = true;
            }
            writer.Enqueue(this);
        }

        public bool Wait(int milliseconds)
        {
            long deadline = Stopwatch.GetTimestamp() + (long)(milliseconds * (double)Stopwatch.Frequency / 1000);
            lock (gate)
            {
                while (!resolved)
                {
                    long remaining = (deadline - Stopwatch.GetTimestamp()) * 1000 / Stopwatch.Frequency;
                    if (remaining <= 0) return false;
                    Monitor.Wait(gate, (int)Math.Min(remaining, int.MaxValue));
                }
                return true;
            }
        }

        public bool WaitOrAbort(int milliseconds, string reason)
        {
            if (Wait(milliseconds)) return !Aborted;
            Abort(reason);
            return false;
        }

        public void Abort(string reason) => Resolve(false, "", "", reason ?? "The background save was aborted.", true);

        public void Abandon()
        {
            Abort("The background save was abandoned before it started.");
            Owner?.Forget(this);
            if (Target.TryGetValue(SaveStore.StateKey, out var current) && ReferenceEquals(current, PayloadToken))
            {
                if (HadPreviousPayload) Target[SaveStore.StateKey] = PreviousPayload!;
                else Target.Remove(SaveStore.StateKey);
            }
        }

        public void Release()
        {
            if (!Resolved) Abort("The save session ended before the background write resolved.");
            if (Target.TryGetValue(SaveStore.StateKey, out var payload) && ReferenceEquals(payload, PayloadToken))
            {
                if (Succeeded) Target[SaveStore.StateKey] = Payload;
                else if (HadPreviousPayload) Target[SaveStore.StateKey] = PreviousPayload!;
                else Target.Remove(SaveStore.StateKey);
            }
            if (Target.TryGetValue(SaveEpochStore.Key, out var marker) && ReferenceEquals(marker, MarkerToken))
            {
                if (!Succeeded) AccountingStopStore.Stop(Target, AccountingStopStore.SaveEpochFailed);
                Target[SaveEpochStore.Key] = FileMarker;
            }
        }

        internal void Write(Func<long>? allocation)
        {
            if (Resolved) return;
            long started = Stopwatch.GetTimestamp();
            long allocatedBefore = allocation?.Invoke() ?? 0;
            string payload = "", marker = "", error = "";
            bool ok = false;
            var cost = Cost;
            try
            {
                cost?.Begin();
                cost?.Enter(SaveCostPhase.Validate);
                if (writer.Fault?.Invoke() is string fault) throw new SaveValidationException(fault);
                SaveCodec.RequireExecutablePolicy(snapshot.Wire);
                payload = SaveCodec.Encode(snapshot, cost);
                cost?.Enter(SaveCostPhase.Publish);
                marker = SaveEpochStore.Success(Sequence, CityState.CurrentSchema, snapshot.Minute, snapshot.PopulationKey, payload.Length);
                ok = true;
            }
            catch (SaveValidationException exception) { error = "Population save refused: " + exception.Message; }
            catch (Exception exception) { error = "Population save failed: " + exception.Message; }
            cost?.End(ok, error);
            if (ok && countRecords && cost != null)
            {
                long counting = Stopwatch.GetTimestamp();
                try
                {
                    var counts = SaveCodec.CountRecords(snapshot.Wire, payload);
                    cost.Counts(counts.Streaming, counts.Semantic, (Stopwatch.GetTimestamp() - counting) * 1000.0 / Stopwatch.Frequency);
                }
                catch (Exception) { cost.Counts(-1, -1, (Stopwatch.GetTimestamp() - counting) * 1000.0 / Stopwatch.Frequency); }
            }
            WriteMilliseconds = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
            if (allocation != null) WriterAllocatedBytes = allocation() - allocatedBefore;
            Resolve(ok, payload, marker, error, false);
        }

        private void Resolve(bool ok, string payload, string marker, string error, bool aborted)
        {
            lock (gate)
            {
                if (resolved) return;
                Succeeded = ok; Payload = payload; Marker = marker; Error = error; Aborted = aborted;
                resolved = true;
                Monitor.PulseAll(gate);
            }
        }

        internal bool Spliced(SaveSpliceOutcome outcome, string error, double waited, double compressed)
        {
            lock (gate)
            {
                if (spliceDone != 0) return false;
                Splice = outcome; SpliceError = error ?? ""; CompressWaitMilliseconds = waited; CompressMilliseconds = compressed;
                Volatile.Write(ref spliceDone, 1);
                Monitor.PulseAll(gate);
                return true;
            }
        }
    }

    public sealed class SaveHandOffWriter
    {
        public static readonly SaveHandOffWriter Shared = new SaveHandOffWriter("Persistent City save writer");
        private readonly object gate = new object();
        private readonly Queue<PendingSave> queue = new Queue<PendingSave>();
        private readonly string name;
        private Thread? thread;

        internal SaveHandOffWriter(string name) { this.name = name; }

        internal Action? BeforeWrite { get; set; }
        internal Func<string?>? Fault { get; set; }
        internal Func<long>? Allocation { get; set; }
        public int ThreadId { get; private set; }
        public long Written { get; private set; }

        internal void Enqueue(PendingSave save)
        {
            lock (gate)
            {
                queue.Enqueue(save);
                if (thread == null)
                {
                    thread = new Thread(Run) { IsBackground = true, Name = name };
                    thread.Start();
                }
                Monitor.Pulse(gate);
            }
        }

        private void Run()
        {
            ThreadId = Thread.CurrentThread.ManagedThreadId;
            while (true)
            {
                PendingSave next;
                lock (gate)
                {
                    while (queue.Count == 0) Monitor.Wait(gate);
                    next = queue.Dequeue();
                }
                try
                {
                    BeforeWrite?.Invoke();
                    next.Write(Allocation);
                }
                catch (Exception exception) { next.Abort("Population save failed: " + exception.Message); }
                Written++;
            }
        }
    }

    public static class SaveHandOff
    {
        public const int CompressWaitMilliseconds = 120000;
        public const int ClaimWaitMilliseconds = 2000;
        public const string StagingSuffix = ".persistentcity-staging";
        private static readonly object gate = new object();
        private static PendingSave? expected, latest, refusing, publishing;
        private static string? expectedPath;

        public static bool RefusalOutstanding { get { lock (gate) return refusing != null; } }
        public static bool PublicationOutstanding { get { lock (gate) return publishing != null && !publishing.SpliceDone; } }

        public static void Expect(PendingSave save, string? tempPath)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            lock (gate)
            {
                if (refusing != null) throw new InvalidOperationException("A cancelled hand-off still waits for its compressor.");
                if (publishing != null && !publishing.SpliceDone) throw new InvalidOperationException("An earlier hand-off is still publishing.");
                expected = latest = save; expectedPath = tempPath; save.TempPath = tempPath;
            }
        }

        public static void Clear()
        {
            lock (gate) { expected = latest = null; expectedPath = null; }
        }

        internal static void Reset()
        {
            lock (gate) { expected = latest = refusing = publishing = null; expectedPath = null; }
            BeforePublish = null;
            SameSlotWaitMilliseconds = PublishWaitMilliseconds;
        }

        public static void MarkSerialized(int compressorThreadId = 0)
        {
            lock (gate) { latest?.MarkSerialized(compressorThreadId); latest = null; }
        }

        public static PendingSave? Claim(string? path) => Claim(path, out _);

        public static PendingSave? Claim(string? path, out bool refused)
        {
            PendingSave? save, misdirected = null;
            refused = false;
            lock (gate)
            {
                save = expected;
                if (save == null)
                {
                    var cancelled = refusing;
                    if (cancelled == null || cancelled.TempPath != null && !string.Equals(path, cancelled.TempPath, StringComparison.Ordinal)) return null;
                    if (cancelled.CompressorThreadId != 0 && cancelled.CompressorThreadId != Thread.CurrentThread.ManagedThreadId) return null;
                    refusing = null;
                    cancelled.MarkClaimed();
                    refused = true;
                    return null;
                }
                expected = null;
                if (expectedPath != null && !string.Equals(path, expectedPath, StringComparison.Ordinal)) { misdirected = save; save = null; }
                expectedPath = null;
                (misdirected ?? save)!.MarkClaimed();
            }
            refused = true;
            if (misdirected != null)
            {
                if (!misdirected.Cancelled) misdirected.Spliced(SaveSpliceOutcome.Failed, "native compression read another temporary file", 0, 0);
                return null;
            }
            if (save!.Cancelled) return null;
            refused = false;
            return save;
        }

        public static bool? TryCompress(string path, string outputPath)
        {
            var save = Claim(path, out bool refused);
            if (save != null) return Compress(path, outputPath, save);
            if (refused) return false;
            PendingSave? earlier;
            lock (gate) earlier = publishing;
            if (earlier == null || !string.Equals(earlier.OutputPath, outputPath, StringComparison.Ordinal)) return null;
            long deadline = Stopwatch.GetTimestamp() + (long)(SameSlotWaitMilliseconds * (double)Stopwatch.Frequency / 1000);
            while (!earlier.SpliceDone)
            {
                if (Stopwatch.GetTimestamp() >= deadline)
                {
                    Interlocked.Increment(ref suppressedCompressions);
                    return false;
                }
                if (!Thread.Yield()) Thread.SpinWait(64);
            }
            return null;
        }

        public static int SameSlotWaitMilliseconds { get; internal set; } = PublishWaitMilliseconds;
        private static int suppressedCompressions;
        public static int SuppressedCompressions => Volatile.Read(ref suppressedCompressions);

        public static PendingSave? TakeSettledPublication()
        {
            lock (gate)
            {
                var settled = publishing;
                if (settled == null || !settled.SpliceDone) return null;
                publishing = null;
                return settled;
            }
        }

        public const int PublishWaitMilliseconds = 30000;

        public static bool AwaitCompression(PendingSave save, int claimMilliseconds = ClaimWaitMilliseconds,
            int spliceMilliseconds = CompressWaitMilliseconds + 10000, int publishMilliseconds = PublishWaitMilliseconds)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (save.CompressionSettled || !save.Serialized && !save.Claimed) return true;
            if (!save.WaitClaimed(claimMilliseconds) && Refuse(save, "native compression did not take the hand-off within "
                + Seconds(claimMilliseconds))) return true;
            if (save.WaitSpliced(spliceMilliseconds)) return true;
            if (save.Cancel("native compression did not finish within " + Seconds(spliceMilliseconds))) return true;
            if (save.WaitSpliced(publishMilliseconds)) return true;
            lock (gate) { if (!save.SpliceDone) publishing = save; }
            return save.SpliceDone;
        }

        private static bool Refuse(PendingSave save, string reason)
        {
            lock (gate)
            {
                if (save.Claimed || !save.Cancel(reason)) return false;
                if (ReferenceEquals(expected, save)) { expected = null; expectedPath = null; }
                if (ReferenceEquals(latest, save)) latest = null;
                refusing = save;
                return true;
            }
        }

        private static string Seconds(int milliseconds) =>
            (milliseconds / 1000.0).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " s";

        public static bool Compress(string path, string outputPath, PendingSave save, int waitMilliseconds = CompressWaitMilliseconds)
            => Compress(path, outputPath, save, waitMilliseconds, File.Create);

        internal static Action? BeforePublish { get; set; }

        internal static bool Compress(string path, string outputPath, PendingSave save, int waitMilliseconds, Func<string, Stream> open)
        {
            long started = Stopwatch.GetTimestamp();
            save.OutputPath = outputPath;
            long deadline = started + (long)(waitMilliseconds * (double)Stopwatch.Frequency / 1000);
            while (!save.Resolved && !save.Cancelled)
            {
                if (Stopwatch.GetTimestamp() >= deadline)
                {
                    save.Abort("The background save writer did not finish within "
                        + (waitMilliseconds / 1000).ToString(System.Globalization.CultureInfo.InvariantCulture) + " s; the save carries a refusal marker.");
                    break;
                }
                if (!Thread.Yield()) Thread.SpinWait(64);
            }
            long waited = Stopwatch.GetTimestamp();
            string[] tokens = { save.PayloadToken, save.MarkerToken };
            string[] values = { save.FilePayload, save.FileMarker };
            var outcome = SaveSpliceOutcome.Failed;
            string error = "";
            bool written = false;
            string staging = outputPath + StagingSuffix;
            try
            {
                using (var input = File.OpenRead(path))
                using (var output = new GZipStream(open(staging), CompressionLevel.Optimal))
                    outcome = SaveSplice.Copy(input, output, tokens, values);
                if (outcome != SaveSpliceOutcome.Spliced)
                    throw new InvalidDataException("the native save did not hold both Persistent City tokens exactly once");
                if (!save.TryBeginPublish())
                    throw new OperationCanceledException("the hand-off was cancelled before publication: " + save.CancelReason);
                BeforePublish?.Invoke();
                Publish(staging, outputPath);
                written = true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                if (outcome == SaveSpliceOutcome.Spliced) outcome = SaveSpliceOutcome.Failed;
                try { if (File.Exists(staging)) File.Delete(staging); }
                catch (Exception cleanup) { error += "; staging file cleanup failed: " + cleanup.Message; }
            }
            long done = Stopwatch.GetTimestamp();
            save.Spliced(outcome, error, (waited - started) * 1000.0 / Stopwatch.Frequency, (done - waited) * 1000.0 / Stopwatch.Frequency);
            return written;
        }

        private static void Publish(string staging, string outputPath)
        {
            if (!File.Exists(outputPath)) { File.Move(staging, outputPath); return; }
            try { File.Replace(staging, outputPath, null); }
            catch (PlatformNotSupportedException) { File.Copy(staging, outputPath, true); File.Delete(staging); }
        }
    }

    public sealed class SaveHandOffTracker
    {
        public PendingSave? Splicing { get; private set; }
        public PendingSave? Publishing { get; private set; }
        public bool Active => Splicing != null || Publishing != null;

        public void Track(PendingSave save) => Splicing = save ?? throw new ArgumentNullException(nameof(save));

        public PendingSave? AfterAwait(PendingSave save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (ReferenceEquals(Splicing, save)) Splicing = null;
            if (save.CompressionSettled) return save;
            if (save.Serialized || save.Claimed) Publishing = save;
            return null;
        }

        public PendingSave? TakeSettledSplice()
        {
            var splicing = Splicing;
            if (splicing == null || !splicing.CompressionSettled) return null;
            Splicing = null;
            return splicing;
        }

        public PendingSave? TakeSettledPublication()
        {
            var publishing = Publishing;
            if (publishing == null || !publishing.CompressionSettled) return null;
            Publishing = null;
            SaveHandOff.TakeSettledPublication();
            return publishing;
        }

        public void Drop(PendingSave save)
        {
            if (ReferenceEquals(Splicing, save)) Splicing = null;
        }
    }

    public static class SaveSplice
    {
        public const int ChunkBytes = 1 << 20;
        private const int MaxPatternBytes = 2048;
        private const int ScratchBytes = 1 << 16;
        private const byte JsonStart = (byte)'{';
        private static byte[]? pooledBuffer, pooledScratch;

        public static SaveSpliceOutcome Copy(Stream input, Stream output, string[] tokens, string[] values, int chunkBytes = ChunkBytes)
        {
            if (tokens == null || values == null || tokens.Length != values.Length || tokens.Length == 0) throw new ArgumentException("Every token needs one value.");
            foreach (string token in tokens)
            {
                if (string.IsNullOrEmpty(token) || 5 + 2 * token.Length > MaxPatternBytes) throw new ArgumentException("Token length is out of range.");
                foreach (char c in token)
                    if (c > 0x7e || c < 0x20 || c == '"' || c == '\\') throw new ArgumentException("Tokens are printable ASCII without quotes or backslashes.");
            }
            foreach (string value in values) if (value == null) throw new ArgumentException("Values cannot be null.");
            int size = Math.Max(chunkBytes, MaxPatternBytes) + MaxPatternBytes;
            byte[] buffer = Take(ref pooledBuffer, size);
            byte[] scratch = Take(ref pooledScratch, ScratchBytes);
            try { return CopyWith(input, output, tokens, values, buffer, scratch); }
            finally { Interlocked.Exchange(ref pooledBuffer, buffer); Interlocked.Exchange(ref pooledScratch, scratch); }
        }

        private static byte[] Take(ref byte[]? pool, int size)
        {
            byte[]? pooled = Interlocked.Exchange(ref pool, null);
            return pooled != null && pooled.Length == size ? pooled : new byte[size];
        }

        private static SaveSpliceOutcome CopyWith(Stream input, Stream output, string[] tokens, string[] values, byte[] buffer, byte[] scratch)
        {
            int filled = Fill(input, buffer, 0, buffer.Length);
            bool eof = filled < buffer.Length;
            bool json = filled > 0 && buffer[0] == JsonStart;
            var patterns = new List<KeyValuePair<byte[], int>>(tokens.Length * 2);
            int longest = 0;
            for (int i = 0; i < tokens.Length; i++)
            {
                if (json) patterns.Add(new KeyValuePair<byte[], int>(JsonPattern(tokens[i]), i));
                else
                {
                    patterns.Add(new KeyValuePair<byte[], int>(OdinPattern(tokens[i], true), i));
                    patterns.Add(new KeyValuePair<byte[], int>(OdinPattern(tokens[i], false), i));
                }
            }
            foreach (var pattern in patterns) longest = Math.Max(longest, pattern.Key.Length);
            var found = new int[tokens.Length];
            while (true)
            {
                int start = 0;
                while (true)
                {
                    int at = -1, which = -1;
                    var window = new ReadOnlySpan<byte>(buffer, start, filled - start);
                    for (int p = 0; p < patterns.Count; p++)
                    {
                        int index = window.IndexOf(patterns[p].Key);
                        if (index >= 0 && (at < 0 || index < at)) { at = index; which = p; }
                    }
                    if (which < 0) break;
                    int token = patterns[which].Value;
                    if (++found[token] > 1) throw new InvalidDataException("A pending save token appears more than once in the native save.");
                    output.Write(buffer, start, at);
                    if (json) WriteJson(output, values[token], scratch);
                    else WriteOdin(output, values[token], scratch);
                    start += at + patterns[which].Key.Length;
                }
                int safe = eof ? filled : Math.Max(start, filled - (longest - 1));
                output.Write(buffer, start, safe - start);
                if (eof) break;
                int keep = filled - safe;
                Buffer.BlockCopy(buffer, safe, buffer, 0, keep);
                int want = buffer.Length - keep;
                int read = Fill(input, buffer, keep, want);
                filled = keep + read;
                eof = read < want;
            }
            int spliced = 0;
            foreach (int count in found) if (count == 1) spliced++;
            return spliced == tokens.Length ? SaveSpliceOutcome.Spliced : spliced == 0 ? SaveSpliceOutcome.Absent : SaveSpliceOutcome.Partial;
        }

        private static int Fill(Stream input, byte[] buffer, int offset, int count)
        {
            int total = 0;
            while (total < count)
            {
                int read = input.Read(buffer, offset + total, count - total);
                if (read == 0) break;
                total += read;
            }
            return total;
        }

        internal static byte[] OdinPattern(string token, bool wide)
        {
            var bytes = new byte[5 + token.Length * (wide ? 2 : 1)];
            bytes[0] = wide ? (byte)1 : (byte)0;
            WriteInt32(bytes, 1, token.Length);
            for (int i = 0; i < token.Length; i++)
            {
                if (wide) { bytes[5 + 2 * i] = (byte)token[i]; bytes[6 + 2 * i] = 0; }
                else bytes[5 + i] = (byte)token[i];
            }
            return bytes;
        }

        internal static byte[] JsonPattern(string token)
        {
            var bytes = new byte[token.Length + 2];
            bytes[0] = bytes[bytes.Length - 1] = (byte)'"';
            for (int i = 0; i < token.Length; i++) bytes[i + 1] = (byte)token[i];
            return bytes;
        }

        private static void WriteInt32(byte[] bytes, int offset, int value)
        {
            bytes[offset] = (byte)value; bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16); bytes[offset + 3] = (byte)(value >> 24);
        }

        private static void WriteOdin(Stream output, string value, byte[] scratch)
        {
            scratch[0] = 1;
            WriteInt32(scratch, 1, value.Length);
            output.Write(scratch, 0, 5);
            int per = scratch.Length / 2;
            for (int offset = 0; offset < value.Length; offset += per)
            {
                int count = Math.Min(per, value.Length - offset);
                for (int i = 0; i < count; i++)
                {
                    char c = value[offset + i];
                    scratch[2 * i] = (byte)c; scratch[2 * i + 1] = (byte)(c >> 8);
                }
                output.Write(scratch, 0, 2 * count);
            }
        }

        private static void WriteJson(Stream output, string value, byte[] scratch)
        {
            var encoder = new UTF8Encoding(false).GetEncoder();
            int capacity = Math.Max(16, scratch.Length / 4 - 8);
            var chars = new char[capacity];
            int used = 0;
            chars[used++] = '"';
            foreach (char c in value)
            {
                if (used > capacity - 6) { Encode(output, encoder, chars, used, scratch, false); used = 0; }
                switch (c)
                {
                    case '"': chars[used++] = '\\'; chars[used++] = '"'; break;
                    case '\\': chars[used++] = '\\'; chars[used++] = '\\'; break;
                    case '\n': chars[used++] = '\\'; chars[used++] = 'n'; break;
                    case '\r': chars[used++] = '\\'; chars[used++] = 'r'; break;
                    case '\t': chars[used++] = '\\'; chars[used++] = 't'; break;
                    case '\b': chars[used++] = '\\'; chars[used++] = 'b'; break;
                    case '\f': chars[used++] = '\\'; chars[used++] = 'f'; break;
                    default:
                        if (c < 0x20 || c == 0x85 || c == 0x2028 || c == 0x2029)
                        {
                            chars[used++] = '\\'; chars[used++] = 'u';
                            chars[used++] = Hex(c >> 12); chars[used++] = Hex(c >> 8); chars[used++] = Hex(c >> 4); chars[used++] = Hex(c);
                        }
                        else chars[used++] = c;
                        break;
                }
            }
            chars[used++] = '"';
            Encode(output, encoder, chars, used, scratch, true);
        }

        private static char Hex(int value) => "0123456789abcdef"[value & 15];

        private static void Encode(Stream output, Encoder encoder, char[] chars, int count, byte[] scratch, bool flush)
        {
            int bytes = encoder.GetBytes(chars, 0, count, scratch, 0, flush);
            output.Write(scratch, 0, bytes);
        }
    }
}
