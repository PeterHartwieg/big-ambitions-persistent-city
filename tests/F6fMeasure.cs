using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

[Collection(StreetFoodHooks.Collection)]
public sealed class F6fMeasure
{
    private readonly ITestOutputHelper output;
    public F6fMeasure(ITestOutputHelper output) { this.output = output; }
    private void Report(string name, double[] ms, long bytes, object? extra = null)
    {
        Array.Sort(ms);
        string text = JsonConvert.SerializeObject(new { name, n = ms.Length, p50 = ms[ms.Length / 2],
            p95 = ms[(int)Math.Ceiling(ms.Length * .95) - 1], max = ms[^1], bytesPerOperation = bytes / (double)ms.Length,
            spikes30 = ms.Where(t => t >= 30).ToArray(), extra });
        output.WriteLine(text); File.AppendAllText(PerfGate.Variable("PC_F6F_MEASURE")!, text + Environment.NewLine);
    }
    private void Measure(string name, Action action, int n = 20000)
    {
        for (int i = 0; i < 1000; i++)
        {
            long bytes = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp(); action();
            _ = Stopwatch.GetElapsedTime(start); _ = GC.GetAllocatedBytesForCurrentThread() - bytes;
        }
        var times = new double[n]; long allocated = 0;
        for (int i = 0; i < n; i++)
        {
            long bytes = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp(); action();
            times[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            allocated += GC.GetAllocatedBytesForCurrentThread() - bytes;
        }
        Report(name, times, allocated);
        if (!name.Contains("naive") && !name.Contains("line")) Assert.Equal(0, allocated);
    }
    [Fact]
    public void AdmissionLookupPublicationAndRequestedLine()
    {
        if (PerfGate.Variable("PC_F6F_MEASURE") == null) return;
        var budget = OrdinaryExchangeTests.Wallet(100000); var price = Money.FromCents(500);
        Measure("admission gate", () => { if (!OrdinaryExchanges.CanAdmit(budget, price, 1501)) throw new Exception("refusal"); });
        foreach (int count in new[] { 1914, 10000, 100000 })
        {
            var owners = new Dictionary<string, ResidentBudget>(count, StringComparer.Ordinal);
            var ids = new string[count];
            for (int i = 0; i < count; i++) { ids[i] = "owner" + i; owners.Add(ids[i], OrdinaryExchangeTests.Wallet(100000)); }
            int cursor = 0;
            Measure($"wallet lookup and admission {count}", () => {
                var b = owners[ids[cursor++ % count]];
                if (!OrdinaryExchanges.CanAdmit(b, price, 1501)) throw new Exception("refusal"); });
        }
        var ledger = new EconomicLedger();
        Measure("admission and debit (reset outside conceptual operation)", () => {
            budget.Balance = budget.OpeningBalance; budget.Ledger = default;
            if (!OrdinaryExchanges.TryPay(budget, ledger, OrdinaryVisitClass.Retail, price, 1501)) throw new Exception("refusal"); });
        var row = new OutsideSpendingDay();
        BigInteger naiveUnits = 0, naiveVisits = 0;
        Measure("naive wide counters", () => { naiveUnits += price.Units; naiveVisits += 1; });
        Measure("delta wide counters", () => row.TryRecord(OrdinaryVisitClass.Retail, price));
        foreach (int count in new[] { 1, 1024, 20000 })
        {
            var places = Enumerable.Range(0, count).Select(i => new Place { Id = "p" + i }).ToArray();
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); long before = GC.GetTotalMemory(true);
            var sources = new Dictionary<string, Place>(count, StringComparer.Ordinal);
            foreach (var p in places) sources.Add(p.Id, p);
            var ambiguous = new HashSet<string>(StringComparer.Ordinal);
            var index = new OrdinaryOwnershipIndex(places, 1500, p => sources.TryGetValue(p.Id, out _) && !ambiguous.Contains(p.Id)
                ? new OrdinaryVisitTerms(0, false, OrdinaryVisitClass.Retail) : default);
            long retained = GC.GetTotalMemory(true) - before;
            index.ObserveBoundary(1560); while (index.Pending) index.Continue();
            string id = places[^1].Id;
            Measure($"owner lookup {count}", () => { if (!index.TryGet(id, out _)) throw new Exception("missing"); });
            int rounds = count == 20000 ? 30 : 1000;
            var times = new double[rounds * ((count + 15) / 16)]; int sample = 0; long allocated = 0, reads = index.NativeReads;
            for (int round = 0; round < rounds; round++)
            {
                index.ObserveBoundary(1620 + round * 60);
                while (index.Pending)
                {
                    long bytes = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp(); index.Continue();
                    times[sample++] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                    allocated += GC.GetAllocatedBytesForCurrentThread() - bytes;
                }
            }
            Report($"ownership refresh {count}", times, allocated, new { retainedBytes = retained, observations = index.NativeReads - reads, rounds, maximumPlacesPerUnit = 16 });
            Assert.Equal(0, allocated); Assert.Equal((long)rounds * count, index.NativeReads - reads);
            GC.KeepAlive(index);
        }
        var city = OrdinaryExchangeTests.Arrival(new OrdinaryExchangeTests.Ownership()); OrdinaryExchangeTests.Drain(city, 1501);
        Measure("requested outside total line", () => { _ = city.FreeOutsideSpending().Text(); }, 5000);
    }
}
