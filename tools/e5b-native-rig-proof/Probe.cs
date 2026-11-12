using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using BAModAPI;
using BigAmbitions.Items;
using Buildings.Retail.Simulation;
using Entities;
using HarmonyLib;
using Helpers.BusinessSimulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;
using UnityEngine;
using UI;
using Object = UnityEngine.Object;

[assembly: RegisterModClass(typeof(E5bNativeRigProof.Entry))]
namespace E5bNativeRigProof
{
    [ModEntryOnCityLoad]
    public sealed class Entry : IModBigAmbitions
    {
        private GameObject host;
        public string[] RelativeAssetBundlePaths => Array.Empty<string>();
        public Task OnLoadAsync(ModContext context)
        {
            if (Session.Isolated()) { host = new GameObject("E5b native proof observer"); host.AddComponent<Session>(); }
            return Task.CompletedTask;
        }
        public Task OnUnloadAsync() { Session.Disarm(); if (host != null) Object.Destroy(host); return Task.CompletedTask; }
    }

    public static class E5bProofRigOps
    {
        public static JToken Op_e5b_proof(JObject args) => Session.Dispatch(args);
    }

    public sealed class Session : MonoBehaviour
    {
        private const string PatchId = "persistentcity.e5b-native-proof";
        private const int EventLimit = 131072, FrameLimit = 65536;
        private static readonly BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static readonly double ToMs = 1000.0 / Stopwatch.Frequency;
        private static readonly Harmony patches = new Harmony(PatchId);
        private static object save;
        private static Target[] targets = Array.Empty<Target>();
        private static IDictionary<string, FoodAuthority> authorities;
        private static FieldInfo simulatorRegistration, queueField;
        private static Func<object, BuildingRegistration> readRegistration;
        private static Func<object, Queue<ValueTuple<BuildingRegistration, int>>> readQueue;
        private static Func<bool> readHasPending;
        private static Func<object, int> readCompletionEvents;
        private static Func<GameSpeedController, bool> readPaused;
        private static GameSpeedController speedController;
        private static object nativeWork;
        private static CitySimulator simulation;
        private static NativeFoodMarket market;
        private static Func<object, NativeFoodMarket> readMarket;
        private static FieldInfo runtimeFailed, runtimeStatus, runtimeInitialization, runtimeSimulation, runtimeMarket, ownershipMarket;
        private static Func<string, int> readSavedSchema;
        private static volatile bool armed;
        private static bool measuring, recoveryHook;
        private static int armThread, offMainCallbacks;
        private static int eventCount, frameCount, currentFrame = -1, depth, exceptions, crossFrame;
        private static long eventDropped, frameDropped, sequence, callSequence, unionStart, frameTicks, observerTicks, priorFrameStart;
        private static int diagnosticCalls, firstMeasureFrame = -1;
        private static string error = "", stopReason = "not-started", queueQualification = "unbound";
        private static readonly Event[] events = new Event[EventLimit];
        private static readonly Frame[] frames = new Frame[FrameLimit];
        private sealed class HookInfo { public string Name, Before, After, Method; public bool Timed, Emit; public long Calls, Ticks, MaxTicks, Spikes; }
        private static readonly Dictionary<MethodBase, HookInfo> labels = new Dictionary<MethodBase, HookInfo>();
        private static readonly List<string> missingTimingScopes = new List<string>();
        private static readonly List<Fault> faults = new List<Fault>();
        private static readonly Dictionary<Type, FieldInfo[]> dtoFields = new Dictionary<Type, FieldInfo[]>();
        private static readonly Dictionary<string, FieldInfo> dtoNamedFields = new Dictionary<string, FieldInfo>();
        private sealed class OrderIdentity { public long Id; }
        private static ConditionalWeakTable<object, OrderIdentity> orderIdentities = new ConditionalWeakTable<object, OrderIdentity>();
        private static long nextOrderIdentity;
        private sealed class Target { public BuildingRegistration Registration; public string Place, NativeId; }
        private struct Event
        {
            public long n, call, durationTicks, ticks, hour, setupCount, requested, from, until;
            public int frame, target, generation, customers, orders, queueCount, thread, jobHour, pendingCompletionEvents;
            public bool desired, managed, suspended, hasPending;
            public string kind;
            public float money;
        }
        private struct Frame { public int frame, skippedUnityFrames; public double modWorkMs, wholeFrameMs, observerMs; public bool diagnostic; }
        private struct Scope { public long start, call; public int frame, jobHour; public bool timed; }
        private sealed class Fault
        {
            public string Id, Kind; public Target Target; public bool Closed, AppliedClosed;
            public object OriginalList; public object[] OriginalRows, ExpectedRows;
            public string OriginalJson, ExpectedJson;
            public IList List;
            public ItemInstance Holder;
        }
        public static bool Isolated() => Environment.GetCommandLineArgs().Contains("-agent-tests") && Environment.GetCommandLineArgs().Contains("-rig-test")
            && Application.productName == "Big Ambitions Agent Tests"
            && string.Equals(Application.persistentDataPath.Replace('\\', '/'),
                (Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/AppData/LocalLow/Hovgaard Games/Big Ambitions Agent Tests").Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
        private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
        private static void Guard()
        {
            Require(Isolated(), "e5b_not_isolated");
            Require(SaveGameManager.Current?.SaveGameName?.StartsWith("FT", StringComparison.Ordinal) == true, "e5b_ft_city_required");
            Require(Object.FindObjectOfType<GameManager>() != null, "e5b_city_required");
            if (armed) Require(ReferenceEquals(save, SaveGameManager.Current), "e5b_save_changed_rearm_required");
        }
        private static JToken Json(object value) => value == null ? JValue.CreateNull() : JToken.FromObject(value);
        private static JToken NativeFloat(float value) => new JObject { ["native_type"] = "System.Single", ["roundtrip"] = value.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            ["ieee754_hex"] = BitConverter.ToUInt32(BitConverter.GetBytes(value), 0).ToString("x8", System.Globalization.CultureInfo.InvariantCulture) };
        private static JObject Wrap(JToken before, JToken after)
        {
            var result = new JObject { ["before"] = before, ["after"] = after };
            Require(result.ToString(Formatting.None).Length <= 48000, "e5b_reply_bound_reduce_limit");
            return result;
        }
        private static bool Paused()
        {
            if (speedController == null) speedController = Object.FindObjectOfType<GameSpeedController>();
            if (readPaused == null)
            {
                var getter = typeof(GameSpeedController).GetProperty("Paused", All)?.GetGetMethod(true);
                Require(getter != null && getter.ReturnType == typeof(bool) && getter.GetParameters().Length == 0, "e5b_native_pause_contract_unavailable");
                readPaused = (Func<GameSpeedController, bool>)getter.CreateDelegate(typeof(Func<GameSpeedController, bool>));
            }
            Require(speedController != null, "e5b_native_speed_controller_unavailable");
            return readPaused(speedController);
        }
        private static Func<object, T> Getter<T>(FieldInfo field)
        {
            Require(field != null && typeof(T).IsAssignableFrom(field.FieldType), "e5b_cached_getter_contract_mismatch");
            var method = new DynamicMethod("E5bRead_" + field.Name, typeof(T), new[] { typeof(object) }, typeof(Session).Module, true);
            var il = method.GetILGenerator();
            if (field.IsStatic) il.Emit(OpCodes.Ldsfld, field);
            else { il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Castclass, field.DeclaringType); il.Emit(OpCodes.Ldfld, field); }
            il.Emit(OpCodes.Ret);
            return (Func<object, T>)method.CreateDelegate(typeof(Func<object, T>));
        }
        public static JToken Dispatch(JObject args)
        {
            Require(!armed || Thread.CurrentThread.ManagedThreadId == armThread, "e5b_dispatch_thread_changed");
            Guard();
            foreach (var p in args.Properties()) Require(new[] { "action", "businesses", "expected_names", "id", "section", "offset", "limit", "holder_id", "product", "closed", "fault_id", "resident_id", "intent_id", "items", "holder_ids" }.Contains(p.Name), "e5b_unknown_argument");
            string action = (string)args["action"];
            if (measuring) { TouchFrame(); diagnosticCalls++; }
            try
            {
                if (action == "discover") { var value = Discover(args); return Wrap(value, value.DeepClone()); }
                if (action == "runtime") { var value = RuntimeStatus(); return Wrap(value, value.DeepClone()); }
                if (action == "placement-facts") { var value = PlacementFacts(args); return Wrap(value, value.DeepClone()); }
                if (action == "arm") return Wrap(Status(), Arm(args));
                Require(armed, "e5b_not_armed");
                if (action == "status") return Wrap(Status(), Status());
                if (action == "snapshot") { var value = Snapshot(args); return Wrap(value, value.DeepClone()); }
                if (action == "read" || action == "capture" || action == "replay-paid") return InvokeProduction(action, args);
                if (action == "events") return Wrap(Status(), PageEvents(args));
                if (action == "frames") return Wrap(Status(), PageFrames(args));
                if (action == "measure-start") { var before = Status(); StartMeasurement(); return Wrap(before, Status()); }
                if (action == "measure-stop") { var before = Status(); measuring = false; stopReason = "explicit-stop"; return Wrap(before, Status()); }
                if (action == "fault-close" || action == "fault-price" || action == "fault-cargo") return Inject(action, args);
                if (action == "restore") return Restore((string)args["fault_id"]);
                if (action == "disarm") { var before = Status(); Require(faults.Count == 0, "e5b_restore_faults_before_disarm"); Disarm(); return Wrap(before, Status()); }
                return Wrap(Status(), new JObject { ["supported"] = false, ["refusal"] = "e5b_unsupported_action", ["action"] = action });
            }
            catch (Exception e)
            {
                var code = e is InvalidOperationException && e.Message.StartsWith("e5b_", StringComparison.Ordinal) ? e.Message : "e5b_" + e.GetType().Name;
                return Wrap(Status(), new JObject { ["supported"] = false, ["refusal"] = code });
            }
        }
        private static JObject Arm(JObject args)
        {
            Require(faults.Count == 0, "e5b_restore_faults_before_arm");
            Require(Paused(), "e5b_pause_before_arm");
            var ids = args["businesses"] as JArray;
            Require(ids != null && ids.Count >= 1 && ids.Count <= 8, "e5b_one_to_eight_explicit_businesses_required");
            var expectedNames = args["expected_names"] as JObject;
            Require(expectedNames != null && expectedNames.Count == ids.Count, "e5b_exact_reviewed_business_names_required");
            var selected = new List<Target>();
            foreach (var token in ids)
            {
                string id = (string)token;
                Require(id != null && id.Length <= 256 && !selected.Any(t => t.NativeId == id), "e5b_invalid_or_duplicate_business");
                var reg = SaveGameManager.Current.BuildingRegistrations.Single(r => r.StreetName + "#" + r.StreetNumber == id);
                Require(reg.RentedByPlayer && reg.HasEstablishedBusiness && !string.IsNullOrWhiteSpace(reg.BusinessName)
                    && (string)expectedNames[id] == reg.BusinessName, "e5b_exact_genuine_fixture_business_required");
                selected.Add(new Target { Registration = reg, NativeId = id, Place = NativeFoodMarket.PlaceId(reg) });
            }
            Disarm();
            armThread = Thread.CurrentThread.ManagedThreadId; offMainCallbacks = 0;
            save = SaveGameManager.Current; targets = selected.ToArray();
            var owner = typeof(NativeFoodMarket).Assembly.GetType("PersistentCity.Runtime.NativeFoodOwnership", true);
            authorities = (IDictionary<string, FoodAuthority>)owner.GetField("records", All).GetValue(null);
            readMarket = Getter<NativeFoodMarket>(owner.GetField("market", All));
            market = readMarket(null);
            simulatorRegistration = AccessTools.Field(typeof(BusinessSimulator), "buildingRegistration");
            readRegistration = Getter<BuildingRegistration>(simulatorRegistration);
            nativeWork = BusinessSimulatorHelper.Work;
            Require(nativeWork.GetType().IsGenericType && nativeWork.GetType().GetGenericTypeDefinition().FullName == "Helpers.DistributedWork`1", "e5b_distributed_work_type_mismatch");
            queueField = AccessTools.Field(nativeWork.GetType(), "_pendingWork");
            Require(queueField != null && queueField.FieldType == typeof(Queue<ValueTuple<BuildingRegistration, int>>), "e5b_pending_queue_contract_mismatch");
            readQueue = Getter<Queue<ValueTuple<BuildingRegistration, int>>>(queueField);
            Require(readQueue(nativeWork) != null, "e5b_pending_queue_uninitialized");
            var hasPending = nativeWork.GetType().GetProperty("HasPendingWork", All)?.GetGetMethod(true)
                ?? AccessTools.Method(nativeWork.GetType(), "HasPendingWork", Type.EmptyTypes);
            Require(hasPending != null && hasPending.ReturnType == typeof(bool) && hasPending.GetParameters().Length == 0, "e5b_has_pending_contract_mismatch");
            readHasPending = (Func<bool>)hasPending.CreateDelegate(typeof(Func<bool>), nativeWork);
            var completion = AccessTools.Field(typeof(BusinessSimulatorHelper), "_pendingCompletionEvents");
            Require(completion != null && completion.IsStatic && completion.FieldType == typeof(int), "e5b_pending_completion_event_contract_mismatch");
            readCompletionEvents = Getter<int>(completion);
            queueQualification = "Read-only native metadata/IL verified exact _pendingWork queue, HasPendingWork reads Count, ForceCompleteAllWork dequeues/invokes _updateAction until Count zero, and SimulateBusiness passes raw tuple hour to SetUp. Runtime thread, callback and ending-hour observations remain required; no direct Core boundary is driven.";
            var runtime = Object.FindObjectOfType<PopulationRuntime>();
            simulation = runtime == null ? null : (CitySimulator)typeof(PopulationRuntime).GetField("simulation", All).GetValue(runtime);
            PrepareDtos();
            labels.Clear(); missingTimingScopes.Clear(); eventCount = frameCount = exceptions = crossFrame = 0; sequence = callSequence = eventDropped = frameDropped = 0; error = "";
            orderIdentities = new ConditionalWeakTable<object, OrderIdentity>(); nextOrderIdentity = 0;
            try
            {
                Hook(AccessTools.Method(typeof(GameManager), "RunMainGameTick"), "native_tick");
                Hook(AccessTools.Method(owner, "AfterNativeTick"), "after_native_tick");
                var recovery = AccessTools.Method(typeof(NativeFoodMarket), "RevalidateRefusedContract");
                recoveryHook = recovery != null;
                if (recoveryHook) Hook(recovery, "recovery");
                Hook(AccessTools.Method(nativeWork.GetType(), "ForceCompleteAllWork", Type.EmptyTypes), "work_drain");
                Hook(AccessTools.Method(typeof(RetailBusinessSimulator), "ProcessAllCustomersFromThisHour"), "native_hourly_customers");
                Hook(AccessTools.Method(typeof(RetailBusinessSimulator), "ProcessCustomer"), "native_customer");
                Hook(AccessTools.Method(typeof(BusinessSimulatorHelper), "SimulateBusiness", new[] { typeof(ValueTuple<BuildingRegistration, int>) }), "simulate_business");
                Hook(AccessTools.Method(typeof(PopulationRuntime), "Update"), "population_update");
                TimingHook(typeof(PopulationRuntime), "OnGUI");
                foreach (string name in new[] { "FixedStepPrefix", "FixedStepPostfix", "FixedStepFinalizer" }) TimingHook("PersistentCity.Runtime.PhysicalTrafficHooks", name);
                foreach (string name in new[] { "DeferNativeForce", "ApplyNativeRotation" }) TimingHook("PersistentCity.Runtime.PhysicalVehicles", name);
                TimingHook("PersistentCity.Runtime.PhysicalIntersections", "UpdatePostfix");
                foreach (string name in new[] { "HourlyCustomers", "CustomerSale", "GenerateCustomers", "CustomerDraw", "ResetRegistration" }) TimingHook(owner, name);
                armed = true; stopReason = "armed";
                return Status();
            }
            catch { patches.UnpatchAll(PatchId); armed = false; throw; }
        }
        private static void TimingHook(string typeName, string name) => TimingHook(typeof(NativeFoodMarket).Assembly.GetType(typeName, false), name, typeName);
        private static void TimingHook(Type type, string name, string requestedType = null)
        {
            MethodBase method = null;
            try { if (type != null) method = AccessTools.Method(type, name); } catch (AmbiguousMatchException) { }
            string key = (requestedType ?? type?.FullName ?? "unknown") + "." + name;
            if (method == null) { missingTimingScopes.Add(key); return; }
            Hook(method, key, timedOnly: true);
        }
        private static void Hook(MethodBase method, string label, bool timedOnly = false)
        {
            Require(method != null, "e5b_required_native_hook_missing_" + label);
            labels.Add(method, new HookInfo { Name = label, Before = label + ":before", After = label + ":after",
                Method = method.DeclaringType.FullName + "." + method.Name,
                Emit = !timedOnly && label != "population_update", Timed = timedOnly || label == "population_update" || label == "after_native_tick" });
            string prefix = label == "recovery" ? nameof(RecoveryBefore) : label == "simulate_business" ? nameof(JobBefore) : method.IsStatic ? nameof(StaticBefore) : nameof(Before);
            string finalizer = label == "recovery" ? nameof(RecoveryAfter) : label == "simulate_business" ? nameof(JobAfter) : method.IsStatic ? nameof(StaticAfter) : nameof(After);
            patches.Patch(method, prefix: new HarmonyMethod(typeof(Session), prefix) { priority = Priority.First },
                finalizer: new HarmonyMethod(typeof(Session), finalizer) { priority = Priority.Last });
        }
        private static bool ContextValid() => armed && ReferenceEquals(save, SaveGameManager.Current)
            && SaveGameManager.Current?.SaveGameName?.StartsWith("FT", StringComparison.Ordinal) == true;
        private static bool CallbackThread()
        {
            if (!armed) return false;
            if (Thread.CurrentThread.ManagedThreadId == armThread) return true;
            Interlocked.Increment(ref offMainCallbacks);
            return false;
        }
        private static void Before(MethodBase __originalMethod, object __instance, out Scope __state)
            => Begin(__originalMethod, __instance, out __state);
        private static void StaticBefore(MethodBase __originalMethod, out Scope __state)
            => Begin(__originalMethod, null, out __state);
        private static void RecoveryBefore(MethodBase __originalMethod, BuildingRegistration __0, out Scope __state)
            => Begin(__originalMethod, __0, out __state);
        private static void JobBefore(MethodBase __originalMethod, ValueTuple<BuildingRegistration, int> __0, out Scope __state)
            => Begin(__originalMethod, __0.Item1, out __state, __0.Item2);
        private static void Begin(MethodBase method, object instance, out Scope __state, int jobHour = -1)
        {
            __state = default;
            if (!CallbackThread() || !ContextValid()) return;
            try
            {
                var hook = labels[method];
                hook.Calls++;
                long call = hook.Emit ? ++callSequence : 0;
                if (hook.Emit) Trace(hook.Before, instance, call, 0, jobHour);
                __state = new Scope { start = Stopwatch.GetTimestamp(), frame = Time.frameCount, call = call, jobHour = jobHour };
                if (measuring && hook.Timed)
                {
                    TouchFrame(); __state.timed = true; __state.start = Stopwatch.GetTimestamp();
                    if (depth++ == 0) unionStart = __state.start;
                }
            }
            catch { error = "e5b_observer_prefix_failed"; measuring = false; }
        }
        private static Exception After(MethodBase __originalMethod, object __instance, Exception __exception, Scope __state)
            => End(__originalMethod, __instance, __exception, __state);
        private static Exception StaticAfter(MethodBase __originalMethod, Exception __exception, Scope __state)
            => End(__originalMethod, null, __exception, __state);
        private static Exception RecoveryAfter(MethodBase __originalMethod, BuildingRegistration __0, Exception __exception, Scope __state)
            => End(__originalMethod, __0, __exception, __state);
        private static Exception JobAfter(MethodBase __originalMethod, ValueTuple<BuildingRegistration, int> __0, Exception __exception, Scope __state)
            => End(__originalMethod, __0.Item1, __exception, __state);
        private static Exception End(MethodBase method, object instance, Exception __exception, Scope __state)
        {
            long end = Stopwatch.GetTimestamp();
            if (!CallbackThread() || !ContextValid()) return __exception;
            try
            {
                if (__state.timed)
                {
                    if (__state.frame != Time.frameCount) crossFrame++;
                    if (__exception != null) exceptions++;
                    if (--depth == 0) frameTicks += Math.Max(0, end - unionStart);
                }
                var hook = labels[method];
                if (__state.timed)
                {
                    long duration = Math.Max(0, end - __state.start); hook.Ticks += duration; hook.MaxTicks = Math.Max(hook.MaxTicks, duration);
                    if (duration * ToMs >= 30) hook.Spikes++;
                }
                if (hook.Emit) Trace(hook.After, instance, __state.call, Math.Max(0, end - __state.start), __state.jobHour);
            }
            catch { error = "e5b_observer_finalizer_failed"; measuring = false; }
            return __exception;
        }
        private static void Trace(string kind, object instance, long call, long duration, int jobHour)
        {
            long start = Stopwatch.GetTimestamp();
            if (measuring) TouchFrame();
            int only = -1;
            if (instance is BusinessSimulator)
            {
                var registration = readRegistration(instance);
                for (int i = 0; i < targets.Length; i++) if (ReferenceEquals(targets[i].Registration, registration)) { only = i; break; }
            }
            if (instance is BuildingRegistration registrationTarget)
                for (int i = 0; i < targets.Length; i++) if (ReferenceEquals(targets[i].Registration, registrationTarget)) { only = i; break; }
            if ((instance is BusinessSimulator || instance is BuildingRegistration) && only < 0) return;
            for (int i = 0; i < targets.Length; i++)
            {
                if (only >= 0 && only != i) continue;
                var reg = targets[i].Registration; authorities.TryGetValue(targets[i].Place, out var row);
                var entry = new Event { n = ++sequence, call = call, durationTicks = duration, ticks = Stopwatch.GetTimestamp(), kind = kind, target = i, frame = Time.frameCount, thread = armThread, jobHour = jobHour,
                    hour = SaveGameManager.Current.Day * 24L + SaveGameManager.Current.Hour, setupCount = FoodSupplyReader.Setups,
                    generation = row?.Generation ?? -1, desired = row?.Desired ?? false, managed = row?.Managed ?? false,
                    suspended = row?.Suspended ?? false, requested = row?.RequestedHour ?? -1, from = row?.FromHour ?? -1, until = row?.UntilHour ?? -1,
                    customers = BuildingManager.Instance != null && ReferenceEquals(BuildingManager.Instance.buildingRegistration, reg) ? IndoorCustomerSpawner.Customers.Count : 0,
                    orders = reg.unprocessedCompletedOrders.Count, money = SaveGameManager.Current.Money,
                    queueCount = readQueue(nativeWork)?.Count ?? -1, hasPending = readHasPending(), pendingCompletionEvents = readCompletionEvents(null) };
                if (eventCount < events.Length) events[eventCount++] = entry; else eventDropped++;
            }
            if (measuring) observerTicks += Stopwatch.GetTimestamp() - start;
        }
        private void Update()
        {
            if (Volatile.Read(ref offMainCallbacks) != 0) { measuring = false; error = "e5b_off_main_thread_callbacks_capture_invalid"; }
            if (armed && !ContextValid()) { Disarm(); stopReason = "save_changed"; }
            if (measuring) TouchFrame();
        }
        private void OnDestroy() => Disarm();
        private static void TouchFrame()
        {
            int nowFrame = Time.frameCount;
            if (nowFrame == currentFrame) return;
            long now = Stopwatch.GetTimestamp();
            if (currentFrame > firstMeasureFrame && currentFrame >= 0)
            {
                var row = new Frame { frame = currentFrame, skippedUnityFrames = Math.Max(0, nowFrame - currentFrame - 1), modWorkMs = frameTicks * ToMs,
                    wholeFrameMs = (now - priorFrameStart) * ToMs, observerMs = observerTicks * ToMs, diagnostic = diagnosticCalls != 0 };
                if (frameCount < frames.Length) frames[frameCount++] = row; else frameDropped++;
            }
            if (depth != 0) { crossFrame++; measuring = false; error = "e5b_cross_frame_scope"; }
            currentFrame = nowFrame; priorFrameStart = now; frameTicks = observerTicks = 0; diagnosticCalls = 0;
        }
        private static void StartMeasurement()
        {
            Require(faults.Count == 0 || faults.All(f => f.Kind == "fault-close"), "e5b_nonclosure_fault_active_in_measurement");
            Require(depth == 0, "e5b_active_scope");
            frameCount = exceptions = crossFrame = 0; frameDropped = 0; currentFrame = firstMeasureFrame = Time.frameCount;
            foreach (var hook in labels.Values) { hook.Calls = hook.Ticks = hook.MaxTicks = hook.Spikes = 0; }
            frameTicks = observerTicks = 0; diagnosticCalls = 1; priorFrameStart = Stopwatch.GetTimestamp(); measuring = true;
        }
        public static void Disarm()
        {
            measuring = false; armed = false; patches.UnpatchAll(PatchId); depth = 0;
            if (faults.Count != 0) error = "e5b_unrestored_negative_faults_reload_disposable_copy";
        }
        private static JObject Status() => new JObject {
            ["prototype"] = true, ["native_validated"] = false, ["armed"] = armed, ["measuring"] = measuring,
            ["error"] = error, ["stop_reason"] = stopReason, ["events"] = eventCount, ["event_dropped"] = eventDropped,
            ["frames"] = frameCount, ["frame_dropped"] = frameDropped, ["timed_exceptions"] = exceptions, ["cross_frame_scopes"] = crossFrame,
            ["stopwatch_frequency"] = Stopwatch.Frequency,
            ["arm_thread_id"] = armThread, ["off_main_callbacks"] = Volatile.Read(ref offMainCallbacks),
            ["thread_qualification"] = "Events observe only the arm/main thread. Any off-main prefix/finalizer increments the atomic invalidation counter before touching Unity/native snapshots or event buffers; original execution and exceptions are preserved. Zero off-main callbacks is required; it does not prove an uninstrumented native queue contract.",
            ["queue_contract_verified"] = readQueue != null && readHasPending != null,
            ["queue_binding_verified"] = readQueue != null && readHasPending != null,
            ["pending_completion_events"] = readCompletionEvents == null ? JValue.CreateNull() : (JToken)readCompletionEvents(null),
            ["settlement_qualification"] = "Queue.Count zero is only queue drain. Native _pendingCompletionEvents and actual completion/customer/accounting trace must also be reconciled before claiming all native work settled.",
            ["timing_coverage_complete"] = armed && missingTimingScopes.Count == 0,
            ["missing_timing_scopes"] = new JArray(missingTimingScopes),
            ["bound_scopes"] = new JArray(labels.Values.Select(h => new JObject { ["method"] = h.Method, ["timed_pc_scope"] = h.Timed,
                ["emits_events"] = h.Emit, ["calls"] = h.Calls, ["inclusive_ms"] = h.Ticks * ToMs, ["max_ms"] = h.MaxTicks * ToMs, ["spikes_ge30"] = h.Spikes })),
            ["recovery_hook_available"] = recoveryHook,
            ["active_faults"] = new JArray(faults.Select(f => new JObject { ["id"] = f.Id, ["kind"] = f.Kind, ["business"] = f.Target.NativeId })),
            ["targets"] = new JArray(targets.Select(t => new JObject { ["id"] = t.NativeId, ["place"] = t.Place })),
            ["queue_qualification"] = queueQualification,
            ["timing_qualification"] = "Mod work is the nested union of bound PC Update/AfterNativeTick, OnGUI, physical native callbacks and food suppression prefixes; missing required scopes make the metric partial. Original native game hooks are observation-only, never whole-native-method timing scopes. Includes synchronous native work forced inside PC and nested probe cost. Do not add nested inclusive components. wholeFrameMs is observer-to-observer wall interval, not CPU/GPU timing. observerMs is partial trace overhead, not total instrumentation overhead. Missing/off-main/cross-frame/overflow/error/diagnostic captures cannot certify combined work. Autosave and no-save proof are independent prerequisites." };
        private static Target Selected(string id) { var target = targets.SingleOrDefault(t => t.NativeId == id); Require(target != null, "e5b_explicit_selected_business_required"); return target; }
        private static int Number(JObject args, string name, int fallback, int max)
        { int n = (int?)args[name] ?? fallback; Require(n >= 0 && n <= max, "e5b_invalid_" + name); return n; }
        private static JObject PlacementFacts(JObject args)
        {
            Require(!measuring, "e5b_placement_facts_outside_measurement_required");
            foreach (var p in args.Properties()) Require(new[] { "action", "items", "id", "holder_ids" }.Contains(p.Name), "e5b_placement_unknown_argument");
            var ids = args["items"] as JArray;
            Require(ids != null && ids.Count >= 1 && ids.Count <= 16, "e5b_one_to_sixteen_explicit_item_ids_required");
            var names = new HashSet<string>(StringComparer.Ordinal); var definitions = new JArray();
            foreach (var token in ids)
            {
                Require(token.Type == JTokenType.String, "e5b_invalid_item_id");
                string id = (string)token;
                Require(!string.IsNullOrWhiteSpace(id) && id.Length <= 128 && names.Add(id), "e5b_invalid_or_duplicate_item_id");
                var item = ItemsGetter.GetByName(id);
                Require(item != null && item.itemName == id, "e5b_loaded_item_definition_required");
                var requirements = new JArray();
                Require(item.furnitureRequirements == null || item.furnitureRequirements.Count <= 64, "e5b_furniture_requirement_bound_exceeded");
                if (item.furnitureRequirements != null) foreach (var requirement in item.furnitureRequirements)
                {
                    Require(requirement != null, "e5b_null_furniture_requirement");
                    var type = requirement.GetType(); var row = new JObject { ["type"] = type.FullName };
                    if (type.FullName == "Furniture.Requirements.HasItemTypeAttached")
                    {
                        var field = type.GetField("itemType", BindingFlags.Public | BindingFlags.Instance);
                        Require(field != null && field.FieldType.IsEnum, "e5b_attached_item_type_contract_unavailable");
                        row["required_item_type"] = EnumFact(field.GetValue(requirement));
                    }
                    else row["required_item_type"] = JValue.CreateNull();
                    requirements.Add(row);
                }
                definitions.Add(new JObject { ["id"] = id, ["stack_type"] = EnumFact(item.stackType),
                    ["wall_mounted"] = item.wallMounted, ["snap_to_ceiling"] = item.snapToCeiling,
                    ["furniture_requirements"] = requirements, ["prefab"] = JValue.CreateNull(),
                    ["prefab_status"] = "unknown_uninspected_no_asset_load_or_instantiation" });
            }
            var parents = new JArray(); var holderIds = args["holder_ids"] as JArray;
            Require(args["holder_ids"] == null || holderIds != null, "e5b_invalid_parent_holder_ids");
            if (holderIds != null && holderIds.Count > 0)
            {
                Require(holderIds.Count <= 16 && args["id"]?.Type == JTokenType.String, "e5b_explicit_parent_business_required");
                string business = (string)args["id"];
                var registration = SaveGameManager.Current.BuildingRegistrations.SingleOrDefault(r => r.StreetName + "#" + r.StreetNumber == business);
                Require(registration != null && registration.RentedByPlayer && registration.HasEstablishedBusiness, "e5b_genuine_parent_business_required");
                var manager = Object.FindObjectOfType<BuildingManager>();
                Require(manager != null && ReferenceEquals(manager.buildingRegistration, registration), "e5b_selected_parent_interior_required");
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var token in holderIds)
                {
                    Require(token.Type == JTokenType.String, "e5b_invalid_parent_holder_id"); string id = (string)token;
                    Require(!string.IsNullOrEmpty(id) && id.Length <= 128 && seen.Add(id), "e5b_invalid_or_duplicate_parent_holder_id");
                    Require(registration.itemInstances.TryGetValue(id, out var instance), "e5b_actual_parent_holder_required");
                    var controller = manager.allItemControllers.SingleOrDefault(c => c != null && ReferenceEquals(c.ItemInstance, instance));
                    Require(controller != null, "e5b_actual_loaded_parent_controller_required");
                    var points = controller.GetComponentsInChildren<AttachmentPoint>(true);
                    Require(points.Length <= 64, "e5b_attachment_point_bound_exceeded");
                    parents.Add(new JObject { ["business_id"] = business, ["holder_id"] = id, ["item"] = instance.itemName,
                        ["attachment_points"] = new JArray(points.Select((point, index) => new JObject { ["index"] = index,
                            ["attachment_type"] = EnumFact(point.AttachmentPointType), ["active"] = point.gameObject.activeInHierarchy })),
                        ["qualification"] = "Actual placed parent components only; mask compatibility is not native placement validity." });
                }
            }
            return new JObject { ["section"] = "placement-facts", ["definitions"] = definitions, ["parents"] = parents,
                ["qualification"] = "Read-only loaded definitions and explicit actual placed parent components. No prefab inspection, asset loading, instantiation, placement, requirement evaluation or positive eligibility mutation." };
        }
        private static JObject EnumFact(object value)
        {
            Require(value != null && value.GetType().IsEnum, "e5b_placement_enum_contract_unavailable");
            return new JObject { ["native_type"] = value.GetType().FullName, ["value"] = Convert.ToInt64(value), ["name"] = value.ToString() };
        }
        private static JObject Discover(JObject args)
        {
            int offset = Number(args, "offset", 0, 2000), limit = Number(args, "limit", 16, 32);
            var selected = SaveGameManager.Current.BuildingRegistrations.Where(r => r.RentedByPlayer && r.HasEstablishedBusiness).ToArray();
            Require(selected.Length <= 2000, "e5b_business_bound_exceeded");
            return new JObject { ["section"] = "discover", ["rows"] = new JArray(selected.Skip(offset).Take(limit).Select(r => new JObject {
                ["id"] = r.StreetName + "#" + r.StreetNumber, ["name"] = r.BusinessName, ["type"] = r.businessTypeName,
                ["place"] = NativeFoodMarket.PlaceId(r), ["products"] = Json(r.cachedAvailableProducts) })),
                ["count"] = selected.Length, ["next"] = offset + limit < selected.Length ? (JToken)(offset + limit) : JValue.CreateNull() };
        }
        private static JObject RuntimeStatus()
        {
            if (readSavedSchema == null)
            {
                runtimeFailed = AccessTools.Field(typeof(PopulationRuntime), "failed");
                runtimeStatus = AccessTools.Field(typeof(PopulationRuntime), "status");
                runtimeInitialization = AccessTools.Field(typeof(PopulationRuntime), "initialization");
                runtimeSimulation = AccessTools.Field(typeof(PopulationRuntime), "simulation");
                runtimeMarket = AccessTools.Field(typeof(PopulationRuntime), "foodMarket");
                ownershipMarket = AccessTools.Field(typeof(NativeFoodMarket).Assembly.GetType("PersistentCity.Runtime.NativeFoodOwnership", true), "market");
                Require(runtimeFailed?.FieldType == typeof(bool) && runtimeStatus?.FieldType == typeof(string)
                    && runtimeInitialization != null && runtimeSimulation != null && runtimeMarket != null && ownershipMarket != null,
                    "e5b_runtime_readiness_contract_unavailable");
                var method = AccessTools.Method(typeof(SaveCodec), "ReadSchema", new[] { typeof(string) });
                Require(method != null && method.ReturnType == typeof(int), "e5b_saved_schema_header_reader_unavailable");
                readSavedSchema = (Func<string, int>)method.CreateDelegate(typeof(Func<string, int>));
            }
            var runtime = Object.FindObjectOfType<PopulationRuntime>();
            var task = runtime == null ? null : runtimeInitialization.GetValue(runtime) as Task;
            var attached = ownershipMarket.GetValue(null);
            var actualMarket = runtime == null ? null : runtimeMarket.GetValue(runtime);
            bool present = SaveGameManager.Current.modData.TryGetValue(SaveStore.StateKey, out var payload);
            JToken schema = JValue.CreateNull();
            string header = !present ? "absent" : payload == null ? "unreadable_null" : "unreadable";
            if (present && payload != null && payload.Length <= SaveCodec.MaxInputCharacters)
            {
                try
                {
                    int value = readSavedSchema(payload); schema = value;
                    header = value > CityState.CurrentSchema ? "future_header" : value >= 13 ? "header_in_supported_predecessor_range" : "unsupported_predecessor_header";
                }
                catch { header = "unreadable_header"; }
            }
            else if (present && payload != null) header = "unreadable_over_input_bound";
            string status = runtime == null ? "component_absent" : (string)runtimeStatus.GetValue(runtime);
            if (status != null && status.Length > 512) status = status.Substring(0, 512) + " [bounded]";
            return new JObject { ["section"] = "runtime", ["component_present"] = runtime != null,
                ["component_enabled"] = runtime == null ? JValue.CreateNull() : (JToken)runtime.enabled,
                ["failed"] = runtime == null ? JValue.CreateNull() : Json(runtimeFailed.GetValue(runtime)), ["status"] = status,
                ["initialization_task"] = task == null ? "absent" : task.Status.ToString(),
                ["simulation_ready"] = runtime != null && runtimeSimulation.GetValue(runtime) != null,
                ["ownership_market_ready"] = attached != null, ["runtime_market_ready"] = actualMarket != null,
                ["market_reference_matches"] = attached != null && ReferenceEquals(attached, actualMarket),
                ["own_namespace_present"] = present, ["saved_schema"] = schema, ["schema_header_status"] = header,
                ["qualification"] = "Read-only actual runtime fields and existing production schema-header parser only; not full payload validation, migration, activation or new identity creation. No raw payload/consumer names emitted." };
        }
        private static JObject PageEvents(JObject args)
        {
            int offset = Number(args, "offset", 0, eventCount), limit = Number(args, "limit", 64, 128);
            var rows = new JArray(); for (int i = offset; i < Math.Min(eventCount, offset + limit); i++) rows.Add(Json(events[i]));
            return new JObject { ["rows"] = rows, ["count"] = eventCount, ["next"] = offset + rows.Count < eventCount ? (JToken)(offset + rows.Count) : JValue.CreateNull(), ["dropped"] = eventDropped };
        }
        private static JObject PageFrames(JObject args)
        {
            Require(!measuring, "e5b_stop_before_exporting_frames");
            int offset = Number(args, "offset", 0, frameCount), limit = Number(args, "limit", 128, 256);
            var rows = new JArray(); for (int i = offset; i < Math.Min(frameCount, offset + limit); i++) rows.Add(Json(frames[i]));
            return new JObject { ["rows"] = rows, ["count"] = frameCount, ["next"] = offset + rows.Count < frameCount ? (JToken)(offset + rows.Count) : JValue.CreateNull(), ["dropped"] = frameDropped };
        }
        private static JObject Snapshot(JObject args)
        {
            var target = Selected((string)args["id"]); var reg = target.Registration;
            string section = (string)args["section"] ?? "business";
            int offset = Number(args, "offset", 0, 100000), limit = Number(args, "limit", 8, 32);
            var rows = new JArray(); int count;
            if (section == "business")
            {
                authorities.TryGetValue(target.Place, out var row);
                return new JObject { ["section"] = "business", ["id"] = target.NativeId, ["place"] = target.Place, ["authority"] = Json(row),
                    ["native_paused"] = Paused(),
                    ["temporarily_closed"] = reg.temporarilyClosed, ["established"] = reg.HasEstablishedBusiness,
                    ["advertised"] = Json(reg.cachedAvailableProducts), ["prices"] = PriceRows(reg),
                    ["hour"] = SaveGameManager.Current.Day * 24L + SaveGameManager.Current.Hour,
                    ["money"] = SaveGameManager.Current.Money.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                    ["supply_setups"] = FoodSupplyReader.Setups, ["supply_setup_ms"] = FoodSupplyReader.SetupMilliseconds,
                    ["read_kind"] = "raw fields only; no PC ownership Find/ScopeStatus, supply setup, restock or sale called" };
            }
            if (section == "cargo")
            {
                var holders = reg.itemInstances.Values.OrderBy(i => i.id, StringComparer.Ordinal).ToArray(); count = holders.Length;
                Require(count <= 2000, "e5b_holder_bound_exceeded");
                foreach (var holder in holders.Skip(offset).Take(limit)) rows.Add(new JObject { ["holder_id"] = holder.id,
                    ["holder_item"] = holder.itemName, ["parent_id"] = holder.parentId, ["cargo"] = Cargo(holder.cargoInstances, 0) });
            }
            else if (section == "orders")
            {
                count = reg.unprocessedCompletedOrders.Count;
                Require(count <= 100000, "e5b_order_bound_exceeded");
                foreach (var order in reg.unprocessedCompletedOrders.Skip(offset).Take(limit)) rows.Add(OrderDto(order));
            }
            else if (section == "history") { count = reg.orderHistory.Count; foreach (var row in reg.orderHistory.Skip(offset).Take(limit)) rows.Add(RawData(row, 0)); }
            else if (section == "receipts")
            {
                var selected = SaveGameManager.Current.modData.Where(p => FoodSaleReceipts.IsReceiptKey(p.Key)).OrderBy(p => p.Key, StringComparer.Ordinal).ToArray(); count = selected.Length;
                foreach (var pair in selected.Skip(offset).Take(limit)) rows.Add(new JObject { ["key"] = pair.Key, ["value"] = pair.Value });
            }
            else if (section == "residents")
            {
                Require(simulation?.State.Food != null, "e5b_live_population_unavailable");
                var selected = simulation.State.Food.Residents.Where(m => m.PlaceId == target.Place).ToArray(); count = selected.Length;
                foreach (var meal in selected.Skip(offset).Take(limit)) rows.Add(Json(meal));
            }
            else if (section == "statements")
            {
                var selected = SaveGameManager.Current.financialSummaries.SelectMany(s => s.businessIncomeStatements.Select(b => new { summary = s, statement = b }))
                    .Where(x => x.statement.Address == reg.Address).ToArray(); count = selected.Length;
                foreach (var x in selected.Skip(offset).Take(limit)) rows.Add(new JObject { ["day"] = x.summary.dayNumber,
                    ["sales"] = NativeFloat(x.statement.TotalSales), ["resources"] = NativeFloat(x.statement.TotalResources), ["salary"] = NativeFloat(x.statement.SalaryExpenses),
                    ["rent"] = NativeFloat(x.statement.RentExpenses), ["profit"] = NativeFloat(x.statement.TotalProfit) });
            }
            else if (section == "transactions")
            {
                count = SaveGameManager.Current.Transactions.Count;
                foreach (var x in SaveGameManager.Current.Transactions.Skip(offset).Take(limit))
                {
                    var matched = new JArray(targets.Where(t => x.address == t.Registration.Address).Select(t => t.NativeId));
                    rows.Add(new JObject { ["amount"] = NativeFloat(x.amount), ["balance"] = NativeFloat(x.balance),
                        ["day"] = x.timestamp.Day, ["hour"] = x.timestamp.Hour, ["minute"] = NativeFloat(x.timestamp.Minute),
                        ["type"] = x.transactionType, ["selected_business_ids"] = matched,
                        ["attribution"] = matched.Count == 0 ? "unattributed_to_selected_businesses; includes null/other addresses" : "exact_native_address_equality" });
                }
            }
            else return new JObject { ["supported"] = false, ["refusal"] = "e5b_unknown_snapshot_section" };
            var result = new JObject { ["section"] = section, ["id"] = target.NativeId, ["rows"] = rows, ["count"] = count,
                ["next"] = offset + rows.Count < count ? (JToken)(offset + rows.Count) : JValue.CreateNull() };
            Require(result.ToString(Formatting.None).Length <= 48000, "e5b_snapshot_reply_bound_reduce_limit");
            return result;
        }
        private static JToken Cargo(IEnumerable<CargoInstance> cargo, int depth)
        {
            if (cargo == null) return JValue.CreateNull();
            Require(depth <= 8, "e5b_nested_cargo_depth_exceeded");
            var rows = new JArray();
            foreach (var c in cargo)
            {
                Require(rows.Count < 512, "e5b_cargo_bound_exceeded");
                rows.Add(c == null ? JValue.CreateNull() : new JObject { ["item"] = c.itemName, ["amount"] = c.amount, ["paid"] = c.paid,
                    ["price_per_unit"] = c.pricePerUnit.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                    ["colors"] = Colors(c.customColors), ["nested"] = NestedCargo(c.nestedCargoInstances) });
            }
            return rows;
        }
        private static JToken NestedCargo(IEnumerable<NestedCargoInstance> nested)
        {
            if (nested == null) return JValue.CreateNull();
            var rows = new JArray();
            foreach (var c in nested)
            {
                Require(rows.Count < 512, "e5b_nested_cargo_bound_exceeded");
                rows.Add(c == null ? JValue.CreateNull() : new JObject { ["item"] = c.itemName, ["amount"] = c.amount,
                    ["price_per_unit"] = c.pricePerUnit.ToString("R", System.Globalization.CultureInfo.InvariantCulture), ["colors"] = Colors(c.customColors) });
            }
            return rows;
        }
        private static object DataField(object value, string name)
        {
            string key = value.GetType().AssemblyQualifiedName + "::" + name;
            if (!dtoNamedFields.TryGetValue(key, out var field))
            {
                field = value.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
                Require(field != null, "e5b_raw_data_field_contract_missing_" + name);
                dtoNamedFields.Add(key, field);
            }
            return field.GetValue(value);
        }
        private static void Contract(Type type, params string[] names)
        {
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly).OrderBy(f => f.Name, StringComparer.Ordinal).ToArray();
            Require(fields.Select(f => f.Name).SequenceEqual(names.OrderBy(n => n, StringComparer.Ordinal)), "e5b_native_dto_field_contract_mismatch_" + type.Name);
            dtoFields[type] = fields;
            foreach (var field in fields) dtoNamedFields[type.AssemblyQualifiedName + "::" + field.Name] = field;
        }
        private static void PrepareDtos()
        {
            dtoFields.Clear(); dtoNamedFields.Clear();
            var timestamp = typeof(Order).GetField("timestamp").FieldType;
            Require(timestamp.FullName == "BigAmbitions.DayNightCycle.Timestamp", "e5b_timestamp_type_mismatch");
            Contract(timestamp, "Day", "Hour", "Minute");
            var history = typeof(BuildingRegistration).GetField("orderHistory").FieldType.GetGenericArguments()[0];
            Require(history.FullName == "Entities.OrderHistoryEntry", "e5b_history_type_mismatch");
            Contract(history, "dayNumber", "totalCustomers", "totalRevenue", "itemSales", "hourReports");
            var item = history.GetField("itemSales").FieldType.GetGenericArguments()[0];
            var hour = history.GetField("hourReports").FieldType.GetGenericArguments()[0];
            Contract(item, "itemName", "amountSold", "totalPrice", "totalWholesalePrice", "itemSoldBreakdownEntries");
            Contract(hour, "hour", "customers");
            Contract(item.GetField("itemSoldBreakdownEntries").FieldType.GetElementType(), "price", "amount");
            var color = typeof(CargoInstance).GetField("customColors").FieldType.GetGenericArguments()[0];
            Require(color.Name == "CustomColor", "e5b_color_type_mismatch"); Contract(color, "channel", "color");
            var serial = color.GetField("color").FieldType;
            Require(serial.Name == "SerializableColor" && serial.GetField("color").FieldType == typeof(int), "e5b_serializable_color_contract_mismatch");
            Contract(serial, "color");
        }
        private static JToken Colors(IEnumerable colors)
        {
            if (colors == null) return JValue.CreateNull();
            var rows = new JArray();
            foreach (var color in colors)
            {
                Require(rows.Count < 128, "e5b_color_bound_exceeded");
                if (color == null) { rows.Add(JValue.CreateNull()); continue; }
                Require(color.GetType().Name == "CustomColor", "e5b_custom_color_type_mismatch");
                var channel = DataField(color, "channel"); var serial = DataField(color, "color");
                rows.Add(new JObject { ["channel"] = Convert.ToInt64(channel), ["channel_name"] = channel.ToString(),
                    ["color"] = serial == null ? JValue.CreateNull() : Json(DataField(serial, "color")) });
            }
            return rows;
        }
        private static JToken RawData(object value, int depth)
        {
            if (value == null) return JValue.CreateNull();
            Require(depth <= 8, "e5b_raw_data_depth_exceeded");
            var type = value.GetType();
            if (type.IsEnum) return new JObject { ["value"] = Convert.ToInt64(value), ["name"] = value.ToString() };
            if (value is string text) { Require(text.Length <= 4096, "e5b_raw_string_bound_exceeded"); return new JValue(text); }
            if (value is float single) return NativeFloat(single);
            if (type.IsPrimitive || value is decimal) return new JValue(value);
            if (value is IList list)
            {
                Require(list.Count <= 1024, "e5b_raw_list_bound_exceeded");
                var rows = new JArray(); foreach (var row in list) rows.Add(RawData(row, depth + 1)); return rows;
            }
            Require(dtoFields.TryGetValue(type, out var fields), "e5b_unreviewed_raw_data_type");
            var result = new JObject { ["native_type"] = type.FullName, ["reader"] = "instance fields only; no public/native getters" };
            foreach (var field in fields) result[field.Name] = RawData(field.GetValue(value), depth + 1);
            return result;
        }
        private static JArray PriceRows(BuildingRegistration reg)
        {
            Require(reg.retailPrices.Count <= 256, "e5b_price_bound_exceeded");
            return new JArray(reg.retailPrices.Select(p => p == null ? (JToken)JValue.CreateNull() : new JObject { ["item"] = p.itemName,
                ["price"] = p.price.ToString("R", System.Globalization.CultureInfo.InvariantCulture) }));
        }
        private static JToken OrderDto(Order order)
        {
            if (order == null) return JValue.CreateNull();
            Require(order.entries == null || order.entries.Count <= 128, "e5b_order_entry_bound_exceeded");
            var identity = orderIdentities.GetValue(order, _ => new OrderIdentity { Id = ++nextOrderIdentity });
            var entries = order.entries == null ? null : new JArray(order.entries.Select(e => e == null ? (JToken)JValue.CreateNull() : new JObject {
                ["item"] = e.itemName, ["price"] = e.price.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                ["available"] = e.available, ["priceAccceptable"] = e.priceAccceptable, ["paid"] = e.paid, ["processed"] = e.processed,
                ["wholesale_price"] = e.wholesalePrice.ToString("R", System.Globalization.CultureInfo.InvariantCulture) }));
            return new JObject { ["session_order_id"] = identity.Id, ["entries"] = entries ?? (JToken)JValue.CreateNull(),
                ["timestamp"] = RawData(order.timestamp, 0), ["completed"] = order.completed,
                ["customer_service_skill"] = NativeFloat(order.customerServiceSkill), ["cleanliness"] = NativeFloat(order.cleanliness),
                ["customer_demand_types"] = RawData(order.customerDemandTypes, 0), ["customer_demand_score"] = NativeFloat(order.customerDemandScore) };
        }
        private static string Fingerprint(Fault fault, IList list)
        {
            JToken value = fault.Kind == "fault-price" ? (JToken)PriceRows(fault.Target.Registration) : Cargo(list.Cast<CargoInstance>(), 0);
            return value.ToString(Formatting.None);
        }
        private static JObject NativeInvariant()
        {
            var shops = new JArray();
            foreach (var target in targets)
            {
                var reg = target.Registration;
                Require(reg.itemInstances.Count <= 2000 && reg.unprocessedCompletedOrders.Count <= 100000, "e5b_invariant_bound_exceeded");
                var holders = new JArray(reg.itemInstances.Values.OrderBy(i => i.id, StringComparer.Ordinal).Select(i => new JObject {
                    ["id"] = i.id, ["item"] = i.itemName, ["cargo"] = Cargo(i.cargoInstances, 0) }));
                shops.Add(new JObject { ["id"] = target.NativeId, ["holders"] = holders, ["orders"] = new JArray(reg.unprocessedCompletedOrders.Select(OrderDto)) });
            }
            var receipts = new JArray(SaveGameManager.Current.modData.Where(p => FoodSaleReceipts.IsReceiptKey(p.Key)).OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new JObject { ["key"] = p.Key, ["value"] = p.Value }));
            var payload = new JObject { ["shops"] = shops, ["receipts"] = receipts,
                ["money"] = SaveGameManager.Current.Money.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                ["selected_live_meals"] = simulation?.State.Food == null ? JValue.CreateNull() : Json(simulation.State.Food.Residents.Where(m => targets.Any(t => t.Place == m.PlaceId)).ToArray()) };
            string digest;
            using (var hash = SHA256.Create()) digest = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(payload.ToString(Formatting.None)))).Replace("-", "").ToLowerInvariant();
            return new JObject { ["digest"] = digest, ["money"] = payload["money"].DeepClone(), ["receipt_count"] = receipts.Count,
                ["order_counts"] = new JArray(targets.Select(t => new JObject { ["id"] = t.NativeId, ["orders"] = t.Registration.unprocessedCompletedOrders.Count })),
                ["scope"] = "selected holder identity/persisted cargo DTO fields, native completed-order DTOs, city-wide saved receipts, native bank and selected live meal facts; excludes native asset caches, expected PC authority/cache changes and supply counters" };
        }
        private static JToken InvokeProduction(string action, JObject args)
        {
            Require(market != null && ReferenceEquals(market, readMarket(null)), "e5b_actual_market_unavailable_or_changed_rearm");
            double minute = SaveGameManager.Current.Day * 1440.0 + SaveGameManager.Current.Hour * 60.0 + SaveGameManager.Current.Minute;
            Target target = action == "read" ? null : Selected((string)args["id"]);
            ResidentMeal paid = null;
            if (action == "replay-paid")
            {
                Require(simulation?.State.Food != null, "e5b_live_population_unavailable");
                paid = simulation.State.Food.Residents.SingleOrDefault(m => m.PlaceId == target.Place && m.ResidentId == (string)args["resident_id"] && m.IntentId == (string)args["intent_id"]);
                Require(paid != null && paid.TargetKind == FoodAlternativeKind.Player
                    && FoodSaleReceipts.ReadStatus(SaveGameManager.Current.modData, paid) == FoodReceiptStatus.Paid,
                    "e5b_preexisting_selected_live_paid_meal_and_exact_saved_receipt_required");
            }
            var before = NativeInvariant(); long start = Stopwatch.GetTimestamp(); JToken result;
            try
            {
                if (action == "read")
                {
                    var businesses = market.Read(minute);
                    Require(businesses.Count <= 64, "e5b_production_read_result_bound_exceeded");
                    result = new JObject { ["businesses"] = new JArray(businesses.Select(b => new JObject { ["id"] = b.Id, ["place"] = b.PlaceId })), ["count"] = businesses.Count };
                }
                else if (action == "capture")
                {
                    Require(authorities.TryGetValue(target.Place, out var row), "e5b_actual_authority_record_unavailable");
                    var business = market.CaptureBusiness(row.BusinessId, minute);
                    result = new JObject { ["captured"] = business != null, ["business"] = Json(business) };
                }
                else result = Json(market.Buy(paid, minute));
            }
            catch (Exception e)
            {
                result = new JObject { ["refusal"] = e is InvalidOperationException && e.Message.StartsWith("e5b_", StringComparison.Ordinal) ? e.Message : "e5b_production_" + e.GetType().Name };
            }
            double elapsed = (Stopwatch.GetTimestamp() - start) * ToMs;
            var after = NativeInvariant(); after["native_unchanged"] = (string)before["digest"] == (string)after["digest"];
            after["production_result"] = result; after["diagnostic_elapsed_ms"] = elapsed;
            after["invocation"] = action; after["provenance"] = "explicit diagnostic invocation of the actual production ownership market; not an automatic resident decision or ordinary-play timing window";
            return Wrap(before, after);
        }
        private static JToken Inject(string kind, JObject args)
        {
            Require(!measuring && Paused(), "e5b_fault_requires_paused_nonmeasurement_checkpoint");
            var target = Selected((string)args["id"]); var reg = target.Registration;
            Require(faults.Count < 24 && faults.All(f => f.Target != target || f.Kind != kind), "e5b_one_fault_of_each_kind_per_target");
            var before = Snapshot(new JObject { ["id"] = target.NativeId });
            var fault = new Fault { Id = Guid.NewGuid().ToString("N"), Kind = kind, Target = target };
            if (kind == "fault-close")
            {
                Require(args["closed"]?.Type == JTokenType.Boolean, "e5b_explicit_closed_boolean_required");
                fault.Closed = reg.temporarilyClosed; fault.AppliedClosed = (bool)args["closed"];
                faults.Add(fault); reg.temporarilyClosed = fault.AppliedClosed;
            }
            else
            {
                IList list;
                if (kind == "fault-price")
                {
                    string product = (string)args["product"];
                    Require(product != null && reg.cachedAvailableProducts.Contains(product), "e5b_actual_advertised_product_required");
                    list = reg.retailPrices;
                    var price = reg.retailPrices.Single(p => p.itemName == product);
                    fault.OriginalRows = list.Cast<object>().ToArray(); fault.OriginalJson = Fingerprint(fault, list);
                    fault.List = list; fault.OriginalList = list; faults.Add(fault); list.Remove(price);
                }
                else
                {
                    string holderId = (string)args["holder_id"];
                    var holder = reg.itemInstances.Values.Single(i => i.id == holderId);
                    Require(holder.cargoInstances != null && holder.cargoInstances.Count == 1, "e5b_single_actual_cargo_required");
                    var cargo = holder.cargoInstances[0];
                    Require(cargo != null && cargo.amount >= 0
                        && (cargo.nestedCargoInstances == null || cargo.nestedCargoInstances.Count == 0)
                        && reg.cachedAvailableProducts.Contains(cargo.itemName), "e5b_actual_advertised_unsealed_product_cargo_required");
                    Require(holder.ItemCached.producerSettings != null && holder.ItemCached.producerSettings.ResourcesRequired, "e5b_real_consuming_producer_required");
                    // Clone only a real descriptor into its existing holder; zero quantity adds no stock.
                    // This deliberately invalid count is negative-test state, never natural depletion evidence.
                    list = holder.cargoInstances; fault.OriginalRows = list.Cast<object>().ToArray(); fault.OriginalJson = Fingerprint(fault, list);
                    var duplicate = cargo.Copy(); duplicate.amount = 0;
                    fault.Holder = holder; fault.List = list; fault.OriginalList = list; faults.Add(fault); list.Add(duplicate);
                }
                fault.List = list; fault.OriginalList = list; fault.ExpectedRows = list.Cast<object>().ToArray(); fault.ExpectedJson = Fingerprint(fault, list);
            }
            if (fault.OriginalJson != null) before["original_list"] = JToken.Parse(fault.OriginalJson);
            return Wrap(before, new JObject { ["fault_id"] = fault.Id, ["provenance"] = "injected negative contract state; never natural native cargo or positive eligibility evidence",
                ["kind"] = kind, ["state"] = Snapshot(new JObject { ["id"] = target.NativeId }), ["negative_list"] = fault.List == null ? JValue.CreateNull() : JToken.Parse(fault.ExpectedJson) });
        }
        private static JToken Restore(string id)
        {
            Require(!measuring && Paused(), "e5b_restore_requires_paused_nonmeasurement_checkpoint");
            var fault = faults.Single(f => f.Id == id); var reg = fault.Target.Registration;
            Require(ReferenceEquals(save, SaveGameManager.Current) && SaveGameManager.Current.BuildingRegistrations.Any(r => ReferenceEquals(r, reg)), "e5b_fault_registration_replaced");
            var before = Snapshot(new JObject { ["id"] = fault.Target.NativeId });
            if (fault.Kind == "fault-close")
            { Require(reg.temporarilyClosed == fault.AppliedClosed, "e5b_fault_closure_drift_refuse_restore"); reg.temporarilyClosed = fault.Closed; }
            else
            {
                Require(fault.ExpectedRows != null && ReferenceEquals(fault.Kind == "fault-price" ? (object)reg.retailPrices : fault.Holder.cargoInstances, fault.OriginalList)
                    && (fault.Holder == null || reg.itemInstances.Values.Any(i => ReferenceEquals(i, fault.Holder)))
                    && fault.List.Count == fault.ExpectedRows.Length
                    && fault.List.Cast<object>().Select((r, i) => ReferenceEquals(r, fault.ExpectedRows[i])).All(v => v)
                    && Fingerprint(fault, fault.List) == fault.ExpectedJson, "e5b_fault_list_drift_refuse_restore");
                fault.List.Clear(); foreach (var row in fault.OriginalRows) fault.List.Add(row);
                Require(Fingerprint(fault, fault.List) == fault.OriginalJson, "e5b_exact_restore_failed");
            }
            faults.Remove(fault);
            return Wrap(before, new JObject { ["restored_exact_original_fields"] = true, ["fault_id"] = id,
                ["provenance"] = "restored injected negative contract state", ["state"] = Snapshot(new JObject { ["id"] = fault.Target.NativeId }) });
        }
    }
}
