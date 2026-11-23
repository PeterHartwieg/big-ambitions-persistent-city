using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using PersistentCity.Compatibility;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;
using Xunit.Abstractions;
using Fake = RealLifeOverhaul.Flare;

namespace PersistentCity.Tests;

[Collection(RloCompatibilityTests.SharedFlareState)]
public sealed class Gr5Fix38AllocationAndLeakTests
{
    private readonly ITestOutputHelper output;
    public Gr5Fix38AllocationAndLeakTests(ITestOutputHelper output) { this.output = output; }

    private static readonly FieldInfo RouteLegsItemsField = typeof(RouteLegs).GetField("items", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static Position P(double x, double z = 0) => new Position(x, 0, z);

    private static Route MakeRoute(Random random, Position from, int legs, TravelMode mode, string service, bool shared, double jitter = 0)
    {
        var route = new Route(new RouteLegs(legs));
        var at = from;
        for (int i = 0; i < legs; i++)
        {
            var next = new Position(at.X + 1 + random.NextDouble() * 4, 0, at.Z + random.NextDouble() - 0.5);
            route.Legs.Add(new RouteLeg { From = i == 0 ? new Position(at.X + jitter, 0, at.Z) : at, To = next, Minutes = 0.05 + random.NextDouble(), Mode = mode, ServiceId = service });
            at = next;
        }
        if (random.Next(4) == 0 && route.Legs.Count > 0) route.WithEstimate(route.Minutes + random.NextDouble() * 3);
        return shared ? TransportComposition.Shared(route) : route;
    }

    private static Position End(Route route, Position fallback) => route.Legs.Count == 0 ? fallback : route.Legs[route.Legs.Count - 1].To;

    private static long MinRoundBytes(int rounds, Action action)
    {
        action();
        long best = long.MaxValue;
        for (int r = 0; r < rounds; r++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            action();
            long used = GC.GetAllocatedBytesForCurrentThread() - before;
            if (used < best) best = used;
        }
        return best;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private CitySimulator RunFiveRetimeCycles(CityState state, ClockRetimeDriver driver, List<WeakReference> retired,
        out long totalCommittedJourneys, out long slackSlots, out long priorSlackBytesEstimate)
    {
        var city = new CitySimulator(state, new Gr5AdvanceSegmentMeasure.Straight());
        city.AttachFoodMarket(new VisitorRoutineTests.Market { Shops = F5gPopulationFillTests.Shops(state) });
        city.AttachOptionalStops(new Gr5AllocationMeasure.StopSpots(), OptionalStopPolicy.ApprovedChoicePercentage);
        city.AdvanceTo(Math.Floor(state.Minute / 1440) * 1440 + 1440 + 15.5 * 60, int.MaxValue);

        long journeys = 0, slackTotal = 0, priorSlack = 0;

        void VerifyTrimmed(string label)
        {
            int travelling = 0;
            long routeLegArrayBytes = 0;
            foreach (var person in state.Citizens.Concat(state.VisitorRoutines))
            {
                if (person.Journey == null) continue;
                travelling++;
                journeys++;
                var list = (List<RouteLeg>)RouteLegsItemsField.GetValue(person.Journey.Legs)!;
                slackTotal += list.Capacity - list.Count;
                Assert.Equal(list.Count, list.Capacity);
                routeLegArrayBytes += 32 + (long)list.Capacity * 8;
            }
            output.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0}: {1} journeys, 0 slack RouteLeg[] slots, backing arrays {2:N0} B (Mono 64-bit model)",
                label, travelling, routeLegArrayBytes));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        CitySimulator StepRetime(CitySimulator current, double rate)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            while (driver.Update(state, rate, false, 1.4, ClockRetimeStages.SliceMilliseconds) != ClockRetimeOutcome.Committed) { }
            var rebuild = current.BeginRebuildRoutes(new Gr5AdvanceSegmentMeasure.Straight(), driver.TakeRetimedTraffic());
            while (!rebuild.Step(0)) { }
            var next = rebuild.TakeResult();
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            output.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "  retime -> {0}x: {1:N0} B (.NET), activeJourneys valid={2}", rate, bytes, next.ActiveJourneyIndexMatchesState()));
            Assert.True(next.ActiveJourneyIndexMatchesState());
            retired.Add(new WeakReference(current));
            return next;
        }

        VerifyTrimmed("R0");
        for (int cycle = 1; cycle <= 5; cycle++)
        {
            int splitBefore = state.Citizens.Concat(state.VisitorRoutines).Count(c => c.Phase == CitizenPhase.Travelling && c.Journey != null);
            priorSlack += splitBefore * 8L;
            city = StepRetime(city, 2);
            city.AdvanceTo(state.Minute + 0.05, int.MaxValue);
            city = StepRetime(city, 1);
            city.AdvanceTo(state.Minute + 9, int.MaxValue);
            VerifyTrimmed("R" + cycle);
        }

        city.AdvanceTo(state.Minute + 30, int.MaxValue);
        VerifyTrimmed("After30GameMinutes");
        totalCommittedJourneys = journeys;
        slackSlots = slackTotal;
        priorSlackBytesEstimate = priorSlack;
        return city;
    }

    [Fact]
    public void RepeatedRetimesTrimEveryCommittedRouteLegArrayAndCollectRetiredSimulators()
    {
        var state = Gr5AllocationMeasure.RigCity();
        var driver = new ClockRetimeDriver();
        var retired = new List<WeakReference>();
        var city = RunFiveRetimeCycles(state, driver, retired, out long totalCommittedJourneys, out long slackSlots, out long priorSlackBytesEstimate);

        Assert.True(totalCommittedJourneys > 100);
        Assert.Equal(0, slackSlots);

        var census = new RetainedHeap();
        census.Add("simulation", city);
        census.Add("driver", driver);
        long simCount = census.Types.FirstOrDefault(t => t.Name == "CitySimulator")?.Objects ?? 0;
        Assert.Equal(1, simCount);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        int stillAlive = retired.Count(r => r.IsAlive);
        output.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "retired simulators alive after GC: {0}/{1}; prior +1 slack model saved >= {2:N0} B across 5 cycles",
            stillAlive, retired.Count, priorSlackBytesEstimate));
        Assert.Equal(0, stillAlive);
    }

    [Fact]
    public void EventShellScratchPreservesSaveHashAndCutsPerEventAllocation()
    {
        string RunCity(bool pooled, out long allocBytes, out int events)
        {
            var state = Gr5AllocationMeasure.RigCity();
            var city = new CitySimulator(state, new Gr5AdvanceSegmentMeasure.Straight()) { PoolEventShells = pooled };
            city.AttachFoodMarket(new VisitorRoutineTests.Market { Shops = F5gPopulationFillTests.Shops(state) });
            city.AttachOptionalStops(new Gr5AllocationMeasure.StopSpots(), OptionalStopPolicy.ApprovedChoicePercentage);
            double start = Math.Floor(state.Minute / 1440) * 1440 + 1440 + 7 * 60;
            city.AdvanceTo(start, int.MaxValue);
            long before = GC.GetAllocatedBytesForCurrentThread();
            var res = city.AdvanceTo(start + 180, int.MaxValue);
            allocBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            events = res.CitizenEvents;
            city.SynchronizeSnapshot();
            return RushSpikeReplay.Sha(SaveCodec.Write(city.State));
        }

        string unpooledHash = RunCity(false, out long unpooledBytes, out int unpooledEvents);
        string pooledHash = RunCity(true, out long pooledBytes, out int pooledEvents);
        Assert.Equal(unpooledHash, pooledHash);
        Assert.Equal(unpooledEvents, pooledEvents);
        Assert.True(pooledEvents > 500);

        var probeState = Gr5AllocationMeasure.RigCity();
        var probeSim = new CitySimulator(probeState, new Gr5AdvanceSegmentMeasure.Straight());
        probeSim.AttachFoodMarket(new VisitorRoutineTests.Market { Shops = F5gPopulationFillTests.Shops(probeState) });
        probeSim.AdvanceTo(Math.Floor(probeState.Minute / 1440) * 1440 + 1440 + 15.5 * 60, int.MaxValue);
        var rich = probeState.Citizens.First(c => c.Plan != null);
        if (rich.Needs == null)
        {
            var needs = new PersonNeeds();
            for (int i = 0; i < 12; i++) needs.Add(new NeedState("cat" + i, i + 1, "venue" + i, 0));
            rich.Needs = needs;
        }
        rich.Car ??= new CarState { Id = rich.Identity.Id + ":car", ParkedPosition = P(10) };
        rich.MealOuting ??= new MealOuting { OriginId = rich.HomeId, OriginActivity = Activity.Home, ResumeUntil = probeState.Minute + 60, ResumeEvent = probeState.Minute + 60 };
        rich.OptionalStop ??= new OptionalStopState { Kind = DwellKind.Resting, PlaceId = rich.HomeId, StartMinute = probeState.Minute, EndMinute = probeState.Minute + 15 };

        var scratchShell = CitySimulator.CloneForEvent(rich);
        long clonePerCall = MinRoundBytes(5, () =>
        {
            for (int i = 0; i < 500; i++) _ = CitySimulator.CloneForEvent(rich);
        }) / 500;
        long copyOnlyPerCall = MinRoundBytes(5, () =>
        {
            for (int i = 0; i < 500; i++) _ = CitySimulator.CopyForEvent(rich, scratchShell);
        }) / 500;

        output.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "3 h rig city advance ({0} citizen events): unpooled {1:N0} B ({2:N0} B/ev) vs pooled+scratch {3:N0} B ({4:N0} B/ev) (.NET); CloneForEvent {5:N0} B vs CopyForEvent {6:N0} B; save hash {7}",
            pooledEvents, unpooledBytes, unpooledBytes / pooledEvents, pooledBytes, pooledBytes / pooledEvents, clonePerCall, copyOnlyPerCall, pooledHash));
        Assert.True(pooledBytes < unpooledBytes, unpooledBytes + " -> " + pooledBytes);
    }

    private sealed class SharedStraightPlanner : IRoutePlanner, ISharedWalkingPlanner
    {
        private readonly Dictionary<(string, string), Route> cache = new Dictionary<(string, string), Route>();
        public int SharedCalls { get; private set; }
        public int CopyCalls { get; private set; }

        public Route? PlanShared(Place from, Place to, double departureMinute, TravelPreferences preferences)
        {
            SharedCalls++;
            var key = (from.Id, to.Id);
            if (!cache.TryGetValue(key, out var route))
            {
                double distance = Position.Distance(from.Position, to.Position);
                int legs = Math.Max(1, Math.Min(16, (int)Math.Ceiling(distance / 25)));
                var built = new Route(new RouteLegs(legs));
                var at = from.Position;
                double totalMinutes = Math.Max(0.25, distance / 80.0);
                for (int i = 1; i <= legs; i++)
                {
                    var next = i == legs ? to.Position : Position.Lerp(from.Position, to.Position, (double)i / legs);
                    built.Legs.Add(new RouteLeg { From = at, To = next, Minutes = totalMinutes / legs, Mode = TravelMode.Walk });
                    at = next;
                }
                route = TransportComposition.Shared(built);
                cache.Add(key, route);
            }
            return route;
        }

        public Route? Plan(Place from, Place to, double departureMinute, TravelPreferences preferences)
        {
            CopyCalls++;
            var shared = PlanShared(from, to, departureMinute, preferences);
            return shared == null ? null : TransportComposition.Copy(shared);
        }
    }

    private sealed class NonSharedStraightPlanner : IRoutePlanner
    {
        private readonly SharedStraightPlanner inner = new SharedStraightPlanner();
        public Route? Plan(Place from, Place to, double departureMinute, TravelPreferences preferences)
            => inner.Plan(from, to, departureMinute, preferences);
    }

    [Fact]
    public void SharedWalkingPlannerAndFoodRecyclingPreserveDecisionsAndRetainZeroRoutes()
    {
        (string Hash, long Bytes, int FoodEvents, CitySimulator Sim) RunFoodArm(bool optimized)
        {
            var state = Gr5AllocationMeasure.RigCity();
            IRoutePlanner planner = optimized ? new SharedStraightPlanner() : new NonSharedStraightPlanner();
            var city = new CitySimulator(state, planner);
            city.AttachFoodMarket(new VisitorRoutineTests.Market { Shops = F5gPopulationFillTests.Shops(state) });
            if (!optimized) city.FoodDecisionObserved += _ => { };
            double day = Math.Floor(state.Minute / 1440) * 1440 + 1440;
            city.AdvanceTo(day + 11 * 60, int.MaxValue);
            long before = GC.GetAllocatedBytesForCurrentThread();
            var res = city.AdvanceTo(day + 15 * 60, int.MaxValue);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            city.SynchronizeSnapshot();
            return (RushSpikeReplay.Sha(SaveCodec.Write(city.State)), bytes, res.FoodContinuationEvents, city);
        }

        var baseline = RunFoodArm(optimized: false);
        var optimized = RunFoodArm(optimized: true);
        Assert.Equal(baseline.Hash, optimized.Hash);
        Assert.Equal(baseline.FoodEvents, optimized.FoodEvents);
        Assert.True(optimized.FoodEvents > 50);

        var foodField = typeof(CitySimulator).GetField("food", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var food = foodField.GetValue(optimized.Sim)!;
        var plannerRoutesField = food.GetType().GetField("routes", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var plannerRef = plannerRoutesField.GetValue(food)!;
        var foodCensus = new RetainedHeap();
        foodCensus.Add("planner", plannerRef);
        long plannerRoutes = foodCensus.Types.FirstOrDefault(t => t.Name == "Route")?.Objects ?? 0;
        long plannerLegs = foodCensus.Types.FirstOrDefault(t => t.Name == "RouteLeg")?.Objects ?? 0;
        foodCensus.Add("food", food);
        long totalRoutes = foodCensus.Types.FirstOrDefault(t => t.Name == "Route")?.Objects ?? 0;
        long totalLegs = foodCensus.Types.FirstOrDefault(t => t.Name == "RouteLeg")?.Objects ?? 0;
        long foodOwnRoutes = totalRoutes - plannerRoutes;
        long foodOwnLegs = totalLegs - plannerLegs;
        Assert.Equal(0, foodOwnRoutes);
        Assert.Equal(0, foodOwnLegs);

        var decisionField = food.GetType().GetField("recycledDecision", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var recycled = (FoodDecisionState?)decisionField.GetValue(food);
        if (recycled != null)
        {
            Assert.Null(recycled.RoutesValidatedBy);
            foreach (var shop in recycled.Shops) { Assert.Null(shop.Outward); Assert.Null(shop.Return); }
        }
        var spareField = food.GetType().GetField("spareDecisionState", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var spareDecision = (FoodDecisionState?)spareField.GetValue(food);
        if (spareDecision != null)
        {
            Assert.Null(spareDecision.RoutesValidatedBy);
            foreach (var shop in spareDecision.Shops) { Assert.Null(shop.Outward); Assert.Null(shop.Return); }
        }
        var recycledBizField = food.GetType().GetField("recycledMaterializedBusinesses", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var recycledBiz = (List<FoodBusiness>)recycledBizField.GetValue(food)!;
        Assert.True(recycledBiz.Count <= FoodChoicePolicy.MaximumShortlist);

        output.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "4 h rig food window ({0} food events): baseline (copy+unrecycled) {1:N0} B vs optimized (PlanShared+recycled) {2:N0} B (-{3:N0} B, {4:0.0}% cut) (.NET); save hash {5}; food-own retained Route={6} RouteLeg={7} (outside planner cache)",
            optimized.FoodEvents, baseline.Bytes, optimized.Bytes, baseline.Bytes - optimized.Bytes,
            100.0 * (baseline.Bytes - optimized.Bytes) / baseline.Bytes, optimized.Hash, foodOwnRoutes, foodOwnLegs));
        Assert.True(optimized.Bytes < baseline.Bytes, baseline.Bytes + " -> " + optimized.Bytes);
    }

    [Fact]
    public void FivePartCarCandidateScoringMatchesJoinAndValidateAndAllocatesZeroBytes()
    {
        var random = new Random(3801);
        int valid = 0, invalid = 0;
        var samples = new List<(Route? A, Route? B, Route? C, Route? D, Route? E, Position From, Position To, TravelPreferences Prefs, Position? Parked)>(512);
        for (int i = 0; i < 4000; i++)
        {
            var from = P(random.NextDouble() * 10, random.NextDouble() * 10);
            var access = MakeRoute(random, from, random.Next(0, 12), TravelMode.Walk, "", random.Next(2) == 0);
            var parked = End(access, from);
            var prep = TransportComposition.Shared(TransportComposition.Single(new RouteLeg { From = parked, To = parked, Mode = TravelMode.Car, Minutes = 2 }));
            var road = MakeRoute(random, parked, random.Next(1, 30), random.Next(15) == 0 ? TravelMode.Walk : TravelMode.Car, "", random.Next(2) == 0, random.Next(15) == 0 ? 0.01 : 0);
            var roadEnd = End(road, parked);
            var park = TransportComposition.Shared(TransportComposition.Single(new RouteLeg { From = roadEnd, To = roadEnd, Mode = TravelMode.Car, Minutes = 2 }));
            var egress = MakeRoute(random, roadEnd, random.Next(0, 12), TravelMode.Walk, "", random.Next(2) == 0, random.Next(15) == 0 ? 0.002 : 0);
            var to = End(egress, roadEnd);
            if (random.Next(12) == 0) to = new Position(to.X + 0.01, 0, to.Z);
            var prefs = new TravelPreferences
            {
                OwnsCar = random.Next(6) != 0,
                AllowTransit = true,
                WalkingToleranceMetres = random.Next(6) == 0 ? 5 : 4000,
            };
            Position? parkedArg = random.Next(10) == 0 ? (Position?)null : parked;
            Route? a = random.Next(25) == 0 ? null : access;
            Route? c = random.Next(25) == 0 ? null : road;

            var joined = TransportComposition.Join(a, prep, c, park, egress);
            bool expected = joined != null && TransportComposition.Valid(joined, from, to, prefs, parkedArg);
            bool actual = TransportComposition.TryJoinedScore(a, prep, c, park, egress, from, to, prefs, parkedArg, out double score);
            Assert.Equal(expected, actual);
            if (expected)
            {
                valid++;
                Assert.Equal(BitConverter.DoubleToInt64Bits(joined!.EstimatedMinutes), BitConverter.DoubleToInt64Bits(score));
                if (samples.Count < 256) samples.Add((a, prep, c, park, egress, from, to, prefs, parkedArg));
            }
            else invalid++;
        }
        Assert.True(valid > 500 && invalid > 500);

        long scoreAlloc = MinRoundBytes(5, () =>
        {
            for (int r = 0; r < 10; r++)
                for (int i = 0; i < samples.Count; i++)
                {
                    var s = samples[i];
                    TransportComposition.TryJoinedScore(s.A, s.B, s.C, s.D, s.E, s.From, s.To, s.Prefs, s.Parked, out _);
                }
        });
        Assert.Equal(0, scoreAlloc);

        var candidates = new RouteCandidates();
        var fastWalk = TransportComposition.Shared(TransportComposition.Single(new RouteLeg { From = samples[0].From, To = samples[0].To, Mode = TravelMode.Walk, Minutes = 0.01 }));
        long losingBefore = MinRoundBytes(5, () =>
        {
            for (int i = 0; i < 200; i++)
            {
                var s = samples[0];
                candidates.Reset();
                candidates.Consider(fastWalk, s.From, s.To, s.Prefs, s.Parked);
                candidates.Consider(TransportComposition.Join(s.A, s.B, s.C, s.D, s.E), s.From, s.To, s.Prefs, s.Parked);
            }
        }) / 200;
        long losingAfter = MinRoundBytes(5, () =>
        {
            for (int i = 0; i < 200; i++)
            {
                var s = samples[0];
                candidates.Reset();
                candidates.Consider(fastWalk, s.From, s.To, s.Prefs, s.Parked);
                candidates.ConsiderJoined(s.A, s.B, s.C, s.D, s.E, s.From, s.To, s.Prefs, s.Parked);
            }
        }) / 200;

        output.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "5-part car candidate: {0} valid / {1} refused bit-identical; TryJoinedScore {2} B over {3} calls; losing car candidate Join+Consider {4:N0} B -> ConsiderJoined {5:N0} B (.NET)",
            valid, invalid, scoreAlloc, samples.Count * 10, losingBefore, losingAfter));
        Assert.Equal(0, losingAfter);
        Assert.True(losingBefore > 0);
    }

    [Fact]
    public void RloPeriodicMatchingPreservesContinuityAndAllocatesZeroBytesWhenUnchanged()
    {
        CityApiHooks.Reset();
        RealLifeOverhaul.GoingPublic.InSession = false;
        CityApiHooks.Seed = 1000;
        CityApiHooks.Citizens = Enumerable.Range(0, 400).Reverse().Select(i => new CityApiHooks.Citizen(
            i, "Given" + i, "Family" + i, i == 0 ? null : "district" + i % 3,
            20 + i % 60, i % 2 == 0, ((Fake.FlarePersona)(i % 8)).ToString(), Fake.FlareAvatarRules.Citizen(1000, i).Seed)).ToList();

        var rlo = StandIn.Build("0.99.0");
        var adapter = new RloIdentityAdapter();
        var initial = adapter.Read(rlo);
        Assert.Equal(RloReadStatus.Ready, initial.Status);
        var state = Population.Create(initial.Identities, new[]
        {
            new Place { Id = "h", Name = "Home", Kind = PlaceKind.Home, Capacity = 500, Position = P(0) },
            new Place { Id = "w", Name = "Work", Kind = PlaceKind.Work, Capacity = 500, Position = P(100) },
        }, initial.PopulationKey, initial.SourceVersion, initial.CitySeed, 1440);
        Assert.Equal(DonorContinuity.Match, initial.Continuity(state));

        var matched = adapter.ReadMatching(rlo, state);
        Assert.Equal(RloReadStatus.Ready, matched.Status);
        Assert.Equal(DonorContinuity.Match, matched.Continuity(state));
        Assert.Equal(initial.PopulationKey, matched.PopulationKey);

        long fullPerRead = MinRoundBytes(3, () =>
        {
            for (int i = 0; i < 10; i++) Assert.Equal(DonorContinuity.Match, adapter.Read(rlo).Continuity(state));
        }) / 10;
        long matchingPerRead = MinRoundBytes(5, () =>
        {
            for (int i = 0; i < 20; i++) Assert.Equal(DonorContinuity.Match, adapter.ReadMatching(rlo, state).Continuity(state));
        }) / 20;

        CityApiHooks.Citizens[10] = new CityApiHooks.Citizen(
            CityApiHooks.Citizens[10].Index, "Renamed", CityApiHooks.Citizens[10].Last, CityApiHooks.Citizens[10].District,
            CityApiHooks.Citizens[10].Age, CityApiHooks.Citizens[10].Female, CityApiHooks.Citizens[10].Persona, CityApiHooks.Citizens[10].AppearanceSeed);
        var mutated = adapter.ReadMatching(rlo, state);
        Assert.Equal(RloReadStatus.Ready, mutated.Status);
        Assert.Equal(DonorContinuity.Different, mutated.Continuity(state));

        CityApiHooks.Seed = 1001;
        var reseeded = adapter.ReadMatching(rlo, state);
        Assert.Equal(DonorContinuity.Different, reseeded.Continuity(state));

        output.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "RLO periodic read (400 citizens, incl. stand-in CityApiCitizen allocations): before Read {0:N0} B/read ({1:N0} KB/600s) -> after ReadMatching {2:N0} B/read ({3:N0} KB/600s), saved {4:N0} B/read ({5:N0} KB/600s) (.NET)",
            fullPerRead, fullPerRead * 20 / 1000, matchingPerRead, matchingPerRead * 20 / 1000, fullPerRead - matchingPerRead, (fullPerRead - matchingPerRead) * 20 / 1000));
        Assert.True(fullPerRead - matchingPerRead >= 75_000, fullPerRead + " -> " + matchingPerRead);
    }

    [Fact]
    public void CarAppearanceSplitHashMatchesConcatenationWithZeroAllocation()
    {
        var ids = Enumerable.Range(0, 500).Select(i => "car:resident-" + i + ":vehicle").ToArray();
        foreach (string id in ids)
        {
            Assert.Equal(CarAppearance.Hash(id + ":paint"), CarAppearance.Hash(id, ":paint"));
            Assert.Equal(CarAppearance.Hash(id + ":parked"), CarAppearance.Hash(id, ":parked"));
            Assert.Equal(CarAppearance.Hash(id + ""), CarAppearance.Hash(id, ""));
        }
        long alloc = MinRoundBytes(5, () =>
        {
            uint acc = 0;
            for (int i = 0; i < ids.Length; i++) acc ^= CarAppearance.Hash(ids[i], ":parked");
            if (acc == 123456789u) throw new InvalidOperationException();
        });
        output.WriteLine("CarAppearance.Hash(id, suffix) over 500 IDs: " + alloc + " B (.NET)");
        Assert.Equal(0, alloc);
    }

    [Fact]
    public void CompactPresentationGridBinaryHeapsAndStructSortedIdsShrinkRetainedGraph()
    {
        const string native = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var network = new TrafficNetwork(native, Gr5NetworkFootprintTests.SignalledRoad(1), 1);
        var state = Gr5AllocationMeasure.RigCity();
        var sim = new CitySimulator(state, new Gr5NetworkRequeueTests.SwappablePlanner { TrafficNetwork = network });
        sim.AttachFoodMarket(new VisitorRoutineTests.Market { Shops = F5gPopulationFillTests.Shops(state) });
        sim.AttachOptionalStops(new Gr5AllocationMeasure.StopSpots(), OptionalStopPolicy.ApprovedChoicePercentage);
        sim.AdvanceTo(Math.Floor(state.Minute / 1440) * 1440 + 1440 + 2 * 60, int.MaxValue);
        Assert.True(sim.ActiveJourneyIndexMatchesState());

        var census = new RetainedHeap();
        census.Add("simulation", sim);
        long sortedIds = census.Types.FirstOrDefault(t => t.Name == "SortedIds")?.Objects ?? 0;
        long sortedSetNodes = census.Types.Where(t => t.Name.Contains("SortedSet", StringComparison.Ordinal)).Sum(t => t.Objects);
        long gridLongLists = census.Types.FirstOrDefault(t => t.Name == "List<Int64>")?.Objects ?? 0;
        long gridIntLists = census.Types.FirstOrDefault(t => t.Name == "List<Int32>")?.Objects ?? 0;

        var signalA = new TrafficSignalSchedule { CycleMinutes = 1, GreenStartMinutes = 0, GreenDurationMinutes = 0.5, PhaseOffsetMinutes = 0 };
        var links = Enumerable.Range(0, 20).Select(i => new TrafficLinkDefinition
        {
            Id = i, Capacity = 4, FreeFlowMinutes = 0.2, HeadwayMinutes = 0.05, Signal = signalA
        }).ToList();
        var kernel = new TrafficKernel(links, 0);
        var rescaled = kernel.RescaleTime(2);
        var rescaledState = rescaled.CaptureShared();
        var distinctSignals = rescaledState.Definitions.Select(d => d.Signal).Distinct(ReferenceEqualityComparer.Instance).Count();
        Assert.Equal(1, distinctSignals);
        Assert.Equal(2.0, rescaledState.Definitions[0].Signal!.CycleMinutes);

        output.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "night rig city retained (Mono 64-bit model): total {0:N0} B / {1:N0} objects; SortedIds={2}, SortedSet*={3}, List<Int64>={4}, List<Int32>={5}; rescaled shared signals={6}/20",
            census.Bytes, census.Objects, sortedIds, sortedSetNodes, gridLongLists, gridIntLists, distinctSignals));
        Assert.Equal(0, sortedIds);
        Assert.True(sortedSetNodes <= 8, sortedSetNodes.ToString());
        Assert.True(gridLongLists <= 8, gridLongLists.ToString());
        Assert.True(gridIntLists <= 8, gridIntLists.ToString());
    }
}
