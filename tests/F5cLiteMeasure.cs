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

public sealed class F5cLiteMeasure
{
    private static object Stats(List<double> values)
    {
        var s = values.OrderBy(v => v).ToList();
        return new { p50 = Math.Round(s[s.Count / 2], 3), p95 = Math.Round(s[Math.Min(s.Count - 1, (int)Math.Ceiling(s.Count * .95) - 1)], 3),
            max = Math.Round(s[^1], 3), atLeast30Ms = s.Count(v => v >= 30) };
    }

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
