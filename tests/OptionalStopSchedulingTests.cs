using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;
public sealed class OptionalStopSchedulingTests
{
    private sealed class Walking : IRoutePlanner
    {
        public Route Plan(Place from, Place to, double at, TravelPreferences p) => new()
        { Legs = { new RouteLeg { From = from.Position, To = to.Position, Mode = TravelMode.Walk, Minutes = 10 } } };
    }
    private sealed class Environment : IOptionalStopEnvironment
    {
        public int SpotQueries;
        public bool Raining { get; set; }
        public bool Present = true;
        public DwellKind Kind;
        public int Capacity = 4;
        public IReadOnlyList<OptionalStopSpot>? Supplied;
        public IReadOnlyList<OptionalStopSpot> Spots(Place place, DwellKind kind)
        {
            SpotQueries++;
            return !Present || kind != Kind ? Array.Empty<OptionalStopSpot>()
                : Supplied ?? Enumerable.Range(0, Capacity).Select(i => new OptionalStopSpot
                { Id = "spot:" + i, TargetId = "target", Position = new(10 + i, 0, 1), Facing = new(10, 0, 3) }).ToArray();
        }
        public OptionalStopAvailability Available(OptionalStopState stop) => Present ? OptionalStopAvailability.Ready : OptionalStopAvailability.Unavailable;
    }
    private static (CitySimulator Sim, Environment Env) Ready(DwellKind kind, int count = 1, double stay = 120,
        double minute = 1440, int percentage = 100, TravelMode arrivalMode = TravelMode.Walk)
    {
        var activity = kind == DwellKind.Break ? Activity.Work : kind == DwellKind.WindowShopping ? Activity.Shop : Activity.Leisure;
        var identities = Enumerable.Range(0, count).Select(i => new CitizenIdentity { Id = "c" + i, Name = "Citizen " + i,
            Age = 30, Persona = Persona.Commuter }).ToArray();
        var places = new[] { new Place { Id = "home", Name = "Home", Capacity = count, Kind = PlaceKind.Home },
            new Place { Id = "venue", Name = "Venue", Kind = (PlaceKind)activity, Capacity = count, Position = new(10, 0, 0) } };
        var state = Population.Create(identities, places, "stop-tests", "0.40.4", 17, minute);
        foreach (var c in state.Citizens)
        {
            c.Phase = CitizenPhase.Travelling; c.PlaceId = c.HomeId; c.Activity = Activity.Home;
            c.DestinationId = "venue"; c.DestinationActivity = activity;
            c.DepartureMinute = minute; c.NextEventMinute = minute + 10; c.ActivityUntilMinute = minute + 10 + stay;
            c.Journey = new Walking().Plan(places[0], places[1], minute, c.Preferences);
            c.Journey.Legs[0].Mode = arrivalMode;
        }
        var env = new Environment { Kind = kind };
        var sim = new CitySimulator(state, new Walking()); sim.AttachOptionalStops(env, percentage);
        return (sim, env);
    }
    [Theory]
    [MemberData(nameof(StreetLifeArms))]
    public void DefaultTimingKeepsEntireSaveAndTunedReloadKeepsCommittedTerms(DwellKind kind, int rate)
    {
        var (baseline, _) = Ready(kind, count: 48, percentage: rate);
        var (defaults, defaultEnv) = Ready(kind, count: 48, percentage: rate);
        defaults.AttachOptionalStops(defaultEnv, rate, new OptionalStopTiming());
        baseline.AdvanceTo(1450); defaults.AdvanceTo(1450);
        Assert.Equal(SaveCodec.Write(baseline.State), SaveCodec.Write(defaults.State));
        string saved = SaveCodec.Write(defaults.State);
        var loaded = new CitySimulator(SaveCodec.Read(saved).State!, new Walking());
        var minimum = new OptionalStopTiming(5, 10, 1, 2, 10);
        loaded.AttachOptionalStops(defaultEnv, rate, minimum);
        Assert.Equal(saved, SaveCodec.Write(loaded.State)); // no saved stop shortened or restarted
        var rebuilt = loaded.RebuildRoutes(new Walking());
        Assert.Equal(saved, SaveCodec.Write(rebuilt.State));
        var (tuned, env) = Ready(kind, count: 48, percentage: rate);
        tuned.AttachOptionalStops(env, rate, minimum); tuned.AdvanceTo(1450);
        Assert.All(tuned.State.Citizens.Where(c => c.OptionalStop != null), c =>
            Assert.Equal(OptionalStopPolicy.Minimum(kind), c.OptionalStop!.EndMinute - c.OptionalStop.StartMinute));
        Assert.Equal(baseline.OptionalStopChances.Drawn, tuned.OptionalStopChances.Drawn);
        Assert.Equal(baseline.OptionalStopChances.Realised, tuned.OptionalStopChances.Realised);
    }

    public static IEnumerable<object[]> StreetLifeArms()
    {
        foreach (var kind in new[] { DwellKind.Break, DwellKind.ChatOutsideVenue, DwellKind.WindowShopping, DwellKind.Resting, DwellKind.Audience })
            foreach (int rate in new[] { 0, 25, 100 }) yield return new object[] { kind, rate };
    }

    [Theory]
    [MemberData(nameof(StreetLifeArms))]
    public void EveryKindUsesExistingResidentsAndConservesChancesAtDayAndNight(DwellKind kind, int rate)
    {
        foreach (double minute in new[] { 1440 + 12 * 60.0, 2880 + 3 * 60.0 })
        {
            var (sim, env) = Ready(kind, count: 48, minute: minute, percentage: rate);
            env.Capacity = 48;
            var ids = sim.State.Citizens.Select(c => c.Identity.Id).ToArray();
            sim.AdvanceTo(minute + 10);
            var tally = sim.OptionalStopChances;
            long pending = sim.State.Citizens.Count(c => c.OptionalStopChoice == true && c.OptionalStop == null);
            long expectedDraws = sim.State.Citizens.Count(c => OptionalStopPolicy.Choose(
                OptionalStopPolicy.VisitKey(c, minute + 10), sim.State.Seed, rate));
            Assert.Equal(48, tally.Eligible);
            Assert.Equal(expectedDraws, tally.Drawn); // no night multiplier or extra opportunities
            Assert.Equal(tally.Drawn, tally.Realised + pending + tally.Dropped);
            Assert.Equal(sim.State.Citizens.Count(c => c.OptionalStop != null), tally.Realised);
            Assert.Equal(0, tally.Dropped);
            Assert.True(sim.OptionalStopIndexesMatchState());
            Assert.Equal(ids, sim.State.Citizens.Select(c => c.Identity.Id));
            Assert.All(sim.State.Citizens, c => { Assert.Equal(1, c.CompletedJourneys); Assert.Single(c.RecentVisits); });
            var stops = sim.State.Citizens.Where(c => c.OptionalStop != null).Select(c => c.OptionalStop!).ToArray();
            Assert.All(stops, s => Assert.Equal(kind, s.Kind));
            Assert.Equal(stops.Length, stops.Select(s => s.SpotId).Distinct().Count());
            if (rate == 0) { Assert.Empty(stops); Assert.Equal(0, env.SpotQueries); }
            else Assert.NotEmpty(stops);
            if (rate == 100) Assert.Equal(48, stops.Length);
            // Reload during the stop, including future work breaks, and continue past the original activity.
            double activeMinute = stops.Length == 0 ? sim.State.Minute : stops.Max(s => s.StartMinute) + 0.1;
            sim.AdvanceTo(activeMinute);
            string saved = SaveCodec.Write(sim.State);
            var read = SaveCodec.Read(saved); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
            var loaded = new CitySimulator(read.State!, new Walking()); loaded.AttachOptionalStops(env, rate);
            Assert.Equal(saved, SaveCodec.Write(loaded.State));
            sim.AdvanceTo(minute + 140); loaded.AdvanceTo(minute + 140);
            Assert.Equal(SaveCodec.Write(sim.State), SaveCodec.Write(loaded.State));
            // Daytime work may continue under the existing routine at the next event;
            // the overlay must nevertheless end and replay the same continuation.
            Assert.All(loaded.State.Citizens, c => Assert.True(c.OptionalStop == null || !c.OptionalStop.ActiveAt(loaded.State.Minute)));
        }
    }

    [Theory]
    [MemberData(nameof(StreetLifeArms))]
    public void WetAdmissionDoesNotDrawAndDryingDoesNotReplayThatVisit(DwellKind kind, int rate)
    {
        var (sim, env) = Ready(kind, 48, percentage: rate); env.Capacity = 48;
        env.Raining = true; sim.AdvanceTo(1450);
        Assert.Equal(0, sim.OptionalStopChances.Eligible);
        Assert.Equal(0, sim.OptionalStopChances.Drawn);
        Assert.Equal(0, env.SpotQueries);
        Assert.All(sim.State.Citizens, c => Assert.Null(c.OptionalStop));
        var read = SaveCodec.Read(SaveCodec.Write(sim.State)); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var loaded = new CitySimulator(read.State!, new Walking());
        env.Raining = false; loaded.AttachOptionalStops(env, rate); loaded.AdvanceTo(1451); loaded.RefreshOptionalStops();
        Assert.Equal(0, loaded.OptionalStopChances.Eligible);
        Assert.All(loaded.State.Citizens, c => { Assert.Null(c.OptionalStop); Assert.Single(c.RecentVisits); Assert.Equal(1570, c.NextEventMinute); });
        // Rain ending before admission permits the ordinary draw, without creating another journey.
        var (dry, drying) = Ready(kind, 48, percentage: rate); drying.Capacity = 48;
        drying.Raining = true; dry.AdvanceTo(1449); drying.Raining = false; dry.AdvanceTo(1450);
        Assert.Equal(48, dry.OptionalStopChances.Eligible);
        Assert.Equal(rate != 0, dry.State.Citizens.Any(c => c.OptionalStop != null));
    }

    [Theory]
    [MemberData(nameof(StreetLifeArms))]
    public void RainDuringEveryKindCancelsPermanentlyThroughReloadAndReturnsToOriginalActivity(DwellKind kind, int rate)
    {
        var (sim, env) = Ready(kind, 48, percentage: rate); env.Capacity = 48; sim.AdvanceTo(1450);
        var originals = sim.State.Citizens.Where(c => c.OptionalStop != null).ToDictionary(c => c.Identity.Id, c => c.OptionalStop!.Copy());
        if (rate != 0) Assert.NotEmpty(originals);
        double activeMinute = originals.Count == 0 ? 1451 : originals.Values.Max(s => s.StartMinute) + 0.1;
        sim.AdvanceTo(activeMinute);
        Assert.All(sim.State.Citizens.Where(c => originals.ContainsKey(c.Identity.Id)), c => Assert.True(c.OptionalStop!.ActiveAt(activeMinute)));
        env.Raining = true; sim.RefreshOptionalStops();
        Assert.All(sim.State.Citizens.Where(c => originals.ContainsKey(c.Identity.Id)), c => Assert.True(c.OptionalStop!.Cancelled));
        string saved = SaveCodec.Write(sim.State);
        var read = SaveCodec.Read(saved); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var loaded = new CitySimulator(read.State!, new Walking()); env.Raining = false; loaded.AttachOptionalStops(env, rate);
        loaded.RefreshOptionalStops(); Assert.Equal(saved, SaveCodec.Write(loaded.State));
        foreach (var c in loaded.State.Citizens.Where(c => originals.ContainsKey(c.Identity.Id)))
        {
            Assert.Equal(originals[c.Identity.Id].EndMinute, c.OptionalStop!.EndMinute);
            Assert.Equal(originals[c.Identity.Id].Members, c.OptionalStop.Members);
            Assert.Equal(1570, c.NextEventMinute);
        }
        loaded.AdvanceTo(1580);
        Assert.All(loaded.State.Citizens, c => { Assert.Null(c.OptionalStop); Assert.Equal(Activity.Home, c.Activity); Assert.Equal(2, c.CompletedJourneys); });
    }

    [Theory]
    [InlineData(0, 100, false)]
    [InlineData(100, 0, true)]
    public void ChangedRateOnlyAffectsNewChancesAndNeverRerollsSavedVisits(int oldRate, int newRate, bool chosen)
    {
        var (sim, env) = Ready(DwellKind.WindowShopping, percentage: oldRate);
        sim.AdvanceTo(1450);
        string saved = SaveCodec.Write(sim.State);
        var read = SaveCodec.Read(saved); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var loaded = new CitySimulator(read.State!, new Walking());
        loaded.AttachOptionalStops(env, newRate);
        loaded = loaded.RebuildRoutes(new Walking());
        loaded.RefreshOptionalStops();
        Assert.Equal(saved, SaveCodec.Write(loaded.State));
        loaded.AdvanceTo(1451);
        Assert.Equal(chosen, loaded.State.Citizens[0].OptionalStopChoice);
        Assert.Equal(chosen, loaded.State.Citizens[0].OptionalStop != null);

        var (fresh, freshEnv) = Ready(DwellKind.WindowShopping, percentage: oldRate);
        fresh.AttachOptionalStops(freshEnv, newRate); fresh.AdvanceTo(1450);
        Assert.Equal(!chosen, fresh.State.Citizens[0].OptionalStopChoice);
        Assert.Equal(!chosen, fresh.State.Citizens[0].OptionalStop != null);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(100, true)]
    public void WaitingConversationPartnerRetainsSavedChoiceAfterRateChange(int oldRate, bool joins)
    {
        var (sim, env) = Ready(DwellKind.ChatOutsideVenue, 2, percentage: oldRate);
        // The second citizen is still en route when the first visit is saved.
        sim.State.Citizens[1].Journey!.Legs[0].Minutes = 20;
        sim.State.Citizens[1].NextEventMinute = 1460;
        sim = sim.RebuildRoutes(new Walking()); sim.AdvanceTo(1450);
        Assert.Equal(joins, sim.State.Citizens[0].OptionalStopChoice);
        var read = SaveCodec.Read(SaveCodec.Write(sim.State)); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var loaded = new CitySimulator(read.State!, new Walking()); loaded.AttachOptionalStops(env, 100);
        loaded.AdvanceTo(1460);
        Assert.Equal(joins, loaded.State.Citizens[0].OptionalStopChoice);
        Assert.All(loaded.State.Citizens, c => Assert.Equal(joins, c.OptionalStop != null));
        Assert.True(loaded.State.Citizens[1].OptionalStopChoice);
    }

    [Fact]
    public void Schema15MigrationPreservesStopsButDoesNotInventWaitingChoices()
    {
        var (sim, env) = Ready(DwellKind.Resting); sim.AdvanceTo(1450);
        var wire = LegacyMoneyWire.AsSchema(JObject.Parse(LegacyWire.Write(sim.State)), 15);
        var read = SaveCodec.Read(wire.ToString()); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Null(read.State!.Citizens[0].OptionalStopChoice);
        var loaded = new CitySimulator(read.State, new Walking()); loaded.AttachOptionalStops(env, 0);
        Assert.Equal(sim.State.Citizens[0].OptionalStop!.EndMinute, loaded.State.Citizens[0].OptionalStop!.EndMinute);
        wire["Citizens"]![0]!["OptionalStopChoice"] = true;
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(wire.ToString()).Status);
        wire["Schema"] = 16; ((JObject)wire["Citizens"]![0]!).Remove("OptionalStopChoice");
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(wire.ToString()).Status);
    }

    [Theory]
    [InlineData(DwellKind.Break, 5, 10)] [InlineData(DwellKind.WindowShopping, 1, 3)]
    [InlineData(DwellKind.Resting, 10, 40)] [InlineData(DwellKind.Audience, 2, 8)]
    public void ArrivalSchedulesBoundedSavedStopWithoutChangingContinuation(DwellKind kind, double min, double max)
    {
        var (sim, _) = Ready(kind); sim.AdvanceTo(1450);
        var c = sim.State.Citizens.Single(); var stop = Assert.IsType<OptionalStopState>(c.OptionalStop);
        Assert.Equal(kind, stop.Kind); Assert.InRange(stop.EndMinute - stop.StartMinute, min, max);
        Assert.Equal(1570, c.NextEventMinute); Assert.Equal(1570, c.ActivityUntilMinute);
        Assert.Equal(1, c.CompletedJourneys); Assert.Single(c.RecentVisits); Assert.Null(c.Journey);
        if (kind == DwellKind.Break) Assert.Equal(1510, stop.StartMinute);
        else Assert.Equal(1450, stop.StartMinute);
        Assert.Equal(1450, stop.VisitMinute); Assert.Equal(c.NextEventMinute, stop.ResumeEventMinute);
        string json = SaveCodec.Write(sim.State); var read = SaveCodec.Read(json);
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error); Assert.Equal(json, SaveCodec.Write(read.State!));
        sim.AdvanceTo(stop.EndMinute); Assert.False(stop.ActiveAt(sim.State.Minute));
        Assert.Equal(1570, c.NextEventMinute);
    }
    [Theory]
    [InlineData(DwellKind.Break)] [InlineData(DwellKind.WindowShopping)] [InlineData(DwellKind.Resting)] [InlineData(DwellKind.Audience)]
    public void AnyRainRefusesAndPermanentlyCancelsWithoutRestartOrIdentityChange(DwellKind kind)
    {
        var (wet, wetEnv) = Ready(kind); wetEnv.Raining = true; wet.AdvanceTo(1450);
        Assert.Null(wet.State.Citizens[0].OptionalStop);
        var (sim, env) = Ready(kind); sim.AdvanceTo(1450);
        var c = sim.State.Citizens[0]; var identity = c.Identity; var stop = c.OptionalStop!; double end = stop.EndMinute;
        env.Raining = true; sim.RefreshOptionalStops(); Assert.True(stop.Cancelled);
        env.Raining = false; sim.RefreshOptionalStops(); sim.AdvanceTo(1451);
        Assert.True(c.OptionalStop!.Cancelled); Assert.Equal(end, c.OptionalStop.EndMinute); Assert.Same(identity, c.Identity);
        Assert.Equal(1570, c.NextEventMinute); Assert.Equal(1, c.CompletedJourneys);
        var read = SaveCodec.Read(SaveCodec.Write(sim.State)); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var loaded = new CitySimulator(read.State!, new Walking()); loaded.AttachOptionalStops(env, 100);
        Assert.True(loaded.State.Citizens[0].OptionalStop!.Cancelled);
    }
    [Theory]
    [InlineData(DwellKind.Break, 4)] [InlineData(DwellKind.WindowShopping, 0.5)]
    [InlineData(DwellKind.Resting, 9)] [InlineData(DwellKind.Audience, 1)]
    public void InsufficientTimeNeverCreatesUndersizedStop(DwellKind kind, double stay)
    {
        var (sim, _) = Ready(kind, stay: stay); sim.AdvanceTo(1450); Assert.Null(sim.State.Citizens[0].OptionalStop);
    }
    [Fact]
    public void MissingSpotAndPerformerLossEndRatherThanReroll()
    {
        var (sim, env) = Ready(DwellKind.Audience); sim.AdvanceTo(1450); var stop = sim.State.Citizens[0].OptionalStop!;
        env.Present = false; sim.RefreshOptionalStops(); Assert.True(stop.Cancelled);
        env.Present = true; sim.RefreshOptionalStops(); Assert.True(stop.Cancelled);
        var (absent, noSpots) = Ready(DwellKind.Resting); noSpots.Present = false; absent.AdvanceTo(1450);
        Assert.Null(absent.State.Citizens[0].OptionalStop);
    }
    [Fact]
    public void ConversationsRequireTwoEligiblePersistentVisitorsAndDistinctSpots()
    {
        var (alone, _) = Ready(DwellKind.ChatOutsideVenue); alone.AdvanceTo(1450); Assert.Null(alone.State.Citizens[0].OptionalStop);
        var (sim, env) = Ready(DwellKind.ChatOutsideVenue, 3); sim.AdvanceTo(1450);
        var group = sim.State.Citizens.Where(c => c.OptionalStop != null).ToArray(); Assert.Equal(2, group.Length);
        var a = group[0].OptionalStop!; var b = group[1].OptionalStop!;
        Assert.Equal(a.Members, b.Members); Assert.Equal(2, a.Members.Count); Assert.Equal(a.TargetId, b.TargetId);
        Assert.Equal(a.EndMinute, b.EndMinute); Assert.Equal(a.StartMinute, b.StartMinute);
        Assert.InRange(a.EndMinute - a.StartMinute, 10, 30); Assert.NotEqual(a.SpotId, b.SpotId);
        Assert.Equal(a.Position, b.Facing); Assert.Equal(b.Position, a.Facing);
        var read = SaveCodec.Read(SaveCodec.Write(sim.State)); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        env.Raining = true; sim.RefreshOptionalStops(); Assert.All(group, c => Assert.True(c.OptionalStop!.Cancelled));
    }
    [Fact]
    public void NoSingletonIsPublishedWhenThereIsOnlyOneValidatedSpot()
    {
        var (sim, env) = Ready(DwellKind.ChatOutsideVenue, 2); env.Capacity = 1; sim.AdvanceTo(1450);
        Assert.All(sim.State.Citizens, c => Assert.Null(c.OptionalStop));
    }
    [Fact]
    public void AudienceRequiresPassingWalkerAndChoiceNotAnAnonymousPopulationTarget()
    {
        var (declined, _) = Ready(DwellKind.Audience, percentage: 0); declined.AdvanceTo(1450);
        Assert.Null(declined.State.Citizens[0].OptionalStop);
        var (transit, _) = Ready(DwellKind.Audience, arrivalMode: TravelMode.Bus); transit.AdvanceTo(1450);
        Assert.Null(transit.State.Citizens[0].OptionalStop);
        var (sim, _) = Ready(DwellKind.Audience, 3); var ids = sim.State.Citizens.Select(c => c.Identity.Id).ToArray(); sim.AdvanceTo(1450);
        Assert.Equal(ids, sim.State.Citizens.Select(c => c.Identity.Id)); Assert.Equal(3, sim.State.Citizens.Count);
        Assert.All(sim.State.Citizens, c => Assert.Equal(DwellKind.Audience, c.OptionalStop!.Kind));
        Assert.Equal(3, sim.State.Citizens.Select(c => c.OptionalStop!.SpotId).Distinct().Count());
    }
    [Fact]
    public void LegacyMigrationAddsNoStopsAndDoesNotInventVisitsOnLoad()
    {
        var (sim, env) = Ready(DwellKind.WindowShopping, percentage: 0); sim.AdvanceTo(1450);
        foreach (int schema in new[] {13,14})
        {
            var wire = LegacyMoneyWire.AsSchema(JObject.Parse(LegacyWire.Write(sim.State)), schema);
            string source = wire.ToString(); var read = SaveCodec.Read(source);
            Assert.True(read.Status == SaveReadStatus.Loaded, read.Error); Assert.Equal(schema, read.SourceSchema);
            Assert.Equal(CityState.CurrentSchema, read.State!.Schema); Assert.Null(read.State.Citizens[0].OptionalStop);
            var loaded = new CitySimulator(read.State, new Walking()); loaded.AttachOptionalStops(env,100); loaded.AdvanceTo(1451);
            Assert.Null(loaded.State.Citizens[0].OptionalStop); Assert.Single(loaded.State.Citizens[0].RecentVisits);
            Assert.Equal(source, wire.ToString());
            wire["Citizens"]![0]!["OptionalStop"] = null;
            Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(wire.ToString()).Status);
        }
    }
    [Fact]
    public void StrictWireRejectsMissingFieldsUnknownKindsAndImpossibleContinuation()
    {
        var (sim, _) = Ready(DwellKind.Resting); sim.AdvanceTo(1450); string source = SaveCodec.Write(sim.State);
        foreach (Action<JObject> damage in new Action<JObject>[] {
            j => ((JObject)j["Citizens"]![0]!).Remove("OptionalStop"),
            j => j["Citizens"]![0]!["OptionalStop"]!["Kind"] = (int)DwellKind.WaitForBus,
            j => j["Citizens"]![0]!["OptionalStop"]!["EndMinute"] = 1600,
            j => j["Citizens"]![0]!["OptionalStop"]!["ResumeEventMinute"] = 1600,
            j => j["Citizens"]![0]!["OptionalStop"]!["Members"] = new JArray("anonymous") })
        { var wire = JObject.Parse(source); damage(wire); Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(wire.ToString()).Status); }
        var future = JObject.Parse(source); future["Schema"] = CityState.CurrentSchema + 1; Assert.Equal(SaveReadStatus.Future, SaveCodec.Read(future.ToString()).Status);
    }
    [Theory]
    [InlineData(DwellKind.Break)] [InlineData(DwellKind.WindowShopping)] [InlineData(DwellKind.Resting)]
    [InlineData(DwellKind.Audience)] [InlineData(DwellKind.ChatOutsideVenue)]
    public void EventBudgetReloadAndRouteRebuildPreserveDeadlinesAndMembership(DwellKind kind)
    {
        var (whole, _) = Ready(kind, 4); whole.AdvanceTo(1451);
        var (sliced, env) = Ready(kind, 4); while (!sliced.AdvanceTo(1450, 1).CaughtUp) { }
        var read = SaveCodec.Read(SaveCodec.Write(sliced.State)); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var loaded = new CitySimulator(read.State!, new Walking()); loaded.AttachOptionalStops(env, 100);
        loaded = loaded.RebuildRoutes(new Walking()); loaded.AdvanceTo(1451);
        Assert.Equal(SaveCodec.Write(whole.State), SaveCodec.Write(loaded.State));
    }
    [Fact]
    public void NightStopsReuseExistingVisitsWithoutAddingNightOutings()
    {
        var (sim, _) = Ready(DwellKind.ChatOutsideVenue, 2, minute: 2880 + 3 * 60);
        sim.AdvanceTo(3070); Assert.Equal(2, sim.State.Citizens.Count);
        Assert.All(sim.State.Citizens, c => { Assert.Equal(1,c.CompletedJourneys); Assert.Single(c.RecentVisits); Assert.NotNull(c.OptionalStop); });
    }
    [Fact]
    public void UndecidedFrequencyDoesNotInventAnAutomaticRateButRetainsSavedStops()
    {
        var (sim, env) = Ready(DwellKind.WindowShopping); sim.AttachOptionalStops(env, null); sim.AdvanceTo(1450);
        Assert.Null(sim.State.Citizens[0].OptionalStop);
        var (active, _) = Ready(DwellKind.WindowShopping); active.AdvanceTo(1450);
        var read = SaveCodec.Read(SaveCodec.Write(active.State)); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var loaded = new CitySimulator(read.State!, new Walking()); loaded.AttachOptionalStops(env, null);
        Assert.Equal(active.State.Citizens[0].OptionalStop!.EndMinute, loaded.State.Citizens[0].OptionalStop!.EndMinute);
        Assert.False(loaded.State.Citizens[0].OptionalStop!.Cancelled);
    }
    [Fact]
    public void CancellingOnePartnerCancelsWholeConversationBeforeImmediateSave()
    {
        var (sim, _) = Ready(DwellKind.ChatOutsideVenue, 2); sim.AdvanceTo(1450);
        sim.CancelOptionalStop(sim.State.Citizens[0].Identity.Id);
        Assert.All(sim.State.Citizens, c => Assert.True(c.OptionalStop!.Cancelled));
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(SaveCodec.Write(sim.State)).Status);
    }
    [Fact]
    public void OrphanedActiveConversationCannotLoad()
    {
        var (sim, _) = Ready(DwellKind.ChatOutsideVenue, 2); sim.AdvanceTo(1450);
        var wire = JObject.Parse(LegacyWire.Write(sim.State)); wire["Citizens"]![0]!["OptionalStop"] = null;
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(wire.ToString()).Status);
    }
    [Fact]
    public void ExistingStopResumesOriginalActivityAndNewJourneyWithoutReplayingVisit()
    {
        var (sim, env) = Ready(DwellKind.WindowShopping); sim.AdvanceTo(1450);
        var c = sim.State.Citizens[0]; double end = c.OptionalStop!.EndMinute;
        sim.AdvanceTo(end + 1); Assert.Equal(CitizenPhase.AtPlace, c.Phase); Assert.Single(c.RecentVisits);
        var loaded = new CitySimulator(SaveCodec.Read(SaveCodec.Write(sim.State)).State!, new Walking()); loaded.AttachOptionalStops(env,100);
        sim.AdvanceTo(1580); loaded.AdvanceTo(1580);
        Assert.Equal(SaveCodec.Write(sim.State), SaveCodec.Write(loaded.State));
        Assert.Null(c.OptionalStop); Assert.Equal(2,c.CompletedJourneys); Assert.Equal(Activity.Home,c.Activity);
    }
    [Fact]
    public void ExactMinimumAndMaximumDurationsAreReachableAndAlwaysBounded()
    {
        foreach (var kind in new[] {DwellKind.Break,DwellKind.WindowShopping,DwellKind.Resting,DwellKind.ChatOutsideVenue,DwellKind.Audience})
        {
            double min=OptionalStopPolicy.Minimum(kind), max=DwellRules.For(kind).MaximumMinutes;
            var durations = Enumerable.Range(0,100000).Select(i => OptionalStopPolicy.Duration("c"+i,kind,17)).ToArray();
            Assert.All(durations, d => Assert.InRange(d,min,max)); Assert.Equal(min,durations.Min()); Assert.Equal(max,durations.Max());
        }
    }

    [Fact]
    public void ClosedVisitOrMealOutingCannotStartAnOptionalStop()
    {
        var (closed, _) = Ready(DwellKind.WindowShopping); closed.State.Places.Single(p=>p.Id=="venue").CloseMinute=0;
        closed.AdvanceTo(1450); Assert.Null(closed.State.Citizens[0].OptionalStop);
        var (meal, _) = Ready(DwellKind.WindowShopping); var c=meal.State.Citizens[0];
        c.MealOuting=new MealOuting {OriginId="home",OriginActivity=Activity.Home,ResumeUntil=1700,ResumeEvent=1700};
        meal.AdvanceTo(1450); Assert.Null(c.OptionalStop); Assert.NotNull(c.MealOuting);
    }
    [Fact]
    public void ClosureCancelsAnAlreadyPlannedBreakAndGroupPartnerLossCancelsConversation()
    {
        var (work, _) = Ready(DwellKind.Break); work.AdvanceTo(1450);
        work.State.Places.Single(p=>p.Id=="venue").CloseMinute=0; work.RefreshOptionalStops();
        Assert.True(work.State.Citizens[0].OptionalStop!.Cancelled);
        var (group, _) = Ready(DwellKind.ChatOutsideVenue,2); group.AdvanceTo(1450);
        group.State.Citizens[0].OptionalStop=null; group.RefreshOptionalStops();
        Assert.True(group.State.Citizens[1].OptionalStop!.Cancelled);
    }

    [Fact]
    public void ConversationSkipsShortStayPartnerAndUsesLaterFeasibleVisitor()
    {
        var (sim, env) = Ready(DwellKind.ChatOutsideVenue, 3);
        env.Present = false; sim.AdvanceTo(1450);
        var shortStay = sim.State.Citizens[0];
        shortStay.ActivityUntilMinute = shortStay.NextEventMinute = 1456;
        // First two visitors already accepted, but geometry was not available at admission.
        var arriving = sim.State.Citizens[2];
        arriving.Phase = CitizenPhase.Travelling; arriving.PlaceId = "home";
        arriving.DestinationId = "venue"; arriving.DestinationActivity = Activity.Leisure;
        arriving.DepartureMinute = 1450; arriving.NextEventMinute = 1451;
        arriving.Journey = new Walking().Plan(sim.State.Places[0], sim.State.Places[1], 1450, arriving.Preferences);
        arriving.Journey.Legs[0].Minutes = 1;
        sim = sim.RebuildRoutes(new Walking()); env.Present = true; sim.AdvanceTo(1451);
        Assert.Null(shortStay.OptionalStop);
        Assert.Equal(new[] { "c1", "c2" }, sim.State.Citizens.Where(c => c.OptionalStop != null).Select(c => c.Identity.Id));
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(SaveCodec.Write(sim.State)).Status);
    }

    [Fact]
    public void InvalidGeometryIsSkippedAndIndividualsCannotShareTheOnlyValidSlot()
    {
        var (sim, env) = Ready(DwellKind.WindowShopping, 2);
        env.Supplied = new[] {
            new OptionalStopSpot { Id="a", TargetId="", Position=new(10,0,1) },
            new OptionalStopSpot { Id="b", TargetId="target", Position=new(double.NaN,0,1) },
            new OptionalStopSpot { Id="c", TargetId="target", Position=new(10,0,1), Facing=new(double.PositiveInfinity,0,0) },
            new OptionalStopSpot { Id="d", TargetId="target", Position=new(1000,0,1) },
            new OptionalStopSpot { Id="e", TargetId="target", Position=new(10,0,1), Facing=new(10,0,3) }
        };
        sim.AdvanceTo(1450);
        var stop = Assert.Single(sim.State.Citizens, c => c.OptionalStop != null).OptionalStop!;
        Assert.Equal("e", stop.SpotId);
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(SaveCodec.Write(sim.State)).Status);
    }

    [Fact]
    public void IndexedStopValidationStillRejectsDuplicateAndMissingReferences()
    {
        var (sim, _) = Ready(DwellKind.ChatOutsideVenue, 2); sim.AdvanceTo(1450);
        var wire = JObject.Parse(LegacyWire.Write(sim.State));
        wire["Citizens"]![1]!["Identity"]!["Id"] = "c0";
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(wire.ToString()).Status);
        wire = JObject.Parse(LegacyWire.Write(sim.State));
        wire["Citizens"]![0]!["OptionalStop"]!["PlaceId"] = "missing";
        Assert.Equal(SaveReadStatus.Invalid, SaveCodec.Read(wire.ToString()).Status);
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(0, false)]
    public void SameVisitEventAndReloadPreserveWaitingChoiceBeforePartnerArrives(int rate, bool chosen)
    {
        var (sim, env) = Ready(DwellKind.ChatOutsideVenue, 2, percentage: rate);
        sim.State.Citizens[1].Journey!.Legs[0].Minutes = 20;
        sim.State.Citizens[1].NextEventMinute = 1460;
        sim = sim.RebuildRoutes(new Walking()); sim.AdvanceTo(1450);
        var first = sim.State.Citizens[0];
        Assert.Equal(chosen, first.OptionalStopChoice);
        // A lifecycle/routine wakeup keeps the admitted activity and its original deadline.
        first.NextEventMinute = 1451; sim = sim.RebuildRoutes(new Walking());
        sim.AttachOptionalStops(env, 100-rate); sim.AdvanceTo(1451);
        Assert.Equal(chosen, first.OptionalStopChoice);
        Assert.Single(first.RecentVisits); Assert.Equal(1570, first.ActivityUntilMinute);
        var read = SaveCodec.Read(SaveCodec.Write(sim.State)); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        sim = new CitySimulator(read.State!, new Walking()); sim.AttachOptionalStops(env, 100);
        sim.AdvanceTo(1460);
        Assert.Equal(chosen, sim.State.Citizens[0].OptionalStopChoice);
        Assert.All(sim.State.Citizens, c => Assert.Equal(chosen, c.OptionalStop != null));
    }

    [Theory]
    [InlineData(1451)] // an event interrupts an active pair
    [InlineData(1490)] // both chats have naturally expired
    public void UsedConversationOpportunityCannotBeRecruitedAgainAfterSameVisitEvent(double eventMinute)
    {
        var (sim, env) = Ready(DwellKind.ChatOutsideVenue, 4);
        for (int i=2; i<4; i++)
        {
            var c=sim.State.Citizens[i];
            c.Journey!.Legs[0].Minutes=60+(i-2)*10;
            c.NextEventMinute=1500+(i-2)*10;
        }
        sim=sim.RebuildRoutes(new Walking()); sim.AdvanceTo(1450);
        var first=sim.State.Citizens[0]; var partner=sim.State.Citizens[1];
        var original=first.OptionalStop!.Copy();
        first.NextEventMinute=eventMinute;
        sim=sim.RebuildRoutes(new Walking()); sim.AdvanceTo(eventMinute);
        Assert.True(first.OptionalStop!.Cancelled);
        Assert.True(partner.OptionalStop!.Cancelled);
        Assert.True(first.OptionalStopChoice); Assert.True(partner.OptionalStopChoice);
        Assert.Equal(original.StartMinute,first.OptionalStop.StartMinute);
        Assert.Equal(original.EndMinute,first.OptionalStop.EndMinute);
        Assert.Equal(original.VisitMinute,first.OptionalStop.VisitMinute);
        string saved=SaveCodec.Write(sim.State);
        var read=SaveCodec.Read(saved); Assert.True(read.Status==SaveReadStatus.Loaded,read.Error);
        Assert.Equal(saved,SaveCodec.Write(read.State!));
        sim=new CitySimulator(read.State!,new Walking()); sim.AttachOptionalStops(env,100);
        sim.AdvanceTo(1500);
        Assert.Null(sim.State.Citizens[2].OptionalStop); // neither old partner is reusable
        sim.AdvanceTo(1510);
        Assert.All(sim.State.Citizens.Take(2),c=> { Assert.True(c.OptionalStop!.Cancelled); Assert.Single(c.RecentVisits); });
        Assert.All(sim.State.Citizens.Skip(2),c=>Assert.False(c.OptionalStop!.Cancelled));
        Assert.Equal(new[]{"c2","c3"},sim.State.Citizens[2].OptionalStop!.Members.OrderBy(id=>id));
        Assert.Equal(SaveReadStatus.Loaded,SaveCodec.Read(SaveCodec.Write(sim.State)).Status);
        sim.AdvanceTo(1580);
        Assert.All(sim.State.Citizens.Take(2),c=>Assert.Null(c.OptionalStop)); // departure clears evidence
    }

}
