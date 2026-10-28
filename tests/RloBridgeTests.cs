using System;
using System.Collections;
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
    /// <summary>RLO's CityApi read contract (docs/rlo-bridge.md), through emitted stand-in RLO assemblies. The stand-in's
    /// methods answer from <see cref="CityApiHooks"/>, so every test here resets it and none runs in parallel with another.</summary>
    [Collection(RloCompatibilityTests.SharedFlareState)]
    public sealed class RloBridgeTests
    {
        private readonly RloIdentityAdapter adapter = new RloIdentityAdapter();

        public RloBridgeTests() => CityApiHooks.Reset();

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

        // What contract 1 promises for the same account: the same citizens RLO's Flare has, in RLO's own order.
        private static void Serve(Fake.FlareDirector city)
        {
            CityApiHooks.Seed = city.File!.Seed;
            CityApiHooks.Citizens = city.Citizens.Reverse().Select(c => new CityApiHooks.Citizen(c.Index, c.First, c.Last, c.District, c.Age, c.Female,
                c.Persona.ToString(), Fake.FlareAvatarRules.Citizen(city.File.Seed, c.Index).Seed)).ToList();
        }

        [Fact]
        public void UnreviewedReleaseWithTheContractIsReadWithoutAPin()
        {
            var city = City(); Serve(city);
            var rlo = StandIn.Build("0.99.0");
            Assert.False(RloIdentityAdapter.IsReviewedAssembly(rlo));
            var result = adapter.Read(rlo);
            Assert.Equal(RloReadStatus.Ready, result.Status);
            Assert.Equal("0.99.0", result.SourceVersion);
            Assert.Contains("CityApi contract 1", result.Message);
            Assert.Equal(400, result.Identities.Count);
            var reservations = new RloPhysicalReservations(rlo, result.Identities);
            Assert.True(reservations.Ready, reservations.LastError);
            Assert.Empty(new RloRoadSpeedOverrides(rlo).Capture());

            // The same release without the contract stays refused everywhere.
            var bare = StandIn.Build("0.99.0", fault: "CityApi");
            Assert.Equal(RloReadStatus.Incompatible, adapter.Read(bare).Status);
            Assert.False(new RloPhysicalReservations(bare, result.Identities).Ready);
            Assert.Throws<InvalidOperationException>(() => new RloRoadSpeedOverrides(bare));
        }

        [Fact]
        public void ACitySavedThroughReflectionLoadsThroughTheContractWithTheSameKey()
        {
            var city = City(); Serve(city);
            var before = adapter.ReadContract(typeof(Fake.FlareDirector).Assembly, "0.45.0", city, true);
            var after = adapter.Read(StandIn.Build("0.99.0"));
            Assert.Equal(RloReadStatus.Ready, before.Status); Assert.Equal(RloReadStatus.Ready, after.Status);
            Assert.StartsWith("rlo:0.40.4:", after.PopulationKey);
            Assert.Equal(before.PopulationKey, after.PopulationKey);
            Assert.Equal(before.CitySeed, after.CitySeed);
            Assert.Equal(before.Identities.Select(Describe), after.Identities.Select(Describe));

            var original = Population.CreateFromHousing(before.Identities,
                new[] { new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 12 } }, before.PopulationKey, before.SourceVersion, before.CitySeed, 100);
            string payload = SaveCodec.Write(original);
            var data = new Dictionary<string, string> { [SaveStore.StateKey] = payload };
            var loaded = SaveStore.ReadFrom(data, after.PopulationKey);
            Assert.Equal(SaveReadStatus.Loaded, loaded.Status);
            Assert.True(after.MatchesPopulation(loaded.State!));
            Assert.Equal(payload, SaveCodec.Write(loaded.State!));

            // A citizen RLO now draws differently is a different donor set: the stored city is refused, never rekeyed.
            CityApiHooks.Citizens[7] = CityApiHooks.Citizens[7] with { AppearanceSeed = 12345 };
            var changed = adapter.Read(StandIn.Build("0.99.1"));
            Assert.False(changed.MatchesPopulation(loaded.State!));
            Assert.Equal(SaveReadStatus.Invalid, SaveStore.ReadFrom(data, changed.PopulationKey).Status);
            Assert.Equal(payload, data[SaveStore.StateKey]);
        }

        [Theory]
        [InlineData(1, RloReadStatus.NotReady, "switched off")]
        [InlineData(2, RloReadStatus.NotReady, "no Flare account")]
        [InlineData(3, RloReadStatus.NotReady, "refreshing")]
        [InlineData(4, RloReadStatus.NotReady, "Going Public")]
        [InlineData(5, RloReadStatus.Incompatible, "fault")]
        public void StatusesOtherThanReadyGiveNoIdentities(int status, RloReadStatus expected, string message)
        {
            Serve(City()); CityApiHooks.CitizensStatus = status;
            var result = adapter.Read(StandIn.Build("0.99.0"));
            Assert.Equal(expected, result.Status);
            Assert.Contains(message, result.Message);
            Assert.Empty(result.Identities);
        }

        [Theory]
        [InlineData("count", "found 399")]
        [InlineData("duplicate", "duplicate or out-of-range Index")]
        [InlineData("range", "duplicate or out-of-range Index")]
        [InlineData("name", "complete RLO name")]
        [InlineData("age", "invalid age or persona")]
        [InlineData("numeric persona", "invalid age or persona")]
        [InlineData("unknown persona", "invalid age or persona")]
        [InlineData("appearance", "no appearance seed")]
        public void InvalidPopulationIsIncompatible(string fault, string message)
        {
            Serve(City());
            var c = CityApiHooks.Citizens;
            switch (fault)
            {
                case "count": c.RemoveAt(0); break;
                case "duplicate": c[0] = c[0] with { Index = c[1].Index }; break;
                case "range": c[0] = c[0] with { Index = 400 }; break;
                case "name": c[0] = c[0] with { Last = " " }; break;
                case "age": c[0] = c[0] with { Age = 0 }; break;
                case "numeric persona": c[0] = c[0] with { Persona = "3" }; break;
                case "unknown persona": c[0] = c[0] with { Persona = "Pirate" }; break;
                case "appearance": c[0] = c[0] with { AppearanceSeed = 0 }; break;
            }
            var result = adapter.Read(StandIn.Build("0.99.0"));
            Assert.Equal(RloReadStatus.Incompatible, result.Status);
            Assert.Contains(message, result.Message);
            Assert.Empty(result.Identities);
        }

        [Fact]
        public void EmptyPopulationOrSeedIsNotReadyYet()
        {
            Serve(City()); CityApiHooks.Seed = 0;
            Assert.Equal(RloReadStatus.NotReady, adapter.Read(StandIn.Build("0.99.0")).Status);
            Serve(City()); CityApiHooks.Citizens.Clear();
            Assert.Equal(RloReadStatus.NotReady, adapter.Read(StandIn.Build("0.99.0")).Status);
        }

        [Theory]
        [InlineData("0.99.0", 2, "")]
        [InlineData("0.45.0", 2, "")]          // a reviewed release number does not rescue an unsupported contract
        [InlineData("0.99.0", 0, "")]
        [InlineData("0.99.0", 1, "ReadSlowedLanes")]
        [InlineData("0.99.0", 1, "Shared")]
        [InlineData("0.99.0", 1, "out")]
        [InlineData("0.99.0", 1, "persona int")]
        public void AContractPersistentCityCannotUseRefusesEverywhereWithoutFallback(string version, int contract, string fault)
        {
            Serve(City());
            var rlo = StandIn.Build(version, contract, fault);
            var result = adapter.Read(rlo);
            Assert.Equal(RloReadStatus.Incompatible, result.Status);
            Assert.Contains("RLO CityApi:", result.Message);
            var reservations = new RloPhysicalReservations(rlo, new[] { new CitizenIdentity { Id = "c1" } });
            Assert.False(reservations.Ready);
            Assert.Contains("RLO CityApi:", reservations.LastError);
            Assert.Contains("RLO CityApi:", Assert.Throws<InvalidOperationException>(() => new RloRoadSpeedOverrides(rlo)).Message);
        }

        [Fact]
        public void AnApiThatThrowsIsIncompatible()
        {
            Serve(City()); CityApiHooks.Throw = true;
            var result = adapter.Read(StandIn.Build("0.99.0"));
            Assert.Equal(RloReadStatus.Incompatible, result.Status);
            Assert.Contains("forbids", result.Message);
        }

        [Fact]
        public void ReservationsFollowTheContractAndFailClosed()
        {
            var rlo = StandIn.Build("0.99.0");
            var sources = new[] { new CitizenIdentity { Id = "c1" }, new CitizenIdentity { Id = "c5" }, new CitizenIdentity { Id = "pc:1" } };
            object pedestrian = new object(), character = new object(), other = new object();
            CityApiHooks.Reserved = new List<int> { 1, 5, 7, 999 };
            CityApiHooks.Pedestrians = new List<object> { pedestrian };
            CityApiHooks.Characters = new List<object> { character };
            var guard = new RloPhysicalReservations(rlo, sources);
            Assert.True(guard.Ready, guard.LastError);
            // c7 and c999 are not Persistent City's donors, and a generated resident is never reserved by index.
            Assert.Equal(new[] { "c1", "c5" }, guard.ReservedCitizenIds.OrderBy(id => id, StringComparer.Ordinal));
            Assert.False(guard.IsReserved("pc:1"));
            Assert.True(guard.IsReservedNativePedestrian(pedestrian, null));
            Assert.True(guard.IsReservedNativePedestrian(other, character));
            Assert.False(guard.IsReservedNativePedestrian(other, other));

            // An answer that says nothing about what RLO holds reserves everyone for that read; the other read still updates.
            foreach (int unknown in new[] { 2, 3 })
            {
                CityApiHooks.ReservedStatus = unknown; CityApiHooks.Pedestrians = new List<object>();
                guard.Update();
                Assert.True(guard.Ready);
                Assert.True(guard.IsReserved("c7")); Assert.True(guard.IsReserved("pc:1"));
                Assert.False(guard.IsReservedNativePedestrian(pedestrian, null));
                CityApiHooks.ReservedStatus = 0; CityApiHooks.BodiesStatus = unknown;
                guard.Update();
                Assert.False(guard.IsReserved("pc:1"));
                Assert.True(guard.IsReservedNativePedestrian(other, null));
                CityApiHooks.BodiesStatus = 0; CityApiHooks.Pedestrians = new List<object> { pedestrian };
            }

            // Switched off: nobody is RLO's.
            CityApiHooks.ReservedStatus = 1; CityApiHooks.BodiesStatus = 1;
            guard.Update();
            Assert.True(guard.Ready);
            Assert.Empty(guard.ReservedCitizenIds);
            Assert.False(guard.IsReservedNativePedestrian(other, character));

            // A fault stops the guard, which then reserves everyone.
            CityApiHooks.BodiesStatus = 5;
            guard.Update();
            Assert.False(guard.Ready);
            Assert.Contains("ReadReservedBodies", guard.LastError);
            Assert.True(guard.IsReserved("pc:1"));
            Assert.True(guard.IsReservedNativePedestrian(other, null));
        }

        [Fact]
        public void SlowedLanesComeFromTheContractAndBadAnswersAreRefused()
        {
            var speeds = new RloRoadSpeedOverrides(StandIn.Build("0.99.0"));
            object a = new object(), b = new object();
            CityApiHooks.Lanes = new List<(object, int, int)> { (a, 50, 35), (b, 20, 20) };
            var snapshot = speeds.Capture();
            Assert.Equal(2, snapshot.Count);
            Assert.Equal(50, snapshot[a].OriginalKilometresPerHour); Assert.Equal(35, snapshot[a].EffectiveKilometresPerHour);
            Assert.Equal(20, snapshot[b].EffectiveKilometresPerHour);
            // The snapshot is detached from the next capture.
            CityApiHooks.Lanes = new List<(object, int, int)>();
            Assert.Empty(speeds.Capture());
            Assert.Equal(2, snapshot.Count);

            CityApiHooks.LanesStatus = 1;
            Assert.Empty(speeds.Capture());
            foreach (int unknown in new[] { 2, 3, 4, 5 })
            {
                CityApiHooks.LanesStatus = unknown;
                Assert.Throws<InvalidOperationException>(() => speeds.Capture());
            }
            CityApiHooks.LanesStatus = 0;
            CityApiHooks.Lanes = new List<(object, int, int)> { (a, 50, 35), (a, 50, 35) };
            Assert.Throws<InvalidOperationException>(() => speeds.Capture());
            CityApiHooks.Lanes = new List<(object, int, int)> { (a, 0, 0) };
            Assert.Throws<InvalidOperationException>(() => speeds.Capture());
            CityApiHooks.Lanes = new List<(object, int, int)> { (a, 50, 35) }; CityApiHooks.ShortLimits = true;
            Assert.Throws<InvalidOperationException>(() => speeds.Capture());
        }

        [Fact]
        public void TheCityApiBindingIsReusedForItsAssemblyAndEveryReadStaysLive()
        {
            var city = City(); Serve(city);
            var rlo = StandIn.Build("0.99.0", liveContract: true);
            var first = adapter.Read(rlo);
            Assert.Equal(RloReadStatus.Ready, first.Status);
            Assert.Equal(1, adapter.Bindings);
            Assert.Equal(first.PopulationKey, adapter.Read(rlo).PopulationKey);
            Assert.Equal(1, adapter.Bindings);

            // Status, citizens and seed are asked for again on the reused binding.
            CityApiHooks.CitizensStatus = 3;
            Assert.Contains("refreshing", adapter.Read(rlo).Message);
            CityApiHooks.CitizensStatus = 0;
            CityApiHooks.Citizens[7] = CityApiHooks.Citizens[7] with { AppearanceSeed = 12345 };
            var changed = adapter.Read(rlo);
            Assert.Equal(RloReadStatus.Ready, changed.Status);
            Assert.NotEqual(first.PopulationKey, changed.PopulationKey);
            Serve(City(2000));
            Assert.Equal(2000, adapter.Read(rlo).CitySeed);
            Assert.Equal(1, adapter.Bindings);

            // The contract version is read on every call; a refusal drops the binding and the next read binds afresh.
            CityApiHooks.Contract = 2;
            var refused = adapter.Read(rlo);
            Assert.Equal(RloReadStatus.Incompatible, refused.Status);
            Assert.Contains("contract 2 is not supported", refused.Message);
            Assert.Empty(refused.Identities);
            CityApiHooks.Contract = 1;
            Assert.Equal(RloReadStatus.Ready, adapter.Read(rlo).Status);
            Assert.Equal(2, adapter.Bindings);
            CityApiHooks.Throw = true;
            Assert.Contains("forbids", adapter.Read(rlo).Message);
            CityApiHooks.Throw = false;
            Assert.Equal(RloReadStatus.Ready, adapter.Read(rlo).Status);
            Assert.Equal(3, adapter.Bindings);

            // Another assembly object binds its own contract and refuses on its own terms.
            Assert.Equal(RloReadStatus.Incompatible, adapter.Read(StandIn.Build("0.99.0", contract: 2)).Status);
            Assert.Equal(RloReadStatus.Ready, adapter.Read(StandIn.Build("0.99.0")).Status);
            Assert.Equal(4, adapter.Bindings);
            Assert.Equal(RloReadStatus.Ready, adapter.Read(rlo).Status);
            Assert.Equal(5, adapter.Bindings);
            Assert.True(adapter.BindingMilliseconds >= 0);
        }

        [Fact]
        public void ANestedReadDuringTheCityApiCallLeavesTheOuterReadIntact()
        {
            var city = City(); Serve(city);
            var rlo = StandIn.Build("0.99.0");
            var expected = adapter.Read(rlo);
            RloReadResult? nested = null;
            CityApiHooks.DuringRead = () => { CityApiHooks.DuringRead = null; nested = adapter.Read(rlo); };
            var outer = adapter.Read(rlo);
            Assert.Equal(RloReadStatus.Ready, outer.Status);
            Assert.Equal(400, outer.Identities.Count);
            Assert.Equal(expected.PopulationKey, outer.PopulationKey);
            Assert.NotNull(nested);
            Assert.Equal(expected.PopulationKey, nested!.PopulationKey);
            // The nested read bound its own buffers; the outer binding stays cached.
            Assert.Equal(2, adapter.Bindings);
            Assert.Equal(expected.PopulationKey, adapter.Read(rlo).PopulationKey);
            Assert.Equal(2, adapter.Bindings);
        }

        private static string Describe(CitizenIdentity c) =>
            string.Join("|", c.Id, c.Name, c.District, c.Age, c.Female, c.Persona, c.AppearanceSeed);
    }

    /// <summary>What the emitted stand-in CityApi answers. Public so the emitted assembly can call it.</summary>
    public static class CityApiHooks
    {
        public sealed record Citizen(int Index, string? First, string? Last, string? District, int Age, bool Female, string? Persona, int AppearanceSeed);

        public static int Seed, CitizensStatus, ReservedStatus, BodiesStatus, LanesStatus, Contract = 1;
        public static bool Throw, ShortLimits;
        public static Action? DuringRead;
        public static List<Citizen> Citizens = new List<Citizen>();
        public static List<int> Reserved = new List<int>();
        public static List<object> Pedestrians = new List<object>(), Characters = new List<object>();
        public static List<(object Lane, int Original, int Current)> Lanes = new List<(object, int, int)>();

        public static void Reset()
        {
            Seed = CitizensStatus = ReservedStatus = BodiesStatus = LanesStatus = 0;
            Contract = 1; Throw = ShortLimits = false; DuringRead = null;
            Citizens = new List<Citizen>(); Reserved = new List<int>();
            Pedestrians = new List<object>(); Characters = new List<object>(); Lanes = new List<(object, int, int)>();
        }

        public static int ReadCitizens(object into, ref int citySeed)
        {
            if (Throw) throw new InvalidOperationException("The stand-in API threw.");
            var list = (IList)into;
            list.Clear();
            citySeed = Seed;
            if (CitizensStatus != 0) return CitizensStatus;
            Type type = into.GetType().GetGenericArguments()[0];
            foreach (var c in Citizens)
            {
                object record = Activator.CreateInstance(type)!;
                foreach (var p in typeof(Citizen).GetProperties()) if (p.Name != "EqualityContract") type.GetField(p.Name)!.SetValue(record, p.GetValue(c));
                list.Add(record);
            }
            DuringRead?.Invoke();
            return 0;
        }

        public static int ReadReservedCitizens(List<int> indexes)
        { indexes.Clear(); if (ReservedStatus == 0) indexes.AddRange(Reserved); return ReservedStatus; }

        public static int ReadReservedBodies(List<object> pedestrians, List<object> characters)
        {
            pedestrians.Clear(); characters.Clear();
            if (BodiesStatus == 0) { pedestrians.AddRange(Pedestrians); characters.AddRange(Characters); }
            return BodiesStatus;
        }

        public static int ReadSlowedLanes(List<object> lanes, List<int> originalLimits, List<int> currentLimits)
        {
            lanes.Clear(); originalLimits.Clear(); currentLimits.Clear();
            if (LanesStatus != 0) return LanesStatus;
            foreach (var (lane, original, current) in Lanes) { lanes.Add(lane); originalLimits.Add(original); if (!ShortLimits) currentLimits.Add(current); }
            return 0;
        }
    }

    /// <summary>Emits an assembly named RealLifeOverhaul with a CityApi of the contract's shape, answering from CityApiHooks.
    /// <c>fault</c> breaks one part of the shape.</summary>
    internal static class StandIn
    {
        private const string Space = "RealLifeOverhaul.ModApi.";

        /// <summary><c>liveContract</c> answers ContractVersion from <see cref="CityApiHooks.Contract"/> on every call.</summary>
        public static Assembly Build(string version, int contract = 1, string fault = "", bool liveContract = false)
        {
            var name = new AssemblyName("RealLifeOverhaul") { Version = Version.TryParse(version, out var v) ? v : new Version(0, 0) };
            var assembly = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
            assembly.SetCustomAttribute(new CustomAttributeBuilder(typeof(AssemblyInformationalVersionAttribute).GetConstructor(new[] { typeof(string) })!, new object[] { version }));
            var module = assembly.DefineDynamicModule("RealLifeOverhaul");
            if (fault == "CityApi") { module.DefineType("RealLifeOverhaul.Elsewhere", TypeAttributes.Public).CreateType(); return assembly; }

            var status = module.DefineEnum(Space + "CityApiStatus", TypeAttributes.Public, typeof(int));
            foreach (var (label, value) in new[] { ("Ready", 0), ("Off", 1), ("NotYet", 2), ("Refreshing", 3), ("Shared", 4), ("Failed", 5) })
                if (!(fault == "Shared" && label == "Shared")) status.DefineLiteral(label, value);
            Type statusType = status.CreateType()!;

            var citizen = module.DefineType(Space + "CityApiCitizen", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class);
            citizen.DefineDefaultConstructor(MethodAttributes.Public);
            foreach (var (field, type) in new[] { ("Index", typeof(int)), ("First", typeof(string)), ("Last", typeof(string)), ("District", typeof(string)),
                ("Age", typeof(int)), ("Female", typeof(bool)), ("Persona", fault == "persona int" ? typeof(int) : typeof(string)), ("AppearanceSeed", typeof(int)) })
                citizen.DefineField(field, type, FieldAttributes.Public | FieldAttributes.InitOnly);
            Type citizenType = citizen.CreateType()!;

            var api = module.DefineType(Space + "CityApi", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.Class);
            var getter = api.DefineMethod("get_ContractVersion", MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.HideBySig, typeof(int), Type.EmptyTypes);
            var il = getter.GetILGenerator();
            if (liveContract) il.Emit(OpCodes.Ldsfld, typeof(CityApiHooks).GetField(nameof(CityApiHooks.Contract))!);
            else il.Emit(OpCodes.Ldc_I4, contract);
            il.Emit(OpCodes.Ret);
            api.DefineProperty("ContractVersion", PropertyAttributes.None, typeof(int), Type.EmptyTypes).SetGetMethod(getter);

            var citizenList = typeof(List<>).MakeGenericType(citizenType);
            var read = api.DefineMethod("ReadCitizens", MethodAttributes.Public | MethodAttributes.Static, statusType, new[] { citizenList, typeof(int).MakeByRefType() });
            read.DefineParameter(1, ParameterAttributes.None, "into");
            read.DefineParameter(2, fault == "out" ? ParameterAttributes.None : ParameterAttributes.Out, "citySeed");
            Forward(read, nameof(CityApiHooks.ReadCitizens), 2);
            Forward(api.DefineMethod("ReadReservedCitizens", MethodAttributes.Public | MethodAttributes.Static, statusType, new[] { typeof(List<int>) }),
                nameof(CityApiHooks.ReadReservedCitizens), 1);
            Forward(api.DefineMethod("ReadReservedBodies", MethodAttributes.Public | MethodAttributes.Static, statusType, new[] { typeof(List<object>), typeof(List<object>) }),
                nameof(CityApiHooks.ReadReservedBodies), 2);
            if (fault != "ReadSlowedLanes")
                Forward(api.DefineMethod("ReadSlowedLanes", MethodAttributes.Public | MethodAttributes.Static, statusType, new[] { typeof(List<object>), typeof(List<int>), typeof(List<int>) }),
                    nameof(CityApiHooks.ReadSlowedLanes), 3);
            api.CreateType();
            return assembly;
        }

        private static void Forward(MethodBuilder method, string hook, int arguments)
        {
            var il = method.GetILGenerator();
            for (short i = 0; i < arguments; i++) il.Emit(OpCodes.Ldarg, i);
            il.Emit(OpCodes.Call, typeof(CityApiHooks).GetMethod(hook)!);
            il.Emit(OpCodes.Ret);
        }
    }
}
