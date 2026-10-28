using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using PersistentCity.Compatibility;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;
using Fake = RealLifeOverhaul.Flare;

namespace PersistentCity.Tests
{
    [Collection(SharedFlareState)]
    public sealed class RloCompatibilityTests
    {
        // Both this class and RloBridgeTests set the stand-in RealLifeOverhaul.GoingPublic.InSession flag.
        public const string SharedFlareState = "RLO stand-in GoingPublic state";

        private readonly RloIdentityAdapter adapter = new RloIdentityAdapter();
        private RloReadResult Read(object? director) => adapter.ReadContract(typeof(Fake.FlareDirector).Assembly, RloIdentityAdapter.ReviewedVersion, director, true);

        private static Fake.FlareDirector City(int seed = 1000)
        {
            RealLifeOverhaul.GoingPublic.InSession = false;
            return new Fake.FlareDirector
            {
                File = new Fake.FlareFile { Seed = seed }, Running = true,
                Citizens = Enumerable.Range(0, 400).Select(i => new Fake.FlareCitizen
                {
                    Index = i, First = "Given" + i, Last = "Family" + i, District = i == 0 ? null : "district" + i % 3,
                    Age = 20 + i % 60, Female = i % 2 == 0, Persona = (Fake.FlarePersona)(i % 8)
                }).ToList()
            };
        }

        [Fact]
        public void PublicEntryRequiresActualReviewedDependency()
        {
            Assert.Equal(RloReadStatus.MissingDependency, adapter.Read(null).Status);
            Assert.Equal(RloReadStatus.Incompatible, adapter.Read(typeof(RloCompatibilityTests).Assembly).Status);
            Assert.True(RloIdentityAdapter.IsReviewedVersion("0.41.1+" + RloIdentityAdapter.ReviewedCommit));
            Assert.True(RloIdentityAdapter.IsReviewedVersion("0.40.4+46b3b8835e113ec3807344a9b7899439684004ad"));
            Assert.True(RloIdentityAdapter.IsReviewedVersion("0.40.4.0"));
            Assert.True(RloIdentityAdapter.IsReviewedVersion("0.45.0+b12d3581860ce380397223a2790fcce8b72b3482"));
            foreach (var version in new[] { "0.40.3", "0.40.5", "0.40.6", "0.41.0", "0.41.2", "0.40.4-preview", "0.40.4+unknown", "0.41.1+unknown" })
                Assert.False(RloIdentityAdapter.IsReviewedVersion(version));
        }

        [Theory]
        [InlineData("0.40.4.0", "0.40.4", true)]
        [InlineData("0.40.4.0", "0.40.4.0", true)]
        [InlineData("0.40.4.0", "0.40.4+46b3b8835e113ec3807344a9b7899439684004ad", true)]
        [InlineData("0.41.1.0", "0.41.1", true)]
        [InlineData("0.41.1.0", "0.41.1.0", true)]
        [InlineData("0.41.1.0", "0.41.1+dbd15c0958770219c0f4d969a8b2ac28593014d2", true)]
        [InlineData("0.45.0.0", "0.45.0+b12d3581860ce380397223a2790fcce8b72b3482", true)]
        [InlineData("0.45.0.0", "0.45.0+dbd15c0958770219c0f4d969a8b2ac28593014d2", false)]
        [InlineData("0.41.1.0", "0.45.0", false)]
        [InlineData("0.40.4.0", "0.41.1", false)]
        [InlineData("0.41.1.0", "0.40.4", false)]
        [InlineData("0.41.1.0", "0.41.1+46b3b8835e113ec3807344a9b7899439684004ad", false)]
        [InlineData("0.40.6.0", "0.40.6", false)]
        [InlineData("0.41.2.0", "0.41.2", false)]
        [InlineData("0.41.1.1", "0.41.1", false)]
        [InlineData("0.41.1.0", "", false)]
        [InlineData("0.41.1.0", null, false)]
        public void ReviewedAssemblyAndInformationMustBelongToTheSameRelease(string version, string? information, bool accepted)
        {
            var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("RealLifeOverhaul")
                { Version = Version.Parse(version) }, AssemblyBuilderAccess.Run);
            if (information != null)
                assembly.SetCustomAttribute(new CustomAttributeBuilder(
                    typeof(AssemblyInformationalVersionAttribute).GetConstructor(new[] { typeof(string) })!, new object[] { information }));
            Assert.Equal(accepted, RloIdentityAdapter.IsReviewedAssembly(assembly));
            var result = adapter.Read(assembly);
            Assert.Equal(RloReadStatus.Incompatible, result.Status); // Metadata alone never supplies a population.
            if (accepted) Assert.Contains("Missing public contract type", result.Message);
            else
            {
                Assert.Contains("version pair has not been reviewed", result.Message);
                Assert.False(new RloPhysicalReservations(assembly, new[] { new CitizenIdentity { Id = "c1" } }).Ready);
                Assert.Throws<InvalidOperationException>(() => new RloRoadSpeedOverrides(assembly));
            }
        }

        [Fact]
        public void ReviewedRloUpgradeKeepsExistingPopulationAndSaveWithoutRekeying()
        {
            var city = City();
            var oldSource = adapter.ReadContract(typeof(Fake.FlareDirector).Assembly, "0.40.4", city, true);
            var newSource = Read(city);
            Assert.Equal(RloReadStatus.Ready, oldSource.Status); Assert.Equal(RloReadStatus.Ready, newSource.Status);
            Assert.Equal("0.41.1", newSource.SourceVersion);
            Assert.StartsWith("rlo:0.40.4:", oldSource.PopulationKey);
            Assert.Equal(oldSource.PopulationKey, newSource.PopulationKey);
            var original = Population.CreateFromHousing(oldSource.Identities,
                new[] { new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 12 } },
                oldSource.PopulationKey, oldSource.SourceVersion, oldSource.CitySeed, 100);
            string payload = SaveCodec.Write(original);
            var data = new Dictionary<string, string> { [SaveStore.StateKey] = payload };
            var loaded = SaveStore.ReadFrom(data, newSource.PopulationKey);
            Assert.Equal(SaveReadStatus.Loaded, loaded.Status);
            Assert.True(newSource.MatchesPopulation(loaded.State!));
            Assert.Equal(payload, SaveCodec.Write(loaded.State!));
            Assert.Equal(payload, data[SaveStore.StateKey]);
            var changed = city.Citizens.ToArray(); changed[0].Last += " changed"; city.Citizens = changed;
            Assert.False(Read(city).MatchesPopulation(loaded.State!));
            Assert.Equal(SaveReadStatus.Invalid, SaveStore.ReadFrom(data, Read(city).PopulationKey).Status);
            Assert.Equal(payload, data[SaveStore.StateKey]);
        }

        [Fact]
        public void ReadsActualPopulationAndDetachesAllIdentityValues()
        {
            var city = City();
            var result = Read(city);
            Assert.Equal(RloReadStatus.Ready, result.Status);
            Assert.Equal(400, result.Identities.Count);
            foreach (var original in city.Citizens)
            {
                var copy = Assert.Single(result.Identities, c => c.Id == "c" + original.Index);
                Assert.Equal(original.First + " " + original.Last, copy.Name);
                Assert.Equal(original.District ?? "", copy.District);
                Assert.Equal(original.Age, copy.Age);
                Assert.Equal(original.Female, copy.Female);
                Assert.Equal(original.Persona.ToString(), copy.Persona.ToString());
                Assert.Equal(Fake.FlareAvatarRules.Citizen(city.File!.Seed, original.Index).Seed, copy.AppearanceSeed);
            }
            result.Identities[0].Name = "Changed only in Persistent City";
            Assert.Equal("Given0", city.Citizens[0].First);
            Assert.Equal(0, city.Mutations);
            Assert.Equal(0, Fake.FlareStore.Mutations);
        }

        [Fact]
        public void PopulationKeyIsIndependentOfListOrderAndChangesWithActualIdentity()
        {
            var city = City();
            var key = Read(city).PopulationKey;
            city.Citizens = city.Citizens.Reverse().ToList();
            Assert.Equal(key, Read(city).PopulationKey);
            var citizen = city.Citizens[0]; citizen.Last = "Changed name";
            var changed = city.Citizens.ToList(); changed[0] = citizen; city.Citizens = changed;
            Assert.NotEqual(key, Read(city).PopulationKey);
            Assert.NotEqual(key, Read(City(2000)).PopulationKey);
        }

        private CityState Snapshot(Fake.FlareDirector city)
        {
            var source = Read(city);
            return new CityState { Seed = source.CitySeed, PopulationKey = source.PopulationKey,
                SourceIdentities = source.Identities.Select(Population.CloneIdentity).ToList(),
                Citizens = source.Identities.Select(identity => new CitizenState { Identity = Population.CloneIdentity(identity) }).ToList() };
        }

        [Fact]
        public void SnapshotComparisonAcceptsReorderedMatchingIdentities()
        {
            var city = City(); var snapshot = Snapshot(city);
            snapshot.SourceIdentities.Reverse();
            Assert.True(Read(city).MatchesPopulation(snapshot));
        }

        [Theory]
        [InlineData("count")]
        [InlineData("seed")]
        [InlineData("key")]
        [InlineData("id")]
        [InlineData("name")]
        [InlineData("district")]
        [InlineData("age")]
        [InlineData("female")]
        [InlineData("persona")]
        [InlineData("appearance")]
        [InlineData("duplicate")]
        public void SnapshotComparisonRejectsAlteredPopulationDespiteRetainedFingerprint(string field)
        {
            var city = City(); var snapshot = Snapshot(city);
            var identity = snapshot.SourceIdentities[0];
            switch (field)
            {
                case "count": snapshot.SourceIdentities.RemoveAt(0); break;
                case "seed": snapshot.Seed++; break;
                case "key": snapshot.PopulationKey += "changed"; break;
                case "id": identity.Id = "unknown"; break;
                case "name": identity.Name += "changed"; break;
                case "district": identity.District += "changed"; break;
                case "age": identity.Age++; break;
                case "female": identity.Female = !identity.Female; break;
                case "persona": identity.Persona = Persona.NightOwl; break;
                case "appearance": identity.AppearanceSeed++; break;
                case "duplicate": snapshot.SourceIdentities[1] = identity; break;
            }
            Assert.False(Read(city).MatchesPopulation(snapshot));
        }

        [Fact]
        public void InactiveDependencyCannotValidateSnapshot()
        {
            var city = City(); var snapshot = Snapshot(city);
            city.Running = false;
            Assert.False(Read(city).MatchesPopulation(snapshot));
        }

        [Fact]
        public void UninitializedOrInactiveFlareNeverCreatesReplacementCitizens()
        {
            Assert.Equal(RloReadStatus.NotReady, Read(null).Status);
            var city = City(); city.Running = false;
            Assert.Equal(RloReadStatus.NotReady, Read(city).Status);
            city.Running = true; city.File = null;
            Assert.Equal(RloReadStatus.NotReady, Read(city).Status);
            city.File = new Fake.FlareFile();
            Assert.Equal(RloReadStatus.NotReady, Read(city).Status);
            city.File.Seed = 1000; city.Citizens = Array.Empty<Fake.FlareCitizen>();
            var result = Read(city);
            Assert.Equal(RloReadStatus.NotReady, result.Status);
            Assert.Empty(result.Identities); Assert.Empty(result.PopulationKey);
        }

        [Fact]
        public void PartialAndDuplicatePopulationFailClosed()
        {
            var city = City(); city.Citizens = city.Citizens.Take(399).ToList();
            Assert.Equal(RloReadStatus.Incompatible, Read(city).Status);
            city = City(); var citizens = city.Citizens.ToList(); citizens[399] = citizens[0]; city.Citizens = citizens;
            var result = Read(city);
            Assert.Equal(RloReadStatus.Incompatible, result.Status);
            Assert.Contains("duplicate", result.Message); Assert.Empty(result.Identities);
        }

        [Fact]
        public void AppearanceMustAgreeWithCurrentCitizen()
        {
            var city = City(); var citizens = city.Citizens.ToList(); var c = citizens[12]; c.Age++; citizens[12] = c; city.Citizens = citizens;
            var result = Read(city);
            Assert.Equal(RloReadStatus.NotReady, result.Status);
            Assert.Contains("disagree", result.Message); Assert.Empty(result.Identities);
        }

        [Fact]
        public void TheReflectionBindingIsReusedForItsAssemblyAndEveryReadStaysLive()
        {
            var assembly = typeof(Fake.FlareDirector).Assembly;
            var city = City(); var snapshot = Snapshot(city);
            Assert.Equal(1, adapter.Bindings);
            Assert.True(Read(city).MatchesPopulation(snapshot));
            Assert.Equal(1, adapter.Bindings);

            // Session, director, Flare, seed and every citizen are read again on the reused binding.
            RealLifeOverhaul.GoingPublic.InSession = true;
            Assert.Contains("Multiplayer", Read(city).Message);
            RealLifeOverhaul.GoingPublic.InSession = false;
            city.Running = false;
            Assert.False(Read(city).MatchesPopulation(snapshot));
            city.Running = true; city.File = new Fake.FlareFile { Seed = 2000 };
            Assert.False(Read(city).MatchesPopulation(snapshot));
            city.File = new Fake.FlareFile { Seed = 1000 };
            var citizens = city.Citizens.ToList(); var c = citizens[3]; c.Last = "Changed"; citizens[3] = c; city.Citizens = citizens;
            Assert.False(Read(city).MatchesPopulation(snapshot));
            Assert.True(Read(City()).MatchesPopulation(snapshot));
            Assert.Equal(1, adapter.Bindings);

            // The live director is looked up on each unsupplied read, never kept with the binding.
            var previous = Fake.FlareDirector.Instance;
            try
            {
                Fake.FlareDirector.Instance = City();
                Assert.True(adapter.ReadContract(assembly, RloIdentityAdapter.ReviewedVersion, null, false).MatchesPopulation(snapshot));
                Fake.FlareDirector.Instance = null;
                Assert.Contains("not available yet", adapter.ReadContract(assembly, RloIdentityAdapter.ReviewedVersion, null, false).Message);
                Fake.FlareDirector.Instance = City(2000);
                Assert.False(adapter.ReadContract(assembly, RloIdentityAdapter.ReviewedVersion, null, false).MatchesPopulation(snapshot));
            }
            finally { Fake.FlareDirector.Instance = previous; }
            Assert.Equal(1, adapter.Bindings);

            // A refusal drops the binding; the next read binds again, as every read did before.
            city = City(); citizens = city.Citizens.ToList(); citizens[399] = citizens[0]; city.Citizens = citizens;
            Assert.Equal(RloReadStatus.Incompatible, Read(city).Status);
            Assert.True(Read(City()).MatchesPopulation(snapshot));
            Assert.Equal(2, adapter.Bindings);
            Assert.Contains("Director snapshot", Read(new object()).Message);
            Assert.True(Read(City()).MatchesPopulation(snapshot));
            Assert.Equal(3, adapter.Bindings);

            // An assembly without the contract never replaces a good binding with a partial one.
            Assert.Equal(RloReadStatus.Incompatible, adapter.ReadContract(typeof(RloIdentityAdapter).Assembly, RloIdentityAdapter.ReviewedVersion, City(), true).Status);
            Assert.Equal(3, adapter.Bindings);
            Assert.True(Read(City()).MatchesPopulation(snapshot));
            Assert.Equal(4, adapter.Bindings);
        }

        [Fact]
        public void RejectsMultiplayerSessionWithoutCallingMutationMethods()
        {
            var city = City(); RealLifeOverhaul.GoingPublic.InSession = true;
            try
            {
                var result = Read(city);
                Assert.Equal(RloReadStatus.NotReady, result.Status); Assert.Contains("Multiplayer", result.Message);
                Assert.Empty(result.Identities); Assert.Equal(0, city.Mutations); Assert.Equal(0, Fake.FlareStore.Mutations);
            }
            finally { RealLifeOverhaul.GoingPublic.InSession = false; }
        }
    }
}

// Deliberately synthetic public shapes. These are not copied RLO rules or a substitute population for production.
namespace RealLifeOverhaul
{
    public static class GoingPublic { public static bool InSession { get; set; } }
}
namespace RealLifeOverhaul.Flare
{
    public enum FlarePersona { Commuter, Foodie, Parent, Student, Retiree, Sporty, Grumpy, NightOwl }
    public struct FlareCitizen
    {
        public int Index; public string First; public string Last; public string? District;
        public int Age; public bool Female; public FlarePersona Persona;
    }
    public sealed class FlareFile { public int Seed; }
    public sealed class FlareDirector
    {
        public static FlareDirector? Instance { get; set; }
        public bool Running { get; set; }
        public FlareFile? File { get; set; }
        public IReadOnlyList<FlareCitizen> Citizens { get; set; } = Array.Empty<FlareCitizen>();
        public int Mutations { get; private set; }
        public void Start() { Mutations++; throw new InvalidOperationException("Forbidden mutation."); }
    }
    public static class FlareLiveRules
    {
        public const int Citizens = 400;
        public static int CitySeed(FlareFile file) => file.Seed;
    }
    public struct FlareAvatarPlan { public int Seed; public int Age; public bool Female; }
    public static class FlareAvatarRules
    {
        public static FlareAvatarPlan Citizen(int seed, int index) => new FlareAvatarPlan { Seed = seed + index + 1, Age = 20 + index % 60, Female = index % 2 == 0 };
    }
    public static class FlareStore
    {
        public static int Mutations { get; private set; }
        public static FlareFile Load(Dictionary<string, string> data) { Mutations++; data["forbidden"] = "write"; throw new InvalidOperationException("Forbidden mutation."); }
    }
}
