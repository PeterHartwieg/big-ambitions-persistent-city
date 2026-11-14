using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PersistentCity.Core;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

public sealed class DressedBodyChurnTests
{
    private const long RigBytesPerBuild = 745426168L / 427;
    private static readonly long ModelledBakedBytesPerBuild = BakedBytes(new BodyShape());
    private readonly ITestOutputHelper output;
    public DressedBodyChurnTests(ITestOutputHelper output) { this.output = output; }

    private sealed class Body
    {
        public PersonDressing Dressing;
        public PersonLook Look;
    }

    private sealed class Person
    {
        public CitizenIdentity Identity = null!;
        public PersonDressing Dressing;
        public int State;
        public double Until;
        public Body? Body;
    }

    private sealed class ChurnResult
    {
        public int Acquisitions, Builds, Hits, Deferred, Failed, PeakActive, MaxBuildsInFrame, Owned, PeakOwned, Grown, Trimmed, NightOwned, NightActive;
        public double HitRate => Acquisitions == 0 ? 0 : (double)Hits / Acquisitions;
    }

    private sealed class BodyShape
    {
        public int Sources = 9, BonesPerSource = 65, SubMeshesPerSource = 1, ElementTypes = 11, VariantsPerType = 24, NameLength = 24;
        public int Bones => Sources * BonesPerSource;
    }

    private const long Header = 32, Reference = 8, Bindpose = 64, Combine = 88, ListObject = 40, SmallObject = 48;

    private static long ArrayBytes(long count, long element) => Header + count * element;

    private static long ListOf(long count, long element)
    {
        long capacity = 4, bytes = ListObject;
        while (capacity < count) capacity *= 2;
        for (long c = 4; c <= capacity; c *= 2) bytes += ArrayBytes(c, element);
        return bytes;
    }

    private static long EnabledMergeBytes(BodyShape b)
    {
        long materials = ArrayBytes(b.SubMeshesPerSource, Reference);
        long validate = b.Sources * 5 * materials + ListOf(b.Sources, Reference) + ArrayBytes(b.Sources, Reference);
        long gather = ArrayBytes(b.Sources, Reference) + 2 * ListOf(b.Sources, Reference) + ArrayBytes(b.Sources, Reference);
        long sort = b.Sources * b.SubMeshesPerSource * (3 * materials + SmallObject) + b.Sources * (ListOf(1, Reference) + 2 * SmallObject);
        long perSource = 2 * ArrayBytes(b.BonesPerSource, Reference) + ArrayBytes(b.BonesPerSource, Bindpose);
        long perMaterial = ArrayBytes(1, Combine) + ArrayBytes(b.BonesPerSource, Bindpose) + ArrayBytes(b.BonesPerSource, Reference)
            + ArrayBytes(b.BonesPerSource, Reference) + ArrayBytes(b.BonesPerSource, Bindpose) + 3 * SmallObject;
        long final = ArrayBytes(b.Sources, Combine) + ArrayBytes(b.Bones, Reference) + ArrayBytes(b.Bones, Bindpose) + ArrayBytes(b.Sources, Reference);
        long objects = 8 * SmallObject + 2 * (Header + 2 * (160 + b.NameLength)) + 4 * SmallObject;
        return validate + gather + sort + b.Sources * perSource + b.Sources * perMaterial + final + objects;
    }

    private static long DressScriptBytes(BodyShape b)
    {
        long variants = ListOf(b.VariantsPerType, Reference) + 3 * SmallObject;
        long randomize = 280 + b.ElementTypes * (variants + 4 * SmallObject) + 2 * variants;
        long visuals = b.ElementTypes * (variants + 3 * SmallObject) + 2 * SmallObject;
        return randomize + visuals;
    }

    private static long BlendshapeBytes(long mergedVertices, long names, long sourceVertices, long sources) =>
        names * (6 * ArrayBytes(mergedVertices, 12) + SmallObject) + sources * 3 * ArrayBytes(sourceVertices, 12);

    private static long FullSupportBytes(BodyShape b, long mergedVertices, long names) =>
        EnabledMergeBytes(b) + DressScriptBytes(b) + BlendshapeBytes(mergedVertices, names, mergedVertices / b.Sources, b.Sources);

    private static long BakedBytes(BodyShape b) => EnabledMergeBytes(b) + DressScriptBytes(b);

    private static CitizenIdentity Identity(int i) => new CitizenIdentity
    { Id = "p" + i.ToString(CultureInfo.InvariantCulture), Name = "N" + i.ToString(CultureInfo.InvariantCulture), Age = 20 + i % 60, Female = i % 2 == 0, AppearanceSeed = 1 + i * 7 };

    private static ChurnResult Churn(bool lru, int maximum, double flap, int persons = 3000, double seconds = 600, int seed = 0x6E5,
        int spare = DressedBodyPool<Body>.DefaultSpare, bool trim = false, double night = double.PositiveInfinity)
    {
        var dice = new Random(seed);
        var people = new Person[persons];
        for (int i = 0; i < persons; i++)
        {
            var identity = Identity(i);
            people[i] = new Person { Identity = identity, Dressing = new PersonDressing(identity, 60), Until = dice.NextDouble() * 4000 };
        }
        var pool = new DressedBodyPool<Body>(spare);
        var budget = lru ? new DressBuildBudget() : null;
        var result = new ChurnResult();
        int owned = 0, active = 0, frame = 0;
        const double step = 1.0 / 30;
        var selectAt = 0.0;
        double nextTrim = 0;
        var waiting = new List<Person>();
        for (double now = 0; now < seconds; now += step, frame++)
        {
            int builtThisFrame = 0;
            bool dark = now >= night;
            foreach (var p in people)
            {
                if (now < p.Until) continue;
                switch (p.State)
                {
                    case 0:
                        if (dark && dice.NextDouble() < 0.95) { p.Until = double.PositiveInfinity; break; }
                        p.State = 1; p.Until = now + 20 + dice.NextDouble() * 70; break;
                    case 1:
                        if (p.Body != null) { pool.Return(p.Body, p.Body.Dressing, now); p.Body = null; active--; }
                        if (dark && dice.NextDouble() < 0.95) { p.State = 0; p.Until = double.PositiveInfinity; break; }
                        double roll = dice.NextDouble();
                        if (roll < flap) { p.State = 2; p.Until = now + 2 + dice.NextDouble() * 8; }
                        else if (roll < 0.6) { p.State = 2; p.Until = now + 60 + dice.NextDouble() * 540; }
                        else { p.State = 0; p.Until = now + 600 + dice.NextDouble() * 3400; }
                        break;
                    case 2:
                        if (dark && dice.NextDouble() < 0.95) { p.State = 0; p.Until = double.PositiveInfinity; break; }
                        p.State = 1; p.Until = now + 20 + dice.NextDouble() * 70; break;
                }
            }
            if (trim && builtThisFrame == 0 && now >= nextTrim && pool.TryTrim(now, 120, out _)) { owned--; result.Trimmed++; nextTrim = now + 5; }
            result.PeakOwned = Math.Max(result.PeakOwned, owned);
            if (dark) { result.NightOwned = owned; result.NightActive = active; }
            if (now < selectAt) continue;
            selectAt = now + 0.25;
            waiting.Clear();
            foreach (var p in people) if (p.State == 1 && p.Body == null) waiting.Add(p);
            int attempts = 0;
            foreach (var p in waiting)
            {
                if (active >= maximum || attempts >= 4) break;
                attempts++;
                Body? body;
                bool built;
                if (lru)
                {
                    var choice = pool.Choose(p.Dressing, owned, maximum, budget, frame, now, out body);
                    if (choice == BodyChoice.Full) { result.Failed++; continue; }
                    if (choice == BodyChoice.Deferred) { result.Deferred++; attempts--; continue; }
                    if (choice == BodyChoice.Grow) { body = new Body(); owned++; result.Grown++; }
                    built = choice != BodyChoice.Hit;
                }
                else
                {
                    built = !pool.TryTakeDressed(p.Dressing, out body);
                    if (built && !pool.TryTakeOldest(out body))
                    {
                        if (owned >= maximum) { result.Failed++; continue; }
                        body = new Body(); owned++; result.Grown++;
                    }
                }
                if (built)
                {
                    body!.Dressing = p.Dressing; body.Look = PersonAppearance.For(p.Identity);
                    result.Builds++; builtThisFrame++;
                }
                else result.Hits++;
                Assert.True(body!.Dressing.Matches(p.Dressing));
                Assert.Equal(PersonAppearance.For(p.Identity).Seed, body.Look.Seed);
                Assert.Equal(PersonAppearance.For(p.Identity).Wardrobe, body.Look.Wardrobe);
                p.Body = body; active++; result.Acquisitions++;
                result.PeakActive = Math.Max(result.PeakActive, active);
            }
            result.MaxBuildsInFrame = Math.Max(result.MaxBuildsInFrame, builtThisFrame);
        }
        result.Owned = owned;
        return result;
    }

    [Theory]
    [InlineData(48, 0.0)]
    [InlineData(96, 0.0)]
    [InlineData(48, 0.2)]
    [InlineData(96, 0.2)]
    public void RigShapedChurnNeverBuildsMoreLooksAndNeverMoreThanOnePerFrame(int maximum, double flap)
    {
        var before = Churn(lru: false, maximum, flap);
        var after = Churn(lru: true, maximum, flap);
        output.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "max={0} flap={1:P0}: before acquisitions={2} builds={3} hits={4} hitRate={5:P1} maxBuildsPerFrame={6} owned={7} modelledSpawnAppearance={8:0.0} MB",
            maximum, flap, before.Acquisitions, before.Builds, before.Hits, before.HitRate, before.MaxBuildsInFrame, before.Owned, before.Builds * RigBytesPerBuild / 1e6));
        output.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "max={0} flap={1:P0}: after  acquisitions={2} builds={3} hits={4} hitRate={5:P1} maxBuildsPerFrame={6} owned={7} deferred={8} modelledSpawnAppearance={9:0.0} MB",
            maximum, flap, after.Acquisitions, after.Builds, after.Hits, after.HitRate, after.MaxBuildsInFrame, after.Owned, after.Deferred, after.Builds * ModelledBakedBytesPerBuild / 1e6));
        Assert.InRange(before.Acquisitions, 300, 1500);
        Assert.True(after.Builds <= before.Builds);
        Assert.True(after.HitRate >= before.HitRate);
        if (flap > 0 && before.PeakActive < maximum) Assert.True(after.HitRate > before.HitRate + 0.05);
        Assert.True(after.Owned <= maximum && before.Owned <= maximum);
        Assert.Equal(1, after.MaxBuildsInFrame);
        Assert.True(before.MaxBuildsInFrame > 1);
        Assert.InRange(after.Acquisitions, before.Acquisitions * 9 / 10, before.Acquisitions * 11 / 10);
        Assert.True(after.Builds * ModelledBakedBytesPerBuild * 6 <= before.Builds * RigBytesPerBuild);
    }

    [Theory]
    [InlineData(96, 0.0)]
    [InlineData(96, 0.2)]
    public void TheIdleSpareBoundsRetainedBodiesAndTrimsThemAtNight(int maximum, double flap)
    {
        const double day = 900, seconds = 1800;
        var grown = Churn(lru: true, maximum, flap, seconds: seconds, spare: int.MaxValue, night: day);
        var bounded = Churn(lru: true, maximum, flap, seconds: seconds, trim: true, night: day);
        foreach (var (label, r) in new[] { ("grow-to-maximum (#262)", grown), ("spare 16 + trim", bounded) })
            output.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "max={0} flap={1:P0} {2}: acquisitions={3} builds={4} hits={5} hitRate={6:P1} peakActive={7} grown={8} peakOwned={9} trimmed={10} night active/owned={11}/{12}",
                maximum, flap, label, r.Acquisitions, r.Builds, r.Hits, r.HitRate, r.PeakActive, r.Grown, r.PeakOwned, r.Trimmed, r.NightActive, r.NightOwned));
        Assert.Equal(maximum, grown.PeakOwned);
        Assert.Equal(maximum, grown.NightOwned);
        Assert.True(bounded.PeakOwned <= bounded.PeakActive + DressedBodyPool<Body>.DefaultSpare + 1);
        Assert.True(bounded.NightOwned <= bounded.NightActive + DressedBodyPool<Body>.DefaultSpare);
        Assert.True(bounded.Trimmed > 0 && bounded.NightOwned < grown.NightOwned);
        Assert.True(bounded.Grown <= grown.Grown + bounded.Trimmed);
        Assert.InRange(bounded.Acquisitions, grown.Acquisitions * 9 / 10, grown.Acquisitions * 11 / 10);
        Assert.True(bounded.Builds <= grown.Builds * 11 / 10);
        Assert.Equal(1, bounded.MaxBuildsInFrame);
    }

    [Fact]
    public void AnUndressedBodyIsDressedBeforeTheBodyCountGrows()
    {
        var pool = new DressedBodyPool<Body>(spare: 4);
        var a = new PersonDressing(Identity(1), 60);
        var fresh = new Body();
        pool.Return(new Body { Dressing = a }, a, 0);
        pool.Return(fresh, default, 1);
        Assert.Equal(BodyChoice.Redress, pool.Choose(new PersonDressing(Identity(2), 60), 2, 8, null, 0, 0, out var taken));
        Assert.Same(fresh, taken);
        Assert.Equal(BodyChoice.Hit, pool.Choose(a, 2, 8, null, 0, 0, out _));
        Assert.Equal(BodyChoice.Grow, pool.Choose(new PersonDressing(Identity(3), 60), 2, 8, null, 0, 0, out _));
        for (int i = 0; i < 4; i++) pool.Return(new Body(), new PersonDressing(Identity(10 + i), 60), 10 + i);
        Assert.Equal(BodyChoice.Redress, pool.Choose(new PersonDressing(Identity(4), 60), 6, 8, null, 0, 0, out _));
        pool.Return(new Body(), new PersonDressing(Identity(20), 60), 20);
        pool.Return(new Body(), new PersonDressing(Identity(21), 60), 21);
        Assert.Equal(5, pool.Count);
        Assert.False(pool.TryTrim(100, 120, out _));
        Assert.True(pool.TryTrim(131, 120, out _));
        Assert.Equal(4, pool.Count);
        Assert.False(pool.TryTrim(1000, 120, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DressedBodyPool<Body>(-1));
    }

    [Fact]
    public void TheBakeRemovesTheCombinersBlendshapeArraysFromEveryBuild()
    {
        var shape = new BodyShape();
        long merged = 1, names = 3;
        while (FullSupportBytes(shape, merged + 1, names) <= RigBytesPerBuild) merged++;
        long before = FullSupportBytes(shape, merged, names), after = BakedBytes(shape);
        output.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "rig 14b measured {0:N0} B/build; FullSupport model {1:N0} B at {2:N0} merged vertices and {3} shape names; baked model {4:N0} B/build ({5:0.0}x less): merge {6:N0} B, dress scripts {7:N0} B",
            RigBytesPerBuild, before, merged, names, after, (double)before / after, EnabledMergeBytes(shape), DressScriptBytes(shape)));
        foreach (int bones in new[] { 40, 65, 90 })
        {
            var s = new BodyShape { BonesPerSource = bones };
            output.WriteLine(string.Format(CultureInfo.InvariantCulture, "bones per source {0}: baked {1:N0} B/build, {2:0.0}x less than measured",
                bones, BakedBytes(s), (double)RigBytesPerBuild / BakedBytes(s)));
        }
        Assert.True(after * 6 <= before);
        Assert.True(BlendshapeBytes(merged, names, merged / shape.Sources, shape.Sources) > 4 * after);
    }

    [Fact]
    public void ThePoolHitAcquireAllocatesNothingAfterWarmUp()
    {
        var pool = new DressedBodyPool<Body>();
        var budget = new DressBuildBudget();
        var dressings = Enumerable.Range(0, 96).Select(i => new PersonDressing(Identity(i), 60)).ToArray();
        var bodies = dressings.Select(d => new Body { Dressing = d }).ToArray();
        for (int i = 0; i < bodies.Length; i++) pool.Return(bodies[i], dressings[i]);
        void Cycle(int rounds)
        {
            for (int round = 0; round < rounds; round++)
                for (int i = 0; i < dressings.Length; i += 7)
                {
                    if (pool.Choose(dressings[i], 96, 96, budget, round, round, out var body) != BodyChoice.Hit) throw new InvalidOperationException("miss");
                    pool.Return(body, dressings[i]);
                }
        }
        Cycle(4);
        long before = GC.GetAllocatedBytesForCurrentThread();
        Cycle(200);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(96, pool.Count);
        Assert.Equal(0, budget.Granted);
    }

    [Fact]
    public void TheSameIdentityAlwaysGetsTheSameLookThroughReleaseEvictionAndRebuild()
    {
        var pool = new DressedBodyPool<Body>();
        var looks = new Dictionary<string, (PersonDressing Dressing, PersonLook Look)>(StringComparer.Ordinal);
        var dice = new Random(11);
        var held = new List<(CitizenIdentity Identity, Body Body)>();
        int owned = 0, rebuilds = 0, hits = 0;
        for (int step = 0; step < 20000; step++)
        {
            if (held.Count > 0 && (held.Count >= 8 || dice.Next(2) == 0))
            {
                int at = dice.Next(held.Count);
                pool.Return(held[at].Body, held[at].Body.Dressing);
                held.RemoveAt(at);
                continue;
            }
            var identity = Identity(dice.Next(40));
            if (held.Any(h => h.Identity.Id == identity.Id)) continue;
            var dressing = new PersonDressing(identity, 60);
            var choice = pool.Choose(dressing, owned, 12, null, step, step, out var body);
            if (choice == BodyChoice.Full) continue;
            if (choice == BodyChoice.Grow) { body = new Body(); owned++; }
            if (choice == BodyChoice.Hit) hits++;
            else { body.Dressing = dressing; body.Look = PersonAppearance.For(identity); rebuilds++; }
            if (!looks.TryGetValue(identity.Id, out var first)) looks[identity.Id] = first = (dressing, body.Look);
            Assert.True(first.Dressing.Matches(body.Dressing));
            Assert.Equal(first.Look.Seed, body.Look.Seed);
            Assert.Equal(first.Look.Wardrobe, body.Look.Wardrobe);
            held.Add((identity, body));
        }
        Assert.True(hits > 0 && rebuilds > looks.Count);
        Assert.True(owned <= 12);
        var older = new CitizenIdentity { Id = "p1", Name = "N1", Age = 22, Female = false, AppearanceSeed = 8 };
        Assert.False(new PersonDressing(older, 60).Matches(looks["p1"].Dressing));
    }

    private static bool Granted(DressBuildBudget budget, int frame, double seconds)
    {
        long granted = budget.Granted, deferred = budget.Deferred;
        bool can = budget.CanTake(frame, seconds);
        Assert.Equal(can, budget.CanTake(frame, seconds));
        Assert.Equal(granted, budget.Granted); Assert.Equal(deferred, budget.Deferred);
        bool took = budget.TryTake(frame, seconds);
        Assert.Equal(can, took);
        return took;
    }

    [Fact]
    public void WouldDeferPredictsChooseWithoutTakingABodyOrABuild()
    {
        var pool = new DressedBodyPool<Body>();
        var budget = new DressBuildBudget(1, 1, 1);
        var a = new PersonDressing(Identity(1), 60);
        var b = new PersonDressing(Identity(2), 60);
        pool.Return(new Body { Dressing = a }, a);
        Assert.False(pool.WouldDefer(b, 1, 2, budget, 1, 0));
        Assert.Equal(BodyChoice.Grow, pool.Choose(b, 1, 2, budget, 1, 0, out _));
        Assert.True(pool.WouldDefer(b, 2, 2, budget, 1, 0));
        Assert.False(pool.WouldDefer(a, 2, 2, budget, 1, 0));
        Assert.False(pool.WouldDefer(b, 2, 2, null, 1, 0));
        Assert.Equal(1, pool.Count);
        Assert.Equal(BodyChoice.Deferred, pool.Choose(b, 2, 2, budget, 1, 0, out _));
        Assert.Equal(BodyChoice.Hit, pool.Choose(a, 2, 2, budget, 1, 0, out _));
        Assert.False(pool.WouldDefer(b, 2, 2, budget, 1, 0));
        Assert.Equal(BodyChoice.Full, pool.Choose(b, 2, 2, budget, 1, 0, out _));
    }

    [Fact]
    public void TheDressBudgetGrantsAtMostOneBuildPerFrameAndTheRateOverTime()
    {
        var budget = new DressBuildBudget();
        int granted = 0;
        var perFrame = new Dictionary<int, int>();
        for (int frame = 0; frame < 6000; frame++)
        {
            double seconds = frame / 60.0;
            for (int call = 0; call < 5; call++)
                if (Granted(budget, frame, seconds))
                {
                    granted++;
                    perFrame[frame] = perFrame.TryGetValue(frame, out int n) ? n + 1 : 1;
                }
        }
        Assert.All(perFrame.Values, n => Assert.Equal(1, n));
        double elapsed = 5999 / 60.0;
        Assert.InRange(granted, (int)(elapsed * DressBuildBudget.DefaultPerSecond) - 1, (int)(DressBuildBudget.DefaultPerSecond + elapsed * DressBuildBudget.DefaultPerSecond) + 1);
        Assert.Equal(granted, budget.Granted);
        Assert.Equal(6000L * 5 - granted, budget.Deferred);
        Assert.False(budget.TryTake(9000, double.NaN));
        var one = new DressBuildBudget(1, 1, 1);
        Assert.True(one.TryTake(1, 0));
        Assert.False(one.TryTake(1, 0));
        Assert.False(one.TryTake(2, 0.5));
        Assert.True(one.TryTake(3, 1.0));
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int frame = 7000; frame < 9000; frame++) budget.TryTake(frame, frame / 60.0);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private sealed class SourceMesh
    {
        public float[] Base = Array.Empty<float>();
        public List<(string Name, float Weight, float[] Delta)> Shapes = new();
    }

    private static float[] PluginRendered(List<SourceMesh> sources, int mergedVertices, float multiplier)
    {
        var merged = new float[mergedVertices];
        int at = 0;
        foreach (var s in sources) { Array.Copy(s.Base, 0, merged, at, s.Base.Length); at += s.Base.Length; }
        var byName = new Dictionary<string, (float Weight, float[] Final)>(StringComparer.OrdinalIgnoreCase);
        int num = 0;
        foreach (var s in sources)
        {
            for (int j = 0; j < s.Shapes.Count; j++)
            {
                string name = s.Shapes[j].Name;
                int index = s.Shapes.FindIndex(x => x.Name == name);
                if (!byName.TryGetValue(name, out var value)) byName[name] = value = (s.Shapes[index].Weight, new float[mergedVertices]);
                var delta = s.Shapes[index].Delta;
                for (int k = 0; k < delta.Length; k++) value.Final[k + num] = delta[k] * multiplier;
            }
            num += s.Base.Length;
        }
        foreach (var (weight, final) in byName.Values)
            if (weight > 0) for (int k = 0; k < mergedVertices; k++) merged[k] += final[k] * weight / 100f;
        return merged;
    }

    private static float[] Baked(List<SourceMesh> sources, int mergedVertices, float multiplier, BlendshapeBakePlan plan)
    {
        var merged = new float[mergedVertices];
        int at = 0;
        foreach (var s in sources) { Array.Copy(s.Base, 0, merged, at, s.Base.Length); at += s.Base.Length; }
        plan.Clear();
        foreach (var s in sources)
        {
            plan.AddSource(s.Base.Length);
            foreach (var shape in s.Shapes) plan.AddShape(shape.Name, shape.Weight);
        }
        Assert.True(plan.Build(mergedVertices, multiplier));
        foreach (var step in plan.Steps)
        {
            var delta = sources[step.Source].Shapes[step.Shape].Delta;
            for (int k = 0; k < step.VertexCount; k++) merged[step.Offset + k] += delta[k] * step.Scale;
        }
        return merged;
    }

    [Fact]
    public void TheBakeRendersWhatTheCombinerBlendshapesRender()
    {
        var dice = new Random(5);
        string[] names = { "Fat", "Slim", "Muscular", "fat", "Brow_Up", "Jaw", "Nose" };
        var plan = new BlendshapeBakePlan();
        for (int round = 0; round < 300; round++)
        {
            var sources = new List<SourceMesh>();
            int total = 0;
            for (int s = 0, count = 1 + dice.Next(8); s < count; s++)
            {
                int vertices = 1 + dice.Next(40);
                var mesh = new SourceMesh { Base = Enumerable.Range(0, vertices).Select(_ => (float)dice.NextDouble()).ToArray() };
                for (int j = 0, shapes = dice.Next(6); j < shapes; j++)
                {
                    float weight = dice.Next(4) switch { 0 => 0, 1 => -20, _ => (float)(dice.NextDouble() * 140) };
                    mesh.Shapes.Add((names[dice.Next(names.Length)], weight, Enumerable.Range(0, vertices).Select(_ => (float)(dice.NextDouble() - .5)).ToArray()));
                }
                sources.Add(mesh); total += vertices;
            }
            int merged = total + dice.Next(3);
            float multiplier = dice.Next(3) == 0 ? 0.5f : 1f;
            var expected = PluginRendered(sources, merged, multiplier);
            var actual = Baked(sources, merged, multiplier, plan);
            for (int k = 0; k < merged; k++) Assert.True(Math.Abs(expected[k] - actual[k]) < 1e-4f, "round " + round + " vertex " + k);
        }
        plan.Clear(); plan.AddSource(4); plan.AddShape("Fat", 50);
        Assert.False(plan.Build(3, 1));
        Assert.Empty(plan.Steps);
    }

    [Fact]
    public void ThePlanAllocatesNothingAfterWarmUp()
    {
        var plan = new BlendshapeBakePlan();
        string[] names = { "Fat", "Slim", "Muscular", "Brow_Up", "Jaw" };
        void Fill()
        {
            plan.Clear();
            for (int s = 0; s < 10; s++)
            {
                plan.AddSource(500 + s);
                for (int j = 0; j < names.Length; j++) plan.AddShape(names[j], j * 20);
            }
            plan.Build(10000, 1);
        }
        Fill(); Fill();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) Fill();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(40, plan.Steps.Count);
    }
}
