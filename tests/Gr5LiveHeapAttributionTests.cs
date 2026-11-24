using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

[Collection(StreetFoodHooks.Collection)]
public sealed class Gr5LiveHeapAttributionTests
{
    private static CitizenState Traveller(string id, Route journey) => new CitizenState
    {
        Identity = new CitizenIdentity { Id = id, Name = id }, Phase = CitizenPhase.Travelling, Journey = journey, PlaceId = "a", DestinationId = "b",
    };

    private static Route Committed(double length)
    {
        var route = new Route { Legs =
        {
            new RouteLeg { From = new Position(0, 0, 0), To = new Position(length, 0, 0), Mode = TravelMode.Walk, Minutes = length / 80 },
            new RouteLeg { From = new Position(length, 0, 0), To = new Position(length, 0, length / 2), Mode = TravelMode.Walk, Minutes = length / 160 },
        } };
        return route.Commit();
    }

    [Fact]
    public void ARetimedRoutesPrecomputedCellsAreReleasedOnceTheGridHasCopiedThemAndRefilingRecomputesTheSameCells()
    {
        var journey = Committed(5_000);
        PresentationGrid.Precompute(journey);
        Assert.NotNull(journey.PresentationCells);
        int cells = journey.PresentationCells!.Length;
        var traveller = Traveller("r1", journey);
        var first = new PresentationGrid();
        first.File(traveller);
        Assert.Null(journey.PresentationCells);
        Assert.Equal(cells, first.Registrations);
        Assert.True(first.Matches(new[] { traveller }, true));
        var rebuilt = new PresentationGrid();
        rebuilt.File(traveller);
        Assert.Equal(cells, rebuilt.Registrations);
        Assert.True(rebuilt.Matches(new[] { traveller }, true));
        var far = Committed(1e9);
        PresentationGrid.Precompute(far);
        Assert.NotNull(far.PresentationCells);
        var lost = Traveller("r2", far);
        rebuilt.File(lost);
        Assert.Null(far.PresentationCells);
        Assert.Equal(1, rebuilt.Unplaced);
        var again = new PresentationGrid();
        again.File(lost);
        Assert.Equal(1, again.Unplaced);
    }

    [Fact]
    public void CollectionRowsCarryTheBodyCountsNotedAtTheBoundaryThatSeesTheCollectionAndNotingAllocatesNothing()
    {
        static void Run(GcPauseSampler target, ref int collections)
        {
            for (int frame = 1; frame <= 2000; frame++)
            {
                target.NoteBodies(frame / 40, frame / 80);
                if (frame % 500 == 0) collections++;
                target.Observe(frame * 16.0, collections, 0, 0, 1000 + frame, frame);
            }
        }
        var warm = new GcPauseSampler(4096);
        warm.Start(1000);
        warm.Observe(0, 0, 0, 0, 1000, 0);
        int warmed = 0;
        Run(warm, ref warmed);
        var sampler = new GcPauseSampler(4096);
        sampler.Start(1000);
        sampler.Observe(0, 0, 0, 0, 1000, 0);
        int gen0 = 0;
        long before = GC.GetAllocatedBytesForCurrentThread();
        Run(sampler, ref gen0);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.Equal(4, sampler.CollectionRowCount);
        for (int i = 0; i < 4; i++)
        {
            int frame = 500 * (i + 1);
            Assert.Equal((frame / 40, frame / 80), (sampler.CollectionRow(i).OwnedBodies, sampler.CollectionRow(i).BoundBodies));
        }
        using var report = JsonDocument.Parse(sampler.ReportJson(new GcReportContext(1000, 2000, null, 2, false, -1, -1)));
        var row = report.RootElement.GetProperty("collection_rows")[3];
        Assert.Equal(50, row.GetProperty("owned_bodies").GetInt32());
        Assert.Equal(25, row.GetProperty("bound_bodies").GetInt32());
        sampler.Start(1000);
        sampler.Observe(0, 0, 0, 0, 1000, 0);
        sampler.Observe(16, 1, 0, 0, 900, 1);
        Assert.Equal((-1, -1), (sampler.CollectionRow(0).OwnedBodies, sampler.CollectionRow(0).BoundBodies));
    }

    [Fact]
    public void CollectionRowsCountTheUnityWorkRequestsTheModMadeDuringThatFrame()
    {
        var sampler = new GcPauseSampler(64);
        sampler.Start(1000);
        sampler.Observe(0, 0, 0, 0, 1000, 0);
        sampler.Observe(16, 1, 0, 0, 900, 1);
        Assert.Equal((-1L, -1L, -1L), (sampler.CollectionRow(0).Instantiated, sampler.CollectionRow(0).Destroyed, sampler.CollectionRow(0).Activated));
        sampler.Start(1000);
        int gen0 = 0;
        for (int frame = 0; frame <= 12; frame++)
        {
            sampler.NoteUnityWork(frame * 2L, frame == 8 ? 40 : frame, frame < 8 ? 3L * frame : 3L * frame + 5, frame, frame < 9 ? frame : frame + 2, 4L * frame);
            if (frame == 7 || frame == 9) gen0++;
            sampler.Observe(frame * 16.0, gen0, 0, 0, 1000 + frame, frame);
        }
        Assert.Equal(2, sampler.CollectionRowCount);
        var first = sampler.CollectionRow(0);
        Assert.Equal((6L, 2L, 1L, 3L), (first.Frame, first.Instantiated, first.Destroyed, first.Activated));
        var second = sampler.CollectionRow(1);
        Assert.Equal((8L, 2L, -1L, 3L), (second.Frame, second.Instantiated, second.Destroyed, second.Activated));
        Assert.Equal((1L, 1L, 4L), (first.AsyncStarted, first.AsyncCompleted, first.MeshRequests));
        Assert.Equal((1L, 3L, 4L), (second.AsyncStarted, second.AsyncCompleted, second.MeshRequests));
        using var report = JsonDocument.Parse(sampler.ReportJson(new GcReportContext(1000, 2000, null, 2, false, -1, -1)));
        var rows = report.RootElement.GetProperty("collection_rows");
        Assert.Equal(2, rows[0].GetProperty("mod_instantiate_requests").GetInt64());
        Assert.Equal(1, rows[0].GetProperty("mod_destroy_requests").GetInt64());
        Assert.Equal(3, rows[0].GetProperty("mod_activate_requests").GetInt64());
        Assert.Equal(1, rows[0].GetProperty("mod_async_instantiate_completions").GetInt64());
        Assert.Equal(4, rows[0].GetProperty("mod_mesh_requests").GetInt64());
        Assert.Equal(JsonValueKind.Null, rows[1].GetProperty("mod_destroy_requests").ValueKind);
        var work = report.RootElement.GetProperty("mod_unity_work");
        Assert.Equal(("requests", "unavailable", "observed_when_done", "unavailable"), (work.GetProperty("counts").GetString(), work.GetProperty("destroy_completion").GetString(),
            work.GetProperty("async_instantiate_completion").GetString(), work.GetProperty("mesh_upload_completion").GetString()));
    }

    [Fact]
    public void OptionalStopWorkIsSplitIntoRefreshAndPhysicalSubsUnderTheOptionalStopPhase()
    {
        Assert.Equal(GcAllocationSegment.ModOptionalStops, GcPauseSampler.SubParent(GcAllocationSegment.ModOptionalStopsRefresh));
        Assert.Equal(GcAllocationSegment.ModOptionalStops, GcPauseSampler.SubParent(GcAllocationSegment.ModOptionalStopsPhysical));
        Assert.True(GcPauseSampler.IsSub(GcAllocationSegment.ModOptionalStopsRefresh) && GcPauseSampler.IsOuter(GcAllocationSegment.ModOptionalStopsRefresh));
        Assert.Equal(GcAllocationSegment.ModRoutes, GcPauseSampler.SubParent(GcAllocationSegment.ModRoutesRetime));
        Assert.Equal(GcAllocationSegment.PhysicalUnattributed, GcPauseSampler.SubParent(GcAllocationSegment.PhysicalUnattributedEntry));
        Assert.Equal("mod_optional_stops_refresh", GcPauseSampler.SegmentName(GcAllocationSegment.ModOptionalStopsRefresh));
        Assert.Equal("mod_optional_stops_physical", GcPauseSampler.SegmentName(GcAllocationSegment.ModOptionalStopsPhysical));
        Assert.Equal(GcPauseSampler.Segments, Enum.GetValues(typeof(GcAllocationSegment)).Length);
        var sampler = new GcPauseSampler(64);
        sampler.Start(1);
        var recorder = sampler.FrameRecorder;
        recorder.Arm(1000);
        recorder.Begin(1, 0, 0, 0);
        recorder.Charge(GcAllocationSegment.ModFood, 1, 100, 0);
        recorder.Detail(GcAllocationSegment.ModOptionalStopsRefresh, 41, 2_100, 1);
        recorder.Detail(GcAllocationSegment.ModOptionalStopsPhysical, 43, 2_400, 1);
        recorder.Charge(GcAllocationSegment.ModOptionalStops, 44, 2_400, 1);
        recorder.End(45, 2_400, 1, sampler);
        Assert.Equal(2_000, sampler.SegmentBytes(GcAllocationSegment.ModOptionalStopsRefresh));
        Assert.Equal(300, sampler.SegmentBytes(GcAllocationSegment.ModOptionalStopsPhysical));
        Assert.Equal(2_300, sampler.SegmentBytes(GcAllocationSegment.ModOptionalStops));
        var row = recorder.Top(0);
        Assert.Equal((int)GcAllocationSegment.ModOptionalStops, row.Phase);
        Assert.Equal((int)GcAllocationSegment.ModOptionalStopsRefresh, row.Sub);
        Assert.Equal(40, row.SubMs, 3);
        string json = recorder.ReportJson();
        Assert.Contains("\"sub\":\"mod_optional_stops_refresh\"", json);
        Assert.Contains("\"gc_phase\":\"mod_optional_stops\"", json);
    }

    private sealed class RuntimeShape
    {
        public GcPauseSampler? gcSampler;
        public List<int[]>? simulation;
    }

    [Fact]
    public void TheCensusReportsTheInstrumentsOwnRootsApartFromTheCity()
    {
        var runtime = new RuntimeShape { gcSampler = new GcPauseSampler(8192), simulation = Enumerable.Range(0, 10).Select(_ => new int[1000]).ToList() };
        var census = new RetainedCensus();
        census.AddOwnedEntries(new Dictionary<string, string> { ["persistentcity:state_v1"] = new string('s', 5000) }, "persistentcity:", "save.modData.");
        census.AddFields("runtime.", runtime, null);
        census.Add("static.RetainedCensus.Layouts", new Dictionary<int, int[]> { [1] = new int[64] });
        census.Add("static.BlendshapeBake.scratch", new float[256]);
        var roots = census.Roots.ToDictionary(r => r.Name, r => r);
        var instruments = census.Diagnostics(RetainedCensus.InstrumentRoot);
        Assert.True(roots["runtime.gcSampler"].Bytes > 8192 * 9, "sampler " + roots["runtime.gcSampler"].Bytes);
        long expected = census.Roots.Where(r => RetainedCensus.InstrumentRoot(r.Name)).Sum(r => r.Bytes);
        Assert.Equal(expected, instruments.Bytes);
        Assert.True(instruments.Bytes >= roots["runtime.gcSampler"].Bytes);
        Assert.False(RetainedCensus.InstrumentRoot("runtime.simulation"));
        Assert.False(RetainedCensus.InstrumentRoot("save.modData.persistentcity:state_v1"));
        Assert.False(RetainedCensus.InstrumentRoot("static.BlendshapeBake.scratch"));
        Assert.True(RetainedCensus.InstrumentRoot("static.RetainedCensus.Layouts"));
        using var json = JsonDocument.Parse(census.ReportJson(8, 0, new[] { typeof(CitySimulator) }, RetainedCensus.InstrumentRoot));
        var root = json.RootElement;
        Assert.Equal(instruments.Bytes, root.GetProperty("diagnostic_bytes").GetInt64());
        Assert.Equal(census.Bytes - instruments.Bytes, root.GetProperty("city_bytes").GetInt64());
        Assert.Equal(census.Objects - instruments.Objects, root.GetProperty("city_objects").GetInt64());
        Assert.Contains("runtime.gcSampler", root.GetProperty("diagnostic_roots").EnumerateArray().Select(e => e.GetString()));
        Assert.DoesNotContain("runtime.simulation", root.GetProperty("diagnostic_roots").EnumerateArray().Select(e => e.GetString()));
        Assert.True(root.GetProperty("city_bytes").GetInt64() >= roots["runtime.simulation"].Bytes + roots["save.modData.persistentcity:state_v1"].Bytes);
        using var plain = JsonDocument.Parse(census.ReportJson(8, 0, new[] { typeof(CitySimulator) }));
        Assert.False(plain.RootElement.TryGetProperty("diagnostic_bytes", out _));
    }
}
