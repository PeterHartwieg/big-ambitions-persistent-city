using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using PersistentCity.Compatibility;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class RloPhysicalReservationsTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ActiveSceneReservesFullOriginalIdentityTupleRatherThanNamesOrGeneratedDonors(bool latest)
    {
        var fixture = new Contract(latest);
        var source = new CitizenIdentity { Id = "c17", Name = "Ada Resident", Age = 35, Female = true, AppearanceSeed = 871 };
        fixture.Scene(source);
        var donor = Population.CloneIdentity(source); donor.Id = "pc:1";
        var sameName = Population.CloneIdentity(source); sameName.Id = "c18"; sameName.AppearanceSeed++;
        var guard = new RloPhysicalReservations(fixture.Assembly, new[] { source, donor, sameName });
        Assert.True(guard.Ready, guard.LastError);
        Assert.True(guard.IsReserved(source.Id));
        Assert.False(guard.IsReserved(donor.Id)); Assert.False(guard.IsReserved(sameName.Id));
        Assert.True(guard.IsReservedNativePedestrian(fixture.NativePedestrian, fixture.Character));
        ((bool[])fixture.Cast.GetType().GetField("_out")!.GetValue(fixture.Cast)!)[0] = false;
        guard.Update();
        Assert.Empty(guard.ReservedCitizenIds);
        Assert.False(guard.IsReserved(source.Id));
        // Reading reservations has not changed the RLO cast's look or identity.
        Assert.Equal("Ada Resident", fixture.Person.GetType().GetField("Name")!.GetValue(fixture.Person));
        Assert.Equal(871, fixture.Person.GetType().GetField("Seed")!.GetValue(fixture.Person));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NativeCasualtyLeaseIsProtectedUntilRloReturnsIt(bool latest)
    {
        var fixture = new Contract(latest); fixture.BorrowNative();
        var guard = new RloPhysicalReservations(fixture.Assembly, new[] { new CitizenIdentity { Id = "c1" } });
        Assert.True(guard.Ready, guard.LastError);
        Assert.True(guard.IsReservedNativePedestrian(fixture.NativePedestrian, null));
        Assert.False(guard.IsReservedNativePedestrian(new object(), null));
        fixture.Takeover.GetType().GetField("_pedestrian")!.SetValue(fixture.Takeover, null);
        guard.Update();
        Assert.False(guard.IsReservedNativePedestrian(fixture.NativePedestrian, null));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void InvalidLiveCastShapeFailsClosedWithoutTouchingBorrowedBodies(bool latest)
    {
        var fixture = new Contract(latest); fixture.Scene(new CitizenIdentity { Id = "c1", Name = "Resident", Age = 30, AppearanceSeed = 15 });
        var guard = new RloPhysicalReservations(fixture.Assembly, new[] { new CitizenIdentity { Id = "c1" } });
        Assert.True(guard.Ready, guard.LastError);
        fixture.Cast.GetType().GetField("_out")!.SetValue(fixture.Cast, new bool[3]);
        guard.Update();
        Assert.False(guard.Ready); Assert.NotEmpty(guard.LastError);
        Assert.True(guard.IsReserved("c1"));
        Assert.True(guard.IsReservedNativePedestrian(new object(), null));
    }

    [Fact]
    public void ValidReflectionShapeOnAnUnreviewedReleaseWithoutCityApiIsRefused()
    {
        var fixture = new Contract(release: "0.46.0"); fixture.BorrowNative();
        var guard = new RloPhysicalReservations(fixture.Assembly, new[] { new CitizenIdentity { Id = "c1" } });
        Assert.False(guard.Ready);
        Assert.Contains("require reviewed RLO", guard.LastError);
        Assert.True(guard.IsReserved("c1"));
        Assert.True(guard.IsReservedNativePedestrian(new object(), null));
        Assert.True(new RloPhysicalReservations(new Contract(release: "0.45.0").Assembly, new[] { new CitizenIdentity { Id = "c1" } }).Ready);
    }

    private sealed class Contract
    {
        public Assembly Assembly { get; }
        public object Cast { get; }
        public object Person { get; }
        public object Character { get; }
        public object NativePedestrian { get; }
        public object Takeover { get; }
        private readonly Type socialDirector, incident;
        private readonly object casualty;
        private readonly ModuleBuilder module;

        public Contract(bool latest = false, string? release = null)
        {
            release ??= latest ? "0.41.1" : "0.40.4";
            var name = new AssemblyName("RealLifeOverhaul") { Version = new Version(release + ".0") };
            var assembly = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
            assembly.SetCustomAttribute(new CustomAttributeBuilder(typeof(AssemblyInformationalVersionAttribute).GetConstructor(new[] { typeof(string) })!, new object[] { release }));
            Assembly = assembly; module = assembly.DefineDynamicModule("ReviewedShape");
            Type baseHuman = Class("BaseHuman", Array.Empty<(string,Type)>());
            Type character = Class("ThirdPersonCharacter", Array.Empty<(string,Type)>(), parent: baseHuman);
            Type pedestrian = Class("Streets.Pedestrians.Pedestrian", Array.Empty<(string,Type)>());
            Type cityPerson = Class("RealLifeOverhaul.CityPerson", Array.Empty<(string,Type)>(), properties: new[] { ("Character", character, false) });
            Type socialPerson = Class("RealLifeOverhaul.SocialPerson", new[] { ("Index", typeof(int)), ("Name", typeof(string)), ("Age", typeof(int)), ("Female", typeof(bool)), ("Seed", typeof(int)) });
            Type cast = Class("RealLifeOverhaul.SocialCast", new[] { ("_wears", typeof(int[])), ("_out", typeof(bool[])), ("_people", cityPerson.MakeArrayType()) });
            socialDirector = Self("RealLifeOverhaul.SocialDirector", new[] { ("Cast", cast, false) });
            Type state = Class("RealLifeOverhaul.SocialState", Array.Empty<(string,Type)>(), staticFields: new[] { ("People", socialPerson.MakeArrayType()) });
            Type takeover = Class("RealLifeOverhaul.PedestrianTakeover", new[] { ("_pedestrian", pedestrian) });
            Type casualtyType = Class("RealLifeOverhaul.Casualty", new[] { ("_pedestrian", takeover) });
            incident = Self("RealLifeOverhaul.PedestrianIncidents", Array.Empty<(string,Type,bool)>(), new[] { ("_casualties", casualtyType.MakeArrayType()) });
            Type ragdoll = Class("RealLifeOverhaul.Ragdoll", Array.Empty<(string,Type)>(), properties: new[] { ("Active", typeof(bool), false), ("Character", baseHuman, false) });
            Type system = Self("RealLifeOverhaul.RagdollSystem", Array.Empty<(string,Type,bool)>(), new[] { ("_ragdolls", ragdoll.MakeArrayType()) });
            _ = system;
            Character = Activator.CreateInstance(character)!; NativePedestrian = Activator.CreateInstance(pedestrian)!;
            Person = Activator.CreateInstance(socialPerson)!; Cast = Activator.CreateInstance(cast)!;
            var social = Activator.CreateInstance(socialDirector)!; socialDirector.GetField("_Cast")!.SetValue(social, Cast);
            socialDirector.GetField("_Instance")!.SetValue(null, social);
            cast.GetField("_wears")!.SetValue(Cast, new[] { -1,-1 }); cast.GetField("_out")!.SetValue(Cast, new bool[2]);
            cast.GetField("_people")!.SetValue(Cast, Array.CreateInstance(cityPerson, 2));
            var people = Array.CreateInstance(socialPerson, 1); people.SetValue(Person, 0); state.GetField("People")!.SetValue(null, people);
            Takeover = Activator.CreateInstance(takeover)!; casualty = Activator.CreateInstance(casualtyType)!;
        }
        public void Scene(CitizenIdentity identity)
        {
            var person = Person.GetType(); person.GetField("Name")!.SetValue(Person, identity.Name); person.GetField("Age")!.SetValue(Person, identity.Age);
            person.GetField("Female")!.SetValue(Person, identity.Female); person.GetField("Seed")!.SetValue(Person, identity.AppearanceSeed);
            var cast = Cast.GetType(); ((int[])cast.GetField("_wears")!.GetValue(Cast)!)[0] = 0; ((bool[])cast.GetField("_out")!.GetValue(Cast)!)[0] = true;
            var bodies = (Array)cast.GetField("_people")!.GetValue(Cast)!; object body = Activator.CreateInstance(bodies.GetType().GetElementType()!)!;
            body.GetType().GetField("_Character")!.SetValue(body, Character); bodies.SetValue(body, 0);
        }
        public void BorrowNative()
        {
            Takeover.GetType().GetField("_pedestrian")!.SetValue(Takeover, NativePedestrian);
            casualty.GetType().GetField("_pedestrian")!.SetValue(casualty, Takeover);
            object incidents = Activator.CreateInstance(incident)!;
            var list = Array.CreateInstance(casualty.GetType(), 1); list.SetValue(casualty, 0); incident.GetField("_casualties")!.SetValue(incidents, list);
            incident.GetField("_Instance")!.SetValue(null, incidents);
        }
        private Type Class(string name, (string,Type)[] fields, (string,Type,bool)[]? properties = null, (string,Type)[]? staticFields = null, Type? parent = null)
        {
            var type = module.DefineType(name, TypeAttributes.Public, parent);
            foreach (var (field, kind) in fields) type.DefineField(field, kind, FieldAttributes.Public);
            foreach (var (field, kind) in staticFields ?? Array.Empty<(string,Type)>()) type.DefineField(field, kind, FieldAttributes.Public | FieldAttributes.Static);
            foreach (var (property, kind, isStatic) in properties ?? Array.Empty<(string,Type,bool)>()) Property(type, property, kind, isStatic);
            return type.CreateType()!;
        }
        private Type Self(string name, (string,Type,bool)[] properties, (string,Type)[]? fields = null)
        {
            var type = module.DefineType(name, TypeAttributes.Public);
            foreach (var (field, kind) in fields ?? Array.Empty<(string,Type)>()) type.DefineField(field, kind, FieldAttributes.Public);
            Property(type, "Instance", type, true);
            foreach (var (property, kind, isStatic) in properties) Property(type, property, kind, isStatic);
            return type.CreateType()!;
        }
        private static void Property(TypeBuilder type, string name, Type kind, bool isStatic)
        {
            var field = type.DefineField("_" + name, kind, FieldAttributes.Public | (isStatic ? FieldAttributes.Static : 0));
            var getter = type.DefineMethod("get_" + name, MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig | (isStatic ? MethodAttributes.Static : 0), kind, Type.EmptyTypes);
            var il = getter.GetILGenerator(); if (!isStatic) il.Emit(OpCodes.Ldarg_0); il.Emit(isStatic ? OpCodes.Ldsfld : OpCodes.Ldfld, field); il.Emit(OpCodes.Ret);
            var property = type.DefineProperty(name, PropertyAttributes.None, kind, Type.EmptyTypes); property.SetGetMethod(getter);
        }
    }
}
