using System;
using System.Linq;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class Gr5ClosureValidationTests
{
    [Fact]
    public void NoClosureBatchValidatesMoreThanItsBudgetAndEveryRelevantNodeIsRevalidatedWithinAFewBatches()
    {
        const int nodes = 40000, relevant = 5000;
        var rota = new ClosureValidationRota();
        var last = new int[nodes];
        int worstGap = 0;
        for (int batch = 1; batch <= 400; batch++)
        {
            rota.Begin(nodes);
            int full = 0;
            for (int index = 0; index < relevant; index++)
                if (rota.TakeFull(index * 7)) { full++; if (last[index] != 0) worstGap = Math.Max(worstGap, batch - last[index]); last[index] = batch; }
            Assert.InRange(full, 0, ClosureValidationRota.DefaultPerBatch);
            if (batch > 40) Assert.All(Enumerable.Range(0, relevant), index => Assert.True(batch - last[index] <= 2 * (relevant / ClosureValidationRota.DefaultPerBatch + 2), "node " + index));
        }
        Assert.InRange(worstGap, relevant / ClosureValidationRota.DefaultPerBatch, 2 * (relevant / ClosureValidationRota.DefaultPerBatch + 2));
        Assert.True(rota.Deferred > 0);
    }

    [Fact]
    public void ASmallRelevantSetIsFullyValidatedOnItsFirstBatchAndThenEveryEighth()
    {
        var rota = new ClosureValidationRota();
        rota.Begin(1000);
        Assert.All(Enumerable.Range(0, 200), index => Assert.True(rota.TakeFull(index)));
        for (int batch = 2; batch <= 8; batch++)
        {
            rota.Begin(1000);
            Assert.All(Enumerable.Range(0, 200), index => Assert.False(rota.TakeFull(index)));
        }
        rota.Begin(1000);
        Assert.All(Enumerable.Range(0, 200), index => Assert.True(rota.TakeFull(index)));
        Assert.Equal(0, rota.Deferred);
        Assert.True(rota.TakeFull(-1));
        Assert.True(rota.TakeFull(5000));
    }

    [Fact]
    public void ANewNetworkSizeRevalidatesEveryNodeAndBatchesAllocateNothing()
    {
        var rota = new ClosureValidationRota(4, 3);
        rota.Begin(10);
        for (int i = 0; i < 4; i++) Assert.True(rota.TakeFull(i));
        Assert.False(rota.TakeFull(4));
        rota.Begin(12);
        Assert.True(rota.TakeFull(0));
        rota.Begin(12);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int batch = 0; batch < 1000; batch++) { rota.Begin(12); for (int i = 0; i < 12; i++) rota.TakeFull(i); }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClosureValidationRota(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClosureValidationRota(1, 0));
    }

    [Fact]
    public void RoadClosureChecksReadFlagsEveryBatchAndValidateTopologyOnTheRota()
    {
        string source = System.IO.File.ReadAllText(System.IO.Path.Combine(CapabilityReportTests.Root(), "Runtime", "RoadRoutes.cs"));
        Assert.Contains("closureDisabled[index] = closureValidation.TakeFull(index) ? Disabled(index) : ClosedByFlags(index);", source);
        Assert.Contains("return ClosedByFlags(index);", source);
        Assert.Contains("closureBatch ? unsafeTopology || !closureBatchCurrent : !IsCurrent", source);
    }
}
