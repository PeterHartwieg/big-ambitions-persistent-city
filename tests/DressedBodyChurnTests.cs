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
        public int Acquisitions, Builds, Hits, Deferred, Failed, PeakActive, MaxBuildsInFrame, Owned;
        public double HitRate => Acquisitions == 0 ? 0 : (double)Hits / Acquisitions;
        public long Bytes => Builds * RigBytesPerBuild;
    }

    private static CitizenIdentity Identity(int i) => new CitizenIdentity
    { Id = "p" + i.ToString(CultureInfo.InvariantCulture), Name = "N" + i.ToString(CultureInfo.InvariantCulture), Age = 20 + i % 60, Female = i % 2 == 0, AppearanceSeed = 1 + i * 7 };

    private static ChurnResult Churn(bool lru, int maximum, double flap, int persons = 3000, double seconds = 600, int seed = 0x6E5)
    {
        var dice = new Random(seed);
        var people = new Person[persons];
        for (int i = 0; i < persons; i++)
        {
            var identity = Identity(i);
            people[i] = new Person { Identity = identity, Dressing = new PersonDressing(identity, 60), Until = dice.NextDouble() * 4000 };
        }
        var pool = new DressedBodyPool<Body>();
        var budget = lru ? new DressBuildBudget() : null;
        var result = new ChurnResult();
        int owned = 0, active = 0, frame = 0;
        const double step = 1.0 / 30;
        var selectAt = 0.0;
        var waiting = new List<Person>();
        for (double now = 0; now < seconds; now += step, frame++)
        {
            int builtThisFrame = 0;
            foreach (var p in people)
            {
                if (now < p.Until) continue;
                switch (p.State)
                {
                    case 0: p.State = 1; p.Until = now + 20 + dice.NextDouble() * 70; break;
                    case 1:
                        if (p.Body != null) { pool.Return(p.Body, p.Body.Dressing); p.Body = null; active--; }
                        double roll = dice.NextDouble();
                        if (roll < flap) { p.State = 2; p.Until = now + 2 + dice.NextDouble() * 8; }
                        else if (roll < 0.6) { p.State = 2; p.Until = now + 60 + dice.NextDouble() * 540; }
                        else { p.State = 0; p.Until = now + 600 + dice.NextDouble() * 3400; }
                        break;
                    case 2: p.State = 1; p.Until = now + 20 + dice.NextDouble() * 70; break;
                }
            }
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
                    if (choice == BodyChoice.Grow) { body = new Body(); owned++; }
                    built = choice != BodyChoice.Hit;
                }
                else
                {
                    built = !pool.TryTakeDressed(p.Dressing, out body);
                    if (built && !pool.TryTakeOldest(out body))
                    {
                        if (owned >= maximum) { result.Failed++; continue; }
                        body = new Body(); owned++;
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
            "max={0} flap={1:P0}: before acquisitions={2} builds={3} hits={4} hitRate={5:P1} maxBuildsPerFrame={6} owned={7} modelledBytes={8:0.0} MB",
            maximum, flap, before.Acquisitions, before.Builds, before.Hits, before.HitRate, before.MaxBuildsInFrame, before.Owned, before.Bytes / 1e6));
        output.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "max={0} flap={1:P0}: after  acquisitions={2} builds={3} hits={4} hitRate={5:P1} maxBuildsPerFrame={6} owned={7} deferred={8} modelledBytes={9:0.0} MB",
            maximum, flap, after.Acquisitions, after.Builds, after.Hits, after.HitRate, after.MaxBuildsInFrame, after.Owned, after.Deferred, after.Bytes / 1e6));
        Assert.InRange(before.Acquisitions, 300, 1500);
        Assert.True(after.Builds <= before.Builds);
        Assert.True(after.HitRate >= before.HitRate);
        if (flap > 0 && before.PeakActive < maximum) Assert.True(after.HitRate > before.HitRate + 0.05);
        Assert.True(after.Owned <= maximum && before.Owned <= maximum);
        Assert.Equal(1, after.MaxBuildsInFrame);
        Assert.True(before.MaxBuildsInFrame > 1);
        Assert.InRange(after.Acquisitions, before.Acquisitions * 9 / 10, before.Acquisitions * 11 / 10);
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
                if (budget.TryTake(frame, seconds))
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
