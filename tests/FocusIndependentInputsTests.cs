using System;
using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

// F2h: native inputs that feed optional stops and stations do not depend on where the player stands.
public sealed class FocusIndependentInputsTests
{
    private readonly ITestOutputHelper output;
    public FocusIndependentInputsTests(ITestOutputHelper output) => this.output = output;

    private sealed class Walking : IRoutePlanner
    {
        public Route Plan(Place from, Place to, double at, TravelPreferences p) => new()
        { Legs = { new RouteLeg { From = from.Position, To = to.Position, Mode = TravelMode.Walk, Minutes = 10 } } };
    }

    private sealed class SceneObject
    {
        public string Key = "";
        public bool Active = true;
        public bool Excluded;
        public Position Position;
    }

    // Scene double. It drives the same StaticSceneMemory and PerformerSites the native environment uses, in the
    // same order: game reasons first, then activity once; audience targets from static performer areas.
    private sealed class Scene : IOptionalStopEnvironment
    {
        public readonly Dictionary<string, SceneObject> Frontages = new(StringComparer.Ordinal);
        public readonly Dictionary<string, SceneObject> Parks = new(StringComparer.Ordinal);
        public readonly List<SceneObject> Performers = new();
        private readonly StaticSceneMemory frontageActivity = new(), parkActivity = new();
        public readonly PerformerSites Sites = new();
        private int revision, read = -1;
        public bool Raining { get; set; }
        public int Capacity = 8;
        public int SpotQueries, AvailabilityQueries;
        /// <summary>A scene load or unload: the native environment rescans performer spawners.</summary>
        public void SceneSetChanged() => revision++;
        public void DeactivateAll()
        {
            foreach (var o in Frontages.Values.Concat(Parks.Values).Concat(Performers)) o.Active = false;
            SceneSetChanged();
        }
        private void EnsurePerformers()
        {
            if (read == revision) return;
            read = revision;
            Sites.Replace(Performers.Select(o => new PerformerSiteReading(o.Key, o.Excluded, o.Position, o, o)));
        }
        private IReadOnlyList<OptionalStopSpot> Ring(string target, Position center, double radius, Position? facing = null)
            => Enumerable.Range(0, Capacity).Select(i => new OptionalStopSpot
            {
                Id = target + ":" + i, TargetId = target, Facing = facing ?? center,
                Position = new Position(center.X + radius * Math.Cos(i * Math.PI / 4), center.Y, center.Z + radius * Math.Sin(i * Math.PI / 4))
            }).ToArray();
        public IReadOnlyList<OptionalStopSpot> Spots(Place place, DwellKind kind)
        {
            SpotQueries++;
            if (kind == DwellKind.Audience)
            {
                EnsurePerformers();
                return Sites.Near(place).SelectMany(site => Ring(site.Id, site.Position, 2)).ToArray();
            }
            if (kind == DwellKind.Resting)
                return Parks.TryGetValue(place.Id, out var park) && !park.Excluded && parkActivity.Admit(place.Id, park.Active, false)
                    ? Ring(place.Id, park.Position, 3, place.Position) : Array.Empty<OptionalStopSpot>();
            return Frontages.TryGetValue(place.Id, out var front) && FrontagePresent(place.Id, front)
                ? Ring(place.Id + ":" + kind, front.Position, 2.5, place.Position) : Array.Empty<OptionalStopSpot>();
        }
        private bool FrontagePresent(string id, SceneObject front) => !front.Excluded && frontageActivity.Admit(id, front.Active, false);
        public OptionalStopAvailability Available(OptionalStopState stop)
        {
            AvailabilityQueries++;
            bool ready = stop.Kind switch
            {
                DwellKind.Audience => Audience(stop),
                DwellKind.Resting => Parks.TryGetValue(stop.TargetId, out var park) && parkActivity.Admit(stop.TargetId, park.Active, false),
                _ => Frontages.TryGetValue(stop.PlaceId, out var front) && FrontagePresent(stop.PlaceId, front)
            };
            return ready ? OptionalStopAvailability.Ready : OptionalStopAvailability.Unavailable;
        }
        private bool Audience(OptionalStopState stop) { EnsurePerformers(); return Sites.Available(stop.TargetId, stop.Facing); }
    }

    private static readonly Position Venue = new(10, 0, 0);

    private static Scene FullScene()
    {
        var scene = new Scene();
        scene.Frontages["venue"] = new SceneObject { Key = "Buildings/Shop[3]", Position = new(10, 0, 3) };
        scene.Parks["venue"] = new SceneObject { Key = "City/Park[1]/HappinessTrigger[0]", Position = new(10, 0, 6) };
        scene.Performers.Add(new SceneObject { Key = "City/Spawner[7]", Position = new(13, 0, 0) });
        return scene;
    }

    private static (CitySimulator Sim, Scene Scene) Ready(DwellKind kind, int count = 1, Scene? scene = null)
    {
        const double minute = 1440;
        var activity = kind == DwellKind.Break ? Activity.Work : kind == DwellKind.WindowShopping ? Activity.Shop : Activity.Leisure;
        var identities = Enumerable.Range(0, count).Select(i => new CitizenIdentity
        { Id = "c" + i, Name = "Citizen " + i, Age = 30, Persona = Persona.Commuter }).ToArray();
        var places = new[] { new Place { Id = "home", Name = "Home", Capacity = count, Kind = PlaceKind.Home },
            new Place { Id = "venue", Name = "Venue", Kind = (PlaceKind)activity, Capacity = count, Position = Venue } };
        var state = Population.Create(identities, places, "f2h-tests", "0.40.4", 17, minute);
        foreach (var c in state.Citizens)
        {
            c.Phase = CitizenPhase.Travelling; c.PlaceId = c.HomeId; c.Activity = Activity.Home;
            c.DestinationId = "venue"; c.DestinationActivity = activity;
            c.DepartureMinute = minute; c.NextEventMinute = minute + 10; c.ActivityUntilMinute = minute + 130;
            c.Journey = new Walking().Plan(places[0], places[1], minute, c.Preferences);
        }
        scene ??= FullScene();
        // Only the kind under test is in the scene, so the planner cannot fall through to another kind.
        if (kind != DwellKind.Audience) scene.Performers.Clear();
        if (kind != DwellKind.Resting) scene.Parks.Clear();
        if (kind == DwellKind.Audience || kind == DwellKind.Resting) scene.Frontages.Clear();
        var sim = new CitySimulator(state, new Walking()); sim.AttachOptionalStops(scene, 100);
        return (sim, scene);
    }

    public static IEnumerable<object[]> Kinds()
    {
        foreach (var kind in new[] { DwellKind.Break, DwellKind.WindowShopping, DwellKind.ChatOutsideVenue, DwellKind.Resting, DwellKind.Audience })
            yield return new object[] { kind };
    }

    private static string Image(IReadOnlyList<OptionalStopSpot> spots)
        => string.Join("|", spots.Select(s => s.Id + "@" + s.TargetId + ":" + P(s.Position) + ">" + P(s.Facing)));
    private static string P(Position p) => FormattableString.Invariant($"{p.X:R},{p.Y:R},{p.Z:R}");

    [Theory]
    [MemberData(nameof(Kinds))]
    public void EnvironmentAnswersAreIdenticalBeforeAndAfterObjectsAreDeactivated(DwellKind kind)
    {
        var (sim, scene) = Ready(kind);
        var place = sim.State.Places.Single(p => p.Id == "venue");
        var spots = scene.Spots(place, kind);
        Assert.NotEmpty(spots);
        var stops = spots.Select(s => new OptionalStopState { Kind = kind, PlaceId = "venue", SpotId = s.Id, TargetId = s.TargetId,
            Position = s.Position, Facing = s.Facing }).ToArray();
        string before = Image(spots);
        var availableBefore = stops.Select(scene.Available).ToArray();
        Assert.All(availableBefore, a => Assert.Equal(OptionalStopAvailability.Ready, a));
        scene.DeactivateAll();
        Assert.Equal(before, Image(scene.Spots(place, kind)));
        Assert.Equal(availableBefore, stops.Select(scene.Available).ToArray());
        // Reactivation and a further rescan change nothing either.
        foreach (var o in scene.Frontages.Values.Concat(scene.Parks.Values).Concat(scene.Performers)) o.Active = true;
        scene.SceneSetChanged();
        Assert.Equal(before, Image(scene.Spots(place, kind)));
        Assert.Equal(availableBefore, stops.Select(scene.Available).ToArray());
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void DeactivationDoesNotCancelSavedStopsAndTheSaveMatchesAnUntouchedArm(DwellKind kind)
    {
        int count = kind == DwellKind.ChatOutsideVenue ? 2 : 6;
        var (watched, near) = Ready(kind, count);
        var (away, _) = Ready(kind, count);
        watched.AdvanceTo(1450); away.AdvanceTo(1450);
        var planned = watched.State.Citizens.Where(c => c.OptionalStop != null).ToArray();
        Assert.NotEmpty(planned);
        Assert.All(planned, c => Assert.Equal(kind, c.OptionalStop!.Kind));
        double active = planned.Max(c => c.OptionalStop!.StartMinute) + 0.1;
        // The player walks up: native objects near them are pooled, culled or toggled.
        near.DeactivateAll();
        for (double m = 1450.25; m <= active; m += 0.25) { watched.AdvanceTo(m); watched.RefreshOptionalStops(); away.AdvanceTo(m); away.RefreshOptionalStops(); }
        Assert.All(planned, c => Assert.False(c.OptionalStop!.Cancelled));
        Assert.Equal(DevelopmentChain.Write(away.State), DevelopmentChain.Write(watched.State));
        watched.AdvanceTo(1600); away.AdvanceTo(1600);
        Assert.Equal(DevelopmentChain.Write(away.State), DevelopmentChain.Write(watched.State));
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void RainStillCancelsAfterDeactivation(DwellKind kind)
    {
        var (sim, scene) = Ready(kind, kind == DwellKind.ChatOutsideVenue ? 2 : 4); sim.AdvanceTo(1450);
        var planned = sim.State.Citizens.Where(c => c.OptionalStop != null).ToArray(); Assert.NotEmpty(planned);
        scene.DeactivateAll(); sim.RefreshOptionalStops();
        Assert.All(planned, c => Assert.False(c.OptionalStop!.Cancelled));
        scene.Raining = true; sim.RefreshOptionalStops();
        Assert.All(planned, c => Assert.True(c.OptionalStop!.Cancelled));
    }

    [Fact]
    public void ClosingAndPartnerLossStillCancelAfterDeactivation()
    {
        var (work, workScene) = Ready(DwellKind.Break); work.AdvanceTo(1450);
        workScene.DeactivateAll(); work.RefreshOptionalStops();
        Assert.False(work.State.Citizens[0].OptionalStop!.Cancelled);
        work.State.Places.Single(p => p.Id == "venue").CloseMinute = 0; work.RefreshOptionalStops();
        Assert.True(work.State.Citizens[0].OptionalStop!.Cancelled);

        var (group, groupScene) = Ready(DwellKind.ChatOutsideVenue, 2); group.AdvanceTo(1450);
        groupScene.DeactivateAll(); group.RefreshOptionalStops();
        Assert.All(group.State.Citizens, c => Assert.False(c.OptionalStop!.Cancelled));
        group.State.Citizens[0].OptionalStop = null; group.RefreshOptionalStops();
        Assert.True(group.State.Citizens[1].OptionalStop!.Cancelled);

        var (pair, pairScene) = Ready(DwellKind.ChatOutsideVenue, 2); pair.AdvanceTo(1450);
        pairScene.DeactivateAll();
        pair.CancelOptionalStop(pair.State.Citizens[0].Identity.Id);
        Assert.All(pair.State.Citizens, c => Assert.True(c.OptionalStop!.Cancelled));
    }

    [Fact]
    public void DlcGatedInaccessibleClosedAndNeverActiveObjectsStayExcluded()
    {
        // DLC-gated performer area and an inaccessible building frontage: active, but excluded by the game.
        var dlc = FullScene(); dlc.Performers[0].Excluded = true;
        var (audience, _) = Ready(DwellKind.Audience, 4, dlc); audience.AdvanceTo(1450);
        Assert.All(audience.State.Citizens, c => Assert.Null(c.OptionalStop));
        Assert.Equal(0, dlc.Sites.Count);
        var locked = FullScene(); locked.Frontages["venue"].Excluded = true;
        var (shop, _) = Ready(DwellKind.WindowShopping, 4, locked); shop.AdvanceTo(1450);
        Assert.All(shop.State.Citizens, c => Assert.Null(c.OptionalStop));
        // A validated frontage that the game then closes still ends the saved stop.
        var (open, openScene) = Ready(DwellKind.WindowShopping, 2); open.AdvanceTo(1450);
        var stop = open.State.Citizens.First(c => c.OptionalStop != null).OptionalStop!;
        openScene.Frontages["venue"].Excluded = true; open.RefreshOptionalStops();
        Assert.True(stop.Cancelled);
        // A performer area the game hides after validation, or one gone from the scene, ends audience stops.
        var (shown, shownScene) = Ready(DwellKind.Audience, 2); shown.AdvanceTo(1450);
        var watching = shown.State.Citizens.First(c => c.OptionalStop != null).OptionalStop!;
        shownScene.Performers[0].Excluded = true; shownScene.SceneSetChanged(); shown.RefreshOptionalStops();
        Assert.True(watching.Cancelled);
        var (gone, goneScene) = Ready(DwellKind.Audience, 2); gone.AdvanceTo(1450);
        var orphan = gone.State.Citizens.First(c => c.OptionalStop != null).OptionalStop!;
        goneScene.Performers.Clear(); goneScene.SceneSetChanged(); gone.RefreshOptionalStops();
        Assert.True(orphan.Cancelled);
    }

    [Fact]
    public void SpawnerInactiveAtTheFirstScanIsCataloguedLikeAnActiveOne()
    {
        // CityMapHider deactivates city objects while the player is indoors or the city map is open, including
        // when a save loads inside a building. The performer catalogue must not depend on that first scan.
        var indoors = FullScene(); indoors.Performers[0].Active = false;
        var (loadedIndoors, indoorScene) = Ready(DwellKind.Audience, 4, indoors);
        var (loadedOutdoors, _) = Ready(DwellKind.Audience, 4);
        loadedIndoors.AdvanceTo(1450); loadedOutdoors.AdvanceTo(1450);
        Assert.Equal(1, indoorScene.Sites.Count);
        Assert.Contains(loadedIndoors.State.Citizens, c => c.OptionalStop != null);
        // The spawner becomes active later (the player walks out); nothing changes and no rescan is needed.
        indoors.Performers[0].Active = true; loadedIndoors.RefreshOptionalStops(); loadedOutdoors.RefreshOptionalStops();
        Assert.Equal(DevelopmentChain.Write(loadedOutdoors.State), DevelopmentChain.Write(loadedIndoors.State));
        loadedIndoors.AdvanceTo(1600); loadedOutdoors.AdvanceTo(1600);
        Assert.Equal(DevelopmentChain.Write(loadedOutdoors.State), DevelopmentChain.Write(loadedIndoors.State));
    }

    // Base-shape model of the removed live performer check, for the RefreshOptionalStops baseline: per audience stop,
    // the target and zone lookups, a reflection read of the spawner's private performer field, an activity flag, a
    // transform identity compare and the facing distance. Spots come from the same scene double. It models CPU work
    // only; Unity's native property costs (activeInHierarchy, transform.position) are not included.
    private sealed class LivePerformerModel : IOptionalStopEnvironment
    {
        private sealed class Zone { private readonly object? _streetPerformer; public Zone(object p) => _streetPerformer = p; public object? Peek() => _streetPerformer; }
        private sealed class Performer { public bool Active = true; public Position At; }
        private static readonly System.Reflection.FieldInfo Field = typeof(Zone).GetField("_streetPerformer",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        private readonly Scene scene;
        private readonly Dictionary<string, Performer> targets = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Zone> zones = new(StringComparer.Ordinal);
        public LivePerformerModel(Scene scene) => this.scene = scene;
        public bool Raining => scene.Raining;
        public IReadOnlyList<OptionalStopSpot> Spots(Place place, DwellKind kind)
        {
            var spots = scene.Spots(place, kind);
            foreach (var spot in spots)
                if (!targets.ContainsKey(spot.TargetId))
                { var live = new Performer { At = spot.Facing }; targets[spot.TargetId] = live; zones[spot.TargetId] = new Zone(live); }
            return spots;
        }
        public OptionalStopAvailability Available(OptionalStopState stop)
            => targets.TryGetValue(stop.TargetId, out var target) && target.Active
                && zones.TryGetValue(stop.TargetId, out var zone) && Field.GetValue(zone) is Performer live
                && ReferenceEquals(live, target) && Position.Distance(stop.Facing, live.At) <= 0.5
                ? OptionalStopAvailability.Ready : OptionalStopAvailability.Unavailable;
    }

    private static (double P50, double P95, double Max, int Stops) RefreshCost(bool liveModel)
    {
        var (sim, scene) = Ready(DwellKind.Audience, 48);
        scene.Performers.AddRange(Enumerable.Range(0, 5).Select(i => new SceneObject { Key = "City/Extra[" + i + "]", Position = new Position(10 + i, 0, -3) }));
        if (liveModel) sim.AttachOptionalStops(new LivePerformerModel(scene), 100);
        sim.AdvanceTo(1450);
        double activeMinute = sim.State.Citizens.Where(c => c.OptionalStop != null).Max(c => c.OptionalStop!.StartMinute) + 0.1;
        sim.AdvanceTo(activeMinute);
        // After: the scene is deactivated (the stops survive). Before: performers stay live, so both arms keep every stop.
        if (!liveModel) scene.DeactivateAll();
        int stops = sim.State.Citizens.Count(c => c.OptionalStop is { Cancelled: false });
        var clock = new Stopwatch(); var samples = new List<double>();
        for (int r = 0; r < 2000; r++) { clock.Restart(); sim.RefreshOptionalStops(); samples.Add(clock.Elapsed.TotalMilliseconds); }
        samples.Sort();
        Assert.Equal(stops, sim.State.Citizens.Count(c => c.OptionalStop is { Cancelled: false }));
        return (samples[1000], samples[1900], samples[^1], stops);
    }

    private sealed class Station { public string Key = ""; public string Name = ""; public bool Active = true; public bool DlcHidden; }

    // EnsureStations' filter: DLC visibility every preparation, activity once per static scene key.
    private static string[] StationSet(IEnumerable<Station> scene, StaticSceneMemory memory)
        => scene.Where(s => memory.Admit(s.Key, s.Active, s.DlcHidden)).Select(s => "subway:" + s.Name)
            .OrderBy(id => id, StringComparer.Ordinal).ToArray();

    [Fact]
    public void StationSetIsUnchangedByDeactivationAndKeepsDlcAndNeverActiveExclusions()
    {
        var stations = new[]
        {
            new Station { Key = "Subway/Station[0]", Name = "Midtown" },
            new Station { Key = "Subway/Station[1]", Name = "Harbor" },
            new Station { Key = "Subway/Station[2]", Name = "Hamptons", DlcHidden = true },
            new Station { Key = "Subway/Station[3]", Name = "Unused", Active = false }
        };
        var memory = new StaticSceneMemory();
        var before = StationSet(stations, memory);
        Assert.Equal(new[] { "subway:Harbor", "subway:Midtown" }, before);
        foreach (var s in stations) s.Active = false;
        Assert.Equal(before, StationSet(stations, memory));
        // Retimed routes keep the save-scoped evidence (ReuseRoadSceneForRetiming).
        var retimed = new StaticSceneMemory(); retimed.Include(memory);
        Assert.Equal(before, StationSet(stations, retimed));
        Assert.Equal(2, retimed.Count);
        // A DLC toggle that hides a validated station still removes it.
        stations[1].DlcHidden = true;
        Assert.Equal(new[] { "subway:Midtown" }, StationSet(stations, memory));
    }

    [Fact]
    public void AudienceStopsUseTheStaticPerformerAreaAndNeedNoLivePerformer()
    {
        var sites = new PerformerSites();
        object zone = new();
        var center = new Position(13, 0, 0);
        sites.Replace(new[] { new PerformerSiteReading("City/Spawner[7]", false, center, zone, zone),
            new PerformerSiteReading("City/Spawner[9]", false, new Position(40, 0, 0), new object(), new object()) });
        var place = new Place { Id = "venue", Position = Venue };
        var near = sites.Near(place);
        Assert.Equal(new[] { "performer:City/Spawner[7]" }, near.Select(t => t.Id));
        Assert.Same(near, sites.Near(place));
        Assert.Equal(center, near[0].Position);
        // No performer object exists anywhere in this reading; the stop is still available and faces the area (Q4).
        Assert.True(sites.Available("performer:City/Spawner[7]", center));
        Assert.True(sites.Available("performer:City/Spawner[7]", new Position(13.4, 0, 0)));
        Assert.False(sites.Available("performer:City/Spawner[7]", new Position(13.6, 0, 0)));
        Assert.False(sites.Available("performer:City/Spawner[8]", center));
        // A rescan keeps the site; only a game reason removes it.
        sites.Replace(new[] { new PerformerSiteReading("City/Spawner[7]", false, center, zone, zone) });
        Assert.True(sites.Available("performer:City/Spawner[7]", center));
        sites.Replace(new[] { new PerformerSiteReading("City/Spawner[7]", true, center, zone, zone) });
        Assert.False(sites.Available("performer:City/Spawner[7]", center));
    }

    private static string Root()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Runtime", "OptionalStopEnvironment.cs"))) root = root.Parent;
        Assert.NotNull(root); return root!.FullName;
    }

    [Fact]
    public void NativeReadsRouteActivityThroughStaticMemoryAndReadNoLivePerformer()
    {
        string environment = File.ReadAllText(Path.Combine(Root(), "Runtime", "OptionalStopEnvironment.cs"));
        var livePerformer = new Regex(@"\bStreetPerformer\b");
        Assert.DoesNotMatch(livePerformer, environment);
        Assert.DoesNotContain("AllPedestrianSpawners", environment);
        Assert.DoesNotContain("_streetPerformer", environment);
        var activity = environment.Split('\n').Where(line => line.Contains("activeInHierarchy")).ToArray();
        Assert.Equal(3, activity.Length);
        Assert.All(activity, line => Assert.Contains("Activity.Admit(", line));
        string routes = File.ReadAllText(Path.Combine(Root(), "Runtime", "MultimodalRoutes.cs"));
        int start = routes.IndexOf("private void EnsureStations()", StringComparison.Ordinal);
        string stations = routes.Substring(start, routes.IndexOf("private List<Station> Nearby", start, StringComparison.Ordinal) - start);
        Assert.Contains("stationActivity.Admit(sceneKey, active, hidden)", stations);
        Assert.Single(stations.Split('\n'), line => line.Contains("activeInHierarchy"));
        // Q4: a visible audience member at its spot faces the saved facing point; no native performer is consulted.
        string bodies = string.Concat(new[] { "", ".Following", ".Queues", ".Selection" }.Select(part => File.ReadAllText(Path.Combine(Root(), "Runtime", "PhysicalPopulation" + part + ".cs"))));
        Assert.Contains("body.Face(stop.Facing)", bodies);
        Assert.DoesNotMatch(livePerformer, bodies);
    }

    [Fact]
    public void OfflineCostOfCachedSceneAnswers()
    {
        // Offline component timings only, not in-game mod-work. 200 spawners, 64 places, 1,000 saved audience stops.
        var readings = Enumerable.Range(0, 200).Select(i => new PerformerSiteReading("City/Spawner[" + i + "]", false,
            new Position(i * 5, 0, (i % 7) * 3), new object(), new object())).ToArray();
        var places = Enumerable.Range(0, 64).Select(i => new Place { Id = "p" + i, Position = new Position(i * 15, 0, 2) }).ToArray();
        var sites = new PerformerSites();
        var clock = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++) sites.Replace(readings);
        double replace = clock.Elapsed.TotalMilliseconds / 100;
        // Before shape: every audience query scanned every spawner with a distance test.
        clock.Restart(); int found = 0;
        for (int r = 0; r < 1000; r++) foreach (var place in places) foreach (var z in readings) if (Position.Distance(place.Position, z.Center) <= 8) found++;
        double scan = clock.Elapsed.TotalMilliseconds / (1000.0 * places.Length);
        clock.Restart(); int cached = 0;
        for (int r = 0; r < 1000; r++) foreach (var place in places) cached += sites.Near(place).Count;
        double near = clock.Elapsed.TotalMilliseconds / (1000.0 * places.Length);
        Assert.Equal(found, cached);
        var stops = Enumerable.Range(0, 1000).Select(i => (Id: "performer:City/Spawner[" + i % 200 + "]", Facing: readings[i % 200].Center)).ToArray();
        clock.Restart(); int ready = 0;
        for (int r = 0; r < 100; r++) foreach (var s in stops) if (sites.Available(s.Id, s.Facing)) ready++;
        double available = clock.Elapsed.TotalMilliseconds / 100;
        Assert.Equal(100 * 1000, ready);
        var memory = new StaticSceneMemory();
        for (int i = 0; i < 1000; i++) memory.Admit("k" + i, true, false);
        clock.Restart();
        for (int r = 0; r < 100; r++) for (int i = 0; i < 1000; i++) memory.Admit("k" + i, false, false);
        double admit = clock.Elapsed.TotalMilliseconds / 100;
        // RefreshOptionalStops, same fixture and sampling: base-shape live performer model, then this build.
        var before = RefreshCost(liveModel: true);
        var after = RefreshCost(liveModel: false);
        Assert.Equal(before.Stops, after.Stops);
        output.WriteLine($"F2H-PERF sites={sites.Count} cachedPlaces={sites.CachedPlaces} memory={memory.Count}");
        output.WriteLine($"F2H-PERF replace200={replace:F4}ms scanPerQuery={scan * 1000:F3}us nearPerQuery={near * 1000:F3}us available1000={available:F4}ms admit1000={admit:F4}ms");
        output.WriteLine($"F2H-PERF refresh-before stops={before.Stops} p50={before.P50:F4}ms p95={before.P95:F4}ms max={before.Max:F4}ms");
        output.WriteLine($"F2H-PERF refresh-after stops={after.Stops} p50={after.P50:F4}ms p95={after.P95:F4}ms max={after.Max:F4}ms");
    }
    // P2 review: a scene-set change marks park and performer caches pending together, before any query, and each stays
    // pending until its own scan runs, even while another scan takes the frame's slot.
    [Fact]
    public void SceneChangeKeepsEveryCachePendingUntilItsOwnScan()
    {
        var scans = new SceneScanSchedule();
        scans.Completed(SceneScan.Parks); scans.Completed(SceneScan.Performers);
        Assert.Equal(SceneScan.None, scans.Next);
        scans.SceneSetChanged();
        Assert.True(scans.Due(DwellKind.Resting)); Assert.True(scans.Due(DwellKind.Audience));
        Assert.False(scans.Due(DwellKind.Break));
        Assert.Equal(SceneScan.Parks, scans.Next); scans.Completed(SceneScan.Parks);
        // The park scan used this frame: the performer catalogue is still the old scene's and is not read.
        Assert.False(scans.Due(DwellKind.Resting)); Assert.True(scans.Due(DwellKind.Audience));
        Assert.Equal(SceneScan.Performers, scans.Next); scans.Completed(SceneScan.Performers);
        Assert.False(scans.Due(DwellKind.Audience)); Assert.Equal(SceneScan.None, scans.Next);
    }

    // P2 review: a cold saved resting stop whose cached trigger was replaced stays Deferred while the rescan is pending
    // and is decided only after Maintain has run it.
    [Fact]
    public void ReplacedParkTriggerDefersAColdSavedStopUntilMaintainRescans()
    {
        var scans = new SceneScanSchedule();
        scans.Completed(SceneScan.Parks); scans.Completed(SceneScan.Performers);
        // The geometry read finds the cached trigger destroyed and raises the park scan, then reports a miss.
        scans.ParkTriggerLost();
        Assert.Equal(OptionalStopAvailability.Deferred,
            SceneScanSchedule.AfterGeometry(OptionalStopAvailability.Unavailable, scans.Due(DwellKind.Resting)));
        Assert.Equal(OptionalStopAvailability.Deferred,
            SceneScanSchedule.AfterGeometry(OptionalStopAvailability.Deferred, scans.Due(DwellKind.Resting)));
        Assert.False(scans.Due(DwellKind.Audience));
        Assert.Equal(OptionalStopAvailability.Unavailable,
            SceneScanSchedule.AfterGeometry(OptionalStopAvailability.Unavailable, scans.Due(DwellKind.Audience)));
        // Maintain runs the park scan; a miss after it ends the stop, and a hit is Ready either way.
        Assert.Equal(SceneScan.Parks, scans.Next); scans.Completed(SceneScan.Parks);
        Assert.Equal(OptionalStopAvailability.Unavailable,
            SceneScanSchedule.AfterGeometry(OptionalStopAvailability.Unavailable, scans.Due(DwellKind.Resting)));
        Assert.Equal(OptionalStopAvailability.Ready, SceneScanSchedule.AfterGeometry(OptionalStopAvailability.Ready, true));
        string environment = File.ReadAllText(Path.Combine(Root(), "Runtime", "OptionalStopEnvironment.cs"));
        // Scene changes are observed on the first read of a frame, RefreshOptionalStops before Maintain included.
        foreach (string entry in new[] { "public void Maintain()", "public IReadOnlyList<OptionalStopSpot> Spots(", "public OptionalStopAvailability Available(" })
        {
            int at = environment.IndexOf(entry, StringComparison.Ordinal);
            Assert.True(at >= 0, entry);
            Assert.Contains("ObserveScenes();", environment.Substring(at, 220));
        }
    }
}
