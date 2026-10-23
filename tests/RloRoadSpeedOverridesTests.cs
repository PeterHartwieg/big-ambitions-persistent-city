using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using PersistentCity.Compatibility;
using Xunit;

namespace PersistentCity.Tests;

public sealed class RloRoadSpeedOverridesTests
{
    [Theory]
    [InlineData(50, 35)]
    [InlineData(30, 20)]
    [InlineData(21, 20)]
    [InlineData(20, 20)]
    [InlineData(1, 1)]
    [InlineData(int.MaxValue, int.MaxValue - 15)]
    public void CapturesReviewedEffectiveAndOriginalSpeedsWithoutMutation(int original, int effective)
    {
        var fixture = new Contract();
        object point = fixture.Point(effective);
        object fault = fixture.Add(1, point, original);
        var snapshot = new RloRoadSpeedOverrides(fixture.Assembly).Capture();
        Assert.Single(snapshot);
        Assert.Equal(original, snapshot[point].OriginalKilometresPerHour);
        Assert.Equal(effective, snapshot[point].EffectiveKilometresPerHour);
        Assert.Equal(effective, fixture.Speed(point));
        Assert.Same(point, fixture.Lane.GetValue(fault));
        Assert.Equal(original, fixture.Limit.GetValue(fault));
    }

    [Fact]
    public void RestoredLaneAndUninitializedWorldOrDirectorHaveNoActiveOverride()
    {
        var fixture = new Contract(); var adapter = new RloRoadSpeedOverrides(fixture.Assembly);
        object point = fixture.Point(20), fault = fixture.Add(1, point, 30);
        Assert.Single(adapter.Capture());
        fixture.SetSpeed(point, 30); fixture.Lane.SetValue(fault, null);
        Assert.Empty(adapter.Capture()); Assert.Equal(30, fixture.Speed(point));
        fixture.WorldField.SetValue(fixture.Director, null);
        Assert.Empty(adapter.Capture());
        fixture.InstanceField.SetValue(null, null);
        Assert.Empty(adapter.Capture());
    }

    [Fact]
    public void SnapshotsAreDetachedReadOnlyAndUseReferenceIdentityEvenWhenNativeEqualityCollides()
    {
        var fixture = new Contract(); var adapter = new RloRoadSpeedOverrides(fixture.Assembly);
        object first = fixture.Point(20), second = fixture.Point(35), unrelated = fixture.Point(20);
        Assert.True(first.Equals(second)); // Deliberately adversarial native equality implementation.
        object fault = fixture.Add(1, first, 30); fixture.Add(2, second, 50);
        var before = adapter.Capture();
        Assert.Equal(2, before.Count); Assert.False(before.ContainsKey(unrelated));
        Assert.Equal(30, before[first].OriginalKilometresPerHour);
        Assert.Equal(50, before[second].OriginalKilometresPerHour);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<object, RloRoadSpeedOverride>)before).Clear());
        fixture.SetSpeed(first, 30); fixture.Lane.SetValue(fault, null); fixture.Faults.Remove(2);
        Assert.Empty(adapter.Capture());
        Assert.Equal(2, before.Count);
        Assert.Equal(20, before[first].EffectiveKilometresPerHour);
        Assert.Equal(35, before[second].EffectiveKilometresPerHour);
    }

    [Theory]
    [InlineData(30, 30)]
    [InlineData(30, 19)]
    [InlineData(30, 21)]
    [InlineData(0, 0)]
    [InlineData(-5, -5)]
    public void UnexpectedObservedSpeedOrInvalidOriginalFailsClosed(int original, int observed)
    {
        var fixture = new Contract(); object point = fixture.Point(observed);
        fixture.Add(1, point, original);
        var adapter = new RloRoadSpeedOverrides(fixture.Assembly);
        Assert.Contains("SlowLimit", Assert.Throws<InvalidOperationException>(() => adapter.Capture()).Message);
        Assert.Equal(observed, fixture.Speed(point));
    }

    [Fact]
    public void DuplicateNativeLaneOwnershipIsRejectedRatherThanMerged()
    {
        var fixture = new Contract(); object point = fixture.Point(20);
        fixture.Add(1, point, 30); fixture.Add(2, point, 30);
        var adapter = new RloRoadSpeedOverrides(fixture.Assembly);
        Assert.Contains("same native lane reference", Assert.Throws<InvalidOperationException>(() => adapter.Capture()).Message);
        Assert.Equal(2, fixture.Faults.Count); Assert.Equal(20, fixture.Speed(point));
    }

    [Fact]
    public void NullFaultDictionaryOrEntryIsNotAnEmptyValidSnapshot()
    {
        var fixture = new Contract(); var adapter = new RloRoadSpeedOverrides(fixture.Assembly);
        fixture.Faults.Add(1, null);
        Assert.Throws<InvalidOperationException>(() => adapter.Capture());
        fixture.ShownField.SetValue(fixture.World, null);
        Assert.Throws<InvalidOperationException>(() => adapter.Capture());
    }

    [Theory]
    [InlineData("0.40.5", "RealLifeOverhaul", 4)]
    [InlineData("", "RealLifeOverhaul", 4)]
    [InlineData("0.40.4+unreviewed", "RealLifeOverhaul", 4)]
    [InlineData("0.40.4", "DifferentMod", 4)]
    [InlineData("0.40.4", "RealLifeOverhaul", 5)]
    public void RejectsUnreviewedAssemblyIdentityOrInformationVersion(string version, string name, int patch)
    {
        var fixture = new Contract(version: version, name: name, patch: patch);
        Assert.Throws<InvalidOperationException>(() => new RloRoadSpeedOverrides(fixture.Assembly));
    }

    [Theory]
    [InlineData("0.40.4", false)]
    [InlineData("0.40.4.0", false)]
    [InlineData("0.40.4+46b3b8835e113ec3807344a9b7899439684004ad", false)]
    [InlineData("0.41.1", true)]
    [InlineData("0.41.1.0", true)]
    [InlineData("0.41.1+dbd15c0958770219c0f4d969a8b2ac28593014d2", true)]
    public void AcceptsOnlyExistingAdaptersReviewedVersionForms(string version, bool latest)
    {
        var fixture = new Contract(version: version, latest: latest);
        var adapter = new RloRoadSpeedOverrides(fixture.Assembly);
        Assert.Empty(adapter.Capture());
        object point = fixture.Point(35); fixture.Add(1, point, 50);
        var snapshot = adapter.Capture();
        Assert.Equal(50, snapshot[point].OriginalKilometresPerHour);
        Assert.Equal(35, snapshot[point].EffectiveKilometresPerHour);
    }

    [Theory]
    [InlineData("laneType")]
    [InlineData("laneLimitType")]
    [InlineData("maxSpeedType")]
    [InlineData("worldFieldType")]
    [InlineData("dictionaryType")]
    [InlineData("dictionaryMutable")]
    [InlineData("instanceType")]
    [InlineData("lanePrivate")]
    public void InvalidContractsAreRejectedEvenWithNoCurrentDirector(string shape)
    {
        var fixture = new Contract(shape: shape);
        fixture.InstanceField.SetValue(null, null);
        var error = Assert.Throws<InvalidOperationException>(() => new RloRoadSpeedOverrides(fixture.Assembly));
        Assert.Contains("RLO road speed overrides:", error.Message);
    }

    private sealed class Contract
    {
        public Assembly Assembly { get; }
        public object Director { get; }
        public object World { get; }
        public IDictionary Faults { get; }
        public FieldInfo InstanceField { get; }
        public FieldInfo WorldField { get; }
        public FieldInfo ShownField { get; }
        public FieldInfo Lane { get; }
        public FieldInfo Limit { get; }
        private readonly Type pointType, shownType;
        private readonly FieldInfo speed;

        public Contract(string shape = "", string version = "0.40.4", string name = "RealLifeOverhaul", int patch = 4, bool latest = false)
        {
            var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name) { Version = latest ? new Version(0, 41, 1, 0) : new Version(0, 40, patch, 0) }, AssemblyBuilderAccess.Run);
            assembly.SetCustomAttribute(new CustomAttributeBuilder(typeof(AssemblyInformationalVersionAttribute).GetConstructor(new[] { typeof(string) })!, new object[] { version }));
            Assembly = assembly;
            var module = assembly.DefineDynamicModule("ReviewedFaultContract");
            var point = module.DefineType(shape == "laneType" ? "Other.Waypoint" : "GleyTrafficSystem.Waypoint", TypeAttributes.Public);
            point.DefineField("maxSpeed", shape == "maxSpeedType" ? typeof(float) : typeof(int), FieldAttributes.Public);
            var equals = point.DefineMethod("Equals", MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig, typeof(bool), new[] { typeof(object) });
            var il = equals.GetILGenerator(); il.Emit(OpCodes.Ldc_I4_1); il.Emit(OpCodes.Ret);
            var hash = point.DefineMethod("GetHashCode", MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig, typeof(int), Type.EmptyTypes);
            il = hash.GetILGenerator(); il.Emit(OpCodes.Ldc_I4_0); il.Emit(OpCodes.Ret);
            pointType = point.CreateType()!; speed = pointType.GetField("maxSpeed")!;
            var world = module.DefineType("RealLifeOverhaul.Faults.FaultWorld", TypeAttributes.NotPublic | TypeAttributes.Sealed);
            var entry = world.DefineNestedType("Shown", TypeAttributes.NestedPrivate | TypeAttributes.Sealed);
            entry.DefineField("Lane", pointType, shape == "lanePrivate" ? FieldAttributes.Private : FieldAttributes.Public);
            entry.DefineField("LaneLimit", shape == "laneLimitType" ? typeof(long) : typeof(int), FieldAttributes.Public);
            Type dictionaryShape = shape == "dictionaryType" ? typeof(Hashtable) : typeof(Dictionary<,>).MakeGenericType(typeof(int), entry);
            world.DefineField("_shown", dictionaryShape, FieldAttributes.Private | (shape == "dictionaryMutable" ? 0 : FieldAttributes.InitOnly));
            Type worldType = world.CreateType()!;
            shownType = entry.CreateType()!;
            Type dictionary = shape == "dictionaryType" ? typeof(Hashtable) : typeof(Dictionary<,>).MakeGenericType(typeof(int), shownType);
            var director = module.DefineType("RealLifeOverhaul.Faults.FaultDirector", TypeAttributes.Public | TypeAttributes.Sealed);
            director.DefineField("_world", shape == "worldFieldType" ? typeof(object) : worldType, FieldAttributes.Private);
            Type instanceType = shape == "instanceType" ? typeof(object) : director;
            var instanceField = director.DefineField("_Instance", instanceType, FieldAttributes.Public | FieldAttributes.Static);
            var getter = director.DefineMethod("get_Instance", MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.HideBySig, instanceType, Type.EmptyTypes);
            il = getter.GetILGenerator(); il.Emit(OpCodes.Ldsfld, instanceField); il.Emit(OpCodes.Ret);
            var property = director.DefineProperty("Instance", PropertyAttributes.None, instanceType, Type.EmptyTypes); property.SetGetMethod(getter);
            Type directorType = director.CreateType()!;
            Director = Activator.CreateInstance(directorType)!; World = Activator.CreateInstance(worldType, true)!;
            InstanceField = directorType.GetField("_Instance")!;
            WorldField = directorType.GetField("_world", BindingFlags.Instance | BindingFlags.NonPublic)!;
            ShownField = worldType.GetField("_shown", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Lane = shownType.GetField("Lane", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;
            Limit = shownType.GetField("LaneLimit")!;
            Faults = (IDictionary)Activator.CreateInstance(dictionary)!;
            ShownField.SetValue(World, Faults); WorldField.SetValue(Director, World); InstanceField.SetValue(null, Director);
        }
        public object Point(int value)
        {
            object point = Activator.CreateInstance(pointType)!;
            if (speed.FieldType == typeof(int)) speed.SetValue(point, value);
            else speed.SetValue(point, (float)value);
            return point;
        }
        public object Add(int id, object? point, int original)
        {
            object entry = Activator.CreateInstance(shownType, true)!;
            Lane.SetValue(entry, point); Limit.SetValue(entry, original); Faults.Add(id, entry); return entry;
        }
        public int Speed(object point) => (int)speed.GetValue(point)!;
        public void SetSpeed(object point, int value) => speed.SetValue(point, value);
    }
}
