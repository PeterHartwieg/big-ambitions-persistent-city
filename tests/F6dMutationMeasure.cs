using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using PersistentCity.Core;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

public sealed class F6dMutationMeasure
{
    private readonly ITestOutputHelper output;
    public F6dMutationMeasure(ITestOutputHelper output) { this.output = output; }
    [Fact]
    public void CheckedLedgerTransactionsInThirtyTwoOwnerUnits()
    {
        string? path = PerfGate.Variable("PC_F6D_OPERATIONS"); if (path == null) return;
        var budgets = Enumerable.Range(0, 100032).Select(_ => new ResidentBudget { OpeningBalance = Money.FromCents(1000), Balance = Money.FromCents(1000),
            DailyIncomeCents = 100, DailyHomeFoodCents = 1, Ledger = new OwnerAccounts { EmploymentIncome = Money.FromUnits(9), Discretionary = Money.FromUnits(9) } }).ToArray();
        void Unit(int start)
        {
            for (int j = start; j < start + 32; j++)
            {
                var b = budgets[j]; HouseholdFinance.PostDay(b, 0);
                if (!HouseholdFinance.HoldPurchase(b, Money.FromUnits(1))) throw new Exception("hold refused");
                HouseholdFinance.RefusePurchase(b);
                if (!HouseholdFinance.HoldPurchase(b, Money.FromUnits(1))) throw new Exception("hold refused");
                HouseholdFinance.ConfirmPurchase(b);
            }
        }
        Unit(0); // JIT warm-up, outside the samples
        var samples = new List<double>((budgets.Length - 32) / 32);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int start = 32; start < budgets.Length; start += 32)
        { long t = Stopwatch.GetTimestamp(); Unit(start); samples.Add(Stopwatch.GetElapsedTime(t).TotalMilliseconds); }
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        samples.Sort();
        string result = JsonConvert.SerializeObject(new { component = "32 owner posting/hold/refuse/hold/confirm", owners = budgets.Length - 32,
            count = samples.Count, p50Ms = samples[samples.Count / 2], p95Ms = samples[(int)Math.Ceiling(samples.Count * .95) - 1], maxMs = samples[^1],
            spikes30 = samples.Count(v => v >= 30), allocatedBytes = allocated });
        File.WriteAllText(path, result); output.WriteLine(result);
        Assert.All(budgets, b => Assert.True(HouseholdFinance.CashConserved(b)));
    }
}
