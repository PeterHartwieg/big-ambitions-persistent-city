using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class Gr5CurbSearchTests
{
    private sealed class Source : ICurbSource
    {
        private readonly Dictionary<(int, int), int[]> cells;
        private readonly Func<int, bool> accepts;
        private readonly long cellTicks, attemptTicks;
        public readonly List<int> Result = new List<int>();
        public readonly List<string> Calls = new List<string>();
        public long Clock;
        public int Limit = 2;
        public bool Log;
        public Source(Dictionary<(int, int), int[]> cells, Func<int, bool> accepts, long cellTicks, long attemptTicks)
        { this.cells = cells; this.accepts = accepts; this.cellTicks = cellTicks; this.attemptTicks = attemptTicks; }
        public long Now() => Clock;
        public void CollectCell(int x, int z, List<int> into)
        {
            Clock += cellTicks;
            if (Log) Calls.Add("cell " + x + "," + z);
            if (cells.TryGetValue((x, z), out var found)) foreach (int c in found) into.Add(c);
        }
        public void Order(List<int> nearby) => nearby.Sort();
        public bool Full => Result.Count >= Limit;
        public void Attempt(int candidate)
        {
            Clock += attemptTicks;
            if (Log) Calls.Add("try " + candidate);
            if (accepts(candidate)) Result.Add(candidate);
        }
    }

    private static Dictionary<(int, int), int[]> Cells(Random random, int reach)
    {
        var cells = new Dictionary<(int, int), int[]>();
        int next = 0;
        for (int x = -reach; x <= reach; x++)
            for (int z = -reach; z <= reach; z++)
                if (random.Next(3) != 0) cells[(x, z)] = Enumerable.Range(0, random.Next(0, 6)).Select(_ => next++ * 7 % 97).ToArray();
        return cells;
    }

    [Fact]
    public void ASlicedCurbSearchTriesTheSameCandidatesAndFindsTheSameCurbsAsOneCall()
    {
        var random = new Random(4417);
        int sliced = 0;
        for (int sample = 0; sample < 200; sample++)
        {
            var cells = Cells(random, 2);
            int seed = sample;
            bool Accepts(int candidate) => (candidate + seed) % 5 == 0;
            var whole = new Source(cells, Accepts, 1, 1) { Log = true, Limit = 1 + sample % 2 };
            var search = new CurbSearch();
            search.Begin(0, 0, 2);
            Assert.True(search.Continue(whole, long.MaxValue));
            var parts = new Source(cells, Accepts, 1, 1) { Log = true, Limit = whole.Limit };
            search.Begin(0, 0, 2);
            int calls = 0;
            while (!search.Continue(parts, parts.Clock + 1 + random.Next(3))) calls++;
            if (calls > 0) sliced++;
            Assert.Equal(whole.Calls, parts.Calls);
            Assert.Equal(whole.Result, parts.Result);
            Assert.True(search.Attempts <= CurbSearch.MaximumAttempts);
        }
        Assert.True(sliced > 150);
    }

    [Fact]
    public void EachCallStopsWithinOneCellOrOneSampleOfItsDeadline()
    {
        var cells = Cells(new Random(91), 2);
        foreach (var (cell, attempt, budget) in new[] { (5L, 190L, 200L), (50L, 50L, 100L), (1L, 400L, 20L) })
        {
            var source = new Source(cells, _ => false, cell, attempt);
            var search = new CurbSearch();
            search.Begin(0, 0, 2);
            long worst = 0;
            while (true)
            {
                long started = source.Clock;
                bool done = search.Continue(source, started + budget);
                worst = Math.Max(worst, source.Clock - started);
                if (done) break;
            }
            Assert.True(worst <= budget + Math.Max(cell, attempt), "worst call " + worst + " ticks against a " + budget + "-tick budget");
            Assert.Equal(CurbSearch.MaximumAttempts, search.Attempts);
        }
    }

    [Fact]
    public void TheRigCarEndsUnitFitsTheSliceBudgetOnceSliced()
    {
        var cells = new Dictionary<(int, int), int[]>();
        for (int x = -2; x <= 2; x++) for (int z = -2; z <= 2; z++) cells[(x, z)] = Enumerable.Range(0, 4).Select(k => (x + 2) * 20 + (z + 2) * 4 + k).ToArray();
        const long PerMillisecond = 1000;
        var oneShot = new Source(cells, _ => false, 50, 1855);
        var search = new CurbSearch();
        search.Begin(0, 0, 2);
        search.Continue(oneShot, long.MaxValue);
        double oneShotMs = oneShot.Clock / (double)PerMillisecond;
        var sliced = new Source(cells, _ => false, 50, 1855);
        search.Begin(0, 0, 2);
        double worstMs = 0; int units = 0;
        while (true)
        {
            long started = sliced.Clock;
            bool done = search.Continue(sliced, started + 2 * PerMillisecond);
            worstMs = Math.Max(worstMs, (sliced.Clock - started) / (double)PerMillisecond);
            units++;
            if (done) break;
        }
        Assert.Equal(23.51, oneShotMs, 2);
        Assert.True(worstMs <= 3.71, "worst sliced unit " + worstMs + " ms");
        Assert.True(units >= 7);
    }

    [Fact]
    public void ContinuingACurbSearchAllocatesNothingAfterWarmUp()
    {
        var cells = Cells(new Random(5), 2);
        var source = new Source(cells, c => c % 11 == 0, 1, 1) { Limit = 2 };
        var search = new CurbSearch();
        for (int i = 0; i < 4; i++) { source.Result.Clear(); search.Begin(0, 0, 2); while (!search.Continue(source, source.Clock + 2)) { } }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 500; i++) { source.Result.Clear(); search.Begin(0, 0, 2); while (!search.Continue(source, source.Clock + 2)) { } }
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    [Fact]
    public void ACancelledSearchIsDoneAndAFreshBeginStartsOver()
    {
        var cells = Cells(new Random(17), 1);
        var source = new Source(cells, _ => true, 1, 1) { Limit = 2 };
        var search = new CurbSearch();
        search.Begin(0, 0, 1);
        Assert.False(search.Continue(source, source.Clock));
        search.Cancel();
        Assert.True(search.Done);
        Assert.True(search.Continue(source, source.Clock));
        source.Result.Clear();
        search.Begin(0, 0, 1);
        Assert.True(search.Continue(source, long.MaxValue));
        Assert.Equal(Math.Min(2, cells.Values.Sum(v => v.Length)), source.Result.Count);
    }
}
