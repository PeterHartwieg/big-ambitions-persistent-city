using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using GleyTrafficSystem;
using UI;
using HarmonyLib;

namespace PersistentCity.Runtime
{
    /// <summary>One owner ID; all driving exceptions are restricted to this instance's valid leases.</summary>
    internal sealed class PhysicalTrafficHooks : IDisposable
    {
        private const string Owner = "hart.persistentcity.physicalvehicles.v1";
        private readonly Harmony harmony = new Harmony(Owner);
        private static PhysicalVehicles? current;
        private bool disposed;
        private bool ownsRegistration;
        private static readonly FieldInfo? IdleVehicles = AccessTools.Field(typeof(TrafficVehicles), "idleVehicles");
        private static readonly FieldInfo? CurrentSpeed = NativeGameSpeed.SpeedField;
        internal static bool CurrentFastForward => NativeGameSpeed.LegacyFastForward;
        internal static readonly FieldInfo? DrivingAiField = AccessTools.Field(typeof(TrafficManager), "drivingAI");
        internal static readonly MethodInfo? RemoveAction = AccessTools.Method(typeof(DrivingAI), "NewDriveActionArrived",
            new[] { typeof(int), typeof(SpecialDriveActionTypes), typeof(bool) });
        private static int selectorCalls;
        private static int bodyForceCalls;
        private static int bodyRotationCalls;
        public bool Ready { get; private set; }
        public string LastError { get; private set; } = "";
        public PhysicalTrafficHooks(PhysicalVehicles vehicles)
        {
            if (current != null || Harmony.HasAnyPatches(Owner))
            {
                LastError = "Another physical traffic hook owner is already registered.";
                UnityEngine.Debug.Log("[Persistent City] Physical traffic hooks: Not active: " + LastError);
                return;
            }
            current = vehicles; ownsRegistration = true;
            try
            {
                selectorCalls = 0;
                if (IdleVehicles == null || IdleVehicles.FieldType != typeof(List<VehicleComponent>)) throw new MissingFieldException("TrafficVehicles.idleVehicles");
                if (CurrentSpeed == null || CurrentSpeed.FieldType != typeof(GameSpeed)) throw new MissingFieldException("GameSpeedController.currentGameSpeed");
                var speedChange = AccessTools.Method(typeof(GameSpeedController), "ChangeTimeScale", new[] { typeof(TimeSpeed), typeof(float) });
                if (speedChange == null) throw new MissingMethodException("GameSpeedController.ChangeTimeScale(TimeSpeed,float)");
                harmony.Patch(speedChange, prefix: new HarmonyMethod(typeof(PhysicalTrafficHooks), nameof(SpeedChangingPrefix)));
                UnityEngine.Debug.Log("[Persistent City] Patch GameSpeedController.ChangeTimeScale: Active");
                if (DrivingAiField == null || DrivingAiField.FieldType != typeof(DrivingAI)
                    || RemoveAction == null)
                    throw new MissingMemberException("Native temporary-stop recovery contract.");
                var crash = AccessTools.Method(typeof(DrivingAI), "VehicleCrashHandler", new[] { typeof(int), typeof(int), typeof(bool) });
                if (crash == null) throw new MissingMethodException("DrivingAI.VehicleCrashHandler(int,int,bool)");
                harmony.Patch(crash, postfix: new HarmonyMethod(typeof(PhysicalTrafficHooks), nameof(CrashPostfix)));
                UnityEngine.Debug.Log("[Persistent City] Patch DrivingAI.VehicleCrashHandler temporary-stop expiry: Active");
                var add = AccessTools.Method(typeof(DensityManager), "AddVehicle", new[] { typeof(bool), typeof(int), typeof(int), typeof(UnityEngine.Vector3), typeof(UnityEngine.Vector3) });
                if (add == null) throw new MissingMethodException("DensityManager.AddVehicle");
                harmony.Patch(add, transpiler: new HarmonyMethod(typeof(PhysicalTrafficHooks), nameof(SelectTranspiler)));
                if (selectorCalls != 1) throw new InvalidOperationException("Unexpected native random traffic selector shape.");
                UnityEngine.Debug.Log("[Persistent City] Patch DensityManager.AddVehicle selector: Active");
                bodyForceCalls = 0; bodyRotationCalls = 0;
                var physics = AccessTools.Method(typeof(TrafficManager), "FixedUpdate", Type.EmptyTypes);
                if (physics == null) throw new MissingMethodException("TrafficManager.FixedUpdate");
                harmony.Patch(physics,
                    prefix: new HarmonyMethod(typeof(PhysicalTrafficHooks), nameof(FixedStepPrefix)),
                    postfix: new HarmonyMethod(typeof(PhysicalTrafficHooks), nameof(FixedStepPostfix)),
                    transpiler: new HarmonyMethod(typeof(PhysicalTrafficHooks), nameof(BodyForceTranspiler)),
                    finalizer: new HarmonyMethod(typeof(PhysicalTrafficHooks), nameof(FixedStepFinalizer)));
                if (bodyForceCalls != 1 || bodyRotationCalls != 1) throw new InvalidOperationException("Unexpected native body force/rotation application shape.");
                UnityEngine.Debug.Log("[Persistent City] Patch TrafficManager.FixedUpdate owned traffic force/rotation limit: Active");
                Patch(typeof(DrivingAI), "HasReachedOrPassedWaypoint", new[] { typeof(int), typeof(int) }, nameof(ReachedPrefix));
                Patch(typeof(DrivingAI), "WaypointRequested", new[] { typeof(int), typeof(VehicleTypes) }, nameof(RequestPrefix));
                Patch(typeof(AiCarRescueCheck), "DoUpdate", new[] { typeof(float) }, nameof(RescuePrefix));
                Patch(typeof(TrafficDespawner), "OnTriggerEnter", new[] { typeof(UnityEngine.Collider) }, nameof(DespawnerPrefix));
                Ready = true;
            }
            catch (Exception error)
            {
                LastError = error.GetType().Name + ": " + error.Message;
                if (ownsRegistration) { harmony.UnpatchAll(Owner); current = null; ownsRegistration = false; }
                Ready = false;
                UnityEngine.Debug.Log("[Persistent City] Physical traffic hooks: Not active (rolled back): " + LastError);
            }
        }
        private void Patch(Type type, string name, Type[] parameters, string prefix)
        {
            var method = AccessTools.Method(type, name, parameters);
            if (method == null) throw new MissingMethodException(type.FullName, name);
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(PhysicalTrafficHooks), prefix));
            UnityEngine.Debug.Log("[Persistent City] Patch " + type.Name + "." + name + ": Active");
        }
        private static IEnumerable<CodeInstruction> SelectTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var original = AccessTools.Method(typeof(TrafficVehicles), nameof(TrafficVehicles.GetIdleVehicleIndex), Type.EmptyTypes);
            var replacement = AccessTools.Method(typeof(PhysicalTrafficHooks), nameof(SelectIdle));
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(original)) { selectorCalls++; instruction.opcode = OpCodes.Call; instruction.operand = replacement; }
                yield return instruction;
            }
        }
        private static int SelectIdle(TrafficVehicles vehicles)
        {
            int selected = vehicles.GetIdleVehicleIndex(); // Preserve exactly one native random group draw.
            if (current == null || !current.Active || selected < 0) return selected;
            // GetVehicleType/GetIdleVehicle operate on idle-list indices, not stable pool indices.
            if (!(IdleVehicles?.GetValue(vehicles) is List<VehicleComponent> idle) || selected >= idle.Count) return -1;
            var candidate = idle[selected];
            if (candidate == null) return -1;
            bool needTaxi = current.TaxiNeedsReplenishment;
            // Taxi classification is memoized per frame; the ordinary-ambient verdict stays live.
            if (!current.IsTaxiThisFrame(candidate) && !PhysicalVehicles.RandomAmbient(candidate)) return selected;
            if (current.IsTaxiThisFrame(candidate) && needTaxi) return selected;
            // Native FindIndex chooses the first idle member of the drawn group. Ordinary members stay
            // idle, so skip those queue heads while retaining the same group draw (no retry/random draw).
            var group = candidate.vehicleGroupType;
            for (int i = 0; i < idle.Count; i++)
                if (idle[i] != null && idle[i].vehicleGroupType == group && !idle[i].gameObject.activeInHierarchy
                    && (current.IsTaxiThisFrame(idle[i]) ? needTaxi : !PhysicalVehicles.RandomAmbient(idle[i]))) return i;
            return -1;
        }
        private static IEnumerable<CodeInstruction> BodyForceTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var original = AccessTools.Method(typeof(UnityEngine.Rigidbody), nameof(UnityEngine.Rigidbody.AddForce),
                new[] { typeof(UnityEngine.Vector3), typeof(UnityEngine.ForceMode) }) ?? throw new MissingMethodException("Rigidbody.AddForce(Vector3,ForceMode)");
            var replacement = AccessTools.Method(typeof(PhysicalTrafficHooks), nameof(ApplyBodyForce)) ?? throw new MissingMethodException(nameof(ApplyBodyForce));
            var move = AccessTools.Method(typeof(UnityEngine.Rigidbody), nameof(UnityEngine.Rigidbody.MoveRotation), new[] { typeof(UnityEngine.Quaternion) })
                ?? throw new MissingMethodException("Rigidbody.MoveRotation(Quaternion)");
            var moveReplacement = AccessTools.Method(typeof(PhysicalTrafficHooks), nameof(ApplyBodyRotation)) ?? throw new MissingMethodException(nameof(ApplyBodyRotation));
            CodeInstruction? previous = null;
            List<CodeInstruction>? between = null;
            int seenForces = 0, seenRotations = 0;
            var history = new Queue<CodeInstruction>();
            CodeInstruction? forceIndex = null;
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(original))
                {
                    // Verified local IL loads ForceMode.VelocityChange immediately before this one call.
                    if (previous == null || previous.opcode != OpCodes.Ldc_I4_2 || (int)UnityEngine.ForceMode.VelocityChange != 2)
                        throw new InvalidOperationException("Native vehicle body force no longer uses the verified velocity-change mode.");
                    if (++seenForces != 1) throw new InvalidOperationException("Native traffic has multiple body force calls.");
                    forceIndex = VerifiedForceReceiver(history.ToArray());
                    if (forceIndex == null) throw new InvalidOperationException("Native body force receiver no longer uses the verified pool slot.");
                    bodyForceCalls = seenForces; instruction.opcode = OpCodes.Call; instruction.operand = replacement;
                    between = new List<CodeInstruction>();
                }
                else if (instruction.Calls(move))
                {
                    if (between == null || forceIndex == null || !VerifiedRotationArguments(between, forceIndex))
                        throw new InvalidOperationException("Native force/rotation commands no longer use the verified adjacent same-body local-yaw shape.");
                    bodyRotationCalls = ++seenRotations; instruction.opcode = OpCodes.Call; instruction.operand = moveReplacement;
                    between = null;
                    forceIndex = null;
                }
                else between?.Add(instruction);
                yield return instruction;
                previous = instruction;
                if (history.Count == 24) history.Dequeue();
                history.Enqueue(instruction);
            }
            if (between != null || seenForces != 1 || seenRotations != 1)
                throw new InvalidOperationException("Native body force has no unique paired rotation.");
        }
        private static CodeInstruction? VerifiedForceReceiver(CodeInstruction[] code)
        {
            // IL_0698..IL_06d8 has 24 force-argument instructions; the first four load its
            // receiver. Require that exact receiver and compare its pool-index load to rotation.
            var bodies = AccessTools.Field(typeof(TrafficManager), "vehicleRigidbody");
            if (code.Length != 24 || bodies?.FieldType != typeof(UnityEngine.Rigidbody[]) || code[0].opcode != OpCodes.Ldarg_0
                || code[1].opcode != OpCodes.Ldfld || !Equals(code[1].operand, bodies) || code[3].opcode != OpCodes.Ldelem_Ref) return null;
            foreach (var instruction in code)
                if (instruction.opcode.FlowControl == FlowControl.Branch || instruction.opcode.FlowControl == FlowControl.Cond_Branch
                    || instruction.opcode.FlowControl == FlowControl.Return || instruction.opcode.FlowControl == FlowControl.Throw) return null;
            return code[2];
        }
        private static bool VerifiedRotationArguments(List<CodeInstruction> code, CodeInstruction forceIndex)
        {
            // Local ExternalPlugins IL_06de..IL_0711 has exactly these 17 instructions between
            // AddForce and MoveRotation; no branches, callbacks or other force application intervenes.
            var bodies = AccessTools.Field(typeof(TrafficManager), "vehicleRigidbody");
            var angles = AccessTools.Field(typeof(TrafficManager), "vehicleRotationAngle");
            var getter = AccessTools.PropertyGetter(typeof(UnityEngine.Rigidbody), nameof(UnityEngine.Rigidbody.rotation));
            var euler = AccessTools.Method(typeof(UnityEngine.Quaternion), nameof(UnityEngine.Quaternion.Euler), new[] { typeof(float), typeof(float), typeof(float) });
            var multiply = AccessTools.Method(typeof(UnityEngine.Quaternion), "op_Multiply", new[] { typeof(UnityEngine.Quaternion), typeof(UnityEngine.Quaternion) });
            if (code.Count != 17 || bodies?.FieldType != typeof(UnityEngine.Rigidbody[]) || angles == null
                || !angles.FieldType.IsGenericType || angles.FieldType.GetGenericTypeDefinition().FullName != "Unity.Collections.NativeArray`1"
                || angles.FieldType.GetGenericArguments()[0] != typeof(float) || getter == null || euler == null || multiply == null) return false;
            return code[0].opcode == OpCodes.Ldarg_0 && code[1].opcode == OpCodes.Ldfld && Equals(code[1].operand, bodies)
                && SameLocal(forceIndex, code[2]) && SameLocal(code[2], code[6]) && SameLocal(code[2], code[12])
                && code[3].opcode == OpCodes.Ldelem_Ref && code[4].opcode == OpCodes.Ldarg_0
                && code[5].opcode == OpCodes.Ldfld && Equals(code[5].operand, bodies) && code[7].opcode == OpCodes.Ldelem_Ref
                && code[8].Calls(getter) && ZeroFloat(code[9]) && code[10].opcode == OpCodes.Ldarg_0
                && code[11].opcode == OpCodes.Ldflda && Equals(code[11].operand, angles)
                && code[13].opcode == OpCodes.Call && code[13].operand is MethodInfo item && item.Name == "get_Item" && item.DeclaringType == angles.FieldType
                && item.ReturnType == typeof(float) && item.GetParameters().Length == 1 && item.GetParameters()[0].ParameterType == typeof(int)
                && ZeroFloat(code[14]) && code[15].Calls(euler) && code[16].Calls(multiply);
        }
        private static bool SameLocal(CodeInstruction a, CodeInstruction b)
        {
            bool local = a.opcode == OpCodes.Ldloc || a.opcode == OpCodes.Ldloc_S || a.opcode == OpCodes.Ldloc_0
                || a.opcode == OpCodes.Ldloc_1 || a.opcode == OpCodes.Ldloc_2 || a.opcode == OpCodes.Ldloc_3;
            return local && a.opcode == b.opcode && Equals(a.operand, b.operand);
        }
        private static bool ZeroFloat(CodeInstruction code) => code.opcode == OpCodes.Ldc_R4 && code.operand is float value && value == 0;
        private static void FixedStepPrefix(out bool __state)
        {
            __state = current != null && current.Active;
            if (__state) current!.BeginNativeFixedStep();
        }
        private static void FixedStepPostfix(bool __state)
        {
            if (__state) current?.EndNativeFixedStep();
        }
        private static Exception? FixedStepFinalizer(Exception? __exception, bool __state)
        {
            if (__exception != null && __state) current?.EndNativeFixedStep(failed: true);
            return __exception;
        }
        private static void ApplyBodyForce(UnityEngine.Rigidbody body, UnityEngine.Vector3 force, UnityEngine.ForceMode mode)
        {
            if (current != null && current.Active && current.DeferNativeForce(body, force, mode)) return;
            body.AddForce(force, mode);
        }
        private static void ApplyBodyRotation(UnityEngine.Rigidbody body, UnityEngine.Quaternion rotation)
        {
            if (current != null && current.Active && current.ApplyNativeRotation(body, rotation)) return;
            body.MoveRotation(rotation);
        }
        private static void SpeedChangingPrefix(TimeSpeed scale)
        {
            // Commit measured progress and release owned leases before native fast-forward clears
            // vehicles. Never override its pause, clock, physics step or requested speed.
            if (scale == TimeSpeed.FastForward || CurrentFastForward || UnityEngine.Time.timeScale > 1)
                current?.BeforeNativeFastForward();
        }
        private static void CrashPostfix(DrivingAI __instance, int myIndex, int otherIndex, bool addAction)
        {
            if (current == null || !current.Active) return;
            current.ObserveCrashExpiry(__instance, myIndex, otherIndex, addAction);
        }
        private static bool ReachedPrefix(int index, int waypointIndex, ref bool __result)
        {
            if (current == null || !current.Active || !current.Reached(index, waypointIndex, out bool result)) return true;
            __result = result; return false;
        }
        private static bool RequestPrefix(int index) => current == null || !current.Active || current.MayRequest(index);
        private static bool DespawnerPrefix(UnityEngine.Collider other) => current == null || !current.Active || !current.Owns(other.gameObject);
        private static bool RescuePrefix(AiCarRescueCheck __instance) => current == null || !current.Active || !current.Owns(__instance.gameObject);
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            if (ownsRegistration) { harmony.UnpatchAll(Owner); current = null; ownsRegistration = false; Ready = false; }
        }
    }
}
