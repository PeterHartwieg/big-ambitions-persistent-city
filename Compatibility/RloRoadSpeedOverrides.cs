using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace PersistentCity.Compatibility
{
    public readonly struct RloRoadSpeedOverride
    {
        public RloRoadSpeedOverride(int originalKilometresPerHour, int effectiveKilometresPerHour)
        { OriginalKilometresPerHour = originalKilometresPerHour; EffectiveKilometresPerHour = effectiveKilometresPerHour; }
        public int OriginalKilometresPerHour { get; }
        public int EffectiveKilometresPerHour { get; }
    }

    /// <summary>Read-only contract for RLO's pothole speed overlay: RLO's CityApi when it has one, otherwise the reviewed
    /// reflection read. Capture on the game's main thread.</summary>
    public sealed class RloRoadSpeedOverrides
    {
        private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        private readonly Type directorType = null!, worldType = null!, shownType = null!, waypointType = null!;
        private readonly PropertyInfo instance = null!;
        private readonly FieldInfo world = null!, shown = null!, lane = null!, laneLimit = null!, maxSpeed = null!;
        private readonly RloBridge? bridge;
        private readonly List<object> lanes = new List<object>();
        private readonly List<int> originals = new List<int>(), currents = new List<int>();

        public RloRoadSpeedOverrides(Assembly rlo)
        {
            try
            {
                if (rlo == null || rlo.GetName().Name != "RealLifeOverhaul") throw Invalid("requires RealLifeOverhaul");
                bridge = RloBridge.Find(rlo);
                if (bridge != null) return;
                if (!RloIdentityAdapter.IsReviewedAssembly(rlo))
                    throw Invalid("requires reviewed RealLifeOverhaul " + RloIdentityAdapter.ReviewedVersions + " or CityApi contract " + RloBridge.SupportedContract);
                directorType = rlo.GetType("RealLifeOverhaul.Faults.FaultDirector", true)!;
                worldType = rlo.GetType("RealLifeOverhaul.Faults.FaultWorld", true)!;
                shownType = worldType.GetNestedType("Shown", BindingFlags.NonPublic) ?? throw Invalid("missing FaultWorld.Shown");
                if (!directorType.IsClass || !directorType.IsPublic || !worldType.IsClass || worldType.IsPublic
                    || !worldType.IsSealed || !shownType.IsNestedPrivate || !shownType.IsSealed)
                    throw Invalid("changed FaultDirector/FaultWorld/Shown type contract");
                instance = directorType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    ?? throw Invalid("missing public static FaultDirector.Instance");
                if (instance.PropertyType != directorType || instance.GetMethod == null || !instance.GetMethod.IsPublic
                    || !instance.GetMethod.IsStatic || instance.GetIndexParameters().Length != 0)
                    throw Invalid("changed FaultDirector.Instance property");
                world = Field(directorType, "_world", worldType, false);
                shown = Field(worldType, "_shown", typeof(Dictionary<,>).MakeGenericType(typeof(int), shownType), false);
                if (!shown.IsInitOnly) throw Invalid("FaultWorld._shown is no longer readonly");
                lane = shownType.GetField("Lane", Members) ?? throw Invalid("missing Shown.Lane");
                waypointType = lane.FieldType;
                if (!waypointType.IsClass || waypointType.FullName != "GleyTrafficSystem.Waypoint" || !lane.IsPublic || lane.IsStatic)
                    throw Invalid("Shown.Lane must be the reviewed GleyTrafficSystem.Waypoint field");
                laneLimit = Field(shownType, "LaneLimit", typeof(int), true);
                maxSpeed = Field(waypointType, "maxSpeed", typeof(int), true);
            }
            catch (Exception error) when (!(error is InvalidOperationException))
            { throw Invalid("could not bind the reviewed fault speed contract", error); }
        }

        public IReadOnlyDictionary<object, RloRoadSpeedOverride> Capture()
        {
            if (bridge != null) return CaptureThroughApi(bridge);
            try
            {
                var result = new Dictionary<object, RloRoadSpeedOverride>(References.Instance);
                object? director = instance.GetValue(null);
                if (director != null)
                {
                    if (director.GetType() != directorType) throw Invalid("unexpected FaultDirector instance type");
                    object? activeWorld = world.GetValue(director);
                    if (activeWorld != null)
                    {
                        if (activeWorld.GetType() != worldType) throw Invalid("unexpected FaultWorld instance type");
                        if (!(shown.GetValue(activeWorld) is IDictionary faults)) throw Invalid("FaultWorld._shown is null or is not the reviewed dictionary");
                        foreach (DictionaryEntry fault in faults)
                        {
                            if (!(fault.Key is int) || fault.Value == null || fault.Value.GetType() != shownType)
                                throw Invalid("unexpected entry in FaultWorld._shown");
                            object? point = lane.GetValue(fault.Value);
                            if (point == null) continue; // Hide restores the native speed and clears Lane.
                            if (point.GetType() != waypointType) throw Invalid("unexpected Shown.Lane instance type");
                            int original = (int)laneLimit.GetValue(fault.Value)!;
                            int observed = (int)maxSpeed.GetValue(point)!;
                            // Reviewed FaultRules.SlowLimit is identical in both accepted releases.
                            int expected = original <= 20 ? original : Math.Max(20, original - 15);
                            if (original <= 0 || observed != expected)
                                throw Invalid("lane speed does not match reviewed SlowLimit: original=" + original + ", observed=" + observed);
                            if (result.ContainsKey(point)) throw Invalid("multiple faults claim the same native lane reference");
                            result.Add(point, new RloRoadSpeedOverride(original, observed));
                        }
                    }
                }
                return new ReadOnlyDictionary<object, RloRoadSpeedOverride>(result);
            }
            catch (Exception error) when (!(error is InvalidOperationException))
            { throw Invalid("could not capture the reviewed fault speed snapshot", error); }
        }

        // RLO owns the slowdown rule here; Persistent City checks only that each lane is listed once with positive limits.
        // NativeRoadSpeeds then checks each lane's live limit against the current one. Off means nothing is slowed. Any
        // other answer but Ready is refused rather than read as no slowdowns: a slowed lane taken as unslowed would
        // record its reduced limit as the road's own.
        private IReadOnlyDictionary<object, RloRoadSpeedOverride> CaptureThroughApi(RloBridge api)
        {
            try
            {
                var result = new Dictionary<object, RloRoadSpeedOverride>(References.Instance);
                RloBridge.Status status = api.ReadSlowedLanes(lanes, originals, currents);
                if (status == RloBridge.Status.Off) return new ReadOnlyDictionary<object, RloRoadSpeedOverride>(result);
                if (status != RloBridge.Status.Ready) throw Invalid("ReadSlowedLanes: " + RloBridge.Describe(status));
                if (originals.Count != lanes.Count || currents.Count != lanes.Count) throw Invalid("ReadSlowedLanes returned lists of different lengths");
                for (int i = 0; i < lanes.Count; i++)
                {
                    object? point = lanes[i];
                    if (point == null) throw Invalid("ReadSlowedLanes returned a null lane");
                    if (originals[i] <= 0 || currents[i] <= 0) throw Invalid("lane limits must be positive: original=" + originals[i] + ", current=" + currents[i]);
                    if (result.ContainsKey(point)) throw Invalid("multiple faults claim the same native lane reference");
                    result.Add(point, new RloRoadSpeedOverride(originals[i], currents[i]));
                }
                return new ReadOnlyDictionary<object, RloRoadSpeedOverride>(result);
            }
            finally { lanes.Clear(); originals.Clear(); currents.Clear(); }
        }

        private static FieldInfo Field(Type type, string name, Type expected, bool isPublic)
        {
            var field = type.GetField(name, Members);
            if (field == null || field.FieldType != expected || field.IsStatic || field.IsPublic != isPublic || (!isPublic && !field.IsPrivate))
                throw Invalid("missing or changed field " + type.FullName + "." + name);
            return field;
        }
        private static InvalidOperationException Invalid(string message, Exception? inner = null) =>
            new InvalidOperationException("RLO road speed overrides: " + message + ".", inner);
        private sealed class References : IEqualityComparer<object>
        {
            public static readonly References Instance = new References();
            public new bool Equals(object? a, object? b) => ReferenceEquals(a, b);
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }
    }
}
