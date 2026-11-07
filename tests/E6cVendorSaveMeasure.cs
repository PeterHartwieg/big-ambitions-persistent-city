using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

/// <summary>E6c offline save cost (docs/perf/e6c.md): payload size and SaveCodec write/read time with 0, 8 and 64 live
/// vendors, 128 live plus retired, and 1,024 tombstones, on the small food city and on the representative 957x2 city.
/// Every row with a stand has its Vehicle scope; with references, every row also has a business reference. Measurement
/// only, not a gate: it runs when PC_E6C_MEASURE is set, e.g.
/// <c>PC_E6C_MEASURE=1 dotnet test tests/PersistentCity.Tests.csproj --filter FullyQualifiedName~E6cVendorSaveMeasure --logger "console;verbosity=detailed"</c>.</summary>
[Collection(StreetFoodHooks.Collection)]
public sealed class E6cVendorSaveMeasure
{
    private readonly ITestOutputHelper output;
    public E6cVendorSaveMeasure(ITestOutputHelper output) { this.output = output; }

    private static (double p50, double p95, double max) Stats(List<double> values)
    {
        var s = values.OrderBy(v => v).ToList();
        return (s[s.Count / 2], s[Math.Min(s.Count - 1, (int)Math.Ceiling(s.Count * .95) - 1)], s[^1]);
    }

    private static readonly (string Name, int Live, int Retired, int Tombstones)[] Shapes =
    {
        ("0 live", 0, 0, 0), ("8 live", 8, 0, 0), ("64 live", 64, 0, 0), ("64 live + 64 retired", 64, 64, 0), ("1,024 tombstones", 0, 0, 1024),
        ("64 + 64 + 1,024 (full table)", 64, 64, 1024),
    };

    [Fact]
    public void SaveBytesAndReadWriteTime()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PC_E6C_MEASURE"))) return; // measurement only on request
        foreach (string city in new[] { "small", "representative-957x2" })
        {
            int samples = city == "small" ? 200 : 20;
            int baseline = 0, baselineBytes = 0;
            foreach (var (name, live, retired, tombstones) in Shapes)
            {
                var sim = city == "small" ? WeightedFoodChoiceTests.Ready(5).city : F7cFixtures.Build(city).City;
                VendorSaveTests.Fill(sim, live, retired, tombstones, references: true);
                var state = sim.State;
                string payload = SaveCodec.Write(state);
                int bytes = Encoding.UTF8.GetByteCount(payload);
                if (live + retired + tombstones == 0) { baseline = payload.Length; baselineBytes = bytes; }
                int section = ("\"Vendors\":" + Newtonsoft.Json.Linq.JObject.Parse(payload)["Vendors"]!.ToString(Newtonsoft.Json.Formatting.None)).Length;
                var write = new List<double>(); var read = new List<double>();
                for (int i = 0; i < 3; i++) { SaveCodec.Write(state); SaveCodec.Read(payload); }
                for (int i = 0; i < samples; i++)
                {
                    long start = Stopwatch.GetTimestamp(); SaveCodec.Write(state); write.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                    start = Stopwatch.GetTimestamp(); var result = SaveCodec.Read(payload); read.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                    Assert.Equal(SaveReadStatus.Loaded, result.Status);
                }
                var (w50, w95, wmax) = Stats(write); var (r50, r95, rmax) = Stats(read);
                output.WriteLine($"{city} | {name} | {payload.Length} chars | {bytes} B | +{payload.Length - baseline} chars / +{bytes - baselineBytes} B | vendor section {section} chars "
                    + $"| write p50 {w50:0.000} p95 {w95:0.000} max {wmax:0.000} ms | read p50 {r50:0.000} p95 {r95:0.000} max {rmax:0.000} ms | {samples} samples");
            }
        }
    }
}
