using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>F5c-lite measurement harness (docs/perf/f5c-lite.md), not a gate: it runs only when
/// PC_F5C_LITE_MEASURE names an output file, e.g.
/// <c>PC_F5C_LITE_MEASURE=out.jsonl dotnet test tests/PersistentCity.Tests.csproj -c Release --filter FullyQualifiedName~F5cLiteMeasure</c>.
/// X1 removed its writer-25 versus writer-26 save-cost arm with the pre-26 reader chain; the visitor-ordering measure remains.</summary>
public sealed class F5cLiteMeasure
{
    private static object Stats(List<double> values)
    {
        var s = values.OrderBy(v => v).ToList();
        return new { p50 = Math.Round(s[s.Count / 2], 3), p95 = Math.Round(s[Math.Min(s.Count - 1, (int)Math.Ceiling(s.Count * .95) - 1)], 3),
            max = Math.Round(s[^1], 3), atLeast30Ms = s.Count(v => v >= 30) };
    }

    /// <summary>Perf win in code this slice adds: represented visitors were first kept ordinal by binary-search insert
    /// (O(n) shift per visitor), now appended and sorted once. Measured on the in-game RLO ID order c0, c1, ...</summary>
    [Fact]
    public void VisitorEnrolmentOrderingInsertVersusSortOnce()
    {
        string? output = PerfGate.Variable("PC_F5C_LITE_MEASURE");
        if (string.IsNullOrEmpty(output)) return;
        foreach (int residents in new[] { 957, 5000, 50000 })
            foreach (int factor in new[] { 2, 3 })
            {
                var ids = new List<string>();
                for (int r = 0; r < residents; r++)
                    for (int slot = 1; slot < factor; slot++) ids.Add(FoodRepresentation.ConsumerId("c" + r, slot));
                double Insert()
                {
                    var list = new List<VisitorPerson>(); long start = Stopwatch.GetTimestamp();
                    foreach (var id in ids)
                    {
                        int low = 0, high = list.Count - 1;
                        while (low <= high) { int mid = low + ((high - low) >> 1); if (string.CompareOrdinal(list[mid].Id, id) < 0) low = mid + 1; else high = mid - 1; }
                        list.Insert(low, new VisitorPerson { Id = id });
                    }
                    return Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                }
                double SortOnce()
                {
                    var city = new CityState(); long start = Stopwatch.GetTimestamp();
                    foreach (var id in ids) PersonTable.AddRepresentedVisitor(city, id);
                    PersonTable.OrderVisitors(city);
                    return Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                }
                Insert(); SortOnce();
                var before = Enumerable.Range(0, 5).Select(_ => Insert()).ToList(); var after = Enumerable.Range(0, 5).Select(_ => SortOnce()).ToList();
                File.AppendAllText(output!, JsonConvert.SerializeObject(new { measure = "visitor-enrolment-ordering", residents, factor, visitors = ids.Count,
                    insertMs = Stats(before), appendSortMs = Stats(after) }) + Environment.NewLine);
            }
    }
}
