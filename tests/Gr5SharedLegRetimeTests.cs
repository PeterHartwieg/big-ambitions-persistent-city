using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

public sealed class Gr5SharedLegRetimeTests
{
    private readonly ITestOutputHelper output;
    public Gr5SharedLegRetimeTests(ITestOutputHelper output) { this.output = output; }

    private const int Walkers = 600, PathLegs = 24;

    private static (CityState State, CitySimulator City) WalkingCity(bool shared)
    {
        var state = Population.Create(Enumerable.Range(0, Walkers).Select(i => new CitizenIdentity
        { Id = "walker" + i.ToString("D4"), Name = "Walker " + i, Age = 30, Persona = Persona.Commuter }), new[]
        {
            new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = Walkers, Position = new Position(0, 0, 0) },
            new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = Walkers, Position = new Position(PathLegs + 2, 0, 0) }
        }, "shared-leg-retime", "0.40.4", 5, 1440);
        state.TravelTimingVersion = 1; state.TravelClockRate = 1;
        var path = new Route();
        for (int k = 0; k < PathLegs; k++)
            path.Legs.Add(new RouteLeg { From = new Position(k, 0, 0), To = new Position(k + 1, 0, 0), Minutes = 0.5 + 0.01 * k, Mode = TravelMode.Walk });
        path = shared ? TransportComposition.Shared(path) : TransportComposition.Frozen(path);
        for (int i = 0; i < Walkers; i++)
        {
            var c = state.Citizens[i];
            c.PlaceId = c.HomeId = "home"; c.DayPlaceId = "work";
            c.Phase = CitizenPhase.Travelling; c.DestinationId = "work"; c.DestinationActivity = Activity.Work;
            c.ActivityUntilMinute = 5000; c.Preferences.OwnsCar = false; c.Car = null;
            var tail = new Route();
            tail.Legs.Add(new RouteLeg { From = new Position(PathLegs, 0, 0), To = new Position(PathLegs + 1, 0, 0), Minutes = 0.7, Mode = TravelMode.Walk });
            tail.Legs.Add(new RouteLeg { From = new Position(PathLegs + 1, 0, 0), To = new Position(PathLegs + 2, 0, 0), Minutes = 0.3 + 0.001 * (i % 7), Mode = TravelMode.Walk });
            c.Journey = TransportComposition.Join(path, tail)!.Commit();
            c.DepartureMinute = 1440 - 0.37 - 0.0213 * (i % 500);
            c.NextEventMinute = c.DepartureMinute + c.Journey.Minutes;
        }
        return (state, new CitySimulator(state, new NoRoutes()));
    }

    private static CitySimulator Retime(CityState state, CitySimulator city, double rate, out long bytes)
    {
        var driver = new ClockRetimeDriver();
        long before = GC.GetAllocatedBytesForCurrentThread();
        while (true)
        {
            var outcome = driver.Update(state, rate, false, 1.4, 0);
            Assert.NotEqual(ClockRetimeOutcome.Restarted, outcome);
            if (outcome != ClockRetimeOutcome.Committed) continue;
            bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            return city.RebuildRoutes(new NoRoutes(), driver.TakeRetimedTraffic());
        }
    }

    private static int Travelling(CityState state) => state.Citizens.Count(c => c.Phase == CitizenPhase.Travelling);

    private (List<long> Unique, List<long> Splits, List<long> Bytes, string Hash) Run(bool shared, int cycles, bool retime)
    {
        var (state, city) = WalkingCity(shared);
        var unique = new List<long> { RetainedCensus.JourneyLegs(state).Legs };
        var splits = new List<long> { RetainedCensus.JourneyLegs(state).SplitPrefixes };
        var bytes = new List<long>();
        for (int cycle = 0; cycle < cycles; cycle++)
        {
            if (retime)
            {
                city = Retime(state, city, 2, out long up);
                city.AdvanceTo(state.Minute + 0.02, int.MaxValue);
                city = Retime(state, city, 1, out long down);
                bytes.Add(up); bytes.Add(down);
            }
            else city.AdvanceTo(state.Minute + 0.02, int.MaxValue);
            city.AdvanceTo(state.Minute + 0.01, int.MaxValue);
            var ledger = RetainedCensus.JourneyLegs(state);
            unique.Add(ledger.Legs); splits.Add(ledger.SplitPrefixes);
        }
        Assert.True(Travelling(state) > Walkers / 2, Travelling(state).ToString());
        city.AdvanceTo(state.Minute + 30, int.MaxValue);
        Assert.Equal(0, Travelling(state));
        state.PrepareForSave();
        return (unique, splits, bytes, RushSpikeReplay.Sha(SaveCodec.Write(state)));
    }

    [Fact]
    public void RetimedJourneysKeepSharingTheLegsTheirSourceJourneysSharedAndSaveTheSameCity()
    {
        const int cycles = 4;
        var control = Run(shared: true, cycles, retime: false);
        var sharedRun = Run(shared: true, cycles, retime: true);
        var separate = Run(shared: false, cycles, retime: true);
        output.WriteLine($"{Walkers} walkers on one shared {PathLegs}-leg path, unique journey RouteLegs at R0..R{cycles}: "
            + $"control {string.Join(" / ", control.Unique)}; retimed with shared copies {string.Join(" / ", sharedRun.Unique)} (split prefixes {string.Join(" / ", sharedRun.Splits)}); "
            + $"retimed with per-journey copies {string.Join(" / ", separate.Unique)}");
        output.WriteLine("bytes per retime, shared copies " + string.Join(", ", sharedRun.Bytes.Select(b => (b / 1e6).ToString("0.000")))
            + " MB; per-journey copies " + string.Join(", ", separate.Bytes.Select(b => (b / 1e6).ToString("0.000"))) + " MB (.NET)");
        Assert.Equal(separate.Hash, sharedRun.Hash);
        for (int r = 1; r <= cycles; r++)
        {
            long excess = sharedRun.Unique[r] - control.Unique[r];
            Assert.True(excess <= 2 * sharedRun.Splits[r] + PathLegs * (2 * r + 1), "R" + r + " excess " + excess + " splits " + sharedRun.Splits[r]);
            Assert.True(sharedRun.Splits[r] <= Walkers);
            Assert.True(separate.Unique[r] - control.Unique[r] > 10 * PathLegs, "R" + r);
        }
        for (int i = 0; i < sharedRun.Bytes.Count; i++) Assert.True(sharedRun.Bytes[i] < separate.Bytes[i], "retime " + i);
    }
}
