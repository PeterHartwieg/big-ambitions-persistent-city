using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Tools.CompactFormat;

// F7p compact-format prototype (issue #94, plan section 3.6). Offline .NET 10 only: no Unity, game, rig or
// native save, and nothing here is wired into Core, Persistence or Runtime. Writer 25 is unchanged.
// Usage: CompactFormat <fixture> <candidate A|B|C|D> [samples]
//   fixtures: representative-957x2 | representative-5000x2 | representative-50000x2 (F7a replay)
//             957x2 | 957x3 | 5000x2 | 5000x3 | 50000x2 | 50000x3 | churn | CF32 | CF64 | encoding-max (E5d stress)
//             file:<path>  a writer-25 payload extracted from a saved checkpoint (rig check 1; never committed)
static class CompactFormatProgram
{
    /// <summary>
    /// Peter's section 9 Q2/Q3 defaults: keep 14 days of shop history and today's visit limit. The prototype
    /// therefore prunes nothing; it only reports bytes per history family so a later retention change is a
    /// visible, separate decision. Change these only with a recorded answer from Peter.
    /// </summary>
    public const int ShopHistoryDays = 14;
    public const int RecentVisitsLimit = SaveCodec.MaxRecentVisits;
    /// <summary>Fields reported in the per-path size table: at least this share of the writer-25 payload.</summary>
    const double PathShare = 0.005;

    static int Main(string[] args)
    {
        string fixture = args.Length > 0 ? args[0] : "representative-957x2";
        string candidate = args.Length > 1 ? args[1] : "C";
        int samples = args.Length > 2 && candidate != "dump" ? int.Parse(args[2]) : 7;
        // Diagnostic only: write the synthetic writer-25 payload to a local path outside the repository (never committed).
        if (candidate == "dump") { File.WriteAllText(args[2], SaveCodec.Write(Fixture(fixture))); return 0; }
        Console.WriteLine(JsonConvert.SerializeObject(Measure(fixture, Candidate.Get(candidate), samples)));
        return 0;
    }

    static CityState Fixture(string name) => name.StartsWith("representative-", StringComparison.Ordinal)
        ? RepresentativeFixture.Build(int.Parse(name.Substring("representative-".Length).Split('x')[0])).State
        : Program.StressFixture(name);

    static object Measure(string fixture, Candidate candidate, int samples)
    {
        long start = Stopwatch.GetTimestamp();
        string? extracted = fixture.StartsWith("file:", StringComparison.Ordinal) ? File.ReadAllText(fixture.Substring(5)) : null;
        var state = extracted == null ? Fixture(fixture) : ReadCurrent(extracted);
        double buildMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        string writer25 = extracted ?? SaveCodec.Write(state);
        string writer25Sha = RepresentativeFixture.Sha256(writer25);
        var records = SaveCodec.CountRecords(state, writer25);

        // The strict writer-25 reader accepts the source and rewrites it to the same bytes, so byte equality of
        // any decoded candidate with the source is semantic equality under today's reader.
        var strict = SaveCodec.Read(writer25);
        Check(strict.Status == SaveReadStatus.Loaded, "Strict reader refused the writer-25 source: " + strict.Error);
        Check(SaveCodec.Write(strict.State!) == writer25, "Strict read/write of the source is not byte-identical.");
        strict = null!;

        object? tree = WireTree.Parse(writer25);
        Check(WireTree.Write(tree) == writer25, "Wire tree does not reproduce writer-25 bytes.");
        var leaves = WireTree.Count(tree);
        // Capture, validation and activation do not depend on the wire candidate, so they are measured once per
        // fixture, in candidate A's process, on the same state and tree (section 3.6 matrix, shared columns).
        object? common = candidate.Id == "A" ? CommonPhases(state, writer25, tree, samples) : null;
        state = null!;

        var stats = new ColumnarStats();
        string payload = candidate.Encode(tree, stats);
        var encode = new List<double>(); var encodeAlloc = new List<long>(); var collectMs = new List<double>(); var writeMs = new List<double>();
        for (int i = 0; i <= samples; i++)
        {
            var split = new ColumnarStats();
            long bytes = GC.GetAllocatedBytesForCurrentThread(); start = Stopwatch.GetTimestamp();
            string again = candidate.Encode(tree, split);
            double ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds; long used = GC.GetAllocatedBytesForCurrentThread() - bytes;
            Check(again == payload, "Encoding is not deterministic.");
            if (i == 0) continue;
            encode.Add(ms); encodeAlloc.Add(used);
            collectMs.Add(split.CollectTicks * 1000.0 / Stopwatch.Frequency); writeMs.Add(split.WriteTicks * 1000.0 / Stopwatch.Frequency);
        }
        var decode = new List<double>(); var decodeAlloc = new List<long>();
        for (int i = 0; i <= samples; i++)
        {
            long bytes = GC.GetAllocatedBytesForCurrentThread(); start = Stopwatch.GetTimestamp();
            var decodedSample = candidate.Decode(payload);
            double ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds; long used = GC.GetAllocatedBytesForCurrentThread() - bytes;
            GC.KeepAlive(decodedSample);
            if (i == 0) continue;
            decode.Add(ms); decodeAlloc.Add(used);
        }

        // Exact semantic round-trip: field by field against the source tree, writer-25 bytes, and decode -> re-encode identity.
        var decoded = candidate.Decode(payload);
        Check(WireTree.DeepEqual(tree, decoded, out string difference), "Round-trip difference: " + difference);
        bool writer25Identical = WireTree.Write(decoded, writer25.Length + 16) == writer25;
        Check(writer25Identical, "Decoded tree does not rewrite the writer-25 bytes.");
        bool reencodeIdentical = candidate.Encode(decoded) == payload;
        Check(reencodeIdentical, "Decode -> re-encode is not byte-identical.");
        var stringLeaves = new HashSet<object>(ReferenceEqualityComparer.Instance); long stringCount = 0;
        CollectStrings(decoded, stringLeaves, ref stringCount);
        decoded = null;

        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); long baseline = GC.GetTotalMemory(true);
        var retainedTree = candidate.Decode(payload);
        long retained = GC.GetTotalMemory(true) - baseline; GC.KeepAlive(retainedTree); retainedTree = null;

        int utf8 = Encoding.UTF8.GetByteCount(payload);
        return new
        {
            fixture = extracted == null ? fixture : "file", candidate = candidate.Id, description = candidate.Description, samples, buildMs,
            source = new { writer = CityState.CurrentSchema, characters = writer25.Length, utf8Bytes = Encoding.UTF8.GetByteCount(writer25),
                sha256 = writer25Sha, streamingRecords = records.Streaming, semanticRecords = records.Semantic,
                strictReaderAcceptsAndRewritesIdentically = true },
            payload = new
            {
                characters = payload.Length, utf8Bytes = utf8, utf16StorageBytes = 2L * payload.Length,
                characterRatioToWriter25 = Math.Round((double)payload.Length / writer25.Length, 4),
                sha256 = RepresentativeFixture.Sha256(payload),
                // Estimate of the native compressor's view: gzip of the UTF-16LE string (Odin stores modData as UTF-16).
                gzipUtf16Bytes = GzipUtf16(payload), writer25GzipUtf16Bytes = candidate.Id == "A" ? (long?)null : GzipUtf16(writer25),
                containers = candidate.Id == "A" ? leaves.Objects + leaves.Arrays : stats.Containers,
                tables = candidate.Id == "A" ? null : new { strings = stats.Strings, keys = stats.Keys, shapes = stats.Shapes, routes = stats.Routes,
                    routeReferences = stats.RouteReferences, roadTags = stats.RoadTags, entries = stats.TableEntries },
                // Semantic records are the entities themselves; payload-local table entries charge one record each (plan section 5).
                recordsAgainstCap = candidate.Id == "A" ? records.Semantic : records.Semantic + stats.TableEntries,
                recordCap = SaveCodec.MaxEmbeddedRecords,
            },
            encodeMs = Stats(encode), encodeAllocatedBytes = LongStats(encodeAlloc),
            // Columnar candidates only: table pass vs column writer (D's deflate and base64 are the remainder).
            encodeSplitMs = candidate.Id == "A" ? null : new { tablePass = Stats(collectMs), columnWriter = Stats(writeMs) },
            decodeMs = Stats(decode), decodeAllocatedBytes = LongStats(decodeAlloc),
            retainedBytesAfterDecode = retained,
            formatIndependentPhases = common,
            decodedStrings = new { leaves = stringCount, distinctInstances = stringLeaves.Count },
            roundTrip = new { fieldsCompared = leaves.Fields, objects = leaves.Objects, arrays = leaves.Arrays, strings = leaves.Strings,
                numbers = leaves.Numbers, bools = leaves.Bools, nulls = leaves.Nulls, differences = 0, writer25Identical, reencodeIdentical },
            pathCharacters = PathSizes(candidate.Id == "A" ? Writer25Paths(tree) : stats.PathCharacters, writer25.Length),
            retention = new { shopHistoryDays = ShopHistoryDays, recentVisitsLimit = RecentVisitsLimit, pruned = false },
            processPeakWorkingSetBytes = Process.GetCurrentProcess().PeakWorkingSet64,
            evidence = "Windows .NET 10 synthetic, foreground, one process per fixture and candidate; encode/decode run from/to the detached wire tree (F7c snapshot stand-in); capture, validation and CityState materialization are not in these timings"
        };
    }

    /// <summary>
    /// Format-independent save and load phases. Synchronize and validate come from F7a's SaveCostRecorder around
    /// SaveCodec.Write on this fixture (today's PrepareForSave and Validate; F7c moves validation onto the snapshot).
    /// Capture is proxied by a detached deep copy of the exact wire tree: every container copied, immutable leaves
    /// shared, which is the copy a snapshot capture performs, minus reading the live graph. Materialization is an
    /// upper bound: the strict writer-25 reader on the source text (a real F7d decoder materializes from its own
    /// columns without a second text parse). Activation validate and simulator construction are F7a's
    /// CompleteInitialization steps on the materialized state.
    /// </summary>
    static object CommonPhases(CityState state, string writer25, object? tree, int samples)
    {
        var synchronize = new List<double>(); var validate = new List<double>(); var capture = new List<double>(); var captureAlloc = new List<long>();
        for (int i = 0; i <= samples; i++)
        {
            var cost = new SaveCostRecorder(); cost.Begin();
            string written = SaveCodec.Write(state, cost); cost.End(true, "");
            Check(written == writer25, "Recorded write changed the payload.");
            long bytes = GC.GetAllocatedBytesForCurrentThread(), started = Stopwatch.GetTimestamp();
            var copy = WireTree.Clone(tree);
            double copyMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds; long copyBytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
            Check(i > 0 || WireTree.DeepEqual(tree, copy, out _), "Detached copy differs.");
            GC.KeepAlive(copy);
            if (i == 0) continue;
            synchronize.Add(cost.Milliseconds(SaveCostPhase.Synchronize)); validate.Add(cost.Milliseconds(SaveCostPhase.Validate));
            capture.Add(copyMs); captureAlloc.Add(copyBytes);
        }
        var materialize = new List<double>(); var activation = new List<double>(); var construct = new List<double>();
        var planner = new RepresentativeFixture.Routes();
        for (int i = 0; i <= samples; i++)
        {
            long started = Stopwatch.GetTimestamp();
            var read = SaveCodec.Read(writer25);
            double readMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            Check(read.Status == SaveReadStatus.Loaded, read.Error);
            started = Stopwatch.GetTimestamp(); SaveCodec.ValidateForActivation(read.State!);
            double validateMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            started = Stopwatch.GetTimestamp(); _ = new CitySimulator(read.State!, planner);
            double constructMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            if (i == 0) continue;
            materialize.Add(readMs); activation.Add(validateMs); construct.Add(constructMs);
        }
        return new
        {
            synchronizeMs = Stats(synchronize), validateMs = Stats(validate),
            captureDetachedCopyMs = Stats(capture), captureDetachedCopyAllocatedBytes = LongStats(captureAlloc),
            materializeUpperBoundMs = Stats(materialize), activationValidateMs = Stats(activation), simulatorConstructionMs = Stats(construct),
            provenance = "synchronize/validate: F7a SaveCostRecorder phases of SaveCodec.Write on this fixture; capture: detached deep copy of the wire tree (proxy); materialize: strict writer-25 SaveCodec.Read of the source text (upper bound); activation validate and construction: F7a CompleteInitialization steps",
        };
    }

    /// <summary>An extracted payload must already be current-writer bytes: older schemas would migrate and differ.</summary>
    static CityState ReadCurrent(string payload)
    {
        var read = SaveCodec.Read(payload);
        Check(read.Status == SaveReadStatus.Loaded, "Strict reader refused the extracted payload: " + read.Error);
        Check(read.SourceSchema == CityState.CurrentSchema, "Extracted payload is schema " + read.SourceSchema + ", not writer " + CityState.CurrentSchema);
        return read.State!;
    }

    static void CollectStrings(object? value, HashSet<object> instances, ref long count)
    {
        switch (value)
        {
            case string s: instances.Add(s); count++; break;
            case WireObject o: foreach (var v in o.Values) CollectStrings(v, instances, ref count); break;
            case object?[] a: foreach (var v in a) CollectStrings(v, instances, ref count); break;
        }
    }

    static long GzipUtf16(string payload)
    {
        byte[] bytes = Encoding.Unicode.GetBytes(payload);
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.Optimal, leaveOpen: true)) gzip.Write(bytes, 0, bytes.Length);
        return buffer.Length;
    }

    /// <summary>Writer-25 characters per field path, with the same path names the columnar encoder reports.</summary>
    static Dictionary<string, long> Writer25Paths(object? tree)
    {
        var sizes = new Dictionary<string, long>(StringComparer.Ordinal);
        var scratch = new StringBuilder();
        long Walk(object? value, string path, int depth)
        {
            long length;
            switch (value)
            {
                case WireObject o:
                    length = 2 + Math.Max(0, o.Keys.Length - 1);
                    for (int i = 0; i < o.Keys.Length; i++) length += Text(o.Keys[i]) + 1 + Walk(o.Values[i], path + "." + o.Keys[i], depth + 1);
                    break;
                case object?[] a:
                    length = 2 + Math.Max(0, a.Length - 1);
                    foreach (var v in a) length += Walk(v, path + "[]", depth + 1);
                    break;
                case string s: length = Text(s); break;
                default: scratch.Clear(); WireTree.Write(scratch, value); length = scratch.Length; break;
            }
            if (depth <= ColumnarCodec.PathDepth) sizes[path] = (sizes.TryGetValue(path, out long sum) ? sum : 0) + length;
            return length;
        }
        long Text(string s) { scratch.Clear(); WireTree.WriteString(scratch, s); return scratch.Length; }
        Check(Walk(tree, "$", 0) > 0, "Empty tree.");
        return sizes;
    }

    static Dictionary<string, long> PathSizes(Dictionary<string, long> sizes, long writer25Characters)
        => sizes.Where(p => p.Value >= writer25Characters * PathShare).OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary(p => p.Key, p => p.Value);

    static object Stats(List<double> values)
    {
        var s = values.OrderBy(v => v).ToList();
        return new { p50 = s[s.Count / 2], p95 = s[Math.Min(s.Count - 1, (int)Math.Ceiling(s.Count * .95) - 1)], max = s[^1], atLeast30Ms = s.Count(v => v >= 30) };
    }

    static object LongStats(List<long> values)
    {
        var s = values.OrderBy(v => v).ToList();
        return new { median = s[s.Count / 2], max = s[^1] };
    }

    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
