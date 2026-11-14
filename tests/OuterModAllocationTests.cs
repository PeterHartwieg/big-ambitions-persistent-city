using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using PersistentCity.Compatibility;
using PersistentCity.Core;
using PersistentCity.Runtime;
using Xunit;
using Xunit.Abstractions;
using Fake = RealLifeOverhaul.Flare;

namespace PersistentCity.Tests
{
    [Collection(RloCompatibilityTests.SharedFlareState)]
    public sealed class OuterModAllocationTests
    {
        private readonly ITestOutputHelper output;
        public OuterModAllocationTests(ITestOutputHelper output) { this.output = output; CityApiHooks.Reset(); }

        private static void Serve(int seed)
        {
            RealLifeOverhaul.GoingPublic.InSession = false;
            CityApiHooks.Seed = seed;
            CityApiHooks.Citizens = Enumerable.Range(0, 400).Reverse().Select(i => new CityApiHooks.Citizen(i, "Given" + i, "Family" + i, i == 0 ? null : "district" + i % 3,
                20 + i % 60, i % 2 == 0, ((Fake.FlarePersona)(i % 8)).ToString(), Fake.FlareAvatarRules.Citizen(seed, i).Seed)).ToList();
        }

        private static string ReferenceHash(int seed, IReadOnlyList<CitizenIdentity> identities)
        {
            using var bytes = new MemoryStream();
            using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true))
            {
                writer.Write("persistentcity:rlo-population:v1"); writer.Write(seed); writer.Write(identities.Count);
                foreach (var c in identities) { writer.Write(c.Id); writer.Write(c.Name); writer.Write(c.District); writer.Write(c.Age); writer.Write(c.Female); writer.Write((int)c.Persona); writer.Write(c.AppearanceSeed); }
            }
            using var hash = SHA256.Create();
            return "rlo:0.40.4:" + BitConverter.ToString(hash.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
        }

        [Fact]
        public void ThePeriodicRloReadKeepsItsPopulationKeyAndAllocatesLess()
        {
            Serve(1000);
            var rlo = StandIn.Build("0.99.0");
            var adapter = new RloIdentityAdapter();
            var first = adapter.Read(rlo);
            Assert.Equal(RloReadStatus.Ready, first.Status);
            Assert.Equal(ReferenceHash(first.CitySeed, first.Identities), first.PopulationKey);
            for (int i = 0; i < 3; i++) adapter.Read(rlo);
            long before = GC.GetAllocatedBytesForCurrentThread();
            const int reads = 20;
            for (int i = 0; i < reads; i++) Assert.Equal(first.PopulationKey, adapter.Read(rlo).PopulationKey);
            long perRead = (GC.GetAllocatedBytesForCurrentThread() - before) / reads;
            output.WriteLine("RLO periodic read (CityApi stand-in, 400 citizens): " + perRead + " B per read, " + perRead * 20 / 1000 + " KB per 600 s at one read per 30 s");
            Assert.True(perRead < 400_000, perRead.ToString());
            foreach (string name in NamesOf) { Assert.True(RloBridge.TryPersona(name, out var parsed)); Assert.Equal(name, parsed.ToString()); }
            Assert.False(RloBridge.TryPersona("nobody", out _));
            long personaBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 4000; i++) RloBridge.TryPersona(NamesOf[i % NamesOf.Length], out _);
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - personaBefore);
        }

        private static readonly string[] NamesOf = Enum.GetNames(typeof(Persona));

        [Fact]
        public void AnEmptyRoadSpeedFrameAllocatesNothingBeyondTheReflectionBox()
        {
            Serve(1000);
            var rlo = StandIn.Build("0.99.0");
            var reviewed = new RloRoadSpeedOverrides(rlo);
            var copied = new NativeRoadSpeeds(() => NativeRoadSpeeds.Copy(reviewed.Capture(), pair => (pair.OriginalKilometresPerHour, pair.EffectiveKilometresPerHour)));
            var dictionary = new NativeRoadSpeeds(() => reviewed.Capture().ToDictionary(pair => pair.Key,
                pair => (pair.Value.OriginalKilometresPerHour, pair.Value.EffectiveKilometresPerHour), NativeRoadSpeeds.ReferenceComparer));
            long Frame(NativeRoadSpeeds speeds)
            {
                for (int i = 0; i < 16; i++) speeds.Refresh();
                long before = GC.GetAllocatedBytesForCurrentThread();
                const int frames = 1000;
                for (int i = 0; i < frames; i++) speeds.Refresh();
                Assert.True(speeds.Verified);
                return (GC.GetAllocatedBytesForCurrentThread() - before) / frames;
            }
            long now = Frame(copied), old = Frame(dictionary);
            output.WriteLine("empty road-speed frame: before (ToDictionary + new capture dictionary) " + old + " B, after " + now + " B; per 600 s at 28 fps: "
                + old * 16800 / 1000 + " KB -> " + now * 16800 / 1000 + " KB");
            Assert.True(now <= 64, now.ToString());
            Assert.True(now * 2 < old);
        }

        [Fact]
        public void TheCoreCallsOutsideAdvanceStayCheapPerFrameAtTheCap()
        {
            var state = Gr5AllocationMeasure.City();
            var simulator = new CitySimulator(state, new FreshCityTraceTests.Walk());
            simulator.AttachFoodMarket(new VisitorRoutineTests.Market { Shops = F5gPopulationFillTests.Shops(state) });
            double day = Math.Floor(state.Minute / 1440) * 1440 + 1440, start = day + 15.5 * 60;
            simulator.AdvanceTo(start - 10, int.MaxValue);
            const int fps = 28;
            long observe = 0, stops = 0, closures = 0;
            int frames = 0;
            for (double minute = start - 10; minute < start + 20; minute += 0.333 / fps)
            {
                bool counted = minute >= start;
                long t0 = GC.GetAllocatedBytesForCurrentThread();
                simulator.BeginAccountingObservations(minute);
                long t1 = GC.GetAllocatedBytesForCurrentThread();
                simulator.RefreshOptionalStops();
                long t2 = GC.GetAllocatedBytesForCurrentThread();
                simulator.RefreshTrafficClosures();
                long t3 = GC.GetAllocatedBytesForCurrentThread();
                simulator.AdvanceFrameSlice(minute, 32);
                if (!counted) continue;
                observe += t1 - t0; stops += t2 - t1; closures += t3 - t2; frames++;
            }
            output.WriteLine("1,000 residents / 2,000 visitors, day 15:30, " + frames + " frames: BeginAccountingObservations " + observe / frames
                + " B/frame, RefreshOptionalStops " + stops / frames + " B/frame, RefreshTrafficClosures " + closures / frames + " B/frame; per 600 s at 28 fps "
                + (observe + stops + closures) / frames * 16800 / 1000 + " KB");
            Assert.True((observe + stops + closures) / frames < 1024, observe + "/" + stops + "/" + closures);
        }
    }
}
