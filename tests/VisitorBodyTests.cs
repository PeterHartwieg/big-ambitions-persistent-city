using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.MovementEquivalence;
using PersistentCity.Persistence;
using PersistentCity.Runtime;
using Xunit;

namespace PersistentCity.Tests;

public sealed class VisitorBodyTests
{
    private const int Residents = 300;
    private static MovementOptions Visitors(Action<MovementOptions>? change = null)
    {
        var options = SelfChecks.Small("f5i", Residents);
        options.Visitors = true;
        change?.Invoke(options);
        return options;
    }
    private static MovementOptions Plain(Action<MovementOptions>? change = null) => Visitors(o => { o.Driver = false; change?.Invoke(o); });

    private static readonly Lazy<MovementResult> PlainRun = new(() => MovementRun.Run(Plain()));
    private static readonly Lazy<MovementResult> BodiesRun = new(() => MovementRun.Run(Visitors()));

    private static void Equal(MovementResult arm, MovementResult reference, string name)
    {
        var comparison = Equivalence.Compare(arm, reference);
        Assert.True(comparison.Identical, name + ": " + string.Join(" ", comparison.Equal.Where(e => !e.Value).Select(e => e.Key)));
        Assert.Equal(reference.Boundaries.Select(b => b.VisitorsSha256), arm.Boundaries.Select(b => b.VisitorsSha256));
        foreach (var key in new[] { "visitors.arrivals", "visitors.purchases", "visitors.ownSales", "visitors.walletUnits", "visitors.foodSpentUnits" })
            Assert.True(arm.Metrics[key] == reference.Metrics[key], name + ": " + key);
    }

    [Fact]
    public void VisitorsAreBodyCandidatesThroughTheActiveJourneyIndex()
    {
        Assert.True(ActiveJourneyIndex.VisitorsReceiveBodies);
        long frames = 0, travellingVisitors = 0, boundVisitors = 0;
        CitySimulator? first = null, last = null;
        var run = MovementRun.Run(Visitors(o =>
        {
            o.ReloadMidJourney = true;
            o.AfterFrame = (city, driver) =>
            {
                first ??= city; last = city;
                if (frames++ % 600 != 0) return;
                var index = city.ActiveJourneys;
                Assert.True(city.ActiveJourneyIndexMatchesState());
                Assert.Equal(0, index.WithheldVisitors);
                Assert.Equal(index.Count, index.BodyCandidates.Count);
                foreach (var routine in city.State.VisitorRoutines)
                {
                    Assert.True(ActiveJourneyIndex.ReceivesBody(routine));
                    if (routine.Phase != CitizenPhase.Travelling) continue;
                    travellingVisitors++;
                    Assert.Contains(routine, index.BodyCandidates);
                    Assert.True(index.Grid.Contains(routine));
                }
                boundVisitors += driver?.VisitorBodies ?? 0;
            };
        }));
        Assert.Equal(1, run.Reloads);
        Assert.NotSame(first, last);
        Assert.True(travellingVisitors > 0 && boundVisitors > 0);
        Assert.All(last!.State.VisitorRoutines, r => Assert.NotEqual(0, r.Identity.AppearanceSeed));
    }

    [Fact]
    public void VisitorBodyArmsEqualBodiesOff()
    {
        var plain = PlainRun.Value;
        Assert.Equal(Residents, plain.Metrics["visitors.count"]);
        Assert.True(plain.Metrics["visitors.arrivals"] > 0 && plain.Metrics["visitors.purchases"] > 0 && plain.Metrics["visitors.foodSpentUnits"] > 0,
            "the visitor fixture must travel, buy and pay");
        Assert.Equal(plain.Metrics["visitors.purchases"], plain.Metrics["visitors.ownSales"]);
        var stationary = BodiesRun.Value;
        Assert.True(stationary.Metrics["bodies.visitorAcquired"] > 0 && stationary.Metrics["bodies.visitorFrames"] > 0, "visitors received bodies");
        Equal(stationary, plain, "stationary");
        Equal(MovementRun.Run(Visitors(o => o.Focus = FocusPath.Sweep)), plain, "sweep");
        Equal(MovementRun.Run(Visitors(o => o.Focus = FocusPath.AwayReturn)), plain, "away/return");
        Equal(MovementRun.Run(Visitors(o => { o.Bodies.Pedestrians = 1; o.Bodies.Vehicles = 1; })), plain, "caps 1/1");
        Equal(MovementRun.Run(Visitors(o => { o.Bodies.Pedestrians = 256; o.Bodies.Vehicles = 64; })), plain, "caps 256/64");
        Equal(MovementRun.Run(Visitors(o => o.Bodies.Lag = LagModel.Ideal)), plain, "ideal lag");
        var failures = MovementRun.Run(Visitors(o => o.Bodies.AcquisitionFailurePercent = 50));
        Assert.True(failures.Metrics["bodies.failedAcquisitions"] > 0);
        Equal(failures, plain, "50 % acquisition failures");
        Equal(MovementRun.Run(Visitors(o => o.HiddenTravel = true)), MovementRun.Run(Plain(o => o.HiddenTravel = true)), "hidden travel");
        Equal(MovementRun.Run(Visitors(o => o.FramesPerSecond = 30)), MovementRun.Run(Plain(o => o.FramesPerSecond = 30)), "30 fps");
    }

    [Fact]
    public void CapsAreUnchangedWithVisitorsEligible()
    {
        var settings = new PrototypeSettings();
        Assert.Equal((96, 24, 120.0), (settings.MaximumPedestrians, settings.MaximumVehicles, settings.PhysicalRadiusMetres));
        var driver = new DriverSettings();
        Assert.Equal((48, 24, 120.0), (driver.Pedestrians, driver.Vehicles, driver.RadiusMetres));
        var stationary = BodiesRun.Value.Metrics;
        Assert.True(stationary["bodies.maxWalkers"] <= 48 && stationary["bodies.maxCars"] <= 24);
        Assert.True(stationary["bodies.maxVisitorBodies"] > 0 && stationary["bodies.maxVisitorBodies"] <= stationary["bodies.maxWalkers"]);
        var one = MovementRun.Run(Visitors(o => { o.Bodies.Pedestrians = 1; o.Bodies.Vehicles = 1; })).Metrics;
        Assert.True(one["bodies.maxWalkers"] <= 1 && one["bodies.maxCars"] <= 1 && one["bodies.maxVisitorBodies"] <= 1);
        var dense = MovementRun.Run(Visitors(o => { o.Residents = MovementFixture.Residents; o.RepresentationFactor = 3; })).Metrics;
        Assert.Equal(2 * MovementFixture.Residents, dense["visitors.count"]);
        Assert.True(dense["bodies.maxWalkers"] <= 48 && dense["bodies.maxCars"] <= 24 && dense["bodies.visitorAcquired"] > 0);
    }

    [Fact]
    public void VisitorBodyContinuesAcrossReload()
    {
        var reload = MovementRun.Run(Visitors(o => o.ReloadMidJourney = true));
        Assert.Equal(1, reload.Reloads);
        Assert.NotEmpty(reload.VisitorBodiesAtReload);
        Assert.True(reload.InFlightAtReload > 0);
        Assert.True(reload.VisitorsReacquiredAfterReload > 0, "a visitor bound at the reload received a body again");
        Assert.True(reload.Metrics["bodies.released"] >= reload.BodiesAtReload);
        Equal(reload, PlainRun.Value, "reload with a visitor body bound");
        Equal(MovementRun.Run(Plain(o => o.ReloadMidJourney = true)), PlainRun.Value, "bodies-off reload");
    }

    [Fact]
    public void ResidentsAreUnchanged()
    {
        var residents = MovementRun.Run(SelfChecks.Small("residents"));
        Assert.Equal(0, residents.Metrics["visitors.count"]);
        Assert.Equal(0, residents.Metrics["bodies.visitorAcquired"]);
        Assert.Equal(ResidentPin, (residents.JourneysSha256, residents.SalesSha256, residents.Metrics["bodies.acquired"],
            residents.Metrics["bodies.walkerFrames"], residents.Metrics["bodies.carFrames"]));
        int indexed = 0;
        var plain = SelfChecks.Small("residents-index"); plain.Driver = false;
        plain.AfterFrame = (city, _) =>
        {
            Assert.Equal(city.ActiveJourneys.Count, city.ActiveJourneys.BodyCandidates.Count);
            indexed = Math.Max(indexed, city.ActiveJourneys.Count);
        };
        MovementRun.Run(plain);
        Assert.True(indexed > 0);
    }
    private static readonly (string, string, double, double, double) ResidentPin = (
        "a7f8be191f08c52ec7d20c46c1dada69b45e5b30c85ef0940ca2b2bb3853f53c", "4941d18ef26284d373d686db2819d4799febcdeb8e6a078c57d1b8d05d7dff4c", 850, 1438800, 174675);

    private static string Between(string text, string start, string end)
    {
        int from = text.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, start);
        return text[from..text.IndexOf(end, from + start.Length, StringComparison.Ordinal)];
    }

    private static string Source(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, relative))) dir = dir.Parent;
        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(dir!.FullName, relative));
    }
}

public sealed class VisitorBodyMeasure
{
    private static string? Output => PerfGate.Variable("PC_F5I_MEASURE") is { Length: > 0 } path ? path : null;

    private static Dictionary<string, object> Time(string name, Action action)
    {
        for (int i = 0; i < 100; i++) action();
        var times = new double[300];
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < times.Length; i++)
        {
            long start = Stopwatch.GetTimestamp(); action();
            times[i] = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        }
        bytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
        Array.Sort(times);
        return new Dictionary<string, object> { ["measure"] = name, ["p50"] = Math.Round(times[150], 4), ["p95"] = Math.Round(times[285], 4),
            ["max"] = Math.Round(times[299], 4), ["bytesPerCall"] = Math.Round(bytes / 300.0, 1), ["atLeast30Ms"] = times.Count(t => t >= 30) };
    }

    internal static CitySimulator City(int factor, int active, out List<CitizenState> travellers)
    {
        var state = MovementFixture.Build(7, MovementFixture.Residents, carOwners: false, representationFactor: factor, visitors: true);
        var city = MovementFixture.Attach(state, new MovementMarket(), new GridPlanner(), fresh: true);
        var people = PersonTable.Schedules(state).ToList();
        travellers = new List<CitizenState>();
        var rng = new Random(factor * 1000 + active);
        double now = state.Minute;
        for (int n = 0; n < active; n++)
        {
            var c = people[(int)((long)n * people.Count / active)];
            var a = GridPlanner.Node(rng.Next(GridPlanner.Size), rng.Next(GridPlanner.Size));
            var b = GridPlanner.Node(rng.Next(GridPlanner.Size), rng.Next(GridPlanner.Size));
            var d = new Position(b.X, 0, a.Z);
            double Minutes(Position x, Position y) => Math.Max(0.5, Position.Distance(x, y) / GridPlanner.WalkMetresPerMinute);
            c.Journey = new Route { Legs = new List<RouteLeg> {
                new RouteLeg { From = a, To = d, Mode = TravelMode.Walk, Minutes = Minutes(a, d) },
                new RouteLeg { From = d, To = b, Mode = TravelMode.Walk, Minutes = Minutes(d, b) } } };
            c.Phase = CitizenPhase.Travelling; c.DepartureMinute = now - 0.25; c.NextEventMinute = now + c.Journey.Minutes;
            travellers.Add(c);
        }
        city.ActiveJourneys.Rebuild(state.Citizens, state.VisitorRoutines);
        Assert.True(city.ActiveJourneyIndexMatchesState());
        return city;
    }

    [Fact]
    public void SelectionAndLeaseCostWithVisitorsEligible()
    {
        if (Output == null) return;
        var lines = new List<string>();
        foreach (int factor in new[] { 2, 3 })
            foreach (int active in new[] { 0, 100, 1000 })
            {
                var city = City(factor, active, out var travellers);
                var focus = MovementFixture.ShopPosition; const double radius = 120;
                var index = city.ActiveJourneys;
                var nearby = new List<IPersonView>();
                var candidates = new List<(CitizenState Citizen, double Distance, int Roster)>();
                double minute = city.State.Minute;
                int Select()
                {
                    candidates.Clear();
                    var roster = index.Grid.Query(focus, radius, nearby);
                    for (int i = 0; i < roster.Count; i++)
                    {
                        var c = (CitizenState)roster[i];
                        if (!(c.NextEventMinute > minute) || !(c.Phase == CitizenPhase.Travelling && c.Journey != null)) continue;
                        double distance = Position.Distance(city.PositionOf(c), focus);
                        if (distance <= radius) candidates.Add((c, distance, candidates.Count));
                    }
                    candidates.Sort((x, y) =>
                    {
                        int o = x.Distance.CompareTo(y.Distance);
                        if (o == 0) o = StringComparer.Ordinal.Compare(x.Citizen.Identity.Id, y.Citizen.Identity.Id);
                        return o != 0 ? o : x.Roster.CompareTo(y.Roster);
                    });
                    for (int i = 0; i < Math.Min(4, candidates.Count); i++) _ = city.SampleJourney(candidates[i].Citizen);
                    return candidates.Count;
                }
                int inRadius = Select();
                var drivers = new Queue<PresentationDriver>();
                void Refill() { while (drivers.Count < 400) drivers.Enqueue(new PresentationDriver(city, new DriverSettings())); }
                Refill();
                var probe = new PresentationDriver(city, new DriverSettings()); probe.Update(focus, false);
                var rows = new List<Dictionary<string, object>>
                {
                    Time("selection (grid roster, Runtime filters)", () => Select()),
                    Time("selection + acquisition (F2 driver double)", () => { if (drivers.Count == 0) Refill(); drivers.Dequeue().Update(focus, false); }),
                    Time("index rebuild (construction / load)", () => index.Rebuild(city.State.Citizens, city.State.VisitorRoutines)),
                    Time("shadow comparison (30 s cadence)", () => Assert.True(city.ActiveJourneyIndexMatchesState())),
                };
                var probeTraveller = travellers.FirstOrDefault(c => c.IsVisitor);
                if (probeTraveller != null)
                {
                    var routes = new[] { probeTraveller.Journey!, new Route { Legs = probeTraveller.Journey!.Legs.Reverse().Select(l => new RouteLeg {
                        From = l.To, To = l.From, Mode = l.Mode, Minutes = l.Minutes }).ToList() } };
                    int flip = 0;
                    rows.Add(Time("visitor route commit re-filed", () => { probeTraveller.Journey = routes[++flip % 2]; index.File(probeTraveller); }));
                }
                foreach (var row in rows)
                {
                    row["visitorsReceiveBodies"] = ActiveJourneyIndex.VisitorsReceiveBodies;
                    row["persons"] = PersonTable.PersonCount(city.State); row["active"] = active;
                    row["activeVisitors"] = travellers.Count(c => c.IsVisitor);
                    row["bodyCandidates"] = index.BodyCandidates.Count; row["withheld"] = index.WithheldVisitors;
                    row["fromCells"] = index.Grid.Query(focus, radius, nearby).Count; row["inRadius"] = inRadius;
                    row["probeBodies"] = probe.Count; row["probeVisitorBodies"] = probe.VisitorBodies;
                    lines.Add(JsonConvert.SerializeObject(row));
                }
            }
        File.AppendAllLines(Output!, lines);
    }
}
