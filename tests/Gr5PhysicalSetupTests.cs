using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class Gr5PhysicalSetupTests
{
    private static CitizenIdentity Person(string id, int seed = 7) => new CitizenIdentity { Id = id, Name = "N " + id, Age = 31, AppearanceSeed = seed };

    [Fact]
    public void AnImmediateAcquireRedressesAnIdleBodyInsteadOfInstantiatingOne()
    {
        var pool = new DressedBodyPool<string>();
        pool.Return("kept-a", new PersonDressing(Person("a"), 60), 0, 100);
        pool.Return("kept-b", new PersonDressing(Person("b"), 60), 0, 300);
        Assert.True(pool.Count < pool.Spare);
        Assert.Equal(BodyChoice.Grow, pool.Choose(new PersonDressing(Person("c"), 60), 2, 16, null, 0, 0, out _));
        Assert.Equal(BodyChoice.Redress, pool.Choose(new PersonDressing(Person("c"), 60), 2, 16, null, 0, 0, true, out var body));
        Assert.Equal("kept-b", body);
        Assert.Equal(BodyChoice.Hit, pool.Choose(new PersonDressing(Person("a"), 60), 2, 16, null, 0, 0, true, out body));
        Assert.Equal("kept-a", body);
        Assert.Equal(BodyChoice.Grow, pool.Choose(new PersonDressing(Person("d"), 60), 2, 16, null, 0, 0, true, out body));
        Assert.Null(body);
    }

    [Fact]
    public void PhysicalSetupWaitsForALightUpdateButNeverMoreThanItsLimit()
    {
        var gate = new PhysicalSetupGate();
        Assert.True(gate.Admit(0.4));
        Assert.Equal(0, gate.LastWait);
        for (int i = 0; i < PhysicalSetupGate.MaximumWaitFrames; i++) Assert.False(gate.Admit(9));
        Assert.True(gate.Admit(9));
        Assert.Equal(PhysicalSetupGate.MaximumWaitFrames, gate.LastWait);
        Assert.False(gate.Admit(PhysicalSetupGate.StartMilliseconds + 0.01));
        Assert.True(gate.Admit(PhysicalSetupGate.StartMilliseconds));
        Assert.Equal(1, gate.LastWait);
        Assert.Equal(PhysicalSetupGate.MaximumWaitFrames + 1, gate.Deferred);
    }

    [Fact]
    public void SetupWithAnEmptyReserveOnSustainedBusyFramesRunsOnAnUpdateWithoutAdvancement()
    {
        const double Routing = 0.6, Advance = 9.5, SynchronousGrowth = 16.559, Setup = 3.0, Spike = 30;
        var gate = new PhysicalSetupGate();
        var reserve = new DressedBodyPool<string>();
        bool setUp = false; int frames = 0, advanced = 0; double setupFrame = 0;
        while (!setUp && frames < 100)
        {
            frames++;
            double elapsed = Routing;
            if (!gate.Reserve(true)) { elapsed += Advance; advanced++; }
            if (!gate.Admit(elapsed)) { Assert.True(elapsed <= Spike); continue; }
            var choice = reserve.Choose(new PersonDressing(Person("first"), 60), 0, 16, null, frames, frames / 60.0, true, out _);
            Assert.Equal(BodyChoice.Grow, choice);
            elapsed += Setup + SynchronousGrowth;
            setupFrame = elapsed; setUp = true;
        }
        Assert.True(setUp);
        Assert.Equal(PhysicalSetupGate.MaximumWaitFrames + 1, frames);
        Assert.Equal(PhysicalSetupGate.MaximumWaitFrames, advanced);
        Assert.Equal(1, gate.Reserved);
        Assert.True(setupFrame < Spike, "setup with synchronous growth on a reserved update: " + setupFrame + " ms");
        Assert.True(Routing + Advance + Setup + SynchronousGrowth > setupFrame + Advance - 0.001, "the reserved update carries no advancement");
        Assert.False(gate.Reserve(true));
        Assert.False(new PhysicalSetupGate(maximumWaitFrames: 0).Reserve(false));
        Assert.True(new PhysicalSetupGate(maximumWaitFrames: 0).Reserve(true));
    }
}
