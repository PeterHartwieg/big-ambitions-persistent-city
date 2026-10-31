using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>Route answers that arrive frames later must produce the same city as synchronous answers.
/// The planner doubles share one pure route function, so every difference comes from the event machinery.</summary>
public sealed class ResumablePlanningTests
{
    private static Position Shift(Position p, double metres) => new(p.X + metres, p.Y, p.Z);
    private static void Add(Route route, Position from, Position to, TravelMode mode, double metresPerMinute)
    {
        double metres = Position.Distance(from, to);
        if (metres > 0.000001) route.Legs.Add(new RouteLeg { From = from, To = to, Mode = mode, Minutes = metres / metresPerMinute });
    }

    // Straight-line walking, a car trip when the resident owns a parked car and the trip is long, a home car
    // allocated on the first plan from home (as the native planner does) and a deterministic refusal so the
    // waiting and retry paths run. Pure in the request, so sync and deferred answers are the same.
    private static Route? Answer(RouteRequest request)
    {
        uint draw = StableRandom.Hash(request.From.Id + ">" + request.To.Id + "@" + request.DepartureMinute.ToString("R", CultureInfo.InvariantCulture), 3);
        if (draw % 11 == 0) return null;
        var citizen = request.Citizen;
        if (citizen != null && request.Preferences.OwnsCar)
        {
            if (citizen.Car == null && citizen.Phase == CitizenPhase.AtPlace && citizen.PlaceId == citizen.HomeId && request.From.Id == citizen.HomeId)
                citizen.Car = new CarState { Id = citizen.Identity.Id + ":car", ParkedPosition = Shift(request.From.Position, 4) };
            if (citizen.Car != null && Position.Distance(request.From.Position, request.To.Position) > 150)
            {
                var drive = new Route();
                Add(drive, request.From.Position, citizen.Car.ParkedPosition, TravelMode.Walk, 80);
                var curb = Shift(request.To.Position, 3);
                Add(drive, citizen.Car.ParkedPosition, curb, TravelMode.Car, 500);
                Add(drive, curb, request.To.Position, TravelMode.Walk, 80);
                return drive.Legs.Count == 0 ? null : drive;
            }
        }
        var walk = new Route();
        Add(walk, request.From.Position, request.To.Position, TravelMode.Walk, 80);
        return walk.Legs.Count == 0 ? null : walk;
    }

    private sealed class SyncPlanner : ICitizenRoutePlanner, ICommuteTimingPlanner
    {
        public int Calls;
        public Func<int, bool>? ThrowAt;
        private Route? Serve(RouteRequest request)
        {
            int call = Calls++;
            if (ThrowAt?.Invoke(call) == true) throw new InvalidOperationException("planner failure");
            return Answer(request);
        }
        public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences) => Serve(new RouteRequest(from, to, minute, null, preferences));
        public Route? Plan(Place from, Place to, double minute, CitizenState citizen) => Serve(new RouteRequest(from, to, minute, citizen, citizen.Preferences));
    }

    private sealed class DeferredPlanner : IResumableRoutePlanner, ICommuteTimingPlanner
    {
        public Func<int, int> ContinuesFor = _ => 1;
        public Func<int, int, bool>? ThrowAt;
        // (request, request index, continue index): lose the captured snapshot here. As in the runtime job, only
        // a job resumed after an earlier sliced Continue can throw it; a job run inline from its start cannot.
        public Func<RouteRequest, int, int, bool>? RestartAt;
        public int Requests, Disposed, Continues, RestartsThrown;
        public readonly List<RouteRequest> Seen = new();
        public IRoutePlanningJob Begin(RouteRequest request)
        {
            Seen.Add(request);
            return new Job(this, request, Requests++, ContinuesFor(Requests - 1));
        }
        public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences)
            => throw new InvalidOperationException("A resumable planner answers through jobs.");
        private sealed class Job : IRoutePlanningJob
        {
            private readonly DeferredPlanner owner; private readonly RouteRequest request; private readonly int index;
            private int remaining, continues; private bool disposed, sliced;
            public Job(DeferredPlanner owner, RouteRequest request, int index, int continues)
            { this.owner = owner; this.request = request; this.index = index; remaining = Math.Max(1, continues); }
            public Route? Result { get; private set; }
            public bool Continue(double budgetMilliseconds)
            {
                Assert.False(disposed);
                owner.Continues++;
                if (owner.ThrowAt?.Invoke(index, continues) == true) throw new InvalidOperationException("planner failure");
                // Like the runtime job, a sliced job may lose its captured state before any slice, including the first.
                if ((sliced || !double.IsPositiveInfinity(budgetMilliseconds)) && owner.RestartAt?.Invoke(request, index, continues) == true)
                { owner.RestartsThrown++; throw new RoutePlanningRestartRequired("road snapshot replaced"); }
                if (!double.IsPositiveInfinity(budgetMilliseconds)) sliced = true;
                continues++;
                if (--remaining > 0) return false;
                Result = Answer(request);
                return true;
            }
            public void Dispose() { if (!disposed) owner.Disposed++; disposed = true; }
        }
    }

    private static CityState City(int count, int seed = 11)
    {
        var identities = Enumerable.Range(0, count).Select(i => new CitizenIdentity
        {
            Id = "rlo:test:c" + i.ToString("D3"), Name = "Citizen " + i, Age = 20 + i % 50, District = i % 2 == 0 ? "a" : "b",
            Persona = (Persona)(i % 8), AppearanceSeed = i + 1
        });
        var places = new[]
        {
            new Place { Id = "home-a", Name = "Home A", District = "a", Kind = PlaceKind.Home, Capacity = count, Position = new(0, 0, 0) },
            new Place { Id = "home-b", Name = "Home B", District = "b", Kind = PlaceKind.Home, Capacity = count, Position = new(900, 0, 0) },
            new Place { Id = "work-1", Name = "Work 1", District = "a", Kind = PlaceKind.Work, Capacity = count / 3 + 1, Position = new(300, 0, 400) },
            new Place { Id = "work-2", Name = "Work 2", District = "b", Kind = PlaceKind.Work, Capacity = count / 3 + 1, Position = new(1500, 0, -200) },
            new Place { Id = "work-3", Name = "Work 3", District = "a", Kind = PlaceKind.Work, Capacity = count / 3 + 1, Position = new(-600, 0, 900) },
            new Place { Id = "school", Name = "School", District = "a", Kind = PlaceKind.Study, Capacity = count, Position = new(200, 0, -700) },
            new Place { Id = "shop-1", Name = "Shop 1", District = "a", Kind = PlaceKind.Shop, Position = new(150, 0, 120), OpenMinute = 480, CloseMinute = 1200 },
            new Place { Id = "shop-2", Name = "Shop 2", District = "b", Kind = PlaceKind.Shop, Position = new(1000, 0, 300), OpenMinute = 540, CloseMinute = 1260 },
            new Place { Id = "shop-3", Name = "Shop 3", District = "a", Kind = PlaceKind.Shop, Position = new(-400, 0, -300), OpenMinute = 600, CloseMinute = 1080 },
            new Place { Id = "leisure-1", Name = "Leisure 1", District = "a", Kind = PlaceKind.Leisure, Position = new(60, 0, 250), OpenMinute = 600, CloseMinute = 1380 },
            new Place { Id = "leisure-2", Name = "Leisure 2", District = "b", Kind = PlaceKind.Leisure, Position = new(1200, 0, 600), OpenMinute = 660, CloseMinute = 1320 },
            new Place { Id = "leisure-3", Name = "Leisure 3", District = "a", Kind = PlaceKind.Leisure, Position = new(-200, 0, 800), OpenMinute = 720, CloseMinute = 1440 }
        };
        var city = Population.Create(identities, places, "resumable-test", "0.40.4", seed, 1440);
        // Some workers start unemployed so the daily employment check probes workplaces during the run.
        for (int i = 0; i < city.Citizens.Count; i += 5) city.Citizens[i].DayPlaceId = "";
        return city;
    }

    // Native frames: one event per slice, and a pending plan gets one Continue per frame.
    private static void RunFrames(CitySimulator city, double target, Action<CitySimulator>? whilePending = null)
    {
        int guard = 0;
        while (true)
        {
            Assert.True(++guard < 10_000_000, "frame loop did not finish");
            if (city.PlanningPending)
            {
                Assert.False(city.IsCaughtUpTo(target));
                whilePending?.Invoke(city);
                city.ContinuePlanning(0);
                continue;
            }
            var result = city.AdvanceFrameSlice(target, 1);
            if (result.PlanningPending) { Assert.False(result.CaughtUp); Assert.True(city.PlanningPending); continue; }
            if (result.CaughtUp) break;
        }
        city.SynchronizeSnapshot();
        Assert.True(city.IsCaughtUpTo(target));
    }

    private static double Target(int day) => 1440 * (day + 1) + 37;

    // A fixed meal outing at the same simulated moment in every run, to exercise the preference-only return request.
    private static bool BeginLunch(CitySimulator city)
    {
        double now = city.State.Minute;
        var shop = city.State.Places.Single(p => p.Id == "shop-1");
        foreach (var person in city.State.Citizens)
        {
            if (person.Phase != CitizenPhase.AtPlace || person.MealOuting != null || person.Activity == Activity.Shop
                || Math.Min(person.NextEventMinute, person.ActivityUntilMinute) < now + 120) continue;
            var origin = city.State.Places.Single(p => p.Id == person.PlaceId);
            var outward = new Route();
            Add(outward, origin.Position, shop.Position, TravelMode.Walk, 80);
            if (outward.Legs.Count == 0 || outward.Minutes > 40) continue;
            if (city.TryBeginMealVisit(person.Identity.Id, shop.Id, outward, outward.Minutes, 20, now + 100)) return true;
        }
        return false;
    }

    private static bool IsProbe(CityState state, RouteRequest request)
        => request.Citizen != null && !state.Citizens.Any(c => ReferenceEquals(c.Identity, request.Citizen.Identity));

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public void DeferredAnswersProduceTheSynchronousCity(int continues)
    {
        var reference = new CitySimulator(City(48), new SyncPlanner());
        var planner = new DeferredPlanner { ContinuesFor = index => 1 + index % continues };
        var deferred = new CitySimulator(City(48), planner);
        var inline = new CitySimulator(City(48), new DeferredPlanner { ContinuesFor = index => 1 + index % continues });
        for (int day = 1; day <= 6; day++)
        {
            double target = Target(day);
            reference.AdvanceTo(target); RunFrames(deferred, target); inline.AdvanceTo(target);
            Assert.False(deferred.PlanningPending);
            string expected = SaveCodec.Write(reference.State);
            Assert.Equal(expected, SaveCodec.Write(deferred.State));
            Assert.Equal(expected, SaveCodec.Write(inline.State));
            if (day == 2)
            {
                double lunch = target + 600 - 37;
                reference.AdvanceTo(lunch); RunFrames(deferred, lunch); inline.AdvanceTo(lunch);
                bool began = BeginLunch(reference);
                Assert.Equal(began, BeginLunch(deferred)); Assert.Equal(began, BeginLunch(inline));
                Assert.True(began, "no resident could start the fixed meal outing");
            }
        }
        Assert.True(planner.Requests > 200, "too few requests to compare: " + planner.Requests);
        Assert.Equal(planner.Requests, planner.Disposed);
        Assert.Contains(planner.Seen, r => IsProbe(deferred.State, r));
        Assert.Contains(planner.Seen, r => r.To.Kind == PlaceKind.Shop || r.To.Kind == PlaceKind.Leisure);
        Assert.Contains(planner.Seen, r => r.Citizen == null);
        Assert.True(reference.State.Citizens.Sum(c => c.CompletedJourneys) > 200);
        Assert.Contains(reference.State.Citizens, c => c.Car != null && c.CompletedJourneys > 0 && c.Preferences.OwnsCar);
    }

    [Fact]
    public void SaveDuringAPendingRequestStoresTheUnstartedEventAndReplaysIt()
    {
        var reference = new CitySimulator(City(48), new SyncPlanner());
        var planner = new DeferredPlanner { ContinuesFor = index => 1 + index % 3 };
        var city = new CitySimulator(City(48), planner);
        int saves = 0, pendingSeen = 0;
        for (int day = 1; day <= 5; day++)
        {
            double target = Target(day);
            reference.AdvanceTo(target);
            int guard = 0;
            while (true)
            {
                Assert.True(++guard < 10_000_000);
                if (city.PlanningPending)
                {
                    pendingSeen++;
                    var request = planner.Seen[planner.Seen.Count - 1];
                    var live = city.State.Citizens.Single(c => c.Identity.Id == request.Citizen!.Identity.Id);
                    // The published state still holds the unstarted event at the held clock.
                    Assert.Equal(city.State.Minute, live.NextEventMinute);
                    Assert.False(city.IsCaughtUpTo(city.State.Minute));
                    if (pendingSeen % 7 == 0)
                    {
                        string saved = SaveCodec.Write(city.State);
                        var read = SaveCodec.Read(saved);
                        Assert.Equal(SaveReadStatus.Loaded, read.Status);
                        Assert.Equal(saved, SaveCodec.Write(read.State!));
                        // The old simulator is dropped with its job; the reload runs the event again from the start.
                        city = new CitySimulator(read.State!, planner);
                        Assert.False(city.PlanningPending);
                        saves++;
                        continue;
                    }
                    city.ContinuePlanning(0);
                    continue;
                }
                var result = city.AdvanceFrameSlice(target, 1);
                if (result.CaughtUp) break;
            }
            city.SynchronizeSnapshot();
            Assert.Equal(SaveCodec.Write(reference.State), SaveCodec.Write(city.State));
        }
        Assert.True(saves > 30, "too few mid-request saves: " + saves);
    }

    [Fact]
    public void PendingRequestHoldsTheClockAndRefusesCompetingUses()
    {
        var planner = new DeferredPlanner { ContinuesFor = _ => 3 };
        var city = new CitySimulator(City(12), planner);
        AdvanceResult result;
        int guard = 0;
        do { Assert.True(++guard < 100000); result = city.AdvanceFrameSlice(Target(3), 1); } while (!result.PlanningPending && !result.CaughtUp);
        Assert.True(result.PlanningPending);
        Assert.False(city.IsCaughtUpTo(city.State.Minute));
        Assert.False(city.IsCaughtUpTo(Target(3)));
        double held = city.State.Minute;
        var request = planner.Seen[0];
        var live = city.State.Citizens.Single(c => c.Identity.Id == request.Citizen!.Identity.Id);
        string before = JsonConvert.SerializeObject(live);
        Assert.Equal(held, live.NextEventMinute);
        // Another frame slice does nothing while the answer is outstanding.
        var again = city.AdvanceFrameSlice(Target(3), 1);
        Assert.True(again.PlanningPending); Assert.Equal(0, again.Events); Assert.Equal(held, city.State.Minute);
        Assert.False(city.TryAcquirePhysicalJourney(live.Identity.Id, out _));
        Assert.False(city.TryBeginOptionalShopVisit(live.Identity.Id, "shop-1", new Route { Legs = { new RouteLeg { From = new(0, 0, 0), To = new(150, 0, 120), Minutes = 2, Mode = TravelMode.Walk } } }, 2, 10, held + 30));
        Assert.False(city.ContinuePlanning(0)); Assert.Equal(before, JsonConvert.SerializeObject(live)); Assert.Equal(held, city.State.Minute);
        Assert.False(city.ContinuePlanning(0)); Assert.Equal(before, JsonConvert.SerializeObject(live));
        // The third Continue answers; the event completes and schedules the next one.
        bool finished = city.ContinuePlanning(0);
        if (!finished) { while (city.PlanningPending) city.ContinuePlanning(0); }
        Assert.False(city.PlanningPending);
        Assert.True(live.NextEventMinute > held);
        Assert.Throws<ArgumentOutOfRangeException>(() => city.ContinuePlanning(double.NaN));
        Assert.True(city.ContinuePlanning(0));
    }

    [Fact]
    public void AdvanceToFinishesAPendingRequestInline()
    {
        var reference = new CitySimulator(City(24), new SyncPlanner());
        var planner = new DeferredPlanner { ContinuesFor = _ => 4 };
        var city = new CitySimulator(City(24), planner);
        AdvanceResult result;
        do { result = city.AdvanceFrameSlice(Target(2), 1); } while (!result.PlanningPending);
        reference.AdvanceTo(Target(2));
        var finished = city.AdvanceTo(Target(2));
        Assert.True(finished.CaughtUp); Assert.False(finished.PlanningPending); Assert.False(city.PlanningPending);
        Assert.Equal(SaveCodec.Write(reference.State), SaveCodec.Write(city.State));
    }

    [Fact]
    public void RebuildWhilePendingAbandonsTheJobAndReplaysTheEvent()
    {
        var reference = new CitySimulator(City(24), new SyncPlanner());
        var planner = new DeferredPlanner { ContinuesFor = _ => 2 };
        var city = new CitySimulator(City(24), planner);
        AdvanceResult result;
        do { result = city.AdvanceFrameSlice(Target(2), 1); } while (!result.PlanningPending);
        int disposedBefore = planner.Disposed;
        var rebuilt = city.RebuildRoutes(new DeferredPlanner { ContinuesFor = _ => 2 });
        Assert.Equal(disposedBefore + 1, planner.Disposed);
        Assert.False(rebuilt.PlanningPending);
        reference.AdvanceTo(Target(3)); RunFrames(rebuilt, Target(3));
        Assert.Equal(SaveCodec.Write(reference.State), SaveCodec.Write(rebuilt.State));
    }

    [Fact]
    public void PlannerFailureLeavesTheResidentUnstartedAndTheEventRetried()
    {
        var reference = new CitySimulator(City(48), new SyncPlanner());
        reference.AdvanceTo(Target(3));
        foreach (int failing in new[] { 0, 1 })
        {
            // Fail once at a chosen request and continue index; the city then recovers without any manual repair.
            var planner = new DeferredPlanner { ContinuesFor = index => 1 + index % 3 };
            bool thrown = false;
            planner.ThrowAt = (index, c) => index == 37 && c == failing && !thrown;
            var city = new CitySimulator(City(48), planner);
            string beforeFailure = "";
            int guard = 0;
            while (true)
            {
                Assert.True(++guard < 10_000_000);
                if (city.PlanningPending)
                {
                    try { city.ContinuePlanning(0); }
                    catch (InvalidOperationException error)
                    {
                        Assert.Equal("planner failure", error.Message); thrown = true;
                        Assert.False(city.PlanningPending);
                        var read = SaveCodec.Read(SaveCodec.Write(city.State));
                        Assert.Equal(SaveReadStatus.Loaded, read.Status);
                        var request = planner.Seen[planner.Seen.Count - 1];
                        var live = city.State.Citizens.Single(c => c.Identity.Id == request.Citizen!.Identity.Id);
                        Assert.Equal(city.State.Minute, live.NextEventMinute);
                        beforeFailure = SaveCodec.Write(city.State);
                    }
                    continue;
                }
                var result = city.AdvanceFrameSlice(Target(3), 1);
                if (result.CaughtUp) break;
            }
            Assert.True(thrown); Assert.NotEqual("", beforeFailure);
            city.SynchronizeSnapshot();
            Assert.Equal(SaveCodec.Write(reference.State), SaveCodec.Write(city.State));
        }
        // The same holds when a synchronous planner throws inside AdvanceTo.
        var sync = new SyncPlanner(); bool syncThrown = false;
        sync.ThrowAt = call => call == 53 && !syncThrown;
        var inline = new CitySimulator(City(48), sync);
        try { inline.AdvanceTo(Target(3)); }
        catch (InvalidOperationException) { syncThrown = true; }
        Assert.True(syncThrown);
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(SaveCodec.Write(inline.State)).Status);
        inline.AdvanceTo(Target(3));
        Assert.Equal(SaveCodec.Write(reference.State), SaveCodec.Write(inline.State));
    }

    [Fact]
    public void FrameCadenceDoesNotChangeDeparturesOrOrder()
    {
        // Two deferred runs with different interruption patterns and different frame interleavings agree
        // with each other and with every observed departure of the synchronous run.
        var departures = new List<string>();
        void Observe(List<string> into, CitySimulator city) => city.ActivityObserved += (id, citizen, minute) =>
            into.Add(id + ":" + minute.ToString("R", CultureInfo.InvariantCulture) + ":" + citizen.Phase + ":" + citizen.PlaceId + ":" + citizen.DestinationId);
        var reference = new CitySimulator(City(36), new SyncPlanner()); Observe(departures, reference);
        var slow = new List<string>(); var fast = new List<string>();
        // Snapshot restarts at pseudo-random requests and continue indices replay whole events; they must not
        // change any departure or its order either.
        static bool Draw(string what, int index, int seed) => StableRandom.Hash(what + ":" + index.ToString(CultureInfo.InvariantCulture), seed) % 6 == 0;
        var slowPlanner = new DeferredPlanner { ContinuesFor = index => 1 + index % 7, RestartAt = (_, index, c) => Draw("slow:" + c, index, 5) };
        var fastPlanner = new DeferredPlanner { ContinuesFor = index => index % 3 == 0 ? 2 : 1, RestartAt = (_, index, _) => Draw("fast", index, 9) };
        var slowCity = new CitySimulator(City(36), slowPlanner); Observe(slow, slowCity);
        var fastCity = new CitySimulator(City(36), fastPlanner); Observe(fast, fastCity);
        for (int day = 1; day <= 4; day++)
        {
            reference.AdvanceTo(Target(day));
            RunFrames(slowCity, Target(day), city => city.SynchronizeSnapshot());
            // Coarser frames: several events between pending checks.
            while (!fastCity.IsCaughtUpTo(Target(day)))
            {
                if (fastCity.PlanningPending) fastCity.ContinuePlanning(0);
                else fastCity.AdvanceFrameSlice(Target(day), 5);
            }
            fastCity.SynchronizeSnapshot();
        }
        Assert.Equal(departures, slow);
        Assert.Equal(departures, fast);
        Assert.True(departures.Count > 500);
        Assert.True(slowCity.EventRestarts > 20, "too few slow restarts: " + slowCity.EventRestarts);
        Assert.True(fastCity.EventRestarts > 20, "too few fast restarts: " + fastCity.EventRestarts);
        Assert.Equal(slowPlanner.RestartsThrown, slowCity.EventRestarts);
        Assert.Equal(fastPlanner.RestartsThrown, fastCity.EventRestarts);
        Assert.Equal(slowPlanner.Requests, slowPlanner.Disposed);
        Assert.Equal(fastPlanner.Requests, fastPlanner.Disposed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void SnapshotRestartReplaysTheWholeEventFromItsFirstRequest(int restartContinue)
    {
        var reference = new CitySimulator(City(48), new SyncPlanner());
        reference.AdvanceTo(Target(3));
        var planner = new DeferredPlanner { ContinuesFor = _ => 3 };
        var city = new CitySimulator(City(48), planner);
        int eventFirst = -1, armedIndex = -1;
        string residentBefore = "";
        bool restarted = false, replayed = false;
        planner.RestartAt = (_, index, c) => index == armedIndex && c == restartContinue;
        int guard = 0;
        while (true)
        {
            Assert.True(++guard < 10_000_000);
            if (city.PlanningPending)
            {
                // Arm the restart on an employment check that has answered its first workplace probe and is now
                // planning a later request, so the event already holds an answer from the old snapshot.
                int latest = planner.Seen.Count - 1;
                if (!restarted && armedIndex < 0 && eventFirst >= 0 && latest > eventFirst && IsProbe(city.State, planner.Seen[eventFirst]))
                    armedIndex = latest;
                bool finished = city.ContinuePlanning(0);
                if (armedIndex >= 0 && !restarted && planner.RestartsThrown == 1)
                {
                    restarted = true;
                    Assert.False(finished);
                    Assert.False(city.PlanningPending);
                    Assert.Equal(1, city.EventRestarts);
                    Assert.Equal(planner.Requests, planner.Disposed);
                    var first = planner.Seen[eventFirst];
                    var live = city.State.Citizens.Single(c => c.Identity.Id == first.Citizen!.Identity.Id);
                    // The resident is unstarted at the held clock, exactly as before the event began.
                    Assert.Equal(city.State.Minute, live.NextEventMinute);
                    Assert.Equal(first.DepartureMinute, city.State.Minute);
                    Assert.Equal(residentBefore, JsonConvert.SerializeObject(live));
                    Assert.False(city.IsCaughtUpTo(city.State.Minute));
                    string saved = SaveCodec.Write(city.State);
                    var read = SaveCodec.Read(saved);
                    Assert.Equal(SaveReadStatus.Loaded, read.Status);
                    Assert.Equal(saved, SaveCodec.Write(read.State!));
                    // The next slice replays the event from its first yield: the first probe is asked again.
                    int before = planner.Seen.Count;
                    var replay = city.AdvanceFrameSlice(Target(3), 1);
                    Assert.True(replay.PlanningPending);
                    Assert.Equal(before + 1, planner.Seen.Count);
                    var again = planner.Seen[before];
                    Assert.NotSame(first, again);
                    Assert.Equal(first.From.Id, again.From.Id); Assert.Equal(first.To.Id, again.To.Id);
                    Assert.Equal(first.DepartureMinute, again.DepartureMinute);
                    Assert.Equal(first.Citizen!.Identity.Id, again.Citizen!.Identity.Id);
                    Assert.True(IsProbe(city.State, again));
                    eventFirst = before;
                    replayed = true;
                }
                continue;
            }
            var result = city.AdvanceFrameSlice(Target(3), 1);
            if (result.PlanningPending)
            {
                eventFirst = planner.Seen.Count - 1;
                var request = planner.Seen[eventFirst];
                residentBefore = request.Citizen == null ? ""
                    : JsonConvert.SerializeObject(city.State.Citizens.Single(c => c.Identity.Id == request.Citizen.Identity.Id));
                continue;
            }
            if (result.CaughtUp) break;
        }
        Assert.True(restarted && replayed, "no employment check with two or more requests was interrupted");
        Assert.Equal(1, planner.RestartsThrown);
        Assert.Equal(planner.Requests, planner.Disposed);
        city.SynchronizeSnapshot();
        Assert.Equal(SaveCodec.Write(reference.State), SaveCodec.Write(city.State));
    }

    [Fact]
    public void ResidentWhoseSnapshotKeepsChangingRunsInlineAfterThreeRestarts()
    {
        var reference = new CitySimulator(City(48), new SyncPlanner());
        var planner = new DeferredPlanner { ContinuesFor = _ => 2 };
        var city = new CitySimulator(City(48), planner);
        string unlucky = city.State.Citizens[7].Identity.Id;
        // Every sliced attempt of this resident's events loses its snapshot; nobody else's does.
        planner.RestartAt = (request, _, c) => c >= 1 && request.Citizen?.Identity.Id == unlucky;
        for (int day = 1; day <= 3; day++)
        {
            reference.AdvanceTo(Target(day));
            RunFrames(city, Target(day));
            Assert.Equal(SaveCodec.Write(reference.State), SaveCodec.Write(city.State));
        }
        Assert.True(city.InlineAfterRestarts > 3, "too few inline runs: " + city.InlineAfterRestarts);
        // Each of the resident's events restarted exactly three times at its held minute, then ran inline.
        Assert.Equal(3 * city.InlineAfterRestarts, city.EventRestarts);
        Assert.Equal(planner.RestartsThrown, city.EventRestarts);
        Assert.Equal(planner.Requests, planner.Disposed);
        Assert.True(reference.State.Citizens.Single(c => c.Identity.Id == unlucky).CompletedJourneys > 0);
    }

    [Fact]
    public void AdvanceToReplaysAnEventWhoseSuspendedJobLostItsSnapshot()
    {
        var reference = new CitySimulator(City(24), new SyncPlanner());
        var planner = new DeferredPlanner { ContinuesFor = _ => 4 };
        var city = new CitySimulator(City(24), planner);
        AdvanceResult result;
        do { result = city.AdvanceFrameSlice(Target(2), 1); } while (!result.PlanningPending);
        Assert.False(city.ContinuePlanning(0));
        // The sliced job resumes inside AdvanceTo with an unlimited budget and finds its snapshot replaced.
        int suspended = planner.Requests - 1;
        planner.RestartAt = (_, index, _) => index == suspended;
        reference.AdvanceTo(Target(2));
        var finished = city.AdvanceTo(Target(2));
        Assert.Equal(1, planner.RestartsThrown); Assert.Equal(1, city.EventRestarts);
        Assert.True(finished.CaughtUp); Assert.False(city.PlanningPending);
        Assert.Equal(planner.Requests, planner.Disposed);
        Assert.Equal(SaveCodec.Write(reference.State), SaveCodec.Write(city.State));
    }

    private static CitizenState Filled(int n, CitizenIdentity identity) => new()
    {
        W = new WeatherCommitment(n,WeatherSample.Unknown,n%2==1),
        OptionalStopChoice = n % 2 == 1,
        OptionalStop = new OptionalStopState { SpotId = "spot-" + n, Members = { "member-" + n } },
        Identity = identity, HomeId = "home-" + n, DayPlaceId = "work-" + n, ShopId = "shop-" + n, LeisureId = "leisure-" + n,
        Preferences = new TravelPreferences { OwnsCar = n % 2 == 1, AllowTransit = n % 2 == 0, WalkingToleranceMetres = 100 * n, FareWeightMinutes = n },
        CommutePreparation = new CommutePreparation { PlaceId = "work-" + n, WorkStartsAt = 10 * n, WorkEndsAt = 20 * n, PreparedAt = n, TravelMinutes = 2 * n },
        MealOuting = new MealOuting { OriginId = "origin-" + n, OriginActivity = (Activity)(n % 5), ResumeUntil = 30 * n, ResumeEvent = 40 * n, Returning = n % 2 == 1 },
        Car = new CarState { Id = "car-" + n, ModelId = "model-" + n, PaintSeed = n, ParkedPosition = new(n, n, n) },
        JoinedMinute = 50 * n, LastEmploymentCheckDay = n, JobChanges = 3 * n, Phase = (CitizenPhase)(n % 4), Activity = (Activity)((n + 1) % 5),
        PlaceId = "place-" + n, DestinationId = "destination-" + n, DestinationActivity = (Activity)((n + 2) % 5),
        Journey = new Route { Legs = { new RouteLeg { From = new(n, 0, 0), To = new(n + 1, 0, 0), Minutes = n, Mode = TravelMode.Walk, ServiceId = "s" + n } } },
        DepartureMinute = 60 * n, NextEventMinute = 70 * n, ActivityUntilMinute = 80 * n, CompletedJourneys = 4 * n, MissedActivities = 5 * n,
        RecentVisits = { new VisitRecord { AtMinute = n, PlaceId = "visit-" + n, Activity = (Activity)(n % 5) } },
        Traits = ResidentTraits.Generate(n, "traits-" + n)
    };

    [Fact]
    public void EventCopyAndCommitCoverEveryResidentField()
    {
        var identity = new CitizenIdentity { Id = "copy", Name = "Copy" };
        var one = Filled(1, identity); var two = Filled(2, identity);
        // The fixture must differ in every public property, so a new field added to CitizenState fails here
        // until both the fixture and the event copy know about it.
        foreach (var property in typeof(CitizenState).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.Name == nameof(CitizenState.Identity)) continue;
            Assert.NotEqual(JsonConvert.SerializeObject(property.GetValue(one)), JsonConvert.SerializeObject(property.GetValue(two)));
        }
        var live = Filled(1, identity);
        var working = CitySimulator.CloneForEvent(live);
        Assert.Equal(JsonConvert.SerializeObject(live), JsonConvert.SerializeObject(working));
        Assert.Same(live.Identity, working.Identity); Assert.Same(live.Journey, working.Journey);
        // Traits are immutable after generation, so the event copy shares them and events never write them back.
        Assert.Same(live.Traits, working.Traits);
        Assert.NotSame(live.RecentVisits, working.RecentVisits); Assert.NotSame(live.Car, working.Car);
        Assert.NotSame(live.CommutePreparation, working.CommutePreparation); Assert.NotSame(live.MealOuting, working.MealOuting);
        Assert.NotSame(live.Preferences, working.Preferences);
        Assert.NotSame(live.OptionalStop, working.OptionalStop);
        Assert.NotSame(live.OptionalStop!.Members, working.OptionalStop!.Members);
        var car = live.Car; var preparation = live.CommutePreparation; var outing = live.MealOuting; var preferences = live.Preferences; var visits = live.RecentVisits;
        CitySimulator.CommitEvent(live, Filled(2, identity));
        Assert.Equal(JsonConvert.SerializeObject(Filled(2, identity)), JsonConvert.SerializeObject(live));
        // Objects that survive the event keep their identity; a cleared one is dropped.
        Assert.Same(car, live.Car); Assert.Same(preparation, live.CommutePreparation); Assert.Same(outing, live.MealOuting);
        Assert.Same(preferences, live.Preferences); Assert.Same(visits, live.RecentVisits);
        var cleared = Filled(3, identity); cleared.Car = null; cleared.MealOuting = null;
        CitySimulator.CommitEvent(live, cleared);
        Assert.Null(live.Car); Assert.Null(live.MealOuting);
        CitySimulator.CommitEvent(live, Filled(4, identity));
        Assert.Equal(JsonConvert.SerializeObject(Filled(4, identity)), JsonConvert.SerializeObject(live));
    }

    [Fact]
    public void RequestsAreAnsweredOnceAndValidated()
    {
        var request = new RouteRequest(new Place { Id = "a" }, new Place { Id = "b" }, 10, null, new TravelPreferences());
        Assert.False(request.Answered);
        request.Answer(null);
        Assert.True(request.Answered); Assert.Null(request.Result);
        Assert.Throws<InvalidOperationException>(() => request.Answer(new Route()));
        Assert.Throws<ArgumentNullException>(() => new RouteRequest(null!, new Place(), 0, null, new TravelPreferences()));
        Assert.Throws<ArgumentNullException>(() => new RouteRequest(new Place(), new Place(), 0, null, null!));
    }
    // A resumable planner that also owns a traffic network the test can replace, as the runtime does when a
    // road snapshot is rebuilt while a route job is suspended.
    private sealed class TrafficDeferredPlanner : IResumableRoutePlanner, ICommuteTimingPlanner, ITrafficRoutePlanner
    {
        public readonly DeferredPlanner Inner;
        public TrafficDeferredPlanner(DeferredPlanner inner) { Inner = inner; TrafficNetwork = Network(); }
        public TrafficNetwork? TrafficNetwork { get; set; }
        public static TrafficNetwork Network() => new TrafficNetwork("resumable-test-roads", new[]
        {
            new TrafficNetworkNode { Id = 0, Position = new Position(5000, 0, 0), KilometresPerHour = 36, AllowsCar = true, Boundary = true, Successors = new[] { 1 } },
            new TrafficNetworkNode { Id = 1, Position = new Position(5020, 0, 0), KilometresPerHour = 36, AllowsCar = true, Boundary = true }
        }, 1);
        public void SetTrafficForecast(Func<int, double, TrafficLinkForecast>? forecast) { }
        public bool IsTrafficLinkClosed(int linkId) => false;
        public IRoutePlanningJob Begin(RouteRequest request) => Inner.Begin(request);
        public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences) => Inner.Plan(from, to, minute, preferences);
    }

    // The scheduler's queue through reflection: every entry once, in strict (minute, resident) order,
    // findable by the set's own comparer, and never the event that is still in flight.
    private static void AssertQueueSound(CitySimulator city)
    {
        var field = typeof(CitySimulator).GetField("due", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var due = (System.Collections.IEnumerable)field.GetValue(city)!;
        var items = due.Cast<object>().ToList();
        Assert.Equal(items.Count, items.Distinct(ReferenceEqualityComparer.Instance).Count());
        var type = items[0].GetType();
        var minuteField = type.GetField("Minute")!; var citizenField = type.GetField("Citizen")!;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < items.Count; i++)
        {
            var citizen = (CitizenState)citizenField.GetValue(items[i])!;
            Assert.True(ids.Add(citizen.Identity.Id), "resident queued twice: " + citizen.Identity.Id);
            if (i == 0) continue;
            double before = (double)minuteField.GetValue(items[i - 1])!, after = (double)minuteField.GetValue(items[i])!;
            string previous = ((CitizenState)citizenField.GetValue(items[i - 1])!).Identity.Id;
            Assert.True(before < after || (before == after && StringComparer.Ordinal.Compare(previous, citizen.Identity.Id) < 0),
                "queue out of order at " + citizen.Identity.Id);
        }
        var contains = due.GetType().GetMethod("Contains")!;
        foreach (var item in items) Assert.True((bool)contains.Invoke(due, new[] { item })!, "queue lost an entry's position");
    }

    [Fact]
    public void NetworkRebuildWhilePendingKeepsTheInFlightEventOutOfTheQueue()
    {
        // The runtime replaces the road snapshot while a job is suspended and refreshes closures before planning
        // resumes. EnsureTrafficNetwork then rebuilds the scheduler queue; the in-flight event must stay out of
        // it until CompleteEvent (or AbortPending) re-adds it.
        var departures = new List<string>(); var rebuiltDepartures = new List<string>();
        void Observe(List<string> into, CitySimulator city) => city.ActivityObserved += (id, citizen, minute) =>
            into.Add(id + ":" + minute.ToString("R", CultureInfo.InvariantCulture) + ":" + citizen.Phase + ":" + citizen.PlaceId + ":" + citizen.DestinationId);
        var reference = new CitySimulator(City(36), new TrafficDeferredPlanner(new DeferredPlanner { ContinuesFor = index => 1 + index % 3 }));
        Observe(departures, reference);
        var planner = new TrafficDeferredPlanner(new DeferredPlanner { ContinuesFor = index => 1 + index % 3 });
        var city = new CitySimulator(City(36), planner);
        Observe(rebuiltDepartures, city);
        int rebuilds = 0, pendingSeen = 0;
        for (int day = 1; day <= 4; day++)
        {
            double target = Target(day);
            RunFrames(reference, target);
            int guard = 0;
            while (true)
            {
                Assert.True(++guard < 10_000_000);
                if (city.PlanningPending)
                {
                    Assert.False(city.IsCaughtUpTo(city.State.Minute));
                    if (++pendingSeen % 5 == 0)
                    {
                        planner.TrafficNetwork = TrafficDeferredPlanner.Network();
                        city.RefreshTrafficClosures();
                        // A frame slice while pending also passes through EnsureTrafficNetwork.
                        planner.TrafficNetwork = TrafficDeferredPlanner.Network();
                        Assert.True(city.AdvanceFrameSlice(target, 1).PlanningPending);
                        rebuilds++;
                        AssertQueueSound(city);
                        Assert.False(city.IsCaughtUpTo(city.State.Minute));
                    }
                    city.ContinuePlanning(0);
                    if (!city.PlanningPending) AssertQueueSound(city);
                    continue;
                }
                var result = city.AdvanceFrameSlice(target, 1);
                if (result.CaughtUp) break;
            }
            city.SynchronizeSnapshot();
            Assert.True(city.IsCaughtUpTo(target));
            AssertQueueSound(city);
            Assert.Equal(SaveCodec.Write(reference.State), SaveCodec.Write(city.State));
        }
        Assert.True(rebuilds > 20, "too few rebuilds while pending: " + rebuilds);
        // Every event, including each interrupted resident's, lands in the uninterrupted order.
        Assert.Equal(departures, rebuiltDepartures);
        Assert.True(departures.Count > 300);
    }
}
