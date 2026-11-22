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
        for (int r = 1; r <= cycles; r++) Assert.True(perJourney[r] <= 0, "R" + r + " " + string.Join(" / ", perJourney));
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
        var objects = new Dictionary<(Position, Position, double), HashSet<RouteLeg>>();
        foreach (var c in state.Citizens)
        {
            if (c.Phase != CitizenPhase.Travelling || c.Journey == null) continue;
            foreach (var leg in c.Journey.Legs)
            {
                if (leg.SplitPrefix) continue;
                var key = (leg.From, leg.To, leg.Minutes);
                if (!objects.TryGetValue(key, out var set)) objects.Add(key, set = new HashSet<RouteLeg>(ReferenceEqualityComparer.Instance));
                set.Add(leg);
            }
        }
        return objects.Count == 0 ? 0 : objects.Values.Max(v => v.Count);
    }

    private (List<long> Unique, List<int> Objects, long Drained) RunAtObservedRates(bool retime, int latePerCycle, double headStart = 0)
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
            if (headStart > 0) city.AdvanceTo(state.Minute + headStart, int.MaxValue);
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
    [InlineData(0, 0)]
    [InlineData(LateWalkersPerCycle, 0)]
    [InlineData(LateWalkersPerCycle, 0.6)]
    public void ObservedClockRatesKeepOneObjectPerSegmentAndRateAndTheRawLegGapPlateaus(int latePerCycle, double headStart)
    {
        var control = RunAtObservedRates(retime: false, latePerCycle, headStart);
        var retimed = RunAtObservedRates(retime: true, latePerCycle, headStart);
        var excess = control.Unique.Zip(retimed.Unique, (c, r) => r - c).ToList();
        var perJourney = excess.Select((e, r) => e - 2L * latePerCycle * r).ToList();
        output.WriteLine($"{Walkers} walkers plus {latePerCycle} late walkers per cycle walking {headStart} minutes before the up retime on a planned {PathLegs}-leg path, up rates {string.Join(" / ", UpRates)}, return rates {string.Join(" / ", ReturnRates)}; "
            + $"unique journey RouteLegs at R0..R{UpRates.Length}: control {string.Join(" / ", control.Unique)}; retimed {string.Join(" / ", retimed.Unique)}; excess {string.Join(" / ", excess)}; "
            + $"excess beyond one split prefix and one tail per late walker {string.Join(" / ", perJourney)}; "
            + $"most objects held for one segment at one duration: control {string.Join(" / ", control.Objects)}, retimed {string.Join(" / ", retimed.Objects)}; "
            + $"after the drain control {control.Drained}, retimed {retimed.Drained}");
        Assert.All(control.Objects, c => Assert.Equal(1, c));
        Assert.All(retimed.Objects, c => Assert.True(c <= 2, "objects per segment and duration " + string.Join(" / ", retimed.Objects)));
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
            Assert.True(excess <= 2 * Walkers + PathLegs * (2 * r + 1), "R" + r + " excess " + excess + " splits " + sharedRun.Splits[r]);
            Assert.True(sharedRun.Splits[r] <= Walkers);
            Assert.True(separate.Unique[r] - control.Unique[r] > 10 * PathLegs, "R" + r);
        }
        for (int i = 0; i < sharedRun.Bytes.Count; i++) Assert.True(sharedRun.Bytes[i] < separate.Bytes[i], "retime " + i);
    }

    private static (CityState State, CitySimulator City) ThreeLegWalker(double walked, bool corner = false)
    {
        var path = new Route();
        double metresPerMinute = TravelTiming.WalkingMetresPerMinute(Walk, 1);
        var at = new Position(0, 0, 0);
        int turn = 0;
        foreach (double length in new[] { 0.7, 0.9, 1.1 })
        {
            var next = corner && turn++ == 1 ? new Position(at.X, 0, at.Z + length) : new Position(at.X + length, 0, at.Z);
            path.Legs.Add(new RouteLeg { From = at, To = next, Minutes = length / metresPerMinute, Mode = TravelMode.Walk });
            at = next;
        }
        var x = at;
        TransportComposition.Shared(path);
        var state = Population.Create(new[] { new CitizenIdentity { Id = "walker0000", Name = "Walker", Age = 30, Persona = Persona.Commuter } }, new[]
        {
            new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 1, Position = new Position(0, 0, 0) },
            new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = 1, Position = x }
        }, "shared-leg-reload", "0.40.4", 5, 1440);
        state.TravelTimingVersion = 1; state.TravelClockRate = 1;
        DepartPlanned(state.Citizens[0], path, 1440 - walked);
        return (state, new CitySimulator(state, new NoRoutes()));
    }

    private static string Saved(CityState state) { state.PrepareForSave(); return SaveCodec.Write(state); }

    [Fact]
    public void AReloadBetweenRetimesLeavesTheNextRetimeSavingTheSameCityWhileTheJourneyIsActive()
    {
        int compared = 0, withPrefix = 0, atCorner = 0;
        foreach (bool corner in new[] { false, true })
        foreach (double into in new[] { 0, 0.5 })
        foreach (double later in new[] { 0.25, 0.3, 0.5, 0.7 })
        for (int start = 0; start < 12; start++)
        {
            var (state, city) = ThreeLegWalker(0.04 * start, corner);
            city = RetimeAt(state, city, 2);
            var walker = state.Citizens[0];
            var legs = walker.Journey!.Legs;
            if (legs.Count < 3 || !legs[0].SplitPrefix) continue;
            city.AdvanceTo(walker.DepartureMinute + legs[0].Minutes + legs[1].Minutes + into * legs[2].Minutes, int.MaxValue);
            city = RetimeAt(state, city, 1);
            if (walker.Phase != CitizenPhase.Travelling) continue;
            bool prefix = walker.Journey!.Legs.Any(l => l.SplitPrefix);
            string saved = Saved(state);
            var read = SaveCodec.Read(saved);
            Assert.True(read.State != null, read.Error);
            var reloaded = read.State!;
            Assert.Equal(saved, Saved(reloaded));
            var reloadedCity = new CitySimulator(reloaded, new NoRoutes());
            city.AdvanceTo(state.Minute + later, int.MaxValue);
            reloadedCity.AdvanceTo(reloaded.Minute + later, int.MaxValue);
            Assert.Equal(Saved(state), Saved(reloaded));
            if (walker.Phase != CitizenPhase.Travelling) continue;
            city = RetimeAt(state, city, 2);
            reloadedCity = RetimeAt(reloaded, reloadedCity, 2);
            Assert.Equal(CitizenPhase.Travelling, walker.Phase);
            Assert.Equal(walker.Journey!.Legs.Count, reloaded.Citizens[0].Journey!.Legs.Count);
            Assert.Equal(RushSpikeReplay.Sha(Saved(state)), RushSpikeReplay.Sha(Saved(reloaded)));
            compared++;
            if (prefix) withPrefix++;
            if (prefix && corner) atCorner++;
        }
        output.WriteLine($"{compared} active three-leg walks compared after a 1x-2x-1x cycle and a reload, saved again after the next retime to 2x; "
            + $"{withPrefix} saved with an active split prefix, {atCorner} of them on the path with a right-angle turn");
        Assert.True(compared >= 40, compared.ToString());
        Assert.True(atCorner >= 10, atCorner.ToString());
    }

    private static double Within(CitizenState c, double minute, double fraction)
    {
        double elapsed = minute - c.DepartureMinute, start = 0;
        foreach (var leg in c.Journey!.Legs)
        {
            if (start + leg.Minutes > elapsed + 1e-9) return c.DepartureMinute + start + fraction * leg.Minutes;
            start += leg.Minutes;
        }
        throw new InvalidOperationException("The journey has ended.");
    }

    private static CitySimulator RetimeJoining(CityState state, CitySimulator city, double rate, out int joined)
    {
        var driver = new ClockRetimeDriver();
        while (true)
        {
            var outcome = driver.Update(state, rate, false, Walk, 0);
            Assert.NotEqual(ClockRetimeOutcome.Restarted, outcome);
            if (outcome != ClockRetimeOutcome.Committed) continue;
            joined = driver.Joined;
            return city.RebuildRoutes(new NoRoutes(), driver.TakeRetimedTraffic());
        }
    }

    [Fact]
    public void ASplitRejoinedInEveryCycleKeepsTheRawLegGapToTheMatchedControlLevel()
    {
        const int cycles = 6;
        (CityState State, CitySimulator City) Start()
        {
            var (state, _) = WalkingCity(shared: true);
            var path = Path(shared: true);
            for (int i = 0; i < Walkers; i++) Depart(state.Citizens[i], path, state.Minute);
            return (state, new CitySimulator(state, new NoRoutes()));
        }
        var (state, city) = Start();
        var (control, controlCity) = Start();
        var lead = state.Citizens[0];
        var retimed = new List<long> { RetainedCensus.JourneyLegs(state).Legs };
        var matched = new List<long> { RetainedCensus.JourneyLegs(control).Legs };
        var joins = new List<int>();
        for (int cycle = 0; cycle < cycles; cycle++)
        {
            double half = Within(lead, state.Minute, 0.5);
            city.AdvanceTo(half, int.MaxValue);
            city = RetimeJoining(state, city, 2, out _);
            double boundary = Within(lead, state.Minute, 1);
            city.AdvanceTo(boundary, int.MaxValue);
            city = RetimeJoining(state, city, 1, out int joined);
            controlCity.AdvanceTo(boundary, int.MaxValue);
            Assert.Equal(Walkers, Travelling(state));
            Assert.Equal(Walkers, Travelling(control));
            joins.Add(joined);
            retimed.Add(RetainedCensus.JourneyLegs(state).Legs);
            matched.Add(RetainedCensus.JourneyLegs(control).Legs);
        }
        var gap = retimed.Zip(matched, (r, c) => r - c).ToList();
        output.WriteLine($"{Walkers} walkers departing together on one shared {PathLegs}-leg path, 2x halfway through each segment and 1x at its end in each of {cycles} cycles; "
            + $"rejoined journeys per cycle {string.Join(" / ", joins)}; unique journey RouteLegs at R0..R{cycles}: retimed {string.Join(" / ", retimed)}, "
            + $"1x control at the same minutes {string.Join(" / ", matched)}; gap {string.Join(" / ", gap)}");
        Assert.All(joins, j => Assert.True(j > 0, "rejoins per cycle " + string.Join(" / ", joins)));
        for (int r = 2; r < gap.Count; r++) Assert.True(gap[r] <= gap.Take(r).Max(), "R" + r + " gap " + string.Join(" / ", gap));
    }

    [Fact]
    public void SameMinuteClockReversalsOnOneSharedLegKeepTheLegGapToTheMatchedControlLevel()
    {
        const int cycles = 6;
        double metresPerMinute = TravelTiming.WalkingMetresPerMinute(Walk, 1), length = 20 * metresPerMinute;
        (CityState State, CitySimulator City) Start()
        {
            var path = TransportComposition.Shared(new Route { Legs = { new RouteLeg { From = new Position(0, 0, 0), To = new Position(length, 0, 0), Minutes = length / metresPerMinute, Mode = TravelMode.Walk } } });
            var state = Population.Create(Enumerable.Range(0, Walkers).Select(i => new CitizenIdentity
            { Id = "walker" + i.ToString("D4"), Name = "Walker " + i, Age = 30, Persona = Persona.Commuter }), new[]
            {
                new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = Walkers, Position = new Position(0, 0, 0) },
                new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = Walkers, Position = new Position(length, 0, 0) }
            }, "same-minute-reversal", "0.40.4", 5, 1440);
            state.TravelTimingVersion = 1; state.TravelClockRate = 1;
            for (int i = 0; i < Walkers; i++) DepartPlanned(state.Citizens[i], path, state.Minute - 1 - 0.0137 * i);
            return (state, new CitySimulator(state, new NoRoutes()));
        }
        var (state, city) = Start();
        var (control, controlCity) = Start();
        var retimed = new List<long> { RetainedCensus.JourneyLegs(state).Legs };
        var matched = new List<long> { RetainedCensus.JourneyLegs(control).Legs };
        for (int cycle = 0; cycle < cycles; cycle++)
        {
            city = RetimeJoining(state, city, 2, out _);
            city = RetimeJoining(state, city, 1, out _);
            retimed.Add(RetainedCensus.JourneyLegs(state).Legs);
            matched.Add(RetainedCensus.JourneyLegs(control).Legs);
            double next = state.Minute + 1;
            city.AdvanceTo(next, int.MaxValue);
            controlCity.AdvanceTo(next, int.MaxValue);
            Assert.Equal(Walkers, Travelling(state));
            Assert.Equal(Walkers, Travelling(control));
        }
        var gap = retimed.Zip(matched, (r, c) => r - c).ToList();
        output.WriteLine($"{Walkers} staggered walkers on one shared straight leg, 2x then 1x at the same city minute and one minute advanced in each of {cycles} cycles; "
            + $"unique journey RouteLegs at R0..R{cycles}: retimed {string.Join(" / ", retimed)}, 1x control {string.Join(" / ", matched)}");
        for (int r = 2; r < gap.Count; r++) Assert.True(gap[r] <= gap[1], "R" + r + " gap " + string.Join(" / ", gap));
        Assert.True(gap[1] <= 2 * Walkers, "gap " + string.Join(" / ", gap));
    }

    private const int CapResidents = 1000, LongLegs = 267;

    private static double PrepareRetime(CityState state, ref CitySimulator city, double rate, out long examined)
    {
        var driver = new ClockRetimeDriver();
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        while (true)
        {
            var outcome = driver.Update(state, rate, false, Walk, 1000);
            Assert.NotEqual(ClockRetimeOutcome.Restarted, outcome);
            if (outcome != ClockRetimeOutcome.Committed) continue;
            double milliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            examined = driver.SourceLegsExamined;
            city = city.RebuildRoutes(new NoRoutes(), driver.TakeRetimedTraffic());
            return milliseconds;
        }
    }

    [Fact]
    public void RetimingLongActiveWalksAtTheResidentCapMatchesCompletedLegsInOnePass()
    {
        var path = new Route();
        double metresPerMinute = TravelTiming.WalkingMetresPerMinute(Walk, 1), x = 0;
        var starts = new double[LongLegs + 1];
        for (int k = 0; k < LongLegs; k++)
        {
            double length = 0.7 + 0.013 * (k % 24);
            path.Legs.Add(new RouteLeg { From = new Position(x, 0, 0), To = new Position(x + length, 0, 0), Minutes = length / metresPerMinute, Mode = TravelMode.Walk });
            starts[k + 1] = starts[k] + path.Legs[k].Minutes;
            x += length;
        }
        TransportComposition.Shared(path);
        var state = Population.Create(Enumerable.Range(0, CapResidents).Select(i => new CitizenIdentity
        { Id = "walker" + i.ToString("D4"), Name = "Walker " + i, Age = 30, Persona = Persona.Commuter }), new[]
        {
            new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = CapResidents, Position = new Position(0, 0, 0) },
            new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = CapResidents, Position = new Position(x, 0, 0) }
        }, "long-walk-retime", "0.40.4", 5, 1440);
        state.TravelTimingVersion = 1; state.TravelClockRate = 1;
        for (int i = 0; i < CapResidents; i++)
        {
            int leg = 200 + i % 50;
            DepartPlanned(state.Citizens[i], path, state.Minute - starts[leg] - 0.37 * path.Legs[leg].Minutes);
        }
        var city = new CitySimulator(state, new NoRoutes());
        var times = new List<double>();
        var perSourceLeg = new List<double>();
        foreach (double rate in new[] { 2, 1, 2, 1, 2 })
        {
            long sourceLegs = state.Citizens.Sum(c => (long)c.Journey!.Legs.Count);
            times.Add(PrepareRetime(state, ref city, rate, out long examined));
            perSourceLeg.Add((double)examined / sourceLegs);
            city.AdvanceTo(state.Minute + 0.3, int.MaxValue);
        }
        Assert.Equal(CapResidents, Travelling(state));
        int longest = state.Citizens.Max(c => c.Journey!.Legs.Count);
        output.WriteLine($"{CapResidents} walkers on one shared {LongLegs}-leg walk with 200 to 249 legs completed, retime preparation ms at 2x/1x/2x/1x/2x: "
            + string.Join(" / ", times.Select(t => t.ToString("0.0")))
            + "; source-leg examinations per source leg " + string.Join(" / ", perSourceLeg.Select(t => t.ToString("0.00"))) + $"; longest journey after the last retime {longest} legs (.NET)");
        Assert.True(longest <= LongLegs + 1, longest.ToString());
        Assert.All(perSourceLeg, p => Assert.True(p > 0 && p <= 4, string.Join(" / ", perSourceLeg)));
    }
}
