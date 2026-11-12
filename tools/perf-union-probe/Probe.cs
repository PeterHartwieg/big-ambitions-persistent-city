using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using BAModAPI;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UI;
using UI.Load;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[assembly: RegisterModClass(typeof(PerfUnionProbe.Entry))]
namespace PerfUnionProbe
{
    [ModEntryOnCityLoad]
    public sealed class Entry : IModBigAmbitions
    {
        private GameObject host;
        public string[] RelativeAssetBundlePaths => Array.Empty<string>();
        public Task OnLoadAsync(ModContext context)
        {
            if (Session.Isolated()) { host = new GameObject("F4b perf union probe"); host.AddComponent<Session>(); }
            return Task.CompletedTask;
        }
        public Task OnUnloadAsync() { Session.Disarm(); if (host != null) Object.Destroy(host); return Task.CompletedTask; }
    }

    public static class PerfUnionRigOps
    {
        public static JToken Op_perf_union(JObject args) => Session.Dispatch(args);
    }

    public struct PerfUnionFrameStart { }

    public sealed class Session : MonoBehaviour
    {
        // Deliberately outside the persistentcity owner namespaces, so the inventory never sees the probe.
        internal const string PatchId = "f4b.perf-union-probe";
        internal const int FrameLimit = ProbeCapacity.Frames, TickLimit = ProbeCapacity.Ticks, CallLimit = ProbeCapacity.Calls,
            DiagnosticLimit = ProbeCapacity.Diagnostics, ReplyLimit = 48000;
        internal static readonly BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        internal static readonly double ToMs = 1000.0 / Stopwatch.Frequency;
        private static readonly Harmony patches = new Harmony(PatchId);

        private sealed class Binding
        {
            public ExpectedScope Expected; public string Label, Kind, Status, Method, Target, Owner;
            public int Scope = -1, Component = -1;
        }
        private struct FrameRow
        {
            public int frame, skipped, gc, saves;
            public long qpc;
            public double modWorkMs, wholeFrameMs, observerMs, boundaryMs, nativeMinute, logicalMinute;
            public bool diagnostic, scene, loading, lateBoundary, crossFrame, partial;
        }
        private struct Sample { public int frame; public long qpc; public JObject accounting, saveEpoch; }

        internal static volatile bool armed;
        internal static bool measuring;
        private static string mode = "none", error = "", stopReason = "not-started", window = "", boundarySource = "none";
        private static object save;
        private static int armThread, offMainCallbacks;
        private static Assembly pc;
        private static Object runtime;
        private static readonly List<Binding> bindings = new List<Binding>();
        internal static readonly Dictionary<MethodBase, int> scopeIndex = new Dictionary<MethodBase, int>();
        internal static readonly Dictionary<MethodBase, int> componentIndex = new Dictionary<MethodBase, int>();
        internal static readonly Dictionary<MethodBase, int> scopeFast = new Dictionary<MethodBase, int>(MethodIdentity.Instance);
        internal static readonly Dictionary<MethodBase, int> componentFast = new Dictionary<MethodBase, int>(MethodIdentity.Instance);
        private static string[] componentNames = Array.Empty<string>();
        internal static UnionClock union = new UnionClock(0, 0, Stopwatch.Frequency);
        internal static readonly HoldTracker holds = new HoldTracker();
        private static bool loopInstalled, eventsSubscribed, holdsBound, advanceBound, patchSetChanged;
        private static CoverageTracker coverage;
        private static int unexpectedAtWindowStart;

        private static readonly FrameRow[] frames = new FrameRow[FrameLimit];
        private static float[] frameComponents = Array.Empty<float>();
        private static readonly RingCursor frameRing = new RingCursor(FrameLimit);
        private static int currentFrame = -1, firstMeasureFrame = -1;
        private static long frameStartQpc, nextDiagnosticQpc;
        internal static long observerTicks;
        private static long boundaryTicks;
        private static int gcAtFrameStart, frameSaves, totalSaves, totalSceneEvents;
        private static bool frameDiagnostic, frameScene, frameLoading, frameLate, pendingWindowStart;
        private static double frameNative, frameLogical;
        internal static int crossFrame, timedExceptions;
        internal static long unknownScopeCalls;

        internal static readonly TickRecord[] ticks = new TickRecord[TickLimit];
        internal static readonly RingCursor tickRing = new RingCursor(TickLimit);
        internal static readonly AdvanceCallRecord[] calls = new AdvanceCallRecord[CallLimit];
        internal static int callCount;
        internal static long callDropped, advanceCalls;
        private static readonly Sample[] samples = new Sample[DiagnosticLimit];
        private static readonly RingCursor sampleRing = new RingCursor(DiagnosticLimit);
        private static long sampleFailures;

        internal static Func<long> heldOrdinary, heldSkip, boundaryClips;
        internal static Func<double> withheld;
        internal static Func<bool> accountingLimited;
        private static Func<double> clockFactor;
        private static Func<object, object> readSimulation, readState, readStopEnvironment;
        private static Func<object, double> readMinute, readSeam;
        private static PropertyInfo accountingDiagnostics, saveEpochDiagnostics;

        internal static double shimFactor = double.NaN;
        internal static long shimTicks, shimMultiplied;
        private static bool shimInstalled;

        public static bool Isolated() => Environment.GetCommandLineArgs().Contains("-agent-tests") && Environment.GetCommandLineArgs().Contains("-rig-test")
            && Application.productName == "Big Ambitions Agent Tests"
            && string.Equals(Application.persistentDataPath.Replace('\\', '/'),
                (Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/AppData/LocalLow/Hovgaard Games/Big Ambitions Agent Tests").Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
        private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
        private static bool FtCity() => SaveGameManager.Current?.SaveGameName?.StartsWith("FT", StringComparison.Ordinal) == true;
        private static void Guard()
        {
            Require(Isolated(), "f4b_not_isolated");
            Require(FtCity(), "f4b_ft_city_required");
            Require(Object.FindObjectOfType<GameManager>() != null, "f4b_city_required");
            if (armed) Require(ReferenceEquals(save, SaveGameManager.Current), "f4b_save_changed_rearm_required");
        }
        internal static bool MainThread()
        {
            if (Thread.CurrentThread.ManagedThreadId == armThread) return true;
            Interlocked.Increment(ref offMainCallbacks);
            return false;
        }
        private static JObject Wrap(JToken before, JToken after)
        {
            var result = new JObject { ["before"] = before, ["after"] = after };
            Require(result.ToString(Formatting.None).Length <= ReplyLimit, "f4b_reply_bound_reduce_limit");
            return result;
        }
        private static int Number(JObject args, string name, int fallback, int max)
        { int n = (int?)args[name] ?? fallback; Require(n >= 0 && n <= max, "f4b_invalid_" + name); return n; }

        public static JToken Dispatch(JObject args)
        {
            Require(!armed || Thread.CurrentThread.ManagedThreadId == armThread, "f4b_dispatch_thread_changed");
            try
            {
                Guard();
                foreach (var p in args.Properties())
                    Require(new[] { "action", "mode", "factor", "expected_scope_hash", "window", "section", "offset", "limit" }.Contains(p.Name), "f4b_unknown_argument");
                string action = (string)args["action"];
                if (measuring) { TouchFrame(Stopwatch.GetTimestamp()); frameDiagnostic = true; }
                if (action == "inventory") return Wrap(Status(), Inventory());
                if (action == "status") return Wrap(Status(), Status());
                if (action == "arm") { var before = Status(); Arm(args); return Wrap(before, Status()); }
                Require(armed, "f4b_not_armed");
                if (action == "measure-start") { var before = Status(); StartMeasurement(args); return Wrap(before, Status()); }
                if (action == "measure-stop") { var before = Status(); StopMeasurement("explicit-stop"); return Wrap(before, Status()); }
                if (action == "page") return Wrap(Status(), Page(args));
                if (action == "heap") return Wrap(Status(), Heap());
                if (action == "disarm") { var before = Status(); Disarm(); stopReason = "disarmed"; return Wrap(before, Status()); }
                return Wrap(Status(), new JObject { ["supported"] = false, ["refusal"] = "f4b_unsupported_action", ["action"] = action });
            }
            catch (Exception e)
            {
                var code = e is InvalidOperationException && e.Message.StartsWith("f4b_", StringComparison.Ordinal) ? e.Message : "f4b_" + e.GetType().Name;
                return new JObject { ["before"] = JValue.CreateNull(), ["after"] = new JObject { ["supported"] = false, ["refusal"] = code } };
            }
        }

        private static JObject Inventory() => new JObject
        {
            ["version"] = ScopeInventory.Version, ["expected_scope_hash"] = ScopeInventory.Hash(),
            ["owners"] = new JArray(ScopeInventory.Owners),
            ["counts"] = new JObject { ["patches"] = ScopeInventory.Patches.Count, ["messages"] = ScopeInventory.Messages.Count,
                ["events"] = ScopeInventory.Events.Count, ["transpiler_helpers"] = ScopeInventory.TranspilerHelpers.Count,
                ["components"] = ScopeInventory.Components.Count, ["call_sites"] = ScopeInventory.CallSites.Count },
        };

        private static void Arm(JObject args)
        {
            Require(!measuring, "f4b_stop_before_arm");
            Require(!LoadScene.isLoading, "f4b_native_load_in_progress");
            Require((string)args["expected_scope_hash"] == ScopeInventory.Hash(), "f4b_expected_scope_hash_mismatch");
            string requested = (string)args["mode"];
            Require(requested == "candidate" || requested == "reference", "f4b_mode_candidate_or_reference_required");
            Disarm();
            armThread = Thread.CurrentThread.ManagedThreadId; offMainCallbacks = 0; error = "";
            save = SaveGameManager.Current;
            pc = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "PersistentCity");
            var foreignOwners = ScopeInventory.Owners.Where(Harmony.HasAnyPatches).ToArray();
            try
            {
                if (requested == "reference")
                {
                    Require(pc == null && foreignOwners.Length == 0, "f4b_reference_requires_persistent_city_absent");
                    double factor = (double?)args["factor"] ?? double.NaN;
                    Require(PaceShim.ValidFactor(factor), "f4b_reference_factor_0_1_to_1_required");
                    shimFactor = factor; shimTicks = shimMultiplied = 0;
                    var tick = AccessTools.Method(typeof(GameManager), nameof(GameManager.RunMainGameTick), new[] { typeof(float) });
                    Require(tick != null, "f4b_native_tick_missing");
                    patches.Patch(tick, prefix: new HarmonyMethod(typeof(ShimPatch), nameof(ShimPatch.Prefix)) { priority = Priority.First + 1 });
                    shimInstalled = true;
                    union = new UnionClock(0, 0, Stopwatch.Frequency); componentNames = Array.Empty<string>();
                    coverage = null;
                }
                else
                {
                    Require(args["factor"] == null, "f4b_candidate_factor_is_read_from_persistent_city");
                    Require(pc != null, "f4b_candidate_requires_persistent_city");
                    var runtimeType = pc.GetType("PersistentCity.Runtime.PopulationRuntime", false);
                    Require(runtimeType != null, "f4b_population_runtime_type_missing");
                    runtime = Object.FindObjectOfType(runtimeType);
                    Require(runtime != null, "f4b_population_runtime_absent");
                    BindReads(runtimeType);
                    BindInventory();
                }
                InstallLoop();
                SceneManager.sceneLoaded += SceneLoaded; SceneManager.sceneUnloaded += SceneUnloaded;
                GlobalEvents.onSaveGame += SaveObserved; eventsSubscribed = true;
                mode = requested; armed = true; stopReason = "armed";
            }
            catch { Disarm(); throw; }
        }

        private static void BindReads(Type runtimeType)
        {
            var clock = pc.GetType("PersistentCity.Runtime.NativeCityClock", false);
            Require(clock != null, "f4b_native_city_clock_missing");
            heldOrdinary = Static<long>(clock, "HeldOrdinaryTicks"); heldSkip = Static<long>(clock, "HeldEconomySkipTicks");
            boundaryClips = Static<long>(clock, "BoundaryClips"); withheld = Static<double>(clock, "WithheldMinutes");
            accountingLimited = Static<bool>(clock, "AccountingLimited"); clockFactor = Static<double>(clock, "Factor");
            var simulation = runtimeType.GetField("simulation", All);
            Require(simulation != null, "f4b_runtime_simulation_field_missing");
            readSimulation = ObjectGetter(simulation);
            var state = simulation.FieldType.GetProperty("State", BindingFlags.Public | BindingFlags.Instance);
            Require(state != null, "f4b_simulation_state_missing");
            readState = ObjectGetter(state);
            var minute = state.PropertyType.GetProperty("Minute", BindingFlags.Public | BindingFlags.Instance);
            Require(minute != null && minute.PropertyType == typeof(double), "f4b_city_minute_missing");
            readMinute = DoubleGetter(minute);
            var stops = runtimeType.GetField("stopEnvironment", All);
            var seam = stops?.FieldType.GetProperty("SeamMilliseconds", BindingFlags.Public | BindingFlags.Instance);
            if (stops != null && seam != null && seam.PropertyType == typeof(double)) { readStopEnvironment = ObjectGetter(stops); readSeam = DoubleGetter(seam); }
            accountingDiagnostics = runtimeType.GetProperty("AccountingDiagnostics", BindingFlags.Public | BindingFlags.Instance);
            saveEpochDiagnostics = runtimeType.GetProperty("SaveEpochDiagnostics", BindingFlags.Public | BindingFlags.Instance);
        }

        private static void BindInventory()
        {
            bindings.Clear(); scopeIndex.Clear(); componentIndex.Clear(); scopeFast.Clear(); componentFast.Clear();
            var timed = new List<KeyValuePair<MethodBase, Binding>>();
            foreach (var exp in ScopeInventory.Patches)
            {
                var row = new Binding { Expected = exp, Label = exp.Label, Kind = exp.Kind.ToString(), Owner = exp.Owner, Target = exp.Target, Method = exp.Type + "." + exp.Method };
                if (exp.Kind == ScopeKind.Transpiler) row.Status = "untimed-transpiled";
                else
                {
                    var method = Find(exp, null);
                    row.Status = method == null ? "unbindable" : "timed";
                    if (method != null) timed.Add(new KeyValuePair<MethodBase, Binding>(method, row));
                }
                bindings.Add(row);
            }
            foreach (var exp in ScopeInventory.Messages.Concat(ScopeInventory.Events).Concat(ScopeInventory.TranspilerHelpers))
            {
                var method = Find(exp, null);
                var row = new Binding { Expected = exp, Label = exp.Label, Kind = exp.Kind.ToString(), Owner = exp.Owner, Target = exp.Target,
                    Method = exp.Type + "." + exp.Method, Status = method == null ? "unbindable" : "timed" };
                if (method != null) timed.Add(new KeyValuePair<MethodBase, Binding>(method, row));
                bindings.Add(row);
            }
            int scopes = 0;
            foreach (var pair in timed)
            {
                if (!scopeIndex.TryGetValue(pair.Key, out int index)) { index = scopes++; scopeIndex.Add(pair.Key, index); }
                pair.Value.Scope = index;
            }
            var components = ScopeInventory.Components.ToArray();
            componentNames = components.Select(c => c.Label).ToArray();
            union = new UnionClock(scopes, components.Length, Stopwatch.Frequency);
            frameComponents = new float[FrameLimit * components.Length];
            foreach (var pair in scopeIndex)
            {
                try
                {
                    patches.Patch(pair.Key, prefix: new HarmonyMethod(typeof(ScopePatch), nameof(ScopePatch.Before)) { priority = Priority.First },
                        finalizer: new HarmonyMethod(typeof(ScopePatch), nameof(ScopePatch.After)) { priority = Priority.Last });
                }
                catch { foreach (var row in bindings.Where(b => b.Scope == pair.Value)) row.Status = "unbindable"; }
            }
            coverage = new CoverageTracker(ScopeInventory.Patches, exp => bindings.Any(b => ReferenceEquals(b.Expected, exp) && b.Status == "timed"), PatchId);
            ScanPatches();
            for (int i = 0; i < components.Length; i++)
            {
                var method = Find(components[i], components[i].Target);
                var row = new Binding { Expected = components[i], Label = components[i].Label, Kind = "Component", Method = components[i].Type + "." + components[i].Method,
                    Target = components[i].Target, Component = i, Status = method == null ? "component-unbound" : "component" };
                if (method != null)
                {
                    try
                    {
                        patches.Patch(method, prefix: new HarmonyMethod(typeof(ComponentPatch), nameof(ComponentPatch.Before)) { priority = Priority.First },
                            finalizer: new HarmonyMethod(typeof(ComponentPatch), nameof(ComponentPatch.After)) { priority = Priority.Last });
                        componentIndex.Add(method, i);
                    }
                    catch { row.Status = "component-unbound"; }
                    if (components[i].Method == "AdvancePopulation" && row.Status == "component")
                    {
                        try
                        {
                            patches.Patch(method, prefix: new HarmonyMethod(typeof(AdvancePatch), nameof(AdvancePatch.Before)),
                                finalizer: new HarmonyMethod(typeof(AdvancePatch), nameof(AdvancePatch.After)));
                            advanceBound = true;
                        }
                        catch { advanceBound = false; }
                    }
                }
                bindings.Add(row);
            }
            var accounting = Find(ScopeInventory.Patches.Single(p => p.Method == "AccountingPrefix"), null);
            if (accounting != null)
            {
                try
                {
                    patches.Patch(accounting, prefix: new HarmonyMethod(typeof(HoldPatch), nameof(HoldPatch.Before)) { priority = Priority.Last },
                        postfix: new HarmonyMethod(typeof(HoldPatch), nameof(HoldPatch.After)) { priority = Priority.First });
                    holdsBound = true;
                }
                catch { holdsBound = false; }
            }
        }

        private static void ScanPatches()
        {
            if (coverage == null) return;
            var observed = new List<ObservedPatch>();
            foreach (var target in Harmony.GetAllPatchedMethods().ToArray()) HarmonyScan.Observe(target, Harmony.GetPatchInfo(target), pc, observed);
            coverage.Observe(observed);
        }

        private static bool CoverageComplete() => coverage != null && coverage.Complete && bindings.All(b => b.Status != "unbindable");

        private static string Name(MethodBase method) => method.DeclaringType?.FullName + "." + method.Name;
        private static MethodBase Find(ExpectedScope exp, string parameters)
        {
            var type = pc.GetType(exp.Type, false);
            if (type == null) return null;
            var found = type.GetMethods(All).Where(m => m.Name == exp.Method).ToArray();
            if (parameters != null)
                found = found.Where(m => string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name)) == parameters).ToArray();
            return found.Length == 1 && !found[0].IsAbstract && !found[0].IsGenericMethodDefinition ? found[0] : null;
        }

        private static Func<T> Static<T>(Type type, string property)
        {
            var getter = type.GetProperty(property, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetGetMethod(true);
            Require(getter != null && getter.ReturnType == typeof(T), "f4b_clock_contract_" + property);
            return (Func<T>)getter.CreateDelegate(typeof(Func<T>));
        }
        private static Func<object, object> ObjectGetter(MemberInfo member)
        {
            var method = new DynamicMethod("F4bRead_" + member.Name, typeof(object), new[] { typeof(object) }, typeof(Session).Module, true);
            var il = method.GetILGenerator();
            Type value;
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Castclass, member.DeclaringType);
            if (member is FieldInfo field) { il.Emit(OpCodes.Ldfld, field); value = field.FieldType; }
            else { var property = (PropertyInfo)member; il.Emit(OpCodes.Callvirt, property.GetGetMethod(true)); value = property.PropertyType; }
            if (value.IsValueType) il.Emit(OpCodes.Box, value);
            il.Emit(OpCodes.Ret);
            return (Func<object, object>)method.CreateDelegate(typeof(Func<object, object>));
        }
        private static Func<object, double> DoubleGetter(PropertyInfo property)
        {
            var method = new DynamicMethod("F4bReadDouble_" + property.Name, typeof(double), new[] { typeof(object) }, typeof(Session).Module, true);
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Castclass, property.DeclaringType);
            il.Emit(OpCodes.Callvirt, property.GetGetMethod(true)); il.Emit(OpCodes.Ret);
            return (Func<object, double>)method.CreateDelegate(typeof(Func<object, double>));
        }

        internal static double NativeMinute()
        {
            var current = SaveGameManager.Current;
            return current == null ? double.NaN : current.Day * 1440.0 + current.Hour * 60.0 + current.Minute;
        }
        internal static double LogicalMinute()
        {
            if (readSimulation == null || runtime == null) return double.NaN;
            var simulation = readSimulation(runtime);
            var state = simulation == null ? null : readState(simulation);
            return state == null ? double.NaN : readMinute(state);
        }
        internal static double SeamMilliseconds()
        {
            if (readSeam == null || runtime == null) return double.NaN;
            var stops = readStopEnvironment(runtime);
            return stops == null ? double.NaN : readSeam(stops);
        }

        private static void InstallLoop()
        {
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            var systems = loop.subSystemList;
            for (int i = 0; systems != null && i < systems.Length; i++)
            {
                if (systems[i].type != typeof(UnityEngine.PlayerLoop.EarlyUpdate)) continue;
                var children = systems[i].subSystemList ?? Array.Empty<PlayerLoopSystem>();
                var start = new PlayerLoopSystem { type = typeof(PerfUnionFrameStart), updateDelegate = FrameStart };
                systems[i].subSystemList = new[] { start }.Concat(children.Where(c => c.type != typeof(PerfUnionFrameStart))).ToArray();
                PlayerLoop.SetPlayerLoop(loop);
                loopInstalled = true; boundarySource = "player-loop-early-update";
                return;
            }
            boundarySource = "first-callback";
        }
        private static void RemoveLoop()
        {
            if (!loopInstalled) return;
            loopInstalled = false;
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            var systems = loop.subSystemList;
            for (int i = 0; systems != null && i < systems.Length; i++)
                if (systems[i].subSystemList != null && systems[i].subSystemList.Any(c => c.type == typeof(PerfUnionFrameStart)))
                    systems[i].subSystemList = systems[i].subSystemList.Where(c => c.type != typeof(PerfUnionFrameStart)).ToArray();
            PlayerLoop.SetPlayerLoop(loop);
        }

        private static bool LoopEntryPresent()
        {
            var systems = PlayerLoop.GetCurrentPlayerLoop().subSystemList;
            return systems != null && systems.Any(s => s.subSystemList != null && s.subSystemList.Any(c => c.type == typeof(PerfUnionFrameStart)));
        }

        private static void SaveObserved() { if (armed) { frameSaves++; totalSaves++; } }
        private static void SceneLoaded(Scene scene, LoadSceneMode loadMode) { if (armed) { frameScene = true; totalSceneEvents++; } }
        private static void SceneUnloaded(Scene scene) { if (armed) { frameScene = true; totalSceneEvents++; } }

        private static void FrameStart()
        {
            if (!armed) return;
            long now = Stopwatch.GetTimestamp();
            if (Thread.CurrentThread.ManagedThreadId != armThread) { Interlocked.Increment(ref offMainCallbacks); return; }
            if (!ReferenceEquals(save, SaveGameManager.Current) || !FtCity()) { StopMeasurement("save_changed"); Disarm(); stopReason = "save_changed"; return; }
            if (Volatile.Read(ref offMainCallbacks) != 0 && measuring) { error = "f4b_off_main_thread_callbacks_capture_invalid"; StopMeasurement("off_main"); }
            CloseFrame(Time.frameCount, now, true);
            if (measuring && now >= nextDiagnosticQpc)
            {
                nextDiagnosticQpc = now + Stopwatch.Frequency;
                frameDiagnostic = true;
                SampleDiagnostics(now);
                Rescan();
            }
            boundaryTicks += Stopwatch.GetTimestamp() - now;
        }

        internal static void TouchFrame(long now) { if (Time.frameCount != currentFrame) CloseFrame(Time.frameCount, now, false); }

        private static void CloseFrame(int nowFrame, long now, bool fromLoop)
        {
            if (nowFrame == currentFrame) return;
            if (measuring && currentFrame >= 0 && currentFrame > firstMeasureFrame) WriteFrame(nowFrame, now, false);
            if (union.Depth != 0)
            {
                crossFrame++; union.Abandon();
                if (measuring) { error = "f4b_cross_frame_scope"; StopMeasurement("cross_frame", writeStopFrame: false); }
            }
            if (measuring && pendingWindowStart) { pendingWindowStart = false; holds.BeginWindow(nowFrame, now); }
            currentFrame = nowFrame; frameStartQpc = now; union.ResetFrame(); observerTicks = boundaryTicks = 0;
            frameDiagnostic = false; frameScene = false; frameSaves = 0; frameLate = !fromLoop;
            gcAtFrameStart = GC.CollectionCount(0); frameLoading = LoadScene.isLoading;
            frameNative = NativeMinute(); frameLogical = LogicalMinute();
        }

        private static void WriteFrame(int nextFrame, long end, bool partial)
        {
            if (!frameRing.TryWrite(out int slot)) return;
            frames[slot] = new FrameRow { frame = currentFrame, skipped = Math.Max(0, nextFrame - currentFrame - 1), qpc = frameStartQpc,
                modWorkMs = union.FrameTicks * ToMs, wholeFrameMs = (end - frameStartQpc) * ToMs, observerMs = observerTicks * ToMs,
                boundaryMs = boundaryTicks * ToMs, gc = GC.CollectionCount(0) - gcAtFrameStart, saves = frameSaves, scene = frameScene,
                loading = frameLoading, diagnostic = frameDiagnostic, lateBoundary = frameLate, partial = partial, crossFrame = !partial && union.Depth != 0,
                nativeMinute = frameNative, logicalMinute = frameLogical };
            int components = union.Components;
            for (int c = 0; c < components; c++) frameComponents[slot * components + c] = (float)(union.FrameComponentTicks(c) * ToMs);
        }

        private static void SampleDiagnostics(long now)
        {
            if (mode != "candidate" || runtime == null) return;
            try
            {
                var sample = new Sample { frame = Time.frameCount, qpc = now,
                    accounting = accountingDiagnostics == null ? null : JObject.FromObject(accountingDiagnostics.GetValue(runtime)),
                    saveEpoch = saveEpochDiagnostics == null ? null : JObject.FromObject(saveEpochDiagnostics.GetValue(runtime)) };
                if (sampleRing.TryWrite(out int slot)) samples[slot] = sample;
            }
            catch { sampleFailures++; }
        }

        private static void Rescan()
        {
            if (coverage == null) return;
            try { ScanPatches(); } catch { error = "f4b_patch_scan_failed"; }
            if (measuring && coverage.Unexpected.Count > unexpectedAtWindowStart) patchSetChanged = true;
        }

        private static void StartMeasurement(JObject args)
        {
            Require(!measuring, "f4b_already_measuring");
            Require(union.Depth == 0, "f4b_active_scope");
            string label = (string)args["window"] ?? "";
            Require(label.Length <= 64 && label.All(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.'), "f4b_invalid_window_label");
            window = label; error = ""; stopReason = "measuring";
            frameRing.Reset(); tickRing.Reset(); sampleRing.Reset(); callCount = 0; callDropped = sampleFailures = advanceCalls = 0;
            Rescan(); unexpectedAtWindowStart = coverage?.Unexpected.Count ?? 0; patchSetChanged = false;
            crossFrame = timedExceptions = 0; unknownScopeCalls = 0; totalSaves = totalSceneEvents = 0;
            union.ResetAll();
            long now = Stopwatch.GetTimestamp();
            currentFrame = firstMeasureFrame = Time.frameCount; frameStartQpc = now; nextDiagnosticQpc = now;
            observerTicks = boundaryTicks = 0; frameDiagnostic = true;
            pendingWindowStart = true; measuring = true;
        }

        private static void StopMeasurement(string reason, bool writeStopFrame = true)
        {
            if (!measuring) return;
            if (reason == "explicit-stop") Rescan();
            long now = Stopwatch.GetTimestamp();
            if (writeStopFrame && currentFrame > firstMeasureFrame) { frameDiagnostic = true; WriteFrame(currentFrame + 1, now, true); }
            if (pendingWindowStart) pendingWindowStart = false;
            else holds.EndWindow(currentFrame, now);
            measuring = false; stopReason = reason;
        }

        public static void Disarm()
        {
            if (measuring) StopMeasurement("disarm");
            measuring = false; armed = false;
            try { patches.UnpatchAll(PatchId); } catch { error = "f4b_unpatch_failed"; }
            try { RemoveLoop(); } catch { error = "f4b_player_loop_restore_failed"; }
            if (eventsSubscribed)
            {
                SceneManager.sceneLoaded -= SceneLoaded; SceneManager.sceneUnloaded -= SceneUnloaded;
                GlobalEvents.onSaveGame -= SaveObserved; eventsSubscribed = false;
            }
            union.Abandon(); shimInstalled = holdsBound = advanceBound = false; coverage = null;
            mode = "none"; runtime = null; pc = null; shimFactor = double.NaN;
            heldOrdinary = heldSkip = boundaryClips = null; withheld = null; accountingLimited = null; clockFactor = null;
            readSimulation = readState = readStopEnvironment = null; readMinute = readSeam = null;
        }

        private void OnDestroy() => Disarm();

        private static JObject Heap()
        {
            Require(!measuring, "f4b_heap_read_outside_windows_only");
            long before = GC.GetTotalMemory(false);
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            return new JObject { ["section"] = "heap", ["before_bytes"] = before, ["after_forced_gc_bytes"] = GC.GetTotalMemory(true),
                ["collections_gen0"] = GC.CollectionCount(0), ["qualification"] = "Managed (Mono) heap after a forced full collection; read outside measured windows." };
        }

        private static JObject Status()
        {
            bool candidate = mode == "candidate";
            JToken NotApplicable(Func<JToken> value) => candidate ? value() : (JToken)"n/a";
            int Count(string status) => bindings.Count(b => b.Status == status);
            JArray Names(string status) => new JArray(bindings.Where(b => b.Status == status).Select(b => b.Label).Distinct().Take(64));
            return new JObject
            {
                ["prototype"] = true, ["native_validated"] = false, ["packaged"] = false, ["mode"] = mode, ["armed"] = armed, ["measuring"] = measuring,
                ["window"] = window, ["error"] = error, ["stop_reason"] = stopReason,
                ["scope_inventory_version"] = ScopeInventory.Version, ["expected_scope_hash"] = ScopeInventory.Hash(),
                ["coverage_complete"] = NotApplicable(() => CoverageComplete()),
                ["patch_scans"] = NotApplicable(() => coverage?.Scans ?? 0), ["patch_set_changed_in_window"] = NotApplicable(() => patchSetChanged),
                ["scopes"] = NotApplicable(() => new JObject { ["timed_methods"] = scopeIndex.Count, ["timed"] = Count("timed"),
                    ["untimed_transpiled"] = Names("untimed-transpiled"),
                    ["not_installed"] = new JArray(bindings.Where(b => b.Status == "timed" && b.Expected?.Kind <= ScopeKind.Finalizer && coverage != null && !coverage.Installed(b.Expected)).Select(b => b.Label).Take(64)),
                    ["unexpected"] = new JArray((coverage?.Unexpected ?? (IReadOnlyCollection<string>)Array.Empty<string>()).Take(64)), ["unbindable"] = Names("unbindable"),
                    ["components"] = new JArray(componentNames), ["component_unbound"] = Names("component-unbound") }),
                ["holds_bound"] = NotApplicable(() => holdsBound), ["advance_records_bound"] = NotApplicable(() => advanceBound),
                ["optional_stop_seam_bound"] = NotApplicable(() => readSeam != null),
                ["calendar_factor"] = candidate ? (JToken)(clockFactor?.Invoke() ?? double.NaN) : shimFactor,
                ["pace_shim"] = mode == "reference" ? (JToken)new JObject { ["active"] = shimInstalled, ["factor"] = shimFactor,
                    ["ticks"] = shimTicks, ["multiplied"] = shimMultiplied, ["changes_native_state"] = true } : "n/a",
                ["frames"] = frameRing.Written, ["frames_drained"] = frameRing.Drained, ["frame_dropped"] = frameRing.Dropped, ["frame_capacity"] = FrameLimit,
                ["ticks"] = NotApplicable(() => tickRing.Written), ["ticks_drained"] = NotApplicable(() => tickRing.Drained), ["tick_dropped"] = NotApplicable(() => tickRing.Dropped),
                ["hold_events"] = NotApplicable(() => holds.Events.Count), ["held_now"] = NotApplicable(() => holds.Held),
                ["advance_calls"] = NotApplicable(() => advanceCalls), ["advance_records_ge30"] = NotApplicable(() => callCount),
                ["advance_record_dropped"] = NotApplicable(() => callDropped),
                ["diagnostic_samples"] = NotApplicable(() => sampleRing.Written), ["diagnostic_dropped"] = NotApplicable(() => sampleRing.Dropped),
                ["diagnostic_failures"] = NotApplicable(() => sampleFailures),
                ["cross_frame_scopes"] = crossFrame, ["timed_exceptions"] = timedExceptions, ["off_main_callbacks"] = Volatile.Read(ref offMainCallbacks),
                ["unknown_scope_calls"] = unknownScopeCalls, ["union_underflows"] = union.Underflows, ["components_outside_union"] = union.ComponentsOutsideUnion,
                ["saves_in_window"] = totalSaves, ["scene_events_in_window"] = totalSceneEvents,
                ["boundary_source"] = boundarySource, ["probe_patches_installed"] = Harmony.HasAnyPatches(PatchId), ["player_loop_entry"] = LoopEntryPresent(), ["stopwatch_frequency"] = Stopwatch.Frequency, ["stopwatch_high_resolution"] = Stopwatch.IsHighResolution,
                ["harmony_version"] = typeof(Harmony).Assembly.GetName().Version?.ToString(),
                ["limits"] = "Mod work is the nested union of every timed Persistent City scope. The union includes nested probe cost; observerMs measures only part of it. "
                    + "Native work that PC forces inside a scope stays in that scope; native work outside PC scopes is not mod-work. Transpiled methods are untimed-transpiled; "
                    + "their inserted PC helpers are timed when they are separate methods. A method the Mono JIT inlines is not observed; components that cannot be patched are "
                    + "component-unbound and the union stays authoritative. Components are attribution only and are never summed. wholeFrameMs is wall time between "
                    + "player-loop frame starts, not CPU or GPU time. Unexpected, unbindable, off-main, cross-frame, overflow or error flags invalidate a window.",
            };
        }

        private static JObject Page(JObject args)
        {
            string section = (string)args["section"];
            bool streamed = section == "frames" || section == "ticks" || section == "diagnostics";
            Require(streamed || !measuring, "f4b_stop_before_exporting");
            long offset = (long?)args["offset"] ?? 0;
            Require(offset >= 0, "f4b_invalid_offset");
            var rows = new JArray(); long count;
            if (streamed)
            {
                var ring = section == "frames" ? frameRing : section == "ticks" ? tickRing : sampleRing;
                int limit = section == "frames" ? Number(args, "limit", 64, ProbeCapacity.FramePage) : section == "ticks" ? Number(args, "limit", 128, ProbeCapacity.TickPage)
                    : Number(args, "limit", 16, ProbeCapacity.DiagnosticPage);
                Require(ring.Page(offset, limit, out int take), "f4b_rows_already_drained_or_beyond_end");
                count = ring.Written;
                int components = union.Components;
                for (long index = offset; index < offset + take; index++)
                {
                    int i = ring.Slot(index);
                    if (section == "frames")
                    {
                        var f = frames[i];
                        var row = new JObject { ["index"] = index, ["frame"] = f.frame, ["qpc"] = f.qpc, ["skipped"] = f.skipped, ["mod_work_ms"] = f.modWorkMs,
                            ["whole_frame_ms"] = f.wholeFrameMs, ["observer_ms"] = f.observerMs, ["boundary_ms"] = f.boundaryMs, ["gc"] = f.gc,
                            ["saves"] = f.saves, ["scene"] = f.scene, ["loading"] = f.loading, ["diagnostic"] = f.diagnostic,
                            ["late_boundary"] = f.lateBoundary, ["partial"] = f.partial, ["cross_frame"] = f.crossFrame, ["native_minute"] = f.nativeMinute,
                            ["logical_minute"] = double.IsNaN(f.logicalMinute) ? JValue.CreateNull() : (JToken)f.logicalMinute };
                        if (components > 0) row["components_ms"] = new JArray(Enumerable.Range(0, components).Select(c => (double)frameComponents[i * components + c]));
                        rows.Add(row);
                    }
                    else if (section == "ticks")
                    {
                        var t = ticks[i];
                        rows.Add(new JObject { ["index"] = index, ["frame"] = t.Frame, ["qpc"] = t.Qpc, ["held"] = t.Held, ["boundary_clip"] = t.BoundaryClip,
                            ["accounting_limited"] = t.AccountingLimited, ["counters_reset"] = t.CountersReset, ["withheld_minutes"] = t.WithheldMinutes,
                            ["native_minute"] = t.NativeMinute, ["logical_minute"] = double.IsNaN(t.LogicalMinute) ? JValue.CreateNull() : (JToken)t.LogicalMinute });
                    }
                    else rows.Add(new JObject { ["index"] = index, ["frame"] = samples[i].frame, ["qpc"] = samples[i].qpc,
                        ["accounting"] = samples[i].accounting, ["save_epoch"] = samples[i].saveEpoch });
                }
                return new JObject { ["section"] = section, ["rows"] = rows, ["count"] = count, ["drained"] = ring.Drained, ["dropped"] = ring.Dropped,
                    ["components"] = section == "frames" ? new JArray(componentNames) : null,
                    ["next"] = offset + rows.Count < count ? (JToken)(offset + rows.Count) : JValue.CreateNull() };
            }
            if (section == "holds")
            {
                count = holds.Events.Count; int limit = Number(args, "limit", 128, 256);
                for (int i = (int)Math.Min(offset, int.MaxValue); i < Math.Min(count, offset + limit); i++)
                {
                    var h = holds.Events[i];
                    rows.Add(new JObject { ["kind"] = h.Kind == HoldEventKind.Hold ? "hold" : "resume", ["frame"] = h.Frame, ["qpc"] = h.Qpc,
                        ["carried_in"] = h.CarriedIn, ["ongoing"] = h.Ongoing });
                }
            }
            else if (section == "calls")
            {
                count = callCount; int limit = Number(args, "limit", 128, 256);
                for (int i = (int)Math.Min(offset, int.MaxValue); i < Math.Min(count, offset + limit); i++)
                {
                    var c = calls[i];
                    rows.Add(new JObject { ["frame"] = c.Frame, ["start_qpc"] = c.StartQpc, ["duration_ms"] = c.DurationTicks * ToMs,
                        ["gc_delta"] = c.CollectionsDelta, ["target_minute"] = c.TargetMinute, ["slowest_unit"] = "unknown",
                        ["optional_stop_ms"] = double.IsNaN(c.OptionalStopMilliseconds) ? JValue.CreateNull() : (JToken)c.OptionalStopMilliseconds });
                }
            }
            else if (section == "scopes")
            {
                count = bindings.Count; int limit = Number(args, "limit", 32, 64);
                for (int i = (int)Math.Min(offset, int.MaxValue); i < Math.Min(count, offset + limit); i++)
                {
                    var b = bindings[i];
                    var row = new JObject { ["label"] = b.Label, ["kind"] = b.Kind, ["status"] = b.Status, ["owner"] = b.Owner, ["method"] = b.Method, ["target"] = b.Target };
                    if (b.Expected != null && b.Expected.Kind <= ScopeKind.Transpiler && coverage != null) row["installed"] = coverage.Installed(b.Expected);
                    if (b.Scope >= 0 && b.Scope < union.Scopes)
                    {
                        row["calls"] = union.Calls(b.Scope); row["inclusive_ms"] = union.Ticks(b.Scope) * ToMs;
                        row["max_ms"] = union.MaxTicks(b.Scope) * ToMs; row["spikes_ge30"] = union.Spikes(b.Scope);
                    }
                    rows.Add(row);
                }
            }
            else throw new InvalidOperationException("f4b_unknown_page_section");
            return new JObject { ["section"] = section, ["rows"] = rows, ["count"] = count, ["components"] = section == "frames" ? new JArray(componentNames) : null,
                ["next"] = offset + rows.Count < count ? (JToken)(offset + rows.Count) : JValue.CreateNull() };
        }
    }

    internal sealed class MethodIdentity : IEqualityComparer<MethodBase>
    {
        internal static readonly MethodIdentity Instance = new MethodIdentity();
        public bool Equals(MethodBase a, MethodBase b) => ReferenceEquals(a, b);
        public int GetHashCode(MethodBase m) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(m);
    }

    internal struct ScopeState { public long Start; public int Frame, Index; public bool Timed; }

    internal static class ScopePatch
    {
        internal static void Before(MethodBase __originalMethod, out ScopeState __state)
        {
            __state = default;
            if (!Session.measuring || !Session.MainThread()) return;
            Session.TouchFrame(Stopwatch.GetTimestamp());
            if (!Session.scopeFast.TryGetValue(__originalMethod, out int index))
            {
                if (!Session.scopeIndex.TryGetValue(__originalMethod, out index)) { Session.unknownScopeCalls++; return; }
                Session.scopeFast[__originalMethod] = index;
            }
            __state.Index = index; __state.Frame = Time.frameCount; __state.Timed = true;
            __state.Start = Stopwatch.GetTimestamp();
            Session.union.Enter(__state.Start);
        }
        internal static void After(Exception __exception, ScopeState __state)
        {
            if (!__state.Timed) return;
            long end = Stopwatch.GetTimestamp();
            if (__exception != null) Session.timedExceptions++;
            if (Time.frameCount != __state.Frame) Session.crossFrame++;
            Session.union.Exit(__state.Index, __state.Start, end);
        }
    }

    internal struct ComponentState { public long Start; public int Index; public bool Timed, Inside; }

    internal static class ComponentPatch
    {
        internal static void Before(MethodBase __originalMethod, out ComponentState __state)
        {
            __state = default;
            if (!Session.measuring || !Session.MainThread()) return;
            if (!Session.componentFast.TryGetValue(__originalMethod, out int index))
            {
                if (!Session.componentIndex.TryGetValue(__originalMethod, out index)) return;
                Session.componentFast[__originalMethod] = index;
            }
            __state.Index = index; __state.Timed = true; __state.Inside = Session.union.Depth > 0;
            __state.Start = Stopwatch.GetTimestamp();
        }
        internal static void After(ComponentState __state)
        {
            if (!__state.Timed) return;
            Session.union.Component(__state.Index, __state.Start, Stopwatch.GetTimestamp(), __state.Inside);
        }
    }

    internal struct AdvanceState { public long Start; public int Collections, Frame; public double Seam; public bool Timed; }

    internal static class AdvancePatch
    {
        internal static void Before(out AdvanceState __state)
        {
            __state = default;
            if (!Session.measuring || !Session.MainThread()) return;
            long probe = Stopwatch.GetTimestamp();
            __state.Seam = Session.SeamMilliseconds(); __state.Collections = GC.CollectionCount(0); __state.Frame = Time.frameCount; __state.Timed = true;
            __state.Start = Stopwatch.GetTimestamp();
            Session.observerTicks += __state.Start - probe;
        }
        internal static void After(double __0, AdvanceState __state)
        {
            if (!__state.Timed) return;
            long end = Stopwatch.GetTimestamp();
            Session.advanceCalls++;
            long duration = end - __state.Start;
            if (!AdvanceCallRule.Record(duration, Stopwatch.Frequency)) return;
            if (Session.callCount >= Session.calls.Length) { Session.callDropped++; return; }
            double seam = Session.SeamMilliseconds();
            Session.calls[Session.callCount++] = new AdvanceCallRecord { Frame = __state.Frame, StartQpc = __state.Start, DurationTicks = duration,
                CollectionsDelta = GC.CollectionCount(0) - __state.Collections, TargetMinute = __0,
                OptionalStopMilliseconds = double.IsNaN(seam) || double.IsNaN(__state.Seam) ? double.NaN : seam - __state.Seam };
            Session.observerTicks += Stopwatch.GetTimestamp() - end;
        }
    }

    internal static class HoldPatch
    {
        internal static void Before(out ClockCounters __state)
        {
            __state = default;
            if (!Session.armed || !Session.MainThread() || Session.heldOrdinary == null) return;
            long probe = Stopwatch.GetTimestamp();
            __state = new ClockCounters(Session.heldOrdinary(), Session.heldSkip(), Session.boundaryClips(), Session.withheld());
            if (Session.measuring) Session.observerTicks += Stopwatch.GetTimestamp() - probe;
        }
        internal static void After(ClockCounters __state)
        {
            if (!Session.armed || !Session.MainThread() || Session.heldOrdinary == null) return;
            long now = Stopwatch.GetTimestamp();
            var after = new ClockCounters(Session.heldOrdinary(), Session.heldSkip(), Session.boundaryClips(), Session.withheld());
            bool record = Session.measuring && Session.holds.WindowOpen;
            var tick = Session.holds.Observe(__state, after, Session.accountingLimited(), Time.frameCount, now,
                record ? Session.NativeMinute() : double.NaN, record ? Session.LogicalMinute() : double.NaN);
            if (record)
            {
                if (Session.tickRing.TryWrite(out int slot)) Session.ticks[slot] = tick;
            }
            if (Session.measuring) Session.observerTicks += Stopwatch.GetTimestamp() - now;
        }
    }

    internal static class ShimPatch
    {
        internal static void Prefix(ref float __0)
        {
            if (!Session.armed || !Session.MainThread()) return;
            var machine = InstanceBehavior<UIs>.Instance?.timeMachine;
            float before = __0;
            __0 = PaceShim.Apply(__0, LoadScene.isLoading, machine != null, machine != null && machine.isRunning, Session.shimFactor);
            Session.shimTicks++;
            if (__0 != before) Session.shimMultiplied++;
        }
    }
}
