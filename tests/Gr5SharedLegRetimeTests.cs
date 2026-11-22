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

    private static Route Path(bool shared)
    {
        var path = new Route();
        for (int k = 0; k < PathLegs; k++)
            path.Legs.Add(new RouteLeg { From = new Position(k, 0, 0), To = new Position(k + 1, 0, 0), Minutes = 0.5 + 0.01 * k, Mode = TravelMode.Walk });
        return shared ? TransportComposition.Shared(path) : TransportComposition.Frozen(path);
    }

    private static void Depart(CitizenState c, Route path, double departure)
    {
        c.PlaceId = c.HomeId = "home"; c.DayPlaceId = "work";
        c.Phase = CitizenPhase.Travelling; c.DestinationId = "work"; c.DestinationActivity = Activity.Work;
        c.ActivityUntilMinute = 5000; c.Preferences.OwnsCar = false; c.Car = null;
        var tail = new Route();
        int i = int.Parse(c.Identity.Id.Substring(6), System.Globalization.CultureInfo.InvariantCulture);
        tail.Legs.Add(new RouteLeg { From = new Position(PathLegs, 0, 0), To = new Position(PathLegs + 1, 0, 0), Minutes = 0.7, Mode = TravelMode.Walk });
        tail.Legs.Add(new RouteLeg { From = new Position(PathLegs + 1, 0, 0), To = new Position(PathLegs + 2, 0, 0), Minutes = 0.3 + 0.001 * (i % 7), Mode = TravelMode.Walk });
        c.Journey = TransportComposition.Join(path, tail)!.Commit();
        c.DepartureMinute = departure;
        c.NextEventMinute = c.DepartureMinute + c.Journey.Minutes;
    }

    private static (CityState State, CitySimulator City) WalkingCity(bool shared, int late = 0, double workX = PathLegs + 2)
    {
        var state = Population.Create(Enumerable.Range(0, Walkers + late).Select(i => new CitizenIdentity
        { Id = "walker" + i.ToString("D4"), Name = "Walker " + i, Age = 30, Persona = Persona.Commuter }), new[]
        {
            new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = Walkers + late, Position = new Position(0, 0, 0) },
            new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = Walkers + late, Position = new Position(workX, 0, 0) }
        }, "shared-leg-retime", "0.40.4", 5, 1440);
        state.TravelTimingVersion = 1; state.TravelClockRate = 1;
        var path = Path(shared);
        for (int i = 0; i < Walkers; i++) Depart(state.Citizens[i], path, 1440 - 0.37 - 0.0213 * (i % 500));
        for (int i = Walkers; i < Walkers + late; i++)
        {
            var c = state.Citizens[i];
            c.PlaceId = c.HomeId = "home"; c.DayPlaceId = "work"; c.Phase = CitizenPhase.AtPlace; c.Journey = null;
            c.ActivityUntilMinute = 5000; c.NextEventMinute = 5000; c.Preferences.OwnsCar = false; c.Car = null;
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

    private const int LateWalkersPerCycle = 2;

    private static int FutureCopies(CityState state)
    {
        var copies = new Dictionary<(Position, Position, double), HashSet<RouteLeg>>();
        foreach (var c in state.Citizens)
        {
            if (c.Phase != CitizenPhase.Travelling || c.Journey == null) continue;
            double start = 0, elapsed = state.Minute - c.DepartureMinute;
            for (int k = 0; k < c.Journey.Legs.Count; k++)
            {
                var leg = c.Journey.Legs[k];
                if (start > elapsed && leg.To.X <= PathLegs)
                {
                    var key = (leg.From, leg.To, leg.Minutes);
                    if (!copies.TryGetValue(key, out var set)) copies.Add(key, set = new HashSet<RouteLeg>(ReferenceEqualityComparer.Instance));
                    set.Add(leg);
                }
                start += leg.Minutes;
            }
        }
        return copies.Count == 0 ? 0 : copies.Values.Max(v => v.Count);
    }

    private (List<long> Unique, List<int> Copies, List<long> Drained, List<int> Travelling) RunWithLateDepartures(int cycles, bool retime)
    {
        var (state, city) = WalkingCity(shared: true, late: LateWalkersPerCycle * cycles);
        var cached = state.Citizens[0].Journey!.Legs.Take(PathLegs).ToList();
        var unique = new List<long> { RetainedCensus.JourneyLegs(state).Legs };
        var copies = new List<int> { FutureCopies(state) };
        var travelling = new List<int> { Travelling(state) };
        for (int cycle = 0; cycle < cycles; cycle++)
        {
            var path = new Route();
            if (retime) path = Path(shared: true); else path.Legs.AddRange(cached);
            for (int k = 0; k < LateWalkersPerCycle; k++) Depart(state.Citizens[Walkers + cycle * LateWalkersPerCycle + k], path, state.Minute);
            city = new CitySimulator(state, new NoRoutes());
            if (retime)
            {
                city = Retime(state, city, 2, out _);
                city.AdvanceTo(state.Minute + 0.02, int.MaxValue);
                city = Retime(state, city, 1, out _);
            }
            else city.AdvanceTo(state.Minute + 0.02, int.MaxValue);
            city.AdvanceTo(state.Minute + 0.01, int.MaxValue);
            unique.Add(RetainedCensus.JourneyLegs(state).Legs);
            copies.Add(FutureCopies(state));
            travelling.Add(Travelling(state));
        }
        Assert.True(Travelling(state) > Walkers / 2, Travelling(state).ToString());
        var drained = new List<long>();
        for (int step = 0; step < 3; step++)
        {
            city.AdvanceTo(state.Minute + 15, int.MaxValue);
            drained.Add(RetainedCensus.JourneyLegs(state).Legs);
        }
        Assert.Equal(0, Travelling(state));
        return (unique, copies, drained, travelling);
    }

    [Fact]
    public void LateDeparturesOnAReplannedPathShareOneScaledCopyAcrossRetimesAndDrain()
    {
        const int cycles = 5;
        var control = RunWithLateDepartures(cycles, retime: false);
        var retimed = RunWithLateDepartures(cycles, retime: true);
        var excess = control.Unique.Zip(retimed.Unique, (c, r) => r - c).ToList();
        output.WriteLine($"{Walkers} walkers plus {LateWalkersPerCycle} late walkers per cycle on a replanned {PathLegs}-leg path, unique journey RouteLegs at R0..R{cycles}: "
            + $"1x control {string.Join(" / ", control.Unique)}; retimed {string.Join(" / ", retimed.Unique)}; excess {string.Join(" / ", excess)}; "
            + $"most copies of one future path leg: control {string.Join(" / ", control.Copies)}, retimed {string.Join(" / ", retimed.Copies)}; "
            + $"drain control {string.Join(" / ", control.Drained)}, retimed {string.Join(" / ", retimed.Drained)}");
        Assert.All(control.Copies, c => Assert.Equal(1, c));
        Assert.All(retimed.Copies, c => Assert.Equal(1, c));
        Assert.Equal(control.Travelling, retimed.Travelling);
        var perJourney = excess.Select((e, r) => e - 2L * retimed.Travelling[r]).ToList();
        output.WriteLine("excess beyond one split prefix and one tail per journey in flight " + string.Join(" / ", perJourney));
        for (int r = 2; r <= cycles; r++) Assert.True(perJourney[r] <= perJourney.Take(r).Max(), "R" + r + " " + string.Join(" / ", perJourney));
        Assert.Equal(control.Drained[control.Drained.Count - 1], retimed.Drained[retimed.Drained.Count - 1]);
    }

    private const double Walk = 1.4;
    private static readonly double[] UpRates = { 1.9999993, 2.0000011, 1.9999987, 2.0000004, 1.9999998 };
    private static readonly double[] ReturnRates = { 1.0000003, 0.9999996, 1.0000008, 0.9999991, 1.0000002 };

    private static Route PlannedPath(double rate)
    {
        var path = new Route();
        double metresPerMinute = TravelTiming.WalkingMetresPerMinute(Walk, rate), x = 0;
        for (int k = 0; k < PathLegs; k++)
        {
            double length = 0.7 + 0.013 * k;
            path.Legs.Add(new RouteLeg { From = new Position(x, 0, 0), To = new Position(x + length, 0, 0), Minutes = length / metresPerMinute, Mode = TravelMode.Walk });
            x += length;
        }
        return TransportComposition.Shared(path);
    }

    private static void DepartPlanned(CitizenState c, Route path, double departure)
    {
        c.PlaceId = c.HomeId = "home"; c.DayPlaceId = "work";
        c.Phase = CitizenPhase.Travelling; c.DestinationId = "work"; c.DestinationActivity = Activity.Work;
        c.ActivityUntilMinute = 5000; c.Preferences.OwnsCar = false; c.Car = null;
        var journey = new Route(new RouteLegs(path.Legs.Count));
        journey.Legs.AddRange(path.Legs);
        c.Journey = journey.Commit();
        c.DepartureMinute = departure;
        c.NextEventMinute = c.DepartureMinute + c.Journey.Minutes;
    }

    private static int MostObjectsForOneSegment(CityState state)
    {
        var objects = new Dictionary<(Position, Position), HashSet<RouteLeg>>();
        foreach (var c in state.Citizens)
        {
            if (c.Phase != CitizenPhase.Travelling || c.Journey == null) continue;
            foreach (var leg in c.Journey.Legs)
            {
                if (leg.SplitPrefix) continue;
                var key = (leg.From, leg.To);
                if (!objects.TryGetValue(key, out var set)) objects.Add(key, set = new HashSet<RouteLeg>(ReferenceEqualityComparer.Instance));
                set.Add(leg);
            }
        }
        return objects.Count == 0 ? 0 : objects.Values.Max(v => v.Count);
    }

    private (List<long> Unique, List<int> Objects, long Drained) RunAtObservedRates(bool retime, int latePerCycle)
    {
        int cycles = UpRates.Length;
        var original = PlannedPath(1);
        var (state, city) = WalkingCity(shared: true, late: latePerCycle * cycles, workX: original.Legs[original.Legs.Count - 1].To.X);
        for (int i = 0; i < Walkers; i++) DepartPlanned(state.Citizens[i], original, 1440 - 0.37 - 0.0213 * (i % 500));
        city = new CitySimulator(state, new NoRoutes());
        var unique = new List<long> { RetainedCensus.JourneyLegs(state).Legs };
        var objects = new List<int> { MostObjectsForOneSegment(state) };
        double current = 1;
        for (int cycle = 0; cycle < cycles; cycle++)
        {
            var path = retime ? PlannedPath(current) : original;
            for (int k = 0; k < latePerCycle; k++) DepartPlanned(state.Citizens[Walkers + cycle * latePerCycle + k], path, state.Minute);
            city = new CitySimulator(state, new NoRoutes());
            if (retime)
            {
                city = RetimeAt(state, city, UpRates[cycle]);
                city.AdvanceTo(state.Minute + 0.3, int.MaxValue);
                city = RetimeAt(state, city, ReturnRates[cycle]);
                current = ReturnRates[cycle];
            }
            else city.AdvanceTo(state.Minute + 0.3, int.MaxValue);
            city.AdvanceTo(state.Minute + 0.2, int.MaxValue);
            unique.Add(RetainedCensus.JourneyLegs(state).Legs);
            objects.Add(MostObjectsForOneSegment(state));
        }
        Assert.True(Travelling(state) > Walkers / 2, Travelling(state).ToString());
        city.AdvanceTo(state.Minute + 60, int.MaxValue);
        Assert.Equal(0, Travelling(state));
        return (unique, objects, RetainedCensus.JourneyLegs(state).Legs);
    }

    private static CitySimulator RetimeAt(CityState state, CitySimulator city, double rate)
    {
        var driver = new ClockRetimeDriver();
        while (true)
        {
            var outcome = driver.Update(state, rate, false, Walk, 0);
            Assert.NotEqual(ClockRetimeOutcome.Restarted, outcome);
            if (outcome == ClockRetimeOutcome.Committed) return city.RebuildRoutes(new NoRoutes(), driver.TakeRetimedTraffic());
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(LateWalkersPerCycle)]
    public void ObservedClockRatesKeepOneObjectPerSegmentAndRateAndTheRawLegGapPlateaus(int latePerCycle)
    {
        var control = RunAtObservedRates(retime: false, latePerCycle);
        var retimed = RunAtObservedRates(retime: true, latePerCycle);
        var excess = control.Unique.Zip(retimed.Unique, (c, r) => r - c).ToList();
        var perJourney = excess.Select((e, r) => e - 2L * latePerCycle * r).ToList();
        output.WriteLine($"{Walkers} walkers plus {latePerCycle} late walkers per cycle on a planned {PathLegs}-leg path, up rates {string.Join(" / ", UpRates)}, return rates {string.Join(" / ", ReturnRates)}; "
            + $"unique journey RouteLegs at R0..R{UpRates.Length}: control {string.Join(" / ", control.Unique)}; retimed {string.Join(" / ", retimed.Unique)}; excess {string.Join(" / ", excess)}; "
            + $"excess beyond one split prefix and one tail per late walker {string.Join(" / ", perJourney)}; "
            + $"most objects held for one path segment: control {string.Join(" / ", control.Objects)}, retimed {string.Join(" / ", retimed.Objects)}; "
            + $"after the drain control {control.Drained}, retimed {retimed.Drained}");
        Assert.All(control.Objects, c => Assert.Equal(1, c));
        Assert.All(retimed.Objects, c => Assert.True(c <= 2, "objects per segment " + string.Join(" / ", retimed.Objects)));
        for (int r = 2; r < excess.Count; r++) Assert.True(perJourney[r] <= perJourney.Take(r).Max(), "R" + r + " excess " + string.Join(" / ", perJourney));
        if (latePerCycle == 0) for (int r = 2; r < excess.Count; r++) Assert.True(excess[r] <= excess.Take(r).Max(), "R" + r + " raw excess " + string.Join(" / ", excess));
        Assert.Equal(control.Drained, retimed.Drained);
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
