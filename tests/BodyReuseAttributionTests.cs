using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using PersistentCity.Core;
using PersistentCity.Runtime;
using UnityEngine;
using UnityEngine.AI;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

[Collection(StreetFoodHooks.Collection)]
public sealed class BodyReuseAttributionTests
{
    private const long Rig14jBytesPerBuild = 56698832L / 358;
    private const long Rig14jBytesPerGrow = 30124416L / 88;
    private readonly ITestOutputHelper output;
    public BodyReuseAttributionTests(ITestOutputHelper output) { this.output = output; }

    private sealed class Body { public PersonDressing Dressing; public PersonLook Look; }

    private static CitizenIdentity Identity(int i) => new CitizenIdentity
    { Id = "p" + i.ToString(CultureInfo.InvariantCulture), Name = "N" + i.ToString(CultureInfo.InvariantCulture), Age = 20 + i % 60, Female = i % 2 == 0, AppearanceSeed = 1 + i * 7 };

    [Fact]
    public void ASpikeFrameNamesItsInnerPhaseItsCollectionAndItsAllocation()
    {
        var recorder = new ModFrameRecorder();
        recorder.Begin(1, 0, 0, 0);
        Assert.False(recorder.Open);
        recorder.Arm(1000);
        recorder.Begin(51358, 0, 0, 0);
        recorder.Charge(GcAllocationSegment.ModPrelude, 1, 100, 0);
        recorder.Charge(GcAllocationSegment.ModRoutes, 2, 600, 0);
        recorder.Charge(GcAllocationSegment.AdvanceEvents, 4, 2600, 0);
        recorder.Charge(GcAllocationSegment.AdvanceOther, 4, 2600, 0);
        recorder.Charge(GcAllocationSegment.PhysicalSelection, 5, 2700, 0);
        recorder.MarkDetail(6, 2800, 0);
        recorder.Detail(GcAllocationSegment.PhysicalSpawnGrow, 66, 302800, 1);
        recorder.Charge(GcAllocationSegment.PhysicalSpawnPedestrian, 67, 302900, 1);
        recorder.Charge(GcAllocationSegment.PhysicalUnattributed, 68, 303000, 1);
        recorder.Charge(GcAllocationSegment.ModInspector, 69, 303000, 1);
        recorder.End(70, 303100, 1, null);
        Assert.Equal(1, recorder.Spikes);
        var row = recorder.Spike(0);
        Assert.Equal(51358, row.Frame);
        Assert.Equal(70, row.ModMs, 6);
        Assert.Equal(GcAllocationSegment.PhysicalSpawnPedestrian, (GcAllocationSegment)row.Phase);
        Assert.Equal(62, row.PhaseMs, 6);
        Assert.Equal(GcAllocationSegment.PhysicalSpawnGrow, (GcAllocationSegment)row.Detail);
        Assert.Equal(60, row.DetailMs, 6);
        Assert.Equal(GcAllocationSegment.PhysicalSpawnPedestrian, (GcAllocationSegment)row.GcPhase);
        Assert.Equal(1, row.Collections);
        Assert.Equal(GcAllocationSegment.PhysicalSpawnPedestrian, (GcAllocationSegment)row.AllocPhase);
        Assert.Equal(300200, row.AllocBytes);
        Assert.Equal(303100, row.Bytes);
        Assert.Equal(1, recorder.Frames);

        using var report = JsonDocument.Parse(recorder.ReportJson());
        var spike = report.RootElement.GetProperty("spike_frames")[0];
        Assert.Equal("physical_spawn_pedestrian", spike.GetProperty("phase").GetString());
        Assert.Equal("physical_spawn_grow", spike.GetProperty("detail").GetString());
        Assert.Equal("physical_spawn_pedestrian", spike.GetProperty("gc_phase").GetString());
        Assert.Equal(51358, report.RootElement.GetProperty("top_frames")[0].GetProperty("frame").GetInt64());
        Assert.Equal(62, report.RootElement.GetProperty("phase_ms").GetProperty("physical_spawn_pedestrian").GetDouble(), 3);
        Assert.Contains("frame 51358 70.0 ms physical_spawn_pedestrian gc@physical_spawn_pedestrian", recorder.ReportHuman());
    }

    [Fact]
    public void TopFramesKeepTheSlowestInOrderAndSpikesAreCountedPastTheirBuffer()
    {
        var dice = new Random(4);
        var all = new List<double>();
        for (int frame = 0; frame < 2000; frame++)
            all.Add(Math.Round((frame % 20 == 0 ? 30 + dice.Next(50) : dice.NextDouble() * 5) * 10) / 10.0);
        var recorder = new ModFrameRecorder();
        recorder.Arm(10000);
        long at = 0;
        foreach (double ms in all)
        {
            long ticks = (long)Math.Round(ms * 10);
            recorder.Begin(at, at, 0, 0);
            recorder.Charge(GcAllocationSegment.PhysicalBindings, at + ticks, 0, 0);
            recorder.End(at + ticks, 0, 0, null);
            at += ticks + 1;
        }
        var expected = all.OrderByDescending(x => x).Take(ModFrameRecorder.TopFrames).ToArray();
        Assert.Equal(ModFrameRecorder.TopFrames, recorder.TopCount);
        for (int i = 0; i < recorder.TopCount; i++) Assert.Equal(expected[i], recorder.Top(i).ModMs, 6);
        Assert.Equal(all.Count(x => x >= 30), recorder.Spikes);
        Assert.Equal(Math.Min(ModFrameRecorder.SpikeCapacity, all.Count(x => x >= 30)), recorder.SpikeCount);
        recorder.Reset();
        Assert.Equal(0, recorder.TopCount); Assert.Equal(0, recorder.Spikes);
    }

    [Fact]
    public void OuterModSegmentsPartitionTheModRemainderWithoutTouchingPhysicalOrAdvance()
    {
        var sampler = new GcPauseSampler(64);
        sampler.Start(1);
        sampler.FrameRecorder.Arm(1000);
        long counter = 0;
        for (int frame = 0; frame < 10; frame++)
        {
            long start = counter;
            sampler.FrameRecorder.Begin(frame, frame * 100, counter, 0);
            counter += 700; sampler.FrameRecorder.Charge(GcAllocationSegment.ModPrelude, frame * 100 + 1, counter, 0);
            counter += 4000; sampler.FrameRecorder.Charge(GcAllocationSegment.ModRlo, frame * 100 + 2, counter, 0);
            counter += 50; sampler.FrameRecorder.Charge(GcAllocationSegment.ModClosures, frame * 100 + 3, counter, 0);
            sampler.BeginNested(counter, 0);
            counter += 900; sampler.Detail(GcAllocationSegment.AdvanceEvents, counter, 0); sampler.FrameRecorder.Charge(GcAllocationSegment.AdvanceEvents, frame * 100 + 4, counter, 0);
            sampler.EndDetails(GcAllocationSegment.AdvanceOther, counter, 0); sampler.FrameRecorder.Charge(GcAllocationSegment.AdvanceOther, frame * 100 + 4, counter, 0);
            sampler.Allocated(GcAllocationSegment.Advance, counter - 900, counter, false);
            sampler.FrameRecorder.Charge(GcAllocationSegment.ModPhysicalSetup, frame * 100 + 5, counter, 0);
            sampler.BeginNested(counter, 0);
            counter += 2500; sampler.Charge(GcAllocationSegment.PhysicalSpawnAppearance, counter, 0); sampler.FrameRecorder.Charge(GcAllocationSegment.PhysicalSpawnAppearance, frame * 100 + 6, counter, 0);
            sampler.EndNested(GcAllocationSegment.Physical, GcAllocationSegment.PhysicalUnattributed, counter, 0); sampler.FrameRecorder.Charge(GcAllocationSegment.PhysicalUnattributed, frame * 100 + 6, counter, 0);
            counter += 30; sampler.FrameRecorder.Charge(GcAllocationSegment.ModSummary, frame * 100 + 7, counter, 0);
            counter += 20;
            sampler.FrameRecorder.End(frame * 100 + 8, counter, 0, sampler);
            sampler.Allocated(GcAllocationSegment.Mod, start, counter, false);
        }
        long outer = 0;
        for (int i = (int)GcAllocationSegment.ModPrelude; i < GcPauseSampler.Segments; i++) if (!GcPauseSampler.IsSub((GcAllocationSegment)i)) outer += sampler.SegmentBytes((GcAllocationSegment)i);
        Assert.Equal(sampler.SegmentBytes(GcAllocationSegment.Mod),
            outer + sampler.SegmentBytes(GcAllocationSegment.Physical) + sampler.SegmentBytes(GcAllocationSegment.Advance));
        Assert.Equal(40000, sampler.SegmentBytes(GcAllocationSegment.ModRlo));
        Assert.Equal(200, sampler.SegmentBytes(GcAllocationSegment.ModOther));
        Assert.Equal(25000, sampler.SegmentBytes(GcAllocationSegment.Physical));
        Assert.Equal(9000, sampler.SegmentBytes(GcAllocationSegment.Advance));
        Assert.Equal(10, sampler.SegmentSamples(GcAllocationSegment.ModRlo));
        Assert.Equal(0, sampler.SegmentSamples(GcAllocationSegment.ModFood));
        using var report = JsonDocument.Parse(sampler.ReportJson(new GcReportContext(1, 1, null, 0, false, 1, 1)));
        Assert.Equal(40000, report.RootElement.GetProperty("mod_rlo_alloc_bytes").GetInt64());
        Assert.Equal(10, report.RootElement.GetProperty("top_frames").GetArrayLength());
        Assert.Equal("mod_rlo", report.RootElement.GetProperty("top_frames")[0].GetProperty("alloc_phase").GetString());
        Assert.Equal(8200, report.RootElement.GetProperty("top_frames")[0].GetProperty("alloc_bytes").GetInt64());
    }

    [Fact]
    public void FrameRecordingAllocatesNothingAndIsOffUntilArmed()
    {
        GcAllocationCounter.Probe();
        var sampler = new GcPauseSampler(64);
        Assert.False(ModFrames.Begin(null, 1));
        Assert.False(ModFrames.Begin(sampler, 1));
        ModFrames.Charge(GcAllocationSegment.ModPrelude);
        ModFrames.End();
        Assert.Equal(0, sampler.FrameRecorder.Frames);
        sampler.Start(1, GcAllocationCounter.Probe());
        sampler.FrameRecorder.Arm(System.Diagnostics.Stopwatch.Frequency);
        void Frame(int frame)
        {
            ModFrames.Begin(sampler, frame);
            for (int i = (int)GcAllocationSegment.ModPrelude; i < GcPauseSampler.Segments; i++) ModFrames.Charge((GcAllocationSegment)i);
            PhysicalAllocation.Begin(sampler);
            for (int i = (int)GcAllocationSegment.PhysicalAmbient; i <= (int)GcAllocationSegment.PhysicalSpawnBake; i++) PhysicalAllocation.Charge((GcAllocationSegment)i);
            PhysicalAllocation.Mark(); PhysicalAllocation.Detail(GcAllocationSegment.PhysicalSpawnGrow);
            PhysicalAllocation.End();
            ModFrames.End();
        }
        for (int frame = 0; frame < 64; frame++) Frame(frame);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int frame = 64; frame < 4096; frame++) Frame(frame);
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
        Assert.Equal(4096, sampler.FrameRecorder.Frames);
        Assert.Equal(4096, sampler.SegmentSamples(GcAllocationSegment.Physical));
        Assert.Equal(0, sampler.SegmentBytes(GcAllocationSegment.ModRlo));
    }

    [Fact]
    public void HintedBodiesKeepPeopleWhoComeBackAndRedressThoseWhoDoNot()
    {
        var pool = new DressedBodyPool<string>(spare: 2) { Minute = 20 };
        PersonDressing D(int i) => new PersonDressing(Identity(i), 60);
        pool.Return("shop-at-100", D(1), 0, 100);
        pool.Return("gone-home", D(2), 1, double.PositiveInfinity);
        pool.Return("shop-at-30", D(3), 2, 30);
        Assert.Equal(BodyChoice.Redress, pool.Choose(D(4), 3, 8, null, 0, 0, out var body));
        Assert.Equal("gone-home", body);
        Assert.Equal(BodyChoice.Redress, pool.Choose(D(5), 3, 3, null, 0, 0, out body));
        Assert.Equal("shop-at-100", body);
        Assert.Equal(BodyChoice.Hit, pool.Choose(D(3), 3, 3, null, 0, 0, out body));
        Assert.Equal("shop-at-30", body);
        pool.Return("shop-at-30", D(3), 3, 30);
        pool.Return("later", D(6), 4, 200);
        Assert.Equal(BodyChoice.Redress, pool.Choose(D(7), 4, 8, null, 0, 0, out body));
        Assert.Equal("later", body);
        pool.Return("later", D(6), 4, 200);
        var roomy = new DressedBodyPool<string>(spare: 3) { Minute = 20 };
        roomy.Return("a", D(1), 0, 40); roomy.Return("b", D(2), 0, 50);
        Assert.Equal(BodyChoice.Grow, roomy.Choose(D(7), 2, 8, null, 0, 0, out _));
        Assert.Equal(BodyChoice.Redress, roomy.Choose(D(7), 8, 8, null, 0, 0, out body));
        Assert.Equal("b", body);
        pool.Minute = 30 + DressedBodyPool<string>.StaleMinutes + 1;
        Assert.True(pool.Expendable(0));
        Assert.False(pool.Expendable(1));
        Assert.Equal(BodyChoice.Redress, pool.Choose(D(8), 4, 8, null, 0, 0, out body));
        Assert.Equal("shop-at-30", body);

        var trim = new DressedBodyPool<string>(spare: 1) { Minute = 0 };
        trim.Return("valuable", D(1), 0, 50);
        trim.Return("expendable", D(2), 10, double.PositiveInfinity);
        Assert.False(trim.TryTrim(100, 120, out _));
        Assert.True(trim.TryTrim(131, 120, out var trimmed));
        Assert.Equal("expendable", trimmed);
        Assert.False(trim.TryTrim(131, 120, out _));
        trim.Return("undressed", default, 0, 5);
        Assert.True(double.IsNaN(trim.WantedAt(1)));
    }

    [Fact]
    public void TheSameResidentGetsTheSameLookThroughHintedReleaseEvictionReserveAndRebuild()
    {
        var pool = new DressedBodyPool<Body>(DressedBodyPool<Body>.StreetSpare);
        var first = new Dictionary<string, (PersonDressing Dressing, PersonLook Look)>(StringComparer.Ordinal);
        var dice = new Random(23);
        var held = new List<(CitizenIdentity Identity, Body Body)>();
        int owned = 0, hits = 0, rebuilds = 0;
        for (int step = 0; step < 30000; step++)
        {
            pool.Minute = step / 10.0;
            if (step % 5000 == 4999)
            {
                var kept = new List<(Body Body, PersonDressing Dressing, double Wanted)>();
                foreach (var h in held) pool.Return(h.Body, h.Body.Dressing, step, double.PositiveInfinity);
                held.Clear();
                for (int i = 0; i < pool.Count; i++) kept.Add((pool[i], pool.DressingAt(i), pool.WantedAt(i)));
                pool = new DressedBodyPool<Body>(DressedBodyPool<Body>.StreetSpare) { Minute = pool.Minute };
                foreach (var k in kept) pool.Return(k.Body, k.Dressing, step, k.Wanted);
            }
            if (held.Count > 0 && (held.Count >= 24 || dice.Next(2) == 0))
            {
                int at = dice.Next(held.Count);
                double wanted = dice.Next(3) switch { 0 => double.PositiveInfinity, 1 => pool.Minute + dice.Next(60), _ => pool.Minute + 10 };
                pool.Return(held[at].Body, held[at].Body.Dressing, step, wanted);
                held.RemoveAt(at);
                continue;
            }
            var identity = Identity(dice.Next(120));
            if (held.Any(h => h.Identity.Id == identity.Id)) continue;
            var dressing = new PersonDressing(identity, 60);
            var choice = pool.Choose(dressing, owned, 48, null, step, step, out var body);
            if (choice == BodyChoice.Full) continue;
            if (choice == BodyChoice.Grow) { body = new Body(); owned++; }
            if (choice == BodyChoice.Hit) hits++;
            else { body.Dressing = dressing; body.Look = PersonAppearance.For(identity); rebuilds++; }
            if (!first.TryGetValue(identity.Id, out var seen)) first[identity.Id] = seen = (dressing, body.Look);
            Assert.True(seen.Dressing.Matches(body.Dressing));
            Assert.Equal(seen.Look.Seed, body.Look.Seed);
            Assert.Equal(seen.Look.Wardrobe, body.Look.Wardrobe);
            held.Add((identity, body));
        }
        Assert.True(hits > rebuilds / 4, hits + " hits / " + rebuilds + " rebuilds");
        Assert.True(owned <= 48);
    }

    [Fact]
    public void TheHintedPoolSpawnPathAllocatesNothingAfterWarmUp()
    {
        var pool = new DressedBodyPool<Body>(DressedBodyPool<Body>.StreetSpare);
        var budget = new DressBuildBudget(1, 1000, 1000);
        var dressings = Enumerable.Range(0, 96).Select(i => new PersonDressing(Identity(i), 60)).ToArray();
        for (int i = 0; i < 48; i++) pool.Return(new Body { Dressing = dressings[i] }, dressings[i], i, i % 3 == 0 ? double.PositiveInfinity : i);
        var held = new Body?[dressings.Length];
        int frame = 0;
        void Cycle(int rounds)
        {
            for (int round = 0; round < rounds; round++)
                for (int i = round % 5; i < dressings.Length; i += 5)
                {
                    frame++;
                    pool.Minute = frame / 30.0;
                    if (held[i] != null) { pool.Return(held[i]!, held[i]!.Dressing, frame, (i & 1) == 0 ? pool.Minute + 20 : double.PositiveInfinity); held[i] = null; continue; }
                    var choice = pool.Choose(dressings[i], 48, 48, budget, frame, frame, out var body);
                    if (choice == BodyChoice.Hit || choice == BodyChoice.Redress) { body.Dressing = dressings[i]; held[i] = body; }
                }
        }
        Cycle(40);
        long before = GC.GetAllocatedBytesForCurrentThread();
        Cycle(400);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(budget.Granted > 0);
    }

    private sealed class Walker
    {
        public CitizenIdentity Identity = null!;
        public PersonDressing Dressing;
        public int State;
        public double Until;
        public Body? Body;
    }

    private sealed class Arm
    {
        public int Acquisitions, Builds, Hits, Grown, Trimmed, MaxWorkInFrame, PeakActive, PeakOwned;
        public double HitRate => Acquisitions == 0 ? 0 : (double)Hits / Acquisitions;
        public long Bytes => Builds * Rig14jBytesPerBuild + Grown * Rig14jBytesPerGrow;
    }

    private static Arm Street(bool hinted, bool keepOnRebuild, int seed, double seconds = 600, int persons = 3000, int maximum = 96, double warm = 300)
    {
        var dice = new Random(seed);
        var people = new Walker[persons];
        for (int i = 0; i < persons; i++)
        {
            var identity = Identity(i);
            people[i] = new Walker { Identity = identity, Dressing = new PersonDressing(identity, 60), Until = dice.NextDouble() * 6000 };
        }
        DressedBodyPool<Body> Fresh() => new DressedBodyPool<Body>(hinted ? DressedBodyPool<Body>.StreetSpare : DressedBodyPool<Body>.DefaultSpare);
        var pool = Fresh();
        var budget = new DressBuildBudget();
        var arm = new Arm();
        var counted = new Arm();
        int owned = 0, active = 0, frame = 0, ceiling = hinted ? PhysicalPedestriansCeiling(maximum) : maximum;
        const double step = 1.0 / 28, minutesPerSecond = 0.333;
        double selectAt = 0, nextTrim = 0;
        bool rebuilt = false;
        var waiting = new List<Walker>();
        for (double now = 0; now < warm + seconds; now += step, frame++)
        {
            int work = 0;
            pool.Minute = now * minutesPerSecond;
            if (!rebuilt && now >= warm)
            {
                rebuilt = true;
                foreach (var p in people) if (p.Body != null) { pool.Return(p.Body, p.Body.Dressing, now, double.PositiveInfinity); p.Body = null; active--; }
                if (!keepOnRebuild) { owned -= pool.Count; pool = Fresh(); }
                arm = counted;
            }
            foreach (var p in people)
            {
                if (now < p.Until) continue;
                if (p.State == 0) { p.State = 1; p.Until = now + 60 + dice.NextDouble() * 160; continue; }
                if (p.State == 1)
                {
                    double roll = dice.NextDouble();
                    double wanted;
                    if (roll < 0.35) { p.State = 2; p.Until = now + 30 + dice.NextDouble() * 300; wanted = p.Until * minutesPerSecond; }
                    else if (roll < 0.45) { p.State = 2; p.Until = now + 5 + dice.NextDouble() * 40; wanted = (now * minutesPerSecond) + PhysicalPopulation.ReturnTravelMinutes; }
                    else { p.State = 0; p.Until = now + 1800 + dice.NextDouble() * 6000; wanted = double.PositiveInfinity; }
                    if (p.Body != null) { pool.Return(p.Body, p.Body.Dressing, now, hinted ? wanted : double.NaN); p.Body = null; active--; }
                    continue;
                }
                p.State = 1; p.Until = now + 60 + dice.NextDouble() * 160;
            }
            if (now >= nextTrim && pool.TryTrim(now, DressedBodyPool<Body>.TrimIdleSeconds, out _)) { owned--; arm.Trimmed++; nextTrim = now + DressedBodyPool<Body>.TrimEverySeconds; }
            if (now < selectAt) continue;
            selectAt = now + 0.25;
            waiting.Clear();
            foreach (var p in people) if (p.State == 1 && p.Body == null) waiting.Add(p);
            int attempts = 0;
            foreach (var p in waiting)
            {
                if (active >= maximum || attempts >= 4) break;
                attempts++;
                var choice = pool.Choose(p.Dressing, owned, ceiling, budget, frame, now, out var body);
                if (choice == BodyChoice.Full || choice == BodyChoice.Deferred) continue;
                if (choice == BodyChoice.Grow) { owned++; arm.Grown++; work++; pool.Return(new Body(), default, now); continue; }
                if (choice == BodyChoice.Redress) { body.Dressing = p.Dressing; body.Look = PersonAppearance.For(p.Identity); arm.Builds++; work++; }
                else arm.Hits++;
                Assert.True(body.Dressing.Matches(p.Dressing));
                Assert.Equal(PersonAppearance.For(p.Identity).Seed, body.Look.Seed);
                p.Body = body; active++; arm.Acquisitions++;
                arm.PeakActive = Math.Max(arm.PeakActive, active); arm.PeakOwned = Math.Max(arm.PeakOwned, owned);
            }
            arm.MaxWorkInFrame = Math.Max(arm.MaxWorkInFrame, work);
        }
        return counted;
    }

    private static int PhysicalPedestriansCeiling(int maximum) => DressedBodyPool<Body>.Ceiling(maximum);

    [Theory]
    [InlineData(0x14A)]
    [InlineData(0x14B)]
    [InlineData(0x14C)]
    public void StreetShapedChurnReusesMoreBodiesBuildsFewerAndGrowsLessWithHintsAndTheReserve(int seed)
    {
        var before = Street(hinted: false, keepOnRebuild: false, seed);
        var after = Street(hinted: true, keepOnRebuild: true, seed);
        foreach (var (label, r) in new[] { ("before (#266: spare 16, oldest first, rebuild destroys)", before), ("after (hints, spare 32, reserve kept)", after) })
            output.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "seed={0:X} {1}: acquisitions={2} builds={3} hits={4} hitRate={5:P1} grown={6} trimmed={7} peakActive={8} peakOwned={9} maxWorkPerFrame={10} modelledSpawn={11:0.0} MB",
                seed, label, r.Acquisitions, r.Builds, r.Hits, r.HitRate, r.Grown, r.Trimmed, r.PeakActive, r.PeakOwned, r.MaxWorkInFrame, r.Bytes / 1e6));
        Assert.True(after.HitRate >= before.HitRate + 0.15, after.HitRate + " vs " + before.HitRate);
        Assert.True(after.Builds < before.Builds);
        Assert.True(after.Grown < before.Grown);
        Assert.True(after.Bytes * 3 <= before.Bytes * 2, after.Bytes + " vs " + before.Bytes);
        Assert.Equal(1, after.MaxWorkInFrame);
        Assert.InRange(after.Acquisitions, before.Acquisitions * 9 / 10, before.Acquisitions * 11 / 10);
    }

    [Fact]
    public void AGrowthBurstIsSpreadOneBodyStepPerFrameAndTheReserveRemovesTheGrowth()
    {
        double clock = 0;
        int Burst(DressedBodyPool<Body> pool, int owned, out int grown, out int frames)
        {
            var budget = new DressBuildBudget(gate: new DressFrameGate(() => clock));
            var wanted = Enumerable.Range(0, 40).Select(i => new PersonDressing(Identity(500 + i), 60)).ToList();
            grown = 0; frames = 0;
            int maxWork = 0;
            for (int frame = 0; wanted.Count > 0 && frame < 20000; frame++)
            {
                frames++;
                clock = 1;
                double seconds = frame / 30.0;
                int work = 0;
                for (int i = wanted.Count - 1; i >= 0; i--)
                {
                    var choice = pool.Choose(wanted[i], owned, 96, budget, frame, seconds, out var body);
                    if (choice == BodyChoice.Deferred || choice == BodyChoice.Full) continue;
                    work++;
                    if (choice == BodyChoice.Grow) { owned++; grown++; pool.Return(new Body(), default, seconds); continue; }
                    wanted.RemoveAt(i);
                }
                maxWork = Math.Max(maxWork, work);
            }
            return maxWork;
        }
        int maxCold = Burst(new DressedBodyPool<Body>(DressedBodyPool<Body>.StreetSpare), 0, out int coldGrown, out int coldFrames);
        var reserve = new DressedBodyPool<Body>(DressedBodyPool<Body>.StreetSpare);
        for (int i = 0; i < 40; i++) reserve.Return(new Body(), new PersonDressing(Identity(i), 60), 0, double.PositiveInfinity);
        int maxWarm = Burst(reserve, 40, out int warmGrown, out int warmFrames);
        output.WriteLine("cold burst: grown=" + coldGrown + " frames=" + coldFrames + " maxWorkPerFrame=" + maxCold
            + "; reserve burst: grown=" + warmGrown + " frames=" + warmFrames + " maxWorkPerFrame=" + maxWarm);
        Assert.Equal(1, maxCold); Assert.Equal(1, maxWarm);
        Assert.Equal(40, coldGrown); Assert.Equal(0, warmGrown);
        Assert.True(coldFrames >= 80 * DressFrameGate.DefaultQuietFrames);
        Assert.True(warmFrames * 3 <= coldFrames * 2);
    }

    private sealed class Walk : IRoutePlanner
    {
        public Route Plan(Place a, Place b, double at, TravelPreferences p) => Legs(a.Position, b.Position);
    }

    private static Route Legs(Position a, Position b)
    {
        var route = new Route { Legs = new() };
        for (int i = 0; i < 4; i++)
        {
            var from = Position.Lerp(a, b, i / 4.0); var to = Position.Lerp(a, b, (i + 1) / 4.0);
            route.Legs.Add(new RouteLeg { From = from, To = to, Mode = TravelMode.Walk, Minutes = Position.Distance(from, to) / 20 });
        }
        return route;
    }

    [Fact]
    public void AWalkerWhoArrivesNearTheFocusIsReleasedWithItsDepartureMinute()
    {
        StreetFoodHooks.Reset(); NavMesh.Reset(); AmbientPedestrians.Reserved.Clear(); PhysicalPedestrians.VisiblePoint = null; PhysicalPedestrians.Defer = null;
        const double now = 1440 + 600;
        var ids = Enumerable.Range(0, 6).Select(i => new CitizenIdentity { Id = "h" + i, Name = "H" + i, Age = 30, AppearanceSeed = i + 1 }).ToList();
        var places = new List<Place>
        {
            new Place { Id = "near", Name = "Near", Kind = PlaceKind.Work, Capacity = 6, Position = new Position(100, 0, 0) },
            new Place { Id = "far", Name = "Far", Kind = PlaceKind.Work, Capacity = 6, Position = new Position(100, 0, 600) },
        };
        for (int i = 0; i < 6; i++)
            places.Add(new Place { Id = "home" + i, Name = "Home", Kind = PlaceKind.Home, Capacity = 1, Position = new Position(100 + 30 * Math.Cos(i), 0, 30 * Math.Sin(i)) });
        var state = Population.Create(ids, places, "return-hint", "0.40.4", 3, now);
        for (int i = 0; i < 6; i++)
        {
            var c = state.Citizens[i];
            c.HomeId = c.PlaceId = "home" + i; c.DayPlaceId = "near"; c.MealOuting = null;
            c.Phase = CitizenPhase.Travelling; c.DestinationId = "near"; c.DestinationActivity = Activity.Work;
            c.Journey = Legs(places[i + 2].Position, places[0].Position);
            c.DepartureMinute = now; c.NextEventMinute = now + c.Journey.Minutes; c.ActivityUntilMinute = now + 240;
        }
        var city = new CitySimulator(state, new Walk());
        NativeTravelPace.Focus = new Position(100, 0, 0); Time.Next(3);
        using var bodies = new PhysicalPopulation(new Transform { Component = new PopulationRuntime() }, city, new MultimodalRoutes(),
            typeof(BodyReuseAttributionTests).Assembly, new VendorBodyTests.Log(), 96, 8, 120, new DwellSpots());
        var pool = PhysicalPedestrians.Last!;
        int preflight = pool.Released.Count;
        double minute = now;
        for (int i = 0; i < 80 && state.Citizens.Any(c => c.Phase == CitizenPhase.Travelling || pool.Active.ContainsKey(c.Identity.Id)); i++)
        {
            minute += 0.1; city.AdvanceTo(minute); Time.Next(0.3f);
            bodies.Update();
            Assert.Equal(minute, pool.Minute, 6);
        }
        var released = pool.Released.Skip(preflight).ToList();
        Assert.NotEmpty(released);
        foreach (var (id, wanted) in released)
        {
            var c = state.Citizens.First(x => x.Identity.Id == id);
            Assert.Equal(CitizenPhase.AtPlace, c.Phase);
            Assert.Equal(c.NextEventMinute, wanted, 6);
            Assert.True(wanted > now);
        }
    }
}

public sealed class BodyLeaseAndScopeTests
{
    private sealed class Character { }

    private sealed class Lease : IBodyLease
    {
        public readonly Character Body;
        public Lease(Character body) { Body = body; }
        public readonly Dictionary<object, int> Crossings = new();
        public readonly List<object> Departed = new(), Faded = new(), Probe = new();
        public readonly List<(int, float)> Originals = new();
        public string Id = "";
        public long Generation;
        public PersonDressing Dressing;
        public bool Valid;
        public bool? CanFade;
        public void Renew(string identityId, long generation, PersonDressing dressing, bool sameLook)
        {
            Id = identityId; Generation = generation; Dressing = dressing; Valid = true;
            Crossings.Clear(); Departed.Clear();
            if (!sameLook) CanFade = null;
        }
        public void Invalidate() { Valid = false; Crossings.Clear(); Departed.Clear(); }
    }

    private static CitizenIdentity Identity(int i) => new CitizenIdentity
    { Id = "q" + i.ToString(CultureInfo.InvariantCulture), Name = "Q" + i.ToString(CultureInfo.InvariantCulture), Age = 30, AppearanceSeed = 1 + i };

    [Fact]
    public void AWarmedPooledAcquisitionWithItsLeaseAndScratchAllocatesNothingAndNewLeasesInvalidateOldOnes()
    {
        var pool = new DressedBodyPool<Character>(DressedBodyPool<Character>.StreetSpare);
        var leases = new BodyLeases<Character, Lease>(body => new Lease(body));
        var dressings = Enumerable.Range(0, 64).Select(i => new PersonDressing(Identity(i), 60)).ToArray();
        var ids = dressings.Select(d => d.Id!).ToArray();
        var active = new Dictionary<string, Lease>(StringComparer.Ordinal);
        var held = new Lease?[dressings.Length];
        int owned = 0;
        for (int i = 0; i < 40; i++) { pool.Return(new Character(), dressings[i], i, double.PositiveInfinity); owned++; }
        long frame = 0;
        void Cycle(int rounds, bool check)
        {
            for (int round = 0; round < rounds; round++)
                for (int i = round % 3; i < dressings.Length; i += 3)
                {
                    frame++;
                    pool.Minute = frame / 30.0;
                    var lease = held[i];
                    if (lease != null)
                    {
                        active.Remove(lease.Id); lease.Crossings[lease] = 1; lease.Departed.Add(lease);
                        lease.Invalidate();
                        pool.Return(lease.Body, lease.Dressing, frame, pool.Minute + 5);
                        held[i] = null;
                        continue;
                    }
                    if (active.Count >= 32) continue;
                    var choice = pool.Choose(dressings[i], owned, DressedBodyPool<Character>.Ceiling(32), null, (int)frame, frame, out var body);
                    if (choice != BodyChoice.Hit && choice != BodyChoice.Redress) continue;
                    long before = leases.Generation;
                    var next = leases.Lease(body, ids[i], dressings[i], choice == BodyChoice.Hit);
                    if (check) { Assert.True(next.Valid); Assert.Equal(before + 1, next.Generation); Assert.Equal(ids[i], next.Id); Assert.Empty(next.Crossings); }
                    if (choice == BodyChoice.Hit) next.CanFade ??= true;
                    active.Add(ids[i], next);
                    held[i] = next;
                }
        }
        Cycle(30, true);
        long allocated = long.MaxValue;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            Cycle(150, false);
            allocated = Math.Min(allocated, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        Assert.Equal(0, allocated);
        Assert.Equal(40, leases.Count);

        var a = leases.Lease(new Character(), "x", dressings[0], false);
        long first = a.Generation;
        a.Invalidate();
        var again = leases.Lease(a.Body, "y", dressings[1], false);
        Assert.Same(a, again);
        Assert.NotEqual(first, again.Generation);
        Assert.Null(again.CanFade);
        leases.Forget(a.Body);
        Assert.False(a.Valid);
    }

    [Fact]
    public void SetupAndUnpreparedPhysicalScopesReconcileWithTheModParent()
    {
        GcAllocationCounter.Probe();
        var sampler = new GcPauseSampler(64);
        sampler.Start(1, GcAllocationCounter.Probe());
        sampler.FrameRecorder.Arm(System.Diagnostics.Stopwatch.Frequency);
        object? keep = null;
        for (int frame = 0; frame < 8; frame++)
        {
            long modStart = GcAllocationCounter.Read();
            ModFrames.Begin(sampler, frame);
            keep = new byte[1000]; ModFrames.Charge(GcAllocationSegment.ModRoutes);
            if (frame % 2 == 0)
            {
                PhysicalAllocation.Begin(sampler);
                keep = new byte[50_000]; PhysicalAllocation.Charge(GcAllocationSegment.PhysicalAmbient);
                PhysicalAllocation.End();
                ModFrames.End();
                sampler.Allocated(GcAllocationSegment.Mod, modStart, GcAllocationCounter.Read(), false);
                continue;
            }
            keep = new byte[2000]; ModFrames.Charge(GcAllocationSegment.ModHoldReplay);
            PhysicalAllocation.Begin(sampler);
            keep = new byte[400_000]; PhysicalAllocation.Charge(GcAllocationSegment.PhysicalSpawnAppearance);
            keep = new byte[3000];
            PhysicalAllocation.End();
            ModFrames.Charge(GcAllocationSegment.ModPhysicalSetup);
            PhysicalAllocation.Begin(sampler);
            keep = new byte[7000]; PhysicalAllocation.Charge(GcAllocationSegment.PhysicalBindings);
            PhysicalAllocation.End();
            ModFrames.End();
            sampler.Allocated(GcAllocationSegment.Mod, modStart, GcAllocationCounter.Read(), false);
        }
        GC.KeepAlive(keep);
        long outer = 0;
        for (int i = (int)GcAllocationSegment.ModPrelude; i < GcPauseSampler.Segments; i++) if (!GcPauseSampler.IsSub((GcAllocationSegment)i)) outer += sampler.SegmentBytes((GcAllocationSegment)i);
        long mod = sampler.SegmentBytes(GcAllocationSegment.Mod), physical = sampler.SegmentBytes(GcAllocationSegment.Physical);
        Assert.True(sampler.SegmentBytes(GcAllocationSegment.PhysicalSpawnAppearance) >= 4 * 400_000);
        Assert.Equal(mod, outer + physical + sampler.SegmentBytes(GcAllocationSegment.Advance));
        Assert.True(physical >= 4 * 460_000);
    }

    [Fact]
    public void AHeapCounterSampleAcrossACollectionIsSkippedEvenWhenTheHeapEndsHigher()
    {
        var sampler = new GcPauseSampler(64);
        sampler.Start(1000, "heap_used");
        sampler.FrameRecorder.Arm(1000);
        sampler.FrameRecorder.Begin(1, 0, 1000, 0, 0);
        sampler.FrameRecorder.Charge(GcAllocationSegment.ModPrelude, 1, 1500, 1, 1);
        sampler.FrameRecorder.End(2, 1600, 1, 1, sampler);
        sampler.Allocated(GcAllocationSegment.Mod, 1000, 1600, true);
        Assert.Equal(0, sampler.SegmentBytes(GcAllocationSegment.Mod));
        Assert.Equal(1, sampler.SegmentSkipped(GcAllocationSegment.Mod));
        Assert.Equal(0, sampler.SegmentBytes(GcAllocationSegment.ModPrelude));
        Assert.Equal(1, sampler.SegmentSkipped(GcAllocationSegment.ModPrelude));

        sampler.FrameRecorder.Begin(2, 10, 5000, 3, 0);
        sampler.FrameRecorder.Charge(GcAllocationSegment.ModPrelude, 11, 5500, 4, 0);
        sampler.FrameRecorder.End(12, 5600, 4, 0, sampler);
        Assert.Equal(500, sampler.SegmentBytes(GcAllocationSegment.ModPrelude));
        Assert.Equal(1, sampler.FrameRecorder.Top(0).Collections);
    }
}
