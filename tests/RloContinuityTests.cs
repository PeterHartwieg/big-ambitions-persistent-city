using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using PersistentCity.Compatibility;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;
using Xunit.Abstractions;
using Fake = RealLifeOverhaul.Flare;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace PersistentCity.Tests
{
    /// <summary>F8d (docs/plans/f8-capability-boundaries.md D6 option B): after activation the saved donor catalogue is
    /// authoritative. Transient RLO answers (Off, NotYet, Refreshing) continue and save; a different catalogue and Shared,
    /// Failed or Incompatible stop as before. Through the stand-in CityApi (RloBridgeTests) and the reviewed reflection
    /// contract's stand-in (RloCompatibilityTests). Red on the base code for the transient cases: there every non-Ready
    /// read failed MatchesPopulation and stopped the running city.</summary>
    [Collection(RloCompatibilityTests.SharedFlareState)]
    public sealed class RloContinuityTests
    {
        private readonly ITestOutputHelper output;
        private readonly RloIdentityAdapter adapter = new RloIdentityAdapter();
        public RloContinuityTests(ITestOutputHelper output) { this.output = output; CityApiHooks.Reset(); RealLifeOverhaul.GoingPublic.InSession = false; }

        private static Fake.FlareDirector Flare(int seed = 1000) => new Fake.FlareDirector
        {
            File = new Fake.FlareFile { Seed = seed }, Running = true,
            Citizens = Enumerable.Range(0, 400).Select(i => new Fake.FlareCitizen
            {
                Index = i, First = "Given" + i, Last = "Family" + i, District = "district" + i % 3,
                Age = 20 + i % 60, Female = i % 2 == 0, Persona = (Fake.FlarePersona)(i % 8)
            }).ToList()
        };

        private static void Serve(Fake.FlareDirector city)
        {
            CityApiHooks.Seed = city.File!.Seed;
            CityApiHooks.Citizens = city.Citizens.Select(c => new CityApiHooks.Citizen(c.Index, c.First, c.Last, c.District, c.Age, c.Female,
                c.Persona.ToString(), Fake.FlareAvatarRules.Citizen(city.File.Seed, c.Index).Seed)).ToList();
        }

        private static CityState Create(RloReadResult ready)
        {
            var homes = Enumerable.Range(0, 40).Select(i => new Place { Id = "home" + i, Name = "Home " + i, Kind = PlaceKind.Home, Capacity = 12,
                District = "district" + i % 3, Position = new Position(i * 30, 0, 0) }).ToArray();
            return Population.CreateFromHousing(ready.Identities, homes, ready.PopulationKey, ready.SourceVersion, ready.CitySeed, 600);
        }

        private static string IdentityDigest(CityState state)
        {
            var text = new StringBuilder(state.PopulationKey).Append('|').Append(state.Seed);
            foreach (var c in state.Citizens.OrderBy(c => c.Identity.Id, StringComparer.Ordinal))
                text.Append('|').Append(c.Identity.Id).Append(':').Append(c.Identity.Name).Append(':').Append(c.Identity.AppearanceSeed);
            foreach (var s in state.SourceIdentities) text.Append("|s").Append(s.Id).Append(':').Append(s.Name);
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));
        }

        /// <summary>The save step's gate (PersistentCityMod.CaptureSteps): continue or refuse, then the unchanged writer.</summary>
        private static string? SaveAfter(RloReadResult read, CityState state) => DonorReads.Continues(read.Continuity(state)) ? SaveCodec.Write(state) : null;

        [Theory]
        [InlineData(0, DonorReadReason.Ready, DonorContinuity.Match)]
        [InlineData(1, DonorReadReason.Off, DonorContinuity.Transient)]
        [InlineData(2, DonorReadReason.NotYet, DonorContinuity.Transient)]
        [InlineData(3, DonorReadReason.Refreshing, DonorContinuity.Transient)]
        [InlineData(4, DonorReadReason.Shared, DonorContinuity.Unsupported)]
        [InlineData(5, DonorReadReason.Failed, DonorContinuity.Unsupported)]
        public void EveryBridgeStatusMapsToItsReasonClass(int status, DonorReadReason reason, DonorContinuity continuity)
        {
            Serve(Flare());
            var rlo = StandIn.Build("0.99.0");
            var state = Create(adapter.Read(rlo));
            CityApiHooks.CitizensStatus = status;
            var read = adapter.Read(rlo);
            Assert.Equal(reason, read.Reason);
            Assert.Equal(continuity, read.Continuity(state));
            // The four statuses and their messages are unchanged; only the class is new.
            Assert.Equal(status == 0 ? RloReadStatus.Ready : status == 5 ? RloReadStatus.Incompatible : RloReadStatus.NotReady, read.Status);
            if (status == 5) Assert.Equal("ContractException: RLO's CityApi reported a fault.", read.Message);
            // Red on the base code: the base periodic check stopped on any read that does not match.
            if (continuity == DonorContinuity.Transient) { Assert.False(read.MatchesPopulation(state)); Assert.True(DonorReads.Continues(continuity)); }
            else Assert.Equal(continuity == DonorContinuity.Match, DonorReads.Continues(continuity));
        }

        [Fact]
        public void ApiFaultsAndEmptyAnswersAreClassed()
        {
            Serve(Flare());
            var rlo = StandIn.Build("0.99.0");
            var state = Create(adapter.Read(rlo));
            CityApiHooks.Throw = true;
            var thrown = adapter.Read(rlo);
            Assert.Equal((RloReadStatus.Incompatible, DonorReadReason.Incompatible, DonorContinuity.Unsupported), (thrown.Status, thrown.Reason, thrown.Continuity(state)));
            CityApiHooks.Throw = false; CityApiHooks.Seed = 0;
            Assert.Equal(DonorReadReason.NotYet, adapter.Read(rlo).Reason);
            Serve(Flare()); CityApiHooks.Citizens.Clear();
            Assert.Equal(DonorReadReason.NotYet, adapter.Read(rlo).Reason);
            Assert.Equal(DonorReadReason.Missing, adapter.Read(null).Reason);
            Assert.Equal(DonorContinuity.Unsupported, adapter.Read(null).Continuity(state));
            Assert.Throws<ArgumentException>(() => DonorReads.Continuity(DonorReadReason.Ready));
            Assert.Equal(DonorContinuity.Unsupported, DonorReads.Continuity(DonorReadReason.NotReady));
        }

        [Fact]
        public void ReadyButDifferentStopsWithThePayloadKept()
        {
            Serve(Flare());
            var rlo = StandIn.Build("0.99.0");
            var state = Create(adapter.Read(rlo));
            var data = new Dictionary<string, string> { [SaveStore.StateKey] = SaveCodec.Write(state) };
            string kept = data[SaveStore.StateKey];
            CityApiHooks.Citizens[7] = CityApiHooks.Citizens[7] with { AppearanceSeed = 12345 };
            var different = adapter.Read(rlo);
            Assert.Equal(RloReadStatus.Ready, different.Status);
            Assert.Equal(DonorContinuity.Different, different.Continuity(state));
            Assert.Null(SaveAfter(different, state));
            Assert.Equal(kept, data[SaveStore.StateKey]);
            // The next load refuses it too (activation stays strict).
            Assert.False(different.MatchesPopulation(SaveCodec.Read(kept).State!));
        }

        [Fact]
        public void ATransientWindowContinuesSavesTheReadyBytesAndChangesNoIdentity()
        {
            var flare = Flare(); Serve(flare);
            var rlo = StandIn.Build("0.99.0");
            var ready = adapter.Read(rlo);
            var state = Create(ready);
            string readySave = SaveAfter(ready, state)!;
            string digest = IdentityDigest(state);
            int residents = state.Citizens.Count;

            foreach (int status in new[] { 1, 2, 3, 2, 1 })
            {
                CityApiHooks.CitizensStatus = status;
                var read = adapter.Read(rlo);
                Assert.Equal(DonorContinuity.Transient, read.Continuity(state));
                Assert.Empty(read.Identities); // nothing from RLO enters the city during the window
                // A save in the window equals a Ready save of the same state, byte for byte.
                Assert.Equal(readySave, SaveAfter(read, state));
                Assert.Equal(digest, IdentityDigest(state));
                Assert.Equal(residents, state.Citizens.Count);
            }
            CityApiHooks.CitizensStatus = 0;
            var back = adapter.Read(rlo);
            Assert.Equal(DonorContinuity.Match, back.Continuity(state));

            // Reload with the matching RLO continues; with a different RLO world it refuses.
            var loaded = SaveCodec.Read(readySave);
            Assert.Equal(SaveReadStatus.Loaded, loaded.Status);
            Assert.True(back.MatchesPopulation(loaded.State!));
            Assert.Equal(digest, IdentityDigest(loaded.State!));
            Serve(Flare(seed: 2000));
            Assert.False(adapter.Read(rlo).MatchesPopulation(loaded.State!));
        }

        [Fact]
        public void ReservationsReserveNobodyUnderOffAndEveryoneUnderNotYetOrRefreshing()
        {
            Serve(Flare());
            var rlo = StandIn.Build("0.99.0");
            var ready = adapter.Read(rlo);
            var guard = new RloPhysicalReservations(rlo, ready.Identities);
            Assert.True(guard.Ready, guard.LastError);
            CityApiHooks.ReservedStatus = CityApiHooks.BodiesStatus = 1;
            guard.Update();
            Assert.True(guard.Ready); Assert.Empty(guard.ReservedCitizenIds); Assert.False(guard.IsReserved("c3"));
            foreach (int status in new[] { 2, 3 })
            {
                CityApiHooks.ReservedStatus = CityApiHooks.BodiesStatus = status;
                guard.Update();
                Assert.True(guard.Ready); Assert.True(guard.IsReserved("c3")); Assert.True(guard.IsReserved("pc:9"));
            }
            // Road speeds: Off is "no slowdowns"; NotYet and Refreshing are refused (the recorded downstream effect).
            CityApiHooks.LanesStatus = 1;
            Assert.Empty(new RloRoadSpeedOverrides(rlo).Capture());
            foreach (int status in new[] { 2, 3 })
            {
                CityApiHooks.LanesStatus = status;
                Assert.Throws<InvalidOperationException>(() => new RloRoadSpeedOverrides(rlo).Capture());
            }
        }

        [Fact]
        public void ReflectionFallbackClassesGoingPublicAsStopAndFlareOffAsTransient()
        {
            var flare = Flare();
            var assembly = typeof(Fake.FlareDirector).Assembly;
            var ready = adapter.ReadContract(assembly, "0.45.0", flare, true);
            Assert.Equal(RloReadStatus.Ready, ready.Status);
            var state = Create(ready);

            RealLifeOverhaul.GoingPublic.InSession = true;
            var shared = adapter.ReadContract(assembly, "0.45.0", flare, true);
            Assert.Equal((DonorReadReason.Shared, DonorContinuity.Unsupported), (shared.Reason, shared.Continuity(state)));
            RealLifeOverhaul.GoingPublic.InSession = false;

            flare.Running = false;
            var off = adapter.ReadContract(assembly, "0.45.0", flare, true);
            Assert.Equal((DonorReadReason.Off, DonorContinuity.Transient), (off.Reason, off.Continuity(state)));
            flare.Running = true;

            Assert.Equal(DonorReadReason.NotYet, adapter.ReadContract(assembly, "0.45.0", new Fake.FlareDirector { Running = true }, true).Reason);
            Assert.Equal(DonorReadReason.NotYet, adapter.ReadContract(assembly, "0.45.0", null, true).Reason);
            Assert.Equal(DonorContinuity.Match, adapter.ReadContract(assembly, "0.45.0", flare, true).Continuity(state));
        }

        [Fact]
        public void TheReportNamesTheTransientWindowAndTheRunningCity()
        {
            var inputs = CapabilityReportTests.Healthy();
            inputs.RloReason = DonorReadReason.Off; inputs.RloMessage = "RLO's Flare is switched off or not running."; inputs.RloTransient = true;
            var registry = CapabilitySnapshot.Build(inputs);
            string text = new CapabilityReport().Status(registry, "0.9.1", CapabilitySnapshot.Context(inputs));
            Assert.StartsWith("Persistent City capabilities (game 0.9.1): city running\n", text);
            Assert.Contains("rlo.donor required off: transient (Off): RLO's Flare is switched off or not running. The city continues and saves on its saved donor catalogue. | scope: whole city", text);
            Assert.Equal(CapabilityState.Active, registry.State(CapabilityId.RloReservations));
            Assert.Equal(CapabilityState.Active, registry.State(CapabilityId.RloRoadSpeeds));

            inputs.RloReason = DonorReadReason.Refreshing;
            registry = CapabilitySnapshot.Build(inputs);
            Assert.Contains("visible bodies pause", registry.Reason(CapabilityId.RloReservations));
            Assert.Contains("road preparation throws and the runtime stops", registry.Reason(CapabilityId.RloRoadSpeeds));
            Assert.True(registry.IsEnabled(CapabilityScope.MarketResearch));
            Assert.False(registry.IsEnabled(CapabilityScope.VisibleBodies));

            // Before a city: a transient answer holds the start (TryStart waits); Shared stops it.
            inputs.CityActive = false;
            Assert.Equal("start waiting (rlo.donor transient)", CapabilitySnapshot.Context(inputs));
            Assert.Contains("Persistent City waits for RLO before it starts.", CapabilitySnapshot.Build(inputs).Reason(CapabilityId.RloDonor));
            inputs.RloReason = DonorReadReason.Shared;
            Assert.Null(CapabilitySnapshot.Context(inputs));
            Assert.Equal(CapabilityStartStatus.Refused, CapabilitySnapshot.Build(inputs).Start().Status);
            inputs.RloReason = DonorReadReason.Missing;
            Assert.Equal(CapabilityState.Absent, CapabilitySnapshot.Build(inputs).State(CapabilityId.RloRoadSpeeds));
        }

        /// <summary>The three call sites use the class; activation stays strict; no extra RLO read and the 30 s cadence
        /// is unchanged.</summary>
        [Fact]
        public void CallSitesUseTheClassAndAddNoRead()
        {
            string mod = File.ReadAllText(Path.Combine(CapabilityReportTests.Root(), "Runtime", "PersistentCityMod.cs")).Replace("\r\n", "\n");
            Assert.Equal(4, System.Text.RegularExpressions.Regex.Matches(mod, @"ReadRlo\(""").Count); // startup, periodic, activation, save
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(mod, @"rlo\.Read\("));
            Assert.Contains("nextDependencyCheck = Time.unscaledTime + 30;", mod);
            Assert.Contains("var continuity = source.Continuity(simulation.State);", mod);
            Assert.Contains("if (!DonorReads.Continues(source.Continuity(simulation!.State)))", mod);
            Assert.Contains("if (!source.MatchesPopulation(state)) { Fail(\"RLO donor catalogue changed during initialization (", mod);
            Assert.Contains("log?.Info(CapabilityActivationLine());", mod);
            // Startup keeps its strict Ready requirement: no city is created from a transient answer.
            Assert.Contains("if (source.Status != RloReadStatus.Ready)", mod);
            // Compatibility owns every RLO read; the classification adds none.
            Assert.DoesNotContain("ReadRlo(", File.ReadAllText(Path.Combine(CapabilityReportTests.Root(), "Runtime", "CapabilityDiagnostics.cs")));
        }

        /// <summary>Plan section 6, F8d: classification cost per 30 s read (the read itself is RLO's and already logged).</summary>
        [Fact]
        public void ClassificationCostPerRead()
        {
            Serve(Flare());
            var rlo = StandIn.Build("0.99.0");
            var ready = adapter.Read(rlo);
            var state = Create(ready);
            CityApiHooks.CitizensStatus = 1;
            var transient = adapter.Read(rlo);
            const int Samples = 2_000;
            double tick = 1000.0 / Stopwatch.Frequency;
            string Measure(string arm, RloReadResult read)
            {
                var times = new double[Samples];
                for (int i = -200; i < Samples; i++)
                {
                    long t0 = Stopwatch.GetTimestamp();
                    _ = read.Continuity(state);
                    if (i >= 0) times[i] = (Stopwatch.GetTimestamp() - t0) * tick;
                }
                Array.Sort(times);
                return string.Format(CultureInfo.InvariantCulture, "F8d classification {0}: p50={1:0.0000} ms p95={2:0.0000} ms max={3:0.0000} ms", arm,
                    times[Samples / 2], times[Samples * 95 / 100], times[^1]);
            }
            var lines = new[] { Measure("Ready (400-identity match, as before)", ready), Measure("transient (no comparison)", transient) };
            foreach (var line in lines) output.WriteLine(line);
            string? path = Environment.GetEnvironmentVariable("PC_F8_MEASURE");
            if (!string.IsNullOrEmpty(path)) File.AppendAllLines(path, lines);
        }
    }
}
