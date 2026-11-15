using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Compatibility;
using PersistentCity.Core;
using PersistentCity.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests
{
    [Collection(RloCompatibilityTests.SharedFlareState)]
    public sealed class Gr5SubSegmentTests
    {
        private readonly ITestOutputHelper output;
        public Gr5SubSegmentTests(ITestOutputHelper output) { this.output = output; CityApiHooks.Reset(); }

        [Fact]
        public void EverySubSegmentNamesItsParentAndLeavesTheParentTotalsUnchanged()
        {
            for (int i = 0; i < GcPauseSampler.Segments; i++)
            {
                var segment = (GcAllocationSegment)i;
                if (!GcPauseSampler.IsSub(segment)) { Assert.Equal(segment, GcPauseSampler.SubParent(segment)); continue; }
                var parent = GcPauseSampler.SubParent(segment);
                Assert.NotEqual(segment, parent);
                Assert.StartsWith(GcPauseSampler.SegmentName(parent).Split('_')[0], GcPauseSampler.SegmentName(segment));
                Assert.True(GcPauseSampler.IsDetail(segment));
                Assert.True(ModFrameRecorder.Overlaps(segment));
                Assert.Equal(GcPauseSampler.IsOuter(parent), GcPauseSampler.IsOuter(segment));
            }
            Assert.Equal(GcPauseSampler.Segments, Enum.GetValues(typeof(GcAllocationSegment)).Length);
            Assert.Equal(GcPauseSampler.Segments, Enumerable.Range(0, GcPauseSampler.Segments).Select(i => GcPauseSampler.SegmentName((GcAllocationSegment)i)).Distinct().Count());
        }

        [Fact]
        public void AdvanceSubSegmentsSplitEventsAndPlanningWithoutMovingTheirMarks()
        {
            var sampler = new GcPauseSampler(64);
            sampler.Start(1);
            sampler.BeginNested(1000, 0);
            sampler.Detail(GcAllocationSegment.AdvanceStops, 1010, 0);
            sampler.Sub(GcAllocationSegment.AdvancePlanningSlice, 1300, 0);
            sampler.Detail(GcAllocationSegment.AdvancePlanning, 1300, 0);
            sampler.Sub(GcAllocationSegment.AdvanceEventsTraffic, 1500, 0);
            sampler.Detail(GcAllocationSegment.AdvanceEvents, 1500, 0);
            sampler.Sub(GcAllocationSegment.AdvanceEventsCitizen, 1550, 0);
            sampler.Detail(GcAllocationSegment.AdvanceEvents, 1550, 0);
            sampler.EndDetails(GcAllocationSegment.AdvanceOther, 1560, 0);
            Assert.Equal(290, sampler.SegmentBytes(GcAllocationSegment.AdvancePlanning));
            Assert.Equal(290, sampler.SegmentBytes(GcAllocationSegment.AdvancePlanningSlice));
            Assert.Equal(250, sampler.SegmentBytes(GcAllocationSegment.AdvanceEvents));
            Assert.Equal(200, sampler.SegmentBytes(GcAllocationSegment.AdvanceEventsTraffic));
            Assert.Equal(50, sampler.SegmentBytes(GcAllocationSegment.AdvanceEventsCitizen));
            Assert.Equal(10, sampler.SegmentBytes(GcAllocationSegment.AdvanceOther));

            Assert.Equal(GcAllocationSegment.AdvanceEventsRouteBegin, GcPauseSampler.AdvanceEventSegment(0, 0, 0, true));
            Assert.Equal(GcAllocationSegment.AdvanceEventsTraffic, GcPauseSampler.AdvanceEventSegment(1, 1, 1, false));
            Assert.Equal(GcAllocationSegment.AdvanceEventsFood, GcPauseSampler.AdvanceEventSegment(0, 1, 1, false));
            Assert.Equal(GcAllocationSegment.AdvanceEventsCitizen, GcPauseSampler.AdvanceEventSegment(0, 0, 1, false));
            Assert.Equal(GcAllocationSegment.AdvanceEventsOther, GcPauseSampler.AdvanceEventSegment(0, 0, 0, false));
        }

        [Fact]
        public void PhysicalSetupDetailsAndTheRemainderSubSegmentAccountForTheUnattributedBurst()
        {
            var sampler = new GcPauseSampler(64);
            sampler.Start(1);
            sampler.BeginNested(10_000, 0);
            sampler.Detail(GcAllocationSegment.PhysicalSetupPlaces, 10_100, 0);
            sampler.Detail(GcAllocationSegment.PhysicalSetupBodies, 9_010_100, 0);
            sampler.Detail(GcAllocationSegment.PhysicalSetupVehicles, 12_010_100, 0);
            sampler.Sub(GcAllocationSegment.PhysicalUnattributedSetup, 12_010_200, 0);
            sampler.EndNested(GcAllocationSegment.Physical, GcAllocationSegment.PhysicalUnattributed, 12_010_200, 0);
            Assert.Equal(12_000_200, sampler.SegmentBytes(GcAllocationSegment.PhysicalUnattributed));
            Assert.Equal(100, sampler.SegmentBytes(GcAllocationSegment.PhysicalUnattributedSetup));
            Assert.Equal(12_000_200, sampler.SegmentBytes(GcAllocationSegment.Physical));
            Assert.Equal(9_000_000, sampler.SegmentBytes(GcAllocationSegment.PhysicalSetupBodies));
            Assert.Equal(12_000_200, SubTotal(sampler, GcAllocationSegment.PhysicalUnattributed));
            Assert.Equal(0, sampler.SegmentBytes(GcAllocationSegment.PhysicalUnattributedUpdate));

            sampler.BeginNested(20_000_000, 0);
            sampler.Detail(GcAllocationSegment.PhysicalUnattributedEntry, 20_000_030, 0);
            sampler.Charge(GcAllocationSegment.PhysicalUnattributed, 20_000_030, 0);
            sampler.Charge(GcAllocationSegment.PhysicalAmbient, 20_000_100, 0);
            sampler.Sub(GcAllocationSegment.PhysicalUnattributedUpdate, 20_000_150, 0);
            sampler.EndNested(GcAllocationSegment.Physical, GcAllocationSegment.PhysicalUnattributed, 20_000_150, 0);
            Assert.Equal(30, sampler.SegmentBytes(GcAllocationSegment.PhysicalUnattributedEntry));
            Assert.Equal(50, sampler.SegmentBytes(GcAllocationSegment.PhysicalUnattributedUpdate));
            Assert.Equal(12_000_280, sampler.SegmentBytes(GcAllocationSegment.PhysicalUnattributed));
            Assert.Equal(12_000_280, SubTotal(sampler, GcAllocationSegment.PhysicalUnattributed));
        }

        private static long SubTotal(GcPauseSampler sampler, GcAllocationSegment parent) => Enumerable.Range(0, GcPauseSampler.Segments)
            .Select(i => (GcAllocationSegment)i).Where(s => GcPauseSampler.IsSub(s) && GcPauseSampler.SubParent(s) == parent).Sum(sampler.SegmentBytes);

        [Fact]
        public void ARouteBeginThatSuspendsOnPlanningIsClassifiedAsARouteBegin()
        {
            var city = new CitySimulator(ResumablePlanningTests.City(12), new ResumablePlanningTests.DeferredPlanner { ContinuesFor = _ => 3 });
            AdvanceResult result;
            int guard = 0;
            do { Assert.True(++guard < 100000); result = city.AdvanceFrameSlice(ResumablePlanningTests.Target(3), 1); } while (!result.PlanningPending && !result.CaughtUp);
            Assert.True(result.PlanningPending);
            Assert.Equal(GcAllocationSegment.AdvanceEventsRouteBegin,
                GcPauseSampler.AdvanceEventSegment(result.TrafficEvents, result.FoodContinuationEvents, result.CitizenEvents, result.PlanningPending));
        }

        [Fact]
        public void RouteRebuildWorkBeforeTheSpeedRefreshIsNotChargedToSpeeds()
        {
            var sampler = new GcPauseSampler(64);
            sampler.Start(1);
            var recorder = sampler.FrameRecorder;
            recorder.Arm(1000);
            recorder.Begin(1, 0, 0, 0);
            recorder.Charge(GcAllocationSegment.ModOptionalStops, 1, 100, 0);
            recorder.Detail(GcAllocationSegment.ModRoutesRebuild, 300, 900_100, 0);
            recorder.Detail(GcAllocationSegment.ModRoutesSpeeds, 301, 900_148, 0);
            recorder.Detail(GcAllocationSegment.ModRoutesNodes, 302, 900_150, 0);
            recorder.Detail(GcAllocationSegment.ModRoutesSweep, 310, 900_400, 0);
            recorder.Charge(GcAllocationSegment.ModRoutes, 311, 900_400, 0);
            recorder.Detail(GcAllocationSegment.PhysicalSpawnGrow, 312, 900_500, 0);
            recorder.End(313, 900_500, 0, sampler);
            Assert.Equal(900_000, sampler.SegmentBytes(GcAllocationSegment.ModRoutesRebuild));
            Assert.Equal(48, sampler.SegmentBytes(GcAllocationSegment.ModRoutesSpeeds));
            Assert.Equal(900_300, sampler.SegmentBytes(GcAllocationSegment.ModRoutes));
            var row = recorder.Top(0);
            Assert.Equal((int)GcAllocationSegment.ModRoutes, row.Phase);
            Assert.Equal((int)GcAllocationSegment.PhysicalSpawnGrow, row.Detail);
            Assert.Equal((int)GcAllocationSegment.ModRoutesRebuild, row.Sub);
            Assert.Equal(299, row.SubMs, 3);
            Assert.Contains("\"detail\":\"physical_spawn_grow\"", recorder.ReportJson());
            Assert.Contains("\"sub\":\"mod_routes_rebuild\"", recorder.ReportJson());
        }

        [Fact]
        public void ModSubSegmentsReachTheSamplerThroughTheFrameRecorder()
        {
            var sampler = new GcPauseSampler(64);
            sampler.Start(1);
            var recorder = sampler.FrameRecorder;
            recorder.Arm(1000);
            recorder.Begin(1, 0, 0, 0);
            recorder.Charge(GcAllocationSegment.ModOptionalStops, 1, 100, 0);
            recorder.Detail(GcAllocationSegment.ModRoutesSpeeds, 2, 1100, 0);
            recorder.Detail(GcAllocationSegment.ModRoutesNodes, 3, 1150, 0);
            recorder.Detail(GcAllocationSegment.ModRoutesSweep, 4, 1160, 0);
            recorder.Charge(GcAllocationSegment.ModRoutes, 5, 1200, 0);
            recorder.Detail(GcAllocationSegment.ModClosuresNetwork, 6, 1300, 0);
            recorder.Detail(GcAllocationSegment.ModClosuresLinks, 7, 1700, 0);
            recorder.Charge(GcAllocationSegment.ModClosures, 8, 1750, 0);
            recorder.End(9, 1750, 0, sampler);
            Assert.Equal(1100, sampler.SegmentBytes(GcAllocationSegment.ModRoutes));
            Assert.Equal(1000, sampler.SegmentBytes(GcAllocationSegment.ModRoutesSpeeds));
            Assert.Equal(50, sampler.SegmentBytes(GcAllocationSegment.ModRoutesNodes));
            Assert.Equal(10, sampler.SegmentBytes(GcAllocationSegment.ModRoutesSweep));
            Assert.Equal(550, sampler.SegmentBytes(GcAllocationSegment.ModClosures));
            Assert.Equal(100, sampler.SegmentBytes(GcAllocationSegment.ModClosuresNetwork));
            Assert.Equal(400, sampler.SegmentBytes(GcAllocationSegment.ModClosuresLinks));
            Assert.Equal(1750, recorder.Top(0).Bytes);
            Assert.Equal((int)GcAllocationSegment.ModRoutes, recorder.Top(0).AllocPhase);
        }

        [Fact]
        public void ASlowedLaneRoadSpeedFrameReusesItsBuffersAndResolvesTheSameSpeeds()
        {
            RealLifeOverhaul.GoingPublic.InSession = false;
            var rlo = StandIn.Build("0.99.0");
            var reviewed = new RloRoadSpeedOverrides(rlo);
            var lanes = Enumerable.Range(0, 12).Select(i => new GleyTrafficSystem.Waypoint { maxSpeed = 35 + i }).ToArray();
            CityApiHooks.Lanes = lanes.Select((lane, i) => ((object)lane, 50 + i, 35 + i)).ToList();
            var copied = new NativeRoadSpeeds(() => NativeRoadSpeeds.Copy(reviewed.Capture(), pair => (pair.OriginalKilometresPerHour, pair.EffectiveKilometresPerHour)));
            var reused = new NativeRoadSpeeds(NativeRoadSpeeds.Reusing<RloRoadSpeedOverride>(reviewed.CaptureInto, RloRoadSpeedOverrides.NewBuffer,
                pair => (pair.OriginalKilometresPerHour, pair.EffectiveKilometresPerHour)));
            long Frame(NativeRoadSpeeds speeds)
            {
                for (int i = 0; i < 16; i++) speeds.Refresh();
                long before = GC.GetAllocatedBytesForCurrentThread();
                const int frames = 1000;
                for (int i = 0; i < frames; i++) speeds.Refresh();
                Assert.True(speeds.Verified);
                return (GC.GetAllocatedBytesForCurrentThread() - before) / frames;
            }
            long old = Frame(copied), now = Frame(reused);
            output.WriteLine("road-speed frame with 12 slowed lanes: before (capture + read-only wrapper + copy) " + old + " B, after (reused buffers) " + now
                + " B; per 600 s at 60 fps: " + old * 36000 / 1000 + " KB -> " + now * 36000 / 1000 + " KB");
            Assert.True(now <= 64, now.ToString());
            Assert.True(now * 8 < old, now + " vs " + old);

            var snapshot = reviewed.Capture();
            Assert.Equal(12, snapshot.Count);
            CityApiHooks.Lanes = new List<(object, int, int)>();
            reused.Refresh(); copied.Refresh();
            Assert.False(reused.HasOverrides); Assert.False(copied.HasOverrides);
            Assert.Equal(12, snapshot.Count);
            CityApiHooks.Lanes = new List<(object, int, int)> { (lanes[0], 50, 35), (lanes[0], 50, 35) };
            Assert.Throws<InvalidOperationException>(() => reused.Refresh());
            lanes[1].maxSpeed = 25; CityApiHooks.Lanes = new List<(object, int, int)> { (lanes[1], 51, 25) };
            reused.Refresh();
            Assert.True(reused.Verified && reused.HasOverrides);
        }
    }

    [CollectionDefinition(nameof(Gr5SubSegmentChargeTests), DisableParallelization = true)]
    public sealed class Gr5SubSegmentChargeCollection { }

    [Collection(nameof(Gr5SubSegmentChargeTests))]
    public sealed class Gr5SubSegmentChargeTests
    {
        [Fact]
        public void SubSegmentChargesAllocateNothing()
        {
            GcAllocationCounter.Probe();
            var sampler = new GcPauseSampler(64);
            sampler.Start(1, GcAllocationCounter.Probe());
            sampler.FrameRecorder.Arm(System.Diagnostics.Stopwatch.Frequency);
            void Frame(long n)
            {
                ModFrames.Begin(sampler, n);
                ModFrames.Detail(GcAllocationSegment.ModRoutesSpeeds);
                ModFrames.Charge(GcAllocationSegment.ModRoutes);
                AdvanceAllocation.Begin(sampler);
                AdvanceAllocation.Sub(GcAllocationSegment.AdvanceEventsTraffic);
                AdvanceAllocation.Detail(GcAllocationSegment.AdvanceEvents);
                AdvanceAllocation.End();
                PhysicalAllocation.Begin(sampler);
                PhysicalAllocation.Detail(GcAllocationSegment.PhysicalSetupBodies);
                PhysicalAllocation.End(GcAllocationSegment.PhysicalUnattributedSetup);
                ModFrames.End();
            }
            for (int i = 0; i < 64; i++) Frame(i);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 64; i < 4096; i++) Frame(i);
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
            Assert.True(sampler.SegmentSamples(GcAllocationSegment.PhysicalUnattributedSetup) >= 4096);
        }
    }
}
