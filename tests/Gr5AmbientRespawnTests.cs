using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class Gr5AmbientRespawnTests
{
    private sealed class Spawner
    {
        public int Active;
        public int LastSet;
        public bool Inside;
        public bool Stuck;
        public int NativeCalls;

        public void SetActive(int amount)
        {
            NativeCalls++;
            LastSet = amount;
            if (Inside) return;
            if (amount > Active && !Stuck) Active = amount;
            if (amount < Active) Active = amount;
        }
    }

    private static List<double> Restore(Spawner spawner, int desired, double spawnMilliseconds, double budgetMilliseconds, Action<int>? between = null)
    {
        var updates = new List<double>();
        int previous = -1;
        bool done = false;
        for (int update = 0; !done && update < 10000; update++)
        {
            between?.Invoke(update);
            double clock = 0;
            while (true)
            {
                var action = AmbientRespawnSteps.Next(spawner.Active, desired, spawner.LastSet, spawner.Inside, previous);
                if (action == AmbientRespawnAction.Done) { done = true; break; }
                if (action == AmbientRespawnAction.SetDesired)
                {
                    clock += Math.Max(0, desired - spawner.Active) * spawnMilliseconds;
                    spawner.SetActive(desired); done = true; break;
                }
                previous = spawner.Active;
                spawner.SetActive(spawner.Active + 1);
                spawner.LastSet = desired;
                clock += spawnMilliseconds;
                if (spawner.Active < desired && clock >= budgetMilliseconds) break;
            }
            updates.Add(clock);
        }
        Assert.True(done);
        return updates;
    }

    [Fact]
    public void RestoringRetiredWalkersSpreadsTheNativeSpawnsUnderTheSliceBudget()
    {
        var spawner = new Spawner { Active = 4, LastSet = 60 };
        var updates = Restore(spawner, 60, 39.1 / 56, ClockRetimeStages.SliceMilliseconds);
        Assert.Equal(60, spawner.Active);
        Assert.Equal(60, spawner.LastSet);
        Assert.True(updates.Count > 1);
        Assert.True(updates.Max() < ClockRetimeStages.SliceMilliseconds + 39.1 / 56, updates.Max().ToString());
        Assert.Equal(57, spawner.NativeCalls);
    }

    [Fact]
    public void ANativeAmountChangeOrABuildingVisitEndsTheStepsAndKeepsTheNativeAmount()
    {
        var foreign = new Spawner { Active = 0, LastSet = 40 };
        Restore(foreign, 40, 1, 4, update => { if (update == 2) foreign.LastSet = 25; });
        Assert.Equal(25, foreign.LastSet);
        Assert.True(foreign.Active < 40);

        var inside = new Spawner { Active = 0, LastSet = 40 };
        Restore(inside, 40, 1, 4, update => { if (update == 1) inside.Inside = true; });
        Assert.Equal(40, inside.LastSet);
        Assert.True(inside.Active < 40);
    }

    [Fact]
    public void ASpawnerThatDoesNotGrowFallsBackToOneNativeCall()
    {
        var stuck = new Spawner { Active = 3, LastSet = 30, Stuck = true };
        Restore(stuck, 30, 1, 4);
        Assert.Equal(2, stuck.NativeCalls);
        Assert.Equal(30, stuck.LastSet);
    }

    [Fact]
    public void AFullOrOverfullSpawnerGetsTheNativeCallOnce()
    {
        var over = new Spawner { Active = 50, LastSet = 30 };
        Restore(over, 30, 1, 4);
        Assert.Equal(1, over.NativeCalls);
        Assert.Equal(30, over.Active);
    }
}
