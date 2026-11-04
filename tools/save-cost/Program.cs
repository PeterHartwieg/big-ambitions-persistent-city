using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;

// F7a save-cost baseline (issue #94). Offline .NET 10 only: no Unity, game, rig or native save.
// Usage: SaveCost <fixture> [samples]
//   representative-957x2 | representative-5000x2 | representative-50000x2  (F3 integrated replay)
//   957x2 | 957x3 | 5000x2 | 5000x3 | 50000x2 | 50000x3 | churn | CF32 | CF64  (E5d stress matrix)
//   determinism  (same seed -> same hash, twice per fixture family)
static class SaveCostProgram
{
    static int Main(string[] args)
    {
        string name = args.Length == 0 ? "representative-957x2" : args[0];
        int samples = args.Length < 2 ? 7 : int.Parse(args[1]);
        Console.WriteLine(JsonConvert.SerializeObject(name == "determinism" ? Program.Determinism() : Program.Measure(name, samples)));
        return 0;
    }
}

static partial class Program
{
    // E5d retained candidate hashes (docs/perf/e5d-name-identity-save-results.json).
    static readonly Dictionary<string, string> E5dAfterSha256 = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["957x2"] = "AAC4681A8B54CD6CA3066E2F315231585BD4D34CC333A3F3A4FF27E6E5DF2C02",
        ["957x3"] = "89C2152B18633E0DDBC9A1606644B11FDB8927492C8CBF435349F18BEAB7146A",
        ["5000x2"] = "F9C4AF275C1B7C4B6F8B8870B86E5F32662DA86DD86E81E658B9CA4EC9C19DFE",
        ["5000x3"] = "F99D85116BCEC2A6A9E94359513A9C9B062EDA5F2AC604ECA2975C0365302D40",
        ["50000x2"] = "4D9E7DA40AF889D82C880CD3AD8A02C686FD87F48C49C72C6DBBE3E60508457C",
        ["50000x3"] = "212ED4C6314E65903A07430291DF45694DE9D62554B8D5B7A3D05CE3588424A1",
        ["CF32"] = "C686C4D49DEE7D4A6464EC175C719543BC782CC349870CAC4B6D07992FF07330",
        ["CF64"] = "1CADB22DCACD019FCBC3F67E9417890A9486B74F8259B98AC6A5C181FBA4C4D1",
    };
    // E5d retained maximum-escaping canonical sizes (no hash was retained): characters, UTF-8 bytes.
    static readonly (int Characters, int Bytes) E5dEncodingMax = (149224, 411368);
    // F3 retained integrated-replay final save hashes (docs/perf/assets/f3-bounded-bulk/after-integrated.jsonl).
    static readonly Dictionary<int, string> F3IntegratedSha256 = new Dictionary<int, string>
    {
        [957] = "97853DFDACE36EF1FCA0712AAB12DEFD4530307FD0D71CBA81C2AF38033C6F8C",
        [5000] = "A8EE0E722E498CB8550C792FA0AAEF908B1FE2C4840F42C22AD59673D8CC721F",
        [50000] = "0976963E8199774C9150A8B972CBCDA102F9A39124495D9BAA8E1CE0E1BC6A79",
    };

    static int Residents(string name) => int.Parse(name.Substring("representative-".Length).Split('x')[0]);

    public static object Determinism()
    {
        var rows = new List<object>();
        foreach (int residents in new[] { 957, 5000 })
        {
            string a = SaveCodec.Write(RepresentativeFixture.Build(residents).State), b = SaveCodec.Write(RepresentativeFixture.Build(residents).State);
            string other = SaveCodec.Write(RepresentativeFixture.Build(residents, RepresentativeFixture.Seed + 1).State);
            Check(a == b, "Representative fixture is not deterministic at " + residents);
            Check(a != other, "Seed does not reach the representative fixture at " + residents);
            rows.Add(new { fixture = "representative-" + residents + "x2", seed = RepresentativeFixture.Seed, sha256 = RepresentativeFixture.Sha256(a),
                repeatEqual = true, otherSeed = RepresentativeFixture.Seed + 1, otherSeedSha256 = RepresentativeFixture.Sha256(other) });
        }
        foreach (string name in new[] { "957x2", "5000x2", "encoding-max" })
        {
            string a = SaveCodec.Write(StressFixture(name)), b = SaveCodec.Write(StressFixture(name));
            Check(a == b, "Stress fixture is not deterministic: " + name);
            bool e5dEqual = name == "encoding-max" ? (a.Length, Encoding.UTF8.GetByteCount(a)) == E5dEncodingMax
                : RepresentativeFixture.Sha256(a) == E5dAfterSha256[name];
            rows.Add(new { fixture = name, seed = 11, sha256 = RepresentativeFixture.Sha256(a), repeatEqual = true, e5dEqual,
                e5dComparison = name == "encoding-max" ? "retained characters and UTF-8 bytes" : "retained afterSha256" });
        }
        return new { mode = "determinism", rows };
    }

    public static object Measure(string name, int samples)
    {
        bool representative = name.StartsWith("representative-", StringComparison.Ordinal);
        long buildStart = Stopwatch.GetTimestamp();
        RepresentativeFixture.Replay? replay = representative ? RepresentativeFixture.Build(Residents(name)) : null;
        CityState state = replay?.State ?? StressFixture(name);
        double buildMs = Stopwatch.GetElapsedTime(buildStart).TotalMilliseconds;

        // Non-intrusion: diagnostics off and on give the same bytes, before and after timing.
        string off = SaveCodec.Write(state);
        var probe = new SaveCostRecorder(); probe.Begin();
        string on = SaveCodec.Write(state, probe); probe.End(true, "");
        Check(off == on, "Diagnostics changed payload bytes.");
        string sha = RepresentativeFixture.Sha256(off);
        string? expected = representative ? F3IntegratedSha256.GetValueOrDefault(Residents(name)) : E5dAfterSha256.GetValueOrDefault(name);
        bool? e5dSizesEqual = name == "encoding-max" ? (off.Length, Encoding.UTF8.GetByteCount(off)) == E5dEncodingMax : null;

        // Mirror the runtime session: bound to the live dictionary, so later writes take the trusted publish path.
        var data = new Dictionary<string, string>(StringComparer.Ordinal) { [SaveCodec.StateKey] = off, ["other:sentinel"] = "keep" };
        var session = SaveSession.Open(data, state.PopulationKey);
        Check(session.InitialReadResult.Status == SaveReadStatus.Loaded, session.InitialReadResult.Error);
        var cost = new SaveCostRecorder();
        var phases = Enum.GetValues<SaveCostPhase>().ToDictionary(p => p, _ => new List<double>());
        var totalOn = new List<double>(); var totalOff = new List<double>(); var allocOn = new List<long>(); var allocOff = new List<long>();
        for (int i = 0; i <= samples; i++)
        {
            // Alternate off/on so host drift affects both arms alike; sample 0 is the discarded warmup.
            long bytes = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
            var plain = session.WriteTo(data, state);
            double plainMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds; long plainBytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
            Check(plain.Success && data[SaveCodec.StateKey] == off, "Diagnostics-off write changed bytes or failed: " + plain.Error);
            bytes = GC.GetAllocatedBytesForCurrentThread();
            cost.Begin();
            var timed = session.WriteTo(data, state, cost);
            cost.End(timed.Success, timed.Error);
            long timedBytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
            Check(timed.Success && data[SaveCodec.StateKey] == off && data["other:sentinel"] == "keep", "Diagnostics-on write changed bytes or failed: " + timed.Error);
            Check(Enum.GetValues<SaveCostPhase>().All(cost.Covered), "A save phase was not covered.");
            Check(Enum.GetValues<SaveCostPhase>().Sum(p => cost.PhaseTicks(p)) == cost.TotalTicks, "Phase timers do not partition the measured span.");
            Check(cost.PayloadCharacters == off.Length && cost.PayloadUtf8Bytes == Encoding.UTF8.GetByteCount(off), "Payload size record differs.");
            if (i == 0) continue;
            foreach (var p in phases.Keys) phases[p].Add(cost.Milliseconds(p));
            totalOn.Add(cost.TotalMilliseconds); totalOff.Add(plainMs); allocOn.Add(timedBytes); allocOff.Add(plainBytes);
        }
        var counts = SaveCodec.CountRecords(state, off);

        // Load: background strict read (+migration-free 25 decode) and activation validation, then the
        // main-thread CitySimulator construction that CompleteInitialization performs.
        var read = new List<double>(); var readAlloc = new List<long>(); var validate = new List<double>(); var construct = new List<double>();
        var planner = replay?.RoutePlanner ?? new RepresentativeFixture.Routes();
        for (int i = 0; i <= samples; i++)
        {
            long bytes = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
            var result = SaveCodec.Read(off);
            double readMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds; long readBytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
            Check(result.Status == SaveReadStatus.Loaded, result.Error);
            start = Stopwatch.GetTimestamp(); SaveCodec.ValidateForActivation(result.State!);
            double validateMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            start = Stopwatch.GetTimestamp(); _ = new CitySimulator(result.State!, planner);
            double constructMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            if (i == 0) continue;
            read.Add(readMs); readAlloc.Add(readBytes); validate.Add(validateMs); construct.Add(constructMs);
        }
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); long baseline = GC.GetTotalMemory(true);
        var retainedRead = SaveCodec.Read(off); Check(retainedRead.Status == SaveReadStatus.Loaded, retainedRead.Error);
        long retained = GC.GetTotalMemory(true) - baseline; GC.KeepAlive(retainedRead);

        var food = state.Food;
        return new
        {
            fixture = name, kind = representative ? "representative (F3 integrated Core replay, natural one-day histories)" : "stress (E5d saturated matrix)",
            schema = state.Schema, writer = CityState.CurrentSchema, samples, seed = state.Seed, buildMs,
            replayEvents = replay?.Events, purchases = food?.Residents.Sum(m => m.Purchases), routes = replay?.RoutePlanner.Calls,
            residents = state.Citizens.Count, consumers = food?.Residents.Count, places = state.Places.Count, historyRows = food?.History.Count,
            historyCells = food?.History.Sum(r => r.ProductSales.Count), scopes = food?.Shops.Count, productNames = state.ProductNames.Count,
            recentVisits = state.Citizens.Sum(c => c.RecentVisits.Count), trafficLinks = state.Traffic?.Definitions.Count ?? 0,
            payload = new { characters = off.Length, utf8Bytes = Encoding.UTF8.GetByteCount(off), utf16StorageBytes = 2L * off.Length,
                streamingRecords = counts.Streaming, semanticRecords = counts.Semantic, sha256 = sha,
                expectedSha256 = expected, expectedSha256Equal = expected == null ? (bool?)null : expected == sha,
                diagnosticsOnOffBytesEqual = true, e5dSizesEqual },
            save = new
            {
                phasesMs = phases.ToDictionary(p => p.Key.ToString().ToLowerInvariant(), p => Stats(p.Value)),
                observeNote = "observe is runtime-only (RLO read, BeforeSave, campaign completion, ownership refresh, epoch check); offline it is the call entry only",
                totalDiagnosticsOnMs = Stats(totalOn), totalDiagnosticsOffMs = Stats(totalOff),
                allocatedBytesDiagnosticsOn = LongStats(allocOn), allocatedBytesDiagnosticsOff = LongStats(allocOff),
                allocationToUtf16Ratio = Math.Round(allocOff.OrderBy(x => x).ElementAt(allocOff.Count / 2) / (2.0 * off.Length), 3),
            },
            load = new { backgroundReadMs = Stats(read), backgroundReadAllocatedBytes = LongStats(readAlloc), backgroundActivationValidateMs = Stats(validate),
                mainThreadSimulatorConstructionMs = Stats(construct), retainedBytesAfterRead = retained },
            escapedSource = name == "encoding-max" ? EscapedRead(off, samples) : null,
            processPeakWorkingSetBytes = Process.GetCurrentProcess().PeakWorkingSet64,
            evidence = "Windows .NET 10 synthetic, foreground, one process per fixture; save/load are their own latency criterion, never ordinary-play mod-work; no Unity, Mono, native save or whole-frame evidence"
        };
    }

    // Every non-ASCII character spelled as a JSON escape: the reader must decode it to the same canonical bytes.
    static object EscapedRead(string canonical, int samples)
    {
        var settings = new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii };
        string escaped = JsonConvert.SerializeObject(Newtonsoft.Json.Linq.JObject.Parse(canonical), Formatting.None, settings);
        var times = new List<double>(); var alloc = new List<long>();
        for (int i = 0; i <= samples; i++)
        {
            long bytes = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
            var result = SaveCodec.Read(escaped);
            double ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds; long used = GC.GetAllocatedBytesForCurrentThread() - bytes;
            Check(result.Status == SaveReadStatus.Loaded && SaveCodec.Write(result.State!) == canonical, "Escaped source did not decode to the canonical bytes.");
            if (i == 0) continue;
            times.Add(ms); alloc.Add(used);
        }
        return new { characters = escaped.Length, utf8Bytes = Encoding.UTF8.GetByteCount(escaped), readMs = Stats(times), readAllocatedBytes = LongStats(alloc), canonicalRewriteEqual = true };
    }

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
}
